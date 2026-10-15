using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChooGuard.Seeded;

namespace ChooGuard.Seeded.Tests
{
    public static class KernelTests
    {
        public static void Run()
        {
            ReplayAndOrder(); DecorationNormalization(); SupplyDependency(); IndependentDomains();
            NoForcedIncident(); InterventionEffects(); LocalPerception(); PersistentPhysicalDestinations();
            JournalCoverage(); NegativeInputs(); FaultHeatCoupling();
        }

        static void Assert(bool value,string message) { if(!value) throw new Exception("Kernel test: "+message); }
        static Entity Entity(string id,double x=0,params string[] tags)
        {
            var e=new Entity {Id=id,Kind=id,X=x}; foreach(var tag in tags) e.Tags.Add(tag);return e;
        }
        static World Empty(ulong seed=1)
        {
            var w=new World {Seed=seed}; w.Fields["environment.autonomous"]=0;return w;
        }
        static void RunSteps(World w,int count,bool community=true)
        {
            var k=new CausalKernel(); var c=new Community();
            for(int i=0;i<count;i++) { k.Step(w); if(community)c.Step(w); }
        }
        static string Journal(World w) { return string.Join("\n",w.Journal.Select(x=>x.ToString()+"|"+x.Reason)); }
        static World Fixture(ulong seed,bool reverse=false)
        {
            var w=Empty(seed);
            w.Fields["rate.electrical-short"]=0.8;w.Fields["rate.medical"]=0.08;w.Fields["rate.ignition"]=0.4;w.Fields["rate.fall"]=0.2;
            var power=Entity("power",0,"power-source","electrical");power.Set("supply.available",1);
            var cabinet=Entity("cabinet",1,"electrical","thermal","water-sensitive"); cabinet.Links.Add(power.Id);
            cabinet.Set("wetness",0.7);cabinet.Set("drainage",0);cabinet.Set("insulation",0);cabinet.Set("fault.heat",100);
            var fuel=Entity("fuel",1.4,"combustible","thermal");fuel.Set("temperature",190);fuel.Set("fuel",1);
            var floor=Entity("floor",0,"walkable","exposed");floor.Set("wetness",0.8);floor.Set("drainage",0);floor.Set("radius",20);
            var person=Entity("person",2,"npc","medical-vulnerable");person.Set("speed",1);person.Set("medical.vulnerability",0.8);person.Links.Add("exit");
            var exit=Entity("exit",9,"exit","destination");
            var all=new[]{power,cabinet,fuel,floor,person,exit};
            foreach(var e in reverse?all.Reverse():all) w.Add(e);return w;
        }

        static void ReplayAndOrder()
        {
            var a=Fixture(107);var b=Fixture(107);var shuffled=Fixture(107,true);
            RunSteps(a,150);RunSteps(b,150);RunSteps(shuffled,150);
            Assert(a.Digest()==b.Digest() && Journal(a)==Journal(b),"same seed/input reproduces full digest and causal journal");
            Assert(a.Digest()==shuffled.Digest() && Journal(a)==Journal(shuffled),"entity insertion order changes nothing");
            var distinct=new HashSet<string>(StringComparer.Ordinal);
            for(ulong seed=1;seed<=12;seed++)
            {
                var w=Fixture(seed);RunSteps(w,100);
                // Excludes Seed itself and ordinary numeric drift: compare event timing/identity.
                distinct.Add(string.Join(";",w.Journal.Where(IsOnset).Select(t=>t.Tick+":"+t.Mechanism+":"+t.Target)));
            }
            Assert(distinct.Count>4,"different seeds diversify actual incident onset timing/identity");
        }

        static bool IsOnset(Transition t)
        {
            return t.Before<=0 && t.After>0 && (t.Property=="fault" || t.Property=="fire" || t.Property=="fallen"
                || t.Property=="medical.condition" || t.Property=="failed" || t.Property=="regional-alert" || t.Property=="communications-disruption");
        }

        static void DecorationNormalization()
        {
            var a=Fixture(73);var b=Fixture(73);
            for(int i=0;i<200;i++)
            {
                var mesh=Entity("mesh:"+i,1,"electrical","combustible");mesh.ParentId="cabinet";mesh.Set("wetness",1);mesh.Set("temperature",900);b.Add(mesh);
            }
            var decoration=Entity("decoration",2,"decoration","combustible");decoration.Set("fire",1);b.Add(decoration);
            RunSteps(a,70);RunSteps(b,70);
            Assert(Journal(a)==Journal(b),"mesh/decorative counts do not amplify or perturb semantic hazard journal");
            Assert(CausalKernel.SemanticEntities(a).Count==CausalKernel.SemanticEntities(b).Count,"semantic parent count stable");
            var logical=Entity("logical-door",1,"semantic","electrical");logical.ParentId="cabinet";b.Add(logical);
            Assert(CausalKernel.SemanticEntities(b).Count==CausalKernel.SemanticEntities(a).Count+1,"explicit logical child remains an input");
        }

