using System;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    /// <summary>A deterministic lesson actor; arrival is measured from physical navigation, never dialogue.</summary>
    public sealed class StationFireActor : MonoBehaviour
    {
        public string ActorId;
        public NavMeshAgent Agent { get; private set; }
        public bool Warned { get; private set; }
        public bool Following { get; private set; }
        public bool Arrived { get; private set; }
        public string Failure { get; private set; } = "";
        public Vector3 Destination { get; private set; }
        public bool WaitingForGuide { get; private set; }
        public bool HasGoal => hasGoal;
        Animator animator;
        Transform guide;
        float walkStride = 1f;
        bool hasGoal;
        static readonly int Speed = Animator.StringToHash("Speed"), Pace = Animator.StringToHash("Pace");

        public void Initialize(string id, GameObject visualPrefab, RuntimeAnimatorController controller)
        {
            ActorId = id;
            if (visualPrefab == null) throw new ArgumentNullException(nameof(visualPrefab));
            var visual = Instantiate(visualPrefab, transform, false);
            visual.name = "기존 인물 모델";
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var navigation in visual.GetComponentsInChildren<NavMeshAgent>(true)) navigation.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                if (controller != null && animator.avatar != null && animator.avatar.isHuman) animator.runtimeAnimatorController = controller;
                if (animator.runtimeAnimatorController != null)
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                        if (clip.name.Contains("_walk_") && clip.averageSpeed.magnitude > .1f) walkStride = clip.averageSpeed.magnitude;
            }
            var body = gameObject.AddComponent<CapsuleCollider>();
            body.radius = .25f; body.height = 1.7f; body.center = Vector3.up * .85f;
            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.radius = .28f; Agent.height = 1.72f; Agent.speed = 1.05f; Agent.acceleration = 3f;
            Agent.angularSpeed = 180; Agent.stoppingDistance = .22f; Agent.autoRepath = true;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            Agent.avoidancePriority = id == "specialist" ? 20 : 50;
        }
        void OnEnable()
        { if (Agent != null && !Agent.isOnNavMesh) Failure = "인물 시작점이 실제 보행면에 연결되지 않았습니다"; }
        public void Warn(Transform speaker)
        {
            Warned = true;
            if (!Following) guide = speaker;
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger("Listen");
        }
        public bool RetreatFrom(Vector3 danger, Transform speaker)
        {
            Warn(speaker);
            var away=transform.position-danger;away.y=0;
            if(away.sqrMagnitude<.01f)return false;
            var desired=transform.position+away.normalized*.85f;
            return NavMesh.SamplePosition(desired,out var hit,.35f,NavMesh.AllAreas)&&Mathf.Abs(hit.position.y-transform.position.y)<.25f&&WalkTo(hit.position);
        }
        public bool EscortTo(Transform staff, Vector3 destination)
        {
            guide=staff;Following=true;Warned=true;return WalkTo(destination,ActorId=="passenger:3"?.9f:1.05f);
        }
        public bool WalkTo(Vector3 destination, float speed = 1.05f)
        {
            if (Agent == null || !Agent.isOnNavMesh) return Fail("보행면 연결이 없습니다");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                return Fail("실제 통로가 막혀 있습니다");
            Destination = destination; Agent.speed = speed; Agent.isStopped = false; Agent.SetPath(path);
            if(!Following)guide=null;
            hasGoal = true; Arrived = false; Failure = ""; return true;
        }
        public bool FollowTo(Transform person, Vector3 destination)
        {
            if (person == null || !Warned) return false;
            guide = person; Following = true;
            return WalkTo(destination, ActorId == "passenger:3" ? .9f : 1.05f);
        }
        public void Stop()
        {
            if (Agent != null && Agent.isOnNavMesh) { Agent.isStopped = true; Agent.ResetPath(); }
            hasGoal = false; Following = false; WaitingForGuide = false;
        }
        bool Fail(string message) { Failure = message; return false; }
        void Update()
        {
            if (Agent == null || !Agent.isOnNavMesh) return;
            if (hasGoal)
            {
                WaitingForGuide = Following && guide != null && Vector3.Distance(transform.position, guide.position) > 5f;
                Agent.isStopped = WaitingForGuide;
                if (!Agent.pathPending && Vector3.Distance(transform.position, Destination) < .48f)
                { Agent.ResetPath(); hasGoal = false; Following = false; Arrived = true; Agent.isStopped = true; }
                else if (!Agent.pathPending && Agent.pathStatus == NavMeshPathStatus.PathInvalid) Failure = "이동 경로를 다시 확인해 주세요";
            }
            else if (Warned && guide != null)
            {
                var direction = guide.position - transform.position; direction.y = 0;
                if (direction.sqrMagnitude > .05f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 180 * Time.deltaTime);
            }
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                float scale = Mathf.Max(.1f, animator.humanScale * animator.speed);
                float gait = Agent.velocity.magnitude / Mathf.Max(.1f, walkStride * scale);
                animator.SetFloat(Speed, Mathf.Min(1, gait), .1f, Time.deltaTime);
                animator.SetFloat(Pace, Mathf.Clamp(gait, .3f, 1.6f));
            }
        }
    }
}
