using System;
using UnityEngine;

namespace ChooGuard.Seeded.Unity
{
    /// <summary>Physical interaction context shared by station play and offline equipment lessons.</summary>
    public interface IDetailedManipulationHost
    {
        World State { get; }
        Camera View { get; }
        Transform Actor { get; }
        Transform Root { get; }
        bool Running { get; }
        bool Captured { get; }
        bool IsReplaying { get; }
        StationWorldAdapter WorldSnapshot { get; }
        string ResolveResponseSubject(string id);
    }

    /// <summary>Device-independent input; targets and contact are always measured by the controller.</summary>
    public struct DetailedInputFrame
    {
        public bool GripPressed, GripReleased, CarryPressed, PlacePressed, CycleToolPressed;
        public bool ConnectPressed, DisconnectPressed, CheckHeld, TagHeld, ReverseHeld;
        public float PointerDeltaX, PointerDeltaY;
        // Wheel detents, independent of the platform's native event range.
        public float ScrollDelta;
    }

    /// <summary>Explicit poses for a real authored component; no primitive offsets are inferred.</summary>
    [Serializable]
    public sealed class DetailedPartPoseBinding
    {
        public string PartId;
        public Transform Part;
        public Vector3 MinLocalPosition, MaxLocalPosition;
        public Quaternion MinLocalRotation = Quaternion.identity, MaxLocalRotation = Quaternion.identity;
        public bool DrivePosition = true, DriveRotation = true;
        public Transform AimOrigin;
        public bool AimWithView;
        public bool ClickLatched;
        [Range(0, 1)] public float AimAfterNormalized = .95f;
    }
}
