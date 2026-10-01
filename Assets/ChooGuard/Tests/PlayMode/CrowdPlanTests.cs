using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class CrowdPlanTests
    {
        private string savedKey;
        private Passenger traveller;
        private string exitKey;
        private bool answered;

        [SetUp]
        public void SetUp()
        {
            traveller = null;
            exitKey = null;
            answered = false;
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "plan-test-" + Guid.NewGuid().ToString("N"));
            EmergencySession.NextSeed = 20260930;
            CrowdMind.Transport = (purpose, state, questions, done, lane) => Reply(questions, done);
        }

        private IEnumerator Reply(IReadOnlyList<JevChoice> questions, Action<Dictionary<string, JevAnswer>> done)
        {
            yield return null;
            var answers = new Dictionary<string, JevAnswer>();
            var shift = EmergencySession.Current;
            foreach (var question in questions)
            {
                string key = question.Criteria.Keys.FirstOrDefault();
                if (traveller == null && question.Id.StartsWith("r", StringComparison.Ordinal) &&
                    int.TryParse(question.Id.Substring(1).Split('_')[0], out int number))
                {
                    var person = shift.Crowd.People.FirstOrDefault(p => p.Number == number);
                    var offeredExit = question.Criteria.Keys.FirstOrDefault(k => k.StartsWith("leave:", StringComparison.Ordinal));
                    if (person != null && person.Trip == Passenger.Purpose.Visit && !person.OnItinerary && !person.Leaving && offeredExit != null)
                    {
                        traveller = person;
                        exitKey = offeredExit;
                        key = exitKey;
                        answered = true;
                    }
                }
                if (key != null) answers[question.Id] = new JevAnswer { Choice = key, Probabilities = new Dictionary<string, float> { [key] = 1 } };
            }
            done(answers);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ClosingBoundExit_DoesNotCarryItsJudgementToAnotherExit()
        {
            var station = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            while (!station.isDone) yield return null;
            var loading = SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            while (!loading.isDone) yield return null;
            float deadline = Time.realtimeSinceStartup + 120;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null) && Time.realtimeSinceStartup < deadline) yield return null;
            var shift = EmergencySession.Current;
            Assert.That(shift, Is.Not.Null);
            Assert.That(shift.Crowd, Is.Not.Null);
            shift.Incidents.enabled = false;
            shift.Player.SetExternalInputMode(true);
            shift.Player.Resume(false);
            while ((!answered || traveller == null || !shift.Crowd.Mind.Ready(traveller)) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(traveller, Is.Not.Null, "no visiting traveller received a bound exit plan");
            Assert.That(answered, Is.True);
            var mind = shift.Crowd.Mind;
            Assert.That(mind.TryTakeRoutine(traveller, out var first), Is.True);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Key, Is.EqualTo(exitKey));
            var original = first.Place;
            shift.World.Closed.Add((original.Position, 2f, "closed destination"));

            Assert.That(mind.TryTakeRoutine(traveller, out var next), Is.True);
            Assert.That(next, Is.Not.Null, "the trip must continue while another plan is requested");
            Assert.That(next.Key, Is.Not.EqualTo(exitKey), "a judgement about the closed exit must not be reused for a different exit");
            Assert.That(next.Place, Is.Not.SameAs(original), "the closed destination cannot be selected again");
            Assert.That(shift.World.IsClosed(next.Place.Position), Is.False);
            Assert.That(shift.Jev.Usage.Requests, Is.Zero, "the controlled transport must not send real model requests");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            CrowdMind.Transport = null;
            Time.timeScale = 1;
            HazardRegistry.Clear();
            var scratch = SceneManager.CreateScene("CrowdPlanScratch");
            SceneManager.SetActiveScene(scratch);
            var emergency = SceneManager.GetSceneByName(SceneFlow.EmergencyScene);
            if (emergency.IsValid() && emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
            if (station.IsValid() && station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
        }
    }
}
