using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        TMP_Text objective,reason,instruction,deviceStatus,nextAction,actionHint,subtitle,targetLabel,progress,pauseTitle,pauseBody;
        Transform hudRoot;
        GameObject pausePanel,coachingPanel,quickPanel,subtitlePanel;
        RectTransform progressFill,targetFrame;
        Button resumeButton;
        AudioSource replyAudio;
        float subtitleUntil;
        float nextCoachingUpdate;
        bool lastHudCaptured,lastRadioOpen;
        StationFireStep lastHudStep;

        TMP_Text GuideText(Transform parent,string name,Vector2 anchor,Vector2 offset,Vector2 size,float fontSize)
        {
            var text=FpsUiFactory.Text(parent,KoreanFont,name,anchor,offset,size,fontSize,TextAlignmentOptions.TopLeft);
            text.textWrappingMode=TextWrappingModes.Normal;text.enableAutoSizing=false;text.lineSpacing=2;
            return text;
        }
        void CreateView()
        {
            if(KoreanFont==null)throw new InvalidOperationException("화재 교육 한국어 폰트가 없습니다");
            if(EventSystem.current==null)new GameObject("화재 교육 UI 입력",typeof(EventSystem),typeof(InputSystemUIInputModule));
            hudRoot=FpsUiFactory.InteractiveCanvas("역무원 화재대응 안내",transform,25).transform;
            coachingPanel=FpsUiFactory.Block(hudRoot,"목표 배경",new Vector2(0,1),new Vector2(24,-20),new Vector2(500,306),new Color(.02f,.028f,.04f,.38f)).gameObject;
            var card=coachingPanel.transform;var top=new Vector2(0,1);
            progress=GuideText(card,"진행",top,new Vector2(20,-16),new Vector2(440,26),17);
            FpsUiFactory.Block(card,"진행 막대 배경",top,new Vector2(20,-47),new Vector2(440,4),new Color(1,1,1,.18f));
            progressFill=FpsUiFactory.Block(card,"진행 막대",top,new Vector2(20,-47),new Vector2(0,4),FpsUiFactory.Accent).rectTransform;
            objective=GuideText(card,"현재 목표",top,new Vector2(20,-54),new Vector2(460,50),24);objective.color=FpsUiFactory.Accent;
            reason=GuideText(card,"이유와 다음 행동",top,new Vector2(20,-123),new Vector2(440,66),18);
            instruction=GuideText(card,"단계별 조작",top,new Vector2(20,-110),new Vector2(460,94),21);
            deviceStatus=GuideText(card,"실제 선택과 반응",top,new Vector2(20,-214),new Vector2(460,58),18);
            deviceStatus.color=new Color(.82f,.93f,1);
            nextAction=GuideText(card,"다음 행동",top,new Vector2(20,-520),new Vector2(440,40),16);nextAction.color=new Color(.8f,.83f,.87f);
            reason.gameObject.SetActive(false);nextAction.gameObject.SetActive(false);
            GuideText(card,"자세한 설명 안내",top,new Vector2(20,-278),new Vector2(460,22),15).text="Esc · 자세한 설명 / 조작 복습 / 대화 기록";
            quickPanel=FpsUiFactory.Block(hudRoot,"현재 조작 배경",new Vector2(.5f,0),new Vector2(0,42),new Vector2(1120,76),FpsUiFactory.PanelStrong).gameObject;
            actionHint=FpsUiFactory.Label(quickPanel.transform,KoreanFont,"현재 조작",Vector2.zero,new Vector2(1080,42),18);
            actionHint.textWrappingMode=TextWrappingModes.Normal;actionHint.enableAutoSizing=false;
            subtitlePanel=FpsUiFactory.Block(hudRoot,"통신 자막 배경",new Vector2(.5f,0),new Vector2(0,114),new Vector2(1120,140),new Color(.02f,.028f,.04f,.58f)).gameObject;
            subtitle=FpsUiFactory.Label(subtitlePanel.transform,KoreanFont,"화자 자막",new Vector2(0,12),new Vector2(1080,104),21);
            subtitle.textWrappingMode=TextWrappingModes.Normal;subtitle.enableAutoSizing=false;
            FpsUiFactory.Text(subtitlePanel.transform,KoreanFont,"대화 넘기기",new Vector2(1,0),new Vector2(-20,9),new Vector2(260,24),16,TextAlignmentOptions.Right).text="Enter · 다음 자막";
            targetFrame=FpsUiFactory.Block(hudRoot,"대상 표식 배경",new Vector2(.5f,.5f),Vector2.zero,new Vector2(370,76),new Color(.02f,.028f,.04f,.86f)).rectTransform;
            targetLabel=FpsUiFactory.Label(targetFrame,KoreanFont,"현재 대상 표식",Vector2.zero,new Vector2(350,70),18);targetLabel.color=FpsUiFactory.Accent;
            FpsUiFactory.Label(hudRoot,KoreanFont,"조준점",Vector2.zero,new Vector2(18,18),18).text="·";
            FpsUiFactory.Text(hudRoot,KoreanFont,"공통 입력",new Vector2(1,0),new Vector2(-24,20),new Vector2(1150,36),16,TextAlignmentOptions.BottomRight).text="WASD 이동 · 마우스 시선 · Esc 설명 / 일시정지";
            pausePanel=FpsUiFactory.Block(hudRoot,"일시정지",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1000,800),FpsUiFactory.PanelStrong).gameObject;
            pausePanel.GetComponent<Image>().raycastTarget=true;
            pauseTitle=FpsUiFactory.Label(pausePanel.transform,KoreanFont,"교육 안내",new Vector2(0,345),new Vector2(930,60),27);
            pauseBody=FpsUiFactory.Label(pausePanel.transform,KoreanFont,"현재 단계 다시 보기",new Vector2(0,30),new Vector2(930,400),20,TextAlignmentOptions.TopLeft);
            pauseBody.textWrappingMode=TextWrappingModes.Normal;pauseBody.enableAutoSizing=false;pauseBody.lineSpacing=3;
            CreateReviewTabs();
            resumeButton=FpsUiFactory.Button(pausePanel.transform,KoreanFont,"계속",new Vector2(.5f,.5f),new Vector2(0,-210),new Vector2(450,54),ResumeLesson);
            FpsUiFactory.Button(pausePanel.transform,KoreanFont,"처음부터 다시",new Vector2(.5f,.5f),new Vector2(0,-278),new Vector2(450,54),RestartLesson);
            FpsUiFactory.Button(pausePanel.transform,KoreanFont,"연습 목록",new Vector2(.5f,.5f),new Vector2(0,-342),new Vector2(450,54),ExitLesson);
            replyAudio=gameObject.AddComponent<AudioSource>();replyAudio.spatialBlend=0;replyAudio.volume=.7f;
            resumeButton.interactable=false;
            // Only the compact goal and active subtitle have local contrast support.
            foreach(var panel in new[]{quickPanel,targetFrame.gameObject})
                panel.GetComponent<Image>().enabled=false;
            foreach(var text in new[]{progress,objective,reason,instruction,deviceStatus,nextAction,actionHint,subtitle,targetLabel})
            {text.outlineColor=new Color32(0,0,0,240);text.outlineWidth=.2f;}
        }
        void UpdatePauseView(bool value)
        {
            if(pausePanel!=null){pausePanel.SetActive(value);if(value)pausePanel.transform.SetAsLastSibling();}
            bool complete=Step==StationFireStep.Complete;
            if(resumeButton!=null){resumeButton.gameObject.SetActive(!complete);resumeButton.interactable=Ready&&!complete;}
            if(pauseTitle!=null)
            {
                if(!Ready){pauseTitle.text=Failure.Length>0?"화재 현장을 준비하지 못했습니다":"역무원 화재대응 · 현장 준비 중";pauseBody.text=Failure.Length>0?Failure:"현장이 준비되면 이곳에 첫 행동과 조작을 안내합니다."; }
                else
                {
                    DescribeCurrentAction(out var stage,out var title,out var why,out var action,out var next);
                    pauseTitle.text=(complete?"":stage+"/10 · ")+title;
                    pauseBody.fontSize=reviewPage==3?18:complete?19:20;
                    pauseBody.text=ReviewBody(why,action,next);
                }
            }
            if(replyAudio!=null){if(value)replyAudio.Pause();else replyAudio.UnPause();}
        }
        void UpdateCoaching(bool announce)
        {
            if(objective==null)return;
            // Navigation queries, hierarchy bounds and changing distance strings
            // are presentation work, not the mechanical/safety simulation.
            if(!announce&&Ready&&lastHudStep==Step&&lastHudCaptured==Captured&&lastRadioOpen==RadioOpen&&Time.unscaledTime<nextCoachingUpdate)return;
            nextCoachingUpdate=Time.unscaledTime+.1f;lastHudStep=Step;lastHudCaptured=Captured;lastRadioOpen=RadioOpen;
            if(!Ready)
            {
                UpdatePauseView(true);
                objective.text=Failure.Length>0?"현장을 준비하지 못했습니다":"화재 현장을 준비하고 있습니다";
                reason.text=Failure.Length>0?Failure:"준비가 끝나면 첫 행동과 조작을 안내합니다.";
                instruction.text="";deviceStatus.text="";nextAction.text="";progress.text="역무원 화재대응";
                targetFrame.gameObject.SetActive(false);quickPanel.SetActive(false);subtitlePanel.SetActive(false);return;
            }
            DescribeCurrentAction(out var stage,out var title,out var why,out var action,out var next);
            bool visibleHud=Captured&&!arrivalTransition;
            coachingPanel.SetActive(visibleHud);quickPanel.SetActive(visibleHud);
            progress.text="역무원 화재대응 · "+stage+"/10"+(Step==StationFireStep.Complete?" · 종료":"");
            progressFill.sizeDelta=new Vector2(440*(Step==StationFireStep.Complete?1:(stage-1)/10f),4);
            if(RadioOpen)
            {
                if(Step!=StationFireStep.Report&&Step!=StationFireStep.ResultReport)title="Q 무전 메뉴";
                action=ReportInstructions(Step==StationFireStep.ResultReport||Step==StationFireStep.Specialist);
            }
            objective.text=title;reason.text=why;instruction.text=BriefInstructions(stage);deviceStatus.text=BriefFeedback(stage);nextAction.text="다음: "+next;
            deviceStatus.color=SafetyCorrection&&Step==StationFireStep.Extinguish&&!RadioOpen?new Color(1,.64f,.47f):new Color(.72f,.88f,1);
            subtitlePanel.SetActive(visibleHud&&!RadioOpen&&!string.IsNullOrEmpty(subtitle.text));
            var target=CurrentTeachingTarget(out var label);
            if(target!=null&&View!=null&&visibleHud&&!RadioOpen)
            {
                var point=target==FireTarget?FireTarget.position:target.GetComponent<StationFireActor>()!=null?target.position+Vector3.up*1.25f:RenderBounds(target).center;
                VisibleAimPoint=point;
                var screen=View.WorldToViewportPoint(point);bool inView=screen.z>0&&screen.x>.04f&&screen.x<.96f&&screen.y>.08f&&screen.y<.92f;
                string prefix="▼ ";
                if(!inView)
                {
                    var local=View.transform.InverseTransformPoint(point);
                    prefix=local.z<0?(local.x<0?"왼쪽 뒤 ← ":"오른쪽 뒤 → "):screen.y<.08f?"아래 ↓ ":screen.y>.92f?"위 ↑ ":local.x<0?"왼쪽 ← ":"오른쪽 → ";
                    screen.x=local.x<0?.48f:.84f;screen.y=.60f;
                }
                else if(screen.x<.48f)prefix="← ";
                Vector2 markerAnchor=default;string markerArrow="";
                bool besideEquipment=inView&&TryPlaceEquipmentLabel(target,screen,out markerAnchor,out markerArrow);
                if(besideEquipment)prefix=markerArrow;
                targetLabel.text=prefix+label+"\n"+Vector3.Distance(Player.transform.position,point).ToString("0.0")+"m";
                targetFrame.gameObject.SetActive(true);
                targetFrame.anchorMin=targetFrame.anchorMax=besideEquipment?markerAnchor:new Vector2(Mathf.Clamp(screen.x,.48f,.85f),Mathf.Clamp(screen.y,.31f,.84f));
                targetFrame.anchoredPosition=besideEquipment?Vector2.zero:new Vector2(0,44);
            }
            else targetFrame.gameObject.SetActive(false);
            actionHint.text=RadioOpen?"Q 유지 · 선택 없이 놓으면 취소 · 우클릭 이전 메뉴":
                Step==StationFireStep.Extinguish&&State.Entities["player"].Get("mech.carry.extinguisher")>0?"G · 소화기 내려놓기 / 다시 들기 F":"";
            quickPanel.SetActive(visibleHud&&!string.IsNullOrEmpty(actionHint.text));
            ShowVisibleRoute(navigationGoal,drawRoute&&visibleHud&&!RadioOpen);
            responseMarker.gameObject.SetActive(visibleHud&&Step==StationFireStep.Extinguish&&State.Entities["player"].Get("mech.carry.extinguisher")>0&&Mechanics.Position(ExtinguisherId,"nozzle")>=.95);
            if(Step==StationFireStep.Complete&&Captured)SetPaused(true);
        }

        // Keep the teaching card outside the visible equipment silhouette so that
        // the learner can distinguish the pin, nozzle and lever before aiming.
        bool TryPlaceEquipmentLabel(Transform target,Vector3 screen,out Vector2 anchor,out string arrow)
        {
            anchor=default;arrow="";
            if(target!=safetyPin&&target!=nozzle&&target!=lever)return false;
            var bounds=RenderBounds(ExtinguisherBody);
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<8;i++)
            {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var projected=View.WorldToViewportPoint(corner);
                if(projected.z<=0)return false;
                min=Vector2.Min(min,new Vector2(projected.x,projected.y));max=Vector2.Max(max,new Vector2(projected.x,projected.y));
            }
            var canvas=((RectTransform)hudRoot).rect.size;
            var half=new Vector2(targetFrame.rect.width/(2*Mathf.Max(1,canvas.x)),targetFrame.rect.height/(2*Mathf.Max(1,canvas.y)));
            var gap=new Vector2(16/Mathf.Max(1,canvas.x),16/Mathf.Max(1,canvas.y));
            for(int i=0;i<4;i++)
            {
                var candidate=i==0?new Vector2(max.x+half.x+gap.x,Mathf.Clamp(screen.y,.31f,.84f)):
                    i==1?new Vector2(min.x-half.x-gap.x,Mathf.Clamp(screen.y,.31f,.84f)):
                    i==2?new Vector2(Mathf.Clamp(screen.x,.48f,.85f),max.y+half.y+gap.y):
                    new Vector2(Mathf.Clamp(screen.x,.48f,.85f),min.y-half.y-gap.y);
                if(candidate.x<.48f||candidate.x>.98f-half.x||candidate.y<.31f||candidate.y>.92f-half.y)continue;
                if(candidate.x+half.x>min.x&&candidate.x-half.x<max.x&&candidate.y+half.y>min.y&&candidate.y-half.y<max.y)continue;
                anchor=candidate;arrow=i==0?"← ":i==1?"→ ":i==2?"▼ ":"▲ ";return true;
            }
            return false;
        }

        Vector3 navigationGoal;
        bool drawRoute;
        Transform WalkTarget(Transform target,Vector3 goal){navigationGoal=goal;drawRoute=Vector3.Distance(Player.transform.position,goal)>.35f;return target;}
        Vector3 Approach(Transform target,float distance=1.2f)
        {
            var actor=target.GetComponent<StationFireActor>();
            if(actor!=null&&Vector3.Distance(Player.transform.position,target.position)<2.1f&&CanSeeActor(Player.transform.position,actor))return Player.transform.position;
            var away=Player.transform.position-target.position;away.y=0;if(away.sqrMagnitude<.01f)away=-(layoutRotation*Vector3.forward);
            Vector3 best=Player.transform.position;float score=float.PositiveInfinity;
            foreach(float angle in new[]{0f,45,-45,90,-90,135,-135,180})
            {
                var p=target.position+Quaternion.Euler(0,angle,0)*away.normalized*distance;p.y=Player.transform.position.y;
                if(!UnityEngine.AI.NavMesh.SamplePosition(p,out var hit,.35f,UnityEngine.AI.NavMesh.AllAreas))continue;
                if(Physics.CheckCapsule(hit.position+Vector3.up*.34f,hit.position+Vector3.up*1.42f,.28f,~0,QueryTriggerInteraction.Ignore))continue;
                if(actor!=null&&!CanSeeActor(hit.position,actor))continue;
                var path=new UnityEngine.AI.NavMeshPath();if(!UnityEngine.AI.NavMesh.CalculatePath(Player.transform.position,hit.position,UnityEngine.AI.NavMesh.AllAreas,path)||path.status!=UnityEngine.AI.NavMeshPathStatus.PathComplete)continue;
                float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                if(length<score){best=hit.position;score=length;}
            }
            return best;
        }
        bool CanSeeActor(Vector3 feet,StationFireActor actor)
        {
            var eye=feet+Vector3.up*1.6f;var delta=actor.transform.position+Vector3.up*1.25f-eye;
            var first=Physics.RaycastAll(eye,delta.normalized,delta.magnitude+.05f,~(1<<2),QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.transform.IsChildOf(Player.transform)).OrderBy(h=>h.distance).FirstOrDefault();
            return first.collider!=null&&first.collider.GetComponentInParent<StationFireActor>()==actor;
        }
        Transform CurrentTeachingTarget(out string label)
        {
            label="";drawRoute=false;
            switch(Step)
            {
                case StationFireStep.Warning:
                    var person=passengers.OrderBy(p=>CanSeeActor(Player.transform.position,p)?0:1).ThenBy(p=>Vector3.Distance(Player.transform.position,p.transform.position)).First();
                    label="가까운 승객 · E로 대피 방향 안내";return WalkTarget(person.transform,Approach(person.transform));
                case StationFireStep.Extinguish:
                    if(State.Entities["player"].Get("mech.carry.extinguisher")<=0)
                    {label="매장 입구 옆 기존 소화기 · F 집기";return WalkTarget(ExtinguisherBody,CurrentExtinguisherApproach());}
                    if(Mechanics.Position(ExtinguisherId,"pin")<.95||Holding("pin")){label=Mechanics.Position(ExtinguisherId,"pin")>=.95?"안전핀 · 분리 완료":"안전핀";return safetyPin;}
                    if(Mechanics.Position(ExtinguisherId,"nozzle")<.95||Holding("nozzle")){label=Mechanics.Position(ExtinguisherId,"nozzle")>=.95?"노즐 · 준비 완료":"노즐";return nozzle;}
                    if(Holding("lever")){label="화점 아래쪽 · 좌클릭 유지한 채 조준";return FireTarget;}
                    if(NeedsSuppressionApproach()){label="퇴로를 둔 대응 위치";return WalkTarget(responseMarker,SuppressionPoint);}
                    label="레버";return lever;
                case StationFireStep.Release:label="레버 · 좌클릭 놓아 분사 중지";return lever;
                case StationFireStep.Specialist:
                    if(Specialist.gameObject.activeSelf){label="소방대 · E로 확인한 상황 전달";return WalkTarget(Specialist.transform,Approach(Specialist.transform));}return null;
                default:return null;
            }
        }
        bool suppressionApproachReached;
        bool NeedsSuppressionApproach()
        {
            var d=Player.transform.position-FireTarget.position;d.y=0;
            if(d.magnitude<3.5f)suppressionApproachReached=true;else if(d.magnitude>4.1f)suppressionApproachReached=false;
            return !suppressionApproachReached;
        }
    }
}
