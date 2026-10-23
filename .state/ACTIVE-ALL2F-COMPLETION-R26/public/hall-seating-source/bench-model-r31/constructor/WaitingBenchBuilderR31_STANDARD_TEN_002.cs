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

public static class WaitingBenchBuilderR31
{
    const string Root = "/Users/um-yunsang/CHOOGuard";
    const string Thread = "classic:/root/r31_waiting_bench_model";
    const string Scope = "C180_R31_PUBLIC_WAITING_BENCH_STANDARD_TEN_002";
    const string BatchPath = ".state/ACTIVE-ALL2F-COMPLETION-R26/public/hall-seating-source/bench-model-r31/constructor/FROZEN-STANDARD-TEN-002.json";
    const string BatchSHA = "13ce75aae649ac830f937070af2b0d3e6535805c8d74eb3640f9b91cf8d11d44";
    const string AuthorityPath = ".state/ACTIVE-ALL2F-COMPLETION-R26/public/hall-seating-source/bench-model-r31/ROOT-CONSTRUCTION-AUTHORITY-R31.json";
    const string Base = ".state/ACTIVE-ALL2F-COMPLETION-R26/public/hall-seating-source/bench-model-r31/constructor";
    const string AssetRoot = "Assets/ChooGuard/Art/StationInterior/PublicWaitingBenchesR31_20261019";
    const string RegisterPath = Base + "/SHARED-ASSET-REGISTER-R31.json";
    static readonly List<string> CreatedAssets = new List<string>();
    static readonly List<string> CreatedFolders = new List<string>();
    static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    static JObject Registry;
    static string ActiveNonce;
    static string Full(string path) { return Path.GetFullPath(Path.Combine(Root, path)); }
    static string SHA(string path) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    static JObject Read(string path) { return JObject.Parse(File.ReadAllText(Full(path))); }
    static Vector3 V(JToken value) { return new Vector3((float)value[0], (float)value[1], (float)value[2]); }
    static JArray JV(Vector3 value) { return new JArray(value.x, value.y, value.z); }
    static string PathOf(Transform t) { return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name; }

    static JObject Guard()
    {
        var a = Read(".state/WRITER-AUTHORITY.json");
        if ((string)a["status"] != "ACTIVE" || (bool?)a["Native_inflight"] != true ||
            (string)a["sole_writer_thread"] != Thread || (string)a["scope"] != Scope ||
            !Scope.StartsWith("C180_R31_PUBLIC_WAITING_BENCH_", StringComparison.Ordinal))
            throw new Exception("Exact active C180 waiting-bench author/scope required.");
        string nonce = (string)a["scene_worker_grant_nonce"];
        if (string.IsNullOrEmpty(nonce) || (ActiveNonce != null && ActiveNonce != nonce))
            throw new Exception("Exact C180 nonce required; never reclaim an unknown call.");
        var g = Read((string)a["delegated_author_lease"]);
        if ((string)g["grant_nonce"] != nonce || (string)g["worker_thread"] != Thread ||
            (string)g["scope"] != Scope || (bool?)g["explicit_delegation"] != true ||
            (string)g["request"] != (string)a["scene_worker_request"] ||
            (string)g["command_source_hash"] != (string)a["scene_worker_command_source_hash"])
            throw new Exception("Exact FIFO delegated grant, request and command source required.");
        ActiveNonce = nonce;
        return a;
    }

    static Transform FindExact(string path)
    {
        Transform current = null;
        foreach (string part in path.Split('/'))
        {
            current = current == null
                ? SceneManager.GetActiveScene().GetRootGameObjects().Select(x => x.transform).SingleOrDefault(x => x.name == part)
                : Enumerable.Range(0, current.childCount).Select(i => current.GetChild(i)).SingleOrDefault(x => x.name == part);
            if (current == null) throw new Exception("Existing fixed parent missing: " + path);
        }
        return current;
    }

