using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class ShiftJournalTests
    {
        private string directory;
        private WorldState Initial() => new WorldState { WorldId = "world", ShiftId = "shift",
            Participants = new[] { new ParticipantState { ParticipantId = "p", TeamId = "t", RoleId = "role-01", RegionId = "hall" } },
            Entities = new[] { new EntityState { EntityId = "door", RegionId = "hall" } } };
        private WorldCommand Command(string id) => new WorldCommand { WorldId = "world", ShiftId = "shift",
            ParticipantId = "p", TeamId = "t", CommandId = id, TargetId = "door", Kind = CommandKind.Operate };

        [SetUp] public void Setup() => directory = Path.Combine(Path.GetTempPath(), "chooguard-journal-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test]
        public void RealDiskJournalRestoresApprovedCommandAndRejectsDuplicateWriter()
        {
            using (var journal = new ShiftJournal(directory))
            {
                var shift = journal.Open(Initial(), (a, e) => true);
                Assert.Throws<IOException>(() => { using (var duplicate = new ShiftJournal(directory)) { } });
                Assert.That(shift.Submit("p", Command("one")).Code, Is.EqualTo(CommandCode.Accepted));
            }
            using (var journal = new ShiftJournal(directory))
            {
                var shift = journal.Open(Initial(), (a, e) => true);
                Assert.That(shift.ExportCheckpoint().Paused, Is.True);
                Assert.That(shift.ExportCheckpoint().Entities[0].Revision, Is.EqualTo(1));
                Assert.That(shift.Submit("p", Command("one")).Sequence, Is.EqualTo(1));
            }
        }

        [Test]
        public void InterruptedLastWriteIsDiscardedButCompleteCorruptionFailsClosed()
        {
            using (var journal = new ShiftJournal(directory)) journal.Open(Initial(), (a, e) => true).Submit("p", Command("one"));
            var log = Path.Combine(directory, "actions-v1.jsonl");
            File.AppendAllText(log, "{\"interrupted\":");
            using (var journal = new ShiftJournal(directory))
                Assert.That(journal.Open(Initial(), (a, e) => true).ExportCheckpoint().Sequence, Is.EqualTo(1));
            File.AppendAllText(log, "{\"invalid\":true}\n");
            using (var journal = new ShiftJournal(directory))
                Assert.Throws<InvalidDataException>(() => journal.Open(Initial(), (a, e) => true));
        }

        [Test]
        public void CheckpointWithChangedBytesOrForeignWorldIsNeverLoaded()
        {
            using (var journal = new ShiftJournal(directory)) journal.Open(Initial(), (a, e) => true);
            var checkpoint = Path.Combine(directory, "checkpoint-v1.json");
            var json = File.ReadAllText(checkpoint); File.WriteAllText(checkpoint, json.Replace("world", "other"));
            using (var journal = new ShiftJournal(directory))
                Assert.Throws<InvalidDataException>(() => journal.Open(Initial(), (a, e) => true));
        }

        [Test]
        public void CheckpointRejectsForeignIdentityMissingReceiptAndChangedFingerprintWithoutReplacingFile()
        {
            using (var journal = new ShiftJournal(directory))
            {
                var shift = journal.Open(Initial(), (a, e) => true);
                shift.Submit("p", Command("one"));
                var originalFile = File.ReadAllText(Path.Combine(directory, "checkpoint-v1.json"));
                var foreign = shift.ExportCheckpoint(); foreign.WorldId = "foreign";
                Assert.Throws<InvalidDataException>(() => journal.Checkpoint(foreign));
                var missing = shift.ExportCheckpoint(); missing.Receipts = Array.Empty<CommandReceipt>();
                Assert.Throws<InvalidDataException>(() => journal.Checkpoint(missing));
                var changed = shift.ExportCheckpoint(); changed.Receipts[0].Fingerprint = new string('a', 64);
                Assert.Throws<InvalidDataException>(() => journal.Checkpoint(changed));
                Assert.That(File.ReadAllText(Path.Combine(directory, "checkpoint-v1.json")), Is.EqualTo(originalFile));
            }
            using (var journal = new ShiftJournal(directory))
                Assert.That(journal.Open(Initial(), (a, e) => true).Submit("p", Command("one")).Code, Is.EqualTo(CommandCode.Accepted));
        }
    }
}
