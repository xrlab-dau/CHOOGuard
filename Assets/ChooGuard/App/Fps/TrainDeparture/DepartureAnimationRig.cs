using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;
using UnityEngine.Experimental.Animations;

namespace ChooGuard.App.Fps.TrainDeparture
{
    // Foundation: Animation Rigging 1.4.1, revision e8760459688ecee8bb14a76e429086cf496110ce.
    // RigBuilder/RigLayer execute the unchanged TwoBoneIKConstraintJob + binder; no local IK solver.
    // Unity Companion License, copyright 2026 Unity Technologies ApS. Retain package LICENSE.md.
    // Setup guidance read: Unity SIGGRAPH2019 TwoBoneIKAutoSetupUtility, 76ba741e333e414212ea6a9fc3d050f9e0f4117d.
    [DefaultExecutionOrder(1000)]
    public sealed class DepartureAnimationRig : MonoBehaviour
    {
        [Serializable]
        public sealed class Arm
        {
            // Preserve authored serialized names. These are source observations, never driven joints.
            public Transform UpperArm, Forearm, Hand, ShoulderAnchor, ElbowHint;
            public Renderer VisibleMesh;
            public Transform RestTarget;
            public Transform[] FingerJoints = Array.Empty<Transform>();
            public Vector3 FingerCurlAxis = Vector3.forward;
            public float FingerCurlDegrees = 45;

            internal bool Valid(Transform actor)
            {
                if (actor == null || UpperArm == null || Forearm == null || Hand == null ||
                    ShoulderAnchor == null || ElbowHint == null || VisibleMesh == null ||
                    !UpperArm.IsChildOf(actor) || !Forearm.IsChildOf(UpperArm) || !Hand.IsChildOf(Forearm) ||
                    !Hand.gameObject.activeInHierarchy || !VisibleMesh.enabled || VisibleMesh.forceRenderingOff ||
                    !VisibleMesh.gameObject.activeInHierarchy) return false;
                var skin = VisibleMesh as SkinnedMeshRenderer;
                if (skin == null || skin.sharedMesh == null || skin.sharedMesh.vertexCount == 0) return false;
                bool upper = false, mid = false, tip = false;
                foreach (var bone in skin.bones)
                { upper |= bone == UpperArm; mid |= bone == Forearm; tip |= bone == Hand; }
                return upper && mid && tip && Vector3.Distance(UpperArm.position, Forearm.position) > .03f &&
                    Vector3.Distance(Forearm.position, Hand.position) > .03f;
            }
        }

        [Serializable]
        public sealed class PartContact
        {
            public DepartureManipulable Part;
            public Transform Grip;
            [Range(0, 1)] public float FingerCurl = .8f;
        }

        public RigBuilder Builder;
        public Rig FoundationRig;
        public TwoBoneIKConstraint LeftIK, RightIK;
        public Animator SourceAnimator;
        public Transform ViewFrame;
        public Arm Left = new Arm(), Right = new Arm();
        // Compatibility bindings: world props own their animation; FPS has no arm posing.
        public DepartureManipulation Manipulation;
        public PartContact[] PartContacts = Array.Empty<PartContact>();
        public Transform BagLeftGrip, BagRightGrip, KeyGrip;
        public Transform SignalRaiseTarget, SignalSweepTarget;
        [Min(.1f)] public float RaiseHoldSeconds = .18f, SweepSeconds = .65f, FinishHoldSeconds = .2f;
        [Min(1)] public float FollowSpeed = 18;
        [Range(.005f, .06f)] public float SignalPositionTolerance = .025f;
        [Range(1, 30)] public float SignalAngleTolerance = 12;
        public bool IsSignalling => phase != Phase.None;
        public bool Paused { get; private set; }
        public bool SignalPoseReached { get; private set; }
        public float SignalProgress => phase == Phase.Complete ? 1 : phase == Phase.Finishing ? .75f :
            phase == Phase.Sweeping ? .25f + .5f * sweep : 0;

