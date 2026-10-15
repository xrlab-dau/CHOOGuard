using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using ChooGuard.Contracts;
using UnityEngine;
namespace ChooGuard.App.Mvp
{
    public sealed class MvpProgressionGraph : MonoBehaviour
    {
        [Serializable] public class ViewSnapshot { public string Title,CurrentNodeId,Status;public NodeView[] Nodes;public EdgeView[] Edges;public TraceView[] Recent; }
        [Serializable] public class NodeView { public string Id,Label,State,Detail; }
        [Serializable] public class EdgeView { public string Id,From,To,Label,Reason,Prediction,Truth;public bool Enabled,Taken; }
        [Serializable] public class TraceView { public long Sequence;public float SimTime;public string OperationId,From,To,Label,Observed,Expected,ClaimClass;public string RunId,RequestId,GraphHash,EdgeId,AgencyId,TeamId,ChoiceRequestId;public int Generation,ReleaseRevision; }
        [Serializable] private class Node { public string id,label; }
        [Serializable] private class Edge { public string id,from,to,@event,effect,label,prediction,reason;public int priority;public string[] allFacts,notFacts; }
        [Serializable] private class Definition { public string id,startNode;public int version;public Node[] nodes;public Edge[] edges; }
        public sealed class Decision { public string OperationId,EdgeId,From,To,Effect,Label,Prediction;internal int Revision; }
        private sealed class Cursor { public string Id,Agency,Team,Node,Unavailable,ChoiceRequestId;public readonly HashSet<string> Visited=new HashSet<string>(),Taken=new HashSet<string>();public RuleFacts Facts;public string Event;public int Revision; }
        public TextAsset GraphAsset;
        public bool Ready { get; private set; }
        public string GraphHash { get; private set; }
        public string StatusReason { get; private set; }="운영 흐름 자료 확인 전";
        public int Revision { get; private set; }
        public IReadOnlyList<TraceView> TraceRecords=>traces.AsReadOnly();
        private Definition graph;
        private TextAsset checkedAsset;
        private bool attempted;
        private readonly Dictionary<string,RuleExpression> guards=new Dictionary<string,RuleExpression>();
        private readonly Dictionary<string,Cursor> operations=new Dictionary<string,Cursor>();
        private readonly List<TraceView> traces=new List<TraceView>();
        private readonly HashSet<string> evidenceKeys=new HashSet<string>();
        private string latestOperation,lastObservation;
        private long sequence;
        private static readonly HashSet<string> Effects=new HashSet<string>{"none","support-standby","lease-warn","lease-medical","enqueue-evacuate","await-outcome","handoff-pending","return"};
        private static readonly HashSet<string> Facts=new HashSet<string>{"evacuation-task","medical-task","response-phase","all-evacuated","channel-free","own-warn-ack","own-evacuate-ack","own-medical-ack","calculation-current","manual-confirm","can-withdraw","can-cancel"};
        private static readonly HashSet<string> Events=new HashSet<string>{"result","depart","arrive","returned","replenished","confirm","withdraw","cancel"};
        private void Awake()=>Initialize();
        public bool Initialize()
        {
            if(attempted&&checkedAsset==GraphAsset)return Ready;attempted=true;checkedAsset=GraphAsset;
            guards.Clear();
            try
            {
                if(GraphAsset==null)throw new InvalidOperationException("운영 흐름 자료 없음");
                byte[] bytes=GraphAsset.bytes;if(bytes.Length>131072)throw new InvalidOperationException("운영 흐름 자료 크기 초과");
                using(var sha=SHA256.Create())GraphHash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                graph=JsonUtility.FromJson<Definition>(GraphAsset.text);
                if(graph==null||graph.version!=1||string.IsNullOrEmpty(graph.id)||graph.nodes==null||graph.edges==null||graph.nodes.Length<1||graph.nodes.Length>32||graph.edges.Length>128)throw new InvalidOperationException("운영 흐름 구조 오류");
                var nodes=new HashSet<string>();foreach(var n in graph.nodes){ValidateId(n.id);if(string.IsNullOrWhiteSpace(n.label)||!nodes.Add(n.id))throw new InvalidOperationException("중복되거나 이름 없는 단계");}
                if(!nodes.Contains(graph.startNode))throw new InvalidOperationException("시작 단계 없음");
                var ids=new HashSet<string>();foreach(var e in graph.edges)
                {
                    ValidateId(e.id);if(!ids.Add(e.id)||!nodes.Contains(e.from)||!nodes.Contains(e.to)||!Effects.Contains(e.effect)||!Events.Contains(e.@event)||string.IsNullOrWhiteSpace(e.label))throw new InvalidOperationException("운영 흐름 연결 오류");
                    var expressions=new List<RuleExpression>();AddFacts(expressions,e.allFacts,false);AddFacts(expressions,e.notFacts,true);guards[e.id]=RuleExpression.All(expressions);
                }
                Array.Sort(graph.edges,(a,b)=>a.priority!=b.priority?a.priority.CompareTo(b.priority):string.CompareOrdinal(a.id,b.id));Ready=true;StatusReason="운영 흐름 준비됨";Revision++;return true;
            }
            catch(Exception ex){Ready=false;graph=null;StatusReason="운영 흐름 사용 불가 · "+ex.Message;Revision++;return false;}
        }
        private static void ValidateId(string id){if(string.IsNullOrWhiteSpace(id)||id.Length>96)throw new InvalidOperationException("단계 식별 정보 오류");new StableId(id);}
        private static void AddFacts(List<RuleExpression> expressions,string[] facts,bool negate)
        {
            if(facts==null)return;if(facts.Length>24)throw new InvalidOperationException("조건 수 초과");
            foreach(var fact in facts){if(!Facts.Contains(fact))throw new InvalidOperationException("허용되지 않은 운영 조건");var expression=RuleExpression.Fact(new StableId(fact));expressions.Add(negate?RuleExpression.Not(expression):expression);}
        }
        public static RuleFacts FactsFrom(IDictionary<string,RuleTruth> source)
        {
            var facts=new List<KeyValuePair<StableId,RuleTruth?>>();foreach(var pair in source)facts.Add(new KeyValuePair<StableId,RuleTruth?>(new StableId(pair.Key),pair.Value));return new RuleFacts(facts);
        }
        public bool RegisterOperation(string operationId,string agencyId,string teamId,MvpPhysicsResult evidence,float simTime,string choiceRequestId=null)
        {
            if(!Initialize()||string.IsNullOrEmpty(operationId)||operations.ContainsKey(operationId))return false;
            var cursor=new Cursor{Id=operationId,Agency=agencyId,Team=teamId,Node=graph.startNode,ChoiceRequestId=choiceRequestId};cursor.Visited.Add(cursor.Node);operations.Add(operationId,cursor);latestOperation=operationId;
            AddTrace(cursor,"",cursor.Node,"업무 선택 접수","요청이 실제 대응팀에 배정됨","현장 도착 후 조건에 맞춰 업무 수행","process",evidence,simTime,"choice:"+operationId,null);Revision++;return true;
        }
        public Decision Evaluate(string operationId,string eventType,RuleFacts facts,MvpPhysicsResult evidence)
        {
            if(!Ready||!operations.TryGetValue(operationId,out var cursor)||cursor.Unavailable!=null)return null;
            cursor.Facts=facts;cursor.Event=eventType;
            foreach(var edge in graph.edges)if(edge.from==cursor.Node&&edge.@event==eventType&&guards[edge.id].Evaluate(facts)==RuleTruth.TRUE)
                return new Decision{OperationId=operationId,EdgeId=edge.id,From=edge.from,To=edge.to,Effect=edge.effect,Label=edge.label,Prediction=edge.prediction,Revision=cursor.Revision};
            return null;
        }
        public bool Commit(Decision decision,MvpPhysicsResult evidence,float simTime,string observed)
        {
            if(!Ready||decision==null||!operations.TryGetValue(decision.OperationId,out var cursor)||cursor.Revision!=decision.Revision||cursor.Node!=decision.From)return false;
            cursor.Node=decision.To;cursor.Revision++;cursor.Visited.Add(decision.To);cursor.Taken.Add(decision.EdgeId);
            AddTrace(cursor,decision.From,decision.To,decision.Label,observed,decision.Prediction,"process",evidence,simTime,"edge:"+cursor.Id+":"+cursor.Revision,decision.EdgeId);Revision++;return true;
        }
        public bool ObservePrimitive(string operationId,string eventType,MvpPhysicsResult evidence,float simTime)
        {
            var decision=Evaluate(operationId,eventType,FactsFrom(new Dictionary<string,RuleTruth>()),evidence);
            if(decision==null){MarkOperationUnavailable(operationId,"실제 진행에 대응하는 운영 경로 없음",evidence,simTime);return false;}
            return Commit(decision,evidence,simTime,decision.Label+" · 실제 상태 변경 확인");
        }
        public void RecordAcknowledgment(string operationId,string action,MvpPhysicsResult result)
        {
            if(!operations.TryGetValue(operationId,out var cursor))return;
            string label=action=="warn"?"경고 방송 접수 확인":action=="evacuate"?"대피 유도 접수 확인":"의료 지원 요청 접수 확인";
            AddTrace(cursor,cursor.Node,cursor.Node,label,"요청 번호와 실제 계산 응답이 일치함",action=="medical"?"요청 접수는 환자 회복·이송 판정이 아님":"효과는 후속 계산 관찰로 확인","process",result,result.sessionSimTime,"ack:"+result.runId+":"+result.generation+":"+result.requestId+":"+operationId,null);
        }
        public void ObserveModel(MvpPhysicsResult result,float simTime)
        {
            if(result==null)return;
            string fingerprint=result.runId+":"+result.generation+":"+result.phase+":"+result.evacuated+":"+result.total+":"+result.warned+":"+result.evacuationOrdered+":"+result.medicalAssistanceRequested+":"+result.fieldCurrent+":"+result.releaseRevision;
            if(fingerprint==lastObservation)return;lastObservation=fingerprint;
            string phase=result.phase=="ordinary"?"평시":result.phase=="incident"?"사건 대응":result.phase=="resolved"?"복구":"계산 범위 확인";
            string observed=phase+" · 출구 도달 "+result.evacuated+"/"+result.total+"명"+(result.medicalAssistanceRequested?" · 의료 요청 접수됨":"")+(result.fireRequired&&!result.fieldCurrent?" · 화재장 현재값 사용 불가":"");
            AddTrace(null,"","","계산 관찰 변경",observed,"후속 관찰에서 현장 변화를 확인합니다.","model-observation",result,simTime,"model:"+fingerprint,null);
        }
        public void MarkOperationUnavailable(string operationId,string reason,MvpPhysicsResult evidence,float simTime)
        {
            if(!operations.TryGetValue(operationId,out var cursor)||cursor.Unavailable==reason)return;cursor.Unavailable=reason;AddTrace(cursor,cursor.Node,cursor.Node,"업무 진행 확인 필요",reason,"완료 판정 없음","unavailable",evidence,simTime,"unavailable:"+operationId+":"+reason,null);Revision++;
        }
        public void ObserveCalculationFailure(string reason,MvpPhysicsResult evidence,float simTime)
        {
            foreach(var cursor in operations.Values)MarkOperationUnavailable(cursor.Id,reason,evidence,simTime);StatusReason="계산 중단 · 기록된 관찰은 현재 성공 판정이 아닙니다";
        }
        private void AddTrace(Cursor cursor,string from,string to,string label,string observed,string expected,string claim,MvpPhysicsResult evidence,float simTime,string key,string edgeId)
        {
            if(!evidenceKeys.Add(key))return;if(traces.Count>=512)traces.RemoveAt(0);
            traces.Add(new TraceView{Sequence=++sequence,SimTime=simTime,OperationId=cursor?.Id,From=from,To=to,Label=label,Observed=observed,Expected=expected,ClaimClass=claim,RunId=evidence?.runId,Generation=evidence?.generation??0,RequestId=evidence?.requestId,ReleaseRevision=evidence?.releaseRevision??0,GraphHash=GraphHash,AgencyId=cursor?.Agency,TeamId=cursor?.Team,ChoiceRequestId=cursor?.ChoiceRequestId,EdgeId=edgeId});Revision++;
        }
        public void ResetRun(){operations.Clear();traces.Clear();evidenceKeys.Clear();latestOperation=null;lastObservation=null;sequence=0;StatusReason=Ready?"새 실행 · 업무 선택 대기":StatusReason;Revision++;}
        public ViewSnapshot Snapshot(string operationId=null)
        {
            Initialize();operations.TryGetValue(operationId??latestOperation??"",out var cursor);var nodes=new List<NodeView>();var edges=new List<EdgeView>();var recent=new List<TraceView>();
            if(graph?.nodes!=null)foreach(var node in graph.nodes)nodes.Add(new NodeView{Id=node.id,Label=node.label,State=cursor?.Node==node.id?"현재":cursor!=null&&cursor.Visited.Contains(node.id)?"지남":"대기",Detail=cursor?.Node==node.id?(cursor.Unavailable??"현재 업무 단계"):""});
            if(graph?.edges!=null)foreach(var edge in graph.edges)
            {
                RuleTruth truth=guards.TryGetValue(edge.id,out var guard)?guard.Evaluate(cursor?.Facts):RuleTruth.UNKNOWN;bool current=cursor!=null&&cursor.Node==edge.from;
                edges.Add(new EdgeView{Id=edge.id,From=edge.from,To=edge.to,Label=edge.label,Prediction=edge.prediction,Truth=truth.ToString(),Enabled=Ready&&current&&cursor.Unavailable==null&&cursor.Event==edge.@event&&truth==RuleTruth.TRUE,Taken=cursor!=null&&cursor.Taken.Contains(edge.id),Reason=truth==RuleTruth.UNKNOWN?"필요한 관찰을 기다리는 중":truth==RuleTruth.CONFLICTED?"관찰이 서로 일치하지 않아 진행할 수 없음":truth==RuleTruth.FALSE?edge.reason:edge.reason});
            }
            for(int i=traces.Count-1;operationId!=""&&i>=0&&recent.Count<24;i--)if(cursor==null||traces[i].OperationId==cursor.Id)recent.Add(traces[i]);
            recent.Reverse();
            return new ViewSnapshot{Title="운영 흐름",CurrentNodeId=cursor?.Node,Status=!Ready?StatusReason:cursor==null?"기관에서 지원업무를 선택하세요":cursor.Unavailable??"선택한 업무의 실제 진행과 관찰",Nodes=nodes.ToArray(),Edges=edges.ToArray(),Recent=recent.ToArray()};
        }
    }
}
