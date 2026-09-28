using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>
    /// Endurance mode of the standalone player: <c>-soak</c> (optionally <c>-soak-shifts N</c>, <c>-soak-minutes M</c>,
    /// <c>-soak-out &lt;file&gt;</c>) plays N shifts back to back. Each loads the station and a fresh session the way the title
    /// does, plays M minutes while the view moves between the standard viewpoints, then returns to the title. Per shift it
    /// records the errors and exceptions logged, the longest frame (a hang shows as seconds), frame-time p99, and memory after
    /// the shift is unloaded (a leak shows as a line that keeps rising). The report is JSON; the player then quits.
    /// </summary>
    public sealed class SoakRun : MonoBehaviour
    {
        [Serializable] private sealed class Shift { public int index, seed, errors, exceptions, people, hitches; public string firstError; public float loadSeconds, maxFrameMs, p99Ms; public long allocatedAfterBytes, reservedAfterBytes, monoAfterBytes; public string[] longFrames; }
        [Serializable] private sealed class Report { public string device, gpu, started; public int shifts; public float minutesPerShift; public long allocatedAtStartBytes; public Shift[] runs; }

        private int errors, exceptions;
        private string firstError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!Environment.GetCommandLineArgs().Contains("-soak") || FindAnyObjectByType<SoakRun>() != null) return;
            var runner = new GameObject("연속 근무 시험");
            DontDestroyOnLoad(runner);
            runner.AddComponent<SoakRun>();
            AutomatedRunBanner.Attach(runner, "자동 연속 근무 시험 중", "CG_SOAK");
        }

        private static string Argument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        private void OnEnable() => UnityEngine.Application.logMessageReceivedThreaded += OnLog;
        private void OnDisable() => UnityEngine.Application.logMessageReceivedThreaded -= OnLog;

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions++;
            else if (type == LogType.Error || type == LogType.Assert) errors++;
            else return;
            if (firstError == null) firstError = message + " @ " + (stackTrace ?? "").Split('\n').FirstOrDefault();
        }

        private IEnumerator Start()
        {
            int count = int.Parse(Argument("-soak-shifts", "5"));
            float minutes = float.Parse(Argument("-soak-minutes", "6"), System.Globalization.CultureInfo.InvariantCulture);
            string output = Argument("-soak-out", Path.Combine(UnityEngine.Application.persistentDataPath, "soak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"));
            // 기본은 근무마다 다른 시드(여러 사건을 겪게). -seed 를 주면 시드+근무 번호로 다시 돌릴 수 있다.
            int seed = int.Parse(Argument("-seed", "0"));
            var report = new Report
            {
                device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName, started = DateTime.Now.ToString("s"), shifts = count, minutesPerShift = minutes,
                allocatedAtStartBytes = Profiler.GetTotalAllocatedMemoryLong(),
            };
            var runs = new List<Shift>();
            for (int i = 0; i < count; i++)
            {
                errors = exceptions = 0;
                firstError = null;
                var run = new Shift { index = i + 1 };
                if (seed != 0) EmergencySession.NextSeed = seed + i;
                float loadStart = Time.realtimeSinceStartup;
                var station = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
                while (!station.isDone) yield return null;
                yield return ShaderWarmup.Run(null);
                var session = SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
                while (!session.isDone) yield return null;
                float giveUp = Time.realtimeSinceStartup + 90;
                while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null || EmergencySession.Current.Crowd.People.Count == 0) && Time.realtimeSinceStartup < giveUp) yield return null;
                run.loadSeconds = Time.realtimeSinceStartup - loadStart;
                var shift = EmergencySession.Current;
                var frames = new List<float>();
                var longFrames = new List<string>();
                if (shift != null)
                {
                    run.seed = shift.World != null ? shift.World.Seed : 0;
                    float start = Time.realtimeSinceStartup, end = start + minutes * 60;
                    int view = -1;
                    float nextView = 0;
                    while (Time.realtimeSinceStartup < end && shift != null)
                    {
                        if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
                        if (shift.Player.IsPaused) shift.Player.Resume(false);
                        if (Time.realtimeSinceStartup > nextView)
                        {
                            // 20초마다 다음 표준 시점으로 옮겨 역 전체를 번갈아 본다.
                            view = (view + 1) % BenchmarkRun.Views.Length;
                            var v = BenchmarkRun.Views[view];
                            shift.Player.RestorePhysicalPose(v.At, v.Yaw, v.Pitch);
                            nextView = Time.realtimeSinceStartup + 20;
                        }
                        yield return null;
                        float ms = Time.unscaledDeltaTime * 1000f;
                        frames.Add(ms);
                        // 100 ms 넘는 프레임은 언제·어느 시점에서였는지 남긴다(멈춤 원인 찾기).
                        if (ms > 100)
                        {
                            run.hitches++;
                            if (longFrames.Count < 50) longFrames.Add((Time.realtimeSinceStartup - start).ToString("F1") + "s " + BenchmarkRun.Views[view].Name + " " + ms.ToString("F0") + "ms");
                        }
                    }
                    run.people = shift != null ? shift.Crowd.People.Count : 0;
                }
                else run.firstError = "근무를 시작하지 못함";
                SceneManager.LoadScene(SceneFlow.TitleScene, LoadSceneMode.Single);
                yield return null;
                yield return null;
                yield return Resources.UnloadUnusedAssets();
                GC.Collect();
                yield return null;
                run.errors = errors;
                run.exceptions = exceptions;
                run.firstError = run.firstError ?? firstError;
                run.maxFrameMs = frames.Count > 0 ? frames.Max() : 0;
                run.longFrames = longFrames.ToArray();
                run.p99Ms = frames.Count > 0 ? frames.OrderBy(f => f).ElementAt(Mathf.Clamp(Mathf.CeilToInt(.99f * frames.Count) - 1, 0, frames.Count - 1)) : 0;
                run.allocatedAfterBytes = Profiler.GetTotalAllocatedMemoryLong();
                run.reservedAfterBytes = Profiler.GetTotalReservedMemoryLong();
                run.monoAfterBytes = Profiler.GetMonoUsedSizeLong();
                runs.Add(run);
                Debug.Log("CG_SOAK shift " + run.index + " seed=" + run.seed + " errors=" + run.errors + " exceptions=" + run.exceptions + " max=" + run.maxFrameMs.ToString("F0") + "ms hitches=" + run.hitches + " allocated=" + (run.allocatedAfterBytes >> 20) + "MB");
            }
            report.runs = runs.ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            UnityEngine.Application.Quit(0);
        }
    }
}
