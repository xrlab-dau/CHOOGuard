using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer.Editor;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class FoundationSimulationTests
    {
        private string folder;
        private SceneSetup[] previous;
        private ConnectedWorldDefinition world;
        private ConnectedRegionView[] views;
        [OneTimeSetUp] public void Build()
        {
            previous = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) Assert.Ignore("Preserve unsaved scenes.");
            folder = "Assets/CHOOguardGenerated/SimulationTest_" + Guid.NewGuid().ToString("N");
            var paths = ConnectedWorldSceneBuilder.Build(folder);
            foreach (var path in paths.Skip(1)) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));
            views = UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None); Physics.SyncTransforms();
        }
        [OneTimeTearDown] public void Clear()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (previous != null && previous.Any(s => s.isActive && s.isLoaded) && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            if (folder != null) AssetDatabase.DeleteAsset(folder);
        }
        private FoundationSimulationProfile Profile()
        {
            var p = JsonUtility.FromJson<FoundationSimulationProfile>(File.ReadAllText("foundation/world/foundation-simulation-profile.json"));
            p.NpcCount = 2; p.NpcSpawnRegions = new[] { "station_concourse_2f" };
            p.Schedule.FirstOnsetTick = p.Schedule.MinimumQuietTicks = p.Schedule.MaximumQuietTicks = 1;
            p.Schedule.Choices = p.Schedule.Choices.Where(c => c.Kind == FoundationIncidentKind.Fire).ToArray();
            p.Incidents = p.Incidents.Where(i => p.Schedule.Choices.Any(c => c.Id == i.ChoiceId)).ToArray(); return p;
        }
        private WorldState Seed()
        {
            var w = JsonUtility.FromJson<WorldState>(File.ReadAllText("foundation/network/connected-world-layout.json"));
            var r = world.Region(world.StartRegionId); var pose = world.Pose(r.Id, r.Hub);
            w.Participants = new[] { new ParticipantState { ParticipantId = "instructor", TeamId = "command", RoleId = "instructor", IsInstructor = true,
                RegionId = pose.RegionId, FrameId = pose.FrameId, Position = pose.Position, LocalPosition = pose.LocalPosition } }; return w;
        }
        private sealed class Sink : ICommitSink { public void Append(ShiftCommit commit) { } }
        [Serializable] private sealed class PerformanceSample
        {
            public string Scope="Editor numeric world fixture,20 synthetic participants+100NPC+2fires; not20 real clients or native load acceptance";
            public int Participants=20,NpcCount=100,WarmupTicks=5,MeasuredTicks=20;
            public double PhysicsTickSeconds=.05,TargetMilliseconds=50,MeanMilliseconds,P95Milliseconds,MaximumMilliseconds;
            public bool AllMeasuredTicksWithinBudget;
            public SimulationTickReport[] Samples;
        }
        [Test, Explicit("Short numeric world profiling; timings are reported separately from test success.")]
        public void ProfileTwentyParticipantsAndOneHundredNpcWorld()
        {
            var profile=Profile(); profile.NpcCount=100; profile.NpcSpawnRegions=world.Regions.Select(r=>r.Id).ToArray();
            var seed=Seed(); seed.Participants=Enumerable.Range(0,20).Select(i=>
            {
                var region=world.Regions[i%world.Regions.Length]; var pose=world.Pose(region.Id,region.Hub);
                return new ParticipantState { ParticipantId="profile-"+i,TeamId=i==0?"command":"team-a",RoleId=i==0?"instructor":"role-01",
                    IsInstructor=i==0,RegionId=pose.RegionId,FrameId=pose.FrameId,Position=pose.Position,LocalPosition=pose.LocalPosition };
            }).ToArray();
            using var simulation=new FoundationWorldSimulation(world,views,profile,seed) { ProfilePhases=true };
            var shift=new AuthoritativeShift(simulation.InitialState,new Sink(),(_,__)=>true,simulation.CanOperate);
            foreach(var p in seed.Participants) shift.SetInputEnabled(p.ParticipantId,true);
            var inputs=seed.Participants.Select((p,i)=>new ServerMovementInput {ParticipantId=p.ParticipantId,Enabled=true,
                Velocity=new Point3(i%2==0?.15f:-.15f,0,0),LoadedRegions=world.Regions.Select(r=>r.Id).ToArray()}).ToArray();
            var samples=new List<SimulationTickReport>();
            for(var i=0;i<25;i++)
            {
                Assert.That(simulation.TryAdvance(shift,inputs,out var report),Is.True,report.Failure);
                if(i>=5) samples.Add(report);
            }
            Assert.That(simulation.NpcCount,Is.EqualTo(100)); Assert.That(shift.ReadSimulation().Participants.Length,Is.EqualTo(20));
            Assert.That(simulation.ActiveIncidentCount,Is.EqualTo(2)); Assert.That(simulation.Tick,Is.EqualTo(25));
            var values=samples.Select(s=>s.TotalMilliseconds).OrderBy(x=>x).ToArray();
            var result=new PerformanceSample {Samples=samples.ToArray(),MeanMilliseconds=values.Average(),P95Milliseconds=values[(int)Math.Ceiling(.95*values.Length)-1],
                MaximumMilliseconds=values.Last(),AllMeasuredTicksWithinBudget=values.All(x=>x<=50)};
            var root="Temp/ChooGuardSimulationPerformance";Directory.CreateDirectory(root);
            File.WriteAllText(root+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json",JsonUtility.ToJson(result,true)+"\n");
            Debug.Log("Synthetic120body world measured20ticks meanMs="+result.MeanMilliseconds+" p95Ms="+result.P95Milliseconds+
                " allTicksWithin50ms="+result.AllMeasuredTicksWithinBudget+"; numerical execution is not performance acceptance.");
        }
        [Serializable] private sealed class EquivalenceReceipt
        {
            public string Scope="25tick120body2fireworld; exactCGP1 and minimum-swept-gap bit comparison between prior and optimized execution";
            public int Participants=20,NpcCount=100,Ticks=25;
            public string[] CheckpointSha256;
            public long[] MinimumGapBits;
        }
        [Test,Explicit("Exact paired world execution; no performance acceptance from the comparison.")]
        public void OptimizedMotionMatchesPriorExecutionForAllTwentyFive120BodyWorldTicks()
        {
            var profile=Profile();profile.NpcCount=100;profile.NpcSpawnRegions=world.Regions.Select(r=>r.Id).ToArray();
            var seed=Seed();seed.Participants=Enumerable.Range(0,20).Select(i=>
            {
                var region=world.Regions[i%world.Regions.Length];var pose=world.Pose(region.Id,region.Hub);
                return new ParticipantState {ParticipantId="profile-"+i,TeamId=i==0?"command":"team-a",RoleId=i==0?"instructor":"role-01",
                    IsInstructor=i==0,RegionId=pose.RegionId,FrameId=pose.FrameId,Position=pose.Position,LocalPosition=pose.LocalPosition};
            }).ToArray();
            using var reference=new FoundationWorldSimulation(world,views,profile,seed) {MotionOptimization=ConnectedWorldMotionAdapter.ExecutionOptimization.None};
            using var optimized=new FoundationWorldSimulation(world,views,profile,seed) {MotionOptimization=ConnectedWorldMotionAdapter.ExecutionOptimization.All};
            var left=new AuthoritativeShift(reference.InitialState,new Sink(),(_,__)=>true,reference.CanOperate);
            var right=new AuthoritativeShift(optimized.InitialState,new Sink(),(_,__)=>true,optimized.CanOperate);
            foreach(var p in seed.Participants){left.SetInputEnabled(p.ParticipantId,true);right.SetInputEnabled(p.ParticipantId,true);}
            var inputs=seed.Participants.Select((p,i)=>new ServerMovementInput {ParticipantId=p.ParticipantId,Enabled=true,
                Velocity=new Point3(i%2==0?.15f:-.15f,0,0),LoadedRegions=world.Regions.Select(r=>r.Id).ToArray()}).ToArray();
            var hashes=new List<string>();var gaps=new List<long>();
            for(var tick=1;tick<=25;tick++)
            {
                Assert.That(reference.TryAdvance(left,inputs,out var full),Is.True,full.Failure);
                Assert.That(optimized.TryAdvance(right,inputs,out var fast),Is.True,fast.Failure);
                var expected=left.ReadSimulation().Checkpoint;var actual=right.ReadSimulation().Checkpoint;
                Assert.That(actual,Is.EqualTo(expected),"CGP1 at tick"+tick);
                var gap=BitConverter.DoubleToInt64Bits(full.Motion.MinimumSweptGapM);
                Assert.That(BitConverter.DoubleToInt64Bits(fast.Motion.MinimumSweptGapM),Is.EqualTo(gap),"minimum gap at tick"+tick);
                hashes.Add(SessionAdmission.Digest(actual));gaps.Add(gap);
            }
            Assert.That(reference.ActiveIncidentCount,Is.EqualTo(2));Assert.That(optimized.ActiveIncidentCount,Is.EqualTo(2));
            var folder="Temp/ChooGuardMotionEquivalence";Directory.CreateDirectory(folder);
            File.WriteAllText(folder+"/"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+".json",JsonUtility.ToJson(new EquivalenceReceipt {CheckpointSha256=hashes.ToArray(),MinimumGapBits=gaps.ToArray()},true)+"\n");
        }
        [Test] public void ServerTickCouplesTwoIncidentsFireCrowdTrainsAndExactRecovery()
        {
            var profile = Profile(); profile.Validate(world);
            using var simulation = new FoundationWorldSimulation(world, views, profile, Seed());
            var shift = new AuthoritativeShift(simulation.InitialState, new Sink(), (_, __) => true, simulation.CanOperate);
            var input = new[] { new ServerMovementInput { ParticipantId = "instructor", LoadedRegions = world.Regions.Select(r => r.Id).ToArray() } };
            for (var i = 0; i < 3; i++) Assert.That(simulation.TryAdvance(shift, input, out var report), Is.True, report.Failure);
            var saved = shift.ExportCheckpoint(); var physical = PhysicalCheckpoint.Decode(saved.SimulationCheckpoint, saved.SimulationDefinitionHash);
            Assert.That(saved.SimulationTick, Is.EqualTo(3)); Assert.That(saved.Entities.Count(e => e.Kind == EntityKind.Incident && e.Active), Is.EqualTo(2));
            Assert.That(saved.Entities.Count(e => e.Kind == EntityKind.Evacuee), Is.EqualTo(2)); Assert.That(physical.Fire.ReleasedHeatJ, Is.GreaterThan(0));
            Assert.That(physical.Trains, Has.Length.EqualTo(2)); Assert.That(physical.Fire.SimulatedSeconds, Is.EqualTo(.15).Within(1e-9));
            Assert.That(shift.Observe("instructor").Entities.Any(e => e.Kind == EntityKind.Incident), Is.False, "No discovery means no source identity.");
            Assert.That(simulation.TryAdvance(shift, input, out var continued), Is.True, continued.Failure);
            var expected = shift.ExportCheckpoint();
            simulation.Restore(saved);
            var recovered = AuthoritativeShift.Restore(saved, new Sink(), (_, __) => true, simulation.CanOperate);
            var frozen = recovered.ReadSimulation().Checkpoint;
            Assert.That(simulation.TryAdvance(recovered, input, out var paused), Is.True, paused.Failure);
            Assert.That(recovered.ReadSimulation().Checkpoint, Is.EqualTo(frozen));
            Assert.That(recovered.Submit("instructor", new WorldCommand { WorldId = saved.WorldId, ShiftId = saved.ShiftId, ParticipantId = "instructor",
                TeamId = "command", CommandId = "resume", Kind = CommandKind.ResumeShift }).Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(simulation.TryAdvance(recovered, input, out var replay), Is.True, replay.Failure);
            Assert.That(recovered.ReadSimulation().Checkpoint, Is.EqualTo(expected.SimulationCheckpoint), "Exact continued physical trajectory.");
            TestContext.WriteLine("Physical checkpoint characters: " + saved.SimulationCheckpoint.Length);
        }
        [Test] public void InvalidLoadedRegionInputCannotAdvanceAnySubsystem()
        {
            using var simulation = new FoundationWorldSimulation(world, views, Profile(), Seed());
            var shift = new AuthoritativeShift(simulation.InitialState, new Sink(), (_, __) => true, simulation.CanOperate);
            var before = shift.ReadSimulation().Checkpoint;
            Assert.That(simulation.TryAdvance(shift, new[] { new ServerMovementInput { ParticipantId = "instructor", LoadedRegions = new[] { "invented" } } }, out _), Is.False);
            Assert.That(shift.ReadSimulation().Checkpoint, Is.EqualTo(before)); Assert.That(simulation.Tick, Is.EqualTo(0));
        }
        [Test] public void ProfileRejectsUnboundIncidentAndInvalidTrainStops()
        {
            var p = Profile(); p.Incidents[0].RegionId = "absent"; Assert.Throws<ArgumentException>(() => p.Validate(world));
            p = Profile(); p.Trains[0].Operation.Stops = Array.Empty<TrainStopDefinition>(); Assert.Throws<ArgumentException>(() => p.Validate(world));
            p = Profile(); p.Trains[0].Operation.DockToleranceM = .1; Assert.Throws<ArgumentException>(() => p.Validate(world));
        }
        [Test] public void ActiveIncidentIdentityCannotDisappearFromRecoveredWorld()
        {
            using var simulation = new FoundationWorldSimulation(world, views, Profile(), Seed());
            var shift = new AuthoritativeShift(simulation.InitialState, new Sink(), (_, __) => true, simulation.CanOperate);
            Assert.That(simulation.TryAdvance(shift, Array.Empty<ServerMovementInput>(), out var report), Is.True, report.Failure);
            var saved = shift.ExportCheckpoint(); var missing = saved.Copy(); missing.Entities = missing.Entities.Where(e => e.Kind != EntityKind.Incident).ToArray();
            Assert.Throws<InvalidDataException>(() => simulation.Restore(missing)); simulation.Restore(saved);
        }
        [Test] public void PhysicalProjectionHidesUnseenFramesAndPrivateIncidentSchedule()
        {
            using var simulation = new FoundationWorldSimulation(world, views, Profile(), Seed());
            var shift = new AuthoritativeShift(simulation.InitialState, new Sink(), (_, __) => true, simulation.CanOperate);
            Assert.That(simulation.TryAdvance(shift, Array.Empty<ServerMovementInput>(), out var report), Is.True, report.Failure);
            var state = shift.ReadSimulation(); var actor = state.Participants[0];
            var observed = simulation.Project(state, actor, _ => false);
            Assert.That(observed.Frames.Select(f => f.FrameId), Is.EquivalentTo(new[] { actor.FrameId }));
            Assert.That(observed.Bodies, Is.Empty); Assert.That(observed.Regions, Is.Empty); Assert.That(observed.Trains, Is.Empty);
            var text = JsonUtility.ToJson(observed);
            foreach (var value in new[] { "ChoiceId", "IncidentCheckpoint", "RefreshTick", "RandomState", "DefinitionHash" }) Assert.That(text.Contains(value), Is.False);
        }
        [Test] public void ObservedSmokeAndServerOpticalDepthUseTheSameConservedGasState()
        {
            using var simulation = new FoundationWorldSimulation(world, views, Profile(), Seed());
            var shift = new AuthoritativeShift(simulation.InitialState,new Sink(),(_,__)=>true,simulation.CanOperate);
            for (var i=0;i<3;i++) Assert.That(simulation.TryAdvance(shift,Array.Empty<ServerMovementInput>(),out var report),Is.True,report.Failure);
            var state=shift.ReadSimulation(); var projected=simulation.Project(state,state.Participants[0],_=>true);
            var cell=projected.Smoke.Select(c=>c.Cell).First(c=>c.FloorRiseM==0 && c.UpperExtinctionPerM>0);
            var y=(float)(cell.CenterY+(cell.InterfaceHeightAboveMinFloorM+cell.BoundingHeightM)/2);
            var a=new Point3((float)(cell.CenterX-cell.WidthM/4),y,(float)cell.CenterZ);
            var b=new Point3((float)(cell.CenterX+cell.WidthM/4),y,(float)cell.CenterZ);
            Assert.That(simulation.OpticalDepth(a,b),Is.EqualTo(cell.UpperExtinctionPerM*System.Math.Sqrt(a.DistanceSquared(b))).Within(.0001));
        }
        [Test] public void AcceptedDoorRequestSurvivesPreTickRecoveryWithoutTurningOffCarServices()
        {
            var seed=Seed(); var region=world.Region("rolling_stock_mainline"); var pose=world.Pose(region.Id,region.Hub);
            seed.Participants[0].RegionId=pose.RegionId; seed.Participants[0].FrameId=pose.FrameId; seed.Participants[0].Position=pose.Position; seed.Participants[0].LocalPosition=pose.LocalPosition;
            using var simulation=new FoundationWorldSimulation(world,views,Profile(),seed);
            var shift=new AuthoritativeShift(simulation.InitialState,new Sink(),(_,__)=>true,simulation.CanOperate); shift.SetInputEnabled("instructor",true);
            var state=shift.ExportCheckpoint(); var target=state.Entities.Single(e=>e.EntityId=="equipment.rolling_stock_mainline");
            Assert.That(shift.Submit("instructor",new WorldCommand { WorldId=state.WorldId,ShiftId=state.ShiftId,ParticipantId="instructor",TeamId="command",
                CommandId="close-request",Kind=CommandKind.Operate,TargetId=target.EntityId,ExpectedRevision=target.Revision }).Code,Is.EqualTo(CommandCode.Accepted));
            state=shift.ExportCheckpoint(); simulation.Restore(state);
            Assert.That(simulation.TryAdvance(shift,Array.Empty<ServerMovementInput>(),out var report),Is.True,report.Failure);
            var current=shift.ReadSimulation(); var view=simulation.Project(current,current.Participants[0],_=>true);
            Assert.That(view.Regions.Single(r=>r.RegionId==region.Id).LightingOn,Is.True);
            Assert.That(view.Trains.Single(t=>t.EntityId=="train.mainline").DoorOpen,Is.False);
        }
        [Test] public void VisibleBodiesAlwaysCarryTheirReferencedFrame()
        {
            var seed=Seed(); var platform=world.Region("rail_platforms_mainline"); var car=world.Region("rolling_stock_mainline");
            var first=world.Pose(platform.Id,platform.Hub); var second=world.Pose(car.Id,car.Hub);
            seed.Participants[0].RegionId=first.RegionId; seed.Participants[0].FrameId=first.FrameId; seed.Participants[0].Position=first.Position; seed.Participants[0].LocalPosition=first.LocalPosition;
            seed.Participants=seed.Participants.Concat(new[]{new ParticipantState {ParticipantId="other",TeamId="team-a",RoleId="role-01",RegionId=second.RegionId,
                FrameId=second.FrameId,Position=second.Position,LocalPosition=second.LocalPosition}}).ToArray();
            using var simulation=new FoundationWorldSimulation(world,views,Profile(),seed); var state=simulation.InitialState;
            var shift=new AuthoritativeShift(state,new Sink(),(_,__)=>true); var current=shift.ReadSimulation(); var other=current.Participants.Single(p=>p.ParticipantId=="other");
            var sample=other.Position; sample.Y+=1;
            var projection=simulation.Project(current,current.Participants.Single(p=>p.ParticipantId=="instructor"),p=>p.DistanceSquared(sample)<1e-8);
            Assert.That(projection.Bodies.Select(b=>b.ParticipantId),Does.Contain("other"));
            Assert.That(projection.Frames.Select(f=>f.FrameId),Does.Contain(other.FrameId));
        }
        [Test] public void PhysicalTrainFrameAndClockMismatchIsRejectedBeforeMutation()
        {
            using var simulation = new FoundationWorldSimulation(world, views, Profile(), Seed());
            var seed = simulation.InitialState; var bad = seed.Copy();
            var physical = PhysicalCheckpoint.Decode(seed.SimulationCheckpoint, seed.SimulationDefinitionHash);
            physical.Trains[0].Motion.ReferenceDistanceM += .01;
            bad.SimulationCheckpoint = PhysicalCheckpoint.Encode(physical);
            Assert.Throws<InvalidDataException>(() => simulation.Restore(bad));
            physical = PhysicalCheckpoint.Decode(seed.SimulationCheckpoint, seed.SimulationDefinitionHash); physical.Trains[0].ElapsedSeconds = 1;
            bad.SimulationCheckpoint = PhysicalCheckpoint.Encode(physical);
            Assert.Throws<InvalidDataException>(() => simulation.Restore(bad));
            simulation.Restore(seed); Assert.That(simulation.Tick, Is.EqualTo(0));
        }
    }
}
