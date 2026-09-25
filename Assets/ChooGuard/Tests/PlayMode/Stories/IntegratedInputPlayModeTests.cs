#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ChooGuard.Presentation.Input;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode.Stories
{
    // Canvas/TMP/EventSystem 이벤트 경계만 통합: 실제 World/operations backend 검증이 아니다.
    // injected composition 시험은 OS IME 수용을 대체하지 않는다.
    public sealed class IntegratedInputPlayModeTests
    {
        private GameObject canvasRoot;
        private GameObject inputRoot;
        private EventSystem eventSystem;
        private InputContextRouter router;
        private InputActionAsset source;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings previousSettings;
        private InputSettings testSettings;
        private readonly List<RoutedInput> background = new List<RoutedInput>();
        private readonly List<EventSystem> suspendedSystems = new List<EventSystem>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DrainNativeEvents();
            foreach (var system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                if (!system.enabled) continue;
                suspendedSystems.Add(system);
                system.enabled = false;
            }
            // Replacing HideAndDontSave settings destroys the old instance in InputManager.
            // Snapshot temporary defaults; retain a persistent project asset by reference.
            previousSettings = InputSystem.settings.hideFlags == HideFlags.HideAndDontSave
                ? Object.Instantiate(InputSystem.settings) : InputSystem.settings;
            testSettings = Object.Instantiate(previousSettings);
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testSettings;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            inputRoot = new GameObject("입력 통합 시험", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = inputRoot.GetComponent<EventSystem>();
            router = inputRoot.AddComponent<InputContextRouter>();
            source = InputActionAsset.FromJson(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "ChooGuard/Presentation/Input/Operations.inputactions")));
            source.devices = new InputDevice[] { mouse, keyboard };
            router.Configure(eventSystem, inputRoot.GetComponent<InputSystemUIInputModule>(), source);
            router.RuntimeActions.devices = new InputDevice[] { mouse, keyboard };
            router.BackgroundInput += background.Add;
            canvasRoot = new GameObject("시험 Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasRoot.GetComponent<Canvas>().sortingOrder = 1000;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                Object.Destroy(inputRoot);
                Object.Destroy(canvasRoot);
                Object.Destroy(source);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                yield return null;
            }
            finally
            {
                // 부분 설정 또는 시험 실패에도 기존 시스템과 설정을 복원한다.
                foreach (var system in suspendedSystems) if (system != null) system.enabled = true;
                suspendedSystems.Clear();
                if (previousSettings != null) InputSystem.settings = previousSettings;
                Object.Destroy(testSettings);
                previousSettings = testSettings = null;
                mouse = null;
                keyboard = null;
                background.Clear();
                DrainNativeEvents();
            }
        }

        private GameObject Panel(string name, Transform parent = null)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent == null ? canvasRoot.transform : parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(220, 120);
            return panel;
        }

        private TMP_InputField TextField(Transform parent)
        {
            var panel = Panel("TMP 입력", parent);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(panel.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(viewport.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.raycastTarget = false;
            var rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var field = panel.AddComponent<TMP_InputField>();
            field.textViewport = viewportRect;
            field.textComponent = text;
            field.targetGraphic = panel.GetComponent<Image>();
            field.text = "CHOOGuard";
            return field;
        }

        private static int DrainNativeEvents()
        {
            // 프로세스 공용 큐를 제한된 횟수로 비워 다른 시험의 키가 결과를 대신하지 않게 한다.
            const int limit = 4096;
            var nativeEvent = new Event();
            var count = 0;
            while (count < limit && Event.PopEvent(nativeEvent)) count++;
            Assert.That(count, Is.LessThan(limit), "Native event queue drain exceeded its bounded limit");
            return count;
        }

        private static void QueueTmpNativeKey(KeyCode key)
        {
            // KeyboardState와 별개인 실제 TMP 이벤트 큐에 주입하며 콜백을 직접 호출하지 않는다.
            var enqueue = typeof(Event).GetMethod("QueueEvent", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(Event) }, null);
            Assert.That(enqueue, Is.Not.Null, "Installed Unity must provide the native event queue seam");
            enqueue.Invoke(null, new object[] { new Event { type = EventType.KeyDown, keyCode = key } });
        }

        private int ProcessTmpNativeKey(TMP_InputField field, KeyCode key)
        {
            try
            {
                Assert.That(field.isFocused, Is.True);
                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(field.gameObject));
                var staleCount = DrainNativeEvents();
                QueueTmpNativeKey(key);
                // IMGUI가 먼저 소비하지 않도록 주입과 실제 OnUpdateSelected 처리를 동기로 잇는다.
                inputRoot.GetComponent<InputSystemUIInputModule>().Process();
                Assert.That(DrainNativeEvents(), Is.Zero, "TMP must consume the injected native event completely");
                Assert.That(field.isFocused, Is.False, "TMP OnUpdateSelected must consume the queued finish key");
                return staleCount;
            }
            finally
            {
                DrainNativeEvents();
            }
        }

        [UnityTest]
        public IEnumerator TmpEscape_ClearsStaleReturn_AndCancelsExactlyOnce()
        {
            var field = TextField(canvasRoot.transform);
            var cancelledEdits = 0;
            var submits = 0;
            field.onEndEdit.AddListener(_ => { if (field.wasCanceled) cancelledEdits++; });
            field.onSubmit.AddListener(_ => submits++);
            eventSystem.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
            yield return null;
            yield return null;
            router.RefreshContext();
            try
            {
                DrainNativeEvents();
                // 오래된 Return을 먼저 넣어도 그것이 새 Escape를 대신 처리해서는 안 된다.
                QueueTmpNativeKey(KeyCode.Return);
                Assert.That(ProcessTmpNativeKey(field, KeyCode.Escape), Is.EqualTo(1));
                Assert.That(field.wasCanceled, Is.True);
                Assert.That(cancelledEdits, Is.EqualTo(1));
                Assert.That(submits, Is.Zero);
                yield return null;
                Assert.That(cancelledEdits, Is.EqualTo(1));
                Assert.That(submits, Is.Zero);
                Assert.That(background, Is.Empty);
                Assert.That(DrainNativeEvents(), Is.Zero);
            }
            finally
            {
                DrainNativeEvents();
            }
        }

        [UnityTest]
        public IEnumerator CloneAndModule_ActualDisableReleasesOwnership_AndEnableRebinds()
        {
            var module = inputRoot.GetComponent<InputSystemUIInputModule>();
            var runtime = router.RuntimeActions;
            Assert.That(module.actionsAsset, Is.SameAs(runtime));
            Assert.That(module.leftClick.action.actionMap.asset, Is.SameAs(runtime));
            Assert.That(runtime, Is.Not.SameAs(source));
            Assert.That(router.RoutePointerDown(501, new Vector2(-100, -100)), Is.EqualTo(InputContext.Camera));
            Assert.That(router.CapturedPointerCount, Is.EqualTo(1));
            background.Clear();
            router.enabled = false; // Actual Unity lifecycle, no SendMessage or callback invocation.
            Assert.That(router.RuntimeActions, Is.Null);
            Assert.That(runtime.enabled, Is.False);
            Assert.That(module.enabled, Is.False);
            Assert.That(module.actionsAsset, Is.Null);
            Assert.That(router.CapturedPointerCount, Is.Zero);
            Assert.That(router.RoutePointerUp(501, Vector2.zero), Is.EqualTo(InputContext.None));
            Assert.That(background, Is.Empty);
            yield return null; // Destroy is deferred in PlayMode.
            Assert.That(runtime == null, Is.True);
            router.enabled = true;
            Assert.That(router.RuntimeActions, Is.Not.Null);
            Assert.That(module.actionsAsset, Is.SameAs(router.RuntimeActions));
            Assert.That(module.leftClick.action.actionMap.asset, Is.SameAs(router.RuntimeActions));
            Assert.That(module.cancel, Is.Null);
            Assert.That(source.enabled, Is.False);
            yield return null;
            yield return null;
            Assert.That(router.RuntimeActions.FindActionMap("Game").enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator UiDragOutsideAndWheel_NeverBecomeWorldSelectionOrZoom()
        {
            var panel = Panel("ScrollRect HUD");
            var scroll = panel.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(panel.transform, false);
            scroll.content = content.GetComponent<RectTransform>();
            scroll.content.sizeDelta = new Vector2(220, 500);
            scroll.viewport = panel.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            yield return null;
            var inside = (Vector2)panel.transform.position;
            var outside = new Vector2(-50, -50);
            Assert.That(router.RoutePointerDown(100, inside), Is.EqualTo(InputContext.Hud));
            Assert.That(router.RoutePointerMove(100, outside), Is.EqualTo(InputContext.Hud));
            Assert.That(router.RouteScroll(100, outside, Vector2.up), Is.EqualTo(InputContext.Hud));
            Assert.That(router.RoutePointerUp(100, outside), Is.EqualTo(InputContext.Hud));
            Assert.That(router.RouteScroll(101, inside, Vector2.up), Is.EqualTo(InputContext.Hud));
            Assert.That(background, Is.Empty);
            router.RoutePointerDown(102, outside);
            router.RoutePointerUp(102, outside);
            Assert.That(background.Count, Is.EqualTo(2), "월드 정상 입력을 모두 막은 구현은 통과하지 않는다");
            Assert.That(background[1].Kind, Is.EqualTo(RoutedInputKind.PointerUp));
        }

        [UnityTest]
        public IEnumerator MouseActions_UseRealRaycast_AndCancelledCaptureCannotLeakRelease()
        {
            var panel = Panel("HUD");
            Canvas.ForceUpdateCanvases();
            yield return null;
            var inside = (Vector2)panel.transform.position;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = inside }.WithButton(MouseButton.Left));
            yield return null;
            Assert.That(router.CapturedPointerCount, Is.EqualTo(1));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-50, -50) }.WithButton(MouseButton.Left));
            yield return null;
            router.CancelAllCaptures();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(-50, -50) });
            yield return null;
            Assert.That(router.CapturedPointerCount, Is.Zero);
            Assert.That(background, Is.Empty);
        }

        [UnityTest]
        public IEnumerator FocusedTmp_BlocksWasdSpace_AndEscapeDoesNotCloseItsModal()
        {
            var previous = Panel("복귀 선택");
            eventSystem.SetSelectedGameObject(previous);
            var modal = Panel("모달");
            var field = TextField(modal.transform);
            var cancelledEdits = 0;
            var closed = 0;
            field.onEndEdit.AddListener(_ => { if (field.wasCanceled) cancelledEdits++; });
            router.ModalClosed += _ => closed++;
            router.OpenModal(modal, field.gameObject);
            field.ActivateInputField();
            yield return null;
            yield return null;
            Assert.That(field.isFocused, Is.True);
            router.RefreshContext();
            Assert.That(router.RuntimeActions.FindActionMap("Game").enabled, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space, Key.Escape));
            yield return null;
            ProcessTmpNativeKey(field, KeyCode.Escape);
            yield return null;
            Assert.That(background, Is.Empty);
            Assert.That(router.ActiveModal, Is.SameAs(modal));
            Assert.That(field.wasCanceled, Is.True);
            Assert.That(cancelledEdits, Is.EqualTo(1), "TMP native Escape must end the cancelled edit exactly once");
            Assert.That(closed, Is.Zero);
            Assert.That(inputRoot.GetComponent<InputSystemUIInputModule>().cancel, Is.Null);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            field.DeactivateInputField();
            eventSystem.SetSelectedGameObject(modal);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            Assert.That(router.ActiveModal, Is.Null);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(previous));
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(cancelledEdits, Is.EqualTo(1));
            Assert.That(background, Is.Empty, "닫기에 소비한 Esc를 복귀한 배경으로 보내면 안 된다");
        }

        [UnityTest]
        public IEnumerator RuntimeClone_PhysicalEscapeClosesOnce_ThenWorldCancelRearmsOnlyAfterNeutral()
        {
            var module = inputRoot.GetComponent<InputSystemUIInputModule>();
            Assert.That(module.actionsAsset, Is.SameAs(router.RuntimeActions));
            Assert.That(module.actionsAsset, Is.Not.SameAs(source));
            Assert.That(source.actionMaps.Any(map => map.enabled), Is.False);
            Assert.That(module.cancel, Is.Null);
            var cancel = router.RuntimeActions.FindAction("UI/Cancel", true);
            Assert.That(cancel.enabled, Is.True);
            Assert.That(cancel.bindings.Any(x => x.effectivePath == "<Keyboard>/escape"), Is.True);
            var previous = Panel("previous");
            eventSystem.SetSelectedGameObject(previous);
            var modal = Panel("modal");
            var closes = 0;
            router.ModalClosed += _ => {
                closes++;
                Assert.That(router.TryRouteShortcut(RoutedInputKind.Pause, Vector2.zero), Is.False);
                Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.None));
            };
            router.OpenModal(modal);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.Space, Key.W));
            yield return null;
            yield return null;
            Assert.That(closes, Is.EqualTo(1));
            Assert.That(router.ActiveModal, Is.Null);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(previous));
            Assert.That(background, Is.Empty);
            router.SetApplicationFocus(false);
            Assert.That(cancel.enabled, Is.False);
            router.SetApplicationFocus(true);
            yield return null;
            yield return null;
            Assert.That(background, Is.Empty);
            Assert.That(router.RuntimeActions.FindActionMap("Game").enabled, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            Assert.That(router.RuntimeActions.FindActionMap("Game").enabled, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.Space));
            yield return null;
            yield return null;
            Assert.That(background.Count, Is.EqualTo(1));
            Assert.That(background[0].Kind, Is.EqualTo(RoutedInputKind.Cancel));
            Assert.That(background[0].Owner.Context, Is.EqualTo(InputContext.World));
            Assert.That(closes, Is.EqualTo(1));
            Assert.That(source.actionMaps.Any(map => map.enabled), Is.False);
        }

        [UnityTest]
        public IEnumerator ActualButtons_AreIndependent_AndUiCaptureBlocksWheelOutside()
        {
            var panel = Panel("HUD");
            Canvas.ForceUpdateCanvases();
            yield return null;
            var inside = (Vector2)panel.transform.position;
            var outside = new Vector2(-50, -50);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = inside }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = outside, scroll = Vector2.up }
                .WithButton(MouseButton.Left).WithButton(MouseButton.Right));
            yield return null;
            Assert.That(router.CapturedPointerCount, Is.EqualTo(2));
            Assert.That(background.Exists(x => x.Kind == RoutedInputKind.Scroll), Is.False);
            Assert.That(background.Exists(x => x.Kind == RoutedInputKind.PointerDown), Is.True);
            background.Clear();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = outside }.WithButton(MouseButton.Right));
            yield return null;
            Assert.That(router.CapturedPointerCount, Is.EqualTo(1));
            Assert.That(background.Exists(x => x.Kind == RoutedInputKind.PointerUp), Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = outside });
            yield return null;
            Assert.That(router.CapturedPointerCount, Is.Zero);
            Assert.That(background.Exists(x => x.Kind == RoutedInputKind.PointerUp), Is.True);
        }

        [UnityTest]
        public IEnumerator TmpFocusIsRestored_AfterInjectedCompositionAndModalClose()
        {
            var field = TextField(canvasRoot.transform);
            eventSystem.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
            yield return null;
            yield return null;
            Assert.That(field.isFocused, Is.True);
            var modal = Panel("modal");
            router.OpenModal(modal, null);
            router.SetCompositionActive(true);
            Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.Text));
            router.SetCompositionActive(false);
            Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.Text));
            yield return null;
            Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.Modal));
            Assert.That(router.RouteCancel(), Is.EqualTo(InputContext.None));
            yield return null;
            Assert.That(modal.activeSelf, Is.False);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(field.gameObject));
            Assert.That(field.isFocused, Is.True);
            Assert.That(background, Is.Empty);
        }

        [UnityTest]
        public IEnumerator TmpEnter_UsesNativeSubmitOnce_WithoutBackgroundPause()
        {
            var field = TextField(canvasRoot.transform);
            var submits = 0;
            field.onSubmit.AddListener(_ => submits++);
            eventSystem.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
            yield return null;
            yield return null;
            router.RefreshContext();
            Assert.That(inputRoot.GetComponent<InputSystemUIInputModule>().submit, Is.Null);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter, Key.Space));
            yield return null;
            ProcessTmpNativeKey(field, KeyCode.Return);
            yield return null;
            Assert.That(submits, Is.EqualTo(1));
            Assert.That(background, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DeviceRemoval_CancelsCapture_AndDisabledRouterDoesNotSubscribe()
        {
            var outside = new Vector2(-50, -50);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = outside }.WithButton(MouseButton.Left));
            yield return null;
            Assert.That(router.CapturedPointerCount, Is.EqualTo(1));
            InputSystem.RemoveDevice(mouse);
            mouse = null;
            Assert.That(router.CapturedPointerCount, Is.Zero);
            background.Clear();
            var cancellations = 0;
            router.InputsCancelled += () => cancellations++;
            router.enabled = false;
            cancellations = 0;
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = outside }.WithButton(MouseButton.Left));
            yield return null;
            Assert.That(cancellations, Is.Zero);
            Assert.That(background, Is.Empty);
            Assert.That(router.RuntimeActions, Is.Null);
        }

        [UnityTest]
        public IEnumerator RenamedCancelAction_PhysicalEscapeHasOneOwner()
        {
            router.enabled = false;
            source.FindAction("UI/Cancel", true).Rename("Back");
            router.enabled = true;
            var modal = Panel("modal");
            router.OpenModal(modal, null);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.Space));
            yield return null;
            yield return null;
            Assert.That(modal.activeSelf, Is.False);
            Assert.That(router.ActiveModal, Is.Null);
            Assert.That(background, Is.Empty);
        }

        [UnityTest]
        public IEnumerator HeldWasdAcrossFocusLoss_MustReturnNeutralBeforeGameRearms()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            yield return null;
            Assert.That(background.Exists(x => x.Kind == RoutedInputKind.Move), Is.True);
            background.Clear();
            router.SetApplicationFocus(false);
            router.SetApplicationFocus(true);
            yield return null;
            yield return null;
            Assert.That(background, Is.Empty);
            Assert.That(router.RuntimeActions.FindActionMap("Game").enabled, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            Assert.That(background, Is.Empty);
            Assert.That(router.RuntimeActions.FindActionMap("Game").enabled, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            yield return null;
            Assert.That(background.Exists(x => x.Kind == RoutedInputKind.Move), Is.True);
        }
    }
}
#endif
