#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor.Twin
{
    // Adds only independently reviewed positive floor that was absent from both render and collision.
    public static class ConcourseFloorExtension
    {
        const string Work = ".planning/2026-10-10-digital-twin-fidelity-rebuild/floor-extension";
        const string Art = "Assets/ChooGuard/Art/StationInterior/ConcourseFidelity20261010";
        public const string Receipt = "content/world/observed-interiors/concourse-floor-extension-20261010.json";
        const string Native6 = "FPSWorld/공식 자료 부산역 역사/MainShell/OfficialStation_-부산역_6";
        static void Need(bool v, string why) { if (!v) throw new InvalidDataException(why); }
        static string Hash(string p) => ConcourseFidelityCorrection.Hash(p);
        static JObject State(Transform t) => Platform6BanksB24PlacementReplay.CarrierFields(t);
        static JObject Pin(UnityEngine.Object o)
        {
            string p = AssetDatabase.GetAssetPath(o);
            return new JObject { ["path"] = p, ["sha256"] = Hash(p), ["meta_sha256"] = Hash(p + ".meta"), ["guid"] = AssetDatabase.AssetPathToGUID(p) };
        }
        static GameObject Add(JToken row, Transform parent, Material material, List<string> assets)
        {
            var go = new GameObject((string)row["id"]); go.transform.SetParent(parent, false);
            try
            {
                go.layer = Platform6BankB1Replay.Find(Native6).gameObject.layer;
                var world = row["vertices"].Select(v => new Vector3((float)v[0], (float)v[1], (float)v[2])).ToArray();
                var indices = row["triangles"].Select(v => (int)v).ToArray();
                Need(world.Length > 0 && indices.Length % 3 == 0 && world.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), "Invalid reviewed slab");
                var mesh = new Mesh { name = go.name };
                mesh.vertices = world.Select(go.transform.InverseTransformPoint).ToArray(); mesh.triangles = indices;
                mesh.uv = row["uv"].Select(v => new Vector2((float)v[0], (float)v[1])).ToArray();
                var actual = mesh.vertices.Select(go.transform.TransformPoint).ToArray(); double area = 0;
                var edges = new Dictionary<(Vector3, Vector3), int>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var a = actual[indices[i]]; var b = actual[indices[i + 1]]; var c = actual[indices[i + 2]];
                    var cross = Vector3.Cross(b - a, c - a);
                    Need(cross.sqrMagnitude > 0 && float.IsFinite(cross.sqrMagnitude), "Float32 slab degenerate");
                    if (Mathf.Abs(a.y - 7) < .00001f && Mathf.Abs(b.y - 7) < .00001f && Mathf.Abs(c.y - 7) < .00001f)
                    { Need(cross.y > 0, "Actual top winding reversed"); area += cross.y * .5; }
                    foreach (var edge in new[] { (a,b), (b,c), (c,a) }) edges[edge] = edges.TryGetValue(edge, out int n) ? n + 1 : 1;
                }
                Need(edges.All(e => e.Value == 1 && edges.TryGetValue((e.Key.Item2, e.Key.Item1), out int n) && n == 1), "Actual slab is not a closed oriented manifold");
                Need(Math.Abs(area - (double)row["world_area_m2_in_model_frame"]) < .005, "Actual top area differs");
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string p = Art + "/" + go.name + ".asset"; Need(!File.Exists(p), "Slab output exists");
                AssetDatabase.CreateAsset(mesh, p); assets.Add(p);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
                var mc = go.AddComponent<MeshCollider>(); mc.cookingOptions = (MeshColliderCookingOptions)30; mc.sharedMesh = mesh;
                return go;
            }
            catch { UnityEngine.Object.DestroyImmediate(go); throw; }
        }
        public static string Install(string[] args)
        {
            Need(args.Length == 1 && !EditorApplication.isPlaying, "Provide pinned request in edit mode");
            var request = JObject.Parse(File.ReadAllText(args[0])); var scene = SceneManager.GetActiveScene();
            Need(!scene.isDirty && scene.path == "Assets/ChooGuard/Scenes/FpsStation.unity" && Hash(scene.path) == (string)request["scene_sha256"], "Reviewed clean epoch required");
            Need(!File.Exists(Receipt), "Floor extension already installed");
            foreach (var pin in request["files"]) Need(Hash((string)pin["path"]) == (string)pin["sha256"], "Evidence changed: " + (string)pin["path"]);
            ConcourseFidelityCorrection.Verify(Array.Empty<string>());
            var recipe = JObject.Parse(File.ReadAllText((string)request["recipe"]));
            var review = JObject.Parse(File.ReadAllText((string)request["review"]));
            Need((string)review["status"] == "PASS" && (string)review["packet_sha256"] == Hash((string)request["recipe"]), "Independent packet review differs");
            foreach (string field in new[] { "candidate_overlap_render_floor_area_px2", "candidate_overlap_collider_floor_area_px2", "candidate_overlap_reviewed_walls_area_px2", "candidate_overlap_source_void_area_px2", "candidate_overlap_source_bank_envelopes_area_px2", "candidate_overlap_B3_hold_band_area_px2", "candidate_overlap_outer_floor_candidate_area_px2" }) Need((double)recipe[field] == 0, "Source protection failed: " + field);
            Need((double)recipe["source_coordinate_regularization"]["positive_expansion_area_px2"] == 0 && recipe["meshes"].Count() == 3, "Reviewed floor count/extent differs");
            Directory.CreateDirectory(Work); string backup = Work + "/FpsStation-before.unity"; string registryBackup = Work + "/registry-before.json";
            File.Copy(scene.path, backup, false); File.Copy(ConcourseFidelityCorrection.Registry, registryBackup, false);
            var originalRegistry = JObject.Parse(File.ReadAllText(registryBackup));
            var parent = Platform6BankB1Replay.Find(Native6).parent;
            var n6State = State(Platform6BankB1Replay.Find(Native6));
            var stone = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Concourse_StoneTile.mat"); Need(stone != null, "Installed stone required");
            var created = new List<GameObject>(); var assets = new List<string>();
            try
            {
                foreach (var row in recipe["meshes"]) created.Add(Add(row, parent, stone, assets));
                Need(JToken.DeepEquals(n6State, State(Platform6BankB1Replay.Find(Native6))), "Retained source owner changed");
                AssetDatabase.SaveAssets(); Physics.SyncTransforms();
                var owners = new JArray(created.Select(go => new JObject { ["owner"] = Platform6BankB1Replay.PathOf(go.transform), ["state"] = State(go.transform), ["asset"] = Pin(go.GetComponent<MeshFilter>().sharedMesh) }));
                EditorSceneManager.MarkSceneDirty(scene); Need(EditorSceneManager.SaveScene(scene), "Save failed");
                var result = new JObject { ["schema"] = "chooguard.concourse-floor-extension.v1", ["status"] = "INSTALLED_REFERENCE_GEOMETRY_RUNTIME_PENDING", ["request"] = args[0], ["request_sha256"] = Hash(args[0]), ["before_scene_sha256"] = request["scene_sha256"], ["scene_sha256"] = Hash(scene.path), ["source_plan"] = recipe["source_plan"], ["source_plan_sha256"] = recipe["source_plan_sha256"], ["added_plan_floor_owners"] = owners, ["new_floor_model_area_m2"] = recipe["candidate_area_m2_in_model_frame"], ["numerical_underfill_px2"] = recipe["source_coordinate_regularization"]["omitted_positive_area_px2"], ["original_geometry_unchanged"] = true, ["site_accuracy_verified"] = false, ["whole_building_complete"] = false };
                foreach (var row in owners) ((JArray)originalRegistry["added_plan_floor_owners"]).Add(row.DeepClone());
                originalRegistry["scene_sha256"] = result["scene_sha256"];
                originalRegistry["floor_extensions"] = new JArray(new JObject { ["receipt"] = Receipt, ["request_sha256"] = Hash(args[0]), ["before_scene_sha256"] = request["scene_sha256"] });
                File.WriteAllText(ConcourseFidelityCorrection.Registry, originalRegistry.ToString());
                File.WriteAllText(Receipt, result.ToString()); File.WriteAllText(Work + "/receipt.json", result.ToString());
                ConcourseFidelityCorrection.Verify(Array.Empty<string>());
                return "SAVED_SOURCE_BETWEEN_BANK_FLOORS " + (string)result["scene_sha256"];
            }
            catch
            {
                foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                foreach (string p in assets) AssetDatabase.DeleteAsset(p);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                File.Copy(backup, scene.path, true); File.Copy(registryBackup, ConcourseFidelityCorrection.Registry, true);
                if (File.Exists(Receipt)) File.Delete(Receipt);
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                throw;
            }
        }
    }
}
#endif
