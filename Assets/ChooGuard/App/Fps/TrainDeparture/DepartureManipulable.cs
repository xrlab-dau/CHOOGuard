// Contains a bounded adaptation of uGUI Slider.Set, com.unity.ugui 2.0.0,
// fingerprint 27635d171b1ab7c244ef67aa22f59c4fbb4e52f9.
// uGUI Copyright © 2015-2020 Unity Technologies ApS (Unity).
// Licensed under the Unity Companion License for Unity-dependent projects:
// https://unity3d.com/legal/licenses/unity_companion_license
// Provided AS IS without warranty. Preserve the package LICENSE.md in distribution.
using System;
using UnityEngine;
using UnityEngine.Events;

namespace ChooGuard.App.Fps.TrainDeparture
{
    public enum DeparturePartKind { KeySocket, CloseButton, CrewHandle, KeypadKey, CrewDoor }
    public enum DepartureDoorMotion { Slide, Hinge }

    /// <summary>A binding to source parts. This component creates no geometry or colliders.</summary>
    public sealed class DepartureManipulable : MonoBehaviour
    {
        [Serializable] public sealed class FloatEvent : UnityEvent<float> { }
        [Serializable] public sealed class DigitEvent : UnityEvent<int> { }

        public DeparturePartKind Kind;
        public bool InteractionEnabled = true;
        [Tooltip("Only these existing solid colliders belong to this control. Never bind the enclosure.")]
        public Collider[] ContactColliders = Array.Empty<Collider>();
        public Transform SurfaceFrame;
        [Tooltip("Outward-facing normal in SurfaceFrame local coordinates; explicitly bind from the source.")]
        public Vector3 LocalOutwardNormal;
        [Range(0, 1)] public float MinimumFacingDot = .05f;
        public float Reach = 1.8f;

        [Header("Source translation (axis is in the part parent's coordinates)")]
        public Transform LinearPart;
        public Vector3 LinearAxis;
        [Tooltip("Signed travel in local metres from the authored rest pose to full travel.")]
        public float LinearTravel;
        [Header("Source rotation (axis is in the part's authored local coordinates)")]
        public Transform TurnPart;
        public Vector3 TurnAxis;
        public float TurnDegrees;
        public DepartureDoorMotion DoorMotion;

        [Header("Input and stroke")]
        public Vector2 DragAxis = Vector2.up;
        [Tooltip("Normalized physical travel per mouse delta unit; mouse deltas are not multiplied by dt.")]
        public float DragSensitivity = .006f;
        public float WheelSensitivity = .12f;
        [Range(.1f, 1)] public float PressThreshold = .9f;
        [Range(0, 9)] public int Digit;
        public bool HandleSpringReturn = true;
        [Header("Crew door authorization (bind the demo keypad/latch callbacks)")]
        public bool InitiallyUnlocked;
        public bool InitiallyHandleReleased;
        public bool RequireHandle = true;

        public FloatEvent TravelChanged = new FloatEvent();
        public UnityEvent StrokeCompleted = new UnityEvent();
        public DigitEvent DigitPressed = new DigitEvent();
        public event Action<DepartureManipulable, float> Changed;
        public event Action<DepartureManipulable> Stroke;
        internal event Action<DepartureManipulable> Disabled;
        public float Amount { get; private set; }
        public bool Unlocked { get { EnsureAuthorization(); return unlocked; } }
        public bool HandleReleased { get { EnsureAuthorization(); return handleReleased; } }
        public string ConfigurationError { get { return ValidateBinding(out var reason) ? "" : reason; } }

        bool captured, authorizationInitialized, unlocked, handleReleased, strokeLatched;
        Transform capturedLinear, capturedTurn;
        Vector3 restPosition;
        Quaternion restRotation;

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        internal bool NeedsLinear => Kind == DeparturePartKind.KeySocket || Kind == DeparturePartKind.CloseButton ||
            Kind == DeparturePartKind.KeypadKey || Kind == DeparturePartKind.CrewDoor && DoorMotion == DepartureDoorMotion.Slide;
        internal bool NeedsTurn => Kind == DeparturePartKind.KeySocket || Kind == DeparturePartKind.CrewHandle ||
            Kind == DeparturePartKind.CrewDoor && DoorMotion == DepartureDoorMotion.Hinge;

