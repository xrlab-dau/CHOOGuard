// 부산역 실내 평면도 + 도달성 측정.
//
// v2 프로브가 y=7.0m 대합실(831표본, 3,324제곱미터, 캡슐통과 719)을 확정했다.
// 남은 물음 둘을 한 번에 잰다.
//   1) 스폰(도로, y=0.145)에서 y=7.0 실내까지 실제로 걸어갈 수 있는가
//   2) 그 실내의 평면 형상은 어떤가 (설계 입력)
//
// NavMesh 베이크는 씬을 변형시키므로 쓰지 않는다. 대신 설 수 있는 복셀을 모으고
// 스폰에서 홍수 채우기를 한다. 걸음 규칙은 1m 이동당 높이차 0.45m 이하(약 24도)로,
// CharacterController 기본 slopeLimit 45도보다 보수적이다 — 거짓 양성을 막기 위해서다.
//
// 결과: JSON(요약·연결성분) + PNG(층별 평면도). 표준출력은 한 줄.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class StationFloorPlan
{
    const float Cell = 1f;            // 평면 격자
    const float HStep = 0.5f;         // 높이 주사 간격
    const float FloorReach = 1.2f;    // 발밑 탐색
    const float FlatDot = 0.7072f;
    const float CapsuleRadius = 0.28f;
    const float CapsuleFoot = 0.28f;
    const float CapsuleHead = 1.44f;
    const float StepUp = 0.45f;       // 1m 이동당 허용 높이차
    const float SameFloorEps = 0.3f;  // 같은 바닥으로 묶는 허용차

    struct Stand { public int Cx, Cz; public float Y; public string Col; public bool Shell; }

    public static void Main(string[] args)
    {
        string outDir = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build";
        Directory.CreateDirectory(Path.GetFullPath(outDir));

        var root = GameObject.Find("공식 자료 부산역 역사");
        if (root == null) throw new InvalidOperationException("역사 루트를 찾지 못했다");
        var shell = root.transform.Find("MainShell");
        if (shell == null) throw new InvalidOperationException("MainShell 이 없다");

        Physics.SyncTransforms();
        bool prior = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        var rends = shell.GetComponentsInChildren<MeshRenderer>(true);
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        // 스폰이 바운즈 밖에 있으므로 여유를 둬 스폰까지 덮는다.
        b.Expand(new Vector3(240f, 0f, 240f));

        float minX = Mathf.Floor(b.min.x), minZ = Mathf.Floor(b.min.z);
        int nx = Mathf.CeilToInt(b.size.x / Cell) + 1;
        int nz = Mathf.CeilToInt(b.size.z / Cell) + 1;
        float lo = b.min.y - 1f, hi = b.max.y + 1f;

        // 1) 설 수 있는 복셀 수집
        var stands = new List<Stand>();
        var index = new Dictionary<int, List<int>>(); // cx*nz+cz -> stands 인덱스들
        long tested = 0;
        for (int cx = 0; cx < nx; cx++)
        for (int cz = 0; cz < nz; cz++)
        {
            float x = minX + cx * Cell, z = minZ + cz * Cell;
            float last = float.NaN;
            for (float h = lo; h <= hi; h += HStep)
            {
                tested++;
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
                bool isShell = fh.collider != null && fh.collider.transform.IsChildOf(shell);
                stands.Add(new Stand { Cx = cx, Cz = cz, Y = fy, Col = fh.collider != null ? fh.collider.name : "?", Shell = isShell });
            }
        }
        Physics.queriesHitBackfaces = prior;

        // 2) 스폰에서 홍수 채우기
        var spawn = new Vector3(-169.5003f, 0.1450f, -40.647f); // fps-scene-native-receipt.json spawnFeet
        int seed = -1; float bestD = float.MaxValue;
        for (int i = 0; i < stands.Count; i++)
        {
            float dx = (minX + stands[i].Cx * Cell) - spawn.x;
            float dz = (minZ + stands[i].Cz * Cell) - spawn.z;
            float dy = stands[i].Y - spawn.y;
            float d = dx * dx + dz * dz + dy * dy * 4f;
            if (d < bestD) { bestD = d; seed = i; }
        }

        var reached = new bool[stands.Count];
        int reachedCount = 0;
        if (seed >= 0)
        {
            var queue = new Queue<int>();
            reached[seed] = true; reachedCount = 1; queue.Enqueue(seed);
            int[] dx4 = { 1, -1, 0, 0 }, dz4 = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                var s = stands[cur];
                for (int d = 0; d < 4; d++)
                {
                    int key = (s.Cx + dx4[d]) * nz + (s.Cz + dz4[d]);
                    if (!index.TryGetValue(key, out var list)) continue;
                    foreach (int j in list)
                    {
                        if (reached[j]) continue;
                        if (Mathf.Abs(stands[j].Y - s.Y) > StepUp) continue;
                        reached[j] = true; reachedCount++; queue.Enqueue(j);
                    }
                }
            }
        }

        // 3) 층 대역별 집계 — 역사 자체 바닥과 도달 여부를 갈라 센다
        var bandAll = new Dictionary<float, int>();
        var bandReached = new Dictionary<float, int>();
        var bandStation = new Dictionary<float, int>();
        var bandStationReached = new Dictionary<float, int>();
        for (int i = 0; i < stands.Count; i++)
        {
            float k = Mathf.Round(stands[i].Y * 2f) / 2f;
            Bump(bandAll, k);
            bool isStation = stands[i].Shell;
            if (isStation) Bump(bandStation, k);
            if (reached[i]) { Bump(bandReached, k); if (isStation) Bump(bandStationReached, k); }
        }

        // 4) y=7.0 대합실 평면도 PNG (도달=초록, 미도달=빨강)
        float planY = 7.0f, planTol = 0.75f;
        int pxMinX = int.MaxValue, pxMaxX = int.MinValue, pxMinZ = int.MaxValue, pxMaxZ = int.MinValue;
        var plan = new List<int>();
        for (int i = 0; i < stands.Count; i++)
        {
            if (Mathf.Abs(stands[i].Y - planY) > planTol) continue;
            if (!stands[i].Shell) continue;
            plan.Add(i);
            if (stands[i].Cx < pxMinX) pxMinX = stands[i].Cx; if (stands[i].Cx > pxMaxX) pxMaxX = stands[i].Cx;
            if (stands[i].Cz < pxMinZ) pxMinZ = stands[i].Cz; if (stands[i].Cz > pxMaxZ) pxMaxZ = stands[i].Cz;
        }
        string planPng = "";
        int planReached = 0;
        if (plan.Count > 0)
        {
            int w = pxMaxX - pxMinX + 1, h2 = pxMaxZ - pxMinZ + 1;
            var tex = new Texture2D(w, h2, TextureFormat.RGBA32, false);
            var px = new Color32[w * h2];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(20, 20, 26, 255);
            foreach (int i in plan)
            {
                int u = stands[i].Cx - pxMinX, v = stands[i].Cz - pxMinZ;
                px[v * w + u] = reached[i] ? new Color32(60, 200, 90, 255) : new Color32(210, 70, 60, 255);
                if (reached[i]) planReached++;
            }
            tex.SetPixels32(px); tex.Apply();
            planPng = Path.Combine(outDir, "floorplan-y7.png");
            File.WriteAllBytes(Path.GetFullPath(planPng), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.station-floorplan.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"method\": \"standable voxels (floor within 1.2m, capsule 0.28/0.28/1.44 clear) then 4-connected flood fill from receipt spawnFeet with 0.45m step-up per 1m; station-own decided by transform ancestry under MainShell, not by name prefix (the prefix also matches 출구지붕/선로상층부)\",\n");
        sb.AppendFormat(inv, "  \"cellMetres\": {0}, \"heightStepMetres\": {1}, \"stepUpMetres\": {2},\n", Cell, HStep, StepUp);
        sb.AppendFormat(inv, "  \"positionsTested\": {0}, \"standableVoxels\": {1}, \"reachableVoxels\": {2},\n", tested, stands.Count, reachedCount);
        sb.AppendFormat(inv, "  \"spawnFeet\": [{0:F2}, {1:F2}, {2:F2}], \"spawnSeedFound\": {3},\n",
            spawn.x, spawn.y, spawn.z, seed >= 0 ? "true" : "false");
        sb.AppendFormat(inv, "  \"floorPlanY\": {0}, \"floorPlanCells\": {1}, \"floorPlanReachable\": {2}, \"floorPlanPng\": \"{3}\",\n",
            planY, plan.Count, planReached, Esc(planPng));
        sb.Append("  \"bands\": [\n");
        var keys = new List<float>(bandAll.Keys);
        keys.Sort((p, q) => bandAll[q].CompareTo(bandAll[p]));
        for (int i = 0; i < keys.Count && i < 18; i++)
        {
            float k = keys[i];
            if (i > 0) sb.Append(",\n");
            sb.AppendFormat(inv, "    {{\"y\": {0:F1}, \"standable\": {1}, \"reachable\": {2}, \"stationFloor\": {3}, \"stationReachable\": {4}}}",
                k, bandAll[k], Get(bandReached, k), Get(bandStation, k), Get(bandStationReached, k));
        }
        sb.Append("\n  ],\n");
        sb.AppendFormat(inv, "  \"bandCount\": {0}\n}}\n", bandAll.Count);

        var jsonPath = Path.Combine(outDir, "station-floorplan.json");
        File.WriteAllText(Path.GetFullPath(jsonPath), sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"FLOORPLAN stand={stands.Count} reached={reachedCount} planCells={plan.Count} planReached={planReached} -> {jsonPath}");
    }

    static void Bump(Dictionary<float, int> d, float k) { d.TryGetValue(k, out int c); d[k] = c + 1; }
    static int Get(Dictionary<float, int> d, float k) => d.TryGetValue(k, out int c) ? c : 0;
    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
