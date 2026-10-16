using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Shell
{
    /// <summary>One deliberate choice before either tutorial selection or emergency preparation.</summary>
    public sealed class PlayModeMenu : MonoBehaviour
    {
        public Button TutorialButton { get; private set; }
        public Button EmergencyButton { get; private set; }
        public bool Visible => gameObject.activeSelf;
        public Func<string> ConnectionSummary;
        public Func<bool> ModalOpen;
        RectTransform panel;
        TMP_Text connection;
        Action back;

        public static PlayModeMenu Create(Transform parent,TMP_FontAsset font,Action tutorial,Action emergency,Action accounts,Action back)
        {
            var root=FpsUiFactory.Node(parent,"플레이 방식 선택");FpsUiFactory.Stretch(root);
            var view=root.gameObject.AddComponent<PlayModeMenu>();view.back=back;
            var shade=FpsUiFactory.Panel(root,"선택 화면 배경",Vector2.zero,Vector2.zero,new Color(.02f,.028f,.04f,.94f));
            FpsUiFactory.Stretch(shade.rectTransform);shade.raycastTarget=true;
            view.panel=FpsUiFactory.Node(root,"모드 선택");view.panel.anchorMin=view.panel.anchorMax=new Vector2(.5f,.5f);view.panel.sizeDelta=new Vector2(1120,640);
            var title=FpsUiFactory.Text(view.panel,font,"제목",new Vector2(0,1),Vector2.zero,new Vector2(1120,62),38,TextAlignmentOptions.TopLeft);
            title.text="어떻게 플레이할까요?";title.fontStyle=FontStyles.Bold;
            var lead=FpsUiFactory.Text(view.panel,font,"안내",new Vector2(0,1),new Vector2(0,-74),new Vector2(1120,42),19,TextAlignmentOptions.TopLeft);
            lead.text="조작을 익히는 실습부터 시작하거나, 변화하는 비상상황에 바로 대응해보세요.";lead.color=FpsUiFactory.TextDim;
            view.TutorialButton=Card(view.panel,font,"튜토리얼",new Vector2(0,1),"차근차근 익히기","장비 조작과 대응 순서를\n안내와 실습으로 익힙니다.","승무원 열차점검 · 13단계\nAI 연결 없이 플레이","튜토리얼 고르기  →",FpsUiFactory.Staff,()=>tutorial?.Invoke());
            view.EmergencyButton=Card(view.panel,font,"랜덤 비상상황 대응",new Vector2(1,1),"현장에서 대응하기","승객과 주변의 이상을 살피고\n상황 변화에 맞춰 대응합니다.","AI 초기 조건 · NPC 행동\n부산역 순회와 비상 대응","비상상황 플레이 준비  →",FpsUiFactory.Accent,()=>emergency?.Invoke());
            view.connection=FpsUiFactory.Text(view.panel,font,"AI 연결 상태",new Vector2(0,1),new Vector2(0,-524),new Vector2(1120,38),16,TextAlignmentOptions.TopLeft);
            view.connection.color=FpsUiFactory.TextDim;view.connection.richText=false;
            FpsUiFactory.Button(view.panel,font,"시작 화면으로",new Vector2(0,0),Vector2.zero,new Vector2(230,52),()=>back?.Invoke(),19);
            FpsUiFactory.Button(view.panel,font,"AI 계정과 모델",new Vector2(1,0),Vector2.zero,new Vector2(270,52),()=>accounts?.Invoke(),19);
            view.Hide();return view;
        }

        static Button Card(Transform parent,TMP_FontAsset font,string title,Vector2 anchor,string tag,string description,string details,string cta,Color accent,Action action)
        {
            var button=FpsUiFactory.Button(parent,font,title,anchor,new Vector2(0,-144),new Vector2(548,360),()=>action(),30);
            var caption=button.GetComponentInChildren<TMP_Text>();caption.gameObject.SetActive(false);
            var colors=button.colors;colors.normalColor=new Color(1,1,1,.07f);colors.highlightedColor=new Color(1,1,1,.14f);colors.selectedColor=new Color(1,1,1,.12f);button.colors=colors;
            FpsUiFactory.Block(button.transform,"선택 표식",new Vector2(0,1),Vector2.zero,new Vector2(548,3),accent);
            var label=FpsUiFactory.Text(button.transform,font,"모드 안내",new Vector2(0,1),new Vector2(26,-24),new Vector2(496,30),16,TextAlignmentOptions.TopLeft);label.text=tag;label.color=accent;
            label=FpsUiFactory.Text(button.transform,font,"모드 제목",new Vector2(0,1),new Vector2(26,-70),new Vector2(496,52),30,TextAlignmentOptions.TopLeft);label.text=title;label.fontStyle=FontStyles.Bold;
            label=FpsUiFactory.Text(button.transform,font,"설명",new Vector2(0,1),new Vector2(26,-132),new Vector2(496,76),19,TextAlignmentOptions.TopLeft);label.text=description;
            label=FpsUiFactory.Text(button.transform,font,"플레이 정보",new Vector2(0,1),new Vector2(26,-222),new Vector2(496,64),16,TextAlignmentOptions.TopLeft);label.text=details;label.color=FpsUiFactory.TextDim;
            label=FpsUiFactory.Text(button.transform,font,"선택 동작",new Vector2(0,0),new Vector2(26,22),new Vector2(496,36),19,TextAlignmentOptions.BottomLeft);label.text=cta;label.color=accent;
            return button;
        }

        public void Show(){gameObject.SetActive(true);Fit();TutorialButton.Select();}
        public void Hide()=>gameObject.SetActive(false);
        void Fit()
        {
            if(panel==null)return;var available=((RectTransform)transform).rect.size;
            panel.localScale=Vector3.one*Mathf.Clamp(Mathf.Min((available.x-64)/1120,(available.y-48)/640),.15f,1f);
        }
        void Update()
        {
            Fit();connection.text=ConnectionSummary?.Invoke()??"AI 계정과 모델을 선택하면 초기 비상조건과 NPC 계획에 함께 사용합니다.";
            if(ModalOpen?.Invoke()==true)return;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)back?.Invoke();
        }
    }
}
