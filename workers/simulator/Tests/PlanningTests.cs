using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class PlanningTests
    {
        static void Check(bool ok,string reason){if(!ok)throw new Exception("Planning contract: "+reason);}
        static World Fixture()
        {
            var w=new World {Seed=42};
            var p=new Entity {Id="player",Kind="person"};p.Tags.UnionWith(new[]{"npc","staff","radio"});p.Set("health",1);p.Set("mobility",1);p.Set("communications.available",1);w.Add(p);
            var grid=new Entity {Id="grid",Kind="electrical",X=1};grid.Tags.Add("electrical");grid.Set("power",1);w.Add(grid);
            var supply=new Entity {Id="supply",Kind="power-source",X=2};supply.Tags.Add("power-source");supply.Links.Add("grid");supply.Set("power",1);w.Add(supply);
            var destination=new Entity {Id="exit",Kind="exit",X=5};destination.Tags.UnionWith(new[]{"walkable","safe-zone","exit"});w.Add(destination);p.Links.Add("exit");
            return w;
        }
        static ActionPlan Plan(World w,string operation="isolate_power",string target="grid")
        {return ActionPlan.Capture(w,"g","m","p",OperationCatalog.Version,"nav",new[]{new ActionStep {Operation=operation,ActorId="player",TargetId=target}});}
        static bool Valid(World w,ActionPlan p,out string reason){return PlanCache.Validate(w,p,p.Goal,p.ModelVersion,p.PromptVersion,p.ExecutorVersion,p.NavVersion,out reason);}
        sealed class FakeProvider:IPlanProvider
        {
            public int Calls;public bool Throw,Unsupported,NullSteps;public PlanRequest Last;public string Operation="isolate_power";public double Amount=1;
            public TaskCompletionSource<ProviderReply> Pending;
            public string Version {get{return "fixture-generator-v1";}}
            public bool Available {get{return true;}}
            public long MaximumOutputTokens {get{return 100;}}
            public long EstimateInputTokens(PlanRequest r){return 100;}
            public Task<ProviderReply> Generate(PlanRequest request,CancellationToken cancellation)
            {
                Calls++;Last=request;if(Throw)throw new Exception("fixture");if(Pending!=null)return Pending.Task;
                return Task.FromResult(Reply(request));
            }
            public ProviderReply Reply(PlanRequest request){return new ProviderReply {RequestId=request.Id,ReturnedModel="fixture-model",InputTokens=40,OutputTokens=10,Steps=NullSteps?null:new List<ActionStep>{new ActionStep{Operation=Unsupported?"invent_explosion":Operation,ActorId="player",TargetId="grid",Amount=Amount}}};}
        }
        sealed class FakeEvaluator:IPlanEvaluator
        {
            public int Calls;public bool Available {get{return true;}}public string Version {get{return "fixture-evaluator-v1";}}public long MaximumOutputTokens {get{return 50;}}
            public long EstimateInputTokens(PlanRequest r,ProviderReply p){return 100;}
            public Task<ProviderReply> Evaluate(PlanRequest r,ProviderReply p,CancellationToken c){Calls++;return Task.FromResult(new ProviderReply {RequestId=r.Id,ReturnedModel="fixture-jev",Steps=p.Steps,Approved=true,InputTokens=50,OutputTokens=4});}
        }
        static void Finish(PlanningCoordinator coordinator,World world){world.Tick++;coordinator.Poll(world,"nav");}
        public static void Run()
        {
            string reason;ActionPlan hit;var catalog=OperationCatalog.CreateDefault();var executor=new PlanExecutor(catalog);
            var w=Fixture();var p=Plan(w);var cache=new PlanCache();cache.Put(p);
            Check(cache.TryGet(w,"g","m","p",OperationCatalog.Version,"nav",out hit,out reason),"initial cache hit");
            w.Entities["player"].Set("memory.age",9);w.Entities["grid"].Set("temperature",34);w.Entities["player"].X=.2;
            Check(Valid(w,p,out reason),"irrelevant thermal/memory drift and expected actor movement do not invalidate");
            w.Entities["supply"].Set("power.isolated",1);Check(!Valid(w,p,out reason),"linked power dependency invalidates");
            w=Fixture();p=Plan(w);w.Entities["grid"].X=10;Check(!Valid(w,p,out reason),"moved target invalidates");
            w=Fixture();p=Plan(w);w.Entities["grid"].Set("removed",1);Check(!Valid(w,p,out reason),"logical removal invalidates");
            w=Fixture();p=Plan(w);w.Entities.Remove("grid");Check(!Valid(w,p,out reason),"deleted dependency invalidates even without topology bump");
            w=Fixture();p=Plan(w);w.TopologyVersion++;Check(!Valid(w,p,out reason),"topology invalidates");
            w=Fixture();p=Plan(w);Check(!PlanCache.Validate(w,p,"different","m","p",OperationCatalog.Version,"nav",out reason),"goal invalidates");
            Check(!PlanCache.Validate(w,p,"g","m2","p",OperationCatalog.Version,"nav",out reason),"model version invalidates");
            Check(!PlanCache.Validate(w,p,"g","m","p2",OperationCatalog.Version,"nav",out reason),"prompt version invalidates");
            Check(!PlanCache.Validate(w,p,"g","m","p",OperationCatalog.Version,"nav2",out reason),"nav geometry invalidates");
            Check(executor.TryExecute(w,p,0,"nav",out reason)&&w.Entities["grid"].Get("power.isolated")==1&&w.Entities["grid"].Get("power")==0,"isolation has causal world effects");
            var oneTag=new Entity{Id="collision",Kind="electrical"};oneTag.Tags.Add("a;b");
            var twoTags=new Entity{Id="collision",Kind="electrical"};twoTags.Tags.UnionWith(new[]{"a","b"});
            Check(ActionPlan.Geometry(oneTag)!=ActionPlan.Geometry(twoTags),"canonical dependency hash distinguishes tag delimiter collisions");
            oneTag.Tags.Clear();twoTags.Tags.Clear();oneTag.Links.Add("a;b");twoTags.Links.UnionWith(new[]{"a","b"});
            Check(ActionPlan.Geometry(oneTag)!=ActionPlan.Geometry(twoTags),"canonical dependency hash distinguishes link delimiter collisions");
            oneTag.Links.Clear();twoTags.Links.Clear();oneTag.State["x;y"]=1;twoTags.State["x"]=1;twoTags.State["y"]=1;
            Check(ActionPlan.StateFingerprint(oneTag,new[]{"x;y"})!=ActionPlan.StateFingerprint(twoTags,new[]{"x","y"}),"canonical state keys never alias merged delimiters");
            Check(ActionPlan.StateFingerprint(oneTag,new[]{"missing"})!=ActionPlan.StateFingerprint(twoTags,new[]{"x"}),"absent read state cannot alias an existing numeric state");
            Check(w.Journal.Any(t=>t.Property=="power"&&t.Before==1&&t.After==0),"actual property before/after journal");
            w=Fixture();p=Plan(w,"move_to","exit");Check(executor.TryExecute(w,p,0,"nav",out reason)&&reason=="movement_goal_started"&&!executor.IsComplete(w,p,0),"goal assignment is not arrival");
            w.Entities["player"].X=5;Check(executor.IsComplete(w,p,0),"arrival checked geometrically");
            MovementCompositions(catalog);
            DependencyScopeAndSeats(catalog);
            w=Fixture();p=Plan(w);w.Entities["player"].Tags.Remove("staff");Check(!executor.TryExecute(w,p,0,"nav",out reason),"actor authorization rechecked");
            w=Fixture();p=Plan(w,"invent_explosion");Check(!executor.TryExecute(w,p,0,"nav",out reason),"unsupported effects refused");
            var b=new DecisionBudget {MaxRequests=2,MaxInputTokens=200,MaxOutputTokens=80,MaxConcurrent=1};DecisionBudget.Reservation ticket;
            Check(b.TryReserve(100,40,out ticket,out reason),"reservation admits budget");DecisionBudget.Reservation other;
            Check(!b.TryReserve(1,1,out other,out reason)&&reason=="concurrency_limit","concurrency limit");b.Settle(ticket,60,10);
            Check(b.MeasuredInputTokens==60&&b.EstimatedInputTokens==100&&b.ChargedInputTokens==60,"measurements separate from estimates");
            Check(!b.TryReserve(141,1,out other,out reason)&&reason=="input_token_budget","hard input reservation");
            Check(!b.TryReserve(1,71,out other,out reason)&&reason=="output_token_budget","hard output reservation");
            Check(b.TryReserve(100,40,out ticket,out reason),"second reservation");b.Settle(ticket,null,null);Check(b.MeasuredInputTokens==60&&b.ChargedInputTokens==160,"unknown failed usage charged conservatively");
            Check(!b.TryReserve(1,1,out other,out reason)&&reason=="request_budget","session request cap");
            b=new DecisionBudget();b.TryReserve(2,2,out ticket,out reason);b.Settle(ticket,3,1);Check(b.Overrun&&!b.TryReserve(1,1,out other,out reason),"provider overrun circuit closes");
            b=new DecisionBudget();b.TryReserve(2,2,out ticket,out reason);b.Settle(ticket,-1,1);Check(b.Overrun&&b.InFlight==0&&b.MeasuredInputTokens==0&&b.ChargedInputTokens==2,"negative provider usage refuses accounting without reservation leak");
            b=new DecisionBudget{MaxConcurrent=2,MaxInputTokens=1};b.TryReserve(1,1,out ticket,out reason);
            Check(!b.TryReserve(long.MaxValue,1,out other,out reason)&&reason=="input_token_budget"&&b.Requests==1,"reservation addition cannot overflow and bypass input cap");
            b.Settle(ticket,1,1);bool doubleRelease=false;try{b.Settle(ticket,1,1);}catch(InvalidOperationException){doubleRelease=true;}
            Check(doubleRelease&&b.InFlight==0&&b.ReservedInputTokens==0&&b.ChargedInputTokens==1,"double settlement never releases or charges twice");
            b=new DecisionBudget{MaxRequests=2,MaxConcurrent=2,MaxInputTokens=long.MaxValue,MaxOutputTokens=long.MaxValue};
            b.TryReserve(1,1,out ticket,out reason);b.TryReserve(1,1,out other,out reason);b.Settle(ticket,long.MaxValue,long.MaxValue);b.Settle(other,1,1);
            Check(b.Overrun&&b.InFlight==0&&b.ReservedInputTokens==0&&b.ReservedOutputTokens==0&&b.MeasuredInputTokens==long.MaxValue&&b.ChargedInputTokens==long.MaxValue&&!b.TryReserve(0,0,out ticket,out reason),"settlement overflow saturates, closes reservations and blocks future upstream work");
            b=new DecisionBudget();var foreignBudget=new DecisionBudget();b.TryReserve(1,1,out ticket,out reason);bool foreignRefused=false;try{foreignBudget.Settle(ticket,1,1);}catch(InvalidOperationException){foreignRefused=true;}
            Check(foreignRefused&&b.InFlight==1&&foreignBudget.InFlight==0,"foreign reservation cannot be settled by another budget");b.Settle(ticket,1,1);

            w=Fixture();var fake=new FakeProvider();var evaluator=new FakeEvaluator();var coordinator=new PlanningCoordinator(catalog,fake,evaluator);
            Check(coordinator.Request(w,"player","respond","nav")!=null,"local fallback immediate");coordinator.Poll(w,"nav");
            Finish(coordinator,w);Finish(coordinator,w);ActionPlan generated;
            Check(coordinator.TryDequeue(out generated)&&fake.Calls==1&&evaluator.Calls==1,"generated composition + independent JEV evaluation accepted");
            Check(coordinator.Budget.MeasuredInputTokens==90&&coordinator.Records.Count==4,"both stages admissions and actual replies recorded");
            var tape=JsonConvert.DeserializeObject<List<PlanningRecord>>(JsonConvert.SerializeObject(coordinator.Records));
            var replayWorld=Fixture();var replayCoordinator=new PlanningCoordinator(catalog,replayRecords:tape);
            replayCoordinator.Request(replayWorld,"player","respond","nav");replayCoordinator.Poll(replayWorld,"nav");Finish(replayCoordinator,replayWorld);Finish(replayCoordinator,replayWorld);
            ActionPlan replayed;Check(replayCoordinator.TryDequeue(out replayed)&&replayed.Id==generated.Id&&replayCoordinator.Budget.Requests==0,"two-stage replay recovers policy with no configured providers and network0");

            w=Fixture();fake=new FakeProvider();coordinator=new PlanningCoordinator(catalog,fake){CooldownTicks=0};coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");Finish(coordinator,w);
            w.Entities["grid"].X+=.1;coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");Finish(coordinator,w);
            Check(fake.Calls==2&&coordinator.Cache.Invalidations==1,"invalidated geometry regenerates even when hazard signature is unchanged");

            w=Fixture();fake=new FakeProvider {Unsupported=true};coordinator=new PlanningCoordinator(catalog,fake);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");Finish(coordinator,w);
            Check(coordinator.UnsupportedReplies==1&&!coordinator.TryDequeue(out generated),"generator cannot invent operations");
            w=Fixture();fake=new FakeProvider {NullSteps=true};coordinator=new PlanningCoordinator(catalog,fake);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");Finish(coordinator,w);Check(coordinator.UnsupportedReplies==1,"null model steps rejected");
            w=Fixture();fake=new FakeProvider();coordinator=new PlanningCoordinator(catalog,fake);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");w.Entities["grid"].Set("power.isolated",1);Finish(coordinator,w);Check(coordinator.StaleReplies==1,"stale response rejected");
            w=Fixture();fake=new FakeProvider();coordinator=new PlanningCoordinator(catalog,fake);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");w.Entities["player"].Set("memory.age",1);Finish(coordinator,w);Check(coordinator.TryDequeue(out generated),"irrelevant drift does not reject async reply");
            w=Fixture();var bystander=new Entity {Id="bystander",Kind="person",X=3};bystander.Tags.Add("npc");w.Add(bystander);fake=new FakeProvider();coordinator=new PlanningCoordinator(catalog,fake);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");bystander.X=4;Finish(coordinator,w);Check(coordinator.TryDequeue(out generated),"unselected moving NPC does not invalidate generated static-object operation");
            w=Fixture();fake=new FakeProvider {Unsupported=true};coordinator=new PlanningCoordinator(catalog,fake);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");Finish(coordinator,w);
            for(int i=0;i<100;i++){w.Tick++;coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");}Check(fake.Calls==1,"unchanged world never triggers periodic model churn");
            w=Fixture();fake=new FakeProvider {Throw=true};coordinator=new PlanningCoordinator(catalog,fake);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");Finish(coordinator,w);Check(coordinator.Budget.InFlight==0&&coordinator.Records.Single(r=>r.Stage=="generator").Disposition.StartsWith("failed:"),"synchronous provider throw settles reservation");
            w=Fixture();fake=new FakeProvider {Pending=new TaskCompletionSource<ProviderReply>()};b=new DecisionBudget {TimeoutSeconds=.02};coordinator=new PlanningCoordinator(catalog,fake,budget:b);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");Thread.Sleep(60);Finish(coordinator,w);Check(b.InFlight==0&&coordinator.Records.Single(r=>r.Stage=="generator").Disposition.Contains("deadline"),"uncooperative provider timeout releases concurrency conservatively");

            w=Fixture();var hidden=new Entity {Id="hidden",X=1,Kind="object"};hidden.Tags.Add("combustible");hidden.Set("fire",1);w.Add(hidden);w.Entities["player"].Set("occlusion.hidden",1);
            var remote=new Entity {Id="remote",X=100,Kind="exit"};remote.Tags.UnionWith(new[]{"walkable","safe-zone"});remote.Set("fire",1);w.Add(remote);w.Entities["player"].Links.Add("remote");
            fake=new FakeProvider();coordinator=new PlanningCoordinator(catalog,fake);coordinator.Request(w,"player","respond","nav");coordinator.Poll(w,"nav");var context=JObject.Parse(fake.Last.ContextJson);
            Check(!context["entities"].Any(e=>(string)e["id"]=="hidden"),"occluded local entity excluded");
            var remoteJson=context["entities"].First(e=>(string)e["id"]=="remote");Check(!remoteJson["state"].HasValues,"known remote destination reveals no hidden hazards");
            ProviderFixtures();
            AdmissionReplayFixtures(catalog);
            ExactRecordedAmount(catalog);
        }
        static void ExactRecordedAmount(OperationCatalog catalog)
        {
            double amount=BitConverter.Int64BitsToDouble(unchecked((long)0x3fc278e93a3bfa76UL));var live=Fixture();live.Entities["grid"].Set("fire",1);live.Entities["player"].Set("extinguisher_charge",1);
            var fake=new FakeProvider{Operation="suppress_fire",Amount=amount};using(var coordinator=new PlanningCoordinator(catalog,fake))
            {
                coordinator.Request(live,"player","respond","nav");coordinator.Poll(live,"nav");live.Tick++;coordinator.Poll(live,"nav");ActionPlan plan;
                Check(coordinator.TryDequeue(out plan)&&BitConverter.DoubleToInt64Bits(plan.Steps[0].Amount)==BitConverter.DoubleToInt64Bits(amount),"live model plan keeps the parsed exact one-ULP amount in (0,1]");
                var record=coordinator.Records.Single(r=>r.Stage=="generator");var saved=LocalTapeCodec.ToObject<ProviderReply>(LocalTapeCodec.Parse(record.ReplyJson));
                Check(record.RecordVersion==PlanningRecord.Version&&record.ReplyEncoding==LocalTapeCodec.Encoding&&BitConverter.DoubleToInt64Bits(saved.Steps[0].Amount)==BitConverter.DoubleToInt64Bits(amount),"inner recorded reply is typed exact data, not lossy normal model JSON");
                string reason;Check(new PlanExecutor(catalog).TryExecute(live,plan,0,"nav",out reason),"live amount applies a grounded actual suppression effect");
                var replayWorld=Fixture();replayWorld.Entities["grid"].Set("fire",1);replayWorld.Entities["player"].Set("extinguisher_charge",1);
                var records=JsonConvert.DeserializeObject<List<PlanningRecord>>(JsonConvert.SerializeObject(coordinator.Records));using(var replay=new PlanningCoordinator(catalog,replayRecords:records))
                {
                    replay.Request(replayWorld,"player","respond","nav");replay.Poll(replayWorld,"nav");replayWorld.Tick++;replay.Poll(replayWorld,"nav");ActionPlan repeated;
                    Check(replay.TryDequeue(out repeated)&&BitConverter.DoubleToInt64Bits(repeated.Steps[0].Amount)==BitConverter.DoubleToInt64Bits(amount),"actual planning replay restores one-ULP operation amount");
                    Check(new PlanExecutor(catalog).TryExecute(replayWorld,repeated,0,"nav",out reason)&&replayWorld.Digest()==live.Digest()&&replay.Budget.Requests==0&&fake.Calls==1,"recorded amount produces exact same action effects/digest with zero new model calls");replay.EnsureReplayConsumed();
                }
                records[0].RecordVersion="planning-record-v2";bool legacyRefused=false;try{new PlanningCoordinator(catalog,replayRecords:records);}catch(ArgumentException){legacyRefused=true;}Check(legacyRefused,"old lossy inner reply versions are refused rather than relabeled or recovered");
            }
        }
        static void AdmissionReplayFixtures(OperationCatalog catalog)
        {
            var world=Fixture();var fake=new FakeProvider();var budget=new DecisionBudget{MaxRequests=0};
            using(var coordinator=new PlanningCoordinator(catalog,fake,budget:budget))
            {
                coordinator.Request(world,"player","respond","nav");coordinator.Poll(world,"nav");
                Check(fake.Calls==0&&budget.InFlight==0&&coordinator.Records.Count==1&&coordinator.Records[0].Disposition=="deferred:request_budget","blocked admission records local deferral before any provider call");
                world.Tick=20;budget.MaxRequests=1;coordinator.Poll(world,"nav");world.Tick++;coordinator.Poll(world,"nav");ActionPlan accepted=null;
                Check(fake.Calls==1&&coordinator.TryDequeue(out accepted)&&coordinator.Records.Count==3,"budget-blocked goal persists and retries after cooldown without another Request");
                var records=JsonConvert.DeserializeObject<List<PlanningRecord>>(JsonConvert.SerializeObject(coordinator.Records));
                var replayWorld=Fixture();using(var replay=new PlanningCoordinator(catalog,replayRecords:records))
                {
                    replay.Request(replayWorld,"player","respond","nav");replay.Poll(replayWorld,"nav");replayWorld.Tick=20;replay.Poll(replayWorld,"nav");replayWorld.Tick++;replay.Poll(replayWorld,"nav");ActionPlan repeated;
                    Check(replay.TryDequeue(out repeated)&&repeated.Id==accepted.Id&&replay.Budget.Requests==0&&fake.Calls==1,"recorded budget deferral and admission tick replay with zero provider calls");replay.EnsureReplayConsumed();
                }
                records.Reverse();bool orderRefused=false;try{new PlanningCoordinator(catalog,replayRecords:records);}catch(ArgumentException){orderRefused=true;}Check(orderRefused,"reordered planning ledger refuses before playback");records.Reverse();
                using(var incomplete=new PlanningCoordinator(catalog,replayRecords:records)){bool refused=false;try{incomplete.EnsureReplayConsumed();}catch(InvalidOperationException){refused=true;}Check(refused,"replay completion refuses unconsumed external action records");}
                records[1].RequestedTick++;bool invalidTick=false;try{new PlanningCoordinator(catalog,replayRecords:records);}catch(ArgumentException){invalidTick=true;}Check(invalidTick,"inconsistent logical admission tick refuses");
            }
            world=Fixture();var other=new Entity{Id="player|x",Kind="person"};other.Tags.UnionWith(new[]{"npc","staff"});other.Set("health",1);other.Set("mobility",1);world.Add(other);
            using(var local=new PlanningCoordinator(catalog)){var one=local.Request(world,"player","x|y","nav");var two=local.Request(world,"player|x","y","nav");Check(one.Goal!=two.Goal,"actor and goal delimiter collisions do not share cache or request slots");}
            world=Fixture();fake=new FakeProvider();budget=new DecisionBudget{TimeoutSeconds=double.NaN};using(var invalid=new PlanningCoordinator(catalog,fake,budget:budget)){invalid.Request(world,"player","respond","nav");invalid.Poll(world,"nav");Check(fake.Calls==0&&budget.InFlight==0&&invalid.Records.Single().Disposition=="deferred:invalid_deadline","invalid deadline refuses before reservation and provider transport");}
            world=Fixture();fake=new FakeProvider{Pending=new TaskCompletionSource<ProviderReply>()};using(var delayed=new PlanningCoordinator(catalog,fake))
            {
                delayed.Request(world,"player","respond","nav");delayed.Poll(world,"nav");for(int i=1;i<=5;i++){world.Tick=i;delayed.Poll(world,"nav");}
                Check(delayed.Records.Count==1&&delayed.Records[0].RequestedTick==0&&delayed.Records[0].AppliedTick==0,"pending provider completion cannot manufacture a simulation apply tick");
                fake.Pending.SetResult(fake.Reply(fake.Last));Thread.Sleep(10);world.Tick=6;delayed.Poll(world,"nav");var response=delayed.Records.Single(r=>r.Stage=="generator");
                Check(response.RequestedTick==0&&response.AppliedTick==6&&response.RecordIndex==2,"async callback becomes an applied model result only on the subsequent logical Poll tick");
                var records=JsonConvert.DeserializeObject<List<PlanningRecord>>(JsonConvert.SerializeObject(delayed.Records));var playback=Fixture();using(var replay=new PlanningCoordinator(catalog,replayRecords:records))
                {replay.Request(playback,"player","respond","nav");replay.Poll(playback,"nav");for(int i=1;i<=6;i++){playback.Tick=i;replay.Poll(playback,"nav");}replay.EnsureReplayConsumed();Check(replay.Records.Last().AppliedTick==6&&replay.Budget.Requests==0&&fake.Calls==1,"recorded async apply tick replays exactly without repeating the source callback or model request");}
            }
        }
        static void MovementCompositions(OperationCatalog catalog)
        {
            string reason;var w=Fixture();w.Entities["player"].X=-4;var executor=new PlanExecutor(catalog);
            var plan=ActionPlan.Capture(w,"operate-after-walking","m","p",OperationCatalog.Version,"nav",new[]{new ActionStep{Operation="move_to",ActorId="player",TargetId="grid"},new ActionStep{Operation="isolate_power",ActorId="player",TargetId="grid"}});
            Check(executor.TryExecute(w,plan,0,"nav",out reason),"composition starts physical movement");w.Entities["player"].X=1;new Community().Step(w);
            Check(executor.AcknowledgeMovementCompletion(w,plan,0,"nav",out reason),"movement completion handshake: "+reason);
            Check(executor.TryExecute(w,plan,1,"nav",out reason)&&w.Entities["grid"].Get("power.isolated")==1,"move to power operation survives actual Community arrival and visit update: "+reason);
            w=Fixture();w.Entities["player"].X=-4;executor=new PlanExecutor(catalog);plan=Plan(w,"move_to","grid");executor.TryExecute(w,plan,0,"nav",out reason);w.Entities["player"].X=1;new Community().Step(w);w.Entities["player"].Set("health",.8);
            Check(!executor.AcknowledgeMovementCompletion(w,plan,0,"nav",out reason),"arrival handshake refuses actor health drift");
            w=Fixture();w.Entities["player"].X=-4;executor=new PlanExecutor(catalog);plan=Plan(w,"move_to","grid");executor.TryExecute(w,plan,0,"nav",out reason);w.Entities["player"].X=1;new Community().Step(w);w.Entities["supply"].Set("power.isolated",1);
            Check(!executor.AcknowledgeMovementCompletion(w,plan,0,"nav",out reason),"arrival handshake refuses changed linked power");
            w=Fixture();w.Entities["player"].X=-4;executor=new PlanExecutor(catalog);plan=Plan(w,"move_to","grid");executor.TryExecute(w,plan,0,"nav",out reason);w.Entities["player"].X=1;new Community().Step(w);
            Check(!executor.AcknowledgeMovementCompletion(w,plan,0,"nav-changed",out reason),"arrival handshake refuses changed navigation");
            w=Fixture();var patient=new Entity{Id="patient",Kind="person",X=5};patient.Tags.Add("npc");patient.Set("health",.6);patient.Set("medical.condition",.3);w.Add(patient);executor=new PlanExecutor(catalog);
            plan=ActionPlan.Capture(w,"walk-and-assist","m","p",OperationCatalog.Version,"nav",new[]{new ActionStep{Operation="move_to",ActorId="player",TargetId="patient"},new ActionStep{Operation="assist",ActorId="player",TargetId="patient"}});
            executor.TryExecute(w,plan,0,"nav",out reason);w.Entities["player"].X=5;new Community().Step(w);
            Check(executor.AcknowledgeMovementCompletion(w,plan,0,"nav",out reason)&&executor.TryExecute(w,plan,1,"nav",out reason)&&patient.Get("assisted")==1&&patient.Get("medical.condition")<.3,"move to assist composition changes patient's actual medical state");
        }
        static void DependencyScopeAndSeats(OperationCatalog catalog)
        {
            string reason;var w=Fixture();var actor=w.Entities["player"];
            var far=new Entity{Id="far",Kind="person",X=100};far.Tags.Add("npc");far.Links.Add("grid");w.Add(far);
            var seat=new Entity{Id="seat",Kind="seat",X=100,ParentId="exit"};seat.Tags.UnionWith(new[]{"seat","place"});seat.Links.Add("grid");w.Add(seat);actor.Links.Add("seat");
            var p=Plan(w);far.Set("health",.2);seat.Set("occupied",1);seat.X=101;
            Check(Valid(w,p,out reason)&&!p.Dependencies.ContainsKey("seat")&&!p.Dependencies.ContainsKey("far"),"unrelated linked seat/NPC changes excluded from operation dependencies");
            w=Fixture();var parent=new Entity{Id="car",Kind="car"};parent.Tags.Add("train");w.Add(parent);w.Entities["grid"].ParentId="car";
            var access=new Entity{Id="access",Kind="door"};access.Tags.Add("door");access.Set("open",1);w.Add(access);w.Entities["grid"].Links.Add("access");
            var sibling=new Entity{Id="other-door",Kind="door"};sibling.Tags.Add("door");w.Add(sibling);parent.Links.Add("other-door");
            p=Plan(w);sibling.Set("open",1);Check(Valid(w,p,out reason)&&!p.Dependencies.ContainsKey(sibling.Id),"selected target parent does not traverse sibling access graph");
            access.Set("locked",1);Check(!Valid(w,p,out reason),"actual access door invalidates");
            p=Plan(w);parent.X=1;Check(!Valid(w,p,out reason),"selected target parent pose invalidates");
            w=Fixture();actor=w.Entities["player"];seat=new Entity{Id="seat",Kind="seat",X=4};seat.Tags.Add("seat");seat.Set("destination.x",.5);seat.Set("accessible",1);seat.Set("reachable",1);w.Add(seat);
            var executor=new PlanExecutor(catalog);p=Plan(w,"move_to","seat");
            Check(executor.TryExecute(w,p,0,"nav",out reason)&&actor.Get("goal.x")==.5,"seat movement uses source aisle approach rather than furniture center");
            actor.X=.5;p=Plan(w,"sit","seat");Check(executor.TryExecute(w,p,0,"nav",out reason)&&actor.Get("seated")==1&&seat.Get("occupant.player")==1,"sit acquires unique occupancy at aisle");
            var other=new Entity{Id="other",Kind="person",X=.5};other.Tags.Add("npc");other.Set("health",1);other.Set("mobility",1);w.Add(other);
            Check(!catalog.Available(w,"other","seat").Any(o=>o.Name=="sit"||o.Name=="stand"),"another actor cannot sit in occupied seat or release it");
            Check(!catalog.Available(w,"player","exit").Any(o=>o.Name=="move_to"),"seated actor must stand before movement");
            p=Plan(w,"stand","seat");Check(executor.TryExecute(w,p,0,"nav",out reason)&&seat.Get("occupied")==0&&seat.Get("occupant.player")==0&&actor.Get("seated")==0,"stand releases owned seat");
            p=Plan(w,"move_to","seat");seat.Set("destination.x",2);Check(!Valid(w,p,out reason),"changed seat approach invalidates movement");
        }
        static void ProviderFixtures()
        {
            string sentEndpoint=null,sentBody=null;var request=new PlanRequest {Id="fixture",ActorId="player",Goal="g",ContextJson="{}"};
            ModelTransport transport=(endpoint,key,body,cancellation)=>{sentEndpoint=endpoint;sentBody=body;return Task.FromResult(new ModelHttpReply {Status=200,Body="{\"model\":\"local-fixture\",\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":7},\"choices\":[{\"message\":{\"content\":\"{\\\"steps\\\":[{\\\"operation\\\":\\\"move_to\\\",\\\"actor_id\\\":\\\"player\\\",\\\"target_id\\\":\\\"exit\\\"}]}\"}}]}"});};
            var generator=new OpenAiCompatibleGenerator("http://127.0.0.1:11434/v1/chat/completions","fixture",transport:transport);
            var reply=generator.Generate(request,CancellationToken.None).GetAwaiter().GetResult();Check(reply.Error==null&&reply.InputTokens==11&&reply.Steps[0].Operation=="move_to","OpenAI-compatible generator fixture parsed");Check((long)JObject.Parse(sentBody)["max_tokens"]==512,"generator output reservation enforced in request");
            transport=(endpoint,key,body,cancellation)=>{sentEndpoint=endpoint;return Task.FromResult(new ModelHttpReply {Status=200,Body="{\"model\":\"jev-fixture\",\"usage\":{\"input_tokens\":12,\"output_tokens\":3},\"answers\":{\"plan\":{\"choice\":\"execute\"}}}"});};
            var evaluator=new TypeSafeEvaluator("fixture-not-a-real-key",transport:transport);var judged=evaluator.Evaluate(request,reply,CancellationToken.None).GetAwaiter().GetResult();Check(judged.Approved&&judged.InputTokens==12&&sentEndpoint==TypeSafeEvaluator.Endpoint,"TypeSafe endpoint and structured response fixture");
        }
    }
}
