using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace ChooGuard.App.Fps.TrainDeparture
{
    // The live train is the dominant image. This canvas contains menus only; the game owns its HUD.
    // The main page is also the game's title: "기차 튜토리얼" starts this scene's departure game, "비상상황 연습" lists the
    // practice categories (PracticeCatalog: 화재, 환자 발생, …), then the scenarios of the chosen one, and loads the station
    // for the chosen scenario (PracticeShift). A detail box beside the list describes the focused row.
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
        TMP_Text title, subtitle;
        Button settingsButton;
        RectTransform detail;
        TMP_Text detailText;
        bool built;
        enum Page { None, Main, Pause, Settings, Complete, Practice, PracticeScenarios }
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
            subtitle = Label(content, "부산역·열차팀장", 127, 24, new Color(.70f, .70f, .68f));
            actions = Node(content, "Actions");
            Stretch(actions);
            var box = Graphic(panel, "Practice detail", new Color(Ground.r, Ground.g, Ground.b, .9f));
            detail = box.rectTransform;
            detail.anchorMin = detail.anchorMax = new Vector2(.37f, .5f);
            detail.pivot = new Vector2(0, 1);
            detail.anchoredPosition = new Vector2(0, 84);
            detail.sizeDelta = new Vector2(640, 360);
            detailText = Label(detail, "", 0, 24, Ink, 320);
            detailText.rectTransform.anchorMin = Vector2.zero; detailText.rectTransform.anchorMax = Vector2.one;
            detailText.rectTransform.pivot = new Vector2(.5f, .5f);
            detailText.rectTransform.offsetMin = new Vector2(32, 28); detailText.rectTransform.offsetMax = new Vector2(-32, -28);
            detailText.enableWordWrapping = true;
            detailText.alignment = TextAlignmentOptions.TopLeft;
            detail.gameObject.SetActive(false);
            ShowMain();
            if (SceneFlow.ConsumePracticeMenuRequest())
            {
                var last = PracticeCatalog.Group(PracticeCatalog.LastGroupId);
                if (last != null) ShowScenarios(last); else ShowPractice();
            }
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
            ShowDetail(null);
            menu.gameObject.SetActive(true);
            menu.alpha = 1; menu.interactable = menu.blocksRaycasts = true;
            title.text = heading;
            title.fontSize = next == Page.Complete ? 48 : 60;
            subtitle.text = next == Page.Main ? "부산역" : next == Page.Practice || next == Page.PracticeScenarios ? "부산역·역무원" : "부산역·열차팀장";
        }
        Button Action(string label, float y, Action action, bool primary = false, float height = 66, float size = 28)
        {
            var node = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            node.name = label; node.transform.SetParent(actions, false);
            var image = node.GetComponent<Image>();
            image.type = Image.Type.Simple; image.color = Color.white; image.raycastTarget = true;
            Position(image.rectTransform, y, ControlWidth, height);
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
            text.text = label; text.fontSize = size; text.color = Ink;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false; text.raycastTarget = false;
            Position(text.rectTransform, 0, ControlWidth - 44, height);
            text.rectTransform.anchoredPosition = new Vector2(22, 0);
            text.rectTransform.sizeDelta = new Vector2(ControlWidth - 44, height);
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
            Open(Page.Main, "훈련 선택");
            Action("기차 튜토리얼", 16, () => Game.Begin(), true);
            Action("비상상황 연습", -72, ShowPractice);
            settingsButton = Action("설정", -160, ShowSettings);
            Action("종료", -248, () => Game.ExitApplication());
            Finish();
        }
        const float RowStep = 54, RowHeight = 48, RowFont = 24, FirstRow = 60;
        /// <summary>One row of a practice list: focusing it (keyboard or pointer) shows <paramref name="about"/> in the detail box.</summary>
        Button Row(string label, float y, Action action, string about, bool primary = false)
        {
            // No "primary" blue row in a list: the focused row is the only highlight, so the detail box always matches it.
            var button = Action(label, y, action, false, RowHeight, RowFont);
            var colors = button.colors;
            colors.selectedColor = colors.highlightedColor = Blue;
            button.colors = colors;
            button.gameObject.AddComponent<MenuHint>().Show = () => ShowDetail(about);
            return button;
        }
        /// <summary>A 준비 중 row: grey text and a grey (not blue) focus, still focusable so its reason shows in the detail box.</summary>
        static void Dim(Button row)
        {
            var colors = row.colors;
            colors.selectedColor = colors.highlightedColor = colors.pressedColor = new Color(.24f, .24f, .24f);
            row.colors = colors;
            row.GetComponentInChildren<TMP_Text>().color = new Color(.55f, .55f, .53f);
        }
        void ShowDetail(string text)
        {
            if (detail == null) return;
            detail.gameObject.SetActive(!string.IsNullOrEmpty(text));
            detailText.text = text ?? "";
        }
        static string About(PracticeGroup group) =>
            "<b><size=30>" + group.Title + "</size></b>\n<color=#ffffffaa>" + group.Scenarios.Length + "가지 상황 · 지금 고를 수 있는 것 " +
            group.Scenarios.Count(scenario => scenario.Available) + "가지</color>\n\n" + group.Description +
            "\n\n<size=20><color=#ffffffaa>" + string.Join(" · ", group.Scenarios.Select(scenario => scenario.Title)) + "</color></size>";
        static string About(PracticeScenario scenario) =>
            "<b><size=30>" + scenario.Title + "</size></b>\n<color=#ffffffaa>" + (scenario.Available ? scenario.Duration +
            (scenario.Lesson != PracticeLesson.None ? " · 단계 안내·순서 점수" : " · 사건 한 건") : "준비 중") + "</color>\n\n" + scenario.Description +
            (scenario.Available ? "" : "\n\n<color=#e0b060>" + scenario.Unavailable + "</color>");
        /// <summary>The practice categories: 화재, 환자 발생, 보안, 시설 고장, 열차·선로, 지진.</summary>
        public void ShowPractice()
        {
            Open(Page.Practice, "비상상황 연습");
            float y = FirstRow;
            foreach (var group in PracticeCatalog.Groups)
            {
                var chosen = group;
                bool open = chosen.Scenarios.Any(scenario => scenario.Available);
                var row = Row(open ? chosen.Title : chosen.Title + " · 준비 중", y, () => ShowScenarios(chosen), About(chosen), y == FirstRow);
                row.gameObject.name = chosen.Title;
                if (!open) Dim(row);
                y -= RowStep;
            }
            Row("돌아가기", y - 12, ShowMain, null);
            Finish();
            ShowDetail(PracticeCatalog.Groups.Length > 0 ? About(PracticeCatalog.Groups[0]) : null);
        }
        /// <summary>The scenarios of one category; choosing one loads the station for it.</summary>
        public void ShowScenarios(PracticeGroup group)
        {
            Open(Page.PracticeScenarios, group.Title);
            float y = FirstRow;
            foreach (var scenario in group.Scenarios)
            {
                var chosen = scenario;
                var row = Row(chosen.Available ? chosen.Title : chosen.Title + " · 준비 중", y, () => StartPractice(group, chosen), About(chosen), y == FirstRow && chosen.Available);
                row.gameObject.name = chosen.Title;
                if (!chosen.Available) Dim(row);
                y -= RowStep;
            }
            Row("돌아가기", y - 12, ShowPractice, null);
            Finish();
            ShowDetail(group.Scenarios.Length > 0 ? About(group.Scenarios[0]) : null);
        }
        void StartPractice(PracticeGroup group, PracticeScenario scenario)
        {
            if (scenario == null || !scenario.Available || SceneFlow.Loading) return;
            if (!UnityEngine.Application.CanStreamedLevelBeLoaded(SceneFlow.StationScene))
            { Debug.LogError("[연습] 역 씬(" + SceneFlow.StationScene + ")이 빌드 목록에 없습니다."); return; }
            if (!PracticeShift.Arm(scenario.Id)) return;
            PracticeCatalog.LastGroupId = group.Id;
            Game.ReleaseForSceneChange();
            menu.interactable = false;
            ShowDetail(null);
            title.text = "불러오는 중";
            SceneFlow.StartShift(Font, null);
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
            else if (page == Page.PracticeScenarios) ShowPractice();
            else if (page == Page.Practice) ShowMain();
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

    /// <summary>Calls <see cref="Show"/> when its row is focused by keyboard or pointer (the practice detail box).</summary>
    sealed class MenuHint : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        public Action Show;
        public void OnSelect(BaseEventData data) => Show?.Invoke();
        public void OnPointerEnter(PointerEventData data) => Show?.Invoke();
    }
}
