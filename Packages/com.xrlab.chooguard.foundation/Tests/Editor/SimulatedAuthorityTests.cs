using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;

namespace ChooGuard.Foundation.Tests
{
    public sealed class SimulatedAuthorityTests
    {
        private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private sealed class Sink : ICommitSink { public readonly List<ShiftCommit> Commits = new List<ShiftCommit>(); public void Append(ShiftCommit c) => Commits.Add(c.Copy()); }
        private sealed class FailedSink : ICommitSink { public void Append(ShiftCommit c) => throw new IOException("fixture interrupted flush"); }
        internal static string Physical(long tick)
        {
            var fire = new ZoneFireModel(new FireNetworkDefinition { Cells = new[] { new FireCellDefinition { Id = "cell", WidthM = 5, DepthM = 5, HeightM = 4 } } });
            var crowd = new CrowdMotionModel(new CrowdDefinition { ProfileId = "fixture", Spaces = new[] { new CrowdSpace { Id = "floor" } } },
                new[] { new CrowdAgent { Id = "body", ContactSpaceId = "floor", RegionId = "train", FrameId = "train", SurfaceId = "floor" } });
            return PhysicalCheckpoint.Encode(new PhysicalWorldState { DefinitionHash = Hash, SimulationTick = tick, Fire = fire.ExportState(), CrowdCheckpoint = crowd.ExportCheckpoint() });
        }
        internal static WorldState Initial() => new WorldState { SchemaVersion = 3, WorldId = "world", ShiftId = "shift", SpatialProfileId = "profile",
            SimulationDefinitionHash = Hash, SimulationCheckpoint = Physical(0),
            Frames = new[] { new SpatialFrame { FrameId = "world" }, new SpatialFrame { FrameId = "train" } },
            Participants = new[] { new ParticipantState { ParticipantId = "p", TeamId = "team", RoleId = "role-01", RegionId = "train", FrameId = "train", ObservedIds = new[] { "incident-old" } } },
            Entities = new[] { new EntityState { EntityId = "equipment", Kind = EntityKind.Equipment, RegionId = "train", FrameId = "train", Position = new Point3(1, 0, 0), LocalPosition = new Point3(1, 0, 0) },
                new EntityState { EntityId = "incident-old", Kind = EntityKind.Incident, RegionId = "train", FrameId = "train", Position = new Point3(1, 0, 0), LocalPosition = new Point3(1, 0, 0) } } };
        internal static WorldCommand Command(CommandKind kind, string id, string target) => new WorldCommand { WorldId = "world", ShiftId = "shift", ParticipantId = "p", TeamId = "team", CommandId = id, Kind = kind, TargetId = target };
        internal static ServerSimulationUpdate Move(WorldState s, float offset, long tick)
        {
            var entities = Array.ConvertAll(s.Entities, e => { var copy = e.Copy(); copy.Position = new Point3(offset + copy.LocalPosition.X, copy.LocalPosition.Y, copy.LocalPosition.Z); return copy; });
            return new ServerSimulationUpdate { Tick = tick, DefinitionHash = Hash, Checkpoint = Physical(tick),
                Frames = new[] { new SpatialFrame { FrameId = "world" }, new SpatialFrame { FrameId = "train", Origin = new Point3(offset, 0, 0) } },
                Actors = new[] { new ServerActorPose { ParticipantId = "p", Pose = new SpatialPose { RegionId = "train", FrameId = "train", Position = new Point3(offset, 0, 0), LocalPosition = new Point3(0, 0, 0) } } }, Entities = entities };
        }

        private static PreparedPhysicalCheckpoint Prepared(ServerSimulationUpdate update)
        {
            var physical = PhysicalCheckpoint.Decode(update.Checkpoint, Hash);
            return PhysicalCheckpoint.Prepare(physical, CrowdMotionModel.FromCheckpoint(physical.CrowdCheckpoint).ExportCheckpointProof());
        }

