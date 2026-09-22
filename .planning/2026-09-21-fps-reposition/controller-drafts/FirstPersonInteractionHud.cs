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
            if(Responder==null)Responder=GetComponent<FirstPersonResponder>();if(Responder==null||KoreanFont==null)return;
            canvasRoot=new GameObject("FirstPersonInteractionHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));canvasRoot.transform.SetParent(transform,false);
            canvasRoot.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scale=canvasRoot.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1440,900);scale.matchWidthOrHeight=.5f;
            prompt=Label("상호작용",new Vector2(0,-155),new Vector2(760,48),23);
            feedback=Label("행동 결과",new Vector2(0,-210),new Vector2(900,60),20);
            pause=Label("정지 안내",Vector2.zero,new Vector2(900,200),25);
            var dot=new GameObject("중앙점",typeof(RectTransform),typeof(Image));dot.transform.SetParent(canvasRoot.transform,false);var rect=dot.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(4,4);reticle=dot.GetComponent<Image>();reticle.color=Color.white;reticle.raycastTarget=false;
            Responder.FeedbackChanged+=OnFeedback;
        }
        private TMP_Text Label(string name,Vector2 offset,Vector2 size,float fontSize)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(canvasRoot.transform,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.anchoredPosition=offset;r.sizeDelta=size;var t=go.GetComponent<TextMeshProUGUI>();t.font=KoreanFont;t.fontSize=fontSize;t.color=Color.white;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;
        }
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
