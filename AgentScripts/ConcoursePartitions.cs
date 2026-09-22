// 대합실 구획(거실) 측정 v2 — 벽과 단차를 갈라 센다.
//
// NFTC 101 은 "바닥면적 33제곱미터 이상으로 구획된 각 거실"에 소화기를 요구한다.
//
// v1 의 결함 둘을 고친다.
//  (1) 벽 간선 104 는 실제로 가시선 차단 54 + 높이 단차 50 의 합이었다. 둘을 합쳐 세는
//      바람에 구획이 50조각으로 부풀었다. 단차는 구획이 아니다 — 계단 한 단이 방을
//      둘로 나누지 않는다. 반면 보행거리 계산에는 단차도 장벽이 맞다.
//      그래서 그래프를 둘로 나눈다: 거실 식별용(벽만) / 보행용(벽+단차).
//  (2) 플레이어 자신의 캡슐이 발밑 셀을 가렸다. FpsStationSceneBuilder.cs:175 의
//      priorStates 관례대로 프로브 동안 비활성화하고 finally 로 되돌린다.
//
// 벽 판정: 인접 두 보행 셀 사이 레이가 바닥 위 0.5m 와 1.3m 에서 모두 막히면 벽.
//          문간은 뚫리므로 "벽으로 구획되고 문으로 이어지는 방"과 일치한다.
// 보행 셀 관례는 StationReach.cs / FirstPersonResponder.cs:41 과 동일하다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class ConcoursePartitions
{
    const float Cell = 1f, FlatDot = 0.7072f;
    const float CapsuleRadius = 0.28f, CapsuleFoot = 0.28f, CapsuleHead = 1.44f;
    const float StepOffset = 0.28f;
    const float XMin = -7f, XMax = 59f, ZMin = -64f, ZMax = -30f;
    const float YProbe = 9f, FloorReach = 2.5f, YLo = 6.4f, YHi = 7.7f;
    const float LowEye = 0.5f, HighEye = 1.3f;

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
        var ok = new bool[nx, nz];
        var yOf = new float[nx, nz];
        var wallBlocked = new bool[nx, nz, 4];   // 거실 식별용 — 벽만
        var walkBlocked = new bool[nx, nz, 4];   // 보행용 — 벽 + 단차
        int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
        int cells = 0, wallEdges = 0, stepEdges = 0, openEdges = 0;

        var player = GameObject.Find("KORAIL 역무원");
        bool playerWasActive = player != null && player.activeSelf;
        bool prior = Physics.queriesHitBackfaces;
        try
        {
            if (player != null) player.SetActive(false);
            Physics.SyncTransforms();
            Physics.queriesHitBackfaces = true;

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
                    { walkBlocked[i, j, d] = true; stepEdges++; continue; }   // 단차는 구획이 아니다
                    var from = new Vector3(XMin + i * Cell, yOf[i, j], ZMin + j * Cell);
                    var to = new Vector3(XMin + a * Cell, yOf[a, b], ZMin + b * Cell);
                    bool lo = Physics.Linecast(from + Vector3.up * LowEye, to + Vector3.up * LowEye, ~0, QueryTriggerInteraction.Ignore);
                    bool hi = Physics.Linecast(from + Vector3.up * HighEye, to + Vector3.up * HighEye, ~0, QueryTriggerInteraction.Ignore);
                    if (lo && hi) { wallBlocked[i, j, d] = true; walkBlocked[i, j, d] = true; wallEdges++; }
                    else openEdges++;
                }
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = prior;
            if (player != null) player.SetActive(playerWasActive);
            Physics.SyncTransforms();
        }

        var roomSizes = Components(ok, wallBlocked, nx, nz, di, dj, out var roomOf);
        var walkSizes = Components(ok, walkBlocked, nx, nz, di, dj, out _);

        var order = new List<int>();
        for (int i = 0; i < roomSizes.Count; i++) order.Add(i);
        order.Sort((p, r) => roomSizes[r].CompareTo(roomSizes[p]));
        int rooms33 = 0;
        foreach (int c in order) if (roomSizes[c] * Cell * Cell >= 33f) rooms33++;

        var palette = new Color32[]
        {
            new Color32(230,80,70,255), new Color32(70,190,230,255), new Color32(250,200,60,255),
            new Color32(120,220,120,255), new Color32(200,120,235,255), new Color32(250,150,70,255),
            new Color32(140,160,255,255), new Color32(90,230,200,255),
        };
        var tex = new Texture2D(nx, nz, TextureFormat.RGBA32, false);
        var px = new Color32[nx * nz];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(16, 16, 22, 255);
        var rank = new Dictionary<int, int>();
        for (int r = 0; r < order.Count; r++) rank[order[r]] = r;
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            if (ok[i, j]) px[j * nx + i] = rank[roomOf[i, j]] < palette.Length ? palette[rank[roomOf[i, j]]] : new Color32(70, 70, 80, 255);
        tex.SetPixels32(px); tex.Apply();
        var png = Path.Combine(outDir, "concourse-partitions.png");
        File.WriteAllBytes(Path.GetFullPath(png), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.concourse-partitions.v2\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"question\": \"NFTC 101 의 '33제곱미터 이상으로 구획된 각 거실'이 이 대합실에 몇 개 있는가\",\n");
        sb.Append("  \"method\": \"a wall separates two adjacent walkable cells when the linecast between them is blocked at BOTH 0.5m and 1.3m above the floor; doorways stay open. Height steps over stepOffset block walking but are NOT partitions, so two graphs are kept: wall-only for rooms, wall+step for walking.\",\n");
        sb.Append("  \"v1Defects\": \"v1 counted 104 wall edges that were really 54 wall + 50 step, inflating the partition count; and the player capsule hid its own cell.\",\n");
        sb.AppendFormat(inv, "  \"playerExcluded\": true,\n  \"walkableCells\": {0}, \"wallEdges\": {1}, \"stepEdges\": {2}, \"openEdges\": {3},\n",
            cells, wallEdges, stepEdges, openEdges);
        sb.AppendFormat(inv, "  \"roomCount\": {0}, \"roomsAtLeast33sqm\": {1}, \"walkComponentCount\": {2},\n",
            roomSizes.Count, rooms33, walkSizes.Count);
        sb.Append("  \"rooms\": [");
        for (int r = 0; r < order.Count && r < 14; r++)
        {
            if (r > 0) sb.Append(", ");
            sb.AppendFormat(inv, "{{\"rank\": {0}, \"sqm\": {1}, \"needsOwnUnit\": {2}}}",
                r, roomSizes[order[r]] * Cell * Cell, roomSizes[order[r]] * Cell * Cell >= 33f ? "true" : "false");
        }
        sb.AppendFormat(inv, "],\n  \"partitionMapPng\": \"{0}\"\n}}\n", Esc(png));
        var p = Path.Combine(outDir, "concourse-partitions.json");
        File.WriteAllText(Path.GetFullPath(p), sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"PARTITIONS v2 cells={cells} wall={wallEdges} step={stepEdges} rooms={roomSizes.Count} rooms33={rooms33} walkComps={walkSizes.Count} -> {p}");
    }

    static List<int> Components(bool[,] ok, bool[,,] blocked, int nx, int nz, int[] di, int[] dj, out int[,] compOut)
    {
        var comp = new int[nx, nz];
        for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) comp[i, j] = -1;
        var sizes = new List<int>();
        var q = new Queue<(int, int)>();
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
        compOut = comp;
        return sizes;
    }

    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
