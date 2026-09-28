// 2층 중앙 대합실의 평천장을 걷어내고 노출 스페이스프레임 + 유리지붕(주광)을 짓는다.
// ReferenceHarvest 관측: "2F 중앙은 시스템천장이 아니라 노출 강관 스페이스프레임(#8A8B8D)
// + 뒤쪽 어두운 금속데크(#3A3C3E) + 유리 랜턴 톱라이트. p95Y 0.85 로 2F가 가장 밝음."
//
// args: [vMin, vMax, bottomChordY, topChordY, glazingY, bayM]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static class BuildAtriumRoof
{
    const string RootName = "부산역 역사 내부 · 중앙홀 지붕";
    const string MatDir = "Assets/ChooGuard/Art/StationInterior/Materials/Lane/";
    const float TH = 16.2f * Mathf.Deg2Rad;

    static Vector2 ToUV(float x, float z) => new Vector2(x * Mathf.Sin(TH) + z * Mathf.Cos(TH),
                                                         x * Mathf.Cos(TH) - z * Mathf.Sin(TH));
    static Vector3 ToXZ(float u, float v, float y) => new Vector3(u * Mathf.Sin(TH) + v * Mathf.Cos(TH), y,
                                                                  u * Mathf.Cos(TH) - v * Mathf.Sin(TH));
    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        float vMin = P(args, 0, -44f), vMax = P(args, 1, 4f);
        float botY = P(args, 2, 12.6f), topY = P(args, 3, 15.2f);
        float glzY = P(args, 4, 15.9f), bay = P(args, 5, 7.2f);
        float uLo = P(args, 6, -999f), uHi = P(args, 7, 999f);

        // ---- 1) 중앙 밴드 천장 타일 제거 ----
        int removed = 0;
        var interior = GameObject.Find("부산역 역사 내부");
        var lvl2 = interior != null ? interior.transform.Find("2F · 지상 2층") : null;
        var ceil = lvl2 != null ? lvl2.Find("천장") : null;
        float uMin = float.MaxValue, uMax = float.MinValue;
        if (ceil != null)
        {
            var kill = new List<GameObject>();
            foreach (Transform t in ceil)
            {
                var uv = ToUV(t.position.x, t.position.z);
                if (uv.y >= vMin && uv.y <= vMax && uv.x >= uLo && uv.x <= uHi)
                {
                    kill.Add(t.gameObject);
                    uMin = Mathf.Min(uMin, uv.x); uMax = Mathf.Max(uMax, uv.x);
                }
            }
            foreach (var g in kill) { UnityEngine.Object.DestroyImmediate(g); removed++; }
        }
        if (uMin > uMax) { uMin = -92.7f; uMax = 77.0f; }

        // ---- 2) 머티리얼 ----
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        Func<string, Color, float, float, float, Material> mk = (id, col, metal, smooth, emis) =>
        {
            string mp = MatDir + id + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, mp); }
            m.shader = lit;
            m.SetColor("_BaseColor", col);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            if (emis > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", new Color(0.92f, 0.95f, 1.0f) * emis);
            }
            EditorUtility.SetDirty(m);
            return m;
        };
        // 강관 트러스 #8A8B8D, 금속데크 #3A3C3E, 유리 랜턴(주광)
        var steel = mk("roof_steel_tube", new Color(0.541f, 0.545f, 0.553f), 0.85f, 0.42f, 0f);
        var deck = mk("roof_metal_deck", new Color(0.227f, 0.235f, 0.243f), 0.55f, 0.30f, 0f);
        var glass = mk("roof_daylight_glazing", new Color(0.86f, 0.90f, 0.95f), 0.0f, 0.55f, 2.6f);

        var old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        var trussRoot = new GameObject("스페이스프레임"); trussRoot.transform.SetParent(root.transform, false);
        var glzRoot = new GameObject("유리 랜턴"); glzRoot.transform.SetParent(root.transform, false);
        var deckRoot = new GameObject("금속데크"); deckRoot.transform.SetParent(root.transform, false);

        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        int tubes = 0, panels = 0;

        Action<Transform, string, Vector3, Vector3, float, Material> tube = (par, nm, a, b, d, m) =>
        {
            var go = new GameObject(nm);
            go.transform.SetParent(par, false);
            var mid = (a + b) * 0.5f;
            var dir = b - a;
            float len = dir.magnitude;
            if (len < 0.05f) { UnityEngine.Object.DestroyImmediate(go); return; }
            go.transform.position = mid;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            go.transform.localScale = new Vector3(d, d, len);
            go.AddComponent<MeshFilter>().sharedMesh = cube;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.On;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            tubes++;
        };

        float vSpan = vMax - vMin;
        int nodesV = Mathf.Max(3, Mathf.RoundToInt(vSpan / bay));
        float dv = vSpan / nodesV;

        for (float u = uMin; u <= uMax + 0.01f; u += bay)
        {
            // 횡단 트러스 1榀: 하현 + 상현 + 수직재 + 사재
            for (int i = 0; i < nodesV; i++)
            {
                float v0 = vMin + dv * i, v1 = vMin + dv * (i + 1);
                var b0 = ToXZ(u, v0, botY); var b1 = ToXZ(u, v1, botY);
                var t0 = ToXZ(u, v0, topY); var t1 = ToXZ(u, v1, topY);
                tube(trussRoot.transform, "하현", b0, b1, 0.22f, steel);
                tube(trussRoot.transform, "상현", t0, t1, 0.18f, steel);
                tube(trussRoot.transform, "수직재", b0, t0, 0.12f, steel);
                tube(trussRoot.transform, "사재", b0, t1, 0.10f, steel);
            }
            var bEnd = ToXZ(u, vMax, botY); var tEnd = ToXZ(u, vMax, topY);
            tube(trussRoot.transform, "수직재", bEnd, tEnd, 0.12f, steel);
        }

        // 종방향 연결재 (장축)
        for (int i = 0; i <= nodesV; i++)
        {
            float v = vMin + dv * i;
            tube(trussRoot.transform, "종방향 하현", ToXZ(uMin, v, botY), ToXZ(uMax, v, botY), 0.14f, steel);
            tube(trussRoot.transform, "종방향 상현", ToXZ(uMin, v, topY), ToXZ(uMax, v, topY), 0.12f, steel);
        }

        // ---- 3) 유리 랜턴 + 금속데크 ----
        // 중앙 1/3 은 유리(주광), 양옆은 어두운 금속데크
        float glzHalf = vSpan / 6f;
        float vc = (vMin + vMax) * 0.5f;
        Action<Transform, string, float, float, float, Material> slab = (par, nm, va, vb, y, m) =>
        {
            float step = 12f;
            for (float u = uMin; u < uMax; u += step)
            {
                float len = Mathf.Min(step, uMax - u);
                var go = new GameObject(nm);
                go.transform.SetParent(par, false);
                go.transform.position = ToXZ(u + len * 0.5f, (va + vb) * 0.5f, y);
                go.transform.rotation = Quaternion.Euler(0f, 16.2f, 0f);
                go.transform.localScale = new Vector3(len, 0.12f, vb - va);
                go.AddComponent<MeshFilter>().sharedMesh = cube;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = m;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                panels++;
            }
        };
        slab(glzRoot.transform, "유리 랜턴", vc - glzHalf, vc + glzHalf, glzY, glass);
        slab(deckRoot.transform, "금속데크 좌", vMin, vc - glzHalf, glzY + 0.3f, deck);
        slab(deckRoot.transform, "금속데크 우", vc + glzHalf, vMax, glzY + 0.3f, deck);

        // 랜턴 하부 주광 광원 (그림자 있는 대표 광원)
        int lights = 0;
        for (float u = uMin + 14f; u < uMax; u += 28f)
        {
            var lgo = new GameObject("랜턴 주광");
            lgo.transform.SetParent(glzRoot.transform, false);
            lgo.transform.position = ToXZ(u, vc, glzY - 1.0f);
            lgo.transform.rotation = Quaternion.Euler(90f, 16.2f, 0f);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 120f;
            l.innerSpotAngle = 70f;
            l.color = new Color(0.90f, 0.94f, 1.0f);
            l.intensity = 9f;
            l.range = 26f;
            l.shadows = LightShadows.Soft;
            lights++;
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        var sb = new StringBuilder();
        sb.Append("removedCeilingTiles=").Append(removed)
          .Append(" tubes=").Append(tubes).Append(" panels=").Append(panels).Append(" daylight=").Append(lights)
          .Append(" u=").Append(uMin.ToString("F1")).Append("..").Append(uMax.ToString("F1"))
          .Append(" v=").Append(vMin).Append("..").Append(vMax)
          .Append(" botY=").Append(botY).Append(" topY=").Append(topY).Append(" glzY=").Append(glzY);
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/atrium-roof.txt"), sb.ToString());
        Debug.Log("ATRIUM_ROOF " + sb);
    }
}
