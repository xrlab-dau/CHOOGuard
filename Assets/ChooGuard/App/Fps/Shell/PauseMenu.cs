using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>Shown while the player controller is paused: first as the shift briefing, then as the Esc menu.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public bool Suppressed { get; set; }
        public bool BriefingDone { get; private set; }
        public Button ContinueButton { get; private set; }

        private FirstPersonResponder responder;
        private Canvas canvas;
        private RectTransform menu;
        private TMP_Text heading, body, continueLabel;
        private SettingsPanel settings;

        public static PauseMenu Create(Transform parent, TMP_FontAsset font, FirstPersonResponder responder, string briefingTitle, string briefingBody)
        {
            var canvasObject = FpsUiFactory.InteractiveCanvas("일시정지 메뉴", parent, 50);
            var pause = canvasObject.AddComponent<PauseMenu>();
            pause.responder = responder;
            pause.canvas = canvasObject.GetComponent<Canvas>();
            var root = (RectTransform)canvasObject.transform;
            var shade = FpsUiFactory.Panel(root, "음영", Vector2.zero, Vector2.zero, new Color(.01f, .015f, .025f, .55f));
            FpsUiFactory.Stretch(shade.rectTransform);
            shade.raycastTarget = true;
            var side = FpsUiFactory.Panel(root, "좌측 판", Vector2.zero, Vector2.zero, new Color(.01f, .015f, .025f, .8f));
            side.rectTransform.anchorMin = Vector2.zero;
            side.rectTransform.anchorMax = new Vector2(0, 1);
            side.rectTransform.pivot = new Vector2(0, .5f);
            side.rectTransform.sizeDelta = new Vector2(760, 0);
            side.rectTransform.anchoredPosition = Vector2.zero;

            pause.menu = FpsUiFactory.Node(root, "메뉴");
            FpsUiFactory.Place(pause.menu, new Vector2(0, .5f), new Vector2(72, 0), new Vector2(620, 560));
            pause.heading = FpsUiFactory.Text(pause.menu, font, "제목", new Vector2(0, 1), Vector2.zero, new Vector2(620, 60), 40, TextAlignmentOptions.BottomLeft);
            pause.heading.fontStyle = FontStyles.Bold;
            pause.body = FpsUiFactory.Text(pause.menu, font, "내용", new Vector2(0, 1), new Vector2(0, -76), new Vector2(600, 150), 18, TextAlignmentOptions.TopLeft);
            pause.body.color = new Color(1, 1, 1, .78f);
            pause.ContinueButton = FpsUiFactory.Button(pause.menu, font, "근무 시작", new Vector2(0, 1), new Vector2(0, -250), new Vector2(360, 56), pause.OnContinue, 24);
            pause.continueLabel = pause.ContinueButton.GetComponentInChildren<TMP_Text>();
            FpsUiFactory.Button(pause.menu, font, "설정", new Vector2(0, 1), new Vector2(0, -316), new Vector2(360, 56), pause.OnSettings, 24);
            FpsUiFactory.Button(pause.menu, font, "타이틀로", new Vector2(0, 1), new Vector2(0, -382), new Vector2(360, 56), SceneFlow.ToTitle, 24);
            FpsUiFactory.Button(pause.menu, font, "게임 종료", new Vector2(0, 1), new Vector2(0, -448), new Vector2(360, 56), SceneFlow.Quit, 24);
            pause.settings = SettingsPanel.Create(root, font, () => { pause.settings.Hide(); pause.menu.gameObject.SetActive(true); });
            pause.heading.text = briefingTitle;
            pause.body.text = briefingBody;
            return pause;
        }

        private void OnContinue()
        {
            if (responder == null) return;
            if (responder.Resume() && !BriefingDone)
            {
                BriefingDone = true;
                heading.text = "일시정지";
                body.text = ControlsText();
                continueLabel.text = "계속하기";
            }
        }

        /// <summary>The controls for the current guidance level (견학 keeps one key press per action).</summary>
        private static string ControlsText() => GameSettings.Guided
            ? "WASD 이동 · 마우스 시점 · Shift 빠르게 · E 상호작용 · R 역무원 열쇠(운전 모드·재가동)\n좌클릭 손에 든 장비 사용 · G 내려놓기\nTab 상황판 · M 역사 안내도 · Q 무전(누른 채 선택)"
            : "WASD 이동 · 마우스 시점 · Shift 빠르게(무거운 장비를 들면 걷기만) · E 상호작용 · E 누른 채 마우스·휠로 손잡이 조작 · R 열쇠\n우클릭 누른 채 살펴보기 · 휠 클릭 내 표시 · 좌클릭 손에 든 장비 사용 · 휠 노즐(관창) · G 내려놓기\nTab 수첩 · M 역사 안내도 · Q 무전(누른 채 좌클릭으로 묶음 열기, 우클릭 뒤로, 떼면 보냄)";

        private void OnSettings()
        {
            menu.gameObject.SetActive(false);
            settings.Show();
        }

        private void Update()
        {
            bool show = responder != null && responder.IsPaused && !Suppressed;
            if (canvas.enabled != show)
            {
                canvas.enabled = show;
                if (!show) { settings.Hide(); menu.gameObject.SetActive(true); }
                else
                {
                    if (BriefingDone) body.text = ControlsText();
                    ContinueButton.Select();
                }
            }
            if (show) GameSettings.Apply(responder);
        }
    }
}
