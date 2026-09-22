using System.Collections.Generic;
using ChooGuard.Contracts;
using ChooGuard.App.Fps.Work;
using UnityEngine;
namespace ChooGuard.App.Fps.Tutorial
{
    // 튜토리얼 모드의 세션 소유자.
    //
    // 경계 리트머스(설계 확정): 이 네임스페이스는 Emergency 심볼을 참조하지 않는다. 그 반대도 같다.
    // 벽시계가 없다 — Time.time 기반 실패 조건을 두지 않는다. 시계는 플레이어가 쥔다.
    // 되감기가 있다. 비상 모드에는 없다.
    //
    // ChecklistVisible=false 는 '무점검표 재시행'이다. 같은 절차·같은 프리팹·같은 단계 수를
    // 점검표만 가린 채 다시 도는 것으로, 동형성 다리를 시험한다. 비상 모드가 아니다 —
    // 무작위도 벽시계도 없으므로 그렇게 부르면 거짓이 된다.
    public sealed class TutorialSession : MonoBehaviour
    {
        public FirstPersonResponder Responder;
        public FpsGazeTracker GazeTracker;
        public FacilityInspectable Target;
        public AuditTerminal AuditTerminal;
        public TextAsset ProcedureAsset;
        public GameObject InspectionTagPrefab;
        [Header("모드")]
        public bool ChecklistVisible=true;
        [Header("판정 입력 (플레이어가 고른다)")]
        public InspectionVerdict PendingVerdict=InspectionVerdict.FIT;

        public ProcedureRunner Runner { get; private set; }
        public string LastReason { get; private set; }="";
        public bool Finished { get; private set; }
        public IReadOnlyList<string> AuditFindings=>findings;
        public int RoleBoundaryViolations { get; private set; }
        public int MisjudgementCount { get; private set; }

        private readonly List<string> findings=new List<string>();
        private readonly Dictionary<string,RuleTruth> facts=new Dictionary<string,RuleTruth>();

        private void Awake()
        {
            Runner=new ProcedureRunner();
            if(Responder==null)Responder=GetComponentInParent<FirstPersonResponder>();
            if(GazeTracker==null&&Responder!=null)GazeTracker=Responder.GetComponent<FpsGazeTracker>();
            if(Target!=null&&GazeTracker!=null)Target.Bind(GazeTracker);
            // 여기서 로드하지 않는다. AddComponent<T>() 는 Awake 를 그 줄에서 동기 실행하므로
            // 호출자가 다음 줄에서 ProcedureAsset 을 꽂으면 이미 늦다. 최초 사용 시점으로 미룬다.
        }
        private bool loadAttempted;
        // 최초 사용 시 한 번 읽는다. 실패는 한 번만 크게 알린다(반복 로그로 콘솔을 묻지 않는다).
        public bool EnsureLoaded()
        {
            if(Runner==null)Runner=new ProcedureRunner();
            if(Runner.Ready)return true;
            if(loadAttempted)return false;
            loadAttempted=true;
            if(Runner.Load(ProcedureAsset))return true;
            Debug.LogError("[TutorialSession] 절차를 읽지 못해 세션을 시작할 수 없습니다 · "+Runner.StatusReason,this);
            return false;
        }
        // 자료를 바꿔 끼울 때만 쓴다. 다시 읽기를 허용한다.
        public bool Reload(TextAsset asset){ProcedureAsset=asset;loadAttempted=false;return EnsureLoaded();}
        private void Start(){EnsureLoaded();Bind();RefreshPrompt();}
        private bool bound;
        // E 키 경로를 절차에 연결한다. 게이트는 세션이 소유하고 설비는 델리게이트만 안다.
        public void Bind()
        {
            if(bound||Target==null)return;
            Target.GateReason=()=>{var decision=Peek();return decision.Allowed?null:decision.Reason;};
            Target.InteractionPerformed+=_=>Advance();
            // 판정 단말도 같은 규율로 꽂는다 — 단말은 이 타입을 모르고 델리게이트 두 개만 받는다.
            // 절차 진행 중에도 단말은 월드에 있고 거부만 한다. 감사는 사후 열거다.
            if(AuditTerminal!=null)
            {
                AuditTerminal.GateReason=()=>Finished?null:"점검이 끝나지 않았습니다 · "+(Peek().Step?.Label??"진행 중");
                AuditTerminal.ReportSource=()=>new AuditReport(findings,Target!=null?Target.name:"점검 대상");
            }
            bound=true;
        }
        private void OnDestroy()
        {
            if(Target!=null)Target.GateReason=null;
            if(AuditTerminal!=null){AuditTerminal.GateReason=null;AuditTerminal.ReportSource=null;}
        }

        // 월드 상태를 사실로 환산한다. 여기가 world_evidence 규율이 사는 곳이다 —
        // 버튼을 눌렀는가가 아니라 월드가 어떤 상태인가만 본다.
        public RuleFacts Observe()
        {
            facts.Clear();
            if(Target==null)return ProcedureRunner.FactsFrom(facts);
            // ① ~ ④ 시선 체류. 관측하지 않은 것은 FALSE 가 아니라 UNKNOWN 이다 — 아직 안 본 것과 부적합은 다르다.
            Set("serial-gazed",Target.Observed("serial"));
            Set("spec-plate-gazed",Target.Observed("spec-plate"));
            Set("gauge-gazed",Target.Observed("gauge"));
            Set("body-gazed",Target.Observed("body"));
            // ③ 고유번호가 실제로 기재됐는가
            facts["serial-recorded"]=string.IsNullOrEmpty(Target.RecordedSerial)?RuleTruth.UNKNOWN:RuleTruth.TRUE;
            facts["verdict-recorded"]=Target.Verdict==InspectionVerdict.NOT_RECORDED?RuleTruth.UNKNOWN:RuleTruth.TRUE;
            facts["verdict-fit"]=Target.Verdict==InspectionVerdict.FIT?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            facts["verdict-unfit"]=Target.Verdict==InspectionVerdict.UNFIT?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            // 월드의 진짜 상태. 플레이어 판정과 대조해 오판정을 잡는다.
            facts["corroded"]=Target.Corroded?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["mechanically-defective"]=Target.MechanicallyDefective?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["pressure-out-of-range"]=Target.PressureOutOfRange?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["expiry-passed"]=Target.ExpiryPassed?RuleTruth.TRUE:RuleTruth.FALSE;
            // ⑤ 잔존물
            facts["repair-order-issued"]=Target.RepairOrderIssued?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            facts["tag-attached"]=Target.AttachedTag!=null?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            return ProcedureRunner.FactsFrom(facts);
        }
        private void Set(string key,bool observed)=>facts[key]=observed?RuleTruth.TRUE:RuleTruth.UNKNOWN;

