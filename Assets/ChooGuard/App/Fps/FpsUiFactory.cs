using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace ChooGuard.App.Fps
{
    // uGUI 코드생성 관례를 한 곳에 모은다. FirstPersonInteractionHud 가 쓰던 Label 을 그대로 옮긴 것으로,
    // 두 번째 화면(판정 단말)이 생기면서 복사 대신 공유로 바꿨다. 기본값(흰색·레이캐스트 비대상·한국어 폰트)이
    // 여기 한 곳에만 있으므로 두 화면의 문자 모양이 갈라지지 않는다.
    //
    // UI Toolkit 을 쓰지 않는다: 저장소에 UXML/USS 0건이고 한국어 폰트 자산이 TMP_FontAsset 전용이다.
    public static class FpsUiFactory
    {
        // 화면 전체를 덮는 오버레이 캔버스. order 는 겹침 순서다 — HUD 0, 그 위에 뜨는 화면은 더 큰 값.
        public static GameObject Canvas(string name,Transform parent,int order)
        {
            var root=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.Canvas),typeof(CanvasScaler));
            root.transform.SetParent(parent,false);
            var canvas=root.GetComponent<UnityEngine.Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;
            var scale=root.GetComponent<CanvasScaler>();
            scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution=new Vector2(1440,900);scale.matchWidthOrHeight=.5f;
            return root;
        }
        // 중앙 기준 앵커로 배치한다. 좌표계가 하나뿐이어야 화면 간 수치를 서로 비교할 수 있다.
        public static TMP_Text Label(Transform parent,TMP_FontAsset font,string name,Vector2 offset,Vector2 size,float fontSize,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=offset;rect.sizeDelta=size;
            var text=go.GetComponent<TextMeshProUGUI>();
            text.font=font;text.fontSize=fontSize;text.color=Color.white;text.alignment=alignment;text.raycastTarget=false;
            return text;
        }
        public static Image Panel(Transform parent,string name,Vector2 offset,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=offset;rect.sizeDelta=size;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;
            return image;
        }
    }
}
