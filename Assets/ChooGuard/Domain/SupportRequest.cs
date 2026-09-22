using System;
using ChooGuard.Contracts;

namespace ChooGuard.Domain
{
    public enum SupportStatus { Requested, Accepted, Rejected, Assigned }
    public enum SupportDenial { None, StaleVersion, MissingAuthority, WrongRun, ForeignReceiver, UnknownTeam, ForeignTeam, IllegalTransition, VersionExhausted }

    /// <summary>기관 간 요청의 순수 상태. 생성/접수는 배정이나 예약을 만들지 않으며 저장과 사건 발행은 별도다.</summary>
    public sealed class SupportRequest
    {
        public StableId RunId { get; }
        public StableId RequestId { get; }
        public long Revision { get; }
        public StableId RequesterAgencyId { get; }
        public StableId ReceiverAgencyId { get; }
        public StableId TargetTeamId { get; }
        public SupportStatus Status { get; }

        // 초기 버전은 정본 저장 계층이 제공한다. 이 생성자는 항상 Requested만 만든다.
        public SupportRequest(StableId runId, StableId requestId, long revision, StableId requesterAgencyId,
            StableId receiverAgencyId, StableId targetTeamId)
            : this(runId, requestId, revision, requesterAgencyId, receiverAgencyId, targetTeamId, SupportStatus.Requested) { }

        private SupportRequest(StableId runId, StableId requestId, long revision, StableId requesterAgencyId,
            StableId receiverAgencyId, StableId targetTeamId, SupportStatus status)
        {
            RunId = AuthorityInput.Id(runId, nameof(runId));
            RequestId = AuthorityInput.Id(requestId, nameof(requestId));
            Revision = AuthorityInput.Revision(revision);
            RequesterAgencyId = AuthorityInput.Id(requesterAgencyId, nameof(requesterAgencyId));
            ReceiverAgencyId = AuthorityInput.Id(receiverAgencyId, nameof(receiverAgencyId));
            TargetTeamId = AuthorityInput.Id(targetTeamId, nameof(targetTeamId));
            if (requesterAgencyId.Equals(receiverAgencyId)) throw new ArgumentException("지원요청은 서로 다른 기관 사이에 생성합니다.");
            Status = status;
        }

        // actingAgencyId는 인증된 업무 주체의 기관이어야 한다. 작성자 분석권으로 대체할 수 없다.
        public SupportDecision Accept(StableId actingAgencyId, long expectedVersion, AuthoritySnapshot snapshot) =>
            Transition(actingAgencyId, expectedVersion, snapshot, SupportStatus.Requested, SupportStatus.Accepted);
        public SupportDecision Reject(StableId actingAgencyId, long expectedVersion, AuthoritySnapshot snapshot) =>
            Transition(actingAgencyId, expectedVersion, snapshot, SupportStatus.Requested, SupportStatus.Rejected);
        public SupportDecision Assign(StableId actingAgencyId, long expectedVersion, AuthoritySnapshot snapshot) =>
            Transition(actingAgencyId, expectedVersion, snapshot, SupportStatus.Accepted, SupportStatus.Assigned);

        private SupportDecision Transition(StableId actor, long expectedVersion, AuthoritySnapshot snapshot, SupportStatus from, SupportStatus to)
        {
            if (expectedVersion != Revision) return Deny(SupportDenial.StaleVersion);
            if (snapshot == null) return Deny(SupportDenial.MissingAuthority);
            if (!RunId.Equals(snapshot.RunId)) return Deny(SupportDenial.WrongRun);
            if (!actor.IsValid || !actor.Equals(ReceiverAgencyId)) return Deny(SupportDenial.ForeignReceiver);
            if (!snapshot.TeamAgencies.TryGetValue(TargetTeamId, out var agency)) return Deny(SupportDenial.UnknownTeam);
            if (!agency.Equals(ReceiverAgencyId)) return Deny(SupportDenial.ForeignTeam);
            if (Status != from) return Deny(SupportDenial.IllegalTransition);
            long next;
            try { next = checked(Revision + 1); }
            catch (OverflowException) { return Deny(SupportDenial.VersionExhausted); }
            return new SupportDecision(new SupportRequest(RunId, RequestId, next, RequesterAgencyId, ReceiverAgencyId, TargetTeamId, to), SupportDenial.None);
        }
        private SupportDecision Deny(SupportDenial denial) => new SupportDecision(this, denial);
    }

    /// <summary>허용이면 새 immutable 상태, 거부면 원래 상태를 반환한다. durable receipt가 아니다.</summary>
    public sealed class SupportDecision
    {
        public bool Allowed => Denial == SupportDenial.None;
        public SupportRequest State { get; }
        public SupportDenial Denial { get; }
        internal SupportDecision(SupportRequest state, SupportDenial denial) { State = state; Denial = denial; }
    }
}
