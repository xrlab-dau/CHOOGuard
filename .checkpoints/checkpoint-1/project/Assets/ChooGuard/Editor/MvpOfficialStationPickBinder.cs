using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChooGuard.Editor
{
    // Camera-query approximation only. Never used by the external crowd/physical solver.
    public static class MvpOfficialStationPickBinder
    {
        private const string Folder="Assets/ChooGuard/Art/OfficialBusanStation/Picking/";
        private const string ProxyName="Official station camera query proxy";
        private const float Cell=2;
        private const int MaxTriangles=24000;
        [Serializable] private sealed class Geometry { public int version=1;public string targetId=MvpAgencyDispatchController.StationTarget;public Vector3 center,size;public string[] sourceSegments;public string boundary="Source MainShell+ExitRoofs bounds select the existing reference-hall task; not whole-site interior/solver coverage."; }
        [Serializable] private sealed class Receipt { public string status,scope;public Vector3 center,size;public int sourceTriangles,proxyTriangles;public float cellMetres;public bool functionalAnchorsUnchanged;public string[] segments; }
        private sealed class Source { public Vector3[] Vertices;public int[] Indices; }
        [MenuItem("ChooGuard/MVP/Bind Official Station Picking")]
        public static void BindExisting()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit mode after official-world publication.");
            var view=UnityEngine.Object.FindFirstObjectByType<MvpStationView>();var controller=UnityEngine.Object.FindFirstObjectByType<MvpAgencyDispatchController>();
            if(view==null||controller==null||view.WholeEnvelope==null)throw new InvalidOperationException("Existing station/controller required.");
            if((view.transform.lossyScale-Vector3.one).sqrMagnitude>.00001f||(view.WholeEnvelope.lossyScale-Vector3.one).sqrMagnitude>.00001f)throw new InvalidOperationException("Existing station frames must remain metre-native.");
            var anchors=new[]{view.PlatformAnchor,view.ConcourseAnchor,view.ExitAnchor,view.CrowdRoot}.Concat(view.FloorRoots??Array.Empty<Transform>()).Concat(view.Teams??Array.Empty<Transform>()).Where(x=>x!=null).ToArray();var matrices=anchors.Select(x=>x.localToWorldMatrix).ToArray();
            var official=view.WholeEnvelope.Find("공식 자료 부산역 역사");if(official==null)throw new InvalidOperationException("Publish official source shell first.");
            var names=new[]{"MainShell","ExitRoofs"};var sources=new List<Source>();bool first=true;Bounds bounds=default;int sourceTriangles=0;
            foreach(var name in names)
            {
                var segment=official.Find(name);if(segment==null)throw new InvalidOperationException("Required official source segment missing: "+name);
                foreach(var f in segment.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(f.sharedMesh==null||!f.sharedMesh.isReadable)throw new InvalidOperationException("Readable source mesh required.");
                    var matrix=view.transform.worldToLocalMatrix*f.transform.localToWorldMatrix;var vertices=f.sharedMesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();
                    foreach(var v in vertices){if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}
                    var indices=f.sharedMesh.triangles;sourceTriangles+=indices.Length/3;sources.Add(new Source{Vertices=vertices,Indices=indices});
                }
            }
            if(first||bounds.size.y<1)throw new InvalidOperationException("Official bounds unavailable.");
            int nx=Mathf.CeilToInt(bounds.size.x/Cell),nz=Mathf.CeilToInt(bounds.size.z/Cell);if(nx*nz>20000)throw new InvalidOperationException("Camera proxy grid budget exceeded.");
            var heights=Enumerable.Repeat(float.NegativeInfinity,nx*nz).ToArray();
            foreach(var source in sources)for(int i=0;i<source.Indices.Length;i+=3)
            {
                var a=source.Vertices[source.Indices[i]];var b=source.Vertices[source.Indices[i+1]];var c=source.Vertices[source.Indices[i+2]];var normal=Vector3.Cross(b-a,c-a);
                if(normal.sqrMagnitude<.000001f||Mathf.Abs(normal.y)<normal.magnitude*.15f)continue;
                float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(denominator)<.000001f)continue;
                int minX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x,c.x)-bounds.min.x)/Cell),0,nx-1),maxX=Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.x,b.x,c.x)-bounds.min.x)/Cell),0,nx-1);
                int minZ=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,b.z,c.z)-bounds.min.z)/Cell),0,nz-1),maxZ=Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.z,b.z,c.z)-bounds.min.z)/Cell),0,nz-1);
                for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
                {
                    float px=bounds.min.x+(x+.5f)*Cell,pz=bounds.min.z+(z+.5f)*Cell;
                    float u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/denominator,v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/denominator,w=1-u-v;
                    if(u<-.0001f||v<-.0001f||w<-.0001f)continue;heights[z*nx+x]=Mathf.Max(heights[z*nx+x],u*a.y+v*b.y+w*c.y);
                }
            }
            var points=new List<Vector3>();var triangles=new List<int>();
            Action<Vector3,Vector3,Vector3> add=(a,b,c)=>{int start=points.Count;points.Add(a);points.Add(b);points.Add(c);triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);};
            Action<Vector3,Vector3> wall=(a,b)=>{var lowA=new Vector3(a.x,bounds.min.y,a.z);var lowB=new Vector3(b.x,bounds.min.y,b.z);add(a,b,lowB);add(a,lowB,lowA);};
            Func<int,int,bool> present=(x,z)=>x>=0&&z>=0&&x<nx&&z<nz&&!float.IsNegativeInfinity(heights[z*nx+x]);
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)if(present(x,z))
            {
                float px=bounds.min.x+x*Cell,pz=bounds.min.z+z*Cell,h=heights[z*nx+x];var a=new Vector3(px,h,pz);var b=new Vector3(Mathf.Min(px+Cell,bounds.max.x),h,pz);var c=new Vector3(b.x,h,Mathf.Min(pz+Cell,bounds.max.z));var d=new Vector3(px,h,c.z);
                add(a,c,b);add(a,d,c);if(!present(x,z-1))wall(a,b);if(!present(x+1,z))wall(b,c);if(!present(x,z+1))wall(c,d);if(!present(x-1,z))wall(d,a);
            }
            if(triangles.Count==0||triangles.Count/3>MaxTriangles)throw new InvalidOperationException("Camera query proxy exceeds bounded triangle budget; retain previous binding.");
            var mesh=new Mesh{name="Official station source-derived camera proxy",indexFormat=IndexFormat.UInt32};mesh.SetVertices(points);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.UploadMeshData(false);
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();mesh=MvpMeshPersistence.Store(Folder+"station-camera-query.asset",mesh);
            var staging=new GameObject(ProxyName+" staging",typeof(MeshCollider));staging.transform.SetParent(view.WholeEnvelope,false);staging.layer=29;staging.transform.position=view.transform.position;staging.transform.rotation=view.transform.rotation;staging.transform.localScale=Vector3.one;staging.GetComponent<MeshCollider>().sharedMesh=mesh;
            staging.SetActive(false);
            var geometry=new Geometry{center=bounds.center,size=bounds.size,sourceSegments=names};string jsonPath=Folder+"station-target-geometry.json";File.WriteAllText(jsonPath,JsonUtility.ToJson(geometry,true));AssetDatabase.ImportAsset(jsonPath,ImportAssetOptions.ForceUpdate);var pickAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);if(pickAsset==null){UnityEngine.Object.DestroyImmediate(staging);throw new InvalidOperationException("Station pick asset import failed.");}
            bool unchanged=anchors.Select(x=>x.localToWorldMatrix).SequenceEqual(matrices);if(!unchanged){UnityEngine.Object.DestroyImmediate(staging);throw new InvalidOperationException("Functional anchors moved during query-proxy build.");}
            var previous=view.WholeEnvelope.Find(ProxyName);if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);staging.name=ProxyName;controller.OfficialStationPickAsset=pickAsset;staging.SetActive(true);
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();Directory.CreateDirectory("art/world");File.WriteAllText("art/world/official-station-picking-receipt.json",JsonUtility.ToJson(new Receipt{status="SOURCE_DERIVED_PICK_AND_BOUNDED_QUERY_PROXY_NATIVE_CHECK_PENDING",scope="MainShell+ExitRoofs only; wholecontext/trains excluded; camera query, not gameplay floor/crowd solver geometry",center=bounds.center,size=bounds.size,sourceTriangles=sourceTriangles,proxyTriangles=triangles.Count/3,cellMetres=Cell,functionalAnchorsUnchanged=unchanged,segments=names},true));EditorSceneManager.MarkSceneDirty(view.gameObject.scene);EditorSceneManager.SaveScene(view.gameObject.scene);
        }
    }
}
