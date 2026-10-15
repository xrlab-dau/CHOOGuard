using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Twin;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Imports physically verified placement records without changing their surveyed pose.</summary>
    public static class MeasuredTwinImport
    {
        private static string ProjectRoot => Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        private static readonly Dictionary<string, ExportedPlacement> regenerated = new Dictionary<string, ExportedPlacement>(StringComparer.Ordinal);

        public static bool VerifyAgainstManifest(EquipmentPlacement item, out string reason)
        {
            reason = "";
            if (item == null || !item.HasPlacementEvidence) { reason = item == null ? "null_placement" : item.VerificationIssue; return false; }
            if (!VerifySources(item.verification, out reason)) return false;
            string key = item.verification.manifest_sha256 + ":" + item.id;
            if (!regenerated.TryGetValue(key, out var expected))
            {
                string python = Path.Combine(ProjectRoot, ".tools/video-reconstruction/bin/python");
                string worker = Path.Combine(ProjectRoot, "workers/twin/metric_pipeline.py");
                if (!File.Exists(python) || !File.Exists(worker)) { reason = "local_metric_worker_unavailable"; return false; }
                string output = Path.Combine(ProjectRoot, "Temp", "metric-import-" + Guid.NewGuid().ToString("N") + ".json");
                try
                {
                    var start = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = python, WorkingDirectory = ProjectRoot, UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true,
                        Arguments = Arg(worker) + " export-placement --manifest " + Arg(Path.Combine(ProjectRoot, item.verification.manifest_path)) + " --object-id " + Arg(item.id) + " --output " + Arg(output)
                    };
                    using (var process = System.Diagnostics.Process.Start(start))
                    {
                        var stdout = process.StandardOutput.ReadToEndAsync();
                        var stderr = process.StandardError.ReadToEndAsync();
                        if (!process.WaitForExit(30000)) { process.Kill(); reason = "metric_verification_timeout"; return false; }
                        if (process.ExitCode != 0 || !File.Exists(output)) { reason = "manifest_metric_validation_failed"; return false; }
                        expected = JsonUtility.FromJson<ExportedPlacement>(File.ReadAllText(output));
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is System.ComponentModel.Win32Exception || exception is InvalidOperationException)
                { reason = "metric_worker_failed"; return false; }
                finally { if (File.Exists(output)) File.Delete(output); }
                if (expected == null || expected.schema != "chooguard.verified-placement.v1" || expected.placement == null || expected.verification == null)
                { reason = "invalid_worker_verification"; return false; }
                regenerated[key] = expected;
            }
            if (expected.object_id != item.id || Vector3.Distance(expected.placement.position, item.position) > .0001f ||
                Vector3.Distance(expected.placement.rotation, item.rotation) > .0001f ||
                JsonUtility.ToJson(expected.verification) != JsonUtility.ToJson(item.verification))
            { reason = "export_does_not_match_retained_manifest"; return false; }
            return true;
        }

        private static string Arg(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static void ValidateGroup(string group)
        {
            if (string.IsNullOrWhiteSpace(group) || group == "." || group == ".." || group.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || group.Contains("/") || group.Contains("\\"))
                throw new ArgumentException("Invalid placement group.", nameof(group));
        }

        public static bool VerifySources(PlacementVerification verification, out string reason)
        {
            reason = "";
            if (verification == null) { reason = "missing_verification"; return false; }
            if (!VerifyFile(verification.manifest_path, verification.manifest_sha256, out reason)) return false;
            foreach (var source in verification.sources ?? Array.Empty<PlacementVerification.EvidenceSource>())
                if (source == null || !VerifyFile(source.path, source.sha256, out reason)) return false;
            return true;
        }

        private static bool VerifyFile(string relativePath, string expectedHash, out string reason)
        {
            reason = "";
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || !PlacementVerification.Hash(expectedHash))
            { reason = "invalid_evidence_path_or_hash"; return false; }
            try
            {
            string path = Path.GetFullPath(Path.Combine(ProjectRoot, relativePath));
            if (!path.StartsWith(ProjectRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path))
            { reason = "missing_local_evidence:" + relativePath; return false; }
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
                { reason = "evidence_hash_changed:" + relativePath; return false; }
            }
            return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            { reason = "unreadable_evidence:" + relativePath; return false; }
        }

        public static string ImportPlacements(string inputPath, string group)
        {
            ValidateGroup(group);
            var input = JsonUtility.FromJson<EquipmentPlacementFile>(File.ReadAllText(inputPath));
            if (input == null || input.items == null) throw new InvalidDataException("Missing placement items.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in input.items)
            {
                if (item == null || !ids.Add(item.id)) throw new InvalidDataException("Null or duplicate placement.");
                if (!item.HasPlacementEvidence) throw new InvalidDataException(item.id + ": " + item.VerificationIssue);
                if (!VerifyAgainstManifest(item, out string issue)) throw new InvalidDataException(item.id + ": " + issue);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentBuilder.PrefabRoot + "/" + item.prefab + ".prefab");
                if (prefab == null) throw new InvalidDataException("Missing measured model prefab: " + item.prefab);
                if (!VerifyModelDimensions(prefab, item.verification, out issue)) throw new InvalidDataException(item.id + ": " + issue);
            }
            EquipmentBuilder.WritePlacements(group, "Independent field-survey verification; exact validated poses preserved.", input.items);
            return "CG_MEASURED_PLACEMENTS imported=" + input.items.Length + " group=" + group;
        }

        public static bool VerifyModelDimensions(GameObject prefab, PlacementVerification proof, out string reason)
        {
            return proof != null ? proof.ValidateModelDimensions(prefab, out reason) : FailMissing(out reason);
        }

        private static bool FailMissing(out string reason) { reason = "missing_verification"; return false; }

        [Serializable] public sealed class ExportedPlacement
        {
            public string schema, object_id;
            public Pose placement;
            public PlacementVerification verification;
            [Serializable] public sealed class Pose { public Vector3 position, rotation; }
        }

        public static EquipmentPlacement ReadVerifiedObject(string inputPath, string prefab, string kind, string label, string zone)
        {
            var export = JsonUtility.FromJson<ExportedPlacement>(File.ReadAllText(inputPath));
            if (export == null || export.schema != "chooguard.verified-placement.v1" || export.placement == null)
                throw new InvalidDataException("Unsupported placement export.");
            var item = new EquipmentPlacement { id = export.object_id, prefab = prefab, kind = kind, label = label, zone = zone,
                position = export.placement.position, rotation = export.placement.rotation, verification = export.verification };
            if (!item.HasPlacementEvidence) throw new InvalidDataException(item.id + ": " + item.VerificationIssue);
            return item;
        }

        public static string ImportVerifiedObject(string inputPath, string group, string prefab, string kind, string label, string zone)
        {
            ValidateGroup(group);
            var item = ReadVerifiedObject(inputPath, prefab, kind, label, zone);
            if (!VerifyAgainstManifest(item, out var issue)) throw new InvalidDataException(item.id + ": " + issue);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentBuilder.PrefabRoot + "/" + item.prefab + ".prefab");
            if (model == null || !VerifyModelDimensions(model, item.verification, out issue)) throw new InvalidDataException(item.id + ": " + issue);
            string path = EquipmentBuilder.PlacementPath(group);
            var prior = File.Exists(path) ? JsonUtility.FromJson<EquipmentPlacementFile>(File.ReadAllText(path)).items : Array.Empty<EquipmentPlacement>();
            var items = new List<EquipmentPlacement>(prior ?? Array.Empty<EquipmentPlacement>());
            if (items.Exists(i => i != null && i.id == item.id)) throw new InvalidDataException("Object already placed: " + item.id);
            items.Add(item);
            EquipmentBuilder.WritePlacements(group, "Independent field-survey verification; exact validated poses preserved.", items);
            return "CG_MEASURED_PLACEMENTS imported=1 group=" + group;
        }
    }
}
