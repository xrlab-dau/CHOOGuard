using System;
using System.Linq;
using NUnit.Framework;
using ChooGuard.Foundation.Simulation;

namespace ChooGuard.Foundation.Tests
{
    public sealed class FirePhysicsTests
    {
        private static FireCellDefinition Cell(string id, double width = 5, double depth = 5, double height = 4) =>
            new FireCellDefinition { Id = id, WidthM = width, DepthM = depth, HeightM = height };
        private static FireNetworkDefinition Network(params FireCellDefinition[] cells) => new FireNetworkDefinition { Cells = cells };
        private static FireForcing Heater(string cell, double power) => new FireForcing {
            Sources = new[] { new FireSourcePower { CellId = cell, HeatReleaseW = power, EnablePlume = false } } };
        private static double Mass(FireState s) => s.Cells.Sum(c => c.UpperMassKg + c.LowerMassKg);
        private static double Energy(FireState s) => s.Cells.Sum(c => c.UpperEnergyJ + c.LowerEnergyJ + c.WallEnergyJ);
        private static double Smoke(FireState s) => s.Cells.Sum(c => c.UpperSmokeKg + c.LowerSmokeKg);
        private static void Advance(ZoneFireModel model, double seconds, FireForcing forcing = null)
        {
            Assert.That(model.TryAdvance(seconds, forcing ?? new FireForcing(), out var report), Is.True, report?.Failure);
        }

        [Test]
        public void SealedHeatingMatchesAnalyticPressureAndEnergyWithoutInventingMass()
        {
            var model = new ZoneFireModel(Network(Cell("room"))); var before = model.ExportState();
            Advance(model, 10, Heater("room", 100000)); var after = model.ExportState();
            Assert.That(model.ReadCell("room").PressurePa, Is.EqualTo(105325).Within(1e-5));
            Assert.That(Energy(after) - Energy(before), Is.EqualTo(1000000).Within(.001));
            Assert.That(Mass(after), Is.EqualTo(Mass(before)).Within(1e-10));
            Assert.That(after.ExternalEnergyJ, Is.EqualTo(1000000).Within(.001));
        }

        [Test]
        public void PlumeFuelSmokeAndWallRadiationShareOneConservativeLedger()
        {
            var cell = Cell("room"); cell.InitialUpperVolumeFraction = .01;
            cell.WallHeatCapacityJPerK = 100000; cell.InsideHeatTransferWPerM2K = 5; cell.OutsideHeatTransferWPerM2K = 2;
            var model = new ZoneFireModel(Network(cell)); var initial = model.ExportState();
            var forcing = new FireForcing { Sources = new[] { new FireSourcePower { CellId = "room", HeatReleaseW = 100000,
                FuelMassKgPerSecond = .002, SmokeMassKgPerSecond = .00002, RadiationFraction = .2, HeightM = .02 } } };
            Advance(model, 2, forcing); var after = model.ExportState();
            Assert.That(Mass(after) - Mass(initial), Is.EqualTo(.004).Within(1e-9));
            Assert.That(Smoke(after), Is.EqualTo(.00004).Within(1e-11));
            Assert.That(Energy(after) - Energy(initial), Is.EqualTo(after.ExternalEnergyJ).Within(.002));
            Assert.That(after.ExternalEnergyJ, Is.EqualTo(after.ReleasedHeatJ + after.ExternalEnthalpyJ - after.WallLossJ - after.RadiationEscapedJ).Within(1e-6));
            Assert.That(after.ReleasedHeatJ, Is.EqualTo(200000).Within(1e-6));
            Assert.That(after.Cells[0].WallEnergyJ, Is.GreaterThan(initial.Cells[0].WallEnergyJ));
            Assert.That(after.Cells[0].LowerMassKg, Is.LessThan(initial.Cells[0].LowerMassKg));
        }

        [Test]
        public void RadiationWithoutAStoredWallIsExplicitlyAccountedOutside()
        {
            var model = new ZoneFireModel(Network(Cell("room"))); var initial = model.ExportState();
            var forcing = Heater("room", 10000); forcing.Sources[0].RadiationFraction = .3;
            Advance(model, 1, forcing); var after = model.ExportState();
            Assert.That(after.RadiationEscapedJ, Is.EqualTo(3000).Within(1e-7));
            Assert.That(Energy(after) - Energy(initial), Is.EqualTo(7000).Within(.001));
            Assert.That(after.ExternalEnergyJ, Is.EqualTo(7000).Within(.001));
        }

