#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.Contracts;
using ChooGuard.Domain;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    // 순수 후보 계산 시험이며 SQL commit 또는 receipt 수용 시험이 아니다.
    public class CSOPS0202Tests
    {
        private readonly ReservationPlanner planner = new ReservationPlanner();
        private static StableId Id(string s) => new StableId(s);
        private static ReservationDemand D(long q = 1, string id = "r", string unit = "count") => new ReservationDemand(Id(id), unit, q);
        private static ReservationRequest Q(long start = 10, long end = 20, params ReservationDemand[] demands) =>
            new ReservationRequest(Id("new"), Id("owner"), new SimTick(start), new SimTick(end), demands.Length == 0 ? new[] { D() } : demands);
        private static ActiveReservation A(string id, long start, long end, long quantity = 1, bool active = true) =>
            new ActiveReservation(Id(id), Id("owner"), 2, new SimTick(start), new SimTick(end), new[] { D(quantity) }, active);
        private ReservationPlanResult Plan(ReservationRequest request, long capacity, params ActiveReservation[] active) =>
            planner.Plan(request, new[] { new ReservationResource(Id("r"), "count", capacity) }, active);

        [TestCase(0, 10, ReservationPlanStatus.Planned)]
        [TestCase(20, 30, ReservationPlanStatus.Planned)]
        [TestCase(9, 11, ReservationPlanStatus.ResourceUnavailable)]
        [TestCase(19, 21, ReservationPlanStatus.ResourceUnavailable)]
        public void HalfOpenWindow_UsesStrictIntersection(long start, long end, ReservationPlanStatus expected)
        { Assert.That(Plan(Q(), 1, A("old", start, end)).Status, Is.EqualTo(expected)); }

        [Test]
        public void DisjointExistingIntervals_AreNotSummedTogether()
        { Assert.That(Plan(Q(), 2, A("a", 0, 15), A("b", 15, 30)).Status, Is.EqualTo(ReservationPlanStatus.Planned)); }

        [Test]
        public void NestedIntervals_AndExactCapacity_AreEvaluatedAtEveryBoundary()
        {
            Assert.That(Plan(Q(), 3, A("a", 0, 30), A("b", 12, 18)).Status, Is.EqualTo(ReservationPlanStatus.Planned));
            Assert.That(Plan(Q(), 2, A("a", 0, 30), A("b", 12, 18)).Status, Is.EqualTo(ReservationPlanStatus.ResourceUnavailable));
        }

        [Test]
        public void Overflow_IsClassifiedWithoutWrapping()
        {
            Assert.That(Plan(Q(), long.MaxValue, A("a", 0, 30, long.MaxValue)).Status, Is.EqualTo(ReservationPlanStatus.ResourceUnavailable));
            Assert.That(Plan(Q(), long.MaxValue, A("a", 0, 30, long.MaxValue), A("b", 12, 18)).Status, Is.EqualTo(ReservationPlanStatus.ResourceDeficit));
            Assert.That(Plan(Q(10, 20, D(long.MaxValue)), long.MaxValue).Status, Is.EqualTo(ReservationPlanStatus.Planned));
        }

        [Test]
        public void Inactive_IsIgnored_AndDeficitDoesNotDeleteReservations()
        {
            var old = A("a", 0, 30, 3);
            Assert.That(Plan(Q(), 1, A("inactive", 0, 30, 3, false)).Status, Is.EqualTo(ReservationPlanStatus.Planned));
            Assert.That(Plan(Q(), 1, old).Status, Is.EqualTo(ReservationPlanStatus.ResourceDeficit));
            Assert.That(old.IsActive, Is.True);
        }

        [Test]
        public void MultiResourceFailure_HasNoPartialCandidate_AndOrdinalLocks()
        {
            var resources = new[] { new ReservationResource(Id("z"), "count", 1), new ReservationResource(Id("A"), "count", 0) };
            var result = planner.Plan(Q(0, 1, D(1, "z"), D(1, "A")), resources, new ActiveReservation[0]);
            Assert.That(result.Status, Is.EqualTo(ReservationPlanStatus.ResourceUnavailable));
            Assert.That(result.ProposedReservations, Is.Empty);
            Assert.That(result.LockOrder.Select(x => x.Value), Is.EqualTo(new[] { "A", "z" }));
        }

        [Test]
        public void UnknownResource_UnitMismatch_AndDuplicateSnapshots_AreInvalid()
        {
            Assert.That(Plan(Q(10, 20, D(1, "unknown")), 1).Status, Is.EqualTo(ReservationPlanStatus.Invalid));
            Assert.That(Plan(Q(10, 20, D(1, "r", "ml")), 1).Status, Is.EqualTo(ReservationPlanStatus.Invalid));
            Assert.That(Plan(Q(), 2, A("a", 0, 1), A("a", 2, 3)).Status, Is.EqualTo(ReservationPlanStatus.Invalid));
            var r = new ReservationResource(Id("r"), "count", 1);
            Assert.That(planner.Plan(Q(), new[] { r, r }, new ActiveReservation[0]).Status, Is.EqualTo(ReservationPlanStatus.Invalid));
            Assert.That(Plan(Q(), 2, A("new", 0, 1, 1, false)).Status, Is.EqualTo(ReservationPlanStatus.ReservationIdConflict));
        }

        [Test]
        public void InvalidValues_AndDuplicateDemands_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => Q(2, 2));
            Assert.Throws<ArgumentException>(() => Q(3, 2));
            Assert.Throws<ArgumentException>(() => Q(0, 1, D(), D()));
            Assert.Throws<ArgumentException>(() => new ReservationResource(default(StableId), "count", 0));
            Assert.Throws<ArgumentException>(() => new ReservationResource(Id("r"), "bad unit", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReservationResource(Id("r"), "count", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => D(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReservationCancellation(Id("a"), Id("owner"), -1));
            Assert.Throws<ArgumentException>(() => new ReservationRequest(Id("a"), default(StableId), new SimTick(0), new SimTick(1), new[] { D() }));
            Assert.Throws<ArgumentException>(() => new ReservationRequest(Id("a"), Id("owner"), new SimTick(0), new SimTick(1), new ReservationDemand[0]));
            Assert.That(planner.Plan(null, null, null).Status, Is.EqualTo(ReservationPlanStatus.Invalid));
            Assert.That(planner.ValidateCancellation(null, null).Status, Is.EqualTo(ReservationCancellationStatus.Invalid));
        }

        [Test]
        public void Cancellation_RequiresOwnerIdVersion_AndRepeatIsNoOp()
        {
            var old = A("a", 0, 10);
            var other = A("b", 0, 10);
            var all = new[] { old, other };
            Assert.That(planner.ValidateCancellation(new ReservationCancellation(Id("a"), Id("foreign"), 2), all).Status, Is.EqualTo(ReservationCancellationStatus.OwnerMismatch));
            Assert.That(planner.ValidateCancellation(new ReservationCancellation(Id("a"), Id("owner"), 1), all).Status, Is.EqualTo(ReservationCancellationStatus.VersionMismatch));
            Assert.That(planner.ValidateCancellation(new ReservationCancellation(Id("missing"), Id("owner"), 2), all).Status, Is.EqualTo(ReservationCancellationStatus.NotFound));
            Assert.That(planner.ValidateCancellation(new ReservationCancellation(Id("a"), Id("owner"), 2), all).Status, Is.EqualTo(ReservationCancellationStatus.Ready));
            Assert.That(planner.ValidateCancellation(new ReservationCancellation(Id("a"), Id("owner"), 2), new[] { A("a", 0, 10, 1, false), other }).Status, Is.EqualTo(ReservationCancellationStatus.AlreadyInactive));
            Assert.That(old.IsActive && other.IsActive, Is.True);
        }

        [Test]
        public void LaterDeficit_TakesPrecedenceOverEarlierUnavailability()
        {
            Assert.That(Plan(Q(), 1, A("a", 10, 15), A("b", 16, 20, 2)).Status, Is.EqualTo(ReservationPlanStatus.ResourceDeficit));
        }

        [Test]
        public void NullSnapshotEntries_AndCancellationDuplicates_AreInvalid()
        {
            Assert.That(Plan(Q(), 1, (ActiveReservation)null).Status, Is.EqualTo(ReservationPlanStatus.Invalid));
            Assert.That(planner.Plan(Q(), new ReservationResource[] { null }, new ActiveReservation[0]).Status, Is.EqualTo(ReservationPlanStatus.Invalid));
            var cancel = new ReservationCancellation(Id("a"), Id("owner"), 2);
            Assert.That(planner.ValidateCancellation(cancel, new[] { A("a", 0, 1), A("a", 1, 2) }).Status, Is.EqualTo(ReservationCancellationStatus.Invalid));
            Assert.That(planner.ValidateCancellation(cancel, new ActiveReservation[] { null }).Status, Is.EqualTo(ReservationCancellationStatus.Invalid));
            Assert.That(planner.ValidateCancellation(new ReservationCancellation(Id("a"), Id("foreign"), 2), new[] { A("a", 0, 1, 1, false) }).Status, Is.EqualTo(ReservationCancellationStatus.OwnerMismatch));
            Assert.That(planner.ValidateCancellation(new ReservationCancellation(Id("a"), Id("owner"), 1), new[] { A("a", 0, 1, 1, false) }).Status, Is.EqualTo(ReservationCancellationStatus.VersionMismatch));
        }

        [Test]
        public void InputCollections_AreCopied_AndOutputsAreReadOnly()
        {
            var demands = new List<ReservationDemand> { D() };
            var request = new ReservationRequest(Id("new"), Id("owner"), new SimTick(10), new SimTick(20), demands);
            var old = new ActiveReservation(Id("old"), Id("owner"), 0, new SimTick(0), new SimTick(1), demands, true);
            demands.Clear();
            var result = Plan(request, 1, old);
            Assert.That(request.Demands.Count, Is.EqualTo(1));
            Assert.That(old.Demands.Count, Is.EqualTo(1));
            Assert.That(result.ProposedReservations.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<ReservationDemand>)request.Demands).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<StableId>)result.LockOrder).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ReservationRequest>)result.ProposedReservations).Clear());
        }
    }
}
#endif
