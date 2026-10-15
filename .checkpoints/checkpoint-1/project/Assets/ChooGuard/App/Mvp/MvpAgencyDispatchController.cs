using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using ChooGuard.Contracts;
namespace ChooGuard.App.Mvp
{
    // Facility resources, logical task owners, and rendered members are deliberately separate.
    public sealed class MvpAgencyDispatchController : MonoBehaviour
    {
        [Serializable] public class Connector { public Vector3[] points; }
        [Serializable] public class Agency { public string id,label;public bool coordinateReady,dispatchSupported;public Vector3 point,pickCenter,pickSize,parkingForward;public Vector3[] polygon;public Connector[] departureConnectors; }
        [Serializable] public class Route { public string id,agencyId,direction,reason;public bool available;public Vector3[] points;public float distanceMeters; }
        [Serializable] public class TeamDefinition { public string id,agencyId,label,visual;public int memberCount;public string[] workTypes; }
        [Serializable] public class TaskDefinition { public string id,label,channel;public bool automaticReturn;public string[] actions; }
        [Serializable] public class TargetDefinition { public string id,label,scope;public Vector3 point;public Vector3[] polygon; }
        [Serializable] public class RouteData { public int version,sourceAgencyCount,maxQueuedJobsPerTeam;public float speedMetersPerSecond,memberHeadwayMeters,departureHeadwaySeconds;public Agency[] agencies;public Route[] routes;public TeamDefinition[] teams;public TaskDefinition[] tasks;public TargetDefinition[] targets; }
        [Serializable] public class MissionSnapshot { public string AgencyId,TeamId,TeamLabel,State,Reason,OperationId,WorkType,TargetId,Stage,Channel,LastCompletedOperationId;public float DistanceTravelled,RouteDistance,TurnaroundRemainingSeconds,TurnaroundTotalSeconds;public string TimingSource;public int VisualMemberCount,QueuedJobs;public Vector3 VehicleLocalPosition;public Vector3[] MemberPositions; }
        private sealed class PickVolume { public BoxCollider Collider;public string AgencyId,TargetId; }
        private readonly List<PickVolume> pickVolumes=new List<PickVolume>();
        private bool swallowRightRelease;
        private sealed class Work { public string Id,Type,Target,RequestId; }
        private sealed class Member { public Transform Body;public Vector3 BasePark,SitePark,From,To,BaseForward;public Vector3[] BasePath,PrefixPath,SuffixPath;public float Prefix,Suffix; }
        private sealed class Mission { public TeamDefinition Definition;public string State="Ready",Stage="",Channel,LastCompletedOperationId;public Work Work;public readonly Queue<Work> Queue=new Queue<Work>();public readonly List<Member> Members=new List<Member>();public Route Route;public float Distance,Span,DepartureAt,ReservationEnd;public string DepartureKey;public bool Departed,TurnaroundPrepared;public float OperationStartedSim,OnsiteAt,OutboundMetres,TurnaroundTotalSeconds;public string TimingSource;public ThirdParty.OpenRaCountdown Turnaround; }
        public TextAsset RouteAsset,SourceCatalogAsset;
        public TextAsset OfficialStationPickAsset;
        [Serializable] private sealed class OfficialStationPickData { public int version;public string targetId;public Vector3 center,size; }
        public MvpTrainingDirector Director;
        public MvpWorkspace Workspace;
        public MvpStationView Station;
        public GameObject FireEnginePrefab,AmbulancePrefab;
        private MvpFacilityResources FacilityResources=>GetComponent<MvpFacilityResources>();
        private MvpTeamTaskController resourceTaskSource;
        private MvpProgressionGraph Progression=>GetComponent<MvpProgressionGraph>();
        public const string Central119="busan-jungbu-central-119",Choryang119="busan-jungbu-choryang-119",StationTarget="busan-station-reference";
        private RouteData data;
        private readonly List<Mission> missions=new List<Mission>();
        private readonly Dictionary<string,Mission> leases=new Dictionary<string,Mission>();
        private readonly Dictionary<string,string> requestIds=new Dictionary<string,string>();
        private readonly HashSet<string> canceledOperations=new HashSet<string>();
        private readonly Dictionary<string,string> requestFingerprints=new Dictionary<string,string>();
        private readonly Dictionary<string,float> departures=new Dictionary<string,float>();
        private string pendingAgency,pendingWork;
        private bool pendingReinforcement;
        private int consumedFrame=-1;
        public string LastReason { get; private set; }
        public string LastOperationId { get; private set; }
        public string SelectedAgencyId { get; private set; }
        public string ContextKind { get; private set; }
        public bool HasContextSelection=>ContextKind!=null;
        public string SelectedOperationalTeamId { get; private set; }
        public bool HasPendingTarget=>pendingWork!=null;
        public string PendingWorkLabel=>Task(pendingWork)?.label;
        public int SourceAgencyCount=>data?.sourceAgencyCount??0;
        public int LogicalTeamCount=>missions.Count;
        public int VisualMemberCount { get {int n=0;foreach(var m in missions)n+=m.Members.Count;return n;} }
        public IReadOnlyList<Agency> Agencies=>data?.agencies??Array.Empty<Agency>();
        public IReadOnlyList<TaskDefinition> Tasks=>data?.tasks??Array.Empty<TaskDefinition>();
        public IReadOnlyList<TargetDefinition> Targets=>data?.targets??Array.Empty<TargetDefinition>();
        public IReadOnlyList<MissionSnapshot> MissionSnapshots
        {
            get {var snapshots=new List<MissionSnapshot>();foreach(var m in missions){var positions=new Vector3[m.Members.Count];for(int i=0;i<positions.Length;i++)positions[i]=m.Members[i].Body.localPosition;snapshots.Add(new MissionSnapshot{AgencyId=m.Definition.agencyId,TeamId=m.Definition.id,TeamLabel=m.Definition.label,State=m.State,Reason=MissionReason(m),OperationId=m.Work?.Id,WorkType=m.Work?.Type,TargetId=m.Work?.Target,Stage=m.Stage,Channel=m.Channel,LastCompletedOperationId=m.LastCompletedOperationId,DistanceTravelled=m.Distance,RouteDistance=m.Span,TurnaroundRemainingSeconds=m.Turnaround?.RemainingSeconds??0,TurnaroundTotalSeconds=m.TurnaroundTotalSeconds,TimingSource=m.TimingSource,VisualMemberCount=m.Members.Count,QueuedJobs=m.Queue.Count,VehicleLocalPosition=positions.Length>0?positions[0]:Vector3.zero,MemberPositions=positions});}return snapshots;}
        }
        private void Start()=>Initialize();
        public void Initialize()
        {
            SubscribeResourceExecution();
            if(data!=null)return;if(RouteAsset==null){LastReason="기관·업무 자료 없음";return;}
            data=JsonUtility.FromJson<RouteData>(RouteAsset.text);if(data==null||data.version!=2||data.teams==null){LastReason="기관·업무 자료 버전 불일치";return;}
            var slots=new Dictionary<string,int>();int siteSlot=0;
            foreach(var definition in data.teams)
            {
                var m=new Mission{Definition=definition};var agency=AgencyById(definition.agencyId);var outbound=FindRoute(definition.agencyId,"outbound");var prefab=definition.visual=="fire"?FireEnginePrefab:definition.visual=="ambulance"?AmbulancePrefab:null;
                if(!slots.ContainsKey(definition.agencyId))slots[definition.agencyId]=0;
                if(agency!=null&&agency.coordinateReady&&prefab!=null&&Station!=null&&outbound!=null)
                for(int i=0;i<definition.memberCount;i++)
                {
                    int slot=slots[definition.agencyId]++;var body=Instantiate(prefab,Station.transform).transform;body.name="AgencyMember_"+definition.id+"_"+i;SetLayer(body);
                    // Authored off-road parking/assembly slots, never a lateral road lane offset.
                    Vector3[] basePath=agency.departureConnectors!=null&&slot<agency.departureConnectors.Length?agency.departureConnectors[slot].points:null;
                    var park=basePath!=null&&basePath.Length>1?basePath[0]:agency.point+new Vector3((slot%2)*6-3,0,-12-(slot/2)*12);
                    var site=outbound.points[outbound.points.Length-1]+new Vector3((siteSlot%4)*6-9,0,-12-(siteSlot/4)*12);siteSlot++;
                    var forward=agency.parkingForward.sqrMagnitude>.01f?agency.parkingForward:Vector3.forward;body.localPosition=park;body.localRotation=Quaternion.LookRotation(forward);m.Members.Add(new Member{Body=body,BasePark=park,SitePark=site,BaseForward=forward,BasePath=basePath??new[]{park,outbound.points[0]}});
                }
                if(m.Members.Count!=definition.memberCount||!RoutesReady(definition.agencyId))m.State="Unavailable";missions.Add(m);
            }
            foreach(var agency in Agencies)if(agency.coordinateReady)CreatePickVolume(agency.polygon,24,agency.id,null,agency.pickCenter,agency.pickSize);
            foreach(var target in Targets)
            {
                if(target.id==StationTarget&&OfficialStationPickAsset!=null)
                {
                    var geometry=JsonUtility.FromJson<OfficialStationPickData>(OfficialStationPickAsset.text);
                    if(geometry!=null&&geometry.version==1&&geometry.targetId==StationTarget&&FinitePick(geometry.center)&&FinitePick(geometry.size)&&geometry.size.x>0&&geometry.size.y>0&&geometry.size.z>0)CreatePickVolume(target.polygon,geometry.size.y,null,target.id,geometry.center,geometry.size);
                    else LastReason="공식 부산역 선택 영역 자료를 확인하세요";
                }
                else if(target.id==StationTarget&&Station.WholeEnvelope!=null&&Station.WholeEnvelope.Find("공식 자료 부산역 역사")!=null)LastReason="공식 부산역 선택 영역 자료가 필요합니다";
                else CreatePickVolume(target.polygon,20,null,target.id);
            }
            ApplyTeamVisibility();
        }
        private static bool FinitePick(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
        private void CreatePickVolume(Vector3[] polygon,float height,string agencyId,string targetId,Vector3 pickCenter=default,Vector3 pickSize=default)
        {
            if(Station==null||polygon==null||polygon.Length<3)return;
            var bounds=new Bounds(polygon[0],Vector3.zero);foreach(var p in polygon)bounds.Encapsulate(p);
            if(pickSize.sqrMagnitude>.01f)bounds=new Bounds(pickCenter,pickSize);
            var go=new GameObject("OperationPick_"+(agencyId??targetId));go.transform.SetParent(Station.transform,false);go.layer=29;go.transform.localPosition=pickSize.sqrMagnitude>.01f?bounds.center:bounds.center+Vector3.up*(height*.5f);
            var collider=go.AddComponent<BoxCollider>();collider.size=pickSize.sqrMagnitude>.01f?pickSize:new Vector3(Mathf.Max(2,bounds.size.x),height+Mathf.Max(0,bounds.size.y),Mathf.Max(2,bounds.size.z));collider.isTrigger=targetId==StationTarget&&OfficialStationPickAsset!=null;
            pickVolumes.Add(new PickVolume{Collider=collider,AgencyId=agencyId,TargetId=targetId});
        }
        private void SubscribeResourceExecution()
        {
            var source=Director?.TeamTasks;if(source==resourceTaskSource)return;
            if(resourceTaskSource!=null)if(resourceTaskSource.OperationExecutionGate==ConsumeOperationResources)resourceTaskSource.OperationExecutionGate=null;
            resourceTaskSource=source;if(source!=null)source.OperationExecutionGate=ConsumeOperationResources;
        }
        private void OnDestroy(){if(resourceTaskSource!=null)if(resourceTaskSource.OperationExecutionGate==ConsumeOperationResources)resourceTaskSource.OperationExecutionGate=null;}
        private bool ConsumeOperationResources(string operationId)
        {
            foreach(var m in missions)if(m.Work?.Id==operationId)
            {
                var resources=FacilityResources;int before=resources?.Revision??0;
                if(resources==null||!resources.ConsumeForExecution(operationId,out var reason)){m.State="Blocked";LastReason="현장 지원 꾸러미 배정 확인 필요";Log(m,LastReason);return false;}
                if(resources.Revision!=before)Log(m,"예약 지원 꾸러미 사용 · 공동 업무당 한 번");return true;
            }
            return false;
        }
        public bool CanRequestWork(string agencyId,string workType,out string reason)
        {
            Initialize();if(Director==null||!Director.CanPlanAgencyOrders){reason="훈련 시작 후 계산 준비를 기다리세요";return false;}if(Progression==null||!Progression.Ready){reason="운영 흐름 준비가 필요합니다";return false;}if(FacilityResources==null){reason="기관 지원 꾸러미 자료 없음";return false;}
            if(!FacilityResources.CanReserve(agencyId,workType,out reason))return false;
            foreach(var m in missions)if(m.Definition.agencyId==agencyId&&m.State=="Ready"&&Compatible(m,workType)){reason="대응팀과 지원 꾸러미 예약 가능";return true;}
            reason="해당 업무의 가용 대응팀이 없습니다";return false;
        }
        private static void SetLayer(Transform t){t.gameObject.layer=29;foreach(Transform child in t)SetLayer(child);}
        private Agency AgencyById(string id){if(data?.agencies!=null)foreach(var a in data.agencies)if(a.id==id)return a;return null;}
        private TaskDefinition Task(string id){if(data?.tasks!=null)foreach(var t in data.tasks)if(t.id==id)return t;return null;}
        private TargetDefinition Target(string id){if(data?.targets!=null)foreach(var t in data.targets)if(t.id==id)return t;return null;}
        private Mission Find(string id)=>missions.Find(m=>m.Definition.id==id);
        private Route FindRoute(string id,string direction){if(data?.routes!=null)foreach(var r in data.routes)if(r.agencyId==id&&r.direction==direction&&r.available&&r.points!=null&&r.points.Length>1)return r;return null;}
        private bool RoutesReady(string id)=>FindRoute(id,"outbound")!=null&&FindRoute(id,"return")!=null;
        private bool Compatible(Mission m,string work)=>Array.IndexOf(m.Definition.workTypes,work)>=0;
        private bool Reject(string reason){LastReason=reason;Workspace?.InlineNotice(reason);return false;}
        public string OperationalTeamLabel(string id)=>Find(id)?.Definition.label??"대응팀";
        public string AgencyLabel(string id)=>AgencyById(id)?.label??"기관 미확인";
        public bool GetTeamAvailability(string channel)=>channel=="ops-1"||leases.ContainsKey(channel);
        public bool CanCommandChannel(string channel,string operationId=null)=>channel=="ops-1"||(operationId!=null&&leases.TryGetValue(channel,out var owner)&&owner.Work?.Id==operationId);
        public bool MatchesOperationOwner(string channel,string operationId,string agencyId,string teamId)=>leases.TryGetValue(channel,out var m)&&m.Work?.Id==operationId&&m.Definition.agencyId==agencyId&&m.Definition.id==teamId;
        public string AvailabilityReason(string channel)=>channel=="ops-1"?"역무 현장 지시 가능":leases.ContainsKey(channel)?"기관 대응팀의 자동 현장 업무 수행 중":"기관에서 지원업무를 요청하세요 · 현장 실행팀은 자동 배정됩니다";
        private string MissionReason(Mission m)=>m.State=="Replenishing"?"재출동 준비 중 · "+(m.Turnaround?.RemainingSeconds??0).ToString("0.0")+"초 후 지원 가능":m.State=="HandoffPending"?"의료 요청 접수 확인 · 실제 환자 회복·이송·인계 판정 없음":m.State=="Standby"?"현장 실행팀 사용 중 · 지원 대기":m.State=="WaitingIncident"?"사건 전 사전 배치 · 사건 발생 후 업무 수행":m.State=="AwaitingOutcome"?"실제 시민 대피 결과 대기":m.State=="Ready"?"기관에서 다음 업무 대기":m.State=="Unavailable"?"지원 경로 또는 표현 자료 없음":"기관 대응팀이 맡은 업무를 수행합니다";
        public bool RequestWork(string agencyId,string workType,string targetId,string requestId=null,bool reinforcement=false)
        {
            Initialize();var agency=AgencyById(agencyId);var task=Task(workType);if(agency==null||!agency.coordinateReady||!agency.dispatchSupported)return Reject("이 기관은 위치만 제공하며 지원업무는 아직 제공하지 않습니다");
            if(task==null||Target(targetId)==null)return Reject("지원하지 않는 업무 또는 대상입니다 · 부산역 참조 대합실만 지정할 수 있습니다");
            if(Director==null||!Director.CanPlanAgencyOrders)return Reject("훈련 시작 후 계산 준비를 기다리세요");
            if(Progression==null||!Progression.Initialize())return Reject(Progression?.StatusReason??"운영 흐름 자료 없음 · 업무 배정 불가");
            string fingerprint=agencyId+"|"+workType+"|"+targetId;
            if(requestId!=null&&requestIds.TryGetValue(requestId,out var previous)){if(requestFingerprints[requestId]!=fingerprint)return Reject("같은 요청 번호에 다른 업무를 사용할 수 없습니다");LastOperationId=previous;if(canceledOperations.Contains(previous))return Reject("취소된 업무 요청입니다 · 새 요청으로 다시 지정하세요");LastReason="이미 접수된 업무입니다";return true;}
            if(!reinforcement)foreach(var m in missions)
            {
                if(m.Definition.agencyId!=agencyId)continue;
                if(m.Work!=null&&m.Work.Type==workType&&m.Work.Target==targetId){LastOperationId=m.Work.Id;RememberRequest(requestId,m.Work.Id,fingerprint);LastReason="같은 업무가 이미 진행 중입니다 · 추가 지원은 별도로 요청하세요";return true;}
                foreach(var w in m.Queue)if(w.Type==workType&&w.Target==targetId){LastOperationId=w.Id;RememberRequest(requestId,w.Id,fingerprint);LastReason="이미 예약된 업무입니다";return true;}
            }
            Mission chosen=null;foreach(var m in missions)if(m.Definition.agencyId==agencyId&&m.State=="Ready"&&Compatible(m,workType)){chosen=m;break;}
            // This fixture exposes only one target. Repeating the same job on a busy group is not meaningful.
            if(chosen==null)return Reject("호환 가능한 가용 대응팀이 없습니다 · 진행 중인 업무를 확인하세요");
            var work=new Work{Id=Guid.NewGuid().ToString("N"),Type=workType,Target=targetId,RequestId=requestId??Guid.NewGuid().ToString("N")};
            if(FacilityResources==null)return Reject("기관 지원 꾸러미 자료 없음 · 업무 배정 불가");
            if(!FacilityResources.TryReserve(work.Id,agencyId,chosen.Definition.id,workType,out var resourceReason))return Reject(resourceReason);
            chosen.Queue.Enqueue(work);
            if(!StartNext(chosen)){bool refunded=FacilityResources.RollbackRejectedReservation(work.Id,out var rollbackReason);chosen.Queue.Clear();if(refunded)chosen.Work=null;return Reject(refunded?"업무 접수 실패 · 지원 꾸러미 예약을 되돌렸습니다":"업무 접수 실패 · "+rollbackReason);}
            RememberRequest(requestId,work.Id,fingerprint);LastOperationId=work.Id;LastReason=null;SelectedOperationalTeamId=chosen.Definition.id;return true;
        }
        private void RememberRequest(string requestId,string operationId,string fingerprint){if(requestId!=null){requestIds[requestId]=operationId;requestFingerprints[requestId]=fingerprint;}}
        private void RescheduleDepartures(string key)
        {
            float next=Director.SimulatedSeconds;var waiting=new List<Mission>();
            foreach(var mission in missions)if(mission.DepartureKey==key)
            {
                if(mission.Departed)next=Mathf.Max(next,mission.ReservationEnd);
                else if(mission.State=="Requested"||mission.State=="Returning")waiting.Add(mission);
            }
            waiting.Sort((a,b)=>a.DepartureAt.CompareTo(b.DepartureAt));
            foreach(var mission in waiting){mission.DepartureAt=next;mission.ReservationEnd=next+data.departureHeadwaySeconds*mission.Members.Count;next=mission.ReservationEnd;}
            departures[key]=next;
        }
        private bool StartNext(Mission m)
        {
            if(m.Queue.Count==0)return false;m.Work=m.Queue.Dequeue();m.Distance=0;m.OperationStartedSim=Director.SimulatedSeconds;m.OnsiteAt=-1;m.OutboundMetres=0;m.TurnaroundPrepared=false;m.Turnaround=null;m.TurnaroundTotalSeconds=0;m.TimingSource=null;if(Progression==null||!Progression.RegisterOperation(m.Work.Id,m.Definition.agencyId,m.Definition.id,Director.Physics.LastResult,Director.SimulatedSeconds,m.Work.RequestId)){m.State="Blocked";LastReason="운영 흐름을 시작할 수 없습니다";return false;}m.Stage="travel";Log(m,"업무 접수 · "+Task(m.Work.Type).label+" → "+Target(m.Work.Target).label);BeginRoute(m,false);return m.State=="Requested";
        }
        private void BeginRoute(Mission m,bool returning)
        {
            m.Route=FindRoute(m.Definition.agencyId,returning?"return":"outbound");if(m.Route==null){m.State="Unavailable";Log(m,"연결된 도로 경로 없음 · 이동 불가");return;}
            m.State=returning?"Returning":"Requested";m.Distance=0;m.Span=0;m.Departed=false;
            string key=returning?"station-return":m.Definition.agencyId;float now=Director.SimulatedSeconds;departures.TryGetValue(key,out float previous);m.DepartureAt=Mathf.Max(now,previous);m.DepartureKey=key;m.ReservationEnd=m.DepartureAt+data.departureHeadwaySeconds*m.Members.Count;departures[key]=m.ReservationEnd;
            for(int i=0;i<m.Members.Count;i++){var member=m.Members[i];member.From=member.Body.localPosition;member.To=returning?member.BasePark:member.SitePark;member.PrefixPath=returning?new[]{member.From,m.Route.points[0]}:member.BasePath;member.SuffixPath=returning?Reverse(member.BasePath):new[]{m.Route.points[m.Route.points.Length-1],member.To};member.Prefix=PathLength(member.PrefixPath);member.Suffix=PathLength(member.SuffixPath);m.Span=Mathf.Max(m.Span,i*data.memberHeadwayMeters+member.Prefix+m.Route.distanceMeters+member.Suffix);}
            Log(m,returning?"자동 기관 복귀 예약":"도로 출동 예약 · 순차 출발");ApplyTeamVisibility();
        }
        public void AdvanceAcceptedTime(float seconds)
        {
            Initialize();if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds)||Director==null||data==null)return;float end=Director.SimulatedSeconds,start=end-seconds;
            // Timers that existed at the start consume this accepted step. New arrivals do not.
            var timedThisStep=new HashSet<Mission>();
            foreach(var m in missions)if(m.State=="Replenishing")
            {
                timedThisStep.Add(m);
                if(m.Turnaround==null){m.State="Blocked";continue;}
                if(!m.Turnaround.AdvanceAcceptedSeconds(seconds))continue;
                if(Progression==null||!Progression.ObservePrimitive(m.Work.Id,"replenished",Director.Physics.LastResult,end)){m.State="Blocked";continue;}
                if(FacilityResources==null||!FacilityResources.CompleteReplenishment(m.Work.Id,out var resourceReason)){m.State="Blocked";LastReason="지원 꾸러미 보충 확인 필요";Log(m,LastReason);continue;}
                Log(m,"지원 꾸러미 보충 완료 · 재출동 준비 완료");m.State="Ready";m.Stage="";m.LastCompletedOperationId=m.Work.Id;m.Work=null;m.Route=null;
                if(m.Queue.Count>0)StartNext(m);
            }
            foreach(var m in missions)
            {
                if(timedThisStep.Contains(m)||(m.State!="Requested"&&m.State!="EnRoute"&&m.State!="Returning"))continue;
                float accepted=Mathf.Max(0,end-Mathf.Max(start,m.DepartureAt));if(accepted<=0)continue;
                if(!m.Departed){if(m.State=="Requested"&&!Progression.ObservePrimitive(m.Work.Id,"depart",Director.Physics.LastResult,end)){m.State="Blocked";continue;}m.Departed=true;if(m.State=="Requested")m.State="EnRoute";Log(m,m.State=="Returning"?"기관 복귀 출발":"대응팀 출동 · 구성 차량 공동 업무");}
                m.Distance=Mathf.Min(m.Span,m.Distance+accepted*data.speedMetersPerSecond);
                for(int i=0;i<m.Members.Count;i++)PlaceMember(m,m.Members[i],Mathf.Max(0,m.Distance-i*data.memberHeadwayMeters));
                if(m.Distance+.001f<m.Span)continue;
                if(m.State=="Returning"){if(Progression==null||!Progression.ObservePrimitive(m.Work.Id,"returned",Director.Physics.LastResult,end)){m.State="Blocked";continue;}var timing=GetComponent<MvpJevOperationalKernel>()?.ResolveTurnaround(m.Work.Id);float duration=timing?.ExpectedSeconds??15f;
                    bool valid=!float.IsNaN(duration)&&!float.IsInfinity(duration)&&duration>=15f&&duration<=45f;
                    if(!valid)duration=15f;m.TimingSource=valid&&timing!=null&&!string.IsNullOrEmpty(timing.Source)?timing.Source:"baseline";m.TurnaroundTotalSeconds=duration;m.Turnaround=new ThirdParty.OpenRaCountdown(duration);
                    m.State="Replenishing";m.Stage="replenishing";m.Route=null;Log(m,"기관 복귀 완료 · 재출동 준비 시작");}
                else {if(!Progression.ObservePrimitive(m.Work.Id,"arrive",Director.Physics.LastResult,end)){m.State="Blocked";continue;}m.State="WaitingIncident";m.Stage="onsite";m.OnsiteAt=end;m.OutboundMetres=m.Distance;Log(m,"현장 도착 · 실행팀 배정 대기");}
            }
        }
        private static Vector3[] Reverse(Vector3[] path){var copy=(Vector3[])path.Clone();Array.Reverse(copy);return copy;}
        private static float PathLength(Vector3[] path){float length=0;for(int i=1;i<path.Length;i++)length+=Vector3.Distance(path[i-1],path[i]);return length;}
        private static Vector3 Sample(Vector3[] path,float distance)
        {
            for(int i=1;i<path.Length;i++){float length=Vector3.Distance(path[i-1],path[i]);if(distance<=length)return Vector3.Lerp(path[i-1],path[i],length>0?distance/length:1);distance-=length;}return path[path.Length-1];
        }
        private void PlaceMember(Mission m,Member member,float distance)
        {
            Vector3 p;if(distance<member.Prefix)p=Sample(member.PrefixPath,distance);
            else if(distance<member.Prefix+m.Route.distanceMeters)p=Sample(m.Route.points,distance-member.Prefix);
            else p=Sample(member.SuffixPath,Mathf.Max(0,distance-member.Prefix-m.Route.distanceMeters));
            var direction=p-member.Body.localPosition;if(new Vector2(direction.x,direction.z).sqrMagnitude>.0001f)member.Body.localRotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.z));member.Body.localPosition=p;
            if(distance>=member.Prefix+m.Route.distanceMeters+member.Suffix-.001f)member.Body.localRotation=Quaternion.LookRotation(m.State=="Returning"?member.BaseForward:Vector3.forward);
        }
        private RuleFacts OperationFacts(Mission m,MvpPhysicsResult result,bool confirmation=false,bool withdrawal=false,bool cancellation=false)
        {
            var facts=new Dictionary<string,RuleTruth>();var task=Task(m.Work.Type);
            facts["evacuation-task"]=task.automaticReturn?RuleTruth.TRUE:RuleTruth.FALSE;facts["medical-task"]=task.id=="medical-support"?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["channel-free"]=leases.ContainsKey(task.channel)?RuleTruth.FALSE:RuleTruth.TRUE;
            facts["own-warn-ack"]=Director.TeamTasks.IsOperationActionComplete(m.Work.Id,"warn")?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["own-evacuate-ack"]=Director.TeamTasks.IsOperationActionComplete(m.Work.Id,"evacuate")?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["own-medical-ack"]=Director.TeamTasks.IsOperationActionComplete(m.Work.Id,"medical")?RuleTruth.TRUE:RuleTruth.FALSE;
            facts["manual-confirm"]=confirmation?RuleTruth.TRUE:RuleTruth.FALSE;facts["can-withdraw"]=withdrawal?RuleTruth.TRUE:RuleTruth.FALSE;facts["can-cancel"]=cancellation?RuleTruth.TRUE:RuleTruth.FALSE;
            if(result!=null){facts["response-phase"]=result.phase=="incident"||result.phase=="resolved"?RuleTruth.TRUE:RuleTruth.FALSE;facts["all-evacuated"]=result.phase=="resolved"||(result.phase!="ordinary"&&result.allEvacuated)?RuleTruth.TRUE:RuleTruth.FALSE;facts["calculation-current"]=result.physicsReady&&!Director.Physics.Failed&&(result.phase=="ordinary"||result.phase=="incident"||result.phase=="resolved")?RuleTruth.TRUE:RuleTruth.FALSE;}
            return MvpProgressionGraph.FactsFrom(facts);
        }
        public void ObserveAcceptedResult(MvpPhysicsResult result)
        {
            if(result==null||Director?.TeamTasks==null)return;
            foreach(var m in missions)
            {
                if(m.Work==null||m.State=="Ready"||m.State=="Requested"||m.State=="EnRoute"||m.State=="Returning"||m.State=="Replenishing"||m.State=="Unavailable"||m.State=="HandoffPending")continue;
                if(Progression==null||!Progression.Ready){m.State="Blocked";LastReason=Progression?.StatusReason??"운영 흐름 자료 없음";continue;}
                for(int transitions=0;transitions<4;transitions++)
                {
                    var decision=Progression.Evaluate(m.Work.Id,"result",OperationFacts(m,result),result);if(decision==null)break;
                    if(!ApplyProgressionEffect(m,decision.Effect)){Progression.MarkOperationUnavailable(m.Work.Id,"선택된 업무 단계를 실행할 수 없습니다",result,Director.SimulatedSeconds);break;}
                    if(!Progression.Commit(decision,result,Director.SimulatedSeconds,"실제 업무 단계 적용 · "+KoreanState(m.State)+"\n현장 전체 관찰: 출구 도달 "+result.evacuated+"/"+result.total+"명"+(result.medicalAssistanceRequested?" · 의료 요청 접수됨":""))){m.State="Blocked";break;}
                }
            }
            ApplyTeamVisibility();
        }
        private bool ApplyProgressionEffect(Mission m,string effect)
        {
            switch(effect)
            {
                case "support-standby":m.State="Standby";return true;
                case "lease-warn":case "lease-medical":
                    string channel=Task(m.Work.Type).channel;if(leases.ContainsKey(channel))return false;
                    leases[channel]=m;m.Channel=channel;m.State="Working";m.Stage=effect=="lease-warn"?"warn":"medical";Log(m,"현장 실행팀 자동 배정 · "+Task(m.Work.Type).label);return Enqueue(m,m.Stage);
                case "enqueue-evacuate":m.Stage="evacuate";return Enqueue(m,m.Stage);
                case "await-outcome":m.State="AwaitingOutcome";m.Stage="outcome";Log(m,"현장 지시 접수 완료 · 실제 대피 결과 대기");return true;
                case "handoff-pending":m.State="HandoffPending";m.Stage="handoff-pending";Log(m,"의료 요청 접수 확인 · 인계 확인 대기 · 회복·이송 판정 없음");return true;
                case "return":if(m.Channel!=null&&Director.TeamTasks.HasActiveTask(m.Channel))return false;Log(m,"운영 조건 확인 · 기관 복귀");Return(m);return m.State=="Returning";
                case "none":return true;
                default:return false;
            }
        }
        private bool Enqueue(Mission m,string action)
        {
            if(Director.TeamTasks.EnqueueOperation(m.Channel,action,m.Work.Id,m.Definition.agencyId,m.Definition.id,action=="evacuate"?"all":null))return true;
            m.State="Blocked";Log(m,"현장 업무 접수 불가 · 진행 상태 확인 필요");return false;
        }
        private bool ApplyUserProgression(Mission m,string eventType,bool confirm=false,bool withdraw=false,bool cancel=false)
        {
            if(Progression==null||!Progression.Ready)return Reject("운영 흐름 사용 불가 · 업무 상태를 확인하세요");
            var result=Director.Physics.LastResult;var decision=Progression.Evaluate(m.Work.Id,eventType,OperationFacts(m,result,confirm,withdraw,cancel),result);
            if(decision==null)return Reject("현재 업무 단계에서 선택할 수 없는 동작입니다");
            if(!ApplyProgressionEffect(m,decision.Effect))return Reject("선택한 업무를 적용할 수 없습니다");
            return Progression.Commit(decision,result,Director.SimulatedSeconds,"사용자 선택 접수 · "+decision.Label);
        }
        private void ReleaseLease(Mission m){if(m.Channel!=null&&leases.TryGetValue(m.Channel,out var owner)&&owner==m){Director?.TeamTasks?.ClearCompletedOperation(m.Channel,m.Work?.Id);leases.Remove(m.Channel);}m.Channel=null;}
        private void Return(Mission m)
        {
            if(!m.TurnaroundPrepared){m.TurnaroundPrepared=true;GetComponent<MvpJevOperationalKernel>()?.PrepareTurnaround(m.Work.Id,m.Definition.agencyId,m.Definition.id,m.Work.Type,m.Members.Count,m.OperationStartedSim,m.OnsiteAt>=0?Mathf.Max(0,Director.SimulatedSeconds-m.OnsiteAt):0,m.OutboundMetres);}
            ReleaseLease(m);m.Stage="return";BeginRoute(m,true);
        }
        public bool RequestReturn(string teamId)
        {
            var m=Find(teamId);if(m==null)return Reject("알 수 없는 대응팀입니다");
            if(m.Work==null||m.State=="Ready"||m.State=="Replenishing"||m.State=="Returning"||m.State=="EnRoute"||m.State=="Requested")return Reject("현장 대기 중인 대응팀만 복귀할 수 있습니다");
            if(m.State=="Working"||m.State=="AwaitingOutcome"||(m.Channel!=null&&Director.TeamTasks.HasActiveTask(m.Channel)))return Reject("진행 중인 현장 업무 접수를 먼저 완료해야 합니다");
            if(m.State=="HandoffPending")return Reject("의료 요청 이후 상태를 확인하고 업무 종료를 확인하세요");
            if(Director==null||!Director.CanPlanAgencyOrders)return Reject("훈련 계산 준비를 기다리세요");return ApplyUserProgression(m,"withdraw",withdraw:true);
        }
        public bool ConfirmMedicalHandoffAndReturn(string teamId)
        {
            var m=Find(teamId);if(m==null||m.State!="HandoffPending"||m.Work?.Type!="medical-support")return Reject("의료 요청 접수 확인 후에만 업무 종료를 확인할 수 있습니다");
            if(Director==null||!Director.CanPlanAgencyOrders)return Reject("훈련 계산 준비를 기다리세요");return ApplyUserProgression(m,"confirm",confirm:true);
        }
        public bool CancelOperation(string teamId)
        {
            var m=Find(teamId);if(m==null)return Reject("알 수 없는 대응팀입니다");
            if(m.State=="Requested"&&!m.Departed){if(FacilityResources==null||!FacilityResources.CanRefundBeforeDeparture(m.Work.Id,out var resourceReason))return Reject("출발 전 지원 꾸러미 예약 확인 필요");if(!ApplyUserProgression(m,"cancel",cancel:true))return false;if(!FacilityResources.RefundBeforeDeparture(m.Work.Id,out resourceReason)){m.State="Blocked";return Reject(resourceReason);}Log(m,"출발 전 업무 취소 · 지원 꾸러미 환급");if(m.Work!=null)canceledOperations.Add(m.Work.Id);string key=m.DepartureKey;m.Queue.Clear();m.Work=null;m.State="Ready";m.Stage="";m.Route=null;m.DepartureKey=null;m.ReservationEnd=0;RescheduleDepartures(key);return true;}
            return Reject("이미 출발했거나 현장 업무 중입니다 · 현장 도착 후 지원 종료를 요청하세요");
        }
        public void ResetMissions()
        {
            Initialize();Progression?.ResetRun();GetComponent<MvpJevOperationalKernel>()?.ResetRun();FacilityResources?.ResetRun();leases.Clear();requestIds.Clear();requestFingerprints.Clear();canceledOperations.Clear();departures.Clear();CancelTargetSelection();LastOperationId=null;SelectedOperationalTeamId=null;
            foreach(var m in missions){m.Turnaround?.Cancel();m.Turnaround=null;m.TurnaroundTotalSeconds=0;m.TimingSource=null;m.TurnaroundPrepared=false;m.OperationStartedSim=0;m.OnsiteAt=-1;m.OutboundMetres=0;m.Queue.Clear();m.Work=null;m.Channel=null;m.LastCompletedOperationId=null;m.Stage="";m.Route=null;m.Distance=0;m.Span=0;m.Departed=false;m.DepartureKey=null;m.ReservationEnd=0;m.State=m.Members.Count==m.Definition.memberCount&&RoutesReady(m.Definition.agencyId)?"Ready":"Unavailable";foreach(var member in m.Members){member.Body.localPosition=member.BasePark;member.Body.localRotation=Quaternion.LookRotation(member.BaseForward);}}
            Station?.Navigation?.EndFollow();ApplyTeamVisibility();
        }
        private void LateUpdate()=>ApplyTeamVisibility();
        private void ApplyTeamVisibility(){if(Station?.Teams==null)return;for(int i=1;i<Mathf.Min(3,Station.Teams.Length);i++)if(Station.Teams[i]!=null&&!GetTeamAvailability(i==1?"fire-1":"medical-1"))Station.Teams[i].gameObject.SetActive(false);}
        private void Log(Mission m,string text)=>Director?.RecordAgencyEvent(AgencyLabel(m.Definition.agencyId)+" · "+m.Definition.label+" · "+text,m.Channel);
        public Transform Vehicle(string teamId)=>Find(teamId)?.Members.Count>0?Find(teamId).Members[0].Body:null;
        public bool SelectAgency(string id){Initialize();if(AgencyById(id)==null)return Reject("알 수 없는 기관입니다");if(HasPendingTarget&&pendingAgency!=id)CancelTargetSelection();SelectedAgencyId=id;SelectedOperationalTeamId=null;ContextKind="agency";LastReason=null;Workspace?.ApplyAgencyContext();return true;}
        public bool SelectOperationalTeam(string id){var m=Find(id);if(m==null)return Reject("알 수 없는 대응팀입니다");SelectedAgencyId=m.Definition.agencyId;SelectedOperationalTeamId=id;ContextKind="team";Workspace?.ApplyAgencyContext();return true;}
        public void SelectStationContext(){CancelTargetSelection();SelectedAgencyId=null;SelectedOperationalTeamId=null;ContextKind="station";Workspace?.ApplyAgencyContext();}
        public void ClearContextSelection(){CancelTargetSelection();SelectedAgencyId=null;SelectedOperationalTeamId=null;ContextKind=null;Workspace?.ApplyAgencyContext();}
        public void FocusAgency(string agencyId){var a=AgencyById(agencyId);if(a!=null&&a.coordinateReady){Workspace.SelectFloor(0);Station.Navigation.Focus(a.point,100);}}
        public void FocusVehicle(string teamId){var v=Vehicle(teamId);if(v!=null){Workspace.SelectFloor(0);Station.Navigation.BeginFollow(v,80);}}
        public bool BeginTargetSelection(string agencyId,string workType,bool reinforcement=false)
        {
            Initialize();var a=AgencyById(agencyId);if(a==null||!a.dispatchSupported||Task(workType)==null)return Reject("이 기관에서 제공하지 않는 업무입니다");
            pendingAgency=agencyId;pendingWork=workType;pendingReinforcement=reinforcement;LastReason="부산역을 클릭해 업무 대상을 지정하세요 · 우클릭으로 취소";return true;
        }
        public void CancelTargetSelection(){pendingAgency=null;pendingWork=null;pendingReinforcement=false;LastReason=null;consumedFrame=Time.frameCount;}
        public bool TrySelectWorld(Ray worldRay)
        {
            Initialize();if(Station==null)return false;var ray=new Ray(Station.transform.InverseTransformPoint(worldRay.origin),Station.transform.InverseTransformDirection(worldRay.direction));
            if(HasPendingTarget)
            {
                foreach(var target in Targets)if(HitTargetVolume(worldRay,target.id)||(Workspace.CurrentFloor!=0&&HitReferenceHall(ray,target)))
                {string a=pendingAgency,w=pendingWork;bool reinforce=pendingReinforcement;if(RequestWork(a,w,target.id,null,reinforce)){pendingAgency=null;pendingWork=null;}consumedFrame=Time.frameCount;return true;}
                LastReason="지원 대상이 아닙니다 · 부산역 건물을 클릭하세요";consumedFrame=Time.frameCount;return true;
            }
            float nearest=float.PositiveInfinity;Mission picked=null;foreach(var m in missions)foreach(var member in m.Members){if(!member.Body.gameObject.activeInHierarchy)continue;var renderers=member.Body.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;Bounds b=renderers[0].bounds;foreach(var renderer in renderers)b.Encapsulate(renderer.bounds);b.Expand(2);if(b.IntersectRay(worldRay,out float d)&&d<nearest){nearest=d;picked=m;}}
            // Visible team members select their logical group before broad building pick volumes.
            if(picked!=null){SelectOperationalTeam(picked.Definition.id);consumedFrame=Time.frameCount;return true;}
            foreach(var lease in leases){int index=lease.Key=="fire-1"?1:2;if(Station.Teams==null||Station.Teams.Length<=index)continue;foreach(var renderer in Station.Teams[index].GetComponentsInChildren<Renderer>())if(renderer.gameObject.activeInHierarchy&&renderer.bounds.IntersectRay(worldRay)){SelectOperationalTeam(lease.Value.Definition.id);consumedFrame=Time.frameCount;return true;}}
            PickVolume selectedFacility=null;foreach(var volume in pickVolumes)if(volume.Collider.Raycast(worldRay,out var hit,10000)&&hit.distance<nearest){nearest=hit.distance;selectedFacility=volume;}
            if(selectedFacility!=null){if(selectedFacility.AgencyId!=null)SelectAgency(selectedFacility.AgencyId);else if(selectedFacility.TargetId==StationTarget)SelectStationContext();consumedFrame=Time.frameCount;return true;}
            if(Workspace.CurrentFloor!=0)foreach(var target in Targets)if(HitReferenceHall(ray,target)){SelectStationContext();consumedFrame=Time.frameCount;return true;}
            ClearContextSelection();return false;
        }
        private static bool HitReferenceHall(Ray ray,TargetDefinition target){var bounds=new Bounds(target.point,new Vector3(30,10,20));return bounds.IntersectRay(ray);}
        private bool HitTargetVolume(Ray ray,string targetId){float closest=float.PositiveInfinity;string selected=null;foreach(var volume in pickVolumes)if(volume.Collider.Raycast(ray,out var hit,10000)&&hit.distance<closest){closest=hit.distance;selected=volume.TargetId;}return selected==targetId;}
        public bool ProcessWorldInput()
        {
            if(consumedFrame==Time.frameCount)return true;var mouse=Mouse.current;if(mouse==null)return HasPendingTarget;
            if(swallowRightRelease){if(mouse.rightButton.wasReleasedThisFrame||!mouse.rightButton.isPressed)swallowRightRelease=false;return true;}
            if(HasPendingTarget&&mouse.rightButton.wasPressedThisFrame){swallowRightRelease=true;CancelTargetSelection();return true;}
            if(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return HasPendingTarget;
            if(mouse.leftButton.wasPressedThisFrame&&Station?.ViewCamera!=null)return TrySelectWorld(Station.ViewCamera.ScreenPointToRay(mouse.position.ReadValue()))||HasPendingTarget;
            return HasPendingTarget;
        }
        public static string KoreanState(string state)=>state=="Ready"?"기관 대기":state=="Requested"?"출발 대기":state=="EnRoute"?"현장 이동":state=="WaitingIncident"?"사건 발생 대기":state=="Standby"?"현장 지원 대기":state=="Working"?"현장 업무":state=="AwaitingOutcome"?"대피 결과 대기":state=="HandoffPending"?"인계 확인 대기":state=="Returning"?"기관 복귀":state=="Replenishing"?"재출동 준비 중":state=="Blocked"?"업무 확인 필요":"출동 불가";
        // Old role-based calls cannot silently select an unrelated/medical team.
        public bool RequestSupport(string agencyId,string teamId)=>Reject("시설에서 지원업무를 선택하고 대상을 지정하세요");
    }
}
