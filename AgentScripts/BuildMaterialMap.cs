// MaterialLane 의 chooguard.material-map.v1 을 씬에 적용한다.
//  1) materials[] 로 .mat 자산을 생성/갱신 (BaseMap/NormalMap/Metallic/Smoothness/Tiling)
//  2) assign[] 규칙을 위->아래로 평가해 마지막 매치를 적용
//  3) 실사 기준선 알베도로 보정 — 부산역 실내 평균 Y=0.429, 채도 0.181 (ReferenceHarvest)
//     흰색 마감을 그대로 두면 휘도 히스토그램이 통째로 어긋난다.
//
// args: [materialMapJson, textureDir, albedoTarget]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class BuildMaterialMap
{
    const string MatDir = "Assets/ChooGuard/Art/StationInterior/Materials/Lane/";

    static readonly string[] Roots = {
        "부산역 역사 내부", "부산역 역사 내부 · 마감",
        "부산역 역사 내부 · 수직동선", "부산역 역사 내부 · 재사용 자산",
        "부산역 역사 내부 · 사인"
    };

    static float F(JToken t, float d) => t == null || t.Type == JTokenType.Null ? d : (float)t;
    static string S(JToken t) => t == null || t.Type == JTokenType.Null ? null : (string)t;

    public static void Main(string[] args)
    {
        string mapPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/generated/materials/material-map.json";
        string texDir = args != null && args.Length > 1 && !string.IsNullOrEmpty(args[1])
            ? args[1] : "Assets/ChooGuard/Art/StationInterior/Textures/Lane/";
        if (!texDir.EndsWith("/")) texDir += "/";
        // 목표 평균 알베도(선형). 0 이면 보정 안 함.
        float albedoTarget = args != null && args.Length > 2 && !string.IsNullOrEmpty(args[2])
            ? float.Parse(args[2], CultureInfo.InvariantCulture) : 0.21f;

        // 텍스처 임포트 + 노멀맵 타입 지정 (5초 메인스레드 제한을 피해 run_script 안에서 처리)
        string texAssetDir = texDir.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(texAssetDir))
        {
            AssetDatabase.ImportAsset(texAssetDir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texAssetDir }))
            {
                string ap = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(ap) as TextureImporter;
                if (ti == null) continue;
                bool dirty = false;
                bool isNormal = ap.EndsWith("_n.png", StringComparison.Ordinal);
                if (isNormal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
                if (!isNormal && !ti.isReadable) { ti.isReadable = true; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
        }

        var map = JObject.Parse(File.ReadAllText(Path.GetFullPath(mapPath)));
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Directory.CreateDirectory(Path.GetFullPath(MatDir));

        // ---- 1) 머티리얼 자산 ----
        var built = new Dictionary<string, Material>();
        int created = 0, texMissing = 0;
        var missingSample = new List<string>();

        foreach (JObject m in (JArray)map["materials"])
        {
            string id = S(m["id"]);
            if (string.IsNullOrEmpty(id)) continue;
            string mp = MatDir + id + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, mp); created++; }
            mat.shader = lit;

            var bt = Resolve(S(m["baseMap"]), texDir);
            var nt = Resolve(S(m["normalMap"]), texDir);
            if (bt != null) mat.SetTexture("_BaseMap", bt);
            else { texMissing++; if (missingSample.Count < 10) missingSample.Add(id + " <- " + S(m["baseMap"])); }
            if (nt != null) { mat.SetTexture("_BumpMap", nt); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }

            mat.SetFloat("_Metallic", F(m["metallic"], 0f));
            mat.SetFloat("_Smoothness", F(m["smoothness"], 0.3f));

            float tile = F(m["tilingMetres"], 0f);
            if (tile > 0.01f)
            {
                float k = 1f / tile;
                mat.SetTextureScale("_BaseMap", new Vector2(k, k));
                if (nt != null) mat.SetTextureScale("_BumpMap", new Vector2(k, k));
            }

            // 알베도 보정: 텍스처 평균 밝기를 목표대로 끌어내린다
            if (albedoTarget > 0f && bt is Texture2D t2)
            {
                float mean = MeanLuma(t2);
                // 너무 흰 면만 내린다. 이미 어두운 재질은 건드리지 않는다.
                if (mean > albedoTarget + 0.02f)
                {
                    float k = Mathf.Clamp(albedoTarget / mean, 0.55f, 1.0f);
                    mat.SetColor("_BaseColor", new Color(k, k, k, 1f));
                }
                else
                {
                    mat.SetColor("_BaseColor", Color.white);
                }
            }
            EditorUtility.SetDirty(mat);
            built[id] = mat;
        }

        // ---- 2) 배정 ----
        var rules = new List<(JObject match, Material mat, string id)>();
        foreach (JObject a in (JArray)map["assign"])
        {
            string id = S(a["material"]);
            if (id == null || !built.TryGetValue(id, out var mm)) continue;
            rules.Add(((JObject)a["match"], mm, id));
        }

        // fitout 박스의 mat 키 조회용
        var matKeyByName = new Dictionary<string, string>();
        var fitPath = Path.GetFullPath(".planning/2026-09-22-station-interior-build/station-interior-fitout.json");
        if (File.Exists(fitPath))
        {
            var fit = JObject.Parse(File.ReadAllText(fitPath));
            foreach (JObject b in (JArray)fit["boxes"])
            {
                string nm = S(b["name"]);
                string mk = S(b["mat"]);
                if (nm != null && mk != null) matKeyByName[nm] = mk;
            }
        }

        var applied = new Dictionary<string, int>();
        int touched = 0, unmatched = 0;
        foreach (var rootName in Roots)
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // 사인 표시면과 발광판은 덮지 않는다
                string nm = mr.gameObject.name;
                if (nm.StartsWith("면 ", StringComparison.Ordinal)) continue;
                if (rootName == "부산역 역사 내부 · 사인" && !nm.StartsWith("케이스", StringComparison.Ordinal)) continue;

                string path = PathOf(mr.transform);
                string level = LevelOf(path);
                string group = GroupOf(path);
                matKeyByName.TryGetValue(nm, out var matKey);

                Material pick = null; string pickId = null;
                foreach (var (match, mm, id) in rules)
                {
                    if (!Matches(match, path, nm, group, level, matKey)) continue;
                    pick = mm; pickId = id;      // 마지막 매치가 이긴다
                }
                if (pick == null) { unmatched++; continue; }

                var arr = mr.sharedMaterials;
                var next = new Material[arr.Length == 0 ? 1 : arr.Length];
                for (int i = 0; i < next.Length; i++) next[i] = pick;
                mr.sharedMaterials = next;
                applied[pickId] = applied.TryGetValue(pickId, out var c) ? c + 1 : 1;
                touched++;
            }
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        var sb = new StringBuilder();
        sb.Append("materialsBuilt=").Append(built.Count).Append(" created=").Append(created)
          .Append(" texMissing=").Append(texMissing).Append(" assignedRenderers=").Append(touched)
          .Append(" unmatched=").Append(unmatched).Append(" albedoTarget=").Append(albedoTarget).Append("\n");
        foreach (var kv in applied.OrderByDescending(k => k.Value).Take(28))
            sb.Append("  ").Append(kv.Key).Append(" x").Append(kv.Value).Append("\n");
        foreach (var s in missingSample) sb.Append("  ? ").Append(s).Append("\n");
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/material-map-build.txt"), sb.ToString());
        Debug.Log("MATERIAL_MAP " + sb);
    }

    static bool Matches(JObject m, string path, string name, string group, string level, string matKey)
    {
        if (m == null) return false;
        var g = S(m["group"]); if (g != null && group.IndexOf(g, StringComparison.Ordinal) < 0) return false;
        var lv = S(m["level"]); if (lv != null && level != lv) return false;
        var mk = S(m["mat"]); if (mk != null && matKey != mk) return false;
        var nc = S(m["nameContains"]); if (nc != null && name.IndexOf(nc, StringComparison.Ordinal) < 0) return false;
        var ne = S(m["nameEndsWith"]); if (ne != null && !name.EndsWith(ne, StringComparison.Ordinal)) return false;
        var h = S(m["hierarchy"]);
        if (h != null)
        {
            var parts = h.Split('*');
            int idx = 0;
            foreach (var p in parts)
            {
                if (p.Length == 0) continue;
                int f = path.IndexOf(p, idx, StringComparison.Ordinal);
                if (f < 0) return false;
                idx = f + p.Length;
            }
        }
        return true;
    }

    static Texture Resolve(string p, string texDir)
    {
        if (string.IsNullOrEmpty(p)) return null;
        var t = AssetDatabase.LoadAssetAtPath<Texture>(p);
        if (t != null) return t;
        string fn = Path.GetFileNameWithoutExtension(p);
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            t = AssetDatabase.LoadAssetAtPath<Texture>(texDir + fn + ext);
            if (t != null) return t;
        }
        return null;
    }

    static float MeanLuma(Texture2D t)
    {
        try
        {
            var rt = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(t, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tmp = new Texture2D(16, 16, TextureFormat.RGB24, false);
            tmp.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            float sum = 0f;
            foreach (var c in tmp.GetPixels()) sum += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            UnityEngine.Object.DestroyImmediate(tmp);
            return sum / 256f;
        }
        catch { return 0f; }
    }

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        var cur = t.parent;
        while (cur != null) { sb.Insert(0, cur.name + "/"); cur = cur.parent; }
        return sb.ToString();
    }

    static string LevelOf(string path)
    {
        foreach (var lv in new[] { "1F", "2F", "3F", "4F", "B1" })
            if (path.IndexOf("/" + lv, StringComparison.Ordinal) >= 0) return lv;
        return "";
    }

    static string GroupOf(string path)
    {
        var parts = path.Split('/');
        return parts.Length >= 2 ? string.Join("/", parts, Math.Max(0, parts.Length - 3), Math.Min(3, parts.Length)) : path;
    }
}
