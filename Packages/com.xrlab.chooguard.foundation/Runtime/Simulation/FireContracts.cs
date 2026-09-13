using System;
using System.Linq;

namespace ChooGuard.Foundation.Simulation
{
    [Serializable] public sealed class FireWallOpening
    {
        public string Id = "";
        public double WidthM, BottomM, HeightM;
        public FireWallOpening Copy() => (FireWallOpening)MemberwiseClone();
    }
    // SI units throughout. This reduced model is not a certified life-safety calculation.
    [Serializable]
    public sealed class FireCellDefinition
    {
        public string Id;
        public double WidthM, DepthM, HeightM, FloorElevationM;
        // A linear ramp has this floor-elevation range and constant vertical clearance HeightM-FloorRiseM.
        public double FloorRiseM;
        public double InitialUpperVolumeFraction = .5;
        public double WallHeatCapacityJPerK, InsideHeatTransferWPerM2K, OutsideHeatTransferWPerM2K;
        public double WallThicknessM = .013, WallConductivityWPerMK = 1;
        // Virtual subdivision faces exchange gas, not heat with invented solid partitions.
        public bool UseExplicitWallAreas;
        public double FloorWallAreaM2, CeilingWallAreaM2, VerticalWallLengthM;
        public double VerticalWallHeightM;
        public FireWallOpening[] WallOpenings = Array.Empty<FireWallOpening>();
        public double VolumeM3 => WidthM * DepthM * (HeightM - FloorRiseM);
        public double SurfaceAreaM2 => UseExplicitWallAreas ? FloorWallAreaM2 + CeilingWallAreaM2 + WallAreaBetween(0, HeightM) :
            2 * WidthM * DepthM + 2 * (WidthM + DepthM) * HeightM;
        public double WallAreaBetween(double bottom, double top)
        {
            var wallTop = VerticalWallHeightM > 0 ? VerticalWallHeightM : HeightM - FloorRiseM;
            var area = VerticalWallLengthM * Math.Max(0, FilledHeight(top, wallTop) - FilledHeight(bottom, wallTop));
            foreach (var opening in WallOpenings ?? Array.Empty<FireWallOpening>())
                area -= opening.WidthM * Math.Max(0, Math.Min(Math.Min(top, wallTop), opening.BottomM + opening.HeightM) - Math.Max(bottom, opening.BottomM));
            return Math.Max(0, area); // Only roundoff can be negative after validated aperture widths.
        }
        public double FilledHeight(double height, double clearance)
        {
            if (FloorRiseM == 0) return Math.Max(0, Math.Min(clearance, height));
            var a = Math.Max(0, Math.Min(FloorRiseM, height - clearance)); var b = Math.Max(0, Math.Min(FloorRiseM, height));
            return clearance * (a / FloorRiseM) + ((b - a) / FloorRiseM) * (height - (a + b) / 2);
        }
        public double HeightForLowerVolume(double lowerVolume, double upperVolume)
        {
            if (double.IsNaN(lowerVolume) || double.IsNaN(upperVolume) || lowerVolume < 0 || upperVolume < 0 ||
                Math.Abs(lowerVolume + upperVolume - VolumeM3) > 1e-8 * Math.Max(1, VolumeM3))
                throw new ArgumentException("Layer volume is outside its sloped compartment.");
            var area = WidthM * DepthM; var q = lowerVolume / area; var u = upperVolume / area;
            if (FloorRiseM == 0) return q;
            if (q < FloorRiseM / 2) return Math.Sqrt(2 * FloorRiseM * q);
            if (u < FloorRiseM / 2) return HeightM - Math.Sqrt(2 * FloorRiseM * u);
            return q + FloorRiseM / 2;
        }
        public double LayerSurfaceArea(double interfaceHeight, bool upper)
        {
            var floorArea = UseExplicitWallAreas ? FloorWallAreaM2 : WidthM * DepthM;
            var ceilingArea = UseExplicitWallAreas ? CeilingWallAreaM2 : WidthM * DepthM;
            var floorBelow = FloorRiseM == 0 ? 1 : Math.Max(0, Math.Min(1, interfaceHeight / FloorRiseM));
            var ceilingAbove = FloorRiseM == 0 ? 1 : Math.Max(0, Math.Min(1, (HeightM - interfaceHeight) / FloorRiseM));
            var walls = UseExplicitWallAreas ? WallAreaBetween(upper ? interfaceHeight : 0, upper ? HeightM : interfaceHeight) :
                2 * (WidthM + DepthM) * (upper ? HeightM - interfaceHeight : interfaceHeight);
            return walls + floorArea * (upper ? 1 - floorBelow : floorBelow) + ceilingArea * (upper ? ceilingAbove : 1 - ceilingAbove);
        }
        public FireCellDefinition Copy()
        { var copy = (FireCellDefinition)MemberwiseClone(); copy.WallOpenings = WallOpenings?.Select(o => o?.Copy()).ToArray(); return copy; }
    }

