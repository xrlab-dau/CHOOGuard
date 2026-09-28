// 지정 XZ 박스 안에서 수평면(바닥/천장 후보)의 면적을 높이 구간별로 집계한다.
// 층 레벨을 가정하지 않고 원본 형상에서 읽어내기 위한 계측이다.
// args: [outJson, minX, maxX, minZ, maxZ, binMetres, rootName]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class HeightHistogram
{
    public static void Main(string[] args)
    {
        string outJson = Arg(args, 0, ".planning/2026-09-22-station-interior-build/height-histogram.json");
        float minX = F(Arg(args, 1, "-65")), maxX = F(Arg(args, 2, "-12"));
        float minZ = F(Arg(args, 3, "-95")), maxZ = F(Arg(args, 4, "60"));
        float bin = F(Arg(args, 5, "0.25"));
        string rootName = Arg(args, 6, "MainShell");

        Transform root = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == rootName) { root = t; break; }
        if (root == null) throw new InvalidOperationException("루트 없음: " + rootName);

        float yLo = -5f, yHi = 40f;
        int nb = Mathf.CeilToInt((yHi - yLo) / bin);
        var up = new double[nb];
        var down = new double[nb];
        var vert = new double[nb];

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh; if (mesh == null) continue;
            var verts = mesh.vertices; var tris = mesh.triangles;
            var l2w = mf.transform.localToWorldMatrix;
            var w = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) w[i] = l2w.MultiplyPoint3x4(verts[i]);
            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = w[tris[i]]; var b = w[tris[i + 1]]; var c = w[tris[i + 2]];
                float cxx = (a.x + b.x + c.x) / 3f, czz = (a.z + b.z + c.z) / 3f, cyy = (a.y + b.y + c.y) / 3f;
                if (cxx < minX || cxx > maxX || czz < minZ || czz > maxZ) continue;
                if (cyy < yLo || cyy >= yHi) continue;
                var n = Vector3.Cross(b - a, c - a);
                float area = n.magnitude * 0.5f;
                if (area < 1e-6f) continue;
                float ny = n.normalized.y;
                int bi = Mathf.Clamp((int)((cyy - yLo) / bin), 0, nb - 1);
                if (ny > 0.8f) up[bi] += area;
                else if (ny < -0.8f) down[bi] += area;
                else if (Mathf.Abs(ny) < 0.4f) vert[bi] += area;
            }
        }

        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.height-histogram.v1\",\n");
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"box\": {{\"minX\": {0}, \"maxX\": {1}, \"minZ\": {2}, \"maxZ\": {3}}}, \"bin\": {4}, \"yLo\": {5},\n",
            N(minX), N(maxX), N(minZ), N(maxZ), N(bin), N(yLo));
        sb.Append("  \"bins\": [");
        bool first = true;
        for (int i = 0; i < nb; i++)
        {
            if (up[i] < 1 && down[i] < 1 && vert[i] < 1) continue;
            if (!first) sb.Append(", ");
            first = false;
            sb.AppendFormat(CultureInfo.InvariantCulture, "[{0},{1},{2},{3}]",
                N(yLo + i * bin), up[i].ToString("F0", CultureInfo.InvariantCulture),
                down[i].ToString("F0", CultureInfo.InvariantCulture), vert[i].ToString("F0", CultureInfo.InvariantCulture));
        }
        sb.Append("],\n  \"binFormat\": \"[y, upFacingArea, downFacingArea, verticalArea]\"\n}\n");
        string full = Path.GetFullPath(outJson);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("HEIGHT_HISTOGRAM -> " + full);
    }

    static string N(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
    static string Arg(string[] a, int i, string d) => a != null && a.Length > i && !string.IsNullOrEmpty(a[i]) ? a[i] : d;
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
