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
        private GameObject canvasRoot;
        private TMP_Text prompt,pause,feedback;
        private Image reticle;
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
            var dot=new GameObject("중앙점",typeof(RectTransform),typeof(Image));dot.transform.SetParent(canvasRoot.transform,false);var rect=dot.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(4,4);reticle=dot.GetComponent<Image>();reticle.color=Color.white;reticle.raycastTarget=false;
            Responder.FeedbackChanged+=OnFeedback;
        }
        private TMP_Text Label(string name,Vector2 offset,Vector2 size,float fontSize)=>FpsUiFactory.Label(canvasRoot.transform,KoreanFont,name,offset,size,fontSize);
        private void Update()
        {
            if(prompt==null)return;prompt.text=Responder.IsPaused?"":Responder.CurrentPrompt;reticle.enabled=!Responder.IsPaused;
            pause.text=Responder.IsPaused?"일시 정지 · 클릭하여 계속\n\n마우스 시점 · WASD 이동 · Shift 빠르게\nE 상호작용 · Esc 정지":"";
            feedback.text=Time.unscaledTime<feedbackUntil?Responder.LastFeedback:"";
        }
        private void OnFeedback(string message){feedbackUntil=Time.unscaledTime+4;}
        private void OnDestroy(){if(Responder!=null)Responder.FeedbackChanged-=OnFeedback;if(canvasRoot!=null)Destroy(canvasRoot);}
    }
}
