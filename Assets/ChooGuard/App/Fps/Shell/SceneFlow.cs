using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Shell
{
    // Presentation scenes are entered directly. TrainDepartureDemo opens on its own menu, which is also the title:
    // "기차 튜토리얼" stays in that scene, "비상상황 연습" loads FpsStation for one practice (PracticeShift).
    public static class SceneFlow
    {
        public const string StationScene = "FpsStation";
        public const string TutorialScene = "TrainDepartureDemo";
        // Leaving a practice ("타이틀로") returns to the train-departure menu, never to the station itself.
        public const string TitleScene = TutorialScene;
        // The session scene is created at runtime for a practice (PracticeShift); no StationEmergency asset is registered.
        public const string EmergencyScene = "StationEmergency";
        public static bool UsesProviderRuntime => Resources.Load<TextAsset>("SourceManifest") != null;
        public static bool Loading { get; private set; }
        public static bool OpenTutorialMenuOnTitle => false;
        public static event Action QuitRequested;
        public static void ConsumeTutorialMenuRequest() { }
        public static void StartTutorial(string sceneName, TMP_FontAsset font, Texture background) => Load(TutorialScene);
        public static void StartShift(TMP_FontAsset font, Texture background) => Load(StationScene);
        public static void ToTitle() => Load(TitleScene);
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
