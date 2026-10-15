#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // These tests use candidates listed by the real station, with offline JEV replies.
    // They check delivery, not which incident is drawn or when one should occur.
    public sealed class JevDirectorRecoveryTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private EmergencySession shift;
        private IncidentDirector director;
        private JevClient client;
        private JevBudget budget;
        private JevConnection connection;
        private string savedKey;

        private static object Field(object owner, string name) => owner.GetType().GetField(name, Members).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Members).SetValue(owner, value);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Members).Invoke(owner, args);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20261013;
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            float end = Time.realtimeSinceStartup + 120;
            while ((EmergencySession.Current?.Incidents == null || EmergencySession.Current.Crowd == null) && Time.realtimeSinceStartup < end) yield return null;
            shift = EmergencySession.Current;
            Assert.That(shift?.Incidents, Is.Not.Null);
            Assert.That(shift.Jev.Available, Is.False, "The scene cannot use the network in these tests.");
            director = shift.Incidents;
            director.enabled = false;
            shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
            Time.timeScale = 0; // Keep live subjects stable while real-time connection recovery is tested.
            var transitionType = typeof(IncidentDirector).GetNestedType("Transition", BindingFlags.NonPublic);
            var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(transitionType));
            var rosterType = typeof(IncidentDirector).GetNestedType("Roster", BindingFlags.NonPublic);
            var roster = Activator.CreateInstance(rosterType, new object[] { director });
            foreach (var _ in (IEnumerable)Call(director, "OriginSteps", list, roster)) { }
            Call(director, "Reconcile", list);
            // Only this disabled director receives a test client. Crowd/scene JEV stays off.
            Environment.SetEnvironmentVariable(JevKey.Variable, "issue280-offline-test-key");
            client = new JevClient(null);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            Set(director, "jev", client);
            budget = (JevBudget)Field(client, "budget");
            connection = (JevConnection)Field(client, "connection");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (director != null) { director.StopAllCoroutines(); Set(director, "jev", shift.Jev); }
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            EmergencySession.NextSeed = 0;
            Time.timeScale = 1;
            Time.captureFramerate = 0;
            var emergency = SceneManager.GetSceneByName(SceneFlow.EmergencyScene);
            var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
            SceneManager.SetActiveScene(SceneManager.CreateScene("JevRecoveryScratch"));
            if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
            HazardRegistry.Clear();
        }

        private object[] LiveCandidates(bool distinctKinds = true)
        {
            var result = new List<object>();
            foreach (DictionaryEntry entry in (IDictionary)Field(director, "candidates"))
            {
                var candidate = entry.Value;
                var fresh = Call(director, "Fresh", candidate);
                if (fresh == null || Field(fresh, "Levels") == null) continue;
                Set(candidate, "Levels", new[] { 0f, 1f, 0f, 0f, 0f });
                result.Add(candidate);
            }
            return distinctKinds ? result.GroupBy(c => (string)Field(Field(c, "Transition"), "Kind")).Select(g => g.First()).ToArray() : result.ToArray();
        }

        private IEnumerator Chosen(object candidate, Func<Dictionary<string, object>, JevChoice, Action<Dictionary<string, JevAnswer>>, IEnumerator> ask) =>
            (IEnumerator)Call(director, "CompleteChosen", candidate, "offline scheduling test", ask);

        private static IEnumerator Answer(JevChoice question, Action<Dictionary<string, JevAnswer>> done)
        {
            yield return null;
            done(new Dictionary<string, JevAnswer> { [question.Id] = new JevAnswer { Score = 1 } });
        }

        [UnityTest]
        public IEnumerator SlowJudgementReleasesItsSlotToTheChosenTransitionBeforeMoreRatings()
        {
            var candidates = LiveCandidates();
            Assert.That(candidates.Length, Is.GreaterThanOrEqualTo(2), "Cover multiple real origin kinds, not one fixed incident.");
            foreach (var candidate in candidates.Take(2))
            {
                if (Call(director, "Fresh", candidate) == null) continue;
                var ticket = budget.Begin(JevLane.Director, Time.realtimeSinceStartup, 100);
                int requests = 0, before = shift.Log.Events;
                var pending = Chosen(candidate, (state, question, done) => { requests++; return Answer(question, done); });
                try
                {
                    Assert.That(pending.MoveNext(), Is.True);
                    Assert.That(requests, Is.Zero, "Do not overflow the serial slot.");
                    Assert.That(shift.Log.Events, Is.EqualTo(before));
                    Assert.That(Call(director, "SendDue", shift.ShiftSeconds, new Dictionary<string, object>()), Is.False);
                    budget.End(ticket, 100, 0, false);
                    connection.Observe(JevLane.Director, requests + 1, JevFailure.None, Time.realtimeSinceStartup, seconds: 16);
                    Assert.That(connection.Serial(JevLane.Director), Is.True);
                    yield return pending;
                    Assert.That(requests, Is.EqualTo(1));
                    Assert.That(shift.Log.Events, Is.EqualTo(before + 1), "The chosen transition must complete exactly once after a slow answer.");
                    Assert.That(Field(director, "applying"), Is.False);
                }
                finally { ((IDisposable)pending).Dispose(); }
            }
            Assert.That(client.Usage.Requests, Is.EqualTo(2), "Only the two simulated existing judgements were charged. Offline replies never send HTTP.");
        }

        [UnityTest]
        public IEnumerator BudgetWaitKeepsTheChoiceAndAnInvalidatedSubjectIsDiscarded()
        {
            var candidate = LiveCandidates().First();
            var ticket = budget.Begin(JevLane.Director, Time.realtimeSinceStartup, JevBudget.MaxTokensPerMinute);
            budget.End(ticket, JevBudget.MaxTokensPerMinute, 0, false);
            int requests = 0, before = shift.Log.Events;
            var pending = Chosen(candidate, (state, question, done) => { requests++; return Answer(question, done); });
            try
            {
                Assert.That(pending.MoveNext(), Is.True);
                Assert.That(requests, Is.Zero);
                Assert.That(client.StatusOf(JevLane.Director), Is.EqualTo("게임 내부 예산 대기"));
                Assert.That(client.FailuresInARowOf(JevLane.Director), Is.Zero);
                // Advance this accounting entry past the real-time window, not the game clock.
                Set(ticket, "At", Time.realtimeSinceStartup - JevBudget.WindowSeconds - 1);
                yield return pending;
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(shift.Log.Events, Is.EqualTo(before + 1));
                Assert.That(Field(director, "applying"), Is.False);
            }
            finally { ((IDisposable)pending).Dispose(); }
            candidate = LiveCandidates().First();
            ticket = budget.Begin(JevLane.Director, Time.realtimeSinceStartup, JevBudget.MaxTokensPerMinute);
            budget.End(ticket, JevBudget.MaxTokensPerMinute, 0, false);
            requests = 0;
            before = shift.Log.Events;
            pending = Chosen(candidate, (state, question, done) => { requests++; return Answer(question, done); });
            try
            {
                Assert.That(pending.MoveNext(), Is.True);
                Assert.That(requests, Is.Zero);
                var transition = Field(candidate, "Transition");
                var empty = typeof(JevDirectorRecoveryTests).GetMethod(nameof(EmptyRecheck), BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(transition.GetType()).Invoke(null, null);
                Set(transition, "Recheck", empty);
                Assert.That(pending.MoveNext(), Is.False, "A no-longer-valid subject must not block the director until the budget recovers.");
                Assert.That(shift.Log.Events, Is.EqualTo(before));
                Assert.That(Field(director, "applying"), Is.False);
                Assert.That(requests, Is.Zero);
            }
            finally { ((IDisposable)pending).Dispose(); }
        }

        [UnityTest]
        public IEnumerator DestroyedUnitySubjectWhileWaitingDoesNotThrowOrKeepTheDirectorBlocked()
        {
            var candidate = LiveCandidates(false).First(c => Field(Field(c, "Transition"), "Subject") is Component component && component != director);
            var equipment = (UnityEngine.Object)Field(Field(candidate, "Transition"), "Subject");
            var ticket = budget.Begin(JevLane.Director, Time.realtimeSinceStartup, 100);
            int requests = 0, before = shift.Log.Events;
            var pending = Chosen(candidate, (state, question, done) => { requests++; return Answer(question, done); });
            try
            {
                Assert.That(pending.MoveNext(), Is.True);
                Assert.That(requests, Is.Zero);
                UnityEngine.Object.DestroyImmediate(equipment);
                Assert.That(pending.MoveNext(), Is.False, "An unavailable Unity subject must be dropped before calling its old recheck closure.");
                Assert.That(requests, Is.Zero);
                Assert.That(shift.Log.Events, Is.EqualTo(before));
                Assert.That(Field(director, "applying"), Is.False);
            }
            finally { ((IDisposable)pending).Dispose(); budget.End(ticket, 100, 0, false); }
            yield break;
        }

        [UnityTest]
        public IEnumerator AReplyWhileManuallyPausedWaitsForResumeBeforeApplying()
        {
            var candidate = LiveCandidates().First();
            int before = shift.Log.Events;
            bool replied = false;
            var pending = Chosen(candidate, (state, question, done) => Reply(question, done));
            IEnumerator Reply(JevChoice question, Action<Dictionary<string, JevAnswer>> done)
            {
                yield return null;
                shift.Player.Pause();
                yield return Answer(question, done);
                replied = true;
            }
            try
            {
                var running = director.StartCoroutine(pending);
                float end = Time.realtimeSinceStartup + 5;
                while (!replied && Time.realtimeSinceStartup < end) yield return null;
                Assert.That(replied, Is.True);
                yield return null;
                Assert.That(shift.Player.IsPaused, Is.True);
                Assert.That(shift.Log.Events, Is.EqualTo(before));
                Assert.That(Field(director, "applying"), Is.True);
                Assert.That(shift.Player.Resume(false), Is.True);
                Time.timeScale = 0;
                yield return running;
                Assert.That(shift.Log.Events, Is.EqualTo(before + 1));
                Assert.That(Field(director, "applying"), Is.False);
            }
            finally { ((IDisposable)pending).Dispose(); }
        }

        [UnityTest]
        public IEnumerator EndingTheShiftBeforeItsReplyNeverAppliesThePendingTransition()
        {
            var candidate = LiveCandidates().First();
            int before = shift.Log.Events;
            var pending = Chosen(candidate, (state, question, done) => EndThenReply(question, done));
            IEnumerator EndThenReply(JevChoice question, Action<Dictionary<string, JevAnswer>> done)
            {
                yield return null;
                typeof(IncidentDirector).GetProperty("Stage", Members).SetValue(director, IncidentDirector.Phase.Ended);
                yield return Answer(question, done);
            }
            try
            {
                yield return pending;
                Assert.That(shift.Log.Events, Is.EqualTo(before));
                Assert.That(Field(director, "applying"), Is.False);
                Assert.That(client.Usage.Requests, Is.Zero);
            }
            finally { ((IDisposable)pending).Dispose(); }
        }

        [UnityTest]
        public IEnumerator CancellingAWaitingChoiceReleasesItsStateWithoutReleasingAnotherRequest()
        {
            var candidate = LiveCandidates().First();
            var ticket = budget.Begin(JevLane.Director, Time.realtimeSinceStartup, 100);
            int before = shift.Log.Events;
            var pending = Chosen(candidate, (state, question, done) => { Assert.Fail("The occupied slot cannot send."); return null; });
            try
            {
                Assert.That(pending.MoveNext(), Is.True);
                Assert.That(Field(director, "applying"), Is.True);
                ((IDisposable)pending).Dispose();
                Assert.That(Field(director, "applying"), Is.False);
                Assert.That(budget.InFlight(JevLane.Director), Is.EqualTo(1));
                Assert.That(client.Usage.Requests, Is.EqualTo(1));
                Assert.That(client.Usage.Failures, Is.Zero);
                Assert.That(shift.Log.Events, Is.EqualTo(before));
            }
            finally { ((IDisposable)pending).Dispose(); budget.End(ticket, 100, 0, false); }
            yield break;
        }

        private static Func<T> EmptyRecheck<T>() => () => default;

        [UnityTest]
        public IEnumerator TemporaryFailureRetriesTheSameLiveChoiceAndKeyRejectionStopsIt()
        {
            var candidates = LiveCandidates();
            int requests = 0, before = shift.Log.Events;
            var pending = Chosen(candidates[0], (state, question, done) => Attempt(question, done));
            IEnumerator Attempt(JevChoice question, Action<Dictionary<string, JevAnswer>> done)
            {
                requests++;
                yield return null;
                if (requests == 1)
                {
                    connection.Observe(JevLane.Director, 1, JevFailure.Timeout, Time.realtimeSinceStartup);
                    done(null);
                }
                else
                {
                    Assert.That(shift.Log.Events, Is.EqualTo(before), "A failure cannot invent an event.");
                    connection.Observe(JevLane.Director, 2, JevFailure.None, Time.realtimeSinceStartup);
                    yield return Answer(question, done);
                }
            }
            try
            {
                yield return pending;
                Assert.That(requests, Is.EqualTo(2));
                Assert.That(shift.Log.Events, Is.EqualTo(before + 1));
                Assert.That(Field(director, "applying"), Is.False);
            }
            finally { ((IDisposable)pending).Dispose(); }
            var other = LiveCandidates().First();
            requests = 0;
            before = shift.Log.Events;
            var rejected = Chosen(other, (state, question, done) => Refuse(done));
            IEnumerator Refuse(Action<Dictionary<string, JevAnswer>> done)
            {
                requests++;
                yield return null;
                connection.Observe(JevLane.Director, 3, JevFailure.Authentication, Time.realtimeSinceStartup);
                Set(client, "disabled", true);
                done(null);
            }
            try
            {
                yield return rejected;
                Assert.That(requests, Is.EqualTo(1));
                Assert.That(shift.Log.Events, Is.EqualTo(before));
                Assert.That(Field(director, "applying"), Is.False);
            }
            finally { ((IDisposable)rejected).Dispose(); }
            Assert.That(client.Usage.Requests, Is.Zero, "The request delegate is offline; no external calls occur.");
        }
    }
}
#endif