        [Test]
        public void SealedLayerPressureWorkConvergesToTheAdiabaticLowerTemperature()
        {
            var errors = new double[3]; var steps = new[] { .1, .05, .025 };
            for (var i = 0; i < steps.Length; i++)
            {
                var definition = Network(Cell("room"));
                var model = new ZoneFireModel(definition, null, new FireSolverOptions { MaxStepSeconds = steps[i] });
                Advance(model, 10, Heater("room", 100000));
                var expected = definition.AmbientTemperatureK * Math.Pow(105325.0 / 101325, (definition.Gamma - 1) / definition.Gamma);
                errors[i] = Math.Abs(model.ReadCell("room").LowerTemperatureK - expected);
            }
            Assert.That(errors[1], Is.LessThan(errors[0] * .55)); Assert.That(errors[2], Is.LessThan(errors[1] * .55));
        }

        [Test]
        public void OnePascalDoorImpulseRelaxesWithoutExplicitStepOscillation()
        {
            var definition = Network(Cell("a", 2.8, 2.8, 2.13), Cell("b", 2.8, 2.8, 2.13));
            definition.Doors = new[] { new FireDoorDefinition { Id = "door", FromCellId = "a", ToCellId = "b", WidthM = .24, HeightM = 1.83 } };
            var state = ZoneFireModel.CreateState(definition); var ratio = 101326.0 / 101325;
            state.Cells[0].UpperEnergyJ *= ratio; state.Cells[0].LowerEnergyJ *= ratio;
            state.Cells[0].UpperMassKg *= ratio; state.Cells[0].LowerMassKg *= ratio;
            var model = new ZoneFireModel(definition, state); Advance(model, .05);
            var difference = model.ReadCell("a").PressurePa - model.ReadCell("b").PressurePa;
            Assert.That(difference, Is.InRange(-1e-6, .001));
            Assert.That(Mass(model.ExportState()), Is.EqualTo(Mass(state)).Within(1e-10));
            Assert.That(Energy(model.ExportState()), Is.EqualTo(Energy(state)).Within(.001));
        }

        [Test]
        public void HeatedClosedNetworkConservesMassSmokeAndExternalEnergy()
        {
            var definition = Network(Cell("a"), Cell("b"), Cell("c"));
            definition.Doors = new[] {
                new FireDoorDefinition { Id = "ab", FromCellId = "a", ToCellId = "b", WidthM = 1, HeightM = 2 },
                new FireDoorDefinition { Id = "bc", FromCellId = "b", ToCellId = "c", WidthM = 1, HeightM = 2 } };
            var state = ZoneFireModel.CreateState(definition); state.Cells[0].UpperSmokeKg = .1;
            var model = new ZoneFireModel(definition, state); Advance(model, 5, Heater("a", 100000));
            var after = model.ExportState();
            Assert.That(Mass(after), Is.EqualTo(Mass(state)).Within(1e-9));
            Assert.That(Smoke(after), Is.EqualTo(.1).Within(1e-10));
            Assert.That(Energy(after) - Energy(state), Is.EqualTo(after.ExternalEnergyJ).Within(.002));
            Assert.That(after.Cells[1].UpperSmokeKg + after.Cells[1].LowerSmokeKg, Is.GreaterThan(0));
            Assert.That(after.Cells.All(c => c.UpperSmokeKg >= 0 && c.LowerSmokeKg >= 0), Is.True);
        }

        [Test]
        public void IsothermalHighOpeningTransfersIntoTheCorrespondingUpperLayer()
        {
            var definition = Network(Cell("a"), Cell("b"));
            definition.Doors = new[] { new FireDoorDefinition { Id = "high", FromCellId = "a", ToCellId = "b", WidthM = 1, BottomElevationM = 2.5, HeightM = 1 } };
            var initial = ZoneFireModel.CreateState(definition); var ratio = 101326.0 / 101325;
            initial.Cells[0].UpperEnergyJ *= ratio; initial.Cells[0].LowerEnergyJ *= ratio;
            initial.Cells[0].UpperMassKg *= ratio; initial.Cells[0].LowerMassKg *= ratio;
            initial.Cells[0].UpperSmokeKg = .1;
            var model = new ZoneFireModel(definition, initial); Advance(model, .005);
            var b = model.ExportState().Cells[1];
            Assert.That(b.UpperSmokeKg, Is.GreaterThan(0)); Assert.That(b.LowerSmokeKg, Is.Zero);
        }

