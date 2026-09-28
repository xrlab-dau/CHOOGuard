// 실내 마감을 실제 PBR 재질로 교체하고, 원시 큐브의 UV 를 월드 스케일로 다시 만든다.
//
// 문제: 슬래브·벽을 Unity 기본 Cube 로 만들어 UV 가 면당 0~1 이라, 20m 벽에 텍스처가 한 장 늘어난다.
// 해결: 각 오브젝트의 실제 월드 치수로 박스 메시를 새로 만들고 UV 를 미터 단위로 찍는다.
//       메시는 루트별 단일 에셋 파일의 서브에셋으로 저장해 프로젝트를 더럽히지 않는다.
//
// 재질 출처: ambientCG CC0 (materials-signage 레인 취득분)
// args: [dummy]

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public static class ApplyRealMaterials
{
    const string PbrDir = "Assets/ChooGuard/Art/StationInterior/PBR";
    const string MatDir = "Assets/ChooGuard/Art/StationInterior/Materials";
    const string MeshAsset = "Assets/ChooGuard/Art/StationInterior/Generated/InteriorBoxMeshes.asset";

    class Rule { public string[] Keys; public string Mat; public float Tex; }

    public static void Main(string[] args)
    {
        // 1) PBR 폴더에서 URP Lit 재질 생성
        var made = new Dictionary<string, Material>();
        foreach (var dir in Directory.GetDirectories(Path.GetFullPath(PbrDir)))
        {
            string set = Path.GetFileName(dir);
            var m = BuildMaterial(set);
            if (m != null) made[set] = m;
        }

        // 2) 이름 규칙 → 재질 · 텍스처 반복 주기(미터)
        var rules = new List<Rule>
        {
            new Rule { Keys = new[]{"천장"},      Mat = "OfficeCeiling003_2K", Tex = 1.2f },
            new Rule { Keys = new[]{"슬래브 1F"}, Mat = "Granite005A_2K",  Tex = 1.2f },
            new Rule { Keys = new[]{"슬래브 2F"}, Mat = "Terrazzo018_4K",  Tex = 0.9f },
            new Rule { Keys = new[]{"슬래브 3F"}, Mat = "Tiles074_4K",     Tex = 0.6f },
            new Rule { Keys = new[]{"슬래브"},    Mat = "Concrete048_2K",  Tex = 3.0f },
            new Rule { Keys = new[]{"기둥"},      Mat = "Metal050A_4K",    Tex = 1.4f },
            new Rule { Keys = new[]{"자동문","유리","승강기"}, Mat = "Facade001_4K", Tex = 3.0f },
            new Rule { Keys = new[]{"매표창구","카운터","종합관광안내소"}, Mat = "WoodFloor064_4K", Tex = 1.2f },
            new Rule { Keys = new[]{"개찰기"},    Mat = "Metal032_2K",     Tex = 0.8f },
            new Rule { Keys = new[]{"자동발매기","ATM","충전소"}, Mat = "Metal046A_2K", Tex = 1.0f },
            new Rule { Keys = new[]{"보관함"},    Mat = "Metal046A_2K",    Tex = 1.0f },
            new Rule { Keys = new[]{"벤치"},      Mat = "WoodFloor064_4K", Tex = 1.0f },
            new Rule { Keys = new[]{"벽","칸막이","인방","상인방","단부벽","배면벽"}, Mat = "PaintedPlaster017_4K", Tex = 2.5f },
        };

        var roots = new List<GameObject>();
        foreach (var n in new[] { "부산역 역사 내부", "부산역 역사 내부 · 마감" })
        {
            var g = GameObject.Find(n);
            if (g != null) roots.Add(g);
        }
        if (roots.Count == 0) throw new InvalidOperationException("실내 루트를 찾지 못했다");

        // 3) 기존 메시 에셋 초기화
        string mdir = Path.GetDirectoryName(MeshAsset).Replace('\\', '/');
        EnsureFolder(mdir);
        if (AssetDatabase.LoadAssetAtPath<Mesh>(MeshAsset) != null || File.Exists(Path.GetFullPath(MeshAsset)))
            AssetDatabase.DeleteAsset(MeshAsset);
        var container = new Mesh { name = "__container" };
        AssetDatabase.CreateAsset(container, MeshAsset);

        int remeshed = 0, assigned = 0, skipped = 0;
        foreach (var root in roots)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                string nm = mf.gameObject.name;

                Rule hit = null;
                foreach (var r in rules)
                {
                    foreach (var k in r.Keys)
                        if (nm.IndexOf(k, StringComparison.Ordinal) >= 0) { hit = r; break; }
                    if (hit != null) break;
                }
                if (hit == null) { skipped++; continue; }
                if (!made.TryGetValue(hit.Mat, out var mat)) { skipped++; continue; }

                // 사인·픽토그램은 색상 유지
                if (nm.IndexOf("사인", StringComparison.Ordinal) >= 0 || nm.IndexOf("표찰", StringComparison.Ordinal) >= 0)
                { skipped++; continue; }

                if (mf.sharedMesh == null || mf.sharedMesh.name == "Cube" || mf.sharedMesh.name.StartsWith("box_"))
                {
                    var s = mf.transform.lossyScale;
                    var box = WorldUvBox(s, hit.Tex);
                    box.name = "box_" + remeshed;
                    AssetDatabase.AddObjectToAsset(box, MeshAsset);
                    mf.sharedMesh = box;
                    var mc = mf.GetComponent<MeshCollider>();
                    if (mc != null) mc.sharedMesh = box;
                    remeshed++;
                }
                mr.sharedMaterial = mat;
                assigned++;
            }
        }

        AssetDatabase.SaveAssets();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("REAL_MATERIALS sets=" + made.Count + " assigned=" + assigned +
                  " remeshed=" + remeshed + " skipped=" + skipped);
    }

    // 로컬 스케일 1 기준 박스. UV 는 월드 치수 / texMetres.
    static Mesh WorldUvBox(Vector3 worldSize, float texMetres)
    {
        float hx = 0.5f, hy = 0.5f, hz = 0.5f;
        float sx = Mathf.Max(worldSize.x, 0.001f) / texMetres;
        float sy = Mathf.Max(worldSize.y, 0.001f) / texMetres;
        float sz = Mathf.Max(worldSize.z, 0.001f) / texMetres;

        var v = new List<Vector3>(); var n = new List<Vector3>(); var u = new List<Vector2>(); var t = new List<int>();
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, float uw, float uh)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(nrm);
            u.Add(new Vector2(0, 0)); u.Add(new Vector2(uw, 0)); u.Add(new Vector2(uw, uh)); u.Add(new Vector2(0, uh));
            t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }
        Face(new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz), Vector3.up, sx, sz);
        Face(new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz), new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz), Vector3.down, sx, sz);
        Face(new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(-hx, hy, -hz), Vector3.back, sx, sy);
        Face(new Vector3(hx, -hy, hz), new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), Vector3.forward, sx, sy);
        Face(new Vector3(-hx, -hy, hz), new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), Vector3.left, sz, sy);
        Face(new Vector3(hx, -hy, -hz), new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz), Vector3.right, sz, sy);

        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, u); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    static Material BuildMaterial(string set)
    {
        string dir = PbrDir + "/" + set;
        string color = Find(dir, "_Color");
        if (color == null) return null;
        string normal = Find(dir, "_NormalGL");
        string ao = Find(dir, "_AmbientOcclusion");
        string metal = Find(dir, "_Metalness");

        if (normal != null)
        {
            var ti = AssetImporter.GetAtPath(normal) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.NormalMap)
            { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        }

        string p = MatDir + "/PBR_" + set + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh);
            EnsureFolder(MatDir);
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(color));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (normal != null)
        {
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_BumpScale", 1f);
        }
        if (ao != null)
        {
            m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ao));
            m.EnableKeyword("_OCCLUSIONMAP");
        }
        bool metallic = metal != null;
        m.SetFloat("_Metallic", metallic ? 0.85f : 0.0f);
        m.SetFloat("_Smoothness", set.StartsWith("Metal") ? 0.62f
                                 : set.StartsWith("Facade") ? 0.88f
                                 : set.StartsWith("Tiles") || set.StartsWith("Terrazzo") || set.StartsWith("Granite") ? 0.45f
                                 : 0.22f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static string Find(string dir, string suffix)
    {
        foreach (var f in Directory.GetFiles(Path.GetFullPath(dir)))
        {
            if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            if (Path.GetFileNameWithoutExtension(f).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return dir + "/" + Path.GetFileName(f);
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
