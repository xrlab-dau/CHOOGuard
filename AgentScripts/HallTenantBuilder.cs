// 부산역 2층 맞이방 점포열·매표소·타는곳 게이트 신축.
// 좌표는 전부 hall-tenants.json (계측 홀 가장자리에서 보간) 에서 온다. 임의 좌표 없음.
//
// 점포 1구획 구성 (영상 6vxUvCQ5_rY, ref_hall2f_concourse_* 판독):
//   y 7.00~10.20  유리 전면 + 업종별 실내 배경
//   y 10.20~12.20 다크 차콜 파사드 밴드 + 백라이트 상호 간판
//   측벽/배면      화강석 건식패널
//
// args: [tenantsJson, signDir]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class HallTenantBuilder
{
    const string GenDir = "Assets/ChooGuard/Art/StationInterior/Generated";
    const string RootName = "부산역 2층 맞이방 · 신축";
    const float Theta = 16.2f;

    static float S, C;
    static Vector3 W(float u, float v, float y)
        => new Vector3(u * S + v * C, y, u * C - v * S);

    class Buf
    {
        public List<Vector3> V = new(); public List<Vector3> N = new();
        public List<Vector2> U = new(); public List<int> T = new();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uu = 1f, float vv = 1f, bool flipU = false)
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
        public Mesh Build(string n)
        {
            var m = new Mesh { name = n };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, U); m.SetTriangles(T, 0);
            m.RecalculateBounds(); m.RecalculateTangents(); return m;
        }
        public bool Empty => V.Count == 0;
    }

    public static void Main(string[] args)
    {
        string js = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/hall-tenants.json";
        string signDir = args != null && args.Length > 1 && !string.IsNullOrEmpty(args[1])
            ? args[1] : "Assets/ChooGuard/Art/StationInterior/Textures/Tenants";

        float th = Theta * Mathf.Deg2Rad; S = Mathf.Sin(th); C = Mathf.Cos(th);

        var raw = File.ReadAllText(Path.GetFullPath(js));
        float floorY = Num(raw, "\"floorY\"", 7f);
        float fy0 = Num(raw, "\"fasciaY0\"", 10.2f);
        float fy1 = Num(raw, "\"fasciaY1\"", 12.2f);

        var bays = ParseBays(raw);
        if (bays.Count == 0) { Debug.LogError("TENANT bays 0"); return; }

        var root = GameObject.Find(RootName) ?? new GameObject(RootName);
        var old = root.transform.Find("점포열");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var shopRoot = new GameObject("점포열");
        shopRoot.transform.SetParent(root.transform, false);

        var matPanel = Mk("Tenant_SidePanel", m =>
        {
            // 실사 맞이방 점포 측벽은 밝은 회백색 도장/패널이다. 화강석은 갈색으로 떠 보인다.
            var t = Tex("PaintedPlaster017", "Color"); if (t) m.SetTexture("_BaseMap", t);
            var n = Tex("PaintedPlaster017", "NormalGL"); if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            m.SetColor("_BaseColor", new Color(0.90f, 0.90f, 0.89f));
            m.SetFloat("_Smoothness", 0.18f);
            m.SetFloat("_Metallic", 0f);
        });
        var matFasciaBase = Mk("Tenant_FasciaBand", m =>
        {
            m.SetColor("_BaseColor", new Color(0.16f, 0.16f, 0.18f));
            m.SetFloat("_Smoothness", 0.40f); m.SetFloat("_Metallic", 0.1f);
        });

        int built = 0, signed = 0;
        foreach (var b in bays)
        {
            float u0 = b.u0, u1 = b.u1, dep = b.depth * b.inward;
            float v0 = b.v0, v1 = b.v1;              // 전면선 (계측 가장자리)
            float bv0 = v0 + dep, bv1 = v1 + dep;    // 배면선

            var go = new GameObject("점포 · " + b.name);
            go.transform.SetParent(shopRoot.transform, false);

            // 점포는 계측 가장자리(v0)에서 홀 쪽으로 depth 만큼 차지한다.
            // 따라서 보이는 전면은 안쪽 끝(fv), 배면은 가장자리(v0).
            float fv0 = v0 + dep, fv1 = v1 + dep;

            // 유리 전면
            var glass = new Buf();
            AddFace(glass, u0, fv0, u1, fv1, floorY, fy0, b.inward);
            Place(go, "전면유리", glass, MatGlassFront(), false);

            // 실내 배경 (업종별) — 전면에서 1.4m 안쪽
            var back = new Buf();
            AddFace(back, u0, fv0 - 1.4f * b.inward, u1, fv1 - 1.4f * b.inward, floorY, fy0, b.inward, true);
            Place(go, "실내배경", back, MatShopInterior(b.cat), false);

            // 파사드 밴드 + 간판
            var fascia = new Buf();
            AddFace(fascia, u0, fv0, u1, fv1, fy0, fy1, b.inward, true);
            var signTex = LoadSign(signDir, "sign_" + b.slug);
            Material fm = signTex != null ? MkSign(b.slug, signTex) : matFasciaBase;
            if (signTex != null) signed++;
            Place(go, "파사드간판", fascia, fm, false);

            // 측벽 2매 + 배면 (가장자리 쪽)
            var side = new Buf();
            AddFace(side, u0, v0, u0, fv0, floorY, fy1, b.inward);
            AddFace(side, u1, fv1, u1, v1, floorY, fy1, b.inward);
            AddFace(side, u0, v0, u1, v1, floorY, fy1, -b.inward);
            // 상부 소핏 - 위에서 박스 속이 보이지 않게 덮는다
            side.Quad(W(u0, v0, fy1), W(u1, v1, fy1), W(u1, fv1, fy1), W(u0, fv0, fy1),
                      Mathf.Abs(u1 - u0) / 3f, b.depth / 3f, false);
            Place(go, "측벽배면", side, matPanel, true);
            built++;
        }

        // 매표소
        BuildTicketOffice(root, raw, floorY, signDir);
        // 타는 곳 게이트
        BuildGates(root, raw, floorY, signDir);

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("HALL_TENANTS bays=" + built + " signed=" + signed);
    }

    // stretch=true 이면 텍스처를 면 전체에 1회 펼친다(간판). false 면 3m 주기로 반복한다(재질).
    static void AddFace(Buf buf, float ua, float va, float ub, float vb, float y0, float y1, float inward, bool stretch = false)
    {
        Vector3 a = W(ua, va, y0), b = W(ub, vb, y0), c = W(ub, vb, y1), d = W(ua, va, y1);
        float uu = stretch ? 1f : Vector3.Distance(a, b) / 3f;
        float vv = stretch ? 1f : (y1 - y0) / 3f;
        // inward>0 이면 법선이 +v 를 향해야 홀 쪽에서 보인다. 되감으면 UV 가 거울이 되므로 flipU 로 되돌린다.
        if (inward > 0) buf.Quad(b, a, d, c, uu, vv, true);
        else buf.Quad(a, b, c, d, uu, vv, true);
    }

    static void BuildTicketOffice(GameObject root, string raw, float floorY, string signDir)
    {
        float u0 = Num(raw, "\"ticketOffice\"[u0]", -8f, "\"ticketOffice\"", "\"u0\"");
        float u1 = Num(raw, "\"ticketOffice\"[u1]", 16f, "\"ticketOffice\"", "\"u1\"");
        var old = root.transform.Find("매표소");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("매표소");
        go.transform.SetParent(root.transform, false);

        // 매표소는 선로측 계측 가장자리(v=+6) 안쪽에 놓인다. 배면이 홀 밖으로 나가면 안 된다.
        float vBack = 6.0f, depth = 12f, vFront = vBack - depth;
        var wall = new Buf();
        AddFace(wall, u0, vBack, u1, vBack, floorY, 12.5f, -1f);
        Place(go, "배면벽", wall, Mk("Ticket_BackWall", m => {
            m.SetColor("_BaseColor", new Color(0.18f, 0.20f, 0.26f)); m.SetFloat("_Smoothness", 0.35f); }), true);

        // 창구 카운터 10개
        var cnt = new Buf();
        int n = 10; float w = (u1 - u0) / n;
        for (int i = 0; i < n; i++)
        {
            float a = u0 + i * w + 0.3f, b = u0 + (i + 1) * w - 0.3f;
            AddFace(cnt, a, vFront, b, vFront, floorY, floorY + 1.05f, -1f);
            AddFace(cnt, a, vFront, a, vFront + 0.8f, floorY, floorY + 1.05f, -1f);
        }
        Place(go, "창구카운터", cnt, Mk("Ticket_Counter", m => {
            var t = Tex("WoodFloor064", "Color"); if (t) m.SetTexture("_BaseMap", t);
            m.SetColor("_BaseColor", new Color(0.72f, 0.60f, 0.46f)); m.SetFloat("_Smoothness", 0.45f); }), true);

        // 매표소 유도사인
        var sign = new Buf();
        AddFace(sign, u0 + 2f, vFront + 0.2f, u0 + 10f, vFront + 0.2f, 12.6f, 14.0f, -1f, true);
        var tex = LoadSign(signDir, "guide_tickets");
        Place(go, "매표소사인", sign, tex != null ? MkSign("guide_tickets", tex) : Mk("Guide_Navy", m => {
            m.SetColor("_BaseColor", new Color(0.06f, 0.23f, 0.42f)); }), false);
    }

    static void BuildGates(GameObject root, string raw, float floorY, string signDir)
    {
        float u0 = Num(raw, "\"gates\"[u0]", 20f, "\"gates\"", "\"u0\"");
        float u1 = Num(raw, "\"gates\"[u1]", 48f, "\"gates\"", "\"u1\"");
        var old = root.transform.Find("타는 곳 게이트");
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("타는 곳 게이트");
        go.transform.SetParent(root.transform, false);

        float v = 3.0f;          // 게이트는 계측 가장자리 안쪽
        int lanes = 12; float pitch = (u1 - u0) / lanes;
        var piers = new Buf();
        for (int i = 0; i <= lanes; i++)
        {
            float a = u0 + i * pitch - 0.35f, b = u0 + i * pitch + 0.35f;
            AddFace(piers, a, v, b, v, floorY, floorY + 1.15f, -1f);
            AddFace(piers, b, v, b, v + 1.6f, floorY, floorY + 1.15f, -1f);
            AddFace(piers, a, v + 1.6f, a, v, floorY, floorY + 1.15f, -1f);
            AddFace(piers, a, v + 1.6f, b, v + 1.6f, floorY, floorY + 1.15f, +1f);
        }
        Place(go, "개집표기", piers, Mk("Gate_Pier", m => {
            m.SetColor("_BaseColor", new Color(0.86f, 0.87f, 0.89f));
            m.SetFloat("_Smoothness", 0.55f); m.SetFloat("_Metallic", 0.3f); }), true);

        // 머리 위 '타는 곳' 청색 사인
        var head = new Buf();
        AddFace(head, u0 + 1f, v - 0.4f, u1 - 1f, v - 0.4f, 11.6f, 13.6f, -1f, true);
        var t1 = LoadSign(signDir, "guide_platforms");
        Place(go, "타는곳사인", head, t1 != null ? MkSign("guide_platforms", t1) : Mk("Guide_Navy2", m => {
            m.SetColor("_BaseColor", new Color(0.06f, 0.23f, 0.42f)); }), false);

        // 녹색 LED 행선안내 전광판 3매
        var boards = new Buf();
        for (int i = 0; i < 3; i++)
        {
            float a = u0 + 2f + i * 9f, b = a + 7.5f;
            AddFace(boards, a, v - 0.6f, b, v - 0.6f, 13.8f, 15.7f, -1f, true);
        }
        var t2 = LoadSign(signDir, "board_departure_1");
        Place(go, "행선안내전광판", boards, t2 != null ? MkEmissive("board_departure_1", t2) : Mk("Board_Dark", m => {
            m.SetColor("_BaseColor", new Color(0.03f, 0.04f, 0.03f)); }), false);
    }

    static void Place(GameObject parent, string name, Buf buf, Material mat, bool collide)
    {
        if (buf.Empty) return;
        var mesh = buf.Build(parent.name.Replace(" ", "_") + "_" + name);
        string path = GenDir + "/Tenants/" + Sanitize(mesh.name) + ".asset";
        Directory.CreateDirectory(GenDir + "/Tenants");
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
        foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        return sb.ToString();
    }

    static Texture2D LoadSign(string dir, string baseName)
        => AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + baseName + ".png");

    static Material MkSign(string slug, Texture t) => Mk("Sign_" + slug, m =>
    {
        m.SetTexture("_BaseMap", t);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.20f);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", t);
        m.SetColor("_EmissionColor", Color.white * 0.75f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    });

    static Material MkEmissive(string slug, Texture t) => Mk("Emis_" + slug, m =>
    {
        m.SetTexture("_BaseMap", t);
        m.SetColor("_BaseColor", Color.white);
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", t);
        m.SetColor("_EmissionColor", Color.white * 2.2f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    });

    static Material MatGlassFront() => Mk("Tenant_Glass", m =>
    {
        m.SetColor("_BaseColor", new Color(0.78f, 0.84f, 0.88f, 0.22f));
        m.SetFloat("_Smoothness", 0.94f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3100;
    });

    static Material MatShopInterior(string cat)
    {
        string file = cat switch
        {
            "cafe" => "front_cafe",
            "bakery" => "front_bakery",
            "convenience" => "front_convenience",
            "snack" => "front_fishcake_snack",
            "pharmacy" => "front_pharmacy",
            _ => "front_souvenir"
        };
        return Mk("ShopInt_" + cat, m =>
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ChooGuard/Art/StationInterior/Textures/Tenants/" + file + ".png");
            if (t) { m.SetTexture("_BaseMap", t); m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", t); m.SetColor("_EmissionColor", Color.white * 0.55f); }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.15f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        });
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

    static Texture Tex(string sub, string needle)
    {
        foreach (var g in AssetDatabase.FindAssets(sub + " t:Texture"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p.Contains(needle)) return AssetDatabase.LoadAssetAtPath<Texture>(p);
        }
        return null;
    }

    class Bay
    {
        public string name, slug, cat, side;
        public float u0, u1, v0, v1, depth, inward;
    }

    static List<Bay> ParseBays(string raw)
    {
        var res = new List<Bay>();
        int k = raw.IndexOf("\"bays\"", StringComparison.Ordinal);
        if (k < 0) return res;
        int i = k;
        while (true)
        {
            int o = raw.IndexOf('{', i); if (o < 0) break;
            int c = raw.IndexOf('}', o); if (c < 0) break;
            var seg = raw.Substring(o, c - o + 1);
            if (!seg.Contains("\"u0\"")) { i = c + 1; continue; }
            res.Add(new Bay
            {
                name = Str(seg, "\"name\""),
                slug = Str(seg, "\"slug\""),
                cat = Str(seg, "\"cat\""),
                side = Str(seg, "\"side\""),
                u0 = F(seg, "\"u0\""), u1 = F(seg, "\"u1\""),
                v0 = F(seg, "\"vEdge0\""), v1 = F(seg, "\"vEdge1\""),
                depth = F(seg, "\"depthM\""), inward = F(seg, "\"inward\"")
            });
            i = c + 1;
        }
        return res;
    }

    static string Str(string s, string key)
    {
        int k = s.IndexOf(key, StringComparison.Ordinal); if (k < 0) return "";
        int a = s.IndexOf('"', k + key.Length + 1); if (a < 0) return "";
        int b = s.IndexOf('"', a + 1); if (b < 0) return "";
        return s.Substring(a + 1, b - a - 1);
    }

    static float F(string s, string key)
    {
        int k = s.IndexOf(key, StringComparison.Ordinal); if (k < 0) return 0f;
        int a = k + key.Length;
        while (a < s.Length && (s[a] == ':' || s[a] == ' ')) a++;
        int b = a;
        while (b < s.Length && (char.IsDigit(s[b]) || s[b] == '-' || s[b] == '.' || s[b] == '+' || s[b] == 'e')) b++;
        return float.TryParse(s.Substring(a, b - a), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }

    static float Num(string raw, string label, float def, string scope = null, string key = null)
    {
        if (scope == null) return F(raw, label);
        int k = raw.IndexOf(scope, StringComparison.Ordinal); if (k < 0) return def;
        int c = raw.IndexOf('}', k); if (c < 0) return def;
        var seg = raw.Substring(k, c - k + 1);
        int kk = seg.IndexOf(key, StringComparison.Ordinal); if (kk < 0) return def;
        return F(seg.Substring(kk), key);
    }
}