        static World WetCircuit()
        {
            var w=Empty();w.Fields["rate.electrical-short"]=1000000;
            var supply=Entity("source",0,"power-source");var load=Entity("load",1,"electrical","thermal");
            load.Links.Add("source");load.Set("wetness",1);load.Set("drainage",0);load.Set("insulation",0);w.Add(supply);w.Add(load);return w;
        }
        static void SupplyDependency()
        {
            var active=WetCircuit();var isolated=WetCircuit();var disconnected=WetCircuit();var removed=WetCircuit();
            isolated.Entities["source"].Set("power.isolated",1);disconnected.Entities["load"].Links.Clear();removed.Entities["source"].Set("removed",1);
            RunSteps(active,8,false);RunSteps(isolated,8,false);RunSteps(disconnected,8,false);RunSteps(removed,8,false);
            Assert(active.Entities["load"].Get("fault")==1,"powered wet conductor can short");
            foreach(var w in new[]{isolated,disconnected,removed})
                Assert(w.Entities["load"].Get("fault")==0 && w.Entities["load"].Get("power")==0,"source isolation/removal/disconnection prevents dependent fault");
            var after=WetCircuit();RunSteps(after,2,false);double before=after.Entities["load"].Get("temperature");
            after.Entities["load"].Set("power.isolated",1);RunSteps(after,2,false);
            Assert(after.Entities["load"].Get("spark")==0 && after.Entities["load"].Get("temperature")<before,"post-fault isolation stops sustained fault energy and permits cooling");
        }

        static void IndependentDomains()
        {
            var counts=new Dictionary<string,int>(StringComparer.Ordinal);
            for(ulong seed=1;seed<=32;seed++)
            {
                var w=Fixture(seed);w.Fields["environment.autonomous"]=1;
                foreach(var mechanism in new[]{"rain-onset","wind-gust","seismic-impulse","regional-alert","communications-disruption"})w.Fields["rate."+mechanism]=0.06;
                var structure=Entity("canopy",1,"structural","exposed");structure.Set("load.capacity",0.05);structure.Set("failure.threshold",0.95);w.Add(structure);
                var radio=Entity("receiver",0,"radio","alarm","battery","electrical");w.Add(radio);
                RunSteps(w,100);
                foreach(var transition in w.Journal.Where(IsOnset))
                { int count;counts.TryGetValue(transition.Property,out count);counts[transition.Property]=count+1; }
            }
            foreach(string key in new[]{"fault","fire","fallen","medical.condition","failed","regional-alert","communications-disruption"})
                Assert(counts.ContainsKey(key) && counts[key]>0,"independent seeded multirun covers "+key+" onset");
            Console.WriteLine("Kernel onset counts across 32 seeds (game rates): "+string.Join(", ",counts.OrderBy(x=>x.Key).Select(x=>x.Key+"="+x.Value)));
        }

        static void NoForcedIncident()
        {
            var w=Empty(42);var floor=Entity("floor",0,"walkable");var npc=Entity("npc",0,"npc");npc.Set("medical.vulnerability",0);
            var source=Entity("power",1,"power-source");var wire=Entity("wire",1,"electrical");wire.Links.Add("power");
            var fuel=Entity("bag",1,"combustible");w.Add(floor);w.Add(npc);w.Add(source);w.Add(wire);w.Add(fuel);
            RunSteps(w,1000);
            Assert(!w.Journal.Any(IsOnset),"benign world has no forced incident or minimum-scenario quota");
        }

