using UnityEngine;
namespace ChooGuard.App.Mvp
{
    // Camera-only DEM sampling. Does not add physics/solver colliders or alter render assets.
    public sealed class MvpCameraSurfaceProfile : MonoBehaviour
    {
        public Transform WorldRoot, CityRoot;
        [System.Serializable] public class FloorSurface { public Transform Root; public Vector3[] Triangles; }
        public FloorSurface[] Floors;
        public bool RaycastFloors(Ray ray,float maxDistance,out Vector3 point)
        {
            point=default;bool found=false;if(Floors==null)return false;
            foreach(var floor in Floors)
            {
                if(floor.Root==null||!floor.Root.gameObject.activeInHierarchy||floor.Triangles==null)continue;
                var origin=floor.Root.InverseTransformPoint(ray.origin);var direction=floor.Root.InverseTransformVector(ray.direction);
                var v=floor.Triangles;
                for(int i=0;i+2<v.Length;i+=3)
                {
                    var e1=v[i+1]-v[i];var e2=v[i+2]-v[i];var h=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<.000001f)continue;
                    var s=origin-v[i];float u=Vector3.Dot(s,h)/det;if(u<0||u>1)continue;var q=Vector3.Cross(s,e1);float w=Vector3.Dot(direction,q)/det;if(w<0||u+w>1)continue;
                    float t=Vector3.Dot(e2,q)/det;if(t<0||t>maxDistance)continue;maxDistance=t;point=ray.GetPoint(t);found=true;
                }
            }
            return found;
        }
        public float MinX, MinZ, Step;
        public int Width, Depth;
        public float[] Heights;
        public bool HasTerrain => WorldRoot!=null && CityRoot!=null && CityRoot.gameObject.activeInHierarchy && Step>0 && Width>1 && Depth>1 && Heights!=null && Heights.Length==Width*Depth;
        public bool TryHeight(Vector3 world,out float height)
        {
            height=0;if(!HasTerrain)return false;
            var p=WorldRoot.InverseTransformPoint(world);float fx=(p.x-MinX)/Step,fz=(p.z-MinZ)/Step;
            if(fx<0||fz<0||fx>Width-1||fz>Depth-1)return false;
            fx=Mathf.Min(fx,Width-1.001f);fz=Mathf.Min(fz,Depth-1.001f);
            int x=Mathf.FloorToInt(fx),z=Mathf.FloorToInt(fz);fx-=x;fz-=z;
            float a=Heights[z*Width+x],b=Heights[z*Width+x+1],c=Heights[(z+1)*Width+x+1],d=Heights[(z+1)*Width+x];
            p.y=fx>=fz?a+(b-a)*fx+(c-b)*fz:a+(c-d)*fx+(d-a)*fz;
            height=WorldRoot.TransformPoint(p).y;return true;
        }
        public bool Raycast(Ray ray,float maxDistance,out Vector3 point)
        {
            point=default;if(!HasTerrain)return false;
            float previous=0;
            // At most half a DEM cell in horizontal travel; bounded to protect input latency.
            float stride=Mathf.Clamp(Step*.5f/Mathf.Max(.05f,new Vector2(ray.direction.x,ray.direction.z).magnitude),1,40);
            for(float t=0;t<=maxDistance;t+=stride)
            {
                var p=ray.GetPoint(t);
                if(TryHeight(p,out float h)&&p.y<=h)
                {
                    float lo=previous,hi=t;
                    for(int n=0;n<18;n++){float mid=(lo+hi)*.5f;var q=ray.GetPoint(mid);if(TryHeight(q,out float y)&&q.y<=y)hi=mid;else lo=mid;}
                    point=ray.GetPoint(hi);return true;
                }
                previous=t;
            }
            return false;
        }
    }
}
