using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class NativeProviderControlTests
    {
        static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException("Native provider control: "+reason);}
        static JObject Provider(string id)=>new JObject{["id"]=id,["label"]=id,["registered"]=true,["billing"]="user_subscription",["auth_modes"]=new JArray("browser_oauth"),["capabilities"]=new JArray("initial_world","npc_plan"),["state"]="registered",["reason"]=""};
        static JObject Account(string id,string provider)=>new JObject{["id"]=id,["provider_id"]=provider,["label"]="My "+provider,["signed_in"]=true,["active"]=provider=="alpha",["login_state"]="signed_in",["catalog_state"]="ready",["inference_state"]="eligible",["catalog_version"]=new string('a',64)};
        static JObject Model(string account,string provider)=>new JObject{["account_id"]=account,["provider_id"]=provider,["model_id"]="org/shared:1",["display_name"]="Shared",["catalog_version"]=new string('a',64),["selectable"]=true,["billing"]="user_subscription",["capabilities"]=new JArray("initial_world","npc_plan"),["reason"]=""};
        static JObject Descriptor()=>new JObject{["schema"]=1,["endpoint"]="http://127.0.0.1:18953",["capability"]=new string('c',48),["browser_url"]="http://127.0.0.1:18953/#"+new string('b',48)};
        sealed class Helper
        {
            public readonly List<(string method,string path,JObject body)> Calls=new List<(string,string,JObject)>();
            public JObject Status=new JObject{["protocol"]="multi-provider-subscription-v1",["provider"]="alpha",["connected"]=true,["cloud_enabled"]=false,["plan_permission"]=true,["quota_stopped"]=false,["model"]="",["session_generation"]="generation-1",["selection"]=null,
                ["providers"]=new JArray(Provider("alpha"),Provider("beta")),["inference_contracts"]=new JObject{["alpha"]=new string('a',64),["beta"]=new string('b',64)},["accounts"]=new JArray(Account("account-a","alpha"),Account("account-b","beta")),["initial_world"]=new JObject{["remaining"]=0,["allowance"]=null},["usage"]=new JObject{["requests"]=0,["input_tokens"]=0,["output_tokens"]=0}};
            public JArray Models=new JArray(Model("account-a","alpha"),Model("account-b","beta"));
            public TaskCompletionSource<ModelHttpReply> Pending;public bool RemoteStale,PollCompletes,IgnoreCancellation;public Action BeforeReply;
            int generation=1;
            void Rotate(){Status["session_generation"]="generation-"+(++generation);if(Status["selection"] is JObject choice)choice["session_generation"]=Status["session_generation"].DeepClone();}
            public JObject Envelope(JObject flow=null)=>new JObject{["schema"]=NativeProviderControlClient.Protocol,["status"]=Status.DeepClone(),["models"]=Models.DeepClone(),["flow"]=flow};
            public Task<ModelHttpReply> Send(string method,string endpoint,string capability,string captured,string body,CancellationToken cancellation)
            {
                Check(capability==new string('c',48),"native control uses app capability, not a provider token");
                string path=new Uri(endpoint).AbsolutePath;Check(path.StartsWith("/native/v1/"),"controls never use the inference route");
                if(path=="/native/v1/status")Check(captured==null,"status is a read-only unbound refresh");else Check(captured==(string)Status["session_generation"],"every control mutation and flow poll binds current generation");
                JObject input=body==null?null:JObject.Parse(body);Calls.Add((method,path,input));
                if(Pending!=null){if(!IgnoreCancellation)cancellation.Register(()=>Pending.TrySetCanceled());return Pending.Task;}
                if(RemoteStale)return Task.FromResult(new ModelHttpReply{Status=409,Body="{\"error\":{\"code\":\"provider_session_changed\"}}"});
                JObject flow=null;
                if(path=="/native/v1/selection"&&method=="POST")
                {
                    Status["provider"]=input["provider_id"].DeepClone();Status["model"]=input["model_id"].DeepClone();Status["cloud_enabled"]=false;
                    Status["selection"]=new JObject{["provider_id"]=input["provider_id"],["account_id"]=input["account_id"],["model_id"]=input["model_id"],["session_generation"]=Status["session_generation"],["catalog_version"]=input["catalog_version"],["selection_version"]=ProviderSelection.Version,["billing"]="user_subscription",["registration"]="own_app_public_oauth"};Rotate();
                }
                else if(path=="/native/v1/selection"&&method=="DELETE"){Status["selection"]=null;Status["model"]="";Status["cloud_enabled"]=false;Rotate();}
                else if(path=="/native/v1/consent")
                {
                    Status["cloud_enabled"]=input["subscription_enabled"].DeepClone();Rotate();
                    if(input["initial_world_allowance"]!=null){Status["initial_world"]["remaining"]=1;Status["initial_world"]["allowance"]=input["initial_world_allowance"].DeepClone();}
                }
                else if(path=="/native/v1/auth/start")
                {flow=Flow("waiting_for_browser");flow["authorization_url"]="https://auth.example.test/authorize?state=TRANSIENT_SECRET";}
                else if(path.StartsWith("/native/v1/auth/flows/"))flow=Flow(method=="DELETE"?"cancelled":PollCompletes?"completed":"waiting_for_browser");
                var envelope=Envelope(flow);if(path=="/native/v1/logout")envelope["logout"]=new JObject{["local_session_cleared"]=true,["remote_revocation_confirmed"]=false};
                BeforeReply?.Invoke();return Task.FromResult(new ModelHttpReply{Status=200,Body=envelope.ToString()});
            }
            public static JObject Flow(string status)=>new JObject{["flow_id"]="flow-1",["provider_id"]="alpha",["status"]=status,["expires_in_seconds"]=100,["poll_after_ms"]=1000,["error_code"]=null,["account_id"]=null};
        }
        static NativeProviderControlReply Await(Task<NativeProviderControlReply> task)=>task.GetAwaiter().GetResult();
        static void Refused(Func<Task<NativeProviderControlReply>> request,string code)
        {bool refused=false;try{Await(request());}catch(NativeProviderControlException error){refused=error.Code==code;}Check(refused,"request refused with "+code);}
        public static void Run()
        {
            var helper=new Helper();var connection=SubscriptionConnection.ParseDescriptor(Descriptor().ToString(),helper.Send);bool editable=true;var client=connection.CreateControlClient(()=>editable);
            var state=Await(client.RefreshAsync(CancellationToken.None)).State;
            Check(state.Providers.Count==2&&state.Accounts.Count==2&&state.Models.Count==2&&!state.Subscription.Ready,"cached status is useful before selecting or enabling a subscription");
            Check(helper.Calls.Count==1&&helper.Calls.All(c=>c.path!="/v1/chat/completions")&&!state.InitialAllowed,"status does not refresh a catalog, call a model or arm initial generation");
            Await(client.RefreshCatalogAsync(CancellationToken.None));Check(helper.Calls.Last().path=="/native/v1/catalog","explicit catalog refresh has its own route");
            var beta=client.State.Models.Single(m=>m.ProviderId=="beta");var alpha=client.State.Models.Single(m=>m.ProviderId=="alpha");Check(alpha.ModelId==beta.ModelId,"catalog keeps duplicate model IDs under distinct accounts/providers");
            state=Await(client.SelectModelAsync(beta,CancellationToken.None)).State;Check(state.Subscription.Selection.ProviderId=="beta"&&!state.Subscription.CloudEnabled,"selection alone never enables subscription use");
            state=Await(client.SetConsentAsync(true,null,null,CancellationToken.None)).State;Check(state.Subscription.Ready&&!state.InitialAllowed,"ordinary subscription consent is separate from initial one-shot allowance");
            var locked=connection.LockSelection(state.Subscription);state=Await(client.SetConsentAsync(true,192000,2048,CancellationToken.None)).State;
            Check(state.InitialAllowed&&state.InitialInputUnits==192000&&state.InitialOutputTokens==2048&&!locked.IsCurrent,"explicit one-shot consent and session-generation CAS update are distinct");
            int before=helper.Calls.Count;editable=false;
            Refused(()=>client.SelectModelAsync(alpha,CancellationToken.None),"native_controls_locked_for_play");Refused(()=>client.StartLoginAsync("alpha",null,false,CancellationToken.None),"native_controls_locked_for_play");Refused(()=>client.LogoutAsync("account-b",CancellationToken.None),"native_controls_locked_for_play");
            Check(helper.Calls.Count==before,"playing rejects control mutations before localhost requests");Await(client.RefreshAsync(CancellationToken.None));Check(helper.Calls.Count==before+1,"playing still permits status inspection");editable=true;
            var start=Await(client.StartLoginAsync("alpha",null,false,CancellationToken.None)).Flow;Check(start.Pending&&start.AuthorizationUrl.StartsWith("https://"),"native login exposes only an external authorization link");
            Check(!JsonConvert.SerializeObject(start).Contains("TRANSIENT_SECRET"),"authorization links/user codes are omitted from generic serialization");
            var poll=Await(client.PollLoginAsync(start.FlowId,CancellationToken.None)).Flow.PreserveStartHints(start);Check(poll.AuthorizationUrl==start.AuthorizationUrl,"poll preserves short-lived start guidance without restarting authentication");
            helper.PollCompletes=true;poll=Await(client.PollLoginAsync(start.FlowId,CancellationToken.None)).Flow;Check(poll.Completed&&poll.AuthorizationUrl==null,"completed flow drops transient browser guidance");
            var logout=Await(client.LogoutAsync("account-b",CancellationToken.None));Check(logout.LocalSessionCleared==true&&logout.RemoteRevocationConfirmed==false,"logout exposes local success separately from remote revocation");
            Await(client.ClearSelectionAsync(CancellationToken.None));Check(client.State.Subscription.Selection==null&&!client.State.Subscription.CloudEnabled,"clear selection keeps accounts while turning subscription use off");
            Check(helper.Calls.All(c=>c.path.StartsWith("/native/v1/")),"all product controls remain outside model inference");
            StaleAndCancellation();InvalidEnvelopes();
        }
        static void StaleAndCancellation()
        {
            var helper=new Helper();var connection=SubscriptionConnection.ParseDescriptor(Descriptor().ToString(),helper.Send);bool editable=true;var client=connection.CreateControlClient(()=>editable);Await(client.RefreshAsync(CancellationToken.None));
            helper.Pending=new TaskCompletionSource<ModelHttpReply>();var task=client.RefreshCatalogAsync(CancellationToken.None);Check(!task.IsCompleted&&client.IsBusy,"network work does not block the caller thread");
            Refused(()=>client.RefreshAsync(CancellationToken.None),"native_control_request_running");editable=false;helper.Pending.SetResult(new ModelHttpReply{Status=200,Body=helper.Envelope().ToString()});Refused(()=>task,"stale_native_control_reply");Check(!client.IsBusy,"playing change discards a late control result and frees the request slot");
            editable=true;helper.Pending=new TaskCompletionSource<ModelHttpReply>();using(var cancellation=new CancellationTokenSource()){task=client.RefreshAsync(cancellation.Token);cancellation.Cancel();Refused(()=>task,"native_control_cancelled");}Check(!client.IsBusy,"cancellation frees the async slot");
            helper.Pending=new TaskCompletionSource<ModelHttpReply>();helper.IgnoreCancellation=true;var previous=client.State;
            using(var cancellation=new CancellationTokenSource())
            {task=client.StartLoginAsync("alpha",null,false,cancellation.Token);cancellation.Cancel();client.Invalidate();Refused(()=>task,"native_control_cancelled");Check(!client.IsBusy&&ReferenceEquals(previous,client.State),"closing an in-flight login frees its slot even if the transport ignores cancellation");var late=helper.Envelope(Helper.Flow("waiting_for_browser"));late["flow"]["authorization_url"]="https://auth.example.test/late?state=MUST_NOT_OPEN";helper.Pending.SetResult(new ModelHttpReply{Status=200,Body=late.ToString()});Check(ReferenceEquals(previous,client.State),"late ignored-cancel200 cannot resurrect auth flow or authorization link");}
            helper.Pending=null;using(var cancellation=new CancellationTokenSource())
            {helper.BeforeReply=()=>cancellation.Cancel();Refused(()=>client.RefreshAsync(cancellation.Token),"native_control_cancelled");Check(ReferenceEquals(previous,client.State),"already completed200 is rejected when cancellation races the response");helper.BeforeReply=null;}
            helper.Pending=null;helper.RemoteStale=true;Refused(()=>client.RefreshAsync(CancellationToken.None),"provider_session_changed");Check(!client.IsBusy,"remote409 does not retry or rotate providers");
        }
        static void InvalidEnvelopes()
        {
            var helper=new Helper();var connection=SubscriptionConnection.ParseDescriptor(Descriptor().ToString(),helper.Send);var client=connection.CreateControlClient();Await(client.RefreshAsync(CancellationToken.None));var previous=client.State;
            helper.Models.Add(helper.Models[0].DeepClone());Refused(()=>client.RefreshAsync(CancellationToken.None),"duplicate_provider_catalog_model");Check(ReferenceEquals(previous,client.State),"malformed catalog cannot partially replace current menu state");
            helper.Models.RemoveAt(helper.Models.Count-1);helper.Models[0]["billing"]="api_key";Refused(()=>client.RefreshAsync(CancellationToken.None),"invalid_provider_catalog");
            helper.Models[0]["billing"]="user_subscription";helper.Pending=new TaskCompletionSource<ModelHttpReply>();var task=client.StartLoginAsync("alpha",null,false,CancellationToken.None);var bad=helper.Envelope(Helper.Flow("waiting_for_browser"));bad["flow"]["authorization_url"]="http://127.0.0.1:18953/#private-bootstrap";helper.Pending.SetResult(new ModelHttpReply{Status=200,Body=bad.ToString()});Refused(()=>task,"invalid_provider_authorization_url");
        }
    }
}
