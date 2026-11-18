using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace ChooGuard.App.Fps.TrainDeparture
{
    // The live train is the dominant image. This canvas contains menus only; the game owns its HUD.
    public sealed class DepartureGameView : MonoBehaviour
    {
        public TrainDepartureGame Game;
        public TMP_FontAsset Font;
        public bool MenuVisible { get; private set; }

        static readonly Color Ink = new Color(.94f, .94f, .91f);
        static readonly Color Ground = new Color(.055f, .055f, .055f);
        static readonly Color Blue = new Color(.12f, .36f, .55f);
        const float ControlWidth = 420f;
        readonly List<Selectable> controls = new List<Selectable>();
        CanvasGroup menu;
        RectTransform actions;
        TMP_Text title;
        Button settingsButton;
        bool built;
        enum Page { None, Main, Pause, Settings, Complete }
        Page page, settingsOrigin;

        public void Build()
        {
            if (built) return;
            built = true;
            var root = new GameObject("Departure menus", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Departure menu input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var panel = Node(root.transform, "Menu");
            Stretch(panel);
            menu = panel.gameObject.AddComponent<CanvasGroup>();
            // Capture clicks outside the menu so they cannot reach gameplay UI beneath it.
            var blocker = Graphic(panel, "Input shield", Color.clear, true);
            Stretch(blocker.rectTransform);
            var ground = Graphic(panel, "Menu ground", Ground);
            ground.rectTransform.anchorMin = Vector2.zero;
            ground.rectTransform.anchorMax = new Vector2(.35f, 1);
            ground.rectTransform.offsetMin = ground.rectTransform.offsetMax = Vector2.zero;
            var content = Node(panel, "Menu content");
            content.anchorMin = content.anchorMax = new Vector2(.06f, .5f);
            content.pivot = new Vector2(0, .5f);
            content.sizeDelta = new Vector2(480, 700);
            Label(content, "CHOOGuard", 300, 26, Ink);
            title = Label(content, "출발 준비", 202, 60, Ink, 90);
            title.fontStyle = FontStyles.Bold;
            Label(content, "부산역·열차팀장", 127, 24, new Color(.70f, .70f, .68f));
            actions = Node(content, "Actions");
            Stretch(actions);
            ShowMain();
        }

        static RectTransform Node(Transform parent, string name)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return (RectTransform)node.transform;
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        static void Position(RectTransform rect, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(width, height);
        }
        static Image Graphic(Transform parent, string name, Color color, bool hit = false)
        {
            var node = DefaultControls.CreateImage(new DefaultControls.Resources());
            node.name = name; node.transform.SetParent(parent, false);
            var image = node.GetComponent<Image>();
            image.color = color; image.raycastTarget = hit;
            return image;
        }
        TMP_Text Label(Transform parent, string text, float y, float size, Color color, float height = 44)
        {
            var node = TMP_DefaultControls.CreateText(new TMP_DefaultControls.Resources());
            node.name = text; node.transform.SetParent(parent, false);
            var label = node.GetComponent<TMP_Text>();
            Position(label.rectTransform, y, 480, height);
            if (Font != null) label.font = Font;
            label.text = text; label.fontSize = size; label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }
        void Open(Page next, string heading)
        {
            if (!built) Build();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            controls.Clear(); settingsButton = null;
            for (int i = actions.childCount - 1; i >= 0; i--)
            {
                var child = actions.GetChild(i).gameObject;
                child.SetActive(false); Destroy(child);
            }
            page = next; MenuVisible = true;
            menu.gameObject.SetActive(true);
            menu.alpha = 1; menu.interactable = menu.blocksRaycasts = true;
            title.text = heading;
            title.fontSize = next == Page.Complete ? 48 : 60;
        }
        Button Action(string label, float y, Action action, bool primary = false)
        {
            var node = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            node.name = label; node.transform.SetParent(actions, false);
            var image = node.GetComponent<Image>();
            image.type = Image.Type.Simple; image.color = Color.white; image.raycastTarget = true;
            Position(image.rectTransform, y, ControlWidth, 66);
            var button = node.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = primary ? Blue : Color.clear;
            colors.highlightedColor = primary ? new Color(.18f, .46f, .66f) : new Color(.20f, .20f, .20f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? new Color(.09f, .26f, .39f) : new Color(.30f, .30f, .30f);
            colors.fadeDuration = .08f;
            button.colors = colors;
            var text = node.GetComponentInChildren<TMP_Text>();
            if (Font != null) text.font = Font;
            text.text = label; text.fontSize = 28; text.color = Ink;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false; text.raycastTarget = false;
            Position(text.rectTransform, 0, ControlWidth - 44, 66);
            text.rectTransform.anchoredPosition = new Vector2(22, 0);
            text.rectTransform.sizeDelta = new Vector2(ControlWidth - 44, 66);
            button.onClick.AddListener(() => action());
            controls.Add(button);
            return button;
        }
        void Finish(Selectable focus = null)
        {
            // Explicit vertical navigation leaves left/right free to adjust the sliders.
            for (int i = 0; i < controls.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = controls[(i + controls.Count - 1) % controls.Count];
                nav.selectOnDown = controls[(i + 1) % controls.Count];
                controls[i].navigation = nav;
            }
            if (EventSystem.current != null && controls.Count > 0)
                EventSystem.current.SetSelectedGameObject((focus != null ? focus : controls[0]).gameObject);
        }
        public void ShowMain()
        {
            Open(Page.Main, "출발 준비");
            Action("시작", 16, () => Game.Begin(), true);
            settingsButton = Action("설정", -72, ShowSettings);
            Action("종료", -160, () => Game.ExitApplication());
            Finish();
        }
        public void ShowPause()
        {
            if (MenuVisible && (page == Page.Pause || (page == Page.Settings && settingsOrigin == Page.Pause))) return;
            Open(Page.Pause, "일시정지");
            Action("계속", 16, () => Game.Resume(), true);
            Action("다시 시작", -66, () => Game.Restart());
            settingsButton = Action("설정", -148, ShowSettings);
            Action("종료", -230, () => Game.ExitApplication());
            Finish();
        }
        public void ShowComplete()
        {
            Open(Page.Complete, "출발 준비 완료");
            Action("다시 시작", 16, () => Game.Restart(), true);
            Action("종료", -72, () => Game.ExitApplication());
            Finish();
        }
        void ShowSettings()
        {
            settingsOrigin = page;
            Open(Page.Settings, "설정");
            Setting("소리", 14, 0, 1, AudioListener.volume,
                v => AudioListener.volume = v, v => Mathf.RoundToInt(v * 100) + "%");
            float sensitivity = Game != null && Game.Player != null ? Game.Player.LookDegreesPerPixel : .08f;
            Setting("시점 감도", -122, .025f, .18f, sensitivity,
                v => { if (Game != null && Game.Player != null) Game.Player.LookDegreesPerPixel = v; },
                v => (v / .08f).ToString("0.00") + "×");
            Action("돌아가기", -290, ReturnFromSettings);
            Finish();
        }
        void Setting(string name, float y, float min, float max, float value,
            UnityEngine.Events.UnityAction<float> changed, Func<float, string> format)
        {
            Label(actions, name, y, 26, Ink);
            var readout = Label(actions, format(value), y, 24, Ink);
            readout.rectTransform.sizeDelta = new Vector2(ControlWidth, 44);
            readout.alignment = TextAlignmentOptions.MidlineRight;
            var node = DefaultControls.CreateSlider(new DefaultControls.Resources());
            node.name = name + " slider"; node.transform.SetParent(actions, false);
            var slider = node.GetComponent<Slider>();
            var rect = node.GetComponent<RectTransform>();
            Position(rect, y - 50, ControlWidth, 44);
            var fill = slider.fillRect.GetComponent<Image>();
            var handle = (Image)slider.targetGraphic;
            // Keep the factory's control/handle hierarchy; this design has no colored fill.
            foreach (var graphic in node.GetComponentsInChildren<Image>())
            {
                graphic.type = Image.Type.Simple;
                if (graphic == handle || graphic == fill) continue;
                graphic.color = new Color(.28f, .28f, .28f); graphic.raycastTarget = false;
                Stretch(graphic.rectTransform);
                graphic.rectTransform.offsetMin = new Vector2(10, 19);
                graphic.rectTransform.offsetMax = new Vector2(-10, -19);
            }
            fill.gameObject.SetActive(false); slider.fillRect = null;
            var hit = node.AddComponent<Image>(); // Factory has no root graphic: preserve the 44px hit target.
            hit.color = Color.clear; hit.raycastTarget = true;
            var travel = (RectTransform)handle.transform.parent;
            Stretch(travel); travel.offsetMin = new Vector2(10, 0); travel.offsetMax = new Vector2(-10, 0);
            handle.color = Color.white; handle.raycastTarget = true;
            // Slider stretches its handle anchors vertically; -14 gives a 30px handle in the 44px root.
            handle.rectTransform.sizeDelta = new Vector2(20, -14);
            slider.minValue = min; slider.maxValue = max;
            var colors = slider.colors;
            colors.normalColor = Ink; colors.highlightedColor = new Color(.37f, .66f, .86f);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = Blue;
            colors.fadeDuration = .08f; slider.colors = colors;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v => { changed(v); readout.text = format(v); });
            controls.Add(slider);
        }
        void ReturnFromSettings()
        {
            Page origin = settingsOrigin;
            page = Page.None; // Allow ShowPause to rebuild, rather than retaining the settings page.
            if (origin == Page.Pause) ShowPause();
            else if (origin == Page.Complete) ShowComplete();
            else ShowMain();
            if (settingsButton != null) settingsButton.Select();
        }
        // The responder handles pause first; the later root update routes cancel for visible menus
        // and keeps the player paused while a menu remains visible.
        // Return true means the menu consumed the input (including main/completion, which stay open).
        public bool HandleCancel()
        {
            if (!MenuVisible) return false;
            if (page == Page.Settings) ReturnFromSettings();
            else if (page == Page.Pause) Game.Resume();
            return true;
        }
        public void HideMenu()
        {
            MenuVisible = false; page = Page.None;
            if (!built) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            menu.interactable = menu.blocksRaycasts = false;
            menu.gameObject.SetActive(false);
        }
    }
}
