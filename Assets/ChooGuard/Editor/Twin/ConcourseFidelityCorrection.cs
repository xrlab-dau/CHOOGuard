#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    // Photographic/source reconstruction. This does not certify as-built dimensions.
    public static class ConcourseFidelityCorrection
    {
        public const string Registry = "content/world/observed-interiors/concourse-fidelity-20261010.json";
        const string Art = "Assets/ChooGuard/Art/StationInterior/ConcourseFidelity20261010";
        const string Work = ".planning/2026-10-10-digital-twin-fidelity-rebuild";
        const string Native5 = "FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_5";
        const string Native6 = "FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_6";
        static void Need(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
        public static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static Transform Find(string path) => Platform6BankB1Replay.Find(path);
        static JObject State(Transform t) => Platform6BanksB24PlacementReplay.CarrierFields(t);
        static JObject AssetPin(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            Need(!string.IsNullOrEmpty(path) && File.Exists(path), "Saved asset required");
            return new JObject { ["path"] = path, ["sha256"] = Hash(path), ["meta_sha256"] = Hash(path + ".meta"), ["guid"] = AssetDatabase.AssetPathToGUID(path) };
        }
        static void CheckPin(JToken pin)
        {
            string path = (string)pin["path"];
            Need(Hash(path) == (string)pin["sha256"] && Hash(path + ".meta") == (string)pin["meta_sha256"] &&
                AssetDatabase.AssetPathToGUID(path) == (string)pin["guid"], "Asset changed: " + path);
        }
        static void SameVertices(Mesh a, Mesh b)
        {
            Need(a.indexFormat == b.indexFormat && a.vertexCount == b.vertexCount && a.bounds == b.bounds &&
                a.vertices.SequenceEqual(b.vertices) && a.normals.SequenceEqual(b.normals) &&
                a.tangents.SequenceEqual(b.tangents) && a.colors.SequenceEqual(b.colors) &&
                a.GetVertexAttributes().SequenceEqual(b.GetVertexAttributes()) &&
                a.boneWeights.SequenceEqual(b.boneWeights) && a.bindposes.SequenceEqual(b.bindposes) &&
                a.blendShapeCount == 0 && b.blendShapeCount == 0, "Original vertex channels/bounds changed");
            for (int u = 0; u < 8; u++)
            {
                var x = new List<Vector4>(); var y = new List<Vector4>(); a.GetUVs(u, x); b.GetUVs(u, y);
                Need(x.SequenceEqual(y), "UV channel changed: " + u);
            }
        }
        static Mesh RoofCopy(Mesh source, Transform owner, JToken ordinals)
        {
            var remove = new HashSet<int>(ordinals.Select(x => (int)x));
            Need(remove.Count == 8, "Exactly eight source-attributed flat cap triangles required");
            var mesh = UnityEngine.Object.Instantiate(source); int ordinal = 0, removed = 0;
            var vertices = source.vertices;
            for (int sm = 0; sm < source.subMeshCount; sm++)
            {
                var indices = source.GetTriangles(sm); var keep = new List<int>();
                for (int i = 0; i < indices.Length; i += 3, ordinal++)
                {
                    if (!remove.Contains(ordinal)) { keep.Add(indices[i]); keep.Add(indices[i + 1]); keep.Add(indices[i + 2]); continue; }
                    var points = new[] { indices[i], indices[i + 1], indices[i + 2] }.Select(n => owner.TransformPoint(vertices[n])).ToArray();
                    Need(points.All(p => Mathf.Abs(p.y - 11) < .0001f) || points.All(p => Mathf.Abs(p.y - 12.3f) < .0001f), "Selected cap is not at reviewed roof elevation");
                    removed++;
                }
                mesh.SetTriangles(keep, sm, false);
            }
            Need(removed == 8, "Missing reviewed cap triangle"); SameVertices(source, mesh); return mesh;
        }
        static Mesh FloorCopy(Mesh source, JToken recipe, out int materialSlot)
        {
            int sm = (int)recipe["source_submesh"];
            int priorFaces = 0; for (int s = 0; s < sm; s++) priorFaces += (int)source.GetIndexCount(s) / 3;
            var selected = recipe["triangle_ordinals"].Select(x => (int)x).ToArray();
            Need(selected.Length == 18 && selected.SequenceEqual(Enumerable.Range(priorFaces, 18)), "Floor range must be exactly the first 18 reviewed SM37 faces");
            var descriptor = source.GetSubMesh(sm); Need(descriptor.topology == MeshTopology.Triangles && descriptor.indexCount == 180, "Unexpected floor/stair source range");
            var mesh = UnityEngine.Object.Instantiate(source); materialSlot = source.subMeshCount; mesh.subMeshCount = materialSlot + 1;
            var floor = descriptor; floor.indexCount = 54;
            descriptor.indexStart += 54; descriptor.indexCount -= 54;
            mesh.SetSubMesh(sm, descriptor, MeshUpdateFlags.DontRecalculateBounds);
            mesh.SetSubMesh(materialSlot, floor, MeshUpdateFlags.DontRecalculateBounds);
            SameVertices(source, mesh);
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var expected = source.GetIndices(s);
                Need((s == sm ? expected.Skip(54) : expected).SequenceEqual(mesh.GetIndices(s)), "Unselected floor/stair indices changed");
            }
            Need(source.GetIndices(sm).Take(54).SequenceEqual(mesh.GetIndices(materialSlot)), "Reviewed floor indices changed");
            return mesh;
        }
        static Material Stone(Material original)
        {
            var shader = Shader.Find("ChooGuard/StationStoneTileWorld");
            Need(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Stone shader must compile before installation");
            var material = new Material(shader) { name = "Concourse_StoneTile_ReferenceAppearance", enableInstancing = true };
            material.SetColor("_StoneTint", new Color(.46f, .47f, .45f)); material.SetColor("_GroutTint", new Color(.24f, .25f, .23f));
            material.SetFloat("_TileSize", .6f); material.SetFloat("_GroutWidth", .003f);
            material.SetFloat("_TileVariation", .045f); material.SetFloat("_GrainContrast", .12f); material.SetFloat("_GrainScale", 700);
            material.SetFloat("_Smoothness", .69f); material.SetFloat("_SmoothnessVariation", .055f); material.SetFloat("_GroutSmoothness", .18f);
            // Shared square-tile axes follow the long source-plan hall direction, not mesh UVs.
            var v = new Vector3(-.3079480997f, 0, .1110653530f).normalized;
            var u = Vector3.Cross(Vector3.up, v).normalized;
            material.SetVector("_AxisU", new Vector4(u.x, u.y, u.z, 0)); material.SetVector("_AxisV", new Vector4(v.x, v.y, v.z, 0));
            material.SetVector("_OriginWS", new Vector4(2.0707812f, 7, -40.0877891f, 0));
            float cull = original.HasProperty("_Cull") ? original.GetFloat("_Cull") : 2;
            material.SetFloat("_Cull", cull); material.doubleSidedGI = cull == 0;
            return material;
        }
        static Vector3 Plan(float u, float v, float y) => new Vector3(
            (float)(u * -.10967428542396562 + v * -.30794809973764947 + 135.45619186760297), y,
            (float)(u * -.2879851030285814 + v * .11106535302671859 + 52.647506373321896));
        static GameObject AddPlanFloor(JToken row, Transform parent, Material material, List<string> created)
        {
            var go = new GameObject((string)row["id"]); go.transform.SetParent(parent, false);
            try
            {
                var world = row["vertices"].Select(v => new Vector3((float)v[0], (float)v[1], (float)v[2])).ToArray();
                var indices = row["triangles"].Select(v => (int)v).ToArray(); double topArea = 0;
                Need(world.Length > 0 && indices.Length % 3 == 0 && world.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), "Invalid source-plan slab");
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var a = world[indices[i]]; var b = world[indices[i + 1]]; var c = world[indices[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a);
                    Need(normal.sqrMagnitude > .000001f, "Degenerate source-plan slab triangle");
                    if (a.y == 7 && b.y == 7 && c.y == 7) { Need(normal.y > 0, "Top slab winding reversed"); topArea += normal.y * .5; }
                }
                Need(Math.Abs(topArea - (double)row["world_area_m2_in_model_frame"]) < .005, "Source-plan floor area differs");
                var mesh = new Mesh { name = (string)row["id"] };
                mesh.vertices = world.Select(go.transform.InverseTransformPoint).ToArray(); mesh.triangles = indices;
                mesh.uv = row["uv"].Select(v => new Vector2((float)v[0], (float)v[1])).ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string path = Art + "/" + (string)row["id"] + ".asset"; Need(!File.Exists(path), "Existing plan slab asset");
                AssetDatabase.CreateAsset(mesh, path); created.Add(path);
                go.layer = Find(Native6).gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                var collider = go.AddComponent<MeshCollider>(); collider.cookingOptions = (MeshColliderCookingOptions)30; collider.sharedMesh = mesh;
                return go;
            }
            catch { UnityEngine.Object.DestroyImmediate(go); throw; }
        }
        static JArray AddedFloorProbes()
        {
            Physics.SyncTransforms(); var result = new JArray();
            foreach (var p in new[] { new Vector2(460, 158), new Vector2(467, 158), new Vector2(460, 240), new Vector2(467, 240), new Vector2(460, 270), new Vector2(467, 270) })
            {
                Need(Physics.Raycast(Plan(p.x, p.y, 8.7f), Vector3.down, out var hit, 2) && Mathf.Abs(hit.point.y - 7) < .0001f, "Source-plan east floor still missing");
                result.Add(new JObject { ["source_px"] = new JArray(p.x, p.y), ["floorY"] = hit.point.y, ["owner"] = Platform6BankB1Replay.PathOf(hit.transform) });
            }
            return result;
        }
        static JArray FloorProbes()
        {
            Physics.SyncTransforms(); var rows = new JArray();
            foreach (var p in new[] { new Vector2(430, 280), new Vector2(430, 158), new Vector2(421, 110), new Vector2(430, 295.5f) })
            {
                Need(Physics.Raycast(Plan(p.x, p.y, 8.7f), Vector3.down, out var hit, 2), "Existing boarding floor lost");
                Need(Mathf.Abs(hit.point.y - 7) < .0001f && Platform6BankB1Replay.PathOf(hit.transform) == Native6, "Existing boarding floor/owner changed");
                rows.Add(new JObject { ["source_px"] = new JArray(p.x, p.y), ["floorY"] = hit.point.y, ["owner"] = Native6 });
            }
            return rows;
        }
        static void ScopedState(JObject before, JObject after, params string[] changes)
        {
            var normalized = (JObject)after.DeepClone();
            foreach (string path in changes) normalized.SelectToken(path).Replace(before.SelectToken(path).DeepClone());
            Need(JToken.DeepEquals(before, normalized), "Unrelated owner fields changed");
        }
        public static string Install(string[] args)
        {
            Need(args.Length == 1 && !EditorApplication.isPlaying, "Provide sealed request in edit mode");
            var request = JObject.Parse(File.ReadAllText(args[0])); var scene = SceneManager.GetActiveScene();
            Need(!scene.isDirty && scene.path == "Assets/ChooGuard/Scenes/FpsStation.unity" && Hash(scene.path) == (string)request["scene_sha256"], "Clean reviewed scene epoch required");
            Need(!File.Exists(Registry), "Fidelity correction already installed; verify instead");
            foreach (var pin in request["files"]) Need(Hash((string)pin["path"]) == (string)pin["sha256"], "Request evidence/source changed");
            var roof = JObject.Parse(File.ReadAllText((string)request["roof_recipe"]));
            var floor = JObject.Parse(File.ReadAllText((string)request["floor_recipe"]));
            var n5 = Find(Native5); var n6 = Find(Native6); var f5 = n5.GetComponent<MeshFilter>(); var c5 = n5.GetComponent<MeshCollider>(); var f6 = n6.GetComponent<MeshFilter>(); var r6 = n6.GetComponent<MeshRenderer>();
            var sources = new[] { f5.sharedMesh, c5.sharedMesh, f6.sharedMesh }; var materials = r6.sharedMaterials;
            var before5 = State(n5); var before6 = State(n6); var sourcePins = new JArray(sources.Select(AssetPin));
            var created = new List<string>(); Directory.CreateDirectory(Art); Directory.CreateDirectory(Work + "/installation");
            var addedFloors = new List<GameObject>();
            string backup = Work + "/installation/FpsStation-before.unity";
            if (File.Exists(backup)) Need(Hash(backup) == (string)request["scene_sha256"], "Predecessor backup differs"); else File.Copy(scene.path, backup, false);
            Mesh roofRender = null, roofCollider = null, floorRender = null; Material stone = null;
            try
            {
                foreach (var row in roof["rows"])
                {
                    Mesh source = (string)row["role"] == "native5-render" ? sources[0] : sources[1];
                    Need(AssetDatabase.GetAssetPath(source) == (string)row["current_asset"] && Hash((string)row["current_asset"]) == (string)row["current_asset_sha256"] &&
                        ArchitecturalCorrectionRegistry.CompleteChannelDigest(source) == (string)row["current_unity_digests"]["complete_channels_sha256"], "Roof predecessor differs from reviewed source");
                    var copy = RoofCopy(source, n5, row["remove_triangle_ordinals"]);
                    Need(ArchitecturalCorrectionRegistry.CompleteChannelDigest(copy) == (string)row["candidate_unity_digests"]["complete_channels_sha256"], "Root cap reconstruction differs from independent candidate");
                    if ((string)row["role"] == "native5-render") roofRender = copy; else roofCollider = copy;
                }
                Need(AssetDatabase.GetAssetPath(sources[2]) == (string)floor["asset"] && Hash((string)floor["asset"]) == (string)floor["asset_sha256"], "Floor predecessor changed");
                floorRender = FloorCopy(sources[2], floor, out int slot); stone = Stone(materials[(int)floor["source_submesh"]]);
                foreach (var item in new[] { (asset: (UnityEngine.Object)roofRender, name: "Native5_ExposedRoof_Render.asset"), (asset: (UnityEngine.Object)roofCollider, name: "Native5_ExposedRoof_Collision.asset"), (asset: (UnityEngine.Object)floorRender, name: "Native6_StoneFloor_Render.asset"), (asset: (UnityEngine.Object)stone, name: "Concourse_StoneTile.mat") })
                {
                    string path = Art + "/" + item.name; Need(!File.Exists(path), "Existing output asset"); item.asset.name = Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(item.asset, path); created.Add(path);
                }
                f5.sharedMesh = roofRender; c5.sharedMesh = roofCollider; f6.sharedMesh = floorRender;
                r6.sharedMaterials = materials.Concat(new[] { stone }).ToArray(); Need(slot == materials.Length, "Unexpected floor material slot count");
                foreach (var file in request["floor_addition_recipes"] ?? new JArray())
                {
                    var addition = JObject.Parse(File.ReadAllText((string)file));
                    Need((string)addition["scene_epoch"] == (string)request["scene_sha256"] && (double)addition["candidate_overlap_render_floor_area_px2"] == 0 &&
                        (double)addition["candidate_overlap_collider_floor_area_px2"] == 0 && (double)addition["candidate_overlap_reviewed_walls_area_px2"] == 0 &&
                        (double)addition["candidate_overlap_source_void_area_px2"] == 0 && (double)addition["candidate_overlap_source_bank_envelopes_area_px2"] == 0, "Plan-floor subtraction/authority changed");
                    foreach (var row in addition["meshes"]) addedFloors.Add(AddPlanFloor(row, n6.parent, stone, created));
                }
                var after5 = State(n5); var after6 = State(n6);
                ScopedState(before5, after5, "filter.MeshFilter.m_Mesh", "collider.MeshCollider.m_Mesh");
                ScopedState(before6, after6, "filter.MeshFilter.m_Mesh", "renderer.MeshRenderer.m_Materials");
                var physics = FloorProbes(); var addedPhysics = addedFloors.Count == 0 ? new JArray() : AddedFloorProbes();
                AssetDatabase.SaveAssets(); foreach (var pin in sourcePins) CheckPin(pin);
                var registry = new JObject { ["schema"] = "chooguard.concourse-fidelity-correction.v1", ["status"] = "REFERENCE_RECONSTRUCTION_INCOMPLETE", ["site_accuracy_verified"] = false,
                    ["whole_building_complete"] = false, ["request"] = args[0], ["request_sha256"] = Hash(args[0]), ["before_scene_sha256"] = request["scene_sha256"],
                    ["source_assets"] = sourcePins, ["outputs"] = new JArray(new UnityEngine.Object[] { roofRender, roofCollider, floorRender, stone }.Select(AssetPin)),
                    ["native5_before"] = before5, ["native5_after"] = after5, ["native6_before"] = before6, ["native6_after"] = after6,
                    ["retired_flat_cap_triangles_per_role"] = 8, ["floor_appearance_triangles"] = 18, ["floor_collider_unchanged"] = true, ["original_vertex_channels_unchanged"] = true,
                    ["tile_pitch_metres_provisional_appearance"] = .6, ["grout_width_metres_provisional_appearance"] = .003, ["actual_PhysX_existing_floor_checks"] = physics,
                    ["added_plan_floor_owners"] = new JArray(addedFloors.Select(go => new JObject { ["owner"] = Platform6BankB1Replay.PathOf(go.transform), ["state"] = State(go.transform), ["asset"] = AssetPin(go.GetComponent<MeshFilter>().sharedMesh) })),
                    ["actual_PhysX_added_east_floor_checks"] = addedPhysics };
                EditorSceneManager.MarkSceneDirty(scene); Need(EditorSceneManager.SaveScene(scene), "Scene save failed");
                registry["scene_sha256"] = Hash(scene.path); File.WriteAllText(Registry, registry.ToString()); File.WriteAllText(Work + "/installation/receipt.json", registry.ToString());
                return "SAVED_REFERENCE_CONCOURSE_ROOF_AND_STONE_FLOOR " + (string)registry["scene_sha256"];
            }
            catch
            {
                f5.sharedMesh = sources[0]; c5.sharedMesh = sources[1]; f6.sharedMesh = sources[2]; r6.sharedMaterials = materials;
                foreach (var go in addedFloors) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); File.Copy(backup, scene.path, true);
                foreach (string path in created) AssetDatabase.DeleteAsset(path);
                if (File.Exists(Registry)) File.Delete(Registry);
                foreach (var asset in new UnityEngine.Object[] { roofRender, roofCollider, floorRender, stone }) if (asset != null && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
        }
        public static string Verify(string[] args)
        {
            var registry = JObject.Parse(File.ReadAllText(Registry)); var scene = SceneManager.GetActiveScene();
            Need(!scene.isDirty && Hash(scene.path) == (string)registry["scene_sha256"], "Saved fidelity scene differs");
            Need(Hash((string)registry["request"]) == (string)registry["request_sha256"], "Request changed");
            foreach (var pin in registry["source_assets"].Concat(registry["outputs"])) CheckPin(pin);
            foreach (var row in registry["added_plan_floor_owners"])
            {
                CheckPin(row["asset"]); Need(JToken.DeepEquals(State(Find((string)row["owner"])), row["state"]), "Plan floor owner/geometry changed");
            }
            Need(JToken.DeepEquals(State(Find(Native5)), registry["native5_after"]) && JToken.DeepEquals(State(Find(Native6)), registry["native6_after"]), "Installed source-scoped owners changed");
            FloorProbes(); Need(!ShaderUtil.ShaderHasError(Shader.Find("ChooGuard/StationStoneTileWorld")), "Stone shader errors");
            if (registry["added_plan_floor_owners"].Any()) AddedFloorProbes();
            return "SAVED_CONCOURSE_REFERENCE_CORRECTION_VERIFIED_NOT_SITE99";
        }
    }
}
#endif
