using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Both hands on the shoulders of the person being walked off (a paramedic supporting a patient from behind). Humanoid IK
    /// on the base layer (the crowd controller runs its IK pass); the hands ease on once the patient is within reach and ease
    /// off when they part. Added for the walk and removed after it (<see cref="PersonBody.SupportFromBehind"/>).
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class SupportHands : MonoBehaviour
    {
        private const float Reach = .95f, EaseSeconds = .4f;

        public Animator Target;
        private Animator animator;
        private float weight;

        private void Awake() => animator = GetComponent<Animator>();

        private void OnAnimatorIK(int layer)
        {
            bool near = Target != null && (Target.transform.position - transform.position).sqrMagnitude < Reach * Reach;
            weight = Mathf.MoveTowards(weight, near ? 1 : 0, Time.deltaTime / EaseSeconds);
            Hold(AvatarIKGoal.LeftHand, HumanBodyBones.LeftUpperArm);
            Hold(AvatarIKGoal.RightHand, HumanBodyBones.RightUpperArm);
        }

        private void Hold(AvatarIKGoal goal, HumanBodyBones bone)
        {
            var shoulder = Target != null ? Target.GetBoneTransform(bone) : null;
            animator.SetIKPositionWeight(goal, shoulder != null ? weight : 0);
            if (shoulder != null) animator.SetIKPosition(goal, shoulder.position + Vector3.up * .04f);
        }
    }
}
