// 부산역 내부 마감·실 구획 생성기.
// 파이썬이 로컬 축(장축 컴퍼스 16.2°) 위에서 배치를 계산해 월드 좌표 박스 목록으로 넘긴다.
// 여기서는 박스를 만들고 재질을 붙이고 콜라이더를 유지하는 일만 한다. 판단은 하지 않는다.
//
// 멱등하다. args: [fitoutJsonPath]

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class StationFitoutBuilder
{
    const string RootName = "부산역 역사 내부 · 마감";
    const string AssetDir = "Assets/ChooGuard/Art/StationInterior";

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/station-interior-fitout.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        EnsureFolder(AssetDir); EnsureFolder(AssetDir + "/Materials");
        var mats = new Dictionary<string, Material>
        {
            { "wall",       Mat("벽_내장",     new Color(0.90f, 0.89f, 0.86f), 0.25f) },
            { "glass",      Mat("유리_커튼월", new Color(0.62f, 0.76f, 0.82f), 0.85f) },
            { "counter",    Mat("카운터_목재", new Color(0.52f, 0.38f, 0.26f), 0.30f) },
            { "gate",       Mat("개찰기_금속", new Color(0.35f, 0.37f, 0.40f), 0.70f) },
            { "kiosk",      Mat("발매기_외장", new Color(0.18f, 0.32f, 0.52f), 0.55f) },
            { "sign",       Mat("사인_청색",   new Color(0.05f, 0.24f, 0.47f), 0.40f) },
            { "escalator",  Mat("에스컬레이터", new Color(0.44f, 0.46f, 0.48f), 0.65f) },
            { "locker",     Mat("보관함",      new Color(0.30f, 0.44f, 0.36f), 0.35f) },
            { "shutter",    Mat("방화셔터",    new Color(0.72f, 0.18f, 0.12f), 0.30f) },
        };

        var existing = GameObject.Find(RootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var root = new GameObject(RootName);
        var world = GameObject.Find("FPSWorld");
        if (world != null) root.transform.SetParent(world.transform, false);

        var groups = new Dictionary<string, Transform>();
        int n = 0;
        foreach (JObject b in (JArray)spec["boxes"])
        {
            string level = (string)b["level"];
            string group = (string)b["group"];
            string key = level + "/" + group;
            if (!groups.TryGetValue(key, out var parent))
            {
                if (!groups.TryGetValue(level, out var lvl))
                {
                    var lg = new GameObject(level);
                    lg.transform.SetParent(root.transform, false);
                    lvl = lg.transform;
                    groups[level] = lvl;
                }
                var gg = new GameObject(group);
                gg.transform.SetParent(lvl, false);
                parent = gg.transform;
                groups[key] = parent;
            }

            var pos = (JArray)b["pos"];
            var size = (JArray)b["size"];
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = (string)b["name"];
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((float)pos[0], (float)pos[1], (float)pos[2]);
            go.transform.localScale = new Vector3((float)size[0], (float)size[1], (float)size[2]);
            var rotJ = b["rot"] as JArray;
            go.transform.rotation = rotJ != null
                ? Quaternion.Euler((float)rotJ[0], (float)rotJ[1], (float)rotJ[2])
                : Quaternion.Euler(0f, (float)b["rotY"], 0f);
            n++;
        }

        var lightsJ = spec["lights"] as JArray;
        int lights = 0;
        if (lightsJ != null)
        {
            foreach (JObject l in lightsJ)
            {
                string level = (string)l["level"];
                string key = level + "/실내조명";
                if (!groups.TryGetValue(key, out var parent))
                {
                    if (!groups.TryGetValue(level, out var lvl))
                    {
                        var lg = new GameObject(level);
                        lg.transform.SetParent(root.transform, false);
                        lvl = lg.transform; groups[level] = lvl;
                    }
                    var gg = new GameObject("실내조명");
                    gg.transform.SetParent(lvl, false);
                    parent = gg.transform; groups[key] = parent;
                }
                var p = (JArray)l["pos"];
                var c = (JArray)l["color"];
                var go = new GameObject((string)l["name"]);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3((float)p[0], (float)p[1], (float)p[2]);
                var li = go.AddComponent<Light>();
                li.type = LightType.Point;
                li.range = (float)l["range"];
                li.intensity = (float)l["intensity"];
                li.color = new Color((float)c[0], (float)c[1], (float)c[2]);
                li.shadows = LightShadows.None;
                lights++;
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("FITOUT_BUILD boxes=" + n + " lights=" + lights + " groups=" + groups.Count);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material Mat(string name, Color c, float smooth)
    {
        string p = AssetDir + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, p);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }
}
