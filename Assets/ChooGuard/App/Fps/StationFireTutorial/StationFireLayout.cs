using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        Transform sourceExtinguisher;
        public string ExtinguisherSourceName=>sourceExtinguisher!=null?sourceExtinguisher.name:"";
        public Vector3 OriginalExtinguisherPosition {get;private set;}
        public string IncidentSourceName=>incidentBench!=null?incidentBench.name:"";
        void ResolveIncidentLayout()
        {
            var objects=stationScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            sourceExtinguisher=objects.SingleOrDefault(t=>t.name=="C165_S01_P5_SOURCE_LEFT_ENTRY_PORTABLE_EXTINGUISHER"&&t.gameObject.activeInHierarchy);
            incidentBench=objects.SingleOrDefault(t=>t.name=="C165_S01_OBSERVED_CHAIR_1_0_1"&&t.gameObject.activeInHierarchy);
            if(sourceExtinguisher==null||incidentBench==null)throw new InvalidOperationException("기존 매장 입구 소화기와 목재 의자를 찾지 못했습니다. 임시 장비를 추가하지 않습니다.");
            OriginalExtinguisherPosition=sourceExtinguisher.position;
            var seat=incidentBench.GetComponentsInChildren<Renderer>().FirstOrDefault(r=>r.name=="bentwood_seat_rim");
            if(seat==null)throw new InvalidOperationException("기존 목재 의자의 실제 좌판을 찾지 못했습니다");
            incidentFirePoint=new Vector3(seat.bounds.center.x,seat.bounds.max.y+.04f,seat.bounds.center.z);
            var toward=sourceExtinguisher.position-incidentBench.position;toward.y=0;toward.Normalize();
            layoutRotation=Quaternion.LookRotation(-toward,Vector3.up);
            anchor=incidentBench.position+toward*3.7f;anchor.y=0;
            bool foundStart=false;
            foreach(float angle in new[]{0f,30,-30,60,-60,90,-90,120,-120,180})
            {
                var offset=Quaternion.Euler(0,angle,0)*toward*3.3f;
                try
                {
                    var candidate=OpenPoint(incidentBench.position+offset,.6f);
                    if(!ClearSight(candidate+Vector3.up*1.6f,incidentFirePoint+Vector3.up*.2f,incidentBench))continue;
                    StartFeet=candidate+Vector3.up*.03f;foundStart=true;break;
                }
                catch(InvalidOperationException){} // Candidate rejected; original map remains unchanged.
            }
            if(!foundStart)throw new InvalidOperationException("의자 화재를 직접 볼 수 있는 열린 시작 위치가 없습니다");
            anchor=StartFeet-Vector3.up*.03f;
            var facing=incidentFirePoint-StartFeet;facing.y=0;layoutRotation=Quaternion.LookRotation(facing,Vector3.up);
            ExtinguisherPickupPoint=OpenPoint(sourceExtinguisher.position-toward*.8f,.65f);
            SuppressionPoint=OpenPoint(incidentBench.position+toward*2.5f,.65f);
            // Existing entrance threshold23390872, extended toward the public aisle. Map-derived, not a surveyed evacuation plan.
            OnwardPoint=OpenPoint(new Vector3(-25.14f,anchor.y,-3.38f),1.0f);
            ReceiptPoint=OpenPoint(new Vector3(-24.35f,anchor.y,-2.65f),.85f);
            ControlPoint=OpenPoint(At(-1.4f,-1.1f),.7f);
            staffAStart=OpenPoint(OnwardPoint+layoutRotation*new Vector3(-1.8f,0,-1.2f),1.0f);
            staffBStart=OpenPoint(OnwardPoint+layoutRotation*new Vector3(1.8f,0,-1.8f),1.0f);
            specialistStart=OpenPoint(OnwardPoint+layoutRotation*new Vector3(0,0,-2.8f),1.0f);
            passengerStarts=new Vector3[4];passengerGoals=new Vector3[4];
            var occupied=new List<Vector3>{StartFeet,staffAStart,staffBStart};
            var offsets=new[]{new Vector2(-1.05f,0),new Vector2(1.1f,-.1f),new Vector2(-.8f,-1.15f),new Vector2(1.25f,-1.25f)};
            for(int i=0;i<4;i++)
            {
                passengerStarts[i]=OpenPoint(At(offsets[i].x,offsets[i].y),1.4f,occupied,true);occupied.Add(passengerStarts[i]);
                passengerGoals[i]=OpenPoint(OnwardPoint+layoutRotation*new Vector3((i%2)*.85f-.4f,0,-1-i/2*.8f),1.1f);
                RequireRoute(passengerStarts[i],passengerGoals[i]);
            }
            RequireRoute(StartFeet,ExtinguisherPickupPoint);RequireRoute(ExtinguisherPickupPoint,SuppressionPoint);
            RequireRoute(SuppressionPoint,ReceiptPoint);RequireRoute(specialistStart,ReceiptPoint);
            Record("incident-chair:"+incidentBench.name+"; original-extinguisher:"+sourceExtinguisher.name+"; separation="+Vector3.Distance(incidentBench.position,sourceExtinguisher.position));
        }
        bool ClearSight(Vector3 eye,Vector3 point,Transform allowed=null)
        {
            var delta=point-eye;
            return !Physics.RaycastAll(eye,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
                .Any(h=>allowed==null||!h.collider.transform.IsChildOf(allowed));
        }
        Vector3 OpenPoint(Vector3 desired,float radius,IEnumerable<Vector3> occupied=null,bool visibleFromStart=false)
        {
            desired.y=anchor.y;
            for(int ring=0;ring<=4;ring++)for(int angle=0;angle<(ring==0?1:16);angle++)
            {
                float theta=angle*Mathf.PI/8;var trial=desired+new Vector3(Mathf.Cos(theta),0,Mathf.Sin(theta))*radius*ring/4;
                if(!NavMesh.SamplePosition(trial,out var hit,.23f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-anchor.y)>.25f)continue;
                if(occupied!=null&&occupied.Any(v=>Vector2.Distance(new Vector2(v.x,v.z),new Vector2(hit.position.x,hit.position.z))<.72f))continue;
                if(Physics.CheckCapsule(hit.position+Vector3.up*.34f,hit.position+Vector3.up*1.42f,.28f,~0,QueryTriggerInteraction.Ignore))continue;
                if(visibleFromStart&&!ClearSight(StartFeet+Vector3.up*1.4f,hit.position+Vector3.up*1.25f))continue;
                return hit.position;
            }
            throw new InvalidOperationException("기존 현장에서 서 있을 열린 보행 위치가 없습니다: "+desired);
        }
        void RequireRoute(Vector3 from,Vector3 to)
        {
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(from,to,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("기존 소화기와 현장을 잇는 보행 경로가 없습니다: "+from+" → "+to);
            float length=0;
            for(int i=1;i<path.corners.Length;i++)
            {
                var a=path.corners[i-1];var b=path.corners[i];length+=Vector3.Distance(a,b);
                var start=a+Vector3.up*.4f;var end=a+Vector3.up*1.35f;var delta=b-a;
                if(Physics.CapsuleCastAll(start,end,.25f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>h.distance<delta.magnitude-.08f))
                    throw new InvalidOperationException("표시할 통로에 실제 장애물이 있습니다: "+from+" → "+to);
            }
            if(length>18)throw new InvalidOperationException("튜토리얼 동선이 불필요하게 멉니다: "+length);
        }
    }
}
