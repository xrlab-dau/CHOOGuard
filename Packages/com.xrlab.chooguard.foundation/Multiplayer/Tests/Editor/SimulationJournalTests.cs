using System;
using System.IO;
using NUnit.Framework;
using ChooGuard.Foundation.Simulation;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class SimulationJournalTests
    {
        private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private static string Physical(long tick)
        {
            var fire = new ZoneFireModel(new FireNetworkDefinition { Cells = new[] { new FireCellDefinition { Id = "cell", WidthM = 5, DepthM = 5, HeightM = 4 } } });
            var crowd = new CrowdMotionModel(new CrowdDefinition { ProfileId = "fixture", Spaces = new[] { new CrowdSpace { Id = "floor" } } },
                new[] { new CrowdAgent { Id = "body", ContactSpaceId = "floor", RegionId = "train", FrameId = "train", SurfaceId = "floor" } });
            return PhysicalCheckpoint.Encode(new PhysicalWorldState { DefinitionHash = Hash, SimulationTick = tick, Fire = fire.ExportState(), CrowdCheckpoint = crowd.ExportCheckpoint() });
        }
        private static WorldState Initial() => new WorldState { SchemaVersion = 3, WorldId = "world", ShiftId = "shift", SpatialProfileId = "profile",
            SimulationDefinitionHash = Hash, SimulationCheckpoint = Physical(0),
            Frames = new[] { new SpatialFrame { FrameId = "world" }, new SpatialFrame { FrameId = "train" } },
            Participants = new[] { new ParticipantState { ParticipantId = "p", TeamId = "team", RoleId = "role-01", RegionId = "train", FrameId = "train", ObservedIds = new[] { "incident-old" } } },
            Entities = new[] { new EntityState { EntityId = "equipment", Kind = EntityKind.Equipment, RegionId = "train", FrameId = "train", Position = new Point3(1, 0, 0), LocalPosition = new Point3(1, 0, 0) },
                new EntityState { EntityId = "incident-old", Kind = EntityKind.Incident, RegionId = "train", FrameId = "train", Position = new Point3(1, 0, 0), LocalPosition = new Point3(1, 0, 0) } } };
        private static WorldCommand Command(CommandKind kind, string id, string target) => new WorldCommand { WorldId = "world", ShiftId = "shift", ParticipantId = "p", TeamId = "team", CommandId = id, Kind = kind, TargetId = target };
        private static ServerSimulationUpdate Move(WorldState s, float offset, long tick) => new ServerSimulationUpdate {
            Tick = tick, DefinitionHash = Hash, Checkpoint = Physical(tick),
            Frames = new[] { new SpatialFrame { FrameId = "world" }, new SpatialFrame { FrameId = "train", Origin = new Point3(offset, 0, 0) } },
            Actors = new[] { new ServerActorPose { ParticipantId = "p", Pose = new SpatialPose { RegionId = "train", FrameId = "train", Position = new Point3(offset, 0, 0) } } },
            Entities = Array.ConvertAll(s.Entities, e => { var copy = e.Copy(); copy.Position = new Point3(offset + copy.LocalPosition.X, copy.LocalPosition.Y, copy.LocalPosition.Z); return copy; }) };
        private string directory;
        [SetUp] public void Setup() { directory = Path.Combine(Path.GetTempPath(), "chooguard-v3-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test]
        public void ApprovedActionsAndAutonomousPhysicalBoundaryReplayTogether()
        {
            var initial = Initial();
            var command = Command(CommandKind.Operate, "operate", "equipment");
            using (var journal = new SimulationJournal(directory))
            {
                var shift = journal.Open(initial, (a, e) => true);
                Assert.That(shift.Submit("p", Command(CommandKind.Report, "report", "incident-old")).Code, Is.EqualTo(CommandCode.Accepted));
                shift.ApplyServerSimulation(Move(shift.ExportCheckpoint(), 50, 1));
                journal.RecordBoundary(shift.ReadSimulation());
                Assert.That(shift.Submit("p", command).Code, Is.EqualTo(CommandCode.Accepted));
            }
            using (var journal = new SimulationJournal(directory))
            {
                var recovered = journal.Open(initial, (a, e) => true); var state = recovered.ExportCheckpoint();
                Assert.That(state.Paused, Is.True); Assert.That(state.Sequence, Is.EqualTo(2)); Assert.That(state.SimulationTick, Is.EqualTo(1));
                Assert.That(state.Frames[1].Origin.X, Is.EqualTo(50)); Assert.That(state.Reports[0].Position.X, Is.EqualTo(1));
                Assert.That(recovered.Submit("p", command).Sequence, Is.EqualTo(2));
                journal.Checkpoint(state);
            }
            using (var journal = new SimulationJournal(directory))
                Assert.That(journal.Open(initial, (a, e) => true).ExportCheckpoint().SimulationTick, Is.EqualTo(1));
        }

        [Test]
        public void TruncatedUnacknowledgedTailIsRemovedButCompleteCorruptionIsRejected()
        {
            var initial = Initial();
            using (var journal = new SimulationJournal(directory))
            { var shift = journal.Open(initial, (a, e) => true); shift.Submit("p", Command(CommandKind.Operate, "one", "equipment")); }
            var path = Path.Combine(directory, "actions-v3.jsonl"); var length = new FileInfo(path).Length;
            File.AppendAllText(path, "{\"Schema\":3,");
            using (var journal = new SimulationJournal(directory))
                Assert.That(journal.Open(initial, (a, e) => true).ExportCheckpoint().Sequence, Is.EqualTo(1));
            Assert.That(new FileInfo(path).Length, Is.EqualTo(length));
            var bytes = File.ReadAllBytes(path); bytes[bytes.Length - 5] ^= 1; File.WriteAllBytes(path, bytes);
            using (var journal = new SimulationJournal(directory)) Assert.Throws<InvalidDataException>(() => journal.Open(initial, (a, e) => true));
        }

        [Test]
        public void StalePhysicalViewCannotOverwriteALaterApprovedActionAtTheSameTick()
        {
            var initial = Initial();
            using (var journal = new SimulationJournal(directory))
            {
                var shift = journal.Open(initial, (a, e) => true); var stale = shift.ReadSimulation();
                Assert.That(shift.Submit("p", Command(CommandKind.Operate, "approved", "equipment")).Code, Is.EqualTo(CommandCode.Accepted));
                Assert.Throws<InvalidDataException>(() => journal.RecordBoundary(stale));
            }
            using (var journal = new SimulationJournal(directory))
            {
                var state = journal.Open(initial, (a, e) => true).ExportCheckpoint();
                Assert.That(state.Entities[0].Active, Is.False); Assert.That(state.Entities[0].Revision, Is.EqualTo(1));
                Assert.That(state.Receipts.Length, Is.EqualTo(1));
            }
        }

        [Test]
        public void RejectedBoundaryCannotPoisonAnOtherwiseRecoverableJournal()
        {
            var initial = Initial();
            using (var journal = new SimulationJournal(directory))
            {
                var shift = journal.Open(initial, (a, e) => true);
                var ownership = shift.ReadSimulation(); ownership.Entities[0].LeaderId = "p";
                Assert.Throws<InvalidDataException>(() => journal.RecordBoundary(ownership));
                var observation = shift.ReadSimulation(); observation.Participants[0].ObservedIds = Array.Empty<string>();
                Assert.Throws<InvalidDataException>(() => journal.RecordBoundary(observation));
                var frame = shift.ReadSimulation(); frame.Frames[0].FrameId = "replacement";
                Assert.Throws<InvalidDataException>(() => journal.RecordBoundary(frame));
                Assert.That(shift.Submit("p", Command(CommandKind.Operate, "one", "equipment")).Code, Is.EqualTo(CommandCode.Accepted));
                var revision = shift.ReadSimulation(); revision.Entities[0].Revision = 0;
                Assert.Throws<InvalidDataException>(() => journal.RecordBoundary(revision));
                journal.RecordBoundary(shift.ReadSimulation());
            }
            using (var journal = new SimulationJournal(directory))
                Assert.That(journal.Open(initial, (a, e) => true).ExportCheckpoint().Entities[0].Revision, Is.EqualTo(1));
        }

        [Test]
        public void CheckpointCannotMoveBehindTheDurablePhysicalBoundaryAndWriterIsExclusive()
        {
            var initial = Initial();
            using (var journal = new SimulationJournal(directory))
            {
                var shift = journal.Open(initial, (a, e) => true);
                Assert.Throws<IOException>(() => { using (var duplicate = new SimulationJournal(directory)) { } });
                shift.ApplyServerSimulation(Move(initial, 5, 1)); journal.RecordBoundary(shift.ReadSimulation());
                Assert.Throws<InvalidDataException>(() => journal.Checkpoint(initial));
            }
        }
    }
}
