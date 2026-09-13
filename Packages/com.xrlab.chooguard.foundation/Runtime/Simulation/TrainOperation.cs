using System;
using System.Linq;

namespace ChooGuard.Foundation.Simulation
{
    public enum TrainOperatingStage { Dwell, ClosingDoors, Cruising, Braking, FaultStopped, DockingError }

    [Serializable] public sealed class TrainStopDefinition
    { public double ReferenceM, DwellSeconds; public bool Boarding; }

    [Serializable] public sealed class TrainOperatingDefinition
    {
        public string TrainId;
        public double ConsistLengthM, RouteStartM, RouteEndM, MaximumSpeedMS, TractionAccelerationMS2;
        public double ClearanceMarginM = 1, DockToleranceM = .05, DoorClosingSeconds = 1;
        public TrainBrakeProfile ServiceBrake, EmergencyBrake;
        public TrainStopDefinition[] Stops;
    }

    [Serializable] public sealed class TrainOperatingInput
    {
        public bool BoardingAreaClear = true, AllowDeparture = true, DoorRequestedOpen = true;
    }

    [Serializable] public sealed class TrainOperatingState
    {
        public string TrainId;
        public TrainMotionState Motion;
        public TrainOperatingStage Stage;
        public int StopIndex;
        public long CompletedStops;
        public double DwellRemainingSeconds, ClosingRemainingSeconds, ElapsedSeconds;
        public bool DoorOpen, FaultLatched, ContinueToTarget;
        public TrainOperatingState Copy() { var copy = (TrainOperatingState)MemberwiseClone(); copy.Motion = Motion?.Copy(); return copy; }
    }

    /// <summary>Reviewed synthetic automatic operation. Stops are event-split; position is never snapped to a dock.</summary>
    public static class TrainOperation
    {
        public static TrainOperatingState Create(TrainOperatingDefinition d)
        {
            ValidateDefinition(d);
            return new TrainOperatingState { TrainId = d.TrainId, Motion = new TrainMotionState { ReferenceDistanceM = d.Stops[0].ReferenceM },
                Stage = TrainOperatingStage.Dwell, StopIndex = 0, DwellRemainingSeconds = d.Stops[0].DwellSeconds, DoorOpen = d.Stops[0].Boarding };
        }

        public static void Advance(TrainOperatingState state, TrainOperatingDefinition d, double seconds, TrainOperatingInput input)
        {
            Validate(state, d);
            if (input == null || !Finite(seconds) || seconds < 0 || seconds > 3600) throw new ArgumentException("Invalid train operation interval/input.");
            var next = state.Copy(); var remaining = seconds; var transitions = 0;
            while (remaining > 1e-12)
            {
                if (++transitions > 10000) throw new InvalidOperationException("Train operation event budget exhausted.");
                if (next.Stage == TrainOperatingStage.DockingError) break;
                if (next.Stage == TrainOperatingStage.FaultStopped)
                { SetDockDoor(next, d, input); break; }
                if (next.Stage == TrainOperatingStage.Dwell)
                {
                    SetDockDoor(next, d, input);
                    var dt = Math.Min(remaining, next.DwellRemainingSeconds); next.DwellRemainingSeconds -= dt; remaining -= dt;
                    if (next.DwellRemainingSeconds > 0 || !input.AllowDeparture || !input.BoardingAreaClear) break;
                    next.Stage = TrainOperatingStage.ClosingDoors; next.ClosingRemainingSeconds = d.DoorClosingSeconds;
                    next.ContinueToTarget = false; next.DoorOpen = false; continue;
                }
                if (next.Stage == TrainOperatingStage.ClosingDoors)
                {
                    if (!input.BoardingAreaClear || !input.AllowDeparture)
                    {
                        if (AtBoardingStop(next, d)) next.DoorOpen = true;
                        next.ClosingRemainingSeconds = d.DoorClosingSeconds; break;
                    }
                    next.DoorOpen = false;
                    var dt = Math.Min(remaining, next.ClosingRemainingSeconds); next.ClosingRemainingSeconds -= dt; remaining -= dt;
                    if (next.ClosingRemainingSeconds > 0) break;
                    if (!next.ContinueToTarget) next.StopIndex = (next.StopIndex + 1) % d.Stops.Length;
                    next.ContinueToTarget = false;
                    next.Motion = new TrainMotionState { ReferenceDistanceM = next.Motion.ReferenceDistanceM };
                    next.Stage = TrainOperatingStage.Cruising; continue;
                }
                if (next.Stage == TrainOperatingStage.Cruising)
                { AdvanceCruise(next, d, ref remaining); continue; }
                if (next.Stage == TrainOperatingStage.Braking)
                {
                    var before = next.Motion.BrakeElapsedSeconds;
                    TrainDynamics.AdvanceBrake(next.Motion, remaining);
                    remaining = Math.Max(0, remaining - (next.Motion.BrakeElapsedSeconds - before));
                    if (next.Motion.Phase != TrainBrakePhase.Stopped) break;
                    if (next.FaultLatched) { next.Stage = TrainOperatingStage.FaultStopped; SetDockDoor(next, d, input); break; }
                    if (Math.Abs(next.Motion.ReferenceDistanceM - d.Stops[next.StopIndex].ReferenceM) > d.DockToleranceM)
                    { next.Stage = TrainOperatingStage.DockingError; next.DoorOpen = false; break; }
                    next.CompletedStops = checked(next.CompletedStops + 1);
                    next.Stage = TrainOperatingStage.Dwell; next.DwellRemainingSeconds = d.Stops[next.StopIndex].DwellSeconds;
                    SetDockDoor(next, d, input);
                }
            }
            next.ElapsedSeconds += seconds; Validate(next, d); CopyInto(next, state);
        }

