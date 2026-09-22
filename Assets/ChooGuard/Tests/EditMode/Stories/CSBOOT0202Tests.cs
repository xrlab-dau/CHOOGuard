#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Contracts;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public class CSBOOT0202Tests
    {
        private static StableId Id(string value) => new StableId(value);
        private static ReceiptKey Key(string requester = "user") => new ReceiptKey(Id("run"), Id(requester), Id("intent"));
        private static ContentReference Ref() => new ContentReference(Id("content"), 0, new string('a', 64));
        private static TargetResult Target() => new TargetResult(Id("target"), TargetStatus.REJECTED, new Reason[0]);
        private static CommandReceipt Receipt() => new CommandReceipt(Key(), new string('b', 64), ReceiptStatus.REJECTED, null, null, new[] { Target() });

        // Written before the additional DTO implementation. Execution is NOT_RUN (Unity/compile prohibited).
        [Test]
        public void AllEighteenMessagesExposeImmutableTypedProperties()
        {
            var types = new[] { typeof(ActivityInterval), typeof(BranchRequest), typeof(Checkpoint), typeof(CommandIntent),
                typeof(CommandReceipt), typeof(CommitBatch), typeof(CommitReceipt), typeof(ComparisonReport), typeof(EvidenceRecord),
                typeof(FieldBatch), typeof(PreviewResult), typeof(ProjectionQuery), typeof(ReceiptKey), typeof(ReceiptLookup),
                typeof(ScriptIR), typeof(SessionProjection), typeof(SimulationJob), typeof(WorkerCapability) };
            Assert.That(types.Distinct().Count(), Is.EqualTo(18));
            foreach (var type in types)
            {
                Assert.That(type.IsSealed, Is.True, type.Name);
                Assert.That(type.GetProperties().All(p => !p.CanWrite), Is.True, type.Name);
                Assert.That(type.GetFields().Length, Is.Zero, type.Name);
            }
        }

        [Test]
        public void CommitEnvelopeRejectsForeignRunOrCommitWithoutConflatingRevisionAndSequence()
        {
            var accepted = new CommandReceipt(Key(), new string('b', 64), ReceiptStatus.ACCEPTED, "commit", new Sequence(42), new[] { Target() });
            var jobs = new[] { Id("job") };
            var commit = new CommitReceipt(Id("run"), Id("commit"), 3, accepted, jobs);
            jobs[0] = Id("changed");
            Assert.That(commit.DispatchableJobs[0], Is.EqualTo(Id("job")));
            Assert.That(commit.IntentReceipt.Sequence.Value.Value, Is.EqualTo(42));
            Assert.Throws<ArgumentException>(() => new CommitReceipt(Id("other"), Id("commit"), 3, accepted, new StableId[0]));
            Assert.Throws<ArgumentException>(() => new CommitReceipt(Id("run"), Id("other"), 3, accepted, new StableId[0]));
            Assert.Throws<ArgumentException>(() => new CommitBatch(Id("other"), 0, Ref(), Ref(), accepted, Ref(), Ref()));
        }

        [Test]
        public void ActivityUsesMonotonicAxisAndPreservesCensoring()
        {
            var open = new ActivityInterval(Id("person"), Id("case"), Id("interval"), new MonotonicTimestamp(9), null,
                ActivityCategory.AUTHORING, false, Ref(), ActivityCompleteness.CENSORED);
            Assert.That(open.EndMonoUs, Is.Null);
            Assert.Throws<ArgumentException>(() => new ActivityInterval(Id("person"), Id("case"), Id("interval"), new MonotonicTimestamp(9),
                new MonotonicTimestamp(8), ActivityCategory.AUTHORING, false, Ref(), ActivityCompleteness.COMPLETE));
            Assert.Throws<ArgumentException>(() => new ActivityInterval(Id("person"), Id("case"), Id("interval"), new MonotonicTimestamp(9),
                null, ActivityCategory.AUTHORING, false, Ref(), ActivityCompleteness.COMPLETE));
        }

        [Test]
        public void CheckpointRequiresExactWorkerSetAndCommonCut()
        {
            var worker = new CheckpointWorkerState(Id("worker"), new Sequence(2), new SimTick(3), new string('a', 64),
                CheckpointRestartMode.FULL_STATE, Ref(), Ref());
            var states = new[] { worker };
            var checkpoint = new Checkpoint(Id("checkpoint"), Id("run"), new Sequence(2), new SimTick(3), Ref(), Ref(), Ref(), Ref(), new[] { Id("worker") }, states);
            states[0] = null;
            Assert.That(checkpoint.WorkerStates[0], Is.SameAs(worker));
            Assert.Throws<ArgumentException>(() => new Checkpoint(Id("cp"), Id("run"), new Sequence(2), new SimTick(4), Ref(), Ref(), Ref(), Ref(), new[] { Id("worker") }, new[] { worker }));
            Assert.Throws<ArgumentException>(() => new Checkpoint(Id("cp"), Id("run"), new Sequence(2), new SimTick(3), Ref(), Ref(), Ref(), Ref(), new[] { Id("worker") }, new CheckpointWorkerState[0]));
            Assert.Throws<ArgumentException>(() => new Checkpoint(Id("cp"), Id("run"), new Sequence(2), new SimTick(3), Ref(), Ref(), Ref(), Ref(), new[] { Id("worker") }, new[] { worker, worker }));
        }

        [Test]
        public void SimulationIntervalsOwnershipAndWireUnitVocabularyAreExplicit()
        {
            var output = new FieldOutputContract(Id("field"), Id("worker"), FieldUnit.KilogramsPerCubicMetre, Id("frame"));
            var job = new SimulationJob(Id("job"), Id("run"), 0, Id("attempt"), Id("worker"), Ref(), new string('a', 64), 0, new SimTick(0), new SimTick(long.MaxValue), Ref(), output);
            Assert.That(job.OutputContract.Unit, Is.EqualTo(FieldUnit.KilogramsPerCubicMetre));
            Assert.Throws<ArgumentException>(() => new SimulationJob(Id("job"), Id("run"), 0, Id("attempt"), Id("other"), Ref(), new string('a', 64), 0, new SimTick(0), new SimTick(1), Ref(), output));
            Assert.Throws<ArgumentException>(() => new FieldBatch(Id("job"), Id("run"), 0, Id("worker"), Ref(), new string('a', 64), 0, new SimTick(2), new SimTick(1), Id("worker"), FieldUnit.Metre, Id("frame"), Ref(), FieldStatus.FAILED, Id("field")));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FieldOutputContract(Id("field"), Id("worker"), (FieldUnit)999, Id("frame")));
        }

        [Test]
        public void EvidenceCannotTurnNotRunIntoExecutedSuccess()
        {
            var record = new EvidenceRecord(Id("evidence"), Ref(), Id("test"), new string('a', 64), Ref(), new[] { Ref() }, new ContentReference[0],
                EvidenceResult.NOT_RUN, null, Id("executor"), null, "DTO boundary only", "Unity execution prohibited");
            Assert.That(record.ExecutedAt, Is.Null);
            Assert.Throws<ArgumentException>(() => new EvidenceRecord(Id("e"), Ref(), Id("test"), new string('a', 64), Ref(), new[] { Ref() }, new ContentReference[0],
                EvidenceResult.NOT_RUN, new UtcTimestamp(DateTime.UtcNow), Id("executor"), null, "scope", "not run"));
            Assert.Throws<ArgumentException>(() => new EvidenceRecord(Id("e"), Ref(), Id("test"), new string('a', 64), Ref(), new[] { Ref() }, new ContentReference[0],
                EvidenceResult.PASSED_WITH_SCOPE, null, Id("executor"), null, "scope", null));
        }

        [Test]
        public void RemainingMessagesRetainReferencesAndCopyNestedArrays()
        {
            var branch = new BranchRequest(Id("intent"), Id("parent"), Ref(), Id("child"), Ref(), BranchKind.EXACT_FORK);
            Assert.That(branch.CheckpointRef, Is.Not.Null);
            Assert.Throws<ArgumentException>(() => new BranchRequest(Id("intent"), Id("parent"), Ref(), Id("parent"), Ref(), BranchKind.EXACT_FORK));
            var fields = new[] { Id("field") };
            var capability = new WorkerCapability(Id("worker"), Ref(), "cg-worker/1", fields, WorkerTimeMode.STEP, WorkerRestartMode.NONE, Ref(), new SimTick(0));
            fields[0] = Id("changed");
            Assert.That(capability.Fields[0], Is.EqualTo(Id("field")));
            Assert.Throws<ArgumentException>(() => new WorkerCapability(Id("worker"), Ref(), "cg-worker/2", fields, WorkerTimeMode.STEP, WorkerRestartMode.NONE, Ref(), new SimTick(0)));
            var refs = new[] { Ref() };
            var step = new ScriptStep(Id("step"), Id("role"), Ref(), "hypothesis", ScriptClaimType.HYPOTHESIS, refs);
            var steps = new[] { step };
            var script = new ScriptIR(Id("script"), 0, Ref(), Ref(), steps, null);
            refs[0] = null; steps[0] = null;
            Assert.That(script.Steps[0].EvidenceRefs[0], Is.Not.Null);
            var runs = new[] { Ref(), Ref() };
            var comparison = new ComparisonReport(Id("comparison"), runs, Ref(), new StableId[0], ComparisonStatus.INCONCLUSIVE, Ref(), Ref(), Ref(), null, null);
            runs[0] = null;
            Assert.That(comparison.RunRefs[0], Is.Not.Null);
            Assert.Throws<ArgumentException>(() => new ComparisonReport(Id("c"), new[] { Ref() }, Ref(), new StableId[0], ComparisonStatus.COMPARABLE, Ref(), Ref(), Ref(), null, null));
            Assert.Throws<ArgumentException>(() => new WorkerCapability(default(StableId), Ref(), "cg-worker/1", fields, WorkerTimeMode.STEP, WorkerRestartMode.NONE, Ref(), new SimTick(0)));
        }

        [Test]
        public void CollectionLimitsAndNullableValuesAreNotSilentlyCoerced()
        {
            var references = Enumerable.Repeat(Ref(), 4096).ToArray();
            Assert.That(new ComparisonReport(Id("c"), references, Ref(), new StableId[0], ComparisonStatus.INCOMPARABLE,
                Ref(), Ref(), Ref(), null, null).RunRefs.Count, Is.EqualTo(4096));
            Assert.Throws<ArgumentException>(() => new ComparisonReport(Id("c"), Enumerable.Repeat(Ref(), 4097), Ref(), new StableId[0],
                ComparisonStatus.INCOMPARABLE, Ref(), Ref(), Ref(), null, null));
            Assert.Throws<ArgumentException>(() => new WorkerCapability(Id("w"), Ref(), "cg-worker/1", new StableId[0],
                WorkerTimeMode.BATCH, WorkerRestartMode.FROM_ORIGIN, Ref(), new SimTick(0)));
            Assert.Throws<ArgumentException>(() => new ScriptStep(Id("s"), Id("r"), Ref(), "claim", ScriptClaimType.OBSERVED_IN_RUN, new ContentReference[0]));
            Assert.Throws<ArgumentException>(() => new EvidenceRecord(Id("e"), Ref(), Id("t"), new string('a', 64), Ref(), new[] { Ref() },
                new ContentReference[0], EvidenceResult.NOT_RUN, null, Id("executor"), default(StableId), "scope", "not run"));
        }

        [Test]
        public void PortSignaturesUseExactK02TypesAndCancellation()
        {
            var names = new[] { "PreviewAsync", "SubmitAsync", "ReadReceiptAsync", "ReadProjectionAsync" };
            var inputs = new[] { typeof(CommandIntent), typeof(CommandIntent), typeof(ReceiptKey), typeof(ProjectionQuery) };
            var outputs = new[] { typeof(PreviewResult), typeof(CommandReceipt), typeof(ReceiptLookup), typeof(SessionProjection) };
            Assert.That(typeof(IOperationsPort).GetMethods().Length, Is.EqualTo(4));
            for (var i = 0; i < names.Length; i++)
            {
                var method = typeof(IOperationsPort).GetMethod(names[i]);
                Assert.That(method, Is.Not.Null);
                Assert.That(method.ReturnType, Is.EqualTo(typeof(Task<>).MakeGenericType(outputs[i])));
                Assert.That(method.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { inputs[i], typeof(CancellationToken) }));
            }
        }

        [Test]
        public void RunStoreCommitSignatureUsesExactWireTypesAndCancellation()
        {
            var type = typeof(IOperationsPort).Assembly.GetType("ChooGuard.Contracts.IRunStore");
            Assert.That(type, Is.Not.Null);
            Assert.That(type.IsInterface, Is.True);
            Assert.That(type.GetMethods().Length, Is.EqualTo(1));
            var method = type.GetMethod("CommitAsync");
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(Task<CommitReceipt>)));
            Assert.That(method.GetParameters().Select(p => p.ParameterType),
                Is.EqualTo(new[] { typeof(CommitBatch), typeof(CancellationToken) }));
        }

        [Test]
        public void ReceiptKeyRetainsAllThreeIdentityFields()
        {
            Assert.That(Key(), Is.EqualTo(Key()));
            Assert.That(Key("other"), Is.Not.EqualTo(Key()));
            Assert.Throws<ArgumentException>(() => new ReceiptKey(Id("run"), default(StableId), Id("intent")));
        }

        [Test]
        public void LookupRejectsBothInconsistentNullabilityCases()
        {
            Assert.That(new ReceiptLookup(false, null).Receipt, Is.Null);
            Assert.That(new ReceiptLookup(true, Receipt()).Found, Is.True);
            Assert.Throws<ArgumentException>(() => new ReceiptLookup(false, Receipt()));
            Assert.Throws<ArgumentException>(() => new ReceiptLookup(true, null));
        }

        [Test]
        public void AcceptedReceiptRequiresDurableCommitAndSequence()
        {
            Assert.Throws<ArgumentException>(() => new CommandReceipt(Key(), new string('b', 64), ReceiptStatus.ACCEPTED, null, null, new[] { Target() }));
            var receipt = new CommandReceipt(Key(), new string('b', 64), ReceiptStatus.ACCEPTED, "commit", new Sequence(0), new[] { Target() });
            Assert.That(receipt.Sequence.Value.Value, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommandReceipt(Key(), new string('b', 64), (ReceiptStatus)999, null, null, new[] { Target() }));
        }

        [Test]
        public void IntentCopiesCallerListsAndPreservesOrder()
        {
            var teams = new[] { Id("team.b"), Id("team.a") };
            var reads = new[] { new EntityRevision(Id("target"), 3) };
            var intent = new CommandIntent(Key(), Id("agency"), teams, Id("action"), new[] { Id("target") }, reads, Ref(), new UtcTimestamp(DateTime.UtcNow));
            teams[0] = Id("mutated"); reads[0] = new EntityRevision(Id("other"), 9);
            Assert.That(intent.ActingTeamIds[0], Is.EqualTo(Id("team.b")));
            Assert.That(intent.ReadSet[0].Revision, Is.EqualTo(3));
            Assert.Throws<NotSupportedException>(() => ((IList<StableId>)intent.ActingTeamIds)[0] = Id("mutated"));
        }

        [Test]
        public void NestedResultsAreDeeplyImmutable()
        {
            var refs = new[] { Ref() };
            var reasons = new[] { new Reason(Id("UNKNOWN_TARGET"), ReasonAxis.knowledge, Id("target"), refs, "대상 없음") };
            var results = new[] { new TargetResult(Id("target"), TargetStatus.REJECTED, reasons) };
            var preview = new PreviewResult(Key(), new[] { new EntityRevision(Id("target"), 0) }, results, Ref(), 0);
            refs[0] = new ContentReference(Id("changed"), 1, new string('c', 64)); reasons[0] = null; results[0] = null;
            Assert.That(preview.TargetResults[0].Reasons[0].EvidenceRefs[0].Id, Is.EqualTo(Id("content")));
        }

        [Test]
        public void RequiredCollectionsHashesAndRevisionsAreNotCoerced()
        {
            Assert.Throws<ArgumentException>(() => new ContentReference(Id("ref"), 0, "invalid"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EntityRevision(Id("entity"), -1));
            Assert.Throws<ArgumentException>(() => new TargetResult(Id("target"), TargetStatus.ACCEPTED, new Reason[] { null }));
            Assert.Throws<ArgumentException>(() => new PreviewResult(Key(), new EntityRevision[0], new[] { Target() }, Ref(), 0));
            Assert.Throws<ArgumentException>(() => new CommandIntent(Key(), Id("agency"), new[] { Id("team"), Id("team") }, Id("action"), new[] { Id("target") }, new[] { new EntityRevision(Id("target"), 0) }, Ref(), new UtcTimestamp(DateTime.UtcNow)));
        }

        [Test]
        public void ProjectionRetainsMandatoryReferencesAndNullableAgency()
        {
            var query = new ProjectionQuery(Id("run"), Id("user"), ViewScope.AUTHOR_ANALYSIS, null, 5);
            var projection = new SessionProjection(query.RunId, 6, new SimTick(7), query.ViewScope, query.AgencyId, Ref(), Ref(), Ref());
            Assert.That(projection.SimTick.Microseconds, Is.EqualTo(7));
            Assert.That(projection.EntitiesRef, Is.Not.Null);
            Assert.That(projection.TasksRef, Is.Not.Null);
            Assert.That(projection.ReasonsRef, Is.Not.Null);
            Assert.Throws<ArgumentException>(() => new ProjectionQuery(Id("run"), Id("user"), ViewScope.AUTHOR_ANALYSIS, default(StableId), 0));
        }
    }
}
#endif