        enum Phase { None, Raising, Sweeping, Finishing, Complete }
        Phase phase;
        bool bagGripping, keyHeld, raisedObserved;
        float phaseTime, sweep, oldAnimatorSpeed, observedDeltaTime;
        int requestFrame = -1, publishedFrame = -1, observedFrame = -1, solveTick, consumedTick;
        Vector3 initialHand, raisedHand, publishedPosition;
        Quaternion publishedRotation;
        Animator pausedAnimator;
        Rig activeRig;
        TwoBoneIKConstraint activeRight;
        Transform activeTarget, activeHint;
        PlayableGraph activeGraph;
        AnimationPlayableOutput activeOutput;
        Playable[] activeChain;
        double[] publishedTimes, observedTimes;

        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w);
        bool Independent(Transform t) => t != null && t.gameObject.activeInHierarchy &&
            (Left == null || Left.UpperArm == null || !t.IsChildOf(Left.UpperArm)) &&
            (Right == null || Right.UpperArm == null || !t.IsChildOf(Right.UpperArm));
        public void SetBagState(bool carried, bool gripping) { bagGripping = carried && gripping; }
        public void SetKeyHeld(bool held) { keyHeld = held; }
        bool Occupied => keyHeld || bagGripping || (Manipulation != null && Manipulation.Holding != null);

        public void SetPaused(bool value)
        {
            if (Paused == value) return;
            Paused = value;
            if (value)
            {
                CancelSignal(); pausedAnimator = SourceAnimator;
                if (pausedAnimator != null) { oldAnimatorSpeed = pausedAnimator.speed; pausedAnimator.speed = 0; }
            }
            else RestoreAnimator();
        }
        void RestoreAnimator()
        {
            if (pausedAnimator != null) pausedAnimator.speed = oldAnimatorSpeed;
            pausedAnimator = null;
        }
        public void ResetRig() { CancelSignal(); bagGripping = keyHeld = false; }
        public void CancelSignal()
        {
            if (activeRig != null) activeRig.weight = 0;
            if (FoundationRig != null) FoundationRig.weight = 0;
            if (activeRight != null) activeRight.weight = 0;
            phase = Phase.None; phaseTime = sweep = 0;
            SignalPoseReached = raisedObserved = false;
            requestFrame = publishedFrame = -1; consumedTick = solveTick;
            activeRig = null; activeRight = null; activeTarget = activeHint = null;
            activeChain = null; publishedTimes = observedTimes = null;
        }
        void OnDisable() { CancelSignal(); RestoreAnimator(); Paused = false; }
        void OnApplicationPause(bool value) { if (value) SetPaused(true); }
        void OnApplicationFocus(bool value) { if (!value) SetPaused(true); }

