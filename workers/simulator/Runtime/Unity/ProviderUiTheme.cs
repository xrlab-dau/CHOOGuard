using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ChooGuard.Seeded.Unity
{
    // Matches the existing project's FpsUiFactory/HUD tokens without depending on
    // its app assembly or changing original Assets. All objects are runtime-owned.
    internal static class ProviderUiTheme
    {
        internal static readonly Color Panel = new Color(.035f,.045f,.06f,.96f);
        internal static readonly Color Strong = new Color(.02f,.028f,.04f,.98f);
        internal static readonly Color Dim = new Color(1,1,1,.62f);
        internal static readonly Color Accent = new Color(1f,.69f,.13f,1f);
        internal static readonly Color Danger = new Color(.94f,.29f,.27f,1f);
        internal static readonly Color Staff = new Color(.36f,.64f,1f,1f);

        internal static RectTransform Node(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            return (RectTransform)go.transform;
        }

        internal static RectTransform Stretch(RectTransform rect,float left=0,float bottom=0,float right=0,float top=0)
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);
            rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);return rect;
        }

        internal static RectTransform Place(RectTransform rect,Vector2 anchor,Vector2 offset,Vector2 size)
        {
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=anchor;
            rect.anchoredPosition=offset;rect.sizeDelta=size;return rect;
        }

        internal static Image Block(Transform parent,string name,Color color,bool raycast=false)
        {
            var rect=Node(parent,name);var image=rect.gameObject.AddComponent<Image>();
            image.color=color;image.raycastTarget=raycast;return image;
        }

        internal static TMP_Text Text(Transform parent,TMP_FontAsset font,string name,float size,Color? color=null)
        {
            var rect=Node(parent,name);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=font;text.fontSize=size;text.color=color??Color.white;
            text.alignment=TextAlignmentOptions.TopLeft;text.raycastTarget=false;
            text.richText=false;text.textWrappingMode=TextWrappingModes.Normal;
            text.overflowMode=TextOverflowModes.Ellipsis;return text;
        }

        internal static Button Button(Transform parent,TMP_FontAsset font,string caption,UnityAction action,bool primary=false)
        {
            var image=Block(parent,caption,Color.white,true);var button=image.gameObject.AddComponent<Button>();
            button.targetGraphic=image;var colors=button.colors;colors.colorMultiplier=1;colors.fadeDuration=.08f;
            colors.normalColor=primary?Accent:new Color(1,1,1,.10f);
            colors.highlightedColor=primary?new Color(1,.78f,.3f,1):new Color(1,1,1,.22f);
            colors.pressedColor=primary?new Color(.82f,.52f,.06f,1):new Color(1,1,1,.32f);
            colors.selectedColor=colors.highlightedColor;colors.disabledColor=new Color(1,1,1,.04f);button.colors=colors;
            var label=Text(image.transform,font,"제목",17,primary?Strong:Color.white);
            Stretch(label.rectTransform,14,6,12,6);label.alignment=TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.text=caption;
            button.onClick.AddListener(action);return button;
        }

        internal static void Caption(Button button,string value)
        {
            var text=button.GetComponentInChildren<TMP_Text>();if(text!=null)text.text=value;
        }

        internal static TMP_InputField Search(Transform parent,TMP_FontAsset font,Action<string> changed)
        {
            var image=Block(parent,"모델 검색",new Color(1,1,1,.08f),true);image.gameObject.SetActive(false);
            var viewport=Node(image.transform,"입력 영역");Stretch(viewport,14,5,12,5);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hint=Text(viewport,font,"검색 안내",17,Dim);Stretch(hint.rectTransform);hint.text="모델 · 제공자 · 계정 검색";
            var text=Text(viewport,font,"검색어",17);Stretch(text.rectTransform);
            hint.alignment=text.alignment=TextAlignmentOptions.MidlineLeft;
            hint.textWrappingMode=text.textWrappingMode=TextWrappingModes.NoWrap;
            var field=image.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=image;
            field.textViewport=viewport;field.textComponent=text;field.placeholder=hint;field.fontAsset=font;field.pointSize=17;
            field.richText=false;field.characterLimit=200;field.lineType=TMP_InputField.LineType.SingleLine;
            field.customCaretColor=true;field.caretColor=Accent;field.selectionColor=new Color(1,.69f,.13f,.35f);
            field.onValueChanged.AddListener(value=>changed(value));image.gameObject.SetActive(true);return field;
        }

        internal static ScrollRect List(Transform parent,string name,out RectTransform content)
        {
            var root=Node(parent,name);var viewport=Block(root,"표시 영역",Color.clear,true);
            Stretch(viewport.rectTransform);viewport.gameObject.AddComponent<RectMask2D>();
            content=Node(viewport.transform,"목록");content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;
            content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=6;
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport.rectTransform;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=32;scroll.inertia=false;return scroll;
        }

        internal static void Height(GameObject go,float height)
        {
            var layout=go.GetComponent<LayoutElement>()??go.AddComponent<LayoutElement>();
            layout.minHeight=height;layout.preferredHeight=height;layout.flexibleHeight=0;
        }

        internal static void Clear(RectTransform content)
        {
            for(var i=content.childCount-1;i>=0;i--)
            {var child=content.GetChild(i).gameObject;child.SetActive(false);UnityEngine.Object.Destroy(child);}
        }
    }
}
