using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Facilities;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Where wall-standing equipment (distribution boards, vending machines, kiosks, bins) can really stand in the twin: places
    /// on the walkable floor along a wall or column, found by raycasts against the twin's own colliders. A spot is a point of the
    /// walkable navmesh's boundary (the navmesh is baked with an agent radius, so its edge runs about 0.3 m from the wall) with the
    /// wall behind it, how far that wall stays flat to each side, and how far the floor in front stays free. Doors, glass, the KTX
    /// set and escalator housings are not walls for this purpose. Everything is measured, nothing is guessed: the placement rules
    /// (the builders) only choose among these spots.
    /// </summary>
    internal static class WallSpots
    {
        /// <summary>One place along a wall. <see cref="Wall"/> is the wall's surface at floor height, <see cref="Normal"/> points into the room.</summary>
        public sealed class Spot
        {
            public Vector3 Wall, Normal, Tangent;
            public string Zone;
            /// <summary>The wall stays flat this far along +Tangent / −Tangent from the spot (m, capped at <see cref="Reach"/>).</summary>
            public float FlatPlus, FlatMinus;
            /// <summary>Free floor in front along the normal (m, capped at <see cref="FreeReach"/>): the least of a few rays at body heights.</summary>
            public float Free;
            /// <summary>Name of the collider the wall belongs to (diagnostics and the column/wall distinction).</summary>
            public string Collider;
            /// <summary>The wall is a free-standing column or pier: it ends within a metre to a side.</summary>
            public bool Column => Mathf.Min(FlatPlus, FlatMinus) < 1f && Mathf.Max(FlatPlus, FlatMinus) < 1.6f;
        }

        public const float Reach = 3f, FreeReach = 4f;
        private const float Step = .5f;

        /// <summary>Parts of the twin that are not a wall to put equipment against (the train set, doors, glass, escalator housings).</summary>
        private static bool NotAWall(Collider collider, Transform train, Transform escalators)
        {
            if (collider.GetComponentInParent<StationDoor>() != null) return true;
            var t = collider.transform;
            if (train != null && t.IsChildOf(train)) return true;
            if (escalators != null && t.IsChildOf(escalators)) return true;
            var name = collider.name.ToLowerInvariant();
            if (name.Contains("glass") || name.Contains("window") || name.Contains("유리") || name.Contains("창") || name.Contains("curtain")) return true;
            var renderer = collider.GetComponent<Renderer>();
            if (renderer != null)
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    var m = material.name.ToLowerInvariant();
                    if (m.Contains("glass") || m.Contains("유리") || m.Contains("window")) return true;
                    if (material.HasProperty("_BaseColor") && material.GetColor("_BaseColor").a < .5f) return true;
                }
            return false;
        }

        /// <summary>What a survey of the walkable floor found: the wall spots and the walkable floor area (m²) of every zone.</summary>
        public sealed class Survey
        {
            public List<Spot> Spots = new List<Spot>();
            public Dictionary<string, float> Area = new Dictionary<string, float>();
        }

        /// <summary>
        /// All wall spots of the walkable ground-level floors (navmesh area 0), one every half metre along the boundary, and the
        /// walkable area of each zone. The world navmesh must be loaded (NavMesh.AddNavMeshData) by the caller.
        /// </summary>
        public static Survey Find(StationPoints points)
        {
            var train = GameObject.Find(StationSurvey.KtxPath)?.transform;
            var escalators = GameObject.Find("FPSWorld/맞이방 · 에스컬레이터")?.transform;
            var triangulation = NavMesh.CalculateTriangulation();
            var welded = new Dictionary<Vector3Int, int>();
            var vertices = new List<Vector3>();
            var remap = new int[triangulation.vertices.Length];
            for (int i = 0; i < remap.Length; i++)
            {
                var v = triangulation.vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * 50), Mathf.RoundToInt(v.y * 20), Mathf.RoundToInt(v.z * 50));
                if (!welded.TryGetValue(key, out int id)) { id = vertices.Count; welded[key] = id; vertices.Add(v); }
                remap[i] = id;
            }
            // 변이 한 삼각형에만 속하면 가장자리(벽·기둥·난간 앞)다.
            var edges = new Dictionary<long, (int a, int b, int third, int count)>();
            void Edge(int a, int b, int third)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                edges[key] = edges.TryGetValue(key, out var e) ? (e.a, e.b, e.third, e.count + 1) : (a, b, third, 1);
            }
            var survey = new Survey();
            for (int t = 0; t < triangulation.indices.Length; t += 3)
            {
                if (triangulation.areas[t / 3] != 0) continue;
                int a = remap[triangulation.indices[t]], b = remap[triangulation.indices[t + 1]], c = remap[triangulation.indices[t + 2]];
                if (a == b || b == c || a == c) continue;
                Edge(a, b, c); Edge(b, c, a); Edge(c, a, b);
                var zone = points.ZoneAt((vertices[a] + vertices[b] + vertices[c]) / 3f);
                if (zone == null) continue;
                float area = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).magnitude * .5f;
                survey.Area[zone.id] = (survey.Area.TryGetValue(zone.id, out float sum) ? sum : 0) + area;
            }
            var spots = survey.Spots;
            foreach (var e in edges.Values)
            {
                if (e.count != 1) continue;
                var a = vertices[e.a]; var b = vertices[e.b];
                var flat = new Vector3(b.x - a.x, 0, b.z - a.z);
                float length = flat.magnitude;
                if (length < .3f) continue;
                var along = flat / length;
                var inward = new Vector3(-along.z, 0, along.x);
                var third = vertices[e.third];
                if (Vector3.Dot(third - a, inward) < 0) inward = -inward;
                for (float s = Mathf.Min(.15f, length * .5f); s <= length - .1f + 1e-3f; s += Step)
                {
                    var p = a + (b - a) * (s / length);
                    var spot = Measure(p, inward, points, train, escalators);
                    if (spot != null) spots.Add(spot);
                }
            }
            return survey;
        }

        private static Spot Measure(Vector3 floor, Vector3 inward, StationPoints points, Transform train, Transform escalators)
        {
            var zone = points.ZoneAt(floor);
            if (zone == null) return null;
            if (!Physics.Raycast(floor + Vector3.up * 1f, -inward, out var hit, 1f, ~0, QueryTriggerInteraction.Ignore)) return null;
            if (hit.distance < .1f || Mathf.Abs(hit.normal.y) > .2f) return null;
            var normal = new Vector3(hit.normal.x, 0, hit.normal.z).normalized;
            if (Vector3.Dot(normal, inward) < .9f || NotAWall(hit.collider, train, escalators)) return null;
            var wall = new Vector3(hit.point.x, floor.y, hit.point.z);
            var tangent = Vector3.Cross(Vector3.up, normal);
            var spot = new Spot { Wall = wall, Normal = normal, Tangent = tangent, Zone = zone.id, Collider = hit.collider.name };
            spot.FlatPlus = Flat(wall, normal, tangent, hit.collider, train, escalators);
            spot.FlatMinus = Flat(wall, normal, -tangent, hit.collider, train, escalators);
            if (spot.FlatPlus + spot.FlatMinus < .4f) return null;
            spot.Free = FreeAhead(wall, normal, tangent);
            return spot;
        }

        /// <summary>How far the same wall plane continues along <paramref name="direction"/> at 1.0 m and 1.8 m height (a door, a jamb or a shopfront ends it).</summary>
        private static float Flat(Vector3 wall, Vector3 normal, Vector3 direction, Collider collider, Transform train, Transform escalators)
        {
            float reached = 0;
            for (float s = .1f; s <= Reach + 1e-3f; s += .1f)
            {
                foreach (float h in new[] { 1.0f, 1.8f })
                {
                    var origin = wall + normal * .4f + direction * s + Vector3.up * h;
                    if (!Physics.Raycast(origin, -normal, out var hit, .6f, ~0, QueryTriggerInteraction.Ignore)) return reached;
                    if (Mathf.Abs(hit.distance - .4f) > .06f || Vector3.Dot(hit.normal, normal) < .97f || NotAWall(hit.collider, train, escalators)) return reached;
                }
                reached = s;
            }
            return reached;
        }

        private static float FreeAhead(Vector3 wall, Vector3 normal, Vector3 tangent)
        {
            float free = FreeReach;
            foreach (float h in new[] { .5f, 1.2f, 1.8f })
                foreach (float side in new[] { -.4f, 0, .4f })
                {
                    var origin = wall + normal * .05f + tangent * side + Vector3.up * h;
                    if (Physics.Raycast(origin, normal, out var hit, FreeReach, ~0, QueryTriggerInteraction.Ignore)) free = Mathf.Min(free, hit.distance);
                }
            return free;
        }
    }
}
