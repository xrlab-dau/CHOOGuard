using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace ChooGuard.App.Mvp
{
    public sealed class MvpAgencyDispatchView : MonoBehaviour
    {
        public MvpAgencyDispatchController Controller;
        private TMP_Text title,resources,instruction,detail,reinforcementLabel;
        private Button workEvac,workMedical,release,cancel;
        private bool reinforcement;
        private readonly Button[] teamRows=new Button[3];
        private readonly TMP_Text[] teamLabels=new TMP_Text[3];
        private readonly string[] visibleTeamIds=new string[3];
        private void Start()
        {
            if(Controller==null)return;Controller.Initialize();var canvas=Controller.Workspace.transform.Find("WorkspaceCanvas");if(canvas==null)return;
            var panel=new GameObject("AgencyDispatchPanel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas,false);var r=(RectTransform)panel.transform;Rect(r,74,72,310,540);panel.GetComponent<Image>().color=new Color(.025f,.055f,.075f,.96f);
            title=Text(r,"기관 운영",10,8,116,24,17);
            var flow=GetComponent<MvpProgressionGraphView>();if(flow==null)flow=gameObject.AddComponent<MvpProgressionGraphView>();flow.Controller=Controller;
            Button(r,"운영 흐름",132,6,78,flow.Toggle);
            var forecastClient=Controller.Workspace.GetComponent<MvpJevForecastClient>();if(forecastClient==null)forecastClient=Controller.Workspace.gameObject.AddComponent<MvpJevForecastClient>();forecastClient.Controller=Controller;
            var forecastView=GetComponent<MvpJevForecastView>();if(forecastView==null)forecastView=gameObject.AddComponent<MvpJevForecastView>();forecastView.Controller=Controller;forecastView.Client=forecastClient;
            Button(r,"AI 예측",216,6,82,forecastView.Toggle);
            int slot=0;foreach(var a in Controller.Agencies){string id=a.id;string label=a.label.Contains("메리놀")?"메리놀병원":a.label;Button(r,label,10+(slot%2)*146,38+(slot/2)*31,140,()=>Controller.SelectAgency(id));slot++;}
            resources=Text(r,"가용 대응팀 확인 중",10,103,290,32,12);
            workEvac=Button(r,"대피 지원 업무",10,140,140,()=>Controller.BeginTargetSelection(Controller.SelectedAgencyId,"evacuation-support",reinforcement));
            workMedical=Button(r,"의료 지원 업무",156,140,142,()=>Controller.BeginTargetSelection(Controller.SelectedAgencyId,"medical-support",reinforcement));
            var additional=Button(r,"추가 지원: 꺼짐",10,175,288,()=>{reinforcement=!reinforcement;reinforcementLabel.text=reinforcement?"추가 지원: 켜짐 · 다른 가용팀 배정":"추가 지원: 꺼짐 · 같은 업무 중복 방지";});reinforcementLabel=additional.GetComponentInChildren<TMP_Text>();
            instruction=Text(r,"기관 선택 → 업무 선택 → 부산역 대합실 클릭",10,210,290,38,12);
            Button(r,"기관 보기",10,253,92,()=>Controller.FocusAgency(Controller.SelectedAgencyId));
            Button(r,"대상 보기",108,253,92,()=>Controller.Station.FocusReferenceHall());
            Button(r,"선택 취소",206,253,92,()=>Controller.CancelTargetSelection());
            for(int i=0;i<teamRows.Length;i++){int index=i;teamRows[i]=Button(r,"",10,289+i*36,288,()=>{if(visibleTeamIds[index]!=null)Controller.SelectOperationalTeam(visibleTeamIds[index]);});teamLabels[i]=teamRows[i].GetComponentInChildren<TMP_Text>();}
            detail=Text(r,"대응팀은 공동 업무를 수행합니다",10,400,290,33,11);
            Button(r,"선택팀 추적",10,439,140,()=>Controller.FocusVehicle(Controller.SelectedOperationalTeamId));
            Button(r,"추적 해제",156,439,142,()=>Controller.Station.Navigation.EndFollow());
            cancel=Button(r,"출발 전 취소",10,475,140,()=>Controller.CancelOperation(Controller.SelectedOperationalTeamId));
            release=Button(r,"지원 종료·복귀",156,475,142,()=>{var id=Controller.SelectedOperationalTeamId;foreach(var m in Controller.MissionSnapshots)if(m.TeamId==id){if(m.State=="HandoffPending")Controller.ConfirmMedicalHandoffAndReturn(id);else Controller.RequestReturn(id);break;}});
            Text(r,"훈련용 팀·차량 구성 · 실제 보유 수 아님\n사건 전 사전배치 / 의료 요청은 회복·이송 판정 아님",10,510,290,26,10);
            Controller.Station.FocusCity();Controller.Workspace.ApplyAgencyContext();
        }
        private void Update()
        {
            if(title==null)return;title.text=Controller.AgencyLabel(Controller.SelectedAgencyId);int total=0,ready=0,vehicles=0,index=0;MvpAgencyDispatchController.MissionSnapshot selected=null;
            foreach(var m in Controller.MissionSnapshots)
            {
                if(m.TeamId==Controller.SelectedOperationalTeamId)selected=m;
                if(m.AgencyId!=Controller.SelectedAgencyId)continue;total++;vehicles+=m.VisualMemberCount;if(m.State=="Ready")ready++;
                if(index<teamRows.Length){visibleTeamIds[index]=m.TeamId;teamRows[index].gameObject.SetActive(true);teamLabels[index].text=m.TeamLabel+" · "+MvpAgencyDispatchController.KoreanState(m.State)+" · 차량 "+m.VisualMemberCount;index++;}
            }
            for(int i=index;i<teamRows.Length;i++){visibleTeamIds[i]=null;teamRows[i].gameObject.SetActive(false);}
            resources.text=total>0?"가용 "+ready+" / 대응팀 "+total+" · 차량 표현 "+vehicles+"대\n업무별 대응팀 자동 배정 · 차량별 명령 없음":"위치 확인 기관 · 출동·환자 인계 기능은 준비 중";
            workEvac.interactable=workMedical.interactable=total>0;
            instruction.text=Controller.HasPendingTarget?(Controller.PendingWorkLabel+" 대상 선택 중 · 부산역을 클릭하세요\n"+(Controller.LastReason??"우클릭 또는 선택 취소로 종료")):(Controller.LastReason??"기관 선택 → 업무 선택 → 부산역 대합실 클릭\n선택 클릭은 카메라를 이동하지 않습니다");
            detail.text=selected==null?"대응팀을 선택하면 진행 상태와 추적을 확인합니다":selected.TeamLabel+" · "+MvpAgencyDispatchController.KoreanState(selected.State)+"\n"+selected.Reason;
            cancel.interactable=selected!=null&&selected.State=="Requested";
            release.interactable=selected!=null&&(selected.State=="HandoffPending"||selected.State=="WaitingIncident"||selected.State=="Standby");
            release.GetComponentInChildren<TMP_Text>().text=selected?.State=="HandoffPending"?"종료 확인·복귀":"지원 종료·복귀";
        }
        private TMP_Text Text(Transform parent,string text,float x,float y,float w,float h,float size){var g=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(parent,false);Rect((RectTransform)g.transform,x,y,w,h);var t=g.GetComponent<TextMeshProUGUI>();t.font=Controller.Workspace.Font;t.text=text;t.fontSize=size;t.color=Color.white;t.raycastTarget=false;return t;}
        private Button Button(Transform parent,string text,float x,float y,float width,UnityEngine.Events.UnityAction click){var g=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));g.transform.SetParent(parent,false);Rect((RectTransform)g.transform,x,y,width,28);g.GetComponent<Image>().color=new Color(.08f,.22f,.28f);var b=g.GetComponent<Button>();b.onClick.AddListener(click);var t=Text(g.transform,text,0,0,width,28,12);t.alignment=TextAlignmentOptions.Center;return b;}
        private static void Rect(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    }
}