        public bool ValidateBinding(out string reason)
        {
            reason = "";
            if (!Enum.IsDefined(typeof(DeparturePartKind), Kind) || !Enum.IsDefined(typeof(DepartureDoorMotion), DoorMotion)) reason = "Unknown part/motion kind.";
            else if (SurfaceFrame == null || !Finite(LocalOutwardNormal) || LocalOutwardNormal.sqrMagnitude < .000001f) reason = "Bind a source surface and nonzero outward normal.";
            else if (!Finite(Reach) || Reach <= 0 || !Finite(MinimumFacingDot) || MinimumFacingDot < 0 || MinimumFacingDot > 1) reason = "Invalid reach/front-face limit.";
            else if (ContactColliders == null || ContactColliders.Length == 0) reason = "Bind exact existing solid contact colliders.";
            else if (!Finite(DragAxis.x) || !Finite(DragAxis.y) || DragAxis.sqrMagnitude < .000001f || !Finite(DragSensitivity) || DragSensitivity <= 0) reason = "Invalid mouse axis/sensitivity.";
            else if (!Finite(WheelSensitivity) || WheelSensitivity <= 0 || !Finite(PressThreshold) || PressThreshold <= 0 || PressThreshold > 1) reason = "Invalid wheel/stroke limits.";
            else if (Kind == DeparturePartKind.KeypadKey && (Digit < 0 || Digit > 9)) reason = "Digit must be a demo keypad digit 0..9.";
            else if (NeedsLinear && (LinearPart == null || !Finite(LinearAxis) || LinearAxis.sqrMagnitude < .000001f || !Finite(LinearTravel) || Mathf.Abs(LinearTravel) < .000001f)) reason = "Bind independent source translation part, axis and nonzero travel.";
            else if (NeedsTurn && (TurnPart == null || !Finite(TurnAxis) || TurnAxis.sqrMagnitude < .000001f || !Finite(TurnDegrees) || Mathf.Abs(TurnDegrees) < .001f || Mathf.Abs(TurnDegrees) > 360)) reason = "Bind independent source pivot, local axis and turn limit (0..360 degrees).";
            else if (Kind != DeparturePartKind.CrewDoor &&
                (NeedsLinear && (SurfaceFrame == LinearPart || SurfaceFrame.IsChildOf(LinearPart)) ||
                 NeedsTurn && (SurfaceFrame == TurnPart || SurfaceFrame.IsChildOf(TurnPart)))) reason = "The moving control must not move its panel/surface frame.";
            if (reason.Length != 0) return false;
            foreach (var contact in ContactColliders)
            {
                if (contact == null || contact.isTrigger) { reason = "Contact colliders must be existing solid colliders."; return false; }
                // A ray hit is resolved through its nearest parent adapter, then checked against this exact list.
                if (contact.GetComponentInParent<DepartureManipulable>() != this)
                { reason = "Each collider must resolve to this adapter as its nearest parent adapter."; return false; }
            }
            return true;
        }

