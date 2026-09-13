using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    [PrebuildSetup("ChooGuard.Foundation.Multiplayer.Tests.WorldMotionPlaySceneSetup")]
    [PostBuildCleanup("ChooGuard.Foundation.Multiplayer.Tests.WorldMotionPlaySceneSetup")]
    public sealed class WorldMotionPlayTests
    {
        private const string Root = "Assets/CHOOguardGenerated/WorldMotionPlayFixture";
        private ConnectedWorldDefinition world;
        private ConnectedWorldGeometry geometry;
        private ConnectedWorldMotionAdapter adapter;
        private readonly List<Scene> loaded = new List<Scene>();
        private readonly List<GameObject> owned = new List<GameObject>();
        [UnitySetUp] public IEnumerator Load()
        {
            world = JsonUtility.FromJson<ConnectedWorldDefinition>(File.ReadAllText(Root + "/connected-world-profile.json")); world.Validate();
            foreach (var r in world.Regions)
            {
#if UNITY_EDITOR
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(Root + "/" + r.SceneName + ".unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
                yield return SceneManager.LoadSceneAsync(r.SceneName, LoadSceneMode.Additive);
#endif
                loaded.Add(SceneManager.GetSceneByName(r.SceneName));
            }
            Physics.SyncTransforms(); geometry = ConnectedWorldGeometry.Capture(world, UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None));
        }
        [UnityTearDown] public IEnumerator Clear()
        {
            adapter?.Dispose(); adapter = null; geometry?.Dispose(); geometry = null;
            foreach (var go in owned) UnityEngine.Object.Destroy(go); owned.Clear();
            foreach (var scene in loaded) if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene); loaded.Clear();
        }
        private WorldBodySpawn Spawn(string id, string region, Point3 p, double radius = WorldBodyPresentation.NpcRadiusM) => new WorldBodySpawn { BodyId = id, Pose = world.Pose(region, p), RadiusM = radius };
        [UnityTest]
        public IEnumerator ActualScene120BodiesMoveWithPresentationCollidersExcluded()
        {
            var bodies = new List<WorldBodySpawn>();
            foreach (var r in world.Regions)
            {
                for (var i = 0; i < (Array.IndexOf(world.Regions, r) < 3 ? 10 : 9) && bodies.Count < 120; i++)
                {
                    var radius = bodies.Count < 100 ? WorldBodyPresentation.NpcRadiusM : .3;
                    Assert.That(geometry.TryFindStandingPoint(r.Id, r.Hub, radius, 1.8, bodies.ToArray(), out var p), Is.True);
                    bodies.Add(Spawn("body-" + bodies.Count.ToString("D3"), r.Id, p.Position, radius));
                }
            }
            adapter = new ConnectedWorldMotionAdapter(geometry, bodies.ToArray(), 17);
            foreach (var b in bodies)
            {
                var owner = new GameObject("Body_" + b.BodyId); owned.Add(owner); owner.AddComponent<WorldBodyPresentation>().BodyId = b.BodyId;
                owner.transform.position = new Vector3(b.Pose.Position.X, b.Pose.Position.Y, b.Pose.Position.Z);
                var c = owner.AddComponent<CapsuleCollider>(); c.radius = (float)b.RadiusM; c.height = 1.8f; c.center = Vector3.up * .9f;
            }
            Physics.SyncTransforms();
            for (var i = 0; i < 3; i++)
            {
                Assert.That(adapter.TryAdvance(.05, bodies.Select(b => new WorldMotionCommand { BodyId = b.BodyId, DesiredVelocity = new Point3(.15f, 0, 0), Pinned = b.BodyId.EndsWith("0") }).ToArray(), out var report), Is.True, report.Failure);
                foreach (var owner in owned) { var p = adapter.Pose(owner.GetComponent<WorldBodyPresentation>().BodyId).Position; owner.transform.position = new Vector3(p.X, p.Y, p.Z); }
                Physics.SyncTransforms(); yield return null;
            }
            Assert.That(adapter.BodyPoses.Count, Is.EqualTo(120)); Assert.That(adapter.ValidateBodies(out var failure), Is.True, failure);
            Assert.That(adapter.Capture().Crowd.Tick, Is.EqualTo(3));
        }
        [UnityTest]
        public IEnumerator MovingMetroOfflineBodyAndLateRestoreUseFrameDeltaAndFixedBridge()
        {
            var car = world.Region("rolling_stock_metro"); var body = Spawn("offline", car.Id, car.Hub);
            adapter = new ConnectedWorldMotionAdapter(geometry, new[] { body }, 19); var original = adapter.Pose("offline").LocalPosition;
            var portal = world.Portals.Single(p => p.From == car.Id); var bridge = geometry.Supports.Single(s => s.PortalId == portal.Id).Source.transform.position;
            var frames = world.Frames.Select(f => f.Copy()).ToArray(); frames.Single(f => f.FrameId == car.FrameId).Origin.X -= 30;
            Assert.That(adapter.TryAdvance(.05, new[] { new WorldMotionCommand { BodyId = "offline", Pinned = true } }, frames, new[] { portal.Id }, out var report), Is.True, report.Failure);
            yield return null;
            var checkpoint = adapter.Capture(); Assert.That(adapter.Pose("offline").LocalPosition.DistanceSquared(original), Is.LessThan(1e-9));
            Assert.That(geometry.Supports.Single(s => s.PortalId == portal.Id).Source.transform.position, Is.EqualTo(bridge));
            var view = UnityEngine.Object.FindObjectsByType<ConnectedRegionView>(FindObjectsSortMode.None).Single(v => v.RegionId == car.Id);
            Assert.That(Quaternion.Angle(view.transform.rotation, Quaternion.identity), Is.LessThan(.001));
            adapter.Dispose(); adapter = new ConnectedWorldMotionAdapter(geometry, new[] { body }, 19);
            Assert.That(adapter.TryRestore(checkpoint, out var failure), Is.True, failure); yield return null;
            Assert.That(JsonUtility.ToJson(adapter.Capture()), Is.EqualTo(JsonUtility.ToJson(checkpoint)));
            Assert.That(adapter.ValidateBodies(out failure), Is.True, failure);
        }
        [UnityTest]
        public IEnumerator NewPhysicalBlockerRejectsWholeCandidateWithoutPartialCommit()
        {
            const string region = "station_concourse_2f";
            adapter = new ConnectedWorldMotionAdapter(geometry, new[] { Spawn("a", region, new Point3(-2, 6, 0)), Spawn("b", region, new Point3(2, 6, 0)) });
            var wall = new GameObject("Late physical obstacle"); owned.Add(wall); wall.transform.position = new Vector3(2.43f, 7, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(.02f, 2, 2); Physics.SyncTransforms(); yield return null;
            var before = JsonUtility.ToJson(adapter.Capture());
            Assert.That(adapter.TryAdvance(.1, new[] { new WorldMotionCommand { BodyId = "a", DesiredVelocity = new Point3(1.2f, 0, 0) }, new WorldMotionCommand { BodyId = "b", DesiredVelocity = new Point3(1.2f, 0, 0) } }, out _), Is.False);
            Assert.That(JsonUtility.ToJson(adapter.Capture()), Is.EqualTo(before));
        }
    }
}
