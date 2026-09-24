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
    //
    // 세션은 하나인데 대상은 여럿이다(jev-inject-structure-005:
    // session_and_terminal=one_each_hoisted 0.87, inspectable_scope=all_twelve_with_per_unit_state 0.65).
    // 유닛마다 자기 러너와 자기 진행 상태를 갖고, 세션은 그중 하나를 '활성'으로 가리킨다.
    // 그래서 기술자를 기다리는 동안 다른 소화기로 걸어가 점검할 수 있다.
    public sealed class TutorialSession : MonoBehaviour
    {
        // 한 유닛의 배선. 설비와 그 설비 전용 기술자 인계를 짝지어 둔다.
        [System.Serializable]
        public sealed class UnitBinding
        {
            public FacilityInspectable Facility;
            public TechnicianDispatch Dispatch;
        }

        public FirstPersonResponder Responder;
        public FpsGazeTracker GazeTracker;
        [SerializeField] private FacilityInspectable target;
        [SerializeField] private TechnicianDispatch dispatch;
        public AuditTerminal AuditTerminal;
        [Header("다중 대상 (비면 Target 하나)")]
        public List<UnitBinding> Units=new List<UnitBinding>();
        public TextAsset ProcedureAsset;
        public GameObject InspectionTagPrefab;
        [Header("모드")]
        public bool ChecklistVisible=true;
        [Header("판정 입력 (플레이어가 고른다)")]
        public InspectionVerdict PendingVerdict=InspectionVerdict.FIT;
        // 인계 진행을 프레임 시간으로 밀어준다. 실패 조건이 아니라 경과일 뿐이다.
        // 시험은 이 값을 false 로 두고 Dispatch.Tick 을 직접 먹여 결정론을 지킨다.
        [Header("인계 진행")]
        public bool DriveHandoffWithFrameTime=true;
        // 이월 기록. 시험은 false 로 두거나 TutorialCarryoverStore.OverridePath 로 임시 경로를 꽂는다.
        [Header("지속 상태 이월")]
        public bool WriteCarryover=true;
        public bool ApplyCarryoverOnStart=true;

        // 유닛별 진행 상태. 활성 유닛이 바뀌어도 각자의 러너와 지적 사항이 그대로 남는다.
        private sealed class UnitState
        {
            public FacilityInspectable Facility;
            public TechnicianDispatch Dispatch;
            public ProcedureRunner Runner;
            public bool LoadAttempted;
            public bool Finished;
            public int RoleBoundaryViolations;
            public int MisjudgementCount;
            public readonly List<string> Findings=new List<string>();
            // 구독을 해제하려면 참조를 들고 있어야 한다. 익명 람다로 += 만 하면 OnDestroy 에서 뗄 수 없고,
            // 설비가 세션보다 오래 살면 죽은 세션의 핸들러가 계속 발화한다(생성기가 둘의 수명을 분리한다).
            public System.Action<FirstPersonResponder> Handler;
        }

        private readonly List<UnitState> units=new List<UnitState>();
        private readonly Dictionary<string,RuleTruth> facts=new Dictionary<string,RuleTruth>();
        private int active=-1;
        private bool bound;

        private UnitState Current=>active>=0&&active<units.Count?units[active]:null;

        // 활성 대상. Units 가 비어 있으면 이것 하나로 세션을 구성한다(기존 단일 대상 동작).
        //
        // 대입은 활성 전환으로 해석한다. 리팩터링 전에는 이 필드가 유일한 진실 공급원이라
        // 대입만으로 대상이 바뀌었는데, 읽기 전용 뷰로 두면 그 의미론이 조용히 사라진다.
        public FacilityInspectable Target
        {
            get=>target;
            set
            {
                target=value;
                if(units.Count==0||value==null)return;     // 아직 구성 전이면 시작 대상으로만 쓰인다
                if(!Activate(value))
                    Debug.LogWarning("[TutorialSession] Units 에 없는 설비를 Target 으로 지정했습니다 · "+value.name,this);
            }
        }

        // 활성 유닛의 기술자 인계. 없으면 요구 발행이 플래그로만 남는다.
        public TechnicianDispatch Dispatch
        {
            get=>dispatch;
            set
            {
                dispatch=value;
                var current=Current;
                if(current!=null)current.Dispatch=value;    // 대입이 실제로 활성 유닛에 반영되어야 한다
            }
        }

        public ProcedureRunner Runner=>Current?.Runner;
        public string LastReason { get; private set; }="";
        public bool Finished=>Current!=null&&Current.Finished;
        public int RoleBoundaryViolations=>Current?.RoleBoundaryViolations??0;
        public int MisjudgementCount=>Current?.MisjudgementCount??0;
        public FacilityInspectable ActiveFacility=>Current?.Facility;
        public int UnitCount=>units.Count;

        // 감사 결과는 전체 유닛을 모은다 — 단말 하나가 역사 전체를 보고해야 한다.
        // 유닛이 하나면 그 유닛의 지적만 나오므로 기존 동작과 같다.
        //
        // 매번 새 목록을 만든다. 내부 리스트를 재사용해 돌려주면 AuditTerminal.LastReport 가
        // 그 리스트를 그대로 들고 있게 되어, 다음 감사가 이미 보관된 보고서를 조용히 바꾼다.
        public IReadOnlyList<string> AuditFindings
        {
            get
            {
                var snapshot=new List<string>();
                foreach(var u in units)snapshot.AddRange(u.Findings);
                return snapshot;
            }
        }

        private void Awake()
        {
            if(Responder==null)Responder=GetComponentInParent<FirstPersonResponder>();
            if(GazeTracker==null&&Responder!=null)GazeTracker=Responder.GetComponent<FpsGazeTracker>();
            // 유닛을 여기서 만들지 않는다. AddComponent<T>() 는 Awake 를 그 줄에서 동기 실행하므로
            // 호출자가 다음 줄에서 Target·Units·ProcedureAsset 을 꽂으면 이미 늦다.
        }

        // 최초 사용 시 한 번 구성한다. Units 가 우선이고, 비어 있으면 Target 하나로 만든다.
        private void BuildUnitsIfNeeded()
        {
            if(units.Count>0)return;
            if(Units!=null)
                foreach(var binding in Units)
                    if(binding!=null&&binding.Facility!=null)
                        units.Add(new UnitState{Facility=binding.Facility,Dispatch=binding.Dispatch});
            if(units.Count==0&&Target!=null)
                units.Add(new UnitState{Facility=Target,Dispatch=Dispatch});
            if(units.Count==0)return;

            // Target 이 목록 안에 있으면 거기서 시작한다 — 생성기가 튜토리얼 대상을 지정한 의도를 지킨다.
            active=0;
            if(Target!=null)
                for(int i=0;i<units.Count;i++)if(units[i].Facility==Target){active=i;break;}
            if(GazeTracker!=null)foreach(var u in units)u.Facility.Bind(GazeTracker);
            SyncActive();
        }

        // 활성 유닛을 Target·Dispatch 에 비춘다. 속성이 아니라 backing field 에 직접 쓴다 —
        // 속성 setter 가 다시 Activate 를 부르면 재귀가 된다.
        private void SyncActive()
        {
            var current=Current;
            if(current==null)return;
            target=current.Facility;
            dispatch=current.Dispatch;
        }

        public bool Activate(FacilityInspectable facility)
        {
            if(facility==null)return false;
            for(int i=0;i<units.Count;i++)
                if(units[i].Facility==facility){active=i;SyncActive();return true;}
            return false;
        }

        // 최초 사용 시 유닛마다 절차를 읽는다. 실패는 유닛마다 한 번만 크게 알린다.
        public bool EnsureLoaded()
        {
            BuildUnitsIfNeeded();
            if(units.Count==0)return false;
            bool allReady=true;
            foreach(var u in units)
            {
                if(u.Runner==null)u.Runner=new ProcedureRunner();
                if(u.Runner.Ready)continue;
                if(u.LoadAttempted){allReady=false;continue;}
                u.LoadAttempted=true;
                if(u.Runner.Load(ProcedureAsset))continue;
                Debug.LogError("[TutorialSession] 절차를 읽지 못해 세션을 시작할 수 없습니다 · "+u.Runner.StatusReason,this);
                allReady=false;
            }
            return allReady;
        }

        // 자료를 바꿔 끼울 때만 쓴다. 모든 유닛이 다시 읽는다.
        public bool Reload(TextAsset asset)
        {
            ProcedureAsset=asset;
            foreach(var u in units){u.LoadAttempted=false;u.Runner=null;}
            return EnsureLoaded();
        }

        // 이월은 Bind 보다 먼저 적용한다 — 프롬프트가 이월된 월드 상태를 보고 만들어져야 한다.
        private void Start(){EnsureLoaded();if(ApplyCarryoverOnStart)ApplyCarryover();Bind();RefreshPrompt();}

        // E 키 경로를 절차에 연결한다. 게이트는 세션이 소유하고 설비는 델리게이트만 안다.
        // 유닛마다 꽂되, 각 델리게이트는 자기 유닛의 상태만 본다 — 활성 유닛이 아니어도 올바른 사유가 뜬다.
        public void Bind()
        {
            if(bound)return;
            EnsureLoaded();
            if(units.Count==0)return;
            foreach(var u in units)
            {
                var unit=u;                     // 클로저가 반복 변수를 잡지 않도록 복사한다
                unit.Facility.GateReason=()=>{var decision=PeekFor(unit);return decision.Allowed?null:decision.Reason;};
                unit.Handler=_=>{Activate(unit.Facility);Advance();};
                unit.Facility.InteractionPerformed+=unit.Handler;
            }
            // 판정 단말도 같은 규율로 꽂는다 — 단말은 이 타입을 모르고 델리게이트 두 개만 받는다.
            // 절차 진행 중에도 단말은 월드에 있고 거부만 한다. 감사는 사후 열거다.
            if(AuditTerminal!=null)
            {
                AuditTerminal.GateReason=()=>Finished?null:"점검이 끝나지 않았습니다 · "+(Peek().Step?.Label??"진행 중");
                AuditTerminal.ReportSource=()=>new AuditReport(AuditFindings,Target!=null?Target.name:"점검 대상");
            }
            bound=true;
        }

        // 인계는 활성 유닛만이 아니라 전부 진행시킨다 — 다른 소화기를 점검하는 동안에도 기술자는 움직인다.
        private void Update()
        {
            if(!DriveHandoffWithFrameTime)return;
            foreach(var u in units)u.Dispatch?.Tick(Time.deltaTime);
        }

        // 플레이어의 결과 확인. 확인은 플레이어의 행동이므로 세션이 사유를 소유한다.
        public bool WitnessRepairResult()
        {
            var current=Current;
            if(current?.Dispatch==null){LastReason="인계 대상이 없습니다";return false;}
            if(!current.Dispatch.Requested){LastReason="폐기·교체 요구를 발행하지 않았습니다";Responder?.ShowFeedback(LastReason);return false;}
            if(!current.Dispatch.WitnessResult(out var reason)){LastReason=reason;Responder?.ShowFeedback(reason);return false;}
            LastReason="교체 결과 확인 · "+current.Dispatch.RequestedSerial+" → "+current.Facility.SerialNumber;
            RefreshPrompt();return true;
        }

        // 꽂은 것은 전부 뗀다. 설비는 세션보다 오래 살 수 있으므로(생성기가 유닛 루트와 세션 호스트를
        // 따로 둔다) 구독이 남으면 죽은 세션의 Advance·Audit·이월 기록이 한 번 더 발화한다.
        private void OnDestroy()
        {
            foreach(var u in units)
            {
                if(u.Facility==null)continue;
                u.Facility.GateReason=null;
                if(u.Handler!=null)u.Facility.InteractionPerformed-=u.Handler;
                u.Handler=null;
            }
            if(AuditTerminal!=null){AuditTerminal.GateReason=null;AuditTerminal.ReportSource=null;}
        }

        // 월드 상태를 사실로 환산한다. 여기가 world_evidence 규율이 사는 곳이다 —
        // 버튼을 눌렀는가가 아니라 월드가 어떤 상태인가만 본다.
        public RuleFacts Observe(){return ObserveFor(Current);}

        private RuleFacts ObserveFor(UnitState unit)
        {
            facts.Clear();
            var facility=unit?.Facility;
            if(facility==null)return ProcedureRunner.FactsFrom(facts);
            var dispatch=unit.Dispatch;
            // ① ~ ④ 시선 체류. 관측하지 않은 것은 FALSE 가 아니라 UNKNOWN 이다 — 아직 안 본 것과 부적합은 다르다.
            Set("serial-gazed",facility.Observed("serial"));
            Set("spec-plate-gazed",facility.Observed("spec-plate"));
            Set("gauge-gazed",facility.Observed("gauge"));
            Set("body-gazed",facility.Observed("body"));
            // ③ 고유번호가 실제로 기재됐는가
            facts["serial-recorded"]=string.IsNullOrEmpty(facility.RecordedSerial)?RuleTruth.UNKNOWN:RuleTruth.TRUE;
            facts["verdict-recorded"]=facility.Verdict==InspectionVerdict.NOT_RECORDED?RuleTruth.UNKNOWN:RuleTruth.TRUE;
            facts["verdict-fit"]=facility.Verdict==InspectionVerdict.FIT?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            facts["verdict-unfit"]=facility.Verdict==InspectionVerdict.UNFIT?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            // 월드의 진짜 상태. 플레이어 판정과 대조해 오판정을 잡는다.
            facts["corroded"]=facility.Corroded?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["mechanically-defective"]=facility.MechanicallyDefective?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["pressure-out-of-range"]=facility.PressureOutOfRange?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["expiry-passed"]=facility.ExpiryPassed?RuleTruth.TRUE:RuleTruth.FALSE;
            // ⑤ 잔존물
            facts["repair-order-issued"]=facility.RepairOrderIssued?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            facts["tag-attached"]=facility.AttachedTag!=null?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            // ⑥ 기술자 인계. 요청이 곧 완료가 아니므로 단계별로 따로 읽는다.
            facts["technician-received"]=dispatch!=null&&dispatch.Requested?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            facts["technician-onsite"]=dispatch!=null&&dispatch.OnSite?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            facts["technician-completed"]=dispatch!=null&&dispatch.Completed?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            facts["replacement-witnessed"]=dispatch!=null&&dispatch.ResultWitnessed?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            facts["service-completed"]=facility.ServiceCompleted?RuleTruth.TRUE:RuleTruth.UNKNOWN;
            return ProcedureRunner.FactsFrom(facts);
        }
        private void Set(string key,bool observed)=>facts[key]=observed?RuleTruth.TRUE:RuleTruth.UNKNOWN;

        public ProcedureRunner.Decision Peek(){EnsureLoaded();return PeekFor(Current);}

        private ProcedureRunner.Decision PeekFor(UnitState unit)
        {
            if(unit?.Runner==null)
                return new ProcedureRunner.Decision{Allowed=false,Reason="점검 대상이 없습니다",Guard=RuleTruth.UNKNOWN};
            return unit.Runner.Next(ObserveFor(unit));
        }

        // 다음 단계를 한 번 시도한다. E 상호작용과 시험이 같은 경로를 쓴다.
        public bool Advance()
        {
            EnsureLoaded();
            var unit=Current;
            if(unit?.Runner==null){LastReason="점검 대상이 없습니다";return false;}
            if(unit.Finished)return false;
            var decision=PeekFor(unit);
            LastReason=decision.Reason;
            if(!decision.Allowed){RefreshPrompt();return false;}
            ApplyEffect(unit,decision.Step);
            // 효과가 월드를 바꿨으니 사실을 다시 읽고 같은 단계로 커밋을 다시 판정한다.
            var confirm=unit.Runner.Next(ObserveFor(unit));
            bool committed=confirm.Allowed&&confirm.Step==decision.Step&&unit.Runner.Commit(confirm);
            if(!committed){LastReason="월드 상태가 단계 요건을 충족하지 못해 완료로 기록하지 않았습니다 · "+decision.Step.Label;RefreshPrompt();return false;}
            LastReason=decision.Step.Label+" 완료";
            // 피드백은 기존 TryInteract 가 이 핸들러 다음에 SuccessMessage 를 읽으므로
            // '방금 끝낸 단계'를 여기서 넣는다. RefreshPrompt 는 다음 단계만 다룬다.
            unit.Facility.SuccessMessage=LastReason;
            if(decision.Step.Effect=="close-inspection")Audit();
            RefreshPrompt();return true;
        }

        private void ApplyEffect(UnitState unit,ProcedureRunner.Step step)
        {
            var facility=unit.Facility;
            switch(step.Effect)
            {
                case "record-serial":facility.RecordSerial();break;
                case "record-verdict":
                    facility.RecordVerdict(PendingVerdict);
                    // 제23조②1 — 기한 전이라도 부식·결함이면 폐기다. 기한만 보고 통과시키면 오판정이다.
                    if(facility.ShouldBeUnfit&&PendingVerdict==InspectionVerdict.FIT)unit.MisjudgementCount++;
                    break;
                case "issue-repair-order":
                    facility.IssueRepairOrder();
                    // 요구는 여기서 끝나지 않는다. 기술자가 수신·이동·수행하는 동안 플레이어는 다른 일을 한다.
                    unit.Dispatch?.Request(facility.RecordedSerial,"부적합 판정 · 폐기·교체 요구");
                    break;
                case "witness-replacement":
                    // 확인은 플레이어의 행동이다. 기술자가 끝냈다는 사실과 별개로 기록된다.
                    if(unit.Dispatch!=null&&!unit.Dispatch.WitnessResult(out var witnessReason))LastReason=witnessReason;
                    break;
                case "attach-tag":
                    var tag=InspectionTagPrefab!=null?Instantiate(InspectionTagPrefab):new GameObject("소화기 점검표");
                    tag.name="소화기 점검표 · "+facility.RecordedSerial;facility.AttachTag(tag);break;
                case "close-inspection":break;
            }
        }

        // 감사관 단말. 월드를 다시 읽어 미충족을 '위치 · 항목'으로 열거한다. 즉시 야단치지 않는다.
        // 활성 유닛의 점검을 닫는다. 다른 유닛의 지적은 각자의 목록에 남아 있다가 함께 보고된다.
        public void Audit()
        {
            var unit=Current;
            if(unit?.Runner==null)return;
            var facility=unit.Facility;
            unit.Findings.Clear();
            string place=facility!=null?facility.name:"대상 미지정";
            foreach(var step in unit.Runner.Unmet(ObserveFor(unit)))unit.Findings.Add(place+" · "+step.Label+" 미완료 — "+step.Basis);
            if(facility!=null)
            {
                if(facility.ShouldBeUnfit&&facility.Verdict==InspectionVerdict.FIT)
                    unit.Findings.Add(place+" · 오판정 — 부식·결함·기한 상태인데 적합으로 기재됨 (제23조②1)");
                if(facility.Verdict==InspectionVerdict.UNFIT&&!facility.RepairOrderIssued)
                    unit.Findings.Add(place+" · 폐기·교체 요구 인계 미발행 (제23조①1)");
            }
            // 인계는 발행으로 끝나지 않는다. 결과를 확인하지 않은 채 닫으면 그대로 열거된다 —
            // 막지는 않는다. 기다리는 것도 선택이고, 확인하지 않은 것도 기록이다.
            if(unit.Dispatch!=null&&unit.Dispatch.Requested&&!unit.Dispatch.ResultWitnessed)
                unit.Findings.Add(place+" · 교체 결과 미확인 — "+unit.Dispatch.StageLabel+" (제23조①1)");
            if(unit.RoleBoundaryViolations>0)
                unit.Findings.Add(place+" · 역할 경계 위반 "+unit.RoleBoundaryViolations+"건 — 현장 수리 시도 (제23조①1)");
            unit.Finished=true;
            // 점검을 닫는 순간이 결과가 확정되는 순간이다. 여기서만 기록한다 —
            // 진행 중에 쓰면 되감기·재시행이 남긴 중간 상태가 다음 회차로 새어 나간다.
            // 기록 실패를 삼키지 않는다. 콘솔에만 남기면 플레이어는 점검이 끝난 줄 알지만
            // 이번 회차 결과가 다음으로 넘어가지 않는다 — 감사 결과에 함께 띄운다.
            if(WriteCarryover&&!TutorialCarryoverStore.Record(unit.Runner.Id,BuildOutcome()))
                unit.Findings.Add(place+" · 이월 기록 실패 — 이번 점검 결과가 다음 회차로 넘어가지 않습니다");
        }

        // 활성 유닛의 결과. facilityId 는 유닛 이름을 쓴다 — 교체로 고유번호가 바뀌어도 그대로다
        // (생성기가 유닛 이름을 유일하게 만든다: FireExtinguisherSliceBuilder 의 UnitPrefix 규율).
        public FacilityOutcome BuildOutcome()
        {
            var unit=Current;
            var facility=unit?.Facility;
            if(facility==null)return null;
            return new FacilityOutcome
            {
                facilityId=facility.name,
                serial=facility.SerialNumber??"",
                replacedFromSerial=facility.ReplacedFromSerial??"",
                verdict=facility.Verdict.ToString(),
                tagAttached=facility.AttachedTag!=null,
                repairOrderIssued=facility.RepairOrderIssued,
                serviceCompleted=facility.ServiceCompleted,
                resultWitnessed=unit.Dispatch!=null&&unit.Dispatch.ResultWitnessed,
                closedAtUtc=System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
        }

        // 이전 회차 결과를 월드에 반영한다. 기록이 없으면 아무것도 하지 않는다 —
        // 튜토리얼을 건너뛴 것은 막을 일이 아니라 초기 조건이 다른 것이다.
        // 적용하는 것은 '월드가 실제로 어떻게 됐는가'뿐이다. 플레이어가 기재한 판정과
        // 점검표는 이번 회차에 다시 해야 하므로 되살리지 않는다.
        public bool ApplyCarryover()
        {
            BuildUnitsIfNeeded();
            if(units.Count==0)return false;
            var data=TutorialCarryoverStore.Load();
            bool any=false;
            foreach(var u in units)
            {
                var facility=u.Facility;
                if(facility==null)continue;
                var outcome=data.Find(facility.name);
                if(outcome==null)continue;
                any=true;
                if(outcome.serviceCompleted&&!facility.ServiceCompleted)
                {
                    facility.ApplyReplacement(outcome.serial);
                    LastReason="이전 점검에서 교체된 설비입니다 · "+outcome.replacedFromSerial+" → "+outcome.serial;
                }
            }
            return any;
        }

        // 현장 수리 시도. 거부가 곧 채점 항목이다 — 설교가 아니라 감점으로 전달된다.
        public bool TryFieldRepair()
        {
            var unit=Current;
            if(unit?.Facility==null)return false;
            if(unit.Facility.CanFieldRepair(out var reason))return true;
            unit.RoleBoundaryViolations++;LastReason=reason;
            if(Responder!=null)Responder.ShowFeedback(reason);
            return false;
        }

        public bool Rewind(string stepId)
        {
            var unit=Current;
            if(unit?.Runner==null)return false;
            bool rewound=unit.Runner.Rewind(stepId);
            if(rewound){unit.Finished=false;unit.Findings.Clear();RefreshPrompt();}
            return rewound;
        }

        // 재시행은 전부 되돌린다. 한 유닛만 되돌리면 '같은 조건에서 다시 돈다'가 성립하지 않는다.
        public void Restart(bool hideChecklist)
        {
            foreach(var u in units)
            {
                // 순서가 중요하다 — 설비가 교체 전 상태를 되돌린 뒤 인계를 지운다.
                u.Runner?.ResetRun();
                u.Facility?.ResetInspection();
                u.Dispatch?.ResetHandoff();
                u.Findings.Clear();
                u.Finished=false;u.RoleBoundaryViolations=0;u.MisjudgementCount=0;
            }
            GazeTracker?.Reset();
            ChecklistVisible=!hideChecklist;LastReason="";RefreshPrompt();
        }

        // InteractionPrompt 는 virtual 이 아니므로 Prompt 필드를 갱신한다 — 기존 계약을 바꾸지 않는다.
        // 유닛마다 자기 다음 단계를 보여줘야 하므로 전부 갱신한다.
        private void RefreshPrompt()
        {
            foreach(var u in units)
            {
                if(u.Facility==null)continue;
                var decision=PeekFor(u);
                u.Facility.Prompt=decision.Step==null?"점검 완료":decision.Step.Label;
                u.Facility.UnavailableMessage=decision.Reason??"지금은 사용할 수 없습니다";
                u.Facility.InteractionEnabled=true;
            }
        }
    }
}
