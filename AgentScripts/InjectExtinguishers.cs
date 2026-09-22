// v3 배치 좌표 12개를 FireExtinguisherSliceBuilder 에 주입한다.
//
// 좌표 근거: extinguisher-placement-v3.json
//   NFTC 101 보행거리 20m + 33제곱미터 이상 구획 거실마다 1개 + 바닥 1.5m 이하.
//   벽 기반 구획 5개(785/404/225/106/45제곱미터), 미커버 8셀.
//
// 여기서 하는 일은 좌표 변환뿐이다. 배치 로직은 빌더가 갖는다(Jev 003 extend_existing_slice_builder 1.00).
//   - v3 의 floorPosition 은 보행 셀 중심이라 그대로 두면 통로 한가운데 선다.
//     벽까지 실측해 (벽거리 - 0.22m) 만큼 붙인다.
//   - 회전은 빌더 관례를 따른다. FireExtinguisherSliceBuilder.cs:166 이 "로컬 +Z 가 플레이어 쪽"
//     이라고 못박았고, 벽에 붙은 유닛에 접근하는 방향은 -wallDir 이다.
//     틀리면 판독면이 벽 안으로 숨는다(빌더가 실측으로 잡았다고 기록한 버그).
//   - 튜토리얼 대상은 스폰에서 가장 가까운 하나. 그것만 Corroded=true 다.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class InjectExtinguishers
{
    const float WallGap = 0.22f;       // 벽면에서 본체 중심까지
    const float MountProbeY = 1.2f;    // 벽 탐색 높이 (설치 높이와 같다)
    const float MaxWallProbe = 2.0f;

    public static void Main(string[] args)
    {
        string planPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/extinguisher-placement-v3.json";
        var full = Path.GetFullPath(planPath);
        if (!File.Exists(full)) throw new FileNotFoundException("배치 산정 결과가 없다: " + full);
        var text = File.ReadAllText(full);

        // 최소 파서 — placements 배열에서 floorPosition 과 wallNormalDir 만 꺼낸다.
        var floors = new List<Vector3>();
        var dirs = new List<Vector3>();
        int idx = text.IndexOf("\"placements\"", StringComparison.Ordinal);
        if (idx < 0) throw new InvalidOperationException("placements 배열이 없다");
        while (true)
        {
            int f = text.IndexOf("\"floorPosition\"", idx, StringComparison.Ordinal);
            if (f < 0) break;
            var pos = ReadTriple(text, f);
            int w = text.IndexOf("\"wallNormalDir\"", f, StringComparison.Ordinal);
            if (w < 0) throw new InvalidOperationException("wallNormalDir 이 없다 — v3 산출물이 아니다");
            var dir = ReadTriple(text, w);
            floors.Add(pos); dirs.Add(dir);
            idx = w + 1;
        }
        if (floors.Count == 0) throw new InvalidOperationException("배치가 비었다");

        var player = GameObject.Find("KORAIL 역무원");
        var spawn = player != null ? player.transform.position : Vector3.zero;

        // 스폰에서 가장 가까운 것을 튜토리얼 대상으로 삼는다
        int target = 0; float best = float.MaxValue;
        for (int i = 0; i < floors.Count; i++)
        {
            float d = Vector3.Distance(floors[i], spawn);
            if (d < best) { best = d; target = i; }
        }

        var builderType = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .First(t => t.Name == "FireExtinguisherSliceBuilder");
        var placementType = builderType.GetNestedType("Placement");
        if (placementType == null) throw new InvalidOperationException("Placement 구조체가 없다 — 빌더가 확장되지 않았다");

        bool priorBack = Physics.queriesHitBackfaces;
        bool playerWasActive = player != null && player.activeSelf;
        var arr = Array.CreateInstance(placementType, floors.Count);
        var report = new StringBuilder();
        try
        {
            if (player != null) player.SetActive(false);
            Physics.SyncTransforms();
            Physics.queriesHitBackfaces = true;

            for (int i = 0; i < floors.Count; i++)
            {
                var floor = floors[i];
                var dir = dirs[i].sqrMagnitude > 0.01f ? dirs[i].normalized : Vector3.forward;
                // 벽까지 실측해 붙인다
                float gap = WallGap;
                var probe = floor + Vector3.up * MountProbeY;
                if (Physics.Raycast(probe, dir, out var wh, MaxWallProbe, ~0, QueryTriggerInteraction.Ignore))
                    gap = Mathf.Max(0f, wh.distance - WallGap);
                var pos = floor + dir * gap;

                var p = Activator.CreateInstance(placementType);
                placementType.GetField("Position").SetValue(p, pos);
                placementType.GetField("Rotation").SetValue(p, Quaternion.LookRotation(-dir, Vector3.up));
                placementType.GetField("Serial").SetValue(p, string.Format(CultureInfo.InvariantCulture, "BSN-CONC-FE-{0:000}", i + 1));
                placementType.GetField("Corroded").SetValue(p, i == target);
                placementType.GetField("ExpiryPassed").SetValue(p, false);
                placementType.GetField("TutorialTarget").SetValue(p, i == target);
                arr.SetValue(p, i);

                report.AppendFormat(CultureInfo.InvariantCulture,
                    "  #{0,2} BSN-CONC-FE-{1:000} floor=({2:F1},{3:F2},{4:F1}) -> pos=({5:F2},{6:F2},{7:F2}) 벽까지 {8:F2}m{9}\n",
                    i, i + 1, floor.x, floor.y, floor.z, pos.x, pos.y, pos.z, gap, i == target ? "  [튜토리얼 대상·부식]" : "");
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = priorBack;
            if (player != null) player.SetActive(playerWasActive);
            Physics.SyncTransforms();
        }

        var build = builderType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .First(m => m.Name == "Build" && m.GetParameters().Length == 2);
        var built = (GameObject[])build.Invoke(null, new object[] { arr, true });

        Debug.Log($"INJECT 요청 {floors.Count}개 · 생성 {(built != null ? built.Length : 0)}개 · 튜토리얼 대상 #{target}\n{report}");
    }

    static Vector3 ReadTriple(string s, int from)
    {
        int lb = s.IndexOf('[', from), rb = s.IndexOf(']', lb);
        var parts = s.Substring(lb + 1, rb - lb - 1).Split(',');
        return new Vector3(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture),
            float.Parse(parts[2], CultureInfo.InvariantCulture));
    }
}
