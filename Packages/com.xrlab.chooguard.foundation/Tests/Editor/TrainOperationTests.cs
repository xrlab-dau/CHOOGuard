using System;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;

namespace ChooGuard.Foundation.Tests
{
    public sealed class TrainOperationTests
    {
        private static TrainOperatingDefinition Definition() => new TrainOperatingDefinition {
            TrainId = "synthetic-train", ConsistLengthM = 20, RouteStartM = 0, RouteEndM = 240,
            MaximumSpeedMS = 8, TractionAccelerationMS2 = 1, DoorClosingSeconds = .5,
            ServiceBrake = new TrainBrakeProfile { DelaySeconds = .5, BuildUpSeconds = 1, DecelerationMS2 = 1 },
            EmergencyBrake = new TrainBrakeProfile { DelaySeconds = .2, BuildUpSeconds = .5, DecelerationMS2 = 1.5 },
            Stops = new[] { new TrainStopDefinition { ReferenceM = 100, DwellSeconds = 1, Boarding = true },
                new TrainStopDefinition { ReferenceM = 210, DwellSeconds = .2 },
                new TrainStopDefinition { ReferenceM = 100, DwellSeconds = 1, Boarding = true },
                new TrainStopDefinition { ReferenceM = 30, DwellSeconds = .2 } } };

        [TestCase(.05)]
        [TestCase(.137)]
        public void AutomaticTrainRepeatsStopsWithoutPositionSnapsOrOvershooting(double step)
        {
            var definition = Definition(); var state = TrainOperation.Create(definition); var visited = 0;
            for (var time = 0.0; time < 240; time += step)
            {
                var previous = state.Copy(); TrainOperation.Advance(state, definition, step, new TrainOperatingInput());
                Assert.That(Math.Abs(state.Motion.SignedSpeedMS), Is.LessThanOrEqualTo(definition.MaximumSpeedMS + 1e-8));
                Assert.That(Math.Abs(state.Motion.ReferenceDistanceM - previous.Motion.ReferenceDistanceM), Is.LessThanOrEqualTo(definition.MaximumSpeedMS * step + 1e-8));
                Assert.That(state.Stage, Is.Not.EqualTo(TrainOperatingStage.DockingError));
                if (state.CompletedStops != previous.CompletedStops)
                {
                    visited++; Assert.That(state.Motion.SignedSpeedMS, Is.Zero);
                    Assert.That(Math.Abs(state.Motion.ReferenceDistanceM - definition.Stops[state.StopIndex].ReferenceM), Is.LessThan(definition.DockToleranceM));
                }
                if (state.DoorOpen) Assert.That(state.Motion.SignedSpeedMS, Is.Zero);
            }
            Assert.That(visited, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void OccupiedBoardingAreaPreventsClosingAndDeparture()
        {
            var definition = Definition(); var state = TrainOperation.Create(definition);
            TrainOperation.Advance(state, definition, 10, new TrainOperatingInput { BoardingAreaClear = false, DoorRequestedOpen = false });
            Assert.That(state.Motion.ReferenceDistanceM, Is.EqualTo(100)); Assert.That(state.Motion.SignedSpeedMS, Is.Zero);
            Assert.That(state.DoorOpen, Is.True); Assert.That(state.Stage, Is.EqualTo(TrainOperatingStage.Dwell));
            TrainOperation.Advance(state, definition, 2, new TrainOperatingInput());
            Assert.That(state.DoorOpen, Is.False); Assert.That(state.Motion.SignedSpeedMS, Is.GreaterThan(0));
        }

        [Test]
        public void EmergencyStopsBetweenStationsAndRequiresExplicitRecovery()
        {
            var definition = Definition(); var state = TrainOperation.Create(definition);
            TrainOperation.Advance(state, definition, 5, new TrainOperatingInput());
            Assert.That(state.Motion.SignedSpeedMS, Is.GreaterThan(0));
            TrainOperation.EmergencyStop(state, definition, "incident-1");
            TrainOperation.Advance(state, definition, 100, new TrainOperatingInput());
            Assert.That(state.Stage, Is.EqualTo(TrainOperatingStage.FaultStopped)); Assert.That(state.DoorOpen, Is.False);
            var at = state.Motion.ReferenceDistanceM;
            TrainOperation.Advance(state, definition, 20, new TrainOperatingInput()); Assert.That(state.Motion.ReferenceDistanceM, Is.EqualTo(at));
            TrainOperation.Recover(state, definition); TrainOperation.Advance(state, definition, 1, new TrainOperatingInput());
            Assert.That(state.Motion.ReferenceDistanceM, Is.GreaterThan(at));
        }

        [Test]
        public void EqualTargetEmergencyCanCancelServiceDelayAndContradictoryRestoresAreRejected()
        {
            var d = Definition(); d.ServiceBrake.DelaySeconds = 2; d.ServiceBrake.BuildUpSeconds = 2;
            d.EmergencyBrake.DecelerationMS2 = d.ServiceBrake.DecelerationMS2; d.EmergencyBrake.DelaySeconds = 0; d.EmergencyBrake.BuildUpSeconds = 0;
            var s = TrainOperation.Create(d);
            for (var i = 0; i < 5000 && s.Motion.Phase != TrainBrakePhase.Delay; i++) TrainOperation.Advance(s, d, .05, new TrainOperatingInput());
            Assert.That(s.Motion.Phase, Is.EqualTo(TrainBrakePhase.Delay));
            TrainOperation.EmergencyStop(s, d, "faster-same-target");
            Assert.That(s.Motion.DelayRemainingSeconds, Is.Zero); Assert.That(s.Motion.Phase, Is.EqualTo(TrainBrakePhase.Full));
            var invalid = TrainOperation.Create(d); invalid.FaultLatched = true;
            Assert.Throws<ArgumentException>(() => TrainOperation.Advance(invalid, d, .05, new TrainOperatingInput()));
            invalid = TrainOperation.Create(d); invalid.Motion.ReferenceDistanceM += 1;
            Assert.Throws<ArgumentException>(() => TrainOperation.Validate(invalid, d));
            invalid = TrainOperation.Create(d); invalid.Stage = TrainOperatingStage.Cruising; invalid.Motion.Phase = TrainBrakePhase.Stopped;
            Assert.Throws<ArgumentException>(() => TrainOperation.Validate(invalid, d));
        }

        [Test]
        public void InvalidOperatingDefinitionCannotStartAnUnrepresentedConsist()
        {
            var definition = Definition(); definition.Stops[0].ReferenceM = 10;
            Assert.Throws<ArgumentException>(() => TrainOperation.Create(definition));
        }
    }
}