        private static void AdvanceCruise(TrainOperatingState s, TrainOperatingDefinition d, ref double remaining)
        {
            var target = d.Stops[s.StopIndex].ReferenceM;
            var direction = Math.Sign(target - s.Motion.ReferenceDistanceM);
            var speed = Math.Abs(s.Motion.SignedSpeedMS);
            if (direction == 0 && speed == 0)
            { s.Stage = TrainOperatingStage.Dwell; s.DwellRemainingSeconds = d.Stops[s.StopIndex].DwellSeconds; s.CompletedStops++; return; }
            if (direction == 0 || speed > 0 && Math.Sign(s.Motion.SignedSpeedMS) != direction)
                throw new InvalidOperationException("Train passed its target without a validated braking transition.");
            var acceleration = speed < d.MaximumSpeedMS ? d.TractionAccelerationMS2 : 0;
            var toCap = acceleration > 0 ? (d.MaximumSpeedMS - speed) / acceleration : double.PositiveInfinity;
            var segment = Math.Min(remaining, toCap);
            double RemainingAfter(double time) => Math.Abs(target - s.Motion.ReferenceDistanceM) - speed * time - acceleration * time * time / 2 -
                TrainDynamics.StoppingDistance(Math.Min(d.MaximumSpeedMS, speed + acceleration * time), d.ServiceBrake);
            if (RemainingAfter(0) <= 0)
            { StartServiceBrake(s, d); return; }
            if (RemainingAfter(segment) <= 0)
            {
                var low = 0.0; var high = segment;
                for (var i = 0; i < 40; i++)
                { var middle = (low + high) / 2; if (RemainingAfter(middle) > 0) low = middle; else high = middle; }
                segment = high;
                s.Motion.ReferenceDistanceM += direction * (speed * segment + acceleration * segment * segment / 2);
                s.Motion.SignedSpeedMS = direction * Math.Min(d.MaximumSpeedMS, speed + acceleration * segment);
                remaining = Math.Max(0, remaining - segment); StartServiceBrake(s, d); return;
            }
            if (segment <= 0) throw new InvalidOperationException("Train speed-cap phase made no progress.");
            s.Motion.ReferenceDistanceM += direction * (speed * segment + acceleration * segment * segment / 2);
            s.Motion.SignedSpeedMS = direction * Math.Min(d.MaximumSpeedMS, speed + acceleration * segment);
            remaining = Math.Max(0, remaining - segment);
        }

        private static void StartServiceBrake(TrainOperatingState state, TrainOperatingDefinition d)
        {
            if (!TrainDynamics.RequestBrake(state.Motion, d.ServiceBrake, "auto:" + state.CompletedStops + ":" + state.StopIndex))
                throw new InvalidOperationException("Automatic approach could not request braking.");
            state.Stage = TrainOperatingStage.Braking; state.DoorOpen = false;
        }

