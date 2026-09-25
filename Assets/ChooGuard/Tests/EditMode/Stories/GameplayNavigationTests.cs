#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ChooGuard.App.Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class GameplayNavigationTests
    {
        private Scene scene;
        private GameplayNavigation navigation;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<Vector3> route = new List<Vector3>();
        private const string Revision = "synthetic-navigation-fixture-not-field-approved";

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            navigation = ObjectInScene("Navigation fixture").AddComponent<GameplayNavigation>();
            navigation.BeginGeometry(Revision);
        }

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            meshes.Clear();
        }

        [Test]
        public void ActualBake_RejectsUnsupportedHeightAndDisconnectedIsland_WithoutPartialRoute()
        {
            var first = Plane(0, 8, 0);
            var island = Plane(12, 20, 0);
            var bytes = GameplayNavigationBaker.BakeGeometry(null, new[] { first, island }, out _);
            Register("floor", bytes);
            Assert.That(navigation.TryRoute(new Vector3(2, .05f, 2), new Vector3(6, .05f, 2), route, out var reason), Is.True, reason);
            Assert.That(Vector3.Distance(route[route.Count - 1], new Vector3(6, .05f, 2)), Is.LessThan(.1f));
            Assert.That(navigation.TryRoute(new Vector3(2, .05f, 2), new Vector3(18, .05f, 2), route, out _), Is.False);
            Assert.That(route, Is.Empty);
            Assert.That(navigation.TryRoute(new Vector3(2, .05f, 2), new Vector3(6, 4, 2), route, out _), Is.False);
            Assert.That(route, Is.Empty);
        }

        [Test]
        public void Portal_RequiresContinuousBakedGeometry_PreservesHeightAndAccessibility()
        {
            var lower = Bake(Plane(0, 8, 0));
            var upper = Bake(Plane(12, 20, 3));
            Register("lower", lower); Register("upper", upper);
            var start = new Vector3(2, .05f, 2);
            var target = new Vector3(18, 3.05f, 2);
            Assert.That(navigation.TryRoute(start, target, route, out _), Is.False);
            var binding = new PortalNavigationBinding
            {
                PortalId = "stairs", FromFloorId = "lower", ToFloorId = "upper", GeometryRevision = Revision,
                Entry = new Vector3(4, .05f, 2), Exit = new Vector3(16, 3.05f, 2),
                Kind = NavigationPortalKind.Stairs, Access = NavigationAccess.All
            };
            // Endpoints alone, or an unrelated valid floor bake, are not a connection.
            Assert.That(navigation.RegisterPortal(binding, out _), Is.False);
            Assert.That(navigation.RegisterPortal(binding, lower, out _), Is.False);
            var raisedConnector = Strip(new[] { 0f, 6f, 14f, 20f }, new[] { .2f, .2f, 3.2f, 3.2f });
            Assert.That(navigation.RegisterPortal(binding, Bake(raisedConnector), out _), Is.False,
                "Separate surfaces cannot use bake precision to hide an actual vertical discontinuity.");
            var connector = Strip(new[] { 0f, 6f, 14f, 20f }, new[] { 0f, 0f, 3f, 3f });
            Assert.That(navigation.RegisterPortal(binding, Bake(connector), out var reason), Is.True, reason);
            Assert.That(navigation.TryRoute(start, target, route, out reason), Is.True, reason);
            Assert.That(route[0].y, Is.EqualTo(.05f).Within(.1f));
            Assert.That(route[route.Count - 1].y, Is.EqualTo(3.05f).Within(.1f));
            for (var i = 1; i < route.Count; i++)
            {
                var delta = route[i] - route[i - 1];
                Assert.That(Mathf.Abs(delta.y), Is.LessThanOrEqualTo(new Vector2(delta.x, delta.z).magnitude + .1f),
                    "A walking route cannot substitute a vertical teleport for the actual sloped connection.");
            }
            Assert.That(navigation.TryRoute(start, target, NavigationAccess.Walk | NavigationAccess.StepFree, route, out _), Is.False);
            var beforeClosure = navigation.RouteRevision;
            Assert.That(navigation.SetPortalAvailable("stairs", false, out _), Is.True);
            Assert.That(navigation.RouteRevision, Is.GreaterThan(beforeClosure));
            Assert.That(navigation.TryRoute(start, target, route, out _), Is.False);
            Assert.That(navigation.SetPortalAvailable("stairs", true, out _), Is.True);
            Assert.That(navigation.TryRoute(start, target, route, out reason), Is.True, reason);
        }

        [Test]
        public void OverlappingClosedDoors_RemainBlockedUntilEveryDoorOpens()
        {
            Register("floor", Bake(Plane(0, 20, 0)));
            var threshold = ObjectInScene("Actual doorway threshold").AddComponent<BoxCollider>();
            threshold.center = new Vector3(10, .85f, 2);
            threshold.size = new Vector3(.4f, 1.7f, 4);
            Physics.SyncTransforms();
            foreach (var id in new[] { "door-a", "door-b" })
                Assert.That(navigation.RegisterDoor(new DoorNavigationBinding
                {
                    DoorId = id, SurfaceId = "floor", GeometryRevision = Revision, Threshold = threshold, IsOpen = false
                }, out var bindReason), Is.True, bindReason);
            var start = new Vector3(2, .05f, 2);
            var target = new Vector3(18, .05f, 2);
            Assert.That(navigation.TryRoute(start, target, route, out _), Is.False);
            Assert.That(navigation.SetDoorState("door-a", true, out _), Is.True);
            Assert.That(navigation.TryRoute(start, target, route, out _), Is.False);
            Assert.That(navigation.SetDoorState("door-b", true, out _), Is.True);
            Assert.That(navigation.TryRoute(start, target, route, out var reason), Is.True, reason);
            Assert.That(Vector3.Distance(route[route.Count - 1], target), Is.LessThan(.1f));
            Assert.That(navigation.SetDoorState("door-a", false, out _), Is.True);
            Assert.That(navigation.TryRoute(start, target, route, out _), Is.False);
            Assert.That(route, Is.Empty);
        }

        [Test]
        public void GeometryRevisionOrFrameChange_RequiresExplicitRebinding()
        {
            var frame = ObjectInScene("Geometry frame").transform;
            var bytes = Bake(Plane(0, 20, 0));
            var binding = new FloorNavigationBinding { FloorId = "floor", GeometryRevision = "other", Frame = frame };
            Assert.That(navigation.RegisterFloor(binding, bytes, out _), Is.False);
            binding.GeometryRevision = Revision;
            Assert.That(navigation.RegisterFloor(binding, bytes, out var reason), Is.True, reason);
            var start = new Vector3(2, .05f, 2);
            var target = new Vector3(18, .05f, 2);
            Assert.That(navigation.TryRoute(start, target, route, out reason), Is.True, reason);
            frame.position = Vector3.right * 30;
            Assert.That(navigation.TryRoute(start, target, route, out _), Is.False);
            Assert.That(route, Is.Empty);
            navigation.BeginGeometry("moved-geometry");
            binding.GeometryRevision = "moved-geometry";
            Assert.That(navigation.RegisterFloor(binding, bytes, out reason), Is.True, reason);
            Assert.That(navigation.TryRoute(start + frame.position, target + frame.position, route, out reason), Is.True, reason);
        }

        [Test]
        public void Culling_ReturnsOnlyCosmetics_RetainsIdentityAndDestination_RejectsDuplicateOwner()
        {
            Register("floor", Bake(Plane(0, 20, 0)));
            var actor = ObjectInScene("Persistent actor").AddComponent<ActorNavigationBinding>();
            actor.ActorId = "citizen-001"; actor.Navigation = navigation;
            actor.transform.position = new Vector3(2, .05f, 2);
            var goal = new Vector3(18, .05f, 2);
            Assert.That(actor.SetDestination(goal, out var reason), Is.True, reason);
            var pool = ObjectInScene("Cosmetic pool").AddComponent<ActorVisualPool>();
            var template = ObjectInScene("Visual-only template");
            template.SetActive(false);
            pool.Templates = new[] { template };
            Assert.That(pool.RegisterActor(actor, "김민수", "시민", "출구 찾기", out reason), Is.True, reason);
            Assert.That(pool.RefreshVisibility(actor.transform.position, out reason), Is.True, reason);
            Assert.That(pool.TryGetVisual(actor.ActorId, out var firstVisual), Is.True);
            Assert.That(pool.SetCulled(actor.ActorId, true), Is.True);
            Assert.That(pool.TryGetVisual(actor.ActorId, out _), Is.False);
            Assert.That(actor.gameObject.activeSelf, Is.True);
            Assert.That(actor.HasDestination, Is.True);
            Assert.That(actor.Destination, Is.EqualTo(goal));
            Assert.That(actor.HasArrived, Is.False);
            Assert.That(navigation.ActorCount, Is.EqualTo(1));
            Assert.That(pool.SetCulled(actor.ActorId, false), Is.True);
            Assert.That(pool.RefreshVisibility(actor.transform.position, out reason), Is.True, reason);
            Assert.That(pool.TryGetVisual(actor.ActorId, out var returnedVisual), Is.True);
            Assert.That(returnedVisual, Is.SameAs(firstVisual));
            Assert.That(returnedVisual.transform.parent, Is.EqualTo(actor.transform));
            var duplicate = ObjectInScene("Duplicate actor").AddComponent<ActorNavigationBinding>();
            duplicate.ActorId = actor.ActorId; duplicate.Navigation = navigation;
            duplicate.transform.position = actor.transform.position;
            Assert.That(duplicate.SetDestination(goal, out _), Is.False);
            Assert.That(pool.RegisterActor(duplicate, "다른 이름", "시민", "", out _), Is.False);
            Assert.That(navigation.ActorCount, Is.EqualTo(1));
            Assert.That(pool.RegisteredCount, Is.EqualTo(1));
            pool.UnregisterActor(actor.ActorId);
        }

        [Test]
        public void CheckpointReset_ValidatesBeforeMoving_AndDiscardsAbandonedTravelOnlyAfterSuccess()
        {
            Register("floor", Bake(Plane(0, 20, 0)));
            var actor = ObjectInScene("Checkpoint actor").AddComponent<ActorNavigationBinding>();
            actor.ActorId = "checkpoint-actor";
            actor.Navigation = navigation;
            var original = new Vector3(2, .05f, 2);
            var destination = new Vector3(18, .05f, 2);
            actor.transform.position = original;
            Assert.That(actor.SetDestination(destination, out var reason), Is.True, reason);
            Assert.That(actor.RestoreCheckpointPosition(new Vector3(5, 20, 2), out _), Is.False);
            Assert.That(actor.transform.position, Is.EqualTo(original));
            Assert.That(actor.Destination, Is.EqualTo(destination));
            Assert.That(actor.HasDestination, Is.True);
            var checkpoint = new Vector3(6, .05f, 2);
            Assert.That(actor.RestoreCheckpointPosition(checkpoint, out reason), Is.True, reason);
            Assert.That(actor.transform.position, Is.EqualTo(checkpoint));
            Assert.That(actor.HasDestination, Is.False);
            Assert.That(actor.HasArrived, Is.False, "Rewind is not evidence of travel completion.");
            Assert.That(actor.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(actor.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(navigation.ActorCount, Is.EqualTo(1));
            Assert.That(actor.ActorId, Is.EqualTo("checkpoint-actor"));
        }

        private void Register(string id, byte[] data)
        {
            Assert.That(navigation.RegisterFloor(new FloorNavigationBinding { FloorId = id, GeometryRevision = Revision },
                data, out var reason), Is.True, reason);
        }
        private byte[] Bake(MeshFilter geometry) => GameplayNavigationBaker.BakeGeometry(null, new[] { geometry }, out _);
        private MeshFilter Plane(float minX, float maxX, float y) => Strip(new[] { minX, maxX }, new[] { y, y });
        private MeshFilter Strip(float[] x, float[] y)
        {
            var vertices = new Vector3[x.Length * 2];
            var triangles = new int[(x.Length - 1) * 6];
            for (var i = 0; i < x.Length; i++)
            {
                vertices[i * 2] = new Vector3(x[i], y[i], 0);
                vertices[i * 2 + 1] = new Vector3(x[i], y[i], 4);
                if (i == x.Length - 1) continue;
                var offset = i * 6; var v = i * 2;
                triangles[offset] = v; triangles[offset + 1] = v + 1; triangles[offset + 2] = v + 3;
                triangles[offset + 3] = v; triangles[offset + 4] = v + 3; triangles[offset + 5] = v + 2;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds(); meshes.Add(mesh);
            var filter = ObjectInScene("Actual synthetic walkable strip").AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            return filter;
        }
        private GameObject ObjectInScene(string name)
        {
            var value = new GameObject(name);
            SceneManager.MoveGameObjectToScene(value, scene);
            return value;
        }
    }
}
#endif
