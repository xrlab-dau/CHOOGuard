using System;
using System.Collections.Generic;
using System.Linq;

namespace ChooGuard.Foundation.Simulation
{
    public enum TrainBrakePhase { Idle, Delay, BuildUp, Full, Stopped }

    [Serializable]
    public sealed class TrainBrakeProfile
    {
        public double DelaySeconds, BuildUpSeconds, DecelerationMS2;
    }

    [Serializable]
    public sealed class TrainMotionState
    {
        // Reference is the consist's positive-route-axis end, not whichever end currently leads.
        public double ReferenceDistanceM, SignedSpeedMS;
        public TrainBrakePhase Phase;
        public string BrakeCommandId = "";
        public int BrakeDirection;
        public double DelayRemainingSeconds, BuildUpRemainingSeconds, DecelerationMS2, TargetDecelerationMS2, BrakeElapsedSeconds;
        public TrainMotionState Copy() => (TrainMotionState)MemberwiseClone();
    }

    [Serializable]
    public sealed class TrackRouteSegment
    {
        public string Id, PhysicalTrackId;
        public double RouteStartM, LengthM, PhysicalStartM;
        public bool Reversed;
    }

    [Serializable]
    public sealed class TrackOccupancy
    {
        public string PhysicalTrackId;
        public double StartM, EndM;
        public bool Overlaps(TrackOccupancy other) => other != null && PhysicalTrackId == other.PhysicalTrackId &&
            StartM < other.EndM && EndM > other.StartM;
    }

    /// <summary>Exact piecewise delay/constant-jerk braking on a synthetic level route; no real vehicle curve is implied.</summary>
    public static class TrainDynamics
    {
        public static bool RequestBrake(TrainMotionState state, TrainBrakeProfile profile, string commandId)
        {
            ValidateState(state);
            ValidateProfile(profile);
            if (string.IsNullOrWhiteSpace(commandId) || commandId.Length > 128) throw new ArgumentException("Invalid synthetic brake command ID.");
            if (state.SignedSpeedMS == 0 || state.Phase == TrainBrakePhase.Stopped) return false;
            var active = state.Phase != TrainBrakePhase.Idle;
            if (active && (commandId == state.BrakeCommandId || profile.DecelerationMS2 < state.TargetDecelerationMS2)) return false;
            if (active && profile.DecelerationMS2 == state.TargetDecelerationMS2 &&
                !(profile.BuildUpSeconds < state.BuildUpRemainingSeconds || profile.DelaySeconds < state.DelayRemainingSeconds)) return false;
            var minimumJerk = 0.0;
            if (active && state.TargetDecelerationMS2 > state.DecelerationMS2)
            {
                if (state.BuildUpRemainingSeconds > 0)
                    minimumJerk = (state.TargetDecelerationMS2 - state.DecelerationMS2) / state.BuildUpRemainingSeconds;
                else if (state.DelayRemainingSeconds > 0)
                    minimumJerk = (state.TargetDecelerationMS2 - state.DecelerationMS2) / state.DelayRemainingSeconds;
            }
            state.BrakeCommandId = commandId; state.BrakeDirection = Math.Sign(state.SignedSpeedMS);
            state.TargetDecelerationMS2 = profile.DecelerationMS2;
            state.DelayRemainingSeconds = active ? 0 : profile.DelaySeconds;
            state.BuildUpRemainingSeconds = profile.BuildUpSeconds;
            if (minimumJerk > 0)
                state.BuildUpRemainingSeconds = Math.Min(state.BuildUpRemainingSeconds,
                    (profile.DecelerationMS2 - state.DecelerationMS2) / minimumJerk);
            if (!active) { state.DecelerationMS2 = 0; state.BrakeElapsedSeconds = 0; }
            if (state.DelayRemainingSeconds > 0) state.Phase = TrainBrakePhase.Delay;
            else if (state.BuildUpRemainingSeconds > 0) state.Phase = TrainBrakePhase.BuildUp;
            else { state.DecelerationMS2 = state.TargetDecelerationMS2; state.Phase = TrainBrakePhase.Full; }
            return true;
        }

