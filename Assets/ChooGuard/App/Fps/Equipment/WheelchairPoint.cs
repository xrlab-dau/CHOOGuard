using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A manual wheelchair kept at the station (kind <c>wheelchair</c>). Stage 1 of the OBJECT_CAPABILITIES wheelchair
    /// adapter: the staff member can push it (E) and set it down again (E while looking at it). While pushed it follows the
    /// player's body every frame without being parented to it, so the interaction raycast (which skips the player's own
    /// children) can still aim at it, and it ignores the player's capsule so the CharacterController is not blocked by its
    /// own load.
    /// <para>
    /// Registration: the chair carries a <see cref="StationEquipment"/> record only when it was spawned from a verified
    /// placement (EquipmentSpawner) or a test attaches one. A hand-placed scene chair has no record, because
    /// MeasuredTwinBuildGate rejects any StationEquipment without a field-survey certificate when the scene is processed
    /// (player build and editor Play mode alike), and this chair has none (see
    /// state/evidence/2026-10-23-wheelchair-station-office-2f.json). With a record present its State mirrors 정지 / 이동 중;
    /// without one the chair is an interactable prop only. Occupant, helper and route links are not implemented.
    /// </para>
    /// </summary>
    public sealed class WheelchairPoint : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated
    {
        public const string StateParked = "정지", StateMoving = "이동 중";
        public const string Kind = "wheelchair";
        [Tooltip("Registry id of this chair (unique per placed chair).")]
        public string Id = "wheelchair-01";
        [Tooltip("Korean name the staff sees in the HUD.")]
        public string Label = "휠체어";
        [Tooltip("Zone id of StationPoints the chair stands in (hall2f, ground1f …).")]
        public string Zone = "";
        [Tooltip("Where the chair sits in front of the pushing staff member (local to the player's body).")]
        public Vector3 PushOffset = new Vector3(0f, 0f, .85f);

        public StationEquipment Equipment { get; private set; }
        public FirstPersonResponder Pusher { get; private set; }
        public bool Pushing => Pusher != null;

        private Collider[] colliders;
        private CharacterController ignoredBody;

        private void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            Equipment = GetComponent<StationEquipment>();   // present only for a verified (spawned) placement; null for a hand-placed scene chair
            if (Equipment != null)
            {
                if (string.IsNullOrEmpty(Equipment.Kind)) Equipment.Assign(Id, Kind, Label, Zone, "storage=station_office;width_m=0.65;length_m=0.83;height_m=0.90");
                Equipment.State = StateParked;
            }
        }

        public string DisplayName => Equipment != null && !string.IsNullOrEmpty(Equipment.Label) ? Equipment.Label : string.IsNullOrEmpty(Label) ? "휠체어" : Label;
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
            ignoredBody = responder.GetComponent<CharacterController>();
            if (ignoredBody != null) foreach (var c in colliders) if (c != null) Physics.IgnoreCollision(c, ignoredBody, true);
            Follow();
            if (Equipment != null) Equipment.State = StateMoving;
        }

        /// <summary>The chair is set down where it stands, on the floor below, keeping its heading.</summary>
        public void Release()
        {
            if (!Pushing) return;
            var yaw = transform.eulerAngles.y;
            var at = transform.position;
            var floor = at;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * .5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == ignoredBody || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance < best) { best = hit.distance; floor = hit.point; }
            }
            transform.SetPositionAndRotation(floor, Quaternion.Euler(0f, yaw, 0f));
            if (ignoredBody != null) foreach (var c in colliders) if (c != null) Physics.IgnoreCollision(c, ignoredBody, false);
            ignoredBody = null;
            Pusher = null;
            if (Equipment != null) Equipment.State = StateParked;
        }

        /// <summary>Places the chair at the pushing offset in front of the staff member's body, level with their feet.</summary>
        private void Follow()
        {
            var body = Pusher.transform;
            var yaw = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
            transform.SetPositionAndRotation(body.position + yaw * PushOffset, yaw);
        }

        private void LateUpdate()
        {
            if (!Pushing) return;
            // The staff member paused, ended the shift or was destroyed: the chair stays where it was left, not in limbo.
            if (Pusher == null || !Pusher.isActiveAndEnabled || Pusher.IsPaused) { Release(); return; }
            Follow();
        }

        private void OnDisable()
        {
            if (Pushing) Release();
        }
    }
}
