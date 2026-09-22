// 부산역 실내 프로브 v2 — 높이 주사(走査) 방식.
//
// v1 은 감사와 같이 위에서 아래로 관통시켰다. 그러나 PhysX 의 RaycastNonAlloc 은
// 콜라이더(shape)당 최근접 히트 하나만 돌려준다. MainShell 은 메시가 8개뿐이라
// 관통 방식으로는 층을 셀 수 없다(점당 2.2히트 vs 감사의 최대 120면).
//
// v2 는 공간 안에 서서 재는 방식으로 바꾼다. 각 (x,z) 에서 높이를 0.5m 씩 훑으며
//   - 아래로 3.5m: 최근접 면 = 바닥
//   - 위로 6.5m: 최근접 면 = 천장
// 각 방향의 최근접 면이 곧 원하는 면이므로 shape당 1히트 제약이 무해해진다.
//
// 뒷면 히트를 켠다. 단면 메시는 법선이 한쪽만 향하므로, 아래에서 천장을 올려다보면
// 뒷면일 수 있다. 그래서 법선 판정은 부호가 아니라 abs(normal.y) 로 한다.
//
// 결과는 JSON 파일로만 쓰고 표준출력에는 요약 한 줄만 낸다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class StationInteriorProbeV2
{
    const float GridStep = 2f;
    const float HeightStep = 0.5f;
    const float FloorReach = 3.5f;   // 발밑 탐색 거리
    const float CeilReach = 6.5f;    // 머리 위 탐색 거리
    const float MinHead = 2.2f;      // 감사와 동일
    const float MaxHead = 6.0f;      // 감사와 동일
    const float FlatDot = 0.7072f;   // FpsStationSceneBuilder 관례 (45도)
    const float CapsuleRadius = 0.28f;
    const float CapsuleFoot = 0.28f;
    const float CapsuleHead = 1.44f;
    const float EyeHeight = 1.6f;
    const float WallRange = 30f;

    sealed class Sample
    {
        public float X, Z, FloorY, CeilY, Head;
        public string FloorCollider, CeilCollider;
        public bool CapsuleClear;
        public int WallsHit;
        public float NearestWall;
    }

    public static void Main(string[] args)
    {
        var outPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0]
            : ".planning/2026-09-22-station-interior-build/unity-interior-probe-v2.json";

        var root = GameObject.Find("공식 자료 부산역 역사");
        if (root == null) throw new InvalidOperationException("역사 루트를 찾지 못했다");
        var shell = root.transform.Find("MainShell");
        if (shell == null) throw new InvalidOperationException("MainShell 자식이 없다");

        Physics.SyncTransforms();
        bool priorBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        var renderers = shell.GetComponentsInChildren<MeshRenderer>(true);
        var shellBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) shellBounds.Encapsulate(renderers[i].bounds);

        // 0) 원인 증명: 한 점에서 관통 수집을 해 콜라이더당 히트 수를 센다.
        //    같은 콜라이더가 두 번 이상 나오지 않으면 shape당 1히트 제약이 사실이다.
        var diagHits = new RaycastHit[512];
        var diagCenter = new Vector3(shellBounds.center.x, shellBounds.max.y + 10f, shellBounds.center.z);
        int diagN = Physics.RaycastNonAlloc(diagCenter, Vector3.down, diagHits, shellBounds.size.y + 30f, ~0, QueryTriggerInteraction.Ignore);
        var diagPerCollider = new Dictionary<int, int>();
        for (int k = 0; k < diagN; k++)
        {
            int id = diagHits[k].collider != null ? diagHits[k].collider.GetInstanceID() : 0;
            diagPerCollider.TryGetValue(id, out int c);
            diagPerCollider[id] = c + 1;
        }
        int diagMaxPerCollider = 0;
        foreach (var kv in diagPerCollider) if (kv.Value > diagMaxPerCollider) diagMaxPerCollider = kv.Value;

        // 1) 높이 주사
        var samples = new List<Sample>();
        int probeColumns = 0;
        long positionsTested = 0;
        float lo = shellBounds.min.y - 1f, hi = shellBounds.max.y + 1f;

        for (float x = shellBounds.min.x; x <= shellBounds.max.x; x += GridStep)
        for (float z = shellBounds.min.z; z <= shellBounds.max.z; z += GridStep)
        {
            probeColumns++;
            float lastFloor = float.NaN;
            for (float h = lo; h <= hi; h += HeightStep)
            {
                positionsTested++;
                var p = new Vector3(x, h, z);
                if (Physics.CheckSphere(p, 0.15f, ~0, QueryTriggerInteraction.Ignore)) continue; // 고체 안
                if (!Physics.Raycast(p, Vector3.down, out var fh, FloorReach, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(fh.normal.y) < FlatDot) continue;
                float fy = fh.point.y;
                if (!float.IsNaN(lastFloor) && Mathf.Abs(fy - lastFloor) < 0.25f) continue; // 같은 바닥 중복
                if (!Physics.Raycast(p, Vector3.up, out var ch, CeilReach, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(ch.normal.y) < FlatDot) continue;
                float head = ch.point.y - fy;
                if (head < MinHead || head > MaxHead) continue;

                lastFloor = fy;
                samples.Add(new Sample
                {
                    X = x, Z = z, FloorY = fy, CeilY = ch.point.y, Head = head,
                    FloorCollider = fh.collider != null ? fh.collider.name : "?",
                    CeilCollider = ch.collider != null ? ch.collider.name : "?",
                });
            }
        }

        // 2) 캡슐 여유 + 8방향 둘러쌈
        var dirs = new[]
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1,0,1).normalized, new Vector3(1,0,-1).normalized,
            new Vector3(-1,0,1).normalized, new Vector3(-1,0,-1).normalized,
        };
        foreach (var s in samples)
        {
            var feet = new Vector3(s.X, s.FloorY + 0.045f, s.Z);
            s.CapsuleClear = !Physics.CheckCapsule(
                feet + Vector3.up * CapsuleFoot, feet + Vector3.up * CapsuleHead,
                CapsuleRadius, ~0, QueryTriggerInteraction.Ignore);
            var eye = new Vector3(s.X, s.FloorY + EyeHeight, s.Z);
            s.NearestWall = float.PositiveInfinity;
            foreach (var d in dirs)
            {
                if (!Physics.Raycast(eye, d, out var wh, WallRange, ~0, QueryTriggerInteraction.Ignore)) continue;
                s.WallsHit++;
                if (wh.distance < s.NearestWall) s.NearestWall = wh.distance;
            }
            if (float.IsPositiveInfinity(s.NearestWall)) s.NearestWall = -1f;
        }
        Physics.queriesHitBackfaces = priorBackfaces;

        // 3) 층 대역 히스토그램 (0.5m 묶음)
        var bands = new Dictionary<float, List<Sample>>();
        foreach (var s in samples)
        {
            float key = Mathf.Round(s.FloorY * 2f) / 2f;
            if (!bands.TryGetValue(key, out var list)) bands[key] = list = new List<Sample>();
            list.Add(s);
        }
        var ordered = new List<KeyValuePair<float, List<Sample>>>(bands);
        ordered.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));

        // 4) 역사 자체(-부산역 메시가 바닥이자 천장)인 표본만 따로 센다 — 감사의 별표 주장과 같은 기준.
        int stationOwn = 0, stationOwnClear = 0;
        var stationBands = new Dictionary<float, int>();
        foreach (var s in samples)
        {
            if (!s.FloorCollider.StartsWith("OfficialStation_-부산역") ) continue;
            if (!s.CeilCollider.StartsWith("OfficialStation_-부산역")) continue;
            stationOwn++;
            if (s.CapsuleClear) stationOwnClear++;
            float key = Mathf.Round(s.FloorY * 2f) / 2f;
            stationBands.TryGetValue(key, out int c);
            stationBands[key] = c + 1;
        }
        var stationOrdered = new List<KeyValuePair<float, int>>(stationBands);
        stationOrdered.Sort((a, b) => b.Value.CompareTo(a.Value));

        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n");
        sb.Append("  \"schema\": \"chooguard.unity-interior-probe.v2\",\n");
        sb.Append("  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"scene\": \"Assets/ChooGuard/Scenes/FpsStation.unity\",\n");
        sb.Append("  \"method\": \"height sweep 0.5m; from each open position cast down 3.5m for floor and up 6.5m for ceiling; nearest face per direction, so PhysX one-hit-per-shape does not truncate\",\n");
        sb.Append("  \"whyNotV1\": \"v1 cast downward through the whole building like the Blender audit, but PhysX returns at most one hit per collider shape; MainShell has only 8 shapes so floors above the first were unreachable by that method\",\n");
        sb.Append("  \"perShapeHitProof\": {");
        sb.AppendFormat(inv, "\"singleDownwardCastHits\": {0}, \"distinctColliders\": {1}, \"maxHitsOnOneCollider\": {2}",
            diagN, diagPerCollider.Count, diagMaxPerCollider);
        sb.Append("},\n");
        sb.AppendFormat(inv, "  \"gridStepMetres\": {0},\n  \"heightStepMetres\": {1},\n", GridStep, HeightStep);
        sb.AppendFormat(inv, "  \"probeColumns\": {0},\n  \"positionsTested\": {1},\n", probeColumns, positionsTested);
        sb.AppendFormat(inv, "  \"roomSamples\": {0},\n", samples.Count);
        sb.Append("  \"mainShellUnityBounds\": {");
        sb.AppendFormat(inv, "\"min\": [{0:F2}, {1:F2}, {2:F2}], \"max\": [{3:F2}, {4:F2}, {5:F2}]",
            shellBounds.min.x, shellBounds.min.y, shellBounds.min.z,
            shellBounds.max.x, shellBounds.max.y, shellBounds.max.z);
        sb.Append("},\n");
        sb.AppendFormat(inv, "  \"stationOwnInteriorSamples\": {0},\n", stationOwn);
        sb.AppendFormat(inv, "  \"stationOwnInteriorSquareMetresApprox\": {0},\n", stationOwn * GridStep * GridStep);
        sb.AppendFormat(inv, "  \"stationOwnCapsuleClear\": {0},\n", stationOwnClear);
        sb.Append("  \"stationOwnBands\": [");
        for (int i = 0; i < stationOrdered.Count && i < 12; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.AppendFormat(inv, "{{\"floorY\": {0:F1}, \"samples\": {1}}}", stationOrdered[i].Key, stationOrdered[i].Value);
        }
        sb.Append("],\n");
        sb.Append("  \"floorBands\": [\n");
        int emitted = 0;
        foreach (var kv in ordered)
        {
            if (emitted >= 16) break;
            var list = kv.Value;
            int clear = 0, enclosed = 0; float headSum = 0, wallSum = 0; int wallN = 0;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            var floorNames = new Dictionary<string, int>();
            foreach (var s in list)
            {
                if (s.CapsuleClear) clear++;
                if (s.WallsHit == 8) enclosed++;
                headSum += s.Head;
                if (s.NearestWall >= 0) { wallSum += s.NearestWall; wallN++; }
                if (s.X < minX) minX = s.X; if (s.X > maxX) maxX = s.X;
                if (s.Z < minZ) minZ = s.Z; if (s.Z > maxZ) maxZ = s.Z;
                floorNames.TryGetValue(s.FloorCollider, out int c); floorNames[s.FloorCollider] = c + 1;
            }
            string domFloor = ""; int domCount = 0;
            foreach (var kv2 in floorNames) if (kv2.Value > domCount) { domCount = kv2.Value; domFloor = kv2.Key; }
            if (emitted > 0) sb.Append(",\n");
            sb.Append("    {");
            sb.AppendFormat(inv, "\"floorY\": {0:F1}, \"samples\": {1}, \"approxSquareMetres\": {2}, ",
                kv.Key, list.Count, list.Count * GridStep * GridStep);
            sb.AppendFormat(inv, "\"capsuleClear\": {0}, \"enclosed8of8\": {1}, \"meanHeadroom\": {2:F2}, \"meanNearestWall\": {3:F2}, ",
                clear, enclosed, headSum / list.Count, wallN == 0 ? -1f : wallSum / wallN);
            sb.AppendFormat(inv, "\"xRange\": [{0:F1}, {1:F1}], \"zRange\": [{2:F1}, {3:F1}], ", minX, maxX, minZ, maxZ);
            sb.AppendFormat(inv, "\"dominantFloorCollider\": \"{0}\", \"dominantCount\": {1}", Esc(domFloor), domCount);
            sb.Append("}");
            emitted++;
        }
        sb.Append("\n  ],\n");
        sb.AppendFormat(inv, "  \"floorBandCount\": {0}\n", ordered.Count);
        sb.Append("}\n");

        var full = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"INTERIOR_PROBE_V2 columns={probeColumns} positions={positionsTested} rooms={samples.Count} " +
                  $"stationOwn={stationOwn} bands={ordered.Count} perShapeMax={diagMaxPerCollider} -> {outPath}");
    }

    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
