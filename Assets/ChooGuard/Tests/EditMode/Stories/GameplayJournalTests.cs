#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ChooGuard.Persistence;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class GameplayJournalTests
    {
        private string root, path;
        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "cg 저장 경로 " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); path = Path.Combine(root, "실행 기록.db");
        }
        [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        private GameplayJournal Open() => new GameplayJournal(CSOPS0201Tests.Open(path));

        [Test] public void KoreanPayloadAndPrivateOwnerSurviveRestartAndRunIsolation()
        {
            using (var journal = Open())
            {
                journal.AppendAndCheckpoint("run-a", 3, 7, "memory", "시민-가", "{\"관측\":\"연기\"}", "{\"generation\":3}");
                journal.Append("run-b", 0, 1, "memory", "시민-나", "{\"관측\":\"평시\"}");
            }
            using (var journal = Open())
            {
                var rows = journal.Read("run-a");
                Assert.That(rows.Count, Is.EqualTo(1));
                Assert.That(rows[0].ActorId, Is.EqualTo("시민-가"));
                Assert.That(rows[0].Json, Is.EqualTo("{\"관측\":\"연기\"}"));
                Assert.That(rows[0].Generation, Is.EqualTo(3));
                Assert.That(rows[0].Sequence, Is.EqualTo(7));
                Assert.That(journal.ReadCheckpoint("run-a").Json, Is.EqualTo("{\"generation\":3}"));
                Assert.That(journal.ReadCheckpoint("run-b"), Is.Null);
            }
        }
        [Test] public void RetriesCannotOverwritePayloadOwnerOrCheckpoint()
        {
            using (var journal = Open())
            {
                journal.AppendAndCheckpoint("run", 1, 1, "mutation", "owner", "{\"open\":true}", "{\"revision\":1}");
                journal.AppendAndCheckpoint("run", 1, 1, "mutation", "owner", "{\"open\":true}", "{\"revision\":1}");
                Assert.Throws<InvalidOperationException>(() => journal.Append("run", 1, 1, "mutation", "other", "{\"open\":true}"));
                Assert.Throws<InvalidOperationException>(() => journal.Append("run", 1, 1, "mutation", "owner", "{\"open\":false}"));
                Assert.Throws<InvalidOperationException>(() => journal.SaveCheckpoint("run", 1, 1, "{\"revision\":999}"));
                Assert.That(journal.Read("run").Count, Is.EqualTo(1));
                Assert.That(journal.ReadCheckpoint("run").Json, Is.EqualTo("{\"revision\":1}"));
            }
        }
        [Test] public void NewGenerationMayResetSequenceButStaleWritersCannotAppend()
        {
            using (var journal = Open())
            {
                journal.Append("run", 1, 80, "event", "actor", "{}");
                journal.AppendAndCheckpoint("run", 2, 1, "event", "actor", "{}", "{\"new\":true}");
                Assert.Throws<InvalidOperationException>(() => journal.Append("run", 1, 81, "event", "actor", "{}"));
                Assert.Throws<InvalidOperationException>(() => journal.SaveCheckpoint("run", 1, 80, "{}"));
                Assert.Throws<InvalidOperationException>(() => journal.SaveCheckpoint("run", 2, 2, "{}"));
                Assert.That(journal.ReadCheckpoint("run").Generation, Is.EqualTo(2));
            }
        }
        [Test] public void CheckpointFailureRollsBackItsEventAndRetryCanCommit()
        {
            using (var provider = CSOPS0201Tests.Open(path))
            using (var journal = new GameplayJournal(provider))
            {
                // Real SQLite failure after INSERT entry; not a mock of the journal's transaction API.
                var execute = typeof(SqliteProvider).GetMethod("Execute", BindingFlags.NonPublic | BindingFlags.Instance);
                execute.Invoke(provider, new object[] { "CREATE TEMP TRIGGER fail_checkpoint BEFORE INSERT ON cg_gameplay_checkpoint BEGIN SELECT RAISE(ABORT,'injected disk write failure'); END", Array.Empty<string>() });
                Assert.Throws<InvalidOperationException>(() => journal.AppendAndCheckpoint("run", 0, 1, "event", "actor", "{}", "{}"));
                Assert.That(journal.Read("run"), Is.Empty);
                Assert.That(journal.ReadCheckpoint("run"), Is.Null);
                execute.Invoke(provider, new object[] { "DROP TRIGGER fail_checkpoint", Array.Empty<string>() });
                journal.AppendAndCheckpoint("run", 0, 1, "event", "actor", "{}", "{}");
                Assert.That(journal.ReadCheckpoint("run").Sequence, Is.EqualTo(1));
            }
        }
        [Test] public void OversizedPayloadLeavesNoDurableEntry()
        {
            using (var journal = Open())
            {
                var payload = "{\"text\":\"" + new string('x', GameplayJournal.MaxEntryBytes) + "\"}";
                Assert.Throws<ArgumentException>(() => journal.Append("run", 0, 1, "event", "actor", payload));
                Assert.That(journal.Read("run"), Is.Empty);
            }
        }
        [Test] public void SQLiteFullIsNotReportedAsSuccessOrMaskedByRollback()
        {
            using (var provider = CSOPS0201Tests.Open(path))
            using (var journal = new GameplayJournal(provider))
            {
                var scalar = typeof(SqliteProvider).GetMethod("Scalar", BindingFlags.NonPublic | BindingFlags.Instance);
                var pages = (string)scalar.Invoke(provider, new object[] { "PRAGMA page_count", Array.Empty<string>() });
                scalar.Invoke(provider, new object[] { "PRAGMA max_page_count=" + pages, Array.Empty<string>() });
                var payload = "{\"text\":\"" + new string('x', 64 * 1024) + "\"}";
                var error = Assert.Throws<InvalidOperationException>(() => journal.AppendAndCheckpoint("full", 0, 1, "event", "owner", payload, "{}"));
                Assert.That(error.Message, Does.StartWith("SQLITE_13:"));
                Assert.That(journal.Read("full"), Is.Empty);
                Assert.That(journal.ReadCheckpoint("full"), Is.Null);
            }
            using (var reopened = Open()) Assert.That(reopened.Read("full"), Is.Empty);
        }
        [Test] public void CompetingConnectionsAgreeOnExactlyOnePayload()
        {
            using (var first = Open())
            using (var second = Open())
            {
                var accepted = 0; var rejected = 0;
                Action<GameplayJournal, string> append = (journal, payload) =>
                {
                    try { journal.Append("race", 0, 1, "event", "actor", payload); System.Threading.Interlocked.Increment(ref accepted); }
                    catch (InvalidOperationException error) when (error.Message == "GAMEPLAY_ENTRY_CONFLICT") { System.Threading.Interlocked.Increment(ref rejected); }
                };
                Task.WaitAll(Task.Run(() => append(first, "{\"winner\":1}")), Task.Run(() => append(second, "{\"winner\":2}")));
                Assert.That(accepted, Is.EqualTo(1)); Assert.That(rejected, Is.EqualTo(1));
                var rows = first.Read("race"); Assert.That(rows.Count, Is.EqualTo(1));
                Assert.That(rows[0].Json, Is.EqualTo("{\"winner\":1}").Or.EqualTo("{\"winner\":2}"));
            }
        }
    }
}
#endif
