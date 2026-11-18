using UnityEngine;
namespace ChooGuard.App.Fps.TrainDeparture
{
    public sealed class DeparturePortal : MonoBehaviour
    {
        public BoxCollider Region;
        public Vector3 LocalInsideDirection=Vector3.forward;
        float previous;
        bool sampled;
        public int Sample(Vector3 feet)
        {
            if(Region==null)return 0;
            var local=transform.InverseTransformPoint(feet+Vector3.up*.6f);
            // Execute installed CoreModule geometry; keep the authored local sample/region.
            float side=new Plane(LocalInsideDirection,Region.center).GetDistanceToPoint(local);
            int result=0;
            if(sampled&&side*previous<0&&new Bounds(Region.center,Region.size).Contains(local))result=side>0?1:-1;
            previous=side;sampled=true;return result;
        }
        public void ResetCrossing(){sampled=false;previous=0;}
    }
}
