#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using ChooGuard.Contracts;
using ChooGuard.Domain;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    // 합성 권한 입력에 대한 순수 판정 시험이며 실제 기관 승인/저장 시험이 아니다.
    public class CSOPS0301Tests
    {
        private static StableId Id(string s) => new StableId(s);
        private static StableId[] Ids(params string[] s) => Array.ConvertAll(s, Id);
        private static ContentReference Rule() => new ContentReference(Id("rule"), 4, new string('a', 64));
        private static AuthorityGrant Grant(string id = "grant", string holder = "user", string agency = "A",
            string team = "team", string action = "assign", string target = "target") =>
            new AuthorityGrant(Id(id), 7, Id(holder), Id(agency), Ids(team), Ids(action), Ids(target), Rule());
        private static Dictionary<StableId, StableId> Teams() => new Dictionary<StableId, StableId>
            { { Id("team"), Id("A") }, { Id("team2"), Id("A") }, { Id("foreign"), Id("B") } };
        private static AuthoritySnapshot Snapshot(params AuthorityGrant[] grants) =>
            new AuthoritySnapshot(Id("run"), grants, Teams(), new Dictionary<StableId, StableId> { { Id("target"), Id("A") } });
        private static CommandIntent Intent(string holder = "user", string agency = "A", string action = "assign", string target = "target", params string[] teams) =>
            new CommandIntent(new ReceiptKey(Id("run"), Id(holder), Id("intent")), Id(agency),
                Ids(teams.Length == 0 ? new[] { "team" } : teams), Id(action), Ids(target),
                new[] { new EntityRevision(Id(target), 0) }, Rule(), new UtcTimestamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        private static SupportRequest Request(long revision = 0) => new SupportRequest(Id("run"), Id("request"), revision, Id("A"), Id("B"), Id("foreign"));

        [Test]
        public void InternalGrant_AllowsAndRetainsCanonicalEvidence()
        {
            var grant = Grant();
            var decision = AuthorityPolicy.Evaluate(Intent(), Snapshot(grant));
            Assert.That(decision.Allowed, Is.True);
            Assert.That(decision.Grant, Is.SameAs(grant));
            Assert.That(decision.Grant.Revision, Is.EqualTo(7));
            Assert.That(decision.Grant.RuleRef.Revision, Is.EqualTo(4));
            Assert.That(decision.Reasons, Is.Empty);
        }

        [TestCase("other", "A", "assign", "target", "team")]
        [TestCase("user", "B", "assign", "target", "team")]
        [TestCase("user", "A", "other", "target", "team")]
        [TestCase("user", "A", "assign", "other", "team")]
        [TestCase("user", "A", "assign", "target", "team2")]
        public void ExactScopeMismatch_Denies(string holder, string agency, string action, string target, string team)
        {
            Assert.That(AuthorityPolicy.Evaluate(Intent(holder, agency, action, target, team), Snapshot(Grant())).Allowed, Is.False);
        }

        [Test]
        public void SameDisplayName_DoesNotMergeIdentities()
        {
            var first = new NamedIdentity(Id("user"), "동일 이름");
            var second = new NamedIdentity(Id("other"), "동일 이름");
            Assert.That(AuthorityPolicy.Evaluate(Intent(second.Id.Value), Snapshot(Grant(holder: first.Id.Value))).Allowed, Is.False);
        }

        [Test]
        public void PartialAndCombinedGrants_CannotCoverOneCommand()
        {
            var intent = Intent("user", "A", "assign", "target", "team", "team2");
            Assert.That(AuthorityPolicy.Evaluate(intent, Snapshot(Grant())).Allowed, Is.False);
            Assert.That(AuthorityPolicy.Evaluate(intent, Snapshot(Grant(), Grant("grant2", team: "team2"))).Allowed, Is.False);
        }

        [Test]
        public void MultipleTeamsAndTargets_RequireOneCompleteGrant()
        {
            var intent = new CommandIntent(new ReceiptKey(Id("run"), Id("user"), Id("multi")), Id("A"), Ids("team", "team2"),
                Id("assign"), Ids("target", "target2"), new[] { new EntityRevision(Id("target"), 0) }, Rule(),
                new UtcTimestamp(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            var targets = new Dictionary<StableId, StableId> { { Id("target"), Id("A") }, { Id("target2"), Id("A") } };
            var full = new AuthorityGrant(Id("full"), 0, Id("user"), Id("A"), Ids("team", "team2"), Ids("assign"), Ids("target", "target2"), Rule());
            var partial = new AuthorityGrant(Id("partial"), 0, Id("user"), Id("A"), Ids("team", "team2"), Ids("assign"), Ids("target"), Rule());
            Assert.That(AuthorityPolicy.Evaluate(intent, new AuthoritySnapshot(Id("run"), new[] { full }, Teams(), targets)).Allowed, Is.True);
            Assert.That(AuthorityPolicy.Evaluate(intent, new AuthoritySnapshot(Id("run"), new[] { partial }, Teams(), targets)).Allowed, Is.False);
        }

        [Test]
        public void ForeignOrUnknownBindings_DenyEvenWithExplicitGrant()
        {
            var foreign = new AuthoritySnapshot(Id("run"), new[] { Grant() }, Teams(),
                new Dictionary<StableId, StableId> { { Id("target"), Id("B") } });
            Assert.That(AuthorityPolicy.Evaluate(Intent(), foreign).Reasons, Does.Contain(AuthorityDenial.ForeignTarget));
            Assert.That(AuthorityPolicy.Evaluate(Intent("user", "A", "assign", "target", "foreign"), Snapshot(Grant(team: "foreign"))).Reasons,
                Does.Contain(AuthorityDenial.ForeignTeam));
            var unknown = new AuthoritySnapshot(Id("run"), new[] { Grant() }, new Dictionary<StableId, StableId>(), new Dictionary<StableId, StableId>());
            Assert.That(AuthorityPolicy.Evaluate(Intent(), unknown).Reasons, Is.EqualTo(new[] { AuthorityDenial.UnknownTeam, AuthorityDenial.UnknownTarget }));
        }

        [Test]
        public void MissingAuthorityEmptyScopesAndWrongRun_Deny()
        {
            Assert.That(AuthorityPolicy.Evaluate(Intent(), null).Allowed, Is.False);
            Assert.That(AuthorityPolicy.Evaluate(null, Snapshot()).Allowed, Is.False);
            Assert.That(AuthorityPolicy.Evaluate(Intent(), Snapshot()).Allowed, Is.False);
            var empty = new AuthorityGrant(Id("empty"), 0, Id("user"), Id("A"), Ids(), Ids(), Ids(), Rule());
            Assert.That(AuthorityPolicy.Evaluate(Intent(), Snapshot(empty)).Allowed, Is.False);
            var otherRun = new AuthoritySnapshot(Id("other"), new[] { Grant() }, Teams(), new Dictionary<StableId, StableId>());
            Assert.That(AuthorityPolicy.Evaluate(Intent(), otherRun).Reasons, Is.EqualTo(new[] { AuthorityDenial.WrongRun }));
        }

        [Test]
        public void InputsAreCopiedAndResultsReadOnly()
        {
            var teams = Ids("team");
            var actions = Ids("assign");
            var targets = Ids("target");
            var grant = new AuthorityGrant(Id("g"), 0, Id("user"), Id("A"), teams, actions, targets, Rule());
            var grants = new List<AuthorityGrant> { grant };
            var bindings = Teams();
            var targetBindings = new Dictionary<StableId, StableId> { { Id("target"), Id("A") } };
            var snapshot = new AuthoritySnapshot(Id("run"), grants, bindings, targetBindings);
            teams[0] = Id("other"); actions[0] = Id("other"); targets[0] = Id("other");
            grants.Clear(); bindings.Clear(); targetBindings.Clear();
            Assert.That(AuthorityPolicy.Evaluate(Intent(), snapshot).Allowed, Is.True);
            Assert.Throws<NotSupportedException>(() => ((IList<StableId>)grant.TeamIds).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<StableId>)grant.ActionIds).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<StableId>)grant.TargetIds).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AuthorityGrant>)snapshot.Grants).Clear());
            Assert.Throws<NotSupportedException>(() => ((IDictionary<StableId, StableId>)snapshot.TeamAgencies).Clear());
            Assert.Throws<NotSupportedException>(() => ((IDictionary<StableId, StableId>)snapshot.TargetAgencies).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AuthorityDenial>)AuthorityPolicy.Evaluate(Intent(), null).Reasons).Clear());
        }

        [Test]
        public void GrantSelection_IsIndependentOfInputOrderAndDuplicateIdsRejected()
        {
            var a = Grant("a"); var z = Grant("z");
            Assert.That(AuthorityPolicy.Evaluate(Intent(), Snapshot(z, a)).Grant.GrantId, Is.EqualTo(Id("a")));
            Assert.Throws<ArgumentException>(() => Snapshot(a, a));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AuthorityGrant(Id("g"), -1, Id("user"), Id("A"), Ids(), Ids(), Ids(), Rule()));
            Assert.Throws<ArgumentException>(() => new AuthorityGrant(default(StableId), 0, Id("user"), Id("A"), Ids(), Ids(), Ids(), Rule()));
            Assert.Throws<ArgumentNullException>(() => new AuthorityGrant(Id("g"), 0, Id("user"), Id("A"), Ids(), Ids(), Ids(), null));
        }

        [Test]
        public void SupportRequestedAcceptedAssigned_AreDistinctImmutableStates()
        {
            var requested = Request();
            Assert.That(requested.Status, Is.EqualTo(SupportStatus.Requested));
            var accepted = requested.Accept(Id("B"), 0, Snapshot()).State;
            Assert.That(accepted.Status, Is.EqualTo(SupportStatus.Accepted));
            var assigned = accepted.Assign(Id("B"), 1, Snapshot()).State;
            Assert.That(assigned.Status, Is.EqualTo(SupportStatus.Assigned));
            Assert.That(assigned.Revision, Is.EqualTo(2));
            Assert.That(requested.Status, Is.EqualTo(SupportStatus.Requested));
            Assert.That(requested.Revision, Is.Zero);
            Assert.That(accepted.Revision, Is.EqualTo(1));
            Assert.That(assigned.Assign(Id("B"), 2, Snapshot()).Denial, Is.EqualTo(SupportDenial.IllegalTransition));
            Assert.That(assigned.RequestId, Is.EqualTo(requested.RequestId));
            Assert.That(assigned.RequesterAgencyId, Is.EqualTo(Id("A")));
            Assert.That(assigned.ReceiverAgencyId, Is.EqualTo(Id("B")));
            Assert.That(assigned.TargetTeamId, Is.EqualTo(Id("foreign")));
        }

        [Test]
        public void SupportStaleForeignUnknownAndIllegalTransitions_DenyWithoutMutation()
        {
            var request = Request();
            Assert.That(request.Accept(Id("B"), 1, Snapshot()).Denial, Is.EqualTo(SupportDenial.StaleVersion));
            Assert.That(request.Accept(Id("A"), 0, Snapshot()).Denial, Is.EqualTo(SupportDenial.ForeignReceiver));
            Assert.That(request.Assign(Id("B"), 0, Snapshot()).Denial, Is.EqualTo(SupportDenial.IllegalTransition));
            var unknown = new AuthoritySnapshot(Id("run"), new AuthorityGrant[0], new Dictionary<StableId, StableId>(), new Dictionary<StableId, StableId>());
            Assert.That(request.Accept(Id("B"), 0, unknown).Denial, Is.EqualTo(SupportDenial.UnknownTeam));
            var reassigned = Teams(); reassigned[Id("foreign")] = Id("A");
            var moved = new AuthoritySnapshot(Id("run"), new AuthorityGrant[0], reassigned, new Dictionary<StableId, StableId>());
            var accepted = request.Accept(Id("B"), 0, Snapshot()).State;
            Assert.That(accepted.Assign(Id("B"), 1, moved).Denial, Is.EqualTo(SupportDenial.ForeignTeam));
            Assert.That(accepted.Assign(Id("A"), 1, Snapshot()).Denial, Is.EqualTo(SupportDenial.ForeignReceiver));
            Assert.That(accepted.Accept(Id("B"), 1, Snapshot()).Denial, Is.EqualTo(SupportDenial.IllegalTransition));
            Assert.That(accepted.Reject(Id("B"), 1, Snapshot()).Denial, Is.EqualTo(SupportDenial.IllegalTransition));
            var rejected = request.Reject(Id("B"), 0, Snapshot()).State;
            Assert.That(rejected.Status, Is.EqualTo(SupportStatus.Rejected));
            Assert.That(rejected.Accept(Id("B"), 1, Snapshot()).Denial, Is.EqualTo(SupportDenial.IllegalTransition));
            Assert.That(request.Accept(Id("A"), 0, Snapshot()).State, Is.SameAs(request));
        }

        [Test]
        public void SupportOverflowAndMissingAuthority_Deny()
        {
            var request = Request(long.MaxValue);
            Assert.That(request.Accept(Id("B"), long.MaxValue, Snapshot()).Denial, Is.EqualTo(SupportDenial.VersionExhausted));
            Assert.That(request.Revision, Is.EqualTo(long.MaxValue));
            Assert.That(Request().Accept(Id("B"), 0, null).Denial, Is.EqualTo(SupportDenial.MissingAuthority));
            Assert.Throws<ArgumentOutOfRangeException>(() => Request(-1));
        }
    }
}
#endif
