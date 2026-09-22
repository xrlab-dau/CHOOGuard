// 실내 종합 조사 — 법선 극성 / 상층 동선 / 조명 밝기.
//
// Jev 006 판정: defect_priority=flipped_normals 0.65, upper_floors=measure_then_decide 1.00,
//               lighting_approach=measure_before_authoring 0.97
//
// 세 가지를 한 번에 잰다.
//  (1) 법선 극성: 실내 바닥 중 아래를 향하는 면이 어디에 얼마나 있는가.
//      런타임은 m_QueriesHitBackfaces=0 이라 그런 바닥은 상호작용 레이가 통과한다.
//  (2) 상층 동선: y 10~13 구간에 중간 참(landing)이 있는가, 경사·계단면(|n.y| 0.5~0.85)이 있는가.
//      계단이 있는데 프로브가 놓친 것인지, 애초에 없는 것인지를 가른다.
//  (3) 조명: 실내 바닥에서 위로 쏴 천장을 맞힌 뒤, 씬의 광원과 앰비언트를 기록한다.
//
// 플레이어는 프로브 동안 비활성화한다(FpsStationSceneBuilder.cs:175 관례).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class InteriorSurvey
{
    const float FlatDot = 0.7072f;
    const float XMin = -30f, XMax = 80f, ZMin = -97f, ZMax = -22f;

    public static void Main(string[] args)
    {
        string outDir = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build";
        Directory.CreateDirectory(Path.GetFullPath(outDir));

        var root = GameObject.Find("공식 자료 부산역 역사");
        var shell = root != null ? root.transform.Find("MainShell") : null;
        if (shell == null) throw new InvalidOperationException("MainShell 을 찾지 못했다");

        var player = GameObject.Find("KORAIL 역무원");
        bool playerWasActive = player != null && player.activeSelf;
        bool prior = Physics.queriesHitBackfaces;

        // (1) 법선 극성 — 콜라이더별 up / down 집계
        var upBy = new Dictionary<string, int>();
        var downBy = new Dictionary<string, int>();
        // (2) 상층 동선
        var heightHist = new Dictionary<float, int>();     // 0.5m 묶음, y 9~34
        var slopeBy = new Dictionary<string, int>();       // 경사·계단면 콜라이더
        int slopeFaces = 0;
        var slopeSamples = new List<string>();

        try
        {
            if (player != null) player.SetActive(false);
            Physics.SyncTransforms();
            Physics.queriesHitBackfaces = true;

            var hits = new RaycastHit[64];
            for (float x = XMin; x <= XMax; x += 2f)
            for (float z = ZMin; z <= ZMax; z += 2f)
            {
                // 실내 바닥 대역(y 6.4~7.7)의 법선 극성
                if (Physics.Raycast(new Vector3(x, 9f, z), Vector3.down, out var fh, 2.6f, ~0, QueryTriggerInteraction.Ignore)
                    && fh.collider != null && fh.collider.transform.IsChildOf(shell)
                    && fh.point.y >= 6.4f && fh.point.y <= 7.7f && Mathf.Abs(fh.normal.y) >= FlatDot)
                {
                    var n = fh.collider.name;
                    if (fh.normal.y > 0) { upBy.TryGetValue(n, out int c); upBy[n] = c + 1; }
                    else { downBy.TryGetValue(n, out int c); downBy[n] = c + 1; }
                }

                // 위에서 아래로 훑으며 y 9~34 의 평탄면 높이 분포와 경사면을 센다.
                // shape당 1히트 제약이 있으므로 여러 높이에서 짧게 쏜다.
                for (float h = 34f; h >= 9f; h -= 0.5f)
                {
                    if (!Physics.Raycast(new Vector3(x, h, z), Vector3.down, out var ph, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (ph.collider == null || !ph.collider.transform.IsChildOf(shell)) continue;
                    float ny = Mathf.Abs(ph.normal.y);
                    if (ny >= FlatDot)
                    {
                        float k = Mathf.Round(ph.point.y * 2f) / 2f;
                        heightHist.TryGetValue(k, out int c); heightHist[k] = c + 1;
                    }
                    else if (ny > 0.35f && ny < FlatDot)   // 경사로·계단 스트링거 후보
                    {
                        slopeFaces++;
                        var n = ph.collider.name; slopeBy.TryGetValue(n, out int c); slopeBy[n] = c + 1;
                        if (slopeSamples.Count < 12)
                            slopeSamples.Add(string.Format(CultureInfo.InvariantCulture,
                                "({0:F0},{1:F2},{2:F0}) n.y={3:F2} {4}", x, ph.point.y, z, ph.normal.y, n));
                    }
                }
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = prior;
            if (player != null) player.SetActive(playerWasActive);
            Physics.SyncTransforms();
        }

        // (3) 조명
        var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var lightLines = new List<string>();
        foreach (var l in lights)
            lightLines.Add(string.Format(CultureInfo.InvariantCulture, "{0} type={1} intensity={2:F2} enabled={3} pos={4}",
                l.name, l.type, l.intensity, l.enabled, l.transform.position.ToString("F1")));

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.interior-survey.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"jevBasis\": \"jev-completion-plan-006: flipped_normals 0.65, measure_then_decide 1.00, measure_before_authoring 0.97\",\n");
        sb.Append("  \"normalPolarity\": {\"note\": \"실내 바닥 대역 y 6.4~7.7 에서 위/아래를 향하는 면의 콜라이더별 표본 수. 런타임은 뒷면을 맞히지 않으므로 down 은 상호작용 레이가 통과한다.\",\n");
        sb.Append("    \"upFacing\": {");
        Emit(sb, upBy); sb.Append("},\n    \"downFacing\": {");
        Emit(sb, downBy); sb.Append("}},\n");
        sb.Append("  \"verticalCirculation\": {\"note\": \"y 9~34 의 평탄면 높이 분포와 경사·계단 후보면(|n.y| 0.35~0.7072).\",\n");
        sb.Append("    \"flatHeightHistogram\": [");
        var keys = new List<float>(heightHist.Keys);
        keys.Sort((a, b) => heightHist[b].CompareTo(heightHist[a]));
        for (int i = 0; i < keys.Count && i < 20; i++)
        { if (i > 0) sb.Append(", "); sb.AppendFormat(inv, "{{\"y\": {0:F1}, \"n\": {1}}}", keys[i], heightHist[keys[i]]); }
        sb.AppendFormat(inv, "],\n    \"slopeFaceSamples\": {0},\n    \"slopeByCollider\": {{", slopeFaces);
        Emit(sb, slopeBy); sb.Append("},\n    \"slopeExamples\": [");
        for (int i = 0; i < slopeSamples.Count; i++)
        { if (i > 0) sb.Append(", "); sb.AppendFormat("\"{0}\"", Esc(slopeSamples[i])); }
        sb.Append("]},\n");
        sb.AppendFormat(inv, "  \"lighting\": {{\"lightCount\": {0}, \"ambientMode\": \"{1}\", \"ambientIntensity\": {2:F2}, \"ambientLight\": \"{3}\", \"ambientSky\": \"{4}\", \"lights\": [",
            lights.Length, RenderSettings.ambientMode, RenderSettings.ambientIntensity,
            ColorUtility.ToHtmlStringRGB(RenderSettings.ambientLight), ColorUtility.ToHtmlStringRGB(RenderSettings.ambientSkyColor));
        for (int i = 0; i < lightLines.Count && i < 10; i++)
        { if (i > 0) sb.Append(", "); sb.AppendFormat("\"{0}\"", Esc(lightLines[i])); }
        sb.Append("]}\n}\n");

        var p = Path.Combine(outDir, "interior-survey.json");
        File.WriteAllText(Path.GetFullPath(p), sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"SURVEY up={upBy.Count}콜라이더 down={downBy.Count}콜라이더 slopeFaces={slopeFaces} lights={lights.Length} -> {p}");
    }

    static void Emit(StringBuilder sb, Dictionary<string, int> d)
    {
        int i = 0;
        foreach (var kv in d)
        { if (i++ > 0) sb.Append(", "); sb.AppendFormat("\"{0}\": {1}", Esc(kv.Key), kv.Value); }
    }
    static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