        [Test]
        public void PreparedPublicationRetainsExactOldViewsAndPreNextTickApprovedCrashRecovery()
        {
            var initial = Initial(); var sink = new Sink(); var shift = new AuthoritativeShift(initial, sink, (_,__)=>true);
            var update = Move(initial, 50, 1); var prepared = Prepared(update);
            shift.ApplyPreparedServerSimulation(update, prepared);
            var previous = shift.ReadSimulation(); var previousCheckpoint = previous.Checkpoint;
            var command = Command(CommandKind.Operate, "approve-at-tick-one", "equipment");
            var accepted = shift.Submit("p", command);
            Assert.That(accepted.Code, Is.EqualTo(CommandCode.Accepted));
            Assert.That(sink.Commits[0].SimulationCheckpoint, Is.EqualTo(prepared.Encoded));
            var recovered = AuthoritativeShift.Restore(initial, new Sink(), (_,__)=>true);
            recovered.Replay(sink.Commits[0]);
            Assert.That(recovered.ReadSimulation().Checkpoint, Is.EqualTo(prepared.Encoded));
            Assert.That(recovered.ReadSimulation().Tick, Is.EqualTo(1)); Assert.That(recovered.Paused, Is.True);
            Assert.That(recovered.Submit("p", command).Sequence, Is.EqualTo(accepted.Sequence));
            var next = Move(shift.ExportCheckpoint(), 55, 2); shift.ApplyPreparedServerSimulation(next, Prepared(next));
            Assert.That(previous.Tick, Is.EqualTo(1)); Assert.That(previous.Sequence, Is.Zero);
            Assert.That(previous.Frames[1].Origin.X, Is.EqualTo(50)); Assert.That(previous.Checkpoint, Is.EqualTo(previousCheckpoint));
            Assert.That(shift.ReadSimulation().Tick, Is.EqualTo(2));
        }

        [Test]
        public void PreparedPublicationRejectsStaleBytesOwnershipAndPausedStateWithoutMutation()
        {
            var initial = Initial(); var shift = new AuthoritativeShift(initial, new Sink(), (_,__)=>true);
            var update = Move(initial, 2, 1); var prepared = Prepared(update);
            update.Checkpoint = Physical(2);
            Assert.Throws<ArgumentException>(()=>shift.ApplyPreparedServerSimulation(update, prepared));
            update = Move(initial, 2, 1); update.Entities[0].LeaderId = "p";
            Assert.Throws<ArgumentException>(()=>shift.ApplyPreparedServerSimulation(update, prepared));
            Assert.That(shift.ReadSimulation().Tick, Is.Zero);
            var paused = AuthoritativeShift.Restore(initial, new Sink(), (_,__)=>true);
            Assert.Throws<InvalidOperationException>(()=>paused.ApplyPreparedServerSimulation(Move(initial, 2, 1), prepared));
            var failed = new AuthoritativeShift(initial, new FailedSink(), (_,__)=>true);
            update = Move(initial,2,1); failed.ApplyPreparedServerSimulation(update,Prepared(update));
            Assert.That(failed.Submit("p",Command(CommandKind.Operate,"flush-fails","equipment")).Code,Is.EqualTo(CommandCode.PersistenceUnavailable));
            Assert.That(failed.Paused,Is.True); Assert.That(failed.ReadSimulation().Tick,Is.EqualTo(1));
            Assert.That(failed.ReadSimulation().Sequence,Is.Zero); Assert.That(failed.ReadSimulation().Checkpoint,Is.EqualTo(update.Checkpoint));
        }

        [Test]
        public void PreparedAuthorityCannotObserveLaterMutationOfTheCallerPhysicalState()
        {
            var initial=Initial(); var shift=new AuthoritativeShift(initial,new Sink(),(_,__)=>true);
            var update=Move(initial,2,1); var physical=PhysicalCheckpoint.Decode(update.Checkpoint,Hash);
            var proof=CrowdMotionModel.FromCheckpoint(physical.CrowdCheckpoint).ExportCheckpointProof();
            var prepared=PhysicalCheckpoint.Prepare(physical,proof);
            physical.SimulationTick=77; physical.DefinitionHash=new string('b',64); physical.Fire.Cells[0].UpperEnergyJ=-1;
            shift.ApplyPreparedServerSimulation(update,prepared);
            Assert.That(shift.ReadSimulation().Tick,Is.EqualTo(1));
            Assert.That(shift.ReadSimulation().DefinitionHash,Is.EqualTo(Hash));
            Assert.That(shift.ReadSimulation().Checkpoint,Is.EqualTo(prepared.Encoded));
        }

