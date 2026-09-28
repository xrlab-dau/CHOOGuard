// MainShell 의 서브메시별 재질·삼각형수·월드 바운즈를 전부 낸다.
// 원본 SketchUp 재질명은 해시된 자산명으로 들어와 있으므로, 조인은 파이썬에서 매니페스트로 한다.
// args: [outJson, rootName]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class SubmeshAtlas
{
    public static void Main(string[] args)
    {
        string outJson = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/submesh-atlas.json";
        string rootName = args != null && args.Length > 1 && !string.IsNullOrEmpty(args[1]) ? args[1] : "MainShell";

        Transform root = null;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == rootName) { root = t; break; }
        if (root == null) throw new InvalidOperationException("루트 없음: " + rootName);

        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.submesh-atlas.v1\",\n  \"root\": \"" + rootName + "\",\n  \"submeshes\": [\n");
        bool first = true;
        int total = 0;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            var rend = mf.GetComponent<Renderer>();
            if (mesh == null || rend == null) continue;
            var verts = mesh.vertices;
            var l2w = mf.transform.localToWorldMatrix;
            var mats = rend.sharedMaterials;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                if (tris.Length == 0) continue;
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                var sum = Vector3.zero;
                double area = 0;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var a = l2w.MultiplyPoint3x4(verts[tris[i]]);
                    var b = l2w.MultiplyPoint3x4(verts[tris[i + 1]]);
                    var c = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);
                    min = Vector3.Min(min, Vector3.Min(a, Vector3.Min(b, c)));
                    max = Vector3.Max(max, Vector3.Max(a, Vector3.Max(b, c)));
                    sum += a + b + c;
                    area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                }
                int triCount = tris.Length / 3;
                var cen = sum / (triCount * 3f);
                string matName = (s < mats.Length && mats[s] != null) ? mats[s].name : "(null)";
                if (!first) sb.Append(",\n");
                first = false;
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "    {{\"mesh\":\"{0}\",\"sub\":{1},\"mat\":\"{2}\",\"tris\":{3},\"area\":{4},\"min\":[{5},{6},{7}],\"max\":[{8},{9},{10}],\"centroid\":[{11},{12},{13}]}}",
                    mf.name, s, matName, triCount, area.ToString("F1", CultureInfo.InvariantCulture),
                    N(min.x), N(min.y), N(min.z), N(max.x), N(max.y), N(max.z), N(cen.x), N(cen.y), N(cen.z));
                total++;
            }
        }
        sb.Append("\n  ],\n  \"count\": " + total + "\n}\n");
        string full = Path.GetFullPath(outJson);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("SUBMESH_ATLAS " + total + " -> " + full);
    }

    static string N(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
