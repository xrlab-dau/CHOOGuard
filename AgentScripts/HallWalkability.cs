// 신축 맞이방의 실제 보행가능 면적을 계측한다.
// 1m 격자마다 플레이어 캡슐(반경 0.28, 높이 1.72)이 들어갈 자리인지, 바닥이 y=7 인지 검사하고
// 스폰 지점에서 4-이웃 플러드필로 실제 도달 가능 영역을 구한다.
//
// args: [outJson, floorY, radius, height, step]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class HallWalkability
{
    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        string outPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/hall-walkability.json";
        float floorY = P(args, 1, 7.0f);
        float rad = P(args, 2, 0.30f);
        float hgt = P(args, 3, 1.72f);
        float step = P(args, 4, 1.0f);

        bool prev = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        var floor = GameObject.Find("부산역 2층 맞이방 · 신축")?.transform.Find("바닥 · 광택화강석");
        if (floor == null) { Debug.LogError("바닥 없음"); return; }
        var b = floor.GetComponent<MeshRenderer>().bounds;

        var open = new HashSet<long>();
        int tested = 0;
        for (float x = b.min.x; x <= b.max.x; x += step)
        for (float z = b.min.z; z <= b.max.z; z += step)
        {
            tested++;
            var foot = new Vector3(x, floorY + 0.05f, z);
            if (!Physics.Raycast(foot + Vector3.up * 2.5f, Vector3.down, out var hit, 4f)) continue;
            if (Mathf.Abs(hit.point.y - floorY) > 0.25f) continue;
            var p0 = new Vector3(x, floorY + rad + 0.05f, z);
            var p1 = new Vector3(x, floorY + hgt - rad, z);
            if (Physics.CheckCapsule(p0, p1, rad, ~0, QueryTriggerInteraction.Ignore)) continue;
            open.Add(Key(x, z, step));
        }

        // 스폰 근처에서 플러드필
        var player = GameObject.Find("KORAIL 역무원");
        Vector3 seedPos = player != null ? player.transform.position : new Vector3(b.center.x, floorY, b.center.z);
        long seed = Key(seedPos.x, seedPos.z, step);
        if (!open.Contains(seed))
        {
            float best = float.MaxValue; long bk = 0; bool found = false;
            foreach (var k in open)
            {
                Decode(k, step, out var kx, out var kz);
                float d = (new Vector2(kx - seedPos.x, kz - seedPos.z)).sqrMagnitude;
                if (d < best) { best = d; bk = k; found = true; }
            }
            if (!found) { Debug.LogError("개방 셀 0"); return; }
            seed = bk;
        }

        var seen = new HashSet<long> { seed };
        var stack = new Stack<long>(); stack.Push(seed);
        while (stack.Count > 0)
        {
            var k = stack.Pop();
            Decode(k, step, out var x, out var z);
            foreach (var (dx, dz) in new[] { (step, 0f), (-step, 0f), (0f, step), (0f, -step) })
            {
                var nk = Key(x + dx, z + dz, step);
                if (open.Contains(nk) && seen.Add(nk)) stack.Push(nk);
            }
        }

        Physics.queriesHitBackfaces = prev;

        float cell = step * step;
        var sb = new StringBuilder();
        sb.Append("{\n \"schema\":\"chooguard.hall-walkability.v1\",\n");
        sb.Append(" \"method\":\"1m 격자 캡슐 간섭검사 + 스폰 플러드필. 실제 콜라이더 기준.\",\n");
        sb.Append(" \"capsule\":{\"radius\":").Append(F(rad)).Append(",\"height\":").Append(F(hgt)).Append("},\n");
        sb.Append(" \"tested\":").Append(tested).Append(",\n");
        sb.Append(" \"openCells\":").Append(open.Count).Append(",\"openAreaM2\":").Append(F(open.Count * cell)).Append(",\n");
        sb.Append(" \"reachableCells\":").Append(seen.Count).Append(",\"reachableAreaM2\":").Append(F(seen.Count * cell)).Append(",\n");
        sb.Append(" \"reachableSharePct\":").Append(F(open.Count > 0 ? 100.0 * seen.Count / open.Count : 0)).Append("\n}");
        var full = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString());
        Debug.Log("HALL_WALKABILITY open=" + (open.Count * cell).ToString("F0") + "m2 reachable="
                  + (seen.Count * cell).ToString("F0") + "m2 ("
                  + (open.Count > 0 ? 100.0 * seen.Count / open.Count : 0).ToString("F1") + "%)");
    }

    static long Key(float x, float z, float step)
        => (long)(Mathf.RoundToInt(x / step) + 100000) * 1000000L + (Mathf.RoundToInt(z / step) + 100000);

    static void Decode(long k, float step, out float x, out float z)
    {
        long zz = k % 1000000L;
        long xx = (k - zz) / 1000000L;
        x = (xx - 100000) * step;
        z = (zz - 100000) * step;
    }

    static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
