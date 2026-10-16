using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public sealed class NativeProviderInfo
    {
        public string Id {get;internal set;} public string Label {get;internal set;} public bool Registered {get;internal set;}
        public string State {get;internal set;} public string Reason {get;internal set;} public string Billing {get;internal set;}
        public string[] AuthModes {get;internal set;} public string[] Capabilities {get;internal set;}
    }
    public sealed class NativeProviderAccount
    {
        public string Id {get;internal set;} public string ProviderId {get;internal set;} public string Label {get;internal set;}
        public bool SignedIn {get;internal set;} public bool Active {get;internal set;}
        public string LoginState {get;internal set;} public string CatalogState {get;internal set;} public string InferenceState {get;internal set;}
        public string Error {get;internal set;} public string CatalogVersion {get;internal set;}
    }
    public sealed class NativeProviderModel
    {
        public string ProviderId {get;internal set;} public string AccountId {get;internal set;} public string ModelId {get;internal set;}
        public string DisplayName {get;internal set;} public string CatalogVersion {get;internal set;}
        public bool Selectable {get;internal set;} public string Reason {get;internal set;} public string Billing {get;internal set;}
        public string[] Capabilities {get;internal set;}
    }
    public sealed class NativeProviderState
    {
        public string Protocol {get;internal set;} public string Generation {get;internal set;}
        public SubscriptionStatus Subscription {get;internal set;}
        public IReadOnlyList<NativeProviderInfo> Providers {get;internal set;}
        public IReadOnlyList<NativeProviderAccount> Accounts {get;internal set;}
        public IReadOnlyList<NativeProviderModel> Models {get;internal set;}
        public bool InitialAllowed {get;internal set;} public long InitialInputUnits {get;internal set;} public long InitialOutputTokens {get;internal set;}
        public string Error {get;internal set;}
    }
    public sealed class NativeAuthFlow
    {
        public string FlowId {get;internal set;} public string ProviderId {get;internal set;} public string Status {get;internal set;}
        public string ErrorCode {get;internal set;} public string AccountId {get;internal set;}
        public int ExpiresInSeconds {get;internal set;} public int PollAfterMs {get;internal set;}
        [JsonIgnore] public string AuthorizationUrl {get;internal set;}
        [JsonIgnore] public string VerificationUri {get;internal set;}
        [JsonIgnore] public string UserCode {get;internal set;}
        public bool Pending {get {return Status=="waiting_for_browser"||Status=="waiting_for_device";}}
        public bool Completed {get {return Status=="completed";}}
        public NativeAuthFlow PreserveStartHints(NativeAuthFlow previous)
        {
            if(Pending&&previous!=null&&previous.FlowId==FlowId)
            {AuthorizationUrl=AuthorizationUrl??previous.AuthorizationUrl;VerificationUri=VerificationUri??previous.VerificationUri;UserCode=UserCode??previous.UserCode;}
            return this;
        }
    }
    public sealed class NativeProviderControlException:Exception
    {
        public string Code {get;}
        public NativeProviderControlException(string code):base(code){Code=code;}
    }
    public sealed class NativeProviderControlReply
    {
        public NativeProviderState State {get;internal set;}
        public NativeAuthFlow Flow {get;internal set;}
        public bool? LocalSessionCleared {get;internal set;}
        public bool? RemoteRevocationConfirmed {get;internal set;}
    }
    /// <summary>Nonblocking native menu operations. Only the helper handles OAuth tokens.</summary>
    public sealed class NativeProviderControlClient
    {
        public const string Protocol="native-provider-control-v1";
        readonly SubscriptionConnection connection;readonly Func<bool> canMutate;
        int busy;long revision;
        public NativeProviderState State {get;private set;}
        public bool IsBusy {get {return Volatile.Read(ref busy)!=0;}}
        public NativeProviderControlClient(SubscriptionConnection connection,Func<bool> canMutate=null)
        {this.connection=connection??throw new ArgumentNullException(nameof(connection));this.canMutate=canMutate??(()=>true);}
        public Task<NativeProviderControlReply> RefreshAsync(CancellationToken cancellation)
        {return Request("GET","/native/v1/status",null,false,cancellation);}
        public Task<NativeProviderControlReply> RefreshCatalogAsync(CancellationToken cancellation)
        {return Request("POST","/native/v1/catalog",new JObject(),true,cancellation);}
        public Task<NativeProviderControlReply> StartLoginAsync(string providerId,string accountId,bool reconsent,CancellationToken cancellation)
        {
            RequireIdentifier(providerId);if(accountId!=null)RequireIdentifier(accountId);
            var provider=State?.Providers.SingleOrDefault(p=>p.Id==providerId);
            if(provider==null||!provider.Registered||provider.Billing!="user_subscription")throw Failure("provider_not_available_for_subscription_mode");
            return Request("POST","/native/v1/auth/start",new JObject{["provider_id"]=providerId,["account_id"]=accountId,["reconsent"]=reconsent},true,cancellation);
        }
        public Task<NativeProviderControlReply> PollLoginAsync(string flowId,CancellationToken cancellation)
        {RequireIdentifier(flowId);return Request("GET","/native/v1/auth/flows/"+flowId,null,true,cancellation);}
        public Task<NativeProviderControlReply> CancelLoginAsync(string flowId,CancellationToken cancellation)
        {RequireIdentifier(flowId);return Request("DELETE","/native/v1/auth/flows/"+flowId,new JObject(),true,cancellation);}
        public Task<NativeProviderControlReply> SelectModelAsync(NativeProviderModel model,CancellationToken cancellation)
        {
            if(model==null||!model.Selectable||model.Billing!="user_subscription"||State==null||!State.Models.Any(m=>m.ProviderId==model.ProviderId&&m.AccountId==model.AccountId&&m.ModelId==model.ModelId&&m.CatalogVersion==model.CatalogVersion&&m.Selectable))throw Failure("authorized_account_model_required");
            return Request("POST","/native/v1/selection",new JObject{["provider_id"]=model.ProviderId,["account_id"]=model.AccountId,["model_id"]=model.ModelId,["catalog_version"]=model.CatalogVersion},true,cancellation);
        }
        public Task<NativeProviderControlReply> ClearSelectionAsync(CancellationToken cancellation)
        {return Request("DELETE","/native/v1/selection",new JObject(),true,cancellation);}
        public Task<NativeProviderControlReply> SetConsentAsync(bool enabled,long? initialInputUnits,long? initialOutputTokens,CancellationToken cancellation)
        {
            var body=new JObject{["subscription_enabled"]=enabled};
            if(initialInputUnits.HasValue||initialOutputTokens.HasValue)
            {
                if(!enabled||!initialInputUnits.HasValue||!initialOutputTokens.HasValue||initialInputUnits<1||initialInputUnits>SubscriptionConnection.MaximumInitialInputUnits||initialOutputTokens<1||initialOutputTokens>4096)throw Failure("invalid_initial_allowance");
                body["initial_world_allowance"]=new JObject{["input_tokens"]=initialInputUnits.Value,["output_tokens"]=initialOutputTokens.Value};
            }
            return Request("POST","/native/v1/consent",body,true,cancellation);
        }
        public Task<NativeProviderControlReply> LogoutAsync(string accountId,CancellationToken cancellation)
        {RequireIdentifier(accountId);return Request("POST","/native/v1/logout",new JObject{["account_id"]=accountId},true,cancellation);}
        public void Invalidate(){Interlocked.Increment(ref revision);connection.InvalidateSelection();}
        async Task<NativeProviderControlReply> Request(string method,string path,JObject body,bool mutation,CancellationToken cancellation)
        {
            if(mutation&&!canMutate())throw Failure("native_controls_locked_for_play");
            if(Interlocked.CompareExchange(ref busy,1,0)!=0)throw Failure("native_control_request_running");
            long ticket=Interlocked.Increment(ref revision);string generation=State?.Generation;
            try
            {
                if(mutation&&!SubscriptionConnection.Identifier(generation))throw Failure("native_status_required");
                ModelHttpReply response;
                using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(30));deadline.Token.ThrowIfCancellationRequested();
                    var call=connection.SendNativeControl(method,path,generation,body?.ToString(Formatting.None),deadline.Token);
                    var stopped=Task.Delay(Timeout.Infinite,deadline.Token);
                    if(await Task.WhenAny(call,stopped).ConfigureAwait(false)!=call)throw new OperationCanceledException(deadline.Token);
                    response=await call.ConfigureAwait(false);
                    deadline.Token.ThrowIfCancellationRequested();
                }
                cancellation.ThrowIfCancellationRequested();
                if(ticket!=Interlocked.Read(ref revision)||mutation&&!canMutate())throw Failure("stale_native_control_reply");
                if(response.Status!=200)
                {
                    if(response.Status==409)connection.InvalidateSelection();
                    string code=null;try{code=(string)JObject.Parse(response.Body)["error"]?["code"];}catch(JsonException){}
                    throw Failure(SubscriptionConnection.Identifier(code)?code:"native_control_http_"+response.Status);
                }
                var reply=ParseReply(JObject.Parse(response.Body));
                cancellation.ThrowIfCancellationRequested();
                if(ticket!=Interlocked.Read(ref revision)||mutation&&!canMutate())throw Failure("stale_native_control_reply");
                // State is applied after the entire native envelope and model/account contract validate.
                reply.State.Subscription=connection.ObserveStatus((JObject)JObject.Parse(response.Body)["status"]);
                State=reply.State;return reply;
            }
            catch(OperationCanceledException){connection.InvalidateSelection();throw Failure("native_control_cancelled");}
            catch(NativeProviderControlException){connection.InvalidateSelection();throw;}
            catch(Exception){connection.InvalidateSelection();throw Failure("native_control_transport_failure");}
            finally{Interlocked.Exchange(ref busy,0);}
        }
        static string Text(JObject value,string key,int maximum=512,bool required=false)
        {
            var token=value[key];if(token==null||token.Type==JTokenType.Null){if(required)throw Failure("invalid_native_control_reply");return null;}
            string text=token.Type==JTokenType.String?(string)token:null;
            if(text==null||text.Length>maximum||text.Any(c=>char.IsControl(c)))throw Failure("invalid_native_control_reply");return text;
        }
        static string[] Strings(JObject value,string key)
        {var array=value[key] as JArray;if(array==null||array.Count>64||array.Any(t=>t.Type!=JTokenType.String))throw Failure("invalid_native_control_reply");return array.Values<string>().ToArray();}
        static void RequireIdentifier(string value){if(!SubscriptionConnection.Identifier(value))throw Failure("invalid_native_control_identifier");}
        static IReadOnlyList<T> ReadRows<T>(JToken token,Func<JObject,T> read,int maximum)
        {
            var rows=token as JArray;if(rows==null||rows.Count>maximum||rows.Any(t=>!(t is JObject)))throw Failure("invalid_native_control_reply");
            return Array.AsReadOnly(rows.OfType<JObject>().Select(read).ToArray());
        }
        static string SafeExternalUrl(JObject value,string name)
        {
            string url=Text(value,name,8192);if(url==null)return null;Uri parsed;
            if(!Uri.TryCreate(url,UriKind.Absolute,out parsed)||parsed.Scheme!="https"||!string.IsNullOrEmpty(parsed.UserInfo)||parsed.IsLoopback||!string.IsNullOrEmpty(parsed.Fragment))throw Failure("invalid_provider_authorization_url");
            return url;
        }
        static NativeProviderControlReply ParseReply(JObject envelope)
        {
            if((string)envelope["schema"]!=Protocol||!(envelope["status"] is JObject status))throw Failure("invalid_native_control_reply");
            string generation=Text(status,"session_generation",160,true);RequireIdentifier(generation);
            if((string)status["protocol"]!="multi-provider-subscription-v1")throw Failure("provider_registry_protocol_required");
            var state=new NativeProviderState{Protocol=(string)status["protocol"],Generation=generation,Error=Text(status,"error",160)};
            state.Providers=ReadRows(status["providers"],row=>new NativeProviderInfo{Id=Text(row,"id",160,true),Label=Text(row,"label",512,true),Registered=(bool?)row["registered"]==true,State=Text(row,"state"),Reason=Text(row,"reason",2048),Billing=Text(row,"billing"),AuthModes=Strings(row,"auth_modes"),Capabilities=Strings(row,"capabilities")},256);
            if(state.Providers.Any(p=>!SubscriptionConnection.Identifier(p.Id))||state.Providers.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=state.Providers.Count)throw Failure("invalid_provider_registry");
            state.Accounts=ReadRows(status["accounts"],row=>new NativeProviderAccount{Id=Text(row,"id",160,true),ProviderId=Text(row,"provider_id",160,true),Label=Text(row,"label",512,true),SignedIn=(bool?)row["signed_in"]==true,Active=(bool?)row["active"]==true,LoginState=Text(row,"login_state"),CatalogState=Text(row,"catalog_state"),InferenceState=Text(row,"inference_state"),Error=Text(row,"error",160),CatalogVersion=Text(row,"catalog_version",64)},4096);
            if(state.Accounts.Any(a=>!SubscriptionConnection.Identifier(a.Id)||!state.Providers.Any(p=>p.Id==a.ProviderId&&p.Registered))||state.Accounts.Select(a=>a.Id).Distinct(StringComparer.Ordinal).Count()!=state.Accounts.Count)throw Failure("invalid_provider_account");
            state.Models=ReadRows(envelope["models"],row=>new NativeProviderModel{ProviderId=Text(row,"provider_id",160,true),AccountId=Text(row,"account_id",160,true),ModelId=Text(row,"model_id",256,true),DisplayName=Text(row,"display_name",512,true),CatalogVersion=Text(row,"catalog_version",64,true),Selectable=(bool?)row["selectable"]==true,Reason=Text(row,"reason",2048),Billing=Text(row,"billing"),Capabilities=Strings(row,"capabilities")},32768);
            if(state.Models.Any(m=>!ProviderSelection.ModelIdentifier(m.ModelId)||!state.Accounts.Any(a=>a.Id==m.AccountId&&a.ProviderId==m.ProviderId&&a.SignedIn)||m.Selectable&&(m.Billing!="user_subscription"||!m.Capabilities.Contains("initial_world")||!m.Capabilities.Contains("npc_plan"))))throw Failure("invalid_provider_catalog");
            if(state.Models.GroupBy(m=>m.ProviderId+"\n"+m.AccountId+"\n"+m.ModelId).Any(g=>g.Count()!=1))throw Failure("duplicate_provider_catalog_model");
            var initial=status["initial_world"] as JObject;var allowance=initial?["allowance"] as JObject;
            state.InitialAllowed=(int?)initial?["remaining"]==1;state.InitialInputUnits=(long?)allowance?["input_tokens"]??0;state.InitialOutputTokens=(long?)allowance?["output_tokens"]??0;
            var reply=new NativeProviderControlReply{State=state};
            if(envelope["flow"] is JObject flow)
            {
                var item=new NativeAuthFlow{FlowId=Text(flow,"flow_id",160,true),ProviderId=Text(flow,"provider_id",160,true),Status=Text(flow,"status",64,true),ErrorCode=Text(flow,"error_code",160),AccountId=Text(flow,"account_id",160),ExpiresInSeconds=(int?)flow["expires_in_seconds"]??0,PollAfterMs=(int?)flow["poll_after_ms"]??1000,
                    AuthorizationUrl=SafeExternalUrl(flow,"authorization_url"),VerificationUri=SafeExternalUrl(flow,"verification_uri"),UserCode=Text(flow,"user_code",160)};
                RequireIdentifier(item.FlowId);RequireIdentifier(item.ProviderId);
                if(!new[]{"waiting_for_browser","waiting_for_device","completed","cancelled","expired","failed"}.Contains(item.Status)||item.ExpiresInSeconds<0||item.PollAfterMs<0||item.PollAfterMs>60000)throw Failure("invalid_provider_login_flow");
                reply.Flow=item;
            }
            if(envelope["logout"] is JObject logout){reply.LocalSessionCleared=(bool?)logout["local_session_cleared"];reply.RemoteRevocationConfirmed=(bool?)logout["remote_revocation_confirmed"];}
            return reply;
        }
        static NativeProviderControlException Failure(string code){return new NativeProviderControlException(code);}
    }
}