        [Test]
        public void ClosedDoorStopsTransportWhileOpenDoorMovesTheSameTracer()
        {
            var definition = Network(Cell("a"), Cell("b"));
            definition.Doors = new[] { new FireDoorDefinition { Id = "door", FromCellId = "a", ToCellId = "b", WidthM = 1, HeightM = 3 } };
            var state = ZoneFireModel.CreateState(definition); state.Cells[0].UpperSmokeKg = .1;
            var shut = new ZoneFireModel(definition, state); var open = new ZoneFireModel(definition, state);
            var forcing = Heater("a", 100000); forcing.Doors = new[] { new FireDoorSetting { DoorId = "door", OpeningFraction = 0 } };
            Advance(shut, 3, forcing); Advance(open, 3, Heater("a", 100000));
            Assert.That(shut.ExportState().Cells[1].UpperSmokeKg, Is.Zero);
            Assert.That(shut.ExportState().Cells[1].LowerSmokeKg, Is.Zero);
            Assert.That(Smoke(open.ExportState()), Is.EqualTo(.1).Within(1e-10));
            Assert.That(open.ExportState().Cells[1].UpperSmokeKg + open.ExportState().Cells[1].LowerSmokeKg, Is.GreaterThan(0));
        }

        [Test]
        public void WallExchangeConservesEnergyAndDoesNotOvershootDespitePositiveTemperatures()
        {
            var cell = Cell("room"); cell.WallHeatCapacityJPerK = 1000;
            cell.InsideHeatTransferWPerM2K = 40000 / cell.SurfaceAreaM2;
            var definition = Network(cell); var initial = ZoneFireModel.CreateState(definition);
            initial.Cells[0].UpperEnergyJ *= 400 / definition.AmbientTemperatureK;
            initial.Cells[0].LowerEnergyJ *= 400 / definition.AmbientTemperatureK;
            initial.Cells[0].WallEnergyJ = 1000 * 300;
            var model = new ZoneFireModel(definition, initial); Advance(model, .05);
            var after = model.ExportState(); var view = model.ReadCell("room");
            Assert.That(Energy(after), Is.EqualTo(Energy(initial)).Within(.001));
            Assert.That(view.WallTemperatureK, Is.InRange(300, 400));
            Assert.That(view.UpperTemperatureK, Is.InRange(300, 400));
            Assert.That(view.LowerTemperatureK, Is.InRange(300, 400));
        }

        private static double VentilationError(double maxStep)
        {
            var definition = Network(Cell("room")); var initial = ZoneFireModel.CreateState(definition);
            initial.Cells[0].UpperSmokeKg = .5; initial.Cells[0].LowerSmokeKg = .5;
            // Balanced mass flow through both layers: C(t)=C0 exp(-q t/M).
            var total = Mass(initial); var q = total / 100;
            definition.Fans = new[] {
                new FireFanDefinition { Id = "in-upper", ToCellId = "room", ToUpper = true, MassFlowKgPerSecond = q / 2 },
                new FireFanDefinition { Id = "in-lower", ToCellId = "room", MassFlowKgPerSecond = q / 2 },
                new FireFanDefinition { Id = "out-upper", FromCellId = "room", FromUpper = true, MassFlowKgPerSecond = q / 2 },
                new FireFanDefinition { Id = "out-lower", FromCellId = "room", MassFlowKgPerSecond = q / 2 } };
            var model = new ZoneFireModel(definition, initial, new FireSolverOptions { MaxStepSeconds = maxStep });
            Advance(model, 10); var after = model.ExportState();
            Assert.That(Mass(after), Is.EqualTo(total).Within(1e-9));
            Assert.That(Smoke(after) - 1, Is.EqualTo(after.ExternalSmokeKg).Within(1e-10));
            return Math.Abs(Smoke(after) - Math.Exp(-.1));
        }

        [Test]
        public void VentilationMatchesAnalyticDecayAndConvergesWhenStepIsHalved()
        {
            var coarse = VentilationError(.1); var medium = VentilationError(.05); var fine = VentilationError(.025);
            Assert.That(coarse, Is.GreaterThan(0)); Assert.That(medium, Is.LessThan(coarse * .55));
            Assert.That(fine, Is.LessThan(medium * .55));
        }

