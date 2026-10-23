using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;

public static class InformationMetricCoreR30
{
    const string Root = "/Users/um-yunsang/CHOOGuard";
    const string Base = ".state/ACTIVE-ALL2F-COMPLETION-R26/public/information-desk/constructor-r30";
    const string Thread = "classic:/root/r28_police_multiplan";
    const string Scope = "C180_R30_INFORMATION_CORE_APPLY_001";
    const string PacketSHA = "15f11fb0dc97093359ab90aeaad743ffb1e50e9610b016e4d06a500989dd8ed7";
    const string MaterialBase = "Assets/ChooGuard/Art/StationInterior/All2FCompletionR26/InformationDesk";
    const string HeldID = "GlobalObjectId_V1-2-7518d664dafa749f2b6d5cba42bb798c-619052705-0";

    static string Sha(string path)
    {
        using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }

    static JObject Guard()
    {
        var a = JObject.Parse(File.ReadAllText(Root + "/.state/WRITER-AUTHORITY.json"));
        if ((string)a["status"] != "ACTIVE" || (bool?)a["Native_inflight"] != true ||
            (string)a["sole_writer_thread"] != Thread || (string)a["scope"] != Scope)
            throw new Exception("Exact own C180 information core construction lease required");
        var grant = JObject.Parse(File.ReadAllText(Root + "/" + (string)a["delegated_author_lease"]));
        if ((string)grant["grant_nonce"] != (string)a["scene_worker_grant_nonce"])
            throw new Exception("Exact FIFO grant nonce required");
        return a;
    }

