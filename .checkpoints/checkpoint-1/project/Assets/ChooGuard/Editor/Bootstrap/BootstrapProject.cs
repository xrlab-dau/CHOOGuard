using System;
using System.IO;
using System.Linq;
using TMPro;
using ChooGuard.Presentation.Input;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor.Bootstrap
{
    public static class BootstrapProject
    {
        public const string SettingsPath = "Assets/ChooGuard/Settings/";
        public const string PipelinePath = SettingsPath + "BootstrapURP.asset";
        public const string RendererPath = SettingsPath + "BootstrapRenderer.asset";
        public const string GlobalPath = SettingsPath + "BootstrapURPGlobalSettings.asset";
        public const string FontPath = SettingsPath + "TMP/Fonts/ChooGuard Bootstrap SDF.asset";
        public const string DefaultActionsPath = "Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/PlayerInput/DefaultInputActions.inputactions";
        public const string OperationsActionsPath = "Assets/ChooGuard/Presentation/Input/Operations.inputactions";
        private const string DefaultGlobal = "Assets/UniversalRenderPipelineGlobalSettings.asset";
        private const string DefaultVolume = "Assets/DefaultVolumeProfile.asset";

        [MenuItem("ChooGuard/Bootstrap/Create owned assets")]
        public static void CreateBootstrapAssets()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes before generating Bootstrap");
            EnsureFolder("Assets/ChooGuard/Scenes");
            EnsureFolder(SettingsPath.TrimEnd('/'));
            ConfigurePipeline();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("TMP_RESOURCE_MISSING");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var emptyBatchStartup = Application.isBatchMode && SceneManager.sceneCount == 1 && string.IsNullOrEmpty(SceneManager.GetSceneAt(0).path) && !SceneManager.GetSceneAt(0).isDirty && SceneManager.GetSceneAt(0).rootCount == 0;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptyBatchStartup ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                // Unity cannot save a second loaded scene over the same path. Dirty scenes were
                // rejected above; close only the saved owned scene and restore its setup in finally.
                var ownedOpenScenes = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt).Where(x => x.handle != scene.handle && x.path == BootstrapValidator.ScenePath).ToArray();
                foreach (var owned in ownedOpenScenes)
                    if (!EditorSceneManager.CloseScene(owned, true)) throw new IOException("Could not close saved owned Bootstrap scene");
                var camera = new GameObject("Camera", typeof(Camera));
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 0, -10);
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                camera.GetComponent<Camera>().backgroundColor = new Color(0.04f, 0.06f, 0.09f);
                camera.AddComponent<UniversalAdditionalCameraData>();
                var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                var label = new GameObject("Bootstrap Marker", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(canvas.transform, false);
                var marker = label.GetComponent<TextMeshProUGUI>();
                marker.font = font;
                marker.fontSharedMaterial = font.material;
                marker.text = "CHOOGuard bootstrap";
                marker.fontSize = 36;
                marker.alignment = TextAlignmentOptions.Center;
                marker.raycastTarget = false;
                marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                marker.rectTransform.sizeDelta = new Vector2(800, 100);
                var system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                WireOperationsInput(scene);
                if (!EditorSceneManager.SaveScene(scene, BootstrapValidator.ScenePath)) throw new IOException("Could not save Bootstrap scene");
            }
            finally
            {
                if (emptyBatchStartup) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else
                {
                    EditorSceneManager.CloseScene(scene, true);
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapValidator.ScenePath, true) };
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.productName = "CHOOGuard";
            PlayerSettings.bundleVersion = "0.1.0";
            EditorUserBuildSettings.development = true;
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = player.FindProperty("activeInputHandler") ?? throw new InvalidOperationException("Missing activeInputHandler");
            input.intValue = 1;
            player.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("CG_BOOT_CREATED: owned scene, pipeline, persistent package UI actions");
        }

        // Scene-only executeMethod: no pipeline, project settings, font or package writes.
        [MenuItem("ChooGuard/Bootstrap/Connect operations input")]
        public static void ConnectOperationsInput()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop play mode and save open scenes before connecting Bootstrap input");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(BootstrapValidator.ScenePath);
            var alreadyLoaded = scene.IsValid() && scene.isLoaded;
            try
            {
                if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(BootstrapValidator.ScenePath, OpenSceneMode.Additive);
                WireOperationsInput(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Bootstrap input connection");
            }
            finally
            {
                if (!alreadyLoaded && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                // Restore saved scene setups; retain an empty untitled startup scene in place.
                if (setup.Length > 0 && setup.All(x => !string.IsNullOrEmpty(x.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        private static void WireOperationsInput(Scene scene)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Editor serialization only");
            var systems = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<EventSystem>(true)).ToArray();
            if (systems.Length != 1) throw new InvalidOperationException("Bootstrap requires exactly one EventSystem");
            var system = systems[0];
            var modules = system.GetComponents<InputSystemUIInputModule>();
            if (modules.Length != 1) throw new InvalidOperationException("Bootstrap requires exactly one input module");
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(OperationsActionsPath);
            if (source == null) throw new InvalidOperationException("Operations input asset missing");
            // Input System also imports hidden legacy-ID duplicates; serialize only canonical references.
            var references = AssetDatabase.LoadAllAssetsAtPath(OperationsActionsPath).OfType<InputActionReference>()
                .Where(x => (x.hideFlags & HideFlags.HideInHierarchy) == 0).ToArray();
            var names = new[] { "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel", "Navigate", "Submit", "Cancel" };
            var fields = new[] { "m_PointAction", "m_LeftClickAction", "m_RightClickAction", "m_MiddleClickAction",
                "m_ScrollWheelAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction" };
            var actions = names.Select(name => references.Single(x => x.action?.actionMap.name == "UI" && x.action.name == name)).ToArray();
            var routers = system.GetComponents<InputContextRouter>();
            if (routers.Length > 1) throw new InvalidOperationException("Duplicate input routers");
            var router = routers.SingleOrDefault() ?? system.gameObject.AddComponent<InputContextRouter>();
            // Do not call Configure: that runtime API creates a nonpersistent clone.
            var serializedModule = new SerializedObject(modules[0]);
            serializedModule.FindProperty("m_ActionsAsset").objectReferenceValue = source;
            for (var i = 0; i < fields.Length; i++) serializedModule.FindProperty(fields[i]).objectReferenceValue = actions[i];
            serializedModule.FindProperty("m_TrackedDevicePositionAction").objectReferenceValue = null;
            serializedModule.FindProperty("m_TrackedDeviceOrientationAction").objectReferenceValue = null;
            serializedModule.ApplyModifiedPropertiesWithoutUndo();
            var serializedRouter = new SerializedObject(router);
            serializedRouter.FindProperty("operations").objectReferenceValue = source;
            serializedRouter.FindProperty("eventSystem").objectReferenceValue = system;
            serializedRouter.FindProperty("inputModule").objectReferenceValue = modules[0];
            serializedRouter.ApplyModifiedPropertiesWithoutUndo();
            router.enabled = modules[0].enabled = system.enabled = true;
            EditorUtility.SetDirty(router);
            EditorUtility.SetDirty(modules[0]);
        }

        private static void ConfigurePipeline()
        {
            var global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(GlobalPath);
            if (File.Exists(DefaultGlobal) || File.Exists(DefaultVolume))
            {
                if (global != null) throw new InvalidOperationException("Owned and excluded defaults coexist; refusing overwrite");
                NormalizeObservedDefaults();
                global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(GlobalPath);
            }
            if (global == null)
            {
                global = ScriptableObject.CreateInstance("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings") as RenderPipelineGlobalSettings;
                if (global == null) throw new InvalidOperationException("URP global settings type unavailable");
                AssetDatabase.CreateAsset(global, GlobalPath);
                EditorGraphicsSettings.PopulateRenderPipelineGraphicsSettings(global);
                var volume = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.name = "Bootstrap Default Volume";
                AssetDatabase.AddObjectToAsset(volume, global);
                DefaultVolumeSettings(global).volumeProfile = volume;
                global.Initialize();
            }
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(global);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null) { pipeline = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(pipeline, PipelinePath); }
            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList");
            renderers.arraySize = 1; renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            var previousQuality = QualitySettings.GetQualityLevel();
            try
            {
                for (var i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
            }
            finally { QualitySettings.SetQualityLevel(previousQuality, false); }
            EditorUtility.SetDirty(global); EditorUtility.SetDirty(pipeline); EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        [Serializable] private sealed class NormalizationManifest { public NormalizationFile[] files; }
        [Serializable] private sealed class NormalizationFile { public string path, sha256, backupPath; }
        private static void NormalizeObservedDefaults()
        {
            // One-time migration is opt-in and content-addressed; ordinary generation needs no environment variables.
            var manifestPath = BuildBaseline.Argument("-cgNormalizeManifest");
            var manifestHash = BuildBaseline.Argument("-cgNormalizeManifestSha256");
            if (string.IsNullOrEmpty(manifestPath) || BuildBaseline.HashFile(manifestPath) != manifestHash)
                throw new InvalidOperationException("Unapproved root defaults; normalization manifest required");
            var manifest = JsonUtility.FromJson<NormalizationManifest>(File.ReadAllText(manifestPath));
            var expected = new[] { DefaultGlobal, DefaultGlobal + ".meta", DefaultVolume, DefaultVolume + ".meta" };
            if (manifest.files.Length != 4 || !manifest.files.Select(x => x.path).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)))
                throw new InvalidOperationException("Normalization path mismatch");
            foreach (var row in manifest.files)
                if (BuildBaseline.HashFile(row.path) != row.sha256 || BuildBaseline.HashFile(row.backupPath) != row.sha256)
                    throw new InvalidOperationException("Normalization source drift: " + row.path);
            if (File.Exists(GlobalPath)) throw new IOException("Owned global destination exists");
            var error = AssetDatabase.MoveAsset(DefaultGlobal, GlobalPath);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            var global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(GlobalPath);
            var original = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultVolume);
            if (original == null || global == null) throw new InvalidOperationException("Missing default volume");
            var setting = DefaultVolumeSettings(global);
            var clone = Object.Instantiate(original);
            clone.name = "Bootstrap Default Volume";
            clone.components.Clear();
            AssetDatabase.AddObjectToAsset(clone, global);
            foreach (var component in original.components)
            {
                if (component == null) throw new InvalidOperationException("Missing VolumeComponent");
                var copied = Object.Instantiate(component);
                AssetDatabase.AddObjectToAsset(copied, global);
                clone.components.Add(copied);
            }
            setting.volumeProfile = clone;
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(global);
            EditorUtility.SetDirty(clone); EditorUtility.SetDirty(global); AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GlobalPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.GetDependencies(GlobalPath, true).Contains(DefaultVolume)) throw new InvalidOperationException("External volume dependency remains");
            foreach (var row in manifest.files.Where(x => x.path.StartsWith(DefaultVolume, StringComparison.Ordinal)))
                if (BuildBaseline.HashFile(row.path) != row.sha256) throw new InvalidOperationException("Volume changed before deletion");
            if (!AssetDatabase.DeleteAsset(DefaultVolume)) throw new IOException("Could not remove verified generated default volume");
            Debug.Log("CG_BOOT_NORMALIZED: default global GUID retained; volume components=" + clone.components.Count);
        }

        public static URPDefaultVolumeProfileSettings DefaultVolumeSettings(RenderPipelineGlobalSettings global)
        {
            // URP 17.3 keeps its implementation and base TryGet internal. Read the observed
            // SerializeReference list through the public Editor serialization API, without reflection.
            var list = new SerializedObject(global).FindProperty("m_Settings.m_SettingsList.m_List");
            if (list == null || !list.isArray) throw new InvalidOperationException("Unsupported URP settings layout");
            for (var i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).managedReferenceValue is URPDefaultVolumeProfileSettings setting) return setting;
            throw new InvalidOperationException("URP default-volume setting missing");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        [MenuItem("ChooGuard/Bootstrap/Write baseline")]
        public static void WriteBaseline() => BuildBaseline.Write();
        public static void BuildDevelopmentPlayer() => BuildBaseline.Build();
    }
}
