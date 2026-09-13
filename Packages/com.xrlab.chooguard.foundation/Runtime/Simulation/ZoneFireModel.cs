using System;
using System.Collections.Generic;
using System.Linq;

namespace ChooGuard.Foundation.Simulation
{
    /// <summary>
    /// Conservative two-layer ideal-gas cells with hydrostatic openings and prescribed heat/smoke.
    /// Pressure is implicit; donor thermodynamics are frozen over accepted adaptive substeps.
    /// This is an independent reduced model, not CFAST parity, combustion chemistry or toxicity.
    /// </summary>
    public sealed class ZoneFireModel
    {
        private readonly FireNetworkDefinition definition;
        private readonly FireSolverOptions options;
        private readonly Dictionary<string, int> indices;
        private readonly int[] doorFrom, doorTo, fanFrom, fanTo;
        private readonly double cp, cv, gasR, gamma, ambientDensity;
        private FireState state;
        private readonly int[] forestParents, forestOrder;
        private sealed class ResolvedForcing
        { public FireSourcePower[] Sources; public double[] Openings; public bool[] Fans; }
        private struct OpeningSlab
        {
            public int From, To, FromLayer, ToLayer;
            public double Bottom, Top, PressureOffset, HeadBottom, HeadTop, Coefficient;
        }

        public ZoneFireModel(FireNetworkDefinition definition, FireState initial = null, FireSolverOptions options = null)
        {
            ValidateDefinition(definition); ValidateOptions(options ?? new FireSolverOptions());
            this.definition = definition.Copy(); this.options = (options ?? new FireSolverOptions()).Copy();
            cp = definition.SpecificHeatCp; gamma = definition.Gamma; cv = cp / gamma; gasR = cp - cv;
            ambientDensity = definition.AmbientPressurePa / (gasR * definition.AmbientTemperatureK);
            indices = definition.Cells.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
            doorFrom = definition.Doors.Select(d => Index(d.FromCellId)).ToArray();
            doorTo = definition.Doors.Select(d => Index(d.ToCellId)).ToArray();
            fanFrom = definition.Fans.Select(f => Index(f.FromCellId)).ToArray();
            fanTo = definition.Fans.Select(f => Index(f.ToCellId)).ToArray();
            BuildForest(definition.Cells.Length, doorFrom, doorTo, out forestParents, out forestOrder);
            state = (initial ?? CreateState(definition)).Copy(); ValidateState(state);
        }

        public FireState ExportState() => state.Copy();
        public FireCellMetrics ReadCell(string id) => Metrics(state.Cells[Index(id)], definition.Cells[Index(id)]);

        public FireOpeningSample SampleOpening(string doorId, double elevation, FireForcing forcing = null)
        {
            forcing = forcing ?? new FireForcing(); ValidateForcing(forcing);
            var index = Array.FindIndex(definition.Doors, d => d.Id == doorId);
            if (index < 0 || !Finite(elevation)) throw new ArgumentException("Invalid opening sample.");
            var door = definition.Doors[index];
            if (elevation < door.BottomElevationM || elevation > door.BottomElevationM + door.HeightM)
                throw new ArgumentException("Sample is outside this opening.");
            var metrics = state.Cells.Select((c, i) => Metrics(c, definition.Cells[i])).ToArray();
            var pressure = PressureDifference(doorFrom[index], doorTo[index], elevation, metrics, new double[metrics.Length]);
            var donor = pressure >= 0 ? doorFrom[index] : doorTo[index]; var layer = LayerAt(donor, elevation, metrics);
            var rho = donor < 0 ? ambientDensity : layer == 0 ? metrics[donor].UpperDensityKgM3 : metrics[donor].LowerDensityKgM3;
            var setting = forcing.Doors.FirstOrDefault(d => d.DoorId == doorId); var fraction = setting?.OpeningFraction ?? door.OpeningFraction;
            var root = Math.Abs(pressure) <= options.PressureRegularizationPa ? pressure / Math.Sqrt(options.PressureRegularizationPa) :
                Math.Sign(pressure) * Math.Sqrt(Math.Abs(pressure));
            var jet = fraction == 0 ? 0 : Math.Sqrt(2 / rho) * root; var mean = door.DischargeCoefficient * fraction * jet;
            return new FireOpeningSample(elevation, pressure, jet, mean, mean * rho * door.WidthM);
        }

        public static FireState CreateState(FireNetworkDefinition definition)
        {
            ValidateDefinition(definition);
            var cv = definition.SpecificHeatCp / definition.Gamma;
            var r = definition.SpecificHeatCp - cv;
            var rho = definition.AmbientPressurePa / (r * definition.AmbientTemperatureK);
            return new FireState { ModelVersion = definition.ModelVersion, Cells = definition.Cells.Select(c => {
                // Uniform reference-density atmosphere. Absolute hydrostatic elevation offset is
                // applied to opening pressures, not mixed into this low-Mach thermodynamic EOS.
                var p = definition.AmbientPressurePa;
                var mass = p * c.VolumeM3 / (r * definition.AmbientTemperatureK); var f = c.InitialUpperVolumeFraction;
                return new FireCellState { CellId = c.Id, UpperMassKg = mass * f, LowerMassKg = mass * (1 - f),
                    UpperEnergyJ = p * c.VolumeM3 * f / (definition.Gamma - 1),
                    LowerEnergyJ = p * c.VolumeM3 * (1 - f) / (definition.Gamma - 1),
                    WallEnergyJ = c.WallHeatCapacityJPerK * definition.AmbientTemperatureK };
            }).ToArray() };
        }

