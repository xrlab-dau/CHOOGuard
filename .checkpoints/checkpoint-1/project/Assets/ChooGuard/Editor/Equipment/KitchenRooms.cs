using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Measures a tenant shop of the twin (FpsStation, the hall root "맞이방 · 원본 정합") into a <see cref="KitchenRoom"/>: the three
    /// wall meshes give the rectangle, the sales fixtures, tables and chairs named after the shop become obstacles, the flat
    /// shop ceiling gives the ceiling height and a ray down gives the floor.
    /// </summary>
    public static class KitchenRooms
    {
        private const string HallRoot = "맞이방 · 원본 정합";

        /// <summary>The room of <paramref name="shop"/> (a shop point of the twin), or throws with what could not be measured.</summary>
        public static KitchenRoom Measure(StationPoints.Point shop)
        {
            var hall = GameObject.Find(HallRoot) ?? throw new InvalidOperationException("FpsStation 을 열어 두세요(" + HallRoot + " 없음)");
            var walls = hall.transform.Find("Shop_" + shop.Label.Replace(' ', '_') + "_Walls") ?? throw new InvalidOperationException(shop.Label + ": 벽 메시 없음");
            var filter = walls.GetComponentInChildren<MeshFilter>();
            var bounds = walls.GetComponentInChildren<Renderer>().bounds;
            var vertices = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray();
            var triangles = filter.sharedMesh.triangles;
            // 벽마다 바닥 모서리 하나: 삼각형 중 바닥 꼭짓점이 둘인 것의 두 점.
            var edges = new List<(Vector2 a, Vector2 b)>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var low = new[] { vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]] }.Where(p => p.y < bounds.min.y + .1f).ToArray();
                if (low.Length != 2) continue;
                var a = new Vector2(low[0].x, low[0].z);
                var b = new Vector2(low[1].x, low[1].z);
                if ((a - b).magnitude < .5f || edges.Any(e => Same(e.a, a) && Same(e.b, b) || Same(e.a, b) && Same(e.b, a))) continue;
                edges.Add((a, b));
            }
            if (edges.Count != 3) throw new InvalidOperationException(shop.Label + ": 벽이 " + edges.Count + "개(3개여야 함)");
            // 뒷벽 = 두 끝이 모두 다른 벽과 만나는 벽.
            var back = edges.Single(e => edges.Count(o => Same(o.a, e.a) || Same(o.b, e.a)) == 2 && edges.Count(o => Same(o.a, e.b) || Same(o.b, e.b)) == 2);
            var sides = edges.Where(e => !e.Equals(back)).ToArray();
            var far = sides.Select(e => Same(e.a, back.a) || Same(e.a, back.b) ? e.b : e.a).ToArray();
            var frontMid = (far[0] + far[1]) / 2;
            var backMid = (back.a + back.b) / 2;
            var along = (back.b - back.a).normalized;
            var toFront = new Vector2(-along.y, along.x);
            var room = new KitchenRoom { ShopId = shop.Id, Label = shop.Label };
            if (Vector2.Dot(toFront, frontMid - backMid) < 0) { room.Origin = back.b; room.U = -along; toFront = -toFront; }
            else { room.Origin = back.a; room.U = along; }
            room.V = toFront;
            room.Width = (back.b - back.a).magnitude;
            room.Depth = Vector2.Dot(frontMid - backMid, toFront);
            var centre = room.Ground(room.Width / 2, room.Depth / 2);
            room.FloorY = FloorAt(centre, bounds.min.y);
            room.CeilingHeight = CeilingAt(hall.transform, bounds) - room.FloorY;

            foreach (Transform child in hall.transform)
            {
                if (!child.name.StartsWith(shop.Label + " ", StringComparison.Ordinal)) continue;
                string kind = child.name.Substring(shop.Label.Length + 1);
                if (!(kind.StartsWith("fixture") || kind.StartsWith("display") || kind.StartsWith("table") || kind.StartsWith("chair"))) continue;
                var renderers = child.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var box = renderers[0].bounds;
                foreach (var r in renderers) box.Encapsulate(r.bounds);
                room.Obstacles.Add(new KitchenRoom.Obstacle(child.name, ToRoom(room, box)));
            }
            var p = new Vector2(shop.Position.x, shop.Position.z) - room.Origin;
            var customer = new Vector2(Vector2.Dot(p, room.U), Vector2.Dot(p, room.V));
            if (customer.x > 0 && customer.x < room.Width && customer.y > 0 && customer.y < room.Depth) room.Customer = customer;
            return room;
        }

        private static bool Same(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < .0004f;

        /// <summary>The (u, v) rectangle a world box covers (its eight corners projected).</summary>
        private static Rect ToRoom(KitchenRoom room, Bounds box)
        {
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (var x in new[] { box.min.x, box.max.x })
                foreach (var z in new[] { box.min.z, box.max.z })
                {
                    var p = new Vector2(x, z) - room.Origin;
                    float u = Vector2.Dot(p, room.U), v = Vector2.Dot(p, room.V);
                    minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                    minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
                }
            return Rect.MinMaxRect(minU, minV, maxU, maxV);
        }

        private static float FloorAt(Vector2 ground, float wallBottom)
        {
            var hits = Physics.RaycastAll(new Vector3(ground.x, wallBottom + 1f, ground.y), Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
            return hits.Length > 0 ? hits.Max(h => h.point.y) : wallBottom;
        }

        /// <summary>The flat suspended ceiling of the tenant row (the hall's "Majibang_ShopCeiling"), else just under the wall tops.</summary>
        private static float CeilingAt(Transform hall, Bounds walls)
        {
            var ceiling = hall.Find("Majibang_ShopCeiling");
            var renderer = ceiling != null ? ceiling.GetComponentInChildren<Renderer>() : null;
            return renderer != null ? renderer.bounds.center.y : walls.max.y - .1f;
        }
    }
}
