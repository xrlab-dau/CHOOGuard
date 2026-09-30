using System;
using System.Collections;
using System.Linq;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ChooGuard.Tests.PlayMode
{
    // 길 안내(PathService)는 요청을 처리하는 순간 몸이 걷지 못하면(에스컬레이터를 타는 중, 앉는 중, 발밑 길이 잠깐 사라짐) 그 요청을
    // 버린다. 새 길을 기다리는 몸을 아무도 다시 묻지 않으면 그 자리에 서 버린다(CI macOS 2026-09-30: 대피하던 승객이 20 s 동안
    // planning=True 로 서 있었다). 걸을 수 있게 되면 요청이 다시 줄을 서야 한다.
    public sealed class PathRequestTests
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

        // 근무는 일시정지(timeScale 0)로 열린다: 시간이 흐르도록 매 프레임 풀어 둔다.
        private static void Resume(EmergencySession shift)
        {
            if (!shift.Player.ExternalInputMode) shift.Player.SetExternalInputMode(true);
            if (shift.Player.IsPaused) shift.Player.Resume(false);
        }

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

        /// <summary>Two waiting places on one floor 12-22 m apart with a plain walk between them: the trip is walked directly, so the body's only way to move is the one request it asked for.</summary>
        private static (Vector3 from, Vector3 to) ShortWalk(StationWorld world)
        {
            var spots = world.Points.All.Where(p => p.Kind == PointKind.Wait).Select(p => StationWorld.OnNavMesh(p.Position, 2f)).ToList();
            var path = new NavMeshPath();
            foreach (var from in spots)
                foreach (var to in spots)
                {
                    var d = to - from;
                    float flat = new Vector2(d.x, d.z).magnitude;
                    if (Mathf.Abs(d.y) > .5f || flat < 12f || flat > 22f) continue;
                    if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) return (from, to);
                }
            Assert.Fail("no two waiting places 12-22 m apart on one floor");
            return default;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator RouteAskedWhileTheBodyCannotWalk_IsAskedAgainOnceItCan()
        {
            EmergencySession shift = null;
            yield return StartShift(s => shift = s);
            var (from, to) = ShortWalk(shift.World);
            var go = Object.Instantiate(shift.Art.Crowd.FemalePassengers.First(p => p != null), from, Quaternion.identity);
            var body = go.GetComponent<PersonBody>();
            if (!body.Agent.isOnNavMesh) body.Agent.Warp(from);
            Assert.That(body.Agent.isOnNavMesh, Is.True, "the test body is not on the navmesh at " + from);
            Resume(shift);
            yield return null;

            Assert.That(body.GoTo(to, 1.3f), Is.True, "the body did not ask for a route");
            // 요청이 처리되기 전에 걷지 못하게 되고, 길 안내가 줄을 모두 처리할 때까지(2 s면 수십 건) 그대로 둔다.
            body.Agent.enabled = false;
            float served = Time.time + 2f;
            while (Time.time < served) { Resume(shift); yield return null; }
            body.Agent.enabled = true;

            float start = Vector3.Distance(body.transform.position, to);
            float until = Time.time + 8f;
            while (Time.time < until && Vector3.Distance(body.transform.position, to) > start - 3f) { Resume(shift); yield return null; }
            float left = Vector3.Distance(body.transform.position, to);
            string state = "planning=" + body.Planning + " hasPath=" + body.Agent.hasPath + " onNavMesh=" + body.OnNavMesh;
            Object.Destroy(go);
            Assert.That(left, Is.LessThan(start - 3f), "the body stood still " + start.ToString("F1") + " m from its goal: the route it asked for was dropped while it could not walk and never asked for again (" + state + ")");

            var scratch = SceneManager.CreateScene("PathRequestScratch");
            SceneManager.SetActiveScene(scratch);
            yield return SceneManager.UnloadSceneAsync(SceneFlow.EmergencyScene);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(SceneFlow.StationScene);
        }
    }
}
