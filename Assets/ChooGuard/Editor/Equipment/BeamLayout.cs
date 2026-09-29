using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Smoke detection under ceilings of 15–20 m (the atrium of the 2F hall): reflector-less beam detectors (광전식 분리형), a
    /// transmitter and a receiver on opposite walls with the optical axis across the space. NFTC 203 table 2.4.1 allows only type 1
    /// smoke (spot, beam, aspirating) or flame detectors from 15 m, 2.4.2.4 makes smoke detection mandatory there, and 2.4.3.15
    /// fixes the mounting: the axis at 80 % or more of the ceiling height (.4), 0.6 m or more from parallel walls (.2), the units
    /// within 1 m of their back wall (.3), the axis within the nominal monitoring distance (.5). The standard leaves the spacing
    /// between axes to the manufacturer: 18.3 m at most, the outermost axis within 9.144 m of the edge (Fireray 5000 data sheet
    /// 24-0318-04 and System Sensor guide BMAG240), the beam 0.5–0.6 m under the ceiling and 3 m or more over the floor; over 9.1 m
    /// a second layer of beams at a lower height catches smoke that stratifies under it (the National Fire Assessment Institute
    /// recommends extra beams at the stratification height; the height used here, half the ceiling height, is a placeholder that
    /// no source fixes). Axes run along the shorter side of the space, evenly spaced; the wall behind each unit is found by casting
    /// a ray along the axis against the vertical faces of the twin's meshes.
    /// </summary>
    internal static class BeamLayout
    {
        /// <summary>Detection width each side of an axis in metres: half the 18.3 m spacing limit (manufacturer value, not in NFTC 203).</summary>
        public const float HalfWidth = 9.144f;

        /// <summary>The upper beam runs this far under the ceiling (Fireray 5000: 0.5–0.6 m; NFTC 203 2.4.3.15.4 asks for 80 % of the ceiling height or more).</summary>
        public const float BelowCeiling = .6f;

        /// <summary>The lower layer's axis as a share of the ceiling height (placeholder, see the class summary).</summary>
        public const float LowerShare = .5f;

        /// <summary>Longest axis: nominal monitoring distance range of a wall pair (type-approval standard art. 19 allows up to 100 m).</summary>
        public const float MaxLength = 100f;

        public sealed class Beam
        {
            public Vector3 Transmitter, Receiver, TransmitterNormal, ReceiverNormal;
            public float AxisHeight, Length;
            /// <summary>"upper" (under the ceiling) or "lower" (stratification layer).</summary>
            public string Layer = "upper";
            public string ZoneId = "";
            public int Level;
        }

        /// <summary>Beams for every component of ceiling cells with mounting heights of 15 m to under 20 m.</summary>
        public static List<Beam> Plan(Scene station, List<StationCeilings.Cell> cells, List<string> notes)
        {
            var beams = new List<Beam>();
            if (cells.Count == 0) return beams;
            var walls = StationWalls.Collect(station, RegionOf(cells));
            foreach (var level in cells.GroupBy(c => c.Floor.y < 3f ? 0 : c.Floor.y < 9.5f ? 1 : 2))
                foreach (var area in Areas(level.ToList()))
                    PlanArea(area, level.Key, walls, beams, notes);
            return beams;
        }

        private static List<List<StationCeilings.Cell>> Areas(List<StationCeilings.Cell> cells)
        {
            var byPosition = cells.GroupBy(c => (c.X, c.Z)).ToDictionary(g => g.Key, g => g.First());
            var seen = new HashSet<(int, int)>();
            var areas = new List<List<StationCeilings.Cell>>();
            foreach (var start in cells.OrderBy(c => c.X).ThenBy(c => c.Z))
            {
                if (!seen.Add((start.X, start.Z))) continue;
                var area = new List<StationCeilings.Cell>();
                var queue = new Queue<(int, int)>();
                queue.Enqueue((start.X, start.Z));
                while (queue.Count > 0)
                {
                    var (x, z) = queue.Dequeue();
                    area.Add(byPosition[(x, z)]);
                    // 조명 띠·이음새로 끊긴 곳(2 m 이내)은 한 공간으로 본다.
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dz = -2; dz <= 2; dz++)
                            if (byPosition.ContainsKey((x + dx, z + dz)) && seen.Add((x + dx, z + dz))) queue.Enqueue((x + dx, z + dz));
                }
                if (area.Count >= 100) areas.Add(area);
            }
            return areas;
        }

        private static void PlanArea(List<StationCeilings.Cell> area, int level, StationWalls walls, List<Beam> beams, List<string> notes)
        {
            var centre = new Vector2(area.Average(c => c.Floor.x), area.Average(c => c.Floor.z));
            float sxx = 0, szz = 0, sxz = 0;
            foreach (var c in area) { float dx = c.Floor.x - centre.x, dz = c.Floor.z - centre.y; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
            float angle = Mathf.Round(.5f * Mathf.Atan2(2 * sxz, sxx - szz) * Mathf.Rad2Deg);
            var u = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var v = new Vector2(-u.y, u.x);
            float Extent(Vector2 axis, out float min) { min = float.MaxValue; float max = float.MinValue; foreach (var c in area) { float p = Vector2.Dot(new Vector2(c.Floor.x, c.Floor.z) - centre, axis); min = Mathf.Min(min, p - .5f); max = Mathf.Max(max, p + .5f); } return max - min; }
            float uSpan = Extent(u, out float uMin), vSpan = Extent(v, out float vMin);
            // 광축은 짧은 변 방향으로 뻗고, 축끼리는 긴 변을 따라 늘어선다.
            bool alongU = uSpan <= vSpan;
            var direction = alongU ? u : v;
            var across = alongU ? v : u;
            float acrossSpan = alongU ? vSpan : uSpan, acrossMin = alongU ? vMin : uMin;
            int count = Mathf.Max(1, Mathf.CeilToInt(acrossSpan / (2 * HalfWidth)));
            for (int k = 0; k < count; k++)
            {
                float offset = acrossMin + (k + .5f) * acrossSpan / count;
                foreach (bool lower in new[] { false, true })
                {
                    Beam beam = null;
                    // 그 자리의 벽이 마주 보지 않으면 옆으로 조금 옮겨 본다(±4.5 m 이내: 바깥 축은 가장자리에서 절반 간격 이내를 지킨다).
                    foreach (float shift in new[] { 0f, 1.5f, -1.5f, 3f, -3f, 4.5f, -4.5f })
                    {
                        beam = Cast(area, centre, direction, across, offset + shift, level, walls, lower);
                        if (beam != null) break;
                    }
                    if (beam == null) notes.Add((lower ? "lower" : "upper") + " beam axis " + (k + 1) + "/" + count + " of the " + area.Count + "-cell area at " + centre.ToString("F0") + ": no wall pair found");
                    else beams.Add(beam);
                }
            }
        }

        private static Beam Cast(List<StationCeilings.Cell> area, Vector2 centre, Vector2 direction, Vector2 across, float offset, int level, StationWalls walls, bool lower)
        {
            // 축 위 천장 칸(2 m 안)의 높이로 광축 높이를 정한다.
            var line = centre + across * offset;
            var near = area.Where(c => Mathf.Abs(Vector2.Dot(new Vector2(c.Floor.x, c.Floor.z) - line, across)) < 2f).ToList();
            if (near.Count == 0) return null;
            float floorY = near.Average(c => c.Floor.y), ceilingY = near.Min(c => c.CeilingY);
            float axis = lower ? floorY + LowerShare * (ceilingY - floorY) : ceilingY - BelowCeiling;
            // 축의 가운데: 그 줄의 천장 칸이 이어지는 구간의 중앙.
            float mid = near.Average(c => Vector2.Dot(new Vector2(c.Floor.x, c.Floor.z) - centre, direction));
            var origin2 = centre + direction * mid + across * offset;
            var origin = new Vector3(origin2.x, axis, origin2.y);
            var dir = new Vector3(direction.x, 0, direction.y);
            // 천장 칸이 끝나는 곳 앞의 가는 부재는 벽이 아니다. 그 뒤 첫 큰 면(벽·기둥)에 단다: 광축은 공칭감시거리(100 m)까지 뻗을 수 있다.
            float forwardEdge = near.Max(c => Vector2.Dot(new Vector2(c.Floor.x, c.Floor.z) - origin2, direction)), backEdge = -near.Min(c => Vector2.Dot(new Vector2(c.Floor.x, c.Floor.z) - origin2, direction));
            if (!walls.Cast(origin, dir, .75f * forwardEdge, MaxLength, out var forward) || !walls.Cast(origin, -dir, .75f * backEdge, MaxLength, out var back))
                return null;
            float length = forward.Distance + back.Distance;
            if (length > MaxLength || length < 8f) return null;
            // 벽면이 광축과 거의 수직이어야 뒷벽에 설치한 것이다.
            if (Vector3.Dot(forward.Normal, -dir) < .85f || Vector3.Dot(back.Normal, dir) < .85f) return null;
            var transmitter = back.Point + back.Normal * .04f;
            var receiver = forward.Point + forward.Normal * .04f;
            return new Beam
            {
                Transmitter = transmitter, Receiver = receiver, TransmitterNormal = back.Normal, ReceiverNormal = forward.Normal,
                AxisHeight = axis - floorY, Length = length, Level = level, Layer = lower ? "lower" : "upper",
            };
        }

        /// <summary>The volume the beam layout looks for walls in: the area's footprint plus 70 m, from 6 m above the lowest floor to 4 m over the highest ceiling.</summary>
        private static Bounds RegionOf(List<StationCeilings.Cell> cells)
        {
            var min = new Vector3(cells.Min(c => c.Floor.x) - 70, cells.Min(c => c.Floor.y) + 6, cells.Min(c => c.Floor.z) - 70);
            var max = new Vector3(cells.Max(c => c.Floor.x) + 70, cells.Max(c => c.CeilingY) + 4, cells.Max(c => c.Floor.z) + 70);
            return new Bounds((min + max) * .5f, max - min);
        }
    }
}
