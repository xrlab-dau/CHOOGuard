using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    public sealed partial class CardiacTutorialLesson
    {
        Vector3 patientFacing;
        Vector3 PatientAxis
        {
            get
            {
                var animator = Patient?.Person?.Body?.Animator;
                var hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                var head = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                return hips != null && head != null ? (head.position - hips.position).normalized : patientFacing;
            }
        }
        public Vector3 PatientChest
        {
            get
            {
                var body = Patient?.Person?.Body;
                var bone = body?.Animator != null ? body.Animator.GetBoneTransform(HumanBodyBones.Chest) : null;
                if (bone == null && body?.Animator != null) bone = body.Animator.GetBoneTransform(HumanBodyBones.UpperChest);
                return ReadSurface(bone);
            }
        }

        void ResolveLayout()
        {
            Cabinet = UnityEngine.Object.FindObjectsByType<AedCabinet>(FindObjectsSortMode.None)
                .SingleOrDefault(c => c.name == "aed-police2f-01" && c.gameObject.activeInHierarchy);
            if (Cabinet == null || !Cabinet.Stored || Cabinet.Unit == null)
                throw new InvalidOperationException("기존 철도경찰 앞 AED 보관함을 찾지 못했습니다. 임시 장비로 대체하지 않습니다.");
            float floor = Cabinet.transform.position.y - 1.35f; // Original fixture's recorded MODEL wall mounting offset.
            var front = Vector3.ProjectOnPlane(Cabinet.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, front);
            var origin = new Vector3(Cabinet.transform.position.x, floor, Cabinet.transform.position.z);
            CabinetApproach = QualifyFeet(origin + front * .95f, floor);
            var rejected = new System.Collections.Generic.List<string>();
            // Bounded, deterministic candidates relative to the actual unmodified fixture.
            foreach (var forward in new[] { 3.4f, 2.8f, 4f })
            foreach (var lateral in new[] { 0f, 1.5f, -1.5f, 3f, -3f })
            {
                try
                {
                    var patient = QualifyFeet(origin + front * forward + right * lateral, floor);
                    var player = QualifyFeet(patient + right * 1.25f + front * .3f, floor);
                    var helper = QualifyFeet(patient + right * 2.1f + front * .8f, floor);
                    var kneel = QualifyFeet(patient - right * .65f, floor);
                    var arrival = QualifyFeet(patient + front * 2.8f - right * 1.3f, floor);
                    // The whole lying body needs room, not only its standing agent capsule.
                    if (Physics.CheckCapsule(patient + front * .7f + Vector3.up * .45f,
                        patient - front * .7f + Vector3.up * .45f, .27f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    RequirePhysicalRoute(player, patient + front * .8f);
                    RequirePhysicalRoute(player, CabinetApproach);
                    RequirePhysicalRoute(helper, kneel);
                    RequirePhysicalRoute(arrival, player);
                    if (!ClearLine(player + Vector3.up * 1.6f, patient + Vector3.up * .4f)) continue;
                    if (!ClearLine(CabinetApproach + Vector3.up * 1.6f, Cabinet.transform.position, Cabinet.transform)) continue;
                    helperStandBack = helper;
                    helperStart = ResolveColleagueStart(patient, kneel, floor);
                    PatientStart = patient; PlayerStart = player; helperKneel = kneel; agencyStart = arrival;
                    patientFacing = front;
                    return;
                }
                catch (InvalidOperationException error) { rejected.Add(forward + "/" + lateral + ": " + error.Message); }
            }
            throw new InvalidOperationException("기존 AED 근처에 환자·동료·왕복 동선의 실제 여유를 확보하지 못했습니다.\n" + string.Join("\n", rejected));
        }

        Vector3 QualifyFeet(Vector3 desired, float floor)
        {
            if (!NavMesh.SamplePosition(desired, out var hit, .35f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - floor) > .3f)
                throw new InvalidOperationException("같은2층 보행면을 확인하지 못했습니다: " + desired);
            var ground = Physics.RaycastAll(hit.position + Vector3.up * .35f, Vector3.down, .8f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.normal.y > .75f).OrderBy(h => h.distance).FirstOrDefault();
            if (ground.collider == null || Mathf.Abs(ground.point.y - floor) > .3f)
                throw new InvalidOperationException("실제 바닥 지지가 없습니다: " + desired);
            var feet = ground.point + Vector3.up * .05f;
            var blockers = Physics.OverlapCapsule(feet + Vector3.up * .34f, feet + Vector3.up * 1.4f, .28f, ~0, QueryTriggerInteraction.Ignore);
            if (blockers.Length > 0)
                throw new InvalidOperationException("사람 자리를 막는 실제 물체: " + desired + " / " + string.Join(",", blockers.Select(c => c.name)));
            return feet;
        }

        static bool ClearLine(Vector3 eye, Vector3 point, Transform allowed = null)
        {
            var delta = point - eye;
            return !Physics.RaycastAll(eye, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                .Any(h => allowed == null || !h.collider.transform.IsChildOf(allowed));
        }

        static float RequirePhysicalRoute(Vector3 from, Vector3 to, float maximumLength = 14f)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("환자와 기존 AED 사이에 연결된 보행 경로가 없습니다.");
            float length = 0;
            for (int i = 1; i < path.corners.Length; i++)
            {
                var delta = path.corners[i] - path.corners[i - 1]; length += delta.magnitude;
                if (Physics.CapsuleCastAll(path.corners[i - 1] + Vector3.up * .36f, path.corners[i - 1] + Vector3.up * 1.4f,
                    .25f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore).Any(h => h.distance < delta.magnitude - .08f))
                    throw new InvalidOperationException("안내할 경로를 실제 물체가 막고 있습니다.");
            }
            if (length > maximumLength) throw new InvalidOperationException("고정 학습 동선이 불필요하게 깁니다: " + length);
            return length;
        }

    }
}
