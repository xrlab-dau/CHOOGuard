using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>Runs the checked-in lesson without rebuilding assets or changing the editing scene.</summary>
    [InitializeOnLoad]
    public static class StationFireTutorialReview
    {
        const string Menu = "ChooGuard/Review/Play fire tutorial 14";
        const string ActiveKey = "ChooGuard.FireTutorial14Review.Active";
        const string PreviousSceneKey = "ChooGuard.FireTutorial14Review.PreviousStartScene";

        static StationFireTutorialReview()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.quitting += RestoreStartScene;
        }

        [MenuItem(Menu)]
        public static void Play()
        {
            if (!CanPlay()) return;
            var required = new[]
            {
                StationFireTutorialSceneBuilder.ScenePath,
                "Assets/ChooGuard/Scenes/FpsStation.unity",
                "Assets/ChooGuard/Scenes/Bootstrap.unity"
            };
            foreach (var path in required)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null ||
                    !EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path))
                    throw new InvalidOperationException("Review scene is missing or disabled: " + path +
                        ". Check out PR #336 and sync its Git LFS assets first.");

            var previous = EditorSceneManager.playModeStartScene;
            SessionState.SetString(PreviousSceneKey, previous == null ? "" : AssetDatabase.GetAssetPath(previous));
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(required[0]);
            try { EditorApplication.EnterPlaymode(); }
            catch { RestoreStartScene(); throw; }
        }

        [MenuItem(Menu, true)]
        static bool CanPlay() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating;

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) RestoreStartScene();
        }

        static void RestoreStartScene()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            var path = SessionState.GetString(PreviousSceneKey, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path)
                ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.SetBool(ActiveKey, false);
            SessionState.EraseString(PreviousSceneKey);
        }
    }
}
