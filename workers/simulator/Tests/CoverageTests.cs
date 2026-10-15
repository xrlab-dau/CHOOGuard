using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Seeded;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class CoverageTests
    {
        static readonly string ManifestPath="workers/simulator/data/semantic-coverage.json";
        static string Manifest=>File.ReadAllText(ManifestPath);
        static string Source=>File.ReadAllText("workers/simulator/data/platform-source.json");
        static void Check(bool condition,string message){if(!condition)throw new Exception("Coverage test: "+message);}
        static void Reject(Action action,string message)
        {bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,message);}
        static World Create(ulong seed=41,int population=0)
        {
            var w=PlatformWorldFactory.Create(Source,seed,population);WorldCoverage.Apply(w,Manifest);
            w.Fields["environment.autonomous"]=0;w.Fields["rain"]=0;w.Fields["wind"]=0;w.Fields["ambient.temperature"]=20;
            return w;
        }
        static void Steps(World w,int count,bool community=false)
        {var kernel=new CausalKernel();var people=new Community();for(int i=0;i<count;i++){kernel.Step(w);if(community)people.Step(w);}}
        static string Journal(World w)=>string.Join("\n",w.Journal.Select(x=>x.ToString()+"|"+x.Reason));
        static bool Act(World w,string operation,string target,string actor="player")
        {
            var plan=ActionPlan.Capture(w,"coverage:"+operation,"local-fixture","coverage-v1",OperationCatalog.Version,"coverage-nav",new[]{new ActionStep{Operation=operation,ActorId=actor,TargetId=target}});
            string reason;return new PlanExecutor(OperationCatalog.CreateDefault()).TryExecute(w,plan,0,"coverage-nav",out reason);
        }
        static Entity AddPerson(World w,string id,Entity at)
        {var p=new Entity{Id=id,Kind="person",X=at.X,Y=at.Y,Z=at.Z};p.Tags.Add("npc");p.Set("health",1);p.Set("mobility",1);p.Set("medical.vulnerability",0);w.Add(p);return p;}
        static void Approach(Entity actor,Entity target)
        {actor.X=target.Get("destination.x",target.X);actor.Y=target.Get("destination.y",target.Y);actor.Z=target.Get("destination.z",target.Z);}

        public static void Run()
        {
            ManifestAndSources();Car04SourceQualification();AtomicAndIdempotentLoad();ManualCabinDoorOperations();SeatOperationsAndLocalResponse();
            SourceShapedPropagationAndInterventions();ReplayAndVisualDedup();
        }

        static void ManifestAndSources()
        {
            var m=WorldCoverage.Parse(Manifest);WorldCoverage.VerifySources(m,Directory.GetCurrentDirectory());
            Check(m.schema_version==2&&m.report.visual_parts==456&&m.report.physical_entities==417&&m.report.source_seats==394&&m.report.navigation_points==35,"source counts include44 qualified new seats and two cabin doors");
            Check(m.report.visual_cars==10&&m.report.passenger_compartments==8&&m.report.operational_passenger_compartments==8,"10 visual cars and8 supported passenger compartments remain distinct");
            Check(m.visual_consist.Count(x=>x.role=="powerhead")==2&&m.visual_consist.Where(x=>x.role=="powerhead").All(x=>!x.passenger_supported),"powerheads never counted as passenger spaces");
            Check(m.report.operation_bound_visual_parts==42&&m.report.unsupported_visual_parts==414,"qualified leaves/lifts and unsupported parts accounted separately");
            var catalog=OperationCatalog.CreateDefault();
            foreach(var e in m.physical_entities.Concat(m.navigation_points))foreach(string name in e.operations)
            {OperationSpec operation;Check(catalog.TryGet(name,out operation),"declared operation executable: "+name);}
            var bad=JObject.Parse(Manifest);((JArray)bad["visual_parts"]).Add(bad["visual_parts"][0].DeepClone());
            Reject(()=>WorldCoverage.Parse(bad.ToString()),"duplicate visual ID/path rejected");
            bad=JObject.Parse(Manifest);((JArray)bad["unsupported"]).RemoveAt(0);
            Reject(()=>WorldCoverage.Parse(bad.ToString()),"missing unsupported reason ledger rejected");
            bad=JObject.Parse(Manifest);bad["sources"][0]["path"]="../outside.fbx";
            Reject(()=>WorldCoverage.Parse(bad.ToString()),"source traversal rejected");
            bad=JObject.Parse(Manifest);((JArray)bad["physical_entities"].First(x=>(string)x["kind"]=="seat")["tags"]).Add("combustible");
            Reject(()=>WorldCoverage.Parse(bad.ToString()),"source seats cannot acquire material hazards from names");
            Reject(()=>WorldCoverage.VerifySources(m,p=>new byte[]{1,2,3}),"source bytes mismatch rejected");
            bad=JObject.Parse(Manifest);var changedSeat=bad["physical_entities"].First(x=>(string)x["kind"]=="seat");changedSeat["position"]["x"]=(double)changedSeat["position"]["x"]+.1;
            var drifted=WorldCoverage.Parse(bad.ToString());
            Reject(()=>WorldCoverage.VerifySources(drifted,Directory.GetCurrentDirectory()),"manifest seat anchor drift rejected even with correct source hashes");
        }

        static void Car04SourceQualification()
        {
            var manifest=WorldCoverage.Parse(Manifest);
            var proposal=JObject.Parse(File.ReadAllText("workers/simulator/geometry_runtime/car04-source-qualification/passenger-semantic-proposal.json"));
            var source=JObject.Parse(File.ReadAllText("Assets/ChooGuard/Art/Emergency/station-points.json"));
            var car04=manifest.physical_entities.Where(e=>e.kind=="seat"&&e.parent_id=="car:4").ToArray();
            Check(car04.Length==44&&proposal["seats"].All(s=>car04.Count(e=>e.id==(string)s["id"])==1),"all44 exact cushion identities promoted once");
            Check(manifest.physical_entities.Count(e=>e.kind=="seat"&&e.parent_id!="car:4")==350&&source["train"]["cars"].Count()==7,"original350 seat records and seven source DTO external doors retained");
            Check(!manifest.physical_entities.Any(e=>e.id=="door:4")&&manifest.physical_entities.Count(e=>e.kind=="door")==9,"two new cabin doors do not invent an eighth external door");
            Check(manifest.sources.Single(s=>s.id=="car04-native-proof").sha256==WorldCoverage.Car04NativeProofSha256,"exact reviewed native source-route proof pinned");
            Check(manifest.visual_parts.Where(p=>p.name.StartsWith("Car04_T4 4car_05_R_in_",StringComparison.Ordinal)).All(p=>p.status=="parent-only"&&manifest.unsupported.Any(u=>u.id==p.id)),"combined Car04 source meshes remain in unsupported ledger despite qualified leaf subsets");
            foreach(var identity in new[]{"cabin-door:4:near","cabin-door:4:far","seat:04-SRC-01A","car:4"})
            {
                var bad=JObject.Parse(Manifest);var e=bad["physical_entities"].Single(x=>(string)x["id"]==identity);
                ((JArray)e["tags"]).Add("electrical");Reject(()=>WorldCoverage.Parse(bad.ToString()),"qualified Car04 entities cannot acquire electrical capability: "+identity);
                bad=JObject.Parse(Manifest);e=bad["physical_entities"].Single(x=>(string)x["id"]==identity);((JArray)e["operations"]).Add("isolate_power");
                Reject(()=>WorldCoverage.Parse(bad.ToString()),"source qualification cannot introduce power operations: "+identity);
            }
            var changed=JObject.Parse(Manifest);changed["sources"].Single(s=>(string)s["id"]=="car04-native-proof")["sha256"]=new string('0',64);
            Reject(()=>WorldCoverage.Parse(changed.ToString()),"arbitrary PASS proof cannot replace reviewed proof");
            changed=JObject.Parse(Manifest);changed["schema_version"]=1;
            Reject(()=>WorldCoverage.Parse(changed.ToString()),"old schema cannot silently gain newly qualified cabin capabilities");
            changed=JObject.Parse(Manifest);changed["physical_entities"].Single(e=>(string)e["id"]=="seat:04-SRC-01A")["id"]="seat:04-01A";
            Reject(()=>WorldCoverage.Parse(changed.ToString()),"old heuristic seat identity cannot masquerade as source cushion");
            changed=JObject.Parse(Manifest);var visual=changed["visual_parts"].First(p=>(string)p["name"]=="Car04_T4 4car_05_R_in_000");visual["status"]="operation-bound";visual["physical_parent_id"]="cabin-door:4:near";
            Reject(()=>WorldCoverage.Parse(changed.ToString()),"partial leaf qualification cannot bind an entire combined source mesh");
            changed=JObject.Parse(Manifest);changed["physical_entities"].Single(e=>(string)e["id"]=="cabin-door:4:near")["links"][1]="car:2";
            Reject(()=>WorldCoverage.Parse(changed.ToString()),"wrong neighboring compartment link rejected");
            changed=JObject.Parse(Manifest);changed["physical_entities"].Single(e=>(string)e["id"]=="seat:04-SRC-01A")["state"]["destination.z"]=-99;
            var drifted=WorldCoverage.Parse(changed.ToString());Reject(()=>WorldCoverage.VerifySources(drifted,Directory.GetCurrentDirectory()),"Car04 aisle destination drift rejected with unchanged source hashes");
            changed=JObject.Parse(Manifest);changed["physical_entities"].Single(e=>(string)e["id"]=="car:4")["position"]["x"]=(double)changed["physical_entities"].Single(e=>(string)e["id"]=="car:4")["position"]["x"]+.1;
            drifted=WorldCoverage.Parse(changed.ToString());Reject(()=>WorldCoverage.VerifySources(drifted,Directory.GetCurrentDirectory()),"original Car04 body coordinate drift rejected");
            // Also reject changing resource coordinates and updating its hash: the reviewed proposal remains authority.
            var access=JObject.Parse(File.ReadAllText("workers/simulator/data/car04-access.json"));access["cabin_doors"][0]["training_open_delta_world"]["x"]=(double)access["cabin_doors"][0]["training_open_delta_world"]["x"]+.1;
            var bytes=System.Text.Encoding.UTF8.GetBytes(access.ToString());string hash;
            using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
            changed=JObject.Parse(Manifest);changed["sources"].Single(s=>(string)s["id"]=="car04-access")["sha256"]=hash;
            drifted=WorldCoverage.Parse(changed.ToString());Reject(()=>WorldCoverage.VerifySources(drifted,p=>p=="workers/simulator/data/car04-access.json"?bytes:File.ReadAllBytes(p)),"changed resource animation cannot bypass proposal by updating resource hash");
        }

        static void AtomicAndIdempotentLoad()
        {
            var w=Create(population:32);Check(w.Entities.Count==532,"default factory world expanded100 to532 entities");
            var seat=w.Entities.Values.First(x=>x.Has("seat"));seat.Set("occupied",1);string before=w.Digest();
            WorldCoverage.Apply(w,Manifest);Check(w.Digest()==before&&seat.Get("occupied")==1,"reapplying manifest retains live state and topology");
            var collision=PlatformWorldFactory.Create(Source,41,0);collision.Add(new Entity{Id=seat.Id,Kind="wrong",ParentId=seat.ParentId});before=collision.Digest();
            Reject(()=>WorldCoverage.Apply(collision,Manifest),"colliding source identity rejected");Check(collision.Digest()==before,"failed load is atomic");
            var car4=w.Entities["car:4"];
            Check(car4.Get("reachable")==1&&!car4.Has("electrical")&&!car4.Has("combustible")&&!car4.Has("structural")&&!car4.Has("thermal"),"Car04 source body gains intercar access without unsupported hazard semantics");
            Check(Act(w,"move_to","car:4"),"qualified Car04 compartment approach accepted");
            Check(w.Entities.Keys.Count(x=>x.StartsWith("seat:04-SRC-",StringComparison.Ordinal))==44&&!w.Entities.Keys.Any(x=>x.StartsWith("seat:04-",StringComparison.Ordinal)&&!x.StartsWith("seat:04-SRC-",StringComparison.Ordinal)),"44 new source identities replace prior40 unactivated candidates");
            var cabin=w.Entities["cabin-door:4:near"];cabin.Set("open",0);cabin.Set("jammed",1);before=w.Digest();
            WorldCoverage.Apply(w,Manifest);Check(w.Digest()==before&&cabin.Get("open")==0&&cabin.Get("jammed")==1,"reapply preserves live manual cabin state");
            collision=PlatformWorldFactory.Create(Source,41,0);var injected=new Entity{Id="cabin-door:4:far",Kind="door",ParentId="car:4",X=1};injected.Tags.Add("source-coordinate");injected.Tags.Add("electrical");collision.Add(injected);before=collision.Digest();
            Reject(()=>WorldCoverage.Apply(collision,Manifest),"incompatible late cabin identity collision rejected");Check(collision.Digest()==before,"late collision cannot partially install earlier seats");
            var bad=JObject.Parse(Manifest);((JArray)bad["physical_entities"].Single(e=>(string)e["id"]=="cabin-door:4:far")["tags"]).Add("combustible");before=w.Digest();
            Reject(()=>WorldCoverage.Apply(w,bad.ToString()),"bad qualified door manifest rejected before mutation");Check(w.Digest()==before,"invalid capability load is atomic");
        }

        static void ManualCabinDoorOperations()
        {
            var w=Create();var player=w.Entities["player"];
            foreach(var id in new[]{"cabin-door:4:near","cabin-door:4:far"})
            {
                var door=w.Entities[id];Approach(player,door);
                Check(!door.Has("electrical")&&!Act(w,"isolate_power",id),"manual cabin door cannot acquire an electrical intervention");
                door.Set("power",0);Check(Act(w,"close_door",id),"manual cabin close works without electrical power");
                door.Set("locked",1);Check(!Act(w,"open_door",id),"locked cabin leaf refuses opening");door.Set("locked",0);
                door.Set("jammed",1);Check(!Act(w,"open_door",id),"jammed cabin leaf refuses opening");door.Set("jammed",0);
                Check(Act(w,"open_door",id)&&door.Get("open")==1,"manual cabin open works without electrical power");
                Check(door.Get("integrity")==1,"manual operation preserves source door integrity");
            }
            var seat=w.Entities["seat:04-SRC-11D"];Check(Act(w,"move_to",seat.Id)&&player.Get("goal.x")==seat.Get("destination.x")&&player.Get("goal.z")==seat.Get("destination.z"),"Car04 seat approach targets its reviewed center aisle");
            Approach(player,seat);Check(Act(w,"sit",seat.Id)&&seat.Get("occupant.player")==1&&Act(w,"stand",seat.Id),"new source cushion supports owned abstract sit/stand");
            Steps(w,4);Check(new[]{"car:4","cabin-door:4:near","cabin-door:4:far","seat:04-SRC-11D"}.All(id=>w.Entities[id].Get("fault")==0&&w.Entities[id].Get("fire")==0),"source qualification does not create electrical/thermal fault propagation");
        }

        static void SeatOperationsAndLocalResponse()
        {
            var w=Create();var seat=w.Entities.Values.First(x=>x.Has("seat"));var player=w.Entities["player"];
            player.X=seat.Get("destination.x")+2;player.Y=seat.Get("destination.y");player.Z=seat.Get("destination.z");
            Check(!Act(w,"sit",seat.Id),"cannot sit remotely");
            Check(Act(w,"move_to",seat.Id)&&player.Get("goal.x")==seat.Get("destination.x")&&player.Get("goal.z")==seat.Get("destination.z"),"source seat approach uses aisle coordinates");
            Approach(player,seat);Check(Act(w,"sit",seat.Id)&&seat.Get("occupied")==1&&seat.Get("occupant.player")==1&&player.Get("seated")==1,"actual source seat supports owned occupancy");
            var other=AddPerson(w,"coverage:other",player);Check(!Act(w,"sit",seat.Id,other.Id)&&!Act(w,"stand",seat.Id,other.Id),"occupied seat excludes second actor and foreign release");
            Check(!Act(w,"move_to","door:1"),"seated actor must stand before moving");
            player.Set("fatigue",.8);new Community().Step(w);Check(player.Get("fatigue")<.8&&player.Get("seated")==1,"calm seated actor rests without routine wandering");
            Check(Act(w,"stand",seat.Id)&&seat.Get("occupied")==0&&player.Get("seated")==0,"owner can stand and release seat");
            Check(Act(w,"sit",seat.Id),"released source seat can be occupied again");
            player.Set("reaction.minimum",0);player.Set("reaction.spread",0);
            var alarm=w.Entities["radio:platform"];alarm.X=player.X+1;alarm.Y=player.Y;alarm.Z=player.Z;alarm.Set("alarm.active",1);
            new Community().Step(w);
            Check(player.Get("awareness")>=.15&&player.Get("seated")==0&&seat.Get("occupied")==0&&w.Journal.Any(t=>t.Mechanism=="seat-release"&&t.Target==seat.Id),"local audible alarm releases owned seat through causal journal");
            var calm=Create();var sourceSeat=calm.Entities.Values.First(x=>x.Has("seat"));var actor=calm.Entities["player"];Approach(actor,sourceSeat);Check(Act(calm,"sit",sourceSeat.Id),"negative fixture owns source seat");
            calm.Entities["radio:platform"].Set("alarm.active",1);calm.Entities["radio:platform"].X=actor.X+1000;
            new Community().Step(calm);Check(actor.Get("seated")==1,"unheard distant alarm is not a global urgency oracle");
        }

        static World DoorCircuit(bool isolate=false)
        {
            var w=Create(71);w.Fields["rate.electrical-short"]=1000000;w.Fields["rate.ignition"]=1000000;
            var door=w.Entities["door:2"];door.Set("wetness",1);door.Set("drainage",0);door.Set("insulation",0);door.Set("fault.heat",200);door.Set("thermal.conductivity",.5);
            var power=w.Entities["power:platform"];power.Set("wear",0);power.Set("insulation",1);power.Set("wetness",0);
            var bag=new Entity{Id="coverage:luggage",Kind="explicit-training-fixture",X=door.X+.5,Y=door.Y,Z=door.Z};
            foreach(string tag in new[]{"semantic","combustible","thermal","training-model"})bag.Tags.Add(tag);
            bag.Set("temperature",20);bag.Set("fuel",1);bag.Set("ignition.temperature",30);bag.Set("thermal.conductivity",.5);w.Add(bag);
            var patient=AddPerson(w,"coverage:patient",door);patient.X+=.8;
            if(isolate){var player=w.Entities["player"];Approach(player,power);Check(Act(w,"isolate_power",power.Id),"source world power isolation accepted");}
            return w;
        }

        static void SourceShapedPropagationAndInterventions()
        {
            var live=DoorCircuit();var isolated=DoorCircuit(true);Steps(live,40);Steps(isolated,40);
            Check(live.Entities["door:2"].Get("fault")==1&&live.Entities["coverage:luggage"].Get("fire")>0,"source door circuit propagates water/electrical/thermal chain to explicit training luggage");
            Check(live.Entities["coverage:patient"].Get("health")<isolated.Entities["coverage:patient"].Get("health"),"source-position thermal exposure affects nearby person");
            Check(isolated.Entities["door:2"].Get("power")==0&&isolated.Entities["door:2"].Get("fault")==0&&isolated.Entities["coverage:luggage"].Get("fire")==0,"supported source-power intervention breaks chain");
            Check(live.Journal.Any(t=>t.Target=="coverage:luggage"&&t.Property=="temperature"&&t.Source.Contains("door:2")),"propagated heating keeps source door identity");
            var regional=Create();regional.Fields["regional-alert"]=1;regional.Fields["alert.decay"]=0;Steps(regional,1);
            Check(regional.Entities["radio:platform"].Get("alert.received")>0,"source-position powered receiver delivers public alert");
            regional.Fields["communications-disruption"]=1;regional.Fields["network.decay"]=0;Steps(regional,1);
            Check(regional.Entities["radio:platform"].Get("alert.received")==0,"regional communication disruption changes delivery");
            var structural=Create();var platform=structural.Entities["platform:p56"];var near=AddPerson(structural,"coverage:debris-patient",platform);
            platform.Set("load.capacity",0);platform.Set("damage.rate",1);platform.Set("failure.threshold",.9);structural.Fields["seismic"]=1;structural.Fields["seismic.decay"]=0;Steps(structural,2);
            Check(platform.Get("failed")==1&&near.Get("health")<1,"source platform ground-load/debris/health chain responds to explicit game parameters");
            var care=Create();var patient=AddPerson(care,"coverage:medical",care.Entities["player"]);patient.Set("medical.condition",.6);patient.Set("health",.9);
            Check(Act(care,"assist",patient.Id)&&patient.Get("assisted")==1,"supported assistance changes source-position patient's medical state");
        }

        static void ReplayAndVisualDedup()
        {
            var a=Create(93,8);var b=Create(93,8);var decorated=Create(93,8);var m=WorldCoverage.Parse(Manifest);
            foreach(var row in m.visual_parts)
            {
                var e=new Entity{Id=row.id,Kind="visual-part",ParentId=row.physical_parent_id??"train:ktx"};
                foreach(string tag in new[]{"render-only","decoration","electrical","combustible"})e.Tags.Add(tag);e.Set("temperature",900);e.Set("fire",1);decorated.Add(e);
            }
            var kernel=new CausalKernel();var people=new Community();
            for(int tick=0;tick<60;tick++)
            {
                if(tick==10)foreach(var w in new[]{a,b,decorated}){Approach(w.Entities["player"],w.Entities["door:2"]);Check(Act(w,"close_door","door:2"),"source door close intervention");}
                kernel.Step(a);people.Step(a);kernel.Step(b);people.Step(b);kernel.Step(decorated);people.Step(decorated);
                Check(a.Digest()==b.Digest()&&Journal(a)==Journal(b),"expanded source state/input exact replay at tick "+tick);
            }
            Check(Journal(a)==Journal(decorated)&&CausalKernel.SemanticEntities(a).Count==CausalKernel.SemanticEntities(decorated).Count,"456 render children cannot amplify risk or change causal/community journal");
            Console.WriteLine("Coverage: 456 source visuals, 417 physical records, 350 unchanged DTO seats +44 source Car04 cushions, 35 navigation points, 8 operational compartments, 2 manual cabin doors; external door4 remains blocked.");
        }
    }
}
