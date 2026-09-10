using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ChooGuard.Foundation.Demo.Editor;

namespace ChooGuard.Foundation.Demo.Tests
{
    // School-PC EditMode checks. Authoring these tests does not establish Unity execution.
    public sealed class SceneBuilderTests
    {
        private string generatedRoot;
        private Scene previousActive;
        private bool initialized;

        [SetUp]
        public void SetUp()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    Assert.Ignore("Save open scenes before running generator tests; tests never discard user changes.");
            previousActive = SceneManager.GetActiveScene();
            initialized = true;
            generatedRoot = "Assets/CHOOguardGenerated/Tests_" + Guid.NewGuid().ToString("N");
        }

        [TearDown]
        public void TearDown()
        {
            if (!initialized) return;
            for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path.StartsWith(generatedRoot + "/", StringComparison.Ordinal))
                    EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.DeleteAsset(generatedRoot);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            initialized = false;
        }

        [Test]
        public void BuildsMapFiveDistinctAnchorsPlayerCameraAndAssembly()
        {
            var scene = FoundationDemoSceneBuilder.Build(generatedRoot);
            var root = scene.GetRootGameObjects().Single();
            Assert.That(root.name, Is.EqualTo("FoundationDemo"));
            Assert.That(root.transform.Find("Environment/ConcourseFloor"), Is.Not.Null);
            Assert.That(root.transform.Find("Environment/CorridorFloor"), Is.Not.Null);
            Assert.That(root.transform.Find("Environment/AssemblyFloor"), Is.Not.Null);
            Assert.That(root.transform.Find("Markers/AssemblyPoint"), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<DemoGameController>(), Has.Length.EqualTo(1));
            var targets = root.GetComponentInChildren<DemoGameController>().Targets;
            Assert.That(targets.Select(target => target.AnchorId),
                Is.EquivalentTo(Enumerable.Range(1, 5).Select(i => "anchor-" + i.ToString("00"))));
            foreach (var target in targets)
            {
                Assert.That(target.GetComponentsInChildren<Collider>(), Is.Not.Empty, target.name);
                Assert.That(target.GetComponentsInChildren<MeshRenderer>(), Is.Not.Empty, target.name);
            }
            var player = root.GetComponentInChildren<DemoPlayerController>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(player.ViewCamera, Is.Not.Null);
            Assert.That(player.ViewCamera.transform.IsChildOf(player.transform), Is.True);
            Assert.That(root.GetComponentsInChildren<Light>(), Is.Not.Empty);
            Assert.That(root.GetComponentInChildren<DemoIncidentVisual>(), Is.Not.Null);
            Assert.That(File.ReadAllText(generatedRoot + "/link.xml"), Does.Contain("Unity.InputSystem"));
            var game = root.GetComponentInChildren<DemoGameController>();
            Assert.That(game.Player, Is.EqualTo(player));
            Assert.That(game.Targets.Count, Is.EqualTo(5));
            Assert.That(game.AssemblyPoint, Is.EqualTo(root.transform.Find("Markers/AssemblyPoint")));
        }

        [Test]
        public void RepeatGenerationHasStableObjectAndAssetCountsAndPreservesForeignFile()
        {
            var first = FoundationDemoSceneBuilder.Build(generatedRoot);
            var objectCount = first.GetRootGameObjects().Single().GetComponentsInChildren<Transform>().Length;
            var ownedCount = AssetDatabase.FindAssets("", new[] { generatedRoot }).Length;
            var foreignFile = generatedRoot + "/team-notes.txt";
            File.WriteAllText(foreignFile, "keep this team-owned note");
            AssetDatabase.ImportAsset(foreignFile);
            var floorMaterial=AssetDatabase.LoadAssetAtPath<Material>(generatedRoot+"/Materials/Floor_ConcourseFloor.mat");
            floorMaterial.shader=Shader.Find("Unlit/Color");EditorUtility.SetDirty(floorMaterial);AssetDatabase.SaveAssetIfDirty(floorMaterial);
            var second = FoundationDemoSceneBuilder.Build(generatedRoot);
            Assert.That(floorMaterial.shader,Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(generatedRoot+"/Materials/Floor.mat").shader));
            Assert.That(second.GetRootGameObjects().Single().GetComponentsInChildren<Transform>().Length,
                Is.EqualTo(objectCount));
            Assert.That(AssetDatabase.FindAssets("", new[] { generatedRoot }).Length, Is.EqualTo(ownedCount + 1));
            Assert.That(File.ReadAllText(foreignFile), Is.EqualTo("keep this team-owned note"));
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount)
                .Count(i => SceneManager.GetSceneAt(i).path == second.path), Is.EqualTo(1));
        }

        [Test]
        public void SavedSceneReloadKeepsScenarioPlayerAndTargetReferences()
        {
            var scene = FoundationDemoSceneBuilder.Build(generatedRoot);
            var path = scene.path;
            EditorSceneManager.CloseScene(scene, true);
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var game = scene.GetRootGameObjects().Single().GetComponentInChildren<DemoGameController>();
            Assert.That(game.Player, Is.Not.Null);
            Assert.That(game.Player.ViewCamera, Is.Not.Null);
            Assert.That(game.Targets.Count, Is.EqualTo(5));
            Assert.That(game.Targets.All(target => target != null), Is.True);
            Assert.That(game.AssemblyPoint, Is.Not.Null);
            var serialized = new SerializedObject(game);
            var scenario = serialized.FindProperty("scenario").objectReferenceValue as TextAsset;
            Assert.That(scenario, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(scenario), Is.EqualTo(generatedRoot + "/foundation-demo.json"));
            Assert.That(scenario.text, Does.Contain("foundation-demo"));
            Assert.That(serialized.FindProperty("incidentCue").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void CentralRouteAndEveryAnchorApproachHaveCapsuleClearanceAndFloorSupport()
        {
            var scene = FoundationDemoSceneBuilder.Build(generatedRoot);
            Physics.SyncTransforms();
            var root = scene.GetRootGameObjects().Single();
            var player = root.GetComponentInChildren<CharacterController>();
            var centerRoute = new[]
            {
                new Vector3(0, .1f, -4), new Vector3(0, .1f, 0),
                new Vector3(0, .1f, 4), new Vector3(0, .1f, 10),
                new Vector3(0, .1f, 17)
            };
            for (var i = 1; i < centerRoute.Length; i++)
                AssertWalkableSegment(scene, player, centerRoute[i - 1], centerRoute[i]);
            foreach (var target in root.GetComponentInChildren<DemoGameController>().Targets)
            {
                var targetPosition = target.transform.position;
                var center = new Vector3(0, .1f, targetPosition.z);
                var approach = new Vector3(Mathf.Sign(targetPosition.x) * 1.1f, .1f, targetPosition.z);
                AssertWalkableSegment(scene, player, center, approach);
                var eye = approach + Vector3.up * 1.6f;
                var aim = targetPosition - eye;
                var closest = Physics.RaycastAll(eye, aim.normalized, aim.magnitude + .6f,
                        ~0, QueryTriggerInteraction.Ignore)
                    .Where(hit => hit.collider.gameObject.scene == scene && hit.collider != player)
                    .OrderBy(hit => hit.distance).FirstOrDefault();
                Assert.That(closest.collider, Is.Not.Null, target.name + " must be aimable");
                Assert.That(closest.collider.GetComponentInParent<DemoInteractable>(), Is.EqualTo(target), target.name);
                Assert.That(aim.magnitude, Is.LessThanOrEqualTo(player.GetComponent<DemoPlayerController>().InteractionReach),
                    target.name + " interaction reach");
            }
        }

        [Test]
        public void RejectsExistingUnownedDirectoryWithoutChangingItsFile()
        {
            Directory.CreateDirectory(generatedRoot);
            var path = generatedRoot + "/team-notes.txt";
            File.WriteAllText(path, "foreign-scene-sentinel");
            AssetDatabase.ImportAsset(generatedRoot, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
            Assert.Throws<InvalidOperationException>(() => FoundationDemoSceneBuilder.Build(generatedRoot));
            Assert.That(File.ReadAllText(path), Is.EqualTo("foreign-scene-sentinel"));
        }

        [TestCase("Assets/ExistingTeamScene")]
        [TestCase("Assets/CHOOguardGenerated/../ExistingTeamScene")]
        [TestCase("Packages/com.xrlab.chooguard.foundation/Demo")]
        [TestCase("Assets/CHOOguardGenerated")]
        public void RejectsOutputOutsideDedicatedGeneratedChild(string root)
        {
            Assert.Throws<ArgumentException>(() => FoundationDemoSceneBuilder.Build(root));
        }

        [Test]
        public void PristineUntitledStartupSceneCanGenerateTheDemo()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scene = FoundationDemoSceneBuilder.Build(generatedRoot);
            Assert.That(scene.path, Is.EqualTo(generatedRoot + "/FoundationDemo.unity"));
            Assert.That(scene.GetRootGameObjects(), Has.Length.EqualTo(1));
        }

        [Test]
        public void DirtySceneStopsGenerationBeforeWritingAssets()
        {
            var initialUntitled = SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(SceneManager.GetSceneAt(0).path);
            var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                initialUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                EditorSceneManager.MarkSceneDirty(temporary);
                Assert.Throws<InvalidOperationException>(() => FoundationDemoSceneBuilder.Build(generatedRoot));
                Assert.That(Directory.Exists(generatedRoot), Is.False);
            }
            finally { EditorSceneManager.CloseScene(temporary, true); }
        }

        [Test]
        public void DetailedPropCatalogueUsesDecorativeGeometryAndBoundedComplexity()
        {
            var scene = FoundationDemoSceneBuilder.Build(generatedRoot);
            var root = scene.GetRootGameObjects().Single();
            foreach (var target in root.GetComponentInChildren<DemoGameController>().Targets)
            {
                var details = target.transform.Find("Details");
                Assert.That(details, Is.Not.Null, target.name);
                Assert.That(details.GetComponentsInChildren<MeshFilter>(true).All(x => AssetDatabase.GetAssetPath(x.sharedMesh).EndsWith(".fbx")), Is.True, "Blender FBX meshes required: " + target.name);
                Assert.That(details.GetComponentsInChildren<Collider>(true), Is.Empty, "Details must not move collision boundaries.");
            }
            Assert.That(root.transform.Find("Props/Bench_-1/Details"), Is.Not.Null);
            Assert.That(root.transform.Find("Props/InformationKiosk/Details"), Is.Not.Null);
            Assert.That(root.transform.Find("Environment/InteriorDetails/CeilingConcourse"), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<DemoHands>(true), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<Renderer>(true).Length, Is.LessThanOrEqualTo(900));
            Assert.That(root.GetComponentsInChildren<MeshFilter>(true).Sum(x => x.sharedMesh.triangles.Length / 3), Is.LessThan(500000));
            var zone = root.transform.Find("Markers/AssemblyGuide/AssemblyZone");
            Assert.That(zone.localScale.x, Is.EqualTo(DemoGameController.DefaultAssemblyRadius * 2));
        }

        [Test]
        public void MacBuildRefusesForeignOutputBeforeGeneratingOrOverwriting()
        {
            const string output = "Builds/FoundationMac";
            if (Directory.Exists(output)) Assert.Ignore("Existing Mac build must be retained.");
            Directory.CreateDirectory(output);
            var sentinel = output + "/team-file.txt";
            File.WriteAllText(sentinel, "keep");
            try
            {
                var error = Assert.Throws<InvalidOperationException>(() => FoundationDemoSceneBuilder.BuildMacPlayerBatch());
                Assert.That(error.Message, Does.Contain("not owned"));
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("keep"));
                Assert.That(Directory.GetFileSystemEntries(output).Select(Path.GetFileName),
                    Is.EquivalentTo(new[] { Path.GetFileName(sentinel) }),
                    "Refusing must not leave anything the builder wrote behind.");
            }
            // A regression that writes before refusing would leave extra files here, and a
            // non-recursive delete would then throw over the real assertion failure.
            finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
        }

        [Test]
        public void WindowsBuildRefusesForeignOutputBeforeGeneratingOrOverwriting()
        {
            const string output = FoundationDemoSceneBuilder.DesktopOutputRoot;
            if (Directory.Exists(output)) Assert.Ignore("Existing Windows build must be retained.");
            Directory.CreateDirectory(output);
            var sentinel = output + "/team-file.txt";
            File.WriteAllText(sentinel, "keep");
            try
            {
                var error = Assert.Throws<InvalidOperationException>(() => FoundationDemoSceneBuilder.BuildDesktopPlayerBatch());
                Assert.That(error.Message, Does.Contain("not owned"));
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("keep"));
                // The recursive teardown below would erase a file written before the refusal,
                // so the untouched tree has to be asserted while it still exists.
                Assert.That(Directory.GetFileSystemEntries(output).Select(Path.GetFileName),
                    Is.EquivalentTo(new[] { Path.GetFileName(sentinel) }),
                    "Refusing must not leave anything the builder wrote behind.");
            }
            // Same reason as the Mac case: teardown must not replace a real failure with an
            // IOException about a directory the refusal was supposed to leave empty.
            finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
        }

        [Test]
        public void EquipmentAndRoomLabelsStayAtPhysicalSignScale()
        {
            var scene = FoundationDemoSceneBuilder.Build(generatedRoot);
            var root = scene.GetRootGameObjects().Single();
            var labels = root.GetComponentsInChildren<TextMesh>(true);
            Assert.That(labels.Length, Is.GreaterThanOrEqualTo(8));
            foreach (var label in labels)
                Assert.That(label.GetComponent<Renderer>().bounds.size.y, Is.LessThan(.45f), label.name);
        }

        [Test]
        public void GeneratedPrefabLibraryContainsReusablePropFamilies()
        {
            FoundationDemoSceneBuilder.Build(generatedRoot);
            foreach (var name in new[] { "SituationPanel", "AlarmSimulator", "RadioConsole", "DirectionSign", "AccessGate", "Bench", "Pillar", "InformationKiosk", "HazardIndicator", "AssemblySign", "Player" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(generatedRoot + "/Prefabs/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null, name);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThanOrEqualTo(3), name);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(x => x != null), Is.True, name);
            }
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(generatedRoot + "/Prefabs/Player.prefab");
            Assert.That(actor.GetComponent<DemoPlayerController>().ViewCamera, Is.Not.Null);
            Assert.That(actor.GetComponentInChildren<DemoHands>(true), Is.Not.Null);
            var hazard = AssetDatabase.LoadAssetAtPath<GameObject>(generatedRoot + "/Prefabs/HazardIndicator.prefab");
            var cue = new SerializedObject(hazard.GetComponentInChildren<DemoIncidentVisual>(true)).FindProperty("cue").objectReferenceValue as Light;
            Assert.That(cue, Is.Not.Null, "Reusable hazard prefab must keep its own cue reference.");
            Assert.That(cue.transform.IsChildOf(hazard.transform), Is.True);
        }

        [Test]
        public void BlenderModelsHaveMetreScaleAndExpandedRoutesReachEverySharedObject()
        {
            var scene=FoundationDemoSceneBuilder.Build(generatedRoot);var root=scene.GetRootGameObjects().Single();
            var module=AssetDatabase.LoadAssetAtPath<GameObject>(FoundationBlenderAssets.Path+"WallModule.fbx");
            var bounds=module.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            Assert.That(bounds.size.x,Is.EqualTo(1).Within(.01f));Assert.That(bounds.size.y,Is.EqualTo(1).Within(.01f));Assert.That(bounds.size.z,Is.EqualTo(1).Within(.01f));
            var actor=AssetDatabase.LoadAssetAtPath<GameObject>(FoundationBlenderAssets.Path+"Evacuee.fbx");
            var actorBounds=new Bounds();foreach(var renderer in actor.GetComponentsInChildren<Renderer>())actorBounds.Encapsulate(renderer.bounds);
            Assert.That(actorBounds.size.y,Is.InRange(1.6f,1.9f));
            var panel=AssetDatabase.LoadAssetAtPath<GameObject>(FoundationBlenderAssets.Path+"SituationPanel.fbx");
            var status=panel.GetComponentsInChildren<MeshFilter>().Single(x=>x.name.StartsWith("Status_"));
            Assert.That(status.sharedMesh.bounds.center.z,Is.LessThan(-.15f),"FBX front must be Unity -Z.");
            var graph=root.GetComponentInChildren<DemoWalkGraph>();graph.Rebuild();
            Assert.That(graph.NodeCount,Is.GreaterThan(600));
            Assert.That(graph.Clear(new Vector3(9,.1f,6.3f),new Vector3(9,.1f,8)),Is.False,"East floor edge must be closed.");
            Assert.That(graph.Clear(new Vector3(-9,.1f,6.3f),new Vector3(-9,.1f,8)),Is.False,"West floor edge must be closed.");
            var start=root.GetComponentInChildren<DemoPlayerController>().transform.position;
            foreach(var target in root.GetComponentsInChildren<DemoInteractable>())
            {
                var approach=target.transform.position-target.transform.forward*1.3f;approach.y=.1f;
                var route=graph.Route(start,approach);
                Assert.That(route.Length,Is.GreaterThan(0),target.name+" must be reachable through real openings.");
                for(var i=1;i<route.Length;i++)Assert.That(graph.Clear(route[i-1],route[i]),Is.True,target.name);
            }
            var actors=root.GetComponentsInChildren<DemoEvacuee>();Assert.That(actors.Length,Is.EqualTo(6));
            foreach(var npc in actors)
            {
                Assert.That(npc.GetComponentsInChildren<MeshFilter>().All(x=>AssetDatabase.GetAssetPath(x.sharedMesh).EndsWith("Evacuee.fbx")),Is.True);
                Assert.That(graph.Route(npc.transform.position,npc.WaitingSlot).Length,Is.GreaterThan(0),npc.name);
            }
            Assert.That(root.GetComponentsInChildren<MeshFilter>().Where(x=>x.GetComponent<Renderer>()!=null&&x.GetComponent<Renderer>().enabled).All(x=>AssetDatabase.GetAssetPath(x.sharedMesh).EndsWith(".fbx")),Is.True,"Every visible prop and structural mesh is Blender authored.");
        }

        [Test]
        public void ReferenceArtUsesDistinctSurfaceResponseAndValidCurvedNormals()
        {
            FoundationDemoSceneBuilder.Build(generatedRoot);
            var steel=AssetDatabase.LoadAssetAtPath<Material>(generatedRoot+"/Materials/Stainless.mat");
            var rubber=AssetDatabase.LoadAssetAtPath<Material>(generatedRoot+"/Materials/Rubber.mat");
            var floor=AssetDatabase.LoadAssetAtPath<Material>(generatedRoot+"/Materials/Floor.mat");
            Assert.That(steel,Is.Not.Null);Assert.That(rubber,Is.Not.Null);
            Assert.That(steel.GetFloat("_Metallic"),Is.GreaterThan(.7f));
            Assert.That(steel.GetFloat(steel.HasProperty("_Glossiness")?"_Glossiness":"_Smoothness"),Is.InRange(.35f,.8f));
            Assert.That(rubber.GetFloat("_Metallic"),Is.Zero);
            Assert.That(rubber.GetFloat(rubber.HasProperty("_Glossiness")?"_Glossiness":"_Smoothness"),Is.LessThan(.2f));
            Assert.That(steel.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"),Is.False);
            Assert.That(floor.mainTexture,Is.Not.Null);
            Assert.That(floor.mainTexture.width,Is.LessThanOrEqualTo(512));
            var curved=false;
            foreach(var filter in AssetDatabase.LoadAssetAtPath<GameObject>(FoundationBlenderAssets.Path+"Pillar.fbx").GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var normals=mesh.normals;var triangles=mesh.triangles;
                Assert.That(normals.Length,Is.EqualTo(mesh.vertexCount));
                Assert.That(normals.All(n=>!float.IsNaN(n.x)&&n.sqrMagnitude>.9f&&n.sqrMagnitude<1.1f),Is.True);
                for(var i=0;i<triangles.Length;i+=3)
                    curved|=Vector3.Dot(normals[triangles[i]],normals[triangles[i+1]])<.999f;
            }
            Assert.That(curved,Is.True,"Cylindrical surfaces must retain interpolated normals.");
        }

        [Test]
        public void ReferenceEnvelopeRetainsWalkableGroundAndClosesUpperWalls()
        {
            var scene=FoundationDemoSceneBuilder.Build(generatedRoot);
            var root=scene.GetRootGameObjects().Single().transform;
            var ceiling=root.Find("Environment/InteriorDetails/CeilingConcourse");
            Assert.That(ceiling.position.y,Is.GreaterThan(6));
            var details=root.Find("Environment/ReferenceArchitecture");
            Assert.That(details,Is.Not.Null);
            var probe=root.GetComponentInChildren<ReflectionProbe>();
            Assert.That(probe.enabled,Is.False,"Probe starts disabled until the runtime has a real graphics device.");
            Assert.That(probe.GetComponent<DemoRealtimeReflection>(),Is.Not.Null);
            Assert.That(details.GetComponentsInChildren<MeshFilter>().All(x=>AssetDatabase.GetAssetPath(x.sharedMesh).EndsWith(".fbx")),Is.True);
            Assert.That(details.GetComponentsInChildren<Collider>().All(c=>c.bounds.min.y>=3.19f),Is.True,"New architecture must not modify the ground navigation envelope.");
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(new Vector3(0,4,-4),Vector3.back,out var hit,5,1,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(hit.collider.bounds.min.y,Is.GreaterThanOrEqualTo(3.19f));
            Assert.That(Physics.Raycast(new Vector3(0,5.5f,6),Vector3.forward,out var header,3,1,QueryTriggerInteraction.Ignore),Is.True,"Hall-to-corridor roof transition must be closed.");
            Assert.That(header.collider.name,Is.EqualTo("ConcourseToCorridorHeader"));
            var floors=root.Find("Environment").GetComponentsInChildren<MeshFilter>().Where(x=>x.name.EndsWith("Floor"));
            foreach(var floor in floors)
            {
                var scale=floor.GetComponent<Renderer>().sharedMaterial.mainTextureScale;
                Assert.That(scale.x,Is.EqualTo(floor.transform.lossyScale.x).Within(.01f));
                Assert.That(scale.y,Is.EqualTo(floor.transform.lossyScale.z).Within(.01f));
            }
        }

        [Test]
        public void FloorTileUvCoversTheTopFaceAtOneMetreScale()
        {
            var mesh=AssetDatabase.LoadAssetAtPath<GameObject>(FoundationBlenderAssets.Path+"FloorModule.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
            var top=Enumerable.Range(0,mesh.vertexCount).Where(i=>mesh.vertices[i].y>.49f&&mesh.normals[i].y>.95f).ToArray();
            Assert.That(top.Length,Is.GreaterThanOrEqualTo(4));
            Assert.That(top.Min(i=>mesh.uv[i].x),Is.EqualTo(0).Within(.015f));
            Assert.That(top.Max(i=>mesh.uv[i].x),Is.EqualTo(1).Within(.015f));
            Assert.That(top.Min(i=>mesh.uv[i].y),Is.EqualTo(0).Within(.015f));
            Assert.That(top.Max(i=>mesh.uv[i].y),Is.EqualTo(1).Within(.015f));
            foreach(var i in top){Assert.That(mesh.uv[i].x,Is.EqualTo(mesh.vertices[i].x+.5f).Within(.002f));Assert.That(mesh.uv[i].y,Is.EqualTo(mesh.vertices[i].z+.5f).Within(.002f));}
        }

        [Serializable] private sealed class OwnerReceipt { public string generator; public int version; public string[] ownedRelativePaths; }
        [TestCase(4)]
        [TestCase(5)]
        public void PreviousMaterialOwnershipMigratesWithoutOverwritingNewTeamFiles(int version)
        {
            var scene=FoundationDemoSceneBuilder.Build(generatedRoot);
            EditorSceneManager.CloseScene(scene,true);
            var marker=generatedRoot+"/generated-owner.json";
            var owner=JsonUtility.FromJson<OwnerReceipt>(File.ReadAllText(marker));
            var oldMaterials=new[]{"Floor","Wall","Metal","Blue","Red","Yellow","Green","Screen","White","Orange"};
            var oldPaths=new[]{"FoundationDemo.unity","foundation-demo.json","link.xml"}
                .Concat(oldMaterials.Select(x=>"Materials/"+x+".mat"))
                .Concat(new[]{"evacuation-drills.json","station-twin-profile.json"})
                .Concat(owner.ownedRelativePaths.Where(x=>x.StartsWith("Prefabs/"))).ToArray();
            if(version==5)oldPaths=oldPaths.Concat(new[]{"Stainless","Stone","Glass","Diffuser","Roof","Rubber","Ceiling"}.Select(x=>"Materials/"+x+".mat"))
                .Concat(FoundationSurfaceMaterials.FloorNames.Select(x=>"Materials/Floor_"+x+".mat"))
                .Concat(new[]{"StoneTile","BrushedSteel","RoofPanel"}.Select(x=>"Textures/"+x+".png")).ToArray();
            foreach(var path in owner.ownedRelativePaths.Except(oldPaths))AssetDatabase.DeleteAsset(generatedRoot+"/"+path);
            owner.version=version;owner.ownedRelativePaths=oldPaths;File.WriteAllText(marker,JsonUtility.ToJson(owner));
            var foreign=generatedRoot+"/Materials/Wood.mat";
            AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),foreign);
            Assert.Throws<InvalidOperationException>(()=>FoundationDemoSceneBuilder.Build(generatedRoot));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(foreign),Is.Not.Null);
            AssetDatabase.DeleteAsset(foreign);
            FoundationDemoSceneBuilder.Build(generatedRoot);
            Assert.That(JsonUtility.FromJson<OwnerReceipt>(File.ReadAllText(marker)).version,Is.EqualTo(6));
        }

        [Test]
        public void DetailedReferencesHaveDistinctTerminalsAndAppropriateCollisionBodies()
        {
            var scene=FoundationDemoSceneBuilder.Build(generatedRoot);var root=scene.GetRootGameObjects().Single();
            var targets=root.GetComponentInChildren<DemoGameController>().Targets;
            var alarm=targets.Single(x=>x.AnchorId=="anchor-02");
            Assert.That(alarm.transform.Find("AuthoredPhysics"),Is.Not.Null);
            // The real-form callpoint is a small box, not an invisible .8m universal console.
            Assert.That(alarm.transform.Find("Body").GetComponent<Collider>(),Is.Null);
            var body=alarm.GetComponentsInChildren<BoxCollider>().Single(x=>x.name=="Collision_RedWeatherproofBackbox");
            Assert.That(body.size.x,Is.InRange(.13f,.15f));
            var panel=targets.Single(x=>x.AnchorId=="anchor-01");
            var radio=targets.Single(x=>x.AnchorId=="anchor-03");
            Assert.That(panel.GetComponentsInChildren<BoxCollider>().Any(x=>x.name=="Collision_ShallowRearHousing"),Is.True);
            Assert.That(radio.GetComponentsInChildren<BoxCollider>().Any(x=>x.name=="Collision_WedgeHousing"),Is.True);
            var leaf=targets.Single(x=>x.AnchorId=="anchor-05").transform.Find("MovingPart").GetComponentsInChildren<BoxCollider>().Single();
            Assert.That(leaf.size.y,Is.GreaterThan(.65f));
            Assert.That(root.transform.Find("Props/Bench_-1/Back").GetComponent<Collider>(),Is.Null,"No invisible old backrest after the timber bench correction.");
            Assert.That(root.transform.Find("Environment/ReferenceFurniture/Blender_InformationIsland"),Is.Not.Null,"Counter must be used, not counted as a catalog-only result.");
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(generatedRoot+"/Materials/Wood.mat").mainTexture,Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(generatedRoot+"/Materials/Fabric.mat").GetFloat("_Metallic"),Is.Zero);
            var glazing=AssetDatabase.LoadAssetAtPath<Material>(generatedRoot+"/Materials/ClearGlass.mat");
            Assert.That(glazing.renderQueue,Is.GreaterThanOrEqualTo(3000));Assert.That(glazing.color.a,Is.LessThan(.3f));
        }

        [Test]
        public void MannequinFacingAndSolesMatchTravelAndFloorAtGaitExtremes()
        {
            var scene=FoundationDemoSceneBuilder.Build(generatedRoot);var root=scene.GetRootGameObjects().Single();
            var actor=root.GetComponentsInChildren<DemoEvacuee>().First();var at=actor.transform.position;
            var visual=actor.GetComponent<DemoEvacueeVisual>();Assert.That(visual,Is.Not.Null);
            var shoes=actor.GetComponentsInChildren<MeshFilter>().Where(x=>x.name.Contains("Leg_")&&x.name.EndsWith("Rubber")).ToArray();
            Assert.That(shoes.Length,Is.EqualTo(2));
            foreach(var shoe in shoes)Assert.That(shoe.sharedMesh.bounds.center.z,Is.GreaterThan(0),"Toes and jacket face the actor +Z travel direction.");
            foreach(var angle in new[]{-8f,0f,8f})
            {
                foreach(var joint in actor.GetComponentsInChildren<Transform>().Where(x=>x.name.EndsWith("Leg_Joint")))joint.localRotation=Quaternion.Euler(angle,0,0);
                visual.AlignFeet();
                Assert.That(shoes.Min(x=>x.GetComponent<Renderer>().bounds.min.y),Is.InRange(-.005f,.015f));
                Assert.That(actor.transform.position,Is.EqualTo(at),"Ground alignment must not move the navigation root.");
            }
            var portal=root.GetComponentsInChildren<StationPortal>().First();Physics.SyncTransforms();
            var origin=portal.ObservationPoint+Vector3.back*2;
            Assert.That(Physics.Raycast(origin,Vector3.forward,out var hit,2.2f,1,QueryTriggerInteraction.Ignore),Is.True);
            Assert.That(hit.collider.transform.IsChildOf(portal.Cue),Is.True,"Discovery ray must hit the actual beacon, not an old oversized cube.");
        }

        [Test]
        public void ImportedAsymmetricGeometryMatchesAuthoredCollisionCoordinates()
        {
            var scene=FoundationDemoSceneBuilder.Build(generatedRoot);var root=scene.GetRootGameObjects().Single();
            var gate=root.GetComponentInChildren<DemoGameController>().Targets.Single(t=>t.AnchorId=="anchor-05");
            var pivot=gate.transform.Find("MovingPart");
            var leaf=pivot.GetComponentsInChildren<Renderer>().Single(r=>r.name.StartsWith("MovingPart_ClearGlass"));
            var collision=pivot.GetComponentsInChildren<BoxCollider>().Single();
            foreach(var angle in new[]{0f,75f})
            {
                pivot.localRotation=Quaternion.Euler(0,angle,0);Physics.SyncTransforms();
                Assert.That(Vector3.Distance(leaf.bounds.center,collision.bounds.center),Is.LessThan(.005f),"FBX import handedness must match authored collider and hinge coordinates.");
                Assert.That(Vector3.Distance(leaf.bounds.size,collision.bounds.size),Is.LessThan(.005f));
            }
            var lid=gate.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Collision_SlopedReaderLid");
            Assert.That(lid.center.x,Is.EqualTo(-.069f).Within(.002f),"No direct Blender-axis offset bypasses the shared adapter.");
            var glove=AssetDatabase.LoadAssetAtPath<GameObject>(FoundationBlenderAssets.Path+"Glove.fbx");
            var knit=glove.GetComponentsInChildren<MeshFilter>().Single(m=>m.name.StartsWith("KnitPanel_"));
            Assert.That(knit.sharedMesh.normals.All(n=>n.z<-.8f),Is.True,"Open back-of-hand sheet must face outward after handedness conversion.");
            var palm=glove.GetComponentsInChildren<MeshFilter>().Single(m=>m.name.StartsWith("Static_Rubber"));
            Assert.That(Mathf.Abs(palm.sharedMesh.bounds.min.x),Is.GreaterThan(palm.sharedMesh.bounds.max.x),"Thumb stays on authored negative X.");
            var avatar=AssetDatabase.LoadAssetAtPath<GameObject>(FoundationBlenderAssets.Path+"Evacuee.fbx");
            Assert.That(avatar.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="LeftLeg_Rubber").sharedMesh.bounds.center.x,Is.LessThan(0));
            Assert.That(avatar.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="RightLeg_Rubber").sharedMesh.bounds.center.x,Is.GreaterThan(0));
        }

        private static void AssertWalkableSegment(Scene scene, CharacterController player,
            Vector3 start, Vector3 end)
        {
            var displacement = end - start;
            const float radius = .3f;
            var bottom = start + Vector3.up * radius;
            var top = start + Vector3.up * (1.8f - radius);
            var obstructed = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore)
                .Any(collider => collider.gameObject.scene == scene && collider != player);
            Assert.That(obstructed, Is.False, "Blocked route start: " + start);
            var hits = Physics.CapsuleCastAll(bottom, top, radius, displacement.normalized,
                displacement.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Assert.That(hits.Where(hit => hit.collider.gameObject.scene == scene && hit.collider != player),
                Is.Empty, "Blocked route: " + start + " -> " + end);
            for (var i = 0; i <= 10; i++)
            {
                var sample = Vector3.Lerp(start, end, i / 10f);
                Assert.That(Physics.RaycastAll(sample, Vector3.down, .3f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(hit => hit.collider.gameObject.scene == scene && hit.normal.y > .95f),
                    Is.True, "Missing walkable floor: " + sample);
            }
        }
    }
}
