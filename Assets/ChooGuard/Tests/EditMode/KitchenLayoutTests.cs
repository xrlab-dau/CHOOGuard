using System.Collections.Generic;
using System.Linq;
using ChooGuard.Editor;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.EditMode
{
    /// <summary>
    /// The kitchen layout keeps the standards' distances and the room's furniture on rooms shaped like the twin's tenant shops
    /// (sizes and fixtures measured from FpsStation), in the room's own frame and turned in the world.
    /// </summary>
    public sealed class KitchenLayoutTests
    {
        /// <summary>A food shop as <c>KitchenRooms.Measure</c> measured it from FpsStation (2026-09-30): width, depth, ceiling height above the floor, where the customer stands, the sales fixtures, tables and chairs as (u, v) rectangles.</summary>
        public sealed class Shop
        {
            public readonly string Label;
            public readonly float Width, Depth, Ceiling;
            public readonly Vector2? Customer;
            public readonly (string Kind, Rect Area)[] Things;
            public Shop(string label, float width, float depth, float ceiling, Vector2? customer, (string, Rect)[] things)
            {
                Label = label; Width = width; Depth = depth; Ceiling = ceiling; Customer = customer; Things = things;
            }
            public override string ToString() => Label;
        }

        private static (string, Rect) R(string kind, float u0, float v0, float u1, float v1) => (kind, Rect.MinMaxRect(u0, v0, u1, v1));

        private static readonly Shop[] Shops =
        {
            new Shop("SUBWAY", 8f, 7.5f, 3.44f, new Vector2(3.661f, 2.652f), new[] { R("fixture", 0.591f, 0.538f, 3.142f, 2.462f), R("display", 1.396f, 1.087f, 2.337f, 1.913f), R("fixture", 2.724f, 0.538f, 5.276f, 2.462f), R("display", 3.529f, 1.087f, 4.471f, 1.913f), R("fixture", 4.858f, 0.538f, 7.409f, 2.462f), R("display", 5.663f, 1.087f, 6.604f, 1.913f), R("table", 1.232f, 4.532f, 2.768f, 6.068f), R("chair", 0.777f, 4.936f, 1.523f, 5.664f), R("chair", 2.477f, 4.936f, 3.223f, 5.664f), R("table", 4.432f, 4.532f, 5.968f, 6.068f), R("chair", 3.977f, 4.936f, 4.723f, 5.664f), R("chair", 5.677f, 4.936f, 6.423f, 5.664f) }),
            new Shop("국제시장단팥빵", 5f, 6f, 3.44f, null, new[] { R("fixture", 0.291f, 0.274f, 2.709f, 2.126f), R("display", 1.029f, 0.787f, 1.971f, 1.613f), R("fixture", 2.291f, 0.274f, 4.709f, 2.126f), R("display", 3.029f, 0.787f, 3.971f, 1.613f) }),
            new Shop("국제시장도나스", 7f, 5f, 3.44f, null, new[] { R("fixture", 0.691f, -0.033f, 3.509f, 2.033f), R("display", 1.629f, 0.587f, 2.571f, 1.413f), R("fixture", 3.491f, -0.033f, 6.309f, 2.033f), R("display", 4.429f, 0.587f, 5.371f, 1.413f) }),
            new Shop("남천할매떡볶이", 7f, 7f, 3.44f, new Vector2(3.166f, 2.548f), new[] { R("fixture", 0.691f, 0.367f, 3.509f, 2.433f), R("display", 1.629f, 0.987f, 2.571f, 1.813f), R("fixture", 3.491f, 0.367f, 6.309f, 2.433f), R("display", 4.429f, 0.987f, 5.371f, 1.813f) }),
            new Shop("다인명가", 4.8f, 7.2f, 3.44f, new Vector2(2.069f, 2.592f), new[] { R("fixture", 0.271f, 0.536f, 2.609f, 2.344f), R("display", 0.969f, 1.027f, 1.911f, 1.853f), R("fixture", 2.191f, 0.536f, 4.529f, 2.344f), R("display", 2.889f, 1.027f, 3.831f, 1.853f), R("table", 1.232f, 4.232f, 2.768f, 5.768f), R("chair", 0.777f, 4.636f, 1.523f, 5.364f), R("chair", 2.477f, 4.636f, 3.223f, 5.364f) }),
            new Shop("떡공방명제", 7f, 9.97f, 3.44f, new Vector2(3.165f, 3.151f), new[] { R("fixture", 0.691f, 0.961f, 3.509f, 3.027f), R("display", 1.629f, 1.581f, 2.571f, 2.407f), R("fixture", 3.491f, 0.961f, 6.309f, 3.027f), R("display", 4.429f, 1.581f, 5.371f, 2.407f), R("table", 1.232f, 7.002f, 2.768f, 8.538f), R("chair", 0.777f, 7.406f, 1.523f, 8.134f), R("chair", 2.477f, 7.406f, 3.223f, 8.134f), R("table", 1.232f, 4.002f, 2.768f, 5.538f), R("chair", 0.777f, 4.406f, 1.523f, 5.134f), R("chair", 2.477f, 4.406f, 3.223f, 5.134f) }),
            new Shop("반월당닭강정", 4.4f, 6.6f, 3.44f, new Vector2(1.869f, 2.476f), new[] { R("fixture", 0.231f, 0.458f, 2.409f, 2.182f), R("display", 0.849f, 0.907f, 1.791f, 1.733f), R("fixture", 1.991f, 0.458f, 4.169f, 2.182f), R("display", 2.609f, 0.907f, 3.551f, 1.733f) }),
            new Shop("비엔씨제과", 11.45f, 7.5f, 3.44f, new Vector2(4.861f, 2.413f), new[] { R("fixture", 0.936f, 0.496f, 3.644f, 2.504f), R("display", 1.819f, 1.087f, 2.761f, 1.913f), R("fixture", 3.226f, 0.496f, 5.934f, 2.504f), R("display", 4.109f, 1.087f, 5.051f, 1.913f), R("fixture", 5.516f, 0.496f, 8.224f, 2.504f), R("display", 6.399f, 1.087f, 7.341f, 1.913f), R("fixture", 7.806f, 0.496f, 10.514f, 2.504f), R("display", 8.689f, 1.087f, 9.631f, 1.913f), R("table", 1.232f, 4.532f, 2.768f, 6.068f), R("chair", 0.777f, 4.936f, 1.523f, 5.664f), R("chair", 2.477f, 4.936f, 3.223f, 5.664f), R("table", 4.432f, 4.532f, 5.968f, 6.068f), R("chair", 3.977f, 4.936f, 4.723f, 5.664f), R("chair", 5.677f, 4.936f, 6.423f, 5.664f), R("table", 7.632f, 4.532f, 9.168f, 6.068f), R("chair", 7.177f, 4.936f, 7.923f, 5.664f), R("chair", 8.877f, 4.936f, 9.623f, 5.664f) }),
            new Shop("소반한식", 5.48f, 7.5f, 3.44f, new Vector2(2.41f, 2.65f), new[] { R("fixture", 0.339f, 0.523f, 2.949f, 2.477f), R("display", 1.173f, 1.087f, 2.115f, 1.913f), R("fixture", 2.531f, 0.523f, 5.141f, 2.477f), R("display", 3.365f, 1.087f, 4.307f, 1.913f), R("table", 1.232f, 4.532f, 2.768f, 6.068f), R("chair", 0.777f, 4.936f, 1.523f, 5.664f), R("chair", 2.477f, 4.936f, 3.223f, 5.664f) }),
            new Shop("청도할매김밥", 4.8f, 6.6f, 3.44f, new Vector2(2.069f, 2.471f), new[] { R("fixture", 0.271f, 0.416f, 2.609f, 2.224f), R("display", 0.969f, 0.907f, 1.911f, 1.733f), R("fixture", 2.191f, 0.416f, 4.529f, 2.224f), R("display", 2.889f, 0.907f, 3.831f, 1.733f) }),
            new Shop("크리스피크림도넛", 7f, 10.98f, 3.44f, new Vector2(3.168f, 3.345f), new[] { R("fixture", 0.691f, 1.163f, 3.509f, 3.229f), R("display", 1.629f, 1.783f, 2.571f, 2.609f), R("fixture", 3.491f, 1.163f, 6.309f, 3.229f), R("display", 4.429f, 1.783f, 5.371f, 2.609f), R("table", 1.232f, 8.012f, 2.768f, 9.548f), R("chair", 0.777f, 8.416f, 1.523f, 9.144f), R("chair", 2.477f, 8.416f, 3.223f, 9.144f), R("table", 1.232f, 5.012f, 2.768f, 6.548f), R("chair", 0.777f, 5.416f, 1.523f, 6.144f), R("chair", 2.477f, 5.416f, 3.223f, 6.144f) }),
            new Shop("환공어묵", 6f, 7.5f, 3.44f, new Vector2(2.668f, 2.653f), new[] { R("fixture", 0.391f, 0.467f, 3.209f, 2.533f), R("display", 1.329f, 1.087f, 2.271f, 1.913f), R("fixture", 2.791f, 0.467f, 5.609f, 2.533f), R("display", 3.729f, 1.087f, 4.671f, 1.913f), R("table", 1.232f, 4.532f, 2.768f, 6.068f), R("chair", 0.777f, 4.936f, 1.523f, 5.664f), R("chair", 2.477f, 4.936f, 3.223f, 5.664f) }),
        };

        private static KitchenRoom Room(Shop shop, float turn)
        {
            var u = new Vector2(Mathf.Cos(turn * Mathf.Deg2Rad), Mathf.Sin(turn * Mathf.Deg2Rad));
            var room = new KitchenRoom { ShopId = "shop-" + shop.Label, Label = shop.Label, Origin = new Vector2(-40, 12), U = u, V = new Vector2(-u.y, u.x), Width = shop.Width, Depth = shop.Depth, FloorY = 7.05f, CeilingHeight = shop.Ceiling, Customer = shop.Customer };
            foreach (var (kind, area) in shop.Things) room.Obstacles.Add(new KitchenRoom.Obstacle(shop.Label + " " + kind, area));
            return room;
        }

        private static Vector2 ToRoom(KitchenRoom room, Vector3 world)
        {
            var d = new Vector2(world.x, world.z) - room.Origin;
            return new Vector2(Vector2.Dot(d, room.U), Vector2.Dot(d, room.V));
        }

        [Test]
        public void EveryFoodShopGetsAKitchenThatKeepsTheStandardsDistances([ValueSource(nameof(Shops))] Shop shop, [Values(0f, 37f, 200f)] float turn)
        {
            var room = Room(shop, turn);
            var plan = KitchenLayout.PlanShop(room);

            KitchenGasBuilder.AuditShop(plan);   // 계량기 ≥2 m·1.6~2 m, 경보기 8 m 안·천장 0.3 m 이내, K급 소화기 ≤1.5 m: 어기면 던진다
            foreach (var part in plan.Parts)
            {
                var at = ToRoom(room, part.Position);
                Assert.That(at.x, Is.InRange(-.05f, room.Width + .05f), part.Id + " 이 방 폭 밖");
                Assert.That(at.y, Is.InRange(0f, room.Depth - .3f), part.Id + " 이 방 깊이 밖");
            }
        }

        [Test]
        public void EveryCookingStationHasAHoodAndAnAutomaticExtinguisherAndEveryRangeItsGasFittings([ValueSource(nameof(Shops))] Shop shop)
        {
            var plan = KitchenLayout.PlanShop(Room(shop, 0));
            var ids = new HashSet<string>(plan.Parts.Select(p => p.Id));
            string Data(KitchenPart part, string key) => part.Data.Split(';').Select(p => p.Split('=')).Where(p => p[0] == key).Select(p => p[1]).FirstOrDefault() ?? "";
            foreach (var appliance in plan.Parts.Where(p => p.Kind == "kitchen_fryer" || p.Kind == "gas_range"))
            {
                Assert.That(plan.Parts.Any(p => p.Kind == "exhaust_hood" && Data(p, "over") == appliance.Id), appliance.Id + " 위에 후드가 없음");
                Assert.That(plan.Parts.Any(p => p.Kind == "kitchen_auto_extinguisher" && Data(p, "over") == appliance.Id), appliance.Id + " 위에 자동확산소화기가 없음");
            }
            foreach (var range in plan.Parts.Where(p => p.Kind == "gas_range"))
            {
                Assert.That(ids.Contains(Data(range, "valve")) && ids.Contains(Data(range, "hose")), range.Id + " 의 밸브·호스가 없음");
                Assert.That(plan.Parts.Any(p => p.Kind == "fuse_cock" && Data(p, "range") == range.Id), range.Id + " 의 퓨즈콕이 없음");
            }
            Assert.That(plan.Parts.Count(p => p.Kind == "kitchen_k_extinguisher"), Is.EqualTo(1));
            Assert.That(plan.Parts.Count(p => p.Kind == "gas_alarm"), Is.EqualTo(KitchenLayout.CooksWithGas(shop.Label) ? 1 : 0));
            Assert.That(plan.Parts.Count(p => p.Kind == "gas_meter"), Is.EqualTo(KitchenLayout.CooksWithGas(shop.Label) ? 1 : 0));
        }

        [Test]
        public void TheKitchenDoesNotStandOnTheShopsFurnitureOrOnTheCustomer([ValueSource(nameof(Shops))] Shop shop, [Values(0f, 37f)] float turn)
        {
            var room = Room(shop, turn);
            var plan = KitchenLayout.PlanShop(room);
            var floorStanding = new[] { "kitchen_fryer", "gas_range", "kitchen_oven", "kitchen_table", "kitchen_counter" };
            foreach (var part in plan.Parts.Where(p => floorStanding.Contains(p.Kind)))
            {
                var at = ToRoom(room, part.Position);
                foreach (var o in room.Obstacles.Where(o => !o.Name.Contains(" fixture")))
                    Assert.That(new Rect(o.Area.x - .3f, o.Area.y - .3f, o.Area.width + .6f, o.Area.height + .6f).Contains(at), Is.False, part.Id + " 이 " + o.Name + " 위에 섬");
                if (room.Customer is Vector2 customer) Assert.That(Vector2.Distance(at, customer), Is.GreaterThan(.5f), part.Id + " 이 손님 자리에 섬");
            }
        }

        [Test]
        public void TheLayoutIsTheSameEveryTime([ValueSource(nameof(Shops))] Shop shop)
        {
            var first = KitchenLayout.PlanShop(Room(shop, 0));
            var second = KitchenLayout.PlanShop(Room(shop, 0));
            CollectionAssert.AreEqual(first.Parts.Select(p => p.Id + p.Position + p.Euler + p.Data), second.Parts.Select(p => p.Id + p.Position + p.Euler + p.Data));
        }
    }
}
