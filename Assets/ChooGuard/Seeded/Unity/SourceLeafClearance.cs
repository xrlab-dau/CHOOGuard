using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChooGuard.Seeded.Unity
{
    public sealed class SourceLeafClearanceResult
    {
        public double Clearance=1;
        public string Method="indexed source triangle surface samples; bidirectional collider-ray narrow phase; actual NPC capsule skin sweep",Reason="sampled_surface_clear";
        public bool Qualified=true;
        public int SampleCount,TriangleCount,CandidateCount,RayTests,InitialContacts;
        public float SampleSpacing,Skin;
        public Vector3 Delta;
        public readonly JArray Blockers=new JArray();
        public JObject Json()=>new JObject{["method"]=Method,["qualified"]=Qualified,["reason"]=Reason,["clearance_fraction"]=Clearance,["sample_count"]=SampleCount,["indexed_triangles"]=TriangleCount,["broad_candidates"]=CandidateCount,["narrow_tests"]=RayTests,["baseline_tangent_or_separating_contacts"]=InitialContacts,["surface_sample_spacing"]=SampleSpacing,["skin"]=Skin,["sweep_delta"]=PlatformSeededSimulator.Point(Delta),["blocking_colliders"]=Blockers.DeepClone(),["claim"]="Measured sampled source-surface sweep; finite sample resolution, not a proof of all continuous mesh contacts"};
    }
    /// <summary>Read-only source geometry query. Broad overlap bounds never determine clearance.</summary>
    public static class SourceLeafClearance
    {
        sealed class SampleCache {public Mesh Mesh;public Matrix4x4 Matrix;public float Spacing;public Vector3[] Samples;public int Triangles;}
        static readonly Dictionary<int,SampleCache> cache=new Dictionary<int,SampleCache>();
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static bool Finite(Vector3 p)=>Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        static bool SameLinear(Matrix4x4 a,Matrix4x4 b)
        {for(int r=0;r<3;r++)for(int c=0;c<3;c++)if(a[r,c]!=b[r,c])return false;return true;}
        static SourceLeafClearanceResult Unsupported(SourceLeafClearanceResult r,string reason){r.Qualified=false;r.Clearance=0;r.Reason=reason;return r;}
        static bool Person(Collider c)=>c.GetComponentInParent<SeededNpcController>()!=null||c is CharacterController;
        static bool Owned(Collider collider,Transform[] leaves,string owner)
        {
            if(leaves.Any(t=>collider.transform==t||collider.transform.IsChildOf(t)))return true;
            var part=collider.GetComponentInParent<DetailedPartBinding>();
            return part!=null&&part.TrainingProxy&&(owner==null||part.OwnerId==owner);
        }
        static SampleCache Samples(Transform leaf,float spacing,int budget,out string error)
        {
            error=null;var filter=leaf.GetComponent<MeshFilter>();var mesh=filter?.sharedMesh??(leaf.GetComponent<MeshCollider>()?.sharedMesh);
            if(mesh==null||!mesh.isReadable){error="unreadable_or_missing_source_leaf_mesh";return null;}
            Matrix4x4 matrix=leaf.localToWorldMatrix;
            if(cache.TryGetValue(leaf.GetInstanceID(),out var saved)&&saved.Mesh==mesh&&saved.Spacing==spacing&&SameLinear(saved.Matrix,matrix))
            {if(saved.Samples.Length>budget){error="surface_sample_budget_exceeded";return null;}return saved;}
            var vertices=mesh.vertices;var indices=mesh.triangles;var points=new List<Vector3>();var unique=new HashSet<string>(StringComparer.Ordinal);int triangles=0;
            Action<Vector3> add=p=>
            {
                var world=matrix.MultiplyPoint3x4(p);
                // Deduplication retains an actual point on the indexed source triangle.
                string key=Math.Round(world.x*100000).ToString(CultureInfo.InvariantCulture)+"|"+Math.Round(world.y*100000).ToString(CultureInfo.InvariantCulture)+"|"+Math.Round(world.z*100000).ToString(CultureInfo.InvariantCulture);
                if(unique.Add(key))points.Add(p);
            };
            for(int i=0;i+2<indices.Length;i+=3)
            {
                int ia=indices[i],ib=indices[i+1],ic=indices[i+2];
                if(ia<0||ib<0||ic<0||ia>=vertices.Length||ib>=vertices.Length||ic>=vertices.Length){error="invalid_source_triangle_index";return null;}
                var a=vertices[ia];var b=vertices[ib];var c=vertices[ic];var wa=matrix.MultiplyPoint3x4(a);var wb=matrix.MultiplyPoint3x4(b);var wc=matrix.MultiplyPoint3x4(c);
                if(!Finite(wa)||!Finite(wb)||!Finite(wc)){error="nonfinite_source_surface";return null;}
                if(Vector3.Cross(wb-wa,wc-wa).sqrMagnitude<1e-18f)continue;
                triangles++;add(a);add(b);add(c);add((a+b+c)/3);
                int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Vector3.Distance(wa,wb),Math.Max(Vector3.Distance(wb,wc),Vector3.Distance(wc,wa)))/spacing));
                if(steps>512){error="surface_sampling_edge_limit";return null;}
                for(int u=0;u<=steps;u++)for(int v=0;v<=steps-u;v++)
                {add(a+(b-a)*(u/(float)steps)+(c-a)*(v/(float)steps));if(points.Count>budget){error="surface_sample_budget_exceeded";return null;}}
            }
            if(points.Count==0){error="empty_indexed_leaf_surface";return null;}
            if(cache.Count>128)cache.Clear();saved=new SampleCache{Mesh=mesh,Matrix=matrix,Spacing=spacing,Samples=points.ToArray(),Triangles=triangles};cache[leaf.GetInstanceID()]=saved;return saved;
        }
        static bool SegmentBounds(Vector3 point,Vector3 delta,Bounds box,float skin)
        {
            Vector3 end=point+delta;box.Expand(skin*2);
            return Math.Min(point.x,end.x)<=box.max.x&&Math.Max(point.x,end.x)>=box.min.x&&Math.Min(point.y,end.y)<=box.max.y&&Math.Max(point.y,end.y)>=box.min.y&&Math.Min(point.z,end.z)<=box.max.z&&Math.Max(point.z,end.z)>=box.min.z;
        }
        static bool InitialBox(BoxCollider box,Vector3 point,Vector3 direction,float skin,out bool entering)
        {
            entering=false;Vector3 p=box.transform.InverseTransformPoint(point)-box.center,h=box.size*.5f;
            if(Math.Abs(p.x)>h.x||Math.Abs(p.y)>h.y||Math.Abs(p.z)>h.z)return false;
            var scale=box.transform.lossyScale;float x=(h.x-Math.Abs(p.x))*Math.Abs(scale.x),y=(h.y-Math.Abs(p.y))*Math.Abs(scale.y),z=(h.z-Math.Abs(p.z))*Math.Abs(scale.z);
            Vector3 local=x<=y&&x<=z?new Vector3(p.x<0?-1:1,0,0):y<=z?new Vector3(0,p.y<0?-1:1,0):new Vector3(0,0,p.z<0?-1:1);
            Vector3 normal=box.transform.worldToLocalMatrix.transpose.MultiplyVector(local).normalized;
            entering=Vector3.Dot(normal,direction)<-1e-4f;return true;
        }
        static float SphereHit(Vector3 point,Vector3 direction,float length,Vector3 center,float radius)
        {
            Vector3 relative=point-center;float b=Vector3.Dot(relative,direction),c=relative.sqrMagnitude-radius*radius,discriminant=b*b-c;
            if(discriminant<0)return float.PositiveInfinity;float t=-b-Mathf.Sqrt(discriminant);
            return t>=0&&t<=length?t:float.PositiveInfinity;
        }
        static float CapsuleHit(Collider collider,Vector3 point,Vector3 direction,float length,float skin,out bool baseline,out bool entering)
        {
            Vector3 center,axis;float radius,height;
            var capsule=collider as CapsuleCollider;var character=collider as CharacterController;
            if(capsule!=null)
            {
                Vector3 localAxis=capsule.direction==0?Vector3.right:capsule.direction==2?Vector3.forward:Vector3.up;
                var scale=capsule.transform.lossyScale;float along=capsule.direction==0?Math.Abs(scale.x):capsule.direction==2?Math.Abs(scale.z):Math.Abs(scale.y),across=capsule.direction==0?Math.Max(Math.Abs(scale.y),Math.Abs(scale.z)):capsule.direction==2?Math.Max(Math.Abs(scale.x),Math.Abs(scale.y)):Math.Max(Math.Abs(scale.x),Math.Abs(scale.z));
                center=capsule.transform.TransformPoint(capsule.center);axis=capsule.transform.TransformDirection(localAxis).normalized;radius=capsule.radius*across+skin;height=capsule.height*along;
            }
            else
            {
                var scale=character.transform.lossyScale;center=character.transform.TransformPoint(character.center);axis=character.transform.up;radius=character.radius*Math.Max(Math.Abs(scale.x),Math.Abs(scale.z))+skin;height=character.height*Math.Abs(scale.y);
            }
            float half=Math.Max(0,height*.5f-(radius-skin));Vector3 a=center-axis*half,b=center+axis*half,pa=point-a;float axial=Vector3.Dot(pa,axis),span=half*2;Vector3 closest=a+axis*Mathf.Clamp(axial,0,span),normal=point-closest;
            baseline=normal.sqrMagnitude<=radius*radius;entering=baseline&&(normal.sqrMagnitude<1e-14f||Vector3.Dot(normal.normalized,direction)<-1e-4f);
            if(baseline)return entering?0:float.PositiveInfinity;
            float best=Math.Min(SphereHit(point,direction,length,a,radius),SphereHit(point,direction,length,b,radius));
            Vector3 q=pa-axis*axial,d=direction-axis*Vector3.Dot(direction,axis);float aa=d.sqrMagnitude,bb=Vector3.Dot(q,d),cc=q.sqrMagnitude-radius*radius,disc=bb*bb-aa*cc;
            if(aa>1e-15f&&disc>=0)
            {float t=(-bb-Mathf.Sqrt(disc))/aa,along=axial+t*Vector3.Dot(direction,axis);if(t>=0&&t<=length&&along>=0&&along<=span)best=Math.Min(best,t);}
            return best;
        }
        public static SourceLeafClearanceResult Measure(IEnumerable<Transform> ownedLeaves,Vector3 worldDelta,string ownerId=null,Func<Collider,string> sourceIdentity=null,float sampleSpacing=.06f,float skin=.0015f,int maxSamples=64000)
        {
            var result=new SourceLeafClearanceResult{Delta=worldDelta,SampleSpacing=sampleSpacing,Skin=skin};var leaves=(ownedLeaves??Array.Empty<Transform>()).Where(t=>t!=null).Distinct().ToArray();
            if(!Finite(worldDelta)||!Finite(sampleSpacing)||sampleSpacing<=0||!Finite(skin)||skin<=0||maxSamples<1||leaves.Length==0)return Unsupported(result,"invalid_sweep_contract");
            float length=worldDelta.magnitude;if(!Finite(length))return Unsupported(result,"nonfinite_sweep_length");if(length<1e-7f){result.Reason="zero_translation";return result;}Vector3 direction=worldDelta/length;
            var candidates=new HashSet<Collider>();var blocked=new Dictionary<Collider,JObject>();
            foreach(var leaf in leaves)
            {
                var samples=Samples(leaf,sampleSpacing,maxSamples-result.SampleCount,out var error);if(samples==null)return Unsupported(result,error);
                result.SampleCount+=samples.Samples.Length;result.TriangleCount+=samples.Triangles;
                var points=samples.Samples.Select(leaf.TransformPoint).ToArray();var sweep=new Bounds(points[0],Vector3.zero);
                foreach(var p in points){sweep.Encapsulate(p);sweep.Encapsulate(p+worldDelta);}sweep.Expand(skin*4);
                var nearby=Physics.OverlapBox(sweep.center,Vector3.Max(sweep.extents,Vector3.one*skin),Quaternion.identity,~(1<<2),QueryTriggerInteraction.Collide).Where(c=>c!=null&&c.enabled&&c.gameObject.activeInHierarchy&&!Owned(c,leaves,ownerId)&&(!c.isTrigger||Person(c))).Distinct().ToArray();
                if(nearby.Length>512)return Unsupported(result,"narrow_candidate_budget_exceeded");foreach(var c in nearby)candidates.Add(c);
                foreach(var point in points)
                    foreach(var obstacle in nearby)
                    {
                        if(!SegmentBounds(point,worldDelta,obstacle.bounds,skin))continue;
                        float distance=float.PositiveInfinity;Vector3 hitPoint=point,hitNormal=Vector3.zero;string method="collider-ray";
                        var box=obstacle as BoxCollider;
                        if(box!=null&&InitialBox(box,point,direction,skin,out bool boxEntering))
                        {result.InitialContacts++;if(boxEntering){distance=0;method="entering-existing-box-contact";}else continue;}
                        else if(Person(obstacle)&&(obstacle is CapsuleCollider||obstacle is CharacterController))
                        {
                            result.RayTests++;distance=CapsuleHit(obstacle,point,direction,length,skin,out bool baseline,out bool entering);method="actual-person-capsule-skin-sweep";
                            if(baseline)result.InitialContacts++;hitPoint=point+direction*distance;
                        }
                        else
                        {
                            result.RayTests++;
                            if(obstacle.Raycast(new Ray(point-direction*skin,direction),out var forward,length+2*skin))
                            {
                                float d=forward.distance-skin;
                                if(d>skin||Vector3.Dot(forward.normal,direction)<-1e-4f){distance=Math.Max(0,d);hitPoint=forward.point;hitNormal=forward.normal;}else result.InitialContacts++;
                            }
                            result.RayTests++;
                            if(obstacle.Raycast(new Ray(point+worldDelta+direction*skin,-direction),out var backward,length+2*skin))
                            {
                                float d=length+skin-backward.distance;
                                if(d>=0&&d<=length&&(d>skin||Vector3.Dot(backward.normal,direction)<-1e-4f)&&d<distance){distance=d;hitPoint=backward.point;hitNormal=backward.normal;method="reverse-collider-ray";}
                            }
                        }
                        if(float.IsInfinity(distance)||distance>length+skin)continue;
                        double fraction=Math.Max(0,Math.Min(1,(distance-skin)/length));result.Clearance=Math.Min(result.Clearance,fraction);
                        if(!blocked.TryGetValue(obstacle,out var prior)||(double)prior["clearance_fraction"]>fraction)
                            blocked[obstacle]=new JObject{["name"]=obstacle.name,["source_id"]=sourceIdentity?.Invoke(obstacle)??"",["clearance_fraction"]=fraction,["distance"]=distance,["method"]=method,["sample"]=PlatformSeededSimulator.Point(point),["hit_point"]=PlatformSeededSimulator.Point(hitPoint),["normal"]=PlatformSeededSimulator.Point(hitNormal),["trigger_person"]=obstacle.isTrigger&&Person(obstacle)};
                    }
            }
            result.CandidateCount=candidates.Count;foreach(var blocker in blocked.Values.OrderBy(b=>(double)b["clearance_fraction"]).ThenBy(b=>(string)b["source_id"],StringComparer.Ordinal).ThenBy(b=>(string)b["name"],StringComparer.Ordinal))result.Blockers.Add(blocker);
            if(result.Blockers.Count>0)result.Reason=result.Clearance<=0?"actual_surface_blocked":"actual_surface_partial_clearance";return result;
        }
    }
}
