using System;
using System.Linq;
using UnityEngine;
namespace ChooGuard.Foundation.Demo
{
    public sealed class StationPortal : MonoBehaviour
    {
        [SerializeField] private string siteId;
        [SerializeField] private string label;
        [SerializeField] private Transform waypoint;
        [SerializeField] private GameObject barrier;
        [SerializeField] private Transform cue;
        [SerializeField] private Renderer lens;
        [SerializeField] private Renderer[] signs;
        [SerializeField] private Light warningLight;
        private MaterialPropertyBlock block;
        private AudioSource audioSource;
        private AudioClip tone;
        public string SiteId {get{return siteId;}} public string Label {get{return label;}}
        public Vector3 Waypoint {get{return waypoint.position;}} public Transform Cue {get{return cue;}}
        public Bounds ExclusionBounds {get{return new Bounds(waypoint.position+Vector3.up*.9f,new Vector3(barrier.transform.localScale.x,1.8f,.32f));}}
        public bool Unavailable {get;private set;}
        public void Configure(string id,string display,Transform routePoint,GameObject solid,Transform indicator,Renderer signal,Renderer[] routeSigns,Light light)
        {siteId=id;label=display;waypoint=routePoint;barrier=solid;cue=indicator;lens=signal;signs=routeSigns;warningLight=light;SetState(false,false,false);}
        public bool CanActivate(Vector3[] actors)
        {var b=ExclusionBounds;b.Expand(new Vector3(1.4f,2,1.4f));return actors.All(x=>!b.Contains(x+Vector3.up*.8f));}
        public bool TrackCrossing(Vector3 point,ref bool approached)
        {
            var delta=point-Waypoint;
            if(Mathf.Abs(delta.x)>ExclusionBounds.extents.x-.3f||Mathf.Abs(delta.y)>1.5f||Mathf.Abs(delta.z)>1.5f){approached=false;return false;}
            if(delta.z<-.2f&&delta.z>-1.5f)approached=true;
            return approached&&delta.z>.2f&&delta.z<1.5f;
        }
        public void SetState(bool unavailable,bool physicalBarrier,bool escalated)
        {
            var newlyUnavailable=unavailable&&!Unavailable;Unavailable=unavailable;
            if(barrier!=null)barrier.SetActive(unavailable&&physicalBarrier);
            if(lens!=null)lens.enabled=unavailable;
            if(warningLight!=null){warningLight.enabled=unavailable;warningLight.intensity=escalated?2.1f:1.0f;}
            if(block==null)block=new MaterialPropertyBlock();
            var color=unavailable?new Color(.94f,.20f,.10f):new Color(.10f,.65f,.37f);
            block.SetColor("_Color",color);block.SetColor("_BaseColor",color);
            foreach(var renderer in signs)if(renderer!=null)renderer.SetPropertyBlock(block);
            if(newlyUnavailable&&Application.isPlaying)PlayLocalCue();
        }
        private void PlayLocalCue()
        {
            if(audioSource==null)
            {
                audioSource=cue.gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=1;audioSource.rolloffMode=AudioRolloffMode.Linear;audioSource.minDistance=2;audioSource.maxDistance=25;audioSource.volume=.10f;
                const int rate=16000;var data=new float[rate/2];
                for(var i=0;i<data.Length;i++)data[i]=(float)Math.Sin(i*Math.PI*2*660/rate)*Mathf.Sin(Mathf.PI*i/data.Length)*.3f;
                tone=AudioClip.Create("Synthetic station indication",data.Length,1,rate,false);tone.SetData(data,0);
            }
            audioSource.PlayOneShot(tone);
        }
        private void OnDestroy(){if(tone!=null)Destroy(tone);}
    }
}
