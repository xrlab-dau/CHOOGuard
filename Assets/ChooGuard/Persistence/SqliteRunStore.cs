using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Contracts;

namespace ChooGuard.Persistence
{
    /// <summary>Trusted application adapter, required (no permissive default). Called under BEGIN IMMEDIATE.
    /// Must resolve the actual intent, recompute its semantic fingerprint, check authorization/readSet,
    /// decode all four verified blobs, and validate resource/effect/outbox consistency against prior state.
    /// Throw on any missing input or failed validation. Return actual decoded event count and job IDs.
    /// Blob durability (flush/atomic rename/local storage) remains the resolver's responsibility.</summary>
    public interface ICommitMaterializer
    {
        string ResolveDurableBlob(ContentReference reference);
        ValidatedCommit Validate(CommitBatch batch, StoredRunState prior, IReadOnlyList<byte[]> blobs);
    }
    /// <summary>Trusted decoder of the hash-verified committed outbox. Must require exactly the
    /// requested run/job and return its semantic generation, never a delivery retry counter.</summary>
    public interface IOutboxMaterializer
    {
        long ReadGeneration(StableId runId, StableId jobId, ContentReference outboxRef, byte[] verifiedBlob);
    }
    public sealed class ValidatedCommit
    {
        public long EventCount { get; }
        public IReadOnlyList<StableId> Jobs { get; }
        public ValidatedCommit(long eventCount, IEnumerable<StableId> jobs)
        {
            if (eventCount < 0) throw new ArgumentOutOfRangeException(nameof(eventCount));
            EventCount = eventCount;
            var copy = new List<StableId>(); var seen = new HashSet<StableId>();
            foreach (var job in jobs ?? throw new ArgumentNullException(nameof(jobs)))
            { if (string.IsNullOrEmpty(job.Value) || !seen.Add(job)) throw new ArgumentException("Invalid/duplicate job ID."); copy.Add(job); }
            Jobs = copy.AsReadOnly();
        }
    }
    public sealed class StoredRunState
    {
        public long Revision { get; }
        public long EventSequence { get; }
        public ContentReference Reservations { get; }
        public ContentReference Projection { get; }
        internal StoredRunState(long revision, long sequence, ContentReference reservations, ContentReference projection)
        { Revision = revision; EventSequence = sequence; Reservations = reservations; Projection = projection; }
    }

