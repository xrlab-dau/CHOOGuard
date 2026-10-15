#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Hud;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// 1층 한 지점에서 승객이 얼어붙는 원인을 잰다 (#273). 고치지 않는다 — **무엇이 참인지만** 적는다.
    /// </summary>
    /// <remarks>
    /// <see cref="CrowdOutageTests"/> 와 <see cref="CrowdEvacuationTests"/> 가 `develop` 에서 세 번에 두 번
    /// 실패하고, 얼어붙은 승객은 늘 거의 같은 자리다(x −11.4~−11.9 · y 0.0 · z 22.7~23.4).
    ///
    /// 그 시험들이 찍는 진단에는 **서로 맞지 않는 값들**이 있다:
    /// <c>hasPath=True · status=PathComplete · stopped=False · link=False · planning=False</c> 인데
    /// <c>remaining=Infinity · speed=0.0</c> 다. 길이 있고 멈추라고 한 적도 없는데 남은 거리를 모른다.
    ///
    /// 주의: 진단의 <c>planning=</c> 은 Unity 의 <see cref="NavMeshAgent.pathPending"/> 이 아니라 게임의
    /// <see cref="PersonBody.Planning"/> 이다. <c>remainingDistance</c> 가 Infinity 가 되는 대표적 조건이
    /// <c>pathPending</c> 인데 그 값은 찍히지 않는다 — 그래서 여기서 직접 읽는다.
    ///
    /// 조건은 실패하는 시험과 같게 맞춘다(같은 시드, JEV 응답을 침묵으로). 다르게 맞추면 다른 것을 재게 된다.
    /// </remarks>
    public sealed class FrozenSpotProbeTests
    {
        private const string OutFolder = ".planning/2026-10-05-frozen-spot";

        private string savedKey;

        private const int FramesPerSecond = 30;

        [SetUp]
        public void SetUp()
        {
            // CrowdEvacuationTests 와 **같은 설정**이다. 다르게 맞추면 다른 것을 재게 된다.
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20260930;
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            Time.captureFramerate = 0;
            Time.timeScale = 1;
            HazardRegistry.Clear();
        }

        private static IEnumerator Play(float gameSeconds, EmergencySession shift)
        {
            float until = shift.ShiftSeconds + gameSeconds;
            while (shift.ShiftSeconds < until) yield return null;
        }

        [UnityTest, Explicit("#273 원인 측정용. -testFilter 로 직접 지정해 돌릴 것."), Timeout(int.MaxValue)]
        public IEnumerator 얼어붙은_승객의_상태와_그_자리를_잰다()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);

            float bootUntil = Time.realtimeSinceStartup + Seconds("CG_FROZEN_BOOT", 150f);
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null
                    || EmergencySession.Current.Crowd.People.Count == 0 || EmergencySession.Current.World == null)
                   && Time.realtimeSinceStartup < bootUntil)
            {
                var booting = EmergencySession.Current;
                if (booting != null && booting.Player != null) Resume(booting);
                yield return null;
            }
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
            Assert.That(session.Jev.Available, Is.False, "TYPESAFE_API_KEY=off 이면 JEV 는 쓸 수 없어야 합니다.");
            Resume(session);
            Time.captureFramerate = FramesPerSecond;

            var crowd = session.Crowd;
            // 굳음 판정은 **게임 자신의 것**을 쓴다(CrowdMind.Metrics). 내가 따로 센 적이 있는데 148명 중
            // 93명을 잡았다 - 목적지에 닿아 서 있는 정상 승객까지 센 것이다. 판정이 제품에 있으면 그것을 쓴다.
            var mind = crowd.Mind;

            yield return Play(5, session);

            // 사람이 가장 많이 모인 곳 곁에서 불이 나고, 경보가 울리고, 안내방송이 나온다(피난 시험과 같다).
            var witness = crowd.People
                .Where(p => !p.Aboard && p.Body.Visible && Passenger.Routine(p.Current) && p.Current != Passenger.Activity.InTrain)
                .OrderByDescending(p => crowd.CountNear(p.transform.position, 8, null))
                .First();
            var spot = StationWorld.OnNavMesh(witness.transform.position + witness.transform.forward * 3f, 2f);
            var fire = new FireHazard("frozen-probe-fire", spot, "시험용 불", "가방", .45f, session.Art, session.transform)
            {
                Where = session.World.Describe(spot),
            };
            HazardRegistry.Add(fire);
            crowd.Alert(spot, 400f, fire, null, "the fire alarm bell is ringing");
            int heard = crowd.Announce();
            Debug.Log("CG_FROZEN 안내방송을 들은 사람 " + heard + "명");

            yield return Play(Seconds("CG_FROZEN_PLAY", 60f), session);

            var findings = mind.Metrics.FrozenFindings;
            Debug.Log("CG_FROZEN 게임이 센 굳은 승객 " + mind.Metrics.Frozen + "명 · 기록 " + findings.Count + "줄");
            Assert.That(mind.Metrics.Frozen, Is.GreaterThan(0),
                "이번 실행에서는 아무도 굳지 않았습니다 - 통과하는 쪽에 해당합니다. 다시 돌려 주십시오.");

            // 기록된 "#번호" 로 그 승객을 찾아 상태를 캔다.
            var numbers = new HashSet<int>();
            foreach (var line in findings)
            {
                var match = Regex.Match(line, "^#([0-9]+) ");
                if (match.Success) numbers.Add(int.Parse(match.Groups[1].Value));
            }
            var caught = crowd.People.Where(p => p != null && numbers.Contains(p.Number)).ToList();

            var record = new JObject
            {
                ["at"] = DateTime.UtcNow.ToString("o"),
                ["seed"] = 20260930,
                ["people"] = crowd.People.Count,
                ["frozenCount"] = mind.Metrics.Frozen,
                ["findings"] = new JArray(findings.Select(f => (object)f)),
                ["frozen"] = new JArray(caught.Select(p => (JToken)Describe(session, p))),
            };
            Directory.CreateDirectory(OutFolder);
            var path = Path.Combine(OutFolder,
                "frozen-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(path, record.ToString(), new UTF8Encoding(false));
            Debug.Log("CG_FROZEN 기록 → " + path);
        }

        /// <summary>
        /// 에스컬레이터 링크 **전부**를 같은 방식으로 재서 "한 대만의 문제인가" 를 가른다 (#273).
        /// </summary>
        /// <remarks>
        /// 생짜 <see cref="NavMeshAgent"/> 를 하나 만들어 재면 다른 것을 재게 된다 — 게임은
        /// <c>autoTraverseOffMeshLink = false</c>(PersonBody)로 두고 <c>isOnOffMeshLink</c> 가 참이 될 때
        /// 직접 몰아준다. 그래서 **실제 승객**이 쓰는 모습을 본다.
        ///
        /// 링크마다 두 가지를 센다.
        /// (1) **탄 적이 있나** — <see cref="Escalator.RiderCount"/> 가 한 번이라도 0 보다 컸나.
        /// (2) **입구에서 굳나** — 승강장 2.5 m 안에서 걷는 활동인데 속도 0 이고 링크에 올라타지도 않은
        ///     사람이 몇 초나 서 있었나.
        ///
        /// 둘을 같이 봐야 뜻이 생긴다. 아무도 쓰지 않은 링크는 굳은 사람도 없어서, 멈춤 시간만 보면
        /// '멀쩡하다' 로 잘못 읽힌다.
        /// </remarks>
        [UnityTest, Explicit("#273 범위 측정용. -testFilter 로 직접 지정해 돌릴 것."), Timeout(int.MaxValue)]
        public IEnumerator 모든_에스컬레이터_링크를_같은_방식으로_잰다()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);

            float bootUntil = Time.realtimeSinceStartup + Seconds("CG_FROZEN_BOOT", 150f);
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null
                    || EmergencySession.Current.Crowd.People.Count == 0 || EmergencySession.Current.World == null)
                   && Time.realtimeSinceStartup < bootUntil)
            {
                var booting = EmergencySession.Current;
                if (booting != null && booting.Player != null) Resume(booting);
                yield return null;
            }
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
            Resume(session);
            Time.captureFramerate = FramesPerSecond;

            var crowd = session.Crowd;
            var links = session.World.Escalators.Where(e => e.Entry != null && e.Entry.path != null && e.Entry.path.Length >= 2).ToList();
            Assert.That(links.Count, Is.GreaterThan(0), "에스컬레이터를 찾지 못했습니다.");

            var maxRiders = links.ToDictionary(e => e, e => 0);
            var stallSeconds = links.ToDictionary(e => e, e => 0f);
            var stalled = links.ToDictionary(e => e, e => new HashSet<int>());

            yield return Play(5, session);

            // 피난을 일으켜 링크를 많이 쓰게 한다(굳음이 드러나는 조건과 같다).
            var witness = crowd.People
                .Where(p => !p.Aboard && p.Body.Visible && Passenger.Routine(p.Current) && p.Current != Passenger.Activity.InTrain)
                .OrderByDescending(p => crowd.CountNear(p.transform.position, 8, null))
                .First();
            var spot = StationWorld.OnNavMesh(witness.transform.position + witness.transform.forward * 3f, 2f);
            var fire = new FireHazard("link-sweep-fire", spot, "시험용 불", "가방", .45f, session.Art, session.transform)
            {
                Where = session.World.Describe(spot),
            };
            HazardRegistry.Add(fire);
            crowd.Alert(spot, 400f, fire, null, "the fire alarm bell is ringing");
            crowd.Announce();

            float watch = Seconds("CG_LINK_PLAY", 90f);
            float until = session.ShiftSeconds + watch;
            float last = session.ShiftSeconds;
            while (session.ShiftSeconds < until)
            {
                float step = session.ShiftSeconds - last;
                last = session.ShiftSeconds;
                foreach (var escalator in links)
                {
                    if (escalator.RiderCount > maxRiders[escalator]) maxRiders[escalator] = escalator.RiderCount;
                    foreach (var person in crowd.People)
                    {
                        if (person == null || person.Hurt || person.Hostile) continue;
                        var doing = person.Current;
                        bool moves = doing == Passenger.Activity.Walk || doing == Passenger.Activity.MoveAway
                                     || doing == Passenger.Activity.Evacuate || doing == Passenger.Activity.Leave;
                        if (!moves || person.Body.Riding != null || person.Body.Scripted) continue;
                        if (Vector3.Distance(person.transform.position, escalator.Start) > 2.5f) continue;
                        var agent = person.Body.Agent;
                        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;
                        if (agent.isOnOffMeshLink || !agent.hasPath || agent.velocity.magnitude > .05f) continue;
                        stallSeconds[escalator] += step;
                        stalled[escalator].Add(person.Number);
                    }
                }
                yield return null;
            }

            var results = new JArray();
            foreach (var escalator in links.OrderByDescending(e => stallSeconds[e]))
                results.Add(new JObject
                {
                    ["label"] = escalator.Label,
                    ["up"] = escalator.End.y > escalator.Start.y,
                    ["running"] = escalator.Running,
                    ["entryBarred"] = escalator.EntryBarred,
                    ["maxRiders"] = maxRiders[escalator],
                    ["everRidden"] = maxRiders[escalator] > 0,
                    ["stallSeconds"] = Math.Round(stallSeconds[escalator], 1),
                    ["stalledPeople"] = stalled[escalator].Count,
                    ["startY"] = Math.Round(escalator.Start.y, 2),
                    ["endY"] = Math.Round(escalator.End.y, 2),
                });

            var record = new JObject
            {
                ["at"] = DateTime.UtcNow.ToString("o"),
                ["seed"] = 20260930,
                ["watchGameSeconds"] = watch,
                ["escalators"] = links.Count,
                ["results"] = results,
            };
            Directory.CreateDirectory(OutFolder);
            var path = Path.Combine(OutFolder,
                "links-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(path, record.ToString(), new UTF8Encoding(false));
            Debug.Log("CG_LINKS 기록 → " + path);
            foreach (var row in results)
                Debug.Log("CG_LINKS " + row["label"] + " · " + (((bool)row["up"]) ? "올라감" : "내려감")
                          + " · 탄 적 " + row["everRidden"] + "(최대 " + row["maxRiders"] + "명)"
                          + " · 입구 멈춤 " + row["stallSeconds"] + "초 / " + row["stalledPeople"] + "명");
        }

        /// <summary>
        /// 멈추는 한 대와 멀쩡한 링크들의 **차이**를 잰다 (#273). 왜 그 한 대만인가.
        /// </summary>
        /// <remarks>
        /// 에이전트가 링크에 올라타려면 링크 시작점이 자기가 선 navmesh 폴리곤에 닿아 있어야 한다. 그래서
        /// 시작·끝점이 **navmesh 위에 있는지**(얼마나 벗어났는지)와 양쪽 승강장이 길로 이어지는지를 본다.
        ///
        /// 승객을 쓰지 않는다 — 여기서는 '누가 쓰나' 가 아니라 '링크가 어떻게 놓여 있나' 를 재기 때문이다.
        /// 사람이 없어도 답이 나와야 한다.
        /// </remarks>
        [UnityTest, Explicit("#273 비교 측정용. -testFilter 로 직접 지정해 돌릴 것."), Timeout(int.MaxValue)]
        public IEnumerator 링크마다_시작점과_끝점이_어떻게_놓였는지_비교한다()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);

            float bootUntil = Time.realtimeSinceStartup + Seconds("CG_FROZEN_BOOT", 150f);
            while ((EmergencySession.Current == null || EmergencySession.Current.World == null
                    || EmergencySession.Current.World.Escalators.Count == 0)
                   && Time.realtimeSinceStartup < bootUntil)
            {
                var booting = EmergencySession.Current;
                if (booting != null && booting.Player != null) Resume(booting);
                yield return null;
            }
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
            var links = session.World.Escalators.Where(e => e.Entry != null && e.Entry.path != null && e.Entry.path.Length >= 2).ToList();
            Assert.That(links.Count, Is.GreaterThan(0), "에스컬레이터를 찾지 못했습니다.");

            var route = new NavMeshPath();
            var results = new JArray();
            foreach (var escalator in links)
            {
                var row = new JObject
                {
                    ["label"] = escalator.Label,
                    ["up"] = escalator.End.y > escalator.Start.y,
                    ["running"] = escalator.Running,
                    ["entryBarred"] = escalator.EntryBarred,
                    ["pathPoints"] = escalator.Entry.path.Length,
                    ["lengthMetres"] = Math.Round(Vector3.Distance(escalator.Start, escalator.End), 2),
                    ["riseMetres"] = Math.Round(escalator.End.y - escalator.Start.y, 2),
                };

                // 링크 양끝이 navmesh 위에 있나. 벗어나 있으면 에이전트가 올라탈 수 없다.
                foreach (var pair in new[] { ("start", escalator.Start), ("end", escalator.End) })
                {
                    var node = new JObject();
                    if (NavMesh.SamplePosition(pair.Item2, out var on, 3f, NavMesh.AllAreas))
                    {
                        node["onNavMesh"] = true;
                        node["offsetMetres"] = Math.Round(Vector3.Distance(on.position, pair.Item2), 3);
                        node["verticalOffset"] = Math.Round(on.position.y - pair.Item2.y, 3);
                        node["areaMask"] = on.mask;
                    }
                    else
                    {
                        node["onNavMesh"] = false;
                        node["offsetMetres"] = (JToken)JValue.CreateNull();
                    }
                    node["y"] = Math.Round(pair.Item2.y, 2);
                    row[pair.Item1] = node;
                }

                // 양끝이 길로 이어지나(링크를 빼고 걸어서 갈 수 있나와는 다른 질문 - 링크를 포함한 경로다).
                row["pathAcross"] = NavMesh.CalculatePath(escalator.Start, escalator.End, NavMesh.AllAreas, route)
                    ? new JObject { ["status"] = route.status.ToString(), ["corners"] = route.corners.Length }
                    : new JObject { ["status"] = "계산 실패", ["corners"] = 0 };

                results.Add(row);
            }

            var record = new JObject
            {
                ["at"] = DateTime.UtcNow.ToString("o"),
                ["escalators"] = links.Count,
                ["results"] = results,
            };
            Directory.CreateDirectory(OutFolder);
            var path = Path.Combine(OutFolder,
                "linkgeometry-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(path, record.ToString(), new UTF8Encoding(false));
            Debug.Log("CG_GEOM 기록 → " + path);
            foreach (var row in results)
                Debug.Log("CG_GEOM " + row["label"] + " · " + (((bool)row["up"]) ? "올라감" : "내려감")
                          + " · 시작 navmesh " + row["start"]["onNavMesh"] + "(" + row["start"]["offsetMetres"] + "m)"
                          + " · 끝 navmesh " + row["end"]["onNavMesh"] + "(" + row["end"]["offsetMetres"] + "m)"
                          + " · 길이 " + row["lengthMetres"] + "m 상승 " + row["riseMetres"] + "m"
                          + " · 경로 " + row["pathAcross"]["status"]);
        }

        /// <summary>
        /// 멈춤이 **벨트 포화와 함께 움직이는지** 시계열로 본다 (#273). 혼잡 설명을 가른다.
        /// </summary>
        /// <remarks>
        /// 앞 측정으로 기하 차이가 없음이 드러났고, 남은 설명은 혼잡이다: 탑승자는 앞사람이 Spacing(1.1 m)을
        /// 지날 때까지 기다리고 벨트는 0.5 m/s 다 — 사람당 2.2 초, 16명이면 마지막 사람이 약 35 초를 기다린다.
        /// 굳음 판정선은 20 초다.
        ///
        /// **반증 조건을 먼저 적는다**: 탑승자가 0명인데도 입구에서 멈춰 있는 구간이 길게 나오면 혼잡
        /// 설명은 깨진다. 그러면 사람이 없는데도 못 올라타는 것이므로 다른 원인이다.
        ///
        /// 0.5 게임초마다 링크별로 (탑승자 수 · 2.5 m 안 이동 중인 사람 · 그중 멈춘 사람)을 적는다.
        /// </remarks>
        [UnityTest, Explicit("#273 혼잡 가설 검증용. -testFilter 로 직접 지정해 돌릴 것."), Timeout(int.MaxValue)]
        public IEnumerator 멈춤이_벨트_포화와_함께_움직이는지_본다()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);

            float bootUntil = Time.realtimeSinceStartup + Seconds("CG_FROZEN_BOOT", 150f);
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null
                    || EmergencySession.Current.Crowd.People.Count == 0 || EmergencySession.Current.World == null)
                   && Time.realtimeSinceStartup < bootUntil)
            {
                var booting = EmergencySession.Current;
                if (booting != null && booting.Player != null) Resume(booting);
                yield return null;
            }
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
            Resume(session);
            Time.captureFramerate = FramesPerSecond;

            var crowd = session.Crowd;
            var links = session.World.Escalators.Where(e => e.Entry != null && e.Entry.path != null && e.Entry.path.Length >= 2).ToList();
            var series = links.ToDictionary(e => e, e => new JArray());

            yield return Play(5, session);

            var witness = crowd.People
                .Where(p => !p.Aboard && p.Body.Visible && Passenger.Routine(p.Current) && p.Current != Passenger.Activity.InTrain)
                .OrderByDescending(p => crowd.CountNear(p.transform.position, 8, null))
                .First();
            var spot = StationWorld.OnNavMesh(witness.transform.position + witness.transform.forward * 3f, 2f);
            var fire = new FireHazard("congestion-fire", spot, "시험용 불", "가방", .45f, session.Art, session.transform)
            {
                Where = session.World.Describe(spot),
            };
            HazardRegistry.Add(fire);
            crowd.Alert(spot, 400f, fire, null, "the fire alarm bell is ringing");
            crowd.Announce();

            float watch = Seconds("CG_LINK_PLAY", 90f);
            float until = session.ShiftSeconds + watch;
            float nextSample = session.ShiftSeconds;
            while (session.ShiftSeconds < until)
            {
                if (session.ShiftSeconds >= nextSample)
                {
                    nextSample = session.ShiftSeconds + .5f;
                    foreach (var escalator in links)
                    {
                        int near = 0, stuck = 0;
                        foreach (var person in crowd.People)
                        {
                            if (person == null || person.Hurt || person.Hostile) continue;
                            var doing = person.Current;
                            bool moves = doing == Passenger.Activity.Walk || doing == Passenger.Activity.MoveAway
                                         || doing == Passenger.Activity.Evacuate || doing == Passenger.Activity.Leave;
                            if (!moves || person.Body.Riding != null || person.Body.Scripted) continue;
                            if (Vector3.Distance(person.transform.position, escalator.Start) > 2.5f) continue;
                            near++;
                            var agent = person.Body.Agent;
                            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) continue;
                            if (agent.isOnOffMeshLink || !agent.hasPath || agent.velocity.magnitude > .05f) continue;
                            stuck++;
                        }
                        if (near == 0 && escalator.RiderCount == 0) continue;   // 아무 일도 없던 순간은 적지 않는다
                        series[escalator].Add(new JObject
                        {
                            ["t"] = Math.Round(session.ShiftSeconds, 1),
                            ["riders"] = escalator.RiderCount,
                            ["near"] = near,
                            ["stuck"] = stuck,
                        });
                    }
                }
                yield return null;
            }

            var results = new JArray();
            foreach (var escalator in links.OrderByDescending(e => series[e].Count))
            {
                var rows = series[escalator];
                if (rows.Count == 0) continue;
                int stuckRows = rows.Count(r => (int)r["stuck"] > 0);
                int stuckWithNoRiders = rows.Count(r => (int)r["stuck"] > 0 && (int)r["riders"] == 0);
                results.Add(new JObject
                {
                    ["label"] = escalator.Label,
                    ["samples"] = rows.Count,
                    ["maxRiders"] = rows.Max(r => (int)r["riders"]),
                    ["maxStuck"] = rows.Max(r => (int)r["stuck"]),
                    ["rowsWithStuck"] = stuckRows,
                    ["rowsWithStuckAndNoRiders"] = stuckWithNoRiders,
                    ["series"] = rows,
                });
                Debug.Log("CG_JAM " + escalator.Label + " · 표본 " + rows.Count
                          + " · 최대 탑승 " + rows.Max(r => (int)r["riders"])
                          + " · 최대 멈춤 " + rows.Max(r => (int)r["stuck"])
                          + " · 멈춤 있던 표본 " + stuckRows
                          + " · 그중 탑승 0명 " + stuckWithNoRiders);
            }

            var record = new JObject
            {
                ["at"] = DateTime.UtcNow.ToString("o"),
                ["seed"] = 20260930,
                ["beltSpeed"] = Escalator.BeltSpeed,
                ["spacing"] = Escalator.Spacing,
                ["secondsPerBoarding"] = Math.Round(Escalator.Spacing / Escalator.BeltSpeed, 2),
                ["results"] = results,
            };
            Directory.CreateDirectory(OutFolder);
            var path = Path.Combine(OutFolder,
                "congestion-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.WriteAllText(path, record.ToString(), new UTF8Encoding(false));
            Debug.Log("CG_JAM 기록 → " + path);
        }

        /// <summary>굳은 승객 하나의 상태와 그 자리에 무엇이 있는지.</summary>
        private static JObject Describe(EmergencySession session, Passenger person)
        {
            var agent = person.Body.Agent;
            var here = person.transform.position;

            // navmesh 위에 올라 있지 않거나 꺼진 에이전트에서 remainingDistance 를 읽으면 Unity 가 오류를
            // 뱉는다("GetRemainingDistance can only be called on an active agent"). 첫 실행에서 실제로 그
            // 오류 때문에 시험이 실패했다 - 그래서 읽기 전에 살아 있는지 본다.
            bool live = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
            var state = new JObject
            {
                ["live"] = live,
                ["isOnNavMesh"] = agent != null && agent.isOnNavMesh,
                ["enabled"] = agent != null && agent.isActiveAndEnabled,
            };
            if (live)
            {
                // 시험 진단이 찍지 않는 pathPending 과 꺾임점 수가 핵심이다.
                state["pathPending"] = agent.pathPending;
                state["hasPath"] = agent.hasPath;
                state["pathStatus"] = agent.pathStatus.ToString();
                state["corners"] = agent.path != null ? agent.path.corners.Length : -1;
                state["remainingDistance"] = float.IsInfinity(agent.remainingDistance)
                    ? "Infinity" : (JToken)Math.Round(agent.remainingDistance, 2);
                state["stoppingDistance"] = Math.Round(agent.stoppingDistance, 2);
                state["isStopped"] = agent.isStopped;
                state["isOnOffMeshLink"] = agent.isOnOffMeshLink;
                state["speedSetting"] = Math.Round(agent.speed, 2);
                state["velocity"] = Math.Round(agent.velocity.magnitude, 2);
                state["desiredVelocity"] = Math.Round(agent.desiredVelocity.magnitude, 2);
                state["updatePosition"] = agent.updatePosition;
                state["areaMask"] = agent.areaMask;
                state["destination"] = Round(agent.destination);
                state["nextPosition"] = Round(agent.nextPosition);
                state["destinationToGoal"] = Math.Round(Vector3.Distance(agent.destination, person.Body.Goal), 2);
            }

            return new JObject
            {
                ["number"] = person.Number,
                ["activity"] = person.Current.ToString(),
                ["at"] = Round(here),
                ["floor"] = MapOverlay.Floor(here),
                ["goal"] = Round(person.Body.Goal),
                ["goalFloor"] = MapOverlay.Floor(person.Body.Goal),
                ["metresToGoal"] = Math.Round(Vector3.Distance(here, person.Body.Goal), 2),
                ["onNavMesh"] = person.Body.OnNavMesh,
                ["planning"] = person.Body.Planning,
                ["walkState"] = person.WalkState(),
                ["agent"] = state,
                ["spot"] = Spot(session, here),
            };
        }

        /// <summary>그 자리에 무엇이 있는지 - 바닥 콜라이더, navmesh 구역, 가장 가까운 오프메시 링크 입구.</summary>
        private static JObject Spot(EmergencySession session, Vector3 here)
        {
            var spot = new JObject
            {
                ["under"] = Physics.Raycast(here + Vector3.up * 2f, Vector3.down, out var floor, 6f, ~0, QueryTriggerInteraction.Ignore)
                    ? floor.collider.name : (JToken)JValue.CreateNull(),
                ["navmeshAreaMask"] = NavMesh.SamplePosition(here, out var on, 1f, NavMesh.AllAreas) ? on.mask : 0,
                ["navmeshOffset"] = NavMesh.SamplePosition(here, out var near, 2f, NavMesh.AllAreas)
                    ? (JToken)Math.Round(Vector3.Distance(near.position, here), 2) : JValue.CreateNull(),
                ["area"] = session.World.Area(here),
                ["describe"] = session.World.Describe(here),
            };

            // 가장 가까운 오프메시 링크 입구 - #272 와 같은 계열인지 가른다.
            Escalator nearest = null;
            float best = float.PositiveInfinity;
            string which = null;
            foreach (var escalator in session.World.Escalators)
            {
                foreach (var pair in new[] { ("start", escalator.Start), ("end", escalator.End) })
                {
                    float distance = Vector3.Distance(pair.Item2, here);
                    if (distance >= best) continue;
                    best = distance;
                    nearest = escalator;
                    which = pair.Item1;
                }
            }
            spot["nearestLink"] = nearest == null ? (JToken)JValue.CreateNull()
                : new JObject { ["label"] = nearest.Label, ["end"] = which, ["metres"] = Math.Round(best, 2) };
            return spot;
        }


        private static JArray Round(Vector3 value) =>
            new JArray((object)Math.Round(value.x, 2), Math.Round(value.y, 2), Math.Round(value.z, 2));

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
