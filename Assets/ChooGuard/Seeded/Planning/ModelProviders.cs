using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    public sealed class OfferedAction { public string operation,actor_id,target_id; }
    public sealed class PlanRequest
    {
        public string Id,ActorId,Goal,NavVersion,ContextJson;
        public long Tick;
        public ActionPlan Basis;
        [JsonIgnore] public World Snapshot;
        public readonly List<OfferedAction> Offers=new List<OfferedAction>();
        public string Json() { return JsonConvert.SerializeObject(new { request_id=Id,actor_id=ActorId,goal=Goal,tick=Tick,context=JToken.Parse(ContextJson),allowed_operations=Offers,max_steps=6,
            response_contract="JSON object {steps:[{operation,actor_id,target_id,amount:(0,1]}]}. Compose only listed executable operations. Never invent effects or objects." }); }
    }
    public sealed class ProviderReply
    {
        public string RequestId,ReturnedModel,Raw,Error;
        public long? InputTokens,OutputTokens;
        public bool Approved=true;
        public List<ActionStep> Steps=new List<ActionStep>();
    }
    public interface IPlanProvider
    {
        string Version {get;}
        bool Available {get;}
        long MaximumOutputTokens {get;}
        long EstimateInputTokens(PlanRequest request);
        Task<ProviderReply> Generate(PlanRequest request,CancellationToken cancellation);
    }
    public interface IPlanEvaluator
    {
        string Version {get;}
        bool Available {get;}
        long MaximumOutputTokens {get;}
        long EstimateInputTokens(PlanRequest request,ProviderReply proposal);
        Task<ProviderReply> Evaluate(PlanRequest request,ProviderReply proposal,CancellationToken cancellation);
    }
    public sealed class ModelHttpReply { public int Status;public string Body; }
    public delegate Task<ModelHttpReply> ModelTransport(string endpoint,string key,string body,CancellationToken cancellation);
    public static class ModelHttp
    {
        public static async Task<ModelHttpReply> Send(string endpoint,string key,string body,CancellationToken cancellation)
        {
            using(var client=new HttpClient {Timeout=System.Threading.Timeout.InfiniteTimeSpan})
            using(var message=new HttpRequestMessage(HttpMethod.Post,endpoint))
            {
                if(!string.IsNullOrEmpty(key))message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
                message.Content=new StringContent(body,Encoding.UTF8,"application/json");
                using(var response=await client.SendAsync(message,cancellation).ConfigureAwait(false))
                    return new ModelHttpReply {Status=(int)response.StatusCode,Body=await response.Content.ReadAsStringAsync().ConfigureAwait(false)};
            }
        }
        // UTF8 bytes plus envelope is a conservative reservation, not reported model usage.
        public static long Estimate(string body) {return Encoding.UTF8.GetByteCount(body)+2048;}
        public static string SafeError(Exception error) {return error is OperationCanceledException?"timeout_or_cancelled":"transport_failure:"+error.GetType().Name;}
    }
    public sealed class OpenAiCompatibleGenerator:IPlanProvider
    {
        private readonly string endpoint,key,model;private readonly ModelTransport transport;
        public string Version {get {return "openai-compatible:"+endpoint+":"+model;}}
        public bool Available {get {return !string.IsNullOrEmpty(endpoint)&&!string.IsNullOrEmpty(model);}}
        public long MaximumOutputTokens {get;private set;}
        public OpenAiCompatibleGenerator(string endpoint,string model,string key=null,long maxOutputTokens=512,ModelTransport transport=null)
        {
            if(!string.IsNullOrEmpty(endpoint)&&!Uri.IsWellFormedUriString(endpoint,UriKind.Absolute))throw new ArgumentException("Absolute generator URL required");
            this.endpoint=endpoint;this.model=model;this.key=key;MaximumOutputTokens=maxOutputTokens;this.transport=transport??ModelHttp.Send;
        }
        private string Body(PlanRequest request) {return JsonConvert.SerializeObject(new {model,temperature=0,max_tokens=MaximumOutputTokens,response_format=new {type="json_object"},messages=new[]{new {role="system",content="Compose supported physical operations for a rail training simulator. Return a JSON object only, without Markdown fences or explanation. Copy operation, actor_id and target_id EXACTLY from allowed_operations. Do not invent effects, objects or scenarios."},new {role="user",content=request.Json()}}});}
        public long EstimateInputTokens(PlanRequest request) {return ModelHttp.Estimate(Body(request));}
        public async Task<ProviderReply> Generate(PlanRequest request,CancellationToken cancellation)
        {
            var reply=new ProviderReply {RequestId=request.Id};
            try
            {
                var response=await transport(endpoint,key,Body(request),cancellation).ConfigureAwait(false);reply.Raw=response.Body;
                if(response.Status<200||response.Status>=300) {reply.Error="generator_http_"+response.Status;return reply;}
                var root=JObject.Parse(response.Body);reply.ReturnedModel=(string)root["model"]??model;
                reply.InputTokens=(long?)root["usage"]?["prompt_tokens"];reply.OutputTokens=(long?)root["usage"]?["completion_tokens"];
                var content=(string)root["choices"]?[0]?["message"]?["content"];
                // Some local instruction models wrap otherwise exact JSON in a single Markdown fence.
                // Preserve Raw and all action values; only remove that unambiguous outer envelope.
                content=content?.Trim();
                if(content!=null&&content.StartsWith("```")&&content.EndsWith("```"))
                {int newline=content.IndexOf('\n');if(newline>=0)content=content.Substring(newline+1,content.Length-newline-4).Trim();}
                var plan=JObject.Parse(content??"");var steps=plan["steps"] as JArray;
                if(steps==null||steps.Count==0||steps.Count>6)throw new ArgumentException("Bounded steps required");
                foreach(var s in steps)reply.Steps.Add(new ActionStep {Operation=(string)s["operation"],ActorId=(string)s["actor_id"],TargetId=(string)s["target_id"],Amount=(double?)s["amount"]??1});
            }
            catch(Exception error){reply.Error=ModelHttp.SafeError(error);}
            return reply;
        }
    }
    public sealed class TypeSafeEvaluator:IPlanEvaluator
    {
        public const string Endpoint="https://api.typesafe.ai/v1/systemone";
        private readonly string key,model,endpoint;private readonly ModelTransport transport;
        public string Version {get {return (endpoint==Endpoint?"typesafe:":"local-typesafe-choice-contract:"+endpoint+":")+model;}}
        public bool Available {get {return !string.IsNullOrEmpty(key)&&key!="off";}}
        public long MaximumOutputTokens {get {return 512;}}
        public TypeSafeEvaluator(string key,string model="jev-latest",ModelTransport transport=null,string endpoint=null){this.key=key;this.model=model;this.endpoint=endpoint??Endpoint;this.transport=transport??ModelHttp.Send;}
        private string Body(PlanRequest request,ProviderReply proposal)
        {
            return JsonConvert.SerializeObject(new {model,state=new {request=JToken.Parse(request.Json()),proposed_steps=proposal.Steps},questions=new {plan=new {type="choice",instructions="Is this supported action composition suitable for the actor's actually observed state and goal? Execution will independently verify object IDs, affordances, proximity and state. Select reject for unsafe, unsupported or ungrounded actions. This is a training-model judgment, not a measured real-world accident probability.",criteria=new {execute="The supported operations fit the supplied observations and goal",reject="The proposed operations do not fit or information is insufficient"}}}});
        }
        public long EstimateInputTokens(PlanRequest request,ProviderReply proposal){return ModelHttp.Estimate(Body(request,proposal));}
        public async Task<ProviderReply> Evaluate(PlanRequest request,ProviderReply proposal,CancellationToken cancellation)
        {
            var reply=new ProviderReply {RequestId=request.Id,Steps=proposal.Steps,Approved=false};
            try
            {
                var response=await transport(endpoint,key,Body(request,proposal),cancellation).ConfigureAwait(false);reply.Raw=response.Body;
                if(response.Status<200||response.Status>=300){reply.Error="jev_http_"+response.Status;return reply;}
                var root=JObject.Parse(response.Body);reply.ReturnedModel=(string)root["model"]??model;
                reply.InputTokens=(long?)root["usage"]?["input_tokens"];reply.OutputTokens=(long?)root["usage"]?["output_tokens"];
                var choice=(string)root["answers"]?["plan"]?["choice"];
                if(choice!="execute"&&choice!="reject")reply.Error="invalid_jev_choice";reply.Approved=choice=="execute";
            }
            catch(Exception error){reply.Error=ModelHttp.SafeError(error);}
            return reply;
        }
    }
}
