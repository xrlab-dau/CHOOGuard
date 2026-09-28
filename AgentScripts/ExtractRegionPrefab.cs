// 원본 모델의 특정 구역 지오메트리를 그대로 떼어내 재사용 가능한 프리팹으로 만든다.
// 공식 부산역 모델은 8개 배치 메시라 에스컬레이터·계단이 개별 오브젝트가 아니다.
// 그래서 '이미 있는 것을 쓴다' 는 곧 '구역을 잘라 자산화한다' 가 된다. 새로 만드는 것이 아니다.
//
// args: [prefabPath, minX,minY,minZ, maxX,maxY,maxZ, rootName, pivot(bottomCentre|centre)]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEditor;

public static class ExtractRegionPrefab
{
    public static void Main(string[] args)
    {
        string prefabPath = Arg(args, 0, "Assets/ChooGuard/Art/StationInterior/Reused/Extracted.prefab");
        var mn = new Vector3(F(Arg(args, 1, "0")), F(Arg(args, 2, "0")), F(Arg(args, 3, "0")));
        var mx = new Vector3(F(Arg(args, 4, "1")), F(Arg(args, 5, "1")), F(Arg(args, 6, "1")));
        string rootName = Arg(args, 7, "MainShell");
        string pivotMode = Arg(args, 8, "bottomCentre");

        Transform root = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == rootName) { root = t; break; }
        if (root == null) throw new InvalidOperationException("루트 없음: " + rootName);

        var box = new Bounds((mn + mx) * 0.5f, mx - mn);
        var byMat = new Dictionary<Material, List<Vector3>>();
        var idxByMat = new Dictionary<Material, List<int>>();
        int triTotal = 0;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh; if (mesh == null) continue;
            var rend = mf.GetComponent<Renderer>(); if (rend == null) continue;
            if (!rend.bounds.Intersects(box)) continue;
            var verts = mesh.vertices;
            var l2w = mf.transform.localToWorldMatrix;
            var w = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) w[i] = l2w.MultiplyPoint3x4(verts[i]);
            var mats = rend.sharedMaterials;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                var mat = s < mats.Length ? mats[s] : null;
                if (mat == null) continue;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var a = w[tris[i]]; var b = w[tris[i + 1]]; var c = w[tris[i + 2]];
                    if (!box.Contains(a) || !box.Contains(b) || !box.Contains(c)) continue;
                    if (!byMat.TryGetValue(mat, out var vl))
                    {
                        vl = new List<Vector3>(); byMat[mat] = vl; idxByMat[mat] = new List<int>();
                    }
                    var il = idxByMat[mat];
                    int bse = vl.Count;
                    vl.Add(a); vl.Add(b); vl.Add(c);
                    il.Add(bse); il.Add(bse + 1); il.Add(bse + 2);
                    triTotal++;
                }
            }
        }
        if (triTotal == 0) throw new InvalidOperationException("구역 안에 삼각형이 없다");

        // 피벗 계산
        var all = new Bounds();
        bool first = true;
        foreach (var kv in byMat)
            foreach (var v in kv.Value) { if (first) { all = new Bounds(v, Vector3.zero); first = false; } else all.Encapsulate(v); }
        Vector3 pivot = pivotMode == "centre" ? all.center : new Vector3(all.center.x, all.min.y, all.center.z);

        var verts2 = new List<Vector3>();
        var subs = new List<List<int>>();
        var matList = new List<Material>();
        foreach (var kv in byMat)
        {
            var il = idxByMat[kv.Key];
            int offset = verts2.Count;
            foreach (var v in kv.Value) verts2.Add(v - pivot);
            var nl = new List<int>(il.Count);
            foreach (var i in il) nl.Add(i + offset);
            subs.Add(nl); matList.Add(kv.Key);
        }

        var outMesh = new Mesh { name = Path.GetFileNameWithoutExtension(prefabPath) };
        outMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        outMesh.SetVertices(verts2);
        outMesh.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) outMesh.SetTriangles(subs[s], s);
        outMesh.RecalculateNormals();
        outMesh.RecalculateBounds();
        var uv = new Vector2[verts2.Count];
        for (int i = 0; i < verts2.Count; i++) uv[i] = new Vector2(verts2[i].x * 0.4f, verts2[i].z * 0.4f);
        outMesh.uv = uv;

        string dir = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
        EnsureFolder(dir);
        string meshPath = dir + "/" + outMesh.name + "_mesh.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (old != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(outMesh, meshPath);

        var go = new GameObject(outMesh.name);
        go.AddComponent<MeshFilter>().sharedMesh = outMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = matList.ToArray();
        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = outMesh;
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        UnityEngine.Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();

        Debug.Log("EXTRACT " + prefabPath + " tris=" + triTotal + " subs=" + subs.Count +
                  " size=" + all.size.ToString("F2") + " pivot=" + pivot.ToString("F2"));
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static string Arg(string[] a, int i, string d) => a != null && a.Length > i && !string.IsNullOrEmpty(a[i]) ? a[i] : d;
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