        // Failure is atomic across the whole requested interval. The caller must not publish a partial world tick.
        public bool TryAdvance(double seconds, FireForcing forcing, out FireStepReport report)
        {
            if (!Positive(seconds) || seconds > 3600) throw new ArgumentException("Invalid fire integration interval.");
            ValidateForcing(forcing); report = new FireStepReport(); var next = state.Copy();
            var resolved = new ResolvedForcing { Sources = forcing.Sources,
                Openings = definition.Doors.Select(d => forcing.Doors.FirstOrDefault(s => s.DoorId == d.Id)?.OpeningFraction ?? d.OpeningFraction).ToArray(),
                Fans = definition.Fans.Select(f => forcing.Fans.FirstOrDefault(s => s.FanId == f.Id)?.Enabled ?? f.Enabled).ToArray() };
            var remaining = seconds;
            while (remaining > Math.Max(1e-12, seconds * 1e-12))
            {
                if (report.AcceptedSubsteps + report.RejectedSubsteps >= options.MaxSubsteps)
                { report.Failure = "Fire substep budget exhausted; no state committed."; return false; }
                var step = Math.Min(remaining, options.MaxStepSeconds);
                var metrics = next.Cells.Select((c, i) => Metrics(c, definition.Cells[i])).ToArray();
                var basis = BaseRates(next, metrics, resolved, ref step);
                var slabs = options.CacheOpeningSlabs ? OpeningSlabs(metrics, resolved) : null;
                report.CachedOpeningSlabs = Math.Max(report.CachedOpeningSlabs, slabs?.Length ?? 0);
                var accepted = false;
                while (!accepted)
                {
                    if (step < options.MinStepSeconds || report.AcceptedSubsteps + report.RejectedSubsteps >= options.MaxSubsteps)
                    { report.Failure = "Fire solver could not accept a positive, resolved substep; no state committed. " +
                        report.LastRejectedReason + " at " + report.ProgressSeconds + "s, residual " + report.LastPressureResidualPa +
                        "Pa, step " + report.LastAttemptSeconds + ", accepted/rejected " + report.AcceptedSubsteps + "/" + report.RejectedSubsteps; return false; }
                    if (TrySubstep(next, metrics, basis, resolved, slabs, step, report, out var candidate))
                    { next = candidate; accepted = true; report.AcceptedSubsteps++; remaining -= step; report.ProgressSeconds += step; }
                    else { step *= .5; report.RejectedSubsteps++; }
                }
            }
            next.SimulatedSeconds = state.SimulatedSeconds + seconds; ValidateState(next);
            state = next; return true;
        }

        private sealed class Rates
        {
            public readonly double[] Mass, Energy, Smoke, Outgoing, Wall;
            public double ExternalMass, ExternalEnergy, ExternalSmoke, Heat, Enthalpy, Radiation, WallLoss;
            public Rates(int count) { Mass = new double[2 * count]; Energy = new double[2 * count];
                Smoke = new double[2 * count]; Outgoing = new double[2 * count]; Wall = new double[count]; }
            public void Set(Rates other)
            {
                Array.Copy(other.Mass, Mass, Mass.Length); Array.Copy(other.Energy, Energy, Energy.Length);
                Array.Copy(other.Smoke, Smoke, Smoke.Length); Array.Copy(other.Outgoing, Outgoing, Outgoing.Length);
                Array.Copy(other.Wall, Wall, Wall.Length); ExternalMass = other.ExternalMass; ExternalEnergy = other.ExternalEnergy;
                ExternalSmoke = other.ExternalSmoke; Heat = other.Heat; Enthalpy = other.Enthalpy;
                Radiation = other.Radiation; WallLoss = other.WallLoss;
            }
        }

        private Rates BaseRates(FireState current, FireCellMetrics[] metrics, ResolvedForcing forcing, ref double step)
        {
            var rates = new Rates(current.Cells.Length);
            foreach (var source in forcing.Sources)
            {
                var i = Index(source.CellId); var m = metrics[i]; var c = current.Cells[i];
                var convective = source.HeatReleaseW * (1 - source.RadiationFraction);
                if (source.EnablePlume && convective > 0)
                {
                    var entrained = PlumeEntrainment(source, m);
                    Transfer(rates, current, metrics, 2 * i + 1, 2 * i, entrained);
                }
                var fuelEnthalpy = source.FuelMassKgPerSecond * cp * definition.AmbientTemperatureK;
                rates.Mass[2 * i] += source.FuelMassKgPerSecond;
                rates.Smoke[2 * i] += source.SmokeMassKgPerSecond;
                rates.Energy[2 * i] += convective + fuelEnthalpy;
                rates.ExternalMass += source.FuelMassKgPerSecond; rates.ExternalSmoke += source.SmokeMassKgPerSecond;
                rates.ExternalEnergy += source.HeatReleaseW + fuelEnthalpy; rates.Heat += source.HeatReleaseW; rates.Enthalpy += fuelEnthalpy;
                var radiation = source.HeatReleaseW * source.RadiationFraction;
                if (definition.Cells[i].WallHeatCapacityJPerK > 0) rates.Wall[i] += radiation;
                else { rates.Radiation += radiation; rates.ExternalEnergy -= radiation; }
            }
            for (var i = 0; i < current.Cells.Length; i++)
            {
                var c = definition.Cells[i]; if (c.WallHeatCapacityJPerK == 0) continue;
                var m = metrics[i]; var wallRate = 0.0;
                for (var layer = 0; layer < 2; layer++)
                {
                    var area = c.LayerSurfaceArea(m.InterfaceHeightM, layer == 0);
                    var conductance = c.InsideHeatTransferWPerM2K * area;
                    var temperature = layer == 0 ? m.UpperTemperatureK : m.LowerTemperatureK;
                    var mass = LayerMass(current.Cells[i], layer);
                    var exchange = conductance * (temperature - m.WallTemperatureK);
                    rates.Energy[2 * i + layer] -= exchange; rates.Wall[i] += exchange;
                    // Combined wall conductance participates in the shared wall time constant.
                    wallRate += conductance / c.WallHeatCapacityJPerK;
                    if (conductance > 0) step = Math.Min(step, .4 * mass * cv / conductance);
                }
                var outsideConductance = c.OutsideHeatTransferWPerM2K > 0
                    ? c.SurfaceAreaM2 / (c.WallThicknessM / c.WallConductivityWPerMK + 1 / c.OutsideHeatTransferWPerM2K) : 0;
                var loss = outsideConductance * (m.WallTemperatureK - definition.AmbientTemperatureK);
                rates.Wall[i] -= loss; rates.ExternalEnergy -= loss; rates.WallLoss += loss;
                wallRate += outsideConductance / c.WallHeatCapacityJPerK;
                if (wallRate > 0) step = Math.Min(step, .4 / wallRate);
            }
            return rates;
        }

