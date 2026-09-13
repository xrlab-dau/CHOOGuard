using System;
using System.IO;
using System.Linq;
using ChooGuard.Foundation.Multiplayer;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    public sealed class ConnectedThermalTests
    {
        private static ConnectedWorldDefinition World() => JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText("foundation/world/connected-world-profile.json"));

        [Test]
        public void SubdivisionPreservesEveryRoomVolumeAndDoesNotInventInternalSolidWalls()
        {
            var world = World(); var domain = ConnectedThermalDomain.Create(world, new ConnectedThermalProfile());
            Assert.That(domain.Bindings.Select(b => b.RegionId).Distinct().Count(), Is.EqualTo(13));
            foreach (var region in world.Regions)
            {
                var cells = domain.Bindings.Where(b => b.RegionId == region.Id && b.PortalId == "")
                    .Select(b => domain.Network.Cells.Single(c => c.Id == b.CellId)).ToArray();
                Assert.That(cells.Sum(c => c.VolumeM3), Is.EqualTo((double)region.SizeX * region.SizeZ * region.Height).Within(.001));
                var expected = (1 + (region.Ceiling ? 1 : 0)) * (double)region.SizeX * region.SizeZ + 2.0 * (region.SizeX + region.SizeZ) * region.WallHeight;
                expected -= world.Portals.Where(p => p.From == region.Id || p.To == region.Id).Sum(p => (double)p.ClearWidth * Math.Min(region.WallHeight, p.ClearHeight));
                Assert.That(cells.Sum(c => c.SurfaceAreaM2), Is.EqualTo(expected).Within(.001));
                Assert.That(cells.All(c => c.WidthM <= 10.00001), Is.True);
            }
            Assert.That(domain.Network.Cells.Length, Is.LessThanOrEqualTo(256));
            Assert.DoesNotThrow(() => new ZoneFireModel(domain.Network));
        }

        [Test]
        public void PortalControlAndBoardingCouplingCloseOnlyTheRepresentedOpening()
        {
            var world = World(); var domain = ConnectedThermalDomain.Create(world, new ConnectedThermalProfile());
            var boarding = world.Portals.Single(p => p.From == "rolling_stock_mainline");
            var from = domain.DoorBindings.Single(b => b.PortalId == boarding.Id && b.BoardingCoupling);
            var forcing = domain.Forcing(Array.Empty<FireSourcePower>(), id => true, id => false, id => true);
            Assert.That(forcing.Doors.Single(d => d.DoorId == from.DoorId).OpeningFraction, Is.Zero);
            Assert.That(domain.DoorBindings.Where(b => b.PortalId == boarding.Id && !b.BoardingCoupling && !b.Controlled)
                .All(b => forcing.Doors.Single(d => d.DoorId == b.DoorId).OpeningFraction == 1), Is.True);
            Assert.That(domain.Bindings.Where(b => b.PortalId == boarding.Id).All(b => b.FrameId == "world"), Is.True);
        }

        [Test]
        public void RefinementPreservesRoomLeakBudgetAndAuthoredOpenSideAreas()
        {
            var world = World(); var coarse = ConnectedThermalDomain.Create(world, new ConnectedThermalProfile());
            var fine = ConnectedThermalDomain.Create(world, new ConnectedThermalProfile { MaximumCellLengthM = 5 });
            foreach (var region in world.Regions)
            {
                double Area(ConnectedThermalDomain domain, string prefix) => domain.Network.Doors
                    .Where(d => d.ToCellId == "" && d.Id.StartsWith(prefix + region.Id + "-", StringComparison.Ordinal)).Sum(d => d.WidthM * d.HeightM);
                Assert.That(Area(coarse, "leak-"), Is.EqualTo(.005 * region.Height).Within(1e-10));
                Assert.That(Area(fine, "leak-"), Is.EqualTo(Area(coarse, "leak-")).Within(1e-10));
                Assert.That(Area(fine, "ambient-"), Is.EqualTo(Area(coarse, "ambient-")).Within(.001));
            }
        }

        [Test]
        public void ControlledPlaneSillAndLintelMatchTheAuthoredPanelAndPreserveTheOpenTransom()
        {
            var world = World(); var domain = ConnectedThermalDomain.Create(world, new ConnectedThermalProfile());
            var portal = world.Portals.Single(p => p.From == "rolling_stock_mainline");
            var binding = domain.DoorBindings.Single(b => b.PortalId == portal.Id && b.Controlled);
            var door = domain.Network.Doors.Single(d => d.Id == binding.DoorId);
            Assert.That(binding.PlanePoint.DistanceSquared(portal.ClosurePoint), Is.LessThan(1e-10));
            Assert.That(door.BottomElevationM, Is.EqualTo(portal.ClosurePoint.Y + portal.ClosureBottom).Within(1e-6));
            Assert.That(door.HeightM, Is.EqualTo(portal.ClosureHeight).Within(1e-6));
            var transom = domain.Network.Doors.Single(d => d.Id == door.Id + "-above");
            Assert.That(transom.HeightM, Is.EqualTo(portal.ClearHeight - portal.ClosureHeight).Within(1e-6));
            Assert.That(domain.Forcing(Array.Empty<FireSourcePower>(), _ => false, _ => true, _ => false).Doors.Single(d => d.DoorId == transom.Id).OpeningFraction, Is.EqualTo(1));
            foreach (var cell in domain.Bindings.Where(b => b.PortalId == portal.Id).Select(b => domain.Network.Cells.Single(c => c.Id == b.CellId)))
            {
                Assert.That(cell.CeilingWallAreaM2, Is.Zero);
                Assert.That(cell.SurfaceAreaM2, Is.EqualTo(cell.WidthM * cell.DepthM + 2 * cell.WidthM * portal.WallHeight).Within(1e-8));
                Assert.That(domain.Network.Doors.Any(d => d.FromCellId == cell.Id && d.ToCellId == ""), Is.True);
            }
        }

        [Test]
        public void SlopedCellVolumesAndEveryControlledSillLintelRemainExactUnderRefinement()
        {
            var world = World();
            foreach (var maximum in new[] { 10.0, 5.0 })
            {
                var domain = ConnectedThermalDomain.Create(world, new ConnectedThermalProfile { MaximumCellLengthM = maximum });
                foreach (var portal in world.Portals)
                {
                    var cells = domain.Bindings.Where(b => b.PortalId == portal.Id).Select(b => domain.Network.Cells.Single(c => c.Id == b.CellId)).ToArray();
                    var dx = (double)portal.ToPoint.X - portal.FromPoint.X; var dz = (double)portal.ToPoint.Z - portal.FromPoint.Z;
                    Assert.That(cells.Sum(c => c.VolumeM3), Is.EqualTo(Math.Sqrt(dx * dx + dz * dz) * portal.ClearWidth * portal.ClearHeight).Within(.001));
                    foreach (var binding in domain.DoorBindings.Where(b => b.PortalId == portal.Id && b.Controlled))
                    {
                        var door = domain.Network.Doors.Single(d => d.Id == binding.DoorId);
                        Assert.That(door.BottomElevationM, Is.EqualTo(portal.ClosurePoint.Y + portal.ClosureBottom).Within(1e-6), portal.Id);
                        Assert.That(door.HeightM, Is.EqualTo(portal.ClosureHeight).Within(1e-6), portal.Id);
                    }
                }
            }
        }

        [Test]
        public void EveryPortalFlowUsesTheCellThatOwnsItsCompleteWallAperture()
        {
            var world = World(); var domain = ConnectedThermalDomain.Create(world, new ConnectedThermalProfile());
            foreach (var portal in world.Portals)
            foreach (var region in new[] { portal.From, portal.To })
            {
                var candidates = domain.Network.Cells.Where(c => c.WallOpenings.Any(o => o.Id == portal.Id) &&
                    domain.Bindings.Single(b => b.CellId == c.Id).RegionId == region).ToArray();
                Assert.That(candidates, Has.Length.EqualTo(1), portal.Id + ":" + region);
                var cell = candidates[0]; var aperture = cell.WallOpenings.Single(o => o.Id == portal.Id);
                Assert.That(aperture.WidthM, Is.EqualTo(portal.ClearWidth).Within(1e-6));
                Assert.That(domain.DoorBindings.Where(b => b.PortalId == portal.Id).Select(b => domain.Network.Doors.Single(d => d.Id == b.DoorId))
                    .Any(d => (d.FromCellId == cell.Id || d.ToCellId == cell.Id) && Math.Abs(d.WidthM - aperture.WidthM) < 1e-6), Is.True);
            }
        }

        [Test]
        public void PoseLookupUsesTheBridgeAndBothHalvesOfAnElevatedPassage()
        {
            var world = World(); var domain = ConnectedThermalDomain.Create(world, new ConnectedThermalProfile());
            foreach (var id in new[] { "rolling_stock_mainline--rail_platforms_mainline", "station_hall_1f--station_concourse_2f" })
            {
                var portal = world.Portals.Single(p => p.Id == id);
                foreach (var t in new[] { .2f, .8f })
                {
                    var point = new Point3(portal.FromPoint.X + (portal.ToPoint.X - portal.FromPoint.X) * t,
                        portal.FromPoint.Y + (portal.ToPoint.Y - portal.FromPoint.Y) * t, portal.FromPoint.Z + (portal.ToPoint.Z - portal.FromPoint.Z) * t);
                    var pose = world.Pose(t < .5f ? portal.From : portal.To, point, id);
                    var cell = domain.CellAt(pose);
                    Assert.That(domain.Bindings.Single(b => b.CellId == cell).PortalId, Is.EqualTo(id));
                }
            }
        }

        [Test]
        public void CachedOpeningGeometryPreservesFullStateAndLedgersWithClosedEdgesAndTracer()
        {
            var d = new FireNetworkDefinition { Cells = Enumerable.Range(0, 4).Select(i => new FireCellDefinition {
                Id = "c" + i, WidthM = 2 + i, DepthM = 3, HeightM = 3, FloorElevationM = i < 2 ? 0 : 1 }).ToArray(),
                Doors = new[] { new FireDoorDefinition { Id = "open", FromCellId = "c0", ToCellId = "c1", WidthM = 2, HeightM = 2 },
                    new FireDoorDefinition { Id = "closed", FromCellId = "c1", ToCellId = "c2", WidthM = 1, HeightM = 1, BottomElevationM = 1 },
                    new FireDoorDefinition { Id = "ambient", FromCellId = "c0", WidthM = .5, HeightM = 2 } } };
            var fast = new ZoneFireModel(d); var direct = new ZoneFireModel(d, options: new FireSolverOptions { CacheOpeningSlabs = false, PreferForestPressureSolver = false });
            var forcing = new FireForcing { Sources = new[] { new FireSourcePower { CellId = "c0", HeatReleaseW = 50000, FuelMassKgPerSecond = .002, SmokeMassKgPerSecond = .0001 } },
                Doors = new[] { new FireDoorSetting { DoorId = "closed", OpeningFraction = 0 } } };
            for (var tick = 0; tick < 10; tick++)
            {
                Assert.That(fast.TryAdvance(.05, forcing, out var a), Is.True, a.Failure);
                Assert.That(direct.TryAdvance(.05, forcing, out var b), Is.True, b.Failure);
                Assert.That(a.CachedOpeningSlabs, Is.GreaterThan(0)); Assert.That(b.CachedOpeningSlabs, Is.Zero);
            }
            var x = fast.ExportState(); var y = direct.ExportState();
            for (var i = 0; i < x.Cells.Length; i++)
            {
                Assert.That(x.Cells[i].UpperMassKg, Is.EqualTo(y.Cells[i].UpperMassKg).Within(1e-9));
                Assert.That(x.Cells[i].LowerMassKg, Is.EqualTo(y.Cells[i].LowerMassKg).Within(1e-9));
                Assert.That(x.Cells[i].UpperSmokeKg, Is.EqualTo(y.Cells[i].UpperSmokeKg).Within(1e-12));
                Assert.That(x.Cells[i].LowerSmokeKg, Is.EqualTo(y.Cells[i].LowerSmokeKg).Within(1e-12));
                Assert.That(x.Cells[i].UpperEnergyJ, Is.EqualTo(y.Cells[i].UpperEnergyJ).Within(1e-5));
                Assert.That(x.Cells[i].LowerEnergyJ, Is.EqualTo(y.Cells[i].LowerEnergyJ).Within(1e-5));
                Assert.That(x.Cells[i].WallEnergyJ, Is.EqualTo(y.Cells[i].WallEnergyJ).Within(1e-5));
            }
            Assert.That(x.ExternalEnergyJ, Is.EqualTo(y.ExternalEnergyJ).Within(1e-5));
            Assert.That(x.ExternalMassKg, Is.EqualTo(y.ExternalMassKg).Within(1e-9));
            Assert.That(x.ExternalSmokeKg, Is.EqualTo(y.ExternalSmokeKg).Within(1e-12));
            Assert.That(x.ReleasedHeatJ, Is.EqualTo(y.ReleasedHeatJ).Within(1e-7));
            Assert.That(x.ExternalEnthalpyJ, Is.EqualTo(y.ExternalEnthalpyJ).Within(1e-5));
            Assert.That(x.RadiationEscapedJ, Is.EqualTo(y.RadiationEscapedJ).Within(1e-5));
            Assert.That(x.WallLossJ, Is.EqualTo(y.WallLossJ).Within(1e-5));
        }

        [Test]
        public void ForestAndDensePressurePathsAgreeForUnequalVolumesAndParallelOpenings()
        {
            var cells = Enumerable.Range(0, 6).Select(i => new FireCellDefinition { Id = "c" + i, WidthM = 2 + i, DepthM = 4, HeightM = 3 }).ToArray();
            var doors = Enumerable.Range(0, 5).Select(i => new FireDoorDefinition { Id = "d" + i, FromCellId = "c" + i, ToCellId = "c" + (i + 1), WidthM = 2, HeightM = 2 }).ToList();
            doors.Add(new FireDoorDefinition { Id = "parallel", FromCellId = "c1", ToCellId = "c2", WidthM = .3, HeightM = 1 });
            doors.Add(new FireDoorDefinition { Id = "outside", FromCellId = "c5", WidthM = 1, HeightM = 2 });
            var definition = new FireNetworkDefinition { Cells = cells, Doors = doors.ToArray() };
            var forest = new ZoneFireModel(definition); var dense = new ZoneFireModel(definition, options: new FireSolverOptions { PreferForestPressureSolver = false });
            var forcing = new FireForcing { Sources = new[] { new FireSourcePower { CellId = "c0", HeatReleaseW = 60000, EnablePlume = false } } };
            Assert.That(forest.TryAdvance(.1, forcing, out var a), Is.True, a.Failure);
            Assert.That(dense.TryAdvance(.1, forcing, out var b), Is.True, b.Failure);
            Assert.That(a.ForestPressureSolves, Is.GreaterThan(0)); Assert.That(b.DensePressureSolves, Is.GreaterThan(0));
            for (var i = 0; i < cells.Length; i++)
            {
                Assert.That(forest.ReadCell("c" + i).UpperTemperatureK, Is.EqualTo(dense.ReadCell("c" + i).UpperTemperatureK).Within(1e-8));
                Assert.That(forest.ReadCell("c" + i).PressurePa, Is.EqualTo(dense.ReadCell("c" + i).PressurePa).Within(1e-7));
            }
            doors.Add(new FireDoorDefinition { Id = "cycle", FromCellId = "c0", ToCellId = "c5", WidthM = 1, HeightM = 2 });
            definition.Doors = doors.ToArray(); var cyclic = new ZoneFireModel(definition);
            Assert.That(cyclic.TryAdvance(.05, forcing, out var c), Is.True, c.Failure);
            Assert.That(c.ForestPressureSolves, Is.Zero); Assert.That(c.DensePressureSolves, Is.GreaterThan(0));
        }

        [Test]
        public void TwoSimultaneousSourcesAdvanceOneHundredWorldTicksWithConservedLedgers()
        {
            var domain = ConnectedThermalDomain.Create(World(), new ConnectedThermalProfile());
            var fire = new ZoneFireModel(domain.Network, options: domain.SolverOptions); var initial = fire.ExportState();
            var sources = new[] { "rolling_stock_mainline", "underground_connector" }.Select(region => new FireSourcePower {
                CellId = domain.Bindings.First(b => b.RegionId == region && b.PortalId == "").CellId,
                HeatReleaseW = 60000, FuelMassKgPerSecond = .004, SmokeMassKgPerSecond = .0004, HeightM = .5 }).ToArray();
            var forcing = domain.Forcing(sources, _ => true, _ => true, _ => true);
            var times = new double[100]; var accepted = 0; var rejected = 0;
            for (var i = 0; i < times.Length; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew(); var ok = fire.TryAdvance(.05, forcing, out var report); watch.Stop();
                times[i] = watch.Elapsed.TotalMilliseconds; accepted += report.AcceptedSubsteps; rejected += report.RejectedSubsteps;
                Assert.That(ok, Is.True, report.Failure);
            }
            var state = fire.ExportState();
            var balance = state.Cells.Sum(c => c.UpperEnergyJ + c.LowerEnergyJ + c.WallEnergyJ) -
                initial.Cells.Sum(c => c.UpperEnergyJ + c.LowerEnergyJ + c.WallEnergyJ) - state.ExternalEnergyJ;
            Assert.That(state.SimulatedSeconds, Is.EqualTo(5).Within(1e-10));
            Assert.That(Math.Abs(balance), Is.LessThan(.005));
            Assert.That(state.Cells.All(c => c.UpperSmokeKg >= 0 && c.LowerSmokeKg >= 0), Is.True);
            Directory.CreateDirectory("Temp/ChooGuardThermalDomain");
            File.WriteAllText("Temp/ChooGuardThermalDomain/two-source-timing.json", JsonUtility.ToJson(new WorldTiming {
                Cells = domain.Network.Cells.Length, Doors = domain.Network.Doors.Length, TickMs = times,
                Accepted = accepted, Rejected = rejected, EnergyResidualJ = balance }, true));
        }
        [Serializable] private sealed class WorldTiming
        { public int Cells, Doors, Accepted, Rejected; public double[] TickMs; public double EnergyResidualJ; }

        [Test]
        public void LocalSourceExchangesWithNeighborsAndFullWorldStepKeepsEnergyLedger()
        {
            var domain = ConnectedThermalDomain.Create(World(), new ConnectedThermalProfile());
            var fire = new ZoneFireModel(domain.Network, options: domain.SolverOptions); var initial = fire.ExportState();
            var cell = domain.Bindings.First(b => b.RegionId == "rolling_stock_mainline" && b.PortalId == "").CellId;
            var source = new FireSourcePower { CellId = cell, HeatReleaseW = 60000, FuelMassKgPerSecond = .004, SmokeMassKgPerSecond = .0004, HeightM = .5 };
            var forcing = domain.Forcing(new[] { source }, _ => true, _ => true, _ => false);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var advanced = fire.TryAdvance(.05, forcing, out var report); timer.Stop();
            Directory.CreateDirectory("Temp/ChooGuardThermalDomain");
            File.WriteAllText("Temp/ChooGuardThermalDomain/last-step.json", JsonUtility.ToJson(report, true));
            File.WriteAllText("Temp/ChooGuardThermalDomain/last-domain.json", "{\"cells\":" + domain.Network.Cells.Length + ",\"doors\":" + domain.Network.Doors.Length +
                ",\"elapsedTicks\":" + timer.ElapsedTicks + ",\"frequency\":" + System.Diagnostics.Stopwatch.Frequency + "}");
            Assert.That(advanced, Is.True, report.Failure);
            var state = fire.ExportState();
            double Energy(FireState s) => s.Cells.Sum(c => c.UpperEnergyJ + c.LowerEnergyJ + c.WallEnergyJ);
            Assert.That(Energy(state) - Energy(initial) - state.ExternalEnergyJ, Is.EqualTo(0).Within(.001));
            Assert.That(state.Cells.All(c => c.UpperSmokeKg >= 0 && c.LowerSmokeKg >= 0), Is.True);
            Assert.That(state.Cells.Single(c => c.CellId == cell).UpperSmokeKg, Is.GreaterThan(0));
            Assert.That(state.Cells.Where(c => domain.Bindings.Single(b => b.CellId == c.CellId).RegionId == "metro_concourse")
                .Sum(c => c.UpperSmokeKg + c.LowerSmokeKg), Is.LessThan(1e-15));
        }
    }
}