        [Test]
        public void MovingFrameKeepsHistoricalReportPoseAndApprovedReplayContext()
        {
            var sink = new Sink(); var initial = Initial(); var shift = new AuthoritativeShift(initial, sink, (a, e) => true);
            Assert.That(shift.Submit("p", Command(CommandKind.Report, "report", "incident-old")).Code, Is.EqualTo(CommandCode.Accepted));
            shift.ApplyServerSimulation(Move(shift.ExportCheckpoint(), 50, 1));
            var report = shift.ExportCheckpoint().Reports[0]; Assert.That(report.Position.X, Is.EqualTo(1));
            Assert.That(report.ObservedFrame.Origin.X, Is.Zero); Assert.That(report.ObservedSimulationTick, Is.Zero);
            var operate = Command(CommandKind.Operate, "operate", "equipment"); Assert.That(shift.Submit("p", operate).Code, Is.EqualTo(CommandCode.Accepted));
            var restored = AuthoritativeShift.Restore(initial, new Sink(), (a, e) => true);
            foreach (var commit in sink.Commits) restored.Replay(commit);
            var recovered = restored.ExportCheckpoint();
            Assert.That(recovered.SimulationTick, Is.EqualTo(1)); Assert.That(recovered.Frames[1].Origin.X, Is.EqualTo(50));
            Assert.That(recovered.Entities[0].Position.X, Is.EqualTo(51)); Assert.That(recovered.Participants[0].LocalPosition.X, Is.Zero);
            Assert.That(recovered.Paused, Is.True);
            Assert.That(restored.Submit("p", operate).Sequence, Is.EqualTo(2));
        }

        [Test]
        public void PhysicalPublicationCannotChangeOwnershipOrAdvanceAPausedWorld()
        {
            var initial = Initial(); var shift = new AuthoritativeShift(initial, new Sink(), (a, e) => true);
            var update = Move(initial, 2, 1); update.Entities[0].LeaderId = "p";
            Assert.Throws<ArgumentException>(() => shift.ApplyServerSimulation(update));
            Assert.That(shift.ExportCheckpoint().SimulationTick, Is.Zero);
            var restored = AuthoritativeShift.Restore(initial, new Sink(), (a, e) => true);
            Assert.Throws<InvalidOperationException>(() => restored.ApplyServerSimulation(Move(initial, 2, 1)));
        }

        [Test]
        public void StorageFailurePublishesPauseAndCompletedIncidentCannotReuseDiscovery()
        {
            var initial = Initial(); var failed = new AuthoritativeShift(initial, new FailedSink(), (a, e) => true);
            Assert.That(failed.Submit("p", Command(CommandKind.Operate, "failed", "equipment")).Code, Is.EqualTo(CommandCode.PersistenceUnavailable));
            Assert.That(failed.Paused, Is.True); Assert.That(failed.Observe("p").Paused, Is.True); Assert.That(failed.Participant("p").InputEnabled, Is.False);
            var shift = new AuthoritativeShift(initial, new Sink(), (a, e) => true); var update = Move(initial, 0, 1);
            update.Entities[1].Active = false; update.Entities[1].Revision = 1; shift.ApplyServerSimulation(update);
            update = Move(shift.ExportCheckpoint(), 0, 2); update.Entities[1].Active = true; update.Entities[1].Revision = 2;
            Assert.Throws<ArgumentException>(() => shift.ApplyServerSimulation(update));
        }

        [Test]
        public void VisibleIncidentCanBeDiscoveredAtRangeWithoutMovingIntoActionReach()
        {
            var initial = Initial(); initial.Participants[0].ObservedIds = Array.Empty<string>();
            initial.Entities[1].Position = new Point3(10, 0, 0); initial.Entities[1].LocalPosition = new Point3(10, 0, 0);
            var shift = new AuthoritativeShift(initial, new Sink(), (a, e) => true);
            Assert.That(shift.DiscoverNearby("p", _ => true), Is.EqualTo(1));
            Assert.That(Array.Exists(shift.Observe("p").Entities, e => e.EntityId == "incident-old"), Is.True);
        }

        [Test]
        public void NewIncidentIdentityDoesNotInheritPreviousDiscovery()
        {
            var initial = Initial(); var shift = new AuthoritativeShift(initial, new Sink(), (a, e) => true); var update = Move(initial, 0, 1);
            var list = new List<EntityState>(update.Entities) { new EntityState { EntityId = "incident-new", Kind = EntityKind.Incident, RegionId = "train", FrameId = "train", Position = new Point3(1, 0, 0), LocalPosition = new Point3(1, 0, 0) } };
            update.Entities = list.ToArray(); shift.ApplyServerSimulation(update);
            Assert.That(Array.Exists(shift.Observe("p").Entities, e => e.EntityId == "incident-new"), Is.False);
        }
    }
}
