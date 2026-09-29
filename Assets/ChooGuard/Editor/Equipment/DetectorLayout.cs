using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Where the ceiling detectors of the station go, by NFTC 203 (자동화재탐지설비, 2026-03-01), from the ceiling cells of
    /// <see cref="StationCeilings"/>. Pure function of the survey: the same twin gives the same detectors.
    /// <list type="bullet">
    /// <item>Smoke spot detectors (photoelectric): one per 150 m² under 4 m (class 2), one per 75 m² from 4 m to under 20 m (class 1) — tables 2.4.1 and 2.4.3.10.1. Ceilings of 20 m and more get none (2.4.5.1: only flame or analogue beam detectors would do).</item>
    /// <item>Rooms: N = ceil(area / A) detectors on the grid of a rectangle laid along the room's main axis with cells as square as possible, each moved to the nearest cell of ceiling that is 0.6 m or more from walls and beams (2.4.3.10.5) and carries no lamp; then every floor point is checked to lie within the half diagonal of the detection area of a detector, and more are added where not.</item>
    /// <item>Corridors and passages (narrow, long): one per 30 m of walking distance, first at half a span from the end (2.4.3.10.2).</item>
    /// <item>Covered but open-sided spaces (the platforms under the concourse deck, the exit deck): nothing within 5 m of the open side (2.4.5.2 exempts spaces open to the air; 2.1.3's 5 m rule is used by analogy [I]). Outdoors: nothing.</item>
    /// <item>Escalator slopes need a smoke detector (2.4.2.1): one at the ceiling above the upper landing. Kitchens of food shops: a fixed-temperature spot heat detector, special class, 70 m² under 4 m (2.4.3.4, table 2.4.3.5, 2.4.5.7).</item>
    /// </list>
    /// </summary>
    internal static class DetectorLayout
    {
        public sealed class Detector
        {
            public Vector3 Ceiling, Normal;
            public float Yaw, Height, Area, Coverage;
            public bool Smoke = true;
            public string ClassName = "", ZoneId = "", Rule = "";
            public int Level;
        }

        /// <summary>One row of the evidence table: a homogeneous region (level, mounting-height band) against the standard's minimum.</summary>
        public sealed class RegionReport
        {
            public string Name = "";
            public int Cells, Components, Placed, Minimum, Uncovered;
            public float FloorArea, AreaPerDetector;
        }

        private static readonly HashSet<string> Enclosed = new HashSet<string> { "hall2f", "main2f", "southgate", "eastexit", "upper3f", "ground1f" };
        private const float SmokeArea4 = 150f, SmokeArea20 = 75f, ExposureMetres = 5f, WallClear = 1.1f, CorridorWalk = 30f, CeilingLimit = 20f, MinComponentCells = 6;

        private static int Level(StationCeilings.Cell c) => c.Floor.y < 3f ? 0 : c.Floor.y < 9.5f ? 1 : 2;
        private static int Band(float h) => h < 4f ? 0 : h < 8f ? 1 : h < 15f ? 2 : 3;
        private static readonly string[] BandNames = { "<4 m", "4-8 m", "8-15 m", "15-20 m" };
        private static readonly string[] LevelNames = { "1F/platform", "2F", "3F" };

        public static List<Detector> Plan(StationCeilings.Result survey, StationPoints points, List<RegionReport> report, List<string> notes)
        {
            var context = new Context(survey);
            var open = new HashSet<(int, int, int)>(survey.Open.Select(c => (c.X, c.Z, Level(c))));
            var accepted = survey.Cells.Where(c => Accept(c, open, points)).ToList();
            var detectors = new List<Detector>();
            int fragments = 0, fragmentCells = 0;
            foreach (var group in accepted.GroupBy(c => (level: Level(c), band: Band(c.Height))).OrderBy(g => g.Key.level).ThenBy(g => g.Key.band))
            {
                var region = new RegionReport { Name = LevelNames[group.Key.level] + " " + BandNames[group.Key.band], Cells = group.Count() };
                var components = Components(group.ToList());
                foreach (var component in components)
                {
                    if (component.Real.Count < MinComponentCells) { fragments++; fragmentCells += component.Real.Count; continue; }
                    region.Components++;
                    PlaceComponent(context, component, group.Key.level, group.Key.band, detectors, region, notes);
                }
                report.Add(region);
            }
            notes.Add("left out " + fragments + " ceiling fragments of under " + MinComponentCells + " cells each (" + fragmentCells + " cells in all: slivers where two ceilings meet)");
            AddEscalatorDetectors(context, survey, points, detectors, notes);
            AddKitchenDetectors(context, survey, points, detectors, notes);
            return detectors;
        }

        private static bool Accept(StationCeilings.Cell c, HashSet<(int, int, int)> open, StationPoints points)
        {
            if (c.Height >= CeilingLimit) return false;
            if (Enclosed.Contains(c.Zone)) return true;
            if (c.Zone != "northdeck" && c.Zone != "tracks") return false;
            // 승강장은 승강장 띠 위 지상 바닥만, 출구 데크는 덮인 곳만. 열린 쪽에서 5 m 안은 뺀다.
            if (c.Zone == "tracks" && (Level(c) != 0 || points.PlatformAt(c.Floor, .3f) == null)) return false;
            int level = Level(c), reach = Mathf.CeilToInt(ExposureMetres);
            for (int dx = -reach; dx <= reach; dx++)
                for (int dz = -reach; dz <= reach; dz++)
                    if (dx * dx + dz * dz <= ExposureMetres * ExposureMetres && open.Contains((c.X + dx, c.Z + dz, level))) return false;
            return true;
        }

        /// <summary>The survey plus the lamp-free mounting spot of each ceiling cell (asked for lazily: only cells a detector may go on).</summary>
        private sealed class Context
        {
            private readonly StationCeilings.Result survey;
            private readonly Dictionary<StationCeilings.Cell, Vector2?> spots = new Dictionary<StationCeilings.Cell, Vector2?>();

            public Context(StationCeilings.Result survey) { this.survey = survey; }

            /// <summary>The point inside the cell nearest its centre that is 0.3 m clear of every lamp panel (0.15 m steps), or null when lamps cover the cell.</summary>
            public Vector2? Spot(StationCeilings.Cell cell)
            {
                if (spots.TryGetValue(cell, out var known)) return known;
                Vector2? found = null;
                float best = float.MaxValue;
                for (int i = -3; i <= 3; i++)
                    for (int j = -3; j <= 3; j++)
                    {
                        float d = (i * i + j * j) * .0225f;
                        if (d >= best || survey.LampNear(cell.Floor.x + i * .15f, cell.Floor.z + j * .15f, cell.CeilingY, .3f)) continue;
                        best = d;
                        found = new Vector2(cell.Floor.x + i * .15f, cell.Floor.z + j * .15f);
                    }
                return spots[cell] = found;
            }

            /// <summary>The ceiling point a detector on this cell is mounted at.</summary>
            public Vector3 Mount(StationCeilings.Cell cell)
            {
                var spot = Spot(cell) ?? new Vector2(cell.Floor.x, cell.Floor.z);
                return new Vector3(spot.x, cell.CeilingY, spot.y);
            }
        }

        // ── 구성 요소 ──

        private sealed class Component
        {
            public readonly List<StationCeilings.Cell> Real = new List<StationCeilings.Cell>();
            public readonly HashSet<(int x, int z)> Closed = new HashSet<(int, int)>();
        }

        private static readonly (int dx, int dz)[] Around = { (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1) };

        /// <summary>Connected areas of one level and height band; slots up to two metres wide (lamp strips, joints) do not split an area.</summary>
        private static List<Component> Components(List<StationCeilings.Cell> cells)
        {
            // 같은 칸에 층이 둘(경사로·계단참)이면 낮은 천장 쪽을 본다.
            var byPosition = cells.GroupBy(c => (c.X, c.Z)).ToDictionary(g => g.Key, g => g.OrderBy(c => c.CeilingY).First());
            cells = byPosition.Values.OrderBy(c => c.X).ThenBy(c => c.Z).ToList();
            var dilated = new HashSet<(int, int)>(byPosition.Keys);
            foreach (var key in byPosition.Keys) foreach (var (dx, dz) in Around) dilated.Add((key.Item1 + dx, key.Item2 + dz));
            var closed = new HashSet<(int, int)>(dilated.Where(p => Around.All(a => dilated.Contains((p.Item1 + a.dx, p.Item2 + a.dz)))));
            closed.UnionWith(byPosition.Keys);
            var components = new List<Component>();
            var seen = new HashSet<(int, int)>();
            foreach (var start in cells)
            {
                if (!seen.Add((start.X, start.Z))) continue;
                var component = new Component();
                var queue = new Queue<(int x, int z)>();
                queue.Enqueue((start.X, start.Z));
                component.Closed.Add((start.X, start.Z));
                while (queue.Count > 0)
                {
                    var (x, z) = queue.Dequeue();
                    byPosition.TryGetValue((x, z), out var here);
                    if (here != null) component.Real.Add(here);
                    foreach (var (dx, dz) in Around)
                    {
                        var next = (x + dx, z + dz);
                        if (!closed.Contains(next) || component.Closed.Contains(next)) continue;
                        // 천장 높이가 이어지지 않는 곳(단차)은 다른 구역이다. 메운 칸은 어느 쪽에나 이어진다.
                        if (here != null && byPosition.TryGetValue(next, out var there) && Mathf.Abs(there.CeilingY - here.CeilingY) > .6f) continue;
                        component.Closed.Add(next);
                        seen.Add(next);
                        queue.Enqueue(next);
                    }
                }
                components.Add(component);
            }
            return components;
        }

        // ── 배치 ──

        private static void PlaceComponent(Context context, Component component, int level, int band, List<Detector> detectors, RegionReport region, List<string> notes)
        {
            float mount = component.Real.Average(c => c.Height);
            float area = band == 0 ? SmokeArea4 : SmokeArea20;
            string className = band == 0 ? "광전식 스포트형 2종" : "광전식 스포트형 1종";
            float reach = Mathf.Sqrt(area / 2f);
            var placeable = component.Real.Where(c => Clearance(c, component.Closed) >= WallClear).ToList();
            if (placeable.Count == 0) { notes.Add("no free ceiling for a detector in a component of " + component.Real.Count + " cells at " + component.Real[0].Floor.ToString("F0")); return; }
            float floorArea = component.Real.Count;
            region.FloorArea += floorArea;

            // 주축을 따라 놓은 직사각형: u = 주축, v = 그 직각.
            var centre = new Vector2(component.Real.Average(c => c.Floor.x), component.Real.Average(c => c.Floor.z));
            float sxx = 0, szz = 0, sxz = 0;
            foreach (var c in component.Real) { float dx = c.Floor.x - centre.x, dz = c.Floor.z - centre.y; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
            float angle = Mathf.Round(.5f * Mathf.Atan2(2 * sxz, sxx - szz) * Mathf.Rad2Deg);
            var u = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var v = new Vector2(-u.y, u.x);
            float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
            foreach (var c in component.Real)
            {
                var p = new Vector2(c.Floor.x - centre.x, c.Floor.z - centre.y);
                float pu = Vector2.Dot(p, u), pv = Vector2.Dot(p, v);
                u0 = Mathf.Min(u0, pu - .5f); u1 = Mathf.Max(u1, pu + .5f); v0 = Mathf.Min(v0, pv - .5f); v1 = Mathf.Max(v1, pv + .5f);
            }
            float length = u1 - u0, width = v1 - v0;
            float maxClear = component.Real.Max(c => Clearance(c, component.Closed));
            bool corridor = maxClear <= 2.6f && length >= CorridorWalk * .4f && length >= 3 * width;

            var nodes = new List<Vector2>();
            int minimum;
            float snap;
            if (corridor)
            {
                minimum = Mathf.Max(1, Mathf.CeilToInt(length / CorridorWalk));
                for (int k = 0; k < minimum; k++) nodes.Add(centre + u * (u0 + (k + .5f) * length / minimum) + v * ((v0 + v1) * .5f));
                reach = CorridorWalk * .5f;
                snap = length / minimum * .5f + 2f;
            }
            else
            {
                minimum = Mathf.Max(1, Mathf.CeilToInt(floorArea / area));
                float side = Mathf.Sqrt(area);
                if (floorArea >= .75f * length * width)
                {
                    // 반듯한 방: 주축을 따라 놓은 격자, 칸 한 변이 √A 이하(칸 넓이 ≤ 담당면적).
                    int nx = Mathf.Max(1, Mathf.CeilToInt(length / side - .05f)), ny = Mathf.Max(1, Mathf.CeilToInt(width / side - .05f));
                    for (int i = 0; i < nx; i++)
                        for (int j = 0; j < ny; j++)
                            nodes.Add(centre + u * (u0 + (i + .5f) * length / nx) + v * (v0 + (j + .5f) * width / ny));
                    snap = .5f * Mathf.Max(length / nx, width / ny) + .5f;
                }
                else
                {
                    // 굽은·구멍 난 구역: 바닥을 N 개로 고르게 나눈 조각의 무게중심(Lloyd)에 둔다. 조각마다 담당면적 이하다.
                    nodes.AddRange(Centroids(component.Real, minimum));
                    snap = .6f * side;
                }
            }

            var placed = new List<StationCeilings.Cell>();
            foreach (var node in nodes)
            {
                // 격자 칸 가운데가 빈 곳(구멍·불규칙한 가장자리)이면 가까운 천장 칸까지만 옮긴다. 없으면 아래 보충 단계가 채운다.
                var pick = PickFree(context, placeable.Where(c => !placed.Contains(c)), node, snap);
                if (pick != null) placed.Add(pick);
            }
            // 담당 구역 끝까지 닿지 않는 곳: 가장 먼 바닥 점 가까이에 더 단다.
            for (int guard = 0; guard < 400; guard++)
            {
                var far = Farthest(component.Real, placed);
                if (far.cell == null || far.distance <= reach) break;
                var pick = PickFree(context, placeable.Where(c => !placed.Contains(c)), new Vector2(far.cell.Floor.x, far.cell.Floor.z), reach * .6f);
                if (pick == null) { region.Uncovered++; notes.Add("floor point beyond reach with no free ceiling near: " + far.cell.Floor.ToString("F0")); break; }
                placed.Add(pick);
            }
            // 표의 면적 기준: 바닥면적 ÷ 담당면적 이상.
            var blocked = new HashSet<StationCeilings.Cell>();
            while (!corridor && placed.Count < minimum)
            {
                var far = Farthest(placeable.Where(c => !placed.Contains(c) && !blocked.Contains(c)).ToList(), placed);
                if (far.cell == null) break;
                if (context.Spot(far.cell) == null) blocked.Add(far.cell); else placed.Add(far.cell);
            }
            region.Minimum += minimum;
            region.Placed += placed.Count;
            foreach (var cell in placed)
                detectors.Add(new Detector
                {
                    Ceiling = context.Mount(cell), Normal = cell.Normal, Yaw = angle, Height = cell.Height, Area = area, Coverage = reach,
                    ClassName = className, ZoneId = cell.Zone, Level = level,
                    Rule = corridor ? "corridor 30 m" : "area " + area + " m2",
                });
            region.AreaPerDetector = region.Placed > 0 ? region.FloorArea / region.Placed : 0;
        }

        /// <summary>Centres of <paramref name="k"/> equal-area pieces of the floor (Lloyd's algorithm from a farthest-point start, deterministic).</summary>
        private static List<Vector2> Centroids(List<StationCeilings.Cell> cells, int k)
        {
            var points = cells.OrderBy(c => c.X).ThenBy(c => c.Z).Select(c => new Vector2(c.Floor.x, c.Floor.z)).ToArray();
            var centres = new List<Vector2> { points[0] };
            var nearest = points.Select(p => (p - points[0]).sqrMagnitude).ToArray();
            while (centres.Count < k && centres.Count < points.Length)
            {
                int far = 0;
                for (int i = 1; i < points.Length; i++) if (nearest[i] > nearest[far] + 1e-4f) far = i;
                centres.Add(points[far]);
                for (int i = 0; i < points.Length; i++) nearest[i] = Mathf.Min(nearest[i], (points[i] - points[far]).sqrMagnitude);
            }
            var owner = new int[points.Length];
            for (int round = 0; round < 12; round++)
            {
                var sum = new Vector2[centres.Count];
                var count = new int[centres.Count];
                for (int i = 0; i < points.Length; i++)
                {
                    int best = 0;
                    float bestD = float.MaxValue;
                    for (int c = 0; c < centres.Count; c++)
                    {
                        float d = (points[i] - centres[c]).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = c; }
                    }
                    owner[i] = best;
                    sum[best] += points[i];
                    count[best]++;
                }
                for (int c = 0; c < centres.Count; c++) if (count[c] > 0) centres[c] = sum[c] / count[c];
            }
            return centres;
        }

        /// <summary>Distance from the cell centre to the nearest cell outside the area (a wall, a step in the ceiling), metres; 3 when farther.</summary>
        private static float Clearance(StationCeilings.Cell cell, HashSet<(int x, int z)> closed)
        {
            float best = 3f;
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                    if (!closed.Contains((cell.X + dx, cell.Z + dz))) best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            return best;
        }

        /// <summary>The nearest cell within <paramref name="within"/> metres of <paramref name="point"/> that has a lamp-free spot (ties by position, so the pick does not depend on list order).</summary>
        private static StationCeilings.Cell PickFree(Context context, IEnumerable<StationCeilings.Cell> cells, Vector2 point, float within)
        {
            float limit = within * within;
            foreach (var c in cells.Select(c => (cell: c, d: (new Vector2(c.Floor.x, c.Floor.z) - point).sqrMagnitude)).Where(t => t.d < limit).OrderBy(t => t.d).ThenBy(t => t.cell.X).ThenBy(t => t.cell.Z))
                if (context.Spot(c.cell) != null) return c.cell;
            return null;
        }

        private static (StationCeilings.Cell cell, float distance) Farthest(List<StationCeilings.Cell> cells, List<StationCeilings.Cell> placed)
        {
            StationCeilings.Cell far = null;
            float farDistance = -1;
            foreach (var c in cells)
            {
                float nearest = float.MaxValue;
                foreach (var p in placed) nearest = Mathf.Min(nearest, (new Vector2(c.Floor.x, c.Floor.z) - new Vector2(p.Floor.x, p.Floor.z)).magnitude);
                if (placed.Count == 0) nearest = float.MaxValue / 2;
                if (nearest > farDistance + 1e-4f) { farDistance = nearest; far = c; }
            }
            return (far, farDistance);
        }

        // ── 에스컬레이터·주방 ──

        private static void AddEscalatorDetectors(Context context, StationCeilings.Result survey, StationPoints points, List<Detector> detectors, List<string> notes)
        {
            var byPosition = survey.Cells.GroupBy(c => (c.X, c.Z)).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var escalator in points.Escalators.OrderBy(e => e.id, StringComparer.Ordinal))
            {
                if (escalator.path == null || escalator.path.Length == 0) continue;
                var top = escalator.path.OrderByDescending(p => p.y).First();
                StationCeilings.Cell pick = null;
                float best = 9f;
                for (int dx = -3; dx <= 3; dx++)
                    for (int dz = -3; dz <= 3; dz++)
                    {
                        if (!byPosition.TryGetValue((Mathf.FloorToInt(top.x) + dx, Mathf.FloorToInt(top.z) + dz), out var list)) continue;
                        foreach (var c in list)
                        {
                            if (Mathf.Abs(c.Floor.y - top.y) > .8f || context.Spot(c) == null || c.Height >= CeilingLimit || !(Enclosed.Contains(c.Zone) || c.Zone == "northdeck" || c.Zone == "tracks")) continue;
                            float d = new Vector2(c.Floor.x - top.x, c.Floor.z - top.z).sqrMagnitude;
                            if (d < best * best) { best = Mathf.Sqrt(d); pick = c; }
                        }
                    }
                if (pick == null) { notes.Add(escalator.id + ": no ceiling over the upper landing"); continue; }
                var here = context.Mount(pick);
                if (detectors.Exists(d => d.Smoke && Mathf.Abs(d.Ceiling.y - here.y) < 1f && new Vector2(d.Ceiling.x - here.x, d.Ceiling.z - here.z).magnitude < 6f)) continue;
                detectors.Add(new Detector
                {
                    Ceiling = here, Normal = pick.Normal, Height = pick.Height, Area = pick.Height < 4f ? SmokeArea4 : SmokeArea20, Coverage = Mathf.Sqrt((pick.Height < 4f ? SmokeArea4 : SmokeArea20) / 2f),
                    ClassName = pick.Height < 4f ? "광전식 스포트형 2종" : "광전식 스포트형 1종", ZoneId = pick.Zone, Level = Level(pick), Rule = "escalator slope 2.4.2.1",
                });
            }
        }

        private static void AddKitchenDetectors(Context context, StationCeilings.Result survey, StationPoints points, List<Detector> detectors, List<string> notes)
        {
            foreach (var shop in points.Of(PointKind.Shop).Where(s => Kitchen(s.Label)).OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                // 매장 점은 카운터 앞 1.2 m 이다. 주방은 카운터 뒤(매장 안쪽)로 본다.
                var inside = shop.Position + Quaternion.Euler(0, shop.Yaw, 0) * Vector3.forward * 2.2f;
                StationCeilings.Cell pick = null;
                float best = 4f * 4f;
                foreach (var c in survey.Cells)
                {
                    if (Mathf.Abs(c.Floor.y - shop.Position.y) > .8f || c.Height >= 4f) continue;
                    float d = new Vector2(c.Floor.x - inside.x, c.Floor.z - inside.z).sqrMagnitude;
                    if (d < best && context.Spot(c) != null) { best = d; pick = c; }
                }
                if (pick == null) { notes.Add("kitchen of '" + shop.Label + "': no low ceiling within 4 m of " + inside.ToString("F0")); continue; }
                detectors.Add(new Detector
                {
                    Ceiling = context.Mount(pick), Normal = pick.Normal, Height = pick.Height, Area = 70f, Coverage = Mathf.Sqrt(70f / 2f), Smoke = false,
                    ClassName = "정온식 스포트형 특종", ZoneId = pick.Zone, Level = Level(pick), Rule = "kitchen " + shop.Label,
                });
            }
        }

        /// <summary>Food shops with a kitchen (same words as IncidentDirector.Kitchens).</summary>
        private static bool Kitchen(string label)
        {
            foreach (var word in new[] { "닭강정", "어묵", "도넛", "도나스", "떡볶이", "김밥", "한식", "명가", "SUBWAY", "제과", "단팥빵", "떡공방" }) if (label.Contains(word)) return true;
            return false;
        }
    }
}