        bool ConstraintValid(TwoBoneIKConstraint c, Arm arm)
        {
            if (c == null || !c.isActiveAndEnabled || !c.IsValid() || !c.transform.IsChildOf(FoundationRig.transform)) return false;
            var d = c.data;
            return d.root == arm.UpperArm && d.mid == arm.Forearm && d.tip == arm.Hand &&
                Independent(d.target) && Independent(d.hint) && Independent(arm.ElbowHint) &&
                d.target != d.hint && d.target.IsChildOf(FoundationRig.transform) && d.hint.IsChildOf(FoundationRig.transform) &&
                !d.maintainTargetPositionOffset && !d.maintainTargetRotationOffset &&
                d.targetPositionWeight >= .999f && d.targetRotationWeight >= .999f && d.hintWeight >= .999f;
        }
        bool GraphValid(bool capture = false)
        {
            if (Builder == null || !Builder.isActiveAndEnabled || SourceAnimator == null ||
                Builder.GetComponent<Animator>() != SourceAnimator || !SourceAnimator.isActiveAndEnabled ||
                !SourceAnimator.isInitialized || SourceAnimator.runtimeAnimatorController == null ||
                !Finite(SourceAnimator.speed) || SourceAnimator.speed <= 0 || SourceAnimator.applyRootMotion ||
                SourceAnimator.cullingMode != AnimatorCullingMode.AlwaysAnimate || SourceAnimator.updateMode != AnimatorUpdateMode.Normal ||
                FoundationRig == null || !FoundationRig.isActiveAndEnabled ||
                !FoundationRig.transform.IsChildOf(SourceAnimator.transform) ||
                !Builder.graph.IsValid() || !Builder.graph.IsPlaying() ||
                Builder.graph.GetTimeUpdateMode() == DirectorUpdateMode.Manual || Builder.graph.GetRootPlayableCount() == 0) return false;
            // RigBuilderUtils builds a separate serial native job chain/output per layer.
            foreach (var layer in Builder.layers)
            {
                if (layer == null || layer.rig != FoundationRig || !layer.active || !layer.IsValid() ||
                    layer.constraints == null || layer.jobs == null || layer.jobs.Length != 2 || layer.constraints.Length != 2) continue;
                bool left = false, right = false;
                foreach (var constraint in layer.constraints)
                { left |= constraint.component == LeftIK; right |= constraint.component == RightIK; }
                if (!left || !right) continue;
                for (int i = 0; i < Builder.graph.GetOutputCount(); i++)
                {
                    var output = Builder.graph.GetOutput(i);
                    if (output.GetPlayableOutputType() != typeof(AnimationPlayableOutput)) continue;
                    var animation = (AnimationPlayableOutput)output;
                    if (animation.GetTarget() != SourceAnimator || !Finite(animation.GetWeight()) || animation.GetWeight() < .999f ||
                        animation.GetSourceOutputPort() != 0 || animation.GetAnimationStreamSource() != AnimationStreamSource.PreviousInputs ||
                        (IsSignalling && !animation.Equals(activeOutput))) continue;
                    if (FoundationChain(layer, animation.GetSourcePlayable(), capture))
                    { if (capture) activeOutput = animation; return true; }
                }
            }
            return false;
        }
        bool FoundationChain(RigLayer layer, Playable source, bool capture)
        {
            var chain = capture ? new Playable[layer.jobs.Length] : activeChain;
            for (int i = layer.jobs.Length - 1; i >= 0; i--)
            {
                if (!source.IsValid() || source.GetPlayableType() != typeof(AnimationScriptPlayable) ||
                    source.GetPlayState() != PlayState.Playing || source.IsDone() || double.IsNaN(source.GetSpeed()) ||
                    double.IsInfinity(source.GetSpeed()) || source.GetSpeed() <= 0 ||
                    source.GetInputCount() != (i == 0 ? 0 : 1) ||
                    (i > 0 && (!Finite(source.GetInputWeight(0)) || source.GetInputWeight(0) < .999f)) ||
                    (IsSignalling && (chain == null || chain.Length != layer.jobs.Length || !source.Equals(chain[i])))) return false;
                var script = (AnimationScriptPlayable)source;
                if (!script.GetProcessInputs() || !(layer.jobs[i] is TwoBoneIKConstraintJob expected)) return false;
                TwoBoneIKConstraintJob actual;
                try { actual = script.GetJobData<TwoBoneIKConstraintJob>(); }
                catch (ArgumentException) { return false; } // Public API rejects any substituted job type.
                // Runtime property handles can resolve in-place; transform handles below are
                // passed by value to SolveTwoBoneIK/PassThrough, and jobWeight is a value property.
                if (!actual.root.Equals(expected.root) || !actual.mid.Equals(expected.mid) || !actual.tip.Equals(expected.tip) ||
                    !actual.target.Equals(expected.target) || !actual.hint.Equals(expected.hint) ||
                    !actual.jobWeight.Equals(expected.jobWeight) || !actual.targetOffset.Equals(expected.targetOffset)) return false;
                if (capture) chain[i] = source;
                if (i > 0) source = source.GetInput(0);
            }
            if (capture)
            {
                activeChain = chain; publishedTimes = new double[chain.Length]; observedTimes = new double[chain.Length];
                for (int i = 0; i < chain.Length; i++) observedTimes[i] = double.NegativeInfinity;
            }
            return true;
        }
        bool ValidSignal()
        {
            if (!isActiveAndEnabled || ViewFrame == null || !ViewFrame.gameObject.activeInHierarchy || !GraphValid() ||
                Left == null || Right == null || !Left.Valid(SourceAnimator.transform) || !Right.Valid(SourceAnimator.transform) ||
                Left.UpperArm == Right.UpperArm || Left.UpperArm.IsChildOf(Right.UpperArm) ||
                Right.UpperArm.IsChildOf(Left.UpperArm) || LeftIK == RightIK || !ConstraintValid(LeftIK, Left) || !ConstraintValid(RightIK, Right) ||
                !Independent(SignalRaiseTarget) || !Independent(SignalSweepTarget) || SignalRaiseTarget == SignalSweepTarget ||
                !SignalRaiseTarget.IsChildOf(ViewFrame) || !SignalSweepTarget.IsChildOf(ViewFrame) ||
                !Finite(SignalRaiseTarget.rotation) || !Finite(SignalSweepTarget.rotation) ||
                !Finite(RaiseHoldSeconds) || RaiseHoldSeconds < .1f || !Finite(SweepSeconds) || SweepSeconds < .1f ||
                !Finite(FinishHoldSeconds) || FinishHoldSeconds < .1f || !Finite(SignalPositionTolerance) ||
                SignalPositionTolerance < .005f || SignalPositionTolerance > .06f || !Finite(SignalAngleTolerance) ||
                SignalAngleTolerance < 1 || SignalAngleTolerance > 30) return false;
            if (IsSignalling && (activeRig != FoundationRig || activeRight != RightIK ||
                activeTarget != RightIK.data.target || activeHint != RightIK.data.hint || !activeGraph.Equals(Builder.graph))) return false;
            var a = ViewFrame.InverseTransformPoint(SignalRaiseTarget.position);
            var b = ViewFrame.InverseTransformPoint(SignalSweepTarget.position);
            var shoulder = ViewFrame.InverseTransformPoint(Right.ShoulderAnchor.position);
            return Finite(a) && Finite(b) && Finite(shoulder) && a.y - shoulder.y >= .18f && b.y - shoulder.y >= .1f &&
                Mathf.Abs(b.x - a.x) >= .18f && Vector3.Distance(a, b) >= .2f && Reachable(SignalRaiseTarget.position) && Reachable(SignalSweepTarget.position);
        }
        bool Reachable(Vector3 target)
        {
            float upper = Vector3.Distance(Right.UpperArm.position, Right.Forearm.position);
            float lower = Vector3.Distance(Right.Forearm.position, Right.Hand.position);
            float distance = Vector3.Distance(Right.UpperArm.position, target);
            return Finite(distance) && distance > Mathf.Abs(upper - lower) + .001f && distance < upper + lower - .001f;
        }

