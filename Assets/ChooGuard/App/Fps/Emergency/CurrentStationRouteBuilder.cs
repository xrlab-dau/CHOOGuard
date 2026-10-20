using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Current physical NavMesh graph; immutable historical graph is retained as source inventory.</summary>
    internal sealed class CurrentStationRouteBuilder
    {
        const float Cell=16f,Band=1f,Reach=32f,Vertical=4.5f,Detour=1.6f,Slack=8f;
        readonly StationWalkingRouteEntry[] walkingRoutes;
        sealed class Draft
        {
            public readonly List<Vector3> Positions=new List<Vector3>();
            public readonly List<string> Labels=new List<string>();
            public readonly List<(int from,int to,float length,string link)> Edges=new List<(int,int,float,string)>();
        }
        CurrentStationRouteBuilder(IReadOnlyList<StationWalkingRoute> walking)
        { walkingRoutes=walking.Select(w=>w.Entry).ToArray(); }

        internal static RouteGraph Build(StationPoints current,IReadOnlyList<Escalator> supplied,IReadOnlyList<Elevator> elevators,
            IReadOnlyList<StationWalkingRoute> walking,TextAsset original)
        {
            var points=current.RoutingView(supplied,elevators);
            var builder=new CurrentStationRouteBuilder(walking);
            var draft=builder.Sample(points);builder.Connect(draft,points);builder.Prune(draft);
            builder.AttachEveryCurrentPoint(draft,points);
            var file=new RouteGraph.File {version=RouteGraph.Version,
                positions=draft.Positions.SelectMany(p=>new[]{Mathf.RoundToInt(p.x*100),Mathf.RoundToInt(p.y*100),Mathf.RoundToInt(p.z*100)}).ToArray(),
                labels=draft.Labels.ToArray(),from=draft.Edges.Select(e=>e.from).ToArray(),to=draft.Edges.Select(e=>e.to).ToArray(),
                length=draft.Edges.Select(e=>Mathf.RoundToInt(e.length*100)).ToArray(),link=draft.Edges.Select(e=>e.link).ToArray(),
                report="Current source MODEL walkability; "+current.All.Count+" bound source points of "+current.SourceAll.Count+
                    "; "+current.CurrentBindings.Count(r=>!r.Bound)+" explicitly witnessed UNBOUND. Historical graph immutable; edges are current actual paths."};
            var graph=RouteGraph.Load(file,supplied,walking);graph.RetainSource(original);
            foreach(var node in graph.Nodes)
                if(!NavMesh.SamplePosition(node.Position,out _, .6f,NavMesh.AllAreas))
                    throw new InvalidOperationException("Derived current graph node is off the current NavMesh: "+node.Label);
            var fields=points.Of(PointKind.Exit).Select(e=>graph.Toward(graph.ByLabel(e.Id),RouteProfile.Evacuation,
                Array.Empty<(Vector3,float)>(),Time.time)).ToArray();
            foreach(var point in points.All)
                if(!fields.Any(f=>f.Reaches(graph.ByLabel(point.Id))))
                    throw new InvalidOperationException("Current physically qualified point has no actual foot-exit route: "+point.Id);
            return graph;
        }

        void AttachEveryCurrentPoint(Draft draft,StationPoints points)
        {
            int baseline=draft.Positions.Count;
            var path=new NavMeshPath();var corners=new Vector3[512];
            foreach(var point in points.All)
            {
                if(point.Kind==PointKind.Exit)continue;
                var candidates=Enumerable.Range(0,baseline).Where(i=>Mathf.Abs(draft.Positions[i].y-point.Position.y)<2.5f)
                    .OrderBy(i=>(draft.Positions[i]-point.Position).sqrMagnitude).ToArray();
                int index=draft.Positions.Count;draft.Positions.Add(point.Position);draft.Labels.Add(point.Id);
                int attached=0;
                foreach(int other in candidates)
                {
                    if(Vector3.Distance(point.Position,draft.Positions[other])>Reach)break;
                    int before=draft.Edges.Count;
                    TryEdge(draft,points,path,corners,index,other,true);
                    if(draft.Edges.Count>before&&++attached==3)break;
                }
                if(attached==0)throw new InvalidOperationException("Current source point cannot attach by an actual NavMesh path: "+point.Id);
            }
        }

        private Draft Sample(StationPoints points)
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
            foreach (var route in walkingRoutes)
            {
                special.Add((route.Start, StationWalkingRoutes.LinkPrefix + route.id + ":start"));
                special.Add((route.End, StationWalkingRoutes.LinkPrefix + route.id + ":end"));
            }
            foreach (var exit in points.Of(PointKind.Exit)) special.Add((exit.Position, exit.Id));
            foreach (var escalator in points.Escalators)
            {
                special.Add((escalator.path[0], escalator.id + ":start"));
                special.Add((escalator.path[escalator.path.Length - 1], escalator.id + ":end"));
                // 계단 양 끝에도 노드를 둔다: 에스컬레이터 끝이 계단과 이어지지 않은 navmesh 조각 위에 있으면(5·6 승강장 북쪽 등) 계단을 이을 노드가 없어 그 층 오르내림이 수백 m 돌아가는 길이 된다.
                if (escalator.stairs)
                {
                    special.Add((escalator.stairsTop, escalator.id + ":stairs-top"));
                    special.Add((escalator.stairsBottom, escalator.id + ":stairs-bottom"));
                }
            }
            foreach (var elevator in points.Elevators)
                for (int stop = 0; stop < elevator.stops.Length; stop++) special.Add((elevator.stops[stop].door, elevator.id + ":" + stop));
            var placed = new List<(Vector3 position, string label)>();
            foreach (var (position, label) in special)
            {
                bool measured = label.StartsWith(StationWalkingRoutes.LinkPrefix, StringComparison.Ordinal);
                if (!NavMesh.SamplePosition(position, out var hit, measured ? .12f : 3f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - position.y) > (measured ? .15f : 1.5f)) throw new InvalidOperationException("이 지점이 navmesh 위에 없습니다: " + label + " " + position);
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

        private void Connect(Draft draft, StationPoints points)
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
            foreach (var route in walkingRoutes)
            {
                int a = Find(draft, StationWalkingRoutes.LinkPrefix + route.id + ":start"), b = Find(draft, StationWalkingRoutes.LinkPrefix + route.id + ":end");
                float length = route.Length + Vector3.Distance(draft.Positions[a], route.Start) + Vector3.Distance(route.End, draft.Positions[b]);
                if(!NavMesh.CalculatePath(draft.Positions[a],draft.Positions[b],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete||
                   !NavMesh.CalculatePath(draft.Positions[b],draft.Positions[a],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Physically verified walking link does not attach to current navigation: "+route.id);
                draft.Edges.Add((a, b, length, StationWalkingRoutes.LinkPrefix + route.id));
                draft.Edges.Add((b, a, length, StationWalkingRoutes.LinkPrefix + route.id));
            }
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
            // 승강 설비의 두 끝(계단의 두 끝도)은 층이 달라 위 조건으로는 만나지 않는다: 링크와 계단 자체를 잇는다.
            foreach (var escalator in points.Escalators)
            {
                TryEdge(draft, points, path, corners, Find(draft, escalator.id + ":start"), Find(draft, escalator.id + ":end"), false);
                if (escalator.stairs) TryEdge(draft, points, path, corners, Find(draft, escalator.id + ":stairs-top"), Find(draft, escalator.id + ":stairs-bottom"), false);
            }
            foreach (var elevator in points.Elevators)
                for (int i = 0; i < elevator.stops.Length; i++)
                    for (int j = i + 1; j < elevator.stops.Length; j++)
                        TryEdge(draft, points, path, corners, Find(draft, elevator.id + ":" + i), Find(draft, elevator.id + ":" + j), false);
        }

        private int Find(Draft draft, string label) => draft.Labels.IndexOf(label);

        /// <summary>Adds a→b when the navmesh walks there without a big detour, and b→a: the same walk when it uses no one-way link, else the way back is walked separately.</summary>
        private void TryEdge(Draft draft, StationPoints points, NavMeshPath path, Vector3[] corners, int a, int b, bool ratio)
        {
            if (!Walk(draft.Positions[a], draft.Positions[b], points, path, corners, ratio, out float length, out string link)) return;
            draft.Edges.Add((a, b, length, link));
            if (Walk(draft.Positions[b], draft.Positions[a], points, path, corners, ratio, out float back, out string backLink)) draft.Edges.Add((b, a, back, backLink));
        }

        private bool Walk(Vector3 from, Vector3 to, StationPoints points, NavMeshPath path, Vector3[] corners, bool ratio, out float length, out string link)
        {
            length = 0;
            link = "";
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            int count = path.GetCornersNonAlloc(corners);
            if(count==0||count>=corners.Length)return false;
            for (int i = 1; i < count; i++)
            {
                // A measured walk is its dedicated endpoint↔endpoint graph arc.
                // Excluding generic crossings prevents a composite path from losing
                // a second walking owner or an existing transport-link closure.
                if (walkingRoutes.Any(w => w.Matches(corners[i - 1], corners[i], out _))) return false;
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }
            if (ratio && length > Vector3.Distance(from, to) * Detour + Slack) return false;
            for (int i = 0; i + 1 < count; i++)
            {
                foreach (var escalator in points.Escalators)
                    if (Near(corners[i], escalator.path[0]) && Near(corners[i + 1], escalator.path[escalator.path.Length - 1]))
                    { if(link.Length>0&&link!=escalator.id)return false;link=escalator.id; }
                foreach (var elevator in points.Elevators)
                    for (int stop = 0; stop < elevator.stops.Length; stop++)
                        for (int other = 0; other < elevator.stops.Length; other++)
                            if (stop != other && Near(corners[i], elevator.stops[stop].door) && Near(corners[i + 1], elevator.stops[other].door))
                            { if(link.Length>0&&link!=RouteGraph.ElevatorLink)return false;link=RouteGraph.ElevatorLink; }
            }
            return true;
        }

        private bool Near(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < .9f && (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z) < .8f;

        /// <summary>
        /// Drops a walk that two hops through another node cover about as well (within a tenth), longest walks first and each judged
        /// against the walks still kept, so what is dropped is always still covered: routes lose nothing and the graph stays a few
        /// edges per node.
        /// </summary>
        private void Prune(Draft draft)
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
        // ── 검사와 기록 ──────────────────────────────────────────────────────

        /// <summary>
        /// Every place of the station must reach an exit through the graph whenever the navmesh itself reaches one (a graph that
        /// disagrees is refused); the returned account lists what cannot reach which exit at all.
        /// </summary>
        private bool[] ReachBack(Draft draft, int goal, bool lifts)
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
        private List<int> NearestNodes(Draft draft, Vector3 position, int count) =>
            Enumerable.Range(0, draft.Positions.Count)
                .Select(i => { var d = draft.Positions[i] - position; return (index: i, squared: d.x * d.x + d.z * d.z + 4f * d.y * d.y, sameStorey: Mathf.Abs(d.y) < 2.5f); })
                .OrderBy(n => n.sameStorey ? 0 : 1).ThenBy(n => n.squared)
                .Take(count).Select(n => n.index).ToList();

        private string Format(Vector3 v) => "(" + v.x.ToString("0.#") + ", " + v.y.ToString("0.#") + ", " + v.z.ToString("0.#") + ")";

        private Vector3 ClosestOnTriangle(Vector2 p, Vector3 a3, Vector3 b3, Vector3 c3)
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
