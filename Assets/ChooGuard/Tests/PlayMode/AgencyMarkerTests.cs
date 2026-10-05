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
        // 출동 상태 줄은 견학에서만 뜬다(#276 안내 수준). 사용자의 안내 설정을 그대로 되돌리려고 원래 값을 그대로 둔다.
        private static readonly string[] GuidancePrefs = { "chooguard.guidance", "chooguard.showRoute.v2", "chooguard.simpleControls" };

        private string savedKey;
        private GameObject parent;
        private readonly Dictionary<string, int?> savedPrefs = new Dictionary<string, int?>();

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            foreach (var pref in GuidancePrefs) savedPrefs[pref] = PlayerPrefs.HasKey(pref) ? PlayerPrefs.GetInt(pref) : (int?)null;
            GameSettings.Guidance = GuidanceLevel.Guided;
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
                foreach (var pref in savedPrefs)
                    if (pref.Value.HasValue) PlayerPrefs.SetInt(pref.Key, pref.Value.Value); else PlayerPrefs.DeleteKey(pref.Key);
                PlayerPrefs.Save();
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

        /// <summary>
        /// 화면 위쪽 출동 상태 줄이 단계를 따라가는지 본다 (#271).
        /// </summary>
        /// <remarks>
        /// 무전은 흐르고 사라지므로 "요청이 반영됐는지" 를 계속 확인할 수 없다 — 그래서 상태 줄이 필요하다.
        ///
        /// 가장 중요한 단정은 **없는 것을 지어내지 않는가** 이다. 팀이 생기기 전에는 좌표가 없으므로 그때의
        /// 상태 줄에 장소 이름이 들어가면 안 된다. 사건 장소를 기관 위치인 양 적는 것이 가장 쉬운 실수라,
        /// 사건 장소 문자열이 들어 있지 않은지까지 본다.
        /// </remarks>
        [UnityTest, Explicit("#271 측정·검수용. -testFilter 로 직접 지정해 돌릴 것."), Timeout(int.MaxValue)]
        public IEnumerator 출동_상태_줄이_단계를_따라간다()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);

            float bootUntil = Time.realtimeSinceStartup + Seconds("CG_AGENCY_BOOT", 150f);
            while ((EmergencySession.Current == null || EmergencySession.Current.Incidents == null
                    || EmergencySession.Current.World == null)
                   && Time.realtimeSinceStartup < bootUntil)
            {
                var booting = EmergencySession.Current;
                if (booting != null && booting.Player != null) Resume(booting);
                yield return null;
            }
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
            Resume(session);
            var director = session.Incidents;

            var spot = StationWorld.OnNavMesh(session.Player.transform.position + session.Player.transform.forward * 6f, 3f);
            var where = session.World.Describe(spot);
            var fire = new FireHazard("agency-status-시험", spot, "시험", "가방", .5f, session.Art, parent.transform) { Where = where };
            Invoke(director, "Register", fire);
            Invoke(director, "Know", fire, "시험");
            yield return null;

            // ① 출동 요청 직후 — 팀이 없다. 소속은 밝히되 위치는 없다고 적어야 한다.
            Invoke(director, "Report", fire);
            yield return null;
            yield return null;
            var called = session.AgencyStatusText;
            Debug.Log("CG_AGENCY_LINE 요청 직후 [" + called + "] " + called.Length + "자");
            Assert.That(called, Does.Contain("소방"), "출동 요청한 기관이 상태 줄에 없습니다.");
            Assert.That(called, Does.Contain("출동 중"), "출동 중 단계가 보이지 않습니다.");
            Assert.That(called, Does.Contain("위치 확인 전"),
                "팀이 생기기 전에는 좌표가 없습니다. '위치 확인 전' 로 적어야 합니다.");
            Assert.That(called, Does.Not.Contain(where),
                "팀이 없는데 사건 장소를 기관 위치처럼 적었습니다: " + where);

            // ② 팀이 와서 현장으로 이동 — 이제 위치가 있다.
            float spawnUntil = Time.realtimeSinceStartup + Seconds("CG_AGENCY_SPAWN", 240f);
            while (Lead(director, Agency.Fire) == null && Time.realtimeSinceStartup < spawnUntil) yield return null;
            Assert.That(Lead(director, Agency.Fire), Is.Not.Null, "소방 팀이 제한 시간 안에 오지 않았습니다.");
            yield return null;
            var walking = session.AgencyStatusText;
            Debug.Log("CG_AGENCY_LINE 이동 중 [" + walking + "] " + walking.Length + "자");
            Assert.That(walking, Does.Contain("이동 중"), "현장 이동 중 단계가 보이지 않습니다.");
            Assert.That(walking, Does.Not.Contain("위치 확인 전"), "팀이 있는데도 위치를 모른다고 적었습니다.");

            // ③ 현장 도착.
            float sceneUntil = Time.realtimeSinceStartup + Seconds("CG_AGENCY_WALK", 180f);
            while (!OnScene(director, Agency.Fire) && Time.realtimeSinceStartup < sceneUntil) yield return null;
            Assert.That(OnScene(director, Agency.Fire), Is.True, "소방이 제한 시간 안에 현장에 닿지 않았습니다.");
            yield return null;
            var onScene = session.AgencyStatusText;
            Debug.Log("CG_AGENCY_LINE 현장 도착 [" + onScene + "] " + onScene.Length + "자");
            Assert.That(onScene, Does.Contain("현장 도착"), "현장 도착 단계가 보이지 않습니다.");
        }

        /// <summary>
        /// #271 의 남은 검수: 두 기관 동시 출동 · 층 이동 · 무전이 사라진 뒤에도 남는지 · 근무 종료 (#271).
        /// </summary>
        /// <remarks>
        /// 한 기관만 보면 줄이 길어질 때 무슨 일이 생기는지 알 수 없다. 둘을 동시에 불러 **각 기관이
        /// 제 단계를 따로 보이는지**와 줄 길이를 잰다.
        ///
        /// 근무 종료는 **단정하지 않고 재기만 한다.** 끝난 뒤 이 줄이 어떻게 보여야 하는지는 아직 정해진
        /// 바가 없다 — 모르는 것을 아는 척 단정으로 박아 두면 나중에 그 단정이 설계를 대신하게 된다.
        /// </remarks>
        [UnityTest, Explicit("#271 검수용. -testFilter 로 직접 지정해 돌릴 것."), Timeout(int.MaxValue)]
        public IEnumerator 두_기관이_동시에_출동해도_각각_단계를_보인다()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);

            float bootUntil = Time.realtimeSinceStartup + Seconds("CG_AGENCY_BOOT", 150f);
            while ((EmergencySession.Current == null || EmergencySession.Current.Incidents == null
                    || EmergencySession.Current.Crowd == null || EmergencySession.Current.Crowd.People.Count == 0)
                   && Time.realtimeSinceStartup < bootUntil)
            {
                var booting = EmergencySession.Current;
                if (booting != null && booting.Player != null) Resume(booting);
                yield return null;
            }
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
            Assert.That(session.Crowd.People.Count, Is.GreaterThan(0), "승객이 없어 난동 사건을 만들 수 없습니다.");
            Resume(session);
            var director = session.Incidents;

            // 소방이 오는 사건과 경찰이 오는 사건을 하나씩. 둘을 동시에 걸어야 줄이 둘이 된다.
            var spot = StationWorld.OnNavMesh(session.Player.transform.position + session.Player.transform.forward * 6f, 3f);
            var fire = new FireHazard("두기관-불", spot, "시험", "가방", .5f, session.Art, parent.transform)
            {
                Where = session.World.Describe(spot),
            };
            var person = session.Crowd.People[0];
            var disturbance = new DisturbanceHazard("두기관-난동", person, 2)
            {
                Where = session.World.Describe(person.transform.position),
            };
            Assert.That(disturbance.Command, Is.EqualTo(Agency.Police), "난동 사건은 경찰이 지휘해야 합니다.");

            foreach (var hazard in new Hazard[] { fire, disturbance })
            {
                Invoke(director, "Register", hazard);
                Invoke(director, "Know", hazard, "시험");
            }
            yield return null;
            foreach (var hazard in new Hazard[] { fire, disturbance }) Invoke(director, "Report", hazard);
            yield return null;
            yield return null;

            // ① 둘 다 아직 팀이 없다 — 줄이 둘이고, 둘 다 위치가 없다고 적어야 한다.
            var called = session.AgencyStatusText;
            var lines = called.Split('\n');
            Debug.Log("CG_AGENCY_TWO 요청 직후 " + lines.Length + "줄 · 가장 긴 줄 "
                      + lines.Max(l => l.Length) + "자\n" + called);
            Assert.That(lines.Length, Is.EqualTo(2), "기관 둘을 불렀는데 줄이 " + lines.Length + "개입니다.");
            Assert.That(lines.All(l => l.Contains("위치 확인 전")), Is.True,
                "팀이 생기기 전인데 위치를 적은 줄이 있습니다:\n" + called);

            // ② 두 팀이 모두 올 때까지 기다리며 층을 따라 본다.
            var floors = new Dictionary<Agency, HashSet<int>>
            {
                [Agency.Fire] = new HashSet<int>(),
                [Agency.Police] = new HashSet<int>(),
            };
            float bothUntil = Time.realtimeSinceStartup + Seconds("CG_AGENCY_SPAWN", 300f);
            while (Time.realtimeSinceStartup < bothUntil)
            {
                bool both = true;
                foreach (var agency in floors.Keys.ToArray())
                {
                    var lead = Lead(director, agency);
                    if (lead == null) { both = false; continue; }
                    floors[agency].Add(MapOverlay.Floor(lead.transform.position));
                }
                if (both) break;
                yield return null;
            }
            foreach (var pair in floors)
                Assert.That(Lead(director, pair.Key), Is.Not.Null, pair.Key + " 팀이 제한 시간 안에 오지 않았습니다.");

            var walking = session.AgencyStatusText;
            var walkingLines = walking.Split('\n');
            Debug.Log("CG_AGENCY_TWO 둘 다 도착 후 " + walkingLines.Length + "줄 · 가장 긴 줄 "
                      + walkingLines.Max(l => l.Length) + "자\n" + walking);
            Assert.That(walkingLines.Length, Is.EqualTo(2), "두 기관이 왔는데 줄이 " + walkingLines.Length + "개입니다.");
            Assert.That(walkingLines.All(l => l.Contains("이동 중") || l.Contains("현장 도착")), Is.True,
                "팀이 있는데 단계가 이동 중도 현장 도착도 아닙니다:\n" + walking);
            Assert.That(walking, Does.Not.Contain("위치 확인 전"), "팀이 둘 다 있는데 위치를 모른다고 적었습니다.");

            // ③ 무전은 흐르고 사라진다. 그 뒤에도 이 줄은 남아야 한다.
            float hold = session.ShiftSeconds + 25f;
            while (session.ShiftSeconds < hold)
            {
                foreach (var agency in floors.Keys.ToArray())
                {
                    var lead = Lead(director, agency);
                    if (lead != null) floors[agency].Add(MapOverlay.Floor(lead.transform.position));
                }
                yield return null;
            }
            var later = session.AgencyStatusText;
            Debug.Log("CG_AGENCY_TWO 25 게임초 뒤\n" + later);
            Assert.That(later, Is.Not.Empty, "무전이 사라진 뒤 상태 줄까지 비었습니다.");
            Assert.That(later, Does.Contain("소방대"), "25초 뒤 소방대가 줄에서 사라졌습니다.");
            Assert.That(later, Does.Contain("철도경찰"), "25초 뒤 철도경찰이 줄에서 사라졌습니다.");

            foreach (var pair in floors)
                Debug.Log("CG_AGENCY_TWO 층 이동 · " + pair.Key + " 가 지난 층 ["
                          + string.Join(",", pair.Value.OrderBy(f => f)) + "]");

            // ④ 근무 종료 — 재기만 한다. 끝난 뒤 이 줄이 어떻게 보여야 하는지는 아직 정해진 바가 없다.
            session.EndShift();
            yield return null;
            yield return null;
            Debug.Log("CG_AGENCY_TWO 근무 종료 직후 [" + session.AgencyStatusText.Replace("\n", " / ") + "]");
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
