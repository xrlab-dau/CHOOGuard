using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChooGuard.Presentation.Input
{
    public enum RoutedInputKind { PointerDown, PointerMove, PointerUp, Scroll, Move, Pause, Cancel }

    public readonly struct RoutedInput
    {
        public InputOwner Owner { get; }
        public RoutedInputKind Kind { get; }
        public Vector2 Value { get; }
        public RoutedInput(InputOwner owner, RoutedInputKind kind, Vector2 value)
        { Owner = owner; Kind = kind; Value = value; }
    }

    // Boundary events only: no World command, camera or backend is fabricated here.
    [DefaultExecutionOrder(-100)]
    public sealed class InputContextRouter : MonoBehaviour
    {
        [SerializeField] private InputActionAsset operations;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule inputModule;
        private readonly PointerCaptureOwner capture = new PointerCaptureOwner();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private readonly List<InputActionReference> references = new List<InputActionReference>();
        private readonly Dictionary<InputControl, int> streams = new Dictionary<InputControl, int>();
        private readonly List<int> movingStreams = new List<int>();
        private InputActionMap ui;
        private InputActionMap game;
        private InputActionReference submitReference;
        private Keyboard keyboard;
        private int nextStream;
        private bool compositionActive;
        private bool focused = true;
        private bool waitForNeutral;
        private int blockedFrame = -1;
        private int compositionFrame = -1;
        private int cancelFrame = -1;
        private bool pendingPause;
        private GameObject previousSelection;
        private GameObject modal;
        private bool bound;

        public InputActionAsset RuntimeActions { get; private set; }
        public GameObject ActiveModal => modal;
        public int CapturedPointerCount => capture.Count;
        public InputContext KeyboardOwner => TextOwnsKeyboard ? InputContext.Text : modal != null ? InputContext.Modal : InputContext.World;
        public Func<Vector2, bool> WorldHitTest { get; set; }
        public event Action<RoutedInput> BackgroundInput;
        public event Action InputsCancelled;
        public event Action<GameObject> ModalClosed;

        public void Configure(EventSystem system, InputSystemUIInputModule module, InputActionAsset source)
        {
            if (system == null || module == null || source == null) throw new ArgumentNullException();
            if (module.gameObject != system.gameObject) throw new ArgumentException("Module must belong to the configured EventSystem.");
            Unbind();
            eventSystem = system;
            inputModule = module;
            operations = source;
            if (isActiveAndEnabled) Bind();
        }

        private void OnEnable() { Bind(); }
        private void OnDisable() { Quarantine(); Unbind(); }
        private void OnDestroy() { Unbind(); }
        private void Bind()
        {
            if (bound || operations == null || eventSystem == null || inputModule == null) return;
            RuntimeActions = Instantiate(operations);
            RuntimeActions.devices = operations.devices;
            RuntimeActions.Disable();
            ui = RuntimeActions.FindActionMap("UI", true);
            game = RuntimeActions.FindActionMap("Game", true);
            inputModule.enabled = false;
            inputModule.point = inputModule.leftClick = inputModule.rightClick = inputModule.middleClick = null;
            inputModule.scrollWheel = inputModule.move = inputModule.submit = inputModule.cancel = null;
            inputModule.trackedDevicePosition = inputModule.trackedDeviceOrientation = null;
            inputModule.actionsAsset = RuntimeActions;
            inputModule.point = Reference("Point");
            inputModule.leftClick = Reference("Click");
            inputModule.rightClick = Reference("RightClick");
            inputModule.middleClick = Reference("MiddleClick");
            inputModule.scrollWheel = Reference("ScrollWheel");
            inputModule.move = Reference("Navigate");
            submitReference = Reference("Submit");
            inputModule.submit = submitReference;
            // The router owns cancellation, independent of the action's name. TMP processes
            // its native Escape/Enter; forwarding ICancelHandler as well would double consume.
            inputModule.cancel = null;
            ui.actionTriggered += OnUiAction;
            game.actionTriggered += OnGameAction;
            InputSystem.onDeviceChange += OnDeviceChange;
            bound = true;
            BindKeyboard();
            inputModule.enabled = true;
            RefreshContext();
        }
        private InputActionReference Reference(string name)
        {
            var reference = InputActionReference.Create(ui.FindAction(name, true));
            references.Add(reference);
            return reference;
        }
        private static void DisposeObject(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void Unbind()
        {
            if (keyboard != null) keyboard.onIMECompositionChange -= OnComposition;
            keyboard = null;
            if (!bound) return;
            bound = false;
            InputSystem.onDeviceChange -= OnDeviceChange;
            ui.actionTriggered -= OnUiAction;
            game.actionTriggered -= OnGameAction;
            if (inputModule != null && inputModule.actionsAsset == RuntimeActions)
            {
                inputModule.enabled = false;
                inputModule.point = inputModule.leftClick = inputModule.rightClick = inputModule.middleClick = null;
                inputModule.scrollWheel = inputModule.move = inputModule.submit = inputModule.cancel = null;
                inputModule.actionsAsset = null;
            }
            RuntimeActions.Disable();
            foreach (var reference in references) DisposeObject(reference);
            references.Clear();
            DisposeObject(RuntimeActions);
            RuntimeActions = null;
            ui = game = null;
            streams.Clear();
            capture.CancelAll();
            pendingPause = false;
        }
        private TMP_InputField SelectedText
        {
            get
            {
                var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
                return selected != null && selected.activeInHierarchy ? selected.GetComponentInParent<TMP_InputField>() : null;
            }
        }
        private bool TextOwnsKeyboard => SelectedText != null || compositionActive || compositionFrame == Time.frameCount;
        private bool BackgroundAllowed => bound && focused && !waitForNeutral && blockedFrame != Time.frameCount && modal == null && !TextOwnsKeyboard;
        private void BindKeyboard()
        {
            if (keyboard == Keyboard.current) return;
            if (keyboard != null) keyboard.onIMECompositionChange -= OnComposition;
            keyboard = Keyboard.current;
            if (keyboard != null) keyboard.onIMECompositionChange += OnComposition;
        }
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            Quarantine();
            compositionActive = false;
            BindKeyboard();
            RefreshContext();
        }
        private void Update()
        {
            if (!bound) return;
            if (keyboard != Keyboard.current) { Quarantine(); BindKeyboard(); }
            if (waitForNeutral && blockedFrame != Time.frameCount && ControlsAreNeutral()) waitForNeutral = false;
            RefreshContext();
        }
        private void LateUpdate()
        {
            if (!bound) return;
            RefreshContext();
            if (pendingPause) TryRouteShortcut(RoutedInputKind.Pause, Vector2.zero);
            pendingPause = false;
            if (game.enabled)
            {
                var move = game.FindAction("Move", true).ReadValue<Vector2>();
                if (move != Vector2.zero) TryRouteShortcut(RoutedInputKind.Move, move);
            }
        }
        private bool ControlsAreNeutral()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is Keyboard keys)
                    foreach (var key in keys.allKeys) if (key.isPressed) return false;
                if (device is Mouse mouse && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)) return false;
            }
            return true;
        }
        private void OnComposition(IMECompositionString value) { SetCompositionActive(value.Count > 0); }
        // Injection seam stores only state, never text; not evidence of OS IME acceptance.
        public void SetCompositionActive(bool active)
        {
            if (compositionActive || active) { compositionFrame = Time.frameCount; Quarantine(); }
            compositionActive = active;
            RefreshContext();
        }
        public void RefreshContext()
        {
            if (!bound) return;
            if (modal != null && modal.activeInHierarchy)
            {
                var selected = eventSystem.currentSelectedGameObject;
                if (selected == null || !selected.transform.IsChildOf(modal.transform)) eventSystem.SetSelectedGameObject(modal);
            }
            if (focused) ui.Enable(); else ui.Disable();
            if (focused && (modal != null || TextOwnsKeyboard) && game.enabled) Quarantine();
            if (BackgroundAllowed) game.Enable(); else game.Disable();
            inputModule.enabled = focused;
            var submit = ui.FindAction("Submit", true);
            inputModule.submit = focused && !TextOwnsKeyboard ? submitReference : null;
            if (focused && !TextOwnsKeyboard) submit.Enable(); else submit.Disable();
        }
        public void OpenModal(GameObject root, GameObject initialFocus = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (modal != null) throw new InvalidOperationException("Only one modal may be open.");
            if (initialFocus != null && !initialFocus.transform.IsChildOf(root.transform)) throw new ArgumentException("Focus must be inside modal.");
            previousSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            modal = root;
            root.SetActive(true);
            Quarantine();
            if (eventSystem != null) eventSystem.SetSelectedGameObject(initialFocus != null ? initialFocus : root);
            RefreshContext();
        }
        public void CloseModal()
        {
            if (modal == null) return;
            var closed = modal;
            modal = null;
            Quarantine();
            closed.SetActive(false);
            if (eventSystem != null)
            {
                var selection = previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null;
                var selectable = selection != null ? selection.GetComponent<Selectable>() : null;
                if (selectable != null && (!selectable.IsActive() || !selectable.IsInteractable())) selection = null;
                eventSystem.SetSelectedGameObject(selection);
                if (SelectedText != null) SelectedText.ActivateInputField();
            }
            previousSelection = null;
            RefreshContext();
            ModalClosed?.Invoke(closed);
        }
        public InputContext RouteCancel()
        {
            if (!bound || !focused) return InputContext.None;
            if (cancelFrame == Time.frameCount)
                return compositionFrame == Time.frameCount ? InputContext.Text : InputContext.None;
            if (TextOwnsKeyboard) { cancelFrame = Time.frameCount; Quarantine(); return InputContext.Text; }
            cancelFrame = Time.frameCount;
            if (modal != null) { CloseModal(); return InputContext.Modal; }
            if (!BackgroundAllowed) return InputContext.None;
            Emit(InputContext.World, -1, RoutedInputKind.Cancel, Vector2.zero);
            Quarantine();
            return InputContext.World;
        }
        public bool TryRouteShortcut(RoutedInputKind kind, Vector2 value)
        {
            if (kind != RoutedInputKind.Move && kind != RoutedInputKind.Pause) throw new ArgumentException("Not a shortcut.");
            if (!BackgroundAllowed || cancelFrame == Time.frameCount) return false;
            Emit(InputContext.World, -1, kind, value);
            return true;
        }
        public InputContext HitContext(Vector2 position)
        {
            if (modal != null) return InputContext.Modal;
            if (eventSystem != null)
            {
                hits.Clear();
                eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, hits);
                foreach (var hit in hits)
                    if (hit.module is GraphicRaycaster)
                        return hit.gameObject.GetComponentInParent<TMP_InputField>() != null ? InputContext.Text : InputContext.Hud;
            }
            return WorldHitTest != null && WorldHitTest(position) ? InputContext.World : InputContext.Camera;
        }
        public InputContext RoutePointerDown(int id, Vector2 position)
        {
            if (!bound || !focused || waitForNeutral || blockedFrame == Time.frameCount) return InputContext.None;
            var owner = new InputOwner(HitContext(position), id, eventSystem.currentSelectedGameObject, modal);
            if (!capture.TryBegin(owner)) return InputContext.None;
            Emit(owner, RoutedInputKind.PointerDown, position);
            return owner.Context;
        }
        public InputContext RoutePointerMove(int id, Vector2 position)
        {
            if (!capture.TryGet(id, out var owner)) return InputContext.None;
            Emit(owner, RoutedInputKind.PointerMove, position);
            return owner.Context;
        }
        public InputContext RoutePointerUp(int id, Vector2 position)
        {
            if (!capture.TryEnd(id, out var owner)) return InputContext.None;
            Emit(owner, RoutedInputKind.PointerUp, position);
            return owner.Context;
        }
        public InputContext RouteScroll(int id, Vector2 position, Vector2 delta)
        {
            if (!bound || !focused || waitForNeutral || blockedFrame == Time.frameCount) return InputContext.None;
            var owner = capture.HasUiCapture ? InputContext.Hud : HitContext(position);
            Emit(owner, id, RoutedInputKind.Scroll, delta);
            return owner;
        }
        private void Emit(InputContext owner, int id, RoutedInputKind kind, Vector2 value)
        {
            Emit(new InputOwner(owner, id, eventSystem.currentSelectedGameObject, modal), kind, value);
        }
        private void Emit(InputOwner owner, RoutedInputKind kind, Vector2 value)
        {
            if (BackgroundAllowed && (owner.Context == InputContext.World || owner.Context == InputContext.Camera))
                BackgroundInput?.Invoke(new RoutedInput(owner, kind, value));
        }
        public void CancelAllCaptures() { Quarantine(); }
        private void Quarantine()
        {
            capture.CancelAll();
            pendingPause = false;
            waitForNeutral = true;
            blockedFrame = Time.frameCount;
            InputsCancelled?.Invoke();
        }
        public void SetApplicationFocus(bool value)
        {
            focused = value;
            Quarantine();
            if (!value) compositionActive = false;
            RefreshContext();
        }
        private void OnApplicationFocus(bool value) { SetApplicationFocus(value); }
        private void OnApplicationPause(bool value) { SetApplicationFocus(!value); }
        private int Stream(InputControl control)
        {
            if (!streams.TryGetValue(control, out var id)) { id = ++nextStream; streams.Add(control, id); }
            return id;
        }
        private void OnUiAction(InputAction.CallbackContext context)
        {
            if (!context.performed || !focused) return;
            if (context.control is KeyControl key && key.keyCode == Key.Escape)
            {
                if (context.ReadValue<float>() > 0.5f) RouteCancel();
                return;
            }
            var pointer = context.control.device as Pointer;
            var position = pointer != null ? pointer.position.ReadValue() : ui.FindAction("Point", true).ReadValue<Vector2>();
            switch (context.action.name)
            {
                case "Click": case "RightClick": case "MiddleClick":
                    var id = Stream(context.control);
                    if (context.ReadValue<float>() > 0.5f) RoutePointerDown(id, position); else RoutePointerUp(id, position);
                    break;
                case "Point":
                    movingStreams.Clear();
                    foreach (var pair in streams) if (pair.Key.device == context.control.device) movingStreams.Add(pair.Value);
                    foreach (var stream in movingStreams) RoutePointerMove(stream, position);
                    break;
                case "ScrollWheel": RouteScroll(-1, position, context.ReadValue<Vector2>()); break;
                case "Cancel": RouteCancel(); break;
            }
        }
        private void OnGameAction(InputAction.CallbackContext context)
        {
            if (!context.performed) return;
            if (context.control is KeyControl key && key.keyCode == Key.Escape) { RouteCancel(); return; }
            if (context.action.name == "Pause" && BackgroundAllowed) pendingPause = true;
        }
    }
}
