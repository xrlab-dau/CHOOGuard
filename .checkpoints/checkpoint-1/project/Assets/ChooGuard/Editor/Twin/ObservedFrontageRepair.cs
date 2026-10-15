#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Clip only the photographed pier visibility conflict; keep the original assets and all other storefront faces.</summary>
    public static class ObservedFrontageRepair
    {
        public const string Evidence = "workers/twin/evidence/registered-structure-20261008/artbox-frontage-conflict-clips.json";
        public const string Receipt = ".planning/2026-10-08-interior-runtime-completion/architecture-artbox-repair.json";
        const string Folder = "Assets/ChooGuard/Art/StationInterior/ArchitectureCorrections";
        const string Audit = ".planning/2026-10-08-interior-runtime-completion/architecture/current-scene-structure.json";
        const string PierOwner = "1층 · 원본 정합/1층 실내 · 근거 표기/Ground_Unit_4916_Walls";
        public sealed class FaceRemainder
        {
            public int Ordinal;
            public Vector3[][] Triangles;
            public float RemainingAreaFraction;
            public Vector2[] CutPolygon;
        }
        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string PathOf(Transform transform)
        {
            var names = new List<string>();
            while (transform != null) { names.Add(transform.name); transform = transform.parent; }
            names.Reverse(); return string.Join("/", names);
        }
        static Vector3 V(JToken t) => new Vector3((float)t[0], (float)t[1], (float)t[2]);
        public static FaceRemainder[] ReadRemainders(string owner)
        {
            var evidence = JObject.Parse(File.ReadAllText(Evidence));
            return ((JArray)evidence["faces"]).Where(f => (string)f["owner"] == owner).Select(ReadRemainder).ToArray();
        }
        static FaceRemainder ReadRemainder(JToken face)
        {
            return new FaceRemainder { Ordinal = (int)face["triangle_ordinal"],
                RemainingAreaFraction = (float)face["strict_preserve_original_triangle_area_fraction"],
                CutPolygon = ((JArray)face["strict_all_four_fixed_pier_frustum_guard_geometry"]["coordinates"][0])
                    .Select(p => new Vector2((float)p[0], (float)p[1])).ToArray(),
                Triangles = ((JArray)face["strict_remaining_barycentric_triangles"]).Select(t =>
                    ((JArray)t).Select(p => new Vector3(1 - (float)p[0] - (float)p[1], (float)p[0], (float)p[1])).ToArray()).ToArray() };
        }
        readonly struct Point
        {
            public readonly double X, Y;
            public Point(double x, double y) { X = x; Y = y; }
        }
        static Point[] Points(Vector3[] triangle) => triangle.Select(v => new Point(v.y, v.z)).ToArray();
        static double Cross(Point a, Point b, Point p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        static double Area(IReadOnlyList<Point> polygon)
        {
            double twice = 0;
            for (int i = 0; i < polygon.Count; i++)
            { var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count]; twice += a.X * b.Y - a.Y * b.X; }
            return Math.Abs(twice) * .5;
        }
        // The clip is always a convex, counterclockwise remainder triangle. The
        // subject may be the concave observed cut; boundary bridges have zero area.
        static double IntersectionArea(Point[] subject, Point[] clip)
        {
            var polygon = subject.ToList();
            for (int edge = 0; edge < clip.Length && polygon.Count != 0; edge++)
            {
                var a = clip[edge]; var b = clip[(edge + 1) % clip.Length]; var output = new List<Point>();
                var previous = polygon[polygon.Count - 1]; double previousDistance = Cross(a, b, previous);
                foreach (var point in polygon)
                {
                    double distance = Cross(a, b, point);
                    if ((previousDistance >= 0) != (distance >= 0))
                    {
                        double t = previousDistance / (previousDistance - distance);
                        output.Add(new Point(previous.X + (point.X - previous.X) * t, previous.Y + (point.Y - previous.Y) * t));
                    }
                    if (distance >= 0) output.Add(point);
                    previous = point; previousDistance = distance;
                }
                polygon = output;
            }
            return Area(polygon);
        }
        public static Mesh KeepObservedRemainder(Mesh source, FaceRemainder[] remainders)
        {
            if (source == null || !source.isReadable || source.subMeshCount != 1 || source.blendShapeCount != 0 ||
                source.boneWeights.Length != 0 || source.colors.Length != 0)
                throw new InvalidDataException("Observed cut requires a readable static single-material structural mesh");
            for (int channel = 1; channel < 8; channel++)
            {
                var extra = new List<Vector4>(); source.GetUVs(channel, extra);
                if (extra.Count != 0) throw new InvalidDataException("Unaccounted source UV channel");
            }
            var originalVertices = source.vertices; var originalIndices = source.triangles;
            var originalNormals = source.normals; var originalUV = source.uv; var originalTangents = source.tangents;
            if (originalNormals.Length != source.vertexCount || originalUV.Length != source.vertexCount || originalTangents.Length != source.vertexCount)
                throw new InvalidDataException("Expected complete source normal, tangent and UV channels");
            int count = originalIndices.Length / 3;
            if (remainders.Length == 0 || remainders.Select(f => f.Ordinal).Distinct().Count() != remainders.Length)
                throw new InvalidDataException("Missing or repeated source face");
            foreach (var face in remainders)
            {
                if (face.Ordinal < 0 || face.Ordinal >= count || !float.IsFinite(face.RemainingAreaFraction) ||
                    face.RemainingAreaFraction <= 0 || face.RemainingAreaFraction >= 1 ||
                    face.Triangles == null || face.Triangles.Length == 0)
                    throw new InvalidDataException("Whole-face removal and invalid ordinals are prohibited");
                double areaFraction = 0;
                foreach (var triangle in face.Triangles)
                {
                    if (triangle == null || triangle.Length != 3) throw new InvalidDataException("Remainder must be triangulated");
                    foreach (var b in triangle)
                        if (!float.IsFinite(b.x) || !float.IsFinite(b.y) || !float.IsFinite(b.z) ||
                            b.x < -.00001f || b.y < -.00001f || b.z < -.00001f || Mathf.Abs(b.x + b.y + b.z - 1) > .00001f)
                            throw new InvalidDataException("Remainder vertex leaves its original source triangle");
                    double signed = (double)(triangle[1].y - triangle[0].y) * (triangle[2].z - triangle[0].z) -
                        (double)(triangle[1].z - triangle[0].z) * (triangle[2].y - triangle[0].y);
                    if (signed <= 1e-12) throw new InvalidDataException("Degenerate or reversed remainder triangle");
                    areaFraction += signed;
                }
                if (Math.Abs(areaFraction - face.RemainingAreaFraction) > .00001)
                    throw new InvalidDataException("Remainder area disagrees with four-view clip receipt");
                if (face.CutPolygon == null || face.CutPolygon.Length < 4)
                    throw new InvalidDataException("Strict observed cut polygon missing");
                foreach (var p in face.CutPolygon)
                    if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || p.x < -.00001f || p.y < -.00001f || p.x + p.y > 1.00001f)
                        throw new InvalidDataException("Strict cut polygon leaves the original source face");
                var cut = face.CutPolygon.Select(p => new Point(p.x, p.y)).ToArray();
                if (Math.Abs(1 - 2 * Area(cut) - areaFraction) > .00001)
                    throw new InvalidDataException("Remainder area disagrees with independent cut polygon");
                for (int i = 0; i < face.Triangles.Length; i++)
                {
                    var triangle = Points(face.Triangles[i]);
                    if (IntersectionArea(cut, triangle) > .0000005)
                        throw new InvalidDataException("Remainder enters the observed cut region");
                    for (int j = 0; j < i; j++)
                        if (IntersectionArea(Points(face.Triangles[j]), triangle) > .0000005)
                            throw new InvalidDataException("Remainder triangles overlap instead of conserving the original face");
                }
            }
            var cuts = remainders.ToDictionary(f => f.Ordinal);
            var vertices = originalVertices.ToList(); var normals = originalNormals.ToList();
            var uv = originalUV.ToList(); var tangents = originalTangents.ToList(); var indices = new List<int>();
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int a = originalIndices[ordinal * 3], b = originalIndices[ordinal * 3 + 1], c = originalIndices[ordinal * 3 + 2];
                if (!cuts.TryGetValue(ordinal, out var face)) { indices.Add(a); indices.Add(b); indices.Add(c); continue; }
                foreach (var triangle in face.Triangles) foreach (var weight in triangle)
                {
                    indices.Add(vertices.Count);
                    vertices.Add(originalVertices[a] * weight.x + originalVertices[b] * weight.y + originalVertices[c] * weight.z);
                    normals.Add((originalNormals[a] * weight.x + originalNormals[b] * weight.y + originalNormals[c] * weight.z).normalized);
                    uv.Add(originalUV[a] * weight.x + originalUV[b] * weight.y + originalUV[c] * weight.z);
                    if (originalTangents[a].w != originalTangents[b].w || originalTangents[a].w != originalTangents[c].w)
                        throw new InvalidDataException("Source face has ambiguous tangent handedness");
                    var tangent = originalTangents[a] * weight.x + originalTangents[b] * weight.y + originalTangents[c] * weight.z;
                    var direction = new Vector3(tangent.x, tangent.y, tangent.z).normalized;
                    tangents.Add(new Vector4(direction.x, direction.y, direction.z, originalTangents[a].w));
                }
            }
            var result = UnityEngine.Object.Instantiate(source); result.name = source.name + "_observed_pier_clearance";
            result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0, uv); result.SetTangents(tangents);
            result.SetTriangles(indices, 0); result.RecalculateBounds(); return result;
        }
        public static string Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || File.Exists(Receipt) || File.Exists(Receipt + ".pending"))
                throw new InvalidOperationException("Repair requires stable edit mode and must not be applied twice");
            var scene = SceneManager.GetSceneByName("FpsStation");
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Save the current production scene before the bounded repair");
            var evidence = JObject.Parse(File.ReadAllText(Evidence));
            if ((string)evidence["preferred_cut_geometry_field"] != "strict_all_four_fixed_pier_frustum_guard_geometry" ||
                !(bool)evidence["verification"]["strict_vertices_and_edge_midpoints_inside_original_feature_in_all_four_source_views"])
                throw new InvalidDataException("Four-view conservative clip contract missing");
            var allFilters = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).ToArray();
            // The observed pier is a fixed reference for these cuts. Changing it
            // invalidates the repair even when all clipped storefront assets match.
            var audit = JObject.Parse(File.ReadAllText(Audit));
            if (Hash(Audit) != (string)evidence["scene_export_sha256"])
                throw new InvalidDataException("Original structural reference audit changed");
            var pierRow = ((JArray)audit["renderers"]).Single(r => (string)r["path"] == PierOwner);
            var pierMesh = ((JArray)audit["meshes"]).Single(m => (string)m["id"] == (string)pierRow["mesh"]);
            var pier = allFilters.Single(f => PathOf(f.transform) == PierOwner);
            if (GlobalObjectId.GetGlobalObjectIdSlow(pier).ToString() != (string)pierRow["global_object_id"] ||
                InteriorEnvelopeRepair.MeshDigest(pier.sharedMesh) != (string)pierMesh["binary_sha256"] ||
                AssetDatabase.GetAssetPath(pier.sharedMesh) != (string)pierMesh["asset"] ||
                !pier.gameObject.activeInHierarchy || pier.GetComponent<Renderer>() == null || !pier.GetComponent<Renderer>().enabled)
                throw new InvalidDataException("Retained fixed pier geometry or visibility changed");
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                if (Mathf.Abs(pier.transform.localToWorldMatrix[r, c] - (float)pierRow["world_matrix"][r][c]) > .00002f)
                    throw new InvalidDataException("Retained fixed pier frame changed");
            var operations = new List<(MeshFilter filter, Mesh original, Mesh replacement, string asset)>();
            bool committed = false; string before = Hash(scene.path);
            try
            {
                foreach (var group in ((JArray)evidence["faces"]).GroupBy(f => (string)f["owner"]))
                {
                    var row = group.First(); var matches = allFilters.Where(f => PathOf(f.transform) == group.Key).ToArray();
                    if (matches.Length != 1) throw new InvalidDataException("Ambiguous or missing structural owner: " + group.Key);
                    var filter = matches[0]; var original = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
                    if (GlobalObjectId.GetGlobalObjectIdSlow(filter).ToString() != (string)row["global_object_id"] ||
                        AssetDatabase.GetAssetPath(original) != (string)row["mesh_asset"] ||
                        AssetDatabase.AssetPathToGUID((string)row["mesh_asset"]) != (string)row["mesh_asset_guid"] ||
                        InteriorEnvelopeRepair.MeshDigest(original) != (string)row["binary_sha256"] || filter.GetComponent<Collider>() != null)
                        throw new InvalidDataException("Audited source identity or geometry changed: " + group.Key);
                    for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++)
                        if (Mathf.Abs(filter.transform.localToWorldMatrix[r, c] - (float)row["world_matrix"][r][c]) > .00002f)
                            throw new InvalidDataException("Source frame changed");
                    if (renderer == null || !renderer.enabled || renderer.sharedMaterials.Length != 1 ||
                        AssetDatabase.GetAssetPath(renderer.sharedMaterial) != (string)row["materials"][0])
                        throw new InvalidDataException("Opaque material owner changed");
                    var material = row["material_contracts"][0];
                    if (Hash((string)material["path"]) != (string)material["sha256"] ||
                        renderer.sharedMaterial.GetFloat("_Surface") != 0 || renderer.sharedMaterial.GetFloat("_Cull") != 2)
                        throw new InvalidDataException("Original opaque frontface material contract changed");
                    foreach (var face in group)
                    {
                        int ordinal = (int)face["triangle_ordinal"]; var indices = original.triangles; var vertices = original.vertices;
                        for (int corner = 0; corner < 3; corner++)
                            if (indices[ordinal * 3 + corner] != (int)face["vertex_indices"][corner] ||
                                (vertices[indices[ordinal * 3 + corner]] - V(face["original_triangle_vertices_local"][corner])).sqrMagnitude > 1e-10f)
                                throw new InvalidDataException("Original face no longer matches evidence");
                    }
                    string asset = Folder + "/ARTBOX_" + (string)row["mesh"] + ".asset";
                    if (File.Exists(asset)) throw new InvalidDataException("Replacement already exists");
                    var replacement = KeepObservedRemainder(original, group.Select(ReadRemainder).ToArray());
                    operations.Add((filter, original, replacement, asset));
                }
                if (operations.Count != 4 || ((JArray)evidence["faces"]).Count != 11)
                    throw new InvalidDataException("Unexpected repair scope");
                var reportRows = new JArray();
                foreach (var operation in operations)
                {
                    AssetDatabase.CreateAsset(operation.replacement, operation.asset);
                    Undo.RecordObject(operation.filter, "Clear observed ARTBOX pier visibility"); operation.filter.sharedMesh = operation.replacement;
                    reportRows.Add(new JObject { ["owner"] = PathOf(operation.filter.transform),
                        ["original_asset"] = AssetDatabase.GetAssetPath(operation.original), ["replacement_asset"] = operation.asset,
                        ["source_mesh_sha256"] = InteriorEnvelopeRepair.MeshDigest(operation.original),
                        ["replacement_mesh_sha256"] = InteriorEnvelopeRepair.MeshDigest(operation.replacement) });
                }
                var report = new JObject { ["schema"] = "chooguard.production-observed-frontage-repair.v1", ["production_installed"] = false,
                    ["site_accuracy_verified"] = false, ["evidence"] = Evidence, ["evidence_sha256"] = Hash(Evidence),
                    ["baseline_scene_sha256"] = before, ["source_face_count_partially_clipped"] = 11, ["owners"] = reportRows,
                    ["fixed_reference_owner"] = PierOwner, ["fixed_reference_mesh_sha256"] = InteriorEnvelopeRepair.MeshDigest(pier.sharedMesh),
                    ["preserved"] = "all unrelated source indices and UVs; fixed original pier, actual glass, fascia outside four-view conflict, all original assets and colliders",
                    ["limitation"] = "Conservative photographic visibility correction in the existing estimated map frame; not field-survey certification or full interior completion" };
                File.WriteAllText(Receipt + ".pending", report.ToString()); AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Production repair was not saved");
                committed = true; report["production_installed"] = true; report["saved_scene_sha256"] = Hash(scene.path);
                // Saved scene/assets are already committed. Retain the pending
                // record if publication fails; the entry guard prevents reapplication.
                File.WriteAllText(Receipt + ".pending", report.ToString()); File.Move(Receipt + ".pending", Receipt);
                return "Saved four-view architectural correction: 11 source faces partially clipped across 4 batches; real pier/glass and surrounding frontage retained";
            }
            finally
            {
                if (!committed)
                {
                    foreach (var operation in operations)
                    {
                        operation.filter.sharedMesh = operation.original;
                        if (AssetDatabase.LoadAssetAtPath<Mesh>(operation.asset) != null) AssetDatabase.DeleteAsset(operation.asset);
                        else UnityEngine.Object.DestroyImmediate(operation.replacement);
                    }
                    File.Delete(Receipt + ".pending");
                }
            }
        }
    }
}
#endif
