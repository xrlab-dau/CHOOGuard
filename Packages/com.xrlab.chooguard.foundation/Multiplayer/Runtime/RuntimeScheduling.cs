using System;

namespace ChooGuard.Foundation.Multiplayer
{
    public static class ServerCatchupBudget
    {
        // Preserve the accumulated simulation debt, but give NGO another network pump
        // when a slow tick has already exhausted one 20 Hz wall-clock budget.
        public static bool CanContinue(float accumulatedSeconds, int ticks, double elapsedMilliseconds)
            => accumulatedSeconds >= .05f && ticks < 4 && (ticks == 0 || elapsedMilliseconds < 50);
    }

    public sealed class ProbeStepCursor
    {
        public int Next { get; private set; }
        public void Drain(ProbeStep[] steps, float elapsedSeconds, Func<ProbeStep, bool> submit)
        {
            while (steps != null && Next < steps.Length && steps[Next].AtSeconds <= elapsedSeconds)
            {
                if (!submit(steps[Next])) return;
                Next++;
            }
        }
    }
}
