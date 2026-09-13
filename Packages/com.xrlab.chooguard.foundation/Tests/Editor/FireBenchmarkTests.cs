using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    public sealed class FireBenchmarkTests
    {
        [Serializable] private sealed class Dataset { public int SchemaVersion; public Benchmark[] Cases; }
        [Serializable] private sealed class Source { public string Url, Sha256; }
        [Serializable] private sealed class Velocity { public double ElevationM, CenterVelocityMS; public bool HasCenterVelocity; }
        [Serializable] private sealed class Benchmark
        {
            public string Id, Role; public Source[] Sources;
            public double DurationSeconds, SteadyWindowStartSeconds, ReferenceUpperC, ReferenceLowerC, ReferenceInterfaceM;
            public FireNetworkDefinition Definition; public FireForcing Forcing; public Velocity[] VelocitySamples;
        }
        [Serializable] private sealed class Trace
        {
            public double Seconds, UpperC, LowerC, InterfaceM, ReferencePressurePa;
        }
        [Serializable] private sealed class Comparison
        {
            public string Id, Role, Status = "comparison_completed_no_empirical_acceptance_threshold";
            public double ReferenceUpperC, ReferenceLowerC, ReferenceInterfaceM, PredictedUpperC, PredictedLowerC, PredictedInterfaceM;
            public double UpperErrorC, LowerErrorC, InterfaceErrorM, CenterJetRmseMS, NominalAreaMeanRmseMS;
            public double MassBalanceErrorKg, EnergyBalanceErrorJ, SmokeBalanceErrorKg, MaximumPressureResidualPa, MaximumCommittedEnergyResidualJ;
            public long ConservativeRepartitions, AcceptedSubsteps; public int FiniteVelocitySamples;
            public Trace[] History; public Source[] Sources;
        }
        [Serializable] private sealed class FileHash { public string Path, Sha256; }
        [Serializable] private sealed class Result
        {
            public string Classification = "synthetic_model_vs_public_experiment_comparison";
            public string Limit = "Calibration case is not independent validation; no posthoc empirical PASS threshold. Actual facility/SOP/corridor/train applicability is unverified.";
            public string ModelVersion = "conservative-two-zone-1";
            public double MaxStepSeconds = .05, PressureTolerancePa = 1e-7, PressureRegularizationPa = .0001, CommitEnergyToleranceJ = .001;
            public FileHash[] SourceHashes; public Comparison[] Cases;
        }

        [Test, Explicit("Runs 3 x 1800 simulated seconds and writes a comparison artifact; not a field acceptance test.")]
        public void ExportPinnedStecklerComparisonsAndNumericalBalances()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            const string datasetPath = "foundation/physics/validation/steckler-cases.json";
            var data = JsonUtility.FromJson<Dataset>(File.ReadAllText(Path.Combine(root, datasetPath)));
            Assert.That(data.SchemaVersion, Is.EqualTo(1)); Assert.That(data.Cases.Select(c => c.Id), Is.EqualTo(new[] { "Steckler_010", "Steckler_014", "Steckler_020" }));
            Assert.That(data.Cases.Select(c => c.Role), Is.EqualTo(new[] { "calibration", "holdout", "holdout" }));
            var comparisons = new List<Comparison>();
            foreach (var benchmark in data.Cases)
            {
                var model = new ZoneFireModel(benchmark.Definition); var before = model.ExportState();
                var history = new List<Trace>(); var maxPressure = 0.0; var maxCommittedEnergy = 0.0;
                for (var step = 1; step <= (int)(benchmark.DurationSeconds * 2); step++)
                {
                    Assert.That(model.TryAdvance(.5, benchmark.Forcing, out var report), Is.True, benchmark.Id + ": " + report?.Failure);
                    maxPressure = Math.Max(maxPressure, report.MaximumPressureResidualPa);
                    maxCommittedEnergy = Math.Max(maxCommittedEnergy, report.MaximumCommittedPressureEnergyResidualJ);
                    if (step % 20 != 0) continue;
                    var m = model.ReadCell("room"); history.Add(new Trace { Seconds = step * .5, UpperC = m.UpperTemperatureK - 273.15,
                        LowerC = m.LowerTemperatureK - 273.15, InterfaceM = m.InterfaceHeightM, ReferencePressurePa = m.PressurePa });
                }
                var steady = history.Where(t => t.Seconds >= benchmark.SteadyWindowStartSeconds).ToArray();
                var upper = steady.Average(t => t.UpperC); var lower = steady.Average(t => t.LowerC); var height = steady.Average(t => t.InterfaceM);
                var after = model.ExportState(); var door = benchmark.Definition.Doors.Single();
                var velocities = benchmark.VelocitySamples.Where(v => v.HasCenterVelocity && v.ElevationM >= door.BottomElevationM && v.ElevationM <= door.BottomElevationM + door.HeightM).ToArray();
                var jetSquared = new List<double>(); var meanSquared = new List<double>();
                foreach (var reference in velocities)
                {
                    var sample = model.SampleOpening("door", reference.ElevationM, benchmark.Forcing);
                    jetSquared.Add(Math.Pow(sample.JetSpeedMS - reference.CenterVelocityMS, 2));
                    meanSquared.Add(Math.Pow(sample.NominalAreaMeanSpeedMS - reference.CenterVelocityMS, 2));
                }
                double Mass(FireState s) => s.Cells.Sum(c => c.UpperMassKg + c.LowerMassKg);
                double Energy(FireState s) => s.Cells.Sum(c => c.UpperEnergyJ + c.LowerEnergyJ + c.WallEnergyJ);
                double Smoke(FireState s) => s.Cells.Sum(c => c.UpperSmokeKg + c.LowerSmokeKg);
                var comparison = new Comparison { Id = benchmark.Id, Role = benchmark.Role, Sources = benchmark.Sources,
                    ReferenceUpperC = benchmark.ReferenceUpperC, ReferenceLowerC = benchmark.ReferenceLowerC, ReferenceInterfaceM = benchmark.ReferenceInterfaceM,
                    PredictedUpperC = upper, PredictedLowerC = lower, PredictedInterfaceM = height,
                    UpperErrorC = upper - benchmark.ReferenceUpperC, LowerErrorC = lower - benchmark.ReferenceLowerC,
                    InterfaceErrorM = height - benchmark.ReferenceInterfaceM, CenterJetRmseMS = Math.Sqrt(jetSquared.Average()),
                    NominalAreaMeanRmseMS = Math.Sqrt(meanSquared.Average()), FiniteVelocitySamples = velocities.Length,
                    MassBalanceErrorKg = Mass(after) - Mass(before) - after.ExternalMassKg,
                    EnergyBalanceErrorJ = Energy(after) - Energy(before) - after.ExternalEnergyJ,
                    SmokeBalanceErrorKg = Smoke(after) - Smoke(before) - after.ExternalSmokeKg,
                    MaximumPressureResidualPa = maxPressure, MaximumCommittedEnergyResidualJ = maxCommittedEnergy,
                    ConservativeRepartitions = after.ConservativeRepartitions, AcceptedSubsteps = after.AcceptedSubsteps, History = history.ToArray() };
                Assert.That(Math.Abs(comparison.MassBalanceErrorKg), Is.LessThan(1e-6));
                Assert.That(Math.Abs(comparison.EnergyBalanceErrorJ), Is.LessThan(.05));
                Assert.That(Math.Abs(comparison.SmokeBalanceErrorKg), Is.LessThan(1e-8));
                Assert.That(comparison.FiniteVelocitySamples, Is.EqualTo(16)); comparisons.Add(comparison);
            }
            var paths = new[] { datasetPath, "Packages/com.xrlab.chooguard.foundation/Runtime/Simulation/FireContracts.cs",
                "Packages/com.xrlab.chooguard.foundation/Runtime/Simulation/ZoneFireModel.cs",
                "Packages/com.xrlab.chooguard.foundation/Tests/Editor/FireBenchmarkTests.cs" };
            var result = new Result { Cases = comparisons.ToArray(), SourceHashes = paths.Select(path => {
                using var hash = SHA256.Create(); return new FileHash { Path = path,
                    Sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(root, path)))).Replace("-", "").ToLowerInvariant() };
            }).ToArray() };
            var directory = Path.Combine(root, "Temp/ChooGuardFireBenchmarks"); Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + ".json");
            File.WriteAllText(output, JsonUtility.ToJson(result, true));
            Debug.Log("Fire experiment comparison written: " + output + ". No empirical/field acceptance claimed.");
        }
    }
}
