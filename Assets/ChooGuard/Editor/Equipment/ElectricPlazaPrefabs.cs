using System;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// The prefabs of the ElectricPlaza group, built from the converted free models (ThirdParty/Models/Objaverse, credits in
    /// ThirdParty/Licenses/NOTICE.txt): every model stands with its pivot on the floor at the wall it leans on (front = +Z),
    /// a distribution board hangs with its bottom 1.0 m above the floor and carries a hinged door. Screens and lit panels glow.
    /// </summary>
    internal static class ElectricPlazaPrefabs
    {
        /// <summary>Bottom of a small distribution board above the floor (m): operable without tools or sitting down (building-electrical design guidance).</summary>
        public const float BoardBottom = 1.0f;

        public const string Board = "DistributionBoard", VendingDrink = "VendingDrink", VendingSnack = "VendingSnack", ChargingKiosk = "ChargingKiosk",
            LitterBin = "LitterBin", RecyclingBin = "RecyclingBin";

        [Serializable] private sealed class Emission { public float strength; public float[] color; }
        [Serializable] private sealed class Entry { public string name, albedo; public Emission emission; }
        [Serializable] private sealed class DoorInfo { public float[] pivotBlender; public float openedDegreesBlender; }
        [Serializable] private sealed class Info { public Entry[] materials; public DoorInfo door; }

        /// <summary>Saves every prefab of the group and returns nothing: the placements name them by file name.</summary>
        public static void BuildAll()
        {
            Save(Board, "distribution_board", "분전반", "DistributionBoard", 30f, batchable: false, compose: AddDoor);
            Save(VendingDrink, "vending_machine", "음료 자동판매기", "VendingDrink", 45f);
            Save(VendingSnack, "vending_machine", "스낵 자동판매기", "VendingSnack", 45f);
            Save(ChargingKiosk, "charging_kiosk", "휴대폰 충전 키오스크", "ChargingKiosk", 45f);
            Save(LitterBin, "litter_bin", "휴지통", "StreetBin", 30f);
            Save(RecyclingBin, "recycling_bin", "분리수거함", "RecyclingBin", 30f);
        }

        private static void Save(string prefab, string kind, string label, string model, float drawDistance, bool batchable = true, Action<Transform> compose = null)
        {
            var body = Model(model);
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = prefab, Kind = kind, Label = label, Model = body,
                ModelPosition = prefab == Board ? new Vector3(0, BoardBottom, 0) : Vector3.zero,
                DrawDistance = drawDistance, Interactable = true, Batchable = batchable,
            }, compose);
        }

        /// <summary>The converted model with its lit panels and screens made emissive (the sidecar JSON says which materials glow).</summary>
        private static GameObject Model(string name)
        {
            var model = EmergencySceneBuilder.ObjaverseModel(name, readable: true);
            var info = Read(name);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                {
                    var entry = info.materials.FirstOrDefault(m => material != null && material.name == name + "_" + m.name);
                    if (entry == null || entry.emission == null || entry.emission.strength <= 0) continue;
                    var color = entry.emission.color != null && entry.emission.color.Length >= 3 ? new Color(entry.emission.color[0], entry.emission.color[1], entry.emission.color[2]) : Color.white;
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * entry.emission.strength * 1.5f);
                    // 화면·조명 판은 밑그림 그대로 빛난다.
                    if (!string.IsNullOrEmpty(entry.albedo)) material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    EditorUtility.SetDirty(material);
                }
            AssetDatabase.SaveAssets();
            return model;
        }

        private static Info Read(string name) =>
            JsonUtility.FromJson<Info>(File.ReadAllText(EmergencySceneBuilder.ObjaverseRoot + "/" + name + "/" + name + ".json"));

        /// <summary>
        /// The door, on a pivot at the hinge. Blender (x, y, z) becomes Unity (−x, z, −y) through the OBJ round trip; the door
        /// opens outward (toward +Z), so of the two ways the model's own opening angle can be read the one that carries the
        /// door's centre forward is taken.
        /// </summary>
        private static void AddDoor(Transform root)
        {
            var info = Read(Board);
            string folder = EmergencySceneBuilder.ObjaverseRoot + "/" + Board + "/", path = folder + Board + "Door.obj";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter ?? throw new FileNotFoundException(path);
            importer.globalScale = 1;
            importer.useFileScale = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.isReadable = false;
            foreach (var entry in info.materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.name), EmergencySceneBuilder.JsonMaterial(Board, entry.name));
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException(path);
            var hinge = new Vector3(-info.door.pivotBlender[0], info.door.pivotBlender[2], -info.door.pivotBlender[1]);
            var pivot = new GameObject("Door").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = hinge + new Vector3(0, BoardBottom, 0);
            var door = (GameObject)PrefabUtility.InstantiatePrefab(model);
            door.name = "Leaf";
            door.transform.SetParent(pivot, false);
            var centre = door.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.center;
            float open = Mathf.Abs(Mathf.DeltaAngle(0, -info.door.openedDegreesBlender));
            float sign = (Quaternion.Euler(0, open, 0) * centre).z > (Quaternion.Euler(0, -open, 0) * centre).z ? 1 : -1;
            pivot.gameObject.AddComponent<BoardDoor>().OpenDegrees = sign * open;
        }
    }
}
