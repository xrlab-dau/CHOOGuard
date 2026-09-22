// 소화기 배치 산정 v2 — 벽 기반 거실 + 보행거리 커버.
//
// 근거: 소화기구 및 자동소화장치의 화재안전기술기준(NFTC 101)
//   - 각 부분으로부터 1개의 소화기까지 보행거리 20m 이내(소형소화기)
//   - 각 층마다 설치하되 바닥면적 33제곱미터 이상으로 구획된 각 거실에도 배치
//   - 바닥으로부터 1.5m 이하에 비치, 축광식 표지 부착
//
// v1(ExtinguisherPlacement.cs)의 결함을 고친다.
//   (1) '구획'을 바닥 머티리얼 경계로 잡았다. 머티리얼은 벽이 아니다. 판정서 자신이
//       source-29e0e0d02d790f52d02b 를 "가장자리를 두르는 띠, 방이 아니다"라고 적었는데도
//       그것을 139제곱미터 거실로 취급해 자투리 배치 5건(커버 1~5셀)을 만들었다.
//       -> 벽(가시선 차단)으로 구획한다. ConcoursePartitions v2 와 같은 규칙.
//   (2) 커버리지 BFS 가 단차를 무시했다. 0.5m 턱 너머를 "20m 안"이라고 셌다.
//       -> 보행 그래프는 벽과 단차를 모두 장벽으로 본다.
//   (3) 플레이어가 자기 발밑 셀을 가렸다.
//       -> FpsStationSceneBuilder.cs:175 의 priorStates 관례로 프로브 동안 비활성화.
//
// Jev 판정(jev-poi-placement-004):
//   placement_method    = bfs_coverage_then_greedy_cover   1.00
//   candidate_positions = wall_adjacent_reachable_cells    0.86
//
// 배치하지 않는다. 좌표만 산정해 보고한다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class ExtinguisherPlacementV2
{
    const float Cell = 1f, FlatDot = 0.7072f;
    const float CapsuleRadius = 0.28f, CapsuleFoot = 0.28f, CapsuleHead = 1.44f;
    const float StepOffset = 0.28f;
    const float LowEye = 0.5f, HighEye = 1.3f;
    const float WalkLimit = 20f;          // NFTC 101 소형소화기 보행거리
    const float RoomThreshold = 33f;      // NFTC 101 구획 거실 면적
    const float MountHeight = 1.2f;       // 1.5m 이하 규정 안
    const float WallReach = 1.2f;         // 벽면 부착 가능 거리
    // 분석 창. 적대검증이 critical 로 지적한 항목: 창이 하드코딩돼 실제 공간보다 작으면
    // 창 밖 보행거리 보장이 거짓이 된다. StationRooms id=192 의 실측 범위가
    // X -24~74, Z -91~-28 이므로 여유 6m 를 둬 그것을 완전히 덮는다.
    // 창에 닿은 셀 수를 함께 보고해 잘림 여부를 출력이 스스로 말하게 한다.
    const float XMin = -30f, XMax = 80f, ZMin = -97f, ZMax = -22f;
    const float YProbe = 9f, FloorReach = 2.5f, YLo = 6.4f, YHi = 7.7f;

    public static void Main(string[] args)
    {
        string outDir = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build";
        Directory.CreateDirectory(Path.GetFullPath(outDir));

        var root = GameObject.Find("공식 자료 부산역 역사");
        var shell = root != null ? root.transform.Find("MainShell") : null;
        if (shell == null) throw new InvalidOperationException("MainShell 을 찾지 못했다");

        int nx = Mathf.RoundToInt((XMax - XMin) / Cell) + 1;
        int nz = Mathf.RoundToInt((ZMax - ZMin) / Cell) + 1;
        var ok = new bool[nx, nz]; var yOf = new float[nx, nz];
        var wallAdj = new bool[nx, nz]; var wallDir = new Vector3[nx, nz];
        var wallBlocked = new bool[nx, nz, 4];
        var walkBlocked = new bool[nx, nz, 4];
        int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
        int cells = 0, wallEdges = 0, stepEdges = 0;

        var player = GameObject.Find("KORAIL 역무원");
        bool playerWasActive = player != null && player.activeSelf;
        bool prior = Physics.queriesHitBackfaces;
        try
        {
            if (player != null) player.SetActive(false);
            Physics.SyncTransforms();
            Physics.queriesHitBackfaces = true;

            var horiz = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
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

                // 부착할 벽이 있는가 — 설치 높이에서 수평으로 본다
                var mount = new Vector3(x, fy + MountHeight, z);
                foreach (var d in horiz)
                    if (Physics.Raycast(mount, d, out var wh, WallReach, ~0, QueryTriggerInteraction.Ignore)
                        && Mathf.Abs(wh.normal.y) < 0.5f && wh.collider.transform.IsChildOf(shell))
                    { wallAdj[i, j] = true; wallDir[i, j] = d; break; }
            }

            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (!ok[i, j]) continue;
                for (int d = 0; d < 4; d++)
                {
                    int a = i + di[d], b = j + dj[d];
                    if (a < 0 || b < 0 || a >= nx || b >= nz || !ok[a, b]) continue;
                    if (Mathf.Abs(yOf[a, b] - yOf[i, j]) > StepOffset)
                    { walkBlocked[i, j, d] = true; stepEdges++; continue; }   // 보행은 막지만 구획은 아니다
                    var from = new Vector3(XMin + i * Cell, yOf[i, j], ZMin + j * Cell);
                    var to = new Vector3(XMin + a * Cell, yOf[a, b], ZMin + b * Cell);
                    if (Physics.Linecast(from + Vector3.up * LowEye, to + Vector3.up * LowEye, ~0, QueryTriggerInteraction.Ignore)
                     && Physics.Linecast(from + Vector3.up * HighEye, to + Vector3.up * HighEye, ~0, QueryTriggerInteraction.Ignore))
                    { wallBlocked[i, j, d] = true; walkBlocked[i, j, d] = true; wallEdges++; }
                }
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = prior;
            if (player != null) player.SetActive(playerWasActive);
            Physics.SyncTransforms();
        }

        // 거실 = 벽을 넘지 않는 연결 성분
        // 창 경계에 닿은 보행 셀 — 0 이 아니면 공간이 잘렸다는 뜻이다
        int edgeTouching = 0;
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            if (ok[i, j] && (i == 0 || j == 0 || i == nx - 1 || j == nz - 1)) edgeTouching++;

        var roomOf = new int[nx, nz];
        var roomSize = Components(ok, wallBlocked, nx, nz, di, dj, roomOf);
        var legalRooms = new List<int>();
        for (int r = 0; r < roomSize.Count; r++) if (roomSize[r] * Cell * Cell >= RoomThreshold) legalRooms.Add(r);

        // 후보 = 벽 부착 가능한 보행 셀, 2m 간격
        var cand = new List<(int i, int j)>();
        for (int i = 0; i < nx; i += 2)
        for (int j = 0; j < nz; j += 2)
            if (ok[i, j] && wallAdj[i, j]) cand.Add((i, j));

        var covers = new List<HashSet<int>>();
        foreach (var c in cand) covers.Add(Coverage(c.i, c.j, ok, yOf, walkBlocked, nx, nz, di, dj));

        // 덮어야 할 셀 — 33제곱미터 이상 거실에 속한 셀만 법규 대상
        var need = new HashSet<int>();
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            if (ok[i, j] && legalRooms.Contains(roomOf[i, j])) need.Add(i * nz + j);

        var chosen = new List<int>();
        // 5a) 거실마다 최소 1개 강제
        foreach (int r in legalRooms)
        {
            int best = -1, bestN = -1;
            for (int ci = 0; ci < cand.Count; ci++)
            {
                if (roomOf[cand[ci].i, cand[ci].j] != r || chosen.Contains(ci)) continue;
                int n = 0; foreach (int k in covers[ci]) if (need.Contains(k)) n++;
                if (n > bestN) { bestN = n; best = ci; }
            }
            if (best >= 0) { chosen.Add(best); foreach (int k in covers[best]) need.Remove(k); }
        }
        // 5b) 남은 미커버를 greedy 로 (Chvatal 1979, ln n 근사)
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
            if (best < 0) break;
            chosen.Add(best);
            foreach (int k in covers[best]) need.Remove(k);
        }

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.extinguisher-placement.v2\",\n  \"computedAt\": \"2026-09-22\",\n");
        sb.Append("  \"regulation\": \"NFTC 101 — 보행거리 20m 이내(소형), 33제곱미터 이상 구획 거실마다, 바닥 1.5m 이하 비치, 축광식 표지\",\n");
        sb.Append("  \"supersedes\": \"extinguisher-placement.json v1 — 구획을 바닥 머티리얼로 잡아 자투리 5건을 만들었고, 커버리지 BFS 가 단차를 무시했으며, 플레이어가 자기 셀을 가렸다\",\n");
        sb.Append("  \"method\": \"rooms = components blocked by WALLS only (dual-height linecast); walking distance = BFS blocked by walls AND steps over stepOffset; candidates = cells with a mountable wall within 1.2m at 1.2m height; one unit forced per room of 33 sqm or more, then greedy set cover (Chvatal 1979)\",\n");
        sb.Append("  \"jevBasis\": \"jev-poi-placement-004: bfs_coverage_then_greedy_cover 1.00, wall_adjacent_reachable_cells 0.86\",\n");
        sb.AppendFormat(inv, "  \"playerExcluded\": true, \"walkableCells\": {0}, \"wallEdges\": {1}, \"stepEdges\": {2},\n", cells, wallEdges, stepEdges);
        sb.AppendFormat(inv, "  \"analysisWindow\": {{\"x\": [{0}, {1}], \"z\": [{2}, {3}]}}, \"cellsTouchingWindowEdge\": {4},\n", XMin, XMax, ZMin, ZMax, edgeTouching);
        sb.AppendFormat(inv, "  \"roomCount\": {0}, \"legalRooms\": {1}, \"candidates\": {2}, \"uncoveredAfterSolve\": {3},\n",
            roomSize.Count, legalRooms.Count, cand.Count, need.Count);
        sb.Append("  \"legalRoomAreas\": [");
        for (int r = 0; r < legalRooms.Count; r++)
        { if (r > 0) sb.Append(", "); sb.AppendFormat(inv, "{0}", roomSize[legalRooms[r]] * Cell * Cell); }
        sb.Append("],\n  \"placements\": [\n");
        for (int k = 0; k < chosen.Count; k++)
        {
            var c = cand[chosen[k]];
            float wx = XMin + c.i * Cell, wz = ZMin + c.j * Cell;
            var d = wallDir[c.i, c.j];
            if (k > 0) sb.Append(",\n");
            sb.AppendFormat(inv, "    {{\"index\": {0}, \"floorPosition\": [{1:F1}, {2:F2}, {3:F1}], \"mountY\": {4:F2}, \"wallNormalDir\": [{5:F0}, {6:F0}, {7:F0}], \"roomSqm\": {8}, \"coverCells\": {9}}}",
                k, wx, yOf[c.i, c.j], wz, yOf[c.i, c.j] + MountHeight, d.x, d.y, d.z,
                roomSize[roomOf[c.i, c.j]] * Cell * Cell, covers[chosen[k]].Count);
        }
        sb.Append("\n  ]\n}\n");
        var p = Path.Combine(outDir, "extinguisher-placement-v3.json");
        File.WriteAllText(Path.GetFullPath(p), sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"EXT_V2 cells={cells} rooms={roomSize.Count} legal={legalRooms.Count} cand={cand.Count} chosen={chosen.Count} uncovered={need.Count} -> {p}");
    }

    static List<int> Components(bool[,] ok, bool[,,] blocked, int nx, int nz, int[] di, int[] dj, int[,] comp)
    {
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) comp[i, j] = -1;
        var sizes = new List<int>(); var q = new Queue<(int, int)>();
        for (int i0 = 0; i0 < nx; i0++)
        for (int j0 = 0; j0 < nz; j0++)
        {
            if (!ok[i0, j0] || comp[i0, j0] >= 0) continue;
            int id = sizes.Count; sizes.Add(0);
            comp[i0, j0] = id; q.Enqueue((i0, j0)); int n = 0;
            while (q.Count > 0)
            {
                var (i, j) = q.Dequeue(); n++;
                for (int d = 0; d < 4; d++)
                {
                    if (blocked[i, j, d]) continue;
                    int a = i + di[d], b = j + dj[d];
                    if (a < 0 || b < 0 || a >= nx || b >= nz || !ok[a, b] || comp[a, b] >= 0) continue;
                    comp[a, b] = id; q.Enqueue((a, b));
                }
            }
            sizes[id] = n;
        }
        return sizes;
    }

    static HashSet<int> Coverage(int si, int sj, bool[,] ok, float[,] y, bool[,,] blocked, int nx, int nz, int[] di, int[] dj)
    {
        var dist = new float[nx, nz];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) dist[i, j] = float.PositiveInfinity;
        dist[si, sj] = 0;
        var q = new Queue<(int, int)>(); q.Enqueue((si, sj));
        var res = new HashSet<int> { si * nz + sj };
        while (q.Count > 0)
        {
            var (i, j) = q.Dequeue(); float d0 = dist[i, j];
            for (int d = 0; d < 4; d++)
            {
                if (blocked[i, j, d]) continue;
                int a = i + di[d], b = j + dj[d];
                if (a < 0 || b < 0 || a >= nx || b >= nz || !ok[a, b]) continue;
                float nd = d0 + Cell;
                if (nd >= dist[a, b] || nd > WalkLimit) continue;
                dist[a, b] = nd; res.Add(a * nz + b); q.Enqueue((a, b));
            }
        }
        return res;
    }
}
