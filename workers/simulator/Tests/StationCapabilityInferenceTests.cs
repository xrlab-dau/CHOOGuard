using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class StationCapabilityInferenceTests
    {
        const string SceneHash="ec2f5f3b6097bcc7eae78e01e50ffbbb670583ea355e074ae2f02e04e8bd1e66",SceneGuid="7518d664dafa749f2b6d5cba42bb798c";
        static string Profiles=>File.ReadAllText("workers/simulator/data/station-capability-profiles.json");
        static string Id(int i)=>"unity:"+SceneGuid+":"+i;
        static void Check(bool value,string message){if(!value)throw new Exception("Station capability inference: "+message);}
        static void Reject(Action action,string message){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,message);}
        public static void Run()
        {
            GroupingAndUnknowns();AliasesAndAtomicity();ProfileBoundaries();SeedStability();CausalSupplyBoundary();FullSnapshot();
            Console.WriteLine("Station capability inference: assembly dedup, inactive/unknown/typed exclusions, material boundaries, stable seeds, atomic/idempotent hashes, owned supply causality and full4115 snapshot PASS.");
        }
        static JObject Object(int id,string name,double x=0,bool active=true,string parent=null,bool bounds=true)
        {
            var p=new JArray(x,0,0);var row=new JObject{["id"]=Id(id),["sceneFileId"]=id.ToString(),["parentId"]=parent,["name"]=name,["path"]=parent==null?name:"typed-parent/"+name,["activeSelf"]=active,["activeInHierarchy"]=active,["physicalEligible"]=active,["layer"]=0,["tag"]="Untagged",
                ["localTransform"]=new JObject{["position"]=p.DeepClone(),["rotation"]=new JArray(0,0,0,1),["scale"]=new JArray(1,1,1)},
                ["worldTransform"]=new JObject{["position"]=p.DeepClone(),["rotation"]=new JArray(0,0,0,1),["lossyScale"]=new JArray(1,1,1),["localToWorldMatrix4x4"]=new JArray(1,0,0,x,0,1,0,0,0,0,1,0,0,0,0,1)},
                ["components"]=new JArray(),["geometryProvenance"]=new JObject{["mode"]="allowlisted",["sourceSceneSha256"]=SceneHash},["sourceBindings"]=new JArray()};
            ((JArray)row["components"]).Add(new JObject{["fileId"]=(100000+id).ToString(),["classId"]=65,["type"]="BoxCollider",["enabled"]=true,["isTrigger"]=false,["center"]=new JArray(0,0,0),["size"]=new JArray(1,1,1),["localBounds"]=new JObject{["center"]=new JArray(0,0,0),["size"]=new JArray(1,1,1)},["worldBounds"]=bounds?(JToken)new JObject{["min"]=new JArray(x-.5,-.5,-.5),["max"]=new JArray(x+.5,.5,.5)}:JValue.CreateNull(),["boundsEvidence"]=bounds?"serialized-box":"unsupported"});
            return row;
        }
        static JObject Fixture(params JObject[] objects)=>new JObject{["schemaVersion"]=WorldObjectRegistry.SchemaVersion,["sourceSnapshot"]=new JObject{["scenePath"]="Assets/ChooGuard/Scenes/FpsStation.unity",["sceneGuid"]=SceneGuid,["sceneSha256"]=SceneHash,["snapshotId"]="inference-fixture",["capturedAtUtc"]="2026-10-14T00:00:00Z"},["frame"]=new JObject{["coordinateSystem"]="Unity left-handed Y-up",["registration"]="preserved-saved-scene",["sourceToSimulationMatrix4x4"]=new JArray(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1)},["objects"]=new JArray(objects),["sourceBindings"]=new JArray()};
        static WorldObjectRegistry Parse(JObject data){string json=data.ToString(Formatting.None);return WorldObjectRegistry.Parse(json,Stable.Hash(json),SceneHash);}
        static World World(WorldObjectRegistry registry,ulong seed=17)
        {var world=new World{Seed=seed};world.Fields["ambient.temperature"]=20;world.Fields["environment.autonomous"]=0;registry.Apply(world);return world;}
        static JObject EmbeddedBounds(JObject row,double x,double y,double z)
        {var collider=row["components"][0];collider["center"]=new JArray(x,y,z);collider["localBounds"]["center"]=new JArray(x,y,z);collider["worldBounds"]=new JObject{["min"]=new JArray(x-.5,y-.5,z-.5),["max"]=new JArray(x+.5,y+.5,z+.5)};return row;}
        static Entity[] Derived(World world)=>world.Entities.Values.Where(e=>e.Has("inferred-training")).ToArray();
        static void GroupingAndUnknowns()
        {
            var registry=Parse(Fixture(EmbeddedBounds(Object(1,"MAIN1F_A18FX1_MONITOR_BODY"),40,1,20),EmbeddedBounds(Object(2,"MAIN1F_A18FX1_MONITOR_SCREEN"),40,1,20),Object(3,"MAIN1F_A18_WALL_0",42),Object(4,"MAIN1F_A18_SIDELIGHT_01",44),Object(5,"MAIN1F_NEW_INDOOR_ST_WEST_FLIGHT_1_RAILS",46),Object(6,"unknown-opaque",48),Object(7,"MAIN1F_A18FX1_MONITOR_BODY",50,false),Object(8,"MAIN1F_A18FX1_RIGHT_LAMP_BODY",52,bounds:false)));
            var world=World(registry);var inference=new StationCapabilityInference(Profiles);int sourceCount=world.Entities.Count;var report=inference.Apply(world,registry);
            Check(report.AssemblyCount==3&&Derived(world).Count(e=>e.Has("electrical")&&!e.Has("power-source"))==1,"monitor render parts group into one electrical assembly");
            var monitor=Derived(world).Single(e=>e.Kind=="inferred-station-electrical-device");
            Check(monitor.X==40&&monitor.Y==1&&monitor.Z==20&&registry.Objects[Id(1)].WorldPosition.All(v=>v==0),"embedded source geometry uses qualified bounds center rather than world0 pivot");
            Check(monitor.Links.Contains(Id(1))&&monitor.Links.Contains(Id(2))&&monitor.Links.Any(id=>world.Entities[id].Has("power-source")),"assembly binds both exact source parts and owned supply");
            Check(Derived(world).Where(e=>e.Kind=="inferred-station-barrier"||e.Kind=="inferred-station-glazing").All(e=>!e.Has("electrical")&&!e.Has("combustible")),"opaque barrier and sidelights do not acquire wiring/fuel");
            Check((int)report.Json["inactiveObjectsExcluded"]==1&&report.UnknownObjectCount==3,"inactive, FLIGHT, unknown opaque and unsupported bounds remain explicit");
            Check(registry.Objects.All(p=>world.Entities.ContainsKey(p.Key)&&world.Entities[p.Key].Has("render-only")),"all source input rows retained inert");
            Check(world.Entities.Count==sourceCount+4,"three assemblies plus one supply, never per-render-part causes");
            string digest=world.Digest();var repeat=inference.Apply(world,registry);Check(repeat.AddedEntities==0&&world.Digest()==digest,"reapply is idempotent");
            new CausalKernel().Step(world);digest=world.Digest();inference.Apply(world,registry);Check(world.Digest()==digest,"reapply preserves evolved mutable causal state");
            Check(world.Fields.Keys.Count(k=>k.StartsWith("inference.station.profiles.sha256.word",StringComparison.Ordinal))==8,"all256 exact profile hash bits bind world state");
        }
        static void AliasesAndAtomicity()
        {
            var parent=Object(1,"typed-parent",10);var child=Object(2,"MAIN1F_A18FX1_MONITOR_BODY",10,parent:Id(1));child["localTransform"]["position"]=new JArray(0,0,0);
            var raw=Fixture(parent,child);((JArray)raw["sourceBindings"]).Add(new JObject{["objectId"]=Id(1),["entityId"]="typed:existing",["relation"]="alias",["capabilities"]=new JArray(),["evidence"]=new JObject{["path"]="workers/simulator/data/platform-source.json",["sha256"]=new string('a',64),["kind"]="exact-source-node"}});
            var registry=Parse(raw);var world=new World{Seed=17};var typed=new Entity{Id="typed:existing",Kind="existing"};typed.Tags.Add("semantic");world.Add(typed);registry.Apply(world);
            var inference=new StationCapabilityInference(Profiles);var report=inference.Apply(world,registry);Check(report.AddedEntities==0&&(int)report.Json["typedAliasObjectsExcluded"]==2,"typed source ancestor excludes all child duplicate causes");
            registry=Parse(Fixture(Object(1,"MAIN1F_A18FX1_MONITOR_BODY",12)));world=World(registry);var clean=World(registry);inference.Apply(clean,registry);
            var collision=Derived(clean).Last();world.Add(new Entity{Id=collision.Id,Kind="wrong"});string before=world.Digest();Reject(()=>inference.Apply(world,registry),"late derived ID collision rejects");Check(world.Digest()==before,"all graph/profile checks precede mutation");
            world=World(registry);world.Fields["inference.station.profiles.sha256.word0"]=0;before=world.Digest();Reject(()=>inference.Apply(world,registry),"partial/stale profile metadata rejects");Check(before==world.Digest(),"hash rejection atomic");
        }
        static void ProfileBoundaries()
        {
            var bad=JObject.Parse(Profiles);((JArray)bad["profiles"].Single(p=>(string)p["id"]=="unknown-barrier")["tags"]).Add("electrical");Reject(()=>new StationCapabilityInference(bad.ToString()),"opaque wiring overreach rejects");
            bad=JObject.Parse(Profiles);((JArray)bad["profiles"].Single(p=>(string)p["id"]=="unknown-barrier")["tags"]).Add("combustible");Reject(()=>new StationCapabilityInference(bad.ToString()),"opaque combustible overreach rejects");
            bad=JObject.Parse(Profiles);bad["profiles"].Single(p=>(string)p["id"]=="electrical-device")["state"]["fault"]=1;Reject(()=>new StationCapabilityInference(bad.ToString()),"forced fault profile rejects");
            bad=JObject.Parse(Profiles);bad["profiles"].Single(p=>(string)p["id"]=="electrical-device")["variations"]["fire"]=new JArray(1,1);Reject(()=>new StationCapabilityInference(bad.ToString()),"seeded event flag rejects");
            var registry=Parse(Fixture(Object(1,"MAIN1F_A18C1_WOOD_DISPLAY_LINER",5),Object(2,"MAIN1F_A18N_DOOR_GLASS_0",8),Object(3,"MAIN1F_A18N_DOOR_FRAME_0",8)));
            var world=World(registry);var receipt=new StationCapabilityInference(Profiles).Apply(world,registry);Check(Derived(world).Count(e=>e.Has("combustible"))==1,"only literal named wood profile grants fuel");
            Check(Derived(world).All(e=>!e.Has("door")&&!e.Tags.Any(t=>t.StartsWith("source-leaf:",StringComparison.Ordinal))),"unqualified hinge/pivot cannot grant physical door motion");
            var door=(JObject)receipt.Json["doorMotionCandidates"].Single();Check((string)door["geometryQualifiedSingleLeafId"]==Id(2)&&!(bool)door["operable"],"exact single leaf candidate excludes frame and records unsupported motion");
        }
        static void SeedStability()
        {
            var a=Parse(Fixture(Object(1,"MAIN1F_A18FX1_MONITOR_BODY",10),Object(2,"MAIN1F_A18FX1_MONITOR_SCREEN",10)));
            var b=Parse(Fixture(Object(3,"MAIN1F_A07_WALL_0",100),Object(2,"MAIN1F_A18FX1_MONITOR_SCREEN",10),Object(1,"MAIN1F_A18FX1_MONITOR_BODY",10)));
            var wa=World(a,91);var wb=World(b,91);var inference=new StationCapabilityInference(Profiles);inference.Apply(wa,a);inference.Apply(wb,b);
            var ea=Derived(wa).Single(e=>e.Kind=="inferred-station-electrical-device");var eb=Derived(wb).Single(e=>e.Kind==ea.Kind);
            Check(ea.Id==eb.Id&&ea.Get("wear")==eb.Get("wear")&&ea.Get("insulation")==eb.Get("insulation"),"unrelated source part/order does not shift assembly random streams");
        }
        static void CausalSupplyBoundary()
        {
            var registry=Parse(Fixture(Object(1,"MAIN1F_A18FX1_MONITOR_BODY",20),Object(2,"MAIN1F_NEW_WC_EM_COUNTER_3BASIN",20)));
            var inference=new StationCapabilityInference(Profiles);var powered=World(registry);var isolated=World(registry);inference.Apply(powered,registry);inference.Apply(isolated,registry);
            foreach(var w in new[]{powered,isolated}){w.Fields["rate.electrical-short"]=1e9;Derived(w).Single(e=>e.Has("water-source")).Set("leak",1);}
            Derived(isolated).Single(e=>e.Has("power-source")).Set("power.isolated",1);
            new CausalKernel().Step(powered);new CausalKernel().Step(isolated);
            var a=Derived(powered).Single(e=>e.Kind=="inferred-station-electrical-device");var b=Derived(isolated).Single(e=>e.Kind==a.Kind);
            Check(a.Get("wetness")>0&&a.Get("power")==1&&a.Get("fault")==1&&a.Get("spark")==1,"explicit water pressure and owned energized supply produce fault causally under boundary test rate");
            Check(b.Get("wetness")>0&&b.Get("power")==0&&b.Get("fault")==0&&b.Get("spark")==0,"same water input without energized supply cannot produce electrical energy");
            Check(powered.Journal.Any(t=>t.Target==a.Id&&t.Mechanism=="conductive-fault"),"inferred assembly participates in kernel causal journal");
        }
        static void FullSnapshot()
        {
            const string directory="output/simulator/world-snapshot/ec2f5f3b6097bcc7";
            var receipt=JObject.Parse(File.ReadAllText(directory+"/receipt.json"));string json=File.ReadAllText(directory+"/world-objects.json");
            var registry=WorldObjectRegistry.Parse(json,(string)receipt["inventory"]["sha256"],SceneHash);
            var world=PlatformWorldFactory.Create(File.ReadAllText("workers/simulator/data/platform-source.json"),File.ReadAllText("workers/simulator/data/semantic-coverage.json"),20261013);registry.Apply(world);int before=world.Entities.Count;
            var inference=new StationCapabilityInference(Profiles);var result=inference.Apply(world,registry);
            Check(registry.Objects.Count==4115&&world.Entities.Count==before+result.AddedEntities&&result.AssemblyCount>100,"full source inventory creates bounded assemblies while retaining all4115 inputs");
            Check((int)result.Json["typedAliasObjectsExcluded"]>=602&&result.UnknownObjectCount>0,"all602 active KTX typed rows and unknown station capabilities remain bounded; inactive rows independently excluded");
            Check(Derived(world).Where(e=>e.Has("electrical")&&!e.Has("power-source")).All(e=>e.Links.Any(id=>world.Entities.ContainsKey(id)&&world.Entities[id].Has("power-source"))),"every inferred electrical device has explicit owned supply");
            Check(Derived(world).All(e=>e.Get("fire")==0&&e.Get("fault")==0&&e.Get("leak")==0),"no production profile forces an incident");
            string digest=world.Digest();inference.Apply(world,registry);Check(world.Digest()==digest,"full snapshot idempotence");
            Directory.CreateDirectory("output/simulator/checks");File.WriteAllText("output/simulator/checks/station-capability-inference.json",result.Json.ToString(Formatting.Indented)+"\n");
            Console.WriteLine("Station inferred assemblies="+result.AssemblyCount+" helper entities="+result.AddedEntities+" unknown="+result.UnknownObjectCount+" profile_sha="+result.ProfilesSha256);
        }
    }
}
