using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    /// <summary>
    /// What every equipment group's builder uses (ChooGuard/Emergency/Equipment/&lt;Group&gt;): equipment prefabs with the
    /// performance settings the spawner relies on, the deterministic placement file, and the catalog that lets a player
    /// build find both. A group's builder ends with <see cref="WritePlacements"/>, which refreshes the catalog too.
    /// </summary>
    public static class EquipmentBuilder
    {
        public const string Root = EmergencySceneBuilder.ArtRoot + "/Equipment";
        public const string PrefabRoot = Root + "/Prefabs";
        public const string CatalogPath = Root + "/EquipmentCatalog.asset";

        public static string PlacementPath(string group) => Root + "/" + group + ".json";

        /// <summary>FpsStation (the twin) loaded for raycasts against its colliders; opened when a batch run starts without it.</summary>
        public static Scene EnsureStation()
        {
            var station = SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            if (!station.IsValid() || !station.isLoaded) station = EditorSceneManager.OpenScene(EmergencySceneBuilder.StationScenePath, OpenSceneMode.Single);
            return station;
        }

        /// <summary>How one equipment prefab is built.</summary>
        public sealed class PrefabSpec
        {
            /// <summary>Prefab file name; the placement entries name it in <c>prefab</c>.</summary>
            public string Name;
            public string Kind, Label;
            /// <summary>The converted model (Models/Objaverse/&lt;Name&gt;) placed as child "Body", or null when <c>Compose</c> builds everything.</summary>
            public GameObject Model;
            public Vector3 ModelPosition, ModelEuler;
            public float DrawDistance = 40f;
            public bool Interactable, Batchable = true;
            /// <summary>Local box of the collider when interactable; null takes the bounds of the renderers.</summary>
            public Bounds? Collider;
        }

        /// <summary>
        /// Saves <paramref name="spec"/> as a prefab under <see cref="PrefabRoot"/>: the root carries
        /// <see cref="StationEquipment"/> with the performance settings, the model hangs below it, small fittings cast no
        /// shadows and take no reflection probe, and a collider exists only when interactable.
        /// <paramref name="compose"/> may add further parts (a lamp under <see cref="EquipmentSpawner.LiveChild"/>, a curtain).
        /// </summary>
        public static GameObject SavePrefab(PrefabSpec spec, Action<Transform> compose = null)
        {
            EmergencySceneBuilder.EnsureFolder(PrefabRoot);
            var root = new GameObject(spec.Name);
            try
            {
                var equipment = root.AddComponent<StationEquipment>();
                equipment.Kind = spec.Kind;
                equipment.Label = spec.Label;
                equipment.DrawDistance = spec.DrawDistance;
                equipment.Interactable = spec.Interactable;
                equipment.Batchable = spec.Batchable;
                if (spec.Model != null)
                {
                    var body = (GameObject)PrefabUtility.InstantiatePrefab(spec.Model);
                    body.name = "Body";
                    body.transform.SetParent(root.transform, false);
                    body.transform.SetLocalPositionAndRotation(spec.ModelPosition, Quaternion.Euler(spec.ModelEuler));
                }
                compose?.Invoke(root.transform);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                }
                if (spec.Interactable && root.GetComponentInChildren<Collider>(true) == null)
                {
                    var box = root.AddComponent<BoxCollider>();
                    var bounds = spec.Collider ?? LocalBounds(root.transform);
                    box.center = bounds.center;
                    box.size = bounds.size;
                }
                else if (!spec.Interactable && root.GetComponentInChildren<Collider>(true) != null)
                    throw new InvalidOperationException(spec.Name + ": 상호작용하지 않는 설비 프리팹에 콜라이더가 있습니다");
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "/" + spec.Name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Bounds LocalBounds(Transform root)
        {
            Bounds? bounds = null;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var mesh = filter.sharedMesh.bounds;
                foreach (var corner in new[] { mesh.min, mesh.max, new Vector3(mesh.min.x, mesh.min.y, mesh.max.z), new Vector3(mesh.min.x, mesh.max.y, mesh.min.z), new Vector3(mesh.max.x, mesh.min.y, mesh.min.z), new Vector3(mesh.min.x, mesh.max.y, mesh.max.z), new Vector3(mesh.max.x, mesh.min.y, mesh.max.z), new Vector3(mesh.max.x, mesh.max.y, mesh.min.z) })
                {
                    var local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (bounds == null) bounds = new Bounds(local, Vector3.zero); else { var b = bounds.Value; b.Encapsulate(local); bounds = b; }
                }
            }
            return bounds ?? new Bounds(Vector3.zero, Vector3.one * .1f);
        }

        /// <summary>
        /// Writes only proposals with evidence for the exact object and site location. Geometry and regulatory spacing
        /// never supply that evidence. Verified entries are ordered by id, positions rounded to a centimetre, and angles
        /// to a tenth of a degree, so two runs give identical bytes, then the catalog is refreshed.
        /// </summary>
        public static void WritePlacements(string group, string note, IEnumerable<EquipmentPlacement> items)
        {
            EmergencySceneBuilder.EnsureFolder(Root);
            var proposed = items.ToArray();
            var ordered = proposed.Where(i => i.HasPlacementEvidence).OrderBy(i => i.id, StringComparer.Ordinal).ToArray();
            int unverified = proposed.Length - ordered.Length;
            foreach (var item in ordered)
            {
                item.position = new Vector3(Mathf.Round(item.position.x * 100) / 100, Mathf.Round(item.position.y * 100) / 100, Mathf.Round(item.position.z * 100) / 100);
                item.rotation = new Vector3(Mathf.Round(item.rotation.x * 10) / 10, Mathf.Round(item.rotation.y * 10) / 10, Mathf.Round(item.rotation.z * 10) / 10);
            }
            var duplicate = ordered.GroupBy(i => i.id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null) throw new InvalidOperationException(group + ": id 가 겹칩니다 " + duplicate.Key);
            if (unverified > 0)
                note += "\nOmitted " + unverified + " unverified placement candidates. Exact object and site-location evidence is required before placement.";
            var file = new EquipmentPlacementFile { group = group, note = note, items = ordered };
            File.WriteAllText(PlacementPath(group), JsonUtility.ToJson(file, true) + "\n");
            AssetDatabase.ImportAsset(PlacementPath(group));
            RefreshCatalog();
            Debug.Log("CG_EQUIPMENT_PLACEMENTS group=" + group + " verified=" + ordered.Length + " unverified_omitted=" + unverified);
        }

        /// <summary>Collects every placement file and prefab under <see cref="Root"/> into the catalog and points EmergencyArt at it.</summary>
        [MenuItem("ChooGuard/Emergency/Equipment/Refresh catalog")]
        public static EquipmentCatalog RefreshCatalog()
        {
            EmergencySceneBuilder.EnsureFolder(PrefabRoot);
            var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<EquipmentCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.Placements = Directory.GetFiles(Root, "*.json", SearchOption.TopDirectoryOnly).Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>).Where(t => t != null).ToArray();
            catalog.Prefabs = Directory.GetFiles(PrefabRoot, "*.prefab", SearchOption.TopDirectoryOnly).Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(g => g != null).ToArray();
            EditorUtility.SetDirty(catalog);
            var art = AssetDatabase.LoadAssetAtPath<EmergencyArt>(EmergencySceneBuilder.ArtAssetPath) ?? throw new FileNotFoundException(EmergencySceneBuilder.ArtAssetPath);
            art.Equipment = catalog;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log("CG_EQUIPMENT_CATALOG placements=" + catalog.Placements.Length + " prefabs=" + catalog.Prefabs.Length);
            return catalog;
        }
    }
}
