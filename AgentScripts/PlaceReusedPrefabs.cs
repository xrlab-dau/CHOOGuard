// 이미 존재하는 모델 자산을 씬에 배치한다. 새 지오메트리를 만들지 않는다.
//
// 임포터 스케일이 자산마다 제각각(cm/inch/임의)이라 targetSize 가 주어지면
// 배치 후 실측해 축별로 맞춘다. 바닥 스냅도 실측 기준이다.
//
// args: [placementsJsonPath]

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class PlaceReusedPrefabs
{
    const string RootName = "부산역 역사 내부 · 재사용 자산";

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/station-reused-placements.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        var existing = GameObject.Find(RootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var root = new GameObject(RootName);
        var world = GameObject.Find("FPSWorld");
        if (world != null) root.transform.SetParent(world.transform, false);

        var groups = new Dictionary<string, Transform>();
        var cache = new Dictionary<string, GameObject>();
        int placed = 0, missing = 0;
        var notFound = new HashSet<string>();

        foreach (JObject p in (JArray)spec["placements"])
        {
            string prefabPath = (string)p["prefab"];
            if (!cache.TryGetValue(prefabPath, out var pf))
            {
                pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                cache[prefabPath] = pf;
            }
            if (pf == null) { missing++; notFound.Add(prefabPath); continue; }

            string group = (string)p["group"];
            if (!groups.TryGetValue(group, out var parent))
            {
                var gg = new GameObject(group);
                gg.transform.SetParent(root.transform, false);
                parent = gg.transform; groups[group] = parent;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            inst.name = (string)p["name"];
            var pos = (JArray)p["pos"]; var eul = (JArray)p["euler"];
            inst.transform.position = new Vector3((float)pos[0], (float)pos[1], (float)pos[2]);
            inst.transform.rotation = Quaternion.Euler((float)eul[0], (float)eul[1], (float)eul[2]);

            var tgtJ = p["targetSize"] as JArray;
            if (tgtJ != null)
            {
                var tgt = new Vector3((float)tgtJ[0], (float)tgtJ[1], (float)tgtJ[2]);
                inst.transform.localScale = Vector3.one;
                var cur = Size(inst);
                inst.transform.localScale = new Vector3(
                    cur.x > 1e-5f ? tgt.x / cur.x : 1f,
                    cur.y > 1e-5f ? tgt.y / cur.y : 1f,
                    cur.z > 1e-5f ? tgt.z / cur.z : 1f);
                bool snap = p["snapBottom"] == null || (bool)p["snapBottom"];
                if (snap)
                {
                    var b = Bounds(inst);
                    inst.transform.position -= new Vector3(0f, b.min.y - (float)pos[1], 0f);
                }
                else
                {
                    var b = Bounds(inst);
                    inst.transform.position -= new Vector3(0f, b.max.y - (float)pos[1], 0f);
                }
            }
            else if (p["scale"] is JArray sc)
            {
                inst.transform.localScale = new Vector3((float)sc[0], (float)sc[1], (float)sc[2]);
            }
            placed++;
        }

        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        string diag = "placed=" + placed + " missing=" + missing +
                      (notFound.Count > 0 ? " | " + string.Join(", ", notFound) : "");
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/place-diag.txt"), diag);
        Debug.Log("PLACE_REUSED " + diag);
    }

    static Bounds Bounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool has = false;
        foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
        return b;
    }

    static Vector3 Size(GameObject go) => Bounds(go).size;
}