        public static void EmergencyStop(TrainOperatingState state, TrainOperatingDefinition d, string commandId)
        {
            Validate(state, d);
            if (string.IsNullOrWhiteSpace(commandId)) throw new ArgumentException("Missing emergency command ID.");
            var next = state.Copy(); next.FaultLatched = true;
            if (next.Motion.SignedSpeedMS == 0) next.Stage = TrainOperatingStage.FaultStopped;
            else
            {
                TrainDynamics.RequestBrake(next.Motion, d.EmergencyBrake, commandId);
                next.Stage = TrainOperatingStage.Braking; next.DoorOpen = false;
            }
            Validate(next, d); CopyInto(next, state);
        }

        public static void Recover(TrainOperatingState state, TrainOperatingDefinition d)
        {
            Validate(state, d);
            if (state.Stage != TrainOperatingStage.FaultStopped || state.Motion.SignedSpeedMS != 0)
                throw new InvalidOperationException("Only a stopped fault can be explicitly recovered.");
            var next = state.Copy(); next.FaultLatched = false;
            if (Math.Abs(next.Motion.ReferenceDistanceM - d.Stops[next.StopIndex].ReferenceM) <= d.DockToleranceM)
            { next.Stage = TrainOperatingStage.Dwell; next.DwellRemainingSeconds = d.Stops[next.StopIndex].DwellSeconds; }
            else
            { next.Stage = TrainOperatingStage.ClosingDoors; next.ContinueToTarget = true; next.ClosingRemainingSeconds = d.DoorClosingSeconds; }
            Validate(next, d); CopyInto(next, state);
        }