        private double PlumeEntrainment(FireSourcePower source, FireCellMetrics m)
        {
            var z = m.InterfaceHeightM - source.HeightM; if (z <= 0) return 0;
            var q = source.HeatReleaseW / 1000; var qc = q * (1 - source.RadiationFraction);
            var diameter = Math.Sqrt(4 * source.PlumeAreaM2 / Math.PI); var cpk = cp / 1000;
            var rho = m.LowerDensityKgM3; var t = m.LowerTemperatureK; var g = definition.GravityMPerS2;
            var star = q / (rho * cpk * t * Math.Sqrt(g) * Math.Pow(diameter, 2.5));
            var origin = diameter * (-1.02 + 1.4 * Math.Pow(star, .4));
            var flame = Math.Max(.0001, diameter * (-1.02 + 3.7 * Math.Pow(star, .4)));
            var delta = Math.Max(.0001, Math.Max(z, flame) - origin);
            var c1 = .196 * Math.Pow(g * rho * rho / (cpk * t), 1.0 / 3);
            var c2 = 2.9 / Math.Pow(Math.Sqrt(g) * cpk * rho * t, 2.0 / 3);
            var rate = c1 * Math.Pow(qc, 1.0 / 3) * Math.Pow(delta, 5.0 / 3) *
                (1 + c2 * Math.Pow(qc, 2.0 / 3) * Math.Pow(delta, -5.0 / 3)) * Math.Min(1, z / flame);
            return Math.Min(rate * source.PlumeMultiplier,
                source.HeatReleaseW * (1 - source.RadiationFraction) / (cp * Math.Max(1, m.UpperTemperatureK - m.LowerTemperatureK)));
        }

        private bool TrySubstep(FireState current, FireCellMetrics[] metrics, Rates basis, ResolvedForcing forcing, OpeningSlab[] slabs,
            double step, FireStepReport report, out FireState next)
        {
            next = null; var n = current.Cells.Length; var increments = new double[n];
            report.LastAttemptSeconds = step;
            var rates = new Rates(n); var scratch = new Rates(n); var residual = new double[n];
            var shiftedResidual = new double[n]; var jacobian = new double[n, n]; var change = new double[n];
            var converged = false;
            for (var iteration = 0; iteration < options.MaxNewtonIterations; iteration++)
            {
                report.PressureIterations++;
                var evaluationStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                Evaluate(current, metrics, basis, forcing, slabs, increments, rates, jacobian, step);
                report.RateEvaluationTicks += System.Diagnostics.Stopwatch.GetTimestamp() - evaluationStarted;
                var error = Residual(increments, rates, step, residual);
                report.LastPressureResidualPa = error;
                if (!Finite(error)) return Reject(report, "nonfinite pressure residual");
                if (error <= options.PressureTolerancePa) { converged = true; break; }
                for (var i = 0; i < n; i++) change[i] = -residual[i];
                var solveStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                if (options.PreferForestPressureSolver && forestOrder != null)
                {
                    report.ForestPressureSolves++;
                    if (!SolveForest(jacobian, change)) return Reject(report, "singular forest pressure Jacobian");
                }
                else
                {
                    report.DensePressureSolves++;
                    if (!Solve(jacobian, change)) return Reject(report, "singular pressure Jacobian");
                }
                report.LinearSolveTicks += System.Diagnostics.Stopwatch.GetTimestamp() - solveStarted;
                var accepted = false; var scale = 1.0; var trial = new double[n];
                for (var backtrack = 0; backtrack < options.MaxBacktracks; backtrack++, scale *= .5)
                {
                    var positive = true;
                    for (var i = 0; i < n; i++) { trial[i] = increments[i] + scale * change[i]; positive &= metrics[i].PressurePa + trial[i] > 0; }
                    if (!positive) continue;
                    evaluationStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    Evaluate(current, metrics, basis, forcing, slabs, trial, scratch);
                    report.RateEvaluationTicks += System.Diagnostics.Stopwatch.GetTimestamp() - evaluationStarted;
                    if (Residual(trial, scratch, step, shiftedResidual) >= error) continue;
                    Array.Copy(trial, increments, n); accepted = true; break;
                }
                if (!accepted) return Reject(report, "pressure line search failed");
            }
            if (!converged) return Reject(report, "pressure iteration budget exhausted");
            var maximum = Residual(increments, rates, step, residual);
            report.MaximumPressureResidualPa = Math.Max(report.MaximumPressureResidualPa, maximum);
            var candidate = current.Copy();
            for (var i = 0; i < n; i++)
            {
                var c = current.Cells[i]; var proposed = candidate.Cells[i]; var p = metrics[i].PressurePa + increments[i];
                report.MaximumPressureEnergyResidualJ = Math.Max(report.MaximumPressureEnergyResidualJ,
                    Math.Abs(residual[i]) * definition.Cells[i].VolumeM3 / (gamma - 1));
                for (var layer = 0; layer < 2; layer++)
                    if (step * rates.Outgoing[2 * i + layer] > options.MaxDonorFraction * LayerMass(c, layer)) return Reject(report, "combined donor withdrawal: " + c.CellId);
                proposed.UpperMassKg += step * rates.Mass[2 * i]; proposed.LowerMassKg += step * rates.Mass[2 * i + 1];
                proposed.UpperSmokeKg += step * rates.Smoke[2 * i]; proposed.LowerSmokeKg += step * rates.Smoke[2 * i + 1];
                proposed.WallEnergyJ += step * rates.Wall[i];
                var total = c.UpperEnergyJ + c.LowerEnergyJ + step * (rates.Energy[2 * i] + rates.Energy[2 * i + 1]);
                // Increment form avoids subtracting atmospheric-scale terms for an ambient-rest step.
                var upperChange = (step * rates.Energy[2 * i] + increments[i] * metrics[i].UpperVolumeM3) / gamma;
                proposed.UpperEnergyJ = c.UpperEnergyJ + upperChange;
                proposed.LowerEnergyJ = c.LowerEnergyJ + step * (rates.Energy[2 * i] + rates.Energy[2 * i + 1]) - upperChange;
                if (!ValidCell(proposed, definition.Cells[i])) return Reject(report, "invalid candidate layer: " + c.CellId);
                var m = Metrics(proposed, definition.Cells[i]);
                if (Math.Abs(m.PressurePa - p) > options.PressureTolerancePa * 2 + 1e-8 ||
                    Math.Abs(m.UpperTemperatureK / metrics[i].UpperTemperatureK - 1) > options.MaxRelativeTemperatureChange ||
                    Math.Abs(m.LowerTemperatureK / metrics[i].LowerTemperatureK - 1) > options.MaxRelativeTemperatureChange ||
                    Math.Abs(m.UpperVolumeM3 - metrics[i].UpperVolumeM3) > options.MaxLayerVolumeFractionChange * definition.Cells[i].VolumeM3)
                    return Reject(report, "candidate thermodynamic change: " + c.CellId);
                var fraction = m.UpperVolumeM3 / definition.Cells[i].VolumeM3;
                if (fraction < options.MinLayerVolumeFraction || fraction > 1 - options.MinLayerVolumeFraction)
                {
                    // Conservative single-zone transition/repartition; never add a minimum mass or energy.
                    var f = fraction < .5 ? options.MinLayerVolumeFraction * 10 : 1 - options.MinLayerVolumeFraction * 10;
                    var mass = proposed.UpperMassKg + proposed.LowerMassKg; var smoke = proposed.UpperSmokeKg + proposed.LowerSmokeKg;
                    proposed.UpperMassKg = mass * f; proposed.LowerMassKg = mass - proposed.UpperMassKg;
                    proposed.UpperEnergyJ = total * f; proposed.LowerEnergyJ = total - proposed.UpperEnergyJ;
                    proposed.UpperSmokeKg = smoke * f; proposed.LowerSmokeKg = smoke - proposed.UpperSmokeKg;
                    candidate.ConservativeRepartitions++;
                }
            }
            // Re-evaluate at the pressure implied by the actual stored energies, not only the
            // Newton candidate. Bound pressure-equation error in joules; record the real residual.
            var committed = new double[n];
            for (var i = 0; i < n; i++)
                committed[i] = (gamma - 1) / definition.Cells[i].VolumeM3 *
                    ((candidate.Cells[i].UpperEnergyJ - current.Cells[i].UpperEnergyJ) + (candidate.Cells[i].LowerEnergyJ - current.Cells[i].LowerEnergyJ));
            var committedEvaluationStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            Evaluate(current, metrics, basis, forcing, slabs, committed, scratch);
            report.RateEvaluationTicks += System.Diagnostics.Stopwatch.GetTimestamp() - committedEvaluationStarted;
            Residual(committed, scratch, step, shiftedResidual);
            for (var i = 0; i < n; i++)
            {
                var errorPa = Math.Abs(shiftedResidual[i]); var errorJ = errorPa * definition.Cells[i].VolumeM3 / (gamma - 1);
                var allowanceJ = options.CommitEnergyToleranceJ + options.PressureTolerancePa * definition.Cells[i].VolumeM3 / (gamma - 1);
                if (!Finite(errorJ) || errorJ > allowanceJ) return Reject(report, "committed pressure/flux inconsistency: " + current.Cells[i].CellId);
                report.MaximumCommittedPressureResidualPa = Math.Max(report.MaximumCommittedPressureResidualPa, errorPa);
                report.MaximumCommittedPressureEnergyResidualJ = Math.Max(report.MaximumCommittedPressureEnergyResidualJ, errorJ);
            }
            candidate.ExternalMassKg += step * rates.ExternalMass; candidate.ExternalEnergyJ += step * rates.ExternalEnergy;
            candidate.ExternalSmokeKg += step * rates.ExternalSmoke; candidate.ReleasedHeatJ += step * rates.Heat;
            candidate.ExternalEnthalpyJ += step * rates.Enthalpy; candidate.RadiationEscapedJ += step * rates.Radiation;
            candidate.WallLossJ += step * rates.WallLoss; candidate.AcceptedSubsteps++;
            next = candidate; return true;
        }

