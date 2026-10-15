using System;
using ChooGuard.Contracts;

namespace ChooGuard.Domain
{
    /// <summary>메모리 내 전달 처리 커서이며 도메인의 권위 있는 revision이 아니다.</summary>
    public sealed class RunState
    {
        public StableId RunId { get; }
        public long ProcessedCount { get; }
        public SimTick SimTick { get; }
        public int? LastPriority { get; }
        public Sequence? LastMailboxSequence { get; }

        public RunState(StableId runId)
        {
            if (!runId.IsValid) throw new ArgumentException("초기화된 run ID가 필요합니다.", nameof(runId));
            RunId = runId;
            SimTick = new SimTick(0);
        }

        private RunState(StableId runId, long processedCount, SimTick tick, int? priority, Sequence? mailboxSequence)
        {
            RunId = runId;
            ProcessedCount = processedCount;
            SimTick = tick;
            LastPriority = priority;
            LastMailboxSequence = mailboxSequence;
        }

        public RunState Copy() => new RunState(RunId, ProcessedCount, SimTick, LastPriority, LastMailboxSequence);

        public RunState Advance(SimTick tick, int priority, Sequence mailboxSequence)
        {
            if (mailboxSequence.Value <= 0)
                throw new ArgumentException("양수 mailbox sequence가 필요합니다.", nameof(mailboxSequence));
            if (LastMailboxSequence.HasValue)
            {
                var tickOrder = tick.Microseconds.CompareTo(SimTick.Microseconds);
                var priorityOrder = priority.CompareTo(LastPriority.Value);
                if (tickOrder < 0 || (tickOrder == 0 && (priorityOrder < 0 ||
                    (priorityOrder == 0 && mailboxSequence.Value <= LastMailboxSequence.Value.Value))))
                    throw new ArgumentException("처리 키는 이전 (tick, priority, sequence)보다 커야 합니다.");
            }

            // 발행 sequence는 처리 정렬 뒤 감소할 수 있으므로 전체 키만 비교한다.
            return new RunState(RunId, checked(ProcessedCount + 1), tick, priority, mailboxSequence);
        }
    }
}
