using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ChooGuard.Contracts;
using ChooGuard.Presentation.Commands;
using ChooGuard.Presentation.Input;
using ChooGuard.Presentation.Selection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Mvp
{
    public sealed class MvpWorkspace : MonoBehaviour
    {
        public CommandPreviewPresenter Preview;
        public InputContextRouter Router;
        public TMP_FontAsset Font;
        public Sprite TrainIcon, TeamIcon, LocationIcon;
        public RenderTexture StationTexture;
        public IReadOnlyList<MvpTeamView> TeamState => port != null ? port.Teams : Array.Empty<MvpTeamView>();
        public event Action<int> FloorChanged;
        public string SelectedTeamId=>team;
        public int SelectedCohort { get; private set; }
        public bool HasPendingTeamOrders=>commandBusy||teamOrders.Count>0;
        public int PlannedTeamOrders(string id){int count=0;foreach(var order in teamOrders)if(order.Team==id)count++;return count;}
        private sealed class TeamOrder { public string Team,Action,Target; }
        private readonly Queue<TeamOrder> teamOrders=new Queue<TeamOrder>();
        private bool commandBusy;
        private CommandIntent uncertainIntent;
        private bool commandsOpen,detailsOpen,debriefOpen;
        private sealed class OperationNotice { public string Key,Text,Team,Location; }
        private readonly HashSet<string> noticeKeys=new HashSet<string>();
        private readonly List<OperationNotice> notices=new List<OperationNotice>();
        public void ClearOperationNotices(){notices.Clear();noticeKeys.Clear();RefreshNotices();}
        public void AddOperationNotice(string key,string text,string teamId=null,string location=null)
        {
            if(!noticeKeys.Add(key))return;
            notices.Insert(0,new OperationNotice{Key=key,Text=text,Team=teamId,Location=location});if(notices.Count>3)notices.RemoveAt(3);RefreshNotices();
        }
        private void RefreshNotices()
        {
            if(transform.Find("WorkspaceCanvas")==null)return;
            for(int i=0;i<3;i++){var button=Find<Button>("Notice_"+i);button.gameObject.SetActive(i<notices.Count);if(i<notices.Count)button.GetComponentInChildren<TMP_Text>().text=notices[i].Text;}
        }
        private void FocusNotice(int index)
        {
            if(index>=notices.Count||(Router!=null&&Router.ActiveModal!=null))return;
            var notice=notices[index];
            var agency=GetComponent<MvpAgencyDispatchController>();
            if(agency!=null)
            {
                if(notice.Team!=null)foreach(var mission in agency.MissionSnapshots)if(mission.TeamId==notice.Team||mission.Channel==notice.Team){agency.SelectOperationalTeam(mission.TeamId);return;}
                agency.SelectStationContext();return;
            }
            if(notice.Team!=null)SelectTeam(notice.Team);
            var station=GetComponent<MvpTrainingDirector>()?.Station;if(station==null)return;
            if(notice.Location==null)station.FocusReferenceHall();else {station.SetFloor(notice.Location=="concourse"?2:1);station.Navigation?.Focus(station.transform.InverseTransformPoint(station.WorldAnchor(notice.Location)),22);}
        }
        public void SetDebriefDisplay(bool open,string timeline)
        {
            bool changed=debriefOpen!=open;debriefOpen=open;
            if(changed){detailsOpen=open;ApplyCompactLayout();}
            var label=Find<TMP_Text>("DebriefTimeline");
            if(label.text!=timeline){label.text=timeline;label.rectTransform.sizeDelta=new Vector2(0,Mathf.Max(180,label.GetPreferredValues(timeline,324,0).y+12));}
            Find<Button>("TrainingDebrief").GetComponentInChildren<TMP_Text>().text=open?"복기 닫기":"운영 복기";
            if(changed&&open)Find<ScrollRect>("DebriefScroll").verticalNormalizedPosition=0;
        }
        public void ShowOperationDetails(){detailsOpen=true;ApplyCompactLayout();}
        public void SetContextPhase(string phase)
        {
            var agency=GetComponent<MvpAgencyDispatchController>();bool available=agency==null||agency.CanCommandChannel(team);
            bool responsePhase=phase=="incident"||phase=="resolved";
            foreach(string id in new[]{"Move","Hold"})Find<Button>(id).interactable=available&&(phase=="ordinary"||responsePhase);
            foreach(string id in new[]{"CohortRelease","TrainingWarn","TrainingEvacuate","TrainingMedical","CohortHold"})Find<Button>(id).interactable=available&&responsePhase;
            var release=Find<Button>("CohortRelease");release.GetComponentInChildren<TMP_Text>().text=!available?"현장 도착 필요":responsePhase?"집단 유도":"사건 후 유도";
        }
        private void Place(string name,float x,float y,float width,float height,bool right=false,bool bottom=false)
        {
            var rect=Find<RectTransform>(name);rect.anchorMin=rect.anchorMax=new Vector2(right?1:0,bottom?0:1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);
            var button=rect.GetComponent<Button>();if(button!=null){var label=button.GetComponentInChildren<TMP_Text>();var child=label.rectTransform;child.anchorMin=Vector2.zero;child.anchorMax=Vector2.one;child.offsetMin=new Vector2(5,3);child.offsetMax=new Vector2(-5,-3);label.enableAutoSizing=true;label.fontSizeMin=11;label.fontSizeMax=15;}
        }
        private void Visible(string name,bool value)=>Find<RectTransform>(name).gameObject.SetActive(value);
        private void ApplyCompactLayout()
        {
            Place("SelectedTeamPanel",74,72,310,194);Place("SelectedTeamTitle",86,82,284,25);Place("SelectedTeamCondition",86,111,284,22);Place("SelectedTeamTask",86,137,284,48);Place("SelectionSummary",86,188,284,20);
            for(int i=0;i<3;i++)Place("Team_"+(i==0?"ops-1":i==1?"fire-1":"medical-1"),86+i*96,218,88,32);
            Place("Move",74,276,98,34);Place("Hold",180,276,98,34);Place("CohortRelease",286,276,98,34);Place("CommandDrawer",74,320,150,30);
            Place("StatusStrip",-374,-166,358,158,true,true);Place("MissionStatus",-362,-156,338,28,true,true);Place("MissionDetail",-362,-120,338,72,true,true);Place("TrainingPause",-362,-40,160,32,true,true);Place("TrainingSpeed",-194,-40,170,32,true,true);
            foreach(string id in new[]{"MissionPanel","MissionObjective","MissionProgressTrack","MissionProgress","ReferenceScope","Disclaimer","Subtitle","CameraHelp"})Visible(id,false);
            Place("DetailsPanel",-372,280,348,256,true);Place("CohortSummary",-360,294,324,180,true);Place("ScopeDetails",-360,490,148,30,true);Visible("DetailsPanel",detailsOpen);Visible("CohortSummary",detailsOpen&&!debriefOpen);Visible("ScopeDetails",detailsOpen);Place("DebriefScroll",-360,294,324,180,true);Visible("DebriefScroll",detailsOpen&&debriefOpen);
            Place("ActionsPanel",74,358,310,244);Visible("ActionsPanel",commandsOpen);
            string[] extra={"Target_platform","Target_concourse","Target_exit","Cohort_0","Cohort_1","Cohort_2","TrainingWarn","TrainingEvacuate","TrainingMedical","CohortHold","RecoverCommand"};
            for(int i=0;i<extra.Length;i++){Place(extra[i],86+(i%2)*146,370+(i/2)*36,138,30);Visible(extra[i],commandsOpen);}
            Visible("DestinationLabel",false);Place("Footer",74,612,310,52);Visible("Footer",commandsOpen);
            Place("TrainingIncident",-504,8,118,32,true);Place("TrainingDebrief",-376,8,110,32,true);Place("TrainingReplay",-256,8,130,32,true);Place("TrainingStart",-116,8,100,32,true);
            for(int i=0;i<3;i++)Place("Notice_"+i,-354,72+i*64,330,56,true);
            RefreshNotices();ApplyAgencyContext();
        }
        public void ApplyAgencyContext()
        {
            var canvas=transform.Find("WorkspaceCanvas");if(canvas==null)return;
            var agency=GetComponent<MvpAgencyDispatchController>();if(agency==null)return;
            // Building and logical team cards own their actions; the old global command HUD stays hidden.
            foreach(string id in new[]{"IconRail","SelectedTeamPanel","SelectedTeamTitle","SelectedTeamCondition","SelectedTeamTask","SelectionSummary","Team_ops-1","Team_fire-1","Team_medical-1","Move","Hold","CohortRelease","CommandDrawer","ActionsPanel","Target_platform","Target_concourse","Target_exit","Cohort_0","Cohort_1","Cohort_2","TrainingWarn","TrainingEvacuate","TrainingMedical","CohortHold","RecoverCommand","Footer","TrainingIncident","TrainingReplay","TrainingDebrief","ScopeDetails","DetailsPanel","CohortSummary","DebriefScroll","CameraCity","CameraStation","ProgressionGraphOverlay","JevForecastPanel"})
            {var item=canvas.Find(id);if(item!=null)item.gameObject.SetActive(false);}
            bool recovery=GetComponent<MvpTrainingDirector>()?.Phase=="resolved";
            var resultButton=canvas.Find("TrainingDebrief");if(resultButton!=null){resultButton.gameObject.SetActive(recovery);resultButton.GetComponentInChildren<TMP_Text>().text=debriefOpen?"복기 닫기":"대응 결과·복기";}
            foreach(string id in new[]{"DetailsPanel","DebriefScroll"}){var item=canvas.Find(id);if(item!=null)item.gameObject.SetActive(recovery&&debriefOpen);}
            bool stationInside=agency.ContextKind=="station"&&CurrentFloor!=0;
            for(int i=0;i<4;i++){var tab=canvas.Find("Floor_"+i);if(tab!=null)tab.gameObject.SetActive(stationInside);}
            var card=canvas.Find("AgencyDispatchPanel");if(card!=null)card.gameObject.SetActive(agency.HasContextSelection);
        }
        public void SelectCohort(int id) { SelectedCohort=Mathf.Clamp(id,0,2);for(int i=0;i<3;i++)Find<Button>("Cohort_"+i).GetComponent<Image>().color=i==SelectedCohort?new Color(.07f,.35f,.4f):panel;GetComponent<MvpTrainingDirector>()?.RefreshDisplay(); }
        public void SetCohortDisplay(string text) { Find<TMP_Text>("CohortSummary").text=text; }
        public void InlineNotice(string text) { if(footer==null)footer=Find<TMP_Text>("Footer");footer.text=text;footer.gameObject.SetActive(true); }
        public void CancelPlannedOrders() { teamOrders.Clear();InlineNotice("예약 지시 취소 · 이미 접수된 통제는 별도 지시로 변경하세요."); }
        public void ProcessPlannedOrders() { if(!commandBusy&&teamOrders.Count>0){var order=teamOrders.Dequeue();_ = IssueTeamCommand(order.Team,order.Action,order.Target,false);} }
        public async Task<bool> IssueTeamCommand(string id,string action,string destination,bool allowPlan=true,string operationId=null)
        {
            var agency=GetComponent<MvpAgencyDispatchController>();if(agency!=null&&!agency.CanCommandChannel(id,operationId)){InlineNotice(agency.AvailabilityReason(id));return false;}
            if(port==null)return false;if(uncertainIntent!=null){InlineNotice("이전 지시 접수 확인이 필요합니다. 처리결과 확인을 눌러 주세요.");return false;}var director=GetComponent<MvpTrainingDirector>();
            if(allowPlan&&((director!=null&&director.IsPaused)||commandBusy)){if(teamOrders.Count>=16){InlineNotice("예약 지시가 가득 찼습니다.");return false;}teamOrders.Enqueue(new TeamOrder{Team=id,Action=action,Target=destination});InlineNotice("전술 계획에 지시 예약 · 재개하면 검증 후 수행합니다.");return true;}
            if(commandBusy)return false;commandBusy=true;CommandIntent intent=null;
            try
            {
                intent=port.CreateIntent(id,action,destination);long revision=port.Revision;var preview=await port.PreviewAsync(intent,CancellationToken.None);
                if(!preview.Key.Equals(intent.Key)||port.Revision!=revision)throw new InvalidOperationException("상태가 바뀌었습니다. 다시 지시하세요.");
                foreach(var result in preview.TargetResults)if(result.Status!=TargetStatus.ACCEPTED)throw new InvalidOperationException(result.Reasons.Count>0?result.Reasons[0].Message:"이 지시는 실행할 수 없습니다.");
                CommandReceipt receipt;
                try { receipt=await port.SubmitAsync(intent,CancellationToken.None); }
                catch(IOException){var lookup=await port.ReadReceiptAsync(intent.Key,CancellationToken.None);if(!lookup.Found){uncertainIntent=intent;throw new InvalidOperationException("접수 여부 확인 필요 · 처리결과 확인을 눌러 주세요.");}receipt=lookup.Receipt;}
                var stored=await port.ReadReceiptAsync(intent.Key,CancellationToken.None);
                if(!stored.Found||!receipt.Key.Equals(intent.Key)||receipt.Fingerprint!=IntentFingerprint(intent)||stored.Receipt.Fingerprint!=receipt.Fingerprint||stored.Receipt.Status!=receipt.Status)throw new InvalidDataException("지시 처리결과가 일치하지 않습니다.");
                if(receipt.Status!=ReceiptStatus.ACCEPTED)throw new InvalidOperationException(receipt.TargetResults[0].Reasons.Count>0?receipt.TargetResults[0].Reasons[0].Message:"지시가 거절되었습니다.");
                uncertainIntent=null;Render();AddOperationNotice(intent.Key.IntentId.Value,"[접수] "+Korean(id)+" · "+Korean(destination)+"\n"+(action=="hold"?"위치 유지 지시":"이동 지시 · 도착 전"),id,destination);InlineNotice(Korean(id)+" · "+(action=="hold"?"대기 접수":"이동 접수")+" · 현장 도착/작업 완료와 구분");return true;
            }
            catch(Exception error){InlineNotice(error.Message);return false;}
            finally{commandBusy=false;}
        }
        public async void RecoverLastCommand()
        {
            if(uncertainIntent==null){InlineNotice("확인이 필요한 접수 지시가 없습니다.");return;}
            try{var lookup=await port.ReadReceiptAsync(uncertainIntent.Key,CancellationToken.None);if(!lookup.Found){InlineNotice("저장된 처리결과 없음 · 새 지시로 다시 시도할 수 있습니다.");uncertainIntent=null;return;}if(lookup.Receipt.Fingerprint!=IntentFingerprint(uncertainIntent))throw new InvalidDataException("처리결과 지문 불일치");uncertainIntent=null;Render();InlineNotice(lookup.Receipt.Status==ReceiptStatus.ACCEPTED?"이전 지시 접수 확인 · 중복 실행 없음":"이전 지시 거절 확인");}catch(Exception error){InlineNotice(error.Message);}
        }
        private static string IntentFingerprint(CommandIntent i)
        {
            using(var memory=new MemoryStream()){using(var w=new BinaryWriter(memory,Encoding.UTF8,true)){w.Write(i.Key.RunId.Value);w.Write(i.Key.RequesterId.Value);w.Write(i.Key.IntentId.Value);w.Write(i.ActingAgencyId.Value);w.Write(i.ActingTeamIds.Count);foreach(var t in i.ActingTeamIds)w.Write(t.Value);w.Write(i.ActionId.Value);w.Write(i.TargetIds.Count);foreach(var t in i.TargetIds)w.Write(t.Value);w.Write(i.ReadSet.Count);foreach(var r in i.ReadSet){w.Write(r.EntityId.Value);w.Write(r.Revision);}w.Write(i.PayloadRef.Id.Value);w.Write(i.PayloadRef.Revision);w.Write(i.PayloadRef.Sha256);}using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(memory.ToArray())).Replace("-","").ToLowerInvariant();}
        }
        public void SetSelectedTeamDisplay(string title,string condition,string task) { Find<TMP_Text>("SelectedTeamTitle").text=title;Find<TMP_Text>("SelectedTeamCondition").text=condition;Find<TMP_Text>("SelectedTeamTask").text=task; }
        public event Action TrainingStartRequested, TrainingPauseRequested, TrainingSpeedRequested;
        public event Action<string> TrainingActionRequested;
        public void SetTrainingDisplay(string status,string objective,string detail,float progress)
        {
            Find<TMP_Text>("MissionStatus").text=status;
            Find<TMP_Text>("MissionObjective").text=objective;
            Find<TMP_Text>("MissionDetail").text=detail;
            Find<Image>("MissionProgress").rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,260f*Mathf.Clamp01(progress));
            ApplyAgencyContext();
        }
        public int CurrentFloor = 0;
        [SerializeField] private int layoutVersion;
        public void SetStationTexture(RenderTexture texture)
        {
            StationTexture = texture;
            var canvas = transform.Find("WorkspaceCanvas");
            if (canvas != null && canvas.Find("StationViewport") != null)
                canvas.Find("StationViewport").GetComponent<RawImage>().texture = texture;
        }
        public void SelectFloor(int floor)
        {
            if (Router != null && Router.ActiveModal != null) return;
            CurrentFloor = floor;
            RefreshFloorTabs();
            FloorChanged?.Invoke(floor);ApplyAgencyContext();
        }
        private void RefreshFloorTabs()
        {
            for (int i=0;i<4;i++) Find<Button>("Floor_"+i).GetComponent<Image>().color = i==CurrentFloor ? new Color(.08f,.43f,.45f) : panel;
        }
        public static string Korean(string value)
        {
            switch(value) {
                case "ops-1": case "Operations": return "역무 훈련팀";
                case "fire-1": case "Fire": return "소방 훈련팀";
                case "medical-1": case "Medical": return "의료 훈련팀";
                case "platform": return "승강장"; case "concourse": return "대합실"; case "exit": return "출입구";
                case "Ready": return "대기"; case "Moved": return "이동 반영"; case "Holding": return "위치 유지";
                default: return value;
            }
        }
        private MvpOperationsPort port;
        private SelectionService selection;
        private string team = "ops-1", target = "platform";
        private long shownRevision = -1;
        private TMP_Text summary, footer;
        private readonly Color ink = new Color(.84f,.9f,.95f);
        private readonly Color panel = new Color(.015f,.075f,.11f,.88f);
        public void EnsureBuilt()
        {
            if(transform.Find("WorkspaceCanvas")!=null) { RefreshLayout();return; }
            var obj=new GameObject("WorkspaceCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));obj.transform.SetParent(transform,false);
            obj.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=obj.GetComponent<CanvasScaler>();scaler.referenceResolution=new Vector2(1440,900);scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.matchWidthOrHeight=.5f;
            var root=obj.transform;
            Box(root,"Background",0,0,1440,900,Color.clear);
            Box(root,"Topbar",0,0,1440,48,new Color(.015f,.05f,.075f,.82f));
            Icon(root,"TrainIcon",TrainIcon,18,12,26);Label(root,"Brand","부산 운영 관제",58,8,360,33,23);
            Label(root,"Subtitle","부산역 · 초량 · 중앙 · 남포 · 북항",438,17,450,22,14,new Color(.6f,.82f,.85f));
            Button(root,"TrainingIncident","사건 발생",766,8,130,38,panel);Button(root,"TrainingDebrief","운영 복기",626,8,130,38,panel);
            Button(root,"TrainingStart","새 운영",1266,8,150,38,new Color(.025f,.15f,.2f,.9f));Button(root,"TrainingReplay","같은 군중 재실행",1044,8,212,38,panel);Button(root,"TrainingMedical","의료 지원",906,8,128,38,panel);
            Box(root,"IconRail",14,98,44,318,new Color(.015f,.05f,.075f,.86f));
            for(int i=0;i<4;i++)Button(root,"Floor_"+i,i==0?"전체":i+"층",17,101+i*48,38,42,panel);
            Button(root,"CameraCity","도시",17,297,38,50,panel);Button(root,"CameraStation","역",17,354,38,50,panel);
            Box(root,"SelectedTeamPanel",74,98,310,272,new Color(.015f,.05f,.075f,.87f));
            Label(root,"SelectedTeamTitle","역무 훈련팀",88,111,282,33,22,new Color(.77f,.96f,1));
            Label(root,"SelectedTeamCondition","상태  지시 대기",88,155,282,29,14,new Color(.2f,.88f,.94f));
            Label(root,"SelectedTeamTask","작업  배치·대기 명령 선택",88,197,282,70,15,new Color(.68f,.84f,.87f));
            Label(root,"SelectionSummary","역무 훈련팀 / 승강장",88,274,282,22,13,new Color(.65f,.81f,.85f));
            string[] ids={"ops-1","fire-1","medical-1"};for(int i=0;i<3;i++)Button(root,"Team_"+ids[i],i==0?"역무":i==1?"소방":"의료",88+i*96,312,88,40,panel);
            Label(root,"CameraHelp","방향키 이동 · 휠 확대 · Q/E 회전\nSpace 계획/재개 · 1/2/3 팀 선택\n우클릭 지시 · Shift 예약 · F 현장",76,385,320,64,13,new Color(.7f,.84f,.88f));
            Box(root,"MissionPanel",1070,496,346,342,new Color(.015f,.05f,.075f,.88f));
            Label(root,"MissionStatus","현장 지휘 준비",1086,508,314,39,20,new Color(.6f,.95f,1));
            Label(root,"MissionObjective","멈춰서 계획하고 순서를 선택하세요.",1086,549,314,50,15);
            Label(root,"CohortSummary","초기 군중 집단을 불러오세요.",1086,607,314,80,12,new Color(.62f,.82f,.84f));
            Label(root,"MissionDetail","현장 대기",1086,696,314,88,13,new Color(.66f,.82f,.86f));
            Box(root,"MissionProgressTrack",1086,791,260,5,new Color(.11f,.24f,.28f));var progress=Rect(root,"MissionProgress",1086,791,0,5).gameObject.AddComponent<Image>();progress.color=new Color(.24f,.88f,.94f);progress.raycastTarget=false;
            Label(root,"ReferenceScope","국소 참조조건 · 현장 안전 판정 아님",1086,807,314,22,12,new Color(.62f,.76f,.8f));
            Button(root,"TrainingPause","일시 정지",1070,850,168,40,panel);Button(root,"TrainingSpeed","속도 변경",1248,850,168,40,panel);
            var viewport=Rect(root,"StationViewport",0,0,1,1).gameObject.AddComponent<RawImage>();viewport.raycastTarget=false;viewport.gameObject.SetActive(false);
            Box(root,"ActionsPanel",74,800,950,90,new Color(.015f,.05f,.075f,.88f));
            Label(root,"DestinationLabel","배치 목적지",88,811,110,23,14);
            Button(root,"Target_platform","승강장",208,805,114,32,panel);Button(root,"Target_concourse","대합실",332,805,114,32,panel);Button(root,"Target_exit","출입구",456,805,114,32,panel);
            for(int i=0;i<3;i++)Button(root,"Cohort_"+i,"집단 "+(i+1),598+i*138,805,128,32,panel);
            Button(root,"Move","팀 이동",88,847,118,32,panel);Button(root,"Hold","팀 대기",216,847,118,32,panel);Button(root,"TrainingWarn","경고 방송",344,847,130,32,panel);Button(root,"TrainingEvacuate","전체 대피",484,847,164,32,panel);Button(root,"CohortRelease","선택 집단 유도",658,847,174,32,panel);Button(root,"CohortHold","선택 집단 대기",842,847,166,32,panel);Button(root,"RecoverCommand","처리결과 확인",74,738,150,28,panel);Button(root,"ScopeDetails","계산 조건",1286,463,130,28,panel);
            Label(root,"Footer","팀 배치와 명령 접수는 이 기기에 저장됩니다.",76,770,935,20,12,new Color(.68f,.81f,.85f));
            Label(root,"Disclaimer","참조모델 실험",1086,440,314,18,12,new Color(.75f,.86f,.9f));
            Button(root,"CommandDrawer","추가 명령 +",74,320,150,30,panel);
            Box(root,"StatusStrip",1066,734,358,158,panel);root.Find("StatusStrip").SetSiblingIndex(root.Find("MissionStatus").GetSiblingIndex());
            Box(root,"DetailsPanel",1068,280,348,256,panel);
            // Details must remain behind the existing text and scope button.
            root.Find("DetailsPanel").SetSiblingIndex(root.Find("CohortSummary").GetSiblingIndex());
            for(int i=0;i<3;i++){Button(root,"Notice_"+i,"",1086,72+i*64,330,56,panel);var label=Find<Button>("Notice_"+i).GetComponentInChildren<TMP_Text>();label.fontSize=13;label.alignment=TextAlignmentOptions.MidlineLeft;}
            var scrollRect=Rect(root,"DebriefScroll",1080,294,324,180);
            var scrollBackground=scrollRect.gameObject.AddComponent<Image>();scrollBackground.color=new Color(0,0,0,.01f);
            scrollRect.gameObject.AddComponent<RectMask2D>();
            var scroll=scrollRect.gameObject.AddComponent<ScrollRect>();scroll.viewport=scrollRect;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            var timeline=Label(scrollRect,"DebriefTimeline","",0,0,324,180,13,new Color(.8f,.93f,.95f));
            timeline.rectTransform.anchorMin=new Vector2(0,1);timeline.rectTransform.anchorMax=Vector2.one;timeline.rectTransform.pivot=new Vector2(0,1);timeline.rectTransform.sizeDelta=new Vector2(0,180);timeline.raycastTarget=false;scroll.content=timeline.rectTransform;
            layoutVersion=11;RefreshFloorTabs();ApplyCompactLayout();
        }

        public void RefreshLayout()
        {
            var canvas = transform.Find("WorkspaceCanvas");
            if (canvas != null && layoutVersion < 11)
            {
                if (Preview != null) Preview.transform.SetParent(transform, false);
                if (UnityEngine.Application.isPlaying) { canvas.gameObject.SetActive(false); Destroy(canvas.gameObject); canvas.name="RetiredWorkspaceCanvas"; }
                else DestroyImmediate(canvas.gameObject);
                EnsureBuilt();
                canvas=transform.Find("WorkspaceCanvas");
                if(Preview != null) Preview.transform.SetParent(canvas, false);
            }
            if (canvas == null) return;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight=.5f;
            var background = canvas.Find("Background").GetComponent<RectTransform>();
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            foreach (var label in canvas.GetComponentsInChildren<TMP_Text>(true))
                if(Font!=null) label.font=Font;
            if(Preview!=null)
            {
                Preview.transform.SetAsLastSibling();
                Preview.Localize(Font);
            }
            SetStationTexture(StationTexture);ApplyCompactLayout();
            if (transform.Find("Workspace Camera") == null)
            {
                var cameraObject = new GameObject("Workspace Camera", typeof(Camera));
                cameraObject.transform.SetParent(transform, false);
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.028f,.045f,.072f);
                camera.cullingMask = 0;
                camera.depth = -10;
            }
        }
        private void Awake()
        {
            EnsureBuilt();
            RefreshLayout();
            if (!UnityEngine.Application.isPlaying) return;
            try
            {
                port = new MvpOperationsPort(Path.Combine(UnityEngine.Application.persistentDataPath,"ChooGuardMvp"));
                selection = new SelectionService(new Authority(port));
                summary = Find<TMP_Text>("SelectionSummary"); footer = Find<TMP_Text>("Footer");
                foreach(var view in port.Teams) { var id=view.Id; Find<Button>("Team_"+id).onClick.AddListener(()=>SelectTeam(id)); }
                foreach(var idValue in MvpOperationsPort.TargetIds) { var id=idValue; Find<Button>("Target_"+id).onClick.AddListener(()=>SelectTarget(id)); }
                for(int i=0;i<4;i++) { var floor=i; Find<Button>("Floor_"+i).onClick.AddListener(()=>SelectFloor(floor)); }
                Find<Button>("TrainingStart").onClick.AddListener(()=> { if(Router.ActiveModal==null) TrainingStartRequested?.Invoke(); });
                Find<Button>("TrainingPause").onClick.AddListener(()=> { if(Router.ActiveModal==null) TrainingPauseRequested?.Invoke(); });
                Find<Button>("TrainingSpeed").onClick.AddListener(()=> { if(Router.ActiveModal==null) TrainingSpeedRequested?.Invoke(); });
                Find<Button>("TrainingWarn").onClick.AddListener(()=> { if(Router.ActiveModal==null) TrainingActionRequested?.Invoke("warn"); });
                Find<Button>("TrainingEvacuate").onClick.AddListener(()=> { if(Router.ActiveModal==null) TrainingActionRequested?.Invoke("evacuate"); });
                Find<Button>("TrainingMedical").onClick.AddListener(()=> { if(Router.ActiveModal==null) TrainingActionRequested?.Invoke("medical"); });
                Find<Button>("TrainingIncident").onClick.AddListener(()=> {if(Router.ActiveModal==null)TrainingActionRequested?.Invoke("incident");});Find<Button>("TrainingDebrief").onClick.AddListener(()=>TrainingActionRequested?.Invoke("debrief"));
                Find<Button>("TrainingReplay").onClick.AddListener(()=>TrainingActionRequested?.Invoke("replay"));Find<Button>("ScopeDetails").onClick.AddListener(()=>TrainingActionRequested?.Invoke("scope"));Find<Button>("CohortRelease").onClick.AddListener(()=>TrainingActionRequested?.Invoke("cohort_release"));Find<Button>("CohortHold").onClick.AddListener(()=>TrainingActionRequested?.Invoke("cohort_hold"));Find<Button>("RecoverCommand").onClick.AddListener(RecoverLastCommand);for(int i=0;i<3;i++){int id=i;Find<Button>("Cohort_"+i).onClick.AddListener(()=>SelectCohort(id));}
                Find<Button>("CameraCity").onClick.AddListener(()=> { var d=GetComponent<MvpTrainingDirector>();if(d!=null&&d.Station!=null)d.Station.FocusCity(); });
                Find<Button>("CameraStation").onClick.AddListener(()=>SelectFloor(0));
                Find<Button>("Move").onClick.AddListener(()=>BeginCommand("move"));
                Find<Button>("Hold").onClick.AddListener(()=>BeginCommand("hold"));
                Find<Button>("CommandDrawer").onClick.AddListener(()=>{commandsOpen=!commandsOpen;Find<Button>("CommandDrawer").GetComponentInChildren<TMP_Text>().text=commandsOpen?"추가 명령 -":"추가 명령 +";ApplyCompactLayout();});
                for(int i=0;i<3;i++){int slot=i;Find<Button>("Notice_"+i).onClick.AddListener(()=>FocusNotice(slot));}
                Preview.Bind(port,selection,Router); Preview.gameObject.SetActive(false);
                SelectTeam(team); Render();
            }
            catch(Exception error) { Debug.LogException(error,this); var text=Find<TMP_Text>("Footer"); text.text="훈련 세션을 열 수 없습니다. 저장 상태를 확인해 주세요."; }
        }
        public void SelectTeam(string id)
        {
            if(port==null || Router.ActiveModal!=null) return;
            team=id; selection.Replace(new[]{new StableId(id)},new StableId("synthetic-floor")); Render();SetContextPhase(GetComponent<MvpTrainingDirector>()?.Phase??"ready");
        }
        public void SelectTarget(string id) { if(port==null || Router.ActiveModal!=null) return; target=id; Render(); }
        public void BeginCommand(string action) { if(Router!=null&&Router.ActiveModal!=null)return;_ = IssueTeamCommand(team,action,target); }
        private void Update()
        {
            if(port==null) return;
            if(shownRevision!=port.Revision) { if(Preview.isActiveAndEnabled) Preview.ObserveRevision(port.Revision); Render(); }
        }
        private void Render()
        {
            shownRevision=port.Revision;
            summary.text=Korean(team)+"  /  "+Korean(target);
            foreach(var view in port.Teams)
            {
                var button=Find<Button>("Team_"+view.Id);
                button.GetComponent<Image>().color=view.Id==team?new Color(.09f,.39f,.43f):new Color(.12f,.19f,.25f);
                button.GetComponentInChildren<TMP_Text>().text=view.Id=="ops-1"?"역무":view.Id=="fire-1"?"소방":"의료";
            }
            foreach(var id in MvpOperationsPort.TargetIds) Find<Button>("Target_"+id).GetComponent<Image>().color=id==target?new Color(.12f,.43f,.46f):new Color(.12f,.22f,.29f);
            footer.text="가상 훈련 상태  ·  변경 "+port.Revision+"회  ·  팀 상태와 명령 처리결과를 이 기기에 저장합니다.";
        }
        private void OnDestroy() { port?.Dispose(); }
        private T Find<T>(string name) where T:Component
        { foreach(var item in GetComponentsInChildren<T>(true)) if(item.name==name) return item; throw new InvalidOperationException("Missing UI: "+name); }
        private static readonly HashSet<string> RightBottomHud = new HashSet<string>
        {
            "MissionPanel","MissionStatus","MissionObjective","CohortSummary","MissionDetail",
            "MissionProgressTrack","MissionProgress","ReferenceScope","ScopeDetails","Disclaimer",
            "TrainingPause","TrainingSpeed"
        };
        private static readonly HashSet<string> RightTopHud = new HashSet<string>
        {
            "TrainingStart","TrainingReplay","TrainingMedical","TrainingIncident","TrainingDebrief"
        };
        private static readonly HashSet<string> LeftBottomHud = new HashSet<string>
        {
            "ActionsPanel","DestinationLabel","Target_platform","Target_concourse","Target_exit",
            "Cohort_0","Cohort_1","Cohort_2","Move","Hold","TrainingWarn","TrainingEvacuate",
            "CohortRelease","CohortHold","RecoverCommand","Footer"
        };
        private RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var obj=new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(parent,false);
            var rect=obj.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1); rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h);
            if(parent.name=="WorkspaceCanvas")
            {
                // Group membership, never reference-coordinate thresholds, selects the anchor.
                if(RightBottomHud.Contains(name)) { rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.anchoredPosition=new Vector2(x-1440,900-y); }
                else if(RightTopHud.Contains(name)) { rect.anchorMin=rect.anchorMax=Vector2.one;rect.anchoredPosition=new Vector2(x-1440,-y); }
                else if(LeftBottomHud.Contains(name)) { rect.anchorMin=rect.anchorMax=Vector2.zero;rect.anchoredPosition=new Vector2(x,900-y); }
                else if(name=="Topbar") { rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.sizeDelta=new Vector2(0,h); }
                
            }
            return rect;
        }
        private void Icon(Transform parent,string name,Sprite sprite,float x,float y,float size)
        { if(sprite==null) return; var image=Rect(parent,name,x,y,size,size).gameObject.AddComponent<Image>(); image.sprite=sprite; image.color=ink; image.raycastTarget=false; }
        private void Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        { var rect=Rect(parent,name,x,y,w,h); var img=rect.gameObject.AddComponent<Image>(); img.color=color; img.raycastTarget=name!="Background"&&name!="MissionProgressTrack"; if(name=="SelectedTeamPanel"||name=="MissionPanel"||name=="ActionsPanel"||name=="IconRail") { var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.15f,.6f,.7f,.7f);outline.effectDistance=new Vector2(1,-1); } }
        private TMP_Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int size,Color? color=null)
        {
            var rect=Rect(parent,name,x,y,w,h); var label=rect.gameObject.AddComponent<TextMeshProUGUI>(); if(Font!=null) label.font=Font;
            label.text=text; label.fontSize=size; label.color=color??ink; label.raycastTarget=false; label.enableWordWrapping=true; return label;
        }
        private void Button(Transform parent,string name,string text,float x,float y,float w,float h,Color color)
        {
            var rect=Rect(parent,name,x,y,w,h); var image=rect.gameObject.AddComponent<Image>(); image.color=color;
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.13f,.59f,.68f,.75f);outline.effectDistance=new Vector2(1,-1);
            var inset=4; var label=Label(rect,"Caption",text,4,inset,w-8,h-inset*2,w<60?13:15); label.alignment=TextAlignmentOptions.Center;
        }
        private sealed class Authority:ISelectionAuthority
        {
            private readonly MvpOperationsPort port;
            public Authority(MvpOperationsPort port) { this.port=port; }
            public bool TryRead(StableId id,out SelectionEntity entity)
            {
                foreach(var view in port.Teams) if(view.Id==id.Value)
                {
                    string hash;
                    using(var sha=SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(view.Id+"|"+view.AgencyId+"|"+view.LocationId+"|"+view.Status))).Replace("-", "").ToLowerInvariant();
                    entity=new SelectionEntity(id,new StableId("synthetic-floor"),true,true,new ContentReference(new StableId("synthetic-training"),port.Revision,hash),new StableId(view.AgencyId)); return true;
                }
                entity=null; return false;
            }
        }
    }
}