        public static void AdvanceBrake(TrainMotionState state, double seconds)
        {
            ValidateState(state);
            if (!Nonnegative(seconds) || seconds > 86400) throw new ArgumentException("Invalid train integration interval.");
            if (state.Phase == TrainBrakePhase.Idle) throw new ArgumentException("Train has no accepted brake request.");
            if (state.Phase == TrainBrakePhase.Stopped || seconds == 0) return;
            var next = state.Copy(); var remaining = seconds;
            while (remaining > 0 && next.Phase != TrainBrakePhase.Stopped)
            {
                var speed = Math.Abs(next.SignedSpeedMS);
                if (next.Phase == TrainBrakePhase.Delay)
                {
                    var dt = Math.Min(remaining, next.DelayRemainingSeconds);
                    next.ReferenceDistanceM += next.BrakeDirection * speed * dt;
                    next.DelayRemainingSeconds -= dt; next.BrakeElapsedSeconds += dt; remaining -= dt;
                    if (next.DelayRemainingSeconds == 0)
                    {
                        next.Phase = next.BuildUpRemainingSeconds > 0 ? TrainBrakePhase.BuildUp : TrainBrakePhase.Full;
                        if (next.Phase == TrainBrakePhase.Full) next.DecelerationMS2 = next.TargetDecelerationMS2;
                    }
                    continue;
                }
                var acceleration = next.DecelerationMS2;
                var jerk = next.Phase == TrainBrakePhase.BuildUp ? (next.TargetDecelerationMS2 - acceleration) / next.BuildUpRemainingSeconds : 0;
                var stoppingTime = jerk > 0 ? 2 * speed / (acceleration + Math.Sqrt(acceleration * acceleration + 2 * jerk * speed)) : speed / acceleration;
                var phaseRemaining = next.Phase == TrainBrakePhase.BuildUp ? next.BuildUpRemainingSeconds : double.PositiveInfinity;
                var h = Math.Min(remaining, Math.Min(phaseRemaining, stoppingTime));
                if (!Positive(h)) throw new InvalidOperationException("Train brake phase made no progress.");
                next.ReferenceDistanceM += next.BrakeDirection * (speed * h - acceleration * h * h / 2 - jerk * h * h * h / 6);
                next.BrakeElapsedSeconds += h; remaining -= h;
                if (h == stoppingTime)
                {
                    next.SignedSpeedMS = 0; next.DecelerationMS2 = 0; next.DelayRemainingSeconds = 0;
                    next.BuildUpRemainingSeconds = 0; next.Phase = TrainBrakePhase.Stopped; break;
                }
                next.SignedSpeedMS = next.BrakeDirection * (speed - acceleration * h - jerk * h * h / 2);
                next.DecelerationMS2 = acceleration + jerk * h;
                if (next.Phase == TrainBrakePhase.BuildUp)
                {
                    next.BuildUpRemainingSeconds -= h;
                    if (next.BuildUpRemainingSeconds == 0)
                    { next.DecelerationMS2 = next.TargetDecelerationMS2; next.Phase = TrainBrakePhase.Full; }
                }
            }
            ValidateState(next);
            state.ReferenceDistanceM = next.ReferenceDistanceM; state.SignedSpeedMS = next.SignedSpeedMS; state.Phase = next.Phase;
            state.DelayRemainingSeconds = next.DelayRemainingSeconds; state.BuildUpRemainingSeconds = next.BuildUpRemainingSeconds;
            state.DecelerationMS2 = next.DecelerationMS2; state.BrakeElapsedSeconds = next.BrakeElapsedSeconds;
        }

        public static double StoppingDistance(double speedMS, TrainBrakeProfile profile)
        {
            ValidateProfile(profile);
            if (!Finite(speedMS) || Math.Abs(speedMS) > 1000) throw new ArgumentException("Invalid train speed.");
            if (speedMS == 0) return 0;
            var speed = Math.Abs(speedMS); var a = profile.DecelerationMS2; var ramp = profile.BuildUpSeconds;
            var distance = speed * profile.DelaySeconds;
            if (ramp == 0) distance += speed * speed / (2 * a);
            else if (speed <= a * ramp / 2)
            { var t = Math.Sqrt(2 * ramp * speed / a); distance += speed * t - a * t * t * t / (6 * ramp); }
            else
            { var afterRamp = speed - a * ramp / 2; distance += speed * ramp - a * ramp * ramp / 6 + afterRamp * afterRamp / (2 * a); }
            if (!Finite(distance)) throw new ArgumentException("Stopping query exceeds numeric range.");
            return distance;
        }

        public static TrackOccupancy[] Occupancy(TrackRouteSegment[] route, double referenceM, double consistLengthM, double marginM = 0) =>
            SweptOccupancy(route, referenceM, referenceM, consistLengthM, marginM);

