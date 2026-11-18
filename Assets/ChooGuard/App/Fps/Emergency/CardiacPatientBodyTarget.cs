using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>The lesson clone's collision/aim body follows its actual pose instead of leaving an invisible standing capsule.</summary>
    public sealed class CardiacPatientBodyTarget : MonoBehaviour
    {
        CapsuleCollider[] original;
        CapsuleCollider posedBody;
        Transform head, leftFoot, rightFoot;
        public void Initialize(PersonBody body)
        {
            var animator = body.Animator;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            original = body.GetComponentsInChildren<CapsuleCollider>();
            foreach (var collider in original) collider.enabled = false;
            var go = new GameObject("승객 자세에 맞는 조작 범위"); go.transform.SetParent(body.transform, false);
            posedBody = go.AddComponent<CapsuleCollider>(); posedBody.direction = 2; posedBody.radius = .19f;
            LateUpdate();
        }
        void LateUpdate()
        {
            if (posedBody == null || head == null || leftFoot == null || rightFoot == null) return;
            var feet = (leftFoot.position + rightFoot.position) * .5f;
            var axis = head.position - feet;
            if (axis.sqrMagnitude < .01f) return;
            posedBody.transform.SetPositionAndRotation((head.position + feet) * .5f, Quaternion.LookRotation(axis, Mathf.Abs(axis.normalized.y) > .9f ? Vector3.forward : Vector3.up));
            posedBody.height = axis.magnitude + .24f;
        }
        void OnDestroy()
        {
            if (original != null) foreach (var collider in original) if (collider != null) collider.enabled = true;
            if (posedBody != null) Destroy(posedBody.gameObject);
        }
    }
}
