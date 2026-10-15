using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    // Opt-in isolated-project test harness. Never runs in an ordinary simulator session.
    public sealed class WholeWorldAcceptanceRunner : MonoBehaviour
    {
        public PlatformSeededSimulator Simulator;
        public JObject Result {get;private set;}
        JObject report;
        IEnumerator Start()
        {
            var manifest=JObject.Parse(Resources.Load<TextAsset>("SourceManifest").text);
            report=new JObject { ["play_mode"]=Application.isPlaying,["unity_version"]=Application.unityVersion,["staging_fingerprint"]=(string)manifest["runtime_fingerprint"],["proof"]="Actual isolated Unity frames and source geometry; inferred training mechanics/procedures, no field-fidelity claim",["network_calls"]=0 };
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                bool more=false;object current=null;Exception failure=null;
                try {more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){failure=e;}
                if(failure!=null){report["passed"]=false;report["exception"]=failure.ToString();Result=report;yield break;}
                if(!more){stack.Pop();continue;}if(current is IEnumerator child){stack.Push(child);continue;}yield return current;
            }
            Result=report;
        }
        IEnumerator Run()
        {
            var sim=Simulator;sim.enabled=false;sim.UseSubscription=false;
            // No ambient opt-in may turn an acceptance fixture into a paid/model request.
            string prior=Environment.GetEnvironmentVariable("CHOOGUARD_ENABLE_MODELS");Environment.SetEnvironmentVariable("CHOOGUARD_ENABLE_MODELS","0");
            if(sim.WorldSnapshot==null)throw new InvalidOperationException("Whole-world snapshot required");
            bool invalid=!sim.StartRun("invalid seed",false);sim.NewSeed();bool random=ulong.TryParse(sim.InitialSeed,out _);
            bool first=sim.StartRun("20261013",false);string digest=sim.State.Digest();int records=sim.State.Entities.Count;yield return null;
            bool second=sim.StartRun("20261013",false);bool same=digest==sim.State.Digest()&&records==sim.State.Entities.Count;yield return null;
            bool other=sim.StartRun("20261014",false)&&digest!=sim.State.Digest();yield return null;
            bool restart=sim.StartRun("20261013",false);yield return null;
            report["source_assembly_hash"]=sim.SourceFingerprint;
            var snapshot=sim.WorldSnapshot;
            int registered=snapshot.Registry.Objects.Keys.Count(id=>sim.State.Entities.ContainsKey(id));
            int identities=snapshot.Objects.Count;
            var trainMeshFilters=snapshot.TrainVisual.GetComponentsInChildren<MeshFilter>(true);
            var sourceMeshes=snapshot.TrainVisual.GetComponentsInChildren<SnapshotObjectIdentity>(true).Where(i=>i.SourceMesh!=null).ToArray();
            var generatedMeshes=trainMeshFilters.Where(m=>m.GetComponent<SnapshotObjectIdentity>()==null).ToArray();
            int meshes=sourceMeshes.Length;
            var expectedSourceMeshes=new SnapshotMeshContract(snapshot.Registry,snapshot.TrainVisual.GetComponent<SnapshotObjectIdentity>().ObjectId);
            var meshIdentityCheck=expectedSourceMeshes.Compare(sourceMeshes.Select(i=>i.ObjectId));
            report["source_mesh_identity_contract"]=meshIdentityCheck;
            int trainBindings=sim.GetComponentsInChildren<SemanticBinding>(true).Count(b=>b.EntityId=="train:ktx");
            bool sourceFiltersPresent=sourceMeshes.All(i=>i.GetComponent<MeshFilter>()!=null);
            bool generatedQualified=generatedMeshes.All(m=>m.name.IndexOf("_SOURCE_CABIN_",StringComparison.Ordinal)>=0);
            report["seed_and_registry"]=new JObject { ["invalid_seed_refused"]=invalid,["fresh_seed_uint64"]=random,["same_seed_state_and_detail_idempotent"]=same,["different_seed_diverges"]=other,["inventory_objects"]=snapshot.Registry.Objects.Count,["expected_inventory_objects"]=snapshot.Registry.Objects.Count,["expected_source_train_meshes"]=expectedSourceMeshes.ExpectedMeshSourceIds.Count,["source_mesh_identity_contract"]=meshIdentityCheck.DeepClone(),["registered_source_objects"]=registered,["live_source_identities"]=identities,["train_source_meshes_including_inactive"]=meshes,["source_mesh_count_basis"]="SnapshotObjectIdentity.SourceMesh != null, independent of generated runtime geometry",["train_runtime_meshfilters_total"]=trainMeshFilters.Length,["train_generated_runtime_meshfilters"]=generatedMeshes.Length,["generated_runtime_meshes"]=new JArray(generatedMeshes.Select(m=>m.name)),["generated_meshes_are_qualified_car04_parts"]=generatedQualified,["source_filters_present"]=sourceFiltersPresent,["train_semantic_roots"]=trainBindings,["raw_prefab_instances"]=(int?)snapshot.Report["raw_prefab_instances"]??-1,["full_world_fingerprint"]=snapshot.Fingerprint,["passed"]=first&&second&&restart&&invalid&&random&&same&&other&&registered==snapshot.Registry.Objects.Count&&identities==snapshot.Registry.Objects.Count&&(bool)meshIdentityCheck["passed"]&&sourceFiltersPresent&&generatedQualified&&trainMeshFilters.Length==meshes+generatedMeshes.Length&&trainBindings==1&&(int?)snapshot.Report["raw_prefab_instances"]==0 };
            report["full_world_snapshot"]=snapshot.Report.DeepClone();report["navigation"]=sim.Navigation.Report.DeepClone();
            yield return StationMovement(sim);
            yield return DetailedManipulation(sim);
            yield return ResponseCommunication(sim);
            yield return RecordedReplay(sim);
            Environment.SetEnvironmentVariable("CHOOGUARD_ENABLE_MODELS",prior);
            string[] checks={"seed_and_registry","detailed_manipulation","response_communication","station_npc_motion","detailed_recorded_replay"};
            report["passed"]=Application.isPlaying&&checks.All(k=>(bool?)report[k]?["passed"]==true);
        }
        static bool Complete(Vector3 a,Vector3 b)
        {var p=new NavMeshPath();return NavMesh.CalculatePath(a,b,NavMesh.AllAreas,p)&&p.status==NavMeshPathStatus.PathComplete;}
        static Vector3 Point(Entity e) => new Vector3((float)e.Get("mech.center.x",e.X),(float)e.Get("mech.center.y",e.Y),(float)e.Get("mech.center.z",e.Z));
        IEnumerator StationMovement(PlatformSeededSimulator sim)
        {
            var result=new JObject {["passed"]=false};report["station_npc_motion"]=result;
            var npc=sim.Npcs.Values.FirstOrDefault(n=>n.Agent.isOnNavMesh&&sim.State.Entities[n.EntityId].Get("population.station.assumed")==1);
            if(npc==null){result["reason"]="No actual seeded station NPC on current NavMesh";yield break;}
            var actor=sim.State.Entities[npc.EntityId];var start=npc.transform.position;
            var destination=sim.State.Entities.Values.Where(e=>e.Kind=="station-walk-place"&&Vector3.Distance(Point(e),start)>1.5f&&Vector3.Distance(Point(e),start)<12).OrderBy(e=>Vector3.Distance(Point(e),start)).FirstOrDefault(e=>Complete(start,Point(e)));
            if(destination==null){result["reason"]="No complete source-qualified nearby station goal";yield break;}
            npc.PendingArrival=false;npc.PendingFailure=false;sim.QueueCommand(npc.EntityId,destination.Id,"move_to");sim.Tick();
            float until=Time.realtimeSinceStartup+20;
            while(Time.realtimeSinceStartup<until&&!npc.PendingArrival&&!npc.PendingFailure){npc.Project(actor);yield return null;}
            float moved=Vector3.Distance(start,npc.transform.position),distance=Vector3.Distance(npc.transform.position,Point(destination));bool arrival=npc.PendingArrival;
            result["id"]=npc.EntityId;result["start"]=PlatformSeededSimulator.Point(start);result["end"]=PlatformSeededSimulator.Point(npc.transform.position);result["destination"]=destination.Id;result["source_floor_id"]=destination.ParentId;result["actual_displacement_m"]=moved;result["goal_distance_m"]=distance;result["arrival_feedback"]=arrival;result["navigation_failure"]=npc.LastFailure;
            sim.Tick();bool observed=actor.Get("goal.complete")>.5;result["core_arrival_observed"]=observed;result["passed"]=arrival&&observed&&moved>1&&distance<.8f&&sim.WorldSnapshot.Registry.Objects.ContainsKey(destination.ParentId);
        }
        sealed class Contact
        {
            public DetailedPartBinding Part;public RaycastHit Hit;public Ray Ray;
            public JObject Json(PlatformSeededSimulator sim) => new JObject {["owner"]=Part.OwnerId,["part"]=Part.PartId,["training_proxy"]=Part.TrainingProxy,["ray_origin"]=PlatformSeededSimulator.Point(Ray.origin),["ray_direction"]=PlatformSeededSimulator.Point(Ray.direction),["hit_point"]=PlatformSeededSimulator.Point(Hit.point),["hit_distance_m"]=Hit.distance,["collider"]=Hit.collider.name,["source_object_id"]=sim.WorldSnapshot.ResolveSourceObject(Hit.collider)};
        }
        bool AimSourceLeaf(PlatformSeededSimulator sim,out Contact contact,ISet<string> skippedOwners=null)
        {
            contact=null;Physics.SyncTransforms();
            var control=sim.State.Entities.Values.FirstOrDefault(e=>sim.ResponseControls?.Engine.Role(e.Id)==ResponseRole.Control);Vector3 reference=control==null?sim.Player.transform.position:Point(control);
            foreach(var part in sim.GetComponentsInChildren<DetailedPartBinding>(true).Where(p=>!p.TrainingProxy&&p.PartId=="leaf"&&p.OwnerId.StartsWith("door:",StringComparison.Ordinal)&&p.GetComponent<Collider>()!=null&&(skippedOwners==null||!skippedOwners.Contains(p.OwnerId))).OrderBy(p=>Vector3.Distance(p.GetComponent<Collider>().bounds.center,reference)).ThenBy(p=>p.OwnerId,StringComparer.Ordinal))
            {
                if(!int.TryParse(part.OwnerId.Substring(5),out int car))continue;
                var spec=sim.Source.train.cars.FirstOrDefault(c=>c.number==car);if(spec==null)continue;
                var bounds=part.GetComponent<Collider>().bounds;
                foreach(var desired in new[]{PlatformNavigation.V(spec.door.outside),bounds.center+Vector3.forward*1.2f,bounds.center+Vector3.back*1.2f,bounds.center+Vector3.right*1.2f,bounds.center+Vector3.left*1.2f})
                {
                    if(!sim.Navigation.TrySpawn(desired,out var feet))continue;
                    sim.Player.Pose(feet,0);sim.Player.View.transform.LookAt(bounds.center);Physics.SyncTransforms();
                    var ray=sim.Player.View.ViewportPointToRay(new Vector3(.5f,.5f,0));
                    if(!Physics.Raycast(ray,out var hit,2.4f,~(1<<2),QueryTriggerInteraction.Collide))continue;
                    var actual=hit.collider.GetComponentInParent<DetailedPartBinding>();
                    if(actual==null||actual.TrainingProxy||actual.OwnerId!=part.OwnerId||actual.PartId!="leaf")continue;
                    contact=new Contact {Part=actual,Hit=hit,Ray=ray};return true;
                }
            }
            return false;
        }
        static MechanicalInput Mechanical(PlatformSeededSimulator sim,Contact contact,string action,double analog=0,double duration=0,double clearance=1)
        {
            return new MechanicalInput {Tick=sim.State.Tick,Sequence=(long)sim.State.Entities["player"].Get("mech.last-sequence")+1,ActorId="player",OwnerId=contact.Part.OwnerId,PartId="leaf",Action=action,Analog=analog,Duration=duration,Contact=contact.Hit.collider!=null,Clearance=clearance};
        }
        static double Clearance(PlatformSeededSimulator sim,Contact contact,double analog,double duration,out JObject observed)
        {
            var leaves=sim.GetComponentsInChildren<DetailedPartBinding>(true).Where(p=>!p.TrainingProxy&&p.PartId=="leaf"&&p.OwnerId==contact.Part.OwnerId).Select(p=>p.transform).Distinct().ToArray();
            var measured=sim.DetailedControls.MeasureClearance(contact.Part.OwnerId,analog,duration);
            observed=measured.Json();observed["owned_source_leaf_count"]=leaves.Length;observed["same_query_as_player_controller"]=true;
            return measured.Qualified&&leaves.Length>0?measured.Clearance:0;
        }
        JObject PhysicalClearanceContract(PlatformSeededSimulator sim)
        {
            // Explicit geometry-only negative/positive fixtures, outside every source/NavMesh region.
            // They never become source objects, population, operational equipment or an emergency.
            var result=new JObject {["scope"]="Isolated actual Unity triangle/capsule fixtures; no source collider/geometry edits",["passed"]=false};
            var root=new GameObject("Explicit isolated physical-clearance fixtures");root.transform.position=new Vector3(1024,64,1024);var meshes=new List<Mesh>();string digest=sim.State.Digest();
            try
            {
                Func<string,Vector3[],int[],GameObject> surface=(name,vertices,indices)=>
                {
                    var mesh=new Mesh {name=name+" fixture triangles",vertices=vertices,triangles=indices};mesh.RecalculateBounds();meshes.Add(mesh);
                    var go=new GameObject(name,typeof(MeshFilter),typeof(MeshCollider));go.transform.SetParent(root.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;return go;
                };
                var leaf=surface("Fixture moving leaf",new[]{new Vector3(0,-.25f,-.25f),new Vector3(0,.25f,-.25f),new Vector3(0,.25f,.25f),new Vector3(0,-.25f,.25f)},new[]{0,1,2,0,2,3});
                var marker=leaf.AddComponent<DetailedPartBinding>();marker.OwnerId="fixture:clearance-leaf";marker.PartId="leaf";marker.TrainingProxy=true;
                surface("Fixture wide AABB with distant surfaces",new[]{new Vector3(-1,-.5f,-3),new Vector3(1,-.5f,-3),new Vector3(1,.5f,-3),new Vector3(-1,.5f,-3),new Vector3(-1,-.5f,3),new Vector3(1,-.5f,3),new Vector3(1,.5f,3),new Vector3(-1,.5f,3)},new[]{0,1,2,0,2,3,4,6,5,4,7,6});
                Physics.SyncTransforms();var clear=SourceLeafClearance.Measure(new[]{leaf.transform},Vector3.right*.5f,marker.OwnerId);result["separated_surfaces_with_overlapping_aabb"]=clear.Json();
                var blocker=surface("Fixture actual perpendicular crossing plate",new[]{new Vector3(.25f,-1,-1),new Vector3(.25f,1,-1),new Vector3(.25f,1,1),new Vector3(.25f,-1,1)},new[]{0,1,2,0,2,3});
                Physics.SyncTransforms();var blocked=SourceLeafClearance.Measure(new[]{leaf.transform},Vector3.right*.5f,marker.OwnerId);result["actual_crossing_surface"]=blocked.Json();DestroyImmediate(blocker);
                var person=new GameObject("Fixture NPC capsule geometry");person.SetActive(false);person.transform.SetParent(root.transform,false);person.transform.localPosition=new Vector3(.25f,0,0);var agent=person.AddComponent<NavMeshAgent>();agent.enabled=false;var npc=person.AddComponent<SeededNpcController>();npc.enabled=false;npc.EntityId="fixture:npc-capsule";var capsule=person.AddComponent<CapsuleCollider>();capsule.radius=.1f;capsule.height=1;capsule.direction=1;capsule.isTrigger=true;person.SetActive(true);
                Physics.SyncTransforms();var personBlocked=SourceLeafClearance.Measure(new[]{leaf.transform},Vector3.right*.5f,marker.OwnerId);result["actual_npc_capsule"]=personBlocked.Json();
                result["npc_fixture_navmesh_claim"]="None: agent disabled, actual CapsuleCollider geometry and analytic sweep only";
                result["passed"]=clear.Qualified&&clear.Clearance>.999&&blocked.Qualified&&blocked.Clearance<.99&&personBlocked.Qualified&&personBlocked.Clearance<.99&&digest==sim.State.Digest();
            }
            finally{DestroyImmediate(root);foreach(var mesh in meshes)if(mesh!=null)DestroyImmediate(mesh);Physics.SyncTransforms();}
            return result;
        }
        IEnumerator DetailedManipulation(PlatformSeededSimulator sim)
        {
            var result=new JObject {["passed"]=false,["claim"]="INFERRED_TRAINING; one source-qualified external leaf probe, not nine-door coverage"};report["detailed_manipulation"]=result;
            if(sim.DetailedControls==null||sim.DetailedControls.Mechanics==null){result["reason"]="Detailed controls not initialized";yield break;}
            var contract=PhysicalClearanceContract(sim);result["physical_clearance_contract"]=contract;
            var skipped=new HashSet<string>(StringComparer.Ordinal);var attempts=new JArray();Contact contact=null;bool movable=false;
            for(int trial=0;trial<7&&AimSourceLeaf(sim,out contact,skipped);trial++)
            {
                skipped.Add(contact.Part.OwnerId);sim.Tick();double candidate=Clearance(sim,contact,-1,.5,out var measure);attempts.Add(new JObject {["contact"]=contact.Json(sim),["sweep"]=measure,["positive_qualified_clearance"]=candidate>0});
                if(candidate>0){movable=true;break;}
            }
            result["actual_qualified_leaf_attempts"]=attempts;
            if(!movable){result["reason"]="Every tested actual-ray qualified leaf sweep refused; no geometry was forced through a collider";yield break;}
            sim.Tick();result["contact_observation"]=contact.Json(sim);var owner=sim.State.Entities[contact.Part.OwnerId];double before=owner.Get("mech.part.leaf");Vector3 rest=contact.Part.transform.position;string nav=sim.Navigation.Version;
            sim.QueueMechanicalInput(Mechanical(sim,contact,"grip"));sim.Tick();
            double clearance=Clearance(sim,contact,-1,.5,out var observed);result["clearance_observation"]=observed;
            sim.QueueMechanicalInput(Mechanical(sim,contact,"drag",-1,.5,clearance));sim.Tick();yield return null;
            double after=owner.Get("mech.part.leaf");float shifted=Vector3.Distance(rest,contact.Part.transform.position);bool partial=after>0&&after<before;
            var catalog=OperationCatalog.CreateDefault();bool bypass=!catalog.Available(sim.State,"player",owner.Id).Any(o=>o.Name=="close_door"||o.Name=="open_door");
            result["owner"]=owner.Id;result["leaf_before"]=before;result["leaf_after"]=after;result["source_leaf_displacement_m"]=shifted;result["legacy_button_refused"]=bypass;result["navigation_version_before"]=nav;result["navigation_version_after"]=sim.Navigation.Version;
            int car=int.Parse(owner.Id.Substring(5));var capsule=sim.Navigation.PhysicalDoorProbes(car).First;bool blocked=!sim.Navigation.DoorLinks.ContainsKey(car)&&(bool?)capsule?["passed"]==false;
            result["direct_capsule_and_links"]=new JObject {["door_links"]=sim.Navigation.DoorLinks.ContainsKey(car),["physical_probe"]=capsule?.DeepClone(),["partial_leaf_refuses_direct_passage"]=blocked};
            result["passed"]=(bool?)contract["passed"]==true&&contact.Hit.distance<=2.4f&&clearance>0&&partial&&shifted>1e-5f&&bypass&&owner.Get("open")==after&&nav!=sim.Navigation.Version&&blocked;
            sim.QueueMechanicalInput(Mechanical(sim,contact,"release"));sim.Tick();
        }
        static ResponseInput Response(PlatformSeededSimulator sim,string actor,string action,string subject=null,string observation=null,string message=null,string recipient=null,string readback=null)
        {return new ResponseInput {Tick=sim.State.Tick,Sequence=(long)sim.State.Entities[actor].Get("response.last-sequence")+1,ActorId=actor,Action=action,SubjectId=subject,ObservationId=observation,MessageId=message,RecipientId=recipient,ChannelId="face-to-face",Readback=readback};}
        ResponseOutcome ObservedResponse(PlatformSeededSimulator sim,ResponseInput input,out JArray observations)
        {observations=sim.ResponseControls.CaptureObservations(new[]{input});sim.ResponseControls.ApplyObservations(observations);return sim.ResponseControls.Engine.Apply(input);}
        IEnumerator ResponseCommunication(PlatformSeededSimulator sim)
        {
            var result=new JObject {["passed"]=false,["claim"]=ResponseWorkflows.Claim};report["response_communication"]=result;
            if(!sim.StartRun("20261013",false)){result["reason"]=sim.Status;yield break;}yield return null;
            if(sim.ResponseControls==null||!AimSourceLeaf(sim,out var contact)){result["reason"]="No initialized workflow or source contact";yield break;}
            sim.Tick();string source=sim.WorldSnapshot.ResolveSourceObject(contact.Hit.collider);if(string.IsNullOrEmpty(source)){result["reason"]="Contact has no frozen source object identity";yield break;}
            var engine=sim.ResponseControls.Engine;var player=sim.State.Entities["player"];
            var recipient=sim.State.Entities.Values.Where(e=>e.Id!="player"&&engine.Role(e.Id).HasValue&&Vector3.Distance(Point(player),Point(e))<=3.8f).OrderBy(e=>engine.Role(e.Id)==ResponseRole.Control?0:1).ThenBy(e=>Vector3.Distance(Point(player),Point(e))).FirstOrDefault();
            if(recipient==null){result["reason"]="No actual local participant within face-to-face range";yield break;}
            double fault=sim.State.Entities[source].Get("fault"),health=player.Get("health",1);var obs=ObservedResponse(sim,Response(sim,"player","observe",source),out var physical);result["physical_los_observations"]=physical;
            if(!obs.Accepted){result["reason"]=obs.Reason;yield break;}
            var sent=ObservedResponse(sim,Response(sim,"player","report",source,obs.RecordId,recipient:recipient.Id),out var channelObs);result["channel_observations"]=channelObs;
            if(!sent.Accepted){result["reason"]=sent.Reason;yield break;}
            string before=sim.State.Digest();var early=engine.Apply(Response(sim,recipient.Id,"acknowledge",message:sent.RecordId));bool atomic=before==sim.State.Digest();
            int guard=0;while(engine.Inspect(sent.RecordId).State["delivered"]==0&&engine.Inspect(sent.RecordId).State["lost"]==0&&guard++<10){sim.Tick();yield return null;}
            bool delivered=engine.Inspect(sent.RecordId).State["delivered"]==1;ResponseOutcome ack=null,wrong=null,readback=null;
            if(delivered){ack=engine.Apply(Response(sim,recipient.Id,"acknowledge",message:sent.RecordId));before=sim.State.Digest();wrong=engine.Apply(Response(sim,recipient.Id,"readback",message:sent.RecordId,readback:"wrong"));atomic&=before==sim.State.Digest();readback=engine.Apply(Response(sim,recipient.Id,"readback",message:sent.RecordId,readback:engine.Inspect(sent.RecordId).ReadbackCode));}
            var remote=sim.State.Entities.Values.FirstOrDefault(e=>e.Has("source-object")&&Vector3.Distance(Point(player),Point(e))>30);bool rangeRefused=false;
            if(remote!=null){var command=Response(sim,"player","observe",remote.Id);ObservedResponse(sim,command,out var remoteObs);var repeated=engine.Apply(Response(sim,"player","observe",remote.Id));rangeRefused=!repeated.Accepted&&repeated.Reason=="subject_outside_observed_space";result["remote_actual_range_m"]=Vector3.Distance(Point(player),Point(remote));result["remote_los_observation"]=remoteObs;}
            var evidence=engine.Inspect(obs.RecordId);result["observation_id"]=obs.RecordId;result["reported_source_id"]=source;result["report_source_ids"]=new JArray(evidence.Links);result["message_id"]=sent.RecordId;result["recipient_id"]=recipient.Id;result["send_is_not_delivery"]=!sent.Complete&&!early.Accepted&&early.Reason=="message_not_delivered";result["actual_delivery"]=delivered;result["acknowledged"]=ack?.Accepted??false;result["wrong_readback_refused"]=wrong!=null&&!wrong.Accepted;result["exact_readback_accepted"]=readback?.Accepted??false;result["atomic_refusals"]=atomic;result["far_source_refused"]=rangeRefused;
            bool unchanged=sim.State.Entities[source].Get("fault")==fault&&player.Get("health",1)<=health;result["no_instant_repair_or_healing"]=unchanged;
            result["passed"]=obs.Accepted&&evidence.Claim==ResponseWorkflows.Claim&&evidence.Links.Contains(source)&&!sent.Complete&&!early.Accepted&&delivered&&ack?.Accepted==true&&wrong!=null&&!wrong.Accepted&&readback?.Accepted==true&&atomic&&rangeRefused&&unchanged;
        }
        IEnumerator RecordedReplay(PlatformSeededSimulator sim)
        {
            var result=new JObject {["passed"]=false,["network_calls"]=0,["claim"]="Recorded actual contact/LOS/route observations; state replay, not independent Unity-physics reproduction"};report["detailed_recorded_replay"]=result;
            sim.StopRun();sim.InitialSeed="20261013";bool begun=sim.BeginInitialGeneration(new OfflineConditionFixture(),true);
            while(sim.InitialGenerationInFlight){sim.PollInitialGeneration();yield return null;}
            var record=sim.PreparedGenerationRecord;bool started=sim.StartRun("20261013",true);result["offline_upstream_fixture"]="Explicit isolated upstream-rain=.73 fixture; production emergencies unforced";result["fixture_prepared"]=begun;result["run_started"]=started;result["prepared_post_digest"]=record?.PostDigest;result["run_initial_digest"]=sim.State?.Digest();bool initial=started&&record!=null&&record.PostDigest==sim.State.Digest();result["detailed_initial_projection_idempotent"]=initial;
            if(!initial){result["reason"]=sim.Status;result["projection_failure"]=sim.LastInitialProjectionFailure;yield break;}
            if(!AimSourceLeaf(sim,out var contact)){result["reason"]="No source ray for recorded detailed input";yield break;}
            string source=sim.WorldSnapshot.ResolveSourceObject(contact.Hit.collider),observation=null;int mechanicalCount=0,responseCount=0;
            for(int tick=0;tick<20;tick++)
            {
                if(tick==0){sim.QueueMechanicalInput(Mechanical(sim,contact,"grip"));mechanicalCount++;sim.QueueResponseInput(Response(sim,"player","observe",source));responseCount++;}
                if(tick==1){double clear=Clearance(sim,contact,-1,.5,out var sweep);result["recorded_contact"]=contact.Json(sim);result["recorded_clearance"]=sweep;sim.QueueMechanicalInput(Mechanical(sim,contact,"drag",-1,.5,clear));mechanicalCount++;}
                if(tick==2){sim.QueueMechanicalInput(Mechanical(sim,contact,"release"));mechanicalCount++;observation=sim.ResponseControls.Engine.RecordIds.LastOrDefault(id=>sim.ResponseControls.Engine.Inspect(id).Kind=="response-observation"&&sim.ResponseControls.Engine.Inspect(id).SubjectId==source);if(observation!=null){var protect=Response(sim,"player","protect",source,observation);protect.Duration=.5;sim.QueueResponseInput(protect);responseCount++;}}
                foreach(var npc in sim.Npcs)npc.Value.Project(sim.State.Entities[npc.Key]);yield return new WaitForSecondsRealtime(.02f);sim.Tick();
            }
            string path=sim.LastRecordingPath,digest=sim.State.Digest();sim.StopRun();var lines=File.ReadAllLines(path).Select(LocalTapeCodec.Parse).ToArray();var ticks=lines.Where(l=>(string)l["type"]=="Tick").ToArray();
            bool detailed=ticks.Any(t=>t["mechanical_inputs"] is JArray a&&a.Count>0)&&ticks.Any(t=>t["response_inputs"] is JArray a&&a.Count>0)&&ticks.All(t=>t["mechanical_outcomes"] is JArray&&t["response_outcomes"] is JArray&&t["response_observations"] is JArray);
            bool loaded=sim.LoadReplay(path);for(int i=0;i<21&&sim.Running;i++){sim.Tick();yield return null;}
            result["ticks"]=ticks.Length;result["queued_mechanical_inputs"]=mechanicalCount;result["queued_response_inputs"]=responseCount;result["detailed_fields_recorded"]=detailed;result["tape"]=path;result["loaded"]=loaded;result["recorded_digest"]=digest;result["replayed_digest"]=sim.State.Digest();result["status"]=sim.Status;result["outcome_failure"]=sim.LastReplayOutcomeFailure;
            result["passed"]=initial&&ticks.Length==20&&mechanicalCount>=3&&responseCount>=2&&detailed&&loaded&&digest==sim.State.Digest()&&sim.Status.StartsWith("Replay complete",StringComparison.Ordinal);
        }
        sealed class OfflineConditionFixture:IEmergenceProvider
        {
            public string Version=>"offline-wholeworld-upstream-condition-fixture-v1";public bool Available=>true;public long MaximumOutputTokens=>128;public long EstimateInputTokens(EmergenceRequest request)=>128;
            public Task<EmergenceReply> Generate(EmergenceRequest request,CancellationToken cancellation) => Task.FromResult(new EmergenceReply {Seed=request.Seed,BasisDigest=request.BasisDigest,ReturnedModel=Version,Raw="{\"fixture\":\"upstream-rain\"}",InputTokens=128,OutputTokens=32,Changes=new List<EmergenceChange>{new EmergenceChange {EntityId="@world",Property="rain",Value=.73}}});
        }
    }
}
