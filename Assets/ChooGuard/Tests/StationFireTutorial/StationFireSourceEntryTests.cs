using System;
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps.Shell;
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
        public IEnumerator CheckedInScenesStartTheLessonAndReturnToTheTutorialMenu()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            yield return Until(() => UnityEngine.Object.FindFirstObjectByType<TitleScreen>()?.Built == true,
                60, "The checked-in title scene must initialize");
            var title = UnityEngine.Object.FindFirstObjectByType<TitleScreen>();
            title.StartButton.onClick.Invoke();
            title.Modes.TutorialButton.onClick.Invoke();
            var menu = UnityEngine.Object.FindFirstObjectByType<TutorialMenu>();
            Assert.That(menu, Is.Not.Null);
            menu.GetComponentsInChildren<Button>().Single(button => button.name == "역무원 화재대응").onClick.Invoke();
            yield return Until(() => StationFireTutorialSession.Current != null && !SceneFlow.Loading &&
                (StationFireTutorialSession.Current.Ready || StationFireTutorialSession.Current.Failure.Length > 0),
                600, "The PR source must prepare the fire lesson without a prebuilt player");
            var lesson = StationFireTutorialSession.Current;
            Assert.That(lesson.Failure, Is.Empty);
            Assert.That(lesson.Ready, Is.True);
            Assert.That(lesson.IncidentSourceName, Is.EqualTo("C165_S01_OBSERVED_CHAIR_1_0_1"));
            Assert.That(lesson.ExtinguisherSourceName, Is.EqualTo("C165_S01_P5_SOURCE_LEFT_ENTRY_PORTABLE_EXTINGUISHER"));
            Assert.That(lesson.Controls, Is.Not.Null);
            Assert.That(OfflineStationStartupScope.IsDeferred, Is.True);
            lesson.SetVerificationInput(true);
            lesson.ResumeLesson();
            yield return null;
            Assert.That(lesson.Step, Is.EqualTo(StationFireStep.Warning));
            lesson.SetPaused(true);
            Assert.That(Time.timeScale, Is.Zero);
            lesson.ExitLesson();
            yield return Until(() => UnityEngine.Object.FindFirstObjectByType<TitleScreen>()?.Built == true &&
                UnityEngine.Object.FindFirstObjectByType<TutorialMenu>() != null,
                180, "Leaving the lesson must return to the source tutorial menu");
            menu = UnityEngine.Object.FindFirstObjectByType<TutorialMenu>();
            var names = menu.GetComponentsInChildren<Button>().Select(button => button.name).ToArray();
            Assert.That(names, Does.Contain("역무원 화재대응"));
            Assert.That(names, Does.Contain("심정지 환자 대응"));
            Assert.That(OfflineStationStartupScope.IsDeferred, Is.False);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            yield return null;
        }
    }
}
