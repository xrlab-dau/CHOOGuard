using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    // Runs only when explicitly attached by the isolated editor acceptance entrypoint.
    // Frame yields are required: carving and NavMeshAgent motion are engine updates.
    public sealed class AcceptancePlayModeRunner : MonoBehaviour
    {
        public JObject Result { get; private set; }
        public PlatformSeededSimulator Simulator;
        IEnumerator Start()
        {
            var sim=Simulator;sim.enabled=false;
            var report=new JObject{["proof"]="Actual Unity PlayMode frames, NavMeshAgent and CharacterController; supplied-model training geometry, no field-fidelity claim.",["source_assembly_hash"]=(string)JObject.Parse(Resources.Load<TextAsset>("SourceManifest").text)["runtime_fingerprint"],["runtime_version"]=World.EngineVersion,["unity_version"]=Application.unityVersion,["play_mode"]=Application.isPlaying};
            bool invalid=!sim.StartRun("invalid seed",false);sim.NewSeed();string random=sim.InitialSeed;
            bool started=sim.StartRun("20261013",false);string initial=sim.State.Digest();sim.StartRun("20261013",false);
            report["navigation"]=sim.Navigation.Report.DeepClone();
            report["seed_ui_contract"]=new JObject{["invalid_seed_refused"]=invalid,["new_seed_is_uint64"]=ulong.TryParse(random,out _),["same_seed_initial_digest"]=initial==sim.State.Digest(),["started"]=started};
            var doors=new JArray();
            foreach(var car in sim.Source.train.cars)
            {
                sim.ProjectDoorState(car.number,true);yield return new WaitForSecondsRealtime(.3f);yield return null;
                var a=PlatformNavigation.V(car.door.outside);var b=PlatformNavigation.V(car.door.inside);
                var open=sim.Navigation.PhysicalDoorProbes(car.number).First;
                bool openPath=Complete(a,b);
                sim.ProjectDoorState(car.number,false);yield return new WaitForSecondsRealtime(.3f);yield return null;
                bool closedPath=Complete(a,b);var closedRoute=new NavMeshPath();NavMesh.CalculatePath(a,b,NavMesh.AllAreas,closedRoute);var closedCorners=new JArray(closedRoute.corners.Select(PlatformSeededSimulator.Point));var closed=sim.Navigation.PhysicalDoorProbes(car.number).First;bool closedAvoided=AvoidsClosedAperture(closedRoute.corners,a,b);
                sim.ProjectDoorState(car.number,true);yield return new WaitForSecondsRealtime(.3f);yield return null;
                bool restoredPath=Complete(a,b);var restored=sim.Navigation.PhysicalDoorProbes(car.number).First;
                doors.Add(new JObject{["car"]=car.number,["open_complete_path"]=openPath,["open_capsule"]=open,["closed_complete_path"]=closedPath,["closed_path_corners"]=closedCorners,["closed_path_avoids_aperture"]=closedAvoided,["closed_alternative_avoids_aperture"]=closedPath&&closedAvoided,["closed_aperture_clearance_radius_m"]=.28f,["closed_carver_obb_size_m"]=new JArray(1.5f,2.6f,.8f),["closed_alternative_route_present"]=closedPath&&closedAvoided,["closed_capsule"]=closed,["restored_complete_path"]=restoredPath,["restored_capsule"]=restored,["passed"]=openPath&&(bool)open["passed"]&&(!closedPath||closedAvoided)&&!(bool)closed["passed"]&&restoredPath&&(bool)restored["passed"]});
            }
            report["door_state_transitions"]=doors;
            var interactCar=sim.Source.train.cars.Last();string doorId="door:"+interactCar.number;
            var binding=sim.GetComponentsInChildren<SemanticBinding>().FirstOrDefault(x=>x.EntityId==doorId&&x.GetComponent<Collider>()!=null);
            var interaction=new JObject{["target"]=doorId,["passed"]=false};
            if(binding!=null&&sim.Navigation.TrySpawn(PlatformNavigation.V(interactCar.door.outside),out var pose))
            {
                sim.Player.Pose(pose,0);sim.Player.View.transform.LookAt(binding.GetComponent<Collider>().bounds.center);sim.Tick();
                bool queued=sim.InteractFromView("close_door");string target=sim.CurrentTarget;sim.Tick();
                bool closed=sim.State.Entities[doorId].Get("open")<.1;
                interaction["ray_target"]=target;interaction["command_queued"]=queued;interaction["closed_by_tick"]=closed;interaction["passed"]=queued&&target==doorId&&closed;
                sim.ProjectDoorState(interactCar.number,true);
            }
            report["fps_interaction"]=interaction;

            // A local waiting-point cordon changes the center surface, then restores it.
            var wait=sim.State.Entities.Values.First(x=>x.Kind=="platform-wait");var center=new Vector3((float)wait.X,(float)wait.Y,(float)wait.Z);
            bool before=NavMesh.SamplePosition(center,out var floor,.15f,NavMesh.AllAreas);center=floor.position;
            wait.X=center.x;wait.Y=center.y;wait.Z=center.z;string beforeVersion=sim.Navigation.Version;
            wait.Set("blocked",1);sim.Navigation.ProjectCordon(wait);yield return new WaitForSecondsRealtime(1);
            bool carved=!NavMesh.SamplePosition(center,out _,.15f,NavMesh.AllAreas);string blockedVersion=sim.Navigation.Version;
            wait.Set("blocked",0);sim.Navigation.ProjectCordon(wait);yield return new WaitForSecondsRealtime(1);
            bool removed=NavMesh.SamplePosition(center,out _,.15f,NavMesh.AllAreas);
            report["local_cordon"]=new JObject{["id"]=wait.Id,["before_surface"]=before,["carved_surface_refused"]=carved,["removed_surface_restored"]=removed,["version_changed"]=beforeVersion!=blockedVersion,["version_restored"]=beforeVersion==sim.Navigation.Version,["passed"]=before&&carved&&removed&&beforeVersion!=blockedVersion&&beforeVersion==sim.Navigation.Version};
            // Use an actual spawned NPC/controller. Keep causal ticks paused during
            // measurement so the community cannot replace the specific test goal.
            var npc=sim.Npcs.Values.FirstOrDefault(x=>x.Agent.isOnNavMesh);var movement=new JObject();
            if(npc!=null)
            {
                var entity=sim.State.Entities[npc.EntityId];var start=npc.transform.position;Vector3 goal=default;bool found=false;
                foreach(var delta in new[]{Vector3.forward*3,Vector3.back*3,Vector3.right*3,Vector3.left*3})
                    if(NavMesh.SamplePosition(start+delta,out var hit,.4f,NavMesh.AllAreas)&&Complete(start,hit.position)){goal=hit.position;found=true;break;}
                if(found)
                {
                    entity.Set("goal.x",goal.x);entity.Set("goal.y",goal.y);entity.Set("goal.z",goal.z);entity.Set("goal.active",1);entity.Set("goal.external",1);
                    float deadline=Time.realtimeSinceStartup+15;
                    while(Time.realtimeSinceStartup<deadline&&!npc.PendingArrival&&!npc.PendingFailure){npc.Project(entity);yield return null;}
                    bool arrival=npc.PendingArrival;float distance=Vector3.Distance(npc.transform.position,goal),moved=Vector3.Distance(start,npc.transform.position);
                    movement=new JObject{["id"]=npc.EntityId,["start"]=PlatformSeededSimulator.Point(start),["goal"]=PlatformSeededSimulator.Point(goal),["end"]=PlatformSeededSimulator.Point(npc.transform.position),["displacement"]=moved,["goal_distance"]=distance,["arrival_feedback"]=arrival,["failure"]=npc.LastFailure,["passed"]=arrival&&moved>1&&distance<.6f};
                    sim.Tick();movement["core_observed_completion"]=entity.Get("goal.complete")>.5;
                }
                else{movement["passed"]=false;movement["reason"]="No complete nearby goal path";}
            }
            else{movement["passed"]=false;movement["reason"]="No actual NPC on NavMesh";}
            report["npc_actual_movement"]=movement;
            yield return ProbeCar04(sim,report);
            sim.StopRun();sim.InitialSeed="20261013";
            bool generationBegun=sim.BeginInitialGeneration(new OfflineConditionFixture(),authorized:true);
            while(sim.InitialGenerationInFlight){sim.PollInitialGeneration();yield return null;}
            var generation=sim.PreparedGenerationRecord;
            bool generatedStarted=sim.StartRun("20261013",true);string generatedInitial=sim.State.Digest();
            report["initial_generation_integration"]=new JObject{["provider"]="offline condition fixture; no real model quality claim",["network_calls"]=0,["generation_started"]=generationBegun,["run_started"]=generatedStarted,["recorded_post_digest"]=generation?.PostDigest,["actual_initial_digest"]=generatedInitial,["passed"]=generationBegun&&generatedStarted&&generation!=null&&generation.PostDigest==generatedInitial&&sim.State.Field("rain")==.73};
            if(!generatedStarted){report["passed"]=false;report["reason"]=sim.Status;report["projection_failure"]=sim.LastInitialProjectionFailure;Result=report;yield break;}
            for(int i=0;i<20;i++){foreach(var actor in sim.Npcs)actor.Value.Project(sim.State.Entities[actor.Key]);yield return new WaitForSecondsRealtime(.04f);sim.Tick();}
            string tape=sim.LastRecordingPath,digest=sim.State.Digest();sim.StopRun();sim.UseSubscription=true;bool loaded=sim.LoadReplay(tape);sim.UseSubscription=false;
            for(int i=0;i<21&&sim.Running;i++){sim.Tick();yield return null;}
            report["recorded_observation_replay"]=new JObject{["loaded"]=loaded,["ticks"]=20,["tape"]=tape,["recorded_digest"]=digest,["replayed_digest"]=sim.State.Digest(),["status"]=sim.Status,["outcome_failure"]=sim.LastReplayOutcomeFailure,["passed"]=loaded&&digest==sim.State.Digest()&&sim.Status.StartsWith("Replay complete",StringComparison.Ordinal)};
            var seed=(JObject)report["seed_ui_contract"];
            report["passed"]=Application.isPlaying&&seed.Properties().All(x=>x.Value.Type==JTokenType.Boolean&&(bool)x.Value)&&doors.Count>0&&doors.All(x=>(bool)x["passed"])&&(bool)report["local_cordon"]["passed"]&&(bool)movement["passed"]&&(bool)interaction["passed"]&&(bool)report["recorded_observation_replay"]["passed"]&&(bool)report["initial_generation_integration"]["passed"]&&(bool)report["car04_passenger_access"]["passed"];
            Result=report;
        }
        static bool AvoidsClosedAperture(Vector3[] corners,Vector3 outside,Vector3 inside)
        {
            var inward=inside-outside;inward.y=0;inward.Normalize();var lateral=new Vector3(inward.z,0,-inward.x);var center=(outside+inside)*.5f;center.y=Mathf.Min(outside.y,inside.y)+1;var half=new Vector3(.75f+.28f,1.3f+.86f,.4f+.28f);
            for(int i=0;i+1<corners.Length;i++)
            {
                var delta=corners[i]+Vector3.up*.86f-center;var a=new Vector3(Vector3.Dot(delta,lateral),delta.y,Vector3.Dot(delta,inward));delta=corners[i+1]+Vector3.up*.86f-center;var b=new Vector3(Vector3.Dot(delta,lateral),delta.y,Vector3.Dot(delta,inward));float lo=0,hi=1;bool intersects=true;
                for(int axis=0;axis<3;axis++){float direction=b[axis]-a[axis];if(Mathf.Abs(direction)<1e-6f){if(Mathf.Abs(a[axis])>half[axis]){intersects=false;break;}}else{float p=(-half[axis]-a[axis])/direction,q=(half[axis]-a[axis])/direction;if(p>q){float swap=p;p=q;q=swap;}lo=Mathf.Max(lo,p);hi=Mathf.Min(hi,q);if(lo>hi){intersects=false;break;}}}
                if(intersects)return false;
            }return true;
        }
        static bool CabinRoutePass(JToken row,bool open)=>((bool)row["complete_path"]==open)&&((bool)row["capsule"]["passed"]==open)&&((bool)row["reverse_capsule"]["passed"]==open);
        IEnumerator ProbeCar04(PlatformSeededSimulator sim,JObject aggregate)
        {
            var access=sim.InteriorAccess;var result=new JObject{["passed"]=false,["production_adapter"]=true};aggregate["car04_passenger_access"]=result;
            if(access==null){result["reason"]="Production Car04Access resource and runtime adapter are absent";yield break;}
            result["source_seat_count"]=access.SourceSeatCount;result["external_door_blocked"]=access.ExternalDoorBlocked();result["no_external_door_operation"]=!sim.State.Entities.ContainsKey("door:4");result["source_geometry"]=access.Report.DeepClone();
            var staffOutside=sim.Player.transform.position;yield return new WaitForSecondsRealtime(.4f);string originalVersion=sim.Navigation.Version;result["open_routes"]=access.Routes();var actions=new JArray();
            foreach(string id in access.DoorIds)
            {
                bool near=id.EndsWith(":near",StringComparison.Ordinal);sim.Player.Pose(Car04InteriorAccess.World(near?-.3f:-14f,1.4f,near?.488f:.638f),0);sim.QueueCommand("player",id,"close_door");sim.Tick();actions.Add(new JObject{["id"]=id,["operation"]="close_door",["core_state_closed"]=sim.State.Entities[id].Get("open")<.1});
            }
            sim.Player.Pose(staffOutside,0);yield return new WaitForSecondsRealtime(.5f);string closedVersion=sim.Navigation.Version;result["closed_routes"]=access.Routes();
            foreach(string id in access.DoorIds)
            {
                bool near=id.EndsWith(":near",StringComparison.Ordinal);sim.Player.Pose(Car04InteriorAccess.World(near?-.3f:-14f,1.4f,near?.488f:.638f),0);sim.QueueCommand("player",id,"open_door");sim.Tick();actions.Add(new JObject{["id"]=id,["operation"]="open_door",["core_state_open"]=sim.State.Entities[id].Get("open")>.9});
            }
            sim.Player.Pose(staffOutside,0);yield return new WaitForSecondsRealtime(.5f);result["restored_routes"]=access.Routes();result["core_door_actions"]=actions;result["navigation_version_changed"]=originalVersion!=closedVersion;result["navigation_version_restored"]=originalVersion==sim.Navigation.Version;result["seat_approaches"]=access.SeatApproaches();
            var movement=new JObject{["passed"]=false};var npc=sim.Npcs.Values.FirstOrDefault(x=>x.Agent.isOnNavMesh);var seat=sim.State.Entities.Values.Where(x=>x.Kind=="seat"&&x.ParentId=="car:4").OrderBy(x=>Math.Abs(x.Get("destination.z")-Car04InteriorAccess.World(-6.5f,1.4f,.448025f).z)).FirstOrDefault();
            if(npc!=null&&seat!=null&&NavMesh.SamplePosition(Car04InteriorAccess.World(5.8f,1.4f,.448025f),out var start,.1f,NavMesh.AllAreas))
            {
                var actor=sim.State.Entities[npc.EntityId];npc.Agent.Warp(start.position);npc.PendingArrival=false;npc.PendingFailure=false;actor.X=start.position.x;actor.Y=start.position.y;actor.Z=start.position.z;actor.Set("goal.active",0);sim.QueueCommand(npc.EntityId,seat.Id,"move_to");sim.Tick();var goal=new Vector3((float)actor.Get("goal.x"),(float)actor.Get("goal.y"),(float)actor.Get("goal.z"));float deadline=Time.realtimeSinceStartup+20;
                while(Time.realtimeSinceStartup<deadline&&!npc.PendingArrival&&!npc.PendingFailure){npc.Project(actor);yield return null;}
                bool arrived=npc.PendingArrival;float error=Vector3.Distance(npc.transform.position,goal),distance=Vector3.Distance(start.position,npc.transform.position);movement=new JObject{["id"]=npc.EntityId,["seat_target"]=seat.Id,["start_neighbor_car"]=3,["start"]=PlatformSeededSimulator.Point(start.position),["destination"]=PlatformSeededSimulator.Point(goal),["end"]=PlatformSeededSimulator.Point(npc.transform.position),["displacement_m"]=distance,["goal_distance_m"]=error,["arrival_feedback"]=arrived,["failure"]=npc.LastFailure};sim.Tick();bool completed=actor.Get("goal.complete")>.5;movement["core_observed_completion"]=completed;movement["passed"]=arrived&&completed&&distance>3&&error<.6f;
            }
            else movement["reason"]="No actual NPC, qualified seat, or supported neighboring vestibule start";
            result["actual_npc_movement"]=movement;bool actionsPass=actions.Count==4&&actions.All(x=>(bool?)(x["core_state_closed"]??x["core_state_open"])==true);
            result["passed"]=access.SourceSeatCount==44&&(bool)result["external_door_blocked"]&&(bool)result["no_external_door_operation"]&&((JArray)result["open_routes"]).All(x=>CabinRoutePass(x,true))&&((JArray)result["closed_routes"]).All(x=>CabinRoutePass(x,false))&&((JArray)result["restored_routes"]).All(x=>CabinRoutePass(x,true))&&actionsPass&&(bool)result["navigation_version_changed"]&&(bool)result["navigation_version_restored"]&&((JArray)result["seat_approaches"]).Count==44&&((JArray)result["seat_approaches"]).All(x=>(bool)x["passed"])&&(bool)movement["passed"];
        }
        sealed class OfflineConditionFixture:IEmergenceProvider
        {
            public string Version=>"offline-engine-contract-fixture-v1";public bool Available=>true;public long MaximumOutputTokens=>128;public long EstimateInputTokens(EmergenceRequest request)=>128;
            public Task<EmergenceReply> Generate(EmergenceRequest request,CancellationToken cancellation)
            {
                return Task.FromResult(new EmergenceReply{Seed=request.Seed,BasisDigest=request.BasisDigest,ReturnedModel=Version,Raw="{\"fixture\":\"upstream-rain\"}",InputTokens=128,OutputTokens=32,Changes=new System.Collections.Generic.List<EmergenceChange>{new EmergenceChange{EntityId="@world",Property="rain",Value=.73}}});
            }
        }
        static bool Complete(Vector3 a,Vector3 b){var p=new NavMeshPath();return NavMesh.CalculatePath(a,b,NavMesh.AllAreas,p)&&p.status==NavMeshPathStatus.PathComplete;}
    }
}
