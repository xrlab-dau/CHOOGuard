using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Plans the route the staff member's map and floor marks show to a hazard they know of. It reads only what the staff member knows — the
    /// hazards, closed-off discs and closed escalators the director hands over (<see cref="Hazards"/>, <see cref="Closures"/>, <see cref="Escalators"/>) —
    /// and the station as baked: the links of the <see cref="RouteGraph"/> (<see cref="RouteGraph.Survey"/>, never its live blocks or escalator state) and
    /// the <see cref="BakedFloor"/>. A fire, a locked door or a shutter the staff member has not met carves the live navmesh and shuts live links; none
    /// of it is read here, so neither the shape of the route nor "no way through" shows what has not been found. The moment something is known, it
    /// counts. It draws no random numbers (the world's sequence is not touched) and allocates nothing per call once warm.
    /// The route runs on the graph's links with real floor corners, from the staff member to a point on the ring <see cref="Hazard.ApproachRadius"/> round the
    /// hazard's <see cref="Hazard.Scene"/>; it is found again from scratch each time, so a newly known obstruction is always avoided or reported.
    /// </summary>
    public sealed class GuideRoute : RouteGraph.IRules
    {
        public enum Outcome { Route, Arrived, NoRoute }

        /// <summary>A closed-off disc the staff member knows of (a cordon they put up, a shutter they saw down); <see cref="Owner"/> is the hazard it was put round, if any.</summary>
        public readonly struct Closure
        {
            public readonly Vector3 Centre;
            public readonly float Radius;
            public readonly Hazard Owner;

            public Closure(Vector3 centre, float radius, Hazard owner)
            {
                Centre = centre;
                Radius = radius;
                Owner = owner;
            }
        }

        /// <summary>Hazards the staff member knows of; each blocks the ground its <see cref="Hazard.Blocks"/> says (only while active and localized).</summary>
        public readonly List<Hazard> Hazards = new List<Hazard>();
        public readonly List<Closure> Closures = new List<Closure>();
        /// <summary>Escalators the staff member knows are closed.</summary>
        public readonly List<Escalator> Escalators = new List<Escalator>();

        private const int Ring = 12, NodeTries = 3, Attempts = 8;
        private const float SampleStep = 1.5f, WalkStride = 1.3f, DirectMeters = PathService.DirectMeters;
        private const byte Open = 1, Shut = 2;
        private enum Built { Done, Start, Edge, End }

        private readonly BakedFloor floor;
        private readonly RouteGraph graph;
        private readonly Vector3[][] hops;
        private readonly byte[] nodeState, edgeState;
        private readonly RouteGraph.Tree[] trees = new RouteGraph.Tree[NodeTries];
        private readonly List<RouteGraph.Node> starts = new List<RouteGraph.Node>(), tried = new List<RouteGraph.Node>();
        private readonly List<RouteGraph.Node>[] ends = new List<RouteGraph.Node>[Ring];
        private readonly Vector3[] ringPoints = new Vector3[Ring];
        private readonly bool[] ringValid = new bool[Ring], badStart = new bool[NodeTries], badEnd = new bool[Ring * NodeTries];
        private readonly List<RouteGraph.Edge> chain = new List<RouteGraph.Edge>();
        private readonly List<Hazard> zones = new List<Hazard>();
        private readonly List<Closure> discs = new List<Closure>();
        private readonly HashSet<Hazard> leavingHazards = new HashSet<Hazard>();
        private readonly HashSet<int> leavingClosures = new HashSet<int>();
        private readonly List<Vector3> leg = new List<Vector3>(), candidate = new List<Vector3>();

        public GuideRoute(StationWorld world)
        {
            floor = world.Baked;
            graph = world.Paths.Graph;
            hops = new Vector3[graph.EdgeCount][];
            nodeState = new byte[graph.Nodes.Count];
            edgeState = new byte[graph.EdgeCount];
            for (int i = 0; i < Ring; i++) ends[i] = new List<RouteGraph.Node>(NodeTries);
        }

        /// <summary>
        /// Finds the way from <paramref name="origin"/> to the ring round <paramref name="target"/>'s scene into <paramref name="route"/> (walkable points, the
        /// first at the staff member's feet). <see cref="Outcome.Arrived"/> when they stand within the ring's reach already — inside the hazard's own danger
        /// too — and <see cref="Outcome.NoRoute"/> when what is known leaves no way (or the staff member is off the floor).
        /// </summary>
        public Outcome Plan(Vector3 origin, Hazard target, List<Vector3> route)
        {
            route.Clear();
            var scene = target.Scene;
            float radius = target.ApproachRadius;
            var apart = origin - scene;
            if (Mathf.Abs(apart.y) < 2.5f && apart.x * apart.x + apart.z * apart.z <= (radius + 2f) * (radius + 2f)) return Outcome.Arrived;
            if (!floor.Sample(origin, 2.5f, out var start) || Mathf.Abs(start.y - origin.y) > 2.5f) return Outcome.NoRoute;
            Begin(target);
            if (!Approaches(scene, radius)) return Outcome.NoRoute;
            if (ByFloor(start, route) || ByGraph(start, route)) return route.Count >= 2 ? Outcome.Route : Outcome.NoRoute;
            route.Clear();
            return Outcome.NoRoute;
        }

        // ── 아는 것 ─────────────────────────────────────────────────────────

        /// <summary>
        /// All known active ground blocks this plan. A route may leave a zone it starts inside, but after the first exit it may never enter it again.
        /// Arrival at the target is decided before planning; its own cordon is the tape they walk up to.
        /// </summary>
        private void Begin(Hazard target)
        {
            zones.Clear();
            discs.Clear();
            foreach (var hazard in Hazards)
                if (hazard != null && hazard.Active && hazard.Localized) zones.Add(hazard);
            foreach (var closure in Closures)
                if (closure.Owner != target) discs.Add(closure);
            Array.Clear(nodeState, 0, nodeState.Length);
            Array.Clear(edgeState, 0, edgeState.Length);
            Array.Clear(badStart, 0, badStart.Length);
            Array.Clear(badEnd, 0, badEnd.Length);
        }

        private static bool Covers(Closure closure, Vector3 position)
        {
            var d = position - closure.Centre;
            if (Mathf.Abs(d.y) > 3f) return false;
            return d.x * d.x + d.z * d.z < closure.Radius * closure.Radius;
        }

        private bool Blocked(Vector3 position)
        {
            for (int i = 0; i < zones.Count; i++) if (zones[i].Blocks(position)) return true;
            for (int i = 0; i < discs.Count; i++) if (Covers(discs[i], position)) return true;
            return false;
        }

        private bool NodeBlocked(RouteGraph.Node node)
        {
            byte state = nodeState[node.Index];
            if (state == 0) nodeState[node.Index] = state = Blocked(node.Position) ? Shut : Open;
            return state == Shut;
        }

        /// <summary>A link is shut when a known escalator closure, or known ground at either end or the middle of it, is in the way. The middle is a coarse look: a walk that still crosses known ground is found and shut by <see cref="Build"/>.</summary>
        float RouteGraph.IRules.Extra(RouteGraph.Edge edge)
        {
            byte state = edgeState[edge.Index];
            if (state == 0)
            {
                bool shut = edge.Escalator != null && Escalators.Contains(edge.Escalator)
                    || NodeBlocked(edge.From) || NodeBlocked(edge.To) || Blocked((edge.From.Position + edge.To.Position) * .5f);
                edgeState[edge.Index] = state = shut ? Shut : Open;
            }
            return state == Shut ? float.PositiveInfinity : 0f;
        }

        /// <summary>Checks this leg against known ground, permitting only the initial escape from a zone the route starts inside.</summary>
        private bool Passable(List<Vector3> points, int from)
        {
            if (from == 0)
            {
                leavingHazards.Clear();
                leavingClosures.Clear();
                for (int i = 0; i < zones.Count; i++) if (zones[i].Blocks(points[0])) leavingHazards.Add(zones[i]);
                for (int i = 0; i < discs.Count; i++) if (Covers(discs[i], points[0])) leavingClosures.Add(i);
            }
            for (int i = Mathf.Max(from, 1); i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / SampleStep));
                for (int step = 0; step <= steps; step++)
                    if (!CanPass(Vector3.Lerp(a, b, (float)step / steps))) return false;
            }
            return true;
        }

        private bool CanPass(Vector3 position)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                var hazard = zones[i];
                if (!hazard.Blocks(position)) leavingHazards.Remove(hazard);
                else if (!leavingHazards.Contains(hazard)) return false;
            }
            for (int i = 0; i < discs.Count; i++)
            {
                if (!Covers(discs[i], position)) leavingClosures.Remove(i);
                else if (!leavingClosures.Contains(i)) return false;
            }
            return true;
        }

        // ── 도착 지점 ───────────────────────────────────────────────────────

        /// <summary>The walkable points on the ring round the scene that known ground does not block, each with the graph nodes nearest it.</summary>
        private bool Approaches(Vector3 scene, float radius)
        {
            bool any = false;
            for (int i = 0; i < Ring; i++)
            {
                ends[i].Clear();
                ringValid[i] = false;
                float angle = i * Mathf.PI * 2f / Ring;
                var spot = scene + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (!floor.Sample(spot, 2.5f, out var point) || Mathf.Abs(point.y - scene.y) > 2.5f || Blocked(point)) continue;
                ringPoints[i] = point;
                ringValid[i] = true;
                Near(point, ends[i]);
                any = true;
            }
            return any;
        }

        /// <summary>The few graph nodes nearest <paramref name="at"/>, nearest first, that known ground does not block.</summary>
        private void Near(Vector3 at, List<RouteGraph.Node> into)
        {
            into.Clear();
            tried.Clear();
            for (int i = 0; i < NodeTries * 2 && into.Count < NodeTries; i++)
            {
                var node = graph.Nearest(at, tried);
                if (node == null) break;
                tried.Add(node);
                if (!NodeBlocked(node)) into.Add(node);
            }
        }

        // ── 바닥으로 바로 ──────────────────────────────────────────────────

        /// <summary>A ring point near enough on the same storey is walked to directly; the shortest walk that known ground leaves free.</summary>
        private bool ByFloor(Vector3 start, List<Vector3> route)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < Ring; i++)
            {
                if (!ringValid[i]) continue;
                var d = ringPoints[i] - start;
                if (Mathf.Abs(d.y) > 2.5f || d.x * d.x + d.z * d.z > DirectMeters * DirectMeters) continue;
                candidate.Clear();
                candidate.Add(start);
                if (!floor.Walk(start, ringPoints[i], candidate) || !Passable(candidate, 0)) continue;
                float length = Length(candidate);
                if (length >= best) continue;
                best = length;
                route.Clear();
                route.AddRange(candidate);
            }
            return route.Count >= 2;
        }

        private static float Length(List<Vector3> points)
        {
            float length = 0;
            for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        // ── 그래프로 ───────────────────────────────────────────────────────

        private bool ByGraph(Vector3 start, List<Vector3> route)
        {
            Near(start, starts);
            if (starts.Count == 0) return false;
            bool fresh = false;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                if (!fresh)
                {
                    for (int i = 0; i < starts.Count; i++)
                        if (!badStart[i]) trees[i] = graph.Survey(starts[i], RouteProfile.Evacuation, this, trees[i]);
                    fresh = true;
                }
                float best = float.PositiveInfinity;
                int bestStart = -1, bestRing = -1, bestEnd = -1;
                for (int i = 0; i < starts.Count; i++)
                {
                    if (badStart[i]) continue;
                    float lead = Vector3.Distance(start, starts[i].Position) * WalkStride;
                    for (int r = 0; r < Ring; r++)
                    {
                        if (!ringValid[r]) continue;
                        for (int k = 0; k < ends[r].Count; k++)
                        {
                            if (badEnd[r * NodeTries + k] || !trees[i].Reaches(ends[r][k])) continue;
                            float cost = lead + trees[i].CostOf(ends[r][k]) + Vector3.Distance(ends[r][k].Position, ringPoints[r]) * WalkStride;
                            if (cost >= best) continue;
                            best = cost;
                            bestStart = i;
                            bestRing = r;
                            bestEnd = k;
                        }
                    }
                }
                if (bestStart < 0) return false;
                var built = Build(start, bestStart, bestRing, bestEnd, route);
                if (built == Built.Done) return true;
                if (built == Built.Edge) fresh = false;
            }
            return false;
        }

        /// <summary>
        /// Lays the walk out over the floor: from the staff member to the first node, along the links, from the last node to the ring point. A part that known
        /// ground still crosses (or that the floor cannot walk) is ruled out for the next try: that start node, that edge, that end.
        /// </summary>
        private Built Build(Vector3 start, int startIndex, int ringIndex, int endIndex, List<Vector3> route)
        {
            var spot = ringPoints[ringIndex];
            trees[startIndex].EdgesTo(ends[ringIndex][endIndex], chain);
            Trim(start, spot);
            var first = chain.Count > 0 ? chain[0].From : starts[startIndex];
            var last = chain.Count > 0 ? chain[chain.Count - 1].To : starts[startIndex];
            route.Clear();
            route.Add(start);
            if (!floor.Walk(start, first.Position, route) || !Passable(route, 0)) { badStart[startIndex] = true; return Built.Start; }
            foreach (var edge in chain)
            {
                var hop = Hop(edge);
                int before = route.Count;
                if (hop.Length == 0 || !Append(route, hop) || !Passable(route, before)) { edgeState[edge.Index] = Shut; return Built.Edge; }
            }
            int joined = route.Count;
            if (!floor.Walk(last.Position, spot, route) || !Passable(route, joined)) { badEnd[ringIndex * NodeTries + endIndex] = true; return Built.End; }
            return Built.Done;
        }

        /// <summary>Drops walking links at either end that would lead the staff member back the way they came (they already stand past the first node, or the ring point lies before the last).</summary>
        private void Trim(Vector3 start, Vector3 spot)
        {
            while (chain.Count >= 2 && Walks(chain[0]) && Vector3.Distance(start, chain[0].To.Position) <= Span(chain[0])) chain.RemoveAt(0);
            while (chain.Count >= 2 && Walks(chain[chain.Count - 1]) && Vector3.Distance(spot, chain[chain.Count - 1].From.Position) <= Span(chain[chain.Count - 1])) chain.RemoveAt(chain.Count - 1);
        }

        private static bool Walks(RouteGraph.Edge edge) => edge.Escalator == null && !edge.Elevator;

        private static float Span(RouteGraph.Edge edge) => Vector3.Distance(edge.From.Position, edge.To.Position);

        private static bool Append(List<Vector3> route, Vector3[] points)
        {
            if (points.Length == 0) return false;
            int from = route.Count > 0 && (route[route.Count - 1] - points[0]).sqrMagnitude < .01f ? 1 : 0;
            for (int i = from; i < points.Length; i++) route.Add(points[i]);
            return true;
        }

        /// <summary>The floor corners of a link in its direction, worked out once (the baked floor never changes). Empty when the floor cannot walk it.</summary>
        private Vector3[] Hop(RouteGraph.Edge edge)
        {
            var known = hops[edge.Index];
            if (known != null) return known;
            leg.Clear();
            bool walked;
            if (edge.Escalator != null) walked = Ride(edge);
            else walked = !edge.Elevator && floor.Walk(edge.From.Position, edge.To.Position, leg);
            return hops[edge.Index] = walked && leg.Count >= 2 ? leg.ToArray() : Array.Empty<Vector3>();
        }

        /// <summary>A ride: on foot to the boarding point, along the belt, on foot from the landing.</summary>
        private bool Ride(RouteGraph.Edge edge)
        {
            var belt = edge.Escalator.Entry.path;
            leg.Add(edge.From.Position);
            if (!Go(belt[0])) return false;
            for (int i = 1; i < belt.Length; i++) leg.Add(belt[i]);
            return Go(edge.To.Position);
        }

        /// <summary>Extends <see cref="leg"/> to <paramref name="to"/>: on foot over the floor unless it is within a step already.</summary>
        private bool Go(Vector3 to)
        {
            var at = leg[leg.Count - 1];
            if ((at - to).sqrMagnitude >= .64f && (!floor.Sample(to, 2f, out var ground) || !floor.Walk(at, ground, leg))) return false;
            leg.Add(to);
            return true;
        }
    }
}
