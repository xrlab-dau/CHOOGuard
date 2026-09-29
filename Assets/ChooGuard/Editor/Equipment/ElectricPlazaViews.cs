using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// The evidence for the ElectricPlaza placements, rendered in batch mode with a GPU device
    /// (unity-batch.sh &lt;clone&gt; method-gfx ChooGuard.Editor.ElectricPlazaViews.Render). The shift's own spawner puts every group's
    /// equipment into FpsStation, then this writes into the clone's <c>.gate/</c> (not part of the repository):
    /// <list type="bullet">
    /// <item><c>w4-plan-&lt;zone&gt;.png</c>: a cut-away top-down plan of each zone (ceilings cut off 3.2 m above the floor) with every item
    /// plotted in its kind's colour (board red, vending blue, kiosk green, recycling orange, litter yellow, cigarette bin magenta);</item>
    /// <item><c>w4-view-&lt;id&gt;.png</c>: an auto-framed close-up of two seeded-random items per kind and of the five items with the
    /// least clearance margin (from <c>.batch/w4-clearance.csv</c>): the camera stands 2.3–3.3 m in front of the item at 1.6 m height and a ray
    /// to the item's centre must hit that item first, otherwise the next angle is tried; an item no angle can see is reported.</item>
    /// </list>
    /// </summary>
    public static class ElectricPlazaViews
    {
        private static readonly Dictionary<string, Color32> Colours = new Dictionary<string, Color32>
        {
            { "distribution_board", new Color32(255, 40, 40, 255) }, { "vending_machine", new Color32(50, 130, 255, 255) }, { "charging_kiosk", new Color32(40, 230, 90, 255) },
            { "recycling_bin", new Color32(255, 150, 20, 255) }, { "litter_bin", new Color32(255, 240, 40, 255) }, { "ash_bin", new Color32(255, 60, 255, 255) },
        };

        /// <summary>Height of an item's centre above its floor point (m), where the close-up looks.</summary>
        private static float CentreHeight(string kind) => kind == "distribution_board" ? 1.4f : kind == "vending_machine" || kind == "charging_kiosk" ? .95f : kind == "recycling_bin" ? .5f : .45f;

        public static void Render()
        {
            EquipmentBuilder.EnsureStation();
            var catalog = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(EquipmentBuilder.CatalogPath);
            var root = new GameObject("views");
            // 콜라이더 없는 유리·외장이 시야와 겹침 검사에 보이도록 임시 콜라이더를 단다(저장하지 않는다).
            using var glass = new TwinColliders();
            Debug.Log("CG_TWIN_COLLIDERS temporary=" + glass.Count);
            var report = EquipmentSpawner.Spawn(catalog, root.transform);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".gate"));
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder, "w4-plan-*.png").Concat(Directory.GetFiles(folder, "w4-view-*.png"))) File.Delete(old);
            var cameraObject = new GameObject("view-camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = .05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .07f, .09f);
            var texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = texture;
            var all = EquipmentRegistry.All.ToList();
            foreach (var zone in all.Select(e => e.Zone).Distinct().OrderBy(z => z, StringComparer.Ordinal)) Plan(camera, texture, folder, zone, all.Where(e => e.Zone == zone).ToList());
            camera.orthographic = false;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 250f;
            int seen = 0, blocked = 0;
            foreach (var equipment in Selection(all))
            {
                if (Closeup(camera, texture, folder, equipment)) seen++; else blocked++;
            }
            texture.Release();
            UnityEngine.Object.DestroyImmediate(cameraObject);
            var spawned = report.ToString();
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("CG_VIEWS_DONE closeups=" + seen + " blocked=" + blocked + " " + spawned);
        }

        /// <summary>Two seeded-random items per kind (fixed seed, ids in order) and the five with the least clearance margin.</summary>
        private static List<StationEquipment> Selection(List<StationEquipment> all)
        {
            var random = new System.Random(20260930);
            var chosen = new List<StationEquipment>();
            foreach (var kind in all.Select(e => e.Kind).Distinct().OrderBy(k => k, StringComparer.Ordinal))
            {
                var pool = all.Where(e => e.Kind == kind).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
                // 수거함·키오스크는 전부, 나머지 종류는 시드 무작위 2개.
                int take = kind == "recycling_bin" || kind == "charging_kiosk" ? pool.Count : 2;
                for (int i = 0; i < take && pool.Count > 0; i++)
                {
                    var pick = pool[random.Next(pool.Count)];
                    pool.Remove(pick);
                    chosen.Add(pick);
                }
            }
            string csv = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".batch", "w4-clearance.csv"));
            if (File.Exists(csv))
            {
                var tight = File.ReadAllLines(csv).Skip(1).Select(l => l.Split(',')).Where(c => c.Length >= 11)
                    .OrderBy(c => float.Parse(c[10], System.Globalization.CultureInfo.InvariantCulture)).ThenBy(c => c[0], StringComparer.Ordinal).Take(5);
                foreach (var row in tight)
                {
                    var equipment = all.FirstOrDefault(e => e.Id == row[0]);
                    if (equipment != null && !chosen.Contains(equipment)) chosen.Add(equipment);
                }
            }
            return chosen;
        }

        private static void Plan(Camera camera, RenderTexture texture, string folder, string zone, List<StationEquipment> items)
        {
            var lo = new Vector3(items.Min(e => e.transform.position.x), 0, items.Min(e => e.transform.position.z));
            var hi = new Vector3(items.Max(e => e.transform.position.x), 0, items.Max(e => e.transform.position.z));
            var centre = (lo + hi) * .5f;
            float floor = items.OrderBy(e => e.transform.position.y).ElementAt(items.Count / 2).transform.position.y;
            float aspect = texture.width / (float)texture.height;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max((hi.z - lo.z) * .5f + 8f, ((hi.x - lo.x) * .5f + 8f) / aspect, 14f);
            camera.transform.position = new Vector3(centre.x, floor + 3.2f, centre.z);
            camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 3.6f;
            camera.Render();
            var image = ReadBack(texture);
            foreach (var equipment in items)
            {
                var screen = camera.WorldToScreenPoint(equipment.transform.position);
                int radius = equipment.Kind == "distribution_board" || equipment.Kind == "vending_machine" || equipment.Kind == "charging_kiosk" ? 6 : 4;
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int x = Mathf.RoundToInt(screen.x) + dx, y = Mathf.RoundToInt(screen.y) + dy;
                        if (x >= 0 && y >= 0 && x < image.width && y < image.height) image.SetPixel(x, y, Mathf.Abs(dx) == radius || Mathf.Abs(dy) == radius ? Color.black : Colours[equipment.Kind]);
                    }
            }
            Save(image, Path.Combine(folder, "w4-plan-" + zone + ".png"), "plan " + zone + " items=" + items.Count + " m/px=" + (camera.orthographicSize * 2 / texture.height).ToString("0.000"));
        }

        private static bool Closeup(Camera camera, RenderTexture texture, string folder, StationEquipment equipment)
        {
            camera.orthographic = false;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 250f;
            var t = equipment.transform;
            var target = t.position + Vector3.up * CentreHeight(equipment.Kind);
            foreach (float distance in new[] { 2.8f, 2.3f, 3.3f })
                foreach (float yaw in new[] { 0f, 25f, -25f, 50f, -50f })
                {
                    var eye = t.position + Quaternion.Euler(0, yaw, 0) * t.forward * distance + Vector3.up * 1.6f;
                    var direction = target - eye;
                    if (Physics.CheckSphere(eye, .25f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    // 카메라에서 항목 가운데로 쏜 광선이 그 항목을 먼저 맞혀야 한다(벽·기둥 뒤에서 본 그림을 빼기 위해).
                    if (!Physics.Raycast(eye, direction.normalized, out var hit, direction.magnitude + .6f, ~0, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<StationEquipment>() != equipment) continue;
                    camera.transform.position = eye;
                    camera.transform.rotation = Quaternion.LookRotation(direction);
                    camera.Render();
                    Save(ReadBack(texture), Path.Combine(folder, "w4-view-" + equipment.Id + ".png"), "view " + equipment.Id + " (" + equipment.Kind + ") at " + t.position.ToString("F1") + " from " + distance + " m yaw " + yaw + " ray-hit=item");
                    var door = equipment.GetComponentInChildren<BoardDoor>();
                    if (door != null)
                    {
                        door.transform.localRotation = Quaternion.Euler(0, door.OpenDegrees, 0);
                        camera.Render();
                        Save(ReadBack(texture), Path.Combine(folder, "w4-view-" + equipment.Id + "-open.png"), "view " + equipment.Id + " door open");
                        door.transform.localRotation = Quaternion.identity;
                    }
                    return true;
                }
            Debug.LogWarning("CG_VIEW_BLOCKED no angle sees " + equipment.Id + " (" + equipment.Kind + ") at " + t.position.ToString("F1"));
            return false;
        }

        private static Texture2D ReadBack(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        private static void Save(Texture2D image, string path, string what)
        {
            File.WriteAllBytes(path, image.EncodeToPNG());
            var pixels = image.GetPixels32();
            long sum = 0;
            for (int i = 0; i < pixels.Length; i += 97) sum += pixels[i].r + pixels[i].g + pixels[i].b;
            Debug.Log("CG_VIEW " + what + " brightness=" + (sum / (pixels.Length / 97 * 3f)).ToString("0.0") + " -> " + path);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