    /// <summary>Reference-backed atomic storage slice. No application adapter is supplied here;
    /// therefore this is not yet the operational authorization/reservation implementation.
    /// Owns its provider connection. Delivery acknowledgement does not apply simulation effects.</summary>
    public sealed class SqliteRunStore : IRunStore, IOutboxStore, IDisposable
    {
        private readonly SqliteProvider db;
        private readonly ICommitMaterializer materializer;
        private readonly IOutboxClock clock;
        private sealed class WallClock : IOutboxClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
        // Additive v1 -> v2 migration: existing run/commit/outbox rows are untouched.
        internal const string DeliverySchema = "CREATE TABLE cg_delivery(run_id TEXT NOT NULL, job_id TEXT NOT NULL, generation INTEGER NOT NULL CHECK(generation>=0), attempt_id TEXT NOT NULL, owner_id TEXT NOT NULL, lease_until INTEGER NOT NULL, acknowledged INTEGER NOT NULL CHECK(acknowledged IN(0,1)), PRIMARY KEY(run_id,job_id), FOREIGN KEY(run_id,job_id) REFERENCES cg_outbox(run_id,job_id))";
        private const int MaxBlobBytes = 16 * 1024 * 1024;
        // Kept identical to Schema.sql; no file/Unity resource lookup at runtime.
        internal static readonly string[] Schema = {
            "CREATE TABLE cg_run(run_id TEXT PRIMARY KEY, revision INTEGER NOT NULL CHECK(revision>=0), sequence INTEGER NOT NULL CHECK(sequence>=0), reservations TEXT NOT NULL, projection TEXT NOT NULL)",
            "CREATE TABLE cg_commit(run_id TEXT NOT NULL REFERENCES cg_run(run_id), requester_id TEXT NOT NULL, intent_id TEXT NOT NULL, fingerprint TEXT NOT NULL, commit_id TEXT NOT NULL UNIQUE, revision INTEGER NOT NULL, receipt TEXT NOT NULL, events TEXT NOT NULL, reservations TEXT NOT NULL, outbox TEXT NOT NULL, projection TEXT NOT NULL, PRIMARY KEY(run_id,requester_id,intent_id))",
            "CREATE TABLE cg_outbox(run_id TEXT NOT NULL REFERENCES cg_run(run_id), job_id TEXT NOT NULL, commit_id TEXT NOT NULL REFERENCES cg_commit(commit_id), PRIMARY KEY(run_id,job_id))"
        };
        public SqliteRunStore(SqliteProvider provider, ICommitMaterializer materializer, IOutboxClock clock = null)
        {
            db = provider ?? throw new ArgumentNullException(nameof(provider));
            this.materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
            this.clock = clock ?? new WallClock();
            InitializeSchema(db);
        }
        internal static void InitializeSchema(SqliteProvider db)
        {
            lock (db.Gate)
            {
                db.VerifySettings();
                var owned = false;
                try
                {
                    db.Execute("BEGIN IMMEDIATE"); owned = true;
                    var version = db.Scalar("PRAGMA user_version");
                    if (version == "0")
                    {
                        if (db.Scalar("SELECT count(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'") != "0")
                            throw new InvalidOperationException("UNRECOGNIZED_DATABASE");
                        foreach (var statement in Schema) db.Execute(statement);
                        db.Execute("PRAGMA user_version=1");
                    }
                    else if (version != "1" && version != "2") throw new InvalidOperationException("UNSUPPORTED_SCHEMA_VERSION");
                    foreach (var statement in Schema)
                    {
                        var table = statement.Substring("CREATE TABLE ".Length).Split('(')[0];
                        if (db.Scalar("SELECT sql FROM sqlite_master WHERE type='table' AND name=?", table) != statement)
                            throw new InvalidOperationException("SCHEMA_MISMATCH");
                    }
                    if (version != "2")
                    {
                        db.Execute(DeliverySchema);
                        db.Execute("PRAGMA user_version=2");
                    }
                    if (db.Scalar("SELECT sql FROM sqlite_master WHERE type='table' AND name='cg_delivery'") != DeliverySchema)
                        throw new InvalidOperationException("SCHEMA_MISMATCH");
                    db.Execute("COMMIT"); owned = false;
                }
                finally { if (owned) db.RollbackIfActive(); }
            }
        }
        public Task<CommitReceipt> CommitAsync(CommitBatch batch, CancellationToken cancellation)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            cancellation.ThrowIfCancellationRequested();
            lock (db.Gate)
            {
                cancellation.ThrowIfCancellationRequested(); db.VerifySettings();
                var owned = false;
                try
                {
                    db.Execute("BEGIN IMMEDIATE"); owned = true;
                    var existing = ReadCommit(batch.Receipt.Key);
                    if (existing != null)
                    {
                        if (existing.IntentReceipt.Fingerprint != batch.Receipt.Fingerprint)
                            throw new InvalidOperationException("INTENT_CONFLICT");
                        db.Execute("ROLLBACK"); owned = false;
                        return Task.FromResult(existing);
                    }
                    var state = ReadState(batch.RunId);
                    if (state.Revision != batch.ExpectedRevision) throw new InvalidOperationException("STALE_STATE");
                    var refs = new[] { batch.EventsRef, batch.ReservationsRef, batch.OutboxRef, batch.ProjectionRef };
                    var bytes = new List<byte[]>();
                    foreach (var reference in refs) { cancellation.ThrowIfCancellationRequested(); bytes.Add(ReadBlob(reference)); }
                    var validated = materializer.Validate(batch, state, bytes.AsReadOnly());
                    if (validated == null) throw new InvalidOperationException("COMMIT_VALIDATION_REQUIRED");
                    var sequence = checked(state.EventSequence + validated.EventCount);
                    var revision = checked(state.Revision + 1);
                    if (batch.Receipt.CommitId == null || !batch.Receipt.Sequence.HasValue || batch.Receipt.Sequence.Value.Value != sequence)
                        throw new InvalidOperationException("COMMITTED_SEQUENCE_MISMATCH");
                    if (batch.Receipt.Status != ReceiptStatus.ACCEPTED && (validated.EventCount != 0 || validated.Jobs.Count != 0))
                        throw new InvalidOperationException("REJECTED_EFFECTS");
                    var receipt = new CommitReceipt(batch.RunId, new StableId(batch.Receipt.CommitId), revision, batch.Receipt, validated.Jobs);
                    db.Execute("INSERT INTO cg_run VALUES(?,?,?,?,?) ON CONFLICT(run_id) DO UPDATE SET revision=excluded.revision,sequence=excluded.sequence,reservations=excluded.reservations,projection=excluded.projection",
                        batch.RunId.Value, N(revision), N(sequence), EncodeRef(batch.ReservationsRef), EncodeRef(batch.ProjectionRef));
                    db.Execute("INSERT INTO cg_commit VALUES(?,?,?,?,?,?,?,?,?,?,?)", batch.RunId.Value, batch.Receipt.Key.RequesterId.Value, batch.Receipt.Key.IntentId.Value,
                        batch.Receipt.Fingerprint, batch.Receipt.CommitId, N(revision), EncodeReceipt(batch.Receipt), EncodeRef(batch.EventsRef), EncodeRef(batch.ReservationsRef), EncodeRef(batch.OutboxRef), EncodeRef(batch.ProjectionRef));
                    foreach (var job in validated.Jobs) db.Execute("INSERT INTO cg_outbox VALUES(?,?,?)", batch.RunId.Value, job.Value, batch.Receipt.CommitId);
                    cancellation.ThrowIfCancellationRequested();
                    db.Execute("COMMIT"); owned = false;
                    // No cancellation check after commit: durable acceptance cannot be undone.
                    return Task.FromResult(receipt);
                }
                finally { if (owned) db.RollbackIfActive(); }
            }
        }
        public Task<OutboxDelivery> ClaimAsync(StableId runId, StableId ownerId, TimeSpan lease, CancellationToken cancellation)
        {
            if (string.IsNullOrEmpty(runId.Value) || string.IsNullOrEmpty(ownerId.Value)) throw new ArgumentException("Valid run and owner required.");
            if (lease < TimeSpan.FromMilliseconds(1) || lease > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(lease));
            cancellation.ThrowIfCancellationRequested();
            var decoder = materializer as IOutboxMaterializer;
            if (decoder == null) throw new InvalidOperationException("OUTBOX_VALIDATION_REQUIRED");
            lock (db.Gate)
            {
                cancellation.ThrowIfCancellationRequested(); db.VerifySettings();
                var owned = false;
                try
                {
                    db.Execute("BEGIN IMMEDIATE"); owned = true;
                    var now = clock.UtcNow.ToUnixTimeMilliseconds();
                    var rows = db.Query("SELECT o.job_id,o.commit_id,c.outbox,d.generation FROM cg_outbox o JOIN cg_commit c ON c.commit_id=o.commit_id AND c.run_id=o.run_id LEFT JOIN cg_delivery d ON d.run_id=o.run_id AND d.job_id=o.job_id WHERE o.run_id=? AND (d.job_id IS NULL OR (d.acknowledged=0 AND d.lease_until<=?)) ORDER BY c.revision,o.job_id LIMIT 1", runId.Value, N(now));
                    if (rows.Count == 0) { db.Execute("ROLLBACK"); owned = false; return Task.FromResult<OutboxDelivery>(null); }
                    var row = rows[0]; var job = new StableId(row[0]); var reference = DecodeRef(row[2]);
                    var generation = decoder.ReadGeneration(runId, job, reference, ReadBlob(reference));
                    if (generation < 0 || (row[3] != null && generation != long.Parse(row[3], CultureInfo.InvariantCulture)))
                        throw new InvalidOperationException("OUTBOX_GENERATION_MISMATCH");
                    var attempt = new StableId("attempt-" + Guid.NewGuid().ToString("N"));
                    var expires = checked(clock.UtcNow.ToUnixTimeMilliseconds() + (long)lease.TotalMilliseconds);
                    var delivery = new OutboxDelivery(new OutboxDeliveryAck(runId, job, new StableId(row[1]), generation, attempt, ownerId), reference, DateTimeOffset.FromUnixTimeMilliseconds(expires));
                    db.Execute("INSERT INTO cg_delivery VALUES(?,?,?,?,?,?,0) ON CONFLICT(run_id,job_id) DO UPDATE SET attempt_id=excluded.attempt_id,owner_id=excluded.owner_id,lease_until=excluded.lease_until", runId.Value, job.Value, N(generation), attempt.Value, ownerId.Value, N(expires));
                    cancellation.ThrowIfCancellationRequested();
                    db.Execute("COMMIT"); owned = false;
                    return Task.FromResult(delivery);
                }
                finally { if (owned) db.RollbackIfActive(); }
            }
        }
        public Task<bool> AcknowledgeAsync(OutboxDeliveryAck acknowledgement, CancellationToken cancellation)
        {
            if (acknowledgement == null) throw new ArgumentNullException(nameof(acknowledgement));
            cancellation.ThrowIfCancellationRequested();
            lock (db.Gate)
            {
                cancellation.ThrowIfCancellationRequested(); db.VerifySettings();
                var owned = false;
                try
                {
                    db.Execute("BEGIN IMMEDIATE"); owned = true;
                    var a = acknowledgement;
                    db.Execute("UPDATE cg_delivery SET acknowledged=1 WHERE run_id=? AND job_id=? AND generation=? AND attempt_id=? AND owner_id=? AND acknowledged=0 AND lease_until>? AND EXISTS(SELECT 1 FROM cg_outbox o WHERE o.run_id=cg_delivery.run_id AND o.job_id=cg_delivery.job_id AND o.commit_id=?)", a.RunId.Value, a.JobId.Value, N(a.Generation), a.AttemptId.Value, a.OwnerId.Value, N(clock.UtcNow.ToUnixTimeMilliseconds()), a.CommitId.Value);
                    var changed = db.Scalar("SELECT changes()") == "1";
                    cancellation.ThrowIfCancellationRequested();
                    db.Execute("COMMIT"); owned = false;
                    return Task.FromResult(changed);
                }
                finally { if (owned) db.RollbackIfActive(); }
            }
        }
        public Task<ReceiptLookup> ReadReceiptAsync(ReceiptKey key, CancellationToken cancellation)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            cancellation.ThrowIfCancellationRequested();
            lock (db.Gate) { var stored = ReadCommit(key); return Task.FromResult(new ReceiptLookup(stored != null, stored?.IntentReceipt)); }
        }
        private StoredRunState ReadState(StableId run)
        {
            var rows = db.Query("SELECT revision,sequence,reservations,projection FROM cg_run WHERE run_id=?", run.Value);
            return rows.Count == 0 ? new StoredRunState(0, 0, null, null) : new StoredRunState(long.Parse(rows[0][0], CultureInfo.InvariantCulture), long.Parse(rows[0][1], CultureInfo.InvariantCulture), DecodeRef(rows[0][2]), DecodeRef(rows[0][3]));
        }
        private CommitReceipt ReadCommit(ReceiptKey key)
        {
            var rows = db.Query("SELECT commit_id,revision,receipt FROM cg_commit WHERE run_id=? AND requester_id=? AND intent_id=?", key.RunId.Value, key.RequesterId.Value, key.IntentId.Value);
            if (rows.Count == 0) return null;
            var row = rows[0]; var jobs = new List<StableId>();
            foreach (var job in db.Query("SELECT job_id FROM cg_outbox WHERE run_id=? AND commit_id=? ORDER BY job_id", key.RunId.Value, row[0])) jobs.Add(new StableId(job[0]));
            return new CommitReceipt(key.RunId, new StableId(row[0]), long.Parse(row[1], CultureInfo.InvariantCulture), DecodeReceipt(key, row[2]), jobs);
        }
        private byte[] ReadBlob(ContentReference reference)
        {
            var path = materializer.ResolveDurableBlob(reference);
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new InvalidOperationException("DURABLE_BLOB_REQUIRED");
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > MaxBlobBytes) throw new InvalidOperationException("BLOB_TOO_LARGE");
                using (var copy = new MemoryStream())
                {
                    var buffer = new byte[8192]; int count;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
                    { if (copy.Length + count > MaxBlobBytes) throw new InvalidOperationException("BLOB_TOO_LARGE"); copy.Write(buffer, 0, count); }
                    var bytes = copy.ToArray();
                    using (var hash = SHA256.Create())
                        if (SqliteProvider.Hex(hash.ComputeHash(bytes)) != reference.Sha256) throw new InvalidOperationException("BLOB_HASH_MISMATCH");
                    return bytes;
                }
            }
        }
        private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static void WriteRef(BinaryWriter writer, ContentReference r) { writer.Write(r.Id.Value); writer.Write(r.Revision); writer.Write(r.Sha256); }
        private static ContentReference ReadRef(BinaryReader reader) => new ContentReference(new StableId(reader.ReadString()), reader.ReadInt64(), reader.ReadString());
        private static string EncodeRef(ContentReference r) => Encode(w => WriteRef(w, r));
        private static ContentReference DecodeRef(string value) => Decode(value, ReadRef);
        private static string Encode(Action<BinaryWriter> write)
        { using (var stream = new MemoryStream()) { using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) write(writer); return Convert.ToBase64String(stream.ToArray()); } }
        private static T Decode<T>(string value, Func<BinaryReader, T> read)
        { using (var stream = new MemoryStream(Convert.FromBase64String(value))) using (var reader = new BinaryReader(stream)) { var result = read(reader); if (stream.Position != stream.Length) throw new InvalidDataException("Trailing stored data."); return result; } }
        private static string EncodeReceipt(CommandReceipt r) => Encode(w =>
        {
            w.Write(r.Fingerprint); w.Write((int)r.Status); w.Write(r.CommitId); w.Write(r.Sequence.Value.Value); w.Write(r.TargetResults.Count);
            foreach (var target in r.TargetResults)
            {
                w.Write(target.TargetId.Value); w.Write((int)target.Status); w.Write(target.Reasons.Count);
                foreach (var reason in target.Reasons)
                { w.Write(reason.Code.Value); w.Write((int)reason.Axis); w.Write(reason.SubjectId.Value); w.Write(reason.Message); w.Write(reason.EvidenceRefs.Count); foreach (var e in reason.EvidenceRefs) WriteRef(w, e); }
            }
        });
        private static int Count(BinaryReader reader) { var n = reader.ReadInt32(); if (n < 0 || n > 4096) throw new InvalidDataException("Invalid stored collection count."); return n; }
        private static CommandReceipt DecodeReceipt(ReceiptKey key, string value) => Decode(value, r =>
        {
            var fingerprint = r.ReadString(); var status = (ReceiptStatus)r.ReadInt32(); var commit = r.ReadString(); var sequence = new Sequence(r.ReadInt64());
            var targets = new List<TargetResult>(); var count = Count(r);
            for (var i = 0; i < count; i++)
            {
                var id = new StableId(r.ReadString()); var targetStatus = (TargetStatus)r.ReadInt32(); var reasons = new List<Reason>(); var reasonCount = Count(r);
                for (var j = 0; j < reasonCount; j++)
                {
                    var code = new StableId(r.ReadString()); var axis = (ReasonAxis)r.ReadInt32(); var subject = new StableId(r.ReadString()); var message = r.ReadString();
                    var evidence = new List<ContentReference>(); var evidenceCount = Count(r);
                    for (var k = 0; k < evidenceCount; k++) evidence.Add(ReadRef(r));
                    reasons.Add(new Reason(code, axis, subject, evidence, message));
                }
                targets.Add(new TargetResult(id, targetStatus, reasons));
            }
            return new CommandReceipt(key, fingerprint, status, commit, sequence, targets);
        });
        public void Dispose() { db.Dispose(); }
    }
}
