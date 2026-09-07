using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChooGuard.Foundation.Demo
{
    [Serializable] public sealed class StationSite
    {
        public string id; public string label; public Vector3 portal; public Vector3 cue; public float width;
        public string physicalReference; public string verification;
    }
    [Serializable] public sealed class StationEquipmentBinding
    {
        public string entityId; public string anchorId; public string kind; public Vector3 position;
        public string physicalReference; public string manualReference; public string verification;
    }
    [Serializable] public sealed class StationWorldConfig
    {
        public string version; public string evidenceStatus; public string units; public string coordinateFrame;
        public string facilityReference; public string manualReference;
        public float minimumQuietSeconds; public float maximumQuietSeconds; public float recoverySeconds; public float escalationSeconds;
        public StationSite[] sites; public StationEquipmentBinding[] equipment; public Vector3[] routineStops;
        public void Validate()
        {
            if(new[]{minimumQuietSeconds,maximumQuietSeconds,recoverySeconds,escalationSeconds}.Any(x=>float.IsNaN(x)||float.IsInfinity(x)))throw new ArgumentException("Timing must be finite.");
            if(string.IsNullOrWhiteSpace(version)||units!="metres"||coordinateFrame!="Unity +Y up +Z forward"||evidenceStatus!="synthetic-unverified"||
                minimumQuietSeconds<=0||maximumQuietSeconds<minimumQuietSeconds||recoverySeconds<=0||escalationSeconds<=0||
                sites==null||sites.Length!=3||sites.Any(x=>x==null||string.IsNullOrWhiteSpace(x.id)||x.width<=0||x.verification!="synthetic")||
                sites.Select(x=>x.id).Distinct().Count()!=3)
                throw new ArgumentException("A versioned synthetic profile with three distinct sites and valid timing is required. Verified twin use requires a separate reviewed evidence profile.");
        }
        public static StationWorldConfig TestProfile()
        {
            return new StationWorldConfig {version="test",evidenceStatus="synthetic-unverified",units="metres",coordinateFrame="Unity +Y up +Z forward",minimumQuietSeconds=5,maximumQuietSeconds=10,recoverySeconds=2,escalationSeconds=40,
                sites=new[]{new StationSite{id="central",width=6,verification="synthetic"},new StationSite{id="west",width=6,verification="synthetic"},new StationSite{id="east",width=6,verification="synthetic"}}};
        }
    }
    public enum StationPhase { Ordinary, Incident, Recovery }
    public enum StationIncidentKind { PassageObstruction, GuidanceOutage }
    public enum StationAction { Observe, Report, Notify, SelectRoute, Recruit, RecordArrival, CloseIncident }
    public sealed class IncidentCandidate
    {
        public string SiteId { get; private set; } public string[] AffectedNpcIds { get; private set; }
        public IncidentCandidate(string site,string[] people){SiteId=site;AffectedNpcIds=people.ToArray();}
    }
    [Serializable] public sealed class StationTrace
    {
        public double seconds; public int revision; public string incidentId; public string action; public string target; public bool accepted; public string detail;
    }
    [Serializable] public sealed class StationDebrief
    {
        public string incidentId; public string kind; public string site; public double startedAt; public double observedAt; public double reportedAt; public double completedAt;
        public string selectedRoute; public int affected; public int arrived; public bool escalated; public bool roleActionObserved;
    }
    public sealed class StationIncident
    {
        public string Id {get;internal set;} public StationIncidentKind Kind {get;internal set;} public string SiteId {get;internal set;}
        public double StartedAt {get;internal set;} public double EscalatesAt {get;internal set;} public double ObservedAt {get;internal set;}=-1; public double ReportedAt {get;internal set;}=-1;
        public bool Escalated {get;internal set;} public bool Notified {get;internal set;} public string SelectedRoute {get;internal set;}
        public IReadOnlyList<string> AffectedNpcIds {get;internal set;}
        internal readonly HashSet<string> recruited=new HashSet<string>(); internal readonly HashSet<string> arrived=new HashSet<string>();
        public bool Discovered {get{return ObservedAt>=0;}} public bool Reported {get{return ReportedAt>=0;}}
        public bool RequiresNotice {get{return Kind==StationIncidentKind.GuidanceOutage||Escalated;}}
        public string ReportAnchor {get{return RequiresNotice?"route-console":"anchor-03";}}
        public int ArrivedCount {get{return arrived.Count;}} public bool IsRecruited(string id){return recruited.Contains(id);}
        public bool CanLead {get{return Reported&&SelectedRoute!=null&&(!RequiresNotice||Notified);}}
    }
    public struct StationReceipt
    {
        public bool Accepted; public string Reason;
        public StationReceipt(bool accepted,string reason){Accepted=accepted;Reason=reason;}
    }
    // Synthetic exercise state, not a railway SOP, real failure probability model or competency score.
    public sealed class StationWorldSession
    {
        private readonly string[] siteIds; private readonly double minimumQuiet,maximumQuiet,recoverySeconds,escalationSeconds;
        private uint random; private int serial; private double nextOnset; private double recoverAt; private string lastCombination;
        private readonly List<StationTrace> trace=new List<StationTrace>(); private readonly List<StationDebrief> history=new List<StationDebrief>();
        public int Seed {get;private set;} public double Seconds {get;private set;} public int Revision {get;private set;}
        public bool Paused {get;set;} public StationPhase Phase {get;private set;} public StationIncident Active {get;private set;}
        public IReadOnlyList<StationTrace> Trace {get{return trace.AsReadOnly();}} public IReadOnlyList<StationDebrief> History {get{return history.AsReadOnly();}}
        public StationWorldSession(StationWorldConfig config,int seed)
        {
            config.Validate();Seed=seed;random=unchecked((uint)seed);
            // Avalanche small neighboring seeds before xorshift so early onset is not correlated with seed magnitude.
            unchecked {random^=random>>16;random*=0x7FEB352D;random^=random>>15;random*=0x846CA68B;random^=random>>16;}
            if(random==0)random=0xA341316C;
            siteIds=config.sites.Select(x=>x.id).ToArray();minimumQuiet=config.minimumQuietSeconds;maximumQuiet=config.maximumQuietSeconds;recoverySeconds=config.recoverySeconds;escalationSeconds=config.escalationSeconds;
            nextOnset=Interval();Phase=StationPhase.Ordinary;
        }
        private uint Next(){random^=random<<13;random^=random>>17;random^=random<<5;return random;}
        private double Interval(){return minimumQuiet+(maximumQuiet-minimumQuiet)*(Next()/(double)uint.MaxValue);}
        public void Tick(double dt,IncidentCandidate[] candidates)
        {
            if(double.IsNaN(dt)||double.IsInfinity(dt)||dt<0)throw new ArgumentException("Finite positive simulation delta required.");
            if(candidates==null||candidates.Any(x=>x==null||!siteIds.Contains(x.SiteId)||x.AffectedNpcIds.Length==0||x.AffectedNpcIds.Distinct().Count()!=x.AffectedNpcIds.Length||x.AffectedNpcIds.Any(id=>!Enumerable.Range(0,6).Select(i=>"npc-"+i).Contains(id))))throw new ArgumentException("Invalid incident candidates.");
            if(Paused||dt==0)return;Seconds+=dt;
            if(Phase==StationPhase.Recovery&&Seconds>=recoverAt)
            {Phase=StationPhase.Ordinary;Active=null;Revision++;nextOnset=recoverAt+Interval();Record("recovered","station",true,"ordinary operation restored",recoverAt);}
            if(Phase==StationPhase.Ordinary&&Seconds>=nextOnset&&candidates.Length>0)
            {
                var available=candidates.OrderBy(x=>x.SiteId,StringComparer.Ordinal).ToArray();
                var selected=available[(int)(Next()%(uint)available.Length)];var kind=(StationIncidentKind)(Next()%2);
                if(selected.SiteId+kind==lastCombination)kind=kind==StationIncidentKind.PassageObstruction?StationIncidentKind.GuidanceOutage:StationIncidentKind.PassageObstruction;
                lastCombination=selected.SiteId+kind;
                // Onset deferred by occupancy is timestamped when a site first becomes eligible.
                var at=Seconds-dt>nextOnset?Seconds:nextOnset;
                Active=new StationIncident{Id="incident-"+(++serial).ToString("000"),Kind=kind,SiteId=selected.SiteId,StartedAt=at,EscalatesAt=at+escalationSeconds+(Next()%21),AffectedNpcIds=Array.AsReadOnly(selected.AffectedNpcIds.ToArray())};
                Phase=StationPhase.Incident;Revision++;Record("onset",selected.SiteId,true,kind.ToString(),at);
            }
            if(Phase==StationPhase.Incident&&!Active.Escalated&&!Active.Reported&&Seconds>=Active.EscalatesAt)
            {Active.Escalated=true;Revision++;Record("escalated",Active.SiteId,true,"secondary communication outage inject after unreported indication; not physical hazard propagation",Active.EscalatesAt);}
        }
        public StationReceipt Act(string incidentId,int expectedRevision,StationAction action,string target)
        {
            string reason=null;
            if(Paused)reason="일시정지";
            else if(Phase!=StationPhase.Incident||Active==null||incidentId!=Active.Id)reason="현재 사건과 다른 입력";
            else if(expectedRevision!=Revision)reason="상태가 변경됨 · 다시 확인";
            else if(action!=StationAction.Observe&&!Active.Discovered)reason="먼저 현장 또는 상황 패널에서 상태 확인";
            else switch(action)
            {
                case StationAction.Observe:
                    if(Active.Discovered)reason="이미 관측한 상태";
                    else if(target!="anchor-01"&&target!="signal-"+Active.SiteId)reason="확인되지 않은 관측 지점";
                    else Active.ObservedAt=Seconds;break;
                case StationAction.Report:
                    if(target!=Active.ReportAnchor)reason="이 통신 경로에서는 보고할 수 없음";
                    else if(Active.Reported)reason="보고 기록 있음";
                    else Active.ReportedAt=Seconds;break;
                case StationAction.Notify:
                    if(target!="anchor-02")reason="안내 전파 장치가 아님";
                    else if(Active.Notified)reason="안내 전파 기록 있음";
                    else Active.Notified=true;break;
                case StationAction.SelectRoute:
                    if(!siteIds.Contains(target)||target==Active.SiteId)reason="현재 사용할 수 없는 경로";
                    else if(Active.recruited.Count>0)reason="인솔 시작 후 경로 유지";
                    else if(target==Active.SelectedRoute)reason="선택된 경로";
                    else Active.SelectedRoute=target;break;
                case StationAction.Recruit:
                    if(!Active.CanLead)reason="보고·사용 가능 경로·필요한 안내 전파 확인";
                    else if(!Active.AffectedNpcIds.Contains(target))reason="현재 인솔 대상이 아님";
                    else if(!Active.recruited.Add(target))reason="이미 인솔 중";break;
                case StationAction.RecordArrival:
                    if(!Active.recruited.Contains(target))reason="인솔하지 않은 인원";
                    else if(!Active.arrived.Add(target))reason="이미 집결 기록 있음";break;
                case StationAction.CloseIncident:
                    if(target!="assembly-register"||Active.arrived.Count!=Active.AffectedNpcIds.Count)reason="집결 확인 또는 도착 인원 미충족";
                    else
                    {
                        history.Add(new StationDebrief{incidentId=Active.Id,kind=Active.Kind.ToString(),site=Active.SiteId,startedAt=Active.StartedAt,observedAt=Active.ObservedAt,reportedAt=Active.ReportedAt,completedAt=Seconds,selectedRoute=Active.SelectedRoute,affected=Active.AffectedNpcIds.Count,arrived=Active.arrived.Count,escalated=Active.Escalated});
                        if(history.Count>20)history.RemoveAt(0);Phase=StationPhase.Recovery;recoverAt=Seconds+recoverySeconds;
                    }break;
                default:reason="지원하지 않는 입력";break;
            }
            if(reason==null)Revision++;
            Record(action.ToString(),target,reason==null,reason??"recorded");return new StationReceipt(reason==null,reason??"기록됨");
        }
        public void Record(string action,string target,bool accepted,string detail,double? eventSeconds=null)
        {
            trace.Add(new StationTrace{seconds=eventSeconds??Seconds,revision=Revision,incidentId=Active==null?"":Active.Id,action=action,target=target,accepted=accepted,detail=detail});
            if(trace.Count>1000)trace.RemoveAt(0);
        }
    }
}
