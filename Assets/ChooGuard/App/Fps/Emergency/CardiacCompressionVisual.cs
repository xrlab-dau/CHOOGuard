using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A visual illustration of the player's compressions, using the colleague's existing skinned arms.
    /// Timing/travel are game feedback, not a measurement of clinical compression depth or effectiveness.
    /// The session owns eligibility, distance, pause and elapsed time; this component never advances a lesson.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class CardiacCompressionVisual : MonoBehaviour
    {
        private const float CyclesPerMinute = 110f;
        private const float VisualTravel = .025f;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private GameObject rigRoot;
        private Arm left, right;
        private Transform[] rigBones;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Transform chest;
        private Vector3 chestBase, chestApplied;
        private bool chestOffsetApplied, presenting, anchorBound, posed;
        private Vector3 feet, requestedChest, patientAxis, chestSurfaceLocal;
        private float presentationTime;
        private Camera view;

        /// <summary>Both original hands and arms were retained. This does not certify screen visibility.</summary>
        public bool Ready { get; private set; }
        public string FailureReason { get; private set; }
        public bool IsPresenting => Ready && presenting && posed && rigRoot != null && rigRoot.activeInHierarchy;
        public bool HandsInView(Camera camera)
        {
            if (!IsPresenting || camera == null) return false;
            return HandInView(camera, left.Hand) && HandInView(camera, right.Hand);
        }
        static bool HandInView(Camera camera, Transform hand)
        {
            var point = camera.WorldToViewportPoint(hand.position);
            return point.z > camera.nearClipPlane && point.x >= .02f && point.x <= .98f && point.y >= .02f && point.y <= .98f;
        }

        public void Initialize(PersonBody patient, GameObject colleaguePrefab, Transform parent, Camera playerView)
        {
            Release();
            view = playerView;
            FailureReason = null;
            var patientAnimator = patient != null ? patient.Animator : null;
            if (!Human(patientAnimator)) { Fail("환자의 기존 Humanoid 골격을 찾지 못했습니다."); return; }
            chest = patientAnimator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = patientAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) { Fail("환자의 기존 가슴 본을 찾지 못했습니다."); return; }
            if (colleaguePrefab == null) { Fail("기존 동료 프리팹이 없습니다."); return; }

            try
            {
                // An inactive parent prevents the clone's crowd scripts/agent from joining the live simulation.
                rigRoot = new GameObject("가슴압박 · 기존 동료 팔");
                rigRoot.SetActive(false);
                rigRoot.transform.SetParent(parent, false);
                var avatar = Instantiate(colleaguePrefab, rigRoot.transform, false);
                avatar.name = "기존 동료 골격 (시각 전용)";
                foreach (var behaviour in avatar.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var agent in avatar.GetComponentsInChildren<NavMeshAgent>(true)) agent.enabled = false;
                foreach (var collider in avatar.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in avatar.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.isKinematic = true;
                    body.detectCollisions = false;
                }
                foreach (var lod in avatar.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
                foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                var animator = avatar.GetComponentInChildren<Animator>(true);
                if (!Human(animator)) { Fail("동료 프리팹에 기존 Humanoid 골격이 없습니다."); return; }
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = false;
                // Initialize only the cloned Animator. Its controller/pose never writes to the real colleague.
                rigRoot.SetActive(true);
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0);
                animator.enabled = false;
                left = Arm.Bind(animator, true);
                right = Arm.Bind(animator, false);
                if (left == null || right == null) { Fail("기존 양팔·손·손가락 본을 연결하지 못했습니다."); return; }

                int leftHandTriangles = 0, rightHandTriangles = 0;
                foreach (var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    RetainArms(renderer, ref leftHandTriangles, ref rightHandTriangles);
                if (leftHandTriangles == 0 || rightHandTriangles == 0)
                {
                    Fail("기존 양손의 읽기 가능한 스킨 메시가 없습니다. 동료 FBX의 Read/Write 설정을 확인하세요.");
                    return;
                }

                rigBones = avatar.GetComponentsInChildren<Transform>(true);
                restPositions = new Vector3[rigBones.Length];
                restRotations = new Quaternion[rigBones.Length];
                for (int i = 0; i < rigBones.Length; i++)
                {
                    restPositions[i] = rigBones[i].localPosition;
                    restRotations[i] = rigBones[i].localRotation;
                }
                Ready = true;
                rigRoot.SetActive(false);
            }
            catch (Exception exception)
            {
                Fail("기존 팔 시각화 준비 실패: " + exception.Message);
            }
        }

        /// <summary>
        /// Call each session Update. patientChest is the visible chest-surface point when a continuous run begins;
        /// it remains attached to the existing chest bone until Hide/inactive, avoiding additive-anchor drift.
        /// bodyForward is the patient's hips-to-head direction. elapsed is session-controlled active time in seconds.
        /// </summary>
        public void Present(Vector3 rescuerFeet, Vector3 patientChest, Vector3 bodyForward, bool active, float elapsed)
        {
            if (!active || !Ready || chest == null || !Finite(rescuerFeet) || !Finite(patientChest) ||
                !Finite(bodyForward) || float.IsNaN(elapsed) || float.IsInfinity(elapsed))
            {
                Hide();
                return;
            }
            if (!presenting)
            {
                RestoreChest();
                chestSurfaceLocal = chest.InverseTransformPoint(patientChest);
                anchorBound = true;
            }
            feet = rescuerFeet;
            requestedChest = patientChest;
            patientAxis = Vector3.ProjectOnPlane(bodyForward, Vector3.up);
            if (patientAxis.sqrMagnitude < .0001f) patientAxis = Vector3.forward;
            else patientAxis.Normalize();
            presentationTime = Mathf.Max(0, elapsed);
            presenting = true;
            rigRoot.SetActive(true);
        }

        public void Hide()
        {
            presenting = false;
            posed = false;
            anchorBound = false;
            RestoreChest();
            if (rigRoot != null) rigRoot.SetActive(false);
        }

        private void Update() => RestoreChest();

        private void LateUpdate()
        {
            if (!Ready || !presenting || chest == null || rigRoot == null) { Hide(); return; }
            RestoreChest();
            for (int i = 0; i < rigBones.Length; i++)
            {
                rigBones[i].localPosition = restPositions[i];
                rigBones[i].localRotation = restRotations[i];
            }
            var surface = anchorBound ? chest.TransformPoint(chestSurfaceLocal) : requestedChest;
            var toward = Vector3.ProjectOnPlane(surface - feet, Vector3.up);
            if (toward.sqrMagnitude < .0001f) toward = Vector3.Cross(patientAxis, Vector3.up);
            toward.Normalize();
            rigRoot.transform.SetPositionAndRotation(feet, Quaternion.LookRotation(toward, Vector3.up));
            var across = right.Upper.position - left.Upper.position;
            var rigFacing = Vector3.Cross(across, Vector3.up);
            if (rigFacing.sqrMagnitude > .0001f)
                rigRoot.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(rigFacing, toward, Vector3.up), Vector3.up) * rigRoot.transform.rotation;

            float phase = Mathf.Repeat(presentationTime * CyclesPerMinute / 60f, 1f);
            float depression = VisualTravel * (.5f - .5f * Mathf.Cos(phase * Mathf.PI * 2));
            var contact = surface - Vector3.up * depression;
            // The proximal ends belong to the player's view, behind/below the camera.
            if (view == null) { Hide(); return; }
            var desiredShoulders = view.transform.position - Vector3.up * .10f - toward * .10f;
            rigRoot.transform.position += desiredShoulders - (left.Upper.position + right.Upper.position) * .5f;
            var handFrame = Quaternion.LookRotation(patientAxis, Vector3.down);
            var side = Vector3.Cross(Vector3.up, toward);
            bool leftContact = left.Solve(contact + Vector3.up * .020f, handFrame, -side - toward * .15f);
            bool rightContact = right.Solve(contact + Vector3.up * .050f, handFrame, side - toward * .15f);
            posed = leftContact && rightContact;
            if (!posed)
            {
                FailureReason = "환자 가슴에 닿도록 조금 더 가까이 자세를 잡으세요.";
                rigRoot.SetActive(false);
                return;
            }
            FailureReason = null;

            // Apply only this frame's small visual movement to the original patient rig. Undo it before animation.
            chestBase = chest.localPosition;
            var localTravel = chest.parent != null ? chest.parent.InverseTransformVector(-Vector3.up * depression) : -Vector3.up * depression;
            chestApplied = chestBase + localTravel;
            chest.localPosition = chestApplied;
            chestOffsetApplied = true;
        }

        private void RestoreChest()
        {
            if (!chestOffsetApplied) return;
            // If an Animator/owner has already supplied a fresh pose, keep that pose instead of restoring stale data.
            if (chest != null && (chest.localPosition - chestApplied).sqrMagnitude < .00000001f)
                chest.localPosition = chestBase;
            chestOffsetApplied = false;
        }

        public string ReachShape => "arms=" + left?.Length + "/" + right?.Length + "; shoulders=" + (left != null && right != null ? Vector3.Distance(left.Upper.position, right.Upper.position) : 0);
        public bool CanReachFrom(Vector3 eye, Vector3 surface, Vector3 bodyAxis, Vector3 toward)
        {
            if (!Ready || left == null || right == null) return false;
            var centre = eye - Vector3.up * .10f - toward * .10f;
            var side = Vector3.Cross(Vector3.up, toward).normalized;
            float halfWidth = Vector3.Distance(left.Upper.position, right.Upper.position) * .5f;
            var frame = Quaternion.LookRotation(Vector3.ProjectOnPlane(bodyAxis, Vector3.up).normalized, Vector3.down);
            return Vector3.Distance(centre - side * halfWidth, left.WristTarget(surface + Vector3.up * .020f, frame)) < left.Length * .97f &&
                Vector3.Distance(centre + side * halfWidth, right.WristTarget(surface + Vector3.up * .050f, frame)) < right.Length * .97f;
        }

        private void RetainArms(SkinnedMeshRenderer renderer, ref int leftHands, ref int rightHands)
        {
            var source = renderer.sharedMesh;
            if (source == null || !source.isReadable) return;
            var weights = source.boneWeights;
            if (weights.Length != source.vertexCount) return;
            var bones = renderer.bones;
            var boneSide = new int[bones.Length];
            var handSide = new int[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                boneSide[i] = Descendant(bones[i], left.Upper) ? 1 : Descendant(bones[i], right.Upper) ? 2 : 0;
                handSide[i] = Descendant(bones[i], left.Hand) ? 1 : Descendant(bones[i], right.Hand) ? 2 : 0;
            }
            var vertexSide = new int[weights.Length];
            var vertexHand = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                float l = Weight(weights[i], boneSide, 1), r = Weight(weights[i], boneSide, 2);
                vertexSide[i] = l >= .55f && l > r ? 1 : r >= .55f && r > l ? 2 : 0;
                vertexHand[i] = vertexSide[i] != 0 && Weight(weights[i], handSide, vertexSide[i]) >= .35f;
            }
            var submeshes = new List<int>[source.subMeshCount];
            int retained = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var triangles = source.GetTriangles(s);
                var kept = submeshes[s] = new List<int>();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    int which = vertexSide[a];
                    if (which == 0 || vertexSide[b] != which || vertexSide[c] != which) continue;
                    kept.Add(a); kept.Add(b); kept.Add(c);
                    retained++;
                    if (!vertexHand[a] && !vertexHand[b] && !vertexHand[c]) continue;
                    if (which == 1) leftHands++; else rightHands++;
                }
            }
            if (retained == 0) return;
            // Clone buffers/bindposes, then change only clone indices. Original mesh/material assets remain untouched.
            var arms = Instantiate(source);
            arms.name = source.name + " (runtime arms only)";
            arms.hideFlags = HideFlags.DontSave;
            ownedMeshes.Add(arms);
            for (int s = 0; s < submeshes.Length; s++) arms.SetTriangles(submeshes[s], s, false);
            arms.UploadMeshData(true);
            renderer.sharedMesh = arms;
            renderer.updateWhenOffscreen = true;
            var bounds = renderer.localBounds;
            bounds.size = Vector3.Max(bounds.size, Vector3.one * 3f);
            renderer.localBounds = bounds;
            renderer.enabled = true;
        }

        private static bool Descendant(Transform bone, Transform ancestor) => bone != null && (bone == ancestor || bone.IsChildOf(ancestor));
        private static float Weight(BoneWeight weight, int[] labels, int wanted) =>
            Part(weight.boneIndex0, weight.weight0, labels, wanted) + Part(weight.boneIndex1, weight.weight1, labels, wanted) +
            Part(weight.boneIndex2, weight.weight2, labels, wanted) + Part(weight.boneIndex3, weight.weight3, labels, wanted);
        private static float Part(int index, float weight, int[] labels, int wanted) => index >= 0 && index < labels.Length && labels[index] == wanted ? weight : 0;
        private static bool Human(Animator animator) => animator != null && animator.avatar != null && animator.avatar.isValid && animator.isHuman;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void Fail(string reason)
        {
            Release();
            FailureReason = reason;
            Debug.LogWarning("[CardiacCompressionVisual] " + reason, this);
        }

        private void Release()
        {
            Hide();
            Ready = false;
            chest = null;
            if (rigRoot != null) Destroy(rigRoot);
            rigRoot = null;
            foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            ownedMeshes.Clear();
            rigBones = null;
            restPositions = null;
            restRotations = null;
            left = right = null;
        }

        private void OnDisable() => Hide();
        private void OnDestroy() => Release();

        internal sealed class Arm
        {
            public Transform Upper, Lower, Hand;
            public float Length;
            private Quaternion palmFrameLocal;
            private Vector3 palmCentreLocal;

            public static Arm Bind(Animator animator, bool left)
            {
                var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
                var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
                var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
                if (upper == null || lower == null || hand == null || middle == null || index == null || little == null) return null;
                float first = Vector3.Distance(upper.position, lower.position), second = Vector3.Distance(lower.position, hand.position);
                if (first < .02f || second < .02f) return null;
                var fingers = middle.position - hand.position;
                var across = index.position - little.position;
                var palm = Vector3.Cross(fingers, across) * (left ? -1f : 1f);
                if (fingers.sqrMagnitude < .000001f || palm.sqrMagnitude < .000000001f) return null;
                return new Arm
                {
                    Upper = upper, Lower = lower, Hand = hand, Length = first + second,
                    palmFrameLocal = Quaternion.LookRotation(hand.InverseTransformDirection(fingers), hand.InverseTransformDirection(palm)),
                    palmCentreLocal = hand.InverseTransformPoint(middle.position) * .55f
                };
            }

            public Vector3 WristTarget(Vector3 palmCentre, Quaternion desiredPalmFrame)
            {
                var handRotation = desiredPalmFrame * Quaternion.Inverse(palmFrameLocal);
                // Imported rigs may retain centimetre-to-metre scale above their bones.
                return palmCentre - handRotation * Vector3.Scale(palmCentreLocal, Hand.lossyScale);
            }

            public bool Solve(Vector3 palmCentre, Quaternion desiredPalmFrame, Vector3 elbowPole)
            {
                var handRotation = desiredPalmFrame * Quaternion.Inverse(palmFrameLocal);
                var target = WristTarget(palmCentre, desiredPalmFrame);
                var start = Upper.position;
                float first = Vector3.Distance(start, Lower.position), second = Vector3.Distance(Lower.position, Hand.position);
                var delta = target - start;
                float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(first - second) + .0001f, first + second - .0001f);
                var direction = delta.sqrMagnitude > .000001f ? delta.normalized : Vector3.down;
                var bend = Vector3.ProjectOnPlane(elbowPole, direction);
                if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(Vector3.forward, direction);
                bend.Normalize();
                float along = (first * first - second * second + distance * distance) / (2f * distance);
                float height = Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
                var elbow = start + direction * along + bend * height;
                Upper.rotation = Quaternion.FromToRotation(Lower.position - start, elbow - start) * Upper.rotation;
                var reachedTarget = start + direction * distance;
                Lower.rotation = Quaternion.FromToRotation(Hand.position - Lower.position, reachedTarget - Lower.position) * Lower.rotation;
                Hand.rotation = handRotation;
                return (Hand.TransformPoint(palmCentreLocal) - palmCentre).sqrMagnitude <= .0001f;
            }
        }
    }
}
