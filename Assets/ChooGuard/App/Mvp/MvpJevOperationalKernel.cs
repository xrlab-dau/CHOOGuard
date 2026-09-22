using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace ChooGuard.App.Mvp
{
    // Internal bounded mean-duration parameter. Never drives physics, orders, or a public probability panel.
    public sealed class MvpJevOperationalKernel : MonoBehaviour
    {
        public sealed class TimingDecision
        {
            public string OperationId { get; } public string RunId { get; } public int Generation { get; }
            public bool HasProbability=>ProbabilityNormal.HasValue;
            public float? ProbabilityNormal { get; }
            public float NormalSeconds { get; } public float ExtendedSeconds { get; } public float ExpectedSeconds { get; }
            public string Source { get; } public string InputHash { get; } public string Model { get; } public float LatencyMs { get; } public string ReasonCode { get; }
            internal TimingDecision(string operationId,string runId,int generation,float? probability,float normal,float extended,string inputHash,string model,float latency,string reason)
            {OperationId=operationId;RunId=runId;Generation=generation;ProbabilityNormal=probability;NormalSeconds=normal;ExtendedSeconds=extended;ExpectedSeconds=probability.HasValue?probability.Value*normal+(1-probability.Value)*extended:normal;Source=probability.HasValue?"jev_expected":"baseline";InputHash=inputHash;Model=probability.HasValue?model:"";LatencyMs=latency;ReasonCode=reason;}
        }
        [Serializable] private sealed class Workload { public string workType;public int memberCount;public float operationStartedSim,onsiteDurationSeconds,returnStartedSim,travelMetres; }
        [Serializable] private sealed class PhysicsState { public int total,evacuated,releaseRevision;public string phase;public float density,pressureIndicator,visibility,temperature,maxFedToxic,maxFedConvectiveHeat,incidentTime;public bool fieldCurrent,fireRequired,warned,evacuationOrdered,medicalAssistanceRequested; }
        [Serializable] private sealed class Request { public int schemaVersion=1;public string requestId,inputHash="",operationId,agencyId,logicalTeamId,runId;public int generation;public float normalSeconds,extendedSeconds;public Workload workload;public PhysicsState physics;public MvpCohortStats[] cohorts;public string[] operationHistory; }
        [Serializable] private sealed class Response { public int schemaVersion,generation;public string requestId,inputHash,operationId,runId,status,model,reasonCode;public bool hasProbability;public float probabilityNormal,normalSeconds,extendedSeconds,latencyMs; }
        private sealed class Record { public string Id,RunId,Hash="",Reason="not_requested";public int Generation,Epoch;public float Normal,Extended;public Request Request;public Response Candidate;public TimingDecision Frozen; }
        [Range(15,45)] public float NormalSeconds=15;
        [Range(15,45)] public float ExtendedSeconds=45;
        public IReadOnlyList<TimingDecision> Decisions=>decisions.AsReadOnly();
        private readonly Dictionary<string,Record> records=new Dictionary<string,Record>();
        private readonly Queue<Record> pending=new Queue<Record>();
        private readonly List<TimingDecision> decisions=new List<TimingDecision>();
        private HttpClient http;
        private CancellationTokenSource cancellation;
        private Task<string> flight;
        private Record flying;
        private int epoch;
        private string runKey;
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private void EnsureClient(){if(http==null)http=new HttpClient(new HttpClientHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(3)};}
        public void PrepareTurnaround(string operationId,string agencyId,string logicalTeamId,string workType,int memberCount,float operationStartedSim,float onsiteSim,float travelMetres)
        {
            if(string.IsNullOrEmpty(operationId))return;var bridge=GetComponent<MvpPhysicsBridge>();var result=bridge?.LastResult;
            string key=result==null?null:result.runId+":"+result.generation;if(runKey!=null&&key!=null&&runKey!=key)ResetRun();if(key!=null)runKey=key;
            if(records.ContainsKey(operationId))return;
            bool profile=Finite(NormalSeconds)&&Finite(ExtendedSeconds)&&NormalSeconds>=15&&ExtendedSeconds<=45&&ExtendedSeconds>=NormalSeconds;
            var record=new Record{Id=operationId,RunId=result?.runId??"",Generation=result?.generation??0,Normal=profile?NormalSeconds:15,Extended=profile?ExtendedSeconds:45,Epoch=epoch};records.Add(operationId,record);
            if(!profile){record.Reason="invalid_profile";return;}
            if(result==null||bridge.Failed||!result.physicsReady||result.phase=="incomplete"||!Finite(operationStartedSim)||!Finite(onsiteSim)||onsiteSim<0||!Finite(travelMetres)||travelMetres<0||memberCount<1){record.Reason="calculation_unavailable";return;}
            float now=GetComponent<MvpTrainingDirector>()?.SimulatedSeconds??result.sessionSimTime;
            foreach(float v in new[]{now,result.density,result.pressureIndicator,result.visibility,result.temperature,result.maxFedToxic,result.maxFedConvectiveHeat,result.incidentTime})if(!Finite(v)){record.Reason="nonfinite_input";return;}
            if(result.cohortStats!=null)foreach(var c in result.cohortStats)if(c==null||!Finite(c.maxFedToxic)||!Finite(c.maxFedConvectiveHeat)||!Finite(c.meanFinalFedToxic)||!Finite(c.meanFinalFedConvectiveHeat)||!Finite(c.finalExitTime)){record.Reason="nonfinite_input";return;}
            var history=new List<string>();var graph=GetComponent<MvpProgressionGraph>();if(graph!=null)foreach(var trace in graph.TraceRecords)if(trace.OperationId==operationId){history.Add(trace.Label);if(history.Count>8)history.RemoveAt(0);}
            var request=new Request{requestId=Guid.NewGuid().ToString("N"),operationId=operationId,agencyId=agencyId,logicalTeamId=logicalTeamId,runId=record.RunId,generation=record.Generation,normalSeconds=record.Normal,extendedSeconds=record.Extended,workload=new Workload{workType=workType,memberCount=memberCount,operationStartedSim=operationStartedSim,onsiteDurationSeconds=onsiteSim,returnStartedSim=now,travelMetres=travelMetres},physics=new PhysicsState{total=result.total,evacuated=result.evacuated,releaseRevision=result.releaseRevision,phase=result.phase,density=result.density,pressureIndicator=result.pressureIndicator,visibility=result.visibility,temperature=result.temperature,maxFedToxic=result.maxFedToxic,maxFedConvectiveHeat=result.maxFedConvectiveHeat,incidentTime=result.incidentTime,fieldCurrent=result.fieldCurrent,fireRequired=result.fireRequired,warned=result.warned,evacuationOrdered=result.evacuationOrdered,medicalAssistanceRequested=result.medicalAssistanceRequested},cohorts=result.cohortStats??Array.Empty<MvpCohortStats>(),operationHistory=history.ToArray()};
            using(var sha=SHA256.Create())request.inputHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)))).Replace("-","").ToLowerInvariant();
            record.Hash=request.inputHash;record.Request=request;record.Reason="pending";
            if(pending.Count>=16){record.Reason="queue_full";return;}pending.Enqueue(record);Pump();
        }
        private async Task<string> Send(string json,CancellationToken token)
        {
            try {using(var body=new StringContent(json,Encoding.UTF8,"application/json"))using(var response=await http.PostAsync("http://127.0.0.1:18764/turnaround",body,token).ConfigureAwait(false)){if(!response.IsSuccessStatusCode)return "";var text=await response.Content.ReadAsStringAsync().ConfigureAwait(false);return text.Length<=32768?text:"";}}
            catch {return "";}
        }
        private void Update(){Harvest();Pump();}
        private void Pump()
        {
            if(flight!=null)return;while(pending.Count>0){var record=pending.Dequeue();if(record.Epoch!=epoch||record.Frozen!=null||record.Reason!="pending")continue;EnsureClient();flying=record;cancellation=new CancellationTokenSource();flight=Send(JsonUtility.ToJson(record.Request),cancellation.Token);break;}
        }
        private void Harvest()
        {
            if(flight==null||!flight.IsCompleted)return;var record=flying;var task=flight;flight=null;flying=null;cancellation?.Dispose();cancellation=null;
            if(record==null||record.Epoch!=epoch||record.Frozen!=null)return;
            try
            {
                var response=JsonUtility.FromJson<Response>(task.Result);var current=GetComponent<MvpPhysicsBridge>()?.LastResult;
                if(response==null||response.schemaVersion!=1||response.status!="ok"||!response.hasProbability||response.model!="typesafe-ai/jev"||response.requestId!=record.Request.requestId||response.inputHash!=record.Hash||response.operationId!=record.Id||response.runId!=record.RunId||response.generation!=record.Generation||current?.runId!=record.RunId||current.generation!=record.Generation||!Finite(response.probabilityNormal)||response.probabilityNormal<0||response.probabilityNormal>1||!Finite(response.latencyMs)||response.latencyMs<0||response.latencyMs>3000||Mathf.Abs(response.normalSeconds-record.Normal)>.001f||Mathf.Abs(response.extendedSeconds-record.Extended)>.001f){record.Reason="unavailable_or_mismatched";return;}
                record.Candidate=response;record.Reason="candidate_ready";
            }
            catch {record.Reason="unavailable_or_timeout";}
        }
        public TimingDecision ResolveTurnaround(string operationId)
        {
            Harvest();if(operationId==null)operationId="";
            if(!records.TryGetValue(operationId,out var record)){var result=GetComponent<MvpPhysicsBridge>()?.LastResult;record=new Record{Id=operationId,RunId=result?.runId??"",Generation=result?.generation??0,Normal=15,Extended=45,Reason="not_prepared",Epoch=epoch};records[operationId]=record;}
            if(record.Frozen!=null)return record.Frozen;
            var current=GetComponent<MvpPhysicsBridge>()?.LastResult;bool matches=record.Epoch==epoch&&current!=null&&current.runId==record.RunId&&current.generation==record.Generation;var candidate=matches?record.Candidate:null;
            record.Frozen=new TimingDecision(record.Id,record.RunId,record.Generation,candidate!=null?(float?)candidate.probabilityNormal:null,record.Normal,record.Extended,record.Hash,candidate?.model??"",candidate?.latencyMs??0,candidate!=null?"matching_response":!matches?"run_changed":record.Reason=="pending"?"baseline_frozen_before_response":record.Reason);
            decisions.Add(record.Frozen);return record.Frozen;
        }
        public void ResetRun(){epoch++;cancellation?.Cancel();cancellation?.Dispose();cancellation=null;flight=null;flying=null;pending.Clear();records.Clear();decisions.Clear();runKey=null;}
        private void OnDestroy(){ResetRun();http?.Dispose();http=null;}
    }
}
