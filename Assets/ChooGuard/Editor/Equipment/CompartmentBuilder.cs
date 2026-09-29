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
    /// curtain, a blocking collider and a carving nav-mesh obstacle that switch on while the curtain is low) and their keyed control boxes with three push buttons.
    /// </summary>
    internal static class CompartmentBuilder
    {
        public const string Group = "Compartments";

        public static void BuildPrefabs()
        {
            BuildShutter();
            BuildControl();
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
            }
            return items;
        }
    }
}
