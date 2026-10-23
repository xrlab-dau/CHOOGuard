using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

public static class RemoveAllSceneLights20261019
{
    const string Root = "/Users/um-yunsang/CHOOGuard";
    const string Thread = "01a15356-cffc-7d10-98f2-0dfa569c689c";
    const string Scope = "C180_USER_DELETE_ALL_SCENE_LIGHTS_20261019";
    const string Work = ".state/ACTIVE-DELETE-ALL-SCENE-LIGHTS-20261019/";
    const string ScenePath = "Assets/ChooGuard/Scenes/FpsStation.unity";

    static void Guard()
    {
        var a = JObject.Parse(File.ReadAllText(Root + "/.state/WRITER-AUTHORITY.json"));
        if ((string)a["status"] != "ACTIVE" || (bool?)a["Native_inflight"] != true ||
            (string)a["sole_writer_thread"] != Thread || (string)a["scope"] != Scope)
            throw new Exception("Exact current lighting removal lease required");
        var g = JObject.Parse(File.ReadAllText(Root + "/" + (string)a["delegated_author_lease"]));
        if ((bool?)g["explicit_delegation"] != true || (string)g["worker_thread"] != Thread ||
            (string)g["scope"] != Scope || string.IsNullOrEmpty((string)a["scene_worker_grant_nonce"]) ||
            (string)g["grant_nonce"] != (string)a["scene_worker_grant_nonce"] ||
            (string)g["command_source_hash"] != (string)a["scene_worker_command_source_hash"])
            throw new Exception("Exact FIFO grant nonce/source required");
    }

    static string Sha(string path)
    {
        using (var h = SHA256.Create())
        using (var s = File.OpenRead(path))
            return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
    }

    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (var p = t; p != null; p = p.parent) parts.Add(p.name);
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    static bool IsLightData(Component c)
    {
        if (c == null) return false;
        var name = c.GetType().FullName;
        return name == "UnityEngine.Rendering.Universal.UniversalAdditionalLightData" ||
               name == "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData";
    }

    static void Write(string relative, JObject data)
    {
        var path = Root + "/" + relative;
        var temp = path + ".tmp";
        File.WriteAllText(temp, data.ToString());
        if (File.Exists(path)) File.Replace(temp, path, null);
        else File.Move(temp, path);
    }

    public static string Apply()
    {
        Guard();
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Current stopped FpsStation editing scene required");
        if (File.Exists(Root + "/" + Work + "APPLIED-REMOVE-ALL-SCENE-LIGHTS.json"))
            throw new Exception("Removal already has an actual receipt; do not blindly replay");

        var lights = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Light>(true)).ToArray();
        var original = JObject.Parse(File.ReadAllText(Root + "/" + Work + "TARGETS-BEFORE-REMOVAL.json"));
        var allTargets = (JArray)original["lights"].DeepClone();
        var deleted = new JArray();
        var deletedObjects = new JArray();
        var removedData = new JArray();
        string before = Sha(Root + "/" + ScenePath);

        foreach (var light in lights)
        {
            var go = light.gameObject;
            var p = go.transform.position;
            deleted.Add(new JObject {
                ["component_instance_id"] = light.GetInstanceID(),
                ["component_global_id"] = GlobalObjectId.GetGlobalObjectIdSlow(light).ToString(),
                ["game_object_instance_id"] = go.GetInstanceID(),
                ["game_object_global_id"] = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString(),
                ["path"] = PathOf(go.transform),
                ["type"] = light.type.ToString(),
                ["bake_type"] = light.lightmapBakeType.ToString(),
                ["enabled"] = light.enabled,
                ["active_self"] = go.activeSelf,
                ["active_in_hierarchy"] = go.activeInHierarchy,
                ["intensity"] = light.intensity,
                ["range"] = light.range,
                ["shadows"] = light.shadows.ToString(),
                ["world_position"] = new JArray(p.x, p.y, p.z)
            });
        }
        foreach (var target in deleted)
            if (!allTargets.Any(t => (string)t["component_global_id"] == (string)target["component_global_id"]))
                allTargets.Add(target.DeepClone());
        var pending = new JObject {
            ["status"] = "EXACT_TARGETS_FROZEN_BEFORE_REMOVAL",
            ["utc"] = DateTime.UtcNow.ToString("o"),
            ["scope"] = Scope,
            ["author"] = Thread,
            ["scene"] = ScenePath,
            ["scene_sha256_before"] = before,
            ["target_light_count"] = lights.Length,
            ["lights"] = deleted
        };
        Write(Work + "TARGETS-BEFORE-REMAINING-REMOVAL-003.json", pending);

