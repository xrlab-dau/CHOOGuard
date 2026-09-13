using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class ConnectedWorldTests
    {
        private ConnectedWorldDefinition Definition() => JsonUtility.FromJson<ConnectedWorldDefinition>(
            File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));

        [Test]
        public void ExactCoverageTreeHasBidirectionalRoutesAndNoInventedDetours()
        {
            var world = Definition(); world.Validate();
            var coverage = JsonUtility.FromJson<Coverage>(File.ReadAllText("foundation/world/facility-coverage.json"));
            CollectionAssert.AreEquivalent(coverage.zones.Select(z => z.id), world.Regions.Select(r => r.Id));
            CollectionAssert.AreEqual(coverage.connections.Select(c => c.from + "--" + c.to), world.Portals.Select(p => p.Id));
            Assert.That(world.Regions, Has.Length.EqualTo(13));
            foreach (var region in world.Regions)
            {
                Assert.That(world.Route(world.StartRegionId, region.Id), Is.Not.Empty, region.Id);
                Assert.That(world.Region(region.Id).SceneName, Does.StartWith("cg_region_"));
            }
            foreach (var portal in world.Portals)
            {
                CollectionAssert.AreEqual(new[] { portal.From, portal.To }, world.Route(portal.From, portal.To));
                CollectionAssert.AreEqual(new[] { portal.To, portal.From }, world.Route(portal.To, portal.From));
                Assert.That(world.Route(portal.From, portal.To, new HashSet<string> { portal.Id }), Is.Empty,
                    "A closed tree edge must not invent a bypass: " + portal.Id);
            }
        }

        [Test]
        public void ProfileRejectsUnconnectedEdgesInvalidFramesAndNonFiniteGeometry()
        {
            var world = Definition(); world.Portals[0].To = "missing";
            Assert.Throws<InvalidDataException>(() => world.Validate());
            world = Definition(); world.Regions[0].FrameId = "missing";
            Assert.Throws<InvalidDataException>(() => world.Validate());
            world = Definition(); world.Regions[0].Center.X = float.NaN;
            Assert.Throws<InvalidDataException>(() => world.Validate());
        }

        [Test]
        public void ClosedPassageAllowsRetreatAndDoesNotTrapAnActorOnItsOwnSide()
        {
            var world = Definition(); var portal = world.Portals[0];
            var from = V(portal.FromPoint); var to = V(portal.ToPoint);
            var pose = world.Pose(portal.From, P(Vector3.Lerp(from, to, .3f)), portal.Id);
            var closed = new HashSet<string> { portal.Id };
            Assert.That(world.TryLocate(pose, P(Vector3.Lerp(from, to, .25f)), .3f, closed, out _), Is.True);
            Assert.That(world.TryLocate(pose, P(Vector3.Lerp(from, to, .55f)), .3f, closed, out _), Is.False);
        }

        [Test]
        public void LegacyJsonRemainsReadableButSchemaTwoRejectsLegacyCommits()
        {
            var legacy = JsonUtility.FromJson<WorldState>("{\"SchemaVersion\":1,\"WorldId\":\"world\",\"ShiftId\":\"shift\",\"Participants\":[{\"ParticipantId\":\"p\",\"TeamId\":\"t\",\"RoleId\":\"r\",\"RegionId\":\"hall\",\"ObservedIds\":[]}],\"Entities\":[],\"Reports\":[],\"Receipts\":[]}");
            var shift = new AuthoritativeShift(legacy, new Sink(), (a, e) => true);
            var commit = new ShiftCommit { WorldSchemaVersion = 0, Receipt = new CommandReceipt {
                WorldId = "world", ShiftId = "shift", ParticipantId = "p", CommandId = "legacy", Fingerprint = new string('a', 64), Sequence = 1 } };
            Assert.DoesNotThrow(() => shift.Replay(commit));
            var spatial = Definition().CreateWorldState();
            var modern = new AuthoritativeShift(spatial, new Sink(), (a, e) => true);
            commit.Receipt.WorldId = spatial.WorldId; commit.Receipt.ShiftId = spatial.ShiftId;
            Assert.Throws<InvalidDataException>(() => modern.Replay(commit));
        }

        [Test]
        public void OccupiedDoorClosureFailsBeforeCommitAndDoesNotOverrideAcceptedDuplicates()
        {
            var world = Definition(); var state = world.CreateWorldState(); var r = world.Region("rolling_stock_mainline");
            var pose = world.Pose(r.Id, world.EquipmentApproach(r.Id));
            state.Participants = new[] { new ParticipantState { ParticipantId = "p", TeamId = "t", RoleId = "r", RegionId = pose.RegionId,
                FrameId = pose.FrameId, LocalPosition = pose.LocalPosition, Position = pose.Position } };
            var canClose = false;
            var shift = new AuthoritativeShift(state, new Sink(), (a, e) => true, e => canClose);
            var command = new WorldCommand { WorldId = state.WorldId, ShiftId = state.ShiftId, ParticipantId = "p", TeamId = "t",
                CommandId = "door-close", TargetId = "equipment." + r.Id, Kind = CommandKind.Operate };
            Assert.That(shift.Submit("p", command).Code, Is.EqualTo(CommandCode.TargetBlocked));
            Assert.That(shift.ExportCheckpoint().Sequence, Is.Zero);
            canClose = true;
            Assert.That(shift.Submit("p", command).Code, Is.EqualTo(CommandCode.Accepted));
            canClose = false;
            Assert.That(shift.Submit("p", command).Code, Is.EqualTo(CommandCode.Accepted));
        }

        [Test]
        public void SpatialCheckpointFailureFreezesNewActionsAndPreservesApprovedDuplicate()
        {
            var world = Definition(); var state = world.CreateWorldState(); var r = world.Region(world.StartRegionId);
            var pose = world.Pose(r.Id, world.EquipmentApproach(r.Id));
            state.Participants = new[] { new ParticipantState { ParticipantId = "p", TeamId = "t", RoleId = "r", IsInstructor = true,
                RegionId = pose.RegionId, FrameId = pose.FrameId, Position = pose.Position, LocalPosition = pose.LocalPosition } };
            var shift = new AuthoritativeShift(state, new Sink(), (a, e) => true);
            var command = new WorldCommand { WorldId = state.WorldId, ShiftId = state.ShiftId, ParticipantId = "p", TeamId = "t",
                CommandId = "before-fault", TargetId = "equipment." + r.Id, Kind = CommandKind.Operate };
            Assert.That(shift.Submit("p", command).Code, Is.EqualTo(CommandCode.Accepted));
            shift.FaultPersistence();
            Assert.That(shift.Paused, Is.True);
            Assert.That(shift.Participant("p").InputEnabled, Is.False);
            Assert.That(shift.Submit("p", command).Code, Is.EqualTo(CommandCode.Accepted));
            command.CommandId = "resume-after-fault"; command.Kind = CommandKind.ResumeShift;
            Assert.That(shift.Submit("p", command).Code, Is.EqualTo(CommandCode.PersistenceUnavailable));
        }

        [Test]
        public void StaticSessionRejectsSameIdChangedFrameGeometryAndReboundEquipment()
        {
            var world = Definition(); var state = world.CreateWorldState();
            Assert.DoesNotThrow(() => ConnectedWorldRuntime.ValidateSessionDefinition(world, state));
            var moved = state.Copy(); moved.Frames.Single(f => f.FrameId == "train-mainline").Origin.X += 10;
            foreach (var entity in moved.Entities.Where(e => e.FrameId == "train-mainline"))
                entity.LocalPosition = moved.Frames.Single(f => f.FrameId == entity.FrameId).ToLocal(entity.Position);
            Assert.Throws<InvalidDataException>(() => ConnectedWorldRuntime.ValidateSessionDefinition(world, moved));
            moved = state.Copy(); moved.Frames.Single(f => f.FrameId == "train-metro").YawDegrees += 90;
            Assert.Throws<InvalidDataException>(() => ConnectedWorldRuntime.ValidateSessionDefinition(world, moved));
            moved = state.Copy(); moved.Entities[0].RegionId = world.StartRegionId;
            Assert.Throws<InvalidDataException>(() => ConnectedWorldRuntime.ValidateSessionDefinition(world, moved));
            var pose = world.Pose(world.StartRegionId, world.Region(world.StartRegionId).Hub);
            state.Participants = new[] { new ParticipantState { ParticipantId = "p", RegionId = pose.RegionId, FrameId = pose.FrameId,
                Position = pose.Position, LocalPosition = new Point3(100, 100, 100) } };
            Assert.Throws<InvalidDataException>(() => ConnectedWorldRuntime.ValidateSessionDefinition(world, state));
        }

        [Test]
        public void PortalProjectionUsesAllControlsFromEitherSideAndOnLateJoinWithoutLeakingEntities()
        {
            var world = Definition(); var state = world.CreateWorldState();
            var portal = world.Portals.Single(p => p.From == "rolling_stock_metro");
            state.Entities.Single(e => e.EntityId == "equipment.rolling_stock_metro").Active = false;
            foreach (var region in new[] { portal.From, portal.To })
            {
                var actor = new ParticipantState { RegionId = region, Position = world.Region(region).Hub };
                var projection = ConnectedWorldRuntime.ProjectPortals(world, state, actor, p => true);
                Assert.That(projection.Single(p => p.PortalId == portal.Id).Open, Is.False,
                    "A closed train control must be visible to a newly joined platform observer even while its own control is open.");
                Assert.That(ConnectedWorldRuntime.ProjectPortals(world, state, actor, p => false), Is.Empty);
                Assert.That(JsonUtility.ToJson(new FieldView { Portals = projection }), Does.Not.Contain("equipment."));
            }
            var far = new ParticipantState { RegionId = world.StartRegionId, Position = world.Region(world.StartRegionId).Hub };
            Assert.That(ConnectedWorldRuntime.ProjectPortals(world, state, far, p => true).Any(p => p.PortalId == portal.Id), Is.False);
        }

        [Test]
        public void DisconnectedPoseReservesDoorwaySoReconnectCannotBeTrappedInsideAClosure()
        {
            var world = Definition(); var portal = world.Portals.First(p => p.LinkedDoorEntityIds.Length > 0);
            var target = world.CreateWorldState().Entities.Single(e => e.EntityId == portal.LinkedDoorEntityIds[0]);
            var center = Vector3.Lerp(V(portal.FromPoint), V(portal.ToPoint), .49f);
            var offline = new ParticipantState { ParticipantId = "offline", InputEnabled = false, Position = P(center), RegionId = portal.From };
            Assert.That(ConnectedWorldRuntime.CanOperateDefinition(world, target, new[] { offline }), Is.False,
                "Disconnected saved poses retain the same closure reservation until the participant reconnects and moves away.");
            offline.InputEnabled = true;
            Assert.That(ConnectedWorldRuntime.CanOperateDefinition(world, target, new[] { offline }), Is.False);
            offline.Position = world.Region(portal.From).Hub;
            Assert.That(ConnectedWorldRuntime.CanOperateDefinition(world, target, new[] { offline }), Is.True);
        }

        [Test]
        public void StaticBoardingFramesRoundTripAndSchemaTwoSurvivesRealDiskRecovery()
        {
            var world = Definition(); world.Validate();
            foreach (var frame in world.Frames)
            {
                var local = new Point3(1.25f, 1.1f, -3);
                Assert.That(frame.ToLocal(frame.ToWorld(local)).DistanceSquared(local), Is.LessThan(1e-8));
            }
            var state = world.CreateWorldState();
            var vehicle = world.Region("rolling_stock_mainline");
            var pose = world.Pose(vehicle.Id, vehicle.Hub);
            state.Participants = new[] { new ParticipantState { ParticipantId = "p", TeamId = "t", RoleId = "role-01",
                RegionId = pose.RegionId, FrameId = pose.FrameId, LocalPosition = pose.LocalPosition, Position = pose.Position } };
            var directory = Path.Combine(Path.GetTempPath(), "cg-world-v2-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var journal = new ShiftJournal(directory))
                {
                    var shift = journal.Open(state, (a, e) => true);
                    var equipment = world.RegionEquipment(vehicle.Id);
                    var atEquipment = world.Pose(vehicle.Id, equipment.Position);
                    shift.SetServerPose("p", atEquipment);
                    Assert.That(shift.Submit("p", new WorldCommand { WorldId = state.WorldId, ShiftId = state.ShiftId,
                        ParticipantId = "p", TeamId = "t", CommandId = "boarding-switch", TargetId = equipment.EntityId,
                        Kind = CommandKind.Operate }).Code, Is.EqualTo(CommandCode.Accepted));
                    journal.Checkpoint(shift.ExportCheckpoint());
                }
                using (var journal = new ShiftJournal(directory))
                {
                    var recovered = journal.Open(state, (a, e) => true);
                    Assert.That(recovered.Paused, Is.True);
                    Assert.That(recovered.Participant("p").FrameId, Is.EqualTo(vehicle.FrameId));
                    Assert.That(recovered.ExportCheckpoint().SchemaVersion, Is.EqualTo(2));
                    Assert.That(recovered.ExportCheckpoint().Entities.Single(e => e.EntityId == world.RegionEquipment(vehicle.Id).EntityId).Revision, Is.EqualTo(1));
                }
                var invalid = state.Copy(); invalid.Participants[0].LocalPosition.X += 10;
                Assert.Throws<InvalidDataException>(() => new AuthoritativeShift(invalid, new Sink(), (a, e) => true));
                var wrongProfile = state.Copy(); wrongProfile.SpatialProfileId = "changed-profile";
                using (var journal = new ShiftJournal(directory))
                    Assert.Throws<InvalidDataException>(() => journal.Open(wrongProfile, (a, e) => true));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void RegionBoundaryAndLateJoinDoNotLeakUnobservedIncidentState()
        {
            var world = Definition(); var initial = world.CreateWorldState();
            var first = world.Region("station_concourse_2f");
            var second = world.Region("rail_terminal_public");
            var pose = world.Pose(first.Id, first.Hub);
            initial.Participants = new[] { new ParticipantState { ParticipantId = "p", TeamId = "t", RoleId = "role-01",
                Position = pose.Position, RegionId = pose.RegionId, FrameId = pose.FrameId, LocalPosition = pose.LocalPosition } };
            var incident = new EntityState { EntityId = "unseen-in-other-region", Kind = EntityKind.Incident,
                RegionId = second.Id, FrameId = second.FrameId, Position = second.Hub,
                LocalPosition = world.Frame(second.FrameId).ToLocal(second.Hub) };
            initial.Entities = initial.Entities.Concat(new[] { incident }).ToArray();
            var shift = new AuthoritativeShift(initial, new Sink(), (a, e) => true);
            Assert.That(JsonUtility.ToJson(shift.Observe("p")), Does.Not.Contain(incident.EntityId));
            shift.SetServerPose("p", world.Pose(second.Id, second.Hub));
            Assert.That(shift.Observe("p").Entities.All(e => e.RegionId == second.Id), Is.True);
            Assert.That(JsonUtility.ToJson(shift.Observe("p")), Does.Not.Contain(incident.EntityId));
            Assert.That(shift.DiscoverNearby("p", p => true), Is.EqualTo(1));
            var restored = AuthoritativeShift.Restore(shift.ExportCheckpoint(), new Sink(), (a, e) => true);
            Assert.That(restored.Observe("p").Entities.Any(e => e.EntityId == incident.EntityId), Is.True);
        }

        [Test]
        public void SerializedScenesSupportEveryEdgeBothWaysEveryRegionActionAndClosedSeams()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                Assert.Ignore("Save scenes before the isolated builder fixture.");
            var path = "Assets/CHOOguardGenerated/ConnectedWorldTest_" + Guid.NewGuid().ToString("N");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var legacy = File.ReadAllBytes("Assets/CHOOguardGenerated/FoundationDemo/FoundationDemo.unity");
            var slice = File.ReadAllBytes(MultiplayerSceneBuilder.ScenePath);
            try
            {
                var paths = ConnectedWorldSceneBuilder.Build(path);
                Assert.That(paths, Has.Length.EqualTo(14));
                EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Single);
                foreach (var scenePath in paths.Skip(1)) EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                Physics.SyncTransforms();
                var definition = Definition();
                var all = new HashSet<string>(definition.Regions.Select(r => r.Id));
                var anchors = UnityEngine.Object.FindObjectsByType<NetworkEntityAnchor>(FindObjectsSortMode.None);
                Assert.That(anchors, Has.Length.EqualTo(13));
                Assert.That(UnityEngine.Object.FindObjectsByType<NetworkFieldRuntime>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
                foreach (var region in definition.Regions)
                {
                    Assert.That(anchors.Count(a => a.RegionId == region.Id), Is.EqualTo(1));
                    var owner = UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None).Single(v => v.RegionId == region.Id);
                    var ceiling = owner.transform.Find("Ceiling");
                    Assert.That(ceiling != null, Is.EqualTo(region.Ceiling));
                    if (ceiling != null)
                        Assert.That(ceiling.GetComponent<Collider>().bounds.min.y, Is.EqualTo(region.Center.Y + region.Height).Within(.0001));
                    var pose = definition.Pose(region.Id, region.Hub);
                    Walk(definition, ref pose, definition.EquipmentApproach(region.Id), all);
                    Assert.That(pose.RegionId, Is.EqualTo(region.Id));
                }
                foreach (var portal in definition.Portals)
                {
                    var passage = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t => t.name == "Portal_" + portal.Id);
                    var passageOwner = passage.GetComponentInParent<ConnectedRegionView>();
                    if (portal.StaticBoarding) Assert.That(passageOwner.FrameId, Is.EqualTo("world"), "Boarding support must not move with the train.");
                    Assert.That(passage.Find("Provisional_PassageCeiling") != null, Is.EqualTo(portal.Ceiling));
                    var passageCeiling = passage.Find("Provisional_PassageCeiling");
                    if (passageCeiling != null)
                    {
                        var collider = passageCeiling.GetComponent<BoxCollider>();
                        foreach (var endpoint in new[] { V(portal.FromPoint), V(portal.ToPoint) })
                            Assert.That(Vector3.Distance(collider.ClosestPoint(endpoint + Vector3.up * (portal.ClearHeight + .01f)),
                                endpoint + Vector3.up * (portal.ClearHeight + .01f)), Is.LessThan(.001), "Finite ceiling must cover each endpoint.");
                    }
                    foreach (var guard in passage.GetComponentsInChildren<MeshCollider>())
                    {
                        var vertices = guard.sharedMesh.vertices.Select(guard.transform.TransformPoint).ToArray();
                        var flat = V(portal.ToPoint) - V(portal.FromPoint); flat.y = 0; var side = Vector3.Cross(Vector3.up, flat.normalized);
                        var sign = Vector3.Dot(guard.transform.position - passage.position, side) < 0 ? -1 : 1;
                        foreach (var endpoint in new[] { V(portal.FromPoint), V(portal.ToPoint) })
                        foreach (var height in new[] { 0f, portal.WallHeight })
                        {
                            var corner = endpoint + side * sign * portal.ClearWidth / 2 + Vector3.up * height;
                            Assert.That(vertices.Min(v => Vector3.Distance(v, corner)), Is.LessThan(.001), "Guard corners must remain vertically above the floor endpoints.");
                        }
                    }
                    if (portal.LinkedDoorEntityIds.Length > 0)
                    {
                        var gate = UnityEngine.Object.FindObjectsByType<ConnectedPortalBarrier>(FindObjectsSortMode.None).Single(g => g.PortalId == portal.Id);
                        Assert.That((gate.transform.position - V(portal.ClosurePoint)).sqrMagnitude, Is.LessThan(1e-8));
                        var panel = gate.transform.Find("Provisional_DoorPanel");
                        Assert.That(panel.localScale.y, Is.EqualTo(portal.ClosureHeight).Within(1e-6));
                        Assert.That(panel.localPosition.y - panel.localScale.y / 2, Is.EqualTo(portal.ClosureBottom).Within(1e-6));
                    }
                    foreach (var reverse in new[] { false, true })
                    {
                        var start = definition.Region(reverse ? portal.To : portal.From);
                        var end = definition.Region(reverse ? portal.From : portal.To);
                        var pose = definition.Pose(start.Id, start.Hub);
                        Walk(definition, ref pose, reverse ? portal.ToPoint : portal.FromPoint, all);
                        Walk(definition, ref pose, reverse ? portal.FromPoint : portal.ToPoint, all);
                        Walk(definition, ref pose, end.Hub, all);
                        Assert.That(pose.RegionId, Is.EqualTo(end.Id), portal.Id);
                        Assert.That(pose.FrameId, Is.EqualTo(end.FrameId), portal.Id);
                        var begin = V(reverse ? portal.ToPoint : portal.FromPoint);
                        var finish = V(reverse ? portal.FromPoint : portal.ToPoint);
                        var phaseDirection = finish - begin; phaseDirection.y = 0; phaseDirection.Normalize();
                        foreach (var phase in new[] { .037f, .081f, .113f })
                        {
                            var noisy = begin - phaseDirection * phase + Vector3.Cross(Vector3.up, phaseDirection) * .001f;
                            pose = definition.Pose(start.Id, P(noisy));
                            Walk(definition, ref pose, P(finish), all);
                        }
                    }
                    var middle = Vector3.Lerp(V(portal.FromPoint), V(portal.ToPoint), .45f);
                    var startPose = definition.Pose(portal.From, P(middle), portal.Id);
                    var noDestination = new HashSet<string> { portal.From };
                    var delta = (V(portal.ToPoint) - middle).normalized; delta.y = 0;
                    for (var i = 0; i < 80; i++)
                    {
                        if (GroundedWorldMotor.TryMove(definition, startPose, delta * .15f, noDestination, null, out var next)) startPose = next;
                    }
                    Assert.That(startPose.RegionId, Is.EqualTo(portal.From), "Loading must hold only the crossing actor.");
                }
                var controlled = definition.Portals.First(p => definition.Regions.Any(r => r.ControlledPortalId == p.Id));
                var barrier = UnityEngine.Object.FindObjectsByType<ConnectedPortalBarrier>(FindObjectsSortMode.None).First(b => b.PortalId == controlled.Id);
                barrier.SetOpen(false); Physics.SyncTransforms();
                var center = (V(controlled.FromPoint) + V(controlled.ToPoint)) / 2;
                var direction = V(controlled.ToPoint) - V(controlled.FromPoint); direction.y = 0; direction.Normalize();
                var before = center - direction * .8f;
                var held = definition.Pose(controlled.From, P(before), controlled.Id);
                var blocked = new HashSet<string> { controlled.Id };
                Assert.That(GroundedWorldMotor.TryMove(definition, held, direction * 1.6f, all, blocked, out _), Is.False);
                CollectionAssert.AreEqual(legacy, File.ReadAllBytes("Assets/CHOOguardGenerated/FoundationDemo/FoundationDemo.unity"));
                CollectionAssert.AreEqual(slice, File.ReadAllBytes(MultiplayerSceneBuilder.ScenePath));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(path);
                if (previous.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static void Walk(ConnectedWorldDefinition world, ref SpatialPose pose, Point3 target, ISet<string> ready)
        {
            var ticks = 0;
            while (new Vector2(pose.Position.X - target.X, pose.Position.Z - target.Z).magnitude > .08f)
            {
                var delta = V(target) - V(pose.Position); delta.y = 0; delta = Vector3.ClampMagnitude(delta, .15f);
                Assert.That(GroundedWorldMotor.TryMove(world, pose, delta, ready, null, out var next), Is.True,
                    pose.RegionId + " blocked at " + JsonUtility.ToJson(pose.Position) + " toward " + JsonUtility.ToJson(target) + ": " + GroundedWorldMotor.LastFailure);
                pose = next;
                Assert.That(++ticks, Is.LessThan(1500));
            }
            Assert.That(pose.Position.Y, Is.EqualTo(target.Y).Within(.08), "Real floor height must follow the slope.");
        }

        private static Vector3 V(Point3 p) => new Vector3(p.X, p.Y, p.Z);
        private static Point3 P(Vector3 p) => new Point3(p.x, p.y, p.z);
        private sealed class Sink : ICommitSink { public void Append(ShiftCommit commit) { } }
        [Serializable] private sealed class Coverage { public Zone[] zones; public Edge[] connections; }
        [Serializable] private sealed class Zone { public string id; }
        [Serializable] private sealed class Edge { public string from, to; }
    }
}
