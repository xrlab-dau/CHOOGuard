using System;
using UnityEngine;

namespace ChooGuard.App.Fps.TrainDeparture
{
    public enum DepartureContextAction { None, StowBag, InspectExterior, InspectAirbag, InspectBogie, InsertKey, TurnKey, CloseDoors, RetrieveKey, Radio }
    public enum DepartureActionLayer { Idle, Alignment, Draw, Contact, Mechanism, EquipmentFeedback, LampFocus, Release, Return, Stow, Recovery }

    /// <summary>Root-driven single-press actions. Timed layers move parts; equipment guards authorize completion.</summary>
    [DisallowMultipleComponent]
    public sealed class DepartureContextActions : MonoBehaviour
    {
        public TrainDepartureGame Game;
        public DepartureManipulation Manipulation;
        public Collider ExteriorContact, AirbagContact, BogieContact;
        [Header("The coordinator's one owned radio, parented to the player body")]
        public Transform RadioRoot, RadioRestAnchor, RadioUseAnchor, RadioPttPart;
        public Collider[] RadioColliders = Array.Empty<Collider>();
        public Vector3 RadioPttAxis = Vector3.right;
        public float RadioPttTravel = .0012f;
        public float RadioReach = 1.8f;
        public DepartureContextAction CurrentAction { get; private set; }
        public DepartureActionLayer CurrentLayer { get; private set; }
        public float LayerProgress => duration > 0 ? Mathf.Clamp01(elapsed / duration) : 0;
        public float PhysicalProgress
        {
            get
            {
                if (!IsBusy) return -1;
                if (CurrentLayer != DepartureActionLayer.EquipmentFeedback) return LayerProgress;
                if (CurrentAction != DepartureContextAction.CloseDoors) return -1; // Audio/bag completion is not a guessed timer.
                int closed = 0; foreach (var door in Game.Doors) if (door.IsClosed) closed++;
                return Game.Doors.Length == 16 ? closed / 16f : 0;
            }
        }
        public bool IsBusy => CurrentAction != DepartureContextAction.None;
        public bool FocusAvailable => available != DepartureContextAction.None;
        // Rendering asks again after the camera settles; the cached hint can describe an earlier aim.
        public bool TryResolveFocusContact(out RaycastHit contactHit)
        {
            contactHit = default;
            available = !CanRun() || IsBusy ? DepartureContextAction.None : ResolveAvailable();
            if (available != DepartureContextAction.None) contactHit = admissionHit;
            if (contactHit.collider == null) available = DepartureContextAction.None;
            PublishHint();
            return available != DepartureContextAction.None;
        }
        public bool RadioPttHeld => IsBusy && CurrentAction == DepartureContextAction.Radio && CurrentLayer == DepartureActionLayer.EquipmentFeedback;
        public bool RadioVoiceFinished { get; private set; }
        public string ContextHint { get; private set; } = "";
        /// <summary>Required for F admission. Fresh nearest-visible-surface check supplied by visual focus.
        /// Arguments: exact physical candidate collider, world hit point, eye-to-hit distance.
        /// A diagnostic adapter may bind this explicitly in tests; production has no permissive fallback.</summary>
        public Func<Collider, Vector3, float, bool> VisualAimValidator
        {
            get => visualAimValidator;
            set
            {
                visualAimValidator = value;
                if (value != null) return;
                queued = false; available = DepartureContextAction.None;
                ContextHint = ""; hud?.SetContextHint("");
            }
        }
        public string Diagnostic => visualAimValidator == null ? "Visual aim validator is not bound." : visualAimDiagnostic;
        Func<Collider, Vector3, float, bool> visualAimValidator;
        string visualAimDiagnostic = "";
        RaycastHit admissionHit;

