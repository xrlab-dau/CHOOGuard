// 특정 층만 남기고 숨긴다 / 되돌린다. 천장 포함 여부를 고른다.
// args: ["hide"|"show", level(예 "2F"), hideCeiling("1"|"0")]

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class LevelIsolate
{
    const string Key = "__levelIsolate";

    static readonly string[] ShellRoots = {
        "공식 자료 부산역 역사", "공식 자료 부산역 주변 공간",
        "Metro113Source", "KTXSource", "Metro113RecoveredRoofCentre"
    };

    public static void Main(string[] args)
    {
        string mode = args != null && args.Length > 0 ? args[0] : "hide";
        string level = args != null && args.Length > 1 ? args[1] : "2F";
        bool hideCeiling = args == null || args.Length < 3 || args[2] == "1";

        var world = GameObject.Find("FPSWorld");
        var interior = GameObject.Find("부산역 역사 내부");
        var finish = GameObject.Find("부산역 역사 내부 · 마감");
        var reuse = GameObject.Find("부산역 역사 내부 · 재사용 자산");

        if (mode == "show")
        {
            int n = 0;
            foreach (var p in EditorPrefs.GetString(Key, "").Split('~'))
            {
                if (string.IsNullOrEmpty(p)) continue;
                var go = FindByPath(p);
                if (go != null) { go.SetActive(true); n++; }
            }
            EditorPrefs.SetString(Key, "");
            Debug.Log("LEVELISO restored=" + n);
            return;
        }

        var hidden = new List<string>();
        Action<GameObject> hide = g => { if (g != null && g.activeSelf) { g.SetActive(false); hidden.Add(PathOf(g.transform)); } };

        if (world != null)
            foreach (Transform t in world.transform)
                foreach (var s in ShellRoots)
                    if (t.name == s) hide(t.gameObject);

        var pv = GameObject.Find("__preview");
        if (pv != null) hide(pv);

        if (hideCeiling)
            foreach (var n in new[] { "부산역 역사 내부 · 조명기구", "부산역 역사 내부 · 중앙홀 지붕" })
            {
                var g = GameObject.Find(n);
                if (g != null) hide(g);
            }

        if (interior != null)
            foreach (Transform t in interior.transform)
            {
                if (!t.name.StartsWith(level, StringComparison.Ordinal)) { hide(t.gameObject); continue; }
                if (hideCeiling)
                    foreach (Transform c in t)
                        if (c.name == "천장") hide(c.gameObject);
            }

        if (finish != null)
            foreach (Transform t in finish.transform)
                if (t.name != level) hide(t.gameObject);

        if (reuse != null)
            foreach (Transform t in reuse.transform)
                if (!t.name.StartsWith(level, StringComparison.Ordinal)) hide(t.gameObject);

        EditorPrefs.SetString(Key, string.Join("~", hidden.ToArray()));
        Debug.Log("LEVELISO hidden=" + hidden.Count);
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    static GameObject FindByPath(string path)
    {
        var parts = path.Split('/');
        GameObject cur = null;
        foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == parts[0]) { cur = g; break; }
        for (int i = 1; i < parts.Length && cur != null; i++)
        {
            Transform next = null;
            foreach (Transform c in cur.transform) if (c.name == parts[i]) { next = c; break; }
            cur = next != null ? next.gameObject : null;
        }
        return cur;
    }
}
