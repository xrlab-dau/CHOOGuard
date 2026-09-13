using System;
using NUnit.Framework;
using ChooGuard.Foundation.Simulation;

namespace ChooGuard.Foundation.Tests
{
    public sealed class PhysicalCheckpointTests
    {
        private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        [Test]
        public void PreparedCheckpointBindsValidatedCrowdAndOwnsImmutablePhysicalValues()
        {
            var state = State(); var model = CrowdMotionModel.FromCheckpoint(state.CrowdCheckpoint);
            var proof = model.ExportCheckpointProof();
            var expected = PhysicalCheckpoint.Encode(state);
            var prepared = PhysicalCheckpoint.Prepare(state, proof);
            Assert.That(prepared.Encoded, Is.EqualTo(expected));
            state.Fire.Cells[0].UpperEnergyJ += 1; state.Trains[0].Motion.ReferenceDistanceM += 1;
            state.SimulationTick++; model.TryAdvance(.01, null, out _);
            Assert.That(prepared.Encoded, Is.EqualTo(expected));
            state = State(); state.CrowdCheckpoint = model.ExportCheckpoint();
            Assert.Throws<ArgumentException>(() => PhysicalCheckpoint.Prepare(state, proof));
            Assert.Throws<ArgumentException>(() => PhysicalCheckpoint.Prepare(state, null));
        }
        private static PhysicalWorldState State()
        {
            var fire = new ZoneFireModel(new FireNetworkDefinition { Cells = new[] { new FireCellDefinition { Id = "room", WidthM = 5, DepthM = 5, HeightM = 4 } } });
            Assert.That(fire.TryAdvance(.75, new FireForcing { Sources = new[] { new FireSourcePower { CellId = "room", HeatReleaseW = 12345.678901234567, EnablePlume = false } } }, out _), Is.True);
            var crowd = new CrowdMotionModel(new CrowdDefinition { ProfileId = "fixture", Spaces = new[] { new CrowdSpace { Id = "floor" } } },
                new[] { new CrowdAgent { Id = "npc", ContactSpaceId = "floor", RegionId = "room", FrameId = "world", SurfaceId = "floor",
                    Position = new CrowdVector(.15000000003791787, 2), IntentMode = CrowdIntentMode.DesiredVelocity,
                    DesiredVelocity = new CrowdVector(.12345678901234567, 0) } });
            Assert.That(crowd.TryAdvance(.75, null, out _), Is.True);
            return new PhysicalWorldState { DefinitionHash = Hash, SimulationTick = 15, RandomState = 12345678901234567890UL,
                Fire = fire.ExportState(), CrowdCheckpoint = crowd.ExportCheckpoint(), DirectorState = "{\"nextEventTick\":12345}",
                Trains = new[] { new TrainOperatingState { TrainId = "train", Motion = new TrainMotionState { ReferenceDistanceM = 123.45678901234567,
                    SignedSpeedMS = -7.123456789012345, Phase = TrainBrakePhase.BuildUp, BrakeDirection = -1, BrakeCommandId = "brake",
                    BuildUpRemainingSeconds = .1234567890123, DecelerationMS2 = .5, TargetDecelerationMS2 = 1, BrakeElapsedSeconds = 3.25 },
                    Stage = TrainOperatingStage.Braking, StopIndex = 1, CompletedStops = 7, ElapsedSeconds = .75 } } };
        }

        [Test]
        public void OptionalTimingDoesNotChangeCheckpointBytesOrDecodedState()
        {
            var state = State(); var expected = PhysicalCheckpoint.Encode(state);
            var encode = new PhysicalCheckpointTiming(); var decode = new PhysicalCheckpointTiming();
            var actual = PhysicalCheckpoint.Encode(state, encode);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(PhysicalCheckpoint.Encode(PhysicalCheckpoint.Decode(actual, Hash, decode)), Is.EqualTo(expected));
            Assert.That(encode.TotalMilliseconds, Is.GreaterThanOrEqualTo(encode.CrowdValidationMilliseconds));
            Assert.That(decode.TotalMilliseconds, Is.GreaterThanOrEqualTo(decode.CrowdValidationMilliseconds));
            Assert.That(encode.CrowdValidationMilliseconds, Is.GreaterThan(0));
            Assert.That(decode.CrowdValidationMilliseconds, Is.GreaterThan(0));
        }

