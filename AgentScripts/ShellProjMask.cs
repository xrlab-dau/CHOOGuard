// 지정 높이대·법선 조건의 삼각형을 XZ 평면에 투영해 채운 마스크를 만든다.
// 지붕 윤곽(=건물 외곽), 바닥판 윤곽 등 '면' 기반 형상을 뽑을 때 쓴다.
// args: [outPng, yLo, yHi, normalMode(up|down|flat|vert|any), minX,maxX,minZ,maxZ, ppm, rootName]

using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class ShellProjMask
{
    public static void Main(string[] args)
    {
        string outPng = Arg(args, 0, ".planning/2026-09-22-station-interior-build/captures/mask.png");
        float yLo = F(Arg(args, 1, "11")), yHi = F(Arg(args, 2, "33"));
        string mode = Arg(args, 3, "flat");
        float minX = F(Arg(args, 4, "-70")), maxX = F(Arg(args, 5, "25"));
        float minZ = F(Arg(args, 6, "-95")), maxZ = F(Arg(args, 7, "60"));
        float ppm = F(Arg(args, 8, "4"));
        string rootName = Arg(args, 9, "MainShell");

        Transform root = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == rootName) { root = t; break; }
        if (root == null) throw new InvalidOperationException("루트 없음: " + rootName);

        int W = Mathf.Clamp(Mathf.RoundToInt((maxX - minX) * ppm), 32, 4096);
        int H = Mathf.Clamp(Mathf.RoundToInt((maxZ - minZ) * ppm), 32, 4096);
        var px = new Color32[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 255);
        long used = 0;

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
                float cy = (a.y + b.y + c.y) / 3f;
                if (cy < yLo || cy > yHi) continue;
                var n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                float ny = Vector3.Normalize(n).y;
                bool keep = mode == "any"
                    || (mode == "up" && ny > 0.8f)
                    || (mode == "down" && ny < -0.8f)
                    || (mode == "flat" && Mathf.Abs(ny) > 0.8f)
                    || (mode == "vert" && Mathf.Abs(ny) < 0.4f);
                if (!keep) continue;
                used++;
                FillTri(px, W, H, minX, maxZ, ppm, a, b, c);
            }
        }

        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.SetPixels32(px); tex.Apply(false, false);
        string full = Path.GetFullPath(outPng);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllBytes(full, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log("PROJMASK " + mode + " y[" + yLo + "," + yHi + "] tris=" + used + " -> " + full);
    }

    static void FillTri(Color32[] px, int W, int H, float minX, float maxZ, float ppm, Vector3 a, Vector3 b, Vector3 c)
    {
        float ax = (a.x - minX) * ppm, ay = (maxZ - a.z) * ppm;
        float bx = (b.x - minX) * ppm, by = (maxZ - b.z) * ppm;
        float cx = (c.x - minX) * ppm, cy2 = (maxZ - c.z) * ppm;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, Mathf.Min(bx, cx))));
        int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(ax, Mathf.Max(bx, cx))));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, Mathf.Min(by, cy2))));
        int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(ay, Mathf.Max(by, cy2))));
        float d = (by - cy2) * (ax - cx) + (cx - bx) * (ay - cy2);
        if (Mathf.Abs(d) < 1e-9f) return;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            float pxc = x + 0.5f, pyc = y + 0.5f;
            float l1 = ((by - cy2) * (pxc - cx) + (cx - bx) * (pyc - cy2)) / d;
            float l2 = ((cy2 - ay) * (pxc - cx) + (ax - cx) * (pyc - cy2)) / d;
            float l3 = 1f - l1 - l2;
            if (l1 < -0.001f || l2 < -0.001f || l3 < -0.001f) continue;
            px[(H - 1 - y) * W + x] = new Color32(255, 255, 255, 255);   // Unity 텍스처 원점은 하단이다
        }
    }

    static string Arg(string[] a, int i, string d) => a != null && a.Length > i && !string.IsNullOrEmpty(a[i]) ? a[i] : d;
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
