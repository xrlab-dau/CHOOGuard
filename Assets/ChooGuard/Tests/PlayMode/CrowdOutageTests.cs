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
    // JEV 가 답하지 않는 동안(접속 끊김): 판단을 기다리는 사람이 서서 굳지 않고 여정 목적대로 걷는다. 지역 규칙이 아니라(그건 키 없음·off 전용)
    // 근무 시작 때 사람들이 걷는 여정 그대로다. 요청은 보내지만 전부 답 없이 돌아오는 경로를 CrowdMind.Transport 로 근무를 적재하기 전에 끼워 넣는다(진짜 서버로는 아무것도 나가지 않는다).
    public sealed class CrowdOutageTests
    {
        private string savedKey;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            // 이 기기가 이미 거절로 기억한 키가 아니도록 매번 새 가짜 키를 쓴다(서버로는 나가지 않는다).
            Environment.SetEnvironmentVariable(JevKey.Variable, "outage-test-key-" + Guid.NewGuid().ToString("N"));
            CrowdMind.Transport = (purpose, state, questions, done, lane) => Silence(done);
            EmergencySession.NextSeed = 20260930;
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            CrowdMind.Transport = null;
            Time.captureFramerate = 0;
            Time.timeScale = 1;
            HazardRegistry.Clear();
        }

        private static IEnumerator Silence(Action<Dictionary<string, JevAnswer>> done)
        {
            yield return new WaitForSeconds(1f);
            done(null);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator JevNotAnswering_PeopleGoOnWithTheirTripsAndNobodyStandsFrozen()
        {
            var station = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            while (!station.isDone) yield return null;
            var loading = SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            while (!loading.isDone) yield return null;
            float giveUp = Time.realtimeSinceStartup + 120;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null || EmergencySession.Current.Crowd.People.Count == 0) && Time.realtimeSinceStartup < giveUp) yield return null;
            var shift = EmergencySession.Current;
            Assert.That(shift, Is.Not.Null, "the shift did not start");
            var crowd = shift.Crowd;
            var mind = crowd.Mind;
            Assert.That(shift.Jev.Available, Is.True, "a plausible key must leave JEV available");
            // 디렉터의 박동도 끈다(평온 구간 30 s 안이라 아직 아무것도 묻지 않았지만, 이 시험의 60 s 동안에도 묻지 않게).
            shift.Incidents.enabled = false;
            Time.captureFramerate = 30;

            IEnumerator Play(float gameSeconds)
            {
                float until = Time.time + gameSeconds;
                while (Time.time < until)
                {
                    if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
                    if (shift.Player.IsPaused) shift.Player.Resume(false);
                    yield return null;
                }
            }

            yield return Play(5);
            var witness = crowd.People
                .Where(p => !p.Aboard && p.Body.Visible && Passenger.Routine(p.Current) && p.Current != Passenger.Activity.InTrain)
                .OrderByDescending(p => crowd.CountNear(p.transform.position, 8, null))
                .First();
            var spot = StationWorld.OnNavMesh(witness.transform.position + witness.transform.forward * 3f, 2f);
            var fire = new FireHazard("outage-fire", spot, "시험용 불", "가방", .45f, shift.Art, shift.transform) { Where = shift.World.Describe(spot) };
            HazardRegistry.Add(fire);
            crowd.Alert(spot, 400f, fire, null, "the fire alarm bell is ringing");
            crowd.Announce();

            yield return Play(60);
            Debug.Log("CROWD_OUTAGE frozen=" + mind.Metrics.Frozen + " continued=" + mind.Metrics.Continued + " " + mind.Metrics.SummaryText(shift.Jev));
            Assert.That(mind.Usable, Is.True);
            Assert.That(mind.Metrics.Requests, Is.GreaterThan(0), "questions were asked");
            Assert.That(mind.Metrics.Answered, Is.EqualTo(0), "nothing answered: this is an outage");
            Assert.That(mind.Metrics.UrgentLocally + mind.Metrics.RoutineLocally, Is.EqualTo(0), "local rules stay for runs without JEV");
            Assert.That(mind.Metrics.Continued, Is.GreaterThan(0), "people waiting for JEV must have gone on with their trips");
            Assert.That(mind.Metrics.Frozen, Is.EqualTo(0), "frozen while JEV was silent:\n" + string.Join("\n", mind.Metrics.FrozenFindings));

            Time.captureFramerate = 0;
            HazardRegistry.Clear();
            Assert.That(shift.Jev.Usage.Requests, Is.EqualTo(0), "no request may leave the process: the test's JEV client was never asked");
            var scratch = SceneManager.CreateScene("CrowdOutageScratch");
            SceneManager.SetActiveScene(scratch);
            yield return SceneManager.UnloadSceneAsync(SceneFlow.EmergencyScene);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(SceneFlow.StationScene);
        }
    }
}
