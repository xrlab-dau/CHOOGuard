using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    // 경로 그래프(Resources/StationRoutes.json)는 navmesh 와 함께 구워 둔 자료다. navmesh 나 역 자료를 다시 구웠는데 그래프를 다시 굽지
    // 않으면 사람들이 길을 못 찾거나 엉뚱한 곳을 향한다: 그때 이 시험이 깨진다(다시 굽기: ChooGuard/Emergency/Bake route graph).
    public sealed class RouteGraphTests
    {
        private string savedKey;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20260930;
        }

        [TearDown]
        public void TearDown() => Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);

        private static IEnumerator StartShift(Action<EmergencySession> ready)
        {
            var station = SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            while (!station.isDone) yield return null;
            var session = SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            while (!session.isDone) yield return null;
            float giveUp = Time.realtimeSinceStartup + 120;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null) && Time.realtimeSinceStartup < giveUp) yield return null;
            Assert.That(EmergencySession.Current, Is.Not.Null, "the shift did not start");
            ready(EmergencySession.Current);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BakedGraph_SitsOnTheNavmeshAndJoinsEveryPlaceToAnExitWithoutTheElevator()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var world = shift.World;
            var graph = world.Paths.Graph;
            Assert.That(graph.Nodes.Count, Is.GreaterThan(200), "the baked route graph is empty");

            var off = graph.Nodes.Where(node => !NavMesh.SamplePosition(node.Position, out _, .6f, NavMesh.AllAreas)).ToList();
            Assert.That(off, Is.Empty, off.Count + " route graph nodes are off the navmesh (the graph is older than the navmesh): " + string.Join(", ", off.Take(5).Select(n => n.Position.ToString("F1"))));

            var exits = world.Points.Of(PointKind.Exit).Select(exit => graph.ByLabel(exit.Id)).ToList();
            Assert.That(exits, Has.None.Null, "every exit needs a node in the route graph");
            var fields = exits.Select(exit => graph.Toward(exit, RouteProfile.Evacuation, Array.Empty<(Vector3, float)>(), Time.time)).ToList();
            var cut = new List<string>();
            var skip = new List<RouteGraph.Node>();
            foreach (var point in world.Points.All)
            {
                if (point.Kind == PointKind.Exit) continue;
                var from = StationWorld.OnNavMesh(point.Position, 2f);
                // 발밑 노드가 막다른 곳이면 다음 노드로 본다(PathService 와 같은 규칙).
                skip.Clear();
                bool reaches = false;
                for (int attempt = 0; attempt < 3 && !reaches; attempt++)
                {
                    var node = graph.Nearest(from, skip);
                    if (node == null) break;
                    skip.Add(node);
                    reaches = fields.Any(field => field.Reaches(node));
                }
                if (!reaches) cut.Add(point.Id + " " + point.Position.ToString("F1"));
            }
            Assert.That(cut, Is.Empty, cut.Count + " places cannot reach any exit on foot on the route graph: " + string.Join(", ", cut.Take(8)));

            var scratch = SceneManager.CreateScene("RouteGraphScratch");
            SceneManager.SetActiveScene(scratch);
            yield return SceneManager.UnloadSceneAsync(SceneFlow.EmergencyScene);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(SceneFlow.StationScene);
        }
    }
}
