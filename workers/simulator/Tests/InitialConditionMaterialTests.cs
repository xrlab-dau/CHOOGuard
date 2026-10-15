using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class InitialConditionMaterialTests
    {
        static void Check(bool value,string reason){if(!value)throw new Exception("Seeded material contract: "+reason);}
        static void Reject(Action action,string reason){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(JsonException){rejected=true;}Check(rejected,reason);}
        static World Fixture(ulong seed=7)
        {
            var w=new World{Seed=seed};w.Fields["ambient.temperature"]=20;w.Fields["environment.autonomous"]=0;w.Fields["source.scene.sha256.word0"]=123;
            var p=new Entity{Id="power|한글",Kind="cabinet",X=1};p.Tags.UnionWith(new[]{"semantic","electrical","power-source","self-powered","thermal"});p.Set("temperature",20);p.Set("insulation",1);p.Set("wear",0);w.Add(p);
            var n=new Entity{Id="npc",Kind="person"};n.Tags.Add("npc");n.Set("health",1);w.Add(n);
            var unknown=new Entity{Id="unknown",Kind="opaque-source",ParentId=p.Id};unknown.Tags.UnionWith(new[]{"source-input","render-only","decoration"});w.Add(unknown);return w;
        }
        static InitialConditionMaterial Material(World w)=>InitialConditionMaterial.Create(w,new[]{new InitialConditionPrior("power|한글","insulation",0,.4),new InitialConditionPrior("power|한글","wetness",.3,1)},Stable.Hash("fixture logical binding"));
        public static void Run()
        {
            var w=Fixture();var material=Material(w);var serialized=material.Serialize();var parsed=InitialConditionMaterial.Parse(serialized);
            Check(parsed.ContentSha256==material.ContentSha256&&parsed.Serialize()==serialized,"immutable canonical material exact roundtrip");
            var reversed=InitialConditionMaterial.Create(w,material.Priors.Reverse(),material.ProviderBindingFingerprint);Check(reversed.ContentSha256==material.ContentSha256,"proposal ordering does not alter material identity");
            var seedChanged=Fixture(8);seedChanged.Entities["npc"].X=91;seedChanged.Entities["npc"].Set("health",.6);
            string reason;Check(material.CanApply(seedChanged,out reason),"same epoch/schema with new seed/NPC pose reuses frozen causal source");
            var values=new HashSet<string>();for(ulong seed=1;seed<=32;seed++){var initial=Fixture(seed);values.Add(JsonConvert.SerializeObject(material.Sample(initial)));}
            Check(values.Count==32,"local seeds diversify actual bounded values from ONE material, no fresh model response required");
            var a=Fixture();var b=Fixture();var record=EmergenceComposer.MaterializeAndApply(a,material);
            Check(record.Disposition=="accepted"&&record.MaterialSha256==material.ContentSha256&&record.InputTokens==null&&record.ReservedInputTokens==0,"offline material application performs zero inference reservations");
            Check(EmergenceComposer.ValidateAndApplyRecorded(b,JsonConvert.DeserializeObject<EmergenceRecord>(JsonConvert.SerializeObject(record)),false,out reason)&&a.Digest()==b.Digest(),"recorded material and samples reproduce exact initial digest");
            var altered=JsonConvert.DeserializeObject<EmergenceRecord>(JsonConvert.SerializeObject(record));altered.Changes[0].Value+=.01;
            var untouched=Fixture();string before=untouched.Digest();Check(!EmergenceComposer.ValidateAndApplyRecorded(untouched,altered,false,out reason)&&untouched.Digest()==before,"tampered recorded sample rejected atomically");
            var epoch=Fixture();epoch.Fields["source.scene.sha256.word0"]++;Check(!material.CanApply(epoch,out reason),"changed source epoch rejects reusable material");
            var geometry=Fixture();geometry.Entities["power|한글"].X++;Check(!material.CanApply(geometry,out reason),"static geometry change rejects material");
            var capability=Fixture();capability.Entities["power|한글"].Tags.Remove("electrical");Check(!material.CanApply(capability,out reason),"changed capability cannot reuse material");
            Reject(()=>InitialConditionMaterial.Create(w,new[]{new InitialConditionPrior("unknown","temperature",20,30)},material.ProviderBindingFingerprint),"all-source input does not grant unknown hazard capability");
            Reject(()=>InitialConditionMaterial.Create(w,new[]{new InitialConditionPrior("power|한글","fire",0,1)},material.ProviderBindingFingerprint),"direct outcomes forbidden");
            Reject(()=>InitialConditionMaterial.Create(w,new[]{new InitialConditionPrior("power|한글","wetness",0,2)},material.ProviderBindingFingerprint),"out-of-range prior forbidden");
            Reject(()=>InitialConditionMaterial.Create(w,new[]{new InitialConditionPrior("power|한글","wetness",double.NaN,1)},material.ProviderBindingFingerprint),"nonfinite prior forbidden");
            Reject(()=>InitialConditionMaterial.Create(w,material.Priors.Concat(material.Priors),material.ProviderBindingFingerprint),"duplicate prior forbidden");
            var tamper=JObject.Parse(serialized);tamper["priors"][0]["lower"]=InitialConditionMaterial.FloatBits(.1);Reject(()=>InitialConditionMaterial.Parse(tamper.ToString()),"material content hash mismatch forbidden");
            DistributionAndCanonical();TimeStepSensitivity();RecordedConfiguration();VersionAndSignedZero();AtomicPreflight();FullWorldReplay();
        }
        static void DistributionAndCanonical()
        {
            Check(Stable.Unit(1,"a|0",1,"x",0)!=Stable.Unit(1,"a",0,"1|x",0),"typed RNG tuple prevents delimiter collisions");
            var a=Fixture();var b=Fixture();a.Entities["power|한글"].Kind="x:y";a.Entities["power|한글"].ParentId="z";b.Entities["power|한글"].Kind="y";b.Entities["power|한글"].ParentId="z:x";
            Check(a.Digest()!=b.Digest(),"typed digest prevents string-boundary collision");
            a=Fixture();b=Fixture();a.Entities["npc"].Z=BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));Check(a.Digest()!=b.Digest(),"exact digest preserves negative zero bits");
            const int n=4096;const double alpha=.001;double tolerance=Math.Sqrt(Math.Log(2*9/alpha)/(2*n));
            // Nine combinations and alpha were declared before observing this fixture.
            double[] rates={.02,.7,2.0},multipliers={.5,1.0,4.0};
            for(int caseIndex=0;caseIndex<rates.Length;caseIndex++)foreach(double dt in new[]{.125,.5,1.0})
            {
                double caseRate=rates[caseIndex],caseMultiplier=multipliers[caseIndex];int observed=0;
                for(ulong seed=1;seed<=(ulong)n;seed++){var world=new World{Seed=seed,StepSeconds=dt,RateMultiplier=caseMultiplier};if(Stable.Occurs(world,"fixture.bernoulli","source",caseRate))observed++;}
                double p=1-Math.Exp(-caseRate*dt*caseMultiplier);
                Check(Math.Abs(observed/(double)n-p)<=tolerance,"predeclared Hoeffding probability threshold at r="+caseRate+" dt="+dt+" m="+caseMultiplier);
                Console.WriteLine("RNG probability fixture r="+caseRate+" dt="+dt+" m="+caseMultiplier+" n="+n+" observed="+observed+" p="+p+" tolerance="+tolerance+" family alpha=.001; no field calibration.");
            }
            for(ulong seed=1;seed<=(ulong)n;seed++)Check(!Stable.Occurs(new World{Seed=seed},"fixture.zero","source",0),"zero rate always quiescent");
            double rate=.7,multiplier=1.3;
            // Expected no-event survival over the same duration is invariant algebraically;
            // different time steps are distinct numerical inputs, not bit-identical trajectories.
            double duration=4;Check(Math.Abs(Math.Pow(Math.Exp(-rate*.125*multiplier),duration/.125)-Math.Exp(-rate*duration*multiplier))<1e-12,"hazard survival probability time-step composition");
        }
        static void TimeStepSensitivity()
        {
            const double horizon=4;double exact=20+50*(1-Math.Exp(-.1*horizon));var errors=new List<double>();
            foreach(double dt in new[]{1.0,.5,.125})
            {
                var w=new World{Seed=23,StepSeconds=dt};w.Fields["environment.autonomous"]=0;w.Fields["ambient.temperature"]=20;
                var body=new Entity{Id="constant-source",Kind="synthetic-thermal"};body.Tags.UnionWith(new[]{"semantic","heat-source","thermal"});body.Set("temperature",20);body.Set("heat.output",5);body.Set("cooling",.1);w.Add(body);
                for(int step=0;step<(int)(horizon/dt);step++)new CausalKernel().Step(w);
                double error=Math.Abs(body.Get("temperature")-exact);errors.Add(error);Check(error<1,"explicit Euler thermal sensitivity bounded at dt="+dt);
                Console.WriteLine("Synthetic thermal dt sensitivity horizon4 dt="+dt+" temperature="+body.Get("temperature")+" analytic="+exact+" abs_error="+error+"; arbitrary game units, no measured material claim.");
            }
            Check(errors[2]<errors[1]&&errors[1]<errors[0],"finer fixed steps converge to the declared constant-source cooling model");
        }
        static void RecordedConfiguration()
        {
            foreach(double rate in new[]{0.0,1.0,4.0,.7123456789012345})
            {
                var world=Fixture();world.RateMultiplier=rate;var config=new JObject{["step_seconds"]=InitialConditionMaterial.FloatBits(world.StepSeconds),["rate_multiplier"]=InitialConditionMaterial.FloatBits(rate)};
                ReplayConfiguration.Validate(JObject.Parse(config.ToString()),WorldStateCodec.FromToken(JObject.Parse(WorldStateCodec.Serialize(world))));
                var mismatch=WorldStateCodec.Clone(world);mismatch.RateMultiplier=rate==4?3:rate+.1;Reject(()=>ReplayConfiguration.Validate(config,mismatch),"recorded config/state mismatch refused");
            }
            foreach(JToken bad in new JToken[]{null,JValue.CreateNull(),new JValue(5),new JValue(-1),new JValue("f64:7ff0000000000000"),new JValue("invalid")})
            {var config=new JObject{["step_seconds"]="f64:3fe0000000000000"};if(bad!=null)config["rate_multiplier"]=bad;Reject(()=>ReplayConfiguration.Validate(config,Fixture()),"missing/nonfinite/out-of-range recorded rate refused");}
            var wrongStep=new JObject{["step_seconds"]=InitialConditionMaterial.FloatBits(1),["rate_multiplier"]=InitialConditionMaterial.FloatBits(1)};Reject(()=>ReplayConfiguration.Validate(wrongStep,Fixture()),"native fixed step guard retained");
        }
        static void VersionAndSignedZero()
        {
            foreach(long version in new[]{long.MaxValue,long.MaxValue-1})
            {
                var world=Fixture();world.Entities["power|한글"].Version=version;
                var material=InitialConditionMaterial.Create(world,new[]{new InitialConditionPrior("power|한글","insulation",.2,.2),new InitialConditionPrior("power|한글","wetness",.6,.6)},Stable.Hash("version fixture"));
                string before=world.Digest();var record=EmergenceComposer.MaterializeAndApply(world,material);
                Check(record.Disposition=="rejected_entity_version_overflow"&&world.Digest()==before,"all cumulative initial writes refuse version overflow without partial mutation");
            }
            var direct=Fixture().Entities["power|한글"];direct.Version=long.MaxValue;double old=direct.Get("insulation");Reject(()=>direct.Set("insulation",.3),"single Entity.Set refuses overflow before state write");Check(direct.Get("insulation")==old&&direct.Version==long.MaxValue,"single write remains unchanged");
            var signed=Fixture();signed.Entities["power|한글"].Set("wetness",0);double minus=BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000UL));
            var zero=InitialConditionMaterial.Create(signed,new[]{new InitialConditionPrior("power|한글","wetness",minus,minus)},Stable.Hash("signed zero fixture"));
            long beforeVersion=signed.Entities["power|한글"].Version;string beforeSignedDigest=signed.Digest();var applied=EmergenceComposer.MaterializeAndApply(signed,zero);Check(signed.Entities["power|한글"].Version==beforeVersion+1&&signed.Digest()!=beforeSignedDigest,"signed-zero bit write advances Version and exact digest");Check(applied.Disposition=="accepted"&&BitConverter.DoubleToInt64Bits(signed.Entities["power|한글"].Get("wetness"))==long.MinValue,"zero-width negative-zero endpoint survives actual entity write");
        }
        static void AtomicPreflight()
        {
            foreach(string kind in new[]{"nan","position","radius","tiny-radius","version","tick","cycle"})
            {
                var w=Fixture();if(kind=="nan")w.Entities["npc"].State["raw-malformed"]=double.NaN;
                if(kind=="position")w.Entities["npc"].X=double.MaxValue;
                if(kind=="radius")w.Entities["power|한글"].Set("heat.reach",1001);
                if(kind=="tiny-radius")w.Entities["npc"].Set("crowd.radius",double.Epsilon);
                if(kind=="version")w.Entities["npc"].Version=long.MaxValue;
                if(kind=="tick")w.Tick=long.MaxValue;
                if(kind=="cycle"){w.Entities["npc"].Tags.Remove("npc");w.Entities["npc"].ParentId="unknown";w.Entities["unknown"].Tags.Clear();w.Entities["unknown"].ParentId="npc";}
                var fields=new SortedDictionary<string,double>(w.Fields,StringComparer.Ordinal);int journal=w.Journal.Count;long tick=w.Tick;var states=w.Entities.Values.ToDictionary(e=>e.Id,e=>new SortedDictionary<string,double>(e.State,StringComparer.Ordinal));
                Reject(()=>new CausalKernel().Step(w),"malformed whole input rejects before tick "+kind);
                Check(w.Tick==tick&&w.Journal.Count==journal&&w.Fields.SequenceEqual(fields)&&w.Entities.Values.All(e=>e.State.SequenceEqual(states[e.Id])),"preflight leaves every field/state/journal/tick unchanged "+kind);
            }
        }
        static void FullWorldReplay()
        {
            string path="output/simulator/platform-ktx-unity/Runs/seed-20261013-20261014T013756412.jsonl";
            var header=JObject.Parse(File.ReadLines(path).First());var frozen=WorldStateCodec.FromToken(header["initial_state"]);
            Check(frozen.Entities.Count==5105,"all frozen world identities retained");
            Check(frozen.Digest()!=(string)header["initial_digest"],"old lossy/v1 native tape remains refused, never rewritten as recovered original");
            var wholeMaterial=InitialConditionMaterial.Create(frozen,new[]{new InitialConditionPrior("power:platform","wetness",.5,1),new InitialConditionPrior("power:platform","insulation",0,.5)},Stable.Hash("frozen whole world synthetic material"));
            var a=WorldStateCodec.Clone(frozen);var b=WorldStateCodec.FromToken(JObject.Parse(WorldStateCodec.Serialize(frozen)));
            var aRecord=EmergenceComposer.MaterializeAndApply(a,wholeMaterial);string materialReason;
            Check(aRecord.Disposition=="accepted"&&EmergenceComposer.ValidateAndApplyRecorded(b,JsonConvert.DeserializeObject<EmergenceRecord>(JsonConvert.SerializeObject(aRecord)),false,out materialReason),"whole5105 deterministic material replay");
            var noIntervention=WorldStateCodec.Clone(a);
            Check(a.Digest()==b.Digest(),"new v2 exact state roundtrip preserves parsed5105 baseline");
            var kernel=new CausalKernel();var community=new Community();
            for(int i=0;i<20;i++)
            {
                // A fixed ordered external input tape is applied to both worlds. This does
                // not replay Unity physics or claim recovery of the legacy native bit loss.
                if(i==3){foreach(var world in new[]{a,b})world.Entities["power:platform"].Set("power.isolated",1);}
                kernel.Step(a);community.Step(a);kernel.Step(b);community.Step(b);kernel.Step(noIntervention);community.Step(noIntervention);Check(a.Digest()==b.Digest(),"whole5105 parsed-world offline causal/action replay tick"+i);
            }
            Check(a.Entities["power:platform"].Get("power")==0&&noIntervention.Entities["power:platform"].Get("power")>0,"paired fixed-material/seed ordered isolation changes supply energy in full world");
            Check(a.Journal.Select(t=>t.ToString()+t.Reason).SequenceEqual(b.Journal.Select(t=>t.ToString()+t.Reason)),"whole5105 ordered transition identity equals");
            Console.WriteLine("Whole5105 parsed-baseline v2 exact codec+20tick ordered-input causal replay PASS; original native lossy digest still refused; Unity/model/network0.");
        }
    }
}