    static void OwnFolder(string path)
    {
        if (!(path == AssetRoot || path.StartsWith(AssetRoot + "/", StringComparison.Ordinal)))
            throw new Exception("Own new Assets boundary required.");
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) OwnFolder(parent);
        Guard();
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        CreatedFolders.Add(path);
    }

    static T Reuse<T>(string path, string sourceSHA) where T : UnityEngine.Object
    {
        var entry = Registry[path] as JObject;
        if (entry == null || (string)entry["source_sha256"] != sourceSHA ||
            !File.Exists(Full(path)) || SHA(Full(path)) != (string)entry["asset_sha256"])
            throw new Exception("Own cached asset provenance changed; never overwrite current edits: " + path);
        var item = AssetDatabase.LoadAssetAtPath<T>(path);
        if (item == null) throw new Exception("Own cached asset type changed: " + path);
        return item;
    }

    static void Remember(string path, string sourceSHA, UnityEngine.Object item)
    {
        Guard();
        AssetDatabase.SaveAssetIfDirty(item);
        Registry[path] = new JObject { ["source_sha256"] = sourceSHA, ["asset_sha256"] = SHA(Full(path)), ["kind"] = item.GetType().Name };
    }

    static Mesh MeshFor(JObject model, JObject hashes, string key)
    {
        string hash = (string)hashes[key];
        if (Meshes.ContainsKey(hash)) return Meshes[hash];
        if (hash == null || hash.Length != 64) throw new Exception("Frozen mesh provenance required.");
        string path = AssetRoot + "/Meshes/MESH_" + hash + ".asset";
        if (File.Exists(Full(path))) return Meshes[hash] = Reuse<Mesh>(path, hash);
        Guard();
        var spec = model["mesh_library"][key];
        var mesh = new Mesh { name = "R31_SHARED_" + hash };
        if (spec["vertices"].Count() > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = spec["vertices"].Select(V).ToArray();
        mesh.normals = spec["normals"].Select(V).ToArray();
        mesh.uv = spec["uv"].Select(x => new Vector2((float)x[0], (float)x[1])).ToArray();
        mesh.triangles = spec["triangles"].Select(x => (int)x).ToArray();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        CreatedAssets.Add(path);
        Remember(path, hash, mesh);
        return Meshes[hash] = mesh;
    }

    static Material MaterialFor(JObject model, JObject hashes, string key)
    {
        string hash = (string)hashes[key];
        if (Materials.ContainsKey(hash)) return Materials[hash];
        if (hash == null || hash.Length != 64) throw new Exception("Frozen material provenance required.");
        string path = AssetRoot + "/Materials/MAT_" + hash + ".mat";
        if (File.Exists(Full(path))) return Materials[hash] = Reuse<Material>(path, hash);
        Guard();
        var spec = model["materials"][key];
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("Current URP Lit required.");
        var mat = new Material(shader) { name = "R31_" + key + "_" + hash, enableInstancing = true };
        var color = spec["base_color_srgb"];
        mat.SetColor("_BaseColor", new Color((float)color[0], (float)color[1], (float)color[2], 1));
        mat.SetFloat("_Metallic", (float)spec["metallic"]);
        mat.SetFloat("_Smoothness", 1 - (float)spec["roughness"]);
        mat.SetFloat("_Surface", 0);
        mat.SetFloat("_Cull", 2);
        mat.SetFloat("_AlphaClip", 0);
        mat.DisableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(mat, path);
        CreatedAssets.Add(path);
        Remember(path, hash, mat);
        return Materials[hash] = mat;
    }

    static Matrix4x4 Matrix(JToken rows)
    {
        var m = Matrix4x4.identity;
        for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) m[r, c] = (float)rows[r][c];
        if (Mathf.Abs(m.determinant - 1) > 0.0001f || Vector3.Distance(m.lossyScale, Vector3.one) > 0.0001f)
            throw new Exception("Only rigid source-part transforms; no mirrored glyphs or geometry stretching.");
        return m;
    }

    public static string Apply()
    {
        var auth = Guard();
        if (SHA(Full(BatchPath)) != BatchSHA) throw new Exception("Exact frozen batch required.");
        var batch = Read(BatchPath);
        if ((string)batch["author"] != Thread || (string)batch["scope"] != Scope) throw new Exception("Frozen own batch author mismatch.");
        var authority = Read(AuthorityPath);
        if ((string)authority["author"] != Thread) throw new Exception("Root waiting-bench delegation changed.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/ChooGuard/Scenes/FpsStation.unity" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Current stopped FpsStation editing scene required.");
        var bankSpecs = (JArray)batch["banks"];
        var models = new Dictionary<string, JObject>();
        var parents = new Dictionary<string, Transform>();
        foreach (JObject bank in bankSpecs)
        {
            string pose = (string)bank["pose_file"], poseSHA = (string)bank["pose_sha256"];
            var elected = ((JArray)authority["authorized_placement_packets"]).OfType<JObject>().SingleOrDefault(x =>
                (string)x["path"] == pose && (string)x["sha256"] == poseSHA && (string)x["constructor_scope"] == Scope &&
                ((JArray)x["bank_ids"]).Any(y => (string)y == (string)bank["id"]));
            if (elected == null || SHA(Full(pose)) != poseSHA) throw new Exception("Exact root-elected individual bank source pose required.");
            string modelFile = (string)bank["model_file"];
            if (!modelFile.StartsWith(".state/ACTIVE-ALL2F-COMPLETION-R26/public/hall-seating-source/bench-model-r31/mesh-packet/", StringComparison.Ordinal) ||
                SHA(Full(modelFile)) != (string)bank["model_sha256"]) throw new Exception("Original immutable own model SHA required.");
            var model = Read(modelFile);
            if ((bool?)model["assemblies"][(string)bank["assembly"]]["construction_module_only"] != false)
                throw new Exception("A construction module cannot become an asserted physical bank.");
            if ((string)bank["family"] != "priority" && (string)bank["family"] != "standard") throw new Exception("Only actual standard/priority source families.");
            models[(string)bank["id"]] = model;
            string rootName = (string)bank["root_name"], parentPath = (string)bank["parent_path"];
            if (!rootName.StartsWith("MAIN2F_R31_PUBLIC_WAITING_BENCH_", StringComparison.Ordinal) || rootName.Contains('/'))
                throw new Exception("New own bench root boundary required.");
            var parent = FindExact(parentPath);
            if (Vector3.Distance(parent.lossyScale, Vector3.one) > 0.0001f || parent.Find(rootName) != null)
                throw new Exception("Fixed parent changed or bench already exists; no replay/overwriting.");
            parents[(string)bank["id"]] = parent;
            Vector3 x = V(bank["localX_world"]), z = V(bank["localZ_world"]);
            Quaternion rotation = Quaternion.Euler(0, (float)bank["world_yaw"], 0);
            if (Vector3.Distance(rotation * Vector3.right, x) > 0.00001f || Vector3.Distance(rotation * Vector3.forward, z) > 0.00001f)
                throw new Exception("Exact rigid source-facing basis required; no mirrored lettering.");
        }
        Registry = File.Exists(Full(RegisterPath)) ? Read(RegisterPath) : new JObject();
        string before = SHA(Full(scene.path));
        bool dirtyBefore = scene.isDirty, saved = false;
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        var roots = new List<GameObject>();
        var parts = new List<GameObject>();
        var records = new JArray();
        try
        {
            OwnFolder(AssetRoot);
            OwnFolder(AssetRoot + "/Meshes");
            OwnFolder(AssetRoot + "/Materials");
            foreach (JObject bank in bankSpecs)
            {
                Guard();
                string id = (string)bank["id"];
                var model = models[id];
                var root = new GameObject((string)bank["root_name"]);
                Undo.RegisterCreatedObjectUndo(root, "R31 source-bound public waiting bank");
                root.transform.SetParent(parents[id], false);
                root.transform.position = V(bank["world_origin"]);
                root.transform.rotation = Quaternion.Euler(0, (float)bank["world_yaw"], 0);
                root.transform.localScale = Vector3.one;
                root.layer = parents[id].gameObject.layer;
                roots.Add(root);
                var bankRecord = new JObject { ["id"] = id, ["root_path"] = PathOf(root.transform), ["family"] = bank["family"].DeepClone(),
                    ["source_pose"] = poseRecord(bank), ["model_file"] = bank["model_file"].DeepClone(), ["model_sha256"] = bank["model_sha256"].DeepClone(), ["parts"] = new JArray() };
                foreach (JObject spec in (JArray)model["assemblies"][(string)bank["assembly"]]["objects"])
                {
                    Guard();
                    var mesh = MeshFor(model, (JObject)bank["mesh_sha256"], (string)spec["mesh"]);
                    var material = MaterialFor(model, (JObject)bank["material_sha256"], (string)spec["material"]);
                    var go = new GameObject((string)spec["name"]);
                    Undo.RegisterCreatedObjectUndo(go, "R31 individual editable physical bench component");
                    go.transform.SetParent(root.transform, false);
                    Matrix4x4 m = Matrix(spec["local_to_module_matrix_rows"]);
                    go.transform.localPosition = m.GetColumn(3);
                    go.transform.localRotation = m.rotation;
                    go.transform.localScale = Vector3.one;
                    go.layer = root.layer;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    bool glyph = go.name.Contains("Glyph") || go.name.Contains("Legend") || go.name.Contains("TopMark");
                    if (!glyph) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    parts.Add(go);
                    ((JArray)bankRecord["parts"]).Add(new JObject { ["name"] = go.name, ["mesh_asset"] = AssetDatabase.GetAssetPath(mesh),
                        ["material_asset"] = AssetDatabase.GetAssetPath(material), ["source_mesh_sha256"] = bank["mesh_sha256"][(string)spec["mesh"]].DeepClone(),
                        ["source_semantic"] = spec["semantic"].DeepClone(), ["collider"] = !glyph });
                }
                records.Add(bankRecord);
            }
            Guard();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Actual waiting-bench Scene save failed.");
            saved = true;
            Undo.CollapseUndoOperations(undo);
            File.WriteAllText(Full(RegisterPath), Registry.ToString());
            for (int i = 0; i < roots.Count; i++) records[i]["root_object_id"] = GlobalObjectId.GetGlobalObjectIdSlow(roots[i]).ToString();
            int partIndex = 0;
            foreach (JObject bankRecord in records) foreach (JObject part in (JArray)bankRecord["parts"])
                part["object_id"] = GlobalObjectId.GetGlobalObjectIdSlow(parts[partIndex++]).ToString();
            var receipt = new JObject { ["status"] = "R31_SOURCE_BOUND_PUBLIC_WAITING_BENCHES_APPLIED_AND_SAVED", ["utc"] = DateTime.UtcNow.ToString("o"),
                ["author"] = Thread, ["scope"] = Scope, ["grant_nonce"] = ActiveNonce, ["batch_packet"] = BatchPath, ["batch_sha256"] = BatchSHA,
                ["banks"] = records, ["bank_count"] = roots.Count, ["editable_parts"] = parts.Count, ["new_assets"] = new JArray(CreatedAssets),
                ["shared_asset_register"] = RegisterPath, ["source_model_originals_unchanged"] = true, ["existing_scene_objects_modified"] = false,
                ["new_Light_Text_TMP_image_glyph_components"] = false, ["agent_QA_Play_tests_probes_reopen_capture"] = false,
                ["dirty_before"] = dirtyBefore, ["scene_sha256_before"] = before, ["scene_sha256_after"] = SHA(Full(scene.path)),
                ["qualification"] = "Individual root-elected SOURCE_MODEL placement and photo/epoch shape; not surveyed/current2026 inventory or whole2F completion." };
            string receiptPath = (string)batch["receipt_path"];
            File.WriteAllText(Full(receiptPath), receipt.ToString());
            return new JObject { ["status"] = receipt["status"], ["bank_count"] = roots.Count, ["editable_parts"] = parts.Count, ["receipt"] = receiptPath }.ToString();
        }
        catch
        {
            if (!saved)
            {
                Guard();
                Undo.RevertAllDownToGroup(undo);
                foreach (string path in CreatedAssets.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
                foreach (string path in CreatedFolders.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
            }
            throw;
        }
    }

    static JObject poseRecord(JObject bank)
    {
        return new JObject { ["file"] = bank["pose_file"].DeepClone(), ["sha256"] = bank["pose_sha256"].DeepClone(),
            ["world_origin"] = bank["world_origin"].DeepClone(), ["world_yaw"] = bank["world_yaw"].DeepClone(),
            ["localX_world"] = bank["localX_world"].DeepClone(), ["localZ_world"] = bank["localZ_world"].DeepClone(), ["rigid_no_mirror"] = true };
    }
}
