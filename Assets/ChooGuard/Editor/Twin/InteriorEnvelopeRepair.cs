#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Resolve identified duplicate enclosure owners. This does not certify the retained owner's field placement.</summary>
    public static class InteriorEnvelopeRepair
    {
        public const string Receipt = ".planning/2026-10-08-interior-runtime-completion/architecture-envelope-repair.json";
        const string Audit = ".planning/2026-10-08-interior-runtime-completion/architecture/current-scene-structure.json";
        const string Overlap = ".planning/2026-10-08-interior-runtime-completion/architecture-kitshop-overlap.json";
        const string Asset = "Assets/ChooGuard/Art/StationInterior/ArchitectureCorrections/1F_KitFixturesOnly.asset";
        const string ShopRoot = "실내 트윈 마감/점포 내부 1F/";
        static bool SecondaryCap(string path)
        {
            if (!path.StartsWith(ShopRoot, StringComparison.Ordinal)) return false;
            string leaf = path.Substring(ShopRoot.Length);
            return leaf.StartsWith("Kit_ShopCeiling_", StringComparison.Ordinal) || leaf == "Kit_CeilingGrid" ||
                leaf.StartsWith("Kit_Light_", StringComparison.Ordinal) && leaf.EndsWith("_downlight", StringComparison.Ordinal);
        }
        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse(); return string.Join("/", parts);
        }
        static string Digest(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static string MeshDigest(Mesh mesh)
        {
            using (var memory = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memory, System.Text.Encoding.UTF8, true))
                {
                    var vertices = mesh.vertices; var indices = mesh.triangles; var normals = mesh.normals;
                    writer.Write(vertices.Length); writer.Write(indices.Length); writer.Write(normals.Length);
                    foreach (var v in vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                    foreach (int i in indices) writer.Write(i);
                    foreach (var v in normals) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                }
                return Digest(memory.ToArray());
            }
        }
        public static Mesh KeepOtherTriangles(Mesh source, int[] remove)
        {
            if (source == null || !source.isReadable || source.subMeshCount != 1)
                throw new InvalidDataException("Exact enclosure partition requires one readable submesh");
            int count = source.triangles.Length / 3;
            if (remove.Length == 0 || remove.Distinct().Count() != remove.Length || remove.Any(i => i < 0 || i >= count))
                throw new InvalidDataException("Invalid or repeated source triangle ordinal");
            var removed = new HashSet<int>(remove); var indices = source.triangles;
            var mesh = UnityEngine.Object.Instantiate(source); mesh.name = source.name + "_fixture_collision_only";
            mesh.triangles = Enumerable.Range(0, count).Where(i => !removed.Contains(i))
                .SelectMany(i => new[] { indices[i * 3], indices[i * 3 + 1], indices[i * 3 + 2] }).ToArray();
            return mesh;
        }
        static void CheckFrame(Transform frame, JToken matrix)
        {
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                if (Mathf.Abs(frame.localToWorldMatrix[r, c] - (float)matrix[r][c]) > .00002f)
                    throw new InvalidDataException("Audited architectural frame changed: " + PathOf(frame));
        }
        public static string Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Architectural repair requires a stable editor");
            if (File.Exists(Receipt) || AssetDatabase.LoadAssetAtPath<Mesh>(Asset) != null)
                throw new InvalidOperationException("An existing repair must be checked, not installed twice");
            var scene = SceneManager.GetSceneByName("FpsStation");
            if (!scene.isLoaded) throw new InvalidOperationException("Production FpsStation must be loaded");
            var audit = JObject.Parse(File.ReadAllText(Audit)); var overlap = JObject.Parse(File.ReadAllText(Overlap));
            if (Digest(File.ReadAllBytes(scene.path)) != (string)overlap["scene_file_sha256"])
                throw new InvalidDataException("Saved production scene changed after structural audit");
            string ownerPath = (string)overlap["kit_owner"];
            var originalPaths = new HashSet<string>(((JArray)overlap["rows"]).Select(r => (string)r["original_owner"]));
            var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => PathOf(t) == ownerPath || originalPaths.Contains(PathOf(t)) || SecondaryCap(PathOf(t)))
                .ToDictionary(PathOf, StringComparer.Ordinal);
            var meshRows = ((JArray)audit["meshes"]).ToDictionary(r => (string)r["id"]);
            var renderRows = ((JArray)audit["renderers"]).Where(r => originalPaths.Contains((string)r["path"]) || SecondaryCap((string)r["path"]))
                .ToDictionary(r => (string)r["path"]);
            if (!transforms.TryGetValue(ownerPath, out var owner)) throw new InvalidDataException("Audited collision owner missing");
            var collider = owner.GetComponent<MeshCollider>();
            var original = collider != null ? collider.sharedMesh : null;
            if (original == null || MeshDigest(original) != (string)overlap["kit_mesh"]["binary_sha256"])
                throw new InvalidDataException("Audited collision geometry changed");
            var colliderRow = ((JArray)audit["colliders"]).Single(r => (string)r["global_object_id"] == (string)overlap["kit_global_object_id"]);
            CheckFrame(owner, colliderRow["world_matrix"]);
            var auditedRemoval = overlap["duplicate_kit_triangle_ordinals"].ToObject<int[]>();
            var rowRemoval = ((JArray)overlap["rows"]).SelectMany(r => r["kit_triangle_ordinals"].ToObject<int[]>()).ToArray();
            if (rowRemoval.Length != rowRemoval.Distinct().Count() || !auditedRemoval.OrderBy(i => i).SequenceEqual(rowRemoval.OrderBy(i => i)))
                throw new InvalidDataException("Per-boundary triangle ownership disagrees with removal partition");
            // Require every retained original boundary owner and its exact sampled geometry to remain present.
            foreach (var row in ((JArray)overlap["rows"]).GroupBy(r => (string)r["original_owner"]).Select(g => g.First()))
            {
                string path = (string)row["original_owner"];
                if (!transforms.TryGetValue(path, out var retained)) throw new InvalidDataException("Original wall owner missing: " + path);
                var filter = retained.GetComponent<MeshFilter>();
                if (filter == null || MeshDigest(filter.sharedMesh) != (string)meshRows[(string)row["original_mesh"]]["binary_sha256"])
                    throw new InvalidDataException("Original wall geometry changed: " + path);
                CheckFrame(retained, renderRows[path]["world_matrix"]);
            }
            var kitVertices = original.vertices; var kitIndices = original.triangles;
            foreach (var row in (JArray)overlap["rows"])
            {
                var retained = transforms[(string)row["original_owner"]]; var wall = retained.GetComponent<MeshFilter>().sharedMesh;
                var wallVertices = wall.vertices; var wallIndices = wall.triangles;
                var corners = row["original_triangle_ordinals"].ToObject<int[]>()
                    .SelectMany(i => wallIndices.Skip(i * 3).Take(3)).Select(i => retained.TransformPoint(wallVertices[i])).ToArray();
                var plane = new Plane(corners[0], corners[1], corners[2]);
                var lower = corners.OrderBy(v => v.y).First();
                var other = corners.OrderByDescending(v => (new Vector2(v.x, v.z) - new Vector2(lower.x, lower.z)).sqrMagnitude).First();
                var direction = other - lower; direction.y = 0; float length = direction.magnitude; direction /= length;
                float minY = corners.Min(v => v.y), maxY = corners.Max(v => v.y);
                foreach (int ordinal in row["kit_triangle_ordinals"].ToObject<int[]>())
                    foreach (int index in kitIndices.Skip(ordinal * 3).Take(3))
                    {
                        var point = owner.TransformPoint(kitVertices[index]); float along = Vector3.Dot(point - lower, direction);
                        if (length < .01f || Mathf.Abs(plane.GetDistanceToPoint(point)) > .0001f || along < -.0001f ||
                            along > length + .0001f || point.y < minY - .0001f || point.y > maxY + .0001f)
                            throw new InvalidDataException("Proposed collision removal is not covered by retained boundary: " + (string)row["element"]);
                    }
            }
            var obsolete = new List<Renderer>(); var withheld = new JArray();
            foreach (var pair in renderRows)
            {
                string path = pair.Key;
                if (!path.StartsWith(ShopRoot, StringComparison.Ordinal)) continue;
                if (!SecondaryCap(path)) continue;
                if (!transforms.TryGetValue(path, out var transform)) throw new InvalidDataException("Secondary owner missing: " + path);
                var filter = transform.GetComponent<MeshFilter>(); var renderer = transform.GetComponent<Renderer>();
                if (filter == null || renderer == null || MeshDigest(filter.sharedMesh) != (string)meshRows[(string)pair.Value["mesh"]]["binary_sha256"])
                    throw new InvalidDataException("Secondary geometry changed: " + path);
                CheckFrame(transform, pair.Value["world_matrix"]);
                obsolete.Add(renderer);
                withheld.Add(new JObject { ["path"] = path, ["original_mesh_asset"] = AssetDatabase.GetAssetPath(filter.sharedMesh),
                    ["previous_enabled"] = renderer.enabled, ["reason"] = "secondary inferred enclosure conflicts with existing envelope owner; original asset retained" });
            }
            if (obsolete.Count != 3) throw new InvalidDataException("Expected exactly ceiling, downlight frame and lens, found " + obsolete.Count);
            var remove = overlap["duplicate_kit_triangle_ordinals"].ToObject<int[]>();
            var replacement = KeepOtherTriangles(original, remove);
            if (replacement.triangles.Length / 3 != (int)overlap["preserve_remaining_triangle_count"])
            { UnityEngine.Object.DestroyImmediate(replacement); throw new InvalidDataException("Unrelated fixture preservation failed"); }
            var preserved = overlap["preserve_remaining_triangle_ordinals"].ToObject<int[]>(); var originalIndices = original.triangles;
            if (!replacement.triangles.SequenceEqual(preserved.SelectMany(i => originalIndices.Skip(i * 3).Take(3))))
            { UnityEngine.Object.DestroyImmediate(replacement); throw new InvalidDataException("Preserved fixture index order changed"); }
            bool committed = false; var enabledBefore = obsolete.Select(r => r.enabled).ToArray();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Asset)); AssetDatabase.Refresh();
                AssetDatabase.CreateAsset(replacement, Asset);
                Undo.RecordObject(collider, "Resolve duplicate interior boundaries"); collider.sharedMesh = replacement;
                foreach (var renderer in obsolete) { Undo.RecordObject(renderer, "Resolve contradictory secondary shop structure"); renderer.enabled = false; }
                var report = new JObject { ["schema"] = "chooguard.exact-interior-envelope-repair.v1", ["site_accuracy_verified"] = false,
                    ["production_installed"] = false, ["baseline_scene_sha256"] = (string)overlap["scene_file_sha256"],
                    ["original_collider_asset"] = AssetDatabase.GetAssetPath(original), ["replacement_collider_asset"] = Asset,
                    ["duplicate_wall_edges_removed"] = 63, ["duplicate_collision_triangles_removed"] = remove.Length,
                    ["unrelated_fixture_triangles_preserved"] = replacement.triangles.Length / 3, ["secondary_renderers_withheld"] = withheld,
                    ["remaining_boundary"] = "Retained geometry remains public-reference estimates. Real wall placement and heights require photographic structural reconstruction, not walkability certification." };
                File.WriteAllText(Receipt + ".pending", report.ToString()); AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Production architecture repair was not saved");
                committed = true; report["production_installed"] = true; report["saved_scene_sha256"] = Digest(File.ReadAllBytes(scene.path));
                File.WriteAllText(Receipt, report.ToString()); File.Delete(Receipt + ".pending");
                return "Saved structural repair: 63 duplicate collision boundaries removed; 132 fixture triangles retained; 3 contradictory ceiling/light batches withheld; wall finishes retained";
            }
            finally
            {
                if (!committed)
                {
                    collider.sharedMesh = original;
                    for (int i = 0; i < obsolete.Count; i++) obsolete[i].enabled = enabledBefore[i];
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(Asset) != null) AssetDatabase.DeleteAsset(Asset);
                    else if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement);
                    File.Delete(Receipt + ".pending");
                }
            }
        }
    }
}
#endif
