using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CHOOGuard.UX.NativePreview
{
    /// <summary>
    /// Native layout/navigation preview ONLY. Does not run the operations model,
    /// approve commands, advance simulation time, export files, or create branches.
    /// </summary>
    public sealed class NativePreviewRouter : MonoBehaviour
    {
        [Serializable]
        public sealed class Surface
        {
            public string id;
            public GameObject root;
            public bool overlay;
        }

        public Surface[] surfaces = Array.Empty<Surface>();
        public TMP_Text statusLabel;
        public TMP_Text modeLabel;
        public UnityEvent<string> intentRequested = new UnityEvent<string>();
        private string currentBase = "S01";
        private string currentOverlay = "";
        private readonly Stack<string> overlayHistory = new Stack<string>();
        private GameObject restoreFocus;

        public bool BlocksWorldInput => currentBase != "S05" || currentOverlay != "";

        private void Start() { Show("S01"); }

        private void Update()
        {
            bool cancel = false;
#if ENABLE_INPUT_SYSTEM
            cancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            cancel = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (cancel)
            {
                // Let text editing consume its own cancellation. No gameplay hotkeys here.
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                var field = selected != null ? selected.GetComponentInParent<TMP_InputField>() : null;
                if (field != null && field.isFocused) { field.DeactivateInputField(); return; }
                if (currentOverlay != "") CloseOverlay();
                else if (currentBase == "S05") Show("S12");
            }
        }

        private Surface Find(string id)
        {
            foreach (var surface in surfaces)
                if (surface != null && surface.id == id) return surface;
            return null;
        }

        public void Show(string id)
        {
            var target = Find(id);
            if (target == null || target.root == null)
            {
                Debug.LogError("Native UX surface is missing: " + id, this);
                return;
            }
            if (target.overlay)
            {
                if (currentOverlay == id) return;
                if (currentOverlay == "")
                    restoreFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                else overlayHistory.Push(currentOverlay);
                currentOverlay = id;
            }
            else
            {
                currentBase = id;
                currentOverlay = "";
                overlayHistory.Clear();
                restoreFocus = null;
            }
            ApplyVisibility();
            SetFocus(target.root);
        }

        public void CloseOverlay()
        {
            currentOverlay = overlayHistory.Count > 0 ? overlayHistory.Pop() : "";
            ApplyVisibility();
            if (currentOverlay != "") SetFocus(Find(currentOverlay)?.root);
            else if (EventSystem.current != null && restoreFocus != null && restoreFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(restoreFocus);
            else SetFocus(Find(currentBase)?.root);
        }

        private void ApplyVisibility()
        {
            foreach (var surface in surfaces)
            {
                if (surface == null || surface.root == null) continue;
                bool active = surface.id == currentBase || surface.id == currentOverlay;
                surface.root.SetActive(active);
                var group = surface.root.GetComponent<CanvasGroup>();
                if (group == null) continue;
                bool interactive = active && (surface.overlay || currentOverlay == "");
                group.interactable = interactive;
                group.blocksRaycasts = interactive;
            }
        }

        private static void SetFocus(GameObject root)
        {
            if (root == null || EventSystem.current == null) return;
            foreach (var item in root.GetComponentsInChildren<Selectable>())
            {
                if (!item.IsActive() || !item.IsInteractable()) continue;
                EventSystem.current.SetSelectedGameObject(item.gameObject);
                return;
            }
        }

        public void OpenTutorial()
        {
            if (modeLabel != null) modeLabel.text = "교육 · TUTORIAL";
            Show("S05");
            Show("S04");
        }

        public void OpenOperations()
        {
            if (modeLabel != null) modeLabel.text = "일반 · RANDOM OPERATIONS";
            Show("S03");
        }

        public void Intent(string intent)
        {
            // This is a presentation-to-core handoff, NOT an acknowledgement from a core.
            if (statusLabel != null)
                statusLabel.text = "UI 의도: " + intent + " | 운영 코어 미연결 · 적용 결과 없음";
            Debug.Log("[CHOOGuard UX preview] Intent only: " + intent, this);
            intentRequested.Invoke(intent);
        }
    }
}
