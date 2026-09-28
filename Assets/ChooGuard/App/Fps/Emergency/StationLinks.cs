using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>A navmesh link people ride instead of walking (escalator belt, elevator car).</summary>
    public interface IStationLink
    {
        string Label { get; }
        /// <summary>Moves <paramref name="body"/> from the link start to its end. Must finish at the end position.</summary>
        IEnumerator Traverse(PersonBody body, OffMeshLinkData data);
    }

    /// <summary>What the staff member can see right now. Spawning, vanishing into elevators and toilets wait until unseen.</summary>
    public static class PlayerView
    {
        public static Camera Camera;
        private static readonly RaycastHit[] hits = new RaycastHit[8];

        public static bool Sees(Vector3 point, float range)
        {
            if (Camera == null) return false;
            var eye = Camera.transform.position;
            var to = point + Vector3.up - eye;
            float distance = to.magnitude;
            if (distance > range) return false;
            if (distance < 3f) return true;
            float halfWidth = Mathf.Atan(Mathf.Tan(Camera.fieldOfView * .5f * Mathf.Deg2Rad) * Camera.aspect) * Mathf.Rad2Deg;
            if (Vector3.Angle(Camera.transform.forward, to) > halfWidth + 8) return false;
            int count = Physics.RaycastNonAlloc(eye, to / distance, hits, distance - .5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].collider.GetComponentInParent<PersonBody>() == null) return false;
            return true;
        }
    }

    /// <summary>
    /// One escalator belt. People step on at the boarding landing, stand at belt speed (0.5 m/s, 승강기 안전기준 별표 24)
    /// about two steps apart, and walk off at the other landing. A stopped belt is walked like stairs; a closed one, or one
    /// whose entry flap gate is shut, is not offered to path finding. Link traversal follows Unity NavMeshComponents
    /// AgentLinkMover (MIT, 2016).
    /// </summary>
    public sealed class Escalator : MonoBehaviour, IStationLink
    {
        public const float BeltSpeed = .5f, Spacing = 1.1f, StoppedWalk = .5f;

        private sealed class Rider { public PersonBody Body; public float S = -1; public bool Fallen; }

        public StationPoints.EscalatorEntry Entry { get; private set; }
        public string Label => Entry.label;
        public bool Running { get; private set; } = true;
        public bool Closed { get; private set; }
        /// <summary>The flap gate at the boarding landing is shut (it follows a stopped belt): nobody is routed onto it.</summary>
        public bool EntryBarred { get; private set; }
        public int RiderCount => riders.Count;
        public Vector3 Middle => PointAt(length * .5f);
        public Vector3 Start => Entry.path[0];
        public Vector3 End => Entry.path[Entry.path.Length - 1];

        private readonly List<Rider> riders = new List<Rider>();
        private NavMeshLinkInstance link;
        private float[] distance;
        private float length;

        public void Setup(StationPoints.EscalatorEntry entry)
        {
            Entry = entry;
            distance = new float[entry.path.Length];
            for (int i = 1; i < entry.path.Length; i++) distance[i] = distance[i - 1] + Vector3.Distance(entry.path[i - 1], entry.path[i]);
            length = distance[distance.Length - 1];
            AddLink();
        }

        private void AddLink()
        {
            link = NavMesh.AddLink(new NavMeshLinkData { startPosition = Start, endPosition = End, width = 0, bidirectional = false, area = 3, costModifier = -1 });
            if (NavMesh.IsLinkValid(link)) NavMesh.SetLinkOwner(link, this);
        }

        private void OnDestroy()
        {
            if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
        }

        /// <summary>Why the steps stand still ("" while running): a stop button, the quake sensor, a staff closure.</summary>
        public string StoppedBy { get; private set; } = "";

        /// <summary>Emergency stop (earthquake sensor, someone pressed the stop button). People on it walk the rest.</summary>
        public void Stop(string cause = "비상정지 버튼")
        {
            if (Running) StoppedBy = cause;
            Running = false;
        }

        /// <summary>
        /// Restart with the key switch at the landing (research E2) — only with the steps clear and nobody fallen; a quake stop
        /// waits for inspection and a closed escalator stays closed.
        /// </summary>
        public bool Restart(out string reason)
        {
            reason = null;
            if (Running) { reason = "운행 중"; return false; }
            if (Closed) { reason = "이용 통제 중이라 재가동하지 않습니다"; return false; }
            if (StoppedBy == "지진 감지") { reason = "지진 감지로 정지 · 점검 전에는 재가동하지 않습니다"; return false; }
            riders.RemoveAll(r => r.Body == null);
            foreach (var r in riders) if (r.Fallen) { reason = "넘어진 승객이 있어 재가동할 수 없습니다"; return false; }
            if (riders.Count > 0) { reason = "디딤판에 사람이 있습니다 · 모두 내린 뒤 재가동"; return false; }
            Running = true;
            StoppedBy = "";
            return true;
        }

        /// <summary>Staff closes the escalator: path finding stops using it; people already on it finish.</summary>
        public void Close()
        {
            Closed = true;
            if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
        }

        /// <summary>
        /// The entry flap gate at the boarding landing shut or opened again. While shut the belt is not offered to path finding
        /// (unlike an ungated stopped belt, which people walk like stairs); people already on it finish.
        /// </summary>
        public void SetEntryBarred(bool barred)
        {
            if (barred == EntryBarred) return;
            EntryBarred = barred;
            if (barred) { if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link); }
            else if (!Closed && !NavMesh.IsLinkValid(link)) AddLink();
        }

        /// <summary>The red stop buttons at both landings (right-hand newel, 0.9 m), pressable by the staff member.</summary>
        public void AddStopButtons()
        {
            var start = Horizontal(Entry.path[1] - Entry.path[0]);
            var end = Horizontal(Entry.path[Entry.path.Length - 1] - Entry.path[Entry.path.Length - 2]);
            EscalatorStopButton.Build(this, Entry.path[0] - start * .25f, start, -start);
            EscalatorStopButton.Build(this, Entry.path[Entry.path.Length - 1] + end * .25f, end, end);
        }

        private static Vector3 Horizontal(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        public Vector3 PointAt(float s)
        {
            s = Mathf.Clamp(s, 0, length);
            for (int i = 1; i < distance.Length; i++)
                if (s <= distance[i]) return Vector3.Lerp(Entry.path[i - 1], Entry.path[i], (s - distance[i - 1]) / Mathf.Max(1e-4f, distance[i] - distance[i - 1]));
            return End;
        }

        /// <summary>Distance from <paramref name="point"/> to the belt line.</summary>
        public float DistanceTo(Vector3 point)
        {
            float best = float.PositiveInfinity;
            for (int i = 1; i < Entry.path.Length; i++)
            {
                var a = Entry.path[i - 1]; var b = Entry.path[i];
                var ab = b - a;
                float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector3.Distance(point, a + ab * t));
            }
            return best;
        }

        /// <summary>A rider fell on the steps: they stay where they are; people behind them stop too.</summary>
        public bool Fall(PersonBody body)
        {
            foreach (var r in riders) if (r.Body == body && r.S >= 0) { r.Fallen = true; return true; }
            return false;
        }

        /// <summary>The rider right behind <paramref name="body"/> on the belt, if any.</summary>
        public PersonBody Behind(PersonBody body)
        {
            int index = riders.FindIndex(r => r.Body == body);
            return index >= 0 && index + 1 < riders.Count && riders[index + 1].S >= 0 ? riders[index + 1].Body : null;
        }

        /// <summary>Whether <paramref name="body"/> is standing on the moving belt now.</summary>
        public bool Carries(PersonBody body)
        {
            foreach (var r in riders) if (r.Body == body && r.S >= distance[1]) return true;
            return false;
        }

        public IEnumerable<PersonBody> Bodies()
        {
            foreach (var r in riders) if (r.Body != null) yield return r.Body;
        }

        public IEnumerator Traverse(PersonBody body, OffMeshLinkData data)
        {
            var me = new Rider { Body = body };
            riders.Add(me);
            try
            {
                float boardS = distance[1], leaveS = distance[distance.Length - 2];
                var forward = Entry.path[1] - Entry.path[0];
                // 앞사람이 두 계단쯤 올라갈 때까지 계단참에서 기다린다.
                while (Ahead(me) is Rider waitFor && (waitFor.S < 0 || waitFor.S < boardS + Spacing))
                {
                    body.Ride(body.transform.position, forward);
                    yield return null;
                }
                yield return body.WalkLine(Entry.path[1], Mathf.Min(body.WalkSpeed, 1.2f));
                me.S = boardS;
                while (me.S < leaveS)
                {
                    if (me.Fallen) { body.transform.position = PointAt(me.S); yield return null; continue; }
                    float speed = Running ? BeltSpeed : StoppedWalk;
                    float next = me.S + speed * Time.deltaTime;
                    // 앞사람을 앞질러 가지 않는다(정지한 벨트를 걸어서 가도 앞사람 뒤에 붙는다).
                    if (Ahead(me) is Rider leader && leader.S > me.S) next = Mathf.Min(next, leader.S - Spacing);
                    next = Mathf.Max(me.S, next);
                    var position = PointAt(next);
                    var direction = PointAt(next + .3f) - position;
                    if (Running) body.Ride(position, direction);
                    else { body.transform.position = position; body.Move(direction, next > me.S ? speed : 0); }
                    me.S = next;
                    yield return null;
                }
                yield return body.WalkLine(End, body.WalkSpeed);
            }
            finally { riders.Remove(me); }
        }

        private Rider Ahead(Rider me)
        {
            // 사라진 사람(열차 출발·퇴장으로 지워진 몸)은 줄에서 뺀다.
            riders.RemoveAll(r => r.Body == null);
            int index = riders.IndexOf(me);
            return index > 0 ? riders[index - 1] : null;
        }
    }

    /// <summary>
    /// One elevator of the station data (a stop per floor). Where the twin has a working car — kit landing doors with a cabin
    /// behind them (<see cref="Facilities.ElevatorCar"/>) — riders call it, walk in when the doors open, stand in the car among
    /// whoever else is aboard (the staff member included), ride and walk out. Elsewhere the doors cannot open, so people enter
    /// and leave the car only while the staff member is not looking at that door (height at 1 m/s plus door time).
    /// </summary>
    public sealed class Elevator : MonoBehaviour, IStationLink
    {
        public const float Speed = 1f, DoorSeconds = 5f;
        public const int Capacity = 8;

        public StationPoints.ElevatorEntry Entry { get; private set; }
        public string Label => Entry.label;
        public bool Running { get; private set; } = true;
        public readonly List<PersonBody> Inside = new List<PersonBody>();
        public int Waiting { get; private set; }

        private readonly List<NavMeshLinkInstance> links = new List<NavMeshLinkInstance>();
        private float carFreeAt, carY, lookAgainAt;
        private Facilities.ElevatorCar car;
        private int[] landing;

        public void Setup(StationPoints.ElevatorEntry entry)
        {
            Entry = entry;
            carY = entry.stops[0].door.y;
            for (int i = 0; i < entry.stops.Length; i++)
                for (int j = i + 1; j < entry.stops.Length; j++)
                {
                    var link = NavMesh.AddLink(new NavMeshLinkData { startPosition = entry.stops[i].door, endPosition = entry.stops[j].door, width = 0, bidirectional = true, area = 4, costModifier = -1 });
                    if (NavMesh.IsLinkValid(link)) { NavMesh.SetLinkOwner(link, this); links.Add(link); }
                }
        }

        private void OnDestroy()
        {
            foreach (var link in links) if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
        }

        /// <summary>The twin's car serving every stop of this elevator (landing doors within 2.5 m of each stop), if any.</summary>
        public Facilities.ElevatorCar Car
        {
            get
            {
                if (car != null || Time.time < lookAgainAt) return car;
                lookAgainAt = Time.time + 5;
                foreach (var candidate in Facilities.ElevatorCar.All)
                {
                    var map = new int[Entry.stops.Length];
                    bool every = true;
                    for (int i = 0; i < map.Length && every; i++) every = (map[i] = candidate.LandingNear(Entry.stops[i].door)) >= 0;
                    if (!every) continue;
                    car = candidate;
                    landing = map;
                    if (!Running) car.Stop();
                    break;
                }
                return car;
            }
        }

        /// <summary>Earthquake or power loss: the car stops where it is; nobody enters.</summary>
        public void Stop()
        {
            Running = false;
            var working = Car;
            if (working != null) working.Stop();
        }

        public Vector3 Door(int stop) => Entry.stops[stop].door;

        private int Nearest(Vector3 p)
        {
            int best = 0; float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < Entry.stops.Length; i++)
            {
                float d = Vector3.Distance(Entry.stops[i].door, p);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        private Vector3 IntoShaft(int stop) => Quaternion.Euler(0, Entry.stops[stop].yaw, 0) * Vector3.forward;

        public IEnumerator Traverse(PersonBody body, OffMeshLinkData data)
        {
            int a = Nearest(data.startPos), b = Nearest(data.endPos);
            int from = Vector3.Distance(body.transform.position, Entry.stops[a].door) <= Vector3.Distance(body.transform.position, Entry.stops[b].door) ? a : b;
            int to = from == a ? b : a;
            if (Car != null) yield return RideCar(body, from, to);
            else yield return RideUnseen(body, from, to);
        }

        /// <summary>Waits beside the landing doors, boards when they stand open, rides among the others and walks out.</summary>
        private IEnumerator RideCar(PersonBody body, int from, int to)
        {
            int lf = landing[from], lt = landing[to];
            var into = IntoShaft(from);
            var side = new Vector3(into.z, 0, -into.x);
            Waiting++;
            Facilities.ElevatorCar.Rider rider = null;
            try
            {
                // 문 앞에서 조금 비켜 서서 기다린다(내리는 사람이 먼저).
                var spot = Entry.stops[from].door - into * (.6f + Random.value * .5f) + side * Random.Range(-.9f, .9f);
                yield return body.WalkLine(StationWorld.OnNavMesh(spot, .6f), Mathf.Min(body.WalkSpeed, 1.1f));
                while (rider == null)
                {
                    if (car.Running && car.DoorsOpenAt(lf) && !car.Full) { rider = car.Board(body, lf, lt); break; }
                    if (car.Running && !car.DoorsOpenAt(lf)) car.Call(lf);
                    var look = car.Threshold(lf) - body.transform.position;
                    body.Ride(body.transform.position, look);
                    yield return null;
                }
            }
            finally { Waiting--; }
            Inside.Add(body);
            try
            {
                yield return body.WalkLine(car.Threshold(lf, -.35f), 1f);
                yield return body.WalkLine(car.Threshold(lf, .4f), 1f);
                yield return body.WalkLine(car.SpotWorld(rider), .9f);
                rider.Walking = false;
                // 목적 층에서 문이 다 열릴 때까지 자리에 선다(가는 동안 카가 사람을 옮긴다).
                while (!(rider.Cabin == lt && car.DoorsOpenAt(lt)))
                {
                    body.Ride(car.SpotWorld(rider), car.Facing(rider));
                    yield return null;
                }
                rider.Walking = true;
                yield return body.WalkLine(car.Threshold(lt, .4f), 1f);
                yield return body.WalkLine(car.Threshold(lt, -.45f), 1f);
            }
            finally
            {
                Inside.Remove(body);
                car.Alight(rider);
            }
            yield return body.WalkLine(Entry.stops[to].door, body.WalkSpeed);
        }

        /// <summary>No working car in the twin: the doors cannot open, so people step in and out only while unseen.</summary>
        private IEnumerator RideUnseen(PersonBody body, int from, int to)
        {
            var door = Entry.stops[from].door;
            var into = IntoShaft(from);
            var side = new Vector3(into.z, 0, -into.x);
            Waiting++;
            // 문 앞에서 조금 비켜 서서 기다린다.
            var spot = door - into * (.6f + Random.value * .5f) + side * Random.Range(-.9f, .9f);
            yield return body.WalkLine(StationWorld.OnNavMesh(spot, .6f), Mathf.Min(body.WalkSpeed, 1.1f));
            // 차는 한 대: 부른 층까지 오는 시간, 문, 가는 시간을 차례로 쓴다.
            float arrive = Mathf.Max(Time.time, carFreeAt) + Mathf.Abs(carY - door.y) / Speed + DoorSeconds * .5f;
            float travel = Mathf.Abs(Entry.stops[to].door.y - door.y) / Speed + DoorSeconds;
            carFreeAt = arrive + travel;
            carY = Entry.stops[to].door.y;
            Inside.RemoveAll(x => x == null);
            while (Time.time < arrive || !Running || PlayerView.Sees(door, 18) || Inside.Count >= Capacity)
            {
                body.Ride(body.transform.position, into);
                yield return null;
            }
            Waiting--;
            yield return body.WalkLine(door, 1f);
            body.SetVisible(false);
            Inside.Add(body);
            try
            {
                var target = Entry.stops[to].door;
                body.transform.position = target + IntoShaft(to) * .7f;
                float until = Time.time + travel;
                while (Time.time < until || !Running) yield return null;
                // 내리는 문도 역무원이 보고 있지 않을 때 연다(최대 15초 기다림).
                float giveUp = Time.time + 15;
                while (PlayerView.Sees(target, 18) && Time.time < giveUp) yield return null;
            }
            finally
            {
                Inside.Remove(body);
                body.SetVisible(true);
            }
            yield return body.WalkLine(Entry.stops[to].door, body.WalkSpeed);
        }
    }
}
