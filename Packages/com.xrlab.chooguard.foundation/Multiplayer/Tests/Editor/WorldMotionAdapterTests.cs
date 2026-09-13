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
    public sealed class WorldMotionAdapterTests
    {
        private string folder;
        private SceneSetup[] previous;
        private ConnectedWorldDefinition world;
        private ConnectedWorldGeometry geometry;
        [OneTimeSetUp] public void Build()
        {
            previous = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) Assert.Ignore("Preserve unsaved scenes.");
            folder = "Assets/CHOOguardGenerated/WorldMotionTest_" + Guid.NewGuid().ToString("N");
            var paths = ConnectedWorldSceneBuilder.Build(folder);
            foreach (var path in paths.Skip(1)) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath)); world.Validate();
            Physics.SyncTransforms();
            geometry = ConnectedWorldGeometry.Capture(world, UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None));
        }
        [OneTimeTearDown] public void Clear()
        {
            geometry?.Dispose();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (previous != null && previous.Any(s => s.isLoaded && s.isActive) && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            if (folder != null) AssetDatabase.DeleteAsset(folder);
        }
        private WorldBodySpawn Spawn(string id, string region, Point3 point, double radius = WorldBodyPresentation.NpcRadiusM) =>
            new WorldBodySpawn { BodyId = id, Pose = world.Pose(region, point), RadiusM = radius, HeightM = 1.8, PreferredSpeedMS = 1.5 };
        private ConnectedWorldMotionAdapter Adapter(params WorldBodySpawn[] bodies) => new ConnectedWorldMotionAdapter(geometry, bodies, 31);
        [Test] public void NavigationStringPrefixesAreBoundedBeforeReadingPayload()
        {
            using var adapter = Adapter(Spawn("walker", world.StartRegionId, world.Region(world.StartRegionId).Hub));
            var before = adapter.ExportCheckpoint(); var snapshot = adapter.Capture();
            foreach (var bytes in new[] { new byte[] { 128,128,64 }, new byte[] { 255 }, new byte[] { 1,255 } })
            {
                snapshot.PathCheckpoint = Convert.ToBase64String(bytes);
                Assert.That(adapter.TryRestore(snapshot, out _), Is.False);
                Assert.That(adapter.ExportCheckpoint(), Is.EqualTo(before));
            }
        }
        [Test] public void DetachedTrainCanContinueWhileStationBodyEntersAndLeavesTheClosedBridge()
        {
            var portal = world.Portals.Single(p => p.Id == "rolling_stock_mainline--rail_platforms_mainline");
            var station = world.Region("rail_platforms_mainline");
            using var adapter = Adapter(Spawn("walker", station.Id, station.Hub, .3));
            var frames = world.Frames.Select(f => f.Copy()).ToArray(); frames.Single(f => f.FrameId == "train-mainline").Origin.X += 20;
            Assert.That(adapter.TryApplyFrames(frames, new[] { portal.Id }, out var failure), Is.True, failure);
            var goal = world.Pose(station.Id, new Point3(portal.ToPoint.X, portal.ToPoint.Y, portal.ToPoint.Z + 1), portal.Id);
            for (var i = 0; i < 60; i++) Assert.That(adapter.TryAdvance(.1, new[] { new WorldMotionCommand { BodyId = "walker", Goal = goal } }, frames, new[] { portal.Id }, out var r), Is.True, r.Failure);
            Assert.That(adapter.BoardingOccupied(portal.Id), Is.True);
            frames.Single(f => f.FrameId == "train-mainline").Origin.X += 1;
            var previous = adapter.Pose("walker").Position;
            Assert.That(adapter.TryAdvance(.1, new[] { new WorldMotionCommand { BodyId = "walker", DesiredVelocity = new Point3(0, 0, -1) } }, frames, new[] { portal.Id }, out var moved), Is.True, moved.Failure);
            Assert.That(adapter.Pose("walker").Position.Z, Is.LessThan(previous.Z));
        }
        [Test]
        public void DescriptorSourcesAndAvatarEnvelopeAgreeWithActualGeometry()
        {
            Assert.That(geometry.Supports.Count, Is.EqualTo(25));
            Assert.That(geometry.Colliders.Count, Is.GreaterThan(100));
            Assert.That(geometry.ValidateSourceGeometry(out var failure), Is.True, failure);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CHOOguardArt/Blender/Evacuee.fbx");
            Assert.That(model, Is.Not.Null);
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            foreach (var v in mf.sharedMesh.vertices)
            {
                var p = model.transform.worldToLocalMatrix.MultiplyPoint3x4(mf.transform.localToWorldMatrix.MultiplyPoint3x4(v));
                Assert.That(Math.Sqrt(p.x * p.x + p.z * p.z), Is.LessThanOrEqualTo(WorldBodyPresentation.NpcRadiusM));
                Assert.That(p.y, Is.InRange(-.001f, (float)WorldBodyPresentation.NpcHeightM));
            }
        }
        private static IEnumerable<TestCaseData> DirectedCases()
        {
            var profile = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));
            foreach (var radius in new[] { .3, WorldBodyPresentation.NpcRadiusM })
            foreach (var portal in profile.Portals) foreach (var reverse in new[] { false, true })
                yield return new TestCaseData(portal.Id, reverse, radius);
        }
        [TestCaseSource(nameof(DirectedCases))]
        public void BothProfilesTraverseAllThirteenRegionsAndAllTwentyFourPortalDirections(string portalId, bool reverse, double radius)
        {
            var portal = world.Portals.Single(p => p.Id == portalId);
                var start = reverse ? portal.To : portal.From; var end = reverse ? portal.From : portal.To;
                using var adapter = Adapter(Spawn("walker", start, world.Region(start).Hub, radius));
                Assert.That(adapter.TryPlanPath("walker", world.Pose(end, world.Region(end).Hub), out var path), Is.True, portal.Id);
                Assert.That(path.Length, Is.GreaterThanOrEqualTo(2));
                var command = new WorldMotionCommand { BodyId = "walker", Goal = world.Pose(end, world.Region(end).Hub) };
                var steps = 0; var stalled = 0; var clock = System.Diagnostics.Stopwatch.StartNew(); var lastPoint = adapter.Pose("walker").Position;
                while (adapter.Pose("walker").Position.DistanceSquared(world.Region(end).Hub) > .08 * .08 && ++steps < 1600)
                {
                    Assert.That(adapter.TryAdvance(.1, new[] { command }, out var report), Is.True, portal.Id + " step " + steps + " " + report.Failure);
                    var point = adapter.Pose("walker").Position;
                    stalled = point.DistanceSquared(lastPoint) < 1e-10 ? stalled + 1 : 0; lastPoint = point;
                    Assert.That(stalled, Is.LessThan(20), portal.Id + " stalled " + JsonUtility.ToJson(adapter.Pose("walker")));
                    Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(45), portal.Id + " bounded fixture timeout at step " + steps + " " + JsonUtility.ToJson(adapter.Pose("walker")));
                }
                Debug.Log("World motion traversal " + radius + " " + start + " -> " + end + " steps=" + steps + " seconds=" + clock.Elapsed.TotalSeconds);
                Assert.That(steps, Is.LessThan(1600), portal.Id + " " + JsonUtility.ToJson(adapter.Pose("walker")));
                Assert.That(adapter.Pose("walker").RegionId, Is.EqualTo(end));
                Assert.That(adapter.ValidateBodies(out var failure), Is.True, failure);
        }
        [Test]
        public void FullRosterMixedContactAndSeparatedFloorsRemainCollective()
        {
            var spawns = new List<WorldBodySpawn>();
            foreach (var region in world.Regions)
            {
                var count = Array.IndexOf(world.Regions, region) < 3 ? 10 : 9;
                for (var i = 0; i < count; i++)
                {
                    var radius = spawns.Count < 100 ? WorldBodyPresentation.NpcRadiusM : .3;
                    Assert.That(geometry.TryFindStandingPoint(region.Id, region.Hub, radius, 1.8, spawns.ToArray(), out var pose), Is.True, region.Id);
                    spawns.Add(Spawn("body-" + spawns.Count.ToString("D3"), region.Id, pose.Position, radius));
                }
                if (spawns.Count == 120) break;
            }
            using var adapter = Adapter(spawns.ToArray());
            var commands = spawns.Select(s => new WorldMotionCommand { BodyId = s.BodyId, DesiredVelocity = new Point3(.2f, 0, .05f), Pinned = s.BodyId.EndsWith("0") }).ToArray();
            var samples = new List<double>(); var timer = new System.Diagnostics.Stopwatch();
            for (var step = 0; step < 10; step++)
            { timer.Restart(); Assert.That(adapter.TryAdvance(.05, commands, out var report), Is.True, report.Failure); timer.Stop(); samples.Add(timer.Elapsed.TotalMilliseconds); }
            Debug.Log("World motion120 body step milliseconds (10 synthetic50ms ticks): " + string.Join(",", samples.Select(v => v.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))));
            Assert.That(adapter.Capture().Crowd.Agents, Has.Length.EqualTo(120));
            Assert.That(adapter.Capture().Crowd.Agents.Select(a => a.RegionId).Distinct().Count(), Is.EqualTo(13));
            Assert.That(adapter.Capture().Crowd.Agents.Select(a => a.ContactSpaceId).Distinct(), Is.EquivalentTo(new[] { "world" }));
            Assert.That(adapter.ValidateBodies(out var failure), Is.True, failure);
        }
        [Test] public void AuthoredWorldWallPruningMatchesTheFullReferenceModelExactly()
        {
            var spawns=new List<WorldBodySpawn>();
            for (var i=0;i<120;i++)
            {
                var region=world.Regions[i%world.Regions.Length]; var radius=i<100 ? WorldBodyPresentation.NpcRadiusM : .3;
                Assert.That(geometry.TryFindStandingPoint(region.Id,region.Hub,radius,1.8,spawns.ToArray(),out var pose),Is.True);
                spawns.Add(Spawn("body-"+i.ToString("D3"),region.Id,pose.Position,radius));
            }
            using var reference=Adapter(spawns.ToArray()); reference.CrowdOptimization=CrowdMotionModel.WallOptimization.None;
            using var optimized=Adapter(spawns.ToArray());
            var commands=spawns.Select(s=>new WorldMotionCommand {BodyId=s.BodyId,DesiredVelocity=new Point3(.2f,0,.05f),Pinned=s.BodyId.EndsWith("0")}).ToArray();
            for (var i=0;i<3;i++)
            {
                Assert.That(reference.TryAdvance(.05,commands,out var full),Is.True,full.Failure);
                Assert.That(optimized.TryAdvance(.05,commands,out var fast),Is.True,fast.Failure);
                Assert.That(optimized.ExportCheckpoint(),Is.EqualTo(reference.ExportCheckpoint()));
                Assert.That(fast.MinimumSweptGapM,Is.EqualTo(full.MinimumSweptGapM));
            }
        }
        [TestCase(ConnectedWorldMotionAdapter.ExecutionOptimization.ComposedClock)]
        [TestCase(ConnectedWorldMotionAdapter.ExecutionOptimization.PreparedClearance)]
        [TestCase(ConnectedWorldMotionAdapter.ExecutionOptimization.All)]
        public void ExecutionOptimizationsKeepExact120BodySnapshotsMinimumGapsAndFailureAtomicity(ConnectedWorldMotionAdapter.ExecutionOptimization mode)
        {
            var spawns=new List<WorldBodySpawn>();
            for(var i=0;i<120;i++)
            {
                var region=world.Regions[i%world.Regions.Length];var radius=i<100?WorldBodyPresentation.NpcRadiusM:.3;
                Assert.That(geometry.TryFindStandingPoint(region.Id,region.Hub,radius,1.8,spawns.ToArray(),out var pose),Is.True);
                spawns.Add(Spawn("execution-"+i.ToString("D3"),region.Id,pose.Position,radius));
            }
            using var reference=Adapter(spawns.ToArray());reference.Optimization=ConnectedWorldMotionAdapter.ExecutionOptimization.None;
            using var optimized=Adapter(spawns.ToArray());optimized.Optimization=mode;
            var commands=spawns.Select(s=>new WorldMotionCommand {BodyId=s.BodyId,DesiredVelocity=new Point3(.2f,0,.05f),Pinned=s.BodyId.EndsWith("0")}).ToArray();
            for(var i=0;i<5;i++)
            {
                Assert.That(reference.TryAdvance(.05,commands,out var full),Is.True,full.Failure);
                Assert.That(optimized.TryAdvance(.05,commands,out var fast),Is.True,fast.Failure);
                Assert.That(optimized.ExportCheckpoint(),Is.EqualTo(reference.ExportCheckpoint()));
                Assert.That(BitConverter.DoubleToInt64Bits(fast.MinimumSweptGapM),Is.EqualTo(BitConverter.DoubleToInt64Bits(full.MinimumSweptGapM)));
            }
            var checkpoint=optimized.ExportCheckpoint();var invalid=commands.Concat(new[]{commands[0]}).ToArray();
            Assert.That(optimized.TryAdvance(.05,invalid,out _),Is.False);
            Assert.That(optimized.ExportCheckpoint(),Is.EqualTo(checkpoint));
            Assert.That(optimized.TryRestoreCheckpoint(checkpoint,out var failure),Is.True,failure);
            Assert.That(optimized.ExportCheckpoint(),Is.EqualTo(reference.ExportCheckpoint()));
        }

        [TestCase(ConnectedWorldMotionAdapter.ExecutionOptimization.ComposedClock)]
        [TestCase(ConnectedWorldMotionAdapter.ExecutionOptimization.PreparedClearance)]
        [TestCase(ConnectedWorldMotionAdapter.ExecutionOptimization.All)]
        public void ExecutionOptimizationsKeepMovingCarExactAndRejectedFrameAtomic(ConnectedWorldMotionAdapter.ExecutionOptimization mode)
        {
            var region=world.Region("rolling_stock_mainline");var spawn=Spawn("rider",region.Id,region.Hub,.3);
            using var reference=Adapter(spawn);reference.Optimization=ConnectedWorldMotionAdapter.ExecutionOptimization.None;
            using var optimized=Adapter(spawn);optimized.Optimization=mode;
            var frames=world.Frames.Select(f=>f.Copy()).ToArray();var closed=new[]{"rolling_stock_mainline--rail_platforms_mainline"};
            var commands=new[]{new WorldMotionCommand {BodyId="rider",Pinned=true}};
            for(var i=0;i<5;i++)
            {
                frames.Single(f=>f.FrameId=="train-mainline").Origin.X+=1;
                Assert.That(reference.TryAdvance(.05,commands,frames,closed,out var full),Is.True,full.Failure);
                Assert.That(optimized.TryAdvance(.05,commands,frames,closed,out var fast),Is.True,fast.Failure);
                Assert.That(optimized.ExportCheckpoint(),Is.EqualTo(reference.ExportCheckpoint()));
                Assert.That(BitConverter.DoubleToInt64Bits(fast.MinimumSweptGapM),Is.EqualTo(BitConverter.DoubleToInt64Bits(full.MinimumSweptGapM)));
            }
            var before=optimized.ExportCheckpoint();frames.Single(f=>f.FrameId=="train-mainline").Origin.Y+=1;
            Assert.That(optimized.TryAdvance(.05,commands,frames,closed,out _),Is.False);
            Assert.That(optimized.ExportCheckpoint(),Is.EqualTo(before));
        }
        [Test] public void ExecutionOptimizationsRollBackAfterCrowdAdvanceFailsTheLaterPhysXCheck()
        {
            var region=world.Region(world.StartRegionId);
            Assert.That(geometry.TryFindStandingPoint(region.Id,region.Hub,.3,1.8,Array.Empty<WorldBodySpawn>(),out var pose),Is.True);
            var spawn=Spawn("walker",region.Id,pose.Position,.3);
            using var reference=Adapter(spawn);reference.Optimization=ConnectedWorldMotionAdapter.ExecutionOptimization.None;
            using var optimized=Adapter(spawn);
            var before=optimized.ExportCheckpoint();var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                obstacle.name="LatePhysXNegativeFixture";obstacle.transform.position=new Vector3(pose.Position.X+.43f,pose.Position.Y+.9f,pose.Position.Z);
                obstacle.transform.localScale=new Vector3(.1f,1.8f,1);Physics.SyncTransforms();
                var commands=new[]{new WorldMotionCommand {BodyId="walker",DesiredVelocity=new Point3(3,0,0)}};
                Assert.That(reference.TryAdvance(.05,commands,out var full),Is.False);
                Assert.That(optimized.TryAdvance(.05,commands,out var fast),Is.False);
                Assert.That(full.Failure,Does.Contain("Grounded candidate rejected"));Assert.That(fast.Failure,Is.EqualTo(full.Failure));
                Assert.That(optimized.ExportCheckpoint(),Is.EqualTo(before));Assert.That(reference.ExportCheckpoint(),Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(obstacle);Physics.SyncTransforms(); }
            Assert.That(optimized.TryAdvance(.05,new[]{new WorldMotionCommand {BodyId="walker",DesiredVelocity=new Point3(.1f,0,0)}},out var next),Is.True,next.Failure);
        }
        [Test] public void MovingAnUnrelatedClosedTrainPreservesTheStationNavigationPath()
        {
            var portal="rolling_stock_mainline--rail_platforms_mainline";
            using var adapter=Adapter(Spawn("walker",world.StartRegionId,world.Region(world.StartRegionId).Hub,.3));
            var frames=world.Frames.Select(f=>f.Copy()).ToArray(); frames.Single(f=>f.FrameId=="train-mainline").Origin.X+=10;
            Assert.That(adapter.TryApplyFrames(frames,new[]{portal},out var failure),Is.True,failure);
            var target=world.Region("station_hall_1f"); var commands=new[]{new WorldMotionCommand {BodyId="walker",Goal=world.Pose(target.Id,target.Hub)}};
            Assert.That(adapter.TryAdvance(.05,commands,frames,new[]{portal},out var first),Is.True,first.Failure);
            var path=adapter.Capture().PathCheckpoint;
            frames.Single(f=>f.FrameId=="train-mainline").Origin.X+=1;
            Assert.That(adapter.TryAdvance(.05,commands,frames,new[]{portal},out var next),Is.True,next.Failure);
            Assert.That(adapter.Capture().PathCheckpoint,Is.EqualTo(path));
        }
        [Test]
        public void Mixed120BodiesKeepContactsAcrossActualRampAndBothLandings()
        {
            var spawns = new List<WorldBodySpawn>();
            var portal = world.Portals.Single(p => p.From == "rail_platforms_mainline" && p.To == "station_concourse_2f");
            for (var lane = 0; lane < 2; lane++) for (var i = 0; i < 10; i++)
            {
                var x = -24.8f + i * 1.45f; var y = x <= -24 ? 2 : x >= -12 ? 6 : 2 + (x + 24) / 3;
                var region = x < -18 ? portal.From : portal.To;
                var body = Spawn("ramp-" + lane + "-" + i, region, new Point3(x, y, lane == 0 ? -.55f : .55f), lane == 0 ? WorldBodyPresentation.NpcRadiusM : .3);
                body.Pose.PortalId = x > -24 && x < -12 ? portal.Id : ""; spawns.Add(body);
            }
            var hall = world.Region("station_hall_1f");
            for (var i = 0; i < 100; i++)
            {
                var radius = i < 90 ? WorldBodyPresentation.NpcRadiusM : .3;
                Assert.That(geometry.TryFindStandingPoint(hall.Id, hall.Hub, radius, 1.8, spawns.ToArray(), out var pose), Is.True);
                var b = Spawn("held-" + i.ToString("D3"), hall.Id, pose.Position, radius); b.Pinned = true; spawns.Add(b);
            }
            using var adapter = Adapter(spawns.ToArray());
            var commands = spawns.Select(b => new WorldMotionCommand { BodyId = b.BodyId, Pinned = b.Pinned, DesiredVelocity = b.Pinned ? new Point3() : new Point3(1.2f, 0, 0) }).ToArray();
            var transitions = 0;
            for (var i = 0; i < 20; i++) { Assert.That(adapter.TryAdvance(.05, commands, out var report), Is.True, report.Failure); transitions += report.SupportTransitions; }
            Assert.That(transitions, Is.GreaterThan(0)); Assert.That(adapter.ValidateBodies(out var failure), Is.True, failure);
            Assert.That(adapter.Capture().Crowd.Agents.Select(a => a.ContactSpaceId).Distinct(), Is.EquivalentTo(new[] { "world" }));
        }
        [TestCase(.3)] [TestCase(WorldBodyPresentation.NpcRadiusM)]
        public void ClosedTreeEdgeWaitsAndActualPropGetsAPathAroundIt(double radius)
        {
            const string region = "station_concourse_2f";
            var from = new Point3(-8, 6, -9); var goal = world.Pose(region, new Point3(-8, 6, -5));
            using var adapter = Adapter(Spawn("npc", region, from, radius));
            Assert.That(GroundedWorldMotor.TryGroundedStep(new Vector3(from.X, from.Y, from.Z), Vector3.forward * 4, out _, (float)radius, 1.8f), Is.False, "The actual bench must obstruct the direct segment.");
            Assert.That(adapter.TryPlanPath("npc", goal, out var path), Is.True);
            Assert.That(path, Has.Length.GreaterThanOrEqualTo(3));
            var ticks = 0;
            while (adapter.Pose("npc").Position.DistanceSquared(goal.Position) > .08 * .08 && ++ticks < 200)
                Assert.That(adapter.TryAdvance(.1, new[] { new WorldMotionCommand { BodyId = "npc", Goal = goal } }, out var report), Is.True, report.Failure);
            Assert.That(ticks, Is.LessThan(200), "Follow the computed corners around the actual prop.");
            var portal = world.Portals.Single(p => p.From == "rail_platforms_mainline");
            Assert.That(adapter.TryApplyFrames(world.Frames, new[] { portal.Id }, out var failure), Is.True, failure);
            Assert.That(adapter.TryPlanPath("npc", world.Pose(portal.From, world.Region(portal.From).Hub), out _), Is.False);
        }
        [Test]
        public void FrameTransactionCarriesOfflineBodiesKeepsBridgesAndRestoresExactly()
        {
            const string car = "rolling_stock_metro";
            using var adapter = Adapter(Spawn("offline", car, world.Region(car).Hub), Spawn("station", "metro_platforms", world.Region("metro_platforms").Hub));
            var portal = world.Portals.Single(p => p.From == car);
            var closed = new[] { portal.Id }; var initial = adapter.Capture(); var local = adapter.Pose("offline").LocalPosition;
            var bridge = geometry.Supports.Single(s => s.PortalId == portal.Id).Source.transform.position;
            var frames = world.Frames.Select(f => f.Copy()).ToArray(); frames.Single(f => f.FrameId == "train-metro").Origin.X -= 60;
            Assert.That(adapter.TryAdvance(.05, new[] { new WorldMotionCommand { BodyId = "offline", Pinned = true } }, frames, closed, out var report), Is.True, report.Failure);
            Assert.That(adapter.Pose("offline").LocalPosition.DistanceSquared(local), Is.LessThan(1e-10));
            Assert.That(adapter.Pose("offline").Position.X, Is.EqualTo(240).Within(.0001));
            Assert.That(geometry.Supports.Single(s => s.PortalId == portal.Id).Source.transform.position, Is.EqualTo(bridge));
            Assert.That(adapter.Pose("station").Position.X, Is.EqualTo(300));
            var captured = adapter.Capture();
            Assert.That(adapter.TryRestore(initial, out var failure), Is.True, failure);
            Assert.That(adapter.TryRestore(captured, out failure), Is.True, failure);
            Assert.That(JsonUtility.ToJson(adapter.Capture()), Is.EqualTo(JsonUtility.ToJson(captured)));
            frames.Single(f => f.FrameId == "train-metro").Origin.X += 60;
            Assert.That(adapter.TryApplyFrames(frames, Array.Empty<string>(), out failure), Is.True, failure);
            Assert.That(adapter.Capture().Crowd.Agents.All(a => a.ContactSpaceId == "world"), Is.True);
        }
        [Test]
        public void StraddlingOfflineNpcRejectsClosureAndDepartureWithoutPartialCommit()
        {
            const string station = "rail_platforms_mainline";
            using var adapter = Adapter(Spawn("offline-npc", station, new Point3(-50, 2, 12.6f)));
            var portal = world.Portals.Single(p => p.From == "rolling_stock_mainline");
            Assert.That(adapter.BoardingOccupied(portal.Id), Is.True);
            var initial = JsonUtility.ToJson(adapter.Capture());
            var frames = world.Frames.Select(f => f.Copy()).ToArray(); frames.Single(f => f.FrameId == "train-mainline").Origin.X += 10;
            Assert.That(adapter.TryAdvance(.05, new[] { new WorldMotionCommand { BodyId = "offline-npc", Pinned = true } }, frames, new[] { portal.Id }, out _), Is.False);
            Assert.That(JsonUtility.ToJson(adapter.Capture()), Is.EqualTo(initial));
        }
        [Test]
        public void RestoreRejectsChangedStaticTransformLabelsAndSameEpochPin()
        {
            const string region = "station_concourse_2f";
            using var adapter = Adapter(Spawn("a", region, world.Region(region).Hub));
            var before = JsonUtility.ToJson(adapter.Capture());
            var invalid = adapter.Capture(); invalid.Poses[0].RegionId = "unknown-region";
            Assert.That(adapter.TryRestore(invalid, out _), Is.False);
            invalid = adapter.Capture(); invalid.Poses[0].PortalId = "unknown-portal";
            Assert.That(adapter.TryRestore(invalid, out _), Is.False);
            invalid = adapter.Capture(); invalid.Crowd.Agents[0].FrameId = "train-metro";
            Assert.That(adapter.TryRestore(invalid, out _), Is.False);
            invalid = adapter.Capture(); invalid.Crowd.Agents[0].Pinned = true;
            Assert.That(adapter.TryRestore(invalid, out _), Is.False);
            var wall = geometry.Colliders.First(c => !c.Walkable && c.FrameId == "world" && c.PortalId == "").Source.transform;
            var at = wall.localPosition;
            try
            {
                wall.localPosition += Vector3.up;
                Assert.That(geometry.ValidateSourceGeometry(out _), Is.False);
                Assert.That(adapter.TryRestore(adapter.Capture(), out _), Is.False);
            }
            finally { wall.localPosition = at; Physics.SyncTransforms(); }
            Assert.That(JsonUtility.ToJson(adapter.Capture()), Is.EqualTo(before));
        }
        [Test]
        public void SameGoalAndObstaclePathContinueExactlyAfterBinaryPathCheckpointRestore()
        {
            const string region = "station_concourse_2f";
            using var adapter = Adapter(Spawn("a", region, new Point3(-8, 6, -9)));
            Assert.That(adapter.TryAdvance(.05, new[] { new WorldMotionCommand { BodyId = "a", Goal = adapter.Pose("a") } }, out var report), Is.True, report.Failure);
            var commands = new[] { new WorldMotionCommand { BodyId = "a", Goal = world.Pose(region, new Point3(-8, 6, -5)) } };
            for (var i = 0; i < 5; i++) Assert.That(adapter.TryAdvance(.1, commands, out report), Is.True, report.Failure);
            var checkpoint = adapter.Capture();
            for (var i = 0; i < 5; i++) Assert.That(adapter.TryAdvance(.1, commands, out report), Is.True, report.Failure);
            var expected = JsonUtility.ToJson(adapter.Capture());
            Assert.That(adapter.TryRestore(checkpoint, out var failure), Is.True, failure);
            for (var i = 0; i < 5; i++) Assert.That(adapter.TryAdvance(.1, commands, out report), Is.True, report.Failure);
            Assert.That(JsonUtility.ToJson(adapter.Capture()), Is.EqualTo(expected));
        }
        [Test]
        public void ExactOuterBinaryCheckpointRestoresMotionFramesAndNavigationAndRejectsMalformedPayloadAtomically()
        {
            const string region = "station_concourse_2f"; const string car = "rolling_stock_metro";
            using var adapter = Adapter(Spawn("a", region, new Point3(-8, 6, -9)), Spawn("b", car, world.Region(car).Hub));
            var frames = world.Frames.Select(f => f.Copy()).ToArray(); frames.Single(f => f.FrameId == "train-metro").Origin.X -= 30;
            var closed = new[] { world.Portals.Single(p => p.From == car).Id };
            var commands = new[] { new WorldMotionCommand { BodyId = "a", Goal = world.Pose(region, new Point3(-8, 6, -5)) }, new WorldMotionCommand { BodyId = "b", Pinned = true } };
            for (var i = 0; i < 5; i++) Assert.That(adapter.TryAdvance(.1, commands, frames, closed, out var report), Is.True, report.Failure);
            var checkpoint = adapter.ExportCheckpoint(); Assert.That(checkpoint, Does.StartWith("CGWM1:"));
            for (var i = 0; i < 3; i++) Assert.That(adapter.TryAdvance(.1, commands, frames, closed, out _), Is.True);
            var continued = adapter.ExportCheckpoint();
            Assert.That(adapter.TryRestoreCheckpoint(checkpoint, out var failure), Is.True, failure);
            Assert.That(adapter.ExportCheckpoint(), Is.EqualTo(checkpoint));
            for (var i = 0; i < 3; i++) Assert.That(adapter.TryAdvance(.1, commands, frames, closed, out _), Is.True);
            Assert.That(adapter.ExportCheckpoint(), Is.EqualTo(continued));
            var payload = Convert.FromBase64String(continued.Substring(6)); payload = payload.Take(payload.Length - 32).ToArray();
            using var stream = new MemoryStream(payload); using var reader = new BinaryReader(stream);
            string ReadText() { var n = reader.ReadInt32(); return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(n)); }
            reader.ReadInt32(); ReadText(); ReadText(); var frameCountOffset = (int)stream.Position; var count = reader.ReadInt32(); var firstPointOffset = 0;
            for (var i = 0; i < count; i++) { ReadText(); if (i == 0) firstPointOffset = (int)stream.Position; reader.ReadBytes(16); }
            count = reader.ReadInt32(); for (var i = 0; i < count; i++) ReadText();
            count = reader.ReadInt32(); var secondBodyOffset = 0;
            for (var i = 0; i < count; i++) { if (i == 1) secondBodyOffset = (int)stream.Position + 4; ReadText(); ReadText(); ReadText(); ReadText(); reader.ReadBytes(24); }
            var duplicate = payload.ToArray(); duplicate[secondBodyOffset] = (byte)'a';
            var nonfinite = payload.ToArray(); Array.Copy(BitConverter.GetBytes(float.NaN), 0, nonfinite, firstPointOffset, 4);
            var oversized = payload.ToArray(); Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, oversized, frameCountOffset, 4);
            string Envelope(byte[] body) { using var sha = System.Security.Cryptography.SHA256.Create(); return "CGWM1:" + Convert.ToBase64String(body.Concat(sha.ComputeHash(body)).ToArray()); }
            foreach (var malformed in new[] { "CGWM2:" + continued.Substring(6), continued.Substring(0, continued.Length - 8), Envelope(payload.Concat(new byte[] { 0 }).ToArray()), Envelope(duplicate), Envelope(nonfinite), Envelope(oversized), Envelope(payload.Take(payload.Length - 7).ToArray()) })
            { Assert.That(adapter.TryRestoreCheckpoint(malformed, out _), Is.False); Assert.That(adapter.ExportCheckpoint(), Is.EqualTo(continued)); }
        }
        [Test]
        public void InvalidCandidateAndForeignCheckpointCannotPartiallyCommit()
        {
            const string region = "station_concourse_2f";
            using var adapter = Adapter(Spawn("a", region, new Point3(-1, 6, 0)), Spawn("b", region, new Point3(1, 6, 0)));
            var initial = adapter.Capture(); var json = JsonUtility.ToJson(initial);
            Assert.That(adapter.TryAdvance(.05, new[] { new WorldMotionCommand { BodyId = "a", DesiredVelocity = new Point3(1, 0, 0) }, new WorldMotionCommand { BodyId = "b", DesiredVelocity = new Point3(float.NaN, 0, 0) } }, out _), Is.False);
            Assert.That(JsonUtility.ToJson(adapter.Capture()), Is.EqualTo(json));
            var invalid = adapter.Capture(); invalid.GeometrySignature = "foreign";
            Assert.That(adapter.TryRestore(invalid, out _), Is.False);
            invalid = adapter.Capture(); invalid.Crowd.Agents[0].FootElevationM += 1;
            Assert.That(adapter.TryRestore(invalid, out _), Is.False);
            Assert.That(JsonUtility.ToJson(adapter.Capture()), Is.EqualTo(json));
        }
    }
}