        /// <summary>One-shot completion consumes each actual LateUpdate solve observation once.</summary>
        public bool StepSignal(bool held, bool outside, float dt)
        {
            if (Paused || Time.timeScale <= 0 || !held || !outside || Occupied || !Finite(dt) || dt <= 0 || dt > .25f || !ValidSignal())
            { CancelSignal(); return false; }
            requestFrame = Time.frameCount;
            if (phase == Phase.None)
            {
                activeRig = FoundationRig; activeRight = RightIK; activeTarget = RightIK.data.target; activeHint = RightIK.data.hint;
                activeGraph = Builder.graph;
                if (!GraphValid(true)) { CancelSignal(); return false; }
                phase = Phase.Raising; consumedTick = solveTick;
                initialHand = ViewFrame.InverseTransformPoint(Right.Hand.position);
                // Start the independent target at the actual observed source wrist.
                activeTarget.SetPositionAndRotation(RightIK.data.tip.position, RightIK.data.tip.rotation);
                return false;
            }
            if (phase == Phase.Complete || consumedTick == solveTick) return false;
            consumedTick = solveTick;
            if (!SignalPoseReached) { phaseTime = 0; return false; }
            float elapsed = Mathf.Min(dt, observedDeltaTime);
            phaseTime += elapsed;
            if (phase == Phase.Raising && phaseTime >= RaiseHoldSeconds)
            {
                raisedHand = ViewFrame.InverseTransformPoint(Right.Hand.position);
                if (raisedHand.y - initialHand.y < .1f || Vector3.Distance(raisedHand, initialHand) < .12f) return false;
                raisedObserved = true; phase = Phase.Sweeping; phaseTime = sweep = 0;
            }
            else if (phase == Phase.Sweeping)
            {
                sweep = Mathf.Clamp01(sweep + elapsed / SweepSeconds);
                if (sweep >= 1) { phase = Phase.Finishing; phaseTime = 0; }
            }
            else if (phase == Phase.Finishing && phaseTime >= FinishHoldSeconds && raisedObserved &&
                Mathf.Abs(ViewFrame.InverseTransformPoint(Right.Hand.position).x - raisedHand.x) >= .16f)
            { phase = Phase.Complete; return true; }
            return false;
        }
        void Update()
        {
            if (!IsSignalling) { if (FoundationRig != null) FoundationRig.weight = 0; return; }
            if (Paused || Time.timeScale <= 0 || requestFrame != Time.frameCount || Occupied || !ValidSignal())
            { CancelSignal(); return; }
            float t = phase == Phase.Raising ? 0 : phase == Phase.Sweeping ? sweep : 1;
            publishedPosition = Vector3.Lerp(SignalRaiseTarget.position, SignalSweepTarget.position, t);
            publishedRotation = Quaternion.Slerp(SignalRaiseTarget.rotation, SignalSweepTarget.rotation, t);
            activeTarget.SetPositionAndRotation(publishedPosition, publishedRotation);
            // Only independent targets/hints are scene-authored; package jobs own all joint rotations.
            if (activeHint != Right.ElbowHint) activeHint.SetPositionAndRotation(Right.ElbowHint.position, Right.ElbowHint.rotation);
            LeftIK.weight = 0; RightIK.weight = 1; FoundationRig.weight = 1;
            Builder.SyncLayers(); // Official pre-Animator job synchronization, after the root's order200 Update.
            for (int i = 0; i < activeChain.Length; i++) publishedTimes[i] = activeChain[i].GetTime();
            publishedFrame = Time.frameCount;
        }
        void LateUpdate()
        {
            SignalPoseReached = false;
            if (!IsSignalling || publishedFrame != Time.frameCount || observedFrame == Time.frameCount) return;
            if (Paused || Time.timeScale <= 0 || Occupied || !ValidSignal() || FoundationRig.weight < .999f || RightIK.weight < .999f)
            { CancelSignal(); return; }
            for (int i = 0; i < activeChain.Length; i++)
            {
                double time = activeChain[i].GetTime();
                if (double.IsNaN(time) || double.IsInfinity(time) || double.IsNaN(publishedTimes[i]) ||
                    double.IsInfinity(publishedTimes[i]) || time <= publishedTimes[i] || time <= observedTimes[i])
                { CancelSignal(); return; }
                observedTimes[i] = time;
            }
            observedFrame = Time.frameCount;
            observedDeltaTime = Mathf.Clamp(Time.deltaTime, 0, .25f);
            float bend = Vector3.Angle(Right.Forearm.position - Right.UpperArm.position, Right.Hand.position - Right.Forearm.position);
            SignalPoseReached = Reachable(publishedPosition) && bend >= 8 && bend <= 165 &&
                Vector3.Distance(Right.Hand.position, publishedPosition) <= SignalPositionTolerance &&
                Quaternion.Angle(Right.Hand.rotation, publishedRotation) <= SignalAngleTolerance &&
                Vector3.Dot(Right.Hand.position - Right.UpperArm.position, ViewFrame.up) >= .09f;
            solveTick++;
        }
    }
}
