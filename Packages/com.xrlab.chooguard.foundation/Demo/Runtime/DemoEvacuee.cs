using System.Linq;
using UnityEngine;
namespace ChooGuard.Foundation.Demo
{
    public enum EvacueeState { Wander, FollowPlayer, AssemblyWait, AwaitLeader }
    public sealed class DemoEvacuee : MonoBehaviour
    {
        [SerializeField] private string groupId;
        [SerializeField] private int index;
        [SerializeField] private Vector3 home;
        [SerializeField] private Vector3 waitingSlot;
        private Vector3[] route = new Vector3[0];
        private int cursor;
        private float nextRoute;
        private float clock;
        private Transform[] limbs;
        private DemoEvacuee[] peers;
        private Vector3[] routineStops;
        private int routineIndex;
        private float routineDwell;
        private int graphVersion=-1;
        private bool hasEscortPortal;
        private Vector3 escortPortal;
        private bool leaderCrossedPortal;
        private bool approachedPortal;
        public bool EscortPortalVisited {get;private set;}
        public int RoutineVisits {get;private set;}
        public string NpcId {get{return "npc-"+index;}}
        public void ConfigureRoutine(Vector3[] stops)
        {routineStops=(Vector3[])stops.Clone();routineIndex=index%stops.Length;routineDwell=1+index*.3f;RoutineVisits=0;InvalidateRoute();}
        public void AwaitLeader()
        {if(State==EvacueeState.Wander){State=EvacueeState.AwaitLeader;InvalidateRoute();Animate(false);}}
        public void ResumeRoutine()
        {State=EvacueeState.Wander;hasEscortPortal=false;EscortPortalVisited=false;leaderCrossedPortal=false;InvalidateRoute();}
        public void SetEscortPortal(Vector3 portal)
        {escortPortal=portal;hasEscortPortal=true;EscortPortalVisited=false;leaderCrossedPortal=false;approachedPortal=false;InvalidateRoute();}
        public void LeaderCrossedPortal(){leaderCrossedPortal=true;InvalidateRoute();}
        public void DisableRoutine(){routineStops=null;hasEscortPortal=false;}
        public void InvalidateRoute(){route=new Vector3[0];cursor=0;nextRoute=0;}

