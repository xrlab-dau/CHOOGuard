using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>
    /// Top-of-screen notice for the automated player modes (<see cref="BenchmarkRun"/>, <see cref="SoakRun"/>). They look
    /// exactly like the game but drive the camera themselves and take no player input, so the screen says so. Esc ends the
    /// run at once (exit code 3, no report).
    /// </summary>
    public sealed class AutomatedRunBanner : MonoBehaviour
    {
        private string logTag;

        public static void Attach(GameObject runner, string text, string logTag)
        {
            var banner = runner.AddComponent<AutomatedRunBanner>();
            banner.logTag = logTag;
            var title = FindAnyObjectByType<TitleScreen>();
            var font = title != null && title.KoreanFont != null ? title.KoreanFont : TMP_Settings.defaultFontAsset;
            var root = (RectTransform)FpsUiFactory.Canvas("자동 실행 알림", runner.transform, 100).transform;
            // 위쪽 가운데의 나침반 띠와 방위 숫자 아래에 둔다.
            var panel = FpsUiFactory.Block(root, "바탕", new Vector2(.5f, 1), new Vector2(0, -96), new Vector2(820, 40), FpsUiFactory.PanelStrong);
            var label = FpsUiFactory.Text(panel.transform, font, "내용", new Vector2(.5f, .5f), Vector2.zero, new Vector2(800, 40), 18, TextAlignmentOptions.Center);
            label.text = text + " · 조작 입력을 받지 않습니다 · Esc 중단";
            label.color = FpsUiFactory.Accent;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            Debug.Log(logTag + " Esc 로 중단");
            UnityEngine.Application.Quit(3);
        }
    }
}
