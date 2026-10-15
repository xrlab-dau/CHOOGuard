using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Seeded
{
    [Serializable] public sealed class CoverageSource { public string id, path, sha256; }
    [Serializable] public sealed class CoverageReference { public string source_id, pointer, node_path; }
    [Serializable] public sealed class CoverageEntity
    {
        public string id, parent_id, kind, mode, claim, operation_claim;
        public SourcePoint position;
        public string[] tags, operations, links;
        public SortedDictionary<string,double> state;
        public CoverageReference[] source_refs;
    }
    [Serializable] public sealed class CoverageBounds { public double[] min, max; }
    [Serializable] public sealed class CoveragePart
    {
        public string id, source_id, node_path, name, physical_parent_id, status, reason;
        public int vertices, polygons;
        public CoverageBounds bounds;
    }
    [Serializable] public sealed class CoverageUnsupported { public string id, category, reason; }
    [Serializable] public sealed class CoverageConsist
    {
        public string id, node_path, role, physical_entity_id, reason;
        public bool passenger_supported, operationally_connected;
    }
    [Serializable] public sealed class CoverageReport
    {
        public int visual_parts, physical_entities, state_connected, operation_connected, navigation_points,
            source_seats, passenger_compartments, operational_passenger_compartments, visual_cars, operation_bound_visual_parts, unsupported_visual_parts;
        public bool ids_unique, parent_dedup;
        public SortedDictionary<string,int> unsupported_categories;
        public string count_claim;
    }
    [Serializable] public sealed class CoverageManifest
    {
        public int schema_version;
        public string claim, inventory_sha256;
        public CoverageSource[] sources;
        public CoveragePart[] visual_parts;
        public CoverageEntity[] physical_entities, navigation_points;
        public CoverageConsist[] visual_consist;
        public CoverageUnsupported[] unsupported;
        public CoverageReport report;
    }

    /// <summary>Source identities and conservative model contracts. Render rows never become risk entities.</summary>
    public static class WorldCoverage
    {
        public const int SchemaVersion = 2;
        public const string Car04NativeProofSha256 = "70636069729c70d8f51464d03955f779d54ddea0874cb34133c72b26435a507f";
        static readonly CoverageSource[] Car04Evidence = {
            new CoverageSource{id="car04-proposal",path="workers/simulator/geometry_runtime/car04-source-qualification/passenger-semantic-proposal.json",sha256="ea43ea2d7bdd447914037adeb7358c894b8f43a1762df2ea0878932896dd3361"},
            new CoverageSource{id="car04-source-leaves",path="workers/simulator/geometry_runtime/car04-source-qualification/source-leaf-contract.json",sha256="99b012e9dd661edf1e7ec4f0f0a544d461708e47a3d1271bf599b8aa64c4b06d"},
            new CoverageSource{id="car04-source-seats",path="workers/simulator/geometry_runtime/car04-source-qualification/source-seat-correspondence-packet.json",sha256="6587023e3929511395bf1a53b9612a4157097e3c2bb1c18f5fbdbef1804e57d5"},
            new CoverageSource{id="car04-source-frame",path="workers/simulator/geometry_runtime/car04-source-qualification/source-geometry.json",sha256="fbb9db344f38d7f3cc419e434da3eed283b530a0b380b55464d0ef120a722bf5"},
            new CoverageSource{id="car04-native-proof",path="workers/simulator/geometry_runtime/car04-source-qualification/unity-source-intercar-v4-local-precision-PASS.json",sha256=Car04NativeProofSha256}
        };
        static readonly string[] CabinIds = {"cabin-door:4:near","cabin-door:4:far"};
        static readonly HashSet<string> Operations = new HashSet<string>(new[]{"move_to","sit","stand","open_door","close_door","isolate_power","hold_train","cordon","clear_cordon"},StringComparer.Ordinal);
        static readonly HashSet<string> SafeAddTags = new HashSet<string>(new[]{"semantic","seat","place","occupiable","source-coordinate","training-model"},StringComparer.Ordinal);
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        static void Require(bool value,string reason) { if(!value)throw new ArgumentException("Coverage: "+reason); }
        static bool Valid(SourcePoint p)=>p!=null&&Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        static bool Hash(string s)=>s!=null&&Regex.IsMatch(s,"^[a-f0-9]{64}$");
        static bool SameSet(IEnumerable<string> values,params string[] expected)=>values!=null&&values.Count()==expected.Length&&new HashSet<string>(values,StringComparer.Ordinal).SetEquals(expected);
        static bool PointEquals(SourcePoint p,JToken token)=>token!=null&&new[]{"x","y","z"}.All(axis=>Math.Abs((axis=="x"?p.x:axis=="y"?p.y:p.z)-(double)token[axis])<1e-9);
        static void StateEquals(CoverageEntity e,params string[] keys)=>Require(SameSet(e.state.Keys,keys),"qualified state keys only: "+e.id);
        static IEnumerable<CoverageEntity> Entities(CoverageManifest m)=>m.physical_entities.Concat(m.navigation_points);

        public static CoverageManifest Parse(string json)
        {
            Require(!string.IsNullOrWhiteSpace(json),"manifest required");
            var m=JsonConvert.DeserializeObject<CoverageManifest>(json);
            Require(m!=null&&m.schema_version==SchemaVersion,"unsupported schema version");
            Require(m.sources!=null&&m.sources.Length>=4&&m.physical_entities!=null&&m.navigation_points!=null&&m.visual_parts!=null&&m.visual_consist!=null&&m.unsupported!=null&&m.report!=null,"complete inventories required");
            Require(!string.IsNullOrWhiteSpace(m.claim)&&Hash(m.inventory_sha256),"claim and inventory hash required");
            var sources=new HashSet<string>(StringComparer.Ordinal);
            foreach(var s in m.sources)
                Require(s!=null&&!string.IsNullOrEmpty(s.id)&&sources.Add(s.id)&&Hash(s.sha256)&&!string.IsNullOrEmpty(s.path)&&!Path.IsPathRooted(s.path)&&!s.path.Split('/','\\').Any(x=>x==".."),"unique source ID, scoped path and SHA256 required");
            Require(new[]{"ktx","platforms","platform-details","station-points","fbx-inventory","car04-derivation"}.All(sources.Contains)&&m.sources.First(s=>s.id=="fbx-inventory").sha256==m.inventory_sha256,"complete geometry/coordinate/derivation source hash chain required");
            Require(Car04Evidence.All(expected=>m.sources.Any(s=>s.id==expected.id&&s.path==expected.path&&s.sha256==expected.sha256))&&m.sources.Any(s=>s.id=="car04-access"&&s.path=="workers/simulator/data/car04-access.json"),"exact reviewed Car04 source qualification hash chain required");
            var all=Entities(m).ToArray();var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var e in all)
            {
                Require(e!=null&&!string.IsNullOrEmpty(e.id)&&ids.Add(e.id)&&!string.IsNullOrEmpty(e.kind)&&Valid(e.position),"unique entity IDs and finite positions required");
                Require(e.mode=="existing"||e.mode=="add","unknown entity mode");
                Require(e.state!=null&&e.state.All(x=>!string.IsNullOrEmpty(x.Key)&&Finite(x.Value))&&e.tags!=null&&e.operations!=null&&e.operations.All(Operations.Contains),"valid state/tags/operations required");
                Require(e.source_refs!=null&&e.source_refs.Length>0&&e.source_refs.All(x=>x!=null&&sources.Contains(x.source_id)&&(!string.IsNullOrEmpty(x.pointer)||!string.IsNullOrEmpty(x.node_path))),"qualified source references required");
                Require(!string.IsNullOrWhiteSpace(e.claim),"entity claim boundary required");
                if(e.mode=="add")
                {
                    bool cabin=CabinIds.Contains(e.id)&&e.kind=="door";
                    Require((e.kind=="seat"||e.kind=="platform-wait"||(e.kind=="car"&&e.id=="car:4")||cabin)&&e.tags.All(t=>SafeAddTags.Contains(t)||(cabin&&t=="door"))&&e.tags.Contains("semantic")&&e.tags.Contains("source-coordinate"),"new source places cannot gain inferred hazard capabilities");
                    if(e.kind=="seat")Require(SameSet(e.operations,"move_to","sit","stand"),"seat operations are occupancy abstractions only");
                    if(e.kind=="platform-wait")Require(SameSet(e.operations,"move_to"),"source wait points only support movement");
                }
                if(e.kind=="seat")Require(e.parent_id!=null&&e.parent_id.StartsWith("car:",StringComparison.Ordinal)&&e.state.ContainsKey("occupied")&&new[]{"x","y","z"}.All(k=>e.state.ContainsKey("destination."+k)),"seat parent, occupancy and aisle destination required");
            }
            ValidateCar04Contracts(m);
            foreach(var e in all)
            {
                Require(string.IsNullOrEmpty(e.parent_id)||ids.Contains(e.parent_id),"dangling physical parent: "+e.id);
                Require(e.links==null||e.links.All(ids.Contains),"dangling entity link: "+e.id);
                var seen=new HashSet<string>(StringComparer.Ordinal);var current=e;
                while(!string.IsNullOrEmpty(current.parent_id))
                {Require(seen.Add(current.id),"parent cycle: "+e.id);current=all.First(x=>x.id==current.parent_id);}
            }
            var primaryIds=new HashSet<string>(ids,StringComparer.Ordinal);var paths=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in m.visual_parts)
            {
                Require(p!=null&&!string.IsNullOrEmpty(p.id)&&primaryIds.Add(p.id)&&sources.Contains(p.source_id)&&!string.IsNullOrEmpty(p.node_path)&&paths.Add(p.source_id+"|"+p.node_path),"unique visual IDs/source paths required");
                Require(p.status=="operation-bound"||p.status=="parent-only"||p.status=="unsupported","unknown visual status");
                Require(string.IsNullOrEmpty(p.physical_parent_id)||ids.Contains(p.physical_parent_id),"unregistered visual physical parent");
                Require(p.status=="unsupported"||!string.IsNullOrEmpty(p.physical_parent_id),"connected visual requires one physical parent");
                Require(!string.IsNullOrWhiteSpace(p.reason)&&p.vertices>=0&&p.polygons>=0&&p.bounds?.min?.Length==3&&p.bounds?.max?.Length==3&&p.bounds.min.Concat(p.bounds.max).All(Finite)&&Enumerable.Range(0,3).All(i=>p.bounds.min[i]<=p.bounds.max[i]),"visual geometry and reason required");
                if(p.status=="operation-bound")Require(!CabinIds.Contains(p.physical_parent_id)&&m.physical_entities.Any(e=>e.id==p.physical_parent_id&&e.kind=="door"&&e.operations.Contains("open_door")),"operation-bound visual needs a whole qualified logical door; Car04 combined meshes only have qualified subsets");
            }
            var unsupported=new HashSet<string>(StringComparer.Ordinal);
            foreach(var u in m.unsupported)
                Require(u!=null&&unsupported.Add(u.id)&&m.visual_parts.Any(p=>p.id==u.id&&p.status!="operation-bound")&&!string.IsNullOrWhiteSpace(u.category)&&!string.IsNullOrWhiteSpace(u.reason),"unsupported IDs must reference unqualified visual rows with reason");
            Require(m.visual_parts.Where(p=>p.status!="operation-bound").All(p=>unsupported.Contains(p.id)),"all unqualified visuals need unsupported ledger rows");
            foreach(var c in m.visual_consist)
                Require(c!=null&&!string.IsNullOrEmpty(c.id)&&primaryIds.Add(c.id)&&!string.IsNullOrEmpty(c.node_path)&&!string.IsNullOrWhiteSpace(c.reason)&&(!c.passenger_supported||m.physical_entities.Any(e=>e.id==c.physical_entity_id&&e.kind=="car"))&&(c.role!="powerhead"||!c.passenger_supported),"visual consist/passenger distinction invalid");
            var r=m.report;
            Require(r.ids_unique&&r.parent_dedup&&r.visual_parts==m.visual_parts.Length&&r.physical_entities==m.physical_entities.Length&&r.navigation_points==m.navigation_points.Length&&r.state_connected==m.physical_entities.Count(e=>e.state.Count>0)&&r.operation_connected==m.physical_entities.Count(e=>e.operations.Length>0)&&r.source_seats==m.physical_entities.Count(e=>e.kind=="seat")&&r.passenger_compartments==m.physical_entities.Count(e=>e.kind=="car")&&r.visual_cars==m.visual_consist.Length&&r.operation_bound_visual_parts==m.visual_parts.Count(p=>p.status=="operation-bound")&&r.unsupported_visual_parts==m.unsupported.Length,"reported counts do not match inventories");
            Require(r.operational_passenger_compartments==m.visual_consist.Count(c=>c.operationally_connected),"operational compartment count mismatch");
            var groups=m.unsupported.GroupBy(x=>x.category).ToDictionary(x=>x.Key,x=>x.Count(),StringComparer.Ordinal);
            Require(r.unsupported_categories!=null&&groups.Count==r.unsupported_categories.Count&&groups.All(x=>r.unsupported_categories.ContainsKey(x.Key)&&r.unsupported_categories[x.Key]==x.Value),"unsupported category counts mismatch");
            return m;
        }

        static void ValidateCar04Contracts(CoverageManifest m)
        {
            Require(!Entities(m).Any(e=>e.id=="door:4"),"Car04 external door remains blocked by original luggage rack");
            var car=m.physical_entities.SingleOrDefault(e=>e.id=="car:4");
            Require(car!=null&&car.kind=="car"&&car.mode=="add"&&car.parent_id=="train:ktx"&&SameSet(car.tags,"semantic","source-coordinate","training-model")&&SameSet(car.operations,"move_to")&&SameSet(car.links,CabinIds),"Car04 exact intercar compartment contract required");
            StateEquals(car,"integrity","occupancy","reachable","accessible");
            Require(car.state["integrity"]==1&&car.state["occupancy"]==0&&car.state["reachable"]==1&&car.state["accessible"]==1&&car.source_refs.Length==2&&car.source_refs.Any(r=>r.source_id=="car04-access"&&r.pointer=="/body_id")&&car.source_refs.Any(r=>r.source_id=="ktx"&&r.node_path=="KTX_Sancheon_Single_10Car_Static/Car04_T4 4car"),"Car04 qualified initial availability and original body identity required");
            for(int index=0;index<CabinIds.Length;index++)
            {
                var door=m.physical_entities.SingleOrDefault(e=>e.id==CabinIds[index]);
                Require(door!=null&&door.kind=="door"&&door.mode=="add"&&door.parent_id=="car:4"&&SameSet(door.tags,"semantic","source-coordinate","door","training-model")&&SameSet(door.operations,"move_to","open_door","close_door")&&SameSet(door.links,"car:4",index==0?"car:3":"car:5"),"only exact manually operable Car04 cabin doors are qualified");
                StateEquals(door,"open","jammed","locked","integrity","power","accessible","reachable");
                Require(door.state.All(s=>s.Value==(s.Key=="jammed"||s.Key=="locked"?0:1))&&door.source_refs.Length==1&&door.source_refs[0].source_id=="car04-access"&&door.source_refs[0].pointer=="/cabin_doors/"+index&&!string.IsNullOrWhiteSpace(door.operation_claim),"manual cabin door defaults and exact resource reference required");
            }
            var seats=m.physical_entities.Where(e=>e.parent_id=="car:4"&&e.kind=="seat").ToArray();
            Require(seats.Length==44&&!Entities(m).Any(e=>e.id.StartsWith("seat:04-",StringComparison.Ordinal)&&!seats.Contains(e)),"exact44 new source seats; prior40 candidate identities remain unactivated");
            foreach(var seat in seats)
            {
                Require(Regex.IsMatch(seat.id,"^seat:04-SRC-(0[1-9]|1[01])[ABCD]$")&&seat.mode=="add"&&SameSet(seat.tags,"semantic","seat","place","occupiable","source-coordinate","training-model")&&SameSet(seat.links,"car:4",CabinIds[0],CabinIds[1])&&seat.source_refs.Any(r=>r.source_id=="car04-access"&&Regex.IsMatch(r.pointer??"","^/seats/[0-9]+$")),"source cushion identities and exact intercar links required");
                StateEquals(seat,"occupied","accessible","reachable","seat.yaw","destination.x","destination.y","destination.z");
                Require(seat.state["occupied"]==0&&seat.state["accessible"]==1&&seat.state["reachable"]==1,"source seat initial occupancy contract required");
            }
        }

        /// <summary>Verify bytes at repository source paths before staging or accepting a portable run.</summary>
        public static void VerifySources(CoverageManifest manifest,string repositoryRoot)
        {
            VerifySources(manifest,path=>File.ReadAllBytes(Path.Combine(repositoryRoot,path)));
        }
        public static void VerifySources(CoverageManifest manifest,Func<string,byte[]> readSource)
        {
            if(manifest==null||readSource==null)throw new ArgumentNullException();
            var bytesById=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            foreach(var source in manifest.sources)
            {
                byte[] bytes=readSource(source.path);string actual;
                using(var hash=SHA256.Create())actual=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                Require(actual==source.sha256,"source hash mismatch: "+source.path);
                bytesById[source.id]=bytes;
            }
            // Check source-shaped identities/coordinates and complete FBX coverage,
            // so a hand-edited manifest cannot drift while retaining source hashes.
            var station=JObject.Parse(System.Text.Encoding.UTF8.GetString(bytesById["station-points"]));
            var inventory=JObject.Parse(System.Text.Encoding.UTF8.GetString(bytesById["fbx-inventory"]));
            VerifyCar04Sources(manifest,bytesById);
            var objects=inventory["objects"].ToArray();
            foreach(var e in Entities(manifest))foreach(var reference in e.source_refs)
            {
                if(reference.source_id=="station-points"&&!string.IsNullOrEmpty(reference.pointer))
                {
                    JToken value=station;
                    foreach(var part in reference.pointer.Split('/').Skip(1))
                    {int index;value=value is JArray&&int.TryParse(part,out index)?value[index]:value[part.Replace("~1","/").Replace("~0","~")];Require(value!=null,"source pointer missing: "+e.id);}
                    if(e.kind=="seat")
                    {
                        Require(e.id=="seat:"+(string)value["id"],"seat source identity mismatch");
                        foreach(var axis in new[]{"x","y","z"})
                        {double position=axis=="x"?e.position.x:axis=="y"?e.position.y:e.position.z;Require(Math.Abs(position-(double)value["anchor"][axis])<1e-9&&Math.Abs(e.state["destination."+axis]-(double)value["aisle"][axis])<1e-9,"seat source anchor/aisle mismatch: "+e.id);}
                    }
                    if(e.kind=="platform-wait")
                    {
                        Require(e.id=="source-point:"+(string)value["id"],"navigation source identity mismatch");
                        Require(Math.Abs(e.position.x-(double)value["position"]["x"])+Math.Abs(e.position.y-(double)value["position"]["y"])+Math.Abs(e.position.z-(double)value["position"]["z"])<1e-9,"navigation source position mismatch");
                    }
                }
                if(!string.IsNullOrEmpty(reference.node_path))Require(objects.Any(o=>(string)o["source_id"]==reference.source_id&&(string)o["node_path"]==reference.node_path),"FBX node reference missing: "+e.id);
            }
            var meshes=objects.Where(o=>(string)o["type"]=="MESH").ToArray();
            Require(meshes.Length==manifest.visual_parts.Length,"FBX visual inventory is incomplete");
            foreach(var p in manifest.visual_parts)
                Require(meshes.Any(o=>(string)o["source_id"]==p.source_id&&(string)o["node_path"]==p.node_path&&(int)o["vertices"]==p.vertices&&(int)o["polygons"]==p.polygons),"visual source geometry mismatch: "+p.id);
        }

        static void VerifyCar04Sources(CoverageManifest manifest,Dictionary<string,byte[]> bytes)
        {
            Func<string,JObject> json=id=>JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes[id]));
            var access=json("car04-access");var proposal=json("car04-proposal");var proof=json("car04-native-proof");
            var leaves=json("car04-source-leaves");var seatDerivation=json("car04-source-seats");
            var candidate=json("car04-derivation")["candidate"];var car=manifest.physical_entities.Single(e=>e.id=="car:4");
            Require(Math.Abs(car.position.x-((double)candidate["min"]["x"]+(double)candidate["max"]["x"])/2)<1e-9&&Math.Abs(car.position.z-((double)candidate["min"]["z"]+(double)candidate["max"]["z"])/2)<1e-9&&Math.Abs(car.position.y-(double)candidate["floor"])<1e-9,"retained Car04 body coordinates cannot drift during qualification");
            Require((string)access["schema"]=="car04-access-v1"&&(bool)access["production_activated"]&&(string)access["body_id"]=="car:4"&&access["external_door_operation"].Type==JTokenType.Null&&!(bool)access["field_seat_count_verified"],"qualified access resource boundary required");
            Require((string)access["source_sha256"]==manifest.sources.Single(s=>s.id=="ktx").sha256,"Car04 resource FBX source mismatch");
            foreach(var field in new[]{"body_id","source_sha256","external_door_operation","external_door_reason","seats","cabin_doors","access_routes","source_model_seat_count","field_seat_count_verified"})
                Require(JToken.DeepEquals(access[field],proposal[field]),"Car04 access resource differs from exact reviewed proposal: "+field);
            Require(JToken.DeepEquals(access["frame"],json("car04-source-frame")["frame"]),"Car04 source frame mismatch");
            var provenance=access["source_provenance"] as JArray;
            Require(provenance!=null&&provenance.Count==Car04Evidence.Length&&Car04Evidence.All(s=>provenance.Count(p=>(string)p["id"]==s.id&&(string)p["path"]==s.path&&(string)p["sha256"]==s.sha256)==1),"access resource requires exact qualification provenance");
            Require((bool)proof["passed"]&&!(bool)proof["source_assets_modified"]&&(bool)proof["all_original_mesh_references_restored"]&&(int)proof["owned_objects_remaining"]==0&&(bool)proof["external_door_still_blocked"]&&(string)proof["source_leaf_contract_sha256"]==manifest.sources.Single(s=>s.id=="car04-source-leaves").sha256&&(string)proof["source_seat_packet_sha256"]==manifest.sources.Single(s=>s.id=="car04-source-seats").sha256,"source proof does not qualify this leaf/seat derivation");
            Require(proof["source_mesh_partitions"].Count()==3&&proof["source_mesh_partitions"].All(p=>(bool)p["all_original_vertex_channels_exact"]&&(bool)p["stationary_triangle_partition_exact"]&&(int)p["kept_triangles"]+(int)p["near_leaf_triangles"]+(int)p["far_leaf_triangles"]==(int)p["source_triangles"]),"exact partial mesh partitions required");
            foreach(string side in new[]{"near_leaf_triangles","far_leaf_triangles"})Require(proof["source_mesh_partitions"].Sum(p=>(int)p[side])==276,"276 original source triangles per cabin leaf required");
            foreach(string stage in new[]{"open_routes","closed_routes","restored_routes"})
                Require(proof[stage].Count()==2&&proof[stage].All(p=>(bool)p["complete_path"]==(stage!="closed_routes")&&(bool)p["capsule"]["passed"]==(stage!="closed_routes")),"source route open/closed/restored capsule qualification required");
            Require(proof["source_cushion_correspondence"].Count()==44&&proof["source_cushion_correspondence"].All(p=>(bool)p["passed"]&&(bool)p["source_pad_hit"]&&(int)p["backrest_source_vertex_samples"]>0),"44 source cushion/backrest correspondences required");
            Require((int)proof["source_floor_nav_adapter"]["source_floor_triangles_added"]==0&&!(bool)proof["source_floor_nav_adapter"]["new_physical_floor_or_collider"],"source routes cannot invent physical floor geometry");
            var sourceSeats=access["seats"].ToArray();
            for(int index=0;index<sourceSeats.Length;index++)
            {
                var source=sourceSeats[index];var seat=manifest.physical_entities.Single(e=>e.id==(string)source["id"]);
                Require(PointEquals(seat.position,source["floor_anchor"])&&seat.state["seat.yaw"]==(double)source["facing_yaw"]&&new[]{"x","y","z"}.All(axis=>Math.Abs(seat.state["destination."+axis]-(double)source["aisle_approach"][axis])<1e-9),"Car04 exact source cushion anchor/aisle mismatch: "+seat.id);
                Require(seat.source_refs.Length==2&&seat.source_refs.Count(r=>r.source_id=="car04-access"&&r.pointer=="/seats/"+index)==1&&source["source_refs"].All(r=>seat.source_refs.Any(actual=>actual.source_id==(string)r["source_id"]&&actual.node_path==(string)r["node_path"])),"Car04 source seat pointer/node mismatch");
                Require(seatDerivation["seats"].Any(s=>(string)s["id"]==(string)source["source_model_label"])&&proof["source_cushion_correspondence"].Any(s=>(string)s["id"]==(string)source["source_model_label"]&&(bool)s["passed"]),"Car04 source seat derivation/proof identity missing");
            }
            foreach(var source in access["cabin_doors"])
            {
                var door=manifest.physical_entities.Single(e=>e.id==(string)source["id"]);
                Require(PointEquals(door.position,source["position"]),"Car04 cabin door source position mismatch");
                var leaf=leaves["leaves"].Single(s=>(string)s["id"]==(string)source["source_leaf_id"]);
                Require(JToken.DeepEquals(leaf["closed_bounds_uvh"],source["closed_bounds_uvh"])&&source["source_parts"].Sum(p=>(int)p["expected_triangles"])==276,"Car04 leaf contract bounds/count mismatch");
                foreach(var part in source["source_parts"])
                    Require(leaf["source_parts"].Any(p=>(string)p["source_mesh"]==(string)part["source_mesh"]&&(int)p["expected_triangles"]==(int)part["expected_triangles"]&&JToken.DeepEquals(p["blender_triangles"],part["blender_triangles"])),"Car04 leaf triangle subset differs from derivation");
            }
        }

        /// <summary>Initialize supported source places, never reset live occupancy or create per-render hazards.</summary>
        public static void Apply(World world,string manifestJson)
        {
            if(world==null)throw new ArgumentNullException(nameof(world));var m=Parse(manifestJson);
            // Check all collisions before mutation, so malformed input cannot partially update a world.
            foreach(var e in Entities(m))
            {
                Entity actual;
                if(world.Entities.TryGetValue(e.id,out actual))
                {
                    Require(actual.Kind==e.kind&&actual.ParentId==e.parent_id,"physical identity collision: "+e.id);
                    if(e.mode=="add")Require(actual.Has("source-coordinate")&&SameSet(actual.Tags,e.tags)&&SameSet(actual.Links,e.links??new string[0])&&Math.Abs(actual.X-e.position.x)+Math.Abs(actual.Y-e.position.y)+Math.Abs(actual.Z-e.position.z)<.0001,"source-place collision: "+e.id);
                }
                else Require(e.mode=="add","required base entity missing: "+e.id);
            }
            foreach(var e in Entities(m))
            {
                Entity actual;bool added=!world.Entities.TryGetValue(e.id,out actual);
                if(added)
                {actual=new Entity{Id=e.id,ParentId=e.parent_id,Kind=e.kind,X=e.position.x,Y=e.position.y,Z=e.position.z};foreach(var tag in e.tags)actual.Tags.Add(tag);world.Add(actual);}
                foreach(var state in e.state)if(!actual.State.ContainsKey(state.Key))actual.Set(state.Key,state.Value);
                if(added&&e.links!=null)foreach(var link in e.links)actual.Links.Add(link);
            }
        }
    }
}
