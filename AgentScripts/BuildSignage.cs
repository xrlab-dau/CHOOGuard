// SignBaker 의 사인 배치(chooguard 사인 스펙)를 씬에 짓는다.
// 종류: tenantFascia(점포 간판) / wayfinding(유도사인) / emergencyExit(비상구 유도등) /
//       platformNumber(타는곳 번호) / departureBoard(행선안내)
//
// 점포 간판은 기존 "<점포명> 간판" 박스의 전면에 텍스처 쿼드를 덧댄다.
// 나머지는 케이스 + 양면 쿼드로 새로 만든다. 비상구 유도등은 이미시브 녹색이다.
//
// args: [signsPlacementJson, textureAssetDir]

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class BuildSignage
{
    const string RootName = "부산역 역사 내부 · 사인";
    const string MatDir = "Assets/ChooGuard/Art/StationInterior/Materials/Signs/";

    static float F(JToken t, float d = 0f) => t == null || t.Type == JTokenType.Null ? d : (float)t;

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/generated/signs/signs-placement.json";
        string texDir = args != null && args.Length > 1 && !string.IsNullOrEmpty(args[1])
            ? args[1] : "Assets/ChooGuard/Art/StationInterior/Textures/Signs/";
        if (!texDir.EndsWith("/")) texDir += "/";

        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Directory.CreateDirectory(Path.GetFullPath(MatDir));

        var frameMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/ChooGuard/Art/StationInterior/Materials/PBR_Metal032_2K.mat");

        var matCache = new Dictionary<string, Material>();
        Func<string, bool, Material> getMat = (texName, emissive) =>
        {
            string key = texName + (emissive ? "|E" : "");
            if (matCache.TryGetValue(key, out var c)) return c;
            string baseName = Path.GetFileNameWithoutExtension(texName);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + baseName + ".png");
            string mp = MatDir + "sign_" + baseName + (emissive ? "_e" : "") + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(emissive ? lit : unlit); AssetDatabase.CreateAsset(m, mp); }
            m.shader = emissive ? lit : unlit;
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (tex != null) m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white * 2.4f);
            }
            EditorUtility.SetDirty(m);
            matCache[key] = m;
            return m;
        };

        var old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        var byKind = new Dictionary<string, Transform>();
        var counts = new Dictionary<string, int>();
        int made = 0, missingTex = 0;
        var missingSample = new List<string>();

        foreach (JObject p in (JArray)spec["placements"])
        {
            string kind = (string)p["kind"] ?? "wayfinding";
            string texName = (string)p["texture"] ?? "";
            string baseName = Path.GetFileNameWithoutExtension(texName);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + baseName + ".png");
            if (tex == null)
            {
                missingTex++;
                if (missingSample.Count < 8) missingSample.Add(texName);
                continue;
            }

            if (!byKind.TryGetValue(kind, out var parent))
            {
                var g = new GameObject(kind);
                g.transform.SetParent(root.transform, false);
                parent = g.transform;
                byKind[kind] = parent;
            }

            bool emissive = kind == "emergencyExit" || kind == "departureBoard";
            var mat = getMat(texName, emissive);

            var go = new GameObject((string)p["name"]);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(F(p["pos"][0]), F(p["pos"][1]), F(p["pos"][2]));
            go.transform.rotation = Quaternion.Euler(0f, F(p["rotY"]), 0f);

            float w = F(p["size"][0], 1f), h = F(p["size"][1], 1f);
            bool twoSided = p["doubleSided"] != null && (bool)p["doubleSided"];

            // 케이스 (점포 간판은 기존 박스가 있으므로 생략)
            if (kind != "tenantFascia")
            {
                var box = new GameObject("케이스");
                box.transform.SetParent(go.transform, false);
                box.transform.localScale = new Vector3(w + 0.08f, h + 0.08f, 0.10f);
                box.AddComponent<MeshFilter>().sharedMesh = cube;
                var bmr = box.AddComponent<MeshRenderer>();
                if (frameMat != null) bmr.sharedMaterial = frameMat;
                bmr.shadowCastingMode = ShadowCastingMode.Off;
            }

            int faces = twoSided ? 2 : 1;
            for (int s = 0; s < faces; s++)
            {
                var q = new GameObject(s == 0 ? "면 앞" : "면 뒤");
                q.transform.SetParent(go.transform, false);
                q.transform.localPosition = new Vector3(0f, 0f, s == 0 ? -0.06f : 0.06f);
                q.transform.localRotation = Quaternion.Euler(0f, s == 0 ? 0f : 180f, 0f);
                q.transform.localScale = new Vector3(w, h, 1f);
                q.AddComponent<MeshFilter>().sharedMesh = quad;
                var qmr = q.AddComponent<MeshRenderer>();
                qmr.sharedMaterial = mat;
                qmr.shadowCastingMode = ShadowCastingMode.Off;
                qmr.receiveShadows = false;
            }

            // 비상구 유도등은 약한 녹색 광원을 하나 단다
            if (kind == "emergencyExit")
            {
                var lgo = new GameObject("유도등 광원");
                lgo.transform.SetParent(go.transform, false);
                lgo.transform.localPosition = new Vector3(0f, -0.25f, 0f);
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.55f, 1f, 0.65f);
                l.intensity = 0.35f;
                l.range = 2.2f;
                l.shadows = LightShadows.None;
            }

            counts[kind] = counts.TryGetValue(kind, out var cc) ? cc + 1 : 1;
            made++;
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        var sb = new System.Text.StringBuilder();
        sb.Append("made=").Append(made).Append(" missingTexture=").Append(missingTex).Append("\n");
        foreach (var kv in counts) sb.Append("  ").Append(kv.Key).Append(" x").Append(kv.Value).Append("\n");
        foreach (var m in missingSample) sb.Append("  ? ").Append(m).Append("\n");
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/signage-build.txt"), sb.ToString());
        Debug.Log("SIGNAGE " + sb);
    }
}
