#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Hud;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // 길 안내는 플레이어가 아는 것과 구워 둔 역만으로 그려야 한다: 불이 깎은 navmesh·잠긴 문·닫힌 에스컬레이터·경로 그래프의 실시간 막힘은
    // 아직 모르는 위험이다. 이 시험은 그 소비자 관점 불변식을 본다(경로 모양·도착·대상), 문구나 화면 구조는 보지 않는다.
    // 합성된 사건이 없는 실행(JEV off)에서 위험은 시험이 직접 만든다: 규칙 시험이라 특정 Hazard 를 만들어도 된다.
    public sealed class Issue256NavigationSmokeTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private string savedKey;
        private bool savedRoute;
        private GameObject parent;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            savedRoute = GameSettings.ShowRoute;
            EmergencySession.NextSeed = 20260930;
            parent = new GameObject("길 안내 시험 위험");
            UnityEngine.Object.DontDestroyOnLoad(parent);
        }

        // 시험이 실패해도 전역 상태를 되돌리고, 근무 씬(역무원·열차를 쓰는 쪽)을 먼저 내린 한 프레임 뒤 역 씬을 내려 다음 시험에 씬을 남기지 않는다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                GameSettings.ShowRoute = savedRoute;
                Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
                EmergencySession.NextSeed = 0;
                Time.captureFramerate = 0;
                HazardRegistry.Clear();
                if (parent != null) UnityEngine.Object.Destroy(parent);
            }
            finally
            {
                Time.timeScale = 1;
            }
            var emergency = SceneManager.GetSceneByName(SceneFlow.EmergencyScene);
            var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
            if (!emergency.isLoaded && !station.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("GuidanceScratch"));
            if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
        }

        // ── 준비 ─────────────────────────────────────────────────────────────

        private static IEnumerator StartShift(Action<EmergencySession> ready)
        {
            var station = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            while (!station.isDone) yield return null;
            var session = SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            while (!session.isDone) yield return null;
            float giveUp = Time.realtimeSinceStartup + 120;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null || EmergencySession.Current.Map == null) && Time.realtimeSinceStartup < giveUp) yield return null;
            Assert.That(EmergencySession.Current, Is.Not.Null, "the shift did not start");
            Assert.That(EmergencySession.Current.Jev.Available, Is.False, "TYPESAFE_API_KEY=off must leave JEV unavailable: nothing may be composed during these tests");
            ready(EmergencySession.Current);
        }

        private static void Resume(EmergencySession shift)
        {
            if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
        }

        private FireHazard Fire(EmergencySession shift, string id, Vector3 at, float intensity)
        {
            var spot = StationWorld.OnNavMesh(at, 2f);
            return new FireHazard(id, spot, "시험", "가방", intensity, shift.Art, parent.transform) { Where = shift.World.Describe(spot) };
        }

        private static void Place(EmergencySession shift, Vector3 position)
        {
            var controller = shift.Player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            shift.Player.transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        /// <summary>Waits until the live navmesh has a hole where an obstacle stands: only then does a run show that guidance does not read it.</summary>
        private static IEnumerator Carved(EmergencySession shift, Vector3 at)
        {
            float giveUp = Time.realtimeSinceStartup + 10;
            while (NavMesh.SamplePosition(at, out _, .05f, NavMesh.AllAreas) && Time.realtimeSinceStartup < giveUp) { Resume(shift); yield return null; }
            Assert.That(NavMesh.SamplePosition(at, out _, .05f, NavMesh.AllAreas), Is.False, "the obstacle never carved the live navmesh, so this run cannot show that guidance ignores it");
        }

        private static Vector3 PointAlong(IReadOnlyList<Vector3> route, float fraction)
        {
            float total = 0;
            for (int i = 1; i < route.Count; i++) total += Vector3.Distance(route[i - 1], route[i]);
            float at = total * fraction;
            for (int i = 1; i < route.Count; i++)
            {
                float length = Vector3.Distance(route[i - 1], route[i]);
                if (at <= length || i == route.Count - 1) return Vector3.Lerp(route[i - 1], route[i], length < .001f ? 0 : Mathf.Clamp01(at / length));
                at -= length;
            }
            return route[0];
        }

        private static void AssertSameRoute(IReadOnlyList<Vector3> expected, IReadOnlyList<Vector3> actual, string why)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), why);
            for (int i = 0; i < expected.Count; i++) Assert.That((actual[i] - expected[i]).sqrMagnitude, Is.LessThan(1e-6f), why + " (point " + i + ")");
        }

        private static float Length(IReadOnlyList<Vector3> route)
        {
            float length = 0;
            for (int i = 1; i < route.Count; i++) length += Vector3.Distance(route[i - 1], route[i]);
            return length;
        }

        /// <summary>A place on the target's storey 14–24 m away (a direct walk), nearest 16 m.</summary>
        private static Vector3? Near(StationWorld world, Vector3 scene, float radius)
        {
            Vector3? best = null;
            float bestScore = float.PositiveInfinity;
            foreach (var point in world.Points.All)
            {
                var d = point.Position - scene;
                float flat = new Vector2(d.x, d.z).magnitude;
                if (Mathf.Abs(d.y) > 2f || flat < radius + 6f || flat > 24f) continue;
                float score = Mathf.Abs(flat - 16f);
                if (score < bestScore) { bestScore = score; best = point.Position; }
            }
            return best;
        }

        /// <summary>Every step of the route is a straight walk on the baked floor (a chord through a wall is not), and it ends on the target's ring.</summary>
        private static void AssertWalkable(StationWorld world, IReadOnlyList<Vector3> route, Hazard target)
        {
            var walk = new List<Vector3>();
            for (int i = 1; i < route.Count; i++)
            {
                bool onFloor = world.Baked.Sample(route[i - 1], .4f, out var a) && Mathf.Abs(a.y - route[i - 1].y) < .3f
                    && world.Baked.Sample(route[i], .4f, out var b) && Mathf.Abs(b.y - route[i].y) < .3f;
                if (!onFloor) continue; // an escalator belt is not floor
                walk.Clear();
                Assert.That(world.Baked.Walk(route[i - 1], route[i], walk), Is.True, "step " + i + " is not a walk on the baked floor");
                Assert.That(Length(walk), Is.LessThan(Vector3.Distance(route[i - 1], route[i]) * 1.15f + .6f), "step " + i + " cuts through a wall or a void instead of following the floor");
            }
            var end = route[route.Count - 1] - target.Scene;
            Assert.That(Mathf.Abs(new Vector2(end.x, end.z).magnitude - target.ApproachRadius), Is.LessThan(3f), "the route must end on the ring where staff stop");
        }

        private static void AssertEscapesThenAvoids(IReadOnlyList<Vector3> route, Func<Vector3, bool> inside, string what)
        {
            bool exited = !inside(route[0]);
            for (int i = 1; i < route.Count; i++)
            {
                float length = Vector3.Distance(route[i - 1], route[i]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / .5f));
                for (int step = 0; step <= steps; step++)
                {
                    var point = Vector3.Lerp(route[i - 1], route[i], (float)step / steps);
                    if (!inside(point)) exited = true;
                    else Assert.That(exited, Is.False, "the route entered " + what + " at " + point.ToString("F1"));
                }
            }
            Assert.That(exited, Is.True, "the route never left " + what);
        }

        // ── 모르는 것은 경로에 나타나지 않고, 알게 되면 즉시 반영된다 ────────────────

        [UnityTest, Timeout(900000)]
        public IEnumerator UnseenObstructionsNeverShapeTheRoute_KnownOnesAlwaysDo()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var world = shift.World;
            var target = Fire(shift, "target", world.Points.Of(PointKind.Office)[0].Position, .3f);
            var origins = new List<Vector3> { world.Points.Of(PointKind.PlatformWait)[0].Position };
            var near = Near(world, target.Scene, target.ApproachRadius);
            if (near.HasValue) origins.Add(near.Value);
            Assume.That(near.HasValue, Is.True, "no place 14–24 m from the target to test a direct walk from");

            var guide = new GuideRoute(world);
            guide.Hazards.Add(target);
            foreach (var origin in origins)
            {
                var baseline = new List<Vector3>();
                Assert.That(guide.Plan(origin, target, baseline), Is.EqualTo(GuideRoute.Outcome.Route), "a route from " + origin.ToString("F0") + " to the target must exist on the station as baked");
                AssertWalkable(world, baseline, target);

                // 아직 모르는 것 전부: 경로 위에 놓여 깎는 불, 세계의 통제 구역, 닫힌 에스컬레이터, 그래프에서 막힌 변.
                var spot = PointAlong(baseline, .5f);
                var unseen = Fire(shift, "unseen-" + origins.IndexOf(origin), spot, .05f);
                HazardRegistry.Add(unseen);
                world.Closed.Add((PointAlong(baseline, .3f), 4f, "아직 모르는 통제 구역"));
                foreach (var escalator in world.Escalators) escalator.Close();
                var node = world.Paths.Graph.Nearest(baseline[0]);
                var goal = world.Paths.Graph.Nearest(baseline[baseline.Count - 1]);
                var probe = world.Paths.Graph.Survey(node, RouteProfile.Evacuation, new NoRules());
                var edges = new List<RouteGraph.Edge>();
                probe.EdgesTo(goal, edges);
                foreach (var edge in edges) world.Paths.Graph.Block(edge, Time.time + 1000f);
                yield return Carved(shift, unseen.Position);

                var again = new List<Vector3>();
                Assert.That(guide.Plan(origin, target, again), Is.EqualTo(GuideRoute.Outcome.Route));
                AssertSameRoute(baseline, again, "a hazard, closure, closed escalator or blocked link the player does not know of changed the route");

                // 알게 된 위험: 경로는 바뀌거나(돌아가거나) 없다고 해야 하고, 지나가서는 안 된다.
                guide.Hazards.Add(unseen);
                var around = new List<Vector3>();
                var outcome = guide.Plan(origin, target, around);
                if (outcome == GuideRoute.Outcome.Route)
                {
                    AssertEscapesThenAvoids(around, unseen.Blocks, "the known hazard");
                    if (!unseen.Blocks(origin))
                        Assert.That(around.SequenceEqual(baseline), Is.False, "the known hazard lies on the route: the route must go round it");
                }
                else Assert.That(outcome, Is.EqualTo(GuideRoute.Outcome.NoRoute));
                guide.Hazards.Remove(unseen);
                unseen.End();
                HazardRegistry.Remove(unseen);

                // 직접 친 통제선(알고 있는 닫힘)도 즉시 반영된다.
                guide.Closures.Add(new GuideRoute.Closure(spot, 4f, null));
                outcome = guide.Plan(origin, target, around);
                if (outcome == GuideRoute.Outcome.Route)
                    AssertEscapesThenAvoids(around, point => Mathf.Abs(point.y - spot.y) <= 3f && new Vector2(point.x - spot.x, point.z - spot.z).magnitude < 4f, "a cordon the staff put up");
                else Assert.That(outcome, Is.EqualTo(GuideRoute.Outcome.NoRoute));
                guide.Closures.Clear();
            }
        }

        private sealed class NoRules : RouteGraph.IRules
        {
            public float Extra(RouteGraph.Edge edge) => 0;
        }

        private sealed class ShutEdges : RouteGraph.IRules
        {
            public readonly HashSet<RouteGraph.Edge> Edges = new HashSet<RouteGraph.Edge>();
            public float Extra(RouteGraph.Edge edge) => Edges.Contains(edge) ? float.PositiveInfinity : 0;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator GraphSurveyReadsNoLiveBlocks_OnlyItsOwnRules()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var world = shift.World;
            var graph = world.Paths.Graph;
            var start = graph.Nearest(world.Points.Of(PointKind.Counter)[0].Position);
            var tree = graph.Survey(start, RouteProfile.Evacuation, new NoRules());
            var goal = world.Points.Of(PointKind.Exit).Select(exit => graph.ByLabel(exit.Id)).FirstOrDefault(node => node != null && tree.Reaches(node));
            Assert.That(goal, Is.Not.Null, "the counter must reach an exit on foot");
            float cost = tree.CostOf(goal);
            var walk = new List<RouteGraph.Edge>();
            tree.EdgesTo(goal, walk);
            Assert.That(walk, Is.Not.Empty);

            // 세계에서 벌어지는 일: 걸어 본 길이 막히고 에스컬레이터가 닫힌다. 살아 있는 탐색은 이를 반영한다.
            foreach (var edge in walk) graph.Block(edge, Time.time + 1000f);
            foreach (var escalator in world.Escalators) escalator.Close();
            var live = graph.Explore(start, RouteProfile.Evacuation, Array.Empty<(Vector3, float)>(), Time.time);
            Assert.That(!live.Reaches(goal) || live.CostOf(goal) > cost + .01f, Is.True, "the live search must feel the blocks, or this test shows nothing");

            var survey = graph.Survey(start, RouteProfile.Evacuation, new NoRules(), tree);
            Assert.That(survey.CostOf(goal), Is.EqualTo(cost), "a survey reads the baked links only");
            var again = new List<RouteGraph.Edge>();
            survey.EdgesTo(goal, again);
            Assert.That(again, Is.EqualTo(walk));

            // 호출자가 닫은 변은 닫힌다.
            var shut = new ShutEdges();
            shut.Edges.Add(walk[walk.Count / 2]);
            var detour = graph.Survey(start, RouteProfile.Evacuation, shut, survey);
            Assert.That(!detour.Reaches(goal) || detour.CostOf(goal) > cost + .01f, Is.True, "a link the planner counts as shut must cost the route");
            detour.EdgesTo(goal, again);
            Assert.That(again, Has.No.Member(walk[walk.Count / 2]));
        }

        // ── 도착과 대상 ──────────────────────────────────────────────────────

        [UnityTest, Timeout(600000)]
        public IEnumerator StandingInsideTheTargetsDanger_IsArrival_NotAFailure()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var world = shift.World;
            var target = Fire(shift, "target", world.Points.Of(PointKind.Office)[0].Position, .6f);
            target.SpreadSmoke(12f);
            var guide = new GuideRoute(world);
            guide.Hazards.Add(target);
            var route = new List<Vector3>();

            var inside = target.Scene + new Vector3(2f, 0, 0);
            Assert.That(target.Blocks(inside), Is.True, "precondition: this spot is inside the fire's danger");
            Assert.That(guide.Plan(inside, target, route), Is.EqualTo(GuideRoute.Outcome.Arrived), "standing in the danger of the place being guided to is being there");
            Assert.That(route, Is.Empty);

            // 다른 알려진 위험 안쪽에서 출발해도 길은 있다: 이미 들어와 있는 자리에서 나가는 길이 길이다.
            var platform = world.Points.Of(PointKind.PlatformWait)[0].Position;
            var other = Fire(shift, "other", platform, .6f);
            other.SpreadSmoke(10f);
            guide.Hazards.Add(other);
            var start = StationWorld.OnNavMesh(platform, 2f);
            Assert.That(other.Blocks(start), Is.True, "precondition: the staff member stands in the other hazard's danger");
            Assert.That(guide.Plan(start, target, route), Is.EqualTo(GuideRoute.Outcome.Route), "being inside one known hazard must not make the way to another vanish");
            Assert.That(route.Count, Is.GreaterThanOrEqualTo(2));
            AssertEscapesThenAvoids(route, other.Blocks, "the hazard the staff started inside");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator StationWideHazardsBlockNothingAndAreNeverWhereGuidanceLeads()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var world = shift.World;
            var office = world.Points.Of(PointKind.Office)[0].Position;
            var target = Fire(shift, "target", world.Points.Of(PointKind.Counter)[0].Position, .3f);
            var outage = new PowerOutageHazard("outage", office, 3);
            var threat = new BombThreatHazard("threat", office, "시험", 2);
            foreach (var hazard in new Hazard[] { outage, threat })
            {
                Assert.That(hazard.Localized, Is.False, "precondition: nothing to walk away from");
                Assert.That(hazard.Blocks(hazard.Position), Is.False, "a power cut or a phoned threat closes no ground");
            }

            var origin = world.Points.Of(PointKind.PlatformWait)[0].Position;
            var guide = new GuideRoute(world);
            guide.Hazards.Add(target);
            var without = new List<Vector3>();
            Assert.That(guide.Plan(origin, target, without), Is.EqualTo(GuideRoute.Outcome.Route));
            guide.Hazards.Add(outage);
            guide.Hazards.Add(threat);
            var with = new List<Vector3>();
            Assert.That(guide.Plan(origin, target, with), Is.EqualTo(GuideRoute.Outcome.Route));
            AssertSameRoute(without, with, "a known station-wide hazard must not close the ground around the office");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EveryHazardsApproachRingLiesOutsideWhatItBlocks()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var at = shift.World.Points.Of(PointKind.Office)[0].Position;
            foreach (float intensity in new[] { .05f, .4f, 1.2f })
            {
                var fire = Fire(shift, "ring-" + intensity, at, intensity);
                for (int stage = 0; stage < 4; stage++)
                {
                    Assert.That(fire.ApproachRadius, Is.GreaterThanOrEqualTo(fire.Clearance + 1f));
                    for (int i = 0; i < 24; i++)
                    {
                        float angle = i * Mathf.PI / 12f;
                        var ring = fire.Scene + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * fire.ApproachRadius;
                        Assert.That(fire.Blocks(ring), Is.False, "staff are guided to a spot the hazard itself blocks (intensity " + intensity + ", smoke " + fire.SmokeRadius.ToString("0") + " m)");
                    }
                    fire.SpreadSmoke(9f);
                }
            }
        }

        // ── 실제 근무: 인지 → 길 → 설정 ─────────────────────────────────────────

        [UnityTest, Timeout(900000)]
        public IEnumerator Session_ShowsTheRouteOnlyOnceTheIncidentIsKnown_IgnoresUnseenHazards_AndFollowsTheSetting()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            Resume(shift);
            var world = shift.World;
            var director = shift.Incidents;
            var worldGuide = shift.GetComponentInChildren<WorldRouteGuide>(true);
            Assert.That(worldGuide, Is.Not.Null);
            GameSettings.ShowRoute = true;

            var fire = Fire(shift, "incident", world.Points.Of(PointKind.Office)[0].Position, .3f);
            Invoke(director, "Register", fire);
            Place(shift, world.Points.Of(PointKind.PlatformWait)[0].Position);
            yield return null;

            shift.RefreshGuide();
            Assert.That(shift.GuideStatus, Is.EqualTo(EmergencySession.GuideState.Unaware), "an incident the staff member has not found must show no route");
            Assert.That(shift.GuidePath, Is.Empty);
            Assert.That(worldGuide.Visible, Is.False);

            Invoke(director, "Know", fire, "시험");
            shift.RefreshGuide();
            Assert.That(shift.GuideStatus, Is.EqualTo(EmergencySession.GuideState.Guiding));
            Assert.That(shift.GuideTarget, Is.SameAs(fire));
            Assert.That(shift.GuidePath.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(worldGuide.Visible, Is.True, "the dotted route must appear on the floor without opening the map");
            var route = shift.GuidePath.ToList();
            var message = shift.GuideMessage;

            // 경로 위에 놓인, 보이지 않는 곳의 위험과 닫힘: 화면은 그대로여야 한다.
            var origin = shift.Player.transform.position;
            Vector3? spot = null;
            foreach (float fraction in new[] { .5f, .6f, .7f, .8f })
            {
                var candidate = PointAlong(route, fraction);
                if (new Vector2(candidate.x - origin.x, candidate.z - origin.z).magnitude >= 16f) { spot = candidate; break; }
            }
            Assert.That(spot.HasValue, Is.True, "the route is too short to put an unseen hazard out of the staff member's sight");
            var unseen = Fire(shift, "unseen", spot.Value, .05f);
            HazardRegistry.Add(unseen);
            world.Closed.Add((PointAlong(route, .3f), 4f, "아직 모르는 통제 구역"));
            foreach (var escalator in world.Escalators) escalator.Close();
            yield return Carved(shift, unseen.Position);
            shift.RefreshGuide();
            Assert.That(shift.GuideStatus, Is.EqualTo(EmergencySession.GuideState.Guiding));
            Assert.That(shift.GuideMessage, Is.EqualTo(message), "what the map says changed because of something the staff member does not know");
            AssertSameRoute(route, shift.GuidePath, "the route changed because of something the staff member does not know");

            // 설정과 반복 갱신이 안내를 바꿀 뿐, 다음 사건·군중 행동의 난수열을 바꾸면 안 된다.
            AssertGuidanceDrawsNoRandomness(world, () =>
            {
                GameSettings.ShowRoute = false;
                Assert.That(shift.GuideStatus, Is.EqualTo(EmergencySession.GuideState.Off));
                Assert.That(shift.GuidePath, Is.Empty);
                Assert.That(worldGuide.Visible, Is.False);
                GameSettings.ShowRoute = true;
                shift.RefreshGuide();
                Assert.That(shift.GuideStatus, Is.EqualTo(EmergencySession.GuideState.Guiding));
                Assert.That(worldGuide.Visible, Is.True);
                AssertSameRoute(route, shift.GuidePath, "turning the setting off and on changed the route");
            });

            // 사고 현장이 정리된 뒤에는 역 전체 위험(정전)을 알아도 사무실로 가라고 하지 않는다.
            fire.End();
            var outage = new PowerOutageHazard("outage", world.Points.Of(PointKind.Office)[0].Position, 3);
            Invoke(director, "Register", outage);
            Invoke(director, "Know", outage, "시험");
            shift.RefreshGuide();
            Assert.That(shift.GuideStatus, Is.EqualTo(EmergencySession.GuideState.NoTarget), "a power cut has no place to walk to");
            Assert.That(shift.GuidePath, Is.Empty);
            Assert.That(worldGuide.Visible, Is.False);
        }

        private sealed class CountingRandom : System.Random
        {
            public int Draws;
            public override int Next() { Draws++; return base.Next(); }
            public override int Next(int maxValue) { Draws++; return base.Next(maxValue); }
            public override int Next(int minValue, int maxValue) { Draws++; return base.Next(minValue, maxValue); }
            public override double NextDouble() { Draws++; return base.NextDouble(); }
            public override void NextBytes(byte[] buffer) { Draws++; base.NextBytes(buffer); }
        }

        private static void AssertGuidanceDrawsNoRandomness(StationWorld world, Action action)
        {
            var field = typeof(StationWorld).GetField("<Random>k__BackingField", Members);
            var original = world.Random;
            var observed = new CountingRandom();
            field.SetValue(world, observed);
            try
            {
                action();
                Assert.That(observed.Draws, Is.Zero, "guidance consumed the incident/crowd random stream");
            }
            finally { field.SetValue(world, original); }
        }

        private static void Invoke(IncidentDirector director, string method, params object[] arguments)
        {
            var member = typeof(IncidentDirector).GetMethod(method, Members);
            Assert.That(member, Is.Not.Null, "IncidentDirector." + method + " is gone: update this test's way of making the staff member know a hazard");
            member.Invoke(director, arguments);
        }
    }
}
#endif
