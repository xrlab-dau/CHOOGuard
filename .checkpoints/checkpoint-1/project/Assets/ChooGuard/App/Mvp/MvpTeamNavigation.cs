using System;
using System.IO;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using UnityEngine;
namespace ChooGuard.App.Mvp
{
    // v1: actual station second floor only. No off-mesh/cross-floor links.
    public sealed class MvpTeamNavigation : MonoBehaviour
    {
        public TextAsset NavData;
        public string GeometryDigest;
        private DtNavMeshQuery query;
        public string LastReason { get; private set; } = "navigation_not_loaded";
        public void Reload() { query=null; }
        static RcVec3f Rc(Vector3 p)=>new RcVec3f(p.x,p.y,p.z);
        static Vector3 Unity(RcVec3f p)=>new Vector3(p.X,p.Y,p.Z);
        public bool TryPlan(Vector3 start,Vector3 end,out Vector3[] corners,out string reason)
        {
            corners=Array.Empty<Vector3>();reason="";
            if(Mathf.Abs(start.y-5.05f)>.3f||Mathf.Abs(end.y-5.05f)>.3f)reason="unsupported_floor_v1_no_cross_floor_link";
            else if(NavData==null)reason="navigation_not_baked";
            else try
            {
                if(query==null){using(var reader=new BinaryReader(new MemoryStream(NavData.bytes))){var data=new DtMeshDataReader().Read(reader,6);var mesh=new DtNavMesh();if(!mesh.Init(data,6,0).Succeeded())throw new InvalidDataException("navmesh_init_failed");query=new DtNavMeshQuery(mesh);}}
                var filter=new DtQueryDefaultFilter();var ext=new RcVec3f(.25f,.3f,.25f);
                var a=query.FindNearestPoly(Rc(start),ext,filter,out var ar,out var ap,out var _);
                var b=query.FindNearestPoly(Rc(end),ext,filter,out var br,out var bp,out var _);
                if(!a.Succeeded()||!b.Succeeded()||ar==0||br==0)reason="endpoint_outside_walkable_surface";
                else if(Vector2.Distance(new Vector2(start.x,start.z),new Vector2(ap.X,ap.Z))>.12f||Vector2.Distance(new Vector2(end.x,end.z),new Vector2(bp.X,bp.Z))>.12f)reason="endpoint_projection_too_far";
                else
                {
                    var path=new long[2048];var status=query.FindPath(ar,br,ap,bp,filter,path,out var count,path.Length);
                    if(!status.Succeeded()||count==0||path[count-1]!=br)reason="unreachable_or_partial_path";
                    else
                    {
                        var straight=new DtStraightPath[512];status=query.FindStraightPath(ap,bp,path,count,straight,out var n,straight.Length,0);
                        if(!status.Succeeded()||n<1||(Unity(straight[n-1].pos)-Unity(bp)).sqrMagnitude>.01f)reason="incomplete_corner_path";
                        else {corners=new Vector3[Math.Max(2,n)];for(int i=0;i<n;i++){corners[i]=Unity(straight[i].pos);corners[i].y=5.05f;}corners[0]=start;corners[corners.Length-1]=end;reason="ready_second_floor_v1";LastReason=reason;return true;}
                    }
                }
            }
            catch(Exception ex){reason="navigation_error:"+ex.GetType().Name+":"+ex.Message;}
            LastReason=reason;return false;
        }
    }
}
