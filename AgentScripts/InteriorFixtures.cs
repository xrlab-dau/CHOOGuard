// 대합실 실내 시설물 추출 — 공식 원본이 이미 갖고 있던 것을 꺼낸다.
//
// Jev 007: first_route = mine_the_existing_official_model 0.99
//          (3자 모델 0.00, 사진 복원 0.00, 도면 확보 0.01)
//
// 앞선 측정의 결함: 구역 판독이 바닥 레이의 triangleIndex 만 읽었다. 구조적으로 실내에 떠 있는
// 지오메트리를 볼 수 없는 방법이었다. 카메라 캡처 한 장을 보고 "내부가 비어 있다" 고 단정한 것도
// 같은 오류다 — 시선 방향에 없었을 뿐이다.
//
// 실측: 주 대합실 실내 체적(바닥 위 0.25~3.4m)에 19종 머티리얼, 삼각형 36,600여 개가 있다.
// 바닥도 천장도 벽도 아닌 것들이므로 시설물이다.
//
// 여기서는 그 삼각형을 공간 클러스터링해 개별 덩어리로 가른다. 각 덩어리의 치수·위치가
// 곧 비임의적 배치 근거가 된다. 이름표는 붙이지 않는다 — 근거가 따로 필요하다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class InteriorFixtures
{
    // 주 대합실. 바닥 y≈7.0, 천장 y≈11.1.
    const float XMin = -10f, XMax = 62f, ZMin = -68f, ZMax = -28f;
    const float YLo = 7.22f, YHi = 10.45f;   // 바닥 바로 위 ~ 천장 아래
    const float Voxel = 0.6f;                 // 클러스터 인접 판정 격자
    const int MinTris = 24;                   // 이보다 작은 덩어리는 버린다

    struct Tri { public Vector3 C; public string Mat; public float Area; }

    public static void Main(string[] args)
    {
        string outDir = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build";
        Directory.CreateDirectory(Path.GetFullPath(outDir));

        var root = GameObject.Find("공식 자료 부산역 역사");
        if (root == null) throw new InvalidOperationException("역사 루트를 찾지 못했다");

        var tris = new List<Tri>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh; var r = mf.GetComponent<MeshRenderer>();
            if (m == null || r == null || !m.isReadable) continue;
            var sm = r.sharedMaterials; var vs = m.vertices; var tf = mf.transform;
            for (int s = 0; s < m.subMeshCount && s < sm.Length; s++)
            {
                if (sm[s] == null) continue;
                var idx = m.GetTriangles(s);
                string mat = sm[s].name;
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    var a = tf.TransformPoint(vs[idx[i]]);
                    var b = tf.TransformPoint(vs[idx[i + 1]]);
                    var c = tf.TransformPoint(vs[idx[i + 2]]);
                    var ctr = (a + b + c) / 3f;
                    if (ctr.y < YLo || ctr.y > YHi) continue;
                    if (ctr.x < XMin || ctr.x > XMax || ctr.z < ZMin || ctr.z > ZMax) continue;
                    tris.Add(new Tri { C = ctr, Mat = mat, Area = Vector3.Cross(b - a, c - a).magnitude * 0.5f });
                }
            }
        }

        // 복셀 격자로 묶고 26-이웃 union-find 로 잇는다
        var cellOf = new Dictionary<long, List<int>>();
        Func<Vector3, long> key = p => ((long)Mathf.FloorToInt(p.x / Voxel) + 4096) * 16777216L
                                     + ((long)Mathf.FloorToInt(p.y / Voxel) + 4096) * 4096L
                                     + ((long)Mathf.FloorToInt(p.z / Voxel) + 4096);
        for (int i = 0; i < tris.Count; i++)
        {
            long k = key(tris[i].C);
            if (!cellOf.TryGetValue(k, out var l)) cellOf[k] = l = new List<int>();
            l.Add(i);
        }
        var parent = new int[tris.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        Func<int, int> find = null;
        find = x => { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; };
        Action<int, int> union = (x, y) => { int rx = find(x), ry = find(y); if (rx != ry) parent[ry] = rx; };

        foreach (var kv in cellOf)
        {
            for (int a = 1; a < kv.Value.Count; a++) union(kv.Value[0], kv.Value[a]);
            // 26 이웃
            long k0 = kv.Key;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dy == 0 && dz == 0) continue;
                long nk = k0 + (long)dx * 16777216L + (long)dy * 4096L + dz;
                if (cellOf.TryGetValue(nk, out var nl) && nl.Count > 0) union(kv.Value[0], nl[0]);
            }
        }

        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < tris.Count; i++)
        {
            int r = find(i);
            if (!groups.TryGetValue(r, out var l)) groups[r] = l = new List<int>();
            l.Add(i);
        }

        var clusters = new List<(int n, Bounds b, string mat, float area, int mats)>();
        foreach (var g in groups.Values)
        {
            if (g.Count < MinTris) continue;
            var bb = new Bounds(tris[g[0]].C, Vector3.zero);
            var byMat = new Dictionary<string, int>(); float area = 0;
            foreach (int i in g)
            {
                bb.Encapsulate(tris[i].C); area += tris[i].Area;
                byMat.TryGetValue(tris[i].Mat, out int c); byMat[tris[i].Mat] = c + 1;
            }
            string dom = ""; int dc = 0;
            foreach (var kv in byMat) if (kv.Value > dc) { dc = kv.Value; dom = kv.Key; }
            clusters.Add((g.Count, bb, dom, area, byMat.Count));
        }
        clusters.Sort((a, c) => c.n.CompareTo(a.n));

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.interior-fixtures.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"jevBasis\": \"jev-bottleneck-route-007: mine_the_existing_official_model 0.99\",\n");
        sb.Append("  \"why\": \"구역 판독이 바닥 레이의 triangleIndex 만 읽어 실내에 떠 있는 지오메트리를 구조적으로 볼 수 없었다. 카메라 한 장을 보고 '내부가 비어 있다' 고 단정한 것도 같은 오류다.\",\n");
        sb.Append("  \"method\": \"주 대합실 실내 체적의 삼각형을 무게중심으로 모아 0.6m 복셀 26-이웃 union-find 로 클러스터링한다. 이름표는 붙이지 않는다 — 형상과 치수만 사실이다.\",\n");
        sb.AppendFormat(inv, "  \"volume\": {{\"x\": [{0}, {1}], \"z\": [{2}, {3}], \"y\": [{4}, {5}]}},\n", XMin, XMax, ZMin, ZMax, YLo, YHi);
        sb.AppendFormat(inv, "  \"trianglesInVolume\": {0}, \"clusters\": {1},\n", tris.Count, clusters.Count);
        sb.Append("  \"fixtures\": [\n");
        for (int i = 0; i < clusters.Count && i < 40; i++)
        {
            var c = clusters[i]; var bb = c.b;
            if (i > 0) sb.Append(",\n");
            sb.AppendFormat(inv, "    {{\"id\": \"F{0:00}\", \"tris\": {1}, \"surfaceArea\": {2:F1}, \"materials\": {3}, ",
                i, c.n, c.area, c.mats);
            sb.AppendFormat(inv, "\"centre\": [{0:F1}, {1:F2}, {2:F1}], \"size\": [{3:F2}, {4:F2}, {5:F2}], ",
                bb.center.x, bb.center.y, bb.center.z, bb.size.x, bb.size.y, bb.size.z);
            sb.AppendFormat(inv, "\"floorClearance\": {0:F2}, \"dominantMaterial\": \"{1}\"}}",
                bb.min.y - 7.0f, Esc(c.mat));
        }
        sb.Append("\n  ]\n}\n");
        var p = Path.Combine(outDir, "interior-fixtures.json");
        File.WriteAllText(Path.GetFullPath(p), sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"FIXTURES tris={tris.Count} clusters={clusters.Count} -> {p}");
    }

    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
