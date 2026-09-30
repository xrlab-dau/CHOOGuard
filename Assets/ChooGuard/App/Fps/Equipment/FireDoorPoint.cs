using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A double-leaf fire door (방화문, a <see cref="StationEquipment"/> of kind <see cref="DoorKind"/>) beside a fire shutter. Building Act enforcement rules art. 14 (2) 4 가
    /// wants a separate escape fire door within 3 m of every automatic fire shutter (research nftc103-compartments-cctv 2.2), so the builder gives each shutter one at the
    /// end of its opening. The door stands closed on its closer (the 60-minute door of art. 14 (2) 1 is kept shut); a person coming up to it, from either side, pushes the leaves
    /// open and the closer swings them shut again a moment after the last person has gone through. The leaves swing away from the push bar (the model's +Z face), towards −Z,
    /// on the hinges of the model. No collider and no nav-mesh block: people walk through it as through any door, which is what a fire door beside a lowered shutter is for.
    /// The transom above the frame (<see cref="FireDoorTransom"/>: steel frame with plaster infill up to the ceiling) is built for the ceiling height of the placement entry (<c>ceiling</c> in metres above the floor).
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class FireDoorPoint : MonoBehaviour, IEquipmentPlaced
    {
        public const string DoorKind = "fire_door";

        /// <summary>Widest opening of a leaf, degrees (a push bar door stops on its closer arm).</summary>
        public const float MaxAngle = 95f;
        /// <summary>A person pushing swings a leaf at this rate (degrees per second); the hydraulic closer brings it back at the slower rate (about 3 s from fully open, EN 1154 closing time range).</summary>
        public const float OpenSpeed = 160f, CloseSpeed = 32f;
        /// <summary>How far in front of and behind the door plane a person opens it, metres, and how long the closer waits after the last person left.</summary>
        public const float Reach = 2.2f, Hold = 1.2f;
        /// <summary>Height of the frame model; the transom above it reaches to <c>ceiling</c>.</summary>
        public const float FrameHeight = 2.1f;
        /// <summary>The frame model's outer width and depth, and where its leaves hinge (from the middle of the frame, back face) - the sidecar JSON of FireDoorFrame.</summary>
        public const float FrameWidth = 2.1986f, FrameDepth = .131f, HingeX = 1.0691f, HingeZ = .0655f;

        public StationEquipment Equipment { get; private set; }
        /// <summary>Angle of the leaves now, 0 closed.</summary>
        public float Angle { get; private set; }
        public bool Closed => Angle < .5f;

        private Transform left, right;
        private Mesh transom;
        private float target, nextSense, closeAt;
        private readonly Collider[] hits = new Collider[24];

        public void OnPlaced()
        {
            Equipment = GetComponent<StationEquipment>();
            left = transform.Find("LeafLeft");
            right = transform.Find("LeafRight");
            var filler = transform.Find("Transom");
            transom = FireDoorTransom.Build(Equipment.Number("ceiling", FrameHeight));
            if (filler != null)
            {
                filler.GetComponent<MeshFilter>().sharedMesh = transom;
                filler.gameObject.SetActive(transom != null);
            }
            Apply();
        }

        private void OnDestroy()
        {
            if (transom != null) Destroy(transom);
        }

        private void Update()
        {
            if (Time.time >= nextSense)
            {
                nextSense = Time.time + .12f;
                if (SomeoneNear()) { target = MaxAngle; closeAt = Time.time + Hold; }
                else if (Time.time >= closeAt) target = 0f;
            }
            if (Mathf.Approximately(Angle, target)) return;
            Angle = Mathf.MoveTowards(Angle, target, (target > Angle ? OpenSpeed : CloseSpeed) * Time.deltaTime);
            Apply();
        }

        /// <summary>A person of the crowd, an agency responder or the staff member within reach of the door plane.</summary>
        private bool SomeoneNear()
        {
            int count = Physics.OverlapBoxNonAlloc(transform.position + transform.up, new Vector3(1.1f, 1f, Reach), hits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].GetComponentInParent<Emergency.Passenger>() != null || hits[i].GetComponentInParent<Emergency.Responder>() != null || hits[i].GetComponentInParent<FirstPersonResponder>() != null) return true;
            return false;
        }

        private void Apply()
        {
            if (left != null) left.localRotation = Quaternion.Euler(0f, Angle, 0f);
            if (right != null) right.localRotation = Quaternion.Euler(0f, -Angle, 0f);
            Equipment.State = Closed ? "닫힘" : Mathf.Approximately(Angle, target) ? "열림" : target > Angle ? "열리는 중" : "닫히는 중";
        }
    }
}
