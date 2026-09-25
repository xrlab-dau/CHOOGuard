using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace ChooGuard.Persistence
{
    public sealed class GameplayJournalEntry
    {
        public string RunId { get; }
        public long Generation { get; }
        public long Sequence { get; }
        public string Kind { get; }
        public string ActorId { get; }
        public string Json { get; }
        internal GameplayJournalEntry(string runId, long generation, long sequence, string kind, string actorId, string json)
        { RunId = runId; Generation = generation; Sequence = sequence; Kind = kind; ActorId = actorId; Json = json; }
    }

    /// <summary>Semantic gameplay data, not physics restart or fresh-inference replay. Owns its WAL/FULL connection.
    /// Checkpoints and entries are immutable identities; exact retries are idempotent, conflicting retries fail.
    /// Capacity exhaustion is explicit and never evicts owner memories or pretends a mutation was durable.</summary>
    public sealed class GameplayJournal : IDisposable
    {
        public const int MaxEntryBytes = 1024 * 1024;
        public const int MaxCheckpointBytes = 16 * 1024 * 1024;
        public const int MaxEntriesPerRun = 100000;
        public const long MaxStoredBytes = 256L * 1024 * 1024;
        private readonly SqliteProvider db;
        private static readonly string[] Schema = {
            "CREATE TABLE cg_gameplay_entry(run_id TEXT NOT NULL,generation INTEGER NOT NULL CHECK(generation>=0),sequence INTEGER NOT NULL CHECK(sequence>=0),kind TEXT NOT NULL,actor_id TEXT NOT NULL,json TEXT NOT NULL,PRIMARY KEY(run_id,generation,sequence))",
            "CREATE TABLE cg_gameplay_checkpoint(run_id TEXT NOT NULL,generation INTEGER NOT NULL CHECK(generation>=0),sequence INTEGER NOT NULL CHECK(sequence>=0),json TEXT NOT NULL,PRIMARY KEY(run_id,generation,sequence))",
            "CREATE TABLE cg_gameplay_budget(run_id TEXT PRIMARY KEY,entries INTEGER NOT NULL CHECK(entries>=0),bytes INTEGER NOT NULL CHECK(bytes>=0))"
        };
        public GameplayJournal(string databasePath) : this(RuntimePackage.LoadConfigured().OpenDatabase(databasePath)) { }
        public GameplayJournal(SqliteProvider provider)
        {
            db = provider ?? throw new ArgumentNullException(nameof(provider));
            try
            {
                SqliteRunStore.InitializeSchema(db);
                Transaction(() =>
                {
                    foreach (var statement in Schema)
                    {
                        var table = statement.Substring("CREATE TABLE ".Length).Split('(')[0];
                        var existing = db.Scalar("SELECT sql FROM sqlite_master WHERE type='table' AND name=?", table);
                        if (existing == null) db.Execute(statement);
                        else if (existing != statement) throw new InvalidOperationException("GAMEPLAY_SCHEMA_MISMATCH: " + table);
                    }
                });
            }
            catch { db.Dispose(); throw; }
        }
        public void Append(string runId, long generation, long sequence, string kind, string actorId, string json)
        {
            ValidateKey(runId, generation, sequence); Text(kind, nameof(kind), 128, false); Text(actorId, nameof(actorId), 256, true);
            var bytes = ValidateJson(json, MaxEntryBytes);
            Transaction(() => AppendCore(runId, generation, sequence, kind, actorId, json, bytes));
        }
        public void SaveCheckpoint(string runId, long generation, long sequence, string json)
        {
            ValidateKey(runId, generation, sequence); var bytes = ValidateJson(json, MaxCheckpointBytes);
            Transaction(() => CheckpointCore(runId, generation, sequence, json, bytes));
        }
        public void AppendAndCheckpoint(string runId, long generation, long sequence, string kind, string actorId, string json, string checkpointJson)
        {
            ValidateKey(runId, generation, sequence); Text(kind, nameof(kind), 128, false); Text(actorId, nameof(actorId), 256, true);
            var bytes = ValidateJson(json, MaxEntryBytes); var checkpointBytes = ValidateJson(checkpointJson, MaxCheckpointBytes);
            Transaction(() =>
            {
                AppendCore(runId, generation, sequence, kind, actorId, json, bytes);
                CheckpointCore(runId, generation, sequence, checkpointJson, checkpointBytes);
            });
        }
        public IReadOnlyList<GameplayJournalEntry> Read(string runId)
        {
            Text(runId, nameof(runId), 256, false);
            lock (db.Gate)
            {
                var rows = db.Query("SELECT generation,sequence,kind,actor_id,json FROM cg_gameplay_entry WHERE run_id=? ORDER BY generation,sequence", runId);
                var result = new List<GameplayJournalEntry>(rows.Count);
                foreach (var row in rows) result.Add(new GameplayJournalEntry(runId, Parse(row[0]), Parse(row[1]), row[2], row[3], row[4]));
                return result.AsReadOnly();
            }
        }
        public GameplayJournalEntry ReadCheckpoint(string runId)
        {
            Text(runId, nameof(runId), 256, false);
            lock (db.Gate)
            {
                var rows = db.Query("SELECT generation,sequence,json FROM cg_gameplay_checkpoint WHERE run_id=? ORDER BY generation DESC,sequence DESC LIMIT 1", runId);
                return rows.Count == 0 ? null : new GameplayJournalEntry(runId, Parse(rows[0][0]), Parse(rows[0][1]), "checkpoint", "", rows[0][2]);
            }
        }
        private void AppendCore(string runId, long generation, long sequence, string kind, string actorId, string json, int bytes)
        {
            var key = new[] { runId, N(generation), N(sequence) };
            var existing = db.Query("SELECT kind,actor_id,json FROM cg_gameplay_entry WHERE run_id=? AND generation=? AND sequence=?", key);
            if (existing.Count != 0)
            {
                if (existing[0][0] != kind || existing[0][1] != actorId || existing[0][2] != json) throw new InvalidOperationException("GAMEPLAY_ENTRY_CONFLICT");
                return;
            }
            var last = db.Query("SELECT generation,sequence FROM cg_gameplay_entry WHERE run_id=? ORDER BY generation DESC,sequence DESC LIMIT 1", runId);
            if (last.Count != 0 && Compare(generation, sequence, Parse(last[0][0]), Parse(last[0][1])) <= 0) throw new InvalidOperationException("GAMEPLAY_ENTRY_STALE");
            var checkpoint = ReadCheckpoint(runId);
            if (checkpoint != null && Compare(generation, sequence, checkpoint.Generation, checkpoint.Sequence) <= 0) throw new InvalidOperationException("GAMEPLAY_ENTRY_BEFORE_CHECKPOINT");
            Reserve(runId, bytes + Encoding.UTF8.GetByteCount(kind) + Encoding.UTF8.GetByteCount(actorId), true);
            db.Execute("INSERT INTO cg_gameplay_entry VALUES(?,?,?,?,?,?)", runId, N(generation), N(sequence), kind, actorId, json);
        }
        private void CheckpointCore(string runId, long generation, long sequence, string json, int bytes)
        {
            var existing = db.Scalar("SELECT json FROM cg_gameplay_checkpoint WHERE run_id=? AND generation=? AND sequence=?", runId, N(generation), N(sequence));
            if (existing != null)
            {
                if (existing != json) throw new InvalidOperationException("GAMEPLAY_CHECKPOINT_CONFLICT");
                return;
            }
            var checkpoint = ReadCheckpoint(runId);
            if (checkpoint != null && Compare(generation, sequence, checkpoint.Generation, checkpoint.Sequence) <= 0) throw new InvalidOperationException("GAMEPLAY_CHECKPOINT_STALE");
            // Initial state can be checkpointed at sequence zero. Other checkpoints must name a committed entry.
            if (sequence != 0 && db.Scalar("SELECT 1 FROM cg_gameplay_entry WHERE run_id=? AND generation=? AND sequence=?", runId, N(generation), N(sequence)) == null)
                throw new InvalidOperationException("GAMEPLAY_CHECKPOINT_UNCOMMITTED_SEQUENCE");
            Reserve(runId, bytes, false);
            db.Execute("INSERT INTO cg_gameplay_checkpoint VALUES(?,?,?,?)", runId, N(generation), N(sequence), json);
        }
        private void Reserve(string runId, int bytes, bool entry)
        {
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(runId) + 256); // Include key/index/row overhead in the capacity budget.
            var current = db.Query("SELECT entries,bytes FROM cg_gameplay_budget WHERE run_id=?", runId);
            var count = current.Count == 0 ? 0 : Parse(current[0][0]);
            if (entry && count >= MaxEntriesPerRun) throw new IOException("GAMEPLAY_JOURNAL_ENTRY_LIMIT");
            var total = Parse(db.Scalar("SELECT COALESCE(SUM(bytes),0) FROM cg_gameplay_budget"));
            if (total > MaxStoredBytes - bytes) throw new IOException("GAMEPLAY_JOURNAL_SIZE_LIMIT");
            db.Execute("INSERT INTO cg_gameplay_budget VALUES(?,?,?) ON CONFLICT(run_id) DO UPDATE SET entries=entries+excluded.entries,bytes=bytes+excluded.bytes", runId, entry ? "1" : "0", N(bytes));
        }
        private void Transaction(Action action)
        {
            lock (db.Gate)
            {
                db.VerifySettings(); var owned = false;
                try
                {
                    db.Execute("BEGIN IMMEDIATE"); owned = true;
                    action();
                    db.Execute("COMMIT"); owned = false;
                }
                finally { if (owned) db.RollbackIfActive(); }
            }
        }
        private static int Compare(long generation, long sequence, long otherGeneration, long otherSequence)
        { var order = generation.CompareTo(otherGeneration); return order == 0 ? sequence.CompareTo(otherSequence) : order; }
        private static long Parse(string value) => long.Parse(value, CultureInfo.InvariantCulture);
        private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static void ValidateKey(string runId, long generation, long sequence)
        {
            Text(runId, nameof(runId), 256, false);
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        }
        private static void Text(string value, string name, int limit, bool emptyAllowed)
        {
            if (value == null || (!emptyAllowed && string.IsNullOrWhiteSpace(value)) || value.IndexOf('\0') >= 0 || Encoding.UTF8.GetByteCount(value) > limit)
                throw new ArgumentException("Invalid bounded journal identifier.", name);
        }
        private static int ValidateJson(string json, int limit)
        {
            if (string.IsNullOrWhiteSpace(json) || json.IndexOf('\0') >= 0) throw new ArgumentException("JSON payload required.", nameof(json));
            var count = Encoding.UTF8.GetByteCount(json);
            if (count > limit) throw new ArgumentException("GAMEPLAY_JSON_SIZE_LIMIT", nameof(json));
            var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 64, MaxStringContentLength = limit, MaxArrayLength = limit, MaxBytesPerRead = 4096, MaxNameTableCharCount = limit };
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), quotas))
                while (reader.Read()) { }
            return count;
        }
        public void Dispose() { db.Dispose(); }
    }
}
