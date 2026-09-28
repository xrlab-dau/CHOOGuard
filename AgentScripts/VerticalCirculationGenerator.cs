// 수직동선 파라메트릭 생성기.
//
// 원본 부산역 모델에서 계측한 규격(경사 30.0° 3건 일치, 재질 50종)을 입력으로 삼고,
// 실제 층고(1F 0.00 → 2F 7.00 → 3F 12.00)에 정확히 맞는 에스컬레이터·계단을 생성한다.
// 원본을 균일 축소하면 단높이·유효폭까지 왜곡되므로 쓰지 않는다. 규격만 물려받는 마이그레이션이다.
//
// 치수 근거 (metric-standards.json):
//   에스컬레이터 유효폭 2.4m · 마감포함 3.4m · 층당 평면 15.0m · 경사 30°
//   계단 유효폭 3.0m(철도설계기준) · 디딤판 0.28m 이상 · 챌면 0.18m 이하(교통약자법 별표1)
//   계단참: 높이 3m 이내마다 너비 1.2m 이상(건축법 방화구조규칙 제15조)
//
// 재질: 원본 부산역 재질 자산을 그대로 참조한다.
// args: [placementsJson, rootName?] — rootName defaults to the original station circulation root.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

public static class VerticalCirculationGenerator
{
    const string DefaultRoot = "부산역 역사 내부 · 수직동선";
    const string OutDir = "Assets/ChooGuard/Art/StationInterior/Generated";
    const string OfficialMat = "Assets/ChooGuard/Art/OfficialBusanStation/Materials/";

    class Build
    {
        public List<Vector3> V = new List<Vector3>();
        public List<Vector3> N = new List<Vector3>();
        public List<Vector2> U = new List<Vector2>();
        public Dictionary<int, List<int>> Sub = new Dictionary<int, List<int>>();

        public void Box(Vector3 c, Vector3 halfAlong, Vector3 halfCross, Vector3 halfUp, int mat)
        {
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float sa = (i & 1) == 0 ? -1 : 1;
                float sc = (i & 2) == 0 ? -1 : 1;
                float su = (i & 4) == 0 ? -1 : 1;
                p[i] = c + halfAlong * sa + halfCross * sc + halfUp * su;
            }
            int[,] faces =
            {
                {0,2,3,1},{4,5,7,6},{0,1,5,4},{2,6,7,3},{0,4,6,2},{1,3,7,5}
            };
            if (!Sub.TryGetValue(mat, out var idx)) { idx = new List<int>(); Sub[mat] = idx; }
            for (int f = 0; f < 6; f++)
            {
                int b = V.Count;
                var a0 = p[faces[f, 0]]; var a1 = p[faces[f, 1]]; var a2 = p[faces[f, 2]]; var a3 = p[faces[f, 3]];
                var nn = Vector3.Cross(a1 - a0, a2 - a0).normalized;
                V.Add(a0); V.Add(a1); V.Add(a2); V.Add(a3);
                N.Add(nn); N.Add(nn); N.Add(nn); N.Add(nn);
                U.Add(new Vector2(0, 0)); U.Add(new Vector2(1, 0)); U.Add(new Vector2(1, 1)); U.Add(new Vector2(0, 1));
                idx.Add(b); idx.Add(b + 1); idx.Add(b + 2);
                idx.Add(b); idx.Add(b + 2); idx.Add(b + 3);
            }
        }

