using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>
    /// Benchmark mode of the standalone player. Launched with <c>-benchmark</c> (optionally <c>-benchmark-out &lt;file&gt;</c>,
    /// <c>-benchmark-seconds &lt;per view&gt;</c>, <c>-benchmark-settle &lt;seconds&gt;</c>, <c>-benchmark-profile &lt;dir&gt;</c>), it
    /// loads the station and a shift the way the title's start does, lets the shift run until the crowd has spread and the KTX
    /// is in, then stands at each of the eleven standard viewpoints looking slowly left and right, and records every frame: its
    /// time, the CPU main/render and GPU split (FrameTimingManager; main-thread work = frame minus the wait for present), the
    /// render counters (batches, SetPass calls, triangles, shadow casters, skinned meshes) and memory. It writes a JSON report
    /// (per viewpoint and overall) and quits. VSync is off so a frame's time is its cost, not the display's pace.
    /// </summary>
    public sealed class BenchmarkRun : MonoBehaviour
    {
        internal readonly struct View
        {
            public readonly string Name;
            public readonly Vector3 At;
            public readonly float Yaw, Pitch;
            public View(string name, float x, float y, float z, float yaw, float pitch) { Name = name; At = new Vector3(x, y, z); Yaw = yaw; Pitch = pitch; }
        }

        // .planning/2026-09-27-production-pass/standard-views.json 과 같은 열한 시점(화면 단계의 전후 비교에 쓴 곳). SoakRun 도 쓴다.
        internal static readonly View[] Views =
        {
            new View("hall-seats", 74.1f, 7.06f, 16.5f, 270, 2), new View("hall-north", 60, 7.1f, -10, 20, 2), new View("hall-south", 70, 7.1f, 30, 200, 2),
            new View("main2f", 12, 7.06f, 13, 0, 2), new View("upper3f", 50, 12.2f, -5, 200, 2), new View("ground1f", -35, .04f, -60, 315, 2),
            new View("platform56", 76, 0, 52, 200, 2), new View("northdeck", 65, 7, 64, 10, 2), new View("plaza", -110, .8f, 60, 110, 3),
            new View("eastexit", 104, 7.06f, 0, 250, 2), new View("southgate", 48, 7.2f, -36, 200, 2),
        };

        [Serializable] private sealed class Stats
        {
            public string view;
            public int frames;
            public float p50, p95, p99, max, mean, cpuMainP95, cpuMainWorkP95, cpuRenderP95, gpuP95;
            public float batches, setPassCalls, drawCalls, trianglesK, shadowCasters, skinnedMeshes;
        }

        /// <summary>Render statistics counters (available in release players); per-frame means over one view.</summary>
        private sealed class Counters : IDisposable
        {
            private static readonly string[] Names = { "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Shadow Casters Count", "Visible Skinned Meshes Count" };
            private readonly ProfilerRecorder[] recorders = Names.Select(n => ProfilerRecorder.StartNew(ProfilerCategory.Render, n)).ToArray();
            private readonly double[] sums = new double[Names.Length];
            private int samples;

            public void Sample()
            {
                for (int i = 0; i < recorders.Length; i++) if (recorders[i].Valid) sums[i] += recorders[i].LastValue;
                samples++;
            }

            public void Fill(Stats stats)
            {
                float Mean(int i) => samples == 0 ? 0 : (float)(sums[i] / samples);
                stats.batches = Mean(0); stats.setPassCalls = Mean(1); stats.drawCalls = Mean(2); stats.trianglesK = Mean(3) / 1000f; stats.shadowCasters = Mean(4); stats.skinnedMeshes = Mean(5);
            }

            public void Dispose() { foreach (var recorder in recorders) recorder.Dispose(); }
        }
        [Serializable] private sealed class Report
        {
            public string build, device, gpu, os, started;
            public int width, height, quality, people, seed;
            public string jev;
            public float settleSeconds, secondsPerView;
            public Stats overall;
            public Stats[] views;
            public long allocatedBytes, reservedBytes, monoUsedBytes, graphicsDriverBytes;
            public bool frameTimingSupported;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!Environment.GetCommandLineArgs().Contains("-benchmark") || FindAnyObjectByType<BenchmarkRun>() != null) return;
            var runner = new GameObject("벤치마크");
            DontDestroyOnLoad(runner);
            runner.AddComponent<BenchmarkRun>();
            AutomatedRunBanner.Attach(runner, "자동 성능 측정 중", "CG_BENCHMARK");
        }

        private static string Argument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        private IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = -1;
            string output = Argument("-benchmark-out", Path.Combine(UnityEngine.Application.persistentDataPath, "benchmark-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"));
            float perView = float.Parse(Argument("-benchmark-seconds", "12"), System.Globalization.CultureInfo.InvariantCulture);
            float settle = float.Parse(Argument("-benchmark-settle", "75"), System.Globalization.CultureInfo.InvariantCulture);
            var started = DateTime.Now;
            // 같은 시드면 같은 근무(승객·사건)라 설정끼리 비교할 수 있다. JEV 를 끄려면 TYPESAFE_API_KEY=off 로 실행한다.
            int seed = int.Parse(Argument("-seed", "20260927"));
            EmergencySession.NextSeed = seed;
            if (SceneManager.GetActiveScene().name != SceneFlow.StationScene)
            {
                var station = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
                while (!station.isDone) yield return null;
                yield return ShaderWarmup.Run(null);
                var session = SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
                while (!session.isDone) yield return null;
            }
            float giveUp = Time.realtimeSinceStartup + 90;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null || EmergencySession.Current.Crowd.People.Count == 0) && Time.realtimeSinceStartup < giveUp) yield return null;
            var shift = EmergencySession.Current;
            if (shift == null) { Debug.LogError("CG_BENCHMARK 근무를 시작하지 못함"); UnityEngine.Application.Quit(2); yield break; }
            Resume(shift);
            // 사람들이 흩어지고 KTX 가 들어오도록 근무를 먼저 흘려보낸다.
            float until = Time.realtimeSinceStartup + settle;
            while (Time.realtimeSinceStartup < until) { Resume(shift); yield return null; }

            var overall = new Samples();
            var overallCounters = new Counters();
            var perViewStats = new List<Stats>();
            var timings = new FrameTiming[1];
            string profileDirectory = Argument("-benchmark-profile", null);
            foreach (var view in Views)
            {
                shift.Player.RestorePhysicalPose(view.At, view.Yaw, view.Pitch);
                // 새 시점의 첫 프레임들(스트리밍·셰이더 준비)은 따로 둔다.
                float warm = Time.realtimeSinceStartup + 1.5f;
                while (Time.realtimeSinceStartup < warm) { Resume(shift); yield return null; }
                var samples = new Samples();
                var counters = new Counters();
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < perView)
                {
                    Resume(shift);
                    float t = (Time.realtimeSinceStartup - t0) / perView;
                    // 한 시점에서 좌우 ±35°를 천천히 한 번 훑는다.
                    shift.Player.RestorePhysicalPose(view.At, view.Yaw + 35f * Mathf.Sin(t * Mathf.PI * 2), view.Pitch);
                    yield return null;
                    FrameTimingManager.CaptureFrameTimings();
                    samples.Add(Time.unscaledDeltaTime * 1000f, FrameTimingManager.GetLatestTimings(1, timings) > 0 ? timings[0] : (FrameTiming?)null);
                    counters.Sample();
                    overallCounters.Sample();
                }
                var stats = samples.Summarize(view.Name);
                counters.Fill(stats);
                counters.Dispose();
                perViewStats.Add(stats);
                overall.AddRange(samples);
                if (profileDirectory != null) yield return Profile(shift, profileDirectory, view.Name);
            }

            var total = overall.Summarize("overall");
            overallCounters.Fill(total);
            overallCounters.Dispose();
            var report = new Report
            {
                build = UnityEngine.Application.version + (Debug.isDebugBuild ? " (development)" : ""),
                device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType, os = SystemInfo.operatingSystem,
                started = started.ToString("s"), width = Screen.width, height = Screen.height, quality = QualitySettings.GetQualityLevel(),
                people = shift.Crowd.People.Count, seed = shift.World.Seed, jev = shift.Jev != null ? shift.Jev.Status : "", settleSeconds = settle, secondsPerView = perView,
                overall = total, views = perViewStats.ToArray(),
                allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), reservedBytes = Profiler.GetTotalReservedMemoryLong(),
                monoUsedBytes = Profiler.GetMonoUsedSizeLong(), graphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver(),
                frameTimingSupported = overall.HasGpu,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("CG_BENCHMARK p50=" + total.p50.ToString("F1") + " p95=" + total.p95.ToString("F1") + " → " + output);
            UnityEngine.Application.Quit(0);
        }

        private static void Resume(EmergencySession shift)
        {
            if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
        }

        /// <summary>
        /// Development players only: records the 90 frames after a view's measured frames as a binary profiler log in
        /// <paramref name="directory"/> (named after the view; open it in the Profiler window), so the profiler's own cost
        /// never enters the statistics.
        /// </summary>
        private static IEnumerator Profile(EmergencySession shift, string directory, string view)
        {
            if (!Profiler.supported) yield break;
            Directory.CreateDirectory(directory);
            Profiler.logFile = Path.Combine(directory, view);
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
            for (int i = 0; i < 90; i++) { Resume(shift); yield return null; }
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = "";
        }

        private sealed class Samples
        {
            private readonly List<float> frame = new List<float>(), main = new List<float>(), work = new List<float>(), render = new List<float>(), gpu = new List<float>();

            public bool HasGpu => gpu.Count > 0;

            public void Add(float frameMs, FrameTiming? timing)
            {
                frame.Add(frameMs);
                if (!(timing is FrameTiming t)) return;
                main.Add((float)t.cpuMainThreadFrameTime);
                // 표시(present)를 기다린 시간을 뺀 것이 메인 스레드가 실제로 일한 시간이다.
                work.Add((float)(t.cpuMainThreadFrameTime - t.cpuMainThreadPresentWaitTime));
                render.Add((float)t.cpuRenderThreadFrameTime);
                // GPU 시간을 못 받은 프레임은 0 으로 오므로 뺀다.
                if (t.gpuFrameTime > 0) gpu.Add((float)t.gpuFrameTime);
            }

            public void AddRange(Samples other)
            {
                frame.AddRange(other.frame); main.AddRange(other.main); work.AddRange(other.work); render.AddRange(other.render); gpu.AddRange(other.gpu);
            }

            public Stats Summarize(string name) => new Stats
            {
                view = name, frames = frame.Count, p50 = Percentile(frame, .5f), p95 = Percentile(frame, .95f), p99 = Percentile(frame, .99f),
                max = frame.Count > 0 ? frame.Max() : 0, mean = frame.Count > 0 ? frame.Average() : 0,
                cpuMainP95 = Percentile(main, .95f), cpuMainWorkP95 = Percentile(work, .95f), cpuRenderP95 = Percentile(render, .95f), gpuP95 = Percentile(gpu, .95f),
            };
        }

        private static float Percentile(List<float> values, float p)
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)];
        }
    }
}
