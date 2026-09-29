using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Editor
{
    /// <summary>
    /// The prefabs and placement entries of the fire compartment equipment (<see cref="CompartmentLayout"/>): automatic fire shutters (fixed head box and rails, the moving
    /// curtain, a blocking collider and a carving nav-mesh obstacle that switch on while the curtain is low), their keyed control boxes with three push buttons, and the
    /// double fire door (frame, two swinging leaves, the fixed panel up to the ceiling) that stands at one end of every shutter opening.
    /// </summary>
    internal static class CompartmentBuilder
    {
        public const string Group = "Compartments";

        public static void BuildPrefabs()
        {
            BuildShutter();
            BuildControl();
            BuildDoor();
        }

        private static void BuildShutter()
        {
            var curtainModel = EmergencySceneBuilder.ObjaverseModel("FireShutterCurtain");
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = "FireShutter", Kind = FireShutterPoint.ShutterKind, Label = "방화셔터", Model = EmergencySceneBuilder.ObjaverseModel("FireShutterBox"), DrawDistance = 70f,
                Interactable = true, Batchable = false,
            }, root =>
            {
                root.gameObject.AddComponent<FireShutterPoint>();
                // 커튼: 셔터 상단(개구부 위 4 m)이 축. 아래로 늘어나며 Y 배율이 0(말려 올라감)에서 1(내려옴)이다.
                var pivot = new GameObject("Curtain");
                pivot.transform.SetParent(root, false);
                pivot.transform.localPosition = new Vector3(0, 4f, 0);
                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(curtainModel);
                mesh.name = "Mesh";
                mesh.transform.SetParent(pivot.transform, false);
                // 내려온 커튼이 플레이어를 막는다: 콜라이더는 낮아졌을 때만 켠다.
                var blocker = new GameObject("Blocker");
                blocker.transform.SetParent(root, false);
                var box = blocker.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 4f, .12f);
                box.center = new Vector3(0, 2f, 0);
                box.enabled = false;
                // 군중은 내려온 커튼을 돌아간다: 낮아졌을 때만 켜는 통과 불가 영역.
                var block = new GameObject("Obstacle");
                block.transform.SetParent(root, false);
                var obstacle = block.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.carving = true;
                obstacle.size = new Vector3(4f, 2.6f, .5f);
                obstacle.center = new Vector3(0, 1.3f, 0);
                obstacle.enabled = false;
            });
        }

        /// <summary>
        /// The fire door: the frame and the two leaves of the converted model (hinges as the model's sidecar gives them) and a fixed panel of the frame's paint from the head of the
        /// frame up to the ceiling, scaled by the door at placement. The sidecar describes OBJ axes; Unity's OBJ import flips X, so the leaf runs from its hinge towards −X here and
        /// the LEFT leaf (hinge at −X) is the mirrored instance. No collider: people walk through it (<see cref="FireDoorPoint"/>).
        /// </summary>
        private static void BuildDoor()
        {
            var frameModel = EmergencySceneBuilder.ObjaverseModel("FireDoorFrame");
            var leafModel = EmergencySceneBuilder.ObjaverseModel("FireDoorLeaf");
            var paint = frameModel.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            var filler = FillerMesh(FireDoorPoint.FrameWidth, FireDoorPoint.FrameDepth);
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = "FireDoor", Kind = FireDoorPoint.DoorKind, Label = "방화문", DrawDistance = 80f, Interactable = false, Batchable = false,
            }, root =>
            {
                root.gameObject.AddComponent<FireDoorPoint>();
                var frame = (GameObject)PrefabUtility.InstantiatePrefab(frameModel);
                frame.name = "Frame";
                frame.transform.SetParent(root, false);
                AddLeaf(root, leafModel, "LeafLeft", new Vector3(-FireDoorPoint.HingeX, 0, -FireDoorPoint.HingeZ), -1f);
                AddLeaf(root, leafModel, "LeafRight", new Vector3(FireDoorPoint.HingeX, 0, -FireDoorPoint.HingeZ), 1f);
                var panel = new GameObject("Filler");
                panel.transform.SetParent(root, false);
                panel.transform.localPosition = new Vector3(0, FireDoorPoint.FrameHeight, 0);
                panel.AddComponent<MeshFilter>().sharedMesh = filler;
                panel.AddComponent<MeshRenderer>().sharedMaterial = paint;
            });
        }

        private static void AddLeaf(Transform root, GameObject model, string name, Vector3 hinge, float mirror)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = hinge;
            pivot.transform.localScale = new Vector3(mirror, 1f, 1f);
            var leaf = (GameObject)PrefabUtility.InstantiatePrefab(model);
            leaf.name = "Leaf";
            leaf.transform.SetParent(pivot.transform, false);
        }

        /// <summary>A box one metre tall, standing on y = 0, centred on x and z: the fixed panel above the door frame (its height is scaled at placement).</summary>
        private static Mesh FillerMesh(float width, float depth)
        {
            string path = EquipmentBuilder.Root + "/Meshes/FireDoorFiller.asset";
            EmergencySceneBuilder.EnsureFolder(EquipmentBuilder.Root + "/Meshes");
            float x = width * .5f, z = depth * .5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int first = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            Face(new Vector3(-x, 0, z), new Vector3(-x, 1, z), new Vector3(x, 1, z), new Vector3(x, 0, z), Vector3.forward);
            Face(new Vector3(x, 0, -z), new Vector3(x, 1, -z), new Vector3(-x, 1, -z), new Vector3(-x, 0, -z), Vector3.back);
            Face(new Vector3(-x, 0, -z), new Vector3(-x, 1, -z), new Vector3(-x, 1, z), new Vector3(-x, 0, z), Vector3.left);
            Face(new Vector3(x, 0, z), new Vector3(x, 1, z), new Vector3(x, 1, -z), new Vector3(x, 0, -z), Vector3.right);
            Face(new Vector3(-x, 1, z), new Vector3(-x, 1, -z), new Vector3(x, 1, -z), new Vector3(x, 1, z), Vector3.up);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = "FireDoorFiller" }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>
        /// The control box: the ShutterBox model with three button colliders on its front plate (centres 18, −30 and −78 mm from the plate centre, 상향 정지 하향), each 48 mm
        /// tall so the aim can pick one at arm's length.
        /// </summary>
        private static void BuildControl()
        {
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = "ShutterControl", Kind = ShutterControlPoint.ControlKind, Label = "방화셔터 조작함", Model = EmergencySceneBuilder.ObjaverseModel("ShutterBox"), DrawDistance = 30f,
                Interactable = true, Batchable = false,
            }, root =>
            {
                root.gameObject.AddComponent<ShutterControlPoint>();
                AddButton(root, "상향 버튼", FireShutterPoint.Command.Up, .018f);
                AddButton(root, "정지 버튼", FireShutterPoint.Command.Stop, -.030f);
                AddButton(root, "하향 버튼", FireShutterPoint.Command.Down, -.078f);
            });
        }

        private static void AddButton(Transform root, string name, FireShutterPoint.Command command, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(-.02f, y, .056f);
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(.07f, .046f, .03f);
            var button = go.AddComponent<ShutterButton>();
            var so = new SerializedObject(button);
            so.FindProperty("command").enumValueIndex = (int)command;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        public static List<EquipmentPlacement> Items(List<CompartmentLayout.Shutter> shutters, Dictionary<DetectorLayout.Detector, string> detectorIds, StationPoints points)
        {
            string ZoneLabel(string id) => points.Zones.FirstOrDefault(z => z.id == id)?.label ?? id;
            var items = new List<EquipmentPlacement>();
            foreach (var s in shutters)
            {
                string number = s.Id.Substring(s.Id.Length - 2);
                string floor = s.Level == 0 ? "1층" : s.Level == 1 ? "2층" : "3층";
                items.Add(new EquipmentPlacement
                {
                    id = s.Id, kind = FireShutterPoint.ShutterKind, prefab = "FireShutter", zone = s.ZoneA, position = s.Position, rotation = Quaternion.LookRotation(s.Normal, Vector3.up).eulerAngles,
                    label = "방화셔터 " + floor + " " + number + "번 (" + ZoneLabel(s.ZoneA) + " · " + ZoneLabel(s.ZoneB) + " 경계)",
                    data = "width=" + F(s.Width) + ";height=" + F(s.Height) + ";detectors=" + string.Join("|", s.Detectors.Select(d => detectorIds[d])) + ";zoneA=" + s.ZoneA + ";zoneB=" + s.ZoneB,
                });
                items.Add(new EquipmentPlacement
                {
                    id = "fc-" + s.Id.Substring(3), kind = ShutterControlPoint.ControlKind, prefab = "ShutterControl", zone = s.ZoneA, position = s.ControlPosition,
                    rotation = Quaternion.LookRotation(s.ControlNormal, Vector3.up).eulerAngles, label = "방화셔터 조작함 " + floor + " " + number + "번", data = "shutter=" + s.Id,
                });
                items.Add(new EquipmentPlacement
                {
                    id = "fd-" + s.Id.Substring(3), kind = FireDoorPoint.DoorKind, prefab = "FireDoor", zone = s.ZoneA, position = s.DoorPosition, rotation = Quaternion.LookRotation(s.Normal, Vector3.up).eulerAngles,
                    label = "방화문 " + floor + " " + number + "번 (" + ZoneLabel(s.ZoneA) + " · " + ZoneLabel(s.ZoneB) + " 경계)", data = "ceiling=" + F(s.DoorCeiling) + ";shutter=" + s.Id,
                });
            }
            return items;
        }
    }
}
