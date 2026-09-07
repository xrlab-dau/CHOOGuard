using System;
using System.Reflection;
using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    // No package installation: supports the legacy manager or a host's existing Input System.
    // The scene builder copies the accompanying link.xml template to Assets for standalone builds.
    internal static class DemoInput
    {
#if !ENABLE_LEGACY_INPUT_MANAGER
        private static ReflectedInput optionalInput = new ReflectedInput();
#endif
        public static string Error { get; private set; }
        public static void Retry()
        {
            Error = null;
#if !ENABLE_LEGACY_INPUT_MANAGER
            optionalInput = new ReflectedInput();
#endif
        }
        public static bool Available
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return string.IsNullOrEmpty(Error);
#else
                try { return optionalInput.Available && string.IsNullOrEmpty(Error); }
                catch (Exception exception)
                {
                    Error = "입력 장치 초기화 실패: " + exception.GetBaseException().Message;
                    return false;
                }
#endif
            }
        }

        public static Vector2 Movement
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Vector2.ClampMagnitude(new Vector2(
                    (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                    (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)), 1);
#else
                return Read(() => optionalInput.Movement, Vector2.zero);
#endif
            }
        }

        public static Vector2 Look
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Read(() => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2, Vector2.zero);
#else
                return Read(() => optionalInput.Look * .12f, Vector2.zero);
#endif
            }
        }

        public static bool InteractPressed
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.E);
#else
                return Read(() => optionalInput.Pressed("eKey"), false);
#endif
            }
        }

        public static bool EscapePressed
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Escape);
#else
                return Read(() => optionalInput.Pressed("escapeKey"), false);
#endif
            }
        }

        private static T Read<T>(Func<T> read, T fallback)
        {
            if (!Available) return fallback;
            try { return read(); }
            catch (Exception exception)
            {
                Error = "입력 장치를 읽을 수 없습니다: " + exception.GetBaseException().Message;
                Debug.LogWarning(Error);
                return fallback;
            }
        }

        private sealed class ReflectedInput
        {
            private readonly PropertyInfo keyboardCurrent;
            private readonly PropertyInfo mouseCurrent;
            private readonly System.Collections.Generic.Dictionary<string, PropertyInfo> keys =
                new System.Collections.Generic.Dictionary<string, PropertyInfo>();
            private readonly PropertyInfo isPressed;
            private readonly PropertyInfo wasPressed;
            private readonly PropertyInfo delta;
            private readonly MethodInfo readDelta;

            public ReflectedInput()
            {
                try
                {
                    var keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
                    var mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
                    if (keyboardType == null || mouseType == null) return;
                    keyboardCurrent = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                    mouseCurrent = mouseType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                    foreach (var key in new[] { "wKey", "sKey", "aKey", "dKey", "eKey", "escapeKey" })
                        keys[key] = keyboardType.GetProperty(key);
                    var keyType = keys["eKey"] == null ? null : keys["eKey"].PropertyType;
                    isPressed = keyType == null ? null : keyType.GetProperty("isPressed");
                    wasPressed = keyType == null ? null : keyType.GetProperty("wasPressedThisFrame");
                    delta = mouseType.GetProperty("delta");
                    readDelta = delta == null ? null : delta.PropertyType.GetMethod("ReadValue", Type.EmptyTypes);
                }
                catch (Exception exception)
                {
                    Error = "입력 API 초기화 실패: " + exception.GetBaseException().Message;
                }
            }

            public bool Available
            {
                get
                {
                    return keyboardCurrent != null && mouseCurrent != null && isPressed != null &&
                        wasPressed != null && readDelta != null &&
                        keyboardCurrent.GetValue(null) != null && mouseCurrent.GetValue(null) != null;
                }
            }

            public Vector2 Movement
            {
                get { return Vector2.ClampMagnitude(new Vector2(Held("dKey") - Held("aKey"), Held("wKey") - Held("sKey")), 1); }
            }
            public Vector2 Look
            {
                get { return (Vector2)readDelta.Invoke(delta.GetValue(mouseCurrent.GetValue(null)), null); }
            }
            public bool Pressed(string key) { return KeyValue(key, wasPressed); }
            private int Held(string key) { return KeyValue(key, isPressed) ? 1 : 0; }
            private bool KeyValue(string key, PropertyInfo value)
            {
                var keyboard = keyboardCurrent.GetValue(null);
                return keyboard != null && keys[key] != null && (bool)value.GetValue(keys[key].GetValue(keyboard));
            }
        }
    }
}
