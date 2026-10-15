#if UNITY_INCLUDE_TESTS
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

namespace ChooGuard.Tests.PlayMode
{
    public sealed class CitizenEscortTests
    {
        private string savedKey;
        private int savedSeed, savedCaptureRate;

        [SetUp]
        public void Setup()
        {
            savedKey = Environment.GetEnvironmentVariable(JevKey.Variable);
            savedSeed = EmergencySession.NextSeed;
            savedCaptureRate = Time.captureFramerate;
            Environment.SetEnvironmentVariable(JevKey.Variable, "off");
            EmergencySession.NextSeed = 20261008;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Environment.SetEnvironmentVariable(JevKey.Variable, savedKey);
            EmergencySession.NextSeed = savedSeed;
            Time.captureFramerate = savedCaptureRate;
            Time.timeScale = 1;
            var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
            var emergency = SceneManager.GetSceneByName(SceneFlow.EmergencyScene);
            if (!station.isLoaded && !emergency.isLoaded) yield break;
            SceneManager.SetActiveScene(SceneManager.CreateScene("CitizenEscortScratch"));
            if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
            yield return null;
            if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
        }

        private static bool FreeWalk(Passenger person, StationWorld world, Vector3 direction, out Vector3 start, out Vector3 end)
        {
            start = end = person.transform.position;
            var at = person.transform.position;
            if (!NavMesh.SamplePosition(at + direction * .85f, out var first, .3f, NavMesh.AllAreas) || Mathf.Abs(first.position.y - at.y) > .3f) return false;
            if (!NavMesh.SamplePosition(at + direction * 3.6f, out var last, .3f, NavMesh.AllAreas) || Mathf.Abs(last.position.y - at.y) > .3f) return false;
            if (world.IsClosed(first.position) || world.IsClosed(last.position) || NavMesh.Raycast(at, last.position, out _, NavMesh.AllAreas)) return false;
            if (Physics.CheckCapsule(first.position + Vector3.up * .3f, first.position + Vector3.up * 1.6f, .24f, ~0, QueryTriggerInteraction.Ignore)) return false;
            start = first.position;
            end = last.position;
            return true;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator RealPassenger_WalksThroughExistingRoute_OneLeaderOwnsOneEscort_ReleaseRestoresMovement()
        {
            yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
            yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
            float deadline = Time.realtimeSinceStartup + 120;
            while ((EmergencySession.Current == null || EmergencySession.Current.Crowd == null || EmergencySession.Current.Crowd.People.Count == 0) && Time.realtimeSinceStartup < deadline) yield return null;
            var shift = EmergencySession.Current;
            Assert.That(shift, Is.Not.Null);
            Assert.That(shift.Crowd, Is.Not.Null);
            Assert.That(shift.Jev.Available, Is.False);
            var player = shift.Player;
            player.SetExternalInputMode(true);
            if (!player.IsPaused) player.Pause();
            Passenger citizen = null;
            Vector3 start = Vector3.zero, end = Vector3.zero, direction = Vector3.forward;
            foreach (var person in shift.Crowd.People.Where(p => !p.Hurt && !p.Hostile && p.Body.Visible && p.Body.OnNavMesh && !p.Body.Scripted && p.Body.Seat == PersonBody.SeatPhase.None && p.Current != Passenger.Activity.Board && p.Current != Passenger.Activity.Alight && p.Current != Passenger.Activity.InTrain && p.Current != Passenger.Activity.OnTrack))
            {
                var nearExit = shift.World.Points.Nearest(PointKind.Exit, person.transform.position);
                if (nearExit != null && Vector3.Distance(nearExit.Position, person.transform.position) < 6) continue;
                foreach (var toward in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
                {
                    if (!FreeWalk(person, shift.World, toward, out start, out end)) continue;
                    citizen = person;
                    direction = toward;
                    break;
                }
                if (citizen != null) break;
            }
            Assert.That(citizen, Is.Not.Null, "An existing walking passenger and a clear existing floor segment must be available; no synthetic person is spawned.");
            citizen.Replan();
            float oldPace = player.WalkSpeed;
            bool oldSprint = player.SprintBlocked;
            float yaw = Quaternion.LookRotation(direction).eulerAngles.y;
            player.RestorePhysicalPose(start, yaw, 0);
            Assert.That(player.Resume(false), Is.True);
            Physics.SyncTransforms();
            var initialPersonPosition = citizen.transform.position;
            Assert.That(citizen.TrySecondary(player, out _), Is.True);
            Assert.That(citizen.BeingEscorted, Is.True);
            Assert.That(citizen.Current, Is.EqualTo(Passenger.Activity.Escorted));
            Assert.That(Vector3.Distance(initialPersonPosition, citizen.transform.position), Is.LessThan(.00001f), "Starting an escort must not move/warp the passenger.");
            Assert.That(player.WalkSpeed, Is.LessThanOrEqualTo(citizen.WalkingPace));
            Assert.That(player.SprintBlocked, Is.True);
            var another = shift.Crowd.People.First(p => p != citizen && !p.Hurt && !p.Hostile && p.Body.Visible && p.Body.OnNavMesh && !p.Body.Scripted && p.Body.Seat == PersonBody.SeatPhase.None && p.Current != Passenger.Activity.Board && p.Current != Passenger.Activity.Alight && p.Current != Passenger.Activity.InTrain && p.Current != Passenger.Activity.OnTrack);
            Assert.That(another.Escort.Begin(player, out var refused), Is.False);
            StringAssert.Contains("동행 중인 승객", refused);
            // Existing speech, announcements and delayed routine results cannot steal the physical escort.
            Assert.That(citizen.TryInteract(player, out _), Is.True);
            citizen.Redirect(null);
            citizen.Evacuate(false);
            Assert.That(citizen.BeingEscorted, Is.True);
            Assert.That(citizen.Current, Is.EqualTo(Passenger.Activity.Escorted));
            Time.captureFramerate = 30;
            float until = Time.time + 10;
            while (Time.time < until && citizen.BeingEscorted)
            {
                if (player.IsPaused) player.Resume(false);
                if (Vector3.Dot(end - player.transform.position, direction) > .2f)
                    Assert.That(player.StepInput(Vector2.up, Vector2.zero, true, false, false, 1f / 30f), Is.True);
                yield return null;
                if (Vector3.Distance(initialPersonPosition, citizen.transform.position) > .7f && Vector3.Distance(player.transform.position, citizen.transform.position) < 2f) break;
            }
            Assert.That(citizen.BeingEscorted, Is.True, "A clear, reachable same-floor walking segment must keep the escort intact.");
            Assert.That(Vector3.Distance(initialPersonPosition, citizen.transform.position), Is.GreaterThan(.7f), "The existing PersonBody and path service must physically move the passenger.");
            Assert.That(citizen.TrySecondary(player, out _), Is.True);
            Assert.That(citizen.BeingEscorted, Is.False);
            Assert.That(player.WalkSpeed, Is.EqualTo(oldPace).Within(.0001f));
            Assert.That(player.SprintBlocked, Is.EqualTo(oldSprint));
            // A distant staff member cannot acquire the same passenger by a menu/remote call.
            player.RestorePhysicalPose(citizen.transform.position + direction * 8f, yaw, 0);
            Assert.That(citizen.TrySecondary(player, out var tooFar), Is.False);
            StringAssert.Contains("가까이", tooFar);
        }
    }
}
#endif
