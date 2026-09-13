using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Demo.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Editor
{
    public static class ConnectedWorldSceneBuilder
    {
        public const string DefinitionPath = "foundation/world/connected-world-profile.json";
        public const string Root = "Assets/CHOOguardGenerated/ConnectedWorld";
        public const string BootstrapPath = Root + "/ConnectedWorld.unity";
        public const string Output = "Builds/FoundationConnectedWorldMac/ChooGuardConnectedWorld.app";
        private static Material[] materials;
        private static string geometryRoot;
        private static Material M(string name) => materials.Single(m => m.name == name);

        [MenuItem("CHOOguard/Multiplayer/Build Connected World")]
        public static void BuildMenu() => Build();

        public static string[] Build(string rootPath = Root, ConnectedWorldDefinition source = null)
        {
            if (source != null && rootPath == Root) throw new ArgumentException("Custom geometry fixtures require an isolated output folder.");
            var json = source == null ? File.ReadAllText(DefinitionPath) : JsonUtility.ToJson(source);
            var world = JsonUtility.FromJson<ConnectedWorldDefinition>(json); world.Validate();
            json = JsonUtility.ToJson(world, true) + "\n";
            Directory.CreateDirectory(rootPath);
            File.WriteAllText(rootPath + "/connected-world-profile.json", json);
            AssetDatabase.ImportAsset(rootPath + "/connected-world-profile.json", ImportAssetOptions.ForceSynchronousImport);
            geometryRoot = rootPath + "/Geometry";
            if (!AssetDatabase.IsValidFolder(geometryRoot)) AssetDatabase.CreateFolder(rootPath, "Geometry");
            materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets/CHOOguardGenerated/FoundationDemo/Materials" })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var driver = new GameObject("ConnectedWorld");
            var worldRuntime = driver.AddComponent<ConnectedWorldRuntime>();
            worldRuntime.DefinitionJson = AssetDatabase.LoadAssetAtPath<TextAsset>(rootPath + "/connected-world-profile.json");
            worldRuntime.SceneRootPath = rootPath;
            var protocol = driver.AddComponent<NetworkFieldRuntime>();
            protocol.BodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CHOOguardArt/Blender/Evacuee.fbx");
            protocol.SmokeShader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.xrlab.chooguard.foundation/Multiplayer/Runtime/ConservativeSmoke.shader");
            var camera = new GameObject("FieldCamera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.SetParent(driver.transform);
            camera.transform.position = V(world.Region(world.StartRegionId).Hub) + Vector3.up * 1.6f;
            camera.nearClipPlane = .08f; camera.farClipPlane = 260;
            camera.gameObject.AddComponent<AudioListener>();
            var sun = new GameObject("Daylight").AddComponent<Light>(); sun.transform.SetParent(driver.transform);
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.transform.rotation = Quaternion.Euler(55, -35, 0);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.32f, .35f, .4f);
            var paths = new List<string> { rootPath + "/ConnectedWorld.unity" };
            Save(bootstrap, paths[0]);
            foreach (var region in world.Regions)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var owner = new GameObject(region.Id).transform;
                SceneManager.MoveGameObjectToScene(owner.gameObject, scene);
                owner.position = V(region.Center);
                var view = owner.gameObject.AddComponent<ConnectedRegionView>();
                view.RegionId = region.Id; view.FrameId = region.FrameId;
                view.LocalBoundsCenter = V(region.LocalBoundsCenter(world.Frame(region.FrameId)));
                view.LocalBoundsSize = V(region.LocalBoundsSize);
                view.EquipmentEntityId = "equipment." + region.Id;
                view.ControlsLighting = string.IsNullOrEmpty(region.ControlledPortalId);
                Room(world, region, owner);
                foreach (var portal in world.Portals.Where(p => p.StaticBoarding ?
                    (world.Region(p.From).FrameId == "world" ? p.From : p.To) == region.Id : p.From == region.Id)) Corridor(portal, owner);
                Furnish(region, owner);
                Equipment(region, owner, view);
                var path = rootPath + "/" + region.SceneName + ".unity";
                Save(scene, path); paths.Add(path);
                EditorSceneManager.CloseScene(scene, true);
            }
            SceneManager.SetActiveScene(bootstrap);
            if (rootPath == Root)
            {
                Directory.CreateDirectory("foundation/network");
                File.WriteAllText("foundation/network/connected-world-layout.json", JsonUtility.ToJson(world.CreateWorldState(), true) + "\n");
                File.WriteAllText("foundation/network/connected-world-scenes.json", JsonUtility.ToJson(new SceneInventory {
                    ProfileId = world.ProfileId, ScenePaths = paths.ToArray(), Regions = world.Regions.Select(r => r.Id).ToArray(),
                    Scope = "Synthetic traversable geometry; authored asset reuse; new placements and missing vehicle/rail/PSD/vertical families have no visual or facility PASS." }, true) + "\n");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Connected world generated: 13 region scenes and 12 provisional connections. Verification is separate.");
            return paths.ToArray();
        }

        private static void Room(ConnectedWorldDefinition world, ConnectedRegionDefinition region, Transform owner)
        {
            var floor = Box("Floor", owner, new Vector3(0, -.15f, 0), new Vector3(region.SizeX, .3f, region.SizeZ), "Stone");
            var support = floor.AddComponent<ConnectedWalkableSurface>();
            support.SurfaceId = "floor." + region.Id; support.RegionId = region.Id; support.FrameId = region.FrameId;
            foreach (var excluded in region.Exclusions)
            {
                // A physical raised bed and rails exclude the unreviewed railway from pedestrian navigation.
                Box("Provisional_NonWalkableRailBed", owner, V(excluded.Center) - owner.position + Vector3.up * .06f,
                    new Vector3(excluded.SizeX, .12f, excluded.SizeZ), "Rubber");
            }
            var openings = world.Portals.Where(p => p.From == region.Id || p.To == region.Id)
                .Select(p => new { Point = V(p.From == region.Id ? p.FromPoint : p.ToPoint) - owner.position, Width = p.ClearWidth, p.ClearHeight }).ToArray();
            for (var side = 0; side < 4; side++)
            {
                var alongX = side < 2; var sign = side % 2 == 0 ? -1 : 1;
                var length = alongX ? region.SizeX : region.SizeZ;
                var edge = (alongX ? region.SizeZ : region.SizeX) / 2 * sign;
                var cuts = openings.Where(p => Mathf.Abs((alongX ? p.Point.z : p.Point.x) - edge) < .05f)
                    .Select(p => new Vector2((alongX ? p.Point.x : p.Point.z) - p.Width / 2,
                        (alongX ? p.Point.x : p.Point.z) + p.Width / 2)).OrderBy(p => p.x).ToArray();
                var at = -length / 2;
                foreach (var cut in cuts)
                {
                    Wall(at, cut.x, alongX, edge, region, owner); at = cut.y;
                }
                Wall(at, length / 2, alongX, edge, region, owner);
                foreach (var opening in openings.Where(p => Mathf.Abs((alongX ? p.Point.z : p.Point.x) - edge) < .05f && p.ClearHeight < region.WallHeight))
                {
                    var center = opening.Point + Vector3.up * ((opening.ClearHeight + region.WallHeight) / 2);
                    Box("Provisional_PortalLintel", owner, center,
                        alongX ? new Vector3(opening.Width, region.WallHeight - opening.ClearHeight, .18f) :
                            new Vector3(.18f, region.WallHeight - opening.ClearHeight, opening.Width), "Wall");
                }
            }
            if (region.Ceiling)
                Box("Ceiling", owner, Vector3.up * (region.Height + .1f), new Vector3(region.SizeX, .2f, region.SizeZ), "Ceiling");
            Text(region.Label + "\n합성 배치 · 실측/절차 미검증", owner, new Vector3(0, 2.7f, -region.SizeZ / 2 + .3f), .15f);
        }

        private static void Wall(float start, float end, bool alongX, float edge, ConnectedRegionDefinition region, Transform owner)
        {
            if (end - start < .02f) return;
            var height = region.WallHeight;
            if (height <= 0) return;
            var at = alongX ? new Vector3((start + end) / 2, height / 2, edge) : new Vector3(edge, height / 2, (start + end) / 2);
            var size = alongX ? new Vector3(end - start, height, .18f) : new Vector3(.18f, height, end - start);
            Box(region.Template == "vehicle" ? "Provisional_CarriageShell" : "BoundaryWall", owner, at, size, "Wall");
        }

        private static void Corridor(ConnectedPortalDefinition portal, Transform owner)
        {
            var start = V(portal.FromPoint); var end = V(portal.ToPoint);
            var direction = (end - start).normalized;
            var right = Vector3.Cross(Vector3.up, direction).normalized;
            var normal = Vector3.Cross(direction, right).normalized;
            var root = FoundationBlenderAssets.Group("Portal_" + portal.Id, owner, Vector3.zero);
            root.position = (start + end) / 2; root.rotation = Quaternion.LookRotation(direction, normal);
            var length = Vector3.Distance(start, end);
            var floor = Box(portal.StaticBoarding ? "Floor_ProvisionalBoardingSeam" : "Floor_ProvisionalRamp", root,
                Vector3.down * .15f, new Vector3(portal.ClearWidth, .3f, length + .08f), "Stone");
            // Keep the visible seam overlap, but make support upper faces meet at the actual plane hinge.
            // Extending a sloped solid beyond the landing creates a small discontinuous step at its end.
            floor.GetComponent<BoxCollider>().size = new Vector3(1, 1, length / (length + .08f));
            var support = floor.AddComponent<ConnectedWalkableSurface>(); var regionOwner = owner.GetComponent<ConnectedRegionView>();
            support.SurfaceId = "passage." + portal.Id; support.RegionId = regionOwner.RegionId; support.FrameId = "world"; support.PortalId = portal.Id;
            if (portal.WallHeight > 0)
                for (var side = -1; side <= 1; side += 2)
                    GuardPrism(portal.Id + "-" + side, root, new Vector3(side * (portal.ClearWidth / 2 + .09f), 0, 0),
                        length, new Vector3(0, portal.WallHeight * normal.y, portal.WallHeight * direction.y));
            if (portal.Ceiling)
                Box("Provisional_PassageCeiling", root, Vector3.up * (portal.ClearHeight * normal.y + .1f) + Vector3.forward * (portal.ClearHeight * direction.y),
                    new Vector3(portal.ClearWidth, .2f, length + .08f), "Ceiling");
            if (portal.LinkedDoorEntityIds.Length > 0)
            {
                var gate = FoundationBlenderAssets.Group("Provisional_PassageClosure", owner, Vector3.zero);
                gate.position = V(portal.ClosurePoint);
                var flatDirection = end - start; flatDirection.y = 0;
                gate.rotation = Quaternion.LookRotation(flatDirection);
                Box("Provisional_DoorPanel", gate, Vector3.up * (portal.ClosureBottom + portal.ClosureHeight / 2),
                    new Vector3(portal.ClearWidth, portal.ClosureHeight, .12f), "Orange");
                var barrier = gate.gameObject.AddComponent<ConnectedPortalBarrier>();
                barrier.PortalId = portal.Id; barrier.LinkedDoorEntityIds = portal.LinkedDoorEntityIds;
                barrier.SetOpen(true);
            }
            var flat = end - start; flat.y = 0;
            Text("연결 · " + (portal.StaticBoarding ? "정지 차량 승하차" : "보행 통로"), owner,
                start - owner.position + Vector3.up * 2.5f, .1f).rotation = Quaternion.LookRotation(flat);
        }

        private static void GuardPrism(string id, Transform parent, Vector3 at, float length, Vector3 rise)
        {
            var corners = new[] { new Vector3(-.09f, 0, -length / 2), new Vector3(.09f, 0, -length / 2),
                new Vector3(.09f, 0, length / 2), new Vector3(-.09f, 0, length / 2) };
            corners = corners.Concat(corners.Select(v => v + rise)).ToArray();
            var faces = new[] { new[] { 0, 1, 2, 3 }, new[] { 4, 7, 6, 5 }, new[] { 0, 4, 5, 1 },
                new[] { 1, 5, 6, 2 }, new[] { 2, 6, 7, 3 }, new[] { 3, 7, 4, 0 } };
            var vertices = new List<Vector3>(); var indices = new List<int>();
            foreach (var face in faces)
            {
                var start = vertices.Count; vertices.AddRange(face.Select(i => corners[i]));
                indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var path = geometryRoot + "/" + id + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var fresh = mesh == null; if (fresh) mesh = new Mesh { name = "AuthoredPassageGuard_" + id }; else mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
            var guard = FoundationBlenderAssets.Group("Provisional_RampGuard", parent, at).gameObject;
            guard.AddComponent<MeshFilter>().sharedMesh = mesh; guard.AddComponent<MeshRenderer>().sharedMaterial = M("Metal");
            guard.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void Furnish(ConnectedRegionDefinition r, Transform owner)
        {
            if (r.Template == "vehicle")
            {
                for (var x = -r.SizeX / 2 + 3; x < r.SizeX / 2 - 2; x += 5)
                {
                    Asset("Bench", owner, new Vector3(x, 0, 1.7f), 0, true);
                    Box("Provisional_CarriageWindow", owner, new Vector3(x, 1.9f, r.SizeZ / 2 - .13f), new Vector3(3.2f, .85f, .04f), "ClearGlass", false);
                }
                Asset("Luggage", owner, new Vector3(-r.SizeX / 2 + 1.5f, 0, 1.2f), 0, false);
            }
            else if (r.Template == "track")
            {
                foreach (var z in new[] { -3.7f, -2.2f })
                    Box("Provisional_RailAdapter", owner, new Vector3(0, .18f, z), new Vector3(r.SizeX - 1, .2f, .1f), "Metal");
                for (var x = -r.SizeX / 2 + 1; x < r.SizeX / 2; x += 1.2f)
                    Box("Provisional_SleeperAdapter", owner, new Vector3(x, .13f, -3), new Vector3(.2f, .12f, 3), "Wood");
                Text("비보행 선로 · 점검 보행로만 연결", owner, new Vector3(-8, 1.4f, -1.5f), .13f);
            }
            else if (r.Template.Contains("platform"))
            {
                for (var x = -r.SizeX / 2 + 4; x < r.SizeX / 2 - 3; x += 8)
                {
                    Asset("Bench", owner, new Vector3(x, 0, -3.3f), 180, true);
                    Asset("TactileTile", owner, new Vector3(x, .015f, 3.9f), 90, false);
                }
                Asset("DepartureBoard", owner, new Vector3(-10, 0, 3.4f), 0, false);
                if (r.Template == "metro-platform")
                    foreach (var x in new[] { -16f, -10f, 10f, 16f })
                        Box("Provisional_BoardingScreen", owner, new Vector3(x, 1.2f, 4.3f), new Vector3(4.5f, 2.4f, .1f), "ClearGlass");
            }
            else if (r.Template == "ticket")
            {
                foreach (var x in new[] { -8f, 0f, 8f })
                { Asset("InformationIsland", owner, new Vector3(x, 0, 5.6f), 0, true); Asset("AssemblyRegister", owner, new Vector3(x, 1.1f, 5.6f), 0, false); }
            }
            else if (r.Template == "connector" || r.Template == "shopping")
            {
                for (var x = -r.SizeX / 2 + 6; x < r.SizeX / 2 - 3; x += 12)
                {
                    Asset("WallPanel", owner, new Vector3(x, 0, -r.SizeZ / 2 + .2f), 0, false);
                    if (r.Template == "shopping") Box("Provisional_ShopFront", owner, new Vector3(x, 1.3f, 4.9f), new Vector3(4, 2.6f, .2f), "ClearGlass");
                }
            }
            else
            {
                foreach (var side in new[] { -1, 1 })
                {
                    Asset("Bench", owner, new Vector3(side * (r.SizeX / 2 - 4), 0, -r.SizeZ / 2 + 3), 0, true);
                    if (r.Template != "forecourt") Asset("Pillar", owner, new Vector3(side * (r.SizeX / 2 - 3), 0, 4), 0, false);
                }
                if (r.Template == "concourse")
                {
                    Asset("RoofTruss", owner, new Vector3(0, r.Height - 1.1f, 5), 0, false);
                    Asset("DepartureBoard", owner, new Vector3(0, 0, 8), 0, false);
                }
                if (r.Template == "forecourt") Asset("AssemblySign", owner, new Vector3(-12, 0, 7), 0, false);
            }
            var lightCount = Mathf.CeilToInt(r.SizeX / 14);
            for (var i = 0; i < lightCount; i++)
            {
                var x = (i + .5f) * r.SizeX / lightCount - r.SizeX / 2;
                if (r.Template != "forecourt" && r.Template != "track")
                    Asset("RecessedLight", owner, new Vector3(x, r.Height - .2f, 0), 0, false);
                var light = new GameObject("SharedRegionLight").AddComponent<Light>(); light.transform.SetParent(owner, false);
                light.transform.localPosition = new Vector3(x, Mathf.Min(r.Height - .3f, 3.5f), 0);
                light.type = LightType.Point; light.intensity = 1.5f; light.range = 13; light.shadows = LightShadows.None;
            }
        }

        private static void Equipment(ConnectedRegionDefinition r, Transform owner, ConnectedRegionView view)
        {
            var asset = Asset(r.EquipmentAsset, owner, V(r.EquipmentPosition) - owner.position, 0, true);
            var anchor = asset.gameObject.AddComponent<NetworkEntityAnchor>();
            anchor.RegionId = r.Id; anchor.EntityId = "equipment." + r.Id; anchor.Kind = EntityKind.Equipment;
            view.Equipment = anchor;
            Box("SharedStateIndicator", asset, new Vector3(0, 2.1f, 0), new Vector3(.18f, .12f, .18f), "Green", false);
            Text(r.EquipmentLabel + "\nE 공유 상태 전환", asset, new Vector3(0, 2.4f, 0), .08f);
        }

        private static Transform Asset(string id, Transform parent, Vector3 at, float yaw, bool collider)
        {
            var owner = FoundationBlenderAssets.Group(id, parent, at); owner.localRotation = Quaternion.Euler(0, yaw, 0);
            var model = FoundationBlenderAssets.Instantiate(id, owner, materials);
            if (collider)
            {
                if (id == "AccessGate") FoundationBlenderAssets.Group("MovingPart", owner, Vector3.zero);
                FoundationAssetPhysics.Apply(owner, model, id);
            }
            return owner;
        }
        private static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size, string material, bool collider = true) =>
            FoundationBlenderAssets.Box(name, parent, at, size, M(material), collider);
        private static Transform Text(string text, Transform parent, Vector3 at, float size)
        {
            var owner = FoundationBlenderAssets.Group("PublicLabel", parent, at);
            var label = owner.gameObject.AddComponent<TextMesh>(); label.text = text; label.characterSize = size;
            label.fontSize = 48; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
            return owner;
        }
        private static void Save(Scene scene, string path)
        { if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path); }
        private static Vector3 V(Point3 p) => new Vector3(p.X, p.Y, p.Z);
        [Serializable] private sealed class SceneInventory { public string ProfileId, Scope; public string[] ScenePaths, Regions; }

        [MenuItem("CHOOguard/Multiplayer/Prepare Connected World Mac")]
        public static void PrepareMac()
        {
            NativeSdkImportPolicy.Prepare();
            var directory = Path.GetDirectoryName(Output); var marker = Path.Combine(directory, "chooguard-connected-world-owner.txt");
            if (Directory.Exists(directory) && (!File.Exists(marker) || File.ReadAllText(marker).Trim() != "connected-world-v1"))
                throw new InvalidOperationException("Connected-world output is not owned by this builder.");
            Directory.CreateDirectory(directory); File.WriteAllText(marker, "connected-world-v1\n");
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX), "Architecture",
                UnityEditor.Build.OSArchitecture.ARM64.ToString());
            var paths = Build();
            EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            PlayerSettings.companyName = "XRLab"; PlayerSettings.productName = "CHOOguard 연결 월드";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
            PlayerSettings.macOS.microphoneUsageDescription = "훈련 중 무전 버튼을 누르는 동안 팀원에게 음성을 전달합니다.";
            PlayerSettings.macOS.cameraUsageDescription = "카메라 접근은 이 음성 훈련에 필요하지 않습니다.";
            AssetDatabase.SaveAssets();
        }
    }
}
