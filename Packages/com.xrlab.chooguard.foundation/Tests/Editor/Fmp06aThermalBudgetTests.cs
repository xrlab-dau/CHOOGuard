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
    /// FMP-06a (#122): thermal/smoke tick cost on the authored 147-cell / 275-opening connected domain.
    ///
    /// Fixed acceptance conditions (changing any of them invalidates the recorded comparison):
    /// the derived domain has 147 cells and 275 openings, two prescribed 60 kW sources, and 100 world
    /// ticks of 0.05 s (5.0 simulated seconds).
    ///
    /// The p95 measured here is a THERMAL-ONLY figure. It does not certify the whole 20 Hz server frame;
    /// that budget stays NOT_ASSESSED.
    ///
    /// Baseline vs optimized uses one axis only - the solver's cached derived opening-slab path
    /// (<see cref="FireSolverOptions.CacheOpeningSlabs"/>) - because it is the cost optimization the
    /// thermal core already carries. Cells, openings, sources, tick length and step cap are identical on
    /// both sides, so no spatial or temporal discretization change is mixed into the comparison.
    /// </summary>
    public sealed class Fmp06aThermalBudgetTests
    {
        private const int FixedCellCount = 147;
        private const int FixedOpeningCount = 275;
        private const int WorldTickCount = 100;
        private const double WorldTickSeconds = .05;
        private const double FixedSimulatedSeconds = WorldTickCount * WorldTickSeconds;
        private const double ThermalP95BudgetMs = 50.0;
        private const double SourceHeatReleaseW = 60000;
        private const double MassResidualBudgetKg = 1e-3;
        private const double EnergyResidualBudgetJ = .005;
        private const string EvidenceDirectory = "Temp/ChooGuardThermalDomain";

        private static readonly string[] SourceRegions = { "rolling_stock_mainline", "underground_connector" };

        private static ConnectedWorldDefinition World() => JsonUtility.FromJson<ConnectedWorldDefinition>(
            File.ReadAllText("foundation/world/connected-world-profile.json"));

        private static ConnectedThermalDomain Fixture() =>
            ConnectedThermalDomain.Create(World(), new ConnectedThermalProfile());

        private static FireForcing Forcing(ConnectedThermalDomain domain) => domain.Forcing(
            SourceRegions.Select(region => new FireSourcePower {
                CellId = domain.Bindings.First(b => b.RegionId == region && b.PortalId == "").CellId,
                HeatReleaseW = SourceHeatReleaseW, FuelMassKgPerSecond = .004, SmokeMassKgPerSecond = .0004, HeightM = .5 }).ToArray(),
            _ => true, _ => true, _ => true);

        /// <summary>Represented gas mass. The model's reservoir ledger (<c>ExternalMassKg</c>) is signed
        /// opposite to this sum, so the two are compared as a closure rather than added; see the mass test.</summary>
        private static double CellMass(FireState state) =>
            state.Cells.Sum(c => c.UpperMassKg + c.LowerMassKg);

        private static double CellEnergy(FireState state) =>
            state.Cells.Sum(c => c.UpperEnergyJ + c.LowerEnergyJ + c.WallEnergyJ);

        /// <summary>Nearest-rank percentile over the recorded per-tick durations.</summary>
        private static double Percentile(IEnumerable<double> values, double percentile)
        {
            var sorted = values.OrderBy(value => value).ToArray();
            var rank = (int)Math.Ceiling(percentile * sorted.Length);
            return sorted[Math.Min(sorted.Length - 1, Math.Max(0, rank - 1))];
        }

        private sealed class PassResult
        {
            public double[] TickMs;
            public int AcceptedSubsteps, RejectedSubsteps;
            public double MaximumPressureResidualPa, MaximumCommittedPressureResidualPa;
            public double MassBalanceErrorKg, EnergyBalanceErrorJ;
            public double ExternalMassKg, InjectedFuelKg;
            public double SimulatedSeconds;
            public FireState Initial, Final;
            public bool NonNegative = true;
            public double P95Ms, MeanMs, MaxMs;
            public int Gen0Collections, Gen1Collections, Gen2Collections;
        }

        private static PassResult RunPass(ConnectedThermalDomain domain, FireSolverOptions options)
        {
            var fire = new ZoneFireModel(domain.Network, options: options);
            var initial = fire.ExportState(); var forcing = Forcing(domain);
            var ticks = new double[WorldTickCount];
            var accepted = 0; var rejected = 0; var maximumResidual = 0.0; var maximumCommittedResidual = 0.0;
            var gen0 = GC.CollectionCount(0); var gen1 = GC.CollectionCount(1); var gen2 = GC.CollectionCount(2);
            for (var i = 0; i < WorldTickCount; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var advanced = fire.TryAdvance(WorldTickSeconds, forcing, out var report);
                watch.Stop();
                ticks[i] = watch.Elapsed.TotalMilliseconds;
                Assert.That(advanced, Is.True, report.Failure);
                accepted += report.AcceptedSubsteps; rejected += report.RejectedSubsteps;
                maximumResidual = Math.Max(maximumResidual, report.MaximumPressureResidualPa);
                maximumCommittedResidual = Math.Max(maximumCommittedResidual, report.MaximumCommittedPressureResidualPa);
            }
            var final = fire.ExportState();
            return new PassResult { Initial = initial, Final = final, TickMs = ticks,
                AcceptedSubsteps = accepted, RejectedSubsteps = rejected,
                MaximumPressureResidualPa = maximumResidual, MaximumCommittedPressureResidualPa = maximumCommittedResidual,
                SimulatedSeconds = final.SimulatedSeconds,
                // Independent-ledger closure: the represented gas mass changes by exactly the amount the
                // external reservoir ledger records (plus the prescribed injection, which both record).
                MassBalanceErrorKg = Math.Abs((CellMass(final) - CellMass(initial)) - final.ExternalMassKg),
                EnergyBalanceErrorJ = Math.Abs((CellEnergy(final) - CellEnergy(initial)) - final.ExternalEnergyJ),
                ExternalMassKg = final.ExternalMassKg,
                InjectedFuelKg = SourceRegions.Length * 0.004 * FixedSimulatedSeconds,
                P95Ms = Percentile(ticks, .95), MeanMs = ticks.Average(), MaxMs = ticks.Max(),
                Gen0Collections = GC.CollectionCount(0) - gen0, Gen1Collections = GC.CollectionCount(1) - gen1,
                Gen2Collections = GC.CollectionCount(2) - gen2,
                NonNegative = final.Cells.All(c => c.UpperMassKg >= 0 && c.LowerMassKg >= 0 &&
                        c.UpperSmokeKg >= 0 && c.LowerSmokeKg >= 0 && c.UpperEnergyJ > 0 && c.LowerEnergyJ > 0 && c.WallEnergyJ > 0) &&
                    final.Cells.Select(c => fire.ReadCell(c.CellId)).All(m => m.PressurePa > 0 &&
                        m.UpperTemperatureK > 0 && m.LowerTemperatureK > 0 && m.WallTemperatureK > 0) };
        }

        private static FireSolverOptions ReferenceOptions(ConnectedThermalDomain domain) => new FireSolverOptions {
            MaxStepSeconds = domain.SolverOptions.MaxStepSeconds, CacheOpeningSlabs = false };

        [Test]
        public void FixedFixtureStillReproducesTheRecordedCellAndOpeningBudget()
        {
            var domain = Fixture();
            Assert.That(domain.Network.Cells.Length, Is.EqualTo(FixedCellCount), "authored cell budget");
            Assert.That(domain.Network.Doors.Length, Is.EqualTo(FixedOpeningCount), "authored opening budget");
            Assert.That(SourceRegions.Length, Is.EqualTo(2), "two prescribed fire sources");
            Assert.That(SourceRegions.Length * SourceHeatReleaseW, Is.EqualTo(120000), "2 x 60 kW total heat release");
            Assert.That(domain.Network.Cells.Length, Is.LessThanOrEqualTo(256), "solver compartment limit");
            Assert.DoesNotThrow(() => new ZoneFireModel(domain.Network, options: domain.SolverOptions));
            Assert.That(domain.Network.Cells.All(c => c.VolumeM3 > 0), Is.True);
        }

        [Test]
        public void OneHundredWorldTicksHoldTheFiftyMillisecondThermalBudget()
        {
            var domain = Fixture();
            var baseline = RunPass(domain, ReferenceOptions(domain));    // uncached reference path
            var optimized = RunPass(domain, domain.SolverOptions);        // shipped cached opening-slab path
            var repeat = RunPass(domain, domain.SolverOptions);           // same conditions, fresh model
            Assert.That(baseline.TickMs.Length, Is.EqualTo(WorldTickCount));
            Assert.That(optimized.SimulatedSeconds, Is.EqualTo(FixedSimulatedSeconds).Within(1e-10));
            var receipt = JsonUtility.ToJson(new BudgetReceipt {
                Cells = domain.Network.Cells.Length, Openings = domain.Network.Doors.Length, TickCount = WorldTickCount,
                TickSeconds = WorldTickSeconds, SimulatedSeconds = optimized.SimulatedSeconds, BudgetP95Ms = ThermalP95BudgetMs,
                Baseline = Stats(baseline), Optimized = Stats(optimized), Repeat = Stats(repeat) }, true);
            Directory.CreateDirectory(EvidenceDirectory);
            File.WriteAllText(EvidenceDirectory + "/fmp06a-thermal-budget.json", receipt);
            // Unity clears Temp/ on exit, so the receipt must also survive in the run log.
            Debug.Log("FMP06A-BUDGET " + receipt);
            Assert.That(optimized.P95Ms, Is.LessThanOrEqualTo(ThermalP95BudgetMs), "optimized thermal p95");
            Assert.That(repeat.P95Ms, Is.LessThanOrEqualTo(ThermalP95BudgetMs), "repeat thermal p95");
            Assert.That(baseline.AcceptedSubsteps, Is.GreaterThan(0), "accepted substeps");
        }

        [Test]
        public void MassEnergyAndNonNegativeStateStayInsideTheRecordedResiduals()
        {
            var domain = Fixture();
            var pass = RunPass(domain, domain.SolverOptions);
            Assert.That(pass.MassBalanceErrorKg, Is.LessThanOrEqualTo(MassResidualBudgetKg), "mass balance residual");
            Assert.That(pass.EnergyBalanceErrorJ, Is.LessThanOrEqualTo(EnergyResidualBudgetJ), "energy balance residual");
            Assert.That(pass.NonNegative, Is.True, "non-negative mass, smoke, pressure, temperature and energy");
            Assert.That(pass.MaximumCommittedPressureResidualPa, Is.LessThanOrEqualTo(1e-3), "committed pressure residual");
            Assert.That(pass.Final.ReleasedHeatJ, Is.GreaterThan(0), "released heat ledger");
            Assert.That(pass.Final.WallLossJ >= 0 && pass.Final.RadiationEscapedJ >= 0, Is.True, "loss ledgers");
            var receipt = JsonUtility.ToJson(new ResidualReceipt {
                Cells = domain.Network.Cells.Length, Openings = domain.Network.Doors.Length, TickCount = WorldTickCount,
                MaxPressureResidualPa = pass.MaximumCommittedPressureResidualPa, MassBalanceErrorKg = pass.MassBalanceErrorKg,
                EnergyBalanceErrorJ = pass.EnergyBalanceErrorJ, ExternalMassLedgerKg = pass.ExternalMassKg,
                InjectedFuelKg = pass.InjectedFuelKg, AcceptedSubsteps = pass.AcceptedSubsteps,
                RejectedSubsteps = pass.RejectedSubsteps, NonNegative = pass.NonNegative }, true);
            Directory.CreateDirectory(EvidenceDirectory);
            File.WriteAllText(EvidenceDirectory + "/fmp06a-thermal-residuals.json", receipt);
            Debug.Log("FMP06A-RESIDUALS " + receipt);
        }

        [Test]
        public void ConnectedRoomBoundsAreNotRegressedByTheThermalFixture()
        {
            var world = World(); var domain = Fixture();
            Assert.That(domain.Bindings.Select(b => b.RegionId).Distinct().Count(), Is.EqualTo(world.Regions.Length));
            foreach (var region in world.Regions)
            {
                var cells = domain.Bindings.Where(b => b.RegionId == region.Id && b.PortalId == "")
                    .Select(b => domain.Network.Cells.Single(c => c.Id == b.CellId)).ToArray();
                Assert.That(cells, Is.Not.Empty, region.Id);
                Assert.That(cells.Sum(c => c.VolumeM3),
                    Is.EqualTo((double)region.SizeX * region.SizeZ * region.Height).Within(.001), region.Id + " volume");
                Assert.That(cells.Sum(c => c.WidthM), Is.EqualTo((double)region.SizeX).Within(1e-6), region.Id + " axial coverage");
                Assert.That(cells.All(c => c.WidthM <= 10.00001), Is.True, region.Id + " cell length");
                Assert.That(cells.All(c => c.DepthM == region.SizeZ && c.HeightM == region.Height &&
                    c.FloorElevationM == region.Center.Y), Is.True, region.Id + " authored bounds");
                var expected = (1 + (region.Ceiling ? 1 : 0)) * (double)region.SizeX * region.SizeZ +
                    2.0 * (region.SizeX + region.SizeZ) * region.WallHeight;
                expected -= world.Portals.Where(p => p.From == region.Id || p.To == region.Id)
                    .Sum(p => (double)p.ClearWidth * Math.Min(region.WallHeight, p.ClearHeight));
                Assert.That(cells.Sum(c => c.SurfaceAreaM2), Is.EqualTo(expected).Within(.001), region.Id + " surface area");
            }
            Assert.That(domain.Network.Cells.Length, Is.EqualTo(FixedCellCount));
            Assert.That(domain.Network.Doors.Length, Is.EqualTo(FixedOpeningCount));
        }

        [Serializable] private sealed class PassStats
        {
            public int Cells, Openings, TickCount, AcceptedSubsteps, RejectedSubsteps;
            public int Gen0Collections, Gen1Collections, Gen2Collections;
            public double TickSeconds, SimulatedSeconds, BudgetP95Ms, P95Ms, MeanMs, MaxMs;
            public double[] TickMs;
        }

        [Serializable] private sealed class BudgetReceipt
        {
            public int Cells, Openings, TickCount;
            public double TickSeconds, SimulatedSeconds, BudgetP95Ms;
            public PassStats Baseline, Optimized, Repeat;
        }

        private static PassStats Stats(PassResult pass) => new PassStats {
            Cells = FixedCellCount, Openings = FixedOpeningCount, TickCount = WorldTickCount,
            TickSeconds = WorldTickSeconds, SimulatedSeconds = pass.SimulatedSeconds, BudgetP95Ms = ThermalP95BudgetMs,
            P95Ms = pass.P95Ms, MeanMs = pass.MeanMs, MaxMs = pass.MaxMs, TickMs = pass.TickMs,
            AcceptedSubsteps = pass.AcceptedSubsteps, RejectedSubsteps = pass.RejectedSubsteps,
            Gen0Collections = pass.Gen0Collections, Gen1Collections = pass.Gen1Collections, Gen2Collections = pass.Gen2Collections };

        [Serializable] private sealed class ResidualReceipt
        {
            public int Cells, Openings, TickCount, AcceptedSubsteps, RejectedSubsteps;
            public double MaxPressureResidualPa, MassBalanceErrorKg, EnergyBalanceErrorJ, ExternalMassLedgerKg, InjectedFuelKg;
            public bool NonNegative;
        }
    }
}
