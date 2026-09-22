// 대합실 내부 스폰 지점 선정 — 임의로 찍지 않고 측정으로 고른다.
//
// 사용자 결정(2026-09-22): 튜토리얼 스폰을 대합실 안으로 옮기고 수직 슬라이스를 만든다.
// 대상 구역은 station-zone-signals.json 의 주 대합실
//   material source-01c3e0dccb083b7bc73a, 883제곱미터, y=7.0, 천장 4.13m, x -7~59, z -64~-30
//
// 판정 관례는 FpsStationSceneBuilder.cs:165-171 을 따른다.
//   바닥 법선 dot >= .7072, 캡슐 반지름 .28 / 발 .28 / 머리 1.44, 네 모서리 지지 확인
// 여기에 여유를 더한다. 스폰은 끼면 안 되므로 반지름 .5 로 한 번 더 보고,
// 8방향 2m 개방과 대합실 중심 근접도로 순위를 매긴다.
//
// 옮기지는 않는다. 후보만 내고 좌표를 보고한다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class StationSpawnPick
{
    const float FlatDot = 0.7072f;
    const float R = 0.28f, Foot = 0.28f, Head = 1.44f;   // FirstPersonResponder body
    const float SafeR = 0.5f;                             // 스폰 여유 반지름
    const float OpenRange = 2f;                           // 8방향 개방 요구
    const float XMin = -7f, XMax = 59f, ZMin = -64f, ZMax = -30f;
    const float YProbe = 9.0f, FloorReach = 2.5f;

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

        var dirs = new[]
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1,0,1).normalized, new Vector3(1,0,-1).normalized,
            new Vector3(-1,0,1).normalized, new Vector3(-1,0,-1).normalized,
        };
        var cx = (XMin + XMax) * .5f; var cz = (ZMin + ZMax) * .5f;

        var cands = new List<(Vector3 feet, float head, int open, float dist, string col)>();
        // 단계별 탈락 계측 — 추측하지 않고 어느 필터가 죽이는지 센다.
        int nCells=0,nNoHit=0,nNotFlat=0,nNotShell=0,nBandOut=0,nCapR=0,nCapSafe=0,nUnsupported=0,nNoCeil=0,nLowCeil=0;
        float sampleY=float.NaN; string sampleCol="-"; float sampleNy=float.NaN;
        for (float x = XMin; x <= XMax; x += 1f)
        for (float z = ZMin; z <= ZMax; z += 1f)
        {
            nCells++;
            var origin = new Vector3(x, YProbe, z);
            if (!Physics.Raycast(origin, Vector3.down, out var fh, FloorReach, ~0, QueryTriggerInteraction.Ignore)) { nNoHit++; continue; }
            if (float.IsNaN(sampleY)) { sampleY=fh.point.y; sampleCol=fh.collider!=null?fh.collider.name:"?"; sampleNy=fh.normal.y; }
            // 단면 메시는 법선이 뒤집혀 있을 수 있다. v2 프로브·구역 신호와 같이 절대값으로 본다.
            if (Mathf.Abs(fh.normal.y) < FlatDot) { nNotFlat++; continue; }
            bool floorNormalDown = fh.normal.y < 0;
            if (fh.collider == null || !fh.collider.transform.IsChildOf(shell)) { nNotShell++; continue; }
            var feet = fh.point + Vector3.up * .045f;
            if (feet.y < 6.5f || feet.y > 7.6f) { nBandOut++; continue; }

            // 본체 캡슐 + 여유 캡슐
            if (Physics.CheckCapsule(feet + Vector3.up * Foot, feet + Vector3.up * Head, R, ~0, QueryTriggerInteraction.Ignore)) { nCapR++; continue; }
            // 여유 캡슐은 반지름만 키우면 아래쪽 구가 바닥을 파고들어 어디서도 통과하지 못한다.
            // 발 높이를 반지름 이상으로 함께 올려야 기하학적으로 성립한다.
            if (Physics.CheckCapsule(feet + Vector3.up * SafeR, feet + Vector3.up * Mathf.Max(Head, SafeR + 1f), SafeR, ~0, QueryTriggerInteraction.Ignore)) { nCapSafe++; continue; }

            // 네 모서리 지지 (빌더 관례)
            bool supported = true;
            foreach (var o in new[] { Vector3.right * .25f, Vector3.left * .25f, Vector3.forward * .25f, Vector3.back * .25f })
                if (!Physics.Raycast(feet + o + Vector3.up * .15f, Vector3.down, out var e, .28f, ~0, QueryTriggerInteraction.Ignore)
                    || Mathf.Abs(e.normal.y) < FlatDot) { supported = false; break; }
            if (!supported) { nUnsupported++; continue; }

            // 천장 확인 — 실내여야 한다
            if (!Physics.Raycast(feet + Vector3.up * .2f, Vector3.up, out var ch, 8f, ~0, QueryTriggerInteraction.Ignore)) { nNoCeil++; continue; }
            float head = ch.point.y - fh.point.y;
            if (head < 2.2f) { nLowCeil++; continue; }

            // 8방향 2m 개방
            int open = 0;
            var eye = feet + Vector3.up * 1.6f;
            foreach (var d in dirs)
                if (!Physics.Raycast(eye, d, OpenRange, ~0, QueryTriggerInteraction.Ignore)) open++;

            float dist = Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz));
            cands.Add((feet, head, open, dist, (floorNormalDown ? "DOWN|" : "UP|") + fh.collider.name));
        }
        Physics.queriesHitBackfaces = prior;

        // 8방향 개방 우선, 그 다음 중심 근접
        cands.Sort((a, c) => a.open != c.open ? c.open.CompareTo(a.open) : a.dist.CompareTo(c.dist));

        // 현재 플레이어 위치도 함께 보고
        var player = GameObject.Find("KORAIL 역무원");
        string playerPos = player != null
            ? string.Format(CultureInfo.InvariantCulture, "[{0:F2}, {1:F2}, {2:F2}]",
                player.transform.position.x, player.transform.position.y, player.transform.position.z)
            : "null";

        var sb = new StringBuilder(); var inv = CultureInfo.InvariantCulture;
        sb.Append("{\n  \"schema\": \"chooguard.station-spawn-pick.v1\",\n  \"probedAt\": \"2026-09-22\",\n");
        sb.Append("  \"targetZone\": \"station-zone-signals.json source-01c3e0dccb083b7bc73a 주 대합실 883제곱미터\",\n");
        sb.Append("  \"rule\": \"FpsStationSceneBuilder.cs:165-171 convention plus a 0.5m safety capsule, ceiling >= 2.2m, ranked by 8-direction 2m openness then centrality\",\n");
        sb.AppendFormat(inv, "  \"currentPlayerPosition\": {0},\n", playerPos);
        sb.AppendFormat(inv, "  \"candidates\": {0},\n", cands.Count);
        sb.AppendFormat(inv, "  \"rejectionStages\": {{\"cells\": {0}, \"noFloorHit\": {1}, \"notFlat\": {2}, \"notShell\": {3}, \"bandOut\": {4}, \"capsuleR\": {5}, \"capsuleSafe\": {6}, \"unsupported\": {7}, \"noCeiling\": {8}, \"lowCeiling\": {9}}},\n",
            nCells,nNoHit,nNotFlat,nNotShell,nBandOut,nCapR,nCapSafe,nUnsupported,nNoCeil,nLowCeil);
        sb.AppendFormat(inv, "  \"firstHitSample\": {{\"y\": {0:F2}, \"normalY\": {1:F3}, \"collider\": \"{2}\"}},\n", sampleY, sampleNy, sampleCol);
        sb.Append("  \"top\": [\n");
        for (int i = 0; i < cands.Count && i < 8; i++)
        {
            var c = cands[i];
            if (i > 0) sb.Append(",\n");
            sb.AppendFormat(inv, "    {{\"feet\": [{0:F2}, {1:F2}, {2:F2}], \"headroom\": {3:F2}, \"openDirs\": {4}, \"distToZoneCentre\": {5:F1}, \"floorCollider\": \"{6}\"}}",
                c.feet.x, c.feet.y, c.feet.z, c.head, c.open, c.dist, c.col);
        }
        sb.Append("\n  ]\n}\n");
        var p = Path.Combine(outDir, "station-spawn-pick.json");
        File.WriteAllText(Path.GetFullPath(p), sb.ToString(), new UTF8Encoding(false));

        Debug.Log($"SPAWN_PICK candidates={cands.Count} best={(cands.Count > 0 ? cands[0].feet.ToString("F2") : "none")} player={playerPos} -> {p}");
    }
}
