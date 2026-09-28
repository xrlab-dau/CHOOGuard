// 공식 원본 외피 메시에서 지정 체적 안의 삼각형을 읽어, 법선으로 바닥/천장/벽을 분류하고
// 벽 삼각형을 UV(장축 16.2도) 좌표계의 선분으로 투영해 벽선을 뽑는다.
// 합성 지오메트리를 만들지 않는다. 오직 원본 읽기.
//
// args: [outJson, xMin, xMax, yMin, yMax, zMin, zMax]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class ExtractShellGeometry
{
    const float ThetaDeg = 16.2f;

    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        string outPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/shell-geometry.json";
        float xMin = P(args, 1, -2f), xMax = P(args, 2, 62f);
        float yMin = P(args, 3, 6.5f), yMax = P(args, 4, 12.0f);
        float zMin = P(args, 5, -67f), zMax = P(args, 6, -27f);

        var vol = new Bounds();
        vol.SetMinMax(new Vector3(xMin, yMin, zMin), new Vector3(xMax, yMax, zMax));

        float th = ThetaDeg * Mathf.Deg2Rad;
        float s = Mathf.Sin(th), c = Mathf.Cos(th);

        var floorTris = new List<float[]>();
        var ceilTris = new List<float[]>();
        var wallSegs = new List<float[]>();   // u0,v0,u1,v1,yBottom,yTop
        var matNames = new Dictionary<string, int>();
        int scanned = 0, inVol = 0;

        var roots = new[] { "공식 자료 부산역 역사", "공식 자료 부산역 주변 공간" };
        foreach (var rn in roots)
        {
            var root = GameObject.Find(rn);
            if (root == null) continue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                if (!mr.bounds.Intersects(vol)) continue;

                var tr = mf.transform;
                var verts = mesh.vertices;
                var world = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++) world[i] = tr.TransformPoint(verts[i]);

                var mats = mr.sharedMaterials;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    string mn = sub < mats.Length && mats[sub] != null ? mats[sub].name : "(none)";
                    var idx = mesh.GetTriangles(sub);
                    for (int i = 0; i + 2 < idx.Length; i += 3)
                    {
                        scanned++;
                        Vector3 a = world[idx[i]], b = world[idx[i + 1]], d2 = world[idx[i + 2]];
                        Vector3 ctr = (a + b + d2) / 3f;
                        if (!vol.Contains(ctr)) continue;
                        inVol++;
                        matNames[mn] = matNames.TryGetValue(mn, out var mc) ? mc + 1 : 1;

                        Vector3 n = Vector3.Cross(b - a, d2 - a);
                        float area = n.magnitude * 0.5f;
                        if (area < 1e-4f) continue;
                        n.Normalize();
                        float ny = Mathf.Abs(n.y);

                        if (ny > 0.85f)
                        {
                            var rec = new[] { ctr.x, ctr.y, ctr.z, area, n.y > 0 ? 1f : -1f };
                            if (ctr.y < (yMin + yMax) * 0.5f) floorTris.Add(rec); else ceilTris.Add(rec);
                        }
                        else if (ny < 0.35f)
                        {
                            // 벽: 수평 투영 선분 (삼각형의 수평 확장 최대 축)
                            float u0 = 9e9f, u1 = -9e9f, v0 = 9e9f, v1 = -9e9f, yb = 9e9f, yt = -9e9f;
                            foreach (var p in new[] { a, b, d2 })
                            {
                                float u = p.x * s + p.z * c;
                                float v = p.x * c - p.z * s;
                                u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                                v0 = Mathf.Min(v0, v); v1 = Mathf.Max(v1, v);
                                yb = Mathf.Min(yb, p.y); yt = Mathf.Max(yt, p.y);
                            }
                            wallSegs.Add(new[] { u0, v0, u1, v1, yb, yt, area });
                        }
                    }
                }
            }
        }

        var sb = new StringBuilder();
        sb.Append("{\n \"schema\":\"chooguard.shell-geometry.v1\",\n");
        sb.Append(" \"basis\":\"공식 원본 FBX 메시 삼각형 직접 판독. 합성 없음.\",\n");
        sb.Append(" \"thetaDeg\":").Append(F(ThetaDeg)).Append(",\n");
        sb.Append(" \"volume\":{\"xMin\":").Append(F(xMin)).Append(",\"xMax\":").Append(F(xMax))
          .Append(",\"yMin\":").Append(F(yMin)).Append(",\"yMax\":").Append(F(yMax))
          .Append(",\"zMin\":").Append(F(zMin)).Append(",\"zMax\":").Append(F(zMax)).Append("},\n");
        sb.Append(" \"scannedTris\":").Append(scanned).Append(",\"trisInVolume\":").Append(inVol).Append(",\n");
        sb.Append(" \"materials\":{");
        bool f1 = true;
        foreach (var kv in matNames)
        {
            if (!f1) sb.Append(","); f1 = false;
            sb.Append("\"").Append(kv.Key.Replace("\"", "'")).Append("\":").Append(kv.Value);
        }
        sb.Append("},\n");
        sb.Append(" \"floorFormat\":\"[x,y,z,area,normalSign]\",\n \"floor\":[");
        Emit(sb, floorTris); sb.Append("],\n");
        sb.Append(" \"ceilFormat\":\"[x,y,z,area,normalSign]\",\n \"ceiling\":[");
        Emit(sb, ceilTris); sb.Append("],\n");
        sb.Append(" \"wallFormat\":\"[u0,v0,u1,v1,yBottom,yTop,area]\",\n \"walls\":[");
        Emit(sb, wallSegs); sb.Append("]\n}");

        var full = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString());
        Debug.Log("SHELL_GEOM inVol=" + inVol + " floor=" + floorTris.Count + " ceil=" + ceilTris.Count + " wall=" + wallSegs.Count);
    }

    static void Emit(StringBuilder sb, List<float[]> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append("[");
            for (int j = 0; j < rows[i].Length; j++)
            {
                if (j > 0) sb.Append(",");
                sb.Append(F(rows[i][j]));
            }
            sb.Append("]");
        }
    }

    static string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);
}
