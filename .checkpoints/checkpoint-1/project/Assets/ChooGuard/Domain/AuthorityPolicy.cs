using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChooGuard.Contracts;

namespace ChooGuard.Domain
{
    /// <summary>권한의 정본 근거와 명시 범위. 빈 범위는 아무것도 허용하지 않는다.</summary>
    public sealed class AuthorityGrant
    {
        public StableId GrantId { get; }
        public long Revision { get; }
        public StableId HolderId { get; }
        public StableId AgencyId { get; }
        public IReadOnlyList<StableId> TeamIds { get; }
        public IReadOnlyList<StableId> ActionIds { get; }
        public IReadOnlyList<StableId> TargetIds { get; }
        public ContentReference RuleRef { get; }
        public AuthorityGrant(StableId grantId, long revision, StableId holderId, StableId agencyId,
            IEnumerable<StableId> teamIds, IEnumerable<StableId> actionIds, IEnumerable<StableId> targetIds, ContentReference ruleRef)
        {
            GrantId = AuthorityInput.Id(grantId, nameof(grantId));
            Revision = AuthorityInput.Revision(revision);
            HolderId = AuthorityInput.Id(holderId, nameof(holderId));
            AgencyId = AuthorityInput.Id(agencyId, nameof(agencyId));
            TeamIds = AuthorityInput.Ids(teamIds, nameof(teamIds));
            ActionIds = AuthorityInput.Ids(actionIds, nameof(actionIds));
            TargetIds = AuthorityInput.Ids(targetIds, nameof(targetIds));
            RuleRef = ruleRef ?? throw new ArgumentNullException(nameof(ruleRef));
        }
    }

    /// <summary>호출자가 정본으로 확인한 현재 권한 입력. 권한 진위, DB 최신성, 작성자 편집권은 여기서 검증하지 않는다.</summary>
    public sealed class AuthoritySnapshot
    {
        public StableId RunId { get; }
        public IReadOnlyList<AuthorityGrant> Grants { get; }
        public IReadOnlyDictionary<StableId, StableId> TeamAgencies { get; }
        public IReadOnlyDictionary<StableId, StableId> TargetAgencies { get; }
        public AuthoritySnapshot(StableId runId, IEnumerable<AuthorityGrant> grants,
            IEnumerable<KeyValuePair<StableId, StableId>> teamAgencies, IEnumerable<KeyValuePair<StableId, StableId>> targetAgencies)
        {
            RunId = AuthorityInput.Id(runId, nameof(runId));
            if (grants == null) throw new ArgumentNullException(nameof(grants));
            var copy = new List<AuthorityGrant>();
            var ids = new HashSet<StableId>();
            foreach (var grant in grants)
            {
                if (grant == null || copy.Count == 4096 || !ids.Add(grant.GrantId))
                    throw new ArgumentException("권한은 null/중복 없이 최대 4096개여야 합니다.", nameof(grants));
                copy.Add(grant);
            }
            copy.Sort((a, b) => StringComparer.Ordinal.Compare(a.GrantId.Value, b.GrantId.Value));
            Grants = copy.AsReadOnly();
            TeamAgencies = AuthorityInput.Bindings(teamAgencies, nameof(teamAgencies));
            TargetAgencies = AuthorityInput.Bindings(targetAgencies, nameof(targetAgencies));
        }
    }

    public enum AuthorityDenial { MissingIntent, MissingAuthority, WrongRun, UnknownTeam, ForeignTeam, UnknownTarget, ForeignTarget, NoCoveringGrant }

    /// <summary>순수 권한 판정이며 receipt나 업무 완료를 의미하지 않는다.</summary>
    public sealed class AuthorityDecision
    {
        public bool Allowed => Grant != null;
        public AuthorityGrant Grant { get; }
        public IReadOnlyList<AuthorityDenial> Reasons { get; }
        internal AuthorityDecision(AuthorityGrant grant, IEnumerable<AuthorityDenial> reasons)
        {
            Grant = grant;
            Reasons = new List<AuthorityDenial>(reasons).AsReadOnly();
        }
    }

