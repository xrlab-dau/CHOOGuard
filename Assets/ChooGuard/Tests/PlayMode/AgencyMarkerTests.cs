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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// 출동한 기관이 현장으로 **걸어오는 동안** 지도 표식이 그 자리를 따라가는지 본다 (#271).
    /// </summary>
    /// <remarks>
    /// 지금 코드는 표식을 두 번만 건다 — 팀을 만들 때(<c>SpawnTeam</c>)와 현장에 닿았을 때
    /// (<c>OnResponderArrived</c>). 그 사이 이동은 반영되지 않는다. **그러므로 이 시험은 지금 실패하는
    /// 것이 맞다.** 고친 뒤 통과해야 뜻이 생긴다 — 좋은 경우와 나쁜 경우 모두에서 통과하는 검사는
    /// 아무것도 말해 주지 않는다.
    ///
    /// 두 가지를 따로 단정한다.
    /// (1) 팀이 생기기 **전에는 표식이 없어야 한다.** 역 밖 출발지는 구현돼 있지 않으므로, 좌표가
    ///     없는데 무언가를 찍으면 그것은 지어낸 위치다(#271 이 명시적으로 금지한다).
    /// (2) 팀이 걷는 동안 표식이 **실제로 움직여야 하고**, 그 자리가 선두 대원의 자리여야 한다.
    ///
    /// JEV 를 끄고 돌린다. 끄면 디렉터가 아무것도 합성하지 않으므로 움직이는 기관은 이 시험이 부른
    /// 하나뿐이다 — 무엇을 봤는지 흐려지지 않는다. 사건은 시험이 직접 만든다(규칙 시험의 관례,
    /// <see cref="Issue256NavigationSmokeTests"/> 와 같다).
    ///
    /// 디렉터의 private 멤버와 지도의 표식 보관함은 리플렉션으로 닿는다. 표식을 **읽는 공개 경로가
    /// 없기 때문**이고, 시험이 보는 것은 "지도에 무엇이 전달됐는가" — 플레이어가 보는 것과 같다.
    /// 못 찾은 멤버는 추측하지 않고 실패로 보고한다.
    /// </remarks>
    public sealed class AgencyMarkerTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private string savedKey;
        private GameObject parent;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20260930;
            parent = new GameObject("기관 표식 시험 위험");
            UnityEngine.Object.DontDestroyOnLoad(parent);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
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
            SceneManager.SetActiveScene(SceneManager.CreateScene("AgencyMarkerScratch"));
            if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
        }

        [UnityTest, Explicit("#271 측정·검수용. -testFilter 로 직접 지정해 돌릴 것."), Timeout(int.MaxValue)]
        public IEnumerator 기관이_현장으로_오는_동안_지도_표식이_따라간다()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);

            float bootUntil = Time.realtimeSinceStartup + Seconds("CG_AGENCY_BOOT", 150f);
            while ((EmergencySession.Current == null || EmergencySession.Current.Incidents == null
                    || EmergencySession.Current.Map == null || EmergencySession.Current.World == null)
                   && Time.realtimeSinceStartup < bootUntil)
            {
                var booting = EmergencySession.Current;
                if (booting != null && booting.Player != null) Resume(booting);
                yield return null;
            }
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
            Assert.That(session.Incidents, Is.Not.Null, "디렉터가 준비되지 않았습니다.");
            Assert.That(session.Jev.Available, Is.False,
                "TYPESAFE_API_KEY=off 이면 JEV 는 쓸 수 없어야 합니다: 이 무대에서는 아무것도 합성되지 않아야 합니다.");
            Resume(session);

            var director = session.Incidents;

            // 소방이 오는 사건을 하나 만든다. 역무원 앞 바닥에 둔다 — 팀이 입구에서 현장까지 실제로
            // 걸어야 이동 중 표식을 볼 수 있다.
            var spot = StationWorld.OnNavMesh(session.Player.transform.position + session.Player.transform.forward * 6f, 3f);
            var fire = new FireHazard("agency-marker-시험", spot, "시험", "가방", .5f, session.Art, parent.transform)
            {
                Where = session.World.Describe(spot),
            };
            Assert.That(fire.Dispatch.Contains(Agency.Fire), Is.True, "이 시험은 소방이 오는 사건을 전제로 합니다.");

            Invoke(director, "Register", fire);
            Invoke(director, "Know", fire, "시험");
            yield return null;

            // ① 팀이 생기기 전에는 표식이 없어야 한다. 역 밖 출발지는 구현돼 있지 않으므로 이 시점의
            //    좌표는 **존재하지 않는다** — 무언가 찍혀 있다면 지어낸 것이다.
            Invoke(director, "Report", fire);
            yield return null;
            Assert.That(Marker(session, Agency.Fire), Is.Null,
                "출동 요청 직후에는 팀이 아직 없습니다. 이때 표식을 찍으면 없는 위치를 지어내는 것입니다.");

            // ② 팀이 생길 때까지 기다린다(Teams.Delay 만큼 걸린다).
            float spawnUntil = Time.realtimeSinceStartup + Seconds("CG_AGENCY_SPAWN", 240f);
            Vector3? first = null;
            while (first == null && Time.realtimeSinceStartup < spawnUntil)
            {
                first = Marker(session, Agency.Fire);
                yield return null;
            }
            Assert.That(first, Is.Not.Null, "소방 팀이 제한 시간 안에 오지 않아 이동을 볼 수 없었습니다.");

            // ③ 걷는 동안 표식을 따라 본다. 현장에 닿으면 멈춘다.
            var seen = new List<Vector3> { first.Value };
            float walkUntil = Time.realtimeSinceStartup + Seconds("CG_AGENCY_WALK", 180f);
            const float SampleEvery = .5f;
            float since = 0;
            while (Time.realtimeSinceStartup < walkUntil && !OnScene(director, Agency.Fire))
            {
                since += Time.deltaTime;
                if (since >= SampleEvery)
                {
                    since = 0;
                    var now = Marker(session, Agency.Fire);
                    if (now.HasValue) seen.Add(now.Value);
                }
                yield return null;
            }

            float moved = seen.Max(p => Vector3.Distance(p, seen[0]));
            var lead = Lead(director, Agency.Fire);
            float gap = lead == null ? -1 : Vector3.Distance(seen[seen.Count - 1], lead.transform.position);
            Debug.Log("CG_AGENCY 표식 표본 " + seen.Count + "개 · 처음에서 가장 멀어진 거리 " + moved.ToString("0.00")
                      + "m · 마지막 표식과 선두 대원의 거리 " + gap.ToString("0.00")
                      + "m · 현장 도착 " + OnScene(director, Agency.Fire));

            // 팀은 입구에서 현장까지 걷는다. 표식이 따라간다면 처음 자리에서 눈에 띄게 멀어져야 한다.
            Assert.That(moved, Is.GreaterThan(3f),
                "표식이 움직이지 않았습니다(" + moved.ToString("0.00") + "m). 이동 중 좌표가 반영되지 않습니다.");
            // 그리고 그 자리는 선두 대원의 자리여야 한다 — 다른 무언가를 따라가면 안 된다.
            Assert.That(gap, Is.LessThan(2f),
                "표식이 선두 대원의 자리와 " + gap.ToString("0.00") + "m 떨어져 있습니다.");
        }

        // ── 거들기 ──────────────────────────────────────────────────────────

        /// <summary>지도에 전달된 표식 좌표. 공개 조회 경로가 없어 보관함을 직접 읽는다.</summary>
        private static Vector3? Marker(EmergencySession session, Agency agency)
        {
            var field = typeof(MapOverlay).GetField("sources", Members);
            Assert.That(field, Is.Not.Null, "MapOverlay 의 표식 보관함을 찾지 못했습니다.");
            if (!(field.GetValue(session.Map) is IDictionary table)) return null;
            var key = "agency-" + agency;
            foreach (DictionaryEntry entry in table)
            {
                if (!key.Equals(entry.Key)) continue;
                var value = entry.Value;
                var item = value.GetType().GetField("Item1");
                return item != null ? (Vector3)item.GetValue(value) : (Vector3?)null;
            }
            return null;
        }

        private static Responder Lead(IncidentDirector director, Agency agency) =>
            Responders(director).FirstOrDefault(r => r != null && r.Lead && r.Agency == agency);

        private static bool OnScene(IncidentDirector director, Agency agency)
        {
            var lead = Lead(director, agency);
            return lead != null && lead.OnScene;
        }

        private static IEnumerable<Responder> Responders(IncidentDirector director)
        {
            var field = typeof(IncidentDirector).GetField("responders", Members);
            Assert.That(field, Is.Not.Null, "IncidentDirector 의 대원 목록을 찾지 못했습니다.");
            return field.GetValue(director) as IEnumerable<Responder> ?? Enumerable.Empty<Responder>();
        }

        private static void Invoke(IncidentDirector director, string name, params object[] arguments)
        {
            var method = typeof(IncidentDirector).GetMethod(name, Members);
            Assert.That(method, Is.Not.Null, "IncidentDirector." + name + " 을 찾지 못했습니다.");
            try { method.Invoke(director, arguments); }
            catch (TargetInvocationException problem) { throw problem.InnerException ?? problem; }
        }

        // SetExternalInputMode(true) 는 Pause() 를 부른다 — Resume(false) 로 풀지 않으면 역무원이 얼어붙는다.
        private static void Resume(EmergencySession shift)
        {
            if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
        }

        private static float Seconds(string variable, float fallback) =>
            float.TryParse(Environment.GetEnvironmentVariable(variable), out var parsed) && parsed > 0 ? parsed : fallback;
    }
}
#endif
