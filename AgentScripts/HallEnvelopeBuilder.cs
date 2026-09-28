// 부산역 2층 맞이방 내부 외피 신축.
// 원본 셸은 단면(single-sided) 메시라 실내에서 벽·지붕이 컬링된다.
// 따라서 계측된 바닥 셀 집합의 경계에 '안쪽을 향한' 라이너 벽과 천장을 세운다.
//
// 구성 (영상 6vxUvCQ5_rY 판독 기준):
//   - 하부 벽 y 7.00~11.50 : 화강석 건식패널 (점포 전면 배경)
//   - 고측창 y 11.50~20.80 : 유리 커튼월 (주간 채광)
//   - 지붕 데크 y 21.60    : 백색 금속 데크
//   - 스페이스프레임 y 19.6~21.6 : 상현/하현 격자 + 사재
//
// args: [cellsJson, floorY, lowTop, clerTop, deckY, bayM, memberM]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class HallEnvelopeBuilder
{
    const string GenDir = "Assets/ChooGuard/Art/StationInterior/Generated";
    const string RootName = "부산역 2층 맞이방 · 신축";
    const float Cell = 2f;
    const float ThetaDeg = 16.2f;      // 역사 장축 방위

    static float P(string[] a, int i, float d)
        => a != null && i < a.Length && !string.IsNullOrEmpty(a[i])
           ? float.Parse(a[i], CultureInfo.InvariantCulture) : d;

    class MeshBuf
    {
        public List<Vector3> V = new(); public List<Vector3> N = new();
        public List<Vector2> U = new(); public List<int> T = new();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            N.Add(n); N.Add(n); N.Add(n); N.Add(n);
            U.Add(ua); U.Add(ub); U.Add(uc); U.Add(ud);
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }
        public void Box(Vector3 c, Vector3 half, Vector3 axisX, Vector3 axisZ)
        {
            var ax = axisX.normalized * half.x;
            var ay = Vector3.up * half.y;
            var az = axisZ.normalized * half.z;
            Vector3 p000 = c - ax - ay - az, p100 = c + ax - ay - az,
                    p110 = c + ax + ay - az, p010 = c - ax + ay - az,
                    p001 = c - ax - ay + az, p101 = c + ax - ay + az,
                    p111 = c + ax + ay + az, p011 = c - ax + ay + az;
            Vector2 z0 = Vector2.zero, z1 = new(1, 0), z2 = new(1, 1), z3 = new(0, 1);
            Quad(p000, p010, p110, p100, z0, z3, z2, z1);
            Quad(p101, p111, p011, p001, z0, z3, z2, z1);
            Quad(p001, p011, p010, p000, z0, z3, z2, z1);
            Quad(p100, p110, p111, p101, z0, z3, z2, z1);
            Quad(p010, p011, p111, p110, z0, z3, z2, z1);
            Quad(p000, p100, p101, p001, z0, z3, z2, z1);
        }
        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, U); m.SetTriangles(T, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
        public int Count => V.Count;
    }

    public static void Main(string[] args)
    {
        string cellsJson = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0])
            ? args[0] : ".planning/2026-09-22-station-interior-build/hall-floor-cells.json";
        float floorY = P(args, 1, 7.00f);
        float lowTop = P(args, 2, 11.50f);
        float clerTop = P(args, 3, 20.80f);
        float deckY = P(args, 4, 21.60f);
        float bay = P(args, 5, 6.0f);
        float mem = P(args, 6, 0.16f);

        var cells = ReadCells(cellsJson);
        if (cells.Count == 0) { Debug.LogError("ENV 셀 0"); return; }
        var set = new HashSet<long>();
        foreach (var p in cells) set.Add(Key(p.x, p.y));

        float xMin = 1e9f, xMax = -1e9f, zMin = 1e9f, zMax = -1e9f;
        foreach (var p in cells)
        {
            xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
            zMin = Mathf.Min(zMin, p.y); zMax = Mathf.Max(zMax, p.y);
        }
        snapX0 = xMin; snapZ0 = zMin;

        // 볼트 범위: 셀들의 v 최소/최대
        float thq = ThetaDeg * Mathf.Deg2Rad;
        vaultV0 = 1e9f; vaultV1 = -1e9f;
        foreach (var p in cells)
        {
            float v = p.x * Mathf.Cos(thq) - p.y * Mathf.Sin(thq);
            vaultV0 = Mathf.Min(vaultV0, v); vaultV1 = Mathf.Max(vaultV1, v);
        }
        vaultRise = P(args, 8, 3.2f);

        var low = new MeshBuf();
        var cler = new MeshBuf();
        float h = Cell * 0.5f;

        // 경계 간선마다 안쪽을 향한 벽면을 만든다
        foreach (var p in cells)
        {
            TryWall(set, low, cler, p, +1, 0, h, floorY, lowTop, clerTop);
            TryWall(set, low, cler, p, -1, 0, h, floorY, lowTop, clerTop);
            TryWall(set, low, cler, p, 0, +1, h, floorY, lowTop, clerTop);
            TryWall(set, low, cler, p, 0, -1, h, floorY, lowTop, clerTop);
        }

        // 지붕 데크 — 실내에서 보이도록 아래를 향하게 감는다
        var deck = new MeshBuf();
        foreach (var p in cells)
        {
            deck.Quad(new Vector3(p.x - h, VaultY(deckY, p.x - h, p.y + h), p.y + h),
                      new Vector3(p.x + h, VaultY(deckY, p.x + h, p.y + h), p.y + h),
                      new Vector3(p.x + h, VaultY(deckY, p.x + h, p.y - h), p.y - h),
                      new Vector3(p.x - h, VaultY(deckY, p.x - h, p.y - h), p.y - h),
                      new Vector2((p.x - h) / 1.2f, (p.y + h) / 1.2f), new Vector2((p.x + h) / 1.2f, (p.y + h) / 1.2f),
                      new Vector2((p.x + h) / 1.2f, (p.y - h) / 1.2f), new Vector2((p.x - h) / 1.2f, (p.y - h) / 1.2f));
        }

        // 원형 기둥 — 실사 판독 주열 피치 9.0m 의 배수. 홀 개방감을 위해 기본 18m.
        float colBay = P(args, 7, 18.0f);
        var cols = new MeshBuf();
        int colN = 0;
        for (float x = xMin + colBay * 0.5f; x <= xMax + 0.01f; x += colBay)
        for (float z = zMin + colBay * 0.5f; z <= zMax + 0.01f; z += colBay)
        {
            if (!Near(set, x, z, Cell)) continue;
            Cylinder(cols, new Vector3(x, floorY, z), 0.55f, deckY - 2.10f - floorY, 16);
            colN++;
        }

        // 스페이스프레임: 상현 y=deckY-0.25, 하현 y=deckY-2.1, 반 베이 엇갈림
        var truss = new MeshBuf();
        float topY = deckY - 0.25f, botY = deckY - 2.10f;
        var topN = new List<Vector3>(); var botN = new List<Vector3>();
        for (float x = xMin; x <= xMax + 0.01f; x += bay)
        for (float z = zMin; z <= zMax + 0.01f; z += bay)
            if (Near(set, x, z, bay)) topN.Add(new Vector3(x, VaultY(topY, x, z), z));
        for (float x = xMin + bay * 0.5f; x <= xMax + 0.01f; x += bay)
        for (float z = zMin + bay * 0.5f; z <= zMax + 0.01f; z += bay)
            if (Near(set, x, z, bay)) botN.Add(new Vector3(x, VaultY(botY, x, z), z));

        int chords = 0;
        foreach (var a in topN)
        foreach (var b in topN)
        {
            if (b.x < a.x || (Mathf.Approximately(b.x, a.x) && b.z <= a.z)) continue;
            float d = Vector3.Distance(a, b);
            if (d > bay * 1.05f) continue;
            Member(truss, a, b, mem); chords++;
        }
        foreach (var a in botN)
        foreach (var b in botN)
        {
            if (b.x < a.x || (Mathf.Approximately(b.x, a.x) && b.z <= a.z)) continue;
            float d = Vector3.Distance(a, b);
            if (d > bay * 1.05f) continue;
            Member(truss, a, b, mem); chords++;
        }
        int diags = 0;
        foreach (var b in botN)
        foreach (var a in topN)
        {
            if (Mathf.Abs(a.x - b.x) > bay * 0.55f || Mathf.Abs(a.z - b.z) > bay * 0.55f) continue;
            Member(truss, b, a, mem * 0.8f); diags++;
        }

        // 자산 저장 + 씬 배치
        Directory.CreateDirectory(GenDir);
        var root = GameObject.Find(RootName) ?? new GameObject(RootName);

        Place(root, "벽 · 화강석건식패널", low.Build("HallWallLow"), MatGranitePanel());
        Place(root, "고측창 · 유리커튼월", cler.Build("HallClerestory"), MatGlass());
        Place(root, "지붕데크 · 백색금속", deck.Build("HallRoofDeck"), MatDeck());
        Place(root, "스페이스프레임 트러스", truss.Build("HallSpaceFrame"), MatSteel());
        Place(root, "기둥 · 원형 백색", cols.Build("HallColumns"), MatColumn());

        // 천창 - 볼트 마루 근처 장축 3줄. 실사의 주광 유입부.
        var sky = new MeshBuf();
        float thS = ThetaDeg * Mathf.Deg2Rad, ss = Mathf.Sin(thS), cs = Mathf.Cos(thS);
        float vMid = (vaultV0 + vaultV1) * 0.5f;
        int skyN = 0;
        foreach (var off in new[] { -9f, 0f, 9f })
        for (float u = -58f; u <= 50f; u += 2f)
        {
            float v = vMid + off;
            Vector3 A = new(u * ss + (v - 1.1f) * cs, 0, u * cs - (v - 1.1f) * ss);
            Vector3 B = new((u + 2f) * ss + (v - 1.1f) * cs, 0, (u + 2f) * cs - (v - 1.1f) * ss);
            Vector3 Cc = new((u + 2f) * ss + (v + 1.1f) * cs, 0, (u + 2f) * cs - (v + 1.1f) * ss);
            Vector3 D = new(u * ss + (v + 1.1f) * cs, 0, u * cs - (v + 1.1f) * ss);
            if (!Near(set, A.x, A.z, Cell * 2f)) continue;
            A.y = VaultY(deckY - 0.05f, A.x, A.z); B.y = VaultY(deckY - 0.05f, B.x, B.z);
            Cc.y = VaultY(deckY - 0.05f, Cc.x, Cc.z); D.y = VaultY(deckY - 0.05f, D.x, D.z);
            sky.Quad(D, Cc, B, A, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            skyN++;
        }
        Place(root, "천창", sky.Build("HallSkylights"), MatSkylight());
        Debug.Log("HALL_SKYLIGHT quads=" + skyN + " rise=" + vaultRise);

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("HALL_ENV lowV=" + low.Count + " clerV=" + cler.Count + " deckV=" + deck.Count
                  + " trussV=" + truss.Count + " topNodes=" + topN.Count + " botNodes=" + botN.Count
                  + " chords=" + chords + " diags=" + diags);
        Debug.Log("HALL_COLUMNS n=" + colN + " bay=" + bay);
    }


    // ── 배럴볼트: 장축에 직교하는 v 방향으로 솟는다 (실사 맞이방 지붕 형상) ──
    static float vaultV0, vaultV1, vaultRise;
    static float VaultY(float baseY, float x, float z)
    {
        float th = ThetaDeg * Mathf.Deg2Rad;
        float v = x * Mathf.Cos(th) - z * Mathf.Sin(th);
        if (vaultV1 - vaultV0 < 1e-3f) return baseY;
        float t = Mathf.Clamp01((v - vaultV0) / (vaultV1 - vaultV0));
        return baseY + vaultRise * Mathf.Sin(t * Mathf.PI);
    }

    // 셀은 xMin/zMin 기준 2m 간격 홀수 격자다. 그 격자에 스냅해서 조회한다.
    static float snapX0, snapZ0;
    static bool Near(HashSet<long> set, float x, float z, float bay)
    {
        float sx = snapX0 + Mathf.Round((x - snapX0) / Cell) * Cell;
        float sz = snapZ0 + Mathf.Round((z - snapZ0) / Cell) * Cell;
        int span = Mathf.CeilToInt(bay * 0.5f / Cell);
        for (int i = -span; i <= span; i++)
        for (int j = -span; j <= span; j++)
            if (set.Contains(Key(sx + i * Cell, sz + j * Cell))) return true;
        return false;
    }

    // 원통 기둥 (옆면만, 실내에서 보이는 방향)
    static void Cylinder(MeshBuf buf, Vector3 baseCentre, float r, float height, int seg)
    {
        for (int i = 0; i < seg; i++)
        {
            float a0 = Mathf.PI * 2f * i / seg, a1 = Mathf.PI * 2f * (i + 1) / seg;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
            var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 b0 = baseCentre + d0 * r, b1 = baseCentre + d1 * r;
            Vector3 t0 = b0 + Vector3.up * height, t1 = b1 + Vector3.up * height;
            int k = buf.V.Count;
            buf.V.Add(b0); buf.V.Add(t0); buf.V.Add(t1); buf.V.Add(b1);
            buf.N.Add(d0); buf.N.Add(d0); buf.N.Add(d1); buf.N.Add(d1);
            buf.U.Add(new Vector2(i / (float)seg * 3.5f, 0));
            buf.U.Add(new Vector2(i / (float)seg * 3.5f, height / 2f));
            buf.U.Add(new Vector2((i + 1) / (float)seg * 3.5f, height / 2f));
            buf.U.Add(new Vector2((i + 1) / (float)seg * 3.5f, 0));
            buf.T.Add(k); buf.T.Add(k + 1); buf.T.Add(k + 2);
            buf.T.Add(k); buf.T.Add(k + 2); buf.T.Add(k + 3);
        }
    }

    static Material MatSkylight() => Mk("Hall_Skylight", m =>
    {
        m.SetColor("_BaseColor", new Color(0.96f, 0.97f, 1.00f));
        m.SetFloat("_Smoothness", 0.85f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(1.00f, 0.99f, 0.96f) * 2.6f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    });

    static Material MatColumn() => Mk("Hall_Column_White", m =>
    {
        var t = Tex("PaintedPlaster017", "Color"); if (t) m.SetTexture("_BaseMap", t);
        m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.91f));
        m.SetFloat("_Smoothness", 0.25f); m.SetFloat("_Metallic", 0f);
    });

    static void Member(MeshBuf buf, Vector3 a, Vector3 b, float r)
    {
        var mid = (a + b) * 0.5f;
        var dir = b - a;
        float len = dir.magnitude;
        if (len < 0.01f) return;
        dir /= len;
        var upRef = Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up;
        var side = Vector3.Normalize(Vector3.Cross(dir, upRef));
        var up2 = Vector3.Normalize(Vector3.Cross(side, dir));
        int i = buf.V.Count;
        Vector3[] ring = { side * r + up2 * r, side * r - up2 * r, -side * r - up2 * r, -side * r + up2 * r };
        for (int k = 0; k < 4; k++) { buf.V.Add(mid - dir * (len * 0.5f) + ring[k]); buf.N.Add(ring[k].normalized); buf.U.Add(new Vector2(k * 0.25f, 0)); }
        for (int k = 0; k < 4; k++) { buf.V.Add(mid + dir * (len * 0.5f) + ring[k]); buf.N.Add(ring[k].normalized); buf.U.Add(new Vector2(k * 0.25f, len)); }
        for (int k = 0; k < 4; k++)
        {
            int k2 = (k + 1) % 4;
            buf.T.Add(i + k); buf.T.Add(i + 4 + k); buf.T.Add(i + 4 + k2);
            buf.T.Add(i + k); buf.T.Add(i + 4 + k2); buf.T.Add(i + k2);
        }
    }

    static void TryWall(HashSet<long> set, MeshBuf low, MeshBuf cler, Vector2 p,
                        int dx, int dz, float h, float y0, float y1, float y2)
    {
        float nx = p.x + dx * Cell, nz = p.y + dz * Cell;
        if (set.Contains(Key(nx, nz))) return;

        // 간선 양 끝점 (안쪽에서 보이도록 winding 결정)
        Vector3 a, b;
        if (dx != 0)
        {
            float ex = p.x + dx * h;
            a = new Vector3(ex, 0, p.y - h * dx); b = new Vector3(ex, 0, p.y + h * dx);
        }
        else
        {
            float ez = p.y + dz * h;
            a = new Vector3(p.x + h * dz, 0, ez); b = new Vector3(p.x - h * dz, 0, ez);
        }
        low.Quad(new Vector3(a.x, y0, a.z), new Vector3(b.x, y0, b.z),
                 new Vector3(b.x, y1, b.z), new Vector3(a.x, y1, a.z),
                 new Vector2(0, 0), new Vector2(Cell / 1.2f, 0), new Vector2(Cell / 1.2f, (y1 - y0) / 1.2f), new Vector2(0, (y1 - y0) / 1.2f));
        cler.Quad(new Vector3(a.x, y1, a.z), new Vector3(b.x, y1, b.z),
                  new Vector3(b.x, y2, b.z), new Vector3(a.x, y2, a.z),
                  new Vector2(0, 0), new Vector2(Cell / 1.5f, 0), new Vector2(Cell / 1.5f, (y2 - y1) / 1.5f), new Vector2(0, (y2 - y1) / 1.5f));
    }

    // 셀 좌표는 홀수 미터 격자다. Cell 로 나누면 반올림이 인접 셀을 뭉갠다 - 정수 미터로 키를 만든다.
    static long Key(float x, float z)
        => (long)(Mathf.RoundToInt(x) + 100000) * 1000000L + (Mathf.RoundToInt(z) + 100000);

    static void Place(GameObject root, string name, Mesh mesh, Material mat)
    {
        string path = GenDir + "/" + mesh.name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var old = root.transform.Find(name);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (name.StartsWith("벽") || name.StartsWith("고측창"))
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    static Material Mk(string file, Action<Material> cfg)
    {
        string p = GenDir + "/" + file + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, p); }
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

    static Material MatGranitePanel() => Mk("Hall_Wall_GranitePanel", m =>
    {
        var t = Tex("Granite005A", "Color"); if (t) m.SetTexture("_BaseMap", t);
        var n = Tex("Granite005A", "NormalGL"); if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
        m.SetColor("_BaseColor", new Color(0.80f, 0.79f, 0.77f));
        m.SetFloat("_Smoothness", 0.30f); m.SetFloat("_Metallic", 0f);
    });

    static Material MatGlass() => Mk("Hall_Clerestory_Glass", m =>
    {
        m.SetColor("_BaseColor", new Color(0.72f, 0.80f, 0.86f, 0.30f));
        m.SetFloat("_Smoothness", 0.95f); m.SetFloat("_Metallic", 0.1f);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
    });

    static Material MatDeck() => Mk("Hall_RoofDeck_White", m =>
    {
        var t = Tex("SheetMetal001", "Color"); if (t) m.SetTexture("_BaseMap", t);
        m.SetColor("_BaseColor", new Color(0.90f, 0.91f, 0.92f));
        m.SetFloat("_Smoothness", 0.35f); m.SetFloat("_Metallic", 0.15f);
    });

    static Material MatSteel() => Mk("Hall_SpaceFrame_Steel", m =>
    {
        m.SetColor("_BaseColor", new Color(0.93f, 0.93f, 0.94f));
        m.SetFloat("_Smoothness", 0.55f); m.SetFloat("_Metallic", 0.35f);
    });

    static List<Vector2> ReadCells(string path)
    {
        var res = new List<Vector2>();
        var json = File.ReadAllText(Path.GetFullPath(path));
        int k = json.IndexOf("\"cells\"", StringComparison.Ordinal);
        if (k < 0) return res;
        int i = json.IndexOf('[', k) + 1;
        while (true)
        {
            int a = json.IndexOf('[', i); if (a < 0) break;
            int b = json.IndexOf(']', a); if (b < 0) break;
            var parts = json.Substring(a + 1, b - a - 1).Split(',');
            if (parts.Length >= 2)
                res.Add(new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture),
                                    float.Parse(parts[1], CultureInfo.InvariantCulture)));
            i = b + 1;
            int close = json.IndexOf(']', i), nextOpen = json.IndexOf('[', i);
            if (nextOpen < 0 || (close >= 0 && close < nextOpen)) break;
        }
        return res;
    }
}
