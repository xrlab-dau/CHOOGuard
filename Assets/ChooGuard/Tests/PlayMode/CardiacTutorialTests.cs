#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.AI;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class CardiacTutorialTests
    {
        string key;
        EmergencySession session;
        [UnitySetUp]
        public IEnumerator LoadLesson()
        {
            key = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "cardiac-tutorial-test-no-network");
            Assert.That(PracticeShift.Arm(PracticeShift.CardiacFamily), Is.True);
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            for (int i = 0; i < 300 && (EmergencySession.Current == null || EmergencySession.Current.CardiacLesson == null); i++) yield return null;
            session = EmergencySession.Current;
            Assert.That(session, Is.Not.Null);
            yield return null;
            Assert.That(session.CardiacLesson.Ready, Is.True, session.CardiacLesson.PreparationFailure);
            session.Player.SetExternalInputMode(true);
            Assert.That(session.Player.Resume(false), Is.True);
        }

        [UnityTearDown]
        public IEnumerator UnloadLesson()
        {
            Environment.SetEnvironmentVariable(JevKey.Variable, key);
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene, LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReliefDoesNotDeadlockWhenAgentArrivesWithinNormalFootTolerance()
        {
            var lesson = session.CardiacLesson;
            yield return Act(() => lesson.PatientChest, typeof(Passenger));
            var requests = session.RadioGroupProviders.SelectMany(p => p()).First(g => g.Label == "요청 · 기관");
            requests.Options[1].Send();
            yield return Until(() => lesson.CallAcknowledged, 4, "119 acknowledged before relief regression");
            yield return Act(() => lesson.PatientChest, typeof(Passenger));
            yield return Until(() => lesson.SurfaceReady, 6, "Patient chest surface ready");
            yield return Walk(lesson.CompressionApproach, .04f);
            yield return Act(() => lesson.PatientChest, typeof(Passenger), true);
            yield return Until(() => lesson.PlayerFirstCompressionSeconds >= CardiacTutorialLesson.FirstPracticeSeconds, 20, "Initial hands-on pressure sample");
            // Exercise an ordinary near-arrival margin, rather than relying on one pixel-perfect final agent foot.
            lesson.Colleague.Agent.stoppingDistance = .18f;
            requests.Options.Last(o => o.Label.Contains("압박 이어받기")).Send();
            yield return new WaitForSeconds(25);
            Assert.That(lesson.MayFetchAed, Is.False, "Calling a distant colleague must leave time to keep compressing");
            Assert.That(lesson.Patient.Compressing, Is.True, "Initial six-second sample never orders an automatic stop");
            Assert.That(lesson.MaintainedDuringApproach, Is.GreaterThan(20), "Actual rendered compression continues through the approach");
            yield return Until(() => lesson.MayFetchAed, 65, "Near-arrived colleague must settle, contact the chest and actually take over");
            Assert.That(lesson.ReliefElapsed, Is.InRange(30f, 60f));
            Assert.That(lesson.Patient.AssistedCompressions, Is.True);
            Assert.That(lesson.Patient.Compressing, Is.False);
        }

        [UnityTest]
        public IEnumerator RealSceneLessonRequiresHandsOnActionsAndCanReachHandover()
        {
            var lesson = session.CardiacLesson;
            Assert.That(session.IsCardiacTutorial, Is.True);
            Assert.That(session.Jev.Available, Is.False, "Tutorial must not contact an external judge even when a key is configured");
            Assert.That(HazardRegistry.Active.OfType<CollapseHazard>().Count(), Is.EqualTo(1));
            Assert.That(lesson.Patient.NotBreathingNormally, Is.True);
            Assert.That(Vector3.Distance(session.Player.transform.position, lesson.PatientStart), Is.LessThan(2));
            Assert.That(lesson.Step, Is.EqualTo(CardiacLessonStep.Response));
            Assert.That(lesson.Cabinet.name, Is.EqualTo("aed-police2f-01"));
            Assert.That(lesson.Cabinet.Stored, Is.True);
            session.Incidents.ToggleCompressions(lesson.Patient.Person);
            Assert.That(lesson.Patient.Compressing, Is.False, "No learning credit for unobserved breathing");
            lesson.RequestRelief();
            Assert.That(lesson.ReliefRequested, Is.False);
            session.Incidents.OnTreated(lesson.Patient.Person);
            Assert.That(lesson.Patient.Treated, Is.False, "Agency treatment must not skip the lesson");
            yield return new WaitForSeconds(1);
            Assert.That(session.Incidents.Stage, Is.EqualTo(IncidentDirector.Phase.Incident));
            Assert.That(lesson.CorePracticeComplete, Is.False);
            Assert.That(lesson.Patient.Aed, Is.Null);
            Assert.That(lesson.ResponseChecked, Is.False, "Seeing the casualty must not perform the E response check");
            yield return Act(() => lesson.PatientChest, typeof(Passenger));
            Assert.That(lesson.ResponseChecked, Is.True);
            var requests = session.RadioGroupProviders.SelectMany(p => p()).First(g => g.Label == "요청 · 기관");
            requests.Options[1].Send(); // The actual product radio option, not a fabricated lesson event.
            yield return Until(() => lesson.CallAcknowledged, 4, "Colleague actually acknowledges119 call");
            yield return Act(() => lesson.PatientChest, typeof(Passenger));
            Assert.That(lesson.BreathingChecked, Is.True);
            yield return Until(() => lesson.SurfaceReady, 6, "Existing patient's rendered chest surface qualified");
            yield return Walk(lesson.CompressionApproach, .04f);
            yield return Act(() => lesson.PatientChest, typeof(Passenger), true);
            yield return Until(() => lesson.PlayerFirstCompressionSeconds >= CardiacTutorialLesson.FirstPracticeSeconds, 20, "Visible player compression sample");
            Assert.That(session.Player.PlayerCamera.transform.localPosition.y, Is.LessThan(1.2f), "Player really adopts a lower compression view");
            requests.Options.Last(o => o.Label.Contains("압박 이어받기")).Send();
            yield return Until(() => lesson.MayFetchAed, 65, "Colleague walks from the actual station, kneels and takes over");
            Assert.That(lesson.ReliefElapsed, Is.InRange(30f, 60f));
            Assert.That(lesson.Patient.AssistedCompressions, Is.True);
            Assert.That(lesson.Patient.Compressing, Is.False);
            session.Incidents.ToggleCompressions(lesson.Patient.Person);
            Assert.That(lesson.Patient.Compressing, Is.False, "Player must not overlap the colleague's active compressions");
            yield return new WaitForSeconds(.4f);
            Assert.That(session.Player.PlayerCamera.transform.localPosition.y, Is.EqualTo(session.Player.EyeHeight).Within(.01f), "Relief restores the normal first-person view");
            yield return Walk(lesson.CabinetApproach);
            yield return Act(() => lesson.Cabinet.transform.position, typeof(AedCabinet));
            Assert.That(session.Hands.Aed, Is.SameAs(lesson.Cabinet.Unit));
            yield return Walk(lesson.PlayerStart);
            yield return Act(() => lesson.PatientChest, typeof(Passenger));
            Assert.That(lesson.Cabinet.Unit.Patient, Is.SameAs(lesson.Patient));
            yield return Act(() => lesson.Cabinet.Unit.transform.position + Vector3.up * .08f, typeof(AedUnit));
            yield return Until(() => lesson.Cabinet.Unit.LidOpen, 3, "Physical lid opens");
            for (int i = 0; i < 2; i++)
            {
                var pad = lesson.Cabinet.Unit.PadInLid();
                yield return Act(() => pad.transform.position, typeof(AedPad));
                Assert.That(session.Hands.Pad, Is.SameAs(pad));
                if (i == 0)
                {
                    yield return Act(() => lesson.Patient.Person.Body.Animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, typeof(Passenger));
                    Assert.That(session.Hands.Pad, Is.SameAs(pad), "E on a distant body part must not place the pad");
                    Assert.That(lesson.Cabinet.Unit.Procedure.Pads, Is.EqualTo(0));
                }
                yield return Act(() => lesson.PadAimPoint, typeof(Passenger));
                Assert.That(session.Hands.Pad, Is.Null);
            }
            yield return Act(() => lesson.PatientChest, typeof(Passenger));
            Assert.That(lesson.Patient.AnyoneCompressing, Is.False);
            yield return Until(() => lesson.Cabinet.Unit.Procedure.Charged, 12, "Actual analysis and charging, without patient contact");
            yield return Act(() => lesson.Cabinet.Unit.ShockControl.transform.position, typeof(AedShockControl));
            Assert.That(lesson.Cabinet.Unit.Procedure.Shocks, Is.EqualTo(1));
            yield return Act(() => lesson.Cabinet.Unit.GetComponent<BoxCollider>().bounds.center, typeof(AedUnit), true);
            Assert.That(session.Hands.Aed, Is.Null, "First accidental R on the case explains patient-target CPR instead of switching it off");
            Assert.That(lesson.Cabinet.Unit.Procedure.Current, Is.EqualTo(AedProcedure.Stage.Compressions));
            yield return Walk(lesson.CompressionApproach, .04f);
            yield return Act(() => lesson.PatientChest, typeof(Passenger), true);
            yield return Until(() => lesson.CorePracticeComplete, 20, "Player resumes real visible compressions before arrival teaching transition");
            yield return Until(() => session.Incidents.CardiacLessonLead != null, 35, "Medical lead approaches through the actual scene");
            yield return Until(() => session.Incidents.CardiacLessonHasMedicalContact, 25, "Medical worker actually reaches patient and takes over");
            yield return Until(() => !lesson.Patient.Compressing && Mathf.Abs(session.Player.PlayerCamera.transform.localPosition.y - session.Player.EyeHeight) < .01f, 3, "Medical takeover restores normal camera posture");
            session.Incidents.ToggleCompressions(lesson.Patient.Person);
            Assert.That(lesson.Patient.Compressing, Is.False, "Player must not interfere with actual medical takeover");
            // Actual on-scene lead far beyond the ordinary wait: no automatic prompt or ending.
            const string guidanceKey = "chooguard.guidance";
            bool hadGuidance = PlayerPrefs.HasKey(guidanceKey); int priorGuidance = PlayerPrefs.GetInt(guidanceKey);
            try
            {
                var director = session.Incidents;
                var leadOnScene = director.CardiacLessonLead;
                leadOnScene.GetType().GetProperty("OnSceneAt").SetValue(leadOnScene, Time.time - 1000f);
                var askedAt = director.GetType().GetField("handoverAskedAt", BindingFlags.Instance | BindingFlags.NonPublic);
                askedAt.SetValue(director, -1f);
                foreach (GuidanceLevel level in Enum.GetValues(typeof(GuidanceLevel)))
                {
                    PlayerPrefs.SetInt(guidanceKey, (int)level);
                    director.GetType().GetMethod("UpdateConsequences", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
                    Assert.That(director.Stage, Is.EqualTo(IncidentDirector.Phase.Incident), "Tutorial must not auto-handover after timeout at " + level);
                    Assert.That((float)askedAt.GetValue(director), Is.EqualTo(-1f), "Tutorial must not trigger the autonomous handover clock at " + level);
                }
            }
            finally { if (hadGuidance) PlayerPrefs.SetInt(guidanceKey, priorGuidance); else PlayerPrefs.DeleteKey(guidanceKey); PlayerPrefs.Save(); }
            var lead = session.Incidents.CardiacLessonLead;
            yield return Act(() => lead.transform.position + Vector3.up * 1.2f, typeof(Responder));
            Assert.That(lesson.Step, Is.EqualTo(CardiacLessonStep.Complete), session.Player.LastFeedback);
            Assert.That(session.Incidents.Stage, Is.EqualTo(IncidentDirector.Phase.Ended));
        }

        IEnumerator Until(Func<bool> predicate, float seconds, string message)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < until) yield return null;
            var visual = session.GetComponent<CardiacCompressionVisual>();
            var helper = session.GetComponent<CardiacColleagueCompressionVisual>();
            var teams = string.Join(";", UnityEngine.Object.FindObjectsByType<Responder>(FindObjectsSortMode.None).Select(r => r.name + " at " + r.transform.position + ", arrived=" + r.OnScene + ", " + r.CardiacCareDiagnostics));
            Assert.That(predicate(), Is.True, message + " | " + session.CardiacLesson.Step + " | " + session.Player.LastFeedback + " | player=" + visual?.FailureReason + " | helper=" + helper?.FailureReason + " | teams=" + teams + " | surface=" + session.CardiacLesson.SurfaceDiagnostics + " | " + session.CardiacLesson.ReliefDiagnostics);
            Debug.Log("CG_CARDIAC_TEST " + message);
        }

        void Look(Vector3 point)
        {
            var player = session.Player;
            var delta = point - player.PlayerCamera.transform.position;
            float yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(-delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
            for (int i = 0; i < 8; i++)
                player.StepInput(Vector2.zero, new Vector2(Mathf.DeltaAngle(player.YawDegrees, yaw), player.PitchDegrees - pitch), false, false, false, .02f);
        }

        IEnumerator Act(Func<Vector3> point, Type expected, bool secondary = false)
        {
            Look(point()); yield return null;
            session.Player.RefreshInteraction();
            Assert.That(session.Player.CurrentInteraction, Is.InstanceOf(expected), "Actual aim target: " + session.Player.CurrentTargetCollider?.name + " | " + session.Player.CurrentPrompt + " | point=" + point() + " eye=" + session.Player.PlayerCamera.transform.position);
            session.Player.StepInput(Vector2.zero, Vector2.zero, false, !secondary, false, .02f, secondary);
            yield return null;
            session.Player.StepInput(Vector2.zero, Vector2.zero, false, false, false, .02f, false);
            Debug.Log("CG_CARDIAC_ACTION " + expected.Name + " " + (secondary ? "R" : "E") + " " + session.Player.LastFeedback);
            yield return null;
        }

        IEnumerator Walk(Vector3 destination, float within = .18f)
        {
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(session.Player.transform.position, destination, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, Is.True);
            foreach (var corner in path.corners.Skip(1))
            {
                float end = Time.realtimeSinceStartup + 15;
                while (Vector2.Distance(new Vector2(session.Player.transform.position.x, session.Player.transform.position.z), new Vector2(corner.x, corner.z)) > within && Time.realtimeSinceStartup < end)
                {
                    var local = session.Player.transform.InverseTransformDirection(Vector3.ProjectOnPlane(corner - session.Player.transform.position, Vector3.up).normalized);
                    session.Player.StepInput(new Vector2(local.x, local.z), Vector2.zero, false, false, false, Mathf.Min(Time.deltaTime, .05f));
                    yield return null;
                }
                Assert.That(Vector3.Distance(session.Player.transform.position, corner), Is.LessThan(.5f), "Walk blocked at " + corner + " actual " + session.Player.transform.position);
            }
        }
    }
}
#endif
