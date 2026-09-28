using ChooGuard.App.Fps.Shell;
using UnityEditor;

namespace ChooGuard.Editor
{
    /// <summary>In the Editor, the in-game 'quit' button ends play mode (Application.Quit does nothing there).</summary>
    internal static class PlayModeQuitHook
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            SceneFlow.QuitRequested -= StopPlaying;
            SceneFlow.QuitRequested += StopPlaying;
        }

        private static void StopPlaying() => EditorApplication.isPlaying = false;
    }
}