        foreach (var light in lights)
        {
            Guard();
            var go = light.gameObject;
            var components = go.GetComponents<Component>();
            bool lightOnlyLeaf = go.transform.childCount == 0 && components.All(
                c => c != null && (c is Transform || c is Light || IsLightData(c)));
            foreach (var c in components.Where(IsLightData))
                removedData.Add(new JObject {
                    ["path"] = PathOf(go.transform),
                    ["component_instance_id"] = c.GetInstanceID(),
                    ["type"] = c.GetType().FullName,
                    ["hide_flags_before"] = (int)c.hideFlags
                });
            // Exact live failure 002 identified bit 64 on additional light data as
            // blocking deletion. Clear only that bit on the objects being deleted.
            foreach (var c in components.Where(IsLightData))
                c.hideFlags = (HideFlags)((int)c.hideFlags & ~64);
            light.hideFlags = (HideFlags)((int)light.hideFlags & ~64);
            if (lightOnlyLeaf)
            {
                deletedObjects.Add(new JObject {
                    ["path"] = PathOf(go.transform),
                    ["game_object_instance_id"] = go.GetInstanceID(),
                    ["game_object_global_id"] = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString()
                });
                go.hideFlags = (HideFlags)((int)go.hideFlags & ~64);
                Undo.DestroyObjectImmediate(go);
            }
            else
            {
                foreach (var c in components.Where(IsLightData))
                    Undo.DestroyObjectImmediate(c);
                Undo.DestroyObjectImmediate(light);
            }
            if (light != null)
            {
                Write(Work + "REMAINING-REMOVAL-ERROR-003.json", new JObject {
                    ["path"] = PathOf(go.transform),
                    ["light_id"] = light.GetInstanceID(),
                    ["light_hide_flags"] = light.hideFlags.ToString(),
                    ["go_hide_flags"] = go.hideFlags.ToString(),
                    ["persistent"] = EditorUtility.IsPersistent(light),
                    ["prefab_instance"] = PrefabUtility.IsPartOfPrefabInstance(go),
                    ["components"] = new JArray(go.GetComponents<Component>().Select(
                        c => c == null ? "MISSING" : c.GetType().FullName + ":" + c.hideFlags))
                });
                throw new Exception("A requested Light could not be removed: " + PathOf(go.transform));
            }
        }

        Guard();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed");
        var receipt = new JObject {
            ["status"] = "ALL_CURRENT_SCENE_LIGHTS_REMOVED_APPLIED_SAVED",
            ["utc"] = DateTime.UtcNow.ToString("o"),
            ["scope"] = Scope,
            ["author"] = Thread,
            ["user_authority"] = Work + "USER-AUTHORITY.json",
            ["scene"] = ScenePath,
            ["removed_light_components"] = allTargets.Count,
            ["removed_light_components_in_this_call"] = lights.Length,
            ["previous_settled_call_partial_removals"] = allTargets.Count - lights.Length,
            ["removed_light_only_game_objects"] = deletedObjects.Count,
            ["removed_additional_light_data_components"] = removedData.Count,
            ["inactive_lights_included"] = true,
            ["exact_deleted_lights"] = allTargets,
            ["exact_deleted_light_only_objects"] = deletedObjects,
            ["exact_removed_additional_light_data"] = removedData,
            ["scene_sha256_before"] = (string)original["scene_sha256_before"],
            ["scene_sha256_before_remaining_removal"] = before,
            ["scene_sha256_after"] = Sha(Root + "/" + ScenePath),
            ["original_source_assets_changed"] = false,
            ["fixture_architecture_meshes_changed"] = false,
            ["QA_Play_reopen_probes_captures"] = false,
            ["runtime_light_creation_scripts_changed"] = false,
            ["performance_gain_measured"] = false,
            ["do_not_restore"] = "User-deleted scene Light components and light-only objects must not be recreated from historical store/sign/optimization packets."
        };
        Write(Work + "APPLIED-REMOVE-ALL-SCENE-LIGHTS.json", receipt);
        Write(".state/USER-DELETED-ALL-SCENE-LIGHTS-20261019.json", receipt);
        Write(".state/CURRENT-SCENE-LIGHTS-REMOVED-20261019.json", new JObject {
            ["status"] = (string)receipt["status"],
            ["utc"] = (string)receipt["utc"],
            ["scene"] = ScenePath,
            ["removed_light_components"] = allTargets.Count,
            ["actual_receipt"] = Work + "APPLIED-REMOVE-ALL-SCENE-LIGHTS.json",
            ["tombstones"] = ".state/USER-DELETED-ALL-SCENE-LIGHTS-20261019.json",
            ["source_prefabs_and_runtime_emergency_systems_unchanged"] = true
        });
        return new JObject {
            ["status"] = (string)receipt["status"],
            ["removed_light_components"] = allTargets.Count,
            ["removed_light_only_game_objects"] = deletedObjects.Count,
            ["removed_additional_light_data_components"] = removedData.Count,
            ["scene_sha256_after"] = (string)receipt["scene_sha256_after"],
            ["receipt"] = Work + "APPLIED-REMOVE-ALL-SCENE-LIGHTS.json"
        }.ToString();
    }
}
