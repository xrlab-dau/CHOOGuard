// 경면 연마 석재 바닥이 천장·간판을 반사하도록 리플렉션 프로브를 깔고 굽는다.
// ReferenceHarvest 관측: "바닥은 전 층 경면 연마 석재로 천장·간판이 길게 정반사. 반사 프로브 필수."
//
// args: [spacingM]

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static class PlaceReflectionProbes
{
    const string RootName = "부산역 역사 내부 · 반사 프로브";
    const float TH = 16.2f * Mathf.Deg2Rad;

    static Vector3 ToXZ(float u, float v, float y)
        => new Vector3(u * Mathf.Sin(TH) + v * Mathf.Cos(TH), y, u * Mathf.Cos(TH) - v * Mathf.Sin(TH));

    public static void Main(string[] args)
    {
        float spacing = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? float.Parse(args[0], CultureInfo.InvariantCulture) : 34f;

        var old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RootName);

        // (층, 바닥Y, 천장Y)
        var levels = new (string id, float fy, float cy)[] {
            ("1F", 0.00f, 4.75f), ("2F", 7.00f, 11.00f),
            ("3F", 12.00f, 16.50f), ("B1", -6.00f, -2.80f)
        };
        float uMin = -88f, uMax = 72f, vMin = -50f, vMax = 12f;

        int made = 0;
        foreach (var (id, fy, cy) in levels)
        {
            var g = new GameObject(id);
            g.transform.SetParent(root.transform, false);
            for (float u = uMin; u <= uMax; u += spacing)
                for (float v = vMin; v <= vMax; v += spacing)
                {
                    var go = new GameObject(string.Format("반사 프로브 {0} {1:F0},{2:F0}", id, u, v));
                    go.transform.SetParent(g.transform, false);
                    go.transform.position = ToXZ(u, v, fy + (cy - fy) * 0.45f);
                    var p = go.AddComponent<ReflectionProbe>();
                    p.mode = ReflectionProbeMode.Realtime;
                    p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                    p.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    p.resolution = 128;
                    p.size = new Vector3(spacing * 1.35f, (cy - fy) * 1.2f, spacing * 1.35f);
                    p.boxProjection = true;
                    p.intensity = 1f;
                    p.clearFlags = ReflectionProbeClearFlags.Skybox;
                    p.cullingMask = ~0;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic);
                    made++;
                }
        }

        // 한 번 렌더해 큐브맵을 채운다
        int rendered = 0;
        foreach (var p in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
        {
            if (p == null) continue;
            p.RenderProbe();
            rendered++;
        }
        Debug.Log("PROBES_RENDERED " + rendered);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/reflection-probes.txt"),
            "probes=" + made + " spacing=" + spacing);
        Debug.Log("REFLECTION_PROBES " + made);
    }
}
