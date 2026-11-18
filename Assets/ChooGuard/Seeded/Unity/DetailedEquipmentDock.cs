using UnityEngine;

namespace ChooGuard.Seeded.Unity
{
    public sealed partial class DetailedManipulationController
    {
        public void SetCarryHeadingFollowsActor(string owner,bool enabled){equipment[owner].FollowActorHeading=enabled;}
        // A holster/receiver is distinct from carrying. G explicitly returns a carried item here.
        public void SetEquipmentDock(string owner,Transform anchor,Vector3 localPosition,Quaternion localRotation,bool snap=false,bool stableBodyHeading=false)
        {
            var item=equipment[owner];item.DockAnchor=anchor;item.DockLocal=localPosition;item.DockRotation=localRotation;
            item.StableBodyDock=stableBodyHeading;item.DockBasis=anchor!=null?anchor.rotation:Quaternion.identity;item.DockLastOrigin=anchor!=null?anchor.position:Vector3.zero;
            item.DockSettled=snap;
            SetCarryCollisions(item,anchor==host.Actor);
            if(snap&&anchor!=null)item.Root.SetPositionAndRotation(anchor.TransformPoint(localPosition),anchor.rotation*localRotation);
            Project();
        }
        void ProjectDock(string owner,EquipmentPose item)
        {
            DockPose(item,out var target,out var rotation);
            // Explicit stowing/retraction remains visible; initial placement uses snap above.
            if(item.DockSettled)item.Root.SetPositionAndRotation(target,rotation);
            else
            {
                item.Root.SetPositionAndRotation(Vector3.MoveTowards(item.Root.position,target,8*Time.deltaTime),Quaternion.RotateTowards(item.Root.rotation,rotation,600*Time.deltaTime));
                item.DockSettled=Vector3.Distance(item.Root.position,target)<.001f&&Quaternion.Angle(item.Root.rotation,rotation)<.1f;
            }
            var center=item.Root.TransformPoint(item.CenterLocal);
            var state=Mechanics.World.Entities[owner];state.X=center.x;state.Y=center.y;state.Z=center.z;
            Mechanics.SetInteractionCenter(owner,center.x,center.y,center.z);
        }
        static void DockPose(EquipmentPose item,out Vector3 position,out Quaternion rotation)
        {
            if(item.StableBodyDock)
            {
                var movement=item.DockAnchor.position-item.DockLastOrigin;movement.y=0;
                if(movement.sqrMagnitude>.000001f)item.DockBasis=Quaternion.Euler(0,item.DockAnchor.eulerAngles.y,0);
                item.DockLastOrigin=item.DockAnchor.position;
                position=item.DockAnchor.position+item.DockBasis*item.DockLocal;rotation=item.DockBasis*item.DockRotation;
            }
            else {position=item.DockAnchor.TransformPoint(item.DockLocal);rotation=item.DockAnchor.rotation*item.DockRotation;}
        }
    }
}
