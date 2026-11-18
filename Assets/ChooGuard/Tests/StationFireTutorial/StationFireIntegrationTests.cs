using System.Collections;
using System;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Shell;
using ChooGuard.App.Fps.TrainDeparture;
using ChooGuard.Seeded.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChooGuard.Tests.StationFireTutorial
{
    public sealed class StationFireIntegrationTests
    {
        [UnityTest,Timeout(1800000)]
        public IEnumerator FirePracticeStartsFromTheSharedMenuAndReturnsWithoutArmingCardiacPractice()
        {
            Time.timeScale=1;
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);
            yield return null;
            var previousPipeline=QualitySettings.renderPipeline;
            var menu=UnityEngine.Object.FindFirstObjectByType<DepartureGameView>();
            Assert.That(menu,Is.Not.Null);
            menu.ShowPractice();
            menu.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text=="역무원 화재대응").onClick.Invoke();
            float deadline=Time.realtimeSinceStartup+600;
            while((ChooGuard.App.Fps.StationFireTutorial.StationFireTutorialSession.Current==null ||
                !ChooGuard.App.Fps.StationFireTutorial.StationFireTutorialSession.Current.Ready || SceneFlow.Loading) && Time.realtimeSinceStartup<deadline)
            {
                var loading=ChooGuard.App.Fps.StationFireTutorial.StationFireTutorialSession.Current;
                if(loading!=null)Assert.That(loading.Failure,Is.Empty);
                yield return null;
            }
            var lesson=ChooGuard.App.Fps.StationFireTutorial.StationFireTutorialSession.Current;
            Assert.That(lesson,Is.Not.Null);Assert.That(lesson.Ready,Is.True);
            Assert.That(SceneFlow.Loading,Is.False,"The scene transition must settle before using its exit button");
            Assert.That(PracticeShift.Armed,Is.False);
            Assert.That(EmergencySession.Current,Is.Null,"The isolated fire lesson must not create an autonomous/cardiac session");
            lesson.GetComponentsInChildren<Button>(true).Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text=="연습 목록").onClick.Invoke();
            Debug.Log("[Fire menu regression] Return requested; loading="+SceneFlow.Loading+" active="+SceneManager.GetActiveScene().name);
            deadline=Time.realtimeSinceStartup+600;
            while((SceneFlow.Loading||UnityEngine.Object.FindFirstObjectByType<DepartureGameView>()==null) && Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            Assert.That(SceneFlow.Loading,Is.False,"Returning to the practice menu exceeded the cold-scene loading allowance");
            menu=UnityEngine.Object.FindFirstObjectByType<DepartureGameView>();
            Assert.That(menu,Is.Not.Null);
            var labels=menu.GetComponentsInChildren<Button>().Select(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text).ToArray();
            Assert.That(labels,Does.Contain("역무원 화재대응").And.Contain("심정지 환자 대응"));
            Assert.That(ChooGuard.App.Fps.StationFireTutorial.StationFireTutorialSession.Current,Is.Null);
            Assert.That(PracticeShift.Armed,Is.False);
            Assert.That(QualitySettings.renderPipeline,Is.SameAs(previousPipeline),"Fire-specific performance settings must be restored on exit");
        }
        [Test]public void CatalogRetainsTheDistinctCardiacAndFireDispatchRoutes()
        {
            var catalog=Resources.Load<TutorialCatalog>(TutorialCatalog.ResourceName);
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Validate(out var reason),Is.True,reason);
            var cardiac=catalog.Categories.Single(c=>c.Id==PracticeShift.CardiacCategory);
            var fire=catalog.Categories.Single(c=>c.Id=="station-fire-response");
            Assert.That(cardiac.SceneName,Is.EqualTo("FpsStation"));Assert.That(PracticeShift.FamilyOf(cardiac.Id),Is.EqualTo(PracticeShift.CardiacFamily));
            Assert.That(fire.SceneName,Is.EqualTo("StationFireTutorial"));Assert.That(PracticeShift.FamilyOf(fire.Id),Is.Null);
            var available=DepartureGameView.PracticeCategories().Select(c=>c.Id).ToArray();
            Assert.That(available,Does.Contain(cardiac.Id).And.Contain(fire.Id));
        }
        [UnityTest]public IEnumerator CardiacPracticeDeferralAndFireStartupScopeReleaseTheirOwnState()
        {
            Time.timeScale=1;yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);yield return null;
            var property=typeof(PlatformSeededSimulator).GetProperty("DeferPreparationForLegacyTests",BindingFlags.NonPublic|BindingFlags.Static);
            Assert.That(property,Is.Not.Null);var before=(bool)property.GetValue(null);
            Assert.That(OfflineStationStartupScope.IsDeferred,Is.False);
            Assert.That(PracticeShift.Arm(PracticeShift.CardiacFamily),Is.True);
            Assert.That(PracticeShift.Armed,Is.True);Assert.That((bool)property.GetValue(null),Is.True);
            using(new OfflineStationStartupScope())
            {
                Assert.That(OfflineStationStartupScope.IsDeferred,Is.True);
                Assert.That(PracticeShift.Take(),Is.EqualTo(PracticeShift.CardiacFamily));
                Assert.That((bool)property.GetValue(null),Is.True,"Consuming a practice does not release its provider protection early");
            }
            Assert.That(OfflineStationStartupScope.IsDeferred,Is.False);
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);yield return null;
            Assert.That(PracticeShift.Armed,Is.False);Assert.That((bool)property.GetValue(null),Is.EqualTo(before));
        }
    }
}
