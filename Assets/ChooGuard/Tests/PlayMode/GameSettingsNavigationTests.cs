#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>Route visibility is an independent option: the standard preset offers it without changing manual controls.</summary>
    public sealed class GameSettingsNavigationTests
    {
        private const string GuidanceKey = "chooguard.guidance";
        private const string RouteKey = "chooguard.showRoute.v2";
        private const string SimpleKey = "chooguard.simpleControls";
        private static readonly string[] Keys = { GuidanceKey, RouteKey, SimpleKey };
        private readonly Dictionary<string, int?> saved = new Dictionary<string, int?>();

        [SetUp]
        public void SetUp()
        {
            foreach (var key in Keys)
            {
                saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                PlayerPrefs.DeleteKey(key);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var key in Keys)
                if (saved[key].HasValue) PlayerPrefs.SetInt(key, saved[key].Value);
                else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        [Test]
        public void FreshProfileShowsTheRouteAndKeepsStandardManualControls()
        {
            Assert.That(GameSettings.Guidance, Is.EqualTo(GuidanceLevel.Standard));
            Assert.That(GameSettings.ShowRoute, Is.True);
            Assert.That(GameSettings.SimpleControls, Is.False);
        }

        [TestCase(GuidanceLevel.Guided, true)]
        [TestCase(GuidanceLevel.Standard, true)]
        [TestCase(GuidanceLevel.Expert, false)]
        public void RouteVisibilityFollowsThePresetUntilSet(GuidanceLevel level, bool visible)
        {
            PlayerPrefs.SetInt(GuidanceKey, (int)level);
            Assert.That(GameSettings.ShowRoute, Is.EqualTo(visible));
        }

        [TestCase(GuidanceLevel.Guided, false)]
        [TestCase(GuidanceLevel.Guided, true)]
        [TestCase(GuidanceLevel.Standard, false)]
        [TestCase(GuidanceLevel.Standard, true)]
        [TestCase(GuidanceLevel.Expert, false)]
        [TestCase(GuidanceLevel.Expert, true)]
        public void SavedRouteChoiceOverridesThePreset(GuidanceLevel level, bool visible)
        {
            PlayerPrefs.SetInt(GuidanceKey, (int)level);
            PlayerPrefs.SetInt(RouteKey, visible ? 1 : 0);
            Assert.That(GameSettings.ShowRoute, Is.EqualTo(visible));
        }

        [Test]
        public void ChangingToStandardRestoresRouteVisibilityAndManualControls()
        {
            PlayerPrefs.SetInt(GuidanceKey, (int)GuidanceLevel.Expert);
            PlayerPrefs.SetInt(RouteKey, 0);
            PlayerPrefs.SetInt(SimpleKey, 1);

            GameSettings.Guidance = GuidanceLevel.Standard;

            Assert.That(GameSettings.ShowRoute, Is.True);
            Assert.That(GameSettings.SimpleControls, Is.False);
            Assert.That(PlayerPrefs.HasKey(RouteKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SimpleKey), Is.False);
        }

        [Test]
        public void SelectingTheCurrentPresetPreservesExplicitOptions()
        {
            PlayerPrefs.SetInt(GuidanceKey, (int)GuidanceLevel.Standard);
            PlayerPrefs.SetInt(RouteKey, 0);
            PlayerPrefs.SetInt(SimpleKey, 1);

            GameSettings.Guidance = GuidanceLevel.Standard;

            Assert.That(GameSettings.ShowRoute, Is.False);
            Assert.That(GameSettings.SimpleControls, Is.True);
        }
    }
}
#endif
