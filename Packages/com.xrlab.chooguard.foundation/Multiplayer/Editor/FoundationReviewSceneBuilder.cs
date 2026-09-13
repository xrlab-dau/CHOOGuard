using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Editor
{
    public static class FoundationReviewSceneBuilder
    {
        public const string Root = "Assets/CHOOguardGenerated/FoundationReview";
        public const string MacOutput = "Builds/FoundationReviewMac/ChooGuardReview.app";

        public static string[] Build(string root = Root)
        {
            var paths = ConnectedWorldSceneBuilder.Build(root);
            var world = UnityEngine.Object.FindFirstObjectByType<ConnectedWorldRuntime>();
            world.GetComponent<NetworkFieldRuntime>().enabled = false;
            var capture = world.gameObject.AddComponent<WorldReviewCapture>();
            capture.World = world; capture.View = Camera.main;
            capture.Sun = world.GetComponentsInChildren<Light>().Single(l => l.type == LightType.Directional);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), paths[0]);
            AssetDatabase.SaveAssets();
            return paths;
        }

        [MenuItem("CHOOguard/Multiplayer/Prepare Foundation Review Mac")]
        public static void PrepareMac()
        {
            var directory=Path.GetDirectoryName(MacOutput); var marker=Path.Combine(directory,"chooguard-review-owner.txt");
            if (Directory.Exists(directory) && (!File.Exists(marker) || File.ReadAllText(marker).Trim()!="foundation-review-v1"))
                throw new InvalidOperationException("Review output directory is not owned by this builder.");
            Directory.CreateDirectory(directory); File.WriteAllText(marker,"foundation-review-v1\n");
            NativeSdkImportPolicy.Prepare();
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX),"Architecture",UnityEditor.Build.OSArchitecture.ARM64.ToString());
            EditorBuildSettings.scenes=Build().Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
            PlayerSettings.companyName="XRLab"; PlayerSettings.productName="CHOOguard Review";
            PlayerSettings.defaultScreenWidth=WorldReviewCapture.Width; PlayerSettings.defaultScreenHeight=WorldReviewCapture.Height;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.runInBackground=true;
            AssetDatabase.SaveAssets();
        }
    }
}