        [Test]
        public void FailedStepBudgetDoesNotCommitPartialTimeOrHeat()
        {
            var model = new ZoneFireModel(Network(Cell("room")), null, new FireSolverOptions { MaxSubsteps = 1 });
            var before = model.ExportState();
            Assert.That(model.TryAdvance(1, Heater("room", 100000), out var report), Is.False);
            var after = model.ExportState();
            Assert.That(report.Failure, Is.Not.Empty); Assert.That(after.SimulatedSeconds, Is.Zero);
            Assert.That(Energy(after), Is.EqualTo(Energy(before))); Assert.That(after.ExternalEnergyJ, Is.Zero);
        }

        [Test]
        public void ElevatedAmbientRoomsRemainAtRestThroughExteriorOpenings()
        {
            var cell = Cell("upstairs"); cell.FloorElevationM = 10;
            var definition = Network(cell);
            definition.Doors = new[] { new FireDoorDefinition { Id = "outside", FromCellId = cell.Id, WidthM = 2, BottomElevationM = 10, HeightM = 3 } };
            var initial = ZoneFireModel.CreateState(definition); initial.Cells[0].UpperSmokeKg = .1;
            var model = new ZoneFireModel(definition, initial); Advance(model, 1);
            var after = model.ExportState();
            Assert.That(after.ExternalMassKg, Is.EqualTo(0).Within(1e-10));
            Assert.That(Energy(after), Is.EqualTo(Energy(initial)).Within(1e-6));
            Assert.That(after.Cells[0].UpperMassKg, Is.EqualTo(initial.Cells[0].UpperMassKg).Within(1e-10));
            Assert.That(after.Cells[0].LowerMassKg, Is.EqualTo(initial.Cells[0].LowerMassKg).Within(1e-10));
            Assert.That(after.Cells[0].UpperSmokeKg, Is.EqualTo(.1).Within(1e-12));
            Assert.That(after.Cells[0].LowerSmokeKg, Is.EqualTo(0).Within(1e-14));
        }

        [Test]
        public void TwoExhaustsUseCombinedDonorWithdrawalAndNeverClampLostMass()
        {
            var definition = Network(Cell("room")); var initial = ZoneFireModel.CreateState(definition);
            var q = initial.Cells[0].UpperMassKg * .6;
            definition.Fans = new[] {
                new FireFanDefinition { Id = "one", FromCellId = "room", FromUpper = true, MassFlowKgPerSecond = q },
                new FireFanDefinition { Id = "two", FromCellId = "room", FromUpper = true, MassFlowKgPerSecond = q } };
            var oneAttempt = new ZoneFireModel(definition, initial, new FireSolverOptions { MaxStepSeconds = .5,
                MaxSubsteps = 1, MaxRelativeTemperatureChange = .99, MaxLayerVolumeFractionChange = .99 });
            Assert.That(oneAttempt.TryAdvance(.5, new FireForcing(), out var rejected), Is.False);
            Assert.That(rejected.LastRejectedReason, Does.StartWith("combined donor withdrawal"));
            var model = new ZoneFireModel(definition, initial, new FireSolverOptions { MaxStepSeconds = .5 });
            Advance(model, .5); var after = model.ExportState();
            Assert.That(Mass(after) - Mass(initial), Is.EqualTo(after.ExternalMassKg).Within(1e-9));
            Assert.That(after.Cells[0].UpperMassKg, Is.GreaterThan(0));
            Assert.That(after.Cells[0].LowerMassKg, Is.GreaterThan(0));
        }

        [Test]
        public void HotUpperLayerProducesOpposingDoorFlowsWithExplicitVelocityConventions()
        {
            var definition = Network(Cell("room", 2.8, 2.8, 2.13));
            definition.Doors = new[] { new FireDoorDefinition { Id = "outside", FromCellId = "room", WidthM = .24, HeightM = 1.83 } };
            var initial = ZoneFireModel.CreateState(definition);
            initial.Cells[0].UpperMassKg *= definition.AmbientTemperatureK / 450;
            var model = new ZoneFireModel(definition, initial); Advance(model, .1);
            var bottom = model.SampleOpening("outside", .1); var top = model.SampleOpening("outside", 1.8);
            Assert.That(bottom.MassFlowPerHeightKgSM, Is.LessThan(0)); Assert.That(top.MassFlowPerHeightKgSM, Is.GreaterThan(0));
            Assert.That(top.NominalAreaMeanSpeedMS, Is.EqualTo(.7 * top.JetSpeedMS).Within(1e-12));
            var closed = new FireForcing { Doors = new[] { new FireDoorSetting { DoorId = "outside", OpeningFraction = 0 } } };
            Assert.That(model.SampleOpening("outside", 1.8, closed).MassFlowPerHeightKgSM, Is.Zero);
        }

