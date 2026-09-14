using System;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Demo;
using ChooGuard.Foundation.Multiplayer.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class MultiplayerSceneTests
    {
        [Test]
        public void GeneratedSliceReusesAuthoredGeometryAndHasOnlyOneWorldDriver()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                Assert.Ignore("Save user scenes before builder tests.");
            var path = "Assets/CHOOguardGenerated/MultiplayerTest_" + Guid.NewGuid().ToString("N");
            var previous = SceneManager.GetActiveScene();
            var originalSceneBytes = File.ReadAllBytes("Assets/CHOOguardGenerated/FoundationDemo/FoundationDemo.unity");
            try
            {
                var scene = MultiplayerSceneBuilder.Build(path);
                var root = scene.GetRootGameObjects().Single();
                Assert.That(root.GetComponentsInChildren<NetworkFieldRuntime>(), Has.Length.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<StationWorldController>(), Is.Empty);
                Assert.That(root.GetComponentsInChildren<DemoGameController>(), Is.Empty);
                Assert.That(root.GetComponentsInChildren<DemoPlayerController>(), Is.Empty);
                Assert.That(root.GetComponentsInChildren<CharacterController>(), Is.Empty, "No stale local player collider may block server movement");
                Assert.That(root.GetComponentsInChildren<NetworkEntityAnchor>().Count(x => x.Kind == EntityKind.Equipment), Is.EqualTo(5));
                Assert.That(root.GetComponentsInChildren<NetworkEntityAnchor>().Count(x => x.Kind == EntityKind.Evacuee), Is.EqualTo(6));
                Assert.That(root.GetComponentsInChildren<Camera>(), Has.Length.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(20));
                CollectionAssert.AreEqual(originalSceneBytes, File.ReadAllBytes("Assets/CHOOguardGenerated/FoundationDemo/FoundationDemo.unity"));
            }
            finally
            {
                for (var i = SceneManager.sceneCount - 1; i >= 0; i--)
                    if (SceneManager.GetSceneAt(i).path.StartsWith(path + "/", StringComparison.Ordinal))
                        EditorSceneManager.CloseScene(SceneManager.GetSceneAt(i), true);
                AssetDatabase.DeleteAsset(path);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                // The build replaced the untitled startup scene, so the generated scene is the only
                // loaded scene: CloseScene cannot drop the last scene, and DeleteAsset then leaves it
                // dirty. Hand the next dirty-scene guard a clean scene instead of that leaked dirt.
                if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
