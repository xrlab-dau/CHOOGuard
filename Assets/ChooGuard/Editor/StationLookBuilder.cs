using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Production look of the station twin (JEV visual-approach-001): post-processing and SSAO in the renderer,
    /// adaptive probe volumes instead of legacy probes, sun and sky for a late-September 14:00 Busan afternoon, a
    /// Neutral-tonemapped global volume, GI static flags on the twin geometry (the KTX set, the scripted escalator and
    /// the fire extinguishers move and stay dynamic) and box-projected reflection probes per space. Everything it
    /// creates lives under <see cref="RootName"/> in FpsStation; no twin geometry is moved or cut.
    /// </summary>
    public static class StationLookBuilder
    {
        public const string LookRoot = "Assets/ChooGuard/Settings/Look";
        public const string RootName = "환경 조명·후처리";
        public const string SkyTexturePath = "Assets/ChooGuard/ThirdParty/Textures/Sky/kloofendal_48d_partly_cloudy_puresky_2k_sunclamp24.hdr";
        private const string UrpAssetPath = "Assets/ChooGuard/Settings/BootstrapURP.asset";
        private const string RendererPath = "Assets/ChooGuard/Settings/BootstrapRenderer.asset";
        private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
        private static readonly string[] StaticRoots = { "FPSWorld/공식 자료 부산역 역사", "FPSWorld/공식 자료 부산역 주변 공간", "맞이방 · 원본 정합", "실내 트윈 마감", "1층 · 원본 정합" };

        // 부산(35.1°N) 9월 말 14:00 KST: 태양 고도 46°, 방위 219°(남서). 빛은 북동쪽(39°)으로 비스듬히 내려간다.
        public static readonly Quaternion SunRotation = Quaternion.Euler(46f, 39.3f, 0f);
        // HDRI 속 해(u .5947, 고도 48°)를 태양 방위 219°에 맞추는 하늘 회전.
        public const float SkyRotation = 95.2f;

        /// <summary>Box-projected reflection probe per walkable space: capture point and the space's box (world).</summary>
        private static readonly (string Name, Vector3 Capture, Vector3 Min, Vector3 Max, int Resolution, int Importance)[] Spaces =
        {
            ("2층 맞이방", new Vector3(65, 8.8f, 15), new Vector3(18, 6.9f, -30), new Vector3(112, 26, 64), 256, 1),
            ("3층", new Vector3(48, 13.9f, -12), new Vector3(0, 12.1f, -55), new Vector3(115, 17, 60), 128, 3),
            ("2층 본관", new Vector3(-20, 8.8f, -5), new Vector3(-62, 6.9f, -100), new Vector3(18, 11, 90), 256, 2),
            ("1층", new Vector3(-25, 1.8f, 0), new Vector3(-70, -.2f, -95), new Vector3(16, 5.5f, 90), 256, 2),
            ("2층 북측 데크", new Vector3(70, 8.8f, 70), new Vector3(25, 6.9f, 60), new Vector3(145, 16, 105), 128, 2),
            ("2층 남측 게이트", new Vector3(48, 8.9f, -40), new Vector3(-6, 6.9f, -70), new Vector3(95, 11, -30), 128, 3),
            ("2층 동측 출구", new Vector3(100, 8.8f, 0), new Vector3(88, 6.9f, -25), new Vector3(112, 11, 30), 128, 3),
            ("하늘광장", new Vector3(130, 9, -20), new Vector3(112, 4, -95), new Vector3(235, 30, 55), 128, 1),
            ("역 광장", new Vector3(-100, 2.5f, 20), new Vector3(-140, -2, -150), new Vector3(-58, 30, 140), 128, 1),
            ("승강장 남쪽 끝", new Vector3(2, 1.8f, -140), new Vector3(-40, -1.5f, -175), new Vector3(60, 8, -105), 128, 2),
            ("승강장 남쪽", new Vector3(28, 1.8f, -70), new Vector3(-20, -1.5f, -105), new Vector3(90, 8, -35), 128, 2),
            ("승강장 가운데", new Vector3(54, 1.8f, 0), new Vector3(0, -1.5f, -35), new Vector3(120, 6.8f, 35), 128, 2),
            ("승강장 북쪽", new Vector3(80, 1.8f, 70), new Vector3(20, -1.5f, 35), new Vector3(150, 6.8f, 120), 128, 2),
        };

        private static void RequireStation()
        {
            var station = SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            if (!station.IsValid() || !station.isLoaded) throw new InvalidOperationException("FpsStation 씬을 연 상태에서 실행하세요.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("재생을 종료한 뒤 실행하세요.");
        }

        [MenuItem("ChooGuard/Look/Apply look (pipeline, statics, sky, volume, probes)")]
        public static void Apply()
        {
            RequireStation();
            Directory.CreateDirectory(LookRoot);
            var log = new List<string>();
            ApplyPipeline(log);
            MarkStatic(log);
            var station = SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            SceneManager.SetActiveScene(station);
            ApplyEnvironment(station, log);
            EditorSceneManager.MarkSceneDirty(station);
            EditorSceneManager.SaveScene(station);
            AssetDatabase.SaveAssets();
            Debug.Log("CG_LOOK_APPLIED\n" + string.Join("\n", log));
        }

        /// <summary>
        /// The pipeline part of <see cref="Apply"/> alone (URP asset, renderer, the player and graphics settings it needs).
        /// Unlike Apply it leaves the environment root and its baked reflection probes alone, so no re-bake is needed.
        /// </summary>
        [MenuItem("ChooGuard/Look/Apply pipeline (Deferred+, static batching)")]
        public static void ApplyPipelineOnly()
        {
            var log = new List<string>();
            ApplyPipeline(log);
            AssetDatabase.SaveAssets();
            Debug.Log("CG_LOOK_PIPELINE\n" + string.Join("\n", log));
        }

        /// <summary>
        /// Materials seen most from the standard player viewpoints (JEV visual-approach-001 "visible_first"): unlit or
        /// flat shells get lit PBR finishes at metric scale (kit meshes have 1 UV unit per metre). Textures are ambientCG
        /// CC0 sets under ThirdParty/Textures. Renderer reassignments only swap materials; no geometry changes.
        /// </summary>
        [MenuItem("ChooGuard/Look/Apply materials")]
        public static void ApplyMaterials()
        {
            RequireStation();
            Directory.CreateDirectory(LookRoot + "/Materials");
            var log = new List<string>();
            var lit = Shader.Find("Universal Render Pipeline/Lit");

            // 맞이방 남쪽 낮은 천장(패널 격자 형상)과 점포 천장: Unlit 단색이라 빛을 받지 않아 평평한 띠로 보였다.
            var panel = LookMaterial("Look_CeilingPanelMetal", lit, m =>
            {
                m.SetColor("_BaseColor", new Color(.83f, .84f, .83f));
                m.SetFloat("_Metallic", .3f);
                m.SetFloat("_Smoothness", .55f);
            });
            var tile = LookMaterial("Look_CeilingTile", lit, m =>
            {
                Textured(m, "OfficeCeiling001", 1 / 3.6f); // 600 mm 격자 6칸 = 3.6 m
                m.SetColor("_BaseColor", new Color(.95f, .95f, .94f));
                m.SetFloat("_Smoothness", .12f);
            });
            // 3층 바닥판의 아랫면(맞이방에서 올려다보는 면): 밝은 도장 콘크리트.
            var soffit = LookMaterial("Look_SlabSoffit", lit, m =>
            {
                Textured(m, "Concrete034", .25f);
                m.SetColor("_BaseColor", new Color(.86f, .86f, .85f));
                m.SetFloat("_Smoothness", .18f);
            });
            // 3층 가장자리 아래 석고 띠(맞이방 남쪽 눈높이 위를 덮는 넓은 면): 1.2 m 금속 천장 패널.
            var soffitPanels = LookMaterial("Look_SoffitPanels", lit, m =>
            {
                var normalPath = "Assets/ChooGuard/ThirdParty/Textures/MetalPlates004/MetalPlates004_2K-JPG_NormalGL.jpg";
                var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                if (importer != null && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                m.EnableKeyword("_NORMALMAP");
                m.SetTextureScale("_BaseMap", new Vector2(1 / 9.6f, 1 / 9.6f)); // 8칸 × 1.2 m
                m.SetColor("_BaseColor", new Color(.86f, .87f, .86f));
                m.SetFloat("_Metallic", .2f);
                m.SetFloat("_Smoothness", .45f);
            });
            int swapped = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var m = materials[i];
                    if (m == null) continue;
                    Material replacement = null;
                    if (m.name == "Majibang_2F_CeilingPanel") replacement = renderer.name.Contains("CeilingPanels") ? panel : tile;
                    else if (m.name == "Canopy_Dark" && renderer.name.StartsWith("Kit_Floor")) replacement = soffit;
                    else if (m.name == "wall_painted_gypsum_fine" && renderer.name == "Box_Kit_Clad_wall_painted_gypsum_fine") replacement = soffitPanels;
                    if (replacement == null) continue;
                    materials[i] = replacement;
                    changed = true;
                }
                if (!changed) continue;
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                swapped++;
            }
            log.Add("renderers re-materialled: " + swapped);

            // 공식 모델의 기본색 0.03 검은 면(유리창·짙은 외장 판): 반사 코팅 유리. 뒤가 어두운 곳에서도 앞 공간이 비치게
            // 반사율(F0)을 0.04 가 아니라 약 0.25 로 둔다(금속도 0.7 × 청회색).
            Edit("Assets/ChooGuard/Art/OfficialBusanStation/Materials/source-1038eb5849196e4cf4d6.mat", m =>
            {
                m.SetColor("_BaseColor", new Color(.3f, .36f, .42f));
                m.SetFloat("_Metallic", .7f);
                m.SetFloat("_Smoothness", .92f);
            }, log);
            // 빛나는 석고 벽: 간접광이 들어오니 자체 발광을 줄인다(0.3 → 0.06).
            Edit("Assets/ChooGuard/Art/StationInterior/Kit/Materials/Kit_GypsumLit.mat", m => m.SetColor("_EmissionColor", new Color(.06f, .06f, .058f)), log);
            // 600 mm 광물 천장(OfficeCeiling003): 전체가 0.34 로 빛나던 것을 조명 기구 칸만 빛나게(색 지도의 청록 조명판에서 뽑은 마스크).
            Edit("Assets/ChooGuard/Art/StationInterior/Kit/Materials/Kit_Ceiling600.mat", m =>
            {
                m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ChooGuard/Art/StationInterior/PBR/OfficeCeiling003_2K/OfficeCeiling003_2K-Derived_Emission.png"));
                m.SetColor("_EmissionColor", new Color(1.6f, 1.6f, 1.55f));
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }, log);
            // 짙은 금속 프레임·접합부: 조금 밝게, 금속 반사.
            Edit("Assets/ChooGuard/Art/StationInterior/VideoSecondFloor/Canopy_Dark.mat", m =>
            {
                m.SetColor("_BaseColor", new Color(.16f, .18f, .19f));
                m.SetFloat("_Metallic", .55f);
                m.SetFloat("_Smoothness", .5f);
            }, log);
            // 석고 도장 벽·천장: 흰 도장(0.55 회색에서).
            Edit("Assets/ChooGuard/Art/StationInterior/Materials/Lane/wall_painted_gypsum_fine.mat", m => m.SetColor("_BaseColor", new Color(.8f, .8f, .79f)), log);

            // 공식 모델 재질: 양면 렌더인데 뒷면 법선이 뒤집히지 않아 뒤에서 본 벽이 검게 나왔다 → StationLit(뒷면 법선 뒤집기).
            // UV 가 없는 무늬 없는 면은 월드 상자 투영으로 Concrete034 를 입히고, 평균 알베도로 나눠 원래 밝기를 지킨다.
            var stationLit = Shader.Find("ChooGuard/StationLit");
            if (stationLit == null) throw new InvalidOperationException("ChooGuard/StationLit 셰이더가 없습니다.");
            var concrete = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ChooGuard/ThirdParty/Textures/Concrete034/Concrete034_2K-JPG_Color.jpg");
            var concreteNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ChooGuard/ThirdParty/Textures/Concrete034/Concrete034_2K-JPG_NormalGL.jpg");
            const float concreteMeanLinear = .482f; // Concrete034 색 지도의 선형 평균(2026-09-27 측정: RGB 0.482)
            int switched = 0, boxed = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { "Assets/ChooGuard/Art/OfficialBusanStation/Materials" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || (m.shader.name != "Universal Render Pipeline/Lit" && m.shader != stationLit) || m.GetFloat("_Cull") != 0) continue;
                if (m.shader != stationLit) { m.shader = stationLit; switched++; }
                bool untextured = m.GetTexture("_BaseMap") == null || m.GetTexture("_BaseMap") == concrete;
                bool opaque = m.GetFloat("_Surface") == 0;
                var colour = m.GetColor("_BaseColor");
                if (untextured && opaque && colour.maxColorComponent > .12f && m.GetFloat("_Metallic") < .5f)
                {
                    if (m.GetTexture("_BaseMap") != concrete)
                    {
                        var linear = colour.linear;
                        m.SetColor("_BaseColor", new Color(Mathf.Min(1, linear.r / concreteMeanLinear), Mathf.Min(1, linear.g / concreteMeanLinear), Mathf.Min(1, linear.b / concreteMeanLinear), colour.a).gamma);
                    }
                    m.SetTexture("_BaseMap", concrete);
                    m.SetTextureScale("_BaseMap", new Vector2(.25f, .25f));
                    m.SetTexture("_BumpMap", concreteNormal);
                    m.SetFloat("_BumpScale", .6f);
                    m.EnableKeyword("_NORMALMAP");
                    m.SetFloat("_BoxMap", 1);
                    m.EnableKeyword("_BOXMAP");
                    boxed++;
                }
                EditorUtility.SetDirty(m);
            }
            log.Add("official materials: " + switched + " to StationLit, " + boxed + " untextured box-mapped with Concrete034");

            var station = SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            EditorSceneManager.MarkSceneDirty(station);
            EditorSceneManager.SaveScene(station);
            AssetDatabase.SaveAssets();
            Debug.Log("CG_LOOK_MATERIALS\n" + string.Join("\n", log));
        }

        private static Material LookMaterial(string name, Shader shader, Action<Material> configure)
        {
            var path = LookRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Textured(Material material, string set, float perMetre)
        {
            var folder = "Assets/ChooGuard/ThirdParty/Textures/" + set + "/" + set + "_2K-JPG_";
            var normalPath = folder + "NormalGL.jpg";
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (importer != null && importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Color.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.EnableKeyword("_NORMALMAP");
            material.SetTextureScale("_BaseMap", new Vector2(perMetre, perMetre));
        }

        private static void Edit(string path, Action<Material> configure, List<string> log)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException("재질이 없습니다: " + path);
            configure(material);
            EditorUtility.SetDirty(material);
            log.Add("material: " + material.name);
        }

        /// <summary>Starts the lighting bake (probe volumes and reflection probes). Poll <see cref="Lightmapping.isRunning"/>.</summary>
        [MenuItem("ChooGuard/Look/Bake lighting (probe volumes, reflection probes)")]
        public static void Bake()
        {
            RequireStation();
            var station = SceneManager.GetSceneByPath(EmergencySceneBuilder.StationScenePath);
            SceneManager.SetActiveScene(station);
            var set = BakingSet(station);
            ProbeReferenceVolume.instance.SetActiveBakingSet(set);
            if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("조명 굽기를 시작하지 못했습니다(이미 굽는 중일 수 있습니다).");
            Debug.Log("CG_LOOK_BAKE_STARTED");
        }

        private static void ApplyPipeline(List<string> log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            var so = new SerializedObject(asset);
            so.FindProperty("m_LightProbeSystem").enumValueIndex = 1; // Adaptive Probe Volumes
            so.FindProperty("m_SoftShadowsSupported").boolValue = true;
            // 성능(2026-09-27, 같은 시드·같은 빌드 비교): 캐스케이드 3→2 로 그림자 캐스터 4,600→3,500, 삼각형 1,040만→850만,
            // GPU p95 34.8→30.1 ms, 프레임 p95 33.3→30.9 ms. 화면(표준 시점·가까운 그림자)은 차이가 보이지 않았다. 거리를 50 m 로
            // 줄이면 조금 더 빠르지만(p95 30.1) 맞이방 먼 쪽 지붕 아래 바닥이 그림자를 잃어 밝아지므로 80 m 는 그대로 둔다.
            so.FindProperty("m_ShadowCascadeCount").intValue = 2;
            so.FindProperty("m_ShadowDistance").floatValue = 80f;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 성능(2026-09-27, 맞이방 좌석 시점 프로파일): 렌더 스레드가 병목이었다 — 메인 패스 그리기 10.9 ms, 그림자 그리기 6.2 ms,
            // 조명마다 스텐실 2.4 ms. 메인 스레드는 물체마다 반사 프로브·조명을 고르는 데 3.5 ms 를 썼다.
            // GPU Resident Drawer 는 이 역에서 오히려 느렸다: 정적 배칭을 꺼야 해서 그리기가 한 프레임 9,000–12,000 번이 되고 렌더
            // 스레드 p95 가 21–36 ms 로 늘었다(트윈은 거의 모두 서로 다른 메시라 인스턴싱 이득이 없다). 정적 배칭을 쓰고 GRD 는 끈다.
            so.Update();
            so.FindProperty("m_GPUResidentDrawerMode").intValue = 0; // Disabled
            so.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            // GRD 를 쓰지 않으므로 DOTS 인스턴싱 변형은 빌드에서 뺀다(기본값). 공개 API 는 읽기 전용이라 설정 자산의 값을 쓴다.
            var graphics = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            graphics.FindProperty("m_BrgStripping").intValue = (int)BatchRendererGroupStrippingMode.KeepIfEntitiesGraphics;
            graphics.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.StandaloneOSX, true);
            EditorUtility.SetDirty(asset);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var rso = new SerializedObject(renderer);
            // Deferred+ 는 조명·반사 프로브를 클러스터로 한 번에 처리한다(조명마다 스텐실 패스, 물체마다 프로브·조명 고르기가 없다).
            renderer.renderingMode = RenderingMode.DeferredPlus;
            rso.Update();
            rso.FindProperty("postProcessData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            rso.ApplyModifiedPropertiesWithoutUndo();
            var ssao = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (ssao == null)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);
                rso.Update();
                var features = rso.FindProperty("m_RendererFeatures");
                var map = rso.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }
            // 역 규모(기둥·좌석·계단 모서리)의 접촉 그늘. 반경은 월드 미터, 강도는 삼색 주변광이 아니라 프로브 간접광 위에서 맞춘다.
            var fso = new SerializedObject(ssao);
            fso.FindProperty("m_Settings.Intensity").floatValue = 1.2f;
            fso.FindProperty("m_Settings.Radius").floatValue = .45f;
            fso.FindProperty("m_Settings.DirectLightingStrength").floatValue = .15f;
            fso.FindProperty("m_Settings.Falloff").floatValue = 60f;
            fso.FindProperty("m_Settings.Downsample").boolValue = true;
            fso.FindProperty("m_Settings.AfterOpaque").boolValue = false;
            fso.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            log.Add("pipeline: APV, soft shadows, 2 cascades 80 m, PostProcessData, SSAO r=.45 i=1.2, Deferred+, static batching (GPU Resident Drawer off)");
        }

        private static void MarkStatic(List<string> log)
        {
            int marked = 0, skipped = 0;
            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (var path in StaticRoots)
            {
                var root = GameObject.Find(path);
                if (root == null) throw new InvalidOperationException("정적 루트를 찾지 못했습니다: " + path);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // 유리처럼 비치는 면은 굽기에서 빛을 막지 않게 둔다.
                    if (renderer.sharedMaterials.Any(m => m != null && m.renderQueue >= (int)RenderQueue.Transparent)) { skipped++; continue; }
                    var go = renderer.gameObject;
                    GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | flags);
                    renderer.receiveGI = ReceiveGI.LightProbes;
                    marked++;
                }
            }
            log.Add("static: " + marked + " renderers contribute GI (receive from probes), " + skipped + " transparent skipped");
        }

        private static void ApplyEnvironment(Scene station, List<string> log)
        {
            var root = station.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, station);

            // 조명 설정: 간접광만 굽는다(라이트맵 없음, 프로브 볼륨). 혼합 조명은 직접광 실시간 + 간접광 굽기.
            var lightingPath = LookRoot + "/StationLighting.lighting";
            var lighting = AssetDatabase.LoadAssetAtPath<LightingSettings>(lightingPath);
            if (lighting == null) { lighting = new LightingSettings { name = "StationLighting" }; AssetDatabase.CreateAsset(lighting, lightingPath); }
            lighting.bakedGI = true;
            lighting.realtimeGI = false;
            lighting.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            lighting.mixedBakeMode = MixedLightingMode.IndirectOnly;
            lighting.directSampleCount = 32;
            lighting.indirectSampleCount = 256;
            lighting.environmentSampleCount = 256;
            lighting.maxBounces = 3;
            lighting.lightmapResolution = 4;
            lighting.ao = false;
            EditorUtility.SetDirty(lighting);
            Lightmapping.lightingSettings = lighting;

            // 하늘: Poly Haven CC0 HDRI(해 원반은 휘도 24로 눌러 실시간 태양과 겹치지 않게 함).
            var skyTexture = AssetDatabase.LoadAssetAtPath<Texture>(SkyTexturePath);
            if (skyTexture == null) throw new InvalidOperationException("하늘 HDRI 가 없습니다: " + SkyTexturePath);
            var skyPath = LookRoot + "/StationSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetTexture("_MainTex", skyTexture);
            sky.SetFloat("_Rotation", SkyRotation);
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Mapping", 1); // latitude-longitude
            sky.SetFloat("_ImageType", 0); // 360°
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .0011f;
            RenderSettings.fogColor = new Color(.68f, .74f, .8f);

            var sun = RenderSettings.sun != null ? RenderSettings.sun : UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun == null) throw new InvalidOperationException("태양(방향성 조명)을 찾지 못했습니다.");
            sun.transform.rotation = SunRotation;
            sun.useColorTemperature = true;
            sun.colorTemperature = 5600f;
            sun.color = Color.white;
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            RenderSettings.sun = sun;
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(sun.transform);

            // 후처리: Neutral 톤매핑, 실내 노출, 약한 색 보정·블룸·주변 어둡게.
            var profilePath = LookRoot + "/StationLook.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            Override<Tonemapping>(profile, t => { t.mode.Override(TonemappingMode.Neutral); });
            Override<ColorAdjustments>(profile, c => { c.postExposure.Override(.1f); c.contrast.Override(8f); c.saturation.Override(2f); });
            Override<Bloom>(profile, b => { b.threshold.Override(1.1f); b.intensity.Override(.25f); b.scatter.Override(.65f); });
            Override<Vignette>(profile, v => { v.intensity.Override(.16f); v.smoothness.Override(.4f); });
            // APV 표본을 보는 쪽·법선 쪽으로 밀어 얇은 벽 너머(불 꺼진 공간)의 어둠이 새어 들지 않게 한다.
            Override<ProbeVolumesOptions>(profile, p => { p.normalBias.Override(.25f); p.viewBias.Override(.35f); });
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("후처리 볼륨").AddComponent<Volume>();
            volume.transform.SetParent(root.transform, false);
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // 간접광 프로브 볼륨: 걸어 다니는 전 구역(역 광장~하늘광장, 승강장 남북 끝, 1층~지붕).
            var probeVolume = new GameObject("간접광 프로브 볼륨").AddComponent<ProbeVolume>();
            probeVolume.transform.SetParent(root.transform, false);
            probeVolume.transform.position = new Vector3(47.5f, 11f, -10f);
            probeVolume.mode = ProbeVolume.Mode.Local;
            probeVolume.size = new Vector3(375f, 28f, 330f);
            var set = BakingSet(station);
            ProbeReferenceVolume.instance.SetActiveBakingSet(set);

            // 공간별 반사 프로브(상자 투영). 작은 공간이 큰 공간보다 우선한다.
            var probes = new GameObject("반사 프로브");
            probes.transform.SetParent(root.transform, false);
            foreach (var space in Spaces)
            {
                var probe = new GameObject(space.Name).AddComponent<ReflectionProbe>();
                probe.transform.SetParent(probes.transform, false);
                probe.transform.position = space.Capture;
                probe.mode = ReflectionProbeMode.Baked;
                probe.boxProjection = true;
                probe.center = (space.Min + space.Max) * .5f - space.Capture;
                probe.size = space.Max - space.Min;
                probe.resolution = space.Resolution;
                probe.hdr = true;
                probe.importance = space.Importance;
                probe.blendDistance = 2f;
                probe.nearClipPlane = .3f;
                probe.farClipPlane = 600f;
            }
            log.Add("environment: sky " + Path.GetFileName(SkyTexturePath) + " rot " + SkyRotation + ", sun " + SunRotation.eulerAngles + ", volume Neutral, probe volume 375x28x330, " + Spaces.Length + " reflection probes");
        }

        private static ProbeVolumeBakingSet BakingSet(Scene station)
        {
            var path = LookRoot + "/StationProbes.asset";
            var set = AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>();
                set.name = "StationProbes";
                // 1.5 m 간격이면 기둥·좌석 사이 간접광을 따라가고, 역 전체(약 12만 m² × 3개 층)를 16 GB 에서 굽는다.
                set.minDistanceBetweenProbes = 1.5f;
                set.simplificationLevels = 3;
                AssetDatabase.CreateAsset(set, path);
            }
            set.TryAddScene(AssetDatabase.AssetPathToGUID(station.path));
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            return set;
        }

        private static void Override<T>(VolumeProfile profile, Action<T> configure) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(false);
                component.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            configure(component);
            EditorUtility.SetDirty(component);
        }
    }
}