        private double Residual(double[] increments, Rates rates, double step, double[] residual)
        {
            var maximum = 0.0;
            for (var i = 0; i < increments.Length; i++)
            {
                residual[i] = increments[i] - step * (gamma - 1) / definition.Cells[i].VolumeM3 * (rates.Energy[2 * i] + rates.Energy[2 * i + 1]);
                maximum = Math.Max(maximum, Math.Abs(residual[i]));
            }
            return maximum;
        }

        private OpeningSlab[] OpeningSlabs(FireCellMetrics[] metrics, ResolvedForcing forcing)
        {
            var result = new List<OpeningSlab>(); var cuts = new double[4];
            for (var i = 0; i < definition.Doors.Length; i++)
            {
                if (forcing.Openings[i] == 0) continue;
                var d = definition.Doors[i]; var from = doorFrom[i]; var to = doorTo[i];
                var bottom = d.BottomElevationM; var top = bottom + d.HeightM; var count = 2; cuts[0] = bottom; cuts[1] = top;
                if (from >= 0) AddCut(cuts, ref count, definition.Cells[from].FloorElevationM + metrics[from].InterfaceHeightM, bottom, top);
                if (to >= 0) AddCut(cuts, ref count, definition.Cells[to].FloorElevationM + metrics[to].InterfaceHeightM, bottom, top);
                Array.Sort(cuts, 0, count);
                for (var j = 0; j + 1 < count; j++)
                {
                    if (cuts[j + 1] - cuts[j] < 1e-12) continue;
                    var middle = (cuts[j] + cuts[j + 1]) / 2;
                    result.Add(new OpeningSlab { From = from, To = to, FromLayer = LayerAt(from, middle, metrics), ToLayer = LayerAt(to, middle, metrics),
                        Bottom = cuts[j], Top = cuts[j + 1], Coefficient = d.DischargeCoefficient * d.WidthM * forcing.Openings[i],
                        PressureOffset = (from < 0 ? definition.AmbientPressurePa : metrics[from].PressurePa) - (to < 0 ? definition.AmbientPressurePa : metrics[to].PressurePa),
                        HeadBottom = HydrostaticDifference(from, to, cuts[j], metrics), HeadTop = HydrostaticDifference(from, to, cuts[j + 1], metrics) });
                }
            }
            return result.ToArray();
        }

