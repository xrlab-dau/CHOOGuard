using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// Hand-motion maths shared by the devices that are worked while E is held (<see cref="IFpsHoldInteraction"/>): how far a mouse
    /// drag moves a point along the path a hinged leaf carries it on, and how many degrees a circling mouse turns a handwheel.
    /// </summary>
    public static class HandGesture
    {
        /// <summary>
        /// How far the mouse drag <paramref name="mouse"/> (look degrees, x right, y up) carries a point that moves along the world direction
        /// <paramref name="direction"/>: sideways motion follows the mouse's x, motion away from the player (or up) follows the mouse pushed
        /// forward (y up), motion toward the player the mouse pulled back. Positive when the hand drags the way the point goes.
        /// </summary>
        public static float Along(Transform view, Vector3 direction, Vector2 mouse)
        {
            var local = view.InverseTransformDirection(direction);
            float length = local.magnitude;
            if (length < 1e-4f) return 0f;
            local /= length;
            return mouse.x * local.x + mouse.y * (local.y + local.z);
        }

        /// <summary>
        /// World direction in which the free part of a leaf hinged on <paramref name="parent"/>'s up axis moves as it opens. <paramref name="offset"/> is the
        /// vector from the hinge to that part in the parent's space, <paramref name="sign"/> the sign of the leaf's opening angle (Euler(0, θ, 0)).
        /// </summary>
        public static Vector3 SwingDirection(Transform parent, Vector3 offset, float sign)
        {
            var tangent = Vector3.Cross(Vector3.up, offset) * sign;
            return parent != null ? parent.TransformDirection(tangent) : tangent;
        }

        /// <summary>
        /// Turns circling mouse motion into degrees of handwheel turn: the signed angle between the directions of successive hand strokes
        /// (counter-clockwise on the screen is positive). A stroke is the mouse travel gathered until it is long enough to have a direction,
        /// so the result does not depend on the frame rate; a reversal (a straight back-and-forth) turns nothing.
        /// </summary>
        public struct Circle
        {
            private const float MinStrokeDegrees = .5f, MaxTurnDegrees = 100f;
            private Vector2 stroke, last;
            private bool hasLast;

            public void Reset()
            {
                stroke = Vector2.zero;
                last = Vector2.zero;
                hasLast = false;
            }

            /// <summary>Adds this step's mouse movement; returns the degrees the hand has circled since the last stroke ended (0 mid-stroke).</summary>
            public float Add(Vector2 mouse)
            {
                stroke += mouse;
                if (stroke.sqrMagnitude < MinStrokeDegrees * MinStrokeDegrees) return 0f;
                var direction = stroke.normalized;
                stroke = Vector2.zero;
                if (!hasLast) { last = direction; hasLast = true; return 0f; }
                float turn = Vector2.SignedAngle(last, direction);
                last = direction;
                return Mathf.Abs(turn) > MaxTurnDegrees ? 0f : turn;
            }
        }
    }
}
