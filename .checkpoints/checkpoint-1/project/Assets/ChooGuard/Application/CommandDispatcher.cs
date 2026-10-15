using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChooGuard.Contracts;

namespace ChooGuard.Application
{
    /// <summary>
    /// Preflight 전용 불변 입력. TargetResults는 호출자가 현재 run에서 이미 계산한 guard 증거다.
    /// 이 타입은 증거의 출처·역할 계산·자원 잠금의 정확성이나 실제 최신 상태 조회를 보장하지 않는다.
    /// 누락은 검사 결과로 거부하며, 중복/null/잘못된 ID 및 음수 revision은 생성 시 거부한다.
    /// </summary>
    public sealed class CommandStateSnapshot
    {
        public StableId RunId { get; }
        public long Revision { get; }
        public IReadOnlyList<EntityRevision> EntityRevisions { get; }
        public IReadOnlyList<TargetResult> TargetResults { get; }

        public CommandStateSnapshot(StableId runId, long revision, IEnumerable<EntityRevision> entityRevisions,
            IEnumerable<TargetResult> targetResults)
        {
            if (!runId.IsValid) throw new ArgumentException("유효한 run ID가 필요합니다.", nameof(runId));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision), "revision은 음수일 수 없습니다.");
            RunId = runId;
            Revision = revision;
            EntityRevisions = CopyUnique(entityRevisions, x => x.EntityId, nameof(entityRevisions));
            TargetResults = CopyUnique(targetResults, x => x.TargetId, nameof(targetResults));
        }

        private static IReadOnlyList<T> CopyUnique<T>(IEnumerable<T> values, Func<T, StableId> identity, string name) where T : class
        {
            if (values == null) throw new ArgumentNullException(name);
            var ids = new HashSet<StableId>();
            var copy = new List<T>();
            foreach (var value in values)
            {
                if (value == null) throw new ArgumentException("null 원소를 허용하지 않습니다.", name);
                var id = identity(value);
                if (!id.IsValid || !ids.Add(id)) throw new ArgumentException("유효하지 않거나 중복된 ID입니다.", name);
                // 원소는 sealed 불변 계약 DTO이므로 컬렉션 소유권 복사로 충분하다.
                copy.Add(value);
            }
            return new ReadOnlyCollection<T>(copy);
        }
    }

    public enum CommandCheckStatus { PreflightReady, PreflightRejected }

    /// <summary>Preflight 전용 결정이며 제출 수락 receipt가 아니다. QUEUED는 실행 가능 후보만 뜻한다.</summary>
    public sealed class CommandCheckResult
    {
        public CommandCheckStatus Status { get; }
        public long CurrentRevision { get; }
        public IReadOnlyList<EntityRevision> ReadSet { get; }
        public IReadOnlyList<TargetResult> TargetResults { get; }
        public IReadOnlyList<Reason> Reasons { get; }
        public bool CanProceed => Status == CommandCheckStatus.PreflightReady;

        internal CommandCheckResult(long revision, IEnumerable<EntityRevision> readSet,
            IEnumerable<TargetResult> targetResults, IEnumerable<Reason> reasons)
        {
            CurrentRevision = revision;
            ReadSet = new ReadOnlyCollection<EntityRevision>(new List<EntityRevision>(readSet));
            TargetResults = new ReadOnlyCollection<TargetResult>(new List<TargetResult>(targetResults));
            Reasons = new ReadOnlyCollection<Reason>(new List<Reason>(reasons));
            var ready = Reasons.Count == 0 && TargetResults.Count != 0;
            foreach (var target in TargetResults) ready &= target.Status == TargetStatus.QUEUED;
            Status = ready ? CommandCheckStatus.PreflightReady : CommandCheckStatus.PreflightRejected;
        }
    }

    /// <summary>
    /// Preflight only: 매 호출에 제공된 현재 불변 snapshot을 독립 평가한다. Preview 성공을 캐시하지 않는다.
    /// IOperationsPort/Submit/멱등성/commit을 구현하지 않으며 예약·outbox·receipt·가짜 효과 참조를 만들지 않는다.
    /// 실제 Submit은 권위 상태에서 guard를 다시 계산하고 원자적 commit 안에서 재검사해야 한다.
    /// </summary>
    public sealed class CommandDispatcher
    {
        public CommandCheckResult Preview(CommandIntent intent, CommandStateSnapshot current) => Evaluate(intent, current);
        public CommandCheckResult RevalidateForSubmit(CommandIntent intent, CommandStateSnapshot current) => Evaluate(intent, current);

        private static CommandCheckResult Evaluate(CommandIntent intent, CommandStateSnapshot current)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (current == null) throw new ArgumentNullException(nameof(current));
            var reasons = new List<Reason>();
            var expected = new Dictionary<StableId, long>();
            var entities = new Dictionary<StableId, long>();
            var guards = new Dictionary<StableId, TargetResult>();
            var targets = new List<StableId>(intent.TargetIds);
            targets.Sort((a, b) => StringComparer.Ordinal.Compare(a.Value, b.Value));
            var requested = new HashSet<StableId>(targets);
            foreach (var entity in current.EntityRevisions) entities.Add(entity.EntityId, entity.Revision);
            foreach (var guard in current.TargetResults)
            {
                guards.Add(guard.TargetId, guard);
                if (!requested.Contains(guard.TargetId))
                    reasons.Add(Failure("UNEXPECTED_TARGET_RESULT", guard.TargetId, "요청 대상 이외의 guard 결과입니다."));
            }
            if (!intent.Key.RunId.Equals(current.RunId))
                reasons.Add(Failure("RUN_MISMATCH", current.RunId, "요청과 현재 snapshot의 run이 다릅니다."));
            foreach (var read in intent.ReadSet)
            {
                if (expected.ContainsKey(read.EntityId))
                    reasons.Add(Failure("DUPLICATE_READ_SET", read.EntityId, "readSet ID가 중복되었습니다."));
                else expected.Add(read.EntityId, read.Revision);
            }
            if (!expected.ContainsKey(current.RunId))
                reasons.Add(Failure("MISSING_READ_SET", current.RunId, "권위 run의 readSet이 없습니다."));
            if (!entities.TryGetValue(current.RunId, out var runRevision))
                reasons.Add(Failure("MISSING_ENTITY", current.RunId, "현재 run entity가 없습니다."));
            else if (runRevision != current.Revision)
                reasons.Add(Failure("STALE_STATE", current.RunId, "run entity와 권위 revision이 다릅니다."));
            if (expected.TryGetValue(current.RunId, out var expectedRun) && expectedRun != current.Revision)
                reasons.Add(Failure("STALE_STATE", current.RunId, "예상 run revision과 권위 revision이 다릅니다."));

            // 보조 자원을 포함하여 전체 readSet을 검사한다. 실패한 공통 전제는 모든 후보를 차단한다.
            foreach (var read in expected)
            {
                if (!entities.TryGetValue(read.Key, out var actual))
                {
                    if (!read.Key.Equals(current.RunId))
                        reasons.Add(Failure("MISSING_ENTITY", read.Key, "readSet의 현재 entity가 없습니다."));
                }
                else if (actual != read.Value)
                    reasons.Add(Failure("STALE_STATE", read.Key, "예상 revision과 현재 entity revision이 다릅니다."));
            }
            reasons.Sort(CompareReasons);
            var results = new List<TargetResult>();
            foreach (var target in targets)
            {
                var targetReasons = new List<Reason>(reasons);
                if (!expected.ContainsKey(target))
                    targetReasons.Add(Failure("MISSING_READ_SET", target, "대상의 readSet이 없습니다."));
                if (!entities.ContainsKey(target) && !expected.ContainsKey(target))
                    targetReasons.Add(Failure("MISSING_ENTITY", target, "대상의 현재 entity가 없습니다."));
                var rejected = targetReasons.Count != 0;
                if (!guards.TryGetValue(target, out var guard))
                {
                    targetReasons.Add(Failure("MISSING_TARGET_RESULT", target, "대상의 현재 guard 결과가 없습니다."));
                    rejected = true;
                }
                else
                {
                    targetReasons.AddRange(guard.Reasons);
                    if (guard.Status == TargetStatus.REJECTED)
                    {
                        rejected = true;
                        if (guard.Reasons.Count == 0)
                            targetReasons.Add(Failure("GUARD_REJECTED", target, "현재 guard가 대상을 거부했습니다."));
                    }
                }
                results.Add(new TargetResult(target, rejected ? TargetStatus.REJECTED : TargetStatus.QUEUED, targetReasons));
            }
            return new CommandCheckResult(current.Revision, intent.ReadSet, results, reasons);
        }

        private static int CompareReasons(Reason left, Reason right)
        {
            var subject = StringComparer.Ordinal.Compare(left.SubjectId.Value, right.SubjectId.Value);
            return subject != 0 ? subject : StringComparer.Ordinal.Compare(left.Code.Value, right.Code.Value);
        }

        private static Reason Failure(string code, StableId subject, string message) =>
            new Reason(new StableId(code), ReasonAxis.evidence, subject, new ContentReference[0], message);
    }
}
