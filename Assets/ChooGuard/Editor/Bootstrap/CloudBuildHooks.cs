#if UNITY_CLOUD_BUILD
using System;
using System.IO;
using System.Linq;
using ChooGuard.Persistence;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChooGuard.Editor.Bootstrap
{
    // Invoked by the configured UBA hooks, never by Editor import or initialization.
    public static class CloudBuildHooks
    {
        private const string GeneratedFolder = "Assets/ChooGuard/GeneratedCloudBuild";
        private static DateTime preparedAt;
        private static string entry, gameplay, output;
        private static RuntimePackage package;
        private static BuildReceipt receipt;

        private static void RequireCloud()
        {
            if (Environment.GetEnvironmentVariable("CG_CLOUD_BUILD") != "win-x64" ||
                !string.Equals(Environment.GetEnvironmentVariable("IS_BUILDER"), "true", StringComparison.OrdinalIgnoreCase) ||
                Environment.GetEnvironmentVariable("BUILDER_OS") != "WINDOWS" ||
                UnityEngine.Application.platform != RuntimePlatform.WindowsEditor || !UnityEngine.Application.isBatchMode)
                throw new InvalidOperationException("Explicit Windows UBA configuration is required; no local or cross-host fallback.");
        }

        public static void PreExport()
        {
            RequireCloud();
            if (receipt != null) throw new InvalidOperationException("Cloud export already prepared.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Configure a native Windows x64 build target.");
            gameplay = Environment.GetEnvironmentVariable("CG_GAMEPLAY_SCENE");
            BuildBaseline.ValidateGameplayScene(gameplay);
            package = BuildBaseline.VerifyBuildInputs(BuildTarget.StandaloneWindows64);
            receipt = BuildBaseline.NewReceipt("StandaloneWindows64");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            entry = BuildBaseline.CreateGameplayEntry(gameplay, GeneratedFolder);
            preparedAt = DateTime.UtcNow;
            Debug.Log("CG_CLOUD_PREPARED: gameplay entry and model scene; Player execution NOT_RUN");
        }

        internal static void ValidatePlayerOptions(BuildPlayerOptions options)
        {
            // UBA may also build a test Player. This hook validates only the explicitly prepared product export.
            if (receipt == null || (options.options & BuildOptions.IncludeTestAssemblies) != 0) return;
            RequireCloud();
            if (options.target != BuildTarget.StandaloneWindows64 ||
                (options.options & BuildOptions.Development) == 0 ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x ||
                options.scenes == null || !options.scenes.SequenceEqual(new[] { entry, gameplay }))
                throw new BuildFailedException("UBA must build Development/Mono Windows x64 with the generated entry first and actual model scene second. Check the explicit Scenes override.");
            var rootValue = Environment.GetEnvironmentVariable("CG_CLOUD_OUTPUT_DIRECTORY");
            if (string.IsNullOrWhiteSpace(rootValue) || !Path.IsPathRooted(rootValue) || !Path.IsPathRooted(options.locationPathName))
                throw new BuildFailedException("Native absolute UBA output paths are required.");
            var root = Path.GetFullPath(rootValue).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var project = BuildBaseline.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // UBA owns one target directory directly below the checkout's .build/last directory.
            var isCloudTargetDirectory = string.Equals(Path.GetDirectoryName(root), Path.Combine(project, ".build", "last"),
                StringComparison.OrdinalIgnoreCase);
            output = Path.GetFullPath(options.locationPathName);
            if (string.Equals(root, project, StringComparison.OrdinalIgnoreCase) ||
                (IsWithin(root, project) && !isCloudTargetDirectory) || IsWithin(project, root) ||
                !IsWithin(output, root) || !output.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("UBA output must be an .exe beneath its output directory, either outside the source checkout or in .build/last/<target>.");
            for (var directory = new DirectoryInfo(Path.GetDirectoryName(output)); directory != null; directory = directory.Parent)
                if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new BuildFailedException("UBA output directory chain contains a symbolic link.");
            if (Directory.Exists(Path.GetDirectoryName(output)) && Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(output))
                .Any(path => string.Equals(path, output, StringComparison.OrdinalIgnoreCase)))
                throw new BuildFailedException("UBA Player output already exists; request a clean build.");
        }

        private static bool IsWithin(string child, string parent) => child.StartsWith(
            parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        public static void PostExport(string exportPath)
        {
            RequireCloud();
            if (receipt == null || package == null || string.IsNullOrEmpty(output))
                throw new InvalidOperationException("No validated product export in this Editor process.");
            var packaged = Array.Empty<string>();
            var report = BuildReport.GetLatestReport();
            try
            {
                BuildBaseline.RecordBuildOutcome(receipt, () =>
                {
                    if (report == null) return null;
                    // Unity 6000.3 reports UTC ticks with DateTimeKind.Unspecified.
                    if (report.summary.platform != BuildTarget.StandaloneWindows64 ||
                        !string.Equals(Path.GetFullPath(report.summary.outputPath), output, StringComparison.OrdinalIgnoreCase) ||
                        DateTime.SpecifyKind(report.summary.buildStartedAt, DateTimeKind.Utc) < preparedAt)
                        throw new InvalidOperationException("Latest BuildReport is not this prepared Windows Player export.");
                    var exported = Path.GetFullPath(exportPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!string.Equals(exported, output, StringComparison.OrdinalIgnoreCase) && !IsWithin(output, exported))
                        throw new InvalidOperationException("UBA exportPath does not contain the reported Player.");
                    if (report.summary.result == BuildResult.Succeeded)
                    {
                        package.VerifyAll();
                        packaged = BuildBaseline.CopyRuntimePackage(BuildTarget.StandaloneWindows64, output, package);
                    }
                    return report.summary.result;
                }, () => report.GetFiles().Select(file => file.path).Concat(packaged).ToArray());
            }
            finally
            {
                var saved = BuildBaseline.SaveReceipt(receipt);
                if (File.Exists(output))
                    File.Copy(Path.Combine(BuildBaseline.ProjectRoot, saved.path),
                        Path.Combine(Path.GetDirectoryName(output), "chooguard-build-receipt.json"), false);
                if (AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.DeleteAsset(GeneratedFolder);
                receipt = null;
            }
            Debug.Log("CG_CLOUD_EXPORTED: hash-verified runtime included; Windows Player execution NOT_RUN");
        }
    }

    public sealed class CloudBuildPlayerValidation : BuildPlayerProcessor
    {
        public override int callbackOrder => 0;
        public override void PrepareForBuild(BuildPlayerContext context) => CloudBuildHooks.ValidatePlayerOptions(context.BuildPlayerOptions);
    }
}
#endif
