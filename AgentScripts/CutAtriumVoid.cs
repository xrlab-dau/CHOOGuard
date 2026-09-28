// 2층 대합실 위로 아트리움 보이드를 낸다. 지정한 uv 밴드에서 대상 층의 바닥 슬래브·천장·기둥을 제거한다.
// 실사: 부산역 2층 대합실은 지붕 트러스까지 트여 있고 3층은 가장자리(푸드코트)에만 있다.
//
// args: [vMin, vMax, uMin, uMax, levelsCsv]   예: ["-44","4","-999","999","3F,4F"]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class CutAtriumVoid
{
    const float TH = 16.2f * Mathf.Deg2Rad;
    static Vector2 ToUV(float x, float z) => new Vector2(x * Mathf.Sin(TH) + z * Mathf.Cos(TH),
                                                         x * Mathf.Cos(TH) - z * Mathf.Sin(TH));
    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    static readonly string[] ChildGroups = { "바닥 슬래브", "천장", "기둥 격자" };

    public static void Main(string[] args)
    {
        float vMin = P(args, 0, -44f), vMax = P(args, 1, 4f);
        float uMin = P(args, 2, -999f), uMax = P(args, 3, 999f);
        string levels = args != null && args.Length > 4 && !string.IsNullOrEmpty(args[4]) ? args[4] : "3F,4F";

        var interior = GameObject.Find("부산역 역사 내부");
        if (interior == null) throw new InvalidOperationException("실내 루트 없음");

        var report = new StringBuilder();
        int total = 0;

        foreach (var lid in levels.Split(','))
        {
            string key = lid.Trim();
            Transform lvl = null;
            foreach (Transform t in interior.transform)
                if (t.name.StartsWith(key, StringComparison.Ordinal)) { lvl = t; break; }
            if (lvl == null) { report.Append(key).Append(": 층 없음\n"); continue; }

            foreach (var gname in ChildGroups)
            {
                var grp = lvl.Find(gname);
                if (grp == null) continue;
                var kill = new List<GameObject>();
                foreach (Transform t in grp)
                {
                    var uv = ToUV(t.position.x, t.position.z);
                    if (uv.y >= vMin && uv.y <= vMax && uv.x >= uMin && uv.x <= uMax)
                        kill.Add(t.gameObject);
                }
                foreach (var g in kill) UnityEngine.Object.DestroyImmediate(g);
                report.Append(key).Append('/').Append(gname).Append(" 제거 ").Append(kill.Count)
                      .Append(" / 잔여 ").Append(grp.childCount).Append('\n');
                total += kill.Count;
            }
        }

        // 마감 그룹(점포·화장실 등)도 보이드 안에 있으면 제거
        var finish = GameObject.Find("부산역 역사 내부 · 마감");
        if (finish != null)
            foreach (var lid in levels.Split(','))
            {
                string key = lid.Trim();
                var lvl = finish.transform.Find(key);
                if (lvl == null) continue;
                int n = 0;
                foreach (Transform grp in lvl)
                {
                    var kill = new List<GameObject>();
                    foreach (Transform t in grp)
                    {
                        var uv = ToUV(t.position.x, t.position.z);
                        if (uv.y >= vMin && uv.y <= vMax && uv.x >= uMin && uv.x <= uMax) kill.Add(t.gameObject);
                    }
                    foreach (var g in kill) UnityEngine.Object.DestroyImmediate(g);
                    n += kill.Count;
                }
                report.Append(key).Append("/마감 제거 ").Append(n).Append('\n');
                total += n;
            }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        report.Insert(0, "총 제거 " + total + "  band v[" + vMin + "," + vMax + "] u[" + uMin + "," + uMax + "] levels=" + levels + "\n");
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/atrium-void.txt"), report.ToString());
        Debug.Log("ATRIUM_VOID " + report);
    }
}
