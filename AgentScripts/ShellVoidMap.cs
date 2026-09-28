// 공식 외피 안의 '빈 공동'을 찾는다.
// 1m 격자로 수평 단면을 잡고, 각 셀에서 +x/-x/+z/-z 네 방향으로 레이를 쏴
// 네 방향 모두 외피에 막히면 '셸 내부', 하나라도 뚫리면 '외부' 로 본다.
// 동시에 그 셀의 바닥 유무와 천장 높이를 기록한다.
//
// args: [outJson, sliceY, xMin, xMax, zMin, zMax, step, maxRay]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class ShellVoidMap
{
    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        string outPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/shell-void.json";
        float sliceY = P(args, 1, 12f);
        float xMin = P(args, 2, -75f), xMax = P(args, 3, 145f);
        float zMin = P(args, 4, -115f), zMax = P(args, 5, 115f);

        // 폐합 판정은 '공식 자료 부산역 역사' 하위 콜라이더만 인정한다.
        // 주변 건물·플랫폼을 벽으로 세면 옥외 공터가 실내로 오판된다.
        var stationRoot = GameObject.Find("공식 자료 부산역 역사");
        if (stationRoot == null) { Debug.LogError("역사 루트 없음"); return; }
        var owned = new HashSet<Collider>(stationRoot.GetComponentsInChildren<Collider>(true));
        Debug.Log("역사 콜라이더 " + owned.Count + "개");
        float step = P(args, 6, 2f);
        float maxRay = P(args, 7, 300f);

        bool prevBack = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        var dirs = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        var rows = new List<string>();
        int inside = 0, total = 0;

        for (float x = xMin; x <= xMax; x += step)
        for (float z = zMin; z <= zMax; z += step)
        {
            total++;
            var o = new Vector3(x, sliceY, z);

            // 자기 셀이 솔리드 안이면 건너뛴다
            if (Physics.CheckSphere(o, 0.35f, ~0, QueryTriggerInteraction.Ignore)) continue;

            bool enclosed = true;
            float nearest = maxRay;
            for (int i = 0; i < 4 && enclosed; i++)
            {
                if (!CastOwned(o, dirs[i], maxRay, owned, out var h)) { enclosed = false; break; }
                if (h.distance < nearest) nearest = h.distance;
            }
            if (!enclosed) continue;

            // 위: 역사 지붕이 반드시 있어야 실내다
            if (!CastOwned(o, Vector3.up, 60f, owned, out var up)) continue;
            float roofY = up.point.y;
            if (roofY < sliceY) continue;

            float floorY = CastOwned(o, Vector3.down, 60f, owned, out var dn) ? dn.point.y : -999f;

            inside++;
            rows.Add("[" + F(x) + "," + F(z) + "," + F(floorY) + "," + F(roofY) + "," + F(nearest) + "]");
        }

        Physics.queriesHitBackfaces = prevBack;

        var sb = new StringBuilder();
        sb.Append("{\n \"schema\":\"chooguard.shell-void.v1\",\n");
        sb.Append(" \"method\":\"수평 4방향 레이 폐합 판정 + 상하 레이. 백페이스 충돌 켬.\",\n");
        sb.Append(" \"sliceY\":").Append(F(sliceY)).Append(",\"stepM\":").Append(F(step)).Append(",\n");
        sb.Append(" \"samples\":").Append(total).Append(",\"insideCells\":").Append(inside)
          .Append(",\"insideAreaM2\":").Append(F(inside * step * step)).Append(",\n");
        sb.Append(" \"cellFormat\":\"[x,z,floorY,roofY,nearestWallDist]\",\n");
        sb.Append(" \"cells\":[").Append(string.Join(",", rows)).Append("]\n}");

        var full = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString());
        Debug.Log("SHELL_VOID y=" + sliceY + " inside=" + inside + " area=" + (inside * step * step).ToString("F0"));
    }

    // 역사 소유 콜라이더에 맞을 때까지 전진하며 캐스트한다. 다른 건물은 통과시킨다.
    static bool CastOwned(Vector3 origin, Vector3 dir, float maxDist, HashSet<Collider> owned, out RaycastHit hit)
    {
        float travelled = 0f;
        var p = origin;
        for (int guard = 0; guard < 64; guard++)
        {
            RaycastHit h;
            float remain = maxDist - travelled;
            if (remain <= 0.01f) break;
            if (!Physics.Raycast(p, dir, out h, remain, ~0, QueryTriggerInteraction.Ignore)) break;
            travelled += h.distance + 0.02f;
            if (owned.Contains(h.collider))
            {
                h.distance = travelled;
                hit = h;
                return true;
            }
            p = h.point + dir * 0.02f;
        }
        hit = default;
        return false;
    }

    static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
