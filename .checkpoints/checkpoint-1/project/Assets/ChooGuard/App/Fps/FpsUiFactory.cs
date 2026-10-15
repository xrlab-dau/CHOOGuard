using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
        public static readonly Color PanelColor=new Color(.035f,.045f,.06f,.72f);
        public static readonly Color PanelStrong=new Color(.02f,.028f,.04f,.92f);
        public static readonly Color TextDim=new Color(1,1,1,.62f);
        public static readonly Color Accent=new Color(1f,.69f,.13f,1f);          // 업무 표식
        public static readonly Color Danger=new Color(.94f,.29f,.27f,1f);        // 위험 표식
        public static readonly Color Staff=new Color(.36f,.64f,1f,1f);           // 동료·기관 표식

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
        // 버튼·슬라이더를 받는 캔버스. 비대화형 HUD 캔버스에는 레이캐스터를 붙이지 않는다.
        public static GameObject InteractiveCanvas(string name,Transform parent,int order)
        {
            var root=Canvas(name,parent,order);root.AddComponent<GraphicRaycaster>();return root;
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

        // HUD 가장자리 요소용. anchor 는 화면 기준점(0..1)이고 pivot 도 같은 값이므로 offset 이 가장자리 여백이 된다.
        public static RectTransform Place(RectTransform rect,Vector2 anchor,Vector2 offset,Vector2 size)
        {
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=anchor;rect.anchoredPosition=offset;rect.sizeDelta=size;return rect;
        }
        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        public static RectTransform Node(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;
        }
        public static TMP_Text Text(Transform parent,TMP_FontAsset font,string name,Vector2 anchor,Vector2 offset,Vector2 size,float fontSize,TextAlignmentOptions alignment)
        {
            var text=Label(parent,font,name,offset,size,fontSize,alignment);Place(text.rectTransform,anchor,offset,size);
            // 본고딕 CJK 의 줄 높이는 글자 크기의 약 1.45배다. Ellipsis 는 줄이 상자보다 높으면 글자를 통째로 버리므로
            // 기본은 Overflow 로 두고, 잘라야 하는 곳만 호출자가 높이를 충분히 잡은 뒤 모드를 바꾼다.
            text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Overflow;return text;
        }
        public static Image Block(Transform parent,string name,Vector2 anchor,Vector2 offset,Vector2 size,Color color)
        {
            var image=Panel(parent,name,offset,size,color);Place(image.rectTransform,anchor,offset,size);return image;
        }
        public static RawImage Picture(Transform parent,string name,Texture texture)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
            var image=go.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;return image;
        }
        public static Button Button(Transform parent,TMP_FontAsset font,string label,Vector2 anchor,Vector2 offset,Vector2 size,UnityAction onClick,float fontSize=22)
        {
            var image=Block(parent,label,anchor,offset,size,Color.white);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.colorMultiplier=1;colors.fadeDuration=.08f;
            colors.normalColor=new Color(1,1,1,.12f);colors.highlightedColor=new Color(1,1,1,.26f);
            colors.pressedColor=new Color(1,1,1,.36f);colors.selectedColor=new Color(1,1,1,.22f);colors.disabledColor=new Color(1,1,1,.04f);
            button.colors=colors;
            var caption=Label(image.transform,font,"제목",Vector2.zero,size,fontSize,TextAlignmentOptions.Left);
            Stretch(caption.rectTransform);caption.margin=new Vector4(22,0,12,0);caption.text=label;
            button.onClick.AddListener(onClick);
            return button;
        }
        public static Slider Slider(Transform parent,string name,Vector2 anchor,Vector2 offset,Vector2 size,float min,float max,float value,Action<float> changed)
        {
            var root=Node(parent,name);Place(root,anchor,offset,size);
            var track=Block(root,"트랙",new Vector2(.5f,.5f),Vector2.zero,new Vector2(size.x,6),new Color(1,1,1,.18f));track.raycastTarget=true;
            var fillArea=Node(root,"채움 영역");Stretch(fillArea);
            fillArea.offsetMin=new Vector2(0,size.y*.5f-3);fillArea.offsetMax=new Vector2(0,-(size.y*.5f-3));
            var fill=Panel(fillArea,"채움",Vector2.zero,Vector2.zero,Accent);Stretch(fill.rectTransform);
            var handleArea=Node(root,"손잡이 영역");Stretch(handleArea);
            var handle=Panel(handleArea,"손잡이",Vector2.zero,new Vector2(18,18),Color.white);handle.raycastTarget=true;
            var slider=root.gameObject.AddComponent<Slider>();
            slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
            slider.minValue=min;slider.maxValue=max;slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v=>changed?.Invoke(v));
            return slider;
        }
        // 한 줄 입력칸. password 면 글자를 가린다(키 입력용). 붙여넣기·선택은 TMP_InputField 기본 동작이다.
        // 참조를 모두 연결한 뒤에 켜야 입력칸이 캐럿·글자 영역을 제대로 만든다.
        public static TMP_InputField InputField(Transform parent,TMP_FontAsset font,string name,Vector2 anchor,Vector2 offset,Vector2 size,float fontSize,string placeholder,bool password)
        {
            var image=Block(parent,name,anchor,offset,size,Color.white);image.raycastTarget=true;
            image.gameObject.SetActive(false);
            var area=Node(image.transform,"글자 영역");Stretch(area);area.offsetMin=new Vector2(16,4);area.offsetMax=new Vector2(-16,-4);area.gameObject.AddComponent<RectMask2D>();
            var hint=Label(area,font,"안내",Vector2.zero,Vector2.zero,fontSize,TextAlignmentOptions.Left);Stretch(hint.rectTransform);
            hint.text=placeholder;hint.color=new Color(1,1,1,.35f);hint.textWrappingMode=TextWrappingModes.NoWrap;
            var text=Label(area,font,"글자",Vector2.zero,Vector2.zero,fontSize,TextAlignmentOptions.Left);Stretch(text.rectTransform);
            text.textWrappingMode=TextWrappingModes.NoWrap;text.richText=false;
            var field=image.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic=image;field.textViewport=area;field.textComponent=text;field.placeholder=hint;field.fontAsset=font;field.pointSize=fontSize;
            field.lineType=TMP_InputField.LineType.SingleLine;field.characterLimit=512;field.richText=false;
            if(password){field.contentType=TMP_InputField.ContentType.Password;field.asteriskChar='*';}
            field.customCaretColor=true;field.caretColor=Accent;field.caretWidth=2;field.selectionColor=new Color(1f,.69f,.13f,.35f);
            var colors=field.colors;colors.colorMultiplier=1;colors.fadeDuration=.08f;
            colors.normalColor=new Color(1,1,1,.1f);colors.highlightedColor=new Color(1,1,1,.16f);colors.selectedColor=new Color(1,1,1,.2f);colors.pressedColor=new Color(1,1,1,.2f);
            field.colors=colors;
            image.gameObject.SetActive(true);
            return field;
        }
    }
}