    public static class AuthorityPolicy
    {
        /// <summary>내부 업무만 판정한다. 외부 지원은 SupportRequest로 요청하며 타기관 직접 배정은 허용하지 않는다.</summary>
        public static AuthorityDecision Evaluate(CommandIntent intent, AuthoritySnapshot snapshot)
        {
            if (intent == null) return Deny(AuthorityDenial.MissingIntent);
            if (snapshot == null) return Deny(AuthorityDenial.MissingAuthority);
            if (!intent.Key.RunId.Equals(snapshot.RunId)) return Deny(AuthorityDenial.WrongRun);
            // 입력 순서나 grant 순서가 거부 이유 순서를 바꾸지 않는다.
            var failures = new SortedSet<AuthorityDenial>();
            foreach (var team in intent.ActingTeamIds)
            {
                if (!snapshot.TeamAgencies.TryGetValue(team, out var agency)) failures.Add(AuthorityDenial.UnknownTeam);
                else if (!agency.Equals(intent.ActingAgencyId)) failures.Add(AuthorityDenial.ForeignTeam);
            }
            foreach (var target in intent.TargetIds)
            {
                if (!snapshot.TargetAgencies.TryGetValue(target, out var agency)) failures.Add(AuthorityDenial.UnknownTarget);
                else if (!agency.Equals(intent.ActingAgencyId)) failures.Add(AuthorityDenial.ForeignTarget);
            }
            if (failures.Count != 0) return new AuthorityDecision(null, failures);
            foreach (var grant in snapshot.Grants)
                if (grant.HolderId.Equals(intent.Key.RequesterId) && grant.AgencyId.Equals(intent.ActingAgencyId) &&
                    Contains(grant.ActionIds, intent.ActionId) && Covers(grant.TeamIds, intent.ActingTeamIds) && Covers(grant.TargetIds, intent.TargetIds))
                    return new AuthorityDecision(grant, Array.Empty<AuthorityDenial>());
            return Deny(AuthorityDenial.NoCoveringGrant);
        }
        private static AuthorityDecision Deny(AuthorityDenial reason) => new AuthorityDecision(null, new[] { reason });
        private static bool Contains(IReadOnlyList<StableId> scope, StableId id)
        {
            foreach (var item in scope) if (item.Equals(id)) return true;
            return false;
        }
        private static bool Covers(IReadOnlyList<StableId> scope, IReadOnlyList<StableId> requested)
        {
            if (scope.Count == 0 || requested.Count == 0) return false;
            foreach (var id in requested) if (!Contains(scope, id)) return false;
            return true;
        }
    }

    internal static class AuthorityInput
    {
        internal static StableId Id(StableId id, string name)
        {
            if (!id.IsValid) throw new ArgumentException("초기화된 ID가 필요합니다.", name);
            return id;
        }
        internal static long Revision(long revision)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision), "음수 버전은 허용하지 않습니다.");
            return revision;
        }
        internal static IReadOnlyList<StableId> Ids(IEnumerable<StableId> values, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new List<StableId>();
            var ids = new HashSet<StableId>();
            foreach (var id in values)
            {
                Id(id, name);
                if (copy.Count == 4096 || !ids.Add(id)) throw new ArgumentException("ID 중복 또는 범위 제한 초과입니다.", name);
                copy.Add(id);
            }
            return copy.AsReadOnly();
        }
        internal static IReadOnlyDictionary<StableId, StableId> Bindings(IEnumerable<KeyValuePair<StableId, StableId>> values, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new Dictionary<StableId, StableId>();
            foreach (var item in values)
            {
                if (copy.Count == 4096) throw new ArgumentException("소속 입력 제한을 초과했습니다.", name);
                copy.Add(Id(item.Key, name), Id(item.Value, name));
            }
            return new ReadOnlyDictionary<StableId, StableId>(copy);
        }
    }
}
