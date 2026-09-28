// 부산역 2층 맞이방 집기·조명·승객 배치.
// Jev 판정 parity_driver=prop_density_human_scale 에 따른 밀도/인체척도 담당.
// 영상 6vxUvCQ5_rY 판독:
//   - 천장 매달림 LCD 열차정보 모니터 (게이트 전면 상부)
//   - 원형 종합안내 아일랜드 + 링 벤치
//   - 대기 벤치 열
//   - 황색 선형 점자블록 유도로
//   - 승객 다수 (빌보드)
//   - 천장 매입 조명 + 트러스 하부 조명
//
// args: [tenantsJson, benchRows, passengerCount, lightRows]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class HallFitoutBuilder
{
    const string GenDir = "Assets/ChooGuard/Art/StationInterior/Generated";
    const string RootName = "부산역 2층 맞이방 · 신축";
    const string TexDir = "Assets/ChooGuard/Art/StationInterior/Textures";
    const float Theta = 16.2f;
    static float S, C;

    static Vector3 W(float u, float v, float y) => new Vector3(u * S + v * C, y, u * C - v * S);
    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    public static void Main(string[] args)
    {
        float th = Theta * Mathf.Deg2Rad; S = Mathf.Sin(th); C = Mathf.Cos(th);
        float floorY = 7.00f;
        int passengers = Mathf.RoundToInt(P(args, 2, 90f));

        var root = GameObject.Find(RootName);
        if (root == null) { Debug.LogError("루트 없음"); return; }

        Reset(root, "집기·조명");
        var fit = new GameObject("집기·조명");
        fit.transform.SetParent(root.transform, false);

        BuildLights(fit, floorY);
        BuildTactile(fit, floorY);
        BuildInfoIsland(fit, floorY);
        BuildBenches(fit, floorY);
        BuildHangingMonitors(fit, floorY);
        BuildPassengers(fit, floorY, passengers);

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("HALL_FITOUT done");
    }

    static void Reset(GameObject root, string name)
    {
        var old = root.transform.Find(name);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    }

    // ── 조명: 트러스 하부 다운라이트 격자 + 전역 노출 보정 ──────────────
    static void BuildLights(GameObject parent, float floorY)
    {
        var go = new GameObject("조명");
        go.transform.SetParent(parent.transform, false);

        int n = 0;
        for (float u = -56; u <= 48; u += 13f)
        for (float v = -42; v <= 6; v += 12f)
        {
            var p = W(u, v, 18.5f);
            if (!Physics.Raycast(p, Vector3.down, out var hit, 14f)) continue;
            if (Mathf.Abs(hit.point.y - floorY) > 0.6f) continue;
            var l = new GameObject("다운라이트 " + n);
            l.transform.SetParent(go.transform, false);
            l.transform.position = p;
            var li = l.AddComponent<Light>();
            li.type = LightType.Point;
            li.range = 26f;
            li.intensity = 1.15f;
            li.color = new Color(1.0f, 0.96f, 0.90f);
            li.shadows = LightShadows.None;
            n++;
        }

        // 홀 전역 필 라이트 (그림자 담당)
        var key = new GameObject("주광 · 고측창 유입");
        key.transform.SetParent(go.transform, false);
        key.transform.position = W(-6, -18, 20f);
        key.transform.rotation = Quaternion.Euler(52f, 196f, 0f);
        var kl = key.AddComponent<Light>();
        kl.type = LightType.Directional;
        kl.intensity = 0.55f;
        kl.color = new Color(1.0f, 0.97f, 0.93f);
        kl.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.36f, 0.39f, 0.44f);
        RenderSettings.ambientEquatorColor = new Color(0.26f, 0.27f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.15f, 0.16f);
        RenderSettings.ambientIntensity = 0.62f;
        BuildPostVolume();
        Debug.Log("HALL_LIGHTS point=" + n);
    }

    // ── 황색 선형 점자블록 유도로 ────────────────────────────────
    static void BuildTactile(GameObject parent, float floorY)
    {
        var buf = new Buf();
        // 홀 장축을 따라 중앙 유도로 + 게이트 분기
        AddStrip(buf, -56, -18, 48, -18, floorY + 0.012f, 0.60f);
        AddStrip(buf, -6, -18, -6, 4, floorY + 0.012f, 0.60f);
        AddStrip(buf, 30, -18, 30, 4, floorY + 0.012f, 0.60f);
        var mat = Mk("Fitout_Tactile", m =>
        {
            var t = Tex("TactilePaving002", "Color"); if (t) m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", new Color(1.00f, 0.82f, 0.10f));
            m.SetFloat("_Smoothness", 0.30f);
        });
        Place(parent, "점자블록 유도로", buf, mat, false);
    }

    static void AddStrip(Buf buf, float u0, float v0, float u1, float v1, float y, float w)
    {
        var d = new Vector2(u1 - u0, v1 - v0);
        float len = d.magnitude; if (len < 0.01f) return;
        d /= len;
        var nrm = new Vector2(-d.y, d.x) * (w * 0.5f);
        Vector3 a = W(u0 - nrm.x, v0 - nrm.y, y), b = W(u1 - nrm.x, v1 - nrm.y, y);
        Vector3 c = W(u1 + nrm.x, v1 + nrm.y, y), e = W(u0 + nrm.x, v0 + nrm.y, y);
        buf.QuadUp(a, b, c, e, len / 0.3f, w / 0.3f);
    }

    // ── 원형 종합안내 아일랜드 ──────────────────────────────────
    static void BuildInfoIsland(GameObject parent, float floorY)
    {
        var buf = new Buf();
        var ctr = W(-24, -6, floorY);          // 보행 유도로(v=-18)에서 12m 이격
        Ring(buf, ctr, 4.2f, 1.15f, 28);       // 카운터
        Ring(buf, ctr, 5.6f, 0.45f, 32);       // 링 벤치 좌대
        var mat = Mk("Fitout_Island", m =>
        {
            var t = Tex("Granite005A", "Color"); if (t) m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", new Color(0.86f, 0.85f, 0.83f));
            m.SetFloat("_Smoothness", 0.45f);
        });
        Place(parent, "종합안내 아일랜드", buf, mat, true);

        // 안내 사인 패널 4면
        var sg = new Buf();
        for (int i = 0; i < 4; i++)
        {
            float a0 = i * 90f * Mathf.Deg2Rad, a1 = a0 + 60f * Mathf.Deg2Rad;
            Vector3 p0 = ctr + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 1.8f + Vector3.up * 2.6f;
            Vector3 p1 = ctr + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 1.8f + Vector3.up * 2.6f;
            sg.Quad(p0, p1, p1 + Vector3.up * 1.4f, p0 + Vector3.up * 1.4f, 1f, 1f, false);
        }
        var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Tenants/guide_information.png");
        Place(parent, "종합안내 사인", sg, MkEmis("Guide_Info", t2, 1.0f), false);
    }

    // 링(도넛) 모양: 바깥면 + 상부 환형 천판. 가운데는 비워 통행 가능하게 둔다.
    static void Ring(Buf buf, Vector3 c, float r, float h, int seg)
    {
        float inner = Mathf.Max(0.4f, r - 1.25f);
        for (int i = 0; i < seg; i++)
        {
            float a0 = Mathf.PI * 2 * i / seg, a1 = Mathf.PI * 2 * (i + 1) / seg;
            Vector3 d0 = new(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 o0 = c + d0 * r, o1 = c + d1 * r;
            Vector3 i0 = c + d0 * inner, i1 = c + d1 * inner;
            buf.Quad(o0, o1, o1 + Vector3.up * h, o0 + Vector3.up * h, r / 2f, h / 2f, false);
            buf.Quad(i1, i0, i0 + Vector3.up * h, i1 + Vector3.up * h, r / 2f, h / 2f, false);
            buf.QuadUp(o0 + Vector3.up * h, o1 + Vector3.up * h, i1 + Vector3.up * h, i0 + Vector3.up * h, 1, 1);
        }
    }

    // ── 대기 벤치 열 ────────────────────────────────────────────
    // StationBench4.fbx 는 단일 벤치가 아니라 196개 묶음 모델이라 인스턴스화하면
    // 렌더러가 폭증하고 척도가 무너진다. 실측 치수로 직접 생성한다.
    // 공항·역사 대기의자 표준: 좌판 폭 2.95m, 깊이 0.62m, 좌고 0.45m, 등받이 0.80m
    static void BuildBenches(GameObject parent, float floorY)
    {
        var buf = new Buf();
        int n = 0;
        float[] rows = { -30f, -26f, -12f, -8f };
        float th = Theta * Mathf.Deg2Rad;
        foreach (var v in rows)
        for (float u = -46; u <= 40; u += 4.4f)
        {
            var p = W(u, v, floorY);
            if (!Clear(p)) continue;
            bool faceNorth = v < -20f;
            float sgn = faceNorth ? 1f : -1f;
            // 좌판
            BoxUV(buf, u - 1.475f, u + 1.475f, v - 0.31f, v + 0.31f, floorY + 0.40f, floorY + 0.45f);
            // 다리 2쌍
            BoxUV(buf, u - 1.30f, u - 1.15f, v - 0.26f, v + 0.26f, floorY, floorY + 0.40f);
            BoxUV(buf, u + 1.15f, u + 1.30f, v - 0.26f, v + 0.26f, floorY, floorY + 0.40f);
            // 등받이
            BoxUV(buf, u - 1.475f, u + 1.475f, v + sgn * 0.26f, v + sgn * 0.32f, floorY + 0.45f, floorY + 0.80f);
            n++;
        }
        var mat = Mk("Fitout_Bench", m =>
        {
            var t = Tex("Metal050A", "Color"); if (t) m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", new Color(0.30f, 0.32f, 0.36f));
            m.SetFloat("_Smoothness", 0.55f); m.SetFloat("_Metallic", 0.45f);
        });
        Place(parent, "대기 벤치", buf, mat, true);
        Debug.Log("HALL_BENCH n=" + n + " verts=" + buf.V.Count);
    }

    // uv 축 정렬 박스 (6면)
    static void BoxUV(Buf buf, float u0, float u1, float v0, float v1, float y0, float y1)
    {
        Vector3 a0 = W(u0, v0, y0), b0 = W(u1, v0, y0), c0 = W(u1, v1, y0), d0 = W(u0, v1, y0);
        Vector3 a1 = W(u0, v0, y1), b1 = W(u1, v0, y1), c1 = W(u1, v1, y1), d1 = W(u0, v1, y1);
        buf.QuadUp(a1, b1, c1, d1, 1, 1);                     // 위
        buf.Quad(a0, b0, b1, a1, 1, 1, false);                // -v
        buf.Quad(c0, d0, d1, c1, 1, 1, false);                // +v
        buf.Quad(b0, c0, c1, b1, 1, 1, false);                // +u
        buf.Quad(d0, a0, a1, d1, 1, 1, false);                // -u
    }

    // ── 천장 매달림 LCD 열차정보 모니터 ──────────────────────────
    static void BuildHangingMonitors(GameObject parent, float floorY)
    {
        var buf = new Buf();
        var frame = new Buf();
        int n = 0;
        for (float u = -44; u <= 40; u += 11f)
        {
            float v = -2.0f;
            // 행거
            frame.Quad(W(u - 0.08f, v, 14.2f), W(u + 0.08f, v, 14.2f),
                       W(u + 0.08f, v, 17.6f), W(u - 0.08f, v, 17.6f), 1, 1, false);
            // 화면 (홀 쪽을 향함 = -v)
            Vector3 a = W(u - 1.5f, v, 12.4f), b = W(u + 1.5f, v, 12.4f);
            Vector3 c = W(u + 1.5f, v, 14.2f), d = W(u - 1.5f, v, 14.2f);
            buf.Quad(a, b, c, d, 1f, 1f, true);
            n++;
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/Tenants/board_departure_2.png");
        Place(parent, "열차정보 모니터", buf, MkEmis("Monitor_Train", tex, 2.4f), false);
        Place(parent, "모니터 행거", frame, Mk("Monitor_Hanger", m => {
            m.SetColor("_BaseColor", new Color(0.24f, 0.24f, 0.26f)); m.SetFloat("_Metallic", 0.5f); }), false);
        Debug.Log("HALL_MONITORS n=" + n);
    }

    // ── 승객 빌보드 ─────────────────────────────────────────────
    static void BuildPassengers(GameObject parent, float floorY, int count)
    {
        var go = new GameObject("승객");
        go.transform.SetParent(parent.transform, false);
        // 실사 컷아웃(pax_*) 우선. 전신 비율(높이/폭 1.5~4.0)만 채택한다.
        var texs = new List<Texture2D>();
        var aspects = new List<float>();
        var seatedTex = new List<Texture2D>();
        var seatedAr = new List<float>();
        foreach (var guid in AssetDatabase.FindAssets("pax_ t:Texture2D",
                 new[] { TexDir + "/Passengers" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null || t.width < 8) continue;
            float ar = t.height / (float)t.width;
            if (ar < 1.5f || ar > 4.0f) continue;
            // 착석 컷아웃은 산포 대상이 아니다 - 벤치 위에 따로 앉힌다
            if (path.Contains("pax_seated_")) { seatedTex.Add(t); seatedAr.Add(ar); continue; }
            texs.Add(t); aspects.Add(ar);
        }
        if (texs.Count == 0)
        {
            for (int i = 1; i <= 4; i++)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    TexDir + "/Passengers/passenger_0" + i +
                    (i == 1 ? "_rolling_luggage" : i == 2 ? "_standing_phone" : i == 3 ? "_bench_seated" : "_walking_commuter") + ".png");
                if (t != null) { texs.Add(t); aspects.Add(2.2f); }
            }
        }
        if (texs.Count == 0) { Debug.LogWarning("승객 텍스처 없음"); return; }
        Debug.Log("HALL_PAX_TEX n=" + texs.Count);

        var mats = new List<Material>();
        for (int i = 0; i < texs.Count; i++) mats.Add(MkCutout("Passenger_" + i, texs[i]));

        var rnd = new System.Random(20260923);
        int n = 0, guard = 0;
        while (n < count && guard < count * 40)
        {
            guard++;
            float u = -56f + (float)rnd.NextDouble() * 104f;
            float v = -44f + (float)rnd.NextDouble() * 50f;
            var p = W(u, v, floorY);
            if (!Clear(p + Vector3.up * 0.05f)) continue;
            if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 6f)) continue;
            if (Mathf.Abs(hit.point.y - floorY) > 0.5f) continue;

            float hgt = 1.62f + (float)rnd.NextDouble() * 0.20f;
            int pick = rnd.Next(texs.Count);
            float wid = hgt / Mathf.Max(0.8f, aspects[pick]);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "승객 " + n;
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(go.transform, false);
            q.transform.position = new Vector3(p.x, floorY + hgt * 0.5f, p.z);
            q.transform.localScale = new Vector3(wid, hgt, 1f);
            q.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
            q.GetComponent<MeshRenderer>().sharedMaterial = mats[pick];
            var bb = q.AddComponent<ChooGuard.World.BillboardY>();
            n++;
        }
        Debug.Log("HALL_PASSENGERS n=" + n);

        // 착석 승객 - 벤치 열 좌판 위. 좌고 0.45m, 착석 실루엣 전고 약 1.30m.
        int sn = 0;
        if (seatedTex.Count > 0)
        {
            var smats = new List<Material>();
            for (int i = 0; i < seatedTex.Count; i++) smats.Add(MkCutout("PaxSeated_" + i, seatedTex[i]));
            float[] rows = { -30f, -26f, -12f, -8f };
            foreach (var v in rows)
            for (float u = -45f; u <= 39f; u += 4.4f)
            {
                if (rnd.NextDouble() > 0.42) continue;
                var seat = W(u + (float)(rnd.NextDouble() - 0.5) * 1.6f, v, floorY);
                if (!Physics.Raycast(seat + Vector3.up * 3f, Vector3.down, out var sh2, 6f)) continue;
                int pick2 = rnd.Next(seatedTex.Count);
                float hh = 1.28f + (float)rnd.NextDouble() * 0.10f;
                float ww = hh / Mathf.Max(0.8f, seatedAr[pick2]);
                var q2 = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q2.name = "착석승객 " + sn;
                UnityEngine.Object.DestroyImmediate(q2.GetComponent<Collider>());
                q2.transform.SetParent(go.transform, false);
                q2.transform.position = new Vector3(seat.x, floorY + hh * 0.5f, seat.z);
                q2.transform.localScale = new Vector3(ww, hh, 1f);
                q2.transform.rotation = Quaternion.Euler(0, Theta + (v < -20f ? 180f : 0f), 0);
                q2.GetComponent<MeshRenderer>().sharedMaterial = smats[pick2];
                sn++;
            }
        }
        Debug.Log("HALL_SEATED n=" + sn);
    }


    // FBX 임포트 배율이 제각각이라 실측 길이에 맞춰 스케일을 보정한다.
    static void FitLength(GameObject go, float targetLongestM)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest < 1e-4f) return;
        float k = targetLongestM / longest;
        go.transform.localScale = go.transform.localScale * k;
    }

    // URP 전역 포스트 볼륨: ACES 톤매핑 + 노출 보정으로 화이트아웃 제거
    static void BuildPostVolume()
    {
        var existing = GameObject.Find("맞이방 포스트 볼륨");
        if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        var go = new GameObject("맞이방 포스트 볼륨");
        var vol = go.AddComponent<UnityEngine.Rendering.Volume>();
        vol.isGlobal = true;
        vol.priority = 10f;

        var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        prof.name = "HallPostProfile";
        string pp = GenDir + "/HallPostProfile.asset";
        AssetDatabase.DeleteAsset(pp);
        Directory.CreateDirectory(GenDir);
        AssetDatabase.CreateAsset(prof, pp);

        var tm = prof.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
        tm.mode.overrideState = true;
        tm.mode.value = UnityEngine.Rendering.Universal.TonemappingMode.ACES;

        var ca = prof.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
        ca.postExposure.overrideState = true;
        ca.postExposure.value = -0.55f;
        ca.contrast.overrideState = true;
        ca.contrast.value = 12f;
        ca.saturation.overrideState = true;
        ca.saturation.value = 6f;

        var bl = prof.Add<UnityEngine.Rendering.Universal.Bloom>(true);
        bl.threshold.overrideState = true; bl.threshold.value = 1.05f;
        bl.intensity.overrideState = true; bl.intensity.value = 0.45f;

        vol.sharedProfile = prof;
        EditorUtility.SetDirty(prof);
        Debug.Log("HALL_POSTVOLUME ACES + exposure -0.55");
    }

    static bool Clear(Vector3 p)
        => !Physics.CheckSphere(p + Vector3.up * 1.0f, 0.75f, ~0, QueryTriggerInteraction.Ignore);

    // ── 메시 유틸 ───────────────────────────────────────────────
    class Buf
    {
        public List<Vector3> V = new(); public List<Vector3> N = new();
        public List<Vector2> U = new(); public List<int> T = new();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uu, float vv, bool flipU)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            N.Add(n); N.Add(n); N.Add(n); N.Add(n);
            float lo = flipU ? uu : 0f, hi = flipU ? 0f : uu;
            U.Add(new Vector2(lo, 0)); U.Add(new Vector2(hi, 0));
            U.Add(new Vector2(hi, vv)); U.Add(new Vector2(lo, vv));
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }
        public void QuadUp(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uu, float vv)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            for (int k = 0; k < 4; k++) N.Add(Vector3.up);
            U.Add(new Vector2(0, 0)); U.Add(new Vector2(uu, 0));
            U.Add(new Vector2(uu, vv)); U.Add(new Vector2(0, vv));
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }
        public bool Empty => V.Count == 0;
        public Mesh Build(string n)
        {
            var m = new Mesh { name = n };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, U); m.SetTriangles(T, 0);
            m.RecalculateBounds(); m.RecalculateTangents(); return m;
        }
    }

    static void Place(GameObject parent, string name, Buf buf, Material mat, bool collide)
    {
        if (buf.Empty) return;
        var mesh = buf.Build("Fitout_" + Sanitize(name));
        Directory.CreateDirectory(GenDir + "/Fitout");
        string path = GenDir + "/Fitout/" + mesh.name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static string Sanitize(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        return sb.ToString();
    }

    static Material Mk(string file, Action<Material> cfg)
    {
        string p = GenDir + "/" + file + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            Directory.CreateDirectory(GenDir);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, p);
        }
        cfg(m); EditorUtility.SetDirty(m); return m;
    }

    static Material MkEmis(string file, Texture t, float e) => Mk(file, m =>
    {
        if (t != null) { m.SetTexture("_BaseMap", t); m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", t); m.SetColor("_EmissionColor", Color.white * e); }
        m.SetColor("_BaseColor", Color.white);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    });

    static Material MkCutout(string file, Texture t) => Mk(file, m =>
    {
        m.SetTexture("_BaseMap", t);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetFloat("_Smoothness", 0.05f);
        m.renderQueue = 2450;
    });

    static Texture Tex(string sub, string needle)
    {
        foreach (var g in AssetDatabase.FindAssets(sub + " t:Texture"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains(needle)) return AssetDatabase.LoadAssetAtPath<Texture>(p);
        }
        return null;
    }
}
