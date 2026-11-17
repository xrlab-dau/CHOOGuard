using System.Globalization;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Adds hand contact to the actual colleague's existing kneeling/treatment animation.
    /// Owns neither movement nor SetTreat, and creates no avatar, renderer, mesh or material.
    /// The session must make this and the player's compression visual mutually exclusive.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(10001)]
    public sealed class CardiacColleagueCompressionVisual : MonoBehaviour
    {
        private const float CyclesPerMinute = 110f;
        private const float VisualTravel = .025f;
        private const float MaxTorsoLeanDegrees = 60f;
        private PersonBody rescuer;
        private Transform chest, torso;
        private CardiacCompressionVisual.Arm left, right;
        private Transform[] armBones;
        private Quaternion[] baseRotations, appliedRotations;
        private bool armOffsetApplied, chestOffsetApplied, torsoOffsetApplied, presenting, posed;
        private Quaternion torsoBaseRotation, torsoAppliedRotation;
        private Vector3 chestBase, chestApplied, chestSurfaceLocal, patientAxis;
        private float elapsed;

        /// <summary>The existing rigs are bound; their current pose may still be out of reach.</summary>
        public bool Ready { get; private set; }
        /// <summary>Initialization failure, or the reason this requested frame could not show hand contact.</summary>
        public string FailureReason { get; private set; }
        public bool IsPresenting => Ready && presenting && posed;

        public void Initialize(PersonBody colleague, PersonBody patient)
        {
            Hide();
            Ready = false;
            FailureReason = null;
            rescuer = colleague;
            left = right = null;
            chest = null;
            torso = null;
            armBones = null;
            baseRotations = appliedRotations = null;
            if (colleague == null || patient == null || colleague == patient ||
                !Human(colleague.Animator) || !Human(patient.Animator))
            {
                FailureReason = "실제 동료와 환자의 서로 다른 기존 Humanoid 골격이 필요합니다.";
                return;
            }
            chest = patient.Animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = patient.Animator.GetBoneTransform(HumanBodyBones.UpperChest);
            left = CardiacCompressionVisual.Arm.Bind(colleague.Animator, true);
            right = CardiacCompressionVisual.Arm.Bind(colleague.Animator, false);
            if (chest == null || left == null || right == null)
            {
                FailureReason = "기존 동료의 양팔·손·손가락 또는 환자의 가슴 본이 없습니다.";
                return;
            }
            armBones = new[] { left.Upper, left.Lower, left.Hand, right.Upper, right.Lower, right.Hand };
            baseRotations = new Quaternion[armBones.Length];
            appliedRotations = new Quaternion[armBones.Length];
            torso = FindTorso(colleague.Animator);
            Ready = true;
        }

        /// <summary>
        /// Call every session Update. Supply the visible chest-surface centre and hips-to-head direction.
        /// SetTreat/KneelNow/FacePose/movement remain with the real colleague's existing owner.
        /// active=false restores this component's offsets immediately; elapsed is session-controlled active time.
        /// </summary>
        public void Present(Vector3 patientChest, Vector3 bodyForward, bool active, float activeElapsed)
        {
            if (!active || !Ready || !isActiveAndEnabled || !Finite(patientChest) || !Finite(bodyForward) ||
                float.IsNaN(activeElapsed) || float.IsInfinity(activeElapsed))
            {
                Hide();
                return;
            }
            if (chest == null || rescuer == null)
            {
                Hide();
                Ready = false;
                FailureReason = "동료 또는 환자의 기존 골격이 사라졌습니다.";
                return;
            }
            if (!presenting)
            {
                RestoreChest();
                chestSurfaceLocal = chest.InverseTransformPoint(patientChest);
            }
            patientAxis = Vector3.ProjectOnPlane(bodyForward, Vector3.up);
            if (patientAxis.sqrMagnitude < .0001f) patientAxis = Vector3.forward;
            else patientAxis.Normalize();
            elapsed = Mathf.Max(0, activeElapsed);
            presenting = true;
        }

        public void Hide()
        {
            presenting = false;
            posed = false;
            RestoreArms();
            RestoreTorso();
            RestoreChest();
            if (Ready) FailureReason = null;
        }

        private void Update()
        {
            // Undo only our prior additive pose before the Animator supplies this frame's treatment pose.
            RestoreArms();
            RestoreTorso();
            RestoreChest();
        }

        private void LateUpdate()
        {
            if (!Ready || !presenting) return;
            RestoreArms();
            RestoreTorso();
            RestoreChest();
            posed = false;
            if (rescuer == null || chest == null || !rescuer.isActiveAndEnabled ||
                left.Upper == null || left.Lower == null || left.Hand == null ||
                right.Upper == null || right.Lower == null || right.Hand == null)
            {
                Ready = false;
                FailureReason = "실제 동료 또는 환자를 더 이상 사용할 수 없습니다.";
                return;
            }
            if (!rescuer.Visible || rescuer.Animator == null || !rescuer.Animator.isActiveAndEnabled)
            {
                FailureReason = "실제 동료 모델 또는 Animator가 표시·동작 중이 아닙니다.";
                return;
            }
            if (!rescuer.Kneeling)
            {
                FailureReason = "기존 동료의 무릎 처치 자세를 기다립니다.";
                return;
            }
            var surface = chest.TransformPoint(chestSurfaceLocal);
            var toward = Vector3.ProjectOnPlane(surface - rescuer.transform.position, Vector3.up);
            if (toward.sqrMagnitude < .0001f) toward = Vector3.Cross(patientAxis, Vector3.up);
            toward.Normalize();
            var side = Vector3.Cross(Vector3.up, toward);
            float phase = Mathf.Repeat(elapsed * CyclesPerMinute / 60f, 1f);
            float depression = VisualTravel * (.5f - .5f * Mathf.Cos(phase * Mathf.PI * 2));
            var contact = surface - Vector3.up * depression;
            var handFrame = Quaternion.LookRotation(patientAxis, Vector3.down);
            var leftPalm = contact + Vector3.up * .020f;
            var rightPalm = contact + Vector3.up * .050f;
            if (!ApplyMinimumTorsoLean(leftPalm, rightPalm, handFrame, toward, out float leanDegrees))
            {
                // Report actual transformed distances at the best bounded attempt, then undo it this frame.
                FailureReason = ContactFailure("기존 팔 길이 안에 드는 몸통 자세를 찾지 못했습니다", leftPalm, rightPalm, handFrame, leanDegrees);
                RestoreTorso();
                return;
            }
            for (int i = 0; i < armBones.Length; i++) baseRotations[i] = armBones[i].localRotation;
            bool leftContact = left.Solve(leftPalm, handFrame, -side - toward * .15f);
            bool rightContact = right.Solve(rightPalm, handFrame, side - toward * .15f);
            if (!leftContact || !rightContact)
            {
                // A stand/kneel transition can recover next frame. Do not stretch bones or move the colleague.
                for (int i = 0; i < armBones.Length; i++) armBones[i].localRotation = baseRotations[i];
                FailureReason = ContactFailure("양손 IK 접촉 오차가 허용 범위를 넘었습니다", leftPalm, rightPalm, handFrame, leanDegrees);
                RestoreTorso();
                return;
            }
            for (int i = 0; i < armBones.Length; i++) appliedRotations[i] = armBones[i].localRotation;
            armOffsetApplied = true;
            chestBase = chest.localPosition;
            var travel = -Vector3.up * depression;
            chestApplied = chestBase + (chest.parent != null ? chest.parent.InverseTransformVector(travel) : travel);
            chest.localPosition = chestApplied;
            chestOffsetApplied = true;
            posed = true;
            FailureReason = null;
        }

        private Transform FindTorso(Animator animator)
        {
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            foreach (var bone in new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest })
            {
                var candidate = animator.GetBoneTransform(bone);
                // Rotate an upper-body child of the pelvis, never the pelvis, root or leg chain.
                if (hips != null && candidate != null && candidate != hips && candidate.IsChildOf(hips) &&
                    left.Upper.IsChildOf(candidate) && right.Upper.IsChildOf(candidate)) return candidate;
            }
            return null;
        }

        private bool ApplyMinimumTorsoLean(Vector3 leftPalm, Vector3 rightPalm, Quaternion handFrame, Vector3 toward, out float degrees)
        {
            var leftTarget = left.WristTarget(leftPalm, handFrame);
            var rightTarget = right.WristTarget(rightPalm, handFrame);
            var pivot = torso != null ? torso.position : Vector3.zero;
            var leanToward = Vector3.ProjectOnPlane((leftTarget + rightTarget) * .5f - pivot, Vector3.up);
            if (leanToward.sqrMagnitude < .0001f) leanToward = toward;
            var axis = Vector3.Cross(Vector3.up, leanToward.normalized);
            var reach = new ReachGeometry(left, right, pivot, axis, leftTarget, rightTarget);
            degrees = 0;
            if (reach.Violation(0) <= 0) return true;
            if (torso == null) return false;

            float bestDegrees = 0, bestViolation = reach.Violation(0);
            bool found = false;
            for (float candidate = 2f; candidate <= MaxTorsoLeanDegrees; candidate += 2f)
            {
                float violation = reach.Violation(candidate);
                if (violation < bestViolation) { bestViolation = violation; bestDegrees = candidate; }
                if (violation > 0) continue;
                // The first reachable interval gives the smallest extra lean at this frame's source pose.
                float low = candidate - 2f, high = candidate;
                for (int i = 0; i < 7; i++)
                {
                    float middle = (low + high) * .5f;
                    if (reach.Violation(middle) <= 0) high = middle; else low = middle;
                }
                degrees = high;
                found = true;
                break;
            }
            if (!found) degrees = bestDegrees;
            if (degrees > .001f)
            {
                torsoBaseRotation = torso.localRotation;
                torso.rotation = Quaternion.AngleAxis(degrees, axis) * torso.rotation;
                torsoAppliedRotation = torso.localRotation;
                torsoOffsetApplied = true;
            }
            return found;
        }

        private readonly struct ReachGeometry
        {
            private readonly Vector3 pivot, axis, leftShoulder, rightShoulder, leftTarget, rightTarget;
            private readonly float leftMinimum, leftMaximum, rightMinimum, rightMaximum;

            public ReachGeometry(CardiacCompressionVisual.Arm left, CardiacCompressionVisual.Arm right,
                Vector3 pivot, Vector3 axis, Vector3 leftTarget, Vector3 rightTarget)
            {
                this.pivot = pivot;
                this.axis = axis;
                this.leftTarget = leftTarget;
                this.rightTarget = rightTarget;
                leftShoulder = left.Upper.position;
                rightShoulder = right.Upper.position;
                float leftUpper = Vector3.Distance(left.Upper.position, left.Lower.position);
                float leftLower = Vector3.Distance(left.Lower.position, left.Hand.position);
                float rightUpper = Vector3.Distance(right.Upper.position, right.Lower.position);
                float rightLower = Vector3.Distance(right.Lower.position, right.Hand.position);
                leftMinimum = Mathf.Abs(leftUpper - leftLower) + .001f;
                rightMinimum = Mathf.Abs(rightUpper - rightLower) + .001f;
                // Keep a small numerical margin without changing any bone length.
                leftMaximum = leftUpper + leftLower - .002f;
                rightMaximum = rightUpper + rightLower - .002f;
            }

            public float Violation(float degrees)
            {
                var rotation = Quaternion.AngleAxis(degrees, axis);
                float l = Vector3.Distance(pivot + rotation * (leftShoulder - pivot), leftTarget);
                float r = Vector3.Distance(pivot + rotation * (rightShoulder - pivot), rightTarget);
                return Mathf.Max(Mathf.Max(l - leftMaximum, leftMinimum - l), Mathf.Max(r - rightMaximum, rightMinimum - r));
            }
        }

        private string ContactFailure(string reason, Vector3 leftPalm, Vector3 rightPalm, Quaternion handFrame, float leanDegrees) =>
            string.Format(CultureInfo.InvariantCulture, "{0} · torso={1}, lean={2:F2}/{3:F0}deg · {4} · {5} (world units)",
                reason, torso != null ? torso.name : "none", leanDegrees, MaxTorsoLeanDegrees,
                ArmDistances("L", left, left.WristTarget(leftPalm, handFrame)),
                ArmDistances("R", right, right.WristTarget(rightPalm, handFrame)));

        private static string ArmDistances(string side, CardiacCompressionVisual.Arm arm, Vector3 wristTarget)
        {
            float upper = Vector3.Distance(arm.Upper.position, arm.Lower.position);
            float lower = Vector3.Distance(arm.Lower.position, arm.Hand.position);
            float distance = Vector3.Distance(arm.Upper.position, wristTarget);
            return string.Format(CultureInfo.InvariantCulture, "{0} shoulder->wrist={1:F4}, upper={2:F4}, lower={3:F4}, total={4:F4}, excess={5:F4}",
                side, distance, upper, lower, upper + lower, Mathf.Max(0, distance - upper - lower));
        }

        private void RestoreArms()
        {
            if (!armOffsetApplied) return;
            for (int i = 0; i < armBones.Length; i++)
            {
                var bone = armBones[i];
                // Preserve a fresh Animator/owner pose if it has already replaced the offset.
                if (bone != null && Quaternion.Angle(bone.localRotation, appliedRotations[i]) < .01f)
                    bone.localRotation = baseRotations[i];
            }
            armOffsetApplied = false;
        }

        private void RestoreTorso()
        {
            if (!torsoOffsetApplied) return;
            if (torso != null && Quaternion.Angle(torso.localRotation, torsoAppliedRotation) < .01f)
                torso.localRotation = torsoBaseRotation;
            torsoOffsetApplied = false;
        }

        private void RestoreChest()
        {
            if (!chestOffsetApplied) return;
            if (chest != null && (chest.localPosition - chestApplied).sqrMagnitude < .00000001f)
                chest.localPosition = chestBase;
            chestOffsetApplied = false;
        }

        private static bool Human(Animator animator) => animator != null && animator.avatar != null && animator.avatar.isValid && animator.isHuman;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private void OnDisable() => Hide();
        private void OnDestroy() => Hide();
    }
}
