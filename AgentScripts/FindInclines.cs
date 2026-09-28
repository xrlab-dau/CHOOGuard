// 씬 안의 '경사면'을 기하로 검출한다. 이름·재질에 의존하지 않는다.
// 에스컬레이터/계단 경사로는 법선 기울기 23°~63° 구간의 연속 면이다.
// 검출한 덩어리의 하단·상단 높이와 평면 길이를 돌려주므로, 이미 있는 동선을 그대로 쓸 수 있다.
//
// args: [outJson, minSlopeDeg, maxSlopeDeg, minClusterArea, excludeRootName]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class FindInclines
{
    class Cluster
    {
        public double Area;
        public Vector3 Min = new Vector3(1e9f, 1e9f, 1e9f);
        public Vector3 Max = new Vector3(-1e9f, -1e9f, -1e9f);
        public Vector3 NormalSum;
        public int Tris;
        public string Owner;
    }

    public static void Main(string[] args)
    {
        string outJson = Arg(args, 0, ".planning/2026-09-22-station-interior-build/inclines.json");
        float minS = F(Arg(args, 1, "23")), maxS = F(Arg(args, 2, "63"));
        float minArea = F(Arg(args, 3, "6"));
        string exclude = Arg(args, 4, "부산역 역사 내부 · 마감");

        float loDot = Mathf.Cos(maxS * Mathf.Deg2Rad);   // 급경사일수록 ny 작다
        float hiDot = Mathf.Cos(minS * Mathf.Deg2Rad);

        Transform ex = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == exclude) { ex = t; break; }

        // 1.5m 복셀 해시로 인접 삼각형을 묶는다.
        const float Vox = 1.5f;
        var cells = new Dictionary<long, int>();
        var parent = new List<int>();
        var data = new List<Cluster>();

        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return;
            parent[b] = a;
            data[a].Area += data[b].Area; data[a].Tris += data[b].Tris;
            data[a].Min = Vector3.Min(data[a].Min, data[b].Min);
            data[a].Max = Vector3.Max(data[a].Max, data[b].Max);
            data[a].NormalSum += data[b].NormalSum;
            data[b].Area = 0;
        }

        long Key(int x, int y, int z) => ((long)(x + 4096) << 40) | ((long)(y + 4096) << 20) | (long)(z + 4096);

        foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var mesh = mf.sharedMesh; if (mesh == null) continue;
            if (ex != null && mf.transform.IsChildOf(ex)) continue;
            var verts = mesh.vertices; var tris = mesh.triangles;
            var l2w = mf.transform.localToWorldMatrix;
            string owner = RootName(mf.transform);
            var w = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) w[i] = l2w.MultiplyPoint3x4(verts[i]);

            for (int i = 0; i < tris.Length; i += 3)
            {
                var a = w[tris[i]]; var b = w[tris[i + 1]]; var c = w[tris[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                float area = n.magnitude * 0.5f;
                if (area < 0.05f) continue;
                float ny = Mathf.Abs(n.normalized.y);
                if (ny < loDot || ny > hiDot) continue;
                var cen = (a + b + c) / 3f;
                int vx = Mathf.FloorToInt(cen.x / Vox), vy = Mathf.FloorToInt(cen.y / Vox), vz = Mathf.FloorToInt(cen.z / Vox);

                int idx = -1;
                for (int dx = -1; dx <= 1 && idx < 0; dx++)
                for (int dy = -1; dy <= 1 && idx < 0; dy++)
                for (int dz = -1; dz <= 1 && idx < 0; dz++)
                    if (cells.TryGetValue(Key(vx + dx, vy + dy, vz + dz), out int found)) idx = Find(found);

                if (idx < 0)
                {
                    idx = data.Count;
                    parent.Add(idx);
                    data.Add(new Cluster { Owner = owner });
                }
                var cl = data[Find(idx)];
                cl.Area += area; cl.Tris++;
                cl.Min = Vector3.Min(cl.Min, Vector3.Min(a, Vector3.Min(b, c)));
                cl.Max = Vector3.Max(cl.Max, Vector3.Max(a, Vector3.Max(b, c)));
                cl.NormalSum += n.normalized * area;

                long k = Key(vx, vy, vz);
                if (cells.TryGetValue(k, out int e)) Union(Find(e), Find(idx));
                else cells[k] = Find(idx);
            }
        }

        var list = new List<Cluster>();
        for (int i = 0; i < data.Count; i++)
            if (Find(i) == i && data[i].Area >= minArea) list.Add(data[i]);
        list.Sort((p, q) => q.Area.CompareTo(p.Area));

        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.inclines.v1\",\n");
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"slopeRangeDeg\": [{0}, {1}], \"minClusterArea\": {2}, \"excluded\": \"{3}\",\n",
            N(minS), N(maxS), N(minArea), exclude);
        sb.Append("  \"inclines\": [\n");
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            var n = c.NormalSum.normalized;
            float slope = Mathf.Acos(Mathf.Abs(n.y)) * Mathf.Rad2Deg;
            float rise = c.Max.y - c.Min.y;
            float runX = c.Max.x - c.Min.x, runZ = c.Max.z - c.Min.z;
            float run = Mathf.Sqrt(runX * runX + runZ * runZ);
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "    {{\"owner\":\"{0}\",\"area\":{1},\"tris\":{2},\"slopeDeg\":{3},\"rise\":{4},\"runXZ\":{5}," +
                "\"min\":[{6},{7},{8}],\"max\":[{9},{10},{11}],\"centre\":[{12},{13},{14}]}}{15}\n",
                Esc(c.Owner), N((float)c.Area), c.Tris, N(slope), N(rise), N(run),
                N(c.Min.x), N(c.Min.y), N(c.Min.z), N(c.Max.x), N(c.Max.y), N(c.Max.z),
                N((c.Min.x + c.Max.x) / 2), N((c.Min.y + c.Max.y) / 2), N((c.Min.z + c.Max.z) / 2),
                i == list.Count - 1 ? "" : ",");
        }
        sb.AppendFormat("  ],\n  \"count\": {0}\n}}\n", list.Count);

        string full = Path.GetFullPath(outJson);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("INCLINES " + list.Count + " -> " + full);
    }

    static string RootName(Transform t)
    {
        var top = t;
        while (top.parent != null) top = top.parent;
        // 루트 바로 아래 한 단계까지 표기
        var cur = t;
        string second = t.name;
        while (cur.parent != null && cur.parent != top) cur = cur.parent;
        second = cur.name;
        return top.name + "/" + second;
    }

    static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    static string N(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
    static string Arg(string[] a, int i, string d) => a != null && a.Length > i && !string.IsNullOrEmpty(a[i]) ? a[i] : d;
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
