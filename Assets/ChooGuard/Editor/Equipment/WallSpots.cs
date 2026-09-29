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
            /// <summary>How many of nine rays fanned across the room in front (within 6 m, chest height) hit something: high in an alcove, a shop interior or a corridor, low in the open concourse.</summary>
            public int Enclosed;
            /// <summary>Distance from a point 0.4 m in front of the wall to the nearest glass pane (shop front, window wall, curtain wall) in the half plane in front of the wall (m, capped at <see cref="GlassReach"/>).</summary>
            public float GlassNearest;
            /// <summary>Name of the collider the wall belongs to (diagnostics and the column/wall distinction).</summary>
            public string Collider;
            /// <summary>The wall is a free-standing column or pier: it ends within a metre to a side.</summary>
            public bool Column => Mathf.Min(FlatPlus, FlatMinus) < 1f && Mathf.Max(FlatPlus, FlatMinus) < 1.6f;
        }

        public const float Reach = 3f, FreeReach = 4f, GlassReach = 2.5f;
        private const float Step = .5f;

        /// <summary>Parts of the twin that are not a wall to put equipment against (the train set, doors, glass, escalator housings).</summary>
        private static bool NotAWall(RaycastHit hit, Transform train, Transform escalators)
        {
            var collider = hit.collider;
            if (collider.GetComponentInParent<StationDoor>() != null) return true;
            var t = collider.transform;
            if (train != null && t.IsChildOf(train)) return true;
            if (escalators != null && t.IsChildOf(escalators)) return true;
            return IsGlass(hit);
        }

        /// <summary>
        /// Glass by the object's name or by the material of the very surface that was hit. The station's main shell is one mesh with
        /// dozens of materials, one of them glazing: only the triangles of that material are glass, the rest of the mesh (pillars, walls)
        /// is solid. A collider without a mesh hit to tell by (a box) counts as glass only when all its materials are.
        /// </summary>
        private static bool IsGlass(RaycastHit hit)
        {
            var collider = hit.collider;
            var name = collider.name.ToLowerInvariant();
            if (name.Contains("glass") || name.Contains("window") || name.Contains("유리") || name.Contains("창") || name.Contains("curtain")) return true;
            var renderer = collider.GetComponent<Renderer>();
            if (renderer == null) return false;
            var materials = renderer.sharedMaterials;
            if (materials.Length == 0) return false;
            if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null && hit.triangleIndex >= 0)
            {
                var mesh = meshCollider.sharedMesh;
                int first = hit.triangleIndex * 3;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    var sub = mesh.GetSubMesh(i);
                    if (first >= sub.indexStart && first < sub.indexStart + sub.indexCount) return IsGlassMaterial(materials[Mathf.Min(i, materials.Length - 1)]);
                }
            }
            return materials.All(IsGlassMaterial);
        }

        private static bool IsGlassMaterial(Material material)
        {
            if (material == null) return false;
            var m = material.name.ToLowerInvariant();
            if (m.Contains("glass") || m.Contains("유리") || m.Contains("window")) return true;
            return material.HasProperty("_BaseColor") && material.GetColor("_BaseColor").a < .5f;
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

        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        /// <summary>
        /// The nearest hit of a ray. Colliders that lie in one plane (a glass pane and the invisible collider behind it, the temporary
        /// colliders next to the twin's own) tie within a centimetre: a glass pane wins, so glass is never mistaken for the wall it
        /// stands over; among equals the hierarchy path that sorts first wins, so two runs pick the same collider.
        /// </summary>
        private static bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == 0) return false;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++) nearest = Mathf.Min(nearest, Hits[i].distance);
            string bestPath = null;
            bool bestGlass = false;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].distance - nearest > .01f) continue;
                bool glass = IsGlass(Hits[i]);
                string path = PathOf(Hits[i].collider.transform);
                if (bestPath != null && (bestGlass && !glass || bestGlass == glass && string.CompareOrdinal(path, bestPath) >= 0)) continue;
                bestPath = path;
                bestGlass = glass;
                best = Hits[i];
            }
            return true;
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            for (var t = transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return path;
        }
        private static Spot Measure(Vector3 floor, Vector3 inward, StationPoints points, Transform train, Transform escalators)
        {
            var zone = points.ZoneAt(floor);
            if (zone == null) return null;
            if (!Cast(floor + Vector3.up * 1f, -inward, 1f, out var hit)) return null;
            if (hit.distance < .1f || Mathf.Abs(hit.normal.y) > .2f) return null;
            var normal = new Vector3(hit.normal.x, 0, hit.normal.z).normalized;
            if (Vector3.Dot(normal, inward) < .9f || NotAWall(hit, train, escalators)) return null;
            var wall = new Vector3(hit.point.x, floor.y, hit.point.z);
            var tangent = Vector3.Cross(Vector3.up, normal);
            var spot = new Spot { Wall = wall, Normal = normal, Tangent = tangent, Zone = zone.id, Collider = hit.collider.name };
            spot.FlatPlus = Flat(wall, normal, tangent, hit.collider, train, escalators);
            spot.FlatMinus = Flat(wall, normal, -tangent, hit.collider, train, escalators);
            if (spot.FlatPlus + spot.FlatMinus < .4f) return null;
            spot.Free = FreeAhead(wall, normal, tangent);
            spot.Enclosed = Enclosure(wall, normal);
            spot.GlassNearest = GlassDistance(wall, normal);
            return spot;
        }

        /// <summary>
        /// How far the nearest glass lies from a point 0.4 m in front of the wall: rays every 15° across the half plane in front (along
        /// the wall to either side included) at 1.1 m and 1.8 m height, capped at <see cref="GlassReach"/>. Glass counts from either
        /// side; a solid front face nearer than the glass hides it, back faces of solid meshes are ignored.
        /// </summary>
        private static float GlassDistance(Vector3 wall, Vector3 normal)
        {
            using var backfaces = TwinColliders.Backfaces();
            float nearest = GlassReach;
            for (int angle = -90; angle <= 90; angle += 15)
            {
                var direction = Quaternion.AngleAxis(angle, Vector3.up) * normal;
                foreach (float h in new[] { 1.1f, 1.8f })
                {
                    int count = Physics.RaycastNonAlloc(wall + normal * .4f + Vector3.up * h, direction, Hits, GlassReach, ~0, QueryTriggerInteraction.Ignore);
                    float solid = GlassReach;
                    for (int i = 0; i < count; i++)
                        if (Vector3.Dot(Hits[i].normal, direction) < 0 && !IsGlass(Hits[i])) solid = Mathf.Min(solid, Hits[i].distance);
                    for (int i = 0; i < count; i++)
                        if (Hits[i].distance <= solid + .01f && IsGlass(Hits[i])) nearest = Mathf.Min(nearest, Hits[i].distance);
                }
            }
            return nearest;
        }

        /// <summary>
        /// A straight look from <paramref name="origin"/> along <paramref name="direction"/> is blocked by any surface of the twin seen
        /// from either side: its walls and panes render two-sided while their colliders are one-sided, so a look from behind would
        /// otherwise pass through them.
        /// </summary>
        public static bool LineBlocked(Vector3 origin, Vector3 direction, float distance)
        {
            using var backfaces = TwinColliders.Backfaces();
            return Physics.Raycast(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
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
                    if (!Cast(origin, -normal, .6f, out var hit)) return reached;
                    if (Mathf.Abs(hit.distance - .4f) > .06f || Vector3.Dot(hit.normal, normal) < .97f || NotAWall(hit, train, escalators)) return reached;
                }
                reached = s;
            }
            return reached;
        }

        private static int Enclosure(Vector3 wall, Vector3 normal)
        {
            int hits = 0;
            var origin = wall + normal * 1f + Vector3.up * 1.3f;
            for (int i = 0; i < 9; i++)
                if (Physics.Raycast(origin, Quaternion.AngleAxis(-90f + i * 22.5f, Vector3.up) * normal, 6f, ~0, QueryTriggerInteraction.Ignore)) hits++;
            return hits;
        }

        /// <summary>Free floor in front along the normal at body heights and across the item's width; surfaces count from either side (a one-sided wall stands in the way like any other).</summary>
        private static float FreeAhead(Vector3 wall, Vector3 normal, Vector3 tangent)
        {
            using var backfaces = TwinColliders.Backfaces();
            float free = FreeReach;
            foreach (float h in new[] { .5f, 1.2f, 1.8f })
                foreach (float side in new[] { -.6f, -.3f, 0, .3f, .6f })
                {
                    var origin = wall + normal * .05f + tangent * side + Vector3.up * h;
                    if (Physics.Raycast(origin, normal, out var hit, FreeReach, ~0, QueryTriggerInteraction.Ignore)) free = Mathf.Min(free, hit.distance);
                }
            return free;
        }
    }
}
