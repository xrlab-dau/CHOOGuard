using System.Collections;
using System.Collections.Generic;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// Meshy equipment hooks: the escalator stop button built from a supplied plate model (its "버튼" child goes in when pressed and
    /// the belt stops), and the station extinguisher marker dressed with the supplied body model so it can be picked up.
    /// </summary>
    public sealed class MeshyExtinguisherStopTests
    {
        private readonly List<Object> made = new List<Object>();
        private EmergencyArt art;

        private GameObject Model(string name, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = size;
            go.transform.position = new Vector3(0, -100, 0);   // a stand-in "prefab", parked out of the way
            made.Add(go);
            return go;
        }

        private static TextAsset Records(string stops) => new TextAsset("{\"version\":1,\"note\":\"test\",\"stops\":[" + stops + "]}");

        [SetUp]
        public void Setup()
        {
            art = ScriptableObject.CreateInstance<EmergencyArt>();
            made.Add(art);
            art.ExtinguisherModel = Model("extinguisher", new Vector3(.16f, .48f, .16f));
            // Plate model: 12 x 12 x 2 cm plate with the red knob as a child named "버튼" standing 1.5 cm proud of its face.
            var plate = new GameObject("stop-plate");
            plate.transform.position = new Vector3(0, -100, 0);
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "판"; face.transform.SetParent(plate.transform, false); face.transform.localScale = new Vector3(.12f, .12f, .02f); face.transform.localPosition = new Vector3(0, 0, .01f);
            var knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = EscalatorStopButton.KnobName; knob.transform.SetParent(plate.transform, false);
            knob.transform.localRotation = Quaternion.Euler(90, 0, 0); knob.transform.localScale = new Vector3(.05f, .0075f, .05f); knob.transform.localPosition = new Vector3(0, 0, .0275f);
            made.Add(plate);
            art.EscalatorStopModel = plate;
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var o in made) if (o != null) Object.Destroy(o);
            made.Clear();
        }

        private Escalator EscalatorWithOneButton()
        {
            var root = new GameObject("시험 에스컬레이터");
            made.Add(root);
            var escalator = root.AddComponent<Escalator>();
            // Down escalator from (0,7,0) to (0,0,12); the record puts one button at the bottom landing, rider-right (+x).
            escalator.Setup(new StationPoints.EscalatorEntry { id = "esc-test", label = "시험", path = new[] { new Vector3(1.4f, 7, -1), new Vector3(0, 7, 0), new Vector3(0, 0, 12), new Vector3(-1.2f, 0, 13) } });
            escalator.AddStopButtons(EscalatorStopSurvey.Load(Records(
                "{\"escalator\":\"esc-test\",\"landing\":\"bottom\",\"side\":\"right\",\"mount\":\"newel\",\"height\":0.3,\"along\":0.3,\"offset\":0.8,\"placementEvidence\":\"test fixture only\"}")), art.EscalatorStopModel);
            return escalator;
        }

        [UnityTest]
        public IEnumerator TheStopButtonUsesThePlateModelAndPressingItStopsTheBeltAndPushesTheKnobIn()
        {
            var escalator = EscalatorWithOneButton();
            Assert.That(escalator.StopButtons, Is.EqualTo(1));
            var button = escalator.GetComponentInChildren<EscalatorStopButton>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.name, Is.EqualTo("비상정지 버튼 · 시험"));
            Assert.That(button.transform.Find("판"), Is.Not.Null, "the supplied plate model is the button, not the primitive plate");
            var knob = button.transform.Find(EscalatorStopButton.KnobName);
            Assert.That(knob, Is.Not.Null);
            Assert.That(knob.GetComponentsInChildren<Collider>(), Is.Empty, "only the plate is aimed at");
            Assert.That(button.GetComponent<Collider>(), Is.Not.Null);
            // Bottom landing of the belt end (0,0,12): outward +z 0.3, rider-right +x 0.8, up 0.3; the plate faces out onto the landing.
            Assert.That((button.transform.position - new Vector3(.8f, .3f, 12.3f)).magnitude, Is.LessThan(1e-3f));
            Assert.That(Vector3.Dot(button.transform.forward, Vector3.forward), Is.GreaterThan(.99f));
            float rest = knob.localPosition.z;

            Assert.That(escalator.Running, Is.True);
            Assert.That(escalator.DriveSpeed, Is.EqualTo(Escalator.BeltSpeed));
            Assert.That(button.TryInteract(null, out var feedback), Is.True);
            Assert.That(feedback, Does.Contain("비상정지"));
            Assert.That(escalator.Running, Is.False, "pressing the button stops the belt");
            Assert.That(escalator.DriveSpeed, Is.EqualTo(0f), "the step drive follows the stopped belt");
            Assert.That(escalator.StoppedBy, Does.Contain("비상정지 버튼"));
            Assert.That(button.StateText, Does.Contain("눌림"));

            float until = Time.time + 2f;
            while (Time.time < until && rest - knob.localPosition.z < EscalatorStopButton.ModelKnobTravel - 1e-4f) yield return null;
            Assert.That(rest - knob.localPosition.z, Is.EqualTo(EscalatorStopButton.ModelKnobTravel).Within(1e-4f), "the model's knob goes in by its travel");

            Assert.That(button.CanInteract(null, out var reason), Is.False);
            Assert.That(reason, Does.Contain("정지됨"));
            Assert.That(button.TrySecondary(null, out var restart), Is.True, "nobody on the steps: the key switch restarts it");
            Assert.That(escalator.Running, Is.True);
            until = Time.time + 2f;
            while (Time.time < until && rest - knob.localPosition.z > 1e-4f) yield return null;
            Assert.That(knob.localPosition.z, Is.EqualTo(rest).Within(1e-4f), "the knob comes back out after the restart");
        }

        [Test]
        public void WithoutAPlateModelTheStopButtonIsStillBuiltFromPrimitives()
        {
            art.EscalatorStopModel = null;
            var escalator = EscalatorWithOneButton();
            var button = escalator.GetComponentInChildren<EscalatorStopButton>();
            Assert.That(button, Is.Not.Null);
            Assert.That(button.transform.Find(EscalatorStopButton.KnobName), Is.Not.Null);
            Assert.That(button.TryInteract(null, out _), Is.True);
            Assert.That(escalator.Running, Is.False);
        }

        [Test]
        public void ABareExtinguisherMarkerGetsTheBodyModelAndAColliderOnItsBounds()
        {
            var marker = new GameObject("ext-test-01");
            made.Add(marker);
            marker.transform.position = new Vector3(3, 7, -5);

            var body = Extinguisher.Dress(marker, art);

            Assert.That(body, Is.Not.Null);
            Assert.That(body.name, Is.EqualTo(Extinguisher.ModelChildName));
            Assert.That(body.transform.parent, Is.EqualTo(marker.transform));
            Assert.That(body.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(body.GetComponentsInChildren<Collider>(), Is.Empty, "the marker's collider is the aim target");
            var box = marker.GetComponent<BoxCollider>();
            Assert.That(box, Is.Not.Null);
            Assert.That(box.size.y, Is.EqualTo(.48f).Within(.01f));
            Assert.That(box.size.x, Is.EqualTo(.16f).Within(.01f));
            Assert.That(Extinguisher.Dress(marker, art), Is.SameAs(body), "dressing twice keeps the one body");
        }

        [Test]
        public void AMarkerThatAlreadyShowsSomethingOrHasNoModelIsLeftAlone()
        {
            var sourced = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            made.Add(sourced);
            Assert.That(Extinguisher.Dress(sourced, art), Is.Null);
            Assert.That(sourced.transform.childCount, Is.EqualTo(0));

            var bare = new GameObject("ext-test-02");
            made.Add(bare);
            art.ExtinguisherModel = null;
            Assert.That(Extinguisher.Dress(bare, art), Is.Null);
            Assert.That(bare.GetComponent<Collider>(), Is.Null);
        }
    }
}
