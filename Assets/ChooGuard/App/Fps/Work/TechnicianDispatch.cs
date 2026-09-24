using System;
using UnityEngine;
namespace ChooGuard.App.Fps.Work
{
    // 인계 단계. 요청이 곧 완료가 아니라는 것을 타입으로 강제한다.
    public enum HandoffStage { NONE, RECEIVED, TRAVELLING, WORKING, COMPLETED }

    // 기술자 인계. 역무원이 '요구'하고 기술자가 '수행'하는 경계를 월드에서 보이게 만든다
    // (제23조①1 — 구입·수리에 필요한 예산은 소방관리 책임자가 요구).
    //
    // 벽시계를 쓰지 않는다. TutorialSession 의 "시계는 플레이어가 쥔다" 규율을 그대로 따른다 —
    // 진행은 Tick(seconds) 로만 일어나고, 시간이 지나서 실패하는 조건은 없다. 늦어질 뿐이다.
    // 설비와 마찬가지로 절차 규칙은 모른다. 무엇이 요청을 부르는지는 세션이 정한다.
    public class TechnicianDispatch : MonoBehaviour
    {
        [Header("대상")]
        public FacilityInspectable Target;

        [Header("단계별 소요 (초)")]
        // 0 이하를 넣어도 단계를 건너뛰지 않는다 — Tick 한 번에 한 단계만 넘어간다.
        public float AcknowledgeSeconds=4f;
        public float TravelSeconds=35f;
        public float WorkSeconds=20f;

        [Header("교체 결과")]
        // 폐기·교체이므로 새 설비의 고유번호가 온다(제23조②1). 비우면 기존 번호에 접미사를 붙인다.
        public string ReplacementSerial="";

        public HandoffStage Stage { get; private set; }=HandoffStage.NONE;
        public float SecondsInStage { get; private set; }
        public string RequestedSerial { get; private set; }="";
        public string RequestedReason { get; private set; }="";
        // 플레이어가 결과를 확인했는가. 확인은 플레이어의 행동이지 기술자의 결과가 아니다.
        public bool ResultWitnessed { get; private set; }

        // 현재 단계의 진행도 0~1. 표현 계층이 읽으려고 공개한다 — 상태 기계는 이 값을 보지 않는다.
        // 소요가 0 인 단계(시험이 쓰는 설정)는 1 로 본다. 남은 시간을 초로 내보이지 않는 것은
        // 시간 초과 실패 조건이 없기 때문이다 — 카운트다운을 보여주면 없는 규칙을 암시하게 된다.
        public float StageProgress01
        {
            get
            {
                var need=RequiredSeconds(Stage);
                return need<=0f?1f:Mathf.Clamp01(SecondsInStage/need);
            }
        }

        public bool Requested=>Stage!=HandoffStage.NONE;
        public bool Completed=>Stage==HandoffStage.COMPLETED;
        // 도착 이후를 '현장'으로 본다 — 입회가 가능한 구간이다.
        public bool OnSite=>Stage==HandoffStage.WORKING||Stage==HandoffStage.COMPLETED;

        // 단계가 바뀔 때만 부른다. 세션이 피드백 문구를 소유하므로 여기서 UI 를 건드리지 않는다.
        public event Action<HandoffStage> StageChanged;

        public string StageLabel
        {
            get
            {
                switch(Stage)
                {
                    case HandoffStage.RECEIVED:return "요구 접수됨 · 기술자 배정 대기";
                    case HandoffStage.TRAVELLING:return "기술자 이동 중";
                    case HandoffStage.WORKING:return "기술자 작업 중 · 입회 가능";
                    case HandoffStage.COMPLETED:return ResultWitnessed?"교체 완료 · 확인됨":"교체 완료 · 결과 미확인";
                    default:return "요구 미발행";
                }
            }
        }

        // 요구 발행. 이미 요청된 건에 다시 부르면 중복 발행하지 않는다 —
        // 같은 대상에 두 번 요구해도 기술자가 둘 오지는 않는다.
        public bool Request(string serial,string reason)
        {
            if(Requested)return false;
            RequestedSerial=serial??"";
            RequestedReason=reason??"";
            Enter(HandoffStage.RECEIVED);
            return true;
        }

        // 진행. 한 번의 호출이 여러 단계를 건너뛰지 않는다 — 각 단계가 최소 한 번은 관찰된다.
        // 남은 시간은 이월하므로 큰 delta 를 여러 번 주면 순서대로 지나간다.
        public void Tick(float seconds)
        {
            if(seconds<=0f)return;
            if(Stage==HandoffStage.NONE||Stage==HandoffStage.COMPLETED)return;
            SecondsInStage+=seconds;
            var need=RequiredSeconds(Stage);
            if(SecondsInStage<need)return;
            // 초과분은 Enter 가 0 으로 초기화한 뒤에 다시 넣는다 — 먼저 빼두면 그대로 버려진다.
            var carry=SecondsInStage-need;
            switch(Stage)
            {
                case HandoffStage.RECEIVED:Enter(HandoffStage.TRAVELLING);break;
                case HandoffStage.TRAVELLING:Enter(HandoffStage.WORKING);break;
                case HandoffStage.WORKING:Complete();break;
            }
            // 완료 단계에는 더 셀 것이 없다.
            if(Stage!=HandoffStage.COMPLETED)SecondsInStage=carry;
        }

        private float RequiredSeconds(HandoffStage stage)
        {
            switch(stage)
            {
                case HandoffStage.RECEIVED:return Mathf.Max(0f,AcknowledgeSeconds);
                case HandoffStage.TRAVELLING:return Mathf.Max(0f,TravelSeconds);
                case HandoffStage.WORKING:return Mathf.Max(0f,WorkSeconds);
                default:return 0f;
            }
        }

        // 플레이어의 결과 확인. 도착 전에는 확인할 것이 없다.
        public bool WitnessResult(out string reason)
        {
            if(Stage!=HandoffStage.COMPLETED){reason="아직 교체가 끝나지 않았습니다 · "+StageLabel;return false;}
            reason=null;ResultWitnessed=true;return true;
        }

        private void Complete()
        {
            // 교체가 월드를 실제로 바꾼다. 결함이 남아 있으면 교체라고 부를 수 없다.
            if(Target!=null)
            {
                var serial=string.IsNullOrEmpty(ReplacementSerial)?NextSerial(Target.SerialNumber):ReplacementSerial;
                Target.ApplyReplacement(serial);
            }
            Enter(HandoffStage.COMPLETED);
        }

        private static string NextSerial(string current)=>string.IsNullOrEmpty(current)?"교체품":current+"-R";

        private void Enter(HandoffStage stage)
        {
            if(Stage==stage)return;
            Stage=stage;SecondsInStage=0f;
            StageChanged?.Invoke(stage);
        }

        public void ResetHandoff()
        {
            Stage=HandoffStage.NONE;SecondsInStage=0f;
            RequestedSerial="";RequestedReason="";ResultWitnessed=false;
        }
    }
}