        public static TrackOccupancy[] SweptOccupancy(TrackRouteSegment[] route, double previousReferenceM, double nextReferenceM, double consistLengthM, double marginM = 0)
        {
            ValidateRoute(route);
            if (!Finite(previousReferenceM) || !Finite(nextReferenceM) || !Positive(consistLengthM) || !Nonnegative(marginM))
                throw new ArgumentException("Invalid consist occupancy.");
            var start = Math.Min(previousReferenceM, nextReferenceM) - consistLengthM - marginM;
            var end = Math.Max(previousReferenceM, nextReferenceM) + marginM;
            if (start < route[0].RouteStartM || end > route[route.Length - 1].RouteStartM + route[route.Length - 1].LengthM)
                throw new ArgumentException("Consist extends beyond the represented route; occupancy cannot be silently dropped.");
            var result = new List<TrackOccupancy>();
            foreach (var segment in route)
            {
                var low = Math.Max(start, segment.RouteStartM); var high = Math.Min(end, segment.RouteStartM + segment.LengthM);
                if (low >= high) continue;
                var a = low - segment.RouteStartM; var b = high - segment.RouteStartM;
                result.Add(new TrackOccupancy { PhysicalTrackId = segment.PhysicalTrackId,
                    StartM = segment.PhysicalStartM + (segment.Reversed ? segment.LengthM - b : a),
                    EndM = segment.PhysicalStartM + (segment.Reversed ? segment.LengthM - a : b) });
            }
            return result.ToArray();
        }

        private static void ValidateRoute(TrackRouteSegment[] route)
        {
            if (route == null || route.Length == 0 || route.Any(s => s == null || string.IsNullOrWhiteSpace(s.Id) ||
                string.IsNullOrWhiteSpace(s.PhysicalTrackId) || !Nonnegative(s.RouteStartM) || !Nonnegative(s.PhysicalStartM) || !Positive(s.LengthM)) ||
                route.Select(s => s.Id).Distinct().Count() != route.Length) throw new ArgumentException("Invalid physical track route.");
            for (var i = 1; i < route.Length; i++)
                if (Math.Abs(route[i].RouteStartM - route[i - 1].RouteStartM - route[i - 1].LengthM) > 1e-8)
                    throw new ArgumentException("Track route contains an unrepresented gap or overlap.");
        }
        public static void ValidateState(TrainMotionState s)
        {
            if (s == null || !Finite(s.ReferenceDistanceM) || !Finite(s.SignedSpeedMS) || Math.Abs(s.SignedSpeedMS) > 1000 ||
                !Enum.IsDefined(typeof(TrainBrakePhase), s.Phase) || !Nonnegative(s.DelayRemainingSeconds) ||
                !Nonnegative(s.BuildUpRemainingSeconds) || !Nonnegative(s.DecelerationMS2) || !Nonnegative(s.TargetDecelerationMS2) ||
                !Nonnegative(s.BrakeElapsedSeconds)) throw new ArgumentException("Invalid train motion state.");
            if (s.Phase != TrainBrakePhase.Idle && s.Phase != TrainBrakePhase.Stopped &&
                (s.BrakeDirection != Math.Sign(s.SignedSpeedMS) || s.SignedSpeedMS == 0 || !Positive(s.TargetDecelerationMS2) ||
                 string.IsNullOrWhiteSpace(s.BrakeCommandId) || s.DecelerationMS2 > s.TargetDecelerationMS2 ||
                 s.Phase == TrainBrakePhase.BuildUp && (s.BuildUpRemainingSeconds == 0 || s.DelayRemainingSeconds != 0 || s.DecelerationMS2 == s.TargetDecelerationMS2) ||
                 s.Phase == TrainBrakePhase.Full && (s.DecelerationMS2 != s.TargetDecelerationMS2 || s.DelayRemainingSeconds != 0 || s.BuildUpRemainingSeconds != 0) ||
                 s.Phase == TrainBrakePhase.Delay && (s.DelayRemainingSeconds == 0 || s.DecelerationMS2 != 0)))
                throw new ArgumentException("Inconsistent train brake phase.");
            if (s.Phase == TrainBrakePhase.Stopped && (s.SignedSpeedMS != 0 || s.DecelerationMS2 != 0 || s.DelayRemainingSeconds != 0 || s.BuildUpRemainingSeconds != 0))
                throw new ArgumentException("Inconsistent stopped train.");
            if (s.Phase == TrainBrakePhase.Idle && (s.DecelerationMS2 != 0 || s.TargetDecelerationMS2 != 0 || s.DelayRemainingSeconds != 0 || s.BuildUpRemainingSeconds != 0))
                throw new ArgumentException("Idle train contains active brake state.");
        }
        private static void ValidateProfile(TrainBrakeProfile p)
        {
            if (p == null || !Nonnegative(p.DelaySeconds) || !Nonnegative(p.BuildUpSeconds) ||
                !Positive(p.DecelerationMS2) || p.DecelerationMS2 > 100) throw new ArgumentException("Invalid synthetic brake profile.");
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool Positive(double x) => Finite(x) && x > 0;
        private static bool Nonnegative(double x) => Finite(x) && x >= 0;
    }
}
