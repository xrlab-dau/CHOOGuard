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
    /// The train-departure scene is the game's title (2026-11-18): its main page offers "기차 튜토리얼" and "비상상황 연습",
    /// and a practice chosen there loads the station, builds the emergency session from the Resources template and runs
    /// in practice mode; "타이틀로" brings the player back to the train menu and releases the practice.
    /// </summary>
    public sealed class PracticeMenuEntryTests
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator ReturningToPracticeListPreservesThePageAndTrainCanStillStart()
        {
            PracticeCatalog.LastGroupId = null;
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene, LoadSceneMode.Single);
            yield return null;
            SceneFlow.ToTutorialMenu();
            float until = Time.realtimeSinceStartup + 300;
            while (SceneFlow.Loading && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(SceneFlow.Loading, Is.False, "Returning to the practice list did not finish loading.");
            yield return null;

            var view = Object.FindFirstObjectByType<DepartureGameView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(Labels(view), Does.Contain("환자 발생").And.Contain("화재").And.Contain("돌아가기"),
                "The title must preserve the practice-list request after all Awake/Start callbacks.");
            Assert.That(Labels(view), Does.Not.Contain("기차 튜토리얼"));
            Assert.That(SceneFlow.ConsumePracticeMenuRequest(), Is.False, "The request must be consumed only once.");
            Assert.That(view.HandleCancel(), Is.True);
            Assert.That(Labels(view), Does.Contain("기차 튜토리얼"));

            var game = Object.FindFirstObjectByType<TrainDepartureGame>();
            Assert.That(game, Is.Not.Null);
            game.Player.SetExternalInputMode(true);
            Button(view, "기차 튜토리얼").onClick.Invoke();
            Assert.That(game.Started, Is.True, "Returning from practice must still allow the train tutorial to start.");
            Assert.That(view.MenuVisible, Is.False);
            game.Restart();
            Assert.That(Labels(view), Does.Contain("기차 튜토리얼"), "Restarting the train must open its main menu.");
            Assert.That(PracticeShift.Armed, Is.False);
        }

        [UnityTest, Timeout(1200000)]
        public IEnumerator TheTrainMenuLeadsToTheCardiacPracticeAndBack()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.TutorialScene, LoadSceneMode.Single);
            yield return null;
            var view = Object.FindFirstObjectByType<DepartureGameView>();
            Assert.That(view, Is.Not.Null, "열차 시연 씬에 메뉴가 없습니다.");
            Assert.That(view.MenuVisible, Is.True);
            Assert.That(Labels(view), Does.Contain("실습하기").And.Contain("기차 튜토리얼").And.Contain("비상상황 연습"));

            Assert.That(PracticeCatalog.GroupOf(PracticeShift.CardiacFamily)?.Title, Is.EqualTo("환자 발생"));
            Button(view, "비상상황 연습").onClick.Invoke();
            yield return null;
            Assert.That(Labels(view), Is.SupersetOf(PracticeCatalog.Groups.Select(group => group.Title)).And.Contain("돌아가기"));
            Assert.That(Labels(view), Does.Not.Contain("기차 튜토리얼"));

            Button(view, "환자 발생").onClick.Invoke();
            yield return null;
            Assert.That(Labels(view), Does.Contain("심정지 · AED").And.Contain("경련").And.Contain("돌아가기"));
            Assert.That(Labels(view), Does.Not.Contain("화재"), "세부 상황 목록에 카테고리가 남아 있습니다.");
            Assert.That(view.HandleCancel(), Is.True);
            yield return null;
            Assert.That(Labels(view), Does.Contain("화재").And.Contain("환자 발생"), "Esc 가 카테고리 목록으로 돌아가지 않았습니다.");
            Button(view, "환자 발생").onClick.Invoke();
            yield return null;

            Assert.That(Application.CanStreamedLevelBeLoaded(SceneFlow.StationScene), Is.True, "FpsStation 이 빌드 목록에 없습니다.");
            Button(view, "심정지 · AED").onClick.Invoke();
            Assert.That(PracticeShift.Armed, Is.True);

            float until = Time.realtimeSinceStartup + 900;
            while ((EmergencySession.Current == null || EmergencySession.Current.Incidents == null || !EmergencySession.Current.Incidents.Practicing)
                   && Time.realtimeSinceStartup < until)
                yield return null;
            var session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null, "연습을 고른 뒤 비상대응 세션이 생기지 않았습니다.");
            Assert.That(session.Incidents, Is.Not.Null);
            Assert.That(session.Incidents.Practicing, Is.True, "세션이 연습 모드로 시작하지 않았습니다.");
            Assert.That(SceneManager.GetSceneByName(SceneFlow.StationScene).isLoaded, Is.True);
            Assert.That(session.gameObject.scene.name, Is.EqualTo(SceneFlow.EmergencyScene));
            Assert.That(PracticeShift.Armed, Is.False, "세션이 연습 종류를 가져가지 않았습니다.");

            SceneFlow.ToTitle();
            until = Time.realtimeSinceStartup + 300;
            while ((SceneManager.GetActiveScene().name != SceneFlow.TutorialScene || EmergencySession.Current != null || SceneFlow.Loading)
                   && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneFlow.TutorialScene), "타이틀로가 열차 메뉴로 돌아가지 않았습니다.");
            Assert.That(EmergencySession.Current, Is.Null);
            Assert.That(SceneManager.GetSceneByName(SceneFlow.StationScene).isLoaded, Is.False);
            yield return null;
            var back = Object.FindFirstObjectByType<DepartureGameView>();
            Assert.That(back, Is.Not.Null);
            Assert.That(Labels(back), Does.Contain("기차 튜토리얼"));
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
