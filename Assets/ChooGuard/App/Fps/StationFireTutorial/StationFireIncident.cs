using System.Collections;
using System.Linq;
using ChooGuard.App.Fps;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        Transform incidentBench;
        Vector3 incidentFirePoint;
        Vector3[] passengerStarts,passengerGoals;
        Vector3 staffAStart,staffBStart,specialistStart;
        bool warningsIssued,workersResponding,arrivalScheduled,arrivalTransition;
        public bool ResponseWithdrawn {get;private set;}
        public Vector3 SuppressionPoint {get;private set;}
        public Vector3 ExtinguisherPickupPoint {get;private set;}
        public bool PassengersSafe=>warningsIssued&&passengers.Count>0&&passengers.All(p=>p.Arrived);
        public bool SuppressionEnded=>WithdrawalRequested||State.Entities[ExtinguisherId].Get("mech.charge")<=0||
            State.Entities[FireId].Get("fire")<=.01&&State.Entities[FireId].Get("mech.agent-deposit")>0;

        void InitializeIncidentActors()
        {
            StaffA=CreateActor("staff:a",StaffPrefab,staffAStart);
            StaffB=CreateActor("staff:b",StaffPrefab,staffBStart);
            Specialist=CreateActor("specialist",SpecialistPrefab,specialistStart);
            Specialist.gameObject.SetActive(false);
            for(int i=0;i<passengerStarts.Length;i++)passengers.Add(CreateActor("passenger:"+i,PassengerPrefabs[i%PassengerPrefabs.Length],passengerStarts[i]));
            Physics.SyncTransforms();StartCoroutine(InitialIncidentReaction());
        }
        IEnumerator InitialIncidentReaction()
        {
            yield return new WaitUntil(()=>Ready&&Captured);
            foreach(var person in passengers)
            {
                person.Warn(Player.transform);
                var away=person.transform.position-FireTarget.position;away.y=0;
                var next=person.transform.position+away.normalized*.45f;
                if(UnityEngine.AI.NavMesh.SamplePosition(next,out var hit,.25f,UnityEngine.AI.NavMesh.AllAreas)&&
                    ClearSight(StartFeet+Vector3.up*1.4f,hit.position+Vector3.up*1.25f))person.WalkTo(hit.position);
            }
            Speak("승객","저쪽 의자에서 연기가 나요! 어느 쪽으로 나가야 하나요?",-1);
            Record("passengers-notice-real-chair-fire");
        }
        public bool InteractAimed()
        {
            if(!Captured||!Running||RadioInputBlocked||View==null)return false;
            Physics.SyncTransforms();
            var hit=Physics.RaycastAll(View.ViewportPointToRay(new Vector3(.5f,.5f,0)),2.5f,~(1<<2),QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.transform.IsChildOf(Player.transform)).OrderBy(h=>h.distance).FirstOrDefault();
            var target=hit.collider!=null?hit.collider.GetComponentInParent<StationFireActor>():null;
            if(target==null)return false;
            Record("actor-contact:"+target.ActorId+"; arrived="+target.Arrived+"; result="+ResultReported+"; flow="+State.Entities[ExtinguisherId].Get("mech.flow"));
            if(target.ActorId.StartsWith("passenger:"))
            {
                if(!warningsIssued)
                {
                    warningsIssued=true;
                    for(int i=0;i<passengers.Count;i++){passengers[i].Warn(Player.transform);passengers[i].WalkTo(passengerGoals[i]);}
                    Speak("역무원","불이 났습니다. 화점에서 떨어져 이쪽 열린 통로로 이동해 주세요. 동료 직원이 계속 안내하겠습니다.",-1);
                    Record("nearby-passengers-directed-to-open-aisle");PhysicalActionCount++;
                }
                else Speak("역무원","화점으로 돌아가지 마시고 안내한 통로를 따라 이동해 주세요.",-1);
                return true;
            }
            if(target==StaffA||target==StaffB)
            {
                Speak("동료",Step==StationFireStep.Specialist?"확인한 상황은 표시된 소방대에게 전달해 주세요. 저는 주변 승객 안내를 지원하겠습니다.":
                    ReportReceived?"주변 승객 안내를 맡고 있습니다. 현장 변화는 무전으로 알려 주세요.":"화재 위치와 필요한 지원을 Q 무전으로 전달해 주세요.",-1);
                // A handled conversation is not evidence that fire service received the situation.
                return true;
            }
            if(target==Specialist&&Specialist.Arrived&&ResultReported&&!SpecialistReceived&&
                State.Entities[ExtinguisherId].Get("mech.flow")==0)
            {
                SpecialistReceived=true;ResponseWithdrawn=State.Entities[FireId].Get("fire")>.01;
                int sent=Speak("역무원",ObservedResponseSummary(),-1);
                Record("observed-actions-and-unknowns-shared-with-fire-service");PhysicalActionCount++;
                Specialist.WalkTo(SuppressionPoint,.9f);StartCoroutine(CompleteAfterInspectionBegins(sent));return true;
            }
            return false;
        }
        string ObservedResponseSummary()
        {
            bool used=State.Entities[ExtinguisherId].Get("mech.charge")<.999;
            string action=used?(State.Entities[FireId].Get("fire")<=.01?"저 의자에 소화기를 사용해 보이는 불꽃은 줄었습니다. ":"저 의자에 소화기를 사용했지만 불이 남아 있습니다. "):"저 의자에서 불이 났으며 소화기는 아직 사용하지 못했습니다. ";
            if(State.Entities[ExtinguisherId].Get("mech.charge")<=0)action+="약제가 소진되어 추가 분사를 할 수 없습니다. ";
            else if(WithdrawalRequested)action+="초기소화를 중단했습니다. ";
            string people=warningsIssued?"주변 승객에게 대피 방향을 안내했습니다. ":"주변 승객에게 대피 방향을 안내하지 못했습니다. ";
            return action+people+"잔불과 미확인 인원은 추가 확인이 필요합니다.";
        }
        void ObserveIncident()
        {
            if(ReportReceived&&!workersResponding)
            {
                workersResponding=true;StaffA.WalkTo(ControlPoint);StaffB.WalkTo(OnwardPoint,.95f);
                Record("coworkers-support-without-player-escort");
            }
            if(!StaffReceived&&PassengersSafe){StaffReceived=true;Record("nearby-passengers-reached-aisle-without-player-escort");}
            if(ResultReported&&!arrivalScheduled){arrivalScheduled=true;StartCoroutine(ArriveAfterTimeTransition());}
            if(SpecialistReceived||Step==StationFireStep.Complete)return;
            StationFireStep next;
            if(SuppressionEnded)
                next=State.Entities[ExtinguisherId].Get("mech.flow")>0||Controls.HeldPart?.PartId=="lever"?StationFireStep.Release:
                    !ResultReported?StationFireStep.ResultReport:StationFireStep.Specialist;
            else if(!warningsIssued)next=StationFireStep.Warning;
            else if(!ReportReceived)next=StationFireStep.Report;
            else next=StationFireStep.Extinguish;
            if(next!=Step)SetStep(next);
        }
        IEnumerator ArriveAfterTimeTransition()
        {
            yield return new WaitUntil(()=>DialogueFinished(resultDialogue));
            // Arrival time is explicitly compressed; never pretend the real service arrived seconds after the call.
            arrivalTransition=true;CloseRadio(false);Controls.ResetInputState();ReleaseEquipmentPressure();
            // A's local control role gives way to the arriving service. B stays with the passenger flow.
            // Use the existing qualified side position and an actual fire-clear route, never warp a person away.
            if(RouteKeepsClearOfFire(StaffA.transform.position,staffAStart)&&StaffA.WalkTo(staffAStart,1.1f))
                Record("coworker-a-clears-fire-service-approach-on-foot");
            var panel=FpsUiFactory.Block(hudRoot,"시간 경과",new Vector2(.5f,.5f),Vector2.zero,new Vector2(2200,1300),Color.black).gameObject;
            var text=FpsUiFactory.Label(panel.transform,KoreanFont,"시간 경과 안내",Vector2.zero,new Vector2(1000,130),26);
            text.text="잠시 후 · 신고를 받은 소방대가 도착했습니다\n<size=20>안전한 통로에서 확인한 상황과 실시한 조치를 전달하세요</size>";
            yield return new WaitForSeconds(2.5f);
            Specialist.gameObject.SetActive(true);Specialist.WalkTo(ReceiptPoint);
            Destroy(panel);arrivalTransition=false;Controls.GuardCaptureClick();
            Record("explicit-time-transition-fire-service-approaches");
        }
        IEnumerator CompleteAfterInspectionBegins(int sent)
        {
            yield return new WaitUntil(()=>DialogueFinished(sent));
            int received=Speak("소방대","전달받았습니다. 화점과 잔불 여부를 확인하겠습니다. 안전한 쪽에서 협조해 주세요.",-1);
            yield return new WaitUntil(()=>DialogueFinished(received));
            SetStep(StationFireStep.Complete);
        }
    }
}
