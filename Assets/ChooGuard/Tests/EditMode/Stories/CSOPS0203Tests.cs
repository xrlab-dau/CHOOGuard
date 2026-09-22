#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ChooGuard.Contracts;
using ChooGuard.Application;
using System.Threading.Tasks;
using ChooGuard.Persistence;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public class CSOPS0203Tests
    {
        // Controlled materializer fixture, NOT production authority/effect verification.
        private sealed class Fixture : ICommitMaterializer, IOutboxMaterializer
        {
            public string Path; public bool Reject; public CancellationTokenSource Cancel; public string JobOverride; public Action DuringValidation;
            public string ResolveDurableBlob(ContentReference reference) => Path;
            public long ReadGeneration(StableId runId, StableId jobId, ContentReference reference, byte[] bytes) => 7;
            public ValidatedCommit Validate(CommitBatch batch, StoredRunState prior, IReadOnlyList<byte[]> blobs)
            {
                if (Reject) throw new InvalidOperationException("FIXTURE_VALIDATION_REJECTED");
                if (Cancel != null) Cancel.Cancel();
                DuringValidation?.Invoke();
                return new ValidatedCommit(3, new[] { new StableId(JobOverride ?? "job-" + batch.Receipt.Key.IntentId.Value) });
            }
        }
        private static string Hash(byte[] bytes)
        { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static CommitBatch Batch(ContentReference blob, string intent = "i", string fingerprint = null, long revision = 0, long sequence = 3)
        {
            var run = new StableId("run");
            var receipt = new CommandReceipt(new ReceiptKey(run, new StableId("requester"), new StableId(intent)), fingerprint ?? Hash(Encoding.UTF8.GetBytes("fixture-meaning")), ReceiptStatus.ACCEPTED,
                "commit-" + intent, new Sequence(sequence), new[] { new TargetResult(new StableId("target"), TargetStatus.ACCEPTED, new Reason[0]) });
            return new CommitBatch(run, revision, blob, blob, receipt, blob, blob);
        }
        private sealed class Clock : IOutboxClock
        {
            public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.FromUnixTimeMilliseconds(1000000);
        }
        private sealed class Transport : IOutboxTransport
        {
            public Func<OutboxDelivery, OutboxDeliveryAck> Send;
            public Task<OutboxDeliveryAck> DeliverAsync(OutboxDelivery delivery, CancellationToken cancellation) => Task.FromResult(Send(delivery));
        }
        private sealed class RecordingOutboxStore : IOutboxStore
        {
            public OutboxDelivery Delivery;
            public bool AcknowledgeResult;
            public int AcknowledgeCalls;
            public OutboxDeliveryAck Acknowledgement;
            public CancellationToken AcknowledgeCancellation;
            public Task<OutboxDelivery> ClaimAsync(StableId runId, StableId ownerId, TimeSpan lease, CancellationToken cancellation)
                => Task.FromResult(Delivery);
            public Task<bool> AcknowledgeAsync(OutboxDeliveryAck acknowledgement, CancellationToken cancellation)
            {
                AcknowledgeCalls++;
                Acknowledgement = acknowledgement;
                AcknowledgeCancellation = cancellation;
                return Task.FromResult(AcknowledgeResult);
            }
        }
        private static OutboxDelivery DispatcherDelivery()
        {
            var identity = new OutboxDeliveryAck(new StableId("run"), new StableId("job"), new StableId("commit"),
                7, new StableId("attempt"), new StableId("owner"));
            return new OutboxDelivery(identity, new ContentReference(new StableId("blob"), 0, Hash(new byte[0])),
                DateTimeOffset.FromUnixTimeMilliseconds(1001000));
        }
        [TestCase("run")]
        [TestCase("job")]
        [TestCase("commit")]
        [TestCase("generation")]
        [TestCase("attempt")]
        [TestCase("owner")]
        public void DispatcherRejectsMismatchedAcknowledgementWithoutCallingStore(string changed)
        {
            var delivery = DispatcherDelivery(); var a = delivery.Identity;
            var wrong = new OutboxDeliveryAck(
                changed == "run" ? new StableId("wrong") : a.RunId,
                changed == "job" ? new StableId("wrong") : a.JobId,
                changed == "commit" ? new StableId("wrong") : a.CommitId,
                changed == "generation" ? a.Generation + 1 : a.Generation,
                changed == "attempt" ? new StableId("wrong") : a.AttemptId,
                changed == "owner" ? new StableId("wrong") : a.OwnerId);
            var store = new RecordingOutboxStore { Delivery = delivery, AcknowledgeResult = true };
            var sends = 0;
            var transport = new Transport { Send = d => { sends++; Assert.That(d, Is.SameAs(delivery)); return wrong; } };

            var result = new OutboxDispatcher(store, transport).DispatchAsync(a.RunId, a.OwnerId,
                TimeSpan.FromSeconds(1), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result, Is.False, changed);
            Assert.That(sends, Is.EqualTo(1));
            Assert.That(store.AcknowledgeCalls, Is.EqualTo(0), changed);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void DispatcherPassesMatchingAcknowledgementToStoreAndReturnsItsResult(bool accepted)
        {
            var delivery = DispatcherDelivery(); var a = delivery.Identity;
            // A separate matching DTO proves value matching, not reference identity.
            var acknowledgement = new OutboxDeliveryAck(a.RunId, a.JobId, a.CommitId, a.Generation, a.AttemptId, a.OwnerId);
            var store = new RecordingOutboxStore { Delivery = delivery, AcknowledgeResult = accepted };
            var sends = 0;
            var transport = new Transport { Send = d => { sends++; Assert.That(d, Is.SameAs(delivery)); return acknowledgement; } };
            using (var cancellation = new CancellationTokenSource())
            {
                var result = new OutboxDispatcher(store, transport).DispatchAsync(a.RunId, a.OwnerId,
                    TimeSpan.FromSeconds(1), cancellation.Token).GetAwaiter().GetResult();

                Assert.That(result, Is.EqualTo(accepted));
                Assert.That(sends, Is.EqualTo(1));
                Assert.That(store.AcknowledgeCalls, Is.EqualTo(1));
                Assert.That(store.Acknowledgement, Is.SameAs(acknowledgement));
                Assert.That(store.AcknowledgeCancellation, Is.EqualTo(cancellation.Token));
            }
        }
        [Test] public void DurableDeliveryMigrationRestartFencingAndFailures()
        {
            var root = Path.Combine(Path.GetTempPath(), "cg-delivery-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var database = Path.Combine(root, "run.db"); var file = Path.Combine(root, "blob");
            var bytes = Encoding.UTF8.GetBytes("explicit fixture outbox generation 7"); File.WriteAllBytes(file, bytes);
            var reference = new ContentReference(new StableId("blob"), 0, Hash(bytes));
            var fixture = new Fixture { Path = file }; var clock = new Clock();
            var run = new StableId("run"); var owner = new StableId("owner"); var lease = TimeSpan.FromSeconds(1);
            var batch = Batch(reference); OutboxDelivery first;
            try
            {
                using (var store = new SqliteRunStore(CSOPS0201Tests.Open(database), fixture, clock)) store.CommitAsync(batch, CancellationToken.None).GetAwaiter().GetResult();
                // Reconstruct the exact previous version without touching its committed rows.
                using (var db = CSOPS0201Tests.Open(database))
                {
                    // Fixture-only setup: provider SQL is intentionally not public production API.
                    var execute = typeof(SqliteProvider).GetMethod("Execute", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    execute.Invoke(db, new object[] { "DROP TABLE cg_delivery", new string[0] });
                    execute.Invoke(db, new object[] { "PRAGMA user_version=1", new string[0] });
                }
                using (var store = new SqliteRunStore(CSOPS0201Tests.Open(database), fixture, clock))
                {
                    Assert.That(store.CommitAsync(batch, CancellationToken.None).Result.Revision, Is.EqualTo(1));
                    first = store.ClaimAsync(run, owner, lease, CancellationToken.None).Result;
                    Assert.That(first.Identity.Generation, Is.EqualTo(7));
                    Assert.That(first.OutboxRef.Sha256, Is.EqualTo(reference.Sha256));
                    Assert.That(store.ClaimAsync(run, owner, lease, CancellationToken.None).Result, Is.Null);
                    using (var competing = new SqliteRunStore(CSOPS0201Tests.Open(database), fixture, clock))
                        Assert.That(competing.ClaimAsync(run, new StableId("other"), lease, CancellationToken.None).Result, Is.Null);
                    Assert.Throws<ArgumentOutOfRangeException>(() => store.ClaimAsync(run, owner, TimeSpan.FromMinutes(6), CancellationToken.None));
                }
                clock.UtcNow += lease;
                using (var store = new SqliteRunStore(CSOPS0201Tests.Open(database), fixture, clock))
                {
                    Assert.That(store.AcknowledgeAsync(first.Identity, CancellationToken.None).Result, Is.False);
                    var second = store.ClaimAsync(run, owner, lease, CancellationToken.None).Result;
                    Assert.That(second.Identity.AttemptId, Is.Not.EqualTo(first.Identity.AttemptId));
                    Assert.That(second.Identity.CommitId, Is.EqualTo(first.Identity.CommitId));
                    Assert.That(store.AcknowledgeAsync(first.Identity, CancellationToken.None).Result, Is.False);
                    var a = second.Identity;
                    foreach (var wrong in new[] {
                        new OutboxDeliveryAck(new StableId("wrong"), a.JobId, a.CommitId, a.Generation, a.AttemptId, a.OwnerId),
                        new OutboxDeliveryAck(a.RunId, new StableId("wrong"), a.CommitId, a.Generation, a.AttemptId, a.OwnerId),
                        new OutboxDeliveryAck(a.RunId, a.JobId, new StableId("wrong"), a.Generation, a.AttemptId, a.OwnerId),
                        new OutboxDeliveryAck(a.RunId, a.JobId, a.CommitId, 6, a.AttemptId, a.OwnerId),
                        new OutboxDeliveryAck(a.RunId, a.JobId, a.CommitId, a.Generation, new StableId("wrong"), a.OwnerId),
                        new OutboxDeliveryAck(a.RunId, a.JobId, a.CommitId, a.Generation, a.AttemptId, new StableId("wrong")) })
                        Assert.That(store.AcknowledgeAsync(wrong, CancellationToken.None).Result, Is.False);
                    using (var cancelled = new CancellationTokenSource())
                    {
                        cancelled.Cancel();
                        Assert.Throws<OperationCanceledException>(() => store.AcknowledgeAsync(a, cancelled.Token));
                        Assert.Throws<OperationCanceledException>(() => store.ClaimAsync(run, owner, lease, cancelled.Token));
                    }
                    Assert.That(store.AcknowledgeAsync(a, CancellationToken.None).Result, Is.True);
                    Assert.That(store.AcknowledgeAsync(a, CancellationToken.None).Result, Is.False);
                    clock.UtcNow += lease;
                    Assert.That(store.ClaimAsync(run, owner, lease, CancellationToken.None).Result, Is.Null);
                    store.CommitAsync(Batch(reference, "next", revision: 1, sequence: 6), CancellationToken.None).GetAwaiter().GetResult();
                    var transport = new Transport { Send = d => throw new IOException("transport failure") };
                    var dispatcher = new OutboxDispatcher(store, transport);
                    Assert.Throws<IOException>(() => dispatcher.DispatchAsync(run, owner, lease, CancellationToken.None).GetAwaiter().GetResult());
                    Assert.That(store.ClaimAsync(run, owner, lease, CancellationToken.None).Result, Is.Null);
                    clock.UtcNow += lease;
                    using (var cancel = new CancellationTokenSource())
                    {
                        transport.Send = d => { cancel.Cancel(); return d.Identity; };
                        Assert.Throws<OperationCanceledException>(() => dispatcher.DispatchAsync(run, owner, lease, cancel.Token).GetAwaiter().GetResult());
                    }
                    clock.UtcNow += lease;
                    transport.Send = d => null;
                    Assert.That(dispatcher.DispatchAsync(run, owner, lease, CancellationToken.None).Result, Is.False);
                    clock.UtcNow += lease;
                    transport.Send = d => d.Identity; // Explicit controlled remote acceptance fixture only.
                    Assert.That(dispatcher.DispatchAsync(run, owner, lease, CancellationToken.None).Result, Is.True);
                    Assert.That(store.ReadReceiptAsync(batch.Receipt.Key, CancellationToken.None).Result.Found, Is.True);
                }
                clock.UtcNow += lease;
                using (var store = new SqliteRunStore(CSOPS0201Tests.Open(database), fixture, clock))
                    Assert.That(store.ClaimAsync(run, owner, lease, CancellationToken.None).Result, Is.Null);
            }
            finally { Directory.Delete(root, true); }
        }
        [Test] public void FileCommitRetryConflictRollbackCancellationAndReopen()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cg-store-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var file = System.IO.Path.Combine(root, "blob"); var database = System.IO.Path.Combine(root, "run.db");
            var content = Encoding.UTF8.GetBytes("explicit fixture materialization input"); File.WriteAllBytes(file, content);
            var reference = new ContentReference(new StableId("blob"), 0, Hash(content)); var fixture = new Fixture { Path = file }; var batch = Batch(reference);
            try
            {
                using (var store = new SqliteRunStore(CSOPS0201Tests.Open(database), fixture))
                {
                    var first = store.CommitAsync(batch, CancellationToken.None).GetAwaiter().GetResult();
                    Assert.That(first.Revision, Is.EqualTo(1)); Assert.That(first.IntentReceipt.Sequence.Value.Value, Is.EqualTo(3));
                    Assert.That(first.DispatchableJobs.Count, Is.EqualTo(1));
                    Assert.That(store.CommitAsync(batch, CancellationToken.None).Result.Revision, Is.EqualTo(1));
                    Assert.Throws<InvalidOperationException>(() => store.CommitAsync(Batch(reference, fingerprint: Hash(Encoding.UTF8.GetBytes("changed"))), CancellationToken.None));
                    Assert.Throws<InvalidOperationException>(() => store.CommitAsync(Batch(reference, "stale"), CancellationToken.None));
                    fixture.Reject = true;
                    Assert.Throws<InvalidOperationException>(() => store.CommitAsync(Batch(reference, "rejected", revision: 1, sequence: 6), CancellationToken.None)); fixture.Reject = false;
                    Assert.That(store.ReadReceiptAsync(Batch(reference, "rejected").Receipt.Key, CancellationToken.None).Result.Found, Is.False);
                    using (var cancel = new CancellationTokenSource())
                    {
                        fixture.Cancel = cancel;
                        Assert.Throws<OperationCanceledException>(() => store.CommitAsync(Batch(reference, "cancelled", revision: 1, sequence: 6), cancel.Token)); fixture.Cancel = null;
                    }
                    File.WriteAllText(file, "tampered");
                    Assert.Throws<InvalidOperationException>(() => store.CommitAsync(Batch(reference, "badblob", revision: 1, sequence: 6), CancellationToken.None)); File.WriteAllBytes(file, content);
                    fixture.JobOverride = "job-i"; // Fail after run+receipt writes: outbox collision rolls back only this transaction.
                    Assert.Throws<InvalidOperationException>(() => store.CommitAsync(Batch(reference, "collision", revision: 1, sequence: 6), CancellationToken.None));
                    fixture.JobOverride = null;
                    Assert.That(store.ReadReceiptAsync(Batch(reference, "collision").Receipt.Key, CancellationToken.None).Result.Found, Is.False);
                    Assert.That(store.ReadReceiptAsync(batch.Receipt.Key, CancellationToken.None).Result.Found, Is.True);
                    // Reentrant BEGIN fails before ownership; must not roll back the outer transaction.
                    fixture.DuringValidation = () => Assert.Throws<InvalidOperationException>(() => store.CommitAsync(Batch(reference, "nested", revision: 1, sequence: 6), CancellationToken.None));
                    var second = store.CommitAsync(Batch(reference, "second", revision: 1, sequence: 6), CancellationToken.None).Result;
                    fixture.DuringValidation = null;
                    Assert.That(second.Revision, Is.EqualTo(2));
                }
                using (var store = new SqliteRunStore(CSOPS0201Tests.Open(database), fixture))
                {
                    Assert.That(store.ReadReceiptAsync(batch.Receipt.Key, CancellationToken.None).Result.Receipt.CommitId, Is.EqualTo("commit-i"));
                    var retry = store.CommitAsync(batch, CancellationToken.None).Result;
                    Assert.That(retry.Revision, Is.EqualTo(1)); Assert.That(retry.DispatchableJobs[0].Value, Is.EqualTo("job-i"));
                }
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
#endif
