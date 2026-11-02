using System;

namespace ChooGuard.App.Fps.TrainDeparture
{
    internal enum DepartureRadioPlaybackStatus { Interrupted, Playing, Completed }

    /// <summary>Native PCM/DSP observations, separated for failure-path tests. Never completes on elapsed time alone.</summary>
    internal sealed class DepartureRadioPlaybackGuard
    {
        internal struct Observation
        {
            internal bool SourceAndClipValid, IsPlaying;
            internal int Position;
            internal double DspTime;
        }
        readonly DepartureGuardedOperation<Observation> complete;
        DepartureRadioPlaybackStatus status;
        int samples, frequency, lastPosition;
        double startDsp, lastDsp, lastProgressDsp;
        bool progressed;
        const double MaximumObservationGap = .25;
        static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);

        internal DepartureRadioPlaybackGuard()
        {
            // Executes the existing MIT UnityHFSM-derived guarded commit helper.
            complete = new DepartureGuardedOperation<Observation>(CanComplete,
                _ => status = DepartureRadioPlaybackStatus.Completed);
        }
        internal bool Begin(int sampleCount, int sampleFrequency, double dspTime)
        {
            Reset();
            if (sampleCount <= 0 || sampleFrequency <= 0 || !Finite(dspTime)) return false;
            samples = sampleCount; frequency = sampleFrequency;
            startDsp = lastDsp = lastProgressDsp = dspTime;
            status = DepartureRadioPlaybackStatus.Playing;
            return true;
        }
        bool CanComplete(Observation observation)
        {
            // Native natural end may retain clip.samples; legacy backends/reset paths use zero.
            // Accept either terminal cursor only while stopped. Remaining PCM samples must
            // fit the ACTUAL observation gap, not a fixed short tail window.
            // DSP endpoint + observed progress remain required; this is not a timer-only result.
            double length = samples / (double)frequency;
            double gap = observation.DspTime - lastDsp;
            double possibleSampleAdvance = Math.Ceiling(gap * frequency);
            bool terminalCursor = observation.Position == 0 || observation.Position == samples;
            return observation.SourceAndClipValid && !observation.IsPlaying && terminalCursor && progressed &&
                observation.DspTime >= startDsp + length && gap >= 0 && gap <= MaximumObservationGap &&
                samples - lastPosition <= possibleSampleAdvance;
        }
        internal DepartureRadioPlaybackStatus Observe(bool sourceAndClipValid, bool playing, int position, double dspTime)
        {
            if (status != DepartureRadioPlaybackStatus.Playing) return status;
            var observation = new Observation { SourceAndClipValid = sourceAndClipValid, IsPlaying = playing, Position = position, DspTime = dspTime };
            if (!sourceAndClipValid || !Finite(dspTime) || dspTime < lastDsp || dspTime - lastDsp > MaximumObservationGap ||
                position < 0 || position > samples || playing && position == samples)
                return status = DepartureRadioPlaybackStatus.Interrupted;
            if (!playing)
            {
                if (!complete.TryApply(observation)) status = DepartureRadioPlaybackStatus.Interrupted;
                return status;
            }
            if (position < lastPosition) return status = DepartureRadioPlaybackStatus.Interrupted; // Seek/restart/loop is not the accepted take.
            if (position > lastPosition) { progressed = true; lastProgressDsp = dspTime; }
            else if (dspTime - lastProgressDsp > MaximumObservationGap) return status = DepartureRadioPlaybackStatus.Interrupted;
            lastPosition = position; lastDsp = dspTime;
            return status;
        }
        internal void Reset()
        {
            status = DepartureRadioPlaybackStatus.Interrupted;
            samples = frequency = lastPosition = 0;
            startDsp = lastDsp = lastProgressDsp = 0;
            progressed = false;
        }
    }
}
