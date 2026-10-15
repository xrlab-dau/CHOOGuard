using System;
using System.Collections;
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
    public sealed class PlayerSpawnTests
    {
        private string savedKey;
        private int savedSeed;

        [SetUp]
        public void SetUp()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            savedSeed = EmergencySession.NextSeed;
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            EmergencySession.NextSeed = savedSeed;
            Time.timeScale = 1;
            yield return UnloadShift();
        }

        private static IEnumerator UnloadShift()
        {
            var emergency = SceneManager.GetSceneByName(SceneFlow.EmergencyScene);
            var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
            if (!emergency.isLoaded && !station.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("SpawnScratch"));
            if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator NewShiftsStartAtDifferentIndoorPositionsWhereThePlayerCanStandAndWalk()
        {
            Vector3 first = default;
            for (int run = 0; run < 2; run++)
            {
                EmergencySession.NextSeed = 20261009 + run;
                yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
                yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
                float until = Time.realtimeSinceStartup + 90;
                while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null) && Time.realtimeSinceStartup < until) yield return null;
                var session = EmergencySession.Current;
                Assert.That(session, Is.Not.Null, "근무가 시작되지 않았습니다.");
                var player = session.Player;
                var start = player.transform.position;
                var zone = session.World.Points.ZoneAt(start);
                Assert.That(zone, Is.Not.Null);
                Assert.That(StationWorld.IsOutdoorZone(zone.id), Is.False, "역사 밖에서 시작하면 안 됩니다.");
                Assert.That(zone.id, Is.Not.EqualTo("tracks"));
                Assert.That(session.World.Points.PlatformAt(start), Is.Null);
                Assert.That(NavMesh.SamplePosition(start, out var nav, .2f, 1 << NavMesh.GetAreaFromName("Walkable")), Is.True);
                Assert.That(Mathf.Abs(nav.position.y - start.y), Is.LessThan(.2f), "다른 층으로 스냅하면 안 됩니다.");
                Assert.That(Physics.Raycast(start + Vector3.up * .2f, Vector3.down, out _, .5f, ~0, QueryTriggerInteraction.Ignore), Is.True, "실제 바닥이 있어야 합니다.");
                Assert.That(Physics.Raycast(start + Vector3.up * 40, Vector3.down, out _, 38f, ~0, QueryTriggerInteraction.Ignore), Is.True, "구역 경계가 겹치는 야외 광장을 제외합니다.");
                if (run == 0) first = start;
                else Assert.That(Vector3.Distance(first, start), Is.GreaterThan(1f), "시드가 바뀐 근무도 고정 위치에서 시작했습니다.");

                player.SetExternalInputMode(true);
                Assert.That(player.Resume(false), Is.True);
                for (int i = 0; i < 25; i++) player.StepInput(Vector2.zero, Vector2.zero, false, false, false, .02f);
                Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True, "스폰 직후 바닥 위에 설 수 있어야 합니다.");
                Assert.That(Mathf.Abs(player.transform.position.y - start.y), Is.LessThan(.2f), "바닥을 뚫고 떨어지면 안 됩니다.");
                var settled = player.transform.position;
                bool walked = false;
                for (int direction = 0; direction < 8 && !walked; direction++)
                {
                    player.RestorePhysicalPose(settled, player.YawDegrees, 0);
                    float angle = direction * Mathf.PI / 4;
                    var move = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    for (int i = 0; i < 20; i++) player.StepInput(move, Vector2.zero, false, false, false, .02f);
                    var delta = player.transform.position - settled;
                    delta.y = 0;
                    walked = delta.sqrMagnitude > .04f;
                }
                Assert.That(walked, Is.True, "설비나 벽 속에서 시작해 움직이지 못하면 안 됩니다.");
                player.Pause();
                yield return UnloadShift();
            }
        }
    }
}
