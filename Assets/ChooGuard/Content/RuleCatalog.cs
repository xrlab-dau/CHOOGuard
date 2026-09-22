using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ChooGuard.Contracts;

namespace ChooGuard.Content
{
    /// <summary>기존 파서와 독립된 타입 지정 후보 목록. 최신 판본을 임의 선택하지 않는다.</summary>
    public sealed class RuleCatalog
    {
        public IReadOnlyList<RuleClause> Clauses { get; }
        public RuleCatalog(IEnumerable<RuleClause> clauses)
        {
            if (clauses == null) throw new ArgumentNullException(nameof(clauses));
            var copy = new List<RuleClause>();
            foreach (var clause in clauses)
            {
                if (clause == null) throw new ArgumentNullException(nameof(clauses));
                copy.Add(clause);
            }
            copy.Sort((a, b) =>
            {
                var order = StringComparer.Ordinal.Compare(a.Id.Value, b.Id.Value);
                return order != 0 ? order : a.Revision.CompareTo(b.Revision);
            });
            for (var i = 1; i < copy.Count; i++)
                if (copy[i - 1].Id.Equals(copy[i].Id) && copy[i - 1].Revision == copy[i].Revision)
                    throw new ArgumentException("규칙 ID와 판본이 중복됩니다.", nameof(clauses));
            // 검수 상태나 scope에 관계없이 동일 ID의 겹치는 판본을 거부한다.
            var byTime = new List<RuleClause>(copy);
            byTime.Sort((a, b) =>
            {
                var order = StringComparer.Ordinal.Compare(a.Id.Value, b.Id.Value);
                return order != 0 ? order : a.ActivationStart.Microseconds.CompareTo(b.ActivationStart.Microseconds);
            });
            for (var i = 1; i < byTime.Count; i++)
                if (byTime[i - 1].Id.Equals(byTime[i].Id) &&
                    byTime[i].ActivationStart.Microseconds < byTime[i - 1].ActivationEnd.Microseconds)
                    throw new ArgumentException("같은 규칙 ID의 활성 구간이 겹칩니다.", nameof(clauses));
            Clauses = new ReadOnlyCollection<RuleClause>(copy);
        }

        /// <summary>정확히 일치하는 scope와 [시작, 끝) tick의 후보를 반환한다. 미검수 후보도 남긴다.</summary>
        public IReadOnlyList<RuleActivationDecision> Query(RuleScope scope, SimTick currentTick, RuleFacts facts)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            var result = new List<RuleActivationDecision>();
            foreach (var clause in Clauses)
            {
                if (!clause.Scope.AgencyId.Equals(scope.AgencyId) || !clause.Scope.JurisdictionId.Equals(scope.JurisdictionId) ||
                    !clause.Scope.ScenarioScopeId.Equals(scope.ScenarioScopeId) || !clause.Scope.EventId.Equals(scope.EventId)) continue;
                if (currentTick.Microseconds < clause.ActivationStart.Microseconds || currentTick.Microseconds >= clause.ActivationEnd.Microseconds) continue;
                result.Add(new RuleActivationDecision(clause, currentTick, facts));
            }
            return new ReadOnlyCollection<RuleActivationDecision>(result);
        }
    }
}
