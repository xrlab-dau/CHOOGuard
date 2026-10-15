using System;
using System.Collections;
using System.Collections.Generic;
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
    // 승객 실시간 판단(2026-09-30): JEV 없는 근무(TYPESAFE_API_KEY=off)에서도 사람들은 지역 규칙으로 움직이고, 사건에 반응하고,
    // 요청 한 건 없이 서 있는 채 굳지 않는다. 실제 JEV 를 쓰는 측정은 게이트웨이(플레이 근무)에서 한다.
    public sealed class CrowdJudgementTests
    {
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
            Time.timeScale = 1;
        }

        private static void Resume(EmergencySession shift)
        {
            if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
        }

        private static IEnumerator Run(float seconds, EmergencySession shift)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) { Resume(shift); yield return null; }
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

        [UnityTest, Timeout(600000)]
        public IEnumerator WithoutJev_CrowdKeepsMovingAndReactsToFireByLocalRules()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var crowd = shift.Crowd;
            var mind = crowd.Mind;
            var scratch = SceneManager.CreateScene("CrowdJudgementScratch");
            Assert.That(shift.Jev.Available, Is.False, "TYPESAFE_API_KEY=off must leave JEV unavailable");
            Assert.That(mind.Usable, Is.False);

            // 1) 사건이 없어도 사람들은 각자 여정대로 움직인다: 일상 판단은 지역 규칙이 활동이 끝날 때 정한다.
            var start = new Dictionary<Passenger, Vector3>();
            foreach (var person in crowd.People) if (!person.Aboard) start[person] = person.transform.position;
            Time.timeScale = 2;
            yield return Run(60, shift);
            int moved = start.Count(pair => pair.Key == null || (pair.Key.transform.position - pair.Value).sqrMagnitude > 9f);
            Assert.That(moved, Is.GreaterThanOrEqualTo(15), "people must keep moving (walking, leaving, riding away) without JEV");
            Assert.That(mind.Metrics.RoutineLocally, Is.GreaterThan(0), "everyday choices come from local rules when JEV is absent");
            Assert.That(mind.Metrics.RoutineByJev, Is.EqualTo(0));
            Assert.That(mind.Metrics.Requests, Is.EqualTo(0), "no request may be built without JEV");
            Assert.That(mind.Metrics.LongestWait, Is.EqualTo(0f), "nobody waits for an answer that will never come");

            // 2) 눈앞의 불: 가까운 사람들이 알아차리고 지역 규칙이 반응 시간 뒤 정한다. 판단이 굳은 채 남지 않는다.
            var witness = crowd.People.FirstOrDefault(p => !p.Aboard && p.Current != Passenger.Activity.Leave && p.Body.Visible);
            Assert.That(witness, Is.Not.Null);
            var spot = StationWorld.OnNavMesh(witness.transform.position + witness.transform.forward * 3f, 2f);
            var fire = new FireHazard("test-fire", spot, "시험용 불", "가방", .45f, shift.Art, shift.transform) { Where = shift.World.Describe(spot) };
            HazardRegistry.Add(fire);
            yield return Run(25, shift);
            Debug.Log("CROWD_JUDGEMENT_NO_JEV " + mind.Metrics.SummaryText(shift.Jev));
            Assert.That(crowd.People.Any(p => p.Noticed.Contains(fire)), "someone next to a burning bag must notice it");
            Assert.That(mind.Metrics.Raised, Is.GreaterThan(0));
            Assert.That(mind.Metrics.UrgentLocally, Is.GreaterThan(0), "the reaction is decided by local rules without JEV");
            Assert.That(mind.Metrics.UrgentByJev, Is.EqualTo(0));
            foreach (var person in crowd.People)
                Assert.That(person.Slot.Waiting, Is.False, "passenger " + person.Number + " is stuck waiting for an answer");

            HazardRegistry.Clear();
            Time.timeScale = 1;
            // 근무 씬(역무원·열차를 쓰는 쪽)을 먼저 내리고 한 프레임 뒤 역 씬을 내린다: 역 씬이 먼저 사라지면 근무 씬의 갱신이 없어진 역무원을 만진다.
            SceneManager.SetActiveScene(scratch);
            yield return SceneManager.UnloadSceneAsync(SceneFlow.EmergencyScene);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(SceneFlow.StationScene);
        }
    }
}
