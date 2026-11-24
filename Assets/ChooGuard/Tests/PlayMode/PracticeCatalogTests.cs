#if UNITY_INCLUDE_TESTS
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
    /// <summary>
    /// Every available scenario of the "비상상황 연습" menu (PracticeCatalog) must be able to happen: within the window a practice
    /// shift waits for it (after <c>PracticeSettleSeconds</c>, for its own <c>OfferSeconds</c>), the station must offer at least one
    /// origin candidate the scenario accepts; a scenario shown as 준비 중 must still have none. One shift is watched for all at once.
    /// </summary>
    public sealed class PracticeCatalogTests
    {
        private const float SettleSeconds = 30f;

        [Test]
        public void ScenarioIdsAreUniqueAndEveryGroupHasOne()
        {
            var ids = PracticeCatalog.Groups.SelectMany(group => group.Scenarios).Select(scenario => scenario.Id).ToList();
            Assert.That(ids, Is.Unique);
            Assert.That(PracticeCatalog.Groups.Select(group => group.Id), Is.Unique);
            Assert.That(PracticeCatalog.Groups.All(group => group.Scenarios.Length > 0), Is.True);
            Assert.That(PracticeCatalog.Scenario(PracticeShift.CardiacFamily).Lesson, Is.EqualTo(PracticeLesson.Cardiac));
            Assert.That(PracticeCatalog.Scenario(PracticeShift.FireFamily).Lesson, Is.EqualTo(PracticeLesson.Fire));
            foreach (var scenario in PracticeCatalog.Groups.SelectMany(group => group.Scenarios))
                Assert.That(scenario.Kinds, Is.Not.Empty, scenario.Id);
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator EveryScenarioFindsACandidateWithinThePracticeWindow()
        {
            string key = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            try
            {
                // A quiet practice keeps the director from composing anything else while the world offers its causes.
                Assert.That(PracticeShift.Arm("환자·저혈당"), Is.True);
                yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
                float until = Time.realtimeSinceStartup + 600;
                while ((EmergencySession.Current == null || EmergencySession.Current.Incidents == null || !EmergencySession.Current.Incidents.Practicing)
                       && Time.realtimeSinceStartup < until)
                    yield return null;
                var session = EmergencySession.Current;
                Assert.That(session, Is.Not.Null, "연습 세션이 생기지 않았습니다.");
                Assert.That(session.Jev.Available, Is.False, "Catalog observation must not make external requests.");
                Assert.That(session.Incidents.Practice?.Id, Is.EqualTo("환자·저혈당"));
                session.Player.SetExternalInputMode(true);
                Assert.That(session.Player.Resume(false), Is.True);

                until = Time.realtimeSinceStartup + 600;
                while (session.ShiftSeconds < SettleSeconds && Time.realtimeSinceStartup < until) yield return null;
                // 첫 등장 시각(근무가 자리잡은 뒤 초)을 후보마다 적는다. 상황마다 자기 대기 시간(OfferSeconds) 안에 나와야 한다.
                var scenarios = PracticeCatalog.Groups.SelectMany(group => group.Scenarios).Where(scenario => scenario.Lesson != PracticeLesson.Cardiac).ToList();
                float watch = scenarios.Where(scenario => scenario.Available).Max(scenario => scenario.OfferSeconds);
                var firstSeen = new Dictionary<(string Kind, string Zone), float>();
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < watch)
                {
                    float at = Time.realtimeSinceStartup - start;
                    foreach (var candidate in session.Incidents.PracticeCandidates())
                        if (!firstSeen.ContainsKey(candidate)) firstSeen[candidate] = at;
                    yield return new WaitForSecondsRealtime(1f);
                }
                Debug.Log("[PracticeCatalogTests] 후보 " + firstSeen.Count + "건: " +
                          string.Join(", ", firstSeen.GroupBy(c => c.Key.Kind).OrderBy(g => g.Key)
                              .Select(g => g.Key + "@" + string.Join("/", g.Select(c => (c.Key.Zone ?? "-") + ":" + c.Value.ToString("0") + "s").Distinct()))));

                var missing = scenarios.Where(scenario => scenario.Available &&
                        !firstSeen.Any(c => c.Value <= scenario.OfferSeconds && scenario.Accepts(c.Key.Kind, c.Key.Zone)))
                    .Select(scenario => scenario.Id + " (" + string.Join("/", scenario.Kinds) + ", " + scenario.OfferSeconds + "s)").ToList();
                Assert.That(missing, Is.Empty, "대기 시간 안에 후보가 한 번도 없었던 상황: " + string.Join(", ", missing));
                var reopened = scenarios.Where(scenario => !scenario.Available && firstSeen.Keys.Any(c => scenario.Accepts(c.Kind, c.Zone)))
                    .Select(scenario => scenario.Id).ToList();
                Assert.That(reopened, Is.Empty, "준비 중으로 막아 둔 상황의 후보가 이제 나옵니다. PracticeCatalog 의 Unavailable 을 풀어 주세요: " + string.Join(", ", reopened));
            }
            finally
            {
                Environment.SetEnvironmentVariable(JevKey.Variable, key);
            }
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene, LoadSceneMode.Single);
            yield return null;
        }
    }
}
#endif