    static Vector3 V(JToken x) { return new Vector3((float)x[0], (float)x[1], (float)x[2]); }
    static JArray J(Vector3 v) { return new JArray(v.x, v.y, v.z); }
    static JArray J(Quaternion q) { return new JArray(q.x, q.y, q.z, q.w); }
    static JArray J(Matrix4x4 m) { var a = new JArray(); for (int i = 0; i < 16; i++) a.Add(m[i]); return a; }
    static JObject Trs(Transform t)
    {
        return new JObject { ["position"] = J(t.localPosition), ["quaternion"] = J(t.localRotation), ["scale"] = J(t.localScale) };
    }
    static string PathOf(Transform t)
    {
        var names = new List<string>(); while (t != null) { names.Add(t.name); t = t.parent; }
        names.Reverse(); return string.Join("/", names);
    }
    static GameObject Obj(string id)
    {
        GlobalObjectId parsed;
        if (!GlobalObjectId.TryParse(id, out parsed)) throw new Exception("Invalid exact own object ID: " + id);
        var go = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as GameObject;
        if (go == null) throw new Exception("Exact construction object is absent: " + id);
        return go;
    }
    static void RequireTrs(Transform t, JToken expected)
    {
        if (Vector3.Distance(t.localPosition, V(expected["position"])) > .001f ||
            Quaternion.Angle(t.localRotation, Quaternion.Euler(V(expected["euler"]))) > .05f ||
            Vector3.Distance(t.localScale, V(expected["scale"])) > .001f)
            throw new Exception("Current own part was edited; do not overwrite its local transform: " + PathOf(t));
    }
    static void RequireWorld(Transform t, JToken expected)
    {
        if (expected == null) return;
        var m = t.localToWorldMatrix;
        for (int i = 0; i < 16; i++)
            if (Mathf.Abs(m[i] - (float)expected[i]) > .002f)
                throw new Exception("Exact own current world placement changed: " + PathOf(t));
    }
    static void Dir(string path)
    {
        string[] parts = path.Split('/'); string p = parts[0];
        for (int i = 1; i < parts.Length; i++)
        { string q = p + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(q)) AssetDatabase.CreateFolder(p, parts[i]); p = q; }
    }

    public static string Apply()
    {
        var lease = Guard();
        string packetFile = Root + "/" + Base + "/ROOT-ELECTED-INFO-CORE-PACKET-R30.json";
        if (Sha(packetFile) != PacketSHA) throw new Exception("Exact frozen elected118 construction packet required");
        var p = JObject.Parse(File.ReadAllText(packetFile));
        if ((string)p["author"] != Thread || (string)p["scope"] != Scope)
            throw new Exception("Packet author/scope mismatch");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/ChooGuard/Scenes/FpsStation.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Current stopped editing FpsStation required");
        string receiptPath = Base + "/APPLIED-INFORMATION-METRIC-CORE-R30.json";
        string folder = (string)p["runtime_asset_folder"];
        if (File.Exists(Root + "/" + receiptPath) || AssetDatabase.IsValidFolder(folder))
            throw new Exception("No replay or overwrite of prior actual R30 construction");
        var owner = Obj((string)p["current_own_root_id"]);
        if (PathOf(owner.transform) != (string)p["current_own_root_path"])
            throw new Exception("Exact current own information root required");
        var native = Obj((string)p["fixed_native024_id"]);
        var axis = p["fixed_world_xz"];
        if (Mathf.Abs(owner.transform.position.x - (float)axis[0]) > .002f ||
            Mathf.Abs(owner.transform.position.z - (float)axis[1]) > .002f)
            throw new Exception("Keep existing fixed root axis; do not force earlier placement");
        var nativeBounds = native.GetComponent<Renderer>().bounds;
        if (Mathf.Abs(nativeBounds.center.x - (float)axis[0]) > .025f ||
            Mathf.Abs(nativeBounds.center.z - (float)axis[1]) > .025f)
            throw new Exception("Fixed native024 source anchor changed");
        var held = Obj(HeldID);
        var heldMesh = held.GetComponent<MeshFilter>();
        var heldState = new JObject { ["object_id"] = HeldID, ["local_TRS"] = Trs(held.transform),
            ["mesh_asset"] = AssetDatabase.GetAssetPath(heldMesh.sharedMesh) };
        var rootState = new JObject { ["object_id"] = (string)p["current_own_root_id"],
            ["local_TRS"] = Trs(owner.transform), ["world_matrix_column_major"] = J(owner.transform.localToWorldMatrix) };
        var changes = (JArray)p["changes"];
        if (changes.Count != 118 || changes.Count(x => (string)x["operation"] == "replace_only_own_mesh_derivative") != 6 ||
            changes.Count(x => (string)x["operation"] == "adjust_only_own_local_TRS") != 111 ||
            changes.Count(x => (string)x["operation"] == "new_own_closed3D_sourceglyph_mesh") != 1)
            throw new Exception("Only root-elected exact118 delta is authorized");
        var targets = new Dictionary<string, GameObject>();
        var expectedKeys = new HashSet<string>();
        foreach (var c in changes)
        {
            string op = (string)c["operation"];
            if (op == "new_own_closed3D_sourceglyph_mesh")
            {
                expectedKeys.Add((string)c["mesh_key"]);
                if (owner.transform.Find((string)c["group"] + "/" + (string)c["id"]) != null)
                    throw new Exception("Exact new source logo already exists; no duplicate");
                continue;
            }
            string id = (string)c["object_id"];
            if (id == HeldID) throw new Exception("Held original-only private worktop is forbidden");
            var go = Obj(id);
            if (!go.transform.IsChildOf(owner.transform) || PathOf(go.transform) != (string)c["path"])
                throw new Exception("Exact own child path required: " + (string)c["id"]);
            RequireWorld(go.transform, c["expected_world_matrix_column_major"]);
            RequireTrs(go.transform, op == "replace_only_own_mesh_derivative" ? c["old_local_TRS"] : c["expected_old_local_TRS"]);
            if (op == "replace_only_own_mesh_derivative")
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || AssetDatabase.GetAssetPath(mf.sharedMesh) != (string)c["expected_mesh_asset"])
                    throw new Exception("Current mesh changed; preserve user edit: " + go.name);
                expectedKeys.Add((string)c["new_mesh_key"]);
            }
            targets.Add(id, go);
        }
        if (expectedKeys.Count != 7 || expectedKeys.Contains("Staff_Inner_Worktop") || ((JObject)p["mesh_library"]).Count != 7)
            throw new Exception("Only seven elected own mesh derivatives; held private payload forbidden");
        var steel = AssetDatabase.LoadAssetAtPath<Material>(MaterialBase + "/MAT_steel.mat");
        var white = AssetDatabase.LoadAssetAtPath<Material>(MaterialBase + "/MAT_white.mat");
        if (steel == null || white == null) throw new Exception("Existing own source materials required");

        string before = Sha(Root + "/" + scene.path);
        bool saved = false, folderCreated = false;
        Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("R30 elected information desk original/current metric core");
        var madeAssets = new JArray(); var actual = new JArray(); var newObjects = new List<GameObject>();
        try
        {
            Guard(); Dir(folder); folderCreated = true;
            var meshes = new Dictionary<string, Mesh>();
            foreach (string key in expectedKeys)
            {
                var spec = p["mesh_library"][key];
                if (spec == null) throw new Exception("Missing elected mesh payload: " + key);
                var m = new Mesh { name = "R30_Information_" + key };
                if (spec["vertices"].Count() > 65000) m.indexFormat = IndexFormat.UInt32;
                m.vertices = spec["vertices"].Select(V).ToArray();
                m.triangles = spec["triangles"].Select(v => (int)v).ToArray();
                m.RecalculateNormals(); m.RecalculateBounds();
                string asset = folder + "/MESH_" + key + ".asset";
                AssetDatabase.CreateAsset(m, asset); madeAssets.Add(asset); meshes.Add(key, m);
            }
            foreach (var c in changes)
            {
                Guard(); string op = (string)c["operation"];
                if (op == "new_own_closed3D_sourceglyph_mesh")
                {
                    string groupName = (string)c["group"];
                    var group = owner.transform.Find(groupName);
                    if (group == null)
                    {
                        var gg = new GameObject(groupName); Undo.RegisterCreatedObjectUndo(gg, "R30 own physical contour group");
                        gg.transform.SetParent(owner.transform, false); group = gg.transform;
                    }
                    var go = new GameObject((string)c["id"]);
                    Undo.RegisterCreatedObjectUndo(go, "R30 actual source closed counterfront glyphs");
                    go.transform.SetParent(group, false);
                    go.transform.localPosition = V(c["position_local"]);
                    go.transform.localRotation = Quaternion.Euler(V(c["euler_local"]));
                    go.transform.localScale = V(c["scale_local"]);
                    go.AddComponent<MeshFilter>().sharedMesh = meshes[(string)c["mesh_key"]];
                    var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = white;
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                    newObjects.Add(go);
                    actual.Add(new JObject { ["operation"] = op, ["id"] = go.name, ["path"] = PathOf(go.transform),
                        ["mesh_asset"] = AssetDatabase.GetAssetPath(meshes[(string)c["mesh_key"]]),
                        ["local_TRS"] = Trs(go.transform), ["source_current_file"] = c["source_current_file"].DeepClone(),
                        ["source_crop_px"] = c["source_crop_px"].DeepClone(), ["real_closed_counterholes"] = true });
                    continue;
                }
                var target = targets[(string)c["object_id"]];
                if (op == "replace_only_own_mesh_derivative")
                {
                    var mf = target.GetComponent<MeshFilter>(); var oldMesh = mf.sharedMesh;
                    var next = meshes[(string)c["new_mesh_key"]];
                    Undo.RecordObject(mf, "R30 own source metric mesh derivative"); mf.sharedMesh = next; EditorUtility.SetDirty(mf);
                    int colliderChanges = 0;
                    foreach (var mc in target.GetComponents<MeshCollider>())
                    {
                        if (mc.sharedMesh != oldMesh) continue;
                        Undo.RecordObject(mc, "R30 matching own physical collider derivative");
                        mc.sharedMesh = next; EditorUtility.SetDirty(mc); colliderChanges++;
                    }
                    if ((string)c["new_material_from_existing_own_packet_key"] == "steel")
                    {
                        var renderer = target.GetComponent<Renderer>();
                        Undo.RecordObject(renderer, "R30 existing own metal plinth finish");
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => steel).ToArray(); EditorUtility.SetDirty(renderer);
                    }
                    actual.Add(new JObject { ["operation"] = op, ["id"] = target.name,
                        ["object_id"] = (string)c["object_id"], ["path"] = PathOf(target.transform),
                        ["previous_mesh_asset"] = AssetDatabase.GetAssetPath(oldMesh),
                        ["new_mesh_asset"] = AssetDatabase.GetAssetPath(next), ["matching_mesh_colliders_changed"] = colliderChanges,
                        ["local_TRS_unchanged"] = true,
                        ["material_source_key"] = c["new_material_from_existing_own_packet_key"] == null ? "preserved" : (string)c["new_material_from_existing_own_packet_key"] });
                }
                else if (op == "adjust_only_own_local_TRS")
                {
                    var old = Trs(target.transform); var next = c["new_local_TRS"];
                    Undo.RecordObject(target.transform, "R30 coherent own physical assembly transform");
                    target.transform.localPosition = V(next["position"]);
                    target.transform.localRotation = Quaternion.Euler(V(next["euler"]));
                    target.transform.localScale = V(next["scale"]); EditorUtility.SetDirty(target.transform);
                    actual.Add(new JObject { ["operation"] = op, ["id"] = target.name,
                        ["object_id"] = (string)c["object_id"], ["path"] = PathOf(target.transform),
                        ["before_local_TRS"] = old, ["after_local_TRS"] = Trs(target.transform),
                        ["mesh_materials_unchanged"] = true });
                }
                else throw new Exception("Unauthorized operation in elected packet");
            }
            Guard(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Information metric core actual scene save failed");
            saved = true; Undo.CollapseUndoOperations(undo);
            foreach (var go in newObjects)
                foreach (var row in actual.Where(x => (string)x["path"] == PathOf(go.transform)))
                    row["object_id"] = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
            var receipt = new JObject
            {
                ["status"] = "INFORMATION_DESK_R30_ELECTED118_METRIC_CORE_APPLIED_SAVED",
                ["utc"] = DateTime.UtcNow.ToString("o"), ["author"] = Thread, ["scope"] = Scope,
                ["grant_nonce"] = (string)lease["scene_worker_grant_nonce"], ["scene_saved"] = true,
                ["scene_sha256_before"] = before, ["scene_sha256_after"] = Sha(Root + "/" + scene.path),
                ["packet"] = Base + "/ROOT-ELECTED-INFO-CORE-PACKET-R30.json", ["packet_sha256"] = PacketSHA,
                ["root_authority"] = ".state/ACTIVE-ALL2F-COMPLETION-R26/public/information-desk/source-original-r30/ROOT-CONSTRUCTION-AUTHORITY-R30.json",
                ["source_qualification"] = "SOURCE_MODEL: authentic2016 metric core elected against Nov2025 wavecounter/blacktruss morphology; not calibrated field or surveyed equality",
                ["root_state_preserved"] = rootState, ["fixed_native024_id"] = (string)p["fixed_native024_id"],
                ["held_private_worktop_preserved"] = heldState,
                ["countertop_outer_diameter_m"] = 7.0, ["countertop_height_from_floor_m"] = 1.20,
                ["canopy_outer_diameter_m"] = 5.0,
                ["current_changed_era_canopy_display_cap_Y_preserved"] = true,
                ["current_rear_angular_opening_preserved"] = true,
                ["mesh_replacement_count"] = 6, ["local_TRS_change_count"] = 111, ["new_closed_physical_glyph_count"] = 1,
                ["actual_change_count"] = actual.Count, ["changes"] = actual, ["created_own_mesh_assets"] = madeAssets,
                ["source_assets_overwritten"] = false, ["native_floor_columns_other_public_1F_other_stores_modified"] = false,
                ["Light_Text_TMP_raster_sign_fontatlas_created"] = false,
                ["QA_Play_tests_probe_reopen_capture"] = false, ["whole2F_complete_or_99percent_claimed"] = false
            };
            File.WriteAllText(Root + "/" + receiptPath, receipt.ToString());
            return new JObject { ["status"] = receipt["status"], ["scene_saved"] = true,
                ["actual_change_count"] = actual.Count, ["receipt"] = receiptPath }.ToString();
        }
        catch
        {
            if (!saved) { Undo.RevertAllDownToGroup(undo); if (folderCreated) AssetDatabase.DeleteAsset(folder); }
            throw;
        }
    }
}
