// Video-priority Busan Station 1F. Execute in Edit mode via unity run_script.
// Geometry topology follows the filmed route; absolute registration and unseen rooms are inferred.
// Owns only its named scene root, VideoFirstFloor assets and floor-one-build.json.
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class VideoFloorOne
{
    const string RootName = "영상복원 · 1층";
    const string Art = "Assets/ChooGuard/Art/StationInterior/VideoFirstFloor";
    const string Base = "Assets/ChooGuard/Art/StationInterior/";
    const string ContractPath = ".planning/2026-09-23-video-twin/construction-contract.json";
    const string ReceiptPath = ".planning/2026-09-23-video-twin/floor-one-build.json";
    const string Restaurant = Base + "Kits/restaurant-bits/KayKit_Restaurant_Bits_1.0_FREE/Assets/fbx (unity)/";
    const float Ceiling = 4.2f;
    static readonly float Sin = Mathf.Sin(16.2f * Mathf.Deg2Rad);
    static readonly float Cos = Mathf.Cos(16.2f * Mathf.Deg2Rad);
    static Vector3 W(float u, float v, float y) => new Vector3(u * Sin + v * Cos, y, u * Cos - v * Sin);
    static Vector3 P(float u, float v, float y) => new Vector3(u, y, v);
    static Vector3 World(Vector3 p) => W(p.x, p.z, p.y);

    sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<int> Triangles = new List<int>();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float width = 1, float height = 1)
        {
            int n = Vertices.Count;
            Vertices.Add(World(a)); Vertices.Add(World(b)); Vertices.Add(World(c)); Vertices.Add(World(d));
            UV.Add(new Vector2(0, 0)); UV.Add(new Vector2(width, 0));
            UV.Add(new Vector2(width, height)); UV.Add(new Vector2(0, height));
            Triangles.Add(n); Triangles.Add(n + 1); Triangles.Add(n + 2);
            Triangles.Add(n); Triangles.Add(n + 2); Triangles.Add(n + 3);
        }
        public void SignQuad(Vector3 lowLeft, Vector3 lowRight, Vector3 highRight, Vector3 highLeft)
        {
            int n = Vertices.Count;
            Vertices.Add(lowLeft); Vertices.Add(highLeft); Vertices.Add(highRight); Vertices.Add(lowRight);
            UV.Add(new Vector2(0, 0)); UV.Add(new Vector2(0, 1));
            UV.Add(new Vector2(1, 1)); UV.Add(new Vector2(1, 0));
            Triangles.Add(n); Triangles.Add(n + 1); Triangles.Add(n + 2);
            Triangles.Add(n); Triangles.Add(n + 2); Triangles.Add(n + 3);
        }
        public Mesh Make(string name, Mesh mesh = null)
        {
            if (mesh == null) mesh = new Mesh();
            mesh.Clear(); mesh.name = name; mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Vertices); mesh.SetUVs(0, UV); mesh.SetTriangles(Triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }

    sealed class Batch
    {
        public MeshData Data = new MeshData();
        public Material Material;
        public bool Collision;
        public string Zone;
    }

    sealed class Builder
    {
        readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
        readonly JArray regions = new JArray();
        readonly JObject contract;
        readonly GameObject root;
        readonly float upperV, lowerV, rise, clearWidth, u0, u1, v0, v1;
        int propCount, localLightCount, meshCount;
        string zone;

        public Builder(JObject contract, GameObject root)
        {
            this.contract = contract; this.root = root;
            var core = contract["sharedCore"];
            upperV = (float)core["upperLandingV"]; lowerV = (float)core["lowerLandingV"];
            rise = (float)core["upperFloorY"] - (float)core["lowerFloorY"];
            clearWidth = (float)core["visibleClearWidthEachM"];
            var opening = (JArray)core["floorOpeningUV"];
            u0 = (float)opening[0]; u1 = (float)opening[1]; v0 = (float)opening[2]; v1 = (float)opening[3];
        }

        public void Prepare()
        {
            AssetDatabase.ImportAsset(Art, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
            Folder(Art + "/Meshes"); Folder(Art + "/Materials"); Folder(Art + "/PropMaterials");
            foreach (string name in new[] { "BakeryDisplay", "StoreShelf", "InfoKiosk", "AtmKiosk", "TrashCan", "TicketBooth" })
                LoadPrefab(Base + "Props/" + name + ".fbx");
            foreach (string name in new[] { "WaitingBench", "LuggageLocker", "FireExtCabinet", "WaterCooler", "TicketCounter", "KorailVendingMachine" })
                LoadPrefab(Base + "Kits/Web/" + name + ".fbx");
            foreach (string name in new[] { "table_round_A", "chair_A", "kitchencounter_sink", "kitchencounter_straight_A", "crate_buns" })
                LoadPrefab(Restaurant + name + ".fbx");
            LoadPrefab("Assets/ChooGuard/Art/WorldSet/Planter.fbx");
            foreach (string file in Directory.GetFiles(Art + "/Signs", "*.png"))
            {
                string path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
                string key = Path.GetFileNameWithoutExtension(path);
                var texture = Required<Texture2D>(path);
                var m = Material("sign_" + key, new Color(1, 1, 1), 0, .25f);
                m.SetTexture("_BaseMap", texture); m.SetTexture("_EmissionMap", texture);
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white * .65f);
                if (key == "paris_logo") Transparent(m, new Color(1, 1, 1, 1));
                EditorUtility.SetDirty(m);
            }
            foreach (string key in new[] { "paris", "paris_logo", "design", "bnc", "rail", "food", "metro", "clock", "korail", "wc", "lockers", "tmo", "zimcarry", "convenience", "fishcake", "cafe", "gimbap", "gukbap", "sushi", "udon", "boxcup", "shabu", "gaemijip", "service", "closed", "exit1", "exit2", "exit3", "exit4", "exit5", "exit6" })
                if (!materials.ContainsKey("sign_" + key)) throw new InvalidOperationException("Missing baked sign: " + key);
            CloneMaterial("floor", Base + "Materials/PBR_Granite005A_2K.mat", new Color(.89f, .9f, .9f), .76f, 0);
            var floorTexture = Required<Texture2D>(Base + "Textures/Lane/floor_station_polished.png");
            materials["floor"].SetTexture("_BaseMap", floorTexture);
            materials["floor"].SetFloat("_BumpScale", .10f);
            CloneMaterial("metal", Base + "Materials/PBR_Metal032_2K.mat", new Color(.73f, .77f, .79f), .76f, .85f);
            // Reuse the hairline normal, but not the dark blue painted-metal albedo.
            materials["metal"].SetTexture("_BaseMap", null);
            materials["metal"].SetFloat("_BumpScale", .16f);
            CloneMaterial("ceiling", Base + "Materials/PBR_OfficeCeiling003_2K.mat", new Color(.94f, .945f, .925f), .24f, .06f);
            // Video200/216 show painted metal, not the source texture's dark repeated motifs.
            // Panel joints, shallow recesses and luminaires are explicitly modelled below.
            var ceilingMaterial = materials["ceiling"];
            ceilingMaterial.SetTexture("_BaseMap", null); ceilingMaterial.SetTexture("_BumpMap", null);
            ceilingMaterial.SetTexture("_MetallicGlossMap", null); ceilingMaterial.SetTexture("_OcclusionMap", null);
            ceilingMaterial.DisableKeyword("_NORMALMAP"); ceilingMaterial.DisableKeyword("_METALLICSPECGLOSSMAP");
            Material("ceiling_joint", new Color(.72f, .75f, .74f), .05f, .2f);
            Material("ceiling_recess", new Color(.86f, .89f, .875f), .08f, .28f);
            CloneMaterial("wall", Base + "Materials/PBR_PaintedPlaster017_4K.mat", new Color(.88f, .875f, .84f), .3f, 0);
            CloneMaterial("wood", Base + "Materials/PBR_WoodFloor064_4K.mat", new Color(.62f, .43f, .24f), .3f, 0);
            Material("black", new Color(.045f, .05f, .055f), .1f, .65f);
            Material("grout", new Color(.39f, .42f, .42f), .2f, .2f);
            Material("fascia", new Color(.17f, .19f, .215f), .35f, .5f);
            Material("yellow", new Color(.94f, .76f, .055f), .1f, .35f);
            Material("red", new Color(.52f, .12f, .12f), .25f, .35f);
            Material("blue", new Color(.02f, .16f, .55f), .1f, .25f);
            Material("tread", new Color(.12f, .14f, .155f), .8f, .45f);
            Material("glass", new Color(.58f, .79f, .79f, .12f), .1f, .9f);
            Transparent(materials["glass"], new Color(.6f, .81f, .81f, .12f));
            Material("teal", new Color(.16f, .55f, .55f, .31f), .15f, .9f);
            Transparent(materials["teal"], new Color(.16f, .55f, .55f, .31f));
            Material("led", new Color(.96f, .98f, 1), 0, .2f);
            materials["led"].EnableKeyword("_EMISSION"); materials["led"].SetColor("_EmissionColor", new Color(.96f, .98f, 1) * 2.1f);
            Material("warm", new Color(1, .88f, .65f), 0, .3f);
            materials["warm"].EnableKeyword("_EMISSION"); materials["warm"].SetColor("_EmissionColor", new Color(1, .84f, .55f) * 1.6f);
            foreach (var m in materials.Values)
            {
                if (m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0)
                {
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    m.EnableKeyword("_EMISSION");
                }
                EditorUtility.SetDirty(m);
            }
        }

        public void Build()
        {
            Region("envelope", "INFERRED_SOURCE_SECTION_ENVELOPE", "Curved source facade samples; end caps, rear boundary and floor coverage interpolated. Local PB/Design Skin wings override facade registration, not original facade assets.");
            Envelope();
            Region("filmed_core", "VIDEO_TOPOLOGY_SFM_RELATIVE_INFERRED_SCALE", "Frames174/200; exact common floor opening, two physical slopes. Relative incline fitted to175..199; seven-metre scale is assumed.");
            Core();
            Region("filmed_landing", "SFM_RELATIVE_LANDMARKS_INFERRED_7M_SCALE", "ground-landmark-probe.json: PB/shutter front nearv-45.9, left stainless column near(2.4,-41.4), right column near(8.4,-37.9). Seven-metre scale and shop depth remain inferred.");
            ParisFront();
            Column(2.4f, -41.4f, .72f, 3.8f);
            Column(8.4f, -37.9f, .78f, Ceiling);
            Sign("rail", -1.22f, -33.65f, 2.35f, 2.4f, .55f, 1, 0);
            Sign("food", 8.4f, -37.48f, 1.22f, .57f, 1.55f, 0, 1);
            Sign("food", 7.98f, -37.9f, 1.22f, .57f, 1.55f, -1, 0);
            Prop("Assets/ChooGuard/Art/WorldSet/Planter.fbx", 8.05f, -46.3f, 0, new Vector3(.55f, 1.05f, .55f), 0, 1);
            Prop(Base + "Kits/Web/FireExtCabinet.fbx", 2.58f, -44.15f, 0, new Vector3(.34f, .62f, .22f), 1, 0);
            Box("black", 3.85f, -40.35f, .006f, 10.55f, .42f, .009f, false);
            Box("black", 9.5f, -38.77f, .007f, .38f, 3.56f, .009f, false);
            Region("filmed_glass_aisle", "VIDEO_OBSERVED_TOPOLOGY_INFERRED_DIMENSIONS", "Frames208/216: Design Skin left and B&C Doughnut right when travelling +u; aisle v[-44,-40] remains open. Store depths inferred.");
            Shop("design", 10, 21.45f, -44, -49, true, "retail", true);
            Shop("bnc", 10, 21.45f, -40, -32, false, "bakery", true);
            Sign("clock", 10.03f, -44.13f, 3.1f, 1.18f, .49f, -1, 0);
            Box("black", 15.7f, -43.87f, .007f, 11.45f, .16f, .008f, false);
            Box("black", 15.7f, -40.12f, .007f, 11.45f, .16f, .008f, false);
            Region("filmed_exits", "VIDEO_OBSERVED_TYPE_INFERRED_LEAF_DIMENSIONS", "Frame220: multi-leaf glazed KTX exits3/4, KORAIL bands, yellow overhead signs and blue Metro Line1 banner. Two doors are posed open for the route; not fare gates.");
            ExitPortal();
            Region("inferred_retail", "SUPPLEMENTAL_TOPOLOGY_INFERRED_GEOMETRY_AND_EPOCH", "Long public strip with front and rear retail; names derive from older floor guide, not a verified complete2022 tenant roster. No interior-photo backdrops.");
            InferredRetail();
            Region("inferred_services", "SUPPLEMENTAL_TOPOLOGY_INFERRED_GEOMETRY_AND_EPOCH", "End WCs, luggage lockers, TMO, Zimcarry and public service rooms. Locations and dimensions inferred; not surveyed and not video-visible.");
            Services();
            Flush();
        }

        void ParisFront()
        {
            // Main's low-error static SfM points separate the near column from
            // the bakery frontage. v-41.3 was the column, not the glazed facade.
            // Rounded relative landmarks; metric scale and unseen depth remain inferred.
            const float front = -45.9f, back = -51.4f;
            Box("wall", 2.75f, back, 1.9f, 13.5f, .16f, 3.8f, true);
            Box("wall", -4, (front + back) / 2, 1.9f, .16f, front - back, 3.8f, true);
            Box("wall", 9.5f, (front + back) / 2, 1.9f, .16f, front - back, 3.8f, true);
            Box("wall", 4.7f, (front + back) / 2, 1.9f, .12f, front - back, 3.8f, true);
            Box("fascia", 9, front, 1.9f, 1, .24f, 3.8f, true);
            Box("wall", .35f, front, 1.9f, 8.7f, .18f, 3.8f, true);
            // Narrow shutter at the same depth as PB, not a broad nearby shopfront.
            Box("metal", 3.85f, front + .105f, 1.53f, 1.62f, .035f, 3.06f, true);
            for (float y = .06f; y < 3.04f; y += .065f)
            {
                Box("ceiling_joint", 3.85f, front + .13f, y, 1.56f, .012f, .009f, false);
                Box("metal", 3.85f, front + .133f, y + .021f, 1.56f, .014f, .02f, false);
            }
            Box("metal", 4.7f, front + .10f, 1.58f, .12f, .10f, 3.16f, true);
            Box("metal", 3, front + .10f, 1.58f, .12f, .10f, 3.16f, true);
            // Left steel partition links the independently recovered column and shutter.
            Box("metal", 2.4f, -43.65f, 1.9f, .16f, 4.5f, 3.8f, true);
            for (float v = -45.6f; v < -41.5f; v += .9f)
                Box("ceiling_joint", 2.486f, v, 1.9f, .012f, .009f, 3.8f, false);
            Box("red", 2.502f, -43.4f, 1.1f, .045f, 1.04f, 2.2f, false);
            Box("metal", 2.546f, -43.06f, 1.08f, .04f, .06f, .29f, false);
            GlassLine(4.7f, front, 5.85f, front, .04f, 3.12f, 1.2f, true);
            GlassLine(7.05f, front, 8.5f, front, .04f, 3.12f, 1.5f, true);
            GlassLine(5.85f, front, 7.05f, front, 2.52f, 3.12f, 1.3f, false);
            Box("fascia", 6.6f, front, 3.22f, 3.8f, .18f, .18f, true);
            Box("wall", 6.6f, front, 3.59f, 3.8f, .20f, .56f, true);
            Sign("paris", 6.6f, front + .105f, 3.22f, 3.1f, .145f, 0, 1);
            Sign("paris_logo", 7.8f, front + .115f, 1.93f, .72f, .72f, 0, 1);
            Prop(Base + "Props/BakeryDisplay.fbx", 5.65f, -48.3f, 0, new Vector3(1.6f, 1.35f, .85f), 0, 1);
            Prop(Base + "Props/BakeryDisplay.fbx", 7.55f, -49.4f, 0, new Vector3(1.6f, 1.35f, .85f), -1, 0);
            Prop(Restaurant + "kitchencounter_straight_A.fbx", 6.6f, -50.6f, 0, new Vector3(3.1f, .9f, .8f), 0, 1);
            Prop(Restaurant + "crate_buns.fbx", 5.8f, -50.5f, .91f, new Vector3(.72f, .32f, .55f), 0, 1);
            Prop(Restaurant + "crate_buns.fbx", 7.2f, -50.5f, .91f, new Vector3(.72f, .32f, .55f), 0, 1);
            for (float v = -46.7f; v > -51; v -= 1.5f)
            {
                Box("warm", 5.5f, v, 3.65f, .65f, .24f, .02f, false);
                Box("warm", 7.6f, v, 3.65f, .65f, .24f, .02f, false);
            }
            LocalLight(6.6f, -48, 3.4f, new Color(1, .84f, .64f), 1.25f, 6);
        }

        static readonly float[] KnotU = { -84, -76, -60, -40, -26, -10, 0, 20, 40, 60, 76 };
        static readonly float[] KnotV = { -27, -34, -39, -43, -44, -44.8f, -43.3f, -41.9f, -40.3f, -35.8f, -28 };
        float Facade(float u)
        {
            for (int i = 1; i < KnotU.Length; i++)
                if (u <= KnotU[i]) return Mathf.Lerp(KnotV[i - 1], KnotV[i], (u - KnotU[i - 1]) / (KnotU[i] - KnotU[i - 1]));
            return KnotV[KnotV.Length - 1];
        }
        float Front(float u, float stripMid)
        {
            if (stripMid >= -4 && stripMid < 9.5f) return -51.4f;
            if (stripMid >= 9.5f && stripMid < 21.5f) return -49;
            if (stripMid >= 21.5f && stripMid < 22.4f) return -45.5f;
            return Facade(u);
        }
        float Rear(float u) => u < -60 ? Mathf.Lerp(3, 14, (u + 84) / 24) : Mathf.Lerp(14, 3, (u + 60) / 136);

        void Envelope()
        {
            var cuts = new SortedSet<float>(KnotU);
            for (float u = -84; u < 76; u += 1.2f) cuts.Add(u);
            foreach (float u in new[] { -4f, 9.5f, 21.5f, 22.4f, u0, u1, 76f }) cuts.Add(u);
            var list = new List<float>(cuts);
            for (int i = 0; i < list.Count - 1; i++)
            {
                float a = list[i], b = list[i + 1], mid = (a + b) * .5f;
                float fa = Front(a, mid), fb = Front(b, mid), ra = Rear(a), rb = Rear(b);
                // 15mm stone finish above the preserved plaza/source slab, not coplanar duplicate faces.
                Quad("floor", P(a, fa, .015f), P(b, fb, .015f), P(b, rb, .015f), P(a, ra, .015f), true, (b - a) / .8f, (ra - fa) / .8f);
                // Tile grid follows the curved edge, rather than filling its bounding rectangle.
                for (float v = -52.6f; v < 14; v += 1.2f)
                {
                    float loA = Mathf.Max(v, fa), loB = Mathf.Max(v, fb);
                    float hiA = Mathf.Min(v + 1.2f, ra), hiB = Mathf.Min(v + 1.2f, rb);
                    if (loA >= hiA || loB >= hiB) continue;
                    if (mid >= u0 && mid <= u1)
                    {
                        if (loA < v0) CeilingTile(a, b, loA, loB, Mathf.Min(hiA, v0), Mathf.Min(hiB, v0));
                        if (hiA > v1) CeilingTile(a, b, Mathf.Max(loA, v1), Mathf.Max(loB, v1), hiA, hiB);
                    }
                    else CeilingTile(a, b, loA, loB, hiA, hiB);
                }
                // Rear service boundary, not a replacement for the original glass facade.
                Quad("wall", P(a, ra, 0), P(b, rb, 0), P(b, rb, Ceiling), P(a, ra, Ceiling), true, b - a, Ceiling);
            }
            for (float u = -78; u < 75; u += 9)
            {
                // No column forest through the filmed landing, corridor or central core.
                if (u > -5 && u < 25) continue;
                Column(u, -24, .7f, Ceiling);
                Column(u, -3.3f, .62f, Ceiling);
            }
            // Inferred end enclosure follows the tapered first-floor wing.
            Box("wall", -83.9f, -12, Ceiling / 2, .2f, 30, Ceiling, true);
            Box("wall", 75.9f, -12.5f, Ceiling / 2, .2f, 30.5f, Ceiling, true);
            for (float u = -79; u < 74; u += 6)
            {
                // Sparse local luminaires; Main retains ownership of environment and global light.
                LocalLight(u, -25, 3.88f, new Color(.91f, .96f, 1), 1.2f, 8);
                if (u < -9 || u > 25) LocalLight(u, -38, 3.85f, new Color(.95f, .97f, 1), .9f, 7);
            }
            LocalLight(5.5f, -36.5f, 3.7f, new Color(.96f, .98f, 1), 1.25f, 7);
            LocalLight(16.5f, -42, 3.85f, new Color(.95f, .98f, 1), 1.3f, 7);
        }

        void CeilingTile(float a, float b, float loA, float loB, float hiA, float hiB)
        {
            if (hiA <= loA || hiB <= loB) return;
            float u = (a + b) / 2, v = (loA + loB + hiA + hiB) / 4;
            float du = b - a, dv = Mathf.Min(hiA - loA, hiB - loB);
            float y = u >= -8 && u <= 10 && v >= -51.4f && v < v0 ? 3.8f : Ceiling;
            Quad("ceiling", P(a, loA, y), P(a, hiA, y), P(b, hiB, y), P(b, loB, y), true, (hiA - loA) / 1.2f, (b - a) / 1.2f);
            if (du < .55f || dv < .55f) return;
            Box("ceiling_joint", a + .002f, v, y - .002f, .004f, dv, .004f, false);
            Box("ceiling_joint", u, loA + .002f, y - .002f, du, .004f, .004f, false);
            int x = Mathf.FloorToInt((u + 84) / 1.2f), z = Mathf.FloorToInt((v + 49) / 1.2f);
            if ((x + z) % 5 == 0)
            {
                Box("ceiling_recess", u, v, y - .013f, .62f, .62f, .02f, false);
                Box("led", u, v, y - .025f, .56f, .56f, .009f, false);
            }
            else if ((x + 2 * z) % 4 == 0)
            {
                Ring(u, v, y - .014f, .112f, .088f, "ceiling_recess");
                Disc(u, v, y - .019f, .088f, "led");
            }
            else if ((x + z) % 2 == 0)
            {
                // Sparse 360mm white shallow recess; the remaining modules are flat.
                // No dark metallic rings or texture multiplied by geometric relief.
                Ring(u, v, y - .009f, .18f, .15f, "ceiling_recess");
                Disc(u, v, y - .001f, .15f, "ceiling");
            }
        }

        void Core()
        {
            // Lining stops at the low ceiling and never caps the shaft or the landing throats.
            foreach (float u in new[] { u0, u1 })
            {
                Box("wall", u, (v0 + v1) / 2, (Ceiling + 7) / 2, .12f, v1 - v0, 7 - Ceiling, true);
                for (float v = v0; v < v1; v += .85f) Box("grout", u + (u == u0 ? .064f : -.064f), v, 5.6f, .012f, .012f, 2.8f, false);
                for (float y = 4.3f; y < 7; y += .85f) Box("grout", u + (u == u0 ? .065f : -.065f), (v0 + v1) / 2, y, .012f, v1 - v0, .013f, false);
                Rail(u, v0, u, v1, 7);
            }
            Box("metal", (u0 + u1) / 2, v0 - .14f, 3.98f, u1 - u0 + .28f, .28f, .36f, true);
            Rail(u0, v0, u1, v0, 7);
            float[] centers = { (float)contract["sharedCore"]["descendingCentreU"], (float)contract["sharedCore"]["oppositeCentreU"] };
            foreach (float u in centers) Escalator(u);
            // Wide landing guide rails visible in200s; rounded dimensions inferred from the view.
            // Keep the recovered turning trajectory clear rather than extending the narrow belt skirts.
            foreach (float u in new[] { centers[0]-1.35f, centers[0]+1.35f })
            {
                foreach(float v in new[]{-33.8f,-35.7f})
                    Box("metal",u,v,.55f,.085f,.085f,1.1f,true);
                Tube(P(u,-33.8f,1.04f),P(u,-35.7f,1.04f),.028f,"metal",true);
                Tube(P(u,-33.8f,.57f),P(u,-35.7f,.57f),.023f,"metal",true);
            }
            // Guard the gaps between the two open upper entrances, not the entrances themselves.
            Rail(u0, v1, centers[0] - .79f, v1, 7);
            Rail(centers[0] + .79f, v1, centers[1] - .79f, v1, 7);
            Rail(centers[1] + .79f, v1, u1, v1, 7);
            Sign("rail", u0 - .14f, -21, 7.95f, 1.8f, .42f, 1, 0);
        }

        void Escalator(float u)
        {
            float half = clearWidth / 2, run = upperV - lowerV;
            const int steps = 40;
            float going = run / steps, stepRise = rise / steps;
            for (int i = 0; i < steps; i++)
            {
                float v = lowerV + i * going, y = i * stepRise;
                Box("tread", u, v + going / 2, y - .032f, clearWidth, going, .06f, false);
                Box("metal", u, v + going - .012f, y + stepRise / 2 - .03f, clearWidth, .023f, stepRise, false);
                Box("yellow", u, v + .02f, y + .001f, clearWidth, .025f, .008f, false);
                for (float cross = -half + .04f; cross < half; cross += .042f)
                    Box("metal", u + cross, v + going / 2, y + .002f, .009f, going - .02f, .005f, false);
            }
            // This invisible collision surface is continuous, exactly joins both landing floors,
            // and follows the recovered incline. Visible tread meshes never receive stair colliders.
            Quad(null, P(u - half, lowerV, 0), P(u + half, lowerV, 0), P(u + half, upperV, 7), P(u - half, upperV, 7), true);
            foreach (bool top in new[] { false, true })
            {
                float a = top ? upperV : v0, b = top ? v1 : lowerV, y = top ? 7 : 0;
                Quad("metal", P(u - half, a, y), P(u + half, a, y), P(u + half, b, y), P(u - half, b, y), true, 2, 2);
                for (float cross = -half + .025f; cross < half; cross += .05f)
                    Box("grout", u + cross, (a + b) / 2, y + .001f, .009f, b - a, .003f, false);
                Box("yellow", u, top ? upperV + .03f : lowerV - .03f, y + .004f, clearWidth, .06f, .005f, false);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float cross = u + side * (half + .12f);
                Quad("metal", P(cross, lowerV, -.55f), P(cross, upperV, 6.45f), P(cross, upperV, 7.25f), P(cross, lowerV, .25f), true, run, .8f);
                Quad("teal", P(cross, lowerV, .22f), P(cross, upperV, 7.22f), P(cross, upperV, 8.02f), P(cross, lowerV, 1.02f), true, run, .8f);
                foreach (float f in new[] { 0f, .2f, .4f, .6f, .8f, 1f })
                    Tube(P(cross, Mathf.Lerp(lowerV, upperV, f), 7 * f + .16f), P(cross, Mathf.Lerp(lowerV, upperV, f), 7 * f + 1.03f), .023f, "metal", false);
                var path = new[] { P(cross, v0 + .05f, .32f), P(cross, v0 - .04f, .65f), P(cross, v0 + .15f, .97f), P(cross, lowerV, 1.07f), P(cross, upperV, 8.07f), P(cross, v1 - .26f, 8.07f), P(cross, v1 - .09f, 7.84f), P(cross, v1 - .06f, 7.45f) };
                for (int i = 1; i < path.Length; i++) Tube(path[i - 1], path[i], .047f, "black", false);
                Quad("teal", P(cross, v0, .2f), P(cross, lowerV, .2f), P(cross, lowerV, 1), P(cross, v0, .86f), true);
                Quad("teal", P(cross, upperV, 7.2f), P(cross, v1 - .15f, 7.2f), P(cross, v1 - .15f, 7.95f), P(cross, upperV, 8.02f), true);
            }
        }

        void ExitPortal()
        {
            const float u = 22;
            // Glazed transom and stainless portal; no solid slab across pedestrian level.
            Box("metal", u, -42, 2.67f, .16f, 6.1f, .14f, true);
            Box("metal", u, -42, 4.11f, .16f, 6.1f, .12f, true);
            GlassLine(u, -45, u, -39, 2.74f, 4.06f, 1.5f, true);
            foreach (float v in new[] { -45f, -43.5f, -40.5f, -39f })
                Box("metal", u, v, 1.33f, .15f, .10f, 2.66f, true);
            // Outer fixed leaves and two inner leaves rotated open toward the exterior.
            DoorLeaf(u, -45, u, -43.54f);
            DoorLeaf(u, -39, u, -40.46f);
            DoorLeaf(u, -43.5f, u + 1.4f, -43.56f);
            DoorLeaf(u, -40.5f, u + 1.4f, -40.44f);
            Sign("exit3", u - .13f, -43.5f, 2.97f, 2.83f, .46f, -1, 0);
            Sign("exit4", u - .13f, -40.5f, 2.97f, 2.83f, .46f, -1, 0);
            // Banner deliberately occupies the side pocket, not the registered v=-42 path.
            Sign("metro", u - .08f, -40.88f, 1.15f, .53f, 1.65f, -1, 0);
            Box("metal", u - .05f, -40.88f, .055f, .33f, .60f, .11f, true);
            Box("led", u - .13f, -42, 3.47f, .05f, .32f, .16f, false);
            // The external covered plaza begins after this threshold and is Main-owned.
            Box("metal", u, -42, -.018f, .32f, 6, .03f, false);
        }

        void DoorLeaf(float a, float b, float c, float d)
        {
            GlassLine(a, b, c, d, .10f, 2.61f, 4, true);
            Tube(P(a, b, .09f), P(c, d, .09f), .028f, "metal", false);
            Tube(P(a, b, 2.61f), P(c, d, 2.61f), .028f, "metal", false);
            float hu = Mathf.Lerp(a, c, .82f), hv = Mathf.Lerp(b, d, .82f);
            Tube(P(hu - .07f, hv, .72f), P(hu - .07f, hv, 1.61f), .018f, "metal", false);
            float length = Vector2.Distance(new Vector2(a, b), new Vector2(c, d));
            float nu = -(d - b) / length, nv = (c - a) / length;
            if (nu > 0) { nu = -nu; nv = -nv; }
            Sign("korail", (a + c) / 2 + nu * .025f, (b + d) / 2 + nv * .025f, 1.06f, length * .9f, .105f, nu, nv);
        }

        void Shop(string name, float a, float b, float front, float back, bool facingPositiveV, string type, bool glassCorner)
        {
            float mid = (a + b) / 2, depth = Mathf.Abs(back - front), inward = back > front ? 1 : -1;
            Box("wall", mid, back, 1.9f, b - a, .15f, 3.8f, true);
            if (glassCorner) GlassLine(a, front, a, back, .05f, 3.72f, 1.8f, true);
            else Box("wall", a, (front + back) / 2, 1.92f, .15f, depth, 3.84f, true);
            Box("wall", b, (front + back) / 2, 1.92f, .15f, depth, 3.84f, true);
            const float doorHalf = .9f;
            GlassLine(a, front, mid - doorHalf, front, .03f, 3.17f, 1.65f, true);
            GlassLine(mid + doorHalf, front, b, front, .03f, 3.17f, 1.65f, true);
            GlassLine(mid - doorHalf, front, mid + doorHalf, front, 2.6f, 3.17f, 1.8f, false);
            Box("fascia", mid, front, 3.53f, b - a, .24f, .65f, true);
            Sign(name, mid, front + (facingPositiveV ? .13f : -.13f), 3.52f, Mathf.Min(b - a - .65f, 7.2f), .52f, 0, facingPositiveV ? 1 : -1);
            if (glassCorner)
            {
                Box("fascia", a, (front + back) / 2, 3.52f, .22f, depth, .62f, true);
                Sign(name, a - .12f, (front + back) / 2, 3.52f, Mathf.Min(depth - .4f, 4.8f), .48f, -1, 0);
            }
            for (float u = a + 1.5f; u < b - .7f; u += 2.4f)
            {
                if (type == "retail")
                {
                    Prop(Base + "Props/StoreShelf.fbx", u, back - inward * .7f, 0, new Vector3(1.65f, 2.05f, .55f), 0, -inward);
                    if (depth > 5.5f) Prop(Base + "Props/StoreShelf.fbx", u, front + inward * 2.7f, 0, new Vector3(1.55f, 1.35f, .65f), 0, -inward);
                }
                else if (type == "bakery")
                {
                    Prop(Base + "Props/BakeryDisplay.fbx", u, front + inward * 1.65f, 0, new Vector3(1.9f, 1.35f, .85f), 0, -inward);
                    Prop(Restaurant + "crate_buns.fbx", u, back - inward * .8f, .8f, new Vector3(.7f, .34f, .55f), 0, -inward);
                    Prop(Restaurant + "kitchencounter_straight_A.fbx", u, back - inward * .8f, 0, new Vector3(1.65f, .81f, .72f), 0, -inward);
                }
                else
                {
                    Prop(Restaurant + "table_round_A.fbx", u, front + inward * 2.3f, 0, new Vector3(.88f, .75f, .88f), 0, 1);
                    Prop(Restaurant + "chair_A.fbx", u, front + inward * 1.53f, 0, new Vector3(.45f, .86f, .48f), 0, inward);
                    Prop(Restaurant + "chair_A.fbx", u, front + inward * 3.07f, 0, new Vector3(.45f, .86f, .48f), 0, -inward);
                }
                Box("warm", u, front + inward * 1.1f, 3.7f, 1.05f, .34f, .035f, false);
            }
            if (type == "dining") Prop(Restaurant + "kitchencounter_straight_A.fbx", mid, back - inward * .6f, 0, new Vector3(2.6f, 1, .85f), 0, -inward);
            LocalLight(mid, front + inward * Mathf.Min(depth * .55f, 3), 3.5f, new Color(1, .83f, .62f), 1.25f, Mathf.Min(7, depth + 1));
        }

        void InferredRetail()
        {
            string[] rearSigns = { "sushi", "udon", "boxcup", "shabu", "gaemijip" };
            for (int i = 0; i < rearSigns.Length; i++)
            {
                float a = -62 + i * 12;
                Shop(rearSigns[i], a, a + 11.6f, -18, -6.4f, false, "dining", false);
            }
            // Clear central core and through-corridor; continue retail beyond its opposite side.
            Shop("convenience", 14, 29, -18, -7, false, "retail", false);
            Shop("cafe", 30, 43, -18, -7, false, "dining", false);
            Shop("gukbap", 44, 57, -18, -7, false, "dining", false);
            Shop("closed", 58, 69, -18, -7, false, "retail", false);
            string[] frontSigns = { "gimbap", "convenience", "cafe", "fishcake" };
            for (int i = 0; i < frontSigns.Length; i++)
            {
                float a = -67 + i * 13.5f, b = a + 11.9f;
                float back = Mathf.Max(Facade(a), Facade(b)) + .5f;
                Shop(frontSigns[i], a, b, -31.8f, back, true, i == 1 ? "retail" : "dining", false);
            }
            Shop("fishcake", 27, 39, -31.5f, -39, true, "dining", false);
            Shop("convenience", 41, 52.5f, -30, -36.5f, true, "retail", false);
            Shop("gukbap", 55, 67, -25.5f, -32.1f, true, "dining", false);
            for (float u = -69; u < 72; u += 15)
            {
                if (u > -6 && u < 27) continue;
                Prop(Base + "Kits/Web/WaitingBench.fbx", u, -20.3f, 0, new Vector3(2.7f, .9f, .65f), 0, -1);
                Prop(Base + "Props/TrashCan.fbx", u + 2, -20.2f, 0, new Vector3(.42f, .85f, .42f), 0, -1);
            }
            // Supplemental numbered door groups: offsets and leaf counts are explicitly inferred.
            SecondaryExit("exit5", -53.9f);
            SecondaryExit("exit2", 25);
            SecondaryExit("exit1", 69.4f);
            Sign("exit6", -73.5f, -16, 3.35f, 2, .46f, 1, 0);
        }

        void SecondaryExit(string sign, float u)
        {
            float v = Facade(u) + .25f;
            for (int s = -1; s <= 1; s += 2) Box("metal", u + s * 1.5f, v, 1.5f, .12f, .14f, 3, true);
            Box("metal", u, v, 2.85f, 3.1f, .18f, .18f, true);
            Sign(sign, u, v + .13f, 3.25f, 2.8f, .44f, 0, 1);
            GlassLine(u - 1.5f, v, u - .9f, v, .05f, 2.7f, 1, true);
            GlassLine(u + .9f, v, u + 1.5f, v, .05f, 2.7f, 1, true);
        }

        void Services()
        {
            ServiceRoom("tmo", -79, -67, -16, -2);
            Prop(Base + "Kits/Web/TicketCounter.fbx", -73.2f, -8, 0, new Vector3(3.8f, 1.12f, .9f), 0, -1);
            Prop(Base + "Kits/Web/WaitingBench.fbx", -76.4f, -13.5f, 0, new Vector3(2.5f, .9f, .65f), 0, 1);
            // Rear backs stop 0.3m in front of the original 1F rear wall line kept under rule F6
            // (source-intrusion-cuts.json: unobserved, outside guide-derived public zones).
            ServiceRoom("zimcarry", -63, -49, -3, 2.7f);
            Prop(Base + "Kits/Web/TicketCounter.fbx", -55.5f, .3f, 0, new Vector3(4, 1.12f, .85f), 0, -1);
            for (float u = -61; u <= -51; u += 2.5f) Prop(Base + "Kits/Web/LuggageLocker.fbx", u, 2.1f, 0, new Vector3(2.25f, 2.05f, .65f), 0, -1);
            ServiceRoom("lockers", -46, -30, -3, 3.7f);
            for (float u = -44; u <= -32; u += 3) Prop(Base + "Kits/Web/LuggageLocker.fbx", u, 3.1f, 0, new Vector3(2.65f, 2.02f, .6f), 0, -1);
            Prop(Base + "Kits/Web/LuggageLocker.fbx", -44.9f, .2f, 0, new Vector3(2.5f, 2.02f, .6f), 1, 0);
            Prop(Base + "Kits/Web/LuggageLocker.fbx", -31.1f, .2f, 0, new Vector3(2.5f, 2.02f, .6f), -1, 0);
            Restroom(-81, -69, -1, 3.2f);
            Restroom(62, 74, -4, 2.5f);
            ServiceRoom("service", -26, -11, -3, 5.2f);
            Prop(Base + "Props/InfoKiosk.fbx", -21, -1.8f, 0, new Vector3(.8f, 1.8f, .55f), 0, -1);
            Prop(Base + "Props/TicketBooth.fbx", -17, 4, 0, new Vector3(3.8f, 2.4f, 1.2f), 0, -1);
            ServiceRoom("service", 31, 58, -4, 4.2f);
            for (float u = 34; u < 57; u += 6)
            {
                Prop(Base + "Props/AtmKiosk.fbx", u, -2.7f, 0, new Vector3(.72f, 1.8f, .66f), 0, -1);
                Prop(Base + "Kits/Web/KorailVendingMachine.fbx", u + 1.15f, -2.7f, 0, new Vector3(.85f, 1.9f, .7f), 0, -1);
            }
            Prop(Base + "Kits/Web/WaterCooler.fbx", -27.8f, -4.3f, 0, new Vector3(.5f, 1.1f, .48f), 0, -1);
        }

        void ServiceRoom(string sign, float a, float b, float front, float back)
        {
            float mid = (a + b) / 2;
            Box("wall", a, (front + back) / 2, 2, .16f, back - front, 4, true);
            Box("wall", b, (front + back) / 2, 2, .16f, back - front, 4, true);
            Box("wall", mid, back, 2, b - a, .16f, 4, true);
            Box("fascia", mid, front, 3.38f, b - a, .18f, .7f, true);
            GlassLine(a, front, mid - 1, front, .05f, 3, 1.8f, true);
            GlassLine(mid + 1, front, b, front, .05f, 3, 1.8f, true);
            Sign(sign, mid, front - .11f, 3.38f, Mathf.Min(6, b - a - .5f), .52f, 0, -1);
            LocalLight(mid, (front + back) / 2, 3.7f, new Color(.95f, .97f, 1), 1.3f, 8);
        }

        void Restroom(float a, float b, float front, float back)
        {
            ServiceRoom("wc", a, b, front, back);
            // Public washroom fitout is inferred; cubicles screen private fixtures from concourse.
            float split = (a + b) / 2;
            Box("wall", split, (front + back) / 2 + .55f, 1.4f, .12f, back - front - 1.1f, 2.8f, true);
            for (float u = a + 1; u < b - .5f; u += 1.5f)
            {
                Box("wall", u, back - 1.05f, 1.08f, .07f, 1.8f, 2.16f, true);
                Box("fascia", u + .65f, back - 1.98f, 1.08f, 1.22f, .045f, 2.05f, true);
                Box("metal", u + 1.12f, back - 2.02f, 1.05f, .04f, .045f, .12f, false);
            }
            Prop(Restaurant + "kitchencounter_sink.fbx", a + 1.7f, front + .8f, 0, new Vector3(2.5f, .85f, .6f), 0, 1);
            Prop(Restaurant + "kitchencounter_sink.fbx", b - 1.7f, front + .8f, 0, new Vector3(2.5f, .85f, .6f), 0, 1);
            Box("metal", a + 1.7f, front + .29f, 1.5f, 2.5f, .035f, .75f, false);
            Box("metal", b - 1.7f, front + .29f, 1.5f, 2.5f, .035f, .75f, false);
        }

        void Column(float u, float v, float width, float height)
        {
            Box("metal", u, v, height / 2, width, width, height, true);
            Box("black", u, v, .08f, width + .02f, width + .02f, .16f, false);
            for (float y = .7f; y < height; y += .85f)
            {
                Box("grout", u, v - width / 2 - .005f, y, width, .009f, .009f, false);
                Box("grout", u - width / 2 - .005f, v, y, .009f, width, .009f, false);
            }
        }

        void GlassLine(float a, float b, float c, float d, float bottom, float top, float spacing, bool collision)
        {
            float length = Vector2.Distance(new Vector2(a, b), new Vector2(c, d));
            if (length < .01f) return;
            Quad("glass", P(a, b, bottom), P(c, d, bottom), P(c, d, top), P(a, b, top), collision, length, top - bottom);
            int divisions = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
            for (int i = 0; i <= divisions; i++)
            {
                float t = (float)i / divisions, u = Mathf.Lerp(a, c, t), v = Mathf.Lerp(b, d, t);
                Box("metal", u, v, (bottom + top) / 2, .055f, .055f, top - bottom, collision);
            }
            Tube(P(a, b, top), P(c, d, top), .027f, "metal", false);
            Tube(P(a, b, bottom), P(c, d, bottom), .026f, "metal", false);
        }

        void Rail(float a, float b, float c, float d, float floor)
        {
            float length = Vector2.Distance(new Vector2(a, b), new Vector2(c, d));
            if (length < .05f) return;
            Quad("teal", P(a, b, floor + .14f), P(c, d, floor + .14f), P(c, d, floor + 1.06f), P(a, b, floor + 1.06f), true, length, 1);
            int posts = Mathf.Max(1, Mathf.CeilToInt(length / 1.15f));
            for (int i = 0; i <= posts; i++)
            {
                float t = (float)i / posts;
                Tube(P(Mathf.Lerp(a, c, t), Mathf.Lerp(b, d, t), floor), P(Mathf.Lerp(a, c, t), Mathf.Lerp(b, d, t), floor + 1.12f), .03f, "metal", false);
            }
            Tube(P(a, b, floor + 1.1f), P(c, d, floor + 1.1f), .042f, "metal", false);
        }

        void Ring(float u, float v, float y, float outer, float inner, string mat)
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
                Quad(mat, P(u + outer * Mathf.Cos(a), v + outer * Mathf.Sin(a), y), P(u + inner * Mathf.Cos(a), v + inner * Mathf.Sin(a), y + .012f), P(u + inner * Mathf.Cos(b), v + inner * Mathf.Sin(b), y + .012f), P(u + outer * Mathf.Cos(b), v + outer * Mathf.Sin(b), y), false);
            }
        }
        void Disc(float u, float v, float y, float radius, string mat) => Ring(u, v, y, radius, .001f, mat);

        void Tube(Vector3 a, Vector3 b, float radius, string mat, bool collision)
        {
            Vector3 along = (b - a).normalized;
            Vector3 across = Vector3.Cross(along, Mathf.Abs(along.y) > .9f ? Vector3.right : Vector3.up).normalized * radius;
            Vector3 other = Vector3.Cross(along, across).normalized * radius;
            for (int i = 0; i < 8; i++)
            {
                float t = i * Mathf.PI / 4, q = (i + 1) * Mathf.PI / 4;
                Vector3 r = across * Mathf.Cos(t) + other * Mathf.Sin(t), s = across * Mathf.Cos(q) + other * Mathf.Sin(q);
                Quad(mat, a + r, b + r, b + s, a + s, collision, (b - a).magnitude, .12f);
            }
        }

        void Box(string mat, float u, float v, float y, float du, float dv, float dy, bool collision)
        {
            float a = u - du / 2, b = u + du / 2, c = v - dv / 2, d = v + dv / 2, lo = y - dy / 2, hi = y + dy / 2;
            Quad(mat, P(a, c, hi), P(b, c, hi), P(b, d, hi), P(a, d, hi), collision, du, dv);
            Quad(mat, P(a, d, lo), P(b, d, lo), P(b, c, lo), P(a, c, lo), collision, du, dv);
            Quad(mat, P(a, c, lo), P(b, c, lo), P(b, c, hi), P(a, c, hi), collision, du, dy);
            Quad(mat, P(b, d, lo), P(a, d, lo), P(a, d, hi), P(b, d, hi), collision, du, dy);
            Quad(mat, P(a, d, lo), P(a, c, lo), P(a, c, hi), P(a, d, hi), collision, dv, dy);
            Quad(mat, P(b, c, lo), P(b, d, lo), P(b, d, hi), P(b, c, hi), collision, dv, dy);
        }

        Batch GetBatch(string mat, bool collision)
        {
            string key = zone + "_" + (mat ?? "slope_collision") + (collision ? "_physical" : "_visual");
            if (!batches.TryGetValue(key, out var batch))
            {
                batch = new Batch { Material = mat == null ? null : materials[mat], Collision = collision, Zone = zone };
                batches.Add(key, batch);
            }
            return batch;
        }
        void Quad(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool collision, float width = 1, float height = 1)
            => GetBatch(mat, collision).Data.Quad(a, b, c, d, width, height);

        void Sign(string key, float u, float v, float y, float width, float height, float normalU, float normalV)
        {
            Vector3 normal = W(normalU, normalV, 0).normalized;
            Vector3 right = Vector3.Cross(normal, Vector3.up).normalized;
            Vector3 center = W(u, v, y), x = right * width / 2, up = Vector3.up * height / 2;
            GetBatch("sign_" + key, false).Data.SignQuad(center - x - up, center + x - up, center + x + up, center - x + up);
        }

        void Prop(string path, float u, float v, float floor, Vector3 target, float directionU, float directionV)
        {
            var container = new GameObject(Path.GetFileNameWithoutExtension(path));
            container.transform.SetParent(Group(zone), false);
            // Fit scale/offset live on an identity-rotation parent so they act in container axes; the instance
            // keeps its authored ancestor orientation (X270 Blender roots, 180/270-yaw FBX subassemblies).
            var fit = new GameObject("fit").transform;
            fit.SetParent(container.transform, false);
            var instance = UnityEngine.Object.Instantiate(prefabs[path], fit);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = prefabs[path].transform.rotation;
            instance.transform.localScale = Vector3.one;
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Acquired prop has no renderers: " + path);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.x <= .00001f || bounds.size.y <= .00001f || bounds.size.z <= .00001f)
                throw new InvalidOperationException("Invalid acquired model bounds: " + path);
            fit.localScale = new Vector3(target.x / bounds.size.x, target.y / bounds.size.y, target.z / bounds.size.z);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            fit.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var renderer in renderers)
            {
                var source = renderer.sharedMaterials;
                for (int i = 0; i < source.Length; i++) source[i] = PropMaterial(source[i]);
                renderer.sharedMaterials = source;
            }
            var collision = container.AddComponent<BoxCollider>();
            collision.center = new Vector3(0, target.y / 2, 0); collision.size = target;
            container.transform.position = W(u, v, floor);
            container.transform.rotation = Quaternion.LookRotation(W(directionU, directionV, 0), Vector3.up);
            foreach (var tr in container.GetComponentsInChildren<Transform>(true)) tr.gameObject.isStatic = true;
            propCount++;
        }

        Material PropMaterial(Material source)
        {
            if (!source) return materials["metal"];
            if (source.shader && source.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)) return source;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
            string key = "prop_" + guid + "_" + id;
            if (materials.TryGetValue(key, out var existing)) return existing;
            string path = Art + "/PropMaterials/" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            if (source.HasProperty("_MainTex")) m.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
            m.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            m.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : .35f);
            m.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0);
            if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap")) { m.SetTexture("_BumpMap", source.GetTexture("_BumpMap")); m.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(m); materials.Add(key, m); return m;
        }

        void LocalLight(float u, float v, float y, Color color, float intensity, float range)
        {
            var go = new GameObject("Local fixture light"); go.transform.SetParent(Group(zone), false); go.transform.position = W(u, v, y);
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range;
            light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForceVertex;
            localLightCount++;
        }

        void Region(string id, string status, string basis)
        {
            zone = id;
            regions.Add(new JObject { ["id"] = id, ["evidenceStatus"] = status, ["basis"] = basis });
            Group(id);
        }
        Transform Group(string id)
        {
            if (!groups.TryGetValue(id, out var group))
            {
                var go = new GameObject(id); group = go.transform; group.SetParent(root.transform, false); groups.Add(id, group);
            }
            return group;
        }

        void Flush()
        {
            foreach (var entry in batches)
            {
                var b = entry.Value;
                if (b.Data.Vertices.Count == 0) continue;
                string path = Art + "/Meshes/" + entry.Key + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool create = mesh == null;
                mesh = b.Data.Make(entry.Key, mesh);
                if (create) AssetDatabase.CreateAsset(mesh, path);
                else EditorUtility.SetDirty(mesh);
                var go = new GameObject(entry.Key); go.transform.SetParent(Group(b.Zone), false); go.isStatic = true;
                if (b.Material)
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = b.Material;
                    if (b.Material.renderQueue >= 3000) renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                if (b.Collision)
                {
                    Mesh collisionMesh = mesh;
                    if (b.Material && b.Material.renderQueue >= 3000)
                    {
                        // Glass is physically two-sided without doubling its rendered opacity.
                        string collisionName = entry.Key + "_two_sided_collision";
                        var front = b.Data.Triangles;
                        var both = new List<int>(front.Count * 2);
                        both.AddRange(front);
                        for (int i = 0; i < front.Count; i += 3)
                        {
                            both.Add(front[i]); both.Add(front[i + 2]); both.Add(front[i + 1]);
                        }
                        string colliderPath = Art + "/Meshes/" + collisionName + ".asset";
                        collisionMesh = AssetDatabase.LoadAssetAtPath<Mesh>(colliderPath);
                        bool createCollision = collisionMesh == null;
                        if (createCollision) collisionMesh = new Mesh();
                        collisionMesh.Clear(); collisionMesh.name = collisionName; collisionMesh.indexFormat = IndexFormat.UInt32;
                        collisionMesh.SetVertices(b.Data.Vertices); collisionMesh.SetTriangles(both, 0);
                        collisionMesh.RecalculateBounds(); collisionMesh.UploadMeshData(false);
                        if (createCollision) AssetDatabase.CreateAsset(collisionMesh, colliderPath);
                        else EditorUtility.SetDirty(collisionMesh);
                    }
                    go.AddComponent<MeshCollider>().sharedMesh = collisionMesh;
                }
                meshCount++;
            }
        }

        Material Material(string key, Color color, float metallic, float smoothness)
        {
            if (materials.TryGetValue(key, out var value)) return value;
            string path = Art + "/Materials/" + key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Cull", 0); EditorUtility.SetDirty(m); materials.Add(key, m); return m;
        }
        void CloneMaterial(string key, string source, Color color, float smoothness, float metallic)
        {
            var original = Required<Material>(source);
            var m = Material(key, color, metallic, smoothness);
            m.CopyPropertiesFromMaterial(original); m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic); m.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(m);
        }
        void LoadPrefab(string path)
        {
            var prefab = Required<GameObject>(path);
            if (path == Base + "Props/StoreShelf.fbx")
            {
                Transform found = null;
                foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                    if (child.name == "GONDOLA1" && child.GetComponentsInChildren<MeshRenderer>(true).Length > 0) { found = child; break; }
                if (found == null) throw new InvalidOperationException("Acquired supermarket shelf subtree is missing: GONDOLA1");
                prefab = found.gameObject;
            }
            if (path == Base + "Props/BakeryDisplay.fbx")
            {
                // Offline FBX hierarchy inspection: Assembly-1071 contains two entire
                // display assemblies (Node_3:418 meshes, Node_978:445), not one case.
                // Reuse one complete case, including its real shelf and bread meshes.
                Transform found = null;
                foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Node_3" && child.GetComponentsInChildren<MeshRenderer>(true).Length > 0) { found = child; break; }
                if (found == null) throw new InvalidOperationException("Acquired bakery case subtree is missing: Node_3");
                prefab = found.gameObject;
            }
            prefabs.Add(path, prefab);
        }

        public JObject Receipt()
        {
            return new JObject
            {
                ["schema"] = "chooguard.video-floor-one-build.v1",
                ["status"] = "BUILDER_EXECUTED_NOT_SCENE_VALIDATED",
                ["root"] = RootName,
                ["basis"] = "Video174/200/208/216/220 plus source MainShell sections and supplemental older floor-guide topology; not surveyed completion.",
                ["epoch"] = contract["epoch"].DeepClone(),
                ["coordinateSystem"] = contract["coordinateSystem"].DeepClone(),
                ["sharedCore"] = contract["sharedCore"].DeepClone(),
                ["regions"] = regions,
                ["dimensions"] = new JObject
                {
                    ["generalCeilingY"] = Ceiling, ["lowerSoffitUndersideY"] = 3.8,
                    ["landingCeilingY"] = 3.8,
                    ["parisGlazedBayU"] = new JArray(4.7, 8.5),
                    ["parisShutterBayU"] = new JArray(3, 4.7),
                    ["parisBackVInferred"] = -51.4,
                    ["leftColumnUV"] = new JArray(2.4, -41.4),
                    ["rightColumnUV"] = new JArray(8.4, -37.9),
                    ["dimensionStatus"] = "INFERRED unless sharedCore states relative SfM fit; even seven-metre rise uses inferred metric scale",
                    ["publicEnvelopeURange"] = new JArray(-84, 76),
                    ["filmedClearAisleV"] = new JArray(-44, -40),
                    ["parisFrontV"] = -45.9, ["designSkinBoundsUV"] = new JArray(10, 21.45, -49, -44),
                    ["bncBoundsUV"] = new JArray(10, 21.45, -40, -32), ["exitPortalU"] = 22,
                    ["escalatorTreadsEach"] = 40, ["escalatorClearWidthEach"] = clearWidth,
                    ["escalatorRise"] = rise, ["escalatorRun"] = upperV - lowerV,
                    ["escalatorSlopeDegrees"] = Mathf.Atan2(rise, upperV - lowerV) * Mathf.Rad2Deg
                },
                ["refinementBasis"] = new JObject
                {
                    ["observedMismatch"] = "Main scene-video200/game-video200-initial versus video200/208/216: full-width PB glazing, repeated whole bakery assembly and oversized dark ceiling rings.",
                    ["response"] = "ground-landmark-probe.json supersedes the coarsev-41.3 PB depth: glazed bayu[4.7,8.5] atv-45.9, shutteru[3,4.7] beside it; near columns remain atv-41.4/-37.9. Walking turn stays nearv-40. Low3.8m panels extend to the recessed frontage; unseen shopbackv-51.4 is inferred.",
                    ["landmarkEvidence"] = "SfM5 low-error static frame200 points transformed5-to4-to-world; seven-metre scale still inferred. Rounded placement, not surveyed centimetre accuracy.",
                    ["acquiredBakerySubtree"] = "BakeryDisplay.fbx/Node_3 (one of two display assemblies)",
                    ["acquiredPlant"] = "WorldSet/Planter.fbx: soil, planter wall and three leaf-crown meshes; replaces bare FlowerPot.",
                    ["verification"] = "Source refinement only. Main rerun and camera comparison pending; dimensions remain inferred."
                },
                ["created"] = new JObject { ["batchedMeshes"] = meshCount, ["acquiredModelInstances"] = propCount, ["localFixtureLights"] = localLightCount, ["assetGuidPolicy"] = "Existing mesh assets cleared and populated through Mesh setters with explicit GPU upload; no DeleteAsset" },
                ["integration"] = new JArray(
                    "Main must remove obsolete synthetic roots and ensure source floors/ceilings do not fill the exact shared opening.",
                    "Main owns original source collision and facade integration. PB/Design Skin contract geometry extends beyond sampled facade; source is deliberately untouched.",
                    "Main must visually verify storefront fitting, transparent sorting, lighting and route clearance in Editor; no validation was run by the builder author.",
                    "The .7m route clearance goal applies to concourse; authored escalator tread clear width is contract1.15m, so controller radius must be below .575m.",
                    "Older supplementary tenant labels and WC/service interiors are inference, not verified2022 tenant or fixture inventory.",
                    "Restroom basins reuse acquired sink models; private sanitary fixtures behind closed cubicles are outside observed public reconstruction.")
            };
        }
    }

    static void Transparent(Material m, Color color)
    {
        m.SetColor("_BaseColor", color); m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000; EditorUtility.SetDirty(m);
    }
    static T Required<T>(string path) where T : UnityEngine.Object
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!value) throw new InvalidOperationException("Required existing asset is missing: " + path);
        return value;
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static void Main(string[] args)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run VideoFloorOne in Edit mode.");
        string contractPath = args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]) ? args[0] : ContractPath;
        var contract = JObject.Parse(File.ReadAllText(contractPath));
        if (!Shader.Find("Universal Render Pipeline/Lit")) throw new InvalidOperationException("The existing URP Lit shader is required.");
        // Stage under the owned root name; do not disturb old source/sibling roots even on failure.
        var old = GameObject.Find(RootName);
        var stage = new GameObject(RootName + " · assembling");
        try
        {
            var builder = new Builder(contract, stage);
            builder.Prepare(); builder.Build();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(ReceiptPath));
            File.WriteAllText(ReceiptPath, builder.Receipt().ToString(Newtonsoft.Json.Formatting.Indented));
            if (old) UnityEngine.Object.DestroyImmediate(old);
            stage.name = RootName;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("VIDEO_FLOOR_ONE_BUILT " + ReceiptPath + " — construction receipt only; Main owns scene verification.");
        }
        catch
        {
            if (stage) UnityEngine.Object.DestroyImmediate(stage);
            throw;
        }
    }
}
