using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    /// <summary>
    /// FMP-08b (#129): numerical receipt for analytic stopping distance and time, brake delay/build-up,
    /// whole-consist occupancy (entry / full occupancy / rear release) and docking position error of the
    /// SHIPPED synthetic train model (<see cref="TrainDynamics"/>, <see cref="TrainOperation"/>).
    ///
    /// REUSE. No dynamics, no threshold and no state machine is changed or re-derived here. The existing
    /// analytic suites are re-run in the same recorded invocation by the check filter
    /// (<c>TrainDynamicsTests; TrainOperationTests; Fmp08bOccupancyTests</c>); this file adds the numeric
    /// receipt on top of them and touches nothing under <c>Runtime/Simulation</c>.
    ///
    /// SOURCE OF TRUTH. Every frozen condition is read from the authored profile
    /// <c>foundation/world/foundation-simulation-profile.json</c> (train <c>train.mainline</c>) rather than
    /// restated as a literal. The literal assertions exist only to pin that file: if the authored profile, its
    /// consist length, its docking tolerance or its brake envelope drift, this file fails instead of silently
    /// measuring a different train. <see cref="FoundationSimulationProfile.Validate"/> is additionally re-run on
    /// the parsed profile, so a profile whose docking tolerance has been widened, or whose emergency envelope no
    /// longer preserves the service envelope, is rejected by the shipped validator rather than accepted here.
    ///
    /// ORACLE. The measured quantities are compared against a closed-form oracle written out in this file
    /// (<see cref="AnalyticStoppingDistanceM"/>, <see cref="AnalyticStoppingTimeS"/>) and against hand-computed
    /// literals at the frozen maximum speed. Agreement between the fixed-step integrator
    /// (<see cref="TrainDynamics.AdvanceBrake"/>), the shipped query (<see cref="TrainDynamics.StoppingDistance"/>)
    /// and the oracle is therefore three-way evidence.
    ///
    /// NEGATIVE CASES THIS HARNESS MUST FAIL ON. All three are encoded as discriminating assertions, never as
    /// prose and never as an exception that merely "did not happen":
    ///
    ///   1. Releasing occupancy from the consist head only.
    ///      <c>OccupancyReleaseIsDecidedByTheRearNotTheHead</c> does NOT restate the release reference. It
    ///      bisects the SHIPPED <see cref="TrainDynamics.Occupancy"/> query for the reference at which the block
    ///      stops being reported as occupied, so the only value it can return is one the shipped rule itself
    ///      produced, and asserts the found boundary is a whole consist length beyond blockEnd - strictly inside
    ///      the window in which the rejected head-only predicate has already released. The disagreement window is
    ///      additionally counted (<c>OccupancyReleaseCounterexamples</c> > 0) and probed directly. If the shipped
    ///      rule ever released from the head, the search would return blockEnd and every one of those assertions
    ///      would fail.
    ///      <c>RearReleaseMovesWithTheConsistLengthNotWithTheHeadPosition</c> varies the consist length and
    ///      asserts the release reference moves with it, and
    ///      <c>RearReleaseOnAReversedSegmentIsStillDecidedByTheRouteAxisRear</c> shows the rule follows the
    ///      route axis, not whichever physical end happens to lead.
    ///   2. Assuming an emergency stop always ends aligned with a platform.
    ///      <c>EmergencyStopBetweenStationsIsNotAssumedToBePlatformAligned</c> commands an emergency stop with
    ///      the train demonstrably mid-line and asserts the stopped reference is farther from every authored stop
    ///      than the frozen docking tolerance, that the state is a latched fault and not a dwell, and that
    ///      <see cref="TrainOperation.Recover"/> sends it to a travel stage rather than a dwell.
    ///      <c>DockedAndMidLineEmergencyStopsTakeDifferentRecoveryBranches</c> shows the aligned branch exists
    ///      but is a distinct branch, so the two cannot be conflated.
    ///   3. Relaxing a threshold after seeing the result.
    ///      Every tolerance used by an acceptance assertion is a <c>const</c> in this file, is asserted equal to
    ///      the value read from the authored profile, and the profile itself is asserted to be rejected by the
    ///      shipped validator once widened. The two stopping distances and stopping times at the frozen maximum
    ///      speed are additionally pinned to full precision, so a loosened comparison cannot be smuggled in.
    ///
    /// LIMITS. Synthetic level route, synthetic constant-deceleration brake envelope, no real vehicle brake
    /// curve, no signalling system, no operator rule book and no real vehicle measurement. The route, consist
    /// and platform lengths are authored game dimensions, not surveyed ones.
    /// </summary>
    public sealed class Fmp08bOccupancyTests
    {
        // ------------------------------------------------------------ frozen conditions ----

        private const string ProfileRelativePath = "foundation/world/foundation-simulation-profile.json";
        private const string WorldRelativePath = "foundation/world/connected-world-profile.json";
        private const string FrozenTrainId = "train.mainline";
        private const string FrozenFrameId = "train-mainline";
        private const string FrozenSegmentId = "route.mainline";
        private const string FrozenPhysicalTrackId = "track.mainline";
        private const double FrozenConsistLengthM = 40;
        private const double FrozenRouteStartM = 0;
        private const double FrozenRouteEndM = 440;
        private const double FrozenMaximumSpeedMS = 6;
        private const double FrozenTractionAccelerationMS2 = .7;
        private const double FrozenClearanceMarginM = 1;
        private const double FrozenDockToleranceM = .05;
        private const double FrozenDoorClosingSeconds = 2;
        private const double FrozenStationReferenceM = 220;
        private const double FrozenServiceDelayS = .4;
        private const double FrozenServiceBuildUpS = .5;
        private const double FrozenServiceDecelerationMS2 = .8;
        private const double FrozenEmergencyDelayS = .2;
        private const double FrozenEmergencyBuildUpS = .3;
        private const double FrozenEmergencyDecelerationMS2 = 1.2;
        private const double FrozenServiceStopDistanceAtMaxSpeedM = 26.391666666666667;
        private const double FrozenServiceStopTimeAtMaxSpeedS = 8.15;
        private const double FrozenEmergencyStopDistanceAtMaxSpeedM = 17.0955;
        private const double FrozenEmergencyStopTimeAtMaxSpeedS = 5.35;

        /// <summary>Every accepted docking position must satisfy |stopped - authored stop| &lt;= this. The value
        /// is pinned to the authored profile in <see cref="RecordedBudgetsCannotBeRelaxedByTheHarness"/>; it is
        /// never chosen in this file.</summary>
        private const double DockingToleranceM = FrozenDockToleranceM;

        /// <summary>Closed-form agreement between the oracle, the shipped query and the integrator. The shipped
        /// suite already establishes 1e-9 for exactly this comparison
        /// (<c>TrainDynamicsTests.BrakingStopsAtTheAnalyticDistanceAndTime</c>).</summary>
        private const double AnalyticDistanceToleranceM = 1e-9;
        private const double AnalyticTimeToleranceS = 1e-9;

        /// <summary>Independence from the tick partition. The shipped suite establishes 1e-7 for this comparison
        /// (<c>TrainDynamicsTests.TickPartitionAndReverseTravelKeepTheSameBrakingSolution</c>), so the same bound
        /// is used here rather than a value chosen after seeing a result.</summary>
        private const double PartitionIndependenceToleranceM = 1e-7;

        /// <summary>Two deliberately different fixed steps; the trajectory must not depend on the partition.</summary>
        private const double FineStepSeconds = .005;
        private const double CoarseStepSeconds = .07;

        private const double DockingStepSeconds = .01;
        private const double DockingRunSeconds = 300;
        private const int MinimumCompletedStops = 4;

        /// <summary>Fixed step used for the emergency-stop trajectory. Deliberately NOT .05: the shipped
        /// TrainDynamics.AdvanceBrake tests the build-up boundary with exact equality
        /// (TrainDynamics.cs:119 "if (next.BuildUpRemainingSeconds == 0)"), while BuildUpRemainingSeconds is a
        /// subtractive accumulator. At .05 the residue from "0.3 - 0.05 * 6" leaves the remaining build-up at
        /// 0.05000000000000002, the next step's h (0.05) undershoots that boundary, DecelerationMS2 rounds to
        /// exactly TargetDecelerationMS2 while BuildUpRemainingSeconds becomes 1.39e-17 instead of 0, and the
        /// shipped ValidateState then rejects its own state for a BuildUp phase already at target deceleration.
        /// That is a latent defect in the shipped integrator, recorded in the FMP-08b receipt and handed off
        /// rather than patched here (Runtime/Simulation brake logic needs tests and lead review). .02 traverses
        /// the same envelope without landing a step on that boundary; the eight steps the partition sweep test
        /// actually runs - 0.005 / 0.01 / 0.02 / 0.025 / 0.04 / 0.06 / 0.075 / 0.1 - all integrate the same
        /// trajectory.</summary>
        private const double EmergencyStepSeconds = .02;
        private const double EmergencyCommandReferenceM = 300;
        private const double EmergencyLatchedHoldSeconds = 20;
        private const double EmergencySafetyMarginM = 1;
        private const string EmergencyCommandId = "fmp08b-emergency";

        /// <summary>The step the shipped integrator's build-up boundary cannot carry at the frozen emergency
        /// profile. It is measured here as a hazard probe and is never the step the accepted trajectory uses; see
        /// <c>TheShippedIntegratorRejectsItsOwnBuildUpBoundaryAndTheMeasuredStepAvoidsIt</c> and the hand-off note
        /// in <c>docs/evidence/work-items/FMP-08b/result.json</c>.</summary>
        private const double EmergencyBoundaryHazardStepSeconds = .05;
        private const int EmergencyBoundaryHazardProbeLimitSteps = 128;
        private const string EmergencyBoundaryHazardCommandId = "fmp08b:boundary-hazard";

        private const double DeterminismRunSeconds = 60;

        /// <summary>Bound on the gap between a published decimal double and the double it was rendered from. The
        /// number is not chosen after seeing a result: a binary64 value near 362 m has a unit in the last place of
        /// about 5.7e-14, and a decimal renderer with bounded significant digits can land one ulp away, so the
        /// bound is set at roughly two ulps. The measured gap itself is published in the receipt as
        /// <c>DeterminismDecimalRoundTripGapM</c> rather than being asserted to zero.</summary>
        private const double JsonDecimalRoundTripToleranceM = 1e-13;

        private const double BoundaryEpsilonM = 1e-6;
        private const double SweepStepM = 5;

        /// <summary>Step of the discrete sweep that counts how often the rejected head-only rule disagrees with
        /// the shipped occupancy rule in the release window. A separate constant, not an inline literal, because
        /// the published count is cross-checked against it.</summary>
        private const double ReleaseSweepStepM = .5;

        /// <summary>Bisection budget for locating the shipped release boundary. 72 halvings take the initial
        /// bracket of block + consist + step down far below one unit in the last place of a ~300 m reference, so
        /// the returned value is the exact boundary rather than an approximation of it.</summary>
        private const int ReleaseBisectionIterations = 72;

        /// <summary>Closed form for the count the release sweep must produce, stated so the published counter is
        /// checked against arithmetic instead of against its own loop. The sweep is phase-aligned with the block
        /// end and steps by <see cref="ReleaseSweepStepM"/>, so the samples strictly inside the disagreement
        /// window (blockEnd, blockEnd + consist) are blockEnd + step ... blockEnd + consist - step, i.e.
        /// consist / step - 1 = 40 / 0.5 - 1 = 79 for the frozen consist.</summary>
        private static int ExpectedOccupancyReleaseCounterexamples =>
            (int)Math.Round(FrozenConsistLengthM / ReleaseSweepStepM) - 1;

        private const double ReversedProbeTrackLengthM = 500;

        /// <summary>Route-axis reference at which the reversed-segment probe's physical block [300, 340] is
        /// released again: the route-axis rear maps to physical 300 while the route-axis head is still at physical
        /// 260. Found from the mapping (500 - reference + consist = 300), not from a measured result.</summary>
        private const double ReversedProbeReleaseReferenceM = 240;

        /// <summary>Route-axis reference at which the physical block's far end is reached by the mapped interval;
        /// an entry/mapping probe, not a release boundary.</summary>
        private const double ReversedProbeMappingReferenceM = 160;
        private const string EvidenceDirectory = "Temp/ChooGuardTrainAnalytic";
        private const string ReceiptFileName = "fmp08b-train-analytic.json";
        private const string ReceiptMarker = "FMP08B-ANALYTIC";

        private static readonly double[] AnalyticSpeedsMS = { .05, .2, 1, 3, 6 };
        private static readonly double[] ReleaseProbeConsistLengthsM = { 20, 40, 60 };

        // ------------------------------------------------------------ measured state ----

        private static FoundationSimulationProfile profile;
        private static ConnectedWorldDefinition world;
        private static FoundationTrainRoute route;
        private static TrainOperatingDefinition operation;
        private static TrackRouteSegment[] segments;

        private static readonly List<AnalyticSample> analytic = new List<AnalyticSample>();
        private static readonly List<OccupancySample> occupancy = new List<OccupancySample>();
        private static readonly List<DockingSample> docking = new List<DockingSample>();

        private static double maximumDockingErrorM;
        private static int completedStops;
        private static int dockingToleranceViolations;
        private static int positionSnapViolations;
        private static int overshootViolations;
        private static int brakingEnvelopeViolations;
        private static int dockingErrorStageEvents;
        private static double minimumCruiseEnvelopeMarginM = double.PositiveInfinity;

        private static double occupancyEntryReferenceM;
        private static double occupancyReleaseReferenceM;
        private static double headOnlyReleaseReferenceM;
        private static int occupancyReleaseCounterexamples;
        private static double occupancyReleaseCounterexampleFirstM = double.NaN;
        private static double occupancyReleaseCounterexampleLastM = double.NaN;
        private static double platformBlockStartM;
        private static double platformBlockEndM;

        private static double emergencyHeadAtCommandM;
        private static double emergencySpeedAtCommandMS;
        private static int emergencyPhaseAtCommand;
        private static double emergencyStoppedAtM;
        private static double emergencyAnalyticTravelM;
        private static double emergencyTravelM;
        private static double emergencyNearestStopDistanceM;
        private static int emergencyStageAfterStop;
        private static bool emergencyDoorOpenAfterStop;
        private static double emergencyDriftWhileLatchedM;

        private static bool emergencyBoundaryHazardRejected;
        private static int emergencyBoundaryHazardRejectedAtStep = -1;
        private static int emergencyBoundaryHazardLastValidPhase = -1;
        private static double emergencyBoundaryHazardSpeedMS;
        private static double emergencyBoundaryHazardBuildUpBeforeS;
        private static double emergencyBoundaryHazardResidueSeconds;
        private static double emergencyBoundaryHazardRoundedDecelerationMS2;
        private static double emergencyBoundaryHazardTargetDecelerationMS2;
        private static double emergencyBoundaryHazardElapsedS;

        private static int offPlatformRecoveryStage;
        private static bool offPlatformRecoveryContinueToTarget;
        private static double offPlatformRecoveryDockErrorM;
        private static double offPlatformRecoveryDistanceM;
        private static int offPlatformRecoveryStopIndex;
        private static int alignedStageAfterEmergencyStop;
        private static int alignedRecoveryStage;
        private static double alignedStopDistanceM;

        private static double determinismFinalReferenceM;
        private static double determinismRepeatFinalReferenceM;
        private static long determinismFinalBits;
        private static long determinismRepeatFinalBits;
        private static bool determinismStatesIdentical;

        private static double crossSegmentMainlineReleasedAtM;
        private static double crossSegmentMainlineHeldAtM;
        private static int crossSegmentIntervalsAtHeadOverrun;
        private static int crossSegmentIntervalsAfterRearClear;

        // ------------------------------------------------------------ fixture ----

        [OneTimeSetUp]
        public static void Measure()
        {
            LoadAuthoredProfile();
            MeasureAnalyticStopping();
            MeasureOccupancyRelease();
            MeasureDockingRun();
            MeasureEmergencyStop();
            MeasureEmergencyBoundaryHazard();
            MeasureDeterminism();
            WriteReceipt();
        }

        private static void LoadAuthoredProfile()
        {
            world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(WorldRelativePath));
            profile = JsonUtility.FromJson<FoundationSimulationProfile>(File.ReadAllText(ProfileRelativePath));
            route = profile.Trains.Single(t => t.Operation.TrainId == FrozenTrainId);
            operation = route.Operation;
            segments = route.Segments;
        }

        // ------------------------------------------------------------ analytic distance / time ----

        /// <summary>Closed-form stopping distance for the same piecewise profile the shipped integrator uses:
        /// delay phase, constant-jerk build-up, then constant deceleration. Written out here so the receipt does
        /// not merely echo the shipped query back at itself.</summary>
        private static double AnalyticStoppingDistanceM(double signedSpeedMS, TrainBrakeProfile brake)
        {
            var speed = Math.Abs(signedSpeedMS);
            var deceleration = brake.DecelerationMS2;
            var ramp = brake.BuildUpSeconds;
            var distance = speed * brake.DelaySeconds;
            if (ramp == 0) return distance + speed * speed / (2 * deceleration);
            if (speed <= deceleration * ramp / 2)
            {
                var rampTime = Math.Sqrt(2 * ramp * speed / deceleration);
                return distance + speed * rampTime - deceleration * rampTime * rampTime * rampTime / (6 * ramp);
            }
            var afterRamp = speed - deceleration * ramp / 2;
            return distance + speed * ramp - deceleration * ramp * ramp / 6 + afterRamp * afterRamp / (2 * deceleration);
        }

        private static double AnalyticStoppingTimeS(double signedSpeedMS, TrainBrakeProfile brake)
        {
            var speed = Math.Abs(signedSpeedMS);
            var deceleration = brake.DecelerationMS2;
            var ramp = brake.BuildUpSeconds;
            if (ramp == 0) return brake.DelaySeconds + speed / deceleration;
            if (speed <= deceleration * ramp / 2) return brake.DelaySeconds + Math.Sqrt(2 * ramp * speed / deceleration);
            return brake.DelaySeconds + ramp + (speed - deceleration * ramp / 2) / deceleration;
        }

        private static TrainMotionState IntegrateToStop(double speedMS, TrainBrakeProfile brake, double step, string commandId)
        {
            var motion = new TrainMotionState { SignedSpeedMS = speedMS };
            Assert.That(TrainDynamics.RequestBrake(motion, brake, commandId), Is.True, "the frozen brake request must be accepted");
            var guard = 0;
            while (motion.Phase != TrainBrakePhase.Stopped)
            {
                TrainDynamics.AdvanceBrake(motion, step);
                if (++guard > 2000000) throw new InvalidOperationException("Frozen brake integration did not reach a stop.");
            }
            return motion;
        }

        private static void MeasureAnalyticStopping()
        {
            var profileIndex = 0;
            foreach (var brake in new[] { operation.ServiceBrake, operation.EmergencyBrake })
            {
                var label = profileIndex == 0 ? "service" : "emergency";
                var speedIndex = 0;
                foreach (var speed in AnalyticSpeedsMS)
                {
                    var fine = IntegrateToStop(speed, brake, FineStepSeconds, "fmp08b:" + label + ":" + speedIndex);
                    var coarse = IntegrateToStop(speed, brake, CoarseStepSeconds, "fmp08b:" + label + ":c" + speedIndex);
                    analytic.Add(new AnalyticSample {
                        Profile = label, SpeedMS = speed,
                        AnalyticDistanceM = AnalyticStoppingDistanceM(speed, brake),
                        AnalyticTimeS = AnalyticStoppingTimeS(speed, brake),
                        ShippedDistanceM = TrainDynamics.StoppingDistance(speed, brake),
                        IntegratedDistanceM = fine.ReferenceDistanceM, IntegratedTimeS = fine.BrakeElapsedSeconds,
                        CoarseDistanceM = coarse.ReferenceDistanceM, CoarseTimeS = coarse.BrakeElapsedSeconds });
                    speedIndex++;
                }
                profileIndex++;
            }
        }

        // ------------------------------------------------------------ occupancy entry / rear release ----

        /// <summary>Platform block: the authored station reference extended one frozen consist length in each
        /// direction. Derived from the profile, not invented in this file.</summary>
        private static TrackOccupancy PlatformBlock() => new TrackOccupancy {
            PhysicalTrackId = segments[0].PhysicalTrackId,
            StartM = FrozenStationReferenceM - FrozenConsistLengthM,
            EndM = FrozenStationReferenceM + FrozenConsistLengthM };

        private static bool Occupies(TrackRouteSegment[] routeSegments, double referenceM, double consistLengthM, TrackOccupancy block) =>
            TrainDynamics.Occupancy(routeSegments, referenceM, consistLengthM).Any(interval => interval.Overlaps(block));

        private static bool OccupiesBlock(double referenceM, double consistLengthM, TrackOccupancy block) =>
            Occupies(segments, referenceM, consistLengthM, block);

        /// <summary>The rejected inference: "the head has left the block, so the block is free". Encoded so the
        /// harness can measure how often it disagrees with the shipped rule instead of only asserting it.</summary>
        private static bool HeadOnlySaysClear(double referenceM, TrackOccupancy block) => referenceM > block.EndM;

        private static void MeasureOccupancyRelease()
        {
            var block = PlatformBlock();
            var consist = operation.ConsistLengthM;
            platformBlockStartM = block.StartM;
            platformBlockEndM = block.EndM;

            occupancyEntryReferenceM = block.StartM;
            // The published release reference is the value the shipped Occupancy query was bisected for, not a
            // constant written next to it; the head-only release point is the rejected rule's value, kept so the
            // two can be compared.
            occupancyReleaseReferenceM = FindReleaseReferenceByBisection(block, consist);
            headOnlyReleaseReferenceM = block.EndM;

            foreach (var reference in new[] { block.StartM - consist, block.StartM, block.StartM + BoundaryEpsilonM,
                block.StartM + consist, block.StartM + consist + SweepStepM, block.EndM, block.EndM + consist / 2,
                block.EndM + consist - BoundaryEpsilonM, block.EndM + consist, block.EndM + consist + SweepStepM })
                RecordOccupancy("platform", reference, consist, block);

            // Count the window in which the rejected head-only predicate disagrees with the shipped rule.
            for (var reference = block.StartM; reference <= block.EndM + consist + consist; reference += ReleaseSweepStepM)
                if (HeadOnlySaysClear(reference, block) && OccupiesBlock(reference, consist, block))
                {
                    occupancyReleaseCounterexamples++;
                    if (double.IsNaN(occupancyReleaseCounterexampleFirstM)) occupancyReleaseCounterexampleFirstM = reference;
                    occupancyReleaseCounterexampleLastM = reference;
                }

            // Cross-segment: the authored mainline plus an explicitly synthetic 60 m stub, declared as such so the
            // extra geometry is never mistaken for authored layout.
            var extended = segments.Concat(new[] { new TrackRouteSegment {
                Id = FrozenSegmentId + ".fmp08b-synthetic-stub", PhysicalTrackId = FrozenPhysicalTrackId + ".fmp08b-synthetic-stub",
                RouteStartM = FrozenRouteEndM, LengthM = 60, PhysicalStartM = 0, Reversed = false } }).ToArray();
            var mainline = new TrackOccupancy { PhysicalTrackId = FrozenPhysicalTrackId, StartM = 0, EndM = FrozenRouteEndM };
            crossSegmentMainlineHeldAtM = FrozenRouteEndM + consist - .1;
            crossSegmentMainlineReleasedAtM = FrozenRouteEndM + consist;
            crossSegmentIntervalsAtHeadOverrun = TrainDynamics.Occupancy(extended, FrozenRouteEndM + consist / 2, consist).Length;
            crossSegmentIntervalsAfterRearClear = TrainDynamics.Occupancy(extended, crossSegmentMainlineReleasedAtM, consist).Length;
        }

        private static void RecordOccupancy(string name, double reference, double consist, TrackOccupancy block)
        {
            var intervals = TrainDynamics.Occupancy(segments, reference, consist);
            occupancy.Add(new OccupancySample {
                Case = name, ReferenceM = reference, ConsistLengthM = consist,
                BlockStartM = block.StartM, BlockEndM = block.EndM, IntervalCount = intervals.Length,
                IntervalStartM = intervals.Length > 0 ? intervals[0].StartM : 0,
                IntervalEndM = intervals.Length > 0 ? intervals[0].EndM : 0,
                OverlapsBlock = intervals.Any(i => i.Overlaps(block)), HeadPastBlockEnd = reference > block.EndM });
        }

        // ------------------------------------------------------------ docking run ----

        private static void MeasureDockingRun()
        {
            var state = TrainOperation.Create(operation);
            for (var elapsed = 0.0; elapsed < DockingRunSeconds; elapsed += DockingStepSeconds)
            {
                var before = state.Copy();
                TrainOperation.Advance(state, operation, DockingStepSeconds, new TrainOperatingInput());
                ObserveDockingTick(before, state);
            }
            completedStops = (int)state.CompletedStops;
        }

        private static void ObserveDockingTick(TrainOperatingState before, TrainOperatingState after)
        {
            // The trajectory may never jump: one tick can move the consist no farther than the traction envelope
            // allows, and TrainOperation.Advance splits at every stage boundary rather than teleporting.
            var moved = Math.Abs(after.Motion.ReferenceDistanceM - before.Motion.ReferenceDistanceM);
            var allowed = operation.MaximumSpeedMS * DockingStepSeconds +
                operation.TractionAccelerationMS2 * DockingStepSeconds * DockingStepSeconds / 2 + 1e-9;
            if (moved > allowed) positionSnapViolations++;

            if (after.Stage == TrainOperatingStage.DockingError) dockingErrorStageEvents++;

            if (after.Stage == TrainOperatingStage.Cruising && after.Motion.SignedSpeedMS != 0)
            {
                var target = operation.Stops[after.StopIndex].ReferenceM;
                var signedGap = Math.Sign(after.Motion.SignedSpeedMS) * (target - after.Motion.ReferenceDistanceM);
                if (signedGap < -1e-9) overshootViolations++;
                var margin = Math.Abs(signedGap) - TrainDynamics.StoppingDistance(after.Motion.SignedSpeedMS, operation.ServiceBrake);
                if (margin < minimumCruiseEnvelopeMarginM) minimumCruiseEnvelopeMarginM = margin;
                if (margin < -1e-9) brakingEnvelopeViolations++;
            }

            if (after.CompletedStops != before.CompletedStops)
            {
                var stop = operation.Stops[after.StopIndex];
                var error = Math.Abs(after.Motion.ReferenceDistanceM - stop.ReferenceM);
                docking.Add(new DockingSample { StopIndex = after.StopIndex, ReferenceM = stop.ReferenceM,
                    StoppedAtM = after.Motion.ReferenceDistanceM, ErrorM = error, SpeedMS = after.Motion.SignedSpeedMS });
                if (error > maximumDockingErrorM) maximumDockingErrorM = error;
                if (error > DockingToleranceM) dockingToleranceViolations++;
            }
        }

        // ------------------------------------------------------------ emergency stop ----

        private static void MeasureEmergencyStop()
        {
            var state = TrainOperation.Create(operation);
            var guard = 0;
            while (!(state.Stage == TrainOperatingStage.Cruising && state.Motion.ReferenceDistanceM >= EmergencyCommandReferenceM))
            {
                TrainOperation.Advance(state, operation, EmergencyStepSeconds, new TrainOperatingInput());
                if (++guard > 40000) throw new InvalidOperationException("Frozen train never reached the mid-line emergency command point.");
            }
            emergencyHeadAtCommandM = state.Motion.ReferenceDistanceM;
            emergencySpeedAtCommandMS = state.Motion.SignedSpeedMS;
            emergencyPhaseAtCommand = (int)state.Motion.Phase;

            TrainOperation.EmergencyStop(state, operation, EmergencyCommandId);
            guard = 0;
            while (state.Stage == TrainOperatingStage.Braking)
            {
                TrainOperation.Advance(state, operation, EmergencyStepSeconds, new TrainOperatingInput());
                if (++guard > 40000) throw new InvalidOperationException("Frozen emergency brake never stopped.");
            }
            emergencyStageAfterStop = (int)state.Stage;
            emergencyStoppedAtM = state.Motion.ReferenceDistanceM;
            emergencyDoorOpenAfterStop = state.DoorOpen;
            emergencyTravelM = emergencyStoppedAtM - emergencyHeadAtCommandM;
            emergencyAnalyticTravelM = TrainDynamics.StoppingDistance(emergencySpeedAtCommandMS, operation.EmergencyBrake);
            emergencyNearestStopDistanceM = operation.Stops.Min(stop => Math.Abs(stop.ReferenceM - emergencyStoppedAtM));

            var held = emergencyStoppedAtM;
            TrainOperation.Advance(state, operation, EmergencyLatchedHoldSeconds, new TrainOperatingInput());
            emergencyDriftWhileLatchedM = Math.Abs(state.Motion.ReferenceDistanceM - held);

            var completedBeforeRecovery = state.CompletedStops;
            TrainOperation.Recover(state, operation);
            offPlatformRecoveryStage = (int)state.Stage;
            offPlatformRecoveryContinueToTarget = state.ContinueToTarget;
            guard = 0;
            while (state.CompletedStops == completedBeforeRecovery)
            {
                TrainOperation.Advance(state, operation, EmergencyStepSeconds, new TrainOperatingInput());
                if (++guard > 40000) throw new InvalidOperationException("Recovered train never reached its platform.");
            }
            offPlatformRecoveryStopIndex = state.StopIndex;
            offPlatformRecoveryDistanceM = state.Motion.ReferenceDistanceM;
            offPlatformRecoveryDockErrorM = Math.Abs(state.Motion.ReferenceDistanceM - operation.Stops[state.StopIndex].ReferenceM);

            var aligned = TrainOperation.Create(operation);
            alignedStopDistanceM = aligned.Motion.ReferenceDistanceM - operation.Stops[aligned.StopIndex].ReferenceM;
            TrainOperation.EmergencyStop(aligned, operation, EmergencyCommandId + ":docked");
            alignedStageAfterEmergencyStop = (int)aligned.Stage;
            TrainOperation.Recover(aligned, operation);
            alignedRecoveryStage = (int)aligned.Stage;
        }

        // ------------------------------------------------------------ boundary hazard probe ----

        /// <summary>Probes, but never adopts, the fixed step at which the shipped integrator writes a build-up
        /// state its own validator refuses. The probe is driven through the shipped public API only, and the
        /// reject is attributed by reconstructing the boundary state with the integrator's own expressions.</summary>
        private static void MeasureEmergencyBoundaryHazard()
        {
            var probe = new TrainMotionState { SignedSpeedMS = FrozenMaximumSpeedMS };
            Assert.That(TrainDynamics.RequestBrake(probe, operation.EmergencyBrake, EmergencyBoundaryHazardCommandId), Is.True,
                "the boundary hazard probe must start from an accepted emergency brake request");
            for (var step = 0; step < EmergencyBoundaryHazardProbeLimitSteps; step++)
            {
                var before = probe.Copy();
                try { TrainDynamics.AdvanceBrake(probe, EmergencyBoundaryHazardStepSeconds); }
                catch (ArgumentException)
                {
                    emergencyBoundaryHazardRejected = true;
                    emergencyBoundaryHazardRejectedAtStep = step;
                    emergencyBoundaryHazardLastValidPhase = (int)before.Phase;
                    emergencyBoundaryHazardSpeedMS = before.SignedSpeedMS;
                    emergencyBoundaryHazardBuildUpBeforeS = before.BuildUpRemainingSeconds;
                    emergencyBoundaryHazardTargetDecelerationMS2 = before.TargetDecelerationMS2;
                    emergencyBoundaryHazardElapsedS = before.BrakeElapsedSeconds;
                    emergencyBoundaryHazardResidueSeconds =
                        before.BuildUpRemainingSeconds - EmergencyBoundaryHazardStepSeconds;
                    emergencyBoundaryHazardRoundedDecelerationMS2 = before.DecelerationMS2 +
                        (before.TargetDecelerationMS2 - before.DecelerationMS2) / before.BuildUpRemainingSeconds *
                        EmergencyBoundaryHazardStepSeconds;
                    return;
                }
                if (probe.Phase == TrainBrakePhase.Stopped) return;
            }
        }

        // ------------------------------------------------------------ determinism ----

        private static void MeasureDeterminism()
        {
            var first = RunShortDocking();
            var second = RunShortDocking();
            determinismFinalReferenceM = first.Motion.ReferenceDistanceM;
            determinismRepeatFinalReferenceM = second.Motion.ReferenceDistanceM;
            determinismFinalBits = BitConverter.DoubleToInt64Bits(first.Motion.ReferenceDistanceM);
            determinismRepeatFinalBits = BitConverter.DoubleToInt64Bits(second.Motion.ReferenceDistanceM);
            determinismStatesIdentical = first.Motion.ReferenceDistanceM == second.Motion.ReferenceDistanceM &&
                first.Motion.SignedSpeedMS == second.Motion.SignedSpeedMS && first.CompletedStops == second.CompletedStops &&
                first.Stage == second.Stage && first.Motion.BrakeElapsedSeconds == second.Motion.BrakeElapsedSeconds;
        }

        private static TrainOperatingState RunShortDocking()
        {
            var state = TrainOperation.Create(operation);
            for (var elapsed = 0.0; elapsed < DeterminismRunSeconds; elapsed += DockingStepSeconds)
                TrainOperation.Advance(state, operation, DockingStepSeconds, new TrainOperatingInput());
            return state;
        }

        // ------------------------------------------------------------ frozen profile binding ----

        [Test]
        public void AuthoredProfileIsTheFrozenTrainThisReceiptWasMeasuredOn()
        {
            Assert.DoesNotThrow(() => profile.Validate(world), "the authored profile must be accepted by the shipped validator");
            Assert.That(route.FrameId, Is.EqualTo(FrozenFrameId));
            Assert.That(operation.TrainId, Is.EqualTo(FrozenTrainId));
            Assert.That(operation.ConsistLengthM, Is.EqualTo(FrozenConsistLengthM));
            Assert.That(operation.RouteStartM, Is.EqualTo(FrozenRouteStartM));
            Assert.That(operation.RouteEndM, Is.EqualTo(FrozenRouteEndM));
            Assert.That(operation.MaximumSpeedMS, Is.EqualTo(FrozenMaximumSpeedMS));
            Assert.That(operation.TractionAccelerationMS2, Is.EqualTo(FrozenTractionAccelerationMS2));
            Assert.That(operation.ClearanceMarginM, Is.EqualTo(FrozenClearanceMarginM));
            Assert.That(operation.DockToleranceM, Is.EqualTo(FrozenDockToleranceM));
            Assert.That(operation.DoorClosingSeconds, Is.EqualTo(FrozenDoorClosingSeconds));
            Assert.That(route.StationReferenceM, Is.EqualTo(FrozenStationReferenceM));
            Assert.That(operation.Stops[0].ReferenceM, Is.EqualTo(FrozenStationReferenceM));

            Assert.That(operation.ServiceBrake.DelaySeconds, Is.EqualTo(FrozenServiceDelayS));
            Assert.That(operation.ServiceBrake.BuildUpSeconds, Is.EqualTo(FrozenServiceBuildUpS));
            Assert.That(operation.ServiceBrake.DecelerationMS2, Is.EqualTo(FrozenServiceDecelerationMS2));
            Assert.That(operation.EmergencyBrake.DelaySeconds, Is.EqualTo(FrozenEmergencyDelayS));
            Assert.That(operation.EmergencyBrake.BuildUpSeconds, Is.EqualTo(FrozenEmergencyBuildUpS));
            Assert.That(operation.EmergencyBrake.DecelerationMS2, Is.EqualTo(FrozenEmergencyDecelerationMS2));

            Assert.That(segments.Length, Is.EqualTo(1));
            Assert.That(segments[0].Id, Is.EqualTo(FrozenSegmentId));
            Assert.That(segments[0].PhysicalTrackId, Is.EqualTo(FrozenPhysicalTrackId));
            Assert.That(segments[0].RouteStartM, Is.EqualTo(FrozenRouteStartM));
            Assert.That(segments[0].LengthM, Is.EqualTo(FrozenRouteEndM));
            Assert.That(segments[0].PhysicalStartM, Is.Zero);
            Assert.That(segments[0].Reversed, Is.False);

            // The published clearance margin is the one the receipt measures occupancy with.
            var published = TrainDynamics.Occupancy(segments, FrozenStationReferenceM, FrozenConsistLengthM, FrozenClearanceMarginM);
            Assert.That(published.Single().StartM, Is.EqualTo(FrozenStationReferenceM - FrozenConsistLengthM - FrozenClearanceMarginM));
            Assert.That(published.Single().EndM, Is.EqualTo(FrozenStationReferenceM + FrozenClearanceMarginM));
        }

        // ------------------------------------------------------------ analytic vs integrated ----

        [Test]
        public void ShippedStoppingDistanceEqualsTheOracleAndTheIntegratorAtEveryFrozenSpeed()
        {
            Assert.That(analytic.Count, Is.EqualTo(AnalyticSpeedsMS.Length * 2));
            foreach (var sample in analytic)
            {
                var context = sample.Profile + " at " + sample.SpeedMS + " m/s";
                Assert.That(sample.ShippedDistanceM, Is.EqualTo(sample.AnalyticDistanceM).Within(AnalyticDistanceToleranceM), "oracle vs shipped query: " + context);
                Assert.That(sample.IntegratedDistanceM, Is.EqualTo(sample.AnalyticDistanceM).Within(AnalyticDistanceToleranceM), "oracle vs integrator: " + context);
                Assert.That(sample.IntegratedTimeS, Is.EqualTo(sample.AnalyticTimeS).Within(AnalyticTimeToleranceS), "oracle vs integrator time: " + context);
                Assert.That(sample.CoarseDistanceM, Is.EqualTo(sample.AnalyticDistanceM).Within(AnalyticDistanceToleranceM), "coarse step distance: " + context);
                Assert.That(sample.CoarseTimeS, Is.EqualTo(sample.AnalyticTimeS).Within(AnalyticTimeToleranceS), "coarse step time: " + context);
                Assert.That(sample.CoarseDistanceM, Is.EqualTo(sample.IntegratedDistanceM).Within(PartitionIndependenceToleranceM), "step partition independence: " + context);
                Assert.That(sample.CoarseTimeS, Is.EqualTo(sample.IntegratedTimeS).Within(PartitionIndependenceToleranceM), "step partition independence in time: " + context);
                Assert.That(sample.AnalyticDistanceM, Is.GreaterThan(0), context);
                Assert.That(sample.AnalyticTimeS, Is.GreaterThan(0), context);
            }
        }

        [Test]
        public void StoppingDistanceAndTimeAtTheFrozenMaximumSpeedMatchTheHandComputedLiterals()
        {
            Assert.That(TrainDynamics.StoppingDistance(FrozenMaximumSpeedMS, operation.ServiceBrake),
                Is.EqualTo(FrozenServiceStopDistanceAtMaxSpeedM).Within(1e-12));
            Assert.That(AnalyticStoppingTimeS(FrozenMaximumSpeedMS, operation.ServiceBrake),
                Is.EqualTo(FrozenServiceStopTimeAtMaxSpeedS).Within(1e-12));
            Assert.That(TrainDynamics.StoppingDistance(FrozenMaximumSpeedMS, operation.EmergencyBrake),
                Is.EqualTo(FrozenEmergencyStopDistanceAtMaxSpeedM).Within(1e-12));
            Assert.That(AnalyticStoppingTimeS(FrozenMaximumSpeedMS, operation.EmergencyBrake),
                Is.EqualTo(FrozenEmergencyStopTimeAtMaxSpeedS).Within(1e-12));

            var service = IntegrateToStop(FrozenMaximumSpeedMS, operation.ServiceBrake, FineStepSeconds, "fmp08b:literal:service");
            var emergency = IntegrateToStop(FrozenMaximumSpeedMS, operation.EmergencyBrake, FineStepSeconds, "fmp08b:literal:emergency");
            Assert.That(service.ReferenceDistanceM, Is.EqualTo(FrozenServiceStopDistanceAtMaxSpeedM).Within(AnalyticDistanceToleranceM));
            Assert.That(service.BrakeElapsedSeconds, Is.EqualTo(FrozenServiceStopTimeAtMaxSpeedS).Within(AnalyticTimeToleranceS));
            Assert.That(emergency.ReferenceDistanceM, Is.EqualTo(FrozenEmergencyStopDistanceAtMaxSpeedM).Within(AnalyticDistanceToleranceM));
            Assert.That(emergency.BrakeElapsedSeconds, Is.EqualTo(FrozenEmergencyStopTimeAtMaxSpeedS).Within(AnalyticTimeToleranceS));
            Assert.That(emergency.ReferenceDistanceM, Is.LessThan(service.ReferenceDistanceM),
                "the emergency envelope must not be longer than the service envelope it preserves");
        }

        [Test]
        public void BrakeDelayAndBuildUpPrecedeFullDecelerationExactlyAsProfiled()
        {
            var motion = new TrainMotionState { SignedSpeedMS = FrozenMaximumSpeedMS };
            Assert.That(TrainDynamics.RequestBrake(motion, operation.ServiceBrake, "fmp08b:phases"), Is.True);
            Assert.That(motion.Phase, Is.EqualTo(TrainBrakePhase.Delay));

            TrainDynamics.AdvanceBrake(motion, FrozenServiceDelayS);
            Assert.That(motion.Phase, Is.EqualTo(TrainBrakePhase.BuildUp));
            Assert.That(motion.SignedSpeedMS, Is.EqualTo(FrozenMaximumSpeedMS).Within(1e-12), "the delay must not change speed");
            Assert.That(motion.ReferenceDistanceM, Is.EqualTo(FrozenMaximumSpeedMS * FrozenServiceDelayS).Within(1e-12));
            Assert.That(motion.DecelerationMS2, Is.Zero, "no deceleration may be applied during the delay");

            TrainDynamics.AdvanceBrake(motion, FrozenServiceBuildUpS);
            var rampedSpeed = FrozenMaximumSpeedMS - FrozenServiceDecelerationMS2 * FrozenServiceBuildUpS / 2;
            var rampedDistance = FrozenMaximumSpeedMS * FrozenServiceBuildUpS -
                FrozenServiceDecelerationMS2 * FrozenServiceBuildUpS * FrozenServiceBuildUpS / 6;
            Assert.That(motion.Phase, Is.EqualTo(TrainBrakePhase.Full));
            Assert.That(motion.SignedSpeedMS, Is.EqualTo(rampedSpeed).Within(1e-12));
            Assert.That(motion.ReferenceDistanceM, Is.EqualTo(FrozenMaximumSpeedMS * FrozenServiceDelayS + rampedDistance).Within(1e-12));
            Assert.That(motion.DecelerationMS2, Is.EqualTo(FrozenServiceDecelerationMS2), "full deceleration must equal the profiled value");

            var remainingM = TrainDynamics.StoppingDistance(FrozenMaximumSpeedMS, operation.ServiceBrake) - motion.ReferenceDistanceM;
            Assert.That(remainingM, Is.EqualTo(rampedSpeed * rampedSpeed / (2 * FrozenServiceDecelerationMS2)).Within(1e-9),
                "the residual distance must be the constant-deceleration remainder");
        }

        // ------------------------------------------------------------ occupancy: entry / rear release ----

        [Test]
        public void OccupancyIsHeldFromHeadEntryUntilTheRearClearsTheBlock()
        {
            var block = PlatformBlock();
            var consist = operation.ConsistLengthM;
            var entry = TrainDynamics.Occupancy(segments, occupancyEntryReferenceM, consist).Single();
            Assert.That(entry.StartM, Is.EqualTo(block.StartM - consist));
            Assert.That(entry.EndM, Is.EqualTo(block.StartM));
            Assert.That(entry.Overlaps(block), Is.False, "the head exactly at the block start has not entered");

            var entered = TrainDynamics.Occupancy(segments, occupancyEntryReferenceM + BoundaryEpsilonM, consist).Single();
            Assert.That(entered.Overlaps(block), Is.True, "the head entering the block must claim the block");

            var fullyInside = TrainDynamics.Occupancy(segments, block.StartM + consist, consist).Single();
            Assert.That(fullyInside.StartM, Is.GreaterThanOrEqualTo(block.StartM));
            Assert.That(fullyInside.EndM, Is.LessThanOrEqualTo(block.EndM));
            Assert.That(fullyInside.Overlaps(block), Is.True, "a whole consist inside the block is occupied");

            var headOutRearIn = TrainDynamics.Occupancy(segments, block.EndM + consist / 2, consist).Single();
            Assert.That(headOutRearIn.EndM, Is.GreaterThan(block.EndM), "the head is already past the block end");
            Assert.That(headOutRearIn.StartM, Is.LessThan(block.EndM), "the rear is still inside the block");
            Assert.That(headOutRearIn.Overlaps(block), Is.True, "the block stays occupied while the rear is on it");

            var atRelease = TrainDynamics.Occupancy(segments, occupancyReleaseReferenceM, consist).Single();
            Assert.That(atRelease.StartM, Is.EqualTo(block.EndM));
            Assert.That(atRelease.Overlaps(block), Is.False, "the block releases exactly when the rear reaches its end");

            var beforeRelease = TrainDynamics.Occupancy(segments, occupancyReleaseReferenceM - BoundaryEpsilonM, consist).Single();
            Assert.That(beforeRelease.Overlaps(block), Is.True, "one micron of rear inside the block still holds it");
        }

        /// <summary>Reference at which the SHIPPED occupancy query stops reporting the block as occupied. The
        /// value is searched for, never restated: the loop can only return a number the shipped
        /// <see cref="TrainDynamics.Occupancy"/> itself answered "free" for, so a rule that released the block at
        /// a different place would move this number and the assertions built on it would fail.</summary>
        private static double FindReleaseReferenceByBisection(TrackOccupancy block, double consistLengthM)
        {
            var occupied = block.EndM;                                   // head exactly at the block end: held
            var free = block.EndM + consistLengthM + SweepStepM;         // rear well past the block end: free
            Assert.That(OccupiesBlock(occupied, consistLengthM, block), Is.True,
                "the release search needs an occupied lower bracket");
            Assert.That(OccupiesBlock(free, consistLengthM, block), Is.False,
                "the release search needs a free upper bracket");
            for (var i = 0; i < ReleaseBisectionIterations; i++)
            {
                var middle = (occupied + free) / 2;
                if (OccupiesBlock(middle, consistLengthM, block)) occupied = middle; else free = middle;
            }
            return free;
        }

        [Test]
        public void OccupancyReleaseIsDecidedByTheRearNotTheHead()
        {
            var block = PlatformBlock();
            var consist = operation.ConsistLengthM;

            // The release boundary is FOUND in the shipped query, not assigned next to it.
            var releaseFound = FindReleaseReferenceByBisection(block, consist);
            Assert.That(releaseFound, Is.EqualTo(block.EndM + consist).Within(BoundaryEpsilonM),
                "the shipped query must keep the block held until the rear, not the head, reaches the block end");
            Assert.That(releaseFound - block.EndM, Is.EqualTo(consist).Within(BoundaryEpsilonM),
                "the found release boundary must be a whole consist length beyond the block end");
            Assert.That(releaseFound - block.EndM, Is.GreaterThan(consist / 2),
                "a value the head-only rule would produce (blockEnd) is not a value this search may return");

            // The head-only predicate is the rejected rule, and the window in which it disagrees with the
            // shipped rule must be non-empty and is measured over a fixed sweep, not asserted into existence.
            Assert.That(HeadOnlySaysClear(block.EndM, block), Is.False, "the head-only rule does not fire before the block end");
            var windowMiddle = block.EndM + consist / 2;
            Assert.That(HeadOnlySaysClear(windowMiddle, block), Is.True, "the head-only rule would free the block here");
            Assert.That(OccupiesBlock(windowMiddle, consist, block), Is.True, "the shipped rule must not free it");
            Assert.That(OccupiesBlock(block.EndM, consist, block), Is.True, "still occupied at the head-only release point");
            Assert.That(occupancyReleaseCounterexamples, Is.GreaterThan(0),
                "the rejected head-only predicate must disagree with the shipped rule somewhere in the window");
            // The published count is cross-checked against three other measured quantities - the first and last
            // disagreeing samples and the sweep step - so it cannot be a number the loop merely accumulated.
            Assert.That(occupancyReleaseCounterexampleFirstM,
                Is.EqualTo(block.EndM + ReleaseSweepStepM).Within(1e-9), "the window opens one sweep step past the block end");
            Assert.That(occupancyReleaseCounterexampleLastM,
                Is.EqualTo(block.EndM + consist - ReleaseSweepStepM).Within(1e-9), "the window closes one sweep step short of the rear release");
            Assert.That(occupancyReleaseCounterexamples,
                Is.EqualTo((int)Math.Round((occupancyReleaseCounterexampleLastM - occupancyReleaseCounterexampleFirstM) /
                    ReleaseSweepStepM) + 1),
                "the published count must be the number of sweep samples between the first and last disagreement");
            Assert.That(occupancyReleaseCounterexamples, Is.EqualTo(ExpectedOccupancyReleaseCounterexamples));

            // And the value the receipt publishes is the value the search found.
            Assert.That(occupancyReleaseReferenceM, Is.EqualTo(releaseFound).Within(BoundaryEpsilonM));
            Assert.That(headOnlyReleaseReferenceM, Is.EqualTo(block.EndM));
        }

        [Test]
        public void RearReleaseMovesWithTheConsistLengthNotWithTheHeadPosition()
        {
            var block = PlatformBlock();
            var foundReleases = new List<double>();
            foreach (var consist in ReleaseProbeConsistLengthsM)
            {
                // Searched for in the shipped query, for each consist length independently.
                var release = FindReleaseReferenceByBisection(block, consist);
                foundReleases.Add(release);
                Assert.That(release, Is.EqualTo(block.EndM + consist).Within(BoundaryEpsilonM),
                    "consist " + consist + " must release at blockEnd + consist in the shipped query");
                Assert.That(OccupiesBlock(release - BoundaryEpsilonM, consist, block), Is.True,
                    "consist " + consist + " must hold one micron earlier");
                Assert.That(OccupiesBlock(block.StartM, consist, block), Is.False,
                    "consist " + consist + " has not entered at the block start");
                Assert.That(OccupiesBlock(block.StartM + BoundaryEpsilonM, consist, block), Is.True,
                    "consist " + consist + " enters with its head");
            }
            // Entry is head-decided and identical for every consist length; release is rear-decided and moves.
            var entryReferences = ReleaseProbeConsistLengthsM.Select(_ => block.StartM).Distinct().ToArray();
            var releaseReferences = foundReleases.Distinct().ToArray();
            Assert.That(entryReferences.Length, Is.EqualTo(1), "every consist length enters the block at the same head position");
            Assert.That(releaseReferences.Length, Is.EqualTo(ReleaseProbeConsistLengthsM.Length),
                "a longer consist must release the block strictly later");
            for (var i = 0; i < ReleaseProbeConsistLengthsM.Length; i++)
                Assert.That(foundReleases[i] - block.EndM - ReleaseProbeConsistLengthsM[i], Is.EqualTo(0).Within(BoundaryEpsilonM),
                    "each found release boundary must sit exactly one consist length past the block end");
            Assert.That(releaseReferences.Min(), Is.GreaterThan(block.EndM),
                "no consist length may release the block at the head-only release point");
        }

        [Test]
        public void RearReleaseOnAReversedSegmentIsStillDecidedByTheRouteAxisRear()
        {
            var reversed = new[] { new TrackRouteSegment { Id = "reversed", PhysicalTrackId = "shared",
                LengthM = ReversedProbeTrackLengthM, Reversed = true } };
            const double consist = 40;
            var block = new TrackOccupancy { PhysicalTrackId = "shared", StartM = 300, EndM = 340 };
            // The route-axis interval is [reference - consist, reference], whichever physical way the segment runs.
            foreach (var reference in new[] { 100.0, 160.0, 160.001, 250.0, 460.0 })
            {
                var interval = TrainDynamics.Occupancy(reversed, reference, consist).Single();
                Assert.That(interval.StartM, Is.EqualTo(ReversedProbeTrackLengthM - reference).Within(1e-9));
                Assert.That(interval.EndM, Is.EqualTo(ReversedProbeTrackLengthM - reference + consist).Within(1e-9));
                Assert.That(interval.EndM - interval.StartM, Is.EqualTo(consist).Within(1e-9), "the whole consist stays occupied");
            }

            // MAPPING PROBE, deliberately NOT labelled a release boundary. At reference 160 the route-axis
            // interval [120, 160] maps to physical [340, 380]: it touches the block's far end and does not cover
            // it. One micron further the consist maps to [339.999999, 379.999999] and covers it. This pins the
            // route-axis -> physical mapping of a reversed segment, which is why 160 appears in the probes at all;
            // the block is not released again at this reference, it is entered from the other side.
            Assert.That(TrainDynamics.Occupancy(reversed, 160, consist).Single().Overlaps(block), Is.False,
                "at reference 160 the consist maps to [340, 380] and does not cover the block");
            Assert.That(TrainDynamics.Occupancy(reversed, 160 + BoundaryEpsilonM, consist).Single().Overlaps(block), Is.True,
                "one micron further it maps onto the block");

            // RELEASE BOUNDARY. Travelling the other way, the block is released at reference 240, where the
            // route-axis rear (physical 300) reaches the block start. The route-axis rear decides, not whichever
            // physical end happens to lead: at 240 the head is already at physical 260, well short of the block.
            Assert.That(TrainDynamics.Occupancy(reversed, ReversedProbeReleaseReferenceM, consist).Single().Overlaps(block), Is.False,
                "the block must be free once the route-axis rear has passed physical 300");
            Assert.That(TrainDynamics.Occupancy(reversed, ReversedProbeReleaseReferenceM - BoundaryEpsilonM, consist).Single().Overlaps(block), Is.True,
                "one micron short of the release reference the block is still held");
            Assert.That(TrainDynamics.Occupancy(reversed, 200, consist).Single().Overlaps(block), Is.True,
                "mid-block the reversed consist still covers it");
            Assert.That(ReversedProbeReleaseReferenceM, Is.Not.EqualTo(ReversedProbeMappingReferenceM),
                "the release boundary is not the mapping boundary 160");
        }

        [Test]
        public void SweptOccupancyCoversEveryIntermediateConsistUnderCoarseSampling()
        {
            var first = platformBlockStartM - FrozenConsistLengthM;
            var last = platformBlockEndM + FrozenConsistLengthM;
            var swept = TrainDynamics.SweptOccupancy(segments, first, last, operation.ConsistLengthM);
            Assert.That(swept.Length, Is.EqualTo(1));
            Assert.That(swept[0].StartM, Is.EqualTo(first - operation.ConsistLengthM));
            Assert.That(swept[0].EndM, Is.EqualTo(last));
            Assert.That(swept[0].Overlaps(PlatformBlock()), Is.True);

            var sampled = 0;
            for (var reference = first; reference <= last; reference += SweepStepM)
            {
                sampled++;
                foreach (var interval in TrainDynamics.Occupancy(segments, reference, operation.ConsistLengthM))
                    Assert.That(swept.Any(s => s.PhysicalTrackId == interval.PhysicalTrackId &&
                        s.StartM <= interval.StartM + 1e-9 && s.EndM >= interval.EndM - 1e-9), Is.True,
                        "the sweep must contain the consist at " + reference + " m");
            }
            Assert.That(sampled, Is.GreaterThan(1));
        }

        [Test]
        public void CrossSegmentRearReleaseKeepsThePhysicalTrackUntilTheRearClearsIt()
        {
            var consist = operation.ConsistLengthM;
            Assert.That(crossSegmentMainlineReleasedAtM, Is.EqualTo(FrozenRouteEndM + consist));
            Assert.That(crossSegmentIntervalsAtHeadOverrun, Is.EqualTo(2),
                "with the head past the mainline end but the rear still on it, both physical tracks are occupied");
            Assert.That(crossSegmentIntervalsAfterRearClear, Is.EqualTo(1),
                "after the rear clears the mainline only the synthetic stub remains occupied");
            Assert.That(crossSegmentMainlineHeldAtM, Is.GreaterThan(FrozenRouteEndM),
                "the held case must have its head past the mainline end");
        }

        [Test]
        public void OccupancyOutsideTheRepresentedRouteIsRejectedAndNeverSilentlyDropped()
        {
            Assert.Throws<ArgumentException>(() => TrainDynamics.Occupancy(segments, FrozenRouteEndM + .5, FrozenConsistLengthM));
            Assert.Throws<ArgumentException>(() => TrainDynamics.Occupancy(segments, FrozenConsistLengthM - .1, FrozenConsistLengthM));
            var atEnd = TrainDynamics.Occupancy(segments, FrozenRouteEndM, FrozenConsistLengthM).Single();
            Assert.That(atEnd.StartM, Is.EqualTo(FrozenRouteEndM - FrozenConsistLengthM));
            Assert.That(atEnd.EndM, Is.EqualTo(FrozenRouteEndM));
            Assert.Throws<ArgumentException>(() => TrainDynamics.SweptOccupancy(segments, atEnd.StartM, FrozenRouteEndM + 1, FrozenConsistLengthM));
        }

        // ------------------------------------------------------------ docking position error ----

        [Test]
        public void AutomaticDockingStaysInsideTheFrozenToleranceAndNeverSnapsOrOvershoots()
        {
            Assert.That(completedStops, Is.GreaterThanOrEqualTo(MinimumCompletedStops));
            Assert.That(docking.Count, Is.EqualTo(completedStops));
            Assert.That(docking.All(sample => sample.SpeedMS == 0), Is.True, "every completed stop must be at zero speed");
            Assert.That(dockingToleranceViolations, Is.Zero);
            Assert.That(positionSnapViolations, Is.Zero, "the trajectory must never jump");
            Assert.That(overshootViolations, Is.Zero, "the consist must never pass its target");
            Assert.That(dockingErrorStageEvents, Is.Zero);
            Assert.That(maximumDockingErrorM, Is.LessThanOrEqualTo(DockingToleranceM));
        }

        [Test]
        public void TheAutomaticApproachNeverEntersTheStoppingEnvelopeWhileCruising()
        {
            // The approach may only brake at or before the analytic stopping distance; this invariant cannot be
            // satisfied by widening a tolerance, because the distance is computed by the shipped query.
            Assert.That(brakingEnvelopeViolations, Is.Zero);
            Assert.That(minimumCruiseEnvelopeMarginM, Is.LessThan(double.PositiveInfinity), "the cruising envelope must have been sampled");
            Assert.That(minimumCruiseEnvelopeMarginM, Is.GreaterThanOrEqualTo(-1e-9));
        }

        // ------------------------------------------------------------ emergency stop is not a docking ----

        [Test]
        public void EmergencyStopBetweenStationsIsNotAssumedToBePlatformAligned()
        {
            Assert.That(emergencyHeadAtCommandM, Is.GreaterThanOrEqualTo(EmergencyCommandReferenceM));
            Assert.That(emergencySpeedAtCommandMS, Is.GreaterThan(0));
            Assert.That(emergencyPhaseAtCommand, Is.EqualTo((int)TrainBrakePhase.Idle),
                "the emergency must be commanded while cruising, before any service brake exists");
            Assert.That(emergencyStageAfterStop, Is.EqualTo((int)TrainOperatingStage.FaultStopped));
            Assert.That(emergencyDoorOpenAfterStop, Is.False, "no door may open at a non-platform stop");

            Assert.That(emergencyTravelM, Is.EqualTo(emergencyAnalyticTravelM).Within(1e-9),
                "the emergency travel must equal the analytic emergency stopping distance");
            Assert.That(emergencyAnalyticTravelM, Is.LessThan(FrozenServiceStopDistanceAtMaxSpeedM + 1e-9));

            Assert.That(emergencyNearestStopDistanceM, Is.GreaterThan(DockingToleranceM),
                "a mid-line emergency stop must not be reported as an authored stop");
            Assert.That(emergencyNearestStopDistanceM, Is.GreaterThanOrEqualTo(EmergencySafetyMarginM),
                "the recorded counterexample must clear the tolerance by a wide margin, not by rounding");

            // An aligned emergency stop must not be a general identity.
            Assert.That(alignedStageAfterEmergencyStop, Is.EqualTo((int)TrainOperatingStage.FaultStopped));
            Assert.That(Math.Abs(alignedStopDistanceM), Is.LessThanOrEqualTo(DockingToleranceM));
            Assert.That(alignedRecoveryStage, Is.EqualTo((int)TrainOperatingStage.Dwell));
            Assert.That(offPlatformRecoveryStage, Is.EqualTo((int)TrainOperatingStage.ClosingDoors),
                "a mid-line fault must resume travel, not become a dwelling stop");
            Assert.That(offPlatformRecoveryStage, Is.Not.EqualTo(alignedRecoveryStage),
                "the aligned and mid-line recoveries are different branches and must both be recorded");
        }

        [Test]
        public void DockedAndMidLineEmergencyStopsTakeDifferentRecoveryBranches()
        {
            Assert.That(emergencyDriftWhileLatchedM, Is.Zero, "a latched fault must not creep");
            Assert.That(offPlatformRecoveryContinueToTarget, Is.True, "the mid-line recovery must continue to its target");
            Assert.That(offPlatformRecoveryStopIndex, Is.EqualTo(1),
                "the recovered train must resume the service it was running when it stopped");
            Assert.That(offPlatformRecoveryDistanceM, Is.GreaterThan(emergencyStoppedAtM),
                "the recovered train must actually travel on rather than dwell where it stopped");
            Assert.That(offPlatformRecoveryDockErrorM, Is.LessThanOrEqualTo(DockingToleranceM),
                "the recovered train must dock inside the frozen tolerance");
        }

        [Test]
        public void TheShippedIntegratorRejectsItsOwnBuildUpBoundaryAndTheMeasuredStepAvoidsIt()
        {
            Assert.That(EmergencyStepSeconds, Is.Not.EqualTo(EmergencyBoundaryHazardStepSeconds),
                "the accepted emergency trajectory must not be taken at the step the probe shows is not carried");
            Assert.That(EmergencyBoundaryHazardProbeLimitSteps * EmergencyBoundaryHazardStepSeconds,
                Is.GreaterThan(FrozenEmergencyStopTimeAtMaxSpeedS),
                "the probe must outlive a whole emergency stop so a clean run cannot be mistaken for a carried step");

            Assert.That(emergencyBoundaryHazardRejected, Is.True,
                "the shipped AdvanceBrake is expected to reject its own build-up boundary state at " +
                EmergencyBoundaryHazardStepSeconds + " s. If that is no longer true, the FMP-08b hand-off recorded in " +
                "docs/evidence/work-items/FMP-08b/result.json must be re-measured deliberately, not deleted silently.");
            Assert.That(emergencyBoundaryHazardLastValidPhase, Is.EqualTo((int)TrainBrakePhase.BuildUp));
            Assert.That(emergencyBoundaryHazardBuildUpBeforeS, Is.GreaterThan(0),
                "the rejected step must have been the one that would have exhausted the build-up ramp");

            // The contradictory pair: a ramp remainder that is a pure rounding residue, together with the
            // deceleration already rounded onto the target. Either one alone is legal during BuildUp.
            Assert.That(emergencyBoundaryHazardResidueSeconds, Is.GreaterThan(0),
                "the remainder must be a residue, not a completed ramp (a zero would have flipped the phase)");
            Assert.That(emergencyBoundaryHazardResidueSeconds, Is.LessThan(1e-15),
                "a residue this small is a rounding artefact, not a physical ramp");
            Assert.That(emergencyBoundaryHazardRoundedDecelerationMS2,
                Is.EqualTo(emergencyBoundaryHazardTargetDecelerationMS2),
                "the integrator rounds the ramp straight onto the target deceleration while the residue remains");

            // The shipped validator, not this harness, is the arbiter: it refuses exactly the state the shipped
            // integrator would have written. This is what makes the hazard a defect rather than a test artefact.
            Assert.Throws<ArgumentException>(() => TrainDynamics.ValidateState(new TrainMotionState {
                SignedSpeedMS = emergencyBoundaryHazardSpeedMS,
                BrakeDirection = Math.Sign(emergencyBoundaryHazardSpeedMS),
                Phase = TrainBrakePhase.BuildUp,
                DecelerationMS2 = emergencyBoundaryHazardRoundedDecelerationMS2,
                TargetDecelerationMS2 = emergencyBoundaryHazardTargetDecelerationMS2,
                BuildUpRemainingSeconds = emergencyBoundaryHazardResidueSeconds,
                BrakeCommandId = EmergencyBoundaryHazardCommandId,
                BrakeElapsedSeconds = emergencyBoundaryHazardElapsedS }),
                "the shipped validator rejects the build-up boundary state the shipped integrator writes");
        }

        [Test]
        public void EmergencyTrajectoryDoesNotDependOnTheFixedStep()
        {
            // Partition independence at eight independent fixed steps, including steps finer and coarser than
            // both the delay (0.2 s) and the build-up (0.3 s). If the shipped integrator ever resolved a phase
            // boundary differently depending on the partition, this test - not the analytic comparison at a
            // single step - is what would catch it.
            var oracleDistanceM = AnalyticStoppingDistanceM(FrozenMaximumSpeedMS, operation.EmergencyBrake);
            var oracleTimeS = AnalyticStoppingTimeS(FrozenMaximumSpeedMS, operation.EmergencyBrake);
            Assert.That(oracleDistanceM, Is.EqualTo(FrozenEmergencyStopDistanceAtMaxSpeedM).Within(AnalyticDistanceToleranceM));
            Assert.That(oracleTimeS, Is.EqualTo(FrozenEmergencyStopTimeAtMaxSpeedS).Within(AnalyticTimeToleranceS));
            Assert.That(TrainDynamics.StoppingDistance(FrozenMaximumSpeedMS, operation.EmergencyBrake),
                Is.EqualTo(oracleDistanceM).Within(AnalyticDistanceToleranceM), "the shipped query must agree with the oracle");

            var steps = new[] { .005, .01, .02, .025, .04, .06, .075, .1 };
            Assert.That(steps.Length, Is.GreaterThanOrEqualTo(6), "the partition sweep must not be a single step");

            // The exclusion of .05 s is ASSERTED here, not narrated: .05 s is the one partition of this envelope
            // the shipped integrator cannot carry, because the build-up boundary state it writes is refused by the
            // shipped ValidateState. Adding that step to the sweep as an expected refusal is what keeps "we simply
            // did not run it" from being an unfalsifiable claim, and it fails loudly if the shipped boundary is
            // ever changed - in which case docs/evidence/work-items/FMP-08b/result.json shippedBoundaryHandoff
            // must be re-measured rather than left standing.
            Assert.That(steps, Has.No.Member(EmergencyBoundaryHazardStepSeconds),
                "the accepted partition sweep must not silently include the step the hazard probe refuses");
            var refusedStep = Assert.Throws<ArgumentException>(() => IntegrateToStop(
                FrozenMaximumSpeedMS, operation.EmergencyBrake, EmergencyBoundaryHazardStepSeconds, "fmp08b:partition:refused"));
            Assert.That(refusedStep.Message, Does.Contain("Inconsistent train brake phase"),
                "the refused step must fail through the shipped phase validator, not through an unrelated error");

            foreach (var step in steps)
            {
                var motion = IntegrateToStop(FrozenMaximumSpeedMS, operation.EmergencyBrake, step,
                    "fmp08b:partition:" + step);
                Assert.That(motion.Phase, Is.EqualTo(TrainBrakePhase.Stopped),
                    "every partition must reach a stop, step " + step);
                Assert.That(motion.ReferenceDistanceM, Is.EqualTo(oracleDistanceM).Within(AnalyticDistanceToleranceM),
                    "the emergency stopping distance must not depend on the fixed step, step " + step);
                Assert.That(motion.BrakeElapsedSeconds, Is.EqualTo(oracleTimeS).Within(AnalyticTimeToleranceS),
                    "the emergency stopping time must not depend on the fixed step, step " + step);
            }
        }

        // ------------------------------------------------------------ thresholds are pinned ----

        [Test]
        public void RecordedBudgetsCannotBeRelaxedByTheHarness()
        {
            Assert.That(DockingToleranceM, Is.EqualTo(.05));
            Assert.That(operation.DockToleranceM, Is.EqualTo(DockingToleranceM));
            Assert.That(operation.ClearanceMarginM, Is.EqualTo(FrozenClearanceMarginM));
            Assert.That(AnalyticDistanceToleranceM, Is.EqualTo(1e-9));
            Assert.That(AnalyticTimeToleranceS, Is.EqualTo(1e-9));
            Assert.That(PartitionIndependenceToleranceM, Is.EqualTo(1e-7));
            Assert.That(FrozenServiceStopDistanceAtMaxSpeedM, Is.EqualTo(26.391666666666667));
            Assert.That(FrozenEmergencyStopDistanceAtMaxSpeedM, Is.EqualTo(17.0955));

            // The shipped validator itself rejects a widened docking tolerance, so the accepted profile cannot
            // carry a loosened threshold even if this harness wanted one.
            var widened = ParseProfile();
            widened.Trains.Single(t => t.Operation.TrainId == FrozenTrainId).Operation.DockToleranceM = .06;
            Assert.Throws<ArgumentException>(() => widened.Validate(world));

            // A weakened emergency envelope is rejected for the same reason.
            var weakened = ParseProfile();
            weakened.Trains.Single(t => t.Operation.TrainId == FrozenTrainId).Operation.EmergencyBrake.DecelerationMS2 = .7;
            Assert.Throws<ArgumentException>(() => weakened.Validate(world));

            // Every measured result recorded in this receipt is inside the pinned budget.
            Assert.That(maximumDockingErrorM, Is.LessThanOrEqualTo(DockingToleranceM));
            Assert.That(dockingToleranceViolations, Is.Zero);
        }

        private static FoundationSimulationProfile ParseProfile() =>
            JsonUtility.FromJson<FoundationSimulationProfile>(File.ReadAllText(ProfileRelativePath));

        // ------------------------------------------------------------ determinism ----

        [Test]
        public void RepeatedFrozenRunsAreBitIdentical()
        {
            Assert.That(determinismStatesIdentical, Is.True, "two identical frozen runs must agree exactly");
            Assert.That(determinismFinalReferenceM, Is.EqualTo(determinismRepeatFinalReferenceM));
            Assert.That(determinismFinalBits, Is.EqualTo(determinismRepeatFinalBits));
        }

        [Test]
        public void TheReceiptCannotPublishABitPatternItsOwnReferencePositionRefutes()
        {
            // The receipt publishes both a double and its bit pattern for the same quantity. These assertions stop
            // it from publishing a pair that contradicts itself, and the round trip through the WRITTEN file is
            // included because the failure this guards against happened at publication, not in the measurement.
            Assert.That(BitConverter.DoubleToInt64Bits(determinismFinalReferenceM), Is.EqualTo(determinismFinalBits),
                "the recorded bit pattern must be the bit pattern of the recorded reference position");
            Assert.That(BitConverter.DoubleToInt64Bits(determinismRepeatFinalReferenceM), Is.EqualTo(determinismRepeatFinalBits),
                "the recorded repeat bit pattern must be the bit pattern of the recorded repeat reference position");
            Assert.That(BitConverter.Int64BitsToDouble(determinismFinalBits), Is.EqualTo(determinismFinalReferenceM),
                "the recorded bit pattern must decode back to the recorded reference position");

            var written = File.ReadAllText(EvidenceDirectory + "/" + ReceiptFileName);
            var published = JsonUtility.FromJson<AnalyticReceipt>(written);
            Assert.That(published.DeterminismFinalBits, Is.EqualTo(determinismFinalBits),
                "the serialised receipt must carry the measured bit pattern without loss");
            Assert.That(published.DeterminismRepeatFinalBits, Is.EqualTo(determinismRepeatFinalBits));
            Assert.That(BitConverter.Int64BitsToDouble(published.DeterminismFinalBits), Is.EqualTo(determinismFinalReferenceM),
                "the serialised bit pattern must decode to the position it was measured from, independently of how "
                + "the decimal field is rendered");
            Assert.That(published.OccupancyReleaseReferenceM, Is.EqualTo(occupancyReleaseReferenceM).Within(BoundaryEpsilonM),
                "the serialised receipt must carry the bisected release reference");
            Assert.That(published.OccupancyReleaseCounterexamples, Is.EqualTo(occupancyReleaseCounterexamples),
                "the serialised receipt must carry the measured counterexample count");

            // MEASURED LIMITATION, not an assumption: the decimal rendering of the reference position does NOT
            // round trip bit-exactly through this reader. JsonUtility writes a bounded number of decimal digits,
            // and re-reading that text recovers an adjacent double. The bit field is therefore the authoritative
            // carrier, and how far the decimal field drifts is published as an exact whole number of units in the
            // last place - not as a double, which would lose the very low bits it is describing.
            Assert.That(published.DeterminismFinalReferenceM,
                Is.EqualTo(determinismFinalReferenceM).Within(JsonDecimalRoundTripToleranceM),
                "the decimal field re-read from the receipt must stay within the frozen decimal round-trip bound");
            var measuredRoundTripUlps = (long)Math.Round(
                Math.Abs(published.DeterminismFinalReferenceM - determinismFinalReferenceM) / ReferenceUlpM());
            Assert.That(published.DeterminismDecimalRoundTripUlps, Is.EqualTo(measuredRoundTripUlps),
                "the published round-trip drift must be the drift this read measured, in exact ulps");
            Assert.That(published.DeterminismFinalReferenceM, Is.Not.EqualTo(determinismFinalReferenceM),
                "the decimal round-trip limitation recorded in docs/evidence/work-items/FMP-08b/result.json is "
                + "only real while the two fields differ; if this ever holds, remove the limitation from the record");
        }

        // ------------------------------------------------------------ receipt ----

        [Test]
        public void AnalyticReceiptCarriesTheRecordedNumbers()
        {
            Assert.That(analytic.Count, Is.EqualTo(AnalyticSpeedsMS.Length * 2));
            Assert.That(occupancy.Count, Is.GreaterThan(0));
            Assert.That(docking.Count, Is.GreaterThan(0));
            Assert.That(File.Exists(EvidenceDirectory + "/" + ReceiptFileName), Is.True,
                "the receipt must be written next to the Temp evidence directory used by the other FMP receipts");
        }

        // ------------------------------------------------------------ receipt DTOs ----

        [Serializable]
        private sealed class AnalyticSample
        {
            public string Profile;
            public double SpeedMS, AnalyticDistanceM, AnalyticTimeS, ShippedDistanceM;
            public double IntegratedDistanceM, IntegratedTimeS, CoarseDistanceM, CoarseTimeS;
        }

        [Serializable]
        private sealed class OccupancySample
        {
            public string Case;
            public double ReferenceM, ConsistLengthM, BlockStartM, BlockEndM, IntervalStartM, IntervalEndM;
            public int IntervalCount;
            public bool OverlapsBlock, HeadPastBlockEnd;
        }

        [Serializable]
        private sealed class DockingSample
        {
            public int StopIndex;
            public double ReferenceM, StoppedAtM, ErrorM, SpeedMS;
        }

        [Serializable]
        private sealed class AnalyticReceipt
        {
            public int SchemaVersion = 1;
            public string WorkId = "FMP-08b";
            public string Classification = "PUBLIC_PROJECT_EVIDENCE";
            public string SourceRef = ProfileRelativePath;
            public string WorldRef = WorldRelativePath;
            public string TrainId, FrameId, SegmentId, PhysicalTrackId;
            public double ConsistLengthM, RouteStartM, RouteEndM, MaximumSpeedMS, TractionAccelerationMS2;
            public double ClearanceMarginM, DockToleranceM, DoorClosingSeconds, StationReferenceM;
            public double ServiceDelayS, ServiceBuildUpS, ServiceDecelerationMS2;
            public double EmergencyDelayS, EmergencyBuildUpS, EmergencyDecelerationMS2;
            public double ServiceStopDistanceAtMaxSpeedM, ServiceStopTimeAtMaxSpeedS;
            public double EmergencyStopDistanceAtMaxSpeedM, EmergencyStopTimeAtMaxSpeedS;
            public double AnalyticDistanceToleranceM, AnalyticTimeToleranceS, PartitionIndependenceToleranceM;
            public double FineStepSeconds, CoarseStepSeconds, DockingStepSeconds, BoundaryEpsilonM;

            public AnalyticSample[] Analytic;
            public OccupancySample[] Occupancy;
            public DockingSample[] Docking;

            public double PlatformBlockStartM, PlatformBlockEndM;
            public double OccupancyEntryReferenceM, OccupancyReleaseReferenceM, HeadOnlyReleaseReferenceM;
            public int OccupancyReleaseCounterexamples;
            public double OccupancyReleaseCounterexampleFirstM, OccupancyReleaseCounterexampleLastM;
            public double ReversedProbeReleaseReferenceM, ReversedProbeMappingReferenceM;
            public double CrossSegmentMainlineHeldAtM, CrossSegmentMainlineReleasedAtM;
            public int CrossSegmentIntervalsAtHeadOverrun, CrossSegmentIntervalsAfterRearClear;

            public int CompletedStops, DockingToleranceViolations, PositionSnapViolations, OvershootViolations;
            public int BrakingEnvelopeViolations, DockingErrorStageEvents;
            public double MaximumDockingErrorM, MinimumCruiseEnvelopeMarginM;

            public double EmergencyHeadAtCommandM, EmergencySpeedAtCommandMS, EmergencyStoppedAtM;
            public double EmergencyAnalyticTravelM, EmergencyTravelM, EmergencyNearestStopDistanceM;
            public int EmergencyStageAfterStop, OffPlatformRecoveryStage, OffPlatformRecoveryStopIndex, AlignedRecoveryStage;
            public bool EmergencyDoorOpenAfterStop, OffPlatformRecoveryContinueToTarget;
            public double OffPlatformRecoveryDockErrorM, OffPlatformRecoveryDistanceM, EmergencyDriftWhileLatchedM;
            public double DeterminismFinalReferenceM, DeterminismRepeatFinalReferenceM;
            public long DeterminismFinalBits, DeterminismRepeatFinalBits;
            public long DeterminismDecimalRoundTripUlps;
            public bool DeterminismStatesIdentical;

            public bool EmergencyBoundaryHazardRejected;
            public int EmergencyBoundaryHazardRejectedAtStep, EmergencyBoundaryHazardLastValidPhase;
            public double EmergencyBoundaryHazardStepSeconds, EmergencyBoundaryHazardBuildUpBeforeS;
            public double EmergencyBoundaryHazardResidueSeconds, EmergencyBoundaryHazardRoundedDecelerationMS2;
            public double EmergencyBoundaryHazardTargetDecelerationMS2;

            public string[] NegativeCaseGuards;
            public string[] ReusedChecks;
            public string[] Limits;
        }

        private static AnalyticReceipt BuildReceipt() => new AnalyticReceipt {
            TrainId = operation.TrainId, FrameId = route.FrameId, SegmentId = segments[0].Id, PhysicalTrackId = segments[0].PhysicalTrackId,
            ConsistLengthM = operation.ConsistLengthM, RouteStartM = operation.RouteStartM, RouteEndM = operation.RouteEndM,
            MaximumSpeedMS = operation.MaximumSpeedMS, TractionAccelerationMS2 = operation.TractionAccelerationMS2,
            ClearanceMarginM = operation.ClearanceMarginM, DockToleranceM = operation.DockToleranceM,
            DoorClosingSeconds = operation.DoorClosingSeconds, StationReferenceM = route.StationReferenceM,
            ServiceDelayS = operation.ServiceBrake.DelaySeconds, ServiceBuildUpS = operation.ServiceBrake.BuildUpSeconds,
            ServiceDecelerationMS2 = operation.ServiceBrake.DecelerationMS2,
            EmergencyDelayS = operation.EmergencyBrake.DelaySeconds, EmergencyBuildUpS = operation.EmergencyBrake.BuildUpSeconds,
            EmergencyDecelerationMS2 = operation.EmergencyBrake.DecelerationMS2,
            ServiceStopDistanceAtMaxSpeedM = TrainDynamics.StoppingDistance(FrozenMaximumSpeedMS, operation.ServiceBrake),
            ServiceStopTimeAtMaxSpeedS = AnalyticStoppingTimeS(FrozenMaximumSpeedMS, operation.ServiceBrake),
            EmergencyStopDistanceAtMaxSpeedM = TrainDynamics.StoppingDistance(FrozenMaximumSpeedMS, operation.EmergencyBrake),
            EmergencyStopTimeAtMaxSpeedS = AnalyticStoppingTimeS(FrozenMaximumSpeedMS, operation.EmergencyBrake),
            AnalyticDistanceToleranceM = AnalyticDistanceToleranceM, AnalyticTimeToleranceS = AnalyticTimeToleranceS,
            PartitionIndependenceToleranceM = PartitionIndependenceToleranceM,
            FineStepSeconds = FineStepSeconds, CoarseStepSeconds = CoarseStepSeconds,
            DockingStepSeconds = DockingStepSeconds, BoundaryEpsilonM = BoundaryEpsilonM,

            Analytic = analytic.ToArray(), Occupancy = occupancy.ToArray(), Docking = docking.ToArray(),

            PlatformBlockStartM = platformBlockStartM, PlatformBlockEndM = platformBlockEndM,
            OccupancyEntryReferenceM = occupancyEntryReferenceM, OccupancyReleaseReferenceM = occupancyReleaseReferenceM,
            HeadOnlyReleaseReferenceM = headOnlyReleaseReferenceM, OccupancyReleaseCounterexamples = occupancyReleaseCounterexamples,
            OccupancyReleaseCounterexampleFirstM = occupancyReleaseCounterexampleFirstM,
            OccupancyReleaseCounterexampleLastM = occupancyReleaseCounterexampleLastM,
            ReversedProbeReleaseReferenceM = ReversedProbeReleaseReferenceM,
            ReversedProbeMappingReferenceM = ReversedProbeMappingReferenceM,
            CrossSegmentMainlineHeldAtM = crossSegmentMainlineHeldAtM, CrossSegmentMainlineReleasedAtM = crossSegmentMainlineReleasedAtM,
            CrossSegmentIntervalsAtHeadOverrun = crossSegmentIntervalsAtHeadOverrun,
            CrossSegmentIntervalsAfterRearClear = crossSegmentIntervalsAfterRearClear,

            CompletedStops = completedStops, DockingToleranceViolations = dockingToleranceViolations,
            PositionSnapViolations = positionSnapViolations, OvershootViolations = overshootViolations,
            BrakingEnvelopeViolations = brakingEnvelopeViolations, DockingErrorStageEvents = dockingErrorStageEvents,
            MaximumDockingErrorM = maximumDockingErrorM, MinimumCruiseEnvelopeMarginM = minimumCruiseEnvelopeMarginM,

            EmergencyHeadAtCommandM = emergencyHeadAtCommandM, EmergencySpeedAtCommandMS = emergencySpeedAtCommandMS,
            EmergencyStoppedAtM = emergencyStoppedAtM, EmergencyAnalyticTravelM = emergencyAnalyticTravelM,
            EmergencyTravelM = emergencyTravelM, EmergencyNearestStopDistanceM = emergencyNearestStopDistanceM,
            EmergencyStageAfterStop = emergencyStageAfterStop, OffPlatformRecoveryStage = offPlatformRecoveryStage,
            OffPlatformRecoveryStopIndex = offPlatformRecoveryStopIndex, AlignedRecoveryStage = alignedRecoveryStage,
            EmergencyDoorOpenAfterStop = emergencyDoorOpenAfterStop,
            OffPlatformRecoveryContinueToTarget = offPlatformRecoveryContinueToTarget,
            OffPlatformRecoveryDockErrorM = offPlatformRecoveryDockErrorM,
            OffPlatformRecoveryDistanceM = offPlatformRecoveryDistanceM,
            EmergencyDriftWhileLatchedM = emergencyDriftWhileLatchedM,
            DeterminismFinalReferenceM = determinismFinalReferenceM, DeterminismRepeatFinalReferenceM = determinismRepeatFinalReferenceM,
            DeterminismFinalBits = determinismFinalBits, DeterminismRepeatFinalBits = determinismRepeatFinalBits,
            DeterminismStatesIdentical = determinismStatesIdentical,

            EmergencyBoundaryHazardRejected = emergencyBoundaryHazardRejected,
            EmergencyBoundaryHazardRejectedAtStep = emergencyBoundaryHazardRejectedAtStep,
            EmergencyBoundaryHazardLastValidPhase = emergencyBoundaryHazardLastValidPhase,
            EmergencyBoundaryHazardStepSeconds = EmergencyBoundaryHazardStepSeconds,
            EmergencyBoundaryHazardBuildUpBeforeS = emergencyBoundaryHazardBuildUpBeforeS,
            EmergencyBoundaryHazardResidueSeconds = emergencyBoundaryHazardResidueSeconds,
            EmergencyBoundaryHazardRoundedDecelerationMS2 = emergencyBoundaryHazardRoundedDecelerationMS2,
            EmergencyBoundaryHazardTargetDecelerationMS2 = emergencyBoundaryHazardTargetDecelerationMS2,

            NegativeCaseGuards = new[] {
                "consist-head-only occupancy release: the release reference is bisected out of the shipped Occupancy query (" +
                    occupancyReleaseReferenceM + " m, found by search and not assigned next to it) while the head-only predicate " +
                    "would release at " + headOnlyReleaseReferenceM + " m; " + occupancyReleaseCounterexamples +
                    " sampled references between " + occupancyReleaseCounterexampleFirstM + " m and " +
                    occupancyReleaseCounterexampleLastM + " m disagree with the shipped rule, and that count is cross-checked " +
                    "against those two endpoints and the sweep step rather than against itself.",
                "reversed-segment probe labels: the route-axis reference " + ReversedProbeMappingReferenceM +
                    " is an entry/mapping probe (the mapped interval touches the block's far end and one micron further covers it), " +
                    "while the pass-release boundary is " + ReversedProbeReleaseReferenceM +
                    " (free there, held one micron earlier). The two are asserted to be different references so a mapping " +
                    "boundary cannot be re-published as a release boundary.",
                "emergency stop assumed platform aligned: the mid-line emergency ended at " + emergencyStoppedAtM +
                    " m with the nearest authored stop " + emergencyNearestStopDistanceM + " m away, stage " + emergencyStageAfterStop +
                    " (FaultStopped), door closed, and Recover went to stage " + offPlatformRecoveryStage + " (ClosingDoors) with ContinueToTarget=" +
                    offPlatformRecoveryContinueToTarget + "; the genuinely aligned branch is recorded separately as stage " + alignedRecoveryStage + " (Dwell).",
                "threshold relaxed after seeing the result: the docking tolerance used is the authored " + DockingToleranceM +
                    " (the shipped validator rejects 0.06), the analytic tolerances are consts asserted equal to 1e-9 and 1e-7, and the stopping distances and times at " +
                    FrozenMaximumSpeedMS + " m/s are pinned to full precision.",
                "fixed step chosen after seeing the result: the accepted emergency partition sweep excludes " +
                    EmergencyBoundaryHazardStepSeconds + " s, and that exclusion is an assertion, not prose - the same test drives " +
                    "the shipped AdvanceBrake at that step and asserts it is refused with the shipped phase-validator message." },
            ReusedChecks = new[] {
                "ChooGuard.Foundation.Tests.TrainDynamicsTests - re-run in the same recorded testFilter invocation; the shipped analytic distance/time contract is untouched.",
                "ChooGuard.Foundation.Tests.TrainOperationTests - re-run in the same recorded testFilter invocation; the shipped docking/dwell/fault contract is untouched.",
                "No file under Runtime/Simulation was modified by this work item." },
            Limits = new[] {
                "Synthetic level route with a synthetic constant-deceleration brake envelope; no real vehicle brake curve and no real vehicle measurement.",
                "Consist lengths, route length and the platform block are authored game dimensions, not surveyed ones.",
                "No signalling, no block occupancy interlocking and no operator rule book is modelled; occupancy is a geometric interval only.",
                "The extra 60 m stub used for the cross-segment rear-release case is declared synthetic and is not part of the authored route.",
                "HAND-OFF, deliberately not patched: TrainDynamics.AdvanceBrake can write a build-up state that its own ValidateState rejects at the frozen emergency profile when the fixed step is " + EmergencyBoundaryHazardStepSeconds + " s. Measured on the shipped API: the request is accepted, " + emergencyBoundaryHazardRejectedAtStep + " accepted steps follow, and the step that would complete the ramp leaves BuildUpRemainingSeconds at " + emergencyBoundaryHazardResidueSeconds + " s with DecelerationMS2 already rounded onto the target " + emergencyBoundaryHazardRoundedDecelerationMS2 + " m/s^2 (target " + emergencyBoundaryHazardTargetDecelerationMS2 + " m/s^2). Nothing under Runtime/Simulation was modified - shipped brake logic needs tests and lead review - so the accepted trajectory instead uses " + EmergencyStepSeconds + " s and the hazard is recorded here.",
                "PUBLICATION LIMIT, measured and not assumed: the doubles in this receipt - and therefore in docs/evidence/work-items/FMP-08b/result.json - are decimal text. Re-reading a rendered value can recover an adjacent binary64, so a reader's reparse of the reference position can differ from the measured one in the last unit in the place. The drift this reader measures is published as DeterminismDecimalRoundTripUlps (an exact whole number of ulps, because a drift expressed as a double loses the low bits it is describing) and the decimal field is checked against a const bound rather than asserted to be exact. The bit pattern field (DeterminismFinalBits), not the decimal field, is the bit-exact carrier of that position.",
                "Whole-server frame budget is NOT_ASSESSED here; this receipt records kinematics and occupancy only." }
        };

        /// <summary>Distance between the measured reference position and the next representable double above it,
        /// i.e. one unit in the last place. Used to express the decimal round-trip gap as an exact integer, since
        /// the gap expressed as a double is itself subject to the same rendering loss.</summary>
        private static double ReferenceUlpM() =>
            BitConverter.Int64BitsToDouble(determinismFinalBits + 1) - determinismFinalReferenceM;

        private static void WriteReceipt()
        {
            var receipt = BuildReceipt();
            var json = JsonUtility.ToJson(receipt, true);
            Directory.CreateDirectory(EvidenceDirectory);
            File.WriteAllText(EvidenceDirectory + "/" + ReceiptFileName, json);

            // The receipt is written twice on purpose. The first write is the exact bytes a reader will get; the
            // second carries what that first write revealed back into the receipt, so the limitation is a
            // published number rather than a claim in prose. The gap is published as a whole number of units in
            // the last place because a gap expressed as a double loses the same low bits it is measuring.
            var reread = JsonUtility.FromJson<AnalyticReceipt>(File.ReadAllText(EvidenceDirectory + "/" + ReceiptFileName));
            receipt.DeterminismDecimalRoundTripUlps = (long)Math.Round(
                Math.Abs(reread.DeterminismFinalReferenceM - receipt.DeterminismFinalReferenceM) / ReferenceUlpM());
            json = JsonUtility.ToJson(receipt, true);
            File.WriteAllText(EvidenceDirectory + "/" + ReceiptFileName, json);
            // Unity clears Temp/ on exit, so the receipt must also survive in the run log.
            Debug.Log(ReceiptMarker + " " + json);
        }
    }
}
