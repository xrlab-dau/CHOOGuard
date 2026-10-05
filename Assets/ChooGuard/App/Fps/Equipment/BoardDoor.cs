using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The hinged door of a distribution board. It sits on a pivot at the hinge and swings between shut (0°) and open
    /// (<see cref="OpenDegrees"/> about the pivot's up axis, outward). It opens by itself in <see cref="SwingSeconds"/>
    /// (<see cref="SetOpen"/>) or follows the hand that swings it (<see cref="SetOpening"/>); it counts as open from
    /// <see cref="OpenThreshold"/> of the swing, which is when the breakers behind it can be reached. Staff open it with the
    /// board key; the shift closes it again when the board is left.
    /// </summary>
    public sealed class BoardDoor : MonoBehaviour
    {
        [Tooltip("Degrees about the hinge's up axis when fully open; the sign is the outward direction.")]
        public float OpenDegrees = 110f;

        public const float SwingSeconds = .6f;
        /// <summary>The door is open (the breakers can be reached) from this share of its swing.</summary>
        public const float OpenThreshold = .6f;
        /// <summary>The door is shut up to this share of its swing.</summary>
        public const float ShutBelow = .02f;

        /// <summary>How far the door has swung (0 shut … 1 fully open).</summary>
        public float Opening { get; private set; }
        public bool IsOpen => Opening >= OpenThreshold;
        public bool IsShut => Opening <= ShutBelow;

        private float goal;

        private void Awake() => enabled = false;

        /// <summary>Starts swinging the door open or shut.</summary>
        public void SetOpen(bool open)
        {
            goal = open ? 1f : 0f;
            enabled = true;
        }

        /// <summary>Puts the door at <paramref name="opening"/> (0..1) at once and leaves it there (the hand that swings it).</summary>
        public void SetOpening(float opening)
        {
            Opening = Mathf.Clamp01(opening);
            goal = Opening;
            Apply();
        }

        private void Apply() => transform.localRotation = Quaternion.Euler(0f, OpenDegrees * Opening, 0f);

        private void Update()
        {
            Opening = Mathf.MoveTowards(Opening, goal, Time.deltaTime / SwingSeconds);
            Apply();
            if (Mathf.Approximately(Opening, goal)) enabled = false;
        }
    }
}
