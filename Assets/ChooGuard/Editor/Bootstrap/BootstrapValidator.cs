using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using ChooGuard.Presentation.Input;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Bootstrap
{
    public sealed class BootstrapSnapshot
    {
        public string EditorVersion, ProjectVersion, PackageLockSha256;
        public string[] MissingRequiredPackages = Array.Empty<string>();
        public string[] LocalPluginPaths = Array.Empty<string>();
        public int EventSystemCount, CameraCount, CanvasCount;
        public bool HasInputSystemModule, HasLegacyInputModule, InputActionsValid, TmpResourcesValid, UrpReferencesValid;
    }

    public sealed class BootstrapValidationReport
    {
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
        public BootstrapValidationReport(IEnumerable<string> errors) => Errors = Array.AsReadOnly(errors.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    public static class BootstrapValidator
    {
        public const string ScenePath = "Assets/ChooGuard/Scenes/Bootstrap.unity";
        public static readonly string[] RequiredPackages = { "com.unity.render-pipelines.universal", "com.unity.ugui", "com.unity.textmeshpro", "com.unity.inputsystem", "com.unity.test-framework" };
        public static BootstrapSnapshot Capture(string projectRoot)
        {
            var root = Path.GetFullPath(projectRoot);
            var snapshot = new BootstrapSnapshot { EditorVersion = Application.unityVersion };
            var version = Path.Combine(root, "ProjectSettings/ProjectVersion.txt");
            if (File.Exists(version)) snapshot.ProjectVersion = File.ReadLines(version).Single(x => x.StartsWith("m_EditorVersion: ", StringComparison.Ordinal)).Substring(17).Trim();
            var packageLock = Path.Combine(root, "Packages/packages-lock.json");
            if (File.Exists(packageLock)) snapshot.PackageLockSha256 = BuildBaseline.HashFile(packageLock);
            var assets = Path.Combine(root, "Assets");
            var extensions = new[] { ".dll", ".so", ".dylib", ".bundle" };
            // NuGetForUnity 가 Assets/packages.config 에 선언한 패키지(id·version)는 Assets/Packages/<id>.<version>/ 아래에 풀린다.
            // 그 안의 DLL 은 선언된 것이다(DotRecast). 선언 없이 Assets 에 놓인 DLL 만 LOCAL_PLUGIN_UNDECLARED 다.
            var declared = DeclaredNuGetFolders(root);
            if (Directory.Exists(assets)) snapshot.LocalPluginPaths = Directory.EnumerateFileSystemEntries(assets, "*", SearchOption.AllDirectories)
                .Where(p => extensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Select(p => p.Substring(root.Length + 1).Replace('\\', '/'))
                .Where(p => !declared.Any(folder => p.StartsWith(folder, StringComparison.Ordinal)))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            // PackageInfo and AssetDatabase belong to the open Editor project, never a foreign fixture directory.
            if (root != Path.GetFullPath(Path.Combine(Application.dataPath, "..")))
            {
                snapshot.MissingRequiredPackages = RequiredPackages.ToArray();
                return snapshot;
            }
            snapshot.MissingRequiredPackages = RequiredPackages.Except(PackageInfo.GetAllRegisteredPackages().Select(p => p.name)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            snapshot.UrpReferencesValid = HasOwnedUrpReferences();
            if (!File.Exists(Path.Combine(root, ScenePath))) return snapshot;
            // Preview scenes do not replace, save or reopen the user's current scene setup, including unsaved scenes.
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try { ObserveScene(scene, snapshot); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return snapshot;
        }

        private static string[] DeclaredNuGetFolders(string root)
        {
            var config = Path.Combine(root, "Assets/packages.config");
            if (!File.Exists(config)) return Array.Empty<string>();
            return System.Xml.Linq.XDocument.Load(config).Root.Elements("package")
                .Select(p => "Assets/Packages/" + (string)p.Attribute("id") + "." + (string)p.Attribute("version") + "/")
                .ToArray();
        }

        public static void ObserveScene(Scene scene, BootstrapSnapshot snapshot)
        {
            T[] Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
            snapshot.CameraCount = Find<Camera>().Length;
            snapshot.CanvasCount = Find<Canvas>().Length;
            var systems = Find<EventSystem>();
            snapshot.EventSystemCount = systems.Length;
            var modules = Find<InputSystemUIInputModule>();
            snapshot.HasInputSystemModule = modules.Length == 1 && modules[0].enabled && systems.Length == 1 && modules[0].gameObject == systems[0].gameObject && systems[0].isActiveAndEnabled;
            snapshot.HasLegacyInputModule = Find<StandaloneInputModule>().Length != 0;
            snapshot.InputActionsValid = modules.Length == 1 && HasPersistentActions(modules[0]);
            // Bootstrap 은 이제 타이틀 화면이다(2026-09-26 방향 재정렬). 글자는 실행 중 TitleScreen 이 만들므로
            // 씬에 고정 표식 대신 타이틀의 한국어 글꼴 연결이 온전한지를 본다.
            var titles = Find<ChooGuard.App.Fps.Shell.TitleScreen>();
            var titleFont = titles.Length == 1 ? titles[0].KoreanFont : null;
            snapshot.TmpResourcesValid = titleFont != null && titleFont.atlasTexture != null && titleFont.material != null &&
                titleFont.material.shader != null && titleFont.material.shader.isSupported;
        }

        public static bool HasPersistentActions(InputSystemUIInputModule module)
        {
            if (module == null) return false;
            var routers = module.GetComponents<InputContextRouter>();
            if (routers.Length != 1 || !routers[0].enabled) return false;
            var router = routers[0];
            var serialized = new SerializedObject(router);
            var source = serialized.FindProperty("operations").objectReferenceValue as InputActionAsset;
            if (source == null || AssetDatabase.GetAssetPath(source) != BootstrapProject.OperationsActionsPath ||
                serialized.FindProperty("inputModule").objectReferenceValue != module ||
                serialized.FindProperty("eventSystem").objectReferenceValue != module.GetComponent<EventSystem>()) return false;
            if (Application.isPlaying && router.RuntimeActions != null)
            {
                var runtime = router.RuntimeActions;
                var cancel = runtime.FindAction("UI/Cancel");
                return module.actionsAsset == runtime && !AssetDatabase.Contains(runtime) && module.cancel == null &&
                    !source.actionMaps.Any(map => map.enabled) && cancel != null && cancel.enabled &&
                    cancel.bindings.Any(binding => binding.effectivePath == "<Keyboard>/escape") &&
                    new[] { module.point, module.leftClick, module.submit }.All(x => x != null && x.action != null &&
                        x.action.enabled && x.action.actionMap.asset == runtime);
            }
            if (module.actionsAsset != source || !AssetDatabase.Contains(source) || router.RuntimeActions != null) return false;
            var refs = new[] { module.point, module.leftClick, module.submit, module.cancel };
            return refs.All(x => x != null && AssetDatabase.Contains(x) && x.action != null && x.action.bindings.Count > 0 && x.action.actionMap.asset == source);
        }

        public static bool HasOwnedUrpReferences()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(BootstrapProject.PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(BootstrapProject.RendererPath);
            var global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(BootstrapProject.GlobalPath);
            if (pipeline == null || renderer == null || global == null || GraphicsSettings.defaultRenderPipeline != pipeline ||
                EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>() != global) return false;
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            if (list.arraySize != 1 || list.GetArrayElementAtIndex(0).objectReferenceValue != renderer) return false;
            for (var i = 0; i < QualitySettings.names.Length; i++) if (QualitySettings.GetRenderPipelineAssetAt(i) != pipeline) return false;
            VolumeProfile profile;
            try { profile = BootstrapProject.DefaultVolumeSettings(global).volumeProfile; }
            catch (InvalidOperationException) { return false; }
            return profile != null && AssetDatabase.GetAssetPath(profile) == BootstrapProject.GlobalPath &&
                profile.components.All(c => c != null && AssetDatabase.GetAssetPath(c) == BootstrapProject.GlobalPath) &&
                !AssetDatabase.GetDependencies(BootstrapProject.GlobalPath, true).Any(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p != BootstrapProject.GlobalPath);
        }

        public static BootstrapValidationReport Validate(BootstrapSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var errors = new List<string>();
            if (snapshot.EditorVersion != "6000.3.23f1" || snapshot.ProjectVersion != snapshot.EditorVersion) errors.Add("EDITOR_VERSION_MISMATCH");
            if (string.IsNullOrEmpty(snapshot.PackageLockSha256)) errors.Add("PACKAGE_LOCK_MISSING");
            if (snapshot.MissingRequiredPackages.Length != 0) errors.Add("REQUIRED_PACKAGE_MISSING");
            if (snapshot.LocalPluginPaths.Length != 0) errors.Add("LOCAL_PLUGIN_UNDECLARED");
            if (snapshot.EventSystemCount != 1) errors.Add("EVENT_SYSTEM_COUNT");
            if (snapshot.CameraCount != 1 || snapshot.CanvasCount != 1) errors.Add("CAMERA_CANVAS_MISSING");
            if (!snapshot.HasInputSystemModule || snapshot.HasLegacyInputModule) errors.Add("INPUT_MODULE_INVALID");
            if (!snapshot.InputActionsValid) errors.Add("INPUT_ACTIONS_MISSING");
            if (!snapshot.TmpResourcesValid) errors.Add("TMP_RESOURCE_MISSING");
            if (!snapshot.UrpReferencesValid) errors.Add("URP_REFERENCE_MISSING");
            return new BootstrapValidationReport(errors);
        }
    }
}