        readonly RaycastHit[] hits = new RaycastHit[32];
        DepartureContextAction available, queuedAction;
        DepartureManipulable queuedPart;
        DepartureManipulable part, aimedPart;
        Collider inspection;
        DepartureGuardedOperation<DepartureContextAction> begin;
        DepartureGuardedOperation<DepartureActionLayer> advance;
        DepartureGuardedOperation<RaycastHit> visualAdmission;
        FirstPersonResponder lockedPlayer, subscribedPlayer;
        DeparturePlayHud hud;
        bool queued, locked, oldMovement, oldLook, oldInteraction, oldJump;
        bool radioCaptured;
        Transform capturedRadio, capturedPtt, radioParent;
        Vector3 radioRestPosition, radioMotionPosition, pttRest;
        Quaternion radioRestRotation, radioMotionRotation, withdrawnRotation, eyeRest, eyePose;
        float elapsed, duration, startAmount, oldFov, actionFov, actionElapsed;
        Camera actionCamera;
        DepartureKeyInventory Key => Game != null ? Game.KeyInventory : null;
        FirstPersonResponder Player => Game != null ? Game.Player : null;
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);

        void Awake() { Resolve(); }
        void OnEnable() { Resolve(); }
        void Resolve()
        {
            if (Game == null) Game = GetComponent<TrainDepartureGame>();
            if (Manipulation == null) Manipulation = GetComponent<DepartureManipulation>();
            if (hud == null) hud = GetComponent<DeparturePlayHud>();
            if (subscribedPlayer != Player)
            {
                if (subscribedPlayer != null) subscribedPlayer.PauseChanged -= Paused;
                subscribedPlayer = Player;
                if (subscribedPlayer != null) subscribedPlayer.PauseChanged += Paused;
            }
            if (begin != null) return;
            // Reuse the same cached guard-before-effect module for the cross-feature admission gate.
            visualAdmission = new DepartureGuardedOperation<RaycastHit>(ValidateVisualHit, null);
            // Actual reuse of the MIT UnityHFSM-derived guard/callback module, not timer-only acceptance.
            begin = new DepartureGuardedOperation<DepartureContextAction>(
                action => CanRun() && !IsBusy && action != DepartureContextAction.None && action == ResolveAvailable() && (queuedPart == null || aimedPart == queuedPart), StartAction);
            advance = new DepartureGuardedOperation<DepartureActionLayer>(
                _ => IsBusy && CanRun() && LayerProgress >= 1, EnterLayer);
        }
        bool CanRun() => isActiveAndEnabled && Game != null && Game.isActiveAndEnabled && Game.Started && !Game.Completed &&
            Player != null && Player.isActiveAndEnabled && Player.PlayerCamera != null && Player.PlayerCamera.isActiveAndEnabled &&
            !Player.IsPaused && (Player.ExternalInputMode || Player.HasFocus) && Time.timeScale > 0 && !Game.IsEmoting && (Game.ItemSlots == null || !Game.ItemSlots.IsSwitching);
        void Paused(bool paused) { if (paused) Cancel(); }
        void OnDisable()
        {
            Cancel();
            if (subscribedPlayer != null) subscribedPlayer.PauseChanged -= Paused;
            subscribedPlayer = null;
        }
        void OnApplicationPause(bool paused) { if (paused) Cancel(); }
        void OnApplicationFocus(bool focused) { if (!focused && (Player == null || !Player.ExternalInputMode)) Cancel(); }
        void LateUpdate()
        {
            if (IsBusy && !CanRun()) Cancel();
            // FPS controller owns yaw/pitch; this temporary lens/local-pose channel is restored on exit.
            if (locked && actionCamera != null) { actionCamera.transform.localRotation = eyePose; actionCamera.fieldOfView = actionFov; }
        }
        public void QueueInteraction()
        {
            Resolve();
            if (!CanRun() || IsBusy) return; // F spam while busy is discarded, never buffered.
            queuedAction = ResolveAvailable(); queuedPart = aimedPart;
            queued = queuedAction != DepartureContextAction.None;
        }
        public void NotifyRadioVoiceFinished() { if (RadioPttHeld) RadioVoiceFinished = true; }

