using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>How a person weighs the links of a route: cost factors for riding an escalator or an elevator (0 forbids the elevator).</summary>
    public readonly struct RouteProfile : IEquatable<RouteProfile>
    {
        public readonly float Escalator, Elevator;

        public RouteProfile(float escalator, float elevator)
        {
            Escalator = escalator;
            Elevator = elevator;
        }

        public static readonly RouteProfile Default = new RouteProfile(1, 1);
        /// <summary>Leaving a burning station: elevators are not used (and stop being an option), escalators are walked like stairs.</summary>
        public static readonly RouteProfile Evacuation = new RouteProfile(1, 0);

        public bool Equals(RouteProfile other) => Escalator == other.Escalator && Elevator == other.Elevator;
        public override bool Equals(object obj) => obj is RouteProfile other && Equals(other);
        public override int GetHashCode() => Escalator.GetHashCode() * 31 + Elevator.GetHashCode();
    }

    /// <summary>
    /// A coarse waypoint graph over the baked navmesh (<c>StationRouteBuilder</c> writes <c>StationRoutes.json</c> beside the navmesh):
    /// about every 16 m of walkable floor per storey one node, plus a node at every exit and at both ends of every escalator and
    /// elevator, and an edge wherever the navmesh walks from one node to a nearby one. A route to anywhere in the station is then
    /// a chain of nodes found in microseconds, and each hop is a short navmesh path an agent can be handed at once. The one long
    /// path query this replaces cost 0.3–1.8 ms and, past about 240 m, came back cut short (the query's node budget ran out).
    /// Routes to the exits are shared by the whole station (one search per exit), routes to other places by everyone who starts from
    /// the same node. Danger (fires, cordons) adds a large cost to the edges that pass through it rather than removing them, so a
    /// person inside the danger zone still has a way out.
    /// A planner that may only use what it knows (<see cref="Survey"/>, for the staff member's guidance) reads none of this state: only the baked links and its own rules.
    /// </summary>
    public sealed class RouteGraph
    {
        /// <summary>Cost (m) an edge that touches a danger zone is charged, per zone.</summary>
        public const float DangerCost = 400f;
        /// <summary>How long a search result stays valid (game seconds): closed links, blocked edges and hazards move on.</summary>
        public const float ResultSeconds = 10f;
        public const int Version = 1;

        /// <summary>The baked file: node positions as flat x,y,z centimetre triples and edges as parallel arrays, so it stays small and diffs by node.</summary>
        [Serializable]
        public sealed class File
        {
            public int version;
            public string report;
            public int[] positions;
            public string[] labels;
            public int[] from, to;
            /// <summary>Per edge: the walk's length in centimetres.</summary>
            public int[] length;
            /// <summary>Per edge: "" for a walk, the escalator entry id for a ride, "elevator" for a lift.</summary>
            public string[] link;
        }

        public const string ElevatorLink = "elevator";

        public sealed class Node
        {
            public int Index;
            public Vector3 Position;
            /// <summary>What the node stands for (an exit id, an escalator end, an elevator stop), or empty for plain floor.</summary>
            public string Label;
            internal readonly List<Edge> Out = new List<Edge>(), In = new List<Edge>();
        }

        public sealed class Edge
        {
            public int Index;
            public Node From, To;
            public float Length;
            public Escalator Escalator;
            public bool Elevator;
            /// <summary>A ride whose escalator this world does not have: the edge cannot be walked.</summary>
            internal bool Missing;
            internal float BlockedUntil;
        }

        /// <summary>Costs from one node to every other, and the edge that leads on from each: a search from a start (<see cref="Tree"/>) or towards a goal (<see cref="Field"/>).</summary>
        public abstract class Costs
        {
            public Node Origin { get; internal set; }
            internal float[] Cost;
            internal Edge[] Via;
            internal float BuiltAt;
            internal int Version;

            public float CostOf(Node node) => Cost[node.Index];
            public bool Reaches(Node node) => !float.IsPositiveInfinity(Cost[node.Index]);
        }

        /// <summary>The cheapest way from <see cref="Costs.Origin"/> to every node.</summary>
        public sealed class Tree : Costs
        {
            /// <summary>Fills <paramref name="chain"/> with the nodes from the start to <paramref name="goal"/>, both included.</summary>
            public void PathTo(Node goal, List<Node> chain)
            {
                chain.Clear();
                for (var node = goal; node != null && node != Origin; node = Via[node.Index]?.From) chain.Add(node);
                chain.Add(Origin);
                chain.Reverse();
            }

            /// <summary>Fills <paramref name="edges"/> with the links from the start to <paramref name="goal"/> in walking order (empty when the goal is the start or cannot be reached).</summary>
            public void EdgesTo(Node goal, List<Edge> edges)
            {
                edges.Clear();
                for (var node = goal; node != null && node != Origin;)
                {
                    var via = Via[node.Index];
                    if (via == null) { edges.Clear(); return; }
                    edges.Add(via);
                    node = via.From;
                }
                edges.Reverse();
            }
        }

        /// <summary>The cheapest way from every node to <see cref="Costs.Origin"/> (a goal everyone heads for: an exit).</summary>
        public sealed class Field : Costs
        {
            /// <summary>Fills <paramref name="chain"/> with the nodes from <paramref name="start"/> to the goal, both included.</summary>
            public void PathFrom(Node start, List<Node> chain)
            {
                chain.Clear();
                for (var node = start; node != null; node = node == Origin ? null : Via[node.Index]?.To) chain.Add(node);
            }
        }

        private readonly List<Node> nodes = new List<Node>();
        private readonly List<Edge> edges = new List<Edge>();
        private readonly Dictionary<string, Node> byLabel = new Dictionary<string, Node>();

        public IReadOnlyList<Node> Nodes => nodes;
        public int EdgeCount => edges.Count;
        /// <summary>The bake's own account of what cannot reach what (twin defects, written when the file was made).</summary>
        public string Report { get; private set; }

        public static RouteGraph Load(TextAsset asset, IReadOnlyList<Escalator> escalators)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var file = JsonUtility.FromJson<File>(asset.text);
            if (file == null || file.version != Version || file.positions == null || file.labels == null || file.from == null || file.to == null || file.length == null || file.link == null
                || file.positions.Length != file.labels.Length * 3 || file.from.Length != file.to.Length || file.from.Length != file.length.Length || file.from.Length != file.link.Length)
                throw new InvalidOperationException("StationRoutes.json 구조 오류 (version " + Version + " 필요) — ChooGuard/Emergency/Bake route graph 로 다시 만드세요.");
            var graph = new RouteGraph { Report = file.report };
            var rides = new Dictionary<string, Escalator>();
            foreach (var escalator in escalators) rides[escalator.Entry.id] = escalator;
            for (int i = 0; i < file.labels.Length; i++)
            {
                var node = new Node { Index = i, Position = new Vector3(file.positions[i * 3], file.positions[i * 3 + 1], file.positions[i * 3 + 2]) * .01f, Label = file.labels[i] };
                graph.nodes.Add(node);
                if (node.Label.Length > 0) graph.byLabel[node.Label] = node;
            }
            for (int i = 0; i < file.from.Length; i++)
            {
                var edge = new Edge { Index = i, From = graph.nodes[file.from[i]], To = graph.nodes[file.to[i]], Length = file.length[i] * .01f };
                string link = file.link[i];
                if (link == ElevatorLink) edge.Elevator = true;
                else if (link.Length > 0 && !rides.TryGetValue(link, out edge.Escalator)) edge.Missing = true;
                graph.edges.Add(edge);
                edge.From.Out.Add(edge);
                edge.To.In.Add(edge);
            }
            return graph;
        }

        // ── 찾기 ────────────────────────────────────────────────────────────

        /// <summary>
        /// The node nearest <paramref name="position"/> on its own storey (or the nearest of all when its storey has none), leaving out
        /// <paramref name="skip"/> (ones already tried, or that an earlier try showed to lead nowhere from here).
        /// </summary>
        public Node Nearest(Vector3 position, IReadOnlyList<Node> skip = null)
        {
            Node best = null, bestAnyStorey = null;
            float bestDistance = float.PositiveInfinity, bestAny = float.PositiveInfinity;
            foreach (var node in nodes)
            {
                if (skip != null && Contains(skip, node)) continue;
                var d = node.Position - position;
                float squared = d.x * d.x + d.z * d.z + 4f * d.y * d.y;
                if (Mathf.Abs(d.y) < 2.5f && squared < bestDistance) { bestDistance = squared; best = node; }
                if (squared < bestAny) { bestAny = squared; bestAnyStorey = node; }
            }
            return best ?? bestAnyStorey;
        }

        private static bool Contains(IReadOnlyList<Node> list, Node node)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == node) return true;
            return false;
        }

        public Node ByLabel(string label) => label != null && byLabel.TryGetValue(label, out var node) ? node : null;

        /// <summary>Marks an edge unusable for a while (its walk failed: a door locked, the way burned, a link closed) so routes go around it.</summary>
        public void Block(Edge edge, float until)
        {
            if (edge == null) return;
            edge.BlockedUntil = until;
            version++;
        }

        public Edge EdgeBetween(Node from, Node to)
        {
            foreach (var edge in from.Out) if (edge.To == to) return edge;
            return null;
        }

        // ── 검색 결과 ───────────────────────────────────────────────────────

        private int version;
        private readonly Dictionary<(int origin, int danger, RouteProfile profile, bool toward), Costs> results = new Dictionary<(int, int, RouteProfile, bool), Costs>();
        private readonly Stack<Costs> spareTrees = new Stack<Costs>(), spareFields = new Stack<Costs>();
        private float[] penalty;
        private readonly Dictionary<int, float[]> penalties = new Dictionary<int, float[]>();
        private int[] heapNode = Array.Empty<int>();
        private float[] heapCost = Array.Empty<float>();
        private int heapSize;

        /// <summary>Searches done and how many were answered again from a search still valid (shared work).</summary>
        public int Searches { get; private set; }
        public int SearchHits { get; private set; }

        /// <summary>What a caller that plans from its own knowledge adds to the baked links (<see cref="Survey"/>): the extra cost (m) of an edge, +∞ for a link it counts as shut.</summary>
        public interface IRules
        {
            float Extra(Edge edge);
        }

        private IRules surveyRules;

        /// <summary>
        /// The cheapest routes from <paramref name="start"/> to every node over the baked links alone, as a planner sees the station that is told
        /// only what it knows: nothing that happens in the session is read (an edge blocked after a failed walk, an escalator stopped or closed, a fire,
        /// a locked door) — <paramref name="rules"/> alone adds cost or shuts a link. Nothing is shared or remembered between calls; the answer goes into
        /// <paramref name="into"/> (made when null) and belongs to the caller, who may reuse it for the next call.
        /// </summary>
        public Tree Survey(Node start, RouteProfile profile, IRules rules, Tree into = null)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var tree = into ?? new Tree { Cost = new float[nodes.Count], Via = new Edge[nodes.Count] };
            tree.Origin = start;
            surveyRules = rules;
            try { Search(tree, profile, 0, false); }
            finally { surveyRules = null; }
            return tree;
        }

        /// <summary>The cheapest routes from <paramref name="start"/> to every node under <paramref name="profile"/>, avoiding <paramref name="dangers"/>; shared by everyone starting from that node for <see cref="ResultSeconds"/>.</summary>
        public Tree Explore(Node start, RouteProfile profile, IReadOnlyList<(Vector3 centre, float radius)> dangers, float now) =>
            (Tree)Result(start, profile, dangers, now, false);

        /// <summary>The cheapest routes from every node to <paramref name="goal"/> (an exit) under <paramref name="profile"/>, avoiding <paramref name="dangers"/>; one search serves the whole station.</summary>
        public Field Toward(Node goal, RouteProfile profile, IReadOnlyList<(Vector3 centre, float radius)> dangers, float now) =>
            (Field)Result(goal, profile, dangers, now, true);

        private Costs Result(Node origin, RouteProfile profile, IReadOnlyList<(Vector3 centre, float radius)> dangers, float now, bool toward)
        {
            int danger = DangerKey(dangers);
            var key = (origin.Index, danger, profile, toward);
            var spare = toward ? spareFields : spareTrees;
            if (results.TryGetValue(key, out var result))
            {
                if (result.Version == version && now - result.BuiltAt < ResultSeconds) { SearchHits++; return result; }
                (result is Field ? spareFields : spareTrees).Push(result);
                results.Remove(key);
            }
            if (results.Count >= 128)
            {
                foreach (var old in results.Values) (old is Field ? spareFields : spareTrees).Push(old);
                results.Clear();
            }
            result = spare.Count > 0 ? spare.Pop() : toward ? new Field { Cost = new float[nodes.Count], Via = new Edge[nodes.Count] } : (Costs)new Tree { Cost = new float[nodes.Count], Via = new Edge[nodes.Count] };
            result.Origin = origin;
            result.BuiltAt = now;
            result.Version = version;
            penalty = PenaltiesFor(dangers, danger);
            Search(result, profile, now, toward);
            results[key] = result;
            Searches++;
            return result;
        }

        /// <summary>A key for the set of danger zones (compared at 3 m and 2 m steps).</summary>
        private static int DangerKey(IReadOnlyList<(Vector3 centre, float radius)> dangers)
        {
            int key = dangers.Count;
            foreach (var (centre, radius) in dangers)
                key = key * 31 + (Mathf.RoundToInt(centre.x / 3f) * 7919 + Mathf.RoundToInt(centre.z / 3f)) * 31 + Mathf.RoundToInt(centre.y / 3f) * 13 + Mathf.RoundToInt(radius / 2f);
            return key;
        }

        /// <summary>The per-edge danger cost for one set of zones, kept while that set stays in use.</summary>
        private float[] PenaltiesFor(IReadOnlyList<(Vector3 centre, float radius)> dangers, int key)
        {
            if (penalties.TryGetValue(key, out var known)) return known;
            if (penalties.Count >= 16) penalties.Clear();
            var array = new float[edges.Count];
            foreach (var (centre, radius) in dangers)
                foreach (var edge in edges)
                    if (Touches(edge, centre, radius)) array[edge.Index] += DangerCost;
            penalties[key] = array;
            return array;
        }

        private static bool Touches(Edge edge, Vector3 centre, float radius) =>
            (Mathf.Abs(edge.From.Position.y - centre.y) < 3f || Mathf.Abs(edge.To.Position.y - centre.y) < 3f)
            && StationWorld.SegmentDistance(centre, edge.From.Position, edge.To.Position) < radius;

        private void Search(Costs result, RouteProfile profile, float now, bool toward)
        {
            Array.Fill(result.Cost, float.PositiveInfinity);
            Array.Clear(result.Via, 0, result.Via.Length);
            if (heapNode.Length < edges.Count + nodes.Count + 1)
            {
                heapNode = new int[edges.Count + nodes.Count + 1];
                heapCost = new float[heapNode.Length];
            }
            heapSize = 0;
            result.Cost[result.Origin.Index] = 0;
            Push(result.Origin.Index, 0);
            while (heapSize > 0)
            {
                int index = Pop(out float cost);
                if (cost > result.Cost[index]) continue;
                foreach (var edge in toward ? nodes[index].In : nodes[index].Out)
                {
                    float weight = Weight(edge, profile, now);
                    if (float.IsPositiveInfinity(weight)) continue;
                    float next = cost + weight;
                    int other = toward ? edge.From.Index : edge.To.Index;
                    if (next >= result.Cost[other]) continue;
                    result.Cost[other] = next;
                    result.Via[other] = edge;
                    Push(other, next);
                }
            }
        }

        private float Weight(Edge edge, RouteProfile profile, float now)
        {
            bool survey = surveyRules != null;
            if (edge.Missing || !survey && edge.BlockedUntil > now) return float.PositiveInfinity;
            float weight = edge.Length;
            if (edge.Escalator != null)
            {
                if (!survey && (edge.Escalator.Closed || edge.Escalator.EntryBarred)) return float.PositiveInfinity;
                weight *= profile.Escalator;
            }
            if (edge.Elevator)
            {
                if (profile.Elevator <= 0) return float.PositiveInfinity;
                weight *= profile.Elevator;
            }
            return survey ? weight + surveyRules.Extra(edge) : weight + penalty[edge.Index];
        }

        private void Push(int node, float cost)
        {
            int i = heapSize++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (heapCost[parent] <= cost) break;
                heapNode[i] = heapNode[parent];
                heapCost[i] = heapCost[parent];
                i = parent;
            }
            heapNode[i] = node;
            heapCost[i] = cost;
        }

        private int Pop(out float cost)
        {
            int top = heapNode[0];
            cost = heapCost[0];
            heapSize--;
            if (heapSize == 0) return top;
            int node = heapNode[heapSize];
            float last = heapCost[heapSize];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= heapSize) break;
                if (child + 1 < heapSize && heapCost[child + 1] < heapCost[child]) child++;
                if (heapCost[child] >= last) break;
                heapNode[i] = heapNode[child];
                heapCost[i] = heapCost[child];
                i = child;
            }
            heapNode[i] = node;
            heapCost[i] = last;
            return top;
        }
    }
}
