// 계단·에스컬레이터 탐지 — 올바른 서명으로.
//
// 앞선 InteriorSurvey 는 경사면(|n.y| 0.35~0.7072)을 찾아 2개밖에 못 찾았고 "계단 없음"으로
// 기울었다. 그러나 씬 뷰에 난간과 황흑 경고띠를 갖춘 에스컬레이터 구조물이 보였다.
//
// 탐지기가 틀렸다. 계단은 경사면이 아니다 — 평평한 디딤판(n.y≈1)과 수직 챌판(n.y≈0)의 반복이고,
// 중간 법선은 스트링거 측면에만 나온다. 게다가 높이를 0.5m 로 묶었는데 디딤판 간격은 0.15~0.2m 라
// 전부 한 칸에 뭉개졌다.
//
// 올바른 서명: 한 XZ 기둥에서 **촘촘한 간격으로 여러 층 쌓인 평탄면**.
//   평바닥 기둥은 평탄면이 1~2개. 계단 기둥은 수직으로 여러 개가 0.12~0.30m 간격으로 쌓인다.
// 0.1m 간격으로 짧게(0.15m) 쏘아 shape당 1히트 제약을 피한다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class StairFinder
{
    const float Cell = 1.5f, HStep = 0.1f, RayLen = 0.16f, FlatDot = 0.7f;
    const float YLo = 5f, YHi = 34f;
    const float MinRise = 0.10f, MaxRise = 0.32f;  // 디딤판 간격 범위
    const int MinTreads = 4;                        // 이 이상 연속해야 계단으로 본다

    public static void Main(string[] args)
    {
        string outDir = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build";
        Directory.CreateDirectory(Path.GetFullPath(outDir));

        var root = GameObject.Find("공식 자료 부산역 역사");
        var shell = root != null ? root.transform.Find("MainShell") : null;
        if (shell == null) throw new InvalidOperationException("MainShell 을 찾지 못했다");

        var rends = shell.GetComponentsInChildren<MeshRenderer>(true);
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

        var player = GameObject.Find("KORAIL 역무원");
        bool pa = player != null && player.activeSelf;
        bool prior = Physics.queriesHitBackfaces;

        var runs = new List<(float x, float z, float lo, float hi, int treads, float rise, string col)>();
        int columns = 0, multiLevel = 0;
        var levelHist = new Dictionary<int, int>();

        try
        {
            if (player != null) player.SetActive(false);
            Physics.SyncTransforms();
            Physics.queriesHitBackfaces = true;

            var heights = new List<float>();
            var names = new List<string>();
            for (float x = b.min.x; x <= b.max.x; x += Cell)
            for (float z = b.min.z; z <= b.max.z; z += Cell)
            {
                columns++;
                heights.Clear(); names.Clear();
                float last = float.NaN;
                for (float h = YHi; h >= YLo; h -= HStep)
                {
                    if (!Physics.Raycast(new Vector3(x, h, z), Vector3.down, out var ph, RayLen, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (ph.collider == null || !ph.collider.transform.IsChildOf(shell)) continue;
                    if (Mathf.Abs(ph.normal.y) < FlatDot) continue;
                    float y = ph.point.y;
                    if (!float.IsNaN(last) && Mathf.Abs(y - last) < 0.05f) continue;
                    last = y; heights.Add(y); names.Add(ph.collider.name);
                }
                if (heights.Count >= 2) multiLevel++;
                int bucket = Mathf.Min(heights.Count, 15);
                levelHist.TryGetValue(bucket, out int c); levelHist[bucket] = c + 1;

                // 아래에서 위로 정렬해 연속 오름 구간을 찾는다
                heights.Reverse(); names.Reverse();
                int start = 0;
                for (int i = 1; i <= heights.Count; i++)
                {
                    bool cont = i < heights.Count;
                    if (cont)
                    {
                        float d = heights[i] - heights[i - 1];
                        cont = d >= MinRise && d <= MaxRise;
                    }
                    if (!cont)
                    {
                        int n = i - start;
                        if (n >= MinTreads)
                            runs.Add((x, z, heights[start], heights[i - 1], n,
                                      (heights[i - 1] - heights[start]) / Mathf.Max(1, n - 1), names[start]));
                        start = i;
                    }
                }
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = prior;
            if (player != null) player.SetActive(pa);
            Physics.SyncTransforms();
        }

        runs.Sort((p, q) => q.treads.CompareTo(p.treads));

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.stair-finder.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"why\": \"InteriorSurvey 는 경사면을 찾아 2개만 검출하고 '계단 없음' 으로 기울었으나, 씬 뷰에 난간과 경고띠를 갖춘 에스컬레이터가 보였다. 계단은 경사면이 아니라 평탄한 디딤판의 반복이므로 탐지기가 틀렸다.\",\n");
        sb.Append("  \"signature\": \"한 XZ 기둥에서 0.10~0.32m 간격으로 4단 이상 연속 상승하는 평탄면\",\n");
        sb.AppendFormat(inv, "  \"cellMetres\": {0}, \"heightStepMetres\": {1}, \"columnsProbed\": {2}, \"multiLevelColumns\": {3},\n",
            Cell, HStep, columns, multiLevel);
        sb.Append("  \"levelCountHistogram\": [");
        var lk = new List<int>(levelHist.Keys); lk.Sort();
        for (int i = 0; i < lk.Count; i++)
        { if (i > 0) sb.Append(", "); sb.AppendFormat(inv, "{{\"levels\": {0}, \"columns\": {1}}}", lk[i], levelHist[lk[i]]); }
        sb.AppendFormat(inv, "],\n  \"stairRunsFound\": {0},\n  \"runs\": [\n", runs.Count);
        for (int i = 0; i < runs.Count && i < 30; i++)
        {
            var r = runs[i];
            if (i > 0) sb.Append(",\n");
            sb.AppendFormat(inv, "    {{\"x\": {0:F1}, \"z\": {1:F1}, \"fromY\": {2:F2}, \"toY\": {3:F2}, \"treads\": {4}, \"meanRise\": {5:F3}, \"collider\": \"{6}\"}}",
                r.x, r.z, r.lo, r.hi, r.treads, r.rise, Esc(r.col));
        }
        sb.Append("\n  ]\n}\n");
        var p = Path.Combine(outDir, "stair-finder.json");
        File.WriteAllText(Path.GetFullPath(p), sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"STAIRS columns={columns} multiLevel={multiLevel} runs={runs.Count} -> {p}");
    }

    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
