using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>One piece of kitchen equipment the layout puts down (the builder turns it into a placement entry).</summary>
    public sealed class KitchenPart
    {
        public string Id, Kind, Label, Prefab, Data = "";
        public Vector3 Position, Euler;
    }

    /// <summary>A shop's kitchen: where it went and what the standards' distances came to (the audit the builder prints and the tests check).</summary>
    public sealed class KitchenPlan
    {
        public KitchenRoom Room;
        public string Side = "";
        /// <summary>Along the side wall (v of the room): where the cooking line starts and ends, and how deep the kitchen reaches into the room (m).</summary>
        public float Start, End, Depth;
        public bool PassCounter;
        public readonly List<KitchenPart> Parts = new List<KitchenPart>();
        /// <summary>Metres between the meter and the nearest burner (annex 7: at least 2 m from any flame); null when the shop burns no gas.</summary>
        public float? MeterToBurner;
        public float MeterHeight, AlarmDrop, AlarmToBurner, KExtinguisherTop, HoodBottom;
        public float Clearance;
    }

    /// <summary>
    /// Lays a shop's kitchen out along one side wall of its room (<see cref="KitchenRooms"/>): the cooking line from the shop's
    /// <see cref="StationKitchens.Spec"/> in stations of 1.2 m, a hood with a duct to the ceiling over every fryer or range, a stainless
    /// backsplash behind them, a stainless pass counter between the kitchen and the shop floor, and for a gas shop the whole gas
    /// route — the meter, the pipe along the wall, the intermediate valve, fuse cock and hose of every range — with the leak
    /// alarm, the K-class extinguisher and the automatic diffusion extinguisher. Distances follow the standards
    /// (도시가스사업법 시행규칙 별표 7: meter 1.6–2 m high and at least 2 m from any flame; NFTC 206: alarm within 8 m of the burner and its
    /// underside within 0.3 m of the ceiling; NFTC 101 표 2.1.1.3 and 2.1.1.6: K-class extinguisher no higher than 1.5 m) and
    /// the room's existing furniture; nothing is random.
    /// </summary>
    public static class KitchenLayout
    {
        public const float Station = 1.2f, PipeD = .06f, ApplianceBack = .22f;
        public const float MeterMinBurnerDistance = 2.05f, MeterBottomMin = 1.6f, MeterBottomMax = 2f;
        public const float AlarmDropMax = .3f, AlarmReach = 8f, ExtinguisherTopMax = 1.5f;
        private const float HoodHeight = 1.5f, HeaderY = 1.7f, HighRunY = 2.4f, MeterRiserTop = .55f, PipeLength = .5f;
        /// <summary>Which side of the meter its outlet riser stands on, in the meter model's own +X (the inlet is on the other). Checked against the model by the builder's render.</summary>
        public const float MeterOutletX = -.08f;

        /// <summary>Depth of a range (m) and of the hood over it, and where the burner under the pan is on the range (model coordinates: x right, y up, z toward the front).</summary>
        public const float RangeDepth = .642f, HoodDepth = .9f, KExtinguisherHeight = .66f, KExtinguisherBottom = .47f;
        public static readonly Vector3 Burner = new Vector3(-.14f, .93f, -.1f);

        public const string FryerPrefab = "KitchenFryer", RangePrefab = "GasRange", OvenPrefab = "KitchenOven", HoodPrefab = "ExhaustHood", SplashPrefab = "KitchenBacksplash",
            TablePrefab = "KitchenTable", CounterPrefab = "KitchenCounter", MeterPrefab = "GasMeter", ValvePrefab = "GasValve", FusePrefab = "FuseCock", HosePrefab = "GasHose",
            AlarmPrefab = "GasAlarm", AutoPrefab = "AutoExtinguisher", KPrefab = "KExtinguisher", PipePrefab = "GasPipe", DropPrefab = "GasPipeDrop",
            ElbowDownPrefab = "GasPipeElbowDown", ElbowUpPrefab = "GasPipeElbowUp";

        /// <summary>Plans the kitchen of the shop in <paramref name="room"/>; throws when no arrangement fits.</summary>
        public static KitchenPlan Plan(KitchenRoom room, StationKitchens.Spec spec)
        {
            var line = spec.Line.ToList();
            for (int drop = 0; ; drop++)
            {
                var stations = line.Take(line.Count - drop).ToList();
                if (stations.Count == 0 || !stations.Any(s => s != "table")) break;
                // 깊이: 통로 1 m + 스테인리스 조리대장(손님과 가름) / 통로만 / 통로 0.6 m(손님 자리 옆 좁은 점포)
                foreach (var (depth, pass) in new[] { (2.5f, true), (1.9f, false), (1.5f, false) })
                {
                    KitchenPlan best = null;
                    foreach (bool left in new[] { true, false })
                    {
                        var plan = TryPlace(room, spec, stations, left, depth, pass);
                        if (plan != null && (best == null || plan.Clearance > best.Clearance + 1e-4f)) best = plan;
                    }
                    if (best != null) return best;
                }
            }
            throw new InvalidOperationException(room.Label + ": 주방 라인이 들어갈 자리가 없습니다 (폭 " + room.Width.ToString("0.0") + " × 깊이 " + room.Depth.ToString("0.0") + " m)");
        }

        private sealed class Wall
        {
            public KitchenRoom Room;
            public bool Left;
            public Vector2 Inward, Right, Along;
            public float Yaw;
            public float U(float d) => Left ? d : Room.Width - d;
            public Vector3 P(float s, float d, float height) => Room.World(U(d), s, height);
            /// <summary>Which way (+1: toward the front of the room, -1: toward the back wall) the model's +X points along the wall.</summary>
            public float RightAlong => Mathf.Sign(Vector2.Dot(Right, Along));
        }

        private static KitchenPlan TryPlace(KitchenRoom room, StationKitchens.Spec spec, List<string> stations, bool left, float depth, bool pass)
        {
            var wall = new Wall { Room = room, Left = left, Along = room.V };
            wall.Inward = left ? room.U : -room.U;
            wall.Right = new Vector2(wall.Inward.y, -wall.Inward.x);
            wall.Yaw = Mathf.Atan2(wall.Inward.x, wall.Inward.y) * Mathf.Rad2Deg;
            var span = left ? new Vector2(0, depth) : new Vector2(room.Width - depth, room.Width);

            // 어디서부터: 판매대(같은 폭에 걸친 것) 앞 0.3 m, 손님 서는 자리 0.7 m 뒤.
            float start = 0;
            foreach (var o in room.Obstacles)
                if (o.Name.Contains(" fixture") && o.Area.xMax > span.x && o.Area.xMin < span.y) start = Mathf.Max(start, o.Area.yMax + .3f);
            if (room.Customer is Vector2 c && c.x > span.x - .5f && c.x < span.y + .5f) start = Mathf.Max(start, c.y + .85f);
            start = Mathf.Ceil(start * 20) / 20f;
            float end = start + Station * stations.Count;
            if (end + .2f > room.Depth - .5f) return null;
            var area = Rect.MinMaxRect(span.x, start - .1f, span.y, end + .1f);
            float clearance = room.Depth - .5f - (end + .2f);
            foreach (var o in room.Obstacles)
            {
                if (o.Name.Contains(" fixture") || o.Name.Contains(" display")) continue;
                float gap = Gap(area, o.Area);
                if (gap < .35f) return null;
                clearance = Mathf.Min(clearance, gap);
            }
            if (room.Customer is Vector2 cu) clearance = Mathf.Min(clearance, Gap(area, new Rect(cu.x, cu.y, 0, 0)) - .5f);

            var plan = new KitchenPlan { Room = room, Side = left ? "left" : "right", Start = start, End = end, Depth = depth, PassCounter = pass, Clearance = clearance };
            float ceiling = room.CeilingHeight;
            if (Mathf.Abs(ceiling - 3.4f) > .06f) throw new InvalidOperationException(room.Label + ": 천장 높이 " + ceiling.ToString("0.00") + " m (3.4 m 로 설계한 후드·배관과 맞지 않음)");
            float hoodBottom = ceiling - HoodHeight;
            plan.HoodBottom = hoodBottom;
            var parts = plan.Parts;
            var counts = new Dictionary<string, int>();
            string Next(string role) { counts.TryGetValue(role, out int k); counts[role] = ++k; return room.ShopId + "/" + role + "-" + k; }
            KitchenPart Add(string id, string kind, string label, string prefab, Vector3 position, Vector3 euler, string data)
            {
                var part = new KitchenPart { Id = id, Kind = kind, Label = label, Prefab = prefab, Position = position, Euler = euler, Data = "shop=" + room.ShopId + (data.Length > 0 ? ";" + data : "") };
                parts.Add(part);
                return part;
            }
            Vector3 Face() => new Vector3(0, wall.Yaw, 0);
            // 벽을 따라 수평으로 뻗는 다리가 dir(벽 방향 ±1)을 가리키게 하는 방위각(모델의 다리는 +X).
            Vector3 Leg(float along) { var d = wall.Along * along; return new Vector3(0, Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg, 0); }

            // ── 조리 라인 ──
            var stationsData = new List<(string kind, string cook, float s, string applianceId, string hoodId, string autoId, string valveId, string hoseId)>();
            var counter = new Dictionary<string, int>();
            for (int i = 0; i < stations.Count; i++)
            {
                var token = stations[i].Split(':');
                string kind = token[0], cook = token.Length > 1 ? token[1] : "";
                bool hooded = kind == "fryer" || kind == "range";
                float s = start + Station * i + Station / 2;
                string appliance = kind == "table" ? null : room.ShopId + "/" + kind + "-" + (counter[kind] = (counter.TryGetValue(kind, out int c1) ? c1 : 0) + 1);
                string hood = hooded ? room.ShopId + "/hood-" + (counter["hood"] = (counter.TryGetValue("hood", out int c2) ? c2 : 0) + 1) : null;
                string auto = kind != "table" ? room.ShopId + "/auto-" + (counter["auto"] = (counter.TryGetValue("auto", out int c3) ? c3 : 0) + 1) : null;
                string valve = kind == "range" ? room.ShopId + "/valve-" + counter["range"] : null;
                string hose = kind == "range" ? room.ShopId + "/hose-" + counter["range"] : null;
                stationsData.Add((kind, cook, s, appliance, hood, auto, valve, hose));
            }
            var burners = new List<float>();
            foreach (var st in stationsData)
            {
                bool hooded = st.hoodId != null;
                switch (st.kind)
                {
                    case "table":
                        Add(Next("table"), "kitchen_table", "스테인리스 작업대", TablePrefab, wall.P(st.s, .02f, 0), Face(), "");
                        break;
                    case "oven":
                        Add(st.applianceId, KitchenAppliancePoint.OvenKind, "오븐", OvenPrefab, wall.P(st.s, .02f + .4f, 0), Face(), "fuel=electric;auto=" + st.autoId);
                        Add(st.autoId, AutoExtinguisherPoint.Kind, "주방용 자동확산소화기", AutoPrefab, wall.P(st.s, .42f, ceiling), Face(), "over=" + st.applianceId);
                        break;
                    case "fryer":
                        Add(Next("table"), "kitchen_table", "스테인리스 작업대", TablePrefab, wall.P(st.s, .02f, 0), Face(), "");
                        Add(st.applianceId, KitchenAppliancePoint.FryerKind, "튀김기", FryerPrefab, wall.P(st.s, .37f, .85f), Face(), "fuel=electric;hood=" + st.hoodId + ";auto=" + st.autoId);
                        break;
                    case "range":
                        burners.Add(st.s);
                        Add(st.applianceId, KitchenAppliancePoint.RangeKind, "가스레인지", RangePrefab, wall.P(st.s, ApplianceBack + RangeDepth / 2, 0), Face(),
                            "fuel=gas;valve=" + st.valveId + ";hose=" + st.hoseId + ";hood=" + st.hoodId + ";auto=" + st.autoId);
                        if (st.cook.Length > 0)
                            Add(Next("cook"), "kitchen_cookware", "조리 도구", st.cook, wall.P(st.s + Burner.x * wall.RightAlong, ApplianceBack + RangeDepth / 2 + Burner.z, Burner.y), Face(), "");
                        break;
                }
                if (!hooded) continue;
                Add(st.hoodId, "exhaust_hood", "주방 후드", HoodPrefab, wall.P(st.s, HoodDepth / 2, hoodBottom), Face(), "over=" + st.applianceId);
                Add(Next("splash"), "kitchen_backsplash", "스테인리스 벽판", SplashPrefab, wall.P(st.s, 0, .86f), Face(), "");
                Add(st.autoId, AutoExtinguisherPoint.Kind, "주방용 자동확산소화기", AutoPrefab, wall.P(st.s, HoodDepth / 2, hoodBottom + .5f), Face(), "over=" + st.applianceId);
            }
            if (pass)
                for (int i = 0; i < stations.Count; i++)
                    Add(Next("counter"), "kitchen_counter", "스테인리스 조리대장", CounterPrefab, wall.P(start + Station * i + Station / 2, 1.9f, 0), Face(), "");

            // ── 가스 배관: 계량기 → 배관 → 중간밸브 → 퓨즈콕 → 호스 → 레인지 ──
            float? kSide = null;
            if (spec.Gas)
            {
                float first = burners.Min(), last = burners.Max();
                float outletDir = Mathf.Sign(MeterOutletX * wall.RightAlong);           // 출구 쪽 입상관이 벽을 따라 기우는 방향(+1: 방 앞쪽)
                float lineDir = outletDir;                                            // 배관은 출구 쪽으로 나가므로 조리 라인이 그쪽에 있어야 한다
                float sm;
                if (lineDir > 0) { sm = Mathf.Min(start - .6f, first - MeterMinBurnerDistance); if (sm - .25f < .35f) return null; }
                else { sm = Mathf.Max(end + .5f, last + MeterMinBurnerDistance); if (sm + .25f > room.Depth - .35f) return null; }
                float meterBottom = ceiling - 1.65f;
                float sOut = sm + outletDir * .08f, sIn = sm - outletDir * .08f;
                string mainId = room.ShopId + "/valve-main";
                var meter = Add(Next("meter"), "gas_meter", "가스계량기", MeterPrefab, wall.P(sm, 0, meterBottom), Face(), "main=" + mainId + ";height=" + Num(meterBottom));
                plan.MeterHeight = meterBottom;
                // 공급관: 천장에서 내려와 메인밸브를 지나 계량기 입구(입상관)로.
                Add(Next("pipe"), "gas_pipe", "도시가스 배관", DropPrefab, wall.P(sIn, PipeD, ceiling), Face(), "");
                Add(mainId, GasValvePoint.Kind, "가스 메인밸브(계량기 앞)", ValvePrefab, wall.P(sIn, PipeD, ceiling - .55f), new Vector3(0, wall.Yaw, 90), "role=main");
                Add(Next("pipe"), "gas_pipe", "도시가스 배관", DropPrefab, wall.P(sIn, PipeD, ceiling - .6f), Face(), "");
                // 출구: 입상관 위 엘보 → 높은 곳 수평관 → 라인 앞에서 내려와 → 아래 수평 주관.
                float sTurn = lineDir > 0 ? start - .3f : end + .3f;
                if ((sTurn - sOut) * lineDir < .3f) sTurn = sOut + lineDir * .3f;
                Add(Next("pipe"), "gas_pipe", "도시가스 배관", ElbowDownPrefab, wall.P(sOut, PipeD, HighRunY), Leg(lineDir), "");
                Run(sOut + lineDir * .1f, sTurn - lineDir * .1f, HighRunY, lineDir);
                Add(Next("pipe"), "gas_pipe", "도시가스 배관", ElbowDownPrefab, wall.P(sTurn, PipeD, HighRunY), Leg(-lineDir), "");
                Add(Next("pipe"), "gas_pipe", "도시가스 배관", DropPrefab, wall.P(sTurn, PipeD, HighRunY - .1f), Face(), "");
                Add(Next("pipe"), "gas_pipe", "도시가스 배관", ElbowUpPrefab, wall.P(sTurn, PipeD, HeaderY), Leg(lineDir), "");
                // 레인지마다: 주관에서 내려오는 관에 중간밸브, 끝에 퓨즈콕, 거기서 호스가 레인지 뒤로.
                float farthest = sTurn;
                foreach (var st in stationsData.Where(x => x.kind == "range"))
                {
                    float sb = st.s - .42f * wall.RightAlong;
                    if ((sb - farthest) * lineDir > 0) farthest = sb;
                    Add(Next("pipe"), "gas_pipe", "도시가스 배관", DropPrefab, wall.P(sb, PipeD, HeaderY), Face(), "");
                    Add(st.valveId, GasValvePoint.Kind, "가스 중간밸브 · " + room.Label, ValvePrefab, wall.P(sb, PipeD, 1.45f), new Vector3(0, wall.Yaw, 90), "role=intermediate;feeds=" + st.applianceId);
                    Add(Next("fuse"), "fuse_cock", "퓨즈콕", FusePrefab, wall.P(sb, PipeD, 1.2f), Face(), "range=" + st.applianceId + ";valve=" + st.valveId);
                    Add(st.hoseId, GasHosePoint.Kind, "가스 호스", HosePrefab, wall.P(sb, PipeD + .1f, 1.125f), Face(), "range=" + st.applianceId + ";valve=" + st.valveId);
                }
                Run(sTurn + lineDir * .1f, farthest, HeaderY, lineDir);
                plan.MeterToBurner = burners.Min(sb => Mathf.Sqrt((sm - sb) * (sm - sb) + (ApplianceBack + RangeDepth / 2) * (ApplianceBack + RangeDepth / 2)));
                kSide = lineDir > 0 ? end + .35f : start - .4f;                           // K급 소화기와 경보기는 계량기 반대쪽
            }
            float sk = kSide ?? start - .4f;
            float kBottom = KExtinguisherBottom;
            Add(Next("kext"), KitchenExtinguisherPoint.Kind, "K급 소화기(주방용)", KPrefab, wall.P(sk, .14f, kBottom), Face(), "top=" + Num(kBottom + KExtinguisherHeight));
            plan.KExtinguisherTop = kBottom + KExtinguisherHeight;
            if (spec.Gas)
            {
                float center = ceiling - .25f + .0375f;
                Add(Next("alarm"), GasAlarmPoint.Kind, "가스누설경보기(LNG)", AlarmPrefab, wall.P(sk, 0, center), Face(), "gas=lng;height=" + Num(center) + ";ceiling=" + Num(ceiling));
                plan.AlarmDrop = ceiling - (center - .0375f);
                plan.AlarmToBurner = burners.Max(sb => Mathf.Sqrt((sk - sb) * (sk - sb) + (ApplianceBack + RangeDepth / 2) * (ApplianceBack + RangeDepth / 2)));
            }

            // 배관 한 줄(0.5 m 토막): from → to 를 벽 높이 y 에서 덮는다. 마지막 토막은 끝에 맞춘다(겹침은 반 토막 이하).
            void Run(float from, float to, float y, float dir)
            {
                float length = (to - from) * dir;
                if (length < .05f) return;
                int count = Mathf.CeilToInt(length / PipeLength - .001f);
                for (int k = 0; k < count; k++)
                {
                    float centreAlong = k == count - 1 ? length - PipeLength / 2 : (k + .5f) * PipeLength;
                    if (length < PipeLength) centreAlong = length / 2;
                    Add(Next("pipe"), "gas_pipe", "도시가스 배관", PipePrefab, wall.P(from + dir * centreAlong, PipeD, y), Face(), "");
                }
            }
            return plan;
        }

        private static float Gap(Rect a, Rect b)
        {
            float du = Mathf.Max(0, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dv = Mathf.Max(0, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(du * du + dv * dv);
        }

        internal static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
