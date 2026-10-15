using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Area-weighted sampling of the station's indoor walkable floor, not a list of spawn markers.</summary>
    internal static class PlayerSpawn
    {
        private readonly struct Triangle
        {
            public readonly int Offset;
            public readonly double Through;
            public Triangle(int offset, double through) { Offset = offset; Through = through; }
        }

        private static readonly Collider[] overlaps = new Collider[32];
        private const float FeetClearance = .05f;

        public static Vector3 Pick(StationPoints points, CharacterController body, int seed)
        {
            // A separate stream: choosing a spawn must not consume the crowd's or the director's random numbers.
            var random = new System.Random(seed ^ 0x51a7f00d);
            var mesh = NavMesh.CalculateTriangulation();
            int walkable = NavMesh.GetAreaFromName("Walkable"), mask = 1 << walkable;
            var triangles = new List<Triangle>(mesh.areas.Length);
            double total = 0;
            for (int i = 0; i < mesh.indices.Length; i += 3)
            {
                if (mesh.areas[i / 3] != walkable) continue;
                var a = mesh.vertices[mesh.indices[i]];
                var b = mesh.vertices[mesh.indices[i + 1]];
                var c = mesh.vertices[mesh.indices[i + 2]];
                if (!Indoor(points, (a + b + c) / 3f)) continue;
                double area = Vector3.Cross(b - a, c - a).magnitude * .5;
                if (area <= 1e-5) continue;
                total += area;
                triangles.Add(new Triangle(i, total));
            }
            Physics.SyncTransforms();
            for (int attempt = 0; attempt < 256 && triangles.Count > 0; attempt++)
            {
                double roll = random.NextDouble() * total;
                int lo = 0, hi = triangles.Count - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (roll < triangles[mid].Through) hi = mid; else lo = mid + 1;
                }
                int i = triangles[lo].Offset;
                var a = mesh.vertices[mesh.indices[i]];
                var b = mesh.vertices[mesh.indices[i + 1]];
                var c = mesh.vertices[mesh.indices[i + 2]];
                float u = Mathf.Sqrt((float)random.NextDouble()), v = (float)random.NextDouble();
                var point = (1 - u) * a + u * (1 - v) * b + u * v * c;
                if (!Indoor(points, point) || !NavMesh.SamplePosition(point, out var nav, .15f, mask)) continue;
                if (!NavMesh.FindClosestEdge(nav.position, out var edge, mask) || edge.distance < body.radius + body.skinWidth) continue;
                if (!Physics.Raycast(point + Vector3.up * .35f, Vector3.down, out var floor, .7f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Dot(floor.normal, Vector3.up) < Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)) continue;
                var feet = floor.point + Vector3.up * FeetClearance;
                if (!Indoor(points, feet) || Mathf.Abs(feet.y - nav.position.y) > .2f) continue;
                // Zone boxes also overlap the square at their edges. A real overhead floor/roof excludes those open-air points.
                if (!Physics.Raycast(feet + Vector3.up * 40f, Vector3.down, out _, 40f - body.height, ~0, QueryTriggerInteraction.Ignore)) continue;
                var centre = feet + body.center;
                float half = body.height * .5f - body.radius;
                int count = Physics.OverlapCapsuleNonAlloc(centre - Vector3.up * half, centre + Vector3.up * half,
                    body.radius + body.skinWidth, overlaps, ~0, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) continue;
                bool clear = true;
                for (int j = 0; j < count; j++)
                    if (overlaps[j] != body && !overlaps[j].transform.IsChildOf(body.transform)) { clear = false; break; }
                if (clear) return feet;
            }
            throw new InvalidOperationException("부산역 역사 내부에 이동 가능한 안전한 스폰 바닥을 찾지 못했습니다.");
        }

        private static bool Indoor(StationPoints points, Vector3 position)
        {
            var zone = points.ZoneAt(position);
            return zone != null && !StationWorld.IsOutdoorZone(zone.id) && zone.id != "tracks" && points.PlatformAt(position) == null;
        }
    }
}
