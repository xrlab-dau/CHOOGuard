using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Keeps a person's own capsule (the aim and contact body the crowd prefab gives every person) on the pose the animator shows.
    /// A standing person keeps the prefab's upright capsule. Someone lying, slumped in a seat, sitting on escalator steps, kneeling
    /// or crouching gets a capsule around head, hips and feet instead, so looking at the empty air or the pillar above them no longer
    /// offers "부상 승객 [E]" or "구급대원 [E]". When another component has switched the capsule off (CardiacPatientBodyTarget gives the
    /// lesson patient its own posed body), this leaves it alone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PosturedBodyCollider : MonoBehaviour
    {
        private const float Radius = .2f, Interval = .2f;
        private CapsuleCollider capsule;
        private Vector3 uprightCenter;
        private float uprightHeight, uprightRadius;
        private int uprightDirection;
        private Transform head, hips, leftFoot, rightFoot;
        private float next;
        private bool posed;

        /// <summary>Whether the capsule is fitted to a non-standing pose now (for tests and the shift log).</summary>
        public bool Posed => posed;

        private void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                uprightCenter = capsule.center; uprightHeight = capsule.height; uprightRadius = capsule.radius; uprightDirection = capsule.direction;
            }
            var animator = GetComponent<PersonBody>()?.Animator ?? GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            // 여러 사람이 같은 프레임에 몰리지 않게 시작을 흩는다.
            next = Time.time + Random.value * Interval;
        }

        private void LateUpdate()
        {
            if (capsule == null || !capsule.enabled || head == null || hips == null || leftFoot == null || rightFoot == null) return;
            if (Time.time < next) return;
            next = Time.time + Interval;
            Fit();
        }

        /// <summary>Fits the capsule to the current pose at once (the interval otherwise spreads the work).</summary>
        public void Fit()
        {
            if (capsule == null || head == null || hips == null || leftFoot == null || rightFoot == null) return;
            var h = transform.InverseTransformPoint(head.position);
            var p = transform.InverseTransformPoint(hips.position);
            var l = transform.InverseTransformPoint(leftFoot.position);
            var r = transform.InverseTransformPoint(rightFoot.position);
            var feet = (l + r) * .5f;
            float lean = new Vector2(h.x - feet.x, h.z - feet.z).magnitude;
            // 서 있는 사람(머리가 높고 발 위에 있음)은 프리팹의 캡슐 그대로다. 걷는 흔들림으로 바뀌지 않게 여유를 둔다.
            if (h.y > 1.3f && lean < .5f)
            {
                if (posed) Restore();
                return;
            }
            var min = Vector3.Min(Vector3.Min(h, p), Vector3.Min(l, r)) - Vector3.one * Radius;
            var max = Vector3.Max(Vector3.Max(h, p), Vector3.Max(l, r)) + Vector3.one * Radius;
            min.y = Mathf.Max(min.y, 0);
            max.y = Mathf.Max(max.y, min.y + 2 * Radius);
            var size = max - min;
            int direction = size.y >= size.x && size.y >= size.z ? 1 : size.x >= size.z ? 0 : 2;
            float length = direction == 0 ? size.x : direction == 1 ? size.y : size.z;
            float across = direction == 0 ? Mathf.Max(size.y, size.z) : direction == 1 ? Mathf.Max(size.x, size.z) : Mathf.Max(size.x, size.y);
            float radius = Mathf.Clamp(across * .5f, .15f, .45f);
            var center = (min + max) * .5f;
            if (posed && capsule.direction == direction && (capsule.center - center).sqrMagnitude < .0009f
                && Mathf.Abs(capsule.height - length) < .03f && Mathf.Abs(capsule.radius - radius) < .03f) return;
            capsule.direction = direction;
            capsule.center = center;
            capsule.radius = radius;
            capsule.height = Mathf.Max(length, 2 * radius);
            posed = true;
        }

        private void Restore()
        {
            capsule.center = uprightCenter; capsule.height = uprightHeight; capsule.radius = uprightRadius; capsule.direction = uprightDirection;
            posed = false;
        }
    }
}
