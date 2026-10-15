using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Hud
{
    /// <summary>
    /// Hold Q, move the mouse toward a message, release to send (PUBG radio wheel). No cursor unlock. The session can show
    /// a ring of groups first and a group's messages after (<see cref="Show(IReadOnlyList{string}, string)"/> with its own centre text).
    /// </summary>
    public sealed class RadioWheel : MonoBehaviour
    {
        private const float Radius = 190, DeadZone = 30;
        private Canvas canvas;
        private TMP_FontAsset font;
        private RectTransform root;
        private readonly List<(Image back, TMP_Text text)> items = new List<(Image, TMP_Text)>();
        private TMP_Text centre;
        private Vector2 pointer;
        private int count;

        public bool Open => canvas.enabled;
        public int Selected { get; private set; } = -1;

        public static RadioWheel Create(Transform parent, TMP_FontAsset font)
        {
            var canvasObject = FpsUiFactory.Canvas("무전 선택", parent, 22);
            var wheel = canvasObject.AddComponent<RadioWheel>();
            wheel.canvas = canvasObject.GetComponent<Canvas>();
            wheel.font = font;
            wheel.root = (RectTransform)canvasObject.transform;
            var hub = FpsUiFactory.Panel(wheel.root, "중심", Vector2.zero, new Vector2(120, 120), new Color(0, 0, 0, .55f));
            hub.sprite = HudSprites.Disc;
            wheel.centre = FpsUiFactory.Label(wheel.root, font, "안내", Vector2.zero, new Vector2(110, 60), 15);
            wheel.canvas.enabled = false;
            return wheel;
        }

        public void Show(IReadOnlyList<string> options) => Show(options, "무전\n<size=12>방향을 고른 뒤 떼기</size>");

        public void Show(IReadOnlyList<string> options, string centreText)
        {
            count = options.Count;
            while (items.Count < count)
            {
                var back = FpsUiFactory.Panel(root, "항목", Vector2.zero, new Vector2(250, 52), new Color(0, 0, 0, .6f));
                var text = FpsUiFactory.Label(back.transform, font, "문구", Vector2.zero, new Vector2(236, 48), 16);
                text.textWrappingMode = TextWrappingModes.Normal;
                items.Add((back, text));
            }
            for (int i = 0; i < items.Count; i++)
            {
                bool used = i < count;
                items[i].back.gameObject.SetActive(used);
                if (!used) continue;
                float angle = 90 - i * 360f / count;
                var direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                items[i].back.rectTransform.anchoredPosition = direction * Radius;
                items[i].text.text = options[i];
            }
            pointer = Vector2.zero;
            Selected = -1;
            centre.text = centreText;
            canvas.enabled = true;
        }

        public void Steer(Vector2 mouseDelta)
        {
            if (!Open || count == 0) return;
            pointer = Vector2.ClampMagnitude(pointer + mouseDelta, 160);
            if (pointer.magnitude < DeadZone) { Selected = -1; }
            else
            {
                float angle = Mathf.Atan2(pointer.y, pointer.x) * Mathf.Rad2Deg;
                float fromTop = Mathf.Repeat(90 - angle + 180f / count, 360);
                Selected = Mathf.Clamp(Mathf.FloorToInt(fromTop / (360f / count)), 0, count - 1);
            }
            for (int i = 0; i < count; i++)
            {
                bool on = i == Selected;
                items[i].back.color = on ? new Color(FpsUiFactory.Accent.r, FpsUiFactory.Accent.g, FpsUiFactory.Accent.b, .85f) : new Color(0, 0, 0, .6f);
                items[i].text.color = on ? new Color(.05f, .05f, .05f, 1) : Color.white;
            }
        }

        /// <summary>Closes the wheel and returns the selected index, or -1 when released inside the dead zone.</summary>
        public int Close()
        {
            canvas.enabled = false;
            int chosen = Selected;
            Selected = -1;
            return chosen;
        }
    }
}
