#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using ChooGuard.Contracts;
using ChooGuard.Editor;
using ChooGuard.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class CSWORLD0102Tests
    {
        private Scene scene;
        private WorldEntityAnchor door;
        private WorldProjectionBinding binding;
        [SetUp] public void SetUp()
        {
            scene = FixtureBuilder.CreatePreviewFixture();
            door = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<WorldEntityAnchor>()).Single(x => x.Representation == WorldRepresentationKind.Door);
            binding = door.BindProjection(new StableId("run.a"), new StableId("authority.a"), door.FrameId, door.EntityId);
        }
        [TearDown] public void TearDown() { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
        private EntityVisualProjection Projection(long revision, bool open = false, bool visible = true) => new EntityVisualProjection(door.EntityId, revision, open, visible);

        [Test] public void Projection_RejectsWrongEntityAndWrongBinding()
        {
            Assert.Throws<ArgumentException>(() => door.ApplyProjection(binding, new EntityVisualProjection(new StableId("other"), 0, false, true)));
            Assert.Throws<ArgumentException>(() => door.BindProjection(new StableId("run.a"), new StableId("authority.a"), new StableId("wrong.frame"), door.EntityId));
            Assert.Throws<InvalidOperationException>(() => door.BindProjection(new StableId("run.b"), new StableId("authority.a"), door.FrameId, door.EntityId));
            Assert.Throws<ArgumentException>(() => door.ApplyProjection(null, Projection(0)));
            Assert.That(door.ProjectedRevision, Is.EqualTo(-1));
        }

        [Test] public void OtherAnchorBinding_CannotSupplyHigherRevision()
        {
            var other = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<WorldEntityAnchor>()).First(x => x != door);
            var otherBinding = other.BindProjection(new StableId("run.b"), new StableId("authority.b"), other.FrameId, other.EntityId);
            Assert.Throws<ArgumentException>(() => door.ApplyProjection(otherBinding, Projection(100)));
            Assert.That(door.ProjectedRevision, Is.EqualTo(-1));
        }

        [Test] public void Revision_IsStrictlyIncreasingIncludingEqualRejection()
        {
            door.ApplyProjection(binding, Projection(5));
            Assert.Throws<InvalidOperationException>(() => door.ApplyProjection(binding, Projection(4, true)));
            Assert.Throws<InvalidOperationException>(() => door.ApplyProjection(binding, Projection(5, true)));
            Assert.That(door.ProjectedDoorOpen, Is.False);
            door.ApplyProjection(binding, Projection(6, true));
            Assert.That(door.ProjectedRevision, Is.EqualTo(6));
            Assert.That(door.ProjectedDoorOpen, Is.True);
        }

        [Test] public void HiddenRenderer_RetainsActiveLogicalObjectColliderAndDoorState()
        {
            door.ApplyProjection(binding, Projection(0, true, false));
            Assert.That(door.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
            Assert.That(door.gameObject.activeInHierarchy, Is.True);
            Assert.That(door.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(door.ProjectedDoorOpen, Is.True);
        }

        [Test] public void DoorOpening_ChangesOnlyVisualRotationNotCollisionOrAnchorTransform()
        {
            var position = door.transform.position;
            var collider = door.GetComponent<BoxCollider>();
            var size = collider.size;
            door.ApplyProjection(binding, Projection(0));
            var renderer = door.GetComponentInChildren<Renderer>();
            var closed = renderer.transform.localRotation;
            door.ApplyProjection(binding, Projection(1, true));
            Assert.That(renderer.transform.localRotation, Is.Not.EqualTo(closed));
            Assert.That(door.transform.position, Is.EqualTo(position));
            Assert.That(door.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(collider.size, Is.EqualTo(size));
            Assert.That(collider.enabled, Is.True);
        }

        [Test] public void Projection_RejectsInvalidIdsAndNegativeRevision()
        {
            Assert.Throws<ArgumentException>(() => new EntityVisualProjection(default, 0, false, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => Projection(-1));
            Assert.Throws<ArgumentNullException>(() => door.ApplyProjection(binding, null));
        }

        [Test] public void Binding_CannotChangeAuthorityOrReinitializeIdentity()
        {
            Assert.That(door.BindProjection(binding.RunId, binding.AuthorityId, binding.FrameId, binding.EntityId), Is.SameAs(binding));
            Assert.Throws<InvalidOperationException>(() => door.BindProjection(binding.RunId, new StableId("authority.b"), binding.FrameId, binding.EntityId));
            Assert.Throws<InvalidOperationException>(() => door.Initialize(door.EntityId, door.FrameId, WorldRepresentationKind.Door));
        }
    }
}
#endif
