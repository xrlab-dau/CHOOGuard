// 부산역 실내 연결 성분 분해 — 설계 가능 면적 확정.
//
// StationFloorPlan 은 스폰(도로)에서 홍수 채우기를 해 y=7.0 역사 바닥 1,992제곱미터 중
// 639제곱미터만 도달 가능하다고 냈다. 평면도를 보니 도달 가능한 초록은 고립된 별동이고,
// 본 대합실(큰 홀 + 긴 복도)은 통째로 도달 불가였다.
//
// 그러나 튜토리얼은 역무원이 역사 안에서 시작한다. 도로에서 걸어올 필요가 없다.
// 그래서 물음을 바꾼다: 씨앗을 고르지 않고 모든 연결 성분을 분해해, 가장 큰 연속
// 실내 공간이 얼마인지 잰다. 그것이 설계 가능 면적이다.
//
// 걸음 규칙은 StationFloorPlan 과 동일(1m 격자, 높이차 0.45m, 캡슐 .28/.28/1.44).
// 다만 8-연결로 넓힌다 — 4-연결은 대각선 계단을 넘지 못해 성분을 잘못 쪼갠다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class StationRooms
{
    const float Cell = 1f, HStep = 0.5f, FloorReach = 1.2f, FlatDot = 0.7072f;
    const float CapsuleRadius = 0.28f, CapsuleFoot = 0.28f, CapsuleHead = 1.44f;
    const float StepUp = 0.45f, SameFloorEps = 0.3f;

    struct Stand { public int Cx, Cz; public float Y; public bool Shell; public string Col; }

    public static void Main(string[] args)
    {
        string outDir = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build";
        Directory.CreateDirectory(Path.GetFullPath(outDir));

        var root = GameObject.Find("공식 자료 부산역 역사");
        var shell = root != null ? root.transform.Find("MainShell") : null;
        if (shell == null) throw new InvalidOperationException("MainShell 을 찾지 못했다");

        Physics.SyncTransforms();
        bool prior = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        var rends = shell.GetComponentsInChildren<MeshRenderer>(true);
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        b.Expand(new Vector3(20f, 0f, 20f)); // 역사 주변만. 도로까지 넓히지 않는다.

        float minX = Mathf.Floor(b.min.x), minZ = Mathf.Floor(b.min.z);
        int nx = Mathf.CeilToInt(b.size.x / Cell) + 1, nz = Mathf.CeilToInt(b.size.z / Cell) + 1;
        float lo = b.min.y - 1f, hi = b.max.y + 1f;

        var stands = new List<Stand>();
        var index = new Dictionary<int, List<int>>();
        for (int cx = 0; cx < nx; cx++)
        for (int cz = 0; cz < nz; cz++)
        {
            float x = minX + cx * Cell, z = minZ + cz * Cell;
            float last = float.NaN;
            for (float h = lo; h <= hi; h += HStep)
            {
                var p = new Vector3(x, h, z);
                if (!Physics.Raycast(p, Vector3.down, out var fh, FloorReach, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(fh.normal.y) < FlatDot) continue;
                float fy = fh.point.y;
                if (!float.IsNaN(last) && Mathf.Abs(fy - last) < SameFloorEps) continue;
                var feet = new Vector3(x, fy + 0.045f, z);
                if (Physics.CheckCapsule(feet + Vector3.up * CapsuleFoot, feet + Vector3.up * CapsuleHead,
                        CapsuleRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
                last = fy;
                int key = cx * nz + cz;
                if (!index.TryGetValue(key, out var list)) index[key] = list = new List<int>();
                list.Add(stands.Count);
                stands.Add(new Stand
                {
                    Cx = cx, Cz = cz, Y = fy,
                    Shell = fh.collider != null && fh.collider.transform.IsChildOf(shell),
                    Col = fh.collider != null ? fh.collider.name : "?",
                });
            }
        }
        Physics.queriesHitBackfaces = prior;

        // 8-연결 성분 분해. 대각선 이동은 sqrt(2)m 이므로 허용 높이차도 그만큼 키운다.
        int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        var comp = new int[stands.Count];
        for (int i = 0; i < comp.Length; i++) comp[i] = -1;
        var compSizes = new List<int>();
        var queue = new Queue<int>();
        for (int s0 = 0; s0 < stands.Count; s0++)
        {
            if (comp[s0] >= 0) continue;
            int id = compSizes.Count; compSizes.Add(0);
            comp[s0] = id; queue.Enqueue(s0); int n = 0;
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue(); n++;
                var s = stands[cur];
                for (int d = 0; d < 8; d++)
                {
                    int key = (s.Cx + dx8[d]) * nz + (s.Cz + dz8[d]);
                    if (!index.TryGetValue(key, out var list)) continue;
                    float allow = d < 4 ? StepUp : StepUp * 1.4142f;
                    foreach (int j in list)
                    {
                        if (comp[j] >= 0) continue;
                        if (Mathf.Abs(stands[j].Y - s.Y) > allow) continue;
                        comp[j] = id; queue.Enqueue(j);
                    }
                }
            }
            compSizes[id] = n;
        }

        // 성분별 통계. 역사 소유 셀이 다수인 성분만 관심 대상.
        int nc = compSizes.Count;
        var cShell = new int[nc]; var cMinY = new float[nc]; var cMaxY = new float[nc];
        var cMinX = new int[nc]; var cMaxX = new int[nc]; var cMinZ = new int[nc]; var cMaxZ = new int[nc];
        var cFloorName = new Dictionary<int, Dictionary<string, int>>();
        for (int i = 0; i < nc; i++) { cMinY[i] = float.MaxValue; cMaxY[i] = float.MinValue; cMinX[i] = int.MaxValue; cMaxX[i] = int.MinValue; cMinZ[i] = int.MaxValue; cMaxZ[i] = int.MinValue; }
        for (int i = 0; i < stands.Count; i++)
        {
            int c = comp[i]; var s = stands[i];
            if (s.Shell) cShell[c]++;
            if (s.Y < cMinY[c]) cMinY[c] = s.Y; if (s.Y > cMaxY[c]) cMaxY[c] = s.Y;
            if (s.Cx < cMinX[c]) cMinX[c] = s.Cx; if (s.Cx > cMaxX[c]) cMaxX[c] = s.Cx;
            if (s.Cz < cMinZ[c]) cMinZ[c] = s.Cz; if (s.Cz > cMaxZ[c]) cMaxZ[c] = s.Cz;
            if (!cFloorName.TryGetValue(c, out var m)) cFloorName[c] = m = new Dictionary<string, int>();
            m.TryGetValue(s.Col, out int k); m[s.Col] = k + 1;
        }

        // 역사 소유 셀 수로 정렬
        var order = new List<int>();
        for (int i = 0; i < nc; i++) order.Add(i);
        order.Sort((p, q) => cShell[q].CompareTo(cShell[p]));

        // 최대 역사 성분의 평면도를 그린다 (높이를 색으로).
        string png = "";
        int best = order.Count > 0 ? order[0] : -1;
        if (best >= 0 && cShell[best] > 0)
        {
            int w = cMaxX[best] - cMinX[best] + 1, h2 = cMaxZ[best] - cMinZ[best] + 1;
            var tex = new Texture2D(w, h2, TextureFormat.RGBA32, false);
            var px = new Color32[w * h2];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(18, 18, 24, 255);
            float span = Mathf.Max(0.001f, cMaxY[best] - cMinY[best]);
            for (int i = 0; i < stands.Count; i++)
            {
                if (comp[i] != best) continue;
                var s = stands[i];
                int u = s.Cx - cMinX[best], v = s.Cz - cMinZ[best];
                if (u < 0 || v < 0 || u >= w || v >= h2) continue;
                float t = (s.Y - cMinY[best]) / span;
                px[v * w + u] = s.Shell
                    ? new Color32((byte)(40 + 200 * t), (byte)(220 - 120 * t), 90, 255)  // 역사: 초록→노랑
                    : new Color32(90, 90, (byte)(140 + 100 * t), 255);                    // 그 밖: 파랑
            }
            tex.SetPixels32(px); tex.Apply();
            png = Path.Combine(outDir, "rooms-largest-component.png");
            File.WriteAllBytes(Path.GetFullPath(png), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.station-rooms.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"method\": \"standable voxels then 8-connected component decomposition without a seed; station ownership by transform ancestry under MainShell\",\n");
        sb.AppendFormat(inv, "  \"cellMetres\": {0}, \"stepUpMetres\": {1},\n", Cell, StepUp);
        sb.AppendFormat(inv, "  \"standableVoxels\": {0}, \"componentCount\": {1},\n", stands.Count, nc);
        sb.AppendFormat(inv, "  \"largestComponentPng\": \"{0}\",\n", Esc(png));
        sb.Append("  \"components\": [\n");
        int emitted = 0;
        foreach (int c in order)
        {
            if (emitted >= 12 || cShell[c] == 0) break;
            var m = cFloorName[c]; string dom = ""; int domN = 0;
            foreach (var kv in m) if (kv.Value > domN) { domN = kv.Value; dom = kv.Key; }
            if (emitted > 0) sb.Append(",\n");
            sb.Append("    {");
            sb.AppendFormat(inv, "\"id\": {0}, \"cells\": {1}, \"stationCells\": {2}, ", c, compSizes[c], cShell[c]);
            sb.AppendFormat(inv, "\"yRange\": [{0:F1}, {1:F1}], ", cMinY[c], cMaxY[c]);
            sb.AppendFormat(inv, "\"worldX\": [{0:F1}, {1:F1}], \"worldZ\": [{2:F1}, {3:F1}], ",
                minX + cMinX[c] * Cell, minX + cMaxX[c] * Cell, minZ + cMinZ[c] * Cell, minZ + cMaxZ[c] * Cell);
            sb.AppendFormat(inv, "\"dominantFloor\": \"{0}\", \"dominantCount\": {1}", Esc(dom), domN);
            sb.Append("}");
            emitted++;
        }
        sb.Append("\n  ]\n}\n");
        var jsonPath = Path.Combine(outDir, "station-rooms.json");
        File.WriteAllText(Path.GetFullPath(jsonPath), sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"ROOMS stands={stands.Count} components={nc} largestStationCells={(best >= 0 ? cShell[best] : 0)} -> {jsonPath}");
    }

    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
