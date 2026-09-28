// DTF 채점용 씬 상태를 기계적으로 추출한다. 주관 평가 없음 - 수치만.
//
// 추출:
//  - 그룹별 MeshRenderer 수, 표면적(월드 bounds 기준 추정), 머티리얼/셰이더/BaseMap 결속 여부
//  - 서로 다른 머티리얼 종수, 텍스처 결속 표면적 비율
//  - 광원 수·종류, 이미시브 머티리얼 수·면적
//  - 프로그램 요소 존재 여부(이름 매칭)
//  - 앰비언트/라이팅 설정
//
// args: [outJson]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static class DtfExport
{
    static readonly string[] InteriorRoots = {
        "부산역 역사 내부",
        "부산역 역사 내부 · 마감",
        "부산역 역사 내부 · 수직동선",
        "부산역 역사 내부 · 재사용 자산",
        "부산역 역사 내부 · 행선안내",
        "부산역 역사 내부 · 지하 연결",
        "부산역 역사 내부 · 조명기구",
        "부산역 역사 내부 · 사인"
    };

    // P 축 필수 요소 -> 씬 이름에서 찾을 키워드 후보
    static readonly (string element, string[] keys)[] Required = {
        ("매표창구",        new[]{"매표소","매표창구","발권"}),
        ("역무실",          new[]{"역무","역무실"}),
        ("철도경찰서",      new[]{"철도경찰","경찰"}),
        ("종합안내",        new[]{"종합안내","안내 스테이션","안내데스크","인포"}),
        ("수유방",          new[]{"수유방","수유"}),
        ("물품보관함",      new[]{"물품보관함","보관함","락커","Locker"}),
        ("화장실",          new[]{"화장실"}),
        ("에스컬레이터",    new[]{"에스컬레이터"}),
        ("엘리베이터",      new[]{"엘리베이터"}),
        ("계단",            new[]{"계단"}),
        ("타는곳 게이트",   new[]{"게이트"}),
        ("임대매장 2F",     new[]{"2F 임대매장"}),
        ("임대매장 1F",     new[]{"1F 임대매장"}),
        ("임대매장 3F",     new[]{"3F 임대매장"}),
        ("코레일라운지",    new[]{"라운지"}),
        ("행선안내 전광판", new[]{"행선안내"}),
        ("자동발권기",      new[]{"발권기","키오스크","Ticket"}),
        ("대합실 의자",     new[]{"벤치","의자","Bench"}),
        ("소화기",          new[]{"소화기"}),
        ("비상구",          new[]{"비상구","피난"}),
    };

    public static void Main(string[] args)
    {
        string outPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/dtf-scene-export.json";

        var groupArea = new Dictionary<string, double>();
        var groupCount = new Dictionary<string, int>();
        var groupTexArea = new Dictionary<string, double>();
        var matNames = new HashSet<string>();
        var matTextured = new HashSet<string>();
        var allNames = new List<string>();

        double totalArea = 0, texArea = 0, emissiveArea = 0, emissiveFaceArea = 0;
        int rendererCount = 0, emissiveMats = 0, emissiveRenderers = 0;
        var emissiveMatNames = new HashSet<string>();

        foreach (var rootName in InteriorRoots)
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                rendererCount++;
                allNames.Add(PathOf(mr.transform));
                string grp = GroupOf(mr.transform, rootName);
                var b = mr.bounds.size;
                // 박스 6면 표면적 추정. 얇은 판은 큰 두 면만 실질 가시면이므로 보수적으로 2*최대면 + 측면
                double a = 2.0 * (b.x * b.y + b.y * b.z + b.z * b.x);
                totalArea += a;
                Bump(groupArea, grp, a);
                BumpI(groupCount, grp, 1);

                bool anyTex = false;
                bool anyEmissive = false;
                foreach (var m in mr.sharedMaterials)
                {
                    if (m == null) continue;
                    matNames.Add(m.name);
                    bool hasTex = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                    if (!hasTex && m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) hasTex = true;
                    if (hasTex) { anyTex = true; matTextured.Add(m.name); }
                    if (m.IsKeywordEnabled("_EMISSION") ||
                        (m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.05f) ||
                        (m.shader != null && m.shader.name.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        if (emissiveMatNames.Add(m.name)) emissiveMats++;
                        if (!anyEmissive) { anyEmissive = true; emissiveRenderers++; emissiveArea += a; emissiveFaceArea += Math.Max(b.x * b.y, Math.Max(b.y * b.z, b.z * b.x)); }
                    }
                }
                // 발광면은 마감이 끝난 표면으로 본다(BaseMap 이 없어도 무지 회색이 아니다)
                if (anyTex || anyEmissive) { texArea += a; Bump(groupTexArea, grp, a); }
            }
        }

        // 사인 실측 (계획이 아니라 씬에 실재하는 것만)
        int tenantFascia = 0, wayfinding = 0, emergencyExit = 0, platformNumber = 0, departureBoard = 0;
        var signRoot = GameObject.Find("부산역 역사 내부 · 사인");
        if (signRoot != null)
            foreach (Transform kindT in signRoot.transform)
            {
                int n = 0;
                foreach (Transform item in kindT)
                {
                    bool textured = false;
                    foreach (var r in item.GetComponentsInChildren<MeshRenderer>(true))
                        foreach (var m in r.sharedMaterials)
                            if (m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) { textured = true; break; }
                    if (textured) n++;
                }
                if (kindT.name == "tenantFascia") tenantFascia = n;
                else if (kindT.name == "wayfinding") wayfinding = n;
                else if (kindT.name == "emergencyExit") emergencyExit = n;
                else if (kindT.name == "platformNumber") platformNumber = n;
                else if (kindT.name == "departureBoard") departureBoard = n;
            }

        // 광원
        int pointLights = 0, spotLights = 0, dirLights = 0, shadowLights = 0;
        double lumenProxy = 0;
        foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (!l.enabled || !l.gameObject.activeInHierarchy) continue;
            if (l.type == LightType.Point) pointLights++;
            else if (l.type == LightType.Spot) spotLights++;
            else if (l.type == LightType.Directional) dirLights++;
            if (l.shadows != LightShadows.None) shadowLights++;
            lumenProxy += l.intensity;
        }

        // 프로그램 요소 - 씬 전체 경로 문자열로 검사(별도 루트의 소화기 등 포함)
        foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            allNames.Add(g.name);
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) allNames.Add(t.name);
        }
        var program = new List<string>();
        foreach (var (element, keys) in Required)
        {
            int hits = 0;
            foreach (var n in allNames)
                foreach (var k in keys)
                    if (n.IndexOf(k, StringComparison.Ordinal) >= 0) { hits++; break; }
            program.Add("{\"element\":" + Q(element) + ",\"hits\":" + hits + ",\"present\":" + (hits > 0 ? "true" : "false") + "}");
        }

        var sb = new StringBuilder();
        sb.Append("{\n \"schema\":\"chooguard.dtf-scene-export.v1\",\n");
        sb.Append(" \"exportedAt\":").Append(Q(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))).Append(",\n");
        sb.Append(" \"scene\":").Append(Q(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)).Append(",\n");
        sb.Append(" \"renderers\":").Append(rendererCount).Append(",\n");
        sb.Append(" \"surfaceAreaM2\":").Append(F(totalArea)).Append(",\n");
        sb.Append(" \"texturedAreaM2\":").Append(F(texArea)).Append(",\n");
        sb.Append(" \"texturedAreaRatio\":").Append(F(totalArea > 0 ? texArea / totalArea : 0)).Append(",\n");
        sb.Append(" \"distinctMaterials\":").Append(matNames.Count).Append(",\n");
        sb.Append(" \"distinctTexturedMaterials\":").Append(matTextured.Count).Append(",\n");
        sb.Append(" \"emissiveMaterials\":").Append(emissiveMats).Append(",\n");
        sb.Append(" \"emissiveRenderers\":").Append(emissiveRenderers).Append(",\n");
        sb.Append(" \"emissiveLuminousAreaM2\":").Append(F(emissiveFaceArea)).Append(",\n");
        sb.Append(" \"emissiveMaterialNames\":[").Append(string.Join(",", emissiveMatNames.OrderBy(x => x).Select(Q))).Append("],\n");
        sb.Append(" \"emissiveAreaM2\":").Append(F(emissiveArea)).Append(",\n");
        sb.Append(" \"lights\":{\"point\":").Append(pointLights).Append(",\"spot\":").Append(spotLights)
          .Append(",\"directional\":").Append(dirLights).Append(",\"withShadows\":").Append(shadowLights)
          .Append(",\"intensitySum\":").Append(F(lumenProxy)).Append("},\n");
        sb.Append(" \"ambient\":{\"mode\":").Append(Q(RenderSettings.ambientMode.ToString()))
          .Append(",\"intensity\":").Append(F(RenderSettings.ambientIntensity))
          .Append(",\"sky\":").Append(C(RenderSettings.ambientSkyColor))
          .Append(",\"equator\":").Append(C(RenderSettings.ambientEquatorColor))
          .Append(",\"ground\":").Append(C(RenderSettings.ambientGroundColor)).Append("},\n");
        sb.Append(" \"signage\":{\"tenantFascia\":").Append(tenantFascia).Append(",\"wayfinding\":").Append(wayfinding).Append(",\"emergencyExit\":").Append(emergencyExit).Append(",\"platformNumber\":").Append(platformNumber).Append(",\"departureBoard\":").Append(departureBoard).Append("},\n");
        sb.Append(" \"program\":[").Append(string.Join(",", program)).Append("],\n");

        sb.Append(" \"groups\":[");
        bool first = true;
        foreach (var kv in groupArea.OrderByDescending(k => k.Value))
        {
            if (!first) sb.Append(",");
            first = false;
            double ta = groupTexArea.TryGetValue(kv.Key, out var t) ? t : 0;
            sb.Append("{\"group\":").Append(Q(kv.Key))
              .Append(",\"count\":").Append(groupCount[kv.Key])
              .Append(",\"areaM2\":").Append(F(kv.Value))
              .Append(",\"texturedAreaM2\":").Append(F(ta))
              .Append(",\"texturedRatio\":").Append(F(kv.Value > 0 ? ta / kv.Value : 0))
              .Append("}");
        }
        sb.Append("],\n");
        sb.Append(" \"materialList\":[").Append(string.Join(",", matNames.OrderBy(x => x).Select(Q))).Append("]\n}");

        var full = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString());
        Debug.Log("DTF_EXPORT renderers=" + rendererCount + " area=" + totalArea.ToString("F0")
                  + " texRatio=" + (totalArea > 0 ? texArea / totalArea : 0).ToString("F3")
                  + " mats=" + matNames.Count);
    }

    static string GroupOf(Transform t, string rootName)
    {
        var parts = new List<string>();
        var cur = t;
        while (cur != null && cur.name != rootName) { parts.Add(cur.name); cur = cur.parent; }
        parts.Reverse();
        // 루트 바로 아래 2단계까지를 그룹으로 본다
        if (parts.Count == 0) return rootName;
        if (parts.Count == 1) return rootName + "/" + parts[0];
        return rootName + "/" + parts[0] + "/" + parts[1];
    }

    static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        var cur = t.parent;
        while (cur != null) { sb.Insert(0, cur.name + "/"); cur = cur.parent; }
        return sb.ToString();
    }

    static void Bump(Dictionary<string, double> d, string k, double v) { d[k] = d.TryGetValue(k, out var x) ? x + v : v; }
    static void BumpI(Dictionary<string, int> d, string k, int v) { d[k] = d.TryGetValue(k, out var x) ? x + v : v; }
    static string F(double v) => v.ToString("F4", CultureInfo.InvariantCulture);
    static string C(Color c) => "[" + F(c.r) + "," + F(c.g) + "," + F(c.b) + "]";
    static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ") + "\"";
}
