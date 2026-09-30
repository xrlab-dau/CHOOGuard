using System;
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // 전원 대피(2026-09-30): 화재·비상벨·안내방송으로 역 안 모두가 나가야 할 때, 한 사람도 20 초 넘게 제자리에서 굳지 않는다.
    // 굳음은 CrowdMind 의 감시가 센다(걷기·물러서기·나가기·대피 중 0.5 m 도 못 움직인 채 20 초, 목적지 3 m 안은 제외).
    // 프레임 시간이 짧은 배치 실행에서도 길 안내 지연이 가려지지 않도록 게임 1초를 30 프레임으로 고정한다(길찾기 예산은 프레임 단위).
    // 828e30b3 에서는 1층 승객이 에스컬레이터 앞에서 굳고 일부는 길 요청이 밀려 굳었다.
    public sealed class CrowdEvacuationTests
    {
        private const int FramesPerSecond = 30;
        private string savedKey;

        [SetUp]
        public void SetUp()
        {
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

        private static void Resume(EmergencySession shift)
        {
            if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
        }

        private static IEnumerator Play(float gameSeconds, EmergencySession shift)
        {
            float until = Time.time + gameSeconds;
            while (Time.time < until) { Resume(shift); yield return null; }
        }

        private static IEnumerator StartShift(Action<EmergencySession> ready)
        {
            var station = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            while (!station.isDone) yield return null;
            var session = SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            while (!session.isDone) yield return null;
            float giveUp = Time.realtimeSinceStartup + 120;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null || EmergencySession.Current.Crowd.People.Count == 0) && Time.realtimeSinceStartup < giveUp) yield return null;
            Assert.That(EmergencySession.Current, Is.Not.Null, "the shift did not start");
            ready(EmergencySession.Current);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator FullEvacuation_NobodyStandsFrozenForSixtySeconds()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var crowd = shift.Crowd;
            var mind = crowd.Mind;
            Assert.That(shift.Jev.Available, Is.False, "TYPESAFE_API_KEY=off must leave JEV unavailable");
            Time.captureFramerate = FramesPerSecond;

            // 근무 시작 때 모두가 한꺼번에 첫 길을 묻는 것이 가라앉은 뒤에 시작한다.
            yield return Play(5, shift);

            // 사람이 가장 많이 모인 곳 곁에서 불이 나고, 비상벨이 울리고, 안내방송이 나온다.
            var witness = crowd.People
                .Where(p => !p.Aboard && p.Body.Visible && Passenger.Routine(p.Current) && p.Current != Passenger.Activity.InTrain)
                .OrderByDescending(p => crowd.CountNear(p.transform.position, 8, null))
                .First();
            var spot = StationWorld.OnNavMesh(witness.transform.position + witness.transform.forward * 3f, 2f);
            var fire = new FireHazard("evacuation-fire", spot, "시험용 불", "가방", .45f, shift.Art, shift.transform) { Where = shift.World.Describe(spot) };
            HazardRegistry.Add(fire);
            crowd.Alert(spot, 400f, fire, null, "the fire alarm bell is ringing");
            int heard = crowd.Announce();
            Assert.That(heard, Is.GreaterThan(30), "the announcement must reach the crowd");

            yield return Play(10, shift);
            int evacuating = crowd.People.Count(p => p.Current == Passenger.Activity.Evacuate);
            Assert.That(evacuating, Is.GreaterThanOrEqualTo(20), "a full evacuation must be under way ten seconds after the announcement");

            yield return Play(50, shift);
            Debug.Log("CROWD_EVACUATION frozen=" + mind.Metrics.Frozen + " evacuated=" + crowd.Evacuated + " " + mind.Metrics.SummaryText(shift.Jev));
            Assert.That(mind.Metrics.Frozen, Is.EqualTo(0), "frozen evacuees:\n" + string.Join("\n", mind.Metrics.FrozenFindings));

            Time.captureFramerate = 0;
            HazardRegistry.Clear();
            // 근무 씬(역무원·열차를 쓰는 쪽)을 먼저 내리고 한 프레임 뒤 역 씬을 내린다: 역 씬이 먼저 사라지면 근무 씬의 갱신이 없어진 역무원을 만진다.
            var scratch = SceneManager.CreateScene("CrowdEvacuationScratch");
            SceneManager.SetActiveScene(scratch);
            yield return SceneManager.UnloadSceneAsync(SceneFlow.EmergencyScene);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(SceneFlow.StationScene);
        }
    }
}
