using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>
    /// The vertical faces of the twin's meshes (walls, columns, glass, shop fronts), for the ray casts the equipment builders
    /// need where a fitting hangs on a wall: the back wall of a beam detector, a valve station, a control box, a camera. The interior
    /// finishing has no colliders, so the triangles themselves are tested (Möller–Trumbore); a face counts from either side.
    /// </summary>
    internal sealed class StationWalls
    {
        public struct Wall { public Vector3 A, B, C, Normal; public string Owner; }

        /// <summary>A ray's first wall: distance, point and the normal facing the ray's origin.</summary>
        public readonly struct Hit
        {
            public readonly float Distance;
            public readonly Vector3 Point, Normal;
            public readonly string Owner;
            public Hit(float distance, Vector3 point, Vector3 normal, string owner) { Distance = distance; Point = point; Normal = normal; Owner = owner; }
        }

        private const float CellSize = 4f;
        private readonly List<Wall> walls = new List<Wall>();
        private readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

        public int Count => walls.Count;

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        /// <summary>Collects the large vertical faces (over 0.6 m², a normal within 17° of horizontal) of every enabled renderer inside <paramref name="region"/>.</summary>
        public static StationWalls Collect(Scene station, Bounds region)
        {
            var result = new StationWalls();
            foreach (var root in station.GetRootGameObjects())
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(false))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || filter.sharedMesh == null || !renderer.bounds.Intersects(region)) continue;
                    var path = StationCeilings.PathOf(filter.transform);
                    if (path.Contains("KTXSource")) continue;
                    var mesh = filter.sharedMesh;
                    var vertices = mesh.vertices;
                    var matrix = filter.transform.localToWorldMatrix;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var indices = mesh.GetIndices(sub);
                        for (int i = 0; i + 2 < indices.Length; i += 3)
                        {
                            var a = matrix.MultiplyPoint3x4(vertices[indices[i]]);
                            var b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]);
                            var c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                            var cross = Vector3.Cross(b - a, c - a);
                            if (cross.sqrMagnitude < 1e-8f) continue;
                            var normal = cross.normalized;
                            float top = Mathf.Max(a.y, b.y, c.y), bottom = Mathf.Min(a.y, b.y, c.y);
                            if (Mathf.Abs(normal.y) > .3f || top < region.min.y || bottom > region.max.y || cross.magnitude * .5f < .6f) continue;
                            result.Add(new Wall { A = a, B = b, C = c, Normal = normal, Owner = path });
                        }
                    }
                }
            return result;
        }

        private void Add(Wall wall)
        {
            int index = walls.Count;
            walls.Add(wall);
            int x0 = Mathf.FloorToInt(Mathf.Min(wall.A.x, wall.B.x, wall.C.x) / CellSize), x1 = Mathf.FloorToInt(Mathf.Max(wall.A.x, wall.B.x, wall.C.x) / CellSize);
            int z0 = Mathf.FloorToInt(Mathf.Min(wall.A.z, wall.B.z, wall.C.z) / CellSize), z1 = Mathf.FloorToInt(Mathf.Max(wall.A.z, wall.B.z, wall.C.z) / CellSize);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    var key = Key(x, z);
                    if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                    list.Add(index);
                }
        }

        /// <summary>
        /// The first wall along <paramref name="direction"/> (unit) between <paramref name="nearest"/> and <paramref name="farthest"/> metres,
        /// ignoring faces that lie nearly parallel to the ray. Deterministic: ties go to the lower index.
        /// </summary>
        public bool Cast(Vector3 origin, Vector3 direction, float nearest, float farthest, out Hit best)
        {
            best = default;
            float bestDistance = float.MaxValue;
            int bestIndex = int.MaxValue;
            var end = origin + direction * farthest;
            var seen = new HashSet<int>();
            // 격자 칸을 따라 걷는다: 반 칸씩 나아가며 지나는 칸의 삼각형만 시험한다.
            int steps = Mathf.CeilToInt(farthest / (CellSize * .5f));
            for (int s = 0; s <= steps; s++)
            {
                var p = Vector3.Lerp(origin, end, steps == 0 ? 0 : s / (float)steps);
                if (!grid.TryGetValue(Key(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize)), out var list)) continue;
                foreach (int index in list)
                {
                    if (!seen.Add(index)) continue;
                    var w = walls[index];
                    if (Mathf.Abs(Vector3.Dot(w.Normal, direction)) < .3f) continue;
                    var e1 = w.B - w.A;
                    var e2 = w.C - w.A;
                    var pv = Vector3.Cross(direction, e2);
                    float det = Vector3.Dot(e1, pv);
                    if (Mathf.Abs(det) < 1e-8f) continue;
                    float inv = 1f / det;
                    var t = origin - w.A;
                    float u = Vector3.Dot(t, pv) * inv;
                    if (u < 0 || u > 1) continue;
                    var q = Vector3.Cross(t, e1);
                    float v = Vector3.Dot(direction, q) * inv;
                    if (v < 0 || u + v > 1) continue;
                    float distance = Vector3.Dot(e2, q) * inv;
                    if (distance < nearest || distance > farthest) continue;
                    if (distance < bestDistance - 1e-5f || Mathf.Abs(distance - bestDistance) <= 1e-5f && index < bestIndex)
                    {
                        bestDistance = distance;
                        best = new Hit(distance, origin + direction * distance, Vector3.Dot(w.Normal, direction) > 0 ? -w.Normal : w.Normal, w.Owner);
                        bestIndex = index;
                    }
                }
            }
            return bestDistance < float.MaxValue;
        }
    }
}
