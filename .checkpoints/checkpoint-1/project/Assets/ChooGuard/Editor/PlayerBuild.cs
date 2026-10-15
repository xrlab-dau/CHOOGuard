using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Standalone desktop players of the build scene list — title, station, emergency session — for macOS (Apple silicon),
    /// Windows (x64) and Linux (x64), for play, the benchmark mode (<c>-benchmark</c>, see ChooGuard.App.Fps.Shell.BenchmarkRun)
    /// and the soak mode (<c>-soak</c>, SoakRun). Frame timing stats are on so the benchmark can split each frame into CPU
    /// main/render thread and GPU time. The development variant goes to its own folder and is only for profiler captures
    /// (<c>-benchmark-profile</c>); measure with the release player. All three use the Mono backend, so one editor with the
    /// Windows and Linux Mono build modules builds every player (CI does this in one licensed session: <see cref="BuildAll"/>).
    /// </summary>
    public static class PlayerBuild
    {
        public const string MacOutput = "Builds/macOS/CHOOGuard.app";
        public const string MacDevelopmentOutput = "Builds/macOS-development/CHOOGuard.app";
        public const string WindowsOutput = "Builds/Windows/CHOOGuard.exe";
        public const string LinuxOutput = "Builds/Linux/CHOOGuard.x86_64";

        [MenuItem("ChooGuard/Build/macOS player")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, MacOutput, BuildOptions.None);

        [MenuItem("ChooGuard/Build/macOS player (development, profiler)")]
        public static void BuildMacDevelopment() => Build(BuildTarget.StandaloneOSX, MacDevelopmentOutput, BuildOptions.Development);

        [MenuItem("ChooGuard/Build/Windows player")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, WindowsOutput, BuildOptions.None);

        [MenuItem("ChooGuard/Build/Linux player")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, LinuxOutput, BuildOptions.None);

        /// <summary>Every desktop player from one editor session: one licence activation and one import in CI.</summary>
        public static void BuildAll()
        {
            BuildMac();
            BuildWindows();
            BuildLinux();
        }

        /// <summary>
        /// Compiles the scripts the macOS release player contains — without the Editor-only assemblies, with the player's API
        /// surface (so an Editor-only member such as <c>Light.lightmapBakeType</c> fails here as it does in a build) — and no
        /// scenes, shaders or player: about a minute instead of a full build. Every compiler error is logged as
        /// <c>CG_PLAYER_COMPILE_ERROR</c>; the editor exits with code 1 when there is one (run it in batch mode:
        /// <c>unity-batch.sh &lt;clone&gt; method ChooGuard.Editor.PlayerBuild.CompilePlayerScriptsMac</c>). The output stays under
        /// Library/, never Assets/.
        /// </summary>
        public static void CompilePlayerScriptsMac() => CompilePlayerScripts(BuildTarget.StandaloneOSX);

        private static void CompilePlayerScripts(BuildTarget target)
        {
            var errors = new List<string>();
            void Capture(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception) errors.Add(condition);
            }
            var output = Path.Combine("Library", "PlayerScriptCompile", target.ToString());
            Directory.CreateDirectory(output);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ScriptCompilationResult result;
            Application.logMessageReceived += Capture;
            try
            {
                result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings { group = BuildPipeline.GetBuildTargetGroup(target), target = target, options = ScriptCompilationOptions.None }, output);
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }
            int assemblies = result.assemblies?.Count() ?? 0;
            bool passed = errors.Count == 0 && assemblies > 0;
            Debug.Log("CG_PLAYER_COMPILE target=" + target + " result=" + (passed ? "Passed" : "Failed") + " assemblies=" + assemblies + " errors=" + errors.Count + " seconds=" + watch.Elapsed.TotalSeconds.ToString("F0"));
            foreach (var error in errors.Distinct())
                Debug.LogError("CG_PLAYER_COMPILE_ERROR " + error);
            if (!passed) EditorApplication.Exit(1);
        }

        private static void Build(BuildTarget target, string output, BuildOptions options)
        {
            PlayerSettings.enableFrameTimingStats = true;
            if (target == BuildTarget.StandaloneOSX)
                EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "ARM64");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = target,
                locationPathName = output,
                options = options,
            });
            var summary = report.summary;
            Debug.Log("CG_PLAYER_BUILD target=" + target + " result=" + summary.result + " size=" + (summary.totalSize / 1048576) + "MB time=" + summary.totalTime.TotalMinutes.ToString("F1") + "min errors=" + summary.totalErrors + " → " + output);
            if (summary.result != BuildResult.Succeeded)
                foreach (var step in report.steps)
                    foreach (var message in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                        Debug.LogError("CG_PLAYER_BUILD_ERROR " + step.name + ": " + message.content);
        }
    }
}
