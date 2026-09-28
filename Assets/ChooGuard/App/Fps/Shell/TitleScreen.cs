using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>Bootstrap scene title: start shift, settings, quit. Built at runtime on the scene's single canvas.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed class TitleScreen : MonoBehaviour
    {
        public TMP_FontAsset KoreanFont;
        public Texture Background;
        public const string GameTitle = "CHOOGuard";
        public const string Subtitle = "부산역 비상상황 대응";

        public bool Built => menu != null;
        public Button StartButton { get; private set; }
        private RectTransform menu;
        private SettingsPanel settings;

        private void Start()
        {
            if (KoreanFont == null) { Debug.LogError("[TitleScreen] 한국어 폰트가 지정되지 않았습니다.", this); enabled = false; return; }
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GameSettings.Apply(null);
            Build();
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            if (Background != null)
            {
                var back = FpsUiFactory.Picture(root, "배경", Background);
                FpsUiFactory.Stretch(back.rectTransform);
                var fitter = back.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = (float)Background.width / Mathf.Max(1, Background.height);
            }
            var shade = FpsUiFactory.Panel(root, "좌측 음영", Vector2.zero, Vector2.zero, new Color(.01f, .015f, .025f, .78f));
            FpsUiFactory.Place(shade.rectTransform, new Vector2(0, .5f), Vector2.zero, new Vector2(560, 0));
            shade.rectTransform.anchorMin = new Vector2(0, 0);
            shade.rectTransform.anchorMax = new Vector2(0, 1);
            var fade = FpsUiFactory.Panel(root, "음영 경계", Vector2.zero, Vector2.zero, new Color(.01f, .015f, .025f, .35f));
            fade.rectTransform.anchorMin = new Vector2(0, 0);
            fade.rectTransform.anchorMax = new Vector2(0, 1);
            fade.rectTransform.pivot = new Vector2(0, .5f);
            fade.rectTransform.anchoredPosition = new Vector2(560, 0);
            fade.rectTransform.sizeDelta = new Vector2(160, 0);

            menu = FpsUiFactory.Node(root, "메뉴");
            FpsUiFactory.Stretch(menu);
            var title = FpsUiFactory.Text(menu, KoreanFont, "제목", new Vector2(0, 1), new Vector2(72, -64), new Vector2(460, 84), 60, TextAlignmentOptions.TopLeft);
            title.text = GameTitle;
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 2;
            var sub = FpsUiFactory.Text(menu, KoreanFont, "부제", new Vector2(0, 1), new Vector2(74, -148), new Vector2(460, 40), 24, TextAlignmentOptions.TopLeft);
            sub.text = Subtitle;
            sub.color = FpsUiFactory.Accent;
            FpsUiFactory.Block(menu, "구분선", new Vector2(0, 1), new Vector2(74, -198), new Vector2(64, 3), FpsUiFactory.Accent);

            StartButton = FpsUiFactory.Button(menu, KoreanFont, "근무 시작", new Vector2(0, .5f), new Vector2(72, 40), new Vector2(360, 56), OnStart, 24);
            FpsUiFactory.Button(menu, KoreanFont, "설정", new Vector2(0, .5f), new Vector2(72, -26), new Vector2(360, 56), OnSettings, 24);
            FpsUiFactory.Button(menu, KoreanFont, "종료", new Vector2(0, .5f), new Vector2(72, -92), new Vector2(360, 56), SceneFlow.Quit, 24);

            var role = FpsUiFactory.Text(menu, KoreanFont, "역할", new Vector2(0, 0), new Vector2(72, 96), new Vector2(460, 56), 17, TextAlignmentOptions.BottomLeft);
            role.text = "KORAIL 역무원 · 부산역 2층 맞이방 근무\n승객과 공간에서 벌어지는 상황에 대응합니다.";
            role.color = FpsUiFactory.TextDim;
            var notice = FpsUiFactory.Text(menu, KoreanFont, "고지", new Vector2(0, 0), new Vector2(72, 36), new Vector2(460, 44), 13, TextAlignmentOptions.BottomLeft);
            notice.text = "공개된 국민행동요령·공공자료 기반의 게임입니다.\n한국철도공사의 내부 절차나 교육 과정을 재현하지 않습니다.";
            notice.color = new Color(1, 1, 1, .45f);

            settings = SettingsPanel.Create(root, KoreanFont, () => { settings.Hide(); menu.gameObject.SetActive(true); });
            StartButton.Select();
        }

        private void OnStart()
        {
            if (SceneFlow.Loading) return;
            menu.gameObject.SetActive(false);
            SceneFlow.StartShift(KoreanFont, Background);
        }

        private void OnSettings()
        {
            menu.gameObject.SetActive(false);
            settings.Show();
        }
    }
}
