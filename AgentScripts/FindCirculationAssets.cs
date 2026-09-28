// 씬과 프로젝트 자산에서 이미 존재하는 수직동선 오브젝트를 찾는다. 새로 만들기 전에 반드시 먼저 돈다.
// args: [outJson]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class FindCirculationAssets
{
    static readonly string[] Keys =
    {
        "에스컬레이터", "escalator", "esc_", "무빙워크", "movingwalk",
        "계단", "stair", "step", "층계",
        "승강기", "엘리베이터", "elevator", "lift", "ev_",
        "램프", "ramp"
    };

    public static void Main(string[] args)
    {
        string outJson = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/circulation-assets.json";

        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.circulation-assets.v1\",\n  \"sceneObjects\": [\n");

        bool first = true;
        int sceneHits = 0;
        var mine = GameObject.Find("부산역 역사 내부 · 마감");
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = t.name.ToLowerInvariant();
            bool hit = false;
            foreach (var k in Keys) if (n.Contains(k.ToLowerInvariant())) { hit = true; break; }
            if (!hit) continue;
            bool isMine = mine != null && t.IsChildOf(mine.transform);
            var rends = t.GetComponentsInChildren<MeshRenderer>(true);
            long tris = 0;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            var b = new Bounds(t.position, Vector3.zero);
            bool has = false;
            foreach (var r in rends) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            if (!first) sb.Append(",\n");
            first = false;
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "    {{\"name\":\"{0}\",\"path\":\"{1}\",\"mine\":{2},\"renderers\":{3},\"tris\":{4},\"pos\":[{5},{6},{7}],\"size\":[{8},{9},{10}]}}",
                Esc(t.name), Esc(Path(t)), isMine ? "true" : "false", rends.Length, tris,
                N(t.position.x), N(t.position.y), N(t.position.z),
                N(b.size.x), N(b.size.y), N(b.size.z));
            sceneHits++;
        }
        sb.Append("\n  ],\n  \"assets\": [\n");

        first = true;
        int assetHits = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string ln = p.ToLowerInvariant();
            bool hit = false;
            foreach (var k in Keys) if (ln.Contains(k.ToLowerInvariant())) { hit = true; break; }
            if (!hit) continue;
            if (!first) sb.Append(",\n");
            first = false;
            sb.AppendFormat("    {{\"path\":\"{0}\"}}", Esc(p));
            assetHits++;
        }
        sb.Append("\n  ],\n");

        // 원본 모델 안의 계단/에스컬레이터 재질을 쓰는 서브메시 위치
        sb.Append("  \"officialMaterialHits\": [\n");
        first = true;
        int matHits = 0;
        foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var rend = mf.GetComponent<Renderer>();
            if (rend == null || mf.sharedMesh == null) continue;
            if (mine != null && mf.transform.IsChildOf(mine.transform)) continue;
            var mats = rend.sharedMaterials;
            for (int s = 0; s < mats.Length && s < mf.sharedMesh.subMeshCount; s++)
            {
                if (mats[s] == null) continue;
                string mn = mats[s].name;
                if (mn.IndexOf("계단", StringComparison.Ordinal) < 0 &&
                    mn.IndexOf("에스", StringComparison.Ordinal) < 0) continue;
                if (!first) sb.Append(",\n");
                first = false;
                sb.AppendFormat("    {{\"mesh\":\"{0}\",\"sub\":{1},\"mat\":\"{2}\"}}", Esc(mf.name), s, Esc(mn));
                matHits++;
            }
        }
        sb.Append("\n  ],\n");
        sb.AppendFormat("  \"counts\": {{\"scene\": {0}, \"assets\": {1}, \"officialMaterial\": {2}}}\n}}\n",
            sceneHits, assetHits, matHits);

        string full = System.IO.Path.GetFullPath(outJson);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("CIRCULATION scene=" + sceneHits + " assets=" + assetHits + " mat=" + matHits + " -> " + full);
    }

    static string Path(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
    static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    static string N(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
