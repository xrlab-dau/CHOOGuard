using System.Collections;
using System.Collections.Generic;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>Meshy prop hooks: the parcel variant of the unattended item and the AED cabinet/unit models, each with a procedural fallback.</summary>
    public sealed class MeshyPropsTests
    {
        private readonly List<Object> made = new List<Object>();
        private EmergencyArt art;
        private GameObject root;

        private GameObject Model(string name, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = size;
            go.transform.position = new Vector3(0, -100, 0);   // a stand-in "prefab", parked out of the way; copies keep its mesh and scale
            made.Add(go);
            return go;
        }

        [SetUp]
        public void Setup()
        {
            art = ScriptableObject.CreateInstance<EmergencyArt>();
            made.Add(art);
            art.SuitcaseModel = Model("suitcase", new Vector3(.36f, .6f, .24f));
            art.ParcelModel = Model("parcel", new Vector3(.48f, .22f, .40f));
            art.AedCabinetModel = Model("cabinet", new Vector3(.31f, .42f, .20f));
            art.AedUnitModel = Model("unit", new Vector3(.31f, .24f, .27f));
            root = new GameObject("rig");
            made.Add(root);
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var o in made) if (o != null) Object.Destroy(o);
            made.Clear();
        }

        [Test]
        public void TheParcelPropUsesTheMeshyModelWithABoxColliderOnItsBounds()
        {
            var parcel = Props.Parcel(root.transform, new Vector3(1, 0, 2), Quaternion.identity, art);
            Assert.That(parcel.name, Is.EqualTo("소포"));
            var box = parcel.GetComponent<BoxCollider>();
            Assert.That(box, Is.Not.Null);
            // Collider size is in the prop's own space; the stand-in model carries its size as a scale, the real prefab as mesh units.
            Assert.That(box.size.x * parcel.transform.lossyScale.x, Is.EqualTo(.48f).Within(.01f));
            Assert.That(box.size.y * parcel.transform.lossyScale.y, Is.EqualTo(.22f).Within(.01f));
        }

        [Test]
        public void WithoutAParcelModelTheParcelPropFallsBackToTheSuitcase()
        {
            art.ParcelModel = null;
            var prop = Props.Parcel(root.transform, Vector3.zero, Quaternion.identity, art);
            Assert.That(prop.name, Is.EqualTo("여행가방"));
        }

        [Test]
        public void EveryThirdUnattendedItemIsASealedParcelWithItsOwnWords()
        {
            var parcel = new SuspiciousItemHazard("bag-3", Vector3.zero, Quaternion.identity, null, art, root.transform, parcel: true) { Where = "2층 맞이방" };
            Assert.That(parcel.IsParcel, Is.True);
            Assert.That(parcel.Visible, Does.Contain("소포"));
            Assert.That(parcel.ReportLine, Does.Contain("주인 없는 소포"));
            Assert.That(parcel.View.name, Is.EqualTo("소포"));
            Assert.That(parcel.View.GetComponent<HazardMarker>().Name, Is.EqualTo("주인 없는 소포"));

            var bag = new SuspiciousItemHazard("bag-1", Vector3.zero, Quaternion.identity, null, art, root.transform) { Where = "2층 맞이방" };
            Assert.That(bag.IsParcel, Is.False);
            Assert.That(bag.ReportLine, Does.Contain("여행가방"));

            art.ParcelModel = null;
            var noModel = new SuspiciousItemHazard("bag-6", Vector3.zero, Quaternion.identity, null, art, root.transform, parcel: true) { Where = "2층 맞이방" };
            Assert.That(noModel.IsParcel, Is.False, "without a parcel model the item stays a suitcase, words included");
        }

        [Test]
        public void TheAedCabinetAndUnitUseTheMeshyModelsAndKeepTheirInteractions()
        {
            var marker = new GameObject("aed fixture");
            marker.transform.SetParent(root.transform, false);
            marker.AddComponent<BoxCollider>();
            var cabinet = AedCabinet.Build(marker, null, art);
            Assert.That(cabinet.transform.Find("AED 보관함 (모델)"), Is.Not.Null);
            Assert.That(cabinet.Unit, Is.Not.Null);
            Assert.That(cabinet.Unit.transform.Find("본체 (모델)"), Is.Not.Null);
            var box = cabinet.Unit.GetComponent<BoxCollider>();
            Assert.That(box, Is.Not.Null);
            Assert.That(box.size.y, Is.EqualTo(.24f).Within(.01f));
            Assert.That(cabinet.Stored, Is.True);
            Assert.That(cabinet.Unit.transform.parent, Is.EqualTo(cabinet.transform));
            // The 27 cm deep case stands upright, scaled down, in the lower part of the cabinet: fully behind the glass and below the heart.
            Assert.That(cabinet.StoredScale, Is.LessThan(1f).And.GreaterThan(.25f));
            Assert.That(Quaternion.Angle(cabinet.Unit.transform.localRotation, Quaternion.Euler(90, 0, 0)), Is.LessThan(.5f), "top of the case faces the glass");
            Assert.That(cabinet.Unit.transform.localScale.x, Is.EqualTo(cabinet.StoredScale).Within(1e-4f));
            var cabinetBounds = Props.LocalBounds(cabinet.transform.Find("AED 보관함 (모델)"));
            var unitWorld = cabinet.transform.worldToLocalMatrix;
            foreach (var filter in cabinet.Unit.GetComponentsInChildren<MeshFilter>())
            {
                var b = filter.sharedMesh.bounds; var toCabinet = cabinet.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = toCabinet.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    Assert.That(cabinetBounds.Contains(corner), Is.True, "stored AED must sit inside the cabinet: " + corner);
                    Assert.That(corner.y, Is.LessThan(cabinetBounds.center.y), "stored AED must stay below the heart at the cabinet's centre: " + corner);
                }
            }
            Assert.That(cabinet.InteractionPrompt, Is.EqualTo("AED 꺼내기"));
            Assert.That(cabinet.Unit.DisplayName, Is.EqualTo("자동심장충격기(AED)"));
        }

        [Test]
        public void WithoutModelsTheAedStaysProcedural()
        {
            art.AedCabinetModel = null; art.AedUnitModel = null;
            var marker = new GameObject("aed fixture plain");
            marker.transform.SetParent(root.transform, false);
            var cabinet = AedCabinet.Build(marker, null, art);
            Assert.That(cabinet.transform.Find("AED 보관함 (모델)"), Is.Null);
            Assert.That(cabinet.Unit.transform.Find("본체 (절차)/본체"), Is.Not.Null, "procedural yellow case");
            Assert.That(cabinet.Unit.transform.Find(AedUnit.LidName), Is.Not.Null, "procedural hinged lid");
            Assert.That(cabinet.Unit.PadSize, Is.EqualTo(AedUnit.DefaultPadSize));
        }

        [UnityTest]
        public IEnumerator TheOpenedAedShowsTheOpenModelAndUsesThePadAndButtonModels()
        {
            art.AedOpenModel = Model("open", new Vector3(.31f, .40f, .27f));
            art.AedPadModel = Model("pad", new Vector3(.10f, .01f, .12f));
            art.AedShockButtonModel = Model("button", new Vector3(.04f, .015f, .04f));
            var marker = new GameObject("aed fixture models");
            marker.transform.SetParent(root.transform, false);
            marker.AddComponent<BoxCollider>();
            var unit = AedCabinet.Build(marker, null, art).Unit;
            var closed = unit.transform.Find("본체 (모델)");
            var open = unit.transform.Find("본체 열림 (모델)");
            Assert.That(open, Is.Not.Null);
            Assert.That(unit.transform.Find(AedUnit.LidName), Is.Null, "with an open model there is no procedural lid");
            Assert.That(closed.gameObject.activeSelf, Is.True);
            Assert.That(open.gameObject.activeSelf, Is.False);
            // 패드: 모델 크기가 패드 크기이고, 충돌체가 그 크기로 뿌리에 붙는다. 버튼: 모델이 램프 자리에 선다.
            Assert.That(unit.PadSize.x, Is.EqualTo(.10f).Within(.005f));
            Assert.That(unit.PadSize.z, Is.EqualTo(.12f).Within(.005f));
            Assert.That(unit.Pads[1].GetComponent<BoxCollider>().size.x, Is.EqualTo(unit.PadSize.x).Within(1e-4f));
            Assert.That(unit.Pads[1].transform.Find("패드 (모델)"), Is.Not.Null);
            var button = unit.transform.Find(AedUnit.LampName);
            Assert.That(button, Is.Not.Null);
            Assert.That(button.GetComponent<MeshRenderer>(), Is.Not.Null, "the stand-in button model is the lamp");

            Assert.That(unit.Procedure.OpenLid(), Is.True);
            yield return null;
            Assert.That(open.gameObject.activeSelf, Is.True, "switched on, the open case shows");
            Assert.That(closed.gameObject.activeSelf, Is.False);
            unit.Procedure.PowerOff();
            yield return null;
            Assert.That(open.gameObject.activeSelf, Is.False);
            Assert.That(closed.gameObject.activeSelf, Is.True);
        }
    }
}
