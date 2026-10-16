using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChooGuard.Seeded.Unity
{
    /// <summary>Current-station entry screen. Generation and starting play are explicit user actions.</summary>
    public sealed class StationPlayPanel : MonoBehaviour
    {
        PlatformSeededSimulator simulator;
        Canvas canvas;
        TMP_Text status, liveStatus;
        TMP_InputField seed;
        Button generate, start;
        GameObject setup;
        bool startAfterGeneration;
        public Button StartShiftButton=>start;

        void Start()
        {
            simulator=GetComponent<PlatformSeededSimulator>();
            var font=simulator.ProviderMenuFont;
            if(font==null){Debug.LogError("현재 역 플레이에 한국어 글꼴이 연결되지 않았습니다.");return;}
            if(EventSystem.current==null)
            {
                var events=new GameObject("Station input",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var root=new GameObject("역 근무",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1440,900);scale.matchWidthOrHeight=1;
            setup=new GameObject("근무 준비",typeof(RectTransform),typeof(Image),typeof(VerticalLayoutGroup));setup.transform.SetParent(root.transform,false);
            var rect=setup.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(28,-28);rect.sizeDelta=new Vector2(480,816);
            setup.GetComponent<Image>().color=new Color(.035f,.045f,.055f,.95f);
            var layout=setup.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(24,24,24,24);layout.spacing=12;layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
            Label(setup.transform,font,"CHOOGuard · 부산역 근무",25,48);
            Label(setup.transform,font,"근무 시작을 누르면 AI가 비상상황을 생성하고 바로 플레이를 시작합니다.",17,62);
            Button(setup.transform,font,"AI 계정과 모델 · F8",()=>simulator.SetProviderMenuOpen(true));
            Label(setup.transform,font,"근무 시드",15,24);
            seed=Input(setup.transform,font,simulator.InitialSeed);
            seed.onEndEdit.AddListener(value=>{if(ulong.TryParse(value,out _))simulator.InitialSeed=value;else seed.SetTextWithoutNotify(simulator.InitialSeed);});
            start=Button(setup.transform,font,"근무 시작",()=>Run(BeginShift));
            generate=Button(setup.transform,font,"초기 조건만 미리 생성",()=>Run(()=>simulator.GenerateInitialConditions()));
            Label(setup.transform,font,"WASD 이동 · Shift 빠르게 이동 · E 조작\nEsc 포인터 해제 · F8 계정과 모델",15,55);
            status=Label(setup.transform,font,"현재 역 준비 중",15,160);
            var bar=new GameObject("근무 상태",typeof(RectTransform));bar.transform.SetParent(root.transform,false);
            var br=bar.GetComponent<RectTransform>();br.anchorMin=new Vector2(0,1);br.anchorMax=new Vector2(1,1);br.pivot=new Vector2(.5f,1);br.anchoredPosition=new Vector2(0,-18);br.sizeDelta=new Vector2(-60,90);
            liveStatus=Label(bar.transform,font,"",16,70);var lr=liveStatus.rectTransform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
            var actions=new GameObject("일시정지 동작",typeof(RectTransform),typeof(HorizontalLayoutGroup));actions.transform.SetParent(root.transform,false);
            var ar=actions.GetComponent<RectTransform>();ar.anchorMin=ar.anchorMax=new Vector2(.5f,0);ar.pivot=new Vector2(.5f,0);ar.anchoredPosition=new Vector2(0,24);ar.sizeDelta=new Vector2(640,46);
            var al=actions.GetComponent<HorizontalLayoutGroup>();al.spacing=12;al.childForceExpandWidth=true;
            Button(actions.transform,font,"이동 계속",()=>{if(simulator.IsPrepared)simulator.Player.Capture(true);});
            Button(actions.transform,font,"AI 계정 · F8",()=>simulator.SetProviderMenuOpen(true));
            Button(actions.transform,font,"근무 종료",()=>simulator.StopRun());
            actions.name="Station pause actions";
            if(PlatformSeededSimulator.OpenProviderMenuOnStart){PlatformSeededSimulator.OpenProviderMenuOnStart=false;simulator.SetProviderMenuOpen(true);}
            else simulator.RefreshProviders(); // Local status only; does not start OAuth, catalog retrieval or inference.
        }

        void Run(Action action)
        {
            try{action();}catch(Exception error){simulator.Status="근무를 시작하지 못했습니다: "+error.Message;Debug.LogException(error);}
        }

        void BeginShift()
        {
            if(startAfterGeneration||simulator.InitialGenerationInFlight||simulator.Running)return;
            if(simulator.ProviderState?.Subscription.Ready!=true)
            {simulator.Status="AI 계정 메뉴에서 연결 상태와 구독 사용 허용을 확인해주세요.";simulator.SetProviderMenuOpen(true);return;}
            if(simulator.HasPreparedInitialConditions){StartPreparedShift();return;}
            if(simulator.ProviderState.InitialAllowed!=true)
            {simulator.Status="AI 계정 메뉴에서 초기 비상상황 생성 1회를 허용해주세요.";simulator.SetProviderMenuOpen(true);return;}
            simulator.GenerateInitialConditions();
            startAfterGeneration=simulator.InitialGenerationInFlight;
            if(startAfterGeneration)simulator.Status="비상상황 생성 중입니다. 완료되면 근무를 시작합니다.";
        }

        void StartPreparedShift()
        {
            if(simulator.StartRun(simulator.InitialSeed))simulator.Player.Capture(true);
        }

        void Update()
        {
            if(canvas==null||simulator==null)return;
            if(startAfterGeneration&&!simulator.InitialGenerationInFlight)
            {
                startAfterGeneration=false;
                if(simulator.HasPreparedInitialConditions)Run(StartPreparedShift);
                else simulator.Status="비상상황을 생성하지 못했습니다. 아래 생성 결과를 확인해주세요.";
            }
            canvas.enabled=!simulator.ProviderMenuOpen;
            bool running=simulator.Running;setup.SetActive(!running);
            var actions=canvas.transform.Find("Station pause actions");
            if(actions!=null)actions.gameObject.SetActive(running&&simulator.Player!=null&&!simulator.Player.Captured);
            bool busy=simulator.InitialGenerationInFlight||simulator.ProviderBusy;
            generate.interactable=simulator.IsPrepared&&!busy&&simulator.ProviderState?.Subscription.Ready==true&&simulator.ProviderState.InitialAllowed;
            start.interactable=simulator.IsPrepared&&!busy;
            start.GetComponentInChildren<TMP_Text>().text=startAfterGeneration?"AI 비상상황 생성 중…":"근무 시작";
            seed.interactable=!busy;
            status.text=simulator.ProviderMessage+"\n\n"+simulator.InitialGenerationStatus+"\n\n"+simulator.Status;
            string model=simulator.ProviderState?.Subscription.Model;
            liveStatus.gameObject.SetActive(running);
            if(running)liveStatus.text="부산역 근무 · "+(string.IsNullOrEmpty(model)?"로컬 행동 실행":model)+"\n승객 "+simulator.Npcs.Count+" · F8 AI 계정 · Esc 포인터 해제";
        }

        static TMP_Text Label(Transform parent,TMP_FontAsset font,string text,int size,float height)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI),typeof(LayoutElement));go.transform.SetParent(parent,false);
            var label=go.GetComponent<TextMeshProUGUI>();label.font=font;label.fontSize=size;label.text=text;label.color=Color.white;label.richText=false;label.raycastTarget=false;
            label.textWrappingMode=TextWrappingModes.Normal;go.GetComponent<LayoutElement>().preferredHeight=height;return label;
        }
        static Button Button(Transform parent,TMP_FontAsset font,string caption,Action action)
        {
            var go=new GameObject(caption,typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=new Color(.32f,.23f,.10f,.98f);go.GetComponent<LayoutElement>().preferredHeight=44;
            var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(()=>action());
            var text=Label(go.transform,font,caption,17,44);text.alignment=TextAlignmentOptions.Center;
            var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(8,0);rect.offsetMax=new Vector2(-8,0);return button;
        }
        static TMP_InputField Input(Transform parent,TMP_FontAsset font,string value)
        {
            var go=new GameObject("Seed",typeof(RectTransform),typeof(Image),typeof(TMP_InputField),typeof(LayoutElement));go.transform.SetParent(parent,false);
            go.GetComponent<Image>().color=new Color(.10f,.12f,.14f,1);go.GetComponent<LayoutElement>().preferredHeight=42;
            var text=Label(go.transform,font,value,17,42);var rect=text.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(10,4);rect.offsetMax=new Vector2(-10,-4);
            var input=go.GetComponent<TMP_InputField>();input.textViewport=rect;input.textComponent=(TextMeshProUGUI)text;input.contentType=TMP_InputField.ContentType.IntegerNumber;input.characterLimit=20;input.text=value;return input;
        }
    }
}
