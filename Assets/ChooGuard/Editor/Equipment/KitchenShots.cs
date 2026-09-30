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
    /// Renders the kitchens of the food shops as the twin looks with the placements from <c>KitchenGas.json</c> instantiated (one run,
    /// no play mode): from the concourse through the shop front, from inside, the line face-on, the gas route and a range with its
    /// valve, fuse cock and hose. Writes JPEGs to <c>&lt;project&gt;/.gate/kitchen/</c>. Needs a graphics device: run it as
    /// <c>unity-batch.sh &lt;clone&gt; method-gfx ChooGuard.Editor.KitchenShots.Render</c> (optionally with the env var
    /// <c>CG_KITCHEN_SHOPS</c> = comma-separated words of shop names to limit the run).
    /// </summary>
    public static class KitchenShots
    {
        private const int Width = 1400, Height = 800;

        [MenuItem("ChooGuard/Emergency/Equipment/Render kitchen views")]
        public static void Render()
        {
            EquipmentBuilder.EnsureStation();
            var art = AssetDatabase.LoadAssetAtPath<EmergencyArt>(EmergencySceneBuilder.ArtAssetPath);
            var catalog = art.Equipment;
            var file = JsonUtility.FromJson<EquipmentPlacementFile>(catalog.Placements.First(t => t.name == KitchenGasBuilder.Group).text);
            var root = new GameObject("주방 시각 확인");
            foreach (var item in file.items)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(catalog.Prefab(item.prefab), root.transform);
                instance.transform.SetPositionAndRotation(item.position, Quaternion.Euler(item.rotation));
                instance.name = item.id;
            }
            var only = (Environment.GetEnvironmentVariable("CG_KITCHEN_SHOPS") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".gate", "kitchen"));
            Directory.CreateDirectory(dir);
            var camera = new GameObject("cam").AddComponent<Camera>();
            camera.fieldOfView = 62;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 80;
            var points = StationPoints.Load(art.StationData);
            foreach (var shop in points.Of(PointKind.Shop).OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                var spec = StationKitchens.Of(shop.Label);
                if (spec == null || only.Length > 0 && !only.Any(shop.Label.Contains)) continue;
                var plan = KitchenLayout.Plan(KitchenRooms.Measure(shop), spec);
                var room = plan.Room;
                bool left = plan.Side == "left";
                float wallU = left ? 0 : room.Width, inward = left ? 1 : -1;
                Vector3 At(float s, float d, float h) => room.World(wallU + inward * d, s, h);
                float mid = (plan.Start + plan.End) / 2;
                string name = shop.Label.Replace(' ', '_');
                Shoot(camera, dir, name + "_1front", room.World(room.Width / 2, room.Depth + 2.2f, 1.65f), At(mid, .5f, 1.3f));
                Shoot(camera, dir, name + "_2inside", At(plan.Start - 1.2f, plan.Depth + .6f, 1.6f), At(mid, .6f, 1.3f));
                Shoot(camera, dir, name + "_3line", At(mid, Mathf.Min(plan.Depth, 2.2f) + 1.3f, 1.55f), At(mid, .5f, 1.25f));
                var kext = plan.Parts.First(p => p.Kind == KitchenExtinguisherPoint.Kind);
                var kInstance = root.GetComponentsInChildren<Transform>().First(t => t.name == kext.Id);
                var kBounds = kInstance.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Debug.Log("CG_KEXT pos=" + kext.Position + " euler=" + kext.Euler + " bounds=" + kBounds.center.ToString("F3") + " size=" + kBounds.size.ToString("F3") + " rot=" + kInstance.rotation.eulerAngles + " child=" + kInstance.GetChild(0).rotation.eulerAngles);
                var wallToRoom = new Vector3(left ? room.U.x : -room.U.x, 0, left ? room.U.y : -room.U.y);
                Shoot(camera, dir, name + "_6kext", kext.Position + wallToRoom * 1.3f + Vector3.up * .5f, kext.Position + Vector3.up * .35f);
                var meter = plan.Parts.FirstOrDefault(p => p.Kind == "gas_meter");
                if (meter == null) continue;
                var toRoom = new Vector3(left ? room.U.x : -room.U.x, 0, left ? room.U.y : -room.U.y);
                var firstRange = plan.Parts.First(p => p.Kind == KitchenAppliancePoint.RangeKind);
                var toLine = Vector3.ProjectOnPlane(firstRange.Position - meter.Position, Vector3.up).normalized;
                var lookAt = meter.Position + toLine * 1.1f + Vector3.up * .45f;
                Shoot(camera, dir, name + "_4gas", meter.Position + toRoom * 2.4f + toLine * .3f + Vector3.up * .25f, lookAt);
                var hose = plan.Parts.First(p => p.Kind == "gas_hose");
                var focus = hose.Position + Vector3.down * .15f;
                Shoot(camera, dir, name + "_5range", focus + toRoom * 1.6f + Vector3.up * .3f, focus + Vector3.up * .05f);
            }
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            Debug.Log("CG_KITCHEN_SHOTS " + dir);
        }

        private static void Shoot(Camera camera, string dir, string name, Vector3 eye, Vector3 target)
        {
            camera.transform.position = eye;
            camera.transform.LookAt(target);
            var rt = new RenderTexture(Width, Height, 24);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".jpg"), texture.EncodeToJPG(85));
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
