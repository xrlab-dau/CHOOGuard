using System.Linq;
using UnityEngine;

namespace ChooGuard.Seeded.Unity
{
    public sealed partial class DetailedManipulationController
    {
        BoxCollider carryProbe;
        float carryBlockedUntil;
        float nextCarryDiagnostic;
        public string CarryObservation {get;private set;}
        public string CarryFeedback=>Time.unscaledTime<carryBlockedUntil?"운반 물체가 주변 물체나 사람에 걸렸습니다. 여유 있는 쪽으로 돌아가세요.":null;
        // A conservative authored footprint prevents held equipment passing through people/walls.
        // This is kinematic carrying, not a mass/force or independently replayed physics claim.
        static Bounds AuthoredEquipmentBounds(Transform root)
        {
            var result=new Bounds();bool any=false;
            foreach(var c in root.GetComponentsInChildren<Collider>(false).Where(c=>c.enabled&&!c.isTrigger))
            {
                Bounds b;
                if(c is BoxCollider box)b=new Bounds(box.center,box.size);
                else if(c is MeshCollider mesh&&mesh.sharedMesh!=null)b=mesh.sharedMesh.bounds;
                else if(c is CapsuleCollider capsule)
                {var size=Vector3.one*capsule.radius*2;size[capsule.direction]=Mathf.Max(size[capsule.direction],capsule.height);b=new Bounds(capsule.center,size);}
                else if(c is SphereCollider sphere)b=new Bounds(sphere.center,Vector3.one*sphere.radius*2);
                else {b=c.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)Include(root.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))),ref result,ref any);continue;}
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    Include(root.InverseTransformPoint(c.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)))),ref result,ref any);
            }
            return result;
        }
        static void Include(Vector3 point,ref Bounds bounds,ref bool any)
        {if(!any){bounds=new Bounds(point,Vector3.zero);any=true;}else bounds.Encapsulate(point);}
        bool CarryBlocker(Collider collider,string owner)
        {
            if(collider==null||WithinOwner(collider,owner)||collider.transform.IsChildOf(host.Actor))return false;
            foreach(var pair in equipment)
                if((host.State.Entities["player"].Get("mech.carry."+pair.Key)>0||pair.Value.DockAnchor==host.Actor||pair.Value.DockAnchor==equipment[owner].Root)&&collider.transform.IsChildOf(pair.Value.Root))return false;
            return !Physics.GetIgnoreLayerCollision(equipment[owner].Root.gameObject.layer,collider.gameObject.layer);
        }
        void Footprint(EquipmentPose item,Vector3 position,Quaternion rotation,out Vector3 center,out Vector3 half)
        {
            var b=EquipmentBounds(item.Root);var s=item.Root.lossyScale;
            center=position+rotation*Vector3.Scale(b.center,s);
            half=Vector3.Max(Vector3.Scale(b.extents,new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z)))-Vector3.one*.003f,Vector3.one*.005f);
        }
        bool Penetration(Vector3 center,Vector3 half,Quaternion rotation,Collider other,out Vector3 direction,out float depth)
        {
            if(carryProbe==null)
            {
                // Keep a live non-solid query shape out of play; query poses are supplied explicitly.
                var go=new GameObject("Runtime carried footprint query"){hideFlags=HideFlags.HideAndDontSave,layer=2};
                go.transform.position=Vector3.down*10000;carryProbe=go.AddComponent<BoxCollider>();carryProbe.isTrigger=true;
            }
            carryProbe.size=half*2;
            return Physics.ComputePenetration(carryProbe,center,rotation,other,other.transform.position,other.transform.rotation,out direction,out depth);
        }
        float TranslationFraction(string owner,EquipmentPose item,Vector3 movement)
        {
            float length=movement.magnitude;if(length<.000001f)return 1;
            Footprint(item,item.Root.position,item.Root.rotation,out var center,out var half);float allowed=1;
            foreach(var hit in Physics.BoxCastAll(center,half,movement/length,item.Root.rotation,length+.008f,~0,QueryTriggerInteraction.Ignore))
            {
                if(!CarryBlocker(hit.collider,owner))continue;
                if(hit.distance<.002f&&Vector3.Dot(movement,hit.normal)>=-.000001f)continue;
                if(hit.distance<.002f&&Penetration(center,half,item.Root.rotation,hit.collider,out var escape,out _)&&Vector3.Dot(movement,escape)>=-.000001f)continue;
                allowed=Mathf.Min(allowed,Mathf.Clamp01((hit.distance-.008f)/length));
                CarryObservation=owner+" at="+item.Root.position+" movement="+movement+" blocked="+hit.collider.name+" distance="+hit.distance+" normal="+hit.normal;
            }
            if(allowed<.99f)
            {
                carryBlockedUntil=Time.unscaledTime+2;
                if(ExternalInputMode&&Time.unscaledTime>nextCarryDiagnostic){nextCarryDiagnostic=Time.unscaledTime+2;Debug.LogWarning("[Carry contact] "+CarryObservation);}
            }
            return allowed;
        }
        public Vector3 ConstrainActorMovement(Vector3 movement)
        {
            if(host?.Actor==null||host.IsReplaying)return movement;
            Physics.SyncTransforms();float allowed=1;
            foreach(var pair in equipment)
                if(host.State.Entities["player"].Get("mech.carry."+pair.Key)>0)allowed=Mathf.Min(allowed,TranslationFraction(pair.Key,pair.Value,movement));
            return movement*allowed;
        }
        void ApplyCarryPose(string owner,EquipmentPose item,Vector3 requested,Quaternion rotation)
        {
            var oldPosition=item.Root.position;var oldRotation=item.Root.rotation;
            var position=Vector3.Lerp(oldPosition,requested,TranslationFraction(owner,item,requested-oldPosition));
            var accepted=oldRotation;int steps=Mathf.Clamp(Mathf.CeilToInt(Quaternion.Angle(oldRotation,rotation)/8),1,12);
            for(int i=1;i<=steps;i++)
            {
                var candidate=Quaternion.Slerp(oldRotation,rotation,i/(float)steps);
                Footprint(item,position,candidate,out var center,out var half);
                Footprint(item,oldPosition,oldRotation,out var oldCenter,out var oldHalf);bool blocked=false;
                foreach(var collider in Physics.OverlapBox(center,half,candidate,~0,QueryTriggerInteraction.Ignore).Where(c=>CarryBlocker(c,owner)))
                {
                    if(!Penetration(center,half,candidate,collider,out _,out var depth)||depth<=.002f)continue;
                    Penetration(oldCenter,oldHalf,oldRotation,collider,out _,out var oldDepth);
                    if(depth>oldDepth+.002f){blocked=true;break;}
                }
                if(blocked){carryBlockedUntil=Time.unscaledTime+2;break;}accepted=candidate;
            }
            item.Root.SetPositionAndRotation(position,accepted);
        }
    }
}
