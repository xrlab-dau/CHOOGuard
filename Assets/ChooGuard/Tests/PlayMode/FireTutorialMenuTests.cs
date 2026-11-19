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
