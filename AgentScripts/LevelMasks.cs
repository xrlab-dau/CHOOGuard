// 부산역 역사 실내의 층 구조를 실측한다.
//
// 목적: "2층 대합실"만 보던 이전 측정(1,887셀)은 1층 7,950제곱미터를 설명하지 못한다.
// 수직 점유 히스토그램을 먼저 만들고, 그 봉우리에서 층을 읽는다. 층 높이를 가정하지 않는다.
//
// 방법: PhysX 는 콜라이더당 최근접 히트 하나만 돌려준다(2026-09-22 실측). 따라서
//   위에서 한 번 관통시키지 않고, 0.25m 짧은 구간으로 끊어 내려오며 전부 수집한다.
//   뒷면 히트를 켜 천장(아래를 향한 면)도 잡는다.
//
// 산출: level-masks.json + 층별 PNG. 표준출력은 한 줄.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class LevelMasks
{
    const float Cell = 1.0f;       // 평면 격자
    const float March = 0.25f;     // 수직 주사 구간
    const float HeadRoom = 1.9f;   // 사람이 설 수 있는 최소 천장고
    const float FlatDot = 0.7f;    // 바닥으로 인정할 법선 기울기

    public static void Main(string[] args)
    {
        string outDir = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build";
        Directory.CreateDirectory(Path.GetFullPath(outDir));

        var root = GameObject.Find("공식 자료 부산역 역사");
        if (root == null) throw new InvalidOperationException("역사 루트 없음");

        // 역사 본체(MainShell)만 계보로 고른다. 출구지붕·선로상층부·Layer0 는 제외한다.
        Transform shell = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "MainShell") { shell = t; break; }
        if (shell == null) throw new InvalidOperationException("MainShell 없음");

        Physics.SyncTransforms();
        bool prior = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        var rends = shell.GetComponentsInChildren<MeshRenderer>(true);
        if (rends.Length == 0) throw new InvalidOperationException("MainShell 렌더러 없음");
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        float minX = Mathf.Floor(b.min.x), minZ = Mathf.Floor(b.min.z);
        int nx = Mathf.CeilToInt(b.size.x / Cell) + 1;
        int nz = Mathf.CeilToInt(b.size.z / Cell) + 1;
        float lo = b.min.y - 1f, hi = b.max.y + 1f;
        int nb = Mathf.CeilToInt((hi - lo) / March) + 1;

        var hist = new int[nb];                      // 바닥 법선 히트 수
        var cellFloors = new List<float>[nx * nz];   // 셀별 바닥 y 목록
        var cellCeils = new List<float>[nx * nz];    // 셀별 천장 y 목록
        var hits = new RaycastHit[8];
        long casts = 0, surf = 0;

        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            float wx = minX + ix * Cell + Cell * 0.5f;
            float wz = minZ + iz * Cell + Cell * 0.5f;
            List<float> fl = null, ce = null;
            float y = hi;
            while (y > lo)
            {
                var org = new Vector3(wx, y, wz);
                int n = Physics.RaycastNonAlloc(org, Vector3.down, hits, March, ~0, QueryTriggerInteraction.Ignore);
                casts++;
                for (int k = 0; k < n; k++)
                {
                    var h = hits[k];
                    if (!h.collider.transform.IsChildOf(shell)) continue;
                    surf++;
                    float d = Vector3.Dot(h.normal, Vector3.up);
                    if (d >= FlatDot)
                    {
                        (fl ??= new List<float>()).Add(h.point.y);
                        int bi = Mathf.Clamp(Mathf.FloorToInt((h.point.y - lo) / March), 0, nb - 1);
                        hist[bi]++;
                    }
                    else if (d <= -FlatDot)
                    {
                        (ce ??= new List<float>()).Add(h.point.y);
                    }
                }
                y -= March;
            }
            if (fl != null) { fl.Sort(); cellFloors[ix * nz + iz] = fl; }
            if (ce != null) { ce.Sort(); cellCeils[ix * nz + iz] = ce; }
        }

        // 히스토그램 봉우리 = 층 후보. 0.5m 이웃 내 최대이고 전체 최대의 4% 이상.
        int peakMax = 0;
        for (int i = 0; i < nb; i++) peakMax = Math.Max(peakMax, hist[i]);
        var levels = new List<float>();
        var levelCounts = new List<int>();
        for (int i = 0; i < nb; i++)
        {
            if (hist[i] < peakMax * 0.04f) continue;
            bool top = true;
            for (int j = Math.Max(0, i - 2); j <= Math.Min(nb - 1, i + 2); j++)
                if (hist[j] > hist[i]) { top = false; break; }
            if (!top) continue;
            float yy = lo + i * March + March * 0.5f;
            if (levels.Count > 0 && yy - levels[levels.Count - 1] < 1.5f)
            {
                if (hist[i] > levelCounts[levelCounts.Count - 1]) { levels[levels.Count - 1] = yy; levelCounts[levelCounts.Count - 1] = hist[i]; }
                continue;
            }
            levels.Add(yy); levelCounts.Add(hist[i]);
        }

        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.level-masks.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"method\": \"0.25m 구간 주사 · 뒷면히트 on · MainShell 계보 한정 · 바닥법선 dot>=0.7\",\n");
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"grid\": {{ \"cell\": {0}, \"nx\": {1}, \"nz\": {2}, \"minX\": {3}, \"minZ\": {4}, \"yLo\": {5}, \"yHi\": {6} }},\n",
            Cell, nx, nz, minX, minZ, lo.ToString("F2", CultureInfo.InvariantCulture), hi.ToString("F2", CultureInfo.InvariantCulture));
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"raycasts\": {0},\n  \"surfaceHits\": {1},\n", casts, surf);

        sb.Append("  \"histogram\": [");
        bool first = true;
        for (int i = 0; i < nb; i++)
        {
            if (hist[i] == 0) continue;
            if (!first) sb.Append(", ");
            first = false;
            sb.AppendFormat(CultureInfo.InvariantCulture, "[{0}, {1}]", (lo + i * March).ToString("F2", CultureInfo.InvariantCulture), hist[i]);
        }
        sb.Append("],\n  \"levels\": [\n");

        for (int L = 0; L < levels.Count; L++)
        {
            float ly = levels[L];
            var tex = new Texture2D(nx, nz, TextureFormat.RGB24, false);
            var px = new Color32[nx * nz];
            int standCells = 0, floorCells = 0;
            float clearSum = 0f;
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                int id = ix * nz + iz;
                var fl = cellFloors[id];
                px[iz * nx + ix] = new Color32(8, 8, 12, 255);
                if (fl == null) continue;
                float best = float.NaN;
                for (int k = 0; k < fl.Count; k++)
                    if (Mathf.Abs(fl[k] - ly) <= 0.8f) { best = fl[k]; break; }
                if (float.IsNaN(best)) continue;
                floorCells++;
                // 천장: 이 바닥 위 첫 아래보기 면
                float ceil = float.PositiveInfinity;
                var ce = cellCeils[id];
                if (ce != null)
                    for (int k = 0; k < ce.Count; k++)
                        if (ce[k] > best + 0.4f) { ceil = ce[k]; break; }
                float clear = ceil - best;
                if (clear >= HeadRoom)
                {
                    standCells++;
                    clearSum += Mathf.Min(clear, 30f);
                    byte g = (byte)Mathf.Clamp(60 + clear * 14f, 60, 255);
                    px[iz * nx + ix] = new Color32(40, g, 90, 255);   // 실내 보행 가능
                }
                else
                {
                    px[iz * nx + ix] = new Color32(150, 60, 40, 255); // 바닥은 있으나 머리 위 막힘/노출
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            string png = Path.Combine(outDir, "level-" + ly.ToString("F1", CultureInfo.InvariantCulture).Replace('.', 'p') + ".png");
            File.WriteAllBytes(Path.GetFullPath(png), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            sb.AppendFormat(CultureInfo.InvariantCulture,
                "    {{ \"y\": {0}, \"floorCells\": {1}, \"standableCells\": {2}, \"meanClearHeight\": {3}, \"png\": \"{4}\" }}{5}\n",
                ly.ToString("F2", CultureInfo.InvariantCulture), floorCells, standCells,
                (standCells > 0 ? clearSum / standCells : 0f).ToString("F2", CultureInfo.InvariantCulture),
                Path.GetFileName(png), L == levels.Count - 1 ? "" : ",");
        }
        sb.Append("  ]\n}\n");

        Physics.queriesHitBackfaces = prior;
        string outPath = Path.Combine(outDir, "level-masks.json");
        File.WriteAllText(Path.GetFullPath(outPath), sb.ToString(), new UTF8Encoding(false));

        var line = new StringBuilder("LEVELS ");
        for (int L = 0; L < levels.Count; L++) line.Append(levels[L].ToString("F2", CultureInfo.InvariantCulture)).Append(L == levels.Count - 1 ? "" : ", ");
        Debug.Log(line + " | casts=" + casts + " surf=" + surf + " -> " + outPath);
    }
}
