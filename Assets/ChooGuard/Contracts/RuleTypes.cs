using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ChooGuard.Contracts
{
    public enum RuleKind { REQUIRED, DISCRETIONARY, ADVISORY, INVARIANT, UNKNOWN }
    public enum RuleTruth { TRUE, FALSE, UNKNOWN, CONFLICTED }
    public enum RuleReviewState { UNREVIEWED, APPROVED, REJECTED }
    public enum RuleExpressionKind { CONSTANT, FACT, ALL, ANY, NOT }
    public enum RuleActivationReason { ACTIVE, OUTSIDE_ACTIVATION_INTERVAL, UNKNOWN_KIND, UNREVIEWED, REJECTED, INVALID_APPROVAL, GUARD_NOT_TRUE, EXCEPTION_NOT_FALSE }

    /// <summary>검수 표시는 데이터일 뿐 기관 승인 진위의 증명이 아니다.</summary>
    public sealed class RuleReview
    {
        public RuleReviewState State { get; }
        public StableId? ReviewerId { get; }
        public long? ReviewedRuleRevision { get; }
        public RuleReview(RuleReviewState state, StableId? reviewerId, long? reviewedRuleRevision)
        {
            State = ContractGuard.Defined(state, nameof(state));
            if (reviewerId.HasValue) ContractGuard.Id(reviewerId.Value, nameof(reviewerId));
            if (reviewedRuleRevision.HasValue) ContractGuard.Nonnegative(reviewedRuleRevision.Value, nameof(reviewedRuleRevision));
            ReviewerId = reviewerId;
            ReviewedRuleRevision = reviewedRuleRevision;
        }
    }

    public sealed class RuleScope
    {
        public StableId AgencyId { get; }
        public StableId JurisdictionId { get; }
        public StableId ScenarioScopeId { get; }
        public StableId EventId { get; }
        public RuleScope(StableId agencyId, StableId jurisdictionId, StableId scenarioScopeId, StableId eventId)
        {
            AgencyId = ContractGuard.Id(agencyId, nameof(agencyId));
            JurisdictionId = ContractGuard.Id(jurisdictionId, nameof(jurisdictionId));
            ScenarioScopeId = ContractGuard.Id(scenarioScopeId, nameof(scenarioScopeId));
            EventId = ContractGuard.Id(eventId, nameof(eventId));
        }
    }

    /// <summary>호출자의 사전을 복사한다. 미관측/null 사실은 UNKNOWN이다.</summary>
    public sealed class RuleFacts
    {
        private readonly Dictionary<StableId, RuleTruth> values = new Dictionary<StableId, RuleTruth>();
        public RuleFacts(IEnumerable<KeyValuePair<StableId, RuleTruth?>> facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            foreach (var fact in facts)
            {
                ContractGuard.Id(fact.Key, nameof(facts));
                var truth = fact.Value ?? RuleTruth.UNKNOWN;
                ContractGuard.Defined(truth, nameof(facts));
                if (values.ContainsKey(fact.Key)) throw new ArgumentException("중복 사실 ID입니다.", nameof(facts));
                values.Add(fact.Key, truth);
            }
        }
        public RuleTruth Get(StableId factId)
        {
            ContractGuard.Id(factId, nameof(factId));
            return values.TryGetValue(factId, out var truth) ? truth : RuleTruth.UNKNOWN;
        }
    }

    /// <summary>실행 코드를 받지 않는 닫힌 AST. 공유 하위 트리도 출현 횟수대로 노드 예산에 센다.</summary>
    public sealed class RuleExpression
    {
        public const int MaxDepth = 32;
        public const int MaxChildren = 256;
        public const int MaxNodes = 4096;
        public RuleExpressionKind Kind { get; }
        public RuleTruth? ConstantValue { get; }
        public StableId? FactId { get; }
        public IReadOnlyList<RuleExpression> Children { get; }
        public int Depth { get; }
        public int NodeCount { get; }
        private RuleExpression(RuleExpressionKind kind, RuleTruth? constant, StableId? fact, IEnumerable<RuleExpression> children)
        {
            var copy = new List<RuleExpression>();
            var depth = 1;
            var nodes = 1;
            foreach (var child in children)
            {
                if (child == null) throw new ArgumentNullException(nameof(children));
                if (copy.Count == MaxChildren) throw new ArgumentException("AST 자식 수 한도를 초과했습니다.", nameof(children));
                depth = Math.Max(depth, child.Depth + 1);
                nodes += child.NodeCount;
                if (depth > MaxDepth || nodes > MaxNodes) throw new ArgumentException("AST 깊이 또는 전체 노드 한도를 초과했습니다.", nameof(children));
                copy.Add(child);
            }
            Kind = kind; ConstantValue = constant; FactId = fact;
            Children = new ReadOnlyCollection<RuleExpression>(copy);
            Depth = depth; NodeCount = nodes;
        }
        public static RuleExpression Constant(RuleTruth value)
            => new RuleExpression(RuleExpressionKind.CONSTANT, ContractGuard.Defined(value, nameof(value)), null, new RuleExpression[0]);
        public static RuleExpression Fact(StableId id)
            => new RuleExpression(RuleExpressionKind.FACT, null, ContractGuard.Id(id, nameof(id)), new RuleExpression[0]);
        public static RuleExpression Not(RuleExpression child)
            => new RuleExpression(RuleExpressionKind.NOT, null, null, new[] { ContractGuard.NotNull(child, nameof(child)) });
        public static RuleExpression All(IEnumerable<RuleExpression> children)
            => new RuleExpression(RuleExpressionKind.ALL, null, null, children ?? throw new ArgumentNullException(nameof(children)));
        public static RuleExpression Any(IEnumerable<RuleExpression> children)
            => new RuleExpression(RuleExpressionKind.ANY, null, null, children ?? throw new ArgumentNullException(nameof(children)));

        /// <summary>AND의 FALSE와 OR의 TRUE가 확정값이다. 미확정이면 CONFLICTED가 UNKNOWN보다 우선한다.
        /// 빈 All은 TRUE, 빈 Any는 FALSE이며 Not은 미확정값을 그대로 보존한다.</summary>
        public RuleTruth Evaluate(RuleFacts facts)
        {
            switch (Kind)
            {
                case RuleExpressionKind.CONSTANT: return ConstantValue.Value;
                case RuleExpressionKind.FACT: return facts == null ? RuleTruth.UNKNOWN : facts.Get(FactId.Value);
                case RuleExpressionKind.NOT:
                    var value = Children[0].Evaluate(facts);
                    return value == RuleTruth.TRUE ? RuleTruth.FALSE : value == RuleTruth.FALSE ? RuleTruth.TRUE : value;
                default:
                    var all = Kind == RuleExpressionKind.ALL;
                    var result = all ? RuleTruth.TRUE : RuleTruth.FALSE;
                    foreach (var child in Children)
                    {
                        var truth = child.Evaluate(facts);
                        if (all && truth == RuleTruth.FALSE) return RuleTruth.FALSE;
                        if (!all && truth == RuleTruth.TRUE) return RuleTruth.TRUE;
                        if (truth == RuleTruth.CONFLICTED) result = RuleTruth.CONFLICTED;
                        else if (truth == RuleTruth.UNKNOWN && result != RuleTruth.CONFLICTED) result = RuleTruth.UNKNOWN;
                    }
                    return result;
            }
        }
    }

    /// <summary>타입 지정 규칙 후보. 원문 입고·검수의 진위·JSON 검증을 대체하지 않는다.</summary>
    public sealed class RuleClause
    {
        public StableId Id { get; }
        public long Revision { get; }
        public RuleScope Scope { get; }
        public ContentReference Source { get; }
        public string ClauseLocator { get; }
        public RuleKind Kind { get; }
        public RuleExpression Guard { get; }
        public string EffectText { get; }
        public RuleExpression Exception { get; }
        public SimTick ActivationStart { get; }
        public SimTick ActivationEnd { get; }
        public RuleReview Review { get; }
        public RuleClause(StableId id, long revision, StableId agencyId, StableId jurisdictionId,
            StableId scenarioScopeId, StableId eventId, ContentReference source, string clauseLocator,
            RuleKind kind, RuleExpression guard, string effectText, RuleExpression exception,
            SimTick activationStart, SimTick activationEnd, RuleReview review)
        {
            Id = ContractGuard.Id(id, nameof(id));
            Revision = ContractGuard.Nonnegative(revision, nameof(revision));
            Scope = new RuleScope(agencyId, jurisdictionId, scenarioScopeId, eventId);
            Source = ContractGuard.NotNull(source, nameof(source));
            if (string.IsNullOrWhiteSpace(clauseLocator)) throw new ArgumentException("원문 절 위치가 필요합니다.", nameof(clauseLocator));
            if (string.IsNullOrWhiteSpace(effectText)) throw new ArgumentException("효과 설명이 필요합니다.", nameof(effectText));
            ClauseLocator = clauseLocator; EffectText = effectText;
            Kind = ContractGuard.Defined(kind, nameof(kind));
            Guard = ContractGuard.NotNull(guard, nameof(guard));
            Exception = exception;
            // guard와 exception을 합친 규칙 단위의 노드 예산도 제한한다.
            if (Guard.NodeCount + (exception == null ? 0 : exception.NodeCount) > RuleExpression.MaxNodes)
                throw new ArgumentException("규칙 전체 AST 노드 한도를 초과했습니다.", nameof(exception));
            if (activationStart.Microseconds >= activationEnd.Microseconds)
                throw new ArgumentException("활성 구간은 비어 있지 않은 반개구간이어야 합니다.", nameof(activationEnd));
            ActivationStart = activationStart; ActivationEnd = activationEnd;
            Review = ContractGuard.NotNull(review, nameof(review));
        }
    }

    /// <summary>활성 후보 판단이지 명령 실행·기관 승인·원문 진위 확인 결과가 아니다.</summary>
    public sealed class RuleActivationDecision
    {
        public RuleClause Clause { get; }
        public RuleTruth GuardTruth { get; }
        public RuleTruth? ExceptionTruth { get; }
        public RuleActivationReason Reason { get; }
        public bool IsActive => Reason == RuleActivationReason.ACTIVE;
        public RuleActivationDecision(RuleClause clause, SimTick currentTick, RuleFacts facts)
        {
            Clause = ContractGuard.NotNull(clause, nameof(clause));
            GuardTruth = clause.Guard.Evaluate(facts);
            ExceptionTruth = clause.Exception == null ? (RuleTruth?)null : clause.Exception.Evaluate(facts);
            if (currentTick.Microseconds < clause.ActivationStart.Microseconds || currentTick.Microseconds >= clause.ActivationEnd.Microseconds)
                Reason = RuleActivationReason.OUTSIDE_ACTIVATION_INTERVAL;
            else if (clause.Kind == RuleKind.UNKNOWN) Reason = RuleActivationReason.UNKNOWN_KIND;
            else if (clause.Review.State == RuleReviewState.UNREVIEWED) Reason = RuleActivationReason.UNREVIEWED;
            else if (clause.Review.State == RuleReviewState.REJECTED) Reason = RuleActivationReason.REJECTED;
            else if (!clause.Review.ReviewerId.HasValue || clause.Review.ReviewedRuleRevision != clause.Revision)
                Reason = RuleActivationReason.INVALID_APPROVAL;
            else if (GuardTruth != RuleTruth.TRUE) Reason = RuleActivationReason.GUARD_NOT_TRUE;
            else if (ExceptionTruth.HasValue && ExceptionTruth.Value != RuleTruth.FALSE) Reason = RuleActivationReason.EXCEPTION_NOT_FALSE;
            else Reason = RuleActivationReason.ACTIVE;
        }
    }
}
