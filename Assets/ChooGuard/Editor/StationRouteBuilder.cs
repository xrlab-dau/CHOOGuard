using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ChooGuard.App.Fps.Emergency;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Bakes the waypoint graph people plan their walks on (<see cref="RouteGraph"/>) from the baked world navmesh and the station
    /// data: about every <see cref="Cell"/> m of walkable floor per metre of height one node, one at every exit and at both ends of every
    /// escalator and elevator, and an edge wherever the navmesh (with the escalator and elevator links the session adds) walks from
    /// one node to a nearby one without a big detour. Edges another node already covers about as well are dropped. The file is
    /// written to Resources so the session finds it beside the navmesh; run this after baking the navmesh or changing the station
    /// data. It refuses to write a graph that cannot route a place the navmesh itself reaches an exit from, and writes into the file
    /// which parts of the twin cannot reach which exits at all (defects of the twin, not hidden).
    /// </summary>
    public static class StationRouteBuilder
    {
        public const string RoutesPath = EmergencySceneBuilder.ArtRoot + "/Resources/StationRoutes.json";
        private const float Cell = 16f, Band = 1f, Reach = 32f, Vertical = 4.5f, Detour = 1.6f, Slack = 8f;

        private sealed class Draft
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<string> Labels = new List<string>();
            public readonly List<(int from, int to, float length, string link)> Edges = new List<(int, int, float, string)>();
        }

        [MenuItem("ChooGuard/Emergency/Bake route graph")]
        public static void Bake()
        {
            var art = AssetDatabase.LoadAssetAtPath<EmergencyArt>(EmergencySceneBuilder.ArtAssetPath) ?? throw new FileNotFoundException(EmergencySceneBuilder.ArtAssetPath);
            if (art.WorldNavMesh == null || art.StationData == null) throw new InvalidOperationException("EmergencyArt 에 navmesh·역 자료가 없습니다: 먼저 navmesh 를 굽습니다.");
            var points = StationPoints.Load(art.StationData);
            var instance = NavMesh.AddNavMeshData(art.WorldNavMesh);
            var links = new List<NavMeshLinkInstance>();
            try
            {
                AddLinks(points, links);
                var draft = Sample(points);
                Connect(draft, points);
                Prune(draft);
                DropPockets(draft);
                var report = Verify(draft, points);
                Write(draft, report);
                Debug.Log("CG_ROUTE_GRAPH nodes=" + draft.Positions.Count + " edges=" + draft.Edges.Count + " path=" + RoutesPath + "\n" + report);
            }
            finally
            {
                foreach (var link in links) NavMesh.RemoveLink(link);
                instance.Remove();
            }
        }

        /// <summary>The links the session adds at runtime (<c>StationWorld</c> builds the same ones from the same data), so paths may ride an escalator or a lift.</summary>
        private static void AddLinks(StationPoints points, List<NavMeshLinkInstance> links)
        {
            foreach (var entry in points.Escalators)
                links.Add(NavMesh.AddLink(new NavMeshLinkData { startPosition = entry.path[0], endPosition = entry.path[entry.path.Length - 1], width = 0, bidirectional = false, area = StationWorld.EscalatorArea, costModifier = -1 }));
            foreach (var entry in points.Elevators)
                for (int i = 0; i < entry.stops.Length; i++)
                    for (int j = i + 1; j < entry.stops.Length; j++)
                        links.Add(NavMesh.AddLink(new NavMeshLinkData { startPosition = entry.stops[i].door, endPosition = entry.stops[j].door, width = 0, bidirectional = true, area = StationWorld.ElevatorArea, costModifier = -1 }));
        }

        // ── 노드 ─────────────────────────────────────────────────────────────

        private static Draft Sample(StationPoints points)
        {
            var mesh = NavMesh.CalculateTriangulation();
            var best = new SortedDictionary<(int band, int x, int z), (Vector3 point, float distance)>();
            float limit = Cell * .75f;
            for (int t = 0; t + 2 < mesh.indices.Length; t += 3)
            {
                var a = mesh.vertices[mesh.indices[t]];
                var b = mesh.vertices[mesh.indices[t + 1]];
                var c = mesh.vertices[mesh.indices[t + 2]];
                if (Mathf.Abs((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) < 1e-3f) continue;
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / Cell), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / Cell);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / Cell), z1 = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / Cell);
                for (int cx = x0; cx <= x1; cx++)
                    for (int cz = z0; cz <= z1; cz++)
                    {
                        var centre = new Vector2((cx + .5f) * Cell, (cz + .5f) * Cell);
                        var point = ClosestOnTriangle(centre, a, b, c);
                        float distance = (new Vector2(point.x, point.z) - centre).magnitude;
                        if (distance > limit) continue;
                        var key = (Mathf.RoundToInt(point.y / Band), cx, cz);
                        if (!best.TryGetValue(key, out var old) || distance < old.distance) best[key] = (point, distance);
                    }
            }
            var floor = new List<Vector3>();
            foreach (var pair in best)
                if (NavMesh.SamplePosition(pair.Value.point, out var hit, 1f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - pair.Value.point.y) < .8f) floor.Add(hit.position);

            var special = new List<(Vector3 position, string label)>();
            foreach (var exit in points.Of(PointKind.Exit)) special.Add((exit.Position, exit.Id));
            foreach (var escalator in points.Escalators)
            {
                special.Add((escalator.path[0], escalator.id + ":start"));
                special.Add((escalator.path[escalator.path.Length - 1], escalator.id + ":end"));
            }
            foreach (var elevator in points.Elevators)
                for (int stop = 0; stop < elevator.stops.Length; stop++) special.Add((elevator.stops[stop].door, elevator.id + ":" + stop));
            var placed = new List<(Vector3 position, string label)>();
            foreach (var (position, label) in special)
            {
                if (!NavMesh.SamplePosition(position, out var hit, 3f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - position.y) > 1.5f) throw new InvalidOperationException("이 지점이 navmesh 위에 없습니다: " + label + " " + position);
                placed.Add((hit.position, label));
            }
            // 출입구·링크 끝 5 m 안의 바닥 노드는 그 노드에 자리를 내준다.
            floor.RemoveAll(f => placed.Any(p => Mathf.Abs(p.position.y - f.y) < 2.2f && new Vector2(p.position.x - f.x, p.position.z - f.z).sqrMagnitude < 25f));
            var draft = new Draft();
            foreach (var f in floor) { draft.Positions.Add(f); draft.Labels.Add(""); }
            foreach (var (position, label) in placed) { draft.Positions.Add(position); draft.Labels.Add(label); }
            return draft;
        }

        // ── 변 ──────────────────────────────────────────────────────────────

        private static void Connect(Draft draft, StationPoints points)
        {
            var grid = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < draft.Positions.Count; i++)
            {
                var key = (Mathf.FloorToInt(draft.Positions[i].x / Reach), Mathf.FloorToInt(draft.Positions[i].z / Reach));
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                list.Add(i);
            }
            var path = new NavMeshPath();
            var corners = new Vector3[512];
            for (int a = 0; a < draft.Positions.Count; a++)
            {
                var from = draft.Positions[a];
                int cx = Mathf.FloorToInt(from.x / Reach), cz = Mathf.FloorToInt(from.z / Reach);
                for (int x = cx - 1; x <= cx + 1; x++)
                    for (int z = cz - 1; z <= cz + 1; z++)
                    {
                        if (!grid.TryGetValue((x, z), out var list)) continue;
                        foreach (int b in list)
                        {
                            if (b <= a) continue;
                            var to = draft.Positions[b];
                            float flat = new Vector2(to.x - from.x, to.z - from.z).magnitude;
                            if (flat > Reach || Mathf.Abs(to.y - from.y) > Vertical) continue;
                            TryEdge(draft, points, path, corners, a, b, true);
                        }
                    }
            }
            // 승강 설비의 두 끝은 층이 달라 위 조건으로는 만나지 않는다: 링크 자체를 잇는다.
            foreach (var escalator in points.Escalators)
                TryEdge(draft, points, path, corners, Find(draft, escalator.id + ":start"), Find(draft, escalator.id + ":end"), false);
            foreach (var elevator in points.Elevators)
                for (int i = 0; i < elevator.stops.Length; i++)
                    for (int j = i + 1; j < elevator.stops.Length; j++)
                        TryEdge(draft, points, path, corners, Find(draft, elevator.id + ":" + i), Find(draft, elevator.id + ":" + j), false);
        }

        private static int Find(Draft draft, string label) => draft.Labels.IndexOf(label);

        /// <summary>Adds a→b when the navmesh walks there without a big detour, and b→a: the same walk when it uses no one-way link, else the way back is walked separately.</summary>
        private static void TryEdge(Draft draft, StationPoints points, NavMeshPath path, Vector3[] corners, int a, int b, bool ratio)
        {
            if (!Walk(draft.Positions[a], draft.Positions[b], points, path, corners, ratio, out float length, out string link)) return;
            draft.Edges.Add((a, b, length, link));
            if (link.Length == 0) { draft.Edges.Add((b, a, length, "")); return; }
            if (Walk(draft.Positions[b], draft.Positions[a], points, path, corners, ratio, out float back, out string backLink)) draft.Edges.Add((b, a, back, backLink));
        }

        private static bool Walk(Vector3 from, Vector3 to, StationPoints points, NavMeshPath path, Vector3[] corners, bool ratio, out float length, out string link)
        {
            length = 0;
            link = "";
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            int count = path.GetCornersNonAlloc(corners);
            for (int i = 1; i < count; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            if (ratio && length > Vector3.Distance(from, to) * Detour + Slack) return false;
            for (int i = 0; i + 1 < count; i++)
            {
                foreach (var escalator in points.Escalators)
                    if (Near(corners[i], escalator.path[0]) && Near(corners[i + 1], escalator.path[escalator.path.Length - 1])) link = escalator.id;
                foreach (var elevator in points.Elevators)
                    for (int stop = 0; stop < elevator.stops.Length; stop++)
                        for (int other = 0; other < elevator.stops.Length; other++)
                            if (stop != other && Near(corners[i], elevator.stops[stop].door) && Near(corners[i + 1], elevator.stops[other].door)) link = RouteGraph.ElevatorLink;
            }
            return true;
        }

        private static bool Near(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < .9f && (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z) < .8f;

        /// <summary>
        /// Drops a walk that two hops through another node cover about as well (within a tenth), longest walks first and each judged
        /// against the walks still kept, so what is dropped is always still covered: routes lose nothing and the graph stays a few
        /// edges per node.
        /// </summary>
        private static void Prune(Draft draft)
        {
            var kept = new Dictionary<(int, int), float>();
            var around = new Dictionary<int, List<int>>();
            foreach (var (from, to, length, _) in draft.Edges)
            {
                kept[(from, to)] = length;
                if (!around.TryGetValue(from, out var list)) around[from] = list = new List<int>();
                list.Add(to);
            }
            var dropped = new HashSet<int>();
            foreach (int i in Enumerable.Range(0, draft.Edges.Count).OrderByDescending(i => draft.Edges[i].length))
            {
                var (from, to, length, link) = draft.Edges[i];
                if (link.Length > 0) continue;
                foreach (int c in around[from])
                {
                    if (c == to || !kept.TryGetValue((c, to), out float second) || kept[(from, c)] + second > length * 1.1f + .5f) continue;
                    dropped.Add(i);
                    kept.Remove((from, to));
                    around[from].Remove(to);
                    break;
                }
            }
            var remaining = draft.Edges.Where((e, i) => !dropped.Contains(i)).ToList();
            draft.Edges.Clear();
            draft.Edges.AddRange(remaining);
        }

        /// <summary>
        /// Removes tiny pieces of floor the walk network does not join (a pocket of navmesh between a bench and a wall, a sliver at an
        /// escalator well): a node there is the nearest node of the places around it and leads nowhere. A piece of three nodes or fewer
        /// holding no exit, escalator or elevator end goes; larger pieces (track beds, the street) stay and are reported.
        /// </summary>
        private static void DropPockets(Draft draft)
        {
            int count = draft.Positions.Count;
            var parent = Enumerable.Range(0, count).ToArray();
            int Root(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
            foreach (var (from, to, _, _) in draft.Edges) parent[Root(from)] = Root(to);
            var size = new int[count];
            var labelled = new bool[count];
            for (int i = 0; i < count; i++) { size[Root(i)]++; if (draft.Labels[i].Length > 0) labelled[Root(i)] = true; }
            var keep = Enumerable.Range(0, count).Where(i => size[Root(i)] > 3 || labelled[Root(i)]).ToList();
            if (keep.Count == count) return;
            var renumber = new Dictionary<int, int>();
            foreach (int i in keep) renumber[i] = renumber.Count;
            var positions = keep.Select(i => draft.Positions[i]).ToList();
            var labels = keep.Select(i => draft.Labels[i]).ToList();
            var edges = draft.Edges.Where(e => renumber.ContainsKey(e.from) && renumber.ContainsKey(e.to)).Select(e => (renumber[e.from], renumber[e.to], e.length, e.link)).ToList();
            draft.Positions.Clear();
            draft.Positions.AddRange(positions);
            draft.Labels.Clear();
            draft.Labels.AddRange(labels);
            draft.Edges.Clear();
            draft.Edges.AddRange(edges);
        }

        // ── 검사와 기록 ──────────────────────────────────────────────────────

        /// <summary>
        /// Every place of the station must reach an exit through the graph whenever the navmesh itself reaches one (a graph that
        /// disagrees is refused); the returned account lists what cannot reach which exit at all.
        /// </summary>
        private static string Verify(Draft draft, StationPoints points)
        {
            int count = draft.Positions.Count;
            var report = new StringBuilder();
            report.Append(count).Append(" nodes, ").Append(draft.Edges.Count).Append(" edges. ");
            var reaching = new bool[count];
            var byStairsOrEscalator = new bool[count];
            foreach (var exit in points.Of(PointKind.Exit))
            {
                int goal = Find(draft, exit.Id);
                var seen = ReachBack(draft, goal, true);
                var walking = ReachBack(draft, goal, false);
                for (int i = 0; i < count; i++) { reaching[i] |= seen[i]; byStairsOrEscalator[i] |= walking[i]; }
                report.Append(exit.Id).Append(": ").Append(seen.Count(s => s)).Append('/').Append(count).Append(" nodes reach it; ");
            }
            var orphans = Enumerable.Range(0, count).Where(i => !reaching[i]).ToList();
            report.Append(orphans.Count).Append(" nodes reach no exit");
            if (orphans.Count > 0)
                report.Append(" (").Append(string.Join(", ", orphans.GroupBy(i => (points.ZoneAt(draft.Positions[i])?.id ?? "outside the zones") + " y=" + Mathf.RoundToInt(draft.Positions[i].y)).OrderByDescending(g => g.Count()).Take(12).Select(g => g.Key + " x" + g.Count()))).Append(')');
            int liftOnly = Enumerable.Range(0, count).Count(i => reaching[i] && !byStairsOrEscalator[i]);
            report.Append(", ").Append(liftOnly).Append(" nodes reach an exit only by the elevator. ");

            var mismatches = new List<string>();
            var probe = new NavMeshPath();
            int placesTotal = 0, placesReaching = 0, placesLiftOnly = 0;
            foreach (var point in points.All)
            {
                if (point.Kind == PointKind.Exit) continue;
                placesTotal++;
                var from = StationWorld.OnNavMesh(point.Position, 2f);
                var candidates = NearestNodes(draft, from, 3);
                int nearest = candidates.Where(i => reaching[i]).DefaultIfEmpty(-1).First();
                if (nearest >= 0) { placesReaching++; if (!byStairsOrEscalator[nearest]) placesLiftOnly++; continue; }
                bool navmeshOk = false;
                foreach (var exit in points.Of(PointKind.Exit))
                    if (NavMesh.CalculatePath(from, exit.Position, NavMesh.AllAreas, probe) && probe.status == NavMeshPathStatus.PathComplete) { navmeshOk = true; break; }
                if (navmeshOk) mismatches.Add(point.Id + " " + Format(point.Position) + " nearest node " + (candidates.Count > 0 ? Format(draft.Positions[candidates[0]]) : "none"));
            }
            report.Append(placesReaching).Append('/').Append(placesTotal).Append(" places reach an exit on the graph (").Append(placesLiftOnly).Append(" of them only by the elevator)");
            // 곳 → 에스컬레이터 끝·출구·엘리베이터 문: navmesh 가 200 m 안 걸음으로 닿는 쌍은 그래프도 이어야 한다.
            var disagreements = CheckAgreement(draft, points);
            report.Append(", ").Append(disagreements.Count == 0 ? "the graph joins every place to every exit and link end the navmesh walks to within 200 m" : disagreements.Count + " place-to-link pairs the navmesh walks but the graph does not");
            if (mismatches.Count > 0 || disagreements.Count > 0)
                throw new InvalidOperationException("경로 그래프가 navmesh 가 닿는 곳을 잇지 못합니다: 출구에 " + mismatches.Count + "곳(" + string.Join(", ", mismatches.Take(6)) + "), 링크·출구 끝에 " + disagreements.Count + "쌍(" + string.Join(", ", disagreements.Take(6)) + ") — 노드 간격이나 변 조건을 손봅니다.\n" + report);
            return report.ToString();
        }

        /// <summary>
        /// Places and the ends of links and exits: every pair the navmesh walks within 200 m (a longer walk is a detour the graph
        /// need not follow: the street strip beside the west exit is only reached through the station, 413 m) must be joined on the
        /// graph as well. Returns the pairs it is not.
        /// </summary>
        private static List<string> CheckAgreement(Draft draft, StationPoints points)
        {
            int count = draft.Positions.Count;
            var specials = Enumerable.Range(0, count).Where(i => draft.Labels[i].Length > 0).ToList();
            var forward = new List<int>[count];
            foreach (var (from, to, _, _) in draft.Edges) (forward[from] ??= new List<int>()).Add(to);
            var pairs = new List<string>();
            var probe = new NavMeshPath();
            var corners = new Vector3[512];
            foreach (var point in points.All)
            {
                if (point.Kind == PointKind.Exit) continue;
                var from = StationWorld.OnNavMesh(point.Position, 2f);
                var seen = new bool[count];
                var stack = new Stack<int>();
                foreach (int start in NearestNodes(draft, from, 3)) { seen[start] = true; stack.Push(start); }
                while (stack.Count > 0)
                {
                    int node = stack.Pop();
                    if (forward[node] == null) continue;
                    foreach (int next in forward[node]) if (!seen[next]) { seen[next] = true; stack.Push(next); }
                }
                foreach (int special in specials)
                {
                    if (seen[special] || (draft.Positions[special] - from).magnitude > 120f) continue;
                    if (!NavMesh.CalculatePath(from, draft.Positions[special], NavMesh.AllAreas, probe) || probe.status != NavMeshPathStatus.PathComplete) continue;
                    int c = probe.GetCornersNonAlloc(corners);
                    float length = 0;
                    for (int k = 1; k < c; k++) length += Vector3.Distance(corners[k - 1], corners[k]);
                    if (length < 200f) pairs.Add(point.Id + " -> " + draft.Labels[special]);
                }
            }
            return pairs;
        }

        /// <summary>The nodes from which <paramref name="goal"/> can be reached along the edges (the elevator's included when <paramref name="lifts"/>).</summary>
        private static bool[] ReachBack(Draft draft, int goal, bool lifts)
        {
            var reverse = new List<int>[draft.Positions.Count];
            foreach (var (from, to, _, link) in draft.Edges)
                if (lifts || link != RouteGraph.ElevatorLink) (reverse[to] ??= new List<int>()).Add(from);
            var seen = new bool[draft.Positions.Count];
            var stack = new Stack<int>();
            seen[goal] = true;
            stack.Push(goal);
            while (stack.Count > 0)
            {
                int node = stack.Pop();
                if (reverse[node] == null) continue;
                foreach (int from in reverse[node]) if (!seen[from]) { seen[from] = true; stack.Push(from); }
            }
            return seen;
        }

        /// <summary>The session's rule for the node a place belongs to: the nearest on the place's own storey (else the nearest of all); when that one leads nowhere it tries the next (<paramref name="count"/> in all).</summary>
        private static List<int> NearestNodes(Draft draft, Vector3 position, int count) =>
            Enumerable.Range(0, draft.Positions.Count)
                .Select(i => { var d = draft.Positions[i] - position; return (index: i, squared: d.x * d.x + d.z * d.z + 4f * d.y * d.y, sameStorey: Mathf.Abs(d.y) < 2.5f); })
                .OrderBy(n => n.sameStorey ? 0 : 1).ThenBy(n => n.squared)
                .Take(count).Select(n => n.index).ToList();

        private static string Format(Vector3 v) => "(" + v.x.ToString("0.#") + ", " + v.y.ToString("0.#") + ", " + v.z.ToString("0.#") + ")";

        private static void Write(Draft draft, string report)
        {
            var file = new RouteGraph.File
            {
                version = RouteGraph.Version,
                report = report,
                positions = draft.Positions.SelectMany(p => new[] { Mathf.RoundToInt(p.x * 100), Mathf.RoundToInt(p.y * 100), Mathf.RoundToInt(p.z * 100) }).ToArray(),
                labels = draft.Labels.ToArray(),
                from = draft.Edges.Select(e => e.from).ToArray(),
                to = draft.Edges.Select(e => e.to).ToArray(),
                length = draft.Edges.Select(e => Mathf.RoundToInt(e.length * 100)).ToArray(),
                link = draft.Edges.Select(e => e.link).ToArray(),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(RoutesPath));
            File.WriteAllText(RoutesPath, JsonUtility.ToJson(file));
            AssetDatabase.ImportAsset(RoutesPath);
        }

        // ── 삼각형 위의 가장 가까운 점 ───────────────────────────────────────────

        /// <summary>The point of triangle <paramref name="a3"/>,<paramref name="b3"/>,<paramref name="c3"/> nearest <paramref name="p"/> in the horizontal plane, at the triangle's height there.</summary>
        private static Vector3 ClosestOnTriangle(Vector2 p, Vector3 a3, Vector3 b3, Vector3 c3)
        {
            var a = new Vector2(a3.x, a3.z);
            var b = new Vector2(b3.x, b3.z);
            var c = new Vector2(c3.x, c3.z);
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            float d1 = Vector2.Dot(ab, ap), d2 = Vector2.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a3;
            var bp = p - b;
            float d3 = Vector2.Dot(ab, bp), d4 = Vector2.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b3;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a3 + (b3 - a3) * (d1 / (d1 - d3));
            var cp = p - c;
            float d5 = Vector2.Dot(ab, cp), d6 = Vector2.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c3;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a3 + (c3 - a3) * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b3 + (c3 - b3) * ((d4 - d3) / (d4 - d3 + (d5 - d6)));
            float denominator = 1f / (va + vb + vc);
            return a3 + (b3 - a3) * (vb * denominator) + (c3 - a3) * (vc * denominator);
        }
    }
}
