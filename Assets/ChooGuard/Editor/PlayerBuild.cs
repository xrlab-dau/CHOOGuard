using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Standalone macOS player (Apple silicon) of the build scene list — title, station, emergency session — for play and for
    /// the benchmark mode (<c>-benchmark</c>, see ChooGuard.App.Fps.Shell.BenchmarkRun). Frame timing stats are on so the
    /// benchmark can split each frame into CPU main/render thread and GPU time. The development variant goes to its own folder
    /// and is only for profiler captures (<c>-benchmark-profile</c>); measure with the release player.
    /// </summary>
    public static class PlayerBuild
    {
        public const string MacOutput = "Builds/macOS/CHOOGuard.app";
        public const string MacDevelopmentOutput = "Builds/macOS-development/CHOOGuard.app";

        [MenuItem("ChooGuard/Build/macOS player")]
        public static void BuildMac() => Build(MacOutput, BuildOptions.None);

        [MenuItem("ChooGuard/Build/macOS player (development, profiler)")]
        public static void BuildMacDevelopment() => Build(MacDevelopmentOutput, BuildOptions.Development);

        private static void Build(string output, BuildOptions options)
        {
            PlayerSettings.enableFrameTimingStats = true;
            EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "ARM64");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.StandaloneOSX,
                locationPathName = output,
                options = options,
            });
            var summary = report.summary;
            Debug.Log("CG_PLAYER_BUILD result=" + summary.result + " size=" + (summary.totalSize / 1048576) + "MB time=" + summary.totalTime.TotalMinutes.ToString("F1") + "min errors=" + summary.totalErrors + " → " + output);
            if (summary.result != BuildResult.Succeeded)
                foreach (var step in report.steps)
                    foreach (var message in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                        Debug.LogError("CG_PLAYER_BUILD_ERROR " + step.name + ": " + message.content);
        }
    }
}