        public Mesh ToMesh(string name, out int[] matOrder)
        {
            var m = new Mesh { name = name };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, U);
            var keys = new List<int>(Sub.Keys); keys.Sort();
            m.subMeshCount = keys.Count;
            for (int i = 0; i < keys.Count; i++) m.SetTriangles(Sub[keys[i]], i);
            m.RecalculateBounds();
            matOrder = keys.ToArray();
            return m;
        }
    }

    public static void Main(string[] args)
    {
        string path = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/vertical-circulation.json";
        var spec = JObject.Parse(File.ReadAllText(Path.GetFullPath(path)));

        EnsureFolder(OutDir);
        var mats = new Material[]
        {
            LoadMat("source-563a47a9fb37ecdbc516"),   // 0 금속#002 — 트러스·스커트·착지판
            LoadMat("source-a8124881ded4766e64bc"),   // 1 금속_원형#001 — 핸드레일
            LoadMat("source-abf1f31198576978dc15"),   // 2 타일#025(계단) — 디딤판
            LoadMat("source-8d04aad275e60ff4e3d9"),   // 3 스트라이프#023 — 단코 경고띠
            LoadGlass(),                              // 4 유리 난간
        };

        string rootName = args != null && args.Length > 1 && !string.IsNullOrEmpty(args[1]) ? args[1] : DefaultRoot;
        var existing = GameObject.Find(rootName);
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var root = new GameObject(rootName);
        var world = GameObject.Find("FPSWorld");
        if (world != null) root.transform.SetParent(world.transform, false);

        int made = 0;
        var log = new System.Text.StringBuilder();
        foreach (JObject u in (JArray)spec["units"])
        {
            string kind = (string)u["kind"];
            float y0 = (float)u["bottomY"], y1 = (float)u["topY"];
            float yaw = (float)u["yaw"];
            var pos = (JArray)u["pos"];
            var origin = new Vector3((float)pos[0], y0, (float)pos[2]);
            string name = (string)u["name"];

            var b = new Build();
            float lenAlong;
            if (kind == "escalator") lenAlong = Escalator(b, y1 - y0, (float)u["clearWidth"]);
            else if (kind == "elevator") lenAlong = Elevator(b, y1 - y0, (float)u["clearWidth"]);
            else lenAlong = Stair(b, y1 - y0, (float)u["clearWidth"]);

            int[] order;
            var mesh = b.ToMesh(name, out order);
            string meshPath = OutDir + "/" + Sanitize(name) + "_mesh.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var use = new Material[order.Length];
            for (int i = 0; i < order.Length; i++) use[i] = mats[order[i]];
            mr.sharedMaterials = use;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            made++;
            log.Append(name + " rise=" + (y1 - y0).ToString("F2") + " runXZ=" + lenAlong.ToString("F2") + "\n");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("VERTCIRC made=" + made + "\n" + log);
    }

    // 승강기: 샤프트 + 각 층 승강장 문. 교통약자법 별표1 — 내부 1.1×1.4m, 신축 폭 1.6m 이상, 문 유효 0.9m
    static float Elevator(Build b, float rise, float clearW)
    {
        float halfC = clearW * 0.5f;      // 샤프트 반폭 (clearW 는 샤프트 외곽폭)
        float depth = 1.90f;              // 샤프트 깊이
        float halfD = depth * 0.5f;
        float doorW = 0.90f, doorH = 2.10f;
        float jamb = (clearW - doorW) * 0.5f;

        // 네 모서리 기둥
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            b.Box(new Vector3(sx * halfC, rise * 0.5f, halfD + sz * halfD),
                  new Vector3(0.08f, 0, 0), new Vector3(0, 0, 0.08f), new Vector3(0, rise * 0.5f, 0), 0);

        // 측면·배면 유리
        b.Box(new Vector3(-halfC, rise * 0.5f, halfD), new Vector3(0.02f, 0, 0), new Vector3(0, 0, halfD - 0.08f), new Vector3(0, rise * 0.5f, 0), 4);
        b.Box(new Vector3(halfC, rise * 0.5f, halfD), new Vector3(0.02f, 0, 0), new Vector3(0, 0, halfD - 0.08f), new Vector3(0, rise * 0.5f, 0), 4);
        b.Box(new Vector3(0, rise * 0.5f, depth), new Vector3(halfC - 0.08f, 0, 0), new Vector3(0, 0, 0.02f), new Vector3(0, rise * 0.5f, 0), 4);

        // 정면: 문 옆 벽체 + 상부 인방
        for (int s = -1; s <= 1; s += 2)
            b.Box(new Vector3(s * (doorW * 0.5f + jamb * 0.5f), rise * 0.5f, 0f),
                  new Vector3(jamb * 0.5f, 0, 0), new Vector3(0, 0, 0.06f), new Vector3(0, rise * 0.5f, 0), 0);

        // 각 정지층 문짝(닫힘) — 5m 간격 가정이 아니라 실제 층 높이는 호출부가 rise 로 준다
        float[] stops = rise > 9f ? new[] { 0f, 7f, 12f } : (rise > 5.5f ? new[] { 0f, 7f } : new[] { 0f, rise });
        foreach (var sy in stops)
        {
            if (sy > rise + 0.01f) continue;
            for (int s = -1; s <= 1; s += 2)
                b.Box(new Vector3(s * doorW * 0.25f, sy + doorH * 0.5f, 0f),
                      new Vector3(doorW * 0.25f, 0, 0), new Vector3(0, 0, 0.04f), new Vector3(0, doorH * 0.5f, 0), 1);
            b.Box(new Vector3(0, sy + doorH + 0.25f, 0f),
                  new Vector3(halfC, 0, 0), new Vector3(0, 0, 0.06f), new Vector3(0, 0.25f, 0), 0);
        }

        // 카(케이지) — 최하층에 정지
        b.Box(new Vector3(0, 1.15f, halfD), new Vector3(0.80f, 0, 0), new Vector3(0, 0, 0.70f), new Vector3(0, 1.15f, 0), 1);
        b.Box(new Vector3(0, 0.03f, halfD), new Vector3(0.80f, 0, 0), new Vector3(0, 0, 0.70f), new Vector3(0, 0.03f, 0), 2);
        return depth;
    }
    // 로컬 축: +Z 진행, +X 횡, +Y 상. 원점은 하부 착지판 앞끝 바닥.
    static float Escalator(Build b, float rise, float clearW)
    {
        const float Slope = 30f;
        const float Going = 0.40f;                       // 계단판 진행 길이
        float stepRise = Going * Mathf.Tan(Slope * Mathf.Deg2Rad);   // 0.2309
        int steps = Mathf.Max(1, Mathf.RoundToInt(rise / stepRise));
        float actualRise = rise / steps;                 // 층고에 정확히 맞춘다
        float going = actualRise / Mathf.Tan(Slope * Mathf.Deg2Rad);
        float landing = 1.20f;                           // 상·하부 착지판
        float skirtW = 0.50f;                            // 편측 스커트 마감폭 (2.4 + 2×0.5 = 3.4)
        float halfC = clearW * 0.5f;
        float z = 0f;

        // 하부 착지판
        b.Box(new Vector3(0, 0.03f, z + landing * 0.5f), new Vector3(0, 0, landing * 0.5f), new Vector3(halfC, 0, 0), new Vector3(0, 0.03f, 0), 0);
        z += landing;

        for (int i = 0; i < steps; i++)
        {
            float y = i * actualRise;
            float zc = z + i * going;
            // 디딤판
            b.Box(new Vector3(0, y + actualRise - 0.03f, zc + going * 0.5f),
                  new Vector3(0, 0, going * 0.5f), new Vector3(halfC, 0, 0), new Vector3(0, 0.03f, 0), 2);
            // 챌면
            b.Box(new Vector3(0, y + actualRise * 0.5f, zc + 0.02f),
                  new Vector3(0, 0, 0.02f), new Vector3(halfC, 0, 0), new Vector3(0, actualRise * 0.5f, 0), 0);
            // 단코 경고띠
            b.Box(new Vector3(0, y + actualRise - 0.055f, zc + going - 0.03f),
                  new Vector3(0, 0, 0.03f), new Vector3(halfC, 0, 0), new Vector3(0, 0.025f, 0), 3);
        }
        float runSteps = steps * going;
        z += runSteps;
        // 상부 착지판
        b.Box(new Vector3(0, rise + 0.03f, z + landing * 0.5f), new Vector3(0, 0, landing * 0.5f), new Vector3(halfC, 0, 0), new Vector3(0, 0.03f, 0), 0);
        float total = landing + runSteps + landing;

        // 경사 구간 중심선
        float midZ = landing + runSteps * 0.5f;
        float midY = rise * 0.5f;
        float inclLen = Mathf.Sqrt(runSteps * runSteps + rise * rise);
        var along = new Vector3(0, rise, runSteps).normalized;
        var up = new Vector3(0, runSteps, -rise).normalized;

        for (int s = -1; s <= 1; s += 2)
        {
            float cx = s * (halfC + skirtW * 0.5f);
            // 스커트 + 트러스 측판
            b.Box(new Vector3(cx, midY - 0.35f, midZ), along * (inclLen * 0.5f), new Vector3(skirtW * 0.5f, 0, 0), up * 0.55f, 0);
            // 유리 난간
            b.Box(new Vector3(cx, midY + 0.55f, midZ), along * (inclLen * 0.5f), new Vector3(0.012f, 0, 0), up * 0.45f, 4);
            // 핸드레일
            b.Box(new Vector3(cx, midY + 1.02f, midZ), along * (inclLen * 0.5f), new Vector3(0.045f, 0, 0), up * 0.035f, 1);
            // 착지부 난간
            b.Box(new Vector3(cx, 0.55f, landing * 0.5f), new Vector3(0, 0, landing * 0.5f), new Vector3(0.012f, 0, 0), new Vector3(0, 0.45f, 0), 4);
            b.Box(new Vector3(cx, rise + 0.55f, landing + runSteps + landing * 0.5f), new Vector3(0, 0, landing * 0.5f), new Vector3(0.012f, 0, 0), new Vector3(0, 0.45f, 0), 4);
        }
        // 트러스 하부
        b.Box(new Vector3(0, midY - 0.95f, midZ), along * (inclLen * 0.5f), new Vector3(halfC + skirtW, 0, 0), up * 0.12f, 0);
        return total;
    }

    static float Stair(Build b, float rise, float clearW)
    {
        const float Riser = 0.175f;      // 교통약자법 챌면 0.18m 이하
        const float Tread = 0.300f;      // 디딤판 0.28m 이상
        const float LandingRiseMax = 3.0f;   // 건축법: 높이 3m 이내마다 계단참
        const float LandingLen = 1.20f;
        int totalRisers = Mathf.Max(1, Mathf.RoundToInt(rise / Riser));
        float actualRiser = rise / totalRisers;
        int perFlight = Mathf.Max(1, Mathf.FloorToInt(LandingRiseMax / actualRiser));
        float halfC = clearW * 0.5f;

        float z = 0f, y = 0f;
        int done = 0;
        while (done < totalRisers)
        {
            int n = Mathf.Min(perFlight, totalRisers - done);
            for (int i = 0; i < n; i++)
            {
                float yy = y + i * actualRiser;
                float zc = z + i * Tread;
                b.Box(new Vector3(0, yy + actualRiser - 0.03f, zc + Tread * 0.5f),
                      new Vector3(0, 0, Tread * 0.5f), new Vector3(halfC, 0, 0), new Vector3(0, 0.03f, 0), 2);
                b.Box(new Vector3(0, yy + actualRiser * 0.5f, zc + 0.02f),
                      new Vector3(0, 0, 0.02f), new Vector3(halfC, 0, 0), new Vector3(0, actualRiser * 0.5f, 0), 0);
                b.Box(new Vector3(0, yy + actualRiser - 0.055f, zc + Tread - 0.025f),
                      new Vector3(0, 0, 0.025f), new Vector3(halfC, 0, 0), new Vector3(0, 0.02f, 0), 3);
            }
            float flightRun = n * Tread;
            // 경사 난간
            float midZ = z + flightRun * 0.5f, midY = y + n * actualRiser * 0.5f;
            float inclLen = Mathf.Sqrt(flightRun * flightRun + (n * actualRiser) * (n * actualRiser));
            var along = new Vector3(0, n * actualRiser, flightRun).normalized;
            var up = new Vector3(0, flightRun, -(n * actualRiser)).normalized;
            for (int s = -1; s <= 1; s += 2)
            {
                float cx = s * halfC;
                b.Box(new Vector3(cx, midY + 0.60f, midZ), along * (inclLen * 0.5f), new Vector3(0.012f, 0, 0), up * 0.50f, 4);
                b.Box(new Vector3(cx, midY + 1.12f, midZ), along * (inclLen * 0.5f), new Vector3(0.035f, 0, 0), up * 0.030f, 1);
            }
            z += flightRun; y += n * actualRiser; done += n;
            if (done < totalRisers)
            {
                b.Box(new Vector3(0, y - 0.03f, z + LandingLen * 0.5f),
                      new Vector3(0, 0, LandingLen * 0.5f), new Vector3(halfC, 0, 0), new Vector3(0, 0.03f, 0), 2);
                for (int s = -1; s <= 1; s += 2)
                {
                    float cx = s * halfC;
                    b.Box(new Vector3(cx, y + 0.60f, z + LandingLen * 0.5f), new Vector3(0, 0, LandingLen * 0.5f), new Vector3(0.012f, 0, 0), new Vector3(0, 0.50f, 0), 4);
                    b.Box(new Vector3(cx, y + 1.12f, z + LandingLen * 0.5f), new Vector3(0, 0, LandingLen * 0.5f), new Vector3(0.035f, 0, 0), new Vector3(0, 0.030f, 0), 1);
                }
                z += LandingLen;
            }
        }
        return z;
    }

    static Material LoadMat(string assetName)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(OfficialMat + assetName + ".mat");
        if (m == null) m = AssetDatabase.LoadAssetAtPath<Material>("Assets/ChooGuard/Art/StationInterior/Materials/벽_내장.mat");
        return m;
    }

    static Material LoadGlass()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/ChooGuard/Art/StationInterior/Materials/유리_커튼월.mat");
        return m != null ? m : LoadMat("source-563a47a9fb37ecdbc516");
    }

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace("→", "-").Replace(" ", "_");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
