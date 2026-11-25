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

        [UnityTest, Timeout(1200000)]
        public IEnumerator FreeShiftStartsWithoutAPracticeAndReleasesItsStateOnReturn()
        {
            string key = System.Environment.GetEnvironmentVariable(JevKey.Variable);
            System.Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            try
            {
                yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene, LoadSceneMode.Single);
                yield return null;
                var view = Object.FindFirstObjectByType<DepartureGameView>();
                Assert.That(view, Is.Not.Null);
                Button(view, "실습하기").onClick.Invoke();
                Assert.That(PracticeShift.Free, Is.True);
                Assert.That(PracticeShift.Armed, Is.False);

                float until = Time.realtimeSinceStartup + 900;
                while ((EmergencySession.Current == null || EmergencySession.Current.Incidents == null || SceneFlow.Loading)
                       && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(SceneFlow.Loading, Is.False);
                var session = EmergencySession.Current;
                Assert.That(session, Is.Not.Null);
                Assert.That(session.Incidents, Is.Not.Null);
                Assert.That(session.Incidents.Practicing, Is.False);
                Assert.That(session.IsCardiacTutorial || session.IsFireTutorial, Is.False);
                Assert.That(session.CardiacLesson, Is.Null);
                Assert.That(session.FireLesson, Is.Null);
                Assert.That(session.Jev.Available, Is.False, "This regression must not make external requests.");
                Assert.That(PracticeShift.Free, Is.True);
                yield return WalkFromSouthPlatformBoardingPoint(session);

                SceneFlow.ToTitle();
                until = Time.realtimeSinceStartup + 300;
                while ((SceneFlow.Loading || EmergencySession.Current != null || SceneManager.GetActiveScene().name != SceneFlow.TitleScene)
                       && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(SceneFlow.Loading, Is.False);
                Assert.That(EmergencySession.Current, Is.Null);
                Assert.That(PracticeShift.Free || PracticeShift.Armed, Is.False);
                Assert.That(SceneManager.GetSceneByName(SceneFlow.StationScene).isLoaded, Is.False);
                yield return null;
                Assert.That(Labels(Object.FindFirstObjectByType<DepartureGameView>()), Does.Contain("실습하기").And.Contain("비상상황 연습"));
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(JevKey.Variable, key);
            }
        }

        private static string[] Labels(DepartureGameView view) =>
            view.GetComponentsInChildren<Button>(false).Where(button => button.gameObject.activeInHierarchy).Select(button => button.gameObject.name).ToArray();

        private static IEnumerator WalkFromSouthPlatformBoardingPoint(EmergencySession session)
        {
            const string id = "esc-well-s-34";
            var lane = session.World.InstalledEscalators.Single(owner => owner.Entry.id == id);
            var node = session.World.Paths.Graph.ByLabel(id + ":start");
            Assert.That(node, Is.Not.Null);
            Assert.That(Vector3.Distance(node.Position, lane.Start), Is.LessThan(.35f));
            var historical = session.World.SourcePoints.Escalators.Single(entry => entry.id == id);
            Assert.That(Vector3.Distance(lane.Start, historical.path[0]), Is.GreaterThan(1f));

            var player = session.Player;
            var heading = lane.Entry.path[1] - node.Position;
            player.SetExternalInputMode(true);
            player.RestorePhysicalPose(node.Position + Vector3.up * .05f, Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg, 0);
            Assert.That(player.Resume(false), Is.True);
            var start = player.transform.position;
            float previousCapture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = .05f;
                for (int frame = 0; frame < 120; frame++)
                {
                    Assert.That(player.StepInput(Vector2.up, Vector2.zero, false, false, false, .05f), Is.True);
                    yield return null;
                }
                Assert.That(start.y - player.transform.position.y, Is.InRange(3f, 7.5f), "Walk onto the descending belt from its route graph node.");
                Assert.That(Vector3.Dot(player.transform.position - start, Vector3.ProjectOnPlane(heading, Vector3.up).normalized), Is.GreaterThan(5f));
            }
            finally { Time.captureDeltaTime = previousCapture; }
        }

        private static Button Button(DepartureGameView view, string label)
        {
            var button = view.GetComponentsInChildren<Button>(false).FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy && candidate.gameObject.name == label);
            Assert.That(button, Is.Not.Null, "메뉴에 '" + label + "' 버튼이 없습니다.");
            return button;
        }
    }
}
#endif
