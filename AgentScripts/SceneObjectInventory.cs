// 씬 전체의 재사용 가능한 오브젝트를 전수 조사한다. 새로 만들기 전 필수 절차.
// 이름이 같은 것끼리 묶고, 치수대로 분류해 '이게 무엇일 수 있는가'를 크기로 추정한다.
// args: [outJson, maxSizeMetres]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class SceneObjectInventory
{
    public static void Main(string[] args)
    {
        string outJson = Arg(args, 0, ".planning/2026-09-22-station-interior-build/scene-inventory.json");
        float maxSize = F(Arg(args, 1, "12"));

        var byName = new Dictionary<string, List<MeshRenderer>>();
        foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var s = mr.bounds.size;
            if (s.x > maxSize || s.y > maxSize || s.z > maxSize) continue;
            if (s.x < 0.05f && s.y < 0.05f && s.z < 0.05f) continue;
            string key = Normalise(mr.gameObject.name);
            if (!byName.TryGetValue(key, out var l)) { l = new List<MeshRenderer>(); byName[key] = l; }
            l.Add(mr);
        }

        var rows = new List<string>();
        foreach (var kv in byName)
        {
            var l = kv.Value;
            var s = l[0].bounds.size;
            long tris = 0;
            var mf0 = l[0].GetComponent<MeshFilter>();
            if (mf0 != null && mf0.sharedMesh != null) tris = mf0.sharedMesh.triangles.Length / 3;
            string owner = Root(l[0].transform);
            rows.Add(string.Format(CultureInfo.InvariantCulture,
                "    {{\"name\":\"{0}\",\"count\":{1},\"tris\":{2},\"size\":[{3},{4},{5}],\"owner\":\"{6}\",\"examplePath\":\"{7}\"}}",
                Esc(kv.Key), l.Count, tris, N(s.x), N(s.y), N(s.z), Esc(owner), Esc(PathOf(l[0].transform))));
        }

        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.scene-inventory.v1\",\n");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"maxSizeMetres\": {0},\n  \"distinctNames\": {1},\n", N(maxSize), rows.Count);
        sb.Append("  \"objects\": [\n");
        sb.Append(string.Join(",\n", rows));
        sb.Append("\n  ]\n}\n");

        string full = Path.GetFullPath(outJson);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("INVENTORY distinct=" + rows.Count + " -> " + full);
    }

    static string Normalise(string n)
    {
        // 말미의 _000 / (1) / 001 같은 인스턴스 번호를 떼어 같은 물건으로 묶는다
        n = n.Trim();
        int i = n.Length;
        while (i > 0 && (char.IsDigit(n[i - 1]) || n[i - 1] == '_' || n[i - 1] == ' ' ||
                         n[i - 1] == '(' || n[i - 1] == ')' || n[i - 1] == '.')) i--;
        return i > 0 ? n.Substring(0, i) : n;
    }

    static string Root(Transform t)
    {
        var top = t; while (top.parent != null) top = top.parent;
        var cur = t; while (cur.parent != null && cur.parent != top) cur = cur.parent;
        return top.name + "/" + cur.name;
    }

    static string PathOf(Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }

    static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    static string N(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
    static string Arg(string[] a, int i, string d) => a != null && a.Length > i && !string.IsNullOrEmpty(a[i]) ? a[i] : d;
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
