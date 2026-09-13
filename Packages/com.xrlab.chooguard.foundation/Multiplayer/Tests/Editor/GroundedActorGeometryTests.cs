using NUnit.Framework;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class GroundedActorGeometryTests
    {
        private Scene scene;
        private SceneSetup[] previous;
        [SetUp] public void Create()
        {
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) Assert.Ignore("Preserve unsaved scenes before geometry fixture.");
            previous = EditorSceneManager.GetSceneManagerSetup();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        [TearDown] public void Remove()
        {
            if (previous == null) return;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (previous.Any(s => s.isLoaded && s.isActive) && previous.All(s => !string.IsNullOrEmpty(s.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        private GameObject Box(Vector3 at, Vector3 scale, bool floor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = at; go.transform.localScale = scale;
            if (floor) { var support = go.AddComponent<ConnectedWalkableSurface>(); support.SurfaceId = "fixture-floor"; support.FrameId = "world"; }
            Physics.SyncTransforms(); return go;
        }

        [Test]
        public void FootprintUsesTheRequestedBodyRadiusAndReturnsItsAuthoredSupport()
        {
            Box(new Vector3(10000, -.1f, 0), new Vector3(.5f, .2f, 5), true);
            var from = new Vector3(10000, 0, 0);
            Assert.That(GroundedWorldMotor.TryGroundedStep(from, Vector3.forward * .1f, out var next, .15f, 1.8f), Is.True);
            Assert.That(GroundedWorldMotor.TrySampleSupport(next, out var support, .15f, 1.8f), Is.True);
            Assert.That(support.Surface.SurfaceId, Is.EqualTo("fixture-floor")); Assert.That(support.Normal, Is.EqualTo(Vector3.up));
            Assert.That(GroundedWorldMotor.TryGroundedStep(from, Vector3.forward * .1f, out _, .3f, 1.8f), Is.False);
        }

        [Test]
        public void SweptCollisionAndHeadroomUseTheSameBodyDimensions()
        {
            Box(new Vector3(10000, -.1f, 0), new Vector3(10, .2f, 10), true);
            Box(new Vector3(10000.5f, 1.2f, 0), new Vector3(.1f, 2.4f, 5), false);
            var from = new Vector3(10000, 0, 0);
            Assert.That(GroundedWorldMotor.TryGroundedStep(from, Vector3.right * .25f, out _, .15f, 1.8f), Is.True);
            Assert.That(GroundedWorldMotor.TryGroundedStep(from, Vector3.right * .25f, out _, .3f, 1.8f), Is.False);
            Box(new Vector3(10000, 1.3f, 0), new Vector3(1, .1f, 1), false);
            Assert.That(GroundedWorldMotor.TrySampleSupport(from, out _, .15f, 1.8f), Is.False);
            Assert.That(GroundedWorldMotor.TrySampleSupport(from, out _, .15f, .8f), Is.True);
        }
    }
}
