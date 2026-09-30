using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// Where the eight breakers of a distribution board's dead-front sit, in the frame of the board prefab's body (origin at
    /// the back-plane bottom centre, +Z out of the wall) before the board is hung <see cref="BoardBottom"/> above the floor.
    /// The BreakerDeck model (make_breakers.py) is built on the same grid; its JSON lists the slot centres and the prefab
    /// builder checks them against these numbers. Slot index = row * 2 + column, row 0 on top, column 0 on the viewer's left
    /// facing the panel; slot 0 is the main breaker.
    /// </summary>
    public static class BreakerDeckLayout
    {
        public const int Slots = 8;
        /// <summary>Bottom of a small distribution board above the floor (m): operable without tools or sitting down (building-electrical design guidance).</summary>
        public const float BoardBottom = 1.0f;
        public const float PlateFront = .240f, HingeZ = .283f, LeverDegrees = 25f;

        private static readonly float[] ColumnX = { .09f, -.09f };
        private static readonly float[] RowY = { .665f, .545f, .425f, .305f };

        /// <summary>The hinge of the lever of a slot (a lever is modelled with its origin on the hinge, neutral pose pointing out of the panel).</summary>
        public static Vector3 Hinge(int slot) => new Vector3(ColumnX[slot % 2], RowY[slot / 2], HingeZ);

        /// <summary>Box of the breaker body a staff member aims at.</summary>
        public static readonly Vector3 BodySize = new Vector3(.06f, .10f, .07f);
    }
}
