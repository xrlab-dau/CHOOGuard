using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    /// <summary>NavMeshAgent is the only physical velocity owner. Core goals and physical telemetry stay distinct.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SeededNpcController : MonoBehaviour
    {
        public string EntityId;
        public NavMeshAgent Agent { get; private set; }
        public string LastFailure="";
        public bool PendingFailure,PendingArrival;
        Vector3 lastGoal=new Vector3(float.NaN,0,0);
        void Awake()=>Initialize();
        public void Initialize()
        {
            Agent=GetComponent<NavMeshAgent>();Agent.radius=.28f;Agent.height=1.72f;Agent.baseOffset=0;Agent.speed=1.3f;Agent.angularSpeed=180;Agent.acceleration=4;
            Agent.updatePosition=true;Agent.updateRotation=true;Agent.autoRepath=true;Agent.autoTraverseOffMeshLink=true;
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        }
        public void Project(Entity entity)
        {
            if(Agent==null||!Agent.isOnNavMesh){LastFailure="not-on-navmesh";PendingFailure=true;return;}
            Agent.speed=(float)(1.3*entity.Get("mobility",1)*System.Math.Max(.15,entity.Get("health",1)));
            if(entity.Get("health",1)<=.05){Agent.isStopped=true;return;}
            Agent.isStopped=false;
            var goal=new Vector3((float)entity.Get("goal.x",entity.X),(float)entity.Get("goal.y",entity.Y),(float)entity.Get("goal.z",entity.Z));
            if(entity.Get("goal.active",0)<.5){if(Agent.hasPath)Agent.ResetPath();lastGoal=new Vector3(float.NaN,0,0);return;}
            if(!Agent.pathPending&&Agent.hasPath&&Agent.remainingDistance<.5f){PendingArrival=true;Agent.ResetPath();lastGoal=new Vector3(float.NaN,0,0);return;}
            if((goal-lastGoal).sqrMagnitude<.05f)return;
            if(!NavMesh.SamplePosition(goal,out var hit,.8f,NavMesh.AllAreas)||Mathf.Abs(hit.position.y-goal.y)>.4f){LastFailure="goal-not-on-same-surface";PendingFailure=true;return;}
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(Agent.transform.position,hit.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete){LastFailure="incomplete-path";PendingFailure=true;return;}
            Agent.SetPath(path);lastGoal=goal;LastFailure="";
        }
    }
}
