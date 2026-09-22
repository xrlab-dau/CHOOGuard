// 소화기 배치 산정 — 법정 보행거리 기준으로 최소 집합을 고른다.
//
// 근거: 소화기구 및 자동소화장치의 화재안전기술기준(NFTC 101), 국가법령정보센터
//   - 각 부분으로부터 1개의 소화기까지 보행거리 20m 이내(소형소화기)
//   - 바닥면적 33제곱미터 이상으로 구획된 각 거실에도 배치
//   - 바닥으로부터 1.5m 이하에 비치
// 기준이 직선거리가 아니라 보행거리이므로, 보행 가능 격자 위 BFS 최단경로로 잰다.
//
// Jev 판정(jev-poi-placement-004):
//   placement_method   = bfs_coverage_then_greedy_cover      1.00
//   candidate_positions= wall_adjacent_reachable_cells       0.86
//   east_room_handling = own_extinguisher_required           0.97
//
// 바퀴: 복셀 수집과 8-연결 BFS 는 AgentScripts/StationReach.cs 의 관례를 그대로 쓴다
//       (stepOffset 0.28, slopeLimit 45, 캡슐 .28/.28/1.44 — FirstPersonResponder.cs:41).
//       커버 선택은 고전 greedy set cover(Chvatal 1979)로, ln(n) 근사 보장이 있다.
//
// 배치하지 않는다. 좌표만 산정해 보고한다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class ExtinguisherPlacement
{
    const float Cell = 1f, FlatDot = 0.7072f;
    const float CapsuleRadius = 0.28f, CapsuleFoot = 0.28f, CapsuleHead = 1.44f;
    const float StepOffset = 0.28f;
    const float WalkLimitMetres = 20f;   // NFTC 101 소형소화기
    const float RoomAreaThreshold = 33f; // NFTC 101 구획 거실
    const float WallProbe = 1.5f;        // 벽 인접 판정 거리
    const float XMin = -7f, XMax = 59f, ZMin = -64f, ZMax = -30f;
    const float YProbe = 9f, FloorReach = 2.5f, YLo = 6.4f, YHi = 7.7f;

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

        // 서브메시 -> 머티리얼 이름 (구역 판정용). StationZoneSignals 와 같은 방식.
        var ends = new Dictionary<int, int[]>(); var mnames = new Dictionary<int, string[]>();
        foreach (var mc in shell.GetComponentsInChildren<MeshCollider>(true))
        {
            var mesh = mc.sharedMesh; if (mesh == null) continue;
            int sub = mesh.subMeshCount; var e = new int[sub]; int acc = 0;
            for (int i = 0; i < sub; i++) { acc += (int)(mesh.GetIndexCount(i) / 3); e[i] = acc; }
            ends[mc.GetInstanceID()] = e;
            var r = mc.GetComponent<MeshRenderer>(); var sm = r != null ? r.sharedMaterials : null;
            var n = new string[sub];
            for (int i = 0; i < sub; i++) n[i] = sm != null && i < sm.Length && sm[i] != null ? sm[i].name : "(none)";
            mnames[mc.GetInstanceID()] = n;
        }

        // 1) 보행 가능 셀 수집
        int nx = Mathf.RoundToInt((XMax - XMin) / Cell) + 1;
        int nz = Mathf.RoundToInt((ZMax - ZMin) / Cell) + 1;
        var yOf = new float[nx, nz]; var ok = new bool[nx, nz]; var matOf = new string[nx, nz];
        var wallAdj = new bool[nx, nz];
        var dirs8 = new[]
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1,0,1).normalized, new Vector3(1,0,-1).normalized,
            new Vector3(-1,0,1).normalized, new Vector3(-1,0,-1).normalized,
        };
        int cells = 0;
        for (int i = 0; i < nx; i++)
        for (int j = 0; j < nz; j++)
        {
            float x = XMin + i * Cell, z = ZMin + j * Cell;
            if (!Physics.Raycast(new Vector3(x, YProbe, z), Vector3.down, out var fh, FloorReach, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (Mathf.Abs(fh.normal.y) < FlatDot) continue;
            if (fh.collider == null || !fh.collider.transform.IsChildOf(shell)) continue;
            float fy = fh.point.y; if (fy < YLo || fy > YHi) continue;
            var feet = new Vector3(x, fy + 0.045f, z);
            if (Physics.CheckCapsule(feet + Vector3.up * CapsuleFoot, feet + Vector3.up * CapsuleHead,
                    CapsuleRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
            ok[i, j] = true; yOf[i, j] = fy; cells++;
            matOf[i, j] = MatOf(fh, ends, mnames);
            var eye = feet + Vector3.up * 1.2f;
            foreach (var d in dirs8)
                if (Physics.Raycast(eye, d, WallProbe, ~0, QueryTriggerInteraction.Ignore)) { wallAdj[i, j] = true; break; }
        }
        Physics.queriesHitBackfaces = prior;

        // 2) 인접 규칙 — StationReach 와 동일(높이차 stepOffset 이내 또는 45도 이내 연속)
        int[] di = { 1, -1, 0, 0, 1, 1, -1, -1 }, dj = { 0, 0, 1, -1, 1, -1, 1, -1 };
        var cost = new float[8];
        for (int d = 0; d < 8; d++) cost[d] = d < 4 ? Cell : Cell * 1.4142f;

        // 3) 후보: 벽 인접 보행 셀을 2m 간격으로 솎는다
        var cand = new List<(int i, int j)>();
        for (int i = 0; i < nx; i += 2)
        for (int j = 0; j < nz; j += 2)
            if (ok[i, j] && wallAdj[i, j]) cand.Add((i, j));

        // 4) 후보마다 BFS 보행거리 -> 20m 커버 집합
        var covers = new List<HashSet<int>>();
        foreach (var c in cand) covers.Add(Coverage(c.i, c.j, ok, yOf, nx, nz, di, dj, cost));

        // 5) greedy set cover. 머티리얼 구역이 33제곱미터 이상이면 그 구역에 최소 1개를 강제한다.
        var need = new HashSet<int>();
        var areaByMat = new Dictionary<string, int>();
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            if (ok[i, j]) { need.Add(i * nz + j); var m = matOf[i, j] ?? "?"; areaByMat.TryGetValue(m, out int a); areaByMat[m] = a + 1; }

        var chosen = new List<int>();
        var mustCoverMats = new List<string>();
        foreach (var kv in areaByMat) if (kv.Value * Cell * Cell >= RoomAreaThreshold) mustCoverMats.Add(kv.Key);

        // 5a) 구획 거실 강제: 각 대상 머티리얼 구역 안에 있는 후보 중 커버가 가장 큰 것을 먼저 고른다.
        foreach (var m in mustCoverMats)
        {
            bool already = false;
            foreach (int ci in chosen) if ((matOf[cand[ci].i, cand[ci].j] ?? "?") == m) { already = true; break; }
            if (already) continue;
            int best = -1, bestN = -1;
            for (int ci = 0; ci < cand.Count; ci++)
            {
                if ((matOf[cand[ci].i, cand[ci].j] ?? "?") != m) continue;
                int n = 0; foreach (int k in covers[ci]) if (need.Contains(k)) n++;
                if (n > bestN) { bestN = n; best = ci; }
            }
            if (best >= 0) { chosen.Add(best); foreach (int k in covers[best]) need.Remove(k); }
        }

        // 5b) 남은 미커버를 greedy 로 덮는다
        int guard = 0;
        while (need.Count > 0 && guard++ < 64)
        {
            int best = -1, bestN = 0;
            for (int ci = 0; ci < cand.Count; ci++)
            {
                if (chosen.Contains(ci)) continue;
                int n = 0; foreach (int k in covers[ci]) if (need.Contains(k)) n++;
                if (n > bestN) { bestN = n; best = ci; }
            }
            if (best < 0) break;                       // 더 덮을 수 없다 — 고립 셀
            chosen.Add(best);
            foreach (int k in covers[best]) need.Remove(k);
        }

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.extinguisher-placement.v1\",\n  \"computedAt\": \"2026-09-22\",\n");
        sb.Append("  \"regulation\": \"소화기구 및 자동소화장치의 화재안전기술기준(NFTC 101) — 보행거리 20m 이내(소형), 33제곱미터 이상 구획 거실마다, 바닥 1.5m 이하 비치\",\n");
        sb.Append("  \"jevBasis\": \"jev-poi-placement-004: placement_method=bfs_coverage_then_greedy_cover 1.00, candidate_positions=wall_adjacent_reachable_cells 0.86, east_room_handling=own_extinguisher_required 0.97\",\n");
        sb.Append("  \"method\": \"walking distance by 8-connected BFS on walkable cells with CharacterController semantics (StationReach convention), then classic greedy set cover (Chvatal 1979, ln n approximation). Room-partition rule forces one unit inside every floor-material region of at least 33 sqm.\",\n");
        sb.AppendFormat(inv, "  \"walkLimitMetres\": {0}, \"roomAreaThreshold\": {1},\n", WalkLimitMetres, RoomAreaThreshold);
        sb.AppendFormat(inv, "  \"walkableCells\": {0}, \"wallAdjacentCandidates\": {1}, \"uncoveredAfterSolve\": {2},\n",
            cells, cand.Count, need.Count);
        sb.Append("  \"materialRegions\": [");
        int mi = 0;
        foreach (var kv in areaByMat)
        {
            if (mi++ > 0) sb.Append(", ");
            sb.AppendFormat(inv, "{{\"material\": \"{0}\", \"sqm\": {1}, \"needsOwnUnit\": {2}}}",
                Esc(kv.Key), kv.Value * Cell * Cell, (kv.Value * Cell * Cell >= RoomAreaThreshold) ? "true" : "false");
        }
        sb.Append("],\n  \"placements\": [\n");
        for (int k = 0; k < chosen.Count; k++)
        {
            var c = cand[chosen[k]];
            float wx = XMin + c.i * Cell, wz = ZMin + c.j * Cell;
            if (k > 0) sb.Append(",\n");
            sb.AppendFormat(inv, "    {{\"index\": {0}, \"floorPosition\": [{1:F1}, {2:F2}, {3:F1}], \"mountY\": {4:F2}, \"coverCells\": {5}, \"material\": \"{6}\"}}",
                k, wx, yOf[c.i, c.j], wz, yOf[c.i, c.j] + 1.2f, covers[chosen[k]].Count, Esc(matOf[c.i, c.j] ?? "?"));
        }
        sb.Append("\n  ]\n}\n");
        var p = Path.Combine(outDir, "extinguisher-placement.json");
        File.WriteAllText(Path.GetFullPath(p), sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"EXT_PLACEMENT cells={cells} cand={cand.Count} chosen={chosen.Count} uncovered={need.Count} -> {p}");
    }

    // 한 지점에서 모든 보행 셀까지의 보행거리를 재고 20m 이내를 모은다.
    static HashSet<int> Coverage(int si, int sj, bool[,] ok, float[,] y, int nx, int nz, int[] di, int[] dj, float[] cost)
    {
        var dist = new float[nx, nz];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) dist[i, j] = float.PositiveInfinity;
        dist[si, sj] = 0;
        // 간선 가중이 1 과 1.414 두 가지뿐이라 다익스트라 대신 단순 큐 완화로 충분하다.
        var q = new Queue<(int, int)>(); q.Enqueue((si, sj));
        var res = new HashSet<int> { si * nz + sj };
        while (q.Count > 0)
        {
            var (i, j) = q.Dequeue();
            float d0 = dist[i, j];
            for (int d = 0; d < 8; d++)
            {
                int a = i + di[d], b = j + dj[d];
                if (a < 0 || b < 0 || a >= nx || b >= nz || !ok[a, b]) continue;
                if (Mathf.Abs(y[a, b] - y[i, j]) > StepOffset) continue;
                float nd = d0 + cost[d];
                if (nd >= dist[a, b] || nd > WalkLimitMetres) continue;
                dist[a, b] = nd; res.Add(a * nz + b); q.Enqueue((a, b));
            }
        }
        return res;
    }

    static string MatOf(RaycastHit h, Dictionary<int, int[]> ends, Dictionary<int, string[]> names)
    {
        int id = h.collider.GetInstanceID();
        if (!ends.TryGetValue(id, out var e) || !names.TryGetValue(id, out var n)) return "?";
        int t = h.triangleIndex; if (t < 0) return "(no-tri)";
        for (int i = 0; i < e.Length; i++) if (t < e[i]) return n[i];
        return "(oor)";
    }

    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
