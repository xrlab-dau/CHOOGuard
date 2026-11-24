using System;

namespace ChooGuard.Seeded.Unity
{
    /// <summary>Defers automatic provider preparation while an offline scene borrows station geometry.</summary>
    public sealed class OfflineStationStartupScope : IDisposable
    {
        static int depth;
        bool disposed;
        public static bool IsDeferred => depth > 0;
        public OfflineStationStartupScope() { checked { depth++; } }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            depth = Math.Max(0, depth - 1);
        }
    }
}
