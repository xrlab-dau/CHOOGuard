using System;
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps.Shell;
using ChooGuard.App.Fps.TrainDeparture;
using ChooGuard.App.Fps.StationFireTutorial;
using ChooGuard.Seeded.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChooGuard.Tests.StationFireTutorial
{
    public sealed class StationFireSourceEntryTests
    {
        static IEnumerator Until(Func<bool> ready, float seconds, string detail)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(ready(), Is.True, detail);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator CurrentTrainingMenuStartsTheCheckedInLessonAndReturnsToTrainingSelection()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);
            yield return Until(() => UnityEngine.Object.FindFirstObjectByType<DepartureGameView>()?.MenuVisible == true,
                60, "The checked-in title scene must initialize");
            var menu = UnityEngine.Object.FindFirstObjectByType<DepartureGameView>();
            Assert.That(menu, Is.Not.Null);
            menu.GetComponentsInChildren<Button>().Single(button => button.name == "화재 튜토리얼").onClick.Invoke();
            yield return Until(() => StationFireTutorialSession.Current != null && !SceneFlow.Loading &&
                (StationFireTutorialSession.Current.Ready || StationFireTutorialSession.Current.Failure.Length > 0),
                600, "The PR source must prepare the fire lesson without a prebuilt player");
            var lesson = StationFireTutorialSession.Current;
            Assert.That(lesson.Failure, Is.Empty);
            Assert.That(lesson.Ready, Is.True);
            Assert.That(lesson.IncidentSourceName, Is.EqualTo("C165_S01_OBSERVED_CHAIR_1_0_1"));
            Assert.That(lesson.ExtinguisherSourceName, Is.EqualTo("C165_S01_P5_SOURCE_LEFT_ENTRY_PORTABLE_EXTINGUISHER"));
            Assert.That(lesson.Controls, Is.Not.Null);
            Assert.That(OfflineStationStartupScope.IsDeferred, Is.False,
                "The startup-only guard must be released once the lesson has prepared");
            lesson.SetVerificationInput(true);
            lesson.ResumeLesson();
            yield return null;
            Assert.That(lesson.Step, Is.EqualTo(StationFireStep.Warning));
            lesson.SetPaused(true);
            Assert.That(Time.timeScale, Is.Zero);
            lesson.ExitLesson();
            yield return Until(() => UnityEngine.Object.FindFirstObjectByType<DepartureGameView>()?.MenuVisible == true && !SceneFlow.Loading,
                180, "Leaving the lesson must return to the current training selection");
            menu = UnityEngine.Object.FindFirstObjectByType<DepartureGameView>();
            var names = menu.GetComponentsInChildren<Button>().Select(button => button.name).ToArray();
            Assert.That(names, Does.Contain("화재 튜토리얼"));
            Assert.That(names, Does.Contain("기차 튜토리얼"));
            Assert.That(names, Does.Contain("비상상황 연습"));
            foreach (var button in menu.GetComponentsInChildren<Button>())
            {
                var rect = button.GetComponent<RectTransform>();
                Assert.That(rect.anchoredPosition.y - rect.rect.height * rect.pivot.y, Is.GreaterThanOrEqualTo(-350),
                    button.name + " must remain inside the training menu");
                Assert.That(rect.anchoredPosition.y + rect.rect.height * (1 - rect.pivot.y), Is.LessThanOrEqualTo(350),
                    button.name + " must remain inside the training menu");
            }
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(StationFireTutorialSession.Current, Is.Null);
            Assert.That(OfflineStationStartupScope.IsDeferred, Is.False);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);
            yield return null;
        }
    }
}
