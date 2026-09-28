// Video-era Busan Station 2F. Geometry categories are observed; metric placement is inferred.
// Run in Edit mode via unity run_script. Owns only this root, VideoSecondFloor assets and receipt.
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class VideoFloorTwo
{
    const string RootName = "영상복원 · 2층";
    const string Art = "Assets/ChooGuard/Art/StationInterior/VideoSecondFloor";
    const string Base = "Assets/ChooGuard/Art/StationInterior/";
    const string Plan = ".planning/2026-09-23-video-twin/";
    const string Restaurant = Base + "Kits/restaurant-bits/KayKit_Restaurant_Bits_1.0_FREE/Assets/fbx (unity)/";
    const float Floor = 7f;
    const float Theta = 16.2f;
    static Transform root;
    static Font font;
    static Shader lit;
    static readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
    static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
    static readonly Dictionary<Material, Material> propMaterials = new Dictionary<Material, Material>();
    static readonly List<Vector2> route = new List<Vector2>();
    static readonly List<object> receipts = new List<object>();
    static Material stone, plaster, steel, white, dark, glass, wood, yellow, blue, led, green, roof;
    static int propCount, skippedProps;
    static readonly float[] OutlineU = { -90, -76, -60, -40, -26, -10, 0, 20, 40, 60, 77 };
    static readonly float[] FrontV = { -23, -32, -39, -43, -44, -44.8f, -43.3f, -41.9f, -40.3f, -35.8f, -25 };
    static readonly float[] TrackV = { 3, 11, 12, 11, 9, 7, 5, 3, 5, 10, 4 };

    // Root rotation maps local (v,y,u) to the shared world basis without reflecting mesh winding.
    static Vector3 P(float u, float v, float y) { return new Vector3(v, y, u); }
    static Vector3 D(float u, float v) { return new Vector3(v, 0, u).normalized; }

    public static void Main(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run VideoFloorTwo in Edit mode.");
        JObject contract = JObject.Parse(File.ReadAllText(Plan + "construction-contract.json"));
        JArray opening = (JArray)contract["sharedCore"]["floorOpeningUV"];
        if ((float)opening[0] != 3.4f || (float)opening[1] != 10.3f ||
            (float)opening[2] != -33.5f || (float)opening[3] != -19.5f)
            throw new InvalidOperationException("Shared opening contract changed; update this builder before execution.");
        lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) throw new InvalidOperationException("URP Lit is required.");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf");
        if (font == null) throw new InvalidOperationException("Existing Korean signage font is missing.");
        models.Clear(); batches.Clear(); route.Clear(); receipts.Clear(); propMaterials.Clear();
        propCount = 0; skippedProps = 0;
        // Resolve every acquired asset before replacing any scene content. Never substitute primitives.
        Load("bench", Base + "Kits/Web/WaitingBench.fbx");
        Load("shelf", Base + "Props/StoreShelf.fbx", "GONDOLA1");
        Load("display", Base + "Props/LedDisplay.fbx");
        Load("kiosk", Base + "Kits/Web/KoreanDigitalKiosk.fbx");
        Load("ticket", Base + "Kits/Web/TicketVendingMachine.fbx");
        Load("counter", Base + "Kits/Web/TicketCounter.fbx");
        Load("locker", Base + "Kits/Web/LuggageLocker.fbx");
        Load("bin", Base + "Kits/Web/TrashBin.fbx");
        Load("table", Restaurant + "table_round_A.fbx");
        Load("chair", Restaurant + "chair_A.fbx");
        Load("food", Restaurant + "crate_buns.fbx");
        foreach (JObject camera in (JArray)JObject.Parse(File.ReadAllText(Plan + "registered-video-cameras.json"))["cameras"])
        {
            float seconds = (float)camera["seconds"];
            if (seconds < 78 || seconds > 170) continue;
            JArray feet = (JArray)camera["uvFeet"];
            route.Add(new Vector2((float)feet[0], (float)feet[1]));
        }
        if (route.Count < 2) throw new InvalidOperationException("Registered 2F video route missing.");
        EnsureFolder(Art);
        Materials();
        GameObject previous = GameObject.Find(RootName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        root = new GameObject(RootName).transform;
        root.rotation = Quaternion.Euler(0, Theta, 0);
        Floors();
        Envelope();
        Entry();
        HighHall();
        Mezzanine();
        Information();
        Retail();
        InferredRemainder();
        Furniture();
        foreach (Batch batch in batches.Values) batch.Save(root);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        var receipt = new
        {
            schema = "chooguard.video-floor-two.build.v1", state = "BUILT_NOT_VISUALLY_VERIFIED",
            root = RootName, source = "https://www.youtube.com/watch?v=6vxUvCQ5_rY",
            epoch = "Uploaded 2022-01-01; capture date unconfirmed, not 2026 as-built",
            dimensionsStatus = "ALL_ABSOLUTE_DIMENSIONS_INFERRED; no surveyed dimensions claimed",
            registeredBasis = "SfM relative trajectory; absolute scale/anchor authorized assumptions in construction-contract.json",
            floorY = Floor, longAxisDegrees = Theta,
            sharedOpeningUV = new[] { 3.4f, 10.3f, -33.5f, -19.5f },
            openingOwnership = "VideoFloorOne: both 1F/2F escalators, well and guards. This builder creates no shared core fixtures.",
            outline = new { u = OutlineU, frontV = FrontV, trackV = TrackV,
                basis = "Source MainShell facade knots supplied in contract; end caps and track-side interpolation inferred, not rectangular guide bbox" },
            observed = new[] { "78s glass exit9 vestibule; not fare gates", "90s low soffit/KAKAO FRIENDS/upper escalator/paired green screens",
                "98s white hierarchical tube spaceframe, grey deck, branched supports, long clerestory", "112s column-centered curved white information counter and dark canopy, LOTTERIA above",
                "124s grouped brown/metal seats and open low-arcade connection", "138s B&C, square stainless columns", "158s Storyway and 味香한부산/local-food shop",
                "166/174s low ceiling and shared descent approach" },
            regions = receipts.ToArray(), generatedMeshBatches = batches.Count,
            acquiredPropInstances = propCount, omittedRouteConflictingProps = skippedProps,
            routeClearancePolicy = "Entire ground-level shop footprints must clear registered route plus0.8m; construction stops on any conflict before emitting those walls. Optional props omit conflicts. Main must exercise actual traversal.",
            constructionNotes = new[] { "No floor-cell perimeter-to-wall derivation", "No crowd billboards, storefront photo boxes or full upper floor slab",
                "Static mesh and material assets updated in-place to preserve GUIDs on rerun", "WaitingBench single model reused; StationBench4 bundled station seating deliberately not instantiated",
                "StoreShelf/GONDOLA1 reused as a selected subtree, not entire supermarket", "Scenes are marked dirty, not saved; Main owns integration and global lighting" },
            verification = "NOT_RUN: assigned authoring only. Main must execute builder, remove conflicting source surfaces, inspect registered views, and walk both landings."
        };
        File.WriteAllText(Plan + "floor-two-build.json", JsonConvert.SerializeObject(receipt, Formatting.Indented));
        Debug.Log("VIDEO_FLOOR_TWO_BUILT batches=" + batches.Count + " props=" + propCount + "; visual/traversal verification pending Main.");
    }

    static void Load(string key, string path, string child = null)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Required acquired asset missing: " + path);
        if (child != null)
        {
            Transform found = null;
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                if (t.name == child && t.GetComponentsInChildren<MeshRenderer>(true).Length > 0) { found = t; break; }
            if (found == null) throw new InvalidOperationException("Required acquired subtree absent: " + path + "/" + child);
            prefab = found.gameObject;
        }
        models.Add(key, prefab);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    static Material Mat(string name, Color tint, float metal = 0, float smooth = .35f, string inherited = null)
    {
        Material source = inherited == null ? null : AssetDatabase.LoadAssetAtPath<Material>(Base + "Materials/" + inherited + ".mat");
        if (inherited != null && source == null) throw new InvalidOperationException("Required PBR material absent: " + inherited);
        Material fresh = source != null ? new Material(source) : new Material(lit);
        fresh.name = name;
        fresh.SetColor("_BaseColor", tint); fresh.SetColor("_Color", tint);
        fresh.SetFloat("_Metallic", metal); fresh.SetFloat("_Smoothness", smooth);
        fresh.SetFloat("_Cull", 2);
        return SaveMaterial(name, fresh);
    }

    static Material SaveMaterial(string name, Material fresh)
    {
        string path = Art + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null) { AssetDatabase.CreateAsset(fresh, path); return fresh; }
        EditorUtility.CopySerialized(fresh, existing);
        UnityEngine.Object.DestroyImmediate(fresh); EditorUtility.SetDirty(existing);
        return existing;
    }

    static void Materials()
    {
        stone = Mat("Floor_Granite", new Color(.89f, .90f, .88f), 0, .84f, "PBR_Granite005A_2K");
        stone.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"Textures/Lane/floor_station_polished.png"));
        stone.SetFloat("_BumpScale", .10f);
        plaster = Mat("Soffit_Plaster", new Color(.69f, .70f, .65f), 0, .3f, "PBR_PaintedPlaster017_4K");
        steel = Mat("Stainless_Hairline", new Color(.64f, .69f, .70f), .86f, .77f, "PBR_Metal032_2K");
        steel.SetTexture("_BaseMap", null);
        steel.SetFloat("_BumpScale", .16f);
        white = Mat("Tube_White", new Color(.80f, .82f, .76f), .12f, .35f);
        dark = Mat("Canopy_Dark", new Color(.11f, .13f, .13f), .3f, .42f);
        wood = Mat("Counter_Wood", new Color(.63f, .34f, .16f), 0, .4f, "PBR_WoodFloor064_4K");
        roof = Mat("Roof_Grey", new Color(.30f, .33f, .34f), .55f, .26f, "PBR_Metal050A_4K");
        yellow = Mat("Sign_ExitYellow", new Color(.96f, .74f, .025f));
        blue = Mat("Sign_RailBlue", new Color(.018f, .12f, .31f));
        glass = Mat("Glass_GreenClear", new Color(.36f, .63f, .63f, .18f), .05f, .91f);
        glass.SetFloat("_Surface", 1); glass.SetFloat("_Blend", 0);
        glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite", 0); glass.SetFloat("_Cull", 0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.SetOverrideTag("RenderType", "Transparent"); glass.renderQueue = 3000;
        glass.SetShaderPassEnabled("ShadowCaster", false); EditorUtility.SetDirty(glass);
        led = Mat("Light_Lens", new Color(.93f, .94f, .85f));
        led.EnableKeyword("_EMISSION"); led.SetColor("_EmissionColor", new Color(1.6f, 1.65f, 1.45f));
        led.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        green = Mat("Display_Green", new Color(.015f, .12f, .035f));
        green.EnableKeyword("_EMISSION"); green.SetColor("_EmissionColor", new Color(.015f, .12f, .025f));
        green.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(led); EditorUtility.SetDirty(green);
    }

    static Batch B(string name, Material mat, bool collision = false)
    {
        Batch batch;
        if (!batches.TryGetValue(name, out batch)) { batch = new Batch(name, mat, collision); batches.Add(name, batch); }
        return batch;
    }
    static void Box(string name, Material mat, float u, float v, float y, float du, float dv, float dy, bool collision = false)
    { B(name, mat, collision).Box(P(u, v, y), new Vector3(dv, dy, du)); }
    static void Tube(string name, Material mat, Vector3 a, Vector3 b, float radius, bool collision = false, int sides = 12)
    { B(name, mat, collision).Tube(a, b, radius, sides); }
    static float Edge(float u, float[] values)
    {
        for (int i = 1; i < OutlineU.Length; i++)
            if (u <= OutlineU[i]) return Mathf.Lerp(values[i - 1], values[i], (u - OutlineU[i - 1]) / (OutlineU[i] - OutlineU[i - 1]));
        return values[values.Length - 1];
    }
    static bool Core(float u, float v, float ru = 0, float rv = 0)
    {
        // Shared 1F descent shaft, and the restaurant escalator pair with a1.5m approach (upper-escalator.json).
        return (u + ru > 3.4f && u - ru < 10.3f && v + rv > -33.5f && v - rv < -19.5f)
            || (u + ru > -73.95f && u - ru < -69.65f && v + rv > -28.2f && v - rv < -15.3f);
    }
    static bool Inside(float u, float v) { return u >= -90 && u <= 77 && v >= Edge(u, FrontV) && v <= Edge(u, TrackV); }

    static void Floors()
    {
        var cuts = new SortedSet<float>(OutlineU); cuts.Add(3.4f); cuts.Add(10.3f);
        float previous = -90;
        foreach (float u in cuts)
        {
            if (u == previous) continue;
            float f0 = Edge(previous, FrontV), f1 = Edge(u, FrontV), t0 = Edge(previous, TrackV), t1 = Edge(u, TrackV);
            if (previous >= 3.4f && u <= 10.3f)
            {
                SlabQuad("Floor_SourceFootprint", stone, previous, u, f0, f1, -33.5f, -33.5f, Floor, .28f, true);
                SlabQuad("Floor_SourceFootprint", stone, previous, u, -19.5f, -19.5f, t0, t1, Floor, .28f, true);
            }
            else SlabQuad("Floor_SourceFootprint", stone, previous, u, f0, f1, t0, t1, Floor, .28f, true);
            previous = u;
        }
        // A dedicated connector, not the rectangular station-guide bounding box.
        SlabQuad("Exit9_ConnectorFloor", stone, -76, -61, Edge(-76, TrackV), Edge(-61, TrackV), 29, 29, Floor, .28f, true);
        // Video78 camera has +0.49m SfM vertical drift; Main connects the outside bridge, no invented step here.
        receipts.Add(new { region = "Entire second-floor connected public deck", u = new[] { -90, 77 }, y = Floor,
            observed = "Source facade curvature and video route continuity", inferred = "End caps, interpolated rear edge, thickness0.28m, all absolute dimensions; same-level exit9 connector v29" });
    }

    static void SlabQuad(string name, Material mat, float u0, float u1, float front0, float front1, float back0, float back1, float top, float depth, bool collision)
    {
        Batch b = B(name, mat, collision);
        Vector3 a = P(u0, front0, top), c = P(u1, back1, top), d = P(u0, back0, top), e = P(u1, front1, top);
        b.Quad(a, e, c, d); b.Quad(d - Vector3.up * depth, c - Vector3.up * depth, e - Vector3.up * depth, a - Vector3.up * depth);
        b.Quad(a - Vector3.up * depth, e - Vector3.up * depth, e, a);
        b.Quad(e - Vector3.up * depth, c - Vector3.up * depth, c, e);
        b.Quad(c - Vector3.up * depth, d - Vector3.up * depth, d, c);
        b.Quad(d - Vector3.up * depth, a - Vector3.up * depth, a, d);
    }

    static void RectSlab(string name, Material mat, float u0, float u1, float v0, float v1, float top, float depth, bool collision)
    {
        // Shared opening is subtracted from EVERY generated horizontal slab, including overhead fitout.
        float[] us = { u0, Mathf.Clamp(3.4f, u0, u1), Mathf.Clamp(10.3f, u0, u1), u1 };
        for (int i = 0; i < 3; i++)
        {
            float a = us[i], b = us[i + 1]; if (b - a < .001f) continue;
            if (a >= 3.4f && b <= 10.3f)
            {
                if (v0 < -33.5f) SlabQuad(name, mat, a, b, v0, v0, Mathf.Min(v1, -33.5f), Mathf.Min(v1, -33.5f), top, depth, collision);
                if (v1 > -19.5f) SlabQuad(name, mat, a, b, Mathf.Max(v0, -19.5f), Mathf.Max(v0, -19.5f), v1, v1, top, depth, collision);
            }
            else SlabQuad(name, mat, a, b, v0, v0, v1, v1, top, depth, collision);
        }
    }

    static void Envelope()
    {
        // Explicit exterior only. Interior patch edges at u-30, v11 and all arcade links are NOT walls.
        for (int i = 1; i < OutlineU.Length; i++)
        {
            float u0 = OutlineU[i - 1], u1 = OutlineU[i];
            Vector3 a = P(u0, FrontV[i - 1], Floor), b = P(u1, FrontV[i], Floor);
            FacadeSegment(a, b, 4.2f, "CurvedFront", true);
            // Track-side glazing. The only original connector found behind it is the overtrack-concourse deck at
            // u[-46,-29] (boarding-gate-probe.json); elsewhere probes hit original walls or dropped onto the 1F ceiling.
            float m = (u0 + u1) * .5f;
            if (m < -62) continue; // Exit9 connection is authored separately.
            float g0 = BoardingGateU[0], g1 = BoardingGateU[1];
            if (u1 <= g0 || u0 >= g1) { FacadeSegment(P(u0, TrackV[i - 1], Floor), P(u1, TrackV[i], Floor), 4.2f, "TrackRear", true); continue; }
            if (g0 > u0) FacadeSegment(P(u0, TrackV[i - 1], Floor), P(g0, Edge(g0, TrackV), Floor), 4.2f, "TrackRear", true);
            if (g1 < u1) FacadeSegment(P(g1, Edge(g1, TrackV), Floor), P(u1, TrackV[i], Floor), 4.2f, "TrackRear", true);
            BoardingGate(g0, g1);
        }
        FacadeSegment(P(-90, -23, Floor), P(-90, 3, Floor), 4.2f, "SouthEnd", true);
        FacadeSegment(P(77, -25, Floor), P(77, 4, Floor), 4.2f, "NorthEnd", true);
        // Low outer fringe and continuation; no high roof is stretched over the complete 2F.
        Ceiling(-90, -76, -30, 4, 11.25f, false);
        Ceiling(-76, -30, -44, HallFrontV, 11.25f, false);
        Ceiling(-30, 77, -45, 11, 11.25f, true);
        // Hall-to-retail fascia is overhead only, with an uninterrupted walk-through underneath.
        Box("HallArcade_Fascia", plaster, -24.8f, -11, 10.725f, .45f, 44, 1.05f);
        GlazedBand(-24.8f, HallFrontV, -24.8f, 11, 11.25f, 15.45f, "ArcadeClerestory");
        Batch spandrel = B("HallArcade_UpperSpandrel", plaster);
        for (float v = HallFrontV; v < 11; v += 3)
        {
            float end = Mathf.Min(v + 3, 11);
            Vector3 a = P(-24.8f, v, 15.45f), b = P(-24.8f, end, 15.45f);
            Vector3 c = P(-24.8f, end, RoofY(-24.8f,end)), d = P(-24.8f, v, RoofY(-24.8f,v));
            spandrel.Quad(a,b,c,d); spandrel.Quad(d,c,b,a);
        }
        receipts.Add(new { region = "Exterior / full low corridor", observed = "112s static SfM fascia samples aroundu-24.8,y10.9; upper glass aroundy14.8", inferred = "Rounded relative-landmark fascia/clerestory geometry under inferred7m scale; unseen northern/front strips and platform bays", routeConnection = "u-30 ceiling transition stays open; fascia farther back atu-24.8" });
    }

    // Boarding gate into the original overtrack concourse; VideoStationIntegration cuts the matching doorway
    // through the original track wall. Width4m and head height3.15m are inferred, not surveyed.
    static readonly float[] BoardingGateU = { -40f, -36f };
    static void BoardingGate(float g0, float g1)
    {
        float v0 = Edge(g0, TrackV), v1 = Edge(g1, TrackV), mid = (g0 + g1) * .5f, vm = Edge(mid, TrackV);
        Box("BoardingGate_Frame", steel, g0, v0, Floor + 1.575f, .16f, .16f, 3.15f, true);
        Box("BoardingGate_Frame", steel, g1, v1, Floor + 1.575f, .16f, .16f, 3.15f, true);
        Box("BoardingGate_Frame", steel, mid, vm, Floor + 3.25f, g1 - g0 + .16f, .2f, .2f, false);
        Sign("타는 곳  /  Platforms", mid, vm - .12f, Floor + 3.6f, 3.8f, .57f, 0, -1, blue, Color.white);
    }

    static void FacadeSegment(Vector3 a, Vector3 b, float height, string key, bool collide)
    {
        Vector3 up = Vector3.up * height;
        Batch g = B(key + "_Glass", glass, collide); g.Quad(a, b, b + up, a + up); g.Quad(a + up, b + up, b, a);
        Tube(key + "_Frame", steel, a, b, .065f);
        Tube(key + "_Frame", steel, a + up, b + up, .08f);
        int bays = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2.7f));
        for (int k = 0; k <= bays; k++)
        { Vector3 c = Vector3.Lerp(a, b, (float)k / bays); Tube(key + "_Frame", steel, c, c + up, .055f); }
    }

    static void GlazedBand(float u0, float v0, float u1, float v1, float bottom, float top, string key)
    {
        Vector3 a = P(u0, v0, bottom), b = P(u1, v1, bottom);
        FacadeSegment(a, b, top - bottom, key, false);
        Tube(key + "_Frame", white, a + Vector3.up * (top - bottom) * .52f, b + Vector3.up * (top - bottom) * .52f, .07f);
    }

    static void Entry()
    {
        Ceiling(-76, -61, 11, 23, 11.2f, false, true);
        FacadeSegment(P(-76, 23, Floor), P(-72.1f, 23, Floor), 4.1f, "Exit9", true);
        FacadeSegment(P(-65.9f, 23, Floor), P(-61, 23, Floor), 4.1f, "Exit9", true);
        // Sliding glass leaves parked aside a 6.2m passage, not invisible collider doors.
        FacadeSegment(P(-72.1f, 23.08f, Floor), P(-70.4f, 23.08f, Floor), 2.65f, "Exit9_ParkedDoor", true);
        FacadeSegment(P(-67.6f, 23.08f, Floor), P(-65.9f, 23.08f, Floor), 2.65f, "Exit9_ParkedDoor", true);
        Box("Exit9_Header", steel, -68.5f, 23, 10.85f, 15, .3f, .55f);
        Sign("↑ 9  나가는 곳", -72.5f, 23.2f, 10.15f, 4.5f, .64f, 0, 1, yellow, Color.black);
        Sign("들어오는 곳\nEntrance", -66.3f, 23.2f, 10.15f, 4.4f, .64f, 0, 1, blue, Color.white);
        // Camera90 looks toward-v. Its left is-u; the old+u shop crossed the105->112 walking leg.
        Shop("KAKAO FRIENDS", -74.25f, 14.5f, 7.5f, 3.5f, 1, 0, "shelf", new Color(.98f, .76f, .08f), true);
        for (float v = 12; v <= 21; v += 4.5f)
            Tube("Entry_RoundColumns", white, P(-62, v, Floor), P(-62, v, 11.2f), .34f, true, 20);
        Box("Exit9_Tactile", yellow, -69, 19, 7.013f, .48f, 20, .02f);
        receipts.Add(new { region = "Exit9 / KAKAO entry", observed = "78s glass portal and90s low arcade/KAKAO FRIENDS", inferred = "u[-76,-61],v[11,29],ceiling11.2; door clear gap2.8m; 78s camera vertical drift not built as a step" });
    }

    static float RoofY(float u, float v)
    {
        // 172 static112s roof-member landmarks fit14.199-.0575u-.0250v (RMS .295m).
        // Add .7m from the visible member plane to the inferred outer roof deck.
        return 14.9f - .0575f * u - .025f * v;
    }

    // High-hall front fascia plane: front-fascia-probe.json, 185 static SfM points (frames 90-124s) at
    // fascia/board height, median v-35.06, IQR[-35.61,-34.15], no measurable slope along u (0.0008).
    // Supersedes the earlier inferred v-33; metric scale is still the inferred7m floor spacing.
    const float HallFrontV = -35f;
    // Raised daylight ridge = the3m roof cell containing v-16.
    static readonly float RidgeV0 = HallFrontV + 3 * Mathf.Floor((-16 - HallFrontV) / 3);

    static void HighHall()
    {
        // Hierarchical chord/web members: large longitudinal tubes, smaller panel chords, diagonal tetrahedral webs.
        for (float u = -76; u < -24.8f; u += 3)
        for (float v = HallFrontV; v < 11; v += 3)
        {
            float u1 = Mathf.Min(u + 3, -24.8f), v1 = Mathf.Min(v + 3, 11);
            Vector3 a = P(u, v, RoofY(u,v)), b = P(u1, v, RoofY(u1,v)), c = P(u1, v1, RoofY(u1,v1)), d = P(u, v1, RoofY(u,v1));
            // A single longitudinal raised daylight ridge at v-16, not three emissive stripes.
            bool ridge = Mathf.Approximately(v, RidgeV0);
            if (ridge) { a += Vector3.up*.65f; b += Vector3.up*.65f; c += Vector3.up*.65f; d += Vector3.up*.65f; }
            Batch deck = B(ridge ? "Roof_RidgeGlass" : "Roof_GreyDeck", ridge ? glass : roof);
            deck.Quad(d, c, b, a); deck.Quad(a, b, c, d);
            Tube("Spaceframe_Upper", white, a - Vector3.up * .25f, b - Vector3.up * .25f, .09f);
            Tube("Spaceframe_Upper", white, a - Vector3.up * .25f, d - Vector3.up * .25f, .09f);
            Vector3 lower = P((u + u1) * .5f, (v + v1) * .5f, RoofY((u + u1)*.5f,(v + v1)*.5f) - 1.2f);
            Tube("Spaceframe_Web", white, lower, a - Vector3.up * .25f, .055f);
            Tube("Spaceframe_Web", white, lower, b - Vector3.up * .25f, .055f);
            Tube("Spaceframe_Web", white, lower, c - Vector3.up * .25f, .055f);
            Tube("Spaceframe_Web", white, lower, d - Vector3.up * .25f, .055f);
            if (u + 4.5f < -24.8f) Tube("Spaceframe_Lower", white, lower, P(u + 4.5f, (v + v1) * .5f, RoofY(u+4.5f,(v+v1)*.5f)-1.2f), .09f);
            if (v + 4.5f < 11) Tube("Spaceframe_Lower", white, lower, P((u + u1) * .5f, v + 4.5f, RoofY((u+u1)*.5f,v+4.5f) - 1.2f), .09f);
            Tube("Roof_Seams", roof, a + Vector3.down * .03f, b + Vector3.down * .03f, .045f, false, 6);
        }
        foreach (float v in new[] { -30f, -16f, -2f, 10f })
            Tube("Spaceframe_PrimaryLongitudinal", white, P(-76, v, RoofY(-76,v) - 1.1f), P(-24.8f, v, RoofY(-24.8f,v) - 1.1f), .20f);
        // Deliberately sparse landmark supports; not a world-XZ column forest.
        foreach (Vector2 uv in new[] { new Vector2(-61, -1.5f), new Vector2(-61, -23), new Vector2(-41, -9.66f), new Vector2(-44, -27) })
        {
            float head = RoofY(uv.x,uv.y)-2.2f;
            Tube("Hall_MainColumns", white, P(uv.x, uv.y, Floor), P(uv.x, uv.y, head+.2f), .48f, true, 24);
            Tube("Hall_ColumnCollars", steel, P(uv.x, uv.y, head-.45f), P(uv.x, uv.y, head-.15f), .53f);
            foreach (float du in new[] { -3f, 3f })
            foreach (float dv in new[] { -3f, 3f })
            {
                float nodeU = -74.5f + Mathf.Round((uv.x + du + 74.5f) / 3) * 3;
                float nodeV = -31.5f + Mathf.Round((uv.y + dv + 31.5f) / 3) * 3;
                Tube("Hall_BranchSupports", white, P(uv.x, uv.y, head-.25f),
                    P(nodeU, nodeV, RoofY(nodeU,nodeV) - 1.2f), .19f);
            }
        }
        Box("Front_RetailFascia", plaster, -50.4f, HallFrontV, 10.725f, 51.2f, .35f, 1.05f);
        GlazedBand(-76, HallFrontV, -24.8f, HallFrontV, 11.25f, 15.45f, "FrontClerestory");
        Box("Track_RetailFascia", plaster, -50.4f, 11, 10.725f, 51.2f, .35f, 1.05f);
        GlazedBand(-76, 11, -24.8f, 11, 11.25f, 15.45f, "TrackClerestory");
        foreach(float v in new[]{HallFrontV,11f})
        for(float u=-76;u<-24.8f;u+=3)
        {
            float end=Mathf.Min(u+3,-24.8f);
            var upper=B("HallEdge_UpperSpandrels",plaster);
            Vector3 a=P(u,v,15.45f),b=P(end,v,15.45f),c=P(end,v,RoofY(end,v)),d=P(u,v,RoofY(u,v));
            upper.Quad(a,b,c,d);upper.Quad(d,c,b,a);
        }
        foreach(float v in new[]{RidgeV0,RidgeV0+3})
        for(float u=-76;u<-24.8f;u+=3)
        {
            float end=Mathf.Min(u+3,-24.8f);
            var ridge=B("Roof_RaisedRidgeSides",glass);
            Vector3 a=P(u,v,RoofY(u,v)),b=P(end,v,RoofY(end,v));
            ridge.Quad(a,b,b+Vector3.up*.65f,a+Vector3.up*.65f);
        }
        receipts.Add(new { region = "High hall", observed = "98/112 circular-tube structural hierarchy, grey deck, branched supports, longitudinal daylight; upper-landmark-probe.json constrains relative roof heights; front-fascia-probe.json fixes the front fascia at v-35 (185 points)", inferred = "u[-76,-24.8],v[-35,11]; visible-member fit y14.199-.0575u-.0250v plus0.7m roof allowance. 172 samples,0.295m residual under inferred7m metric scale. Lattice3m, member radii, deck offset and unseen extent remain inferred." });
    }

    static void Mezzanine()
    {
        // Restaurant level: front balcony (TrackWing), front corner wing and a narrow left strip.
        // SfM 90-100s (sfm-model4-090-100.json): boards on the front band atv≈-34.8; two94s rays put the
        // escalator base line atu≈-71.8, rising toward -v; its head hides behind a fascia nearv-27. The pair itself
        // (u[-73.95,-69.65], incline v-17.99..-27) is the standard generated escalator from upper-escalator.json.
        // Nothing may cover the filmed90->98s path (u-69..-64); 90s shows open hall beyond the entry soffit.
        RectSlab("Mezzanine_TrackWing", stone, -64, -30, HallFrontV, -25.5f, 12.2f, .32f, true);
        RectSlab("Mezzanine_EndWing", stone, -76, -64, HallFrontV, -27, 12.2f, .32f, true);
        RectSlab("Mezzanine_EndWing", stone, -76, -74, -27, 11, 12.2f, .32f, true);
        Rail(-64, -25.5f, -30, -25.5f, 12.2f, "Mezzanine");
        Rail(-64, -27, -64, -25.5f, 12.2f, "Mezzanine");
        // The escalator pair's head u[-73.95,-69.65] stays open at the front-wing edge.
        Rail(-69.65f, -27, -64, -27, 12.2f, "Mezzanine");
        Rail(-74, -27, -74, 11, 12.2f, "Mezzanine");
        Box("Mezzanine_Fascia", plaster, -47, -25.5f, 11.55f, 34, .36f, 1.1f);
        Box("Mezzanine_Fascia", plaster, -66.825f, -27, 11.55f, 5.65f, .36f, 1.1f);
        Box("Mezzanine_Fascia", plaster, -74.975f, -27, 11.55f, 2.05f, .36f, 1.1f);
        Box("Mezzanine_Fascia", plaster, -74, -8, 11.55f, .36f, 38, 1.1f);
        foreach (float u in new[] { -62f, -54f, -46f, -38f })
            Tube("Mezzanine_Supports", white, P(u, -26, Floor), P(u, -26, 11.88f), .28f, true, 16);
        Shop("OLIVE YOUNG", -51, -29.5f, 11, 5.8f, 0, 1, "shelf", new Color(.30f, .56f, .06f), true);
        Shop("LOTTERIA", -47, -30, 12.5f, 4.2f, 0, 1, "counter", new Color(.53f, .06f, .035f), true, 12.2f);
        for (int i = 0; i < 5; i++)
        {
            float u = -61 + 4.8f * i;
            Prop("table", "Restaurant table · inferred", u, -26.6f, 12.2f, new Vector3(1.1f, .76f, 1.1f), 0, false);
            Prop("chair", "Restaurant chair", u - .95f, -26.6f, 12.2f, new Vector3(.48f, .86f, .5f), 90, false);
            Prop("chair", "Restaurant chair", u + .95f, -26.6f, 12.2f, new Vector3(.48f, .86f, .5f), -90, false);
        }
        for (int i = 0; i < 2; i++)
        {
            // SfM board clusters: u≈-67.6 and -63.0, face y≈9.7, median v-34.75: hung0.3m inside the
            // SfM-fitted front fascia (HallFrontV), which removes the former1.75m residual.
            float u = -67.6f + i * 4.6f;
            // Real acquired LED assembly behind a legible video-era green information face.
            // LedDisplay's long axis is model z; at yaw0 local z is station u, parallel to the front wall.
            Prop("display", "Paired departure display " + i, u, HallFrontV + .3f, 9.35f, new Vector3(.38f, 1.35f, 4.5f), 0, false);
            Sign("열차 출발 안내\nKTX   서울    경부선\nTrain Departures", u, HallFrontV + .57f, 10.03f, 4.35f, 1.15f, 0, 1, green, new Color(.65f, 1f, .28f));
        }
        receipts.Add(new { region = "Partial restaurant mezzanine", observed = "90-95s upper escalator pair rising away just left of the paired green boards; 112 LOTTERIA above OLIVE YOUNG on image-left; SfM model4 90-100s: 70 board points at u[-69.5,-61.5], v median-34.75, y median9.73; two94s base rays at u-71.66/-71.97; front fascia v-35 (front-fascia-probe.json)", inferred = "Front wing u[-76,-64]v[-35,-27] and left strip u[-76,-74] replace the former plate over the filmed path; strip width inferred", escalator = "Separate root 영상복원 · 식당층 에스컬레이터 from upper-escalator.json via VerticalCirculationGenerator + WireEscalatorMotion" });
    }

    static void Rail(float u0, float v0, float u1, float v1, float y, string key)
    {
        Vector3 a = P(u0, v0, y + .12f), b = P(u1, v1, y + .12f);
        Batch g = B(key + "_RailGlass", glass, true);
        g.Quad(a, b, b + Vector3.up * .93f, a + Vector3.up * .93f);
        g.Quad(a + Vector3.up * .93f, b + Vector3.up * .93f, b, a);
        Tube(key + "_RailMetal", steel, a + Vector3.up, b + Vector3.up, .045f);
        int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2));
        for (int i = 0; i <= count; i++)
        { Vector3 p = Vector3.Lerp(a, b, (float)i / count); Tube(key + "_RailMetal", steel, p - Vector3.up * .12f, p + Vector3.up, .035f); }
    }

    static void Information()
    {
        const float u = -41, v = -9.66f;
        // Counter has a real staff opening; the canopy is a dark disk, not a concentric ring bench.
        Arc("Information_Counter", white, u, v, 7.2f, 8.25f, 1.4f, 2.4f, 35, 325, true);
        Arc("Information_Platform", dark, u, v, 7.01f, 7.2f, 1.35f, 2.45f, 35, 325, true);
        Arc("Information_Worktop", stone, u, v, 8.25f, 8.34f, 1.3f, 2.5f, 35, 325, true);
        Arc("Information_Canopy", dark, u, v, 9.55f, 9.75f, 0, 1.95f, 0, 360, false);
        Sign("부산관광안내소\nTourist Information", u - 1.47f, v + 1.21f, 9.25f, 1.65f, .42f, -.771f, .637f, blue, Color.white);
        for (int i = 0; i < 3; i++)
            Prop("kiosk", "Information terminal " + i, u - 1.25f + i * 1.1f, v - 1.15f, 8.34f, new Vector3(.45f, .36f, .2f), 180, false);
        receipts.Add(new { region = "Tourist information", observed = "112s normalized counter width about0.20/canopy0.16; Main low-errorSfM front median(-42.94,-8.14,8.25),canopy median(-42.22,-8.68,9.56)", inferred = "Surface-depth-constrained center(-41,-9.66),camera depth15.11; counter2.4/worktop2.5 radii predictwidth0.195/0.203, canopy1.95 at9.55..9.75 predicts0.158. Front surface(-42.85,-8.13) and canopy sector(-42.20,-8.67) agree supplied point cues. Sign1.65x0.42 at9.25 remains inferred; not a rendered/surveyed verification." });
    }

    static void Arc(string key, Material mat, float u, float v, float bottom, float top, float inner, float outer, float start, float end, bool collision)
    {
        Batch b = B(key, mat, collision);
        int pieces = Mathf.CeilToInt((end - start) / 7.5f);
        for (int i = 0; i < pieces; i++)
        {
            float a = Mathf.Lerp(start, end, (float)i / pieces) * Mathf.Deg2Rad;
            float c = Mathf.Lerp(start, end, (float)(i + 1) / pieces) * Mathf.Deg2Rad;
            Vector3 ai = P(u + Mathf.Cos(a) * inner, v + Mathf.Sin(a) * inner, bottom);
            Vector3 ao = P(u + Mathf.Cos(a) * outer, v + Mathf.Sin(a) * outer, bottom);
            Vector3 ci = P(u + Mathf.Cos(c) * inner, v + Mathf.Sin(c) * inner, bottom);
            Vector3 co = P(u + Mathf.Cos(c) * outer, v + Mathf.Sin(c) * outer, bottom);
            Vector3 h = Vector3.up * (top - bottom);
            b.Quad(ai + h, ao + h, co + h, ci + h); b.Quad(ci, co, ao, ai);
            b.Quad(ao, co, co + h, ao + h); if (inner > .01f) b.Quad(ci, ai, ai + h, ci + h);
            if (i == 0) b.Quad(ai, ao, ao + h, ai + h);
            if (i == pieces - 1) b.Quad(co, ci, ci + h, co + h);
        }
    }

    static void Ceiling(float u0, float u1, float v0, float v1, float y, bool recess, bool extension = false)
    {
        Batch panels = B(recess ? "LowCeiling_RecessPanels" : "LowCeiling_FlatPanels", plaster);
        const float pitch = 1.2f;
        for (float u = u0 + pitch * .5f; u < u1; u += pitch)
        for (float v = v0 + pitch * .5f; v < v1; v += pitch)
        {
            if (!extension && (!Inside(u - .6f, v - .6f) || !Inside(u + .6f, v + .6f))) continue;
            if (Core(u, v, .61f, .61f)) continue;
            int iu = Mathf.RoundToInt((u - u0) / pitch), iv = Mathf.RoundToInt((v - v0) / pitch);
            bool light = iu % 4 == 1 && iv % 4 == 1;
            if (light)
            {
                Box("LowCeiling_LightFrame", steel, u, v, y + .018f, 1.18f, 1.18f, .06f);
                Box("LowCeiling_SquareLED", led, u, v, y - .02f, .94f, .94f, .025f);
            }
            else if (recess)
            {
                // Square-to-round annulus with a raised disk gives real circular recess depth, not a decal.
                const int n = 16; float r = .43f;
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2 / n, b = (k + 1) * Mathf.PI * 2 / n;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a), cb = Mathf.Cos(b), sb = Mathf.Sin(b);
                    float qa = .589f / Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa)), qb = .589f / Mathf.Max(Mathf.Abs(cb), Mathf.Abs(sb));
                    Vector3 oa = P(u + ca * qa, v + sa * qa, y), ob = P(u + cb * qb, v + sb * qb, y);
                    Vector3 ia = P(u + ca * r, v + sa * r, y + .08f), ib = P(u + cb * r, v + sb * r, y + .08f);
                    panels.Quad(ob, oa, ia, ib);
                    panels.Triangle(P(u, v, y + .085f), ib, ia);
                }
                if (iu % 4 == 3 && iv % 3 == 0)
                    Tube("LowCeiling_Downlights", led, P(u, v, y + .06f), P(u, v, y + .085f), .12f, false, 12);
            }
            else panels.Box(P(u, v, y + .045f), new Vector3(1.185f, .09f, 1.185f));
        }
    }

    static void Retail()
    {
        Shop("B&C", -16, -17.8f, 9, 6, 0, 1, "counter", new Color(.12f, .085f, .07f), true);
        Shop("Storyway", 12.8f, -10.3f, 8.0f, 5.5f, -1, -.1f, "shelf", new Color(.18f, .29f, .16f), true);
        Shop("味香한부산\nBUSAN LOCAL FOOD GIFT SHOP", 14.2f, -18.3f, 6.3f, 5.3f, -1, 0, "shelf", new Color(.20f, .22f, .24f), true);
        Shop("옛날 수제 호두과자", -2.8f, -23.6f, 6.4f, 5.2f, 0, 1, "counter", new Color(.28f, .16f, .075f), true);
        // Recessed inferred retail behind the fascia, not a transparent full-height exterior behind the desk.
        Shop("식음료 매장 · 추정", -24, -28, 7, 6, -1, 0, "counter", new Color(.28f,.22f,.18f), false);
        // Width stops 0.4m short of the original arcade track-fringe walls kept under rule F6 (v5.5-6.5).
        Shop("생활용품 매장 · 추정", -24, 2.6f, 5, 6, -1, 0, "shelf", new Color(.26f,.29f,.26f), false);
        foreach (Vector2 p in new[] { new Vector2(-27,-12), new Vector2(-17,-2), new Vector2(-10,-21), new Vector2(0,-5), new Vector2(17,-19), new Vector2(26,-4) })
        {
            if (!Clear(p.x, p.y, .65f, .65f)) continue;
            Box("LowArcade_SquareColumns", steel, p.x, p.y, 9.12f, .9f, .9f, 4.24f, true);
            Box("LowArcade_ColumnBase", dark, p.x, p.y, 7.12f, .97f, .97f, .24f, true);
        }
        // Signs are outside the shared opening and above the clear approach. No duplicate portal posts/guards.
        Sign("↓ 나가는 곳  /  Exit", 6.0f, -18.65f, 10.13f, 5.8f, .65f, 0, 1, yellow, Color.black);
        Sign("← 타는 곳   ·   표 사는 곳", 6.8f, -35.0f, 10.1f, 5.0f, .62f, 0, 1, blue, Color.white);
        receipts.Add(new { region = "Filmed low retail arcade", observed = "138 B&C;158 Storyway/local-food/food counter;166/174 stainless columns and mixed recessed ceiling", inferred = "Shop depth/size and exact fronts; all shell dimensions; tenant order follows video, not later board brands", coreApproach = "u[3.4,10.3],v[-19.5,-15] left open; yellow sign v-18.65" });
    }

    static void Shop(string name, float u, float v, float width, float depth, float faceU, float faceV, string fixture, Color fascia, bool observed, float floor = Floor)
    {
        Vector3 normal = D(faceU, faceV), right = Vector3.Cross(Vector3.up, normal).normalized;
        float radiusU = (Mathf.Abs(right.z) * width + Mathf.Abs(normal.z) * depth) * .5f;
        float radiusV = (Mathf.Abs(right.x) * width + Mathf.Abs(normal.x) * depth) * .5f;
        // Protect the complete architectural shell, including side/back walls and entrance glazing.
        // Relocate a conflicting shop; never silently cut a fake doorway through its side wall.
        if (Mathf.Abs(floor - Floor) < .1f && !Clear(u,v,radiusU,radiusV))
            throw new InvalidOperationException("Shop shell intrudes on registered public route/core: " + name);
        Vector3 center = P(u, v, floor), front = center + normal * depth * .5f;
        string safe = "Shop_" + name.Replace('\n', '_').Replace('/', '_');
        Material signMat = Mat(safe + "_Fascia", fascia);
        Batch walls = B(safe + "_Walls", plaster, true), frames = B(safe + "_Frames", steel);
        // Recessed side/back walls, no front wall: a large real opening into a furnished volume.
        Vector3 a = front - right * width * .5f, b = front + right * width * .5f;
        Vector3 c = b - normal * depth, d = a - normal * depth;
        Wall(walls, a, d, 3.45f); Wall(walls, d, c, 3.45f); Wall(walls, c, b, 3.45f);
        frames.Tube(a, a + Vector3.up * 3.5f, .055f, 8); frames.Tube(b, b + Vector3.up * 3.5f, .055f, 8);
        frames.Tube(a + Vector3.up * 2.85f, b + Vector3.up * 2.85f, .05f, 8);
        // Side display glazing only; central60% remains an actual unobstructed store entrance.
        Batch glazing = B(safe + "_SideGlazing", glass, true);
        Wall(glazing, a, a + right * width * .18f, 2.8f); Wall(glazing, b - right * width * .18f, b, 2.8f);
        Sign(name, front.z, front.x, floor + 3.08f, width - .2f, .55f, faceU, faceV, signMat, Color.white);
        float yaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
        // Individual source shelf subtree/counter; back and sides retain real shop depth.
        for (int i = -1; i <= 1; i++)
        {
            Vector3 p = center + right * (width * .24f * i) - normal * (depth * .22f);
            Prop(fixture, name + " acquired fixture", p.z, p.x, floor, new Vector3(width * .20f, fixture == "shelf" ? 1.75f : 1.05f, .78f), yaw, false);
            if (fixture == "counter") Prop("food", name + " displayed baked goods", p.z, p.x, floor + 1.06f, new Vector3(.7f, .22f, .45f), yaw, false);
        }
        // All shop LEDs are geometry/emission; global scene light remains Main's responsibility.
        for (int i = -1; i <= 1; i++)
        {
            Vector3 p = center + right * i * width * .25f;
            B("Shop_LED", led).Box(p + Vector3.up * 3.5f, new Vector3(.58f, .035f, .58f));
        }
        receipts.Add(new { region = name, evidence = observed ? "VIDEO_VISIBLE_BRAND_GEOMETRY_INFERRED" : "UNSEEN_METADATA_TOPOLOGY_INFERRED", centreUV = new[] { u, v }, widthM = width, depthM = depth, floorY = floor });
    }

    static void Wall(Batch b, Vector3 a, Vector3 c, float height)
    {
        Vector3 h = Vector3.up * height;
        b.Quad(a, c, c + h, a + h); b.Quad(a + h, c + h, c, a);
    }

    static void InferredRemainder()
    {
        // Beyond the filmed hall retain coherent continuous public/service strips in the source footprint.
        // Later board topology supplies USES, not claimed2022 tenant identities or measured room boundaries.
        Shop("편의점 · 추정 배치", 35, -30, 11, 7, 0, 1, "shelf", new Color(.22f,.30f,.25f), false);
        Shop("식음료 매장 · 추정 배치", 51, -27, 10, 7, 0, 1, "counter", new Color(.32f,.23f,.18f), false);
        Shop("여행용품 · 추정 배치", -45, -36.5f, 10, 6, 0, 1, "shelf", new Color(.24f,.27f,.29f), false);
        Shop("카페 · 추정 배치", -81, -16, 9, 6, 1, 0, "counter", new Color(.28f,.19f,.12f), false);
        // Bent service edge: ticketing to station office/nursery/police, never a claimed10-window video fact.
        ServiceRoom("역무실", 31, -1.5f, 7, 7, 0, -1);
        ServiceRoom("수유방", 39.5f, -.5f, 6, 7, 0, -1);
        ServiceRoom("철도경찰", 48, 1, 8, 7, 0, -1);
        ServiceRoom("화장실", 64, -4, 9, 9, -1, 0);
        ServiceRoom("고객대기실", 65, -19, 9, 9, -1, 0);
        for (int i = 0; i < 4; i++)
            Prop("counter", "매표창구 · 수량/위치 추정 " + i, 19.5f + i * 2.1f, -1, Floor, new Vector3(1.75f,1.15f,.9f), 180, true);
        Sign("표 사는 곳  /  Tickets", 23, -2, 10.15f, 8.5f, .7f, 0, -1, blue, Color.white);
        for (int i = 0; i < 3; i++)
            Prop("ticket", "자동발매기 · 추정 " + i, 33 + i * 1.2f, -8, Floor, new Vector3(.8f,1.8f,.6f), 180, true);
        Prop("locker", "물품보관 · 추정", 55, -2, Floor, new Vector3(3.8f,2.05f,.6f), 180, true);
        foreach (float u in new[] { 35f, 49f, 62f })
        foreach (float v in new[] { -14f, -23f })
            if (Clear(u,v,.6f,.6f)) Box("Inferred_NorthColumns", steel, u,v,9.1f,.7f,.7f,4.2f,true);
        receipts.Add(new { region = "Unfilmed northern/front public retail and services", observed = "Supporting boards: bent ticket/office/nursery/police edge, toilets and front-side shops; source curved envelope", inferred = "All rooms, tenant category labels, window count4, furniture and columns u[18,77]; no claim these represent surveyed2022 tenancy", connection = "Continuous public concourse alongv[-25,-8], return routes to filmed arcade and inferred rear platform portals" });
    }

    static void ServiceRoom(string name, float u, float v, float width, float depth, float nu, float nv)
    {
        Vector3 normal = D(nu,nv), right = Vector3.Cross(Vector3.up,normal), center = P(u,v,Floor);
        Vector3 f = center + normal * depth * .5f;
        Vector3 a = f - right * width * .5f, b = f + right * width * .5f;
        Batch wall = B("ServiceRooms_Walls", plaster, true);
        Wall(wall,a,a-normal*depth,4.15f); Wall(wall,a-normal*depth,b-normal*depth,4.15f); Wall(wall,b-normal*depth,b,4.15f);
        Wall(wall,a,f-right*.9f,4.15f); Wall(wall,f+right*.9f,b,4.15f);
        Vector3 left = f-right*.9f, rr=f+right*.9f;
        wall.Quad(left+Vector3.up*2.35f,rr+Vector3.up*2.35f,rr+Vector3.up*4.15f,left+Vector3.up*4.15f);
        Sign(name, f.z, f.x, 9.9f, Mathf.Min(width-1,4.5f), .6f, nu,nv,blue,Color.white);
        // An open, reachable inferred service interior with reused seating, not a solid room box.
        Prop("bench",name+" interior · inferred",u,v,Floor,new Vector3(2.3f,.85f,.66f),0,false);
    }

    static void Furniture()
    {
        Vector2[] pockets = { new Vector2(-57,-17), new Vector2(-52,-22), new Vector2(-38,-16), new Vector2(-35,-22), new Vector2(38,-18), new Vector2(52,-16) };
        for (int p = 0; p < pockets.Length; p++)
        for (int row = 0; row < 2; row++)
        for (int pair = 0; pair < 2; pair++)
            Prop("bench", "Grouped brown-metal bench " + p + "_" + row + "_" + pair,
                pockets[p].x + pair * 3.1f, pockets[p].y + row * 1.25f, Floor, new Vector3(2.7f,.82f,.66f), row == 0 ? 0 : 180, true);
        foreach (Vector2 p in new[] { new Vector2(-35,-9),new Vector2(-56,3),new Vector2(-17,-.5f),new Vector2(23,-12) })
            Prop("kiosk","Freestanding information display",p.x,p.y,Floor,new Vector3(.72f,1.85f,.72f),-90,true);
        foreach (Vector2 p in new[] { new Vector2(-54,-17),new Vector2(-35,-16),new Vector2(29,-17) })
            Prop("bin","Acquired litter bin",p.x,p.y,Floor,new Vector3(.48f,.85f,.48f),0,true);
        receipts.Add(new { region = "Seating and information furniture", observed = "124 grouped brown/metal benches and free-standing display cases", inferred = "Pocket centers, count, individual dimensions; north pockets unseen", asset = "Kits/Web/WaitingBench single geometry/two materials, NOT StationBench4 bundled scene" });
    }

    static bool Clear(float u, float v, float ru, float rv)
    {
        if (Core(u,v,ru,rv)) return false;
        // Capsule-vs-axis-aligned footprint: conservatively enlarge rectangle by0.8m and clip each path segment.
        for (int i = 1; i < route.Count; i++)
        {
            Vector2 a=route[i-1], b=route[i];
            float t0=0,t1=1; Vector2 delta=b-a;
            if (Clip(-delta.x,a.x-(u-ru-.8f),ref t0,ref t1) && Clip(delta.x,u+ru+.8f-a.x,ref t0,ref t1) &&
                Clip(-delta.y,a.y-(v-rv-.8f),ref t0,ref t1) && Clip(delta.y,v+rv+.8f-a.y,ref t0,ref t1)) return false;
        }
        return true;
    }
    static bool Clip(float p,float q,ref float lo,ref float hi)
    {
        if (Mathf.Abs(p)<.00001f) return q>=0;
        float r=q/p;
        if(p<0) { if(r>hi)return false; if(r>lo)lo=r; }
        else { if(r<lo)return false; if(r<hi)hi=r; }
        return true;
    }

    static GameObject Prop(string key,string name,float u,float v,float y,Vector3 target,float yaw,bool respectRoute)
    {
        // target is natural model XYZ: width,height,depth. Rotate its footprint before route exclusion.
        float a=yaw*Mathf.Deg2Rad;
        float rv=(Mathf.Abs(Mathf.Cos(a))*target.x+Mathf.Abs(Mathf.Sin(a))*target.z)*.5f;
        float ru=(Mathf.Abs(Mathf.Sin(a))*target.x+Mathf.Abs(Mathf.Cos(a))*target.z)*.5f;
        if(Core(u,v,ru,rv) || (respectRoute && !Clear(u,v,ru,rv))) { skippedProps++; return null; }
        Transform holder=new GameObject(name).transform; holder.SetParent(root,false);
        // Fit scale/offset live on an identity-rotation parent so they act in holder axes. The instance keeps
        // its authored ancestor orientation (Blender Z-up roots import as X270; shelves as 0/180/180).
        Transform fit=new GameObject("fit").transform; fit.SetParent(holder,false);
        GameObject obj=UnityEngine.Object.Instantiate(models[key]); obj.transform.SetParent(fit,false);
        obj.transform.localPosition=Vector3.zero; obj.transform.localRotation=models[key].transform.rotation; obj.transform.localScale=Vector3.one;
        foreach(Collider c in obj.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach(Camera c in obj.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach(Light l in obj.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l);
        Bounds b=LocalBounds(obj,holder);
        if(b.size.x<.00001f || b.size.y<.00001f || b.size.z<.00001f) throw new InvalidOperationException("Acquired prop bounds invalid: "+key);
        fit.localScale=new Vector3(target.x/b.size.x,target.y/b.size.y,target.z/b.size.z);
        b=LocalBounds(obj,holder); fit.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);
        holder.localPosition=P(u,v,y); holder.localRotation=Quaternion.Euler(0,yaw,0);
        BoxCollider collider=holder.gameObject.AddComponent<BoxCollider>(); collider.center=Vector3.up*target.y*.5f; collider.size=target;
        ConvertPropMaterials(obj);
        propCount++; return holder.gameObject;
    }

    static Bounds LocalBounds(GameObject obj,Transform relative)
    {
        Bounds bound=new Bounds(); bool any=false;
        foreach(MeshFilter f in obj.GetComponentsInChildren<MeshFilter>(true))
        {
            if(f.sharedMesh==null) continue;
            Bounds b=f.sharedMesh.bounds; Matrix4x4 m=relative.worldToLocalMatrix*f.transform.localToWorldMatrix;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                Vector3 p=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
                if(!any){bound=new Bounds(p,Vector3.zero);any=true;}else bound.Encapsulate(p);
            }
        }
        if(!any) throw new InvalidOperationException("Acquired model has no mesh bounds: "+obj.name);
        return bound;
    }

    static void ConvertPropMaterials(GameObject go)
    {
        foreach(Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials=r.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                Material original=materials[i]; if(original==null) {materials[i]=steel;continue;}
                if(original.shader!=null && original.shader.name.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal)) continue;
                Material mapped;
                if(!propMaterials.TryGetValue(original,out mapped))
                {
                    string guid; long local;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original,out guid,out local);
                    string key="Reused_"+guid+"_"+local;
                    Material fresh=new Material(lit); fresh.name=original.name;
                    Color color=original.HasProperty("_Color")?original.GetColor("_Color"):Color.white;
                    Texture tex=original.HasProperty("_MainTex")?original.GetTexture("_MainTex"):null;
                    fresh.SetColor("_BaseColor",color);
                    if(tex!=null)
                    {
                        fresh.SetTexture("_BaseMap",tex);
                        fresh.SetTextureScale("_BaseMap",original.GetTextureScale("_MainTex"));
                        fresh.SetTextureOffset("_BaseMap",original.GetTextureOffset("_MainTex"));
                    }
                    fresh.SetFloat("_Metallic",original.HasProperty("_Metallic")?original.GetFloat("_Metallic"):0);
                    fresh.SetFloat("_Smoothness",original.HasProperty("_Glossiness")?original.GetFloat("_Glossiness"):.42f);
                    if(original.HasProperty("_BumpMap") && original.GetTexture("_BumpMap")!=null)
                    {
                        fresh.SetTexture("_BumpMap",original.GetTexture("_BumpMap"));
                        fresh.EnableKeyword("_NORMALMAP");
                    }
                    mapped=SaveMaterial(key,fresh); propMaterials.Add(original,mapped);
                }
                materials[i]=mapped;
            }
            r.sharedMaterials=materials;
        }
    }

    static void Sign(string text,float u,float v,float y,float width,float height,float faceU,float faceV,Material board,Color ink)
    {
        Vector3 n=D(faceU,faceV), right=Vector3.Cross(Vector3.up,n), c=P(u,v,y);
        Batch b=B("SignBoards_"+board.name,board);
        Vector3 a=c-right*width*.5f-Vector3.up*height*.5f, d=c-right*width*.5f+Vector3.up*height*.5f;
        b.Quad(a,a+right*width,d+right*width,d); b.Quad(d,d+right*width,a+right*width,a);
        GameObject go=new GameObject("Sign · "+text.Replace('\n',' ')); go.transform.SetParent(root,false);
        go.transform.localPosition=c+n*.015f; go.transform.localRotation=Quaternion.LookRotation(-n,Vector3.up);
        TextMesh t=go.AddComponent<TextMesh>();t.font=font;t.text=text;t.fontSize=80;t.characterSize=.11f;
        t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=ink;t.richText=false;
        MeshRenderer r=go.GetComponent<MeshRenderer>();r.sharedMaterial=font.material;r.shadowCastingMode=ShadowCastingMode.Off;
        // TextMesh's renderer supplies its generated local bounds; no guessed font-to-world conversion.
        font.RequestCharactersInTexture(text,80,FontStyle.Normal);
        Vector3 natural = r.localBounds.size;
        if (natural.x <= 0 || natural.y <= 0)
            throw new InvalidOperationException("Korean font produced empty sign geometry: " + text);
        float scale=Mathf.Min((width-.22f)/natural.x,(height-.12f)/natural.y);
        go.transform.localScale=Vector3.one*scale;
    }

    sealed class Batch
    {
        readonly string name; readonly Material mat; readonly bool collision;
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>(); readonly List<int> triangles=new List<int>();
        public Batch(string name,Material mat,bool collision){this.name=name;this.mat=mat;this.collision=collision;}
        public void Triangle(Vector3 a,Vector3 b,Vector3 c)
        {
            int i=vertices.Count;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);
            uv.Add(new Vector2(a.x,a.z));uv.Add(new Vector2(b.x,b.z));uv.Add(new Vector2(c.x,c.z));
            triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);
        }
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int i=vertices.Count;Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            Vector3 axis=(b-a).normalized;Vector3 up=Vector3.Cross(normal,axis);
            uv.Add(Vector2.zero);uv.Add(new Vector2(Vector3.Dot(b-a,axis),Vector3.Dot(b-a,up)));
            uv.Add(new Vector2(Vector3.Dot(c-a,axis),Vector3.Dot(c-a,up)));uv.Add(new Vector2(Vector3.Dot(d-a,axis),Vector3.Dot(d-a,up)));
            triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);triangles.Add(i);triangles.Add(i+2);triangles.Add(i+3);
        }
        public void Box(Vector3 center,Vector3 size)
        {
            Vector3 h=size*.5f;
            Vector3 a=center+new Vector3(-h.x,-h.y,-h.z), b=center+new Vector3(h.x,-h.y,-h.z);
            Vector3 c=center+new Vector3(h.x,-h.y,h.z), d=center+new Vector3(-h.x,-h.y,h.z), up=Vector3.up*size.y;
            Quad(a,b,c,d);Quad(d+up,c+up,b+up,a+up);
            Quad(b,a,a+up,b+up);Quad(c,b,b+up,c+up);Quad(d,c,c+up,d+up);Quad(a,d,d+up,a+up);
        }
        public void Tube(Vector3 a,Vector3 b,float radius,int sides)
        {
            Vector3 axis=(b-a).normalized;
            Vector3 x=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.forward:Vector3.up).normalized;
            Vector3 y=Vector3.Cross(axis,x);
            for(int i=0;i<sides;i++)
            {
                float t=i*Mathf.PI*2/sides,t1=(i+1)*Mathf.PI*2/sides;
                Vector3 r=(x*Mathf.Cos(t)+y*Mathf.Sin(t))*radius,s=(x*Mathf.Cos(t1)+y*Mathf.Sin(t1))*radius;
                Quad(a+r,a+s,b+s,b+r);Triangle(a,a+s,a+r);Triangle(b,b+r,b+s);
            }
        }
        public void Save(Transform parent)
        {
            if(vertices.Count==0)return;
            string path=Art+"/"+name+".asset";
            Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create=saved==null;
            if(create)saved=new Mesh();
            // Mutate the persistent mesh itself: serialized copies left stale GPU geometry on rerun.
            saved.Clear(false);saved.name=name;saved.indexFormat=IndexFormat.UInt32;
            saved.SetVertices(vertices);saved.SetUVs(0,uv);saved.SetTriangles(triangles,0,false);
            saved.RecalculateNormals();saved.RecalculateTangents();saved.RecalculateBounds();
            saved.UploadMeshData(false);
            if(create)AssetDatabase.CreateAsset(saved,path);
            EditorUtility.SetDirty(saved);
            GameObject go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=saved;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
        }
    }
}
