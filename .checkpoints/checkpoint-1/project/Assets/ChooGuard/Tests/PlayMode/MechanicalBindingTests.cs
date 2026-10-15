using System.Collections;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.Circulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class MechanicalBindingTests
    {
        private GameObject root;

        [SetUp] public void SetUp() => root = new GameObject("기계 바인딩 시험");
        [TearDown] public void TearDown() { if (root != null) Object.DestroyImmediate(root); }

        private GameObject Part(string name, Vector3 local)
        {
            var part = new GameObject(name);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = local;
            return part;
        }

        private Escalator Lane()
        {
            var lane = Part("시험 운행 상태", Vector3.zero).AddComponent<Escalator>();
            lane.Setup(new StationPoints.EscalatorEntry { id = "binding-test", label = "시험 에스컬레이터",
                path = new[] { Vector3.zero, Vector3.forward, new Vector3(0, 3, 6), new Vector3(0, 3, 7) } });
            lane.enabled = false; // Time is advanced explicitly below; no scene or geometry is authored by these tests.
            return lane;
        }

        private EscalatorRide Ride(string name, EscalatorRide.RideDirection direction = EscalatorRide.RideDirection.Up)
        {
            var go = Part(name, Vector3.zero);
            go.AddComponent<BoxCollider>().isTrigger = true;
            var ride = go.AddComponent<EscalatorRide>();
            ride.Direction = direction;
            return ride;
        }

        private ElevatorCar Car()
        {
            var car = Part("시험 승강기 상태", Vector3.zero).AddComponent<ElevatorCar>();
            car.enabled = false;
            car.Landings.Add(new ElevatorCar.Landing { Floor = "1", Y = 0 });
            car.Landings.Add(new ElevatorCar.Landing { Floor = "2", Y = 4 });
            return car;
        }

        private static void Near(Vector3 actual, Vector3 expected) => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-5f));

        [Test]
        public void ExistingStepShaderUsesTheSameStoppedAndResumedDriveDistance()
        {
            var lane = Lane();
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform, false);
            var material = new Material(Shader.Find("CHOOGuard/Escalator/Step"));
            try
            {
                var renderer = cube.GetComponent<Renderer>(); renderer.sharedMaterial = material;
                var motion = root.AddComponent<EscalatorMotion>();
                motion.StepSurfaces = new[] { new EscalatorMotion.StepSurface { Renderer = renderer, MaterialIndex = 0 } };
                lane.BindDrive(root.transform);
                var block = new MaterialPropertyBlock();
                lane.AdvanceDrive(2f); motion.ApplyMotion(); renderer.GetPropertyBlock(block, 0);
                Assert.That(block.GetFloat("_DriveBound"), Is.EqualTo(1));
                Assert.That(block.GetFloat("_TravelMetres"), Is.EqualTo(1));
                lane.Stop(); lane.AdvanceDrive(5f); motion.ApplyMotion(); renderer.GetPropertyBlock(block, 0);
                Assert.That(block.GetFloat("_TravelMetres"), Is.EqualTo(1));
                Assert.That(lane.Restart(out _), Is.True);
                lane.AdvanceDrive(1f); motion.ApplyMotion(); renderer.GetPropertyBlock(block, 0);
                Assert.That(block.GetFloat("_TravelMetres"), Is.EqualTo(1.5f));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void OneDrive_FreezesStepsHandrailsAndRideVolumes_AndResumesWithoutPhaseJump()
        {
            var lane = Lane();
            var steps = Part("분리된 발판", Vector3.zero).transform;
            var rail = Part("분리된 손잡이", Vector3.right * .25f).transform;
            var motion = root.AddComponent<EscalatorMotion>();
            var path = new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.down, Vector3.down };
            motion.Loops = new[] {
                new EscalatorMotion.Loop { Parts = new[] { steps }, Path = path },
                new EscalatorMotion.Loop { Parts = new[] { rail }, Path = path }
            };
            var up = Ride("상부 이송");
            var down = Ride("하부 이송", EscalatorRide.RideDirection.Down);
            lane.BindDrive(root.transform);
            Assert.That(motion.AnimatedPartCount, Is.EqualTo(2));
            Near(up.WorldVelocity, Vector3.forward * Escalator.BeltSpeed);
            Near(down.WorldVelocity, Vector3.back * Escalator.BeltSpeed);

            lane.AdvanceDrive(1f); motion.ApplyMotion();
            Near(steps.position, Vector3.right * .5f);
            Near(rail.position, Vector3.right * .75f);
            lane.Stop();
            lane.AdvanceDrive(20f); motion.ApplyMotion();
            Near(up.WorldVelocity, Vector3.zero); Near(down.WorldVelocity, Vector3.zero);
            Near(steps.position, Vector3.right * .5f); Near(rail.position, Vector3.right * .75f);
            Assert.That(lane.TravelDistance, Is.EqualTo(.5d));

            Assert.That(lane.Restart(out _), Is.True);
            lane.BindDrive(root.transform); // Rebinding the same lane must not recapture or restart a moving loop.
            motion.ApplyMotion();
            Near(steps.position, Vector3.right * .5f);
            lane.AdvanceDrive(.5f); motion.ApplyMotion();
            Near(steps.position, Vector3.right * .75f);
            Near(rail.position, Vector3.right);
        }

        [Test]
        public void MissingDriveAfterBinding_StopsTransport_InsteadOfFallingBackToLegacySpeed()
        {
            var lane = Lane();
            var ride = Ride("이송 볼륨");
            ride.Bind(lane);
            Object.DestroyImmediate(lane.gameObject);
            Near(ride.WorldVelocity, Vector3.zero);
        }

        [Test]
        public void UnprovidedPartRoutes_CreateNoParts_AndInvalidTimeDoesNotChangeDrivePhase()
        {
            var lane = Lane();
            var motion = root.AddComponent<EscalatorMotion>();
            var part = Part("경로 미확인 부품", new Vector3(3, 4, 5)).transform;
            motion.Loops = new[] { new EscalatorMotion.Loop { Parts = new[] { part }, Path = new[] { Vector3.zero } } };
            int children = root.transform.childCount;
            motion.Bind(lane);
            lane.AdvanceDrive(float.NaN); lane.AdvanceDrive(float.PositiveInfinity); lane.AdvanceDrive(-1f);
            motion.ApplyMotion();
            Assert.That(lane.TravelDistance, Is.Zero);
            Assert.That(motion.AnimatedPartCount, Is.Zero);
            Assert.That(root.transform.childCount, Is.EqualTo(children));
            Near(part.position, new Vector3(3, 4, 5));
        }

        [Test]
        public void ScaledImportedPartRoot_StillMovesTheBeltInWorldMetres()
        {
            var lane = Lane();
            root.transform.localScale = new Vector3(2, 1, 1);
            var part = Part("축척이 있는 발판", Vector3.zero).transform;
            var motion = root.AddComponent<EscalatorMotion>();
            motion.Loops = new[] { new EscalatorMotion.Loop { Parts = new[] { part },
                Path = new[] { Vector3.zero, Vector3.right, Vector3.right + Vector3.down, Vector3.down } } };
            motion.Bind(lane);
            lane.AdvanceDrive(1f); motion.ApplyMotion();
            Near(part.position, Vector3.right * .5f);
            Near(part.localPosition, Vector3.right * .25f);
        }

        [Test]
        public void ElevatorButton_PartialPushReturnsToRest_AndOnlyFullPushQueuesTheCall()
        {
            var car = Car();
            var button = Part("층 버튼", Vector3.zero).AddComponent<ElevatorButton>();
            button.Car = car; button.Kind = ElevatorButton.Kinds.Floor; button.Floor = "2";
            var part = Part("실제 누름 부품", new Vector3(.7f, 1.1f, .3f)).transform;
            var rest = part.localPosition;
            var travel = Vector3.back * .006f;
            button.BindPressPart(part, travel);
            button.BeginHold(null); button.Hold(null, Vector2.zero, 0, ElevatorButton.PressSeconds * .5f);
            button.AdvancePressVisual(.1f);
            Near(part.localPosition, rest + travel * .5f);
            Assert.That(car.Landings[1].CarCall, Is.False);
            Assert.That(button.EndHold(null), Does.Contain("끝까지"));
            button.AdvancePressVisual(.1f); Near(part.localPosition, rest);

            button.BeginHold(null); button.Hold(null, Vector2.zero, 0, ElevatorButton.PressSeconds);
            button.AdvancePressVisual(.1f);
            Assert.That(car.Landings[1].CarCall, Is.True);
            Assert.That(button.Requested, Is.True);
            Near(part.localPosition, rest + travel);
            button.Hold(null, Vector2.zero, 0, 1f); // Holding continues without submitting another control action.
            Assert.That(button.EndHold(null), Is.Null);
        }

        [Test]
        public void ElevatorControls_RejectMissingLandingAndStoppedCar_ButLeaveAlarmAvailable()
        {
            var car = Car();
            var button = Part("호출 버튼", Vector3.zero).AddComponent<ElevatorButton>();
            button.Car = car; button.Kind = ElevatorButton.Kinds.Hall; button.Landing = -1;
            Assert.That(button.TryInteract(null, out _), Is.False);
            button.Landing = 1;
            car.Stop();
            Assert.That(button.TryInteract(null, out _), Is.False);
            Assert.That(car.Landings[1].HallCall, Is.False);
            button.Kind = ElevatorButton.Kinds.Alarm;
            Assert.That(button.TryInteract(null, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator SurveyedEscalatorStopPart_PartialPushSpringsBack_AndCompletedPushStopsItsDrive()
        {
            var lane = Lane();
            var button = Part("관측된 정지 버튼", Vector3.zero).AddComponent<EscalatorStopButton>();
            var part = Part("분리된 누름 부품", new Vector3(.3f, 1.2f, .6f)).transform;
            var rest = part.localPosition;
            var travel = Vector3.left * .007f;
            button.Bind(lane, part, travel);
            button.BeginHold(null); button.Hold(null, Vector2.zero, 0, CallPointButton.PressSeconds * .5f);
            yield return new WaitForSeconds(.15f);
            Near(part.localPosition, rest + travel * .5f);
            Assert.That(lane.Running, Is.True);
            button.EndHold(null);
            yield return new WaitForSeconds(.15f);
            Near(part.localPosition, rest);
            button.BeginHold(null); button.Hold(null, Vector2.zero, 0, CallPointButton.PressSeconds);
            yield return new WaitForSeconds(.15f);
            Assert.That(lane.Running, Is.False);
            Near(part.localPosition, rest + travel);
        }
    }
}
