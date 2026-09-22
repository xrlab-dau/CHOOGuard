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

        [MenuItem("ChooGuard/수직 슬라이스/소화기 월간점검 배치")]
        public static void BuildMenu(){BindMaterials();Build(true);}

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

        public static GameObject Build(bool saveScene)
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!=ScenePath)
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return null;
                scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            }

            var responder=Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[슬라이스] FpsStation 에 FirstPersonResponder 가 없습니다.");return null;}

            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var procedure=AssetDatabase.LoadAssetAtPath<TextAsset>(ProcedurePath);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if(model==null){Debug.LogError("[슬라이스] 소화기 모델 없음 · "+ModelPath);return null;}
            if(procedure==null){Debug.LogError("[슬라이스] 절차 정의 없음 · "+ProcedurePath);return null;}
            if(font==null)Debug.LogWarning("[슬라이스] 한국어 폰트 없음 · "+FontPath+" · 점검표 HUD 는 비활성으로 남습니다.");

            var existing=GameObject.Find(RootName);
            if(existing!=null)Undo.DestroyObjectImmediate(existing);

            var root=new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root,"소화기 슬라이스 배치");

            // 플레이어 정면 1.2m, 높이 1.10m. 3m 상한 안이라 어느 씬 배치에서도 팔 닿는 거리다.
            var player=responder.transform;
            var forward=player.forward;forward.y=0;
            if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;
            forward.Normalize();
            var stand=player.position+forward*1.2f;
            root.transform.position=new Vector3(stand.x,player.position.y,stand.z);
            root.transform.rotation=Quaternion.LookRotation(-forward,Vector3.up);

            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name="소화기 본체";
            instance.transform.SetParent(root.transform,false);
            instance.transform.localPosition=Vector3.zero;

            // 메시 실측 경계에서 관측 지점을 비례로 잡는다. 하드코딩한 치수를 쓰지 않는다.
            var bounds=RendererBounds(instance);
            float height=Mathf.Max(bounds.size.y,.1f);
            float width=Mathf.Max(Mathf.Max(bounds.size.x,bounds.size.z),.05f);
            float mount=1.10f-bounds.center.y+bounds.extents.y*0f;   // 본체 중심을 1.10m 에 둔다
            instance.transform.localPosition=new Vector3(0,mount,0);
            bounds=RendererBounds(instance);

            var inspectable=root.AddComponent<FacilityInspectable>();
            inspectable.SerialNumber="BSN-CONC-FE-003";
            inspectable.Prompt="소화기 점검";
            inspectable.Corroded=true;          // 월드 상태: 부식 있음 → 정답은 '부적합'이다(제23조②1)
            inspectable.ExpiryPassed=false;     // 기한은 남았다 — 기한만 보고 통과시키면 틀린다
            inspectable.AllowFieldRepair=false; // 제23조①1 — 역무원은 수리를 '요구'한다

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

            var tracker=responder.GetComponent<FpsGazeTracker>();
            if(tracker==null)tracker=Undo.AddComponent<FpsGazeTracker>(responder.gameObject);
            tracker.Responder=responder;
            inspectable.Bind(tracker);

            var sessionObject=new GameObject("튜토리얼 세션");
            Undo.RegisterCreatedObjectUndo(sessionObject,"튜토리얼 세션 생성");
            sessionObject.transform.SetParent(root.transform,false);
            var session=sessionObject.AddComponent<TutorialSession>();
            session.Responder=responder;session.GazeTracker=tracker;session.Target=inspectable;
            session.ProcedureAsset=procedure;session.ChecklistVisible=true;
            session.PendingVerdict=InspectionVerdict.UNFIT;   // 부식이 있으므로 이것이 정답

            // 점검표 HUD 를 붙이지 않는다. 조사 결론(2026-09-22): 단계 라벨·거부 사유·완료 피드백은
            // FirstPersonInteractionHud 의 중앙 프롬프트로 이미 흐르므로 별도 패널은 중복이다.
            // 8단계 전모는 작업 중 어디에도 표시하지 않고 판정 단말에서만 열거한다(Viscera Cleanup 선례).
            if(font==null)Debug.LogWarning("[슬라이스] 한국어 폰트 미확인 — 기존 HUD 표시를 점검하세요.");

            session.AuditTerminal=BuildAuditTerminal(root,player,forward,font);

            EditorSceneManager.MarkSceneDirty(scene);
            if(saveScene)EditorSceneManager.SaveScene(scene);
            // 피벗이 아니라 메시 중심 높이를 찍는다 — 피벗은 소화기 밑바닥이라 오해를 부른다.
            Debug.Log("[슬라이스] 배치 완료 · 관측지점 "+points.Count+"개 · 메시 중심 y="+bounds.center.y.ToString("0.00")+"m (피벗 "+instance.transform.position.y.ToString("0.00")+"m) · 절차 "+procedure.name);
            Selection.activeGameObject=root;
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
