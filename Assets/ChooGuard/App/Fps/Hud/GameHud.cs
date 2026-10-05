using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Hud
{
    /// <summary>
    /// In-shift HUD. PUBG-style edges (compass, radio feed, equipment, status) and My-Summer-Car-style centre
    /// (looked-at object name + action). Nothing else stays on screen; details live behind Tab and M.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public struct Slot
        {
            public string Label, Hint;
            public float? Fill;
            public bool Active;
        }

        private sealed class SlotView
        {
            public Image Back, Fill, FillBack;
            public TMP_Text Label, Hint;
        }

        public CompassBar Compass { get; private set; }
        public RadioFeed Radio { get; private set; }
        public RectTransform Root { get; private set; }
        public TMP_FontAsset Font { get; private set; }

        private FirstPersonResponder responder;
        private Canvas canvas;
        private Image dot, holdRing, vignette;
        private TMP_Text targetName, targetAction, toast, clock, situation, hints, agencyStatus;
        private readonly List<SlotView> slots = new List<SlotView>();
        private RectTransform slotRow;
        private float toastUntil, danger;
        private Color situationColour = Color.white;

        public static GameHud Create(Transform parent, TMP_FontAsset font, FirstPersonResponder responder)
        {
            var canvasObject = FpsUiFactory.Canvas("근무 HUD", parent, 10);
            var hud = canvasObject.AddComponent<GameHud>();
            hud.Build(font, responder);
            return hud;
        }

        private void Build(TMP_FontAsset font, FirstPersonResponder player)
        {
            Font = font;
            responder = player;
            canvas = GetComponent<Canvas>();
            Root = (RectTransform)transform;

            vignette = FpsUiFactory.Panel(Root, "위험 가장자리", Vector2.zero, Vector2.zero, new Color(.25f, .23f, .22f, 0));
            vignette.sprite = HudSprites.Vignette;
            FpsUiFactory.Stretch(vignette.rectTransform);

            Compass = CompassBar.Create(Root, font);
            Compass.Bind(player.PlayerCamera != null ? player.PlayerCamera.transform : player.transform);
            Radio = RadioFeed.Create(Root, font);

            dot = FpsUiFactory.Panel(Root, "조준점", Vector2.zero, new Vector2(5, 5), new Color(1, 1, 1, .9f));
            dot.sprite = HudSprites.Disc;
            holdRing = FpsUiFactory.Panel(Root, "진행 고리", Vector2.zero, new Vector2(44, 44), FpsUiFactory.Accent);
            holdRing.sprite = HudSprites.Ring;
            holdRing.type = Image.Type.Filled;
            holdRing.fillMethod = Image.FillMethod.Radial360;
            holdRing.fillOrigin = (int)Image.Origin360.Top;
            holdRing.fillClockwise = true;
            holdRing.enabled = false;
            targetName = FpsUiFactory.Label(Root, font, "대상 이름", new Vector2(0, -44), new Vector2(700, 28), 19);
            targetName.fontStyle = FontStyles.Bold;
            targetAction = FpsUiFactory.Label(Root, font, "대상 동작", new Vector2(0, -72), new Vector2(760, 26), 17);
            toast = FpsUiFactory.Label(Root, font, "결과", new Vector2(0, -150), new Vector2(900, 30), 18);

            slotRow = FpsUiFactory.Node(Root, "장비");
            FpsUiFactory.Place(slotRow, new Vector2(.5f, 0), new Vector2(0, 26), new Vector2(640, 58));

            var status = FpsUiFactory.Block(Root, "근무 상태", new Vector2(0, 0), new Vector2(24, 26), new Vector2(440, 58), new Color(0, 0, 0, .38f));
            clock = FpsUiFactory.Text(status.rectTransform, font, "시각", new Vector2(0, 1), new Vector2(14, -6), new Vector2(120, 24), 20, TextAlignmentOptions.TopLeft);
            clock.fontStyle = FontStyles.Bold;
            situation = FpsUiFactory.Text(status.rectTransform, font, "상황", new Vector2(0, 0), new Vector2(14, 6), new Vector2(416, 24), 15, TextAlignmentOptions.BottomLeft);
            // 장소 이름이 길어도 한 줄에 둔다(줄바꿈하면 위의 시각을 덮는다): 글자를 줄이고, 그래도 넘치면 말줄임.
            situation.textWrappingMode = TextWrappingModes.NoWrap;
            situation.enableAutoSizing = true;
            situation.fontSizeMin = 12;
            situation.fontSizeMax = 15;
            situation.overflowMode = TextOverflowModes.Ellipsis;

            // 나침반(위 -16, 높이 30 + 방위 글자) 아래. 조준점과 그 아래 대상 이름(-44)은 가운데 앵커라 겹치지 않는다.
            // 기관마다 한 줄이라 세 줄까지 둔다(-88 에서 -154). 조준점과 그 아래 글자는 가운데 앵커라 멀다.
            agencyStatus = FpsUiFactory.Text(Root, font, "출동 상태", new Vector2(.5f, 1), new Vector2(0, -88), new Vector2(760, 66), 15, TextAlignmentOptions.Top);
            agencyStatus.textWrappingMode = TextWrappingModes.NoWrap;
            agencyStatus.overflowMode = TextOverflowModes.Ellipsis;
            agencyStatus.color = new Color(1, 1, 1, .88f);
            agencyStatus.enabled = false;

            hints = FpsUiFactory.Text(Root, font, "조작 안내", new Vector2(1, 0), new Vector2(-24, 28), new Vector2(420, 24), 14, TextAlignmentOptions.BottomRight);
            hints.text = "Tab 상황판   M 안내도   Q 무전   Esc 메뉴";
            hints.color = new Color(1, 1, 1, .5f);

            if (responder != null) responder.FeedbackChanged += OnFeedback;
        }

        private void OnDestroy()
        {
            if (responder != null) responder.FeedbackChanged -= OnFeedback;
        }

        private void OnFeedback(string message) => Toast(message);

        public void Toast(string message, float seconds = 3.5f)
        {
            if (string.IsNullOrEmpty(message)) return;
            toast.text = message;
            toastUntil = Time.unscaledTime + seconds;
        }

        /// <summary>
        /// 화면 위쪽 출동 기관 상태 한 줄. 빈 문자열이면 아무것도 띄우지 않는다 (#271).
        /// </summary>
        /// <remarks>
        /// 나침반 **아래**에 둔다. 상단 가운데는 나침반이 이미 쓰고 있고, 조준점과 그 아래 대상 이름·동작도
        /// 가리면 안 된다. 한 줄로 두고 넘치면 말줄임한다 — 늘어나는 상자를 두면 기관이 둘 이상일 때
        /// 화면을 덮는다.
        /// </remarks>
        public void SetAgencyStatus(string text)
        {
            if (agencyStatus == null) return;
            agencyStatus.text = text ?? "";
            agencyStatus.enabled = !string.IsNullOrEmpty(text);
        }

        public void SetStatus(string clockText, string situationText, Color colour)
        {
            clock.text = clockText;
            situation.text = situationText;
            situationColour = colour;
        }

        public void SetDanger(float amount) => danger = Mathf.Clamp01(amount);

        public void SetProgress(float? progress)
        {
            holdRing.enabled = progress.HasValue;
            if (progress.HasValue) holdRing.fillAmount = Mathf.Clamp01(progress.Value);
        }

        public void SetSlots(IReadOnlyList<Slot> values)
        {
            while (slots.Count < values.Count)
            {
                var view = new SlotView { Back = FpsUiFactory.Block(slotRow, "칸", new Vector2(.5f, 0), Vector2.zero, new Vector2(SlotWidth, 58), new Color(0, 0, 0, .42f)) };
                var back = view.Back.rectTransform;
                view.Label = FpsUiFactory.Text(back, Font, "이름", new Vector2(0, 1), new Vector2(12, -6), new Vector2(SlotWidth - 20, 24), 17, TextAlignmentOptions.TopLeft);
                view.Hint = FpsUiFactory.Text(back, Font, "조작", new Vector2(0, 0), new Vector2(12, 8), new Vector2(SlotWidth - 20, 20), 12, TextAlignmentOptions.BottomLeft);
                view.Hint.color = new Color(1, 1, 1, .6f);
                view.FillBack = FpsUiFactory.Block(back, "잔량 바탕", new Vector2(0, 0), Vector2.zero, new Vector2(SlotWidth, 3), new Color(1, 1, 1, .15f));
                view.Fill = FpsUiFactory.Block(back, "잔량", new Vector2(0, 0), Vector2.zero, new Vector2(SlotWidth, 3), FpsUiFactory.Accent);
                slots.Add(view);
            }
            float width = values.Count * (SlotWidth + 6) - 6;
            for (int i = 0; i < slots.Count; i++)
            {
                var view = slots[i];
                bool used = i < values.Count;
                view.Back.gameObject.SetActive(used);
                if (!used) continue;
                var value = values[i];
                view.Back.rectTransform.anchoredPosition = new Vector2(-width * .5f + SlotWidth * .5f + i * (SlotWidth + 6), 0);
                view.Back.color = value.Active ? new Color(.1f, .12f, .15f, .7f) : new Color(0, 0, 0, .42f);
                view.Label.text = value.Label;
                view.Label.color = value.Active ? Color.white : new Color(1, 1, 1, .8f);
                view.Hint.text = value.Hint;
                view.Fill.enabled = view.FillBack.enabled = value.Fill.HasValue;
                if (value.Fill.HasValue) view.Fill.rectTransform.sizeDelta = new Vector2(SlotWidth * Mathf.Clamp01(value.Fill.Value), 3);
            }
        }

        private const float SlotWidth = 204;
        private string shownPrompt, shownSecondary;

        private void LateUpdate()
        {
            if (responder == null) return;
            bool paused = responder.IsPaused;
            canvas.enabled = !paused;
            if (paused) return;

            var interaction = responder.CurrentInteraction;
            string prompt = responder.CurrentPrompt ?? "", secondary = responder.CurrentSecondaryPrompt ?? "";
            if (interaction == null && prompt.Length == 0)
            {
                targetName.text = targetAction.text = "";
                shownPrompt = shownSecondary = null;
            }
            else
            {
                targetName.text = (interaction as IFpsNamed)?.DisplayName ?? "";
                bool available = prompt.StartsWith("E · ");
                // 문구가 바뀔 때만 다시 만든다(매 프레임 문자열을 새로 만들지 않는다).
                if (!ReferenceEquals(prompt, shownPrompt) || !ReferenceEquals(secondary, shownSecondary))
                {
                    shownPrompt = prompt; shownSecondary = secondary;
                    string first = available ? "<b>[E]</b> " + prompt.Substring(4) : prompt;
                    targetAction.text = secondary.Length > 0 ? first + "     <color=#FFFFFFFF><b>[R]</b> " + secondary.Substring(4) + "</color>" : first;
                }
                targetAction.color = available ? Color.white : new Color(1, 1, 1, .6f);
            }
            dot.color = interaction != null ? FpsUiFactory.Accent : new Color(1, 1, 1, .9f);
            toast.enabled = Time.unscaledTime < toastUntil;
            situation.color = situationColour;
            var v = vignette.color;
            v.a = Mathf.MoveTowards(v.a, danger * .85f, Time.unscaledDeltaTime * 1.5f);
            vignette.color = v;
        }
    }
}
