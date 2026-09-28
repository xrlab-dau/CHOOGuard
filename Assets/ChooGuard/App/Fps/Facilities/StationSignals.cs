using System;
using UnityEngine;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>
    /// Station-wide signals the twin's facilities follow: the fire alarm (automatic fire detection or a pressed call point
    /// opens the automatic doors on the escape routes and the ticket gates) and the recorded door sounds. The main-game
    /// shift sets them; without a shift (tutorial, editor) they stay quiet.
    /// </summary>
    public static class StationSignals
    {
        /// <summary>The fire receiver has a fire signal: the station bell rings and interlocked doors and gates open.</summary>
        public static bool FireAlarm { get; set; }

        public static AudioClip DoorOpen, DoorClose;

        /// <summary>A manual call point (발신기) was pressed at the given place; the shift's fire receiver answers.</summary>
        public static event Action<Vector3, string> CallPointPressed;

        public static void PressCallPoint(Vector3 at, string label)
        {
            FireAlarm = true;
            CallPointPressed?.Invoke(at, label);
        }

        public static void Clear()
        {
            FireAlarm = false;
            DoorOpen = DoorClose = null;
        }
    }
}
