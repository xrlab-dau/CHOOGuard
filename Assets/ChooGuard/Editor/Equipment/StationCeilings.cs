using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>
    /// What is above the floor of the Busan twin, measured from its meshes (no physics: the interior finishing has no
    /// colliders): for every metre cell where a person can stand, the lowest ceiling surface over it (a downward-facing
    /// triangle of a ceiling renderer), plus ceilings over cells nobody can walk (a shop's inside). Deterministic: the same
    /// scene gives the same cells in the same order. Equipment builders place detectors, sprinklers and cameras on them.
    /// </summary>
    internal static class StationCeilings
    {
        /// <summary>Cell edge in metres.</summary>
        public const float Step = 1f;

        /// <summary>Lowest a ceiling can be above the floor to count (a head-height beam or a lintel is no ceiling).</summary>
        public const float MinHeight = 2.2f;

        /// <summary>Highest ceiling looked for above a floor.</summary>
        public const float MaxHeight = 30f;

        // 천장 렌더러: 이름에 아래 낱말이 있는 것(천장 타일·패널·덱 라이너·슬래브 밑면·승강장 상부 덱·출구 지붕). 조명·그림자용 복제본·격자선은 뺀다.
        private static readonly string[] CeilingWords = { "Ceiling", "DeckLiner", "Majibang_3F_L", "Majibang_2F_Floor", "선로상층부", "출구지붕", "Kit_Slab_", "GypsumLit" };
        private static readonly string[] NotCeilingWords = { "ShadowCaster", "CeilingLED", "CeilingGrid", "KTXSource", "OfficialStation_-부산역_선로상층부(주차장)" };

        /// <summary>One metre of floor and the ceiling over it.</summary>
        public sealed class Cell
        {
            public int X, Z;
            public Vector3 Floor;
            public float CeilingY;
            /// <summary>Unit normal of the ceiling surface (pointing down into the room); straight down for a flat ceiling.</summary>
            public Vector3 Normal = Vector3.down;
            public string Owner = "";
            public string Zone = "";
            /// <summary>False when the floor height was inferred from the walkable floor nearby (inside a shop, behind a counter).</summary>
            public bool Walkable;

            public float Height => CeilingY - Floor.y;
            public Vector3 Ceiling => new Vector3(Floor.x, CeilingY, Floor.z);
        }

        /// <summary>Triangles indexed by a two-metre grid on x/z; answers "which surfaces are over this point".</summary>
        private sealed class SurfaceIndex
        {
            private const float CellSize = 2f;
            private struct Triangle { public Vector3 A, B, C, Normal; public int Owner; }
            private readonly List<Triangle> triangles = new List<Triangle>();
            private readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();

            private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

            public void Add(Vector3 a, Vector3 b, Vector3 c, int owner)
            {
                int index = triangles.Count;
                triangles.Add(new Triangle { A = a, B = b, C = c, Normal = Vector3.Cross(b - a, c - a).normalized, Owner = owner });
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / CellSize), x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / CellSize);
                int z0 = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / CellSize), z1 = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / CellSize);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        var key = Key(x, z);
                        if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                        list.Add(index);
                    }
            }

            /// <summary>Heights, owners and normals of every surface over (x, z), lowest first.</summary>
            public void At(float x, float z, List<(float y, int owner, Vector3 normal)> into)
            {
                into.Clear();
                if (!grid.TryGetValue(Key(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize)), out var list)) return;
                foreach (int i in list)
                {
                    var t = triangles[i];
                    float det = (t.B.z - t.C.z) * (t.A.x - t.C.x) + (t.C.x - t.B.x) * (t.A.z - t.C.z);
                    if (Mathf.Abs(det) < 1e-7f) continue;
                    float l1 = ((t.B.z - t.C.z) * (x - t.C.x) + (t.C.x - t.B.x) * (z - t.C.z)) / det;
                    float l2 = ((t.C.z - t.A.z) * (x - t.C.x) + (t.A.x - t.C.x) * (z - t.C.z)) / det;
                    float l3 = 1 - l1 - l2;
                    if (l1 < -1e-5f || l2 < -1e-5f || l3 < -1e-5f) continue;
                    into.Add((l1 * t.A.y + l2 * t.B.y + l3 * t.C.y, t.Owner, t.Normal));
                }
                into.Sort((p, q) => p.y.CompareTo(q.y));
            }
        }

        /// <summary>The survey's cells: floors with a ceiling over them, and walkable floors with no ceiling over them (open sky).</summary>
        public sealed class Result
        {
            public List<Cell> Cells;
            /// <summary>Walkable cells with nothing over them within <see cref="MaxHeight"/>: outdoors, or an opening in the roof.</summary>
            public List<Cell> Open;
            /// <summary>Whether a lamp panel or LED strip of the ceiling lies within <c>radius</c> metres of (x, z) at that ceiling height.</summary>
            public Func<float, float, float, float, bool> LampNear;
        }

        /// <summary>
        /// Surveys the open station scene: walkable floors from the world navmesh (level ground only), ceilings from the
        /// ceiling renderers, lamp panels from the lighting renderers. Cells are ordered by x, then z, then floor height.
        /// </summary>
        public static Result Survey(Scene station, EmergencyArt art, StationPoints points)
        {
            var ceilingOwners = new List<string>();
            var ceilings = new SurfaceIndex();
            var lamps = new SurfaceIndex();
            foreach (var root in station.GetRootGameObjects())
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(false))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                    var path = PathOf(filter.transform);
                    if (IsCeiling(path))
                    {
                        int owner = ceilingOwners.Count;
                        ceilingOwners.Add(path);
                        AddDownFacing(filter, ceilings, owner);
                    }
                    else if (IsLamp(path)) AddDownFacing(filter, lamps, 0);
                }

            var floors = new SurfaceIndex();
            var instance = NavMesh.AddNavMeshData(art.WorldNavMesh);
            try
            {
                var triangulation = NavMesh.CalculateTriangulation();
                for (int i = 0; i < triangulation.indices.Length; i += 3)
                {
                    // 걷는 평지(영역 0)만: 에스컬레이터·엘리베이터·계단 영역은 제외한다.
                    if (triangulation.areas[i / 3] != 0) continue;
                    floors.Add(triangulation.vertices[triangulation.indices[i]], triangulation.vertices[triangulation.indices[i + 1]], triangulation.vertices[triangulation.indices[i + 2]], 0);
                }
            }
            finally { NavMesh.RemoveNavMeshData(instance); }

            var walkable = new Dictionary<(int, int), List<float>>();
            var cells = new List<Cell>();
            var open = new List<Cell>();
            var above = new List<(float y, int owner, Vector3 normal)>();
            var below = new List<(float y, int owner, Vector3 normal)>();
            const int minX = -145, maxX = 175, minZ = -165, maxZ = 175;
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    float cx = (x + .5f) * Step, cz = (z + .5f) * Step;
                    floors.At(cx, cz, below);
                    if (below.Count == 0) continue;
                    ceilings.At(cx, cz, above);
                    var heights = new List<float>();
                    foreach (var floor in below)
                    {
                        if (heights.Exists(h => Mathf.Abs(h - floor.y) < .3f)) continue;
                        heights.Add(floor.y);
                        int found = above.FindIndex(c => c.y >= floor.y + MinHeight && c.y <= floor.y + MaxHeight);
                        if (found < 0) { open.Add(new Cell { X = x, Z = z, Floor = new Vector3(cx, floor.y, cz), CeilingY = float.PositiveInfinity, Walkable = true }); continue; }
                        var hit = above[found];
                        cells.Add(new Cell { X = x, Z = z, Floor = new Vector3(cx, floor.y, cz), CeilingY = hit.y, Normal = hit.normal, Owner = ceilingOwners[hit.owner], Walkable = true });
                    }
                    walkable[(x, z)] = heights;
                }

            // 걷지 못하는 바닥 위의 천장(점포 안쪽 등): 가까운 걷는 바닥 중 천장 밑 2.2 m 이하에 있는 가장 높은 것을 바닥으로 본다.
            var taken = new HashSet<(int, int, int)>(cells.Select(c => (c.X, c.Z, Mathf.RoundToInt(c.Floor.y * 10))));
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    float cx = (x + .5f) * Step, cz = (z + .5f) * Step;
                    ceilings.At(cx, cz, above);
                    if (above.Count == 0 || walkable.ContainsKey((x, z))) continue;
                    foreach (var ceiling in above)
                    {
                        if (!NearestFloor(walkable, x, z, ceiling.y, out float floorY)) continue;
                        if (!taken.Add((x, z, Mathf.RoundToInt(floorY * 10)))) break;
                        cells.Add(new Cell { X = x, Z = z, Floor = new Vector3(cx, floorY, cz), CeilingY = ceiling.y, Normal = ceiling.normal, Owner = ceilingOwners[ceiling.owner], Walkable = false });
                        break;
                    }
                }
            foreach (var cell in cells) cell.Zone = points.ZoneAt(cell.Floor + Vector3.up * .1f)?.id ?? "";
            foreach (var cell in open) cell.Zone = points.ZoneAt(cell.Floor + Vector3.up * .1f)?.id ?? "";
            var lampHits = new List<(float y, int owner, Vector3 normal)>();
            return new Result
            {
                Cells = cells.OrderBy(c => c.X).ThenBy(c => c.Z).ThenBy(c => c.Floor.y).ToList(),
                Open = open.OrderBy(c => c.X).ThenBy(c => c.Z).ThenBy(c => c.Floor.y).ToList(),
                // 조명 면이 천장면 가까이(아래 .35 m, 위 .15 m)에 있고 반경 안 아홉 점 중 한 곳이라도 걸리면 조명 곁이다.
                LampNear = (x, z, ceilingY, radius) =>
                {
                    for (int i = 0; i < 9; i++)
                    {
                        lamps.At(x + (i % 3 - 1) * radius, z + (i / 3 - 1) * radius, lampHits);
                        if (lampHits.Exists(l => l.y >= ceilingY - .35f && l.y <= ceilingY + .15f)) return true;
                    }
                    return false;
                },
            };
        }

        private static bool NearestFloor(Dictionary<(int, int), List<float>> walkable, int x, int z, float ceilingY, out float floorY)
        {
            for (int radius = 1; radius <= 12; radius++)
            {
                float best = float.NegativeInfinity;
                for (int dx = -radius; dx <= radius; dx++)
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius || !walkable.TryGetValue((x + dx, z + dz), out var heights)) continue;
                        foreach (float y in heights)
                            if (y <= ceilingY - MinHeight && y >= ceilingY - MaxHeight && y > best) best = y;
                    }
                if (!float.IsNegativeInfinity(best)) { floorY = best; return true; }
            }
            floorY = 0;
            return false;
        }

        // 조명: 천장 등기구·LED 띠. 매장 간판 라이트박스(Kit_Lightbox_)는 천장 조명이 아니다.
        private static readonly string[] LampWords = { "Kit_Light_", "CeilingLED", "ShopLED" };

        private static bool IsLamp(string path)
        {
            foreach (var word in LampWords) if (path.Contains(word)) return true;
            return false;
        }

        private static bool IsCeiling(string path)
        {
            foreach (var word in NotCeilingWords) if (path.Contains(word)) return false;
            foreach (var word in CeilingWords) if (path.Contains(word)) return true;
            return false;
        }

        private static void AddDownFacing(MeshFilter filter, SurfaceIndex index, int owner)
        {
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
                    var normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-10f) continue;
                    // 45° 넘게 기울지 않은, 아래를 보는 면만 천장이다.
                    if (normal.normalized.y < -.7f) index.Add(a, b, c, owner);
                }
            }
        }

        public static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            for (var t = transform; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
