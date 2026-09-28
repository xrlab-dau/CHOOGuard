// 껍데기에서 진짜 평면도를 뽑는다.
//
// 스케치업 계열 원본은 벽이 두께 없는 한 겹 면이라, 위에서 내려다본 얇은 절단면 렌더에는
// 거의 찍히지 않는다(2026-09-22 실측). 그래서 렌더가 아니라 삼각형–수평면 교선을 직접 계산한다.
// 교선 집합이 그 높이의 벽 선도(line drawing)다.
//
// args: [outPrefix, planeY, minX, maxX, minZ, maxZ, pixelsPerMetre, rootName]
// 산출: <outPrefix>.json (선분 목록 + 통계), <outPrefix>.png (선도 래스터)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class ShellFootprint
{
    public static void Main(string[] args)
    {
        string prefix = Arg(args, 0, ".planning/2026-09-22-station-interior-build/footprint-y2");
        float planeY = F(Arg(args, 1, "2.0"));
        float minX = F(Arg(args, 2, "-70")), maxX = F(Arg(args, 3, "20"));
        float minZ = F(Arg(args, 4, "-95")), maxZ = F(Arg(args, 5, "55"));
        float ppm = F(Arg(args, 6, "6"));
        string rootName = Arg(args, 7, "MainShell");

        Transform root = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == rootName) { root = t; break; }
        if (root == null) throw new InvalidOperationException("루트 없음: " + rootName);

        var segs = new List<Vector4>();   // x1,z1,x2,z2
        long triTotal = 0, triCross = 0;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var l2w = mf.transform.localToWorldMatrix;
            var w = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) w[i] = l2w.MultiplyPoint3x4(verts[i]);

            for (int i = 0; i < tris.Length; i += 3)
            {
                triTotal++;
                var a = w[tris[i]]; var b = w[tris[i + 1]]; var c = w[tris[i + 2]];
                float lo = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                float hi = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                if (planeY < lo || planeY > hi) continue;
                // 수평 삼각형은 교선이 아니라 면이므로 버린다.
                var nrm = Vector3.Cross(b - a, c - a);
                if (nrm.sqrMagnitude < 1e-12f) continue;
                if (Mathf.Abs(Vector3.Normalize(nrm).y) > 0.94f) continue;

                var pts = new List<Vector3>(2);
                Cross(a, b, planeY, pts); Cross(b, c, planeY, pts); Cross(c, a, planeY, pts);
                if (pts.Count < 2) continue;
                var p = pts[0]; var q = pts[pts.Count - 1];
                if ((p - q).sqrMagnitude < 1e-6f) continue;
                if (p.x < minX && q.x < minX) continue;
                if (p.x > maxX && q.x > maxX) continue;
                if (p.z < minZ && q.z < minZ) continue;
                if (p.z > maxZ && q.z > maxZ) continue;
                segs.Add(new Vector4(p.x, p.z, q.x, q.z));
                triCross++;
            }
        }

        int wpx = Mathf.Clamp(Mathf.RoundToInt((maxX - minX) * ppm), 32, 4096);
        int hpx = Mathf.Clamp(Mathf.RoundToInt((maxZ - minZ) * ppm), 32, 4096);
        var tex = new Texture2D(wpx, hpx, TextureFormat.RGB24, false);
        var px = new Color32[wpx * hpx];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(10, 10, 14, 255);
        var ink = new Color32(235, 235, 245, 255);
        foreach (var s in segs)
            Line(px, wpx, hpx,
                (s.x - minX) * ppm, (maxZ - s.y) * ppm,
                (s.z - minX) * ppm, (maxZ - s.w) * ppm, ink);
        tex.SetPixels32(px); tex.Apply(false, false);
        string pngPath = Path.GetFullPath(prefix + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.shell-footprint.v1\",\n");
        sb.Append("  \"method\": \"삼각형–수평면 교선. 수평면(|n.y|>0.94)은 제외.\",\n");
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"planeY\": {0}, \"minX\": {1}, \"maxX\": {2}, \"minZ\": {3}, \"maxZ\": {4}, \"pixelsPerMetre\": {5},\n",
            N(planeY), N(minX), N(maxX), N(minZ), N(maxZ), N(ppm));
        sb.AppendFormat("  \"trianglesScanned\": {0}, \"segments\": {1},\n", triTotal, segs.Count);
        sb.Append("  \"png\": \"" + Path.GetFileName(pngPath) + "\",\n  \"seg\": [");
        for (int i = 0; i < segs.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var s = segs[i];
            sb.AppendFormat(CultureInfo.InvariantCulture, "[{0},{1},{2},{3}]", N(s.x), N(s.y), N(s.z), N(s.w));
        }
        sb.Append("]\n}\n");
        File.WriteAllText(Path.GetFullPath(prefix + ".json"), sb.ToString(), new UTF8Encoding(false));
        Debug.Log("FOOTPRINT y=" + planeY + " segs=" + segs.Count + " -> " + prefix + ".json");
    }

    static void Cross(Vector3 a, Vector3 b, float y, List<Vector3> outPts)
    {
        if ((a.y - y) * (b.y - y) > 0f) return;
        float d = b.y - a.y;
        if (Mathf.Abs(d) < 1e-7f) return;
        float t = (y - a.y) / d;
        if (t < 0f || t > 1f) return;
        outPts.Add(Vector3.Lerp(a, b, t));
    }

    static void Line(Color32[] px, int w, int h, float x0, float y0, float x1, float y1, Color32 col)
    {
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : (float)i / steps;
            int xi = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int yi = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            if (xi < 0 || yi < 0 || xi >= w || yi >= h) continue;
            px[(h - 1 - yi) * w + xi] = col;   // Unity 텍스처 원점은 하단이다
        }
    }

    static string N(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
    static string Arg(string[] a, int i, string d) => a != null && a.Length > i && !string.IsNullOrEmpty(a[i]) ? a[i] : d;
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
