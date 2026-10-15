using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChooGuard.Seeded;
public static class CompleteProviderInputProbe {
public static int Main(string[] args) { try { Run(args).GetAwaiter().GetResult(); return 0; }
catch(Exception e) { Console.Error.WriteLine(e.ToString()); return 1; } }
static async Task Run(string[] args) {
var header=LocalTapeCodec.ParseJsonData(File.ReadLines(args[0]).First());
var world=WorldStateCodec.FromToken(header["initial_state"]);
if(world.Entities.Count!=5105 || world.Tick!=0)throw new Exception("Exact historical 5105 initial state required");
var rows=new JArray();
for(int i=1;i<args.Length-1;i++) {
var coverage=WorldCoverage.Parse(File.ReadAllText(args[i]));
var baseline=EmergenceComposer.CreateRequest(world,coverage,false);
var compact=EmergenceComposer.CreateRequest(world,coverage,true);
var original=(JObject)JObject.Parse(baseline.Json)["graph"];
var decoded=EmergenceGraphCodec.Decode((JObject)JObject.Parse(compact.Json)["graph"]);
if(!JToken.DeepEquals(original["entities"],decoded["entities"]) ||
!JToken.DeepEquals(original["visuals"],decoded["visuals"]) ||
!JToken.DeepEquals(original["link_sets"],decoded["link_sets"]) ||
!JToken.DeepEquals(original["state_schemas"],decoded["state_schemas"]))
throw new Exception("Complete graph roundtrip mismatch");
foreach(var variant in new[]{baseline,compact}) {
string body=null;
ModelTransport capture=(endpoint,key,payload,ct)=> {
if(key!=null)throw new Exception("No developer key allowed"); body=payload;
return Task.FromResult(new ModelHttpReply{Status=200,Body=JsonConvert.SerializeObject(new{
model="gpt-6.1-sol",usage=new{prompt_tokens=0,completion_tokens=0},choices=new[]{new{
message=new{content=JsonConvert.SerializeObject(new{basis_digest=variant.BasisDigest,seed=variant.Seed.ToString(),priors=new object[0]})}}}})}); };
var provider=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","gpt-6.1-sol",null,512,capture);
var reply=await provider.Generate(variant,CancellationToken.None);
if(body==null || reply.Error!=null)throw new Exception("Production capture failed: "+reply.Error);
var payload=JObject.Parse(body);var messages=(JArray)payload["messages"];
long helperEstimate=messages.Sum(m=>(long)Encoding.UTF8.GetByteCount((string)m["content"])+32)+64;
long csharpEstimate=provider.EstimateInputTokens(variant);
string name=Path.GetFileNameWithoutExtension(args[i])+"-"+(ReferenceEquals(variant,compact)?"compact":"raw");
File.WriteAllText(Path.Combine(args.Last(),name+".body.json"),body,new UTF8Encoding(false));
rows.Add(new JObject{["variant"]=name,["world_entities"]=world.Entities.Count,["visual_rows"]=coverage.visual_parts.Length,
["graph_encoding"]=(string)JObject.Parse(variant.Json)["graph"]["encoding"]??"raw",
["graph_roundtrip_preserved"]=true,["body_utf8_bytes"]=Encoding.UTF8.GetByteCount(body),
["client_model_input_sha256"]=provider.InputFingerprint(variant),["request_utf8_bytes"]=Encoding.UTF8.GetByteCount(variant.Json),
["csharp_reservation_units"]=csharpEstimate,["helper_reservation_units"]=helperEstimate,
["csharp_256000_admits"]=csharpEstimate<=256000,["helper_default_192000_admits"]=helperEstimate<=192000,
["helper_max_256000_admits"]=helperEstimate<=256000,["output_tokens_requested"]=512,
["coverage_sha256"]=Stable.Hash(File.ReadAllText(args[i])),["world_digest"]=world.Digest(),
["source_epoch_claim"]="Historical frozen ec2f 5105-state baseline with current source serializer; not current original/Unity proof"});
}
}
Console.WriteLine(rows.ToString(Formatting.None));
}
}