        static void InterventionEffects()
        {
            var dry=Empty();var wet=Empty();
            foreach(var w in new[]{dry,wet}) { var f=Entity("material",0,"combustible");f.Set("fire",0.8);f.Set("temperature",200);w.Add(f); }
            wet.Entities["material"].Set("wetness",1);wet.Entities["material"].Set("drainage",0);
            RunSteps(dry,8,false);RunSteps(wet,8,false);
            Assert(wet.Entities["material"].Get("fire")<dry.Entities["material"].Get("fire"),"suppression water changes physical combustion");
            var unattended=Empty();var assisted=Empty();
            foreach(var w in new[]{unattended,assisted}) { var p=Entity("patient",0,"npc");p.Set("medical.condition",0.5);p.Set("medical.vulnerability",0);w.Add(p); }
            assisted.Entities["patient"].Set("assisted",1);RunSteps(unattended,20,false);RunSteps(assisted,20,false);
            Assert(assisted.Entities["patient"].Get("health")>unattended.Entities["patient"].Get("health"),"assistance reduces physiological deterioration");
            var rolling=Empty();var held=Empty();
            foreach(var w in new[]{rolling,held}) { var t=Entity("train",0,"moving-body");t.Set("speed",3);var p=Entity("p",0.5,"npc");p.Set("medical.vulnerability",0);w.Add(t);w.Add(p); }
            held.Entities["train"].Set("hold",1);RunSteps(rolling,4,false);RunSteps(held,4,false);
            Assert(held.Entities["p"].Get("health")>rolling.Entities["p"].Get("health"),"holding moving vehicle removes collision energy");
            var manual=Empty();var alarm=Entity("alarm",0,"alarm","electrical","battery");alarm.Set("alert.relay",1);manual.Add(alarm);manual.Fields["communications-disruption"]=1;
            RunSteps(manual,2,false);Assert(alarm.Get("alarm.active")==1,"manual local alarm persists and operates through regional communications outage");
            var multi=Empty();var patient=Entity("p",0,"npc");patient.Set("medical.vulnerability",0);multi.Add(patient);
            foreach(string id in new[]{"a","b"}) { var f=Entity(id,1,"combustible");f.Set("fire",1);multi.Add(f); }
            RunSteps(multi,1,false);
            Assert(multi.Journal.Any(t=>t.Target=="p" && t.Property=="health" && t.Source=="a,b"),"causal journal retains multiple concurrent exposure contributors");
        }

        static void LocalPerception()
        {
            var remote=Empty();var p=Entity("p",0,"npc");var f=Entity("fire",100,"combustible");f.Set("fire",1);remote.Add(p);remote.Add(f);remote.Fields["regional-alert"]=1;
            new Community().Step(remote);Assert(p.Get("awareness")==0,"remote hazards and regional alerts are not omniscient perception");
            f.X=3;p.Set("occlusion.fire",1);new Community().Step(remote);Assert(p.Get("awareness")==0,"explicit sight occlusion prevents visual evidence");
            p.Set("occlusion.fire",0);new Community().Step(remote);Assert(p.Get("awareness")>0 && p.Get("reaction.remaining")>0,"near visible hazard creates seeded response latency");
            var social=Empty();var observer=Entity("observer",0,"npc");var peer=Entity("peer",1,"npc");var distant=Entity("distant",100,"npc");peer.Set("awareness",1);observer.Links.Add("peer");distant.Links.Add("peer");social.Add(observer);social.Add(peer);social.Add(distant);
            new Community().Step(social);Assert(observer.Get("awareness")>0 && distant.Get("awareness")==0,"social knowledge needs local proximity, not link alone");
            var alarm=Entity("alarm",8,"alarm");alarm.Set("alarm.active",1);remote.Add(alarm);p.Set("awareness",0);f.X=100;new Community().Step(remote);Assert(p.Get("awareness")>0,"local audible alarm informs person");
        }

        static void PersistentPhysicalDestinations()
        {
            var w=Empty(7);var p=Entity("p",0,"npc");var seat=Entity("seat",5,"seat","destination");var door=Entity("door",8,"door","destination");w.Add(p);w.Add(seat);w.Add(door);
            var community=new Community();community.Step(w);
            double target=p.Get("goal.x");Assert(target==seat.X || target==door.X,"routine goal uses actual registered physical destination");
            w.Tick=60;community.Step(w);Assert(p.Get("goal.x")==target && p.Get("goal.active")==1,"active goal persists across timing boundary");
            p.X=target;w.Tick++;community.Step(w);Assert(p.Get("goal.x")!=target,"arrival releases destination and chooses another physical goal");
            Assert(p.State.Any(s=>s.Key.StartsWith("visit.",StringComparison.Ordinal)),"arrival records destination memory");
            p.Set("goal.failed",1);foreach(var e in new[]{seat,door})p.Set("unreachable."+e.Id,1);w.Tick++;community.Step(w);
            Assert(p.Get("goal.active")==0,"navigation failure consumes active goal without inventing random coordinates");
            var noPlace=Empty();var lone=Entity("lone",0,"npc");noPlace.Add(lone);community.Step(noPlace);Assert(lone.Get("goal.active")==0,"world without a destination never invents routine coordinates");
        }

