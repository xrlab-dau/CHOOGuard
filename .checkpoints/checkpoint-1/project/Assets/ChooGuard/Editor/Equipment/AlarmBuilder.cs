using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>The prefabs and placement entries of the fire bells and the receiver (<see cref="AlarmLayout"/>): the bell is the converted "Firebell" model on a wall, the receiver the converted panel with its English boot screen replaced by a plain display the game writes on.</summary>
    internal static class AlarmBuilder
    {
        public const string Group = "Alarm";
        private const string DisplayMaterialPath = EmergencySceneBuilder.ArtRoot + "/Materials/AlarmPanelDisplay.mat";
        private static readonly string[] LevelCodes = { "1f", "2f", "3f" }, LevelNames = { "1층", "2층", "3층" };

        public static void BuildPrefabs()
        {
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = "AlarmBell", Kind = AlarmBellSounder.BellKind, Label = "경종", Model = EmergencySceneBuilder.ObjaverseModel("AlarmBell", readable: true), DrawDistance = 36f,
            });
            var display = DisplayMaterial();
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = "AlarmPanel", Kind = AlarmPanelPoint.PanelKind, Label = "자동화재탐지설비 수신기", Model = EmergencySceneBuilder.ObjaverseModel("AlarmPanel"), DrawDistance = 30f, Batchable = false,
            }, root =>
            {
                root.gameObject.AddComponent<AlarmPanelPoint>();
                // 모델의 화면(재질 m5)에는 영어 부팅 화면이 구워져 있다: 한국어를 그릴 어두운 화면으로 바꾼다.
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) if (materials[i] != null && materials[i].name.EndsWith("_m5")) materials[i] = display;
                    renderer.sharedMaterials = materials;
                }
            });
        }

        private static Material DisplayMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit 셰이더가 없습니다.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(DisplayMaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, DisplayMaterialPath); }
            material.shader = shader;
            material.SetColor("_BaseColor", new Color(.015f, .03f, .07f));
            material.SetFloat("_Smoothness", .75f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static List<EquipmentPlacement> Items(AlarmLayout.Result plan)
        {
            var items = new List<EquipmentPlacement>();
            foreach (var group in plan.Bells.GroupBy(b => b.Level).OrderBy(g => g.Key))
            {
                int number = 0;
                foreach (var b in group.OrderBy(b => Mathf.Round(b.Position.x * 100)).ThenBy(b => Mathf.Round(b.Position.z * 100)))
                {
                    number++;
                    items.Add(new EquipmentPlacement
                    {
                        id = "bell-" + LevelCodes[b.Level] + "-" + number.ToString("000"), kind = AlarmBellSounder.BellKind, prefab = "AlarmBell", zone = b.ZoneId,
                        position = b.Position, rotation = Quaternion.LookRotation(b.Normal, Vector3.up).eulerAngles, label = "경종 " + LevelNames[b.Level] + " " + number.ToString("000") + "번",
                        data = "reach=" + AlarmLayout.Reach.ToString("0", CultureInfo.InvariantCulture),
                    });
                }
            }
            if (plan.Receiver != null)
                items.Add(new EquipmentPlacement
                {
                    id = "rx-office", kind = AlarmPanelPoint.PanelKind, prefab = "AlarmPanel", zone = plan.Receiver.Zone, position = plan.Receiver.Position,
                    rotation = Quaternion.LookRotation(plan.Receiver.Normal, Vector3.up).eulerAngles, label = "자동화재탐지설비 수신기 (역무실)", data = "",
                });
            return items;
        }
    }
}
