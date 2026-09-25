using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ChooGuard.App.Fps.Runtime;
using ChooGuard.Persistence;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Bootstrap
{
    [Serializable] public sealed class FileHash { public string path, sha256; }
    [Serializable] public sealed class HostInfo { public string name, version, architecture; }
    [Serializable] public sealed class PipelineInfo { public string name = "URP", packageVersion, assetPath, assetSha256; }
    [Serializable] public sealed class BackendInfo { public string target = "StandaloneWindows64", architecture = "x86_64", scriptingBackend = "Mono"; public bool development = true; }
    [Serializable] public sealed class BuildReceipt
    {
        public int schemaVersion = 1;
        public string storyId = "CS-BOOT.01.01", runId, sourceTreeDigest, editorVersion, packageLockSha256;
        public HostInfo host;
        public string requestedTarget, buildStatus = "NOT_RUN", runStatus = "NOT_RUN", reason;
        public FileHash[] outputs = Array.Empty<FileHash>();
    }
    [Serializable] public sealed class BaselineDocument
    {
        public string editorVersion, packageLockSha256;
        public PipelineInfo renderPipeline;
        public BackendInfo backend = new BackendInfo();
        public HostInfo os;
        public FileHash[] nativePlugins = Array.Empty<FileHash>();
        public FileHash buildReceiptRef;
    }

    public static class BuildBaseline
    {
        public static string ProjectRoot => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
        public static string HashFile(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var indexes = args.Select((x, i) => new { x, i }).Where(x => x.x == name).ToArray();
            if (indexes.Length == 0) return null;
            if (indexes.Length != 1 || indexes[0].i + 1 >= args.Length) throw new ArgumentException("Invalid argument " + name);
            return args[indexes[0].i + 1];
        }
        public static string SourceTreeDigest()
        {
            var root = ProjectRoot;
            var files = new[] { "Assets", "Packages", "ProjectSettings" }.SelectMany(folder => Directory.GetFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories))
                .Concat(new[] { "workers", "workers/physics", "workers/prediction" }.SelectMany(folder => Directory.GetFiles(Path.Combine(root, folder), "*", SearchOption.TopDirectoryOnly)).Where(IsWorkerSource))
                .Concat(new[] { Path.Combine(root, "workers/runtime/runtime-manifest.json") }.Where(File.Exists))
                .Concat(new[] { Path.Combine(root, "docs/build/baseline.schema.json") }).Select(path => new { path, relative = path.Substring(root.Length + 1).Replace('\\', '/') }).OrderBy(x => x.relative, StringComparer.Ordinal);
            var text = string.Concat(files.Select(x => x.relative + "\0" + HashFile(x.path) + "\n"));
            using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        private static bool IsWorkerSource(string path)
        {
            var extension = Path.GetExtension(path);
            return extension == ".py" || extension == ".mjs" || extension == ".json" || extension == ".txt" || extension == ".sh";
        }
        private static HostInfo Host() => new HostInfo { name = UnityEngine.Application.platform.ToString(), version = SystemInfo.operatingSystem, architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString() };
        internal static BuildReceipt NewReceipt(string target) => new BuildReceipt
        {
            runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6),
            sourceTreeDigest = SourceTreeDigest(), editorVersion = UnityEngine.Application.unityVersion,
            packageLockSha256 = HashFile(Path.Combine(ProjectRoot, "Packages/packages-lock.json")), host = Host(), requestedTarget = target
        };
        internal static FileHash SaveReceipt(BuildReceipt receipt)
        {
            var relative = "docs/build/evidence/CS-BOOT.01.01/" + receipt.runId + "/build-receipt.json";
            var path = Path.Combine(ProjectRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(JsonUtility.ToJson(receipt, true) + "\n");
            return new FileHash { path = relative, sha256 = HashFile(path) };
        }
        public static void Write()
        {
            var report = BootstrapValidator.Validate(BootstrapValidator.Capture(ProjectRoot));
            if (!report.IsValid) throw new InvalidOperationException(string.Join(",", report.Errors));
            var receipt = NewReceipt("StandaloneWindows64");
            receipt.reason = "Windows build and run not executed. Host=" + UnityEngine.Application.platform + "; Windows module supported=" + BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) + ". Local static validation is not Player acceptance.";
            var document = new BaselineDocument
            {
                editorVersion = receipt.editorVersion, packageLockSha256 = receipt.packageLockSha256, os = receipt.host,
                renderPipeline = new PipelineInfo { packageVersion = PackageInfo.GetAllRegisteredPackages().Single(p => p.name == "com.unity.render-pipelines.universal").version, assetPath = BootstrapProject.PipelinePath, assetSha256 = HashFile(Path.Combine(ProjectRoot, BootstrapProject.PipelinePath)) },
                buildReceiptRef = SaveReceipt(receipt)
            };
            File.WriteAllText(Path.Combine(ProjectRoot, "docs/build/baseline.json"), JsonUtility.ToJson(document, true) + "\n", new UTF8Encoding(false));
            Debug.Log("CG_BOOT_BASELINE: Windows NOT_RUN; receipt=" + document.buildReceiptRef.path);
        }
        private sealed class BuildRequest
        {
            public BuildTarget Target;
            public string Output;
            public string GameplayScene;
        }
        private static string RequiredBuildArgument(string[] args, string name)
        {
            var indexes = args.Select((value, index) => new { value, index }).Where(x => x.value == name).ToArray();
            if (indexes.Length != 1 || indexes[0].index + 1 >= args.Length)
                throw new ArgumentException("Exactly one value required for " + name);
            var value = args[indexes[0].index + 1];
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException("Missing value for " + name);
            return value;
        }
        private static string AbsolutePath(string value)
        {
            if (!Path.IsPathRooted(value)) throw new ArgumentException("Absolute build paths required");
            var full = Path.GetFullPath(value);
            var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return trimmed.Length == 0 ? Path.GetPathRoot(full) : trimmed;
        }
        // Conservative on case-insensitive macOS volumes; false-positive rejection is intentional.
        private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private static bool Within(string child, string parent) => child.StartsWith(
            parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
        private static void RequireDirectoryChain(string path)
        {
            for (var directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
            {
                // GetAttributes throws on missing/unreadable paths; no permissive fallback.
                var attributes = File.GetAttributes(directory.FullName);
                if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0)
                    throw new IOException("Build directory must exist without symbolic links: " + directory.FullName);
            }
        }
        private static BuildRequest ValidateBuildArguments(string[] args)
        {
            var targetName = RequiredBuildArgument(args, "-cgBuildTarget");
            var root = AbsolutePath(RequiredBuildArgument(args, "-cgBuildRoot"));
            var output = AbsolutePath(RequiredBuildArgument(args, "-cgBuildOutput"));
            if (targetName != "StandaloneOSX" && targetName != "StandaloneWindows64")
                throw new ArgumentException("Unsupported explicit build target");
            var gameplay = RequiredBuildArgument(args, "-cgGameplayScene").Replace('\\', '/');
            ValidateGameplayScene(gameplay);
            var project = AbsolutePath(ProjectRoot);
            if (SamePath(root, project) || Within(root, project) || Within(project, root))
                throw new IOException("Build root must not overlap the project");
            if (SamePath(output, root) || !Within(output, root))
                throw new IOException("Build output must be a strict descendant of the explicit build root");
            RequireDirectoryChain(root);
            var parent = Path.GetDirectoryName(output);
            RequireDirectoryChain(parent);
            // Enumeration detects every directory entry, including dangling links, without following the leaf.
            if (Directory.EnumerateFileSystemEntries(parent).Any(entry => SamePath(entry, output)))
                throw new IOException("Build output entry already exists");
            return new BuildRequest { Target = (BuildTarget)Enum.Parse(typeof(BuildTarget), targetName), Output = output, GameplayScene = gameplay };
        }
        public static void Build() => BuildFromArguments(Environment.GetCommandLineArgs());
        private static void BuildFromArguments(string[] args)
        {
            // Boundary failures produce neither a receipt nor a build output. This preflight is not a
            // filesystem race lock; the caller must keep its scratch directory under its own control.
            var request = ValidateBuildArguments(args);
            var supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, request.Target);
            var windowsHost = UnityEngine.Application.platform == RuntimePlatform.WindowsEditor;
            var receipt = NewReceipt(request.Target.ToString());
            if (!supported || (request.Target == BuildTarget.StandaloneWindows64 && !windowsHost))
            {
                receipt.reason = "Module supported=" + supported + "; host=" + UnityEngine.Application.platform +
                    "; Windows host available=" + windowsHost + "; no validator, build, target fallback or installation.";
                var reference = SaveReceipt(receipt);
                Debug.Log("CG_BOOT_BUILD_NOT_RUN: " + reference.path);
                return;
            }
            BuildReport built = null;
            var packagedFiles = Array.Empty<string>();
            try
            {
                RecordBuildOutcome(receipt, () =>
                {
                    var package = VerifyBuildInputs(request.Target);
                    built = BuildGameplayPlayer(request);
                    if (built != null && built.summary.result == BuildResult.Succeeded) packagedFiles = CopyRuntimePackage(request.Target, request.Output, package);
                    return built == null ? (BuildResult?)null : built.summary.result;
                }, () => built.GetFiles().Select(f => f.path).Concat(packagedFiles).ToArray());
            }
            catch
            {
                SaveReceipt(receipt);
                throw;
            }
            var saved = SaveReceipt(receipt);
            Debug.Log("CG_BOOT_BUILD_SUCCEEDED: " + request.Output + "; receipt=" + saved.path);
        }
        internal static void ValidateGameplayScene(string gameplay)
        {
            if (string.IsNullOrEmpty(gameplay) || gameplay == BootstrapValidator.ScenePath ||
                !gameplay.StartsWith("Assets/", StringComparison.Ordinal) || !gameplay.EndsWith(".unity", StringComparison.Ordinal) ||
                gameplay.Split('/').Any(segment => segment == ".." || segment == ".") ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(gameplay) == null)
                throw new ArgumentException("An existing actual model scene asset is required; a Bootstrap-only or laboratory build is not product acceptance.");
        }
        internal static RuntimePackage VerifyBuildInputs(BuildTarget target)
        {
            var validation = BootstrapValidator.Validate(BootstrapValidator.Capture(ProjectRoot));
            if (!validation.IsValid) throw new InvalidOperationException(string.Join(",", validation.Errors));
            var platform = target == BuildTarget.StandaloneWindows64 ? "win-x64" :
                (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "osx-arm64" : "osx-x64");
            var package = RuntimePackage.Load(Path.Combine(ProjectRoot, "workers/runtime"), platform);
            package.VerifyAll();
            package.RequireBroker();
            return package;
        }
        // Called only by explicit builds; existing model scenes and their save state are not rewritten.
        internal static string CreateGameplayEntry(string gameplayScene, string folder)
        {
            ValidateGameplayScene(gameplayScene);
            if (Directory.Exists(Path.Combine(ProjectRoot, folder)))
                throw new IOException("Generated entry folder already exists: " + folder);
            var entry = folder + "/ChooGuardGameplay.unity";
            var previous = SceneManager.GetActiveScene();
            var emptyBatchStartup = UnityEngine.Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(previous.path) && !previous.isDirty && previous.rootCount == 0;
            var generated = default(Scene);
            try
            {
                Directory.CreateDirectory(Path.Combine(ProjectRoot, folder));
                AssetDatabase.Refresh();
                generated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptyBatchStartup ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(generated);
                var root = new GameObject("ChooGuard Gameplay Runtime");
                var bootstrap = root.AddComponent<GameplayBootstrap>();
                bootstrap.WorldSceneName = Path.GetFileNameWithoutExtension(gameplayScene);
                bootstrap.SeedJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Gameplay/practice-seed.json");
                bootstrap.TransitionJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ChooGuard/Art/Gameplay/atomic-transitions.json");
                bootstrap.KoreanFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset");
                if (bootstrap.KoreanFont == null) throw new FileNotFoundException("A packaged Korean font is required for the gameplay entry.");
                if (bootstrap.SeedJson == null || bootstrap.TransitionJson == null)
                    throw new FileNotFoundException("Gameplay seed/atomic-transition TextAssets are required for the native build entry.");
                if (!EditorSceneManager.SaveScene(generated, entry)) throw new IOException("Failed to save the generated gameplay entry scene.");
                return entry;
            }
            finally
            {
                if (emptyBatchStartup)
                {
                    if (generated.IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                else
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    if (generated.IsValid()) EditorSceneManager.CloseScene(generated, true);
                }
            }
        }
        private static BuildReport BuildGameplayPlayer(BuildRequest request)
        {
            var folder = "Assets/ChooGuard/GeneratedBuild-" + Guid.NewGuid().ToString("N");
            try
            {
                var entry = CreateGameplayEntry(request.GameplayScene, folder);
                return BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { entry, request.GameplayScene }, target = request.Target,
                    locationPathName = request.Output, options = BuildOptions.Development
                });
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            }
        }
        internal static string[] CopyRuntimePackage(BuildTarget buildTarget, string output, RuntimePackage package)
        {
            var data = buildTarget == BuildTarget.StandaloneWindows64
                ? Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_Data")
                : Path.Combine(output, "Contents/Resources/Data");
            var destination = Path.Combine(data, "StreamingAssets/ChooGuardRuntime");
            Directory.CreateDirectory(destination);
            var written = new System.Collections.Generic.List<string>();
            foreach (var file in package.Platform.files)
            {
                var source = package.VerifyFile(file.path);
                var target = Path.Combine(destination, file.path);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, false);
                if (buildTarget == BuildTarget.StandaloneOSX && (file.path == package.Platform.python || file.path == package.Platform.node) && Chmod(target, 493) != 0)
                    throw new IOException("Failed to preserve packaged runtime executable mode.");
                if (HashFile(target) != file.sha256) throw new IOException("Packaged file verification failed: " + file.path);
                written.Add(target);
            }
            var manifest = Path.Combine(destination, "runtime-manifest.json");
            using (var stream = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write))
                new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(RuntimePackageManifest)).WriteObject(stream,
                    new RuntimePackageManifest { schemaVersion = 1, platforms = new[] { package.Platform } });
            written.Add(manifest);
            return written.ToArray();
        }
        [System.Runtime.InteropServices.DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "chmod", SetLastError = true)]
        private static extern int Chmod(string path, uint mode);
        // Private result seam: tests supply simulated outcomes, never fabricated BuildReport objects.
        internal static void RecordBuildOutcome(BuildReceipt receipt, Func<BuildResult?> build, Func<string[]> reportedFiles)
        {
            var observed = new System.Collections.Generic.List<FileHash>();
            var inventory = "Output inventory UNOBSERVED; no returned BuildReport; empty outputs do not prove file absence.";
            try
            {
                var result = build();
                if (!result.HasValue) throw new InvalidOperationException("BuildPlayer returned no BuildReport");
                inventory = "Output inventory INCOMPLETE; reported-file enumeration or hashing unfinished; outputs do not prove file absence.";
                foreach (var path in reportedFiles())
                    if (File.Exists(path))
                        observed.Add(new FileHash { path = Path.GetFullPath(path), sha256 = HashFile(path) });
                receipt.outputs = observed.ToArray();
                inventory = "Output inventory OBSERVED_REPORTED_FILES_ONLY; existing reported files hashed, not an exhaustive directory inventory.";
                if (result.Value != BuildResult.Succeeded)
                    throw new InvalidOperationException("BuildReport=" + result.Value);
                receipt.buildStatus = "SUCCEEDED";
                receipt.reason = "BuildReport=" + result.Value + "; " + inventory + " Player execution not performed";
            }
            catch (Exception exception)
            {
                receipt.outputs = observed.ToArray();
                receipt.buildStatus = "FAILED";
                receipt.reason = inventory + " " + exception;
                throw;
            }
        }
    }
}
