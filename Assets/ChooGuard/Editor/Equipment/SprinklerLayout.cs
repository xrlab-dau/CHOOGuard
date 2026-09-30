using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Where the sprinkler heads, pipes and alarm valves go, by NFTC 103 (스프링클러설비의 화재안전기술기준, in force 2024-07-01),
    /// from the ceiling cells of <see cref="StationCeilings"/>. Pure function of the survey and the walls: the same twin gives the same network.
    /// <list type="bullet">
    /// <item>Where: every enclosed ceiling under 20 m (2.12.1.9 lets lobbies of 20 m and more go without). Toilets are exempt (2.12.1.1), so are spaces open to the air
    /// (platform canopies, the exit deck: same reading as the detectors, 2.12.1.1 "직접 외기에 개방된 복도 등 이와 유사한 장소").</item>
    /// <item>Heads: standard-response closed heads on a square lattice laid along each room's main axis, spacing under 3.25 m = 2·r·cos 45° with r = 2.3 m for fire-resistant
    /// construction (2.7.3.4); an audit then adds heads until every floor point lies within 2.3 m of a head. Heads keep 0.25 m from walls (2.7.7.1 asks 0.10 m) and lamp panels and 0.6 m from detectors.</item>
    /// <item>Pipes: a cross main per block of at most 16 head columns with branch lines at right angles, at most 8 heads on each side of the main (2.5.9.2), no tournament pipes (2.5.9.1); branch
    /// diameters from table 2.5.3.3 row "가" (2 heads DN25, 3 DN32, 5 DN40, 10 DN50), the main by the heads it carries (30 DN65, 60 DN80, 80 DN90, 100 DN100, 160 DN125, more DN150).
    /// Above a finished ceiling the pipes lie in the ceiling void (no mesh, the head hangs through the tile); under an exposed slab, deck or roof they are red exposed pipe on hangers
    /// with upright heads (2.5.13, 2.5.18) - but only where the slab is high enough (<see cref="MinExposedCeiling"/>) for the pipe to clear people's heads (<see cref="ClearHeight"/> above the
    /// walkable floor): under a lower slab the pipe is concealed and only pendant heads show. Where the runs of two zones would pass through each other, one zone hangs one <see cref="LayerStep"/> lower
    /// (<see cref="Separate"/>); a feed leaves its main at right angles. <see cref="SprinklerAudit"/> counts the clearances and overlaps that remain (both must be 0).</item>
    /// <item>Protection zones (방호구역): tiles of 48 m, at most 2,304 m² on one floor (2.3.1.1 caps 3,000 m²), one alarm valve each on the nearest wall (2.3.1.4: 0.8-1.5 m above the floor).</item>
    /// </list>
    /// </summary>
    internal static class SprinklerLayout
    {
        public const float Spacing = 3.2f, Reach = 2.3f, ZoneTile = 48f, PipeDrop = .376f, PlenumRise = .35f, WallGap = .25f, LampGap = .25f, DetectorGap = .6f, CeilingLimit = 20f;
        /// <summary>Lowest a pipe surface may hang above the floor under it, the ceiling height from which exposed pipe is allowed (2.4 m + pipe hung 0.376 m + a second layer 0.26 m + radius of the biggest pipe 0.083 m, rounded up) and the vertical step between two zone layers.</summary>
        public const float ClearHeight = 2.4f, MinExposedCeiling = 3.2f, LayerStep = .26f;
        private const int MinZoneHeads = 40, MinComponentCells = 6, ColumnsPerBlock = 16;

        /// <summary>The AlarmValve model (its pivot is the back face centre at mid height): height, the riser's offset from the wall along the front and to the right, and the pivot's height above the floor (NFTC 103 2.3.1.4: 0.8-1.5 m).</summary>
        public const float ValveHeight = 1.35f, ValveRiserDepth = .12f, ValveRiserSide = .0213f, ValveMountHeight = 1.1f;
        private static SprinklerAudit.Floors headFloors;
        private static readonly string[] ExposedWords = { "Majibang_2F_Floor", "Kit_Slab_", "DeckLiner", "선로상층부", "출구지붕" };

        public sealed class Head
        {
            /// <summary>The attach point on the ceiling and the ceiling's downward normal there.</summary>
            public Vector3 Ceiling, Normal;
            public float Height, FloorY;
            public int Level, Line = -1;
            public Vector2Int Tile;
            public string ZoneId = "", Owner = "", Valve = "";
            /// <summary>Upright on exposed pipe rather than a pendant through a ceiling tile.</summary>
            public bool Exposed, Spur;
        }

        /// <summary>A straight run of pipe: <c>main</c> (cross main), <c>branch</c>, <c>spur</c> (an audit head's short arm), <c>feed</c> (main to the alarm valve wall).</summary>
        public sealed class Line
        {
            public string Role = "";
            public Vector3 A, B;
            public int Dn, Level, Heads;
            /// <summary>Height of the floor beneath the line.</summary>
            public float FloorY;
            public bool Exposed;
            /// <summary>How far the zone layer hangs this line below its natural height (see <see cref="Separate"/>).</summary>
            public float Drop;
            public Vector2Int Tile;
            public string Valve = "";
        }

        public sealed class Valve
        {
            public string Key = "";
            public int Level, Heads;
            public Vector2Int Tile;
            public Vector3 Position, Normal, MainMid;
            public float FloorY, CeilingY, Area;
        }

        public sealed class Report
        {
            public string Name = "";
            public int Cells, Heads, Uncovered;
            public float AreaPerHead;
        }

        public sealed class Result
        {
            public readonly List<Head> Heads = new List<Head>();
            public readonly List<Line> Lines = new List<Line>();
            public readonly List<Valve> Valves = new List<Valve>();
            public readonly List<Report> Regions = new List<Report>();
            public SprinklerAudit.Report Audit;
        }

        private sealed class Lattice
        {
            public Vector2 Centre, U, V;
            public float U0, V0, Du, Dv;
            public readonly List<Node> Nodes = new List<Node>();
            public Vector3 At(float u, float v, float y) { var p = Centre + U * u + V * v; return new Vector3(p.x, y, p.y); }
        }

        private sealed class Node { public int I, J; public Head Head; }

        private static readonly string[] LevelNames = { "1F/platform", "2F", "3F" };
        private static int Band(float h) => h < 4f ? 0 : h < 8f ? 1 : h < 15f ? 2 : 3;
        private static readonly string[] BandNames = { "<4 m", "4-8 m", "8-15 m", "15-20 m" };

        public static bool IsExposed(string owner) { foreach (var word in ExposedWords) if (owner.Contains(word)) return true; return false; }

        public static int BranchDn(int heads) => heads <= 2 ? 25 : heads <= 3 ? 32 : heads <= 5 ? 40 : 50;

        public static int MainDn(int heads) => heads <= 30 ? 65 : heads <= 60 ? 80 : heads <= 80 ? 90 : heads <= 100 ? 100 : heads <= 160 ? 125 : 150;

        public static Result Plan(StationCeilings.Result survey, StationPoints points, StationWalls walls, List<Vector3> avoid, List<string> notes)
        {
            var result = new Result();
            headFloors = new SprinklerAudit.Floors(survey);
            var toilets = points.Of(PointKind.Toilet).ToList();
            var accepted = survey.Cells.Where(c => DetectorLayout.Enclosed.Contains(c.Zone) && c.Height < CeilingLimit).ToList();
            int exempt = accepted.RemoveAll(c => c.Height < 4.5f && toilets.Exists(t => Mathf.Abs(t.Position.y - c.Floor.y) < 1f && new Vector2(t.Position.x - c.Floor.x, t.Position.z - c.Floor.z).magnitude < 3f));
            notes.Add("exempt 2.12.1.1 (toilets): " + exempt + " cells");
            var context = new DetectorLayout.Context(survey, WallGap, LampGap);
            var lattices = new List<Lattice>();
            foreach (var group in accepted.GroupBy(DetectorLayout.Level).OrderBy(g => g.Key))
                foreach (var component in DetectorLayout.Components(group.ToList()))
                {
                    if (component.Real.Count < MinComponentCells) continue;
                    context.Register(component);
                    var lattice = PlanLattice(context, survey, component, avoid, result);
                    if (lattice != null) lattices.Add(lattice);
                }
            Audit(context, survey, accepted, avoid, result, notes);
            AssignZones(result);
            foreach (var lattice in lattices) BuildLines(lattice, result);
            AttachSpurs(result);
            ConcealLow(result, notes);
            BuildValves(survey, walls, accepted, result, notes);
            Separate(result, notes);
            ConcealLow(result, notes);
            StandRisers(result);
            Summarise(accepted, result);
            result.Audit = SprinklerAudit.Run(result, survey);
            return result;
        }

        // ── 격자 ──

        private static Lattice PlanLattice(DetectorLayout.Context context, StationCeilings.Result survey, DetectorLayout.Component component, List<Vector3> avoid, Result result)
        {
            var cells = component.Real;
            var byPosition = cells.ToDictionary(c => (c.X, c.Z));
            var centre = new Vector2(cells.Average(c => c.Floor.x), cells.Average(c => c.Floor.z));
            float sxx = 0, szz = 0, sxz = 0;
            foreach (var c in cells) { float dx = c.Floor.x - centre.x, dz = c.Floor.z - centre.y; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
            float angle = Mathf.Round(.5f * Mathf.Atan2(2 * sxz, sxx - szz) * Mathf.Rad2Deg);
            var u = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var v = new Vector2(-u.y, u.x);
            float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
            foreach (var c in cells)
            {
                var p = new Vector2(c.Floor.x - centre.x, c.Floor.z - centre.y);
                float pu = Vector2.Dot(p, u), pv = Vector2.Dot(p, v);
                u0 = Mathf.Min(u0, pu - .5f); u1 = Mathf.Max(u1, pu + .5f); v0 = Mathf.Min(v0, pv - .5f); v1 = Mathf.Max(v1, pv + .5f);
            }
            int nu = Mathf.Max(1, Mathf.CeilToInt((u1 - u0) / Spacing - .001f)), nv = Mathf.Max(1, Mathf.CeilToInt((v1 - v0) / Spacing - .001f));
            var lattice = new Lattice
            {
                Centre = centre, U = u, V = v, U0 = u0, V0 = v0, Du = (u1 - u0) / nu, Dv = (v1 - v0) / nv,
            };
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    var target = centre + u * (u0 + (i + .5f) * lattice.Du) + v * (v0 + (j + .5f) * lattice.Dv);
                    var head = Locate(context, survey, byPosition, target, avoid);
                    if (head == null) continue;
                    lattice.Nodes.Add(new Node { I = i, J = j, Head = head });
                    result.Heads.Add(head);
                }
            return lattice;
        }

        private static bool NearDetector(List<Vector3> avoid, Vector2 point, float ceilingY)
        {
            foreach (var d in avoid) if (Mathf.Abs(d.y - ceilingY) < 1f && (new Vector2(d.x, d.z) - point).sqrMagnitude < DetectorGap * DetectorGap) return true;
            return false;
        }

        /// <summary>
        /// The head for a lattice node. Under an exposed ceiling it sits exactly on the node (the pipe runs through it) or is dropped when the node breaks a clearance; through a
        /// finished ceiling it takes the nearest clear spot within 0.9 m (the pipe above is in the void, the head hangs on a short drop).
        /// </summary>
        private static Head Locate(DetectorLayout.Context context, StationCeilings.Result survey, Dictionary<(int, int), StationCeilings.Cell> byPosition, Vector2 target, List<Vector3> avoid)
        {
            int x = Mathf.FloorToInt(target.x), z = Mathf.FloorToInt(target.y);
            Vector2? spot = null;
            StationCeilings.Cell cell = null;
            bool exposed = byPosition.TryGetValue((x, z), out var here) && IsExposed(here.Owner) && here.Height >= MinExposedCeiling;
            if (exposed)
            {
                cell = here;
                if (context.Clear(target, cell) && !NearDetector(avoid, target, cell.CeilingY)) spot = target;
            }
            else
            {
                float best = .81f;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!byPosition.TryGetValue((x + dx, z + dz), out var candidate)) continue;
                        var found = context.SpotNear(candidate, target, q => NearDetector(avoid, q, candidate.CeilingY));
                        if (found == null) continue;
                        float d = (found.Value - target).sqrMagnitude;
                        if (d >= best) continue;
                        best = d; spot = found; cell = candidate;
                    }
            }
            return spot == null ? null : MakeHead(survey, cell, spot.Value, exposed);
        }

        private static Head MakeHead(StationCeilings.Result survey, StationCeilings.Cell cell, Vector2 at, bool exposed)
        {
            var hit = survey.CeilingAt(at.x, at.y, cell.Floor.y);
            float ceiling = hit?.y ?? cell.CeilingY;
            // Exposed pipe only where a person under it (on the highest walkable floor there: a stair or a ledge counts) still has the clear height under the pipe hung 0.376 m below the slab.
            var pipe = new Vector3(at.x, ceiling - PipeDrop, at.y);
            exposed = exposed && ceiling - cell.Floor.y >= MinExposedCeiling && headFloors.Clearance(pipe, pipe, .083f) >= ClearHeight;
            return new Head
            {
                Ceiling = new Vector3(at.x, ceiling, at.y), Normal = hit?.normal ?? cell.Normal, Height = ceiling - cell.Floor.y, FloorY = cell.Floor.y,
                Level = DetectorLayout.Level(cell), ZoneId = cell.Zone, Owner = cell.Owner, Exposed = exposed,
            };
        }

        // ── 보충: 바닥의 어느 점이든 헤드까지 2.3 m ──

        private static void Audit(DetectorLayout.Context context, StationCeilings.Result survey, List<StationCeilings.Cell> accepted, List<Vector3> avoid, Result result, List<string> notes)
        {
            var byPosition = new Dictionary<(int, int, int), StationCeilings.Cell>();
            foreach (var c in accepted.OrderBy(c => c.CeilingY)) byPosition.TryAdd((DetectorLayout.Level(c), c.X, c.Z), c);
            var buckets = new Dictionary<(int, int, int), List<Head>>();
            void Bucket(Head h) { var key = (h.Level, Mathf.FloorToInt(h.Ceiling.x / 4f), Mathf.FloorToInt(h.Ceiling.z / 4f)); if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Head>(); list.Add(h); }
            foreach (var h in result.Heads) Bucket(h);
            int added = 0, stuck = 0;
            foreach (var cell in accepted.OrderBy(c => c.X).ThenBy(c => c.Z).ThenBy(c => c.Floor.y))
            {
                if (Covered(buckets, cell)) continue;
                var target = new Vector2(cell.Floor.x, cell.Floor.z);
                bool exposed = IsExposed(cell.Owner) && cell.Height >= MinExposedCeiling;
                Head pick = null;
                float best = float.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!byPosition.TryGetValue((DetectorLayout.Level(cell), cell.X + dx, cell.Z + dz), out var candidate) || Mathf.Abs(candidate.CeilingY - cell.CeilingY) > .6f) continue;
                        var spot = context.SpotNear(candidate, target, q => NearDetector(avoid, q, candidate.CeilingY));
                        if (spot == null) continue;
                        float d = (spot.Value - target).sqrMagnitude;
                        if (d >= best) continue;
                        best = d;
                        pick = MakeHead(survey, candidate, spot.Value, exposed);
                    }
                if (pick == null) { stuck++; notes.Add("audit: floor cell at " + cell.Floor.ToString("F0") + " has no ceiling spot for a head"); continue; }
                pick.Spur = true;
                result.Heads.Add(pick);
                Bucket(pick);
                added++;
            }
            notes.Add("audit added " + added + " heads; " + stuck + " floor cells without a possible spot");
        }

        private static bool Covered(Dictionary<(int, int, int), List<Head>> buckets, StationCeilings.Cell cell)
        {
            int level = DetectorLayout.Level(cell), bx = Mathf.FloorToInt(cell.Floor.x / 4f), bz = Mathf.FloorToInt(cell.Floor.z / 4f);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (buckets.TryGetValue((level, bx + dx, bz + dz), out var list))
                        foreach (var h in list)
                            if (Mathf.Abs(h.Ceiling.y - cell.CeilingY) <= 8f && new Vector2(h.Ceiling.x - cell.Floor.x, h.Ceiling.z - cell.Floor.z).magnitude <= Reach + .001f) return true;
            return false;
        }

        // ── 방호구역 ──

        private static Vector2Int TileOf(Head h) => new Vector2Int(Mathf.FloorToInt(h.Ceiling.x / ZoneTile), Mathf.FloorToInt(h.Ceiling.z / ZoneTile));

        /// <summary>Tiles of 48 m per floor; a tile with fewer than 12 heads joins the nearest tile of the same floor.</summary>
        private static void AssignZones(Result result)
        {
            foreach (var h in result.Heads) h.Tile = TileOf(h);
            foreach (var level in result.Heads.Select(h => h.Level).Distinct().OrderBy(l => l))
            {
                var tiles = result.Heads.Where(h => h.Level == level).GroupBy(h => h.Tile).ToDictionary(g => g.Key, g => g.ToList());
                var centre = tiles.ToDictionary(t => t.Key, t => new Vector2(t.Value.Average(h => h.Ceiling.x), t.Value.Average(h => h.Ceiling.z)));
                var big = tiles.Where(t => t.Value.Count >= MinZoneHeads).Select(t => t.Key).OrderBy(t => t.x).ThenBy(t => t.y).ToList();
                if (big.Count == 0) continue;
                foreach (var small in tiles.Where(t => t.Value.Count < MinZoneHeads).OrderBy(t => t.Key.x).ThenBy(t => t.Key.y).ToList())
                {
                    var target = big.OrderBy(t => (centre[t] - centre[small.Key]).sqrMagnitude).ThenBy(t => t.x).ThenBy(t => t.y).First();
                    foreach (var h in small.Value) h.Tile = target;
                }
            }
            foreach (var h in result.Heads) h.Valve = ValveKey(h.Level, h.Tile);
        }

        public static string ValveKey(int level, Vector2Int tile) => "sp-" + level + "-" + tile.x + "-" + tile.y;

        // ── 배관 ──

        private static void BuildLines(Lattice lattice, Result result)
        {
            foreach (var group in lattice.Nodes.GroupBy(n => n.Head.Tile).OrderBy(g => g.Key.x).ThenBy(g => g.Key.y))
            {
                var nodes = group.ToList();
                int i0 = nodes.Min(n => n.I), i1 = nodes.Max(n => n.I), columns = i1 - i0 + 1;
                int blocks = Mathf.CeilToInt(columns / (float)ColumnsPerBlock), width = Mathf.CeilToInt(columns / (float)blocks);
                for (int k = 0; k < blocks; k++)
                {
                    int start = i0 + k * width, end = Mathf.Min(i1, start + width - 1);
                    var block = nodes.Where(n => n.I >= start && n.I <= end).ToList();
                    if (block.Count == 0) continue;
                    int split = start + (end - start + 1) / 2;
                    float uc = lattice.U0 + split * lattice.Du;
                    var head0 = block[0].Head;
                    bool mainExposed = block.Count(n => n.Head.Exposed) * 2 >= block.Count;
                    float mainAxis = Axis(block.Select(n => n.Head), mainExposed);
                    var rows = block.GroupBy(n => n.J).OrderBy(g => g.Key).ToList();
                    foreach (var row in rows)
                    {
                        float vj = lattice.V0 + (row.Key + .5f) * lattice.Dv;
                        foreach (var side in new[] { row.Where(n => n.I < split).ToList(), row.Where(n => n.I >= split).ToList() })
                        {
                            if (side.Count == 0) continue;
                            // From the main outwards, cut into runs under one kind of ceiling: the diameter falls with the heads still to come.
                            var ordered = side.OrderBy(n => Mathf.Abs(n.I + .5f - split)).ToList();
                            float from = uc;
                            for (int r = 0; r < ordered.Count;)
                            {
                                int e = r;
                                while (e + 1 < ordered.Count && ordered[e + 1].Head.Exposed == ordered[r].Head.Exposed) e++;
                                var run = ordered.GetRange(r, e - r + 1);
                                bool exposed = run[0].Head.Exposed;
                                float axis = Axis(run.Select(n => n.Head), exposed);
                                float uFar = lattice.U0 + (run[run.Count - 1].I + .5f) * lattice.Du;
                                result.Lines.Add(new Line { Role = "branch", A = lattice.At(from, vj, axis), B = lattice.At(uFar, vj, axis), Dn = BranchDn(ordered.Count - r), Level = head0.Level, Heads = run.Count, Exposed = exposed, Tile = head0.Tile, Valve = head0.Valve, FloorY = head0.FloorY });
                                foreach (var n in run) n.Head.Line = result.Lines.Count - 1;
                                from = uFar;
                                r = e + 1;
                            }
                        }
                    }
                    float vMin = lattice.V0 + (rows.First().Key + .5f) * lattice.Dv, vMax = lattice.V0 + (rows.Last().Key + .5f) * lattice.Dv;
                    if (vMax - vMin < .5f) { vMin -= .6f; vMax += .6f; }
                    result.Lines.Add(new Line { Role = "main", A = lattice.At(uc, vMin, mainAxis), B = lattice.At(uc, vMax, mainAxis), Dn = MainDn(block.Count), Level = head0.Level, Heads = block.Count, Exposed = mainExposed, Tile = head0.Tile, Valve = head0.Valve, FloorY = head0.FloorY });
                }
            }
        }

        /// <summary>Pipe axis height above the ceiling attach points of <paramref name="heads"/>: hung <see cref="PipeDrop"/> under the lowest exposed ceiling, or lying in the void over a finished one.</summary>
        private static float Axis(IEnumerable<Head> heads, bool exposed) => exposed ? heads.Min(h => h.Ceiling.y) - PipeDrop : heads.Max(h => h.Ceiling.y) + PlenumRise;

        /// <summary>Audit heads sit on a short arm from the nearest line of their zone (a stub when the zone has no line near).</summary>
        private static void AttachSpurs(Result result)
        {
            var lines = result.Lines.ToList();
            foreach (var head in result.Heads.Where(h => h.Line < 0).OrderBy(h => h.Ceiling.x).ThenBy(h => h.Ceiling.z))
            {
                Line best = null;
                Vector3 from = default;
                float bestD = 12f * 12f;
                foreach (var line in lines)
                {
                    if (line.Level != head.Level || line.Valve != head.Valve || line.Role == "feed") continue;
                    var a = new Vector2(line.A.x, line.A.z);
                    var ab = new Vector2(line.B.x, line.B.z) - a;
                    var p = new Vector2(head.Ceiling.x, head.Ceiling.z);
                    float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    var q = a + ab * t;
                    float d = (p - q).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = line; from = new Vector3(q.x, line.A.y, q.y); }
                }
                float axis = best != null ? best.A.y : head.Exposed ? head.Ceiling.y - PipeDrop : head.Ceiling.y + PlenumRise;
                var start = best != null ? from : new Vector3(head.Ceiling.x - .6f, axis, head.Ceiling.z);
                result.Lines.Add(new Line { Role = "spur", A = start, B = new Vector3(head.Ceiling.x, axis, head.Ceiling.z), Dn = 25, Level = head.Level, Heads = 1, Exposed = head.Exposed, Tile = head.Tile, Valve = head.Valve, FloorY = head.FloorY });
                head.Line = result.Lines.Count - 1;
                lines.Add(result.Lines[result.Lines.Count - 1]);
            }
        }

        // ── 유수검지장치 ──

        private static void BuildValves(StationCeilings.Result survey, StationWalls walls, List<StationCeilings.Cell> accepted, Result result, List<string> notes)
        {
            var orphans = new List<string>();
            foreach (var group in result.Heads.GroupBy(h => h.Valve).OrderBy(g => g.First().Level).ThenBy(g => g.First().Tile.x).ThenBy(g => g.First().Tile.y))
            {
                var heads = group.ToList();
                var tile = heads[0].Tile;
                int level = heads[0].Level;
                var mains = result.Lines.Where(l => l.Valve == group.Key && l.Role == "main").ToList();
                if (mains.Count == 0) mains = result.Lines.Where(l => l.Valve == group.Key).ToList();
                var centroid = new Vector2(heads.Average(h => h.Ceiling.x), heads.Average(h => h.Ceiling.z));
                var main = mains.OrderBy(l => (new Vector2((l.A.x + l.B.x) * .5f, (l.A.z + l.B.z) * .5f) - centroid).sqrMagnitude).First();
                var mid = (main.A + main.B) * .5f;
                float floorY = heads.Average(h => h.FloorY);
                var origin = new Vector3(mid.x, floorY + 1.2f, mid.z);
                StationWalls.Hit best = default;
                bool found = false;
                for (int k = 0; k < 16; k++)
                {
                    float a = k * 22.5f * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    // 방 바깥·유리·문은 걸러 낸다: 벽 바로 앞 0.6 m 바닥에 걷는 칸이 있어야 한다.
                    if (!walls.Cast(origin, dir, .5f, 40f, out var hit) || found && hit.Distance >= best.Distance) continue;
                    var front = hit.Point + hit.Normal * .6f;
                    if (!accepted.Exists(c => Mathf.Abs(c.Floor.y - floorY) < .8f && Mathf.Abs(c.Floor.x - front.x) < .6f && Mathf.Abs(c.Floor.z - front.z) < .6f && c.Walkable)) continue;
                    if (hit.Owner.Contains("Glass") || hit.Owner.Contains("문")) continue;
                    best = hit; found = true;
                }
                if (!found) { orphans.Add(group.Key); continue; }
                float ceilingY = survey.CeilingAt(best.Point.x + best.Normal.x * .3f, best.Point.z + best.Normal.z * .3f, floorY)?.y ?? heads.Max(h => h.Ceiling.y);
                float feedDn = Mathf.Max(100, MainDn(heads.Count));
                var right = Vector3.Cross(Vector3.up, best.Normal);
                var riser = new Vector3(best.Point.x, 0, best.Point.z) + best.Normal * ValveRiserDepth + right * ValveRiserSide;
                float mount = floorY + ValveMountHeight;
                result.Valves.Add(new Valve { Key = group.Key, Level = level, Tile = tile, Heads = heads.Count, Position = new Vector3(best.Point.x, mount, best.Point.z), Normal = best.Normal, MainMid = mid, FloorY = floorY, CeilingY = ceilingY });
                // 입상관: 밸브 세트 위 끝에서 천장(또는 노출 배관 높이)까지 보이는 관. 이음은 그 위 교차배관으로 간다.
                result.Lines.Add(new Line { Role = "riser", A = new Vector3(riser.x, mount + ValveHeight * .5f, riser.z), B = new Vector3(riser.x, main.Exposed ? mid.y : ceilingY, riser.z), Dn = 100, Level = level, Heads = heads.Count, Exposed = true, Tile = tile, Valve = group.Key, FloorY = floorY });
                result.Lines.Add(PlanFeed(result, main, riser, (int)feedDn, level, tile, group.Key, floorY, heads.Count));
            }
            // A zone with no wall near its main has no place for a valve: its heads join the nearest zone of the same floor that has one.
            foreach (var key in orphans)
            {
                var heads = result.Heads.Where(h => h.Valve == key).ToList();
                var centre = new Vector2(heads.Average(h => h.Ceiling.x), heads.Average(h => h.Ceiling.z));
                var target = result.Valves.Where(v => v.Level == heads[0].Level).OrderBy(v => (new Vector2(v.MainMid.x, v.MainMid.z) - centre).sqrMagnitude).ThenBy(v => v.Key, StringComparer.Ordinal).First();
                foreach (var h in heads) { h.Valve = target.Key; h.Tile = target.Tile; }
                foreach (var l in result.Lines.Where(l => l.Valve == key)) { l.Valve = target.Key; l.Tile = target.Tile; }
                target.Heads += heads.Count;
                notes.Add("valve " + key + ": no wall near its main; its " + heads.Count + " heads are served from " + target.Key);
            }
            var buckets = new Dictionary<(int, int, int), List<Head>>();
            foreach (var h in result.Heads) { var bucket = (h.Level, Mathf.FloorToInt(h.Ceiling.x / 4f), Mathf.FloorToInt(h.Ceiling.z / 4f)); if (!buckets.TryGetValue(bucket, out var list)) buckets[bucket] = list = new List<Head>(); list.Add(h); }
            var areas = new Dictionary<string, int>();
            foreach (var cell in accepted)
            {
                int level = DetectorLayout.Level(cell), bx = Mathf.FloorToInt(cell.Floor.x / 4f), bz = Mathf.FloorToInt(cell.Floor.z / 4f);
                Head nearest = null;
                float bestD = float.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (buckets.TryGetValue((level, bx + dx, bz + dz), out var near))
                            foreach (var h in near)
                            {
                                float d = new Vector2(h.Ceiling.x - cell.Floor.x, h.Ceiling.z - cell.Floor.z).sqrMagnitude;
                                if (d < bestD) { bestD = d; nearest = h; }
                            }
                if (nearest == null) continue;
                areas.TryGetValue(nearest.Valve, out int area);
                areas[nearest.Valve] = area + 1;
            }
            foreach (var valve in result.Valves) { areas.TryGetValue(valve.Key, out int area); valve.Area = area; }
        }

        // ── 급수 배관과 층 나누기 ──

        private static readonly float[] FeedShifts = { 0f, .4f, -.4f, .8f, -.8f, 1.2f, -1.2f, 1.6f, -1.6f, 2f, -2f };

        /// <summary>
        /// The feed from the valve's riser to the cross main: it meets the main at a right angle (a tee at the foot of the perpendicular from the riser, the main is lengthened when that foot lies beyond
        /// its end), never along it, and is shifted along the main in 0.4 m steps until it clears the branch lines beside it by 5 cm. A feed of a concealed main is concealed.
        /// </summary>
        private static Line PlanFeed(Result result, Line main, Vector3 riser, int dn, int level, Vector2Int tile, string key, float floorY, int heads)
        {
            var along = new Vector3(main.B.x - main.A.x, 0, main.B.z - main.A.z);
            float length = along.magnitude;
            along = length > 1e-4f ? along / length : Vector3.right;
            float foot = Vector3.Dot(new Vector3(riser.x - main.A.x, 0, riser.z - main.A.z), along);
            var target = new Vector3(riser.x, main.A.y, riser.z);
            var others = main.Exposed ? result.Lines.Where(l => l.Valve == key && l.Exposed && l != main && l.Role != "riser").ToList() : new List<Line>();
            Line feed = null;
            float at = foot;
            foreach (float shift in FeedShifts)
            {
                float t = foot + shift;
                var q = new Vector3(main.A.x + along.x * t, main.A.y, main.A.z + along.z * t);
                var candidate = new Line { Role = "feed", A = q, B = target, Dn = dn, Level = level, Heads = heads, Exposed = main.Exposed, Tile = tile, Valve = key, FloorY = floorY };
                var direction = (target - q).normalized;
                bool square = (target - q).sqrMagnitude >= .01f && Mathf.Abs(Vector3.Dot(direction, along)) <= .64f;
                if (feed == null) { feed = candidate; at = t; }
                if (!square) continue;
                bool clear = true;
                foreach (var other in others)
                {
                    float gap = SprinklerAudit.Gap(candidate, other, out bool joint);
                    if (gap < (joint ? 0f : SprinklerAudit.ClashGap)) { clear = false; break; }
                }
                if (!clear) continue;
                feed = candidate;
                at = t;
                break;
            }
            if (at < 0) main.A = new Vector3(main.A.x + along.x * at, main.A.y, main.A.z + along.z * at);
            else if (at > length) main.B = new Vector3(main.A.x + along.x * at, main.B.y, main.A.z + along.z * at);
            return feed;
        }

        /// <summary>
        /// Two zones whose exposed pipes would pass through each other are hung on different layers: the zones are coloured in order so that no two neighbours share a layer, and layer k hangs
        /// k steps of <see cref="LayerStep"/> lower (the risers of a lowered zone end lower, its upright heads sit on its pipe). Runs of one zone are kept apart by the layout itself.
        /// </summary>
        private static void Separate(Result result, List<string> notes)
        {
            var exposed = result.Lines.Where(l => l.Exposed).ToList();
            var neighbours = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            for (int i = 0; i < exposed.Count; i++)
                for (int j = i + 1; j < exposed.Count; j++)
                {
                    var a = exposed[i];
                    var b = exposed[j];
                    if (a.Valve == b.Valve || Mathf.Abs(Mathf.Min(a.A.y, a.B.y) - Mathf.Min(b.A.y, b.B.y)) > 2f) continue;
                    float gap = SprinklerAudit.Gap(a, b, out bool joint);
                    if (gap >= (joint ? 0f : SprinklerAudit.ClashGap) - 1e-4f) continue;
                    if (!neighbours.TryGetValue(a.Valve, out var na)) neighbours[a.Valve] = na = new HashSet<string>(StringComparer.Ordinal);
                    if (!neighbours.TryGetValue(b.Valve, out var nb)) neighbours[b.Valve] = nb = new HashSet<string>(StringComparer.Ordinal);
                    na.Add(b.Valve);
                    nb.Add(a.Valve);
                }
            var layers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var zone in neighbours.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                int layer = 0;
                while (neighbours[zone].Any(n => layers.TryGetValue(n, out int taken) && taken == layer)) layer++;
                layers[zone] = layer;
            }
            foreach (var line in exposed)
            {
                if (!layers.TryGetValue(line.Valve, out int layer) || layer == 0) continue;
                float drop = layer * LayerStep;
                line.B.y -= drop;
                if (line.Role != "riser") { line.A.y -= drop; line.Drop = drop; }
            }
            foreach (var pair in layers.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal)) notes.Add("zone " + pair.Key + " hangs " + (pair.Value * LayerStep).ToString("0.00") + " m lower where its pipes would cross a neighbour's");
        }

        /// <summary>
        /// A run of exposed pipe that would hang lower than <see cref="ClearHeight"/> above the floor under any point of it (a stair, a ledge, a low soffit under a high slab) is concealed
        /// instead, with the heads on it: they become pendant heads and the pipe joins the records without a mesh, at the height of the ceiling void.
        /// </summary>
        private static void ConcealLow(Result result, List<string> notes)
        {
            int count = 0;
            for (int i = 0; i < result.Lines.Count; i++)
            {
                var line = result.Lines[i];
                if (!line.Exposed || line.Role == "riser") continue;
                if (headFloors.Clearance(line.A, line.B, SprinklerAudit.Radius(line)) >= ClearHeight - .001f) continue;
                float lift = PipeDrop + PlenumRise + line.Drop;
                line.Exposed = false;
                line.A.y += lift;
                line.B.y += lift;
                line.Drop = 0;
                foreach (var head in result.Heads) if (head.Line == i) head.Exposed = false;
                count++;
            }
            if (count > 0) notes.Add("concealed " + count + " pipe runs that would hang under " + ClearHeight.ToString("0.0") + " m above the floor under them");
        }

        /// <summary>The riser stands from the valve to the height its zone's feed runs at: the ceiling void when the feed is concealed (the visible pipe ends at the ceiling), the feed's own height when it is exposed.</summary>
        private static void StandRisers(Result result)
        {
            foreach (var riser in result.Lines.Where(l => l.Role == "riser"))
            {
                var feed = result.Lines.FirstOrDefault(l => l.Role == "feed" && l.Valve == riser.Valve);
                var valve = result.Valves.First(v => v.Key == riser.Valve);
                riser.B = new Vector3(riser.B.x, feed != null && feed.Exposed ? feed.A.y : valve.CeilingY, riser.B.z);
            }
        }

        // ── 표 ──

        private static void Summarise(List<StationCeilings.Cell> accepted, Result result)
        {
            var buckets = new Dictionary<(int, int, int), List<Head>>();
            foreach (var h in result.Heads) { var key = (h.Level, Mathf.FloorToInt(h.Ceiling.x / 4f), Mathf.FloorToInt(h.Ceiling.z / 4f)); if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<Head>(); list.Add(h); }
            foreach (var group in accepted.GroupBy(c => (level: DetectorLayout.Level(c), band: Band(c.Height))).OrderBy(g => g.Key.level).ThenBy(g => g.Key.band))
            {
                int heads = result.Heads.Count(h => h.Level == group.Key.level && Band(h.Height) == group.Key.band);
                result.Regions.Add(new Report
                {
                    Name = LevelNames[group.Key.level] + " " + BandNames[group.Key.band], Cells = group.Count(), Heads = heads,
                    AreaPerHead = heads > 0 ? group.Count() / (float)heads : 0, Uncovered = group.Count(c => !Covered(buckets, c)),
                });
            }
        }
    }
}