        public ProcedureRunner.Decision Peek(){EnsureLoaded();return Runner.Next(Observe());}

        // 다음 단계를 한 번 시도한다. E 상호작용과 시험이 같은 경로를 쓴다.
        public bool Advance()
        {
            if(Finished)return false;
            var decision=Peek();
            LastReason=decision.Reason;
            if(!decision.Allowed){RefreshPrompt();return false;}
            ApplyEffect(decision.Step);
            // 효과가 월드를 바꿨으니 사실을 다시 읽고 같은 단계로 커밋을 다시 판정한다.
            var confirm=Runner.Next(Observe());
            bool committed=confirm.Allowed&&confirm.Step==decision.Step&&Runner.Commit(confirm);
            if(!committed){LastReason="월드 상태가 단계 요건을 충족하지 못해 완료로 기록하지 않았습니다 · "+decision.Step.Label;RefreshPrompt();return false;}
            LastReason=decision.Step.Label+" 완료";
            // 피드백은 기존 TryInteract 가 이 핸들러 다음에 SuccessMessage 를 읽으므로
            // '방금 끝낸 단계'를 여기서 넣는다. RefreshPrompt 는 다음 단계만 다룬다.
            Target.SuccessMessage=LastReason;
            if(decision.Step.Effect=="close-inspection")Audit();
            RefreshPrompt();return true;
        }

        private void ApplyEffect(ProcedureRunner.Step step)
        {
            switch(step.Effect)
            {
                case "record-serial":Target.RecordSerial();break;
                case "record-verdict":
                    Target.RecordVerdict(PendingVerdict);
                    // 제23조②1 — 기한 전이라도 부식·결함이면 폐기다. 기한만 보고 통과시키면 오판정이다.
                    if(Target.ShouldBeUnfit&&PendingVerdict==InspectionVerdict.FIT)MisjudgementCount++;
                    break;
                case "issue-repair-order":Target.IssueRepairOrder();break;
                case "attach-tag":
                    var tag=InspectionTagPrefab!=null?Instantiate(InspectionTagPrefab):new GameObject("소화기 점검표");
                    tag.name="소화기 점검표 · "+Target.RecordedSerial;Target.AttachTag(tag);break;
                case "close-inspection":break;
            }
        }

        // 감사관 단말. 월드를 다시 읽어 미충족을 '위치 · 항목'으로 열거한다. 즉시 야단치지 않는다.
        public void Audit()
        {
            findings.Clear();
            string place=Target!=null?Target.name:"대상 미지정";
            foreach(var step in Runner.Unmet(Observe()))findings.Add(place+" · "+step.Label+" 미완료 — "+step.Basis);
            if(Target!=null)
            {
                if(Target.ShouldBeUnfit&&Target.Verdict==InspectionVerdict.FIT)
                    findings.Add(place+" · 오판정 — 부식·결함·기한 상태인데 적합으로 기재됨 (제23조②1)");
                if(Target.Verdict==InspectionVerdict.UNFIT&&!Target.RepairOrderIssued)
                    findings.Add(place+" · 폐기·교체 요구 인계 미발행 (제23조①1)");
            }
            if(RoleBoundaryViolations>0)
                findings.Add(place+" · 역할 경계 위반 "+RoleBoundaryViolations+"건 — 현장 수리 시도 (제23조①1)");
            Finished=true;
        }

        // 현장 수리 시도. 거부가 곧 채점 항목이다 — 설교가 아니라 감점으로 전달된다.
        public bool TryFieldRepair()
        {
            if(Target==null)return false;
            if(Target.CanFieldRepair(out var reason))return true;
            RoleBoundaryViolations++;LastReason=reason;
            if(Responder!=null)Responder.ShowFeedback(reason);
            return false;
        }

        public bool Rewind(string stepId)
        {
            bool rewound=Runner.Rewind(stepId);
            if(rewound){Finished=false;findings.Clear();RefreshPrompt();}
            return rewound;
        }
        public void Restart(bool hideChecklist)
        {
            Runner.ResetRun();Target?.ResetInspection();GazeTracker?.Reset();
            findings.Clear();Finished=false;RoleBoundaryViolations=0;MisjudgementCount=0;
            ChecklistVisible=!hideChecklist;LastReason="";RefreshPrompt();
        }

        // InteractionPrompt 는 virtual 이 아니므로 Prompt 필드를 갱신한다 — 기존 계약을 바꾸지 않는다.
        private void RefreshPrompt()
        {
            if(Target==null)return;
            var decision=Peek();
            Target.Prompt=decision.Step==null?"점검 완료":decision.Step.Label;
            Target.UnavailableMessage=decision.Reason??"지금은 사용할 수 없습니다";
            Target.InteractionEnabled=true;
        }
    }
}
