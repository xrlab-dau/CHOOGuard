#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CHOOGuard.UX.NativePreview.Editor
{
    /// <summary>
    /// Creates an isolated uGUI/TMP design-preview scene and UI prefab.
    /// Creates no operations backend, approves nothing, installs no packages,
    /// changes no input/pipeline settings, and does not overwrite an existing scene.
    /// </summary>
    public sealed class NativeUXSceneBuilder : EditorWindow
    {
        private TMP_FontAsset koreanFont;
        private const string OutputRoot = "Assets/CHOOGuardUXNativeV2/Generated";
        private static readonly Color Background = new Color32(17, 23, 31, 255);
        private static readonly Color Surface = new Color32(29, 39, 50, 246);
        private static readonly Color Raised = new Color32(42, 57, 70, 255);
        private static readonly Color Text = new Color32(237, 242, 247, 255);
        private static readonly Color Muted = new Color32(181, 196, 208, 255);
        private static readonly Color Accent = new Color32(52, 113, 151, 255);
        private static readonly Color Warning = new Color32(245, 196, 113, 255);
        private static TMP_FontAsset font;
        private static NativePreviewRouter router;
        private static readonly List<NativePreviewRouter.Surface> screens = new List<NativePreviewRouter.Surface>();

        [MenuItem("Tools/CHOOGuard/UX/Create Native Preview")]
        public static void Open() { GetWindow<NativeUXSceneBuilder>("CHOOGuard Native UX"); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity native uGUI / TMP layout preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Source preview only. Requires uGUI + TextMeshPro. Assign a project-owned Korean font. This creates a new isolated scene; it does not connect an operations engine.", MessageType.Info);
            koreanFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Korean TMP Font", koreanFont, typeof(TMP_FontAsset), false);
            using (new EditorGUI.DisabledScope(koreanFont == null || EditorApplication.isPlaying))
                if (GUILayout.Button("Create new native preview scene + UI prefab")) Build(koreanFont);
        }

        public static void Build(TMP_FontAsset selectedFont)
        {
            if (selectedFont == null) throw new ArgumentNullException(nameof(selectedFont));
            if (!selectedFont.HasCharacter('운') || !selectedFont.HasCharacter('영'))
            {
                EditorUtility.DisplayDialog("Korean glyphs required", "Create or assign a TMP font asset containing Korean glyphs before generating. Font files are not supplied in this package.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string path = OutputRoot + "/" + suffix;
            EnsureFolder(path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            font = selectedFont;
            screens.Clear();

            var cameraGO = new GameObject("WorldCamera", typeof(Camera));
            var camera = cameraGO.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
            camera.orthographicSize = 20f;
            camera.transform.position = new Vector3(25, 32, -28);
            camera.transform.LookAt(Vector3.zero);
            var lightGO = new GameObject("PreviewLight", typeof(Light));
            lightGO.GetComponent<Light>().type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(55, -30, 0);
            CreateGraybox(path);

            var eventGO = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventGO.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            eventGO.AddComponent<StandaloneInputModule>();
#endif
            var ui = new GameObject("CHOOGuard_NativeUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(NativePreviewRouter));
            ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ui.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            router = ui.GetComponent<NativePreviewRouter>();

            BuildMenus(ui.transform);
            BuildHUD(ui.transform);
            BuildOverlays(ui.transform);
            router.surfaces = screens.ToArray();
            router.Show("S01");
            EditorUtility.SetDirty(router);
            PrefabUtility.SaveAsPrefabAsset(ui, path + "/NativeUI_Preview.prefab");
            EditorSceneManager.SaveScene(scene, path + "/NativeUX_Preview.unity");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = ui;
            Debug.Log("Created native preview assets: " + path + ". Unity runtime, Korean IME, layout scaling, and core integration still require verification.");
        }

        private static void BuildMenus(Transform parent)
        {
            var s01 = Screen(parent, "S01", false, true);
            var box = Card(s01, "Launcher", new Vector2(.30f,.15f), new Vector2(.70f,.85f));
            Heading(box, "CHOOGuard", 48);
            Paragraph(box, "철도 비상대응 운영 실험실", Text, 28);
            Paragraph(box, "UNITY NATIVE UX PREVIEW\n레이아웃·화면 연결 예시 / 실제 현장·기관 승인·물리 계산 아님", Warning);
            Route(box, "현장 묶음 확인", "S02");
            Method(box, "교육 · 튜토리얼", router.OpenTutorial);
            Method(box, "일반 · 운영 실험", router.OpenOperations);
            Route(box, "조작·접근성 설정", "S12");

            var s02 = Screen(parent, "S02", false, true);
            var c02 = Card(s02, "SiteDetails", new Vector2(.20f,.13f), new Vector2(.80f,.88f));
            Heading(c02, "현장 묶음 확인");
            Paragraph(c02, "부산 철도 공간 — 설계용 예시", Text, 28);
            Paragraph(c02, "맵·규칙·모델·기관 검수는 각각 별도 자격입니다. 이 프리뷰에는 원본 자료나 검증된 시뮬레이션이 연결돼 있지 않습니다.", Muted);
            Paragraph(c02, "공간: SYNTHETIC_FIXTURE\n기관 규칙: NOT_CONNECTED\n물리 모델: NOT_CONNECTED\n기관 승인: NOT_EVALUATED", Warning);
            Route(c02, "상황 설정으로", "S03");
            Route(c02, "처음으로", "S01");

            var s03 = Screen(parent, "S03", false, true);
            var c03 = Card(s03, "Setup", new Vector2(.20f,.12f), new Vector2(.80f,.89f));
            Heading(c03, "운영 실험 준비");
            Paragraph(c03, "선택한 현장·규칙·자원 묶음과 모델 버전을 확인합니다. 모드는 힌트와 상황 조건만 바꾸며 물리·권한 규칙을 바꾸지 않습니다.", Muted);
            Paragraph(c03, "교육 / 일반 두 모드 유지\n초기 조건 생성: 미연동\n원인·자원·정보·권한은 실제 코어에 연결할 항목", Warning);
            Route(c03, "운영 HUD 구조 보기", "S05");
            Method(c03, "튜토리얼 안내 보기", router.OpenTutorial);
            Route(c03, "현장 확인으로", "S02");
        }

        private static void BuildHUD(Transform parent)
        {
            var hud = Screen(parent, "S05", false, false);
            var top = Panel(hud, "TopStatus", new Vector2(0,.925f), Vector2.one, Raised);
            var topRow = Row(top, 12);
            var brand = Label(topRow, "CHOOGuard", 26, Text); Fixed(brand.gameObject, 170, 42);
            router.modeLabel = Label(topRow, "일반 · RANDOM OPERATIONS", 20, Muted); Flexible(router.modeLabel.gameObject);
            Route(topRow, "상황", "S03", true);
            Intent(topRow, "실험 재개 요청", "RequestResume", true);
            Intent(topRow, "다음 판단 지점", "RequestNextDecision", true);
            Route(topRow, "분기", "S08", true);
            Route(topRow, "비교", "S09", true);
            Route(topRow, "대본", "S10", true);
            Route(topRow, "설정", "S12", true);

            var left = Card(hud, "AgencyRoster", new Vector2(.01f,.30f), new Vector2(.18f,.905f));
            Heading(left, "기관 · 팀", 26);
            Paragraph(left, "공간 선택과 같은 EntityId로 연결할 목록", Muted, 19);
            Route(left, "철도 현장팀 A  ·  선택", "S06");
            Route(left, "소방 지원팀 B  ·  선택", "S06");
            Route(left, "시설 지원팀 C  ·  선택", "S06");
            Intent(left, "기관 수신 정보 보기", "RequestAgencyKnowledge");
            Paragraph(left, "합성 이름 / 실제 편성·권한 아님", Warning, 18);

            var right = Card(hud, "Inspector", new Vector2(.80f,.30f), new Vector2(.99f,.905f));
            Heading(right, "선택 업무", 26);
            Paragraph(right, "보고·인계 상태 확인", Text, 23);
            Paragraph(right, "이유: 코어 연결 전\n영향: 수행 가능성 계산 없음\n근거: 원문 미결속\n조건: 유효한 ViewModel 수신", Warning, 20);
            Route(right, "근거·대기 관계", "S06");
            Route(right, "명령 미리보기", "S07");
            Paragraph(right, "버튼은 요청 경로를 보여줄 뿐 완료·승인 상태를 만들지 않습니다.", Muted, 18);

            var dock = Card(hud, "SelectionDock", new Vector2(.20f,.02f), new Vector2(.78f,.22f));
            Heading(dock, "선택 · 명령 · 인계", 24);
            var commandRow = Row(dock, 0); Fixed(commandRow.gameObject, -1, 48);
            Route(commandRow, "업무 요청", "S07", true);
            Intent(commandRow, "취소 요청", "RequestCancelAssignment", true);
            Intent(commandRow, "대상 위치 보기", "RequestFocusSelection", true);
            router.statusLabel = Paragraph(dock, "UI 레이아웃 프리뷰 · 실제 운영 상태 없음", Warning, 19);

            var minimap = Card(hud, "MinimapPanel", new Vector2(.01f,.02f), new Vector2(.18f,.28f));
            Heading(minimap, "현장 개요", 23);
            Paragraph(minimap, "RenderTexture 연결 지점\n현재 MinimapCamera 미연결\n가짜 지도 이미지로 대체하지 않음", Muted, 18);
            var mini = new GameObject("MinimapRenderTexture", typeof(RectTransform), typeof(RawImage));
            mini.transform.SetParent(minimap, false);
            mini.GetComponent<RawImage>().color = new Color32(44, 65, 77, 255);
            mini.GetComponent<RawImage>().raycastTarget = false;
            Flexible(mini);

            var log = Card(hud, "EventRail", new Vector2(.80f,.02f), new Vector2(.99f,.28f));
            Heading(log, "사건 · 업무 기록", 23);
            Paragraph(log, "실제 이벤트 없음\n타임라인은 읽기 전용 투영\n애니메이션 종료 ≠ 업무 완료", Muted, 19);
            Route(log, "전체 타임라인", "S09");

            var hint = Panel(hud, "WorldHint", new Vector2(.22f,.77f), new Vector2(.77f,.87f), new Color(0,0,0,.55f));
            var hintText = Label(hint, "이 공간은 Unity Camera가 직접 렌더링합니다.\n현재는 합성 기본 도형이며, 지도 선택·카메라 조작은 아직 연결하지 않았습니다.", 22, Text);
            Stretch(hintText.rectTransform, 14);
            hint.GetComponent<Image>().raycastTarget = false;
        }

        private static void BuildOverlays(Transform parent)
        {
            Overlay(parent, "S04", "튜토리얼 · 판단 안내", "목표 → 관련 객체 → 조건·근거 → 직접 조작 → 결과 확인\n\n현재는 UX 안내 레이어이며 실제 훈련 대본이 아닙니다. 안내를 닫아도 선택한 현장과 실행 맥락을 유지합니다.", "명령 구조 살펴보기", "S07", false);
            Overlay(parent, "S06", "업무·근거 인스펙터", "업무 단계 / 권한 / 기관별 정보 / 예약 자원 / 이동 / 안전 / 증거를 분리합니다.\n\n원문 링크와 이벤트 ID는 코어에서 전달받아 표시합니다. 본 프리뷰는 가짜 매뉴얼 번호나 완료 기록을 생성하지 않습니다.", "명령 미리보기", "S07", false);
            Overlay(parent, "S07", "명령 미리보기", "입력: 선택 팀·업무·대상·현재 상태 버전\n검사: 소속 권한·복수 차단 이유·부분 실행 여부\n출력: CommandIntent\n\n현재 코어 미연결: 이 화면에서 요청을 승인하거나 업무를 완료할 수 없습니다.", "요청 의도 표시", "@PreviewCommand", true);
            Overlay(parent, "S08", "운영안 분기", "원본 A를 보존합니다.\n필수 상태: 업무·예약·메시지·기관 정보·물리 이력\n\nCheckpointDescriptor가 없으므로 정확 분기는 사용할 수 없습니다. 분기 버튼의 UI만 만든 것을 B안 생성 완료라고 표시하지 않습니다.", "분기 가능 여부 요청", "@RequestCheckpointCapabilities", true);
            Overlay(parent, "S09", "운영안 비교 · 타임라인", "기준 맵·규칙·물리모델·외부 입력·평가 범위가 일치할 때만 비교합니다.\n\n실제 실행 결과가 없으므로 가상의 개선율·피해 감소·최적안을 보여주지 않습니다.\n분기와 대본 편집은 같은 실행 맥락을 참조해야 합니다.", "대본 검토 화면", "S10", false);
            var script = Overlay(parent, "S10", "대본·근거 검토", "문서 편집 예시입니다. 내용을 수정해도 원본 실행 로그는 바뀌지 않습니다. 운영 의미를 바꾼 변경에는 새로운 검토가 필요합니다.", "내보내기 자격 확인", "S11", false);
            var fieldGO = new GameObject("KoreanScriptInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            fieldGO.transform.SetParent(script, false);
            fieldGO.GetComponent<Image>().color = Background;
            Fixed(fieldGO, -1, 160);
            var viewport = new GameObject("TextViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(fieldGO.transform, false); Stretch(viewport.GetComponent<RectTransform>(), 10);
            var inputText = Label(viewport.transform, "", 22, Text); Stretch(inputText.rectTransform, 0);
            var placeholder = Label(viewport.transform, "검토용 메모를 입력하세요. 저장·AI 생성은 미연결입니다.", 22, Muted); Stretch(placeholder.rectTransform, 0);
            var field = fieldGO.GetComponent<TMP_InputField>();
            field.textViewport = viewport.GetComponent<RectTransform>();
            field.textComponent = inputText;
            field.placeholder = placeholder;
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            UnityEventTools.AddStringPersistentListener(field.onEndEdit, router.Intent, "DraftTextEditedPreview");

            Overlay(parent, "S11", "내보내기 · 자격 확인", "초안 / 모델 내 실행 / 기관별 검토 / 지정 용도 승인 상태를 구분합니다.\n\n승인 증거와 고객 양식이 없으므로 승인본 출력은 불가합니다. 이 프리뷰는 실제 파일을 저장하거나 외부 서비스에 전송하지 않습니다.", "출력 조건 조회 요청", "@RequestExportEligibility", true);
            Overlay(parent, "S12", "설정 · 입력·접근성", "설계 목표: 키 재배정, 한국어 입력, 글자 확대, 고대비, 움직임 최소화, 목록 대체 조작\n\n이 프리뷰에는 글자 확대·키 재배정·보조기술 연결이 구현돼 있지 않습니다. 실제 Unity Game View와 Player 빌드에서 별도 검증합니다.\n\nEsc: 현재 레이어 닫기. 입력 중에는 입력 종료를 먼저 처리합니다.", "설정 연결 요청", "@RequestAccessibilitySettings", true);
        }

        private static RectTransform Overlay(Transform parent, string id, string title, string body, string action, string destination, bool modal)
        {
            var s = Screen(parent, id, true, false);
            var dim = s.gameObject.AddComponent<Image>();
            dim.color = new Color(0,0,0, modal ? .65f : .35f);
            dim.raycastTarget = true;
            var box = Card(s, id + "_Window", new Vector2(.21f,.12f), new Vector2(.79f,.88f));
            Heading(box, title, 32);
            Paragraph(box, body, Text, 23);
            if (destination.StartsWith("@")) Intent(box, action, destination.Substring(1));
            else Route(box, action, destination);
            Method(box, "닫기 · 이전 맥락 유지", router.CloseOverlay);
            return box;
        }

        private static RectTransform Screen(Transform parent, string id, bool overlay, bool solid)
        {
            var go = new GameObject(id, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); Stretch(rect, 0);
            if (solid) { var image = go.AddComponent<Image>(); image.color = Background; image.raycastTarget = true; }
            screens.Add(new NativePreviewRouter.Surface { id=id, root=go, overlay=overlay });
            return rect;
        }

        private static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin=min; rect.anchorMax=max; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
            go.GetComponent<Image>().color = color;
            return rect;
        }
        private static RectTransform Card(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect=Panel(parent,name,min,max,Surface);
            var group=rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding=new RectOffset(20,20,18,18); group.spacing=12;
            group.childControlWidth=true; group.childControlHeight=true;
            group.childForceExpandWidth=true; group.childForceExpandHeight=false;
            return rect;
        }
        private static RectTransform Row(Transform parent, int padding)
        {
            var go=new GameObject("Row",typeof(RectTransform),typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent,false);
            var group=go.GetComponent<HorizontalLayoutGroup>();
            group.padding=new RectOffset(padding,padding,padding,padding); group.spacing=10;
            group.childControlWidth=true; group.childControlHeight=true;
            group.childForceExpandWidth=false; group.childForceExpandHeight=true;
            var rect=go.GetComponent<RectTransform>(); Stretch(rect,0);
            return rect;
        }
        private static TMP_Text Label(Transform parent, string value, int size, Color color)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));
            go.transform.SetParent(parent,false);
            var text=go.GetComponent<TextMeshProUGUI>();
            text.font=font; text.text=value; text.fontSize=size; text.color=color;
            text.enableAutoSizing=false; text.enableWordWrapping=true;
            text.alignment=TextAlignmentOptions.TopLeft;
            text.raycastTarget=false; text.richText=false;
            return text;
        }
        private static TMP_Text Paragraph(Transform parent, string value, Color color, int size=22)
        {
            var text=Label(parent,value,size,color);
            var layout=text.gameObject.AddComponent<LayoutElement>(); layout.flexibleWidth=1;
            return text;
        }
        private static void Heading(Transform parent, string value, int size=36)
        {
            var text=Paragraph(parent,value,Text,size); text.fontStyle=FontStyles.Bold;
        }
        private static Button Button(Transform parent,string value,bool inline)
        {
            var go=new GameObject(value,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));
            go.transform.SetParent(parent,false); go.GetComponent<Image>().color=Accent;
            var button=go.GetComponent<Button>(); button.targetGraphic=go.GetComponent<Image>();
            var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.15f,1.15f,1.15f,1);
            colors.selectedColor=new Color(1.18f,1.18f,1.18f,1); button.colors=colors;
            var layout=go.GetComponent<LayoutElement>(); layout.minHeight=48; layout.preferredHeight=48;
            if(inline){layout.preferredWidth=154;layout.minWidth=110;layout.flexibleWidth=1;}
            var text=Label(go.transform,value,20,Text); text.alignment=TextAlignmentOptions.Center; Stretch(text.rectTransform,8);
            return button;
        }
        private static void Route(Transform parent,string value,string id,bool inline=false)
        {
            UnityEventTools.AddStringPersistentListener(Button(parent,value,inline).onClick,router.Show,id);
        }
        private static void Intent(Transform parent,string value,string intent,bool inline=false)
        {
            UnityEventTools.AddStringPersistentListener(Button(parent,value,inline).onClick,router.Intent,intent);
        }
        private static void Method(Transform parent,string value,UnityEngine.Events.UnityAction action)
        {
            UnityEventTools.AddPersistentListener(Button(parent,value,false).onClick,action);
        }
        private static void Stretch(RectTransform rect,float inset)
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=new Vector2(inset,inset);rect.offsetMax=new Vector2(-inset,-inset);
        }
        private static void Fixed(GameObject go,float width,float height)
        {
            var layout=go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if(width>=0){layout.minWidth=width;layout.preferredWidth=width;}
            if(height>=0){layout.minHeight=height;layout.preferredHeight=height;}
        }
        private static void Flexible(GameObject go)
        {
            var layout=go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            layout.flexibleWidth=1;layout.flexibleHeight=1;
        }
        private static void EnsureFolder(string path)
        {
            var parts=path.Split('/'); var prefix=parts[0];
            for(int i=1;i<parts.Length;i++){
                string next=prefix+"/"+parts[i];
                if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(prefix,parts[i]);
                prefix=next;
            }
        }
        private static void CreateGraybox(string path)
        {
            var root=new GameObject("SYNTHETIC_Layout_Context_Not_Station_Model");
            var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if(shader==null){Debug.LogWarning("No supported preview shader. Scene has UI only.");return;}
            var mat=new Material(shader); mat.color=new Color32(76,95,104,255);
            AssetDatabase.CreateAsset(mat,path+"/Graybox.mat");
            Vector3[] positions={new Vector3(0,-.6f,0),new Vector3(-9,0,2),new Vector3(7,0,5),new Vector3(1,0,-7)};
            Vector3[] sizes={new Vector3(46,1,32),new Vector3(12,1.5f,7),new Vector3(15,1.5f,5),new Vector3(24,1,2.5f)};
            for(int i=0;i<positions.Length;i++){
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="LayoutBlock_"+i;
                cube.transform.SetParent(root.transform);cube.transform.position=positions[i];cube.transform.localScale=sizes[i];
                cube.GetComponent<Renderer>().sharedMaterial=mat;
            }
        }
    }
}
#endif
