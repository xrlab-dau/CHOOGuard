using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace ChooGuard.Editor
{
    // Explicit metre-native coverage; convex cells must preserve holes in source coverage.
    public sealed class MvpSourceCoverage
    {
        [Serializable] public sealed class Point { public float x,z; }
        [Serializable] public sealed class Polygon { public Point[] points; public float groundY; }
        [Serializable] public sealed class Contract { public string sourceSha256,registrationHash,units; public Polygon[] surfacePolygons,buildingPolygons; public string[] excludedBuildingIds; }
        readonly Contract data;
        readonly Dictionary<Vector2Int,List<Polygon>> surfaces=new Dictionary<Vector2Int,List<Polygon>>(),buildings=new Dictionary<Vector2Int,List<Polygon>>();
        public static MvpSourceCoverage Active { get; private set; }
        public string RegistrationHash=>data.registrationHash;
        public string SourceHash=>data.sourceSha256;
        public int clippedTriangles,excludedBuildings,excludedProps;
        public static void Activate(string path,string registrationHash)
        {
            var contract=JsonUtility.FromJson<Contract>(File.ReadAllText(path));
            if(contract==null)throw new InvalidDataException("Missing official coverage contract");
            if(!Hash(contract.sourceSha256)||!Hash(contract.registrationHash)||!Hash(registrationHash))throw new InvalidDataException("Coverage requires 64 hexadecimal SHA256 hashes");
            if(contract.surfacePolygons==null||contract.surfacePolygons.Length==0||contract.buildingPolygons==null||contract.buildingPolygons.Length==0)throw new InvalidDataException("Coverage surface and building masks must not be empty");
            if(contract.units!="station-local-metres"||contract.registrationHash!=registrationHash||string.IsNullOrEmpty(contract.sourceSha256))throw new InvalidDataException("Official coverage registration/units mismatch");
            Active=new MvpSourceCoverage(contract);
        }
        static bool Hash(string value)=>value!=null&&value.Length==64&&value.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f')||(c>='A'&&c<='F'));
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static void Clear(){Active=null;}
        MvpSourceCoverage(Contract contract){data=contract;Index(data.surfacePolygons,surfaces);Index(data.buildingPolygons,buildings);}
        static Vector2Int Cell(float x,float z)=>new Vector2Int(Mathf.FloorToInt(x/32),Mathf.FloorToInt(z/32));
        static void Index(Polygon[] polygons,Dictionary<Vector2Int,List<Polygon>> index)
        {
            if(polygons==null)return;
            foreach(var polygon in polygons)
            {
                if(polygon==null||!Finite(polygon.groundY))throw new InvalidDataException("Coverage polygon groundY must be finite");
                if(polygon.points==null||polygon.points.Length<3)throw new InvalidDataException("Coverage polygon requires three points");
                if(polygon.points.Any(p=>p==null||!Finite(p.x)||!Finite(p.z)))throw new InvalidDataException("Coverage coordinates must be finite");
                double area=0;var origin=polygon.points[0];for(int i=0;i<polygon.points.Length;i++){var a=polygon.points[i];var b=polygon.points[(i+1)%polygon.points.Length];area+=((double)a.x-origin.x)*((double)b.z-origin.z)-((double)b.x-origin.x)*((double)a.z-origin.z);}
                if(double.IsNaN(area)||double.IsInfinity(area)||area<=0)throw new InvalidDataException("Coverage polygons must be CCW");
                for(int i=0;i<polygon.points.Length;i++){var a=polygon.points[i];var b=polygon.points[(i+1)%polygon.points.Length];var c=polygon.points[(i+2)%polygon.points.Length];if(((double)b.x-a.x)*((double)c.z-b.z)-((double)b.z-a.z)*((double)c.x-b.x)<0)throw new InvalidDataException("Coverage polygons must be convex");}
                var min=Cell(polygon.points.Min(p=>p.x),polygon.points.Min(p=>p.z));var max=Cell(polygon.points.Max(p=>p.x),polygon.points.Max(p=>p.z));
                for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++){var key=new Vector2Int(x,z);if(!index.TryGetValue(key,out var list)){list=new List<Polygon>();index[key]=list;}list.Add(polygon);}
            }
        }
        static IEnumerable<Polygon> Candidates(IList<Vector3> points,Dictionary<Vector2Int,List<Polygon>> index)
        {
            var found=new HashSet<Polygon>();var min=Cell(points.Min(p=>p.x),points.Min(p=>p.z));var max=Cell(points.Max(p=>p.x),points.Max(p=>p.z));
            for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++)if(index.TryGetValue(new Vector2Int(x,z),out var list))foreach(var p in list)if(found.Add(p))yield return p;
        }
        static double Distance(Vector3 p,Point a,Point b)=>((double)b.x-a.x)*((double)p.z-a.z)-((double)b.z-a.z)*((double)p.x-a.x);
        static List<Vector3> Half(List<Vector3> polygon,Point a,Point b,bool inside)
        {
            var output=new List<Vector3>();for(int i=0;i<polygon.Count;i++){var p=polygon[i];var q=polygon[(i+1)%polygon.Count];double dp=Distance(p,a,b),dq=Distance(q,a,b);bool ip=inside?dp>=0:dp<=0,iq=inside?dq>=0:dq<=0;if(ip)output.Add(p);if(ip!=iq)output.Add(Vector3.LerpUnclamped(p,q,(float)(dp/(dp-dq))));}return output;
        }
        static List<Vector3> Intersection(List<Vector3> points,Polygon mask){for(int i=0;i<mask.points.Length&&points.Count>=3;i++)points=Half(points,mask.points[i],mask.points[(i+1)%mask.points.Length],true);return points;}
        static double Area(List<Vector3> p){if(p.Count<3)return 0;double a=0;var origin=p[0];for(int i=0;i<p.Count;i++)a+=((double)p[i].x-origin.x)*((double)p[(i+1)%p.Count].z-origin.z)-((double)p[(i+1)%p.Count].x-origin.x)*((double)p[i].z-origin.z);return Math.Abs(a)*.5;}
        public bool ExcludeBuilding(string id,IEnumerable<Vector2> footprint)
        {
            var p=footprint.Select(v=>new Vector3(v.x,0,v.y)).ToList();
            bool excluded=(data.excludedBuildingIds!=null&&Array.IndexOf(data.excludedBuildingIds,id)>=0)||Candidates(p,buildings).Any(m=>Area(Intersection(p,m))>.01f);
            if(excluded)excludedBuildings++;return excluded;
        }
        public bool ExcludeProp(float x,float z,float width,float depth)
        {
            float radius=Mathf.Max(.5f,Mathf.Sqrt(width*width+depth*depth)*.5f);var p=new List<Vector3>{new Vector3(x-radius,0,z-radius),new Vector3(x+radius,0,z-radius),new Vector3(x+radius,0,z+radius),new Vector3(x-radius,0,z+radius)};
            bool excluded=Candidates(p,surfaces).Concat(Candidates(p,buildings)).Any(m=>Area(Intersection(p,m))>.01f);if(excluded)excludedProps++;return excluded;
        }
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,List<Vector3> output,bool retainGround=false)
        {
            var input=new List<Vector3>{a,b,c};var pieces=new List<List<Vector3>>{input};bool touched=false;
            foreach(var mask in Candidates(input,surfaces))
            {
                var next=new List<List<Vector3>>();foreach(var piece in pieces)
                {
                    if(Area(Intersection(piece,mask))<=0){next.Add(piece);continue;}touched=true;var remaining=piece;
                    for(int i=0;i<mask.points.Length&&remaining.Count>=3;i++)
                    {var outside=Half(remaining,mask.points[i],mask.points[(i+1)%mask.points.Length],false);if(Area(outside)>0)next.Add(outside);remaining=Half(remaining,mask.points[i],mask.points[(i+1)%mask.points.Length],true);}
                    if(retainGround&&Area(remaining)>0){for(int i=0;i<remaining.Count;i++){var v=remaining[i];v.y=Mathf.Min(v.y,mask.groundY-.08f);remaining[i]=v;}Fan(remaining,output);}
                }pieces=next;
            }
            foreach(var piece in pieces)Fan(piece,output);if(touched)clippedTriangles++;
        }
        static void Fan(List<Vector3> polygon,List<Vector3> output){for(int i=1;i+1<polygon.Count;i++){if(Vector3.Cross(polygon[i]-polygon[0],polygon[i+1]-polygon[0]).sqrMagnitude<1e-12f)continue;output.Add(polygon[0]);output.Add(polygon[i]);output.Add(polygon[i+1]);}}
    }
}
