using System.Collections;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.Seeded.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.StationFireTutorial
{
    public sealed class StationFireIntegrationTests
    {
        [Test]public void CatalogRetainsTheDistinctCardiacAndFireDispatchRoutes()
        {
            var catalog=Resources.Load<TutorialCatalog>(TutorialCatalog.ResourceName);
            Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Validate(out var reason),Is.True,reason);
            var cardiac=catalog.Categories.Single(c=>c.Id==PracticeShift.CardiacCategory);
            var fire=catalog.Categories.Single(c=>c.Id=="station-fire-response");
            Assert.That(cardiac.SceneName,Is.EqualTo("FpsStation"));Assert.That(PracticeShift.FamilyOf(cardiac.Id),Is.EqualTo(PracticeShift.CardiacFamily));
            Assert.That(fire.SceneName,Is.EqualTo("StationFireTutorial"));Assert.That(PracticeShift.FamilyOf(fire.Id),Is.Null);
            Assert.That(catalog.Categories.Any(c=>c.SceneName=="CrewTrainTutorial"),Is.True);
        }
        [UnityTest]public IEnumerator CardiacPracticeDeferralAndFireStartupScopeReleaseTheirOwnState()
        {
            Time.timeScale=1;yield return SceneManager.LoadSceneAsync("Bootstrap");yield return null;
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
            yield return SceneManager.LoadSceneAsync("Bootstrap");yield return null;
            Assert.That(PracticeShift.Armed,Is.False);Assert.That((bool)property.GetValue(null),Is.EqualTo(before));
        }
    }
}