        public string GroupId { get { return groupId; } }
        public EvacueeState State { get; private set; }
        public Vector3 WaitingSlot { get { return waitingSlot; } }
        public void Configure(string group, int number, Vector3 spawn, Vector3 slot)
        { groupId=group;index=number;home=spawn;waitingSlot=slot;ResetActor(); }
        public void ResetActor()
        {
            transform.position=home;transform.rotation=Quaternion.identity;State=EvacueeState.Wander;
            route=new Vector3[0];cursor=0;nextRoute=0;clock=0;hasEscortPortal=false;EscortPortalVisited=false;leaderCrossedPortal=false;RoutineVisits=0;
            if(limbs!=null) foreach(var limb in limbs)limb.localRotation=Quaternion.identity;
        }
        public bool Recruit()
        { if(State!=EvacueeState.Wander&&State!=EvacueeState.AwaitLeader)return false;State=EvacueeState.FollowPlayer;nextRoute=0;return true; }
        // Called only by the game tick: pause/focus loss stops all movement and timers.
        public void Tick(float dt, Transform leader, DemoWalkGraph graph, Vector3 assembly)
        {
            if(dt<=0||State==EvacueeState.AwaitLeader||State==EvacueeState.AssemblyWait||leader==null||graph==null)return;
            clock+=dt;
            if(graphVersion!=graph.Version){graphVersion=graph.Version;InvalidateRoute();}
            var nearAssembly=Horizontal(leader.position,assembly)<2.0f;
            var destination=State==EvacueeState.Wander
                ? home+new Vector3(Mathf.Sin(clock*.3f+index)*.65f,0,Mathf.Cos(clock*.3f+index)*.65f)
                : nearAssembly ? waitingSlot : leader.position;
            if(State==EvacueeState.Wander&&routineStops!=null&&routineStops.Length>0)
            {
                destination=routineStops[routineIndex];
                if(Horizontal(transform.position,destination)<.4f)
                {
                    routineDwell-=dt;Animate(false);
                    if(routineDwell<=0){RoutineVisits++;routineIndex=(routineIndex+1)%routineStops.Length;routineDwell=1.5f+index*.3f;InvalidateRoute();}
                    return;
                }
            }
            if(State==EvacueeState.FollowPlayer&&hasEscortPortal&&!EscortPortalVisited)
            {
                var portalOffset=transform.position-escortPortal;
                if(Mathf.Abs(portalOffset.x)>2.5f||Mathf.Abs(portalOffset.z)>1.5f)approachedPortal=false;
                if(Mathf.Abs(portalOffset.x)<2.5f&&portalOffset.z<-.15f&&portalOffset.z>-1.5f)approachedPortal=true;
                if(approachedPortal&&Mathf.Abs(portalOffset.x)<2.5f&&portalOffset.z>.15f)EscortPortalVisited=true;
                else if(leaderCrossedPortal||nearAssembly)destination=escortPortal+Vector3.forward*.7f;
            }
            var distance=Horizontal(transform.position,destination);
            if(State==EvacueeState.FollowPlayer && nearAssembly && distance<.38f && (!hasEscortPortal||EscortPortalVisited))
            {State=EvacueeState.AssemblyWait;Animate(false);return;}
            if(State==EvacueeState.FollowPlayer && !nearAssembly && !(hasEscortPortal&&!EscortPortalVisited&&leaderCrossedPortal) && distance<1.5f+index*.12f)
            {Animate(false);return;}
            nextRoute-=dt;
            if(nextRoute<=0)
            {
                route=graph.Route(transform.position,destination);cursor=0;nextRoute=.65f+index*.04f;
                if(route.Length>1&&graph.Clear(transform.position,route[1]))cursor=1;
                else if(route.Length==1&&graph.Clear(transform.position,destination))route=new[]{new Vector3(destination.x,.1f,destination.z)};
            }
            if(route.Length==0){Animate(false);return;}
            while(cursor<route.Length && Horizontal(transform.position,route[cursor])<.15f &&
                (cursor==route.Length-1 || graph.Clear(transform.position,route[cursor+1])))cursor++;
            Vector3 target;
            if(cursor<route.Length)target=route[cursor];
            else if(graph.Clear(transform.position,destination))target=new Vector3(destination.x,.1f,destination.z);
            else {Animate(false);return;}
            var movement=target-transform.position;movement.y=0;
            var speed=State==EvacueeState.Wander?(routineStops!=null?1.15f:.5f):2.65f;
            var delta=Vector3.ClampMagnitude(movement,speed*dt);
            if(peers==null)peers=transform.parent!=null?transform.parent.GetComponentsInChildren<DemoEvacuee>():new DemoEvacuee[0];
            foreach(var peer in peers)
            {
                if(peer==this || (peer.State!=EvacueeState.AssemblyWait && peer.index>index))continue;
                var away=transform.position-peer.transform.position;away.y=0;
                var separation=away.magnitude;
                if(separation<.62f && separation>.001f)delta+=away.normalized*(.62f-separation)*dt*4;
            }
            delta=Vector3.ClampMagnitude(delta,speed*dt);
            if(graph.Clear(transform.position,transform.position+delta))transform.position+=delta;
            if(movement.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(movement),dt*9);
            Animate(delta.sqrMagnitude>.00001f);
        }
        private void Animate(bool moving)
        {
            if(limbs==null)limbs=GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftArm_Joint"||t.name=="RightArm_Joint"||t.name=="LeftLeg_Joint"||t.name=="RightLeg_Joint").ToArray();
            foreach(var limb in limbs)
            {
                var sign=limb.name.StartsWith("Left")?1:-1;
                if(limb.name.Contains("Arm"))sign=-sign;
                limb.localRotation=Quaternion.Euler(moving?Mathf.Sin(clock*7)*sign*8:0,0,0);
            }
        }
        public static float Horizontal(Vector3 a,Vector3 b){a.y=0;b.y=0;return Vector3.Distance(a,b);}
    }
}
