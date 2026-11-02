using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.TrainDeparture
{
    /// <summary>Screen-space instrument marks driven solely by interaction and audio state.</summary>
    [DisallowMultipleComponent]
    public sealed class DeparturePlayHud : MonoBehaviour
    {
        public TrainDepartureGame Game;
        public TMP_FontAsset Font;
        public DepartureItemSlots Slots;
        static readonly Color Ivory = new Color(.94f,.93f,.87f,.88f);
        static readonly Color Blue = new Color(.29f,.56f,.70f,.95f);
        RectTransform surface, travel, radio;
        Image travelFill;
        TMP_Text radioText, actionHint;
        readonly TMP_Text[] slotLabels = new TMP_Text[4];
        readonly Image[] slotSelection = new Image[4];
        string contextHint = "";
        TrainDepartureGame subscribedGame;
        bool holding, receiving, transmitting;
        float progress = -1;

        public void Build()
        {
            BindGame();
            if(surface != null) { Refresh(); return; }
            surface = Rect("Departure gameplay HUD", transform, Vector2.zero, Vector2.zero);
            surface.anchorMin = Vector2.zero; surface.anchorMax = Vector2.one;
            surface.offsetMin = surface.offsetMax = Vector2.zero;
            var canvas = surface.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = surface.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            // No raycaster: this overlay must never intercept world or menu input.
            Mark("Reticle", surface, Vector2.zero, new Vector2(2,2), Ivory);
            travel = Rect("Physical travel", surface, new Vector2(0,-28), new Vector2(22,2));
            Mark("Travel track", travel, Vector2.zero, new Vector2(22,2), new Color(.08f,.10f,.11f,.75f));
            travelFill = Mark("Travel position", travel, Vector2.zero, new Vector2(22,2), Blue);
            travelFill.rectTransform.pivot = new Vector2(0,.5f);
            travelFill.rectTransform.anchoredPosition = new Vector2(-11,0);
            radio = Rect("Radio activity", surface, new Vector2(-44,-40), new Vector2(30,16));
            radio.anchorMin = radio.anchorMax = Vector2.one;
            Mark("Radio antenna", radio, new Vector2(-12,5), new Vector2(1,8), Ivory);
            Mark("Radio body", radio, new Vector2(-12,-2), new Vector2(5,8), Ivory);
            var label = TMP_DefaultControls.CreateText(new TMP_DefaultControls.Resources());
            label.name = "Audio direction";
            radioText = label.GetComponent<TMP_Text>();
            Place(radioText.rectTransform, radio, new Vector2(4,0), new Vector2(24,16));
            if(Font != null) radioText.font = Font;
            radioText.fontSize = 11; radioText.alignment = TextAlignmentOptions.Midline;
            radioText.color = Ivory; radioText.raycastTarget = false;
            // Official uGUI 2.0.0 TMP_DefaultControls.CreateText retains its factory root.
            var hint = TMP_DefaultControls.CreateText(new TMP_DefaultControls.Resources());
            hint.name = "Aimed physical action";
            actionHint = hint.GetComponent<TMP_Text>();
            Place(actionHint.rectTransform, surface, new Vector2(0,-50), new Vector2(680,48));
            if(Font != null) actionHint.font = Font;
            actionHint.fontSize = 19; actionHint.alignment = TextAlignmentOptions.Midline;
            actionHint.color = Ivory; actionHint.raycastTarget = false;
            actionHint.text = "";
            // Reuse the installed TMP / UGUI factories, with one small factual equipment strip.
            string[] names = { "열쇠", "무전기", "가방", "빈손" };
            for (int i=0;i<4;i++)
            {
                var cell=Rect("Owned item slot "+(i+1),surface,new Vector2((i-1.5f)*86,38),new Vector2(80,32));
                cell.anchorMin=cell.anchorMax=new Vector2(.5f,0);
                slotSelection[i]=Mark("Selection",cell,new Vector2(0,-13),new Vector2(64,2),Blue);
                var node=TMP_DefaultControls.CreateText(new TMP_DefaultControls.Resources());
                var text=node.GetComponent<TMP_Text>(); slotLabels[i]=text;
                Place(text.rectTransform,cell,Vector2.zero,new Vector2(80,28));
                if(Font!=null)text.font=Font;
                text.fontSize=14;text.alignment=TextAlignmentOptions.Midline;text.raycastTarget=false;
                text.text=(i+1)+"  "+names[i];
            }
            Refresh();
        }

        public void SetGripAvailable(bool available, bool holding, float physicalProgress)
        {
            this.holding = holding;
            progress = float.IsNaN(physicalProgress) || float.IsInfinity(physicalProgress) ? -1 : physicalProgress;
            Refresh();
        }
        /// <summary>The interaction driver supplies one short action for the current physical aim.</summary>
        public void SetContextHint(string hint)
        { contextHint = hint ?? ""; Refresh(); }
        public void SetRadioState(bool receiving, bool transmitting)
        { this.receiving = receiving; this.transmitting = transmitting; Refresh(); }

        void BindGame()
        {
            if(subscribedGame == Game) return;
            UnbindGame(); subscribedGame = Game;
            contextHint = "";
            holding = receiving = transmitting = false; progress = -1;
            if(subscribedGame == null) return;
            subscribedGame.GripChanged += SetGripAvailable;
            subscribedGame.RadioChanged += SetRadioState;
            subscribedGame.Restarted += ClearGrip;
            receiving = subscribedGame.Receiving; transmitting = subscribedGame.Transmitting;
        }
        void UnbindGame()
        {
            if(subscribedGame == null) return;
            subscribedGame.GripChanged -= SetGripAvailable;
            subscribedGame.RadioChanged -= SetRadioState;
            subscribedGame.Restarted -= ClearGrip;
            subscribedGame = null;
        }
        void ClearGrip() { contextHint = ""; SetGripAvailable(false,false,-1); }
        void LateUpdate() { BindGame(); Refresh(); }
        void OnDestroy()
        {
            UnbindGame();
            if(surface == null) return;
            // Component-only destruction does not destroy children of its host.
            surface.gameObject.SetActive(false);
            Destroy(surface.gameObject);
            surface = null;
        }
        void Refresh()
        {
            if(surface == null) return;
            bool visible = Game != null && Game.Started && !Game.Completed && !Game.IsEmoting && Game.Player != null && !Game.Player.IsPaused;
            surface.gameObject.SetActive(visible);
            for(int i=0;i<4;i++)
            {
                bool available=Slots!=null&&Slots.IsAvailable(i+1);
                bool selected=Slots!=null&&Slots.SelectedSlot==i+1;
                slotLabels[i].color=available ? selected ? Blue : Ivory : new Color(.5f,.52f,.53f,.45f);
                slotSelection[i].gameObject.SetActive(selected);
            }
            // Negative values mean no measurable physical travel. Never animate from time.
            travel.gameObject.SetActive(holding && progress >= 0);
            travelFill.rectTransform.sizeDelta = new Vector2(22 * Mathf.Clamp01(progress),2);
            radio.gameObject.SetActive(receiving || transmitting);
            radioText.text = transmitting ? "TX" : "RX";
            radioText.color = transmitting ? Blue : Ivory;
            string hint = visible ? contextHint : "";
            actionHint.text = hint;
            actionHint.gameObject.SetActive(hint.Length != 0);
        }
        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var result = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            Place(result, parent, position, size); return result;
        }
        static void Place(RectTransform result, Transform parent, Vector2 position, Vector2 size)
        {
            result.SetParent(parent,false); result.anchorMin = result.anchorMax = new Vector2(.5f,.5f);
            result.sizeDelta = size; result.anchoredPosition = position;
        }
        static Image Mark(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var node = DefaultControls.CreateImage(new DefaultControls.Resources());
            node.name = name;
            var mark = node.GetComponent<Image>();
            Place(mark.rectTransform, parent, position, size);
            mark.color = color; mark.raycastTarget = false; return mark;
        }
    }
}