        private static bool AtBoardingStop(TrainOperatingState s, TrainOperatingDefinition d) =>
            d.Stops.Any(stop => stop.Boarding && Math.Abs(stop.ReferenceM - s.Motion.ReferenceDistanceM) <= d.DockToleranceM);
        private static void SetDockDoor(TrainOperatingState s, TrainOperatingDefinition d, TrainOperatingInput input)
        {
            if (!AtBoardingStop(s, d)) { s.DoorOpen = false; return; }
            if (input.DoorRequestedOpen) s.DoorOpen = true;
            else if (input.BoardingAreaClear) s.DoorOpen = false;
        }
        private static void CopyInto(TrainOperatingState source, TrainOperatingState target)
        {
            target.Motion = source.Motion.Copy(); target.Stage = source.Stage; target.StopIndex = source.StopIndex;
            target.CompletedStops = source.CompletedStops; target.DwellRemainingSeconds = source.DwellRemainingSeconds;
            target.ClosingRemainingSeconds = source.ClosingRemainingSeconds; target.ElapsedSeconds = source.ElapsedSeconds;
            target.DoorOpen = source.DoorOpen; target.FaultLatched = source.FaultLatched; target.ContinueToTarget = source.ContinueToTarget;
        }
        private static void ValidateDefinition(TrainOperatingDefinition d)
        {
            if (d == null || string.IsNullOrWhiteSpace(d.TrainId) || d.TrainId.Length > 128 || !Positive(d.ConsistLengthM) ||
                !Nonnegative(d.RouteStartM) || !Positive(d.RouteEndM - d.RouteStartM) || !Positive(d.MaximumSpeedMS) || d.MaximumSpeedMS > 1000 ||
                !Positive(d.TractionAccelerationMS2) || !Nonnegative(d.ClearanceMarginM) || !Positive(d.DockToleranceM) || d.DockToleranceM > .5 ||
                !Nonnegative(d.DoorClosingSeconds) || d.Stops == null || d.Stops.Length < 2 || d.Stops.Length > 64 ||
                d.Stops.Any(s => s == null || !Finite(s.ReferenceM) || !Nonnegative(s.DwellSeconds) ||
                    s.ReferenceM - d.ConsistLengthM - d.ClearanceMarginM < d.RouteStartM || s.ReferenceM + d.ClearanceMarginM > d.RouteEndM))
                throw new ArgumentException("Invalid synthetic train operating definition.");
            TrainDynamics.StoppingDistance(1, d.ServiceBrake); TrainDynamics.StoppingDistance(1, d.EmergencyBrake);
            if (d.EmergencyBrake.DecelerationMS2 < d.ServiceBrake.DecelerationMS2 || d.EmergencyBrake.DelaySeconds > d.ServiceBrake.DelaySeconds ||
                d.EmergencyBrake.BuildUpSeconds > d.ServiceBrake.BuildUpSeconds)
                throw new ArgumentException("Operating emergency profile must preserve the service braking envelope.");
            for (var i = 0; i < d.Stops.Length; i++)
                if (Math.Abs(d.Stops[i].ReferenceM - d.Stops[(i + 1) % d.Stops.Length].ReferenceM) < .1)
                    throw new ArgumentException("Consecutive train stops must be distinct.");
        }
        public static void Validate(TrainOperatingState s, TrainOperatingDefinition d)
        {
            ValidateDefinition(d);
            if (s == null || s.TrainId != d.TrainId || s.Motion == null || !Enum.IsDefined(typeof(TrainOperatingStage), s.Stage) ||
                s.StopIndex < 0 || s.StopIndex >= d.Stops.Length || s.CompletedStops < 0 || !Nonnegative(s.DwellRemainingSeconds) ||
                !Nonnegative(s.ClosingRemainingSeconds) || !Nonnegative(s.ElapsedSeconds) || !Finite(s.Motion.ReferenceDistanceM) ||
                !Finite(s.Motion.SignedSpeedMS) || Math.Abs(s.Motion.SignedSpeedMS) > d.MaximumSpeedMS + 1e-8 ||
                s.Motion.ReferenceDistanceM - d.ConsistLengthM - d.ClearanceMarginM < d.RouteStartM - 1e-8 ||
                s.Motion.ReferenceDistanceM + d.ClearanceMarginM > d.RouteEndM + 1e-8 ||
                (s.Stage == TrainOperatingStage.Dwell || s.Stage == TrainOperatingStage.ClosingDoors || s.Stage == TrainOperatingStage.FaultStopped ||
                    s.Stage == TrainOperatingStage.DockingError) && s.Motion.SignedSpeedMS != 0 ||
                s.DoorOpen && (s.Motion.SignedSpeedMS != 0 || !AtBoardingStop(s, d)) ||
                s.Stage == TrainOperatingStage.FaultStopped && !s.FaultLatched)
                throw new ArgumentException("Invalid train operation state.");
            TrainDynamics.ValidateState(s.Motion);
            var stoppedPhase = s.Motion.Phase == TrainBrakePhase.Idle || s.Motion.Phase == TrainBrakePhase.Stopped;
            var atTarget = Math.Abs(s.Motion.ReferenceDistanceM - d.Stops[s.StopIndex].ReferenceM) <= d.DockToleranceM;
            if (s.FaultLatched && s.Stage != TrainOperatingStage.Braking && s.Stage != TrainOperatingStage.FaultStopped ||
                s.Stage == TrainOperatingStage.Braking && (stoppedPhase || s.Motion.SignedSpeedMS == 0) ||
                s.Stage == TrainOperatingStage.Cruising && (s.Motion.Phase != TrainBrakePhase.Idle || s.DoorOpen ||
                    s.Motion.SignedSpeedMS != 0 && Math.Sign(s.Motion.SignedSpeedMS) != Math.Sign(d.Stops[s.StopIndex].ReferenceM - s.Motion.ReferenceDistanceM)) ||
                s.Stage == TrainOperatingStage.Dwell && (!stoppedPhase || !atTarget || s.DwellRemainingSeconds > d.Stops[s.StopIndex].DwellSeconds + 1e-9) ||
                s.Stage == TrainOperatingStage.ClosingDoors && (!stoppedPhase || !s.ContinueToTarget && !atTarget) ||
                s.Stage == TrainOperatingStage.FaultStopped && !stoppedPhase ||
                s.Stage == TrainOperatingStage.DockingError && s.Motion.Phase != TrainBrakePhase.Stopped ||
                s.DoorOpen && s.Stage != TrainOperatingStage.Dwell && s.Stage != TrainOperatingStage.ClosingDoors && s.Stage != TrainOperatingStage.FaultStopped)
                throw new ArgumentException("Contradictory train operation/brake/dock state.");
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool Positive(double x) => Finite(x) && x > 0;
        private static bool Nonnegative(double x) => Finite(x) && x >= 0;
    }
}
