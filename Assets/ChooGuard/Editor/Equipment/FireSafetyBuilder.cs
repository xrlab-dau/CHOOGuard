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
            var detectors = DetectorLayout.Plan(station, survey, points, regions, notes, out var beams);
            var items = Name(detectors, beams, points);

            BuildDetectorPrefab("SmokeDetector", DetectorPoint.SmokeKind, "연기감지기", false);
            BuildDetectorPrefab("HeatDetector", DetectorPoint.HeatKind, "열감지기", false);
            BuildDetectorPrefab("BeamDetector", DetectorPoint.BeamKind, "광전식 분리형 감지기", true);
            EquipmentBuilder.WritePlacements(Group,
                "Spot-type smoke and heat detectors placed by NFTC 203 on the ceilings of the twin (FireSafetyBuilder, DetectorLayout). Positions are ceiling points; rotation follows the ceiling.", items);

            var log = new List<string> { "CG_FIRESAFETY placements=" + items.Count + " smoke=" + items.Count(i => i.kind == DetectorPoint.SmokeKind) + " heat=" + items.Count(i => i.kind == DetectorPoint.HeatKind) + " beam pairs=" + beams.Count };
            foreach (var region in regions)
                log.Add("  region " + region.Name + ": cells(m2)=" + region.Cells + " components=" + region.Components + " min(region)=ceil(area/A)=" + region.RegionMinimum + " min(sum of components)=" + region.Minimum +
                        " placed=" + region.Placed + " m2/detector=" + region.AreaPerDetector.ToString("0.0") + " uncovered=" + region.Uncovered);
            foreach (var beam in beams)
                log.Add("  beam(" + beam.Layer + ") " + beam.Transmitter.ToString("F1") + " -> " + beam.Receiver.ToString("F1") + " length=" + beam.Length.ToString("0.0") + " m axis=" + beam.AxisHeight.ToString("0.0") + " m above the floor");
            foreach (var g in detectors.GroupBy(d => d.ZoneId).OrderBy(g => g.Key)) log.Add("  zone " + g.Key + ": " + g.Count());
            foreach (var note in notes.Take(40)) log.Add("  note " + note);
            Debug.Log(string.Join("\n", log));
        }

        /// <summary>Ids, labels and the receiver's detection zones (경계구역: 24 m tiles per floor, under the 600 m² and 50 m limits of NFTC 203 2.1.1.3) for every detector and beam unit.</summary>
        private static List<EquipmentPlacement> Name(List<DetectorLayout.Detector> detectors, List<BeamLayout.Beam> beams, StationPoints points)
        {
            string Floor(int level) => level == 0 ? "1층" : level == 1 ? "2층" : "3층";
            (string floor, int tx, int tz) Tile(int level, Vector3 at) => (Floor(level), Mathf.FloorToInt(at.x / 24f), Mathf.FloorToInt(at.z / 24f));
            var tiles = detectors.Select(d => Tile(d.Level, d.Ceiling)).Concat(beams.Select(b => Tile(b.Level, (b.Transmitter + b.Receiver) * .5f))).Distinct().ToList();
            var zoneNumbers = new Dictionary<(string floor, int tx, int tz), int>();
            foreach (var floor in tiles.Select(t => t.floor).Distinct().OrderBy(f => f, StringComparer.Ordinal))
            {
                int next = 1;
                foreach (var tile in tiles.Where(t => t.floor == floor).OrderBy(t => t.tx).ThenBy(t => t.tz)) zoneNumbers[tile] = next++;
            }
            var items = new List<EquipmentPlacement>();
            foreach (var group in detectors.GroupBy(d => (tile: Tile(d.Level, d.Ceiling), smoke: d.Smoke)))
            {
                int number = 0;
                foreach (var d in group.OrderBy(d => d.Ceiling.x).ThenBy(d => d.Ceiling.z))
                {
                    number++;
                    string zoneName = group.Key.tile.floor + " " + zoneNumbers[group.Key.tile] + "구역";
                    var rotation = Quaternion.FromToRotation(Vector3.down, d.Normal) * Quaternion.Euler(0, d.Yaw, 0);
                    items.Add(new EquipmentPlacement
                    {
                        id = (d.Smoke ? "smoke-" : "heat-") + LevelCodes[d.Level] + "-z" + zoneNumbers[group.Key.tile].ToString("00") + "-" + number.ToString("00"),
                        kind = d.Smoke ? DetectorPoint.SmokeKind : DetectorPoint.HeatKind,
                        label = (d.Smoke ? "연기감지기 " : "열감지기 ") + zoneName + " " + number.ToString("00") + "번",
                        zone = d.ZoneId,
                        prefab = d.Smoke ? "SmokeDetector" : "HeatDetector",
                        position = d.Ceiling,
                        rotation = rotation.eulerAngles,
                        data = "coverage=" + F(d.Coverage) + ";height=" + F(d.Height) + ";area=" + F(d.Area) + ";class=" + d.ClassName + ";zone=" + zoneName + (d.Room.Length > 0 ? ";room=" + d.Room : ""),
                    });
                }
            }
            // 광전식 분리형: 송광부(tx)와 수광부(rx) 한 쌍이 한 감지기다. 경계구역과 번호는 쌍의 가운데 기준.
            foreach (var group in beams.GroupBy(b => Tile(b.Level, (b.Transmitter + b.Receiver) * .5f)))
            {
                int number = 0;
                foreach (var beam in group.OrderBy(b => b.Layer, StringComparer.Ordinal).ThenBy(b => b.Transmitter.x).ThenBy(b => b.Transmitter.z))
                {
                    number++;
                    string zoneName = group.Key.floor + " " + zoneNumbers[group.Key] + "구역";
                    string stem = "beam-" + LevelCodes[beam.Level] + "-z" + zoneNumbers[group.Key].ToString("00") + "-" + number.ToString("00");
                    string zoneId = points.ZoneAt(beam.Transmitter - Vector3.up * beam.AxisHeight + Vector3.up * .1f)?.id ?? "";
                    string data = "coverage=" + F(BeamLayout.HalfWidth) + ";height=" + F(beam.AxisHeight) + ";area=0;class=광전식 분리형 1종;zone=" + zoneName + ";length=" + F(beam.Length);
                    items.Add(new EquipmentPlacement
                    {
                        id = stem + "-tx", kind = DetectorPoint.BeamKind, label = "광전식 분리형 감지기 " + zoneName + " " + (beam.Layer == "upper" ? "상부 " : "하부 ") + number.ToString("00") + "번 송광부", zone = zoneId, prefab = "BeamDetector",
                        position = beam.Transmitter, rotation = Quaternion.LookRotation(beam.Receiver - beam.Transmitter, Vector3.up).eulerAngles, data = data + ";role=tx;partner=" + stem + "-rx",
                    });
                    items.Add(new EquipmentPlacement
                    {
                        id = stem + "-rx", kind = DetectorPoint.BeamKind, label = "광전식 분리형 감지기 " + zoneName + " " + (beam.Layer == "upper" ? "상부 " : "하부 ") + number.ToString("00") + "번 수광부", zone = zoneId, prefab = "BeamDetector",
                        position = beam.Receiver, rotation = Quaternion.LookRotation(beam.Transmitter - beam.Receiver, Vector3.up).eulerAngles, data = data + ";role=rx;partner=" + stem + "-tx",
                    });
                }
            }
            return items;
        }

        private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// A detector prefab: the converted model on its mounting face, <see cref="DetectorPoint"/>, and the local action indicator
        /// (동작표시등) as a red glow under "Live" that <see cref="DetectorPoint.Trip"/> switches on: under the head of a ceiling
        /// detector, on the front of a wall unit.
        /// </summary>
        private static void BuildDetectorPrefab(string name, string kind, string label, bool wall)
        {
            var info = JsonUtility.FromJson<ModelSize>(File.ReadAllText(EmergencySceneBuilder.ObjaverseRoot + "/" + name + "/" + name + ".json"));
            var glow = EmergencySceneBuilder.ParticleMaterial("DetectorLamp", AssetDatabase.LoadAssetAtPath<Texture2D>(EmergencySceneBuilder.MaterialRoot + "/FlameSoft.png"), true);
            // 동작표시등은 적색 발광(감지기 형식승인 기준 제5조 제31호). 어느 쪽에서 봐도 보이게 양면으로 그린다.
            glow.SetColor("_BaseColor", new Color(1f, .08f, .05f, 1f));
            glow.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(glow);
            EquipmentBuilder.SavePrefab(new EquipmentBuilder.PrefabSpec
            {
                Name = name, Kind = kind, Label = label, Model = EmergencySceneBuilder.ObjaverseModel(name, readable: true), DrawDistance = wall ? 60f : 30f,
            }, root =>
            {
                root.gameObject.AddComponent<DetectorPoint>();
                var live = new GameObject(EquipmentSpawner.LiveChild);
                live.transform.SetParent(root, false);
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.DestroyImmediate(lamp.GetComponent<Collider>());
                lamp.name = "동작표시등";
                lamp.transform.SetParent(live.transform, false);
                // 천장형: 머리 아래면 가장자리(바닥을 봄). 벽형: 앞면 위쪽 모서리(+Z 를 봄). Quad 는 −Z 를 보므로 돌려 붙인다.
                if (wall) lamp.transform.SetLocalPositionAndRotation(new Vector3(info.size[0] * .3f, info.size[1] * .2f, info.size[2] + .004f), Quaternion.Euler(0, 180, 0));
                else lamp.transform.SetLocalPositionAndRotation(new Vector3(info.size[0] * .3f, -info.size[1] - .004f, 0), Quaternion.Euler(-90, 0, 0));
                lamp.transform.localScale = Vector3.one * (wall ? .12f : .16f);
                lamp.GetComponent<MeshRenderer>().sharedMaterial = glow;
                live.SetActive(false);
            });
        }

        [Serializable] private sealed class ModelSize { public float[] size; }
    }
}
