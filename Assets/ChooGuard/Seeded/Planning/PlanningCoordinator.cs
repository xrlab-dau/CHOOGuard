using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public sealed class PlanningRecord
    {
        public const string Version="planning-record-v3";
        public string RecordVersion=Version;
        public string RequestId,Stage,RequestJson,ReplyJson,ReplyEncoding,Disposition,PolicyVersion;
        public long RequestedTick,AppliedTick,RecordIndex;
    }
    /// <summary>AgenticCache-inspired guarded reuse. Not the upstream AgenticCache implementation.</summary>
    public sealed class PlanningCoordinator:IDisposable
    {
        public const string PromptVersion="grounded-plan-v3";
        public readonly PlanCache Cache=new PlanCache();
        public readonly DecisionBudget Budget;
        public readonly List<PlanningRecord> Records=new List<PlanningRecord>();
        public readonly List<string> Diagnostics=new List<string>();
        public long CooldownTicks=20;
        public int MaximumOffers=40;
        public double PerceptionRadius=18;
        public long Coalesced,StaleReplies,UnsupportedReplies,Fallbacks;
        public bool ReplayMode {get;private set;}
        public string GeneratorStatus {get {return ReplayMode?"recorded-response replay; network disabled":generator!=null&&generator.Available?"configured: "+generator.Version:"local fallback; generative model not configured";}}
        private readonly OperationCatalog catalog;
        private readonly IPlanProvider generator;
        private readonly IPlanEvaluator evaluator;
        private readonly SortedDictionary<string,PendingGoal> waiting=new SortedDictionary<string,PendingGoal>(StringComparer.Ordinal);
        private readonly Dictionary<string,long> lastRequest=new Dictionary<string,long>(StringComparer.Ordinal);
        private readonly Dictionary<string,string> signatures=new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly Queue<ActionPlan> ready=new Queue<ActionPlan>();
        private readonly Dictionary<string,PlanningRecord> replay=new Dictionary<string,PlanningRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> consumedReplay=new HashSet<string>(StringComparer.Ordinal);
        private Work work;
        private bool disposed;
        private string replayPolicy;
        private sealed class PendingGoal {public string Actor,Goal,Nav;}
        private sealed class Work
        {
            public PlanRequest Request;public string Stage;public ProviderReply Proposal;public Task<ProviderReply> Task;
            public CancellationTokenSource Cancellation;public DecisionBudget.Reservation Reservation;public PlanningRecord ReplayRecord;public long StartedTick;
        }
        public PlanningCoordinator(OperationCatalog catalog,IPlanProvider generator=null,IPlanEvaluator evaluator=null,DecisionBudget budget=null,IEnumerable<PlanningRecord> replayRecords=null)
        {
            this.catalog=catalog??throw new ArgumentNullException(nameof(catalog));this.generator=generator;this.evaluator=evaluator;Budget=budget??new DecisionBudget();
            if(replayRecords!=null)
            {
                ReplayMode=true;long ordinal=0;
                foreach(var source in replayRecords)
                {
                    var record=source==null?null:JsonConvert.DeserializeObject<PlanningRecord>(JsonConvert.SerializeObject(source));
                    if(record==null||record.RecordVersion!=PlanningRecord.Version||record.ReplyEncoding!=LocalTapeCodec.Encoding||record.RecordIndex!=++ordinal||record.RequestedTick<0||record.AppliedTick<record.RequestedTick||string.IsNullOrEmpty(record.RequestId)||string.IsNullOrEmpty(record.RequestJson)||string.IsNullOrEmpty(record.ReplyJson)||string.IsNullOrEmpty(record.PolicyVersion)||
                        !new[]{"generator","evaluator","generator_admission","evaluator_admission"}.Contains(record.Stage))throw new ArgumentException("Invalid or unordered planning record");
                    if(record.Stage.EndsWith("_admission",StringComparison.Ordinal)&&(record.AppliedTick!=record.RequestedTick||record.Disposition!="admitted"&&!(record.Disposition??"").StartsWith("deferred:",StringComparison.Ordinal)))throw new ArgumentException("Invalid planning admission");
                    replay.Add(RecordKey(record.Stage,record.RequestId),record);
                    if(replayPolicy==null)replayPolicy=record.PolicyVersion;else if(replayPolicy!=record.PolicyVersion)throw new ArgumentException("Replay policy changes need separate recordings");
                }
            }
        }
        private string Policy {get {return replayPolicy??new JArray("planning-policy-v3",generator?.Version??"local",evaluator?.Version??"no-evaluator").ToString(Formatting.None);}}
        static string RecordKey(string stage,string id)=>new JArray(stage,id).ToString(Formatting.None);
        static string GoalKey(string actor,string goal)=>new JArray(actor,goal).ToString(Formatting.None);
        public PlanRequest CreateRequest(World world,string actorId,string goal,string nav){return Build(world,actorId,goal,nav);}
        public bool TryDequeue(out ActionPlan plan){if(ready.Count>0){plan=ready.Dequeue();return true;}plan=null;return false;}
        public ActionPlan Request(World world,string actorId,string goal,string nav)
        {
            if(disposed)throw new ObjectDisposedException(nameof(PlanningCoordinator));
            if(!world.Entities.ContainsKey(actorId))throw new ArgumentException("Unknown actor");
            string key=GoalKey(actorId,goal);ActionPlan cached;string reason;
            if(Cache.TryGet(world,key,Policy,PromptVersion,OperationCatalog.Version,nav,out cached,out reason))return cached;
            string signature=MeaningfulSignature(world,actorId,key,nav);
            string previous;
            bool changed=reason!="cache_miss"||!signatures.TryGetValue(key,out previous)||previous!=signature;
            if(changed){signatures[key]=signature;waiting[key]=new PendingGoal {Actor=actorId,Goal=key,Nav=nav};}
            else Coalesced++;
            if(waiting.ContainsKey(key)&&(work!=null&&work.Request.Goal==key))Coalesced++;
            Fallbacks++;return LocalPlan(world,actorId,key,nav);
        }
        public void NotifyFailure(string actorId,string goal){signatures.Remove(GoalKey(actorId,goal));}
        public void Poll(World world,string nav)
        {
            if(disposed)return;
            if(work!=null)
            {
                if(work.ReplayRecord!=null)
                {
                    if(world.Tick<work.ReplayRecord.AppliedTick)return;
                    if(world.Tick!=work.ReplayRecord.AppliedTick)throw new InvalidOperationException("Replay missed recorded apply tick");
                    Complete(world,nav,ReadRecordedReply(work.ReplayRecord));
                }
                else if(work.Task.IsCompleted)Complete(world,nav,work.Task.GetAwaiter().GetResult());
            }
            if(work!=null||waiting.Count==0)return;
            foreach(var pair in waiting.ToArray())
            {
                long last;if(lastRequest.TryGetValue(pair.Key,out last)&&world.Tick-last<CooldownTicks)continue;
                if(!world.Entities.ContainsKey(pair.Value.Actor)){waiting.Remove(pair.Key);continue;}
                if(!ReplayMode&&(generator==null||!generator.Available)){waiting.Remove(pair.Key);lastRequest[pair.Key]=world.Tick;Diagnostics.Add("generator_absent:"+pair.Key);continue;}
                var request=Build(world,pair.Value.Actor,pair.Key,nav);
                if(Start(world,request,"generator",null))waiting.Remove(pair.Key);
                // Budget/affordance refusal preserves the goal and records a logical retry tick.
                lastRequest[pair.Key]=world.Tick;
                break;
            }
        }
        private bool Start(World world,PlanRequest request,string stage,ProviderReply proposal)
        {
            var next=new Work {Request=request,Stage=stage,Proposal=proposal,StartedTick=world.Tick};
            if(ReplayMode)
            {
                PlanningRecord admission;string admissionKey=RecordKey(stage+"_admission",request.Id);
                if(!replay.TryGetValue(admissionKey,out admission)||admission.RequestedTick!=world.Tick||admission.RequestJson!=request.Json())throw new InvalidOperationException("Missing or mismatched planning admission");
                AppendReplay(admission,admissionKey);
                if(admission.Disposition.StartsWith("deferred:",StringComparison.Ordinal)){Diagnostics.Add("recorded_"+admission.Disposition);return false;}
                PlanningRecord record;string key=RecordKey(stage,request.Id);
                if(!replay.TryGetValue(key,out record)||!consumedReplay.Add(key)||record.RequestedTick!=world.Tick||record.RequestJson!=request.Json())
                    throw new InvalidOperationException("Missing or mismatched recorded planning request: "+key);
                next.ReplayRecord=record;work=next;return true;
            }
            if(request.Offers.Count==0){Admission(world,request,stage,"deferred:no_affordances");return false;}
            if(double.IsNaN(Budget.TimeoutSeconds)||double.IsInfinity(Budget.TimeoutSeconds)||Budget.TimeoutSeconds<=0||Budget.TimeoutSeconds>300){Admission(world,request,stage,"deferred:invalid_deadline");return false;}
            long input,output;string reason;
            try{input=stage=="generator"?generator.EstimateInputTokens(request):evaluator.EstimateInputTokens(request,proposal);output=stage=="generator"?generator.MaximumOutputTokens:evaluator.MaximumOutputTokens;}
            catch(Exception){Admission(world,request,stage,"deferred:provider_estimation_failed");return false;}
            if(output<1){Admission(world,request,stage,"deferred:invalid_output_limit");return false;}
            if(!Budget.TryReserve(input,output,out next.Reservation,out reason)){Admission(world,request,stage,"deferred:"+reason);Diagnostics.Add("model_blocked:"+reason);return false;}
            Admission(world,request,stage,"admitted");
            next.Cancellation=new CancellationTokenSource();next.Cancellation.CancelAfter(TimeSpan.FromSeconds(Budget.TimeoutSeconds));
            Task<ProviderReply> call;
            try {call=stage=="generator"?generator.Generate(request,next.Cancellation.Token):evaluator.Evaluate(request,proposal,next.Cancellation.Token);}
            catch(Exception error){call=System.Threading.Tasks.Task.FromResult(new ProviderReply {RequestId=request.Id,Error=ModelHttp.SafeError(error)});}
            next.Task=WithDeadline(call,next.Cancellation,Budget.TimeoutSeconds,request.Id);work=next;return true;
        }
        private static async Task<ProviderReply> WithDeadline(Task<ProviderReply> call,CancellationTokenSource cancellation,double seconds,string id)
        {
            try
            {
                if(await Task.WhenAny(call,Task.Delay(TimeSpan.FromSeconds(seconds))).ConfigureAwait(false)!=call){cancellation.Cancel();return new ProviderReply {RequestId=id,Error="deadline_exceeded"};}
                var result=await call.ConfigureAwait(false);cancellation.Token.ThrowIfCancellationRequested();return result;
            }
            catch(Exception error){return new ProviderReply {RequestId=id,Error=ModelHttp.SafeError(error)};}
        }
        private void Complete(World world,string nav,ProviderReply reply)
        {
            var finished=work;work=null;
            if(finished.Reservation!=null)Budget.Settle(finished.Reservation,reply?.InputTokens,reply?.OutputTokens);
            finished.Cancellation?.Dispose();
            string disposition="";string reason;
            var request=finished.Request;
            if(reply==null||!string.IsNullOrEmpty(reply.Error))disposition="failed:"+(reply?.Error??"empty_reply");
            else if(reply.RequestId!=request.Id)disposition="wrong_request_id";
            else if(reply.Steps==null||reply.Steps.Count==0||reply.Steps.Count>6||reply.Steps.Any(s=>s==null||double.IsNaN(s.Amount)||double.IsInfinity(s.Amount)||s.Amount<=0||s.Amount>1||!request.Offers.Any(o=>o.operation==s.Operation&&o.actor_id==s.ActorId&&o.target_id==s.TargetId))){disposition="unsupported_reply";UnsupportedReplies++;}
            else if(Budget.Overrun&&!ReplayMode||ReplayMode&&finished.ReplayRecord.Disposition=="rejected_usage_overrun")disposition="rejected_usage_overrun";
            else if(!PlanCache.Validate(world,ActionPlan.Capture(request.Snapshot,request.Goal,Policy,PromptVersion,OperationCatalog.Version,request.NavVersion,reply.Steps),request.Goal,Policy,PromptVersion,OperationCatalog.Version,nav,out reason)){disposition="stale:"+reason;StaleReplies++;}
            else if(finished.Stage=="generator"&&(ReplayMode?replay.ContainsKey(RecordKey("evaluator_admission",request.Id)):evaluator!=null&&evaluator.Available))
            {
                disposition="generated_for_evaluation";
                Record(world,finished,reply,disposition);
                if(!Start(world,request,"evaluator",reply))Diagnostics.Add("evaluation_budget_exhausted; local fallback retained");
                return;
            }
            else if(!reply.Approved)disposition="evaluator_rejected";
            else
            {
                var plan=ActionPlan.Capture(world,request.Goal,Policy,PromptVersion,OperationCatalog.Version,nav,reply.Steps);
                Cache.Put(plan);ready.Enqueue(plan);disposition="accepted";
            }
            Record(world,finished,reply??new ProviderReply {RequestId=request.Id,Error="empty_reply"},disposition);
            Diagnostics.Add(disposition+":"+request.Id);
        }
        private void Record(World world,Work finished,ProviderReply reply,string disposition)
        {
            var record=new PlanningRecord {RecordIndex=Records.Count+1L,RequestId=finished.Request.Id,Stage=finished.Stage,RequestedTick=finished.StartedTick,AppliedTick=world.Tick,RequestJson=finished.Request.Json(),ReplyEncoding=LocalTapeCodec.Encoding,ReplyJson=EncodeReply(reply),Disposition=disposition,PolicyVersion=Policy};
            if(finished.ReplayRecord!=null){record.RequestedTick=finished.ReplayRecord.RequestedTick;if(record.Disposition!=finished.ReplayRecord.Disposition||record.RecordIndex!=finished.ReplayRecord.RecordIndex||record.ReplyJson!=finished.ReplayRecord.ReplyJson)throw new InvalidOperationException("Replay planning disposition/order/reply differs");}
            Records.Add(record);
        }
        void Admission(World world,PlanRequest request,string stage,string disposition)
        {Records.Add(new PlanningRecord{RecordIndex=Records.Count+1L,RequestId=request.Id,Stage=stage+"_admission",RequestedTick=world.Tick,AppliedTick=world.Tick,RequestJson=request.Json(),ReplyEncoding=LocalTapeCodec.Encoding,ReplyJson=EncodeReply(new ProviderReply{RequestId=request.Id,Approved=disposition=="admitted",Error=disposition=="admitted"?null:disposition}),Disposition=disposition,PolicyVersion=Policy});}
        static string EncodeReply(ProviderReply reply)=>LocalTapeCodec.Encode((JObject)LocalTapeCodec.FromObject(reply)).ToString(Formatting.None);
        public static ProviderReply ReadRecordedReply(PlanningRecord record)
        {
            if(record==null||record.RecordVersion!=PlanningRecord.Version||record.ReplyEncoding!=LocalTapeCodec.Encoding)throw new ArgumentException("recorded_reply_encoding_mismatch");
            return LocalTapeCodec.ToObject<ProviderReply>(LocalTapeCodec.Parse(record.ReplyJson));
        }
        void AppendReplay(PlanningRecord record,string key)
        {if(record.RecordIndex!=Records.Count+1L||!consumedReplay.Add(key))throw new InvalidOperationException("Replay planning record order differs");Records.Add(JsonConvert.DeserializeObject<PlanningRecord>(JsonConvert.SerializeObject(record)));}
        public void EnsureReplayConsumed()
        {if(ReplayMode&&(work!=null||consumedReplay.Count!=replay.Count))throw new InvalidOperationException("Replay has unapplied planning records");}
        private string MeaningfulSignature(World world,string actorId,string goal,string nav)
        {
            var actor=world.Entities[actorId];var bits=new JArray("planning-observation-v3",goal,nav,world.TopologyVersion,Policy,ActionPlan.Number(actor.Get("goal.kind")),ActionPlan.Number(actor.Get("goal.external")),ActionPlan.Number(actor.Get("extinguisher_charge")),ActionPlan.Number(actor.Get("communications.available")));
            foreach(var e in world.Entities.Values.Where(e=>e.Id==actorId||Visible(actor,e)).OrderBy(e=>e.Id,StringComparer.Ordinal))
                bits.Add(new JArray(e.Id,e.Get("fire")>0,e.Get("smoke")>.3,e.Get("health",1)<.7,ActionPlan.Number(e.Get("open")),ActionPlan.Number(e.Get("jammed")),ActionPlan.Number(e.Get("power.isolated")),ActionPlan.Number(e.Get("blocked")),ActionPlan.Number(e.Get("alarm.active"))));
            return Stable.Hash(bits.ToString(Formatting.None));
        }
        private bool Visible(Entity actor,Entity target){return target.Get("visibility",1)>0&&actor.Get("occlusion."+target.Id)<=0&&actor.Distance(target)<=actor.Get("sight.radius",Math.Min(12,PerceptionRadius));}
        private bool KnownDestination(Entity actor,Entity target){return actor.Links.Contains(target.Id)&&(target.Has("walkable")||target.Has("safe-zone")||target.Has("door")||target.Has("destination")||target.Has("exit")||target.Has("shelter"));}
        private PlanRequest Build(World world,string actorId,string goal,string nav)
        {
            var actor=world.Entities[actorId];var request=new PlanRequest {ActorId=actorId,Goal=goal,Tick=world.Tick,NavVersion=nav,Id=Stable.Hash(new JArray("plan-request-v3",world.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),world.Tick,actorId,goal,nav).ToString(Formatting.None)),Snapshot=Snapshot(world)};
            var known=world.Entities.Values.Where(e=>e.Id==actorId||Visible(actor,e)||KnownDestination(actor,e)).OrderBy(e=>actor.Distance(e)).ThenBy(e=>e.Id,StringComparer.Ordinal).Take(24).ToArray();
            foreach(var e in known)
                foreach(var op in catalog.Available(world,actorId,e.Id))
                {
                    if(request.Offers.Count>=MaximumOffers)break;
                    if(!Visible(actor,e)&&op.Name!="move_to")continue;
                    request.Offers.Add(new OfferedAction {operation=op.Name,actor_id=actorId,target_id=e.Id});
                }
            var visibleKeys=new HashSet<string>(new[]{"fire","smoke","open","jammed","locked","blocked","alarm.active","health","awareness","assisted","charge","power","power.isolated","extinguisher_charge","occupied","accessible","reachable","seated"},StringComparer.Ordinal);
            request.ContextJson=JsonConvert.SerializeObject(new {observation="Visible local observations and previously known destination coordinates. Position-based visibility approximation unless observer occlusion keys are supplied. Hidden destination states are excluded.",entities=known.Select(e=>new {id=e.Id,kind=e.Kind,x=e.X,y=e.Y,z=e.Z,tags=e.Tags,observed=e.Id==actorId||Visible(actor,e),state=e.State.Where(s=>(e.Id==actorId||Visible(actor,e))&&visibleKeys.Contains(s.Key)).ToDictionary(s=>s.Key,s=>s.Value)})});
            if(request.Offers.Count>0)request.Basis=ActionPlan.Capture(world,goal,Policy,PromptVersion,OperationCatalog.Version,nav,request.Offers.Select(o=>new ActionStep {Operation=o.operation,ActorId=o.actor_id,TargetId=o.target_id}));
            return request;
        }
        private static World Snapshot(World world)
        {
            var copy=new World {Seed=world.Seed,Tick=world.Tick,TopologyVersion=world.TopologyVersion,StepSeconds=world.StepSeconds,RateMultiplier=world.RateMultiplier};
            foreach(var source in world.Entities.Values){var entity=new Entity {Id=source.Id,Kind=source.Kind,ParentId=source.ParentId,X=source.X,Y=source.Y,Z=source.Z,Version=source.Version};entity.Tags.UnionWith(source.Tags);entity.Links.UnionWith(source.Links);foreach(var state in source.State)entity.State.Add(state.Key,state.Value);copy.Entities.Add(entity.Id,entity);}
            foreach(var field in world.Fields)copy.Fields.Add(field.Key,field.Value);return copy;
        }
        private ActionPlan LocalPlan(World world,string actorId,string goal,string nav)
        {
            var actor=world.Entities[actorId];ActionStep step=null;
            foreach(var e in world.Entities.Values.OrderBy(e=>actor.Distance(e)).ThenBy(e=>e.Id,StringComparer.Ordinal))
            {
                var op=Visible(actor,e)?catalog.Available(world,actorId,e.Id).FirstOrDefault(o=>o.Name=="assist"):null;
                if(op!=null){step=new ActionStep {Operation=op.Name,ActorId=actorId,TargetId=e.Id};break;}
            }
            if(step==null)
            {
                var target=world.Entities.Values.Where(e=>e.Id!=actorId&&(Visible(actor,e)||KnownDestination(actor,e))&&(!Visible(actor,e)||e.Get("blocked")==0)&&(e.Has("safe-zone")||e.Has("walkable"))).OrderBy(e=>actor.Distance(e)).ThenBy(e=>e.Id,StringComparer.Ordinal).FirstOrDefault();
                if(target==null)return null;
                step=new ActionStep {Operation="move_to",ActorId=actorId,TargetId=target.Id};
            }
            return ActionPlan.Capture(world,goal,"local-fallback-v1",PromptVersion,OperationCatalog.Version,nav,new[]{step});
        }
        public void Dispose(){disposed=true;if(work!=null){work.Cancellation?.Cancel();work.Cancellation?.Dispose();if(work.Reservation!=null)Budget.Settle(work.Reservation,null,null);work=null;}waiting.Clear();}
    }
}
