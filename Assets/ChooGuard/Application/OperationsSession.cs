using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChooGuard.Contracts;
using ChooGuard.Domain;

namespace ChooGuard.Application
{
    public enum EnqueueStatus { Enqueued, WrongRun, StaleOrder, WriterBusy, Closed, SequenceExhausted }

    public sealed class EnqueueResult
    {
        public EnqueueStatus Status { get; }
        public Sequence? MailboxSequence { get; }
        internal EnqueueResult(EnqueueStatus status, Sequence? mailboxSequence = null)
        {
            Status = status;
            MailboxSequence = mailboxSequence;
        }
    }

    public sealed class MailboxCommand
    {
        public CommandIntent Intent { get; }
        public SimTick SimulationTick { get; }
        public int Priority { get; }
        public Sequence MailboxSequence { get; }
        internal MailboxCommand(CommandIntent intent, SimTick simulationTick, int priority, Sequence mailboxSequence)
        {
            Intent = intent;
            SimulationTick = simulationTick;
            Priority = priority;
            MailboxSequence = mailboxSequence;
        }
    }

    public enum DrainStatus { Drained, WriterBusy, Closed, ProcessorFailed }

    public sealed class DrainResult
    {
        public DrainStatus Status { get; }
        public IReadOnlyList<MailboxCommand> ProcessedCommands { get; }
        public MailboxCommand FailedCommand { get; }
        public Exception Failure { get; }
        public RunState State { get; }
        internal DrainResult(DrainStatus status, IEnumerable<MailboxCommand> processedCommands, RunState state,
            MailboxCommand failedCommand = null, Exception failure = null)
        {
            Status = status;
            ProcessedCommands = new ReadOnlyCollection<MailboxCommand>(new List<MailboxCommand>(processedCommands));
            State = state.Copy();
            FailedCommand = failedCommand;
            Failure = failure;
        }
    }

    /// <summary>
    /// 프로세스 내 run별 독점 메모리 큐. 소유자는 반드시 Dispose로 run 등록을 해제해야 한다.
    /// 프로세스 간 소유권, 영속화, 업무 승인 및 receipt를 제공하지 않는다.
    /// 같은 명령 key도 별도 ticket으로 접수하며 중복 제거를 보장하지 않는다.
    /// </summary>
    public sealed class OperationsSession : IDisposable
    {
        private static readonly object RegistryGate = new object();
        private static readonly HashSet<StableId> LiveRuns = new HashSet<StableId>();
        private readonly object gate = new object();
        private readonly List<MailboxCommand> pending = new List<MailboxCommand>();
        private RunState state;
        private long lastIssuedSequence;
        private bool writerActive;
        private bool disposed;

        public StableId RunId { get; }
        public int PendingCount { get { lock (gate) return pending.Count; } }

        public OperationsSession(StableId runId)
        {
            state = new RunState(runId);
            RunId = runId;
            lock (RegistryGate)
            {
                if (!LiveRuns.Add(runId))
                    throw new InvalidOperationException("이 run의 세션이 이미 열려 있습니다.");
            }
        }

        public EnqueueResult Enqueue(CommandIntent intent, SimTick simulationTick, int priority)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            lock (gate)
            {
                if (disposed) return new EnqueueResult(EnqueueStatus.Closed);
                if (writerActive) return new EnqueueResult(EnqueueStatus.WriterBusy);
                if (!RunId.Equals(intent.Key.RunId)) return new EnqueueResult(EnqueueStatus.WrongRun);
                if (state.LastMailboxSequence.HasValue &&
                    (simulationTick.Microseconds < state.SimTick.Microseconds ||
                     (simulationTick.Microseconds == state.SimTick.Microseconds && priority < state.LastPriority.Value)))
                    return new EnqueueResult(EnqueueStatus.StaleOrder);
                if (lastIssuedSequence == long.MaxValue) return new EnqueueResult(EnqueueStatus.SequenceExhausted);

                var sequence = new Sequence(checked(lastIssuedSequence + 1));
                var copy = new CommandIntent(intent.Key, intent.ActingAgencyId, intent.ActingTeamIds, intent.ActionId,
                    intent.TargetIds, intent.ReadSet, intent.PayloadRef, intent.AuthoredAt);
                pending.Add(new MailboxCommand(copy, simulationTick, priority, sequence));
                lastIssuedSequence = sequence.Value;
                return new EnqueueResult(EnqueueStatus.Enqueued, sequence);
            }
        }

        public DrainResult Drain(Action<MailboxCommand> process)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));
            var processed = new List<MailboxCommand>();
            lock (gate)
            {
                if (disposed) return new DrainResult(DrainStatus.Closed, processed, state);
                if (writerActive) return new DrainResult(DrainStatus.WriterBusy, processed, state);
                writerActive = true;
            }

            try
            {
                List<MailboxCommand> batch;
                lock (gate)
                {
                    batch = new List<MailboxCommand>(pending);
                    batch.Sort(CompareCommands);
                }
                foreach (var command in batch)
                {
                    RunState candidate;
                    try
                    {
                        lock (gate)
                            candidate = state.Advance(command.SimulationTick, command.Priority, command.MailboxSequence);
                        // 신뢰된 동기 소비자를 잠금 밖에서 호출한다. 실패 후 재시도는 다시 호출할 수 있다.
                        // 외부 부수효과의 원자성·rollback·exactly-once 또는 at-most-once는 보장하지 않는다.
                        process(command);
                    }
                    catch (Exception failure)
                    {
                        lock (gate)
                            return new DrainResult(DrainStatus.ProcessorFailed, processed, state, command, failure);
                    }

                    lock (gate)
                    {
                        pending.Remove(command);
                        state = candidate;
                        processed.Add(command);
                    }
                }
                lock (gate) return new DrainResult(DrainStatus.Drained, processed, state);
            }
            finally
            {
                lock (gate) writerActive = false;
            }
        }

        /// <summary>Individual world effects share this run's exclusive writer and clock without fake agency commands.</summary>
        public void ExecuteWorld(SimTick tick, Action write, bool restoreClock = false)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            RunState candidate;
            long sequence;
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(OperationsSession));
                if (writerActive || pending.Count != 0) throw new InvalidOperationException("다른 세계 쓰기 또는 기관 명령 처리가 진행 중입니다.");
                if (!restoreClock && tick.Microseconds < state.SimTick.Microseconds) throw new InvalidOperationException("세계 시각은 단조 증가해야 합니다.");
                sequence = checked(lastIssuedSequence + 1);
                candidate = (restoreClock ? new RunState(RunId) : state).Advance(tick, 0, new Sequence(sequence));
                writerActive = true;
            }
            try
            {
                write();
                lock (gate) { state = candidate; lastIssuedSequence = sequence; }
            }
            finally { lock (gate) writerActive = false; }
        }

        public RunState Snapshot()
        {
            lock (gate) return state.Copy();
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                if (writerActive) throw new InvalidOperationException("처리 중에는 세션을 폐기할 수 없습니다.");
                // 생성자는 registry 잠금만 사용하므로 반대 순서의 중첩 잠금이 없다.
                lock (RegistryGate)
                {
                    pending.Clear();
                    disposed = true;
                    LiveRuns.Remove(RunId);
                }
            }
        }

        private static int CompareCommands(MailboxCommand left, MailboxCommand right)
        {
            var order = left.SimulationTick.Microseconds.CompareTo(right.SimulationTick.Microseconds);
            if (order != 0) return order;
            order = left.Priority.CompareTo(right.Priority);
            return order != 0 ? order : left.MailboxSequence.Value.CompareTo(right.MailboxSequence.Value);
        }
    }
}
