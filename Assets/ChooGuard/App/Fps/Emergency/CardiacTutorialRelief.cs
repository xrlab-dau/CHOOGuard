using System;
using System.Linq;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public sealed partial class CardiacTutorialLesson
    {
        // A fixed teaching scene's real approach, not a clinical compression-cycle duration.
        public const float ColleagueApproachSeconds = 40f, MinimumApproachSeconds = 30f;
        Vector3 helperStandBack;
        float helperWalkSpeed = 1.6f, reliefRequestedAt = -1f, helperNearSince = -1f;
        float helperContactSeconds, nextReliefRoute, maintainedDuringApproach;
        bool helperAtPatient;
        public float ReliefElapsed => ReliefRequested ? Mathf.Max(0, Time.time - reliefRequestedAt) : 0;
        public float MaintainedDuringApproach => maintainedDuringApproach;
        public bool ColleagueAtPatient => helperAtPatient;

        Vector3 ResolveColleagueStart(Vector3 patient, Vector3 kneel, float floor)
        {
            // Reuse current physically bound station destinations. Their old role labels are not SOP authority.
            var candidates = Session.World.Points.All.Where(p => Mathf.Abs(p.Position.y - floor) < .3f &&
                (p.Kind == PointKind.Office || p.Kind == PointKind.Meet || p.Kind == PointKind.Wait || p.Kind == PointKind.Counter))
                .Where(p => Vector3.Distance(p.Position, patient) >= 35 && Vector3.Distance(p.Position, patient) <= 80)
                .OrderBy(p => p.Kind == PointKind.Office ? 0 : 1)
                .ThenBy(p => Mathf.Abs(Vector3.Distance(p.Position, patient) - 60)).ThenBy(p => p.Id, StringComparer.Ordinal);
            foreach (var point in candidates)
            {
                try
                {
                    var start = QualifyFeet(point.Position, floor);
                    float length = RequirePhysicalRoute(start, kneel, 80);
                    if (length < 48) continue;
                    helperWalkSpeed = length / ColleagueApproachSeconds; // 1.2–2m/s; ordinary walking/quick approach.
                    Debug.Log("CG_CARDIAC_HELPER_ROUTE point=" + point.Id + "; length=" + length + "; speed=" + helperWalkSpeed);
                    return start;
                }
                catch (InvalidOperationException) { }
            }
            throw new InvalidOperationException("같은2층의 기존 통로에서 동료가30~60초 동안 접근할 출발 위치를 확보하지 못했습니다.");
        }

        bool UpdateColleagueRelief(bool playerPresenting)
        {
            if (!ReliefRequested || helperWorking || stopping || Patient.Aed != null || Session.Hands.Aed != null) return false;
            if (playerPresenting) maintainedDuringApproach += Time.deltaTime;
            var delta = Colleague.transform.position - helperKneel;
            // NavMesh agent feet need not coincide with one exact 3D point. Actual hand contact is checked separately.
            bool near = new Vector2(delta.x, delta.z).magnitude <= .22f && Mathf.Abs(delta.y) < .25f;
            if (!near)
            {
                helperAtPatient = false; helperNearSince = -1; helperContactSeconds = 0;
                if (Time.time >= nextReliefRoute && !Colleague.Kneeling && (Colleague.NoRoute || !Colleague.EnRoute))
                { nextReliefRoute = Time.time + 2; Colleague.GoTo(helperKneel, helperWalkSpeed); }
                return false;
            }
            if (helperNearSince < 0)
            {
                helperNearSince = Time.time;
                Colleague.Stop(); Colleague.Face(PatientChest);
                Note("colleague-at-near-foot; elapsed=" + ReliefElapsed.ToString("F1"));
            }
            helperAtPatient = true;
            Colleague.SetTreat(true);
            if (Time.time - helperNearSince > .75f && !Colleague.TreatmentPoseSettled) Colleague.KneelNow();
            bool pose = Colleague.TreatmentPoseSettled && ReliefElapsed >= MinimumApproachSeconds;
            helperContactSeconds = pose && colleagueVisual.IsPresenting ? helperContactSeconds + Time.deltaTime : 0;
            if (helperContactSeconds >= .25f)
            {
                helperWorking = true; reliefEstablished = true; Patient.AssistedCompressions = true;
                Patient.StopCompressions("동료가 실제로 압박을 이어받음");
                Session.Hud.Radio.Push(Hud.RadioChannel.Colleague, "제가 압박을 이어가겠습니다. 안내된 원래 AED를 가져와 주세요.");
                Note("colleague-at-patient-and-compressing; elapsed=" + ReliefElapsed.ToString("F1") + "; maintained=" + maintainedDuringApproach.ToString("F1"), "동료가 환자 곁에서 실제 압박 교대");
            }
            return pose;
        }
    }
}
