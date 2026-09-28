// 추출한 원본 프리팹에서 '치수 파라미터'를 계측한다. 형상을 베끼는 것이 아니라 규격을 읽는다.
// 이 값이 파라메트릭 생성기의 입력이 되고, 생성기는 실제 층고에 정확히 맞는 물건을 만든다.
//
// args: [prefabPath, outJson]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class AnalyzeSourceProfile
{
    public static void Main(string[] args)
    {
        string prefabPath = Arg(args, 0, "Assets/ChooGuard/Art/StationInterior/Reused/원본에스컬레이터_상승7p87.prefab");
        string outJson = Arg(args, 1, ".planning/2026-09-22-station-interior-build/source-profile.json");

        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (pf == null) throw new InvalidOperationException("프리팹 없음: " + prefabPath);
        var mesh = pf.GetComponent<MeshFilter>().sharedMesh;
        var mats = pf.GetComponent<MeshRenderer>().sharedMaterials;
        var v = mesh.vertices;
        var tris = mesh.triangles;

        // 주 진행 방향: 경사 삼각형(23~63°)의 수평 성분 평균
        var dirSum = Vector3.zero;
        double inclineArea = 0;
        var slopes = new List<float>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            var a = v[tris[i]]; var b = v[tris[i + 1]]; var c = v[tris[i + 2]];
            var n = Vector3.Cross(b - a, c - a);
            float area = n.magnitude * 0.5f;
            if (area < 0.01f) continue;
            var nn = n.normalized;
            float ny = Mathf.Abs(nn.y);
            if (ny < 0.45f || ny > 0.93f) continue;
            var horiz = new Vector3(nn.x, 0f, nn.z);
            if (horiz.sqrMagnitude < 1e-6f) continue;
            dirSum += horiz.normalized * area;
            inclineArea += area;
            slopes.Add(Mathf.Acos(ny) * Mathf.Rad2Deg);
        }
        var fall = dirSum.sqrMagnitude > 1e-6f ? dirSum.normalized : Vector3.forward;
        var along = new Vector3(-fall.z, 0f, fall.x);   // 경사 법선의 수평성분에 직교 = 진행 방향과 평행하지 않음
        along = new Vector3(fall.x, 0f, fall.z).normalized;  // 경사면이 향하는 수평 방향 = 진행 방향
        var cross = new Vector3(-along.z, 0f, along.x);

        slopes.Sort();
        float slopeMed = slopes.Count > 0 ? slopes[slopes.Count / 2] : 0f;

        // 수평면(계단판 후보)을 y 로 히스토그램 -> 단높이
        var flatY = new List<float>();
        var flatArea = new List<float>();
        for (int i = 0; i < tris.Length; i += 3)
        {
            var a = v[tris[i]]; var b = v[tris[i + 1]]; var c = v[tris[i + 2]];
            var n = Vector3.Cross(b - a, c - a);
            float area = n.magnitude * 0.5f;
            if (area < 0.02f) continue;
            if (Mathf.Abs(n.normalized.y) < 0.95f) continue;
            flatY.Add((a.y + b.y + c.y) / 3f);
            flatArea.Add(area);
        }
        flatY.Sort();
        var steps = new List<float>();
        for (int i = 0; i < flatY.Count; i++)
        {
            if (steps.Count == 0 || flatY[i] - steps[steps.Count - 1] > 0.06f) steps.Add(flatY[i]);
        }
        var rises = new List<float>();
        for (int i = 1; i < steps.Count; i++)
        {
            float d = steps[i] - steps[i - 1];
            if (d > 0.08f && d < 0.40f) rises.Add(d);
        }
        rises.Sort();
        float riseMed = rises.Count > 0 ? rises[rises.Count / 2] : 0f;

        // 폭: cross 축 투영 범위 (경사 구간의 정점만)
        float cMin = 1e9f, cMax = -1e9f, aMin = 1e9f, aMax = -1e9f;
        foreach (var p in v)
        {
            float cc = Vector3.Dot(p, cross), aa = Vector3.Dot(p, along);
            cMin = Mathf.Min(cMin, cc); cMax = Mathf.Max(cMax, cc);
            aMin = Mathf.Min(aMin, aa); aMax = Mathf.Max(aMax, aa);
        }

        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.source-profile.v1\",\n");
        sb.AppendFormat("  \"prefab\": \"{0}\",\n", prefabPath.Replace("\\", "/"));
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"vertices\": {0}, \"triangles\": {1}, \"subMeshes\": {2},\n", v.Length, tris.Length / 3, mesh.subMeshCount);
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"bounds\": {{\"size\": [{0},{1},{2}]}},\n", N(mesh.bounds.size.x), N(mesh.bounds.size.y), N(mesh.bounds.size.z));
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"inclineSlopeDegMedian\": {0}, \"inclineArea\": {1},\n", N(slopeMed), N((float)inclineArea));
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"alongAxis\": [{0},{1},{2}], \"crossAxis\": [{3},{4},{5}],\n",
            N(along.x), N(along.y), N(along.z), N(cross.x), N(cross.y), N(cross.z));
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"widthAcross\": {0}, \"lengthAlong\": {1},\n", N(cMax - cMin), N(aMax - aMin));
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"flatLevels\": {0}, \"stepRiseMedian\": {1}, \"stepRiseSamples\": {2},\n",
            steps.Count, N(riseMed), rises.Count);
        sb.Append("  \"materials\": [");
        for (int i = 0; i < mats.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.AppendFormat("\"{0}\"", mats[i] == null ? "null" : mats[i].name);
        }
        sb.Append("]\n}\n");

        string full = Path.GetFullPath(outJson);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("PROFILE slope=" + slopeMed.ToString("F1") + " rise=" + riseMed.ToString("F3") +
                  " width=" + (cMax - cMin).ToString("F2") + " levels=" + steps.Count + " -> " + outJson);
    }

    static string N(float x) => x.ToString("F3", CultureInfo.InvariantCulture);
    static string Arg(string[] a, int i, string d) => a != null && a.Length > i && !string.IsNullOrEmpty(a[i]) ? a[i] : d;
}
