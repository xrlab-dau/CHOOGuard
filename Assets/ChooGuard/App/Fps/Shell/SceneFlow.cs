using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Shell
{
    // Presentation scenes are entered directly. No title, category or loading-screen UI.
    public static class SceneFlow
    {
        public const string StationScene = "FpsStation";
        public const string TutorialScene = "TrainDepartureDemo";
        // Compatibility for remaining optional tools; these do not register or recreate scenes.
        public const string TitleScene = StationScene;
        public const string EmergencyScene = "StationEmergency";
        public static bool UsesProviderRuntime => Resources.Load<TextAsset>("SourceManifest") != null;
        public static bool Loading { get; private set; }
        public static bool OpenTutorialMenuOnTitle => false;
        public static event Action QuitRequested;
        public static void ConsumeTutorialMenuRequest() { }
        public static void StartTutorial(string sceneName, TMP_FontAsset font, Texture background) => Load(TutorialScene);
        public static void StartShift(TMP_FontAsset font, Texture background) => Load(StationScene);
        public static void ToTitle() => Load(StationScene);
        public static void ToTutorialMenu() => Load(TutorialScene);
        private static void Load(string sceneName)
        {
            if (Loading) return;
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Presentation scene is not registered: " + sceneName);
                return;
            }
            Time.timeScale = 1;
            Loading = true;
            try
            {
                // Unity 6000.3 AsyncOperation.completed sample: associate completion with
                // this operation, rather than any sceneLoaded event in the process.
                var loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (loadOp == null) { Loading = false; return; }
                loadOp.completed += OnLoaded;
            }
            catch { Loading = false; throw; }
        }
        private static void OnLoaded(AsyncOperation operation)
        {
            Loading = false;
            operation.completed -= OnLoaded;
        }
        public static void Quit()
        {
            QuitRequested?.Invoke();
            UnityEngine.Application.Quit();
        }
    }
}
