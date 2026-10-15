using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Mvp
{
    // Read-only projection: all transitions and observations belong to the runtime graph.
    public sealed class MvpProgressionGraphView : MonoBehaviour
    {
        public MvpAgencyDispatchController Controller;
        private GameObject overlay;
        private RectTransform content;
        private TMP_Text heading, details;
        private MvpProgressionGraph graph;
        private int revision = -1;
        private string operationId, teamId, selectionId;
        private bool selectedEdge;
        private MvpProgressionGraph.ViewSnapshot snapshot;
        private readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();

        public void Toggle()
        {
            if (overlay == null) Build();
            if (overlay == null) return;
            overlay.SetActive(!overlay.activeSelf);
            if (overlay.activeSelf) { revision = -1; Refresh(); }
        }
        private void Update() { if (overlay != null && overlay.activeSelf) Refresh(); }
        private void OnDestroy() { if (overlay != null) Destroy(overlay); }
        private void Build()
        {
            if (Controller == null || Controller.Workspace == null) return;
            var canvas = Controller.Workspace.transform.Find("WorkspaceCanvas");
            if (canvas == null) return;
            overlay = Box(canvas, "ProgressionGraphOverlay", new Color(.015f,.035f,.05f,.99f)).gameObject;
            var root = (RectTransform)overlay.transform; Stretch(root, 24,24,24,24);
            heading = Label(root, "운영 흐름", 20,14,850,58,20);
            var close = Button(root, "닫기", () => overlay.SetActive(false));
            close.anchorMin = close.anchorMax = close.pivot = Vector2.one;
            close.anchoredPosition = new Vector2(-16,-16); close.sizeDelta = new Vector2(80,34);
            var viewport = Box(root,"GraphViewport",new Color(.025f,.055f,.075f));
            Stretch(viewport,16,80,16,196); viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            content = Box(viewport,"GraphContent",new Color(.025f,.055f,.075f));
            Rect(content,0,0,940,800); scroll.viewport=viewport; scroll.content=content;
            scroll.horizontal=true; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped;
            details=Label(root,"단계 또는 흐름을 선택하면 진행 이유와 결과를 확인합니다.",20,0,900,170,14);
            var d=(RectTransform)details.transform; d.anchorMin=new Vector2(0,0); d.anchorMax=new Vector2(1,0); d.pivot=new Vector2(0,0); d.anchoredPosition=new Vector2(20,12); d.sizeDelta=new Vector2(-40,170);
            overlay.SetActive(false);
        }
        private void Refresh()
        {
            if (graph == null) graph=Controller.Workspace.GetComponent<MvpProgressionGraph>();
            string nextOperation=null, team="대응팀 미선택";
            foreach (var mission in Controller.MissionSnapshots)
                if (mission.TeamId == Controller.SelectedOperationalTeamId) { nextOperation=mission.OperationId ?? mission.LastCompletedOperationId; team=mission.TeamLabel+" · "+MvpAgencyDispatchController.KoreanState(mission.State); break; }
            if (graph == null) { heading.text="운영 흐름 · "+team; details.text="운영 흐름을 아직 불러오지 못했습니다."; return; }
            heading.text="운영 흐름 · "+team+"\n단계·흐름 선택으로 자세히 보기 · 드래그하여 이동";
            if (revision==graph.Revision && operationId==nextOperation && teamId==Controller.SelectedOperationalTeamId) return;
            operationId=nextOperation; teamId=Controller.SelectedOperationalTeamId; revision=graph.Revision;
            snapshot=graph.Snapshot(operationId ?? "");
            Render();
        }
        private void Render()
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            positions.Clear();
            if(snapshot==null) { details.text="진행 정보를 기다리고 있습니다."; return; }
            var nodes=snapshot.Nodes ?? new MvpProgressionGraph.NodeView[0];
            var edges=snapshot.Edges ?? new MvpProgressionGraph.EdgeView[0];
            for(int i=0;i<nodes.Length;i++) positions[nodes[i].Id]=new Vector2(30+(i%3)*305,35+(i/3)*145);
            float graphHeight=Mathf.Max(220,((nodes.Length+2)/3)*145+40);
            content.sizeDelta=new Vector2(940,graphHeight+edges.Length*38+55);
            foreach(var edge in edges)
            {
                if(!positions.TryGetValue(edge.From,out var from)||!positions.TryGetValue(edge.To,out var to))continue;
                from+=new Vector2(125,42); to+=new Vector2(125,42);
                var delta=new Vector2(to.x-from.x,-to.y+from.y);
                var line=Box(content,"Connection",edge.Taken?new Color(.35f,.85f,.65f):new Color(.3f,.43f,.5f));
                Rect(line,from.x,from.y,delta.magnitude,edge.Taken?4:2); line.pivot=new Vector2(0,.5f); line.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                line.GetComponent<Image>().raycastTarget=false;
                var arrow=Label(content,"▶",from.x+(to.x-from.x)*.6f-8,from.y+(to.y-from.y)*.6f-8,20,20,16);
                arrow.transform.localRotation=line.localRotation;
            }
            foreach(var node in nodes)
            {
                var id=node.Id; var p=positions[id];
                var card=Button(content,node.Label+"\n"+(id==snapshot.CurrentNodeId?"현재 단계":Safe(node.State)),()=>{selectionId=id;selectedEdge=false;ShowDetails();});
                Rect(card,p.x,p.y,250,84); card.GetComponent<Image>().color=id==snapshot.CurrentNodeId?new Color(.08f,.38f,.4f):new Color(.075f,.14f,.19f);
            }
            Label(content,"진행 흐름 · 선택하여 조건과 예상 영향 확인",30,graphHeight-10,880,28,15);
            for(int i=0;i<edges.Length;i++)
            {
                var edge=edges[i]; var id=edge.Id;
                var row=Button(content,(edge.Taken?"진행됨 · ":edge.Enabled?"진행 가능 · ":"대기 · ")+edge.Label,()=>{selectionId=id;selectedEdge=true;ShowDetails();});
                Rect(row,30,graphHeight+25+i*38,880,32);
            }
            ShowDetails();
        }
        private void ShowDetails()
        {
            if(snapshot==null)return;
            string text=Safe(snapshot.Status), expected="아직 선택한 흐름이 없습니다.", observed="선택한 업무의 관찰 결과가 아직 없습니다.";
            string selectedNode=null;
            if(selectedEdge && snapshot.Edges!=null) foreach(var edge in snapshot.Edges) if(edge.Id==selectionId) { text=edge.Label+" · "+(edge.Taken?"진행됨":edge.Enabled?"진행 가능":"대기")+"\n진행 이유: "+Safe(edge.Reason); expected=Safe(edge.Prediction); }
            if(!selectedEdge && snapshot.Nodes!=null) foreach(var node in snapshot.Nodes) if(node.Id==(selectionId??snapshot.CurrentNodeId)) { selectedNode=node.Id; text=node.Label+"\n"+Safe(node.Detail); }
            if(snapshot.Recent!=null) for(int i=snapshot.Recent.Length-1;i>=0;i--)
            {
                var trace=snapshot.Recent[i]; if(operationId==null || trace.OperationId!=operationId)continue;
                if(selectedNode!=null && trace.To!=selectedNode && trace.From!=selectedNode)continue;
                if(selectedEdge && snapshot.Edges!=null) { var match=false; foreach(var edge in snapshot.Edges) if(edge.Id==selectionId && edge.From==trace.From && edge.To==trace.To)match=true; if(!match)continue; }
                observed=Safe(trace.Observed); if(!selectedEdge)expected=Safe(trace.Expected); break;
            }
            details.text=text+"\n예상 영향: "+expected+"\n관찰 결과: "+observed;
        }
        private static string Safe(string value) { switch(value) { case "current": return "현재"; case "visited": return "지남"; case "available": return "진행 가능"; case "blocked": return "진행 불가"; case "pending": return "대기"; } return string.IsNullOrWhiteSpace(value)?"아직 확인되지 않았습니다.":value.Replace("ACK","접수 확인").Replace("UNKNOWN","확인 대기").Replace("CONFLICTED","상태 불일치").Replace("TRUE","조건 충족").Replace("FALSE","조건 미충족"); }
        private RectTransform Box(Transform parent,string name,Color color) { var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);g.GetComponent<Image>().color=color;return (RectTransform)g.transform; }
        private TMP_Text Label(Transform parent,string value,float x,float y,float w,float h,float size) { var g=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(parent,false);Rect((RectTransform)g.transform,x,y,w,h);var t=g.GetComponent<TextMeshProUGUI>();t.font=Controller.Workspace.Font;t.fontSize=size;t.color=Color.white;t.text=value;t.raycastTarget=false;t.overflowMode=TextOverflowModes.Ellipsis;return t; }
        private RectTransform Button(Transform parent,string value,UnityEngine.Events.UnityAction action) { var r=Box(parent,"GraphChoice",new Color(.09f,.23f,.29f));var b=r.gameObject.AddComponent<Button>();b.onClick.AddListener(action);var text=Label(r,value,8,4,1,1,14);Stretch((RectTransform)text.transform,8,4,8,4);text.alignment=TextAlignmentOptions.MidlineLeft;return r; }
        private static void Rect(RectTransform r,float x,float y,float w,float h) { r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h); }
        private static void Stretch(RectTransform r,float left,float top,float right,float bottom) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top); }
    }
}
