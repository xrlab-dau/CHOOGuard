using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Where the fire detectors of the station go, by NFTC 203 (자동화재탐지설비, 2026-03-01), from the ceiling cells of
    /// <see cref="StationCeilings"/>. Pure function of the survey: the same twin gives the same detectors.
    /// <list type="bullet">
    /// <item>Smoke spot detectors (photoelectric): one per 150 m² under 4 m (class 2), one per 75 m² from 4 m to under 15 m (class 1) — tables 2.4.1 and 2.4.3.10.1.</item>
    /// <item>15 m to under 20 m (the atrium of the 2F hall): beam detectors on opposite walls (<see cref="BeamLayout"/>); 20 m and more would need flame or analogue beam detectors (2.4.5.1) and does not occur in the twin.</item>
    /// <item>Rooms: N = ceil(area / A) on a grid along the room's main axis (or on equal-area pieces of an irregular room), each moved to the nearest spot 0.65 m or more from walls and beams (2.4.3.10.5) and 0.3 m clear of lamp panels; every floor point is then checked to lie within 1.3 times the half diagonal of the detection area of a detector, and detectors are added where not (the audit).</item>
    /// <item>Corridors and passages (narrow, long): one per 30 m of walking distance (2.4.3.10.2).</item>
    /// <item>Open platforms, the exit deck and every other space open to the air (canopies and piloti): no detectors — NFTC 203 2.4.5.2 exempts "헛간 등 외부와 기류가 통하는 장소로서 감지기에 따라 화재 발생을 유효하게 감지할 수 없는 장소", and the commentary reads canopies and piloti as such places unless there is a special reason. The enclosed rooms (1F, 2F, 3F, shops, toilets) get detectors.</item>
    /// <item>Escalator slopes need a smoke detector (2.4.2.1): one at the ceiling above the upper landing where the landing is inside. Toilet rooms (no shower, so 2.4.5.5 does not exempt them): one smoke detector each, near the entrance (2.4.3.10.3). Kitchens of food shops (<see cref="ChooGuard.App.Fps.Emergency.IncidentDirector.KitchenOf"/>): a fixed-temperature spot heat detector, special class, 70 m² under 4 m (2.4.3.4, table 2.4.3.5); smoke detectors are exempt there (2.4.5.7).</item>
    /// </list>
    /// </summary>
    internal static class DetectorLayout
    {
        public sealed class Detector
        {
            public Vector3 Ceiling, Normal;
            public float Yaw, Height, Area, Coverage;
            public bool Smoke = true;
            public string ClassName = "", ZoneId = "", Rule = "", Room = "";
            /// <summary>Id of the fire shutter this detector closes (a detector on either side of a curtain), or empty.</summary>
            public string Shutter = "";
            public int Level;
        }

        /// <summary>One row of the evidence table: a homogeneous region (level, mounting-height band) against the standard's minimum.</summary>
        public sealed class RegionReport
        {
            public string Name = "";
            public int Cells, Components, Placed, Minimum, RegionMinimum, Uncovered;
            public float FloorArea, AreaPerDetector;
        }

        internal static readonly HashSet<string> Enclosed = new HashSet<string> { "hall2f", "main2f", "southgate", "eastexit", "upper3f", "ground1f" };
        private const float SmokeArea4 = 150f, SmokeArea20 = 75f, WallClear = .65f, CorridorWalk = 30f, CeilingLimit = 20f, MinComponentCells = 6;

        internal static int Level(StationCeilings.Cell c) => c.Floor.y < 3f ? 0 : c.Floor.y < 9.5f ? 1 : 2;
        private static int Band(float h) => h < 4f ? 0 : h < 8f ? 1 : h < 15f ? 2 : 3;
        private static readonly string[] BandNames = { "<4 m", "4-8 m", "8-15 m", "15-20 m" };
        private static readonly string[] LevelNames = { "1F/platform", "2F", "3F" };

        public static List<Detector> Plan(Scene station, StationCeilings.Result survey, StationPoints points, List<RegionReport> report, List<string> notes, out List<BeamLayout.Beam> beams)
        {
            var context = new Context(survey);
            var accepted = survey.Cells.Where(Accept).ToList();
            foreach (var g in survey.Cells.Where(c => !Enclosed.Contains(c.Zone) && c.Height < CeilingLimit && c.Zone.Length > 0).GroupBy(c => c.Zone).OrderBy(g => g.Key))
                notes.Add("exempt 2.4.5.2 (open to the air: canopies, piloti, outdoors): zone " + g.Key + " " + g.Count() + " cells");
            var detectors = new List<Detector>();
            var orphans = new List<StationCeilings.Cell>();
            foreach (var group in accepted.Where(c => Band(c.Height) < 3).GroupBy(c => (level: Level(c), band: Band(c.Height))).OrderBy(g => g.Key.level).ThenBy(g => g.Key.band))
            {
                var region = new RegionReport { Name = LevelNames[group.Key.level] + " " + BandNames[group.Key.band], Cells = group.Count() };
                foreach (var component in Components(group.ToList()))
                {
                    if (component.Real.Count < MinComponentCells || !PlaceComponent(context, component, group.Key.level, group.Key.band, detectors, region, notes)) { orphans.AddRange(component.Real); continue; }
                    region.Components++;
                }
                report.Add(region);
            }
            var atrium = accepted.Where(c => Band(c.Height) == 3).ToList();
            beams = BeamLayout.Plan(station, atrium, notes);
            // 벽 한 쌍을 찾지 못한 광축 자리: 광전식 스포트형 1종도 15 m 이상 20 m 미만에 쓸 수 있다(표 2.4.1). 광축이 닿지 않는 바닥에만 단다.
            var placedBeams = beams;
            var beyond = atrium.Where(c => !placedBeams.Exists(b => SegmentDistance(new Vector2(c.Floor.x, c.Floor.z), new Vector2(b.Transmitter.x, b.Transmitter.z), new Vector2(b.Receiver.x, b.Receiver.z)) <= BeamLayout.HalfWidth)).ToList();
            int beyondCells = beyond.Count;
            var fallback = new RegionReport { Name = "2F 15-20 m spot fallback (no wall for a beam)", Cells = beyondCells };
            int before = detectors.Count;
            foreach (var component in beyondCells > 0 ? Components(beyond) : new List<Component>())
                if (component.Real.Count >= MinComponentCells && PlaceComponent(context, component, Level(component.Real[0]), 3, detectors, fallback, notes)) fallback.Components++;
            for (int i = before; i < detectors.Count; i++) detectors[i].Rule += " (fallback: no wall for a beam)";
            if (beyondCells > 0) { fallback.Placed = detectors.Count - before; fallback.FloorArea = beyondCells; fallback.RegionMinimum = Mathf.CeilToInt(beyondCells / SmokeArea20); fallback.AreaPerDetector = fallback.Placed > 0 ? beyondCells / (float)fallback.Placed : 0; report.Add(fallback); }
            Audit(context, accepted.Where(c => Band(c.Height) < 3).Concat(beyond).ToList(), detectors, notes);
            fallback.Uncovered = beyond.Count(c => !Covered(c, detectors));
            AddToiletDetectors(context, survey, points, detectors, notes);
            AddEscalatorDetectors(context, survey, points, detectors, notes);
            AddKitchenDetectors(context, survey, points, detectors, notes);
            // 지역별 표: 바닥면적을 담당면적으로 나눈 최소 수와 실제 수(스포트형 연기감지기, 감사·화장실·에스컬레이터 추가분 포함).
            foreach (var region in report.Where(r => !r.Name.Contains("fallback")))
            {
                var cells = accepted.Where(c => Band(c.Height) < 3 && LevelNames[Level(c)] + " " + BandNames[Band(c.Height)] == region.Name).ToList();
                float area = region.Name.EndsWith("<4 m", StringComparison.Ordinal) ? SmokeArea4 : SmokeArea20;
                region.FloorArea = cells.Count;
                region.RegionMinimum = Mathf.CeilToInt(cells.Count / area);
                region.Placed = detectors.Count(d => d.Smoke && LevelNames[d.Level] + " " + BandNames[Band(d.Height)] == region.Name);
                region.AreaPerDetector = region.Placed > 0 ? region.FloorArea / region.Placed : 0;
                region.Uncovered = cells.Count(c => !Covered(c, detectors));
            }
            return detectors;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static bool Accept(StationCeilings.Cell c) => c.Height < CeilingLimit && Enclosed.Contains(c.Zone);

        /// <summary>Reach of a detection area as used by the layout: 1.3 times the half diagonal of its square.</summary>
        private static float ReachOf(float height) => Mathf.Sqrt((height < 4f ? SmokeArea4 : SmokeArea20) / 2f) * 1.3f;

        private static bool Covered(StationCeilings.Cell cell, List<Detector> detectors)
        {
            float reach = ReachOf(cell.Height);
            foreach (var d in detectors)
            {
                if (!d.Smoke || d.Level != Level(cell) || Mathf.Abs(d.Ceiling.y - cell.CeilingY) > 8f) continue;
                if (new Vector2(d.Ceiling.x - cell.Floor.x, d.Ceiling.z - cell.Floor.z).magnitude <= Mathf.Max(reach, d.Coverage)) return true;
            }
            return false;
        }

        /// <summary>
        /// Every accepted floor cell — also the slivers and small areas that got no detector of their own — must lie within reach of a
        /// detector on the same level. A detector goes on the nearest ceiling spot within 0.9 times the reach that keeps the wall and lamp clearances.
        /// </summary>
        private static void Audit(Context context, List<StationCeilings.Cell> cells, List<Detector> detectors, List<string> notes)
        {
            int added = 0, stuck = 0;
            foreach (var cell in cells.OrderBy(c => c.X).ThenBy(c => c.Z))
            {
                if (Covered(cell, detectors)) continue;
                StationCeilings.Cell pick = null;
                // 그 칸이 닿는 거리(reach) 안이면 어느 천장 칸에 달아도 그 칸을 담당한다: 가장 가까운 자리를 고른다.
                float best = Mathf.Pow(.9f * ReachOf(cell.Height), 2);
                foreach (var other in cells)
                {
                    float d = new Vector2(other.Floor.x - cell.Floor.x, other.Floor.z - cell.Floor.z).sqrMagnitude;
                    if (d < best && Level(other) == Level(cell) && other.Zone == cell.Zone && Mathf.Abs(other.CeilingY - cell.CeilingY) < 8f && context.Spot(other) != null) { best = d; pick = other; }
                }
                if (pick == null) { stuck++; notes.Add("audit: floor cell at " + cell.Floor.ToString("F0") + " has no ceiling spot within reach that keeps the wall and lamp clearances"); continue; }
                var mount = context.Mount(pick);
                float area = pick.Height < 4f ? SmokeArea4 : SmokeArea20;
                detectors.Add(new Detector { Ceiling = mount.point, Normal = mount.normal, Height = pick.Height, Area = area, Coverage = ReachOf(pick.Height), ClassName = pick.Height < 4f ? "광전식 스포트형 2종" : "광전식 스포트형 1종", ZoneId = pick.Zone, Level = Level(pick), Rule = "audit: floor point beyond reach" });
                added++;
            }
            notes.Add("audit added " + added + " detectors; " + stuck + " floor cells without a possible spot");
        }

        /// <summary>The survey plus the mounting spot of each ceiling cell: clear of walls and lamp panels (asked for lazily, only for cells a fitting may go on).</summary>
        internal sealed class Context
        {
            private readonly StationCeilings.Result survey;
            private readonly float wallClear, lampClear;
            private readonly Dictionary<StationCeilings.Cell, Vector2?> spots = new Dictionary<StationCeilings.Cell, Vector2?>();
            private readonly Dictionary<StationCeilings.Cell, HashSet<(int x, int z)>> closedOf = new Dictionary<StationCeilings.Cell, HashSet<(int, int)>>();
            private readonly HashSet<(int x, int z)> anyCeiling;

            /// <param name="wallClear">Least distance of a spot from a wall or a step in the ceiling, metres (NFTC 203 2.4.3.10.5: 0.65 for detectors).</param>
            /// <param name="lampClear">Least distance of a spot from a lamp panel, metres.</param>
            public Context(StationCeilings.Result survey, float wallClear = WallClear, float lampClear = .3f)
            {
                this.survey = survey;
                this.wallClear = wallClear;
                this.lampClear = lampClear;
                anyCeiling = new HashSet<(int, int)>(survey.Cells.Select(c => (c.X, c.Z)));
            }

            /// <summary>Tells which area a cell belongs to, so the wall distance of its spot is measured against that area's edge.</summary>
            public void Register(Component component) { foreach (var cell in component.Real) closedOf[cell] = component.Closed; }

            /// <summary>Distance from a point in the cell to the nearest square outside its area (a wall, a step in the ceiling), metres; 3 when farther.</summary>
            private float WallDistance(Vector2 point, StationCeilings.Cell cell)
            {
                var inside = closedOf.TryGetValue(cell, out var closed) ? closed : anyCeiling;
                float best = 3f;
                for (int dx = -3; dx <= 3; dx++)
                    for (int dz = -3; dz <= 3; dz++)
                    {
                        int x = cell.X + dx, z = cell.Z + dz;
                        if (inside.Contains((x, z))) continue;
                        float nx = Mathf.Clamp(point.x, x, x + 1), nz = Mathf.Clamp(point.y, z, z + 1);
                        best = Mathf.Min(best, Mathf.Sqrt((point.x - nx) * (point.x - nx) + (point.y - nz) * (point.y - nz)));
                    }
                return best;
            }

            /// <summary>The point inside the cell nearest its centre that is <c>wallClear</c> from every wall and <c>lampClear</c> clear of every lamp panel (0.15 m steps), or null.</summary>
            public Vector2? Spot(StationCeilings.Cell cell)
            {
                if (spots.TryGetValue(cell, out var known)) return known;
                Vector2? found = null;
                float best = float.MaxValue;
                for (int i = -3; i <= 3; i++)
                    for (int j = -3; j <= 3; j++)
                    {
                        float d = (i * i + j * j) * .0225f;
                        var point = new Vector2(cell.Floor.x + i * .15f, cell.Floor.z + j * .15f);
                        if (d >= best || WallDistance(point, cell) < wallClear || survey.LampNear(point.x, point.y, cell.CeilingY, lampClear)) continue;
                        best = d;
                        found = point;
                    }
                return spots[cell] = found;
            }

            /// <summary>Whether the point keeps the wall and lamp clearances of this context (the cell tells which area's edge to measure against).</summary>
            public bool Clear(Vector2 point, StationCeilings.Cell cell) => WallDistance(point, cell) >= wallClear && !survey.LampNear(point.x, point.y, cell.CeilingY, lampClear);

            /// <summary>The point of <paramref name="cell"/> nearest <paramref name="target"/> that keeps the clearances and is not rejected, or null (0.15 m steps over the cell, not cached).</summary>
            public Vector2? SpotNear(StationCeilings.Cell cell, Vector2 target, Func<Vector2, bool> reject = null)
            {
                Vector2? found = null;
                float best = float.MaxValue;
                for (int i = -3; i <= 3; i++)
                    for (int j = -3; j <= 3; j++)
                    {
                        var point = new Vector2(cell.Floor.x + i * .15f, cell.Floor.z + j * .15f);
                        float d = (point - target).sqrMagnitude;
                        if (d >= best || WallDistance(point, cell) < wallClear || survey.LampNear(point.x, point.y, cell.CeilingY, lampClear) || reject != null && reject(point)) continue;
                        best = d;
                        found = point;
                    }
                return found;
            }

            /// <summary>The ceiling point a detector on this cell is mounted at (the ceiling's own height and slope at the spot) and the ceiling's downward normal there.</summary>
            public (Vector3 point, Vector3 normal) Mount(StationCeilings.Cell cell)
            {
                var spot = Spot(cell) ?? new Vector2(cell.Floor.x, cell.Floor.z);
                var hit = survey.CeilingAt(spot.x, spot.y, cell.Floor.y);
                return (new Vector3(spot.x, hit?.y ?? cell.CeilingY, spot.y), hit?.normal ?? cell.Normal);
            }
        }

        // ── 구성 요소 ──

        internal sealed class Component
        {
            public readonly List<StationCeilings.Cell> Real = new List<StationCeilings.Cell>();
            public readonly HashSet<(int x, int z)> Closed = new HashSet<(int, int)>();
        }

        private static readonly (int dx, int dz)[] Around = { (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1) };

        /// <summary>Connected areas of one level and height band; slots up to two metres wide (lamp strips, joints) do not split an area.</summary>
        internal static List<Component> Components(List<StationCeilings.Cell> cells)
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

        private static bool PlaceComponent(Context context, Component component, int level, int band, List<Detector> detectors, RegionReport region, List<string> notes)
        {
            float mount = component.Real.Average(c => c.Height);
            float area = band == 0 ? SmokeArea4 : SmokeArea20;
            string className = band == 0 ? "광전식 스포트형 2종" : "광전식 스포트형 1종";
            // 조문은 담당면적만 정한다. 불규칙한 구역에서는 바닥 점이 정사각 칸의 반 대각선보다 3할까지 더 멀어도 면적 기준은 지킨다.
            float reach = Mathf.Sqrt(area / 2f) * 1.3f;
            context.Register(component);
            var placeable = component.Real.Where(c => Clearance(c, component.Closed) >= 1f).ToList();
            if (placeable.Count == 0) { return false; }
            float floorArea = component.Real.Count;

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
                    // 반듯한 방: 주축을 따라 놓은 격자. 직사각형 넓이 ÷ 담당면적 이상의 칸을 가장 정사각형에 가깝게 나눈다(칸 넓이 ≤ 담당면적).
                    var (nx, ny) = Grid(length, width, Mathf.Max(minimum, Mathf.CeilToInt(length * width / area - .02f)));
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
            if (placed.Count == 0) return false;
            region.Minimum += minimum;
            foreach (var cell in placed)
                detectors.Add(new Detector
                {
                    Ceiling = context.Mount(cell).point, Normal = context.Mount(cell).normal, Yaw = angle, Height = cell.Height, Area = area, Coverage = reach,
                    ClassName = className, ZoneId = cell.Zone, Level = level,
                    Rule = corridor ? "corridor 30 m" : "area " + area + " m2",
                });
            return true;
        }

        /// <summary>Columns × rows with columns × rows ≥ n whose cells are as square as possible (NFTC 203 gives the area per detector, not the layout).</summary>
        private static (int, int) Grid(float length, float width, int n)
        {
            int bestX = 1, bestY = n;
            float best = float.MaxValue;
            for (int nx = 1; nx <= n; nx++)
            {
                int ny = Mathf.CeilToInt(n / (float)nx);
                float score = Mathf.Max(length / nx, width / ny);
                if (score < best - 1e-4f) { best = score; bestX = nx; bestY = ny; }
            }
            return (bestX, bestY);
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

        // ── 에스컬레이터·화장실·주방 ──

        /// <summary>Nearest ceiling cell to a floor point, on the same floor, within <paramref name="radius"/> metres, that has a mounting spot; only enclosed zones.</summary>
        private static StationCeilings.Cell Nearest(Context context, StationCeilings.Result survey, Vector3 floorPoint, float radius, float maxHeight)
        {
            StationCeilings.Cell pick = null;
            float best = radius * radius;
            foreach (var c in survey.Cells)
            {
                if (Mathf.Abs(c.Floor.y - floorPoint.y) > .8f || c.Height >= maxHeight || !Enclosed.Contains(c.Zone)) continue;
                float d = new Vector2(c.Floor.x - floorPoint.x, c.Floor.z - floorPoint.z).sqrMagnitude;
                if (d < best && context.Spot(c) != null) { best = d; pick = c; }
            }
            return pick;
        }

        private static Detector Smoke(Context context, StationCeilings.Cell cell, string rule)
        {
            var mount = context.Mount(cell);
            float area = cell.Height < 4f ? SmokeArea4 : SmokeArea20;
            return new Detector { Ceiling = mount.point, Normal = mount.normal, Height = cell.Height, Area = area, Coverage = ReachOf(cell.Height), ClassName = cell.Height < 4f ? "광전식 스포트형 2종" : "광전식 스포트형 1종", ZoneId = cell.Zone, Level = Level(cell), Rule = rule };
        }

        private static bool SmokeNear(List<Detector> detectors, Vector3 point, float radius) =>
            detectors.Exists(d => d.Smoke && Mathf.Abs(d.Ceiling.y - point.y) < 1f && new Vector2(d.Ceiling.x - point.x, d.Ceiling.z - point.z).magnitude < radius);

        /// <summary>
        /// Escalator slopes (2.4.2.1): a smoke detector at the ceiling above the upper landing when a detector is not already within 6 m.
        /// A landing outside an enclosed zone (a platform under the open canopy) is exempt under 2.4.5.2 and noted.
        /// </summary>
        private static void AddEscalatorDetectors(Context context, StationCeilings.Result survey, StationPoints points, List<Detector> detectors, List<string> notes)
        {
            int inside = 0;
            foreach (var escalator in points.Escalators.OrderBy(e => e.id, StringComparer.Ordinal))
            {
                if (escalator.path == null || escalator.path.Length == 0) continue;
                var top = escalator.path.OrderByDescending(p => p.y).First();
                var pick = Nearest(context, survey, top, 8f, CeilingLimit);
                if (pick == null)
                {
                    notes.Add("exempt 2.4.5.2: " + escalator.id + " upper landing " + top.ToString("F0") + " is open to the air (no enclosed ceiling within 8 m)");
                    continue;
                }
                inside++;
                var at = context.Mount(pick).point;
                if (SmokeNear(detectors, at, 6f)) continue;
                detectors.Add(Smoke(context, pick, "escalator slope 2.4.2.1"));
            }
            notes.Add("escalator upper landings under a ceiling: " + inside + " of " + points.Escalators.Count);
        }

        /// <summary>
        /// Public toilets have no shower, so 2.4.5.5 does not exempt them: one smoke detector in the room near the entrance (2.4.3.10.3),
        /// unless a detector already hangs within 4 m. A toilet point with no ceiling of an enclosed zone within 3 m is noted.
        /// </summary>
        private static void AddToiletDetectors(Context context, StationCeilings.Result survey, StationPoints points, List<Detector> detectors, List<string> notes)
        {
            foreach (var toilet in points.Of(PointKind.Toilet).OrderBy(t => t.Id, StringComparer.Ordinal))
            {
                var pick = Nearest(context, survey, toilet.Position, 3f, 4.5f);
                if (pick == null) { notes.Add("toilet '" + toilet.Id + "' at " + toilet.Position.ToString("F0") + ": no ceiling of an enclosed zone within 3 m, no detector"); continue; }
                var at = context.Mount(pick).point;
                var existing = detectors.Find(d => d.Smoke && Mathf.Abs(d.Ceiling.y - at.y) < 1f && new Vector2(d.Ceiling.x - at.x, d.Ceiling.z - at.z).magnitude < 4f);
                if (existing != null) { existing.Room = "toilet"; continue; }
                var detector = Smoke(context, pick, "toilet 2.4.3.10.3 / 2.4.5.5");
                detector.Room = "toilet";
                detectors.Add(detector);
            }
        }

        /// <summary>Kitchens of food shops (<see cref="IncidentDirector.KitchenOf"/>): a fixed-temperature spot heat detector; smoke detectors are exempt there (2.4.5.7).</summary>
        private static void AddKitchenDetectors(Context context, StationCeilings.Result survey, StationPoints points, List<Detector> detectors, List<string> notes)
        {
            foreach (var shop in points.Of(PointKind.Shop).Where(s => IncidentDirector.KitchenOf(s) != null).OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                // 매장 점은 카운터 앞 1.2 m 이다. 주방은 카운터 뒤(매장 안쪽)로 본다.
                var inside = shop.Position + Quaternion.Euler(0, shop.Yaw, 0) * Vector3.forward * 2.2f;
                var pick = Nearest(context, survey, new Vector3(inside.x, shop.Position.y, inside.z), 4f, 4f);
                if (pick == null) { notes.Add("kitchen of '" + shop.Label + "': no low ceiling within 4 m of " + inside.ToString("F0")); continue; }
                var mount = context.Mount(pick);
                detectors.Add(new Detector
                {
                    Ceiling = mount.point, Normal = mount.normal, Height = pick.Height, Area = 70f, Coverage = Mathf.Sqrt(70f / 2f), Smoke = false,
                    ClassName = "정온식 스포트형 특종", ZoneId = pick.Zone, Level = Level(pick), Rule = "kitchen " + shop.Label,
                });
            }
        }
    }
}
