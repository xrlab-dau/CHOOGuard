using System;
using System.Linq;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;

namespace ChooGuard.Foundation.Tests
{
    public sealed class TrainDynamicsTests
    {
        [TestCase(20, 0, 0, 200, 20)]
        [TestCase(20, 2, 0, 240, 22)]
        [TestCase(20, 2, 4, 279.3333333333333, 24)]
        [TestCase(.5, 2, 4, 1.666666666666667, 4)]
        [TestCase(.03, 0, 0, .00045, .03)]
        public void BrakingStopsAtTheAnalyticDistanceAndTime(double speed, double delay, double buildup, double distance, double time)
        {
            var motion = new TrainMotionState { SignedSpeedMS = speed };
            Assert.That(TrainDynamics.RequestBrake(motion, new TrainBrakeProfile { DelaySeconds = delay, BuildUpSeconds = buildup, DecelerationMS2 = 1 }, "brake"), Is.True);
            TrainDynamics.AdvanceBrake(motion, 100);
            Assert.That(motion.ReferenceDistanceM, Is.EqualTo(distance).Within(1e-9));
            Assert.That(motion.BrakeElapsedSeconds, Is.EqualTo(time).Within(1e-9));
            Assert.That(motion.SignedSpeedMS, Is.Zero); Assert.That(motion.Phase, Is.EqualTo(TrainBrakePhase.Stopped));
        }

        [Test]
        public void TickPartitionAndReverseTravelKeepTheSameBrakingSolution()
        {
            var initial = new TrainMotionState { SignedSpeedMS = -20, ReferenceDistanceM = 1000 };
            TrainDynamics.RequestBrake(initial, new TrainBrakeProfile { DelaySeconds = 2, BuildUpSeconds = 4, DecelerationMS2 = 1 }, "reverse");
            var one = initial.Copy(); var many = initial.Copy(); TrainDynamics.AdvanceBrake(one, 40);
            for (var i = 0; i < 800; i++) TrainDynamics.AdvanceBrake(many, .05);
            Assert.That(many.ReferenceDistanceM, Is.EqualTo(one.ReferenceDistanceM).Within(1e-7));
            Assert.That(one.ReferenceDistanceM, Is.EqualTo(1000 - 279.3333333333333).Within(1e-7));
            Assert.That(many.SignedSpeedMS, Is.Zero);
        }

