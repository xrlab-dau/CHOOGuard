using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Builds the kitchen and gas equipment group (menu ChooGuard/Emergency/Equipment/Kitchen and gas): the equipment prefabs (with
    /// the run-time components that make the valves, appliances, alarm, hose and extinguishers work) and
    /// <c>Art/Emergency/Equipment/KitchenGas.json</c>, the placements of every food shop's kitchen from <see cref="StationKitchens"/> laid out by
    /// <see cref="KitchenLayout"/> on the room <see cref="KitchenRooms"/> measures from the twin. Deterministic: two runs write the same bytes.
    /// </summary>
    public static class KitchenGasBuilder
    {
        public const string Group = "KitchenGas";
        private const string OwnModels = EmergencySceneBuilder.ArtRoot + "/Equipment/Models";
        /// <summary>The FBX's own root node is the mesh, stood up by a 270° turn about X (the prefab's Body child takes its rotation from the spec, so it is given back).</summary>
        private static readonly Vector3 ExtinguisherFbxEuler = new Vector3(270, 0, 0);
        private const string ExtinguisherFbx = "Assets/ChooGuard/Art/FireSafety/korean_fire_extinguisher_01_2k.fbx";
        private const string KBodyTexture = EmergencySceneBuilder.ArtRoot + "/Equipment/Textures/KExtinguisher_body_diff.jpg";
        private const string MaterialFolder = EmergencySceneBuilder.ArtRoot + "/Equipment/Materials";

        [Serializable] private sealed class Sidecar { public Entry[] materials; [Serializable] public sealed class Entry { public string name, source; } }

        [MenuItem("ChooGuard/Emergency/Equipment/Kitchen and gas")]
        public static void Build()
        {
            EquipmentBuilder.EnsureStation();
            BuildPrefabs();
            var art = AssetDatabase.LoadAssetAtPath<EmergencyArt>(EmergencySceneBuilder.ArtAssetPath) ?? throw new FileNotFoundException(EmergencySceneBuilder.ArtAssetPath);
            var points = StationPoints.Load(art.StationData);
            var items = new List<EquipmentPlacement>();
            var report = new List<string>();
            foreach (var shop in points.Of(PointKind.Shop).OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                var spec = StationKitchens.Of(shop.Label);
                if (spec == null) continue;
                var plan = KitchenLayout.Plan(KitchenRooms.Measure(shop), spec);
                Audit(plan, spec);
                foreach (var part in plan.Parts)
                    items.Add(new EquipmentPlacement { id = part.Id, kind = part.Kind, label = part.Label, zone = shop.Zone, prefab = part.Prefab, position = part.Position, rotation = part.Euler, data = part.Data });
                report.Add(Describe(plan, spec));
            }
            EquipmentBuilder.WritePlacements(Group, "식품 점포 12곳의 주방·가스 설비: 옆벽을 따라 조리 라인·후드·벽판·가스 배관(계량기→중간밸브→퓨즈콕→호스)·경보기·K급/자동확산소화기. StationKitchens 표와 벽 메시에서 결정적으로 계산.", items);
            foreach (var line in report) Debug.Log("CG_KITCHEN " + line);
            Debug.Log("CG_KITCHEN_GAS parts=" + items.Count + " shops=" + report.Count);
        }

        /// <summary><see cref="Audit"/> with the shop's own kitchen (by the plan's room label), for tests outside the game assembly.</summary>
        public static void AuditShop(KitchenPlan plan) => Audit(plan, StationKitchens.Of(plan.Room.Label));

        /// <summary>The standards' distances as built, or throws.</summary>
        public static void Audit(KitchenPlan plan, StationKitchens.Spec spec)
        {
            string shop = plan.Room.Label;
            if (spec.Gas)
            {
                if (plan.MeterToBurner < 2f) throw new InvalidOperationException(shop + ": 계량기가 화구에서 " + plan.MeterToBurner + " m (별표 7: 2 m 이상)");
                if (plan.MeterHeight < KitchenLayout.MeterBottomMin || plan.MeterHeight > KitchenLayout.MeterBottomMax) throw new InvalidOperationException(shop + ": 계량기 높이 " + plan.MeterHeight + " m (1.6~2 m)");
                if (plan.AlarmDrop > KitchenLayout.AlarmDropMax) throw new InvalidOperationException(shop + ": 경보기 아래쪽이 천장에서 " + plan.AlarmDrop + " m (NFTC 206: 0.3 m 이하)");
                if (plan.AlarmToBurner > KitchenLayout.AlarmReach) throw new InvalidOperationException(shop + ": 경보기가 화구에서 " + plan.AlarmToBurner + " m (NFTC 206: 8 m 이내)");
            }
            if (plan.KExtinguisherTop > KitchenLayout.ExtinguisherTopMax) throw new InvalidOperationException(shop + ": K급 소화기 윗면 " + plan.KExtinguisherTop + " m (NFTC 101 2.1.1.6: 1.5 m 이하)");
        }

        private static string Describe(KitchenPlan plan, StationKitchens.Spec spec) =>
            plan.Room.Label + " side=" + plan.Side + " line=" + plan.Start.ToString("0.00") + "–" + plan.End.ToString("0.00") + " of D=" + plan.Room.Depth.ToString("0.0") + " pass=" + plan.PassCounter +
            " clear=" + plan.Clearance.ToString("0.00") + (spec.Gas ? " meterToBurner=" + plan.MeterToBurner.Value.ToString("0.00") + " meterH=" + plan.MeterHeight.ToString("0.00") + " alarmDrop=" + plan.AlarmDrop.ToString("0.000") + " alarmToBurner=" + plan.AlarmToBurner.ToString("0.0") : "") +
            " kTop=" + plan.KExtinguisherTop.ToString("0.00") + " parts=" + plan.Parts.Count;

        // ── 프리팹 ──

        private static GameObject Third(string name) => EmergencySceneBuilder.ObjaverseModel(name, true);
        private static GameObject Own(string name) => EmergencySceneBuilder.ObjaverseModel(name, true, OwnModels);

        private static void Prefab(string name, string kind, string label, GameObject model, float draw, bool interactable, bool batchable, Action<Transform> compose = null, Bounds? collider = null, Vector3 modelEuler = default) =>
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec { Name = name, Kind = kind, Label = label, Model = model, ModelEuler = modelEuler, DrawDistance = draw, Interactable = interactable, Batchable = batchable, Collider = collider }, compose);

        private static Transform Marker(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        private static void BuildPrefabs()
        {
            EmergencySceneBuilder.EnsureFolder(MaterialFolder);
            // 조리 설비: 튀김기 기름 표면·화구·오븐 문틈에서 불이 난다.
            Prefab(KitchenLayout.FryerPrefab, KitchenAppliancePoint.FryerKind, "튀김기", Third("KitchenFryer"), 35, true, true, root => { Marker(root, "Fire", new Vector3(-.136f, .345f, .068f)); root.gameObject.AddComponent<KitchenAppliancePoint>(); });
            Prefab(KitchenLayout.RangePrefab, KitchenAppliancePoint.RangeKind, "가스레인지", Third("GasRange"), 35, true, true, root => { Marker(root, "Fire", KitchenLayout.Burner); root.gameObject.AddComponent<KitchenAppliancePoint>(); });
            Prefab(KitchenLayout.OvenPrefab, KitchenAppliancePoint.OvenKind, "오븐", Third("DeckOven"), 35, true, true, root => { Marker(root, "Fire", new Vector3(0, .9f, .41f)); root.gameObject.AddComponent<KitchenAppliancePoint>(); });
            Prefab(KitchenLayout.HoodPrefab, "exhaust_hood", "주방 후드", Third("RangeHood"), 40, false, true);
            foreach (var cookware in new[] { "StockPot", "SteamerPot", "FryPan", "Wok" }) Prefab(cookware, "kitchen_cookware", "조리 도구", Third(cookware), 20, false, true);
            // 스테인리스 가구(부딪히지 않게 단단히).
            Prefab(KitchenLayout.SplashPrefab, "kitchen_backsplash", "스테인리스 벽판", Own("StainlessBacksplash"), 30, false, true);
            Prefab(KitchenLayout.TablePrefab, "kitchen_table", "스테인리스 작업대", Own("StainlessTable"), 30, true, true);
            Prefab(KitchenLayout.CounterPrefab, "kitchen_counter", "스테인리스 조리대장", Own("StainlessCabinet"), 30, true, true);
            // 가스 설비.
            Prefab(KitchenLayout.MeterPrefab, "gas_meter", "가스계량기", Own("GasMeter"), 30, false, true, root => Marker(root, "Leak", new Vector3(KitchenLayout.MeterOutletX, .29f, .06f)));
            foreach (var pipe in new[] { KitchenLayout.PipePrefab, KitchenLayout.DropPrefab, KitchenLayout.ElbowDownPrefab, KitchenLayout.ElbowUpPrefab }) Prefab(pipe, "gas_pipe", "도시가스 배관", Own(pipe), 25, false, true);
            Prefab(KitchenLayout.FusePrefab, "fuse_cock", "퓨즈콕", Own("FuseCock"), 20, false, true);
            Prefab(KitchenLayout.ValvePrefab, GasValvePoint.Kind, "가스 밸브", Third("BallValve"), 25, true, false, root =>
            {
                var lever = (GameObject)PrefabUtility.InstantiatePrefab(Third("BallValveLever"));
                lever.name = "Lever";
                lever.transform.SetParent(root, false);
                lever.transform.localPosition = new Vector3(0, .036f, 0);
                root.gameObject.AddComponent<GasValvePoint>();
            }, new Bounds(new Vector3(-.04f, .03f, 0), new Vector3(.22f, .14f, .14f)));
            Prefab(KitchenLayout.HosePrefab, GasHosePoint.Kind, "가스 호스", null, 25, false, false, root =>
            {
                var attached = (GameObject)PrefabUtility.InstantiatePrefab(Own("GasHose"));
                attached.name = "Attached";
                attached.transform.SetParent(root, false);
                var loose = (GameObject)PrefabUtility.InstantiatePrefab(Own("GasHoseOff"));
                loose.name = "Detached";
                loose.transform.SetParent(root, false);
                loose.SetActive(false);
                Marker(root, "Leak", new Vector3(.07f, -.62f, .26f));
                root.gameObject.AddComponent<GasHosePoint>();
            });
            BuildAlarm();
            BuildAutoExtinguisher();
            BuildKExtinguisher();
        }

        /// <summary>The material slot of the imported model whose sidecar entry has the original name <paramref name="source"/> (slots follow the OBJ's, not the sidecar's order).</summary>
        private static int SlotOf(GameObject model, string name, string source)
        {
            var sidecar = JsonUtility.FromJson<Sidecar>(File.ReadAllText(OwnModels + "/" + name + "/" + name + ".json"));
            var entry = sidecar.materials.First(m => m.source == source);
            var materials = model.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            for (int i = 0; i < materials.Length; i++) if (materials[i].name == name + "_" + entry.name) return i;
            throw new InvalidOperationException(name + ": 재질 " + source + " 칸을 찾지 못함");
        }

        private static Material Emissive(string name, Color colour, float intensity)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", colour * .5f);
            material.SetFloat("_Smoothness", .6f);
            // URP Lit derives the _EMISSION keyword from these flags whenever an editor validates the material, so they are set to what it
            // would set itself (a glowing lamp is realtime-emissive, an unlit one is black) and the asset stays the same after that.
            bool glows = intensity > 0f;
            material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (glows) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colour * intensity);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Set(Component component, string field, object value)
        {
            var so = new SerializedObject(component);
            var property = so.FindProperty(field);
            switch (value)
            {
                case int i: property.intValue = i; break;
                case UnityEngine.Object o: property.objectReferenceValue = o; break;
                default: throw new ArgumentException(field);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildAlarm()
        {
            var model = Own("GasAlarm");
            int slot = SlotOf(model, "GasAlarm", "Led_Red");
            var on = Emissive("GasAlarm_LedRedOn", new Color(1f, .05f, .03f), 4f);
            Prefab(KitchenLayout.AlarmPrefab, GasAlarmPoint.Kind, "가스누설경보기", model, 30, true, false, root =>
            {
                var alarm = root.gameObject.AddComponent<GasAlarmPoint>();
                var body = root.GetComponentInChildren<MeshRenderer>();
                Set(alarm, "body", body);
                Set(alarm, "redSlot", slot);
                Set(alarm, "redOn", on);
                Set(alarm, "redOff", body.sharedMaterials[slot]);
            }, new Bounds(new Vector3(0, 0, .02f), new Vector3(.16f, .12f, .08f)));
        }

        private static void BuildAutoExtinguisher()
        {
            var model = Own("AutoExtinguisher");
            int slot = SlotOf(model, "AutoExtinguisher", "Bulb");
            var burst = Emissive("AutoExtinguisher_BulbBurst", new Color(.12f, .02f, .02f), 0f);
            Prefab(KitchenLayout.AutoPrefab, AutoExtinguisherPoint.Kind, "주방용 자동확산소화기", model, 25, false, false, root =>
            {
                var unit = root.gameObject.AddComponent<AutoExtinguisherPoint>();
                Set(unit, "body", root.GetComponentInChildren<MeshRenderer>());
                Set(unit, "bulbSlot", slot);
                Set(unit, "burst", burst);
            });
        }

        /// <summary>The twin's extinguisher model with a K-class sticker on its body: same body, other label (KExtinguisher_body_diff.jpg from make_k_label.py).</summary>
        private static void BuildKExtinguisher()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(ExtinguisherFbx) ?? throw new FileNotFoundException(ExtinguisherFbx);
            var original = fbx.GetComponentInChildren<Renderer>().sharedMaterials.First(m => m.name.EndsWith("_body", StringComparison.Ordinal));
            var path = MaterialFolder + "/KExtinguisher_body.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(original); AssetDatabase.CreateAsset(material, path); }
            material.CopyPropertiesFromMaterial(original);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KBodyTexture) ?? throw new FileNotFoundException(KBodyTexture);
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
            EditorUtility.SetDirty(material);
            Prefab(KitchenLayout.KPrefab, KitchenExtinguisherPoint.Kind, "K급 소화기(주방용)", fbx, 30, true, false, root =>
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) if (materials[i].name == original.name) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                root.gameObject.AddComponent<KitchenExtinguisherPoint>();
            }, null, ExtinguisherFbxEuler);
        }
    }
}
