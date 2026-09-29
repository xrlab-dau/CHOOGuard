using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Finds the way for every walking person. A body asks for a route to its goal (<see cref="PersonBody.GoTo"/>); the service
    /// works the requests off in a time-sliced queue, <see cref="BudgetMs"/> of main-thread time per frame at most (evacuees
    /// first), and hands each agent a short path with <c>NavMeshAgent.SetPath</c>. The agents' own asynchronous path queue is
    /// never used: with about a hundred people told to leave at once it filled up for tens of seconds and left them standing.
    /// A long trip is planned on the <see cref="RouteGraph"/> (shared by everyone starting from the same place), and the body
    /// then asks for one short leg after another as it walks; a leg that cannot be walked any more (a door locked, the way burned,
    /// an escalator closed) blocks that edge of the graph for a while and the trip is planned again from where the body stands.
    /// </summary>
    public sealed class PathService
    {
        /// <summary>Main-thread milliseconds of path work per frame, at most one request more (a request is a tree lookup and one short path).</summary>
        public const float BudgetMs = 1f;
        /// <summary>A goal this near on the same floor is walked to directly, without a route.</summary>
        public const float DirectMeters = 25f;
        /// <summary>How long an edge whose walk failed stays out of routes (game seconds).</summary>
        public const float BlockSeconds = 30f;
        private const int Attempts = 3, StartTries = 3;
        private float requestMs = .15f;

        public RouteGraph Graph { get; }

        /// <summary>Somewhere that cannot be reached (from where, to where, why); each pair of 6 m cells is reported once.</summary>
        public event Action<Vector3, Vector3, string> Unreachable;

        private readonly StationWorld world;
        private readonly Queue<PersonBody> urgent = new Queue<PersonBody>(), everyday = new Queue<PersonBody>();
        private readonly Dictionary<StationPoints.Point, RouteGraph.Node> exitNodes = new Dictionary<StationPoints.Point, RouteGraph.Node>();
        private readonly HashSet<RouteGraph.Node> exits = new HashSet<RouteGraph.Node>();
        private readonly List<(Vector3 centre, float radius)> dangers = new List<(Vector3, float)>();
        private readonly List<RouteGraph.Node> chain = new List<RouteGraph.Node>(), starts = new List<RouteGraph.Node>();
        private readonly HashSet<(int, int, int, int, int, int)> reported = new HashSet<(int, int, int, int, int, int)>();
        private readonly NavMeshPath path = new NavMeshPath();

        public int Queued => urgent.Count + everyday.Count;
        /// <summary>Requests served so far (plans and legs).</summary>
        public int Served { get; private set; }
        public int Abandoned { get; private set; }

        public PathService(StationWorld world, RouteGraph graph)
        {
            this.world = world;
            Graph = graph;
            foreach (var exit in world.Points.Of(PointKind.Exit))
            {
                var node = graph.ByLabel(exit.Id);
                if (node == null) continue;
                exitNodes[exit] = node;
                exits.Add(node);
            }
        }

        /// <summary>The body needs a leg (or a whole plan): it is served in the next frames' budget.</summary>
        public void Enqueue(PersonBody body)
        {
            if (body.Queued) return;
            body.Queued = true;
            (body.Urgent ? urgent : everyday).Enqueue(body);
        }

        /// <summary>
        /// Works the queue within <see cref="BudgetMs"/>: a request is only started when the average request still fits in what is
        /// left (one is always served, so nothing waits forever). Returns the milliseconds it took and how many requests it served.
        /// </summary>
        public float Tick(out int served)
        {
            long began = Stopwatch.GetTimestamp();
            served = 0;
            while (urgent.Count > 0 || everyday.Count > 0)
            {
                float used = Ms(Stopwatch.GetTimestamp() - began);
                if (served > 0 && used + requestMs * 1.5f > BudgetMs) break;
                var body = urgent.Count > 0 ? urgent.Dequeue() : everyday.Dequeue();
                if (body == null) continue;
                long started = Stopwatch.GetTimestamp();
                Serve(body);
                served++;
                // 처음 한 번의 느린 호출(코드 준비)이 평균을 끌어올려 다음 프레임들을 굶기지 않게 2 ms 로 자른다.
                requestMs = Mathf.Lerp(requestMs, Mathf.Min(2f, Ms(Stopwatch.GetTimestamp() - started)), .05f);
            }
            Served += served;
            return Ms(Stopwatch.GetTimestamp() - began);
        }

        private static float Ms(long ticks) => ticks * 1000f / Stopwatch.Frequency;

        // ── 한 몸의 요청 ─────────────────────────────────────────────────────

        private void Serve(PersonBody body)
        {
            body.Queued = false;
            if (!body.WantsRoute) return;
            if (body.Fresh && !Plan(body)) { Abandon(body, "no route on the graph"); return; }
            Lead(body);
        }

        /// <summary>
        /// The few nodes nearest <paramref name="from"/>, nearest first: the nearest one can be a dead end (a sliver of navmesh between
        /// a bench and a wall) that leads nowhere, so a route is planned from whichever of them is cheapest to leave by.
        /// </summary>
        private List<RouteGraph.Node> Starts(Vector3 from, RouteGraph.Node skip)
        {
            starts.Clear();
            if (skip != null) starts.Add(skip);
            for (int i = 0; i < StartTries; i++)
            {
                var node = Graph.Nearest(from, starts);
                if (node == null) break;
                starts.Add(node);
            }
            if (skip != null) starts.Remove(skip);
            return starts;
        }

        /// <summary>Chooses the chain of nodes from where the body stands to its goal (empty for a short trip).</summary>
        private bool Plan(PersonBody body)
        {
            var from = body.transform.position;
            var goal = body.Goal;
            body.Chain.Clear();
            body.Step = 0;
            body.Fresh = false;
            var d = goal - from;
            if (d.x * d.x + d.z * d.z <= DirectMeters * DirectMeters && Mathf.Abs(d.y) < 2.5f) return true;
            var end = Graph.Nearest(goal);
            var candidates = Starts(from, body.SkipStart);
            if (end == null || candidates.Count == 0) return true;
            RouteGraph.Node start = null;
            if (exits.Contains(end))
            {
                // 출구로 가는 길은 역 전체가 나누어 쓰는 출구별 탐색 하나에서 얻는다.
                var field = Graph.Toward(end, body.Profile, Dangers(null, 0), Time.time);
                float best = float.PositiveInfinity;
                foreach (var node in candidates)
                {
                    float cost = field.CostOf(node) + Vector3.Distance(from, node.Position) * 1.3f;
                    if (cost < best) { best = cost; start = node; }
                }
                if (start == null) return false;
                field.PathFrom(start, chain);
            }
            else
            {
                // 그 밖의 곳은 같은 노드에서 떠나는 사람끼리 나누는 탐색에서 얻는다.
                foreach (var node in candidates)
                {
                    var tree = Graph.Explore(node, body.Profile, Dangers(null, 0), Time.time);
                    if (!tree.Reaches(end)) continue;
                    tree.PathTo(end, chain);
                    start = node;
                    break;
                }
                if (start == null) return false;
            }
            // 몸이 이미 첫 노드를 지나쳤거나 다음 노드가 더 가까우면 되돌아가지 않는다.
            while (chain.Count >= 2 && Vector3.Distance(from, chain[1].Position) <= Vector3.Distance(chain[0].Position, chain[1].Position)) chain.RemoveAt(0);
            while (chain.Count >= 2 && Vector3.Distance(goal, chain[chain.Count - 2].Position) <= Vector3.Distance(chain[chain.Count - 1].Position, chain[chain.Count - 2].Position)) chain.RemoveAt(chain.Count - 1);
            body.Chain.AddRange(chain);
            return true;
        }

        /// <summary>Gives the agent the path to the next stop of its trip, or plans the trip again when that walk is closed.</summary>
        private void Lead(PersonBody body)
        {
            var agent = body.Agent;
            var target = body.Target;
            bool last = body.Step >= body.Chain.Count;
            if (!agent.CalculatePath(target, path) || path.status == NavMeshPathStatus.PathInvalid || !last && path.status == NavMeshPathStatus.PathPartial)
            {
                Closed(body, last);
                return;
            }
            // 목적지 바로 앞까지만 닿는 길은 그대로 걷는다(도착하지 못한 사람은 승객이 막힌 길로 판단에 올린다).
            if (last && path.status == NavMeshPathStatus.PathPartial) Report(body.transform.position, target, "the last leg ends short of the goal");
            agent.isStopped = false;
            if (!agent.SetPath(path)) { Closed(body, last); return; }
            body.Attempts = 0;
        }

        private void Closed(PersonBody body, bool last)
        {
            if (last || ++body.Attempts > Attempts) { Abandon(body, (last ? "the last leg has no walk" : "legs keep closing") + " (target " + body.Target.ToString("F1") + ", step " + body.Step + " of " + body.Chain.Count + ")"); return; }
            if (body.Step > 0) Graph.Block(Graph.EdgeBetween(body.Chain[body.Step - 1], body.Chain[body.Step]), Time.time + BlockSeconds);
            else body.SkipStart = body.Chain[0];
            body.Replan(true);
        }

        private void Abandon(PersonBody body, string why)
        {
            Abandoned++;
            Report(body.transform.position, body.Goal, why);
            body.GiveUp();
        }

        // ── 안전한 출구 ──────────────────────────────────────────────────────

        /// <summary>
        /// The exit with the cheapest route from <paramref name="from"/> that avoids every active hazard (and <paramref name="avoid"/>
        /// within <paramref name="clearance"/>), one an earlier way turned out blocked (<paramref name="except"/>) excluded while another
        /// exists. One search per exit serves the whole station, so telling a whole hall to leave costs a table lookup per person.
        /// When no exit can be reached from here the one farthest from the danger is chosen, and the failed pairs are reported.
        /// </summary>
        public StationPoints.Point SafeExit(Vector3 from, Vector3? avoid, float clearance, StationPoints.Point except = null)
        {
            StationPoints.Point best = null, farthest = null;
            float bestCost = float.PositiveInfinity, farthestDistance = -1;
            var candidates = Starts(from, null);
            var danger = Dangers(avoid, clearance);
            foreach (var exit in world.Points.Of(PointKind.Exit))
            {
                if (exit == except) continue;
                if (avoid.HasValue)
                {
                    float away = Vector3.Distance(exit.Position, avoid.Value);
                    if (away > farthestDistance) { farthestDistance = away; farthest = exit; }
                }
                float cost = float.PositiveInfinity;
                if (exitNodes.TryGetValue(exit, out var node))
                {
                    var field = Graph.Toward(node, RouteProfile.Evacuation, danger, Time.time);
                    foreach (var start in candidates) cost = Mathf.Min(cost, field.CostOf(start) + Vector3.Distance(from, start.Position) * 1.3f);
                }
                if (float.IsPositiveInfinity(cost)) { Report(from, exit.Position, "exit " + exit.Id + " has no route from here"); continue; }
                if (cost < bestCost) { bestCost = cost; best = exit; }
            }
            return best ?? farthest ?? world.RandomExit(except);
        }

        /// <summary>Active hazards, closed-off discs and the caller's own danger spot, as zones routes keep away from.</summary>
        private List<(Vector3 centre, float radius)> Dangers(Vector3? extra, float clearance)
        {
            dangers.Clear();
            var active = HazardRegistry.Active;
            for (int i = 0; i < active.Count; i++)
                if (active[i].Active && active[i].Localized && active[i].Clearance > 0) dangers.Add((active[i].Position, active[i].Clearance));
            foreach (var zone in world.Closed) dangers.Add((zone.centre, zone.radius));
            // 호출한 쪽이 넘긴 위험 자리가 이미 등록된 위험과 같으면 다시 넣지 않는다(같은 상황에 탐색 키가 둘로 갈라지지 않게).
            if (extra.HasValue && clearance > 0)
            {
                bool known = false;
                foreach (var (centre, radius) in dangers)
                    if ((centre - extra.Value).sqrMagnitude < 9f && radius >= clearance - 2f) { known = true; break; }
                if (!known) dangers.Add((extra.Value, clearance));
            }
            return dangers;
        }

        private void Report(Vector3 from, Vector3 to, string why)
        {
            if (Unreachable == null) return;
            var key = (Mathf.RoundToInt(from.x / 6f), Mathf.RoundToInt(from.y / 3f), Mathf.RoundToInt(from.z / 6f), Mathf.RoundToInt(to.x / 6f), Mathf.RoundToInt(to.y / 3f), Mathf.RoundToInt(to.z / 6f));
            if (reported.Add(key)) Unreachable(from, to, why);
        }
    }
}