        [Test]
        public void RepeatedAndWeakerCommandsDoNotRestartDelayAndEscalationKeepsDeceleration()
        {
            var motion = new TrainMotionState { SignedSpeedMS = 20 };
            var service = new TrainBrakeProfile { DelaySeconds = 2, BuildUpSeconds = 4, DecelerationMS2 = 1 };
            TrainDynamics.RequestBrake(motion, service, "service"); TrainDynamics.AdvanceBrake(motion, 3);
            var acceleration = motion.DecelerationMS2; var elapsed = motion.BrakeElapsedSeconds;
            Assert.That(TrainDynamics.RequestBrake(motion, service, "service"), Is.False);
            Assert.That(TrainDynamics.RequestBrake(motion, service, "another-service-id"), Is.False);
            Assert.That(motion.BrakeElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(TrainDynamics.RequestBrake(motion, new TrainBrakeProfile { DelaySeconds = 3, BuildUpSeconds = 2, DecelerationMS2 = 2 }, "emergency"), Is.True);
            Assert.That(motion.DelayRemainingSeconds, Is.Zero);
            Assert.That(motion.DecelerationMS2, Is.EqualTo(acceleration));
            TrainDynamics.AdvanceBrake(motion, .1); Assert.That(motion.DecelerationMS2, Is.GreaterThan(acceleration));
        }

        [Test]
        public void HigherTargetWithSlowBuildUpCannotWeakenTheExistingBrakeEnvelope()
        {
            foreach (var delay in new[] { 0.0, 2.0 })
            foreach (var buildup in new[] { 0.0, 4.0 })
            {
                var service = new TrainMotionState { SignedSpeedMS = 20 };
                TrainDynamics.RequestBrake(service, new TrainBrakeProfile { DelaySeconds = delay, BuildUpSeconds = buildup, DecelerationMS2 = 1 }, "service");
                TrainDynamics.AdvanceBrake(service, delay > 0 ? 1 : .5);
                var escalated = service.Copy();
                TrainDynamics.RequestBrake(escalated, new TrainBrakeProfile { DelaySeconds = 9, BuildUpSeconds = 100, DecelerationMS2 = 2 }, "emergency");
                while (service.Phase != TrainBrakePhase.Stopped)
                {
                    TrainDynamics.AdvanceBrake(service, .05); TrainDynamics.AdvanceBrake(escalated, .05);
                    Assert.That(Math.Abs(escalated.SignedSpeedMS), Is.LessThanOrEqualTo(Math.Abs(service.SignedSpeedMS) + 1e-8));
                }
                Assert.That(escalated.ReferenceDistanceM, Is.LessThanOrEqualTo(service.ReferenceDistanceM + 1e-7));
            }
        }

        [Test]
        public void WholeConsistAndSweptOccupancyRemainUntilTheRearClears()
        {
            var route = new[] { new TrackRouteSegment { Id = "segment", PhysicalTrackId = "shared-track", LengthM = 500 } };
            var occupied = TrainDynamics.Occupancy(route, 250, 80);
            Assert.That(occupied.Single().StartM, Is.EqualTo(170)); Assert.That(occupied.Single().EndM, Is.EqualTo(250));
            Assert.That(occupied.Single().Overlaps(new TrackOccupancy { PhysicalTrackId = "shared-track", StartM = 100, EndM = 200 }), Is.True);
            Assert.That(TrainDynamics.Occupancy(route, 280, 80).Single().Overlaps(new TrackOccupancy { PhysicalTrackId = "shared-track", StartM = 100, EndM = 200 }), Is.False);
            var swept = TrainDynamics.SweptOccupancy(route, 90, 250, 80).Single();
            Assert.That(swept.StartM, Is.EqualTo(10)); Assert.That(swept.EndM, Is.EqualTo(250));
            Assert.That(swept.Overlaps(new TrackOccupancy { PhysicalTrackId = "shared-track", StartM = 100, EndM = 110 }), Is.True);
            Assert.Throws<ArgumentException>(() => TrainDynamics.Occupancy(route, 500, 80, 10));
            Assert.That(TrainDynamics.Occupancy(route, 490, 80, 10).Single().EndM, Is.EqualTo(500));
        }

        [Test]
        public void OppositeRouteDirectionsReferToTheSamePhysicalTrack()
        {
            var forward = new[] { new TrackRouteSegment { Id = "a", PhysicalTrackId = "track", LengthM = 500 } };
            var reverse = new[] { new TrackRouteSegment { Id = "b", PhysicalTrackId = "track", LengthM = 500, Reversed = true } };
            var a = TrainDynamics.Occupancy(forward, 250, 80).Single();
            var b = TrainDynamics.Occupancy(reverse, 320, 80).Single();
            Assert.That(b.StartM, Is.EqualTo(180)); Assert.That(b.EndM, Is.EqualTo(260)); Assert.That(a.Overlaps(b), Is.True);
        }

        [Test]
        public void RestoredPhaseInvariantsAndLongStoppingQueriesAreConsistent()
        {
            var full = new TrainMotionState { SignedSpeedMS = 10, BrakeDirection = 1, Phase = TrainBrakePhase.Full,
                DecelerationMS2 = .5, TargetDecelerationMS2 = 1, BrakeCommandId = "saved" };
            Assert.Throws<ArgumentException>(() => TrainDynamics.AdvanceBrake(full, .05));
            full.Phase = TrainBrakePhase.Delay; full.DelayRemainingSeconds = 1;
            Assert.Throws<ArgumentException>(() => TrainDynamics.AdvanceBrake(full, .05));
            Assert.That(TrainDynamics.StoppingDistance(100, new TrainBrakeProfile { DecelerationMS2 = .001 }), Is.EqualTo(5000000).Within(1e-6));
            Assert.Throws<ArgumentException>(() => TrainDynamics.StoppingDistance(0, null));
        }

        [Test]
        public void ConsistSpanningSegmentsKeepsBothMappedPhysicalIntervals()
        {
            var route = new[] {
                new TrackRouteSegment { Id = "a", PhysicalTrackId = "first", LengthM = 100 },
                new TrackRouteSegment { Id = "b", PhysicalTrackId = "second", RouteStartM = 100, LengthM = 100, PhysicalStartM = 200, Reversed = true } };
            var intervals = TrainDynamics.Occupancy(route, 150, 80);
            Assert.That(intervals.Length, Is.EqualTo(2));
            Assert.That(intervals[0].StartM, Is.EqualTo(70)); Assert.That(intervals[0].EndM, Is.EqualTo(100));
            Assert.That(intervals[1].StartM, Is.EqualTo(250)); Assert.That(intervals[1].EndM, Is.EqualTo(300));
        }

        [Test]
        public void InvalidTrainAndTrackInputsCannotEnterTheIntegrator()
        {
            Assert.Throws<ArgumentException>(() => TrainDynamics.RequestBrake(new TrainMotionState { SignedSpeedMS = 20 }, new TrainBrakeProfile { DecelerationMS2 = 0 }, "bad"));
            Assert.Throws<ArgumentException>(() => TrainDynamics.AdvanceBrake(new TrainMotionState(), double.NaN));
            Assert.Throws<ArgumentException>(() => TrainDynamics.Occupancy(new[] { new TrackRouteSegment { Id = "s", PhysicalTrackId = "t", LengthM = -1 } }, 1, 1));
        }
    }
}