        private void Evaluate(FireState current, FireCellMetrics[] metrics, Rates basis, ResolvedForcing forcing, OpeningSlab[] slabs,
            double[] increments, Rates rates, double[,] pressureJacobian = null, double step = 0)
        {
            if (slabs == null) { EvaluateUncached(current, metrics, basis, forcing, increments, rates, pressureJacobian, step); return; }
            rates.Set(basis);
            if (pressureJacobian != null)
            { Array.Clear(pressureJacobian, 0, pressureJacobian.Length); for (var i = 0; i < current.Cells.Length; i++) pressureJacobian[i, i] = 1; }
            for (var i = 0; i < definition.Fans.Length; i++)
            {
                if (!forcing.Fans[i]) continue;
                var fan = definition.Fans[i];
                Transfer(rates, current, metrics, fanFrom[i] < 0 ? -1 : 2 * fanFrom[i] + (fan.FromUpper ? 0 : 1),
                    fanTo[i] < 0 ? -1 : 2 * fanTo[i] + (fan.ToUpper ? 0 : 1), fan.MassFlowKgPerSecond);
            }
            var cuts = new double[5]; var regularizationRoot = Math.Sqrt(options.PressureRegularizationPa);
            foreach (var slab in slabs)
            {
                var delta = (slab.From < 0 ? 0 : increments[slab.From]) - (slab.To < 0 ? 0 : increments[slab.To]);
                double Pressure(double elevation) => slab.PressureOffset + delta - (elevation == slab.Bottom ? slab.HeadBottom : elevation == slab.Top ? slab.HeadTop :
                    slab.HeadBottom + (slab.HeadTop - slab.HeadBottom) * ((elevation - slab.Bottom) / (slab.Top - slab.Bottom)));
                var dl = Pressure(slab.Bottom); var dh = Pressure(slab.Top);
                var count = 2; cuts[0] = slab.Bottom; cuts[1] = slab.Top;
                if (dl != dh)
                    for (var cut = -1; cut <= 1; cut++)
                    {
                        var threshold = cut * options.PressureRegularizationPa;
                        if ((dl - threshold) * (dh - threshold) < 0)
                            AddCut(cuts, ref count, slab.Bottom + (slab.Top - slab.Bottom) * (threshold - dl) / (dh - dl), slab.Bottom, slab.Top);
                    }
                Array.Sort(cuts, 0, count);
                for (var part = 0; part + 1 < count; part++)
                {
                    var height = cuts[part + 1] - cuts[part]; if (height < 1e-12) continue;
                    var a = Pressure(cuts[part]); var b = Pressure(cuts[part + 1]); var forward = a + b >= 0;
                    var donor = forward ? slab.From : slab.To; var receiver = forward ? slab.To : slab.From;
                    var layer = forward ? slab.FromLayer : slab.ToLayer; var destinationLayer = forward ? slab.ToLayer : slab.FromLayer;
                    var rho = donor < 0 ? ambientDensity : layer == 0 ? metrics[donor].UpperDensityKgM3 : metrics[donor].LowerDensityKgM3;
                    var aa = Math.Abs(a); var bb = Math.Abs(b); var denominator = Math.Sqrt(aa) + Math.Sqrt(bb);
                    var linear = Math.Max(aa, bb) <= options.PressureRegularizationPa * (1 + 1e-8);
                    var integral = linear ? height * (aa + bb) / (2 * regularizationRoot) :
                        2 * height / 3 * (aa + Math.Sqrt(aa * bb) + bb) / denominator;
                    var mass = slab.Coefficient * Math.Sqrt(2 * rho) * integral;
                    var temperature = donor < 0 ? definition.AmbientTemperatureK : layer == 0 ? metrics[donor].UpperTemperatureK : metrics[donor].LowerTemperatureK;
                    if (pressureJacobian != null)
                    {
                        var derivative = linear ? height / regularizationRoot : height / denominator;
                        var conductance = slab.Coefficient * Math.Sqrt(2 * rho) * derivative * cp * temperature;
                        if (slab.From >= 0)
                        { var value = step * (gamma - 1) / definition.Cells[slab.From].VolumeM3 * conductance;
                            pressureJacobian[slab.From, slab.From] += value; if (slab.To >= 0) pressureJacobian[slab.From, slab.To] -= value; }
                        if (slab.To >= 0)
                        { var value = step * (gamma - 1) / definition.Cells[slab.To].VolumeM3 * conductance;
                            pressureJacobian[slab.To, slab.To] += value; if (slab.From >= 0) pressureJacobian[slab.To, slab.From] -= value; }
                    }
                    if (receiver >= 0)
                    {
                        if (destinationLayer == 1 && temperature > metrics[receiver].LowerTemperatureK + .01) destinationLayer = 0;
                        else if (destinationLayer == 0 && temperature < metrics[receiver].UpperTemperatureK - .01) destinationLayer = 1;
                    }
                    Transfer(rates, current, metrics, donor < 0 ? -1 : 2 * donor + layer, receiver < 0 ? -1 : 2 * receiver + destinationLayer, mass);
                }
            }
        }

        private void EvaluateUncached(FireState current, FireCellMetrics[] metrics, Rates basis, ResolvedForcing forcing, double[] increments, Rates rates,
            double[,] pressureJacobian = null, double step = 0)
        {
            rates.Set(basis);
            if (pressureJacobian != null)
            { Array.Clear(pressureJacobian, 0, pressureJacobian.Length); for (var i = 0; i < current.Cells.Length; i++) pressureJacobian[i, i] = 1; }
            for (var i = 0; i < definition.Fans.Length; i++)
            {
                var fan = definition.Fans[i];
                if (!forcing.Fans[i]) continue;
                Transfer(rates, current, metrics, fanFrom[i] < 0 ? -1 : 2 * fanFrom[i] + (fan.FromUpper ? 0 : 1),
                    fanTo[i] < 0 ? -1 : 2 * fanTo[i] + (fan.ToUpper ? 0 : 1), fan.MassFlowKgPerSecond);
            }
            var cuts = new double[20];
            for (var i = 0; i < definition.Doors.Length; i++)
            {
                var door = definition.Doors[i];
                var opening = forcing.Openings[i]; if (opening == 0) continue;
                var from = doorFrom[i]; var to = doorTo[i]; var bottom = door.BottomElevationM; var top = bottom + door.HeightM;
                var count = 2; cuts[0] = bottom; cuts[1] = top;
                if (from >= 0) AddCut(cuts, ref count, definition.Cells[from].FloorElevationM + metrics[from].InterfaceHeightM, bottom, top);
                if (to >= 0) AddCut(cuts, ref count, definition.Cells[to].FloorElevationM + metrics[to].InterfaceHeightM, bottom, top);
                Array.Sort(cuts, 0, count); var initialCount = count;
                for (var slab = 0; slab < initialCount - 1; slab++)
                {
                    var low = cuts[slab]; var high = cuts[slab + 1];
                    var dl = PressureDifference(from, to, low, metrics, increments);
                    var dh = PressureDifference(from, to, high, metrics, increments);
                    if (dl != dh)
                        for (var cut = -1; cut <= 1; cut++)
                        {
                            var threshold = cut * options.PressureRegularizationPa;
                            if ((dl - threshold) * (dh - threshold) < 0)
                                AddCut(cuts, ref count, low + (high - low) * (threshold - dl) / (dh - dl), low, high);
                        }
                }
                Array.Sort(cuts, 0, count);
                for (var slab = 0; slab < count - 1; slab++)
                {
                    var low = cuts[slab]; var high = cuts[slab + 1]; if (high - low < 1e-12) continue;
                    var middle = (low + high) / 2;
                    var a = PressureDifference(from, to, low, metrics, increments);
                    var b = PressureDifference(from, to, high, metrics, increments);
                    var forward = a + b >= 0; var donor = forward ? from : to; var receiver = forward ? to : from;
                    var layer = LayerAt(donor, middle, metrics); var rho = donor < 0 ? ambientDensity :
                        (layer == 0 ? metrics[donor].UpperDensityKgM3 : metrics[donor].LowerDensityKgM3);
                    var aa = Math.Abs(a); var bb = Math.Abs(b); var denominator = Math.Sqrt(aa) + Math.Sqrt(bb);
                    var linear = Math.Max(aa, bb) <= options.PressureRegularizationPa * (1 + 1e-8);
                    // Continuous monotone linear core below a declared microscopic pressure scale.
                    // Unlike independent clipping it preserves the paired mass/enthalpy/species flux.
                    var integral = linear ? (high - low) * (aa + bb) / (2 * Math.Sqrt(options.PressureRegularizationPa)) :
                        2 * (high - low) / 3 * (aa + Math.Sqrt(aa * bb) + bb) / denominator;
                    var mass = door.DischargeCoefficient * door.WidthM * opening * Math.Sqrt(2 * rho) * integral;
                    if (pressureJacobian != null)
                    {
                        var temperature = donor < 0 ? definition.AmbientTemperatureK :
                            layer == 0 ? metrics[donor].UpperTemperatureK : metrics[donor].LowerTemperatureK;
                        // Exact derivative of the integrated square-root flux. Neutral-plane boundary
                        // terms vanish because the flux is zero there. Safeguard the zero-gradient limit.
                        var derivative = linear ? (high - low) / Math.Sqrt(options.PressureRegularizationPa) : (high - low) / denominator;
                        var conductance = door.DischargeCoefficient * door.WidthM * opening * Math.Sqrt(2 * rho) * derivative * cp * temperature;
                        if (from >= 0)
                        { var value = step * (gamma - 1) / definition.Cells[from].VolumeM3 * conductance;
                            pressureJacobian[from, from] += value; if (to >= 0) pressureJacobian[from, to] -= value; }
                        if (to >= 0)
                        { var value = step * (gamma - 1) / definition.Cells[to].VolumeM3 * conductance;
                            pressureJacobian[to, to] += value; if (from >= 0) pressureJacobian[to, from] -= value; }
                    }
                    var destinationLayer = LayerAt(receiver, middle, metrics);
                    if (receiver >= 0)
                    {
                        var donorTemperature = donor < 0 ? definition.AmbientTemperatureK :
                            layer == 0 ? metrics[donor].UpperTemperatureK : metrics[donor].LowerTemperatureK;
                        // Buoyant incoming gas rises; cool incoming gas falls. No additional unvalidated spill mixing.
                        if (destinationLayer == 1 && donorTemperature > metrics[receiver].LowerTemperatureK + .01) destinationLayer = 0;
                        else if (destinationLayer == 0 && donorTemperature < metrics[receiver].UpperTemperatureK - .01) destinationLayer = 1;
                    }
                    Transfer(rates, current, metrics, donor < 0 ? -1 : 2 * donor + layer,
                        receiver < 0 ? -1 : 2 * receiver + destinationLayer, mass);
                }
            }
        }

