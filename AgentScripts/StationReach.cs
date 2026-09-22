// 부산역 실내 도달성 재측정 — CharacterController 실제 규칙으로.
//
// 앞선 StationFloorPlan 은 걸음 규칙을 "1m 당 높이차 0.45m"(약 24도)로 잡고
// 대합실이 도로 스폰에서 도달 불가라고 냈다. 그러나 실제 컨트롤러 설정은
// FirstPersonResponder.cs:41 에서 stepOffset=0.28, slopeLimit=45 다.
// 정상 계단은 30~35도로 1m 당 0.58~0.70m 오르므로 0.45m 임계는 계단을 막는다.
// 즉 "도달 불가"가 건물의 사실이 아니라 임계값의 산물일 수 있다.
//
// 그래서 간선 판정을 컨트롤러 거동에 맞춘다.
//   - 이웃 높이차가 stepOffset 이하면 바로 연결(단차 오르기)
//   - 넘으면 사이를 0.25m 간격으로 세부 표본해 바닥 높이가 매끄럽게 변하는지 본다.
//     연속된 표본의 높이차가 모두 stepOffset 이하면 계단·경사로이므로 연결.
//     한 번에 뛰면 턱이므로 차단.
//   - 세부 표본마다 캡슐 여유도 확인한다(난간·문틀에 막히는 경우).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class StationReach
{
    const float Cell = 1f, HStep = 0.5f, FloorReach = 1.2f, FlatDot = 0.7072f;
    const float CapsuleRadius = 0.28f, CapsuleFoot = 0.28f, CapsuleHead = 1.44f;
    const float StepOffset = 0.28f;      // FirstPersonResponder.cs:41
    const float SlopeLimitDeg = 45f;     // FirstPersonResponder.cs:41
    const float SameFloorEps = 0.3f;
    const int SubSamples = 3;            // 이웃 사이 0.25m 간격

    struct Stand { public int Cx, Cz; public float Y; public bool Shell; }

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
        b.Expand(new Vector3(240f, 0f, 240f)); // 스폰(도로)까지 덮는다

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
                stands.Add(new Stand { Cx = cx, Cz = cz, Y = fy, Shell = fh.collider.transform.IsChildOf(shell) });
            }
        }

        // 스폰 씨앗
        var spawn = new Vector3(-169.5003f, 0.1450f, -40.647f);
        int seed = -1; float bestD = float.MaxValue;
        for (int i = 0; i < stands.Count; i++)
        {
            float dx = (minX + stands[i].Cx * Cell) - spawn.x, dz = (minZ + stands[i].Cz * Cell) - spawn.z;
            float dy = stands[i].Y - spawn.y;
            float d = dx * dx + dz * dz + dy * dy * 4f;
            if (d < bestD) { bestD = d; seed = i; }
        }

        int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 }, dz8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        var reached = new bool[stands.Count];
        int reachedCount = 0, stairEdges = 0, ledgeBlocked = 0, capsuleBlocked = 0;
        if (seed >= 0)
        {
            var q = new Queue<int>();
            reached[seed] = true; reachedCount = 1; q.Enqueue(seed);
            while (q.Count > 0)
            {
                int cur = q.Dequeue(); var s = stands[cur];
                float ax = minX + s.Cx * Cell, az = minZ + s.Cz * Cell;
                for (int d = 0; d < 8; d++)
                {
                    int key = (s.Cx + dx8[d]) * nz + (s.Cz + dz8[d]);
                    if (!index.TryGetValue(key, out var list)) continue;
                    float run = d < 4 ? Cell : Cell * 1.4142f;
                    float slopeMax = run * Mathf.Tan(SlopeLimitDeg * Mathf.Deg2Rad);
                    foreach (int j in list)
                    {
                        if (reached[j]) continue;
                        float dh = stands[j].Y - s.Y;
                        if (Mathf.Abs(dh) > slopeMax) continue;            // 45도 초과
                        bool ok;
                        if (Mathf.Abs(dh) <= StepOffset) ok = true;        // 단차 오르기
                        else
                        {
                            ok = Walkable(ax, az, s.Y, minX + stands[j].Cx * Cell, minZ + stands[j].Cz * Cell, stands[j].Y,
                                          out bool blockedByCapsule);
                            if (ok) stairEdges++;
                            else if (blockedByCapsule) capsuleBlocked++; else ledgeBlocked++;
                        }
                        if (!ok) continue;
                        reached[j] = true; reachedCount++; q.Enqueue(j);
                    }
                }
            }
        }
        Physics.queriesHitBackfaces = prior;

        // 집계: 역사 소유 셀을 층 대역별로
        var bandShell = new Dictionary<float, int>();
        var bandShellReach = new Dictionary<float, int>();
        int concourse = 0, concourseReach = 0;   // StationRooms id=192 범위
        for (int i = 0; i < stands.Count; i++)
        {
            if (!stands[i].Shell) continue;
            float k = Mathf.Round(stands[i].Y * 2f) / 2f;
            Bump(bandShell, k); if (reached[i]) Bump(bandShellReach, k);
            float wx = minX + stands[i].Cx * Cell, wz = minZ + stands[i].Cz * Cell;
            if (stands[i].Y >= 6.0f && stands[i].Y <= 11.0f && wx >= -24 && wx <= 74 && wz >= -91 && wz <= -28)
            { concourse++; if (reached[i]) concourseReach++; }
        }

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.station-reach.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"walkRule\": \"CharacterController semantics from FirstPersonResponder.cs:41 — stepOffset 0.28, slopeLimit 45deg. Edges above stepOffset are sub-sampled every 0.25m; the edge connects only if every consecutive floor rise stays within stepOffset and the capsule is clear at each sub-point.\",\n");
        sb.Append("  \"supersedes\": \"station-floorplan.json used a flat 0.45m per 1m threshold (~24deg), which blocks normal 30-35deg stairs\",\n");
        sb.AppendFormat(inv, "  \"standableVoxels\": {0}, \"reachableVoxels\": {1},\n", stands.Count, reachedCount);
        sb.AppendFormat(inv, "  \"stairEdgesAccepted\": {0}, \"ledgeEdgesRejected\": {1}, \"capsuleBlockedEdges\": {2},\n",
            stairEdges, ledgeBlocked, capsuleBlocked);
        sb.AppendFormat(inv, "  \"concourseCells\": {0}, \"concourseReachable\": {1}, \"concourseReachableShare\": {2:F3},\n",
            concourse, concourseReach, concourse == 0 ? 0f : (float)concourseReach / concourse);
        sb.Append("  \"shellBands\": [");
        var keys = new List<float>(bandShell.Keys);
        keys.Sort((p, q) => bandShell[q].CompareTo(bandShell[p]));
        for (int i = 0; i < keys.Count && i < 14; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.AppendFormat(inv, "{{\"y\": {0:F1}, \"shell\": {1}, \"reached\": {2}}}", keys[i], bandShell[keys[i]], Get(bandShellReach, keys[i]));
        }
        sb.Append("]\n}\n");
        var jsonPath = Path.Combine(outDir, "station-reach.json");
        File.WriteAllText(Path.GetFullPath(jsonPath), sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"REACH stands={stands.Count} reached={reachedCount} concourse={concourseReach}/{concourse} stairs={stairEdges} ledges={ledgeBlocked} -> {jsonPath}");
    }

    // 두 셀 사이 바닥 단면을 0.25m 간격으로 훑어 계단·경사로인지, 넘을 수 없는 턱인지 가른다.
    static bool Walkable(float ax, float az, float ay, float bx, float bz, float by, out bool blockedByCapsule)
    {
        blockedByCapsule = false;
        float prev = ay;
        for (int k = 1; k <= SubSamples + 1; k++)
        {
            float t = (float)k / (SubSamples + 1);
            float x = Mathf.Lerp(ax, bx, t), z = Mathf.Lerp(az, bz, t);
            float y;
            if (k == SubSamples + 1) y = by;
            else
            {
                var origin = new Vector3(x, Mathf.Max(ay, by) + 0.6f, z);
                if (!Physics.Raycast(origin, Vector3.down, out var h, Mathf.Abs(ay - by) + 1.4f, ~0, QueryTriggerInteraction.Ignore))
                    return false;
                y = h.point.y;
                var feet = new Vector3(x, y + 0.045f, z);
                if (Physics.CheckCapsule(feet + Vector3.up * CapsuleFoot, feet + Vector3.up * CapsuleHead,
                        CapsuleRadius, ~0, QueryTriggerInteraction.Ignore)) { blockedByCapsule = true; return false; }
            }
            if (Mathf.Abs(y - prev) > StepOffset) return false;
            prev = y;
        }
        return true;
    }

    static void Bump(Dictionary<float, int> d, float k) { d.TryGetValue(k, out int c); d[k] = c + 1; }
    static int Get(Dictionary<float, int> d, float k) => d.TryGetValue(k, out int c) ? c : 0;
}
