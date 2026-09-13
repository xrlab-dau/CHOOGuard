using System;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Multiplayer.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldShutdownTests
{
    [Test]
    public void AdapterDisposalReleasesNavigationAfterRegionSceneWasAlreadyDestroyed()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) Assert.Ignore("Preserve unsaved scenes.");
        var root = "Assets/CHOOguardGenerated/ShutdownTest_" + Guid.NewGuid().ToString("N");
        ConnectedWorldMotionAdapter adapter = null;
        try
        {
            var paths = ConnectedWorldSceneBuilder.Build(root);
            foreach (var path in paths.Skip(1)) EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(ConnectedWorldSceneBuilder.DefinitionPath));
            var views = UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None);
            adapter = new ConnectedWorldMotionAdapter(world, views, new[] { new WorldBodySpawn {
                BodyId = "shutdown-body", Pose = world.Pose(world.StartRegionId, world.Region(world.StartRegionId).Hub),
                RadiusM = .3, HeightM = 1.8, PreferredSpeedMS = 1.5 } }, 1);
            UnityEngine.Object.DestroyImmediate(views[0].gameObject);
            Assert.DoesNotThrow(() => adapter.Dispose());
            Assert.DoesNotThrow(() => adapter.Dispose());
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (previous.Any(s => s.isLoaded && s.isActive) && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            AssetDatabase.DeleteAsset(root);
        }
    }
}