        [Test]
        public void MicroscopicPressureCoreIsContinuousAndItsFlowChangeIsExplicit()
        {
            const double epsilon = .0001;
            FireOpeningSample At(double pressure)
            {
                var definition = Network(Cell("a"), Cell("b"));
                definition.Doors = new[] { new FireDoorDefinition { Id = "door", FromCellId = "a", ToCellId = "b", WidthM = 1, HeightM = 2 } };
                var initial = ZoneFireModel.CreateState(definition); var ratio = (definition.AmbientPressurePa + pressure) / definition.AmbientPressurePa;
                initial.Cells[0].UpperEnergyJ *= ratio; initial.Cells[0].LowerEnergyJ *= ratio;
                initial.Cells[0].UpperMassKg *= ratio; initial.Cells[0].LowerMassKg *= ratio;
                return new ZoneFireModel(definition, initial).SampleOpening("door", 0);
            }
            var quarter = At(epsilon / 4); var threshold = At(epsilon); var outside = At(2 * epsilon);
            Assert.That(quarter.JetSpeedMS / threshold.JetSpeedMS, Is.EqualTo(.25).Within(2e-6),
                "Inside the declared core velocity is linear; at epsilon/4 it is half the unregularized Bernoulli prediction.");
            Assert.That(outside.JetSpeedMS / threshold.JetSpeedMS, Is.EqualTo(Math.Sqrt(2)).Within(2e-6));
            Assert.That(At(-epsilon / 4).JetSpeedMS, Is.EqualTo(-quarter.JetSpeedMS).Within(1e-8));
            Assert.That(At(0).JetSpeedMS, Is.Zero);
            Assert.That(Math.Abs(At(epsilon * 1.0001).JetSpeedMS - At(epsilon * .9999).JetSpeedMS), Is.LessThan(.00001));
        }

        [Test]
        public void PressureRegularizationAndSolverToleranceCanBeTightenedIndependently()
        {
            double Run(double epsilon, double tolerance)
            {
                var definition = Network(Cell("room", 2.8, 2.8, 2.13));
                definition.Doors = new[] { new FireDoorDefinition { Id = "outside", FromCellId = "room", WidthM = .24, HeightM = 1.83 } };
                var model = new ZoneFireModel(definition, null, new FireSolverOptions { PressureRegularizationPa = epsilon, PressureTolerancePa = tolerance });
                Advance(model, 1, Heater("room", 62900)); return model.ReadCell("room").UpperTemperatureK;
            }
            var baseline = Run(.0001, 1e-7); var smallerCore = Run(.00005, 1e-7); var tighterSolve = Run(.0001, 1e-8);
            Assert.That(Math.Abs(smallerCore - baseline), Is.LessThan(.001));
            Assert.That(Math.Abs(tighterSolve - baseline), Is.LessThan(.001));
        }

        [Test]
        public void InvalidInputAndNonfiniteStateAreRejectedAtTheBoundary()
        {
            var definition = Network(Cell("room")); definition.Cells[0].HeightM = double.NaN;
            Assert.Throws<ArgumentException>(() => new ZoneFireModel(definition));
            definition = Network(Cell("room")); definition.Doors = new[] { new FireDoorDefinition { Id = "bad", FromCellId = "missing", WidthM = 1, HeightM = 1 } };
            Assert.Throws<ArgumentException>(() => new ZoneFireModel(definition));
            definition = Network(Cell("room")); var state = ZoneFireModel.CreateState(definition); state.Cells[0].LowerSmokeKg = -1;
            Assert.Throws<ArgumentException>(() => new ZoneFireModel(definition, state));
            var model = new ZoneFireModel(definition);
            Assert.Throws<ArgumentException>(() => model.TryAdvance(.05, Heater("room", double.NaN), out _));
        }
    }
}
