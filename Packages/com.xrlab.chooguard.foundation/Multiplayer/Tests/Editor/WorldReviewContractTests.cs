using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;
using UnityEngine;
using System;
using ChooGuard.Foundation.Multiplayer.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public sealed class WorldReviewContractTests
{
    [Test]
    public void ReviewBuilderIsIdempotentAndKeepsProtocolDisabledInSeparateScenes()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) Assert.Ignore("Preserve unsaved scenes.");
        var root = "Assets/CHOOguardGenerated/ReviewTest_" + Guid.NewGuid().ToString("N");
        try
        {
            var first = FoundationReviewSceneBuilder.Build(root);
            var second = FoundationReviewSceneBuilder.Build(root);
            CollectionAssert.AreEqual(first,second); Assert.That(second.Length,Is.EqualTo(14));
            var capture = UnityEngine.Object.FindObjectsByType<WorldReviewCapture>(FindObjectsSortMode.None);
            Assert.That(capture.Length,Is.EqualTo(1));
            Assert.That(capture[0].World.SceneRootPath,Is.EqualTo(root));
            Assert.That(capture[0].World.GetComponent<NetworkFieldRuntime>().enabled,Is.False);
            Assert.That(capture[0].View,Is.Not.Null); Assert.That(capture[0].Sun,Is.Not.Null);
            Assert.That(second.All(p => p.StartsWith(root+"/")),Is.True);
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            if(previous.Any(s=>s.isLoaded&&s.isActive)&&previous.All(s=>!string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            AssetDatabase.DeleteAsset(root);
        }
    }
    [Test]
    public void StaticReviewUsesFixedMetreCamerasAndExposesMissingTemporalEvidence()
    {
        var a = WorldReviewCapture.Shots(); var b = WorldReviewCapture.Shots();
        Assert.That(a.Select(s => s.FileName).Distinct().Count(), Is.EqualTo(a.Length));
        Assert.That(a.Any(s => s.ViewId == "V01" && s.Orthographic), Is.True);
        Assert.That(a.Count(s => s.ViewId == "V02" && s.Orthographic), Is.EqualTo(2));
        Assert.That(a.Any(s => s.ViewId == "V05" && s.DoorOpen), Is.True);
        Assert.That(a.Any(s => s.ViewId == "V05" && !s.DoorOpen), Is.True);
        Assert.That(a.Any(s => s.Environment == "low-light"), Is.True);
        Assert.That(a.All(s => s.FieldOfView == 60 && s.Near == .05f && s.Far == 300), Is.True);
        Assert.That(a.All(s => Vector3.Distance(s.Position, s.Target) > .1f), Is.True);
        a[0].Position = Vector3.zero;
        Assert.That(b[0].Position, Is.Not.EqualTo(Vector3.zero), "Callers cannot change later candidate cameras.");
        Assert.That(WorldReviewCapture.Unmeasured, Does.Contain("V10"));
        Assert.That(WorldReviewCapture.Unmeasured, Does.Contain("V11"));
        Assert.That(WorldReviewCapture.Unmeasured, Does.Contain("V12"));
    }
}
