using System;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class WorldMotionPlaySceneSetup : IPrebuildSetup, IPostBuildCleanup
    {
        public const string Root = "Assets/CHOOguardGenerated/WorldMotionPlayFixture";
        public void Setup()
        {
            if (AssetDatabase.IsValidFolder(Root)) throw new InvalidOperationException("World motion Play fixture folder already exists; inspect before reuse.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try { ConnectedWorldSceneBuilder.Build(Root); }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                if (previous.Any(s => s.isLoaded && s.isActive) && previous.All(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
        public void Cleanup() { if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root); }
    }
}
