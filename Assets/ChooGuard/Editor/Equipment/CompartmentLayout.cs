using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Where the automatic fire shutters go, from the station's fire compartment lines. No source gives the real shutter positions of Busan Station (public photos of
    /// Korean stations show none, research nftc103-compartments-cctv 2.3), so the compartments follow the law and the twin: the station's own areas (StationPoints zones:
    /// concourse, hall, main building, gates, exits, 1F) are the compartments, and a shutter closes each opening in the wall between two of them that a person walks
    /// through. An opening is found where the boundary between two areas is a short gap (at most 9.5 m) and rays along the boundary meet a wall on each side within
    /// 3-8 m (Building Act enforcement rules art. 14 (2) 4: a shutter seals an opening where a fire door does not fit; the type approval sizes cap a shutter at 8 m x 4 m),
    /// with a ceiling low enough (5.5 m) for the shutter box to sit under it and a wall face beside the opening for the control box. Each shutter gets a smoke and a heat
    /// detector on both sides (art. 14 (2) 4 다: flame or smoke plus heat) and a keyed control box on the wall (0.8-1.5 m above the floor); the opening also holds the double fire
    /// door that art. 14 (2) 4 가 wants within 3 m of the shutter (<see cref="PlaceDoor"/>: it takes one end, the shutter the rest).
    /// Compartment lines that are open floors more than 9.5 m wide (the hall towards the main building, the atrium edge) get no shutter: they are the station's
    /// large undivided spaces, which the standard leaves to smoke control and performance design.
    /// </summary>
    internal static class CompartmentLayout
    {
        public const float MinWidth = 3f, MaxWidth = 8f, MaxCeiling = 5.5f, MaxGap = 9.5f, ControlHeight = 1.3f, DetectorSide = 1.4f;
        /// <summary>The fire door of a shutter fills this much of the opening at one end: the double door frame (2.2 m) and a little play against the wall.</summary>
        public const float DoorBay = 2.3f;

        public sealed class Shutter
        {
            public string Id = "", ZoneA = "", ZoneB = "";
            public int Level;
            /// <summary>Middle of the opening on the floor, the unit vector along it and the one across it (pointing into zone A: the roll box side).</summary>
            public Vector3 Position, Tangent, Normal;
            public float Width, Height;
            /// <summary>The escape fire door in the end of the opening (floor, middle of the frame) and the ceiling height above the floor there; it faces the same way as the shutter.</summary>
            public Vector3 DoorPosition;
            public float DoorCeiling;
            public Vector3 ControlPosition, ControlNormal;
            public readonly List<DetectorLayout.Detector> Detectors = new List<DetectorLayout.Detector>();
        }

        public static List<Shutter> Plan(StationCeilings.Result survey, StationWalls walls, List<string> notes)
        {
            var byPosition = new Dictionary<(int, int, int), StationCeilings.Cell>();
            foreach (var c in survey.Cells.Where(c => c.Walkable)) byPosition.TryAdd((DetectorLayout.Level(c), c.X, c.Z), c);
            var edges = new Dictionary<(int level, string a, string b), List<Vector2>>();
            foreach (var pair in byPosition.OrderBy(p => p.Key.Item1).ThenBy(p => p.Key.Item2).ThenBy(p => p.Key.Item3))
            {
                var (level, x, z) = pair.Key;
                var cell = pair.Value;
                if (!DetectorLayout.Enclosed.Contains(cell.Zone)) continue;
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (!byPosition.TryGetValue((level, x + dx, z + dz), out var other) || other.Zone == cell.Zone || other.Zone.Length == 0 || Mathf.Abs(other.Floor.y - cell.Floor.y) > .6f) continue;
                    // 둘 다 실내인 경계는 한 번만(사전순 앞쪽 구역 쪽에서).
                    if (DetectorLayout.Enclosed.Contains(other.Zone) && string.CompareOrdinal(cell.Zone, other.Zone) > 0) continue;
                    var key = (level, cell.Zone, other.Zone);
                    if (!edges.TryGetValue(key, out var list)) edges[key] = list = new List<Vector2>();
                    list.Add(new Vector2(x + .5f + dx * .5f, z + .5f + dz * .5f));
                }
            }

            var found = new List<Shutter>();
            foreach (var group in edges.OrderBy(e => e.Key.level).ThenBy(e => e.Key.a, StringComparer.Ordinal).ThenBy(e => e.Key.b, StringComparer.Ordinal))
            {
                foreach (var cluster in Clusters(group.Value))
                {
                    float extentX = cluster.Max(p => p.x) - cluster.Min(p => p.x) + 1, extentZ = cluster.Max(p => p.y) - cluster.Min(p => p.y) + 1;
                    if (cluster.Count < 2) continue;
                    if (Mathf.Max(extentX, extentZ) > MaxGap) { notes.Add("open line " + group.Key.a + " | " + group.Key.b + " (" + Mathf.Max(extentX, extentZ).ToString("0") + " m, floor " + group.Key.level + "): no wall pair, left to the large-space rules"); continue; }
                    var shutter = Site(survey, walls, byPosition, group.Key.level, group.Key.a, group.Key.b, cluster, notes);
                    if (shutter == null) continue;
                    if (found.Exists(s => s.Level == shutter.Level && Vector3.Distance(s.Position, shutter.Position) < 3f)) continue;
                    found.Add(shutter);
                }
            }
            string[] levelCodes = { "1f", "2f", "3f" };
            // Numbers run 1, 2, 3 … on each floor.
            foreach (var level in found.Select(s => s.Level).Distinct().OrderBy(l => l))
            {
                int n = 0;
                foreach (var shutter in found.Where(s => s.Level == level).OrderBy(s => Mathf.Round(s.Position.x * 10)).ThenBy(s => Mathf.Round(s.Position.z * 10)))
                    shutter.Id = "fs-" + levelCodes[level] + "-" + (++n).ToString("00");
            }
            foreach (var shutter in found) { PlaceDoor(survey, shutter); AddDetectors(survey, shutter); }
            notes.Add("fire shutters: " + found.Count + " (each with a fire door of " + DoorBay.ToString("0.0") + " m at one end): " + string.Join(", ", found.Select(s => s.Id + " " + s.Width.ToString("0.0") + " m x " + s.Height.ToString("0.0") + " m " + s.ZoneA + "|" + s.ZoneB)));
            return found.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>Boundary edges joined into clusters (edges within 2 m of each other), in a fixed order.</summary>
        private static List<List<Vector2>> Clusters(List<Vector2> edges)
        {
            var pending = new HashSet<Vector2>(edges);
            var clusters = new List<List<Vector2>>();
            while (pending.Count > 0)
            {
                var start = pending.OrderBy(p => p.x).ThenBy(p => p.y).First();
                var queue = new Queue<Vector2>();
                var cluster = new List<Vector2>();
                queue.Enqueue(start);
                pending.Remove(start);
                while (queue.Count > 0)
                {
                    var p = queue.Dequeue();
                    cluster.Add(p);
                    foreach (var q in pending.Where(q => Mathf.Abs(q.x - p.x) <= 2f && Mathf.Abs(q.y - p.y) <= 2f).ToList()) { pending.Remove(q); queue.Enqueue(q); }
                }
                clusters.Add(cluster);
            }
            return clusters;
        }

        /// <summary>
        /// Building Act enforcement rules art. 14 (2) 4 가: an automatic fire shutter needs a separate 60-minute fire door within 3 m for people to get out. The door takes the end of the
        /// opening away from the control box (so its leaves do not hide the box) and the shutter closes the rest of the passage.
        /// </summary>
        private static void PlaceDoor(StationCeilings.Result survey, Shutter shutter)
        {
            float side = Vector3.Dot(shutter.ControlPosition - shutter.Position, shutter.Tangent) > 0 ? -1f : 1f;
            shutter.DoorPosition = shutter.Position + shutter.Tangent * (side * (shutter.Width - DoorBay) * .5f);
            shutter.Position -= shutter.Tangent * (side * DoorBay * .5f);
            shutter.Width -= DoorBay;
            var hit = survey.CeilingAt(shutter.DoorPosition.x, shutter.DoorPosition.z, shutter.Position.y);
            shutter.DoorCeiling = hit != null ? hit.Value.y - shutter.Position.y : shutter.Height;
        }

        /// <summary>The opening of one boundary gap: the ray angle (5° steps) whose two hits are walls square to it, the width, the ceiling, the wall beside it for the control box.</summary>
        private static Shutter Site(StationCeilings.Result survey, StationWalls walls, Dictionary<(int, int, int), StationCeilings.Cell> byPosition, int level, string zoneA, string zoneB, List<Vector2> cluster, List<string> notes)
        {
            var centre = new Vector2(cluster.Average(p => p.x), cluster.Average(p => p.y));
            var near = byPosition.Values.Where(c => DetectorLayout.Level(c) == level && c.Zone == zoneA).OrderBy(c => (new Vector2(c.Floor.x, c.Floor.z) - centre).sqrMagnitude).ThenBy(c => c.X).ThenBy(c => c.Z).FirstOrDefault();
            if (near == null) return null;
            float floorY = near.Floor.y;
            var origin = new Vector3(centre.x, floorY + 1.2f, centre.y);
            float bestScore = 0, bestP = 0, bestM = 0, bestAngle = 0;
            for (int step = 0; step < 36; step++)
            {
                float angle = step * 5f;
                var t = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad));
                if (!walls.Cast(origin, t, 0f, MaxWidth, out var p) || !walls.Cast(origin, -t, 0f, MaxWidth, out var m)) continue;
                float width = p.Distance + m.Distance;
                float square = Mathf.Min(Mathf.Abs(Vector3.Dot(p.Normal, t)), Mathf.Abs(Vector3.Dot(m.Normal, t)));
                if (width < MinWidth || width > MaxWidth || square < .85f) continue;
                if (square > bestScore + 1e-4f) { bestScore = square; bestP = p.Distance; bestM = m.Distance; bestAngle = angle; }
            }
            if (bestScore == 0) return null;
            var tangent = new Vector3(Mathf.Cos(bestAngle * Mathf.Deg2Rad), 0, Mathf.Sin(bestAngle * Mathf.Deg2Rad));
            var normal = Vector3.Cross(Vector3.up, tangent);
            var middle = origin + tangent * ((bestP - bestM) * .5f);
            // 기준선 앞(구역 A 쪽)을 향하게: 구역 A 칸의 무게중심 쪽.
            var sideA = byPosition.Values.Where(c => DetectorLayout.Level(c) == level && c.Zone == zoneA && Vector2.Distance(new Vector2(c.Floor.x, c.Floor.z), new Vector2(middle.x, middle.z)) < 6f).ToList();
            if (sideA.Count > 0 && Vector3.Dot(new Vector3(sideA.Average(c => c.Floor.x) - middle.x, 0, sideA.Average(c => c.Floor.z) - middle.z), normal) < 0) normal = -normal;
            var hit = survey.CeilingAt(middle.x, middle.z, floorY);
            float ceilingY = hit?.y ?? near.CeilingY;
            float height = ceilingY - floorY;
            if (height > MaxCeiling || height < 2.6f) { notes.Add("opening " + zoneA + "|" + zoneB + " at " + middle.ToString("F1") + " (" + (bestP + bestM).ToString("0.0") + " m): ceiling " + height.ToString("0.0") + " m, no shutter"); return null; }
            var shutter = new Shutter
            {
                Level = level, ZoneA = zoneA, ZoneB = zoneB, Position = new Vector3(middle.x, floorY, middle.z), Tangent = tangent, Normal = normal, Width = bestP + bestM, Height = height,
            };
            if (!FindControl(walls, shutter)) { notes.Add("opening " + zoneA + "|" + zoneB + " at " + middle.ToString("F1") + ": no wall face beside it for the control box"); return null; }
            return shutter;
        }

        /// <summary>
        /// The wall face a control box hangs on at 1.3 m: first the walls that bound the opening (a passage between two walls: the box on the wall a little way in front of or behind the
        /// curtain, facing into the passage), then the wall faces beside the opening when it is a gap in a wall line. Glass is no wall for a box.
        /// </summary>
        private static bool FindControl(StationWalls walls, Shutter shutter)
        {
            foreach (float along in new[] { .8f, 1.4f, 2.2f, 3f })
                foreach (float face in new[] { 1f, -1f })
                    foreach (float side in new[] { 1f, -1f })
                    {
                        var from = shutter.Position + Vector3.up * ControlHeight + shutter.Normal * (face * along);
                        var direction = shutter.Tangent * side;
                        if (!walls.Cast(from, direction, .05f, shutter.Width * .5f + 1.5f, out var hit) || Vector3.Dot(hit.Normal, -direction) < .9f || hit.Owner.Contains("Glass")) continue;
                        shutter.ControlPosition = hit.Point + hit.Normal * .003f;
                        shutter.ControlNormal = hit.Normal;
                        return true;
                    }
            foreach (float side in new[] { 1f, -1f })
                foreach (float face in new[] { 1f, -1f })
                    foreach (float along in new[] { .4f, .8f, 1.3f, 2f, 3f, 4f })
                    {
                        var q = shutter.Position + shutter.Tangent * (side * (shutter.Width * .5f + along)) + Vector3.up * ControlHeight;
                        var from = q + shutter.Normal * (face * 1.2f);
                        if (!walls.Cast(from, -shutter.Normal * face, .05f, 2.4f, out var hit) || Vector3.Dot(hit.Normal, shutter.Normal * face) < .9f || hit.Owner.Contains("Glass")) continue;
                        shutter.ControlPosition = hit.Point + hit.Normal * .003f;
                        shutter.ControlNormal = hit.Normal;
                        return true;
                    }
            return false;
        }

        /// <summary>A smoke and a heat detector on each side of the curtain, 1.4 m from its plane (art. 14 (2) 4 다), on the ceiling at the shutter's own height.</summary>
        private static void AddDetectors(StationCeilings.Result survey, Shutter shutter)
        {
            foreach (float side in new[] { 1f, -1f })
                for (int kind = 0; kind < 2; kind++)
                {
                    bool smoke = kind == 0;
                    float across = (smoke ? -1f : 1f) * shutter.Width * .25f * side;
                    var at = shutter.Position + shutter.Normal * (side * DetectorSide) + shutter.Tangent * across;
                    var hit = survey.CeilingAt(at.x, at.z, shutter.Position.y);
                    float ceiling = hit?.y ?? shutter.Position.y + shutter.Height;
                    float height = ceiling - shutter.Position.y;
                    shutter.Detectors.Add(new DetectorLayout.Detector
                    {
                        Ceiling = new Vector3(at.x, ceiling, at.z), Normal = hit?.normal ?? Vector3.down, Yaw = Mathf.Atan2(shutter.Tangent.x, shutter.Tangent.z) * Mathf.Rad2Deg, Height = height,
                        Area = smoke ? (height < 4f ? 150f : 75f) : 70f, Coverage = smoke ? 6f : 4f, Smoke = smoke,
                        ClassName = smoke ? (height < 4f ? "광전식 스포트형 2종" : "광전식 스포트형 1종") : "정온식 스포트형 특종",
                        ZoneId = shutter.ZoneA, Level = shutter.Level, Rule = "fire shutter " + shutter.Id, Room = "shutter", Shutter = shutter.Id,
                    });
                }
        }
    }
}
