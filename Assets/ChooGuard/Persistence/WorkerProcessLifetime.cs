using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ChooGuard.Persistence
{
    /// <summary>Owned worker teardown, with a Windows kill-on-close job as crash protection.</summary>
    public static class WorkerProcessLifetime
    {
        public static IDisposable Attach(Process process)
        {
            if (process == null) throw new ArgumentNullException(nameof(process));
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
            var job = CreateJobObjectW(IntPtr.Zero, null);
            if (job.IsInvalid) { var error = Marshal.GetLastWin32Error(); job.Dispose(); throw new Win32Exception(error, "WORKER_JOB_CREATE_FAILED"); }
            try
            {
                var limits = new ExtendedLimitInformation { BasicLimitInformation = new BasicLimitInformation { LimitFlags = 0x2000 } }; // KILL_ON_JOB_CLOSE
                if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf(typeof(ExtendedLimitInformation))))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WORKER_JOB_LIMIT_FAILED");
                if (!AssignProcessToJobObject(job, process.Handle))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WORKER_JOB_ASSIGN_FAILED");
                return job;
            }
            catch { job.Dispose(); throw; }
        }
        /// <summary>Only for a broker launched with --owner-stdin, never an externally supplied endpoint.</summary>
        public static bool ShutdownOwnedBroker(Process process, IDisposable lifetime)
        {
            const int graceMilliseconds = 3000, killMilliseconds = 1000;
            bool graceful = false;
            try
            {
                if (process == null) return true;
                var elapsed = Stopwatch.StartNew();
                try
                {
                    if (!process.HasExited)
                    {
                        // A stuck/broken stdin must not block Player teardown before the job/Kill fallback.
                        var send = Task.Run(() =>
                        {
                            try { process.StandardInput.Write("shutdown\n"); process.StandardInput.Flush(); }
                            catch (IOException) { }
                            catch (InvalidOperationException) { }
                        });
                        send.Wait(graceMilliseconds);
                        int remaining = Math.Max(0, graceMilliseconds - (int)elapsed.ElapsedMilliseconds);
                        graceful = process.WaitForExit(remaining);
                    }
                    else graceful = true;
                }
                catch (InvalidOperationException) { graceful = true; } // Process.Start never succeeded.
                catch (Win32Exception) { }
            }
            finally
            {
                // Closing a Windows job kills its children: keep it alive until graceful exit was attempted.
                try { lifetime?.Dispose(); }
                finally
                {
                    if (process != null)
                    {
                        try { if (!process.HasExited) { process.Kill(); process.WaitForExit(killMilliseconds); } }
                        catch (InvalidOperationException) { }
                        catch (Win32Exception) { }
                        finally { process.Dispose(); }
                    }
                }
            }
            return graceful;
        }
        [StructLayout(LayoutKind.Sequential)] private struct BasicLimitInformation
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimitInformation
        {
            public BasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObjectW(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimitInformation info, uint length);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
