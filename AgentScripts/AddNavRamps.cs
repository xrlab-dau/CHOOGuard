// 수직동선에 내비게이션 램프와 승강기 오프메시링크를 붙인다.
//
// 이유: 레거시 NavMesh 는 에이전트 반경만큼 보행면을 침식한다. 에스컬레이터 디딤판
// 진행길이 0.404m 는 반경 0.30m(직경 0.60m)에 완전히 잠식돼 보행면이 남지 않는다.
// 실측 결과 1F→2F 경로가 PathPartial 로 끊겼다. 그래서 계단면 위에 투명 경사판을 덮는다.
// 시각적으로는 원래 계단이 보이고, 경로 탐색만 경사판을 쓴다.
//
// 승강기는 경사로 대체가 불가능하므로 OffMeshLink 로 층간을 잇는다.
//
// args: [verticalCirculationJson]

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class AddNavRamps
{
    const string RootName = "부산역 역사 내부 · 내비게이션";
    const string MatPath = "Assets/ChooGuard/Art/StationInterior/Materials/내비_투명.mat";

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/vertical-circulation.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(sh);
            mat.SetFloat("_Surface", 1f);                 // Transparent
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", new Color(0f, 1f, 0.4f, 0.0f));
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        var existing = GameObject.Find(RootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var root = new GameObject(RootName);
        var world = GameObject.Find("FPSWorld");
        if (world != null) root.transform.SetParent(world.transform, false);

        int ramps = 0, links = 0;
        foreach (JObject u in (JArray)spec["units"])
        {
            string kind = (string)u["kind"];
            float y0 = (float)u["bottomY"], y1 = (float)u["topY"];
            float yaw = (float)u["yaw"];
            float w = (float)u["clearWidth"];
            var p = (JArray)u["pos"];
            var origin = new Vector3((float)p[0], y0, (float)p[2]);
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            if (kind == "elevator")
            {
                // 승강기: 각 정지층을 잇는 오프메시링크
                var stops = new List<float> { y0, 7.00f };
                if (y1 > 11.5f) stops.Add(12.00f);
                var door = origin + fwd * -0.9f;
                for (int i = 1; i < stops.Count; i++)
                {
                    if (stops[i] > y1 + 0.01f) continue;
                    var go = new GameObject((string)u["name"] + " 링크 " + stops[i - 1].ToString("F0") + "-" + stops[i].ToString("F0"));
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = new Vector3(door.x, stops[i - 1], door.z);
                    var sTr = new GameObject("start").transform; sTr.SetParent(go.transform, false);
                    sTr.position = new Vector3(door.x, stops[i - 1] + 0.05f, door.z);
                    var eTr = new GameObject("end").transform; eTr.SetParent(go.transform, false);
                    eTr.position = new Vector3(door.x, stops[i] + 0.05f, door.z);
                    var ol = go.AddComponent<UnityEngine.AI.OffMeshLink>();
                    ol.startTransform = sTr; ol.endTransform = eTr;
                    ol.biDirectional = true; ol.costOverride = 8f;
                    ol.area = 2;   // Jump 영역 — 별도 비용
                    links++;
                }
                continue;
            }

            // 경사 램프: 하부 착지판 끝에서 상부 착지판 시작까지
            float landing = kind == "escalator" ? 1.20f : 1.20f;
            float run = kind == "escalator"
                ? (y1 - y0) / Mathf.Tan(30f * Mathf.Deg2Rad)
                : (y1 - y0) / Mathf.Tan(30.3f * Mathf.Deg2Rad) + 2.4f;   // 계단은 계단참 포함 여유
            float rise = y1 - y0;
            float len = Mathf.Sqrt(run * run + rise * rise);
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

            var mid = origin + fwd * (landing + run * 0.5f) + Vector3.up * (rise * 0.5f + 0.12f);
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "내비 램프 · " + (string)u["name"];
            ramp.transform.SetParent(root.transform, false);
            ramp.transform.position = mid;
            ramp.transform.rotation = Quaternion.Euler(-ang, yaw, 0f);
            ramp.transform.localScale = new Vector3(w, 0.06f, len);
            ramp.GetComponent<MeshRenderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(ramp.GetComponent<BoxCollider>());
            GameObjectUtility.SetStaticEditorFlags(ramp, StaticEditorFlags.NavigationStatic);
            GameObjectUtility.SetNavMeshArea(ramp, 0);

            // 상·하부 착지판도 평탄 램프로 덮어 접속을 보장한다
            foreach (var (t, yy) in new[] { (0.5f * landing, y0), (landing + run + landing * 0.5f, y1) })
            {
                var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = "내비 착지판 · " + (string)u["name"];
                pad.transform.SetParent(root.transform, false);
                pad.transform.position = origin + fwd * t + Vector3.up * (yy - y0 + 0.10f);
                pad.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                pad.transform.localScale = new Vector3(w, 0.06f, landing + 0.6f);
                pad.GetComponent<MeshRenderer>().sharedMaterial = mat;
                UnityEngine.Object.DestroyImmediate(pad.GetComponent<BoxCollider>());
                GameObjectUtility.SetStaticEditorFlags(pad, StaticEditorFlags.NavigationStatic);
                GameObjectUtility.SetNavMeshArea(pad, 0);
            }
            ramps++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/navramps.txt"),
            "ramps=" + ramps + " offMeshLinks=" + links);
        Debug.Log("NAV_RAMPS ramps=" + ramps + " links=" + links);
    }
}
