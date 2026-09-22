using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChooGuard.App.Mvp
{
    public sealed class MvpTrainingDirector : MonoBehaviour
    {
        [SerializeField] private MvpWorkspace workspace;
        public MvpStationView Station;
        public MvpPhysicsBridge Physics;
        public MvpTeamTaskController TeamTasks;
        private bool subscribed,running,complete;
        private int speedIndex,seed,population;
        private float budget,incidentAt;
        private string phase="ready";
        private bool incidentRequested,showDebrief;
        private string scenario,status="참조모델 실험 준비",feedback,lastTeamState;
        private readonly List<string> history=new List<string>();
        private readonly Queue<MvpControlOrder> actions=new Queue<MvpControlOrder>();
        private bool hasReplay,showScope;
        private string priorOutcome,replayExpectedDigest;
        public void RefreshDisplay()=>Render();
        private static readonly int[] Speeds={1,2,4};
        public string State=>phase=="resolved"?"Recovery":phase=="incomplete"?"Incomplete":running?"Running":"Ready";
        public string Phase=>phase;
        public void RecordTeamEvent(string text,string teamId=null){Record(text);workspace?.AddOperationNotice("team:"+SimulatedSeconds+":"+text,"[팀] "+SimulatedSeconds.ToString("0")+"초 · 참조 홀\n"+text,teamId);Render();}
        public void RecordAgencyEvent(string text,string teamId=null){Record(text);workspace?.AddOperationNotice("agency:"+SimulatedSeconds+":"+text,"[기관] "+SimulatedSeconds.ToString("0")+"초 · "+text,teamId);Render();}
        public bool IsPaused { get; private set; }
        public float SimulatedSeconds { get; private set; }
        public float Progress=>Physics!=null && Physics.LastResult!=null ? (float)Physics.LastResult.evacuated/Mathf.Max(1,Physics.LastResult.total):0;
        public IReadOnlyList<string> Events=>history.AsReadOnly();
        public int Speed=>Speeds[speedIndex];
        public bool CanPlanAgencyOrders=>running&&Physics!=null&&Physics.Ready&&!Physics.Failed;
        public bool CanExecuteTasks=>running&&!IsPaused&&Physics!=null&&!Physics.Failed;
        public void Bind(MvpWorkspace value) { Unsubscribe();workspace=value;if(Physics==null) Physics=GetComponent<MvpPhysicsBridge>(); if(isActiveAndEnabled) Subscribe();Render(); }
        private void Awake() { if(workspace==null) workspace=GetComponent<MvpWorkspace>(); if(Physics==null) Physics=GetComponent<MvpPhysicsBridge>(); }
        private void OnEnable() { Subscribe(); }
        private void Start() { Render(); }
        private void Subscribe()
        {
            if(subscribed || workspace==null || Physics==null) return;
            workspace.TrainingStartRequested+=StartExercise;workspace.TrainingPauseRequested+=TogglePause;workspace.TrainingSpeedRequested+=CycleSpeed;workspace.TrainingActionRequested+=Act;
            if(TeamTasks!=null)TeamTasks.TaskReady+=ReleaseTask;
            Physics.ResultReceived+=Receive;Physics.StatusChanged+=ReceiveStatus;subscribed=true;
        }
        private void Unsubscribe()
        {
            if(!subscribed) return;
            if(workspace!=null) { workspace.TrainingStartRequested-=StartExercise;workspace.TrainingPauseRequested-=TogglePause;workspace.TrainingSpeedRequested-=CycleSpeed;workspace.TrainingActionRequested-=Act; }
            if(TeamTasks!=null)TeamTasks.TaskReady-=ReleaseTask;
            if(Physics!=null) { Physics.ResultReceived-=Receive;Physics.StatusChanged-=ReceiveStatus; }subscribed=false;
        }
        public void StartExercise() => StartConfigured(false);
        public void ReplaySameSeed() => StartConfigured(true);
        public void StartCrowdSession(int selectedSeed=271828){seed=selectedSeed;scenario="crowd_medical";population=50;hasReplay=true;StartConfigured(true,false);}
        private void StartConfigured(bool replay,bool compareDigest=true)
        {
            if(Physics==null) { status="계산 워커 연결 없음";Render();return; }
            replayExpectedDigest=replay&&compareDigest&&Physics.LastResult!=null?Physics.LastResult.initialStateDigest:null;
            if(!replay||!hasReplay){seed=UnityEngine.Random.Range(1,int.MaxValue);var random=new System.Random(seed);scenario=random.Next(2)==0?"fire_smoke":"crowd_medical";population=50;}else if(Physics.LastResult!=null)priorOutcome="직전 실행 "+Physics.LastResult.evacuated+"/"+Physics.LastResult.total+"명 · "+Physics.LastResult.simTime.ToString("0")+"초";hasReplay=true;population=50;incidentAt=240+new System.Random(seed).Next(181);phase="ordinary";incidentRequested=false;showDebrief=false;
            history.Clear();workspace.ClearOperationNotices();actions.Clear();if(TeamTasks!=null)TeamTasks.ResetTasks();running=true;complete=false;IsPaused=false;WorkspaceCancelPlans();speedIndex=0;SimulatedSeconds=0;budget=0;feedback=null;
            GetComponent<MvpAgencyDispatchController>()?.ResetMissions();
            status="평시 운영 준비";Record((scenario=="fire_smoke"?"화재·연기":"군중·병목")+" 사건 예정 · 평시 인원 "+population+" · 난수 "+seed);
            if(Station!=null) { Station.SetCrowd(Array.Empty<MvpPhysicsAgent>());Station.SetScenarioMarker(scenario);Station.SetIncidentVisible(false);Station.ResetAcceptedClock(); if(GetComponent<MvpAgencyDispatchController>()!=null){workspace.SelectFloor(0);Station.FocusCity();}else Station.FocusReferenceHall(); }
            Physics.StartRoutineRun(seed,population,scenario);Render();
        }
        public bool BeginIncident()
        {
            if(!running||phase!="ordinary"||Physics==null||!Physics.Ready||Physics.InFlight)return false;
            if(!Physics.BeginIncident())return false;
            incidentRequested=true;Record("사건 발생 요청 · 같은 시민 상태 유지");return true;
        }
        public void TogglePause() { if(!running)return;IsPaused=!IsPaused;Record(IsPaused?"계산 요청 일시 정지":"계산 요청 재개");Render(); }
        public void CycleSpeed() { speedIndex=(speedIndex+1)%Speeds.Length;Record("요청 속도 "+Speed+"배");Render(); }
        private void WorkspaceCancelPlans(){workspace.CancelPlannedOrders();}
        public void Act(string action)
        {
            if(action=="incident"){if(!BeginIncident())feedback="평시 계산 응답 뒤 사건을 발생시킬 수 있습니다.";Render();return;}if(action=="debrief"){showDebrief=!showDebrief;Render();return;}if(action=="replay"){ReplaySameSeed();return;}if(action=="scope"){showScope=!showScope;Render();return;}
            if(!running) { feedback="무작위 실험을 먼저 시작하세요.";Render();return; }
            if(Physics.Failed) { feedback="계산 불가 상태입니다. 새 실험으로 연결을 다시 시작하세요.";Render();return; }
            if(phase=="ordinary"){feedback="평시에는 팀을 먼저 배치하세요. 사건 발생 후 대응 지시가 가능합니다.";Render();return;}
            string policy=null;int cohort=-1;if(action=="cohort_release"){action="evacuate";policy="staged";cohort=workspace.SelectedCohort;}else if(action=="cohort_hold"){action="evacuate";policy="hold";cohort=workspace.SelectedCohort;}else if(action=="evacuate")policy="all";
            if(action!="warn"&&action!="evacuate"&&action!="medical")return;
            if(actions.Count>=8) { feedback="명령 처리 대기 중입니다.";Render();return; }
            if(TeamTasks==null || !TeamTasks.RequestTask(action,policy,cohort)) { feedback="대응팀의 진행 중인 작업을 먼저 확인하세요.";Render();return; }
            Record(action=="warn"?"경고 방송 요청":action=="evacuate"?"대피 유도 요청":"의료 지원 요청");feedback="대응팀이 도착해 현장 작업을 수행한 뒤 계산에 반영됩니다.";Render();
        }
        private void ReleaseTask(MvpControlOrder order) { if(running) { actions.Enqueue(order);Record("현장 작업 실행 · "+(order.Action=="warn"?"경고 방송":order.Action=="evacuate"?"집단 통제":"의료 지원 요청")); } }
        private void Receive(MvpPhysicsResult result)
        {
            if(replayExpectedDigest!=null&&result.initialStateDigest!=replayExpectedDigest){running=false;Physics.Cancel();status="재실행 초기 조건 불일치 · 비교 불가";Render();return;}
            float next=result.lifecycleVersion==1?result.sessionSimTime:result.simTime;
            float acceptedDelta=Mathf.Max(0,next-SimulatedSeconds);SimulatedSeconds=next;
            Station?.AdvanceAcceptedTime(acceptedDelta);GetComponent<MvpAgencyDispatchController>()?.AdvanceAcceptedTime(acceptedDelta);feedback=null;TeamTasks?.Observe(result);
            if(Station!=null)Station.SetCrowd(result.agents);
            string previous=phase;phase=result.phase;
            if(phase=="ordinary")workspace.AddOperationNotice("phase:ordinary","[운영] 대합실 · 시민 50명\n평시 운영 시작 · 팀 사전 배치","ops-1");
            if(previous!=phase)
            {
                workspace.AddOperationNotice("phase:"+phase,"["+(phase=="incident"?"사건":phase=="resolved"?"복구":phase=="incomplete"?"범위 종료":"운영")+"] "+SimulatedSeconds.ToString("0")+"초 · 대합실\n"+(phase=="incident"?"같은 시민 대응 시작 · 팀 선택":phase=="resolved"?"영향 인원 출구 도달 · 재개 판정 없음":"계산 범위 종료 · 미완료"),phase=="incident"?"ops-1":null);
                if(phase=="incident"){Record("사건 발생 · 평시 시민과 배치 유지");Station?.SetIncidentVisible(true);}
                if(phase=="resolved"){complete=true;Record("영향 인원 출구 도달 · 후속 조치와 복기 가능 · 안전 재개 판정 아님");Station?.SetIncidentVisible(false);}
                if(phase=="incomplete"){Record("화재장 계산 범위 종료 · 미완료 · 현재 안전 판정 불가");IsPaused=true;}
            }
            GetComponent<MvpProgressionGraph>()?.ObserveModel(result,SimulatedSeconds);
            if(phase=="incomplete")GetComponent<MvpProgressionGraph>()?.ObserveCalculationFailure("계산 범위 종료 · 과거 관찰은 현재 완료 판정이 아닙니다",result,SimulatedSeconds);
            GetComponent<MvpAgencyDispatchController>()?.ObserveAcceptedResult(result);
            status=phase=="ordinary"?"평시 운영 · 팀 사전 배치":phase=="resolved"?"복구 운영 · 후속 조치":phase=="incomplete"?"계산 범위 종료 · 미완료":"사건 대응 · 현장 지시";
            Render();
        }
        private void ReceiveStatus(string text) { status=text;if(Physics!=null&&Physics.Failed) { running=false;GetComponent<MvpProgressionGraph>()?.ObserveCalculationFailure("계산 중단 · 완료 판정 없음",Physics.LastResult,SimulatedSeconds);Record("계산 중단 · 완료 판정 없음"); }Render(); }
        public void Tick(float delta)
        {
            if(!running || IsPaused || Physics==null || Physics.Failed || float.IsNaN(delta) || float.IsInfinity(delta) || delta<0)return;
            var teamState="";foreach(var team in workspace.TeamState)teamState+=team.Id+":"+team.LocationId+";";
            if(teamState!=lastTeamState) { lastTeamState=teamState;Record("승인된 대응팀 배치 변경"); }
            workspace.ProcessPlannedOrders();if(workspace.HasPendingTeamOrders)return;
            if(!Physics.Ready || Physics.InFlight)return;
            if(phase=="incomplete")return;
            if(phase=="ordinary"&&!incidentRequested&&SimulatedSeconds>=incidentAt){BeginIncident();return;}
            if(actions.Count>0) { var order=actions.Peek();if(Physics.SendControl(order)){TeamTasks?.ControlSent(order,Physics.LastResult!=null?Physics.LastResult.releaseRevision:0);actions.Dequeue();}return; }
            budget=Mathf.Min(2,budget+delta*Speed);
            if(budget>=1 && Physics.SendAction("advance",1))budget-=1;
        }
        private void Update()=>Tick(Time.unscaledDeltaTime);
        private void Record(string text) { if(history.Count>=2000)history.RemoveAt(0);history.Add(SimulatedSeconds.ToString("0.0")+"초 · "+text); }
        private void Render()
        {
            if(workspace==null || workspace.transform.Find("WorkspaceCanvas")==null)return;
            string title=(IsPaused?(Physics!=null&&Physics.InFlight?"정지 요청 · 진행 중 응답 대기 · ":"일시 정지 · "):"")+status;
            string objective=phase=="ordinary"?"평시 팀 배치 → 사건 발생 → 대응 → 복구·복기":phase=="resolved"?"팀 재배치와 의료 지원 요청 후 결정을 복기하세요.":running||complete?(scenario=="fire_smoke"?"연기 노출과 대피 순서를 판단하세요.":"병목을 관찰하고 집단 유도 순서를 정하세요."):"무작위 현장 시작 → 멈춰서 계획 → 재개";
            var result=Physics!=null?Physics.LastResult:null;
            string detail="운영 시간 "+SimulatedSeconds.ToString("0.0")+"초 · "+Speed+"배";
            if(result!=null&&result.incidentTime>=0)detail+=" · 사건 "+result.incidentTime.ToString("0.0")+"초";
            if(result!=null&&phase!="ordinary")detail+="\n출구 도달 "+result.evacuated+" / "+result.total+"명 · 밀도 "+result.density.ToString("0.00")+"명/㎡\n"+(result.fieldCurrent?"가시거리 "+result.visibility.ToString("0.0")+"m · ":result.fireRequired?"노출 계산 종료 · 기록값 ":"군중 계산 · ")+"최대 노출 "+result.maxFedToxic.ToString("0.000000");
            detail+="\n"+(feedback??(IsPaused?"계획 중 · 지시를 예약하고 재개하세요.":(phase=="ordinary"?"유한 시민 50명 · 4~7분 사이 사건 발생":phase=="resolved"?"영업 재개·환자 회복 판정은 제공하지 않습니다.":"대기 집단도 연기에 노출됩니다.")));
            if(complete&&priorOutcome!=null)detail+="\n"+priorOutcome;
            if(showScope)detail="30×20m 기준 공간 · 화재장 0~120초\n보행 0.05초 · 초기 위치로 3집단 고정\n부산역 보정/임상/압착력 미지원\n집단 순응은 가정 · 재실행 때 팀 배치는 유지";
            string cohortText="집단 계산 결과를 기다리고 있습니다.";
            if(result!=null&&result.cohortStats!=null&&result.cohortStats.Length>0)
            {
                cohortText="";MvpCohortStats selected=null;
                foreach(var cohort in result.cohortStats)
                {
                    if(cohort==null)continue;
                    cohortText+="집단 "+(cohort.id+1)+"  "+(cohort.isReleased?"유도 중":"대기")+" · 남음 "+cohort.active+" / "+cohort.total+" · 도달 "+cohort.evacuated+"\n";
                    if(cohort.id==workspace.SelectedCohort)selected=cohort;
                }
                cohortText+=selected!=null
                    ?"선택 집단 최대 노출 "+selected.maxFedToxic.ToString("0.000000")+" · 열 "+selected.maxFedConvectiveHeat.ToString("0.000000")
                    :"선택한 집단의 계산 결과가 아직 없습니다.";
            }
            string timeline="운영 복기 · "+status+"\n스크롤하여 전체 결정 기록 보기\n\n";if(showDebrief)foreach(string entry in history)timeline+=entry+"\n";
            workspace.SetDebriefDisplay(showDebrief,timeline);
            workspace.SetContextPhase(phase);workspace.SetCohortDisplay(cohortText);
            workspace.SetTrainingDisplay(title,objective,detail,Progress);
        }
        private void OnDisable() { Unsubscribe();if(Physics!=null)Physics.Cancel(); }
        private void OnDestroy()=>Unsubscribe();
    }
}
