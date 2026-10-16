using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.Seeded.Unity
{
    /// <summary>Appearance follows physical velocity without applying root motion to the NavMesh owner.</summary>
    public sealed class StationNpcAnimation : MonoBehaviour
    {
        NavMeshAgent agent;
        Animator[] animators;
        void Start()
        {
            agent=GetComponent<NavMeshAgent>();
            animators=GetComponentsInChildren<Animator>().Where(a=>a.runtimeAnimatorController!=null&&a.parameters.Any(p=>p.name=="Speed"&&p.type==AnimatorControllerParameterType.Float)).ToArray();
            foreach(var animator in animators)animator.applyRootMotion=false;
        }
        void Update()
        {
            if(agent==null||animators==null)return;
            foreach(var animator in animators)animator.SetFloat("Speed",agent.velocity.magnitude);
        }
    }
}
