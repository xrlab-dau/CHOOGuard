// 3개 층 보행 검증. NavMesh 위에서 실제로 걸어 다닐 수 있는지를 경로 계산으로 증명한다.
//
// 1) 층별 NavMesh 표본 적중률 — 슬래브 격자에서 NavMesh.SamplePosition 이 잡히는 비율
// 2) 층간 연결 — 1F 임의점 → 2F/3F 임의점 완전경로(PathComplete) 성립 여부
// 3) 도로 스폰 → 2F 대합실 — 외부에서 실내까지 걸어 들어올 수 있는가
//
// args: [outJson]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Newtonsoft.Json.Linq;

public static class NavWalkTest
{
    public static void Main(string[] args)
    {
        string outJson = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/nav-walk-test.json";
        string specPath = ".planning/2026-09-22-station-interior-build/station-interior-spec.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(specPath)));

        var levelPts = new Dictionary<string, List<Vector3>>();
        var levelY = new Dictionary<string, float>();
        var sb = new StringBuilder();
        sb.Append("{\n  \"schema\": \"chooguard.nav-walk-test.v1\",\n  \"agent\": {\"radius\":0.30,\"height\":1.80,\"slope\":45,\"climb\":0.28},\n  \"levels\": [\n");

        bool firstLevel = true;
        foreach (JObject L in (JArray)spec["levels"])
        {
            string id = (string)L["id"];
            float fy = (float)L["floorY"];
            levelY[id] = fy;
            var pts = new List<Vector3>();
            var hits = new List<Vector3>();
            int sampled = 0, onMesh = 0;
            foreach (JArray r in (JArray)L["slabRects"])
            {
                float x0 = (float)r[0], z0 = (float)r[1], x1 = (float)r[2], z1 = (float)r[3];
                for (float x = x0 + 1f; x < x1; x += 3f)
                for (float z = z0 + 1f; z < z1; z += 3f)
                {
                    sampled++;
                    var probe = new Vector3(x, fy + 0.4f, z);
                    if (NavMesh.SamplePosition(probe, out var hit, 1.2f, NavMesh.AllAreas))
                    {
                        onMesh++;
                        if (hits.Count < 4000) hits.Add(hit.position);
                    }
                }
            }
            levelPts[id] = hits;
            if (!firstLevel) sb.Append(",\n");
            firstLevel = false;
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "    {{\"id\":\"{0}\",\"floorY\":{1},\"sampled\":{2},\"onNavMesh\":{3},\"coverage\":{4}}}",
                id, N(fy), sampled, onMesh, N(sampled > 0 ? (float)onMesh / sampled : 0f));
        }
        sb.Append("\n  ],\n  \"paths\": [\n");

        var checks = new List<(string, Vector3, Vector3)>();
        void Pair(string name, string a, string b)
        {
            if (!levelPts.ContainsKey(a) || !levelPts.ContainsKey(b)) return;
            var la = levelPts[a]; var lb = levelPts[b];
            if (la.Count == 0 || lb.Count == 0) return;
            checks.Add((name, la[la.Count / 3], lb[lb.Count * 2 / 3]));
        }
        Pair("1F→2F", "1F", "2F");
        Pair("2F→3F", "2F", "3F");
        Pair("1F→3F", "1F", "3F");
        Pair("1F 동측→1F 서측", "1F", "1F");

        var spawn = new Vector3(-169.5f, 0.145f, -40.647f);
        if (levelPts.ContainsKey("2F") && levelPts["2F"].Count > 0)
            checks.Add(("도로 스폰→2F 대합실", spawn, levelPts["2F"][levelPts["2F"].Count / 2]));

        bool firstPath = true;
        foreach (var (name, from, to) in checks)
        {
            var path = new NavMeshPath();
            bool ok = false;
            var a = from; var b = to;
            if (NavMesh.SamplePosition(from, out var ha, 3f, NavMesh.AllAreas)) a = ha.position;
            if (NavMesh.SamplePosition(to, out var hb, 3f, NavMesh.AllAreas)) b = hb.position;
            ok = NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path);
            float len = 0f;
            for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (!firstPath) sb.Append(",\n");
            firstPath = false;
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "    {{\"name\":\"{0}\",\"status\":\"{1}\",\"complete\":{2},\"corners\":{3},\"lengthM\":{4}," +
                "\"from\":[{5},{6},{7}],\"to\":[{8},{9},{10}]}}",
                name, path.status, (ok && path.status == NavMeshPathStatus.PathComplete) ? "true" : "false",
                path.corners.Length, N(len), N(a.x), N(a.y), N(a.z), N(b.x), N(b.y), N(b.z));
        }

        var tri = NavMesh.CalculateTriangulation();
        double area = 0;
        for (int i = 0; i + 2 < tri.indices.Length; i += 3)
        {
            var p0 = tri.vertices[tri.indices[i]];
            var p1 = tri.vertices[tri.indices[i + 1]];
            var p2 = tri.vertices[tri.indices[i + 2]];
            area += Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
        }
        sb.Append("\n  ],\n");
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  \"navMesh\": {{\"vertices\": {0}, \"triangles\": {1}, \"areaSqm\": {2}}}\n}}\n",
            tri.vertices.Length, tri.indices.Length / 3, N((float)area));

        string full = Path.GetFullPath(outJson);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("NAV_WALK_TEST navArea=" + area.ToString("F0") + " -> " + outJson);
    }

    static string N(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
