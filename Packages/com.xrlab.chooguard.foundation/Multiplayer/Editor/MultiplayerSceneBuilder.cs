using System;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Demo;
using ChooGuard.Foundation.Demo.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Editor
{
    public static class MultiplayerSceneBuilder
    {
        public const string Root = "Assets/CHOOguardGenerated/MultiplayerSlice";
        public const string ScenePath = Root + "/FoundationDemo.unity";
        public const string Output = "Builds/FoundationMultiplayerMac";

        [MenuItem("CHOOguard/Multiplayer/Build Shared Station Slice")]
        public static void BuildMenu() => Build();

        public static Scene Build(string rootPath = Root)
        {
            var scene = FoundationDemoSceneBuilder.Build(rootPath);
            var root = scene.GetRootGameObjects().Single();
            root.name = "MultiplayerStation";
            var canonicalAnchors = Enumerable.Range(1, 5).Select(i => "anchor-" + i.ToString("00")).ToArray();
            var targets = root.GetComponentsInChildren<DemoInteractable>(true).Where(t => canonicalAnchors.Contains(t.AnchorId)).ToArray();
            foreach (var target in targets)
            {
                var anchor = target.gameObject.AddComponent<NetworkEntityAnchor>();
                anchor.EntityId = target.AnchorId; anchor.Kind = EntityKind.Equipment;
                anchor.RequiredRoleId = "role-" + target.AnchorId.Substring("anchor-".Length);
            }
            var evacuees = root.GetComponentsInChildren<DemoEvacuee>(true);
            for (var i = 0; i < evacuees.Length; i++)
            {
                var anchor = evacuees[i].gameObject.AddComponent<NetworkEntityAnchor>();
                anchor.EntityId = "npc-" + i; anchor.Kind = EntityKind.Evacuee;
            }
            // Reuse authored geometry, without serializing the old incident/controller definitions into this build.
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Namespace == "ChooGuard.Foundation.Demo" && !(behaviour is DemoInteractable))
                    UnityEngine.Object.DestroyImmediate(behaviour);
            foreach (var controller in root.GetComponentsInChildren<CharacterController>(true))
                UnityEngine.Object.DestroyImmediate(controller);
            root.AddComponent<NetworkFieldRuntime>();
            if (!EditorSceneManager.SaveScene(scene, rootPath + "/FoundationDemo.unity"))
                throw new IOException("Could not save the multiplayer scene.");
            var layout = new WorldState { WorldId = "synthetic-station", ShiftId = "shift-001",
                Entities = root.GetComponentsInChildren<NetworkEntityAnchor>().Select(a => new EntityState {
                    EntityId = a.EntityId, RegionId = a.RegionId, RequiredRoleId = a.RequiredRoleId, Kind = a.Kind,
                    Position = new Point3(a.transform.position.x, a.transform.position.y, a.transform.position.z) }).ToArray() };
            if (rootPath == Root)
            {
                Directory.CreateDirectory("foundation/network");
                File.WriteAllText("foundation/network/slice-layout.json", JsonUtility.ToJson(layout, true) + "\n");
            }
            Debug.Log("Shared station slice generated. Geometry is provisional; network/runtime verification remains separate.");
            return scene;
        }

        [MenuItem("CHOOguard/Multiplayer/Build Mac Client And Local Server")]
        public static void BuildMac() => BuildPlayer(BuildTarget.StandaloneOSX, Output + "/ChooGuardMultiplayer.app", false);

        [MenuItem("CHOOguard/Multiplayer/Prepare Mac Build")]
        public static void PrepareMac() => PreparePlayer(BuildTarget.StandaloneOSX, Output + "/ChooGuardMultiplayer.app");

        [MenuItem("CHOOguard/Multiplayer/Build Windows Client")]
        public static void BuildWindows() => BuildPlayer(BuildTarget.StandaloneWindows64, "Builds/FoundationMultiplayerWindows/ChooGuardMultiplayer.exe", false);

        [MenuItem("CHOOguard/Multiplayer/Build Windows Dedicated Server")]
        public static void BuildWindowsServer() => BuildPlayer(BuildTarget.StandaloneWindows64, "Builds/FoundationServerWindows/ChooGuardServer.exe", true);

        [MenuItem("CHOOguard/Multiplayer/Build Linux Dedicated Server")]
        public static void BuildLinuxServer() => BuildPlayer(BuildTarget.StandaloneLinux64, "Builds/FoundationServerLinux/ChooGuardServer", true);

        private static void BuildPlayer(BuildTarget target, string output, bool dedicated)
        {
            var scene = PreparePlayer(target, output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene.path },
                target = target, locationPathName = output, options = BuildOptions.None,
                subtarget = (int)(dedicated ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player) });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Multiplayer player build failed: " + report.summary.result);
            Debug.Log("Multiplayer build succeeded: " + output);
        }

        private static Scene PreparePlayer(BuildTarget target, string output)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException("Required target support is not installed: " + target);
            NativeSdkImportPolicy.Prepare();
            if (target == BuildTarget.StandaloneOSX)
                EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(target),
                    "Architecture", UnityEditor.Build.OSArchitecture.ARM64.ToString());
            var outputDirectory = Path.GetDirectoryName(output);
            var marker = Path.Combine(outputDirectory, "chooguard-multiplayer-owner.txt");
            if (Directory.Exists(outputDirectory) && (!File.Exists(marker) ||
                File.ReadAllText(marker).Trim() != "com.xrlab.chooguard.foundation.multiplayer.v1"))
                throw new InvalidOperationException("Build directory is not owned by this builder.");
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(marker, "com.xrlab.chooguard.foundation.multiplayer.v1\n");
            var scene = Build();
            PlayerSettings.companyName = "XRLab";
            PlayerSettings.productName = "CHOOguard 공동 훈련";
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            // Runtime endpoint validation permits plaintext only on loopback for the local SFU control API.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.macOS.microphoneUsageDescription = "훈련 중 무전 버튼을 누르는 동안 팀원에게 음성을 전달합니다.";
            // SDK links WebCamTexture even for voice-only clients; this app never starts camera capture.
            PlayerSettings.macOS.cameraUsageDescription = "카메라 접근은 이 음성 훈련에 필요하지 않습니다.";
            AssetDatabase.SaveAssets();
            return scene;
        }
    }
}
