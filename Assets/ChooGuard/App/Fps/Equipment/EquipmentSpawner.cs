using System.Collections.Generic;
using System.Diagnostics;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// Places the station's equipment at the start of a shift from every group's placement file. It is written for
    /// hundreds to thousands of small fittings: objects are grouped in spatial cells (one static batch and one culling
    /// unit per cell), the renderers of things that do not move are combined into static batches, decorative equipment has
    /// no collider (the prefab carries one only when <see cref="StationEquipment.Interactable"/>), and renderers beyond each
    /// prefab's draw distance are switched off (<see cref="EquipmentCulling"/>).
    /// </summary>
    public static class EquipmentSpawner
    {
        /// <summary>Child of an equipment prefab whose renderers stay out of static batches (a lamp whose glow differs per object).</summary>
        public const string LiveChild = "Live";

        /// <summary>The report of the latest spawn (for diagnostics and the performance evidence), or null before the first.</summary>
        public static Report Last { get; private set; }

        /// <summary>Edge of a batching/culling cell in metres (horizontal, vertical).</summary>
        public const float CellSize = 24f, CellHeight = 6f;

        /// <summary>What a spawn made, for the log line and the performance evidence.</summary>
        public sealed class Report
        {
            public Transform Root;
            public int Placed, Skipped, Cells, Batched, Unbatched;
            public float Milliseconds;
            public readonly SortedDictionary<string, int> PerKind = new SortedDictionary<string, int>(System.StringComparer.Ordinal);

            public override string ToString() =>
                "placed=" + Placed + " skipped=" + Skipped + " cells=" + Cells + " batched=" + Batched + " unbatched=" + Unbatched + " ms=" + Milliseconds.ToString("0") +
                " [" + string.Join(", ", PerKind) + "]";
        }

        /// <summary>
        /// Instantiates every placement of <paramref name="catalog"/> under a new "설비" object below <paramref name="parent"/>.
        /// An entry that names an unknown prefab or repeats an id is skipped with an error in the console.
        /// </summary>
        public static Report Spawn(EquipmentCatalog catalog, Transform parent)
        {
            var clock = Stopwatch.StartNew();
            var root = new GameObject("설비");
            root.transform.SetParent(parent, false);
            var culling = root.AddComponent<EquipmentCulling>();
            var report = new Report { Root = root.transform };
            var cells = new Dictionary<Vector3Int, Cell>();
            var ids = new HashSet<string>();
            foreach (var file in catalog.Placements)
            {
                if (file == null) continue;
                var data = JsonUtility.FromJson<EquipmentPlacementFile>(file.text);
                foreach (var item in data.items)
                {
                    var prefab = catalog.Prefab(item.prefab);
                    if (prefab == null || !ids.Add(item.id))
                    {
                        Debug.LogError("[EquipmentSpawner] " + data.group + " 의 '" + item.id + "' 를 건너뜀: " + (prefab == null ? "프리팹 '" + item.prefab + "' 없음" : "id 가 겹침"));
                        report.Skipped++;
                        continue;
                    }
                    var key = new Vector3Int(Mathf.FloorToInt(item.position.x / CellSize), Mathf.FloorToInt(item.position.y / CellHeight), Mathf.FloorToInt(item.position.z / CellSize));
                    if (!cells.TryGetValue(key, out var cell))
                    {
                        var cellRoot = new GameObject("격자 " + key.x + "_" + key.y + "_" + key.z);
                        cellRoot.transform.SetParent(root.transform, false);
                        cells[key] = cell = new Cell { Root = cellRoot };
                    }
                    Place(prefab, item, cell);
                    report.PerKind.TryGetValue(item.kind, out int count);
                    report.PerKind[item.kind] = count + 1;
                    report.Placed++;
                }
            }
            foreach (var cell in cells.Values)
            {
                Batch(cell, report);
                culling.Add(cell);
            }
            report.Cells = cells.Count;
            report.Milliseconds = (float)clock.Elapsed.TotalMilliseconds;
            Debug.Log("CG_EQUIPMENT " + report);
            Last = report;
            return report;
        }

        private static void Place(GameObject prefab, EquipmentPlacement item, Cell cell)
        {
            var instance = Object.Instantiate(prefab, item.position, Quaternion.Euler(item.rotation), cell.Root.transform);
            instance.name = item.id;
            var equipment = instance.GetComponent<StationEquipment>() ?? instance.AddComponent<StationEquipment>();
            equipment.Assign(item.id, item.kind, item.label, item.zone, item.data);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            float draw = equipment.DrawDistance * equipment.DrawDistance;
            cell.Items.Add(new Cell.Item { Equipment = equipment, Renderers = renderers, DrawSquared = draw });
            cell.MaxDrawSquared = Mathf.Max(cell.MaxDrawSquared, draw);
            foreach (var renderer in renderers)
            {
                if (cell.HasBounds) cell.Bounds.Encapsulate(renderer.bounds);
                else { cell.Bounds = renderer.bounds; cell.HasBounds = true; }
            }
        }

        /// <summary>One static batch per cell out of the renderers of everything that does not move (a mesh must be readable to be combined).</summary>
        private static void Batch(Cell cell, Report report)
        {
            var parts = new List<GameObject>();
            foreach (var item in cell.Items)
            {
                if (!item.Equipment.Batchable) continue;
                foreach (var renderer in item.Renderers)
                {
                    if (!(renderer is MeshRenderer) || IsLive(renderer.transform, item.Equipment.transform)) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    if (!filter.sharedMesh.isReadable) { report.Unbatched++; continue; }
                    parts.Add(renderer.gameObject);
                }
            }
            if (parts.Count < 2) return;
            StaticBatchingUtility.Combine(parts.ToArray(), cell.Root);
            report.Batched += parts.Count;
        }

        private static bool IsLive(Transform part, Transform root)
        {
            for (var t = part; t != null && t != root; t = t.parent) if (t.name == LiveChild) return true;
            return false;
        }

        /// <summary>The objects of one batching/culling cell.</summary>
        internal sealed class Cell
        {
            internal sealed class Item
            {
                public StationEquipment Equipment;
                public Renderer[] Renderers;
                public float DrawSquared;
                public bool Shown = true;
            }

            public GameObject Root;
            public Bounds Bounds;
            public bool HasBounds;
            public float MaxDrawSquared;
            public readonly List<Item> Items = new List<Item>();
            public bool Shown = true;
        }
    }

    /// <summary>
    /// Switches renderers off beyond their draw distance, four times a second: a whole cell when the camera is farther
    /// from it than any of its objects draws, otherwise object by object. The engine's frustum culling does the rest.
    /// A lit lamp under <see cref="EquipmentSpawner.LiveChild"/> is switched by its owner with SetActive; this only toggles Renderer.enabled.
    /// </summary>
    internal sealed class EquipmentCulling : MonoBehaviour
    {
        private const float Interval = .25f;
        private readonly List<EquipmentSpawner.Cell> cells = new List<EquipmentSpawner.Cell>();
        private float nextCheck;

        public void Add(EquipmentSpawner.Cell cell) => cells.Add(cell);

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + Interval;
            var camera = PlayerView.Camera != null ? PlayerView.Camera : Camera.main;
            if (camera == null) return;
            var eye = camera.transform.position;
            foreach (var cell in cells)
            {
                bool near = cell.Bounds.SqrDistance(eye) <= cell.MaxDrawSquared;
                if (!near && !cell.Shown) continue;
                cell.Shown = near;
                foreach (var item in cell.Items)
                {
                    bool show = near && (item.Equipment.transform.position - eye).sqrMagnitude <= item.DrawSquared;
                    if (show == item.Shown) continue;
                    item.Shown = show;
                    foreach (var renderer in item.Renderers) renderer.enabled = show;
                }
            }
        }
    }
}
