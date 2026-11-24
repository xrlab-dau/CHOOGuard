using System;
using System.Linq;
using UnityEngine;

namespace ChooGuard.Seeded.Unity
{
    public sealed partial class DetailedManipulationController
    {
        readonly EquipmentPose partCollisionPose=new EquipmentPose();
        float partBlockedUntil;
        float nextPartDiagnostic;
        public string PartFeedback=>Time.unscaledTime<partBlockedUntil?"부품이 벽이나 사람에 걸렸습니다. 공간을 확보해 다시 조작하세요.":null;
        void RequestedPartPose(PartPose pose,float amount,out Vector3 point,out Quaternion rotation)
        {
            var p=pose.Explicit;var t=p.Part;
            var local=p.DrivePosition?Vector3.Lerp(p.MinLocalPosition,p.MaxLocalPosition,amount):t.localPosition;
            var turn=p.DriveRotation?Quaternion.Slerp(p.MinLocalRotation,p.MaxLocalRotation,amount):t.localRotation;
            point=t.parent!=null?t.parent.TransformPoint(local):local;rotation=t.parent!=null?t.parent.rotation*turn:turn;
            if(p.AimWithView&&amount>=p.AimAfterNormalized&&host.View!=null)rotation=host.View.transform.rotation*Quaternion.Inverse(pose.PartToAimRotation);
        }
        float PartFraction(PartPose pose,Vector3 point,Quaternion rotation)
        {
            var t=pose.Explicit.Part;var start=t.position;var before=t.rotation;
            float distance=Vector3.Distance(start,point),angle=Quaternion.Angle(before,rotation);
            if(distance<.000001f&&angle<.01f)return 1;
            partCollisionPose.Root=t;float limit=TranslationFraction(pose.Binding.OwnerId,partCollisionPose,point-start);
            int steps=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(distance/.008f,angle/2)),1,72);float accepted=0;
            PartFootprint(t,start,before,out var oldCenter,out var oldHalf);
            for(int i=1;i<=steps;i++)
            {
                float fraction=limit*i/steps;var candidate=Quaternion.Slerp(before,rotation,fraction);
                PartFootprint(t,Vector3.Lerp(start,point,fraction),candidate,out var center,out var half);bool blocked=false;
                foreach(var collider in Physics.OverlapBox(center,half,candidate,~0,QueryTriggerInteraction.Ignore).Where(c=>CarryBlocker(c,pose.Binding.OwnerId)))
                {
                    bool known=Penetration(center,half,candidate,collider,out _,out var depth);
                    if(known&&depth<=.0005f)continue;
                    bool wasInside=Penetration(oldCenter,oldHalf,before,collider,out _,out var oldDepth);
                    if(known&&wasInside&&depth<oldDepth-.0001f)continue;
                    blocked=true;
                    if(ExternalInputMode&&Time.unscaledTime>nextPartDiagnostic)
                    {nextPartDiagnostic=Time.unscaledTime+1;Debug.LogWarning("[Part contact] "+pose.Binding.PartId+" collider="+collider.name+" center="+center+" half="+half+" depth="+depth+" old="+oldDepth+" known="+known);}
                    break;
                }
                if(blocked)break;accepted=fraction;
            }
            if(accepted<.99f)partBlockedUntil=Time.unscaledTime+2;
            return accepted;
        }
        static void PartFootprint(Transform part,Vector3 position,Quaternion rotation,out Vector3 center,out Vector3 half)
        {
            var b=AuthoredEquipmentBounds(part);var scale=part.lossyScale;
            center=position+rotation*Vector3.Scale(b.center,scale);
            half=Vector3.Max(Vector3.Scale(b.extents,new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z))),Vector3.one*.003f)+Vector3.one*.001f;
        }
        double ExplicitPartClearance(PartPose pose,double analog,double duration)
        {
            if(duration<=0||analog==0)return 1;
            var definition=pose.Definition;var owner=Mechanics.World.Entities[pose.Binding.OwnerId];
            double friction=definition.Friction*owner.Get("mech.friction-scale",1);
            double effort=Math.Sign(analog)*Math.Max(0,Math.Abs(analog)-friction)/(1-friction);
            double current=Mechanics.Position(pose.Binding.OwnerId,pose.Binding.PartId);
            double next=Math.Max(definition.Min,Math.Min(definition.Max,current+effort*definition.Speed*duration));
            RequestedPartPose(pose,Mathf.InverseLerp((float)definition.Min,(float)definition.Max,(float)next),out var point,out var rotation);
            return PartFraction(pose,point,rotation);
        }
        void ProjectExplicitPart(PartPose pose,float amount)
        {
            var p=pose.Explicit;if(!p.DrivePosition&&!p.DriveRotation&&!p.AimWithView)return;
            RequestedPartPose(pose,amount,out var point,out var rotation);var t=p.Part;
            float fraction=host.IsReplaying?1:PartFraction(pose,point,rotation);
            t.SetPositionAndRotation(Vector3.Lerp(t.position,point,fraction),Quaternion.Slerp(t.rotation,rotation,fraction));
        }
    }
}
