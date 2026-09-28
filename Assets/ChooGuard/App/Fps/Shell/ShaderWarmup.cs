using System;
using System.Collections;
using System.IO;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>
    /// Pipeline-state (PSO) warm-up. The first time a shader variant is drawn with a given render state, Metal compiles a
    /// pipeline and that frame stalls; on a fresh install a whole first shift stutters (benchmark, first launch of a new
    /// build: p95 55 ms, second launch 38 ms). A collection of the states the station uses, traced in a development player,
    /// lives in Resources and is compiled behind the loading screen. The driver keeps compiled pipelines on disk, so later
    /// launches warm up in moments.
    /// <para>Tracing: a development player started with <c>-trace-pso &lt;file&gt;</c> records every state it draws
    /// (appending to the file when it exists) and saves on quit; copy the file to
    /// <c>Assets/ChooGuard/Settings/Resources/ShaderWarmup/station-metal.graphicsstate</c>.</para>
    /// </summary>
    public static class ShaderWarmup
    {
        public const string ResourcePath = "ShaderWarmup/station-metal";
        private const int StatesPerFrame = 24;

        private static GraphicsStateCollection trace;
        private static string tracePath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeginTraceIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-trace-pso");
            if (index < 0 || index + 1 >= args.Length) return;
            if (!Debug.isDebugBuild) { Debug.LogError("CG_PSO 추적은 개발 빌드에서만 됩니다."); return; }
            tracePath = Path.GetFullPath(args[index + 1]);
            trace = File.Exists(tracePath) ? new GraphicsStateCollection(tracePath) : new GraphicsStateCollection();
            trace.BeginTrace();
            UnityEngine.Application.quitting += EndTrace;
            Debug.Log("CG_PSO trace started (" + trace.totalGraphicsStateCount + " states already) → " + tracePath);
        }

        private static void EndTrace()
        {
            trace.EndTrace();
            Directory.CreateDirectory(Path.GetDirectoryName(tracePath));
            bool saved = trace.SaveToFile(tracePath);
            Debug.Log("CG_PSO trace saved=" + saved + " variants=" + trace.variantCount + " states=" + trace.totalGraphicsStateCount + " → " + tracePath);
        }

        /// <summary>
        /// Compiles the traced states a few per frame, reporting progress 0..1. Does nothing (and reports 1) when there is no
        /// collection for this platform and graphics API, or while tracing (a trace must see the real first use).
        /// </summary>
        public static IEnumerator Run(Action<float> progress)
        {
            var collection = trace == null ? Resources.Load<GraphicsStateCollection>(ResourcePath) : null;
            if (collection == null || collection.isWarmedUp || collection.runtimePlatform != UnityEngine.Application.platform || collection.graphicsDeviceType != SystemInfo.graphicsDeviceType)
            {
                progress?.Invoke(1);
                yield break;
            }
            float started = Time.realtimeSinceStartup, lastAdvance = started;
            int total = Mathf.Max(1, collection.totalGraphicsStateCount), done = 0;
            var handle = default(JobHandle);
            while (!collection.isWarmedUp)
            {
                handle = collection.WarmUpProgressively(StatesPerFrame, handle);
                if (collection.completedWarmupCount != done) { done = collection.completedWarmupCount; lastAdvance = Time.realtimeSinceStartup; }
                // 10초 동안 하나도 더 준비되지 않으면 멈춘다(남은 것은 처음 쓰일 때 만들어진다). 불러오기 화면이 끝나지 않는 일은 없게 한다.
                else if (Time.realtimeSinceStartup - lastAdvance > 10) { Debug.LogWarning("CG_PSO warm-up stalled at " + done + "/" + total); break; }
                progress?.Invoke(Mathf.Clamp01((float)done / total));
                yield return null;
            }
            handle.Complete();
            progress?.Invoke(1);
            Debug.Log("CG_PSO warmed " + collection.completedWarmupCount + "/" + collection.totalGraphicsStateCount + " states in " + (Time.realtimeSinceStartup - started).ToString("F1") + " s");
        }
    }
}
