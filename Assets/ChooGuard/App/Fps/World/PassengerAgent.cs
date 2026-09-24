using UnityEngine;
namespace ChooGuard.App.Fps.World
{
    public enum PassengerStatus { IDLE, MOVING, ARRIVED, NO_PATH, STUCK }

    // 승객 하나. 경로를 받아 걷고, 실패하면 왜 실패했는지 구분해서 말한다.
    //
    // MISSION_DESIGN 8절 1순위가 "사람이 실제로 움직였는가를 판정할 대상" 이다. 유도 성공만
    // 세면 안 되고 못 감·막힘을 구분할 수 있어야 한다. 그래서 상태를 넷으로 나눈다.
    //
    // 규율은 튜토리얼 계층에서 그대로 가져온다.
    // - 시계는 플레이어가 쥔다. 제한 시간으로 실패시키지 않는다 — STUCK 은 실패 판정이 아니라
    //   "전진이 멈췄다" 는 관측이다. 목표를 다시 주면 그대로 다시 걷는다.
    // - 진행은 Tick(seconds) 으로만 일어난다. 시험은 DriveWithFrameTime 을 끄고 직접 먹인다.
    // - 완료는 월드 상태로 판정한다. "유도했다" 가 아니라 "그 자리에 도달했다" 로 본다.
    [DisallowMultipleComponent]
    public sealed class PassengerAgent : MonoBehaviour
    {
        public StationNavigation Navigation;
        [Header("걸음")]
        public float WalkSpeed=1.2f;        // 대합실 보행. 플레이어(1.8)보다 느리게 둔다.
        public float ArriveRadius=.6f;
        public float CornerRadius=.35f;
        [Header("막힘 관측")]
        // 이만큼 시간 동안 이만큼도 못 나아가면 막힌 것으로 본다. 실패가 아니라 관측이다.
        public float StuckSeconds=3f;
        public float StuckProgress=.15f;
        [Header("구동")]
        public bool DriveWithFrameTime=true;
        // 있으면 Start 에서 그리로 출발한다. 씬에서 목적지를 지정할 수 있어야 배치기가 배선할 수 있다.
        public Transform StartDestination;

        public PassengerStatus Status { get; private set; }=PassengerStatus.IDLE;
        public string LastReason { get; private set; }="";
        public Vector3 Destination { get; private set; }
        public int CornerIndex { get; private set; }
        public int CornerCount=>corners==null?0:corners.Length;
        public float TravelledMetres { get; private set; }

        private Vector3[] corners;
        private float sinceProgress;
        private Vector3 progressMark;

        private void Awake()
        {
            if(Navigation==null)Navigation=Object.FindFirstObjectByType<StationNavigation>();
        }

        // 목표를 준다. 경로가 없으면 그 자리에서 NO_PATH 로 서고 이유를 남긴다 —
        // 조용히 제자리에 서 있으면 "안 움직인다" 와 "갈 수 없다" 를 구분할 수 없다.
        public bool SetDestination(Vector3 target)
        {
            Destination=target;
            corners=null;CornerIndex=0;
            if(Navigation==null)
            {
                Status=PassengerStatus.NO_PATH;LastReason="navigation_missing";
                Debug.LogWarning("[승객] 길찾기 컴포넌트가 없습니다.",this);
                return false;
            }
            if(!Navigation.TryPlan(transform.position,target,out var planned,out var reason))
            {
                Status=PassengerStatus.NO_PATH;LastReason=reason;
                return false;
            }
            corners=planned;
            CornerIndex=corners.Length>1?1:0;   // 0번은 현재 위치다
            Status=PassengerStatus.MOVING;LastReason="";
            sinceProgress=0f;progressMark=transform.position;
            return true;
        }

        public void Stop(){corners=null;Status=PassengerStatus.IDLE;LastReason="";}

        private void Start()
        {
            // 목적지가 배선돼 있으면 바로 출발한다. 실패해도 조용히 서 있지 않는다 —
            // SetDestination 이 NO_PATH 와 이유를 남긴다.
            if(StartDestination!=null)SetDestination(StartDestination.position);
        }

        private void Update(){ if(DriveWithFrameTime)Tick(Time.deltaTime); }

        public void Tick(float seconds)
        {
            if(seconds<=0f||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            if(Status!=PassengerStatus.MOVING||corners==null||corners.Length==0)return;

            var target=corners[Mathf.Clamp(CornerIndex,0,corners.Length-1)];
            var here=transform.position;
            var flat=new Vector3(target.x-here.x,0f,target.z-here.z);
            float distance=flat.magnitude;

            // 마지막 지점에 닿았는가. 높이는 보지 않는다 — navmesh 높이와 발밑이 조금씩 다르다.
            if(CornerIndex>=corners.Length-1&&distance<=ArriveRadius)
            {
                Status=PassengerStatus.ARRIVED;LastReason="";
                return;
            }
            if(distance<=CornerRadius&&CornerIndex<corners.Length-1){CornerIndex++;return;}

            float step=Mathf.Min(WalkSpeed*seconds,distance);
            if(distance>0.0001f)
            {
                var direction=flat/distance;
                var lift=(target.y-here.y)*Mathf.Clamp01(seconds*4f);
                transform.position=here+direction*step+Vector3.up*lift;
                transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
                TravelledMetres+=step;
            }

            // 막힘 관측. 몸이 어딘가에 걸려 제자리걸음을 하면 그대로 두지 않고 말한다.
            sinceProgress+=seconds;
            if(Vector3.Distance(transform.position,progressMark)>=StuckProgress)
            {
                progressMark=transform.position;sinceProgress=0f;
            }
            else if(sinceProgress>=StuckSeconds)
            {
                Status=PassengerStatus.STUCK;
                LastReason="no_progress_for_"+StuckSeconds.ToString("0.#")+"s";
            }
        }
    }
}
