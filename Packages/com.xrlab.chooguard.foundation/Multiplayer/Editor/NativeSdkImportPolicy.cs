using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ChooGuard.Foundation.Multiplayer.Editor
{
    /// <summary>Reviewed desktop importer normalization for the exact LiveKit 2.0.0 native files.</summary>
    public static class NativeSdkImportPolicy
    {
        public const string OwnedRoot = "Assets/CHOOguardGenerated/MultiplayerNative";
        private const string Owner = "com.xrlab.chooguard.foundation.native-sdk-v1";
        [Serializable] private sealed class Pin { public string path, sha256; public long size; }
        [Serializable] private sealed class Pins { public string livekitSdk; public Pin[] desktopBinaries; }

        [MenuItem("CHOOguard/Multiplayer/Prepare Native Voice Plugins")]
        public static void Prepare()
        {
            var pins = JsonUtility.FromJson<Pins>(File.ReadAllText("foundation/network/dependency-contract.json"));
            var package = PackageInfo.FindForAssetPath("Packages/io.livekit.livekit-sdk/Runtime");
            if (package == null || package.version != pins.livekitSdk)
                throw new InvalidDataException("Resolve the pinned LiveKit SDK before preparing importers.");
            var native = pins.desktopBinaries.Where(p => p.path.Contains("/ffi-")).ToArray();
            // Validate the entire input set before changing any import setting.
            foreach (var pin in native)
            {
                var bytes = File.ReadAllBytes(Path.Combine(package.resolvedPath, pin.path));
                if (bytes.LongLength != pin.size || Hash(bytes) != pin.sha256)
                    throw new InvalidDataException("Run prepare_multiplayer_dependencies.py --apply: " + pin.path);
            }
            var ownerFile = OwnedRoot + "/owner.txt";
            if (Directory.Exists(OwnedRoot) && (!File.Exists(ownerFile) || File.ReadAllText(ownerFile) != Owner))
                throw new InvalidOperationException("Native output directory has no matching ownership marker.");
            Directory.CreateDirectory(OwnedRoot);
            File.WriteAllText(ownerFile, Owner);
            foreach (var pin in native)
            {
                // PackageCache importer changes are not durable. Own a reproducible, ignored player copy instead.
                var asset = OwnedRoot + "/" + pin.path.Substring("Runtime/Plugins/".Length);
                Directory.CreateDirectory(Path.GetDirectoryName(asset));
                if (!File.Exists(asset) || Hash(File.ReadAllBytes(asset)) != pin.sha256)
                    File.Copy(Path.Combine(package.resolvedPath, pin.path), asset, true);
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(asset) as PluginImporter;
                if (importer == null) throw new InvalidOperationException("Native dependency has no plugin importer: " + pin.path);
                var mac = pin.path.Contains("ffi-macos-");
                var windows = pin.path.Contains("ffi-windows-");
                var target = mac ? BuildTarget.StandaloneOSX : windows ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneLinux64;
                var cpu = pin.path.Contains("-arm64/") ? "ARM64" : "x86_64";
                var os = mac ? "OSX" : windows ? "Windows" : "Linux";
                var changed = importer.GetCompatibleWithAnyPlatform() || importer.GetCompatibleWithEditor() ||
                    importer.GetEditorData("OS") != os || importer.GetEditorData("CPU") != cpu ||
                    importer.GetPlatformData(target, "CPU") != cpu;
                importer.SetCompatibleWithAnyPlatform(false);
                // Original SDK plugins remain the Editor's source; these copies are only Player inputs.
                importer.SetCompatibleWithEditor(false);
                importer.SetEditorData("OS", os); importer.SetEditorData("CPU", cpu);
                foreach (var platform in new[] { BuildTarget.StandaloneOSX, BuildTarget.StandaloneWindows,
                    BuildTarget.StandaloneWindows64, BuildTarget.StandaloneLinux64, BuildTarget.Android, BuildTarget.iOS, BuildTarget.WebGL })
                {
                    var enabled = platform == target;
                    changed |= importer.GetCompatibleWithPlatform(platform) != enabled;
                    importer.SetCompatibleWithPlatform(platform, enabled);
                }
                importer.SetPlatformData(target, "CPU", cpu);
                if (changed) importer.SaveAndReimport();
            }
            RegisterPackageExclusions();
            Debug.Log("Verified native SDK hashes and prepared five owned desktop Player plugins.");
        }

        public static void RegisterPackageExclusions()
        {
            var pins = JsonUtility.FromJson<Pins>(File.ReadAllText("foundation/network/dependency-contract.json"));
            foreach (var pin in pins.desktopBinaries.Where(p => p.path.Contains("/ffi-")))
            {
                var original = AssetImporter.GetAtPath("Packages/io.livekit.livekit-sdk/" + pin.path) as PluginImporter;
                if (original == null) throw new InvalidOperationException("Original SDK plugin importer is missing.");
                original.SetIncludeInBuildDelegate(path => false);
                var owned = OwnedRoot + "/" + pin.path.Substring("Runtime/Plugins/".Length);
                if (!File.Exists(owned) || Hash(File.ReadAllBytes(owned)) != pin.sha256)
                    throw new InvalidDataException("Run Prepare Native Voice Plugins before building.");
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }

    public sealed class NativeSdkBuildPreflight : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => NativeSdkImportPolicy.RegisterPackageExclusions();
    }
}
