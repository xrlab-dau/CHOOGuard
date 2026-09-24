using System.Collections.Generic;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Tutorial;
using ChooGuard.App.Fps.Work;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.EditorTools
{
    // 수직 슬라이스 배치기. 소화기 1개 + 관측 콜라이더 4개 + 절차 세션을 FpsStation 에 심는다.
    //
    // 콜라이더는 전부 non-trigger 여야 한다 — FirstPersonResponder.cs:110 의 레이캐스트가
    // QueryTriggerInteraction.Ignore 라서 트리거 콜라이더는 보이지 않는다.
    public static class FireExtinguisherSliceBuilder
    {
        private const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        private const string ModelPath="Assets/ChooGuard/Art/FireSafety/korean_fire_extinguisher_01_2k.fbx";
        private const string ProcedurePath="Assets/ChooGuard/Art/Procedures/fire-extinguisher-monthly.json";
        private const string FontPath="Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset";
        private const string RootName="튜토리얼 · 소화기 월간점검";
        // 유닛 루트는 고유해야 한다 — TutorialSession.Audit 이 Target.name 을 지적 접두사로,
        // Bind 가 보고 제목으로 쓴다(TutorialSession.cs:157,:75). 이름이 겹치면 보고가 무너진다.
        // 제거 훑기가 RootName 과 이 접두사를 둘 다 본다.
        private const string UnitPrefix="소화기 · ";

        // 기본 메뉴는 역사 전체 12개를 배치한다. 예전에는 이 메뉴가 단일 배치를 불렀고,
        // Build(placements) 가 시작할 때 접두사에 걸리는 기존 유닛을 전부 지우므로
        // 한 번 누르면 12개가 1개로 줄었다(2026-09-24 실제 발생, 씬 복원함).
        [MenuItem("ChooGuard/수직 슬라이스/소화기 월간점검 배치 (역사 12개)")]
        public static void BuildMenu(){BindMaterials();Build(PlacementsV3,true);}

        // 단일 배치는 개발용으로만 남긴다. 누르면 역사 배치가 이것 하나로 대체된다.
        [MenuItem("ChooGuard/수직 슬라이스/개발용 · 플레이어 앞 1개만 배치")]
        public static void BuildSingleMenu()
        {
            if(!EditorUtility.DisplayDialog("소화기 단일 배치",
                "역사에 배치된 소화기를 모두 제거하고 플레이어 앞에 1개만 둡니다. 계속할까요?","배치","취소"))return;
            BindMaterials();Build(true);
        }

        [MenuItem("ChooGuard/수직 슬라이스/소화기 머티리얼 결속")]
        public static void BindMaterialsMenu(){BindMaterials();}

        private const string MaterialDir="Assets/ChooGuard/Art/FireSafety/Materials";
        private const string TextureDir="Assets/ChooGuard/Art/FireSafety/Textures";

        // FBX 임포터가 URP/Lit 머티리얼 3개를 만들지만 텍스처를 하나도 붙이지 못한다 —
        // Poly Haven 의 파일명(_diff_2k / _nor_gl_2k / _arm_2k)이 Unity 자동탐색 규칙과 안 맞는다.
        // 명시적으로 머티리얼 자산을 만들고 ModelImporter 리맵으로 묶어, 이후 어떤 배치에서도 붙게 한다.
        //
        // 주의: Poly Haven 의 arm 맵은 R=AO / G=Roughness / B=Metallic 인데 URP Lit 마스크맵은
        // R=Metallic / G=Occlusion / A=Smoothness 다. 채널 배치가 달라 그대로 꽂으면 틀린다.
        // 이 슬라이스는 시각적으로 지배적인 BaseMap·BumpMap 만 붙이고 금속도·매끄러움은 스칼라로 준다.
        // arm 채널 재배치는 텍스처 가공 단계가 필요하므로 여기서 하지 않는다.
        public static void BindMaterials()
        {
            var importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if(importer==null){Debug.LogError("[슬라이스] 모델 임포터를 찾지 못했습니다 · "+ModelPath);return;}
            if(!AssetDatabase.IsValidFolder(MaterialDir))
                AssetDatabase.CreateFolder("Assets/ChooGuard/Art/FireSafety","Materials");

            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null){Debug.LogError("[슬라이스] URP/Lit 셰이더를 찾지 못했습니다.");return;}

            // slot: FBX 의 머티리얼 이름 접미사 / metallic / smoothness / 투명 여부
            var slots=new[]
            {
                new{ slot="body",  metallic=.35f, smooth=.45f, transparent=false },
                new{ slot="paper", metallic=0f,   smooth=.25f, transparent=false },
                new{ slot="glass", metallic=.10f, smooth=.90f, transparent=true  },
            };
            int bound=0,missing=0;
            foreach(var spec in slots)
            {
                string name="korean_fire_extinguisher_01_"+spec.slot;
                string path=MaterialDir+"/"+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
                if(material.shader!=shader)material.shader=shader;

                var albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir+"/"+name+"_diff_2k.jpg");
                var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(TextureDir+"/"+name+"_nor_gl_2k.jpg");
                if(albedo==null||normal==null){missing++;Debug.LogWarning("[슬라이스] 텍스처 누락 · "+name+" diff="+(albedo!=null)+" nor="+(normal!=null));}
                if(albedo!=null)material.SetTexture("_BaseMap",albedo);
                if(normal!=null)
                {
                    MarkAsNormalMap(TextureDir+"/"+name+"_nor_gl_2k.jpg");
                    material.SetTexture("_BumpMap",normal);
                    material.EnableKeyword("_NORMALMAP");
                }
                material.SetFloat("_Metallic",spec.metallic);
                material.SetFloat("_Smoothness",spec.smooth);
                if(spec.transparent)
                {
                    material.SetFloat("_Surface",1);                 // Transparent
                    material.SetFloat("_Blend",0);                   // Alpha
                    material.SetColor("_BaseColor",new Color(1,1,1,.45f));
                    material.renderQueue=3000;
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                EditorUtility.SetDirty(material);
                // FBX 안의 머티리얼 이름으로 리맵한다. 이름이 정확히 같아야 묶인다.
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),material);
                bound++;
            }
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();
            Debug.Log("[슬라이스] 머티리얼 결속 "+bound+"개"+(missing>0?" · 텍스처 누락 "+missing+"개":"")+" · "+MaterialDir);
        }

        // 노멀맵은 임포트 타입이 NormalMap 이어야 URP 가 올바르게 해석한다.
        private static void MarkAsNormalMap(string texturePath)
        {
            var textureImporter=AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if(textureImporter==null||textureImporter.textureType==TextureImporterType.NormalMap)return;
            textureImporter.textureType=TextureImporterType.NormalMap;
            textureImporter.SaveAndReimport();
        }

        // 배치 하나의 사양. 좌표·식별자·월드 상태를 바깥에서 주입받는다.
        // NFTC 101 보행거리·구획 산정 결과(extinguisher-placement-v3.json)를 그대로 받기 위한 것이다.
        public struct Placement
        {
            public Vector3 Position;      // 바닥 접지점. 본체는 여기서 1.10m 위에 온다.
            public Quaternion Rotation;   // 로컬 +Z 가 플레이어를 향해야 판독면이 보인다.
            public string Serial;
            public bool Corroded;         // 월드 상태. PendingVerdict 와 짝이다 — 따로 놀면 거짓 오판정이 난다.
            public bool ExpiryPassed;
            public bool TutorialTarget;   // 절차 세션이 물릴 대상. 정확히 하나여야 한다.
        }

        // 역사 2층 대합실 12개. 좌표 출처는
        // .planning/2026-09-22-station-interior-build/extinguisher-placement-v3.json
        // (schema chooguard.extinguisher-placement.v2, computedAt 2026-09-22).
        // 산정 근거는 NFTC 101 — 보행거리 20m 이내(소형), 33제곱미터 이상 구획 거실마다, 바닥 1.5m 이하.
        // 방법은 벽으로만 막은 구획 분해 + 벽·단차를 막은 BFS 보행거리 + 그리디 커버(jev-poi-placement-004).
        // 그 실행의 uncoveredAfterSolve 는 8셀이며, 법정 구획 5개(404·106·785·225·45㎡)를 덮는다.
        //
        // 주의 — 여기서 데이터와 다르게 처리하는 것 두 가지를 기록한다.
        // ① 자료의 mountY 는 바닥+1.2m 인데 이 생성기는 BuildUnit 에서 바닥+1.10m 로 세운다.
        //    둘 다 NFTC 101 의 1.5m 이하 안이라 규정 위반은 아니지만 자료와 10cm 다르다.
        // ② 고유번호는 자료에 없다. 아래 값은 작성한 것이지 출처가 있는 것이 아니다.
        //    월드 상태(부식·기한)도 마찬가지로 튜토리얼 시나리오 조건이며 실측이 아니다.
        private static readonly Placement[] PlacementsV3=
        {
            Unit( 0,-10f,6.70f,-73f,  1,0, false,false,false),
            Unit( 1, -6f,7.00f,-39f, -1,0, false,false,false),
            Unit( 2, 14f,7.00f,-49f,  0,-1,false,false,false),
            Unit( 3, 48f,7.02f,-47f,  0,-1,false,false,false),
            Unit( 4, 60f,6.70f,-65f,  0,-1,false,false,false),
            Unit( 5, 48f,7.00f,-49f,  0, 1,false,false,false),
            Unit( 6, -8f,6.70f,-53f,  1,0, false,false,false),
            Unit( 7,-16f,6.70f,-89f,  1,0, false,false,false),
            Unit( 8, 16f,7.00f,-37f,  0, 1,false,false,false),
            // 플레이어 시작 지점(26, 7.05, -47)에서 약 6.3m, 같은 층이라 튜토리얼 대상으로 둔다.
            Unit( 9, 24f,7.00f,-53f,  0,-1,true, true, false),
            Unit(10, 40f,7.00f,-59f,  0,-1,false,false,false),
            Unit(11, 44f,7.20f,-35f,  0, 1,false,false,false),
        };

        // wallNormalDir 은 벽에서 실내 쪽을 가리킨다. 판독면(로컬 +Z)이 그쪽을 봐야 플레이어에게 보인다.
        private static Placement Unit(int index,float x,float y,float z,int nx,int nz,
                                      bool tutorialTarget,bool corroded,bool expiryPassed)
            =>new Placement
            {
                Position=new Vector3(x,y,z),
                Rotation=Quaternion.LookRotation(new Vector3(nx,0,nz),Vector3.up),
                Serial="BSN-CONC-FE-"+(index+1).ToString("000"),
                Corroded=corroded,
                ExpiryPassed=expiryPassed,
                TutorialTarget=tutorialTarget,
            };

        // 기존 단일 배치 경로. 플레이어 정면 1.2m 에 하나를 두던 동작을 그대로 보존한다.
        public static GameObject Build(bool saveScene)
        {
            if(!Prepare(out var scene,out var responder,out _,out _,out _))return null;
            var player=responder.transform;
            var forward=player.forward;forward.y=0;
            if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;
            forward.Normalize();
            var stand=player.position+forward*1.2f;
            var one=new Placement
            {
                Position=new Vector3(stand.x,player.position.y,stand.z),
                Rotation=Quaternion.LookRotation(-forward,Vector3.up),
                // 역사 배치(BSN-CONC-FE-001~012)와 겹치면 안 된다. 유닛 이름이 곧 이월 기록의
                // facilityId 라서, 같은 이름이면 서로 다른 소화기가 한 기록을 공유하게 된다.
                Serial="DEV-FE-001",
                Corroded=true,          // 정답은 '부적합'이다(제23조②1)
                ExpiryPassed=false,     // 기한만 보고 통과시키면 틀린다
                TutorialTarget=true,
            };
            var built=Build(new[]{one},saveScene);
            return built!=null&&built.Length>0?built[0]:null;
        }

        // 여러 배치. 세션과 판정 단말은 유닛마다 만들지 않고 한 번만 만들어 바깥에 둔다
        // (Jev 005 session_and_terminal=one_each_hoisted 0.87). 유닛 루트를 지워도 살아남아야 하기 때문이다.
        public static GameObject[] Build(IReadOnlyList<Placement> placements,bool saveScene)
        {
            if(placements==null||placements.Count==0){Debug.LogError("[슬라이스] 배치 목록이 비었습니다.");return null;}
            if(!Prepare(out var scene,out var responder,out var model,out var procedure,out var font))return null;

            int targets=0;
            foreach(var p in placements)if(p.TutorialTarget)targets++;
            if(targets!=1){Debug.LogError("[슬라이스] 튜토리얼 대상은 정확히 1개여야 합니다 · 현재 "+targets);return null;}

            // 고유번호가 곧 유닛 이름이고(root.name=UnitPrefix+Serial), 유닛 이름이 곧 이월 기록의
            // facilityId 다. 겹치면 보고 제목도 이월 기록도 서로 섞인다 — 배치 전에 막는다.
            var seenSerials=new HashSet<string>();
            foreach(var p in placements)
                if(!seenSerials.Add(p.Serial??""))
                {Debug.LogError("[슬라이스] 고유번호가 겹칩니다 · "+p.Serial);return null;}

            // 근접 경고. RefreshInteraction 은 3m 상한 안에서 가장 가까운 콜라이더만 고르므로
            // 두 유닛이 그 안에 들어오면 어느 것을 겨눈 것인지 모호해진다(Jev 005 assert_and_report 0.95).
            float minGap=float.PositiveInfinity;string gapPair="";
            for(int i=0;i<placements.Count;i++)for(int j=i+1;j<placements.Count;j++)
            {
                float g=Vector3.Distance(placements[i].Position,placements[j].Position);
                if(g<minGap){minGap=g;gapPair=placements[i].Serial+" ↔ "+placements[j].Serial;}
            }
            if(minGap<3f)Debug.LogWarning("[슬라이스] 유닛 간 최소 간격 "+minGap.ToString("0.00")+"m · "+gapPair+" · 상호작용 상한 3m 안이라 조준이 모호할 수 있습니다.");

            // 기존 산출물 제거. GameObject.Find 는 하나만, 그것도 활성 객체만 돌려주므로
            // 유닛이 여럿이면 앞선 것들이 남는다(Jev 005 iterate_roots_by_prefix 0.87).
            int removed=0;
            foreach(var r in scene.GetRootGameObjects())
                if(r!=null&&(r.name.StartsWith(RootName)||r.name.StartsWith(UnitPrefix)))
                {Undo.DestroyObjectImmediate(r);removed++;}

            var tracker=responder.GetComponent<FpsGazeTracker>();
            if(tracker==null)tracker=Undo.AddComponent<FpsGazeTracker>(responder.gameObject);
            tracker.Responder=responder;

            var roots=new List<GameObject>();
            var inspectables=new List<FacilityInspectable>();
            FacilityInspectable tutorialTarget=null;
            foreach(var p in placements)
            {
                var built=BuildUnit(p,model,tracker,out var inspectable);
                roots.Add(built);
                inspectables.Add(inspectable);
                if(p.TutorialTarget)tutorialTarget=inspectable;
            }

            // 세션·단말은 어느 유닛에도 속하지 않는 자체 루트에 둔다.
            var host=new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(host,"튜토리얼 세션 호스트");
            var playerT=responder.transform;
            var fwd=playerT.forward;fwd.y=0;
            if(fwd.sqrMagnitude<.0001f)fwd=Vector3.forward;
            fwd.Normalize();

            var sessionObject=new GameObject("튜토리얼 세션");
            Undo.RegisterCreatedObjectUndo(sessionObject,"튜토리얼 세션 생성");
            sessionObject.transform.SetParent(host.transform,false);
            var session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;session.Target=tutorialTarget;
            session.ProcedureAsset=procedure;session.ChecklistVisible=true;
            session.PendingVerdict=tutorialTarget!=null&&tutorialTarget.Corroded?InspectionVerdict.UNFIT:InspectionVerdict.FIT;

            // 기술자 인계는 설비마다 하나다 — 기술자는 특정 소화기로 간다.
            // 붙이지 않으면 Dispatch 가 null 이라 요구 발행이 플래그로만 남는다.
            // 인계 오브젝트는 유닛 루트가 아니라 호스트 아래 둔다. 유닛을 지워도 세션이 살아남는
            // one_each_hoisted 규율과 같은 이유로, 인계도 유닛 수명에 묶지 않는다.
            session.Units=new List<TutorialSession.UnitBinding>();
            foreach(var inspectable in inspectables)
            {
                var dispatchObject=new GameObject("기술자 인계 · "+inspectable.SerialNumber);
                Undo.RegisterCreatedObjectUndo(dispatchObject,"기술자 인계 생성");
                dispatchObject.transform.SetParent(host.transform,false);
                var dispatch=dispatchObject.AddComponent<TechnicianDispatch>();
                dispatch.Target=inspectable;
                session.Units.Add(new TutorialSession.UnitBinding{Facility=inspectable,Dispatch=dispatch});
                if(inspectable==tutorialTarget)session.Dispatch=dispatch;
            }

            // 점검표 HUD 를 붙이지 않는다. 조사 결론(2026-09-22): 단계 라벨·거부 사유·완료 피드백은
            // FirstPersonInteractionHud 의 중앙 프롬프트로 이미 흐르므로 별도 패널은 중복이다.
            // 8단계 전모는 작업 중 어디에도 표시하지 않고 판정 단말에서만 열거한다(Viscera Cleanup 선례).
            if(font==null)Debug.LogWarning("[슬라이스] 한국어 폰트 미확인 — 기존 HUD 표시를 점검하세요.");

            session.AuditTerminal=BuildAuditTerminal(host,playerT,fwd,font);

            EditorSceneManager.MarkSceneDirty(scene);
            if(saveScene)EditorSceneManager.SaveScene(scene);
            Debug.Log("[슬라이스] 배치 "+roots.Count+"개 · 기존 제거 "+removed+"개 · 최소 간격 "+minGap.ToString("0.00")+"m · 절차 "+procedure.name);
            if(roots.Count>0)Selection.activeGameObject=roots[0];
            return roots.ToArray();
        }

        private static bool Prepare(out UnityEngine.SceneManagement.Scene scene,out FirstPersonResponder responder,
                                    out GameObject model,out TextAsset procedure,out TMP_FontAsset font)
        {
            responder=null;model=null;procedure=null;font=null;
            scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath)
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return false;
                scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            }
            responder=Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[슬라이스] FpsStation 에 FirstPersonResponder 가 없습니다.");return false;}
            model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            procedure=AssetDatabase.LoadAssetAtPath<TextAsset>(ProcedurePath);
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if(model==null){Debug.LogError("[슬라이스] 소화기 모델 없음 · "+ModelPath);return false;}
            if(procedure==null){Debug.LogError("[슬라이스] 절차 정의 없음 · "+ProcedurePath);return false;}
            if(font==null)Debug.LogWarning("[슬라이스] 한국어 폰트 없음 · "+FontPath+" · 점검표 HUD 는 비활성으로 남습니다.");
            return true;
        }

        // 유닛 하나. 관측 지점 4개의 id 는 TutorialSession.cs:92~95 와 문자열로 맞물려 있으므로 건드리지 않는다.
        private static GameObject BuildUnit(Placement p,GameObject model,FpsGazeTracker tracker,out FacilityInspectable inspectable)
        {
            var root=new GameObject(UnitPrefix+p.Serial);
            Undo.RegisterCreatedObjectUndo(root,"소화기 배치");
            root.transform.SetPositionAndRotation(p.Position,p.Rotation);

            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name="소화기 본체";
            instance.transform.SetParent(root.transform,false);
            instance.transform.localPosition=Vector3.zero;

            // 메시 실측 경계에서 관측 지점을 비례로 잡는다. 하드코딩한 치수를 쓰지 않는다.
            var bounds=RendererBounds(instance);
            float height=Mathf.Max(bounds.size.y,.1f);
            float width=Mathf.Max(Mathf.Max(bounds.size.x,bounds.size.z),.05f);
            // RendererBounds 는 renderer.bounds, 즉 월드 공간이다. 1.10f-bounds.center.y 를 그대로
            // 로컬 오프셋으로 쓰면 최종 월드 높이가 층과 무관하게 1.10m 로 고정된다. 플레이어가
            // 도로(y≈0.145)에 있을 때만 우연히 맞았고, 대합실(y≈7.05)로 옮기자 바닥 아래로 내려갔다.
            // 루트 높이를 더해 "루트 바닥에서 1.10m" 라는 원래 의도대로 만든다. 1.5m 이하 규정 안이다.
            float mount=root.transform.position.y+1.10f-bounds.center.y;
            instance.transform.localPosition=new Vector3(0,mount,0);
            bounds=RendererBounds(instance);

            inspectable=root.AddComponent<FacilityInspectable>();
            inspectable.SerialNumber=p.Serial;
            inspectable.Prompt="소화기 점검";
            inspectable.Corroded=p.Corroded;          // 월드 상태. 세션의 PendingVerdict 와 짝이다(제23조②1)
            inspectable.ExpiryPassed=p.ExpiryPassed;  // 기한만 보고 통과시키면 틀린다
            inspectable.AllowFieldRepair=false;       // 제23조①1 — 역무원은 수리를 '요구'한다

            // 배치 규율 두 개 모두 실측으로 잡았다.
            // ① 레이캐스트는 가장 가까운 솔리드 콜라이더만 고른다(FirstPersonResponder.cs:111).
            //    따라서 판독 지점은 본체 박스 **밖**으로 띄워야 한다. 안에 두면 본체가 먼저 맞는다.
            // ② 루트는 LookRotation(-플레이어전방) 으로 세우므로 로컬 **+Z 가 플레이어 쪽**이다.
            //    -Z 로 두면 판독면이 본체 뒤에 숨는다(RaycastAll 로 본체 1.16m → 고유번호 1.58m 확인).
            // ③ 판독 지점을 위아래로 **쌓지 않는다.** 눈높이 1.6m 에서 1.2m 거리의 1.1m 대상을 보면
            //    약 30° 내려보기가 되고, 그 각도에서는 앞면에 돌출한 판이 그 아래 전부를 가린다
            //    (실측: 아래쪽 고유번호가 위쪽 제원표에 영구히 가려졌다).
            //    좌우로 벌려 나란히 두면 서로 가리지 않고, 그 사이 틈으로 본체가 드러난다.
            float bodyHalfZ=width*.475f;
            float faceZ=bodyHalfZ+.03f;
            float side=width*.23f;                         // 좌우 분리 거리
            float plateWidth=width*.32f;                   // 두 판 사이에 틈이 남는 폭
            var points=new List<FacilityInspectable.InspectionPoint>
            {
                Point(root,"body","본체 외관",bounds,new Vector3(0,.42f,0),new Vector3(width*.95f,height*.55f,width*.95f),.7f),
                Point(root,"spec-plate","제원표",bounds,new Vector3(-side,.55f,faceZ),new Vector3(plateWidth,height*.20f,.02f),.8f),
                Point(root,"serial","고유번호",bounds,new Vector3(side,.55f,faceZ),new Vector3(plateWidth,height*.14f,.02f),.5f),
                Point(root,"gauge","지시압력계",bounds,new Vector3(0,.88f,faceZ*.7f),new Vector3(width*.4f,height*.12f,.04f),.6f),
            };
            inspectable.Points=points.ToArray();
            inspectable.Bind(tracker);
            return root;
        }

        // 판정 단말. 소화기 옆이 아니라 **몇 걸음 떨어진 곳**에 둔다 — 감사는 작업 자리에서 하는 것이 아니고,
        // 걸어가는 짧은 이동 자체가 '점검을 마치고 보고하러 간다'는 절차의 구획이 된다.
        // 절차 진행 중에도 존재하며 거부만 한다(Jev 006 present_but_refuses 0.74). 갑자기 나타나면 디제틱 파탄이고
        // 플레이어가 단말의 위치를 미리 학습할 수 없다.
        private static AuditTerminal BuildAuditTerminal(GameObject root,Transform player,Vector3 forward,TMP_FontAsset font)
        {
            var right=Vector3.Cross(Vector3.up,forward).normalized;
            var terminal=GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminal.name="판정 단말";
            terminal.transform.SetParent(root.transform,true);
            terminal.transform.position=player.position+right*2.2f+forward*.6f+Vector3.up*1.32f;
            // 화면이 플레이어 쪽을 보게 세운다. 단말은 벽에 붙은 물건이므로 빌보드로 따라 돌지 않는다.
            terminal.transform.rotation=Quaternion.LookRotation(-right,Vector3.up);
            terminal.transform.localScale=new Vector3(.62f,.46f,.07f);
            var renderer=terminal.GetComponent<MeshRenderer>();
            if(renderer!=null&&renderer.sharedMaterial!=null)renderer.sharedMaterial.color=new Color(.10f,.12f,.14f);

            var component=terminal.AddComponent<AuditTerminal>();
            component.Prompt="감사 결과 확인";
            component.UnavailableMessage="지금은 감사 결과가 없습니다";

            var view=terminal.AddComponent<AuditTerminalView>();
            view.KoreanFont=font;
            component.View=view;
            if(font==null)Debug.LogWarning("[슬라이스] 한국어 폰트가 없어 감사 화면이 뜨지 않습니다 · "+FontPath);
            Undo.RegisterCreatedObjectUndo(terminal,"판정 단말 배치");
            return component;
        }

        private static Bounds RendererBounds(GameObject instance)
        {
            var renderers=instance.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)return new Bounds(instance.transform.position,Vector3.one*.5f);
            var bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        // 자식 콜라이더를 **루트 로컬 공간**에 만든다. 오프셋을 월드 축에 적용하면 회전이 무시돼
        // 제원표가 엉뚱한 면에 붙는다(실측으로 잡은 버그). 로컬 +Z 가 플레이어 쪽이다.
        // 반드시 non-trigger 다 — 레이캐스트가 QueryTriggerInteraction.Ignore 이기 때문이다.
        private static FacilityInspectable.InspectionPoint Point(GameObject root,string id,string label,Bounds bounds,Vector3 offsetFromBase,Vector3 size,float dwell)
        {
            var go=new GameObject("관측 · "+label);
            go.transform.SetParent(root.transform,false);
            // 월드 경계의 높이를 루트 로컬 y 로 환산한다. 루트는 Y 축만 회전하므로 높이는 보존된다.
            float worldY=bounds.min.y+bounds.size.y*offsetFromBase.y;
            float localY=worldY-root.transform.position.y;
            go.transform.localPosition=new Vector3(offsetFromBase.x,localY,offsetFromBase.z);
            var box=go.AddComponent<BoxCollider>();
            box.isTrigger=false;                       // QueryTriggerInteraction.Ignore 때문에 필수
            box.size=new Vector3(Mathf.Max(size.x,.02f),Mathf.Max(size.y,.02f),Mathf.Max(size.z,.02f));
            return new FacilityInspectable.InspectionPoint{Id=id,Label=label,Surface=box,RequiredDwellSeconds=dwell};
        }
    }
}
