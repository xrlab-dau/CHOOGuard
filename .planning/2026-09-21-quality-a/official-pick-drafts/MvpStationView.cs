using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace ChooGuard.App.Mvp
{
    public sealed class MvpStationView : MonoBehaviour
    {
        public Camera ViewCamera;
        public RenderTexture Texture;
        public Transform[] FloorRoots;
        public Transform IncidentMarker;
        public Transform PlatformAnchor, ConcourseAnchor, ExitAnchor;
        public Transform[] Teams;
        public AnimationClip IdleClip, WalkClip, InteractClip;
        public Material TaskLineMaterial;
        private LineRenderer selectionRing,taskLine;
        private Mesh cohortSelectionMesh;
        public static Vector3 ReferenceLocal(float x,float z)=>MvpSpatialMetrics.Reference(x,z);
        public void SetScenarioMarker(string scenario){if(IncidentMarker!=null)IncidentMarker.localPosition=ReferenceLocal(scenario=="fire_smoke"?15:24.5f,scenario=="fire_smoke"?5:10)+Vector3.up*1.5f;}
        private readonly Vector3?[] taskTargets=new Vector3?[3];
        private readonly float[] interactionUntil=new float[3];
        private float acceptedClock;
        private readonly Vector3[][] routes=new Vector3[3][];
        private readonly Vector3?[] routeGoals=new Vector3?[3];
        private readonly int[] routeCursor=new int[3];
        private readonly string[] routeReasons=new string[3];
        public bool TeamRouteAvailable(int index)=>routes[index]!=null;
        public string TeamRouteReason(int index)
        {
            string reason=routeReasons[index];
            switch(reason)
            {
                case "unsupported_floor_v1_no_cross_floor_link":return "지원하지 않는 층 · 층간 이동 경로 없음";
                case "navigation_not_baked":return "보행 경로 자료 없음";
                case "endpoint_outside_walkable_surface":return "출발점 또는 목표점이 보행 영역 밖에 있음";
                case "endpoint_projection_too_far":return "출발점 또는 목표점에서 보행 경로가 너무 멂";
                case "unreachable_or_partial_path":return "목표까지 연결된 보행 경로 없음";
                case "incomplete_corner_path":return "목표까지의 보행 경로가 불완전함";
                case "ready_second_floor_v1":return "2층 보행 경로 준비됨";
                case null:case "":case "route_not_requested":return "보행 경로 요청 전";
                default:return reason.StartsWith("navigation_error:")?"보행 경로 계산 오류":"보행 경로 상태 확인 필요";
            }
        }
        private bool EnsureRoute(int index,Vector3 target)
        {
            if(routeGoals[index].HasValue&&(routeGoals[index].Value-target).sqrMagnitude<.0001f)return routes[index]!=null;
            routeGoals[index]=target;routeCursor[index]=1;routes[index]=null;
            var nav=GetComponent<MvpTeamNavigation>();
            if(nav==null){routeReasons[index]="navigation_not_baked";return false;}
            bool ready=nav.TryPlan(Teams[index].localPosition,target,out var corners,out var reason);
            routeReasons[index]=reason;if(ready)routes[index]=corners;return ready;
        }
        private void AdvanceRoute(int index,float distance)
        {
            var route=routes[index];if(route==null)return;
            while(distance>0&&routeCursor[index]<route.Length)
            {
                var target=route[routeCursor[index]];float remaining=Vector3.Distance(Teams[index].localPosition,target);
                if(remaining<=distance){Teams[index].localPosition=target;distance-=remaining;routeCursor[index]++;}
                else {Teams[index].localPosition=Vector3.MoveTowards(Teams[index].localPosition,target,distance);distance=0;}
            }
        }
        public int CrowdVisualCount=>crowd.Count;
        public int CrowdVisualInstanceId(int id)=>crowd.TryGetValue(id,out var visual)?visual.Body.GetInstanceID():0;
        private int selectedIndex;
        private string selectedTarget;
        public Transform WholeEnvelope;
        public MvpOpenWorldCamera Navigation;
        public Transform CrowdRoot;
        public GameObject CrowdTemplate;
        public GameObject[] CivilianTemplates;
        public AnimationClip[] CivilianWalkClips;
        private readonly Dictionary<int, CrowdVisual> crowd=new Dictionary<int, CrowdVisual>();
        private sealed class CrowdVisual { public Transform Body; public PlayableGraph Graph; public Vector3 From,To; public float Start,Duration=.8f; public int Cohort;public bool Moving; }
        [SerializeField] private MvpWorkspace workspace;
        private readonly string[] ids={"ops-1","fire-1","medical-1"};
        private readonly string[] locations={"concourse","concourse","concourse"};
        private PlayableGraph[] graphs;
        private AnimationMixerPlayable[] mixers;
        private int floor=2;
        private bool incident;
        public void Bind(MvpWorkspace value) { if(workspace!=null) workspace.FloorChanged-=SetFloor; workspace=value; if(value!=null) { value.FloorChanged+=SetFloor; value.SetStationTexture(null); SetFloor(value.CurrentFloor); } }
        private void Start() { Bind(workspace); if(Teams==null) return; graphs=new PlayableGraph[Teams.Length]; mixers=new AnimationMixerPlayable[Teams.Length]; for(int i=0;i<Teams.Length;i++) { var animator=Teams[i].GetComponentInChildren<Animator>(); if(animator==null || IdleClip==null || WalkClip==null) continue; animator.applyRootMotion=false;graphs[i]=PlayableGraph.Create("훈련팀 동작"); var output=AnimationPlayableOutput.Create(graphs[i],"동작",animator); mixers[i]=AnimationMixerPlayable.Create(graphs[i],3); var idle=AnimationClipPlayable.Create(graphs[i],IdleClip); var walk=AnimationClipPlayable.Create(graphs[i],WalkClip); graphs[i].Connect(idle,0,mixers[i],0); graphs[i].Connect(walk,0,mixers[i],1);if(InteractClip!=null)graphs[i].Connect(AnimationClipPlayable.Create(graphs[i],InteractClip),0,mixers[i],2); mixers[i].SetInputWeight(0,1); output.SetSourcePlayable(mixers[i]); graphs[i].Play(); } }
        public void SetFloor(int value)
        {
            floor=Mathf.Clamp(value,0,3);
            bool officialShell=WholeEnvelope!=null&&WholeEnvelope.Find("공식 자료 부산역 역사")!=null;
            if(FloorRoots!=null) for(int i=0;i<FloorRoots.Length;i++) if(FloorRoots[i]!=null)FloorRoots[i].gameObject.SetActive((floor==0&&!officialShell)||floor==i+1);
            if(WholeEnvelope!=null)WholeEnvelope.gameObject.SetActive(floor==0);
            // Interior cutaways hide only the surrounding source-city geometry, not simulation or hub roots.
            var cityGeometry=transform.Find("도시 공개지형");
            if(cityGeometry!=null)cityGeometry.gameObject.SetActive(floor==0);
            if(CrowdRoot!=null)CrowdRoot.gameObject.SetActive(floor==0||floor==2);
            if(Navigation!=null)Navigation.Focus(new Vector3(0,3,0),floor==0?260:125);
            SetIncidentVisible(incident);
        }
        public void FocusReferenceHall() { if(workspace!=null)workspace.SelectFloor(2);else SetFloor(2);if(Navigation!=null)Navigation.Focus(new Vector3(-37,5.05f,-38),22); }
        public void FocusCity() { if(workspace!=null)workspace.SelectFloor(0);else SetFloor(0);if(Navigation!=null)Navigation.Focus(new Vector3(0,0,-720),2000); }
        public void SetCrowd(MvpPhysicsAgent[] agents)
        {
            if(CrowdRoot==null || agents==null)return;
            var present=new HashSet<int>();
            foreach(var agent in agents)
            {
                present.Add(agent.id);
                if(!crowd.TryGetValue(agent.id,out var visual))
                {
                    int variant=CivilianTemplates!=null && CivilianTemplates.Length>0?(int)((uint)agent.id%(uint)CivilianTemplates.Length):-1;
                    var template=variant>=0?CivilianTemplates[variant]:null;
                    bool civilian=template!=null;if(!civilian)template=CrowdTemplate;if(template==null)continue;
                    var walkClip=civilian?(CivilianWalkClips!=null&&variant<CivilianWalkClips.Length?CivilianWalkClips[variant]:null):WalkClip;
                    var body=Instantiate(template,CrowdRoot).transform;foreach(var child in body.GetComponentsInChildren<Transform>(true))child.gameObject.layer=CrowdRoot.gameObject.layer;body.name="계산 보행자 "+agent.id;body.localScale=template.transform.localScale*(civilian?1f:MvpSpatialMetrics.CivilianHeight/MvpSpatialMetrics.CrewHeight);
                    visual=new CrowdVisual { Body=body,From=ReferenceLocal(agent.x,agent.z),To=ReferenceLocal(agent.x,agent.z),Start=Time.unscaledTime };body.localPosition=visual.To;crowd.Add(agent.id,visual);
                    var animator=body.GetComponentInChildren<Animator>();
                    if(animator!=null&&walkClip!=null) { animator.applyRootMotion=false;visual.Graph=PlayableGraph.Create("계산 보행 동작");var output=AnimationPlayableOutput.Create(visual.Graph,"보행",animator);output.SetSourcePlayable(AnimationClipPlayable.Create(visual.Graph,walkClip));visual.Graph.Play(); }
                }
                var position=ReferenceLocal(agent.x,agent.z);
                visual.Cohort=agent.cohort;
                var delta=position-visual.To;visual.Moving=delta.sqrMagnitude>.0001f;visual.From=visual.Body.localPosition;visual.To=position;visual.Duration=Mathf.Clamp(Time.unscaledTime-visual.Start,.1f,1.1f);visual.Start=Time.unscaledTime;
                if(delta.sqrMagnitude>.0001f)visual.Body.localRotation=Quaternion.LookRotation(new Vector3(delta.x,0,delta.z));
                if(visual.Graph.IsValid())visual.Graph.GetRootPlayable(0).SetSpeed(delta.sqrMagnitude>.0001f?1:0);
            }
            var remove=new List<int>();foreach(var item in crowd)if(!present.Contains(item.Key))remove.Add(item.Key);
            foreach(var id in remove) { var visual=crowd[id];if(visual.Graph.IsValid())visual.Graph.Destroy();Destroy(visual.Body.gameObject);crowd.Remove(id); }
        }
        public void SetIncidentVisible(bool visible) { incident=visible; if(IncidentMarker!=null) IncidentMarker.gameObject.SetActive(visible && (floor==0 || floor==2)); }
        private Vector3 Anchor(string location) { var anchor=location=="platform"?PlatformAnchor:location=="exit"?ExitAnchor:ConcourseAnchor; if(anchor!=null) return transform.InverseTransformPoint(anchor.position); if(location=="platform") return new Vector3(48,.05f,4); if(location=="exit") return new Vector3(-64,.05f,-4); return new Vector3(-20,5.05f,0); }
        public Vector3 WorldAnchor(string location)=>transform.TransformPoint(Anchor(location));
        public bool TeamArrived(int index,string location)=>(Teams[index].localPosition-(Anchor(location)+new Vector3(0,0,(index-1)*2.2f))).sqrMagnitude<.04f;
        public void SetTaskTarget(int index,string action) { taskTargets[index]=action=="warn"?ReferenceLocal(6,10):action=="evacuate"?ReferenceLocal(30,10):ReferenceLocal(3,6);EnsureRoute(index,taskTargets[index].Value); }
        public void ClearTaskTarget(int index) { taskTargets[index]=null;routeGoals[index]=null;routes[index]=null; }
        public bool TeamTaskArrived(int index)=>taskTargets[index].HasValue&&(Teams[index].localPosition-taskTargets[index].Value).sqrMagnitude<.04f;
        public void ShowTeamInteraction(int index)
        {
            if(index<0||index>=interactionUntil.Length)return;
            if(mixers!=null&&index<mixers.Length&&mixers[index].IsValid())
            {
                var interaction=mixers[index].GetInput(2);
                if(interaction.IsValid()){interaction.SetTime(0);interaction.SetDone(false);}
            }
            interactionUntil[index]=acceptedClock+(InteractClip!=null?Mathf.Min(2,InteractClip.length):.4f);
        }
        public bool TeamInteractionFinished(int index)=>acceptedClock>=interactionUntil[index];
        public void SetTeamSelection(int index,string location) { selectedIndex=index;selectedTarget=location; }
        private LineRenderer Line(string name,int count,bool loop)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.layer=gameObject.layer;var line=go.AddComponent<LineRenderer>();line.sharedMaterial=TaskLineMaterial;line.positionCount=count;line.loop=loop;line.useWorldSpace=true;line.widthMultiplier=.10f;line.startColor=line.endColor=new Color(.2f,.92f,1);return line;
        }
        // Only acknowledged engine time authorizes travel and preparation completion.
        public void AdvanceAcceptedTime(float delta)
        {
            if(delta<=0||float.IsNaN(delta)||float.IsInfinity(delta)||workspace==null||Teams==null)return;
            acceptedClock+=delta;
            for(int i=0;i<Teams.Length&&i<ids.Length;i++)
            {
                bool holding=false;foreach(var state in workspace.TeamState)if(state.Id==ids[i]) { holding=state.Status=="Holding";if(locations[i]!=state.LocationId)taskTargets[i]=null;locations[i]=state.LocationId; }
                if(!holding&&EnsureRoute(i,taskTargets[i]??(Anchor(locations[i])+new Vector3(0,0,(i-1)*2.2f))))AdvanceRoute(i,delta*MvpSpatialMetrics.WalkingSpeed);
            }
        }
        public void ResetAcceptedClock(){acceptedClock=0;Array.Clear(interactionUntil,0,interactionUntil.Length);Array.Clear(routeGoals,0,routeGoals.Length);Array.Clear(routes,0,routes.Length);}
        private static void LoopAnimation(Playable playable)
        {
            if(!playable.IsValid())return;
            if(playable.GetPlayableType()==typeof(AnimationClipPlayable)) { var clip=(AnimationClipPlayable)playable;float length=clip.GetAnimationClip().length;if(length>0&&clip.GetTime()>=length){clip.SetTime(clip.GetTime()%length);clip.SetDone(false);} }
            for(int i=0;i<playable.GetInputCount();i++)LoopAnimation(playable.GetInput(i));
        }
        private void Update()
        {
            if(workspace==null||Teams==null)return;
            var director=workspace.GetComponent<MvpTrainingDirector>();bool paused=director!=null&&director.IsPaused;
            foreach(var visual in crowd.Values){if(paused)visual.Start+=Time.unscaledDeltaTime;else visual.Body.localPosition=Vector3.Lerp(visual.From,visual.To,Mathf.Clamp01((Time.unscaledTime-visual.Start)/visual.Duration));if(visual.Graph.IsValid())visual.Graph.GetRootPlayable(0).SetSpeed(!paused&&visual.Moving?1:0);}
            for(int i=0;i<Teams.Length;i++)
            {
                bool holding=false;foreach(var state in workspace.TeamState)if(state.Id==ids[i]) { holding=state.Status=="Holding"; }
                Vector3 target=taskTargets[i]??(Anchor(locations[i])+new Vector3(0,0,(i-1)*2.2f));EnsureRoute(i,target);var next=routes[i]!=null&&routeCursor[i]<routes[i].Length?routes[i][routeCursor[i]]:Teams[i].localPosition;var delta=next-Teams[i].localPosition;bool moving=delta.magnitude>.08f&&!paused&&!holding;
                if(moving&&new Vector2(delta.x,delta.z).sqrMagnitude>.01f)Teams[i].localRotation=Quaternion.Slerp(Teams[i].localRotation,Quaternion.LookRotation(new Vector3(delta.x,0,delta.z)),Time.unscaledDeltaTime*6);
                Teams[i].gameObject.SetActive(taskTargets[i].HasValue||floor==0||(locations[i]=="concourse"?floor==2:floor==1));
                if(mixers!=null&&mixers[i].IsValid()) { bool interact=!moving&&!paused&&InteractClip!=null&&acceptedClock<interactionUntil[i];mixers[i].SetInputWeight(0,!moving&&!interact?1:0);mixers[i].SetInputWeight(1,moving?1:0);mixers[i].SetInputWeight(2,interact?1:0); }
            }
            if(graphs!=null)foreach(var graph in graphs)if(graph.IsValid())LoopAnimation(graph.GetRootPlayable(0));
            foreach(var visual in crowd.Values)if(visual.Graph.IsValid())LoopAnimation(visual.Graph.GetRootPlayable(0));
            if(TaskLineMaterial==null)return;
            if(cohortSelectionMesh==null){var go=new GameObject("선택 집단 표시",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(CrowdRoot!=null?CrowdRoot:transform,false);go.layer=gameObject.layer;cohortSelectionMesh=new Mesh{name="선택 집단 위치"};go.GetComponent<MeshFilter>().sharedMesh=cohortSelectionMesh;go.GetComponent<MeshRenderer>().sharedMaterial=TaskLineMaterial;}
            var vertices=new List<Vector3>();var triangles=new List<int>();foreach(var visual in crowd.Values)if(visual.Cohort==workspace.SelectedCohort){var center=visual.Body.localPosition-Vector3.up*.03f;for(int n=0;n<12;n++){float a=n*Mathf.PI/6,b=(n+1)*Mathf.PI/6;int start=vertices.Count;vertices.Add(center+new Vector3(Mathf.Cos(a)*.31f,0,Mathf.Sin(a)*.31f));vertices.Add(center+new Vector3(Mathf.Cos(a)*.37f,0,Mathf.Sin(a)*.37f));vertices.Add(center+new Vector3(Mathf.Cos(b)*.37f,0,Mathf.Sin(b)*.37f));vertices.Add(center+new Vector3(Mathf.Cos(b)*.31f,0,Mathf.Sin(b)*.31f));triangles.Add(start);triangles.Add(start+2);triangles.Add(start+1);triangles.Add(start);triangles.Add(start+3);triangles.Add(start+2);}}
            cohortSelectionMesh.Clear();cohortSelectionMesh.SetVertices(vertices);cohortSelectionMesh.SetTriangles(triangles,0);cohortSelectionMesh.RecalculateBounds();
            if(selectionRing==null)selectionRing=Line("선택 팀 표시",48,true);if(taskLine==null)taskLine=Line("현장 작업 경로",2,false);
            var team=Teams[Mathf.Clamp(selectedIndex,0,Teams.Length-1)];selectionRing.gameObject.SetActive(team.gameObject.activeInHierarchy);
            for(int i=0;i<48;i++){float angle=i*Mathf.PI*2/48;selectionRing.SetPosition(i,team.position+new Vector3(Mathf.Cos(angle)*1.5f,.12f,Mathf.Sin(angle)*1.5f));}
            var selectedRoute=routes[selectedIndex];int cursor=routeCursor[selectedIndex];
            taskLine.gameObject.SetActive(selectedTarget!=null&&team.gameObject.activeInHierarchy&&selectedRoute!=null&&cursor<selectedRoute.Length);
            if(selectedRoute!=null&&cursor<selectedRoute.Length){taskLine.positionCount=1+selectedRoute.Length-cursor;taskLine.SetPosition(0,team.position+Vector3.up*.3f);for(int n=cursor;n<selectedRoute.Length;n++)taskLine.SetPosition(1+n-cursor,transform.TransformPoint(selectedRoute[n])+Vector3.up*.3f);}
        }
        private void OnDestroy() { if(cohortSelectionMesh!=null)Destroy(cohortSelectionMesh); foreach(var visual in crowd.Values) if(visual.Graph.IsValid())visual.Graph.Destroy(); if(workspace!=null) workspace.FloorChanged-=SetFloor; if(graphs!=null) foreach(var graph in graphs) if(graph.IsValid()) graph.Destroy(); }
    }
}
