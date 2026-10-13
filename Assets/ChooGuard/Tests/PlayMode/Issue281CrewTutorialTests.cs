using System;
using System.Collections;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Shell;
using ChooGuard.App.Fps.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class Issue281CrewTutorialTests
    {
        private string previousKey;
        private CrewInspectionSession session;
        private static readonly Vector3 Axis = new Vector3(.347234f, 0, .937778f).normalized;
        private static readonly Vector3 Aisle = new Vector3(71.99928f, .4500034f, 30.76221f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousKey = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
            Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", "off");
            yield return SceneManager.LoadSceneAsync("CrewTrainTutorial", LoadSceneMode.Single);
            yield return null;
            session = Object.FindFirstObjectByType<CrewInspectionSession>();
            Assert.NotNull(session);
            Assert.IsTrue(session.Initialize(out var reason), reason);
            session.Responder.SetExternalInputMode(true);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return null;
            Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", previousKey);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator GuidedInspectionWorksThroughRealWalkingAndSolidRaycasts()
        {
            Assert.IsFalse(SceneManager.GetSceneByName("StationEmergency").isLoaded);
            Assert.IsFalse(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(b => b.GetType().Namespace == "ChooGuard.App.Fps.Emergency"));
            var originalLuggage = session.Luggage.position;
            var originalDoor = session.DoorLeaves[0].position;
            session.Begin();
            Assert.IsTrue(session.Started);
            Assert.IsTrue(session.Plan.Steps.All(s => s.GuidanceVoice != null && s.GuidanceVoice.length > 1 && !string.IsNullOrWhiteSpace(s.Observation)), "Every step needs authored coaching and concrete observation points.");
            Assert.IsTrue(session.RadioDisplay.text.Contains("85%"), "Battery is shown on the actual radio.");
            Assert.AreEqual(13, session.Plan.Steps.Length);
            Assert.IsFalse(session.Points.Any(p => p.Id == "medical"));
            Assert.IsFalse(session.Plan.Steps.Any(s => s.TargetId == "medical"));
            var voices = session.Plan.Steps.SelectMany(s => new[] { s.GuidanceVoice, s.ResponseVoice }).Where(v => v != null).Distinct().ToArray();
            Write("voice-duration.txt", voices.Length + " clips · " + voices.Sum(v => v.length).ToString("F2") + " seconds");
            // A later item must never be accepted simply because it is in reach.
            Assert.IsFalse(session.Points.Single(p => p.Id == "climate").CanInteract(session.Responder, out _));
            session.Continue();
            Assert.AreEqual(0, session.StepIndex, "Reading acknowledgement cannot skip an unperformed action.");
            Shot(session.Responder.PlayerCamera, "01-start");
            var transcript = "";
            for (int stage = 0; stage < session.Plan.Steps.Length; stage++)
            {
                Assert.AreEqual(stage, session.StepIndex);
                var step = session.Current;
                if (step.HandheldRadio) Assert.IsTrue(SendCurrentRadio(), step.Title);
                else
                {
                    var point = session.CurrentPoint;
                    float along = Vector3.Dot(point.transform.position - Aisle, Axis);
                    var feet = Aisle + Axis * Mathf.Clamp(along, -.7f, 10f) + Vector3.up * .03f;
                    if (point.Id == "intercom") { feet = point.Marker.position + point.transform.forward * 1.05f; feet.y = Aisle.y + .03f; }
                    yield return WalkTo(feet);
                    Assert.That(session.Responder.transform.position.y, Is.InRange(.35f, .7f), "Walkable cabin floor: " + point.Id);
                    Aim(point);
                    session.Responder.RefreshInteraction();
                    Shot(session.Responder.PlayerCamera, "aim-" + (stage + 1).ToString("00"));
                    Assert.AreSame(point, session.Responder.CurrentInteraction, DescribeRay(point));
                    yield return null;
                    Shot(session.Responder.PlayerCamera, "step-" + (stage + 1).ToString("00"));
                    session.Responder.StepInput(Vector2.zero, Vector2.zero, false, false, false, .02f);
                    session.Responder.StepInput(Vector2.zero, Vector2.zero, false, true, false, .02f);
                    Assert.IsTrue(session.Acting, point.Id + " · " + session.Responder.LastFeedback);
                }
                Assert.IsFalse(session.ReadingResult, "A button press starts a visible/audible action rather than immediately completing it.");
                session.Continue();
                Assert.AreEqual(stage, session.StepIndex, "Enter cannot skip a device response or moving geometry.");
                Assert.IsFalse(session.StepRadioInput(true, false, Vector2.zero), "An action in progress cannot be repeated.");
                if (step.Action == CrewInspectionAction.IntercomTest)
                {
                    Assert.AreEqual("intercom", session.CurrentPoint.Id);
                    Assert.IsTrue(session.PaTested);
                    Assert.AreEqual("호출 중", session.IntercomState);
                    Assert.IsFalse(session.RadioDisplay.text.Contains("TX"), "The wired intercom must not change the portable radio to transmit mode.");
                }
                if (step.Action == CrewInspectionAction.CabinInspect)
                {
                    var cabin = session.CurrentPoint;
                    Assert.AreEqual(3, cabin.InspectionTargets.Length);
                    Aim(cabin.InspectionTargets[0]);
                    yield return new WaitForSeconds(2.2f);
                    Assert.IsTrue(session.Acting, "Looking only at the seat cannot complete cabin inspection.");
                    session.Continue(); Assert.AreEqual(stage, session.StepIndex);
                }
                if (step.Action == CrewInspectionAction.OpenDoor)
                {
                    yield return new WaitForSeconds(.35f);
                    Assert.That(session.DoorOpenAmount, Is.GreaterThan(.35f).And.LessThan(1f), "The FBX door moves continuously, not by teleportation.");
                    session.Responder.RestorePhysicalPose(session.Responder.transform.position, session.Responder.transform.eulerAngles.y + 180, 0);
                    yield return new WaitForSeconds(2);
                    Assert.IsTrue(session.AwaitingObservation, "Operating the panel alone is insufficient; observe the door.");
                    session.Continue(); Assert.AreEqual(stage, session.StepIndex);
                }
                if (step.Action == CrewInspectionAction.ClearLuggage)
                {
                    yield return new WaitForSeconds(step.ResponseVoice.length * .55f);
                    Assert.That(Vector3.Distance(originalLuggage, session.Luggage.position), Is.GreaterThan(.01f));
                    Assert.That(Vector3.Distance(session.LuggageClearPosition, session.Luggage.position), Is.GreaterThan(.01f), "Luggage rolls through an intermediate position after the response.");
                }
                Shot(session.Responder.PlayerCamera, "response-" + (stage + 1).ToString("00"));
                float responseDeadline = Time.realtimeSinceStartup + 20;
                while (session.Acting && Time.realtimeSinceStartup < responseDeadline)
                {
                    if (step.Action == CrewInspectionAction.CabinInspect && session.AttentionTarget != null) Aim(session.AttentionTarget);
                    else if (session.ObservationPoint != null) Aim(session.ObservationPoint);
                    yield return null;
                }
                Assert.IsTrue(session.ReadingResult, "Device response finished: " + step.Title);
                if (step.Action == CrewInspectionAction.IntercomTest) Assert.IsTrue(session.IntercomTested && session.IntercomState.Contains("종료"));
                if (step.Action == CrewInspectionAction.ClimateAdjust)
                {
                    Assert.AreEqual(2,session.CabinSectionsChecked);
                    Assert.IsTrue(session.CoolingAdjusted);
                    Assert.IsTrue(session.ClimateDisplay.text.Contains("설정 적용"));
                }
                Shot(session.Responder.PlayerCamera, "result-" + (stage + 1).ToString("00"));
                Assert.IsFalse(session.StepRadioInput(true, false, Vector2.zero), "An already completed action is not repeated.");
                transcript += step.Title + " · " + session.LastResult.Replace("\n", " / ") + "\n";
                if (step.Action == CrewInspectionAction.DoorInspect)
                {
                    Assert.IsTrue(session.DoorInspected);
                    Assert.IsFalse(session.DepartureHeld);
                    Assert.AreEqual(originalLuggage, session.Luggage.position, "The fault was already present at the first door inspection.");
                    Assert.AreEqual(originalDoor, session.DoorLeaves[0].position);
                }
                if (step.Action == CrewInspectionAction.OpenDoor)
                {
                    Assert.That(Vector3.Distance(originalDoor, session.DoorLeaves[0].position), Is.GreaterThan(.5f), "The actual FBX door must open visibly.");
                }
                if (step.Action == CrewInspectionAction.ClearLuggage)
                    Assert.That(Vector3.Distance(originalLuggage, session.Luggage.position), Is.GreaterThan(.5f), "The obstruction must visibly move.");
                yield return null;
                session.Continue();
            }
            Assert.IsTrue(session.Finished);
            Assert.IsTrue(session.Responder.IsPaused);
            Assert.IsTrue(session.IntercomTested && session.PaTested && session.DepartureHeld && session.LuggageCleared);
            Assert.AreEqual(2,session.CabinSectionsChecked); Assert.IsTrue(session.CoolingAdjusted);
            Assert.AreEqual(0f, session.DoorOpenAmount);
            yield return null;
            Shot(session.Responder.PlayerCamera, "13-complete");
            Write("walkthrough.txt", transcript);
            session.Restart();
            Assert.IsFalse(session.Started || session.Finished || session.DepartureHeld || session.LuggageCleared);
            Assert.IsFalse(session.IntercomTested || session.CoolingAdjusted); Assert.AreEqual(0,session.CabinSectionsChecked);
            Assert.AreEqual(originalLuggage, session.Luggage.position);
            Assert.AreEqual(originalDoor, session.DoorLeaves[0].position);
            Assert.That(Vector3.Distance(session.StartFeet, session.Responder.transform.position), Is.LessThan(.01f));
        }

        [UnityTest]
        public IEnumerator PauseAndLongReadingDoNotCompleteOrLoseTheCurrentStage()
        {
            session.Begin();
            Aim(session.CurrentPoint);
            session.Responder.RefreshInteraction();
            Assert.IsTrue(session.Perform(session.CurrentPoint, session.Responder), DescribeRay(session.CurrentPoint));
            float actionProgress = session.ActionProgress;
            session.Responder.Pause();
            session.Continue();
            Assert.AreEqual(0, session.StepIndex);
            Assert.IsTrue(session.Acting);
            float originalScale = Time.timeScale;
            Time.timeScale = 100;
            yield return new WaitForSeconds(600);
            Time.timeScale = originalScale;
            Assert.AreEqual(actionProgress, session.ActionProgress, "Pausing preserves the equipment action and its audio timeline.");
            Assert.IsFalse(session.ReadingResult);
            Assert.AreEqual(0, session.StepIndex, "Six hundred simulated seconds do not create a timeout or auto-advance.");
            session.Resume();
            yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(session.ReadingResult);
            session.Continue();
            Assert.AreEqual(1, session.StepIndex);
        }

        [UnityTest]
        public IEnumerator MutedCoachingStillCompletesPracticeAndRestartCancelsAnActiveResponse()
        {
            session.Begin(); session.ToggleVoice();
            Assert.IsFalse(session.VoiceEnabled); Assert.IsFalse(session.CoachSpeaking);
            Aim(session.CurrentPoint); session.Responder.RefreshInteraction();
            Assert.IsTrue(session.Perform(session.CurrentPoint, session.Responder));
            yield return new WaitForSeconds(.3f);
            session.Restart();
            Assert.IsFalse(session.Acting || session.ReadingResult || session.Started);
            session.Begin(); Aim(session.CurrentPoint); session.Responder.RefreshInteraction();
            Assert.IsTrue(session.Perform(session.CurrentPoint, session.Responder));
            yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(session.ReadingResult);
            session.Continue();
            yield return WalkTo(Aisle+Axis*4.8f+Vector3.up*.03f);
            Aim(session.CurrentPoint); session.Responder.RefreshInteraction();
            session.ToggleVoice();
            Assert.IsTrue(session.Perform(session.CurrentPoint, session.Responder));
            yield return new WaitForSeconds(.3f);
            session.ToggleVoice();
            yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(session.PaTested && session.ReadingResult, "Muting an active reply adds no artificial full-clip waiting period.");
            session.Continue();
            var intercomFeet = session.CurrentPoint.Marker.position + session.CurrentPoint.transform.forward * 1.05f; intercomFeet.y = Aisle.y + .03f;
            yield return WalkTo(intercomFeet);
            session.RadioPowerOn=false;
            Aim(session.CurrentPoint); session.Responder.RefreshInteraction();
            Assert.IsTrue(session.Perform(session.CurrentPoint,session.Responder),DescribeRay(session.CurrentPoint));
            yield return new WaitForSeconds(2.2f);
            Assert.IsTrue(session.IntercomTested&&session.ReadingResult,"A wired intercom operates independently of portable-radio power.");
            Assert.IsFalse(session.RadioDisplay.text.Contains("TX")); session.RadioPowerOn=true;
            session.ToggleVoice(); session.ReplayGuidance();
            Assert.IsTrue(session.VoiceEnabled);
        }

        [UnityTest]
        public IEnumerator IntercomPanelAndDisplayedMarkerCanBeUsedFromNearbyCabinPositions()
        {
            session.Begin(); session.ToggleVoice();
            for (int stage = 0; stage < 2; stage++)
            {
                float along = Vector3.Dot(session.CurrentPoint.transform.position - Aisle, Axis);
                yield return WalkTo(Aisle + Axis * Mathf.Clamp(along, -.7f, 10f) + Vector3.up * .03f);
                Aim(session.CurrentPoint); session.Responder.RefreshInteraction();
                Assert.IsTrue(session.Responder.TryInteract(), DescribeRay(session.CurrentPoint));
                yield return new WaitForSeconds(2.2f);
                Assert.IsTrue(session.ReadingResult); session.Continue();
            }
            var intercom = session.CurrentPoint;
            Assert.AreEqual("intercom", intercom.Id);
            var callButton = intercom.Marker.position - Vector3.up * .05f + intercom.transform.right * .025f + intercom.transform.forward * .012f;
            var aims = new[] {
                callButton,
                callButton + Vector3.up * .045f - intercom.transform.right * .025f,
                callButton + Vector3.up * .085f - intercom.transform.right * .025f,
                intercom.Marker.position + Vector3.up * .04f
            };
            var probe = new GameObject("Intercom aiming probe").transform;
            try
            {
                foreach (var approach in new[] { new Vector2(1.05f, 0), new Vector2(1.15f, .12f), new Vector2(1.20f, .24f) })
                {
                    var feet = intercom.Marker.position + intercom.transform.forward * approach.x + intercom.transform.right * approach.y; feet.y = Aisle.y + .03f;
                    yield return WalkTo(feet);
                    foreach (var aim in aims)
                    {
                        probe.position = aim; Aim(probe); session.Responder.RefreshInteraction();
                        Assert.AreSame(intercom, session.Responder.CurrentInteraction,
                            "Panel/marker must be usable from nearby cabin positions. " + DescribeRay(intercom));
                        Assert.IsTrue(session.Responder.CurrentPrompt.StartsWith("E"));
                    }
                }
                probe.position = callButton + intercom.transform.right * .3f;
                Aim(probe); session.Responder.RefreshInteraction();
                Assert.AreNotSame(intercom, session.Responder.CurrentInteraction, "An adjacent wall must not call the intercom.");
                probe.position = new Vector3(71.556f, 2.011f, 31.872f);
                Aim(probe); session.Responder.RefreshInteraction();
                Assert.AreNotSame(intercom, session.Responder.CurrentInteraction, "The old detached glass/wall position must not trigger the intercom.");
                probe.position = intercom.Marker.position + Vector3.up * .04f;
                Aim(probe); session.Responder.RefreshInteraction();
                yield return null;
                Shot(session.Responder.PlayerCamera, "intercom-panel-recognized");
                Shot(session.Responder.PlayerCamera, "intercom-panel-recognized-169", 1920, 1080);
                Assert.IsTrue(session.Responder.TryInteract(), "E must start the actual intercom call.");
                Assert.AreEqual("호출 중", session.IntercomState);
                yield return new WaitForSeconds(2.2f);
                Assert.IsTrue(session.IntercomTested && session.ReadingResult);
                session.Continue(); Assert.AreEqual("extinguisher", session.CurrentPoint.Id);
            }
            finally { Object.DestroyImmediate(probe.gameObject); }
        }

        [UnityTest]
        public IEnumerator CategoryMenuCanStartWithoutJevAndCanBeReturnedTo()
        {
            session.Exit();
            yield return null; yield return null;
            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.NotNull(title);
            var menu = Object.FindFirstObjectByType<TutorialMenu>();
            Assert.NotNull(menu, "Completion/cancellation returns to tutorial categories.");
            Shot(Object.FindFirstObjectByType<Camera>(), "00-categories");
            var card = menu.GetComponentsInChildren<Button>().Single(b => b.name == "승무원 열차점검");
            card.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 90;
            while (SceneManager.GetActiveScene().name != "CrewTrainTutorial" && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual("CrewTrainTutorial", SceneManager.GetActiveScene().name);
            Assert.IsFalse(SceneManager.GetSceneByName("StationEmergency").isLoaded);
            Assert.IsFalse(SceneFlow.Loading);
        }

        [Test]
        public void AdditionalCategoriesUseDataAndRejectDuplicateIdentities()
        {
            var catalog = ScriptableObject.CreateInstance<TutorialCatalog>();
            try
            {
                catalog.Categories = new[] {
                    new TutorialCategory { Id = "crew", Title = "승무원", SceneName = "CrewTrainTutorial" },
                    new TutorialCategory { Id = "future", Title = "새 교육", SceneName = "FutureTutorial", ExpectedMinSeconds = 60, ExpectedMaxSeconds = 120 }
                };
                Assert.IsTrue(catalog.Validate(out var reason), reason);
                catalog.Categories[1].Id = "crew";
                Assert.IsFalse(catalog.Validate(out _));
            }
            finally { Object.DestroyImmediate(catalog); }
        }

        [UnityTest]
        public IEnumerator ReportSendsWhenMouseStaysOnSameMessageAfterOpeningGroup()
        {
            var guidance=GameSettings.Guidance;
            try
            {
                GameSettings.Guidance=GuidanceLevel.Standard;session.Begin();session.ToggleVoice();
                typeof(CrewInspectionSession).GetProperty("StepIndex").SetValue(session,12);
                typeof(CrewInspectionSession).GetProperty("DepartureHeld").SetValue(session,true);
                typeof(CrewInspectionSession).GetProperty("LuggageCleared").SetValue(session,true);
                typeof(CrewInspectionSession).GetMethod("SetDoor",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(session,new object[]{0f});
                session.StepRadioInput(true,true,Vector2.zero);
                session.StepRadioInput(false,true,Vector2.up*80,true);
                Assert.AreEqual(0,session.RadioGroup);
                // Group and message occupy the same top position. No new mouse movement.
                session.StepRadioInput(false,true,Vector2.zero);
                Shot(session.Responder.PlayerCamera,"report-group-stationary");
                Assert.IsTrue(session.StepRadioInput(false,false,Vector2.zero),"Releasing Q over the report message must transmit: "+session.Responder.LastFeedback);
                Assert.IsTrue(session.Acting);Assert.IsFalse(session.Wheel.Open);
                yield return new WaitForSeconds(2.2f);
                Assert.IsTrue(session.ReadingResult);
                session.Continue();Assert.IsTrue(session.Finished);
            }
            finally{GameSettings.Guidance=guidance;}
        }
        [Test]
        public void BroadcastSpeakerIsMountedAgainstRealCeiling()
        {
            var point=session.Points.Single(p=>p.Id=="pa");
            var ceiling=point.transform.parent.GetComponent<MeshCollider>();
            Assert.NotNull(ceiling,"Added tutorial equipment must be attached to a real train ceiling surface.");
            var renderers=point.GetComponentsInChildren<MeshRenderer>();
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            var backfaces=Physics.queriesHitBackfaces;RaycastHit hit;
            try{Physics.queriesHitBackfaces=true;Assert.IsTrue(ceiling.Raycast(new Ray(bounds.center,point.transform.up),out hit,1f),"The cabinet top must meet the train ceiling, not hang in open space.");}
            finally{Physics.queriesHitBackfaces=backfaces;}
            float highest=renderers.SelectMany(r=>r.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v=>Vector3.Dot(r.transform.TransformPoint(v),point.transform.up))).Max();
            Assert.That(Mathf.Abs(highest-Vector3.Dot(hit.point,point.transform.up)),Is.LessThan(.012f),"Speaker housing must touch the ceiling within 12mm.");
        }
        private bool SendCurrentRadio()
        {
            int stage = session.StepIndex;
            var previousGuidance = GameSettings.Guidance;
            try
            {
                foreach (var guidance in new[] { GuidanceLevel.Guided, GuidanceLevel.Standard })
                {
                    GameSettings.Guidance = guidance;
                    session.StepRadioInput(true, true, Vector2.zero);
                    Assert.IsTrue(session.Wheel.Open);
                    Assert.IsFalse(session.Acting, "Pressing Q opens selection, not a transmission.");
                    Assert.IsTrue(session.Responder.SuppressLookInput && session.Responder.SuppressInteractionInput);
                    Assert.IsFalse(session.StepRadioInput(false, false, Vector2.zero), "Releasing in the dead zone cancels.");
                    Assert.IsFalse(session.Wheel.Open || session.Acting);
                    session.StepRadioInput(true, true, Vector2.zero);
                    session.Responder.Pause();
                    Assert.IsFalse(session.Wheel.Open || session.Responder.SuppressLookInput || session.Responder.SuppressInteractionInput);
                    session.Resume(); Assert.AreEqual(stage, session.StepIndex);
                    if (guidance == GuidanceLevel.Standard)
                    {
                        session.StepRadioInput(true, true, Vector2.zero);
                        var wrong = session.Current.Action == CrewInspectionAction.HoldDeparture ? Vector2.up : Vector2.down;
                        session.StepRadioInput(false, true, wrong * 80, true);
                        Assert.That(session.RadioGroup, Is.GreaterThanOrEqualTo(0));
                        session.StepRadioInput(false, true, Vector2.zero, false, true);
                        Assert.AreEqual(-1, session.RadioGroup, "Right click returns to the group list.");
                        session.StepRadioInput(false, true, wrong * 80, true);
                        session.StepRadioInput(false, true, Vector2.up * 80);
                        Assert.IsFalse(session.StepRadioInput(false, false, Vector2.zero), "The wrong report cannot skip this lesson.");
                        Assert.IsFalse(session.Acting); Assert.AreEqual(stage, session.StepIndex);
                    }
                }
            }
            finally { GameSettings.Guidance = previousGuidance; }
            session.StepRadioInput(true, true, Vector2.zero);
            if (session.RadioGrouped)
            {
                var group = session.Current.Action == CrewInspectionAction.HoldDeparture ? Vector2.down : Vector2.up;
                session.StepRadioInput(false, true, group * 80, true);
            }
            session.StepRadioInput(false, true, Vector2.up * 80);
            Shot(session.Responder.PlayerCamera, "radio-select-" + stage);
            Assert.IsFalse(session.Acting);
            bool sent = session.StepRadioInput(false, false, Vector2.zero);
            Assert.IsFalse(session.Wheel.Open || session.Responder.SuppressLookInput || session.Responder.SuppressInteractionInput);
            return sent;
        }

        private IEnumerator WalkTo(Vector3 destination)
        {
            var player = session.Responder;
            for (int frame = 0; frame < 240; frame++)
            {
                var delta = destination - player.transform.position; delta.y = 0;
                if (delta.magnitude < .12f) yield break;
                var local = player.transform.InverseTransformDirection(delta.normalized);
                player.StepInput(new Vector2(local.x, local.z), Vector2.zero, false, false, false, .025f);
                if (frame % 20 == 0) yield return null;
            }
            Assert.Fail("Cabin route blocked: " + player.transform.position + " → " + destination + ", " + player.LastCollisionFlags);
        }
        private void Aim(CrewInspectionPoint point)
        {
            var renderers = point.GetComponentsInChildren<Renderer>();
            Vector3 target = renderers.Length > 0 ? renderers[0].bounds.center : point.GetComponent<Collider>().bounds.center;
            var player = session.Responder;
            var rotation = Quaternion.LookRotation(target - player.PlayerCamera.transform.position, Vector3.up).eulerAngles;
            player.RestorePhysicalPose(player.transform.position, rotation.y, Mathf.DeltaAngle(0, rotation.x));
        }
        private void Aim(Transform attention)
        {
            var player=session.Responder;
            var rotation=Quaternion.LookRotation(attention.position-player.PlayerCamera.transform.position,Vector3.up).eulerAngles;
            player.RestorePhysicalPose(player.transform.position,rotation.y,Mathf.DeltaAngle(0,rotation.x));
        }
        private string DescribeRay(CrewInspectionPoint point)
        {
            var c = session.Responder.PlayerCamera;
            var hits = Physics.RaycastAll(c.transform.position, c.transform.forward, 3, ~0, QueryTriggerInteraction.Ignore)
                .OrderBy(h => h.distance).Select(h => h.collider.name + "@" + h.distance.ToString("F3") + " " + h.point.ToString("F3") + " normal=" + h.normal.ToString("F3"));
            return "Target " + point.Id + " from " + c.transform.position + " · hits " + string.Join(", ", hits);
        }
        private static string ReviewFolder => Environment.GetEnvironmentVariable("CG_TUTORIAL_REVIEW");
        private static void Write(string file, string text)
        {
            if (!string.IsNullOrWhiteSpace(ReviewFolder)) File.WriteAllText(Path.Combine(ReviewFolder, file), text);
        }
        // Optional review screenshots use the game's camera and UI. No OS input or desktop capture.
        private static void Shot(Camera camera, string name, int width = 1440, int height = 900)
        {
            if (string.IsNullOrWhiteSpace(ReviewFolder) || camera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var target = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray();
            var previous = camera.targetTexture; var active = RenderTexture.active; float previousAspect = camera.aspect;
            try
            {
                camera.targetTexture = target;
                camera.aspect = width / (float)height;
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f; }
                Canvas.ForceUpdateCanvases();
                foreach (var view in Object.FindObjectsByType<CrewInspectionView>(FindObjectsSortMode.None)) view.SendMessage("LateUpdate");
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                if (name == "intercom-panel-recognized")
                {
                    var panel = Object.FindFirstObjectByType<CrewInspectionSession>().Points.Single(p => p.Id == "intercom");
                    var collider = panel.GetComponent<Collider>(); bool wasEnabled = collider.enabled;
                    collider.enabled = false; Physics.SyncTransforms();
                    var rays = new System.Text.StringBuilder();
                    rays.AppendLine("camera=" + camera.transform.position.ToString("F4") + " rotation=" + camera.transform.rotation.eulerAngles.ToString("F4") + " aspect=" + camera.aspect + " marker=" + panel.Marker.position.ToString("F4"));
                    for (float x = .2f; x < .801f; x += .005f)
                        for (float y = .25f; y < .751f; y += .005f)
                            if (Physics.Raycast(camera.ViewportPointToRay(new Vector3(x, y, 0)), out var hit, 5, ~0, QueryTriggerInteraction.Ignore))
                                rays.AppendLine(x.ToString("F3") + "," + y.ToString("F3") + " " + hit.point.ToString("F4") + " normal=" + hit.normal.ToString("F3") + " " + hit.collider.name);
                    collider.enabled = wasEnabled; Physics.SyncTransforms();
                    Write("intercom-runtime-rays.txt", rays.ToString());
                }
                var tutorial = Object.FindFirstObjectByType<CrewInspectionSession>();
                var pin = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "인터폰 실제 위치" && t.gameObject.activeInHierarchy);
                if (pin != null && tutorial != null)
                {
                    var native = tutorial.Points.Single(p => p.Id == "intercom").Marker.position;
                    var expected = camera.WorldToViewportPoint(native);
                    var actual = camera.WorldToScreenPoint(pin.rectTransform.position);
                    Assert.That(Vector2.Distance(new Vector2(expected.x * width, expected.y * height), actual), Is.LessThan(3), "The actual marker pin must project onto the native panel, without a label offset.");
                }
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(ReviewFolder, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; }
                camera.targetTexture = previous; camera.aspect = previousAspect; RenderTexture.active = active;
                Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target);
            }
        }
    }
}