        [Test]
        public void CompositeCheckpointPreservesExactPhysicsBitsAndDefinitionBinding()
        {
            var before = State(); var encoded = PhysicalCheckpoint.Encode(before); var after = PhysicalCheckpoint.Decode(encoded, Hash);
            Assert.That(after.SimulationTick, Is.EqualTo(before.SimulationTick)); Assert.That(after.RandomState, Is.EqualTo(before.RandomState));
            Assert.That(after.CrowdCheckpoint, Is.EqualTo(before.CrowdCheckpoint)); Assert.That(after.DirectorState, Is.EqualTo(before.DirectorState));
            Assert.That(BitConverter.DoubleToInt64Bits(after.Fire.Cells[0].UpperEnergyJ), Is.EqualTo(BitConverter.DoubleToInt64Bits(before.Fire.Cells[0].UpperEnergyJ)));
            Assert.That(BitConverter.DoubleToInt64Bits(after.Trains[0].Motion.ReferenceDistanceM), Is.EqualTo(BitConverter.DoubleToInt64Bits(before.Trains[0].Motion.ReferenceDistanceM)));
            Assert.That(BitConverter.DoubleToInt64Bits(after.Trains[0].Motion.SignedSpeedMS), Is.EqualTo(BitConverter.DoubleToInt64Bits(before.Trains[0].Motion.SignedSpeedMS)));
            Assert.That(PhysicalCheckpoint.Encode(after), Is.EqualTo(encoded));
            Assert.Throws<ArgumentException>(() => PhysicalCheckpoint.Decode(encoded, new string('b', 64)));
        }

        [Test]
        public void CorruptionTruncationAndUnknownEnvelopesAreRejected()
        {
            var encoded = PhysicalCheckpoint.Encode(State()); var bytes = Convert.FromBase64String(encoded.Substring(5)); bytes[20] ^= 1;
            Assert.Throws<ArgumentException>(() => PhysicalCheckpoint.Decode("CGP1:" + Convert.ToBase64String(bytes), Hash));
            Assert.Throws<ArgumentException>(() => PhysicalCheckpoint.Decode(encoded.Substring(0, encoded.Length - 8), Hash));
            Assert.Throws<ArgumentException>(() => PhysicalCheckpoint.Decode("CGP9:" + encoded.Substring(5), Hash));
        }

        [Test]
        public void InvalidNestedCrowdCannotBeSavedAndUnicodeIdsFitTheCoreContract()
        {
            var s = State(); s.CrowdCheckpoint = "CGC1:";
            Assert.Throws<ArgumentException>(() => PhysicalCheckpoint.Encode(s));
            s = State(); s.Trains[0].TrainId = new string('한', 128); s.Trains[0].Motion.BrakeCommandId = new string('차', 128);
            var restored = PhysicalCheckpoint.Decode(PhysicalCheckpoint.Encode(s), Hash);
            Assert.That(restored.Trains[0].TrainId, Is.EqualTo(s.Trains[0].TrainId));
            Assert.That(restored.Trains[0].Motion.BrakeCommandId, Is.EqualTo(s.Trains[0].Motion.BrakeCommandId));
        }

