using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ChooGuard.Seeded;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// This deliberately has no Main: the normal CLI compiles every Tools source.
public static class ModelBenchmark
{
    const string Goal="Inspect power:platform. If isolate_power for power:platform is listed, execute ONLY that action. Otherwise execute ONLY move_to toward power:platform. Return exactly one supported step.";
    const string Nav="source-navigation-v1";
    sealed class Measurement
    {
        public string policy,observation_hash,request_hash,disposition,execution_reason;
        public int observation,dependency_count;
        public double latency_ms;
        public bool cache_hit,execution_valid;
        public long input_tokens,output_tokens,simulation_ticks_while_waiting;
        public ActionStep[] steps;
    }
    static string Option(string[] args,string key,string fallback)
    {int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
    static World Clone(World w)
    {
        var copy=new World{Seed=w.Seed,Tick=w.Tick,TopologyVersion=w.TopologyVersion,StepSeconds=w.StepSeconds,RateMultiplier=w.RateMultiplier};
        foreach(var e in w.Entities.Values)
        {
            var c=new Entity{Id=e.Id,Kind=e.Kind,ParentId=e.ParentId,X=e.X,Y=e.Y,Z=e.Z,Version=e.Version};c.Tags.UnionWith(e.Tags);c.Links.UnionWith(e.Links);foreach(var v in e.State)c.State.Add(v.Key,v.Value);copy.Entities.Add(c.Id,c);
        }
        foreach(var f in w.Fields)copy.Fields.Add(f.Key,f.Value);return copy;
    }
    static DecisionBudget Budget(double timeout)
    {return new DecisionBudget{MaxRequests=16,MaxInputTokens=160000,MaxOutputTokens=4096,MaxConcurrent=1,TimeoutSeconds=timeout};}
    static PlanningCoordinator Coordinator(OperationCatalog catalog,IPlanProvider provider,double timeout)
    {return new PlanningCoordinator(catalog,provider,budget:Budget(timeout)){CooldownTicks=0,MaximumOffers=40,PerceptionRadius=4};}
    static Measurement Observe(World snapshot,int index,string policy,PlanningCoordinator coordinator,World background,List<PlanningRecord> allRecords)
    {
        var watch=Stopwatch.StartNew();int before=coordinator.Records.Count;long hits=coordinator.Cache.Hits;long ticks=0;
        var candidate=coordinator.Request(snapshot,"player",Goal,Nav);bool cache=coordinator.Cache.Hits>hits;
        ActionPlan generated=null;
        if(!cache)
        {
            coordinator.Poll(snapshot,Nav);
            var kernel=new CausalKernel();
            // Run an independent live world at 10 ms wall-clock intervals while awaiting async inference.
            // Snapshot observations stay identical across policies, so model latency cannot alter their inputs.
            while(coordinator.Budget.InFlight>0)
            {
                kernel.Step(background);ticks++;Thread.Sleep(10);coordinator.Poll(snapshot,Nav);
            }
            coordinator.TryDequeue(out generated);candidate=generated;
        }
        var records=coordinator.Records.Skip(before).ToArray();allRecords.AddRange(records);
        string reason="no_accepted_plan";bool valid=false;
        if(candidate!=null)
        {
            var executionWorld=Clone(snapshot);var executor=new PlanExecutor(OperationCatalog.CreateDefault());
            valid=true;for(int i=0;i<candidate.Steps.Count;i++)if(!executor.TryExecute(executionWorld,candidate,i,Nav,out reason)){valid=false;break;}
        }
        return new Measurement{policy=policy,observation=index,observation_hash=snapshot.Digest(),request_hash=records.Length==0?null:Stable.Hash(records[0].RequestJson),
            disposition=cache?"cache_reused":records.LastOrDefault()?.Disposition??"no_model_request",cache_hit=cache,latency_ms=watch.Elapsed.TotalMilliseconds,
            input_tokens=records.Sum(r=>PlanningCoordinator.ReadRecordedReply(r).InputTokens??0),output_tokens=records.Sum(r=>PlanningCoordinator.ReadRecordedReply(r).OutputTokens??0),
            execution_valid=valid,execution_reason=reason,dependency_count=candidate?.Dependencies.Count??0,steps=candidate?.Steps.ToArray(),simulation_ticks_while_waiting=ticks};
    }
    static object Summary(IEnumerable<Measurement> measurements)
    {
        var rows=measurements.ToArray();var latency=rows.Where(x=>!x.cache_hit).Select(x=>x.latency_ms).OrderBy(x=>x).ToArray();
        Func<double,double> percentile=p=>latency.Length==0?0:latency[(int)Math.Ceiling(p*latency.Length)-1];
        return new{observations=rows.Length,generator_calls=rows.Count(x=>!x.cache_hit&&x.disposition!="no_model_request"),cache_hits=rows.Count(x=>x.cache_hit),
            measured_input_tokens=rows.Sum(x=>x.input_tokens),measured_output_tokens=rows.Sum(x=>x.output_tokens),valid_plans=rows.Count(x=>x.execution_valid),
            unsupported_plans=rows.Count(x=>x.disposition=="unsupported_reply"),failed_plans=rows.Count(x=>x.disposition.StartsWith("failed:")),stale_plans=rows.Count(x=>x.disposition.StartsWith("stale:")),
            p50_latency_ms=percentile(.5),p95_latency_ms=percentile(.95),local_cost=0,simulation_ticks_while_waiting=rows.Sum(x=>x.simulation_ticks_while_waiting)};
    }
    public static int Run(string[] args)
    {
        string output=Option(args,"--output","output/simulator/model-benchmark");Directory.CreateDirectory(output);
        string source=File.ReadAllText(Option(args,"--source","workers/simulator/data/platform-source.json"));
        string coverage=File.ReadAllText(Option(args,"--coverage","workers/simulator/data/semantic-coverage.json"));
        ulong seed=ulong.Parse(Option(args,"--seed","20261013"));double timeout=double.Parse(Option(args,"--timeout","120"),System.Globalization.CultureInfo.InvariantCulture);
        var world=PlatformWorldFactory.Create(source,seed);WorldCoverage.Apply(world,coverage);var power=world.Entities["power:platform"];var actor=world.Entities["player"];
        actor.X=power.X-.5;actor.Y=power.Y;actor.Z=power.Z;actor.Set("sight.radius",2);
        // Controlled supplied observer occlusion focuses this feasibility trial on one source-placed training prop.
        foreach(var e in world.Entities.Values)if(e.Id!="player"&&e.Id!=power.Id)actor.Set("occlusion."+e.Id,1);
        var observations=new List<World>();var interventions=new JArray();
        observations.Add(Clone(world));world.Tick++;observations.Add(Clone(world));
        var unrelated=world.Entities.Values.First(e=>e.Has("seat"));unrelated.Set("occupied",1);world.Entities["npc:0031"].Set("health",.6);world.Tick++;
        interventions.Add(new JObject{{"observation",2},{"kind","unrelated seat occupancy and distant NPC health"},{"seat",unrelated.Id},{"npc","npc:0031"}});observations.Add(Clone(world));
        power.Set("power",0);power.Set("power.isolated",1);world.Tick++;
        interventions.Add(new JObject{{"observation",3},{"kind","actual power isolation"},{"entity",power.Id},{"before_power",1},{"after_power",0}});observations.Add(Clone(world));world.Tick++;observations.Add(Clone(world));
        power.X+=.25;world.Tick++;interventions.Add(new JObject{{"observation",5},{"kind","actual selected target position change"},{"delta_x",.25}});observations.Add(Clone(world));
        string endpoint=Option(args,"--endpoint","http://127.0.0.1:18951/v1/chat/completions");var provider=new OpenAiCompatibleGenerator(endpoint,"default_model",maxOutputTokens:160);
        var catalog=OperationCatalog.CreateDefault();var records=new List<PlanningRecord>();var baseline=new List<Measurement>();var cached=new List<Measurement>();
        var background=PlatformWorldFactory.Create(source,seed);WorldCoverage.Apply(background,coverage);
        for(int i=0;i<observations.Count;i++)using(var coordinator=Coordinator(catalog,provider,timeout))
        {baseline.Add(Observe(observations[i],i,"repeated-state-baseline",coordinator,background,records));Console.WriteLine("baseline "+i+" "+baseline.Last().disposition+" "+baseline.Last().input_tokens+"/"+baseline.Last().output_tokens);}
        long invalidations,hits;
        using(var coordinator=Coordinator(catalog,provider,timeout))
        {
            for(int i=0;i<observations.Count;i++){cached.Add(Observe(observations[i],i,"salient-event-dependency-cache",coordinator,background,records));Console.WriteLine("cached "+i+" "+cached.Last().disposition+" "+cached.Last().input_tokens+"/"+cached.Last().output_tokens);}
            invalidations=coordinator.Cache.Invalidations;hits=coordinator.Cache.Hits;
        }
        var trace=new{seed,source_hash=Stable.Hash(source),coverage_hash=Stable.Hash(coverage),observation_hashes=observations.Select(x=>x.Digest()).ToArray(),interventions,baseline,cached};
        string traceJson=JsonConvert.SerializeObject(trace,Formatting.Indented);File.WriteAllText(Path.Combine(output,"paired-trace.json"),traceJson);
        File.WriteAllText(Path.Combine(output,"planning-records.json"),JsonConvert.SerializeObject(records,Formatting.Indented));
        var all=baseline.Concat(cached).ToArray();var summary=new JObject{{"status",all.Any(r=>r.execution_valid)&&all.Sum(r=>r.input_tokens)>0?"measured":"failed"},{"actual_generator_calls",records.Count(r=>r.Stage=="generator")},
            {"measured_input_tokens",all.Sum(r=>r.input_tokens)},{"measured_output_tokens",all.Sum(r=>r.output_tokens)},{"local_cost",0},{"live_jev_status","notcalled"},
            {"baseline",JObject.FromObject(Summary(baseline))},{"event_cache",JObject.FromObject(Summary(cached))},{"cache_invalidations",invalidations},{"cache_hits",hits},
            {"source_hash",Stable.Hash(source)},{"coverage_hash",Stable.Hash(coverage)},{"paired_trace_hash",Stable.Hash(traceJson)},
            {"config",JObject.FromObject(new{seed,endpoint,model="default_model",maximum_output_tokens=160,timeout_seconds=timeout,maximum_concurrency=1,observations=observations.Count,initial_cache="empty for each policy",observer="controlled supplied occlusion of unrelated entities",execution="guarded executor on independent snapshot copy",background="causal simulation runs independently during every async wait",concurrent_load="shared Unity and isolated Unity/Blender may coexist; wall latency is machine-load-dependent"})},
            {"claim","Small local model feasibility and matched-observation accounting; not railway response expertise, training effectiveness, TypeSafe JEV quality, or paid-service cost savings."}};
        File.WriteAllText(Path.Combine(output,"results.json"),summary.ToString(Formatting.Indented)+"\n");Console.WriteLine(summary.ToString(Formatting.None));return (string)summary["status"]=="measured"?0:1;
    }
}
