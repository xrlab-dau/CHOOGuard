// 공식 원본 외피(OfficialBusanStation_*) 안에서 실제로 서서 걸을 수 있는 실내 공간을 실측한다.
// 합성 지오메트리를 전혀 추가하지 않는다. 레이캐스트로 바닥·천장만 읽는다.
//
// 격자점마다: 위에서 아래로 쏴서 바닥 y, 그 위에서 위로 쏴서 천장 y 를 얻는다.
// 유효 실내 = 바닥이 있고, 천장고 >= 2.2m, 바닥이 지면(y<1)이 아닌 곳.
//
// args: [outJson, xMin, xMax, zMin, zMax, step]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class ProbeOfficialInterior
{
    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        string outPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/official-interior-probe.json";
        float xMin = P(args, 1, -70f), xMax = P(args, 2, 165f);
        float zMin = P(args, 3, -225f), zMax = P(args, 4, 170f);
        float step = P(args, 5, 2f);

        bool prevBack = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;   // 지붕 안쪽면(백페이스)을 읽어야 아트리움이 잡힌다

        var cells = new List<string>();
        var byLevel = new Dictionary<int, int>();
        int hits = 0, total = 0;
        float fxMin = float.MaxValue, fxMax = float.MinValue, fzMin = float.MaxValue, fzMax = float.MinValue;

        for (float x = xMin; x <= xMax; x += step)
        for (float z = zMin; z <= zMax; z += step)
        {
            total++;
            // 아래로 훑으며 층마다 바닥 후보를 찾는다
            for (float probeY = 34f; probeY > 0f; probeY -= 0.5f)
            {
                var origin = new Vector3(x, probeY, z);
                RaycastHit down;
                if (!Physics.Raycast(origin, Vector3.down, out down, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float floorY = down.point.y;
                if (floorY < 1.0f) break;                 // 지면/선로층은 실내로 보지 않는다

                // 머리 위 천장
                var head = new Vector3(x, floorY + 0.3f, z);
                RaycastHit up;
                float ceil = Physics.Raycast(head, Vector3.up, out up, 30f, ~0, QueryTriggerInteraction.Ignore)
                    ? up.point.y : -1f;
                float clear = ceil > 0 ? ceil - floorY : -1f;
                if (clear < 2.2f) { probeY = floorY - 0.6f; continue; }

                hits++;
                int lvl = Mathf.RoundToInt(floorY);
                byLevel[lvl] = byLevel.TryGetValue(lvl, out var c) ? c + 1 : 1;
                if (x < fxMin) fxMin = x; if (x > fxMax) fxMax = x;
                if (z < fzMin) fzMin = z; if (z > fzMax) fzMax = z;

                if (cells.Count < 20000)
                    cells.Add("[" + F(x) + "," + F(z) + "," + F(floorY) + "," + F(clear) + "]");
                break;
            }
        }

        var sb = new StringBuilder();
        sb.Append("{\n \"schema\":\"chooguard.official-interior-probe.v1\",\n");
        sb.Append(" \"method\":\"공식 원본 외피 콜라이더만 대상으로 한 레이캐스트 실측. 합성 지오메트리 미추가.\",\n");
        sb.Append(" \"grid\":{\"xMin\":").Append(F(xMin)).Append(",\"xMax\":").Append(F(xMax))
          .Append(",\"zMin\":").Append(F(zMin)).Append(",\"zMax\":").Append(F(zMax))
          .Append(",\"stepM\":").Append(F(step)).Append("},\n");
        sb.Append(" \"samples\":").Append(total).Append(",\n");
        sb.Append(" \"interiorCells\":").Append(hits).Append(",\n");
        sb.Append(" \"interiorAreaM2\":").Append(F(hits * step * step)).Append(",\n");
        sb.Append(" \"footprint\":{\"xMin\":").Append(F(fxMin)).Append(",\"xMax\":").Append(F(fxMax))
          .Append(",\"zMin\":").Append(F(fzMin)).Append(",\"zMax\":").Append(F(fzMax)).Append("},\n");
        sb.Append(" \"byFloorY\":{");
        bool first = true;
        foreach (var kv in byLevel)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.Append("\"").Append(kv.Key).Append("\":").Append(F(kv.Value * step * step));
        }
        sb.Append("},\n");
        sb.Append(" \"cellFormat\":\"[x,z,floorY,clearHeight]\",\n");
        sb.Append(" \"cells\":[").Append(string.Join(",", cells)).Append("]\n}");

        var full = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString());
        Debug.Log("OFFICIAL_INTERIOR_PROBE cells=" + hits + " area=" + (hits * step * step).ToString("F0") + "m2");
    }

    static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
