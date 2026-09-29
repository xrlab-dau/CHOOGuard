using System;
using TMPro;
using UnityEngine;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>Mouse sensitivity and master volume. Shared by the title and the in-shift pause menu.</summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private Action close;
        private TMP_Text sensitivityValue, volumeValue, routeValue;

        public static SettingsPanel Create(RectTransform parent, TMP_FontAsset font, Action onClose)
        {
            var root = FpsUiFactory.Node(parent, "설정");
            FpsUiFactory.Place(root, new Vector2(0, .5f), new Vector2(72, 0), new Vector2(520, 480));
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

            var routeButton = FpsUiFactory.Button(root, font, "길 안내 표시", new Vector2(0, 1), new Vector2(0, -294), new Vector2(500, 48),
                () => { GameSettings.ShowRoute = !GameSettings.ShowRoute; panel.Refresh(); }, 20);
            panel.routeValue = FpsUiFactory.Text(routeButton.transform, font, "상태", new Vector2(1, .5f), new Vector2(-20, 0), new Vector2(100, 34), 18, TextAlignmentOptions.Right);
            FpsUiFactory.Button(root, font, "돌아가기", new Vector2(0, 1), new Vector2(0, -365), new Vector2(260, 52), () => panel.close?.Invoke(), 22);
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
        }
    }
}
