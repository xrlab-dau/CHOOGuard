using System;
using UnityEngine;

namespace ChooGuard.App.Fps.TrainDeparture
{
    /// <summary>One player-owned key. Root supplies input and live simulation time.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class DepartureKeyInventory : MonoBehaviour
    {
        public TrainDepartureGame Game;
        public DepartureManipulation Manipulation;
        public DepartureManipulable Socket;
        public Transform KeyModel, CarryAnchor, PocketAnchor;
        public Collider KeyCollider;
        public Vector3 SeatedLocalPosition = new Vector3(0, 0, .0025f);
        public Quaternion SeatedLocalRotation = Quaternion.Euler(0, 180, 0);
        [Min(.1f)] public float MotionSeconds = .3f;
        public bool IsDrawn => initialized && phase != Phase.Pocketed && phase != Phase.Stowing;
        public bool IsSocketed => initialized && (phase == Phase.Aligning || phase == Phase.Socketed);
        public bool IsAligning => initialized && phase == Phase.Aligning;
        public bool IsReturning => initialized && phase == Phase.Returning;
        public bool IsInHand => initialized && phase != Phase.Pocketed && phase != Phase.Socketed;
        public bool IsReadyInHand => initialized && phase == Phase.InHand;
        public bool OccupiesHand => IsInHand;
        public string ConfigurationError => ValidateBinding(out var reason) ? "" : reason;

        enum Phase { Pocketed, Drawing, InHand, Aligning, Socketed, Returning, Stowing }
        Phase phase;
        bool initialized, frozen, poseRecorded;
        float elapsed;
        Vector3 startPosition, frozenPosition;
        Quaternion startRotation, frozenRotation;
        Collider[] hubContacts;
        Renderer[] renderers;
        DepartureGuardedOperation<bool> draw, stow, align, registerContacts, endGrip, finishMotion;
        DepartureGuardedOperation<Quaternion> retrieve;
        TrainDepartureGame subscribedGame;
        FirstPersonResponder subscribedPlayer;
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

        public bool ValidateBinding(out string reason)
        {
            reason = "";
            if (Game == null || Manipulation == null || Manipulation.Game != Game || Manipulation.KeyInventory != this ||
                Socket == null || Socket.Kind != DeparturePartKind.KeySocket)
                reason = "Bind the same game, manipulation and key socket to the inventory.";
            else if (KeyModel == null || KeyCollider == null || CarryAnchor == null || PocketAnchor == null ||
                KeyModel == CarryAnchor || KeyModel == PocketAnchor || CarryAnchor.IsChildOf(KeyModel) || PocketAnchor.IsChildOf(KeyModel) ||
                !(KeyCollider.transform == KeyModel || KeyCollider.transform.IsChildOf(KeyModel)) || KeyCollider.isTrigger)
                reason = "Bind one key model with its solid collider and independent carry/pocket anchors.";
            else if (!Finite(MotionSeconds) || MotionSeconds < .1f || !Finite(SeatedLocalPosition) ||
                !Finite(SeatedLocalRotation.x) || !Finite(SeatedLocalRotation.y) || !Finite(SeatedLocalRotation.z) ||
                !Finite(SeatedLocalRotation.w) || Quaternion.Dot(SeatedLocalRotation, SeatedLocalRotation) < .000001f)
                reason = "Invalid key motion duration or seated pose.";
            else if (!Socket.ValidateBinding(out reason)) return false;
            else if (Socket.LinearPart == KeyModel || Socket.TurnPart == KeyModel || Socket.LinearPart.IsChildOf(KeyModel) || Socket.TurnPart.IsChildOf(KeyModel) ||
                !(Socket.LinearPart == Socket.transform || Socket.LinearPart.IsChildOf(Socket.transform)))
                reason = "Keep fixed socket pivots under the socket adapter, independent of the carried key.";
            return reason.Length == 0;
        }

        void Awake() { ResolveBindings(); }
        void OnEnable() { ResolveBindings(); }
        void ResolveBindings()
        {
            if (subscribedGame != Game)
            {
                if (subscribedGame != null) subscribedGame.Restarted -= ResetItem;
                subscribedGame = Game;
                if (subscribedGame != null) subscribedGame.Restarted += ResetItem;
            }
            var player = Game != null ? Game.Player : null;
            if (subscribedPlayer == player) return;
            if (subscribedPlayer != null) subscribedPlayer.PauseChanged -= OnPause;
            subscribedPlayer = player;
            if (subscribedPlayer != null) subscribedPlayer.PauseChanged += OnPause;
        }
        void OnDisable()
        {
            if (Manipulation != null && Manipulation.Holding == Socket) Manipulation.Release();
            if (subscribedGame != null) subscribedGame.Restarted -= ResetItem;
            if (subscribedPlayer != null) subscribedPlayer.PauseChanged -= OnPause;
            subscribedGame = null; subscribedPlayer = null;
        }
        void OnPause(bool paused) { if (paused && initialized) RememberPose(); }
        bool EnsureInitialized()
        {
            ResolveBindings();
            if (initialized) return ValidateBinding(out _);
            if (!ValidateBinding(out _)) return false;
            // Initial socket contacts must be its fixed hub, never this carried model.
            foreach (var contact in Socket.ContactColliders) if (contact == KeyCollider) return false;
            hubContacts = (Collider[])Socket.ContactColliders.Clone();
            renderers = KeyModel.GetComponentsInChildren<Renderer>(true);
            initialized = true;
            BuildOperations();
            StowImmediately();
            return true;
        }
        void BuildOperations()
        {
            draw = new DepartureGuardedOperation<bool>(
                _ => CanStep() && !Manipulation.IsHolding && phase == Phase.Pocketed,
                _ => { SetVisible(true); BeginMotion(Phase.Drawing, CarryAnchor); });
            stow = new DepartureGuardedOperation<bool>(
                _ => CanStep() && !Manipulation.IsHolding && phase == Phase.InHand,
                _ => BeginMotion(Phase.Stowing, PocketAnchor));
            align = new DepartureGuardedOperation<bool>(
                _ => CanStep() && phase == Phase.InHand,
                _ => BeginMotion(Phase.Aligning, Socket.LinearPart));
            registerContacts = new DepartureGuardedOperation<bool>(
                _ => phase == Phase.Aligning && KeyModel.parent == Socket.LinearPart &&
                    KeyCollider.GetComponentInParent<DepartureManipulable>() == Socket,
                _ =>
                {
                    var contacts = new Collider[hubContacts.Length + 1];
                    Array.Copy(hubContacts, contacts, hubContacts.Length); contacts[hubContacts.Length] = KeyCollider;
                    Socket.ContactColliders = contacts;
                });
            endGrip = new DepartureGuardedOperation<bool>(
                _ => initialized && IsSocketed && Game.Mechanism.Depth <= 0,
                _ => ReturnToHand());
            retrieve = new DepartureGuardedOperation<Quaternion>(
                _ => initialized && phase == Phase.Socketed && KeyModel.parent == Socket.LinearPart &&
                    KeyCollider.GetComponentInParent<DepartureManipulable>() == Socket && Game.Mechanism.Retrieved,
                withdrawnRotation =>
                {
                    // Keep the actual withdrawal angle while the same item returns to its owner.
                    KeyModel.rotation = withdrawnRotation;
                    ReturnToHand();
                });
            finishMotion = new DepartureGuardedOperation<bool>(
                _ => elapsed >= MotionSeconds && (phase == Phase.Stowing || phase == Phase.Drawing ||
                    phase == Phase.Returning || phase == Phase.Aligning && KeyModel.parent == Socket.LinearPart &&
                    KeyCollider.GetComponentInParent<DepartureManipulable>() == Socket),
                _ =>
                {
                    if (phase == Phase.Stowing) { phase = Phase.Pocketed; SetVisible(false); }
                    else phase = phase == Phase.Aligning ? Phase.Socketed : Phase.InHand;
                });
        }
        bool CanStep()
        {
            var player = Game != null ? Game.Player : null;
            return isActiveAndEnabled && Game != null && Game.isActiveAndEnabled && Game.Started && !Game.Completed &&
                player != null && player.isActiveAndEnabled && !player.IsPaused &&
                (player.ExternalInputMode || player.HasFocus) && Time.timeScale > 0;
        }
        void SetVisible(bool visible)
        {
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = visible;
            KeyCollider.enabled = visible;
        }
        void RestoreHubContacts() { Socket.ContactColliders = (Collider[])hubContacts.Clone(); }
        void BeginMotion(Phase next, Transform parent)
        {
            KeyModel.SetParent(parent, true);
            startPosition = KeyModel.localPosition; startRotation = KeyModel.localRotation;
            elapsed = 0; phase = next;
            RememberPose();
        }
        void RememberPose()
        {
            frozenPosition = KeyModel.position; frozenRotation = KeyModel.rotation; poseRecorded = true;
        }
        void StowImmediately()
        {
            RestoreHubContacts();
            KeyModel.SetParent(PocketAnchor, false);
            KeyModel.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            phase = Phase.Pocketed; elapsed = 0; frozen = false;
            SetVisible(false); RememberPose();
        }
        public void ResetItem()
        {
            if (!EnsureInitialized()) return;
            if (Manipulation.Holding == Socket) Manipulation.Release();
            StowImmediately();
        }
        public bool TryDraw() => EnsureInitialized() && draw.TryApply(true);
        public bool TryStow() => EnsureInitialized() && stow.TryApply(true);
        // Digit cancellation owns only equip motion; never reset an inserted/partial mechanism.
        public void CancelEquipMotion()
        {
            if (EnsureInitialized() && (phase == Phase.Drawing || phase == Phase.Stowing)) StowImmediately();
        }
        public void ToggleDraw()
        {
            if (!EnsureInitialized()) return;
            if (!draw.TryApply(true)) stow.TryApply(true);
        }
        internal bool CanGripSocket(DepartureManipulable part)
        {
            return EnsureInitialized() && isActiveAndEnabled && part == Socket &&
                (phase == Phase.InHand || phase == Phase.Socketed);
        }
        internal bool BeginSocketGrip(DepartureManipulable part)
        {
            if (!CanGripSocket(part)) return false;
            if (phase == Phase.Socketed) return true;
            if (!align.TryApply(true)) return false;
            // Contact registration is itself guarded after reparenting the one model.
            if (registerContacts.TryApply(true)) return true;
            ReturnToHand(); // Ownership failure restores hub-only contacts.
            return false;
        }
        internal bool SocketReady => initialized && phase == Phase.Socketed && KeyModel.parent == Socket.LinearPart;
        internal void EndSocketGrip()
        {
            if (initialized) endGrip.TryApply(true);
        }
        void ReturnToHand()
        {
            RestoreHubContacts();
            BeginMotion(Phase.Returning, CarryAnchor);
        }
        internal void CompleteRetrieval(Quaternion withdrawnRotation)
        {
            if (initialized) retrieve.TryApply(withdrawnRotation);
        }
        public void Step(float dt)
        {
            if (!EnsureInitialized() || !CanStep() || !Finite(dt) || dt <= 0 || dt > .25f) return;
            if (frozen)
            {
                KeyModel.SetPositionAndRotation(frozenPosition, frozenRotation);
                startPosition = KeyModel.localPosition; startRotation = KeyModel.localRotation; elapsed = 0; frozen = false;
            }
            bool moving = phase == Phase.Drawing || phase == Phase.Stowing || phase == Phase.Aligning || phase == Phase.Returning;
            if (moving)
            {
                elapsed = Mathf.Min(MotionSeconds, elapsed + dt);
                float t = Mathf.SmoothStep(0, 1, elapsed / MotionSeconds);
                bool aligning = phase == Phase.Aligning;
                KeyModel.SetLocalPositionAndRotation(Vector3.Lerp(startPosition, aligning ? SeatedLocalPosition : Vector3.zero, t),
                    Quaternion.Slerp(startRotation, aligning ? SeatedLocalRotation : Quaternion.identity, t));
                finishMotion.TryApply(true);
            }
            RememberPose();
        }
        void LateUpdate()
        {
            if (!initialized) return;
            if (!CanStep())
            {
                if (poseRecorded) KeyModel.SetPositionAndRotation(frozenPosition, frozenRotation);
                frozen = true;
            }
            else RememberPose();
        }
    }
}
