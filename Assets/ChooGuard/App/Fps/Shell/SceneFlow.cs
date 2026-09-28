using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>Title → station → emergency session. Build order: Bootstrap(0), FpsStation(1), StationEmergency(2).</summary>
    public static class SceneFlow
    {
        public const string TitleScene = "Bootstrap";
        public const string StationScene = "FpsStation";
        public const string EmergencyScene = "StationEmergency";

        public static bool Loading { get; private set; }

        public static void StartShift(TMP_FontAsset font, Texture background)
        {
            if (Loading) return;
            Loading = true;
            var runner = new GameObject("근무 불러오기");
            Object.DontDestroyOnLoad(runner);
            runner.AddComponent<LoadingScreen>().Run(font, background);
        }

        public static void ToTitle()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(TitleScene, LoadSceneMode.Single);
        }

        /// <summary>Raised when the player chooses to quit. The Editor assembly uses it to leave play mode.</summary>
        public static event System.Action QuitRequested;

        public static void Quit()
        {
            QuitRequested?.Invoke();
            UnityEngine.Application.Quit();
        }

        // Playing FpsStation directly in the Editor still gets the emergency session, as long as the build lists it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSessionForDirectStationPlay()
        {
            Loading = false;
            var active = SceneManager.GetActiveScene();
            if (active.name != StationScene || SceneManager.GetSceneByName(EmergencyScene).isLoaded) return;
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(EmergencyScene)) return;
            SceneManager.LoadSceneAsync(EmergencyScene, LoadSceneMode.Additive);
        }

        private sealed class LoadingScreen : MonoBehaviour
        {
            private static readonly string[] Tips =
            {
                "연기가 보이면 먼저 주변에 알리고 신고합니다. 불이 커졌다면 소화기보다 대피가 먼저입니다.",
                "주인 없는 가방은 만지지 않습니다. 사람들을 떨어뜨리고 철도경찰에 신고합니다.",
                "흔들림이 멈출 때까지 머리를 보호하고, 멈춘 뒤에 안내에 따라 이동합니다.",
                "승강기는 비상 시 이용하지 않도록 안내합니다.",
            };
            private Image bar;
            private TMP_Text status;

            public void Run(TMP_FontAsset font, Texture background)
            {
                var canvas = FpsUiFactory.Canvas("불러오기 화면", transform, 1000);
                var back = FpsUiFactory.Picture(canvas.transform, "배경", background);
                FpsUiFactory.Stretch(back.rectTransform);
                back.color = background != null ? new Color(.55f, .55f, .55f, 1) : Color.black;
                var shade = FpsUiFactory.Panel(canvas.transform, "음영", Vector2.zero, Vector2.zero, new Color(0, 0, 0, .45f));
                FpsUiFactory.Stretch(shade.rectTransform);
                var tip = FpsUiFactory.Text(canvas.transform, font, "도움말", new Vector2(.5f, 0), new Vector2(0, 118), new Vector2(1100, 60), 20, TextAlignmentOptions.Center);
                tip.text = Tips[Random.Range(0, Tips.Length)];
                tip.color = FpsUiFactory.TextDim;
                FpsUiFactory.Block(canvas.transform, "진행 바탕", new Vector2(.5f, 0), new Vector2(0, 80), new Vector2(900, 4), new Color(1, 1, 1, .15f));
                bar = FpsUiFactory.Block(canvas.transform, "진행", new Vector2(.5f, 0), new Vector2(-450, 80), new Vector2(0, 4), FpsUiFactory.Accent);
                bar.rectTransform.pivot = new Vector2(0, 0);
                bar.rectTransform.anchoredPosition = new Vector2(-450, 80);
                status = FpsUiFactory.Text(canvas.transform, font, "상태", new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(900, 30), 17, TextAlignmentOptions.Center);
                StartCoroutine(Load());
            }

            private IEnumerator Load()
            {
                var station = SceneManager.LoadSceneAsync(StationScene, LoadSceneMode.Single);
                while (!station.isDone) { Report(station.progress * .6f, "부산역 불러오는 중"); yield return null; }
                // 처음 보는 화면 상태마다 셰이더를 만드느라 멈추지 않도록 미리 만든다(ShaderWarmup). 두 번째 실행부터는 금방 끝난다.
                yield return ShaderWarmup.Run(p => Report(.6f + p * .25f, "화면 준비 중"));
                var session = SceneManager.LoadSceneAsync(EmergencyScene, LoadSceneMode.Additive);
                while (!session.isDone) { Report(.85f + session.progress * .15f, "근무 준비 중"); yield return null; }
                Report(1, "근무 시작");
                yield return null;
                Loading = false;
                Destroy(gameObject);
            }

            private void Report(float value, string message)
            {
                bar.rectTransform.sizeDelta = new Vector2(900 * Mathf.Clamp01(value), 4);
                status.text = message;
            }
        }
    }
}
