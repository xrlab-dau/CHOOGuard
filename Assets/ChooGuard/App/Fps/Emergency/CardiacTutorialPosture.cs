using System;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    public sealed partial class CardiacTutorialLesson
    {
        Camera postureView;
        Vector3 homeEye, compressionFeet;
        Transform surfaceAnchor;
        readonly Vector3[] surfaceSites = new Vector3[3];
        float nextSurfaceAttempt;
        int surfaceAttempts;
        float stanceEyeGap = .5f;
        public bool SurfaceReady { get; private set; }
        public string SurfaceDiagnostics => "attempts=" + surfaceAttempts + ", elapsed=" + (Patient != null ? Time.time - Patient.StartedAt : 0) + ", chest=" + (surfaceAnchor != null ? surfaceAnchor.position.ToString() : PatientChest.ToString());
        public string ReliefDiagnostics => Colleague == null ? "no colleague" : "helper=" + Colleague.transform.position + "; goal=" + helperKneel + "; planning=" + Colleague.Planning + "; noRoute=" + Colleague.NoRoute + "; remaining=" + (Colleague.Agent.isOnNavMesh ? Colleague.Agent.remainingDistance : -1) + "; elapsed=" + ReliefElapsed + "; settled=" + Colleague.TreatmentPoseSettled + "; contact=" + colleagueVisual?.FailureReason;
        void InitializePosture() { postureView = Session.Player.PlayerCamera; homeEye = postureView.transform.localPosition; }

        Vector3 ReadSurface(Transform bone)
        {
            if (bone == null) return PatientStart + Vector3.up * .3f;
            var animator = Patient?.Person?.Body?.Animator;
            var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            bool lying = head != null && hips != null && Mathf.Abs(head.position.y - hips.position.y) < .35f && bone.position.y - PatientStart.y < .75f;
            if (!SurfaceReady && Patient != null && Time.time >= Patient.StartedAt + 3f && Time.time >= nextSurfaceAttempt && lying)
            { nextSurfaceAttempt = Time.time + 1f; TryBindSurface(bone); }
            return SurfaceReady ? surfaceAnchor.TransformPoint(surfaceSites[0]) : bone.position + Vector3.up * .10f;
        }

        void TryBindSurface(Transform bone)
        {
            try
            {
                var axis = Vector3.ProjectOnPlane(PatientAxis, Vector3.up).normalized;
                var side = Vector3.Cross(Vector3.up, axis);
                var points = new[] { bone.position, bone.position + axis * .10f - side * .09f, bone.position - axis * .08f + side * .13f };
                var tops = new[] { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };
                foreach (var renderer in Patient.Person.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var snapshot = new Mesh();
                    try
                    {
                        renderer.BakeMesh(snapshot);
                        foreach (var local in snapshot.vertices)
                        {
                            var world = renderer.transform.TransformPoint(local);
                            if (world.y < bone.position.y - .05f || world.y > bone.position.y + .35f) continue;
                            for (int i = 0; i < points.Length; i++)
                                if (Vector2.SqrMagnitude(new Vector2(world.x - points[i].x, world.z - points[i].z)) < .075f * .075f)
                                    tops[i] = Mathf.Max(tops[i], world.y);
                        }
                    }
                    finally { Destroy(snapshot); }
                }
                for (int i = 0; i < tops.Length; i++)
                {
                    if (float.IsNegativeInfinity(tops[i])) throw new InvalidOperationException("기존 환자 메시에서 가슴 표면을 찾지 못했습니다.");
                    points[i].y = tops[i] + .008f; surfaceSites[i] = bone.InverseTransformPoint(points[i]);
                }
                surfaceAnchor = bone; SurfaceReady = true;
                Debug.Log("CG_CARDIAC_SURFACE_BOUND chest=" + surfaceAnchor.TransformPoint(surfaceSites[0]));
            }
            catch (Exception error)
            {
                if (++surfaceAttempts < 3) return;
                PreparationFailure = error.Message; Ready = false; SetStep(CardiacLessonStep.Failed); Debug.LogException(error);
            }
        }

        public Vector3 PadSite(int index)
        {
            var centre = PatientChest;
            if (SurfaceReady) return surfaceAnchor.TransformPoint(surfaceSites[index == 0 ? 1 : 2]);
            var axis = Vector3.ProjectOnPlane(PatientAxis, Vector3.up).normalized;
            var side = Vector3.Cross(Vector3.up, axis);
            return index == 0 ? centre + axis * .10f - side * .09f : centre - axis * .08f + side * .13f;
        }

        public Vector3 CompressionApproach
        {
            get
            {
                var chest = PatientChest;
                var towardPlayer = Vector3.ProjectOnPlane(PlayerStart - chest, Vector3.up).normalized;
                var desired = new Vector3(chest.x, PatientStart.y, chest.z) + towardPlayer * .52f;
                return NavMesh.SamplePosition(desired, out var hit, .15f, NavMesh.AllAreas) ? hit.position : desired;
            }
        }

        Vector3 CompressionEyePosition
        {
            get
            {
                var normalEye = Session.Player.transform.TransformPoint(homeEye);
                var surface = PatientChest;
                float eyeY = surface.y + stanceEyeGap;
                float t = Mathf.Clamp01((normalEye.y - eyeY) / Mathf.Max(.1f, normalEye.y - surface.y));
                return Vector3.Lerp(normalEye, surface, t);
            }
        }

        bool FindCompressionEyePosition()
        {
            var surface = PatientChest;
            var toward = Vector3.ProjectOnPlane(surface - Session.Player.transform.position, Vector3.up).normalized;
            // Choose the highest comfortable view that the original arms can reach throughout the visual stroke.
            for (float gap = .70f; gap >= .32f; gap -= .015f)
            {
                stanceEyeGap = gap;
                if (compressionVisual.CanReachFrom(CompressionEyePosition, surface - Vector3.up * .025f, PatientAxis, toward)) return true;
            }
            return false;
        }

        void LateUpdate()
        {
            if (postureView == null || !Ready) return;
            bool pressing = Patient != null && Patient.Compressing && !Finished;
            if (pressing && Vector3.ProjectOnPlane(Session.Player.transform.position - compressionFeet, Vector3.up).magnitude > .10f)
            { Patient.StopCompressions("압박 자세에서 이동함"); pressing = false; }
            Session.Hud.HideTargetPrompt = pressing;
            if (Session.Player.IsPaused) return;
            var target = pressing ? Session.Player.transform.InverseTransformPoint(CompressionEyePosition) : homeEye;
            postureView.transform.localPosition = Vector3.MoveTowards(postureView.transform.localPosition, target, Time.deltaTime * 4f);
        }

        void RestorePosture()
        {
            if (postureView != null) postureView.transform.localPosition = homeEye;
            if (Session?.Hud != null) Session.Hud.HideTargetPrompt = false;
        }
    }
}