        private void Transfer(Rates rates, FireState current, FireCellMetrics[] metrics, int from, int to, double mass)
        {
            if (mass == 0) return;
            var temperature = from < 0 ? definition.AmbientTemperatureK : (from % 2 == 0 ? metrics[from / 2].UpperTemperatureK : metrics[from / 2].LowerTemperatureK);
            var enthalpy = mass * cp * temperature;
            var smoke = from < 0 ? 0 : mass * (from % 2 == 0 ? current.Cells[from / 2].UpperSmokeKg : current.Cells[from / 2].LowerSmokeKg) /
                LayerMass(current.Cells[from / 2], from % 2);
            if (from >= 0) { rates.Mass[from] -= mass; rates.Energy[from] -= enthalpy; rates.Smoke[from] -= smoke; rates.Outgoing[from] += mass; }
            else { rates.ExternalMass += mass; rates.ExternalEnergy += enthalpy; rates.Enthalpy += enthalpy; rates.ExternalSmoke += smoke; }
            if (to >= 0) { rates.Mass[to] += mass; rates.Energy[to] += enthalpy; rates.Smoke[to] += smoke; }
            else { rates.ExternalMass -= mass; rates.ExternalEnergy -= enthalpy; rates.Enthalpy -= enthalpy; rates.ExternalSmoke -= smoke; }
        }

        private double PressureDifference(int from, int to, double elevation, FireCellMetrics[] metrics, double[] increments)
        {
            // Subtract the large common atmospheric reference before adding tiny implicit increments.
            var pa = from < 0 ? definition.AmbientPressurePa : metrics[from].PressurePa;
            var pb = to < 0 ? definition.AmbientPressurePa : metrics[to].PressurePa;
            var delta = (from < 0 ? 0 : increments[from]) - (to < 0 ? 0 : increments[to]);
            return (pa - pb) + delta - HydrostaticDifference(from, to, elevation, metrics);
        }
        private double HydrostaticDifference(int from, int to, double elevation, FireCellMetrics[] metrics)
        {
            double Head(int cell)
            {
                if (cell < 0) return ambientDensity * elevation;
                var m = metrics[cell]; var height = elevation - definition.Cells[cell].FloorElevationM;
                return m.LowerDensityKgM3 * Math.Min(height, m.InterfaceHeightM) + m.UpperDensityKgM3 * Math.Max(0, height - m.InterfaceHeightM);
            }
            var floorFrom = from < 0 ? 0 : definition.Cells[from].FloorElevationM;
            var floorTo = to < 0 ? 0 : definition.Cells[to].FloorElevationM;
            return definition.GravityMPerS2 *
                (Head(from) - Head(to) + ambientDensity * (floorFrom - floorTo));
        }
        private int LayerAt(int cell, double elevation, FireCellMetrics[] metrics) => cell < 0 ? 1 :
            elevation > definition.Cells[cell].FloorElevationM + metrics[cell].InterfaceHeightM ? 0 : 1;
        private static void AddCut(double[] values, ref int count, double point, double bottom, double top)
        {
            if (point <= bottom || point >= top) return;
            for (var i = 0; i < count; i++) if (values[i] == point) return;
            values[count++] = point;
        }

        private FireCellMetrics Metrics(FireCellState s, FireCellDefinition c)
        {
            var p = (gamma - 1) * (s.UpperEnergyJ + s.LowerEnergyJ) / c.VolumeM3;
            var upper = (gamma - 1) * s.UpperEnergyJ / p; var lower = c.VolumeM3 - upper;
            return new FireCellMetrics(p, s.UpperEnergyJ / (cv * s.UpperMassKg), s.LowerEnergyJ / (cv * s.LowerMassKg),
                c.HeightForLowerVolume(lower, upper), upper, lower, s.UpperMassKg / upper, s.LowerMassKg / lower,
                c.WallHeatCapacityJPerK == 0 ? definition.AmbientTemperatureK : s.WallEnergyJ / c.WallHeatCapacityJPerK);
        }
        private int Index(string id) => id == "" ? -1 : indices.TryGetValue(id, out var index) ? index : throw new ArgumentException("Unknown fire cell.");
        private static double LayerMass(FireCellState c, int layer) => layer == 0 ? c.UpperMassKg : c.LowerMassKg;

