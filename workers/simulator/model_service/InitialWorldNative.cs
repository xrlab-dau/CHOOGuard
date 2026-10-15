using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ChooGuard.Seeded;

public static class InitialWorldNative
{
    internal static JObject Load(string path)
    {
        string text=File.ReadAllText(path);
        // This decoder applies only to the new local FPS tape. Ordinary model/request/state
        // JSON remains unchanged; raw legacy tapes are never silently promoted.
        return path.EndsWith(".jsonl",StringComparison.Ordinal)?LocalTapeCodec.Parse(text.Split('\n')[0]):JObject.Parse(text);
    }
    public static int Main(string[] args)
    {
        try
        {
            string mode=args[0],directory=args[1];Directory.CreateDirectory(directory);
            var document=Load(args[2]);
            var world=WorldStateCodec.FromToken(document["initial_state"]??document);string basis=world.Digest();
            if(args.Length>6&&args[6]=="restore-fixture-rain")
            {
                var changes=document["emergence_generation"]?["Changes"] as JArray;
                if(mode!="export"||changes==null||changes.Count!=1||(string)changes[0]["EntityId"]!="@world"||(string)changes[0]["Property"]!="rain"||(double)changes[0]["Value"]!=.73)throw new ArgumentException("Only the recorded one-field offline fixture may be inverted");
                world.Fields["rain"]=Stable.Unit(world.Seed,"environment",0,"rain");basis=world.Digest();
            }
            if(basis!=args[4])throw new ArgumentException("Initial world does not match the recorded basis: "+basis);
            var coverage=WorldCoverage.Parse(File.ReadAllText(args[3]));WorldCoverage.VerifySources(coverage,Directory.GetCurrentDirectory());
            if(coverage.physical_entities.Concat(coverage.navigation_points).Any(entity=>!world.Entities.ContainsKey(entity.id)))throw new ArgumentException("Recorded world omits current source-qualified entities");
            var request=EmergenceComposer.CreateRequest(world,coverage);
            File.WriteAllText(Path.Combine(directory,"whole-world-request.json"),request.Json+"\n");
            File.WriteAllText(Path.Combine(directory,"whole-world-baseline.json"),WorldStateCodec.Serialize(world)+"\n");
            var graph=(JObject)JObject.Parse(request.Json)["graph"];var legacy=JObject.Parse(EmergenceComposer.CreateRequest(world,coverage,false).Json);
            if(!JToken.DeepEquals(legacy["graph"],EmergenceGraphCodec.Decode(graph)))throw new ArgumentException("Graph roundtrip failed");
            var report=new JObject{["basis_digest"]=basis,["seed"]=world.Seed.ToString(),["entity_count"]=world.Entities.Count,["visual_count"]=graph["visual_count"],
                ["identity_count"]=EmergenceGraphCodec.Identities(graph).Length,["graph_roundtrip_exact"]=true,["original_request_utf8_bytes"]=System.Text.Encoding.UTF8.GetByteCount(legacy.ToString(Formatting.None)),
                ["compact_request_utf8_bytes"]=System.Text.Encoding.UTF8.GetByteCount(request.Json),["network_calls"]=0,["mode"]=mode};
            if(mode=="apply")
            {
                string response=File.ReadAllText(args[5]);ModelTransport recorded=(u,k,b,c)=>Task.FromResult(new ModelHttpReply{Status=200,Body=response});
                var provider=new OpenAiCompatibleEmergenceProvider("http://127.0.0.1:1/v1/chat/completions","recorded-offline",transport:recorded);
                var record=new EmergenceComposer(provider).GenerateAndApplyAsync(world,false,coverage,authorized:true).GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(directory,"native-generation-record.json"),JsonConvert.SerializeObject(record,Formatting.Indented));
                if(record.Disposition!="accepted"&&record.Disposition!="accepted_no_change")throw new ArgumentException("Native model reply refused: "+record.Disposition);
                var replay=WorldStateCodec.FromToken(document["initial_state"]??document);string reason;
                if(!EmergenceComposer.ValidateAndApplyRecorded(replay,record,false,out reason)||world.Digest()!=replay.Digest())throw new ArgumentException("Native initial replay refused: "+reason);
                var kernel=new CausalKernel();for(int i=0;i<40;i++){kernel.Step(world);kernel.Step(replay);if(world.Digest()!=replay.Digest())throw new ArgumentException("Native causal replay diverged");}
                report["disposition"]=record.Disposition;report["change_count"]=record.Changes.Count;report["post_digest"]=record.PostDigest;report["replayed_ticks"]=40;report["replay_digest"]=replay.Digest();
                report["reported_input_tokens"]=record.InputTokens;report["reported_output_tokens"]=record.OutputTokens;
            }
            else if(mode!="export")throw new ArgumentException("Unknown mode");
            File.WriteAllText(Path.Combine(directory,"native-context-report.json"),report.ToString(Formatting.Indented)+"\n");Console.WriteLine(report.ToString(Formatting.None));return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
    }
}
