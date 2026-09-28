// 실내 지오메트리를 Navigation Static 으로 표시한다. 레거시 NavMesh 베이크의 전제다.
// 보행 불가 요소(조명·사인·천장·셔터)는 제외하지 않는다 — 셔터는 평시 개방이지만
// 형상 자체는 장애물이므로 NotWalkable 로 지정해 경로에서 배제한다.
// args: [dummy]

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class PrepareNavStatic
{
    public static void Main(string[] args)
    {
        string[] roots =
        {
            "부산역 역사 내부", "부산역 역사 내부 · 마감",
            "부산역 역사 내부 · 수직동선", "부산역 역사 내부 · 재사용 자산",
            "공식 자료 부산역 역사", "공식 자료 부산역 주변 공간", "부산역 역사 내부 · 내비게이션"
        };
        int marked = 0, notWalkable = 0, skipped = 0;
        foreach (var rn in roots)
        {
            var g = GameObject.Find(rn);
            if (g == null) continue;
            foreach (var mr in g.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = mr.gameObject;
                string n = go.name;
                if (n.IndexOf("사인", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("표찰", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("피난유도등", StringComparison.Ordinal) >= 0)
                { skipped++; continue; }

                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.NavigationStatic);
                // 천장·셔터·사인은 걸을 수 없는 면으로 지정
                bool block = n.IndexOf("천장", StringComparison.Ordinal) >= 0 ||
                             n.IndexOf("방화셔터", StringComparison.Ordinal) >= 0;
                GameObjectUtility.SetNavMeshArea(go, block ? 1 : 0);   // 1 = Not Walkable
                if (block) notWalkable++;
                marked++;
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("NAVSTATIC marked=" + marked + " notWalkable=" + notWalkable + " skipped=" + skipped);
        System.IO.File.WriteAllText(
            System.IO.Path.GetFullPath(".planning/2026-09-22-station-interior-build/navstatic.txt"),
            "marked=" + marked + " notWalkable=" + notWalkable + " skipped=" + skipped);
    }
}
