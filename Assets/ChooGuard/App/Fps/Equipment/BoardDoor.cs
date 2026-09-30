using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The hinged door of a distribution board. It sits on a pivot at the hinge and swings between shut (0°) and open
    /// (<see cref="OpenDegrees"/> about the pivot's up axis, outward) in <see cref="SwingSeconds"/>. Staff open it with the
    /// board key; the shift closes it again when the board is left.
    /// </summary>
    public sealed class BoardDoor : MonoBehaviour
    {
        [Tooltip("Degrees about the hinge's up axis when fully open; the sign is the outward direction.")]
        public float OpenDegrees = 110f;

        public const float SwingSeconds = .6f;

        public bool IsOpen { get; private set; }

        private float angle;

        private void Awake() => enabled = false;

        /// <summary>Starts swinging the door open or shut.</summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            enabled = true;
        }

        private void Update()
        {
            float target = IsOpen ? OpenDegrees : 0f;
            angle = Mathf.MoveTowards(angle, target, Mathf.Abs(OpenDegrees) / SwingSeconds * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (Mathf.Approximately(angle, target)) enabled = false;
        }
    }
}
