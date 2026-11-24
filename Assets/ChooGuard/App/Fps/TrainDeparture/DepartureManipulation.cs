using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.TrainDeparture
{
    /// <summary>Single-hand physical input. TrainDepartureGame is the sole input source.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class DepartureManipulation : MonoBehaviour, IDepartureInput
    {
        public TrainDepartureGame Game;
        public FirstPersonResponder Player;
        public DepartureKeyInventory KeyInventory;
        [Tooltip("Bind every adapter for restart reset, including parts not yet touched.")]
        public DepartureManipulable[] Parts = Array.Empty<DepartureManipulable>();
        [Tooltip("Maximum initial centre-ray reach. Every solid layer blocks; triggers never block/grab.")]
        public float MaximumReach = 2.2f;
        [Range(0, 1)] public float MinimumGripAimDot = .8f;
        // Read-only physical aim, before grip authorization (e.g. pocketed owned key).
        public DepartureManipulable AimedPart { get; private set; }
        public DepartureManipulable Focused { get; private set; }
        public DepartureManipulable Holding { get; private set; }
        public float Progress { get; private set; } = -1;
        public bool FocusAvailable => Focused != null;
        public bool IsHolding => Holding != null;
        public float PhysicalProgress => Progress;

        readonly HashSet<DepartureManipulable> touched = new HashSet<DepartureManipulable>();
        TrainDepartureGame subscribedGame;
        FirstPersonResponder subscribedPlayer;
        readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        Collider grabCollider;
        Vector3 localGrabPoint;
        bool ownsLookSuppression;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        void Awake() { ResolveBindings(); }
        void OnEnable() { ResolveBindings(); }
        void ResolveBindings()
        {
            if (Game == null) Game = GetComponent<TrainDepartureGame>();
            if (Player == null && Game != null) Player = Game.Player;
            if (subscribedPlayer != Player)
            {
                if (subscribedPlayer != null) subscribedPlayer.PauseChanged -= OnPlayerPause;
                subscribedPlayer = Player;
                if (subscribedPlayer != null) subscribedPlayer.PauseChanged += OnPlayerPause;
            }
            if (subscribedGame == Game) return;
            if (subscribedGame != null) subscribedGame.Restarted -= ResetParts;
            subscribedGame = Game;
            if (subscribedGame != null) subscribedGame.Restarted += ResetParts;
        }
        void OnDisable()
        {
            Release();
            if (subscribedGame != null) subscribedGame.Restarted -= ResetParts;
            subscribedGame = null;
            if (subscribedPlayer != null) subscribedPlayer.PauseChanged -= OnPlayerPause;
            subscribedPlayer = null;
        }
        void OnPlayerPause(bool paused) { if (paused) Release(); }
        void OnPartDisabled(DepartureManipulable part) { if (Holding == part) Release(); }
        void OnApplicationFocus(bool focused) { if (!focused && (Player == null || !Player.ExternalInputMode)) Release(); }
        void OnApplicationPause(bool paused) { if (paused) Release(); }
        // No OS input is polled here. This also cancels on disable/pause if the root stops stepping.
        void LateUpdate()
        {
            if (!CanRun() || Holding != null && (!CanContinue(Holding) || !ContactStillValid())) Release();
        }
        bool CanRun()
        {
            return isActiveAndEnabled && Game != null && Game.isActiveAndEnabled && Game.Started && !Game.Completed &&
                Player != null && Player.isActiveAndEnabled && Player.PlayerCamera != null && Player.PlayerCamera.isActiveAndEnabled &&
                !Player.IsPaused && (Player.ExternalInputMode || Player.HasFocus) && Time.timeScale > 0 &&
                Finite(MaximumReach) && MaximumReach > 0 && Finite(MinimumGripAimDot) && MinimumGripAimDot >= 0 && MinimumGripAimDot <= 1;
        }
        bool CanBegin(DepartureManipulable part)
        {
            if (part == null || !part.CaptureBinding()) return false;
            if (part.Kind != DeparturePartKind.KeySocket && KeyInventory != null && KeyInventory.OccupiesHand) return false;
            switch (part.Kind)
            {
                case DeparturePartKind.KeySocket:
                    return KeyInventory != null && KeyInventory.CanGripSocket(part) && !Game.Mechanism.Retrieved &&
                        (Game.Flow.Stage == DepartureStage.CloseDoors || Game.Flow.Stage == DepartureStage.RetrieveKey);
                case DeparturePartKind.CloseButton:
                    return (Game.Flow.Stage == DepartureStage.CloseDoors || Game.Flow.Stage == DepartureStage.ObserveClosedLamp) && Game.Mechanism.Depth >= 1 && Game.Mechanism.Rotation >= 1 && !Game.Mechanism.Retrieved;
                case DeparturePartKind.CrewHandle: return part.Unlocked;
                case DeparturePartKind.CrewDoor: return part.Unlocked && (!part.RequireHandle || part.HandleReleased);
                case DeparturePartKind.KeypadKey: return true;
                default: return false;
            }
        }
        bool CanContinue(DepartureManipulable part)
        {
            if (part == null || !part.CaptureBinding()) return false;
            if (part.Kind == DeparturePartKind.KeySocket)
                return KeyInventory != null && KeyInventory.isActiveAndEnabled && KeyInventory.Socket == part &&
                    (KeyInventory.IsAligning || KeyInventory.SocketReady) && !Game.Mechanism.Retrieved &&
                    (Game.Flow.Stage == DepartureStage.CloseDoors || Game.Flow.Stage == DepartureStage.RetrieveKey);
            if (part.Kind == DeparturePartKind.CloseButton)
                return !Game.Mechanism.Retrieved && Game.Mechanism.Depth >= 1 && Game.Mechanism.Rotation >= 1 &&
                    (Game.Flow.Stage == DepartureStage.CloseDoors || Game.Flow.Stage == DepartureStage.ObserveClosedLamp || Game.Flow.Stage == DepartureStage.RetrieveKey);
            return CanBegin(part);
        }
        DepartureManipulable FindAimedPart(out RaycastHit hit)
        {
            var eye = Player.PlayerCamera.transform;
            // Do not filter out walls/enclosures or search all hits for an interactive object behind one.
            if (!CastNearestSolid(eye.position, eye.forward, MaximumReach, out hit)) return null;
            var part = hit.collider.GetComponentInParent<DepartureManipulable>();
            if (part == null || !part.Ready || !part.Owns(hit.collider) || hit.distance > part.Reach || !part.FrontFacing(eye.position, hit.point)) return null;
            return part;
        }
        DepartureManipulable FindFocus(out RaycastHit hit)
        {
            AimedPart = FindAimedPart(out hit);
            if (!CanBegin(AimedPart)) return null;
            touched.Add(AimedPart);
            return AimedPart;
        }
        bool ContactStillValid()
        {
            if (Holding == null || grabCollider == null || !Holding.Owns(grabCollider)) return false;
            var eye = Player.PlayerCamera.transform;
            var contact = grabCollider.transform.TransformPoint(localGrabPoint);
            var offset = contact - eye.position;
            float distance = offset.magnitude;
            if (distance <= .0001f || distance > Mathf.Min(MaximumReach, Holding.Reach) ||
                Vector3.Dot(eye.forward, offset / distance) < MinimumGripAimDot || !Holding.FrontFacing(eye.position, contact)) return false;
            // Follow the captured physical contact, not a different button now under the reticle.
            if (!CastNearestSolid(eye.position, offset / distance, distance + .01f, out var hit)) return false;
            return Holding.Owns(hit.collider);
        }
        bool CastNearestSolid(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            // This game carries the bag until the stow animation hides it. A socketed
            // key is reparented below Socket.LinearPart and must remain a real contact.
            var bag = Game != null && Game.OwnBag != null && Game.OwnBag.gameObject.activeInHierarchy ? Game.OwnBag : null;
            var key = KeyInventory != null && !KeyInventory.IsSocketed && KeyInventory.KeyModel != null &&
                KeyInventory.KeyModel.IsChildOf(Player.transform) ? KeyInventory.KeyModel : null;
            return NearestSolidRay.TryCast(origin, direction, distance, ~0, hitBuffer, Player.transform, bag, key, out hit, out _);
        }
        // Query-only production hook: unlike StepInput(false), never releases an automated momentary control.
        public void RefreshAimOnly()
        {
            ResolveBindings();
            if (!CanRun()) { AimedPart = Focused = null; Progress = -1; return; }
            Focused = FindFocus(out _); UpdateProgress();
        }
        public void StepInput(bool pressed, bool held, Vector2 delta, float wheel, float dt)
        {
            ResolveBindings();
            if (!held)
            {
                Release();
                if (CanRun()) { Focused = FindFocus(out _); UpdateProgress(); }
                return;
            }
            if (!CanRun() || !Finite(dt) || dt <= 0 || dt > .25f || !Finite(delta.x) || !Finite(delta.y) || !Finite(wheel)) { Release(); return; }
            if (Holding != null && (!CanContinue(Holding) || !ContactStillValid())) { Release(); return; }
            if (Holding == null)
            {
                Focused = FindFocus(out var hit);
                if (!pressed || Focused == null) { UpdateProgress(); return; }
                Holding = Focused; Holding.Disabled += OnPartDisabled; grabCollider = hit.collider;
                localGrabPoint = grabCollider.transform.InverseTransformPoint(hit.point);
                ownsLookSuppression = !Player.SuppressLookInput;
                Player.SuppressLookInput = true;
                if (Holding.Kind == DeparturePartKind.KeySocket)
                {
                    if (!KeyInventory.BeginSocketGrip(Holding)) { Release(); return; }
                    // The fresh socket contact only acquires the hand. Alignment and
                    // physical travel require later live samples, never this press.
                    Focused = Holding; UpdateProgress(); return;
                }
            }
            AimedPart = Focused = Holding;
            Player.SuppressLookInput = true;
            ApplyInput(delta, wheel);
            if (Holding != null) UpdateProgress(); // A bound callback is allowed to cancel/disable this component.
        }
        void ApplyInput(Vector2 delta, float wheel)
        {
            var part = Holding;
            float movement = part.Drag(delta);
            switch (part.Kind)
            {
                case DeparturePartKind.KeySocket:
                    if (KeyInventory == null || !KeyInventory.SocketReady) break;
                    var mechanism = Game.Mechanism;
                    var withdrawnRotation = KeyInventory.KeyModel.rotation;
                    if (Game.Flow.Stage == DepartureStage.CloseDoors)
                    {
                        // Reaching the insertion stop does not turn the key in this same input sample.
                        if (mechanism.Depth < 1) mechanism.Insert(Mathf.Max(0, movement));
                        else if (wheel != 0) mechanism.Rotate(wheel * part.WheelSensitivity);
                    }
                    else if (Game.Flow.Stage == DepartureStage.RetrieveKey)
                    {
                        // Lamp observation authorizes a later pull at the current turn angle; reverse turning is optional.
                        if (wheel != 0) mechanism.Rotate(wheel * part.WheelSensitivity);
                        else mechanism.Withdraw(Mathf.Max(0, -movement));
                    }
                    part.ApplyKeyPose(mechanism);
                    if (mechanism.Retrieved)
                    {
                        KeyInventory.CompleteRetrieval(withdrawnRotation);
                        Release();
                    }
                    break;
                case DeparturePartKind.CloseButton:
                    part.SetAmount(part.Amount + movement, false);
                    Game.Mechanism.SetButtonHeld(Holding == part && part.Ready && part.Amount >= part.PressThreshold);
                    break;
                case DeparturePartKind.KeypadKey:
                case DeparturePartKind.CrewHandle:
                    part.SetAmount(part.Amount + movement, true);
                    break;
                case DeparturePartKind.CrewDoor:
                    part.SetAmount(part.Amount + movement, false);
                    break;
            }
        }
        void UpdateProgress()
        {
            var part = Holding != null ? Holding : Focused;
            if (part == null) { Progress = -1; return; }
            if (part.Kind != DeparturePartKind.KeySocket) { Progress = part.Amount; return; }
            var mechanism = Game.Mechanism;
            Progress = Game.Flow.Stage == DepartureStage.RetrieveKey
                ? 1 - mechanism.Depth
                : (mechanism.Depth + mechanism.Rotation) * .5f;
        }
        public void Release()
        {
            var previous = Holding;
            Holding = Focused = AimedPart = null; grabCollider = null; Progress = -1;
            if (Game != null) Game.Mechanism.SetButtonHeld(false);
            if (ownsLookSuppression && Player != null) Player.SuppressLookInput = false;
            ownsLookSuppression = false;
            if (previous != null)
            {
                previous.Disabled -= OnPartDisabled; previous.EndGrip();
                if (previous.Kind == DeparturePartKind.KeySocket && KeyInventory != null) KeyInventory.EndSocketGrip();
            }
        }
        void ResetParts()
        {
            Release();
            if (Parts != null) foreach (var part in Parts) if (part != null) touched.Add(part);
            foreach (var part in touched) if (part != null) part.ResetState();
        }
    }
}
