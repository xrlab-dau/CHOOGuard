using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// One walking passenger follows the staff member through the existing path service.
    /// This is accompanied walking, not lifting an injured person or moving a character by teleport.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CitizenEscort : MonoBehaviour
    {
        private static readonly Dictionary<FirstPersonResponder, CitizenEscort> leaders = new Dictionary<FirstPersonResponder, CitizenEscort>();
        public Passenger Person { get; private set; }
        public FirstPersonResponder Leader { get; private set; }
        public bool Active => engaged;
        private bool engaged;
        public string StateText { get; private set; } = "";
        public const float Reach = 2.5f, Separation = 4.5f;
        private float previousPace, accompaniedPace, nextRoute, separatedAt = -1;
        private bool previousSprintBlocked, previousUrgent;
        private Vector3 lastGoal;
        private bool hasGoal;

        public void Setup(Passenger person) => Person = person;

        private bool HandsOccupied()
        {
            var hands = Person != null && Person.Crowd != null && Person.Crowd.Session != null ? Person.Crowd.Session.Hands : null;
            return hands != null && (hands.Held != null || hands.Hose != null || hands.Aed != null);
        }

        public bool CanBegin(FirstPersonResponder leader, out string reason)
        {
            reason = null;
            if (Active) { reason = "이미 동행 안내 중입니다"; return false; }
            if (Person == null || Person.Crowd == null || Person.Crowd.World == null || Person.Body == null || leader == null || leader.IsPaused)
            { reason = "지금은 동행 안내할 수 없습니다"; return false; }
            if (Person.Hurt || Person.Hostile || !Person.Body.Visible || Person.Body.Scripted || Person.Body.Seat != PersonBody.SeatPhase.None || !Person.Body.OnNavMesh ||
                Person.Current == Passenger.Activity.OnTrack || Person.Current == Passenger.Activity.InTrain || Person.Current == Passenger.Activity.Board || Person.Current == Passenger.Activity.Alight)
            { reason = "보행 가능한 승객만 동행 안내할 수 있습니다"; return false; }
            if (Person.Crowd.Player != leader) { reason = "현재 근무 중인 직원만 동행 안내할 수 있습니다"; return false; }
            if (HandsOccupied() || leader.Holding) { reason = "손에 든 장비를 내려놓은 뒤 동행 안내하세요"; return false; }
            if (leaders.TryGetValue(leader, out var existing) && existing != null && existing.Active)
            { reason = "동행 중인 승객을 먼저 인계하거나 안내를 마치세요"; return false; }
            if (Vector3.Distance(leader.transform.position, Person.transform.position) > Reach)
            { reason = "승객 가까이에서 동행 안내를 시작하세요"; return false; }
            if (Mathf.Abs(leader.transform.position.y - Person.transform.position.y) > .75f)
            { reason = "같은 층에서 승객 가까이 접근하세요"; return false; }
            return true;
        }

        public bool Begin(FirstPersonResponder leader, out string feedback)
        {
            if (!CanBegin(leader, out feedback)) return false;
            Leader = leader;
            engaged = true;
            leaders[leader] = this;
            previousPace = leader.WalkSpeed;
            previousSprintBlocked = leader.SprintBlocked;
            previousUrgent = Person.Body.Urgent;
            accompaniedPace = Mathf.Min(previousPace, Person.WalkingPace * .9f);
            leader.WalkSpeed = accompaniedPace;
            leader.SprintBlocked = true;
            Person.BeginEscort();
            hasGoal = false;
            nextRoute = 0;
            separatedAt = -1;
            StateText = "직원과 동행 중 · 실제 출구까지 천천히 이동";
            feedback = "승객과 동행 안내를 시작했습니다 · 천천히 이동하고 승객이 따라오는지 확인하세요 · R로 안내 마침";
            Person.Remember("직원의 동행 안내를 받기 시작함");
            return true;
        }

        public bool Release(FirstPersonResponder leader, out string feedback)
        {
            if (!Active || Leader != leader) { feedback = "이 승객과 동행 중이 아닙니다"; return false; }
            Stop("동행 안내를 마쳤습니다 · 필요하면 E로 대피 방향을 안내하세요", null);
            feedback = "동행 안내를 마쳤습니다 · 필요하면 E로 대피 방향을 안내하세요";
            return true;
        }

        private void LateUpdate()
        {
            if (!Active) return;
            if (Leader == null) { Stop("동행 안내 직원이 없어 안내를 멈췄습니다", null); return; }
            if (Leader.IsPaused) return;
            if (Person == null || Person.Body == null || Person.Crowd == null || Person.Hurt || Person.Hostile || Person.Current != Passenger.Activity.Escorted || !Person.Body.Visible)
            { Stop("승객 상태가 바뀌어 동행 안내를 멈췄습니다 · 상태를 확인하세요", null); return; }
            if (HandsOccupied() || Leader.Holding)
            { Stop("장비 조작을 시작해 동행 안내를 멈췄습니다 · 승객을 다시 확인하세요", null); return; }
            Leader.SprintBlocked = true;
            // Existing escalator/elevator traversal keeps control of its physical occupant.
            if (Person.Body.Scripted && Person.Body.Riding != null)
            { StateText = "승객이 연결 통로를 이동 중 · 가까운 층에서 기다리세요"; return; }
            if (Person.Body.Scripted)
            { Stop("승객이 다른 이동을 시작해 동행 안내를 멈췄습니다", null); return; }
            float distance = Vector3.Distance(Leader.transform.position, Person.transform.position);
            if (distance > Separation)
            {
                if (separatedAt < 0) separatedAt = Time.time;
                StateText = "승객과 거리가 벌어짐 · 돌아가 가까이 접근";
                Person.Body.Stop();
                hasGoal = false;
                if (Time.time - separatedAt > 1.25f) Stop("승객과 거리가 벌어져 동행 안내를 멈췄습니다 · 돌아가 다시 안내하세요", null);
                return;
            }
            separatedAt = -1;
            var world = Person.Crowd.World;
            var reached = world.Points.Nearest(PointKind.Exit, Person.transform.position, p =>
                Mathf.Abs(p.Position.y - Person.transform.position.y) < .6f && !world.IsClosed(p.Position, .5f));
            if (reached != null && Vector3.Distance(Person.transform.position, reached.Position) <= 1.1f && distance <= Reach && Person.Body.OnNavMesh)
            { Stop("승객이 실제 출구에 도착했습니다 · 동행 안내 완료", reached); return; }
            if (Person.Body.NoRoute && !Person.Body.Planning)
            { Stop("승객이 이 경로로 따라올 수 없습니다 · 돌아가 다른 통로로 안내하세요", null); return; }
            if (distance <= 1.1f)
            {
                Person.Body.Stop();
                hasGoal = false;
                StateText = "직원 가까이에서 대기 · 천천히 이동";
                return;
            }
            if (Time.time < nextRoute) return;
            nextRoute = Time.time + .6f;
            var target = Leader.transform.position - Leader.transform.forward * .6f;
            if (!NavMesh.SamplePosition(target, out var floor, .8f, NavMesh.AllAreas) || Mathf.Abs(floor.position.y - target.y) > .75f || world.IsClosed(floor.position, .4f))
            { Stop("이 방향으로는 승객을 안내할 수 없습니다 · 가까운 통로에서 다시 안내하세요", null); return; }
            if (!hasGoal || (lastGoal - floor.position).sqrMagnitude > .16f)
            {
                lastGoal = floor.position;
                hasGoal = true;
                Person.Body.GoTo(lastGoal, Person.WalkingPace);
            }
            StateText = "직원을 따라 걷는 중 · 승객 간격과 통로를 확인";
        }

        private void Stop(string feedback, StationPoints.Point reached)
        {
            var leader = Leader;
            if (!ReferenceEquals(leader, null)) leaders.Remove(leader);
            Leader = null;
            engaged = false;
            if (leader != null)
            {
                if (Mathf.Abs(leader.WalkSpeed - accompaniedPace) < .0001f) leader.WalkSpeed = previousPace;
                if (!HandsOccupied()) leader.SprintBlocked = previousSprintBlocked;
            }
            if (Person != null && Person.Body != null)
            {
                Person.Body.Urgent = previousUrgent;
                Person.EndEscort(reached);
                Person.Remember(reached != null ? "직원과 실제 출구에 도착함" : "직원과 동행 안내를 마침");
                if (Person.Crowd != null && Person.Crowd.Session != null && Person.Crowd.Session.Log != null)
                    Person.Crowd.Session.Log.Add("승객 " + Person.Number + " · " + feedback);
            }
            StateText = "";
            if (leader != null && leader.isActiveAndEnabled) leader.ShowFeedback(feedback);
        }

        private void OnDisable()
        {
            if (Active) Stop("동행 안내가 종료되었습니다", null);
        }
    }
}
