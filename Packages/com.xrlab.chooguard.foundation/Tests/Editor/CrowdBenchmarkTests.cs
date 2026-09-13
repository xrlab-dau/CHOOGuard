using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    public sealed class CrowdBenchmarkTests
    {
        [Serializable] private sealed class Dataset { public int SchemaVersion; public string AdapterVersion; public Case[] Cases; }
        [Serializable] private sealed class Case
        { public string Id, Role, TrajectorySha256; public int OriginalParticipants, MeasuredEvents; public double MeanDensityPM, MeanSpeedMS; }
        [Serializable] private sealed class Sample { public int Frame; public double DensityPM, SpeedMS; }
        [Serializable] private sealed class Comparison
        {
            public string Id, Role, TrajectorySha256;
            public int OriginalParticipants, SimulatedParticipants, ReferenceEvents, SimulatedEvents, MaximumProjectionIterations;
            public double ConditionalRingLengthM, InputDensityPM, MeasuredDensityPM, DensityErrorPM;
            public double ReferenceSpeedMS, SimulatedSpeedMS, SpeedErrorMS, ElapsedWallMilliseconds;
            public double MaximumConstraintViolationMS, MinimumSweptGapM;
            public Sample[] Samples;
        }
        [Serializable] private sealed class SourceHash { public string Path, Sha256; }
        [Serializable] private sealed class Result
        {
            public string ModelVersion = CrowdMotionModel.Version;
            public string Classification = "conditional_uniform_corridor_comparison_no_empirical_pass_threshold";
            public string Limit = "Measured local mean density conditions an equally spaced periodic synthetic corridor, not recorded initial trajectories. Original participant counts retained; conditional ring lengths are inferred, not facility dimensions. Exact published stationary frame ranges unavailable: source aggregates use all complete recorded crossing windows, model runs20s after0.2s warmup. Density sampling error is reported. No bottleneck/field acceptance.";
            public string Calibration = "Whole runs n14,n25,n39,n62 only; fixed radius0.15/person repulsion5/range0.1; grid v0[.8,1.6] T[.4,1.6] step.005 minimizing equal-run mean-speed squared equilibrium error. Same frozen profile used for9 holdout runs. No tolerance fitted after results.";
            public string Operator = "25fps, negative x=0 crossing, preceding integer frame, [-2,2]m occupancy, all occupant Euclidean speed frame+/-5 over0.4s; incomplete windows excluded.";
            public double RadiusM = .15, PreferredSpeedMS, TimeGapSeconds, CalibrationEquilibriumRmseMS, CalibrationActualRmseMS, HoldoutActualRmseMS, HoldoutMeanAbsoluteErrorMS;
            public double SimulatedSecondsPerRun = 20, MaxSubstepSeconds = .01;
            public SourceHash[] SourceHashes;
            public Comparison[] Cases;
        }

        [Test, Explicit("Runs actual C# conditional corridor trajectories and exports measured errors; not independent trajectory/field validation.")]
        public void ExportPinnedJuelichConditionalCorridorComparisons()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            const string dataPath = "foundation/physics/validation/juelich-corridor-aggregates.json";
            var data = JsonUtility.FromJson<Dataset>(File.ReadAllText(Path.Combine(root, dataPath)));
            Assert.That(data.SchemaVersion, Is.EqualTo(1)); Assert.That(data.AdapterVersion, Is.EqualTo("juelich-camera1-txt-si-method-b-1"));
            Assert.That(data.Cases.Length, Is.EqualTo(13));
            Assert.That(data.Cases.Where(c => c.Role == "calibration").Select(c => c.Id), Is.EqualTo(new[] { "n14", "n25", "n39", "n62" }));
            Assert.That(data.Cases.Where(c => c.Id.StartsWith("n17", StringComparison.Ordinal)).All(c => c.Role == "holdout"), Is.True);
            var result = new Result(); var bestLoss = double.PositiveInfinity;
            // This analytical equilibrium fit is explicitly separate from the actual C# trajectory evaluation below.
            for (var speedIndex = 0; speedIndex <= 160; speedIndex++)
            for (var gapIndex = 0; gapIndex <= 240; gapIndex++)
            {
                var speed = .8 + speedIndex * .005; var timeGap = .4 + gapIndex * .005;
                var loss = data.Cases.Where(c => c.Role == "calibration").Average(c =>
                    Math.Pow(Math.Min(speed, Math.Max(0, (1 / c.MeanDensityPM - .3) / timeGap)) - c.MeanSpeedMS, 2));
                if (loss >= bestLoss) continue;
                bestLoss = loss; result.PreferredSpeedMS = speed; result.TimeGapSeconds = timeGap;
            }
            result.CalibrationEquilibriumRmseMS = Math.Sqrt(bestLoss);
            var comparisons = new List<Comparison>();
            foreach (var reference in data.Cases)
                comparisons.Add(Simulate(reference, result.PreferredSpeedMS, result.TimeGapSeconds));
            result.Cases = comparisons.ToArray();
            result.CalibrationActualRmseMS = Math.Sqrt(result.Cases.Where(c => c.Role == "calibration").Average(c => c.SpeedErrorMS * c.SpeedErrorMS));
            result.HoldoutActualRmseMS = Math.Sqrt(result.Cases.Where(c => c.Role == "holdout").Average(c => c.SpeedErrorMS * c.SpeedErrorMS));
            result.HoldoutMeanAbsoluteErrorMS = result.Cases.Where(c => c.Role == "holdout").Average(c => Math.Abs(c.SpeedErrorMS));
            result.SourceHashes = new[] { dataPath,
                "Packages/com.xrlab.chooguard.foundation/Runtime/Simulation/CrowdContracts.cs",
                "Packages/com.xrlab.chooguard.foundation/Runtime/Simulation/CrowdMotionModel.cs",
                "Packages/com.xrlab.chooguard.foundation/Runtime/Simulation/CrowdCheckpoint.cs",
                "Packages/com.xrlab.chooguard.foundation/Tests/Editor/CrowdBenchmarkTests.cs",
                "scripts/dev/fetch_crowd_benchmarks.py" }.Select(path => new SourceHash { Path = path, Sha256 = Hash(Path.Combine(root, path)) }).ToArray();
            var output = Path.Combine(root, "Temp/ChooGuardCrowdBenchmarks"); Directory.CreateDirectory(output);
            var outputPath = Path.Combine(output, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + ".json");
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true) + "\n");
            TestContext.Out.WriteLine($"crowd_benchmark={outputPath}; holdout_rmse_ms={result.HoldoutActualRmseMS:R}; holdout_mae_ms={result.HoldoutMeanAbsoluteErrorMS:R}");
        }

        private static Comparison Simulate(Case reference, double preferredSpeed, double timeGap)
        {
            var n = reference.OriginalParticipants; var length = n / reference.MeanDensityPM;
            var definition = new CrowdDefinition { ProfileId = "juelich-conditional-uniform-v1",
                Parameters = new CrowdParameters { TimeGapSeconds = timeGap }, Spaces = new[] { new CrowdSpace { Id = "corridor", PeriodicLengthXM = length } } };
            var agents = Enumerable.Range(0, n).Select(i => new CrowdAgent { Id = i.ToString("D3"), ContactSpaceId = "corridor", RegionId = "conditional", FrameId = "world", SurfaceId = "plane",
                RadiusM = .15, PreferredSpeedMS = preferredSpeed, Position = new CrowdVector((i + .5) * length / n, 0), Goal = new CrowdVector(-1000000, 0) }).ToArray();
            var model = new CrowdMotionModel(definition, agents, 20061127); var frames = new List<CrowdVector[]>();
            var timer = Stopwatch.StartNew(); var maxResidual = 0.0; var minGap = double.PositiveInfinity; var iterations = 0;
            for (var frame = 0; frame <= 510; frame++)
            {
                frames.Add(model.ExportSnapshot().Agents.Select(a => a.Position - new CrowdVector(length / 2, 0)).ToArray());
                if (frame == 510) break;
                Assert.That(model.TryAdvance(.04, null, out var report), Is.True, reference.Id + ": " + report.Failure);
                maxResidual = Math.Max(maxResidual, report.MaximumConstraintViolationMS); minGap = Math.Min(minGap, report.MinimumSweptGapM);
                iterations = Math.Max(iterations, report.MaximumProjectionIterations);
            }
            timer.Stop(); var samples = new List<Sample>();
            for (var frame = 5; frame <= 505; frame++)
            {
                var occupants = Enumerable.Range(0, n).Where(i => frames[frame][i].X >= -2 && frames[frame][i].X <= 2).ToArray();
                for (var i = 0; i < n; i++)
                {
                    if (!(frames[frame][i].X >= 0 && frames[frame + 1][i].X < 0 && frames[frame][i].X - frames[frame + 1][i].X < length / 2)) continue;
                    Assert.That(occupants.Length, Is.GreaterThan(0));
                    var speed = occupants.Average(j => {
                        var delta = frames[frame + 5][j] - frames[frame - 5][j]; delta.X -= Math.Round(delta.X / length) * length;
                        return delta.Length / .4;
                    });
                    samples.Add(new Sample { Frame = frame, DensityPM = occupants.Length / 4.0, SpeedMS = speed });
                }
            }
            Assert.That(samples.Count, Is.GreaterThan(0), reference.Id);
            var measuredDensity = samples.Average(s => s.DensityPM); var simulatedSpeed = samples.Average(s => s.SpeedMS);
            Assert.That(minGap, Is.GreaterThanOrEqualTo(-definition.Parameters.GeometryToleranceM));
            Assert.That(maxResidual, Is.LessThanOrEqualTo(definition.Parameters.VelocityToleranceMS));
            return new Comparison { Id = reference.Id, Role = reference.Role, TrajectorySha256 = reference.TrajectorySha256,
                OriginalParticipants = n, SimulatedParticipants = n, ReferenceEvents = reference.MeasuredEvents, SimulatedEvents = samples.Count,
                ConditionalRingLengthM = length, InputDensityPM = reference.MeanDensityPM, MeasuredDensityPM = measuredDensity,
                DensityErrorPM = measuredDensity - reference.MeanDensityPM, ReferenceSpeedMS = reference.MeanSpeedMS,
                SimulatedSpeedMS = simulatedSpeed, SpeedErrorMS = simulatedSpeed - reference.MeanSpeedMS,
                ElapsedWallMilliseconds = timer.Elapsed.TotalMilliseconds, MaximumProjectionIterations = iterations,
                MaximumConstraintViolationMS = maxResidual, MinimumSweptGapM = minGap, Samples = samples.ToArray() };
        }
        private static string Hash(string path)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    }
}
