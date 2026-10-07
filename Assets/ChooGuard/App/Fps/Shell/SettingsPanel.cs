using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>
    /// Mouse sensitivity, master volume, the guidance level (견학·표준·실전, each with a sentence on what it shows or hides) and its two
    /// separate switches (route marks, simple controls). Shared by the title and the in-shift pause menu.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private static readonly GuidanceLevel[] Levels = { GuidanceLevel.Guided, GuidanceLevel.Standard, GuidanceLevel.Expert };

        private Action close;
        private TMP_Text sensitivityValue, volumeValue, routeValue, simpleValue, guidanceText;
        private readonly Image[] levelButtons = new Image[3];
        private readonly TMP_Text[] levelCaptions = new TMP_Text[3];

        public static SettingsPanel Create(RectTransform parent, TMP_FontAsset font, Action onClose)
        {
            var root = FpsUiFactory.Node(parent, "설정");
            FpsUiFactory.Place(root, new Vector2(0, .5f), new Vector2(72, 0), new Vector2(520, 660));
            var panel = root.gameObject.AddComponent<SettingsPanel>();
            panel.close = onClose;
            var heading = FpsUiFactory.Text(root, font, "제목", new Vector2(0, 1), Vector2.zero, new Vector2(520, 60), 40, TextAlignmentOptions.BottomLeft);
            heading.text = "설정";
            heading.fontStyle = FontStyles.Bold;

            var sensLabel = FpsUiFactory.Text(root, font, "감도 제목", new Vector2(0, 1), new Vector2(0, -96), new Vector2(360, 30), 20, TextAlignmentOptions.Left);
            sensLabel.text = "마우스 감도";
            panel.sensitivityValue = FpsUiFactory.Text(root, font, "감도 값", new Vector2(0, 1), new Vector2(380, -96), new Vector2(120, 30), 20, TextAlignmentOptions.Right);
            FpsUiFactory.Slider(root, "감도", new Vector2(0, 1), new Vector2(0, -134), new Vector2(500, 28), .2f, 3f, GameSettings.MouseSensitivity,
                v => { GameSettings.MouseSensitivity = v; panel.Refresh(); });

            var volLabel = FpsUiFactory.Text(root, font, "음량 제목", new Vector2(0, 1), new Vector2(0, -186), new Vector2(360, 30), 20, TextAlignmentOptions.Left);
            volLabel.text = "전체 음량";
            panel.volumeValue = FpsUiFactory.Text(root, font, "음량 값", new Vector2(0, 1), new Vector2(380, -186), new Vector2(120, 30), 20, TextAlignmentOptions.Right);
            FpsUiFactory.Slider(root, "음량", new Vector2(0, 1), new Vector2(0, -224), new Vector2(500, 28), 0f, 1f, GameSettings.MasterVolume,
                v => { GameSettings.MasterVolume = v; panel.Refresh(); });

            var guidanceLabel = FpsUiFactory.Text(root, font, "안내 수준 제목", new Vector2(0, 1), new Vector2(0, -272), new Vector2(500, 30), 20, TextAlignmentOptions.Left);
            guidanceLabel.text = "안내 수준";
            for (int i = 0; i < Levels.Length; i++)
            {
                var level = Levels[i];
                var button = FpsUiFactory.Button(root, font, GameSettings.Label(level), new Vector2(0, 1), new Vector2(i * 170, -310), new Vector2(160, 44),
                    () => { GameSettings.Guidance = level; panel.Refresh(); }, 20);
                panel.levelButtons[i] = (Image)button.targetGraphic;
                panel.levelCaptions[i] = button.GetComponentInChildren<TMP_Text>();
            }
            panel.guidanceText = FpsUiFactory.Text(root, font, "안내 수준 설명", new Vector2(0, 1), new Vector2(0, -364), new Vector2(500, 56), 15, TextAlignmentOptions.TopLeft);
            panel.guidanceText.color = FpsUiFactory.TextDim;

            var routeButton = FpsUiFactory.Button(root, font, "길 안내 표시", new Vector2(0, 1), new Vector2(0, -432), new Vector2(500, 48),
                () => { GameSettings.ShowRoute = !GameSettings.ShowRoute; panel.Refresh(); }, 20);
            panel.routeValue = FpsUiFactory.Text(routeButton.transform, font, "상태", new Vector2(1, .5f), new Vector2(-20, 0), new Vector2(100, 34), 18, TextAlignmentOptions.Right);
            var simpleButton = FpsUiFactory.Button(root, font, "간편 조작 (손잡이를 E 한 번으로)", new Vector2(0, 1), new Vector2(0, -490), new Vector2(500, 48),
                () => { GameSettings.SimpleControls = !GameSettings.SimpleControls; panel.Refresh(); }, 20);
            panel.simpleValue = FpsUiFactory.Text(simpleButton.transform, font, "상태", new Vector2(1, .5f), new Vector2(-20, 0), new Vector2(100, 34), 18, TextAlignmentOptions.Right);
            FpsUiFactory.Button(root, font, "돌아가기", new Vector2(0, 1), new Vector2(0, -564), new Vector2(260, 52), () => panel.close?.Invoke(), 22);
            panel.Refresh();
            root.gameObject.SetActive(false);
            return panel;
        }

        public void Show() { gameObject.SetActive(true); Refresh(); }
        public void Hide() => gameObject.SetActive(false);

        private void Refresh()
        {
            if (sensitivityValue != null) sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.00") + "×";
            if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100) + "%";
            if (routeValue != null) routeValue.text = GameSettings.ShowRoute ? "켜짐" : "꺼짐";
            if (simpleValue != null) simpleValue.text = GameSettings.SimpleControls ? "켜짐" : "꺼짐";
            var current = GameSettings.Guidance;
            for (int i = 0; i < Levels.Length; i++)
            {
                if (levelButtons[i] == null) continue;
                bool chosen = Levels[i] == current;
                // 고른 수준은 업무 색 바탕으로 둔다(버튼 색 배율은 그대로).
                levelButtons[i].color = chosen ? FpsUiFactory.Accent : Color.white;
                levelCaptions[i].color = chosen ? new Color(.05f, .05f, .05f, 1) : Color.white;
                levelCaptions[i].fontStyle = chosen ? FontStyles.Bold : FontStyles.Normal;
            }
            if (guidanceText != null) guidanceText.text = GameSettings.Describe(current) + "\n<size=13>항목은 아래에서 따로 바꿀 수 있습니다. 어느 수준이든 역무원이 할 수 있는 일은 같습니다.</size>";
        }
    }
}
