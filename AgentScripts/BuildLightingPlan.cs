// LightingLane 의 조도 역산 조명계획(chooguard.lighting-plan.v1)을 씬에 짓는다.
// kind: linear_troffer | downlight | cove | uplight
// 각 기구는 이미시브 판 + (선택) 광원. 임시로 만든 조명기구 그룹과 기존 실내조명은 대체한다.
//
// args: [lightingPlanJson]

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class BuildLightingPlan
{
    const string MatDir = "Assets/ChooGuard/Art/StationInterior/Materials/";
    const string RootName = "부산역 역사 내부 · 조명기구";

    static float F(JToken t, float d = 0f) => t == null || t.Type == JTokenType.Null ? d : (float)t;

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/generated/lighting/lighting-plan.json";
        var plan = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var matCache = new Dictionary<string, Material>();

        Func<string, Color, float, Material> mkMat = (id, col, inten) =>
        {
            if (matCache.TryGetValue(id, out var cached)) return cached;
            string mp = MatDir + id + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, mp); }
            m.shader = lit;
            m.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.94f, 1f));
            m.SetFloat("_Smoothness", 0.25f);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", col * inten);
            EditorUtility.SetDirty(m);
            matCache[id] = m;
            return m;
        };

        var old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RootName);

        int removedOld = 0;
        var finish = GameObject.Find("부산역 역사 내부 · 마감");
        if (finish != null)
            foreach (Transform lvl in finish.transform)
            {
                var t = lvl.Find("실내조명");
                if (t != null) { removedOld += t.childCount; UnityEngine.Object.DestroyImmediate(t.gameObject); }
            }

        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var byLevel = new Dictionary<string, Transform>();
        var counts = new Dictionary<string, int>();
        int made = 0, lights = 0, shadowed = 0;
        double emissiveArea = 0;

        foreach (JObject f in (JArray)plan["fixtures"])
        {
            string lid = (string)f["level"];
            string kind = (string)f["kind"];
            if (!byLevel.TryGetValue(lid, out var parent))
            {
                var g = new GameObject(lid);
                g.transform.SetParent(root.transform, false);
                parent = g.transform;
                byLevel[lid] = parent;
            }

            var ec = (JArray)f["emissiveColor"];
            var col = new Color(F(ec[0], 1f), F(ec[1], 1f), F(ec[2], 1f));
            float inten = F(f["emissiveIntensity"], 3f);
            var mat = mkMat("조명기구_" + kind, col, inten);

            var go = new GameObject((string)f["name"]);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(F(f["pos"][0]), F(f["pos"][1]), F(f["pos"][2]));
            go.transform.rotation = Quaternion.Euler(0f, F(f["rotY"]), 0f);
            float sx = F(f["size"][0], 1f), sy = F(f["size"][1], 0.06f), sz = F(f["size"][2], 0.3f);
            go.transform.localScale = new Vector3(sx, sy, sz);
            emissiveArea += sx * sz;

            go.AddComponent<MeshFilter>().sharedMesh = cube;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            made++;
            counts[kind] = counts.TryGetValue(kind, out var c) ? c + 1 : 1;

            var lj = f["light"];
            if (lj != null && lj.Type == JTokenType.Object)
            {
                var lo = (JObject)lj;
                var lgo = new GameObject("광원");
                lgo.transform.SetParent(parent, false);
                float ly = lo["y"] != null ? F(lo["y"]) : F(f["pos"][1]) - 0.4f;
                lgo.transform.position = new Vector3(F(f["pos"][0]), ly, F(f["pos"][2]));
                lgo.transform.rotation = Quaternion.Euler(90f, F(f["rotY"]), 0f);
                var l = lgo.AddComponent<Light>();
                string lt = (string)lo["type"] ?? "Point";
                l.type = lt == "Spot" ? LightType.Spot : LightType.Point;
                var lc = (JArray)lo["color"];
                l.color = lc != null ? new Color(F(lc[0], 1f), F(lc[1], 1f), F(lc[2], 1f)) : Color.white;
                l.intensity = F(lo["intensity"], 3f);
                l.range = F(lo["range"], 10f);
                if (l.type == LightType.Spot) l.spotAngle = Mathf.Clamp(F(lo["angle"], 60f), 1f, 179f);
                string sh = (string)lo["shadows"] ?? "None";
                l.shadows = sh == "Soft" ? LightShadows.Soft : (sh == "Hard" ? LightShadows.Hard : LightShadows.None);
                if (l.shadows != LightShadows.None) shadowed++;
                lights++;
            }
        }

        var amb = (JObject)plan["ambient"];
        if (amb != null)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Col(amb["skyColor"]);
            RenderSettings.ambientEquatorColor = Col(amb["equatorColor"]);
            RenderSettings.ambientGroundColor = Col(amb["groundColor"]);
            RenderSettings.ambientIntensity = F(amb["intensityMultiplier"], 1f);
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        var sb = new System.Text.StringBuilder();
        sb.Append("fixtures=").Append(made).Append(" lights=").Append(lights)
          .Append(" shadowed=").Append(shadowed).Append(" emissiveAreaM2=").Append(emissiveArea.ToString("F0"))
          .Append(" removedOld=").Append(removedOld).Append("\n");
        foreach (var kv in counts) sb.Append("  ").Append(kv.Key).Append(" x").Append(kv.Value).Append("\n");
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/lighting-build.txt"), sb.ToString());
        Debug.Log("LIGHTING_PLAN " + sb);
    }

    static Color Col(JToken t)
    {
        if (t == null) return Color.grey;
        var a = (JArray)t;
        return new Color(F(a[0], 0.5f), F(a[1], 0.5f), F(a[2], 0.5f));
    }
}
