using System;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Seeded;
using Newtonsoft.Json.Linq;

public static class SubscriptionTests
{
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static JObject Descriptor(){return new JObject{["schema"]=1,["endpoint"]="http://127.0.0.1:18953",["capability"]=new string('a',48),["browser_url"]="http://127.0.0.1:18953/#"+new string('b',48)};}
    static void Refused(JObject descriptor)
    {bool refused=false;try{SubscriptionConnection.ParseDescriptor(descriptor.ToString());}catch(ArgumentException){refused=true;}Check(refused,"unsafe descriptor refused");}
    public static void Run()
    {
        var d=Descriptor();d["endpoint"]="https://provider.example";Refused(d);
        d=Descriptor();d["endpoint"]="http://127.0.0.1:18953/?destination=other";Refused(d);
        d=Descriptor();d["browser_url"]="http://127.0.0.1:18953.evil.example/#"+new string('b',48);Refused(d);
        d=Descriptor();d["capability"]="tiny";Refused(d);
        d=Descriptor();d["capability"]=new string('a',32)+"\r\n";Refused(d);
        int calls=0;bool enabled=false,changed=false;
        var connection=SubscriptionConnection.ParseDescriptor(Descriptor().ToString(),(method,url,capability,generation,body,cancellation)=>
        {
            calls++;Check(capability==new string('a',48),"native requests use local capability only");
            if(method=="GET")return Task.FromResult(new ModelHttpReply{Status=200,Body=new JObject{["provider"]="openai-chatgpt",["connected"]=true,["cloud_enabled"]=enabled,["model"]="provider-model",["session_generation"]="session-1",
                ["selection"]=new JObject{["provider_id"]="openai-chatgpt",["account_id"]="account-1",["model_id"]="provider-model",["session_generation"]="session-1",["catalog_version"]=new string('a',64),["selection_version"]=ProviderSelection.Version,["billing"]="user_subscription",["registration"]="own_app_public_oauth"},
                ["providers"]=new JArray(new JObject{["id"]="openai-chatgpt",["registered"]=true,["billing"]="user_subscription",["auth_modes"]=new JArray("browser_oauth"),["capabilities"]=new JArray("initial_world","npc_plan")}),
                ["inference_contracts"]=new JObject{["openai-chatgpt"]=new string('d',64)},
                ["usage"]=new JObject{["requests"]=2,["input_tokens"]=40,["output_tokens"]=10}}.ToString()});
            Check(url=="http://127.0.0.1:18953/v1/chat/completions"&&generation=="session-1","fixed loopback bridge with immutable session generation");
            Check((string)JObject.Parse(body)["model"]=="provider-model","explicit account model used");
            Check((string)JObject.Parse(body)["chooguard_selection"]?["account_id"]=="account-1","exact account selection travels only to loopback helper");
            if(changed)return Task.FromResult(new ModelHttpReply{Status=409,Body="{\"error\":\"session_changed\"}"});
            return Task.FromResult(new ModelHttpReply{Status=200,Body="{\"model\":\"provider-model\",\"usage\":{\"prompt_tokens\":40,\"completion_tokens\":10},\"choices\":[{\"message\":{\"content\":\"{\\\"steps\\\":[{\\\"operation\\\":\\\"raise_alarm\\\",\\\"actor_id\\\":\\\"player\\\",\\\"target_id\\\":\\\"radio\\\"}]}\"}}]}"});
        });
        var status=connection.Refresh(CancellationToken.None).GetAwaiter().GetResult();
        Check(!status.Ready&&calls==1,"login alone does not enable cloud inference");
        bool denied=false;try{connection.CreateGenerator(status);}catch(InvalidOperationException){denied=true;}Check(denied&&calls==1,"no inference before explicit enable");
        enabled=true;status=connection.Refresh(CancellationToken.None).GetAwaiter().GetResult();Check(status.Ready&&status.InputTokens==40,"safe status and measured usage");
        var provider=connection.CreateGenerator(status,128);Check(!provider.Version.Contains(new string('a',48)),"local capability excluded from replay policy");
        var request=new PlanRequest{Id="request-1",ActorId="player",Goal="g",ContextJson="{}"};
        var reply=provider.Generate(request,CancellationToken.None).GetAwaiter().GetResult();Check(reply.Error==null&&reply.InputTokens==40&&reply.Steps.Count==1,"bridge reply supports actual planning contract");
        changed=true;reply=provider.Generate(request,CancellationToken.None).GetAwaiter().GetResult();Check(reply.Error=="generator_http_409","account/model change rejected without retry or API key fallback");
    }
}
