using System;
using System.Linq;
using UnityEngine;

namespace ChooGuard.Seeded.Unity
{
    public sealed partial class DetailedManipulationController
    {
        DetailedPartBinding latchedPress;
        DetailedPartBinding pickupTarget;
        public string FocusedEquipmentId=>pickupTarget!=null?pickupTarget.OwnerId:null;
        double latchedDuration;
        string interactionFeedback;
        float interactionFeedbackUntil;
        public string InteractionFeedback=>Time.unscaledTime<interactionFeedbackUntil?interactionFeedback:null;
        public bool IsPressLatched(string owner)=>latchedPress!=null&&latchedPress.OwnerId==owner;
        // Return false to retain the carried item and explain a rejected socket placement.
        public Func<string,bool> BeforePlace;
        void AccumulateLatched(float dt){if(latchedPress!=null)latchedDuration+=dt;}
        void FlushLatched()
        {
            if(latchedPress==null||latchedDuration<=0)return;
            pending.Enqueue(Input(latchedPress,"press",1,Math.Min(Mechanics.World.StepSeconds,latchedDuration)));latchedDuration=0;
        }
        public void StopLatchedPress()
        {
            if(latchedPress==null)return;
            FlushLatched();pending.Enqueue(Input(latchedPress,"release"));latchedPress=null;
        }
        void SayInteraction(string value){interactionFeedback=value;interactionFeedbackUntil=Time.unscaledTime+3;Hint=value;}
        DetailedPartBinding CarryPart(string owner)=>poses.Values.FirstOrDefault(p=>p.Binding.OwnerId==owner&&p.Definition.Carryable)?.Binding;
        void UpdatePickupTarget(Ray ray)
        {
            pickupTarget=aimed!=null&&equipment.ContainsKey(aimed.OwnerId)?CarryPart(aimed.OwnerId):null;
            if(pickupTarget!=null)return;
            var hit=Physics.SphereCastAll(ray,.06f,2.4f,~(1<<2),QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.transform.IsChildOf(host.Actor)).OrderBy(h=>h.distance).FirstOrDefault();
            var part=hit.collider!=null?hit.collider.GetComponentInParent<DetailedPartBinding>():null;
            if(part!=null&&equipment.ContainsKey(part.OwnerId))pickupTarget=CarryPart(part.OwnerId);
        }
        bool TryDirectCarry()
        {
            if(pickupTarget==null)
            {SayInteraction("가까운 장비를 바라보면 F로 집을 수 있습니다. 벽 너머나 거리 밖에서는 집을 수 없습니다.");return equipment.Count>0;}
            var body=pickupTarget;if(body==null)return false;
            var other=equipment.Keys.FirstOrDefault(id=>id!=body.OwnerId&&host.State.Entities["player"].Get("mech.carry."+id)>0);
            if(other!=null)
            {
                if(equipment[other].DockAnchor==host.Actor)QueueDirectPlace(CarryPart(other));
                else {SayInteraction("먼저 들고 있는 장비를 G로 놓아 손을 비우세요.");return true;}
            }
            if(held!=null){FlushHeld();pending.Enqueue(Input(held,"release"));held=null;}
            foreach(var action in new[]{"grip","carry","release"})
            {var input=Input(body,action);input.Contact=true;pending.Enqueue(input);}
            interactionFeedback=null;return true;
        }
        bool TryDirectPlace()
        {
            var owner=equipment.Keys.FirstOrDefault(id=>host.State.Entities["player"].Get("mech.carry."+id)>0);
            if(owner==null)return false;
            if(BeforePlace!=null&&!BeforePlace(owner))return true;
            QueueDirectPlace(CarryPart(owner));return true;
        }
        void QueueDirectPlace(DetailedPartBinding body)
        {
            if(body==null)return;
            if(held!=null&&held.OwnerId==body.OwnerId){FlushHeld();pending.Enqueue(Input(held,"release"));held=null;}
            if(!TryExplicitPlacement(body.OwnerId,out var point,out var rotation))return;
            var grip=Input(body,"grip");grip.Contact=true;pending.Enqueue(grip);
            var place=Input(body,"place");place.Contact=true;place.X=point.x;place.Y=point.y;place.Z=point.z;
            placementRotations[place.Sequence]=rotation;pending.Enqueue(place);var release=Input(body,"release");release.Contact=true;pending.Enqueue(release);
        }
    }
}
