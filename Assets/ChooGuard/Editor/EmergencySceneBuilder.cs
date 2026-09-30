using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Builds the main-game shell around the modelling scene without editing it: title (Bootstrap), the additive
    /// StationEmergency scene, the title/map images rendered from the twin, and the build scene order.
    /// </summary>
    public static class EmergencySceneBuilder
    {
        public const string ArtRoot = "Assets/ChooGuard/Art/Emergency";
        public const string UiRoot = ArtRoot + "/UI";
        public const string TitleImagePath = UiRoot + "/TitleStation.png";
        public const string MapImagePath = UiRoot + "/StationMap2F.png";
        public const string EmergencyScenePath = "Assets/ChooGuard/Scenes/StationEmergency.unity";
        public const string StationScenePath = "Assets/ChooGuard/Scenes/FpsStation.unity";
        public const string BootstrapScenePath = "Assets/ChooGuard/Scenes/Bootstrap.unity";
        public const string KoreanFontPath = "Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset";

        // 2층 맞이방과 바로 붙은 외곽. +X 동, +Z 북 (station-interior-spec coordinateNote).
        public static readonly Rect MapBounds = new Rect(-80, -100, 210, 210);
        private const float MapCameraY = 9.9f, MapDepth = 4.6f, MapPixelsPerMetre = 6f;

        [MenuItem("ChooGuard/Emergency/Render title and map images")]
        public static void RenderImages()
        {
            RequireStationOpen();
            EnsureFolder(UiRoot);
            // 대합실 좌석·출발 안내판·트러스 지붕이 한 화면에 드는 시점 (사람 눈높이 1.8m).
            RenderPerspective(TitleImagePath, new Vector3(64, 8.8f, -2), Quaternion.Euler(1, 10, 0), 58, 1920, 1080);
            RenderTopDown(MapImagePath, MapBounds, MapCameraY, MapDepth, MapPixelsPerMetre);
            AssetDatabase.ImportAsset(TitleImagePath);
            AssetDatabase.ImportAsset(MapImagePath);
            foreach (var path in new[] { TitleImagePath, MapImagePath })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            Debug.Log("CG_EMERGENCY_IMAGES " + TitleImagePath + " " + MapImagePath);
        }

        public const string ArtAssetPath = ArtRoot + "/EmergencyArt.asset";
        public const string MaterialRoot = ArtRoot + "/Materials";

        [MenuItem("ChooGuard/Emergency/Build session scene and title")]
        public static void BuildScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("재생을 종료한 뒤 실행하세요.");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath) ?? throw new FileNotFoundException(KoreanFontPath);
            var title = AssetDatabase.LoadAssetAtPath<Texture2D>(TitleImagePath) ?? throw new FileNotFoundException("먼저 'Render title and map images'를 실행하세요: " + TitleImagePath);
            var map = AssetDatabase.LoadAssetAtPath<Texture2D>(MapImagePath) ?? throw new FileNotFoundException(MapImagePath);
            var art = BuildArt();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("열린 씬의 변경을 먼저 저장하세요.");
            try
            {
                BuildEmergencyScene(font, map, art);
                BuildTitle(font, title);
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(StationScenePath, true),
                new EditorBuildSettingsScene(EmergencyScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("CG_EMERGENCY_SCENES " + EmergencyScenePath + " + title + build order");
        }

        private static void BuildEmergencyScene(TMP_FontAsset font, Texture2D map, EmergencyArt art)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("비상대응 세션");
            SceneManager.MoveGameObjectToScene(root, scene);
            var session = root.AddComponent<EmergencySession>();
            session.KoreanFont = font;
            session.StationMap = map;
            session.StationMapBounds = MapBounds;
            session.Art = art;
            session.Passengers = 110;
            session.StationMapLabel = "2층 평면 · 역 전체";
            if (!EditorSceneManager.SaveScene(scene, EmergencyScenePath)) throw new IOException("저장 실패: " + EmergencyScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void BuildTitle(TMP_FontAsset font, Texture2D background)
        {
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Additive);
            try
            {
                var canvas = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).Single();
                foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true)) Object.DestroyImmediate(text.gameObject);
                var titleScreen = canvas.GetComponent<TitleScreen>() ?? canvas.gameObject.AddComponent<TitleScreen>();
                titleScreen.KoreanFont = font;
                titleScreen.Background = background;
                var camera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true)).Single();
                camera.backgroundColor = new Color(.02f, .025f, .035f);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("저장 실패: " + BootstrapScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void RequireStationOpen()
        {
            var station = SceneManager.GetSceneByPath(StationScenePath);
            if (!station.IsValid() || !station.isLoaded) throw new InvalidOperationException("FpsStation 씬을 연 상태에서 실행하세요.");
        }

        /// <summary>
        /// Collects everything the shift needs at runtime into EmergencyArt: navmesh, places, crowd, particle and prop
        /// materials, and the hanging boards over the 2F hall that an earthquake can bring down.
        /// </summary>
        [MenuItem("ChooGuard/Emergency/Build emergency art")]
        public static EmergencyArt BuildArt()
        {
            RequireStationOpen();
            EnsureFolder(MaterialRoot);
            var art = AssetDatabase.LoadAssetAtPath<EmergencyArt>(ArtAssetPath);
            if (art == null) { art = ScriptableObject.CreateInstance<EmergencyArt>(); AssetDatabase.CreateAsset(art, ArtAssetPath); }
            art.WorldNavMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(StationNavigationBuilder.NavMeshPath) ?? throw new FileNotFoundException(StationNavigationBuilder.NavMeshPath);
            art.StationData = AssetDatabase.LoadAssetAtPath<TextAsset>(StationNavigationBuilder.PointsPath) ?? throw new FileNotFoundException(StationNavigationBuilder.PointsPath);
            art.TrainDoors = StationNavigationBuilder.BuildTrainDoors();
            art.Crowd = AssetDatabase.LoadAssetAtPath<CrowdCatalog>(CrowdAssetBuilder.CatalogPath) ?? throw new FileNotFoundException(CrowdAssetBuilder.CatalogPath);
            art.Flame = ParticleMaterial("Flame", ParticleTexture("FlameSoft", 128, false), true);
            art.Smoke = ParticleMaterial("Smoke", ParticleTexture("SmokePuff", 256, true), false);
            // 통제선: 스테인리스 벨트 차단봉(무게 받침·기둥·검은 벨트 통)과 붉은 인쇄 벨트.
            art.CordonPost = LitMaterial("CordonPost", new Color(.80f, .81f, .82f), .82f, 1f);
            art.CordonHead = LitMaterial("CordonHead", new Color(.03f, .03f, .035f), .45f, 0);
            art.CordonTape = BeltMaterial();
            art.Stanchion = StanchionMesh();
            // 출입문에 끼는 물건: 회색 모직 외투 자락(ambientCG Fabric031, CC0)과 검은 가죽(Leather027, CC0).
            art.Coat = PbrMaterial("Coat", "Fabric031_1K", new Color(.33f, .33f, .36f), 4f);
            art.Leather = PbrMaterial("Leather", "Leather027_1K", Color.white, 3f);
            art.CoatFlap = CoatFlapMesh();
            // 실제 모델 소품(Sketchfab CC BY, Objaverse 사본을 OBJ 로 바꾼 것 — 출처는 ThirdParty/Licenses/NOTICE.txt).
            art.SuitcaseModel = ObjaverseModel("Suitcase");
            art.HandbagModel = ObjaverseModel("Handbag");
            art.MedicalBagModel = ObjaverseModel("MedicalBag");
            art.Hanging = CollectHanging();
            // 녹음 소리(Freesound·Kenney CC0, 출처는 ThirdParty/Licenses/NOTICE.txt): 바닥 환경음은 스테레오 2D, 나머지는 3D 모노.
            art.ConcourseBed = StationAudio("amb_concourse_crowd", true);
            art.PlatformBed = StationAudio("amb_platform_korea", true);
            art.OutdoorBed = StationAudio("amb_plaza_city", true);
            art.TrainArrive = StationAudio("train_arrive", false);
            art.TrainDepart = StationAudio("train_depart", false);
            art.DoorOpen = StationAudio("door_open", false);
            art.DoorClose = StationAudio("door_close", false);
            art.Escalator = StationAudio("escalator_loop", false);
            art.Fire = StationAudio("fire_loop", false);
            art.Footsteps = Enumerable.Range(0, 5).Select(i => StationAudio("footstep_concrete_" + i, false)).ToArray();
            AssignResponderArt(art);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log("CG_EMERGENCY_ART hanging=" + art.Hanging.Length + " parts=" + art.Hanging.Sum(h => h.Parts.Length));
            return art;
        }

        /// <summary>
        /// Only the parts of EmergencyArt the responders and announcements need (no station scene required): equipment,
        /// the fire hose material, the vehicle prefabs and the announcement voices.
        /// </summary>
        [MenuItem("ChooGuard/Emergency/Build responder art")]
        public static void BuildResponderArt()
        {
            var art = AssetDatabase.LoadAssetAtPath<EmergencyArt>(ArtAssetPath) ?? throw new FileNotFoundException(ArtAssetPath);
            AssignResponderArt(art);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log("CG_RESPONDER_ART vehicles=" + new[] { art.FireEngine, art.Ambulance, art.PoliceCar, art.SwatVan }.Count(v => v != null) + " voices=" + art.Announcements.Length);
        }

        private static void AssignResponderArt(EmergencyArt art)
        {
            // 출동 장비(Sketchfab CC BY, Objaverse 사본 → blender_convert.py 로 OBJ: 저작자 표시는 ThirdParty/Licenses/NOTICE.txt).
            art.NozzleModel = ObjaverseModel("Nozzle");
            art.CotModel = ObjaverseModel("Cot");
            art.ToolboxModel = ObjaverseModel("Toolbox");
            art.FlashlightModel = ObjaverseModel("Flashlight");
            art.WetFloorSignModel = ObjaverseModel("WetFloorSign");
            art.EodRobotModel = ObjaverseModel("EodRobot");
            art.TrafficConeModel = ObjaverseModel("TrafficCone");
            // 소방 호스: 누런 흰색 방수포(ambientCG Fabric031).
            art.HoseMaterial = PbrMaterial("FireHose", "Fabric031_1K", new Color(.88f, .85f, .76f), 1f);
            // 차량: 소방 펌프차는 원래 달린 회전등을, 구급차·순찰차는 지붕 경광등 막대를 깜빡인다.
            art.FireEngine = VehiclePrefab("FireEngine", false);
            art.Ambulance = VehiclePrefab("Ambulance", true);
            art.PoliceCar = VehiclePrefab("PoliceCar", true);
            art.SwatVan = VehiclePrefab("SwatVan", false);
            // 안내방송 음성: PaLine 순서. MeloTTS-Korean(MIT)으로 미리 만든 WAV(생성기·문장: asset-library/research-public/2026-09-27/production-pass/audio/make_pa.py,
            // 2026-09-29 추가분은 .../2026-09-29/pa-lines/make_pa_lines.py·pa_lines_report.json).
            art.Announcements = Enum.GetNames(typeof(PaLine)).Select(Announcement).ToArray();
        }

        public const string VehicleRoot = ArtRoot + "/Vehicles";

        /// <summary>
        /// A parked emergency vehicle: the converted model (nose +Z), a light bar on the roof when the model has none,
        /// beacon materials renamed Beacon_Red/Beacon_Blue for <see cref="EmergencyVehicle"/>, and a box collider so
        /// nobody walks through it.
        /// </summary>
        private static GameObject VehiclePrefab(string name, bool lightBar)
        {
            EnsureFolder(VehicleRoot);
            var model = ObjaverseModel(name);
            var root = new GameObject(name);
            try
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
                body.name = "차체";
                body.transform.SetParent(root.transform, false);
                var info = JsonUtility.FromJson<ObjaverseInfo>(File.ReadAllText(ObjaverseRoot + "/" + name + "/" + name + ".json"));
                // 펌프차 모델의 회전등(Rotate_feu)은 붉은 경광등으로 쓴다.
                MarkBeacons(body, name, info, source => source == "Rotate_feu" ? "Red" : null);
                var bounds = Bounds(body);
                if (lightBar)
                {
                    var bar = (GameObject)PrefabUtility.InstantiatePrefab(ObjaverseModel("LightBar"));
                    bar.name = "경광등";
                    bar.transform.SetParent(root.transform, false);
                    // 지붕 앞쪽(차 길이의 1/4 지점) 가장 높은 곳에 가로로 얹는다.
                    float z = bounds.center.z + bounds.extents.z * .25f;
                    bar.transform.localPosition = new Vector3(0, RoofHeight(body, z, bounds), z);
                    var barInfo = JsonUtility.FromJson<ObjaverseInfo>(File.ReadAllText(ObjaverseRoot + "/LightBar/LightBar.json"));
                    MarkBeacons(bar, "LightBar", barInfo, source => null, colour => colour.r > .45f && colour.g < .3f && colour.b < .3f ? "Red" : colour.b > .45f && colour.r < .3f ? "Blue" : null);
                }
                var collider = root.AddComponent<BoxCollider>();
                collider.center = bounds.center - root.transform.position;
                collider.size = bounds.size;
                root.AddComponent<EmergencyVehicle>();
                return PrefabUtility.SaveAsPrefabAsset(root, VehicleRoot + "/" + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Swaps the beacon slots of a vehicle part for the shared emissive Beacon_Red / Beacon_Blue materials.</summary>
        private static void MarkBeacons(GameObject part, string name, ObjaverseInfo info, Func<string, string> bySource, Func<Color, string> byColour = null)
        {
            foreach (var renderer in part.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    var entry = info.materials.FirstOrDefault(m => name + "_" + m.name == materials[i].name);
                    if (entry == null) continue;
                    var colour = entry.baseColor != null && entry.baseColor.Length >= 3 ? new Color(entry.baseColor[0], entry.baseColor[1], entry.baseColor[2]) : Color.white;
                    var kind = bySource(entry.source) ?? byColour?.Invoke(colour);
                    if (kind != null) materials[i] = BeaconMaterial(kind);
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Material BeaconMaterial(string kind)
        {
            var colour = kind == "Red" ? new Color(.85f, .06f, .04f) : new Color(.08f, .2f, .9f);
            var material = LitMaterial("Beacon_" + kind, colour, .7f, 0);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        /// <summary>Roof height at <paramref name="z"/> on the centre line: a ray down from above the model.</summary>
        private static float RoofHeight(GameObject body, float z, Bounds bounds)
        {
            var colliders = new List<MeshCollider>();
            foreach (var filter in body.GetComponentsInChildren<MeshFilter>(true))
            {
                var c = filter.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = filter.sharedMesh;
                colliders.Add(c);
            }
            var ray = new Ray(new Vector3(bounds.center.x, bounds.max.y + 1, z), Vector3.down);
            float best = float.NegativeInfinity;
            foreach (var c in colliders)
                if (c.Raycast(ray, out var hit, 5)) best = Mathf.Max(best, hit.point.y);
            foreach (var c in colliders) Object.DestroyImmediate(c);
            return float.IsNegativeInfinity(best) ? bounds.max.y : best;
        }

        public const string StationAudioRoot = "Assets/ChooGuard/ThirdParty/Audio/Station";

        /// <summary>
        /// Loads a station recording with game import settings: beds stay stereo and compressed in memory (long loops),
        /// point sources are forced to mono for 3D panning; short one-shots decompress on load.
        /// </summary>
        private static AudioClip StationAudio(string name, bool bed)
        {
            var path = StationAudioRoot + "/" + name + ".wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter ?? throw new FileNotFoundException(path);
            var settings = importer.defaultSampleSettings;
            var clipLength = AssetDatabase.LoadAssetAtPath<AudioClip>(path).length;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = bed ? .6f : .7f;
            settings.loadType = clipLength > 3f ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !bed;
            importer.loadInBackground = bed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        public const string AnnouncementRoot = "Assets/ChooGuard/Art/Audio/Announcements";

        private static AudioClip Announcement(string line)
        {
            var path = AnnouncementRoot + "/pa_" + line + ".wav";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter ?? throw new FileNotFoundException(path);
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .8f;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static Texture2D ParticleTexture(string name, int size, bool puff)
        {
            var path = MaterialRoot + "/" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float alpha = Mathf.Clamp01(1 - r);
                        alpha = alpha * alpha * (3 - 2 * alpha);
                        if (puff)
                        {
                            // 뭉게뭉게한 연기 덩어리: 가장자리를 잡음으로 흐트린다.
                            float n = Mathf.PerlinNoise(x * 6f / size + 3.1f, y * 6f / size + 7.7f) * .6f + Mathf.PerlinNoise(x * 13f / size + 1.3f, y * 13f / size + 5.2f) * .4f;
                            alpha *= Mathf.Clamp01(.35f + n * .9f);
                        }
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material ParticleMaterial(string name, Texture2D texture, bool additive)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? throw new InvalidOperationException("URP 입자 셰이더가 없습니다.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", additive ? 2 : 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LitMaterial(string name, Color colour, float smoothness, float metallic)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit 셰이더가 없습니다.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Retractable-belt stanchion as a lathed mesh (m, pivot on the floor): a 33 cm domed weighted base, a 63 mm post and
        /// a 78 mm belt head at the height the belt runs (0.86 m), capped in steel. Submesh 0 is stainless steel, 1 the black
        /// belt cassette. Sizes follow common queue-barrier posts (overall 0.93 m).
        /// </summary>
        private static Mesh StanchionMesh()
        {
            // (반지름, 높이, 부분) — 부분 0 스테인리스, 1 검은 벨트 통. 아래에서 위로.
            var profile = new (float r, float y, int part)[]
            {
                (0f, 0f, 0), (.160f, 0f, 0), (.165f, .008f, 0), (.150f, .022f, 0), (.100f, .036f, 0), (.045f, .046f, 0), (.036f, .054f, 0),
                (.0315f, .060f, 0), (.0315f, .830f, 1), (.039f, .833f, 1), (.039f, .915f, 0), (.036f, .925f, 0), (.020f, .932f, 0), (0f, .934f, 0),
            };
            const int sides = 28;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var parts = new[] { new List<int>(), new List<int>() };
            float v = 0;
            for (int i = 0; i + 1 < profile.Length; i++)
            {
                // 띠의 재질은 아래 점이 정한다: 그 점부터 다음 점까지가 한 부분이다.
                var (r0, y0, part) = profile[i];
                var (r1, y1, _) = profile[i + 1];
                // 띠마다 꼭짓점을 따로 두어 모서리를 살리고, 띠 안에서는 매끈하게 돈다.
                var along = new Vector2(r1 - r0, y1 - y0);
                var normal2 = new Vector2(along.y, -along.x).normalized;
                float length = along.magnitude;
                int start = vertices.Count;
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s * Mathf.PI * 2 / sides;
                    var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    var normal = (dir * normal2.x + Vector3.up * normal2.y).normalized;
                    vertices.Add(dir * r0 + Vector3.up * y0);
                    vertices.Add(dir * r1 + Vector3.up * y1);
                    normals.Add(normal);
                    normals.Add(normal);
                    uvs.Add(new Vector2((float)s / sides, v));
                    uvs.Add(new Vector2((float)s / sides, v + length));
                }
                v += length;
                for (int s = 0; s < sides; s++)
                {
                    int a = start + s * 2, b = a + 1, c = a + 2, d = a + 3;
                    parts[part].AddRange(new[] { a, b, d, a, d, c });
                }
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(StanchionPath);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, StanchionPath); }
            mesh.Clear();
            mesh.name = "벨트 차단봉";
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(parts[0], 0);
            mesh.SetTriangles(parts[1], 1);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        public const string StanchionPath = MaterialRoot + "/Stanchion.asset";
        public const string BeltTexturePath = MaterialRoot + "/CordonBelt.png";
        public const string PbrRoot = ArtRoot + "/PBR";
        public const string CoatFlapPath = MaterialRoot + "/CoatFlap.asset";
        public const string ObjaverseRoot = "Assets/ChooGuard/ThirdParty/Models/Objaverse";

        [Serializable] private sealed class ObjaverseInfo { public string name; public ObjaverseMaterial[] materials; }
        [Serializable] private sealed class ObjaverseMaterial { public string name, source, albedo, normal, metallicSmoothness, alphaMode; public float[] baseColor; }

        /// <summary>
        /// A prop converted from an Objaverse GLB (asset-library/.../props/convert_glb.py and .../responder-assets/
        /// blender_convert.py: OBJ in metres, pivot at the bottom centre, real size; PBR maps as PNG with a sidecar JSON).
        /// Each OBJ material is remapped to a URP Lit material built from those maps (<see cref="JsonMaterial"/>). Equipment
        /// models are imported <paramref name="readable"/>: the shift combines their meshes into static batches at run time.
        /// </summary>
        public static GameObject ObjaverseModel(string name, bool readable = false)
        {
            string folder = ObjaverseRoot + "/" + name + "/", objPath = folder + name + ".obj";
            var info = JsonUtility.FromJson<ObjaverseInfo>(File.ReadAllText(folder + name + ".json"));
            var importer = AssetImporter.GetAtPath(objPath) as ModelImporter ?? throw new FileNotFoundException(objPath);
            importer.globalScale = 1;
            importer.useFileScale = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.isReadable = readable;
            foreach (var entry in info.materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.name), JsonMaterial(name, entry.name));
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(objPath) ?? throw new FileNotFoundException(objPath);
        }

        /// <summary>
        /// URP Lit for one material of a converted model's sidecar JSON: base colour or colour map, normal map, metallic in
        /// R and smoothness (1 − glTF roughness) in A; alpha-tested when the JSON says MASK (printed liveries, decals).
        /// </summary>
        public static Material JsonMaterial(string name, string entryName)
        {
            string folder = ObjaverseRoot + "/" + name + "/";
            var info = JsonUtility.FromJson<ObjaverseInfo>(File.ReadAllText(folder + name + ".json"));
            var entry = info.materials.First(m => m.name == entryName);
            var material = LitMaterial(name + "_" + entry.name, entry.baseColor != null && entry.baseColor.Length >= 3 ? new Color(entry.baseColor[0], entry.baseColor[1], entry.baseColor[2]) : Color.white, 1f, 0);
            if (!string.IsNullOrEmpty(entry.albedo)) material.SetTexture("_BaseMap", ImportTexture(folder + entry.albedo, false));
            if (!string.IsNullOrEmpty(entry.normal)) { material.SetTexture("_BumpMap", ImportTexture(folder + entry.normal, true)); material.EnableKeyword("_NORMALMAP"); }
            if (!string.IsNullOrEmpty(entry.metallicSmoothness))
            {
                material.SetTexture("_MetallicGlossMap", ImportTexture(folder + entry.metallicSmoothness, false, linear: true));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Metallic", 1f);
            }
            else material.SetFloat("_Smoothness", .35f);
            bool clip = entry.alphaMode == "MASK";
            material.SetFloat("_AlphaClip", clip ? 1 : 0);
            material.SetFloat("_Cutoff", .5f);
            if (clip) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = clip ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// URP Lit from an ambientCG 1K set under <see cref="PbrRoot"/>: colour, OpenGL normal (Unity's convention) and a
        /// smoothness map packed from roughness (alpha, 1 − roughness) by the receipt step. <paramref name="tiling"/> repeats
        /// the 1 m² set per UV unit.
        /// </summary>
        private static Material PbrMaterial(string name, string set, Color tint, float tiling)
        {
            string folder = PbrRoot + "/" + set + "/", id = set.Replace("_1K", "");
            var colour = ImportTexture(folder + id + "_1K-JPG_Color.jpg", false);
            var normal = ImportTexture(folder + id + "_1K-JPG_NormalGL.jpg", true);
            var smooth = ImportTexture(folder + id + "_1K_MetallicSmoothness.png", false, linear: true);
            var material = LitMaterial(name, tint, 1f, 0);
            material.SetTexture("_BaseMap", colour);
            material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", smooth);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Smoothness", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D ImportTexture(string path, bool normal, bool linear = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new FileNotFoundException(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !linear;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// A coat's hem caught between closing door leaves (m, pivot at the gap on the floor; z out onto the platform): a
        /// 30 × 42 cm panel of cloth whose folds deepen toward its free end, which sags a little. Both faces, UVs in metres.
        /// </summary>
        private static Mesh CoatFlapMesh()
        {
            const int columns = 14, rows = 12;
            const float length = .30f, height = .42f, top = .96f;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int side = 0; side < 2; side++)
                for (int r = 0; r <= rows; r++)
                    for (int c = 0; c <= columns; c++)
                    {
                        float z = -.01f + length * c / columns, t = (float)c / columns, down = (float)r / rows;
                        // 문틈에서 멀수록 주름이 깊고(자유 끝), 아래로 갈수록 조금 처진다.
                        float fold = (.004f + .018f * t) * Mathf.Sin(z * Mathf.PI * 2 / .11f + down * 1.3f);
                        float y = top - height * down - .04f * t * t * down;
                        vertices.Add(new Vector3(fold, y, z));
                        uvs.Add(new Vector2(z, y));
                    }
            int stride = columns + 1, back = (rows + 1) * stride;
            var triangles = new List<int>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    int a = r * stride + c, b = a + 1, d = a + stride, e = d + 1;
                    // 앞면(-x 쪽에서 보이게)과 뒷면(+x)의 감기 방향을 반대로 둔다.
                    triangles.AddRange(new[] { a, b, e, a, e, d });
                    triangles.AddRange(new[] { back + a, back + e, back + b, back + a, back + d, back + e });
                }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CoatFlapPath);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, CoatFlapPath); }
            mesh.Clear();
            mesh.name = "외투 자락";
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>
        /// The printed belt: a red woven strap with "출입금지 · KEEP OUT" in white (texture made by
        /// asset-library/research-public/2026-09-27/production-pass/props/make_belt.py, Noto Sans CJK KR, OFL).
        /// </summary>
        private static Material BeltMaterial()
        {
            var importer = AssetImporter.GetAtPath(BeltTexturePath) as TextureImporter ?? throw new FileNotFoundException(BeltTexturePath);
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            var material = LitMaterial("CordonTape", Color.white, .3f, 0);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BeltTexturePath));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Hanging boards over the walkable 2F hall (1.4 m or wider, 9.2–12.5 m high). Each item gathers the renderers
        /// that make up the board. Static-batched parts keep their mesh asset (a copy falls, the original is hidden);
        /// other parts are moved as they are. Items with a static-batched part lacking a mesh asset are skipped.
        /// </summary>
        private static EmergencyArt.HangingItem[] CollectHanging()
        {
            var navmesh = UnityEngine.AI.NavMesh.AddNavMeshData(AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(StationNavigationBuilder.NavMeshPath));
            try
            {
                var station = SceneManager.GetSceneByPath(StationScenePath);
                var renderers = station.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>()).Where(r => r.enabled).ToList();
                var keywords = new[] { "board", "Board", "Screen", "AdPanel", "Lightbox", "Sign", "sign", "Canopy" };
                var used = new HashSet<MeshRenderer>();
                var items = new List<EmergencyArt.HangingItem>();
                foreach (var main in renderers.OrderByDescending(r => r.bounds.size.x * r.bounds.size.z))
                {
                    var b = main.bounds;
                    // 결합 메시(홀 전체에 흩어진 판을 한 메시로 묶은 것)는 한 장의 판이 아니므로 크기 상한으로 거른다.
                    float span = Mathf.Max(b.size.x, b.size.z);
                    if (used.Contains(main) || b.center.y < 9.2f || b.center.y > 12.5f || span < 1.4f || span > 6.5f || b.size.y < .25f || b.size.y > 2.6f) continue;
                    if (!keywords.Any(k => main.name.Contains(k))) continue;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(new Vector3(b.center.x, 7, b.center.z), out _, 1.2f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    var region = b;
                    region.Expand(.3f);
                    var members = renderers.Where(r => !used.Contains(r) && region.Contains(r.bounds.center) && r.bounds.size.magnitude < b.size.magnitude * 1.2f).ToList();
                    var parts = new List<EmergencyArt.HangingPart>();
                    bool movable = true;
                    foreach (var member in members)
                    {
                        bool batched = GameObjectUtility.GetStaticEditorFlags(member.gameObject).HasFlag(StaticEditorFlags.BatchingStatic);
                        var mesh = member.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                        if (batched && (mesh == null || !AssetDatabase.Contains(mesh))) { movable = false; break; }
                        if (!batched && member.transform.childCount > 0) { movable = false; break; }
                        parts.Add(new EmergencyArt.HangingPart
                        {
                            Path = PathOf(member.transform),
                            Mesh = batched ? mesh : null,
                            Materials = member.sharedMaterials,
                            Position = member.transform.position,
                            Rotation = member.transform.rotation,
                            Scale = member.transform.lossyScale,
                        });
                    }
                    if (!movable || parts.Count == 0) continue;
                    foreach (var member in members) used.Add(member);
                    items.Add(new EmergencyArt.HangingItem { Label = LabelOf(main.name), Centre = b.center, Size = b.size, Parts = parts.ToArray() });
                }
                return items.ToArray();
            }
            finally { UnityEngine.AI.NavMesh.RemoveNavMeshData(navmesh); }
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return "/" + path;
        }

        private static string LabelOf(string name)
        {
            if (name.Contains("Arrival") || name.Contains("Departure") || name.Contains("Screen") || name.Contains("Canopy")) return "열차 안내 전광판";
            if (name.Contains("AdPanel")) return "광고판";
            if (name.Contains("Lightbox") || name.Contains("Fascia")) return "점포 간판";
            if (name.Contains("WallPanel")) return "벽면 안내판";
            return "천장 안내판";
        }

        public static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void RenderTopDown(string path, Rect bounds, float cameraY, float depth, float pixelsPerMetre)
        {
            WithCamera(camera =>
            {
                camera.orthographic = true;
                camera.orthographicSize = bounds.height * .5f;
                camera.aspect = bounds.width / bounds.height;
                camera.nearClipPlane = .02f;
                camera.farClipPlane = depth;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.07f, .08f, .1f, 1);
                camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, cameraY, bounds.center.y), Quaternion.Euler(90, 0, 0));
                Save(camera, path, Mathf.RoundToInt(bounds.width * pixelsPerMetre), Mathf.RoundToInt(bounds.height * pixelsPerMetre));
            });
        }

        private static void RenderPerspective(string path, Vector3 position, Quaternion rotation, float fov, int width, int height)
        {
            WithCamera(camera =>
            {
                var source = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == "FirstPersonCamera");
                if (source != null) camera.CopyFrom(source);
                camera.fieldOfView = fov;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 600;
                camera.transform.SetPositionAndRotation(position, rotation);
                Save(camera, path, width, height);
            });
        }

        private static void WithCamera(Action<Camera> render)
        {
            var go = EditorUtility.CreateGameObjectWithHideFlags("CG capture", HideFlags.HideAndDontSave, typeof(Camera));
            try { render(go.GetComponent<Camera>()); }
            finally { Object.DestroyImmediate(go); }
        }

        private static void Save(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
