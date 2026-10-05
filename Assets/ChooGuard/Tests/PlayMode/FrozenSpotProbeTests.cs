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