        private static void BuildForest(int count, int[] from, int[] to, out int[] parents, out int[] order)
        {
            var adjacent = Enumerable.Range(0, count).Select(_ => new HashSet<int>()).ToArray();
            for (var i = 0; i < from.Length; i++)
                if (from[i] >= 0 && to[i] >= 0) { adjacent[from[i]].Add(to[i]); adjacent[to[i]].Add(from[i]); }
            var parent = Enumerable.Repeat(-2, count).ToArray(); var traversal = new List<int>();
            for (var root = 0; root < count; root++)
            {
                if (parent[root] != -2) continue;
                parent[root] = -1; var queue = new Queue<int>(); queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue(); traversal.Add(node);
                    foreach (var next in adjacent[node].OrderBy(x => x))
                    {
                        if (next == parent[node]) continue;
                        if (parent[next] != -2) { parents = null; order = null; return; }
                        parent[next] = node; queue.Enqueue(next);
                    }
                }
            }
            parents = parent; order = traversal.ToArray();
        }

        // J = I + diag(dt*(gamma-1)/V) L. L is a symmetric nonnegative conductance
        // Laplacian plus ambient diagonals. A forest permits exact leaf Schur elimination;
        // cycles retain pivoted elimination. No pressure tolerance or flux equation changes.
        private bool SolveForest(double[,] matrix, double[] right)
        {
            var diagonal = new double[right.Length];
            for (var i = 0; i < right.Length; i++) diagonal[i] = matrix[i, i];
            for (var at = forestOrder.Length - 1; at >= 0; at--)
            {
                var node = forestOrder[at]; var parent = forestParents[node];
                if (!Finite(diagonal[node]) || diagonal[node] <= 0) return false;
                if (parent < 0) continue;
                var scale = matrix[parent, node] / diagonal[node];
                diagonal[parent] -= scale * matrix[node, parent]; right[parent] -= scale * right[node];
            }
            foreach (var node in forestOrder)
            {
                var parent = forestParents[node];
                right[node] = (right[node] - (parent < 0 ? 0 : matrix[node, parent] * right[parent])) / diagonal[node];
                if (!Finite(right[node])) return false;
            }
            return true;
        }

