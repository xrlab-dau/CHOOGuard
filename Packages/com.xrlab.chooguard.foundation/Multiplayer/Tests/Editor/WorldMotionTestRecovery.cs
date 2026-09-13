using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public static class WorldMotionTestRecovery
    {
        [MenuItem("CHOOguard/Tests/Clear Interrupted World Motion Fixtures")]
        public static void Clear()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var folder in AssetDatabase.GetSubFolders("Assets/CHOOguardGenerated").Where(p => p.Contains("/WorldMotionTest_") || p.EndsWith("/WorldMotionPlayFixture")))
                AssetDatabase.DeleteAsset(folder);
        }
    }
}
