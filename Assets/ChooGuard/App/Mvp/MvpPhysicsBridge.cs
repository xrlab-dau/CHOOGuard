using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Persistence;
using UnityEngine;
namespace ChooGuard.App.Mvp
{
    [Serializable] public sealed class MvpHazardFieldFrame
    {
        public int version,width,height;
        public string caseId,quantity,unit,samplerVersion,encoding,sourceSha256,deckSha256,payloadSha256,valuesBase64;
        public float originX,originY,stepX,stepY,sampleHeight,requestedIncidentTime,sampledIncidentTime,validFrom,validUntil,displayMin,displayMax;
    }
    [Serializable] public sealed class MvpPhysicsAgent { public string routineState,destinationId; public int id,cohort; public float x,z,fedToxic,fedConvectiveHeat; public bool released; }
    [Serializable] public sealed class MvpCohortStats { public int id,total,active,evacuated,released; public bool isReleased,final; public float maxFedToxic,maxFedConvectiveHeat,meanFinalFedToxic,meanFinalFedConvectiveHeat,finalExitTime; }
    public sealed class MvpControlOrder { public string Action,ReleasePolicy,RequestId=Guid.NewGuid().ToString("N"); public int CohortId=-1; }
    [Serializable] public sealed class MvpPhysicsResult
    {
        public MvpHazardFieldFrame hazardField;
        public int hazardFieldVersion;
        public string hazardFieldSourceSha256,hazardFieldDeckSha256,samplerVersion;
        public string phase,incidentInitialDigest;
        public string kind,runId,engine,engineVersion,fdsStatus,fdsVersion,unsupported,message,diagnostic,caseId,initialStateDigest,cohortScheme,releasePolicyHistoryDigest,requestId,lastControlRequestId;
        public int lifecycleVersion,normalDepartedCount,incidentEvacuatedCount;
        public int generation,seed,total,evacuated,releaseRevision;
        public float sessionSimTime,incidentOnsetSimTime,incidentTime,fieldValidUntil;
        public bool fieldCurrent;
        public float simTime,density,pressureIndicator,visibility,temperature,extinction,sootDensity,maxFedToxic,maxFedConvectiveHeat;
        public bool available,fdsAvailable,physicsReady,fireRequired,allEvacuated,warned,evacuationOrdered,medicalAssistanceRequested;
        public MvpPhysicsAgent[] agents;
        public MvpCohortStats[] cohortStats;
    }
    public sealed class MvpPhysicsBridge : MonoBehaviour
    {
        [Serializable] private sealed class Request
        {
            public string kind,runId,scenario,action,releasePolicy,requestId;
            public int cohortId=-1;
            public int protocolVersion=1,generation,seed,population;
            public float stepSeconds;
        }
        public event Action<MvpPhysicsResult> ResultReceived;
        public event Action<string> StatusChanged;
        public bool InFlight { get; private set; }
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public MvpPhysicsResult LastResult { get; private set; }
        public string Diagnostic { get; private set; }
        private Process process;
        private IDisposable processJob;
        private readonly ConcurrentQueue<string> inbox=new ConcurrentQueue<string>();
        private int queued,readerEpoch;
        private bool capabilities,pendingStart,routineStart;
        private int lifecycleVersion,hazardFieldVersion;
        private string hazardSourceHash,hazardDeckHash;
        private string run,scenario,inFlightRequest;
        private int generation,seed,population;
        private float sentAt;
        private volatile string readerFailure;
        private string stderrTail;
        public void StartRun(int valueSeed,int count,string type)
        { StartSession(valueSeed,count,type,false); }
        public void StartRoutineRun(int seed,int population,string scenario) { StartSession(seed,population,scenario,true); }
        public bool BeginIncident() => Ready && LastResult!=null && LastResult.phase=="ordinary" && SendAction("begin_incident",0);
        private void StartSession(int valueSeed,int count,string type,bool routine)
        {
            routineStart=routine;
            Cancel(); generation++; run=Guid.NewGuid().ToString("N"); seed=valueSeed; population=count; scenario=type;
            Failed=false; Ready=false; LastResult=null; Diagnostic=null; pendingStart=true;
            if(process==null || process.HasExited) Launch();
            else if(capabilities) SendStart();
        }
        private void Launch()
        {
            DisposeProcess();
            try
            {
                RuntimePackage.ConfigureRoot(UnityEngine.Application.isEditor
                    ? Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../workers/runtime"))
                    : Path.Combine(UnityEngine.Application.streamingAssetsPath,"ChooGuardRuntime"));
                var start=RuntimePackage.LoadConfigured().PhysicsStartInfo();
                process=new Process { StartInfo=start,EnableRaisingEvents=true };
                if(!process.Start()) throw new InvalidOperationException("WORKER_START_FAILED");
                processJob=WorkerProcessLifetime.Attach(process);
                int epoch=++readerEpoch;
                var output=process.StandardOutput; var errors=process.StandardError;
                Task.Run(()=>ReadLines(output,epoch,true)); Task.Run(()=>ReadLines(errors,epoch,false));
                Send(new Request { kind="HELLO" }); StatusChanged?.Invoke("전문 계산 엔진 연결 중");
            }
            catch(Exception error) { Fail(error.Message); }
        }
        private void ReadLines(StreamReader reader,int epoch,bool protocol)
        {
            try
            {
                var builder=new StringBuilder(1024);
                while(epoch==Volatile.Read(ref readerEpoch))
                {
                    int c=reader.Read();
                    if(epoch!=Volatile.Read(ref readerEpoch)) return;
                    if(c<0) { if(protocol) ReaderFailed(epoch,"계산 워커 연결이 종료되었습니다."); return; }
                    if(c=='\n')
                    {
                        string line=builder.ToString(); builder.Clear();
                        lock(inbox)
                        {
                            if(epoch!=Volatile.Read(ref readerEpoch)) return;
                            if(protocol) { if(Interlocked.Increment(ref queued)>16) { readerFailure="계산 수신 대기열 한도를 초과했습니다."; return; } inbox.Enqueue(line); }
                            else stderrTail=line.Length>2048?line.Substring(line.Length-2048):line;
                        }
                    }
                    else { if(builder.Length>=1024*1024) { ReaderFailed(epoch,"계산 메시지 크기 한도를 초과했습니다."); return; } builder.Append((char)c); }
                }
            }
            catch(Exception error) { ReaderFailed(epoch,error.Message); }
        }
        private void ReaderFailed(int epoch,string message)
        {
            lock(inbox) { if(epoch==Volatile.Read(ref readerEpoch)) readerFailure=message; }
        }
        private void SendStart() { pendingStart=false; if(routineStart && lifecycleVersion!=1) { Fail("평시 운영 계약을 지원하지 않는 계산 엔진입니다."); return; } SendAction(routineStart?"start_routine":"start",0); }
        public bool SendAction(string action,float seconds) => SendControl(new MvpControlOrder { Action=action },seconds);
        public bool SendControl(MvpControlOrder order,float seconds=0)
        {
            if(Failed || InFlight || !capabilities)return false;
            return Send(new Request { kind="SUBMIT",runId=run,generation=generation,seed=seed,population=population,scenario=scenario,action=order.Action,stepSeconds=seconds,releasePolicy=order.ReleasePolicy,cohortId=order.CohortId,requestId=order.RequestId });
        }
        private bool Send(Request request)
        {
            try { inFlightRequest=request.requestId;process.StandardInput.WriteLine(JsonUtility.ToJson(request)); process.StandardInput.Flush(); InFlight=true; sentAt=Time.realtimeSinceStartup; return true; }
            catch(Exception error) { Fail(error.Message); return false; }
        }
        private void Update()
        {
            while(inbox.TryDequeue(out string line))
            {
                Interlocked.Decrement(ref queued);
                try
                {
                    var result=JsonUtility.FromJson<MvpPhysicsResult>(line);
                    if(result==null) throw new InvalidDataException("빈 계산 응답");
                    if(result.kind=="CAPABILITIES")
                    {
                        InFlight=false; capabilities=result.available; lifecycleVersion=result.lifecycleVersion; hazardFieldVersion=result.hazardFieldVersion; hazardSourceHash=result.hazardFieldSourceSha256; hazardDeckHash=result.hazardFieldDeckSha256;
                        if(!capabilities || (scenario=="fire_smoke" && !result.fdsAvailable)) { Fail("필수 물리 엔진을 사용할 수 없습니다. "+result.diagnostic); continue; }
                        if(pendingStart) SendStart(); continue;
                    }
                    if(result.runId!=run || result.generation!=generation) continue;
                    if(result.kind=="CANCELLED") { InFlight=false; continue; }
                    if(result.kind=="ERROR") { if(result.requestId!=inFlightRequest)continue;Fail(result.message); continue; }
                    if(result.kind!="RESULT") { Fail("지원하지 않는 계산 응답"); continue; }
                    if(result.requestId!=inFlightRequest)continue;
                    InFlight=false;
                    if(result.cohortScheme!="spawn-z-thirds-v1"||string.IsNullOrEmpty(result.initialStateDigest)||result.cohortStats==null||result.cohortStats.Length!=3)throw new InvalidDataException("집단 계산 계약 불일치");
                    if(LastResult!=null&&(result.initialStateDigest!=LastResult.initialStateDigest||result.releaseRevision<LastResult.releaseRevision))throw new InvalidDataException("초기 상태 또는 통제 순서 불일치");
                    if(result.caseId!="reference-hall-30x20-v1" || result.fireRequired!=(scenario=="fire_smoke") || !result.physicsReady || (result.fireRequired && result.fdsStatus!="completed_reference") || result.seed!=seed || result.total!=population || result.agents==null || result.evacuated<0 || result.evacuated>population || result.agents.Length+result.evacuated!=population || (result.allEvacuated && result.evacuated!=population) || !Finite(result.simTime) || !Finite(result.temperature) || !Finite(result.visibility) || !Finite(result.density) || !Finite(result.pressureIndicator)) throw new InvalidDataException("계산 결과 계약 불일치");
                    if(LastResult!=null && result.simTime<LastResult.simTime) throw new InvalidDataException("이전 시각의 계산 응답");
                    int cohortTotal=0,cohortActive=0,cohortEvacuated=0;for(int i=0;i<3;i++){var c=result.cohortStats[i];if(c.id!=i||c.active<0||c.evacuated<0||c.active+c.evacuated!=c.total||c.released<0||c.released>c.active)throw new InvalidDataException("집단 인원 불일치");cohortTotal+=c.total;cohortActive+=c.active;cohortEvacuated+=c.evacuated;}if(cohortTotal!=population||cohortActive!=result.agents.Length||cohortEvacuated!=result.evacuated)throw new InvalidDataException("집단 합계 불일치");
                    foreach(var agent in result.agents) if(agent.cohort<0||agent.cohort>2||agent.released!=result.cohortStats[agent.cohort].isReleased||!Finite(agent.x)||!Finite(agent.z)||agent.x<0||agent.x>30||agent.z<0||agent.z>20) throw new InvalidDataException("기준 공간 밖 계산 좌표");
                    if(routineStart && (result.lifecycleVersion!=1 || !Finite(result.sessionSimTime) || Mathf.Abs(result.sessionSimTime-result.simTime)>.001f || result.normalDepartedCount!=0 || result.incidentEvacuatedCount!=result.evacuated || (result.phase!="ordinary" && result.phase!="incident" && result.phase!="resolved" && result.phase!="incomplete"))) throw new InvalidDataException("운영 단계 계약 불일치");
                    if(routineStart && result.phase=="ordinary" && (result.incidentTime!=-1 || result.incidentOnsetSimTime!=-1 || result.evacuated!=0)) throw new InvalidDataException("평시 사건 시각 불일치");
                    if(routineStart && result.phase!="ordinary" && (string.IsNullOrEmpty(result.incidentInitialDigest) || Mathf.Abs(result.incidentTime-(result.sessionSimTime-result.incidentOnsetSimTime))>.001f)) throw new InvalidDataException("사건 시각 계약 불일치");
                    if(LastResult!=null && !string.IsNullOrEmpty(LastResult.incidentInitialDigest) && result.incidentInitialDigest!=LastResult.incidentInitialDigest) throw new InvalidDataException("사건 초기 상태 불일치");
                    if(LastResult!=null && LastResult.phase=="ordinary" && result.phase=="incident")
                    {
                        if(result.simTime!=LastResult.simTime || result.agents.Length!=LastResult.agents.Length) throw new InvalidDataException("사건 전환 중 물리 시간이 변경되었습니다.");
                        foreach(var before in LastResult.agents)
                        {
                            var after=Array.Find(result.agents,a=>a.id==before.id);
                            if(after==null || after.x!=before.x || after.z!=before.z || after.cohort!=before.cohort || after.fedToxic!=before.fedToxic || after.fedConvectiveHeat!=before.fedConvectiveHeat || after.released) throw new InvalidDataException("사건 전환 중 시민 상태가 변경되었습니다.");
                        }
                    }
                    NormalizeOptionalHazardField(result);
                    if(result.fieldCurrent)
                    {
                        if(!result.fireRequired || result.phase!="incident" || hazardFieldVersion!=1 || result.samplerVersion!="native_nodes_nearest_v1" || result.hazardField==null || result.hazardField.sourceSha256!=hazardSourceHash || result.hazardField.deckSha256!=hazardDeckHash || Mathf.Abs(result.hazardField.requestedIncidentTime-result.incidentTime)>.001f) throw new InvalidDataException("위험장 출처 또는 시각 계약 불일치");
                        if(!TryDecodeHazardField(result.hazardField,out _,out string fieldError)) throw new InvalidDataException(fieldError);
                    }
                    else if(result.hazardField!=null) throw new InvalidDataException("현재 유효하지 않은 위험장 응답");
                    if(result.fireRequired && result.phase=="incident" && !result.fieldCurrent) throw new InvalidDataException("사건 위험장이 누락되었습니다.");
                    LastResult=result; Ready=true; ResultReceived?.Invoke(result);
                }
                catch(Exception error) { Fail(error.Message); }
            }
            if(readerFailure!=null && !Failed) { var error=readerFailure; readerFailure=null; Fail(error); }
            if(InFlight && Time.realtimeSinceStartup-sentAt>10) Fail("계산 응답 대기 시간이 초과되었습니다. 완료를 판정하지 않습니다.");
        }
        // JsonUtility materializes an omitted/null nested serializable object as a default DTO.
        // Only the wholly default inactive object represents absence; populated inactive fields stay invalid.
        public static void NormalizeOptionalHazardField(MvpPhysicsResult result)
        {
            if(result==null || result.fieldCurrent || result.hazardField==null) return;
            var frame=result.hazardField;
            if(frame.version==0 && frame.width==0 && frame.height==0 &&
                frame.caseId==null && frame.quantity==null && frame.unit==null && frame.samplerVersion==null &&
                frame.encoding==null && frame.sourceSha256==null && frame.deckSha256==null && frame.payloadSha256==null && frame.valuesBase64==null &&
                frame.originX==0 && frame.originY==0 && frame.stepX==0 && frame.stepY==0 && frame.sampleHeight==0 &&
                frame.requestedIncidentTime==0 && frame.sampledIncidentTime==0 && frame.validFrom==0 && frame.validUntil==0 && frame.displayMin==0 && frame.displayMax==0)
                result.hazardField=null;
        }
        public static bool TryDecodeHazardField(MvpHazardFieldFrame frame,out float[] values,out string error)
        {
            values=null; error=null;
            try
            {
                if(frame==null || frame.version!=1 || frame.caseId!="reference-hall-30x20-v1" || frame.quantity!="SOOT EXTINCTION COEFFICIENT" || frame.unit!="1/m" || frame.samplerVersion!="native_nodes_nearest_v1" || frame.encoding!="base64-float32-le-row-major-yx") throw new InvalidDataException("위험장 형식 불일치");
                if(frame.width!=61 || frame.height!=41 || frame.originX!=0 || frame.originY!=0 || frame.stepX!=.5f || frame.stepY!=.5f || frame.sampleHeight!=1.5f) throw new InvalidDataException("위험장 좌표 불일치");
                if(!Finite(frame.requestedIncidentTime) || !Finite(frame.sampledIncidentTime) || frame.validFrom!=0 || frame.validUntil!=120 || frame.requestedIncidentTime<0 || frame.requestedIncidentTime>120 || frame.sampledIncidentTime<0 || frame.sampledIncidentTime>120 || Mathf.Abs(frame.sampledIncidentTime-frame.requestedIncidentTime)>1.21f) throw new InvalidDataException("위험장 시각 범위 불일치");
                if(frame.displayMin!=0 || !Finite(frame.displayMax) || Mathf.Abs(frame.displayMax-4.27322674f)>.00001f || !HashText(frame.sourceSha256) || !HashText(frame.deckSha256) || !HashText(frame.payloadSha256)) throw new InvalidDataException("위험장 범위 또는 출처 불일치");
                if(frame.valuesBase64==null || frame.valuesBase64.Length!=13340) throw new InvalidDataException("위험장 자료 길이 불일치");
                byte[] bytes=Convert.FromBase64String(frame.valuesBase64);
                if(bytes.Length!=61*41*4) throw new InvalidDataException("위험장 자료 크기 불일치");
                using(var sha=SHA256.Create()) if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!=frame.payloadSha256) throw new InvalidDataException("위험장 해시 불일치");
                var decoded=new float[61*41];
                for(int i=0;i<decoded.Length;i++)
                {
                    if(!BitConverter.IsLittleEndian) Array.Reverse(bytes,i*4,4);
                    decoded[i]=BitConverter.ToSingle(bytes,i*4);
                    if(!Finite(decoded[i]) || decoded[i]<0 || decoded[i]>frame.displayMax+.00001f) throw new InvalidDataException("위험장 물리값 불일치");
                }
                values=decoded; return true;
            }
            catch(Exception exception) { error=exception.Message; return false; }
        }
        private static bool HashText(string value)
        {
            if(value==null || value.Length!=64) return false;
            foreach(char c in value) if(!((c>='0'&&c<='9')||(c>='a'&&c<='f'))) return false;
            return true;
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private void Fail(string text) { Failed=true; Ready=false; InFlight=false; Diagnostic=text; DisposeProcess(); UnityEngine.Debug.LogWarning("계산 워커 진단: "+text,this); StatusChanged?.Invoke("계산 불가 · 엔진 진단을 확인하고 새 실험을 시작하세요."); }
        public void Cancel()
        {
            if(process!=null && !string.IsNullOrEmpty(run))
                try { if(!process.HasExited) { process.StandardInput.WriteLine(JsonUtility.ToJson(new Request { kind="CANCEL",runId=run,generation=generation })); process.StandardInput.Flush(); } } catch(IOException) { } catch(InvalidOperationException) { }
            pendingStart=false; InFlight=false; Ready=false;
        }
        private void DisposeProcess()
        {
            lock(inbox) { Interlocked.Increment(ref readerEpoch); readerFailure=null; }
            capabilities=false;
            var owned=process; process=null;
            if(owned!=null)
            {
                try
                {
                    if(!owned.HasExited)
                    {
                        try { owned.StandardInput.Close(); } catch(IOException) { } // EOF is the worker's graceful shutdown contract.
                        if(!owned.WaitForExit(250)) { owned.Kill(); if(!owned.WaitForExit(1500)) UnityEngine.Debug.LogError("WORKER_SHUTDOWN_TIMEOUT",this); }
                    }
                }
                catch(Exception error) when(error is InvalidOperationException || error is IOException || error is System.ComponentModel.Win32Exception)
                { UnityEngine.Debug.LogWarning("WORKER_SHUTDOWN_FAILED: "+error.Message,this); }
                finally { owned.Dispose(); }
            }
            processJob?.Dispose(); processJob=null; // Windows closes the owned job and terminates any remaining descendants.
            lock(inbox) { while(inbox.TryDequeue(out _)) {} queued=0; }
        }
        private void OnApplicationQuit() { DisposeProcess(); }
        private void OnDisable() { Cancel(); DisposeProcess(); }
        private void OnDestroy() { DisposeProcess(); }
    }
}
