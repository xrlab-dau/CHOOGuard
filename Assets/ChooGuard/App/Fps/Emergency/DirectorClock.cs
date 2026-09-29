using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>What a stretch of main-thread work cost, in milliseconds on the wall clock and on the thread's own CPU clock.</summary>
    public struct Cost
    {
        public float WallMs, CpuMs;

        public static Cost operator +(Cost a, Cost b) => new Cost { WallMs = a.WallMs + b.WallMs, CpuMs = a.CpuMs + b.CpuMs };
    }

    /// <summary>
    /// Measures a stretch of main-thread work for the shift record. The wall clock includes every time the thread was
    /// pre-empted, which on a busy machine (several Unity editors sharing the CPU) dwarfs the work itself; where the platform
    /// exposes the calling thread's CPU time (the macOS editor, where measurement runs are made) that is measured too, so
    /// the record can tell the code's cost from the machine's load. Elsewhere <see cref="CpuMeasured"/> is false.
    /// </summary>
    public readonly struct DirectorSpan
    {
        private readonly long wall, cpu;

#if UNITY_EDITOR_OSX
        // CLOCK_THREAD_CPUTIME_ID on macOS.
        [DllImport("libc")]
        private static extern ulong clock_gettime_nsec_np(int clockId);

        public static bool CpuMeasured => true;
        private static long CpuNow() => (long)clock_gettime_nsec_np(16);
#else
        public static bool CpuMeasured => false;
        private static long CpuNow() => 0;
#endif

        private DirectorSpan(long wallStamp, long cpuStamp)
        {
            wall = wallStamp;
            cpu = cpuStamp;
        }

        public static DirectorSpan Begin() => new DirectorSpan(Stopwatch.GetTimestamp(), CpuNow());

        /// <summary>The cost since <see cref="Begin"/>.</summary>
        public Cost Stop() => new Cost
        {
            WallMs = (float)((Stopwatch.GetTimestamp() - wall) * 1000.0 / Stopwatch.Frequency),
            CpuMs = CpuMeasured ? (CpuNow() - cpu) / 1e6f : 0f,
        };
    }
}