        private static bool Solve(double[,] a, double[] b)
        {
            var n = b.Length;
            for (var column = 0; column < n; column++)
            {
                var pivot = column; for (var row = column + 1; row < n; row++) if (Math.Abs(a[row, column]) > Math.Abs(a[pivot, column])) pivot = row;
                if (!Finite(a[pivot, column]) || Math.Abs(a[pivot, column]) < 1e-15) return false;
                if (pivot != column)
                {
                    for (var j = column; j < n; j++) { var swap = a[pivot, j]; a[pivot, j] = a[column, j]; a[column, j] = swap; }
                    var value = b[pivot]; b[pivot] = b[column]; b[column] = value;
                }
                for (var row = column + 1; row < n; row++)
                {
                    if (a[row, column] == 0) continue;
                    var scale = a[row, column] / a[column, column]; a[row, column] = 0;
                    for (var j = column + 1; j < n; j++) a[row, j] -= scale * a[column, j];
                    b[row] -= scale * b[column];
                }
            }
            for (var row = n - 1; row >= 0; row--)
            { for (var j = row + 1; j < n; j++) b[row] -= a[row, j] * b[j]; b[row] /= a[row, row]; if (!Finite(b[row])) return false; }
            return true;
        }

        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private static bool Reject(FireStepReport report, string reason) { report.LastRejectedReason = reason; return false; }
        private static bool Positive(double x) => Finite(x) && x > 0;
        private static bool Nonnegative(double x) => Finite(x) && x >= 0;
        private static bool Identifier(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 128;
        private static void ValidateOptions(FireSolverOptions o)
        {
            if (!Positive(o.MaxStepSeconds) || !Positive(o.MinStepSeconds) || o.MinStepSeconds > o.MaxStepSeconds ||
                !Positive(o.PressureTolerancePa) || !Positive(o.CommitEnergyToleranceJ) ||
                !Positive(o.PressureRegularizationPa) || o.PressureRegularizationPa > .01 ||
                !Positive(o.MaxDonorFraction) || o.MaxDonorFraction >= 1 ||
                !Positive(o.MaxRelativeTemperatureChange) || o.MaxRelativeTemperatureChange >= 1 ||
                !Positive(o.MaxLayerVolumeFractionChange) || o.MaxLayerVolumeFractionChange >= 1 ||
                !Positive(o.MinLayerVolumeFraction) || o.MinLayerVolumeFraction >= .01 ||
                o.MaxSubsteps < 1 || o.MaxNewtonIterations < 1 || o.MaxBacktracks < 1)
                throw new ArgumentException("Invalid fire solver options.");
        }
        private static void ValidateDefinition(FireNetworkDefinition d)
        {
            if (d == null || d.SchemaVersion != 1 || !Identifier(d.ModelVersion) ||
                d.PressureConvention != "uniform-thermodynamic-reference-with-ambient-hydrostatic-offset" || !Positive(d.AmbientTemperatureK) ||
                !Positive(d.AmbientPressurePa) || !Positive(d.SpecificHeatCp) || !Finite(d.Gamma) || d.Gamma <= 1 || d.Gamma >= 2 ||
                !Positive(d.GravityMPerS2) || d.Cells == null || d.Cells.Length < 1 || d.Cells.Length > 256 || d.Doors == null || d.Fans == null)
                throw new ArgumentException("Invalid fire network.");
            foreach (var c in d.Cells)
                if (c == null || !Identifier(c.Id) || !Positive(c.WidthM) || !Positive(c.DepthM) || !Positive(c.HeightM) ||
                    !Positive(c.VolumeM3) || !Finite(c.FloorElevationM) || !Finite(c.SurfaceAreaM2) || !Nonnegative(c.FloorRiseM) ||
                    c.FloorRiseM > c.HeightM / 2 || c.FloorRiseM > 0 && (!c.UseExplicitWallAreas || c.WallOpenings == null || c.WallOpenings.Length != 0) ||
                    c.UseExplicitWallAreas && (!Nonnegative(c.FloorWallAreaM2) || !Nonnegative(c.CeilingWallAreaM2) || !Nonnegative(c.VerticalWallLengthM) ||
                        !Nonnegative(c.VerticalWallHeightM) || c.VerticalWallHeightM > c.HeightM || c.WallOpenings == null ||
                        c.WallOpenings.Any(o => o == null || !Positive(o.WidthM) || !Nonnegative(o.BottomM) || !Positive(o.HeightM) || o.BottomM + o.HeightM > c.HeightM + 1e-8) ||
                        c.WallOpenings.Sum(o => o.WidthM) > c.VerticalWallLengthM + 1e-8) ||
                    !Positive(c.InitialUpperVolumeFraction) || c.InitialUpperVolumeFraction >= 1 ||
                    !Nonnegative(c.WallHeatCapacityJPerK) || !Nonnegative(c.InsideHeatTransferWPerM2K) ||
                    !Nonnegative(c.OutsideHeatTransferWPerM2K) || !Positive(c.WallThicknessM) || !Positive(c.WallConductivityWPerMK) ||
                    c.WallHeatCapacityJPerK == 0 && (c.InsideHeatTransferWPerM2K > 0 || c.OutsideHeatTransferWPerM2K > 0))
                    throw new ArgumentException("Invalid fire compartment or wall.");
            if (d.Cells.Select(c => c.Id).Distinct().Count() != d.Cells.Length) throw new ArgumentException("Duplicate fire compartment.");
            bool Endpoint(string id) => id != null && (id == "" || d.Cells.Any(c => c.Id == id));
            foreach (var door in d.Doors)
            {
                if (door == null || !Identifier(door.Id) || !Endpoint(door.FromCellId) || !Endpoint(door.ToCellId) || door.FromCellId == door.ToCellId ||
                    !Positive(door.WidthM) || !Positive(door.HeightM) || !Finite(door.BottomElevationM) || !Positive(door.DischargeCoefficient) ||
                    door.DischargeCoefficient > 1 || !Nonnegative(door.OpeningFraction) || door.OpeningFraction > 1)
                    throw new ArgumentException("Invalid fire opening.");
                foreach (var id in new[] { door.FromCellId, door.ToCellId }.Where(id => id != ""))
                { var c = d.Cells.Single(x => x.Id == id); if (door.BottomElevationM < c.FloorElevationM - 1e-9 ||
                    door.BottomElevationM + door.HeightM > c.FloorElevationM + c.HeightM + 1e-9) throw new ArgumentException("Opening lies outside a compartment."); }
            }
            foreach (var fan in d.Fans)
                if (fan == null || !Identifier(fan.Id) || !Endpoint(fan.FromCellId) || !Endpoint(fan.ToCellId) || fan.FromCellId == fan.ToCellId ||
                    !Nonnegative(fan.MassFlowKgPerSecond)) throw new ArgumentException("Invalid mechanical ventilation.");
            if (d.Doors.Select(x => x.Id).Distinct().Count() != d.Doors.Length || d.Fans.Select(x => x.Id).Distinct().Count() != d.Fans.Length)
                throw new ArgumentException("Duplicate fire flow identifier.");
        }
        private static bool ValidCell(FireCellState s, FireCellDefinition c) => s != null && s.CellId == c.Id &&
            Positive(s.UpperMassKg) && Positive(s.LowerMassKg) && Positive(s.UpperEnergyJ) && Positive(s.LowerEnergyJ) &&
            Nonnegative(s.UpperSmokeKg) && Nonnegative(s.LowerSmokeKg) && s.UpperSmokeKg <= s.UpperMassKg && s.LowerSmokeKg <= s.LowerMassKg &&
            (c.WallHeatCapacityJPerK == 0 ? s.WallEnergyJ == 0 : Positive(s.WallEnergyJ));
        private void ValidateState(FireState s)
        {
            if (s == null || s.SchemaVersion != 1 || s.ModelVersion != definition.ModelVersion || s.Cells == null || s.Cells.Length != definition.Cells.Length ||
                !Nonnegative(s.SimulatedSeconds) || s.AcceptedSubsteps < 0 || s.ConservativeRepartitions < 0 ||
                !new[] { s.ExternalMassKg, s.ExternalEnergyJ, s.ExternalSmokeKg, s.ReleasedHeatJ, s.ExternalEnthalpyJ, s.RadiationEscapedJ, s.WallLossJ }.All(Finite))
                throw new ArgumentException("Invalid saved fire state.");
            for (var i = 0; i < s.Cells.Length; i++)
            {
                if (!ValidCell(s.Cells[i], definition.Cells[i])) throw new ArgumentException("Invalid saved fire layer.");
                var m = Metrics(s.Cells[i], definition.Cells[i]);
                if (!new[] { m.PressurePa, m.UpperTemperatureK, m.LowerTemperatureK, m.UpperVolumeM3, m.LowerVolumeM3,
                    m.UpperDensityKgM3, m.LowerDensityKgM3, m.WallTemperatureK }.All(Positive)) throw new ArgumentException("Nonfinite fire thermodynamics.");
            }
        }
        private void ValidateForcing(FireForcing f)
        {
            if (f == null || f.Sources == null || f.Doors == null || f.Fans == null) throw new ArgumentException("Invalid fire forcing.");
            foreach (var s in f.Sources)
                if (s == null || !indices.ContainsKey(s.CellId ?? "") || !Nonnegative(s.HeatReleaseW) || !Nonnegative(s.FuelMassKgPerSecond) ||
                    !Nonnegative(s.SmokeMassKgPerSecond) || s.SmokeMassKgPerSecond > s.FuelMassKgPerSecond ||
                    !Nonnegative(s.RadiationFraction) || s.RadiationFraction > 1 || !Positive(s.PlumeAreaM2) || !Positive(s.PlumeMultiplier) ||
                    !Nonnegative(s.HeightM) || s.HeightM >= definition.Cells[Index(s.CellId)].HeightM) throw new ArgumentException("Invalid prescribed fire source.");
            if (f.Doors.Any(d => d == null || !definition.Doors.Any(x => x.Id == d.DoorId) || !Nonnegative(d.OpeningFraction) || d.OpeningFraction > 1) ||
                f.Fans.Any(d => d == null || !definition.Fans.Any(x => x.Id == d.FanId)) ||
                f.Doors.Select(d => d.DoorId).Distinct().Count() != f.Doors.Length || f.Fans.Select(d => d.FanId).Distinct().Count() != f.Fans.Length)
                throw new ArgumentException("Unknown or duplicate fire control.");
        }
    }
}
