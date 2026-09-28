// 부산역 2F 맞이방 + 3F 식당가, rebuilt from the ORIGINAL model map (FPSWorld/공식 자료 부산역 역사).
// Geometry comes only from .planning/2026-09-23-video-twin/majibang-plan.json:
//   2F cells  - original-shell probes (enclosure, roof, missing floor, platform-stair wells);
//   3F L      - official on-site board registered to the original box shell, dimensions by Jev 013/014.
// Nothing is placed from the 2022-video registration. Helpers follow VideoFloorTwo (proven prop fitting).
// Run in Edit mode: unity run_script AgentScripts/MajibangBuilder.cs MajibangBuilder.Main
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MajibangBuilder
{
    const string RootName = "맞이방 · 원본 정합";
    const string Art = "Assets/ChooGuard/Art/StationInterior/Majibang";
    const string PlanPath = ".planning/2026-09-23-video-twin/majibang-plan.json";
    const string Mats = "Assets/ChooGuard/Art/StationInterior/VideoSecondFloor/";
    const string Base = "Assets/ChooGuard/Art/StationInterior/";
    const string Restaurant = Base + "Kits/restaurant-bits/KayKit_Restaurant_Bits_1.0_FREE/Assets/fbx (unity)/";
    const float Theta = 16.2f;

    static Transform root;
    static Font font;
    static Shader lit;
    static Material stone, plaster, steel, glass, dark, wood, led, blue, ceilingPanel;
    static readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
    static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
    static readonly Dictionary<Material, Material> propMaterials = new Dictionary<Material, Material>();
    static readonly List<object> receipts = new List<object>();
    static int propCount, downlights;
    static string batchPrefix = "";
    // Per-unit options; open fronts and unresolved fitout are explicit evidence-backed plan decisions.
    static string unitFasciaTex;
    static float unitDoorW = 2.0f;
    static bool unitOpenFront, unitFixtures = true;
    // Storefront hand-off: plan/spec "storefronts": "kit" leaves unit fronts open (no glass, frames, fascia band or sign) for kit
    // Storefront elements; walls, shop ceilings, lights and fixtures are unchanged. Default (absent) keeps the builders' fronts.
    static bool kitStorefronts;
    // Plan "hallAppearance": "white" (evidence BL-05 / CX-19): white deck liner with skylight strips under the original roof and
    // smooth white tubular trusses on the same hallStructure lines. Default (absent) keeps the builder appearance.
    static bool hallWhite;
    // Batch.Save target: Art for every scene build; null keeps kit-preview meshes in memory (tracked for cleanup).
    static string meshFolder = Art;
    static readonly List<UnityEngine.Object> kitTransient = new List<UnityEngine.Object>();

    // Root rotation maps local (v, y, u) to the station basis, identical to the other station builders.
    // Inside the box frame the same helpers take box coordinates (a along the box axis, b across it).
    static Vector3 P(float u, float v, float y) { return new Vector3(v, y, u); }
    static Vector3 D(float u, float v) { return new Vector3(v, 0, u).normalized; }
    // Original box walls (inner faces), measured from the shell: east wall and south wall.
    static float EastV(float u) { return inBox ? boxEastB : .0722f * u + 94.29f; }

    // Over-track box frame: the box walls and raised roof run 4.13 deg off the station axis (east wall slope .0722).
    // Origin on the raised-roof west edge at u -3 (v 33.25); a = along the box axis, b = across (east).
    static Transform frame, boxFrame;
    static bool inBox;
    static float boxEastB;
    static void EnterBox() { frame = boxFrame; inBox = true; }
    static void ExitBox() { frame = root; inBox = false; }

    public static void Main(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run MajibangBuilder in Edit mode.");
        JObject plan = JObject.Parse(File.ReadAllText(PlanPath));
        kitStorefronts = (string)plan["storefronts"] == "kit";
        hallWhite = (string)plan["hallAppearance"] == "white";
        batches.Clear(); models.Clear(); propMaterials.Clear(); receipts.Clear(); propCount = 0; downlights = 0;
        batchPrefix = "";
        lit = Shader.Find("Universal Render Pipeline/Lit");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf");
        if (lit == null || font == null) throw new InvalidOperationException("URP Lit shader or Korean font missing.");
        stone = Reuse("Floor_Granite"); plaster = Reuse("Soffit_Plaster"); steel = Reuse("Stainless_Hairline");
        glass = Reuse("Glass_GreenClear"); dark = Reuse("Canopy_Dark"); wood = Reuse("Counter_Wood");
        led = Reuse("Light_Lens"); blue = Reuse("Sign_RailBlue");
        // Resolve every acquired asset before replacing scene content. Never substitute primitives.
        Load("counter", Base + "Kits/Web/TicketCounter.fbx");
        Load("shelf", Base + "Props/StoreShelf.fbx", "GONDOLA1");
        Load("table", Restaurant + "table_round_A.fbx");
        Load("chair", Restaurant + "chair_A.fbx");
        Load("food", Restaurant + "crate_buns.fbx");
        Load("ticket", Base + "Kits/Web/TicketVendingMachine.fbx");
        Load("bench", Base + "Kits/Web/WaitingBench.fbx");
        EnsureFolder(Art);
        // Unlit near-white panels stand in for the bounce light realtime lighting lacks under the 3F slab
        // (RY2qvE0Tugk 1:36-2:19: the strip ceiling reads uniformly near-white between the downlights).
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) throw new InvalidOperationException("URP Unlit shader missing.");
        ceilingPanel = Colour("Majibang_2F_CeilingPanel", new Color(.84f, .84f, .82f), unlit);

        GameObject previous = GameObject.Find(RootName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        root = new GameObject(RootName).transform;
        root.rotation = Quaternion.Euler(0, Theta, 0);
        frame = root; inBox = false;
        JObject bf = (JObject)plan["boxFrame"];
        if (bf == null) throw new InvalidOperationException("Plan boxFrame missing.");
        boxFrame = new GameObject("Box frame").transform; boxFrame.SetParent(root, false);
        boxFrame.localPosition = P((float)bf["origin"][0], (float)bf["origin"][1], 0);
        boxFrame.localRotation = Quaternion.Euler(0, (float)bf["rotDeg"], 0);
        boxEastB = (float)bf["eastB"];

        int cells = SecondFloor(plan);
        JObject third = (JObject)plan["thirdFloor"];
        if (third != null) ThirdFloor(third);
        float floor2 = (float)plan["floorTopY"];
        if (plan["secondFloorUnits"] is JArray units2) foreach (JObject unit in units2) Unit(unit, floor2);
        if (plan["secondFloorExtras"] is JObject extras) SecondFloorExtras(extras, floor2);
        if (plan["hallStructure"] is JObject hall) HallStructure(hall, floor2);
        if (plan["wallPanels"] is JArray wallPanels) foreach (JObject wp in wallPanels) WallPanel(wp);
        if (plan["doorOpenings"] is JArray doorSets) foreach (JObject doors in doorSets) DoorOpenings(doors);
        foreach (Batch batch in batches.Values) batch.Save();
        BindWorldText();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        File.WriteAllText(".planning/2026-09-23-video-twin/majibang-build.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            schema = "chooguard.majibang-build.v2", root = RootName, plan = PlanPath,
            floor2F = new { topY = (float)plan["floorTopY"], cells, regions = plan["regions"] },
            thirdFloor = third != null, meshBatches = batches.Count, props = propCount, downlights, regions = receipts,
            storefronts = kitStorefronts ? "kit" : "builder", hallAppearance = hallWhite ? "white" : "builder", slabOpenings = slabOpeningReport,
            source = "original-model probes and the official board registered to the original shell; no video registration"
        }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("MAJIBANG_BUILT cells=" + cells + " batches=" + batches.Count + " props=" + propCount);
    }

    // Independent 1F pass: never rebuild or remove the existing 2F/3F reconstruction.
    public static void FirstFloor(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run FirstFloor in Edit mode.");
        const string planPath = ".planning/2026-09-23-video-twin/first-floor-plan.json";
        const string groundRoot = "1층 · 원본 정합";
        JObject plan = JObject.Parse(File.ReadAllText(planPath));
        JArray triangles = (JArray)plan["floorTriangles"];
        if (triangles == null || triangles.Count == 0) throw new InvalidOperationException("1F floor triangulation is absent.");
        batches.Clear(); receipts.Clear(); models.Clear(); propMaterials.Clear();
        propCount = 0; downlights = 0; batchPrefix = "Ground_";
        lit = Shader.Find("Universal Render Pipeline/Lit");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf");
        stone = AssetDatabase.LoadAssetAtPath<Material>(Base + "Materials/Lane/floor_granite_tile.mat");
        if (stone == null || font == null || lit == null) throw new InvalidOperationException("Required 1F materials or font missing.");
        plaster = Reuse("Soffit_Plaster"); steel = Reuse("Stainless_Hairline");
        glass = Reuse("Glass_GreenClear"); dark = Reuse("Canopy_Dark");
        led = Reuse("Light_Lens"); blue = Reuse("Sign_RailBlue");
        ceilingPanel = Colour("Ground_CeilingPanel", new Color(.78f, .79f, .78f), Shader.Find("Universal Render Pipeline/Unlit"));
        EnsureFolder(Art);
        const string floorMaterialPath = Art + "/Ground_Floor.mat";
        Material groundStone = AssetDatabase.LoadAssetAtPath<Material>(floorMaterialPath);
        if (groundStone == null)
        {
            groundStone = new Material(stone) { name = "Ground_Floor" };
            AssetDatabase.CreateAsset(groundStone, floorMaterialPath);
        }
        groundStone.SetFloat("_BumpScale", .15f);
        groundStone.SetFloat("_Smoothness", .65f);
        EditorUtility.SetDirty(groundStone);
        stone = groundStone;
        GameObject previous = GameObject.Find(groundRoot);
        root = new GameObject(groundRoot + " · building").transform;
        root.rotation = Quaternion.Euler(0, Theta, 0);
        frame = root; inBox = false;
        try
        {
            Batch floor = B("Floor", stone, true);
            float y = (float)plan["floorY"], depth = (float)plan["slabDepth"];
            foreach (JArray triangle in triangles)
            {
                Vector2 a = new Vector2((float)triangle[0][0], (float)triangle[0][1]);
                Vector2 b = new Vector2((float)triangle[1][0], (float)triangle[1][1]);
                Vector2 c = new Vector2((float)triangle[2][0], (float)triangle[2][1]);
                if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) < 0)
                { Vector2 swap = b; b = c; c = swap; }
                SlabTriangle(floor, a, b, c, y, depth);
            }
            foreach (Batch batch in batches.Values) batch.Save();
            // Tenancies are owned by FirstFloorInterior; carry an existing checked interior across a floor rebuild.
            Transform interior = previous != null ? previous.transform.Find(InteriorName) : null;
            if (interior != null) interior.SetParent(root, false);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
            root.name = groundRoot;
            BindWorldText();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            File.WriteAllText(".planning/2026-09-23-video-twin/first-floor-build.json",
                Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    schema = "chooguard.first-floor-build.v1", root = groundRoot,
                    plan = planPath, floorY = y, triangles = triangles.Count,
                    addedArea = (float)plan["floorAreaAdded"], basis = plan["basis"],
                    registration = plan["registration"], interior = InteriorBuild,
                    sourceGeometryModified = false, upperFloorGeometryModified = false
                }, Newtonsoft.Json.Formatting.Indented));
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            throw;
        }
    }

    const string InteriorName = "1층 실내 · 근거 표기";
    const string InteriorSpec = ".planning/2026-09-23-video-twin/1f-implementation/interior-spec.json";
    const string InteriorChecks = ".planning/2026-09-23-video-twin/1f-implementation/checks.json";
    const string InteriorBuild = ".planning/2026-09-23-video-twin/1f-implementation/build.json";

    // 1F tenancies from the boundary-checked interior spec (interior.py spec/check). Adds geometry only inside the
    // measured 1F enclosure; never rebuilds Ground_Floor, the source shell, the 2F slab or observed finishes.
    public static void FirstFloorInterior(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run FirstFloorInterior in Edit mode.");
        JObject spec = JObject.Parse(File.ReadAllText(InteriorSpec));
        kitStorefronts = (string)spec["storefronts"] == "kit";
        JObject checks = JObject.Parse(File.ReadAllText(InteriorChecks));
        if (File.GetLastWriteTimeUtc(InteriorChecks) < File.GetLastWriteTimeUtc(InteriorSpec) || ((JArray)checks["blocking"]).Count != 0)
            throw new InvalidOperationException("Interior spec is unchecked or has blocking boundary issues; run interior.py check.");
        Transform ground = GameObject.Find("1층 · 원본 정합")?.transform ?? throw new InvalidOperationException("1F root missing.");
        if (ground.Find("Ground_Floor") == null) throw new InvalidOperationException("Protected Ground_Floor missing.");
        batches.Clear(); receipts.Clear(); texMats.Clear();
        batchPrefix = "Ground_";
        lit = Shader.Find("Universal Render Pipeline/Lit");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf");
        if (font == null || lit == null) throw new InvalidOperationException("Required 1F font or shader missing.");
        plaster = Reuse("Soffit_Plaster"); steel = Reuse("Stainless_Hairline");
        glass = Reuse("Glass_GreenClear"); dark = Reuse("Canopy_Dark");
        ceilingPanel = Colour("Ground_CeilingPanel", new Color(.78f, .79f, .78f), Shader.Find("Universal Render Pipeline/Unlit"));
        Transform previous = ground.Find(InteriorName), legacy = ground.Find("상가 참고 배치 · 정합 미승인");
        Transform interior = new GameObject(InteriorName + " · building").transform;
        interior.SetParent(ground, false);
        root = ground; frame = interior; inBox = false;
        try
        {
            float y = (float)spec["floorY"];
            foreach (JObject unit in (JArray)spec["units"]) GroundUnit(unit, y);
            foreach (Batch batch in batches.Values) batch.Save();
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(interior.gameObject);
            throw;
        }
        // Unity reports destroyed objects as null, so record the cutover before destroying.
        bool legacyRemoved = legacy != null;
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        // The inactive reference layout shared these unit meshes and is superseded by the checked interior.
        if (legacyRemoved) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        interior.name = InteriorName;
        BindWorldText();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        File.WriteAllText(InteriorBuild, Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            schema = "chooguard.1f-interior-build.v1", root = "1층 · 원본 정합/" + InteriorName,
            spec = InteriorSpec, checks = InteriorChecks, checkedAgainstAudit = (string)checks["auditUtc"],
            meshBatches = batches.Count, units = receipts, legacyReferenceRemoved = legacyRemoved, storefronts = kitStorefronts ? "kit" : "builder",
            basis = spec["basis"], corrections = spec["corrections"]
        }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("FIRST_FLOOR_INTERIOR_BUILT units=" + receipts.Count + " batches=" + batches.Count);
    }
    // Explicit two-unit cutover. "inspect" is read-only; "apply" is for the integration owner.
    // Legacy meshes mix many shops: remove only uniquely matched triangles, never entire shared batches.
    public static void CorrectOpenFrontages(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run CorrectOpenFrontages in Edit mode.");
        if (args == null || args.Length != 1 || (args[0] != "inspect" && args[0] != "apply"))
            throw new ArgumentException("Use exactly one argument: inspect or apply.");
        bool apply = args[0] == "apply";
        JObject plan = JObject.Parse(File.ReadAllText(PlanPath));
        kitStorefronts = false; // the legacy-ownership preflight must reproduce the builder fronts exactly
        JObject[] units = ((JArray)plan["secondFloorUnits"]).OfType<JObject>()
            .Where(j => (string)j["name"] == "꽃들" || (string)j["name"] == "복순도가").ToArray();
        if (units.Length != 2) throw new InvalidOperationException("Expected exactly the two documented open-front units.");
        root = GameObject.Find(RootName)?.transform ?? throw new InvalidOperationException("Existing station fitout missing.");
        frame = root; inBox = false; batchPrefix = ""; batches.Clear(); receipts.Clear(); texMats.Clear();
        int present = units.Count(j => root.Find((string)j["batchOwner"]) != null);
        if (present == 2) { Debug.Log("OPEN_FRONTAGES_ALREADY_OWNED; no scene or assets changed."); return; }
        if (present != 0) throw new InvalidOperationException("Partial frontage ownership migration; refusing to overwrite it.");
        plaster = Reuse("Soffit_Plaster"); steel = Reuse("Stainless_Hairline"); glass = Reuse("Glass_GreenClear");
        dark = Reuse("Canopy_Dark"); led = Reuse("Light_Lens");
        ceilingPanel = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Majibang_2F_CeilingPanel.mat");
        lit = Shader.Find("Universal Render Pipeline/Lit");
        if (ceilingPanel == null || lit == null) throw new InvalidOperationException("Existing shop materials missing.");
        var removeObjects = new List<GameObject>();
        var sourceImages = new List<object>();
        foreach (JObject unit in units)
        {
            string name = (string)unit["name"], previousFace = (string)unit["frontageCorrection"]?["previousFace"];
            if ((string)unit["face"] != "+v" || (string)unit["frontage"] != "open" ||
                (string)unit["fixtures"] != "unresolved" || string.IsNullOrEmpty((string)unit["batchOwner"]) ||
                unit["frame"] != null || (previousFace != "+u" && previousFace != "-u"))
                throw new InvalidOperationException("Unexpected correction contract for " + name);
            float u0 = (float)unit["u"][0], u1 = (float)unit["u"][1], v0 = (float)unit["v"][0], v1 = (float)unit["v"][1], floor = (float)unit["floor"];
            Vector3 center = P((u0 + u1) * .5f, (v0 + v1) * .5f, floor), normal = D(previousFace == "+u" ? 1 : -1, 0);
            float width = v1 - v0, depth = u1 - u0;
            unitDoorW = 2f;
            ShopGeometry(name, center, width, depth, normal, false);
            string texture = (string)unit["fasciaTex"];
            TextureImporter importer = AssetImporter.GetAtPath(texture) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Fascia importer missing: " + texture);
            importer.GetSourceTextureWidthAndHeight(out int sw, out int sh);
            if (sw <= 0 || sh <= 0) throw new InvalidOperationException("Invalid source image dimensions: " + texture);
            sourceImages.Add(new { name, texture, sourceWidth = sw, sourceHeight = sh, currentNpotScale = importer.npotScale.ToString(),
                proposedAspect = (float)sw / sh, proposedSpan = .62f * sw / sh, proposedHeight = .62f });
            Transform sign = RequireLegacyChild("Majibang_Fascia_" + Path.GetFileNameWithoutExtension(texture));
            MeshFilter signMesh = sign.GetComponent<MeshFilter>();
            Vector3 signCenter = center + normal * (depth * .5f + .06f) + Vector3.up * 3.25f;
            if (signMesh == null || signMesh.sharedMesh == null || (signMesh.sharedMesh.bounds.center - signCenter).sqrMagnitude > .000001f ||
                sign.childCount != 0 || AssetDatabase.GetAssetPath(signMesh.sharedMesh) != Art + "/" + sign.name + ".asset")
                throw new InvalidOperationException("Legacy fascia was edited: " + name);
            removeObjects.Add(sign.gameObject);
            Transform fill = RequireLegacyChild(name + " fill light", false);
            if (fill.childCount != 0 || fill.GetComponent<Light>() == null || (fill.localPosition - center - Vector3.up * 3.1f).sqrMagnitude > .000001f)
                throw new InvalidOperationException("Legacy fill light was edited: " + name);
            removeObjects.Add(fill.gameObject);
            // These are the exact existing generated gondolas, not arbitrary props in the footprint.
            Transform[] fixtures = root.Cast<Transform>().Where(t => t.name == name + " fixture").ToArray();
            int count = Mathf.Clamp(Mathf.RoundToInt(width / 3f), 2, 6);
            if (fixtures.Length != count) throw new InvalidOperationException("Legacy fixture ownership changed: " + name);
            Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;
            var positions = new List<Vector3>();
            for (int i = 0; i < count; i++) positions.Add(center + right * (width * .8f * ((i + .5f) / count - .5f)) - normal * (depth * .3f));
            foreach (Transform fixture in fixtures)
            {
                int match = positions.FindIndex(p => (p - fixture.localPosition).sqrMagnitude < .000001f);
                var meshes = fixture.GetComponentsInChildren<MeshFilter>(true);
                BoxCollider fixtureCollider = fixture.GetComponent<BoxCollider>();
                Vector3 expectedSize = new Vector3(Mathf.Min(2.4f, width * .8f / count), 1.75f, .78f);
                Quaternion expectedRotation = Quaternion.Euler(0, Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg, 0);
                if (match < 0 || fixture.childCount != 1 || fixture.GetChild(0).name != "fit" || fixture.GetChild(0).childCount != 1 ||
                    fixture.GetChild(0).GetChild(0).name != "GONDOLA1(Clone)" || meshes.Length != 1 ||
                    (fixture.localScale - Vector3.one).sqrMagnitude > .000001f || Quaternion.Angle(fixture.localRotation, expectedRotation) > .001f ||
                    fixtureCollider == null || (fixtureCollider.size - expectedSize).sqrMagnitude > .000001f ||
                    (fixtureCollider.center - Vector3.up * expectedSize.y * .5f).sqrMagnitude > .000001f ||
                    meshes.Any(m => AssetDatabase.GetAssetPath(m.sharedMesh) != Base + "Props/StoreShelf.fbx"))
                    throw new InvalidOperationException("Legacy fixture was edited; preserve it for manual review: " + name);
                positions.RemoveAt(match); removeObjects.Add(fixture.gameObject);
            }
        }
        var remainders = new List<FrontageRemainder>();
        foreach (KeyValuePair<string, Batch> pair in batches)
        {
            Transform child = RequireLegacyChild(pair.Key);
            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || child.childCount != 0 ||
                AssetDatabase.GetAssetPath(filter.sharedMesh) != Art + "/" + pair.Key + ".asset")
                throw new InvalidOperationException("Legacy batch ownership changed: " + pair.Key);
            MeshCollider collider = child.GetComponent<MeshCollider>();
            if (collider != null && collider.sharedMesh != filter.sharedMesh)
                throw new InvalidOperationException("Edited collider on " + pair.Key);
            int[] remaining = pair.Value.RemoveOwnedTriangles(filter.sharedMesh);
            if (pair.Key.StartsWith("Shop_", StringComparison.Ordinal))
            {
                if (remaining.Length != 0) throw new InvalidOperationException("Unexpected extra geometry in " + pair.Key);
                removeObjects.Add(child.gameObject);
            }
            else remainders.Add(new FrontageRemainder { filter = filter, collider = collider, indices = remaining,
                removedTriangles = (filter.sharedMesh.triangles.Length - remaining.Length) / 3 });
        }
        var legacy = remainders.Select(r => new { name = r.filter.name, removedTriangles = r.removedTriangles, retainedTriangles = r.indices.Length / 3 }).ToArray();
        batches.Clear();
        foreach (JObject unit in units)
        {
            float u0 = (float)unit["u"][0], u1 = (float)unit["u"][1], v0 = (float)unit["v"][0], v1 = (float)unit["v"][1];
            batchPrefix = (string)unit["batchOwner"] + "_";
            ShopGeometry((string)unit["name"], P((u0 + u1) * .5f, (v0 + v1) * .5f, (float)unit["floor"]), u1 - u0, v1 - v0, D(0, 1), true);
        }
        object[] proposed = batches.Values.Select(b => b.Describe()).ToArray();
        string reportPath = ".planning/2026-09-23-video-twin/frontage-correction/" + (apply ? "integration" : "ownership-preflight") + ".json";
        if (apply)
        {
            // Stage all new shop-owned geometry before touching any existing scene object.
            Transform staging = new GameObject("Open frontages · staging").transform;
            staging.SetParent(root, false); staging.gameObject.SetActive(false);
            try
            {
                batches.Clear(); batchPrefix = ""; frame = staging;
                foreach (JObject unit in units) Unit(unit, (float)plan["floorTopY"]);
                foreach (Batch batch in batches.Values) batch.Save();
                foreach (FrontageRemainder remainder in remainders)
                {
                    remainder.replacement = UnityEngine.Object.Instantiate(remainder.filter.sharedMesh);
                    remainder.replacement.name = remainder.filter.sharedMesh.name + "_OpenFrontRemainder";
                    remainder.replacement.SetTriangles(remainder.indices, 0, false);
                    AssetDatabase.CreateAsset(remainder.replacement, AssetDatabase.GenerateUniqueAssetPath(Art + "/" + remainder.replacement.name + ".asset"));
                }
                // Keep original generated mesh assets intact; unrelated triangles and vertex channels are copied verbatim.
                foreach (FrontageRemainder remainder in remainders)
                {
                    Undo.RecordObject(remainder.filter, "Correct documented open shopfronts");
                    remainder.filter.sharedMesh = remainder.replacement;
                    if (remainder.collider != null)
                    {
                        Undo.RecordObject(remainder.collider, "Correct documented open shopfronts");
                        remainder.collider.sharedMesh = remainder.replacement;
                    }
                }
                foreach (GameObject obj in removeObjects) Undo.DestroyObjectImmediate(obj);
                while (staging.childCount > 0)
                {
                    GameObject shop = staging.GetChild(0).gameObject;
                    shop.transform.SetParent(root, false);
                    Undo.RegisterCreatedObjectUndo(shop, "Correct documented open shopfronts");
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staging.gameObject);
                frame = root; batchPrefix = ""; batches.Clear();
            }
        }
        File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            schema = "chooguard.open-frontage-cutover.v1", applied = apply, units = units.Select(j => (string)j["name"]).ToArray(),
            legacy, sourceImages, proposed, removedObjects = removeObjects.Count,
            invariants = "Footprints/floor/source shell/stairs/rails/SceneView unchanged. No scene save. No generic replacement fixtures. Historical topology, not surveyed accuracy.",
            residual = "Florist normal pedestrian approach still intersects original shell; see mesh-intersections.json. Correction does not hide this conflict."
        }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("OPEN_FRONTAGES_" + (apply ? "APPLIED" : "PREFLIGHT") + " " + reportPath);
    }

    sealed class FrontageRemainder
    {
        public MeshFilter filter;
        public MeshCollider collider;
        public int[] indices;
        public int removedTriangles;
        public Mesh replacement;
    }

    static Transform RequireLegacyChild(string name, bool identity = true)
    {
        Transform[] found = root.Cast<Transform>().Where(t => t.name == name).ToArray();
        if (found.Length != 1) throw new InvalidOperationException("Expected one directly owned legacy object: " + name);
        Transform t = found[0];
        if (identity && (t.localPosition.sqrMagnitude > .000001f || Quaternion.Angle(t.localRotation, Quaternion.identity) > .001f || (t.localScale - Vector3.one).sqrMagnitude > .000001f))
            throw new InvalidOperationException("Legacy batch transform was edited: " + name);
        return t;
    }


    // Appearance-only refresh for recovered 2F fascia evidence; never regenerates shop walls or fixtures.
    public static void RefreshFascias(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run RefreshFascias in Edit mode.");
        JObject plan = JObject.Parse(File.ReadAllText(PlanPath));
        if ((string)plan["storefronts"] == "kit") throw new InvalidOperationException("Plan uses kit storefronts: fascias come from Storefront elements; RefreshFascias would duplicate them.");
        GameObject station = GameObject.Find(RootName);
        if (station == null) throw new InvalidOperationException("Existing station fitout missing.");
        root = station.transform;
        boxFrame = root.Find("Box frame");
        boxEastB = (float)plan["boxFrame"]["eastB"];
        batches.Clear(); receipts.Clear(); texMats.Clear(); batchPrefix = "";
        JArray units = (JArray)plan["secondFloorUnits"];
        foreach (JObject unit in units)
            if (unit["fasciaRecovery"] != null) TexMat((string)unit["fasciaTex"]);
        foreach (JObject unit in units)
        {
            if (unit["fasciaRecovery"] == null) continue;
            inBox = (string)unit["frame"] == "box";
            frame = inBox ? boxFrame : root;
            if (frame == null) throw new InvalidOperationException("Existing box frame missing.");
            string name = ((string)unit["name"]).Trim(), face = (string)unit["face"];
            string owner = (string)unit["batchOwner"];
            batchPrefix = string.IsNullOrEmpty(owner) ? "" : owner + "_";
            if (!string.IsNullOrEmpty(owner))
                frame = frame.Find(owner) ?? throw new InvalidOperationException("Apply CorrectOpenFrontages before refreshing " + name + ".");
            float u0 = (float)unit["u"][0], u1 = (float)unit["u"][1], v0 = (float)unit["v"][0];
            float v1 = unit["v"][1].Type == JTokenType.Null ? EastV(u0) - .25f : (float)unit["v"][1];
            float width, depth;
            Vector3 normal;
            switch (face)
            {
                case "+u": normal = D(1, 0); width = v1 - v0; depth = u1 - u0; break;
                case "-u": normal = D(-1, 0); width = v1 - v0; depth = u1 - u0; break;
                case "+v": normal = D(0, 1); width = u1 - u0; depth = v1 - v0; break;
                case "-v": normal = D(0, -1); width = u1 - u0; depth = v1 - v0; break;
                default: throw new InvalidOperationException("Unsupported fascia face: " + face);
            }
            float floor = unit["floor"] != null ? (float)unit["floor"] : (float)plan["floorTopY"];
            Vector3 front = P((u0 + u1) * .5f, (v0 + v1) * .5f, floor) + normal * depth * .5f;
            string texture = (string)unit["fasciaTex"], prefix = batchPrefix + (inBox ? "Box_" : "");
            string meshName = prefix + "Majibang_Fascia_" + Path.GetFileNameWithoutExtension(texture);
            Transform previous = frame.Find(meshName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            foreach (TextMesh text in frame.GetComponentsInChildren<TextMesh>(true))
                if (text.transform.parent == frame && text.text == name) UnityEngine.Object.DestroyImmediate(text.gameObject);
            string signName = prefix + "SignBoards_Shop_" + name.Replace('\n', '_').Replace('/', '_').Replace(' ', '_') + "_Fascia";
            Transform sign = frame.Find(signName);
            if (sign != null) UnityEngine.Object.DestroyImmediate(sign.gameObject);
            PhotoFascia(front, width, normal, texture);
            receipts.Add(new { name, texture, evidence = unit["fasciaRecovery"], placementChanged = false });
        }
        foreach (Batch batch in batches.Values) batch.Save();
        batchPrefix = ""; ExitBox();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        File.WriteAllText(".planning/2026-09-23-video-twin/fascia-recovery-build.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(receipts, Newtonsoft.Json.Formatting.Indented));
    }

    static void BindWorldText()
    {
        const string path = Art + "/StationWorldText.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("ChooGuard/Station/WorldText");
        if (shader == null) throw new InvalidOperationException("Depth-tested world-text shader missing.");
        if (material == null)
        {
            material = new Material(shader) { name = "StationWorldText" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_Color", Color.white);
        var binding = UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Fps.StationWorldText>();
        if (binding == null) binding = new GameObject("역사 월드 안내문").AddComponent<ChooGuard.App.Fps.StationWorldText>();
        binding.Configure(font, material);
        EditorUtility.SetDirty(binding);
    }

    // Polygonal 1F tenancies retain the public map's irregular footprints rather than fitting rectangles.
    // Heights and opening widths are explicit reference-model estimates in the plan, not survey data.
    static void GroundUnit(JObject unit, float floor)
    {
        Vector2[] poly = Poly((JArray)unit["polygon"]);
        string name = (string)unit["name"], kind = (string)unit["kind"], id = (string)unit["mid"];
        float height = (float)unit["height"];
        int doorEdge = (int)unit["doorEdge"];
        var fronts = new HashSet<int>();
        foreach (JToken edge in (JArray)unit["frontEdges"]) fronts.Add((int)edge);
        var doors = new HashSet<int> { doorEdge };
        var signEdges = new HashSet<int> { doorEdge };
        if (unit["doorEdges"] is JArray extraDoors) foreach (JToken edge in extraDoors) doors.Add((int)edge);
        if (unit["signEdges"] is JArray extraSigns) foreach (JToken edge in extraSigns) signEdges.Add((int)edge);
        Batch wall = B("Unit_" + id + "_Walls", plaster, true);
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 p = poly[i], q = poly[(i + 1) % poly.Length];
            Vector3 a = P(p.x, p.y, floor), b = P(q.x, q.y, floor);
            Vector3 edge = b - a, middle = (a + b) * .5f;
            float width = edge.magnitude;
            if (!fronts.Contains(i)) { Wall(wall, a, b, height); continue; }
            if (kitStorefronts && kind != "vacant" && kind != "hoarding") continue;
            Vector3 normal = D(q.y - p.y, p.x - q.x);
            if (kind == "vacant" || kind == "hoarding")
            {
                Wall(wall, a, b, height);
            }
            else if (doors.Contains(i))
            {
                Vector3 halfDoor = edge.normalized * Mathf.Min((float)unit["doorWidth"], width * .65f) * .5f;
                Facade(a, middle - halfDoor, height - .65f, "Shopfront");
                Facade(middle + halfDoor, b, height - .65f, "Shopfront");
            }
            else Facade(a, b, height - .65f, "Shopfront");
            Vector3 low = Vector3.up * (height - .65f), high = Vector3.up * height;
            Batch band = B("Unit_" + id + "_FasciaBand", dark);
            band.Quad(a + low, b + low, b + high, a + high);
            band.Quad(a + high, b + high, b + low, a + low);
            if (!signEdges.Contains(i)) continue;
            string texture = (string)unit["fasciaTex"];
            if (!string.IsNullOrEmpty(texture))
            {
                Material mat = TexMat(texture);
                Texture image = mat.GetTexture("_BaseMap");
                float h = .5f, w = Mathf.Min(width - .25f, h * image.width / image.height);
                Vector3 c = middle + Vector3.up * (height - .34f) + normal * .06f;
                Vector3 right = Vector3.Cross(Vector3.up, -normal);
                B("Fascia_" + id, mat).TexQuad(c - right * w * .5f - Vector3.up * h * .5f,
                    c + right * w * .5f - Vector3.up * h * .5f,
                    c + right * w * .5f + Vector3.up * h * .5f,
                    c - right * w * .5f + Vector3.up * h * .5f, normal);
            }
            else
            {
                string label = kind == "hoarding" ? "COMING SOON\n비엔씨 Donuts" : name;
                Sign(label, middle.z, middle.x, floor + height - .34f,
                    Mathf.Min(width - .25f, 6f), .58f, normal.z, normal.x, dark, Color.white);
            }
        }
        PolySlab("ShopCeiling", ceilingPanel, poly, floor + height, .06f, false);
        receipts.Add(new { name, mapId = id, evidence = unit["evidence"], facade = unit["fasciaStatus"] });
    }

    // ---------------- 2F floor (original-map cells) ----------------
    static int SecondFloor(JObject plan)
    {
        float top = (float)plan["floorTopY"], depth = (float)plan["slabDepth"];
        var rows = new SortedDictionary<int, List<int>>();
        foreach (JArray cell in (JArray)plan["cells"])
        {
            int u = (int)cell[0], v = (int)cell[1];
            if (!rows.TryGetValue(v, out var list)) rows[v] = list = new List<int>();
            list.Add(u);
        }
        // Plan "slabOpenings" (root frame): the floor is clipped exactly against each polygon and guarded on the 2F side.
        var openings = new List<(string id, List<Vector2> poly, JObject spec)>();
        if (plan["slabOpenings"] is JArray ops)
            foreach (JObject o in ops)
            {
                string oid = (string)o["id"] ?? "slabOpening" + openings.Count;
                List<Vector2> poly = KPoly(KT(o, "polygon", oid));
                if (poly.Count < 3 || Mathf.Abs(KArea(poly)) < .01f) throw new InvalidOperationException("Degenerate slab opening " + oid);
                openings.Add((oid, poly, o));
            }
        var holes = openings.Select(o => o.poly).ToList();
        Batch slab = B("Majibang_2F_Floor", stone, true);
        int cells = 0, clipped = 0;
        foreach (var row in rows)
        {
            row.Value.Sort();
            int start = row.Value[0], last = start;
            for (int i = 1; i <= row.Value.Count; i++)
            {
                if (i < row.Value.Count && row.Value[i] == last + 1) { last = row.Value[i]; continue; }
                float u0 = start - .5f, u1 = last + .5f, v0 = row.Key - .5f, v1 = row.Key + .5f;
                if (holes.Any(h => h.Max(q => q.x) > u0 && h.Min(q => q.x) < u1 && h.Max(q => q.y) > v0 && h.Min(q => q.y) < v1)) { ClippedSlab(slab, u0, u1, v0, v1, holes, top, depth); clipped++; }
                else slab.Slab(u0, u1, v0, v1, top, depth);
                cells += last - start + 1;
                if (i < row.Value.Count) start = last = row.Value[i];
            }
        }
        slabOpeningReport.Clear();
        foreach (var o in openings)
        {
            int[] guarded = OpeningGuard(o.id, o.poly, o.spec, top);
            slabOpeningReport.Add(new { o.id, polygon = o.spec["polygon"], area = Math.Round(Mathf.Abs(KArea(o.poly)), 3), guardedEdges = guarded, evidence = o.spec["evidence"] });
        }
        if (openings.Count > 0) Debug.Log("SLAB_OPENINGS " + openings.Count + " clippedRuns=" + clipped);
        return cells;
    }
    static readonly List<object> slabOpeningReport = new List<object>();

    // Slab run minus the opening polygons: exact top and underside (outer minus union of openings) and closed vertical faces on
    // every boundary piece; top/underside UVs follow Batch.Slab (local x,z).
    static void ClippedSlab(Batch b, float u0, float u1, float v0, float v1, List<List<Vector2>> openings, float top, float depth)
    {
        var rect = new List<Vector2> { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
        List<Vector2> t = KTriangulate(rect, openings);
        for (int i = 0; i < t.Count; i += 3)
            foreach (float h in new[] { top, top - depth })
            {
                Vector3 p0 = P(t[i].x, t[i].y, h), p1 = P(t[i + 1].x, t[i + 1].y, h), p2 = P(t[i + 2].x, t[i + 2].y, h);
                b.Tri(p0, p1, p2, new Vector2(p0.x, p0.z), new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z), h == top ? Vector3.up : Vector3.down);
            }
        var rings = new List<List<Vector2>> { rect }; rings.AddRange(openings);
        Vector3 dn = Vector3.down * depth;
        foreach (List<Vector2> ring in rings)
            for (int i = 0; i < ring.Count; i++)
                foreach (var piece in KBoundary(rings, ring[i], ring[(i + 1) % ring.Count]))
                {
                    Vector2 s = Vector2.Lerp(ring[i], ring[(i + 1) % ring.Count], piece.t0), e = Vector2.Lerp(ring[i], ring[(i + 1) % ring.Count], piece.t1);
                    Vector3 a = P(s.x, s.y, top), c = P(e.x, e.y, top), outward = -KD(piece.inward);
                    if (Vector3.Dot(Vector3.Cross(c - a, c - (a + dn)), outward) >= 0) b.Quad(a + dn, c + dn, c, a); else b.Quad(c + dn, a + dn, a, c);
                }
    }

    // Glass guard on the 2F side of a slab opening: stainless shoe, posts <= 1.2 m, top rail, double-sided glass (with collider).
    // guard: {height 1.1, finish "glass"|<material>, edges "all"|[edge indices], open [edge indices left open for escalators/stairs]};
    // "guard": false builds none. Edge i runs polygon[i] -> polygon[i+1]. Returns the guarded edge indices.
    static int[] OpeningGuard(string id, List<Vector2> poly, JObject o, float floorY)
    {
        JToken gt = o["guard"];
        if (gt != null && gt.Type == JTokenType.Boolean && !(bool)gt) return new int[0];
        JObject g = gt as JObject ?? new JObject();
        float h = KF(g, "height", 1.1f); string finish = KS(g, "finish", "glass");
        JToken es = g["edges"]; bool all = es == null || es.Type == JTokenType.String && (string)es == "all";
        if (!all && !(es is JArray)) throw new InvalidOperationException(id + ": guard.edges must be \"all\" or an index list.");
        var edges = new HashSet<int>(); if (es is JArray el) foreach (JToken t in el) edges.Add((int)t);
        var open = new HashSet<int>(); if (g["open"] is JArray ol) foreach (JToken t in ol) open.Add((int)t);
        Material panel = finish == "glass" ? glass : KM(finish);
        Batch gl = B("Majibang_OpeningGuard_" + panel.name, panel, true), st = B("Majibang_OpeningGuard_Frame", steel);
        Vector3 up = Vector3.up; var guarded = new List<int>();
        for (int i = 0; i < poly.Count; i++)
        {
            if ((!all && !edges.Contains(i)) || open.Contains(i)) continue;
            Vector2 a2 = poly[i], b2 = poly[(i + 1) % poly.Count]; float len = Vector2.Distance(a2, b2);
            if (len < .05f) continue;
            Vector2 d2 = (b2 - a2) / len, perp = new Vector2(-d2.y, d2.x);
            if (KInRing(poly, (a2 + b2) * .5f + perp * .05f)) perp = -perp;
            Vector3 a = KP(a2 + perp * .06f, floorY), c = KP(b2 + perp * .06f, floorY), dir = (c - a) / len, n = KD(perp);
            KBox(st, (a + c) * .5f + up * .05f, dir, up, n, new Vector3(len, .1f, .06f), false);
            KGlass(gl, a + up * .1f, dir, up, len, h - .16f, n);
            int posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.2f));
            for (int k = 0; k <= posts; k++) { Vector3 q = Vector3.Lerp(a, c, (float)k / posts); KTube(st, q, q + up * h, .025f, 10); }
            KTube(st, a + up * h, c + up * h, .03f, 12);
            guarded.Add(i);
        }
        return guarded.ToArray();
    }

    // ---------------- 3F L (board registered to the original box) ----------------
    static void ThirdFloor(JObject t)
    {
        bool box = (string)t["frame"] == "box";
        if (box) EnterBox();
        float y = (float)t["y"], depth = (float)t["depth"];
        foreach (JObject s in (JArray)t["slabs"]) PolySlab((string)s["name"], stone, Poly((JArray)s["poly"]), y, depth, true);
        // thirdFloor.railGuard false: kit Balustrades (3F 보강) replace the builder guard; the slab-edge fascia stays.
        bool railGuard = t["railGuard"] == null || t["railGuard"].Type != JTokenType.Boolean || (bool)t["railGuard"];
        foreach (JArray r in (JArray)t["rails"])
        {
            float u0 = (float)r[0], v0 = (float)r[1], u1 = (float)r[2], v1 = (float)r[3];
            if (railGuard) Rail(u0, v0, u1, v1, y);
            // Slab-edge fascia band under every void edge.
            Vector3 a = P(u0, v0, y - depth), b = P(u1, v1, y - depth), dn = Vector3.down * .85f;
            Batch f = B("Majibang_3F_EdgeFascia", plaster);
            f.Quad(a + dn, b + dn, b, a); f.Quad(a, b, b + dn, a + dn);
        }
        JObject c = (JObject)t["ceiling2F"];
        LowCeiling(Poly((JArray)c["poly"]), (float)c["y"]);
        foreach (JObject unit in (JArray)t["units"]) Unit(unit, y);
        JArray door = (JArray)t["eastDoor"]["u"];
        float du0 = (float)door[0], du1 = (float)door[1], dm = (du0 + du1) * .5f;
        Sign("출입문  Entrance", dm, EastV(dm) - .3f, y + 2.9f, 3.2f, .5f, 0, -1, blue, Color.white);
        receipts.Add(new { region = "3F 식당가 L", frame = box ? "box" : "station", basis = t["basis"], eastDoor = t["eastDoor"] });
        if (t["edgeBands"] is JArray bands) foreach (JObject band in bands) EdgeBand(band);
        if (t["frontSigns"] is JArray fronts)
            foreach (JObject s in fronts)
            {
                JArray bg = (JArray)s["bg"], ink = (JArray)s["ink"];
                Sign((string)s["text"], (float)s["u"], (float)s["v"], (float)s["y"], (float)s["w"], (float)s["h"], (float)s["n"][0], (float)s["n"][1],
                    Colour("Sign_" + (string)s["id"], new Color((float)bg[0], (float)bg[1], (float)bg[2])), new Color((float)ink[0], (float)ink[1], (float)ink[2]));
            }
        if (box) ExitBox();
    }

    // ---------------- lattice hall structure (box frame) ----------------
    // Tree-column layout is a visual reference fit, not metrically registered SfM evidence.
    // Trusses follow that inferred grid; their heights are sampled from the original roof underside.
    static readonly HashSet<Collider> originalRoof = new HashSet<Collider>();
    static float RoofUnder(float a, float b)
    {
        Vector3 o = frame.TransformPoint(P(a, b, 12.6f));
        float best = float.MaxValue;
        foreach (RaycastHit h in Physics.RaycastAll(o, Vector3.up, 30f))
            if (originalRoof.Contains(h.collider) && h.distance < best) best = h.distance;
        if (best == float.MaxValue) throw new InvalidOperationException("No original roof above box point " + a + "," + b);
        return 12.6f + best;
    }

    static void HallStructure(JObject h, float floor)
    {
        EnterBox();
        originalRoof.Clear();
        GameObject shell = GameObject.Find("FPSWorld/공식 자료 부산역 역사");
        if (shell == null) throw new InvalidOperationException("Original station shell missing.");
        foreach (Collider c in shell.GetComponentsInChildren<Collider>(true)) originalRoof.Add(c);
        Material white = Reuse("Tube_White"), trussMat = hallWhite ? KM("Kit_TrussWhite") : white;
        JObject r = (JObject)h["roof"];
        float a0 = (float)r["a"][0], a1 = (float)r["a"][1], b0 = (float)r["b"][0], b1 = (float)r["b"][1];
        float depth = (float)h["trussDepth"], gap = (float)h["deckGap"];
        var primaryA = new HashSet<float>(); var primaryB = new HashSet<float>();
        foreach (JToken t in (JArray)h["primaryA"]) primaryA.Add((float)t);
        foreach (JToken t in (JArray)h["primaryB"]) primaryB.Add((float)t);
        int members = 0;
        // Trusses along a (constant b) and along b (constant a).
        foreach (JToken t in (JArray)h["linesB"])
        {
            float b = (float)t; bool main = primaryB.Contains(b);
            members += Truss(a0, b, a1, b, (float)h["panelA"], depth, gap, main ? "Hall_PrimaryTruss" : "Hall_SecondaryTruss", trussMat, main);
        }
        foreach (JToken t in (JArray)h["linesA"])
        {
            float a = (float)t; bool main = primaryA.Contains(a);
            members += Truss(a, b0, a, b1, (float)h["panelB"], depth, gap, main ? "Hall_PrimaryTruss" : "Hall_SecondaryTruss", trussMat, main);
        }
        if (hallWhite) HallDeck(a0, a1, b0, b1, ((JArray)h["linesB"]).Select(t => (float)t).ToList());
        // Tree columns: shaft to the branch point, four branches to bottom-chord nodes, lantern in the cup.
        float reachA = (float)h["branchReach"][0], reachB = (float)h["branchReach"][1], branchY = (float)h["branchY"];
        foreach (JObject c in (JArray)h["treeColumns"])
        {
            float ca = (float)c["a"], cb = (float)c["b"];
            Vector3 foot = P(ca, cb, floor), top = P(ca, cb, branchY);
            B("Hall_TreeColumn_Shaft", white, true).Tube(foot, top, .45f, 20);
            B("Hall_TreeColumn_Collar", white).Tube(top - Vector3.up * .35f, top + Vector3.up * .15f, .62f, 20);
            foreach (Vector2 s in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
            {
                float ta = ca + s.x * reachA, tb = Mathf.Clamp(cb + s.y * reachB, b0 + .3f, b1 - .3f);
                Vector3 tip = P(ta, tb, RoofUnder(ta, tb) - gap - depth);
                B("Hall_TreeColumn_Branches", white).Tube(top, tip, .22f, 12);
            }
            Lantern(ca, cb, branchY + .35f, (float)h["lantern"]["radius"], (float)h["lantern"]["height"]);
            receipts.Add(new { region = "Tree column " + (string)c["id"], a = ca, b = cb, evidence = (string)c["evidence"] });
        }
        // Clerestory glazing on the original roof steps (west and east edges of the raised deck).
        foreach (JObject g in (JArray)h["clerestories"])
        {
            float gb = (float)g["b"], lowY = (float)g["lowY"], probe = (float)g["probe"];
            Batch glazing = B("Hall_Clerestory_Glass", glass);
            Batch mullions = B("Hall_Clerestory_Mullions", white);
            for (float a = a0; a < a1 - .01f; a += 3f)
            {
                float an = Mathf.Min(a + 3f, a1);
                float t0 = RoofUnder(a, gb + probe) - .05f, t1 = RoofUnder(an, gb + probe) - .05f;
                Vector3 p0 = P(a, gb, lowY), p1 = P(an, gb, lowY), q0 = P(a, gb, t0), q1 = P(an, gb, t1);
                glazing.Quad(p0, p1, q1, q0); glazing.Quad(q0, q1, p1, p0);
                mullions.Tube(p0, q0, .05f, 6);
            }
        }
        receipts.Add(new { region = "Lattice hall roof", members, frame = "box", basis = h["basis"], appearance = hallWhite ? "white (BL-05/CX-19)" : "builder" });
        if (h["edgeColumns"] is JArray ecs)
            foreach (JObject c in ecs)
            {
                float ca = (float)c["a"], cb = (float)c["b"], cr = (float)c["radius"];
                B("Hall_EdgeColumn", white, true).Tube(P(ca, cb, floor), P(ca, cb, (float)c["top"]), cr, 20);
                receipts.Add(new { region = "Slab-edge column " + (string)c["id"], a = ca, b = cb, evidence = (string)c["evidence"] });
            }
        ExitBox();
    }

    // Flat Warren truss with verticals under the original deck; nodes under the low roof are dropped. Returns member count.
    static float minDeckY = 19.5f;
    static int Truss(float a0, float b0, float a1, float b1, float panel, float depth, float gap, string batch, Material mat, bool main)
    {
        float len = Mathf.Sqrt((a1 - a0) * (a1 - a0) + (b1 - b0) * (b1 - b0));
        int n = Mathf.Max(2, Mathf.RoundToInt(len / panel));
        var top = new Vector3[n + 1]; var bottom = new Vector3[n + 1]; var ok = new bool[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n, a = Mathf.Lerp(a0, a1, t), b = Mathf.Lerp(b0, b1, t);
            float y = RoofUnder(a, b) - gap;
            ok[i] = y + gap >= minDeckY;
            top[i] = P(a, b, y); bottom[i] = P(a, b, y - depth);
        }
        float chord = main ? .19f : .13f, web = main ? .09f : .065f;
        Batch chords = B(batch + "_Chords", mat), webs = B(batch + "_Webs", mat);
        int m = 0;
        for (int i = 0; i < n; i++)
        {
            if (!ok[i] || !ok[i + 1]) continue;
            // White appearance: smooth round sections (same lines and radii); builder: faceted tubes.
            if (hallWhite)
            {
                KTube(chords, top[i], top[i + 1], chord, 14); KTube(chords, bottom[i], bottom[i + 1], chord, 14);
                KTube(webs, i % 2 == 0 ? bottom[i] : top[i], i % 2 == 0 ? top[i + 1] : bottom[i + 1], web, 10);
                KTube(webs, bottom[i], top[i], web * .8f, 10); KTube(webs, bottom[i + 1], top[i + 1], web * .8f, 10);
            }
            else
            {
                chords.Tube(top[i], top[i + 1], chord, 10); chords.Tube(bottom[i], bottom[i + 1], chord, 10);
                webs.Tube(i % 2 == 0 ? bottom[i] : top[i], i % 2 == 0 ? top[i + 1] : bottom[i + 1], web, 8);
                webs.Tube(bottom[i], top[i], web * .8f, 8);
                webs.Tube(bottom[i + 1], top[i + 1], web * .8f, 8);
            }
            m += 5;
        }
        return m;
    }

    // BL-05 / CX-19: white daylit deck liner 30 mm under the original roof underside (1.5 m sampled height field) and skylight
    // strips (1.2 m) centred between the constant-b truss lines. Cells under the low roof (below minDeckY) are left out.
    static void HallDeck(float a0, float a1, float b0, float b1, List<float> linesB)
    {
        const float step = 1.5f, sky = .6f;
        int na = Mathf.CeilToInt((a1 - a0) / step), nb = Mathf.CeilToInt((b1 - b0) / step);
        var y = new float[na + 1, nb + 1];
        for (int i = 0; i <= na; i++) for (int j = 0; j <= nb; j++) y[i, j] = RoofUnder(a0 + (a1 - a0) * i / na, b0 + (b1 - b0) * j / nb);
        Batch deck = B("Hall_DeckLiner", KM("Kit_DeckWhite"));
        for (int i = 0; i < na; i++)
            for (int j = 0; j < nb; j++)
            {
                if (y[i, j] < minDeckY || y[i + 1, j] < minDeckY || y[i + 1, j + 1] < minDeckY || y[i, j + 1] < minDeckY) continue;
                float ta = a0 + (a1 - a0) * i / na, tb = b0 + (b1 - b0) * j / nb, ua = a0 + (a1 - a0) * (i + 1) / na, ub = b0 + (b1 - b0) * (j + 1) / nb;
                deck.Face(P(ta, tb, y[i, j] - .03f), P(ua, tb, y[i + 1, j] - .03f), P(ua, ub, y[i + 1, j + 1] - .03f), P(ta, ub, y[i, j + 1] - .03f),
                    new Vector2(ta, tb), new Vector2(ua, tb), new Vector2(ua, ub), new Vector2(ta, ub), Vector3.down);
            }
        Batch strips = B("Hall_Skylights", KM(Lane + "roof_daylight_glazing.mat"));
        linesB.Sort();
        for (int k = 0; k + 1 < linesB.Count; k++)
        {
            float bc = (linesB[k] + linesB[k + 1]) * .5f;
            for (int i = 0; i < na; i++)
            {
                float ta = a0 + (a1 - a0) * i / na, ua = a0 + (a1 - a0) * (i + 1) / na, y0 = RoofUnder(ta, bc), y1 = RoofUnder(ua, bc);
                if (y0 < minDeckY || y1 < minDeckY) continue;
                strips.Face(P(ta, bc - sky, y0 - .05f), P(ua, bc - sky, y1 - .05f), P(ua, bc + sky, y1 - .05f), P(ta, bc + sky, y0 - .05f),
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), Vector3.down);
            }
        }
    }

    // Octagonal lantern seated on the column top (G-B07 / BL-09, photos bodamew_004 / best_pb_han_023): dark bronze frame,
    // tapered base cone (radius -> .5 over .6 m) under warm translucent emissive panes, one warm point light.
    static void Lantern(float a, float b, float y0, float radius, float height)
    {
        Material bronze = KM("#2B2F33");
        Batch panes = B("Hall_Lantern_Glass", KLanternGlass()), frameB = B("Hall_Lantern_Frame", bronze);
        const int sides = 8;
        for (int i = 0; i < sides; i++)
        {
            float t0 = i * Mathf.PI * 2 / sides, t1 = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(t0), 0, Mathf.Sin(t0)), d1 = new Vector3(Mathf.Cos(t1), 0, Mathf.Sin(t1));
            Vector3 e0 = d0 * radius, e1 = d1 * radius;
            Vector3 c0 = P(a, b, y0), c1 = P(a, b, y0 + height), cb = P(a, b, y0 - .6f);
            panes.Quad(c0 + e0, c0 + e1, c1 + e1, c1 + e0); panes.Quad(c1 + e0, c1 + e1, c0 + e1, c0 + e0);
            frameB.Tube(c0 + e0, c1 + e0, .05f, 6);
            for (int k = 0; k <= 4; k++) frameB.Tube(c0 + e0 + Vector3.up * height * k / 4f, c0 + e1 + Vector3.up * height * k / 4f, .04f, 6);
            // Base cone: outward-facing frustum faces plus the bottom cap.
            Vector3 out0 = (d0 + d1).normalized;
            frameB.Face(cb + d0 * .5f, cb + d1 * .5f, c0 + e1, c0 + e0, Vector2.zero, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), out0 - Vector3.up * .5f);
            frameB.Tri(cb, cb + d1 * .5f, cb + d0 * .5f, Vector2.zero, Vector2.right, Vector2.up, Vector3.down);
        }
        frameB.Tube(P(a, b, y0 + height), P(a, b, y0 + height + 2.2f), .06f, 6);
        var lamp = new GameObject("Hall_Lantern_Light").AddComponent<Light>();
        lamp.transform.SetParent(frame, false); lamp.transform.localPosition = P(a, b, y0 + height * .5f);
        lamp.type = LightType.Point; lamp.range = 16f; lamp.intensity = 2.2f; lamp.color = new Color(1f, .93f, .82f);
        lamp.shadows = LightShadows.None; lamp.renderMode = LightRenderMode.ForcePixel;
    }
    static Material KLanternGlass()
    {
        if (kitMats.TryGetValue("Kit_LanternGlass", out Material m)) return m;
        m = KitCreate("Kit_LanternGlass", lit, x =>
        {
            x.SetColor("_BaseColor", new Color(1f, .89f, .69f, .6f)); x.SetFloat("_Smoothness", .6f); x.SetFloat("_Metallic", 0);
            x.SetFloat("_Surface", 1); x.SetFloat("_Blend", 0); x.SetFloat("_ZWrite", 0);
            x.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); x.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            x.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); x.SetOverrideTag("RenderType", "Transparent"); x.renderQueue = (int)RenderQueue.Transparent;
            x.EnableKeyword("_EMISSION"); x.SetColor("_EmissionColor", new Color(1f, .89f, .69f) * 1.3f);
            x.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        });
        kitMats["Kit_LanternGlass"] = m;
        return m;
    }

    // Black fascia band on a 3F slab edge, facing the void, carrying photo-textured LED panels.
    // Band: from (a0,b0) to (a1,b1) in the current frame, outward normal (nu,nv); panels: {"tex", "s":[s0,s1] along the band}.
    static void EdgeBand(JObject e)
    {
        float a0 = (float)e["from"][0], b0 = (float)e["from"][1], a1 = (float)e["to"][0], b1 = (float)e["to"][1];
        float nu = (float)e["normal"][0], nv = (float)e["normal"][1], y0 = (float)e["y"][0], y1 = (float)e["y"][1];
        Vector3 n = D(nu, nv), p0 = P(a0, b0, 0) + n * .06f, p1 = P(a1, b1, 0) + n * .06f;
        Vector3 along = (p1 - p0).normalized; float len = (p1 - p0).magnitude;
        Batch band = B("Majibang_3F_EdgeBand", dark);
        Vector3 lo0 = p0 + Vector3.up * y0, lo1 = p1 + Vector3.up * y0, hi0 = p0 + Vector3.up * y1, hi1 = p1 + Vector3.up * y1;
        // Wound so the face is visible from the void side (normal n).
        if (Vector3.Dot(Vector3.Cross(lo1 - lo0, hi0 - lo0), n) > 0) band.Quad(lo0, lo1, hi1, hi0); else band.Quad(lo0, hi0, hi1, lo1);
        foreach (JObject pnl in (JArray)e["panels"])
        {
            float s0 = (float)pnl["s"][0], s1 = (float)pnl["s"][1], py0 = (float)pnl["y"][0], py1 = (float)pnl["y"][1];
            if (s1 > len + .01f) throw new InvalidOperationException("Panel beyond band end: " + pnl["tex"]);
            Material m = TexMat((string)pnl["tex"]);
            Vector3 c0 = p0 + along * s0 + n * .05f, c1 = p0 + along * s1 + n * .05f;
            Batch b = B("Majibang_AdPanel_" + System.IO.Path.GetFileNameWithoutExtension((string)pnl["tex"]), m);
            Vector3 q0 = c0 + Vector3.up * py0, q1 = c1 + Vector3.up * py0, q2 = c1 + Vector3.up * py1, q3 = c0 + Vector3.up * py1;
            // Texture reads left-to-right as seen from the void: the viewer's right is up x (-n).
            bool alongIsRight = Vector3.Dot(Vector3.Cross(Vector3.up, -n), along) > 0;
            if (alongIsRight) b.TexQuad(q0, q1, q2, q3, n); else b.TexQuad(q1, q0, q3, q2, n);
        }
        receipts.Add(new { region = (string)e["name"], panels = e["panels"], evidence = (string)e["evidence"] });
    }

    static readonly Dictionary<string, Material> texMats = new Dictionary<string, Material>();
    // Unlit lightbox material from a project texture (LED panels read by their own light).
    static Material TexMat(string texPath)
    {
        if (texMats.TryGetValue(texPath, out Material cached)) return cached;
        AssetDatabase.ImportAsset(texPath);
        // Recovered crops are not power-of-two images. Resampling each axis independently distorts lettering.
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer != null && importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) throw new InvalidOperationException("Panel texture missing: " + texPath);
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        Material m = Colour("Panel_" + System.IO.Path.GetFileNameWithoutExtension(texPath), Color.white, unlitShader);
        m.SetTexture("_BaseMap", tex); EditorUtility.SetDirty(m);
        texMats[texPath] = m;
        return m;
    }

    // A textured (photo) or lettered panel hung at (u,v) in its frame, facing normal n; optional dark backing box.
    static void WallPanel(JObject p)
    {
        bool enter = (string)p["frame"] == "box" && !inBox;
        if (enter) EnterBox();
        float u = (float)p["u"], v = (float)p["v"], w = (float)p["w"], y0 = (float)p["y"][0], y1 = (float)p["y"][1];
        Vector3 n = D((float)p["n"][0], (float)p["n"][1]), right = Vector3.Cross(Vector3.up, -n).normalized, c = P(u, v, 0);
        if (p["tex"] != null)
        {
            float depth = p["depth"] != null ? (float)p["depth"] : .12f;
            Vector3 back = c - n * depth;
            // Closed dark backer box from the panel plane back to `depth` (G-B17 / BL-26): back-to-back panels
            // (sign_Tickets_1F / sign_FoodCourt_Tickets) share one solid dark block instead of two open planes with a see-through gap.
            Batch housing = B("Majibang_PanelHousing", dark);
            KBox(housing, (c + back) * .5f + Vector3.up * (y0 + y1) * .5f, right, Vector3.up, n, new Vector3(w + .16f, y1 - y0 + .16f, depth));
            Material m = TexMat((string)p["tex"]);
            Batch b = B("Majibang_WallPanel_" + System.IO.Path.GetFileNameWithoutExtension((string)p["tex"]), m);
            Vector3 f = c + n * .02f, l = f - right * w * .5f, r = f + right * w * .5f;
            b.TexQuad(l + Vector3.up * y0, r + Vector3.up * y0, r + Vector3.up * y1, l + Vector3.up * y1, n);
        }
        else
        {
            JArray bg = (JArray)p["bg"], ink = (JArray)p["ink"];
            Sign((string)p["text"], u, v, (y0 + y1) * .5f, w, y1 - y0, (float)p["n"][0], (float)p["n"][1],
                Colour("Sign_" + (string)p["id"], new Color((float)bg[0], (float)bg[1], (float)bg[2])), new Color((float)ink[0], (float)ink[1], (float)ink[2]));
        }
        receipts.Add(new { region = (string)p["id"], frame = inBox ? "box" : "station", u, v, y = p["y"], evidence = (string)p["evidence"] });
        if (enter) ExitBox();
    }

    static Vector2[] Poly(JArray a)
    {
        var p = new Vector2[a.Count];
        for (int i = 0; i < a.Count; i++) p[i] = new Vector2((float)a[i][0], (float)a[i][1]);
        return p;
    }

    // ---------------- original door leaves opened for walking ----------------
    // Always cuts from the ORIGINAL FBX mesh, so reruns are idempotent and the original stays restorable.
    static void DoorOpenings(JObject d)
    {
        string target = (string)d["target"];
        GameObject layer = GameObject.Find((string)d["layer"]);
        if (layer == null) throw new InvalidOperationException("Original layer missing: " + d["layer"]);
        MeshFilter filter = null;
        foreach (MeshFilter f in layer.GetComponentsInChildren<MeshFilter>(true)) if (f.name == target) filter = f;
        if (filter == null) throw new InvalidOperationException("Original door mesh missing: " + target);
        string fbx = (string)d["fbx"] ?? "Assets/ChooGuard/Art/OfficialBusanStation/OfficialBusanStation_PlatformsParking.fbx";
        Mesh original = null;
        foreach (UnityEngine.Object a in AssetDatabase.LoadAllAssetsAtPath(fbx)) if (a is Mesh m && m.name == target) original = m;
        if (original == null) throw new InvalidOperationException("Original FBX mesh missing: " + target);
        ChooGuard.Editor.FpsSourceOpening.Assign(filter, original);
        float sn = Mathf.Sin(Theta * Mathf.Deg2Rad), cs = Mathf.Cos(Theta * Mathf.Deg2Rad);
        var boxes = new List<(Bounds, Vector3)>();
        foreach (JObject o in (JArray)d["openings"])
        {
            float u0 = (float)o["u"][0], u1 = (float)o["u"][1], v0 = (float)o["v"][0], v1 = (float)o["v"][1], y0 = (float)o["y"][0], y1 = (float)o["y"][1];
            float u = (u0 + u1) * .5f, v = (v0 + v1) * .5f;
            var centre = new Vector3(u * sn + v * cs, (y0 + y1) * .5f, u * cs - v * sn);
            // Size order matches the rotated local (v, y, u) basis, as VideoStationIntegration.Station.
            boxes.Add((new Bounds(centre, new Vector3(v1 - v0, y1 - y0, u1 - u0)), new Vector3(0, Theta, 0)));
        }
        Mesh cut = ChooGuard.Editor.FpsSourceOpening.Subtract(filter, boxes, target + "_MajibangDoors");
        string path = Art + "/" + target + "_MajibangDoors.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(cut, path);
        ChooGuard.Editor.FpsSourceOpening.Assign(filter, cut);
        receipts.Add(new { region = (string)d["region"] ?? "Original door bank opened", target, originalAsset = fbx, generatedAsset = path, openings = d["openings"], basis = (string)d["basis"] });
    }

    static bool InPoly(Vector2[] poly, float u, float v)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > v) != (poly[j].y > v) && u < (poly[j].x - poly[i].x) * (v - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    }

    // Ear-clipped horizontal slab of any simple polygon in station (u,v).
    static void PolySlab(string name, Material mat, Vector2[] input, float top, float depth, bool collision)
    {
        var poly = new List<Vector2>(input);
        float area = 0;
        for (int i = 0; i < poly.Count; i++) { Vector2 p = poly[i], q = poly[(i + 1) % poly.Count]; area += p.x * q.y - q.x * p.y; }
        if (area < 0) poly.Reverse();
        Batch b = B(name, mat, collision);
        Vector3 dn = Vector3.down * depth;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
            Vector3 a = P(p.x, p.y, top), c = P(q.x, q.y, top);
            b.Quad(a + dn, c + dn, c, a);
        }
        var idx = new List<int>(); for (int i = 0; i < poly.Count; i++) idx.Add(i);
        int guard = 0;
        while (idx.Count > 3)
        {
            if (guard++ > 4096) throw new InvalidOperationException("Slab polygon could not be triangulated: " + name);
            bool clipped = false;
            for (int k = 0; k < idx.Count && !clipped; k++)
            {
                int ia = idx[(k + idx.Count - 1) % idx.Count], ic = idx[k], id = idx[(k + 1) % idx.Count];
                Vector2 a = poly[ia], c = poly[ic], d = poly[id];
                if ((c.x - a.x) * (d.y - a.y) - (c.y - a.y) * (d.x - a.x) <= 0) continue;
                bool inside = false;
                foreach (int j in idx)
                    if (j != ia && j != ic && j != id && InTriangle(poly[j], a, c, d)) { inside = true; break; }
                if (inside) continue;
                SlabTriangle(b, a, c, d, top, depth); idx.RemoveAt(k); clipped = true;
            }
            if (!clipped) throw new InvalidOperationException("No ear found while triangulating " + name);
        }
        SlabTriangle(b, poly[idx[0]], poly[idx[1]], poly[idx[2]], top, depth);
    }

    static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    // Counter-clockwise in (u,v) is front-facing upward in the rotated (v,y,u) local frame.
    static void SlabTriangle(Batch b, Vector2 a, Vector2 c, Vector2 d, float top, float depth)
    {
        b.Triangle(P(a.x, a.y, top), P(c.x, c.y, top), P(d.x, d.y, top));
        b.Triangle(P(a.x, a.y, top - depth), P(d.x, d.y, top - depth), P(c.x, c.y, top - depth));
    }

    static void Rail(float u0, float v0, float u1, float v1, float y)
    {
        Vector3 a = P(u0, v0, y + .1f), b = P(u1, v1, y + .1f), h = Vector3.up * .95f;
        Batch g = B("Majibang_3F_RailGlass", glass, true);
        g.Quad(a, b, b + h, a + h); g.Quad(a + h, b + h, b, a);
        B("Majibang_3F_RailMetal", steel).Tube(a + Vector3.up * 1.02f, b + Vector3.up * 1.02f, .045f, 10);
        int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2f));
        for (int i = 0; i <= n; i++) { Vector3 p = Vector3.Lerp(a, b, (float)i / n); B("Majibang_3F_RailMetal", steel).Tube(p - Vector3.up * .1f, p + Vector3.up * 1.02f, .035f, 8); }
    }

    // 2F low ceiling under the 3F slab: 1.2m panels with a square LED every fourth panel.
    static void LowCeiling(Vector2[] poly, float y)
    {
        float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
        foreach (Vector2 p in poly) { u0 = Mathf.Min(u0, p.x); u1 = Mathf.Max(u1, p.x); v0 = Mathf.Min(v0, p.y); v1 = Mathf.Max(v1, p.y); }
        const float pitch = 1.2f;
        for (float u = u0 + pitch * .5f; u < u1; u += pitch)
        for (float v = v0 + pitch * .5f; v < v1; v += pitch)
        {
            if (!InPoly(poly, u - .6f, v - .6f) || !InPoly(poly, u + .6f, v + .6f) || !InPoly(poly, u - .6f, v + .6f) || !InPoly(poly, u + .6f, v - .6f)) continue;
            int iu = Mathf.RoundToInt((u - u0) / pitch), iv = Mathf.RoundToInt((v - v0) / pitch);
            if (iu % 4 == 1 && iv % 4 == 1)
            {
                B("Majibang_2F_CeilingLED", led).Box(P(u, v, y - .02f), new Vector3(.94f, .03f, .94f));
                // Recessed downlight: the 2023 walk (RY2qvE0Tugk 1:36-2:19) shows this strip lit only by ceiling panels.
                var lamp = new GameObject("Majibang_2F_Downlight").AddComponent<Light>();
                lamp.transform.SetParent(frame, false);
                lamp.transform.localPosition = P(u, v, y - .08f);
                lamp.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                lamp.type = LightType.Spot; lamp.spotAngle = 120f; lamp.innerSpotAngle = 60f;
                lamp.range = 7f; lamp.intensity = 3.2f; lamp.color = new Color(1f, .96f, .9f);
                lamp.shadows = LightShadows.None; lamp.renderMode = LightRenderMode.ForcePixel;
                downlights++;
            }
            else B("Majibang_2F_CeilingPanels", ceilingPanel).Box(P(u, v, y + .045f), new Vector3(1.185f, .09f, 1.185f));
        }
    }

    // ---------------- units ----------------
    static void Unit(JObject j, float floorDefault)
    {
        bool enter = (string)j["frame"] == "box" && !inBox;
        if (enter) EnterBox();
        string name = (string)j["name"], kind = (string)j["kind"], face = (string)j["face"];
        unitFasciaTex = (string)j["fasciaTex"];
        unitDoorW = j["doorW"] != null ? (float)j["doorW"] : 2.0f;
        unitOpenFront = (string)j["frontage"] == "open";
        unitFixtures = (string)j["fixtures"] != "unresolved";
        Transform previousFrame = frame;
        string previousPrefix = batchPrefix, owner = (string)j["batchOwner"];
        if (!string.IsNullOrEmpty(owner))
        {
            frame = new GameObject(owner).transform; frame.SetParent(previousFrame, false);
            batchPrefix += owner + "_";
        }
        float u0 = (float)j["u"][0], u1 = (float)j["u"][1];
        float v0 = (float)j["v"][0];
        float v1 = j["v"][1].Type == JTokenType.Null ? EastV(u0) - .25f : (float)j["v"][1];
        float cu = (u0 + u1) * .5f, cv = (v0 + v1) * .5f, fu, fv, width, depth;
        float floor = j["floor"] != null ? (float)j["floor"] : floorDefault;
        if (face == "-v") { fu = 0; fv = -1; width = u1 - u0; depth = v1 - v0; }
        else if (face == "+v") { fu = 0; fv = 1; width = u1 - u0; depth = v1 - v0; }
        else if (face == "+u") { fu = 1; fv = 0; width = v1 - v0; depth = u1 - u0; }
        else if (face == "-u") { fu = -1; fv = 0; width = v1 - v0; depth = u1 - u0; }
        else throw new InvalidOperationException("Unsupported unit face " + face + " for " + name);
        Color fascia = j["fascia"] is JArray fa ? new Color((float)fa[0], (float)fa[1], (float)fa[2]) : new Color(.2f, .22f, .24f);
        if (kind == "service") ServiceRoom(name.Trim(), cu, cv, width, depth, fu, fv, floor);
        else if (kind == "foodcourt") FoodCourt(name, u0, u1, v0, v1, floor, (JArray)j["stalls"], fascia);
        else if (kind == "tickets") TicketRow(name.Trim(), cu, cv, width, depth, fu, fv, floor);
        else if (kind == "machines") Machines(name.Trim(), cu, cv, width, depth, fu, fv, floor);
        else Shop(name.Trim(), cu, cv, width, depth, fu, fv, kind == "restaurant" ? "counter" : "shelf", fascia, floor);
        receipts.Add(new { region = name.Trim(), kind, frame = inBox ? "box" : "station", u = new[] { u0, u1 }, v = new[] { v0, v1 }, floorY = floor, evidence = (string)j["evidence"] });
        frame = previousFrame; batchPrefix = previousPrefix;
        if (enter) ExitBox();
    }

    static void PhotoFascia(Vector3 front, float width, Vector3 normal, string texture)
    {
        Material material = TexMat(texture);
        TextureImporter importer = AssetImporter.GetAtPath(texture) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Fascia source importer missing: " + texture);
        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
        if (sourceWidth <= 0 || sourceHeight <= 0) throw new InvalidOperationException("Fascia source dimensions invalid: " + texture);
        float height = Mathf.Min(.62f, width * .92f * sourceHeight / sourceWidth);
        float span = height * sourceWidth / sourceHeight;
        Vector3 bottom = front + normal * .06f + Vector3.up * (3.25f - height * .5f);
        Vector3 right = Vector3.Cross(Vector3.up, -normal).normalized;
        Batch face = B("Majibang_Fascia_" + Path.GetFileNameWithoutExtension(texture), material);
        face.TexQuad(bottom - right * span * .5f, bottom + right * span * .5f,
            bottom + right * span * .5f + Vector3.up * height,
            bottom - right * span * .5f + Vector3.up * height, normal);
    }

    static void Shop(string name, float u, float v, float width, float depth, float faceU, float faceV, string fixture, Color fascia, float floor)
    {
        Vector3 normal = D(faceU, faceV), right = Vector3.Cross(Vector3.up, normal).normalized;
        Vector3 center = P(u, v, floor), front = center + normal * depth * .5f;
        string safe = "Shop_" + name.Replace('\n', '_').Replace('/', '_').Replace(' ', '_');
        ShopGeometry(name, center, width, depth, normal, unitOpenFront);
        // Kit storefronts own the fascia; otherwise brand face from a photo crop, lettering as the fallback.
        if (!kitStorefronts && !string.IsNullOrEmpty(unitFasciaTex)) PhotoFascia(front, width, normal, unitFasciaTex);
        else if (!kitStorefronts)
        {
            Vector3 face = front + normal * .06f;
            Sign(name, face.z, face.x, floor + 3.25f, Mathf.Min(width - .2f, 7.5f), .55f, faceU, faceV,
                Colour(safe + "_Fascia", fascia), Color.white);
        }
        var fill = new GameObject(name + " fill light").AddComponent<Light>();
        fill.transform.SetParent(frame, false); fill.transform.localPosition = center + Vector3.up * 3.1f;
        fill.type = LightType.Point; fill.range = Mathf.Max(width, depth) * .9f + 2f; fill.intensity = 1.3f; fill.color = new Color(1f, .96f, .9f);
        fill.shadows = LightShadows.None; fill.renderMode = LightRenderMode.ForcePixel;
        // Do not substitute generic grocery gondolas for photographed flower displays or refrigerators.
        if (!unitFixtures) return;
        ShopFixtures(name, center, front, width, depth, normal, right, fixture, floor);
    }

    // Pure mesh accumulation, shared by construction and the exact-triangle legacy ownership preflight.
    static void ShopGeometry(string name, Vector3 center, float width, float depth, Vector3 normal, bool openFront)
    {
        Vector3 right = Vector3.Cross(Vector3.up, normal).normalized, front = center + normal * depth * .5f;
        string safe = "Shop_" + name.Replace('\n', '_').Replace('/', '_').Replace(' ', '_');
        Batch walls = B(safe + "_Walls", plaster, true);
        Vector3 a = front - right * width * .5f, b = front + right * width * .5f, c = b - normal * depth, d = a - normal * depth;
        Wall(walls, a, d, 3.45f); Wall(walls, d, c, 3.45f); Wall(walls, c, b, 3.45f);
        if (!kitStorefronts)
        {
            Batch frames = B("Majibang_ShopFrames", steel);
            frames.Tube(a, a + Vector3.up * 3.5f, .055f, 8); frames.Tube(b, b + Vector3.up * 3.5f, .055f, 8);
            frames.Tube(a + Vector3.up * 2.85f, b + Vector3.up * 2.85f, .05f, 8);
            if (!openFront)
            {
                float door = Mathf.Min(unitDoorW, width * .6f);
                Batch glazing = B("Majibang_ShopFrontGlass", glass, true);
                Wall(glazing, a, front - right * door * .5f, 2.85f); Wall(glazing, front + right * door * .5f, b, 2.85f);
                frames.Tube(front - right * door * .5f, front - right * door * .5f + Vector3.up * 2.85f, .045f, 8);
                frames.Tube(front + right * door * .5f, front + right * door * .5f + Vector3.up * 2.85f, .045f, 8);
            }
            // Dark fascia band over the front; brand face from a photo crop when one exists, lettering otherwise.
            Vector3 fo = normal * .04f;
            Batch band = B("Majibang_ShopFascia", dark);
            Vector3 f0 = a + fo + Vector3.up * 2.9f, f1 = b + fo + Vector3.up * 2.9f, f2 = b + fo + Vector3.up * 3.6f, f3 = a + fo + Vector3.up * 3.6f;
            if (Vector3.Dot(Vector3.Cross(f1 - f0, f2 - f0), normal) > 0) band.Quad(f0, f1, f2, f3); else band.Quad(f0, f3, f2, f1);
        }
        // Bright interior ceiling and one fill light (storefronts read lit from the hall in every photo).
        Vector3 i0 = a + Vector3.up * 3.44f, i1 = b + Vector3.up * 3.44f, i2 = c + Vector3.up * 3.44f, i3 = d + Vector3.up * 3.44f;
        Batch ceil = B("Majibang_ShopCeiling", ceilingPanel);
        if (Vector3.Dot(Vector3.Cross(i1 - i0, i2 - i0), Vector3.down) > 0) ceil.Quad(i0, i1, i2, i3); else ceil.Quad(i0, i3, i2, i1);
        for (int i = -1; i <= 1; i++) B("Majibang_ShopLED", led).Box(center + right * i * width * .25f + Vector3.up * 3.5f, new Vector3(.58f, .035f, .58f));
    }

    static void ShopFixtures(string name, Vector3 center, Vector3 front, float width, float depth, Vector3 normal, Vector3 right, string fixture, float floor)
    {
        float yaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
        int count = Mathf.Clamp(Mathf.RoundToInt(width / 3f), 2, 6);
        for (int i = 0; i < count; i++)
        {
            float t = (i + .5f) / count - .5f;
            Vector3 p = center + right * (width * .8f * t) - normal * (depth * .3f);
            Prop(fixture, name + " fixture", p.z, p.x, floor, new Vector3(Mathf.Min(2.4f, width * .8f / count), fixture == "shelf" ? 1.75f : 1.05f, .78f), yaw);
            if (fixture == "counter") Prop("food", name + " display", p.z, p.x, floor + 1.06f, new Vector3(.7f, .22f, .45f), yaw);
        }
        // Customer seating inside restaurants: tables toward the front half of the unit.
        if (fixture == "counter" && depth > 7)
            for (float s = -width * .5f + 2f; s <= width * .5f - 2f; s += 3.2f)
            for (float r = 2.2f; r < depth * .55f; r += 3f)
            {
                Vector3 p = front + right * s - normal * r;
                Prop("table", name + " table", p.z, p.x, floor, new Vector3(1.0f, .76f, 1.0f), yaw);
                Vector3 l = p - right * .85f, rr = p + right * .85f;
                Prop("chair", name + " chair", l.z, l.x, floor, new Vector3(.46f, .86f, .5f), yaw + 90);
                Prop("chair", name + " chair", rr.z, rr.x, floor, new Vector3(.46f, .86f, .5f), yaw - 90);
            }
    }

    static void FoodCourt(string name, float u0, float u1, float v0, float v1, float floor, JArray stalls, Color fascia)
    {
        // Corridor-side glass front with two entrances; solid end walls; the original east wall carries the windows.
        float[] gaps = { u0 + 4, u0 + 7, u1 - 7, u1 - 4 };
        float[] run = { u0, gaps[0], gaps[1], gaps[2], gaps[3], u1 };
        for (int i = 0; i < run.Length; i += 2) Facade(P(run[i], v0, floor), P(run[i + 1], v0, floor), 3.6f, "Majibang_FoodCourtFront");
        Batch walls = B("Majibang_FoodCourt_Walls", plaster, true);
        Wall(walls, P(u0, v0, floor), P(u0, EastV(u0) - .25f, floor), 4.6f);
        Wall(walls, P(u1, EastV(u1) - .25f, floor), P(u1, v0, floor), 4.6f);
        Material signMat = Colour("Shop_FoodCourt_Fascia", fascia);
        foreach (float gm in new[] { (gaps[0] + gaps[1]) * .5f, (gaps[2] + gaps[3]) * .5f })
            Sign(name, gm, v0 - .06f, floor + 3.3f, 5.2f, .7f, 0, -1, signMat, Color.white);
        // Brand stalls along the inside of the front, facing the window-side seating (+v).
        float stallW = (u1 - u0 - 14) / stalls.Count;
        for (int i = 0; i < stalls.Count; i++)
        {
            float su = u0 + 7 + stallW * (i + .5f), sv = v0 + 2.2f;
            string brand = (string)stalls[i];
            Box("Majibang_FoodCourt_StallBacks", wood, su, v0 + .6f, floor + 1.4f, stallW - .4f, .12f, 2.8f, true);
            Prop("counter", brand + " counter", su - stallW * .2f, sv, floor, new Vector3(Mathf.Min(2.6f, stallW * .38f), 1.05f, .8f), 180);
            Prop("counter", brand + " counter", su + stallW * .2f, sv, floor, new Vector3(Mathf.Min(2.6f, stallW * .38f), 1.05f, .8f), 180);
            Prop("food", brand + " display", su, sv, floor + 1.06f, new Vector3(.7f, .22f, .45f), 180);
            Sign(brand, su, v0 + .75f, floor + 2.55f, Mathf.Min(stallW - .6f, 5.5f), .55f, 0, 1, Colour("Shop_FoodCourt_Stall" + i, new Color(.08f, .08f, .09f)), new Color(1f, .93f, .78f));
        }
        // Window-side seating grid.
        int tables = 0;
        for (float u = u0 + 2.5f; u <= u1 - 2.5f; u += 3.4f)
        for (float v = v0 + 6.5f; v <= EastV(u) - 2.2f; v += 3.1f)
        {
            Prop("table", "푸드코트 table", u, v, floor, new Vector3(1.0f, .76f, 1.0f), 0);
            Prop("chair", "푸드코트 chair", u - .85f, v, floor, new Vector3(.46f, .86f, .5f), 90);
            Prop("chair", "푸드코트 chair", u + .85f, v, floor, new Vector3(.46f, .86f, .5f), -90);
            tables++;
        }
        // Dark ceiling with pendant bulbs seen in the 2023-2026 food-court photos.
        Batch ceil = B("Majibang_FoodCourt_Ceiling", dark);
        Vector3 c0 = P(u0, v0, floor + 4.6f), c1 = P(u1, v0, floor + 4.6f), c2 = P(u1, EastV(u1) - .25f, floor + 4.6f), c3 = P(u0, EastV(u0) - .25f, floor + 4.6f);
        ceil.Quad(c3, c2, c1, c0);
        for (float u = u0 + 1.5f; u < u1; u += 3f)
        for (float v = v0 + 1.5f; v < EastV(u) - .5f; v += 3f)
            B("Majibang_FoodCourt_Bulbs", led).Tube(P(u, v, floor + 4.6f), P(u, v, floor + 4.1f), .07f, 8);
        receipts.Add(new { region = "푸드코트 seating", tables });
    }

    static void TicketRow(string name, float u, float v, float width, float depth, float nu, float nv, float floor)
    {
        Vector3 normal = D(nu, nv), right = Vector3.Cross(Vector3.up, normal), center = P(u, v, floor);
        Vector3 back = center - normal * depth * .5f;
        Wall(B("Majibang_ServiceWalls", plaster, true), back - right * width * .5f, back + right * width * .5f, 3.6f);
        float yaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg + 180;
        int windows = Mathf.Max(2, Mathf.FloorToInt(width / 2.2f));
        for (int i = 0; i < windows; i++)
        {
            Vector3 p = center + right * (width * ((i + .5f) / windows - .5f)) + normal * (depth * .5f - .6f);
            Prop("counter", name + " window " + i, p.z, p.x, floor, new Vector3(1.8f, 1.12f, .9f), yaw);
        }
        Vector3 s = center + normal * (depth * .5f - .1f);
        Sign(name + "  Tickets", s.z, s.x, floor + 3.0f, Mathf.Min(width - .5f, 6f), .6f, nu, nv, blue, Color.white);
    }

    static void Machines(string name, float u, float v, float width, float depth, float nu, float nv, float floor)
    {
        Vector3 normal = D(nu, nv), right = Vector3.Cross(Vector3.up, normal), center = P(u, v, floor);
        float yaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg + 180;
        int n = Mathf.Max(2, Mathf.FloorToInt(width / 1.1f));
        for (int i = 0; i < n; i++)
        {
            Vector3 p = center + right * (width * ((i + .5f) / n - .5f));
            Prop("ticket", name + " " + i, p.z, p.x, floor, new Vector3(.8f, 1.8f, .6f), yaw);
        }
        Vector3 s = center + normal * .4f;
        Sign(name, s.z, s.x, floor + 2.6f, Mathf.Min(width - .2f, 3.5f), .45f, nu, nv, blue, Color.white);
    }

    static void SecondFloorExtras(JObject x, float floor)
    {
        JObject info = (JObject)x["info"];
        bool infoBox = (string)info["frame"] == "box";
        if (infoBox) EnterBox();
        float iu = (float)info["u"], iv = (float)info["v"], r = (float)info["radius"];
        // KORAIL 철도이용안내 desk (photo tom2410_008): black body, white counter band, dark canopy ring around the tree column.
        // info.kit = true skips the builder desk (a kit spec models it). info.customerSide [du,dv] (G-B09/G-B10/BL-12): lettering
        // directly on the canopy (no fin boards), one lightbox with a closed housing on the customer side, KORAIL plaque, counter
        // screens and monitors. Absent: the original symmetric ±u sign pairs.
        Material white = Reuse("Tube_White");
        bool deskKit = info["kit"] != null && info["kit"].Type == JTokenType.Boolean && (bool)info["kit"];
        if (!deskKit)
        {
            Arc("Majibang_Info_Body", dark, iu, iv, floor, floor + .95f, r - .55f, r, 30, 330, true);
            Arc("Majibang_Info_Band", white, iu, iv, floor + .95f, floor + 1.12f, r - .6f, r + .04f, 30, 330, true);
            Arc("Majibang_Info_Canopy", dark, iu, iv, floor + 3.3f, floor + 4.0f, r - .3f, r + .45f, 0, 360, false);
        }
        if (deskKit) { }
        else if (info["customerSide"] is JArray cs)
        {
            float theta = Mathf.Atan2((float)cs[1], (float)cs[0]);
            // The body arc spans 30..330 deg: the staff opening faces +u; the customer face must be on the closed body.
            if (Mathf.Abs(Mathf.DeltaAngle(0, theta * Mathf.Rad2Deg)) < 40) throw new InvalidOperationException("info.customerSide points into the staff opening (+u +-30 deg).");
            InfoDeskFaces(iu, iv, r, floor, theta);
        }
        else
            foreach (float side in new[] { 1f, -1f })
            {
                Sign("KORAIL 부산역", iu + side * (r + .47f), iv, floor + 3.65f, 2.8f, .5f, side, 0, dark, Color.white);
                Sign("철도이용안내\nBusan Station Information", iu + side * (r - .5f), iv, floor + 2.75f, 2.4f, .7f, side, 0, blue, Color.white);
            }
        receipts.Add(new { region = "KORAIL 철도이용안내", frame = infoBox ? "box" : "station", centre = new[] { iu, iv }, desk = deskKit ? "kit" : info["customerSide"] != null ? "customer-side" : "builder", evidence = (string)info["evidence"] });
        if (infoBox) ExitBox();
        foreach (JObject s in (JArray)x["signs"])
            Sign((string)s["text"], (float)s["u"], (float)s["v"], (float)s["y"], (float)s["w"], (float)s["h"], (float)s["fu"], (float)s["fv"], blue, Color.white);
        int benches = 0;
        JObject seat = (JObject)x["seating"];
        if (seat != null)
        {
            bool seatBox = (string)seat["frame"] == "box";
            benchDecalText = seat["decalText"] == null || seat["decalText"].Type != JTokenType.Boolean || (bool)seat["decalText"];
            if (seatBox) EnterBox();
            float sa0 = (float)seat["a"][0], sa1 = (float)seat["a"][1], sb0 = (float)seat["b"][0], sb1 = (float)seat["b"][1];
            float pitch = (float)seat["pitchA"], len = (float)seat["len"], gap = (float)seat["gap"];
            var avoid = new List<Vector3>();
            foreach (JArray av in (JArray)seat["avoid"]) avoid.Add(new Vector3((float)av[0], (float)av[1], (float)av[2]));
            for (float a = sa0; a <= sa1; a += pitch)
            for (float b = sb0 + len * .5f; b <= sb1 - len * .5f; b += len + gap)
            {
                bool blocked = false;
                foreach (Vector3 av in avoid) if (Mathf.Abs(a - av.x) < av.z + .4f && Mathf.Abs(b - av.y) < av.z + len * .5f) blocked = true;
                if (blocked) continue;
                KorailBench(a, b, floor, len); benches++;
            }
            if (seatBox) ExitBox();
        }
        Material green = AssetDatabase.LoadAssetAtPath<Material>(Mats + "Display_Green.mat");
        if (green == null) throw new InvalidOperationException("Reused departure-board material missing.");
        foreach (JArray bd in (JArray)x["boards"]["items"])
            Sign("열차 출발 안내   KTX · SRT · ITX · 무궁화", (float)bd[0], (float)bd[1], (float)bd[2], 5.2f, .9f, (float)bd[3], (float)bd[4], green, new Color(1f, .75f, .15f));
        receipts.Add(new { region = "대합실 좌석", benches, seating = x["seating"], evidence = (string)x["seating"]?["evidence"] });
    }

    // KORAIL waiting bench (photo unna.kr-58_005, 2010): 2.0 x 0.55 m wooden slab on two brushed-steel box legs, seat 0.45 m.
    // G-B11 / BL-27: one wood material with metric UVs on every face, stainless U grab bars lying on the seat between places,
    // KORAIL decals on the outer leg faces.
    // seating.decalText (default true): KORAIL lettering on the leg decals (one world-text object per bench end).
    static bool benchDecalText = true;
    static void KorailBench(float a, float b, float floor, float len)
    {
        Batch top = B("Majibang_Bench_Wood", wood, true), legs = B("Majibang_Bench_Legs", steel), grab = B("Majibang_Bench_Grab", steel);
        Vector3 along = KD(new Vector2(0, 1)), across = KD(new Vector2(1, 0)), up = Vector3.up;
        KBox(top, P(a, b, floor + .4f), along, up, across, new Vector3(len, .1f, .55f));
        foreach (float s in new[] { -1f, 1f })
            legs.Box(P(a, b + s * (len * .5f - .3f), floor + .175f), new Vector3(.09f, .35f, .5f));
        int seats = Mathf.Max(1, Mathf.RoundToInt(len / .65f));
        float yb = floor + .47f;
        for (int k = 1; k < seats; k++)
        {
            float bk = b + ((float)k / seats - .5f) * len;
            var path = new List<Vector3> { P(a + .25f, bk - .06f, yb), P(a - .25f, bk - .06f, yb), P(a - .25f, bk + .06f, yb), P(a + .25f, bk + .06f, yb) };
            KTubePath(grab, path, .015f, 8);
            foreach (float e in new[] { -.06f, .06f }) KTube(grab, P(a + .2f, bk + e, floor + .45f), P(a + .2f, bk + e, yb), .01f, 6);
        }
        Material decal = KM("#004B87");
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 n = KD(new Vector2(0, s)), c = P(a, b + s * (len * .5f - .3f + .047f), floor + .26f);
            KRectC(B("Majibang_Bench_Decal", decal), c, Vector3.Cross(up, -n), up, .34f, .075f, n);
            if (benchDecalText) KText("KORAIL", c + n * .002f, n, .32f, .07f, Color.white);
        }
    }

    // Customer-side desk faces (angle theta in the desk frame's (u,v) plane): canopy lettering front and back, one housed lightbox,
    // KORAIL + "K" plaque on the body, three acrylic counter screens with staff monitors behind them.
    static void InfoDeskFaces(float iu, float iv, float r, float floor, float theta)
    {
        Vector3 up = Vector3.up;
        Func<float, float, float, Vector3> at = (ang, rad, h) => P(iu + Mathf.Cos(ang) * rad, iv + Mathf.Sin(ang) * rad, floor + h);
        Func<float, Vector3> radial = ang => D(Mathf.Cos(ang), Mathf.Sin(ang));
        foreach (float t in new[] { theta, theta + Mathf.PI })
            KText("KORAIL 부산역", at(t, r + .455f, 3.65f), radial(t), 2.6f, .5f, Color.white);
        Vector3 n = radial(theta), right = Vector3.Cross(up, -n), lc = at(theta, r - .5f, 2.75f);
        KBox(B("Majibang_Info_LightboxHousing", dark), lc - n * .07f, right, up, n, new Vector3(2.5f, .8f, .12f));
        Vector3 face = lc + n * .004f;
        KRectC(B("SignBoards_" + blue.name, blue), face, right, up, 2.4f, .7f, n);
        KText("철도이용안내\nBusan Station Information", face + n * .002f, n, 2.4f, .7f, Color.white);
        // Plaque and logo on the body (body outer radius r, top at .95).
        Vector3 pc = at(theta - .2f / r, r + .006f, .55f), pn = radial(theta - .2f / r);
        KRectC(B("SignBoards_" + blue.name, blue), pc, Vector3.Cross(up, -pn), up, .3f, .3f, pn);
        KText("K", pc + pn * .002f, pn, .26f, .28f, Color.white);
        float lt = theta + .45f / r; Vector3 ln = radial(lt);
        KText("KORAIL", at(lt, r + .008f, .55f), ln, .8f, .2f, Color.white);
        Material gm = KGlassMat("clear");
        foreach (float dk in new[] { -20f, 0f, 20f })
        {
            float t = theta + dk * Mathf.Deg2Rad; Vector3 tn = radial(t), tr = Vector3.Cross(up, -tn);
            KGlass(B("Majibang_Info_Screens", gm), at(t, r - .12f, 1.12f) - tr * .4f, tr, up, .8f, .45f, tn);
            KBox(B("Majibang_Info_Monitors", dark), at(t, r - .45f, 1.45f), tr, up, tn, new Vector3(.46f, .3f, .04f));
            KBox(B("Majibang_Info_Monitors", dark), at(t, r - .45f, 1.17f), tr, up, tn, new Vector3(.06f, .1f, .06f), false);
        }
    }

    static void Arc(string key, Material mat, float u, float v, float bottom, float top, float inner, float outer, float start, float end, bool collision)
    {
        Batch b = B(key, mat, collision);
        int pieces = Mathf.CeilToInt((end - start) / 7.5f);
        Vector3 h = Vector3.up * (top - bottom);
        for (int i = 0; i < pieces; i++)
        {
            float a = Mathf.Lerp(start, end, (float)i / pieces) * Mathf.Deg2Rad, c = Mathf.Lerp(start, end, (float)(i + 1) / pieces) * Mathf.Deg2Rad;
            Vector3 ai = P(u + Mathf.Cos(a) * inner, v + Mathf.Sin(a) * inner, bottom), ao = P(u + Mathf.Cos(a) * outer, v + Mathf.Sin(a) * outer, bottom);
            Vector3 ci = P(u + Mathf.Cos(c) * inner, v + Mathf.Sin(c) * inner, bottom), co = P(u + Mathf.Cos(c) * outer, v + Mathf.Sin(c) * outer, bottom);
            b.Quad(ai + h, ao + h, co + h, ci + h); b.Quad(ci, co, ao, ai);
            b.Quad(ao, co, co + h, ao + h); if (inner > .01f) b.Quad(ci, ai, ai + h, ci + h);
            if (i == 0) b.Quad(ai, ao, ao + h, ai + h);
            if (i == pieces - 1) b.Quad(co, ci, ci + h, co + h);
        }
    }

    static void ServiceRoom(string name, float u, float v, float width, float depth, float nu, float nv, float floor)
    {
        Vector3 normal = D(nu, nv), right = Vector3.Cross(Vector3.up, normal), center = P(u, v, floor);
        Vector3 f = center + normal * depth * .5f, a = f - right * width * .5f, b = f + right * width * .5f;
        Batch wall = B("Majibang_ServiceWalls", plaster, true);
        Wall(wall, a, a - normal * depth, 4.1f); Wall(wall, a - normal * depth, b - normal * depth, 4.1f); Wall(wall, b - normal * depth, b, 4.1f);
        float door = Mathf.Min(1.8f, width * .5f);
        Wall(wall, a, f - right * door * .5f, 4.1f); Wall(wall, f + right * door * .5f, b, 4.1f);
        Vector3 l = f - right * door * .5f, r = f + right * door * .5f;
        wall.Quad(l + Vector3.up * 2.35f, r + Vector3.up * 2.35f, r + Vector3.up * 4.1f, l + Vector3.up * 4.1f);
        Sign(name, f.z, f.x, floor + 2.75f, Mathf.Min(width - .4f, 3.2f), .5f, nu, nv, blue, Color.white);
    }

    // ---------------- helpers (VideoFloorTwo conventions) ----------------
    static Material Reuse(string name)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(Mats + name + ".mat");
        if (m == null) throw new InvalidOperationException("Reused material missing: " + name);
        return m;
    }

    static Material Colour(string name, Color tint, Shader shader = null)
    {
        string path = Art + "/" + name.Replace(' ', '_') + ".mat";
        Material fresh = new Material(shader != null ? shader : lit) { name = name };
        fresh.SetColor("_BaseColor", tint); fresh.SetFloat("_Smoothness", .4f);
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null) { AssetDatabase.CreateAsset(fresh, path); return fresh; }
        EditorUtility.CopySerialized(fresh, existing); UnityEngine.Object.DestroyImmediate(fresh); EditorUtility.SetDirty(existing);
        return existing;
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

    static Batch B(string name, Material mat, bool collision = false)
    {
        // Batches are per frame: the same logical batch in the box frame is a separate mesh under the box transform.
        string key = batchPrefix + (inBox ? "Box_" + name : name);
        if (!batches.TryGetValue(key, out Batch batch)) { batch = new Batch(key, mat, collision, frame); batches.Add(key, batch); }
        return batch;
    }
    static void Box(string name, Material mat, float u, float v, float y, float du, float dv, float dy, bool collision = false)
    { B(name, mat, collision).Box(P(u, v, y), new Vector3(dv, dy, du)); }

    static void Wall(Batch b, Vector3 a, Vector3 c, float height)
    {
        Vector3 h = Vector3.up * height;
        b.Quad(a, c, c + h, a + h); b.Quad(a + h, c + h, c, a);
    }

    static void Facade(Vector3 a, Vector3 b, float height, string key)
    {
        if (Vector3.Distance(a, b) < .05f) return;
        Vector3 up = Vector3.up * height;
        Batch g = B(key + "_Glass", glass, true); g.Quad(a, b, b + up, a + up); g.Quad(a + up, b + up, b, a);
        Batch f = B(key + "_Frame", steel);
        f.Tube(a, b, .05f, 8); f.Tube(a + up, b + up, .06f, 8);
        int bays = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2.4f));
        for (int k = 0; k <= bays; k++) { Vector3 c = Vector3.Lerp(a, b, (float)k / bays); f.Tube(c, c + up, .045f, 8); }
    }

    static void Sign(string text, float u, float v, float y, float width, float height, float faceU, float faceV, Material board, Color ink)
    {
        Vector3 n = D(faceU, faceV), right = Vector3.Cross(Vector3.up, n), c = P(u, v, y);
        Batch b = B("SignBoards_" + board.name, board);
        Vector3 a = c - right * width * .5f - Vector3.up * height * .5f, d = c - right * width * .5f + Vector3.up * height * .5f;
        b.Quad(a, a + right * width, d + right * width, d); b.Quad(d, d + right * width, a + right * width, a);
        SignText(text, c, n, width, height, ink);
    }

    // Depth-tested world text (rebound by BindWorldText) fitted inside width x height with the given margins, facing n.
    static TextMesh SignText(string text, Vector3 c, Vector3 n, float width, float height, Color ink, float padW = .22f, float padH = .12f)
    {
        GameObject go = new GameObject("Sign · " + text.Replace('\n', ' ')); go.transform.SetParent(frame, false);
        go.transform.localPosition = c + n * .015f; go.transform.localRotation = Quaternion.LookRotation(-n, Vector3.up);
        TextMesh t = go.AddComponent<TextMesh>(); t.font = font; t.text = text; t.fontSize = 80; t.characterSize = .11f;
        t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = ink; t.richText = false;
        MeshRenderer r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = font.material; r.shadowCastingMode = ShadowCastingMode.Off;
        font.RequestCharactersInTexture(text, 80, FontStyle.Normal);
        Vector3 natural = r.localBounds.size;
        if (natural.x <= 0 || natural.y <= 0) throw new InvalidOperationException("Korean font produced empty sign geometry: " + text);
        go.transform.localScale = Vector3.one * Mathf.Min((width - padW) / natural.x, (height - padH) / natural.y);
        return t;
    }

    static void Prop(string key, string name, float u, float v, float y, Vector3 target, float yaw)
    {
        Transform holder = new GameObject(name).transform; holder.SetParent(frame, false);
        Transform fit = new GameObject("fit").transform; fit.SetParent(holder, false);
        GameObject obj = UnityEngine.Object.Instantiate(models[key]); obj.transform.SetParent(fit, false);
        obj.transform.localPosition = Vector3.zero; obj.transform.localRotation = models[key].transform.rotation; obj.transform.localScale = Vector3.one;
        foreach (Collider c in obj.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (Camera c in obj.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach (Light l in obj.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l);
        Bounds b = LocalBounds(obj, holder);
        if (b.size.x < .00001f || b.size.y < .00001f || b.size.z < .00001f) throw new InvalidOperationException("Acquired prop bounds invalid: " + key);
        fit.localScale = new Vector3(target.x / b.size.x, target.y / b.size.y, target.z / b.size.z);
        b = LocalBounds(obj, holder); fit.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        holder.localPosition = P(u, v, y); holder.localRotation = Quaternion.Euler(0, yaw, 0);
        BoxCollider collider = holder.gameObject.AddComponent<BoxCollider>(); collider.center = Vector3.up * target.y * .5f; collider.size = target;
        ConvertPropMaterials(obj);
        propCount++;
    }

    static Bounds LocalBounds(GameObject obj, Transform relative)
    {
        Bounds bound = new Bounds(); bool any = false;
        foreach (MeshFilter f in obj.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            Bounds b = f.sharedMesh.bounds; Matrix4x4 m = relative.worldToLocalMatrix * f.transform.localToWorldMatrix;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                Vector3 p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                if (!any) { bound = new Bounds(p, Vector3.zero); any = true; } else bound.Encapsulate(p);
            }
        }
        if (!any) throw new InvalidOperationException("Acquired model has no mesh bounds: " + obj.name);
        return bound;
    }

    static void ConvertPropMaterials(GameObject go)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material original = materials[i]; if (original == null) { materials[i] = steel; continue; }
                if (original.shader != null && original.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)) continue;
                if (!propMaterials.TryGetValue(original, out Material mapped))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long local);
                    Material existing = AssetDatabase.LoadAssetAtPath<Material>(Mats + "Reused_" + guid + "_" + local + ".mat");
                    if (existing == null)
                    {
                        Material fresh = new Material(lit) { name = original.name };
                        fresh.SetColor("_BaseColor", original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
                        Texture tex = original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : null;
                        if (tex != null) fresh.SetTexture("_BaseMap", tex);
                        string path = Art + "/Reused_" + guid + "_" + local + ".mat";
                        existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (existing == null) { AssetDatabase.CreateAsset(fresh, path); existing = fresh; }
                    }
                    mapped = existing; propMaterials.Add(original, mapped);
                }
                materials[i] = mapped;
            }
            r.sharedMaterials = materials;
        }
    }

    sealed class Batch
    {
        readonly string name; readonly Material mat; readonly bool collision; readonly Transform parent;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        // Kit: explicit per-vertex normals (smooth cylinders); every other vertex keeps its recalculated flat normal.
        readonly Dictionary<int, Vector3> smooth = new Dictionary<int, Vector3>();
        // Kit: collider-only mesh (ramps) - no MeshFilter/MeshRenderer.
        public bool renderless;
        public Batch(string name, Material mat, bool collision, Transform parent) { this.name = name; this.mat = mat; this.collision = collision; this.parent = parent; }

        public object Describe()
        {
            return new { name, collision, vertexCount = vertices.Count, triangleCount = triangles.Count / 3,
                verticesUVY = vertices.Select(p => new[] { p.z, p.x, p.y }).ToArray(), triangleIndices = triangles.ToArray() };
        }

        // Match orientation, position and UV; abort on missing/ambiguous ownership before any scene writes.
        public int[] RemoveOwnedTriangles(Mesh mesh)
        {
            if (mesh.subMeshCount != 1) throw new InvalidOperationException("Unexpected legacy submeshes: " + name);
            Vector3[] actual = mesh.vertices; Vector2[] actualUv = mesh.uv; int[] indices = mesh.triangles;
            if (actualUv.Length != actual.Length) throw new InvalidOperationException("Legacy UV data missing: " + name);
            bool[] remove = new bool[indices.Length / 3];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int match = -1;
                for (int j = 0; j < indices.Length; j += 3)
                {
                    if (remove[j / 3]) continue;
                    bool equal = false;
                    for (int offset = 0; offset < 3 && !equal; offset++)
                    {
                        equal = true;
                        for (int k = 0; k < 3; k++)
                        {
                            int expectedIndex = triangles[i + k], actualIndex = indices[j + (k + offset) % 3];
                            if ((vertices[expectedIndex] - actual[actualIndex]).sqrMagnitude > .00000001f ||
                                (uv[expectedIndex] - actualUv[actualIndex]).sqrMagnitude > .00000001f) { equal = false; break; }
                        }
                    }
                    if (!equal) continue;
                    if (match >= 0) throw new InvalidOperationException("Ambiguous legacy triangle ownership: " + name + " triangle " + i / 3);
                    match = j / 3;
                }
                if (match < 0) throw new InvalidOperationException("Edited/missing legacy triangle: " + name + " triangle " + i / 3);
                remove[match] = true;
            }
            var kept = new List<int>(indices.Length - triangles.Count);
            for (int i = 0; i < indices.Length; i += 3)
                if (!remove[i / 3]) { kept.Add(indices[i]); kept.Add(indices[i + 1]); kept.Add(indices[i + 2]); }
            return kept.ToArray();
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            uv.Add(new Vector2(a.x, a.z)); uv.Add(new Vector2(b.x, b.z)); uv.Add(new Vector2(c.x, c.z));
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }

        // Quad with 0..1 texture coordinates: a bottom-left, b bottom-right, c top-right, d top-left; faces n.
        public void TexQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) >= 0) { triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3); }
            else { triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2); }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = vertices.Count; Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            if (Mathf.Abs(normal.y) > .7f)
                foreach (Vector3 p in new[] { a, b, c, d }) uv.Add(new Vector2(p.x, p.z));
            else
            {
                Vector3 axis = (b - a).normalized, up = Vector3.Cross(normal, axis);
                uv.Add(Vector2.zero); uv.Add(new Vector2(Vector3.Dot(b - a, axis), Vector3.Dot(b - a, up)));
                uv.Add(new Vector2(Vector3.Dot(c - a, axis), Vector3.Dot(c - a, up))); uv.Add(new Vector2(Vector3.Dot(d - a, axis), Vector3.Dot(d - a, up)));
            }
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }

        // Axis-aligned slab in station (u,v): top face up, bottom face down, four sides outward.
        public void Slab(float u0, float u1, float v0, float v1, float top, float depth)
        {
            Vector3 a = P(u0, v0, top), b = P(u1, v0, top), c = P(u1, v1, top), d = P(u0, v1, top), dn = Vector3.down * depth;
            Quad(a, b, c, d); Quad(d + dn, c + dn, b + dn, a + dn);
            Quad(a + dn, b + dn, b, a); Quad(b + dn, c + dn, c, b); Quad(c + dn, d + dn, d, c); Quad(d + dn, a + dn, a, d);
        }

        public void Box(Vector3 center, Vector3 size)
        {
            Vector3 h = size * .5f;
            Vector3 a = center + new Vector3(-h.x, -h.y, -h.z), b = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 c = center + new Vector3(h.x, -h.y, h.z), d = center + new Vector3(-h.x, -h.y, h.z), up = Vector3.up * size.y;
            Quad(a, b, c, d); Quad(d + up, c + up, b + up, a + up);
            Quad(b, a, a + up, b + up); Quad(c, b, b + up, c + up); Quad(d, c, c + up, d + up); Quad(a, d, d + up, a + up);
        }

        public void Tube(Vector3 a, Vector3 b, float radius, int sides)
        {
            Vector3 axis = (b - a).normalized;
            Vector3 x = Vector3.Cross(axis, Mathf.Abs(axis.y) > .95f ? Vector3.forward : Vector3.up).normalized, y = Vector3.Cross(axis, x);
            for (int i = 0; i < sides; i++)
            {
                float t = i * Mathf.PI * 2 / sides, t1 = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 r = (x * Mathf.Cos(t) + y * Mathf.Sin(t)) * radius, s = (x * Mathf.Cos(t1) + y * Mathf.Sin(t1)) * radius;
                Quad(a + r, a + s, b + s, b + r); Triangle(a, a + s, a + r); Triangle(b, b + r, b + s);
            }
        }

        public int TriangleCount { get { return triangles.Count / 3; } }
        public int VertexCount { get { return vertices.Count; } }
        public void SetNormal(int index, Vector3 normal) { smooth[index] = normal; }

        // Kit quad with explicit texture coordinates (a..d in loop order), wound to face n.
        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ta, Vector2 tb, Vector2 tc, Vector2 td, Vector3 n)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uv.Add(ta); uv.Add(tb); uv.Add(tc); uv.Add(td);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) >= 0) { triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3); }
            else { triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2); }
        }

        // Kit triangle with explicit texture coordinates, wound to face n.
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ta, Vector2 tb, Vector2 tc, Vector3 n)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); uv.Add(ta); uv.Add(tb); uv.Add(tc);
            bool keep = Vector3.Dot(Vector3.Cross(b - a, c - a), n) >= 0;
            triangles.Add(i); triangles.Add(keep ? i + 1 : i + 2); triangles.Add(keep ? i + 2 : i + 1);
        }

        public void Save()
        {
            if (vertices.Count == 0) return;
            // Kit previews keep meshes in memory (meshFolder null); every other build writes one asset per batch.
            string path = meshFolder == null ? null : meshFolder + "/" + name.Replace(' ', '_') + ".asset";
            Mesh saved = path == null ? null : AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = path != null && saved == null;
            if (saved == null)
            {
                saved = new Mesh();
                if (path == null) { saved.hideFlags = HideFlags.HideAndDontSave; kitTransient.Add(saved); }
            }
            saved.Clear(false); saved.name = name; saved.indexFormat = IndexFormat.UInt32;
            saved.SetVertices(vertices); saved.SetUVs(0, uv); saved.SetTriangles(triangles, 0, false);
            saved.RecalculateNormals();
            if (smooth.Count > 0)
            {
                Vector3[] normals = saved.normals;
                foreach (KeyValuePair<int, Vector3> n in smooth) normals[n.Key] = n.Value;
                saved.normals = normals;
            }
            saved.RecalculateTangents(); saved.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(saved, path);
            if (path != null) EditorUtility.SetDirty(saved);
            GameObject go = new GameObject(name); go.transform.SetParent(parent, false);
            if (!renderless)
            {
                go.AddComponent<MeshFilter>().sharedMesh = saved;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            if (collision) go.AddComponent<MeshCollider>().sharedMesh = saved;
            if (!renderless && EnclosureShadow(name)) CastEnclosureShadow(go, saved, mat);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }

    // ================================ Interior kit (interior twin, 2026-09-24) ================================
    // Spec-driven generators for the zone-spec vocabulary; full schema, defaults, units and triangle budgets in
    // .planning/2026-09-24-interior-twin/kit-spec.md. Every generator builds into the current `frame` through B() batches.
    // Entries: KitBuild(spec, root path) - Main's scene integration; KitPreview(prefix[, showcase]) - self-cleaning renders;
    //   KitMaterials() - persists kit-owned materials to Kit/Materials.
    // Element: {"type","id","zone","tier":"a"|"a-failed"|"b" (c or missing -> skipped, never built),"evidence":[paths],
    //   "frame":"root"|"box" (box = majibang-plan boxFrame coordinates),"geometry":{...},"params":{...}}.
    // Units: source-model metres in station UVY (u,v plan, y absolute height); angles in degrees; colours "#RRGGBB".
    // Materials: finish keyword (KitFinishes / KitDerived), kit material (KitDefs), "#RRGGBB" paint, or an "Assets/...mat" path.
    // Geometry: point [u,v]; edge [[u,v],[u,v]]; polyline [[u,v],..]; polygon [[u,v],..]; holes [[[u,v],..],..] (holes may touch,
    //   overlap or cross the outer ring: filled = outer minus union of holes); y = floor (ceiling underside for SuspendedCeiling,
    //   light height for Lighting); y0/y1 = vertical extent.
    // Facing: params.faceToward [u,v] (preferred) or params.side "left"|"right" (left = (-dv,du) of the edge direction);
    //   point elements: params.heading [du,dv] or faceToward (front / upward-travel direction).
    // Types (geometry | main params, defaults):
    //   SuspendedCeiling polygon,holes?,y | module "600x600"|"1200x300", finish ceiling600|ceilingPan, uvRepeat (4.8 ceiling600),
    //     gridAngle 0, gridOrigin polygon[0], grid true, trim true, bulkhead 0, holeBulkhead .3, trimEdges/bulkheadEdges
    //     all|outer|holes|none|[outer edge indices], fixtures{layout grid|linear|downlight|none, pitch [2.4,2.4], size, phase [i,j],
    //     lights{pitch [6,6], drop 1.2, intensity, range, kelvin, shadows, mode}}
    //   Column point,y0,y1 | shape round|rect, diameter .9, size [.9,.9], angle 0, cladding stainless|granite|panel|plaster|<mat>,
    //     panelWidth (rect face), panelHeight 1.2 (.9 granite), joints 4, base .12, cap .08, cornerGuard 1.5 (rect panel/plaster)
    //   WallCladding polyline,y0,y1 | finish stone, panelWidth 1.2, panelHeight .6 (0 = full), joint .008, standoff .03,
    //     skirting .12, cap .04, openings [[s0,s1,yTop],..] (s along the polyline from its start, yTop absolute), collider true
    //   Storefront edge,y | height 3, transom 2.5 (0 none), mullionPitch 1.5, mullionWidth .05, mullionDepth .12, bulkhead .15,
    //     frame stainless|black|white|<mat>, doors [{at, width 1.8, type sliding|swing|open, leaves, open 0..1}],
    //     fascia{height .8, depth .3, inset .06, text|texture|material, board "#1E2226", ink, signWidth, signAt, downlights true}
    //   ShopInterior polygon,y,y1(y+3) | kind cafe|convenience|bakery|restaurant|retail, front 0 (edge index), floor (by kind),
    //     wallFinish plaster, backWall (by kind), ceiling Kit_GridWhite, downlightPitch 1.4, fixtures true, light true, collider true, seed
    //   ServiceDoor edge,y | t .5, width 1.0, height 2.1, leaves (2 when width>1.3), colour "#7E868C", frame "#565D63", proud .05,
    //     plate "관계자외 출입금지", plateColour, plateInk, kickPlate true, closer true
    //   FloorFinish polygon,holes?,y | finish polished, lift .003, rotation 0, uvOrigin, uvRepeat 1, border{width .3, finish granite},
    //     strips [{from,to,width .3,kind line|dot}], pads [{center,size [.6,.6],angle 0,kind dot|line}]
    //   HangingSign point,y(bottom) | heading, width 2.4, height .45, depth .12, style navy|yellow|green|dark, items
    //     [{text,sub,arrow left|right|up|down|upleft|upright|downleft|downright,badge}], back, doubleSided true, ceilingY (y+h+.6),
    //     case signCase, texture|material
    //   WallSign point,y(centre) | heading (out of the wall), width 1.2, height .4, depth .05, standoff .02, style, items, texture|material
    //   Escalator foot,y | heading (upward travel), rise, angle 30, width 1.0, flatSteps 3, runout .9, truss 1.0, count 1,
    //     spacing width+.75, direction up|down, directions [..], cladding escalatorBody
    //   ElevatorFront edge,y | t .5, cars 1, pitch 2.6, doorWidth 1.1, doorHeight 2.1, portalHeight 2.9, surround granite,
    //     jamb stainless, leaf hairline, proud .05, open 0, glass false, floorLabel "1", label "엘리베이터", labelSub, labelHeight,
    //     panelWidth .9, panelHeight .6
    //   Stair foot,y | heading, rise, width 1.8, riser .165, tread .3, maxRun 18, landing 1.2, finish granite, stringer gypsum,
    //     nosing rubber|yellow, sides balustrade|rail|none or [left,right]
    //   LockerBank edge,y | depth .55, columnWidth .45, rows [.46,.36,.36,.36] (bottom->top), plinth .1, colour locker, kiosk,
    //     label "물품보관함 Lockers", prop false (Kits/Web/LuggageLocker.fbx)
    //   VendingMachine point,y | heading, kind drink|snack|coffee|ticket, width .95 (.8 ticket), depth .8, height 1.83 (1.75), colour, text
    //   Bench point,y | heading (sitters face), length 1.8, seats (length/.6), seatHeight .44, back true, finish seat, frame hairline
    //   Gate edge,y | faceToward (entry side), pitch .9, lanes (fit), wide [lane indices], widePitch 1.25, cabinetWidth .22,
    //     cabinetLength 1.6, height 1.0, body gateBody, flaps open|closed, mode both|entry|exit, fences [[[u,v],[u,v]],..], fenceHeight 1
    //   Lighting point|polygon(+holes),y | kind point|spot, pitch [6,6], range 8, intensity 1.2, kelvin 4000, spotAngle 90,
    //     shadows none|soft, mode mixed|baked|realtime
    //   Balustrade polyline,y | height 1.1, panel glass|bars|solid, glass clear|teal|<mat>, rail round|flat|none, railFinish stainless,
    //     postPitch 1.2 (0 frameless), shoe true (false for solid), shoeHeight .1, panelFinish gypsum (solid), faceToward, collider true,
    //     handrail {height .9, offset .07, radius .02, finish, bracketPitch 1.2} (second rail on brackets, faceToward side)
    //   DisplayBoard point,y (board bottom; floor mount: floor) | heading, mount hanging|wall|floor, kind departure|arrival|screen|ledband|
    //     clock|timetable, width 2.4 (.9 clock), height .6 (.3 ledband, .35 clock, 1.2 timetable), depth .12, doubleSided (hanging),
    //     ceilingY (bottom+h+.8), standHeight 1.2, standoff .02, housing dark, title, rows [{time,train,dest,track,status}], rowCount 4,
    //     text (clock), ink, texture|material (screen)
    //   Counter polyline (customer front),y | faceToward, depth .7, height 1.05, top granite, body counterWood, glass clear,
    //     screens [{at,width 1.2,height .75,label}], equipment true, backPanel {height 2.4, offset 1.4, finish wood, text}, label, collider
    //   DoorSet edge,y | faceToward (outside), doorHeight 2.4, frameHeight 3.0, doors [{at,width 1.8,type auto|swing,open}] or count,
    //     pitch (width+1.2), doorWidth, type, open; frame stainless, glass clear, transom true, sidelights true, vestibule 0, mat,
    //     number, numberAt, numberSize .45, sign (text or items), signWidth, signHeight .45, signStyle navy, signSide both|inside|outside
    //   Fixture point,y (wall/column: centre; floor: floor; ceiling: ceiling underside) | kind clock|totem|standee|planter|bin|aed|cctv|
    //     speaker|extinguisher|hydrant|atm|kiosk|frame|charger, heading, mount floor|wall|ceiling|column (default by kind), width,
    //     height, depth, text, texture|material, drop .6 (ceiling clock), finish/plantHeight (planter), colour/band (atm, kiosk),
    //     label0/label1 (bin), signPlate (floor extinguisher), column {center [u,v], radius} (clamp ring for column mounts)
    const string Kit = Base + "Kit";
    const string KitFolderRegistry = ".planning/2026-09-24-interior-twin/kit-folders.json";   // mesh folder -> owning kit root
    const string Lane = Base + "Materials/Lane/";
    const string Furniture = Base + "Kits/furniture-bits/KayKit_Furniture_Bits_1.0_FREE/Assets/fbx (unity)/";
    static readonly Dictionary<string, string> KitFinishes = new Dictionary<string, string>
    {
        { "ceilingMetal", Lane + "ceiling_metal_panel.mat" }, { "ceilingLouver", Lane + "ceiling_linear_louver.mat" },
        { "gypsum", Lane + "wall_painted_gypsum_fine.mat" }, { "plaster", Base + "Materials/벽_내장.mat" }, { "panel", Base + "Materials/기둥_마감.mat" },
        { "granite", Lane + "PBR_Granite005A_2K.mat" }, { "stone", Lane + "wall_granite_drypanel.mat" },
        { "trim", Lane + "trim_stainless_hairline.mat" }, { "steelBlue", Lane + "wall_steel_panel_blue.mat" },
        { "wood", Lane + "wall_wood_slat_panel.mat" }, { "ceramic", Lane + "wall_ceramic_tile.mat" }, { "shopPanel", Lane + "wall_b1_shop_panel.mat" },
        { "floorGranite", Lane + "floor_granite_tile.mat" }, { "polished", Lane + "floor_station_polished.mat" }, { "terrazzo", Lane + "PBR_Terrazzo018_4K.mat" },
        { "porcelain", Lane + "floor_porcelain_large.mat" }, { "woodFloor", Lane + "PBR_WoodFloor064_4K.mat" }, { "checker", Lane + "PBR_Metal046A_2K.mat" },
        { "tactileLine", Lane + "floor_tactile_linear_yellow.mat" }, { "tactileDot", Lane + "floor_tactile_yellow.mat" },
        { "seat", Lane + "seat_charcoal_poly.mat" }, { "rubber", Lane + "stair_rubber_nosing.mat" }, { "signCase", Lane + "sign_case_panel.mat" },
        { "glass", Mats + "Glass_GreenClear.mat" }, { "steel", Mats + "Stainless_Hairline.mat" }, { "dark", Mats + "Canopy_Dark.mat" },
        { "white", Mats + "Tube_White.mat" }, { "yellow", Mats + "Sign_ExitYellow.mat" }, { "counterWood", Mats + "Counter_Wood.mat" },
        { "gateBody", Base + "Materials/개찰기_금속.mat" }, { "machineBlue", Base + "Materials/발매기_외장.mat" }, { "locker", Base + "Materials/보관함.mat" },
        { "escalatorBody", Base + "Materials/에스컬레이터.mat" }, { "ledPanel", Base + "Materials/조명기구_발광.mat" },
        { "downlight", Base + "Materials/조명기구_downlight.mat" }, { "linearLight", Base + "Materials/조명기구_linear_troffer.mat" },
        { "navy", Base + "Materials/사인_청색.mat" }, { "stepTread", "Assets/ChooGuard/Art/OfficialBusanStation/Materials/source-abf1f31198576978dc15.mat" },
        { "concrete", Lane + "PBR_Concrete048_2K.mat" }, { "tile", Lane + "wall_toilet_tile_300x600.mat" }, { "floorTile", Lane + "floor_toilet_tile.mat" },
    };
    // Kit-owned materials (Kit/Materials/<name>.mat; in memory during KitPreview): base, emission, smoothness, metallic, unlit.
    static readonly Dictionary<string, (Color c, Color e, float sm, float met, bool unlit)> KitDefs = new Dictionary<string, (Color c, Color e, float sm, float met, bool unlit)>
    {
        { "Kit_SignNavy", (new Color(.03f, .12f, .34f), new Color(.015f, .05f, .15f), .55f, 0f, false) },
        { "Kit_SignGreen", (new Color(0f, .5f, .28f), new Color(0f, .1f, .05f), .5f, 0f, false) },
        { "Kit_Glyph", (Color.white, Color.clear, 0f, 0f, true) },
        { "Kit_GlyphDark", (new Color(.07f, .07f, .08f), Color.clear, 0f, 0f, true) },
        { "Kit_GlyphYellow", (new Color(.97f, .76f, .05f), Color.clear, 0f, 0f, true) },
        { "Kit_Screen", (new Color(.015f, .018f, .022f), Color.clear, .92f, 0f, false) },
        { "Kit_ScreenBlue", (new Color(.05f, .14f, .3f), new Color(.12f, .42f, .95f), .9f, 0f, false) },
        { "Kit_EmitGreen", (new Color(.04f, .3f, .1f), new Color(.1f, 1.5f, .35f), .8f, 0f, false) },
        { "Kit_EmitRed", (new Color(.3f, .03f, .03f), new Color(1.7f, .08f, .06f), .8f, 0f, false) },
        { "Kit_EmitAmber", (new Color(.3f, .18f, .02f), new Color(1.7f, .8f, .05f), .8f, 0f, false) },
        { "Kit_EmitCyan", (new Color(.03f, .2f, .3f), new Color(.1f, .9f, 1.6f), .8f, 0f, false) },
        { "Kit_Rubber", (new Color(.04f, .04f, .045f), Color.clear, .55f, 0f, false) },
        { "Kit_Backlight", (new Color(.95f, .95f, .92f), new Color(1.1f, 1.1f, 1.05f), .5f, 0f, false) },
        { "Kit_GridWhite", (new Color(.9f, .9f, .89f), new Color(.3f, .3f, .29f), .45f, 0f, false) },
        { "Kit_TrussWhite", (new Color(.93f, .93f, .92f), new Color(.1f, .1f, .1f), .5f, .1f, false) },
        { "Kit_DeckWhite", (new Color(.9f, .9f, .89f), new Color(.28f, .28f, .27f), .3f, 0f, false) },
        { "Kit_ProductRed", (new Color(.75f, .08f, .07f), Color.clear, .6f, .2f, false) },
        { "Kit_ProductBlue", (new Color(.08f, .25f, .7f), Color.clear, .6f, .2f, false) },
        { "Kit_ProductGreen", (new Color(.1f, .55f, .2f), Color.clear, .6f, .2f, false) },
        { "Kit_ProductYellow", (new Color(.95f, .75f, .1f), Color.clear, .6f, .2f, false) },
        { "Kit_ProductWhite", (new Color(.92f, .92f, .9f), Color.clear, .6f, .1f, false) },
        { "Kit_Bread", (new Color(.72f, .45f, .2f), Color.clear, .3f, 0f, false) },
        { "Kit_Porcelain", (new Color(.93f, .94f, .95f), Color.clear, .85f, 0f, false) },
        { "Kit_Mirror", (new Color(.74f, .79f, .82f), Color.clear, .97f, .35f, false) },
        { "Kit_Carton", (new Color(.6f, .44f, .27f), Color.clear, .2f, 0f, false) },
        { "Kit_Vinyl", (new Color(.62f, .64f, .6f), Color.clear, .45f, 0f, false) },
        { "Kit_Aluminium", (new Color(.8f, .81f, .82f), Color.clear, .55f, .35f, false) },
    };
    // Keyword aliases for kit-owned materials.
    static readonly Dictionary<string, string> KitAliases = new Dictionary<string, string>
    {
        { "vinyl", "Kit_Vinyl" }, { "aluminium", "Kit_Aluminium" }, { "mirror", "Kit_Mirror" }, { "porcelainWhite", "Kit_Porcelain" },
    };
    // Kit variants of project materials (same maps, corrected response): the source ceiling textures are authored metallic and read
    // charcoal under the weak light below slabs; albedo ~.86 plus self-emission from the colour map keeps them near-white like the footage.
    static readonly Dictionary<string, (string name, string source, Color c, Color e, float sm, float met)> KitDerived = new Dictionary<string, (string name, string source, Color c, Color e, float sm, float met)>
    {
        { "ceiling600", ("Kit_Ceiling600", Base + "Materials/PBR_OfficeCeiling003_2K.mat", new Color(.86f, .86f, .85f), new Color(.34f, .34f, .33f), .12f, 0f) },
        { "ceilingPan", ("Kit_CeilingPan", Lane + "ceiling_perforated_pan.mat", new Color(.88f, .88f, .87f), new Color(.32f, .32f, .31f), .35f, .1f) },
        // Brushed metals: without reflection probes fully metallic stainless renders near-black, so the kit keeps the maps at
        // metallic .45 with a light albedo.
        { "stainless", ("Kit_Stainless", Lane + "column_stainless_brushed.mat", new Color(.74f, .76f, .77f), Color.clear, .6f, .45f) },
        { "hairline", ("Kit_Hairline", Lane + "metal_stainless_hairline.mat", new Color(.78f, .79f, .8f), Color.clear, .55f, .45f) },
    };
    static readonly string[] KitProducts = { "Kit_ProductRed", "Kit_ProductBlue", "Kit_ProductGreen", "Kit_ProductYellow", "Kit_ProductWhite" };
    // Library props: path, optional child, yaw offset that turns the model's front to +z.
    static readonly Dictionary<string, (string path, string child, float front)> KitModels = new Dictionary<string, (string path, string child, float front)>
    {
        { "kit_kitchenCounter", (Restaurant + "kitchencounter_straight_decorated.fbx", null, 0f) },
        { "kit_kitchenTable", (Restaurant + "kitchentable_A_large_decorated.fbx", null, 0f) },
        { "kit_tableRound", (Restaurant + "table_round_A.fbx", null, 0f) },
        { "kit_chair", (Restaurant + "chair_A.fbx", null, 0f) },
        { "kit_dining", (Base + "Kits/Ground/dining/dining.fbx", null, 0f) },
        { "kit_cabinet", (Furniture + "cabinet_medium_decorated.fbx", null, 0f) },
        { "kit_tableMedium", (Furniture + "table_medium.fbx", null, 0f) },
        { "kit_plant", (Furniture + "cactus_medium_A.fbx", null, 0f) },
        { "kit_locker", (Base + "Kits/Web/LuggageLocker.fbx", null, 0f) },
    };
    static readonly Dictionary<string, Material> kitMats = new Dictionary<string, Material>();
    static readonly List<object> kitReport = new List<object>();
    static int kitPropTris, kitTextTris, kitLights;
    static float kitFillArea;
    static bool kitPreviewing;

    static void KitInit()
    {
        lit = Shader.Find("Universal Render Pipeline/Lit");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ChooGuard/ThirdParty/Fonts/NotoSansCJKkr-Regular.otf");
        if (lit == null || font == null) throw new InvalidOperationException("URP Lit shader or Korean font missing.");
        stone = Reuse("Floor_Granite"); plaster = Reuse("Soffit_Plaster"); steel = Reuse("Stainless_Hairline");
        glass = Reuse("Glass_GreenClear"); dark = Reuse("Canopy_Dark"); wood = Reuse("Counter_Wood"); led = Reuse("Light_Lens"); blue = Reuse("Sign_RailBlue");
        kitMats.Clear(); kitReport.Clear(); kitPropTris = kitTextTris = kitLights = 0; kitFillArea = 0;
    }

    // Resolves a material key; kit-owned materials are persisted under Kit/Materials except during KitPreview (meshFolder null).
    static Material KM(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Empty kit material key.");
        if (KitAliases.TryGetValue(key, out string alias)) key = alias;
        if (kitMats.TryGetValue(key, out Material m)) return m;
        if (key.StartsWith("Assets/", StringComparison.Ordinal) || KitFinishes.ContainsKey(key))
        {
            string path = KitFinishes.TryGetValue(key, out string mapped) ? mapped : key;
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) throw new InvalidOperationException("Kit material missing: " + path);
        }
        else if (key.StartsWith("#", StringComparison.Ordinal))
        {
            if (!ColorUtility.TryParseHtmlString(key, out Color c)) throw new InvalidOperationException("Bad kit colour: " + key);
            m = KitCreate("Kit_Paint_" + key.Substring(1).ToUpperInvariant(), lit, x => { x.SetColor("_BaseColor", c); x.SetFloat("_Smoothness", .4f); x.SetFloat("_Metallic", 0); });
        }
        else if (KitDerived.TryGetValue(key, out var v))
        {
            Material src = AssetDatabase.LoadAssetAtPath<Material>(v.source);
            if (src == null) throw new InvalidOperationException("Kit source material missing: " + v.source);
            m = KitCreate(v.name, lit, x =>
            {
                Texture map = src.GetTexture("_BaseMap"), nrm = src.GetTexture("_BumpMap"), ao = src.HasProperty("_OcclusionMap") ? src.GetTexture("_OcclusionMap") : null;
                x.SetTexture("_BaseMap", map); x.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap")); x.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
                if (nrm != null) { x.SetTexture("_BumpMap", nrm); x.EnableKeyword("_NORMALMAP"); }
                if (ao != null) { x.SetTexture("_OcclusionMap", ao); x.EnableKeyword("_OCCLUSIONMAP"); }
                x.SetColor("_BaseColor", v.c); x.SetFloat("_Smoothness", v.sm); x.SetFloat("_Metallic", v.met);
                if (v.e.maxColorComponent > 0) { x.SetTexture("_EmissionMap", map); x.EnableKeyword("_EMISSION"); x.SetColor("_EmissionColor", v.e); }
                x.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            });
        }
        else if (KitDefs.TryGetValue(key, out var d))
        {
            m = KitCreate(key, d.unlit ? Shader.Find("Universal Render Pipeline/Unlit") : lit, x =>
            {
                x.SetColor("_BaseColor", d.c);
                if (!d.unlit) { x.SetFloat("_Smoothness", d.sm); x.SetFloat("_Metallic", d.met); }
                if (d.e.maxColorComponent > 0) { x.EnableKeyword("_EMISSION"); x.SetColor("_EmissionColor", d.e); x.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive; }
            });
        }
        else throw new InvalidOperationException("Unknown kit material: " + key);
        kitMats[key] = m;
        return m;
    }

    // Unlit lightbox material showing a project texture (importer settings untouched).
    static Material KitTexMat(string texPath)
    {
        string key = "tex:" + texPath;
        if (kitMats.TryGetValue(key, out Material m)) return m;
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) throw new InvalidOperationException("Kit texture missing: " + texPath);
        m = KitCreate("Kit_Tex_" + Path.GetFileNameWithoutExtension(texPath), Shader.Find("Universal Render Pipeline/Unlit"), x => { x.SetTexture("_BaseMap", tex); x.SetColor("_BaseColor", Color.white); });
        kitMats[key] = m;
        return m;
    }

    static Material KitCreate(string name, Shader shader, Action<Material> setup)
    {
        if (shader == null) throw new InvalidOperationException("Kit shader missing for " + name);
        var fresh = new Material(shader) { name = name };
        setup(fresh);
        if (meshFolder == null) { fresh.hideFlags = HideFlags.HideAndDontSave; kitTransient.Add(fresh); return fresh; }
        EnsureFolder(Kit + "/Materials");
        string path = Kit + "/Materials/" + KitSafe(name) + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null) { AssetDatabase.CreateAsset(fresh, path); return fresh; }
        EditorUtility.CopySerialized(fresh, existing); UnityEngine.Object.DestroyImmediate(fresh); EditorUtility.SetDirty(existing);
        return existing;
    }

    static string KitSafe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    // ---- spec readers ----
    static float KF(JObject o, string k, float d) { JToken t = o?[k]; return t == null || t.Type == JTokenType.Null ? d : (float)t; }
    static int KI(JObject o, string k, int d) { JToken t = o?[k]; return t == null || t.Type == JTokenType.Null ? d : (int)t; }
    static string KS(JObject o, string k, string d) { JToken t = o?[k]; return t == null || t.Type == JTokenType.Null ? d : (string)t; }
    static bool KB(JObject o, string k, bool d) { JToken t = o?[k]; return t == null || t.Type == JTokenType.Null ? d : (bool)t; }
    static JObject KO(JObject o, string k) { return o?[k] as JObject ?? new JObject(); }
    static JToken KT(JObject o, string k, string id)
    {
        JToken t = o?[k];
        if (t == null || t.Type == JTokenType.Null) throw new InvalidOperationException(id + ": required field '" + k + "' missing.");
        return t;
    }
    static float KN(JObject o, string k, string id) { return (float)KT(o, k, id); }
    static Vector2 KV(JToken t) { return new Vector2((float)t[0], (float)t[1]); }
    static List<Vector2> KPoly(JToken t) { var l = new List<Vector2>(); foreach (JToken q in t) l.Add(KV(q)); return l; }
    static List<List<Vector2>> KHoles(JObject g) { var l = new List<List<Vector2>>(); if (g["holes"] is JArray hs) foreach (JToken h in hs) l.Add(KPoly(h)); return l; }
    static Vector3 KP(Vector2 uv, float y) { return P(uv.x, uv.y, y); }
    static Vector3 KD(Vector2 uvDir) { return P(uvDir.x, uvDir.y, 0); }

    // Outward face normal of an edge (horizontal, local frame): toward params.faceToward, else params.side.
    static Vector3 KEdgeNormal(Vector2 a, Vector2 b, JObject p)
    {
        Vector3 dir = (KP(b, 0) - KP(a, 0)).normalized, n = Vector3.Cross(Vector3.up, dir);
        if (p["faceToward"] is JArray f) { if (Vector3.Dot(KP(KV(f), 0) - (KP(a, 0) + KP(b, 0)) * .5f, n) < 0) n = -n; }
        else if (KS(p, "side", "left") == "right") n = -n;
        return n;
    }

    static Vector3 KHeading(JObject p, Vector2 at, string id)
    {
        if (p["faceToward"] is JArray f) { Vector3 d = KP(KV(f), 0) - KP(at, 0); if (d.sqrMagnitude > 1e-6f) return d.normalized; }
        if (p["heading"] is JArray h) return KD(KV(h).normalized);
        throw new InvalidOperationException(id + ": params.heading or params.faceToward required.");
    }

    // ---- geometry primitives (local frame) ----
    // Rectangle o + ex*[0,w] + ey*[0,h] facing n; texture coordinates in metres from uv0 times uvScale.
    static void KRect(Batch b, Vector3 o, Vector3 ex, Vector3 ey, float w, float h, Vector3 n, Vector2 uv0, float uvScale = 1)
    {
        b.Face(o, o + ex * w, o + ex * w + ey * h, o + ey * h, uv0 * uvScale, (uv0 + new Vector2(w, 0)) * uvScale,
            (uv0 + new Vector2(w, h)) * uvScale, (uv0 + new Vector2(0, h)) * uvScale, n);
    }
    // Rectangle centred at c spanning ex (width w) and ey (height h), facing n.
    static void KRectC(Batch b, Vector3 c, Vector3 ex, Vector3 ey, float w, float h, Vector3 n)
    { KRect(b, c - ex * w * .5f - ey * h * .5f, ex, ey, w, h, n, Vector2.zero); }
    // Oriented box: centre c, orthonormal axes x/y/z, full sizes s; bottom and back (-z) faces optional.
    static void KBox(Batch b, Vector3 c, Vector3 x, Vector3 y, Vector3 z, Vector3 s, bool bottom = true, bool back = true)
    {
        Vector3 hx = x * s.x * .5f, hy = y * s.y * .5f, hz = z * s.z * .5f;
        KRect(b, c - hx - hy + hz, x, y, s.x, s.y, z, Vector2.zero);
        if (back) KRect(b, c - hx - hy - hz, x, y, s.x, s.y, -z, Vector2.zero);
        KRect(b, c + hx - hy - hz, z, y, s.z, s.y, x, Vector2.zero);
        KRect(b, c - hx - hy - hz, z, y, s.z, s.y, -x, Vector2.zero);
        KRect(b, c - hx + hy - hz, x, z, s.x, s.z, y, Vector2.zero);
        if (bottom) KRect(b, c - hx - hy - hz, x, z, s.x, s.z, -y, Vector2.zero);
    }
    // Smooth vertical cylinder wall around the vertical axis through `axis` (y ignored), metre UVs.
    static void KCyl(Batch b, Vector3 axis, float r, float y0, float y1, int sides, bool outward = true)
    {
        axis.y = 0;
        for (int i = 0; i < sides; i++)
        {
            float t0 = i * Mathf.PI * 2 / sides, t1 = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(t0), 0, Mathf.Sin(t0)), d1 = new Vector3(Mathf.Cos(t1), 0, Mathf.Sin(t1)), h = Vector3.up * (y1 - y0);
            Vector3 a = axis + d0 * r + Vector3.up * y0, c = axis + d1 * r + Vector3.up * y0;
            int v = b.VertexCount; float s = outward ? 1 : -1;
            b.Face(a, c, c + h, a + h, new Vector2(t0 * r, y0), new Vector2(t1 * r, y0), new Vector2(t1 * r, y1), new Vector2(t0 * r, y1), (d0 + d1) * s);
            b.SetNormal(v, d0 * s); b.SetNormal(v + 1, d1 * s); b.SetNormal(v + 2, d1 * s); b.SetNormal(v + 3, d0 * s);
        }
    }
    // Annulus (r0 > 0) or disc in the plane spanned by ex/ey around c, facing n.
    static void KRing(Batch b, Vector3 c, Vector3 ex, Vector3 ey, float r0, float r1, int sides, Vector3 n)
    {
        for (int i = 0; i < sides; i++)
        {
            float t0 = i * Mathf.PI * 2 / sides, t1 = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 d0 = ex * Mathf.Cos(t0) + ey * Mathf.Sin(t0), d1 = ex * Mathf.Cos(t1) + ey * Mathf.Sin(t1);
            Vector2 u0 = new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)), u1 = new Vector2(Mathf.Cos(t1), Mathf.Sin(t1));
            if (r0 <= 1e-4f) b.Tri(c, c + d0 * r1, c + d1 * r1, Vector2.zero, u0 * r1, u1 * r1, n);
            else b.Face(c + d0 * r0, c + d1 * r0, c + d1 * r1, c + d0 * r1, u0 * r0, u1 * r0, u1 * r1, u0 * r1, n);
        }
    }
    // Horizontal ring/disc helpers.
    static void KRingH(Batch b, Vector3 c, float r0, float r1, int sides, Vector3 n) { KRing(b, c, Vector3.right, Vector3.forward, r0, r1, sides, n); }
    // Round tube a->b with smooth normals.
    static void KTube(Batch b, Vector3 a, Vector3 c, float r, int sides)
    {
        Vector3 axis = (c - a).normalized;
        Vector3 x = Vector3.Cross(axis, Mathf.Abs(axis.y) > .95f ? Vector3.forward : Vector3.up).normalized, y = Vector3.Cross(axis, x);
        float len = Vector3.Distance(a, c);
        for (int i = 0; i < sides; i++)
        {
            float t0 = i * Mathf.PI * 2 / sides, t1 = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 d0 = x * Mathf.Cos(t0) + y * Mathf.Sin(t0), d1 = x * Mathf.Cos(t1) + y * Mathf.Sin(t1);
            int v = b.VertexCount;
            b.Face(a + d0 * r, a + d1 * r, c + d1 * r, c + d0 * r, new Vector2(t0 * r, 0), new Vector2(t1 * r, 0), new Vector2(t1 * r, len), new Vector2(t0 * r, len), d0 + d1);
            b.SetNormal(v, d0); b.SetNormal(v + 1, d1); b.SetNormal(v + 2, d1); b.SetNormal(v + 3, d0);
        }
    }
    static void KTubePath(Batch b, List<Vector3> path, float r, int sides) { for (int i = 0; i + 1 < path.Count; i++) if ((path[i + 1] - path[i]).sqrMagnitude > 1e-8f) KTube(b, path[i], path[i + 1], r, sides); }
    // Arrow glyph in the plane (right, up) centred at c, facing n; size = overall length.
    static void KArrow(Batch b, Vector3 c, Vector3 right, Vector3 up, Vector3 n, float size, string dir)
    {
        float deg = dir == "up" ? 90 : dir == "down" ? -90 : dir == "left" ? 180 : dir == "upleft" ? 135 : dir == "upright" ? 45 : dir == "downleft" ? -135 : dir == "downright" ? -45 : 0;
        float a = deg * Mathf.Deg2Rad; Vector3 ax = right * Mathf.Cos(a) + up * Mathf.Sin(a), ay = -right * Mathf.Sin(a) + up * Mathf.Cos(a);
        c += n * .004f;
        KRect(b, c - ax * .45f * size - ay * .085f * size, ax, ay, .5f * size, .17f * size, n, Vector2.zero);
        b.Tri(c + ax * .45f * size, c - ax * .02f * size + ay * .33f * size, c - ax * .02f * size - ay * .33f * size, Vector2.zero, Vector2.right, Vector2.up, n);
    }
    // Diagonal cross glyph (stop / no entry).
    static void KCross(Batch b, Vector3 c, Vector3 right, Vector3 up, Vector3 n, float size)
    {
        c += n * .004f;
        Vector3 d1 = (right + up).normalized, d2 = (right - up).normalized;
        KRectC(b, c, d1, d2, size, size * .18f, n); KRectC(b, c, d2, d1, size, size * .18f, n);
    }

    // Renderless double-sided collider geometry shared by the kit (walkable routes keep only intentional blockers).
    static Batch KCol() { Batch c = B("Kit_Colliders", dark, true); c.renderless = true; return c; }
    static void KColRect(Vector3 o, Vector3 ex, Vector3 ey, float w, float h)
    {
        if (w <= 1e-3f || h <= 1e-3f) return;
        Batch c = KCol(); Vector3 n = Vector3.Cross(ex, ey);
        KRect(c, o, ex, ey, w, h, n, Vector2.zero); KRect(c, o, ex, ey, w, h, -n, Vector2.zero);
    }
    static void KColBox(Vector3 c, Vector3 x, Vector3 y, Vector3 z, Vector3 s) { KBox(KCol(), c, x, y, z, s); }

    // ---- planar polygon utilities (station u,v) ----
    static float KArea(List<Vector2> p) { float a = 0; for (int i = 0; i < p.Count; i++) { Vector2 q = p[i], r = p[(i + 1) % p.Count]; a += q.x * r.y - r.x * q.y; } return a * .5f; }
    static float KOrient(Vector2 a, Vector2 b, Vector2 c) { return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x); }
    static bool KSegCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        const float e = 1e-6f;
        float d1 = KOrient(c, d, a), d2 = KOrient(c, d, b), d3 = KOrient(a, b, c), d4 = KOrient(a, b, d);
        return ((d1 > e && d2 < -e) || (d1 < -e && d2 > e)) && ((d3 > e && d4 < -e) || (d3 < -e && d4 > e));
    }
    // Filled region = inside the outer ring rings[0] (even-odd) and outside the UNION of the hole rings, so holes may touch,
    // overlap each other or cross the outer ring (buffered unit footprints).
    static bool KInRing(List<Vector2> poly, Vector2 q)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
        return c;
    }
    static bool KInside(List<List<Vector2>> rings, Vector2 q)
    {
        if (!KInRing(rings[0], q)) return false;
        for (int r = 1; r < rings.Count; r++) if (KInRing(rings[r], q)) return false;
        return true;
    }
    // Sweep of ring crossings sorted along x: returns [start,end] pairs of the filled region (outer minus union of holes).
    static List<(T a, T b)> KSweep<T>(List<(float x, int ring, T tag)> xs, int ringCount)
    {
        xs.Sort((p, q) => p.x.CompareTo(q.x));
        var parity = new bool[ringCount]; int holesIn = 0; bool filled = false; var result = new List<(T a, T b)>(); (float x, int ring, T tag) start = default;
        foreach (var c in xs)
        {
            parity[c.ring] = !parity[c.ring];
            if (c.ring > 0) holesIn += parity[c.ring] ? 1 : -1;
            bool now = parity[0] && holesIn == 0;
            if (now && !filled) start = c;
            else if (!now && filled && c.x - start.x > 1e-4f) result.Add((start.tag, c.tag));
            filled = now;
        }
        return result;
    }
    // Filled intervals (x ranges) of the horizontal line y = line.
    static List<Vector2> KIntervals(List<List<Vector2>> rings, float line)
    {
        var xs = new List<(float x, int ring, float tag)>();
        for (int r = 0; r < rings.Count; r++)
            for (int i = 0; i < rings[r].Count; i++)
            {
                Vector2 p = rings[r][i], q = rings[r][(i + 1) % rings[r].Count];
                if ((p.y > line) != (q.y > line)) { float x = p.x + (line - p.y) * (q.x - p.x) / (q.y - p.y); xs.Add((x, r, x)); }
            }
        return KSweep(xs, rings.Count).Select(t => new Vector2(t.a, t.b)).ToList();
    }
    // Triangulation of outer minus holes, returned as point triples. Hole-free simple polygons are ear-clipped; everything else
    // (many holes, holes touching/overlapping each other or the outer ring) is trapezoidated: slabs between every vertex height and
    // every edge crossing, filled spans by the union sweep. Exact area; T-junctions only along slab lines.
    static List<Vector2> KTriangulate(List<Vector2> outerIn, List<List<Vector2>> holesIn)
    {
        Vector2 o = outerIn[0];
        var rings = new List<List<Vector2>> { outerIn.Select(q => q - o).ToList() };
        rings.AddRange(holesIn.Where(h => h.Count >= 3).Select(h => h.Select(q => q - o).ToList()));
        if (rings.Count == 1)
        {
            var outer = new List<Vector2>(rings[0]); if (KArea(outer) < 0) outer.Reverse();
            try { return KEarClip(outer).Select(q => q + o).ToList(); }
            catch (InvalidOperationException) { }
        }
        var edges = new List<(Vector2 a, Vector2 b, int ring)>();
        var ys = new List<float>();
        for (int r = 0; r < rings.Count; r++)
            for (int i = 0; i < rings[r].Count; i++)
            {
                Vector2 a = rings[r][i], b = rings[r][(i + 1) % rings[r].Count];
                ys.Add(a.y);
                if (Mathf.Abs(a.y - b.y) > 1e-6f) edges.Add(a.y < b.y ? (a, b, r) : (b, a, r));
            }
        for (int i = 0; i < edges.Count; i++)
            for (int j = i + 1; j < edges.Count; j++)
            {
                var e = edges[i]; var f = edges[j];
                if (e.b.y <= f.a.y || f.b.y <= e.a.y || !KSegCross(e.a, e.b, f.a, f.b)) continue;
                Vector2 d1 = e.b - e.a, d2 = f.b - f.a; float den = d1.x * d2.y - d1.y * d2.x;
                if (Mathf.Abs(den) > 1e-12f) ys.Add(e.a.y + d1.y * (((f.a.x - e.a.x) * d2.y - (f.a.y - e.a.y) * d2.x) / den));
            }
        ys.Sort();
        var tris = new List<Vector2>();
        Func<(Vector2 a, Vector2 b, int ring), float, float> X = (e, y) => e.a.x + (y - e.a.y) * (e.b.x - e.a.x) / (e.b.y - e.a.y);
        for (int k = 0; k + 1 < ys.Count; k++)
        {
            float y0 = ys[k], y1 = ys[k + 1];
            if (y1 - y0 < 1e-5f) continue;
            float ym = (y0 + y1) * .5f;
            var xs = new List<(float x, int ring, Vector2 tag)>();
            foreach (var e in edges)
                if (e.a.y < ym && e.b.y > ym) xs.Add((X(e, ym), e.ring, new Vector2(X(e, y0), X(e, y1))));
            foreach (var (l, r) in KSweep(xs, rings.Count))
            {
                Vector2 p0 = new Vector2(l.x, y0) + o, p1 = new Vector2(r.x, y0) + o, p2 = new Vector2(r.y, y1) + o, p3 = new Vector2(l.y, y1) + o;
                if (r.x - l.x > 1e-5f) { tris.Add(p0); tris.Add(p1); tris.Add(p2); }
                if (r.y - l.y > 1e-5f) { tris.Add(p0); tris.Add(p2); tris.Add(p3); }
            }
        }
        return tris;
    }
    // Pieces of edge a->b that bound the filled region (split at every crossing/touch with other ring edges), with the inward normal.
    static List<(float t0, float t1, Vector2 inward)> KBoundary(List<List<Vector2>> rings, Vector2 a, Vector2 b)
    {
        float len = Vector2.Distance(a, b); var result = new List<(float t0, float t1, Vector2 inward)>();
        if (len < .01f) return result;
        Vector2 d = (b - a) / len, perp = new Vector2(-d.y, d.x);
        var ts = new List<float> { 0, 1 };
        foreach (List<Vector2> ring in rings)
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 p = ring[i], q = ring[(i + 1) % ring.Count], e = q - p;
                float den = d.x * e.y - d.y * e.x;
                if (Mathf.Abs(den) > 1e-9f)
                {
                    float t = ((p.x - a.x) * e.y - (p.y - a.y) * e.x) / den, s = ((p.x - a.x) * d.y - (p.y - a.y) * d.x) / den;
                    if (t > 0 && t < len && s >= -1e-4f && s <= 1 + 1e-4f) ts.Add(t / len);
                }
                foreach (Vector2 v in new[] { p, q })
                {
                    float t = Vector2.Dot(v - a, d);
                    if (t > 0 && t < len && Mathf.Abs(Vector2.Dot(v - a, perp)) < 1e-3f) ts.Add(t / len);
                }
            }
        ts.Sort();
        for (int i = 0; i + 1 < ts.Count; i++)
        {
            if ((ts[i + 1] - ts[i]) * len < .005f) continue;
            Vector2 m = a + (b - a) * ((ts[i] + ts[i + 1]) * .5f);
            bool left = KInside(rings, m + perp * .03f), right = KInside(rings, m - perp * .03f);
            if (left == right) continue;
            Vector2 inward = left ? perp : -perp;
            if (result.Count > 0 && Mathf.Approximately(result[result.Count - 1].t1, ts[i]) && result[result.Count - 1].inward == inward)
                result[result.Count - 1] = (result[result.Count - 1].t0, ts[i + 1], inward);
            else result.Add((ts[i], ts[i + 1], inward));
        }
        return result;
    }
    static List<Vector2> KEarClip(List<Vector2> pts)
    {
        var idx = Enumerable.Range(0, pts.Count).ToList(); var tris = new List<Vector2>(); int guard = 0;
        while (idx.Count > 3)
        {
            if (guard++ > 200000) throw new InvalidOperationException("Kit triangulation did not converge.");
            int n = idx.Count; bool clipped = false;
            for (int k = 0; k < n && !clipped; k++)
            {
                int ia = (k + n - 1) % n, ic = (k + 1) % n;
                Vector2 a = pts[idx[ia]], b = pts[idx[k]], c = pts[idx[ic]];
                if (KOrient(a, b, c) <= 1e-9f) continue;
                bool blocked = false;
                for (int j = 0; j < n && !blocked; j++)
                {
                    if (j == ia || j == k || j == ic) continue;
                    Vector2 q = pts[idx[j]];
                    if (q == a || q == b || q == c) continue;
                    blocked = KOrient(a, b, q) >= 0 && KOrient(b, c, q) >= 0 && KOrient(c, a, q) >= 0;
                }
                if (blocked) continue;
                tris.Add(a); tris.Add(b); tris.Add(c); idx.RemoveAt(k); clipped = true;
            }
            if (clipped) continue;
            int flat = -1; float least = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                float o = Mathf.Abs(KOrient(pts[idx[(k + n - 1) % n]], pts[idx[k]], pts[idx[(k + 1) % n]]));
                if (o < least) { least = o; flat = k; }
            }
            if (least > 1e-5f) throw new InvalidOperationException("Kit polygon could not be triangulated (self-intersecting?).");
            idx.RemoveAt(flat);
        }
        if (idx.Count == 3 && KOrient(pts[idx[0]], pts[idx[1]], pts[idx[2]]) > 1e-9f) { tris.Add(pts[idx[0]]); tris.Add(pts[idx[1]]); tris.Add(pts[idx[2]]); }
        return tris;
    }
    // Horizontal polygon surface (with holes) at height y facing n, metre UVs rotated by rot about uvOrigin, scaled by 1/repeat.
    static void KFill(Batch b, List<Vector2> outer, List<List<Vector2>> holes, float y, Vector3 n, Vector2 uvOrigin, float rotDeg = 0, float repeat = 1)
    {
        float a = rotDeg * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
        Func<Vector2, Vector2> uv = q => { Vector2 d = q - uvOrigin; return new Vector2(d.x * cs + d.y * sn, -d.x * sn + d.y * cs) / repeat; };
        List<Vector2> t = KTriangulate(outer, holes);
        for (int i = 0; i < t.Count; i += 3)
        {
            b.Tri(KP(t[i], y), KP(t[i + 1], y), KP(t[i + 2], y), uv(t[i]), uv(t[i + 1]), uv(t[i + 2]), n);
            kitFillArea += Mathf.Abs(KOrient(t[i], t[i + 1], t[i + 2])) * .5f;
        }
    }

    // Library prop scaled into size (x across its front, y up, z depth) at `at` (local), front facing `facing`.
    static void KitProp(string key, string name, Vector3 at, Vector3 size, Vector3 facing)
    {
        if (!KitModels.TryGetValue(key, out var model)) throw new InvalidOperationException("Unknown kit prop " + key);
        if (!models.ContainsKey(key)) Load(key, model.path, model.child);
        Prop(key, name, at.z, at.x, at.y, size, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg + model.front);
        Transform holder = frame.GetChild(frame.childCount - 1);
        foreach (MeshFilter f in holder.GetComponentsInChildren<MeshFilter>(true))
            if (f.sharedMesh != null) for (int s = 0; s < f.sharedMesh.subMeshCount; s++) kitPropTris += (int)f.sharedMesh.GetIndexCount(s) / 3;
    }

    static void KText(string text, Vector3 c, Vector3 n, float width, float height, Color ink)
    {
        if (string.IsNullOrEmpty(text) || width <= .05f || height <= .02f) return;
        SignText(text, c, n, width, height, ink, Mathf.Min(.22f, width * .12f), Mathf.Min(.12f, height * .15f));
        kitTextTris += 2 * text.Replace("\n", "").Replace(" ", "").Length;
    }

    static int KitTris() { int t = kitPropTris + kitTextTris; foreach (Batch b in batches.Values) t += b.TriangleCount; return t; }

    // Single-sided enclosure surfaces (ceiling tiles/grid/bulkheads, wall cladding and joint backers, fascias, shop walls and
    // ceilings, builder shop ceilings) must block sunlight from either side. Lit materials cast two-sided; materials without a
    // ShadowCaster pass (URP Unlit ceiling panels) get a shadow-only copy with a cull-off caster material.
    static readonly string[] EnclosureBatches = { "Kit_Ceiling_", "Kit_CeilingGrid", "Kit_Bulkhead", "Kit_Clad_", "Kit_Joint", "Kit_Fascia_", "Kit_ShopWall_", "Kit_ShopCeiling_", "ShopCeiling", "Kit_RoomWall_", "Kit_Canopy_", "Kit_Slab_" };
    static bool EnclosureShadow(string batchName) { return EnclosureBatches.Any(k => batchName.Contains(k)); }
    static void CastEnclosureShadow(GameObject go, Mesh mesh, Material mat)
    {
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        if (mat.FindPass("ShadowCaster") >= 0) { r.shadowCastingMode = ShadowCastingMode.TwoSided; return; }
        var caster = new GameObject(go.name + "_ShadowCaster"); caster.transform.SetParent(go.transform, false);
        caster.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer sr = caster.AddComponent<MeshRenderer>();
        sr.sharedMaterial = KShadowMaterial(); sr.shadowCastingMode = ShadowCastingMode.ShadowsOnly; sr.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(caster, StaticEditorFlags.BatchingStatic);
        if (meshFolder == null) caster.hideFlags = HideFlags.HideAndDontSave;
    }
    static Material KShadowMaterial()
    {
        if (kitMats.TryGetValue("Kit_ShadowCaster", out Material m)) return m;
        m = KitCreate("Kit_ShadowCaster", lit, x => { x.SetColor("_BaseColor", Color.black); x.SetFloat("_Cull", 0); x.doubleSidedGI = true; });
        kitMats["Kit_ShadowCaster"] = m;
        return m;
    }
    static int KMod(int i, int m) { return ((i % m) + m) % m; }
    static bool KRectIn(List<List<Vector2>> rings, float ca, float cb, float ha, float hb)
    {
        for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) if (!KInside(rings, new Vector2(ca + i * ha, cb + j * hb))) return false;
        return true;
    }
    // Solid runs of [0,len] left by the gaps (x = start, y = end).
    static List<Vector2> KRuns(float len, List<Vector2> gaps)
    {
        var runs = new List<Vector2>(); float at = 0;
        foreach (Vector2 g in gaps.OrderBy(q => q.x)) { if (g.x - at > .005f) runs.Add(new Vector2(at, g.x)); at = Mathf.Max(at, g.y); }
        if (len - at > .005f) runs.Add(new Vector2(at, len));
        return runs;
    }

    // ---- dispatcher ----
    static void KitElement(JObject e)
    {
        string type = (string)e["type"], id = (string)e["id"], tier = (string)e["tier"];
        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id)) throw new InvalidOperationException("Kit element needs type and id: " + e.ToString(Newtonsoft.Json.Formatting.None));
        bool buildable = tier == "a" || tier == "a-failed" || tier == "b" || (kitPreviewing && tier == "preview");
        if (!buildable) { kitReport.Add(new { id, type, tier, zone = (string)e["zone"], built = false, reason = "tier " + (tier ?? "missing") + " is never built" }); return; }
        JObject g = KO(e, "geometry"), p = KO(e, "params");
        bool box = (string)e["frame"] == "box";
        if (box && boxFrame == null) throw new InvalidOperationException(id + ": box frame unavailable.");
        if (box) EnterBox();
        int before = KitTris(), lightsBefore = kitLights; float areaBefore = kitFillArea;
        try
        {
            switch (type)
            {
                case "SuspendedCeiling": KitCeiling(id, g, p); break;
                case "Column": KitColumn(id, g, p); break;
                case "WallCladding": KitWall(id, g, p); break;
                case "Storefront": KitStorefront(id, g, p); break;
                case "ServiceDoor": KitServiceDoor(id, g, p); break;
                case "HangingSign": KitSign(id, g, p, true); break;
                case "WallSign": KitSign(id, g, p, false); break;
                case "FloorFinish": KitFloor(id, g, p); break;
                case "ShopInterior": KitShop(id, g, p); break;
                case "Escalator": KitEscalator(id, g, p); break;
                case "ElevatorFront": KitElevator(id, g, p); break;
                case "Gate": KitGate(id, g, p); break;
                case "Stair": KitStair(id, g, p); break;
                case "LockerBank": KitLockers(id, g, p); break;
                case "VendingMachine": KitVending(id, g, p); break;
                case "Bench": KitBench(id, g, p); break;
                case "Lighting": KitLighting(id, g, p); break;
                case "Balustrade": KitBalustrade(id, g, p); break;
                case "DisplayBoard": KitDisplayBoard(id, g, p); break;
                case "Counter": KitCounter(id, g, p); break;
                case "DoorSet": KitDoorSet(id, g, p); break;
                case "Fixture": KitFixture(id, g, p); break;
                case "Slab": KitSlab(id, g, p); break;
                case "Room": KitRoom(id, g, p); break;
                case "ToiletRoom": KitToiletRoom(id, g, p); break;
                case "Window": KitWindow(id, g, p); break;
                case "Canopy": KitCanopy(id, g, p); break;
                default: throw new InvalidOperationException("Unknown kit element type '" + type + "' (" + id + ").");
            }
        }
        finally { if (box) ExitBox(); }
        kitReport.Add(new { id, type, tier, zone = (string)e["zone"], built = true, triangles = KitTris() - before, lights = kitLights - lightsBefore, surfaceArea = Math.Round(kitFillArea - areaBefore, 2) });
    }

    // Integration entry (Main): builds every element of a zone spec under a freshly created root path, replacing a previous
    // build of that path only after success. Meshes -> Kit/<zone>/, receipt -> <spec>.receipt.json. No scene save.
    // args[0] = spec JSON ({"zone","elements":[...]} or [...]); args[1] = root path "Parent/Child" (parent must exist) or "Root".
    public static void KitBuild(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run KitBuild in Edit mode.");
        if (args == null || args.Length < 2) throw new ArgumentException("KitBuild <spec.json> <root path>");
        JToken spec = JToken.Parse(File.ReadAllText(args[0]));
        JArray elements = spec as JArray ?? spec["elements"] as JArray;
        if (elements == null) throw new InvalidOperationException("Kit spec has no elements array: " + args[0]);
        string zone = (string)(spec as JObject)?["zone"] ?? Path.GetFileNameWithoutExtension(args[0]);
        string rootPath = args[1].Trim('/'); int slash = rootPath.LastIndexOf('/');
        string leaf = slash < 0 ? rootPath : rootPath.Substring(slash + 1);
        Transform parent = null, previous;
        if (slash >= 0)
        {
            GameObject pg = GameObject.Find("/" + rootPath.Substring(0, slash));
            if (pg == null) throw new InvalidOperationException("KitBuild parent not found (must be active): " + rootPath.Substring(0, slash));
            parent = pg.transform;
            if ((parent.lossyScale - Vector3.one).sqrMagnitude > 1e-6f) throw new InvalidOperationException("KitBuild parent is scaled: " + parent.name);
            previous = parent.Find(leaf);
        }
        else previous = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == leaf)?.transform;
        string receiptPath = Path.Combine(Path.GetDirectoryName(args[0]), Path.GetFileNameWithoutExtension(args[0]) + ".receipt.json");
        string folder = Kit + "/" + KitSafe(zone);
        // One kit root per mesh folder: batches are named by material, so two roots sharing a spec zone would overwrite each
        // other's meshes (including Kit_Colliders). The registry maps folder -> owning root path.
        JObject folderOwners = File.Exists(KitFolderRegistry) ? JObject.Parse(File.ReadAllText(KitFolderRegistry)) : new JObject();
        string folderOwner = (string)folderOwners[folder];
        if (folderOwner != null && folderOwner != rootPath)
            throw new InvalidOperationException("Kit mesh folder " + folder + " belongs to " + folderOwner + "; give the spec for " + rootPath + " a distinct zone (now '" + zone + "').");
        KitInit(); EnsureFolder(folder);
        Transform savedRoot = root, savedBox = boxFrame;
        GameObject built = new GameObject(leaf);
        var meshes = new List<string>();
        try
        {
            root = built.transform;
            if (parent != null) root.SetParent(parent, false);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, Theta, 0));
            frame = root; inBox = false; boxFrame = null; batches.Clear(); batchPrefix = ""; meshFolder = folder;
            if (elements.Any(t => (string)t["frame"] == "box"))
            {
                JObject bf = (JObject)JObject.Parse(File.ReadAllText(PlanPath))["boxFrame"];
                boxFrame = new GameObject("Box frame").transform; boxFrame.SetParent(root, false);
                boxFrame.localPosition = P((float)bf["origin"][0], (float)bf["origin"][1], 0);
                boxFrame.localRotation = Quaternion.Euler(0, (float)bf["rotDeg"], 0);
                boxEastB = (float)bf["eastB"];
            }
            foreach (JToken e in elements) KitElement((JObject)e);
            foreach (Batch b in batches.Values) b.Save();
            meshes = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Concat(root.GetComponentsInChildren<MeshCollider>(true).Select(c => c.sharedMesh))
                .Select(m => AssetDatabase.GetAssetPath(m)).Where(s => s.StartsWith(folder + "/", StringComparison.Ordinal)).Distinct().OrderBy(s => s).ToList();
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(built);
            throw;
        }
        finally { meshFolder = Art; batches.Clear(); frame = savedRoot; root = savedRoot; boxFrame = savedBox; inBox = false; }
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        if (File.Exists(receiptPath) && JObject.Parse(File.ReadAllText(receiptPath))["meshes"] is JArray old)
            foreach (string stale in old.Select(t => (string)t).Where(s => s.StartsWith(folder + "/", StringComparison.Ordinal) && !meshes.Contains(s))) AssetDatabase.DeleteAsset(stale);
        BindWorldText();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(built.scene);
        var materials = kitMats.Values.Select(m => AssetDatabase.GetAssetPath(m)).Distinct().OrderBy(s => s).ToArray();
        File.WriteAllText(receiptPath, Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            schema = "chooguard.interior-kit-build.v1", spec = args[0], root = rootPath, zone, builtUtc = DateTime.UtcNow.ToString("o"),
            triangles = kitReport.Sum(r => (int)(r.GetType().GetProperty("triangles")?.GetValue(r) ?? 0)), lights = kitLights,
            elements = kitReport, meshes, materials, note = "Positions come only from the spec; tier c / missing-tier elements are skipped. Scene not saved."
        }, Newtonsoft.Json.Formatting.Indented));
        folderOwners[folder] = rootPath;
        File.WriteAllText(KitFolderRegistry, folderOwners.ToString(Newtonsoft.Json.Formatting.Indented));
        kitMats.Clear();
        Debug.Log("KIT_BUILT " + rootPath + " elements=" + kitReport.Count + " meshes=" + meshes.Count + " receipt=" + receiptPath);
    }

    // Dry build of a zone spec (same KitElement path, real frames/positions) under a HideAndDontSave root with in-memory
    // materials only: no AssetDatabase writes, no scene or receipt changes. Per-element exceptions are caught and reported.
    // args[0] = spec JSON; writes <spec>.validate.json {elements:[{id,type,tier,ok,built,error,triangles}], errors}.
    public static void KitValidate(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run KitValidate in Edit mode.");
        if (args == null || args.Length < 1) throw new ArgumentException("KitValidate <spec.json>");
        JToken spec = JToken.Parse(File.ReadAllText(args[0]));
        JArray elements = spec as JArray ?? spec["elements"] as JArray;
        if (elements == null) throw new InvalidOperationException("Kit spec has no elements array: " + args[0]);
        string outPath = Path.Combine(Path.GetDirectoryName(args[0]), Path.GetFileNameWithoutExtension(args[0]) + ".validate.json");
        Transform savedRoot = root, savedFrame = frame, savedBox = boxFrame; bool savedIn = inBox; string savedPrefix = batchPrefix, savedFolder = meshFolder;
        GameObject temp = null; var results = new List<object>(); int errors = 0;
        // Read-only mesh-folder ownership check (same registry as KitBuild): root = args[1] or the spec's top-level "root".
        string zone = (string)(spec as JObject)?["zone"] ?? Path.GetFileNameWithoutExtension(args[0]), folder = Kit + "/" + KitSafe(zone);
        string rootArg = args.Length > 1 ? args[1].Trim('/') : ((string)(spec as JObject)?["root"])?.Trim('/');
        string folderOwner = File.Exists(KitFolderRegistry) ? (string)JObject.Parse(File.ReadAllText(KitFolderRegistry))[folder] : null;
        string folderConflict = folderOwner != null && rootArg != null && folderOwner != rootArg
            ? "Kit mesh folder " + folder + " belongs to " + folderOwner + "; KitBuild into " + rootArg + " would throw (give the spec a distinct zone)." : null;
        if (folderConflict != null) errors++;
        try
        {
            KitInit(); kitPreviewing = false; meshFolder = null; batches.Clear(); batchPrefix = "";
            temp = new GameObject("Kit validate (temporary)") { hideFlags = HideFlags.HideAndDontSave };
            temp.transform.rotation = Quaternion.Euler(0, Theta, 0);
            root = frame = temp.transform; inBox = false; boxFrame = null;
            if (elements.Any(t => (string)t["frame"] == "box"))
            {
                JObject bf = (JObject)JObject.Parse(File.ReadAllText(PlanPath))["boxFrame"];
                boxFrame = new GameObject("Box frame").transform; boxFrame.SetParent(root, false);
                boxFrame.localPosition = P((float)bf["origin"][0], (float)bf["origin"][1], 0);
                boxFrame.localRotation = Quaternion.Euler(0, (float)bf["rotDeg"], 0);
                boxEastB = (float)bf["eastB"];
            }
            var ids = new HashSet<string>();
            foreach (JToken t in elements)
            {
                JObject e = t as JObject;
                string id = (string)e?["id"], type = (string)e?["type"], tier = (string)e?["tier"];
                int before = kitReport.Count;
                try
                {
                    if (e == null) throw new InvalidOperationException("Element is not an object.");
                    if (id != null && !ids.Add(id)) throw new InvalidOperationException("Duplicate element id " + id + ".");
                    KitElement(e);
                    object rep = kitReport.Count > before ? kitReport[kitReport.Count - 1] : null;
                    bool built = rep != null && (bool)rep.GetType().GetProperty("built").GetValue(rep);
                    int tris = rep != null ? (int)(rep.GetType().GetProperty("triangles")?.GetValue(rep) ?? 0) : 0;
                    results.Add(new { id, type, tier, ok = true, built, error = (string)null, triangles = tris });
                }
                catch (Exception ex)
                {
                    errors++; inBox = false; frame = root;
                    results.Add(new { id, type, tier, ok = false, built = false, error = ex.GetType().Name + ": " + ex.Message, triangles = 0 });
                }
            }
        }
        finally
        {
            if (temp != null) UnityEngine.Object.DestroyImmediate(temp);
            foreach (UnityEngine.Object o in kitTransient) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            kitTransient.Clear(); batches.Clear(); kitMats.Clear();
            meshFolder = savedFolder; root = savedRoot; frame = savedFrame; boxFrame = savedBox; inBox = savedIn; batchPrefix = savedPrefix;
        }
        File.WriteAllText(outPath, Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            schema = "chooguard.interior-kit-validate.v1", spec = args[0], validatedUtc = DateTime.UtcNow.ToString("o"), zone, folder, root = rootArg, folderOwner, folderConflict, elements = results, errors,
            note = "Dry build under a temporary HideAndDontSave root (in-memory meshes/materials); no scene, asset or receipt writes."
        }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("KIT_VALIDATE " + args[0] + " elements=" + results.Count + " errors=" + errors + " -> " + outPath);
    }

    // Persists every kit-owned material definition to Kit/Materials (asset writes only; no scene access).
    public static void KitMaterials(string[] args)
    {
        KitInit(); meshFolder = Kit;
        try { foreach (string k in KitDefs.Keys.Concat(KitDerived.Keys)) KM(k); AssetDatabase.SaveAssets(); }
        finally { meshFolder = Art; kitMats.Clear(); }
    }

    // ---- SuspendedCeiling ----
    static void KitCeiling(string id, JObject g, JObject p)
    {
        float y = KN(g, "y", id);
        List<Vector2> outer = KPoly(KT(g, "polygon", id)); List<List<Vector2>> holes = KHoles(g);
        bool linear = KS(p, "module", "600x600") == "1200x300";
        float ma = linear ? 1.2f : .6f, mb = linear ? .3f : .6f;
        string finish = KS(p, "finish", linear ? "ceilingPan" : "ceiling600");
        Material tile = KM(finish);
        bool office = finish == "ceiling600" || tile.name == "PBR_OfficeCeiling003_2K";
        float repeat = KF(p, "uvRepeat", office ? 4.8f : 1f), angDeg = KF(p, "gridAngle", 0), ang = angDeg * Mathf.Deg2Rad;
        Vector2 ea = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)), eb = new Vector2(-ea.y, ea.x);
        Vector2 origin = p["gridOrigin"] is JArray go ? KV(go) : outer[0];
        Func<Vector2, Vector2> toG = q => new Vector2(Vector2.Dot(q - origin, ea), Vector2.Dot(q - origin, eb));
        Func<float, float, Vector2> toUV = (a, b) => origin + ea * a + eb * b;
        var rings = new List<List<Vector2>> { outer.Select(toG).ToList() }; rings.AddRange(holes.Select(h => h.Select(toG).ToList()));
        var swapped = rings.Select(r => r.Select(q => new Vector2(q.y, q.x)).ToList()).ToList();
        float minA = rings[0].Min(q => q.x), maxA = rings[0].Max(q => q.x), minB = rings[0].Min(q => q.y), maxB = rings[0].Max(q => q.y);
        Vector3 A = KD(ea), Bv = KD(eb), down = Vector3.down;
        // Tiles: one surface whose UVs are locked to the module grid, so texture tile edges coincide with the T-bars.
        KFill(B("Kit_Ceiling_" + tile.name, tile), outer, holes, y, down, origin, angDeg, repeat);
        Batch white = B("Kit_CeilingGrid", KM("Kit_GridWhite"));
        if (KB(p, "grid", true))
        {
            Batch tb = linear ? B("Kit_Joint", dark) : white;
            float w = linear ? .008f : .024f, drop = linear ? .002f : .012f;
            for (int k = Mathf.CeilToInt(minB / mb); k * mb <= maxB; k++) foreach (Vector2 iv in KIntervals(rings, k * mb)) KTBar(tb, KP(toUV(iv.x, k * mb), y), KP(toUV(iv.y, k * mb), y), w, drop);
            for (int k = Mathf.CeilToInt(minA / ma); k * ma <= maxA; k++) foreach (Vector2 iv in KIntervals(swapped, k * ma)) KTBar(tb, KP(toUV(k * ma, iv.x), y), KP(toUV(k * ma, iv.y), y), w, drop);
        }
        JObject fx = KO(p, "fixtures"); string layout = KS(fx, "layout", linear ? "linear" : "grid");
        JArray pitch = fx["pitch"] as JArray, size = fx["size"] as JArray;
        float pa = pitch != null ? (float)pitch[0] : 2.4f, pb = pitch != null ? (float)pitch[1] : 2.4f;
        if (layout == "grid" || layout == "downlight")
        {
            int sa = Mathf.Max(1, Mathf.RoundToInt(pa / ma)), sb = Mathf.Max(1, Mathf.RoundToInt(pb / mb));
            float fa = size != null ? (float)size[0] : layout == "grid" ? ma : .2f, fb = size != null ? (float)size[size.Count > 1 ? 1 : 0] : layout == "grid" ? mb : fa;
            // The OfficeCeiling003 colour map bakes louvre troffers at module cells a=3, b=0 (mod 4) of its 4.8 m repeat:
            // the default phase puts the modelled LED panels exactly on them.
            JArray phase = fx["phase"] as JArray;
            int qa = phase != null ? (int)phase[0] : office && Mathf.Approximately(repeat, 4.8f) ? 3 % sa : sa / 2;
            int qb = phase != null ? (int)phase[1] : office && Mathf.Approximately(repeat, 4.8f) ? 0 : sb / 2;
            for (int i = Mathf.FloorToInt(minA / ma); i * ma < maxA; i++)
            {
                if (KMod(i, sa) != KMod(qa, sa)) continue;
                for (int j = Mathf.FloorToInt(minB / mb); j * mb < maxB; j++)
                {
                    if (KMod(j, sb) != KMod(qb, sb)) continue;
                    float ca = (i + .5f) * ma, cb = (j + .5f) * mb;
                    if (!KRectIn(rings, ca, cb, fa * .5f + .03f, fb * .5f + .03f)) continue;
                    Vector3 c = KP(toUV(ca, cb), y);
                    if (layout == "grid") KLedPanel(c, A, Bv, fa, fb); else KDownlight(c, fa * .5f);
                }
            }
        }
        else if (layout == "linear")
        {
            int sb = Mathf.Max(1, Mathf.RoundToInt(pb / mb)); float width = size != null ? (float)size[size.Count > 1 ? 1 : 0] : .08f;
            for (int j = Mathf.FloorToInt(minB / mb); j * mb < maxB; j++)
            {
                if (KMod(j, sb) != sb / 2) continue;
                float cb = (j + .5f) * mb;
                foreach (Vector2 iv in KIntervals(rings, cb)) if (iv.y - iv.x > 1.2f) KLinearLight(KP(toUV(iv.x + .3f, cb), y), A, Bv, iv.y - iv.x - .6f, width);
            }
        }
        else if (layout != "none") throw new InvalidOperationException(id + ": unknown fixtures.layout " + layout);
        // Optional sparse real lights (no shadows, mixed) on their own grid, for spaces without light from elsewhere.
        if (fx["lights"] is JObject lights)
        {
            JArray lp = lights["pitch"] as JArray; float la = lp != null ? (float)lp[0] : 6f, lb = lp != null ? (float)lp[1] : 6f;
            for (int i = Mathf.FloorToInt(minA / la); i * la < maxA; i++)
                for (int j = Mathf.FloorToInt(minB / lb); j * lb < maxB; j++)
                    if (KInside(rings, new Vector2((i + .5f) * la, (j + .5f) * lb)))
                        KLight(id + " light " + i + "," + j, KP(toUV((i + .5f) * la, (j + .5f) * lb), y - KF(lights, "drop", 1.2f)), lights);
        }
        // Perimeter/hole trim flange and plasterboard bulkheads (outer: bulkhead, holes: holeBulkhead) facing out of the ceiling, only
        // along real boundaries of the filled region. trimEdges/bulkheadEdges: "all" (default) | "outer" | "holes" | "none" | [outer edge
        // indices] (+ all holes); edge i runs polygon[i] -> polygon[i+1]. Lets tiled ceilings suppress internal seams.
        float bulk = KF(p, "bulkhead", 0), holeBulk = KF(p, "holeBulkhead", .3f);
        Func<string, int, bool> pick = (key, code) =>
        {
            JToken sel = p[key];
            if (sel is JArray list) return code >= 1 || list.Any(t => (int)t == -code - 1);
            string mode = sel == null || sel.Type == JTokenType.Null ? "all" : (string)sel;
            if (mode == "all") return true;
            if (mode == "none") return false;
            if (mode == "outer") return code < 0;
            if (mode == "holes") return code >= 1;
            throw new InvalidOperationException(id + ": bad " + key + " '" + mode + "'");
        };
        bool trim = KB(p, "trim", true);
        Batch bulkB = B("Kit_Bulkhead", KM("gypsum"));
        var uvRings = new List<List<Vector2>> { outer }; uvRings.AddRange(holes);
        for (int r = 0; r < uvRings.Count; r++)
        {
            List<Vector2> ring = uvRings[r]; float bh = r == 0 ? bulk : holeBulk;
            for (int i = 0; i < ring.Count; i++)
            {
                // Selector code: outer edge i -> -(i+1); hole edges -> ring index (>= 1).
                int code = r == 0 ? -(i + 1) : r;
                bool doTrim = trim && pick("trimEdges", code), doBulk = bh > 0 && pick("bulkheadEdges", code);
                if (!doTrim && !doBulk) continue;
                Vector2 a2 = ring[i], b2 = ring[(i + 1) % ring.Count];
                foreach (var piece in KBoundary(uvRings, a2, b2))
                {
                    Vector2 s2 = Vector2.Lerp(a2, b2, piece.t0), e2 = Vector2.Lerp(a2, b2, piece.t1);
                    float len = Vector2.Distance(s2, e2);
                    Vector3 a = KP(s2, y), ex = KD((e2 - s2) / len), inward = KD(piece.inward);
                    if (doTrim) KRect(white, a + down * .004f, ex, inward, len, .03f, down, Vector2.zero);
                    if (doBulk) KRect(bulkB, a, ex, Vector3.up, len, bh, -inward, new Vector2(0, y));
                }
            }
        }
    }
    // Ceiling grid member (T-bar or pan joint) hanging `drop` below the tile plane along a->c.
    static void KTBar(Batch b, Vector3 a, Vector3 c, float w, float drop)
    {
        float len = Vector3.Distance(a, c); if (len < .005f) return;
        Vector3 dir = (c - a) / len, side = Vector3.Cross(Vector3.up, dir), dn = Vector3.down * drop;
        KRect(b, a - side * w * .5f + dn, dir, side, len, w, Vector3.down, Vector2.zero);
        KRect(b, a - side * w * .5f + dn, dir, Vector3.up, len, drop, -side, Vector2.zero);
        KRect(b, a + side * w * .5f + dn, dir, Vector3.up, len, drop, side, Vector2.zero);
    }
    // Recessed LED flat panel: emissive lens inside a 30 mm white frame, facing down.
    static void KLedPanel(Vector3 c, Vector3 A, Vector3 Bv, float fa, float fb)
    {
        Material lens = KM("ledPanel"); Batch l = B("Kit_Light_" + lens.name, lens), f = B("Kit_CeilingGrid", KM("Kit_GridWhite"));
        Vector3 d = Vector3.down, cc = c + d * .008f;
        KRectC(l, c + d * .006f, A, Bv, fa - .06f, fb - .06f, d);
        KRectC(f, cc + Bv * (fb * .5f - .015f), A, Bv, fa, .03f, d); KRectC(f, cc - Bv * (fb * .5f - .015f), A, Bv, fa, .03f, d);
        KRectC(f, cc + A * (fa * .5f - .015f), A, Bv, .03f, fb - .06f, d); KRectC(f, cc - A * (fa * .5f - .015f), A, Bv, .03f, fb - .06f, d);
    }
    static void KDownlight(Vector3 c, float r)
    {
        Material lens = KM("downlight");
        KRingH(B("Kit_CeilingGrid", KM("Kit_GridWhite")), c + Vector3.down * .006f, r, r + .025f, 20, Vector3.down);
        KRingH(B("Kit_Light_" + lens.name, lens), c + Vector3.down * .004f, 0, r, 20, Vector3.down);
    }
    // Continuous linear slot light from `start` along A.
    static void KLinearLight(Vector3 start, Vector3 A, Vector3 Bv, float len, float w)
    {
        Material lens = KM("linearLight"); Batch l = B("Kit_Light_" + lens.name, lens), f = B("Kit_CeilingGrid", KM("Kit_GridWhite"));
        Vector3 d = Vector3.down;
        KRect(l, start + d * .005f - Bv * w * .5f, A, Bv, len, w, d, Vector2.zero);
        KRect(f, start + d * .007f - Bv * (w * .5f + .015f), A, Bv, len, .015f, d, Vector2.zero);
        KRect(f, start + d * .007f + Bv * w * .5f, A, Bv, len, .015f, d, Vector2.zero);
    }

    // Real URP light (point/spot) at a local position: no shadows and Mixed by default (bake-friendly, realtime until baked).
    static void KLight(string name, Vector3 at, JObject lp)
    {
        var go = new GameObject(name); go.transform.SetParent(frame, false); go.transform.localPosition = at;
        Light l = go.AddComponent<Light>();
        l.type = KS(lp, "kind", "point") == "spot" ? LightType.Spot : LightType.Point;
        if (l.type == LightType.Spot) { go.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); l.spotAngle = KF(lp, "spotAngle", 90); }
        l.range = KF(lp, "range", 8); l.intensity = KF(lp, "intensity", 1.2f); l.color = Mathf.CorrelatedColorTemperatureToRGB(KF(lp, "kelvin", 4000));
        l.shadows = KS(lp, "shadows", "none") == "soft" ? LightShadows.Soft : LightShadows.None;
        string mode = KS(lp, "mode", "mixed");
        l.lightmapBakeType = mode == "baked" ? LightmapBakeType.Baked : mode == "realtime" ? LightmapBakeType.Realtime : LightmapBakeType.Mixed;
        kitLights++;
    }

    // Clad face: panels on a pw x ph grid (columns anchored at s0 along the run, rows from the face bottom) with open joints over
    // a dark backer; openings (x = start, y = end along ex, z = top, w = bottom, both above the face bottom) are left bare.
    // pw/ph <= 0: continuous.
    static void KPanels(Batch skin, Batch backer, Vector3 o, Vector3 ex, Vector3 ey, Vector3 n, float w, float h, float pw, float ph, float joint, float s0, float y0, List<Vector4> openings)
    {
        if (w <= .01f || h <= .01f) return;
        var xs = new List<float> { 0, w }; var ys = new List<float> { 0, h };
        if (pw > 0) for (int k = Mathf.CeilToInt(s0 / pw + 1e-4f); k * pw - s0 < w - 1e-3f; k++) if (k * pw - s0 > 1e-3f) xs.Add(k * pw - s0);
        if (ph > 0) for (int k = 1; k * ph < h - 1e-3f; k++) ys.Add(k * ph);
        if (openings != null) foreach (Vector4 op in openings)
        {
            if (op.x > 1e-3f && op.x < w - 1e-3f) xs.Add(op.x);
            if (op.y > 1e-3f && op.y < w - 1e-3f) xs.Add(op.y);
            if (op.z > 1e-3f && op.z < h - 1e-3f) ys.Add(op.z);
            if (op.w > 1e-3f && op.w < h - 1e-3f) ys.Add(op.w);
        }
        xs = xs.OrderBy(v => v).Aggregate(new List<float>(), (l, v) => { if (l.Count == 0 || v - l[l.Count - 1] > .005f) l.Add(v); return l; });
        ys = ys.OrderBy(v => v).Aggregate(new List<float>(), (l, v) => { if (l.Count == 0 || v - l[l.Count - 1] > .005f) l.Add(v); return l; });
        float gap = Mathf.Max(0, joint) * .5f, depth = .01f;
        for (int i = 0; i + 1 < xs.Count; i++)
            for (int j = 0; j + 1 < ys.Count; j++)
            {
                float xa = xs[i], xb = xs[i + 1], ya = ys[j], yb = ys[j + 1], xm = (xa + xb) * .5f, ym = (ya + yb) * .5f;
                if (openings != null && openings.Any(op => xm > op.x && xm < op.y && ym < op.z && ym > op.w)) continue;
                float pw2 = xb - xa - 2 * gap, ph2 = yb - ya - 2 * gap;
                if (pw2 <= .002f || ph2 <= .002f) continue;
                Vector3 p0 = o + ex * (xa + gap) + ey * (ya + gap);
                KRect(skin, p0, ex, ey, pw2, ph2, n, new Vector2(s0 + xa + gap, y0 + ya + gap));
                if (gap <= 0) continue;
                Vector3 back = -n * depth;
                KRect(skin, p0 + back, ex, n, pw2, depth, -ey, Vector2.zero); KRect(skin, p0 + ey * ph2 + back, ex, n, pw2, depth, ey, Vector2.zero);
                KRect(skin, p0 + back, ey, n, ph2, depth, -ex, Vector2.zero); KRect(skin, p0 + ex * pw2 + back, ey, n, ph2, depth, ex, Vector2.zero);
                KRect(backer, o + ex * xa + ey * ya - n * (depth + .002f), ex, ey, xb - xa, yb - ya, n, Vector2.zero);
            }
    }

    // ---- Column ----
    static void KitColumn(string id, JObject g, JObject p)
    {
        Vector2 at = KV(KT(g, "point", id)); float y0 = KN(g, "y0", id), y1 = KN(g, "y1", id);
        string clad = KS(p, "cladding", "stainless");
        Material skin = KM(clad == "plaster" ? "gypsum" : clad), trimM = KM("trim");
        float baseH = KF(p, "base", .12f), capH = KF(p, "cap", .08f), ph = KF(p, "panelHeight", clad == "granite" ? .9f : 1.2f);
        Batch sk = B("Kit_Column_" + skin.name, skin), jt = B("Kit_Joint", dark), tr = B("Kit_Trim_" + trimM.name, trimM);
        Vector3 axis = KP(at, 0), up = Vector3.up;
        float c0 = y0 + baseH, c1 = y1 - capH;
        if (KS(p, "shape", "round") == "round")
        {
            float r = KF(p, "diameter", .9f) * .5f; const int sides = 32;
            KCyl(sk, axis, r, c0, c1, sides);
            for (int k = 1; c0 + k * ph < c1 - .05f; k++) KCyl(jt, axis, r + .0015f, c0 + k * ph - .004f, c0 + k * ph + .004f, sides);
            int vj = KI(p, "joints", 4);
            for (int k = 0; k < vj; k++)
            {
                float t = (k + .5f) * Mathf.PI * 2 / vj; Vector3 d = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)), tg = new Vector3(-d.z, 0, d.x);
                KRect(jt, axis + d * (r + .0015f) - tg * .004f + up * c0, tg, up, .008f, c1 - c0, d, Vector2.zero);
            }
            KCyl(tr, axis, r + .015f, y0, c0, sides); KRingH(tr, axis + up * c0, r, r + .015f, sides, up);
            KCyl(tr, axis, r + .02f, c1, y1, sides); KRingH(tr, axis + up * c1, r, r + .02f, sides, Vector3.down);
            KCyl(KCol(), axis, r + .02f, y0, y1, 12);
            return;
        }
        JArray sz = p["size"] as JArray; float sa = sz != null ? (float)sz[0] : .9f, sb = sz != null ? (float)sz[1] : .9f;
        float ang = KF(p, "angle", 0) * Mathf.Deg2Rad, pw = KF(p, "panelWidth", 0);
        Vector3 ax = KD(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang))), bz = KD(new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang))), mid = axis + up * (y0 + y1) * .5f;
        for (int f = 0; f < 4; f++)
        {
            Vector3 n = f == 0 ? bz : f == 1 ? -bz : f == 2 ? ax : -ax, ex = Vector3.Cross(Vector3.up, n);
            float w = f < 2 ? sa : sb, half = f < 2 ? sb * .5f : sa * .5f;
            Vector3 o = axis + n * half - ex * w * .5f + up * c0;
            KPanels(sk, jt, o, ex, up, n, w, c1 - c0, pw > 0 ? pw : w + 1, ph, .008f, 0, c0, null);
        }
        float guard = KF(p, "cornerGuard", clad == "panel" || clad == "plaster" ? 1.5f : 0);
        if (guard > 0)
            for (int cx = -1; cx <= 1; cx += 2) for (int cz = -1; cz <= 1; cz += 2)
            {
                Vector3 corner = axis + ax * cx * (sa * .5f + .002f) + bz * cz * (sb * .5f + .002f) + up * c0;
                KRect(tr, corner, -ax * cx, up, .05f, guard, bz * cz, Vector2.zero); KRect(tr, corner, -bz * cz, up, .05f, guard, ax * cx, Vector2.zero);
            }
        KBox(tr, axis + up * (y0 + baseH * .5f), ax, up, bz, new Vector3(sa + .03f, baseH, sb + .03f), false);
        KBox(tr, axis + up * (y1 - capH * .5f), ax, up, bz, new Vector3(sa + .04f, capH, sb + .04f));
        KColBox(mid, ax, up, bz, new Vector3(sa + .03f, y1 - y0, sb + .03f));
    }

    // ---- WallCladding ----
    static void KitWall(string id, JObject g, JObject p)
    {
        List<Vector2> line = KPoly(KT(g, "polyline", id)); float y0 = KN(g, "y0", id), y1 = KN(g, "y1", id);
        Material skin = KM(KS(p, "finish", "stone")), trimM = KM("trim");
        float pw = KF(p, "panelWidth", 1.2f), ph = KF(p, "panelHeight", .6f), joint = KF(p, "joint", .008f), off = KF(p, "standoff", .03f);
        float skirt = KF(p, "skirting", .12f), cap = KF(p, "cap", .04f); bool collide = KB(p, "collider", true);
        // openings [s0, s1, yTop] (doorway from the floor) or [s0, s1, yTop, yBottom] (window / hatch with a sill), heights absolute.
        var openings = new List<Vector4>();
        if (p["openings"] is JArray ops) foreach (JToken op in ops) openings.Add(new Vector4((float)op[0], (float)op[1], (float)op[2], ((JArray)op).Count > 3 ? (float)op[3] : float.NegativeInfinity));
        Batch sk = B("Kit_Clad_" + skin.name, skin), jt = B("Kit_Joint", dark), tr = B("Kit_Trim_" + trimM.name, trimM);
        Vector3 up = Vector3.up; float s = 0, panelBase = y0 + skirt;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a2 = line[i], b2 = line[i + 1]; float len = Vector2.Distance(a2, b2);
            if (len < .01f) continue;
            Vector3 n = KEdgeNormal(a2, b2, p), ex = (KP(b2, 0) - KP(a2, 0)).normalized, wallA = KP(a2, y0), o = wallA + n * off;
            var local = new List<Vector4>(); var gaps = new List<Vector2>();
            foreach (Vector4 op in openings)
            {
                float a = Mathf.Max(op.x - s, 0), b = Mathf.Min(op.y - s, len);
                if (b - a <= .01f) continue;
                bool doorway = op.w <= y0 + .01f;
                local.Add(new Vector4(a, b, op.z - panelBase, doorway ? float.NegativeInfinity : op.w - panelBase));
                if (doorway) gaps.Add(new Vector2(a, b));
            }
            KPanels(sk, jt, o + up * skirt, ex, up, n, len, (y1 - cap) - panelBase, pw, ph, joint, s, panelBase, local);
            foreach (Vector2 run in KRuns(len, gaps))
            {
                if (skirt > 0) KBox(tr, o + ex * ((run.x + run.y) * .5f) + up * skirt * .5f + n * .006f, ex, up, n, new Vector3(run.y - run.x, skirt, .012f), false, false);
            }
            if (collide)
            {
                // Full-height runs between openings, then the head (and sill) pieces of each opening.
                var allGaps = local.Select(op => new Vector2(op.x, op.y)).ToList();
                foreach (Vector2 run in KRuns(len, allGaps)) KColRect(wallA + ex * run.x, ex, up, run.y - run.x, y1 - y0);
                foreach (Vector4 op in local)
                {
                    KColRect(wallA + ex * op.x + up * (op.z + skirt), ex, up, op.y - op.x, y1 - (panelBase + op.z));
                    if (!float.IsNegativeInfinity(op.w)) KColRect(wallA + ex * op.x, ex, up, op.y - op.x, op.w + skirt);
                }
            }
            if (cap > 0) KBox(tr, o + ex * len * .5f + up * (y1 - cap * .5f) + n * .006f, ex, up, n, new Vector3(len, cap, .012f), true, false);
            s += len;
        }
    }

    static Color KC(JObject o, string k, Color d) { string s = KS(o, k, null); return s != null && ColorUtility.TryParseHtmlString(s, out Color c) ? c : d; }
    static void KGlass(Batch b, Vector3 o, Vector3 ex, Vector3 ey, float w, float h, Vector3 n)
    {
        if (w <= .005f || h <= .005f) return;
        KRect(b, o, ex, ey, w, h, n, Vector2.zero); KRect(b, o, ex, ey, w, h, -n, Vector2.zero);
    }

    // ---- Storefront ----
    static void KitStorefront(string id, JObject g, JObject p)
    {
        JToken edge = KT(g, "edge", id); Vector2 a2 = KV(edge[0]), b2 = KV(edge[1]); float y = KN(g, "y", id), L = Vector2.Distance(a2, b2);
        Vector3 n = KEdgeNormal(a2, b2, p), ex = (KP(b2, 0) - KP(a2, 0)).normalized, up = Vector3.up, A = KP(a2, y);
        float H = KF(p, "height", 3f), tr = KF(p, "transom", 2.5f), pitch = KF(p, "mullionPitch", 1.5f);
        float mw = KF(p, "mullionWidth", .05f), md = KF(p, "mullionDepth", .12f), kick = KF(p, "bulkhead", .15f);
        if (tr <= kick + .3f || tr >= H - .15f) tr = 0;
        string frameKey = KS(p, "frame", "stainless");
        Material fm = frameKey == "stainless" ? steel : frameKey == "black" ? dark : frameKey == "white" ? KM("white") : KM(frameKey), hw = KM("hairline");
        Batch fr = B("Kit_Frame_" + fm.name, fm), gl = B("Kit_Glass", glass), kp = B("Kit_Trim_" + hw.name, hw);
        var doors = new List<(float c, float w, string type, int leaves, float open)>();
        if (p["doors"] is JArray ds)
            foreach (JObject d in ds)
            {
                float w = KF(d, "width", 1.8f), c = KF(d, "at", L * .5f);
                if (c - w * .5f < -.01f || c + w * .5f > L + .01f) throw new InvalidOperationException(id + ": door outside the storefront edge.");
                doors.Add((c, w, KS(d, "type", "sliding"), KI(d, "leaves", w > 1.2f ? 2 : 1), Mathf.Clamp01(KF(d, "open", 0))));
            }
        var gaps = doors.Select(d => new Vector2(d.c - d.w * .5f, d.c + d.w * .5f)).ToList();
        var posts = new List<float>();
        foreach (Vector2 r in KRuns(L, gaps)) { int bays = Mathf.Max(1, Mathf.RoundToInt((r.y - r.x) / pitch)); for (int k = 0; k <= bays; k++) posts.Add(r.x + (r.y - r.x) * k / bays); }
        foreach (Vector2 gp in gaps) { posts.Add(gp.x); posts.Add(gp.y); }
        posts = posts.OrderBy(s => s).Aggregate(new List<float>(), (l, s) => { if (l.Count == 0 || s - l[l.Count - 1] > .02f) l.Add(s); return l; });
        float top = tr > 0 ? tr : H - .08f;
        foreach (float s in posts) KBox(fr, A + ex * s + up * H * .5f, ex, up, n, new Vector3(mw, H, md));
        KBox(fr, A + ex * L * .5f + up * (H - .04f), ex, up, n, new Vector3(L, .08f, md + .01f));
        if (tr > 0) KBox(fr, A + ex * L * .5f + up * tr, ex, up, n, new Vector3(L, .07f, md));
        for (int i = 0; i + 1 < posts.Count; i++)
        {
            float s0 = posts[i] + mw * .5f, s1 = posts[i + 1] - mw * .5f, sm = (s0 + s1) * .5f, gw = s1 - s0;
            if (gw <= .01f) continue;
            if (!gaps.Any(gp => sm > gp.x && sm < gp.y))
            {
                if (kick > 0) KBox(kp, A + ex * sm + up * kick * .5f, ex, up, n, new Vector3(gw, kick, .04f));
                KBox(fr, A + ex * sm + up * (kick + .025f), ex, up, n, new Vector3(gw, .05f, md));
                KGlass(gl, A + ex * s0 + up * (kick + .05f), ex, up, gw, top - .035f - kick - .05f, n);
                KColRect(A + ex * s0, ex, up, gw, H);
            }
            if (tr > 0) KGlass(gl, A + ex * s0 + up * (tr + .035f), ex, up, gw, H - .08f - tr - .035f, n);
        }
        foreach (var d in doors)
        {
            if (d.type == "open") continue;
            float s0 = d.c - d.w * .5f, doorTop = top - .035f, lw = d.w / d.leaves;
            if (d.type == "sliding")
            {
                // Automatic sliding leaves run behind the line and part from the door centre; operator header inside above the head.
                for (int k = 0; k < d.leaves; k++)
                {
                    float dir = d.leaves == 1 ? 1 : k == 0 ? -1 : 1, ls = s0 + lw * k + dir * d.open * lw - (d.leaves == 2 && k == 1 ? .015f : 0);
                    KDoorLeaf(fr, gl, kp, A + ex * ls - n * .1f, ex, n, lw + .015f, doorTop, false);
                }
                float hs = Mathf.Max(0, d.c - d.w), he = Mathf.Min(L, d.c + d.w);
                KBox(kp, A + ex * (hs + he) * .5f + up * (doorTop + .09f) - n * .15f, ex, up, n, new Vector3(he - hs, .18f, .12f));
            }
            else if (d.type == "swing")
            {
                float angle = d.open * 85 * Mathf.Deg2Rad;
                for (int k = 0; k < d.leaves; k++)
                {
                    bool left = k == 0; Vector3 hinge = A + ex * (left ? s0 : s0 + d.w) - n * .02f;
                    Vector3 along = (left ? ex : -ex) * Mathf.Cos(angle) - n * Mathf.Sin(angle);
                    KDoorLeaf(fr, gl, kp, hinge, along, Vector3.Cross(Vector3.up, along), lw - .01f, doorTop, true);
                }
            }
            else throw new InvalidOperationException(id + ": unknown door type " + d.type);
        }
        if (p["fascia"] is JObject fa)
        {
            float fh = KF(fa, "height", .8f), fd = KF(fa, "depth", .3f), inset = KF(fa, "inset", .06f);
            Material board = KM(KS(fa, "board", "#1E2226"));
            KBox(B("Kit_Fascia_" + board.name, board), A + ex * L * .5f + up * (H + fh * .5f) + n * (fd - .05f) * .5f, ex, up, n, new Vector3(L, fh, fd + .05f));
            float sw = Mathf.Min(KF(fa, "signWidth", L - 2 * inset), L - 2 * inset), sh = fh - 2 * inset;
            Vector3 sc = A + ex * KF(fa, "signAt", L * .5f) + up * (H + fh * .5f) + n * (fd + .004f), right = Vector3.Cross(Vector3.up, -n);
            string tex = KS(fa, "texture", null), mat = KS(fa, "material", null), text = KS(fa, "text", null);
            if (tex != null || mat != null)
            {
                Material lm = tex != null ? KitTexMat(tex) : KM(mat);
                Vector3 bl = sc - right * sw * .5f - up * sh * .5f;
                B("Kit_Lightbox_" + lm.name, lm).TexQuad(bl, bl + right * sw, bl + right * sw + up * sh, bl + up * sh, n);
            }
            else if (text != null) KText(text, sc, n, sw, sh, KC(fa, "ink", Color.white));
            if (KB(fa, "downlights", true))
                for (float s = .75f; s < L - .3f; s += 1.5f) KDownlight(A + ex * s + n * fd * .5f + up * H, .05f);
        }
    }
    // Door leaf from its hinge/start corner o along `along` (width w, height h): glass in a stainless frame, pull bars on both faces.
    static void KDoorLeaf(Batch fr, Batch gl, Batch pull, Vector3 o, Vector3 along, Vector3 n, float w, float h, bool framed)
    {
        Vector3 up = Vector3.up; float stile = framed ? .06f : .03f;
        KBox(fr, o + along * w * .5f + up * .05f, along, up, n, new Vector3(w, .1f, .04f));
        KBox(fr, o + along * w * .5f + up * (h - .04f), along, up, n, new Vector3(w, .08f, .04f));
        KBox(fr, o + along * stile * .5f + up * h * .5f, along, up, n, new Vector3(stile, h, .04f));
        KBox(fr, o + along * (w - stile * .5f) + up * h * .5f, along, up, n, new Vector3(stile, h, .04f));
        KGlass(gl, o + along * stile + up * .1f, along, up, w - 2 * stile, h - .18f, n);
        Vector3 bar = o + along * (w - .12f);
        foreach (float side in new[] { 1f, -1f })
        {
            KTube(pull, bar + n * side * .055f + up * .8f, bar + n * side * .055f + up * 1.4f, .014f, 8);
            KBox(pull, bar + n * side * .037f + up * .85f, along, up, n, new Vector3(.02f, .02f, .035f));
            KBox(pull, bar + n * side * .037f + up * 1.35f, along, up, n, new Vector3(.02f, .02f, .035f));
        }
    }

    // ---- ServiceDoor ----
    static void KitServiceDoor(string id, JObject g, JObject p)
    {
        JToken edge = KT(g, "edge", id); Vector2 a2 = KV(edge[0]), b2 = KV(edge[1]); float y = KN(g, "y", id);
        Vector3 n = KEdgeNormal(a2, b2, p), ex = (KP(b2, 0) - KP(a2, 0)).normalized, up = Vector3.up;
        float w = KF(p, "width", 1f), h = KF(p, "height", 2.1f), proud = KF(p, "proud", .05f), fw = .06f;
        int leaves = KI(p, "leaves", w > 1.3f ? 2 : 1);
        Vector3 c = Vector3.Lerp(KP(a2, y), KP(b2, y), KF(p, "t", .5f));
        Material leafM = KM(KS(p, "colour", "#7E868C")), frameM = KM(KS(p, "frame", "#565D63")), hw = KM("hairline");
        Batch lf = B("Kit_Door_" + leafM.name, leafM), fr = B("Kit_Door_" + frameM.name, frameM), ss = B("Kit_Trim_" + hw.name, hw);
        Vector3 fc = c + n * (proud - .07f);
        KBox(fr, fc - ex * (w * .5f + fw * .5f) + up * (h + fw) * .5f, ex, up, n, new Vector3(fw, h + fw, .14f), false);
        KBox(fr, fc + ex * (w * .5f + fw * .5f) + up * (h + fw) * .5f, ex, up, n, new Vector3(fw, h + fw, .14f), false);
        KBox(fr, fc + up * (h + fw * .5f), ex, up, n, new Vector3(w + 2 * fw, fw, .14f));
        KRect(B("Kit_Joint", dark), c + n * (proud - .1f) - ex * w * .5f, ex, up, w, h, n, Vector2.zero);
        float lw = w / leaves; bool kick = KB(p, "kickPlate", true), closer = KB(p, "closer", true);
        string plate = KS(p, "plate", "관계자외 출입금지");
        for (int k = 0; k < leaves; k++)
        {
            Vector3 lc = c + n * (proud - .045f) + ex * (-w * .5f + lw * (k + .5f)) + up * (h * .5f + .005f), face = lc + n * .0225f;
            KBox(lf, lc, ex, up, n, new Vector3(lw - .006f, h - .012f, .045f), false, false);
            float free = leaves == 1 || k == 0 ? 1 : -1;
            Vector3 hp = face + ex * free * (lw * .5f - .09f) + up * (1f - h * .5f - .005f);
            KRing(ss, hp + n * .002f, ex, up, 0, .03f, 12, n);
            KBox(ss, hp + n * .025f, ex, up, n, new Vector3(.02f, .02f, .05f));
            KBox(ss, hp + n * .05f - ex * free * .06f, ex, up, n, new Vector3(.14f, .022f, .022f));
            if (kick) KRect(ss, face + n * .001f - ex * (lw - .03f) * .5f - up * (h * .5f - .015f), ex, up, lw - .03f, .25f, n, Vector2.zero);
            if (closer && k == 0) KBox(fr, face + n * .03f + up * (h * .5f - .09f) - ex * free * lw * .15f, ex, up, n, new Vector3(.32f, .06f, .06f));
            if (k == 0 && !string.IsNullOrEmpty(plate))
            {
                Material pm = KM(KS(p, "plateColour", "#1B2A4A"));
                Vector3 pc = face + n * .003f + up * (1.55f - h * .5f - .005f);
                KRectC(B("Kit_Plate_" + pm.name, pm), pc, ex, up, Mathf.Min(.4f, lw - .1f), .11f, n);
                KText(plate, pc + n * .001f, n, Mathf.Min(.4f, lw - .1f), .11f, KC(p, "plateInk", Color.white));
            }
        }
        KColRect(c - ex * w * .5f, ex, up, w, h);
    }

    // ---- HangingSign / WallSign (station wayfinding: navy boards, white Korean + English text, glyph arrows, badges) ----
    static (Material board, Color ink, Material glyph, Material badge, Color badgeInk) KSignStyle(string style)
    {
        Color white = Color.white, ink = new Color(.07f, .07f, .08f);
        switch (style)
        {
            case "navy": return (KM("Kit_SignNavy"), white, KM("Kit_Glyph"), KM("Kit_GlyphYellow"), ink);
            case "yellow": return (KM("yellow"), ink, KM("Kit_GlyphDark"), KM("Kit_SignNavy"), white);
            case "green": return (KM("Kit_SignGreen"), white, KM("Kit_Glyph"), KM("Kit_Glyph"), new Color(0, .4f, .22f));
            case "dark": return (dark, white, KM("Kit_Glyph"), KM("Kit_GlyphYellow"), ink);
            default: throw new InvalidOperationException("Unknown sign style " + style);
        }
    }
    static void KitSign(string id, JObject g, JObject p, bool hanging)
    {
        Vector2 at = KV(KT(g, "point", id)); float y = KN(g, "y", id);
        Vector3 n = KHeading(p, at, id), up = Vector3.up, ex = Vector3.Cross(Vector3.up, -n);
        float w = KF(p, "width", hanging ? 2.4f : 1.2f), h = KF(p, "height", hanging ? .45f : .4f), d = KF(p, "depth", hanging ? .12f : .05f);
        var st = KSignStyle(KS(p, "style", "navy"));
        Material caseM = KM(KS(p, "case", "signCase"));
        Vector3 c = hanging ? KP(at, y + h * .5f + .02f) : KP(at, y) + n * (KF(p, "standoff", .02f) + d * .5f);
        KBox(B("Kit_SignCase_" + caseM.name, caseM), c, ex, up, n, new Vector3(w + .04f, h + .04f, d));
        KSignSide(c + n * (d * .5f + .002f), n, w, h, p, st, p["items"] as JArray, false);
        if (hanging && KB(p, "doubleSided", true)) KSignSide(c - n * (d * .5f + .002f), -n, w, h, p, st, p["back"] as JArray ?? p["items"] as JArray, p["back"] == null);
        if (!hanging) return;
        float ceil = KF(p, "ceilingY", y + h + .6f), top = y + h + .04f;
        Material hw = KM("hairline"); Batch rods = B("Kit_Trim_" + hw.name, hw);
        if (ceil > top + .02f)
            foreach (float s in new[] { -.35f, .35f })
            {
                Vector3 rb = KP(at, 0) + ex * s * w + up * top;
                KTube(rods, rb, rb + up * (ceil - top), .012f, 8);
                KRingH(rods, rb + up * (ceil - top - .003f), 0, .045f, 12, Vector3.down);
            }
    }
    // One sign face centred at fc facing n: lightbox texture/material, or board + item slots [arrow][badge][text/sub][arrow].
    static void KSignSide(Vector3 fc, Vector3 n, float w, float h, JObject p, (Material board, Color ink, Material glyph, Material badge, Color badgeInk) st, JArray items, bool mirror)
    {
        Vector3 right = Vector3.Cross(Vector3.up, -n), up = Vector3.up;
        string tex = KS(p, "texture", null), mat = KS(p, "material", null);
        if (tex != null || mat != null)
        {
            Material lm = tex != null ? KitTexMat(tex) : KM(mat);
            Vector3 bl = fc - right * w * .5f - up * h * .5f;
            B("Kit_Lightbox_" + lm.name, lm).TexQuad(bl, bl + right * w, bl + right * w + up * h, bl + up * h, n);
            return;
        }
        KRectC(B("Kit_Sign_" + st.board.name, st.board), fc, right, up, w, h, n);
        if (items == null || items.Count == 0) return;
        Batch glyphs = B("Kit_Glyph_" + st.glyph.name, st.glyph);
        float slot = w / items.Count, gsz = h * .72f, pad = h * .12f;
        for (int i = 0; i < items.Count; i++)
        {
            JObject it = items[i] as JObject ?? new JObject { ["text"] = (string)items[i] };
            string arrow = KS(it, "arrow", null), badge = KS(it, "badge", null), text = KS(it, "text", ""), sub = KS(it, "sub", null);
            if (mirror && arrow != null) arrow = arrow.Contains("left") ? arrow.Replace("left", "right") : arrow.Replace("right", "left");
            float x0 = -w * .5f + slot * i + pad, x1 = -w * .5f + slot * (i + 1) - pad;
            if (arrow != null)
            {
                bool atRight = arrow.Contains("right");
                KArrow(glyphs, fc + right * (atRight ? x1 - gsz * .5f : x0 + gsz * .5f), right, up, n, gsz, arrow);
                if (atRight) x1 -= gsz + pad; else x0 += gsz + pad;
            }
            if (badge != null)
            {
                float r = gsz * .45f; Vector3 bc = fc + right * (x0 + r) + n * .003f;
                KRing(B("Kit_Glyph_" + st.badge.name, st.badge), bc, right, up, 0, r, 24, n);
                KText(badge, bc + n * .002f, n, r * 1.6f, r * 1.5f, st.badgeInk);
                x0 += 2 * r + pad;
            }
            Vector3 tc = fc + right * ((x0 + x1) * .5f);
            // TextMesh line boxes carry ~40% leading, so the boxes overlap the margins to reach station-sign cap heights.
            if (sub != null) { KText(text, tc + up * h * .12f, n, x1 - x0, h * .72f, st.ink); KText(sub, tc - up * h * .28f, n, x1 - x0, h * .36f, st.ink); }
            else KText(text, tc, n, x1 - x0, h * .95f, st.ink);
            if (i > 0) KRectC(glyphs, fc + right * (-w * .5f + slot * i) + n * .003f, right, up, .012f, h * .7f, n);
        }
    }

    // ---- FloorFinish (overlay, no collider) ----
    static void KitFloor(string id, JObject g, JObject p)
    {
        List<Vector2> poly = KPoly(KT(g, "polygon", id)); List<List<Vector2>> holes = KHoles(g);
        float y = KN(g, "y", id) + KF(p, "lift", .003f);
        Material m = KM(KS(p, "finish", "polished"));
        Vector2 o = p["uvOrigin"] is JArray uo ? KV(uo) : poly[0];
        KFill(B("Kit_Floor_" + m.name, m), poly, holes, y, Vector3.up, o, KF(p, "rotation", 0), KF(p, "uvRepeat", 1));
        if (p["border"] is JObject border)
        {
            float bw = KF(border, "width", .3f); Material bm = KM(KS(border, "finish", "granite")); Batch bb = B("Kit_Floor_" + bm.name, bm);
            var rings = new List<List<Vector2>> { poly }; rings.AddRange(holes);
            foreach (List<Vector2> ring in rings)
                for (int i = 0; i < ring.Count; i++)
                    foreach (var piece in KBoundary(rings, ring[i], ring[(i + 1) % ring.Count]))
                    {
                        Vector2 s2 = Vector2.Lerp(ring[i], ring[(i + 1) % ring.Count], piece.t0), e2 = Vector2.Lerp(ring[i], ring[(i + 1) % ring.Count], piece.t1);
                        float len = Vector2.Distance(s2, e2);
                        KRect(bb, KP(s2, y + .001f), KD((e2 - s2) / len), KD(piece.inward), len, bw, Vector3.up, new Vector2(Vector2.Dot(s2, (e2 - s2) / len), 0));
                    }
        }
        if (p["strips"] is JArray strips)
            foreach (JObject s in strips)
            {
                Material sm = KM(KS(s, "kind", "line") == "dot" ? "tactileDot" : "tactileLine");
                KTactile(B("Kit_Floor_" + sm.name, sm), KP(KV(KT(s, "from", id)), y), KP(KV(KT(s, "to", id)), y), KF(s, "width", .3f));
            }
        if (p["pads"] is JArray pads)
            foreach (JObject pd in pads)
            {
                Vector2 c2 = KV(KT(pd, "center", id)); JArray sz = pd["size"] as JArray;
                float a = sz != null ? (float)sz[0] : .6f, b = sz != null ? (float)sz[1] : .6f, ang = KF(pd, "angle", 0) * Mathf.Deg2Rad;
                Vector2 d2 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Material pm = KM(KS(pd, "kind", "dot") == "line" ? "tactileLine" : "tactileDot");
                KTactile(B("Kit_Floor_" + pm.name, pm), KP(c2 - d2 * a * .5f, y), KP(c2 + d2 * a * .5f, y), b);
            }
    }
    // Raised 5 mm tactile strip along a->c (centreline), width w; texture v runs along the strip (guide bars follow travel).
    static void KTactile(Batch b, Vector3 a, Vector3 c, float w)
    {
        float len = Vector3.Distance(a, c); if (len < .01f) return;
        Vector3 dir = (c - a) / len, side = Vector3.Cross(Vector3.up, dir), h = Vector3.up * .005f, o = a - side * w * .5f;
        b.Face(o + h, o + side * w + h, o + side * w + dir * len + h, o + dir * len + h, Vector2.zero, new Vector2(w, 0), new Vector2(w, len), new Vector2(0, len), Vector3.up);
        KRect(b, o, dir, Vector3.up, len, .005f, -side, Vector2.zero); KRect(b, o + side * w, dir, Vector3.up, len, .005f, side, Vector2.zero);
        KRect(b, o, side, Vector3.up, w, .005f, -dir, Vector2.zero); KRect(b, o + dir * len, side, Vector3.up, w, .005f, dir, Vector2.zero);
    }

    // ---- ShopInterior (furnished tenancy: finishes, lit ceiling, fixtures by kind) ----
    static readonly Dictionary<string, (string back, string floor)> KitShopKinds = new Dictionary<string, (string back, string floor)>
    {
        { "cafe", ("wood", "woodFloor") }, { "convenience", ("shopPanel", "floorGranite") }, { "bakery", ("ceramic", "floorGranite") },
        { "restaurant", ("wood", "floorGranite") }, { "retail", ("plaster", "floorGranite") },
    };
    static void KitShop(string id, JObject g, JObject p)
    {
        List<Vector2> poly = KPoly(KT(g, "polygon", id)); float y = KN(g, "y", id), y1 = KF(g, "y1", y + 3f);
        string kind = KS(p, "kind", "retail");
        if (!KitShopKinds.TryGetValue(kind, out var look)) throw new InvalidOperationException(id + ": unknown shop kind " + kind);
        int count = poly.Count, fi = KI(p, "front", 0);
        if (fi < 0 || fi >= count) throw new InvalidOperationException(id + ": front edge index out of range.");
        float sign = KArea(poly) > 0 ? 1 : -1;
        Func<int, Vector2> inward = i => { Vector2 d = (poly[(i + 1) % count] - poly[i]).normalized; return new Vector2(-d.y, d.x) * sign; };
        Vector2 fa = poly[fi], fb = poly[(fi + 1) % count], ex2 = (fb - fa).normalized, in2 = inward(fi);
        float L = Vector2.Distance(fa, fb), depth = poly.Max(q => Vector2.Dot(q - fa, in2));
        if (L < 1.5f || depth < 1.5f) throw new InvalidOperationException(id + ": shop too small for an interior (front " + L + " m, depth " + depth + " m).");
        Vector3 EX = KD(ex2), IN = KD(in2), OUT = -IN, up = Vector3.up;
        Func<float, float, float, Vector3> at = (s, d, h) => KP(fa + ex2 * s + in2 * d, y + h);
        var openEdges = new HashSet<int>((p["openEdges"] as JArray ?? new JArray()).Select(t => (int)t));
        if (openEdges.Any(i => i < 0 || i >= count)) throw new InvalidOperationException(id + ": openEdges index out of range.");
        int backEdge = p["back"] != null && p["back"].Type != JTokenType.Null ? (int)p["back"]
            : Enumerable.Range(0, count).Where(i => i != fi && !openEdges.Contains(i)).OrderByDescending(i => Vector2.Dot((poly[i] + poly[(i + 1) % count]) * .5f - fa, in2)).DefaultIfEmpty(-1).First();
        if (backEdge >= count) throw new InvalidOperationException(id + ": back edge index out of range.");
        var none = new List<List<Vector2>>();
        Material fm = KM(KS(p, "floor", look.floor));
        KFill(B("Kit_Floor_" + fm.name, fm), poly, none, y + .004f, up, fa, Mathf.Atan2(ex2.y, ex2.x) * Mathf.Rad2Deg, 1);
        Material wm = KM(KS(p, "wallFinish", "plaster")), bm = KM(KS(p, "backWall", look.back));
        bool collide = KB(p, "collider", true);
        for (int i = 0; i < count; i++)
        {
            if (i == fi || openEdges.Contains(i)) continue;
            Vector2 a2 = poly[i], b2 = poly[(i + 1) % count]; float len = Vector2.Distance(a2, b2);
            if (len < .05f) continue;
            bool back = i == backEdge; Material m = back ? bm : wm;
            Vector3 n = KD(inward(i)), ex = KD((b2 - a2) / len), o = KP(a2, y) + n * .02f;
            KPanels(B("Kit_ShopWall_" + m.name, m), B("Kit_Joint", dark), o + up * .1f, ex, up, n, len, y1 - y - .1f, back ? 1.2f : 0, 0, back ? .004f : 0, 0, y + .1f, null);
            KBox(B("Kit_Joint", dark), o + ex * len * .5f + up * .05f + n * .006f, ex, up, n, new Vector3(len, .1f, .012f), false, false);
            if (collide) KColRect(KP(a2, y), ex, up, len, y1 - y);
        }
        Material cm = KM(KS(p, "ceiling", "Kit_GridWhite"));
        KFill(B("Kit_ShopCeiling_" + cm.name, cm), poly, none, y1, Vector3.down, fa, 0, 1);
        float dp = KF(p, "downlightPitch", 1.4f);
        for (float s = dp * .5f; s < L; s += dp)
            for (float d = dp * .5f; d < depth; d += dp)
            {
                Vector2 q = fa + ex2 * s + in2 * d;
                if (KInRing(poly, q)) KDownlight(KP(q, y1), .06f);
            }
        if (KB(p, "light", true))
            KLight(id + " light", at(L * .5f, depth * .5f, y1 - y - 1f), new JObject { ["intensity"] = 1.1f, ["range"] = Mathf.Max(L, depth) + 1.5f, ["kelvin"] = kind == "convenience" ? 5000 : 3500 });
        if (!KB(p, "fixtures", true)) return;
        var rnd = new System.Random(KI(p, "seed", id.Aggregate(17, (h, ch) => unchecked(h * 31 + ch))));
        float backD = depth - .02f;
        if (p["layout"] is JArray layout) { KShopLayout(id, layout, y, rnd); return; }
        switch (kind)
        {
            case "cafe":
            {
                int nc = Mathf.Clamp(Mathf.FloorToInt(L * .35f), 1, 3);
                for (int k = 0; k < nc; k++) KitProp("kit_kitchenCounter", id + " counter " + k, at(L * .5f + (k - (nc - 1) * .5f), backD - 1.4f, 0), new Vector3(1f, .95f, .65f), OUT);
                KMenuBoards(at(L * .5f, backD - .03f, 2.15f), EX, OUT, Mathf.Min(L - .8f, 3.2f));
                // Seating capped at six table sets (~7k tris) per shop.
                int sets = 0;
                for (float d = 1.3f; d < backD - 2.3f && sets < 6; d += 2.1f)
                    for (float s = 1.2f; s < L - .9f && sets < 6; s += 2.2f, sets++) KTableSet(id + " " + s + "," + d, at(s, d, 0), EX, IN);
                break;
            }
            case "convenience":
            {
                KCooler(at(L * .5f, backD - .32f, 0), EX, OUT, Mathf.Min(L - .6f, 6f), rnd);
                float d0 = 1.4f, d1 = backD - 1.5f;
                if (d1 - d0 >= 1f) for (float s = 2.1f; s < L - .9f; s += 1.9f) KShelf(at(s, (d0 + d1) * .5f, 0), IN, EX, d1 - d0, 1.5f, .4f, true, rnd, KitProducts, 4);
                KitProp("kit_kitchenCounter", id + " counter", at(.8f, 1.1f, 0), new Vector3(1.2f, .95f, .6f), EX);
                break;
            }
            case "bakery":
            {
                KShelf(at(L * .5f, backD, 0), EX, OUT, Mathf.Min(L - .8f, 5f), 1.6f, .45f, false, rnd, new[] { "Kit_Bread" }, 4);
                KitProp("kit_kitchenCounter", id + " counter", at(L * .5f, backD - 1.3f, 0), new Vector3(1.6f, .95f, .65f), OUT);
                for (float s = 1.3f; s < L - 1f; s += 2f)
                    for (float d = 1.4f; d < backD - 2.3f; d += 1.6f)
                    {
                        KitProp("kit_tableMedium", id + " table " + s + "," + d, at(s, d, 0), new Vector3(1.2f, .8f, .8f), OUT);
                        KLoaves(at(s, d, .8f), EX, OUT, 1.1f, .7f, rnd);
                    }
                break;
            }
            case "restaurant":
            {
                int nk = Mathf.Clamp(Mathf.FloorToInt((L - .6f) / 1.35f), 1, 5);
                for (int k = 0; k < nk; k++) KitProp("kit_kitchenTable", id + " kitchen " + k, at(L * .5f + (k - (nk - 1) * .5f) * 1.35f, backD - .5f, 0), new Vector3(1.35f, .87f, .9f), OUT);
                for (float d = 1.2f; d < backD - 2.1f; d += 2.1f)
                    for (float s = 1f; s < L - .9f; s += 2f) KitProp("kit_dining", id + " dining " + s + "," + d, at(s, d, 0), new Vector3(1.5f, .9f, 1.55f), EX);
                break;
            }
            default:
            {
                KShelf(at(L * .5f, backD, 0), EX, OUT, Mathf.Min(L - .8f, 6f), 2f, .4f, false, rnd, KitProducts, 5);
                for (float s = 1.4f; s < L - 1f; s += 2.2f)
                    for (float d = 1.5f; d < backD - 1.4f && s < 1.4f + 2.2f * 2; d += 1.8f) KitProp("kit_cabinet", id + " display " + s + "," + d, at(s, d, 0), new Vector3(1f, .9f, .5f), OUT);
                KitProp("kit_plant", id + " plant", at(.45f, .45f, 0), new Vector3(.5f, .75f, .5f), OUT);
                break;
            }
        }
    }
    // Explicit ShopInterior fixtures (params.layout replaces the kind's automatic set): {fixture, at [u,v] (floor footprint centre;
    // shelf/bakeryShelf: back line centre; menu: point on the wall face), heading [du,dv] (front / customer side; menu: out of the
    // wall), length (extent across heading), depth, height, elevation (menu: centre height 2.15; others: base above the floor 0)}.
    static void KShopLayout(string id, JArray layout, float y, System.Random rnd)
    {
        int k = 0;
        foreach (JObject f in layout)
        {
            string key = KS(f, "fixture", null), name = id + " " + key + " " + k++;
            if (key == null) throw new InvalidOperationException(id + ": layout entry needs `fixture`.");
            Vector2 at2 = KV(KT(f, "at", id));
            if (!(f["heading"] is JArray hd) || KV(hd).sqrMagnitude < 1e-8f) throw new InvalidOperationException(id + ": layout " + key + " needs a non-zero heading.");
            Vector3 fw = KD(KV(hd).normalized), side = Vector3.Cross(Vector3.up, fw);
            float elev = KF(f, "elevation", key == "menu" ? 2.15f : 0f);
            Vector3 c = KP(at2, y + elev);
            Func<float, float, float, Vector3> size = (l, h, d) => new Vector3(KF(f, "length", l), KF(f, "height", h), KF(f, "depth", d));
            switch (key)
            {
                case "tableSet": KTableSet(name, c, side, fw); break;
                case "dining": KitProp("kit_dining", name, c, size(1.5f, .9f, 1.55f), fw); break;
                case "kitchen": KitProp("kit_kitchenTable", name, c, size(1.35f, .87f, .9f), fw); break;
                case "counter": KitProp("kit_kitchenCounter", name, c, size(1.2f, .95f, .65f), fw); break;
                case "cabinet": KitProp("kit_cabinet", name, c, size(1f, .9f, .5f), fw); break;
                case "plant": KitProp("kit_plant", name, c, size(.5f, .75f, .5f), fw); break;
                case "menu": KMenuBoards(c, side, fw, KF(f, "length", 3.2f)); break;
                case "shelf": KShelf(c, side, fw, KF(f, "length", 2f), KF(f, "height", 2f), KF(f, "depth", .4f), false, rnd, KitProducts, 5); break;
                case "gondola": KShelf(c, side, fw, KF(f, "length", 2f), KF(f, "height", 1.5f), KF(f, "depth", .4f), true, rnd, KitProducts, 4); break;
                case "cooler": KCooler(c, side, fw, KF(f, "length", 3f), rnd); break;
                case "bakeryShelf": KShelf(c, side, fw, KF(f, "length", 2f), KF(f, "height", 1.6f), KF(f, "depth", .45f), false, rnd, new[] { "Kit_Bread" }, 4); break;
                case "loafTable":
                {
                    Vector3 s = size(1.2f, .8f, .8f);
                    KitProp("kit_tableMedium", name, c, s, fw);
                    KLoaves(c + Vector3.up * s.y, side, fw, s.x - .1f, s.z - .1f, rnd);
                    break;
                }
                default: throw new InvalidOperationException(id + ": unknown layout fixture '" + key + "'.");
            }
        }
    }
    // Bread loaves on a tray (tray centre c on the table top, rows along ex).
    static void KLoaves(Vector3 c, Vector3 ex, Vector3 n, float len, float depth, System.Random rnd)
    {
        Material tray = KM("hairline"), bread = KM("Kit_Bread"); Batch tb = B("Kit_Trim_" + tray.name, tray), bb = B("Kit_Product_" + bread.name, bread);
        Vector3 up = Vector3.up;
        KBox(tb, c + up * .01f, ex, up, n, new Vector3(len, .02f, depth), false);
        for (int r = 0; r < 3; r++)
            for (int k = 0; k < 5; k++)
            {
                float lw = .13f + (float)rnd.NextDouble() * .05f, lh = .05f + (float)rnd.NextDouble() * .04f;
                KBox(bb, c + ex * ((k - 2) * len * .19f) + n * ((r - 1) * depth * .3f) + up * (.02f + lh * .5f), ex, up, n, new Vector3(lw, lh, lw * .7f), false);
            }
    }
    static void KTableSet(string name, Vector3 c, Vector3 ex, Vector3 fwd)
    {
        KitProp("kit_tableRound", name + " table", c, new Vector3(.75f, .74f, .75f), fwd);
        KitProp("kit_chair", name + " chair L", c - ex * .62f, new Vector3(.45f, .85f, .48f), ex);
        KitProp("kit_chair", name + " chair R", c + ex * .62f, new Vector3(.45f, .85f, .48f), -ex);
    }
    // Digital menu boards on a back wall (centre c, facing n): dark screens with lit menu lines.
    static void KMenuBoards(Vector3 c, Vector3 ex, Vector3 n, float width)
    {
        Material gly = KM("Kit_Glyph"); Batch sb = B("Kit_Screen", KM("Kit_Screen")), gb = B("Kit_Glyph_" + gly.name, gly);
        int k = Mathf.Max(1, Mathf.RoundToInt(width / 1.05f)); float pw = width / k - .06f;
        for (int i = 0; i < k; i++)
        {
            Vector3 pc = c + ex * ((i + .5f) * width / k - width * .5f);
            KBox(sb, pc + n * .02f, ex, Vector3.up, n, new Vector3(pw, .6f, .04f));
            for (int r = 0; r < 5; r++)
            {
                Vector3 lc = pc + n * .041f + Vector3.up * (.2f - r * .1f);
                KRectC(gb, lc - ex * pw * .12f, ex, Vector3.up, pw * .55f, .022f, n);
                KRectC(gb, lc + ex * pw * .33f, ex, Vector3.up, pw * .14f, .022f, n);
            }
        }
    }
    // Parametric display shelving (c = centre of the back line on the floor; along = run; face = display side, both sides if twoSided).
    static void KShelf(Vector3 c, Vector3 along, Vector3 face, float len, float h, float depth, bool twoSided, System.Random rnd, string[] products, int levels)
    {
        Material body = KM("white"); Batch bb = B("Kit_Shelf_" + body.name, body), dk = B("Kit_Joint", dark);
        Vector3 up = Vector3.up; float step = (h - .2f) / levels;
        KBox(bb, c + up * h * .5f, along, up, face, new Vector3(len, h, .04f));
        foreach (float side in twoSided ? new[] { 1f, -1f } : new[] { 1f })
        {
            Vector3 fn = face * side, b0 = c + fn * (.02f + depth * .5f);
            KBox(dk, b0 + up * .06f, along, up, fn, new Vector3(len, .12f, depth), false);
            KBox(bb, b0 + up * (h - .0125f), along, up, fn, new Vector3(len, .025f, depth));
            for (int lv = 0; lv < levels; lv++)
            {
                float yl = .12f + lv * step;
                if (lv > 0) KBox(bb, b0 + up * (yl + .0125f), along, up, fn, new Vector3(len, .025f, depth));
                float x = -len * .5f + .03f, top = yl + (lv > 0 ? .025f : 0);
                while (x < len * .5f - .08f)
                {
                    float w = Mathf.Min(.06f + (float)rnd.NextDouble() * .13f, len * .5f - .03f - x), ph = Mathf.Min(step - .06f, .08f + (float)rnd.NextDouble() * .16f);
                    Material m = KM(products[rnd.Next(products.Length)]);
                    KBox(B("Kit_Product_" + m.name, m), b0 + along * (x + w * .5f) + up * (top + ph * .5f) + fn * depth * .08f, along, up, fn, new Vector3(w - .012f, ph, depth * .78f), false, false);
                    x += w;
                }
            }
        }
        Vector3 mid = twoSided ? Vector3.zero : face * (depth * .5f + .02f); float full = twoSided ? 2 * depth + .06f : depth + .04f;
        foreach (float e in new[] { -1f, 1f }) KBox(bb, c + along * e * (len * .5f + .015f) + up * h * .5f + mid, along, up, face, new Vector3(.03f, h, full));
        KColBox(c + up * h * .5f + mid, along, up, face, new Vector3(len + .06f, h, full));
    }
    // Refrigerated display wall: stocked shelving behind framed glass doors with a lit header (c = cabinet centre on the floor).
    static void KCooler(Vector3 c, Vector3 along, Vector3 face, float len, System.Random rnd)
    {
        const float h = 2f, depth = .6f;
        Vector3 up = Vector3.up, fr = c + face * depth * .5f;
        KShelf(c - face * depth * .5f, along, face, len, h - .25f, depth - .1f, false, rnd, KitProducts, 5);
        Material body = KM("white"), hl = KM("Kit_Backlight"); Batch bb = B("Kit_Shelf_" + body.name, body), sb = B("Kit_Frame_" + steel.name, steel);
        KBox(bb, fr + up * (h - .12f) - face * .3f, along, up, face, new Vector3(len + .06f, .24f, .6f));
        KRectC(B("Kit_Light_" + hl.name, hl), fr + up * (h - .12f) + face * .002f, along, up, len - .1f, .14f, face);
        int doors = Mathf.Max(1, Mathf.RoundToInt(len / .75f)); float dw = len / doors;
        for (int k = 0; k <= doors; k++) KBox(sb, fr + along * (k * dw - len * .5f) + up * (h - .24f) * .5f, along, up, face, new Vector3(.04f, h - .24f, .04f));
        KBox(sb, fr + up * .05f, along, up, face, new Vector3(len, .1f, .04f));
        KGlass(B("Kit_Glass", glass), fr - along * len * .5f + up * .1f, along, up, len, h - .34f, face);
        for (int k = 0; k < doors; k++)
        {
            Vector3 hb = fr + face * .035f + along * ((k + .88f) * dw - len * .5f) + up * .85f;
            KTube(sb, hb, hb + up * .5f, .012f, 6);
        }
    }

    // ---- Escalator (parametric: steps, comb plates, skirts, decking, glass balustrade, handrail loops, truss cladding) ----
    static void KitEscalator(string id, JObject g, JObject p)
    {
        Vector2 foot = KV(KT(g, "foot", id)); float y = KN(g, "y", id);
        Vector3 f = KHeading(p, foot, id), s = Vector3.Cross(Vector3.up, f);
        float rise = KN(p, "rise", id), w = KF(p, "width", 1f), spacing = KF(p, "spacing", w + .75f);
        int count = KI(p, "count", 1); JArray dirs = p["directions"] as JArray;
        for (int k = 0; k < count; k++)
            KEscalatorUnit(KP(foot, y) + s * (k - (count - 1) * .5f) * spacing, f, s, w, rise, p,
                dirs != null ? (string)dirs[k % dirs.Count] : KS(p, "direction", "up"), k > 0 ? spacing : 0);
    }
    static void KEscalatorUnit(Vector3 o, Vector3 f, Vector3 s, float w, float rise, JObject p, string dir, float gapToPrev)
    {
        float ang = Mathf.Clamp(KF(p, "angle", 30), 20, 40) * Mathf.Deg2Rad, tan = Mathf.Tan(ang), truss = KF(p, "truss", 1f), runout = KF(p, "runout", .9f);
        int flat = KI(p, "flatSteps", 3);
        float run = rise / tan; int n = Mathf.Max(1, Mathf.RoundToInt(run / .4f)); float t = run / n, r = rise / n;
        float x0 = runout + flat * t, x1 = x0 + run, total = x1 + x0, bl = Mathf.Min(.6f, run * .25f);
        Vector3 up = Vector3.up;
        // Balustrade profile: flat - fillet - incline - fillet - flat.
        Func<float, float> Hs = x =>
        {
            if (x <= x0 - bl) return 0;
            if (x < x0 + bl) { float q = x - x0 + bl; return tan * q * q / (4 * bl); }
            if (x <= x1 - bl) return (x - x0) * tan;
            if (x < x1 + bl) { float q = x1 + bl - x; return rise - tan * q * q / (4 * bl); }
            return rise;
        };
        Func<float, float, float, Vector3> Pt = (x, lat, h) => o + f * x + s * lat + up * (Hs(x) + h);
        var xs = new List<float> { 0, x0 - bl, x0 - bl * .5f, x0, x0 + bl * .5f, x0 + bl, x1 - bl, x1 - bl * .5f, x1, x1 + bl * .5f, x1 + bl, total };
        void Deck(Batch b, float latA, float latB, float h, Vector3 nn, float from, float to)
        {
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float a = Mathf.Max(xs[i], from), c = Mathf.Min(xs[i + 1], to); if (c - a < 1e-3f) continue;
                b.Face(Pt(a, latA, h), Pt(c, latA, h), Pt(c, latB, h), Pt(a, latB, h), new Vector2(a, latA), new Vector2(c, latA), new Vector2(c, latB), new Vector2(a, latB), nn);
            }
        }
        void Side(Batch b, float lat, float h0, float h1, Vector3 nn, float from, float to)
        {
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float a = Mathf.Max(xs[i], from), c = Mathf.Min(xs[i + 1], to); if (c - a < 1e-3f) continue;
                b.Face(Pt(a, lat, h0), Pt(c, lat, h0), Pt(c, lat, h1), Pt(a, lat, h1), new Vector2(a, Hs(a) + h0), new Vector2(c, Hs(c) + h0), new Vector2(c, Hs(c) + h1), new Vector2(a, Hs(a) + h1), nn);
            }
        }
        float half = w * .5f, outer = half + .3f, end = float.MaxValue;
        Material tread = KM("stepTread"), plate = KM("checker"), hw = KM("hairline"), clad = KM(KS(p, "cladding", "escalatorBody")), rub = KM("Kit_Rubber"), yl = KM("yellow");
        Batch tb = B("Kit_EscStep_" + tread.name, tread), rb = B("Kit_Joint", dark), pb = B("Kit_EscPlate_" + plate.name, plate), sk = B("Kit_Frame_" + steel.name, steel);
        Batch dk = B("Kit_Trim_" + hw.name, hw), cl = B("Kit_EscClad_" + clad.name, clad), gl = B("Kit_Glass", glass), hr = B("Kit_Rubber", rub), yb = B("Kit_Esc_" + yl.name, yl);
        // Steps: flat landing steps and the stepped incline, one texture repeat per tread.
        void Tread(float xa, float xb, float h)
        {
            Vector3 a = o + f * xa - s * half + up * h, b = a + s * w;
            tb.Face(a, b, b + f * (xb - xa), a + f * (xb - xa), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), up);
        }
        for (float x = runout; x < x0 - 1e-3f; x += t) Tread(x, Mathf.Min(x + t, x0) - .01f, .005f);
        for (int i = 0; i < n; i++)
        {
            float xa = x0 + i * t;
            KRect(rb, o + f * xa - s * half + up * (i * r), s, up, w, r, -f, Vector2.zero);
            Tread(xa, xa + t - .01f, (i + 1) * r);
        }
        for (float x = x1; x < total - runout - 1e-3f; x += t) Tread(x, Mathf.Min(x + t, total - runout) - .01f, rise + .005f);
        // Floor plates with yellow comb edges.
        foreach (bool top in new[] { false, true })
        {
            float xa = top ? total - runout : 0, xb = top ? total : runout, h = top ? rise : 0;
            Vector3 a = o + f * xa - s * (half + .05f) + up * (h + .012f);
            pb.Face(a, a + s * (w + .1f), a + s * (w + .1f) + f * (xb - xa), a + f * (xb - xa), Vector2.zero, new Vector2(w + .1f, 0), new Vector2(w + .1f, xb - xa), new Vector2(0, xb - xa), up);
            KRect(yb, o + f * (top ? xa : xb - .06f) - s * half + up * (h + .014f), s, f, w, .06f, up, Vector2.zero);
        }
        float gx0 = .55f, gx1 = total - .55f, rr = .37f;
        Material go = KM("Kit_EmitGreen"), no = KM("Kit_EmitRed");
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 inN = -s * side, outN = s * side;
            Side(sk, side * (half + .01f), -.02f, .25f, inN, 0, end);
            Deck(dk, side * (half + .01f), side * outer, .25f, up, 0, end);
            Side(cl, side * outer, -truss, .25f, outN, 0, end);
            Side(gl, side * (half + .12f), .25f, .92f, inN, gx0, gx1); Side(gl, side * (half + .12f), .25f, .92f, outN, gx0, gx1);
            float lat = side * (half + .12f);
            var path = new List<Vector3>();
            for (int a = 270; a >= 90; a -= 15) path.Add(Pt(gx0 + rr * Mathf.Cos(a * Mathf.Deg2Rad), lat, .6f + rr * Mathf.Sin(a * Mathf.Deg2Rad)));
            foreach (float x in xs) if (x > gx0 && x < gx1) path.Add(Pt(x, lat, .97f));
            for (int a = 90; a >= -90; a -= 15) path.Add(Pt(gx1 + rr * Mathf.Cos(a * Mathf.Deg2Rad), lat, .6f + rr * Mathf.Sin(a * Mathf.Deg2Rad)));
            KTubePath(hr, path, .04f, 10);
            foreach (bool top in new[] { false, true })
            {
                Vector3 nf = top ? f : -f, fc = Pt(top ? total : 0, side * (half + .01f), 0);
                KRect(dk, fc, s * side, up, .29f, .25f, nf, Vector2.zero);
                // Entry end shows a green go arrow, the other end a red no-entry cross.
                bool entry = (dir == "up") != top;
                Vector3 ic = Pt(top ? total : 0, side * (half + .155f), .13f) + nf * .004f, rt = Vector3.Cross(up, -nf);
                if (entry) KArrow(B("Kit_Glyph_" + go.name, go), ic, rt, up, nf, .12f, "up"); else KCross(B("Kit_Glyph_" + no.name, no), ic, rt, up, nf, .1f);
            }
        }
        Deck(cl, -outer, outer, -truss, Vector3.down, x0 - bl, end);
        KRect(cl, Pt(total, -outer, -truss), s, up, 2 * outer, truss + .25f, f, Vector2.zero);
        if (gapToPrev > 2 * outer + .01f) Deck(dk, outer - gapToPrev, -outer, .25f, up, 0, end);
        // Colliders: walking ramp through the step mid-rise line, balustrade sides down to the truss.
        float Hc(float x) => Mathf.Clamp((x - x0 + t * .5f) * tan, 0, rise);
        float[] cx = { 0, x0 - t * .5f, x1 - t * .5f, total };
        for (int i = 0; i < 3; i++)
        {
            Vector3 a = o + f * cx[i] + up * Hc(cx[i]), c = o + f * cx[i + 1] + up * Hc(cx[i + 1]);
            float len = Vector3.Distance(a, c); if (len < 1e-3f) continue;
            Vector3 d = (c - a) / len;
            KColRect(a - s * half, d, s, len, w);
            foreach (float side in new[] { -1f, 1f }) KColRect(a + s * side * (half + .12f) - up * truss, d, up, len, truss + 1f);
        }
    }

    // ---- ElevatorFront (landing doors in a clad portal, frames, hall lanterns, call panels, label) ----
    static void KitElevator(string id, JObject g, JObject p)
    {
        JToken edge = KT(g, "edge", id); Vector2 a2 = KV(edge[0]), b2 = KV(edge[1]); float y = KN(g, "y", id);
        Vector3 n = KEdgeNormal(a2, b2, p), ex = (KP(b2, 0) - KP(a2, 0)).normalized, up = Vector3.up, right = Vector3.Cross(up, -n);
        int cars = KI(p, "cars", 1);
        float pitch = KF(p, "pitch", 2.6f), dw = KF(p, "doorWidth", 1.1f), dh = KF(p, "doorHeight", 2.1f), ph = KF(p, "portalHeight", 2.9f), proud = KF(p, "proud", .05f);
        float open = Mathf.Clamp01(KF(p, "open", 0)), W = cars * pitch;
        if (dw + .3f > pitch || dh + .6f > ph) throw new InvalidOperationException(id + ": door does not fit the portal (pitch/portalHeight).");
        Vector3 c = Vector3.Lerp(KP(a2, y), KP(b2, y), KF(p, "t", .5f)), o = c - ex * W * .5f + n * proud;
        Material sur = KM(KS(p, "surround", "granite")), jm = KM(KS(p, "jamb", "stainless")), lm = KM(KS(p, "leaf", "hairline"));
        Batch sb = B("Kit_Clad_" + sur.name, sur), jt = B("Kit_Joint", dark), jb = B("Kit_Frame_" + jm.name, jm), lb = B("Kit_Door_" + lm.name, lm);
        var openings = new List<Vector4>();
        for (int i = 0; i < cars; i++) { float sc = pitch * (i + .5f); openings.Add(new Vector4(sc - dw * .5f - .1f, sc + dw * .5f + .1f, dh + .1f, float.NegativeInfinity)); }
        KPanels(sb, jt, o, ex, up, n, W, ph, KF(p, "panelWidth", .9f), KF(p, "panelHeight", .6f), .006f, 0, y, openings);
        foreach (float e in new[] { 0f, 1f }) KRect(sb, o + ex * W * e - n * proud, n, up, proud, ph, ex * (e * 2 - 1), Vector2.zero);
        KRect(sb, o + up * ph - n * proud, ex, n, W, proud, up, Vector2.zero);
        bool glassy = KB(p, "glass", false);
        Material amber = KM("Kit_EmitAmber"), btn = KM("Kit_Backlight"); Batch ab = B("Kit_Glyph_" + amber.name, amber), bt = B("Kit_Light_" + btn.name, btn);
        Color amberInk = new Color(1f, .62f, .1f);
        for (int i = 0; i < cars; i++)
        {
            float sc = pitch * (i + .5f); Vector3 dc = o + ex * sc;
            foreach (float e in new[] { -1f, 1f }) KBox(jb, dc + ex * e * (dw * .5f + .05f) + up * (dh + .1f) * .5f - n * .04f, ex, up, n, new Vector3(.1f, dh + .1f, .12f));
            KBox(jb, dc + up * (dh + .05f) - n * .04f, ex, up, n, new Vector3(dw + .2f, .1f, .12f));
            KBox(jb, dc + up * .01f - n * .07f, ex, up, n, new Vector3(dw, .02f, .14f));
            KRect(jt, dc - ex * dw * .5f - n * .16f, ex, up, dw, dh, n, Vector2.zero);
            foreach (float e in new[] { -1f, 1f })
            {
                float lw = dw * .5f + .01f;
                Vector3 lc = dc + ex * e * (dw * .25f + open * dw * .5f) + up * dh * .5f - n * .11f;
                if (!glassy) { KBox(lb, lc, ex, up, n, new Vector3(lw, dh, .03f), false); continue; }
                KBox(jb, lc + up * (dh * .5f - .03f), ex, up, n, new Vector3(lw, .06f, .03f)); KBox(jb, lc - up * (dh * .5f - .03f), ex, up, n, new Vector3(lw, .06f, .03f));
                KBox(jb, lc + ex * (lw * .5f - .025f), ex, up, n, new Vector3(.05f, dh, .03f)); KBox(jb, lc - ex * (lw * .5f - .025f), ex, up, n, new Vector3(.05f, dh, .03f));
                KGlass(B("Kit_Glass", glass), lc - ex * (lw * .5f - .05f) - up * (dh * .5f - .06f), ex, up, lw - .1f, dh - .12f, n);
            }
            if (!glassy && open < .01f) KRectC(jt, dc + up * dh * .5f - n * .094f, ex, up, .006f, dh - .02f, n);
            // Hall lantern / position indicator above the head.
            Vector3 ic = dc + up * (dh + .38f) + n * .012f;
            KBox(B("Kit_Screen", KM("Kit_Screen")), ic, ex, up, n, new Vector3(.46f, .16f, .024f));
            KArrow(ab, ic + n * .013f - right * .15f, right, up, n, .09f, "up"); KArrow(ab, ic + n * .013f + right * .15f, right, up, n, .09f, "down");
            KText(KS(p, "floorLabel", "1"), ic + n * .014f, n, .16f, .14f, amberInk);
            float bay = (pitch - dw) * .5f - .1f;
            if (bay >= .25f)
            {
                Vector3 pc = dc + right * (dw * .5f + .1f + bay * .5f) + up * 1.1f + n * .006f;
                KBox(jb, pc, right, up, n, new Vector3(.12f, .3f, .012f));
                KRing(bt, pc + n * .0065f + up * .055f, right, up, 0, .025f, 14, n); KRing(bt, pc + n * .0065f - up * .055f, right, up, 0, .025f, 14, n);
            }
        }
        string label = KS(p, "label", "엘리베이터");
        if (!string.IsNullOrEmpty(label))
        {
            float lw = Mathf.Min(W - .3f, 2.4f); Vector3 lc = o + ex * W * .5f + up * KF(p, "labelHeight", ph + .22f);
            Material caseM = KM("signCase");
            KBox(B("Kit_SignCase_" + caseM.name, caseM), lc + n * .025f, right, up, n, new Vector3(lw + .04f, .34f, .05f));
            KSignSide(lc + n * .052f, n, lw, .3f, new JObject(), KSignStyle("navy"), new JArray(new JObject { ["text"] = label, ["sub"] = KS(p, "labelSub", "Elevator") }), false);
        }
        KColRect(o, ex, up, W, ph);
    }

    // ---- Gate (ticket gate line: cabinets, lane indicators, readers, flaps, optional fences) ----
    static void KitGate(string id, JObject g, JObject p)
    {
        JToken edge = KT(g, "edge", id); Vector2 a2 = KV(edge[0]), b2 = KV(edge[1]); float y = KN(g, "y", id), L = Vector2.Distance(a2, b2);
        Vector3 n = KEdgeNormal(a2, b2, p), ex = (KP(b2, 0) - KP(a2, 0)).normalized, up = Vector3.up, A = KP(a2, y);
        float pitch = KF(p, "pitch", .9f), wp = KF(p, "widePitch", 1.25f), cw = KF(p, "cabinetWidth", .22f), cl = KF(p, "cabinetLength", 1.6f), h = KF(p, "height", 1f);
        var wide = new HashSet<int>(); if (p["wide"] is JArray wl) foreach (JToken t in wl) wide.Add((int)t);
        int lanes = KI(p, "lanes", -1);
        if (lanes < 0) { lanes = 0; float sum = cw; while (sum + (wide.Contains(lanes) ? wp : pitch) <= L + 1e-3f) { sum += wide.Contains(lanes) ? wp : pitch; lanes++; } }
        var centres = new List<float> { 0 };
        for (int i = 0; i < lanes; i++) centres.Add(centres[i] + (wide.Contains(i) ? wp : pitch));
        float span = centres[lanes] + cw;
        if (lanes < 1 || span > L + .01f) throw new InvalidOperationException(id + ": " + lanes + " lanes (" + span + " m) do not fit the " + L + " m gate edge.");
        float s0 = (L - span) * .5f + cw * .5f;
        string mode = KS(p, "mode", "both"); bool closed = KS(p, "flaps", "open") == "closed";
        Material body = KM(KS(p, "body", "gateBody")), hw = KM("hairline"), gm = KM("Kit_EmitGreen"), rm = KM("Kit_EmitRed"), cy = KM("Kit_EmitCyan"), sc = KM("Kit_ScreenBlue");
        Batch bb = B("Kit_Gate_" + body.name, body), tb = B("Kit_Trim_" + hw.name, hw), gl = B("Kit_Glass", glass), scr = B("Kit_Screen", KM("Kit_Screen"));
        for (int k = 0; k <= lanes; k++)
        {
            Vector3 c = A + ex * (s0 + centres[k]);
            KBox(bb, c + up * (h - .03f) * .5f, ex, up, n, new Vector3(cw, h - .03f, cl), false);
            KBox(tb, c + up * (h - .015f), ex, up, n, new Vector3(cw + .02f, .03f, cl + .02f));
            KRectC(B("Kit_Glyph_" + sc.name, sc), c + up * (h + .002f), ex, n, cw - .09f, .18f, up);
            foreach (float e in new[] { 1f, -1f })
            {
                bool allowed = mode == "both" || (e > 0 ? mode == "entry" : mode == "exit");
                Vector3 fn = n * e, fc = c + fn * (cl * .5f + .002f) + up * (h * .78f), rt = Vector3.Cross(up, -fn);
                KRectC(scr, fc, rt, up, cw - .05f, .12f, fn);
                if (allowed)
                {
                    KArrow(B("Kit_Glyph_" + gm.name, gm), fc + fn * .001f, rt, up, fn, .1f, "up");
                    KRectC(B("Kit_Glyph_" + cy.name, cy), c + up * (h + .002f) + fn * (cl * .5f - .22f), ex, n, cw - .07f, .14f, up);
                }
                else KCross(B("Kit_Glyph_" + rm.name, rm), fc + fn * .001f, rt, up, fn, .08f);
            }
            foreach (float e in new[] { -1f, 1f })
            {
                int lane = e < 0 ? k - 1 : k; if (lane < 0 || lane >= lanes) continue;
                float lw = centres[lane + 1] - centres[lane] - cw, reach = closed ? lw * .5f - .015f : .06f;
                Vector3 fo = c + ex * e * cw * .5f + up * .45f;
                KGlass(gl, fo, ex * e, up, reach, .45f, n);
                KRect(B("Kit_Glyph_" + rm.name, rm), fo + ex * e * (reach - .02f), ex * e, up, .02f, .45f, n, Vector2.zero);
                KRect(B("Kit_Glyph_" + rm.name, rm), fo + ex * e * (reach - .02f), ex * e, up, .02f, .45f, -n, Vector2.zero);
                if (closed) KColRect(fo, ex * e, up, reach, .45f);
            }
            KColBox(c + up * h * .5f, ex, up, n, new Vector3(cw, h, cl));
        }
        if (p["fences"] is JArray fences) foreach (JToken fe in fences) KRailing(KP(KV(fe[0]), y), KP(KV(fe[1]), y), KF(p, "fenceHeight", 1f));
    }
    // Glass balustrade fence: stainless posts (1.2 m) and top rail, glass infill, collider.
    static void KRailing(Vector3 a, Vector3 b, float h)
    {
        float len = Vector3.Distance(a, b); if (len < .05f) return;
        Vector3 d = (b - a) / len, up = Vector3.up;
        Material hw = KM("hairline"); Batch tb = B("Kit_Trim_" + hw.name, hw);
        int posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.2f));
        for (int i = 0; i <= posts; i++) { Vector3 q = a + d * (len * i / posts); KTube(tb, q, q + up * h, .025f, 8); }
        KTube(tb, a + up * h, b + up * h, .025f, 8);
        KGlass(B("Kit_Glass", glass), a + up * .1f, d, up, len, h - .18f, Vector3.Cross(up, d));
        KColRect(a, d, up, len, h + .1f);
    }

    // ---- Stair (granite treads + nosings, landings, stringers/soffit, balustrade or rails, ramp collider) ----
    static void KitStair(string id, JObject g, JObject p)
    {
        Vector2 foot = KV(KT(g, "foot", id)); float y = KN(g, "y", id);
        Vector3 f = KHeading(p, foot, id), s = Vector3.Cross(Vector3.up, f), up = Vector3.up, o = KP(foot, y);
        float rise = KN(p, "rise", id), w = KF(p, "width", 1.8f), tr = KF(p, "tread", .3f), land = KF(p, "landing", 1.2f);
        int n = Mathf.Max(1, Mathf.RoundToInt(rise / KF(p, "riser", .165f))), maxRun = Mathf.Max(2, KI(p, "maxRun", 18)), flights = Mathf.CeilToInt(n / (float)maxRun);
        float r = rise / n, half = w * .5f;
        Material fin = KM(KS(p, "finish", "granite")), str = KM(KS(p, "stringer", "gypsum")), nos = KM(KS(p, "nosing", "rubber"));
        Batch tb = B("Kit_Stair_" + fin.name, fin), nb = B("Kit_Nosing_" + nos.name, nos), sb = B("Kit_Stringer_" + str.name, str);
        // Nosing line (x along heading, h above y) starting at the first nosing; landings are flat runs.
        var line = new List<Vector2> { new Vector2(0, r) };
        float x = 0; int done = 0;
        for (int fl = 0; fl < flights; fl++)
        {
            int cnt = n / flights + (fl < n % flights ? 1 : 0);
            for (int i = 0; i < cnt; i++, done++)
            {
                float h0 = done * r, h1 = h0 + r;
                KRect(tb, o + f * x - s * half + up * h0, s, up, w, r, -f, new Vector2(0, h0));
                KRect(nb, o + f * x - s * (half - .03f) + up * (h1 - .03f) - f * .002f, s, up, w - .06f, .03f, -f, Vector2.zero);
                if (done == n - 1) break;
                bool landing = i == cnt - 1;
                float dx = landing ? land : tr;
                KRect(tb, o + f * x - s * half + up * h1, s, f, w, dx, up, new Vector2(0, x));
                KRect(nb, o + f * (x + .004f) - s * (half - .03f) + up * (h1 + .002f), s, f, w - .06f, .05f, up, Vector2.zero);
                if (landing) { line.Add(new Vector2(x, h1)); line.Add(new Vector2(x + land - tr, h1)); }
                x += dx;
            }
        }
        line.Add(new Vector2(x, rise));
        KRect(nb, o + f * (x + .004f) - s * (half - .03f) + up * (rise + .002f), s, f, w - .06f, .05f, up, Vector2.zero);
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a = line[i], b = line[i + 1]; float len = Vector2.Distance(a, b); if (len < 1e-3f) continue;
            Vector3 d = (f * (b.x - a.x) + up * (b.y - a.y)) / len, pa = o + f * a.x + up * a.y;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 so = pa + s * side * (half + .01f) - up * .4f;
                KRect(sb, so, d, up, len, .5f, s * side, Vector2.zero); KRect(sb, so, d, up, len, .5f, -s * side, Vector2.zero);
            }
            KRect(sb, pa - s * (half + .01f) - up * .4f, d, s, len, w + .02f, Vector3.down, Vector2.zero);
            KColRect(pa - s * half, d, s, len, w);
        }
        // sides[0] = left (-dv,du of the heading), sides[1] = right.
        string[] sides = p["sides"] is JArray sa ? new[] { (string)sa[0], (string)sa[sa.Count > 1 ? 1 : 0] } : new[] { KS(p, "sides", "balustrade"), KS(p, "sides", "balustrade") };
        Material hw = KM("hairline"); Batch rail = B("Kit_Trim_" + hw.name, hw);
        var railLine = new List<Vector2>(line) { new Vector2(x + .3f, rise) };
        for (int k = 0; k < 2; k++)
        {
            string mode = sides[k]; float side = k == 0 ? 1 : -1;
            if (mode == "none") continue;
            if (mode != "balustrade" && mode != "rail") throw new InvalidOperationException(id + ": unknown stair side " + mode);
            float lat = side * (half - (mode == "rail" ? .07f : .02f));
            KTubePath(rail, railLine.Select(q => o + f * q.x + s * lat + up * (q.y + .9f)).ToList(), .025f, 10);
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i], b = line[i + 1]; float len = Vector2.Distance(a, b); if (len < 1e-3f) continue;
                Vector3 d = (f * (b.x - a.x) + up * (b.y - a.y)) / len, pa = o + f * a.x + up * a.y + s * lat;
                if (mode == "balustrade") KGlass(B("Kit_Glass", glass), pa + up * .1f, d, up, len, .74f, s * side);
                int posts = Mathf.Max(1, Mathf.CeilToInt(len / 1.2f));
                for (int q = mode == "rail" || i == 0 ? 0 : 1; q <= posts; q++) { Vector3 pp = pa + d * (len * q / posts); KTube(rail, pp, pp + up * .9f, mode == "rail" ? .02f : .015f, 8); }
                KColRect(pa, d, up, len, 1f);
            }
        }
    }

    // ---- LockerBank (parametric carcass, door grid with locks and number plates, kiosk column, label band) ----
    static void KitLockers(string id, JObject g, JObject p)
    {
        JToken edge = KT(g, "edge", id); Vector2 a2 = KV(edge[0]), b2 = KV(edge[1]); float y = KN(g, "y", id), L = Vector2.Distance(a2, b2);
        Vector3 n = KEdgeNormal(a2, b2, p), ex = (KP(b2, 0) - KP(a2, 0)).normalized, up = Vector3.up;
        float dp = KF(p, "depth", .55f), cw = KF(p, "columnWidth", .45f), plinth = KF(p, "plinth", .1f);
        List<float> rows = p["rows"] is JArray ra ? ra.Select(t => (float)t).ToList() : new List<float> { .46f, .36f, .36f, .36f };
        int cols = Mathf.FloorToInt(L / cw + 1e-3f);
        if (cols < 1) throw new InvalidOperationException(id + ": locker edge shorter than one column.");
        float W = cols * cw, H = plinth + rows.Sum() + .06f;
        Vector3 c0 = Vector3.Lerp(KP(a2, y), KP(b2, y), .5f) + n * dp * .5f, front = c0 + n * dp * .5f;
        if (KB(p, "prop", false)) { KitProp("kit_locker", id, c0, new Vector3(W, H, dp), n); return; }
        Material body = KM(KS(p, "colour", "locker")), gly = KM("Kit_Glyph"), scr = KM("Kit_ScreenBlue"), cy = KM("Kit_EmitCyan"), navy = KM("Kit_SignNavy");
        Batch bb = B("Kit_Locker_" + body.name, body), dk = B("Kit_Joint", dark), gb = B("Kit_Glyph_" + gly.name, gly);
        KBox(bb, c0 + up * (plinth + (H - plinth) * .5f), ex, up, n, new Vector3(W, H - plinth, dp));
        KBox(dk, c0 + up * plinth * .5f - n * .03f, ex, up, n, new Vector3(W - .02f, plinth, dp - .06f), false);
        int kiosk = KI(p, "kiosk", -1);
        for (int col = 0; col < cols; col++)
        {
            Vector3 cc = front + ex * ((col + .5f) * cw - W * .5f);
            if (col == kiosk)
            {
                KRectC(dk, cc + up * (plinth + (H - plinth) * .5f) + n * .002f, ex, up, cw - .02f, H - plinth - .08f, n);
                KRectC(B("Kit_Glyph_" + scr.name, scr), cc + up * 1.3f + n * .004f, ex, up, cw - .1f, .42f, n);
                KRectC(B("Kit_Glyph_" + cy.name, cy), cc + up * .95f + n * .004f, ex, up, .12f, .06f, n);
                KRectC(gb, cc + up * .8f + n * .004f, ex, up, .14f, .012f, n);
                continue;
            }
            float yb = plinth;
            foreach (float rh in rows)
            {
                Vector3 dc = cc + up * (yb + rh * .5f);
                KBox(bb, dc + n * .008f, ex, up, n, new Vector3(cw - .012f, rh - .012f, .016f), false, false);
                KRectC(dk, dc + n * .0165f + ex * (cw * .5f - .07f), ex, up, .035f, .06f, n);
                KRectC(gb, dc + n * .0165f + up * (rh * .5f - .05f) - ex * (cw * .5f - .06f), ex, up, .05f, .022f, n);
                yb += rh;
            }
        }
        string label = KS(p, "label", "물품보관함 Lockers");
        if (!string.IsNullOrEmpty(label))
        {
            KBox(B("Kit_Sign_" + navy.name, navy), c0 + up * (H + .13f) + n * (dp * .5f - .03f), ex, up, n, new Vector3(W, .26f, .06f));
            KText(label, front + up * (H + .13f), n, Mathf.Min(W - .2f, 3f), .22f, Color.white);
        }
        KColBox(c0 + up * H * .5f, ex, up, n, new Vector3(W, H, dp));
    }

    // ---- VendingMachine (drink/snack: recessed lit window with stock; coffee: lit picture + cup niche; ticket: touch screen + slots) ----
    static void KitVending(string id, JObject g, JObject p)
    {
        Vector2 at = KV(KT(g, "point", id)); float y = KN(g, "y", id);
        Vector3 n = KHeading(p, at, id), up = Vector3.up, ex = Vector3.Cross(up, -n), c = KP(at, y);
        string kind = KS(p, "kind", "drink");
        if (kind != "drink" && kind != "snack" && kind != "coffee" && kind != "ticket") throw new InvalidOperationException(id + ": unknown vending kind " + kind);
        float W = KF(p, "width", kind == "ticket" ? .8f : .95f), D = KF(p, "depth", .8f), H = KF(p, "height", kind == "ticket" ? 1.75f : 1.83f);
        Material body = KM(KS(p, "colour", kind == "drink" ? "#B5121B" : kind == "snack" ? "#1D3F8F" : kind == "coffee" ? "#3B2417" : "machineBlue"));
        Material gly = KM("Kit_Glyph"), bl = KM("Kit_Backlight"), scr = KM("Kit_ScreenBlue"), cy = KM("Kit_EmitCyan"), hw = KM("hairline");
        Batch bb = B("Kit_Vend_" + body.name, body), dk = B("Kit_Joint", dark), gb = B("Kit_Glyph_" + gly.name, gly), lb = B("Kit_Light_" + bl.name, bl);
        Batch sb = B("Kit_Glyph_" + scr.name, scr), cb = B("Kit_Glyph_" + cy.name, cy), tb = B("Kit_Trim_" + hw.name, hw);
        Vector3 fc = c + n * D * .5f, bl0 = fc - ex * W * .5f;
        var rnd = new System.Random(id.Aggregate(17, (h, ch) => unchecked(h * 31 + ch)));
        if (kind == "drink" || kind == "snack")
        {
            // Carcass stops .12 short of the door plane; the door carries a recessed display window.
            KBox(bb, c + up * H * .5f - n * .06f, ex, up, n, new Vector3(W, H, D - .12f), false);
            foreach (float e in new[] { -1f, 1f }) KRect(bb, fc + ex * e * W * .5f - n * .12f, n, up, .12f, H, ex * e, Vector2.zero);
            KRect(bb, fc - ex * W * .5f + up * H - n * .12f, ex, n, W, .12f, up, Vector2.zero);
            float x0 = .04f, winW = W * .7f - .06f, x1 = x0 + winW, y0 = H * (kind == "drink" ? .38f : .3f), y1 = H - .12f, wh = y1 - y0;
            KDoorWindow(bb, dk, bl0, ex, up, n, W, H, x0, x1, y0, y1, .115f);
            Vector3 wc = bl0 + ex * (x0 + winW * .5f);
            KRectC(lb, wc + up * (y0 + wh * .5f) - n * .114f, ex, up, winW, wh, n);
            int rowsN = kind == "drink" ? 4 : 5, perRow = kind == "drink" ? 7 : 5;
            Material shelf = KM("white");
            for (int rI = 0; rI < rowsN; rI++)
            {
                float ry = y0 + .02f + rI * wh / rowsN, ph = wh / rowsN * (kind == "drink" ? .62f : .5f), pw = winW / perRow;
                KBox(B("Kit_Shelf_" + shelf.name, shelf), wc + up * ry - n * .06f, ex, up, n, new Vector3(winW, .012f, .1f), true, false);
                for (int k = 0; k < perRow; k++)
                {
                    Material m = KM(KitProducts[rnd.Next(KitProducts.Length)]); Vector3 pc = wc + ex * ((k + .5f) * pw - winW * .5f);
                    KBox(B("Kit_Product_" + m.name, m), pc + up * (ry + .006f + ph * .5f) - n * .07f, ex, up, n, new Vector3(pw * (kind == "drink" ? .55f : .82f), ph, .05f), false, false);
                    if (kind == "drink") KRectC(cb, pc + up * (ry + .003f) - n * .009f, ex, up, .03f, .01f, n);
                }
            }
            KGlass(B("Kit_Glass", glass), bl0 + ex * x0 + up * y0 - n * .006f, ex, up, winW, wh, n);
            KRectC(tb, wc + up * .27f + n * .002f, ex, up, winW * .64f, .2f, n);
            KRectC(dk, wc + up * .27f + n * .003f, ex, up, winW * .6f, .16f, n);
            Vector3 pcx = bl0 + ex * (x1 + (W - x1) * .5f) + n * .002f; float pw2 = W - x1 - .06f;
            KRectC(dk, pcx + up * H * .62f, ex, up, pw2, H * .42f, n);
            KRectC(sb, pcx + up * (H * .78f) + n * .001f, ex, up, pw2 * .8f, .08f, n);
            KRectC(gb, pcx + up * (H * .68f) + n * .001f - ex * pw2 * .25f, ex, up, .012f, .05f, n);
            KRectC(gb, pcx + up * (H * .68f) + n * .001f + ex * pw2 * .12f, ex, up, pw2 * .4f, .012f, n);
            KRectC(cb, pcx + up * (H * .6f) + n * .001f, ex, up, pw2 * .5f, .05f, n);
            if (kind == "snack")
                for (int kr = 0; kr < 4; kr++) for (int kc = 0; kc < 3; kc++)
                    KRectC(gb, pcx + up * (H * .52f - kr * .04f) + ex * ((kc - 1) * pw2 * .25f) + n * .001f, ex, up, pw2 * .18f, .025f, n);
        }
        else if (kind == "coffee")
        {
            KBox(bb, c + up * H * .5f, ex, up, n, new Vector3(W, H, D), false);
            Vector3 pc = bl0 + ex * W * .42f + n * .002f;
            KRectC(lb, pc + up * H * .7f, ex, up, W * .72f, H * .42f, n);
            KText(KS(p, "text", "COFFEE"), pc + up * H * .7f + n * .001f, n, W * .6f, .16f, new Color(.25f, .14f, .07f));
            KRectC(tb, pc + up * .95f, ex, up, .32f, .36f, n); KRectC(dk, pc + up * .95f + n * .001f, ex, up, .28f, .32f, n);
            Vector3 panel = bl0 + ex * W * .9f + n * .002f;
            KRectC(sb, panel + up * 1.25f, ex, up, W * .14f, .12f, n);
            KRectC(cb, panel + up * 1.05f, ex, up, W * .12f, .05f, n);
            KRectC(gb, panel + up * .92f, ex, up, .012f, .05f, n);
        }
        else
        {
            KBox(bb, c + up * H * .5f, ex, up, n, new Vector3(W, H, D), false);
            Vector3 mid = fc + n * .002f;
            KBox(dk, mid + up * 1.22f + n * .008f, ex, up, n, new Vector3(W * .78f, .48f, .016f));
            KRectC(sb, mid + up * 1.22f + n * .017f, ex, up, W * .72f, .42f, n);
            KRectC(cb, mid + up * .92f - ex * W * .25f, ex, up, .1f, .02f, n);
            KRectC(dk, mid + up * .92f + ex * W * .2f, ex, up, .16f, .04f, n);
            KRectC(dk, mid + up * .92f + ex * W * .02f, ex, up, .03f, .06f, n);
            KRectC(tb, mid + up * .72f, ex, up, .24f, .06f, n); KRectC(dk, mid + up * .72f + n * .001f, ex, up, .2f, .025f, n);
            Material navy = KM("Kit_SignNavy");
            KRectC(B("Kit_Sign_" + navy.name, navy), mid + up * (H - .12f), ex, up, W - .04f, .2f, n);
            KText(KS(p, "text", "승차권 발매기"), mid + up * (H - .12f) + n * .001f, n, W - .1f, .17f, Color.white);
        }
        KColBox(c + up * H * .5f, ex, up, n, new Vector3(W, H, D));
    }
    // Door face (W x H from its bottom-left bl, facing n) with a window [x0,x1]x[y0,y1] recessed by `depth` (reveals in `reveal`).
    static void KDoorWindow(Batch skin, Batch reveal, Vector3 bl, Vector3 ex, Vector3 up, Vector3 n, float W, float H, float x0, float x1, float y0, float y1, float depth)
    {
        KRect(skin, bl, ex, up, W, y0, n, Vector2.zero);
        KRect(skin, bl + up * y1, ex, up, W, H - y1, n, Vector2.zero);
        KRect(skin, bl + up * y0, ex, up, x0, y1 - y0, n, Vector2.zero);
        KRect(skin, bl + ex * x1 + up * y0, ex, up, W - x1, y1 - y0, n, Vector2.zero);
        Vector3 w0 = bl + ex * x0 + up * y0 - n * depth; float ww = x1 - x0, wh = y1 - y0;
        KRect(reveal, w0, ex, n, ww, depth, up, Vector2.zero); KRect(reveal, w0 + up * wh, ex, n, ww, depth, -up, Vector2.zero);
        KRect(reveal, w0, n, up, depth, wh, ex, Vector2.zero); KRect(reveal, w0 + ex * ww, n, up, depth, wh, -ex, Vector2.zero);
    }

    // ---- Bench (beam-mounted seat shells with backrests and armrests) ----
    static void KitBench(string id, JObject g, JObject p)
    {
        Vector2 at = KV(KT(g, "point", id)); float y = KN(g, "y", id);
        Vector3 n = KHeading(p, at, id), up = Vector3.up, ex = Vector3.Cross(up, -n), c = KP(at, y);
        float len = KF(p, "length", 1.8f), sh = KF(p, "seatHeight", .44f); bool back = KB(p, "back", true);
        int seats = KI(p, "seats", Mathf.Max(1, Mathf.RoundToInt(len / .6f))); float pitch = len / seats;
        Material seat = KM(KS(p, "finish", "seat")), fr = KM(KS(p, "frame", "hairline"));
        Batch sb = B("Kit_Seat_" + seat.name, seat), fb = B("Kit_Trim_" + fr.name, fr);
        KBox(fb, c + up * (sh - .09f) - n * .05f, ex, up, n, new Vector3(len - .1f, .06f, .08f));
        foreach (float e in new[] { -1f, 1f })
        {
            Vector3 lc = c + ex * e * (len * .5f - .2f) - n * .05f;
            KBox(fb, lc + up * (sh - .12f) * .5f, ex, up, n, new Vector3(.06f, sh - .12f, .06f));
            KBox(fb, lc + up * .01f, ex, up, n, new Vector3(.1f, .02f, .5f));
        }
        Vector3 tilt = (up * Mathf.Cos(.17f) - n * Mathf.Sin(.17f)).normalized, tz = Vector3.Cross(tilt, ex).normalized;
        for (int k = 0; k < seats; k++)
        {
            Vector3 sc = c + ex * ((k + .5f) * pitch - len * .5f);
            KBox(sb, sc + up * (sh - .02f), ex, up, n, new Vector3(pitch - .05f, .04f, .44f));
            if (back) KBox(sb, sc - n * .2f + up * sh + tilt * .22f, ex, tilt, tz, new Vector3(pitch - .05f, .4f, .03f));
            if (k == 0) continue;
            Vector3 ac = c + ex * (k * pitch - len * .5f);
            KBox(fb, ac + up * (sh + .2f), ex, up, n, new Vector3(.04f, .03f, .36f));
            KBox(fb, ac + up * (sh + .1f) + n * .12f, ex, up, n, new Vector3(.03f, .2f, .03f));
        }
        KColBox(c + up * (sh + (back ? .2f : 0)) * .5f, ex, up, n, new Vector3(len, sh + (back ? .2f : 0), .5f));
    }

    // ---- Lighting (sparse real lights: one at a point, or a grid inside a polygon) ----
    static void KitLighting(string id, JObject g, JObject p)
    {
        float y = KN(g, "y", id);
        if (g["point"] is JArray pt) { KLight(id, KP(KV(pt), y), p); return; }
        List<Vector2> poly = KPoly(KT(g, "polygon", id));
        var rings = new List<List<Vector2>> { poly }; rings.AddRange(KHoles(g));
        JArray lp = p["pitch"] as JArray; float pa = lp != null ? (float)lp[0] : 6f, pb = lp != null ? (float)lp[1] : 6f;
        float u0 = poly.Min(q => q.x), u1 = poly.Max(q => q.x), v0 = poly.Min(q => q.y), v1 = poly.Max(q => q.y);
        int k = 0;
        for (float u = u0 + pa * .5f; u < u1; u += pa)
            for (float v = v0 + pb * .5f; v < v1; v += pb)
                if (KInside(rings, new Vector2(u, v))) KLight(id + " " + k++, KP(new Vector2(u, v), y), p);
    }

    // ================= enhancement-round types (2026-09-25): Balustrade, DisplayBoard, Counter, DoorSet, Fixture =================
    static readonly Color KAmber = new Color(1f, .62f, .1f), KInkDark = new Color(.07f, .07f, .08f);
    // "clear" = transparent low-iron glass (Kit_GlassClear), "frosted" = translucent satin glass (Kit_GlassFrosted),
    // "teal" = the station's green-clear glass, else any material key.
    static Material KGlassMat(string which)
    {
        if (which == "teal") return glass;
        if (which != "clear" && which != "frosted") return KM(which);
        bool frosted = which == "frosted"; string name = frosted ? "Kit_GlassFrosted" : "Kit_GlassClear";
        if (kitMats.TryGetValue(name, out Material m)) return m;
        m = KitCreate(name, lit, x =>
        {
            x.SetColor("_BaseColor", frosted ? new Color(.9f, .93f, .94f, .78f) : new Color(.8f, .9f, .92f, .18f));
            x.SetFloat("_Smoothness", frosted ? .35f : .95f); x.SetFloat("_Metallic", 0);
            x.SetFloat("_Surface", 1); x.SetFloat("_Blend", 0); x.SetFloat("_ZWrite", 0);
            x.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); x.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            x.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); x.SetOverrideTag("RenderType", "Transparent"); x.renderQueue = (int)RenderQueue.Transparent;
        });
        kitMats[name] = m;
        return m;
    }
    // Texture/material lightbox (w x h centred at c, facing n); false when the params name neither.
    static bool KLightbox(JObject p, Vector3 c, Vector3 right, float w, float h, Vector3 n)
    {
        string tex = KS(p, "texture", null), mat = KS(p, "material", null);
        if (tex == null && mat == null) return false;
        Material lm = tex != null ? KitTexMat(tex) : KM(mat);
        Vector3 bl = c - right * w * .5f - Vector3.up * h * .5f;
        B("Kit_Lightbox_" + lm.name, lm).TexQuad(bl, bl + right * w, bl + right * w + Vector3.up * h, bl + Vector3.up * h, n);
        return true;
    }
    // Two suspension rods from the top of a hung object (centre xy at c, top height topY) to ceilingY.
    static void KRods(Vector3 c, Vector3 ex, float span, float topY, float ceilingY)
    {
        if (ceilingY <= topY + .02f) return;
        Material hw = KM("hairline"); Batch rods = B("Kit_Trim_" + hw.name, hw);
        foreach (float s in new[] { -span * .5f, span * .5f })
        {
            Vector3 rb = new Vector3(c.x, topY, c.z) + ex * s;
            KTube(rods, rb, rb + Vector3.up * (ceilingY - topY), .012f, 8);
            KRingH(rods, rb + Vector3.up * (ceilingY - topY - .003f), 0, .045f, 12, Vector3.down);
        }
    }

    // ---- Balustrade (guard along a polyline: glass / bars / solid infill, posts, shoe, round/flat rail) ----
    static void KitBalustrade(string id, JObject g, JObject p)
    {
        List<Vector2> line = KPoly(KT(g, "polyline", id)); float y = KN(g, "y", id);
        float h = KF(p, "height", 1.1f), pitch = KF(p, "postPitch", 1.2f);
        string panel = KS(p, "panel", "glass"), rail = KS(p, "rail", "round");
        if (panel != "glass" && panel != "bars" && panel != "solid") throw new InvalidOperationException(id + ": unknown balustrade panel " + panel);
        if (rail != "round" && rail != "flat" && rail != "none") throw new InvalidOperationException(id + ": unknown balustrade rail " + rail);
        bool shoe = KB(p, "shoe", panel != "solid"), collide = KB(p, "collider", true);
        Material rm = KM(KS(p, "railFinish", "stainless")); Batch rb = B("Kit_Rail_" + rm.name, rm);
        Material gm = KGlassMat(KS(p, "glass", "clear")), pm = KM(KS(p, "panelFinish", "gypsum"));
        Vector3 up = Vector3.up;
        float shoeH = shoe ? KF(p, "shoeHeight", .1f) : 0, top = rail == "none" ? h : h - .05f;
        var railPath = new List<Vector3>();
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a2 = line[i], b2 = line[i + 1]; float len = Vector2.Distance(a2, b2);
            if (len < .01f) continue;
            Vector3 a = KP(a2, y), c = KP(b2, y), d = (c - a) / len, n = KEdgeNormal(a2, b2, p);
            if (shoe) KBox(rb, (a + c) * .5f + up * shoeH * .5f, d, up, n, new Vector3(len, shoeH, .07f), false);
            if (panel == "glass")
            {
                KGlass(B("Kit_Glass_" + gm.name, gm), a + up * (shoeH + .01f), d, up, len, top - shoeH - .03f, n);
                if (!shoe || pitch <= 0)
                    for (float s = .3f; s < len; s += 1f)
                        foreach (float hh in shoe ? new[] { top - .08f } : new[] { .15f, top - .08f })
                            KBox(rb, a + d * s + up * hh, d, up, n, new Vector3(.06f, .05f, .05f));
            }
            else if (panel == "bars")
            {
                KTube(rb, a + up * (shoeH + .1f), c + up * (shoeH + .1f), .015f, 6);
                int bars = Mathf.Max(1, Mathf.RoundToInt(len / .11f));
                for (int k = 1; k < bars; k++) { Vector3 q = a + d * (len * k / bars); KTube(rb, q + up * (shoeH + .02f), q + up * top, .01f, 6); }
            }
            else KBox(B("Kit_Parapet_" + pm.name, pm), (a + c) * .5f + up * (shoeH + (top - shoeH) * .5f), d, up, n, new Vector3(len, top - shoeH, .1f));
            if (pitch > 0 && panel != "solid")
            {
                int posts = Mathf.Max(1, Mathf.CeilToInt(len / pitch));
                for (int k = i == 0 ? 0 : 1; k <= posts; k++) KBox(rb, a + d * (len * k / posts) + up * (shoeH + (top - shoeH) * .5f), d, up, n, new Vector3(.05f, top - shoeH, .05f));
            }
            if (rail == "flat") KBox(rb, (a + c) * .5f + up * (h - .02f), d, up, n, new Vector3(len + .04f, .04f, panel == "solid" ? .14f : .08f));
            if (railPath.Count == 0) railPath.Add(a + up * (h - .025f));
            railPath.Add(c + up * (h - .025f));
            if (collide) KColRect(a, d, up, len, Mathf.Max(h, 1f));
        }
        if (rail == "round") KTubePath(rb, railPath, .025f, 10);
        // Optional second (hand) rail on short brackets, on the faceToward side: handrail {height .9, offset .07, radius .02,
        // finish (railFinish), bracketPitch 1.2}; stops .15 short of each polyline end.
        if (p["handrail"] is JObject hr)
        {
            float hh = KF(hr, "height", .9f), off = KF(hr, "offset", .07f), hrad = KF(hr, "radius", .02f), bp = Mathf.Max(.3f, KF(hr, "bracketPitch", 1.2f));
            Material hm = KM(KS(hr, "finish", KS(p, "railFinish", "stainless"))); Batch hb = B("Kit_Rail_" + hm.name, hm);
            var pts = new List<Vector3>(); var norms = new List<Vector3>();
            for (int i = 0; i < line.Count; i++)
            {
                Vector3 nsum = Vector3.zero;
                if (i > 0 && Vector2.Distance(line[i - 1], line[i]) > .01f) nsum += KEdgeNormal(line[i - 1], line[i], p);
                if (i + 1 < line.Count && Vector2.Distance(line[i], line[i + 1]) > .01f) nsum += KEdgeNormal(line[i], line[i + 1], p);
                if (nsum.sqrMagnitude < 1e-6f) continue;
                nsum.Normalize();
                pts.Add(KP(line[i], y + hh)); norms.Add(nsum);
            }
            if (pts.Count >= 2)
            {
                pts[0] += (pts[1] - pts[0]).normalized * .15f;
                pts[pts.Count - 1] += (pts[pts.Count - 2] - pts[pts.Count - 1]).normalized * .15f;
                var path = pts.Select((q, i) => q + norms[i] * off).ToList();
                KTubePath(hb, path, hrad, 10);
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    float len = Vector3.Distance(pts[i], pts[i + 1]); int nb = Mathf.Max(1, Mathf.CeilToInt(len / bp));
                    for (int k = i == 0 ? 0 : 1; k <= nb; k++)
                    {
                        float t = (float)k / nb; Vector3 q = Vector3.Lerp(pts[i], pts[i + 1], t), nn = Vector3.Lerp(norms[i], norms[i + 1], t).normalized;
                        KTube(hb, q + nn * .02f - Vector3.up * .05f, q + nn * off - Vector3.up * hrad, .008f, 6);
                    }
                }
            }
        }
    }

    // ---- DisplayBoard (LED departure/arrival boards, screens, LED bands, clocks, timetables; hanging / wall / floor totem) ----
    static void KitDisplayBoard(string id, JObject g, JObject p)
    {
        Vector2 at = KV(KT(g, "point", id)); float y = KN(g, "y", id);
        Vector3 n = KHeading(p, at, id), up = Vector3.up, ex = Vector3.Cross(up, -n);
        string kind = KS(p, "kind", "departure"), mount = KS(p, "mount", "hanging");
        if (mount != "hanging" && mount != "wall" && mount != "floor") throw new InvalidOperationException(id + ": unknown board mount " + mount);
        float w = KF(p, "width", kind == "clock" ? .9f : 2.4f);
        float h = KF(p, "height", kind == "ledband" ? .3f : kind == "clock" ? .35f : kind == "timetable" ? 1.2f : .6f), d = KF(p, "depth", .12f);
        float bottom = mount == "floor" ? y + KF(p, "standHeight", 1.2f) : y;
        Vector3 c = KP(at, bottom + h * .5f);
        if (mount == "wall") c += n * (d * .5f + KF(p, "standoff", .02f));
        Material housing = KM(KS(p, "housing", "dark"));
        KBox(B("Kit_Board_" + housing.name, housing), c, ex, up, n, new Vector3(w + .06f, h + .06f, d));
        KBoardFace(id, c + n * (d * .5f + .002f), n, w, h, kind, p, false);
        if (KB(p, "doubleSided", mount == "hanging")) KBoardFace(id, c - n * (d * .5f + .002f), -n, w, h, kind, p, true);
        if (mount == "hanging") KRods(c, ex, w * .7f, bottom + h + .03f, KF(p, "ceilingY", bottom + h + .8f));
        else if (mount == "floor")
        {
            Material hw = KM("hairline"); Batch st = B("Kit_Trim_" + hw.name, hw);
            Vector3 f = KP(at, y);
            foreach (float s in new[] { -w * .3f, w * .3f }) KBox(st, f + ex * s + up * (bottom - y) * .5f, ex, up, n, new Vector3(.08f, bottom - y, .08f));
            KBox(st, f + up * .015f, ex, up, n, new Vector3(w * .8f, .03f, .45f), false);
            KColBox(f + up * (bottom + h - y) * .5f, ex, up, n, new Vector3(w, bottom + h - y, .45f));
        }
    }
    static void KBoardFace(string id, Vector3 fc, Vector3 n, float w, float h, string kind, JObject p, bool back)
    {
        Vector3 right = Vector3.Cross(Vector3.up, -n), up = Vector3.up;
        float iw = w - .04f, ih = h - .04f;
        Material screen = KM("Kit_Screen");
        switch (kind)
        {
            case "screen":
                if (!KLightbox(p, fc, right, iw, ih, n)) KRectC(B("Kit_Glyph_Kit_ScreenBlue", KM("Kit_ScreenBlue")), fc, right, up, iw, ih, n);
                return;
            case "ledband":
            {
                KRectC(B("Kit_Screen", screen), fc, right, up, iw, ih, n);
                string text = KS(p, "title", null) ?? string.Join("   ", (p["rows"] as JArray ?? new JArray()).OfType<JObject>().Select(r => string.Join(" ", new[] { "time", "train", "dest", "track", "status" }.Select(k => KS(r, k, "")).Where(s => s.Length > 0))));
                KText(text, fc + n * .001f, n, iw - .06f, ih * .9f, KC(p, "ink", KAmber));
                return;
            }
            case "clock":
                KRectC(B("Kit_Screen", screen), fc, right, up, iw, ih, n);
                KText(KS(p, "text", "12:00"), fc + n * .001f, n, iw * .9f, ih * .9f, KC(p, "ink", KAmber));
                return;
            case "departure": case "arrival": case "timetable":
            {
                bool light = kind == "timetable";
                Material bg = light ? KM("Kit_Backlight") : screen;
                KRectC(B("Kit_Screen_" + bg.name, bg), fc, right, up, iw, ih, n);
                JArray rows = p["rows"] as JArray ?? new JArray();
                int nr = Mathf.Max(rows.Count, KI(p, "rowCount", 4));
                float th = ih / (nr + 1.4f) * 1.4f, rh = (ih - th) / nr;
                string title = KS(p, "title", kind == "departure" ? "출발  Departures" : kind == "arrival" ? "도착  Arrivals" : "열차 시간표  Timetable");
                Vector3 tc = fc + up * (ih * .5f - th * .5f) + n * .001f;
                if (light) KRectC(B("Kit_Sign_Kit_SignNavy", KM("Kit_SignNavy")), tc, right, up, iw, th, n);
                KText(title, tc + n * .001f, n, iw * .6f, th * .85f, light ? Color.white : KAmber);
                Material sep = KM(light ? "#9AA0A6" : "#2B2B2B"); Batch sb = B("Kit_Glyph_" + sep.name, sep);
                string[] keys = { "time", "train", "dest", "track", "status" };
                float[] frac = { .14f, .22f, .34f, .1f, .2f };
                for (int r = 0; r <= nr; r++) KRectC(sb, fc + up * (ih * .5f - th - r * rh) + n * .0015f, right, up, iw - .04f, .006f, n);
                for (int r = 0; r < rows.Count; r++)
                {
                    JObject row = rows[r] as JObject; if (row == null) continue;
                    float yc = ih * .5f - th - (r + .5f) * rh, x = -iw * .5f;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        float cw = iw * frac[k]; string v = KS(row, keys[k], "");
                        Color ink = light ? KInkDark : k == 2 ? Color.white : KAmber;
                        if (k == 4 && (v.Contains("지연") || v.IndexOf("delay", StringComparison.OrdinalIgnoreCase) >= 0)) ink = new Color(1f, .2f, .15f);
                        KText(v, fc + right * (x + cw * .5f) + up * yc + n * .001f, n, cw * .94f, rh * .8f, ink);
                        x += cw;
                    }
                }
                return;
            }
            default: throw new InvalidOperationException(id + ": unknown board kind " + kind);
        }
    }

    // ---- Counter (ticket / information / service counter: customer-side front polyline) ----
    static void KitCounter(string id, JObject g, JObject p)
    {
        List<Vector2> line = KPoly(KT(g, "polyline", id)); float y = KN(g, "y", id);
        float depth = KF(p, "depth", .7f), H = KF(p, "height", 1.05f);
        Material top = KM(KS(p, "top", "granite")), body = KM(KS(p, "body", "counterWood")), hw = KM("hairline");
        Batch tb = B("Kit_CounterTop_" + top.name, top), bb = B("Kit_CounterBody_" + body.name, body), jb = B("Kit_Joint", dark), sb = B("Kit_Trim_" + hw.name, hw);
        Vector3 up = Vector3.up;
        var segs = new List<(Vector3 a, Vector3 d, Vector3 n, float s0, float len)>(); float total = 0;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a2 = line[i], b2 = line[i + 1]; float len = Vector2.Distance(a2, b2);
            if (len < .01f) continue;
            Vector3 a = KP(a2, y), c = KP(b2, y), d = (c - a) / len, n = KEdgeNormal(a2, b2, p), mid = (a + c) * .5f - n * depth * .5f;
            KBox(bb, mid + up * (.1f + (H - .14f) * .5f), d, up, n, new Vector3(len, H - .14f, depth));
            KBox(jb, mid - n * .03f + up * .05f, d, up, n, new Vector3(len, .1f, depth - .06f), false);
            KBox(tb, mid + n * .02f + up * (H - .02f), d, up, n, new Vector3(len + .02f, .04f, depth + .04f));
            KRect(sb, a + n * .002f + up * (H - .12f), d, up, len, .03f, n, Vector2.zero);
            if (KB(p, "collider", true)) KColBox(mid + up * H * .5f, d, up, n, new Vector3(len, H, depth));
            segs.Add((a, d, n, total, len)); total += len;
        }
        if (segs.Count == 0) throw new InvalidOperationException(id + ": counter polyline is empty.");
        Func<float, (Vector3 pos, Vector3 d, Vector3 n)> At = s =>
        {
            var sg = segs.LastOrDefault(q => q.s0 <= s) ; if (sg.len == 0) sg = segs[0];
            return (sg.a + sg.d * Mathf.Clamp(s - sg.s0, 0, sg.len), sg.d, sg.n);
        };
        var stations = new List<float>();
        Material gm = KGlassMat(KS(p, "glass", "clear"));
        if (p["screens"] is JArray screens)
            foreach (JObject sc in screens)
            {
                float s = KF(sc, "at", total * .5f), sw = KF(sc, "width", 1.2f), sh = KF(sc, "height", .75f);
                var (pos, d, n) = At(s); stations.Add(s);
                Vector3 o = pos - d * sw * .5f - n * .08f + up * H;
                KGlass(B("Kit_Glass_" + gm.name, gm), o + up * .06f, d, up, sw, sh - .06f, n);
                KBox(sb, o + d * sw * .5f + up * (sh - .015f), d, up, n, new Vector3(sw, .03f, .03f));
                foreach (float e in new[] { 0f, sw }) KBox(sb, o + d * e + up * sh * .5f, d, up, n, new Vector3(.03f, sh, .05f));
                KBox(jb, o + d * sw * .5f + up * .03f, d, up, n, new Vector3(sw, .06f, .05f), false);
                KRing(sb, o + d * sw * .5f + up * .32f + n * .004f, d, up, 0, .06f, 12, n);
                KRectC(jb, o + d * sw * .5f + up * .07f + n * .03f, d, up, .32f, .03f, n);
                string lbl = KS(sc, "label", null);
                if (lbl != null)
                {
                    Vector3 lc = o + d * sw * .5f + up * (sh + .12f);
                    KBox(B("Kit_Sign_Kit_SignNavy", KM("Kit_SignNavy")), lc, d, up, n, new Vector3(Mathf.Min(sw, .8f), .18f, .03f));
                    KText(lbl, lc + n * .017f, n, Mathf.Min(sw, .8f) - .04f, .16f, Color.white);
                }
            }
        if (KB(p, "equipment", true))
        {
            if (stations.Count == 0) for (float s = .8f; s < total - .4f; s += 1.6f) stations.Add(s);
            Material scr = KM("Kit_ScreenBlue");
            foreach (float s in stations)
            {
                var (pos, d, n) = At(s);
                Vector3 mc = pos - n * (depth * .7f) + up * (H + .3f) - d * .15f;
                KBox(jb, mc, d, up, n, new Vector3(.46f, .3f, .04f));
                KRectC(B("Kit_Glyph_" + scr.name, scr), mc - n * .021f, d, up, .42f, .26f, -n);
                KBox(jb, mc - up * .2f, d, up, n, new Vector3(.05f, .12f, .05f), false);
                KBox(jb, pos - n * (depth * .55f) + up * (H + .08f) + d * .3f, d, up, n, new Vector3(.2f, .16f, .25f), false);
                KBox(jb, pos - n * .18f + up * (H + .025f) + d * .35f, d, up, n, new Vector3(.08f, .05f, .16f), false);
            }
        }
        if (p["backPanel"] is JObject bp)
        {
            float bh = KF(bp, "height", 2.4f), off = KF(bp, "offset", 1.4f);
            Material pm = KM(KS(bp, "finish", "wood")); Batch pb = B("Kit_CounterBack_" + pm.name, pm);
            foreach (var sg in segs) KBox(pb, sg.a + sg.d * sg.len * .5f - sg.n * (depth + off) + up * bh * .5f, sg.d, up, sg.n, new Vector3(sg.len, bh, .06f));
            string text = KS(bp, "text", null);
            if (text != null)
            {
                // Centred on the longest segment so corner panels of bent counters never cut through the band.
                var sgL = segs.OrderByDescending(q => q.len).First(); Vector3 pos = sgL.a + sgL.d * sgL.len * .5f, d = sgL.d, n = sgL.n; float lw = Mathf.Min(sgL.len - .3f, 4f);
                Vector3 lc = pos - n * (depth + off - .035f) + up * (bh - .3f);
                KRectC(B("Kit_Sign_Kit_SignNavy", KM("Kit_SignNavy")), lc, d, up, lw, .36f, n);
                KText(text, lc + n * .002f, n, lw - .1f, .32f, Color.white);
            }
        }
        string label = KS(p, "label", null);
        if (label != null)
        {
            var sgL = segs.OrderByDescending(q => q.len).First(); Vector3 pos = sgL.a + sgL.d * sgL.len * .5f, d = sgL.d, n = sgL.n; float lw = Mathf.Min(sgL.len - .3f, 2.4f);
            Vector3 lc = pos + n * .004f + up * (H * .55f);
            KRectC(B("Kit_Sign_Kit_SignNavy", KM("Kit_SignNavy")), lc, d, up, lw, .26f, n);
            KText(label, lc + n * .002f, n, lw - .08f, .24f, Color.white);
        }
    }

    // ---- DoorSet (glass entrance door bank in a stainless frame; optional vestibule; exit number badge; header sign) ----
    static void KitDoorSet(string id, JObject g, JObject p)
    {
        JToken edge = KT(g, "edge", id); Vector2 a2 = KV(edge[0]), b2 = KV(edge[1]); float y = KN(g, "y", id), L = Vector2.Distance(a2, b2);
        Vector3 n = KEdgeNormal(a2, b2, p), ex = (KP(b2, 0) - KP(a2, 0)).normalized, up = Vector3.up, A = KP(a2, y);
        float dh = KF(p, "doorHeight", 2.4f), fh = KF(p, "frameHeight", 3f), vest = KF(p, "vestibule", 0);
        if (fh < dh + .1f) throw new InvalidOperationException(id + ": frameHeight must exceed doorHeight by .1.");
        var doors = new List<(float c, float w, string type, float open)>();
        if (p["doors"] is JArray ds)
            foreach (JObject d in ds) doors.Add((KF(d, "at", L * .5f), KF(d, "width", 1.8f), KS(d, "type", "auto"), Mathf.Clamp01(KF(d, "open", 0))));
        else
        {
            int count = KI(p, "count", 1); float w = KF(p, "doorWidth", 1.8f), pitch = KF(p, "pitch", w + 1.2f);
            for (int k = 0; k < count; k++) doors.Add((L * .5f + (k - (count - 1) * .5f) * pitch, w, KS(p, "type", "auto"), Mathf.Clamp01(KF(p, "open", 0))));
        }
        foreach (var d in doors)
        {
            if (d.c - d.w * .5f < -.01f || d.c + d.w * .5f > L + .01f) throw new InvalidOperationException(id + ": door outside the door-set edge.");
            if (d.type != "auto" && d.type != "swing") throw new InvalidOperationException(id + ": unknown door type " + d.type);
        }
        Material fm = KM(KS(p, "frame", "stainless")), hw = KM("hairline"), gm = KGlassMat(KS(p, "glass", "clear"));
        Batch fr = B("Kit_Frame_" + fm.name, fm), gl = B("Kit_Glass_" + gm.name, gm), kp = B("Kit_Trim_" + hw.name, hw), jb = B("Kit_Joint", dark);
        bool transom = KB(p, "transom", true), side = KB(p, "sidelights", true);
        var gaps = doors.Select(d => new Vector2(d.c - d.w * .5f, d.c + d.w * .5f)).ToList();
        void Line(Vector3 o, float flip)
        {
            var posts = new List<float> { 0, L }; foreach (Vector2 gp in gaps) { posts.Add(gp.x); posts.Add(gp.y); }
            foreach (float s in posts.Distinct()) KBox(fr, o + ex * s + up * fh * .5f, ex, up, n, new Vector3(.08f, fh, .15f));
            KBox(fr, o + ex * L * .5f + up * (dh + .05f), ex, up, n, new Vector3(L, .1f, .15f));
            KBox(fr, o + ex * L * .5f + up * (fh - .04f), ex, up, n, new Vector3(L, .08f, .15f));
            if (transom && fh - dh > .25f) KGlass(gl, o + up * (dh + .1f), ex, up, L, fh - dh - .18f, n);
            foreach (Vector2 run in KRuns(L, gaps))
            {
                float rl = run.y - run.x; if (rl < .1f) continue;
                if (side)
                {
                    KBox(fr, o + ex * (run.x + rl * .5f) + up * .05f, ex, up, n, new Vector3(rl, .1f, .12f), false);
                    KGlass(gl, o + ex * run.x + up * .1f, ex, up, rl, dh - .1f, n);
                }
                KColRect(o + ex * run.x, ex, up, rl, fh);
            }
            foreach (var d in doors)
            {
                float s0 = d.c - d.w * .5f, lw = d.w * .5f;
                if (d.type == "auto")
                {
                    for (int k = 0; k < 2; k++)
                    {
                        float dir = k == 0 ? -1 : 1, ls = s0 + lw * k + dir * d.open * lw - (k == 1 ? .015f : 0);
                        KDoorLeaf(fr, gl, kp, o + ex * ls - n * flip * .1f, ex, n, lw + .015f, dh - .01f, false);
                    }
                    KBox(kp, o + ex * d.c + up * (dh - .1f) - n * flip * .2f, ex, up, n, new Vector3(Mathf.Min(L, d.w * 2f), .18f, .12f));
                    KBox(jb, o + ex * d.c + up * (dh - .03f) + n * flip * .09f, ex, up, n, new Vector3(.18f, .05f, .04f), false);
                }
                else
                {
                    float angle = d.open * 85 * Mathf.Deg2Rad;
                    for (int k = 0; k < 2; k++)
                    {
                        bool left = k == 0; Vector3 hinge = o + ex * (left ? s0 : s0 + d.w);
                        Vector3 along = (left ? ex : -ex) * Mathf.Cos(angle) - n * flip * Mathf.Sin(angle);
                        KDoorLeaf(fr, gl, kp, hinge, along, Vector3.Cross(Vector3.up, along), lw - .01f, dh - .01f, true);
                    }
                }
            }
        }
        Line(A, 1);
        if (vest > 0)
        {
            Line(A - n * vest, 1);
            foreach (float e in new[] { 0f, L })
            {
                KGlass(gl, A + ex * e - n * vest + up * .05f, n, up, vest, fh - .1f, ex);
                KColRect(A + ex * e - n * vest, n, up, vest, fh);
            }
            Material mat = KM(KS(p, "mat", "#2E3133"));
            KRect(B("Kit_Floor_" + mat.name, mat), A - n * (vest - .1f) + ex * .1f + up * .006f, ex, n, L - .2f, vest - .2f, up, Vector2.zero);
            KFill(B("Kit_Ceiling_" + KM("Kit_GridWhite").name, KM("Kit_GridWhite")), new List<Vector2> { a2, b2, b2 - KV2(n) * vest, a2 - KV2(n) * vest }, new List<List<Vector2>>(), y + fh, Vector3.down, a2);
        }
        // Number badge and header sign on the outside face (line A, facing n), the inside face (innermost line, facing -n) or both.
        string sides = KS(p, "signSide", "both");
        if (sides != "both" && sides != "inside" && sides != "outside") throw new InvalidOperationException(id + ": signSide must be both|inside|outside.");
        string number = KS(p, "number", null);
        JToken sign = p["sign"];
        foreach (bool outside in new[] { true, false })
        {
            if (sides != "both" && (sides == "outside") != outside) continue;
            Vector3 f = outside ? n : -n, O = outside ? A : A - n * vest, right = Vector3.Cross(up, -f);
            if (number != null)
            {
                float bs = KF(p, "numberSize", .45f), by = fh - dh > bs + .1f ? dh + (fh - dh) * .5f : fh + bs * .5f + .08f;
                Vector3 bc = O + ex * KF(p, "numberAt", L * .5f) + up * by + f * .085f;
                KRectC(B("Kit_Sign_" + KM("yellow").name, KM("yellow")), bc, right, up, bs, bs, f);
                KText(number, bc + f * .002f, f, bs * .9f, bs * .95f, KInkDark);
            }
            if (sign != null && sign.Type != JTokenType.Null)
            {
                JArray items = sign is JArray arr ? arr : new JArray(new JObject { ["text"] = (string)sign });
                float sw = KF(p, "signWidth", Mathf.Min(L - .2f, 4.8f)), sh = KF(p, "signHeight", .45f);
                Vector3 sc = O + ex * L * .5f + up * (fh + sh * .5f + .08f) + f * .06f;
                Material caseM = KM("signCase");
                KBox(B("Kit_SignCase_" + caseM.name, caseM), sc, right, up, f, new Vector3(sw + .04f, sh + .04f, .08f));
                KSignSide(sc + f * .042f, f, sw, sh, new JObject(), KSignStyle(KS(p, "signStyle", "navy")), items, false);
            }
        }
    }
    static Vector2 KV2(Vector3 localDir) { return new Vector2(localDir.z, localDir.x); }

    // ---- Fixture (small station fixtures and props) ----
    static readonly Dictionary<string, string> KitFixtureMount = new Dictionary<string, string>
    {
        { "clock", "wall" }, { "totem", "floor" }, { "standee", "floor" }, { "planter", "floor" }, { "bin", "floor" }, { "aed", "wall" },
        { "cctv", "ceiling" }, { "speaker", "ceiling" }, { "extinguisher", "wall" }, { "hydrant", "wall" }, { "atm", "floor" },
        { "kiosk", "floor" }, { "frame", "wall" }, { "charger", "floor" },
    };
    static void KitFixture(string id, JObject g, JObject p)
    {
        Vector2 at = KV(KT(g, "point", id)); float y = KN(g, "y", id);
        string kind = KS(p, "kind", null);
        if (kind == null || !KitFixtureMount.TryGetValue(kind, out string defMount)) throw new InvalidOperationException(id + ": unknown fixture kind " + (kind ?? "(missing)"));
        string mount = KS(p, "mount", defMount);
        if (mount == "column") mount = "wall";
        if (mount != "floor" && mount != "wall" && mount != "ceiling") throw new InvalidOperationException(id + ": unknown fixture mount " + mount);
        Vector3 n = p["heading"] != null || p["faceToward"] != null ? KHeading(p, at, id) : KD(new Vector2(1, 0));
        Vector3 up = Vector3.up, ex = Vector3.Cross(up, -n), o = KP(at, y);
        Material hw = KM("hairline"), white = KM("white"), navy = KM("Kit_SignNavy"), scr = KM("Kit_ScreenBlue");
        Batch st = B("Kit_Trim_" + hw.name, hw), wb = B("Kit_Fixture_" + white.name, white), jb = B("Kit_Joint", dark);
        Batch nb = B("Kit_Sign_" + navy.name, navy), sb = B("Kit_Glyph_" + scr.name, scr);
        Material red = KM("#C8102E"); Batch rb = B("Kit_Fixture_" + red.name, red);
        string text = KS(p, "text", null);
        // Optional column clamp (wall/column mounts): column {center [u,v] (element frame), radius} draws a stainless ring
        // (height .08, radius + .03) around the column at y; the fixture's bracket starts at `point` on the column face.
        if (p["column"] is JObject col && mount == "wall")
        {
            Vector3 cc = KP(KV(KT(col, "center", id)), 0); float cr = KF(col, "radius", .45f) + .03f;
            KCyl(st, cc, cr, y - .04f, y + .04f, 28);
            KRingH(st, new Vector3(cc.x, y + .04f, cc.z), cr - .03f, cr, 28, up);
            KRingH(st, new Vector3(cc.x, y - .04f, cc.z), cr - .03f, cr, 28, Vector3.down);
        }
        switch (kind)
        {
            case "clock":
            {
                float r = KF(p, "width", .5f) * .5f;
                if (mount == "ceiling")
                {
                    float cy = y - KF(p, "drop", .6f) - r;
                    Vector3 cc = KP(at, cy);
                    KClockFace(cc + n * .03f, n, r); KClockFace(cc - n * .03f, -n, r);
                    KTube(st, KP(at, cy + r), KP(at, y), .012f, 8);
                    KRingSide(st, cc - n * .03f, n, ex, r, .06f);
                }
                else { Vector3 cc = o + n * .045f; KClockFace(cc, n, r); KRingSide(st, cc - n * .04f, n, ex, r, .05f); KRing(jb, cc - n * .045f, ex, up, 0, r, 24, -n); }
                return;
            }
            case "totem":
            {
                float w = KF(p, "width", .6f), h = KF(p, "height", 2.2f), d = KF(p, "depth", .3f);
                KBox(nb, o + up * h * .5f, ex, up, n, new Vector3(w, h, d));
                KBox(st, o + up * .04f, ex, up, n, new Vector3(w + .04f, .08f, d + .04f), false);
                Vector3 face = o + n * (d * .5f + .002f) + up * (h * .45f);
                if (!KLightbox(p, face, ex, w - .1f, h * .6f, n)) KRectC(sb, face, ex, up, w - .1f, h * .6f, n);
                if (text != null) KText(text, o + n * (d * .5f + .003f) + up * (h - .18f), n, w - .08f, .24f, Color.white);
                KColBox(o + up * h * .5f, ex, up, n, new Vector3(w, h, d));
                return;
            }
            case "standee":
            {
                float w = KF(p, "width", .6f), h = KF(p, "height", 1.6f);
                KBox(st, o + up * .01f, ex, up, n, new Vector3(w * .8f, .02f, .35f), false);
                KTube(st, o + up * .02f - n * .03f, o + up * (h + .25f) - n * .03f, .015f, 8);
                Vector3 pc = o + up * (.25f + h * .5f);
                KBox(wb, pc, ex, up, n, new Vector3(w, h, .02f));
                if (!KLightbox(p, pc + n * .011f, ex, w - .04f, h - .04f, n) && text != null) KText(text, pc + n * .012f, n, w - .08f, h * .3f, KInkDark);
                KColBox(o + up * (h + .25f) * .5f, ex, up, n, new Vector3(w, h + .25f, .35f));
                return;
            }
            case "planter":
            {
                float w = KF(p, "width", .8f), h = KF(p, "height", .6f);
                Material pm = KM(KS(p, "finish", "granite"));
                KBox(B("Kit_Planter_" + pm.name, pm), o + up * h * .5f, ex, up, n, new Vector3(w, h, w), false);
                KRectC(B("Kit_Fixture_Soil", KM("#3B2E22")), o + up * (h - .04f), ex, n, w - .06f, w - .06f, up);
                KitProp("kit_plant", id + " plant", o + up * (h - .04f), new Vector3(w * .85f, KF(p, "plantHeight", 1f), w * .85f), n);
                KColBox(o + up * h * .5f, ex, up, n, new Vector3(w, h, w));
                return;
            }
            case "bin":
            {
                float w = KF(p, "width", .7f), h = KF(p, "height", .95f), d = KF(p, "depth", .4f);
                KBox(st, o + up * h * .5f, ex, up, n, new Vector3(w, h, d), false);
                Material blueM = KM("#1F5FAF"), greenM = KM("#2E8B3E");
                string[] labels = { KS(p, "label0", "일반쓰레기"), KS(p, "label1", "재활용") };
                for (int k = 0; k < 2; k++)
                {
                    Vector3 cc = o + ex * ((k - .5f) * w * .5f) + n * (d * .5f + .002f);
                    KRectC(jb, cc + up * (h - .15f), ex, up, w * .38f, .08f, n);
                    Material lm = k == 0 ? blueM : greenM;
                    KRectC(B("Kit_Fixture_" + lm.name, lm), cc + up * (h - .32f), ex, up, w * .44f, .14f, n);
                    KText(labels[k], cc + up * (h - .32f) + n * .001f, n, w * .42f, .12f, Color.white);
                }
                KColBox(o + up * h * .5f, ex, up, n, new Vector3(w, h, d));
                return;
            }
            case "aed":
            {
                Material green = KM("#009A44"); Batch gb = B("Kit_Fixture_" + green.name, green);
                Vector3 cc = o + n * .09f;
                KBox(wb, cc, ex, up, n, new Vector3(.42f, .52f, .18f));
                KRectC(gb, cc + n * .091f + up * .19f, ex, up, .4f, .12f, n);
                KText("AED", cc + n * .092f + up * .19f, n, .3f, .11f, Color.white);
                KGlass(B("Kit_Glass", glass), cc + n * .092f - ex * .17f - up * .2f, ex, up, .34f, .3f, n);
                KRectC(B("Kit_Glyph_Kit_EmitGreen", KM("Kit_EmitGreen")), cc + n * .092f + up * .135f + ex * .15f, ex, up, .04f, .02f, n);
                Vector3 sc = o + n * .012f + up * .45f;
                KBox(gb, sc, ex, up, n, new Vector3(.6f, .2f, .02f));
                KText(text ?? "자동심장충격기 AED", sc + n * .011f, n, .56f, .17f, Color.white);
                return;
            }
            case "cctv":
            {
                if (mount == "ceiling")
                {
                    KRingH(wb, o + up * -.002f, 0, .1f, 16, Vector3.down);
                    KCyl(wb, o, .09f, y - .05f, y - .002f, 16);
                    KCyl(jb, o, .07f, y - .14f, y - .05f, 16);
                    KRingH(jb, KP(at, y - .14f), 0, .07f, 16, Vector3.down);
                }
                else
                {
                    Vector3 cc = o + n * .18f + up * -.05f, look = (n - up * .5f).normalized;
                    KBox(wb, o + n * .05f, ex, up, n, new Vector3(.08f, .12f, .1f));
                    KTube(wb, o + n * .08f, cc, .02f, 8);
                    KBox(wb, cc, ex, Vector3.Cross(look, ex), look, new Vector3(.1f, .1f, .26f));
                    KRectC(jb, cc + look * .131f, ex, Vector3.Cross(look, ex), .07f, .07f, look);
                }
                return;
            }
            case "speaker":
            {
                if (mount == "ceiling")
                {
                    KRingH(st, KP(at, y - .004f), .1f, .12f, 20, Vector3.down);
                    KRingH(wb, KP(at, y - .003f), 0, .1f, 20, Vector3.down);
                    for (int k = -3; k <= 3; k++) KRectC(jb, KP(at, y - .005f) + ex * k * .025f, ex, n, .006f, .16f - Mathf.Abs(k) * .018f, Vector3.down);
                }
                else
                {
                    Vector3 cc = o + n * .08f;
                    KBox(wb, cc, ex, up, n, new Vector3(.22f, .32f, .15f));
                    KRectC(jb, cc + n * .076f, ex, up, .18f, .26f, n);
                }
                return;
            }
            case "extinguisher":
            {
                float baseY = mount == "floor" ? y + .06f : y - .3f;
                Vector3 axis = mount == "floor" ? o + n * .12f : o + n * .1f;
                if (mount == "floor") KBox(rb, o + up * .03f + n * .12f, ex, up, n, new Vector3(.3f, .06f, .3f), false);
                else KBox(jb, o + n * .02f + up * -.1f, ex, up, n, new Vector3(.1f, .12f, .04f));
                KCyl(rb, axis, .08f, baseY, baseY + .48f, 14);
                KRingH(rb, new Vector3(axis.x, baseY + .48f, axis.z), 0, .08f, 14, up);
                KBox(jb, new Vector3(axis.x, baseY + .54f, axis.z), ex, up, n, new Vector3(.05f, .1f, .08f), false);
                KTube(jb, new Vector3(axis.x, baseY + .56f, axis.z) + ex * .03f, new Vector3(axis.x, baseY + .2f, axis.z) + ex * .1f + n * .06f, .012f, 6);
                if (mount == "wall" || KB(p, "signPlate", true))
                {
                    Vector3 sc = (mount == "floor" ? o + n * .12f + up * .9f : o + n * .012f + up * .45f);
                    KBox(rb, sc, ex, up, n, new Vector3(.36f, .16f, .02f));
                    KText(text ?? "소화기", sc + n * .011f, n, .32f, .14f, Color.white);
                }
                if (mount == "floor") KColBox(o + up * .35f + n * .12f, ex, up, n, new Vector3(.3f, .7f, .3f));
                return;
            }
            case "hydrant":
            {
                float w = KF(p, "width", .75f), h = KF(p, "height", 1.8f);
                Vector3 cc = o + n * .1f;
                KBox(rb, cc, ex, up, n, new Vector3(w, h, .2f));
                KRectC(jb, cc + n * .101f, ex, up, .006f, h - .1f, n);
                KRectC(jb, cc + n * .101f + up * (h * .2f), ex, up, w - .08f, .006f, n);
                KText(text ?? "소화전", cc + n * .102f + up * (h * .32f), n, w * .6f, .2f, Color.white);
                KText("옥내소화전", cc + n * .102f - up * (h * .05f), n, w * .7f, .14f, Color.white);
                foreach (float e in new[] { -.06f, .06f }) KBox(st, cc + n * .12f + ex * e, ex, up, n, new Vector3(.02f, .12f, .03f));
                KRing(B("Kit_Glyph_Kit_EmitRed", KM("Kit_EmitRed")), o + n * .06f + up * (h * .5f + .12f), ex, up, 0, .06f, 14, n);
                KBox(jb, o + n * .03f + up * (h * .5f + .12f), ex, up, n, new Vector3(.16f, .16f, .06f));
                return;
            }
            case "atm":
            {
                float w = KF(p, "width", .9f), h = KF(p, "height", 1.75f), d = KF(p, "depth", .85f);
                Material body = KM(KS(p, "colour", "#DADCDD")), band = KM(KS(p, "band", "#004B87"));
                KBox(B("Kit_Fixture_" + body.name, body), o + up * h * .5f, ex, up, n, new Vector3(w, h, d), false);
                Vector3 f = o + n * (d * .5f + .002f);
                KRectC(B("Kit_Fixture_" + band.name, band), f + up * (h - .15f), ex, up, w, .26f, n);
                KText(text ?? "ATM", f + up * (h - .15f) + n * .001f, n, w - .1f, .2f, Color.white);
                KRectC(jb, f + up * 1.25f, ex, up, .44f, .34f, n); KRectC(sb, f + up * 1.25f + n * .001f, ex, up, .4f, .3f, n);
                KBox(jb, f + up * .98f + n * .1f, ex, up, n, new Vector3(.5f, .06f, .2f), false);
                KRectC(st, f + up * 1.02f + n * .201f - ex * .12f, ex, n, .16f, .12f, up);
                KRectC(jb, f + up * 1.06f + ex * .2f, ex, up, .1f, .015f, n);
                KRectC(jb, f + up * .82f, ex, up, .3f, .03f, n);
                KColBox(o + up * h * .5f, ex, up, n, new Vector3(w, h, d));
                return;
            }
            case "kiosk":
            {
                float w = KF(p, "width", .6f), h = KF(p, "height", 1.6f);
                Material body = KM(KS(p, "colour", "machineBlue")); Batch kb = B("Kit_Fixture_" + body.name, body);
                KBox(kb, o + up * .5f, ex, up, n, new Vector3(w * .8f, 1f, .4f), false);
                Vector3 tilt = (up * Mathf.Cos(.26f) - n * Mathf.Sin(.26f)).normalized, tn = Vector3.Cross(ex, tilt);
                if (Vector3.Dot(tn, n) < 0) tn = -tn;
                Vector3 hc = o + up * (1f + (h - 1f) * .5f);
                KBox(kb, hc, ex, tilt, tn, new Vector3(w, h - 1f, .1f));
                if (!KLightbox(p, hc + tn * .052f, ex, w - .08f, (h - 1f) - .1f, tn)) KRectC(sb, hc + tn * .052f, ex, tilt, w - .08f, h - 1.1f, tn);
                if (text != null) KText(text, o + n * .202f + up * .85f, n, w * .75f, .14f, Color.white);
                KColBox(o + up * h * .5f, ex, up, n, new Vector3(w, h, .45f));
                return;
            }
            case "frame":
            {
                float w = KF(p, "width", 1f), h = KF(p, "height", .7f);
                Vector3 cc = o + n * .02f;
                KBox(st, cc, ex, up, n, new Vector3(w + .06f, h + .06f, .03f));
                if (!KLightbox(p, cc + n * .016f, ex, w, h, n))
                {
                    KRectC(wb, cc + n * .016f, ex, up, w, h, n);
                    if (text != null) KText(text, cc + n * .017f, n, w - .1f, h * .5f, KInkDark);
                }
                return;
            }
            case "charger":
            {
                float w = KF(p, "width", .45f), h = KF(p, "height", 1.3f);
                KBox(wb, o + up * h * .5f, ex, up, n, new Vector3(w, h, w), false);
                KRectC(jb, o + up * (h + .001f), ex, n, w - .04f, w - .04f, up);
                Vector3 f = o + n * (w * .5f + .002f);
                for (int k = 0; k < 4; k++) KRectC(B("Kit_Glyph_Kit_EmitCyan", KM("Kit_EmitCyan")), f + up * (h * .45f + k * .1f), ex, up, w * .6f, .03f, n);
                KRectC(nb, f + up * (h - .15f), ex, up, w - .02f, .2f, n);
                KText(text ?? "휴대폰 충전", f + up * (h - .15f) + n * .001f, n, w - .06f, .16f, Color.white);
                KColBox(o + up * h * .5f, ex, up, n, new Vector3(w, h, w));
                return;
            }
        }
    }
    // Analog clock face centred at c facing n: white dial, 12 ticks, hands at 10:10.
    static void KClockFace(Vector3 c, Vector3 n, float r)
    {
        Vector3 right = Vector3.Cross(Vector3.up, -n), up = Vector3.up;
        Material face = KM("Kit_Backlight"); Batch fb = B("Kit_Light_" + face.name, face), jb = B("Kit_Joint", dark);
        KRing(fb, c, right, up, 0, r * .92f, 28, n);
        for (int k = 0; k < 12; k++)
        {
            float a = k * Mathf.PI / 6; Vector3 dir = right * Mathf.Sin(a) + up * Mathf.Cos(a), side = Vector3.Cross(n, dir);
            float len = k % 3 == 0 ? r * .16f : r * .08f;
            KRectC(jb, c + n * .002f + dir * (r * .82f - len * .5f), side, dir, r * .035f, len, n);
        }
        foreach (var (deg, len, wid) in new[] { (-60f, .5f, .06f), (60f, .75f, .04f) })
        {
            float a = deg * Mathf.Deg2Rad; Vector3 dir = right * Mathf.Sin(a) + up * Mathf.Cos(a), side = Vector3.Cross(n, dir);
            KRectC(jb, c + n * .004f + dir * (r * len * .5f - r * .05f), side, dir, r * wid, r * len, n);
        }
    }
    // Cylindrical rim (depth along n) of a disc of radius r centred at c.
    static void KRingSide(Batch b, Vector3 c, Vector3 n, Vector3 right, float r, float depth)
    {
        Vector3 up = Vector3.Cross(n, right).normalized;
        for (int i = 0; i < 28; i++)
        {
            float t0 = i * Mathf.PI * 2 / 28, t1 = (i + 1) * Mathf.PI * 2 / 28;
            Vector3 d0 = right * Mathf.Cos(t0) + up * Mathf.Sin(t0), d1 = right * Mathf.Cos(t1) + up * Mathf.Sin(t1);
            int v = b.VertexCount;
            b.Face(c + d0 * r, c + d1 * r, c + d1 * r + n * depth, c + d0 * r + n * depth, Vector2.zero, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), d0 + d1);
            b.SetNormal(v, d0); b.SetNormal(v + 1, d1); b.SetNormal(v + 2, d1); b.SetNormal(v + 3, d0);
        }
    }

    // ================= Full-twin round (2026-09-26): Slab, Room, ToiletRoom, Window, Canopy =================
    // ---- Slab (walkable floor plate: top, edge faces, optional soffit; top + edges in Kit_Colliders) ----
    static void KitSlab(string id, JObject g, JObject p)
    {
        List<Vector2> outer = KPoly(KT(g, "polygon", id)); List<List<Vector2>> holes = KHoles(g);
        float y = KN(g, "y", id), depth = KF(p, "depth", .3f);
        if (outer.Count < 3) throw new InvalidOperationException(id + ": slab polygon needs 3+ points.");
        if (depth < .02f) throw new InvalidOperationException(id + ": slab depth must be >= .02.");
        Material top = KM(KS(p, "finish", "floorGranite")), edge = KM(KS(p, "edgeFinish", "plaster"));
        string soffit = KS(p, "soffit", "plaster");
        Vector2 uv0 = p["uvOrigin"] is JArray uo ? KV(uo) : outer[0];
        KFill(B("Kit_Slab_" + top.name, top), outer, holes, y, Vector3.up, uv0, KF(p, "rotation", 0), KF(p, "uvRepeat", 1));
        float area = kitFillArea;
        if (soffit != "none") { Material sm = KM(soffit); KFill(B("Kit_Slab_" + sm.name, sm), outer, holes, y - depth, Vector3.down, uv0, KF(p, "rotation", 0), 1); }
        bool collide = KB(p, "collider", true);
        if (collide) KFill(KCol(), outer, holes, y, Vector3.up, uv0);
        kitFillArea = area;   // surfaceArea reports the walkable top only
        var rings = new List<List<Vector2>> { outer }; rings.AddRange(holes.Where(h => h.Count >= 3));
        Batch eb = B("Kit_Slab_" + edge.name, edge); Vector3 up = Vector3.up;
        foreach (List<Vector2> ring in rings)
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Count]; float len = Vector2.Distance(a, b);
                foreach (var piece in KBoundary(rings, a, b))
                {
                    Vector2 p0 = Vector2.Lerp(a, b, piece.t0); float w = (piece.t1 - piece.t0) * len;
                    Vector3 ex = KD((b - a) / len), outward = -KD(piece.inward);
                    KRect(eb, KP(p0, y - depth), ex, up, w, depth, outward, new Vector2(piece.t0 * len, y - depth));
                    if (collide) KColRect(KP(p0, y - depth), ex, up, w, depth);
                }
            }
    }

    // ---- shared by Room / ToiletRoom: outlines, wall rings with openings, ceilings, furniture slots ----
    // Closed outline as given: a repeated closing point is dropped, any other zero-length edge is an error (opening edge indices
    // refer to the given vertex order).
    static List<Vector2> KOutline(JToken t, string id)
    {
        List<Vector2> r = KPoly(t);
        if (r.Count > 3 && (r[0] - r[r.Count - 1]).sqrMagnitude < 1e-8f) r.RemoveAt(r.Count - 1);
        for (int i = 0; i < r.Count; i++)
            if ((r[(i + 1) % r.Count] - r[i]).sqrMagnitude < 1e-4f) throw new InvalidOperationException(id + ": polygon has a zero-length edge at index " + i + ".");
        if (r.Count < 3 || Mathf.Abs(KArea(r)) < .1f) throw new InvalidOperationException(id + ": polygon needs 3+ points and area.");
        return r;
    }
    static Vector2 KInward(List<Vector2> poly, int i)
    {
        Vector2 d = (poly[(i + 1) % poly.Count] - poly[i]).normalized;
        return new Vector2(-d.y, d.x) * (KArea(poly) > 0 ? 1 : -1);
    }
    // Inner ring `t` inside the outline: vertex i = mitred inner corner at poly[i] (miter limited to 4t); edge i stays parallel.
    static List<Vector2> KInset(List<Vector2> poly, float t)
    {
        int n = poly.Count; var r = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            int h = (i + n - 1) % n;
            Vector2 d0 = (poly[i] - poly[h]).normalized, d1 = (poly[(i + 1) % n] - poly[i]).normalized, n0 = KInward(poly, h), n1 = KInward(poly, i);
            Vector2 a = poly[h] + n0 * t, b = poly[i] + n1 * t;
            float den = d0.x * d1.y - d0.y * d1.x;
            Vector2 q = Mathf.Abs(den) < 1e-4f ? b : a + d0 * (((b.x - a.x) * d1.y - (b.y - a.y) * d1.x) / den);
            if ((q - poly[i]).magnitude > 4 * t) q = poly[i] + (n0 + n1).normalized * 4 * t;
            r.Add(q);
        }
        return r;
    }
    struct KOpening { public int edge; public float s0, s1, y0, y1; public string kind; }
    // {edge, at (opening centre, m from the edge start), width, height (door 2.1, open 2.4, window 1.2), kind door|window|open,
    //  sill (window, .9 above the floor)}; heads are clamped .05 under the wall top.
    static KOpening KOpeningOf(JObject o, List<Vector2> poly, float y, float top, string id)
    {
        int e = KI(o, "edge", -1);
        if (e < 0 || e >= poly.Count) throw new InvalidOperationException(id + ": opening edge " + e + " out of range.");
        string kind = KS(o, "kind", "door");
        if (kind != "door" && kind != "window" && kind != "open") throw new InvalidOperationException(id + ": opening kind must be door, window or open.");
        float L = Vector2.Distance(poly[e], poly[(e + 1) % poly.Count]), at = KN(o, "at", id), w = KN(o, "width", id);
        float y0 = kind == "window" ? y + KF(o, "sill", .9f) : y, h = KF(o, "height", kind == "window" ? 1.2f : kind == "open" ? 2.4f : 2.1f);
        var r = new KOpening { edge = e, s0 = Mathf.Max(0, at - w * .5f), s1 = Mathf.Min(L, at + w * .5f), y0 = y0, y1 = Mathf.Min(y0 + h, top - .05f), kind = kind };
        if (r.s1 - r.s0 < .1f || r.y1 - r.y0 < .1f) throw new InvalidOperationException(id + ": opening on edge " + e + " at " + at + " falls outside the wall.");
        return r;
    }
    static List<KOpening> KOpeningsOf(JObject p, List<Vector2> poly, float y, float top, string id)
    {
        var l = new List<KOpening>();
        if (p["openings"] is JArray a) foreach (JToken o in a) l.Add(KOpeningOf((JObject)o, poly, y, top, id));
        return l;
    }
    // Walls inside the outline (outer face on the polygon edge, inner face t inward, mitred corners). Openings cut both faces and get
    // lined reveals (jambs, head, sill); top cap at `top`; skirting on both faces broken at floor openings; collider on the centreline
    // with the head/sill pieces (floor openings stay walkable). Returns the inner ring.
    static List<Vector2> KRoomWalls(List<Vector2> poly, float y, float top, float t, Material inM, Material outM, List<KOpening> ops, float skirt, bool collide)
    {
        int n = poly.Count; List<Vector2> inner = KInset(poly, t);
        Batch bi = B("Kit_RoomWall_" + inM.name, inM), bo = B("Kit_RoomWall_" + outM.name, outM), sk = B("Kit_Joint", dark);
        Vector3 up = Vector3.up; float H = top - y;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % n], d = (b - a).normalized, nin = KInward(poly, i), ia = inner[i], ib = inner[(i + 1) % n];
            float L = Vector2.Distance(a, b), si0 = Vector2.Dot(ia - a, d), si1 = Vector2.Dot(ib - a, d);
            Vector3 ex = KD(d), N = KD(nin);
            var mine = ops.Where(o => o.edge == i).ToList();
            Func<float, List<Vector4>> cut = s0 => mine.Select(o => new Vector4(o.s0 - s0, o.s1 - s0, o.y1 - y, o.y0 <= y + .01f ? -1f : o.y0 - y)).ToList();
            KPanels(bo, null, KP(a, y), ex, up, -N, L, H, 0, 0, 0, 0, y, cut(0));
            if (si1 - si0 > .01f) KPanels(bi, null, KP(ia, y), ex, up, N, si1 - si0, H, 0, 0, 0, si0, y, cut(si0));
            bo.Face(KP(a, top), KP(b, top), KP(ib, top), KP(ia, top), a, b, ib, ia, up);
            foreach (KOpening o in mine)
            {
                Vector3 p0 = KP(a + d * o.s0, 0), p1 = KP(a + d * o.s1, 0); float h = o.y1 - o.y0;
                KRect(bi, p0 + up * o.y0, N, up, t, h, ex, Vector2.zero);
                KRect(bi, p1 + up * o.y0, N, up, t, h, -ex, Vector2.zero);
                KRect(bi, p0 + up * o.y1, ex, N, o.s1 - o.s0, t, Vector3.down, Vector2.zero);
                if (o.y0 > y + .01f) KRect(bi, p0 + up * o.y0, ex, N, o.s1 - o.s0, t, up, Vector2.zero);
            }
            var floorGaps = mine.Where(o => o.y0 <= y + .01f).Select(o => new Vector2(o.s0, o.s1)).ToList();
            if (skirt > 0)
                foreach (Vector2 r in KRuns(L, floorGaps))
                {
                    KBox(sk, KP(a + d * ((r.x + r.y) * .5f), y + skirt * .5f) - N * .006f, ex, up, -N, new Vector3(r.y - r.x, skirt, .012f), false, false);
                    float r0 = Mathf.Max(r.x, si0), r1 = Mathf.Min(r.y, si1);
                    if (r1 - r0 > .01f) KBox(sk, KP(a + d * ((r0 + r1) * .5f) + nin * t, y + skirt * .5f) + N * .006f, ex, up, N, new Vector3(r1 - r0, skirt, .012f), false, false);
                }
            if (!collide) continue;
            Vector3 c0 = KP(a + nin * (t * .5f), y);
            foreach (Vector2 r in KRuns(L, mine.Select(o => new Vector2(o.s0, o.s1)).ToList())) KColRect(c0 + ex * r.x, ex, up, r.y - r.x, H);
            foreach (KOpening o in mine)
            {
                KColRect(c0 + ex * o.s0 + up * (o.y1 - y), ex, up, o.s1 - o.s0, top - o.y1);
                if (o.y0 > y + .01f) KColRect(c0 + ex * o.s0, ex, up, o.s1 - o.s0, o.y0 - y);
            }
        }
        return inner;
    }
    // Centred grid of points (pitch along ax and its normal) inside ring minus holes, keeping `margin` to the boundary; capped at 4000.
    static List<Vector2> KGrid(List<Vector2> ring, List<List<Vector2>> holes, Vector2 ax, float pitch, float margin)
    {
        Vector2 ay = new Vector2(-ax.y, ax.x);
        float a0 = ring.Min(q => Vector2.Dot(q, ax)), a1 = ring.Max(q => Vector2.Dot(q, ax)), b0 = ring.Min(q => Vector2.Dot(q, ay)), b1 = ring.Max(q => Vector2.Dot(q, ay));
        pitch = Mathf.Max(pitch, .3f);
        int na = Mathf.Max(1, Mathf.FloorToInt((a1 - a0) / pitch)), nb = Mathf.Max(1, Mathf.FloorToInt((b1 - b0) / pitch));
        float sa = (a0 + a1 - (na - 1) * pitch) * .5f, sb = (b0 + b1 - (nb - 1) * pitch) * .5f;
        var rings = new List<List<Vector2>> { ring }; if (holes != null) rings.AddRange(holes);
        var r = new List<Vector2>();
        for (int i = 0; i < na && r.Count < 4000; i++)
            for (int j = 0; j < nb && r.Count < 4000; j++)
            {
                Vector2 q = ax * (sa + i * pitch) + ay * (sb + j * pitch);
                if (KInside(rings, q) && KInside(rings, q + ax * margin) && KInside(rings, q - ax * margin) && KInside(rings, q + ay * margin) && KInside(rings, q - ay * margin)) r.Add(q);
            }
        return r;
    }
    // Floor overlay over the whole outline (covers the thresholds in the wall band; "tile" = floorTile, "none" = no overlay) and an
    // optional structural Slab (depth `slab`, top at y, collider) for rooms placed over voids.
    static void KRoomFloor(string id, List<Vector2> poly, float y, string floorKey, float slab, float rotDeg)
    {
        if (floorKey == "tile") floorKey = "floorTile";
        if (floorKey != "none") { Material fm = KM(floorKey); KFill(B("Kit_Floor_" + fm.name, fm), poly, new List<List<Vector2>>(), y + .004f, Vector3.up, poly[0], rotDeg, 1); }
        if (slab > 0)
            KitSlab(id + " slab", new JObject { ["polygon"] = new JArray(poly.Select(q => new JArray(q.x, q.y))), ["y"] = y },
                new JObject { ["depth"] = slab, ["finish"] = "concrete" });
    }
    // Ceiling on the inner ring ("plaster" = Kit_GridWhite, ceiling600 = 8x8-module texture at 4.8 m, "none"), visible fixtures
    // (LED panels on ceiling600, downlights otherwise) and real lights on a covering grid (spacing <= pitch). light: true/false or
    // {pitch 6, fixturePitch, drop .6 (below y1, .05-1), intensity 1, range, kelvin, shadows, mode}. The ceiling plate runs to the wall centreline (`fill` ring)
    // so the ceiling/wall seam is inside the wall; its grid stays anchored at the inner face corner inner[0]. A shadow-only cap over
    // the outer outline .3 above y1 (Kit_ShadowCap: invisible, no collider) keeps the sun-shadow occluder clear of the depth bias,
    // so the top of the inner wall faces does not catch serrated sunlight when nothing else roofs the room.
    static void KRoomCeiling(string id, List<Vector2> inner, List<Vector2> fill, float y1, string ceilKey, float rotDeg, Vector2 ax, JToken lightTok, float kelvin, float fixturePitch)
    {
        if (ceilKey != "none")
        {
            Material cm = KM(ceilKey == "plaster" ? "Kit_GridWhite" : ceilKey);
            float area = kitFillArea;
            KFill(B("Kit_Ceiling_" + cm.name, cm), fill, new List<List<Vector2>>(), y1, Vector3.down, inner[0], rotDeg, ceilKey == "ceiling600" ? 4.8f : 1);
            kitFillArea = area;
        }
        bool on = lightTok == null || lightTok.Type == JTokenType.Null || lightTok.Type != JTokenType.Boolean || (bool)lightTok;
        if (!on) return;
        JObject lp = lightTok as JObject ?? new JObject();
        Vector2 ay = new Vector2(-ax.y, ax.x);
        if (ceilKey != "none")
            foreach (Vector2 q in KGrid(inner, null, ax, KF(lp, "fixturePitch", fixturePitch), .4f))
            {
                if (ceilKey == "ceiling600") KLedPanel(KP(q, y1), KD(ax), KD(ay), .6f, .6f);
                else KDownlight(KP(q, y1), .07f);
            }
        float rp = KF(lp, "pitch", 6f);
        var l = new JObject { ["intensity"] = 1f, ["range"] = Mathf.Max(6f, rp * 1.3f), ["kelvin"] = kelvin };
        foreach (var kv in lp) l[kv.Key] = kv.Value;
        List<Vector2> at = KGrid(inner, null, ax, rp, .2f);
        if (at.Count == 0) { Vector2 c = inner.Aggregate(Vector2.zero, (s, q) => s + q) / inner.Count; if (KInRing(inner, c)) at.Add(c); }
        for (int k = 0; k < at.Count; k++) KLight(id + " light " + k, KP(at[k], y1 - .3f), l);
    }
    // Wall-aligned slot finder: footprints are oriented rects (u,v) inside the ring (no ring edge crossing them), clear of placed
    // footprints and walk zones; each item's front clearance must stay clear of placed footprints.
    sealed class KLayout
    {
        public readonly List<Vector2> ring; public readonly List<Vector2[]> solid = new List<Vector2[]>(), clear = new List<Vector2[]>();
        public KLayout(List<Vector2> ring) { this.ring = ring; }
        public static Vector2[] Rect(Vector2 c, Vector2 ax, float w, float d)
        {
            Vector2 bx = ax * (w * .5f), by = new Vector2(-ax.y, ax.x) * (d * .5f);
            return new[] { c - bx - by, c + bx - by, c + bx + by, c - bx + by };
        }
        public bool Inside(Vector2[] r)
        {
            Vector2 c = (r[0] + r[1] + r[2] + r[3]) * .25f;
            if (!KInRing(ring, c)) return false;
            foreach (Vector2 q in r) if (!KInRing(ring, q + (c - q).normalized * .015f)) return false;
            for (int i = 0; i < ring.Count; i++)
                for (int k = 0; k < 4; k++) if (KSegCross(ring[i], ring[(i + 1) % ring.Count], r[k], r[(k + 1) % 4])) return false;
            return true;
        }
        public static bool Overlap(Vector2[] A, Vector2[] Bq)
        {
            foreach (Vector2[] P in new[] { A, Bq })
                for (int i = 0; i < 4; i++)
                {
                    Vector2 e = P[(i + 1) % 4] - P[i], ax = new Vector2(-e.y, e.x).normalized;
                    float a0 = A.Min(q => Vector2.Dot(q, ax)), a1 = A.Max(q => Vector2.Dot(q, ax)), b0 = Bq.Min(q => Vector2.Dot(q, ax)), b1 = Bq.Max(q => Vector2.Dot(q, ax));
                    if (a1 <= b0 + .005f || b1 <= a0 + .005f) return false;
                }
            return true;
        }
        public bool Try(Vector2[] foot, Vector2[] clr)
        {
            if (!Inside(foot) || (clr != null && !Inside(clr))) return false;
            if (solid.Any(s => Overlap(s, foot)) || clear.Any(s => Overlap(s, foot))) return false;
            if (clr != null && solid.Any(s => Overlap(s, clr))) return false;
            solid.Add(foot); if (clr != null) clear.Add(clr);
            return true;
        }
    }
    struct KRun { public Vector2 a, d, n; public float len; public int edge; }
    // Inner-face runs (inner ring edges >= .3 m) with the inward normal.
    static List<KRun> KRunsOf(List<Vector2> inner)
    {
        float sign = KArea(inner) > 0 ? 1 : -1; var l = new List<KRun>();
        for (int i = 0; i < inner.Count; i++)
        {
            Vector2 a = inner[i], b = inner[(i + 1) % inner.Count]; float len = Vector2.Distance(a, b);
            if (len < .3f) continue;
            Vector2 d = (b - a) / len;
            l.Add(new KRun { a = a, d = d, n = new Vector2(-d.y, d.x) * sign, len = len, edge = i });
        }
        return l;
    }
    // Walk zone in front of a floor opening: opening width + .3, from the outer face `depth` into the room.
    static Vector2[] KApproach(List<Vector2> poly, KOpening o, float depth)
    {
        Vector2 a = poly[o.edge], d = (poly[(o.edge + 1) % poly.Count] - a).normalized, nin = KInward(poly, o.edge);
        return KLayout.Rect(a + d * ((o.s0 + o.s1) * .5f) + nin * (depth * .5f), d, o.s1 - o.s0 + .3f, depth);
    }
    // Places up to `max` items (w along the run, dep from the wall face, front clearance clr) along a run, sliding .1 m past
    // obstacles; fromEnd starts at the run end. Returns footprint centres.
    static List<Vector2> KFillRun(KLayout lay, KRun r, float w, float dep, float clr, int max, bool fromEnd)
    {
        var got = new List<Vector2>(); const float gap = .01f;
        for (float s = .02f; s + w <= r.len - .02f + 1e-4f && got.Count < max;)
        {
            float sc = fromEnd ? r.len - s - w * .5f : s + w * .5f;
            Vector2 c = r.a + r.d * sc + r.n * (dep * .5f + gap);
            Vector2[] foot = KLayout.Rect(c, r.d, w, dep), cl = clr > 0 ? KLayout.Rect(c + r.n * (dep * .5f + clr * .5f), r.d, w, clr) : null;
            if (lay.Try(foot, cl)) { got.Add(c); s += w; } else s += .1f;
        }
        return got;
    }
    // Count params: integer cap, or "auto"/missing = as many as fit.
    static (int cap, bool auto) KCount(JObject p, string k)
    {
        JToken t = p[k];
        if (t == null || t.Type == JTokenType.Null || (t.Type == JTokenType.String && (string)t == "auto")) return (int.MaxValue, true);
        return (Mathf.Max(0, (int)t), false);
    }

    // ---- Room (back-of-house enclosure: walls with openings, floor, ceiling, light, generic interior) ----
    static void KitRoom(string id, JObject g, JObject p)
    {
        List<Vector2> poly = KOutline(KT(g, "polygon", id), id); float y = KN(g, "y", id), y1 = KN(g, "y1", id);
        if (y1 - y < 2f) throw new InvalidOperationException(id + ": y1 (ceiling underside) must be at least 2.0 above y.");
        float t = KF(p, "wallThickness", .12f), top = KF(p, "wallTop", y1);
        if (t < .03f || t > .6f) throw new InvalidOperationException(id + ": wallThickness must be .03-.6.");
        if (top < y1 - 1e-3f) throw new InvalidOperationException(id + ": wallTop must not be below y1.");
        var ops = KOpeningsOf(p, poly, y, top, id);
        string wf = KS(p, "wallFinish", "plaster");
        List<Vector2> inner = KRoomWalls(poly, y, top, t, KM(wf), KM(KS(p, "outerFinish", wf)), ops, KF(p, "skirting", .1f), KB(p, "collider", true));
        Vector2 ax = (poly[1] - poly[0]).normalized; float rot = Mathf.Atan2(ax.y, ax.x) * Mathf.Rad2Deg;
        KRoomFloor(id, poly, y, KS(p, "floor", "floorGranite"), KF(p, "slab", 0), rot);
        string ck = KS(p, "ceiling", "ceiling600");
        KRoomCeiling(id, inner, KInset(poly, t * .5f), y1, ck, rot, ax, p["light"], 4000, ck == "ceiling600" ? 2.4f : 1.8f);
        string interior = KS(p, "interior", "none");
        if (interior == "none") return;
        var rnd = new System.Random(KI(p, "seed", id.Aggregate(17, (h, ch) => unchecked(h * 31 + ch))));
        var lay = new KLayout(inner);
        foreach (KOpening o in ops) if (o.kind != "window") lay.clear.Add(KApproach(poly, o, t + 1.2f));
        var runs = KRunsOf(inner).OrderByDescending(r => r.len).ToList();
        if (runs.Count == 0) return;
        Vector3 up = Vector3.up; int k = 0;
        Material wood = KM("counterWood"), grey = KM("#6E7478"), steelGrey = KM("#AEB4B8");
        Batch wb = B("Kit_Furniture_" + wood.name, wood), gb = B("Kit_Furniture_" + grey.name, grey), sg = B("Kit_Furniture_" + steelGrey.name, steelGrey), jb = B("Kit_Joint", dark);
        Material hw = KM("hairline"); Batch mb = B("Kit_Trim_" + hw.name, hw);
        switch (interior)
        {
            case "office":
                foreach (KRun r in runs.Take(2))
                    foreach (Vector2 c in KFillRun(lay, r, 1.4f, .7f, .9f, 6 - k, false))
                    {
                        Vector3 C = KP(c, y), AX = KD(r.d), FW = KD(r.n);
                        KBox(wb, C + up * .725f, AX, up, FW, new Vector3(1.4f, .03f, .7f));
                        foreach (float e in new[] { -1f, 1f }) KBox(gb, C + AX * (e * .675f) + up * .355f, AX, up, FW, new Vector3(.03f, .71f, .66f));
                        KBox(gb, C - FW * .3f + up * .45f, AX, up, FW, new Vector3(1.32f, .4f, .02f));
                        KBox(B("Kit_Screen", KM("Kit_Screen")), C - FW * .14f + up * 1.02f, AX, up, FW, new Vector3(.56f, .34f, .03f));
                        KBox(jb, C - FW * .16f + up * .8f, AX, up, FW, new Vector3(.05f, .14f, .05f));
                        KBox(jb, C + FW * .12f + up * .748f, AX, up, FW, new Vector3(.42f, .016f, .14f));
                        KitProp("kit_chair", id + " chair " + k, C + FW * .7f, new Vector3(.45f, .85f, .48f), -FW);
                        KColBox(C + up * .37f, AX, up, FW, new Vector3(1.4f, .74f, .7f));
                        k++;
                    }
                foreach (KRun r in runs.Skip(1))
                    foreach (Vector2 c in KFillRun(lay, r, .9f, .45f, .8f, 4, true)) KCabinet(KP(c, y), KD(r.d), KD(r.n), sg, jb, mb, .9f, 1.8f, .45f, 1);
                break;
            case "storage":
                foreach (float w in new[] { 1.8f, .9f })
                    foreach (KRun r in runs)
                        foreach (Vector2 c in KFillRun(lay, r, w, .5f, .8f, 12, false))
                            KShelf(KP(c - r.n * .26f, y), KD(r.d), KD(r.n), w - .04f, 2f, .45f, false, rnd, new[] { "Kit_Carton" }, 4);
                break;
            case "staff":
                foreach (Vector2 c in KFillRun(lay, runs[0], .9f, .5f, .9f, 4, true)) KCabinet(KP(c, y), KD(runs[0].d), KD(runs[0].n), sg, jb, mb, .9f, 1.8f, .5f, 2);
                foreach (KRun r in runs.Skip(1).Concat(runs.Take(1)))
                {
                    List<Vector2> kc = KFillRun(lay, r, 1.8f, .62f, .9f, 1, false);
                    if (kc.Count == 0) continue;
                    Vector3 C = KP(kc[0], y), AX = KD(r.d), FW = KD(r.n);
                    KitProp("kit_kitchenCounter", id + " kitchenette", C - AX * .3f, new Vector3(1.2f, .9f, .6f), FW);
                    Material wm = KM("white"); Batch fb = B("Kit_Furniture_" + wm.name, wm);
                    KBox(fb, C + AX * .6f + up * .85f, AX, up, FW, new Vector3(.6f, 1.7f, .62f));
                    KBox(jb, C + AX * .6f + FW * .312f + up * 1.2f, AX, up, FW, new Vector3(.58f, .008f, .005f));
                    KBox(mb, C + AX * .38f + FW * .33f + up * 1.2f, AX, up, FW, new Vector3(.02f, .5f, .03f));
                    KColBox(C + AX * .6f + up * .85f, AX, up, FW, new Vector3(.6f, 1.7f, .62f));
                    break;
                }
                {
                    Vector2 cen = inner.Aggregate(Vector2.zero, (s, q) => s + q) / inner.Count, tax = runs[0].d;
                    if (lay.Try(KLayout.Rect(cen, tax, 2.1f, 2f), null))
                    {
                        Vector3 C = KP(cen, y), AX = KD(tax), FW = Vector3.Cross(up, AX);
                        KBox(wb, C + up * .735f, AX, up, FW, new Vector3(1.2f, .03f, .75f));
                        foreach (float e in new[] { -1f, 1f }) foreach (float f in new[] { -1f, 1f }) KBox(gb, C + AX * (e * .55f) + FW * (f * .32f) + up * .36f, AX, up, FW, new Vector3(.04f, .72f, .04f));
                        foreach (float e in new[] { -1f, 1f }) foreach (float f in new[] { -1f, 1f }) KitProp("kit_chair", id + " chair " + (k++), C + AX * (e * .3f) + FW * (f * .65f), new Vector3(.45f, .85f, .48f), -FW * f);
                        KColBox(C + up * .37f, AX, up, FW, new Vector3(1.2f, .74f, .75f));
                    }
                }
                break;
            default: throw new InvalidOperationException(id + ": interior must be none, office, storage or staff.");
        }
    }
    // Steel cabinet / staff locker (C = footprint centre on the floor, FW out of the wall): body, door split(s), handles, collider.
    static void KCabinet(Vector3 C, Vector3 AX, Vector3 FW, Batch body, Batch joint, Batch metal, float w, float h, float d, int rows)
    {
        Vector3 up = Vector3.up, f = C + FW * (d * .5f + .001f);
        KBox(body, C + up * h * .5f, AX, up, FW, new Vector3(w, h, d));
        KRectC(joint, f + up * h * .5f, AX, up, .006f, h - .04f, FW);
        for (int r = 1; r < rows; r++) KRectC(joint, f + up * (h * r / rows), AX, up, w - .02f, .006f, FW);
        for (int r = 0; r < rows; r++)
            foreach (float e in new[] { -1f, 1f })
            {
                float hy = rows == 1 ? h * .52f : h * (r + .5f) / rows;
                KBox(metal, f + AX * (e * .04f) + FW * .01f + up * hy, AX, up, FW, new Vector3(.015f, .12f, .02f));
                if (rows > 1) for (int v = 0; v < 3; v++) KRectC(joint, f + AX * (e * w * .25f) + up * (hy + h / rows * .3f - v * .03f), AX, up, w * .3f, .008f, FW);
            }
        KColBox(C + up * h * .5f, AX, up, FW, new Vector3(w, h, d));
    }

    // ---- ToiletRoom (public toilet: tiled walls, stalls, WC pans, urinals + dividers, vanity, mirror, dryer, sign, ceiling, light) ----
    struct KItem { public string kind; public Vector2 c; public KRun r; public float w, dep; public int n; public bool rev; }
    static readonly string[] KToiletKinds = { "male", "female", "accessible", "unisex" };
    static void KitToiletRoom(string id, JObject g, JObject p)
    {
        List<Vector2> poly = KOutline(KT(g, "polygon", id), id); float y = KN(g, "y", id), y1 = KN(g, "y1", id);
        if (y1 - y < 2.3f) throw new InvalidOperationException(id + ": y1 (ceiling underside) must be at least 2.3 above y.");
        string kind = KS(p, "kind", "male");
        if (!KToiletKinds.Contains(kind)) throw new InvalidOperationException(id + ": kind must be male, female, accessible or unisex.");
        float t = KF(p, "wallThickness", .12f), top = KF(p, "wallTop", y1);
        if (t < .03f || t > .6f) throw new InvalidOperationException(id + ": wallThickness must be .03-.6.");
        if (top < y1 - 1e-3f) throw new InvalidOperationException(id + ": wallTop must not be below y1.");
        if (!(p["entry"] is JObject en)) throw new InvalidOperationException(id + ": params.entry {edge, at, width} required.");
        var ops = KOpeningsOf(p, poly, y, top, id);
        KOpening entry = KOpeningOf(new JObject { ["edge"] = en["edge"], ["at"] = en["at"], ["width"] = KF(en, "width", 1.2f), ["height"] = KF(en, "height", 2.3f), ["kind"] = "open" }, poly, y, top, id);
        ops.Add(entry);
        string wf = KS(p, "wallFinish", "tile");
        List<Vector2> inner = KRoomWalls(poly, y, top, t, KM(wf), KM(KS(p, "outerFinish", wf)), ops, 0, KB(p, "collider", true));
        int ei = entry.edge;
        Vector2 fa = poly[ei], ex2 = (poly[(ei + 1) % poly.Count] - fa).normalized, in2 = KInward(poly, ei), eMid = fa + ex2 * ((entry.s0 + entry.s1) * .5f);
        float rot = Mathf.Atan2(ex2.y, ex2.x) * Mathf.Rad2Deg;
        KRoomFloor(id, poly, y, KS(p, "floor", "tile"), KF(p, "slab", 0), rot);
        KRoomCeiling(id, inner, KInset(poly, t * .5f), y1, KS(p, "ceiling", "ceilingPan"), rot, ex2, p["light"], 5000, 1.8f);

        // Plan: stalls on the back wall (from the far corner), vanity on the side nearest the entry, urinals on the far side.
        var (stallCap, stallAuto) = KCount(p, "stalls"); var (urinalCap, urinalAuto) = KCount(p, "urinals"); var (basinCap, basinAuto) = KCount(p, "basins");
        if (kind != "male") urinalCap = 0;
        float sw = KF(p, "stallWidth", kind == "accessible" ? 1.2f : .95f), sd = KF(p, "stallDepth", 1.5f), cor = KF(p, "corridor", 1.2f), upit = KF(p, "urinalPitch", .8f), bp = KF(p, "basinPitch", .8f);
        var runs = KRunsOf(inner);
        var back = runs.Where(r => Vector2.Dot(r.n, in2) < -.7f).OrderByDescending(r => r.len).ToList();
        var front = runs.Where(r => Vector2.Dot(r.n, in2) > .7f).ToList();
        var sides = runs.Where(r => Mathf.Abs(Vector2.Dot(r.n, in2)) <= .7f).OrderByDescending(r => Mathf.Abs(Vector2.Dot(r.a + r.d * (r.len * .5f) - eMid, ex2))).ToList();
        Func<KRun, bool> farEnd = r => Vector2.Distance(r.a + r.d * r.len, eMid) > Vector2.Distance(r.a, eMid);
        List<KItem> items = null; KLayout lay = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            lay = new KLayout(inner); var list = new List<KItem>(); items = list;
            foreach (KOpening o in ops) if (o.kind != "window") lay.clear.Add(KApproach(poly, o, t + 1.5f));
            KLayout L = lay;
            int Fill(IEnumerable<KRun> rs, string what, float w, float dep, float clr, int cap, bool far, int n)
            {
                int got = 0;
                foreach (KRun r in rs)
                {
                    if (got >= cap) break;
                    bool rev = far ? farEnd(r) : !farEnd(r);
                    foreach (Vector2 c in KFillRun(L, r, w, dep, clr, cap - got, rev)) { list.Add(new KItem { kind = what, c = c, r = r, w = w, dep = dep, n = n, rev = rev }); got++; }
                }
                return got;
            }
            int Vanity(IEnumerable<KRun> rs, int want, bool auto)
            {
                int left = want, placed = 0;
                foreach (KRun r in rs)
                    while (left > 0)
                    {
                        bool ok = false;
                        for (int kk = Mathf.Min(left, 4); kk >= 1 && !ok; kk--)
                            if (Fill(new[] { r }, "vanity", kk * bp + .6f, .55f, 1f, 1, false, kk) == 1) { left -= kk; placed += kk; ok = true; }
                        if (!ok || auto) break;
                    }
                return placed;
            }
            var nearFirst = Enumerable.Reverse(sides).Concat(front).Concat(back).ToList();
            if (kind == "accessible")
            {
                Fill(back.Concat(sides), "accWc", sw, .8f, 1.5f, 1, true, 0);
                Vanity(nearFirst, 1, true);
                break;
            }
            int stalls = Fill(back, "stall", sw, sd, cor, stallCap, true, 0);
            if (kind != "male" && stalls < stallCap && sides.Count > 1) stalls += Fill(sides.Take(1), "stall", sw, sd, cor, stallCap - stalls, true, 0);
            Vanity(nearFirst, basinAuto ? 4 : basinCap, basinAuto);
            int urinals = urinalCap > 0 ? Fill(sides.Concat(front).Concat(back), "urinal", upit, .45f, .8f, urinalCap, true, 0) : 0;
            if (kind == "male" && urinals == 0 && urinalAuto && stallAuto && stalls >= 2 && attempt == 0) { stallCap = stalls / 2; continue; }
            break;
        }

        // Build.
        Material por = KM("Kit_Porcelain"), part = KM(KS(p, "partitionColour", "#B9BEC2")), hw = KM("hairline"), top2 = KM(KS(p, "vanityTop", "granite"));
        Batch pb = B("Kit_Sanitary_" + por.name, por), tb = B("Kit_Partition_" + part.name, part), mb = B("Kit_Trim_" + hw.name, hw), jb = B("Kit_Joint", dark);
        Vector3 up = Vector3.up;
        Func<string, Vector2, KItem?> neighbour = (what, q) => items.Where(it => it.kind == what && KInRing(KLayout.Rect(it.c, it.r.d, it.w, it.dep).ToList(), q)).Select(it => (KItem?)it).FirstOrDefault();
        float open = KF(p, "doorOpen", .25f), ph = KF(p, "partitionHeight", 2f);
        foreach (KItem it in items)
        {
            Vector2 wall2 = it.c - it.r.n * (it.dep * .5f + .01f);
            Vector3 W = KP(wall2, y), AX = KD(it.r.d), FW = KD(it.r.n);
            switch (it.kind)
            {
                case "stall":
                {
                    Vector2 pl = it.c - it.r.d * (it.w * .5f + .08f), pr = it.c + it.r.d * (it.w * .5f + .08f);
                    bool left = KInRing(inner, pl), right = KInRing(inner, pr) && neighbour("stall", pr) == null;
                    KStall(tb, mb, W, AX, FW, it.w, it.dep + .01f, ph, open, left, right);
                    KWc(pb, mb, jb, W, AX, FW);
                    KBox(mb, W + AX * (it.w * .5f - .1f) + FW * .6f + up * .72f, AX, up, FW, new Vector3(.03f, .13f, .13f));   // paper holder
                    break;
                }
                case "urinal":
                {
                    KUrinal(pb, mb, W, AX, FW);
                    if (neighbour("urinal", it.c - it.r.d * (it.w * .5f + .08f)) != null)
                    {
                        Vector3 dc = W - AX * (it.w * .5f) + FW * .235f + up * 1.0f;
                        KBox(tb, dc, AX, up, FW, new Vector3(.02f, .9f, .45f));
                        KColBox(dc, AX, up, FW, new Vector3(.03f, .9f, .45f));
                    }
                    break;
                }
                case "vanity":
                    KVanity(id, W, wall2, it.r.d, it.r.n, it.w, it.n, bp, y, y1, pb, mb, jb, top2);
                    break;
                case "accWc":
                {
                    float cs = it.rev ? 1 : -1;                                           // side of the corner wall along AX
                    Vector3 corner = W + AX * (cs * it.w * .5f), pan = corner - AX * (cs * .45f);
                    KWc(pb, mb, jb, pan, AX, FW);
                    Vector3 sb = corner - AX * (cs * .05f);
                    KTube(mb, sb + FW * .25f + up * .75f, sb + FW * .95f + up * .75f, .017f, 8);
                    KTube(mb, sb + FW * .95f + up * .75f, sb + FW * .95f + up * 1.45f, .017f, 8);
                    Vector3 fb = pan - AX * (cs * .38f);
                    KBox(mb, fb + FW * .03f + up * .8f, AX, up, FW, new Vector3(.08f, .25f, .05f));
                    KTube(mb, fb + FW * .06f + up * .75f, fb + FW * .8f + up * .75f, .017f, 8);
                    Material red = KM("Kit_EmitRed");
                    KRectC(B("Kit_Light_" + red.name, red), corner - AX * (cs * .002f) + FW * .8f + up * .9f, FW, up, .08f, .08f, -AX * cs);
                    break;
                }
            }
        }
        if (!KB(p, "sign", true)) return;
        // Pictogram sign on the corridor face beside the entry (right side, else left, else above the opening).
        float EL = Vector2.Distance(poly[ei], poly[(ei + 1) % poly.Count]), bw = KF(p, "signWidth", .45f), bh = bw * 1.55f, sc; float hc = y + 1.6f;
        if (entry.s1 + .15f + bw <= EL - .1f) sc = entry.s1 + .15f + bw * .5f;
        else if (entry.s0 - .15f - bw >= .1f) sc = entry.s0 - .15f - bw * .5f;
        else if (entry.y1 + .1f + bh <= top - .05f) { sc = (entry.s0 + entry.s1) * .5f; hc = entry.y1 + .1f + bh * .5f; }
        else return;
        Vector3 nOut = -KD(in2);
        KToiletSign(kind, KP(fa + ex2 * sc, hc) + nOut * .02f, nOut, bw, bh, KS(p, "signColour", null));
    }
    // Stall: side partitions (skipped where a wall or a neighbour stall takes them), front pilasters, head rail, door leaf swinging in
    // by `open` x 90 deg (hinge on the -AX pilaster) with an occupancy indicator; W = back-wall point at the stall centre, FW = out.
    static void KStall(Batch part, Batch metal, Vector3 W, Vector3 AX, Vector3 FW, float sw, float sd, float ph, float open, bool leftPanel, bool rightPanel)
    {
        Vector3 up = Vector3.up; const float th = .025f, lift = .15f; float hp = ph - lift;
        foreach (float side in new[] { -1f, 1f })
        {
            if ((side < 0 && !leftPanel) || (side > 0 && !rightPanel)) continue;
            Vector3 pc = W + AX * (side * sw * .5f) + FW * (sd * .5f) + up * (lift + hp * .5f);
            KBox(part, pc, AX, up, FW, new Vector3(th, hp, sd));
            KBox(metal, W + AX * (side * sw * .5f) + FW * (sd - .08f) + up * (lift * .5f), AX, up, FW, new Vector3(.04f, lift, .04f), false);
            KColBox(pc - up * (lift * .5f), AX, up, FW, new Vector3(th + .02f, ph, sd));
        }
        float dw = Mathf.Min(.65f, sw - .2f), pw = (sw - dw) * .5f; Vector3 F = W + FW * sd;
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 pc = F + AX * (side * (sw * .5f - pw * .5f)) + up * (lift + hp * .5f);
            KBox(part, pc, AX, up, FW, new Vector3(pw, hp, th));
            KBox(metal, F + AX * (side * (sw * .5f - pw * .5f)) + up * (lift * .5f), AX, up, FW, new Vector3(.04f, lift, .04f), false);
            KColBox(pc - up * (lift * .5f), AX, up, FW, new Vector3(pw, ph, th + .02f));
        }
        KBox(metal, F + up * (ph + .02f), AX, up, FW, new Vector3(sw, .035f, .035f));
        float a = Mathf.Clamp01(open) * 90 * Mathf.Deg2Rad;
        Vector3 hinge = F - AX * (dw * .5f), dir = (AX * Mathf.Cos(a) - FW * Mathf.Sin(a)).normalized, z = Vector3.Cross(dir, up).normalized;
        if (Vector3.Dot(Vector3.Cross(AX, up), FW) < 0) z = -z;
        KBox(part, hinge + dir * (dw * .5f) + up * (lift + hp * .5f), dir, up, z, new Vector3(dw - .01f, hp - .01f, th));
        Vector3 face = hinge + dir * (dw - .09f) + z * (th * .5f + .002f) + up * 1.0f;
        KBox(metal, face + z * .01f, dir, up, z, new Vector3(.04f, .1f, .02f));
        Material green = KM("Kit_EmitGreen"); KRectC(B("Kit_Light_" + green.name, green), face + up * .12f, dir, up, .05f, .025f, z);
        if (open < .05f) KColBox(hinge + dir * (dw * .5f) + up * (ph * .5f), dir, up, z, new Vector3(dw, ph, th + .02f));
    }
    // Floor-standing WC with concealed cistern (W = wall point at the pan centre, FW = out of the wall): pedestal, rim + seat,
    // water, raised lid, flush plate, collider.
    static void KWc(Batch por, Batch metal, Batch dk, Vector3 W, Vector3 AX, Vector3 FW)
    {
        Vector3 up = Vector3.up;
        KBox(por, W + FW * .3f + up * .19f, AX, up, FW, new Vector3(.3f, .38f, .4f), false);
        KBox(por, W + FW * .09f + up * .5f, AX, up, FW, new Vector3(.4f, .2f, .18f), false);
        Vector3 bc = W + FW * .4f + up * .38f;
        KEllipseBand(por, bc, AX, FW, .19f, .25f, .05f, 20);
        KRing(por, bc + up * .05f, AX * .19f, FW * .25f, .62f, 1f, 20, up);
        KRing(dk, bc + up * .01f, AX * .19f, FW * .25f, 0, .62f, 20, up);
        KBox(por, W + FW * .2f + up * .68f, AX, up, FW, new Vector3(.36f, .46f, .025f), false);
        KRectC(metal, W + FW * .002f + up * 1.0f, AX, up, .22f, .15f, FW);
        KColBox(W + FW * .35f + up * .22f, AX, up, FW, new Vector3(.4f, .44f, .66f));
    }
    // Vertical elliptical band (radii rx along ax, ry along ay) from c up by h, outward smooth normals.
    static void KEllipseBand(Batch b, Vector3 c, Vector3 ax, Vector3 ay, float rx, float ry, float h, int sides)
    {
        Vector3 H = Vector3.up * h;
        for (int i = 0; i < sides; i++)
        {
            float t0 = i * Mathf.PI * 2 / sides, t1 = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 p0 = c + ax * (rx * Mathf.Cos(t0)) + ay * (ry * Mathf.Sin(t0)), p1 = c + ax * (rx * Mathf.Cos(t1)) + ay * (ry * Mathf.Sin(t1));
            Vector3 n0 = (ax * (Mathf.Cos(t0) / rx) + ay * (Mathf.Sin(t0) / ry)).normalized, n1 = (ax * (Mathf.Cos(t1) / rx) + ay * (Mathf.Sin(t1) / ry)).normalized;
            int v = b.VertexCount;
            b.Face(p0, p1, p1 + H, p0 + H, new Vector2(t0 * rx, 0), new Vector2(t1 * rx, 0), new Vector2(t1 * rx, h), new Vector2(t0 * rx, h), n0 + n1);
            b.SetNormal(v, n0); b.SetNormal(v + 1, n1); b.SetNormal(v + 2, n1); b.SetNormal(v + 3, n0);
        }
    }
    // Wall-hung urinal with sensor flush (W = wall point at the urinal centre).
    static void KUrinal(Batch por, Batch metal, Vector3 W, Vector3 AX, Vector3 FW)
    {
        Vector3 up = Vector3.up; Material bowl = KM("#C9CED1");
        KBox(por, W + FW * .16f + up * .8f, AX, up, FW, new Vector3(.36f, .64f, .3f), false);
        KRectC(B("Kit_Sanitary_" + bowl.name, bowl), W + FW * .311f + up * .82f, AX, up, .26f, .44f, FW);
        KBox(por, W + FW * .27f + up * .5f, AX, up, FW, new Vector3(.36f, .05f, .12f));
        KBox(metal, W + FW * .05f + up * 1.23f, AX, up, FW, new Vector3(.08f, .14f, .08f));
        KColBox(W + FW * .16f + up * .8f, AX, up, FW, new Vector3(.38f, .66f, .32f));
    }
    // Vanity (item width = n basins x pitch + .1 counter, then .5 for the hand dryer): stone top with basin cut-outs, apron, bowls,
    // faucets and traps, mirror over the counter, dryer, collider. W/wall2 = wall point at the item centre (local / u,v).
    static void KVanity(string id, Vector3 W, Vector2 wall2, Vector2 d2, Vector2 n2, float w, int n, float bp, float y, float y1, Batch por, Batch metal, Batch dk, Material topM)
    {
        Vector3 up = Vector3.up, AX = KD(d2), FW = KD(n2);
        float lc = n * bp + .1f, x0 = -w * .5f, dep = .55f, th = .04f, ty = y + .84f;
        Vector2 s0 = wall2 + d2 * x0, s1 = wall2 + d2 * (x0 + lc);
        var rect = new List<Vector2> { s0, s1, s1 + n2 * dep, s0 + n2 * dep };
        var holes = new List<List<Vector2>>(); var centres = new List<Vector2>();
        for (int k = 0; k < n; k++)
        {
            Vector2 bc = wall2 + d2 * (x0 + .05f + bp * (k + .5f)) + n2 * .31f; centres.Add(bc);
            holes.Add(Enumerable.Range(0, 16).Select(j => bc + new Vector2(Mathf.Cos(j * Mathf.PI / 8), Mathf.Sin(j * Mathf.PI / 8)) * .19f).ToList());
        }
        Batch tb = B("Kit_Counter_" + topM.name, topM);
        float rotDeg = Mathf.Atan2(d2.y, d2.x) * Mathf.Rad2Deg, area = kitFillArea;
        KFill(tb, rect, holes, ty, up, s0, rotDeg); KFill(tb, rect, holes, ty - th, Vector3.down, s0, rotDeg);
        kitFillArea = area;
        Vector3 C = KP(wall2 + d2 * (x0 + lc * .5f), y);
        KRect(tb, C - AX * (lc * .5f) + FW * dep + up * (ty - th - y), AX, up, lc, th, FW, Vector2.zero);
        foreach (float e in new[] { -1f, 1f }) KRect(tb, C + AX * (e * lc * .5f) + up * (ty - th - y), FW, up, dep, th, AX * e, Vector2.zero);
        KBox(tb, C + FW * (dep - .02f) + up * (ty - th - y - .08f), AX, up, FW, new Vector3(lc, .16f, .02f));
        foreach (Vector2 bc in centres)
        {
            Vector3 axis = KP(bc, 0);
            KCyl(por, axis, .19f, ty - th - .13f, ty - th, 16, false);
            KCyl(por, axis, .2f, ty - th - .13f, ty - th, 16, true);
            KRingH(por, KP(bc, ty - th - .13f), 0, .19f, 16, up);
            KRingH(dk, KP(bc, ty - th - .128f), 0, .025f, 8, up);
            Vector3 fb = KP(bc - n2 * .25f, ty);
            KTube(metal, fb, fb + up * .24f, .016f, 8); KTube(metal, fb + up * .24f, fb + up * .24f + FW * .14f, .012f, 8);
            Vector3 tr = KP(bc, ty - th - .13f);
            KTube(metal, tr, tr + up * -.2f, .018f, 8); KTube(metal, tr + up * -.2f, KP(bc - n2 * .3f, ty - th - .33f), .018f, 8);
        }
        float mTop = Mathf.Min(y + 2f, y1 - .15f);
        if (mTop > y + 1.25f) KRect(B("Kit_Mirror", KM("Kit_Mirror")), C - AX * (lc * .5f) + FW * .008f + up * 1.05f, AX, up, lc, mTop - y - 1.05f, FW, Vector2.zero);
        Material wm = KM("#E6E8E9"); Batch db = B("Kit_Sanitary_" + wm.name, wm);
        Vector3 dc = W + AX * (w * .5f - .25f) + FW * .1f + up * 1.2f;
        KBox(db, dc, AX, up, FW, new Vector3(.3f, .34f, .2f), false);
        KRectC(dk, dc + FW * .101f - up * .12f, AX, up, .22f, .03f, FW);
        KColBox(C + FW * (dep * .5f) + up * ((ty - y) * .5f), AX, up, FW, new Vector3(lc, ty - y, dep));
    }
    // Toilet pictogram board (c = board centre, n = out of the wall): pictogram square over a Korean + English text band.
    static void KToiletSign(string kind, Vector3 c, Vector3 n, float w, float h, string colour)
    {
        Vector3 up = Vector3.up, R = Vector3.Cross(up, -n);
        Material bm = KM(colour ?? (kind == "female" ? "#C8323C" : kind == "unisex" ? "Kit_SignNavy" : "#1F4E9E")), gly = KM("Kit_Glyph");
        Batch bb = B("Kit_Plate_" + bm.name, bm), gb = B("Kit_Glyph_" + gly.name, gly);
        KBox(bb, c, R, up, n, new Vector3(w, h, .03f));
        Vector3 f = c + n * .017f, pc = f + up * (h * .5f - w * .5f); float s = w * .8f;
        switch (kind)
        {
            case "male": KFigure(gb, pc, R, up, n, s, false); break;
            case "female": KFigure(gb, pc, R, up, n, s, true); break;
            case "accessible": KWheelchair(gb, pc, R, up, n, s); break;
            default: KFigure(gb, pc - R * (s * .2f), R, up, n, s * .8f, false); KFigure(gb, pc + R * (s * .2f), R, up, n, s * .8f, true); break;
        }
        string ko = kind == "male" ? "남자 화장실" : kind == "female" ? "여자 화장실" : kind == "accessible" ? "장애인 화장실" : "화장실";
        string en = kind == "male" ? "Men" : kind == "female" ? "Women" : kind == "accessible" ? "Accessible" : "Toilet";
        float band = h - w;
        KText(ko, f + up * (-h * .5f + band * .64f) + n * .001f, n, w * .9f, band * .36f, Color.white);
        KText(en, f + up * (-h * .5f + band * .24f) + n * .001f, n, w * .9f, band * .26f, Color.white);
    }
    static void KFigure(Batch b, Vector3 c, Vector3 R, Vector3 U, Vector3 n, float s, bool woman)
    {
        KRing(b, c + U * (.31f * s), R, U, 0, .075f * s, 16, n);
        if (woman)
        {
            Vector3 t0 = c + U * (.2f * s), t1 = c - U * (.12f * s);
            b.Face(t1 - R * (.17f * s), t1 + R * (.17f * s), t0 + R * (.08f * s), t0 - R * (.08f * s), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, n);
            foreach (float e in new[] { -1f, 1f })
            {
                KRectC(b, c + R * (e * .05f * s) - U * (.25f * s), R, U, .06f * s, .26f * s, n);
                KRectC(b, c + R * (e * .14f * s) + U * (.08f * s), R, U, .045f * s, .22f * s, n);
            }
        }
        else
        {
            KRectC(b, c + U * (.06f * s), R, U, .2f * s, .28f * s, n);
            foreach (float e in new[] { -1f, 1f })
            {
                KRectC(b, c + R * (e * .055f * s) - U * (.23f * s), R, U, .08f * s, .3f * s, n);
                KRectC(b, c + R * (e * .135f * s) + U * (.07f * s), R, U, .045f * s, .26f * s, n);
            }
        }
    }
    static void KWheelchair(Batch b, Vector3 c, Vector3 R, Vector3 U, Vector3 n, float s)
    {
        KRing(b, c + U * (.32f * s) - R * (.04f * s), R, U, 0, .07f * s, 16, n);
        KRectC(b, c + U * (.1f * s) - R * (.06f * s), R, U, .09f * s, .26f * s, n);
        KRectC(b, c - U * (.03f * s) + R * (.06f * s), R, U, .26f * s, .08f * s, n);
        KRectC(b, c - U * (.15f * s) + R * (.18f * s), R, U, .08f * s, .22f * s, n);
        KRing(b, c - U * (.17f * s) - R * (.06f * s), R, U, .15f * s, .2f * s, 24, n);
    }

    // ---- Window (frame, mullions, optional transom, glass, sill; pair with a Room / WallCladding opening) ----
    static void KitWindow(string id, JObject g, JObject p)
    {
        JToken edge = KT(g, "edge", id); Vector2 a2 = KV(edge[0]), b2 = KV(edge[1]); float y = KN(g, "y", id), L = Vector2.Distance(a2, b2);
        float H = KF(p, "height", 1.2f), pitch = KF(p, "mullionPitch", 1.2f), fw = KF(p, "frameWidth", .05f), fd = KF(p, "depth", .08f);
        if (L < 4 * fw + .1f || H < 4 * fw + .1f) throw new InvalidOperationException(id + ": window too small for its frame.");
        Vector3 n = KEdgeNormal(a2, b2, p), ex = (KP(b2, 0) - KP(a2, 0)).normalized, up = Vector3.up, A = KP(a2, y) + n * KF(p, "inset", 0);
        string fk = KS(p, "frame", "stainless");
        Material fm = fk == "stainless" ? steel : fk == "black" ? dark : fk == "white" ? KM("white") : KM(fk), gm = KGlassMat(KS(p, "glass", "clear"));
        Batch fr = B("Kit_Frame_" + fm.name, fm), gl = B("Kit_Glass_" + gm.name, gm);
        Func<float, float, Vector3> at = (s, h) => A + ex * s + up * h;
        KBox(fr, at(fw * .5f, H * .5f), ex, up, n, new Vector3(fw, H, fd));
        KBox(fr, at(L - fw * .5f, H * .5f), ex, up, n, new Vector3(fw, H, fd));
        KBox(fr, at(L * .5f, fw * .5f), ex, up, n, new Vector3(L - 2 * fw, fw, fd));
        KBox(fr, at(L * .5f, H - fw * .5f), ex, up, n, new Vector3(L - 2 * fw, fw, fd));
        int panes = Mathf.Max(1, Mathf.RoundToInt((L - 2 * fw) / Mathf.Max(.2f, pitch))); float pw = (L - 2 * fw) / panes;
        for (int k = 1; k < panes; k++) KBox(fr, at(fw + k * pw, H * .5f), ex, up, n, new Vector3(fw * .8f, H - 2 * fw, fd * .9f));
        float tr = KF(p, "transom", 0);
        if (tr > 2 * fw && tr < H - 2 * fw) KBox(fr, at(L * .5f, tr), ex, up, n, new Vector3(L - 2 * fw, fw * .8f, fd * .9f));
        KGlass(gl, at(fw, fw), ex, up, L - 2 * fw, H - 2 * fw, n);
        JToken st = p["sill"];
        string sill = st == null || st.Type == JTokenType.Null ? "inside" : st.Type == JTokenType.Boolean ? ((bool)st ? "inside" : "none") : (string)st;
        if (sill != "none")
        {
            if (sill != "inside" && sill != "outside" && sill != "both") throw new InvalidOperationException(id + ": sill must be true/false or inside/outside/both/none.");
            Material sm = KM(KS(p, "sillFinish", "granite")); Batch sb = B("Kit_Trim_" + sm.name, sm); float sdp = KF(p, "sillDepth", .15f);
            foreach (float side in sill == "both" ? new[] { 1f, -1f } : sill == "outside" ? new[] { -1f } : new[] { 1f })
                KBox(sb, at(L * .5f, -.015f) + n * (side * (fd * .5f + sdp * .5f - .01f)), ex, up, n, new Vector3(L + .1f, .03f, sdp + .02f));
        }
        if (KB(p, "collider", true)) KColRect(A, ex, up, L, H);
    }

    // ---- Canopy (covered passage soffit: slats / aluminium panels / plaster, closed top, fascia band, optional lights) ----
    static void KitCanopy(string id, JObject g, JObject p)
    {
        List<Vector2> outer = KPoly(KT(g, "polygon", id)); List<List<Vector2>> holes = KHoles(g); float y = KN(g, "y", id);
        if (outer.Count < 3) throw new InvalidOperationException(id + ": canopy polygon needs 3+ points.");
        string finish = KS(p, "finish", "metalSlat");
        float thick = KF(p, "thickness", .3f), fascia = KF(p, "fascia", .4f);
        if (thick < .05f) throw new InvalidOperationException(id + ": thickness must be >= .05.");
        var rings = new List<List<Vector2>> { outer }; rings.AddRange(holes.Where(h => h.Count >= 3));
        Vector2 dir;
        if (p["slatAngle"] != null && p["slatAngle"].Type != JTokenType.Null) { float a = KF(p, "slatAngle", 0) * Mathf.Deg2Rad; dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        else { int li = Enumerable.Range(0, outer.Count).OrderByDescending(i => Vector2.Distance(outer[i], outer[(i + 1) % outer.Count])).First(); dir = (outer[(li + 1) % outer.Count] - outer[li]).normalized; }
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float rotDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector3 up = Vector3.up, down = Vector3.down;
        Material sm = KM(KS(p, "colour", finish == "plaster" ? "gypsum" : "Kit_Aluminium"));
        Batch sb = B("Kit_Canopy_" + sm.name, sm), jb = B("Kit_Joint", dark);
        // Strips across the region along `dir` (pitch, width), clipped by the filled intervals of their centreline.
        var rr = rings.Select(r => r.Select(q => new Vector2(Vector2.Dot(q, dir), Vector2.Dot(q, perp))).ToList()).ToList();
        float yMin = rr[0].Min(q => q.y), yMax = rr[0].Max(q => q.y);
        Func<Vector2, Vector2> W2 = q => dir * q.x + perp * q.y;
        Action<Batch, float, float, float, float> strips = (b, pitch, w, h, at) =>
        {
            for (float c = yMin + pitch * .5f; c < yMax; c += pitch)
                foreach (Vector2 iv in KIntervals(rr, c))
                {
                    Vector2 q0 = W2(new Vector2(iv.x, c - w * .5f)), q1 = W2(new Vector2(iv.y, c - w * .5f)), q2 = W2(new Vector2(iv.y, c + w * .5f)), q3 = W2(new Vector2(iv.x, c + w * .5f));
                    b.Face(KP(q0, at), KP(q1, at), KP(q2, at), KP(q3, at), new Vector2(iv.x, c - w * .5f), new Vector2(iv.y, c - w * .5f), new Vector2(iv.y, c + w * .5f), new Vector2(iv.x, c + w * .5f), down);
                    if (h <= 0) continue;
                    Vector3 len = KP(q1, 0) - KP(q0, 0); float l = len.magnitude;
                    if (l < .01f) continue;
                    KRect(b, KP(q0, at), len / l, up, l, h, -KD(perp), new Vector2(iv.x, 0));
                    KRect(b, KP(q3, at), len / l, up, l, h, KD(perp), new Vector2(iv.x, 0));
                }
        };
        switch (finish)
        {
            case "metalSlat":
            {
                float sp = KF(p, "slatPitch", .15f), sw = KF(p, "slatWidth", .1f), sh = KF(p, "slatDepth", .04f);
                if (sw >= sp || sp < .05f) throw new InvalidOperationException(id + ": slatWidth must be below slatPitch (>= .05).");
                KFill(jb, outer, holes, y + sh + .02f, down, outer[0], rotDeg);
                strips(sb, sp, sw, sh, y);
                break;
            }
            case "aluminium":
            case "plaster":
                KFill(sb, outer, holes, y, down, outer[0], rotDeg);
                if (finish == "aluminium") strips(jb, KF(p, "panelWidth", 1.2f), .012f, 0, y - .002f);
                break;
            default: throw new InvalidOperationException(id + ": finish must be metalSlat, aluminium or plaster.");
        }
        float area = kitFillArea;
        Material tm = KM(KS(p, "topFinish", "dark"));
        KFill(B("Kit_Canopy_" + tm.name, tm), outer, holes, y + thick, up, outer[0], rotDeg);
        kitFillArea = area;
        Material fm = KM(KS(p, "fasciaFinish", "Kit_Aluminium")); Batch fb = B("Kit_Canopy_" + fm.name, fm);
        float fy0 = Mathf.Min(y, y + thick - fascia);
        foreach (List<Vector2> ring in rings)
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % ring.Count]; float len = Vector2.Distance(a, b);
                foreach (var piece in KBoundary(rings, a, b))
                {
                    Vector2 p0 = Vector2.Lerp(a, b, piece.t0); float w = (piece.t1 - piece.t0) * len;
                    Vector3 ex = KD((b - a) / len), outw = -KD(piece.inward), inw = KD(piece.inward), o = KP(p0, 0);
                    KRect(sb, o + up * y, ex, up, w, thick, outw, new Vector2(piece.t0 * len, y));
                    if (fascia <= 0) continue;
                    Vector3 fo = o + outw * .01f;
                    KRect(fb, fo + up * fy0, ex, up, w, y + thick - fy0 + .02f, outw, new Vector2(piece.t0 * len, fy0));
                    KRect(fb, fo + up * fy0, ex, inw, w, .05f, down, Vector2.zero);
                    KRect(fb, fo + up * (y + thick + .02f), ex, inw, w, .05f, up, Vector2.zero);
                    if (fy0 < y - .005f) KRect(fb, fo + inw * .05f + up * fy0, ex, up, w, y - fy0, inw, Vector2.zero);
                }
            }
        if (p["lights"] is JObject lp)
        {
            float pitch = KF(lp, "pitch", 3f), dy = fy0 < y ? 0 : 0;
            string lk = KS(lp, "kind", "downlight");
            foreach (Vector2 q in KGrid(outer, holes, dir, pitch, .3f))
            {
                if (lk == "linear") KLinearLight(KP(q - dir * (KF(lp, "length", 1.2f) * .5f), y - .004f), KD(dir), KD(perp), KF(lp, "length", 1.2f), .06f);
                else KDownlight(KP(q, y - .004f), .07f);
            }
            float rp = KF(lp, "realPitch", 9f);
            if (rp > 0)
            {
                var l = new JObject { ["intensity"] = 1f, ["range"] = Mathf.Max(6f, rp * 1.2f), ["kelvin"] = 3500 };
                foreach (var kv in lp) if (kv.Key != "kind") l[kv.Key] = kv.Value;
                int k = 0;
                foreach (Vector2 q in KGrid(outer, holes, dir, rp, .3f)) KLight(id + " light " + k++, KP(q, y - .3f), l);
            }
        }
        if (KB(p, "collider", false)) { float a2 = kitFillArea; KFill(KCol(), outer, holes, y + thick, up, outer[0]); kitFillArea = a2; }
    }

    // Showcase bay far from the station (U/V 400, Y 50): builds a showcase spec (preview scaffold + kit elements) with in-memory
    // meshes/materials under a HideAndDontSave root, renders its views with a temporary camera and destroys everything.
    // args[0] = output PNG prefix; args[1] (optional) = showcase spec (default kit-preview/showcase.json).
    public static void KitPreview(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run KitPreview in Edit mode.");
        if (args == null || args.Length < 1) throw new ArgumentException("KitPreview <png prefix> [showcase.json]");
        string specPath = args.Length > 1 ? args[1] : ".planning/2026-09-24-interior-twin/kit-preview/showcase.json";
        JObject spec = JObject.Parse(File.ReadAllText(specPath));
        Transform savedRoot = root, savedFrame = frame, savedBox = boxFrame; bool savedIn = inBox; string savedPrefix = batchPrefix, savedFolder = meshFolder;
        GameObject preview = null, camGo = null; var shots = new List<string>();
        try
        {
            KitInit(); kitPreviewing = true; meshFolder = null; batches.Clear(); batchPrefix = "";
            preview = new GameObject("Kit preview (temporary)") { hideFlags = HideFlags.HideAndDontSave };
            preview.transform.rotation = Quaternion.Euler(0, Theta, 0);
            root = frame = preview.transform; inBox = false; boxFrame = null;
            // Preview-only context (not kit elements): slabs {polygon, top, depth}, closed shell boxes {box [u,v,y,du,dv,dy]}, and
            // builder slab runs {runs [[u0,u1,v0,v1],..], top, depth, openings [slabOpening,..]} through the 2F SecondFloor clipping.
            if (spec["scaffold"] is JArray scaffold)
                foreach (JObject s in scaffold)
                {
                    // Builder fixtures through their builder functions: lantern [u,v,y0,radius,height], infoDesk [u,v,r,floor,du,dv],
                    // bench [u,v,floor,len] (preview of builder appearance changes; no plan or scene involved).
                    if (s["lantern"] is JArray ln) { Lantern((float)ln[0], (float)ln[1], (float)ln[2], (float)ln[3], (float)ln[4]); continue; }
                    if (s["bench"] is JArray bn) { KorailBench((float)bn[0], (float)bn[1], (float)bn[2], (float)bn[3]); continue; }
                    if (s["infoDesk"] is JArray dk)
                    {
                        float iu = (float)dk[0], iv = (float)dk[1], r = (float)dk[2], fl = (float)dk[3];
                        Material white = Reuse("Tube_White");
                        Arc("Majibang_Info_Body", dark, iu, iv, fl, fl + .95f, r - .55f, r, 30, 330, true);
                        Arc("Majibang_Info_Band", white, iu, iv, fl + .95f, fl + 1.12f, r - .6f, r + .04f, 30, 330, true);
                        Arc("Majibang_Info_Canopy", dark, iu, iv, fl + 3.3f, fl + 4.0f, r - .3f, r + .45f, 0, 360, false);
                        InfoDeskFaces(iu, iv, r, fl, Mathf.Atan2((float)dk[5], (float)dk[4]));
                        continue;
                    }
                    Material sm = KM((string)s["finish"]);
                    if (s["box"] is JArray bx) Box("Kit_Scaffold_" + (string)s["name"], sm, (float)bx[0], (float)bx[1], (float)bx[2], (float)bx[3], (float)bx[4], (float)bx[5]);
                    else if (s["runs"] is JArray runs)
                    {
                        float top = (float)s["top"], depth = (float)s["depth"];
                        var ops = (s["openings"] as JArray ?? new JArray()).Cast<JObject>().ToList();
                        var holes = ops.Select(o => KPoly(o["polygon"])).ToList();
                        Batch rb = B("Kit_Scaffold_" + (string)s["name"], sm, true);
                        foreach (JArray r in runs)
                        {
                            float u0 = (float)r[0], u1 = (float)r[1], v0 = (float)r[2], v1 = (float)r[3];
                            if (holes.Any(h => h.Max(q => q.x) > u0 && h.Min(q => q.x) < u1 && h.Max(q => q.y) > v0 && h.Min(q => q.y) < v1)) ClippedSlab(rb, u0, u1, v0, v1, holes, top, depth);
                            else rb.Slab(u0, u1, v0, v1, top, depth);
                        }
                        for (int k = 0; k < ops.Count; k++) OpeningGuard((string)ops[k]["id"], holes[k], ops[k], top);
                    }
                    else PolySlab("Kit_Scaffold_" + (string)s["name"], sm, KPoly(s["polygon"]).ToArray(), (float)s["top"], (float)s["depth"], false);
                }
            foreach (JObject e in (JArray)spec["elements"]) KitElement(e);
            foreach (Batch b in batches.Values) b.Save();
            Shader textShader = Shader.Find("ChooGuard/Station/WorldText");
            if (textShader == null) throw new InvalidOperationException("Depth-tested world-text shader missing.");
            var text = new Material(textShader) { hideFlags = HideFlags.HideAndDontSave, mainTexture = font.material.mainTexture };
            kitTransient.Add(text);
            foreach (TextMesh t in preview.GetComponentsInChildren<TextMesh>(true)) t.GetComponent<MeshRenderer>().sharedMaterial = text;
            foreach (Transform t in preview.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            camGo = new GameObject("Kit preview camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = camGo.AddComponent<Camera>();
            if (SceneView.lastActiveSceneView != null) camera.CopyFrom(SceneView.lastActiveSceneView.camera);
            camera.enabled = false; camera.orthographic = false; camera.usePhysicalProperties = false; camera.nearClipPlane = .05f; camera.farClipPlane = 600;
            foreach (JObject v in (JArray)spec["views"])
            {
                JArray eye = (JArray)v["eye"], target = (JArray)v["target"];
                Vector3 we = preview.transform.TransformPoint(P((float)eye[0], (float)eye[1], (float)eye[2]));
                Vector3 wt = preview.transform.TransformPoint(P((float)target[0], (float)target[1], (float)target[2]));
                camera.transform.SetPositionAndRotation(we, Quaternion.LookRotation(wt - we, Vector3.up));
                camera.fieldOfView = KF(v, "fov", 60);
                string path = args[0] + "-" + (string)v["name"] + ".png";
                KitCapture(camera, path, KI(v, "width", 1600), KI(v, "height", 900));
                shots.Add(path);
            }
        }
        finally
        {
            if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
            if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
            foreach (UnityEngine.Object o in kitTransient) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            kitTransient.Clear(); batches.Clear(); kitMats.Clear();
            meshFolder = savedFolder; kitPreviewing = false; root = savedRoot; frame = savedFrame; boxFrame = savedBox; inBox = savedIn; batchPrefix = savedPrefix;
        }
        var byType = kitReport.GroupBy(r => (string)r.GetType().GetProperty("type").GetValue(r))
            .Select(gr => new { type = gr.Key, elements = gr.Count(), triangles = gr.Sum(r => (int)(r.GetType().GetProperty("triangles")?.GetValue(r) ?? 0)) }).ToArray();
        File.WriteAllText(args[0] + "-report.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
        {
            schema = "chooguard.interior-kit-preview.v1", spec = specPath, shots, byType, elements = kitReport,
            note = "Temporary HideAndDontSave root, in-memory meshes/materials; destroyed after rendering. No scene or asset writes."
        }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("KIT_PREVIEW " + string.Join(", ", shots));
    }

    static void KitCapture(Camera camera, string path, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 4);
        Texture2D image = null;
        try
        {
            camera.targetTexture = target; camera.aspect = (float)width / height; camera.ResetProjectionMatrix();
            camera.Render(); RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(false, false);
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
