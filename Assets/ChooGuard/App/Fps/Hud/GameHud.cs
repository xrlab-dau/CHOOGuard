using System.Collections.Generic;
using ChooGuard.App.Fps.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Hud
{
    /// <summary>
    /// In-shift HUD. PUBG-style edges (compass, radio feed, equipment, status) and My-Summer-Car-style centre: the looked-at object's name with,
    /// by guidance level, the action it offers (견학), its state and the input glyph (표준) or the glyph alone (실전). Nothing else stays on screen;
    /// details live behind Tab and M.
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
        private Image dot, holdRing, observeRing, vignette;
        private TMP_Text targetName, targetAction, toast, observation, clock, situation, hints;
        private readonly List<SlotView> slots = new List<SlotView>();
        private RectTransform slotRow;
        private float toastUntil, observationUntil, danger;
        private float? progress;
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
            observeRing = FpsUiFactory.Panel(Root, "살펴보기 고리", Vector2.zero, new Vector2(30, 30), new Color(.55f, .8f, 1f, .9f));
            observeRing.sprite = HudSprites.Ring;
            observeRing.type = Image.Type.Filled;
            observeRing.fillMethod = Image.FillMethod.Radial360;
            observeRing.fillOrigin = (int)Image.Origin360.Top;
            observeRing.enabled = false;
            targetName = FpsUiFactory.Label(Root, font, "대상 이름", new Vector2(0, -44), new Vector2(700, 28), 19);
            targetName.fontStyle = FontStyles.Bold;
            targetAction = FpsUiFactory.Label(Root, font, "대상 동작", new Vector2(0, -72), new Vector2(760, 26), 17);
            observation = FpsUiFactory.Label(Root, font, "살펴본 것", new Vector2(0, -108), new Vector2(900, 30), 17);
            observation.color = new Color(.72f, .88f, 1f, 1);
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

            hints = FpsUiFactory.Text(Root, font, "조작 안내", new Vector2(1, 0), new Vector2(-24, 28), new Vector2(620, 24), 14, TextAlignmentOptions.BottomRight);
            hints.color = new Color(1, 1, 1, .5f);
            RefreshHints();

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

        public void SetStatus(string clockText, string situationText, Color colour)
        {
            clock.text = clockText;
            situation.text = situationText;
            situationColour = colour;
        }

        public void SetDanger(float amount) => danger = Mathf.Clamp01(amount);

        /// <summary>The key hints in the corner for the current guidance level.</summary>
        public void RefreshHints() =>
            hints.text = GameSettings.Guided ? "Tab 상황판   M 안내도   Q 무전   Esc 메뉴" : "Tab 수첩   M 안내도   Q 무전   우클릭 살펴보기   휠 클릭 표시   Esc 메뉴";

        /// <summary>The hold ring for a tool (pin pull, AED): null hides it. A handle held by the hand draws its own travel over it.</summary>
        public void SetProgress(float? value) => progress = value;

        /// <summary>How far through looking closely the player is (null hides the small ring).</summary>
        public void SetObserveProgress(float? value)
        {
            observeRing.enabled = value.HasValue;
            if (value.HasValue) observeRing.fillAmount = Mathf.Clamp01(value.Value);
        }

        /// <summary>What looking closely showed: a line under the centre for a few seconds (the notebook keeps it).</summary>
        public void ShowObservation(string text, float seconds = 6f)
        {
            if (string.IsNullOrEmpty(text)) return;
            observation.text = text;
            observationUntil = Time.unscaledTime + seconds;
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
        private string shownPrompt, shownSecondary, shownName, shownState;
        private object shownTarget;
        private bool shownHoldable, shownHolding;
        private HoldStyle shownStyle;
        private GuidanceLevel shownLevel;

        private void LateUpdate()
        {
            if (responder == null) return;
            bool paused = responder.IsPaused;
            canvas.enabled = !paused;
            if (paused) return;

            // 손잡이를 잡은 동안은 시선이 문짝을 벗어나도 잡고 있는 것을 보인다.
            bool holding = responder.Holding;
            object interaction = holding ? responder.CurrentHold : responder.CurrentInteraction;
            string prompt = responder.CurrentPrompt ?? "", secondary = responder.CurrentSecondaryPrompt ?? "";
            if (interaction == null && prompt.Length == 0)
            {
                targetName.text = targetAction.text = "";
                shownPrompt = shownSecondary = shownName = shownState = null;
                shownTarget = null;
            }
            else
            {
                var level = GameSettings.Guidance;
                string name = (interaction as IFpsNamed)?.DisplayName ?? "";
                // 실전은 상태를 보여 주지 않는다(살펴보기로 안다).
                string state = level != GuidanceLevel.Expert ? (interaction as IFpsStated)?.StateText ?? "" : "";
                bool available = holding || prompt.StartsWith("E · ");
                bool holdable = responder.CurrentHoldable;
                var style = (interaction as IFpsHoldInteraction)?.HoldStyle ?? HoldStyle.Press;
                // 문구가 바뀔 때만 다시 만든다(매 프레임 문자열을 새로 만들지 않는다).
                if (!ReferenceEquals(name, shownName) || !ReferenceEquals(state, shownState) || !ReferenceEquals(interaction, shownTarget))
                {
                    shownName = name; shownState = state; shownTarget = interaction;
                    targetName.text = state.Length > 0 ? name + " <color=#FFFFFFB0>· " + state + "</color>" : name;
                }
                if (!ReferenceEquals(prompt, shownPrompt) || !ReferenceEquals(secondary, shownSecondary) || holdable != shownHoldable || holding != shownHolding || style != shownStyle || level != shownLevel)
                {
                    shownPrompt = prompt; shownSecondary = secondary; shownHoldable = holdable; shownHolding = holding; shownStyle = style; shownLevel = level;
                    targetAction.text = holding ? HoldGlyph(style) : level == GuidanceLevel.Guided ? GuidedAction(prompt, secondary, available) : Glyphs(prompt, secondary, available, holdable, style);
                }
                targetAction.color = available ? Color.white : new Color(1, 1, 1, .6f);
            }
            float? ring = progress;
            if (responder.Holding && responder.CurrentHold.HoldProgress >= 0) ring = responder.CurrentHold.HoldProgress;
            holdRing.enabled = ring.HasValue;
            if (ring.HasValue) holdRing.fillAmount = Mathf.Clamp01(ring.Value);
            dot.color = interaction != null ? FpsUiFactory.Accent : new Color(1, 1, 1, .9f);
            toast.enabled = Time.unscaledTime < toastUntil;
            observation.enabled = Time.unscaledTime < observationUntil;
            situation.color = situationColour;
            var v = vignette.color;
            v.a = Mathf.MoveTowards(v.a, danger * .85f, Time.unscaledDeltaTime * 1.5f);
            vignette.color = v;
        }

        // 견학: 지금 할 수 있는 동작 이름(현행).
        private static string GuidedAction(string prompt, string secondary, bool available)
        {
            string first = available ? "<b>[E]</b> " + prompt.Substring(4) : prompt;
            return secondary.Length > 0 ? first + "     <color=#FFFFFFFF><b>[R]</b> " + secondary.Substring(4) + "</color>" : first;
        }

        // 표준·실전: 동작 이름 대신 입력 모양만(손잡이는 누른 채 움직이는 방법, 열쇠는 R). 할 수 없을 때는 그 이유(보이는 상태)를 그대로 보인다.
        private static string Glyphs(string prompt, string secondary, bool available, bool holdable, HoldStyle style)
        {
            string first = available ? (holdable ? HoldGlyph(style) : "<b>[E]</b>") : prompt;
            return secondary.Length > 0 ? (first.Length > 0 ? first + "     " : "") + "<b>[R]</b>" : first;
        }

        // 손잡이를 다루는 입력 모양: 끌기·휠(1/4회전·레버), 끌기(여닫이), 원 그리기·휠(여러 바퀴 도는 밸브), 누르고 있기(누름 버튼).
        private static string HoldGlyph(HoldStyle style)
        {
            switch (style)
            {
                case HoldStyle.Crank: return "<b>[E 누른 채 · 원 그리기/휠]</b>";
                case HoldStyle.Swing: return "<b>[E 누른 채 · 끌기]</b>";
                case HoldStyle.Press: return "<b>[E 누른 채]</b>";
                default: return "<b>[E 누른 채 · 끌기/휠]</b>";
            }
        }
    }
}
