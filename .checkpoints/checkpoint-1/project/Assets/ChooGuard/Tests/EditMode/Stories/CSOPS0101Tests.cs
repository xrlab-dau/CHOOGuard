#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ChooGuard.Application;
using ChooGuard.Contracts;
using ChooGuard.Domain;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    // Root 계약의 메모리 전달 의미만 검사한다. 업무 승인/영속화/외부 부작용 원자성 시험이 아니다.
    public class CSOPS0101Tests
    {
        private const int WaitMilliseconds = 5000;
        private static StableId NewRun() => new StableId("run-" + Guid.NewGuid().ToString("N"));
        private static StableId Id(string value) => new StableId(value);
        private static CommandIntent Intent(StableId run, string name = "intent") =>
            new CommandIntent(new ReceiptKey(run, Id("requester"), Id(name)), Id("agency"),
                new[] { Id("team") }, Id("action"), new[] { Id("target") },
                new[] { new EntityRevision(Id("target"), 7) },
                new ContentReference(Id("payload"), 3, new string('a', 64)),
                new UtcTimestamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        private static void State(RunState state, StableId run, long count, long tick, int? priority, long? sequence)
        {
            Assert.That(state.RunId, Is.EqualTo(run));
            Assert.That(state.ProcessedCount, Is.EqualTo(count));
            Assert.That(state.SimTick.Microseconds, Is.EqualTo(tick));
            Assert.That(state.LastPriority, Is.EqualTo(priority));
            Assert.That(state.LastMailboxSequence.HasValue, Is.EqualTo(sequence.HasValue));
            if (sequence.HasValue) Assert.That(state.LastMailboxSequence.Value.Value, Is.EqualTo(sequence.Value));
        }

        private static void Rejected(EnqueueResult result, EnqueueStatus status)
        {
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.MailboxSequence, Is.Null);
        }

        [Test]
        public void InitialStateAndEmptyDrain_HaveNoCursorOrEffects()
        {
            var run = NewRun();
            using (var session = new OperationsSession(run))
            {
                Assert.That(session.RunId, Is.EqualTo(run));
                Assert.That(session.PendingCount, Is.Zero);
                State(session.Snapshot(), run, 0, 0, null, null);
                var result = session.Drain(_ => Assert.Fail("빈 큐 callback 금지"));
                Assert.That(result.Status, Is.EqualTo(DrainStatus.Drained));
                Assert.That(result.ProcessedCommands, Is.Empty);
                Assert.That(result.FailedCommand, Is.Null);
                Assert.That(result.Failure, Is.Null);
                State(result.State, run, 0, 0, null, null);
            }
        }

        [Test]
        public void InvalidRunAndNullArguments_ThrowWithoutAdmission()
        {
            Assert.Throws<ArgumentException>(() => new RunState(default(StableId)));
            Assert.Throws<ArgumentException>(() => new OperationsSession(default(StableId)));
            using (var session = new OperationsSession(NewRun()))
            {
                Assert.Throws<ArgumentNullException>(() => session.Enqueue(null, new SimTick(0), 0));
                Assert.Throws<ArgumentNullException>(() => session.Drain(null));
                Assert.That(session.PendingCount, Is.Zero);
                Assert.That(session.Enqueue(Intent(session.RunId), new SimTick(0), 0).MailboxSequence.Value.Value, Is.EqualTo(1));
            }
        }

        [Test]
        public void RunClaim_IsExclusiveAndIndependentAndReacquirableAfterDispose()
        {
            var run = NewRun();
            using (var session = new OperationsSession(run))
            using (var other = new OperationsSession(NewRun()))
            {
                Assert.Throws<InvalidOperationException>(() => new OperationsSession(run));
                Assert.That(other.Enqueue(Intent(other.RunId), new SimTick(0), 0).Status, Is.EqualTo(EnqueueStatus.Enqueued));
                Assert.Throws<InvalidOperationException>(() => new OperationsSession(run));
            }
            using (var reopened = new OperationsSession(run)) State(reopened.Snapshot(), run, 0, 0, null, null);
        }

        [Test]
        public void Advance_UsesLexicographicCursorAndLeavesOriginalAndCopyUnchanged()
        {
            var run = NewRun();
            var initial = new RunState(run);
            var copy = initial.Copy();
            Assert.That(copy, Is.Not.SameAs(initial));
            var first = initial.Advance(new SimTick(0), int.MinValue, new Sequence(1));
            var second = first.Advance(new SimTick(0), int.MaxValue, new Sequence(9));
            var third = second.Advance(new SimTick(1), int.MinValue, new Sequence(1));
            State(initial, run, 0, 0, null, null);
            State(copy, run, 0, 0, null, null);
            State(first, run, 1, 0, int.MinValue, 1);
            State(third, run, 3, 1, int.MinValue, 1);
            var highSequence = initial.Advance(new SimTick(0), int.MinValue, new Sequence(9));
            var lowerSequence = highSequence.Advance(new SimTick(0), 0, new Sequence(1));
            State(lowerSequence, run, 2, 0, 0, 1);
        }

        [TestCase(9L, 100, 100L)]
        [TestCase(10L, 4, 100L)]
        [TestCase(10L, 5, 8L)]
        [TestCase(10L, 5, 9L)]
        [TestCase(11L, 6, 0L)]
        public void Advance_RejectsNonIncreasingTupleOrZeroTicket(long tick, int priority, long sequence)
        {
            var run = NewRun();
            var state = new RunState(run).Advance(new SimTick(10), 5, new Sequence(9));
            Assert.Throws<ArgumentException>(() => state.Advance(new SimTick(tick), priority, new Sequence(sequence)));
            State(state, run, 1, 10, 5, 9);
        }

        [Test]
        public void Advance_RejectsProcessedCountOverflowWithoutMutatingCursor()
        {
            var state = new RunState(NewRun());
            SetOnlyPrivateLong(state, long.MaxValue);
            Assert.Throws<OverflowException>(() => state.Advance(new SimTick(0), 0, new Sequence(1)));
            State(state, state.RunId, long.MaxValue, 0, null, null);
        }

        [Test]
        public void ShuffledInput_ProcessesDeterministicallyIncludingComparatorBoundaries()
        {
            var first = SortedTrace();
            var second = SortedTrace();
            Assert.That(first, Is.EqualTo(new[] { "0:-2147483648:4:d", "10:-2147483648:3:c", "10:2147483647:2:b", "10:2147483647:5:e", "20:0:1:a", "9223372036854775807:2147483647:6:f" }));
            Assert.That(second, Is.EqualTo(first));
        }

        private static string[] SortedTrace()
        {
            var run = NewRun();
            using (var session = new OperationsSession(run))
            {
                var ticks = new[] { 20L, 10L, 10L, 0L, 10L, long.MaxValue };
                var priorities = new[] { 0, int.MaxValue, int.MinValue, int.MinValue, int.MaxValue, int.MaxValue };
                for (var i = 0; i < ticks.Length; i++)
                {
                    var admitted = session.Enqueue(Intent(run, ((char)('a' + i)).ToString()), new SimTick(ticks[i]), priorities[i]);
                    Assert.That(admitted.Status, Is.EqualTo(EnqueueStatus.Enqueued));
                    Assert.That(admitted.MailboxSequence.Value.Value, Is.EqualTo(i + 1));
                }
                State(session.Snapshot(), run, 0, 0, null, null);
                var observed = new List<MailboxCommand>();
                var result = session.Drain(observed.Add);
                Assert.That(result.Status, Is.EqualTo(DrainStatus.Drained));
                Assert.That(result.ProcessedCommands, Is.EqualTo(observed));
                Assert.That(session.PendingCount, Is.Zero);
                State(result.State, run, 6, long.MaxValue, int.MaxValue, 6);
                return observed.Select(command => command.SimulationTick.Microseconds + ":" + command.Priority + ":" + command.MailboxSequence.Value + ":" + command.Intent.Key.IntentId.Value).ToArray();
            }
        }

        [Test]
        public void ProcessingTicketMayDecreaseWhenTickIncreases()
        {
            using (var session = new OperationsSession(NewRun()))
            {
                session.Enqueue(Intent(session.RunId), new SimTick(20), 0);
                session.Enqueue(Intent(session.RunId), new SimTick(10), 0);
                var seen = new List<long>();
                var result = session.Drain(command => seen.Add(command.MailboxSequence.Value));
                Assert.That(seen, Is.EqualTo(new long[] { 2, 1 }));
                Assert.That(result.Status, Is.EqualTo(DrainStatus.Drained));
                State(result.State, session.RunId, 2, 20, 0, 1);
            }
        }

        [Test]
        public void WrongRunAndStaleAdmissions_DoNotConsumeTicketsAndEqualCursorPrefixIsAllowed()
        {
            using (var session = new OperationsSession(NewRun()))
            {
                Rejected(session.Enqueue(Intent(NewRun()), new SimTick(0), 0), EnqueueStatus.WrongRun);
                Assert.That(session.Enqueue(Intent(session.RunId), new SimTick(10), 5).MailboxSequence.Value.Value, Is.EqualTo(1));
                session.Drain(_ => { });
                Rejected(session.Enqueue(Intent(session.RunId), new SimTick(9), int.MaxValue), EnqueueStatus.StaleOrder);
                Rejected(session.Enqueue(Intent(session.RunId), new SimTick(10), 4), EnqueueStatus.StaleOrder);
                Assert.That(session.PendingCount, Is.Zero);
                State(session.Snapshot(), session.RunId, 1, 10, 5, 1);
                Assert.That(session.Enqueue(Intent(session.RunId), new SimTick(10), 5).MailboxSequence.Value.Value, Is.EqualTo(2));
                var result = session.Drain(_ => { });
                Assert.That(result.Status, Is.EqualTo(DrainStatus.Drained));
                State(result.State, session.RunId, 2, 10, 5, 2);
            }
        }

        [Test]
        public void Admission_CopiesIntentAndAllThreeCollectionsAndPreservesValues()
        {
            var run = NewRun();
            var teams = new List<StableId> { Id("team") };
            var targets = new List<StableId> { Id("target") };
            var reads = new List<EntityRevision> { new EntityRevision(Id("target"), 7) };
            var original = new CommandIntent(new ReceiptKey(run, Id("requester"), Id("intent")), Id("agency"), teams,
                Id("action"), targets, reads, new ContentReference(Id("payload"), 3, new string('a', 64)),
                new UtcTimestamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            using (var session = new OperationsSession(run))
            {
                session.Enqueue(original, new SimTick(2), 7);
                teams.Clear(); targets.Clear(); reads.Clear();
                CommandIntent copy = null;
                var result = session.Drain(command => copy = command.Intent);
                Assert.That(result.Status, Is.EqualTo(DrainStatus.Drained));
                Assert.That(copy, Is.Not.Null.And.Not.SameAs(original));
                Assert.That(copy.ActingTeamIds, Is.Not.SameAs(original.ActingTeamIds).And.Not.SameAs(teams));
                Assert.That(copy.TargetIds, Is.Not.SameAs(original.TargetIds).And.Not.SameAs(targets));
                Assert.That(copy.ReadSet, Is.Not.SameAs(original.ReadSet).And.Not.SameAs(reads));
                Assert.That(copy.ActingTeamIds, Is.EqualTo(new[] { Id("team") }));
                Assert.That(copy.TargetIds, Is.EqualTo(new[] { Id("target") }));
                Assert.That(copy.ReadSet.Count, Is.EqualTo(1));
                Assert.That(copy.ReadSet[0].EntityId, Is.EqualTo(Id("target")));
                Assert.That(copy.ReadSet[0].Revision, Is.EqualTo(7));
                Assert.That(copy.Key, Is.EqualTo(original.Key));
                Assert.That(copy.ActingAgencyId, Is.EqualTo(original.ActingAgencyId));
                Assert.That(copy.ActionId, Is.EqualTo(original.ActionId));
                Assert.That(copy.PayloadRef.Id, Is.EqualTo(original.PayloadRef.Id));
                Assert.That(copy.PayloadRef.Revision, Is.EqualTo(3));
                Assert.That(copy.PayloadRef.Sha256, Is.EqualTo(original.PayloadRef.Sha256));
                Assert.That(copy.AuthoredAt.Value, Is.EqualTo(original.AuthoredAt.Value));
                Assert.Throws<NotSupportedException>(() => ((IList<StableId>)copy.ActingTeamIds).Add(Id("extra")));
                Assert.Throws<NotSupportedException>(() => ((IList<StableId>)copy.TargetIds).Clear());
                Assert.Throws<NotSupportedException>(() => ((IList<EntityRevision>)copy.ReadSet).Clear());
            }
        }

        [Test]
        public void SnapshotsAndResults_RemainDetachedAndReadOnlyAcrossFurtherDrains()
        {
            using (var session = new OperationsSession(NewRun()))
            {
                var before = session.Snapshot();
                var another = session.Snapshot();
                Assert.That(another, Is.Not.SameAs(before));
                session.Enqueue(Intent(session.RunId), new SimTick(1), 0);
                var first = session.Drain(_ => { });
                var derived = first.State.Advance(new SimTick(100), 0, new Sequence(100));
                Assert.That(derived.ProcessedCount, Is.EqualTo(2));
                Assert.That(first.State, Is.Not.SameAs(session.Snapshot()));
                Assert.Throws<NotSupportedException>(() => ((IList<MailboxCommand>)first.ProcessedCommands).Clear());
                session.Enqueue(Intent(session.RunId), new SimTick(2), 0);
                var second = session.Drain(_ => { });
                Assert.That(second.ProcessedCommands, Is.Not.SameAs(first.ProcessedCommands));
                Assert.That(first.ProcessedCommands.Count, Is.EqualTo(1));
                State(before, session.RunId, 0, 0, null, null);
                State(first.State, session.RunId, 1, 1, 0, 1);
                State(session.Snapshot(), session.RunId, 2, 2, 0, 2);
            }
        }

        [Test]
        public void PublicStateAndResultProperties_HaveNoSettersOrPublicResultConstructors()
        {
            foreach (var type in new[] { typeof(RunState), typeof(MailboxCommand), typeof(EnqueueResult), typeof(DrainResult) })
            {
                Assert.That(type.IsSealed, Is.True, type.Name);
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Assert.That(property.GetSetMethod(true), Is.Null, type.Name + "." + property.Name);
            }
            foreach (var type in new[] { typeof(MailboxCommand), typeof(EnqueueResult), typeof(DrainResult) })
                Assert.That(type.GetConstructors(), Is.Empty, type.Name);
            Assert.That(typeof(IOperationsPort).IsAssignableFrom(typeof(OperationsSession)), Is.False);
        }

        [Test]
        public void ReentrantWrites_AreBusyWithoutMutationAndDisposeRetainsRunClaim()
        {
            using (var session = new OperationsSession(NewRun()))
            {
                session.Enqueue(Intent(session.RunId), new SimTick(1), 0);
                var result = session.Drain(_ =>
                {
                    Rejected(session.Enqueue(Intent(session.RunId), new SimTick(2), 0), EnqueueStatus.WriterBusy);
                    var nested = session.Drain(__ => Assert.Fail("재진입 callback 금지"));
                    Assert.That(nested.Status, Is.EqualTo(DrainStatus.WriterBusy));
                    Assert.That(nested.ProcessedCommands, Is.Empty);
                    Assert.Throws<InvalidOperationException>(() => session.Dispose());
                    Assert.Throws<InvalidOperationException>(() => new OperationsSession(session.RunId));
                    State(session.Snapshot(), session.RunId, 0, 0, null, null);
                    Assert.That(session.PendingCount, Is.EqualTo(1));
                });
                Assert.That(result.Status, Is.EqualTo(DrainStatus.Drained), result.Failure?.ToString());
                Assert.That(session.Enqueue(Intent(session.RunId), new SimTick(2), 0).MailboxSequence.Value.Value, Is.EqualTo(2));
            }
        }

        [Test]
        public void ConcurrentWrites_RejectWhileCallbackRunsAndReadsDoNotBlock()
        {
            var session = new OperationsSession(NewRun());
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var checkedCalls = new ManualResetEventSlim();
            Exception writerError = null, observerError = null;
            DrainResult drained = null;
            var writer = new Thread(() =>
            {
                try
                {
                    drained = session.Drain(_ =>
                    {
                        entered.Set();
                        if (!release.Wait(WaitMilliseconds)) throw new TimeoutException("callback 해제 대기 초과");
                    });
                }
                catch (Exception error) { writerError = error; }
            }) { IsBackground = true };
            var observer = new Thread(() =>
            {
                try
                {
                    Rejected(session.Enqueue(Intent(session.RunId), new SimTick(2), 0), EnqueueStatus.WriterBusy);
                    var busy = session.Drain(_ => Assert.Fail("동시 callback 금지"));
                    Assert.That(busy.Status, Is.EqualTo(DrainStatus.WriterBusy));
                    Assert.That(busy.ProcessedCommands, Is.Empty);
                    Assert.Throws<InvalidOperationException>(() => session.Dispose());
                    Assert.Throws<InvalidOperationException>(() => new OperationsSession(session.RunId));
                    State(session.Snapshot(), session.RunId, 0, 0, null, null);
                    Assert.That(session.PendingCount, Is.EqualTo(1));
                }
                catch (Exception error) { observerError = error; }
                finally { checkedCalls.Set(); }
            }) { IsBackground = true };
            var writerStarted = false;
            var observerStarted = false;
            try
            {
                session.Enqueue(Intent(session.RunId), new SimTick(1), 0);
                writer.Start(); writerStarted = true;
                Assert.That(entered.Wait(WaitMilliseconds), Is.True, "callback 진입 제한 시간");
                observer.Start(); observerStarted = true;
                Assert.That(checkedCalls.Wait(WaitMilliseconds), Is.True, "callback 해제 전에 조회/거부가 완료되어야 한다");
                Assert.That(observerError, Is.Null);
                release.Set();
                Assert.That(writer.Join(WaitMilliseconds), Is.True);
                Assert.That(writerError, Is.Null);
                Assert.That(drained.Status, Is.EqualTo(DrainStatus.Drained), drained.Failure?.ToString());
                Assert.That(session.Enqueue(Intent(session.RunId), new SimTick(2), 0).MailboxSequence.Value.Value, Is.EqualTo(2));
            }
            finally
            {
                release.Set();
                var writerStopped = !writerStarted || writer.Join(WaitMilliseconds);
                var observerStopped = !observerStarted || observer.Join(WaitMilliseconds);
                if (writerStopped && observerStopped)
                {
                    session.Dispose(); entered.Dispose(); release.Dispose(); checkedCalls.Dispose();
                }
                Assert.That(writerStopped && observerStopped, Is.True, "모든 시험 thread는 제한 시간 내 종료해야 한다");
            }
        }

        [Test]
        public void ProcessorFailure_CommitsOnlyPrefixAndRetryKeepsFailedAndSuffixTickets()
        {
            using (var session = new OperationsSession(NewRun()))
            {
                session.Enqueue(Intent(session.RunId, "last"), new SimTick(30), 0);
                session.Enqueue(Intent(session.RunId, "first"), new SimTick(10), 0);
                session.Enqueue(Intent(session.RunId, "failed"), new SimTick(20), 0);
                var failure = new InvalidOperationException("consumer 실패");
                var attempted = new List<long>();
                var result = session.Drain(command =>
                {
                    attempted.Add(command.MailboxSequence.Value);
                    if (command.Intent.Key.IntentId.Equals(Id("failed"))) throw failure;
                });
                Assert.That(result.Status, Is.EqualTo(DrainStatus.ProcessorFailed));
                Assert.That(result.Failure, Is.SameAs(failure));
                Assert.That(result.FailedCommand.Intent.Key.IntentId, Is.EqualTo(Id("failed")));
                Assert.That(result.FailedCommand.MailboxSequence.Value, Is.EqualTo(3));
                Assert.That(attempted, Is.EqualTo(new long[] { 2, 3 }));
                Assert.That(result.ProcessedCommands.Select(command => command.MailboxSequence.Value), Is.EqualTo(new long[] { 2 }));
                State(result.State, session.RunId, 1, 10, 0, 2);
                Assert.That(session.PendingCount, Is.EqualTo(2));
                var retried = new List<long>();
                var retry = session.Drain(command => retried.Add(command.MailboxSequence.Value));
                Assert.That(retry.Status, Is.EqualTo(DrainStatus.Drained));
                Assert.That(retried, Is.EqualTo(new long[] { 3, 1 }));
                Assert.That(retry.FailedCommand, Is.Null);
                Assert.That(retry.Failure, Is.Null);
                State(retry.State, session.RunId, 3, 30, 0, 1);
                Assert.That(session.PendingCount, Is.Zero);
                Assert.That(session.Enqueue(Intent(session.RunId), new SimTick(31), 0).MailboxSequence.Value.Value, Is.EqualTo(4));
            }
        }

        [Test]
        public void SequenceExhaustion_AllowsMaxValueOnceAndNeverWraps()
        {
            using (var session = new OperationsSession(NewRun()))
            {
                SetOnlyPrivateLong(session, long.MaxValue - 1);
                var last = session.Enqueue(Intent(session.RunId), new SimTick(0), 0);
                Assert.That(last.Status, Is.EqualTo(EnqueueStatus.Enqueued));
                Assert.That(last.MailboxSequence.Value.Value, Is.EqualTo(long.MaxValue));
                Rejected(session.Enqueue(Intent(session.RunId), new SimTick(1), 0), EnqueueStatus.SequenceExhausted);
                Assert.That(session.PendingCount, Is.EqualTo(1));
                var result = session.Drain(_ => { });
                Assert.That(result.Status, Is.EqualTo(DrainStatus.Drained));
                State(result.State, session.RunId, 1, 0, 0, long.MaxValue);
                Rejected(session.Enqueue(Intent(session.RunId), new SimTick(1), 0), EnqueueStatus.SequenceExhausted);
                Assert.That(session.PendingCount, Is.Zero);
            }
        }

        // 공개 seed/setter 없이 필드 이름을 추측하지 않고 경계값만 주입한다.
        private static void SetOnlyPrivateLong(object target, long value)
        {
            var fields = target.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.IsPrivate && field.FieldType == typeof(long)).ToArray();
            Assert.That(fields.Length, Is.EqualTo(1), "private instance long 필드 단일성 계약");
            fields[0].SetValue(target, value);
        }

        [Test]
        public void Dispose_DiscardsPendingClosesWritesAndIsIdempotentWithoutReleasingNewOwner()
        {
            var run = NewRun();
            var session = new OperationsSession(run);
            try
            {
                session.Enqueue(Intent(run), new SimTick(1), 0);
                session.Drain(_ => { });
                session.Enqueue(Intent(run), new SimTick(2), 0);
                session.Dispose();
                session.Dispose();
                Assert.That(session.PendingCount, Is.Zero);
                State(session.Snapshot(), run, 1, 1, 0, 1);
                Rejected(session.Enqueue(Intent(run), new SimTick(3), 0), EnqueueStatus.Closed);
                var closed = session.Drain(_ => Assert.Fail("닫힌 session callback 금지"));
                Assert.That(closed.Status, Is.EqualTo(DrainStatus.Closed));
                Assert.That(closed.ProcessedCommands, Is.Empty);
                State(closed.State, run, 1, 1, 0, 1);
                using (var next = new OperationsSession(run))
                {
                    session.Dispose();
                    Assert.Throws<InvalidOperationException>(() => new OperationsSession(run));
                    State(next.Snapshot(), run, 0, 0, null, null);
                    Assert.That(next.Enqueue(Intent(run), new SimTick(0), 0).MailboxSequence.Value.Value, Is.EqualTo(1));
                }
            }
            finally { session.Dispose(); }
        }
    }
}
#endif
