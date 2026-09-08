using System.Linq;
using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    // Keep the authored soles on the visible floor while the navigation root remains at its .1m sample height.
    // This changes only presentation, not routes, capsule collision, incident logic or crowd decisions.
    public sealed class DemoEvacueeVisual : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        private MeshFilter[] shoes;
        private Vector3[][] vertices;
        public void Configure(Transform model){visual=model;shoes=null;vertices=null;}
        private void LateUpdate(){AlignFeet();}
        public void AlignFeet()
        {
            if(visual==null)return;
            if(shoes==null)
            {
                shoes=visual.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.Contains("Leg_")&&m.name.EndsWith("Rubber")).ToArray();
                vertices=shoes.Select(m=>m.sharedMesh.vertices).ToArray();
            }
            if(shoes.Length==0||!Physics.Raycast(transform.position+Vector3.up*.4f,Vector3.down,out var floor,1,1,QueryTriggerInteraction.Ignore))return;
            var lowest=float.PositiveInfinity;
            for(var i=0;i<shoes.Length;i++)
            {
                var matrix=shoes[i].transform.localToWorldMatrix;
                foreach(var point in vertices[i])lowest=Mathf.Min(lowest,matrix.MultiplyPoint3x4(point).y);
            }
            visual.position+=Vector3.up*(floor.point.y+.003f-lowest);
        }
    }
}
