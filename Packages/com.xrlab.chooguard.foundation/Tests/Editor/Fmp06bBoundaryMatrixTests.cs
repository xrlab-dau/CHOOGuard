using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    /// <summary>
    /// FMP-06b (#123): door / transom / mechanical-ventilation boundary matrix and sloped-compartment
    /// regressions on the same authored 147-cell / 275-opening / 150-fan connected domain the FMP-06a
    /// thermal budget was recorded against.
    ///
    /// Fixed acceptance conditions (changing any of them invalidates the recorded comparison): the derived
    /// domain has 147 cells, 275 openings and 150 fans; two prescribed 60 kW sources; 100 world ticks of
    /// 0.05 s (5.0 simulated seconds); solver step cap 0.01 s through <see cref="ConnectedThermalDomain.SolverOptions"/>.
    ///
    /// Scope: this is the reduced conservative two-zone network. It is not CFD, not a certified life-safety
    /// calculation and not a surveyed leakage measurement. Everything below is a regression on the authored
    /// synthetic geometry recorded in foundation/world/connected-world-profile.json.
    ///
    /// MATRIX SEMANTICS. The matrix has one row per (boundary actuator x commanded state). A row is
    /// SUPPORTED when the shipped <see cref="ConnectedThermalDomain.Forcing"/> adapter can actually reach
    /// that state, and UNSUPPORTED when it cannot. Support is DERIVED by enumerating every combination of
    /// the adapter's three predicates (portal open, boarding aligned, fan enabled) and inspecting the
    /// resulting per-door opening fractions and per-fan enables - it is not asserted by hand. An
    /// UNSUPPORTED row is never executed and is never counted as a PASS; the receipt carries the reason and
    /// the probe evidence instead.
    ///
    /// The probe boundary is the underground_connector--underground_shopping_passage portal of the 99.6 m
    /// operator-published corridor, which carries one of the two prescribed 60 kW sources. Its authored
    /// closure at ClosureAlong 0.5 splits the 2.7 m clear opening into a controlled 2.3 m leaf plus an
    /// uncontrolled 0.4 m lintel transom; the two remaining cuts of the same portal are uncontrolled
    /// full-height openings, so the chain is never sealable through the shipped adapter.
    /// </summary>
    public sealed class Fmp06bBoundaryMatrixTests
    {
        private const int FixedCellCount = 147;
        private const int FixedOpeningCount = 275;
        private const int FixedFanCount = 150;
        private const int FixedRegionCount = 13;
        private const int FixedPortalCount = 12;
        private const int WorldTickCount = 100;
        private const double WorldTickSeconds = .05;
        private const double FixedSimulatedSeconds = WorldTickCount * WorldTickSeconds;
        private const int ConvergenceTicks = 20;
        private const int BoundaryFluxSamples = 16;
        private const double SourceHeatReleaseW = 60000;
        private const double SourceFuelMassKgPerSecond = .004;
        private const double SourceSmokeKgPerSecond = .0004;
        private const double MassResidualBudgetKg = 1e-3;
        private const double EnergyResidualBudgetJ = .005;
        private const double PressureResidualBudgetPa = 1e-3;
        private const double FluxZeroToleranceKgPerSecond = 1e-12;
        private const double ConvergenceTemperatureToleranceK = 1e-3;
        private const double ConvergenceFluxFraction = 1e-2;
        private const double SourceSmokeRetentionFraction = .98;
        private const string EvidenceDirectory = "Temp/ChooGuardBoundaryMatrix";

        /// <summary>Door matrix probe portal: the operator-published corridor's controlled doorway. Authored
        /// ClosureAlong 0.5 with ClosureBottom 0 / ClosureHeight 2.3 inside a 2.7 m clear opening, giving one
        /// controlled 2.3 m leaf plus an uncontrolled 0.4 m lintel transom at the authored cut.</summary>
        private const string DoorProbePortalId = "underground_connector--underground_shopping_passage";
        private const string DoorProbeSourceRegion = "underground_connector";
        private const string DoorProbeSinkRegion = "underground_shopping_passage";
        /// <summary>Mechanical ventilation probe region: the 99.6 m operator-published corridor, which
        /// carries the second prescribed source and is the longest authored compartment.</summary>
        private const string VentilationProbeRegion = "underground_connector";
        private const string OpenAllSignature = "boundary-all-open";
        private const string LeafClosedSignature = "leaf-closed:" + DoorProbePortalId;
        private const string VentilationOffSignature = "ventilation-off:" + VentilationProbeRegion;

        private static readonly string[] SourceRegions = { "rolling_stock_mainline", "underground_connector" };
        private static readonly string[] MatrixTypes =
            { "door.leaf", "transom.above", "boundary.sealed", "ventilation.supply", "ventilation.exhaust" };
        private static readonly string[] MatrixStates = { "open", "closed" };

        private static ConnectedWorldDefinition world;
        private static ConnectedThermalDomain domain;
        private static FireSourcePower[] sources;
        private static Dictionary<string, BoundaryRun> runs;
        private static MatrixCell[] matrix;
        private static ActuationProbe probe;
        private static string leafDoorId, transomDoorId, intakeFanId, exhaustFanId;

        // ---------------------------------------------------------------- fixture ----

        private static ConnectedWorldDefinition World() => JsonUtility.FromJson<ConnectedWorldDefinition>(
            File.ReadAllText("foundation/world/connected-world-profile.json"));

        private static ConnectedThermalDomain Fixture() => ConnectedThermalDomain.Create(World(), new ConnectedThermalProfile());

        /// <summary>Prescribed two-source forcing. Every predicate defaults to open/enabled so a pass that
        /// does not name an actuator reproduces the FMP-06a boundary condition.</summary>
        private static FireForcing Forcing(ConnectedThermalDomain target, Func<string, bool> portalOpen = null,
            Func<string, bool> boardingAligned = null, Func<string, bool> fanEnabled = null)
        {
            portalOpen = portalOpen ?? (_ => true);
            boardingAligned = boardingAligned ?? (_ => true);
            fanEnabled = fanEnabled ?? (_ => true);
            return target.Forcing(Sources(target), portalOpen, boardingAligned, fanEnabled);
        }

        private static FireSourcePower[] Sources(ConnectedThermalDomain target) => SourceRegions.Select(region => new FireSourcePower {
            CellId = target.Bindings.First(b => b.RegionId == region && b.PortalId == "").CellId,
            HeatReleaseW = SourceHeatReleaseW, FuelMassKgPerSecond = SourceFuelMassKgPerSecond,
            SmokeMassKgPerSecond = SourceSmokeKgPerSecond, HeightM = .5 }).ToArray();

        private static int[] CellIndices(ConnectedThermalDomain target, Func<ThermalCellBinding, bool> select) =>
            target.Bindings.Select((b, i) => new { Binding = b, Index = i }).Where(x => select(x.Binding)).Select(x => x.Index).ToArray();

        private static int[] DoorIndices(ConnectedThermalDomain target, Func<ThermalDoorBinding, bool> select) =>
            target.DoorBindings.Select((b, i) => new { Binding = b, Index = i }).Where(x => select(x.Binding)).Select(x => x.Index).ToArray();

        private static int[] FanIndices(ConnectedThermalDomain target, Func<ThermalFanBinding, bool> select) =>
            target.FanBindings.Select((b, i) => new { Binding = b, Index = i }).Where(x => select(x.Binding)).Select(x => x.Index).ToArray();

        private static int[] RegionCells(ConnectedThermalDomain target, string regionId) =>
            CellIndices(target, b => b.RegionId == regionId && b.PortalId == "");

        private static double RegionSmokeKg(FireState state, ConnectedThermalDomain target, string regionId) =>
            RegionCells(target, regionId).Sum(i => state.Cells[i].UpperSmokeKg + state.Cells[i].LowerSmokeKg);

        private static double RegionUpperTemperatureK(ZoneFireModel model, ConnectedThermalDomain target, string regionId)
        {
            var cells = RegionCells(target, regionId);
            Assert.That(cells, Is.Not.Empty, regionId + " carries no authored cell");
            return cells.Max(i => model.ReadCell(target.Network.Cells[i].Id).UpperTemperatureK);
        }

        private static double RegionLowerTemperatureK(ZoneFireModel model, ConnectedThermalDomain target, string regionId) =>
            RegionCells(target, regionId).Max(i => model.ReadCell(target.Network.Cells[i].Id).LowerTemperatureK);

        /// <summary>Signed mass flow through one opening, integrated over its authored elevation range.
        /// Positive is FromCell -> ToCell; a zero commanded fraction must integrate to exactly zero.</summary>
        private static double BoundaryFluxKgPerSecond(ZoneFireModel model, ConnectedThermalDomain target, string doorId, FireForcing forcing)
        {
            var door = target.Network.Doors.Single(d => d.Id == doorId);
            var dz = door.HeightM / BoundaryFluxSamples;
            var total = 0.0;
            for (var i = 0; i < BoundaryFluxSamples; i++)
                total += model.SampleOpening(doorId, door.BottomElevationM + (i + .5) * dz, forcing).MassFlowPerHeightKgSM * dz;
            return total;
        }

        private static double CellMass(FireState state) => state.Cells.Sum(c => c.UpperMassKg + c.LowerMassKg);

        private static double CellEnergy(FireState state) =>
            state.Cells.Sum(c => c.UpperEnergyJ + c.LowerEnergyJ + c.WallEnergyJ);

        private static bool NonNegative(ZoneFireModel model, FireState state) =>
            state.Cells.All(c => c.UpperMassKg >= 0 && c.LowerMassKg >= 0 && c.UpperSmokeKg >= 0 && c.LowerSmokeKg >= 0 &&
                c.UpperSmokeKg <= c.UpperMassKg && c.LowerSmokeKg <= c.LowerMassKg &&
                c.UpperEnergyJ > 0 && c.LowerEnergyJ > 0 && c.WallEnergyJ > 0) &&
            state.Cells.Select(c => model.ReadCell(c.CellId)).All(m => m.PressurePa > 0 && m.UpperTemperatureK > 0 &&
                m.LowerTemperatureK > 0 && m.WallTemperatureK > 0 && m.UpperVolumeM3 > 0 && m.LowerVolumeM3 > 0 &&
                m.InterfaceHeightM >= 0 && m.UpperDensityKgM3 > 0 && m.LowerDensityKgM3 > 0);

        [OneTimeSetUp]
        public void BuildBoundaryFixture()
        {
            world = World(); domain = Fixture(); sources = Sources(domain);
            Assert.That(domain.Network.Cells.Length, Is.EqualTo(FixedCellCount), "authored cell budget");
            Assert.That(domain.Network.Doors.Length, Is.EqualTo(FixedOpeningCount), "authored opening budget");
            Assert.That(domain.Network.Fans.Length, Is.EqualTo(FixedFanCount), "authored mechanical ventilation budget");

            var leaves = domain.DoorBindings.Where(b => b.PortalId == DoorProbePortalId && b.Controlled).ToArray();
            var transoms = domain.DoorBindings.Where(b => b.PortalId == DoorProbePortalId && !b.Controlled &&
                b.DoorId.EndsWith("-above", StringComparison.Ordinal)).ToArray();
            Assert.That(leaves.Length, Is.EqualTo(1), "probe portal must carry exactly one controlled leaf");
            Assert.That(transoms.Length, Is.EqualTo(1), "probe portal must carry exactly one authored lintel transom");
            Assert.That(world.Portals.Single(p => p.Id == DoorProbePortalId).StaticBoarding, Is.False,
                "the corridor doorway is a plain controlled portal, not a boarding coupling");
            leafDoorId = leaves[0].DoorId; transomDoorId = transoms[0].DoorId;
            intakeFanId = domain.FanBindings.First(b => b.RegionId == VentilationProbeRegion &&
                b.FanId.StartsWith("intake-", StringComparison.Ordinal)).FanId;
            exhaustFanId = domain.FanBindings.First(b => b.RegionId == VentilationProbeRegion &&
                b.FanId.StartsWith("exhaust-", StringComparison.Ordinal)).FanId;

            probe = ProbeActuation();
            runs = new Dictionary<string, BoundaryRun> {
                { OpenAllSignature, RunBoundaryPass(OpenAllSignature, Forcing(domain)) },
                { LeafClosedSignature, RunBoundaryPass(LeafClosedSignature, Forcing(domain, portalOpen: p => p != DoorProbePortalId)) },
                { VentilationOffSignature, RunBoundaryPass(VentilationOffSignature, Forcing(domain, fanEnabled: r => r != VentilationProbeRegion)) } };
            matrix = BuildMatrix();
        }

        // ------------------------------------------------------- actuation probe ----

        private sealed class ActuationSample
        {
            public double[] DoorFractions;
            public bool[] Fans;
        }

        private sealed class ActuationProbe
        {
            public int Samples;
            public double LeafMin, LeafMax, TransomMin, TransomMax;
            public int PortalDoorCount, ControlledDoorCount, GatedDoorCount, AlwaysOpenDoorCount, MinimumOpenDoors;
            public bool LeafClosableAlone, TransomOpenAlone, SealedBoundaryReachable, VentilationIndependent;
            public bool SupplyOn, SupplyOff, ExhaustOn, ExhaustOff, AllOpen;
            public string Evidence = "";
        }

        /// <summary>Enumerates the shipped adapter's whole control space: 4 portal predicates x 3 boarding
        /// predicates x 3 fan predicates = 36 distinct forcings, and reads back the resolved commanded state
        /// of the probe portal's leaf and transom and the probe region's supply and exhaust fans.</summary>
        private ActuationProbe ProbeActuation()
        {
            var portalPredicates = new Func<string, bool>[] { _ => true, _ => false, p => p != DoorProbePortalId, p => p == DoorProbePortalId };
            var boardingPredicates = new Func<string, bool>[] { _ => true, _ => false, p => p != DoorProbePortalId };
            var fanPredicates = new Func<string, bool>[] { _ => true, _ => false, r => r != VentilationProbeRegion };
            var samples = new List<ActuationSample>();
            foreach (var portal in portalPredicates)
                foreach (var boarding in boardingPredicates)
                    foreach (var fan in fanPredicates)
                    {
                        var forcing = Forcing(domain, portal, boarding, fan);
                        var fractions = new double[domain.DoorBindings.Length];
                        var enabled = new bool[domain.FanBindings.Length];
                        for (var i = 0; i < fractions.Length; i++)
                            fractions[i] = forcing.Doors.First(s => s.DoorId == domain.DoorBindings[i].DoorId).OpeningFraction;
                        for (var i = 0; i < enabled.Length; i++)
                            enabled[i] = forcing.Fans.First(s => s.FanId == domain.FanBindings[i].FanId).Enabled;
                        samples.Add(new ActuationSample { DoorFractions = fractions, Fans = enabled });
                    }

            var leaf = DoorIndices(domain, b => b.DoorId == leafDoorId);
            var transom = DoorIndices(domain, b => b.DoorId == transomDoorId);
            // Every authored aperture of the probe portal, not just the controlled leaf and its transom: the
            // other cuts of the same portal are uncontrolled openings that the adapter cannot shut at all.
            var portalDoors = DoorIndices(domain, b => b.PortalId == DoorProbePortalId);
            var supply = FanIndices(domain, b => b.FanId == intakeFanId);
            var exhaust = FanIndices(domain, b => b.FanId == exhaustFanId);
            double Minimum(int[] rows, Func<ActuationSample, int, double> read) => samples.Min(s => rows.Min(i => read(s, i)));
            double Maximum(int[] rows, Func<ActuationSample, int, double> read) => samples.Max(s => rows.Max(i => read(s, i)));
            int OpenDoors(ActuationSample sample) => portalDoors.Count(i => sample.DoorFractions[i] > 0);
            bool FanReachable(int[] rows, bool state) => samples.Any(s => rows.All(i => s.Fans[i] == state));
            var leafMin = Minimum(leaf, (s, i) => s.DoorFractions[i]);
            var leafMax = Maximum(leaf, (s, i) => s.DoorFractions[i]);
            var transomMin = Minimum(transom, (s, i) => s.DoorFractions[i]);
            var transomMax = Maximum(transom, (s, i) => s.DoorFractions[i]);
            var alwaysOpen = portalDoors.Count(i => samples.All(s => s.DoorFractions[i] > 0));
            var minimumOpen = samples.Min(OpenDoors);
            var shareControl = samples.Any(s => supply.All(i => s.Fans[i]) != exhaust.All(i => s.Fans[i]));

            return new ActuationProbe {
                Samples = samples.Count,
                LeafMin = leafMin, LeafMax = leafMax, TransomMin = transomMin, TransomMax = transomMax,
                PortalDoorCount = portalDoors.Length,
                ControlledDoorCount = DoorIndices(domain, b => b.PortalId == DoorProbePortalId && b.Controlled).Length,
                GatedDoorCount = DoorIndices(domain, b => b.PortalId == DoorProbePortalId && !b.Controlled && b.BoardingCoupling).Length,
                AlwaysOpenDoorCount = alwaysOpen, MinimumOpenDoors = minimumOpen,
                // Closing the leaf while the authored lintel gap stays open is the only expressible single-actuator move.
                LeafClosableAlone = samples.Any(s => leaf.All(i => s.DoorFractions[i] == 0) && transom.All(i => s.DoorFractions[i] == 1)),
                TransomOpenAlone = samples.Any(s => transom.All(i => s.DoorFractions[i] == 1) && leaf.All(i => s.DoorFractions[i] == 1)),
                // A sealed boundary needs every aperture of the whole portal shut simultaneously.
                SealedBoundaryReachable = samples.Any(s => OpenDoors(s) == 0),
                // Independent mechanical control needs one probe forcing where supply and exhaust disagree.
                VentilationIndependent = shareControl,
                SupplyOn = FanReachable(supply, true), SupplyOff = FanReachable(supply, false),
                ExhaustOn = FanReachable(exhaust, true), ExhaustOff = FanReachable(exhaust, false),
                AllOpen = samples.Any(s => portalDoors.All(i => s.DoorFractions[i] > 0) &&
                    supply.Concat(exhaust).All(i => s.Fans[i])),
                Evidence = samples.Count + " adapter forcings probed; leaf fraction range [" + leafMin + "," + leafMax +
                    "]; lintel transom fraction range [" + transomMin + "," + transomMax + "]; portal has " + portalDoors.Length +
                    " authored apertures of which " + DoorIndices(domain, b => b.PortalId == DoorProbePortalId && b.Controlled).Length +
                    " controlled, " + alwaysOpen + " unconditional (at fraction 1 in every forcing), minimum simultaneously open " +
                    minimumOpen + "; supply reachable on=" + FanReachable(supply, true) + " off=" + FanReachable(supply, false) +
                    "; exhaust reachable on=" + FanReachable(exhaust, true) + " off=" + FanReachable(exhaust, false) +
                    "; supply and exhaust independently separable=" + (shareControl ? "yes" : "no") };
        }

        // ------------------------------------------------------------ matrix ----

        [Serializable]
        private sealed class MatrixCell
        {
            public string Type, State, Classification, UnsupportedReason = "", RunSignature = "", EquivalentRows = "", Reachability = "", Coupling = "";
            public bool Supported, Independent, PassCounted;
            public int TicksAdvanced;
            public double SinkRegionSmokeKg, SourceRegionSmokeKg, SinkRegionUpperTemperatureK, SourceRegionUpperTemperatureK;
            public double VentilationRegionSmokeKg, VentilationRegionLowerTemperatureK, VentilationRegionUpperTemperatureK;
            public double LeafBoundaryFluxKgPerSecond, TransomBoundaryFluxKgPerSecond, BoundaryNetFluxKgPerSecond;
            public double MassBalanceErrorKg, EnergyBalanceErrorJ, MaximumCommittedPressureResidualPa;
            public bool NonNegative, AdvanceSucceeded;
        }

        private MatrixCell[] BuildMatrix()
        {
            var rows = new List<MatrixCell>();
            foreach (var type in MatrixTypes)
                foreach (var state in MatrixStates)
                {
                    var open = state == "open";
                    bool supported; bool independent; string coupling = ""; string reason = ""; string signature = ""; string reachability;
                    switch (type)
                    {
                        case "door.leaf":
                            supported = open ? probe.LeafMax == 1 : probe.LeafMin == 0;
                            independent = probe.LeafClosableAlone;
                            signature = open ? OpenAllSignature : LeafClosedSignature;
                            reachability = "leaf fraction range [" + probe.LeafMin + "," + probe.LeafMax + "] over " + probe.Samples + " adapter forcings";
                            break;
                        case "transom.above":
                            supported = open ? probe.TransomMax == 1 : probe.TransomMin == 0;
                            independent = probe.TransomOpenAlone;
                            signature = open ? OpenAllSignature : "";
                            reachability = "lintel transom fraction range [" + probe.TransomMin + "," + probe.TransomMax + "] over " + probe.Samples + " adapter forcings";
                            if (!supported)
                                reason = "the authored lintel transom of " + DoorProbePortalId + " is not in the adapter's controllable set (Controlled=false, no per-door " +
                                    "channel); its fraction is " + probe.TransomMin + " under every adapter forcing, so commanding it closed is unreachable.";
                            break;
                        case "boundary.sealed":
                            supported = open ? probe.AllOpen : probe.SealedBoundaryReachable;
                            independent = false;
                            signature = open ? OpenAllSignature : "";
                            reachability = "portal carries " + probe.PortalDoorCount + " authored apertures (" + probe.ControlledDoorCount + " controlled, " +
                                probe.AlwaysOpenDoorCount + " unconditional); minimum simultaneously open = " + probe.MinimumOpenDoors + " over " + probe.Samples + " adapter forcings";
                            if (!supported)
                                reason = "the portal carries " + probe.PortalDoorCount + " authored apertures and the adapter commands only the " +
                                    probe.ControlledDoorCount + " closure leaf; the remaining " + probe.AlwaysOpenDoorCount + " uncontrolled apertures stay at fraction 1 " +
                                    "under every adapter forcing, so at least " + probe.MinimumOpenDoors + " apertures are open in every reachable state and a sealed boundary is " +
                                    "unreachable. Sealing would need a per-aperture command channel.";
                            break;
                        default:
                            var supply = type == "ventilation.supply";
                            supported = open ? (supply ? probe.SupplyOn : probe.ExhaustOn) : (supply ? probe.SupplyOff : probe.ExhaustOff);
                            independent = probe.VentilationIndependent;
                            signature = open ? OpenAllSignature : VentilationOffSignature;
                            coupling = "supply and exhaust of " + VentilationProbeRegion + " share the single region-keyed fan predicate of the adapter, so neither " +
                                "can be commanded apart from the other; both rows resolve to the same commanded state " + (open ? "\"boundary-all-open\"" : "\"" + VentilationOffSignature + "\"");
                            reachability = "supply reachable on=" + probe.SupplyOn + " off=" + probe.SupplyOff + "; exhaust reachable on=" + probe.ExhaustOn +
                                " off=" + probe.ExhaustOff + "; separable=" + (probe.VentilationIndependent ? "yes" : "no");
                            break;
                    }
                    rows.Add(new MatrixCell { Type = type, State = state, Supported = supported, Independent = independent,
                        Classification = supported ? "SUPPORTED" : "UNSUPPORTED", UnsupportedReason = reason,
                        RunSignature = signature, Reachability = reachability, Coupling = coupling });
                }

            // Execute each distinct commanded boundary state exactly once.
            foreach (var row in rows.Where(r => r.Supported))
            {
                var run = runs[row.RunSignature];
                row.PassCounted = true;
                row.TicksAdvanced = run.TicksAdvanced;
                row.AdvanceSucceeded = run.AdvanceSucceeded;
                row.NonNegative = run.NonNegative;
                row.SinkRegionSmokeKg = run.SinkRegionSmokeKg;
                row.SourceRegionSmokeKg = run.SourceRegionSmokeKg;
                row.SinkRegionUpperTemperatureK = run.SinkRegionUpperTemperatureK;
                row.SourceRegionUpperTemperatureK = run.SourceRegionUpperTemperatureK;
                row.VentilationRegionSmokeKg = run.VentilationRegionSmokeKg;
                row.VentilationRegionLowerTemperatureK = run.VentilationRegionLowerTemperatureK;
                row.VentilationRegionUpperTemperatureK = run.VentilationRegionUpperTemperatureK;
                row.LeafBoundaryFluxKgPerSecond = run.LeafBoundaryFluxKgPerSecond;
                row.TransomBoundaryFluxKgPerSecond = run.TransomBoundaryFluxKgPerSecond;
                row.BoundaryNetFluxKgPerSecond = run.BoundaryNetFluxKgPerSecond;
                row.MassBalanceErrorKg = run.MassBalanceErrorKg;
                row.EnergyBalanceErrorJ = run.EnergyBalanceErrorJ;
                row.MaximumCommittedPressureResidualPa = run.MaximumCommittedPressureResidualPa;
            }
            // Record which rows collapse onto the same commanded state - that is the coupling evidence.
            foreach (var row in rows.Where(r => r.Supported))
                row.EquivalentRows = string.Join(",", rows.Where(other => other != row && other.Supported &&
                    other.RunSignature == row.RunSignature).Select(other => other.Type + "/" + other.State).ToArray());
            return rows.ToArray();
        }

        private static string Key(MatrixCell row) => row.Type + "/" + row.State;

        // ------------------------------------------------------------- passes ----

        private sealed class BoundaryRun
        {
            public string Signature;
            public FireState Initial, Final;
            public int TicksAdvanced, AcceptedSubsteps, RejectedSubsteps;
            public double MassBalanceErrorKg, EnergyBalanceErrorJ, MaximumCommittedPressureResidualPa;
            public double SinkRegionSmokeKg, SourceRegionSmokeKg, SinkRegionUpperTemperatureK, SourceRegionUpperTemperatureK;
            public double VentilationRegionSmokeKg, VentilationRegionLowerTemperatureK, VentilationRegionUpperTemperatureK;
            public double LeafBoundaryFluxKgPerSecond, TransomBoundaryFluxKgPerSecond, BoundaryNetFluxKgPerSecond;
            public bool NonNegative, AdvanceSucceeded;
            public string Failure = "";
        }

        private static BoundaryRun RunBoundaryPass(string signature, FireForcing forcing)
        {
            var fire = new ZoneFireModel(domain.Network, options: domain.SolverOptions);
            var initial = fire.ExportState();
            var accepted = 0; var rejected = 0; var pressure = 0.0; var ticks = 0; var failure = ""; var succeeded = true;
            for (var i = 0; i < WorldTickCount; i++)
            {
                if (!fire.TryAdvance(WorldTickSeconds, forcing, out var report)) { failure = report.Failure; succeeded = false; break; }
                accepted += report.AcceptedSubsteps; rejected += report.RejectedSubsteps;
                pressure = Math.Max(pressure, report.MaximumCommittedPressureResidualPa);
                ticks++;
            }
            var final = fire.ExportState();
            var leaf = BoundaryFluxKgPerSecond(fire, domain, leafDoorId, forcing);
            var transom = BoundaryFluxKgPerSecond(fire, domain, transomDoorId, forcing);
            // Total boundary transport across every authored aperture of the probe portal.
            var portal = domain.DoorBindings.Where(b => b.PortalId == DoorProbePortalId)
                .Sum(b => Math.Abs(BoundaryFluxKgPerSecond(fire, domain, b.DoorId, forcing)));
            return new BoundaryRun { Signature = signature, Initial = initial, Final = final, TicksAdvanced = ticks,
                AcceptedSubsteps = accepted, RejectedSubsteps = rejected, Failure = failure, AdvanceSucceeded = succeeded,
                MassBalanceErrorKg = Math.Abs((CellMass(final) - CellMass(initial)) - final.ExternalMassKg),
                EnergyBalanceErrorJ = Math.Abs((CellEnergy(final) - CellEnergy(initial)) - final.ExternalEnergyJ),
                MaximumCommittedPressureResidualPa = pressure, NonNegative = NonNegative(fire, final),
                SinkRegionSmokeKg = RegionSmokeKg(final, domain, DoorProbeSinkRegion),
                SourceRegionSmokeKg = RegionSmokeKg(final, domain, DoorProbeSourceRegion),
                SinkRegionUpperTemperatureK = RegionUpperTemperatureK(fire, domain, DoorProbeSinkRegion),
                SourceRegionUpperTemperatureK = RegionUpperTemperatureK(fire, domain, DoorProbeSourceRegion),
                VentilationRegionSmokeKg = RegionSmokeKg(final, domain, VentilationProbeRegion),
                VentilationRegionLowerTemperatureK = RegionLowerTemperatureK(fire, domain, VentilationProbeRegion),
                VentilationRegionUpperTemperatureK = RegionUpperTemperatureK(fire, domain, VentilationProbeRegion),
                LeafBoundaryFluxKgPerSecond = leaf, TransomBoundaryFluxKgPerSecond = transom,
                BoundaryNetFluxKgPerSecond = portal };
        }

        // -------------------------------------------------------------- tests ----

        [Test]
        public void FixedBoundaryFixtureStillReproducesTheRecordedCellOpeningAndFanBudget()
        {
            Assert.That(domain.Network.Cells.Length, Is.EqualTo(FixedCellCount), "147 authored cells");
            Assert.That(domain.Network.Doors.Length, Is.EqualTo(FixedOpeningCount), "275 authored openings");
            Assert.That(domain.Network.Fans.Length, Is.EqualTo(FixedFanCount), "150 authored mechanical fans");
            Assert.That(world.Regions.Length, Is.EqualTo(FixedRegionCount), "13 authored regions");
            Assert.That(world.Portals.Length, Is.EqualTo(FixedPortalCount), "12 authored portals");
            Assert.That(domain.Bindings.Length, Is.EqualTo(domain.Network.Cells.Length), "one thermal binding per cell");
            Assert.That(domain.Bindings.Select(b => b.CellId).Distinct().Count(), Is.EqualTo(domain.Bindings.Length));
            for (var i = 0; i < domain.Bindings.Length; i++)
                Assert.That(domain.Bindings[i].CellId, Is.EqualTo(domain.Network.Cells[i].Id), "binding and cell order must stay aligned");
            Assert.That(domain.DoorBindings.Select(b => b.DoorId).Distinct().Count(), Is.EqualTo(domain.DoorBindings.Length));
            Assert.That(domain.Network.Cells.All(c => c.VolumeM3 > 0), Is.True);
            Assert.That(runs.Count, Is.EqualTo(3), "the matrix resolves onto exactly three distinct commanded boundary states");
            Assert.That(runs.Values.All(r => r.AdvanceSucceeded), Is.True, "every commanded boundary state integrates");
            Assert.That(runs.Values.All(r => r.TicksAdvanced == WorldTickCount), Is.True, "100 world ticks per pass");
            Assert.That(runs.Values.All(r => Math.Abs(r.Final.SimulatedSeconds - FixedSimulatedSeconds) < 1e-9), Is.True);
        }

        [Test]
        public void EveryMatrixRowIsClassifiedAndNoUnsupportedRowIsCountedAsPass()
        {
            Assert.That(matrix.Length, Is.EqualTo(MatrixTypes.Length * MatrixStates.Length), "open/closed x actuator type");
            Assert.That(matrix.Select(Key).Distinct().Count(), Is.EqualTo(matrix.Length), "no duplicate matrix row");
            foreach (var type in MatrixTypes)
                Assert.That(matrix.Count(r => r.Type == type), Is.EqualTo(MatrixStates.Length), type + " must be tested in both states");

            var supported = matrix.Where(r => r.Supported).ToArray();
            var unsupported = matrix.Where(r => !r.Supported).ToArray();
            Assert.That(supported, Is.Not.Empty, "at least one actuator axis must be commandable");

            foreach (var row in unsupported)
            {
                Assert.That(row.PassCounted, Is.False, Key(row) + " is UNSUPPORTED and must never be counted as PASS");
                Assert.That(row.TicksAdvanced, Is.Zero, Key(row) + " must not be executed as a pass");
                Assert.That(row.UnsupportedReason, Is.Not.Empty, Key(row) + " must carry an explicit reason");
                Assert.That(row.Classification, Is.EqualTo("UNSUPPORTED"), Key(row));
                Assert.That(row.RunSignature, Is.Empty, Key(row) + " has no expressible commanded state");
            }
            foreach (var row in supported)
            {
                Assert.That(row.PassCounted, Is.True, Key(row));
                Assert.That(row.Classification, Is.EqualTo("SUPPORTED"), Key(row));
                Assert.That(row.RunSignature, Is.Not.Empty, Key(row));
                Assert.That(row.TicksAdvanced, Is.EqualTo(WorldTickCount), Key(row) + " reached its commanded state for the whole horizon");
                Assert.That(row.AdvanceSucceeded, Is.True, Key(row));
                Assert.That(row.NonNegative, Is.True, Key(row) + " non-negative mass, smoke, pressure, temperature and energy");
            }
            Assert.That(matrix.Count(r => r.PassCounted), Is.EqualTo(supported.Length),
                "PASS rows must be exactly the SUPPORTED rows; unsupported rows must not pad the pass count");

            // The load-bearing classification: the two aperture states the shipped adapter cannot command.
            Assert.That(matrix.Single(r => r.Type == "transom.above" && r.State == "closed").Supported, Is.False,
                "the authored lintel transom has no command channel");
            Assert.That(matrix.Single(r => r.Type == "boundary.sealed" && r.State == "closed").Supported, Is.False,
                "a sealed portal boundary is not expressible while the transom is uncontrolled");
            Assert.That(matrix.Single(r => r.Type == "transom.above" && r.State == "closed").Reachability, Is.Not.Empty,
                "an UNSUPPORTED row still reports the probe range that proves it unreachable");
            Assert.That(matrix.Single(r => r.Type == "door.leaf" && r.State == "closed").Independent, Is.True,
                "the controlled leaf closes without moving the lintel transom");
            foreach (var row in matrix.Where(r => r.Type.StartsWith("ventilation.", StringComparison.Ordinal) && r.Supported))
                Assert.That(row.Independent, Is.False, Key(row) + " shares one region-keyed fan predicate");
            Assert.That(matrix.Where(r => r.Type.StartsWith("ventilation.", StringComparison.Ordinal)).All(r => r.Coupling != ""), Is.True);
            // The coupled pair must provably collapse onto one commanded state.
            var supplyClosed = matrix.Single(r => r.Type == "ventilation.supply" && r.State == "closed");
            var exhaustClosed = matrix.Single(r => r.Type == "ventilation.exhaust" && r.State == "closed");
            Assert.That(supplyClosed.RunSignature, Is.EqualTo(exhaustClosed.RunSignature), "the two ventilation rows are not separable");
            Assert.That(supplyClosed.EquivalentRows, Does.Contain("ventilation.exhaust/closed"));
            Assert.That(supplyClosed.VentilationRegionSmokeKg, Is.EqualTo(exhaustClosed.VentilationRegionSmokeKg));

            var receipt = new MatrixReceipt {
                Cells = domain.Network.Cells.Length, Openings = domain.Network.Doors.Length, Fans = domain.Network.Fans.Length,
                Regions = world.Regions.Length, Portals = world.Portals.Length, MatrixRows = matrix.Length,
                SupportedRows = supported.Length, UnsupportedRows = unsupported.Length,
                PassCountedRows = matrix.Count(r => r.PassCounted), ExecutedRuns = runs.Count,
                DoorProbePortalId = DoorProbePortalId, DoorProbeSourceRegion = DoorProbeSourceRegion, DoorProbeSinkRegion = DoorProbeSinkRegion,
                VentilationProbeRegion = VentilationProbeRegion, ProbeEvidence = probe.Evidence, Matrix = matrix,
                Distinctions = matrix.Select(r => Key(r) + "=" + r.Classification + (r.Supported ? "" : " (" + r.UnsupportedReason + ")")).ToArray(),
                UnsupportedAxes = unsupported.Select(r => Key(r) + ": " + r.UnsupportedReason).ToArray() };
            WriteReceipt("fmp06b-boundary-matrix.json", "FMP06B-MATRIX", receipt);
        }

        [Test]
        public void DoorTransomAndVentilationResponseFollowTheCommandedBoundaryState()
        {
            var allOpen = runs[OpenAllSignature];
            var leafClosed = runs[LeafClosedSignature];
            var ventilationOff = runs[VentilationOffSignature];
            Assert.That(allOpen.AdvanceSucceeded && leafClosed.AdvanceSucceeded && ventilationOff.AdvanceSucceeded, Is.True);

            // (1) A closed controlled leaf is not a sealed boundary: the authored lintel transom still passes gas.
            Assert.That(leafClosed.LeafBoundaryFluxKgPerSecond, Is.EqualTo(0).Within(FluxZeroToleranceKgPerSecond),
                "a commanded-closed leaf must carry no mass");
            Assert.That(Math.Abs(leafClosed.TransomBoundaryFluxKgPerSecond), Is.GreaterThan(0),
                "the uncontrolled lintel transom keeps the boundary open at a commanded-closed leaf");
            Assert.That(leafClosed.BoundaryNetFluxKgPerSecond, Is.LessThan(allOpen.BoundaryNetFluxKgPerSecond),
                "closing the leaf must reduce total portal boundary transport");

            // (2) Smoke response across the doorway. The transported smoke is small in absolute terms - the
            // doorway top sits near the source region's layer interface - so the claim is the direction and
            // the ratio, not a magnitude. The measured values are in the receipt.
            Assert.That(leafClosed.SinkRegionSmokeKg, Is.LessThan(allOpen.SinkRegionSmokeKg),
                "closing the boundary leaf must reduce smoke arriving in " + DoorProbeSinkRegion);
            Assert.That(leafClosed.SourceRegionSmokeKg, Is.GreaterThan(0), "the source region must still hold its smoke ledger");
            // At this horizon the doorway is sub-critical for smoke: the shutting of the leaf changes the
            // smoke retained upstream only in the sixth significant figure. Rather than tune a tolerance to
            // that, the recorded claim is the robust one - the source region keeps the overwhelming majority
            // of its injected smoke under every commanded boundary state. The measured delta is in the receipt.
            var injectedSmokeKg = SourceSmokeKgPerSecond * FixedSimulatedSeconds;
            Assert.That(allOpen.SourceRegionSmokeKg, Is.GreaterThan(SourceSmokeRetentionFraction * injectedSmokeKg),
                "the open doorway must not drain " + DoorProbeSourceRegion);
            Assert.That(leafClosed.SourceRegionSmokeKg, Is.GreaterThan(SourceSmokeRetentionFraction * injectedSmokeKg),
                "a shut doorway must not drain " + DoorProbeSourceRegion);
            Assert.That(leafClosed.SourceRegionSmokeKg, Is.LessThanOrEqualTo(injectedSmokeKg), "smoke ledger cannot exceed what was injected");

            // (3) Mechanical ventilation is a region-keyed axis with a measurable, repeatable direction.
            Assert.That(ventilationOff.VentilationRegionSmokeKg, Is.GreaterThan(allOpen.VentilationRegionSmokeKg),
                "disabling ventilation must retain smoke in " + VentilationProbeRegion);
            Assert.That(ventilationOff.VentilationRegionLowerTemperatureK, Is.GreaterThan(allOpen.VentilationRegionLowerTemperatureK),
                "disabling ventilation must stop the ambient supply that cools the lower layer of " + VentilationProbeRegion);
            Assert.That(ventilationOff.VentilationRegionUpperTemperatureK,
                Is.Not.EqualTo(allOpen.VentilationRegionUpperTemperatureK), "the commanded ventilation state must move the zone temperature, not only the smoke tracer");

            var receipt = new ResidualReceipt { Cells = domain.Network.Cells.Length, Openings = domain.Network.Doors.Length,
                Fans = domain.Network.Fans.Length, Runs = runs.Count, MassBudgetKg = MassResidualBudgetKg,
                EnergyBudgetJ = EnergyResidualBudgetJ, PressureBudgetPa = PressureResidualBudgetPa,
                BoundaryRuns = runs.Values.Select(RunReceipt).ToArray(), Limits = BoundaryLimits().ToArray() };
            WriteReceipt("fmp06b-boundary-response.json", "FMP06B-RESIDUALS", receipt);
        }

        [Test]
        public void MassEnergyAndNonNegativePreservationHoldOnEveryCommandedBoundaryState()
        {
            foreach (var run in runs.Values)
            {
                Assert.That(run.MassBalanceErrorKg, Is.LessThanOrEqualTo(MassResidualBudgetKg), run.Signature + " mass balance residual");
                Assert.That(run.EnergyBalanceErrorJ, Is.LessThanOrEqualTo(EnergyResidualBudgetJ), run.Signature + " energy balance residual");
                Assert.That(run.MaximumCommittedPressureResidualPa, Is.LessThanOrEqualTo(PressureResidualBudgetPa), run.Signature + " committed pressure residual");
                Assert.That(run.NonNegative, Is.True, run.Signature + " non-negative state");
                Assert.That(run.Final.ReleasedHeatJ, Is.GreaterThan(0), run.Signature + " released heat ledger");
                Assert.That(run.Final.WallLossJ >= 0 && run.Final.RadiationEscapedJ >= 0 && run.Final.ExternalSmokeKg >= 0,
                    Is.True, run.Signature + " loss and smoke ledgers");
                Assert.That(run.AcceptedSubsteps, Is.GreaterThan(0), run.Signature + " accepted substeps");
            }
        }

        [Test]
        public void PassageCellOwnershipTransfersToTheStationFrameForStaticBoardingPortals()
        {
            var transferred = new List<string>();
            foreach (var portal in world.Portals)
            {
                var owned = domain.Bindings.Where(b => b.PortalId == portal.Id).ToArray();
                if (owned.Length == 0) continue;
                var expected = portal.StaticBoarding
                    ? (world.Region(portal.From).FrameId == "world" ? portal.From : portal.To)
                    : portal.From;
                Assert.That(owned.All(b => b.RegionId == expected), Is.True, portal.Id + " passage cell ownership");
                Assert.That(owned.All(b => b.FrameId == "world"), Is.True, portal.Id + " passage cells resolve in the stationary frame");
                Assert.That(owned.Select(b => b.CellId).Distinct().Count(), Is.EqualTo(owned.Length), portal.Id + " one binding per passage cell");
                foreach (var binding in owned)
                {
                    var cell = domain.Network.Cells.Single(c => c.Id == binding.CellId);
                    Assert.That(binding.Size.X, Is.EqualTo((float)cell.WidthM), portal.Id + " binding width must not change under the transfer");
                    Assert.That(binding.Size.Y, Is.EqualTo((float)cell.HeightM), portal.Id + " binding height must not change under the transfer");
                    Assert.That(binding.Size.Z, Is.EqualTo((float)cell.DepthM), portal.Id + " binding depth must not change under the transfer");
                }
                if (!portal.StaticBoarding) continue;
                transferred.Add(portal.Id);
                Assert.That(expected, Is.Not.EqualTo(portal.From), portal.Id + " must hand ownership to the station-side region");
                var along = new Point3(portal.FromPoint.X + (portal.ToPoint.X - portal.FromPoint.X) * .25f,
                    portal.FromPoint.Y + (portal.ToPoint.Y - portal.FromPoint.Y) * .25f,
                    portal.FromPoint.Z + (portal.ToPoint.Z - portal.FromPoint.Z) * .25f);
                var pose = world.Pose(expected, along, portal.Id);
                var resolved = domain.CellAt(pose);
                Assert.That(owned.Any(b => b.CellId == resolved), Is.True, portal.Id + " transferred passage cell must still resolve through its portal pose");
                var vehicleLocal = world.Frame(world.Region(portal.From).FrameId).ToLocal(along);
                Assert.That(TryResolve(portal.From, vehicleLocal), Is.Null, portal.Id + " the vehicle region must not claim the transferred passage volume");
            }
            Assert.That(transferred.Count, Is.EqualTo(2), "the profile carries exactly two static-boarding vehicle portals");
            Assert.That(domain.FanBindings.Where(b => b.RegionId == "rail_platforms_mainline").Count(), Is.GreaterThan(0),
                "ventilation stays attributed to the station-side cells that own the transferred volume");
        }

        private static string TryResolve(string regionId, Point3 local)
        {
            try { return domain.CellAt(regionId, local); }
            catch (ArgumentException) { return null; }
        }

        [Test]
        public void DuplicateAndInvalidOpeningsAreRejectedOrDroppedAndNeverSilentlyCounted()
        {
            // (a) The shipped authored domain must not carry the degenerate sub-door of a zero-sill closure.
            Assert.That(domain.Network.Doors.Any(d => d.Id.EndsWith("-below", StringComparison.Ordinal)), Is.False,
                "a ClosureBottom of zero yields no below-sill opening; it must be dropped at authoring, not authored as zero height");
            Assert.That(domain.Network.Doors.All(d => d.WidthM > 0 && d.HeightM > 0), Is.True, "no degenerate opening may survive authoring");
            Assert.That(domain.Network.Cells.Any(c => c.Id.StartsWith("passage-", StringComparison.Ordinal)), Is.True);

            // (b) Every authored aperture must fit inside its own vertical wall, so no clamp can hide a duplicate.
            foreach (var cell in domain.Network.Cells.Where(c => c.WallOpenings.Length > 0))
            {
                Assert.That(cell.WallOpenings.Sum(o => o.WidthM), Is.LessThanOrEqualTo(cell.VerticalWallLengthM + 1e-8), cell.Id + " aperture widths");
                Assert.That(cell.WallOpenings.All(o => o.BottomM + o.HeightM <= cell.HeightM + 1e-8), Is.True, cell.Id + " aperture heights");
                Assert.That(cell.WallOpenings.Select(o => o.Id).Distinct().Count(), Is.EqualTo(cell.WallOpenings.Length), cell.Id + " duplicate aperture id");
            }

            // (c) The solver rejects duplicated identifiers instead of merging them.
            Assert.DoesNotThrow(() => new ZoneFireModel(ValidNetwork()));
            AssertRejected(DuplicateCellNetwork(), "Duplicate fire compartment");
            AssertRejected(DuplicateDoorNetwork(), "Duplicate fire flow identifier");
            AssertRejected(DuplicateFanNetwork(), "Duplicate fire flow identifier");

            // (d) The solver rejects openings that do not lie inside their compartment.
            AssertRejected(OutOfCompartmentNetwork(-1, 2), "Opening lies outside a compartment");
            AssertRejected(OutOfCompartmentNetwork(0, 4), "Opening lies outside a compartment");
            AssertRejected(SelfOpeningNetwork(), "Invalid fire opening");
            AssertRejected(NarrowOpeningNetwork(), "Invalid fire opening");
            AssertRejected(FractionOutOfRangeNetwork(), "Invalid fire opening");

            // (e) Duplicated apertures that would over-subtract the real wall are refused, never clamped.
            var doubled = ValidNetwork();
            doubled.Cells[0].WallOpenings = new[] {
                new FireWallOpening { Id = "p1", WidthM = 3, BottomM = 0, HeightM = 2 },
                new FireWallOpening { Id = "p2", WidthM = 3, BottomM = 0, HeightM = 2 } };
            AssertRejected(doubled, "Invalid fire compartment");

            // (f) The authoring path refuses a domain whose axial cells would bisect a doorway.
            var tooFine = Assert.Throws<ArgumentException>(() => ConnectedThermalDomain.Create(World(),
                new ConnectedThermalProfile { MaximumCellLengthM = 1 }));
            Assert.That(tooFine.Message, Does.Contain("bisect a doorway"));
            Assert.Throws<ArgumentException>(() => ConnectedThermalDomain.Create(World(), new ConnectedThermalProfile { MaximumCellLengthM = 0 }));
            Assert.Throws<ArgumentException>(() => ConnectedThermalDomain.Create(World(), new ConnectedThermalProfile { MaximumRampRisePerCellM = 2 }));

            // (g) Duplicate or unknown forcing controls are refused rather than resolved first-wins.
            var fire = new ZoneFireModel(domain.Network, options: domain.SolverOptions);
            var valid = Forcing(domain);
            Assert.DoesNotThrow(() => fire.TryAdvance(WorldTickSeconds, valid, out _));
            Assert.That(ThrowsForcing(fire, DuplicateControlForcing(duplicateDoor: true)), Is.True, "duplicate door control");
            Assert.That(ThrowsForcing(fire, DuplicateControlForcing(duplicateDoor: false)), Is.True, "duplicate fan control");
            Assert.That(ThrowsForcing(fire, UnknownDoorForcing()), Is.True, "unknown door control");
            Assert.That(ThrowsForcing(fire, RangeForcing(1.5)), Is.True, "opening fraction above one");
            Assert.That(ThrowsForcing(fire, RangeForcing(-.5)), Is.True, "negative opening fraction");

            // (h) Interval guards.
            Assert.Throws<ArgumentException>(() => fire.TryAdvance(0, valid, out _));
            Assert.Throws<ArgumentException>(() => fire.TryAdvance(-1, valid, out _));
            Assert.Throws<ArgumentException>(() => fire.TryAdvance(3601, valid, out _));
        }

        private static void AssertRejected(FireNetworkDefinition definition, string message)
        {
            var error = Assert.Throws<ArgumentException>(() => new ZoneFireModel(definition));
            Assert.That(error.Message, Does.Contain(message));
        }

        private static bool ThrowsForcing(ZoneFireModel fire, FireForcing forcing)
        {
            try { fire.TryAdvance(WorldTickSeconds, forcing, out _); return false; }
            catch (ArgumentException) { return true; }
        }

        private static FireNetworkDefinition ValidNetwork() => new FireNetworkDefinition {
            Cells = new[] { TestCell("a", 4), TestCell("b", 4) },
            Doors = new[] { new FireDoorDefinition { Id = "d", FromCellId = "a", ToCellId = "b", WidthM = 1, HeightM = 2, BottomElevationM = 0 } },
            Fans = new[] { new FireFanDefinition { Id = "f", FromCellId = "a", ToCellId = "b", MassFlowKgPerSecond = .01 } } };

        private static FireCellDefinition TestCell(string id, double wallLength) => new FireCellDefinition { Id = id, WidthM = 2, DepthM = 2,
            HeightM = 3, FloorElevationM = 0, UseExplicitWallAreas = true, FloorWallAreaM2 = 4, CeilingWallAreaM2 = 4,
            VerticalWallLengthM = wallLength, VerticalWallHeightM = 3 };

        private static FireNetworkDefinition DuplicateCellNetwork()
        { var network = ValidNetwork(); network.Cells = network.Cells.Concat(new[] { TestCell("a", 4) }).ToArray(); return network; }

        private static FireNetworkDefinition DuplicateDoorNetwork()
        { var network = ValidNetwork(); network.Doors = network.Doors.Concat(new[] { network.Doors[0].Copy() }).ToArray(); return network; }

        private static FireNetworkDefinition DuplicateFanNetwork()
        { var network = ValidNetwork(); network.Fans = network.Fans.Concat(new[] { new FireFanDefinition { Id = "f", FromCellId = "a", ToCellId = "b", MassFlowKgPerSecond = .01 } }).ToArray(); return network; }

        private static FireNetworkDefinition OutOfCompartmentNetwork(double bottom, double height)
        { var network = ValidNetwork(); network.Doors[0].BottomElevationM = bottom; network.Doors[0].HeightM = height; return network; }

        private static FireNetworkDefinition SelfOpeningNetwork()
        { var network = ValidNetwork(); network.Doors[0].ToCellId = "a"; return network; }

        private static FireNetworkDefinition NarrowOpeningNetwork()
        { var network = ValidNetwork(); network.Doors[0].WidthM = 0; return network; }

        private static FireNetworkDefinition FractionOutOfRangeNetwork()
        { var network = ValidNetwork(); network.Doors[0].OpeningFraction = 1.5; return network; }

        private static FireForcing DuplicateControlForcing(bool duplicateDoor)
        {
            var forcing = Forcing(domain);
            if (duplicateDoor) forcing.Doors = forcing.Doors.Concat(new[] { new FireDoorSetting { DoorId = forcing.Doors[0].DoorId, OpeningFraction = 1 } }).ToArray();
            else forcing.Fans = forcing.Fans.Concat(new[] { new FireFanSetting { FanId = forcing.Fans[0].FanId, Enabled = true } }).ToArray();
            return forcing;
        }

        private static FireForcing UnknownDoorForcing()
        {
            var forcing = Forcing(domain);
            forcing.Doors = forcing.Doors.Concat(new[] { new FireDoorSetting { DoorId = "no-such-opening", OpeningFraction = 1 } }).ToArray();
            return forcing;
        }

        private static FireForcing RangeForcing(double fraction)
        {
            var forcing = Forcing(domain);
            forcing.Doors[0].OpeningFraction = fraction;
            return forcing;
        }

        [Test]
        public void SlopedVolumeAndLayerBoundariesStayExactOnAuthoredRamps()
        {
            var ramps = domain.Network.Cells.Where(c => c.FloorRiseM > 0).ToArray();
            Assert.That(ramps, Is.Not.Empty, "the authored profile carries sloped passages");
            Assert.That(ramps.All(c => c.UseExplicitWallAreas && c.WallOpenings.Length == 0), Is.True,
                "a sloped cell exchanges through its explicit sloped faces, never an authored vertical aperture");
            Assert.That(ramps.All(c => c.FloorRiseM <= c.HeightM / 2 + 1e-12), Is.True, "authored rise inside the compartment half-height");

            foreach (var cell in ramps)
            {
                var clearance = cell.HeightM - cell.FloorRiseM;
                Assert.That(cell.VolumeM3, Is.EqualTo(cell.WidthM * cell.DepthM * clearance).Within(1e-9), cell.Id + " sloped volume");
                Assert.That(clearance, Is.GreaterThan(0), cell.Id + " constant vertical clearance");
                // Every unit of authored vertical wall is assigned to exactly one layer at every interface height.
                Assert.That(cell.WallAreaBetween(0, cell.HeightM),
                    Is.EqualTo(cell.VerticalWallLengthM * cell.VerticalWallHeightM).Within(1e-9), cell.Id + " wall area fully assigned");
                // FilledHeight returns a volume per unit floor area (that is what WallAreaBetween multiplies
                // by the wall length), so the inverse identity carries the floor area. The 1x1 hand-built
                // cells of SlopedFireCellTests hide this factor because there area == 1; authored ramp cells
                // are 17.92 m2 and expose it.
                var floorArea = cell.WidthM * cell.DepthM;
                for (var i = 0; i <= 20; i++)
                {
                    // VolumeM3 is a computed property, so V*20/20 does not round back to V; the endpoints of
                    // the sweep are taken exactly instead of as V - V*i/20, which would hand the solver a
                    // one-ulp-negative upper volume and trip its own compartment guard.
                    var lower = i == 20 ? cell.VolumeM3 : cell.VolumeM3 * i / 20.0;
                    var upper = i == 20 ? 0.0 : cell.VolumeM3 - lower;
                    var height = cell.HeightForLowerVolume(lower, upper);
                    Assert.That(cell.FilledHeight(height, clearance) * floorArea, Is.EqualTo(lower).Within(1e-9), cell.Id + " layer volume inverse");
                    Assert.That(cell.WallAreaBetween(0, height) + cell.WallAreaBetween(height, cell.HeightM),
                        Is.EqualTo(cell.WallAreaBetween(0, cell.HeightM)).Within(1e-9), cell.Id + " wall area partition");
                    Assert.That(cell.LayerSurfaceArea(height, true) + cell.LayerSurfaceArea(height, false),
                        Is.EqualTo(cell.SurfaceAreaM2).Within(1e-9), cell.Id + " layer surface partition");
                }
                Assert.That(cell.LayerSurfaceArea(cell.HeightM / 2, false), Is.GreaterThan(0), cell.Id);
                Assert.That(cell.HeightForLowerVolume(cell.VolumeM3, 0), Is.EqualTo(cell.HeightM).Within(1e-9), cell.Id + " full-slope limit");
            }

            // The authored ramp budget of each portal must reproduce the portal's own vertical drop and its
            // true sloped floor length - no invented rise and no lost area at the cut boundaries.
            foreach (var group in domain.Bindings.Where(b => b.PortalId != "").GroupBy(b => b.PortalId))
            {
                var portal = world.Portals.Single(p => p.Id == group.Key);
                var cells = group.Select(b => domain.Network.Cells.Single(c => c.Id == b.CellId)).ToArray();
                // Mirror ConnectedThermalDomain.Create exactly: point differences stay float, the squared
                // terms are promoted to double before the root.
                var dx = portal.ToPoint.X - portal.FromPoint.X; var dy = portal.ToPoint.Y - portal.FromPoint.Y;
                var dz = portal.ToPoint.Z - portal.FromPoint.Z;
                var horizontal = Math.Sqrt((double)dx * dx + (double)dz * dz);
                var sloped = Math.Sqrt(horizontal * horizontal + (double)dy * dy);
                Assert.That(cells.Sum(c => c.FloorRiseM), Is.EqualTo(Math.Abs(dy)).Within(1e-6), group.Key + " authored ramp rise budget");
                Assert.That(cells.Sum(c => c.FloorWallAreaM2), Is.EqualTo(sloped * portal.ClearWidth).Within(1e-6),
                    group.Key + " sloped floor area must equal the true 3D portal length times clear width");
                Assert.That(cells.Sum(c => c.WidthM), Is.EqualTo(horizontal).Within(1e-6), group.Key + " axial coverage");
            }
        }

        [Test]
        public void LongCorridorsAndVehicleBoundariesStayInsideTheAuthoredDiscretizationLimits()
        {
            var limits = ProbeLimits();
            Assert.That(limits.Any(l => l.StartsWith("connector axial cells=", StringComparison.Ordinal)), Is.True);
            Assert.That(domain.Network.Cells.All(c => c.WidthM <= 10.00001), Is.True, "authored maximum cell length is 10 m");
            Assert.That(domain.Network.Cells.Where(c => c.FloorRiseM > 0).All(c => c.FloorRiseM <= .5000001), Is.True,
                "authored maximum ramp rise per cell is 0.5 m");

            // The 99.6 m operator-published corridor is split axially without losing length or volume.
            var corridor = world.Region(VentilationProbeRegion);
            var corridorCells = RegionCells(domain, corridor.Id).Select(i => domain.Network.Cells[i]).ToArray();
            Assert.That(corridorCells.Sum(c => c.WidthM), Is.EqualTo((double)corridor.SizeX).Within(1e-6), "corridor axial coverage");
            Assert.That(corridorCells.Sum(c => c.VolumeM3),
                Is.EqualTo((double)corridor.SizeX * corridor.SizeZ * corridor.Height).Within(1e-3), "corridor volume");
            Assert.That(corridorCells.Length, Is.GreaterThan(1), "a 99.6 m corridor is not a single well-mixed cell");

            // Vehicle boundaries keep their authored envelope and their static-boarding coupling.
            foreach (var vehicle in world.Regions.Where(r => r.Template == "vehicle"))
            {
                var cells = RegionCells(domain, vehicle.Id).Select(i => domain.Network.Cells[i]).ToArray();
                Assert.That(cells, Is.Not.Empty, vehicle.Id);
                Assert.That(cells.All(c => c.DepthM == vehicle.SizeZ && c.HeightM == vehicle.Height &&
                    c.FloorElevationM == vehicle.Center.Y), Is.True, vehicle.Id + " vehicle envelope");
                Assert.That(cells.Sum(c => c.WidthM), Is.EqualTo((double)vehicle.SizeX).Within(1e-6), vehicle.Id + " vehicle length");
                var portals = world.Portals.Where(p => p.From == vehicle.Id || p.To == vehicle.Id).ToArray();
                Assert.That(portals.All(p => p.StaticBoarding), Is.True, vehicle.Id + " vehicle boundaries are static-boarding couplings");
                Assert.That(portals.All(p => p.LinkedDoorEntityIds.Length > 0), Is.True, vehicle.Id + " vehicle boundaries carry a controlled leaf");
            }

            // A substep-budget exhaustion must fail atomically and report the limit; no partial tick is published.
            var bounded = new ZoneFireModel(domain.Network, options: new FireSolverOptions { MaxStepSeconds = .05, MaxSubsteps = 1 });
            var before = bounded.ExportState();
            var forced = Forcing(domain);
            Assert.That(bounded.TryAdvance(.5, forced, out var report), Is.False, "a one-substep budget cannot integrate half a second");
            Assert.That(report.Failure, Does.Contain("no state committed"));
            var after = bounded.ExportState();
            Assert.That(after.SimulatedSeconds, Is.EqualTo(before.SimulatedSeconds), "a failed tick must not advance time");
            Assert.That(after.Cells.Select(c => c.UpperMassKg).ToArray(), Is.EqualTo(before.Cells.Select(c => c.UpperMassKg).ToArray()));
            Assert.That(after.Cells.Select(c => c.WallEnergyJ).ToArray(), Is.EqualTo(before.Cells.Select(c => c.WallEnergyJ).ToArray()));
            Assert.That(after.Cells.Select(c => c.UpperSmokeKg).ToArray(), Is.EqualTo(before.Cells.Select(c => c.UpperSmokeKg).ToArray()));
            Assert.That(bounded.TryAdvance(WorldTickSeconds, forced, out var retry), Is.False, "the exhausted model stays unusable, it does not silently resume");
            Assert.That(retry.Failure, Is.Not.Empty);

            var receipt = new ResidualReceipt { Cells = domain.Network.Cells.Length, Openings = domain.Network.Doors.Length,
                Fans = domain.Network.Fans.Length, Runs = runs.Count, MassBudgetKg = MassResidualBudgetKg,
                EnergyBudgetJ = EnergyResidualBudgetJ, PressureBudgetPa = PressureResidualBudgetPa,
                BoundaryRuns = runs.Values.Select(RunReceipt).ToArray(), Limits = limits.ToArray() };
            WriteReceipt("fmp06b-boundary-limits.json", "FMP06B-LIMITS", receipt);
        }

        [Test]
        public void StepCapRefinementConvergesOnTheBoundaryProbe()
        {
            var caps = new[] { .01, .005, .0025 };
            var smoke = new double[caps.Length]; var sourceSmoke = new double[caps.Length];
            var upper = new double[caps.Length]; var lower = new double[caps.Length];
            var flux = new double[caps.Length]; var residuals = new double[caps.Length]; var accepted = new int[caps.Length];
            var outlet = new double[caps.Length];
            for (var i = 0; i < caps.Length; i++)
            {
                var fire = new ZoneFireModel(domain.Network, options: new FireSolverOptions { MaxStepSeconds = caps[i] });
                var forcing = Forcing(domain);
                var initial = fire.ExportState();
                for (var tick = 0; tick < ConvergenceTicks; tick++)
                    Assert.That(fire.TryAdvance(WorldTickSeconds, forcing, out var step), Is.True, step.Failure);
                var state = fire.ExportState();
                Assert.That(state.SimulatedSeconds, Is.EqualTo(ConvergenceTicks * WorldTickSeconds).Within(1e-9), "cap " + caps[i]);
                smoke[i] = RegionSmokeKg(state, domain, DoorProbeSinkRegion);
                sourceSmoke[i] = RegionSmokeKg(state, domain, DoorProbeSourceRegion);
                upper[i] = RegionUpperTemperatureK(fire, domain, DoorProbeSourceRegion);
                lower[i] = RegionLowerTemperatureK(fire, domain, VentilationProbeRegion);
                flux[i] = BoundaryFluxKgPerSecond(fire, domain, transomDoorId, forcing);
                outlet[i] = state.ExternalMassKg;
                residuals[i] = Math.Abs((CellMass(state) - CellMass(initial)) - state.ExternalMassKg);
                accepted[i] = (int)state.AcceptedSubsteps;
            }
            var line = "worldTicks=" + ConvergenceTicks + "; caps=" + string.Join("|", caps.Select(c => c.ToString()).ToArray()) +
                "; sinkSmokeKg=" + string.Join("|", smoke.Select(v => v.ToString("R")).ToArray()) +
                "; sourceSmokeKg=" + string.Join("|", sourceSmoke.Select(v => v.ToString("R")).ToArray()) +
                "; sourceUpperK=" + string.Join("|", upper.Select(v => v.ToString("R")).ToArray()) +
                "; ventLowerK=" + string.Join("|", lower.Select(v => v.ToString("R")).ToArray()) +
                "; transomFluxKgPerSecond=" + string.Join("|", flux.Select(v => v.ToString("R")).ToArray()) +
                "; externalMassKg=" + string.Join("|", outlet.Select(v => v.ToString("R")).ToArray()) +
                "; massResidualKg=" + string.Join("|", residuals.Select(v => v.ToString("R")).ToArray()) +
                "; acceptedSubsteps=" + string.Join("|", accepted.Select(v => v.ToString()).ToArray());
            // Log before asserting so the measured sweep survives even a failed threshold.
            Directory.CreateDirectory(EvidenceDirectory);
            File.WriteAllText(EvidenceDirectory + "/fmp06b-step-cap-convergence.txt", line);
            Debug.Log("FMP06B-CONVERGENCE " + line);

            // Refinement must actually engage: a 0.0025 s cap forces at least 20 substeps per 0.05 s world tick.
            Assert.That(accepted[2], Is.GreaterThan(accepted[0]), "the finest cap must genuinely refine the integration");
            Assert.That(accepted[2], Is.GreaterThanOrEqualTo(ConvergenceTicks * 20), "the finest cap binds the step");
            Assert.That(accepted[1], Is.GreaterThan(accepted[0]), "each refinement must bind the step more tightly than the last");
            // Zone temperatures are cap-stable in absolute terms.
            Assert.That(Math.Abs(upper[2] - upper[1]), Is.LessThanOrEqualTo(ConvergenceTemperatureToleranceK), "upper zone temperature is cap-stable");
            Assert.That(Math.Abs(lower[2] - lower[1]), Is.LessThanOrEqualTo(ConvergenceTemperatureToleranceK), "lower zone temperature is cap-stable");
            // The transom boundary flux is cap-stable in relative terms. It is a sampled integral of a
            // buoyancy-driven exchange, so absolute agreement is not claimed; the two finest caps must agree
            // to well inside the flux's own magnitude.
            Assert.That(Math.Abs(flux[2] - flux[1]), Is.LessThanOrEqualTo(ConvergenceFluxFraction * Math.Abs(flux[2])),
                "the transom boundary flux must be cap-stable relative to its own magnitude");
            Assert.That(residuals.All(r => r <= MassResidualBudgetKg), Is.True, "mass closure is cap-independent");
            // The transported smoke is negligible against the smoke the source region itself holds at every
            // cap, so it carries no convergence information at this horizon; that is recorded rather than
            // dressed up as a converging quantity.
            var largestSourceSmoke = sourceSmoke.Max();
            Assert.That(largestSourceSmoke, Is.GreaterThan(0), "the source region must hold smoke during the sweep");
            Assert.That(smoke.All(v => v <= 1e-30 * largestSourceSmoke), Is.True,
                "the doorway stays sub-critical for smoke at every probed step cap");
        }

        // ------------------------------------------------------------ receipts ----

        [Serializable]
        private sealed class BoundaryRunReceipt
        {
            public string Signature, Failure = "";
            public int TicksAdvanced, AcceptedSubsteps, RejectedSubsteps;
            public double SimulatedSeconds, MassBalanceErrorKg, EnergyBalanceErrorJ, MaximumCommittedPressureResidualPa;
            public double SinkRegionSmokeKg, SourceRegionSmokeKg, SinkRegionUpperTemperatureK, SourceRegionUpperTemperatureK;
            public double VentilationRegionSmokeKg, VentilationRegionLowerTemperatureK, VentilationRegionUpperTemperatureK;
            public double LeafBoundaryFluxKgPerSecond, TransomBoundaryFluxKgPerSecond, BoundaryNetFluxKgPerSecond;
            public bool NonNegative, AdvanceSucceeded;
        }

        [Serializable]
        private sealed class MatrixReceipt
        {
            public int SchemaVersion = 1;
            public string WorkId = "FMP-06b";
            public string Classification = "PUBLIC_PROJECT_EVIDENCE";
            public int Cells, Openings, Fans, Regions, Portals;
            public int MatrixRows, SupportedRows, UnsupportedRows, PassCountedRows, ExecutedRuns;
            public string DoorProbePortalId, DoorProbeSourceRegion, DoorProbeSinkRegion, VentilationProbeRegion, ProbeEvidence;
            public MatrixCell[] Matrix;
            public string[] Distinctions;
            public string[] UnsupportedAxes;
        }

        [Serializable]
        private sealed class ResidualReceipt
        {
            public int SchemaVersion = 1;
            public string WorkId = "FMP-06b";
            public int Cells, Openings, Fans, Runs;
            public double MassBudgetKg, EnergyBudgetJ, PressureBudgetPa;
            public BoundaryRunReceipt[] BoundaryRuns;
            public string[] Limits;
        }

        private static BoundaryRunReceipt RunReceipt(BoundaryRun run) => new BoundaryRunReceipt {
            Signature = run.Signature, TicksAdvanced = run.TicksAdvanced, AcceptedSubsteps = run.AcceptedSubsteps,
            RejectedSubsteps = run.RejectedSubsteps, SimulatedSeconds = run.Final.SimulatedSeconds,
            MassBalanceErrorKg = run.MassBalanceErrorKg, EnergyBalanceErrorJ = run.EnergyBalanceErrorJ,
            MaximumCommittedPressureResidualPa = run.MaximumCommittedPressureResidualPa,
            SinkRegionSmokeKg = run.SinkRegionSmokeKg, SourceRegionSmokeKg = run.SourceRegionSmokeKg,
            SinkRegionUpperTemperatureK = run.SinkRegionUpperTemperatureK, SourceRegionUpperTemperatureK = run.SourceRegionUpperTemperatureK,
            VentilationRegionSmokeKg = run.VentilationRegionSmokeKg, VentilationRegionLowerTemperatureK = run.VentilationRegionLowerTemperatureK,
            VentilationRegionUpperTemperatureK = run.VentilationRegionUpperTemperatureK,
            LeafBoundaryFluxKgPerSecond = run.LeafBoundaryFluxKgPerSecond, TransomBoundaryFluxKgPerSecond = run.TransomBoundaryFluxKgPerSecond,
            BoundaryNetFluxKgPerSecond = run.BoundaryNetFluxKgPerSecond, NonNegative = run.NonNegative,
            AdvanceSucceeded = run.AdvanceSucceeded, Failure = run.Failure };

        private static void WriteReceipt(string fileName, string marker, object receipt)
        {
            var json = JsonUtility.ToJson(receipt, true);
            Directory.CreateDirectory(EvidenceDirectory);
            File.WriteAllText(EvidenceDirectory + "/" + fileName, json);
            // Unity clears Temp/ on exit, so the receipt must also survive in the run log.
            Debug.Log(marker + " " + json);
        }

        private static List<string> BoundaryLimits()
        {
            var limits = new List<string>(ProbeLimits()) {
                "Door matrix probe portal: " + DoorProbePortalId + " (authored ClosureAlong 0.5, ClosureBottom 0, ClosureHeight 2.3 in a 2.7 m clear opening, StaticBoarding true).",
                "Whole-server 20 Hz frame budget is NOT_ASSESSED here; this work item records boundary responses and residuals only.",
                "The reduced two-zone network is not CFD, not a leak-area measurement and not a certified life-safety calculation." };
            return limits;
        }

        private static List<string> ProbeLimits()
        {
            var limits = new List<string>();
            var corridor = world.Region(VentilationProbeRegion);
            var corridorCells = RegionCells(domain, corridor.Id).Select(i => domain.Network.Cells[i]).ToArray();
            limits.Add("connector axial cells=" + corridorCells.Length + "; lengthM=" + corridor.SizeX + "; maxCellM=" +
                (corridorCells.Length == 0 ? 0 : corridorCells.Max(c => c.WidthM)));
            foreach (var group in domain.Bindings.Where(b => b.PortalId != "").GroupBy(b => b.PortalId))
            {
                var portal = world.Portals.Single(p => p.Id == group.Key);
                var cells = group.Select(b => domain.Network.Cells.Single(c => c.Id == b.CellId)).ToArray();
                var dy = Math.Abs(portal.ToPoint.Y - portal.FromPoint.Y);
                if (dy > 0)
                    limits.Add("ramp portal " + group.Key + ": cells=" + cells.Length + "; authoredRiseM=" +
                        cells.Sum(c => c.FloorRiseM) + "; dropM=" + dy + "; maxRisePerCellM=" + cells.Max(c => c.FloorRiseM));
            }
            limits.Add("probe portal " + DoorProbePortalId + ": " + probe.PortalDoorCount + " authored apertures, " + probe.ControlledDoorCount +
                " controlled, " + probe.AlwaysOpenDoorCount + " unconditional; minimum simultaneously open = " + probe.MinimumOpenDoors + ".");
            limits.Add("long corridor " + VentilationProbeRegion + ": cells=" + corridorCells.Length + "; lengthM=" + corridor.SizeX +
                "; volumeM3=" + corridorCells.Sum(c => c.VolumeM3) + "; widthM=" + corridor.SizeZ + "; heightM=" + corridor.Height +
                "; maxCellM=" + (corridorCells.Length == 0 ? 0 : corridorCells.Max(c => c.WidthM)));
            foreach (var vehicle in world.Regions.Where(r => r.Template == "vehicle"))
            {
                var vehicleCells = RegionCells(domain, vehicle.Id).Select(i => domain.Network.Cells[i]).ToArray();
                var portals = world.Portals.Where(p => p.From == vehicle.Id || p.To == vehicle.Id).ToArray();
                var doors = domain.DoorBindings.Where(b => portals.Any(p => p.Id == b.PortalId))
                    .Select(b => domain.Network.Doors.Single(d => d.Id == b.DoorId)).ToArray();
                var gated = domain.DoorBindings.Count(b => portals.Any(p => p.Id == b.PortalId) && b.BoardingCoupling);
                var controlled = domain.DoorBindings.Count(b => portals.Any(p => p.Id == b.PortalId) && b.Controlled);
                limits.Add("vehicle boundary " + vehicle.Id + ": cells=" + vehicleCells.Length + "; axialCoverageM=" + vehicleCells.Sum(c => c.WidthM) +
                    "; volumeM3=" + vehicleCells.Sum(c => c.VolumeM3) + "; portal=" + string.Join(",", portals.Select(p => p.Id).ToArray()) +
                    "; apertures=" + doors.Length + " (controlled=" + controlled + ", boardingGated=" + gated + ")" +
                    "; minApertureM2=" + (doors.Length == 0 ? 0 : doors.Min(d => d.WidthM * d.HeightM)) +
                    "; maxApertureM2=" + (doors.Length == 0 ? 0 : doors.Max(d => d.WidthM * d.HeightM)) +
                    "; staticBoarding=" + string.Join(",", portals.Select(p => p.StaticBoarding.ToString()).ToArray()));
            }
            limits.Add("authored cell count=" + domain.Network.Cells.Length + " of the 256-compartment solver limit.");
            return limits;
        }
    }
}
