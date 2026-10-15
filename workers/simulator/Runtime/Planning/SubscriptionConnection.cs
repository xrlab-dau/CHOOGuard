using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    // Unity receives a short-lived local capability, never the provider's credentials.
    public sealed class SubscriptionStatus
    {
        public bool Connected,CloudEnabled;
        public string Provider,AccountLabel,Model,SessionGeneration,Error,InferenceContractSha256;
        public ProviderSelection Selection;
        public bool RegisteredSubscriptionProvider;
        public long Requests,InputTokens,OutputTokens;
        public bool Ready {get {return Connected&&CloudEnabled&&RegisteredSubscriptionProvider&&Selection!=null&&SubscriptionConnection.PublicHash(InferenceContractSha256)&&Provider==Selection.ProviderId&&Model==Selection.ModelId&&SessionGeneration==Selection.SessionGeneration;}}
    }
    public delegate Task<ModelHttpReply> SubscriptionTransport(string method,string endpoint,string capability,string generation,string body,CancellationToken cancellation);
    public sealed class SubscriptionConnection
    {
        public static string DescriptorPath {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".local","share","chooguard","provider","connection.json");}}
        public string Endpoint {get;private set;}
        public string BrowserUrl {get;private set;}
        private string capability;
        private SubscriptionTransport transport;
        private volatile string currentSelectionFingerprint;
        internal static bool Identifier(string value)
        {
            if(string.IsNullOrEmpty(value)||value.Length>160)return false;
            foreach(char c in value)if(!(char.IsLetterOrDigit(c)||c=='-'||c=='_'||c=='.'||c==':'))return false;
            return true;
        }
        internal static bool PublicHash(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        public static bool TryLoad(out SubscriptionConnection connection,out string reason)
        {
            connection=null;
            try
            {
                var path=DescriptorPath;
                if(!File.Exists(path)){reason="subscription_helper_not_running";return false;}
                if(new FileInfo(path).Length>16384){reason="invalid_connection_descriptor";return false;}
                connection=ParseDescriptor(File.ReadAllText(path));reason="";return true;
            }
            catch(Exception){reason="invalid_connection_descriptor";return false;}
        }
        public static SubscriptionConnection ParseDescriptor(string json,SubscriptionTransport transport=null)
        {
            if(json==null||json.Length>16384)throw new ArgumentException("invalid_connection_descriptor");
            var root=JObject.Parse(json);
            var endpoint=(string)root["endpoint"];Uri uri;
            if((int?)root["schema"]!=1||!Uri.TryCreate(endpoint,UriKind.Absolute,out uri)||uri.Scheme!="http"||uri.Host!="127.0.0.1"||uri.Port<1024||uri.Port>65535||uri.UserInfo!=""||uri.AbsolutePath!="/"||uri.Query!=""||uri.Fragment!="")
                throw new ArgumentException("invalid_connection_endpoint");
            endpoint=uri.GetLeftPart(UriPartial.Authority);
            string capability=(string)root["capability"],browser=(string)root["browser_url"];
            if(!Identifier(capability)||capability.Length<32)throw new ArgumentException("invalid_local_capability");
            if(!Uri.TryCreate(browser,UriKind.Absolute,out uri)||uri.GetLeftPart(UriPartial.Authority)!=endpoint||uri.UserInfo!=""||uri.AbsolutePath!="/"||uri.Query!=""||!Identifier(uri.Fragment.TrimStart('#'))||uri.Fragment.Length<33)
                throw new ArgumentException("invalid_connection_browser_url");
            return new SubscriptionConnection{Endpoint=endpoint,BrowserUrl=browser,capability=capability,transport=transport??Send};
        }
        public async Task<SubscriptionStatus> Refresh(CancellationToken cancellation)
        {
            try
            {
                var response=await transport("GET",Endpoint+"/status",capability,null,null,cancellation).ConfigureAwait(false);
                if(response.Status!=200){currentSelectionFingerprint=null;return new SubscriptionStatus{Error="subscription_helper_http_"+response.Status};}
                return ObserveStatus(JObject.Parse(response.Body));
            }
            catch(Exception error){currentSelectionFingerprint=null;return new SubscriptionStatus{Error=ModelHttp.SafeError(error)};}
        }
        internal SubscriptionStatus ObserveStatus(JObject root)
        {
                var usage=root["usage"];
                ProviderSelection selection=null;
                if(root["selection"]!=null&&root["selection"].Type!=JTokenType.Null)selection=ProviderSelection.Parse(root["selection"]);
                var providers=root["providers"] as JArray;
                var inferenceContract=selection==null?null:root["inference_contracts"]?[selection.ProviderId];
                bool registered=selection!=null&&providers!=null&&providers.OfType<JObject>().Count(p=>(string)p["id"]==selection.ProviderId)==1&&providers.OfType<JObject>().Any(p=>
                    (string)p["id"]==selection.ProviderId&&(bool?)p["registered"]==true&&(string)p["billing"]=="user_subscription"&&
                    p["auth_modes"] is JArray modes&&modes.Values<string>().Contains("browser_oauth")&&p["capabilities"] is JArray capabilities&&
                    capabilities.Values<string>().Contains("initial_world")&&capabilities.Values<string>().Contains("npc_plan"));
                var status=new SubscriptionStatus{Provider=(string)root["provider"],Connected=(bool?)root["connected"]??false,CloudEnabled=(bool?)root["cloud_enabled"]??false,
                    AccountLabel=(string)root["account_label"],Model=(string)root["model"],SessionGeneration=(string)root["session_generation"],Error=(string)root["error"],
                    Requests=(long?)usage?["requests"]??0,InputTokens=(long?)usage?["input_tokens"]??0,OutputTokens=(long?)usage?["output_tokens"]??0,
                    Selection=selection,RegisteredSubscriptionProvider=registered,
                    InferenceContractSha256=inferenceContract?.Type==JTokenType.String?(string)inferenceContract:null};
                currentSelectionFingerprint=status.Ready?selection.Fingerprint:null;
                if(status.Connected&&status.CloudEnabled&&!status.Ready&&string.IsNullOrEmpty(status.Error))status.Error=selection!=null&&!PublicHash(status.InferenceContractSha256)?"subscription_helper_upgrade_required":"authorized_provider_selection_required";
                return status;
        }
        internal void InvalidateSelection(){currentSelectionFingerprint=null;}
        public NativeProviderControlClient CreateControlClient(Func<bool> canMutate=null){return new NativeProviderControlClient(this,canMutate);}
        internal Task<ModelHttpReply> SendNativeControl(string method,string path,string generation,string body,CancellationToken cancellation)
        {
            bool flow=path.StartsWith("/native/v1/auth/flows/",StringComparison.Ordinal)&&Identifier(path.Substring("/native/v1/auth/flows/".Length));
            bool allowed=method=="GET"&&(path=="/native/v1/status"||flow)||method=="DELETE"&&(path=="/native/v1/selection"||flow)||method=="POST"&&new[]{"/native/v1/catalog","/native/v1/auth/start","/native/v1/selection","/native/v1/consent","/native/v1/logout"}.Contains(path);
            if(!allowed)throw new ArgumentException("native_control_route_not_allowed");
            return transport(method,Endpoint+path,capability,path=="/native/v1/status"?null:generation,body,cancellation);
        }
        public SubscriptionSession LockSelection(SubscriptionStatus status)
        {
            if(!IsSelectionCurrent(status))throw new InvalidOperationException("subscription_selection_not_ready");
            return new SubscriptionSession(this,status.Selection);
        }
        public bool IsSelectionCurrent(SubscriptionStatus status)
        {return status!=null&&status.Ready&&status.Selection.Fingerprint==currentSelectionFingerprint;}
        public IPlanProvider CreateGenerator(SubscriptionStatus status,long maximumOutputTokens=512)
        {return LockSelection(status).CreateGenerator(maximumOutputTokens);}
        public IEmergenceProvider CreateEmergenceProvider(SubscriptionStatus status,long maximumOutputTokens=512)
        {return LockSelection(status).CreateEmergenceProvider(maximumOutputTokens);}
        public ModelTransport CreateTransport(SubscriptionStatus status)
        {return CreateTransport(LockSelection(status).Selection);}
        internal ModelTransport CreateTransport(ProviderSelection selection)
        {
            return async (endpoint,ignoredKey,body,cancellation)=>
            {
                cancellation.ThrowIfCancellationRequested();
                if(endpoint!=Endpoint+"/v1/chat/completions")throw new ArgumentException("fixed_subscription_bridge_required");
                if(!string.IsNullOrEmpty(ignoredKey))throw new ArgumentException("api_key_forbidden_for_subscription");
                if(selection.Fingerprint!=currentSelectionFingerprint)return new ModelHttpReply{Status=409,Body="{\"error\":{\"code\":\"provider_session_changed\"}}"};
                var payload=JObject.Parse(body);
                if((string)payload["model"]!=selection.ModelId||payload["chooguard_selection"]!=null)throw new ArgumentException("subscription_routing_mismatch");
                payload["chooguard_selection"]=selection.ToToken();
                var reply=await transport("POST",endpoint,capability,selection.SessionGeneration,payload.ToString(Newtonsoft.Json.Formatting.None),cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if(reply.Status==409&&selection.Fingerprint==currentSelectionFingerprint)currentSelectionFingerprint=null;
                // A refreshed selection invalidates even a successful late result from the old session.
                return selection.Fingerprint==currentSelectionFingerprint?reply:new ModelHttpReply{Status=409,Body="{\"error\":{\"code\":\"provider_session_changed\"}}"};
            };
        }
        public sealed class SubscriptionSession
        {
            readonly SubscriptionConnection connection;
            public ProviderSelection Selection {get;}
            public string LogicalBindingFingerprint {get {return Selection.LogicalFingerprint;}}
            public JObject LogicalBindingRecord {get {return Selection.ToBindingRecord();}}
            internal SubscriptionSession(SubscriptionConnection connection,ProviderSelection selection){this.connection=connection;Selection=selection;}
            public bool IsCurrent {get {return Selection.Fingerprint==connection.currentSelectionFingerprint;}}
            public IPlanProvider CreateGenerator(long maximumOutputTokens=512){return new SessionGenerator(connection,Selection,maximumOutputTokens);}
            public IEmergenceProvider CreateEmergenceProvider(long maximumOutputTokens=512){return new SessionEmergenceProvider(connection,Selection,maximumOutputTokens);}
        }
        sealed class SessionGenerator:IPlanProvider
        {
            private readonly OpenAiCompatibleGenerator generator;
            private readonly string version;
            public SessionGenerator(SubscriptionConnection connection,ProviderSelection selection,long maximumOutputTokens)
            {
                if(maximumOutputTokens<1||maximumOutputTokens>512)throw new ArgumentException("bounded_output_required");
                version=selection.CacheVersion;
                generator=new OpenAiCompatibleGenerator(connection.Endpoint+"/v1/chat/completions",selection.ModelId,null,maximumOutputTokens,connection.CreateTransport(selection));
            }
            public string Version {get {return version;}}
            public bool Available {get {return true;}}
            public long MaximumOutputTokens {get {return generator.MaximumOutputTokens;}}
            public long EstimateInputTokens(PlanRequest request){return generator.EstimateInputTokens(request);}
            public Task<ProviderReply> Generate(PlanRequest request,CancellationToken cancellation){return generator.Generate(request,cancellation);}
        }
        sealed class SessionEmergenceProvider:IMaterialBoundProvider
        {
            readonly OpenAiCompatibleEmergenceProvider provider;readonly string version;readonly SubscriptionConnection connection;readonly ProviderSelection selection;
            public SessionEmergenceProvider(SubscriptionConnection connection,ProviderSelection selection,long maximumOutputTokens)
            {this.connection=connection;this.selection=selection;version=selection.CacheVersion+":initial_world";provider=new OpenAiCompatibleEmergenceProvider(connection.Endpoint+"/v1/chat/completions",selection.ModelId,null,maximumOutputTokens,connection.CreateTransport(selection));}
            public string LogicalBindingFingerprint=>selection.LogicalFingerprint;
            public JObject LogicalBindingRecord=>selection.ToBindingRecord();
            public bool IsLeaseCurrent=>selection.Fingerprint==connection.currentSelectionFingerprint;
            public string ModelInputFingerprint(EmergenceRequest request)=>provider.InputFingerprint(request);
            public string Version {get {return version;}}
            public bool Available {get {return provider.Available;}}
            public long MaximumOutputTokens {get {return provider.MaximumOutputTokens;}}
            public long EstimateInputTokens(EmergenceRequest request){return provider.EstimateInputTokens(request);}
            public Task<EmergenceReply> Generate(EmergenceRequest request,CancellationToken cancellation){return provider.Generate(request,cancellation);}
        }
        static async Task<ModelHttpReply> Send(string method,string endpoint,string capability,string generation,string body,CancellationToken cancellation)
        {
            using(var handler=new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false})
            using(var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan})
            using(var message=new HttpRequestMessage(new HttpMethod(method),endpoint))
            {
                message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",capability);
                if(generation!=null)message.Headers.Add("X-ChooGuard-Session",generation);
                if(body!=null)message.Content=new StringContent(body,Encoding.UTF8,"application/json");
                using(var response=await client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,cancellation).ConfigureAwait(false))
                using(var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using(var buffer=new MemoryStream())
                {
                    var bytes=new byte[8192];int count;
                    while((count=await stream.ReadAsync(bytes,0,bytes.Length,cancellation).ConfigureAwait(false))>0)
                    {if(buffer.Length+count>1048576)throw new InvalidDataException("helper_response_too_large");buffer.Write(bytes,0,count);}
                    return new ModelHttpReply{Status=(int)response.StatusCode,Body=Encoding.UTF8.GetString(buffer.ToArray())};
                }
            }
        }
    }
}
