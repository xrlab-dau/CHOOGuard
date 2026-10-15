using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class StationSemanticInventoryTests
    {
        static void Check(bool value,string reason){if(!value)throw new Exception("Full-domain semantic/geometry: "+reason);}
        public static void Run()
        {
            const string directory="output/simulator/world-snapshot/ec2f5f3b6097bcc7";
            var receipt=JObject.Parse(File.ReadAllText(directory+"/receipt.json"));string json=File.ReadAllText(directory+"/world-objects.json");
            var registry=WorldObjectRegistry.Parse(json,(string)receipt["inventory"]["sha256"],(string)receipt["sourceScene"]["sha256"]);
            var world=PlatformWorldFactory.Create(File.ReadAllText("workers/simulator/data/platform-source.json"),File.ReadAllText("workers/simulator/data/semantic-coverage.json"),20261013);registry.Apply(world);
            var inference=new StationCapabilityInference(File.ReadAllText("workers/simulator/data/station-capability-profiles.json"));var result=inference.Apply(world,registry);var inventory=(JObject)result.Json["sourceAttributionInventory"];
            Check((int)inventory["objectCount"]==registry.Objects.Count&&inventory["objects"].Select(x=>(string)x["sourceId"]).Distinct().Count()==registry.Objects.Count,"exactly one evidence/missing-contract attribution for every frozen source row");
            var rows=inventory["objects"].OfType<JObject>().ToArray();
            Check(rows.Any(x=>(string)x["status"]=="KNOWN_NONCAUSAL_METADATA")&&rows.Any(x=>(string)x["status"]=="UNRESOLVED_CAPABILITIES"),"metadata has no independent cause and unresolved physical roles remain explicit");
            var chair=rows.Single(x=>(string)x["name"]=="MAIN1F_NEW_SF_A11_009_chair");Check((string)chair["status"]=="SUPPORTED_INFERRED_ASSEMBLY"&&chair["semanticBindings"].Single()["role"].Value<string>()=="furniture","literal qualified chair maps to a passive evidence-labelled furniture assembly");
            string chairEntity=(string)chair["semanticBindings"][0]["assemblyId"];Check(!world.Entities[chairEntity].Has("combustible")&&!world.Entities[chairEntity].Has("electrical")&&!world.Entities[chairEntity].Has("moving-body"),"furniture label does not invent fuel, wiring or operable movement");
            Check(rows.Where(x=>(string)x["status"]=="SUPPORTED_INFERRED_ASSEMBLY").All(x=>x["missingContracts"].Any()),"supported role retains unverified material/mechanism/field contracts");
            Check(world.Entities.Values.Where(e=>e.Has("inferred-training")).All(e=>e.Get("fire")==0&&e.Get("fault")==0&&e.Get("leak")==0),"semantic expansion does not seed incident outcomes");
            string digest=world.Digest();Check(inference.Apply(world,registry).AddedEntities==0&&world.Digest()==digest,"whole-source enrichment is deterministic and idempotent");
            var trainBinding=registry.Objects.Values.Single(o=>o.Name=="KTXSource");var meshes=new SnapshotMeshContract(registry,trainBinding.Id);Check(meshes.ExpectedMeshSourceIds.Count==703&&(bool)meshes.Compare(meshes.ExpectedMeshSourceIds)["passed"],"independent immutable train subtree yields exact703source mesh IDs");
            Check(!(bool)meshes.Compare(meshes.ExpectedMeshSourceIds.Skip(1))["passed"],"missing source mesh cannot be replaced by generated runtime mesh count");
            Check(!(bool)meshes.Compare(meshes.ExpectedMeshSourceIds.Concat(new[]{meshes.ExpectedMeshSourceIds[0]}))["passed"],"duplicate source identity refused");
            Check(!(bool)meshes.Compare(meshes.ExpectedMeshSourceIds.Concat(new[]{"generated:looks_like_source"}))["passed"],"unexpected source identity refused");
            var invalid=(JObject)result.Json.DeepClone();invalid["sourceRegistryFingerprint"]=new string('0',64);bool refused=false;try{StationSemanticInventory.Build(registry,invalid);}catch(ArgumentException){refused=true;}Check(refused,"mixed source epoch attribution refused");
            Directory.CreateDirectory("output/simulator/all-domains-core");File.WriteAllText("output/simulator/all-domains-core/source-semantic-inventory.json",inventory.ToString(Formatting.Indented)+"\n");File.WriteAllText("output/simulator/all-domains-core/station-inference.json",result.Json.ToString(Formatting.Indented)+"\n");
            Console.WriteLine("Full-domain frozen4115 source attribution complete; inferred assemblies="+result.AssemblyCount+" newstates="+result.AddedEntities+" unresolved legacy-count="+result.UnknownObjectCount+". Native/latest scene/field coverage remains unverified.");
        }
    }
}
