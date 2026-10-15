#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace ChooGuard.Editor.Twin
{
    // A source-visible open room contradicts one older inferred divider. This
    // bounded layer runs after the existing stair/office composition.
    public static class Main2fDividerRetentionRegistry
    {
        public const string Registry = "content/world/observed-interiors/main2f-source-divider-retention.json";
        public const string Source = "content/world/observed-interiors/main2f-source-divider-retention-source.json";
        public const string Schema = "chooguard.main2f-source-divider-retention.v1";
        static void Require(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }
        public static JObject Read()
        {
            if (!File.Exists(Registry)) return null;
            var p = JObject.Parse(File.ReadAllText(Registry));
            Require((string)p["schema"] == Schema && (string)p["source"] == Source &&
                StairInfillOmissionRegistry.Hash(Source) == (string)p["source_sha256"], "Divider retention authority changed");
            var s = JObject.Parse(File.ReadAllText(Source));
            Require((int)s["released_triangles"] == 18 && s["meshes"].Count() == 3 &&
                StairInfillOmissionRegistry.Hash((string)s["source_plan"]) == (string)s["source_plan_sha256"], "Divider plan changed");
            var rows = p["entries"] as JArray;
            Require(rows != null && rows.Count == 3 && rows.Select(r => (string)r["generation_source_asset"]).Distinct().Count() == 3 &&
                rows.Select(r => (string)r["role"]).OrderBy(x => x).SequenceEqual(new[]{"mesh-1409", "mesh-3372", "mesh-3448"}), "Exact three divider sources required");
            return p;
        }
        static JToken Row(JObject p, string source) => p?["entries"]?.SingleOrDefault(r => (string)r["generation_source_asset"] == source);
        public static string[] InstalledAssetsForRoot(string root) => root.Trim('/') == StairInfillOmissionRegistry.Root && Read() is JObject p
            ? p["entries"].Select(r => (string)r["replacement_asset"]).ToArray() : Array.Empty<string>();
        public static Mesh RemoveExact(Mesh source, JToken proof)
        {
            Require(source != null && source.isReadable && source.subMeshCount == 1 && source.GetTopology(0) == MeshTopology.Triangles &&
                source.vertexCount == (int)proof["source_vertex_count"] && source.triangles.Length / 3 == (int)proof["source_triangle_count"] &&
                ArchitecturalCorrectionRegistry.CompleteChannelDigest(source) == (string)proof["source_complete_channels_sha256"], "Divider baseline channels changed");
            int[] indices = source.GetTriangles(0), removed = proof["remove_triangle_ordinals"].ToObject<int[]>();
            Require(removed.Distinct().Count() == removed.Length && removed.All(i => i >= 0 && i < indices.Length / 3) &&
                removed.Length == proof["remove_source_indices"].Count(), "Invalid divider triangle selection");
            for (int k = 0; k < removed.Length; k++)
                Require(indices.Skip(removed[k] * 3).Take(3).SequenceEqual(proof["remove_source_indices"][k].ToObject<int[]>()), "Divider ordered triangle changed");
            var clone = UnityEngine.Object.Instantiate(source);
            try
            {
                Require(ArchitecturalCorrectionRegistry.CompleteChannelDigest(clone) == ArchitecturalCorrectionRegistry.CompleteChannelDigest(source), "Divider clone changed channels");
                var selected = removed.ToHashSet();
                clone.SetTriangles(Enumerable.Range(0, indices.Length / 3).Where(i => !selected.Contains(i)).SelectMany(i => indices.Skip(i * 3).Take(3)).ToArray(), 0, false);
                Require(clone.vertexCount == source.vertexCount && clone.indexFormat == source.indexFormat &&
                    clone.GetVertexAttributes().SequenceEqual(source.GetVertexAttributes()) && clone.bounds == source.bounds &&
                    ArchitecturalCorrectionRegistry.CompleteChannelDigest(clone) == (string)proof["retained_complete_channels_sha256"], "Divider retained channels changed");
                clone.name = source.name + "_SourceOpenRoom"; clone.hideFlags = HideFlags.None;
                return clone;
            }
            catch { UnityEngine.Object.DestroyImmediate(clone); throw; }
        }
        public static Mesh ResolveComposed(Mesh generated, string source)
        {
            var p = Read(); var row = Row(p, source); if (row == null) return generated;
            var baseline = StairInfillOmissionRegistry.CheckedMesh(row, "baseline");
            var target = StairInfillOmissionRegistry.CheckedMesh(row, "replacement");
            var proof = JObject.Parse(File.ReadAllText(Source))["meshes"].Single(s => (string)s["role"] == (string)row["role"]);
            Require((string)row["baseline_asset"] == (string)proof["source_asset"] &&
                (string)row["baseline_complete_digest"] == (string)proof["source_complete_channels_sha256"] &&
                (string)row["replacement_complete_digest"] == (string)proof["retained_complete_channels_sha256"], "Divider source binding changed");
            var expected = RemoveExact(baseline, proof);
            try
            {
                Require(ArchitecturalCorrectionRegistry.CompleteChannelDigest(expected) == ArchitecturalCorrectionRegistry.CompleteChannelDigest(target), "Divider persisted retention changed");
            }
            finally { UnityEngine.Object.DestroyImmediate(expected); }
            string digest = generated == null ? null : ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated);
            Require(digest == (string)row["baseline_complete_digest"] || digest == (string)row["replacement_complete_digest"], "Unreviewed pre-divider composition");
            return target;
        }
    }
}
#endif
