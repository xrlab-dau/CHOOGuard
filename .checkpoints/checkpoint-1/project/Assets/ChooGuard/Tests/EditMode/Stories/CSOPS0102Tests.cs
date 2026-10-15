#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.Application;
using ChooGuard.Contracts;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    // 실제 Submit/영속화가 아니라 외부 guard 증거를 소비하는 preflight 시험이다.
    public class CSOPS0102Tests
    {
        private static StableId Id(string value) => new StableId(value);
        private static EntityRevision Rev(string id, long revision = 7) => new EntityRevision(Id(id), revision);
        private static TargetResult Guard(string target = "target", string code = null, ReasonAxis axis = ReasonAxis.resources) =>
            new TargetResult(Id(target), code == null ? TargetStatus.ACCEPTED : TargetStatus.REJECTED,
                code == null ? new Reason[0] : new[] { new Reason(Id(code), axis, Id(target), new ContentReference[0], "현재 조건 미충족") });
        private static CommandIntent Intent(EntityRevision[] reads = null, string[] targets = null, string run = "run") =>
            new CommandIntent(new ReceiptKey(Id(run), Id("requester"), Id("intent")), Id("agency"),
                new[] { Id("team") }, Id("action"), (targets ?? new[] { "target" }).Select(Id),
                reads ?? new[] { Rev("run"), Rev("target"), Rev("aux") },
                new ContentReference(Id("payload"), 0, new string('a', 64)),
                new UtcTimestamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        private static CommandStateSnapshot State(long revision = 7, EntityRevision[] entities = null, TargetResult[] guards = null) =>
            new CommandStateSnapshot(Id("run"), revision, entities ?? new[] { Rev("run", revision), Rev("target"), Rev("aux") },
                guards ?? new[] { Guard() });
        private static void HasReason(CommandCheckResult result, string code)
        {
            Assert.That(result.CanProceed, Is.False);
            Assert.That(result.Reasons.Concat(result.TargetResults.SelectMany(x => x.Reasons)).Any(x => x.Code.Value == code), Is.True, code);
        }

        [Test]
        public void Preview_IsPureAndReturnsOnlyQueuedCandidates()
        {
            var intent = Intent(); var state = State(); var dispatcher = new CommandDispatcher();
            var reads = intent.ReadSet.ToArray(); var entities = state.EntityRevisions.ToArray(); var guards = state.TargetResults.ToArray();
            var result = dispatcher.Preview(intent, state);
            Assert.That(result.Status, Is.EqualTo(CommandCheckStatus.PreflightReady));
            Assert.That(result.CanProceed, Is.True);
            Assert.That(result.CurrentRevision, Is.EqualTo(7));
            Assert.That(result.TargetResults.Single().Status, Is.EqualTo(TargetStatus.QUEUED));
            CollectionAssert.AreEqual(reads, intent.ReadSet);
            CollectionAssert.AreEqual(entities, state.EntityRevisions);
            CollectionAssert.AreEqual(guards, state.TargetResults);
            Assert.That(state.Revision, Is.EqualTo(7));
            Assert.That(intent.Key.IntentId, Is.EqualTo(Id("intent")));
            Assert.That(dispatcher.Preview(intent, state).CanProceed, Is.True);
        }

        [TestCase("RESOURCE_UNAVAILABLE", ReasonAxis.resources)]
        [TestCase("AUTHORITY_DENIED", ReasonAxis.authority)]
        public void Revalidate_UsesNewGuardEvidenceRatherThanPreview(string code, ReasonAxis axis)
        {
            var dispatcher = new CommandDispatcher(); var intent = Intent();
            Assert.That(dispatcher.Preview(intent, State()).CanProceed, Is.True);
            var failed = State(guards: new[] { Guard(code: code, axis: axis) });
            HasReason(dispatcher.RevalidateForSubmit(intent, failed), code);
            HasReason(dispatcher.Preview(intent, failed), code);
        }

        [TestCase("run")]
        [TestCase("target")]
        [TestCase("aux")]
        public void Revalidate_RejectsEveryStaleRead(string changed)
        {
            var entities = new[] { Rev("run", changed == "run" ? 8 : 7), Rev("target", changed == "target" ? 8 : 7), Rev("aux", changed == "aux" ? 8 : 7) };
            HasReason(new CommandDispatcher().RevalidateForSubmit(Intent(), State(changed == "run" ? 8 : 7, entities)), "STALE_STATE");
        }

        [Test]
        public void AuthoritativeRevision_CannotBeBypassedByMatchingRunEntity()
        {
            HasReason(new CommandDispatcher().Preview(Intent(), State(8, new[] { Rev("run"), Rev("target"), Rev("aux") })), "STALE_STATE");
        }

        [Test]
        public void WrongRunAndUnrequestedGuard_AreRejected()
        {
            HasReason(new CommandDispatcher().Preview(Intent(run: "other"), State()), "RUN_MISMATCH");
            HasReason(new CommandDispatcher().Preview(Intent(), State(guards: new[] { Guard(), Guard("other") })), "UNEXPECTED_TARGET_RESULT");
        }

        [TestCase("run")]
        [TestCase("target")]
        public void RequiredReadCoverage_CannotBeOmitted(string missing)
        {
            HasReason(new CommandDispatcher().Preview(Intent(new[] { Rev("run"), Rev("target"), Rev("aux") }.Where(x => x.EntityId.Value != missing).ToArray()), State()), "MISSING_READ_SET");
        }

        [TestCase("run")]
        [TestCase("target")]
        [TestCase("aux")]
        public void MissingCurrentEntity_IsExplicitFailure(string missing)
        {
            HasReason(new CommandDispatcher().Preview(Intent(), State(entities: new[] { Rev("run"), Rev("target"), Rev("aux") }.Where(x => x.EntityId.Value != missing).ToArray())), "MISSING_ENTITY");
        }

        [Test]
        public void MissingGuardAndDuplicateRead_AreExplicitFailures()
        {
            HasReason(new CommandDispatcher().Preview(Intent(), State(guards: new TargetResult[0])), "MISSING_TARGET_RESULT");
            HasReason(new CommandDispatcher().Preview(Intent(new[] { Rev("run"), Rev("target"), Rev("target") }), State()), "DUPLICATE_READ_SET");
        }

        [Test]
        public void MixedTargets_AreOrdinalAndPreserveFailureAxis()
        {
            var intent = Intent(new[] { Rev("run"), Rev("z"), Rev("a") }, new[] { "z", "a" });
            var state = State(entities: new[] { Rev("run"), Rev("z"), Rev("a") }, guards: new[] { Guard("z"), Guard("a", "AUTHORITY_DENIED", ReasonAxis.authority) });
            var result = new CommandDispatcher().Preview(intent, state);
            Assert.That(result.CanProceed, Is.False);
            CollectionAssert.AreEqual(new[] { "a", "z" }, result.TargetResults.Select(x => x.TargetId.Value));
            Assert.That(result.TargetResults[0].Status, Is.EqualTo(TargetStatus.REJECTED));
            Assert.That(result.TargetResults[0].Reasons.Single().Axis, Is.EqualTo(ReasonAxis.authority));
            Assert.That(result.TargetResults[1].Status, Is.EqualTo(TargetStatus.QUEUED));
        }

        [Test]
        public void MaximumRevision_IsComparedWithoutIncrementOrOverflow()
        {
            var result = new CommandDispatcher().Preview(Intent(new[] { Rev("run", long.MaxValue), Rev("target") }), State(long.MaxValue));
            Assert.That(result.CanProceed, Is.True);
            Assert.That(result.CurrentRevision, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void SnapshotAndResult_OwnReadOnlyCollections()
        {
            var entities = new[] { Rev("run"), Rev("target"), Rev("aux") }; var guards = new[] { Guard() };
            var snapshot = State(entities: entities, guards: guards);
            entities[0] = Rev("other"); guards[0] = Guard(code: "RESOURCE_UNAVAILABLE");
            var result = new CommandDispatcher().Preview(Intent(), snapshot);
            Assert.That(result.CanProceed, Is.True);
            Assert.Throws<NotSupportedException>(() => ((IList<EntityRevision>)snapshot.EntityRevisions)[0] = Rev("other"));
            Assert.Throws<NotSupportedException>(() => ((IList<TargetResult>)snapshot.TargetResults).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<EntityRevision>)result.ReadSet).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<TargetResult>)result.TargetResults).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<Reason>)result.Reasons).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<Reason>)result.TargetResults[0].Reasons).Clear());
        }

        [Test]
        public void InvalidSnapshotAndNullCalls_FailClosed()
        {
            Assert.Throws<ArgumentException>(() => State(entities: new[] { Rev("run"), Rev("run") }));
            Assert.Throws<ArgumentException>(() => State(guards: new[] { Guard(), Guard() }));
            Assert.Throws<ArgumentException>(() => State(entities: new EntityRevision[] { null }));
            Assert.Throws<ArgumentException>(() => State(guards: new TargetResult[] { null }));
            Assert.Throws<ArgumentException>(() => new CommandStateSnapshot(default(StableId), 0, new EntityRevision[0], new TargetResult[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => State(-1));
            Assert.Throws<ArgumentNullException>(() => new CommandStateSnapshot(Id("run"), 0, null, new TargetResult[0]));
            Assert.Throws<ArgumentNullException>(() => new CommandStateSnapshot(Id("run"), 0, new EntityRevision[0], null));
            Assert.Throws<ArgumentNullException>(() => new CommandDispatcher().Preview(null, State()));
            Assert.Throws<ArgumentNullException>(() => new CommandDispatcher().RevalidateForSubmit(Intent(), null));
            Assert.Throws<ArgumentException>(() => new EntityRevision(default(StableId), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rev("target", -1));
        }

        [Test]
        public void RejectedGuardWithoutReason_RemainsRejectedWithExplicitReason()
        {
            var rejected = new TargetResult(Id("target"), TargetStatus.REJECTED, new Reason[0]);
            HasReason(new CommandDispatcher().Preview(Intent(), State(guards: new[] { rejected })), "GUARD_REJECTED");
        }
    }
}
#endif
