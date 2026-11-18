using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace ChooGuard.App.Fps.TrainDeparture
{
    // Camera foundation: installed Unity Cinemachine 3.1.7, revision
    // 8cb586bf217170b0035e42685880fe1fdefc9729. Brain, CameraManagerBase,
    // BlendManager, Follow, HardLookAt and Deoccluder execute the camera work unchanged.
    // Unity Companion Cinemachine Code License 1.0; copyright 2025 Unity Technologies ApS.
    // Retain the package LICENSE.md/license.txt. Application code owns only signal/input lifecycle.
    [DefaultExecutionOrder(1100), DisallowMultipleComponent]
    public sealed class DepartureSignalEmote : MonoBehaviour
    {
        public FirstPersonResponder Player;
        public DepartureAnimationRig Rig;
        public Camera EmoteCamera;
        public Transform ActorRoot, UpperBodyTarget;
        public CinemachineBrain Brain;
        public DepartureSignalCameraSelector Selector;
        public CinemachineCamera FpsEndpoint, EmoteEndpoint;
        public DepartureSignalCameraDeoccluder Deoccluder;
        public int ActorLayer = 31;
        public Vector3 FacingDirection = Vector3.forward;
        public bool IsPlaying => phase != Phase.Idle;

        enum Phase { Idle, PrimingFps, PrimingBlend, Signal, Hold, Returning }
        const float CompletedHoldSeconds = .3f, AttemptLimitSeconds = 12, ActorClearance = .9f;
        Phase phase;
        bool paused, requestedHeld, captured, oldMovement, oldLook, oldInteraction, oldJump;
        bool oldFpsEnabled, oldEmoteEnabled, oldBrainEnabled, oldSelection, outputVisible;
        bool oldOrthographic;
        int oldEmoteMask, sampleFrame = -1, cameraFrame = -1;
        float holdTime, attemptTime, sampleDt, oldFov, oldNear, oldFar, oldOrthoSize;
        LensSettings oldFpsLens;
        CinemachineCore.BlendHints oldFpsHint;
        FirstPersonResponder lockedPlayer;
        DepartureAnimationRig activeRig;
        Camera fps, output;
        CinemachineBrain activeBrain;
        DepartureSignalCameraSelector activeSelector;
        DepartureSignalCameraDeoccluder activeDeoccluder;
        CinemachineCamera activeFpsEndpoint, activeEmoteEndpoint;
        Transform activeActor, activeTarget;

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(Quaternion v) => Finite(v.x) && Finite(v.y) && Finite(v.z) && Finite(v.w);

        bool SourceVisible(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                !renderer.gameObject.activeInHierarchy || renderer.gameObject.layer != ActorLayer ||
                !renderer.transform.IsChildOf(ActorRoot)) return false;
            var skin = renderer as SkinnedMeshRenderer;
            if (skin != null) return skin.sharedMesh != null && skin.sharedMesh.vertexCount > 0;
            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0;
        }

        bool ValidBindings()
        {
            if (captured && (Player != lockedPlayer || Rig != activeRig || EmoteCamera != output ||
                Brain != activeBrain || Selector != activeSelector || Deoccluder != activeDeoccluder ||
                FpsEndpoint != activeFpsEndpoint || EmoteEndpoint != activeEmoteEndpoint ||
                ActorRoot != activeActor || UpperBodyTarget != activeTarget || Player == null ||
                Player.PlayerCamera != fps)) return false;
            if (!isActiveAndEnabled || Player == null || !Player.isActiveAndEnabled || Rig == null ||
                !Rig.isActiveAndEnabled || Rig.Paused || EmoteCamera == null || ActorRoot == null ||
                UpperBodyTarget == null || !UpperBodyTarget.gameObject.activeInHierarchy ||
                !UpperBodyTarget.IsChildOf(Player.transform) || !ActorRoot.gameObject.activeInHierarchy ||
                !ActorRoot.IsChildOf(Player.transform) || Rig.ViewFrame != Player.transform ||
                Player.PlayerCamera == null || EmoteCamera == Player.PlayerCamera ||
                !EmoteCamera.gameObject.activeInHierarchy || !Player.PlayerCamera.gameObject.activeInHierarchy ||
                Player.PlayerCamera.transform.IsChildOf(EmoteCamera.transform) ||
                ActorLayer < 0 || ActorLayer > 31 || !Finite(FacingDirection) ||
                Vector3.ProjectOnPlane(FacingDirection, Vector3.up).sqrMagnitude < .0001f ||
                !Finite(Player.transform.position) || !Finite(Player.PlayerCamera.transform.position) ||
                !Finite(Player.PlayerCamera.transform.rotation) || !Finite(UpperBodyTarget.position) ||
                (Player.PlayerCamera.cullingMask & (1 << ActorLayer)) != 0 ||
                ((captured ? oldEmoteMask : EmoteCamera.cullingMask) & (1 << ActorLayer)) == 0 ||
                (!captured && (!Player.PlayerCamera.enabled || EmoteCamera.enabled)) ||
                Rig.Left == null || Rig.Right == null || !SourceVisible(Rig.Left.VisibleMesh) ||
                !SourceVisible(Rig.Right.VisibleMesh)) return false;
            return ValidCameraModules();
        }

        bool ValidCameraModules()
        {
            if (Brain == null || !Brain.gameObject.activeInHierarchy || (captured && !Brain.enabled) ||
                Brain.OutputCamera != EmoteCamera ||
                Brain.ControlledObject != EmoteCamera.gameObject ||
                Brain.UpdateMethod != CinemachineBrain.UpdateMethods.ManualUpdate ||
                Brain.BlendUpdateMethod != CinemachineBrain.BrainUpdateMethods.LateUpdate ||
                Brain.DefaultBlend.Style != CinemachineBlendDefinition.Styles.Cut || Brain.CustomBlends != null ||
                Selector == null || !Selector.isActiveAndEnabled || FpsEndpoint == null || EmoteEndpoint == null ||
                FpsEndpoint == EmoteEndpoint || !FpsEndpoint.isActiveAndEnabled || !EmoteEndpoint.isActiveAndEnabled ||
                FpsEndpoint.transform.parent != Selector.transform || EmoteEndpoint.transform.parent != Selector.transform ||
                Selector.FpsCamera != FpsEndpoint || Selector.EmoteCamera != EmoteEndpoint ||
                Selector.LookAt != UpperBodyTarget || FpsEndpoint.LookAt != UpperBodyTarget ||
                EmoteEndpoint.LookAt != UpperBodyTarget || FpsEndpoint.Follow != Player.PlayerCamera.transform ||
                EmoteEndpoint.Follow != Player.transform || Selector.CustomBlends != null ||
                Selector.DefaultBlend.Style != CinemachineBlendDefinition.Styles.EaseInOut ||
                !Finite(Selector.DefaultBlend.Time) || Mathf.Abs(Selector.DefaultBlend.Time - .25f) > .001f ||
                !Brain.IsValidChannel(Selector) || Deoccluder == null || !Deoccluder.isActiveAndEnabled ||
                Deoccluder.ComponentOwner != Selector || Deoccluder.CollideAgainst.value == 0 ||
                (Deoccluder.CollideAgainst.value & ((1 << 2) | (1 << ActorLayer))) != 0 ||
                Deoccluder.TransparentLayers.value != 0 || !Deoccluder.AvoidObstacles.Enabled ||
                Deoccluder.AvoidObstacles.UseFollowTarget.Enabled ||
                Deoccluder.AvoidObstacles.Strategy != CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward ||
                Deoccluder.AvoidObstacles.MinimumOcclusionTime != 0 ||
                Deoccluder.AvoidObstacles.DampingWhenOccluded != 0 || Deoccluder.AvoidObstacles.DistanceLimit != 0 ||
                !Finite(Deoccluder.AvoidObstacles.CameraRadius) || Deoccluder.AvoidObstacles.CameraRadius < .1f ||
                !Finite(Deoccluder.MinimumDistanceFromTarget) || Deoccluder.MinimumDistanceFromTarget < .01f ||
                Deoccluder.MinimumDistanceFromTarget > .1f) return false;
            // These must be the actual active package pipeline modules, not merely unused components.
            var position = FpsEndpoint.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachineHardLockToTarget;
            var rotation = FpsEndpoint.GetCinemachineComponent(CinemachineCore.Stage.Aim) as CinemachineRotateWithFollowTarget;
            var follow = EmoteEndpoint.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachineFollow;
            var aim = EmoteEndpoint.GetCinemachineComponent(CinemachineCore.Stage.Aim) as CinemachineHardLookAt;
            return position != null && position.IsValid && position.Damping == 0 &&
                rotation != null && rotation.IsValid && rotation.Damping == 0 &&
                follow != null && follow.IsValid && Finite(follow.FollowOffset) &&
                follow.TrackerSettings.BindingMode == BindingMode.LockToTargetWithWorldUp &&
                aim != null && aim.IsValid && Finite(aim.LookAtOffset);
        }

        bool CanRun() => !paused && Player != null && !Player.IsPaused && Time.timeScale > 0 &&
            (Player.ExternalInputMode || Player.HasFocus);
        bool PrecisionFree() => Player != null && !Player.Holding &&
            (Rig == null || Rig.Manipulation == null || !Rig.Manipulation.IsHolding);

        /// <summary>Sample G held each live frame, including after Flow advances. True only on real rig success.</summary>
        public bool Step(bool requested, bool outside, bool allowed, float dt)
        {
            bool pressed = requested && !requestedHeld;
            requestedHeld = requested;
            if (!Finite(dt) || dt <= 0 || dt > .25f || !outside || !CanRun())
            { if (IsPlaying) Cancel(); return false; }
            if (!IsPlaying)
            {
                if (!pressed || !allowed || !ValidBindings() || !PrecisionFree()) return false;
                Begin();
            }
            if (!ValidBindings() || !PrecisionFree() ||
                (phase != Phase.Hold && phase != Phase.Returning && !allowed))
            { Cancel(); return false; }
            sampleFrame = Time.frameCount; sampleDt = dt;
            LockInput();
            if (phase == Phase.Returning) return false;
            if (phase == Phase.Hold)
            {
                activeRig.StepSignal(true, outside, dt);
                holdTime += dt;
                if (holdTime >= CompletedHoldSeconds)
                { activeRig.CancelSignal(); activeSelector.ShowEmote = false; phase = Phase.Returning; }
                return false;
            }
            attemptTime += dt;
            if (attemptTime > AttemptLimitSeconds) { Cancel(); return false; }
            if (!activeRig.StepSignal(true, outside, dt)) return false;
            if (phase != Phase.Signal || !outputVisible || cameraFrame < 0 || activeDeoccluder.CorrectedFrame != cameraFrame)
            { Cancel(); return false; }
            phase = Phase.Hold; holdTime = 0;
            return true;
        }

        void LockInput()
        {
            lockedPlayer.MovementBlocked = lockedPlayer.SuppressLookInput = lockedPlayer.SuppressInteractionInput = true;
            lockedPlayer.EnableJump = false;
        }

        void Begin()
        {
            lockedPlayer = Player; activeRig = Rig; output = EmoteCamera; fps = Player.PlayerCamera;
            activeBrain = Brain; activeSelector = Selector; activeDeoccluder = Deoccluder;
            activeFpsEndpoint = FpsEndpoint; activeEmoteEndpoint = EmoteEndpoint;
            activeActor = ActorRoot; activeTarget = UpperBodyTarget;
            oldMovement = Player.MovementBlocked; oldLook = Player.SuppressLookInput;
            oldInteraction = Player.SuppressInteractionInput; oldJump = Player.EnableJump;
            oldFpsEnabled = fps.enabled; oldEmoteEnabled = output.enabled; oldBrainEnabled = Brain.enabled;
            oldEmoteMask = output.cullingMask; oldFov = output.fieldOfView; oldNear = output.nearClipPlane;
            oldFar = output.farClipPlane; oldOrthoSize = output.orthographicSize; oldOrthographic = output.orthographic;
            oldSelection = Selector.ShowEmote; oldFpsLens = FpsEndpoint.Lens; oldFpsHint = FpsEndpoint.BlendHint;
            captured = true;
            LockInput(); activeRig.CancelSignal();
            FpsEndpoint.Lens = LensSettings.FromCamera(fps);
            // Package CameraBlendStack snapshots the actual original FPS state when blending away.
            FpsEndpoint.BlendHint |= CinemachineCore.BlendHints.FreezeWhenBlendingOut;
            Selector.ShowEmote = false; Selector.PreviousStateIsValid = false;
            Brain.enabled = true; Brain.ResetState();
            foreach (var listener in output.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            output.cullingMask = oldEmoteMask & ~(1 << ActorLayer);
            outputVisible = false; cameraFrame = -1; holdTime = attemptTime = 0;
            phase = Phase.PrimingFps;
        }

        void LateUpdate()
        {
            if (!IsPlaying) return;
            if (sampleFrame != Time.frameCount || !CanRun() || !ValidBindings() || !PrecisionFree())
            { Cancel(); return; }
            LockInput();
            // This execution order is after DepartureAnimationRig (1000). No camera tick runs in Step.
            bool priming = phase == Phase.PrimingFps || phase == Phase.PrimingBlend;
            activeBrain.ManualUpdate(Time.frameCount, priming ? 0 : sampleDt);
            if (!ReferenceEquals(activeBrain.ActiveVirtualCamera, activeSelector) || activeBrain.IsBlending ||
                activeDeoccluder.CorrectedFrame != Time.frameCount || !Finite(output.transform.position) ||
                !Finite(output.transform.rotation) || !Finite(output.fieldOfView))
            { Cancel(); return; }
            cameraFrame = Time.frameCount;
            outputVisible = Vector3.ProjectOnPlane(output.transform.position - activeTarget.position, Vector3.up)
                .sqrMagnitude >= ActorClearance * ActorClearance;
            output.cullingMask = outputVisible ? oldEmoteMask : oldEmoteMask & ~(1 << ActorLayer);
            output.enabled = true; fps.enabled = false;
            if (phase == Phase.PrimingFps)
            {
                activeSelector.ShowEmote = true; phase = Phase.PrimingBlend;
            }
            else if (phase == Phase.PrimingBlend)
            {
                // Snapshot at native blend weight zero before changing the live FPS target yaw.
                // Cinemachine updates leaf cameras before managers; rotating any earlier would
                // overwrite the original outgoing pose before CameraBlendStack could snapshot it.
                if (!activeSelector.IsBlending ||
                    !ReferenceEquals(activeSelector.LiveChild, activeEmoteEndpoint)) { Cancel(); return; }
                var forward = Vector3.ProjectOnPlane(FacingDirection, Vector3.up).normalized;
                lockedPlayer.RestorePhysicalPose(lockedPlayer.transform.position,
                    Quaternion.LookRotation(forward).eulerAngles.y, 0);
                phase = Phase.Signal;
            }
            else if (phase == Phase.Returning && !activeSelector.IsBlending &&
                ReferenceEquals(activeSelector.LiveChild, activeFpsEndpoint))
                Restore();
        }

        void Restore()
        {
            activeRig?.CancelSignal();
            if (captured)
            {
                if (lockedPlayer != null)
                {
                    lockedPlayer.MovementBlocked = oldMovement; lockedPlayer.SuppressLookInput = oldLook;
                    lockedPlayer.SuppressInteractionInput = oldInteraction; lockedPlayer.EnableJump = oldJump;
                }
                if (activeSelector != null)
                { activeSelector.ShowEmote = oldSelection; activeSelector.PreviousStateIsValid = false; }
                if (activeBrain != null) { activeBrain.ResetState(); activeBrain.enabled = oldBrainEnabled; }
                if (activeFpsEndpoint != null)
                { activeFpsEndpoint.Lens = oldFpsLens; activeFpsEndpoint.BlendHint = oldFpsHint; }
                if (output != null)
                {
                    output.enabled = oldEmoteEnabled; output.cullingMask = oldEmoteMask;
                    output.fieldOfView = oldFov; output.nearClipPlane = oldNear; output.farClipPlane = oldFar;
                    output.orthographicSize = oldOrthoSize; output.orthographic = oldOrthographic;
                }
                if (fps != null) fps.enabled = oldFpsEnabled;
            }
            phase = Phase.Idle; captured = outputVisible = false;
            sampleFrame = cameraFrame = -1; holdTime = attemptTime = 0;
            lockedPlayer = null; activeRig = null; fps = output = null; activeBrain = null;
            activeSelector = null; activeDeoccluder = null; activeFpsEndpoint = activeEmoteEndpoint = null;
            activeActor = activeTarget = null;
        }

        public void Cancel() { Restore(); requestedHeld = true; }
        public void ResetEmote() { Cancel(); paused = false; }
        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            if (value) Cancel();
        }
        void OnDisable() { Cancel(); }
        void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
        void OnApplicationPause(bool value) { if (value) Cancel(); }
    }
}