    [Serializable]
    public sealed class FireDoorDefinition
    {
        public string Id, FromCellId, ToCellId = ""; // Empty endpoint is the ambient reservoir.
        public double WidthM, HeightM, BottomElevationM, DischargeCoefficient = .7;
        public double OpeningFraction = 1;
        public FireDoorDefinition Copy() => (FireDoorDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class FireFanDefinition
    {
        public string Id, FromCellId = "", ToCellId = "";
        public bool FromUpper, ToUpper, Enabled = true;
        public double MassFlowKgPerSecond;
        public FireFanDefinition Copy() => (FireFanDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class FireNetworkDefinition
    {
        public int SchemaVersion = 1;
        public string ModelVersion = "conservative-two-zone-1";
        public string PressureConvention = "uniform-thermodynamic-reference-with-ambient-hydrostatic-offset";
        public double AmbientTemperatureK = 293.15, AmbientPressurePa = 101325;
        public double SpecificHeatCp = 1012, Gamma = 1.4, GravityMPerS2 = 9.81;
        public FireCellDefinition[] Cells = Array.Empty<FireCellDefinition>();
        public FireDoorDefinition[] Doors = Array.Empty<FireDoorDefinition>();
        public FireFanDefinition[] Fans = Array.Empty<FireFanDefinition>();
        public FireNetworkDefinition Copy() => new FireNetworkDefinition { SchemaVersion = SchemaVersion,
            ModelVersion = ModelVersion, PressureConvention = PressureConvention, AmbientTemperatureK = AmbientTemperatureK, AmbientPressurePa = AmbientPressurePa,
            SpecificHeatCp = SpecificHeatCp, Gamma = Gamma, GravityMPerS2 = GravityMPerS2,
            Cells = Cells.Select(c => c.Copy()).ToArray(), Doors = Doors.Select(d => d.Copy()).ToArray(),
            Fans = Fans.Select(f => f.Copy()).ToArray() };
    }

    [Serializable]
    public sealed class FireCellState
    {
        public string CellId;
        public double UpperMassKg, LowerMassKg, UpperEnergyJ, LowerEnergyJ, UpperSmokeKg, LowerSmokeKg, WallEnergyJ;
        public FireCellState Copy() => (FireCellState)MemberwiseClone();
    }

    [Serializable]
    public sealed class FireState
    {
        public int SchemaVersion = 1;
        public string ModelVersion = "conservative-two-zone-1";
        public double SimulatedSeconds, ExternalMassKg, ExternalEnergyJ, ExternalSmokeKg;
        public double ReleasedHeatJ, ExternalEnthalpyJ, RadiationEscapedJ, WallLossJ;
        public long AcceptedSubsteps, ConservativeRepartitions;
        public FireCellState[] Cells = Array.Empty<FireCellState>();
        public FireState Copy() => new FireState { SchemaVersion = SchemaVersion, ModelVersion = ModelVersion,
            SimulatedSeconds = SimulatedSeconds, ExternalMassKg = ExternalMassKg, ExternalEnergyJ = ExternalEnergyJ,
            ExternalSmokeKg = ExternalSmokeKg, AcceptedSubsteps = AcceptedSubsteps, ConservativeRepartitions = ConservativeRepartitions,
            ReleasedHeatJ = ReleasedHeatJ, ExternalEnthalpyJ = ExternalEnthalpyJ, RadiationEscapedJ = RadiationEscapedJ, WallLossJ = WallLossJ,
            Cells = Cells.Select(c => c.Copy()).ToArray() };
    }

    [Serializable]
    public sealed class FireSourcePower
    {
        public string CellId;
        public double HeatReleaseW, FuelMassKgPerSecond, SmokeMassKgPerSecond, RadiationFraction;
        public double HeightM, PlumeAreaM2 = .09, PlumeMultiplier = 1;
        public bool EnablePlume = true;
    }
    [Serializable] public sealed class FireDoorSetting { public string DoorId; public double OpeningFraction; }
    [Serializable] public sealed class FireFanSetting { public string FanId; public bool Enabled; }
    [Serializable]
    public sealed class FireForcing
    {
        public FireSourcePower[] Sources = Array.Empty<FireSourcePower>();
        public FireDoorSetting[] Doors = Array.Empty<FireDoorSetting>();
        public FireFanSetting[] Fans = Array.Empty<FireFanSetting>();
    }

    public sealed class FireSolverOptions
    {
        public bool PreferForestPressureSolver = true;
        public bool CacheOpeningSlabs = true;
        public double MaxStepSeconds = .05, MinStepSeconds = 1e-7, PressureTolerancePa = 1e-7;
        public double CommitEnergyToleranceJ = .001;
        public double PressureRegularizationPa = .0001;
        public double MaxDonorFraction = .4, MaxRelativeTemperatureChange = .15, MaxLayerVolumeFractionChange = .05;
        public double MinLayerVolumeFraction = 1e-7;
        public int MaxSubsteps = 4096, MaxNewtonIterations = 32, MaxBacktracks = 20;
        public FireSolverOptions Copy() => (FireSolverOptions)MemberwiseClone();
    }

    public sealed class FireStepReport
    {
        public string Failure = "";
        public string LastRejectedReason = "";
        public int AcceptedSubsteps, RejectedSubsteps, PressureIterations;
        public int ForestPressureSolves, DensePressureSolves;
        public long RateEvaluationTicks, LinearSolveTicks;
        public int CachedOpeningSlabs;
        public double MaximumPressureResidualPa, MaximumPressureEnergyResidualJ;
        public double MaximumCommittedPressureResidualPa, MaximumCommittedPressureEnergyResidualJ;
        public double LastPressureResidualPa, LastAttemptSeconds, ProgressSeconds;
    }

    public readonly struct FireCellMetrics
    {
        // PressurePa is the thermodynamic reference pressure; opening calculations also apply
        // the ambient floor-elevation offset and the individual layer hydrostatic columns.
        public readonly double PressurePa, UpperTemperatureK, LowerTemperatureK, InterfaceHeightM,
            UpperVolumeM3, LowerVolumeM3, UpperDensityKgM3, LowerDensityKgM3, WallTemperatureK;
        public FireCellMetrics(double pressure, double upperTemperature, double lowerTemperature, double interfaceHeight,
            double upperVolume, double lowerVolume, double upperDensity, double lowerDensity, double wallTemperature)
        {
            PressurePa = pressure; UpperTemperatureK = upperTemperature; LowerTemperatureK = lowerTemperature;
            InterfaceHeightM = interfaceHeight; UpperVolumeM3 = upperVolume; LowerVolumeM3 = lowerVolume;
            UpperDensityKgM3 = upperDensity; LowerDensityKgM3 = lowerDensity; WallTemperatureK = wallTemperature;
        }
    }

    public readonly struct FireOpeningSample
    {
        public readonly double ElevationM, PressureDifferencePa, JetSpeedMS, NominalAreaMeanSpeedMS, MassFlowPerHeightKgSM;
        public FireOpeningSample(double elevation, double pressure, double jet, double mean, double mass)
        { ElevationM = elevation; PressureDifferencePa = pressure; JetSpeedMS = jet; NominalAreaMeanSpeedMS = mean; MassFlowPerHeightKgSM = mass; }
    }
}
