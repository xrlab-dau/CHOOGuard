// 부산역 역사 내부 구조체 생성기 (사각형 분할 방식).
//
// 공식 모델의 역사 외피(9,301㎡)에는 층 슬래브가 파편적으로만 존재한다.
// 실측 레벨: 1F FL 0.00 / 천장 4.75, 2F FL 7.00 / 천장 11.00, 3F FL 12.00.
// 2F FL 7.00 은 과선교 데크 상면과 같은 레벨이며, 데크 서단 x=-8.8 이 z=-48 동측벽에 접합한다.
//
// 슬래브는 파이썬이 마스크에서 탐욕적 병합한 사각형 목록으로 들어온다. 원본에 이미 바닥이
// 있는 영역은 제외되어 있으므로 면이 겹치지 않는다.
//
// 멱등하다. args: [specJsonPath]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class StationInteriorBuilder
{
    const string RootName = "부산역 역사 내부";
    const string AssetDir = "Assets/ChooGuard/Art/StationInterior";

    public static void Main(string[] args)
    {
        string specPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/station-interior-spec.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(specPath)));

        EnsureFolder(AssetDir);
        EnsureFolder(AssetDir + "/Materials");

        var matFloor = Mat("바닥_화강석", new Color(0.70f, 0.68f, 0.64f), 0.35f);
        var matColumn = Mat("기둥_마감", new Color(0.88f, 0.87f, 0.84f), 0.45f);

        var existing = GameObject.Find(RootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var root = new GameObject(RootName);
        var world = GameObject.Find("FPSWorld");
        if (world != null) root.transform.SetParent(world.transform, true);

        var fpArr = (JArray)spec["footprint"];
        var poly = new List<Vector2>();
        foreach (var v in fpArr) poly.Add(new Vector2((float)v[0], (float)v[1]));

        var gridJ = (JObject)spec["structuralGrid"];
        float bearing = (float)gridJ["bearingDeg"];
        float gLong = (float)gridJ["spacingAlongBuilding"];
        float gCross = (float)gridJ["spacingAcrossBuilding"];
        float colSize = (float)gridJ["columnSize"];
        float rad = bearing * Mathf.Deg2Rad;
        var axisLong = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        var axisCross = new Vector2(axisLong.y, -axisLong.x);
        var centre = Centroid(poly);

        int slabCount = 0, colCount = 0;
        double slabArea = 0;

        foreach (JObject L in (JArray)spec["levels"])
        {
            string id = (string)L["id"];
            float floorY = (float)L["floorY"];
            float thick = (float)L["slabThickness"];
            float ceilY = (float)L["ceilingY"];

            var levelGo = new GameObject(id + " · " + (string)L["korean"]);
            levelGo.transform.SetParent(root.transform, false);

            var slabParent = new GameObject("바닥 슬래브");
            slabParent.transform.SetParent(levelGo.transform, false);
            foreach (JArray r in (JArray)L["slabRects"])
            {
                float x0 = (float)r[0], z0 = (float)r[1], x1 = (float)r[2], z1 = (float)r[3];
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "슬래브 " + id + " " + x0.ToString("F0") + "," + z0.ToString("F0");
                go.transform.SetParent(slabParent.transform, false);
                go.transform.position = new Vector3((x0 + x1) * 0.5f, floorY - thick * 0.5f, (z0 + z1) * 0.5f);
                go.transform.localScale = new Vector3(x1 - x0, thick, z1 - z0);
                go.GetComponent<MeshRenderer>().sharedMaterial = matFloor;
                slabCount++;
                slabArea += (x1 - x0) * (z1 - z0);
            }

            var colParent = new GameObject("기둥 격자");
            colParent.transform.SetParent(levelGo.transform, false);
            int nL = Mathf.CeilToInt(220f / gLong), nC = Mathf.CeilToInt(140f / gCross);
            for (int i = -nL; i <= nL; i++)
            for (int j = -nC; j <= nC; j++)
            {
                var p = centre + axisLong * (i * gLong) + axisCross * (j * gCross);
                if (!Inside(poly, p)) continue;
                if (DistToEdge(poly, p) < 2.0f) continue;
                var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
                col.name = "기둥 " + id + " " + i + "_" + j;
                col.transform.SetParent(colParent.transform, false);
                col.transform.position = new Vector3(p.x, (floorY + ceilY) * 0.5f, p.y);
                col.transform.rotation = Quaternion.Euler(0f, bearing, 0f);
                col.transform.localScale = new Vector3(colSize, ceilY - floorY, colSize);
                col.GetComponent<MeshRenderer>().sharedMaterial = matColumn;
                colCount++;
            }

            // 천장: 실측 천장고에 시스템 천장판을 단다 (1F 4.75 / 2F 11.00 / 3F 16.50)
            var ceilParent = new GameObject("천장");
            ceilParent.transform.SetParent(levelGo.transform, false);
            foreach (JArray r in (JArray)L["slabRects"])
            {
                float x0 = (float)r[0], z0 = (float)r[1], x1 = (float)r[2], z1 = (float)r[3];
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "천장 " + id + " " + x0.ToString("F0") + "," + z0.ToString("F0");
                go.transform.SetParent(ceilParent.transform, false);
                go.transform.position = new Vector3((x0 + x1) * 0.5f, ceilY + 0.04f, (z0 + z1) * 0.5f);
                go.transform.localScale = new Vector3(x1 - x0, 0.08f, z1 - z0);
                UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
                go.GetComponent<MeshRenderer>().sharedMaterial = matColumn;
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("INTERIOR_BUILD slabs=" + slabCount + " area=" + slabArea.ToString("F0") +
                  "m2 columns=" + colCount);
    }

    static Vector2 Centroid(List<Vector2> p)
    {
        var c = Vector2.zero;
        foreach (var v in p) c += v;
        return c / p.Count;
    }

    static bool Inside(List<Vector2> p, Vector2 q)
    {
        bool c = false;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            if (((p[i].y > q.y) != (p[j].y > q.y)) &&
                (q.x < (p[j].x - p[i].x) * (q.y - p[i].y) / (p[j].y - p[i].y) + p[i].x)) c = !c;
        return c;
    }

    static float DistToEdge(List<Vector2> p, Vector2 q)
    {
        float best = float.MaxValue;
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count];
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
        }
        return best;
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
