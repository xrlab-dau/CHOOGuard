#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.Contracts;
using ChooGuard.Content;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class CSPACK0201Tests
    {
        private static StableId Id(string value) => new StableId(value);
        private static RuleExpression C(RuleTruth value) => RuleExpression.Constant(value);
        private static RuleClause Clause(string id = "r", long revision = 1, long start = 10, long end = 20,
            RuleKind kind = RuleKind.REQUIRED, RuleExpression guard = null, RuleExpression exception = null, RuleReview review = null)
            => new RuleClause(Id(id), revision, Id("agency"), Id("jurisdiction"), Id("scenario"), Id("event"),
                // 시험 전용 참조이며 실제 원문 취득을 뜻하지 않는다.
                new ContentReference(Id("fixture"), 0, new string('a', 64)), "제1절", kind,
                guard ?? C(RuleTruth.TRUE), "대피 경로를 검토한다", exception, new SimTick(start), new SimTick(end),
                review ?? new RuleReview(RuleReviewState.APPROVED, Id("reviewer"), revision));
        private static RuleScope Scope(string agency = "agency", string jurisdiction = "jurisdiction", string scenario = "scenario", string evt = "event")
            => new RuleScope(Id(agency), Id(jurisdiction), Id(scenario), Id(evt));
        private static RuleActivationDecision Decision(RuleClause clause, RuleFacts facts = null)
            => new RuleCatalog(new[] { clause }).Query(Scope(), new SimTick(10), facts).Single();

        [Test]
        public void FourValueTablesAndEmptyIdentitiesAreExplicit()
        {
            var values = new[] { RuleTruth.TRUE, RuleTruth.FALSE, RuleTruth.UNKNOWN, RuleTruth.CONFLICTED };
            var and = new[,] { { 0, 1, 2, 3 }, { 1, 1, 1, 1 }, { 2, 1, 2, 3 }, { 3, 1, 3, 3 } };
            var or = new[,] { { 0, 0, 0, 0 }, { 0, 1, 2, 3 }, { 0, 2, 2, 3 }, { 0, 3, 3, 3 } };
            var not = new[] { RuleTruth.FALSE, RuleTruth.TRUE, RuleTruth.UNKNOWN, RuleTruth.CONFLICTED };
            for (var i = 0; i < 4; i++)
            {
                Assert.That(RuleExpression.Not(C(values[i])).Evaluate(null), Is.EqualTo(not[i]));
                for (var j = 0; j < 4; j++)
                {
                    Assert.That(RuleExpression.All(new[] { C(values[i]), C(values[j]) }).Evaluate(null), Is.EqualTo(values[and[i, j]]));
                    Assert.That(RuleExpression.Any(new[] { C(values[i]), C(values[j]) }).Evaluate(null), Is.EqualTo(values[or[i, j]]));
                }
            }
            Assert.That(RuleExpression.All(new RuleExpression[0]).Evaluate(null), Is.EqualTo(RuleTruth.TRUE));
            Assert.That(RuleExpression.Any(new RuleExpression[0]).Evaluate(null), Is.EqualTo(RuleTruth.FALSE));
        }

        [Test]
        public void MissingAndNullFactsStayUnknownAndInputsAreCopied()
        {
            var input = new Dictionary<StableId, RuleTruth?> { [Id("known")] = RuleTruth.TRUE, [Id("null")] = null };
            var facts = new RuleFacts(input);
            input[Id("known")] = RuleTruth.FALSE;
            Assert.That(RuleExpression.Fact(Id("known")).Evaluate(facts), Is.EqualTo(RuleTruth.TRUE));
            foreach (var key in new[] { "missing", "null" })
                Assert.That(RuleExpression.Not(RuleExpression.Fact(Id(key))).Evaluate(facts), Is.EqualTo(RuleTruth.UNKNOWN));
            Assert.That(RuleExpression.Fact(Id("known")).Evaluate(null), Is.EqualTo(RuleTruth.UNKNOWN));
            Assert.That(Decision(Clause(guard: RuleExpression.Fact(Id("missing"))), facts).IsActive, Is.False);
        }

        [Test]
        public void ScopeIsExactAndActivationUsesHalfOpenTicks()
        {
            var catalog = new RuleCatalog(new[] { Clause() });
            foreach (var scope in new[] { Scope(agency: "Agency"), Scope(jurisdiction: "other"), Scope(scenario: "other"), Scope(evt: "other") })
                Assert.That(catalog.Query(scope, new SimTick(10), null), Is.Empty);
            Assert.That(catalog.Query(Scope(), new SimTick(9), null), Is.Empty);
            Assert.That(catalog.Query(Scope(), new SimTick(10), null).Single().IsActive, Is.True);
            Assert.That(catalog.Query(Scope(), new SimTick(19), null).Single().IsActive, Is.True);
            Assert.That(catalog.Query(Scope(), new SimTick(20), null), Is.Empty);
        }

        [TestCase(RuleTruth.TRUE, false)][TestCase(RuleTruth.FALSE, true)]
        [TestCase(RuleTruth.UNKNOWN, false)][TestCase(RuleTruth.CONFLICTED, false)]
        public void ExceptionRequiresDeterminateFalse(RuleTruth truth, bool active)
            => Assert.That(Decision(Clause(exception: C(truth))).IsActive, Is.EqualTo(active));

        [TestCase(RuleTruth.FALSE)][TestCase(RuleTruth.UNKNOWN)][TestCase(RuleTruth.CONFLICTED)]
        public void GuardRequiresDeterminateTrue(RuleTruth truth)
        {
            var decision = Decision(Clause(guard: C(truth)));
            Assert.That(decision.IsActive, Is.False);
            Assert.That(decision.GuardTruth, Is.EqualTo(truth));
            Assert.That(decision.Reason, Is.EqualTo(RuleActivationReason.GUARD_NOT_TRUE));
        }

        [Test]
        public void UnknownKindAndUnreviewedCandidatesRemainVisibleButInactive()
        {
            Assert.That(Decision(Clause(kind: RuleKind.UNKNOWN)).Reason, Is.EqualTo(RuleActivationReason.UNKNOWN_KIND));
            var unreviewed = new RuleReview(RuleReviewState.UNREVIEWED, null, null);
            var decision = Decision(Clause(review: unreviewed));
            Assert.That(decision.Clause.Review.State, Is.EqualTo(RuleReviewState.UNREVIEWED));
            Assert.That(decision.IsActive, Is.False);
            Assert.That(decision.Reason, Is.EqualTo(RuleActivationReason.UNREVIEWED));
            foreach (var kind in new[] { RuleKind.REQUIRED, RuleKind.DISCRETIONARY, RuleKind.ADVISORY, RuleKind.INVARIANT })
                Assert.That(Decision(Clause(kind: kind)).IsActive, Is.True);
        }

        [Test]
        public void ApprovalNeedsReviewerAndMatchingRevisionWithoutRewritingReviewState()
        {
            foreach (var review in new[] {
                new RuleReview(RuleReviewState.APPROVED, null, 1),
                new RuleReview(RuleReviewState.APPROVED, Id("reviewer"), null),
                new RuleReview(RuleReviewState.APPROVED, Id("reviewer"), 0) })
            {
                var decision = Decision(Clause(review: review));
                Assert.That(decision.IsActive, Is.False);
                Assert.That(decision.Reason, Is.EqualTo(RuleActivationReason.INVALID_APPROVAL));
                Assert.That(decision.Clause.Review.State, Is.EqualTo(RuleReviewState.APPROVED));
            }
            Assert.That(Decision(Clause(review: new RuleReview(RuleReviewState.REJECTED, null, null))).Reason,
                Is.EqualTo(RuleActivationReason.REJECTED));
        }

        [Test]
        public void DuplicateAndOverlappingRevisionsAreRejectedButAdjacentWindowsAreAllowed()
        {
            Assert.Throws<ArgumentException>(() => new RuleCatalog(new[] { Clause(), Clause(start: 20, end: 30) }));
            Assert.Throws<ArgumentException>(() => new RuleCatalog(new[] { Clause(), Clause(revision: 2, start: 19, end: 30) }));
            var catalog = new RuleCatalog(new[] { Clause(revision: 2, start: 20, end: 30), Clause() });
            Assert.That(catalog.Clauses.Select(c => c.Revision), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(catalog.Query(Scope(), new SimTick(20), null).Single().Clause.Revision, Is.EqualTo(2));
        }

        [Test]
        public void CollectionsAreCopiedReadOnlyAndSortingIsOrdinal()
        {
            var children = new[] { C(RuleTruth.TRUE) };
            var expression = RuleExpression.All(children);
            children[0] = C(RuleTruth.FALSE);
            Assert.That(expression.Evaluate(null), Is.EqualTo(RuleTruth.TRUE));
            Assert.Throws<NotSupportedException>(() => ((IList<RuleExpression>)expression.Children).Clear());
            var clauses = new List<RuleClause> { Clause("z"), Clause("A") };
            var catalog = new RuleCatalog(clauses);
            clauses.Clear();
            Assert.That(catalog.Clauses.Select(c => c.Id.Value), Is.EqualTo(new[] { "A", "z" }));
            Assert.Throws<NotSupportedException>(() => ((IList<RuleClause>)catalog.Clauses).Clear());
            var result = catalog.Query(Scope(), new SimTick(10), null);
            Assert.That(result.Select(d => d.Clause.Id.Value), Is.EqualTo(new[] { "A", "z" }));
            Assert.Throws<NotSupportedException>(() => ((IList<RuleActivationDecision>)result).Clear());
        }

        [Test]
        public void AstDepthChildrenAndExpandedNodeBudgetAreBounded()
        {
            var nested = C(RuleTruth.TRUE);
            for (var i = 1; i < RuleExpression.MaxDepth; i++) nested = RuleExpression.Not(nested);
            Assert.That(nested.Depth, Is.EqualTo(32));
            Assert.Throws<ArgumentException>(() => RuleExpression.Not(nested));
            Assert.Throws<ArgumentException>(() => RuleExpression.All(Enumerable.Repeat(C(RuleTruth.TRUE), RuleExpression.MaxChildren + 1)));
            var block = RuleExpression.All(Enumerable.Repeat(C(RuleTruth.TRUE), 255));
            var exact = RuleExpression.All(Enumerable.Repeat(block, 15).Concat(new[] { RuleExpression.All(Enumerable.Repeat(C(RuleTruth.TRUE), 254)) }));
            Assert.That(exact.NodeCount, Is.EqualTo(4096));
            Assert.Throws<ArgumentException>(() => RuleExpression.Not(exact));
            Assert.Throws<ArgumentException>(() => RuleExpression.All(Enumerable.Repeat(block, 16)));
        }

        [Test]
        public void ClauseRejectsMissingFieldsAndCombinedAstBudget()
        {
            var valid = Clause();
            Func<ContentReference, string, RuleExpression, string, RuleReview, RuleClause> make = (source, locator, guard, effect, review) =>
                new RuleClause(Id("r"), 1, Id("a"), Id("j"), Id("s"), Id("e"), source, locator,
                    RuleKind.REQUIRED, guard, effect, null, new SimTick(0), new SimTick(1), review);
            Assert.Throws<ArgumentNullException>(() => make(null, "절", valid.Guard, "효과", valid.Review));
            Assert.Throws<ArgumentException>(() => make(valid.Source, " ", valid.Guard, "효과", valid.Review));
            Assert.Throws<ArgumentNullException>(() => make(valid.Source, "절", null, "효과", valid.Review));
            Assert.Throws<ArgumentException>(() => make(valid.Source, "절", valid.Guard, null, valid.Review));
            Assert.Throws<ArgumentNullException>(() => make(valid.Source, "절", valid.Guard, "효과", null));
            var block = RuleExpression.All(Enumerable.Repeat(C(RuleTruth.TRUE), 255));
            var large = RuleExpression.All(Enumerable.Repeat(block, 8));
            Assert.Throws<ArgumentException>(() => Clause(guard: large, exception: large));
            Assert.That(new RuleActivationDecision(valid, new SimTick(20), null).Reason,
                Is.EqualTo(RuleActivationReason.OUTSIDE_ACTIVATION_INTERVAL));
            Assert.Throws<ArgumentNullException>(() => new RuleActivationDecision(null, new SimTick(0), null));
        }

        [Test]
        public void MalformedPublicInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => C((RuleTruth)99));
            Assert.Throws<ArgumentException>(() => RuleExpression.Fact(default(StableId)));
            Assert.Throws<ArgumentNullException>(() => RuleExpression.Not(null));
            Assert.Throws<ArgumentNullException>(() => RuleExpression.All(null));
            Assert.Throws<ArgumentNullException>(() => RuleExpression.Any(new RuleExpression[] { null }));
            Assert.Throws<ArgumentNullException>(() => new RuleFacts(null));
            Assert.Throws<ArgumentException>(() => new RuleFacts(new[] { new KeyValuePair<StableId, RuleTruth?>(default(StableId), null) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuleFacts(new[] { new KeyValuePair<StableId, RuleTruth?>(Id("f"), (RuleTruth)99) }));
            Assert.Throws<ArgumentException>(() => new RuleFacts(new[] { new KeyValuePair<StableId, RuleTruth?>(Id("f"), null), new KeyValuePair<StableId, RuleTruth?>(Id("f"), null) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuleReview((RuleReviewState)99, null, null));
            Assert.Throws<ArgumentException>(() => new RuleReview(RuleReviewState.APPROVED, default(StableId), 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuleReview(RuleReviewState.APPROVED, null, -1));
            Assert.Throws<ArgumentException>(() => new RuleScope(default(StableId), Id("j"), Id("s"), Id("e")));
            Assert.Throws<ArgumentOutOfRangeException>(() => Clause(kind: (RuleKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Clause(revision: -1));
            Assert.Throws<ArgumentException>(() => Clause(start: 10, end: 10));
            Assert.Throws<ArgumentException>(() => Clause(start: 20, end: 10));
            Assert.Throws<ArgumentNullException>(() => new RuleCatalog(null));
            Assert.Throws<ArgumentNullException>(() => new RuleCatalog(new RuleClause[] { null }));
            Assert.Throws<ArgumentNullException>(() => new RuleCatalog(new RuleClause[0]).Query(null, new SimTick(0), null));
        }
    }
}
#endif
