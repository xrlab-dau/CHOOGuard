using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ChooGuard.App.Mvp
{
    // A read-only observer. Neither the service nor its probabilities can issue orders.
    public sealed class MvpJevForecastClient : MonoBehaviour
    {
        [Serializable] public sealed class PhysicsSnapshot { public int total,evacuated; public float density,pressureIndicator,visibility,temperature,maxFedToxic,extinction,sootDensity,maxFedConvectiveHeat,incidentTime,fieldValidUntil; public bool fieldCurrent,fireRequired,warned,evacuationOrdered,medicalAssistanceRequested; }
        [Serializable] public sealed class Mission { public string teamId,operationId,workType,state,stage; public float distanceTravelled,routeDistance; public int memberCount; }
        [Serializable] public sealed class Request { public int schemaVersion=1; public string snapshotId,stateHash,decisionKey,runId; public int generation; public float simTime; public string graphHash,phase,selectedOperationId; public PhysicsSnapshot physics; public MvpCohortStats[] cohorts; public int releaseRevision; public string lastControlRequestId,cohortScheme,releasePolicyHistoryDigest; public Mission[] missions; public string decisionSummary; public bool calculationCurrent; }
        [Serializable] public sealed class Forecast { public string id; public int horizonSeconds; public float probability; }
        [Serializable] public sealed class Response { public int schemaVersion; public string snapshotId,stateHash,decisionKey,runId; public int generation; public float basisSimTime; public string status,model; public float latencyMs; public Forecast[] forecasts; public string reasonCode; }
        [Serializable] public sealed class Comparison { public string snapshotId,decisionKey,runId,eventId,status; public int generation,horizonSeconds; public float probability,basisSimTime,originDensity,observedSimTime,brier; public int outcome=-1; public string[] arrivalOperations; }
        public MvpAgencyDispatchController Controller;
        public Response Current { get; private set; }
        public string Status { get; private set; }="예측 준비 중";
        public int Revision { get; private set; }
        public int ScoredCount { get; private set; }
        public float BrierSum { get; private set; }
        public int UnscoredCount { get; private set; }
        public IReadOnlyList<Comparison> Comparisons=>comparisons.AsReadOnly();
        private readonly List<Comparison> comparisons=new List<Comparison>();
        private readonly HashSet<string> observedControlIds=new HashSet<string>();
        private readonly SortedSet<string> choices=new SortedSet<string>(StringComparer.Ordinal);
        private HttpClient http;
        private Task<string> flight;
        private Request sent,pending,currentState;
        private MvpPhysicsBridge physics;
        private MvpProgressionGraph graph;
        private MvpPhysicsResult inspectedResult;
        private readonly List<Request> observations=new List<Request>();
        private bool pendingUrgent;
        private string runKey,conditionKey,lastFingerprint,lastUrgent,lastSentHash;
        private float inspectAt,nextSendAt,pendingSince,lastSendAt=-100;
        private bool invalidated;
        private int conditionEpoch;

        private void Start()
        {
            if(Controller==null)Controller=GetComponent<MvpAgencyDispatchController>();
            if(Controller==null)return;
            physics=Controller.Workspace.GetComponent<MvpPhysicsBridge>();graph=Controller.Workspace.GetComponent<MvpProgressionGraph>();
            http=new HttpClient(new HttpClientHandler { UseProxy=false }) { Timeout=TimeSpan.FromSeconds(4) };
        }
        private void OnDestroy() { http?.Dispose(); }
        private void LateUpdate()
        {
            if(http==null)return;
            float now=Time.realtimeSinceStartup;
            if(now>=inspectAt || (physics!=null && physics.LastResult!=inspectedResult)) { inspectAt=now+.25f; inspectedResult=physics!=null?physics.LastResult:null; Inspect(now); }
            if(flight!=null && flight.IsCompleted)
            {
                Inspect(now);
                var done=flight;flight=null;
                if(done.IsCanceled||done.IsFaulted) Hide("예측 갱신 대기");
                else Accept(done.Result,sent);
            }
            if(flight==null && pending!=null && now>=nextSendAt && now-pendingSince>=.25f)
            {
                sent=pending;pending=null;pendingUrgent=false;lastSendAt=now;lastSentHash=sent.stateHash;
                flight=Send(JsonUtility.ToJson(sent));
            }
        }
        private async Task<string> Send(string json)
        {
            using(var body=new StringContent(json,Encoding.UTF8,"application/json"))
            using(var response=await http.PostAsync("http://127.0.0.1:18764/forecast",body).ConfigureAwait(false))
            { if(!response.IsSuccessStatusCode)return "";var text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);return text.Length<=32768?text:""; }
        }
        private void Inspect(float now)
        {
            var result=physics!=null?physics.LastResult:null;
            if(result==null || !physics.Ready || physics.Failed || result.kind!="RESULT" || !result.physicsReady || result.phase=="incomplete" || (result.fireRequired && result.phase=="incident" && !result.fieldCurrent))
            { Invalidate("현재 계산 확인 대기");return; }
            var key=result.runId+":"+result.generation;
            if(runKey!=key) { Invalidate("새 실행 예측 준비 중");choices.Clear();observedControlIds.Clear();observations.Clear();runKey=key;conditionKey=null;lastFingerprint=null;lastUrgent=null;lastSentHash=null; }
            var request=Capture(result);
            if(request==null) { Invalidate("현재 계산 확인 대기");return; }
            invalidated=false;
            if(conditionKey!=request.decisionKey)
            {
                foreach(var record in comparisons)if(record.status=="pending")Unscore(record,"changed_decision");
                conditionKey=request.decisionKey;Hide("선택 반영 중");
            }
            currentState=request;
            if(observations.Count>=512)observations.RemoveAt(0);observations.Add(request);
            Observe(request);
            if(Current!=null && Current.forecasts!=null)foreach(var forecast in Current.forecasts)if(request.simTime>=Current.basisSimTime+forecast.horizonSeconds) { Hide("예측 갱신 대기");break; }
            string urgent=request.decisionKey+"|"+request.phase+"|"+result.releaseRevision+"|"+(result.evacuated/5)+"|"+Mathf.FloorToInt(result.density*2);
            foreach(var m in request.missions)urgent+="|"+m.teamId+":"+m.state+":"+m.stage;
            string fingerprint=request.stateHash;
            if(fingerprint==lastFingerprint)return;
            bool meaningful=urgent!=lastUrgent;lastFingerprint=fingerprint;lastUrgent=urgent;
            // A pending request is replaced, never queued. Debounce starts at the first change;
            // later samples replace its content without starving a continuously running model.
            if(pending==null)pendingSince=now;
            pendingUrgent|=meaningful;pending=request;nextSendAt=Mathf.Max(now, lastSendAt+(pendingUrgent?2f:5f));
            if(fingerprint==lastSentHash)pending=null;
        }
        private Request Capture(MvpPhysicsResult r)
        {
            float sim=r.lifecycleVersion==1?r.sessionSimTime:r.simTime;
            if(string.IsNullOrEmpty(r.runId)||!Finite(sim)||!Finite(r.density)||!Finite(r.pressureIndicator)||!Finite(r.visibility)||!Finite(r.temperature)||!Finite(r.maxFedToxic)||!Finite(r.extinction)||!Finite(r.sootDensity)||!Finite(r.maxFedConvectiveHeat)||!Finite(r.incidentTime)||!Finite(r.fieldValidUntil)||r.total<=0||r.evacuated<0||r.evacuated>r.total)return null;
            if(r.cohortStats!=null)foreach(var cohort in r.cohortStats)
                if(cohort==null||!Finite(cohort.maxFedToxic)||!Finite(cohort.maxFedConvectiveHeat)||!Finite(cohort.meanFinalFedToxic)||!Finite(cohort.meanFinalFedConvectiveHeat)||!Finite(cohort.finalExitTime))return null;
            if(!string.IsNullOrEmpty(r.lastControlRequestId)&&observedControlIds.Add(r.lastControlRequestId))
            {
                bool owned=false;
                if(graph!=null)foreach(var trace in graph.TraceRecords)
                    if(trace.RunId==r.runId&&trace.Generation==r.generation&&trace.RequestId==r.lastControlRequestId&&!string.IsNullOrEmpty(trace.OperationId)&&trace.EdgeId==null&&trace.From==trace.To&&
                        (trace.Label=="경고 방송 접수 확인"||trace.Label=="대피 유도 접수 확인"||trace.Label=="의료 지원 요청 접수 확인")) { owned=true;break; }
                if(!owned)choices.Add("external-control:"+r.lastControlRequestId);
            }
            var missions=new List<Mission>();string selected="";
            if(!string.IsNullOrEmpty(Controller.LastOperationId))choices.Add("operation:"+Controller.LastOperationId);
            foreach(var m in Controller.MissionSnapshots)
            {
                if(!Finite(m.DistanceTravelled)||!Finite(m.RouteDistance))return null;
                missions.Add(new Mission{teamId=m.TeamId,operationId=m.OperationId??"",workType=m.WorkType??"",state=m.State,stage=m.Stage??"",distanceTravelled=m.DistanceTravelled,routeDistance=m.RouteDistance,memberCount=m.VisualMemberCount});
                if(m.TeamId==Controller.SelectedOperationalTeamId)selected=m.OperationId??m.LastCompletedOperationId??"";
                var operation=m.OperationId??m.LastCompletedOperationId;
                if(!string.IsNullOrEmpty(operation))
                {
                    choices.Add("operation:"+operation);

                }
            }
            var explicitChoices=new List<string>();
            foreach(var choice in choices)if(choice.StartsWith("operation:",StringComparison.Ordinal))
            {
                var view=graph?.Snapshot(choice.Substring(10));
                if(view?.Recent!=null)foreach(var trace in view.Recent)
                    if(trace.EdgeId!=null && (trace.EdgeId.IndexOf("cancel",StringComparison.Ordinal)>=0||trace.EdgeId.IndexOf("withdraw",StringComparison.Ordinal)>=0||trace.EdgeId.IndexOf("confirm",StringComparison.Ordinal)>=0))explicitChoices.Add("decision:"+trace.Sequence+":"+trace.EdgeId);
            }
            foreach(var choice in explicitChoices)choices.Add(choice);
            missions.Sort((a,b)=>string.CompareOrdinal(a.teamId,b.teamId));
            var conditional=new StringBuilder(runKey).Append('|').Append(conditionEpoch).Append('|').Append(graph?.GraphHash).Append('|').Append(r.phase=="ordinary"?"ordinary":"response");
            foreach(var choice in choices)conditional.Append('|').Append(choice);
            var request=new Request{snapshotId="",stateHash="",decisionKey=Hash(conditional.ToString()),runId=r.runId,generation=r.generation,simTime=sim,graphHash=graph?.GraphHash??"",phase=r.phase,selectedOperationId=selected,physics=new PhysicsSnapshot{total=r.total,evacuated=r.evacuated,density=r.density,pressureIndicator=r.pressureIndicator,visibility=r.visibility,temperature=r.temperature,maxFedToxic=r.maxFedToxic,extinction=r.extinction,sootDensity=r.sootDensity,maxFedConvectiveHeat=r.maxFedConvectiveHeat,incidentTime=r.incidentTime,fieldValidUntil=r.fieldValidUntil,fieldCurrent=r.fieldCurrent,fireRequired=r.fireRequired,warned=r.warned,evacuationOrdered=r.evacuationOrdered,medicalAssistanceRequested=r.medicalAssistanceRequested},cohorts=r.cohortStats??new MvpCohortStats[0],releaseRevision=r.releaseRevision,lastControlRequestId=r.lastControlRequestId??"",cohortScheme=r.cohortScheme??"",releasePolicyHistoryDigest=r.releasePolicyHistoryDigest??"",missions=missions.ToArray(),decisionSummary="현재 승인된 기관 업무와 시민 계산 상태를 기준으로 전망",calculationCurrent=true};
            request.stateHash=Hash(JsonUtility.ToJson(request));request.snapshotId=Guid.NewGuid().ToString("N");return request;
        }
        private void Accept(string json,Request basis)
        {
            Response response=null;try { if(!string.IsNullOrEmpty(json))response=JsonUtility.FromJson<Response>(json); }catch(ArgumentException) { }
            if(currentState==null||invalidated||basis==null||basis.decisionKey!=conditionKey||basis.runId!=currentState.runId||basis.generation!=currentState.generation)return;
            if(response==null||response.schemaVersion!=1||response.status!="ok"||response.model!="typesafe-ai/jev"||response.snapshotId!=basis.snapshotId||response.stateHash!=basis.stateHash||response.decisionKey!=basis.decisionKey||response.runId!=basis.runId||response.generation!=basis.generation||!Finite(response.basisSimTime)||Mathf.Abs(response.basisSimTime-basis.simTime)>.001f||!Finite(response.latencyMs)||response.latencyMs<0||response.forecasts==null||response.forecasts.Length>3)
            { Hide("예측 갱신 대기");return; }
            if(Regex.Matches(json, "\"probability\"\\s*:\\s*-?[0-9]+(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\\s*[,}]").Count!=response.forecasts.Length) { Hide("예측 갱신 대기");return; }
            var ids=new HashSet<string>();
            foreach(var f in response.forecasts)
            {
                int horizon=f==null?0:f.id=="density_rise_60"?60:f.id=="support_arrival_120"||f.id=="evacuation_complete_120"?120:0;
                if(f==null||horizon==0||f.horizonSeconds!=horizon||!Finite(f.probability)||f.probability<0||f.probability>1||!ids.Add(f.id)||!Applicable(f.id,basis)) { Hide("예측 갱신 대기");return; }
            }
            Current=response;Status=response.forecasts.Length>0?"AI 추정 · 관찰로 검증 중":"현재 조건에서 적용할 예측이 없습니다";Revision++;
            foreach(var f in response.forecasts)
            {
                if(comparisons.Count>=192) { if(comparisons[0].status=="pending")Unscore(comparisons[0],"record_limit");comparisons.RemoveAt(0); }
                var arriving=new List<string>();foreach(var m in basis.missions)if(Travelling(m.state)&&!string.IsNullOrEmpty(m.operationId))arriving.Add(m.operationId);
                comparisons.Add(new Comparison{snapshotId=basis.snapshotId,decisionKey=basis.decisionKey,runId=basis.runId,generation=basis.generation,eventId=f.id,horizonSeconds=f.horizonSeconds,probability=f.probability,basisSimTime=basis.simTime,originDensity=basis.physics.density,status="pending",arrivalOperations=arriving.ToArray()});
            }
            foreach(var observation in observations)if(observation.simTime>=basis.simTime && observation.decisionKey==basis.decisionKey)Observe(observation);
        }
        private static bool Applicable(string id,Request r)
        {
            if(id=="density_rise_60")return true;
            if(id=="evacuation_complete_120")return r.phase!="ordinary"&&r.physics.evacuated<r.physics.total;
            bool travelling=false;foreach(var m in r.missions)if(Travelling(m.state)&&!string.IsNullOrEmpty(m.operationId))travelling=true;return travelling;
        }
        private void Observe(Request observed)
        {
            foreach(var record in comparisons)
            {
                if(record.status!="pending")continue;
                if(record.decisionKey!=observed.decisionKey||record.runId!=observed.runId||record.generation!=observed.generation) { Unscore(record,"changed_condition");continue; }
                float age=observed.simTime-record.basisSimTime;
                if(age<0) { Unscore(record,"clock_reset");continue; }
                bool happened=record.eventId=="density_rise_60"?observed.physics.density>=record.originDensity+.5f:record.eventId=="evacuation_complete_120"?observed.physics.evacuated==observed.physics.total:false;
                if(record.eventId=="support_arrival_120")foreach(var mission in observed.missions)if(Arrived(mission.state)&&Array.IndexOf(record.arrivalOperations,mission.operationId)>=0)happened=true;
                if(happened && age<=record.horizonSeconds)Score(record,1,observed.simTime);
                else if(age>=record.horizonSeconds) { if(happened && age>record.horizonSeconds)Unscore(record,"boundary_observation_gap");else Score(record,0,observed.simTime); }
            }
        }
        private void Score(Comparison record,int outcome,float sim) { record.status="scored";record.outcome=outcome;record.observedSimTime=sim;record.brier=(record.probability-outcome)*(record.probability-outcome);ScoredCount++;BrierSum+=record.brier;Revision++; }
        private void Unscore(Comparison record,string reason) { record.status=reason;UnscoredCount++; }
        private void Invalidate(string message) { if(!invalidated) { conditionEpoch++;foreach(var record in comparisons)if(record.status=="pending")Unscore(record,"calculation_unavailable");invalidated=true; }currentState=null;pending=null;lastFingerprint=null;Hide(message); }
        private void Hide(string message) { if(Current!=null||Status!=message) { Current=null;Status=message;Revision++; } }
        private static bool Travelling(string state)=>state=="Requested"||state=="EnRoute";
        private static bool Arrived(string state)=>state=="OnSite"||state=="AwaitingOutcome"||state=="Working"||state=="Standby"||state=="WaitingIncident"||state=="HandoffPending"||state=="Returning";
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static string Hash(string value) { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant(); }
    }
}
