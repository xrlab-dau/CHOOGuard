#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ChooGuard.Editor.Bootstrap;
using ChooGuard.Presentation.Input;
using UnityEngine.InputSystem;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ChooGuard.Tests.EditMode.Stories
{
    public class CSBOOT0101Tests
    {
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(global::UnityEngine.Application.dataPath, ".."));
        private const string SettingsPath = "Assets/ChooGuard/Settings/Resources/TMP Settings.asset";
        private const string FontPath = BootstrapProject.FontPath;
        private static readonly string[] Whitelist = {
            SettingsPath, FontPath,
            "Assets/ChooGuard/Settings/TMP/Shaders/TMP_SDF-Mobile.shader",
            "Assets/ChooGuard/Settings/TMP/Shaders/TMPro_Properties.cginc",
            "Assets/ChooGuard/Settings/TMP/LineBreaking/Leading Characters.txt",
            "Assets/ChooGuard/Settings/TMP/LineBreaking/Following Characters.txt",
            "Assets/ChooGuard/ThirdPartyNotices/LiberationSans-SDF-OFL.txt"
        };
        private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).ToArray();
        private static void Preview(Action<Scene> inspect)
        {
            var scene = EditorSceneManager.OpenPreviewScene(BootstrapValidator.ScenePath);
            try { inspect(scene); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static SerializedProperty Required(SerializedObject value, string name)
        {
            var property = value.FindProperty(name);
            Assert.That(property, Is.Not.Null, "Serialized field missing: " + name);
            return property;
        }
        [Test]
        public void BootstrapScene_HasOneCameraCanvasAndEventSystem()
        {
            var snapshot = BootstrapValidator.Capture(ProjectRoot);
            Assert.That(snapshot.CameraCount, Is.EqualTo(1));
            Assert.That(snapshot.CanvasCount, Is.EqualTo(1));
            Assert.That(snapshot.EventSystemCount, Is.EqualTo(1));
        }
        [Test]
        public void SubmittedScene_HasValidConfigurationAndNoMissingScripts()
        {
            var snapshot = BootstrapValidator.Capture(ProjectRoot);
            Assert.That(BootstrapValidator.Validate(snapshot).Errors, Is.Empty);
            Preview(scene => {
                foreach (var transform in Find<Transform>(scene))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
                var canvas = Find<Canvas>(scene).Single();
                Assert.That(canvas.enabled, Is.True);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Not.Null);
                var scaler = canvas.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1280, 720)));
                var marker = Find<TextMeshProUGUI>(scene).Single();
                Assert.That(marker.raycastTarget, Is.False);
                Assert.That(marker.rectTransform.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
                Assert.That(marker.rectTransform.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)));
            });
            // 빌드 씬 목록은 부트스트랩 시절 '정확히 1개'였다. FPS 전환으로 FpsStation.unity 가
            // 등록되면서(2026-09-22, 수직 슬라이스의 선행 조건) 그 단정이 거짓이 됐다.
            // 다만 이 테스트가 지키려던 불변식은 개수가 아니라 '부트스트랩 씬이 0번 인덱스에
            // enabled 로 있다'는 진입점 규율이므로, 개수 대신 그것만 단정한다.
            // 개수를 새 숫자로 고정하면 씬을 하나 더 추가할 때 같은 방식으로 또 깨진다.
            Assert.That(EditorBuildSettings.scenes, Is.Not.Empty);
            Assert.That(EditorBuildSettings.scenes[0].path, Is.EqualTo(BootstrapValidator.ScenePath));
            Assert.That(EditorBuildSettings.scenes[0].enabled, Is.True);
            Assert.That(PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone), Is.EqualTo(ScriptingImplementation.Mono2x));
            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            Assert.That(Required(player, "activeInputHandler").intValue, Is.EqualTo(1));
        }
        [Test]
        public void SubmittedInput_PreservesPersistentSourceCancelAndSerializedRouterReferences()
        {
            Preview(scene => {
                var router = Find<InputContextRouter>(scene).Single();
                var module = Find<InputSystemUIInputModule>(scene).Single();
                var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(BootstrapProject.OperationsActionsPath);
                var serialized = new SerializedObject(router);
                Assert.That(Required(serialized, "operations").objectReferenceValue, Is.SameAs(source));
                Assert.That(Required(serialized, "eventSystem").objectReferenceValue, Is.SameAs(Find<EventSystem>(scene).Single()));
                Assert.That(Required(serialized, "inputModule").objectReferenceValue, Is.SameAs(module));
                Assert.That(router.RuntimeActions, Is.Null, "Editor saving must not create a runtime clone");
                Assert.That(BootstrapValidator.HasPersistentActions(module), Is.True);
                Assert.That(module.cancel.action, Is.SameAs(source.FindAction("UI/Cancel", true)));
                module.cancel = null;
                Assert.That(BootstrapValidator.HasPersistentActions(module), Is.False, "Only runtime cancellation is router-owned");
            });
        }

        [Test]
        public void Capture_PreservesDirtyUnsavedSceneAndSubmittedBytes()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sentinel = new GameObject("Unsaved sentinel");
            EditorSceneManager.MarkSceneDirty(scene);
            var before = BuildBaseline.HashFile(BootstrapValidator.ScenePath);
            try
            {
                BootstrapValidator.Capture(ProjectRoot);
                Assert.That(scene.IsValid() && scene.isLoaded && scene.isDirty, Is.True);
                Assert.That(sentinel, Is.Not.Null);
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle));
                Assert.That(BuildBaseline.HashFile(BootstrapValidator.ScenePath), Is.EqualTo(before));
                Assert.Throws<InvalidOperationException>(() => BootstrapProject.CreateBootstrapAssets());
                Assert.Throws<InvalidOperationException>(() => BootstrapProject.ConnectOperationsInput());
                Assert.That(scene.isDirty && sentinel != null, Is.True);
                Assert.That(BuildBaseline.HashFile(BootstrapValidator.ScenePath), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(sentinel);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Length > 0 && setup.All(x => !string.IsNullOrEmpty(x.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
        [Test]
        public void Capture_PreservesOpenSavedScene()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.OpenScene(BootstrapValidator.ScenePath, OpenSceneMode.Single);
            try
            {
                BootstrapValidator.Capture(ProjectRoot);
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle));
                Assert.That(scene.path, Is.EqualTo(BootstrapValidator.ScenePath));
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Length > 0 && setup.All(x => !string.IsNullOrEmpty(x.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
        public static void RunVolumeObservationProbe()
        {
            var global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(BootstrapProject.GlobalPath);
            var profile = BootstrapProject.DefaultVolumeSettings(global).volumeProfile;
            foreach (var asset in new Object[] { global, profile }.Concat(profile.components.Cast<Object>()))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id);
                Debug.Log("CG_BOOT_VOLUME_OBSERVE: name=" + asset.name + "; type=" + asset.GetType().FullName +
                    "; path=" + AssetDatabase.GetAssetPath(asset) + "; guid=" + guid + "; id=" + id +
                    "; main=" + AssetDatabase.IsMainAsset(asset) + "; sub=" + AssetDatabase.IsSubAsset(asset) +
                    "; persistent=" + EditorUtility.IsPersistent(asset) + "; flags=" + asset.hideFlags);
            }
        }
        // Explicit CLI-only generator probe: never runs in the submitted-scene NUnit suite.
        public static void RunGeneratorSavedSceneProbe()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(BootstrapValidator.ScenePath, OpenSceneMode.Single);
            var before = EditorSceneManager.GetSceneManagerSetup();
            var guid = AssetDatabase.AssetPathToGUID(BootstrapValidator.ScenePath);
            try
            {
                void Observe(string phase)
                {
                    var snapshot = BootstrapValidator.Capture(ProjectRoot);
                    Preview(scene => {
                        var marker = Find<TextMeshProUGUI>(scene).Single();
                        var input = Find<InputSystemUIInputModule>(scene).Single();
                        string Ref(Object value)
                        {
                            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string assetGuid, out long id), Is.True);
                            return assetGuid + ":" + id;
                        }
                        Debug.Log("CG_BOOT_PROBE_OBSERVE: " + phase + "; cameras=" + snapshot.CameraCount +
                            "; canvases=" + snapshot.CanvasCount + "; events=" + snapshot.EventSystemCount +
                            "; urp=" + snapshot.UrpReferencesValid + "; tmp=" + snapshot.TmpResourcesValid +
                            "; actions=" + snapshot.InputActionsValid + "; text=" + marker.text +
                            "; font=" + Ref(marker.font) + "; material=" + Ref(marker.fontSharedMaterial) +
                            "; atlas=" + Ref(marker.font.atlasTexture) + "; point=" + Ref(input.point) +
                            "; click=" + Ref(input.leftClick) + "; submit=" + Ref(input.submit) + "; cancel=" + Ref(input.cancel));
                    });
                }
                Observe("before");
                BootstrapProject.CreateBootstrapAssets();
                Observe("after");
                var after = EditorSceneManager.GetSceneManagerSetup();
                Assert.That(after.Select(x => (x.path, x.isLoaded, x.isActive)),
                    Is.EqualTo(before.Select(x => (x.path, x.isLoaded, x.isActive))));
                Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
                Assert.That(AssetDatabase.AssetPathToGUID(BootstrapValidator.ScenePath), Is.EqualTo(guid));
                Assert.That(BootstrapValidator.Validate(BootstrapValidator.Capture(ProjectRoot)).Errors, Is.Empty);
                Debug.Log("CG_BOOT_SAVED_SCENE_PROBE_PASS: scene setup and asset GUID retained; validator valid");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (setup.Length > 0 && setup.All(x => !string.IsNullOrEmpty(x.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
        private static string[] BuildArgs(string root, string output, string target = "StandaloneOSX") =>
            new[] { "-cgBuildTarget", target, "-cgBuildRoot", root, "-cgBuildOutput", output, "-cgGameplayScene", "Assets/ChooGuard/Scenes/MvpWorkspace.unity" };
        private static object InvokeBuild(string method, string[] args)
        {
            var helper = typeof(BuildBaseline).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(helper, Is.Not.Null);
            try { return helper.Invoke(null, new object[] { args }); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
        private static void InvokeBuildOutcome(BuildReceipt receipt, Func<UnityEditor.Build.Reporting.BuildResult?> build, Func<string[]> files)
        {
            var helper = typeof(BuildBaseline).GetMethod("RecordBuildOutcome", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(helper, Is.Not.Null);
            try { helper.Invoke(null, new object[] { receipt, build, files }); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
        [TestCase(UnityEditor.Build.Reporting.BuildResult.Failed)]
        [TestCase(UnityEditor.Build.Reporting.BuildResult.Cancelled)]
        public void SimulatedFailedBuildOutcome_RetainsReportedPartialFileAndThrows(UnityEditor.Build.Reporting.BuildResult result)
        {
            var root = BuildFixture();
            try
            {
                var partial = Path.Combine(root, "partial.bin");
                File.WriteAllText(partial, "partial output fixture");
                var hash = BuildBaseline.HashFile(partial);
                var receipt = new BuildReceipt();
                TestContext.WriteLine("SIMULATED result via private seam; real scratch file; not an actual failed Unity BuildReport.");
                var error = Assert.Throws<InvalidOperationException>(() => InvokeBuildOutcome(receipt, () => result,
                    () => new[] { partial, Path.Combine(root, "not-produced.bin") }));
                Assert.That(error.Message, Is.EqualTo("BuildReport=" + result));
                Assert.That(receipt.buildStatus, Is.EqualTo("FAILED"));
                Assert.That(receipt.runStatus, Is.EqualTo("NOT_RUN"));
                Assert.That(receipt.reason, Does.Contain("OBSERVED_REPORTED_FILES_ONLY"));
                Assert.That(receipt.outputs.Length, Is.EqualTo(1), "Returned failure must retain reported partial output");
                Assert.That(receipt.outputs[0].path, Is.EqualTo(partial));
                Assert.That(receipt.outputs[0].sha256, Is.EqualTo(hash));
                Assert.That(BuildBaseline.HashFile(partial), Is.EqualTo(hash));
            }
            finally { Directory.Delete(root, true); }
        }
        [TestCase("null-report", "UNOBSERVED")]
        [TestCase("build-throws", "UNOBSERVED")]
        [TestCase("files-throws", "INCOMPLETE")]
        [TestCase("hash-throws", "INCOMPLETE")]
        public void SimulatedBuildObservationFailure_ReportsInventoryLimitsAndThrows(string damage, string inventory)
        {
            var root = BuildFixture();
            try
            {
                var partial = Path.Combine(root, "partial.bin");
                var locked = Path.Combine(root, "locked.bin");
                File.WriteAllText(partial, "retained partial fixture");
                File.WriteAllText(locked, "unreadable while held exclusively");
                var receipt = new BuildReceipt();
                var filesCalled = false;
                Func<UnityEditor.Build.Reporting.BuildResult?> build = () =>
                {
                    if (damage == "build-throws") throw new IOException("simulated BuildPlayer exception");
                    return damage == "null-report" ? (UnityEditor.Build.Reporting.BuildResult?)null : UnityEditor.Build.Reporting.BuildResult.Succeeded;
                };
                Func<string[]> files = () =>
                {
                    filesCalled = true;
                    if (damage == "files-throws") throw new IOException("simulated GetFiles exception");
                    return new[] { partial, locked };
                };
                using (var hold = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    if (damage == "hash-throws") Assert.Throws<IOException>(() => BuildBaseline.HashFile(locked));
                    Assert.Catch(() => InvokeBuildOutcome(receipt, build, files));
                }
                Assert.That(receipt.buildStatus, Is.EqualTo("FAILED"));
                Assert.That(receipt.runStatus, Is.EqualTo("NOT_RUN"));
                Assert.That(receipt.reason, Does.Contain("Output inventory " + inventory));
                Assert.That(receipt.reason, Does.Contain("do not prove file absence"));
                Assert.That(filesCalled, Is.EqualTo(inventory == "INCOMPLETE"));
                Assert.That(receipt.outputs.Length, Is.EqualTo(damage == "hash-throws" ? 1 : 0));
                if (damage == "hash-throws")
                {
                    Assert.That(receipt.outputs[0].path, Is.EqualTo(partial));
                    Assert.That(receipt.outputs[0].sha256, Is.EqualTo(BuildBaseline.HashFile(partial)));
                }
                Assert.That(File.Exists(partial) && File.Exists(locked), Is.True);
            }
            finally { Directory.Delete(root, true); }
        }
        [Test]
        public void SimulatedSucceededBuildOutcome_HashesOnlyExistingReportedFiles()
        {
            var root = BuildFixture();
            try
            {
                var output = Path.Combine(root, "output.bin");
                File.WriteAllText(output, "success fixture");
                File.WriteAllText(Path.Combine(root, "unreported.bin"), "must not crawl");
                var receipt = new BuildReceipt();
                InvokeBuildOutcome(receipt, () => UnityEditor.Build.Reporting.BuildResult.Succeeded,
                    () => new[] { output, Path.Combine(root, "missing.bin") });
                Assert.That(receipt.buildStatus, Is.EqualTo("SUCCEEDED"));
                Assert.That(receipt.runStatus, Is.EqualTo("NOT_RUN"));
                Assert.That(receipt.reason, Does.Contain("OBSERVED_REPORTED_FILES_ONLY"));
                Assert.That(receipt.outputs.Length, Is.EqualTo(1));
                Assert.That(receipt.outputs[0].path, Is.EqualTo(output));
                Assert.That(receipt.outputs[0].sha256, Is.EqualTo(BuildBaseline.HashFile(output)));
            }
            finally { Directory.Delete(root, true); }
        }
        private static string BuildFixture()
        {
            var parent = BuildBaseline.Argument("-cgFixtureRoot");
            Assert.That(parent, Is.Not.Null.And.Not.Empty);
            var root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
        private static string[] ReceiptFiles() => Directory.GetFiles(Path.Combine(ProjectRoot,
            "docs/build/evidence/CS-BOOT.01.01"), "build-receipt.json", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
        private static void RejectBuild(string[] args)
        {
            var before = ReceiptFiles();
            var error = Assert.Catch(() => InvokeBuild("BuildFromArguments", args));
            Assert.That(error, Is.InstanceOf<ArgumentException>().Or.InstanceOf<IOException>());
            Assert.That(ReceiptFiles(), Is.EqualTo(before), "Invalid boundaries must not create receipts");
        }
        [TestCase("-cgBuildRoot", "missing")]
        [TestCase("-cgBuildRoot", "duplicate")]
        [TestCase("-cgBuildRoot", "no-value")]
        [TestCase("-cgBuildOutput", "missing")]
        [TestCase("-cgBuildOutput", "duplicate")]
        [TestCase("-cgBuildOutput", "no-value")]
        [TestCase("-cgBuildTarget", "missing")]
        [TestCase("-cgBuildTarget", "duplicate")]
        [TestCase("-cgBuildTarget", "no-value")]
        [TestCase("-cgGameplayScene", "missing")]
        [TestCase("-cgGameplayScene", "duplicate")]
        [TestCase("-cgGameplayScene", "no-value")]
        public void BuildArguments_RejectMissingDuplicateOrValuelessOption(string option, string damage)
        {
            var root = BuildFixture();
            try
            {
                var args = BuildArgs(root, Path.Combine(root, "new.app")).ToList();
                var index = args.IndexOf(option);
                if (damage == "missing") args.RemoveRange(index, 2);
                else if (damage == "duplicate") args.AddRange(new[] { option, args[index + 1] });
                else { args.RemoveRange(index, 2); args.Add(option); }
                RejectBuild(args.ToArray());
                Assert.That(Directory.GetFileSystemEntries(root), Is.Empty);
            }
            finally { Directory.Delete(root, true); }
        }
        [TestCase("relative-root")]
        [TestCase("missing-root")]
        [TestCase("relative-output")]
        [TestCase("missing-parent")]
        [TestCase("outside-output")]
        [TestCase("equal-output")]
        [TestCase("existing-file")]
        [TestCase("existing-directory")]
        [TestCase("project-equal")]
        [TestCase("project-ancestor")]
        [TestCase("project-descendant")]
        [TestCase("project-case-alias")]
        [TestCase("numeric-target")]
        [TestCase("wrong-case-target")]
        [TestCase("option-as-value")]
        public void BuildBoundaries_RejectUnsafePathsAndTargets(string damage)
        {
            var fixture = BuildFixture();
            try
            {
                var root = fixture;
                var output = Path.Combine(root, "new.app");
                var target = "StandaloneOSX";
                switch (damage)
                {
                    case "relative-root": root = "relative"; break;
                    case "missing-root": root = Path.Combine(root, "absent"); output = Path.Combine(root, "new.app"); break;
                    case "relative-output": output = "relative.app"; break;
                    case "missing-parent": output = Path.Combine(root, "absent/new.app"); break;
                    case "outside-output": output = root + "-sibling.app"; break;
                    case "equal-output": output = root; break;
                    case "existing-file": File.WriteAllText(output, "sentinel"); break;
                    case "existing-directory": Directory.CreateDirectory(output); break;
                    case "project-equal": root = ProjectRoot; output = Path.Combine(root, "new.app"); break;
                    case "project-ancestor": root = Path.GetDirectoryName(ProjectRoot); output = Path.Combine(root, "new.app"); break;
                    case "project-descendant": root = Path.Combine(ProjectRoot, "Assets"); output = Path.Combine(root, "new.app"); break;
                    case "project-case-alias": root = ProjectRoot.ToLowerInvariant(); output = Path.Combine(root, "new.app"); break;
                    case "numeric-target": target = ((int)BuildTarget.StandaloneOSX).ToString(); break;
                    case "wrong-case-target": target = "standaloneosx"; break;
                    case "option-as-value": root = "-cgBuildOutput"; break;
                }
                var entries = Directory.GetFileSystemEntries(fixture).OrderBy(x => x).ToArray();
                RejectBuild(BuildArgs(root, output, target));
                Assert.That(Directory.GetFileSystemEntries(fixture).OrderBy(x => x), Is.EqualTo(entries));
                if (damage == "existing-file") Assert.That(File.ReadAllText(output), Is.EqualTo("sentinel"));
            }
            finally { Directory.Delete(fixture, true); }
        }
        [TestCase("root-link")]
        [TestCase("parent-link")]
        [TestCase("leaf-link")]
        [TestCase("dangling-link")]
        public void BuildBoundaries_RejectSymbolicLinks(string kind)
        {
            // The caller creates real symlinks outside the project before this Unity process starts.
            var fixture = BuildBaseline.Argument("-cgBuildLinkFixture");
            Assert.That(fixture, Is.Not.Null.And.Not.Empty);
            var link = Path.Combine(fixture, kind);
            Assert.That(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint), Is.True);
            var root = kind == "root-link" ? link : fixture;
            var output = kind == "root-link" || kind == "parent-link" ? Path.Combine(link, "new.app") : link;
            RejectBuild(BuildArgs(root, output));
            Assert.That(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint), Is.True);
        }
        [Test]
        public void BuildBoundaries_AcceptNewExplicitScratchOutputWithoutCreatingIt()
        {
            var root = BuildFixture();
            try
            {
                var before = ReceiptFiles();
                Assert.That(InvokeBuild("ValidateBuildArguments", BuildArgs(root, Path.Combine(root, "new.app"))), Is.Not.Null);
                Assert.That(Directory.GetFileSystemEntries(root), Is.Empty);
                Assert.That(ReceiptFiles(), Is.EqualTo(before));
            }
            finally { Directory.Delete(root, true); }
        }
        [Test]
        [UnityEngine.TestTools.UnityPlatform(RuntimePlatform.OSXEditor)]
        public void WindowsBuild_OnMacRecordsNotRunBeforeValidatorWithoutOutputOrTargetFallback()
        {
            Assert.That(global::UnityEngine.Application.platform, Is.EqualTo(RuntimePlatform.OSXEditor), "This local no-Windows-host oracle requires the observed Mac host");
            var root = BuildFixture();
            var previous = GraphicsSettings.defaultRenderPipeline;
            var target = EditorUserBuildSettings.activeBuildTarget;
            try
            {
                GraphicsSettings.defaultRenderPipeline = null;
                Assert.That(BootstrapValidator.HasOwnedUrpReferences(), Is.False, "Static validator would reject this in-memory fixture");
                var before = ReceiptFiles();
                InvokeBuild("BuildFromArguments", BuildArgs(root, Path.Combine(root, "new.exe"), "StandaloneWindows64"));
                var added = ReceiptFiles().Except(before).ToArray();
                Assert.That(added.Length, Is.EqualTo(1));
                var receipt = JsonUtility.FromJson<BuildReceipt>(File.ReadAllText(added[0]));
                Assert.That(receipt.requestedTarget, Is.EqualTo("StandaloneWindows64"));
                Assert.That(receipt.buildStatus, Is.EqualTo("NOT_RUN"));
                Assert.That(receipt.runStatus, Is.EqualTo("NOT_RUN"));
                Assert.That(receipt.outputs, Is.Empty);
                Assert.That(receipt.reason, Does.Contain("no validator, build, target fallback"));
                Assert.That(Directory.GetFileSystemEntries(root), Is.Empty);
                Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(target));
                TestContext.WriteLine("Unsupported-host receipt=" + added[0]);
            }
            finally { GraphicsSettings.defaultRenderPipeline = previous; Directory.Delete(root, true); }
        }
        [Test]
        public void DuplicateEventSystem_IsRejected()
        {
            var snapshot = BootstrapValidator.Capture(ProjectRoot);
            snapshot.EventSystemCount = 2;
            Assert.That(BootstrapValidator.Validate(snapshot).Errors, Does.Contain("EVENT_SYSTEM_COUNT"));
        }
        [Test]
        public void DuplicateRealEventSystems_AreObservedAndRejected()
        {
            Preview(scene => {
                var duplicate = new GameObject("Duplicate EventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(duplicate, scene);
                var snapshot = BootstrapValidator.Capture(ProjectRoot);
                BootstrapValidator.ObserveScene(scene, snapshot);
                Assert.That(snapshot.EventSystemCount, Is.EqualTo(2));
                Assert.That(BootstrapValidator.Validate(snapshot).Errors, Does.Contain("EVENT_SYSTEM_COUNT"));
            });
        }
        [Test]
        public void LocalDll_IsObservedFromForeignFilesystemFixture()
        {
            var parent = BuildBaseline.Argument("-cgFixtureRoot");
            Assert.That(parent, Is.Not.Null.And.Not.Empty, "Provide a new local scratch fixture root");
            var root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets/Plugins"));
            try
            {
                File.WriteAllBytes(Path.Combine(root, "Assets/Plugins/LocalOnly.dll"), new byte[] { 0 });
                var snapshot = BootstrapValidator.Capture(root);
                Assert.That(snapshot.LocalPluginPaths, Is.EqualTo(new[] { "Assets/Plugins/LocalOnly.dll" }));
                Assert.That(BootstrapValidator.Validate(snapshot).Errors, Does.Contain("LOCAL_PLUGIN_UNDECLARED"));
                Assert.That(snapshot.CameraCount, Is.Zero);
            }
            finally { Directory.Delete(root, true); }
        }
        [TestCase("intact")]
        [TestCase("tampered")]
        [TestCase("undeclared")]
        [TestCase("version-drift")]
        public void ManagedPackages_RequireDeclaredPathsAndExactBytes(string change)
        {
            var root = BuildFixture();
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Assets"));
                Directory.CreateDirectory(Path.Combine(root, "workers"));
                foreach (var relative in new[] { "Assets/NuGet.config", "Assets/packages.config", "workers/nuget-managed.lock.json" })
                    File.Copy(Path.Combine(ProjectRoot, relative), Path.Combine(root, relative));
                foreach (var name in new[] { "Core", "Detour", "Recast" })
                {
                    var relative = "Assets/Packages/DotRecast." + name + ".2026.3.1/lib/netstandard2.1/DotRecast." + name + ".dll";
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, relative)));
                    File.Copy(Path.Combine(ProjectRoot, relative), Path.Combine(root, relative));
                }
                var core = Path.Combine(root, "Assets/Packages/DotRecast.Core.2026.3.1/lib/netstandard2.1/DotRecast.Core.dll");
                if (change == "tampered") File.WriteAllBytes(core, new byte[] { 0 });
                if (change == "undeclared") File.Copy(core, Path.Combine(root, "Assets/Packages/Unreviewed.dll"));
                if (change == "version-drift")
                {
                    var config = Path.Combine(root, "Assets/packages.config");
                    File.WriteAllText(config, File.ReadAllText(config).Replace("2026.3.1", "2026.3.2"));
                }
                var snapshot = BootstrapValidator.Capture(root);
                if (change == "intact")
                {
                    Assert.That(snapshot.ManagedDependencyErrors, Is.Empty);
                    Assert.That(snapshot.LocalPluginPaths, Is.Empty);
                }
                else
                {
                    Assert.That(BootstrapValidator.Validate(snapshot).Errors, Does.Contain("LOCAL_PLUGIN_UNDECLARED"));
                    if (change != "undeclared")
                        Assert.That(BootstrapValidator.Validate(snapshot).Errors, Does.Contain("MANAGED_DEPENDENCY_INVALID"));
                }
            }
            finally { Directory.Delete(root, true); }
        }
        [Test]
        public void RequiredPackages_MatchRegistryAndMissingPackageIsRejected()
        {
            var snapshot = BootstrapValidator.Capture(ProjectRoot);
            var expected = BootstrapValidator.RequiredPackages.Except(PackageInfo.GetAllRegisteredPackages().Select(x => x.name)).OrderBy(x => x, StringComparer.Ordinal);
            Assert.That(snapshot.MissingRequiredPackages, Is.EqualTo(expected));
            Assert.That(snapshot.MissingRequiredPackages, Is.Empty);
            snapshot.MissingRequiredPackages = new[] { "com.unity.inputsystem" };
            Assert.That(BootstrapValidator.Validate(snapshot).Errors, Does.Contain("REQUIRED_PACKAGE_MISSING"));
        }
        [TestCase("font", "TMP_RESOURCE_MISSING")]
        [TestCase("actions", "INPUT_ACTIONS_MISSING")]
        public void DamagedSceneReference_IsRejected(string field, string error)
        {
            if (field == "font")
            {
                var sceneHash = BuildBaseline.HashFile(BootstrapValidator.ScenePath);
                var settingsHash = BuildBaseline.HashFile(SettingsPath);
                // Capture the normal submitted scene before changing the in-memory TMP default.
                var snapshot = BootstrapValidator.Capture(ProjectRoot);
                try
                {
                    Preview(scene => {
                        var marker = Find<TextMeshProUGUI>(scene).Single();
                        var ownedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                        var originalDefault = TMP_Settings.defaultFontAsset;
                        Assert.That(ownedFont, Is.Not.Null);
                        Assert.That(marker.font, Is.EqualTo(ownedFont));
                        Assert.That(originalDefault, Is.EqualTo(ownedFont));
                        var ownedMaterial = ownedFont.material;
                        string Id(Object value) => value == null ? "null" : value.GetInstanceID().ToString();
                        string ObserveReferences(string phase)
                        {
                            var raw = Required(new SerializedObject(marker), "m_fontAsset").objectReferenceValue;
                            var observation = phase + ": rawFont=" + Id(raw) + "; markerFont=" + Id(marker.font) +
                                "; defaultFont=" + Id(TMP_Settings.defaultFontAsset) + "; sharedMaterial=" + Id(marker.fontSharedMaterial) +
                                "; ownedFont=" + Id(ownedFont) + "; ownedMaterial=" + Id(ownedMaterial);
                            TestContext.WriteLine(observation);
                            return observation;
                        }
                        try
                        {
                            // With a valid default, null is healed by TMP and is not missing-resource damage.
                            // Remove only the in-memory fallback to establish the effective-resource counterexample.
                            TMP_Settings.defaultFontAsset = null;
                            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                                "The LiberationSans SDF Font Asset was not found. There is no Font Asset assigned to " + marker.gameObject.name + ".");
                            marker.font = null;
                            var afterMutation = ObserveReferences("after-mutation");
                            Assert.That(marker.font, Is.Null, afterMutation);
                            var beforeObserve = ObserveReferences("before-observe");
                            Assert.That(marker.font, Is.Null, beforeObserve);
                            Assert.That(TMP_Settings.defaultFontAsset, Is.Null, beforeObserve);
                            BootstrapValidator.ObserveScene(scene, snapshot);
                            Assert.That(BootstrapValidator.Validate(snapshot).Errors, Does.Contain(error), beforeObserve);
                        }
                        finally
                        {
                            TMP_Settings.defaultFontAsset = originalDefault;
                            marker.font = ownedFont;
                            marker.fontSharedMaterial = ownedMaterial;
                        }
                    });
                }
                finally
                {
                    Assert.That(BuildBaseline.HashFile(BootstrapValidator.ScenePath), Is.EqualTo(sceneHash));
                    Assert.That(BuildBaseline.HashFile(SettingsPath), Is.EqualTo(settingsHash));
                }
                return;
            }
            Preview(scene => {
                Find<InputSystemUIInputModule>(scene).Single().point = null;
                var snapshot = BootstrapValidator.Capture(ProjectRoot);
                BootstrapValidator.ObserveScene(scene, snapshot);
                Assert.That(BootstrapValidator.Validate(snapshot).Errors, Does.Contain(error));
            });
        }
        [Test]
        public void DamagedUrpReference_IsRejectedAndRestored()
        {
            var previous = GraphicsSettings.defaultRenderPipeline;
            try
            {
                GraphicsSettings.defaultRenderPipeline = null;
                Assert.That(BootstrapValidator.Validate(BootstrapValidator.Capture(ProjectRoot)).Errors, Does.Contain("URP_REFERENCE_MISSING"));
            }
            finally { GraphicsSettings.defaultRenderPipeline = previous; }
            Assert.That(BootstrapValidator.HasOwnedUrpReferences(), Is.True);
        }
        [Test]
        public void OwnedUrpGlobal_HasClosedVolumeDependenciesAndNoExcludedDefaults()
        {
            Assert.That(BootstrapValidator.HasOwnedUrpReferences(), Is.True);
            var global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(BootstrapProject.GlobalPath);
            var volume = BootstrapProject.DefaultVolumeSettings(global).volumeProfile;
            Assert.That(AssetDatabase.IsSubAsset(volume), Is.True);
            // URP's build preprocessor adds hidden persistent objects for required overrides.
            // On this Editor IsSubAsset returns false for those hidden objects; verify actual
            // same-file identity instead of that Project-window classification.
            var embedded = AssetDatabase.LoadAllAssetsAtPath(BootstrapProject.GlobalPath);
            var globalGuid = AssetDatabase.AssetPathToGUID(BootstrapProject.GlobalPath);
            var componentIds = new System.Collections.Generic.HashSet<long>();
            foreach (var component in volume.components)
            {
                Assert.That(component, Is.Not.Null);
                Assert.That(EditorUtility.IsPersistent(component), Is.True);
                Assert.That(AssetDatabase.IsMainAsset(component), Is.False);
                Assert.That(AssetDatabase.GetAssetPath(component), Is.EqualTo(BootstrapProject.GlobalPath));
                Assert.That(embedded, Does.Contain(component));
                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out string guid, out long id), Is.True);
                Assert.That(guid, Is.EqualTo(globalGuid));
                Assert.That(id, Is.Not.Zero);
                Assert.That(componentIds.Add(id), Is.True, "Each embedded component has its own local identity");
            }
            foreach (var path in new[] { "Assets/UniversalRenderPipelineGlobalSettings.asset", "Assets/DefaultVolumeProfile.asset" })
            {
                Assert.That(File.Exists(path), Is.False);
                Assert.That(File.Exists(path + ".meta"), Is.False);
            }
        }
        [Test]
        public void ValidationErrors_AreSortedAndDeduplicated()
        {
            var errors = new BootstrapValidationReport(new[] { "Z", "A", "Z" }).Errors;
            Assert.That(errors, Is.EqualTo(new[] { "A", "Z" }));
            Assert.That(BootstrapValidator.Validate(new BootstrapSnapshot()).IsValid, Is.False);
        }
        private static bool IsRawFontPath(string path) =>
            new[] { ".ttf", ".otf", ".ttc" }.Contains(Path.GetExtension(path).ToLowerInvariant());

        [TestCase("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf")]
        [TestCase("Assets/Other/Font.TTF")]
        [TestCase("Packages/example/Font.ttc")]
        public void BootstrapRawFontDetection_RejectsAnyLocationInDependencyClosure(string path)
        {
            Assert.That(new[] { FontPath, path }.Where(IsRawFontPath), Is.EqualTo(new[] { path }));
            Assert.That(IsRawFontPath(FontPath), Is.False);
        }

        [Test]
        public void TmpStaticWhitelist_HasDetachedSourcesAndClosedDependencies()
        {
            foreach (var path in Whitelist) Assert.That(File.Exists(path), Is.True, path);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath);
            Assert.That(font, Is.Not.Null); Assert.That(settings, Is.Not.Null);
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(font.fallbackFontAssetTable, Is.Empty);
            var serializedFont = new SerializedObject(font);
            foreach (var name in new[] { "m_SourceFontFileGUID", "m_SourceFontFilePath" })
                Assert.That(Required(serializedFont, name).stringValue, Is.Null.Or.Empty, name);
            Assert.That(Required(serializedFont, "m_SourceFontFile").objectReferenceValue, Is.Null);
            var editorRef = typeof(TMP_FontAsset).GetField("m_SourceFontFile_EditorRef", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(editorRef, Is.Not.Null); Assert.That(editorRef.GetValue(font), Is.Null);
            foreach (var name in new[] { "sourceFontFileName", "sourceFontFileGUID", "referencedFontAssetGUID", "referencedTextAssetGUID" })
                Assert.That(Required(serializedFont, "m_CreationSettings." + name).stringValue, Is.Null.Or.Empty, name);
            var sf = new SerializedObject(settings);
            Assert.That(Required(sf, "m_defaultFontAsset").objectReferenceValue, Is.EqualTo(font));
            foreach (var name in new[] { "m_fallbackFontAssets", "m_EmojiFallbackTextAssets" }) Assert.That(Required(sf, name).arraySize, Is.Zero, name);
            foreach (var name in new[] { "m_defaultSpriteAsset", "m_defaultStyleSheet" }) Assert.That(Required(sf, name).objectReferenceValue, Is.Null, name);
            foreach (var name in new[] { "m_defaultFontAssetPath", "m_defaultSpriteAssetPath", "m_defaultColorGradientPresetsPath", "m_StyleSheetsResourcePath" }) Assert.That(Required(sf, name).stringValue, Is.Null.Or.Empty, name);
            Assert.That(Required(sf, "m_enableEmojiSupport").boolValue, Is.False);
            Assert.That(AssetDatabase.GetAssetPath(Required(sf, "m_leadingCharacters").objectReferenceValue), Is.EqualTo(Whitelist[4]));
            Assert.That(AssetDatabase.GetAssetPath(Required(sf, "m_followingCharacters").objectReferenceValue), Is.EqualTo(Whitelist[5]));
            var assets = AssetDatabase.LoadAllAssetsAtPath(FontPath);
            Assert.That(assets.OfType<TMP_FontAsset>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<Material>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<Texture2D>().Count(), Is.EqualTo(1));
            Assert.That(assets.Length, Is.EqualTo(3));
            Assert.That(assets.All(x => x.name.StartsWith("ChooGuard Bootstrap SDF", StringComparison.Ordinal)), Is.True);
            Assert.That(font.faceInfo.familyName, Is.EqualTo("ChooGuard Bootstrap SDF"));
            Assert.That(font.material.shader.name, Is.EqualTo("TextMeshPro/Mobile/Distance Field"));
            Assert.That(AssetDatabase.GetAssetPath(font.material.shader), Is.EqualTo(Whitelist[2]));
            Assert.That(font.material.mainTexture, Is.EqualTo(font.atlasTextures.Single()));
            Assert.That(AssetDatabase.GetAssetPath(font.material), Is.EqualTo(FontPath));
            Assert.That(AssetDatabase.GetAssetPath(font.atlasTextures.Single()), Is.EqualTo(FontPath));
            foreach (var character in "CHOOGuard bootstrap".Distinct()) Assert.That(font.HasCharacter(character, false, false), Is.True);
            var dependencies = AssetDatabase.GetDependencies(Whitelist, true);
            Assert.That(dependencies.Where(x => x.StartsWith("Assets/", StringComparison.Ordinal) && !Whitelist.Contains(x)), Is.Empty);
            Assert.That(dependencies.Where(x => !x.StartsWith("Assets/", StringComparison.Ordinal) && !x.StartsWith("Packages/", StringComparison.Ordinal)), Is.Empty);
            // Bootstrap owns a detached static font, not every local-development font in the project.
            // Inspect the actual recursive scene/settings closure regardless of where a raw font lives.
            // This is not a release-build inventory: raw fonts must still be excluded from distribution.
            var bootstrapDependencies = AssetDatabase.GetDependencies(
                Whitelist.Concat(new[] { BootstrapValidator.ScenePath }).ToArray(), true);
            Assert.That(bootstrapDependencies.Where(IsRawFontPath), Is.Empty, "Bootstrap must not depend on a raw font");
            var files = new[] { "Assets/ChooGuard/Settings/TMP", "Assets/ChooGuard/Settings/Resources", "Assets/ChooGuard/ThirdPartyNotices" }.SelectMany(x => Directory.GetFiles(x, "*", SearchOption.AllDirectories)).Where(x => !x.EndsWith(".meta", StringComparison.Ordinal));
            Assert.That(files.Where(IsRawFontPath), Is.Empty, "Bootstrap resource roots must remain source-free");
            Assert.That(files, Is.EquivalentTo(Whitelist));
        }
    }
}
#endif
