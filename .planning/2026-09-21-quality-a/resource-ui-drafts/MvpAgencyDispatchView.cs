using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace ChooGuard.App.Mvp
{
    public sealed class MvpAgencyDispatchView : MonoBehaviour
    {
        public MvpAgencyDispatchController Controller;
        private GameObject panel;
        private TMP_Text title,resources,instruction,detail;
        private Button workEvac,workMedical,release,cancel,inside,follow,unfollow,cancelTarget,centralSource,choryangSource;
        private string stationSource=MvpAgencyDispatchController.Central119;
        private readonly Button[] teamRows=new Button[3];
        private readonly TMP_Text[] teamLabels=new TMP_Text[3];
        private readonly string[] visibleTeamIds=new string[3];
        private void Start()
        {
            if(Controller==null)return;Controller.Initialize();var canvas=Controller.Workspace.transform.Find("WorkspaceCanvas");if(canvas==null)return;
            panel=new GameObject("AgencyDispatchPanel",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas,false);var r=(RectTransform)panel.transform;Rect(r,24,72,320,542);panel.GetComponent<Image>().color=new Color(.025f,.055f,.075f,.96f);
            title=Text(r,"",12,10,240,30,19);Button(r,"닫기",266,10,42,Controller.ClearContextSelection);
            resources=Text(r,"",12,52,296,104,12);
            centralSource=Button(r,"중앙119 지원",12,164,142,()=>stationSource=MvpAgencyDispatchController.Central119);
            choryangSource=Button(r,"초량119 지원",162,164,146,()=>stationSource=MvpAgencyDispatchController.Choryang119);
            workEvac=Button(r,"대피 지원 요청",12,202,142,()=>RequestSupport("evacuation-support"));
            workMedical=Button(r,"의료 지원 요청",162,202,146,()=>RequestSupport("medical-support"));
            instruction=Text(r,"",12,244,296,118,12);
            inside=Button(r,"내부 보기",12,370,142,()=>{if(Controller.Workspace.CurrentFloor==0)Controller.Station.FocusReferenceHall();else Controller.Station.FocusCity();});
            cancelTarget=Button(r,"대상 선택 취소",162,370,146,Controller.CancelTargetSelection);
            for(int i=0;i<teamRows.Length;i++){int index=i;teamRows[i]=Button(r,"",12,412+i*38,296,()=>{if(visibleTeamIds[index]!=null)Controller.SelectOperationalTeam(visibleTeamIds[index]);});teamLabels[i]=teamRows[i].GetComponentInChildren<TMP_Text>();}
            detail=Text(r,"",12,106,296,66,13);
            follow=Button(r,"팀 추적",12,185,142,()=>Controller.FocusVehicle(Controller.SelectedOperationalTeamId));
            unfollow=Button(r,"추적 해제",162,185,146,()=>Controller.Station.Navigation.EndFollow());
            cancel=Button(r,"출발 전 취소",12,230,142,()=>Controller.CancelOperation(Controller.SelectedOperationalTeamId));
            release=Button(r,"지원 종료·복귀",162,230,146,()=>{foreach(var m in Controller.MissionSnapshots)if(m.TeamId==Controller.SelectedOperationalTeamId){if(m.State=="HandoffPending")Controller.ConfirmMedicalHandoffAndReturn(m.TeamId);else Controller.RequestReturn(m.TeamId);break;}});
            panel.SetActive(false);Controller.Workspace.ApplyAgencyContext();
        }
        private void RequestSupport(string work)
        {
            if(Controller.ContextKind=="station")Controller.RequestWork(stationSource,work,MvpAgencyDispatchController.StationTarget);
            else Controller.BeginTargetSelection(Controller.SelectedAgencyId,work);
        }
        private void Update()
        {
            if(panel==null)return;bool visible=Controller.HasContextSelection;panel.SetActive(visible);if(!visible)return;
            bool station=Controller.ContextKind=="station",team=Controller.ContextKind=="team";
            string source=station?stationSource:Controller.SelectedAgencyId;
            int ready=0,total=0,index=0;MvpAgencyDispatchController.MissionSnapshot selected=null;
            foreach(var m in Controller.MissionSnapshots)
            {
                if(m.TeamId==Controller.SelectedOperationalTeamId)selected=m;
                if(m.AgencyId!=source)continue;total++;if(m.State=="Ready")ready++;
                if(!team&&index<teamRows.Length){visibleTeamIds[index]=m.TeamId;teamRows[index].gameObject.SetActive(true);teamLabels[index].text=m.TeamLabel+" · "+MvpAgencyDispatchController.KoreanState(m.State);index++;}
            }
            for(int i=index;i<teamRows.Length;i++){visibleTeamIds[i]=null;teamRows[i].gameObject.SetActive(false);}
            title.text=station?"부산역":team?(selected?.TeamLabel??"대응팀"):Controller.AgencyLabel(source);
            resources.text=team?(selected==null?"팀 상태 확인 중":MvpAgencyDispatchController.KoreanState(selected.State)):EquipmentSummary(source,ready,total);
            ((RectTransform)resources.transform).sizeDelta=new Vector2(296,team?48:104);
            centralSource.gameObject.SetActive(station);choryangSource.gameObject.SetActive(station);
            centralSource.GetComponent<Image>().color=stationSource==MvpAgencyDispatchController.Central119?new Color(.08f,.4f,.4f):new Color(.08f,.22f,.28f);
            choryangSource.GetComponent<Image>().color=stationSource==MvpAgencyDispatchController.Choryang119?new Color(.08f,.4f,.4f):new Color(.08f,.22f,.28f);
            workEvac.gameObject.SetActive(!team&&total>0);workMedical.gameObject.SetActive(!team&&total>0);
            string evacuationReason,medicalReason;
            bool evacuationAvailable=Controller.CanRequestWork(source,"evacuation-support",out evacuationReason);
            bool medicalAvailable=Controller.CanRequestWork(source,"medical-support",out medicalReason);
            workEvac.interactable=evacuationAvailable;workMedical.interactable=medicalAvailable;
            instruction.gameObject.SetActive(!team);instruction.text=Controller.LastReason??(Controller.HasPendingTarget?"지원할 부산역 건물을 클릭하세요.":station?"이 역에 필요한 지원을 요청하세요.":total>0?"지원 요청을 누른 뒤 부산역 건물을 클릭하세요.":"건물을 선택해 위치와 이용 가능한 지원을 확인하세요.");
            if(!team&&total>0)
            {
                // Both reasons remain visible without clicking either disabled action.
                if(!evacuationAvailable)instruction.text+="\n대피: "+EquipmentReason(evacuationReason);
                if(!medicalAvailable)instruction.text+="\n의료: "+EquipmentReason(medicalReason);
            }
            inside.gameObject.SetActive(station);inside.GetComponentInChildren<TMP_Text>().text=Controller.Workspace.CurrentFloor==0?"내부 보기":"외부 보기";
            cancelTarget.gameObject.SetActive(!team&&Controller.HasPendingTarget);
            detail.gameObject.SetActive(team);detail.text=selected?.Reason??"";
            follow.gameObject.SetActive(team);unfollow.gameObject.SetActive(team);cancel.gameObject.SetActive(team);release.gameObject.SetActive(team);
            cancel.interactable=selected!=null&&selected.State=="Requested";
            release.interactable=selected!=null&&(selected.State=="HandoffPending"||selected.State=="WaitingIncident"||selected.State=="Standby");
            release.GetComponentInChildren<TMP_Text>().text=selected?.State=="HandoffPending"?"종료 확인·복귀":"지원 종료·복귀";
            ((RectTransform)panel.transform).sizeDelta=new Vector2(320,team?278:542);
        }
        private string EquipmentSummary(string agencyId,int ready,int total)
        {
            if(total==0)return "이 시설에서 요청할 수 있는 지원이 없습니다.";
            string agency=agencyId==MvpAgencyDispatchController.Central119?"중앙119":agencyId==MvpAgencyDispatchController.Choryang119?"초량119":Controller.AgencyLabel(agencyId);
            string summary=agency+" · 가용 대응팀 "+ready+" / "+total;
            var equipment=Controller.Workspace.GetComponent<MvpFacilityResources>();
            if(equipment==null||!equipment.Ready)return summary+"\n훈련장비 배정 정보를 기다리고 있습니다.";
            // Immutable snapshots only. Consumed is outstanding used equipment, not cumulative loss.
            foreach(var stock in equipment.Stocks)if(stock.AgencyId==agencyId)
            {
                string label=stock.WorkType=="evacuation-support"?"대피":stock.WorkType=="medical-support"?"의료":"지원";
                summary+="\n"+label+" · 가용 "+stock.Stock+" · 배정 "+stock.Committed+" · 사용 "+stock.Consumed;
            }
            return summary+"\n재사용 훈련장비 세트 · 사용분은 준비 후 회수\n훈련용 배정 수량 · 실제 기관 재고 아님";
        }
        private static string EquipmentReason(string reason)
        {
            if(string.IsNullOrEmpty(reason))return "요청 가능 조건을 확인 중입니다.";
            return reason.Replace("지원 꾸러미","훈련장비").Replace("꾸러미","장비 세트").Replace("보충","재출동 준비");
        }
        private TMP_Text Text(Transform parent,string text,float x,float y,float w,float h,float size){var g=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(parent,false);Rect((RectTransform)g.transform,x,y,w,h);var t=g.GetComponent<TextMeshProUGUI>();t.font=Controller.Workspace.Font;t.text=text;t.fontSize=size;t.color=Color.white;t.raycastTarget=false;t.overflowMode=TextOverflowModes.Ellipsis;return t;}
        private Button Button(Transform parent,string text,float x,float y,float width,UnityEngine.Events.UnityAction click){var g=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));g.transform.SetParent(parent,false);Rect((RectTransform)g.transform,x,y,width,30);g.GetComponent<Image>().color=new Color(.08f,.22f,.28f);var b=g.GetComponent<Button>();b.onClick.AddListener(click);var t=Text(g.transform,text,4,0,width-8,30,12);t.alignment=TextAlignmentOptions.Center;return b;}
        private static void Rect(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    }
}
