// 행선안내 전광판을 대합실 천장에 매단다.
// 판은 양면(앞·뒤 쿼드 2장)이며, 장축(16.2°) 직교 방향을 향한다.
// args: [boardsJson]  — {boards:[{name,level,pos,size,yaw}]}

using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class PlaceDepartureBoards
{
    const string MatPath = "Assets/ChooGuard/Art/StationInterior/Materials/행선안내_전광판.mat";
    const string TexPath = "Assets/ChooGuard/Art/StationInterior/Textures/DepartureBoard_2F.png";
    const string RootName = "부산역 역사 내부 · 행선안내";

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/board-placements.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        if (tex == null) throw new InvalidOperationException("전광판 텍스처 없음: " + TexPath);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (mat == null) { mat = new Material(unlit); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.shader = unlit;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);

        var frameMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/ChooGuard/Art/StationInterior/Materials/PBR_Metal032_2K.mat");

        var old = GameObject.Find(RootName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(RootName);

        int n = 0;
        foreach (JObject b in (JArray)spec["boards"])
        {
            var pos = new Vector3((float)b["pos"][0], (float)b["pos"][1], (float)b["pos"][2]);
            float bw = (float)b["size"][0], bh = (float)b["size"][1];
            float yaw = (float)b["yaw"];

            var go = new GameObject((string)b["name"]);
            go.transform.SetParent(root.transform, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // 케이스(두께 0.28m)
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "케이스";
            box.transform.SetParent(go.transform, false);
            box.transform.localScale = new Vector3(bw + 0.16f, bh + 0.16f, 0.28f);
            if (frameMat != null) box.GetComponent<MeshRenderer>().sharedMaterial = frameMat;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<BoxCollider>());

            // 표시면 앞·뒤
            for (int s = 0; s < 2; s++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = s == 0 ? "표시면 앞" : "표시면 뒤";
                q.transform.SetParent(go.transform, false);
                q.transform.localPosition = new Vector3(0f, 0f, s == 0 ? -0.16f : 0.16f);
                q.transform.localRotation = Quaternion.Euler(0f, s == 0 ? 0f : 180f, 0f);
                q.transform.localScale = new Vector3(bw, bh, 1f);
                q.GetComponent<MeshRenderer>().sharedMaterial = mat;
                UnityEngine.Object.DestroyImmediate(q.GetComponent<MeshCollider>());
            }

            // 행거 2본
            for (int s = -1; s <= 1; s += 2)
            {
                float drop = (float)b["hangerDrop"];
                if (drop <= 0.01f) continue;
                var rod = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rod.name = "행거";
                rod.transform.SetParent(go.transform, false);
                rod.transform.localPosition = new Vector3(s * bw * 0.36f, bh * 0.5f + drop * 0.5f, 0f);
                rod.transform.localScale = new Vector3(0.07f, drop, 0.07f);
                if (frameMat != null) rod.GetComponent<MeshRenderer>().sharedMaterial = frameMat;
                UnityEngine.Object.DestroyImmediate(rod.GetComponent<BoxCollider>());
            }
            n++;
        }

        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("DEPARTURE_BOARDS " + n);
        File.WriteAllText(Path.GetFullPath(".planning/2026-09-22-station-interior-build/board-build.txt"), "boards=" + n);
    }
}
