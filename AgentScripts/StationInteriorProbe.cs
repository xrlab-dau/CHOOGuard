// 부산역 실내 도달성·둘러쌈 프로브 (Unity 콜라이더 기준).
//
// 감사(ver13-interior-verdict.json)는 Blender 지오메트리를 쟀다. 이 프로브는 같은 방 정의를
// Unity MeshCollider 에 적용해, 플레이어가 실제로 설 수 있는지와 옆이 막혀 있는지를 잰다.
//
// 방 정의는 감사와 동일: 위를 보는 면(normal.y>0.8) 위 2.2~6.0m 에 아래를 보는 면(normal.y<-0.8).
// 캡슐/바닥 임계는 FpsStationSceneBuilder.cs:165-171 의 기존 관례를 그대로 쓴다
// (반지름 .28, 발밑 .28 ~ 머리 1.44, 바닥 법선 dot >= .7072).
//
// 결과는 JSON 파일로만 쓰고 표준출력에는 요약 한 줄만 낸다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class StationInteriorProbe
{
    const float GridStep = 2f;          // 감사와 동일한 2m 격자
    const float MinHead = 2.2f;         // 감사와 동일한 머리 공간 하한
    const float MaxHead = 6.0f;         // 감사와 동일한 상한
    const float FloorDot = 0.7072f;     // FpsStationSceneBuilder 관례 (45도)
    const float CapsuleRadius = 0.28f;  // 동일
    const float CapsuleFoot = 0.28f;
    const float CapsuleHead = 1.44f;
    const float EyeHeight = 1.6f;
    const float WallRange = 30f;        // 둘러쌈 판정 사거리
    const int RayBuffer = 512;

    sealed class Sample
    {
        public float X, Z, FloorY, CeilY, Head;
        public string FloorCollider, CeilCollider;
        public bool CapsuleClear;
        public int WallsHit;            // 8방향 중 30m 안에 벽이 있는 방향 수
        public float NearestWall;
    }

    public static void Main(string[] args)
    {
        var outPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0]
            : ".planning/2026-09-22-station-interior-build/unity-interior-probe.json";

        var root = GameObject.Find("공식 자료 부산역 역사");
        if (root == null) throw new InvalidOperationException("역사 루트를 찾지 못했다: 공식 자료 부산역 역사");

        var shell = root.transform.Find("MainShell");
        if (shell == null) throw new InvalidOperationException("MainShell 자식이 없다");

        Physics.SyncTransforms();

        // 1) MainShell 의 Unity 월드 바운즈 — 소스 z 를 Unity y 로 옮기는 매핑을 실측으로 확정한다.
        var renderers = shell.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("MainShell 아래 MeshRenderer 가 없다");
        var shellBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) shellBounds.Encapsulate(renderers[i].bounds);

        var colliders = shell.GetComponentsInChildren<MeshCollider>(true);
        int convexCount = 0, disabledCount = 0;
        foreach (var c in colliders) { if (c.convex) convexCount++; if (!c.enabled) disabledCount++; }

        // 2) 격자 수직 프로브. 감사와 같은 알고리즘을 Unity 콜라이더에 적용.
        //
        // 비볼록 MeshCollider 는 기본 설정에서 앞면만 맞는다. 아래로 쏘면 천장은 뒷면이라
        // 히트가 사라져 방이 0 이 된다. 감사(순수 기하 교차)와 같은 것을 보려면 뒷면을 켜야 한다.
        bool priorBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        float top = shellBounds.max.y + 10f;
        float castLen = shellBounds.size.y + 30f;
        var hits = new RaycastHit[RayBuffer];
        var samples = new List<Sample>();
        int probePoints = 0;
        int pointsWithAnyHit = 0, totalHits = 0, upFaces = 0, downFaces = 0, bufferFull = 0;

        for (float x = shellBounds.min.x; x <= shellBounds.max.x; x += GridStep)
        for (float z = shellBounds.min.z; z <= shellBounds.max.z; z += GridStep)
        {
            probePoints++;
            var origin = new Vector3(x, top, z);
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, hits, castLen, ~0, QueryTriggerInteraction.Ignore);
            if (n > 0) pointsWithAnyHit++;
            if (n >= RayBuffer) bufferFull++;
            totalHits += n;
            for (int k = 0; k < n; k++)
            {
                if (hits[k].normal.y > 0.8f) upFaces++;
                else if (hits[k].normal.y < -0.8f) downFaces++;
            }
            if (n < 2) continue;

            // 거리 오름차순 = y 내림차순. 위에서부터 훑는다.
            Array.Sort(hits, 0, n, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

            // 바닥 후보마다, 그 위 2.2~6.0m 안의 가장 낮은 천장을 찾는다.
            for (int i = n - 1; i >= 0; i--)
            {
                if (hits[i].normal.y <= 0.8f) continue;           // 바닥이 아니다
                float fy = hits[i].point.y;
                for (int j = i - 1; j >= 0; j--)                   // j 는 더 위
                {
                    if (hits[j].normal.y >= -0.8f) continue;       // 천장이 아니다
                    float head = hits[j].point.y - fy;
                    if (head < MinHead) continue;
                    if (head > MaxHead) break;                     // 더 위는 더 멀다
                    samples.Add(new Sample
                    {
                        X = x, Z = z, FloorY = fy, CeilY = hits[j].point.y, Head = head,
                        FloorCollider = hits[i].collider != null ? hits[i].collider.name : "?",
                        CeilCollider = hits[j].collider != null ? hits[j].collider.name : "?",
                    });
                    break;
                }
            }
        }

        // 3) 방 표본마다 캡슐 여유와 8방향 둘러쌈을 잰다.
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

        // 4) 바닥 높이를 0.5m 로 묶어 층 히스토그램을 만든다.
        var bands = new Dictionary<float, List<Sample>>();
        foreach (var s in samples)
        {
            float key = Mathf.Round(s.FloorY * 2f) / 2f;
            if (!bands.TryGetValue(key, out var list)) bands[key] = list = new List<Sample>();
            list.Add(s);
        }
        var ordered = new List<KeyValuePair<float, List<Sample>>>(bands);
        ordered.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));

        // 5) JSON 을 직접 쓴다. 표본 좌표는 층 대역별 요약으로만 남겨 파일을 작게 유지한다.
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n");
        sb.Append("  \"schema\": \"chooguard.unity-interior-probe.v1\",\n");
        sb.Append("  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"scene\": \"Assets/ChooGuard/Scenes/FpsStation.unity\",\n");
        sb.Append("  \"roomDefinition\": \"floor normal.y>0.8, ceiling normal.y<-0.8 at 2.2~6.0m above; identical to ver13 audit\",\n");
        sb.Append("  \"capsuleConvention\": \"radius 0.28, foot 0.28, head 1.44 — FpsStationSceneBuilder.cs:168\",\n");
        sb.AppendFormat(inv, "  \"gridStepMetres\": {0},\n", GridStep);
        sb.AppendFormat(inv, "  \"probePoints\": {0},\n", probePoints);
        sb.AppendFormat(inv, "  \"roomSamples\": {0},\n", samples.Count);
        sb.AppendFormat(inv, "  \"roomShareOfProbes\": {0:F4},\n", probePoints == 0 ? 0f : (float)samples.Count / probePoints);
        sb.Append("  \"mainShellUnityBounds\": {");
        sb.AppendFormat(inv, "\"min\": [{0:F2}, {1:F2}, {2:F2}], \"max\": [{3:F2}, {4:F2}, {5:F2}]",
            shellBounds.min.x, shellBounds.min.y, shellBounds.min.z,
            shellBounds.max.x, shellBounds.max.y, shellBounds.max.z);
        sb.Append("},\n");
        sb.AppendFormat(inv, "  \"mainShellRenderers\": {0},\n", renderers.Length);
        sb.AppendFormat(inv, "  \"mainShellMeshColliders\": {0},\n", colliders.Length);
        sb.AppendFormat(inv, "  \"convexColliders\": {0},\n", convexCount);
        sb.AppendFormat(inv, "  \"disabledColliders\": {0},\n", disabledCount);
        sb.Append("  \"rayDiagnostics\": {");
        sb.AppendFormat(inv, "\"queriesHitBackfaces\": true, \"pointsWithAnyHit\": {0}, \"totalHits\": {1}, ",
            pointsWithAnyHit, totalHits);
        sb.AppendFormat(inv, "\"upFacingHits\": {0}, \"downFacingHits\": {1}, \"bufferSaturatedPoints\": {2}, \"rayBuffer\": {3}",
            upFaces, downFaces, bufferFull, RayBuffer);
        sb.Append("},\n");
        sb.Append("  \"floorBands\": [\n");
        int emitted = 0;
        foreach (var kv in ordered)
        {
            if (emitted >= 14) break;
            var list = kv.Value;
            int clear = 0, enclosed = 0; float headSum = 0, wallSum = 0; int wallN = 0;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var s in list)
            {
                if (s.CapsuleClear) clear++;
                if (s.WallsHit == 8) enclosed++;
                headSum += s.Head;
                if (s.NearestWall >= 0) { wallSum += s.NearestWall; wallN++; }
                if (s.X < minX) minX = s.X; if (s.X > maxX) maxX = s.X;
                if (s.Z < minZ) minZ = s.Z; if (s.Z > maxZ) maxZ = s.Z;
            }
            if (emitted > 0) sb.Append(",\n");
            sb.Append("    {");
            sb.AppendFormat(inv, "\"floorY\": {0:F1}, \"samples\": {1}, \"approxSquareMetres\": {2}, ",
                kv.Key, list.Count, list.Count * GridStep * GridStep);
            sb.AppendFormat(inv, "\"capsuleClear\": {0}, \"enclosed8of8\": {1}, \"meanHeadroom\": {2:F2}, \"meanNearestWall\": {3:F2}, ",
                clear, enclosed, headSum / list.Count, wallN == 0 ? -1f : wallSum / wallN);
            sb.AppendFormat(inv, "\"xRange\": [{0:F1}, {1:F1}], \"zRange\": [{2:F1}, {3:F1}], ", minX, maxX, minZ, maxZ);
            sb.AppendFormat(inv, "\"floorCollider\": \"{0}\", \"ceilCollider\": \"{1}\"",
                Esc(list[0].FloorCollider), Esc(list[0].CeilCollider));
            sb.Append("}");
            emitted++;
        }
        sb.Append("\n  ],\n");
        sb.AppendFormat(inv, "  \"floorBandCount\": {0}\n", ordered.Count);
        sb.Append("}\n");

        var full = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"INTERIOR_PROBE points={probePoints} rooms={samples.Count} bands={ordered.Count} " +
                  $"shellY=[{shellBounds.min.y:F1},{shellBounds.max.y:F1}] convex={convexCount} disabled={disabledCount} -> {outPath}");
    }

    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
