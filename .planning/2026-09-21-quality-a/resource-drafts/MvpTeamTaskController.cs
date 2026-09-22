using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
namespace ChooGuard.App.Mvp
{
    public sealed class MvpTeamTaskController : MonoBehaviour
    {
        public MvpWorkspace Workspace;
        public MvpStationView Station;
        public MvpTrainingDirector Director;
        public event Action<MvpControlOrder> TaskReady;
        public Func<string,bool> OperationExecutionGate;
        private sealed class Job { public MvpControlOrder Order;public string Team,Location,State,OperationId,AgencyId,LogicalTeamId;public int Index,ExpectedRevision;public bool Released,Complete; }
        private readonly Dictionary<string,Queue<Job>> queues=new Dictionary<string,Queue<Job>>();
        private string lastCard;
        private readonly HashSet<string> operationAcks=new HashSet<string>();
        private Vector2 rightDown;
        private int resetEpoch;
        public bool HasActiveTask(string teamId){if(queues.TryGetValue(teamId,out var jobs))foreach(var job in jobs)if(!job.Complete)return true;return false;}
        public bool RequestTask(string action,string policy=null,int cohort=-1)
        {
            string team=Workspace.SelectedTeamId;var agency=Workspace.GetComponent<MvpAgencyDispatchController>();if(agency!=null&&!agency.CanCommandChannel(team)){Workspace.InlineNotice(agency.AvailabilityReason(team));return false;}string location=action=="evacuate"?"exit":"concourse";
            if(!queues.TryGetValue(team,out var queue)){queue=new Queue<Job>();queues[team]=queue;}
            if(queue.Count>=6)return false;
            bool append=Director!=null&&Director.IsPaused||Keyboard.current!=null&&(Keyboard.current.leftShiftKey.isPressed||Keyboard.current.rightShiftKey.isPressed);
            if(!append&&queue.Count>0&&!queue.Peek().Released){queue.Clear();Station.ClearTaskTarget(Index(team));}
            queue.Enqueue(new Job { Order=new MvpControlOrder{Action=action,ReleasePolicy=policy,CohortId=cohort},Team=team,Location=location,Index=Index(team),State="작업 예약" });lastCard=null;return true;
        }
        public bool EnqueueOperation(string channel,string action,string operationId,string agencyId,string logicalTeamId,string policy=null)
        {
            var agency=Workspace.GetComponent<MvpAgencyDispatchController>();
            if(operationId==null||agency==null||!agency.MatchesOperationOwner(channel,operationId,agencyId,logicalTeamId)||(action!="warn"&&action!="evacuate"&&action!="medical"))return false;
            if(!queues.TryGetValue(channel,out var queue)){queue=new Queue<Job>();queues[channel]=queue;}
            if(queue.Count>=6)return false;
            foreach(var existing in queue)if(existing.OperationId==operationId&&existing.Order.Action==action)return true;
            queue.Enqueue(new Job{Order=new MvpControlOrder{Action=action,ReleasePolicy=policy,CohortId=-1},Team=channel,Location=action=="evacuate"?"exit":"concourse",Index=Index(channel),State="작업 예약",OperationId=operationId,AgencyId=agencyId,LogicalTeamId=logicalTeamId});lastCard=null;return true;
        }
        public bool IsOperationActionComplete(string operationId,string action)=>operationAcks.Contains(operationId+":"+action);
        public void ClearCompletedOperation(string channel,string operationId)
        {
            if(HasActiveTask(channel))return;queues.Remove(channel);Station.ClearTaskTarget(Index(channel));lastCard=null;
        }
        private string Provenance(Job job)=>job.OperationId==null?MvpWorkspace.Korean(job.Team):Workspace.GetComponent<MvpAgencyDispatchController>().AgencyLabel(job.AgencyId)+" · "+Workspace.GetComponent<MvpAgencyDispatchController>().OperationalTeamLabel(job.LogicalTeamId);
        private static int Index(string id)=>id=="ops-1"?0:id=="fire-1"?1:2;
        public void ResetTasks(){resetEpoch++;queues.Clear();operationAcks.Clear();lastCard=null;if(Station!=null)for(int i=0;i<3;i++)Station.ClearTaskTarget(i);}
        public void CancelSelected()
        {
            string team=Workspace.SelectedTeamId;var agency=Workspace.GetComponent<MvpAgencyDispatchController>();if(agency!=null&&!agency.CanCommandChannel(team)){Workspace.InlineNotice(agency.AvailabilityReason(team));return;}
            if(queues.TryGetValue(team,out var queue)&&queue.Count>0&&queue.Peek().Released){Workspace.InlineNotice("이미 전달된 통제입니다. 집단 대기/유도 지시로 변경하세요.");return;}
            queues.Remove(team);Station.ClearTaskTarget(Index(team));Workspace.CancelPlannedOrders();_ = Workspace.IssueTeamCommand(team,"hold",Location(team));lastCard=null;
        }
        public void Observe(MvpPhysicsResult result)
        {
            foreach(var queue in queues.Values)
            {
                if(queue.Count==0)continue;var job=queue.Peek();if(!job.Released||result.requestId!=job.Order.RequestId||result.lastControlRequestId!=job.Order.RequestId)continue;
                if(job.Order.Action=="evacuate"&&result.releaseRevision!=job.ExpectedRevision)continue;
                Director?.RecordTeamEvent("통제 접수 확인 · "+Provenance(job),job.Team);if(job.OperationId!=null){operationAcks.Add(job.OperationId+":"+job.Order.Action);Workspace.GetComponent<MvpProgressionGraph>()?.RecordAcknowledgment(job.OperationId,job.Order.Action,result);}job.Complete=true;job.State="통제 반영 확인 · 다음 지시 가능";lastCard=null;
            }
        }
        public void ControlSent(MvpControlOrder order,int previousRevision)
        {foreach(var queue in queues.Values)if(queue.Count>0&&queue.Peek().Order.RequestId==order.RequestId)queue.Peek().ExpectedRevision=previousRevision+1;}
        private bool Holding(string id){foreach(var team in Workspace.TeamState)if(team.Id==id)return team.Status=="Holding";return false;}
        private string Location(string id){foreach(var team in Workspace.TeamState)if(team.Id==id)return team.LocationId;return "concourse";}
        private async void Dispatch(Job job)
        {
            int epoch=resetEpoch;job.State="지시 검증 중";bool accepted=await Workspace.IssueTeamCommand(job.Team,"move",job.Location,false,job.OperationId);if(epoch!=resetEpoch)return;job.State=accepted?"출동 접수":"지시 거절 · 재지시 필요";Director?.RecordTeamEvent((accepted?"출동 접수 · ":"출동 거절 · ")+Provenance(job),job.Team);
        }
        private void Update()
        {
            if(Workspace==null||Station==null)return;
            if((Director==null||Director.CanExecuteTasks)&&!Workspace.HasPendingTeamOrders)
            foreach(var queue in queues.Values)
            {
                if(queue.Count==0)continue;var job=queue.Peek();var agency=Workspace.GetComponent<MvpAgencyDispatchController>();if(agency!=null&&!agency.GetTeamAvailability(job.Team))continue;if(job.Complete){if(queue.Count>1)queue.Dequeue();continue;}if(job.Released)continue;
                if(job.State=="작업 예약"){Dispatch(job);continue;}if(job.State=="지시 검증 중"||job.State.StartsWith("지시 거절"))continue;
                if(Holding(job.Team)){job.State="팀 대기 중 · 이동 지시로 재개";continue;}
                if(Location(job.Team)!=job.Location){job.State="배치 변경 · 재지시 필요";continue;}
                Station.SetTaskTarget(job.Index,job.Order.Action);
                if(!Station.TeamRouteAvailable(job.Index)){job.State="경로 차단 · "+Station.TeamRouteReason(job.Index);continue;}
                if(!Station.TeamTaskArrived(job.Index)){job.State="이동 중";continue;}
                if(job.State!="현장 작업"){job.State="현장 작업";Director?.RecordTeamEvent("현장 도착 · "+Provenance(job),job.Team);Station.ShowTeamInteraction(job.Index);continue;}
                if(!Station.TeamInteractionFinished(job.Index))continue;
                if(job.OperationId!=null&&(OperationExecutionGate==null||!OperationExecutionGate(job.OperationId))){job.State="지원 꾸러미 확인 필요 · 실행 보류";continue;}job.Released=true;job.State="계산 통제 반영 대기";TaskReady?.Invoke(job.Order);
            }
            string selected=Workspace.SelectedTeamId;queues.TryGetValue(selected,out var selectedQueue);var current=selectedQueue!=null&&selectedQueue.Count>0?selectedQueue.Peek():null;
            string idleState=Holding(selected)?"위치 유지":Station.TeamArrived(Index(selected),Location(selected))?"배치 대기":"이동 중";
            if(current==null&&idleState=="이동 중"&&!Station.TeamRouteAvailable(Index(selected)))idleState="경로 차단 · "+Station.TeamRouteReason(Index(selected));
            var selectedAgency=Workspace.GetComponent<MvpAgencyDispatchController>();if(selectedAgency!=null&&!selectedAgency.GetTeamAvailability(selected))idleState=selectedAgency.AvailabilityReason(selected);
            string state=current?.State??idleState;string action=current==null?"대상에 우클릭으로 지시":current.Order.Action=="evacuate"?(current.Order.ReleasePolicy=="hold"?"집단 대기":"대피 유도")+(current.Order.CohortId<0?" · 전체":" · 집단 "+(current.Order.CohortId+1)):current.Order.Action=="warn"?"경고 방송":"의료 지원 요청";
            int pending=0;string next="";if(selectedQueue!=null)foreach(var job in selectedQueue){if(job==current||job.Complete)continue;pending++;if(pending<=2)next+=(next.Length>0?" · ":"")+(job.Order.Action=="warn"?"방송":job.Order.Action=="evacuate"?"집단 통제":"의료 요청");}
            int planned=Workspace.PlannedTeamOrders(selected);if(planned>0){pending+=planned;next+=(next.Length>0?" · ":"")+"배치 "+planned+"건";}
            string step=current==null?"평시 배치":current.Complete?"반영 확인":current.Released?"작업 전달 → 반영 대기":state=="현장 작업"?"현장 준비·작업":state=="이동 중"?"이동":"접수·준비";
            string task="위치 "+MvpWorkspace.Korean(Location(selected))+" · "+step+"\n"+(pending>0?"다음 "+pending+"건 · "+next:"목표 "+action);
            if(selectedAgency!=null&&!selectedAgency.GetTeamAvailability(selected))
            {
                state="출동 불가";task=selectedAgency.AvailabilityReason(selected);
                foreach(var mission in selectedAgency.MissionSnapshots)if(mission.TeamId==selected)
                {
                    state=MvpAgencyDispatchController.KoreanState(mission.State);
                    string context=mission.State=="Ready"?"중앙119안전센터 · 새 지원 요청 가능":mission.State=="Returning"?"부산역 → 중앙119안전센터":mission.State=="EnRoute"||mission.State=="Requested"?"중앙119안전센터 → 부산역":"중앙119안전센터 · 출동 자료 확인 필요";
                    task=context+"\n"+(mission.State=="Ready"?"기관 지원을 요청한 뒤 현장 도착을 기다리세요":mission.State=="Returning"?"복귀 중 · 현장 작업 지시 불가":"현장 도착 전 · 현장 작업 지시 불가");break;
                }
            }
            string card=selected+state+task;
            if(card!=lastCard){Workspace.SetSelectedTeamDisplay(MvpWorkspace.Korean(selected),"상태  "+state,task);lastCard=card;}
            Station.SetTeamSelection(Index(selected),current!=null&&!current.Complete?current.Location:idleState=="이동 중"?Location(selected):null);
            if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>()!=null)return;
            var keyboard=Keyboard.current;if(keyboard!=null){if(keyboard.spaceKey.wasPressedThisFrame)Director?.TogglePause();if(keyboard.digit1Key.wasPressedThisFrame)Workspace.SelectTeam("ops-1");if(selectedAgency==null&&keyboard.digit2Key.wasPressedThisFrame)Workspace.SelectTeam("fire-1");if(selectedAgency==null&&keyboard.digit3Key.wasPressedThisFrame)Workspace.SelectTeam("medical-1");if(keyboard.fKey.wasPressedThisFrame)Station.FocusReferenceHall();if(keyboard.escapeKey.wasPressedThisFrame){if(selectedAgency!=null&&selectedAgency.HasPendingTarget)selectedAgency.CancelTargetSelection();else CancelSelected();}}
            if(selectedAgency!=null&&selectedAgency.ProcessWorldInput())return;
            var mouse=Mouse.current;if(mouse==null||Station.ViewCamera==null||(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())||(Workspace.Router!=null&&Workspace.Router.ActiveModal!=null))return;
            if(mouse.leftButton.wasPressedThisFrame)
            {
                float best=32*32;int nearest=-1;for(int i=0;i<Station.Teams.Length;i++){if(!Station.Teams[i].gameObject.activeInHierarchy)continue;var p=Station.ViewCamera.WorldToScreenPoint(Station.Teams[i].position+Vector3.up);float d=((Vector2)p-mouse.position.ReadValue()).sqrMagnitude;if(p.z>0&&d<best){best=d;nearest=i;}}
                if(nearest>=0)Workspace.SelectTeam(nearest==0?"ops-1":nearest==1?"fire-1":"medical-1");
                else if(Director!=null&&Director.Physics!=null&&Director.Physics.LastResult!=null){best=24*24;foreach(var agent in Director.Physics.LastResult.agents){var p=Station.ViewCamera.WorldToScreenPoint(Station.transform.TransformPoint(MvpSpatialMetrics.Reference(agent.x,agent.z)+Vector3.up*.25f));float d=((Vector2)p-mouse.position.ReadValue()).sqrMagnitude;if(p.z>0&&d<best){best=d;Workspace.SelectCohort(agent.cohort);}}}
            }
            if(mouse.rightButton.wasPressedThisFrame)rightDown=mouse.position.ReadValue();
            if(mouse.rightButton.wasReleasedThisFrame&&(mouse.position.ReadValue()-rightDown).sqrMagnitude<16)
            {
                var ray=Station.ViewCamera.ScreenPointToRay(mouse.position.ReadValue());var plane=new Plane(Vector3.up,Station.transform.TransformPoint(MvpSpatialMetrics.Reference(0,0)));
                if(plane.Raycast(ray,out float distance)){var point=ray.GetPoint(distance);var local=Station.transform.InverseTransformPoint(point)-MvpSpatialMetrics.ReferenceOrigin;if(local.x>=0&&local.x<=30&&local.z>=0&&local.z<=20){Director?.Act("cohort_release");return;}string target="concourse";float best=float.MaxValue;foreach(string location in new[]{"platform","concourse","exit"}){float d=(Station.WorldAnchor(location)-point).sqrMagnitude;if(d<best){best=d;target=location;}}Workspace.SelectTarget(target);Workspace.BeginCommand("move");}
            }
        }
    }
}
