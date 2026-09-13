using UnityEditor;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer.Editor
{
    public static class FoundationSimulationSceneBuilder
    {
        public const string Root = "Assets/CHOOguardGenerated/FoundationSimulation";
        public const string MacOutput = "Builds/FoundationSimulationMac/ChooGuardFoundation.app";
        [MenuItem("CHOOguard/Multiplayer/Build Foundation Simulation")]
        public static void Build() => ConnectedWorldSceneBuilder.Build(Root);
        [MenuItem("CHOOguard/Multiplayer/Prepare Foundation Simulation Mac")]
        public static void PrepareMac()
        {
            NativeSdkImportPolicy.Prepare();
            var directory=Path.GetDirectoryName(MacOutput); var marker=Path.Combine(directory,"chooguard-simulation-owner.txt");
            if (Directory.Exists(directory) && (!File.Exists(marker) || File.ReadAllText(marker).Trim()!="foundation-simulation-v1"))
                throw new InvalidOperationException("Simulation build directory is not owned by this builder.");
            Directory.CreateDirectory(directory); File.WriteAllText(marker,"foundation-simulation-v1\n");
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX),"Architecture",UnityEditor.Build.OSArchitecture.ARM64.ToString());
            var paths=ConnectedWorldSceneBuilder.Build(Root);
            EditorBuildSettings.scenes=paths.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
            PlayerSettings.companyName="XRLab"; PlayerSettings.productName="CHOOguard Foundation";
            PlayerSettings.defaultScreenWidth=1920; PlayerSettings.defaultScreenHeight=1080;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed; PlayerSettings.runInBackground=true;
            PlayerSettings.macOS.microphoneUsageDescription="훈련 중 무전 버튼을 누르는 동안 팀원에게 음성을 전달합니다.";
            PlayerSettings.macOS.cameraUsageDescription="이 훈련은 카메라를 사용하지 않습니다.";
            AssetDatabase.SaveAssets();
        }
    }
}
