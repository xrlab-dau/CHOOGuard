using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Equipment;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Builds the fire detection, suppression, compartment and surveillance group (ChooGuard/Emergency/Equipment/Fire
    /// safety): its prefabs and <c>Art/Emergency/Equipment/FireSafety.json</c>. The placement is a pure function of the twin
    /// (<see cref="StationCeilings"/>, <see cref="DetectorLayout"/>): running it twice gives identical bytes.
    /// </summary>
    public static class FireSafetyBuilder
    {
        public const string Group = "FireSafety";
        private static readonly string[] LevelCodes = { "1f", "2f", "3f" };

        [MenuItem("ChooGuard/Emergency/Equipment/Fire safety")]
        public static void Build()
        {
            var station = EquipmentBuilder.EnsureStation();
            var art = AssetDatabase.LoadAssetAtPath<EmergencyArt>(EmergencySceneBuilder.ArtAssetPath) ?? throw new FileNotFoundException(EmergencySceneBuilder.ArtAssetPath);
            var points = StationPoints.Load(art.StationData);
            var survey = StationCeilings.Survey(station, art, points);
            var regions = new List<DetectorLayout.RegionReport>();
            var notes = new List<string>();
            var detectors = DetectorLayout.Plan(survey, points, regions, notes);
            var items = Name(detectors);

            BuildDetectorPrefab("SmokeDetector", DetectorPoint.SmokeKind, "연기감지기");
            BuildDetectorPrefab("HeatDetector", DetectorPoint.HeatKind, "열감지기");
            EquipmentBuilder.WritePlacements(Group,
                "Spot-type smoke and heat detectors placed by NFTC 203 on the ceilings of the twin (FireSafetyBuilder, DetectorLayout). Positions are ceiling points; rotation follows the ceiling.", items);

            var log = new List<string> { "CG_FIRESAFETY detectors=" + items.Count + " smoke=" + items.Count(i => i.kind == DetectorPoint.SmokeKind) + " heat=" + items.Count(i => i.kind == DetectorPoint.HeatKind) };
            foreach (var region in regions)
                log.Add("  region " + region.Name + ": cells=" + region.Cells + " components=" + region.Components + " floorArea=" + region.FloorArea.ToString("0") +
                        " minimum(ceil(area/A) per component)=" + region.Minimum + " placed=" + region.Placed + " m2/detector=" + region.AreaPerDetector.ToString("0.0") + " uncovered=" + region.Uncovered);
            foreach (var g in detectors.GroupBy(d => d.ZoneId).OrderBy(g => g.Key)) log.Add("  zone " + g.Key + ": " + g.Count());
            foreach (var note in notes.Take(40)) log.Add("  note " + note);
            Debug.Log(string.Join("\n", log));
        }

        /// <summary>Ids, labels and the receiver's detection zones (경계구역: 24 m tiles per floor, under the 600 m² and 50 m limits of NFTC 203 2.1.1.3) for every detector.</summary>
        private static List<EquipmentPlacement> Name(List<DetectorLayout.Detector> detectors)
        {
            string Floor(DetectorLayout.Detector d) => d.Level == 0 ? (d.ZoneId == "tracks" ? "승강장" : "1층") : d.Level == 1 ? "2층" : "3층";
            string Code(DetectorLayout.Detector d) => d.Level == 0 ? (d.ZoneId == "tracks" ? "pf" : "1f") : LevelCodes[d.Level];
            var zoneNumbers = new Dictionary<(string floor, int tx, int tz), int>();
            foreach (var floor in detectors.Select(Floor).Distinct().OrderBy(f => f, StringComparer.Ordinal))
            {
                int next = 1;
                foreach (var tile in detectors.Where(d => Floor(d) == floor).Select(d => (tx: Mathf.FloorToInt(d.Ceiling.x / 24f), tz: Mathf.FloorToInt(d.Ceiling.z / 24f))).Distinct().OrderBy(t => t.tx).ThenBy(t => t.tz))
                    zoneNumbers[(floor, tile.tx, tile.tz)] = next++;
            }
            var items = new List<EquipmentPlacement>();
            foreach (var group in detectors.GroupBy(d => (floor: Floor(d), zone: zoneNumbers[(Floor(d), Mathf.FloorToInt(d.Ceiling.x / 24f), Mathf.FloorToInt(d.Ceiling.z / 24f))], smoke: d.Smoke)))
            {
                int number = 0;
                foreach (var d in group.OrderBy(d => d.Ceiling.x).ThenBy(d => d.Ceiling.z))
                {
                    number++;
                    string zoneName = group.Key.floor + " " + group.Key.zone + "구역";
                    string prefix = d.Smoke ? "smoke" : "heat";
                    var rotation = Quaternion.FromToRotation(Vector3.down, d.Normal) * Quaternion.Euler(0, d.Yaw, 0);
                    items.Add(new EquipmentPlacement
                    {
                        id = prefix + "-" + Code(d) + "-z" + group.Key.zone.ToString("00") + "-" + number.ToString("00"),
                        kind = d.Smoke ? DetectorPoint.SmokeKind : DetectorPoint.HeatKind,
                        label = (d.Smoke ? "연기감지기 " : "열감지기 ") + zoneName + " " + number.ToString("00") + "번",
                        zone = d.ZoneId,
                        prefab = d.Smoke ? "SmokeDetector" : "HeatDetector",
                        position = d.Ceiling,
                        rotation = rotation.eulerAngles,
                        data = "coverage=" + F(d.Coverage) + ";height=" + F(d.Height) + ";area=" + F(d.Area) + ";class=" + d.ClassName + ";zone=" + zoneName,
                    });
                }
            }
            return items;
        }

        private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// A detector prefab: the converted model hanging from its mounting face, <see cref="DetectorPoint"/>, and the local
        /// action indicator (동작표시등) as a glow under "Live" that <see cref="DetectorPoint.Trip"/> switches on.
        /// </summary>
        private static void BuildDetectorPrefab(string name, string kind, string label)
        {
            var info = JsonUtility.FromJson<ModelSize>(File.ReadAllText(EmergencySceneBuilder.ObjaverseRoot + "/" + name + "/" + name + ".json"));
            var glow = EmergencySceneBuilder.ParticleMaterial("DetectorLamp", AssetDatabase.LoadAssetAtPath<Texture2D>(EmergencySceneBuilder.MaterialRoot + "/FlameSoft.png"), true);
            // 동작표시등은 적색 발광(감지기 형식승인 기준 제5조 제31호).
            glow.SetColor("_BaseColor", new Color(1f, .08f, .05f, 1f));
            EditorUtility.SetDirty(glow);
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = name, Kind = kind, Label = label, Model = EmergencySceneBuilder.ObjaverseModel(name, readable: true), DrawDistance = 30f,
            }, root =>
            {
                root.gameObject.AddComponent<DetectorPoint>();
                var live = new GameObject(EquipmentSpawner.LiveChild);
                live.transform.SetParent(root, false);
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.DestroyImmediate(lamp.GetComponent<Collider>());
                lamp.name = "동작표시등";
                lamp.transform.SetParent(live.transform, false);
                // 표시등 창은 머리 아래면 가장자리 쪽에 있다. 바닥을 보는 발광 원판.
                lamp.transform.SetLocalPositionAndRotation(new Vector3(info.size[0] * .3f, -info.size[1] - .004f, 0), Quaternion.Euler(90, 0, 0));
                lamp.transform.localScale = Vector3.one * .16f;
                lamp.GetComponent<MeshRenderer>().sharedMaterial = glow;
                live.SetActive(false);
            });
        }

        [Serializable] private sealed class ModelSize { public float[] size; }
    }
}
