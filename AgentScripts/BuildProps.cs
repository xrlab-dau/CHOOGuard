// PropsLane 의 chooguard.props-placement.v1 을 씬에 짓는다.
// 각 item 은 primitives[] 조합으로 만든다(FBX 불필요). finish 로 인스턴스 색·광택을 덮는다.
// supersedes 에 적힌 기존 크루드 박스는 먼저 제거한다.
//
// args: [propsPlacementJson]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class BuildProps
{
    const string RootName = "부산역 역사 내부 · 집기";
    const string MatDir = "Assets/ChooGuard/Art/StationInterior/Materials/Lane/";

    static float F(JToken t, float d = 0f) => t == null || t.Type == JTokenType.Null ? d : (float)t;
    static string S(JToken t) => t == null || t.Type == JTokenType.Null ? null : (string)t;

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/generated/props/props-placement.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        // ---- supersedes 제거 ----
        var sup = new HashSet<string>();
        var supTok = spec["supersedes"];
        if (supTok != null)
        {
            if (supTok.Type == JTokenType.Array)
                foreach (var t in (JArray)supTok)
                    sup.Add(t.Type == JTokenType.Object ? S(t["name"]) : (string)t);
            else if (supTok.Type == JTokenType.Object && supTok["names"] != null)
                foreach (var t in (JArray)supTok["names"]) sup.Add((string)t);
        }
        int removed = 0;
        if (sup.Count > 0)
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t == null) continue;
                if (sup.Contains(t.name)) { UnityEngine.Object.DestroyImmediate(t.gameObject); removed++; }
            }
        }

        // ---- 머티리얼 캐시 ----
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var baseCache = new Dictionary<string, Material>();
        Func<string, Material> getBase = key =>
        {
            if (key == null) key = "wall_painted_gypsum";
            if (baseCache.TryGetValue(key, out var m)) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + key + ".mat");
            if (m == null) m = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ChooGuard/Art/StationInterior/Materials/" + key + ".mat");
            baseCache[key] = m;
            return m;
        };
        var finishCache = new Dictionary<string, Material>();
        Func<string, JToken, Material> getFinish = (key, fin) =>
        {
            string col = fin != null ? S(fin["baseColor"]) : null;
            float sm = fin != null ? F(fin["smoothness"], -1f) : -1f;
            float me = fin != null ? F(fin["metallic"], -1f) : -1f;
            string id = (key ?? "def") + "|" + (col ?? "-") + "|" + sm.ToString("F2") + "|" + me.ToString("F2");
            if (finishCache.TryGetValue(id, out var m)) return m;
            var src = getBase(key);
            string safe = id.Replace("|", "_").Replace("#", "").Replace(".", "p");
            string mp = MatDir + "prop_" + safe + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null)
            {
                m = src != null ? new Material(src) : new Material(lit);
                AssetDatabase.CreateAsset(m, mp);
            }
            if (col != null && ColorUtility.TryParseHtmlString(col.StartsWith("#") ? col : "#" + col, out var c))
                m.SetColor("_BaseColor", c);
            if (sm >= 0f) m.SetFloat("_Smoothness", sm);
            if (me >= 0f) m.SetFloat("_Metallic", me);
            EditorUtility.SetDirty(m);
            finishCache[id] = m;
            return m;
        };

        var old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RootName);

        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var byKind = new Dictionary<string, Transform>();
        var counts = new Dictionary<string, int>();
        int made = 0, prims = 0;

        foreach (JObject it in (JArray)spec["items"])
        {
            string kind = S(it["kind"]) ?? "prop";
            if (!byKind.TryGetValue(kind, out var parent))
            {
                var g = new GameObject(kind);
                g.transform.SetParent(root.transform, false);
                parent = g.transform;
                byKind[kind] = parent;
            }

            var go = new GameObject(S(it["name"]));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(F(it["pos"][0]), F(it["pos"][1]), F(it["pos"][2]));
            go.transform.rotation = Quaternion.Euler(0f, F(it["rotY"]), 0f);
            var sc = it["scale"];
            if (sc != null) go.transform.localScale = new Vector3(F(sc[0], 1f), F(sc[1], 1f), F(sc[2], 1f));

            var plist = it["primitives"] as JArray;
            if (plist != null)
                foreach (JObject pr in plist)
                {
                    string shape = (S(pr["shape"]) ?? "cube").ToLowerInvariant();
                    var p = new GameObject(S(pr["name"]) ?? shape);
                    p.transform.SetParent(go.transform, false);
                    var lp = pr["localPos"];
                    p.transform.localPosition = lp != null
                        ? new Vector3(F(lp[0]), F(lp[1]), F(lp[2])) : Vector3.zero;
                    var lr = pr["localRotY"];
                    if (lr != null) p.transform.localRotation = Quaternion.Euler(0f, F(lr), 0f);
                    var ls = pr["localSize"];
                    var size = ls != null ? new Vector3(F(ls[0], 0.1f), F(ls[1], 0.1f), F(ls[2], 0.1f)) : Vector3.one * 0.1f;
                    p.transform.localScale = shape == "cylinder"
                        ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
                    p.AddComponent<MeshFilter>().sharedMesh = shape == "cylinder" ? cyl : cube;
                    var mr = p.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = getFinish(S(pr["materialKey"]) ?? S(pr["mat"]), pr["finish"]);
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    GameObjectUtility.SetStaticEditorFlags(p, StaticEditorFlags.BatchingStatic);
                    prims++;
                }

            counts[kind] = counts.TryGetValue(kind, out var c) ? c + 1 : 1;
            made++;
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        var sb = new StringBuilder();
        sb.Append("removedSuperseded=").Append(removed).Append(" items=").Append(made)
          .Append(" primitives=").Append(prims).Append("\n");
        foreach (var kv in counts.OrderByDescending(k => k.Value)) sb.Append("  ").Append(kv.Key).Append(" x").Append(kv.Value).Append("\n");
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/props-build.txt"), sb.ToString());
        Debug.Log("PROPS " + sb);
    }
}
