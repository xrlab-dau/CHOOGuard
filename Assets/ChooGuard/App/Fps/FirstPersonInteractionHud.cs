using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ChooGuard.App.Fps
{
    // Contextual action guidance only: no quiz, review/debrief, prediction, or RTS controls.
    public sealed class FirstPersonInteractionHud : MonoBehaviour
    {
        public FirstPersonResponder Responder;
        public TMP_FontAsset KoreanFont;
        // 응시 진행을 표시하려면 체류 시간을 읽어야 한다. 비워 두면 플레이어에서 찾는다.
        public ChooGuard.App.Fps.Work.FpsGazeTracker GazeTracker;
        private GameObject canvasRoot;
        private TMP_Text prompt,pause,feedback,gazeLabel;
        private Image reticle;
        private readonly System.Collections.Generic.List<Image> reticleParts=new System.Collections.Generic.List<Image>();
        private float feedbackUntil;
        private void Start()
        {
            if(Responder==null)Responder=GetComponent<FirstPersonResponder>();
            // 침묵 실패 금지 — 예전에는 여기서 오류 없이 return 해 캔버스 자체가 생성되지 않았고
            // "아무것도 안 보이는데 오류도 없는" 상태가 됐다. 크게 실패한다.
            if(Responder==null){Debug.LogError("[FirstPersonInteractionHud] FirstPersonResponder 가 없어 HUD 를 만들지 않습니다.",this);enabled=false;return;}
            if(KoreanFont==null){Debug.LogError("[FirstPersonInteractionHud] 한국어 폰트가 없어 HUD 를 만들지 않습니다. Assets/ChooGuard/Settings/ImportedAssets/Fonts/NotoSansCJKkr SDF.asset 를 지정하세요.",this);enabled=false;return;}
            // 캔버스·라벨 생성은 FpsUiFactory 로 옮겼다. 판정 단말 화면이 두 번째 사용처가 되면서
            // 복사 대신 공유로 바꿨다 — 기본값(폰트·색·레이캐스트 비대상)이 한 곳에만 있어야 두 화면이 갈라지지 않는다.
            canvasRoot=FpsUiFactory.Canvas("FirstPersonInteractionHUD",transform,0);
            prompt=Label("상호작용",new Vector2(0,-155),new Vector2(760,48),23);
            feedback=Label("행동 결과",new Vector2(0,-210),new Vector2(900,60),20);
            pause=Label("정지 안내",Vector2.zero,new Vector2(900,200),25);
            // 조준점. 4×4 흰 점 하나로는 어디를 겨누는지 보이지 않았다 — 고유번호 판독면이 시야의
            // 1.4%(약 4°×3°)뿐이라 그 점을 그 위에 0.5초 얹는 것이 사실상 불가능했다(2026-09-28 실측).
            // 검은 테두리를 깔아 밝은 벽에서도 보이게 하고, 십자로 만들어 중심을 읽을 수 있게 한다.
            reticle=Bar("조준점 · 중심",new Vector2(3,3),Color.white);
            Bar("조준점 · 좌",new Vector2(7,1),Color.white,new Vector2(-9,0));
            Bar("조준점 · 우",new Vector2(7,1),Color.white,new Vector2(9,0));
            Bar("조준점 · 상",new Vector2(1,7),Color.white,new Vector2(0,9));
            Bar("조준점 · 하",new Vector2(1,7),Color.white,new Vector2(0,-9));
            // 지금 무엇을 보고 있고 체류가 얼마나 쌓였는지. 판정은 바꾸지 않고 상태만 보여준다.
            gazeLabel=Label("응시 상태",new Vector2(0,-118),new Vector2(560,34),19);
            if(GazeTracker==null)GazeTracker=Responder.GetComponentInChildren<ChooGuard.App.Fps.Work.FpsGazeTracker>();
            if(GazeTracker==null)GazeTracker=FindFirstObjectByType<ChooGuard.App.Fps.Work.FpsGazeTracker>();
            if(GazeTracker==null)Debug.LogWarning("[FirstPersonInteractionHud] FpsGazeTracker 를 찾지 못해 응시 진행을 표시하지 않습니다.",this);
            Responder.FeedbackChanged+=OnFeedback;
        }
        private TMP_Text Label(string name,Vector2 offset,Vector2 size,float fontSize)=>FpsUiFactory.Label(canvasRoot.transform,KoreanFont,name,offset,size,fontSize);

        // 조준점 조각 하나. 흰 막대 뒤에 한 픽셀 큰 검은 막대를 깔아 밝은 배경에서도 읽히게 한다.
        private Image Bar(string name,Vector2 size,Color colour,Vector2 offset=default)
        {
            Image Make(string suffix,Vector2 s,Color c)
            {
                var go=new GameObject(name+suffix,typeof(RectTransform),typeof(Image));
                go.transform.SetParent(canvasRoot.transform,false);
                var rect=go.GetComponent<RectTransform>();
                rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
                rect.sizeDelta=s;rect.anchoredPosition=offset;
                var image=go.GetComponent<Image>();image.color=c;image.raycastTarget=false;
                reticleParts.Add(image);
                return image;
            }
            Make(" 테두리",size+new Vector2(2,2),new Color(0,0,0,.75f));
            return Make("",size,colour);
        }

        private void Update()
        {
            if(prompt==null)return;prompt.text=Responder.IsPaused?"":Responder.CurrentPrompt;
            bool visible=!Responder.IsPaused;
            foreach(var part in reticleParts)if(part!=null)part.enabled=visible;
            pause.text=Responder.IsPaused?"일시 정지 · 클릭하여 계속\n\n마우스 시점 · WASD 이동 · Shift 빠르게\nE 상호작용 · Esc 정지":"";
            feedback.text=Time.unscaledTime<feedbackUntil?Responder.LastFeedback:"";
            gazeLabel.text=visible?GazeText():"";
        }

        // 지금 겨누고 있는 관측 지점과 체류 진행. 무엇도 판정하지 않는다 — 이미 쌓인 값을 읽어 보여줄 뿐이다.
        // 이것이 없으면 "고유번호를 확인하세요" 만 반복되고 왜 인정되지 않는지 알 길이 없다.
        private string GazeText()
        {
            var collider=Responder.CurrentTargetCollider;
            if(collider==null||GazeTracker==null)return "";
            var facility=collider.GetComponentInParent<ChooGuard.App.Fps.Work.FacilityInspectable>();
            if(facility==null||facility.Points==null)return "";
            foreach(var point in facility.Points)
            {
                if(point==null||point.Surface!=collider)continue;
                float held=GazeTracker.DwellOf(collider);
                float need=Mathf.Max(.01f,point.RequiredDwellSeconds);
                if(held>=need)return point.Label+" 확인됨";
                return point.Label+" "+held.ToString("0.0")+" / "+need.ToString("0.0")+"초";
            }
            return "";
        }
        private void OnFeedback(string message){feedbackUntil=Time.unscaledTime+4;}
        private void OnDestroy(){if(Responder!=null)Responder.FeedbackChanged-=OnFeedback;if(canvasRoot!=null)Destroy(canvasRoot);}
    }
}