        [Test]
        [TestCase(TrainOperatingStage.ClosingDoors, TrainBrakePhase.Idle)]
        [TestCase(TrainOperatingStage.Braking, TrainBrakePhase.Delay)]
        [TestCase(TrainOperatingStage.Braking, TrainBrakePhase.BuildUp)]
        [TestCase(TrainOperatingStage.FaultStopped, TrainBrakePhase.Stopped)]
        public void TrainContinuesExactlyFromEveryOperationalRecoveryPhase(TrainOperatingStage stage, TrainBrakePhase phase)
        {
            var d = new TrainOperatingDefinition { TrainId = "train", ConsistLengthM = 20, RouteStartM = 0, RouteEndM = 240,
                MaximumSpeedMS = 8, TractionAccelerationMS2 = 1, DoorClosingSeconds = .5,
                ServiceBrake = new TrainBrakeProfile { DelaySeconds = 1, BuildUpSeconds = 2, DecelerationMS2 = 1 },
                EmergencyBrake = new TrainBrakeProfile { DelaySeconds = .2, BuildUpSeconds = 1, DecelerationMS2 = 1.5 },
                Stops = new[] { new TrainStopDefinition { ReferenceM = 100, DwellSeconds = 1, Boarding = true }, new TrainStopDefinition { ReferenceM = 210, DwellSeconds = 1 } } };
            var train = TrainOperation.Create(d);
            if (stage == TrainOperatingStage.FaultStopped)
            { TrainOperation.Advance(train, d, 5, new TrainOperatingInput()); TrainOperation.EmergencyStop(train, d, "fault"); TrainOperation.Advance(train, d, 100, new TrainOperatingInput()); }
            else
                for (var i = 0; i < 10000 && (train.Stage != stage || train.Motion.Phase != phase); i++)
                    TrainOperation.Advance(train, d, .05, new TrainOperatingInput());
            Assert.That(train.Stage, Is.EqualTo(stage)); Assert.That(train.Motion.Phase, Is.EqualTo(phase));
            var physical = State(); physical.Trains = new[] { train };
            var recovered = PhysicalCheckpoint.Decode(PhysicalCheckpoint.Encode(physical), Hash);
            if (stage == TrainOperatingStage.FaultStopped)
            { TrainOperation.Recover(physical.Trains[0], d); TrainOperation.Recover(recovered.Trains[0], d); }
            for (var i = 0; i < 200; i++)
            { TrainOperation.Advance(physical.Trains[0], d, .05, new TrainOperatingInput()); TrainOperation.Advance(recovered.Trains[0], d, .05, new TrainOperatingInput()); }
            Assert.That(PhysicalCheckpoint.Encode(recovered), Is.EqualTo(PhysicalCheckpoint.Encode(physical)));
        }

        [Test]
        public void FireAndCrowdContinueIdenticallyAfterCompositeRecovery()
        {
            var before = State(); var after = PhysicalCheckpoint.Decode(PhysicalCheckpoint.Encode(before), Hash);
            var definition = new FireNetworkDefinition { Cells = new[] { new FireCellDefinition { Id = "room", WidthM = 5, DepthM = 5, HeightM = 4 } } };
            var a = new ZoneFireModel(definition, before.Fire); var b = new ZoneFireModel(definition, after.Fire);
            var forcing = new FireForcing { Sources = new[] { new FireSourcePower { CellId = "room", HeatReleaseW = 20000, EnablePlume = false } } };
            Assert.That(a.TryAdvance(.05, forcing, out _), Is.True); Assert.That(b.TryAdvance(.05, forcing, out _), Is.True);
            Assert.That(a.ExportState().Cells[0].UpperEnergyJ, Is.EqualTo(b.ExportState().Cells[0].UpperEnergyJ));
            var ca = CrowdMotionModel.FromCheckpoint(before.CrowdCheckpoint); var cb = CrowdMotionModel.FromCheckpoint(after.CrowdCheckpoint);
            Assert.That(ca.TryAdvance(.05, null, out _), Is.True); Assert.That(cb.TryAdvance(.05, null, out _), Is.True);
            Assert.That(ca.ExportCheckpoint(), Is.EqualTo(cb.ExportCheckpoint()));
        }
    }
}
