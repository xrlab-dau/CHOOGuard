using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace ChooGuard.Foundation.Demo
{
    [DefaultExecutionOrder(50)]
    public sealed class StationWorldController : MonoBehaviour
    {
        [SerializeField] private TextAsset worldProfile;
        [SerializeField] private TextAsset roleProfile;
        [SerializeField] private DemoGameController legacy;
        [SerializeField] private DemoPlayerController player;
        [SerializeField] private DemoEvacuee[] crowd;
        [SerializeField] private StationPortal[] portals;
        [SerializeField] private DemoWalkGraph graph;
        [SerializeField] private DemoWorldGuide guide;
        [SerializeField] private DemoInteractable[] equipment;
        [SerializeField] private Transform assembly;
        [SerializeField] private Transform spawn;
        private StationWorldConfig config;
        private ScenarioProfile roles;
        private string roleId="role-01";
        private bool roleActionObserved;
        private string appliedIncident;
        private StationPhase appliedPhase;
        private bool crossedChosenRoute;
        private bool approachedChosenRoute;
        private string shiftId;
        private double accumulator;
        private string contextFeedback;
        private Transform contextObject;
        private float feedbackRemaining;
        private int guideRevision=-1;
        private GUIStyle bodyStyle,titleStyle;
        private Font menuFont;
        private Vector2 scroll;
        private bool showDebrief;
        private string configurationError;
        public StationWorldSession Session {get;private set;}
        public TrainingSession RoleSession {get;private set;}
        public bool IsRunning {get;private set;}
        public bool LegacyOverride {get;private set;}
        public bool IsConfigured {get{return config!=null&&configurationError==null;}}
        public IReadOnlyList<StationPortal> Portals {get{return portals;}}
        public IReadOnlyList<DemoEvacuee> Crowd {get{return crowd;}}
        public DemoPlayerController Player {get{return player;}}
        public bool CrossedChosenRoute {get{return crossedChosenRoute;}}
        public void Configure(TextAsset profile,TextAsset core,DemoGameController guided,DemoPlayerController actor,DemoEvacuee[] people,StationPortal[] routes,DemoWalkGraph walk,DemoWorldGuide navigation,DemoInteractable[] devices,Transform muster,Transform start)
        {worldProfile=profile;roleProfile=core;legacy=guided;player=actor;crowd=people;portals=routes;graph=walk;guide=navigation;equipment=devices;assembly=muster;spawn=start;}
        private void Start()
        {
            try
            {
                config=JsonUtility.FromJson<StationWorldConfig>(worldProfile.text);config.Validate();roles=JsonUtility.FromJson<ScenarioProfile>(roleProfile.text);ScenarioValidation.Validate(roles);
                if(portals==null||portals.Length!=3||portals.Any(x=>x==null)||!portals.Select(x=>x.SiteId).OrderBy(x=>x).SequenceEqual(config.sites.Select(x=>x.id).OrderBy(x=>x))||crowd==null||crowd.Length!=6||crowd.Any(x=>x==null||!x.isActiveAndEnabled)||crowd.Select(x=>x.NpcId).Distinct().Count()!=6||config.routineStops==null||config.routineStops.Length<3)throw new InvalidOperationException("World routes, routine destinations and six unique NPCs are required.");
                if(!crowd.Select(x=>x.NpcId).OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,6).Select(i=>"npc-"+i)))throw new InvalidOperationException("Expected NPC IDs npc-0 through npc-5.");
                DemoGameController.ValidateCrowd(crowd,assembly.position);
                foreach(var binding in config.equipment)
                {
                    var target=equipment.Single(x=>x.AnchorId==binding.anchorId);
                    if(Vector3.Distance(target.transform.position,binding.position)>.02f)throw new InvalidOperationException("Synthetic binding differs from built geometry: "+binding.entityId);
                }
                player.SetControlEnabled(false);guide.Hide();
            }
            catch(Exception error){configurationError=error.Message;Debug.LogError("Station world: "+error.Message,this);}
        }
        public void StartShift(int seed,string selectedRole="role-01")
        {
            if(!IsConfigured)throw new InvalidOperationException(configurationError??"World has not initialized.");
            if(!roles.roles.Any(x=>x.roleId==selectedRole))throw new ArgumentException("Unknown role.");
            if(IsRunning&&!LegacyOverride)SaveRecord();
            shiftId=Guid.NewGuid().ToString("N");
            LegacyOverride=false;roleId=selectedRole;Session=new StationWorldSession(config,seed);IsRunning=true;accumulator=0;appliedIncident=null;appliedPhase=StationPhase.Ordinary;
            contextFeedback=null;feedbackRemaining=0;showDebrief=false;crossedChosenRoute=false;guide.Hide();
            foreach(var target in equipment)target.ResetVisual();
            foreach(var portal in portals)portal.SetState(false,false,false);
            foreach(var npc in crowd){npc.ResetActor();npc.ConfigureRoutine(config.routineStops);}
            player.Teleport(spawn);player.SetControlEnabled(true);graph.SetRestrictedAreas(new Bounds[0]);
            Session.Record("shift-start","station",true,"seed="+seed+"; profile="+config.version+"; role="+roleId+"; synthetic-unverified");
        }
        public void EnableLegacyRegression()
        {
            LegacyOverride=true;IsRunning=false;guide.Hide();
            foreach(var portal in portals)portal.SetState(false,false,false);
            graph.SetRestrictedAreas(new Bounds[0]);foreach(var npc in crowd)npc.DisableRoutine();
        }
        public void PauseWorld(){if(!IsRunning)return;Session.Paused=true;player.SetControlEnabled(false);}
        public void ResumeWorld(){if(!IsRunning||!player.InputAvailable)return;Session.Paused=false;showDebrief=false;player.SetControlEnabled(true);}
        private IncidentCandidate[] Candidates()
        {
            var positions=crowd.Select(x=>x.transform.position).Concat(new[]{player.transform.position}).ToArray();
            return portals.Where(p=>p.CanActivate(positions)).Select(p=>
            {
                var nearest=crowd.Where(x=>x.transform.position.z<p.Waypoint.z-.8f).OrderBy(x=>DemoEvacuee.Horizontal(x.transform.position,p.Waypoint)).ToArray();
                if(nearest.Length<2)return null;
                var affected=nearest.Where(x=>DemoEvacuee.Horizontal(x.transform.position,p.Waypoint)<10).ToArray();
                if(affected.Length<2)affected=nearest.Take(2).ToArray();
                return new IncidentCandidate(p.SiteId,affected.Select(x=>x.NpcId).OrderBy(x=>x,StringComparer.Ordinal).ToArray());
            }).Where(x=>x!=null).ToArray();
        }
        public void TickWorld(float delta)
        {
            if(!IsRunning||LegacyOverride||Session.Paused)return;
            if(float.IsNaN(delta)||float.IsInfinity(delta)||delta<0)throw new ArgumentException("Invalid world time.");
            if(crowd.Any(x=>x==null||!x.isActiveAndEnabled)){PauseWorld();Session.Record("world-incomplete","crowd",false,"required actor unavailable");return;}
            accumulator+=delta;
            while(accumulator>=.05)
            {
                accumulator-=.05;Session.Tick(.05,Candidates());ProjectState();
                var incident=Session.Active;
                if(Session.Phase==StationPhase.Incident&&incident.SelectedRoute!=null&&!crossedChosenRoute)
                {
                    var route=portals.Single(x=>x.SiteId==incident.SelectedRoute);
                    if(route.TrackCrossing(player.transform.position,ref approachedChosenRoute))
                    {
                        crossedChosenRoute=true;Session.Record("leader-route-crossing",route.SiteId,true,"physical waypoint");
                        foreach(var npc in crowd.Where(x=>incident.IsRecruited(x.NpcId)))npc.LeaderCrossedPortal();
                    }
                }
                foreach(var npc in crowd)npc.Tick(.05f,player.transform,graph,assembly.position);
                if(Session.Phase==StationPhase.Incident)
                {
                    foreach(var npc in crowd.Where(x=>incident.IsRecruited(x.NpcId)&&x.State==EvacueeState.AssemblyWait&&x.EscortPortalVisited))
                        if(!incident.arrived.Contains(npc.NpcId)&&PhysicalArrival(npc))Session.Act(incident.Id,Session.Revision,StationAction.RecordArrival,npc.NpcId);
                }
                feedbackRemaining=Mathf.Max(0,feedbackRemaining-.05f);
            }
            DiscoverVisibleSignal();UpdateNavigation(delta);
        }
        private bool PhysicalArrival(DemoEvacuee npc)
        {return npc.isActiveAndEnabled&& DemoEvacuee.Horizontal(npc.transform.position,npc.WaitingSlot)<.45f&&Mathf.Abs(npc.transform.position.y-assembly.position.y)<.5f&&DemoEvacuee.Horizontal(npc.transform.position,assembly.position)<2;}
        private void ProjectState()
        {
            var current=Session.Active;
            if(Session.Phase==StationPhase.Incident&&current.Id!=appliedIncident)
            {
                appliedIncident=current.Id;crossedChosenRoute=false;approachedChosenRoute=false;roleActionObserved=false;
                RoleSession=new TrainingSession(roles);RoleSession.SelectRole(roleId);RoleSession.AcknowledgeBriefing();
                foreach(var p in portals)p.SetState(p.SiteId==current.SiteId,current.Kind==StationIncidentKind.PassageObstruction,current.Escalated);
                var affectedPortal=portals.Single(x=>x.SiteId==current.SiteId);
                graph.SetRestrictedAreas(new[]{affectedPortal.ExclusionBounds});guide.InvalidateRoute();
                foreach(var npc in crowd)
                {
                    npc.InvalidateRoute();if(current.AffectedNpcIds.Contains(npc.NpcId))npc.AwaitLeader();
                    Session.Record("actor-at-onset",npc.NpcId,true,npc.transform.position.ToString("F3"));
                }
                Session.Record("actor-at-onset","player",true,player.transform.position.ToString("F3"));
            }
            if(Session.Phase==StationPhase.Incident)
            {
                portals.Single(x=>x.SiteId==current.SiteId).SetState(true,current.Kind==StationIncidentKind.PassageObstruction,current.Escalated);
            }
            if(appliedPhase==StationPhase.Recovery&&Session.Phase==StationPhase.Ordinary)
            {
                foreach(var p in portals)p.SetState(false,false,false);
                graph.SetRestrictedAreas(new Bounds[0]);foreach(var npc in crowd)npc.ResumeRoutine();guide.Hide();
                foreach(var target in equipment)target.ResetVisual();appliedIncident=null;
            }
            if(appliedPhase!=StationPhase.Recovery&&Session.Phase==StationPhase.Recovery)
            {guide.Hide();Session.History.Last().roleActionObserved=roleActionObserved;SaveRecord();}
            appliedPhase=Session.Phase;
            if(guideRevision!=Session.Revision){guideRevision=Session.Revision;ProjectEquipment();}
        }
        public string EquipmentState(string anchor)
        {
            if(Session==null||Session.Phase==StationPhase.Ordinary)return "normal";
            var incident=Session.Active;
            if(anchor=="anchor-03"&&incident.ReportAnchor!="anchor-03")return "offline";
            if(anchor=="anchor-01")return "indication";
            if(anchor=="anchor-02"&&incident.Notified)return "notice-active";
            if(anchor==incident.ReportAnchor&&incident.Reported)return "reported";
            return "normal";
        }
        private void ProjectEquipment()
        {
            foreach(var target in equipment)
            {
                var state=EquipmentState(target.AnchorId);
                if(state=="offline")target.ShowState(new Color(.8f,.13f,.08f));
                else if(state=="indication")target.ShowState(new Color(1,.65f,.10f));
                else if(state=="reported"||state=="notice-active")target.ShowState(new Color(.1f,.9f,.4f));
            }
        }
        private void DiscoverVisibleSignal()
        {
            if(Session.Phase!=StationPhase.Incident||Session.Active.Discovered)return;
            var portal=portals.Single(x=>x.SiteId==Session.Active.SiteId);var point=portal.Cue.position+Vector3.up*.1f;
            var delta=point-player.ViewCamera.transform.position;
            if(delta.magnitude>10||Vector3.Dot(delta.normalized,player.ViewCamera.transform.forward)<.7f)return;
            RaycastHit hit;
            if(Physics.Raycast(player.ViewCamera.transform.position,delta.normalized,out hit,delta.magnitude+.4f,1,QueryTriggerInteraction.Ignore)&&hit.collider.transform.IsChildOf(portal.Cue))
                Session.Act(Session.Active.Id,Session.Revision,StationAction.Observe,"signal-"+portal.SiteId);
        }
        private bool Aim(out DemoInteractable device,out DemoEvacuee person)
        {
            device=null;person=null;RaycastHit hit;
            if(!Physics.Raycast(new Ray(player.ViewCamera.transform.position,player.ViewCamera.transform.forward),out hit,player.InteractionReach,~0,QueryTriggerInteraction.Collide))return false;
            device=hit.collider.GetComponentInParent<DemoInteractable>();person=hit.collider.GetComponentInParent<DemoEvacuee>();
            return (device!=null&&device.isActiveAndEnabled&&equipment.Contains(device))||(person!=null&&person.isActiveAndEnabled&&crowd.Contains(person));
        }
        public bool TryInteract(InputModality modality=InputModality.Desktop)
        {
            if(!IsRunning||LegacyOverride||Session.Paused)return false;
            DemoInteractable device;DemoEvacuee person;if(!Aim(out device,out person)){Session.Record("interaction-miss","",false,"no registered visible object within reach");return false;}
            var active=Session.Active;
            contextObject=person!=null?person.transform:device.transform;feedbackRemaining=3;
            if(Session.Phase!=StationPhase.Incident)
            {
                contextFeedback=Session.Phase==StationPhase.Recovery?"상황 종료 · 운영 복구 중":"정상 운영";
                Session.Record("routine-inspection",person!=null?person.NpcId:device.AnchorId,true,contextFeedback);return true;
            }
            var id=person!=null?person.NpcId:device.AnchorId;
            StationAction action;
            if(person!=null)action=StationAction.Recruit;
            else if(id=="anchor-01"||id.StartsWith("signal-"))action=StationAction.Observe;
            else if(id=="anchor-03"||id=="route-console")action=StationAction.Report;
            else if(id=="anchor-02")action=StationAction.Notify;
            else if(id.StartsWith("choose-")){action=StationAction.SelectRoute;id=id.Substring(7);}
            else if(id=="assembly-register")
            {
                if(!crossedChosenRoute||DemoEvacuee.Horizontal(player.transform.position,assembly.position)>2||Mathf.Abs(player.transform.position.y-assembly.position.y)>.5f||crowd.Where(x=>active.AffectedNpcIds.Contains(x.NpcId)).Any(x=>!PhysicalArrival(x)))
                {contextFeedback="선택 경로 통과 · 인솔 인원 도착 확인 필요";Session.Record("close-rejected",id,false,contextFeedback);return false;}
                action=StationAction.CloseIncident;
            }
            else
            {
                contextFeedback=id=="rally-west"||id=="rally-east"?"인솔 대상에게 가까이 다가가 E":"현장 표지 · 사용 가능 경로 확인";
                Session.Record("device-inspected",id,true,contextFeedback);TryRoleAction(device.AnchorId,modality);return true;
            }
            var receipt=Session.Act(active.Id,Session.Revision,action,id);contextFeedback=receipt.Reason;
            if(receipt.Accepted)
            {
                if(person!=null)
                {
                    person.Recruit();person.SetEscortPortal(portals.Single(x=>x.SiteId==active.SelectedRoute).Waypoint);
                    if(crossedChosenRoute)person.LeaderCrossedPortal();
                }
                else
                {
                    TryRoleAction(device.AnchorId,modality);
                    if(action==StationAction.Observe)contextFeedback=portals.Single(x=>x.SiteId==active.SiteId).Label+" · "+(active.Kind==StationIncidentKind.PassageObstruction?"통행 제한":"안내·통신 이상");
                    else if(action==StationAction.SelectRoute){crossedChosenRoute=false;approachedChosenRoute=false;contextFeedback="선택 경로 · "+portals.Single(x=>x.SiteId==active.SelectedRoute).Label;}
                    else if(action==StationAction.CloseIncident)contextFeedback="인솔 기록 완료 · 현장 운영 복구";
                }
                var hands=player.GetComponentInChildren<DemoHands>();if(hands!=null)hands.Pulse();ProjectState();
            }
            else if(device!=null&&id=="anchor-01"&&active.Discovered)TryRoleAction(device.AnchorId,modality);
            return receipt.Accepted;
        }
        private void TryRoleAction(string anchor,InputModality modality)
        {
            if(roleActionObserved||RoleSession==null)return;var fixture=RoleSession.CreateRoleActionFixture();if(anchor!=fixture.targetAnchorId)return;
            var action=new TrainingAction(Guid.NewGuid().ToString("N"),fixture.scenarioVersion,fixture.roleId,fixture.preStateHash,fixture.actionId,anchor,modality);
            roleActionObserved=RoleSession.Submit(in action).Accepted;Session.Record("role-action",anchor,roleActionObserved,"selected role contract only; other roles receive notifications");
        }
        private void UpdateNavigation(float dt)
        {
            var incident=Session.Phase==StationPhase.Incident?Session.Active:null;
            if(incident==null||!incident.Discovered||incident.SelectedRoute==null)guide.Hide();
            if(incident!=null&&incident.Discovered&&incident.SelectedRoute!=null)
            {
                var route=portals.Single(x=>x.SiteId==incident.SelectedRoute);
                var destination=crossedChosenRoute?assembly.position:route.Waypoint;
                guide.Tick(dt,graph,player,"selected-"+route.SiteId,destination,destination,crossedChosenRoute?"집결 지점":"선택 경로 · "+route.Label);
            }
            DemoInteractable device;DemoEvacuee person;
            if(feedbackRemaining>0&&contextObject!=null)guide.ShowContext(player.ViewCamera,contextObject.position,contextFeedback);
            else if(Aim(out device,out person))
            {
                var caption=person!=null?(incident!=null&&incident.AffectedNpcIds.Contains(person.NpcId)?"[E] 인솔 · 함께 이동":"이용객"):
                    device.AnchorId.StartsWith("choose-")?"[E] "+device.DisplayName:"[E] "+device.DisplayName+" · "+StateLabel(EquipmentState(device.AnchorId));
                guide.ShowContext(player.ViewCamera,person!=null?person.transform.position+Vector3.up:device.InteractionPoint,caption);
            }
        }
        private string DisplayTarget(string id)
        {
            var portal=portals.FirstOrDefault(x=>x.SiteId==id);if(portal!=null)return portal.Label;
            var device=equipment.FirstOrDefault(x=>x.AnchorId==id);if(device!=null)return device.DisplayName;
            if(id!=null&&id.StartsWith("npc-")){int number;if(int.TryParse(id.Substring(4),out number))return "이용객 "+(number+1);}
            return string.IsNullOrEmpty(id)?"현장":id;
        }
        private static string ActionLabel(string action)
        {switch(action){case "onset":return "상황 발생";case "Observe":return "상태 관측";case "Report":return "상황 보고";case "Notify":return "안내 전파";case "SelectRoute":return "경로 선택";case "Recruit":return "인솔 시작";case "RecordArrival":return "인원 도착";case "CloseIncident":return "집결 확인";case "escalated":return "통신 상태 변화";case "leader-route-crossing":return "통로 통과";default:return null;}}
        private static string StateLabel(string state)
        {switch(state){case "offline":return "통신 불가";case "indication":return "이상 신호";case "reported":return "보고 기록";case "notice-active":return "안내 전파";default:return "정상";}}
        private void Update()
        {
            if(LegacyOverride||!IsRunning)return;
            if(!player.InputAvailable){PauseWorld();return;}
            if(DemoInput.EscapePressed){if(Session.Paused)ResumeWorld();else PauseWorld();return;}
            if(Session.Paused)return;
            TickWorld(Time.deltaTime);if(DemoInput.InteractPressed)TryInteract();
            if(player.transform.position.y<-8){PauseWorld();Session.Record("out-of-bounds","player",false,"world paused; restart required");}
        }
        private void OnApplicationFocus(bool focus){if(!focus&&!LegacyOverride)PauseWorld();}
        private void OnDisable(){if(Application.isPlaying&&player!=null)player.SetControlEnabled(false);}
        [Serializable] private sealed class RecordFile {public string shiftId;public string version;public string engineVersion;public string randomGenerator;public StationWorldConfig configuration;public string evidenceStatus;public int seed;public StationDebrief[] episodes;public StationTrace[] events;}
        private void SaveRecord()
        {
            if(Session==null)return;
            try
            {
                var directory=Path.Combine(Application.persistentDataPath,"StationSessions");Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,"shift-"+shiftId+".json"),JsonUtility.ToJson(new RecordFile{shiftId=shiftId,version=config.version,engineVersion=Application.unityVersion,randomGenerator="xorshift32-avalanche-v1",configuration=config,evidenceStatus=config.evidenceStatus,seed=Session.Seed,episodes=Session.History.ToArray(),events=Session.Trace.ToArray()},true));
            }
            catch(IOException error){Debug.LogWarning("Local training record could not be saved: "+error.GetType().Name);}
        }
        private void OnApplicationQuit(){if(IsRunning)SaveRecord();}
        private void Styles()
        {
            if(bodyStyle!=null)return;
            var available=Font.GetOSInstalledFontNames();var choice=new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Noto Sans KR"}.FirstOrDefault(available.Contains);
            if(choice!=null)menuFont=Font.CreateDynamicFontFromOSFont(choice,20);
            bodyStyle=new GUIStyle(GUI.skin.label){font=menuFont,wordWrap=true,fontSize=20};titleStyle=new GUIStyle(bodyStyle){fontSize=30,fontStyle=FontStyle.Bold};
        }
        private void OnGUI()
        {
            if(LegacyOverride)return;Styles();var oldMatrix=GUI.matrix;var oldFont=GUI.skin.font;var oldSize=GUI.skin.button.fontSize;
            var scale=Mathf.Max(.65f,Mathf.Min(Screen.width/1100f,Screen.height/760f));GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);GUI.skin.font=menuFont;GUI.skin.button.fontSize=20;
            var width=Screen.width/scale;var height=Screen.height/scale;
            try
            {
                GUI.Label(new Rect(20,16,width-40,32),"CHOOguard  |  KORAIL 검증 전 예시",bodyStyle);
                if(IsRunning&&!Session.Paused){GUI.Label(new Rect(width/2-8,height/2-16,24,30),"+",titleStyle);return;}
                var panel=new Rect((width-850)/2,100,850,height-130);var old=GUI.color;GUI.color=new Color(.025f,.04f,.055f,.97f);GUI.DrawTexture(panel,Texture2D.whiteTexture);GUI.color=old;
                GUILayout.BeginArea(new Rect(panel.x+28,panel.y+24,panel.width-56,panel.height-48));scroll=GUILayout.BeginScrollView(scroll);
                if(!IsConfigured){GUILayout.Label("공간 구성 확인",titleStyle);GUILayout.Label(configurationError??"현장 불러오는 중",bodyStyle);}
                else if(!IsRunning)
                {
                    GUILayout.Label("CHOOguard  |  현장 근무",titleStyle);
                    GUILayout.Label("역사 안을 자유롭게 이동하며 설비와 이용객을 살펴보세요. 운영 중 뜻밖의 변화가 발생할 수 있습니다. 현장에서 보이는 정보와 설비 상태를 확인하고 대응합니다.",bodyStyle);GUILayout.Space(18);
                    GUILayout.Label("WASD 이동 · 마우스 시점 · E 설비 확인/가까운 이용객 인솔 · Esc 일시정지/대응 기록",bodyStyle);GUILayout.Space(18);
                    foreach(var role in roles.roles)if(GUILayout.Button((roleId==role.roleId?"● ":"○ ")+role.temporaryDisplayName,GUILayout.Height(40)))roleId=role.roleId;
                    GUILayout.Space(18);GUILayout.Label("현재 공간·사건·세부 절차는 검증 전 합성 훈련입니다. 실제 자료와 담당자 검수로 교체합니다.",bodyStyle);
                    if(GUILayout.Button("현장 입장",GUILayout.Height(58)))StartShift(unchecked(Environment.TickCount^Guid.NewGuid().GetHashCode()),roleId);
                }
                else
                {
                    GUILayout.Label(showDebrief?"대응 기록":"일시정지",titleStyle);
                    if(showDebrief)
                    {
                        if(Session.History.Count==0)GUILayout.Label("완료한 대응 기록이 아직 없습니다.",bodyStyle);
                        foreach(var entry in Session.History.Reverse())GUILayout.Label(entry.incidentId+" · "+DisplayTarget(entry.site)+"\n관측 "+(entry.observedAt-entry.startedAt).ToString("0.0")+"초 · 보고 "+(entry.reportedAt-entry.observedAt).ToString("0.0")+"초 · 경로 "+DisplayTarget(entry.selectedRoute)+" · 집결 "+entry.arrived+"/"+entry.affected+"\n직무 행동 관측 "+(entry.roleActionObserved?"있음":"없음")+" · 공식 평가 아님",bodyStyle);
                        if(Session.History.Count>0)
                        {
                            var last=Session.History.Last();GUILayout.Space(12);GUILayout.Label("최근 대응 시간순 기록",titleStyle);
                            foreach(var entry in Session.Trace.Where(x=>x.incidentId==last.incidentId&&ActionLabel(x.action)!=null).Take(40))
                                GUILayout.Label((entry.seconds-last.startedAt).ToString("0.0")+"초  "+ActionLabel(entry.action)+" · "+DisplayTarget(entry.target)+(entry.accepted?"":" · "+entry.detail),bodyStyle);
                        }

                    }
                    if(GUILayout.Button("현장으로 돌아가기",GUILayout.Height(52)))ResumeWorld();
                    if(GUILayout.Button("대응 기록 보기",GUILayout.Height(46)))showDebrief=true;
                    if(GUILayout.Button("새 근무 시작",GUILayout.Height(46)))StartShift(unchecked(Environment.TickCount^Guid.NewGuid().GetHashCode()),roleId);
                }
                GUILayout.EndScrollView();GUILayout.EndArea();
            }
            finally{GUI.matrix=oldMatrix;GUI.skin.font=oldFont;GUI.skin.button.fontSize=oldSize;}
        }
        private void OnDestroy(){if(menuFont!=null)Destroy(menuFont);}
    }
}