        internal bool Ready => isActiveAndEnabled && InteractionEnabled && ValidateBinding(out _);
        internal bool Owns(Collider contact)
        {
            if (contact == null || !contact.enabled || !contact.gameObject.activeInHierarchy || contact.isTrigger || ContactColliders == null) return false;
            foreach (var candidate in ContactColliders) if (contact == candidate) return true;
            return false;
        }
        internal bool FrontFacing(Vector3 eye, Vector3 contact)
        {
            var direction = eye - contact;
            return direction.sqrMagnitude > .000001f &&
                Vector3.Dot(SurfaceFrame.TransformDirection(LocalOutwardNormal.normalized).normalized, direction.normalized) >= MinimumFacingDot;
        }
        void OnDisable() { Disabled?.Invoke(this); EndGrip(); }
        void EnsureCaptured()
        {
            if (captured) return;
            captured = true;
            capturedLinear = LinearPart; capturedTurn = TurnPart;
            if (LinearPart != null) restPosition = LinearPart.localPosition;
            if (TurnPart != null) restRotation = TurnPart.localRotation;
            EnsureAuthorization();
        }
        void EnsureAuthorization()
        {
            if (authorizationInitialized) return;
            authorizationInitialized = true; unlocked = InitiallyUnlocked; handleReleased = InitiallyHandleReleased;
        }
        internal bool CaptureBinding()
        {
            if (!Ready) return false;
            EnsureCaptured();
            // Runtime reparent/rebinding requires a fresh scene binding, not recapturing a partially moved pose.
            return (!NeedsLinear || capturedLinear == LinearPart) && (!NeedsTurn || capturedTurn == TurnPart);
        }
        public void SetUnlocked(bool value) { EnsureAuthorization(); unlocked = value; }
        public void SetHandleReleased(bool value) { EnsureAuthorization(); handleReleased = value; }

        internal void ApplyKeyPose(BerneDoorMechanism mechanism)
        {
            if (!CaptureBinding()) return;
            LinearPart.localPosition = restPosition + LinearAxis.normalized * (LinearTravel * mechanism.Depth);
            TurnPart.localRotation = restRotation * Quaternion.AngleAxis(TurnDegrees * mechanism.Rotation, TurnAxis.normalized);
        }
        internal void SetAmount(float value, bool allowStroke)
        {
            if (!CaptureBinding()) return;
            // Adapted uGUI 2.0.0 Slider.Set (539-554), UCL: clamp, reject unchanged,
            // commit, update visuals, then notify. Physical stroke threshold is domain policy.
            if (!Finite(value)) return;
            float newValue = Mathf.Clamp01(value);
            if (Amount == newValue) return;
            float previousAmount = Amount;
            Amount = newValue;
            if (NeedsLinear) LinearPart.localPosition = restPosition + LinearAxis.normalized * (LinearTravel * Amount);
            if (NeedsTurn) TurnPart.localRotation = restRotation * Quaternion.AngleAxis(TurnDegrees * Amount, TurnAxis.normalized);
            TravelChanged?.Invoke(Amount); Changed?.Invoke(this, Amount);
            if (allowStroke && !strokeLatched && previousAmount < PressThreshold && Amount >= PressThreshold)
            {
                strokeLatched = true;
                if (Kind == DeparturePartKind.KeypadKey) DigitPressed?.Invoke(Digit);
                StrokeCompleted?.Invoke(); Stroke?.Invoke(this);
            }
        }
        internal float Drag(Vector2 delta) => Vector2.Dot(delta, DragAxis.normalized) * DragSensitivity;
        internal void EndGrip()
        {
            bool returns = Kind == DeparturePartKind.CloseButton || Kind == DeparturePartKind.KeypadKey ||
                Kind == DeparturePartKind.CrewHandle && HandleSpringReturn;
            // Release still returns a disabled part; validation is not used to retain a depressed switch.
            if (returns && captured)
            {
                bool changed = Amount != 0; Amount = 0;
                if (capturedLinear != null && NeedsLinear) capturedLinear.localPosition = restPosition;
                if (capturedTurn != null && NeedsTurn) capturedTurn.localRotation = restRotation;
                if (changed) { TravelChanged?.Invoke(0); Changed?.Invoke(this, 0); }
            }
            strokeLatched = false;
        }
        internal void ResetState()
        {
            EndGrip();
            if (captured)
            {
                if (capturedLinear != null && NeedsLinear) capturedLinear.localPosition = restPosition;
                if (capturedTurn != null && NeedsTurn) capturedTurn.localRotation = restRotation;
            }
            Amount = 0; authorizationInitialized = true; unlocked = InitiallyUnlocked; handleReleased = InitiallyHandleReleased; strokeLatched = false;
        }
    }
}
