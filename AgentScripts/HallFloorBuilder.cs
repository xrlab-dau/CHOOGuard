// 부산역 2층 맞이방 바닥판 신축.
// 계측된 '지붕 아래 폐합 공동 중 바닥 없는 셀'(shell-void-y12.json)만을 좌표 권위로 쓴다.
// 셀 격자를 하나의 병합 메시로 만들어 .asset 으로 저장한다(씬 비대화 방지).
//
// args: [voidJson, outRoot, floorY, uMin, uMax, wellU0, wellU1, wellV0, wellV1]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class HallFloorBuilder
{
    const float ThetaDeg = 16.2f;
    const string GenDir = "Assets/ChooGuard/Art/StationInterior/Generated";
    public const string RootName = "부산역 2층 맞이방 · 신축";

    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        string voidJson = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/shell-void-y12.json";
        float floorY = P(args, 1, 7.0f);
        float uMin = P(args, 2, -60f), uMax = P(args, 3, 52f);
        float wellU0 = P(args, 4, 999f), wellU1 = P(args, 5, 999f);
        float wellV0 = P(args, 6, 999f), wellV1 = P(args, 7, 999f);
        float step = 2f;

        float th = ThetaDeg * Mathf.Deg2Rad, s = Mathf.Sin(th), c = Mathf.Cos(th);

        var text = File.ReadAllText(Path.GetFullPath(voidJson));
        var cells = ParseCells(text);
        var keep = new List<Vector2>();
        foreach (var cell in cells)
        {
            float x = cell.x, z = cell.y, fy = cell.z;
            if (fy > 5.0f) continue;                      // 이미 바닥이 있는 곳은 건드리지 않는다
            float u = x * s + z * c, v = x * c - z * s;
            if (u < uMin || u > uMax) continue;
            if (u >= wellU0 && u <= wellU1 && v >= wellV0 && v <= wellV1) continue;  // 에스컬레이터 보이드
            keep.Add(new Vector2(x, z));
        }

        if (keep.Count == 0) { Debug.LogError("HALL_FLOOR 대상 셀 0"); return; }

        var verts = new List<Vector3>(keep.Count * 4);
        var uvs = new List<Vector2>(keep.Count * 4);
        var norms = new List<Vector3>(keep.Count * 4);
        var tris = new List<int>(keep.Count * 6);
        float h = step * 0.5f, tile = 0.9f;             // 900mm 대형 화강석
        foreach (var p in keep)
        {
            int bi = verts.Count;
            verts.Add(new Vector3(p.x - h, floorY, p.y - h));
            verts.Add(new Vector3(p.x - h, floorY, p.y + h));
            verts.Add(new Vector3(p.x + h, floorY, p.y + h));
            verts.Add(new Vector3(p.x + h, floorY, p.y - h));
            for (int i = 0; i < 4; i++) norms.Add(Vector3.up);
            uvs.Add(new Vector2((p.x - h) / tile, (p.y - h) / tile));
            uvs.Add(new Vector2((p.x - h) / tile, (p.y + h) / tile));
            uvs.Add(new Vector2((p.x + h) / tile, (p.y + h) / tile));
            uvs.Add(new Vector2((p.x + h) / tile, (p.y - h) / tile));
            tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
            tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 3);
        }

        var mesh = new Mesh { name = "HallFloor2F" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        Directory.CreateDirectory(GenDir);
        string meshPath = GenDir + "/HallFloor2F.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        var mat = BuildFloorMaterial();

        var root = GameObject.Find(RootName) ?? new GameObject(RootName);
        var old = root.transform.Find("바닥 · 광택화강석");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("바닥 · 광택화강석");
        go.transform.SetParent(root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        var b = mesh.bounds;
        Debug.Log("HALL_FLOOR cells=" + keep.Count + " area=" + (keep.Count * step * step).ToString("F0")
                  + "m2 verts=" + verts.Count + " bounds=" + b.min.ToString("F1") + ".." + b.max.ToString("F1"));
    }

    static Material BuildFloorMaterial()
    {
        const string matPath = GenDir + "/Hall_Floor_PolishedGranite.mat";
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(sh);
            Directory.CreateDirectory(GenDir);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = sh;

        var alb = FindTex(new[]{
            "Assets/ChooGuard/Art/StationInterior/Textures/Lane/floor_station_polished.png",
            "Assets/ChooGuard/Art/StationInterior/PBR/Granite005A_2K/Granite005A_2K-JPG_Color.jpg"});
        var nrm = FindTex(new[]{
            "Assets/ChooGuard/Art/StationInterior/Textures/Lane/floor_station_polished_n.png",
            "Assets/ChooGuard/Art/StationInterior/PBR/Granite005A_2K/Granite005A_2K-JPG_NormalGL.jpg"});

        if (alb != null) mat.SetTexture("_BaseMap", alb);
        if (nrm != null) { mat.SetTexture("_BumpMap", nrm); mat.EnableKeyword("_NORMALMAP"); }
        mat.SetColor("_BaseColor", new Color(0.93f, 0.92f, 0.89f, 1f));
        mat.SetFloat("_Smoothness", 0.88f);      // 영상의 거울급 반사
        mat.SetFloat("_Metallic", 0.0f);
        mat.SetFloat("_SpecularHighlights", 1f);
        mat.SetFloat("_EnvironmentReflections", 1f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture FindTex(string[] candidates)
    {
        foreach (var p in candidates)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture>(p);
            if (t != null) return t;
        }
        // 폴더 검색 폴백
        foreach (var guid in AssetDatabase.FindAssets("Granite005A t:Texture"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.Contains("Color")) return AssetDatabase.LoadAssetAtPath<Texture>(p);
        }
        return null;
    }

    // [x,z,floorY,roofY,dist] 배열만 뽑는 최소 파서
    static List<Vector3> ParseCells(string json)
    {
        var res = new List<Vector3>();
        int k = json.IndexOf("\"cells\"", StringComparison.Ordinal);
        if (k < 0) return res;
        int i = json.IndexOf('[', k) + 1;
        while (i < json.Length)
        {
            int a = json.IndexOf('[', i);
            if (a < 0) break;
            int b = json.IndexOf(']', a);
            if (b < 0) break;
            var parts = json.Substring(a + 1, b - a - 1).Split(',');
            if (parts.Length >= 3)
                res.Add(new Vector3(
                    float.Parse(parts[0], CultureInfo.InvariantCulture),
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture)));
            i = b + 1;
            int close = json.IndexOf(']', i);
            int nextOpen = json.IndexOf('[', i);
            if (nextOpen < 0 || (close >= 0 && close < nextOpen)) break;
        }
        return res;
    }
}
