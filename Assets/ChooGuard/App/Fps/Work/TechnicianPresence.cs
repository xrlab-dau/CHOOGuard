using UnityEngine;
namespace ChooGuard.App.Fps.Work
{
    // 기술자 인계를 눈에 보이게 한다. 표현만 하고 아무것도 결정하지 않는다 —
    // 단계 전이도 완료 판정도 TechnicianDispatch 가 소유한다. 여기서 월드 상태를 바꾸면
    // "완료를 월드 상태로 판정한다" 는 규율이 표현 계층으로 새어 나간다.
    //
    // 왜 필요했나: 2026-09-25 실측에서 요구 발행 후 59 초를 기다렸는데 완료 시점 프레임이
    // 요청 직후 프레임과 사실상 같았다. 상태 기계는 옳게 도는데 화면에 볼 것이 없었다.
    //
    // 이것은 임시 대역이다. 실제 캐릭터·애니메이션이 아니라 사람 크기의 형상이 출발 지점에서
    // 작업 지점으로 이동하고 작업 중 머무는 것까지만 한다. 그것만으로도 "도착이 보이는가" 가
    // 참이 되지만, 이 대역을 근거로 G9 의 관찰 가능성을 통과로 보아서는 안 된다.
    [DisallowMultipleComponent]
    public sealed class TechnicianPresence : MonoBehaviour
    {
        public TechnicianDispatch Dispatch;
        [Header("표시")]
        public Transform Body;         // 보이는 형상. 없으면 아무것도 하지 않는다.
        public Transform Entry;        // 출발 지점. 없으면 작업 지점에서 그냥 나타난다.
        public Transform WorkSpot;     // 작업 지점. 없으면 대상 설비 자리.
        // 프레임으로 갱신할지. 시험은 이 값을 끄고 Apply() 를 직접 불러 결정론을 지킨다.
        public bool DriveWithFrameTime=true;

        private void Awake(){ if(Dispatch==null)Dispatch=GetComponent<TechnicianDispatch>(); }
        private void Start(){ Apply(); }
        private void Update(){ if(DriveWithFrameTime)Apply(); }

        // 단계에 따라 형상을 놓는다. 여러 번 불러도 같은 결과다.
        public void Apply()
        {
            if(Body==null||Dispatch==null)return;
            var work=WorkSpot!=null?WorkSpot.position
                     :Dispatch.Target!=null?Dispatch.Target.transform.position:Body.position;
            var from=Entry!=null?Entry.position:work;

            switch(Dispatch.Stage)
            {
                // 접수 단계에서는 아직 보이지 않는다. 요구가 접수됐을 뿐 출발하지 않았다.
                case HandoffStage.NONE:
                case HandoffStage.RECEIVED:
                    Show(false);
                    break;
                case HandoffStage.TRAVELLING:
                    Show(true);
                    Body.position=Vector3.Lerp(from,work,Dispatch.StageProgress01);
                    Face(work);
                    break;
                case HandoffStage.WORKING:
                    Show(true);
                    Body.position=work;
                    Face(Dispatch.Target!=null?Dispatch.Target.transform.position:work);
                    break;
                // 끝나자마자 사라지지 않는다. 플레이어가 결과를 확인(입회)할 때까지 현장에 있어야
                // "입회" 라는 말이 성립한다. 확인한 뒤에 떠난다.
                case HandoffStage.COMPLETED:
                    Show(!Dispatch.ResultWitnessed);
                    if(!Dispatch.ResultWitnessed)
                    {
                        Body.position=work;
                        Face(Dispatch.Target!=null?Dispatch.Target.transform.position:work);
                    }
                    break;
            }
        }

        public bool Visible=>Body!=null&&Body.gameObject.activeSelf;

        private void Show(bool visible)
        {
            if(Body.gameObject.activeSelf!=visible)Body.gameObject.SetActive(visible);
        }

        private void Face(Vector3 point)
        {
            var look=point-Body.position;look.y=0;
            if(look.sqrMagnitude>.0001f)Body.rotation=Quaternion.LookRotation(look.normalized,Vector3.up);
        }
    }
}
