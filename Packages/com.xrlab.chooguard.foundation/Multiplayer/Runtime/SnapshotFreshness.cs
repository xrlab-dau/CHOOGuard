using System;

namespace ChooGuard.Foundation.Multiplayer
{
    public sealed class SnapshotFreshness
    {
        public const double MaximumAgeSeconds=.5;
        private double receivedAt=-1;
        public void Received(double now)
        { Validate(now); if (now<receivedAt) throw new ArgumentException("Snapshot clock went backwards."); receivedAt=now; }
        public bool IsCurrent(double now)
        { Validate(now); return receivedAt>=0 && now>=receivedAt && now-receivedAt<=MaximumAgeSeconds; }
        public double AgeSeconds(double now) { Validate(now); return receivedAt<0 ? -1 : Math.Max(0,now-receivedAt); }
        private static void Validate(double now) { if (double.IsNaN(now)||double.IsInfinity(now)||now<0) throw new ArgumentException("Invalid snapshot clock."); }
    }
}
