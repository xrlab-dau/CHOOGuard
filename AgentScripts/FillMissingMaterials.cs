// BaseMap 이 비어 있는 표면을 전부 덮는다. 그룹 경로·오브젝트 이름으로 실제 역사 마감에 맞는 PBR 을 고른다.
// DTF M 축의 texturedAreaRatio 를 직접 끌어올리는 작업이다.
//
// args: [dryRun("1"|"0")]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class FillMissingMaterials
{
    const string MatDir = "Assets/ChooGuard/Art/StationInterior/Materials/";

    static readonly string[] Roots = {
        "부산역 역사 내부", "부산역 역사 내부 · 마감",
        "부산역 역사 내부 · 수직동선", "부산역 역사 내부 · 재사용 자산"
    };

    // (경로/이름 조건, 머티리얼) — 위에서 아래로 평가, 첫 매치 채택
    static readonly (string pathKey, string nameKey, string mat)[] Rules = {
        // B1 지하도상가 — 실제 부산역 지하도상가는 화강석 바닥 + 도장 석고 점포벽
        ("B1 지하도상가 바닥", null,      "바닥_화강석"),
        ("B1 지하도상가 점포", "간판",    "사인_청색"),
        ("B1 지하도상가 점포", null,      "PBR_Tiles074_4K"),
        ("B1 지하도상가 벽",   null,      "PBR_PaintedPlaster017_4K"),

        // 방화구획 — 방화셔터와 내화벽
        ("방화구획", "셔터",             "방화셔터"),
        ("방화구획", null,               "PBR_Concrete048_2K"),

        // 임대매장 — 바닥은 목재, 측벽·배면벽은 도장, 간판은 청색 사인
        ("임대매장", "간판",             "사인_청색"),
        ("임대매장", "바닥",             "PBR_WoodFloor064_4K"),
        ("임대매장", "측벽",             "벽_내장"),
        ("임대매장", "배면벽",           "벽_내장"),

        // 출입구
        ("출입구", "유리",               "유리_커튼월"),
        ("출입구", null,                 "PBR_Metal046A_2K"),

        // 재사용 자산
        ("안내 스테이션", null,          "카운터_목재"),
        ("자판기",        null,          "발매기_외장"),
        ("키오스크",      null,          "발매기_외장"),

        // 기둥·천장 (혹시 남은 것)
        ("기둥 격자", null,              "기둥_마감"),
        ("천장",      null,              "PBR_OfficeCeiling003_2K"),

        // 최종 폴백 — 이름 기반. 무지 표면을 남기지 않는다.
        ("", "사인",                     "사인_청색"),
        ("", "표찰",                     "사인_청색"),
        ("", "표지",                     "사인_청색"),
        ("", "유리",                     "유리_커튼월"),
        ("", "셔터",                     "방화셔터"),
        ("", "바닥",                     "바닥_화강석"),
        ("", "슬래브",                   "바닥_화강석"),
        ("", "카운터",                   "카운터_목재"),
        ("", "손잡이",                   "PBR_Metal046A_2K"),
        ("", "난간",                     "PBR_Metal046A_2K"),
        ("", "벽",                       "벽_내장"),
        ("", "인방",                     "벽_내장"),
        ("", "천장",                     "PBR_OfficeCeiling003_2K"),
        ("", null,                       "PBR_PaintedPlaster017_4K"),
    };

    public static void Main(string[] args)
    {
        bool dry = args != null && args.Length > 0 && args[0] == "1";

        var cache = new Dictionary<string, Material>();
        Func<string, Material> get = id =>
        {
            if (cache.TryGetValue(id, out var m)) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + id + ".mat");
            cache[id] = m;
            return m;
        };

        var missing = Rules.Select(r => r.mat).Distinct().Where(id => get(id) == null).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("머티리얼 없음: " + string.Join(", ", missing));

        var applied = new Dictionary<string, int>();
        int touched = 0, skipped = 0, unmatched = 0;
        var unmatchedSample = new List<string>();

        foreach (var rootName in Roots)
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = mr.sharedMaterials;
                bool needs = false;
                foreach (var m in mats)
                {
                    if (m == null) { needs = true; break; }
                    bool hasTex = (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
                               || (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null);
                    if (!hasTex) { needs = true; break; }
                }
                if (!needs) { skipped++; continue; }

                string path = PathOf(mr.transform);
                string name = mr.gameObject.name;
                string pick = null;
                foreach (var (pk, nk, id) in Rules)
                {
                    if (path.IndexOf(pk, StringComparison.Ordinal) < 0) continue;
                    if (nk != null && name.IndexOf(nk, StringComparison.Ordinal) < 0) continue;
                    pick = id;
                    break;
                }
                if (pick == null)
                {
                    unmatched++;
                    if (unmatchedSample.Count < 12) unmatchedSample.Add(path);
                    continue;
                }

                if (!dry)
                {
                    var target = get(pick);
                    var next = new Material[mats.Length == 0 ? 1 : mats.Length];
                    for (int i = 0; i < next.Length; i++)
                    {
                        var cur = i < mats.Length ? mats[i] : null;
                        bool hasTex = cur != null &&
                            ((cur.HasProperty("_BaseMap") && cur.GetTexture("_BaseMap") != null)
                          || (cur.HasProperty("_MainTex") && cur.GetTexture("_MainTex") != null));
                        next[i] = hasTex ? cur : target;
                    }
                    mr.sharedMaterials = next;
                }
                applied[pick] = applied.TryGetValue(pick, out var c) ? c + 1 : 1;
                touched++;
            }
        }

        if (!dry)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        var sb = new StringBuilder();
        sb.Append("dry=").Append(dry).Append(" touched=").Append(touched)
          .Append(" alreadyTextured=").Append(skipped).Append(" unmatched=").Append(unmatched).Append("\n");
        foreach (var kv in applied.OrderByDescending(k => k.Value))
            sb.Append("  ").Append(kv.Key).Append(" x").Append(kv.Value).Append("\n");
        foreach (var u in unmatchedSample) sb.Append("  ? ").Append(u).Append("\n");
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/fill-materials.txt"), sb.ToString());
        Debug.Log("FILL_MATERIALS " + sb.ToString());
    }

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        var cur = t.parent;
        while (cur != null) { sb.Insert(0, cur.name + "/"); cur = cur.parent; }
        return sb.ToString();
    }
}
