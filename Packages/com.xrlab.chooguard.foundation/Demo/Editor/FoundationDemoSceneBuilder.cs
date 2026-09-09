using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Demo.Editor
{
    /// <summary>Repeatable reference-informed art on a provisional playable station footprint.</summary>
    public static class FoundationDemoSceneBuilder
    {
        public const string DefaultGeneratedRoot = "Assets/CHOOguardGenerated/FoundationDemo";
        public const string DefaultScenePath = DefaultGeneratedRoot + "/FoundationDemo.unity";
        public const string DesktopOutputRoot = "Builds/FoundationDesktop";
        private const string OwnerId = "com.xrlab.chooguard.foundation.playable-demo";
        private const string MarkerName = "generated-owner.json";
        private static readonly string[] MaterialNames =
            { "Floor", "Wall", "Metal", "Blue", "Red", "Yellow", "Green", "Screen", "White", "Orange",
                "Stainless", "Stone", "Glass", "Diffuser", "Roof", "Rubber", "Ceiling", "Wood", "Fabric", "ClearGlass" };
        private static readonly string[] LegacyMaterialNames =
            { "Floor", "Wall", "Metal", "Blue", "Red", "Yellow", "Green", "Screen", "White", "Orange" };
        private static readonly string[] PrefabNames = { "SituationPanel", "AlarmSimulator", "RadioConsole", "DirectionSign", "AccessGate", "Bench", "Pillar", "InformationKiosk", "HazardIndicator", "AssemblySign", "Player", "Evacuee", "RouteConsole", "RallyPoint", "AssemblyRegister", "DoorFrame", "Luggage" };
        private static readonly Color[] MaterialColors =
        {
            new Color(.90f, .89f, .87f), new Color(.76f, .77f, .75f), new Color(.10f, .13f, .15f),
            new Color(.09f, .27f, .40f), new Color(.68f, .10f, .08f), new Color(.88f, .64f, .15f),
            new Color(.10f, .48f, .30f), new Color(.08f, .58f, .65f), new Color(.86f, .89f, .87f),
            new Color(1f, .36f, .05f), new Color(.80f,.82f,.83f), new Color(.88f,.87f,.84f),
            new Color(.84f,.88f,.89f), new Color(.93f,.95f,.90f), new Color(.88f,.90f,.88f), new Color(.075f,.08f,.085f), new Color(.50f,.53f,.54f), new Color(.67f,.39f,.17f), new Color(.10f,.13f,.18f), new Color(.8f,.9f,.95f,.18f)
        };

        [Serializable]
        private sealed class Ownership
        {
            public string generator;
            public int version;
            public string[] ownedRelativePaths;
        }

        public static void GenerateBatch() { Build(); }

        [MenuItem("CHOOguard/Foundation/Build Playable Demo")]
        public static void BuildPlayableDemoMenu()
        {
            try
            {
                var generated = Build();
                // Open just the generated scene for Play Mode: unrelated clean scenes remain on disk.
                var scene = EditorSceneManager.OpenScene(generated.path, OpenSceneMode.Single);
                Selection.activeGameObject = scene.GetRootGameObjects().Single();
                Debug.Log("CHOOguard synthetic demo generated: " + scene.path +
                    ". Enter Play Mode, choose a role, and acknowledge the provisional briefing.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("CHOOguard/Foundation/Build Desktop Player")]
        public static void BuildDesktopPlayerMenu()
        {
            try { BuildDesktopPlayerBatch(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        // Batch entry point deliberately propagates failures to Unity's command-line exit status.
        public static void BuildDesktopPlayerBatch()
        {
            RequireSavedScenes();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows Standalone build support is not installed. " +
                    "Use the configured school PC; this entry point does not install modules.");
            RequireOwnedBuildDirectory();
            var scene = Build();
            Directory.CreateDirectory(DesktopOutputRoot);
            File.WriteAllText(DesktopOutputRoot + "/" + MarkerName,
                JsonUtility.ToJson(new Ownership { generator = OwnerId + ".windows", version = 1,
                    ownedRelativePaths = new[] { "ChooGuardFoundation.exe" } }, true));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene.path },
                locationPathName = DesktopOutputRoot + "/ChooGuardFoundation.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Desktop build did not succeed: " + report.summary.result);
            Debug.Log("CHOOguard Windows desktop player built: " +
                Path.GetFullPath(DesktopOutputRoot + "/ChooGuardFoundation.exe") +
                ". This result still needs a standalone playthrough.");
        }

        [MenuItem("CHOOguard/Foundation/Build Mac Player")]
        public static void BuildMacPlayerMenu()
        {
            try { BuildMacPlayerBatch(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        // Batch entry point deliberately propagates failures to Unity's command-line exit status.
        public static void BuildMacPlayerBatch()
        {
            const string output = "Builds/FoundationMac";
            RequireSavedScenes();
            RejectLinkedParents(output);
            if (File.Exists(output)) throw new InvalidOperationException("Mac output collides with a file.");
            if (Directory.Exists(output))
            {
                RejectLinkedTree(new DirectoryInfo(output));
                var markerPath = output + "/" + MarkerName;
                if (!File.Exists(markerPath)) throw new InvalidOperationException("Mac output is not owned by the demo builder.");
                var owner = JsonUtility.FromJson<Ownership>(File.ReadAllText(markerPath));
                if (owner == null || owner.generator != OwnerId + ".macos" || owner.version != 1)
                    throw new InvalidOperationException("Mac output is not owned by this demo builder.");
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new InvalidOperationException("Installed Editor does not have Mac Standalone support.");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x)
                throw new InvalidOperationException("This lightweight Mac player uses installed Mono support; select Mono in this host.");
            var scene = Build();
            PlayerSettings.companyName = "XRLab";
            PlayerSettings.productName = "CHOOguard Foundation";
            PlayerSettings.defaultScreenWidth = 2560;
            PlayerSettings.defaultScreenHeight = 1600;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory(output);
            File.WriteAllText(output + "/" + MarkerName, JsonUtility.ToJson(new Ownership
                { generator = OwnerId + ".macos", version = 1, ownedRelativePaths = new[] { "ChooGuardFoundation.app" } }, true));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene.path }, locationPathName = output + "/ChooGuardFoundation.app",
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Mac player build failed: " + report.summary.result);
            var root = scene.GetRootGameObjects().Single();
            var renderers = root.GetComponentsInChildren<Renderer>(true).Length;
            var triangles = root.GetComponentsInChildren<MeshFilter>(true).Sum(x => x.sharedMesh.triangles.Length / 3);
            File.WriteAllText(output + "/build-summary.json", "{\"platform\":\"macOS\",\"editor\":\"" + Application.unityVersion +
                "\",\"renderers\":" + renderers + ",\"triangles\":" + triangles + ",\"buildBytes\":" + report.summary.totalSize + "}");
            Debug.Log("CHOOguard Mac player built: " + Path.GetFullPath(output + "/ChooGuardFoundation.app"));
        }

        public static Scene Build(string generatedRoot = DefaultGeneratedRoot)
        {
            ValidateGeneratedRoot(generatedRoot);
            RequireSavedScenes();
            RequireOwnedGeneratedDirectory(generatedRoot);
            var sourcePath = ResolveCanonicalScenarioPath();
            var linkerPath = Path.Combine(PackageInfo.FindForAssembly(typeof(TrainingSession).Assembly).resolvedPath,
                "Demo", "Runtime", "link.xml");
            if (!File.Exists(linkerPath)) throw new FileNotFoundException("Optional input preservation file missing.", linkerPath);
            var scenarioText = File.ReadAllText(sourcePath);
            var profile = JsonUtility.FromJson<ScenarioProfile>(scenarioText);
            ScenarioValidation.Validate(profile);
            if (!profile.roles.Select(role => role.targetAnchorId).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(Enumerable.Range(1, 5).Select(i => "anchor-" + i.ToString("00"))))
                throw new InvalidOperationException("This map requires canonical anchors anchor-01 through anchor-05.");
            var shader = FindCompatibleShader();
            var previousActive = SceneManager.GetActiveScene();
            var scenePath = generatedRoot + "/FoundationDemo.unity";
            var scene = default(Scene);
            try
            {
                EnsureFolder(generatedRoot);
                EnsureFolder(generatedRoot + "/Materials");
                WriteOwnership(generatedRoot);
                FoundationSurfaceMaterials.CreateTextures(generatedRoot);
                var materials = CreateMaterials(generatedRoot, shader);
                var scenarioPath = generatedRoot + "/foundation-demo.json";
                File.Copy(sourcePath, scenarioPath, true);
                AssetDatabase.ImportAsset(scenarioPath, ImportAssetOptions.ForceSynchronousImport);
                // Unity does not apply package-local link.xml files; keep the copy under Assets.
                File.Copy(linkerPath, generatedRoot + "/link.xml", true);
                AssetDatabase.ImportAsset(generatedRoot + "/link.xml", ImportAssetOptions.ForceSynchronousImport);
                var scenario = AssetDatabase.LoadAssetAtPath<TextAsset>(scenarioPath);
                if (scenario == null) throw new InvalidOperationException("Scenario JSON did not import as a TextAsset.");

                // Unity cannot add a scene beside its pristine, never-saved startup scene.
                // RequireSavedScenes already rejects dirty user work; replace only this sole clean scene.
                var replaceUntitled = SceneManager.sceneCount == 1 &&
                    string.IsNullOrEmpty(SceneManager.GetSceneAt(0).path);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("FoundationDemo").transform;
                var environment = Group("Environment", root);
                BuildEnvironment(environment, materials);
                BuildProps(Group("Props", root), materials);
                var targets = BuildInteractables(Group("Interactables", root), materials);
                var markers = Group("Markers", root);
                var spawn = Group("Spawn", markers);
                spawn.position = new Vector3(0, .1f, -4);
                var assembly = Group("AssemblyPoint", markers);
                assembly.position = new Vector3(0, 0, 17);
                var guide = Group("AssemblyGuide", markers);
                Primitive("AssemblyZone", guide, PrimitiveType.Cylinder, new Vector3(0, .015f, 17),
                    new Vector3(DemoGameController.DefaultAssemblyRadius * 2, .01f, DemoGameController.DefaultAssemblyRadius * 2), materials[6], false);
                for (var z = -2; z <= 15; z += 2)
                    Arrow("RouteArrow_" + z, guide, new Vector3(0, .022f, z), materials[6]);
                var player = CreatePlayer(root, spawn);
                var lighting = Group("Lighting", root);
                var light = Group("Sun", lighting).gameObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, .94f, .84f);
                light.intensity = 1.1f;
                light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.Euler(50, -35, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f, .69f, .74f);
                var game = Group("Game", root).gameObject.AddComponent<DemoGameController>();
                game.Configure(scenario, player, spawn, targets, assembly);
                game.ConfigureAssemblyGuide(guide);
                var beacon = Group("SyntheticIncidentBeacon", lighting).gameObject.AddComponent<Light>();
                beacon.transform.localPosition = new Vector3(4.5f, 2.1f, 5.5f);
                beacon.type = LightType.Point;
                beacon.color = new Color(1f, .3f, .05f);
                beacon.range = 10;
                beacon.intensity = 1.2f;
                beacon.shadows = LightShadows.None;
                game.ConfigureIncidentCue(beacon);
                beacon.transform.SetParent(root.Find("Props/SyntheticHazardIndicator"), true);
                var indicator = root.Find("Props/SyntheticHazardIndicator/OrangeBeacon").GetComponent<Renderer>();
                indicator.gameObject.AddComponent<DemoIncidentVisual>().Configure(beacon, indicator);
                FoundationDemoDetailBuilder.Build(root, materials);
                FoundationBlenderAssets.Apply(root,materials);
                var drillPath=generatedRoot+"/evacuation-drills.json";
                File.Copy(Path.Combine(Path.GetDirectoryName(sourcePath),"evacuation-drills.json"),drillPath,true);
                AssetDatabase.ImportAsset(drillPath,ImportAssetOptions.ForceSynchronousImport);
                FoundationEvacuationBuilder.Build(root,materials,AssetDatabase.LoadAssetAtPath<TextAsset>(drillPath));
                var worldPath=generatedRoot+"/station-twin-profile.json";
                File.Copy(Path.Combine(Path.GetDirectoryName(sourcePath),"..","world","station-twin-profile.json"),worldPath,true);
                AssetDatabase.ImportAsset(worldPath,ImportAssetOptions.ForceSynchronousImport);
                StationWorldBuilder.Build(root,materials,AssetDatabase.LoadAssetAtPath<TextAsset>(worldPath),scenario);
                FoundationReferenceArchitecture.Build(root,materials,generatedRoot);
                ExportPrefabs(root, generatedRoot);
                Physics.SyncTransforms();

                // Close only a clean previous generation, after the replacement has been built.
                for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    var existing = SceneManager.GetSceneAt(i);
                    if (existing != scene && existing.path == scenePath)
                        EditorSceneManager.CloseScene(existing, true);
                }
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                    throw new InvalidOperationException("Unity could not save the generated demo scene.");
                return scene;
            }
            catch
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                throw;
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
        }

        private static void ExportPrefabs(Transform root, string generatedRoot)
        {
            EnsureFolder(generatedRoot + "/Prefabs");
            var paths = new[] {
                "Interactables/SituationPanel_01", "Interactables/AlarmSimulator_02", "Interactables/RadioConsole_03",
                "Interactables/DirectionSign_04", "Interactables/AccessGate_05", "Props/Bench_-1", "Props/Pillar_-1_-2",
                "Props/InformationKiosk", "Props/SyntheticHazardIndicator", "Props/AssemblyDirectionSign", "Player", "Evacuees/Evacuee_0", "SharedObjects/route-console", "SharedObjects/rally-west", "SharedObjects/assembly-register", "Environment/Blender_DoorFrame", "Environment/Blender_Luggage" };
            for (var i = 0; i < paths.Length; i++)
            {
                var source = root.Find(paths[i]);
                if (source == null) throw new InvalidOperationException("Missing prefab source: " + paths[i]);
                if (PrefabUtility.SaveAsPrefabAsset(source.gameObject, generatedRoot + "/Prefabs/" + PrefabNames[i] + ".prefab") == null)
                    throw new InvalidOperationException("Could not save generated prefab: " + PrefabNames[i]);
            }
        }

        private static void BuildEnvironment(Transform parent, Material[] materials)
        {
            Box("ConcourseFloor", parent, new Vector3(0, -.15f, .5f), new Vector3(16, .3f, 15), materials[0]);
            Box("CorridorFloor", parent, new Vector3(0, -.15f, 11), new Vector3(6, .3f, 6), materials[0]);
            Box("AssemblyFloor", parent, new Vector3(0, -.15f, 18), new Vector3(12, .3f, 8), materials[0]);
            Box("ConcourseWestWall", parent, new Vector3(-8.15f, 1.6f, .5f), new Vector3(.3f, 3.2f, 15), materials[1]);
            Box("ConcourseEastWall", parent, new Vector3(8.15f, 1.6f, .5f), new Vector3(.3f, 3.2f, 15), materials[1]);
            Box("ConcourseBackWall", parent, new Vector3(0, 1.6f, -7.15f), new Vector3(16.6f, 3.2f, .3f), materials[1]);
            foreach (var sign in new[] { -1, 1 })
            {
                Box("PassageWing_" + sign, parent, new Vector3(sign * 5.65f, 1.6f, 8.15f),
                    new Vector3(5, 3.2f, .3f), materials[1]);
                Box("CorridorWall_" + sign, parent, new Vector3(sign * 3.15f, 1.6f, 11),
                    new Vector3(.3f, 3.2f, 6), materials[1]);
                Box("AssemblyWing_" + sign, parent, new Vector3(sign * 4.65f, 1.6f, 13.85f),
                    new Vector3(3, 3.2f, .3f), materials[1]);
                Box("AssemblySideWall_" + sign, parent, new Vector3(sign * 6.15f, 1.6f, 18),
                    new Vector3(.3f, 3.2f, 8), materials[1]);
            }
            Box("AssemblyEndWall", parent, new Vector3(0, 1.6f, 22.15f), new Vector3(12.6f, 3.2f, .3f), materials[1]);
            Box("PassageLintel", parent, new Vector3(0, 3.05f, 8), new Vector3(6, .3f, .35f), materials[3]);
            Box("AssemblyLintel", parent, new Vector3(0, 3.05f, 14), new Vector3(6, .3f, .35f), materials[6]);
        }

        private static void BuildProps(Transform parent, Material[] materials)
        {
            foreach (var side in new[] { -1, 1 })
            {
                var bench = Group("Bench_" + side, parent);
                bench.localPosition = new Vector3(side * 5.6f, 0, 2.5f);
                Box("Seat", bench, new Vector3(0, .55f, 0), new Vector3(2.4f, .18f, .7f), materials[3]);
                Box("Back", bench, new Vector3(0, .94f, .28f), new Vector3(2.4f, .65f, .15f), materials[3]);
                foreach (var leg in new[] { -1, 1 })
                    Box("Leg_" + leg, bench, new Vector3(leg * .9f, .25f, 0), new Vector3(.15f, .5f, .55f), materials[2]);
                foreach (var z in new[] { -2f, 6f })
                    Primitive("Pillar_" + side + "_" + z, parent, PrimitiveType.Cylinder,
                        new Vector3(side * 6.6f, 1.6f, z), new Vector3(.65f, 1.6f, .65f), materials[1], true);
            }
            var kiosk = Group("InformationKiosk", parent);
            kiosk.localPosition = new Vector3(-5.8f, 0, -4.5f);
            Box("Base", kiosk, new Vector3(0, .55f, 0), new Vector3(1.4f, 1.1f, .7f), materials[2]);
            Box("Screen", kiosk, new Vector3(0, 1.55f, 0), new Vector3(1.2f, .85f, .18f), materials[7]);
            Box("Roof", kiosk, new Vector3(0, 2.1f, 0), new Vector3(1.7f, .15f, .8f), materials[3]);
            var hazard = Group("SyntheticHazardIndicator", parent);
            hazard.localPosition = new Vector3(5.3f, 0, 6.2f);
            Primitive("OrangeBeacon", hazard, PrimitiveType.Capsule, new Vector3(0, .7f, 0),
                new Vector3(.7f, .65f, .7f), materials[9], true);
            Box("RedPedestal", hazard, new Vector3(0, .15f, 0), new Vector3(1.1f, .3f, 1.1f), materials[4]);
            var sign = Group("AssemblyDirectionSign", parent);
            sign.localPosition = new Vector3(0, 2.65f, 13.8f);
            Box("Board", sign, Vector3.zero, new Vector3(1.7f, .45f, .1f), materials[6], false);
            Arrow("Arrow", sign, new Vector3(0, 0, -.065f), materials[8], true);
        }

        private static DemoInteractable[] BuildInteractables(Transform parent, Material[] materials)
        {
            var names = new[] { "SituationPanel", "AlarmSimulator", "RadioConsole", "DirectionSign", "AccessGate" };
            var labels = new[] { "상황 패널", "경보 체험 버튼", "무전 콘솔", "방향 안내 표지", "체험 게이트" };
            var positions = new[]
            {
                new Vector3(-3, 1.1f, 0), new Vector3(3, 1.1f, 0),
                new Vector3(-3, 1.1f, 4), new Vector3(3, 1.1f, 4), new Vector3(2.15f, 1.1f, 10)
            };
            var colors = new[] { materials[3], materials[4], materials[7], materials[5], materials[6] };
            var targets = new DemoInteractable[5];
            for (var i = 0; i < targets.Length; i++)
            {
                var target = Group(names[i] + "_" + (i + 1).ToString("00"), parent);
                target.localPosition = positions[i];
                if (i != 4) target.localRotation = Quaternion.Euler(0, positions[i].x < 0 ? -90 : 90, 0);
                Box("Support", target, new Vector3(0, -.7f, .05f), new Vector3(.2f, .8f, .2f), materials[2]);
                Box("Base", target, new Vector3(0, -1.025f, .05f), new Vector3(.7f, .15f, .6f), materials[2]);
                Box("Body", target, Vector3.zero, new Vector3(.8f, .7f, .3f), colors[i]);
                switch (i)
                {
                    case 0:
                        Box("Display", target, new Vector3(0, .03f, -.17f), new Vector3(.58f, .4f, .04f), materials[7], false);
                        break;
                    case 1:
                        Primitive("PushButton", target, PrimitiveType.Sphere, new Vector3(0, 0, -.2f),
                            new Vector3(.25f, .25f, .12f), materials[8], true);
                        break;
                    case 2:
                        Box("RadioHandset", target, new Vector3(-.18f, 0, -.24f), new Vector3(.12f, .45f, .12f), materials[2]);
                        Box("Antenna", target, new Vector3(.22f, .58f, .04f), new Vector3(.035f, .5f, .035f), materials[2]);
                        break;
                    case 3:
                        Arrow("DirectionArrow", target, new Vector3(0, 0, -.17f), materials[2], true);
                        break;
                    case 4:
                        var moving = Group("MovingPart", target);
                        moving.localPosition = new Vector3(.1f, .05f, 0);
                        Box("BarrierArm", moving, new Vector3(.2f, 0, 0), new Vector3(.8f, .12f, .15f), materials[5]);
                        break;
                }
                targets[i] = target.gameObject.AddComponent<DemoInteractable>();
                targets[i].Configure("anchor-" + (i + 1).ToString("00"), labels[i], i);
            }
            return targets;
        }

        private static DemoPlayerController CreatePlayer(Transform root, Transform spawn)
        {
            var player = Group("Player", root);
            player.position = spawn.position;
            var capsule = player.gameObject.AddComponent<CharacterController>();
            capsule.height = 1.8f;
            capsule.radius = .3f;
            capsule.center = new Vector3(0, .9f, 0);
            capsule.stepOffset = .25f;
            capsule.skinWidth = .03f;
            capsule.slopeLimit = 45f;
            var camera = Group("ViewCamera", player).gameObject.AddComponent<Camera>();
            camera.transform.localPosition = new Vector3(0, 1.6f, 0);
            camera.tag = "MainCamera";
            camera.nearClipPlane = .08f;
            camera.farClipPlane = 80f;
            camera.fieldOfView = 70f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .09f, .14f);
            camera.gameObject.AddComponent<AudioListener>();
            var controller = player.gameObject.AddComponent<DemoPlayerController>();
            controller.Configure(camera);
            return controller;
        }

        private static Transform Group(string name, Transform parent)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            return item;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collision = true)
        {
            return Primitive(name, parent, PrimitiveType.Cube, position, scale, material, collision);
        }

        private static GameObject Primitive(string name, Transform parent, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material, bool collision)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        private static void Arrow(string name, Transform parent, Vector3 position, Material material, bool vertical = false)
        {
            var arrow = Group(name, parent);
            arrow.localPosition = position;
            if (vertical) arrow.localRotation = Quaternion.Euler(-90, 0, 0);
            Box("Shaft", arrow, Vector3.zero, new Vector3(.08f, .015f, .42f), material, false);
            foreach (var side in new[] { -1, 1 })
            {
                var head = Box("Head_" + side, arrow, new Vector3(side * .09f, 0, .15f),
                    new Vector3(.08f, .015f, .27f), material, false);
                head.transform.localRotation = Quaternion.Euler(0, side * -45, 0);
            }
        }

        private static string ResolveCanonicalScenarioPath()
        {
            var package = PackageInfo.FindForAssembly(typeof(TrainingSession).Assembly);
            if (package == null) throw new InvalidOperationException("Load the foundation package as a local UPM package.");
            var path = Path.GetFullPath(Path.Combine(package.resolvedPath, "..", "..", "foundation", "scenarios", "foundation-demo.json"));
            if (!File.Exists(path)) throw new FileNotFoundException("Keep the CHOOGuard repository intact; canonical scenario missing.", path);
            return path;
        }

        private static Shader FindCompatibleShader()
        {
            var names = GraphicsSettings.currentRenderPipeline == null
                ? new[] { "Standard" }
                : new[] { "Universal Render Pipeline/Lit" };
            var shader = names.Select(Shader.Find).FirstOrDefault(item => item != null);
            if (shader == null) throw new InvalidOperationException("No supported built-in or URP material shader is available.");
            return shader;
        }

        private static Material[] CreateMaterials(string generatedRoot, Shader shader)
        {
            return MaterialNames.Select((name, index) =>
            {
                var path = generatedRoot + "/Materials/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite a non-material asset: " + path);
                    material = new Material(shader) { name = name };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = name=="Screen" ? Shader.Find(GraphicsSettings.currentRenderPipeline==null?"Unlit/Color":"Universal Render Pipeline/Unlit") : shader;
                if (material.HasProperty("_Color")) material.SetColor("_Color", MaterialColors[index]);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", MaterialColors[index]);
                FoundationSurfaceMaterials.Configure(material,name,generatedRoot);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                return material;
            }).ToArray();
        }

        private static string[] OwnedPaths()
        {
            return VersionFourOwnedPaths()
                .Concat(MaterialNames.Except(LegacyMaterialNames).Select(name=>"Materials/"+name+".mat"))
                .Concat(FoundationSurfaceMaterials.FloorNames.Select(name=>"Materials/Floor_"+name+".mat"))
                .Concat(FoundationSurfaceMaterials.TextureNames.Select(name=>"Textures/"+name+".png")).ToArray();
        }

        private static string[] VersionFiveOwnedPaths()
        {
            return VersionFourOwnedPaths().Concat(new[]{"Stainless","Stone","Glass","Diffuser","Roof","Rubber","Ceiling"}.Select(name=>"Materials/"+name+".mat"))
                .Concat(FoundationSurfaceMaterials.FloorNames.Select(name=>"Materials/Floor_"+name+".mat"))
                .Concat(new[]{"StoneTile","BrushedSteel","RoofPanel"}.Select(name=>"Textures/"+name+".png")).ToArray();
        }

        private static string[] VersionFourOwnedPaths()
        {
            return LegacyOwnedPaths().Concat(new[]{"evacuation-drills.json","station-twin-profile.json"})
                .Concat(PrefabNames.Select(name=>"Prefabs/"+name+".prefab")).ToArray();
        }

        private static string[] LegacyOwnedPaths()
        {
            return new[] { "FoundationDemo.unity", "foundation-demo.json", "link.xml" }
                .Concat(LegacyMaterialNames.Select(name => "Materials/" + name + ".mat")).ToArray();
        }

        private static void ValidateGeneratedRoot(string root)
        {
            if (root == null || !Regex.IsMatch(root, @"\AAssets/CHOOguardGenerated/[A-Za-z0-9_-]+\z"))
                throw new ArgumentException("Output must be one dedicated child of Assets/CHOOguardGenerated.", nameof(root));
            RejectLinkedParents(root);
        }

        private static void RejectLinkedParents(string path)
        {
            var current = new DirectoryInfo(Path.GetFullPath(path));
            while (current != null)
            {
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Generated output cannot use a linked directory: " + current.FullName);
                current = current.Parent;
            }
        }

        private static void RequireSavedScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play Mode and wait for compilation before generating the demo.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close modified scenes before generating the demo; no user scene is discarded.");
        }

        private static void RequireOwnedGeneratedDirectory(string root)
        {
            if (File.Exists(root)) throw new InvalidOperationException("Output collides with an existing file.");
            if (!Directory.Exists(root)) return;
            var marker = root + "/" + MarkerName;
            if (!File.Exists(marker)) throw new InvalidOperationException("Existing output directory is not owned by this generator: " + root);
            var ownership = JsonUtility.FromJson<Ownership>(File.ReadAllText(marker));
            var legacy = ownership != null && ownership.version == 1 && ownership.ownedRelativePaths != null &&
                ownership.ownedRelativePaths.SequenceEqual(LegacyOwnedPaths());
            var current = ownership != null && ownership.version == 6 && ownership.ownedRelativePaths != null &&
                ownership.ownedRelativePaths.SequenceEqual(OwnedPaths());
            var versionFive=ownership!=null&&ownership.version==5&&ownership.ownedRelativePaths!=null&&ownership.ownedRelativePaths.SequenceEqual(VersionFiveOwnedPaths());
            var versionFour=ownership!=null&&ownership.version==4&&ownership.ownedRelativePaths!=null&&ownership.ownedRelativePaths.SequenceEqual(VersionFourOwnedPaths());
            var versionThreePaths=LegacyOwnedPaths().Concat(new[]{"evacuation-drills.json"}).Concat(PrefabNames.Select(name=>"Prefabs/"+name+".prefab")).ToArray();
            var versionThree=ownership!=null&&ownership.version==3&&ownership.ownedRelativePaths!=null&&ownership.ownedRelativePaths.SequenceEqual(versionThreePaths);
            var previousPaths=LegacyOwnedPaths().Concat(PrefabNames.Take(11).Select(name=>"Prefabs/"+name+".prefab")).ToArray();
            var previous=ownership!=null && ownership.version==2 && ownership.ownedRelativePaths!=null && ownership.ownedRelativePaths.SequenceEqual(previousPaths);
            if (ownership == null || ownership.generator != OwnerId || (!legacy && !current && !previous && !versionThree && !versionFour && !versionFive))
                throw new InvalidOperationException("Output ownership marker does not match this generator: " + root);
            if (legacy || previous || versionThree || versionFour || versionFive)
                foreach (var path in OwnedPaths().Except(versionFive?VersionFiveOwnedPaths():versionFour?VersionFourOwnedPaths():versionThree?versionThreePaths:previous?previousPaths:LegacyOwnedPaths()))
                    if (File.Exists(root + "/" + path))
                        throw new InvalidOperationException("New generated path already contains an unowned file: " + path);
            foreach (var path in OwnedPaths().Concat(new[] { MarkerName }))
                if (File.Exists(root + "/" + path) && (File.GetAttributes(root + "/" + path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Generated files cannot be symbolic links: " + path);
            RejectLinkedParents(root + "/Materials");
            RejectLinkedParents(root + "/Prefabs");
            RejectLinkedParents(root + "/Textures");
        }

        private static void RequireOwnedBuildDirectory()
        {
            RejectLinkedParents(DesktopOutputRoot);
            if (File.Exists(DesktopOutputRoot)) throw new InvalidOperationException("Desktop output collides with an existing file.");
            if (!Directory.Exists(DesktopOutputRoot)) return;
            RejectLinkedTree(new DirectoryInfo(DesktopOutputRoot));
            var path = DesktopOutputRoot + "/" + MarkerName;
            if (!File.Exists(path)) throw new InvalidOperationException("Desktop output is not owned by the demo builder.");
            var owner = JsonUtility.FromJson<Ownership>(File.ReadAllText(path));
            if (owner == null || owner.generator != OwnerId + ".windows" || owner.version != 1)
                throw new InvalidOperationException("Desktop output ownership marker is invalid.");
        }

        private static void RejectLinkedTree(DirectoryInfo directory)
        {
            foreach (var item in directory.EnumerateFileSystemInfos())
            {
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Desktop output cannot contain linked files or directories: " + item.Name);
                if (item is DirectoryInfo child) RejectLinkedTree(child);
            }
        }

        private static void WriteOwnership(string root)
        {
            var marker = root + "/" + MarkerName;
            File.WriteAllText(marker, JsonUtility.ToJson(new Ownership
                { generator = OwnerId, version = 6, ownedRelativePaths = OwnedPaths() }, true));
            AssetDatabase.ImportAsset(marker, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var split = path.LastIndexOf('/');
            var parent = path.Substring(0, split);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(split + 1))))
                throw new InvalidOperationException("Unity could not create generated folder: " + path);
        }
    }
}
