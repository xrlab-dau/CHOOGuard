using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class EmergenceGenerationTests
    {
        static void Check(bool value,string reason){if(!value)throw new Exception("Initial emergence contract: "+reason);}
        static World Fixture()
        {
            var w=new World{Seed=42};w.Fields["environment.autonomous"]=0;w.Fields["ambient.temperature"]=20;w.Fields["rain"]=0;w.Fields["wind"]=0;
            var power=new Entity{Id="supply",Kind="cabinet"};power.Tags.UnionWith(new[]{"semantic","power-source","electrical","thermal","self-powered"});power.Set("supply.available",1);power.Set("power",1);power.Set("insulation",1);power.Set("wear",0);power.Set("temperature",20);w.Add(power);
            var bag=new Entity{Id="bag",Kind="luggage",X=.5};bag.Tags.UnionWith(new[]{"semantic","combustible"});bag.Set("fuel",1);bag.Set("temperature",20);w.Add(bag);
            var prop=new Entity{Id="visual",Kind="window",ParentId="supply"};prop.Tags.UnionWith(new[]{"decoration","render-only"});w.Add(prop);
            return w;
        }
        static EmergenceChange C(string id,string property,double value){return new EmergenceChange{EntityId=id,Property=property,Value=value};}
        sealed class Provider:IEmergenceProvider
        {
            public int Calls;public bool Enabled=true,Throw,WrongSeed,WrongBasis;public long Input=80,Output=20;public List<EmergenceChange> Changes=new List<EmergenceChange>();
            public TaskCompletionSource<EmergenceReply> Pending;public EmergenceRequest Request;
            public string Version{get{return "FIXTURE-only-initial-generator";}}public bool Available{get{return Enabled;}}public long MaximumOutputTokens{get{return 512;}}
            public long EstimateInputTokens(EmergenceRequest r){return 100;}
            public EmergenceReply Reply(EmergenceRequest r){return new EmergenceReply{BasisDigest=WrongBasis?"wrong":r.BasisDigest,Seed=WrongSeed?r.Seed+1:r.Seed,Changes=Changes,Raw="FIXTURE RESPONSE, not live inference",ReturnedModel="fixture",InputTokens=Input,OutputTokens=Output};}
            public Task<EmergenceReply> Generate(EmergenceRequest r,CancellationToken c){Calls++;Request=r;if(Throw)throw new InvalidOperationException("fixture");return Pending?.Task??Task.FromResult(Reply(r));}
        }
        static EmergenceRecord Apply(World world,List<EmergenceChange> changes)
        {return new EmergenceComposer(new Provider{Changes=changes}).GenerateAndApplyAsync(world,false,authorized:true).GetAwaiter().GetResult();}
        static void AtomicNegative(List<EmergenceChange> changes,string label)
        {
            var w=Fixture();string before=w.Digest();var record=Apply(w,changes);
            Check(record.Disposition.StartsWith("rejected_")&&w.Digest()==before,label+" rejected without partial mutation");
        }
        public static void Run()
        {
            var w=Fixture();var provider=new Provider{Changes=new List<EmergenceChange>{C("supply","insulation",.2)}};var composer=new EmergenceComposer(provider);string before=w.Digest();
            var rejected=composer.GenerateAndApplyAsync(w,false).GetAwaiter().GetResult();Check(rejected.Disposition=="blocked_not_authorized"&&provider.Calls==0&&w.Digest()==before,"no consent makes zero network calls");
            rejected=composer.GenerateAndApplyAsync(w,true,authorized:true).GetAwaiter().GetResult();Check(provider.Calls==0&&w.Digest()==before,"running session refuses generation");
            var accepted=composer.GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(accepted.Disposition=="accepted"&&w.Entities["supply"].Get("insulation")==.2&&accepted.PreDigest==before&&accepted.PostDigest==w.Digest(),"typed upstream condition applied with pre/post record");
            Check(composer.Budget.MeasuredInputTokens==80&&composer.Budget.MeasuredOutputTokens==20&&accepted.RawReply.Contains("FIXTURE")&&accepted.RequestJson!=null,"actual usage separate from estimates and raw transcript retained");
            rejected=composer.GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(rejected.Disposition=="blocked_session_request_cap"&&provider.Calls==1,"maximum one request per training session");
            w=Fixture();w.Tick=1;provider=new Provider();rejected=new EmergenceComposer(provider).GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(provider.Calls==0,"noninitial tick refuses generation");
            w=Fixture();provider=new Provider{Enabled=false};rejected=new EmergenceComposer(provider).GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(provider.Calls==0&&rejected.Disposition=="blocked_provider_unavailable","unconfigured endpoint refuses call");
            w=Fixture();provider=new Provider{Changes=new List<EmergenceChange>()};accepted=new EmergenceComposer(provider).GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(accepted.Disposition=="accepted_no_change","quiescent model output allowed");
            AtomicNegative(new List<EmergenceChange>{C("supply","insulation",.1),C("bag","fire",1)},"direct fire consequence");
            foreach(string property in new[]{"fault","health","injury","failed","open","load","scenario_id"})AtomicNegative(new List<EmergenceChange>{C("supply",property,1)},"unsupported field "+property);
            AtomicNegative(new List<EmergenceChange>{C("unknown","temperature",40)},"invented object");AtomicNegative(new List<EmergenceChange>{C("visual","temperature",40)},"decorative target");
            AtomicNegative(new List<EmergenceChange>{C("bag","insulation",.2)},"untyped property");AtomicNegative(new List<EmergenceChange>{C("supply","temperature",200)},"out of range temperature");
            AtomicNegative(new List<EmergenceChange>{C("supply","insulation",double.NaN)},"nonfinite value");
            AtomicNegative(new List<EmergenceChange>{C("supply","insulation",.1),C("supply","insulation",.2)},"duplicate field");
            AtomicNegative(Enumerable.Range(0,17).Select(i=>C("supply","insulation",.1)).ToList(),"many changes");
            foreach(bool wrongSeed in new[]{true,false})
            {w=Fixture();before=w.Digest();provider=new Provider{WrongSeed=wrongSeed,WrongBasis=!wrongSeed,Changes=new List<EmergenceChange>{C("supply","wetness",.9)}};rejected=new EmergenceComposer(provider).GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(rejected.Disposition=="rejected_basis_or_seed"&&w.Digest()==before,"response basis/seed contract");}
            AsyncGuards();TransportFixtures();TrajectoryAndReplay();FullWorldRequestSizes();CompactGraphContracts();
        }
        static void AsyncGuards()
        {
            var w=Fixture();string before=w.Digest();var p=new Provider{Throw=true};var composer=new EmergenceComposer(p);
            var record=composer.GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(record.Disposition.StartsWith("failed_")&&w.Digest()==before&&composer.Budget.InFlight==0,"network failure settles and leaves initial world unchanged");
            w=Fixture();before=w.Digest();p=new Provider{Pending=new TaskCompletionSource<EmergenceReply>()};var budget=new DecisionBudget{TimeoutSeconds=.02};composer=new EmergenceComposer(p,budget);
            record=composer.GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(record.Disposition.Contains("deadline")&&w.Digest()==before&&budget.InFlight==0,"uncooperative provider deadline settles without mutation");
            w=Fixture();before=w.Digest();p=new Provider{Pending=new TaskCompletionSource<EmergenceReply>()};composer=new EmergenceComposer(p);var cancel=new CancellationTokenSource();
            var task=composer.GenerateAndApplyAsync(w,false,cancellation:cancel.Token,authorized:true);cancel.Cancel();record=task.GetAwaiter().GetResult();Check(record.Disposition=="cancelled"&&w.Digest()==before&&composer.Budget.InFlight==0,"explicit cancellation cannot apply changes");
            w=Fixture();p=new Provider{Pending=new TaskCompletionSource<EmergenceReply>(),Changes=new List<EmergenceChange>{C("supply","insulation",.1)}};composer=new EmergenceComposer(p);task=composer.GenerateAndApplyAsync(w,false,authorized:true);
            w.Entities["bag"].Set("fuel",.8);before=w.Digest();p.Pending.SetResult(p.Reply(p.Request));record=task.GetAwaiter().GetResult();Check(record.Disposition=="rejected_stale_basis_or_seed"&&w.Digest()==before,"compare exact digest immediately before apply");
            w=Fixture();before=w.Digest();bool running=false;p=new Provider{Pending=new TaskCompletionSource<EmergenceReply>(),Changes=new List<EmergenceChange>{C("supply","insulation",.1)}};composer=new EmergenceComposer(p){IsSessionRunning=()=>running};task=composer.GenerateAndApplyAsync(w,false,authorized:true);running=true;p.Pending.SetResult(p.Reply(p.Request));record=task.GetAwaiter().GetResult();Check(w.Digest()==before&&record.Disposition=="rejected_session_not_stopped_initial","late session start prevents apply");
            w=Fixture();p=new Provider();budget=new DecisionBudget{MaxInputTokens=99};composer=new EmergenceComposer(p,budget);record=composer.GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(p.Calls==0&&record.Disposition=="blocked_input_token_budget","honest oversized request blocks, never truncates graph");
            w=Fixture();before=w.Digest();p=new Provider{Output=513,Changes=new List<EmergenceChange>{C("supply","insulation",.1)}};composer=new EmergenceComposer(p);record=composer.GenerateAndApplyAsync(w,false,authorized:true).GetAwaiter().GetResult();Check(record.Disposition=="rejected_usage_overrun"&&w.Digest()==before&&composer.Budget.InFlight==0,"actual usage overrun closes circuit before apply");
        }
        static void TransportFixtures()
        {
            string sent=null;var request=EmergenceComposer.CreateRequest(Fixture());
            ModelTransport transport=(url,key,body,cancel)=>{sent=body;return Task.FromResult(new ModelHttpReply{Status=200,Body=JsonConvert.SerializeObject(new{model="FIXTURE-only",usage=new{prompt_tokens=33,completion_tokens=22},choices=new[]{new{message=new{content=JsonConvert.SerializeObject(new{basis_digest=request.BasisDigest,seed=request.Seed.ToString(),changes=new[]{new{entity_id="supply",property="wetness",value=.7}}})}}}})});};
            var p=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","fixture",transport:transport);var reply=p.Generate(request,CancellationToken.None).GetAwaiter().GetResult();
            Check(reply.Error==null&&reply.InputTokens==33&&reply.OutputTokens==22&&reply.Changes[0].Property=="wetness","OpenAI transport actual-usage/initial-schema fixture parsed");
            var body=JObject.Parse(sent);Check((long)body["max_tokens"]==512&&(string)body["chooguard_purpose"]=="initial_world"&&body["messages"][0]["content"].ToString().Contains("UNTRUSTED DATA"),"dedicated initial allowance, output bound and graph-instruction boundary serialized");
            transport=(u,k,b,c)=>Task.FromResult(new ModelHttpReply{Status=200,Body=JsonConvert.SerializeObject(new{usage=new{prompt_tokens=7,completion_tokens=4},choices=new[]{new{message=new{content="{\"basis_digest\":\"x\",\"seed\":\"42\",\"scenario_id\":1,\"changes\":[]}"}}}})});
            p=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","fixture",transport:transport);reply=p.Generate(request,CancellationToken.None).GetAwaiter().GetResult();Check(reply.Error=="invalid_initial_condition_json"&&reply.InputTokens==7,"scenario fields rejected while reported usage retained");
            transport=(u,k,b,c)=>Task.FromResult(new ModelHttpReply{Status=403,Body="{\"error\":{\"code\":\"initial_not_armed\",\"message\":\"private-library-message\"}}"});
            p=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","fixture",transport:transport);reply=p.Generate(request,CancellationToken.None).GetAwaiter().GetResult();Check(reply.Error=="generator_http_403:initial_not_armed"&&!reply.Error.Contains("private"),"safe actionable helper refusal excludes arbitrary error message");
            transport=(u,k,b,c)=>Task.FromResult(new ModelHttpReply{Status=403,Body="{\"error\":{\"code\":\"unsafe\\ncode\",\"message\":\"private-library-message\"}}"});
            p=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","fixture",transport:transport);reply=p.Generate(request,CancellationToken.None).GetAwaiter().GetResult();Check(reply.Error=="generator_http_403","non-ASCII-envelope-safe code refused without exposing raw error");
        }
        static void TrajectoryAndReplay()
        {
            var baseline=Fixture();var generated=Fixture();var initial=Apply(generated,new List<EmergenceChange>{C("supply","temperature",80),C("supply","cooling",.01),C("supply","wetness",.8),C("supply","insulation",0),C("@world","rain",.8)});
            Check(initial.Disposition=="accepted","causal initial conditions accepted");string reason;var replay=Fixture();
            Check(EmergenceComposer.ValidateAndApplyRecorded(replay,initial,false,out reason)&&replay.Digest()==generated.Digest(),"recorded model reply reproduces exact applied initial world: "+reason);
            var altered=JsonConvert.DeserializeObject<EmergenceRecord>(JsonConvert.SerializeObject(initial));altered.PostDigest="incorrect";var untouched=Fixture();string before=untouched.Digest();Check(!EmergenceComposer.ValidateAndApplyRecorded(untouched,altered,false,out reason)&&untouched.Digest()==before,"wrong recorded postdigest refused before replay mutation");
            var kernel=new CausalKernel();bool supplyTrajectoryWarmer=false;for(int i=0;i<40;i++){kernel.Step(generated);kernel.Step(replay);kernel.Step(baseline);supplyTrajectoryWarmer|=generated.Entities["supply"].Get("temperature")>baseline.Entities["supply"].Get("temperature");Check(generated.Digest()==replay.Digest(),"applied model reply causal resimulation tick "+i);}
            Check(generated.Entities["bag"].Get("temperature")>baseline.Entities["bag"].Get("temperature")&&supplyTrajectoryWarmer,"upstream initial conditions change source trajectory and downstream receiver: generated bag="+generated.Entities["bag"].Get("temperature")+", baseline bag="+baseline.Entities["bag"].Get("temperature")+", generated supply="+generated.Entities["supply"].Get("temperature")+", baseline supply="+baseline.Entities["supply"].Get("temperature"));
            Check(generated.Fields["rain"]>0&&generated.Fields["rain"]<.8,"initial rain persists and follows kernel recession");
            int faults=0,isolatedFaults=0;
            for(ulong seed=1;seed<=64;seed++)
            {
                var wet=Fixture();wet.Seed=seed;var isolated=Fixture();isolated.Seed=seed;
                var changes=new List<EmergenceChange>{C("supply","wetness",1),C("supply","drainage",0),C("supply","insulation",0),C("supply","wear",1),C("@world","rate_multiplier",4)};
                Apply(wet,changes);Apply(isolated,changes);isolated.Entities["supply"].Set("power.isolated",1);
                for(int t=0;t<40;t++){kernel.Step(wet);kernel.Step(isolated);}if(wet.Entities["supply"].Get("fault")>0)faults++;if(isolated.Entities["supply"].Get("fault")>0)isolatedFaults++;
            }
            Check(faults>0&&isolatedFaults==0,"AI upstream insulation/moisture alters seeded fault outcomes; actual isolation counterfactual removes them");
            Console.WriteLine("Initial-condition causal fixtures: powered faults "+faults+"/64, isolated "+isolatedFaults+"/64 after40ticks; game parameters, not observed frequencies.");
        }
        static void FullWorldRequestSizes()
        {
            string path="workers/simulator/data/platform-source.json",coveragePath="workers/simulator/data/semantic-coverage.json";
            if(!File.Exists(path)||!File.Exists(coveragePath))throw new Exception("Real source fixtures required for request size verification");
            string source=File.ReadAllText(path),coverageJson=File.ReadAllText(coveragePath);var manifest=WorldCoverage.Parse(coverageJson);var w=PlatformWorldFactory.Create(source,20261013);WorldCoverage.Apply(w,coverageJson);
            var p=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","fixture");var r=EmergenceComposer.CreateRequest(w,manifest);long first=p.EstimateInputTokens(r);
            Check((int)JObject.Parse(r.Json)["graph"]["entity_count"]==w.Entities.Count&&first<=new EmergenceComposer(p).Budget.MaxInputTokens,"entire real source graph fits bounded initial budget");
            int count=475;for(int i=0;i<count;i++){var e=new Entity{Id="render:source-mesh:"+i.ToString("D4"),Kind="source-visual",ParentId="train:ktx",X=1+i*.001};e.Tags.UnionWith(new[]{"render-only","decoration"});w.Add(e);}
            r=EmergenceComposer.CreateRequest(w,manifest);long second=p.EstimateInputTokens(r);var graph=EmergenceGraphCodec.Decode((JObject)JObject.Parse(r.Json)["graph"]);
            Check((int)graph["entity_count"]==w.Entities.Count&&(int)graph["visual_count"]==manifest.visual_parts.Length&&second<=new EmergenceComposer(p).Budget.MaxInputTokens,"~961 state plus full visual identity mapping fits without omitted graph entities");
            Check(graph["entities"].Count()==w.Entities.Count&&graph["entities"].Any(e=>(string)e[0]=="render:source-mesh:0474"),"last decoration identity and mapping retained");
            Console.WriteLine("Initial emergence graph: "+(w.Entities.Count-count)+" source entities reserve "+first+"; "+w.Entities.Count+" incl475render identities reserve "+second+" conservative input tokens; default cap256000, maxonecall/512output/60s; fixtures only.");
        }
        static void CompactGraphContracts()
        {
            string source=File.ReadAllText("workers/simulator/data/platform-source.json"),coverageJson=File.ReadAllText("workers/simulator/data/semantic-coverage.json");var coverage=WorldCoverage.Parse(coverageJson);
            var world=PlatformWorldFactory.Create(source,coverageJson,20261013);
            foreach(string id in new[]{"render:한글😀첫째","render:한글😁둘째"}){var e=new Entity{Id=id,Kind="source-visual",ParentId="train:ktx",X=-2.1234567,Y=0,Z=.0001};e.Tags.Add("decoration");world.Add(e);}
            var legacy=JObject.Parse(EmergenceComposer.CreateRequest(world,coverage,false).Json);var compact=JObject.Parse(EmergenceComposer.CreateRequest(world,coverage).Json);var graph=(JObject)compact["graph"];
            Check(JToken.DeepEquals(legacy["graph"],EmergenceGraphCodec.Decode(graph)),"every original identity, parent, link, coordinate, state schema/value, operation and visual mapping roundtrips");
            Check(EmergenceComposer.CreateRequest(world,coverage).Json==compact.ToString(Formatting.None),"encoding order deterministic");
            Check(compact.ToString(Formatting.None).Length<legacy.ToString(Formatting.None).Length,"compact full graph reduces actual serialized context");
            var ids=EmergenceGraphCodec.Identities(graph);int target=Array.IndexOf(ids,"power:platform");
            Check(target>=0&&EmergenceGraphCodec.ResolveEntity(graph,new JObject{["entity_ref"]=target})=="power:platform","numeric reference bound to exact source identity");
            Action<JObject> negative=bad=>{bool refused=false;try{EmergenceGraphCodec.Decode(bad);}catch(ArgumentException){refused=true;}catch(OverflowException){refused=true;}Check(refused,"malformed compact graph refused");};
            foreach(int test in Enumerable.Range(0,8))
            {
                var bad=(JObject)graph.DeepClone();
                switch(test)
                {
                    case 0:bad["identities"][0][0]=1;break;
                    case 1:bad["identities"][1]=bad["identities"][0].DeepClone();break;
                    case 2:bad["entity_groups"][0][1][0]=-1;break;
                    case 3:bad["entity_groups"][0][1][0]=ids.Length;break;
                    case 4:bad["entity_groups"][0][2][0]=new JArray();break;
                    case 5:bad["numeric_scale"]=1000;break;
                    case 6:bad["entity_count"]=(int)bad["entity_count"]-1;break;
                    case 7:bad["link_sets"][0]=new JArray(ids.Length);break;
                }
                negative(bad);
            }
            foreach(var change in new[]{new JObject{["entity_ref"]=-1},new JObject{["entity_ref"]=ids.Length},new JObject{["entity_ref"]=target,["entity_id"]="power:platform"},new JObject{["entity_ref"]="1"}})
            {bool refused=false;try{EmergenceGraphCodec.ResolveEntity(graph,change);}catch(ArgumentException){refused=true;}Check(refused,"ambiguous/out of range/noninteger proposal ref refused");}
            var request=EmergenceComposer.CreateRequest(world,coverage);
            ModelTransport transport=(u,k,b,c)=>Task.FromResult(new ModelHttpReply{Status=200,Body=JsonConvert.SerializeObject(new{model="fixture",choices=new[]{new{message=new{content=JsonConvert.SerializeObject(new{basis_digest=request.BasisDigest,seed=request.Seed.ToString(),changes=new[]{new{entity_ref=target,property="wetness",value=.6}}})}}}})});
            var provider=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","fixture",transport:transport);
            var record=new EmergenceComposer(provider).GenerateAndApplyAsync(world,false,coverage,authorized:true).GetAwaiter().GetResult();
            Check(record.Disposition=="accepted"&&record.Changes[0].EntityId=="power:platform"&&world.Entities["power:platform"].Get("wetness")==.6,"actual provider parser resolves reference before typed atomic apply");
            foreach(double value in new[]{10000000001d,1e100,-1e100})
            {
                var large=Fixture();large.Entities["supply"].Set("large-source-state",value);
                var raw=JObject.Parse(EmergenceComposer.CreateRequest(large,compact:false).Json);var encoded=JObject.Parse(EmergenceComposer.CreateRequest(large).Json);
                Check(JToken.DeepEquals(raw["graph"],EmergenceGraphCodec.Decode((JObject)encoded["graph"])),"large finite source state retained through raw numeric column");
            }
            foreach(string parent in new[]{"",new string('x',8193)})
            {
                var unusual=Fixture();unusual.Entities["bag"].ParentId=parent;
                var raw=JObject.Parse(EmergenceComposer.CreateRequest(unusual,compact:false).Json);var encoded=JObject.Parse(EmergenceComposer.CreateRequest(unusual).Json);
                Check(encoded["graph"]["encoding"]==null&&JToken.DeepEquals(raw["graph"],encoded["graph"]),"unusual source identity preserved in legacy fallback");
            }
            var empty=JObject.Parse(EmergenceComposer.CreateRequest(new World()).Json);
            Check((int)EmergenceGraphCodec.Decode((JObject)empty["graph"])["entity_count"]==0,"empty world graph remains representable");
            var atomic=Fixture();var atomicRequest=EmergenceComposer.CreateRequest(atomic);string unchanged=atomic.Digest();
            int supply=Array.IndexOf(EmergenceGraphCodec.Identities((JObject)JObject.Parse(atomicRequest.Json)["graph"]),"supply");
            transport=(u,k,b,c)=>Task.FromResult(new ModelHttpReply{Status=200,Body=JsonConvert.SerializeObject(new{choices=new[]{new{message=new{content=JsonConvert.SerializeObject(new{basis_digest=atomicRequest.BasisDigest,seed=atomicRequest.Seed.ToString(),changes=new[]{new{entity_ref=supply,property="wetness",value=.6},new{entity_ref=-1,property="wetness",value=.6}}})}}}})});
            provider=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","fixture",transport:transport);
            record=new EmergenceComposer(provider).GenerateAndApplyAsync(atomic,false,authorized:true).GetAwaiter().GetResult();
            Check(record.Disposition=="failed_invalid_initial_condition_json"&&atomic.Digest()==unchanged,"late malformed model reference cannot partially apply preceding valid change");
        }
    }
}