        // Only the root calls these, after inventory sampling and before/after authoritative world feedback.
        public void BeforeWorld(float dt)
        {
            Resolve();
            if (!CanRun() || !Finite(dt) || dt <= 0 || dt > .25f) { Cancel(); return; }
            // A pause during the already-authorized withdrawal return must not strand the
            // only key in hand after Flow has reached radio. Finish its existing stow on resume.
            if (!IsBusy && Game.Flow.Stage == DepartureStage.SendDepartureRadio && Game.Mechanism.Retrieved && Key != null && Key.IsReadyInHand && (Game.ItemSlots == null || Game.ItemSlots.SelectedSlot == 4))
                Key.ToggleDraw();
            Manipulation?.RefreshAimOnly();
            available = IsBusy ? DepartureContextAction.None : ResolveAvailable();
            bool requested = queued; queued = false;
            if (requested && !IsBusy) begin.TryApply(queuedAction);
            queuedPart = null; queuedAction = DepartureContextAction.None;
            if (IsBusy)
            {
                actionElapsed += dt;
                // A missing physical response must release the player, never become a timed success.
                float limit = CurrentAction == DepartureContextAction.Radio && Game.DepartureRadio != null ? Game.DepartureRadio.length + 5 : 12;
                if (!BindingsStillValid() || actionElapsed > limit) { Cancel(); return; }
                elapsed = Mathf.Min(duration, elapsed + dt);
                TickLayer();
            }
            PublishHint();
        }
        public void AfterWorld()
        {
            if (!IsBusy || !CanRun()) return;
            if (CurrentAction == DepartureContextAction.CloseDoors && CurrentLayer == DepartureActionLayer.EquipmentFeedback &&
                Game.Flow.Stage == DepartureStage.ObserveClosedLamp && Game.Mechanism.ClosedLamp && Game.Mechanism.CloseCommand)
                advance.TryApply(DepartureActionLayer.LampFocus);
            if (CurrentAction == DepartureContextAction.Radio && CurrentLayer == DepartureActionLayer.EquipmentFeedback && RadioVoiceFinished)
                advance.TryApply(DepartureActionLayer.Release);
        }
        bool ValidateVisualHit(RaycastHit hit)
        {
            if (visualAimValidator == null) return false;
            try
            {
                bool accepted = hit.collider != null && visualAimValidator(hit.collider, hit.point, hit.distance);
                visualAimDiagnostic = accepted ? "" : "Physical contact is not the nearest visible target surface.";
                return accepted;
            }
            catch (Exception exception)
            {
                visualAimDiagnostic = "Visual aim validation failed: " + exception.GetType().Name;
                return false;
            }
        }
        DepartureContextAction ResolveAvailable()
        {
            visualAimDiagnostic = ""; admissionHit = default;
            if (visualAimValidator == null) return DepartureContextAction.None;
            var action = ResolvePhysicalAvailable();
            if (action == DepartureContextAction.None) return action;
            // Explicit inspection/radio queries already recorded their exact physical hit.
            // Controls expose AimedPart, so recover its actual contact after ownership/front/reach checks.
            bool control = action == DepartureContextAction.StowBag || action == DepartureContextAction.InsertKey ||
                action == DepartureContextAction.TurnKey || action == DepartureContextAction.CloseDoors || action == DepartureContextAction.RetrieveKey;
            if (control)
            {
                admissionHit = default;
                if (aimedPart == null || !aimedPart.Ready) return DepartureContextAction.None;
                foreach (var contact in aimedPart.ContactColliders)
                {
                    if (!aimedPart.Owns(contact) || !ExactContact(contact, Mathf.Min(Manipulation.MaximumReach, aimedPart.Reach), out var hit) ||
                        !aimedPart.FrontFacing(Player.PlayerCamera.transform.position, hit.point)) continue;
                    if (visualAdmission.TryApply(hit)) return action;
                }
                return DepartureContextAction.None;
            }
            if (action == DepartureContextAction.Radio)
            {
                // A nearer PTT surface may overlap the body's collider; test both real contacts.
                foreach (var contact in RadioColliders)
                    if (ExactContact(contact, RadioReach, out var hit) && visualAdmission.TryApply(hit)) return action;
                return DepartureContextAction.None;
            }
            // Runs for hints, queued presses and again inside the guarded Begin operation.
            return visualAdmission.TryApply(admissionHit) ? action : DepartureContextAction.None;
        }
        DepartureContextAction ResolvePhysicalAvailable()
        {
            if (!CanRun() || Manipulation == null) return DepartureContextAction.None;
            Manipulation.RefreshAimOnly(); aimedPart = Manipulation.AimedPart;
            var stage = Game.Flow.Stage; var mechanism = Game.Mechanism;
            if (stage == DepartureStage.EnterCrewSpace && Game.InsideTrain && Game.HasOwnBag &&
                (Game.ItemSlots == null || Game.ItemSlots.SelectedSlot == 3) && Game.LuggageStowTarget != null && Game.OwnBag != null && aimedPart == Game.LuggageHandle && aimedPart != null && aimedPart.Unlocked && (Key == null || !Key.OccupiesHand))
                return DepartureContextAction.StowBag;
            if (stage == DepartureStage.InspectExterior && !Game.InsideTrain)
            {
                if (!Game.Flow.AirbagObserved && ExactVisible(AirbagContact, Game.Airbag, 2.8f)) return DepartureContextAction.InspectAirbag;
                if (!Game.Flow.BogieObserved && ExactVisible(BogieContact, Game.Bogie, 2.5f)) return DepartureContextAction.InspectBogie;
                if (!Game.Flow.ExteriorObserved && ExactVisible(ExteriorContact, Game.Exterior, 4)) return DepartureContextAction.InspectExterior;
            }
            if (stage == DepartureStage.CloseDoors && Key != null && Key.ValidateBinding(out _) && aimedPart == Key.Socket && !mechanism.Retrieved)
            {
                if (mechanism.Depth < 1 && (Key.SocketReady || Key.IsReadyInHand && (Game.ItemSlots == null || Game.ItemSlots.KeySelected))) return DepartureContextAction.InsertKey;
                if (mechanism.Depth >= 1 && mechanism.Rotation < 1 && Key.SocketReady) return DepartureContextAction.TurnKey;
            }
            if ((stage == DepartureStage.CloseDoors || stage == DepartureStage.ObserveClosedLamp) && Key != null && Key.SocketReady &&
                mechanism.Depth >= 1 && mechanism.Rotation >= 1 && !mechanism.Retrieved && aimedPart != null && aimedPart.Kind == DeparturePartKind.CloseButton)
                return DepartureContextAction.CloseDoors;
            if (stage == DepartureStage.RetrieveKey && Key != null && Key.SocketReady && aimedPart == Key.Socket && !mechanism.Retrieved && mechanism.ClosedLamp)
                return DepartureContextAction.RetrieveKey;
            if (stage == DepartureStage.SendDepartureRadio && (Key == null || !Key.OccupiesHand) && Game.DepartureRadio != null && (Game.ItemSlots == null || Game.ItemSlots.RadioSelected) && RadioValid() && RadioAimed())
                return DepartureContextAction.Radio;
            return DepartureContextAction.None;
        }
        bool ExactVisible(Collider contact, Renderer renderer, float reach)
        {
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy ||
                contact == null || !(contact.transform == renderer.transform || contact.transform.IsChildOf(renderer.transform))) return false;
            return ExactContact(contact, reach, out _);
        }
        bool ExactContact(Collider contact, float reach, out RaycastHit contactHit)
        {
            contactHit = default;
            if (contact == null || !contact.enabled || contact.isTrigger || !contact.gameObject.activeInHierarchy || !Finite(reach) || reach <= 0) return false;
            var eye = Player.PlayerCamera.transform;
            // Official Collider.Raycast module performs an actual surface query, including owned player children.
            if (!contact.Raycast(new Ray(eye.position, eye.forward), out contactHit, reach) || Vector3.Dot(contactHit.normal, -eye.forward) < .05f) return false;
            var bag = Game.OwnBag != null && Game.OwnBag.gameObject.activeInHierarchy ? Game.OwnBag : null;
            var key = Key != null && !Key.IsSocketed ? Key.KeyModel : null;
            bool found = NearestSolidRay.TryCast(eye.position, eye.forward, reach, ~0, hits, Player.transform, bag, key, out var world, out bool saturated);
            bool accepted = !saturated && (!found || world.collider == contact || world.distance >= contactHit.distance - .001f);
            if (accepted) admissionHit = contactHit;
            return accepted;
        }
        bool RadioValid()
        {
            if (RadioRoot == null || RadioPttPart == null || !RadioRoot.gameObject.activeInHierarchy || !RadioRoot.IsChildOf(Player.transform) ||
                RadioRoot.IsChildOf(Player.PlayerCamera.transform) || !RadioPttPart.IsChildOf(RadioRoot) || RadioPttPart == RadioRoot ||
                !Finite(RadioPttTravel) || RadioPttTravel <= 0 || !Finite(RadioPttAxis.sqrMagnitude) || RadioPttAxis.sqrMagnitude < .000001f ||
                RadioColliders == null || RadioColliders.Length == 0) return false;
            foreach (var c in RadioColliders) if (c == null || c.isTrigger || !c.transform.IsChildOf(RadioRoot)) return false;
            return true;
        }
        bool RadioAimed()
        {
            foreach (var c in RadioColliders) if (ExactContact(c, RadioReach, out _)) return true;
            return false;
        }
        bool BindingsStillValid()
        {
            if (CurrentAction == DepartureContextAction.Radio) return RadioValid() && RadioRoot == capturedRadio && RadioPttPart == capturedPtt && RadioRoot.parent == radioParent;
            bool inspecting = CurrentAction == DepartureContextAction.InspectExterior || CurrentAction == DepartureContextAction.InspectAirbag || CurrentAction == DepartureContextAction.InspectBogie;
            if (inspecting) return inspection != null && inspection.enabled && inspection.gameObject.activeInHierarchy;
            if (part == null || !part.CaptureBinding()) return false;
            if (CurrentAction == DepartureContextAction.InsertKey || CurrentAction == DepartureContextAction.TurnKey || CurrentAction == DepartureContextAction.RetrieveKey)
                return Key != null && Key.isActiveAndEnabled && Key.ValidateBinding(out _) && Key.Socket == part;
            return true;
        }
        void StartAction(DepartureContextAction action)
        {
            Manipulation.Release(); // Once, before our owned hold begins. Never each root frame.
            CurrentAction = action; part = aimedPart; inspection = null; RadioVoiceFinished = false; actionElapsed = 0;
            lockedPlayer = Player; oldMovement = Player.MovementBlocked; oldLook = Player.SuppressLookInput;
            oldInteraction = Player.SuppressInteractionInput; oldJump = Player.EnableJump;
            Player.MovementBlocked = Player.SuppressLookInput = Player.SuppressInteractionInput = true; Player.EnableJump = false; locked = true;
            actionCamera = Player.PlayerCamera; eyeRest = eyePose = actionCamera.transform.localRotation; oldFov = actionFov = actionCamera.fieldOfView;
            switch (action)
            {
                case DepartureContextAction.InspectExterior: part = null; inspection = ExteriorContact; break;
                case DepartureContextAction.InspectAirbag: part = null; inspection = AirbagContact; break;
                case DepartureContextAction.InspectBogie: part = null; inspection = BogieContact; break;
                case DepartureContextAction.Radio:
                    part = null; CaptureRadio(); break;
            }
            if (action == DepartureContextAction.InsertKey)
            {
                if (Key.SocketReady) EnterLayer(DepartureActionLayer.Mechanism);
                else if (Key.IsReadyInHand) { if (!Key.BeginSocketGrip(part)) { Cancel(); return; } EnterLayer(DepartureActionLayer.Alignment); }
                else { Cancel(); return; } // F never auto-draws a second prop.
            }
            else EnterLayer(DepartureActionLayer.Alignment);
        }
        void EnterLayer(DepartureActionLayer layer)
        {
            CurrentLayer = layer; elapsed = 0;
            duration = .15f;
            if (layer == DepartureActionLayer.Draw || layer == DepartureActionLayer.Return || layer == DepartureActionLayer.Stow) duration = .3f;
            if (layer == DepartureActionLayer.Alignment) duration = CurrentAction == DepartureContextAction.InsertKey || CurrentAction == DepartureContextAction.Radio ? .3f : .15f;
            if (layer == DepartureActionLayer.Contact) duration = CurrentAction == DepartureContextAction.Radio ? .12f : .18f;
            if (layer == DepartureActionLayer.Mechanism) duration = CurrentAction == DepartureContextAction.TurnKey ? .35f : .4f;
            if (layer == DepartureActionLayer.EquipmentFeedback) duration = inspection != null ? .4f : .01f;
            if (layer == DepartureActionLayer.LampFocus) duration = .25f;
            if (layer == DepartureActionLayer.Release) duration = CurrentAction == DepartureContextAction.Radio ? .12f : .15f;
            if (part != null) startAmount = part.Amount;
            if (layer == DepartureActionLayer.Mechanism && CurrentAction == DepartureContextAction.InsertKey) startAmount = Game.Mechanism.Depth;
            if (layer == DepartureActionLayer.Mechanism && CurrentAction == DepartureContextAction.TurnKey) startAmount = Game.Mechanism.Rotation;
            if (layer == DepartureActionLayer.Mechanism && CurrentAction == DepartureContextAction.RetrieveKey) { startAmount = Game.Mechanism.Depth; withdrawnRotation = Key.KeyModel.rotation; }
            if (CurrentAction == DepartureContextAction.Radio && (layer == DepartureActionLayer.Alignment || layer == DepartureActionLayer.Return))
            { radioMotionPosition = RadioRoot.localPosition; radioMotionRotation = RadioRoot.localRotation; }
        }
        void TickLayer()
        {
            float t = Mathf.SmoothStep(0, 1, LayerProgress);
            if (CurrentLayer == DepartureActionLayer.Draw)
            {
                if (LayerProgress >= 1 && Key.IsReadyInHand)
                { if (Key.BeginSocketGrip(part)) advance.TryApply(DepartureActionLayer.Alignment); else Cancel(); }
                return;
            }
            if (CurrentLayer == DepartureActionLayer.Alignment)
            {
                actionFov = Mathf.Lerp(oldFov, oldFov - 2, t);
                if (CurrentAction == DepartureContextAction.Radio) MoveRadio(true, t);
                if (CurrentAction == DepartureContextAction.InsertKey && !Key.SocketReady) return;
                advance.TryApply(inspection != null ? DepartureActionLayer.EquipmentFeedback :
                    CurrentAction == DepartureContextAction.CloseDoors || CurrentAction == DepartureContextAction.Radio ? DepartureActionLayer.Contact : DepartureActionLayer.Mechanism);
                return;
            }
            if (CurrentLayer == DepartureActionLayer.Contact)
            {
                if (CurrentAction == DepartureContextAction.Radio) SetPtt(t);
                else { part.SetAmount(t, false); Game.Mechanism.SetButtonHeld(part.Amount >= part.PressThreshold); }
                advance.TryApply(DepartureActionLayer.EquipmentFeedback); return;
            }
            if (CurrentLayer == DepartureActionLayer.Mechanism)
            {
                var mechanism = Game.Mechanism;
                switch (CurrentAction)
                {
                    case DepartureContextAction.StowBag:
                        part.SetAmount(Mathf.Lerp(startAmount, 1, t), true);
                        if (LayerProgress >= 1) { Game.BeginLuggageStow(); advance.TryApply(DepartureActionLayer.EquipmentFeedback); }
                        break;
                    case DepartureContextAction.InsertKey:
                        mechanism.Insert(Mathf.Max(0, Mathf.Lerp(startAmount, 1, t) - mechanism.Depth)); part.ApplyKeyPose(mechanism);
                        if (mechanism.Depth >= 1) advance.TryApply(DepartureActionLayer.Recovery); break;
                    case DepartureContextAction.TurnKey:
                        mechanism.Rotate(Mathf.Max(0, Mathf.Lerp(startAmount, 1, t) - mechanism.Rotation)); part.ApplyKeyPose(mechanism);
                        if (mechanism.Rotation >= 1) advance.TryApply(DepartureActionLayer.Recovery); break;
                    case DepartureContextAction.RetrieveKey:
                        mechanism.Withdraw(Mathf.Max(0, mechanism.Depth - Mathf.Lerp(startAmount, 0, t))); part.ApplyKeyPose(mechanism);
                        if (mechanism.Retrieved) { Key.CompleteRetrieval(withdrawnRotation); advance.TryApply(DepartureActionLayer.Return); }
                        break;
                }
                return;
            }
            if (CurrentLayer == DepartureActionLayer.EquipmentFeedback)
            {
                if (inspection != null)
                {
                    Renderer renderer = CurrentAction == DepartureContextAction.InspectAirbag ? Game.Airbag : CurrentAction == DepartureContextAction.InspectBogie ? Game.Bogie : Game.Exterior;
                    float reach = CurrentAction == DepartureContextAction.InspectAirbag ? 2.8f : CurrentAction == DepartureContextAction.InspectBogie ? 2.5f : 4;
                    if (!ExactVisible(inspection, renderer, reach)) { Cancel(); return; }
                    if (LayerProgress >= 1)
                    {
                        Game.Flow.TryAction(CurrentAction == DepartureContextAction.InspectAirbag ? DepartureAction.InspectAirbag : CurrentAction == DepartureContextAction.InspectBogie ? DepartureAction.InspectBogie : DepartureAction.InspectExterior);
                        advance.TryApply(DepartureActionLayer.Recovery);
                    }
                }
                else if (CurrentAction == DepartureContextAction.StowBag && !Game.HasOwnBag) advance.TryApply(DepartureActionLayer.Release);
                else if (CurrentAction == DepartureContextAction.CloseDoors) { part.SetAmount(1, false); Game.Mechanism.SetButtonHeld(true); }
                else if (CurrentAction == DepartureContextAction.Radio) SetPtt(1);
                return;
            }
            if (CurrentLayer == DepartureActionLayer.LampFocus)
            {
                part.SetAmount(1, false); Game.Mechanism.SetButtonHeld(true);
                if (!Game.Mechanism.ClosedLamp || Game.ClosedLamp == null) { Cancel(); return; }
                var direction = Game.ClosedLamp.bounds.center - actionCamera.transform.position;
                if (direction.sqrMagnitude > .000001f)
                    eyePose = Quaternion.Slerp(eyeRest, Quaternion.Inverse(actionCamera.transform.parent.rotation) * Quaternion.LookRotation(direction, Vector3.up), t);
                actionCamera.transform.localRotation = eyePose;
                if (LayerProgress >= 1 && Game.IsClosedLampVisible() && Game.Mechanism.TryObserveLamp())
                { Game.Flow.TryAction(DepartureAction.ObserveChangedLamp); advance.TryApply(DepartureActionLayer.Release); }
                return;
            }
            if (CurrentLayer == DepartureActionLayer.Release)
            {
                if (CurrentAction == DepartureContextAction.Radio) SetPtt(1 - t);
                else { part.SetAmount(1 - t, false); Game.Mechanism.SetButtonHeld(false); }
                advance.TryApply(CurrentAction == DepartureContextAction.Radio ? DepartureActionLayer.Return : DepartureActionLayer.Recovery); return;
            }
            if (CurrentLayer == DepartureActionLayer.Return)
            {
                if (CurrentAction == DepartureContextAction.Radio) { MoveRadio(false, t); advance.TryApply(DepartureActionLayer.Recovery); }
                else if (LayerProgress >= 1 && Key.IsReadyInHand) { Key.ToggleDraw(); advance.TryApply(DepartureActionLayer.Stow); }
                return;
            }
            if (CurrentLayer == DepartureActionLayer.Stow)
            { if (!Key.IsInHand && !Key.IsDrawn) advance.TryApply(DepartureActionLayer.Recovery); return; }
            if (CurrentLayer == DepartureActionLayer.Recovery)
            {
                actionFov = Mathf.Lerp(oldFov - 2, oldFov, t); eyePose = Quaternion.Slerp(eyePose, eyeRest, t);
                if (LayerProgress >= 1)
                {
                    bool completeRadio = CurrentAction == DepartureContextAction.Radio && RadioVoiceFinished;
                    Finish();
                    if (completeRadio) Game.Flow.TryAction(DepartureAction.TransmitDeparture);
                }
            }
        }
        void CaptureRadio()
        {
            if (radioCaptured && capturedRadio == RadioRoot && capturedPtt == RadioPttPart) return;
            capturedRadio = RadioRoot; capturedPtt = RadioPttPart; radioParent = RadioRoot.parent;
            radioRestPosition = RadioRestAnchor != null ? radioParent.InverseTransformPoint(RadioRestAnchor.position) : RadioRoot.localPosition;
            radioRestRotation = RadioRestAnchor != null ? Quaternion.Inverse(radioParent.rotation) * RadioRestAnchor.rotation : RadioRoot.localRotation;
            pttRest = RadioPttPart.localPosition; radioCaptured = true;
        }
        void MoveRadio(bool use, float t)
        {
            var anchor = use ? RadioUseAnchor : RadioRestAnchor;
            var position = anchor != null ? radioParent.InverseTransformPoint(anchor.position) : radioRestPosition + (use ? new Vector3(0, .25f, .04f) : Vector3.zero);
            var rotation = anchor != null ? Quaternion.Inverse(radioParent.rotation) * anchor.rotation : radioRestRotation;
            RadioRoot.SetLocalPositionAndRotation(Vector3.Lerp(radioMotionPosition, position, t), Quaternion.Slerp(radioMotionRotation, rotation, t));
        }
        void SetPtt(float amount) { if (capturedPtt != null) capturedPtt.localPosition = pttRest + RadioPttAxis.normalized * (RadioPttTravel * Mathf.Clamp01(amount)); }
        public void Cancel()
        {
            queued = false; queuedPart = null; queuedAction = DepartureContextAction.None;
            if (IsBusy && CurrentAction == DepartureContextAction.InsertKey && Key != null && Game.Mechanism.Depth <= 0) Key.EndSocketGrip();
            bool cancellingRadio = IsBusy && CurrentAction == DepartureContextAction.Radio;
            if (cancellingRadio) Game.CancelDepartureTransmission();
            // A later menu cancel must not pull an already pocketed radio back to its carry pose.
            if (radioCaptured && cancellingRadio)
            {
                SetPtt(0);
                if (capturedRadio != null && capturedRadio.parent == radioParent) capturedRadio.SetLocalPositionAndRotation(radioRestPosition, radioRestRotation);
            }
            Finish();
        }
        void Finish()
        {
            if (Game != null) Game.Mechanism.SetButtonHeld(false);
            if (part != null && (part.Kind == DeparturePartKind.CloseButton || part.Kind == DeparturePartKind.CrewHandle)) part.EndGrip();
            if (locked)
            {
                if (lockedPlayer != null) { lockedPlayer.MovementBlocked = oldMovement; lockedPlayer.SuppressLookInput = oldLook; lockedPlayer.SuppressInteractionInput = oldInteraction; lockedPlayer.EnableJump = oldJump; }
                if (actionCamera != null) { actionCamera.transform.localRotation = eyeRest; actionCamera.fieldOfView = oldFov; }
            }
            locked = false; lockedPlayer = null; actionCamera = null; part = null; inspection = null;
            CurrentAction = DepartureContextAction.None; CurrentLayer = DepartureActionLayer.Idle; elapsed = duration = 0;
            available = DepartureContextAction.None; ContextHint = ""; hud?.SetContextHint("");
        }
        void PublishHint()
        {
            ContextHint = "";
            if (!CanRun()) { hud?.SetContextHint(""); return; }
            if (!IsBusy)
            {
                switch (available)
                {
                    case DepartureContextAction.StowBag: ContextHint = "[F] 손잡이 당기기"; break;
                    case DepartureContextAction.InspectExterior: ContextHint = "[F] 외관 확인"; break;
                    case DepartureContextAction.InspectAirbag: ContextHint = "[F] 에어백 확인"; break;
                    case DepartureContextAction.InspectBogie: ContextHint = "[F] 대차 확인"; break;
                    case DepartureContextAction.InsertKey: ContextHint = "[F] 베른키 삽입"; break;
                    case DepartureContextAction.TurnKey: ContextHint = "[F] 베른키 돌리기"; break;
                    case DepartureContextAction.CloseDoors: ContextHint = "[F] 문 닫기"; break;
                    case DepartureContextAction.RetrieveKey: ContextHint = "[F] 베른키 회수"; break;
                    case DepartureContextAction.Radio: ContextHint = "[F] 발차 무전"; break;
                }
                if (Game.Flow.Stage == DepartureStage.SignalCaptain && !Game.InsideTrain && (Game.ItemSlots == null || Game.ItemSlots.SelectedSlot == 4 && !Game.ItemSlots.IsSwitching) && (Key == null || !Key.OccupiesHand) && Game.SignalEmote != null)
                    ContextHint = "[G] 출발 수신호";
            }
            hud?.SetContextHint(ContextHint);
        }
    }
}
