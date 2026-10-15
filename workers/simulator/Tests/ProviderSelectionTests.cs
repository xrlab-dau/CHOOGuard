using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class ProviderSelectionTests
    {
        static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("Provider selection: "+reason);}
        static JObject Choice(string provider="alpha",string account="account-1",string generation="session-1")
        {return new JObject{["provider_id"]=provider,["account_id"]=account,["model_id"]="org/shared-model:revision",["session_generation"]=generation,["catalog_version"]=new string('a',64),["selection_version"]=ProviderSelection.Version,["billing"]="user_subscription",["registration"]="own_app_public_oauth"};}
        static JObject Registry(string id)
        {return new JObject{["id"]=id,["registered"]=true,["billing"]="user_subscription",["auth_modes"]=new JArray("browser_oauth"),["capabilities"]=new JArray("initial_world","npc_plan")};}
        static JObject Status(JObject choice)
        {return new JObject{["provider"]=choice["provider_id"].DeepClone(),["model"]=choice["model_id"].DeepClone(),["session_generation"]=choice["session_generation"].DeepClone(),["connected"]=true,["cloud_enabled"]=true,["account_label"]="private-email@example.test",["selection"]=choice.DeepClone(),["providers"]=new JArray(Registry("alpha"),Registry("beta")),["inference_contracts"]=new JObject{["alpha"]=new string('a',64),["beta"]=new string('b',64)},["usage"]=new JObject{["requests"]=0,["input_tokens"]=0,["output_tokens"]=0}};}
        static JObject Descriptor()
        {return new JObject{["schema"]=1,["endpoint"]="http://127.0.0.1:18953",["capability"]=new string('c',48),["browser_url"]="http://127.0.0.1:18953/#"+new string('b',48)};}
        sealed class Helper
        {
            public JObject Current=Status(Choice());public readonly List<JObject> Posts=new List<JObject>();
            public bool RemoteChanged,InvalidProposal;public TaskCompletionSource<ModelHttpReply> Pending;
            public Task<ModelHttpReply> Send(string method,string url,string capability,string generation,string body,CancellationToken cancellation)
            {
                Check(capability==new string('c',48),"native uses app capability without OAuth token");
                if(method=="GET")return Task.FromResult(new ModelHttpReply{Status=200,Body=Current.ToString()});
                Check(url=="http://127.0.0.1:18953/v1/chat/completions","fixed local helper only");
                var request=JObject.Parse(body);Posts.Add(request);
                Check(request["chooguard_selection"] is JObject,"both request types carry a complete tuple");
                Check(generation==(string)request["chooguard_selection"]["session_generation"],"header and body generation match");
                if(RemoteChanged)return Task.FromResult(new ModelHttpReply{Status=409,Body="{\"error\":{\"code\":\"provider_session_changed\"}}"});
                if(Pending!=null)return Pending.Task;
                string content;
                if(InvalidProposal)content="{\"steps\":[{\"operation\":\"invent_incident\",\"actor_id\":\"player\",\"target_id\":\"grid\"}]}";
                else if((string)request["chooguard_purpose"]=="initial_world")
                {var input=JObject.Parse((string)request["messages"][1]["content"]);content=new JObject{["basis_digest"]=input["basis_digest"],["seed"]=input["seed"],["changes"]=new JArray()}.ToString(Formatting.None);}
                else content="{\"steps\":[{\"operation\":\"isolate_power\",\"actor_id\":\"player\",\"target_id\":\"grid\"}]}";
                return Task.FromResult(Completion(content,(string)request["model"]));
            }
        }
        static ModelHttpReply Completion(string content,string model)
        {return new ModelHttpReply{Status=200,Body=new JObject{["model"]=model,["usage"]=new JObject{["prompt_tokens"]=40,["completion_tokens"]=10},["choices"]=new JArray(new JObject{["message"]=new JObject{["content"]=content}})}.ToString()};}
        static SubscriptionConnection Connect(Helper helper){return SubscriptionConnection.ParseDescriptor(Descriptor().ToString(),helper.Send);}
        static World World()
        {
            var world=new World{Seed=42};var actor=new Entity{Id="player",Kind="person"};actor.Tags.UnionWith(new[]{"npc","staff","radio"});actor.Set("health",1);actor.Set("mobility",1);actor.Set("communications.available",1);world.Add(actor);
            var grid=new Entity{Id="grid",Kind="electrical",X=1};grid.Tags.Add("electrical");grid.Set("power",1);world.Add(grid);return world;
        }
        static SubscriptionStatus Refresh(SubscriptionConnection connection){return connection.Refresh(CancellationToken.None).GetAwaiter().GetResult();}
        static void Refuse(JObject choice)
        {bool refused=false;try{ProviderSelection.Parse(choice);}catch(ArgumentException){refused=true;}Check(refused,"invalid or paid-key selection refused");}
        public static void Run()
        {
            var choice=Choice();var immutable=ProviderSelection.Parse(choice);choice["account_id"]="replacement";Check(immutable.AccountId=="account-1","selection detached from mutable status JSON");
            var alpha=ProviderSelection.Parse(Choice());var beta=ProviderSelection.Parse(Choice("beta"));var another=ProviderSelection.Parse(Choice("alpha","account-2"));
            Check(alpha.ModelId==beta.ModelId&&alpha.CacheVersion!=beta.CacheVersion&&alpha.CacheVersion!=another.CacheVersion,"same model ID cannot share cache across provider or account");
            var changedCatalog=Choice();changedCatalog["catalog_version"]=new string('b',64);Check(alpha.CacheVersion!=ProviderSelection.Parse(changedCatalog).CacheVersion,"catalog revision invalidates model cache");
            var renewed=ProviderSelection.Parse(Choice(generation:"session-2"));
            Check(alpha.CacheVersion==renewed.CacheVersion&&alpha.SameLogicalBinding(renewed)&&!alpha.SameAs(renewed),"logical cache identity survives a new auth lease; routing lease remains exact and distinct");
            Check(alpha.LogicalFingerprint!=beta.LogicalFingerprint&&alpha.LogicalFingerprint!=another.LogicalFingerprint,"logical identity never crosses provider/account");
            var binding=alpha.ToBindingRecord();Check(!binding.ToString().Contains("account-1")&&!binding.ToString().Contains("session-1"),"material logical binding records contain neither account handle nor volatile lease");
            Check(JToken.DeepEquals(binding,ProviderSelection.ValidateBindingRecord(binding)),"strict logical provenance roundtrip");
            var invalidBinding=(JObject)binding.DeepClone();invalidBinding["access_token"]="forbidden";bool invalidBindingRefused=false;try{ProviderSelection.ValidateBindingRecord(invalidBinding);}catch(ArgumentException){invalidBindingRefused=true;}Check(invalidBindingRefused,"logical provenance rejects credentials and extra fields");
            var record=alpha.ToRecord().ToString();Check(!record.Contains("account-1")&&!record.Contains("private-email")&&!record.Contains(new string('c',48)),"routing records redact account handle/email and app capability");
            Check(JToken.DeepEquals(alpha.ToRecord(),ProviderSelection.ValidateRecord(JObject.Parse(record))),"safe recorded routing validates without an account credential or network");
            var invalidRecord=alpha.ToRecord();invalidRecord["account_id"]="private-account-handle";bool recordRefused=false;try{ProviderSelection.ValidateRecord(invalidRecord);}catch(ArgumentException){recordRefused=true;}Check(recordRefused,"recording cannot include extra private account handles");
            choice=Choice();choice["billing"]="api_key";Refuse(choice);choice=Choice();choice["registration"]="existing_cli_credentials";Refuse(choice);choice=Choice();choice["extra"]="token";Refuse(choice);choice=Choice();choice["model_id"]="model\r\nAuthorization: x";Refuse(choice);

            var helper=new Helper();var connection=Connect(helper);var status=Refresh(connection);Check(status.Ready,"registered arbitrary provider can be ready");var session=connection.LockSelection(status);
            var initial=session.CreateEmergenceProvider();var world=World();var request=EmergenceComposer.CreateRequest(world);var reply=initial.Generate(request,CancellationToken.None).GetAwaiter().GetResult();
            Check(reply.Error==null&&reply.Changes.Count==0&&reply.InputTokens==40,"initial subscription response parsed with guarded condition schema");
            var generator=session.CreateGenerator(128);var planRequest=new PlanRequest{Id="request",ActorId="player",Goal="respond",ContextJson="{}"};var planReply=generator.Generate(planRequest,CancellationToken.None).GetAwaiter().GetResult();
            Check(planReply.Error==null&&planReply.Steps.Count==1&&helper.Posts.Count==2,"NPC and initial model route are both available");
            Check(JToken.DeepEquals(helper.Posts[0]["chooguard_selection"],helper.Posts[1]["chooguard_selection"])&&JToken.DeepEquals(helper.Posts[0]["chooguard_selection"],session.Selection.ToToken()),"exact same immutable tuple used for initial and NPC");
            Check(initial.Version==generator.Version+":initial_world"&&!generator.Version.Contains(new string('c',48)),"routing policy contains no credentials");
            var bound=connection.CreateTransport(status);bool keyRefused=false;try{bound(connection.Endpoint+"/v1/chat/completions","developer-key-must-not-be-used",new JObject{["model"]=session.Selection.ModelId}.ToString(),CancellationToken.None).GetAwaiter().GetResult();}catch(ArgumentException){keyRefused=true;}
            bool endpointRefused=false;try{bound("https://api.example.invalid/v1/chat/completions",null,"{}",CancellationToken.None).GetAwaiter().GetResult();}catch(ArgumentException){endpointRefused=true;}
            Check(keyRefused&&endpointRefused&&helper.Posts.Count==2,"subscription transport refuses developer key and remote endpoint without any inference post");
            helper.Current=Status(Choice("beta"));status=Refresh(connection);Check(status.Ready&&!session.IsCurrent,"browser selection change invalidates old locked session");
            int before=helper.Posts.Count;planReply=generator.Generate(planRequest,CancellationToken.None).GetAwaiter().GetResult();reply=initial.Generate(request,CancellationToken.None).GetAwaiter().GetResult();
            Check(planReply.Error=="generator_http_409"&&reply.Error.StartsWith("generator_http_409")&&helper.Posts.Count==before,"stale initial and NPC tuples make zero extra helper inference posts");
            bool oldRefused=false;try{connection.LockSelection(new SubscriptionStatus{Connected=true,CloudEnabled=true,Provider="openai-chatgpt",Model="m",SessionGeneration="g"});}catch(InvalidOperationException){oldRefused=true;}Check(oldRefused,"legacy missing selection cannot silently infer");
            helper.Current=Status(Choice());helper.Current["providers"][0]["registered"]=false;Check(!Refresh(connection).Ready,"unregistered provider cannot infer");
            helper.Current=Status(Choice());helper.Current["providers"][0]["capabilities"]=new JArray("initial_world");Check(!Refresh(connection).Ready,"selection must support both initial and NPC use");
            helper.Current=Status(Choice());helper.Current["providers"][0]["billing"]="api_key";Check(!Refresh(connection).Ready,"developer key registry entry cannot masquerade as subscription");
            helper.Current=Status(Choice());helper.Current.Remove("inference_contracts");var oldHelper=Refresh(connection);Check(!oldHelper.Ready&&oldHelper.Error=="subscription_helper_upgrade_required","missing backend contract requires explicit helper upgrade before inference");
            foreach(var badHash in new JToken[]{new JValue("tiny"),new JValue(new string('A',64)),new JValue(123),JValue.CreateNull()})
            {helper.Current=Status(Choice());helper.Current["inference_contracts"]["alpha"]=badHash;Check(!Refresh(connection).Ready,"malformed public backend contract cannot authorize inference");}
            helper.Current=Status(Choice());Refresh(connection);helper.RemoteChanged=true;var remoteSession=connection.LockSelection(Refresh(connection));planReply=remoteSession.CreateGenerator().Generate(planRequest,CancellationToken.None).GetAwaiter().GetResult();Check(planReply.Error=="generator_http_409"&&!remoteSession.IsCurrent,"remote selection change invalidates local lock without account or API fallback");
            before=helper.Posts.Count;remoteSession.CreateGenerator().Generate(planRequest,CancellationToken.None).GetAwaiter().GetResult();Check(helper.Posts.Count==before,"after remote409 the invalidated selection makes no further helper post");
            LateSelectionAndReplay();
            CancelledBoundTransport();
        }
        static void CancelledBoundTransport()
        {
            var helper=new Helper();var connection=Connect(helper);var session=connection.LockSelection(Refresh(connection));
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.Cancel();var blocked=session.CreateGenerator().Generate(new PlanRequest{Id="cancelled",ContextJson="{}"},cancellation.Token).GetAwaiter().GetResult();
                Check(blocked.Error=="timeout_or_cancelled"&&helper.Posts.Count==0,"cancelled inference is refused before native transport and uses no provider attempt");
            }
            helper.Pending=new TaskCompletionSource<ModelHttpReply>();
            using(var cancellation=new CancellationTokenSource())
            {
                var pending=session.CreateGenerator().Generate(new PlanRequest{Id="late-cancel",ContextJson="{}"},cancellation.Token);cancellation.Cancel();helper.Pending.SetResult(Completion("{\"steps\":[{\"operation\":\"isolate_power\",\"actor_id\":\"player\",\"target_id\":\"grid\"}]}",session.Selection.ModelId));
                Check(pending.GetAwaiter().GetResult().Error=="timeout_or_cancelled"&&helper.Posts.Count==1,"uncooperative native transport late200 is discarded after cancellation without any fallback call");
            }
        }
        static void LateSelectionAndReplay()
        {
            var helper=new Helper();var connection=Connect(helper);var session=connection.LockSelection(Refresh(connection));helper.Pending=new TaskCompletionSource<ModelHttpReply>();
            var pending=session.CreateGenerator().Generate(new PlanRequest{Id="late",ContextJson="{}"},CancellationToken.None);
            helper.Current=Status(Choice(generation:"session-2"));Refresh(connection);helper.Pending.SetResult(Completion("{\"steps\":[{\"operation\":\"isolate_power\",\"actor_id\":\"player\",\"target_id\":\"grid\"}]}","org/shared-model:revision"));
            Check(pending.GetAwaiter().GetResult().Error=="generator_http_409","late reply from replaced browser selection discarded");
            helper=new Helper();connection=Connect(helper);session=connection.LockSelection(Refresh(connection));var world=World();var catalog=OperationCatalog.CreateDefault();
            using(var coordinator=new PlanningCoordinator(catalog,session.CreateGenerator()))
            {
                coordinator.Request(world,"player","respond","nav");coordinator.Poll(world,"nav");world.Tick++;coordinator.Poll(world,"nav");ActionPlan generated;
                Check(coordinator.TryDequeue(out generated)&&coordinator.Records.Count==2&&coordinator.Records[0].Stage=="generator_admission"&&coordinator.Budget.MeasuredInputTokens==40,"selected model NPC admission and plan recorded, tokens measured separately from movement");
                int posts=helper.Posts.Count;var records=JsonConvert.DeserializeObject<List<PlanningRecord>>(JsonConvert.SerializeObject(coordinator.Records));var replayWorld=World();
                using(var replay=new PlanningCoordinator(catalog,replayRecords:records))
                {replay.Request(replayWorld,"player","respond","nav");replay.Poll(replayWorld,"nav");replayWorld.Tick++;replay.Poll(replayWorld,"nav");ActionPlan replayed;Check(replay.TryDequeue(out replayed)&&replayed.Id==generated.Id&&replay.Budget.Requests==0&&helper.Posts.Count==posts,"subscription NPC reply replay makes zero network calls");}
            }
            helper.InvalidProposal=true;world=World();using(var coordinator=new PlanningCoordinator(catalog,session.CreateGenerator()))
            {coordinator.Request(world,"player","respond","nav");coordinator.Poll(world,"nav");world.Tick++;coordinator.Poll(world,"nav");ActionPlan refused;Check(!coordinator.TryDequeue(out refused)&&coordinator.UnsupportedReplies==1&&world.Entities["grid"].Get("power")==1,"provider text cannot invent incidents or bypass executable operation guards");}
        }
    }
}
