using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static class ApplyScoutAssetsToScene
{
    const string AdsDir = "Assets/ChooGuard/Art/StationInterior/Textures/Ads/";
    const string ShopsDir = "Assets/ChooGuard/Art/StationInterior/Textures/Shops/";
    const string PassDir = "Assets/ChooGuard/Art/StationInterior/Textures/Passengers/";
    const string MatDir = "Assets/ChooGuard/Art/StationInterior/Materials/Scout/";

    public static void Main(string[] args)
    {
        AssetDatabase.ImportAsset("Assets/ChooGuard/Art/StationInterior/Textures", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
        Directory.CreateDirectory(Path.GetFullPath(MatDir));

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");

        // 1. 광고판 머티리얼 6종 생성
        var adFiles = new string[] {
            "ad_01_korail_talk.png", "ad_02_visit_busan_beach.png", "ad_03_samjin_amook.png",
            "ad_04_bnk_busanbank.png", "ad_05_ktx_cheongryong.png", "ad_06_busan_global_city.png"
        };
        var adMats = new List<Material>();
        foreach (var af in adFiles)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AdsDir + af);
            string mp = MatDir + "mat_" + Path.GetFileNameWithoutExtension(af) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(unlit); AssetDatabase.CreateAsset(m, mp); }
            m.shader = unlit;
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            adMats.Add(m);
        }

        // 2. 기둥 스크린에 광고 순환 적용
        int adBound = 0;
        var cols = GameObject.Find("부산역 역사 내부/2F · 지상 2층/기둥 격자");
        if (cols != null && adMats.Count > 0)
        {
            int idx = 0;
            foreach (Transform c in cols.transform)
            {
                foreach (Transform s in c)
                {
                    if (s.name.StartsWith("Column_LED_AdScreen"))
                    {
                        var mr = s.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            mr.sharedMaterial = adMats[idx % adMats.Count];
                            idx++;
                            adBound++;
                        }
                    }
                }
            }
        }

        // 3. 점포 내부 Fake Interior 쿼드 배치
        var shopFiles = new (string match, string tex)[] {
            ("어묵", "shop_amook_interior.png"),
            ("환공", "shop_amook_interior.png"),
            ("삼진", "shop_amook_interior.png"),
            ("제과", "shop_bakery_interior.png"),
            ("비엔씨", "shop_bakery_interior.png"),
            ("빵", "shop_bakery_interior.png"),
            ("도넛", "shop_bakery_interior.png"),
            ("크리스피", "shop_bakery_interior.png"),
            ("편의점", "shop_convenience_interior.png"),
            ("스토리웨이", "shop_convenience_interior.png"),
            ("올리브영", "shop_convenience_interior.png"),
            ("카페", "shop_cafe_interior.png"),
            ("파스쿠찌", "shop_cafe_interior.png"),
            ("커피", "shop_cafe_interior.png"),
            ("특산", "shop_souvenir_interior.png"),
            ("별빛", "shop_souvenir_interior.png"),
            ("분식", "shop_snack_interior.png"),
            ("떡볶이", "shop_snack_interior.png")
        };
        var shopMats = new Dictionary<string, Material>();
        foreach (var item in shopFiles)
        {
            if (shopMats.ContainsKey(item.tex)) continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ShopsDir + item.tex);
            string mp = MatDir + "mat_" + Path.GetFileNameWithoutExtension(item.tex) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, mp); }
            m.shader = lit;
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTexture("_EmissionMap", tex); }
            m.SetColor("_BaseColor", Color.white);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.85f) * 1.5f);
            EditorUtility.SetDirty(m);
            shopMats[item.tex] = m;
        }

        int shopsPlaced = 0;
        var finish = GameObject.Find("부산역 역사 내부 · 마감/2F");
        var glzGroup = finish != null ? finish.transform.Find("2F 임대매장(전면유리)") : null;
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        if (glzGroup != null)
        {
            var oldFake = GameObject.Find("부산역 역사 내부 · 매장인테리어");
            if (oldFake != null) UnityEngine.Object.DestroyImmediate(oldFake);
            var fakeRoot = new GameObject("부산역 역사 내부 · 매장인테리어");
            fakeRoot.transform.SetParent(finish.transform, false);

            foreach (Transform g in glzGroup)
            {
                string gname = g.name;
                Material targetMat = shopMats["shop_convenience_interior.png"]; // 기본
                foreach (var s in shopFiles)
                {
                    if (gname.Contains(s.match)) { targetMat = shopMats[s.tex]; break; }
                }

                var quadGo = new GameObject("InteriorBackdrop_" + gname);
                quadGo.transform.SetParent(fakeRoot.transform, false);
                // 유리창 배면 0.25m 안쪽으로 배치
                Vector3 normal = g.rotation * Vector3.forward;
                quadGo.transform.position = g.position - normal * 0.25f;
                quadGo.transform.rotation = g.rotation;
                quadGo.transform.localScale = new Vector3(g.localScale.x, g.localScale.y, 1f);

                var mf = quadGo.AddComponent<MeshFilter>();
                mf.sharedMesh = quad;
                var mr = quadGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = targetMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                shopsPlaced++;
            }
        }

        // 4. 승객 실루엣 빌보드 배치 (2F 보행로 및 벤치)
        var passFiles = new string[] {
            "passenger_01_rolling_luggage.png", "passenger_02_standing_phone.png",
            "passenger_03_bench_seated.png", "passenger_04_walking_commuter.png"
        };
        var passMats = new List<Material>();
        foreach (var pf in passFiles)
        {
            // 투명 텍스처 타입 설정
            string tp = PassDir + pf;
            var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
            if (ti != null && (!ti.alphaIsTransparency || !ti.isReadable))
            {
                ti.alphaIsTransparency = true;
                ti.isReadable = true;
                ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            string mp = MatDir + "mat_" + Path.GetFileNameWithoutExtension(pf) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(unlit); AssetDatabase.CreateAsset(m, mp); }
            m.shader = unlit;
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1); // Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            passMats.Add(m);
        }

        var oldPass = GameObject.Find("부산역 역사 내부 · 승객실루엣");
        if (oldPass != null) UnityEngine.Object.DestroyImmediate(oldPass);
        var passRoot = new GameObject("부산역 역사 내부 · 승객실루엣");
        passRoot.transform.SetParent(GameObject.Find("부산역 역사 내부").transform, false);

        int passPlaced = 0;
        // 2F 보행로 좌표 (u -35~35, v -25~-5)
        var rng = new System.Random(42);
        for (int i = 0; i < 48; i++)
        {
            float u = (float)(rng.NextDouble() * 70.0 - 35.0);
            float v = (float)(rng.NextDouble() * 20.0 - 25.0);
            float th = 16.2f * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(u * Mathf.Sin(th) + v * Mathf.Cos(th), 7.0f + 0.88f, u * Mathf.Cos(th) - v * Mathf.Sin(th));

            var pgo = new GameObject("Passenger_" + (i + 1));
            pgo.transform.SetParent(passRoot.transform, false);
            pgo.transform.position = pos;
            pgo.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
            pgo.transform.localScale = new Vector3(0.85f, 1.75f, 1f);

            var mf = pgo.AddComponent<MeshFilter>();
            mf.sharedMesh = quad;
            var mr = pgo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = passMats[i % passMats.Count];
            mr.shadowCastingMode = ShadowCastingMode.Off;
            passPlaced++;
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("APPLY_SCOUT_ASSETS: Ads=" + adBound + ", ShopBackdrops=" + shopsPlaced + ", Passengers=" + passPlaced);
    }
}