        static void JournalCoverage()
        {
            var w=Fixture(9);var before=w.Entities.ToDictionary(e=>e.Key,e=>new Dictionary<string,double>(e.Value.State),StringComparer.Ordinal);
            RunSteps(w,1);
            foreach(var e in w.Entities.Values) foreach(var state in e.State)
            {
                double previous;bool existed=before[e.Id].TryGetValue(state.Key,out previous);
                if(!existed || previous!=state.Value)
                    Assert(w.Journal.Any(t=>t.Target==e.Id && t.Property==state.Key && t.After==state.Value),"state mutation journaled for "+e.Id+":"+state.Key);
            }
            Assert(w.Journal.All(t=>!string.IsNullOrEmpty(t.Source) && !string.IsNullOrEmpty(t.Target) && !string.IsNullOrEmpty(t.Mechanism) && !string.IsNullOrEmpty(t.Reason)),"causal journal has source/target/mechanism/explanation");
        }

        static void NegativeInputs()
        {
            var cycle=Empty();var a=Entity("a");a.ParentId="b";var b=Entity("b");b.ParentId="a";cycle.Add(a);cycle.Add(b);
            bool rejected=false;try{new CausalKernel().Step(cycle);}catch(ArgumentException){rejected=true;}Assert(rejected,"cyclic semantic parent input rejected");
            var invalid=Empty();invalid.StepSeconds=double.NaN;rejected=false;try{new CausalKernel().Step(invalid);}catch(ArgumentException){rejected=true;}Assert(rejected,"nonfinite timestep rejected");
            invalid=Empty();invalid.Fields["rain"]=double.PositiveInfinity;rejected=false;try{new CausalKernel().Step(invalid);}catch(ArgumentException){rejected=true;}Assert(rejected,"nonfinite world input rejected");
            var names=Empty();var pretender=Entity("fire_alarm_electrical_medical",0);names.Add(pretender);RunSteps(names,3);
            Assert(pretender.State.Count==0,"names do not manufacture capabilities or hazards");
        }

        static World FaultAndFuel(string condition="near")
        {
            var w=WetCircuit();w.Fields["rate.ignition"]=1000000;
            var load=w.Entities["load"];load.Set("fault.heat",180);load.Set("thermal.conductivity",0.6);
            var fuel=Entity("baggage",condition=="remote"?30:1.5,"combustible","thermal");
            fuel.Set("temperature",20);fuel.Set("thermal.conductivity",0.6);fuel.Set("ignition.temperature",170);
            if(condition=="insulated")fuel.Tags.Add("thermal-insulated");
            if(condition=="disconnected")fuel.Set("thermal.link-required",1);
            if(condition=="other-compartment")fuel.Set("thermal.zone",1);
            if(condition=="above")fuel.Y=4;
            if(condition=="linked") { fuel.Set("thermal.link-required",1);fuel.Links.Add(load.Id); }
            if(condition=="isolated")load.Set("power.isolated",1);
            w.Add(fuel);return w;
        }

        static void FaultHeatCoupling()
        {
            var heated=FaultAndFuel();RunSteps(heated,120,false);
            var onset=heated.Journal.FirstOrDefault(t=>t.Target=="baggage" && t.Property=="fire" && t.Before==0 && t.After>0);
            Assert(onset!=null,"cold baggage ignites through wet powered fault -> hot source -> bounded local thermal exchange");
            Assert(heated.Journal.Any(t=>t.Target=="baggage" && t.Property=="temperature" && t.After>t.Before && t.Source.Contains("load")),"fault-to-material heating retains source provenance");
            Assert(onset.Tick>5,"heat coupling never creates instant ignition");
            foreach(var condition in new[]{"remote","insulated","disconnected","other-compartment","above","isolated"})
            {
                var safe=FaultAndFuel(condition);RunSteps(safe,120,false);
                Assert(safe.Entities["baggage"].Get("fire")==0,"hot object cannot ignite material when "+condition);
            }
            var connected=FaultAndFuel("linked");RunSteps(connected,120,false);
            Assert(connected.Entities["baggage"].Get("fire")>0,"explicit connected conductive relation enables otherwise link-gated transfer");

            var balance=Empty();balance.Fields["ambient.temperature"]=20;
            var hot=Entity("hot",0,"thermal");hot.Set("temperature",100);hot.Set("cooling",0);hot.Set("thermal.capacity",2);
            var cold=Entity("cold",0.5,"thermal");cold.Set("temperature",20);cold.Set("cooling",0);cold.Set("thermal.capacity",3);
            balance.Add(hot);balance.Add(cold);double before=hot.Get("temperature")*2+cold.Get("temperature")*3;RunSteps(balance,1,false);
            double after=hot.Get("temperature")*2+cold.Get("temperature")*3;
            Assert(Math.Abs(before-after)<1e-9,"pair heat exchange conserves capacity-weighted energy without internal sources/cooling");
            Assert(cold.Get("temperature")>20 && cold.Get("temperature")<100,"bounded gradient transfer heats without overshoot");
        }
    }
}
