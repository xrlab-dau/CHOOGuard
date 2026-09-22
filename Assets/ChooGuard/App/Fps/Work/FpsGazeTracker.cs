using System.Collections.Generic;
using UnityEngine;
namespace ChooGuard.App.Fps.Work
{
    // 시선 체류를 콜라이더 단위로 누적한다. 어느 모드에도 속하지 않는 관측 장치다.
    //
    // FirstPersonResponder.RefreshInteraction 은 부모 사슬에서 IFpsInteraction 을 찾은 경우에만
    // CurrentTargetCollider 를 채운다(FirstPersonResponder.cs:109~117). 따라서 부모에 점검 대상
    // 컴포넌트가 달려 있으면 자식 콜라이더 하나하나가 그대로 관측 대상이 된다 — 자식마다 상호작용
    // 컴포넌트를 달 필요가 없다.
    [DisallowMultipleComponent]
    public sealed class FpsGazeTracker : MonoBehaviour
    {
        public FirstPersonResponder Responder;
        public float MaxTrackedSeconds=120;
        public int MaxTrackedColliders=64;
        public Collider CurrentCollider { get; private set; }
        public float CurrentDwellSeconds { get; private set; }
        private readonly Dictionary<Collider,float> dwell=new Dictionary<Collider,float>();
        private void Awake(){if(Responder==null)Responder=GetComponent<FirstPersonResponder>();}
        private void Update(){if(Responder!=null)Accumulate(Responder.CurrentTargetCollider,Time.deltaTime);}
        // 시험에서 프레임을 직접 먹이기 위해 공개한다. Update 와 같은 경로를 쓴다.
        public void Accumulate(Collider collider,float deltaSeconds)
        {
            if(float.IsNaN(deltaSeconds)||float.IsInfinity(deltaSeconds)||deltaSeconds<=0)return;
            if(collider!=CurrentCollider){CurrentCollider=collider;CurrentDwellSeconds=0;}
            if(collider==null)return;
            if(!dwell.TryGetValue(collider,out var total))
            {
                if(dwell.Count>=MaxTrackedColliders)return; // 조용히 늘어나지 않게 상한을 둔다.
                total=0;
            }
            total=Mathf.Min(total+deltaSeconds,MaxTrackedSeconds);dwell[collider]=total;
            CurrentDwellSeconds=Mathf.Min(CurrentDwellSeconds+deltaSeconds,MaxTrackedSeconds);
        }
        public float DwellOf(Collider collider)=>collider!=null&&dwell.TryGetValue(collider,out var total)?total:0;
        public bool Dwelled(Collider collider,float requiredSeconds)=>DwellOf(collider)>=requiredSeconds;
        public void Reset(){dwell.Clear();CurrentCollider=null;CurrentDwellSeconds=0;}
    }
}
