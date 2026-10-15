using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded.Tests
{
    public static class WorldObjectRegistryTests
    {
        const string SceneHash="ec2f5f3b6097bcc7eae78e01e50ffbbb670583ea355e074ae2f02e04e8bd1e66";
        const string SceneGuid="7518d664dafa749f2b6d5cba42bb798c";
        static string Id(int id)=>"unity:"+SceneGuid+":"+id;
        static void Check(bool condition,string message){if(!condition)throw new Exception("World-object registry: "+message);}
        static void Reject(Action action,string message){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,message);}
        public static void Run()
        {
            FullSavedSnapshot();ParentTransformsAndOrder();InventoryAndAliasAtomicity();NegativeContracts();SpatialAndSeedStability();
            Console.WriteLine("World-object registry: all4115 saved objects, parent/TRS/active contracts, typed aliases, atomic ingestion, immutable records, BVH candidates and independent seed streams PASS.");
        }
        static WorldObjectRegistry Parse(JObject data)
        {string json=data.ToString(Formatting.None);return WorldObjectRegistry.Parse(json,Stable.Hash(json),SceneHash);}
        static JObject Object(int id,string parent=null,double x=0,bool active=true,bool parentActive=true,bool collider=false)
        {
            var position=new JArray(x,0,0);var matrix=new JArray(1,0,0,x,0,1,0,0,0,0,1,0,0,0,0,1);
            var row=new JObject{{"id",Id(id)},{"sceneFileId",id.ToString(System.Globalization.CultureInfo.InvariantCulture)},{"parentId",parent},
                {"name","object-"+id},{"path",parent==null?"object-"+id:"object-1/object-"+id},{"activeSelf",active},{"activeInHierarchy",active&&parentActive},
                {"physicalEligible",collider&&active&&parentActive},{"layer",0},{"tag","Untagged"},
                {"localTransform",new JObject{{"position",position.DeepClone()},{"rotation",new JArray(0,0,0,1)},{"scale",new JArray(1,1,1)}}},
                {"worldTransform",new JObject{{"position",position.DeepClone()},{"rotation",new JArray(0,0,0,1)},{"lossyScale",new JArray(1,1,1)},{"localToWorldMatrix4x4",matrix}}},
                {"components",new JArray()},{"geometryProvenance",new JObject{{"mode","allowlisted"},{"sourceSceneSha256",SceneHash}}},{"sourceBindings",new JArray()}};
            if(collider)((JArray)row["components"]).Add(new JObject{{"fileId",(100000+id).ToString()},{"classId",65},{"type","BoxCollider"},{"enabled",true},{"isTrigger",false},
                {"center",new JArray(0,0,0)},{"size",new JArray(1,1,1)},{"localBounds",new JObject{{"center",new JArray(0,0,0)},{"size",new JArray(1,1,1)}}},
                {"worldBounds",new JObject{{"min",new JArray(x-.5,-.5,-.5)},{"max",new JArray(x+.5,.5,.5)}}},{"boundsEvidence","serialized-box"}});
            return row;
        }
        static JObject Fixture(params JObject[] rows)=>new JObject{{"schemaVersion",WorldObjectRegistry.SchemaVersion},
            {"sourceSnapshot",new JObject{{"scenePath","Assets/ChooGuard/Scenes/FpsStation.unity"},{"sceneGuid",SceneGuid},{"sceneSha256",SceneHash},{"snapshotId","registry-fixture"},{"capturedAtUtc","2026-10-14T00:00:00Z"}}},
            {"frame",new JObject{{"coordinateSystem","Unity left-handed Y-up"},{"registration","preserved-saved-scene"},{"sourceToSimulationMatrix4x4",new JArray(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1)}}},
            {"objects",new JArray(rows)},{"sourceBindings",new JArray()}};
        static JObject Binding(int source,string target)=>new JObject{{"objectId",Id(source)},{"entityId",target},{"relation","part-of"},{"capabilities",new JArray()},
            {"evidence",new JObject{{"path","workers/simulator/data/platform-source.json"},{"sha256",new string('a',64)},{"kind","exact-source-node"}}}};
        static void FullSavedSnapshot()
        {
            const string directory="output/simulator/world-snapshot/ec2f5f3b6097bcc7";
            string json=File.ReadAllText(directory+"/world-objects.json");var receipt=JObject.Parse(File.ReadAllText(directory+"/receipt.json"));
            var registry=WorldObjectRegistry.Parse(json,(string)receipt["inventory"]["sha256"],SceneHash);var raw=JObject.Parse(json);
            Check(registry.Objects.Count==4115&&registry.Report.ObjectCount==4115,"exact full saved object count");
            Check(registry.Report.ActiveCount==3844&&registry.Report.PhysicalEligibleCount==2748,"actual saved hierarchy and physical eligibility counts");
            Check(registry.Objects.Values.Sum(o=>o.Components.Count(c=>c.Type=="MeshFilter"))==3827&&registry.Objects.Values.Sum(o=>o.Components.Count(c=>c.Type=="MeshCollider"))==3028,"all mesh/collider provenance retained");
            Check(registry.Objects.Values.All(o=>o.Id=="unity:"+SceneGuid+":"+o.SceneFileId),"IDs derive from scene GUID/fileID");
            Check(registry.Objects.Values.Where(o=>!o.ActiveInHierarchy).All(o=>!o.PhysicalEligible&&o.WorldBounds==null),"inactive inventory never spatially physical");
            Check(registry.Report.UnsupportedBoundsCount>0&&registry.Report.IndexedColliderObjectCount+registry.Report.UnsupportedBoundsCount==2748,"unknown FBX/native bounds remain explicit unsupported");
            foreach(var row in (JArray)raw["objects"])
            {
                var o=registry.Objects[(string)row["id"]];Check(o.ParentId==(string)row["parentId"]&&o.WorldPosition.SequenceEqual(row["worldTransform"]["position"].Select(v=>(double)v)),"every source row retains parent/world pose");
                Check(JToken.DeepEquals(o.GeometryProvenance,row["geometryProvenance"]),"every provenance record retained");
            }
            Check(registry.UnsupportedCapabilities.Contains("combustion-and-fire")&&registry.Objects.Values.All(o=>!o.MaterialCapabilitiesSupported),"no name/material-reference hazard inference");
            // The construction writer may save a newer original scene while this
            // immutable packet remains valid. Its live-source guard must refuse
            // that drift; never roll back construction to make this fixture pass.
            string liveHash;
            using(var hasher=System.Security.Cryptography.SHA256.Create())
                liveHash=BitConverter.ToString(hasher.ComputeHash(File.ReadAllBytes(registry.ScenePath))).Replace("-","").ToLowerInvariant();
            if(liveHash==registry.SceneSha256)registry.VerifySources(File.ReadAllBytes);
            else Reject(()=>registry.VerifySources(File.ReadAllBytes),"new construction save must fail frozen live-scene guard");
            var world=PlatformWorldFactory.Create(File.ReadAllText("workers/simulator/data/platform-source.json"),File.ReadAllText("workers/simulator/data/semantic-coverage.json"),20261013);
            Check(world.Entities.Count==532,"current typed platform/KTX/NPC baseline retained");int semanticCount=CausalKernel.SemanticEntities(world).Count;
            var result=registry.Apply(world);Check(result.AddedObjects==4115&&world.Entities.Count==4647&&result.BoundObjects==43,"all4115 source objects become world inputs exactly once alongside532 typed states");
            Check(CausalKernel.SemanticEntities(world).Count==semanticCount,"full station inventory cannot amplify independent typed hazards");
            Check(world.Fields.Count(f=>f.Key.StartsWith("source.snapshot.sha256.word",StringComparison.Ordinal))==8,"all256 snapshot digest bits retained exactly as UInt32 words");
            string digest=world.Digest();world.Fields["source.snapshot.sha256.word0"]+=1;Check(world.Digest()!=digest,"source snapshot fingerprint binds replay/world digest");
        }
        static void ParentTransformsAndOrder()
        {
            var parent=Object(1,x:10);parent["localTransform"]["position"]=new JArray(10,2,3);
            double q=Math.Sqrt(.5);parent["localTransform"]["rotation"]=new JArray(0,q,0,q);parent["localTransform"]["scale"]=new JArray(2,3,4);
            parent["worldTransform"]=new JObject{{"position",new JArray(10,2,3)},{"rotation",new JArray(0,q,0,q)},{"lossyScale",new JArray(2,3,4)},{"localToWorldMatrix4x4",new JArray(0,0,4,10,0,3,0,2,-2,0,0,3,0,0,0,1)}};
            var child=Object(2,Id(1));child["localTransform"]["position"]=new JArray(1,2,3);
            child["worldTransform"]=new JObject{{"position",new JArray(22,8,1)},{"rotation",new JArray(0,q,0,q)},{"lossyScale",new JArray(2,3,4)},{"localToWorldMatrix4x4",new JArray(0,0,4,22,0,3,0,8,-2,0,0,1,0,0,0,1)}};
            var a=Parse(Fixture(parent,child));var b=Parse(Fixture((JObject)child.DeepClone(),(JObject)parent.DeepClone()));
            Check(a.Fingerprint==b.Fingerprint&&a.SnapshotSha256!=b.SnapshotSha256,"canonical inventory independent of input order; exact byte hashes remain distinct");
            Check(a.ChildrenOf(Id(1)).SequenceEqual(new[]{Id(2)})&&a.ChildrenOf(Id(2)).Count==0,"parent/children hierarchy indexed");
            var wa=new World{Seed=17};var wb=new World{Seed=17};a.Apply(wa);b.Apply(wb);Check(wa.Digest()!=wb.Digest(),"exact snapshot byte hash binds world replay digest");
            for(int i=0;i<8;i++)wb.Fields["source.snapshot.sha256.word"+i]=wa.Fields["source.snapshot.sha256.word"+i];
            Check(wa.Digest()==wb.Digest(),"source graph ingestion independent of payload order after accounting for exact byte-hash metadata");
            var position=a.Objects[Id(2)].WorldPosition;position[0]=999;var clone=a.Objects[Id(2)].Data;clone["activeSelf"]=false;
            Check(a.Objects[Id(2)].WorldPosition[0]==22&&a.Objects[Id(2)].ActiveSelf,"public arrays/JSON cannot mutate registry");
            var reflected=Object(3);reflected["localTransform"]["scale"]=new JArray(-2,1,1);reflected["worldTransform"]["lossyScale"]=new JArray(2,1,1);reflected["worldTransform"]["localToWorldMatrix4x4"][0]=-2;
            Check(Parse(Fixture(reflected)).Objects[Id(3)].WorldLossyScale[0]==2,"reflection retained in matrix with magnitude scale");
        }
        static void InventoryAndAliasAtomicity()
        {
            var root=Object(1,collider:true);root["name"]="FIRE_ELECTRICAL_WOOD";root["path"]="FIRE_ELECTRICAL_WOOD";
            var inactive=Object(2,active:false,collider:true);var child=Object(3,Id(2),parentActive:false,collider:true);child["path"]="object-2/object-3";
            var fixture=Fixture(root,inactive,child);((JArray)fixture["sourceBindings"]).Add(Binding(1,"car:1"));
            var registry=Parse(fixture);var w=new World{Seed=2};var car=new Entity{Id="car:1",Kind="car",X=8};car.Tags.Add("semantic");car.Set("temperature",24);w.Add(car);
            int semantic=CausalKernel.SemanticEntities(w).Count;string typedState=car.Get("temperature").ToString();var applied=registry.Apply(w);
            Check(applied.AddedObjects==3&&applied.BoundObjects==1&&w.Entities.Count==4,"every object retained including alias and inactive rows");
            Check(w.Entities[Id(1)].ParentId=="car:1"&&w.Entities[Id(1)].Has("render-only")&&!w.Entities[Id(1)].Has("electrical")&&!w.Entities[Id(1)].Has("combustible"),"explicit alias cannot promote hazards from names");
            Check(CausalKernel.SemanticEntities(w).Count==semantic&&car.X==8&&car.Get("temperature").ToString()==typedState,"typed geometry/state and independent risk count preserved");
            string before=w.Digest();var repeated=registry.Apply(w);Check(w.Digest()==before&&repeated.AddedObjects==0&&repeated.ExistingObjects==3,"idempotent reapplication preserves live state/topology");
            var absent=new World();before=absent.Digest();Reject(()=>registry.Apply(absent),"missing typed binding rejects");Check(absent.Digest()==before,"missing binding load atomic");
            var collision=new World();collision.Add(new Entity{Id="car:1",Kind="car"});collision.Add(new Entity{Id=Id(3),Kind="wrong"});before=collision.Digest();Reject(()=>registry.Apply(collision),"late source ID collision rejects");Check(collision.Digest()==before,"late rejection cannot partially add earlier source rows");
            var extra=new WorldObjectBinding(Id(2),"car:1","alias","workers/simulator/data/platform-source.json",new string('b',64),"exact-source-node");
            var withExtra=new World();withExtra.Add(new Entity{Id="car:1",Kind="car"});Check(registry.Apply(withExtra,new[]{extra}).BoundObjects==2,"explicit additional typed association supported");
            Reject(()=>registry.Apply(withExtra),"reapplication cannot silently change association set");
            Check(registry.Query(new ObjectBounds(-10,-10,-10,10,10,10)).Count==1,"inactive colliders excluded from spatial candidates");
        }
        static void NegativeContracts()
        {
            var valid=Fixture(Object(1),Object(2,Id(1)));var bad=(JObject)valid.DeepClone();((JArray)bad["objects"]).Add(bad["objects"][0].DeepClone());Reject(()=>Parse(bad),"duplicate stable IDs reject");
            bad=(JObject)valid.DeepClone();bad["objects"][1]["parentId"]=Id(99);Reject(()=>Parse(bad),"missing parents reject");
            bad=(JObject)valid.DeepClone();bad["objects"][0]["parentId"]=Id(2);Reject(()=>Parse(bad),"parent cycles reject");
            bad=(JObject)valid.DeepClone();bad["objects"][1]["worldTransform"]["position"][0]=1;Reject(()=>Parse(bad),"world position/matrix drift reject");
            bad=(JObject)valid.DeepClone();bad["objects"][1]["localTransform"]["position"][0]=1;Reject(()=>Parse(bad),"parent/local/world matrix drift reject");
            bad=(JObject)valid.DeepClone();bad["objects"][1]["localTransform"]["rotation"][0]=1;Reject(()=>Parse(bad),"unnormalized quaternion reject");
            bad=(JObject)valid.DeepClone();bad["objects"][1]["activeInHierarchy"]=false;Reject(()=>Parse(bad),"inconsistent active ancestry reject");
            bad=(JObject)valid.DeepClone();bad["objects"][1]["physicalEligible"]=true;Reject(()=>Parse(bad),"physical eligibility cannot be invented without collider");
            bad=(JObject)valid.DeepClone();bad["objects"][1]["worldTransform"]["position"][0]=double.NaN;Reject(()=>Parse(bad),"nonfinite values reject");
            bad=(JObject)valid.DeepClone();bad["objects"][1]["geometryProvenance"]["sourceSceneSha256"]=new string('0',64);Reject(()=>Parse(bad),"mismatched object source provenance reject");
            bad=(JObject)valid.DeepClone();bad["frame"]["sourceToSimulationMatrix4x4"][3]=.000001;Reject(()=>Parse(bad),"even small registration refit reject");
            bad=(JObject)valid.DeepClone();bad["sourceSnapshot"]["scenePath"]="../outside.unity";Reject(()=>Parse(bad),"source path traversal reject");
            bad=(JObject)valid.DeepClone();bad["sourceSnapshot"]["sceneSha256"]=new string('0',64);Reject(()=>Parse(bad),"mismatched scene hash reject");
            bad=(JObject)valid.DeepClone();var binding=Binding(1,"car:1");((JArray)binding["capabilities"]).Add("combustible");((JArray)bad["sourceBindings"]).Add(binding);Reject(()=>Parse(bad),"source binding cannot infer capabilities");
            bad=Fixture(Object(1,collider:true),Object(2,collider:true));bad["objects"][1]["components"][0]["fileId"]=bad["objects"][0]["components"][0]["fileId"].DeepClone();Reject(()=>Parse(bad),"duplicate component IDs reject");
            bad=Fixture(Object(1,collider:true));bad["objects"][0]["components"][0]["worldBounds"]["max"][0]=10;Reject(()=>Parse(bad),"local/world collider bound mismatch reject");
            string json=valid.ToString(Formatting.None);Reject(()=>WorldObjectRegistry.Parse(json,new string('0',64),SceneHash),"wrong snapshot bytes hash reject");
            string trailing=json+" {}";Reject(()=>WorldObjectRegistry.Parse(trailing,Stable.Hash(trailing),SceneHash),"trailing JSON reject");
            string duplicate=json.Replace("\"schemaVersion\":","\"schemaVersion\":\"duplicate\",\"schemaVersion\":");Reject(()=>WorldObjectRegistry.Parse(duplicate,Stable.Hash(duplicate),SceneHash),"duplicate JSON properties reject");
        }
        static void SpatialAndSeedStability()
        {
            const int count=2048;var rows=Enumerable.Range(1,count).Select(i=>Object(i,x:i*3,collider:true)).ToArray();var fixture=Fixture(rows);var registry=Parse(fixture);
            Check(registry.Objects.Count==count&&registry.Report.IndexedColliderObjectCount==count,"all large inventory rows retained and indexed");
            Check(registry.Query(new ObjectBounds(300-.1,-1,-1,300+.1,1,1)).Select(o=>o.Id).SequenceEqual(new[]{Id(100)}),"local BVH query excludes thousands of distant decorations");
            Check(registry.QuerySegmentCandidates(new[]{299.0,0,0},new[]{301.0,0,0}).Single().Id==Id(100),"segment uses collider bounds candidates");
            var extended=(JObject)fixture.DeepClone();((JArray)extended["objects"]).Add(Object(count+1,x:100000,collider:true));var next=Parse(extended);
            Check(registry.Unit(71,"object-response",4,Id(100))==next.Unit(71,"object-response",4,Id(100)),"unrelated source additions do not shift existing object seed streams");
            Check(registry.CacheKey!=next.CacheKey&&registry.Fingerprint!=next.Fingerprint,"source additions invalidate inventory/cache bindings");
            var pair=Fixture(Object(1,collider:true),Object(2,x:3,collider:true));((JArray)pair["sourceBindings"]).Add(Binding(1,"train:ktx"));((JArray)pair["sourceBindings"]).Add(Binding(2,"train:ktx"));var dedup=Parse(pair);
            Check(dedup.Query(new ObjectBounds(-1,-1,-1,4,1,1)).Count==1,"two source render parts do not become independent physical risk groups");
            Check(dedup.QuerySegmentCandidates(new[]{2.0,0,0},new[]{4.0,0,0}).Single().Id==Id(2),"segment intersection is tested before physical-parent deduplication");
            var parent=Object(1);var child=Object(2,Id(1),collider:true);var hierarchy=Fixture(parent,child);((JArray)hierarchy["sourceBindings"]).Add(Binding(1,"train:ktx"));var inherited=Parse(hierarchy);
            Check(inherited.PhysicalIdentityOf(Id(2))=="train:ktx"&&inherited.QueryObjectCandidates(new ObjectBounds(-1,-1,-1,1,1,1)).Single().Id==Id(2),"source parent association deduplicates exact child colliders without deleting inventory");
            Reject(()=>registry.QuerySegmentCandidates(new[]{double.PositiveInfinity,0,0},new[]{0.0,0,0}),"nonfinite spatial query reject");
            Reject(()=>registry.RelationsFor(Id(1),-1),"negative relation distance reject");
        }
    }
}
