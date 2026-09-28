// 천장 조명기구(이미시브 판 + 광원)를 짓는다. 기존 실내조명 포인트라이트 그룹은 대체한다.
// args: [luminairePlanJson]

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class BuildLuminaires
{
    const string MatPath = "Assets/ChooGuard/Art/StationInterior/Materials/조명기구_발광.mat";
    const string RootName = "부산역 역사 내부 · 조명기구";

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/luminaire-plan.json";
        var plan = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        // 발광 머티리얼
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.shader = lit;
        mat.SetColor("_BaseColor", new Color(1f, 0.98f, 0.94f, 1f));
        mat.SetFloat("_Smoothness", 0.2f);
        mat.SetFloat("_Metallic", 0f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        // 4000K 주백색, HDR 강도 3.2
        mat.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.88f) * 3.2f);
        EditorUtility.SetDirty(mat);

        var old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RootName);

        // 기존 실내조명 그룹 제거 (포인트라이트 500)
        int removed = 0;
        var finish = GameObject.Find("부산역 역사 내부 · 마감");
        if (finish != null)
        {
            foreach (Transform lvl in finish.transform)
            {
                var t = lvl.Find("실내조명");
                if (t != null) { removed += t.childCount; UnityEngine.Object.DestroyImmediate(t.gameObject); }
            }
        }

        var quad = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var byLevel = new Dictionary<string, Transform>();
        int made = 0, lights = 0;

        foreach (JObject f in (JArray)plan["fixtures"])
        {
            string lid = (string)f["level"];
            if (!byLevel.TryGetValue(lid, out var parent))
            {
                var g = new GameObject(lid);
                g.transform.SetParent(root.transform, false);
                parent = g.transform;
                byLevel[lid] = parent;
            }

            var go = new GameObject((string)f["name"]);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((float)f["pos"][0], (float)f["pos"][1], (float)f["pos"][2]);
            go.transform.rotation = Quaternion.Euler(0f, (float)f["rotY"], 0f);
            go.transform.localScale = new Vector3((float)f["size"][0], (float)f["size"][1], (float)f["size"][2]);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            made++;

            if ((bool)f["light"])
            {
                var lgo = new GameObject("광원");
                lgo.transform.SetParent(go.transform, false);
                lgo.transform.localPosition = new Vector3(0f, -1.0f, 0f);
                lgo.transform.localScale = Vector3.one;
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.96f, 0.90f);
                l.intensity = 4.5f;
                l.range = 12f;
                l.shadows = LightShadows.None;
                lights++;
            }
        }

        // 그림자 있는 대표 광원 4개 (L축 리얼리즘 플래그)
        int shadowed = 0;
        foreach (var kv in byLevel)
        {
            foreach (Transform child in kv.Value)
            {
                var l = child.GetComponentInChildren<Light>();
                if (l == null) continue;
                l.shadows = LightShadows.Soft;
                l.intensity = 6.0f;
                l.range = 18f;
                shadowed++;
                break;
            }
        }

        // 앰비언트 3색 (실내 기준)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.56f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.40f, 0.41f, 0.43f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.21f, 0.20f);

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/luminaire-build.txt"),
            "fixtures=" + made + " lights=" + lights + " shadowed=" + shadowed + " removedOldLights=" + removed);
        Debug.Log("LUMINAIRES fixtures=" + made + " lights=" + lights + " shadowed=" + shadowed + " removed=" + removed);
    }
}
