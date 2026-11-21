#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using ChooGuard.App.Fps.TrainDeparture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// The "맞이방 화재 대응" practice (docs/fire-tutorial.md "1-1") sits in the "비상상황 연습" list next to
    /// "심정지 환자 대응" (PracticeMenuEntryTests). Unlike the cardiac lesson it stages nothing fixed: the ordinary
    /// practice-family draw (IncidentDirector.Practice.cs) must find a real bin/power-bank fire candidate in the
    /// 2층 맞이방 within the offer window for the shift to leave Practicing.
    /// </summary>
    public sealed class FireTutorialMenuTests
    {
        private string key;

        [SetUp]
        public void PreventExternalRequests()
        {
            key = System.Environment.GetEnvironmentVariable(JevKey.Variable);
            System.Environment.SetEnvironmentVariable(JevKey.Variable, "off");
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            System.Environment.SetEnvironmentVariable(JevKey.Variable, key);
            Time.timeScale = 1;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EvacuationCanBeAnnouncedBeforeOrAfterASuppressionAttemptWithoutPenalty(bool attempted)
        {
            var lesson = new FireTutorialLesson(null);
            if (attempted) { lesson.OnPinPulled(); lesson.OnSpraying(); }
            lesson.OnEvacuationAnnounced();
            lesson.OnEvacuationAnnounced();
            Assert.That(lesson.EvacuationAnnounced, Is.True);
            Assert.That(lesson.OrderScore, Is.EqualTo(FireTutorialLesson.FullScore),
                "Announcing evacuation must not require an extinguisher attempt first.");
            Assert.That(lesson.Mistakes, Is.Empty);
        }

        [Test]
        public void EvacuationDoesNotEraseOtherDeductionsOrChangeAFinalizedScore()
        {
            var lesson = new FireTutorialLesson(null);
            lesson.OnExtinguisherPicked();
            lesson.OnReceiverResetRequested();
            Assert.That(lesson.OrderScore, Is.EqualTo(80));
            lesson.OnEvacuationAnnounced();
            Assert.That(lesson.OrderScore, Is.EqualTo(80));
            Assert.That(lesson.Mistakes.Select(m => m.Key), Is.EquivalentTo(new[]
                { "extinguisher-before-report", "receiver-reset-before-handover" }));
            lesson.OnHandedOver();
            lesson.RecordOrderMistake(FireTutorialLesson.Mistake.ExtinguisherBeforeReport);
            Assert.That(lesson.OrderScore, Is.EqualTo(80));
            Assert.That(lesson.Mistakes.All(m => m.Count == 1), Is.True);
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator TheTrainMenuListsTheFirePracticeAndStartsIt()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.TutorialScene, LoadSceneMode.Single);
            yield return null;
            var view = Object.FindFirstObjectByType<DepartureGameView>();
            Assert.That(view, Is.Not.Null, "열차 시연 씬에 메뉴가 없습니다.");

            Assert.That(PracticeCatalog.Scenario(PracticeShift.FireFamily)?.Lesson, Is.EqualTo(PracticeLesson.Fire));

            Button(view, "비상상황 연습").onClick.Invoke();
            yield return null;
            Button(view, "화재").onClick.Invoke();
            yield return null;
            Assert.That(Labels(view), Does.Contain("맞이방 작은 불").And.Contain("주방 기름 화재").And.Contain("전기 설비 화재"));

            // Unavailable entries still show their explanation but must never arm a shift or leave the menu.
            Button(view, "주방 기름 화재").onClick.Invoke();
            yield return null;
            Assert.That(PracticeShift.Armed, Is.False);
            Assert.That(SceneFlow.Loading, Is.False);
            Assert.That(view.MenuVisible, Is.True);

            Assert.That(Application.CanStreamedLevelBeLoaded(SceneFlow.StationScene), Is.True, "FpsStation 이 빌드 목록에 없습니다.");
            Button(view, "맞이방 작은 불").onClick.Invoke();
            Assert.That(PracticeShift.Armed, Is.True);

            float until = Time.realtimeSinceStartup + 900;
            while ((EmergencySession.Current == null || EmergencySession.Current.Incidents == null || !EmergencySession.Current.Incidents.Practicing)
                   && Time.realtimeSinceStartup < until)
                yield return null;
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "연습을 고른 뒤 비상대응 세션이 생기지 않았습니다.");
            Assert.That(session.IsFireTutorial, Is.True);
            Assert.That(session.FireLesson, Is.Not.Null, "화재 연습인데 FireLesson 이 만들어지지 않았습니다.");
            Assert.That(session.Incidents.Practicing, Is.True, "세션이 연습 모드로 시작하지 않았습니다.");
            Assert.That(PracticeShift.Armed, Is.False, "세션이 연습 종류를 가져가지 않았습니다.");

            session.Player.SetExternalInputMode(true);
            Assert.That(session.Player.Resume(false), Is.True);
            Assert.That(session.Jev.Available, Is.False, "The test must not contact an external judge.");
            until = Time.realtimeSinceStartup + 180;
            while (!HazardRegistry.Active.OfType<FireHazard>().Any() && Time.realtimeSinceStartup < until)
                yield return null;
            var fire = HazardRegistry.Active.OfType<FireHazard>().SingleOrDefault();
            Assert.That(fire, Is.Not.Null, "Selecting fire practice must produce a real fire after resuming the shift.");
            Assert.That(fire.Extinguished, Is.False);
            Assert.That(HazardRegistry.Active.Count, Is.EqualTo(1), "Practice must not compose unrelated incidents.");

            SceneFlow.ToTitle();
            until = Time.realtimeSinceStartup + 300;
            while ((SceneManager.GetActiveScene().name != SceneFlow.TutorialScene || EmergencySession.Current != null || SceneFlow.Loading)
                   && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneFlow.TutorialScene), "타이틀로가 열차 메뉴로 돌아가지 않았습니다.");
            Assert.That(EmergencySession.Current, Is.Null);
        }

        private static string[] Labels(DepartureGameView view) =>
            view.GetComponentsInChildren<Button>(false).Where(button => button.gameObject.activeInHierarchy).Select(button => button.gameObject.name).ToArray();

        private static Button Button(DepartureGameView view, string label)
        {
            var button = view.GetComponentsInChildren<Button>(false).FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy && candidate.gameObject.name == label);
            Assert.That(button, Is.Not.Null, "메뉴에 '" + label + "' 버튼이 없습니다.");
            return button;
        }
    }
}
#endif
