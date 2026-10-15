#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChooGuard.Editor
{
    public static class FpsAuthoredTransferBuilder
    {
        [Serializable] public class Manifest {public PathSpec[] paths;}
        [Serializable] public class PathSpec {public string id;public float width_m=3,clearance_m=2.6f,floor_thickness_m=.15f;public Waypoint[] waypoints;}
        [Serializable] public class Waypoint {public string id;public float[] xyz;public Vector3 Point=>new Vector3(xyz[0],xyz[1],xyz[2]);}
        // Visible geometry and matching box colliders: authored prototype, never survey/as-built.
        public static List<Collider> Build(Transform parent,Manifest manifest,Material floorMaterial,Material railMaterial)
        {
            var support=new List<Collider>();
            foreach(var path in manifest.paths)
            {
                var root=new GameObject(path.id+" [프로토타입 연결 통로 · 깊이 미측량]");root.transform.SetParent(parent,false);
                if(path.waypoints==null||path.waypoints.Length<2||path.width_m<1||path.clearance_m<2.6f||path.floor_thickness_m<.05f||path.floor_thickness_m>.3f)throw new ArgumentException("Invalid transfer dimensions");
                for(int i=0;i<path.waypoints.Length;i++)
                {
                    var at=path.waypoints[i].Point;
                    // Flat square junctions make adjacent differently oriented flights overlap visibly.
                    support.Add(Box(root.transform,"착지 "+path.waypoints[i].id,at-Vector3.up*(path.floor_thickness_m*.5f),new Vector3(path.width_m,path.floor_thickness_m,path.width_m),Quaternion.identity,floorMaterial));
                    if(i==0)continue;
                    var a=path.waypoints[i-1].Point;var b=at;var delta=b-a;var horizontal=new Vector3(delta.x,0,delta.z);float run=horizontal.magnitude;
                    if(run<.1f)throw new ArgumentException("Vertical-only transfer segment forbidden");
                    var direction=horizontal/run;
                    float inset=(path.width_m*.5f)/Mathf.Max(Mathf.Abs(direction.x),Mathf.Abs(direction.z));
                    if(run<=2*inset+.3f)throw new ArgumentException("Transfer waypoints too close for flat landing footprints");
                    a+=direction*inset;b-=direction*inset;delta=b-a;horizontal=new Vector3(delta.x,0,delta.z);run=horizontal.magnitude;
                    var rotation=Quaternion.LookRotation(horizontal/run,Vector3.up);
                    int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(delta.y)/.16f));float tread=run/steps;
                    if(tread<.28f)throw new ArgumentException("Stair tread too short");
                    for(int step=0;step<steps;step++)
                    {
                        float t0=(float)step/steps,t1=(float)(step+1)/steps;
                        // Descending uses start height, ascending uses end: first/last risers <=0.16m.
                        float top=Mathf.Max(Mathf.Lerp(a.y,b.y,t0),Mathf.Lerp(a.y,b.y,t1));
                        var center=Vector3.Lerp(a,b,(t0+t1)*.5f);center.y=top-path.floor_thickness_m*.5f;
                        support.Add(Box(root.transform,"디딤판 "+i+"-"+step,center,new Vector3(path.width_m,path.floor_thickness_m,tread+.02f),rotation,floorMaterial));
                    }
                    // Continuous visible side safety walls on each segment; open at landings.
                    var along=delta.normalized;var side=Vector3.Cross(Vector3.up,horizontal.normalized);var wallRotation=Quaternion.LookRotation(along,Vector3.up);
                    foreach(float sign in new[]{-1f,1f})Box(root.transform,"측면 안전벽",(a+b)*.5f+side*(path.width_m*.5f+.06f)*sign+Vector3.up*.55f,new Vector3(.12f,1.1f,delta.magnitude),wallRotation,railMaterial);
                }
            }
            return support;
        }
        private static Collider Box(Transform parent,string name,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(position,rotation);go.transform.localScale=scale;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.GetComponent<Collider>();
        }
    }
}
#endif
