using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using ChooGuard.Seeded;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class SimulatorCli
{
    static string implementation;
    static string defaultCoverage;
    static string Option(string[] args, string key, string fallback = null)
    {
        int i=Array.IndexOf(args,key); return i>=0 && i+1<args.Length ? args[i+1] : fallback;
    }
    public static int Main(string[] args)
    {
        try
        {
            string command=args.Length==0 ? "test" : args[0];
            if(command=="test") { Test(); return 0; }
            implementation=Option(args,"--implementation-sha");
            if(implementation==null||implementation.Length!=64||implementation.Any(c=>!((c>='0'&&c<='9')||(c>='a'&&c<='f'))))throw new ArgumentException("Current source implementation hash is required; use run_checks.py");
            string source=Option(args,"--source","workers/simulator/data/platform-source.json");
            ulong seed=ulong.Parse(Option(args,"--seed","20261013"),System.Globalization.CultureInfo.InvariantCulture);
            int ticks=int.Parse(Option(args,"--ticks","1200"));
            if(ticks<1 || ticks>100000) throw new ArgumentException("ticks must be in 1..100000");
            string output=Option(args,"--output", "output/simulator/checks/"+command+".jsonl");
            if(command=="run") Run(source,seed,ticks,output,Option(args,"--input"));
            else if(command=="replay") Replay(Option(args,"--input") ?? throw new ArgumentException("--input required"));
            else if(command=="benchmark") Benchmark(source,seed,ticks,output);
            else throw new ArgumentException("Unknown command");
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e.ToString()); return 1; }
    }
    static string CoverageJson()
    {
        if(defaultCoverage==null)
        {
            defaultCoverage=File.ReadAllText("workers/simulator/data/semantic-coverage.json");
            WorldCoverage.VerifySources(WorldCoverage.Parse(defaultCoverage),Directory.GetCurrentDirectory());
        }
        return defaultCoverage;
    }
    static World Create(string json,ulong seed,string coverage=null)=>PlatformWorldFactory.Create(json,coverage??CoverageJson(),seed,32);
    static void Test()
    {
        int count=0;
        foreach(var type in typeof(World).Assembly.GetTypes().OrderBy(t=>t.FullName,StringComparer.Ordinal))
        {
            if(!type.Name.EndsWith("Tests",StringComparison.Ordinal)||!(type.Namespace=="ChooGuard.Seeded.Tests"||type.Namespace=="ChooGuard.Seeded.Planning.Tests"||type==typeof(SubscriptionTests))) continue;
            var run=type.GetMethod("Run",BindingFlags.Public|BindingFlags.Static);
            if(run==null) throw new InvalidOperationException(type.FullName+" missing public static Run()");
            run.Invoke(null,null); count++; Console.WriteLine("PASS "+type.FullName);
        }
        foreach(var required in new[]{"KernelTests","PlanningTests","CoverageTests","SubscriptionTests","EmergenceGenerationTests","WorldObjectRegistryTests"})
            if(!typeof(World).Assembly.GetTypes().Any(t=>t.Name==required&&(t.Namespace=="ChooGuard.Seeded.Tests"||t.Namespace=="ChooGuard.Seeded.Planning.Tests"||t==typeof(SubscriptionTests))))
                throw new InvalidOperationException("Missing an independent test suite: "+required);
        string json=File.ReadAllText("workers/simulator/data/platform-source.json");
        var a=Create(json,42); var b=Create(json,42); var c=Create(json,43);
        var kernel=new CausalKernel(); var community=new Community();
        for(int i=0;i<120;i++)
        {
            kernel.Step(a); community.Step(a); kernel.Step(b); community.Step(b); kernel.Step(c); community.Step(c);
            if(a.Digest()!=b.Digest()) throw new InvalidOperationException("Source-based replay diverged at tick "+a.Tick);
        }
        if(a.Digest()==c.Digest()) throw new InvalidOperationException("Source world ignores seed");
        Console.WriteLine("PASS source-based fixed-tick replay (120 ticks), different-seed divergence");
    }
    static object TickRecord(World w,int start,JArray commands=null,JArray outcomes=null)
    {
        return new { kind="tick",tick=w.Tick,digest=w.Digest(),commands=commands??new JArray(),outcomes=outcomes??new JArray(),transitions=w.Journal.Skip(start).ToArray() };
    }
    static JArray ApplyInputs(World world,JArray commands)
    {
        var outcomes=new JArray();var catalog=OperationCatalog.CreateDefault();var executor=new PlanExecutor(catalog);
        foreach(JObject command in commands)
        {
            string reason="", status="rejected";
            try
            {
                var step=new ActionStep {ActorId=(string)command["actor"],TargetId=(string)command["target"],Operation=(string)command["operation"],Amount=(double?)command["amount"]??1};
                var plan=ActionPlan.Capture(world,"player-command","input","v1",OperationCatalog.Version,"headless-no-navigation",new[]{step});
                status=executor.TryExecute(world,plan,0,"headless-no-navigation",out reason)?"applied":"rejected";
            }
            catch(ArgumentException e) {reason=e.Message;}
            outcomes.Add(new JObject { ["status"]=status,["reason"]=reason });
        }
        return outcomes;
    }
    static void Run(string source,ulong seed,int ticks,string output,string input)
    {
        string json=File.ReadAllText(source),coverage=CoverageJson(); var w=Create(json,seed,coverage);
        var inputs=new SortedDictionary<long,JArray>();
        if(input!=null) foreach(string line in File.ReadLines(input))
        {
            if(string.IsNullOrWhiteSpace(line)) continue;
            var record=JObject.Parse(line); long tick=(long)record["tick"];
            if(tick<0||tick>=ticks) throw new ArgumentException("Input tick outside run");
            if(!inputs.ContainsKey(tick))inputs[tick]=new JArray(); inputs[tick].Add(record);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        var watch=Stopwatch.StartNew(); var kernel=new CausalKernel(); var community=new Community();
        using(var file=new StreamWriter(output))
        {
            file.WriteLine(JsonConvert.SerializeObject(new {kind="header",engine=World.EngineVersion,rng=Stable.RandomVersion,implementation_sha256=implementation,seed,step=w.StepSeconds,sourceHash=Stable.Hash(json),source=json,coverage_hash=Stable.Hash(coverage),coverage,initialDigest=w.Digest(),policy="local-baseline",modelCalls=0}));
            for(int i=0;i<ticks;i++)
            {
                int before=w.Journal.Count;JArray commands; if(!inputs.TryGetValue(w.Tick,out commands))commands=new JArray();
                var outcomes=ApplyInputs(w,commands);kernel.Step(w);community.Step(w);
                file.WriteLine(JsonConvert.SerializeObject(TickRecord(w,before,commands,outcomes)));
            }
        }
        Console.WriteLine(JsonConvert.SerializeObject(new {output,ticks,entities=w.Entities.Count,stateUpdates=w.Journal.Count,incidentOnsets=Onsets(w),elapsedMs=watch.ElapsedMilliseconds,finalDigest=w.Digest(),policy="local-baseline",modelCalls=0,claim="Kernel state replay; does not verify Unity physics, NavMesh, real incident frequency or model cost savings."}));
    }
    static void Replay(string input)
    {
        using(var lines=new StreamReader(input))
        {
            var header=JObject.Parse(lines.ReadLine());
            if((string)header["engine"]!=World.EngineVersion || (string)header["rng"]!=Stable.RandomVersion) throw new InvalidOperationException("Replay version mismatch");
            if((string)header["implementation_sha256"]!=implementation)throw new InvalidOperationException("Replay source implementation mismatch; record with the current build. Older unversioned tapes are retained as history.");
            string source=(string)header["source"];
            if(Stable.Hash(source)!=(string)header["sourceHash"]) throw new InvalidOperationException("Source hash mismatch");
            string coverage=(string)header["coverage"];
            if(coverage==null||Stable.Hash(coverage)!=(string)header["coverage_hash"])throw new InvalidOperationException("Coverage hash mismatch");
            WorldCoverage.VerifySources(WorldCoverage.Parse(coverage),Directory.GetCurrentDirectory());
            var w=Create(source,(ulong)header["seed"],coverage);
            if(w.Digest()!=(string)header["initialDigest"]) throw new InvalidOperationException("Initial state mismatch");
            var kernel=new CausalKernel(); var community=new Community(); int count=0;
            string line;
            while((line=lines.ReadLine())!=null)
            {
                var expected=JObject.Parse(line); int before=w.Journal.Count;
                var commands=(JArray)expected["commands"]??new JArray();var outcomes=ApplyInputs(w,commands);
                kernel.Step(w);community.Step(w);count++;
                if((long)expected["tick"]!=w.Tick || (string)expected["digest"]!=w.Digest()) throw new InvalidOperationException("First divergent tick "+w.Tick);
                if(!JToken.DeepEquals(outcomes,expected["outcomes"]??new JArray()))throw new InvalidOperationException("Input outcome differs at tick "+w.Tick);
                var actual=JObject.FromObject(TickRecord(w,before,commands,outcomes));
                if(!JToken.DeepEquals(actual["transitions"],expected["transitions"])) throw new InvalidOperationException("Transition order differs at tick "+w.Tick);
            }
            Console.WriteLine(JsonConvert.SerializeObject(new {replay="PASS",ticks=count,modelCalls=0,finalDigest=w.Digest(),scope="engine-free state resimulation, not trajectory playback"}));
        }
    }
    static void Benchmark(string source,ulong seed,int ticks,string output)
    {
        string json=File.ReadAllText(source); var rows=new List<object>();
        for(ulong n=0;n<8;n++)
        {
            var w=Create(json,seed+n); var kernel=new CausalKernel(); var community=new Community(); var watch=Stopwatch.StartNew();
            for(int i=0;i<ticks;i++) {kernel.Step(w);community.Step(w);}
            rows.Add(new {seed=seed+n,ticks,entities=w.Entities.Count,stateUpdates=w.Journal.Count,incidentOnsets=Onsets(w),updateDomains=w.Journal.GroupBy(t=>t.Domain).ToDictionary(g=>g.Key,g=>g.Count()),mechanisms=w.Journal.Select(t=>t.Mechanism).Distinct().Count(),elapsedMs=watch.ElapsedMilliseconds,finalDigest=w.Digest()});
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output,JsonConvert.SerializeObject(new {policy="local-baseline",modelCalls=0,claim="Source-coordinate integration smoke benchmark; game parameters are not measured real-world frequencies. API cost savings and behavioral validity unmeasured.",runs=rows},Formatting.Indented));
        Console.WriteLine("Wrote "+output);
    }
    static object Onsets(World world)
    {
        var keys=new HashSet<string>(new[]{"fire","fault","failed","medical.condition","fallen","regional-alert","communications-disruption"},StringComparer.Ordinal);
        return world.Journal.Where(t=>keys.Contains(t.Property)&&t.Before<=0&&t.After>0).GroupBy(t=>t.Property).ToDictionary(g=>g.Key,g=>g.Count());
    }
}
