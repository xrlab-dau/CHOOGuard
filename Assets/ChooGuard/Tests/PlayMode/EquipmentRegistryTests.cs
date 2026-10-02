using System.Collections;
using System.Collections.Generic;
using ChooGuard.App.Fps.Equipment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // 설비 배치의 소비자 관점 규칙: 레지스트리 조회, 배치 파일의 잘못된 항목, 먼 설비의 그리기 끄기.
    public sealed class EquipmentRegistryTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in made) if (item != null) Object.DestroyImmediate(item);
            made.Clear();
            EquipmentRegistry.Clear();
        }

        private StationEquipment Make(string id, string kind, Vector3 at)
        {
            var go = new GameObject(id);
            made.Add(go);
            go.transform.position = at;
            var equipment = go.AddComponent<StationEquipment>();
            equipment.Assign(id, kind, id, "hall2f");
            return equipment;
        }

        [Test]
        public void NearestIsTheClosestOfItsKindWithinReachThatPassesTheFilter()
        {
            var near = Make("smoke-near", "smoke_detector", new Vector3(3, 0, 0));
            var far = Make("smoke-far", "smoke_detector", new Vector3(20, 0, 0));
            Make("heat-nearest", "heat_detector", new Vector3(1, 0, 0));

            Assert.That(EquipmentRegistry.Nearest("smoke_detector", Vector3.zero), Is.SameAs(near));
            Assert.That(EquipmentRegistry.Nearest("smoke_detector", Vector3.zero, 2f), Is.Null, "범위 밖은 없는 것이다");
            Assert.That(EquipmentRegistry.Nearest("smoke_detector", Vector3.zero, 50f, e => e != near), Is.SameAs(far));
        }

        [Test]
        public void AssignFilesTheObjectAgainUnderItsNewKindAndId()
        {
            var equipment = Make("placeholder", "smoke_detector", Vector3.zero);

            equipment.Assign("bell-1", "alarm_bell", "경종", "hall2f");

            Assert.That(EquipmentRegistry.OfKind("smoke_detector"), Is.Empty);
            Assert.That(EquipmentRegistry.OfKind("alarm_bell"), Is.EquivalentTo(new[] { equipment }));
            Assert.That(EquipmentRegistry.Find("bell-1"), Is.SameAs(equipment));
            Assert.That(EquipmentRegistry.Find("placeholder"), Is.Null);
        }

        [Test]
        public void EquipmentThatIsSwitchedOffIsNotSomethingToPickAnymore()
        {
            var equipment = Make("shutter-1", "fire_shutter", Vector3.zero);

            equipment.gameObject.SetActive(false);

            Assert.That(EquipmentRegistry.Nearest("fire_shutter", Vector3.zero), Is.Null);
            equipment.gameObject.SetActive(true);
            Assert.That(EquipmentRegistry.Nearest("fire_shutter", Vector3.zero), Is.SameAs(equipment));
        }

        private EquipmentCatalog Catalog(GameObject prefab, string json)
        {
            var catalog = ScriptableObject.CreateInstance<EquipmentCatalog>();
            made.Add(catalog);
            catalog.Prefabs = new[] { prefab };
            catalog.Placements = new[] { new TextAsset(json) };
            return catalog;
        }

        private GameObject Prefab(string name, float drawDistance)
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            made.Add(prefab);
            prefab.name = name;
            Object.DestroyImmediate(prefab.GetComponent<Collider>());
            prefab.transform.localScale = Vector3.one * .1f;
            var equipment = prefab.AddComponent<StationEquipment>();
            equipment.DrawDistance = drawDistance;
            equipment.Batchable = false;
            // 프리팹 원본 역할이라 레지스트리에는 없어야 한다(실제 프리팹 자산은 씬에 켜져 있지 않다).
            EquipmentRegistry.Unregister(equipment);
            return prefab;
        }

        [Test]
        public void SpawnPlacesEveryValidEntryAndSkipsUnknownPrefabsAndRepeatedIds()
        {
            var prefab = Prefab("SmokeDetector", 40);
            var json = "{\"group\":\"test\",\"items\":[" +
                       "{\"id\":\"a\",\"kind\":\"smoke_detector\",\"label\":\"A\",\"zone\":\"hall2f\",\"prefab\":\"SmokeDetector\",\"placementEvidence\":\"test fixture only: A at (5,10,5)\",\"position\":{\"x\":5,\"y\":10,\"z\":5},\"rotation\":{\"x\":0,\"y\":0,\"z\":0}}," +
                       "{\"id\":\"b\",\"kind\":\"smoke_detector\",\"label\":\"B\",\"zone\":\"hall2f\",\"prefab\":\"NoSuchPrefab\",\"placementEvidence\":\"test fixture only: B at (6,10,5)\",\"position\":{\"x\":6,\"y\":10,\"z\":5},\"rotation\":{\"x\":0,\"y\":0,\"z\":0}}," +
                       "{\"id\":\"a\",\"kind\":\"smoke_detector\",\"label\":\"A again\",\"zone\":\"hall2f\",\"prefab\":\"SmokeDetector\",\"placementEvidence\":\"test fixture only: duplicate A at (7,10,5)\",\"position\":{\"x\":7,\"y\":10,\"z\":5},\"rotation\":{\"x\":0,\"y\":0,\"z\":0}}]}";
            var parent = new GameObject("설비 시험");
            made.Add(parent);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("'b'.*NoSuchPrefab"));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("'a'.*겹침"));

            var report = EquipmentSpawner.Spawn(Catalog(prefab, json), parent.transform);

            Assert.That(report.Placed, Is.EqualTo(1));
            Assert.That(report.Skipped, Is.EqualTo(2));
            Assert.That(report.Unverified, Is.Zero);
            var placed = EquipmentRegistry.Find("a");
            Assert.That(placed, Is.Not.Null);
            Assert.That(placed.Kind, Is.EqualTo("smoke_detector"));
            Assert.That(placed.Zone, Is.EqualTo("hall2f"));
            Assert.That(placed.transform.position, Is.EqualTo(new Vector3(5, 10, 5)));
        }

        [Test]
        public void SpawnWithholdsMissingEmptyAndWhitespaceEvidenceBeforeReservingIdsOrCreatingObjects()
        {
            var prefab = Prefab("SmokeDetector", 40);
            prefab.AddComponent<BoxCollider>();
            prefab.GetComponent<StationEquipment>().Interactable = true;
            var json = "{\"group\":\"test\",\"items\":[" +
                       "{\"id\":\"shared\",\"kind\":\"unverified_fixture\",\"prefab\":\"NoSuchPrefab\",\"position\":{\"x\":100,\"y\":100,\"z\":100}}," +
                       "{\"id\":\"shared\",\"kind\":\"unverified_fixture\",\"prefab\":\"SmokeDetector\",\"placementEvidence\":\"\",\"position\":{\"x\":200,\"y\":200,\"z\":200}}," +
                       "{\"id\":\"shared\",\"kind\":\"unverified_fixture\",\"prefab\":\"SmokeDetector\",\"placementEvidence\":\" \\t\\r\\n\",\"position\":{\"x\":300,\"y\":300,\"z\":300}}," +
                       "{\"id\":\"shared\",\"kind\":\"smoke_detector\",\"label\":\"Observed fixture\",\"zone\":\"hall2f\",\"prefab\":\"SmokeDetector\",\"placementEvidence\":\"test fixture only: observed detector at (5.125,10.375,5.625), yaw 37.5\",\"position\":{\"x\":5.125,\"y\":10.375,\"z\":5.625},\"rotation\":{\"x\":0,\"y\":37.5,\"z\":0}}]}";
            var parent = new GameObject("설비 근거 시험");
            made.Add(parent);

            var report = EquipmentSpawner.Spawn(Catalog(prefab, json), parent.transform);

            Assert.That(report.Unverified, Is.EqualTo(3));
            Assert.That(report.Skipped, Is.Zero, "근거가 없는 항목은 프리팹 오류나 중복 id 오류로 처리하지 않는다");
            Assert.That(report.Placed, Is.EqualTo(1), "근거 없는 항목이 같은 id의 확인된 배치를 막지 않는다");
            Assert.That(report.Cells, Is.EqualTo(1), "근거 없는 좌표에는 격자를 만들지 않는다");
            Assert.That(report.Root.GetComponentsInChildren<StationEquipment>(true).Length, Is.EqualTo(1));
            Assert.That(report.Root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1), "확인된 설비만 콜라이더를 가진다");
            var placed = EquipmentRegistry.Find("shared");
            Assert.That(placed, Is.Not.Null);
            Assert.That(EquipmentRegistry.All, Is.EquivalentTo(new[] { placed }));
            Assert.That(EquipmentRegistry.OfKind("unverified_fixture"), Is.Empty);
            Assert.That(placed.transform.position, Is.EqualTo(new Vector3(5.125f, 10.375f, 5.625f)));
            Assert.That(Quaternion.Angle(placed.transform.rotation, Quaternion.Euler(0, 37.5f, 0)), Is.LessThan(.001f));
            Assert.That(report.ToString(), Does.Contain("unverified=3"));
        }

        [UnityTest]
        public IEnumerator EquipmentBeyondItsDrawDistanceIsNotDrawnAndComesBackWhenTheCameraIsNear()
        {
            var prefab = Prefab("SmokeDetector", 10);
            var json = "{\"group\":\"test\",\"items\":[{\"id\":\"far\",\"kind\":\"smoke_detector\",\"label\":\"F\",\"zone\":\"hall2f\",\"prefab\":\"SmokeDetector\",\"placementEvidence\":\"test fixture only: F at (0,10,0)\",\"position\":{\"x\":0,\"y\":10,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0}}]}";
            var cameraObject = new GameObject("시험 카메라", typeof(Camera));
            made.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 10, 40);
            var parent = new GameObject("설비 시험");
            made.Add(parent);

            EquipmentSpawner.Spawn(Catalog(prefab, json), parent.transform);
            var renderer = EquipmentRegistry.Find("far").GetComponentInChildren<Renderer>();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(renderer.enabled, Is.False, "40 m 밖의 감지기는 그리지 않는다");

            camera.transform.position = new Vector3(0, 10, 5);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(renderer.enabled, Is.True, "5 m 안으로 오면 다시 그린다");
        }
    }
}
