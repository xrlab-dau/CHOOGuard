using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A manual wheelchair kept at the station (kind <c>wheelchair</c>, registered through <see cref="StationEquipment"/> on
    /// the same root). Stage 1 of the OBJECT_CAPABILITIES wheelchair adapter: the staff member can push it (E) and set it
    /// down again (E while looking at it). It follows the player's body while pushed and ignores the player's capsule so
    /// the CharacterController is not blocked by its own load. Occupant, helper and route links are not implemented here:
    /// <see cref="StationEquipment.State"/> only says 정지 / 이동 중, which JevWorldInventory reports per kind.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class WheelchairPoint : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated
    {
        public const string StateParked = "정지", StateMoving = "이동 중";
        [Tooltip("Where the chair sits in front of the pushing staff member (local to the player's body).")]
        public Vector3 PushOffset = new Vector3(0f, 0f, .85f);

        public StationEquipment Equipment { get; private set; }
        public FirstPersonResponder Pusher { get; private set; }
        public bool Pushing => Pusher != null;

        private Collider[] colliders;
        private CharacterController ignoredBody;
        private Transform originalParent;

        private void Awake()
        {
            Equipment = GetComponent<StationEquipment>();
            colliders = GetComponentsInChildren<Collider>(true);
            if (string.IsNullOrEmpty(Equipment.State) || Equipment.State == "정상") Equipment.State = StateParked;
        }

        public string DisplayName => string.IsNullOrEmpty(Equipment != null ? Equipment.Label : null) ? "휠체어" : Equipment.Label;
        public string StateText => Pushing ? StateMoving : StateParked;
        public string InteractionPrompt => Pushing ? "휠체어 놓기" : "휠체어 밀기";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (responder == null || responder.IsPaused) { reason = ""; return false; }
            if (Pushing && Pusher != responder) { reason = "다른 사람이 밀고 있습니다"; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            if (Pushing) { Release(); feedback = "휠체어를 놓았습니다"; }
            else { Push(responder); feedback = "휠체어를 밀고 있습니다 · 다시 E 로 놓기"; }
            return true;
        }

        /// <summary>The chair goes in front of <paramref name="responder"/> and moves with them.</summary>
        public void Push(FirstPersonResponder responder)
        {
            if (responder == null || Pushing) return;
            Pusher = responder;
            originalParent = transform.parent;
            ignoredBody = responder.GetComponent<CharacterController>();
            if (ignoredBody != null) foreach (var c in colliders) if (c != null) Physics.IgnoreCollision(c, ignoredBody, true);
            transform.SetParent(responder.transform, true);
            transform.localPosition = PushOffset;
            transform.localRotation = Quaternion.identity;
            Equipment.State = StateMoving;
        }

        /// <summary>The chair is set down where it stands, on the floor below, keeping its heading.</summary>
        public void Release()
        {
            if (!Pushing) return;
            var yaw = transform.eulerAngles.y;
            transform.SetParent(originalParent, true);
            var at = transform.position;
            var floor = at;
            if (Physics.Raycast(at + Vector3.up * .5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) && (ignoredBody == null || hit.collider != ignoredBody))
                floor = hit.point;
            transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
            if (ignoredBody != null) foreach (var c in colliders) if (c != null) Physics.IgnoreCollision(c, ignoredBody, false);
            ignoredBody = null;
            Pusher = null;
            Equipment.State = StateParked;
        }

        private void LateUpdate()
        {
            // The staff member paused, ended the shift or was destroyed: the chair stays where it was left, not in limbo.
            if (Pushing && (Pusher == null || !Pusher.isActiveAndEnabled || Pusher.IsPaused)) Release();
        }

        private void OnDisable()
        {
            if (Pushing) Release();
        }
    }
}
