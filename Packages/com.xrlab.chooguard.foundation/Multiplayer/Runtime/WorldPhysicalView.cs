using System;
using ChooGuard.Foundation.Simulation;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable] public sealed class ObservedBody
    { public string ParticipantId, TeamId, RoleId, RegionId, FrameId; public Point3 Position, LocalPosition; }
    [Serializable] public sealed class ObservedTrain
    { public string EntityId, FrameId; public double SignedSpeedMS; public bool DoorOpen; }
    [Serializable] public sealed class ObservedRegionEffect
    { public string RegionId; public bool LightingOn, PublicAddressAvailable; }
    [Serializable] public sealed class ObservedSmokeCell
    { public string RegionId; public SmokeCell Cell; public double UpperTemperatureK, LowerTemperatureK; }
    [Serializable] public sealed class WorldPhysicalView
    {
        public SpatialFrame[] Frames = Array.Empty<SpatialFrame>();
        public ObservedBody[] Bodies = Array.Empty<ObservedBody>();
        public ObservedTrain[] Trains = Array.Empty<ObservedTrain>();
        public ObservedRegionEffect[] Regions = Array.Empty<ObservedRegionEffect>();
        public ObservedSmokeCell[] Smoke = Array.Empty<ObservedSmokeCell>();
    }
}
