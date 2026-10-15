using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ChooGuard.Seeded;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class LocalChoiceEvaluation
{
    public static int Main(string[] args)
    {
        string path=args.Length>0?args[0]:"output/simulator/model-benchmark";
        var record=JsonConvert.DeserializeObject<List<PlanningRecord>>(File.ReadAllText(Path.Combine(path,"planning-records.json"))).First(r=>r.Stage=="generator"&&r.Disposition=="accepted");
        var captured=JObject.Parse(record.RequestJson);
        var request=new PlanRequest{Id=(string)captured["request_id"],ActorId=(string)captured["actor_id"],Goal=(string)captured["goal"],Tick=(long)captured["tick"],ContextJson=captured["context"].ToString(Formatting.None)};
        request.Offers.AddRange(captured["allowed_operations"].ToObject<List<OfferedAction>>());
        var proposal=PlanningCoordinator.ReadRecordedReply(record);
        var evaluator=new TypeSafeEvaluator("local-contract",model:"default_model",endpoint:"http://127.0.0.1:18951/v1/systemone");
        var timer=Stopwatch.StartNew();ProviderReply reply;
        using(var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(180)))reply=evaluator.Evaluate(request,proposal,cancel.Token).GetAwaiter().GetResult();
        var result=new{status=reply.Error==null?"measured":"failed",live_jev_status="notcalled",evaluator="local MLX Qwen Choice contract; not TypeSafe JEV",latency_ms=timer.Elapsed.TotalMilliseconds,
            measured_input_tokens=reply.InputTokens,measured_output_tokens=reply.OutputTokens,local_cost=0,approved=reply.Approved,error=reply.Error,raw=reply.Raw,
            request_hash=Stable.Hash(record.RequestJson),proposal_hash=Stable.Hash(record.ReplyJson),claim="Actual C# TypeSafeEvaluator transport/Choice parsing with a local small-model judgment. Does not establish TypeSafe model quality or economics."};
        File.WriteAllText(Path.Combine(path,"local-choice-evaluation.json"),JsonConvert.SerializeObject(result,Formatting.Indented)+"\n");Console.WriteLine(JsonConvert.SerializeObject(result));return reply.Error==null?0:1;
    }
}
