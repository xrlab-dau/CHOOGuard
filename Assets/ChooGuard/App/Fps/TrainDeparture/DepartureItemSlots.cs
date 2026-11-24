using System;
using UnityEngine;

namespace ChooGuard.App.Fps.TrainDeparture
{
    /// <summary>Root-driven equipment adapter. One authored model per owned item; no hardware polling.</summary>
    [DisallowMultipleComponent]
    public sealed class DepartureItemSlots : MonoBehaviour, IDepartureSlotItems
    {
        public TrainDepartureGame Game;
        public Transform RadioPocketAnchor, BagPocketAnchor;
        [Min(.1f)] public float MotionSeconds = .3f;
        public int SelectedSlot => selection != null ? selection.SelectedSlot : 3;
        public int RequestedSlot => selection != null ? selection.RequestedSlot : 3;
        public bool IsSwitching => selection != null && selection.IsSwitching;
        public bool BagVisible => initialized && (carried == 3 || moving == 3);
        public bool RadioSelected => initialized && SelectedSlot == 2 && carried == 2 && !IsSwitching;
        public bool KeySelected => SelectedSlot == 1 && !IsSwitching;
        DepartureSlotSelection selection;
        DeparturePlayHud hud;
        bool initialized, drawing;
        int carried = 3, moving;
        float elapsed;
        Vector3 startPosition;
        Quaternion startRotation;
        Renderer[] radioRenderers, bagRenderers;
        Collider[] radioColliders, bagColliders;
        bool[] radioRendererStates, bagRendererStates, radioColliderStates, bagColliderStates;
        TrainDepartureGame subscribed;
        FirstPersonResponder subscribedPlayer;
        DepartureKeyInventory Key => Game != null ? Game.KeyInventory : null;
        DepartureContextActions Actions => Game != null ? Game.ContextActions : null;
        Transform Radio => Actions != null ? Actions.RadioRoot : null;
        Transform Bag => Game != null ? Game.OwnBag : null;
        bool IDepartureSlotItems.CanSwitch => CanRun();
        bool IDepartureSlotItems.SocketLocked => Key != null && Key.IsSocketed;
        bool CanRun() => isActiveAndEnabled && Game != null && Game.isActiveAndEnabled && Game.Started && !Game.Completed &&
            Game.Player != null && Game.Player.isActiveAndEnabled && !Game.Player.IsPaused &&
            (Game.Player.ExternalInputMode || Game.Player.HasFocus) && Time.timeScale > 0 &&
            (Game.View == null || !Game.View.MenuVisible) &&
            (Key == null || !Key.OccupiesHand || SelectedSlot == 1 || IsSwitching && RequestedSlot == 1) && !Game.IsEmoting && (Actions == null || !Actions.IsBusy);
        void Awake() { Bind(); }
        void OnEnable() { Bind(); }
        void Bind()
        {
            if (Game == null) Game = GetComponent<TrainDepartureGame>();
            if (subscribed != Game)
            {
                if (subscribed != null) subscribed.Restarted -= ResetItems;
                subscribed = Game;
                if (subscribed != null) subscribed.Restarted += ResetItems;
            }
            var player = Game != null ? Game.Player : null;
            if (subscribedPlayer == player) return;
            if (subscribedPlayer != null) subscribedPlayer.PauseChanged -= Paused;
            subscribedPlayer = player;
            if (player != null) player.PauseChanged += Paused;
        }
        void Paused(bool paused) { if (paused) CancelTransition(); }
        void OnDisable()
        {
            CancelTransition();
            if (subscribed != null) subscribed.Restarted -= ResetItems;
            if (subscribedPlayer != null) subscribedPlayer.PauseChanged -= Paused;
            subscribed = null; subscribedPlayer = null;
        }
        void OnApplicationPause(bool paused) { if (paused) CancelTransition(); }
        void OnApplicationFocus(bool focused) { if (!focused && (Game == null || Game.Player == null || !Game.Player.ExternalInputMode)) CancelTransition(); }
        bool Prepare()
        {
            Bind();
            if (initialized) return true;
            if (Game == null || Bag == null || Game.BagCarryAnchor == null || Actions == null || Radio == null || Actions.RadioRestAnchor == null) return false;
            radioRenderers = Radio.GetComponentsInChildren<Renderer>(true); bagRenderers = Bag.GetComponentsInChildren<Renderer>(true);
            radioColliders = Radio.GetComponentsInChildren<Collider>(true); bagColliders = Bag.GetComponentsInChildren<Collider>(true);
            radioRendererStates = Capture(radioRenderers); bagRendererStates = Capture(bagRenderers);
            radioColliderStates = Capture(radioColliders); bagColliderStates = Capture(bagColliders);
            selection = new DepartureSlotSelection(this); hud=Game.GetComponent<DeparturePlayHud>(); initialized = true;
            SetRadioVisible(false); SetBagVisible(Game.HasOwnBag); carried = Game.HasOwnBag ? 3 : 4;
            PlacePocket(2); Publish(); return true;
        }
        static bool[] Capture(Renderer[] values) { var result = new bool[values.Length]; for (int i=0;i<values.Length;i++) result[i]=values[i].enabled; return result; }
        static bool[] Capture(Collider[] values) { var result = new bool[values.Length]; for (int i=0;i<values.Length;i++) result[i]=values[i].enabled; return result; }
        static void Visible(Renderer[] values, bool[] states, bool visible) { for(int i=0;i<values.Length;i++) if(values[i]!=null) values[i].enabled=visible&&states[i]; }
        static void Visible(Collider[] values, bool[] states, bool visible) { for(int i=0;i<values.Length;i++) if(values[i]!=null) values[i].enabled=visible&&states[i]; }
        void SetRadioVisible(bool visible) { Visible(radioRenderers,radioRendererStates,visible); Visible(radioColliders,radioColliderStates,visible); }
        void SetBagVisible(bool visible) { Visible(bagRenderers,bagRendererStates,visible); Visible(bagColliders,bagColliderStates,visible); }
        public bool IsAvailable(int slot)
        {
            if (slot == 4) return true;
            if (Game == null) return false;
            if (slot == 1) return Key != null && Key.ValidateBinding(out _);
            if (slot == 3) return Game.HasOwnBag && Bag != null && Game.BagCarryAnchor != null;
            var player = Game.Player;
            return slot == 2 && Radio != null && player != null && Actions.RadioRestAnchor != null &&
                Radio.IsChildOf(player.transform) && !Radio.IsChildOf(player.PlayerCamera.transform) &&
                Actions.RadioRestAnchor.IsChildOf(player.transform) && !Actions.RadioRestAnchor.IsChildOf(player.PlayerCamera.transform);
        }
        bool IDepartureSlotItems.Available(int slot) => IsAvailable(slot);
        bool IDepartureSlotItems.Stowed(int slot) => slot == 4 || (slot == 1 ? Key == null || !Key.IsDrawn && !Key.IsInHand : carried != slot && moving != slot);
        bool IDepartureSlotItems.Drawn(int slot) => slot == 1 ? Key != null && Key.IsReadyInHand : carried == slot && moving == 0;
        bool IDepartureSlotItems.StartStow(int slot)
        {
            if (slot == 1) return Key != null && Key.TryStow();
            if (slot == 4) return true;
            return BeginMotion(slot,false);
        }
        bool IDepartureSlotItems.StartDraw(int slot)
        {
            if (slot == 1) return Key != null && Key.TryDraw();
            return BeginMotion(slot,true);
        }
        bool BeginMotion(int slot, bool draw)
        {
            var model = slot == 2 ? Radio : Bag;
            if (model == null || moving != 0 || !IsAvailable(slot) || !Finite(MotionSeconds) || MotionSeconds < .1f) return false;
            if (draw) { model.gameObject.SetActive(true); if (slot == 2) SetRadioVisible(true); else SetBagVisible(true); }
            moving = slot; drawing = draw; elapsed = 0; startPosition = model.position; startRotation = model.rotation;
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void Target(int slot, bool draw, out Vector3 position, out Quaternion rotation)
        {
            var anchor = slot == 2 ? draw ? Actions.RadioRestAnchor : RadioPocketAnchor : draw ? Game.BagCarryAnchor : BagPocketAnchor;
            if (anchor != null) { position = anchor.position; rotation = anchor.rotation; return; }
            // Storage fallback is body-relative; never follows the camera pitch while awaiting F.
            position = Game.Player.transform.TransformPoint(slot == 2 ? new Vector3(.22f,.45f,.1f) : new Vector3(-.25f,.35f,.05f));
            rotation = Game.Player.transform.rotation;
        }
        void PlacePocket(int slot)
        {
            var model = slot == 2 ? Radio : Bag;
            if(model == null) return;
            Target(slot,false,out var p,out var q); model.SetPositionAndRotation(p,q);
        }
        public void Initialize() { Prepare(); }
        public bool RequestSlot(int slot)
        {
            if (!Prepare()) return false;
            ObserveReturnedKey();
            if (!CanRun()) return false;
            bool accepted = selection.RequestSlot(slot); Publish(); return accepted;
        }
        public void Step(float dt)
        {
            if (!Prepare()) return;
            ObserveReturnedKey();
            if (!CanRun()) { CancelTransition(); selection.Step(); Publish(); return; }
            if (!Finite(dt) || dt <= 0 || dt > .25f) { CancelTransition(); return; }
            if (moving != 0)
            {
                var model = moving == 2 ? Radio : Bag;
                Target(moving,drawing,out var position,out var rotation);
                elapsed = Mathf.Min(MotionSeconds,elapsed+dt);
                // Actual installed Unity 6000.3.23f1 interpolation modules, same sampled motion as key inventory.
                float t = Mathf.SmoothStep(0,1,elapsed/MotionSeconds);
                model.SetPositionAndRotation(Vector3.Lerp(startPosition,position,t),Quaternion.Slerp(startRotation,rotation,t));
                if (elapsed >= MotionSeconds)
                {
                    if (drawing) carried = moving;
                    else { if(moving==2)SetRadioVisible(false);else SetBagVisible(false); carried=4; }
                    moving=0;
                }
            }
            selection.Step(); Publish();
        }
        void IDepartureSlotItems.CancelEquip()
        {
            Key?.CancelEquipMotion();
            if (moving != 0)
            {
                PlacePocket(moving); if(moving==2)SetRadioVisible(false);else SetBagVisible(false);
            }
            moving=0; carried=4;
        }
        public void CancelTransition() { if(initialized) { selection.Cancel(); Publish(); } }
        public void ResetItems()
        {
            if (!Prepare()) return;
            moving=0; carried=3; selection.Reset(); SetRadioVisible(false); PlacePocket(2); SetBagVisible(true); Publish();
        }
        void ObserveReturnedKey()
        {
            // After a depth-zero cancelled insertion, hand ownership remains real.
            // Authorized withdrawal at the radio stage keeps its existing return/stow recovery.
            if (Game.Flow.Stage != DepartureStage.SendDepartureRadio) selection.ObserveReturnedKey();
        }
        void Publish() { if(hud!=null) hud.Slots=this; }
    }
}
