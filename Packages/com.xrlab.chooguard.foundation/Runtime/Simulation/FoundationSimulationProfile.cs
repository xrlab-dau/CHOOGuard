using System;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;

namespace ChooGuard.Foundation.Simulation
{
    public enum FoundationTrainingMode { Practice, Evaluation }
    [Serializable] public sealed class FoundationIncidentDefinition
    {
        public string ChoiceId, RegionId, PortalId = "", TrainId = "", SourceId;
        public Point3 LocalPosition;
        public double HeatReleaseW = 60000, FuelMassKgPerSecond = .003, SmokeMassKgPerSecond = .0003, RadiationFraction = .3;
    }
    [Serializable] public sealed class FoundationTrainRoute
    {
        public string FrameId, RegionId, PortalId;
        public Point3 DirectionWorld;
        public double StationReferenceM;
        public TrainOperatingDefinition Operation;
        public TrackRouteSegment[] Segments;
    }
    [Serializable] public sealed class FoundationSimulationProfile
    {
        public int SchemaVersion = 1, NpcCount = 100;
        public string ProfileId, Classification = "provisional-synthetic";
        public long Seed = 1;
        public double SmokeMassExtinctionM2PerKg = 8700, ObservationOpticalDepthLimit = 3;
        public FoundationTrainingMode TrainingMode;
        public string[] NpcSpawnRegions;
        public ConnectedThermalProfile Thermal = new ConnectedThermalProfile();
        public IncidentSchedule Schedule;
        public FoundationIncidentDefinition[] Incidents;
        public FoundationTrainRoute[] Trains;
        public void Validate(ConnectedWorldDefinition world)
        {
            world.Validate();
            if (SchemaVersion != 1 || !Id(ProfileId) || Classification != "provisional-synthetic" || Seed <= 0 || NpcCount < 0 || NpcCount > 100 ||
                !Finite(SmokeMassExtinctionM2PerKg) || SmokeMassExtinctionM2PerKg <= 0 || SmokeMassExtinctionM2PerKg > 1000000 ||
                !Finite(ObservationOpticalDepthLimit) || ObservationOpticalDepthLimit <= 0 || ObservationOpticalDepthLimit > 100 ||
                !Enum.IsDefined(typeof(FoundationTrainingMode), TrainingMode) || NpcSpawnRegions == null || NpcSpawnRegions.Length < 1 || NpcSpawnRegions.Length > 13 ||
                NpcSpawnRegions.Distinct().Count() != NpcSpawnRegions.Length || NpcSpawnRegions.Any(id => !world.Regions.Any(r => r.Id == id)) ||
                Incidents == null || Incidents.Length < 1 || Incidents.Length > 128 || Incidents.Any(i => i == null) ||
                Trains == null || Trains.Length != world.Frames.Count(f => f.FrameId != "world") || Trains.Any(t => t == null))
                throw new ArgumentException("Invalid Foundation simulation profile.");
            new IncidentDirector(Schedule, (ulong)Seed);
            if (!Incidents.Select(i => i.ChoiceId).OrderBy(x => x).SequenceEqual(Schedule.Choices.Select(c => c.Id).OrderBy(x => x)))
                throw new ArgumentException("Every authored incident choice needs exactly one world binding.");
            if (Trains.Select(t => t.FrameId).Distinct().Count() != Trains.Length || Trains.Any(t => t.Operation == null) ||
                Trains.Select(t => t.Operation.TrainId).Distinct().Count() != Trains.Length) throw new ArgumentException("Duplicate or missing train identity.");
            foreach (var train in Trains)
            {
                var region = world.Regions.SingleOrDefault(r => r.Id == train.RegionId);
                var portal = world.Portals.SingleOrDefault(p => p.Id == train.PortalId);
                if (region == null || region.FrameId == "world" || region.FrameId != train.FrameId || portal == null || !portal.StaticBoarding ||
                    (portal.From != region.Id && portal.To != region.Id) || !train.DirectionWorld.Finite || train.DirectionWorld.Y != 0 ||
                    Math.Abs(train.DirectionWorld.DistanceSquared(new Point3()) - 1) > 1e-8 || !Finite(train.StationReferenceM) ||
                    train.Operation.ConsistLengthM < region.SizeX || train.Operation.DockToleranceM > .05 || train.Operation.Stops == null || train.Operation.Stops.Length < 2 ||
                    train.Operation.Stops.Any(s => s == null) || !train.Operation.Stops.Any(s => s.Boarding && s.ReferenceM == train.StationReferenceM) || train.Operation.Stops.Any(s => s.Boarding && s.ReferenceM != train.StationReferenceM) ||
                    train.Operation.Stops[0].ReferenceM != train.StationReferenceM || train.Segments == null || train.Segments.Length == 0)
                    throw new ArgumentException("Invalid synthetic straight-route train binding.");
                TrainOperation.Create(train.Operation);
                TrainDynamics.Occupancy(train.Segments, train.StationReferenceM, train.Operation.ConsistLengthM, train.Operation.ClearanceMarginM);
            }
            foreach (var incident in Incidents)
            {
                var choice = Schedule.Choices.Single(c => c.Id == incident.ChoiceId);
                var region = world.Regions.SingleOrDefault(r => r.Id == incident.RegionId);
                if (region == null || !Id(incident.SourceId) || !incident.LocalPosition.Finite || !region.Contains(world.Frame(region.FrameId).ToWorld(incident.LocalPosition)))
                    throw new ArgumentException("Incident source/region position is invalid.");
                if (choice.Kind == FoundationIncidentKind.RouteRestriction && !world.Portals.Any(p => p.Id == incident.PortalId && (p.From == region.Id || p.To == region.Id)) ||
                    (choice.Kind == FoundationIncidentKind.TrainFault || choice.Kind == FoundationIncidentKind.EmergencyStop) && !Trains.Any(t => t.Operation.TrainId == incident.TrainId && t.RegionId == region.Id))
                    throw new ArgumentException("Incident effect is not bound to its region resource.");
                if (choice.Kind == FoundationIncidentKind.Fire && (!Finite(incident.HeatReleaseW) || incident.HeatReleaseW <= 0 || incident.HeatReleaseW > 1000000 ||
                    !Finite(incident.FuelMassKgPerSecond) || incident.FuelMassKgPerSecond <= 0 || !Finite(incident.SmokeMassKgPerSecond) || incident.SmokeMassKgPerSecond < 0 ||
                    incident.SmokeMassKgPerSecond > incident.FuelMassKgPerSecond || !Finite(incident.RadiationFraction) || incident.RadiationFraction < 0 || incident.RadiationFraction > 1))
                    throw new ArgumentException("Invalid authored fire source.");
            }
            ConnectedThermalDomain.Create(world, Thermal);
        }
        private static bool Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128;
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
