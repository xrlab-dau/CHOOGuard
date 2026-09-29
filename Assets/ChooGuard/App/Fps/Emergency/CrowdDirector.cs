using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Keeps the station populated: people already inside at shift start (catching the KTX to Seoul, waiting to meet
    /// someone, visiting shops, leaving after an earlier train), new arrivals walking in from the city, the passengers
    /// who ride each arriving KTX in and get off, and those who board and ride away. While a hazard exists it staggers
    /// perception ticks and relays social cues (people leaving, warnings, staff instructions, announcements).
    /// </summary>
    public sealed class CrowdDirector : MonoBehaviour
    {
        /// <summary>People in the station (not counting those seated in a train).</summary>
        public int Target = 110;
        /// <summary>People who ride each arriving set in (JEV 007: about 110 in the station plus the train).</summary>
        public int RidersPerTrain = 38;
        [Header("실시간 판단")]
        [Tooltip("사건 근처에 있는 승객을 JEV 가 다시 판단하는 주기(초). 역 전체가 겪는 일(정전 등)은 이 값의 세 배.")]
        public float JudgePeriodSeconds = 4f;
        [Tooltip("이 거리(m, 같은 층) 안에서 알고 있는 사건은 '근처'로 본다.")]
        public float JudgeNearMeters = 30f;

        public EmergencySession Session { get; private set; }
        public StationWorld World { get; private set; }
        public CrowdMind Mind { get; private set; }
        public FirstPersonResponder Player => Session != null ? Session.Player : null;
        public Hazard MainHazard => Session != null ? Session.Incidents.Main : null;
        public bool Shaking { get; set; }
        /// <summary>New people keep walking in unless the station is being cleared.</summary>
        public bool Arrivals { get; set; } = true;
        public Transform PeopleRoot => root;

        public readonly List<Passenger> People = new List<Passenger>();
        public int Spawned { get; private set; }
        public int LeftNormally { get; private set; }
        public int Evacuated { get; private set; }
        public int RodeAway { get; private set; }
        public readonly List<Passenger> Injured = new List<Passenger>();

        private CrowdCatalog catalog;
        private Transform root;
        private float nextArrival, perceiveBudget;
        private int perceiveCursor;

        public void Begin(EmergencySession session, StationWorld world, CrowdCatalog crowd, JevClient jev)
        {
            Session = session;
            World = world;
            catalog = crowd;
            root = new GameObject("승객").transform;
            root.SetParent(transform, false);
            Mind = new CrowdMind(this, jev);
            Mind.Metrics.Graph(world.Paths.Graph);
            world.Paths.Unreachable += Mind.Metrics.Unreachable;
            if (world.Train != null)
            {
                world.Train.Loading += FillTrain;
                world.Train.Opened += OnOpened;
                world.Train.Departed += OnDeparted;
            }
            PopulateInitial();
        }

        public int InStation
        {
            get
            {
                int count = 0;
                foreach (var person in People) if (!person.Aboard) count++;
                return count;
            }
        }

        // ── 근무 시작 인구 ─────────────────────────────────────────────────

        private void PopulateInitial()
        {
            // 근무 시작 때 열차는 서울에서 들어오는 중이다(JEV 007: 도착 먼저). 역 안에는 다음 서울행 손님, 마중, 방문객,
            // 앞 열차에서 내려 나가는 사람이 있다. 비율은 설계 가정이다.
            for (int i = 0; i < Target; i++)
            {
                float roll = (float)World.Random.NextDouble();
                var trip = roll < .56f ? Passenger.Purpose.Depart : roll < .66f ? Passenger.Purpose.Greet : roll < .88f ? Passenger.Purpose.Visit : Passenger.Purpose.Arrive;
                var (activity, place, seconds) = StartingActivity(trip);
                if (place == null) continue;
                var start = activity == Passenger.Activity.Walk
                    ? StationWorld.OnNavMesh(place.Position + new Vector3(World.Range(-6, 6), 0, World.Range(-6, 6)), 3)
                    : activity == Passenger.Activity.Browse || activity == Passenger.Activity.Meet ? SpotAt(place.Position) : place.Position;
                var person = Spawn(start, place.Kind == PointKind.Seat || place.Kind == PointKind.Chair ? place.Yaw : World.Range(0, 360), trip);
                if (person == null) { Release(place); continue; }
                person.StartAt(activity, place, seconds);
            }
        }

        private (Passenger.Activity, StationPoints.Point, float) StartingActivity(Passenger.Purpose trip)
        {
            float roll = (float)World.Random.NextDouble();
            var here = new Vector3(64, 7, -2);
            switch (trip)
            {
                case Passenger.Purpose.Depart:
                    if (roll < .38f) return (Passenger.Activity.Sit, World.ReserveSeat(here + new Vector3(World.Range(-20, 20), 0, World.Range(-5, 35))), World.Range(40, 300));
                    if (roll < .50f) return (Passenger.Activity.Stand, World.ReservePlace(PointKind.Wait, p => p.Zone == "hall2f"), World.Range(20, 120));
                    if (roll < .58f) return (Passenger.Activity.Queue, World.ReservePlace(PointKind.Counter), World.Range(10, 45));
                    if (roll < .72f) return (Passenger.Activity.Browse, World.Pick(World.Points.Of(PointKind.Shop)), World.Range(15, 70));
                    if (roll < .88f) return (Passenger.Activity.Sit, World.ReserveChair(here, 1e4f), World.Range(60, 260));
                    return (Passenger.Activity.Walk, World.ReservePlace(PointKind.Wait), 0);
                case Passenger.Purpose.Greet:
                    if (roll < .5f) return (Passenger.Activity.Meet, World.Pick(World.Points.Of(PointKind.Meet)), World.Range(60, 200));
                    return (Passenger.Activity.Sit, World.ReserveSeat(here), World.Range(40, 160));
                case Passenger.Purpose.Visit:
                    if (roll < .45f) return (Passenger.Activity.Browse, World.Pick(World.Points.Of(PointKind.Shop)), World.Range(15, 80));
                    if (roll < .8f) return (Passenger.Activity.Sit, World.ReserveChair(here, 1e4f), World.Range(60, 260));
                    return (Passenger.Activity.Walk, World.ReservePlace(PointKind.Wait), 0);
                default:
                    return (Passenger.Activity.Walk, World.ReservePlace(PointKind.Wait, p => p.Zone == "hall2f" || p.Zone == "northdeck" || p.Zone == "main2f"), 0);
            }
        }

        private void Release(StationPoints.Point place)
        {
            if (place.Kind == PointKind.Seat || place.Kind == PointKind.Chair) World.ReleaseSeat(place); else World.ReleasePlace(place);
        }

        private Passenger Spawn(Vector3 position, float yaw, Passenger.Purpose trip)
        {
            bool female = World.Chance(.5f);
            var pool = female ? catalog.FemalePassengers : catalog.MalePassengers;
            if (pool == null || pool.Length == 0) return null;
            var prefab = pool[World.Random.Next(pool.Length)];
            var go = Instantiate(prefab, position, Quaternion.Euler(0, yaw, 0), root);
            go.name = "승객 " + (Spawned + 1);
            var body = go.GetComponent<PersonBody>();
            if (!body.Agent.isOnNavMesh) body.Agent.Warp(position);
            if (!body.Agent.isOnNavMesh) { Destroy(go); return null; }
            // 옷차림이 같은 사람이 겹쳐 보이지 않도록 걷는 모습에 약간의 차이를 둔다.
            body.Animator.speed = World.Range(.92f, 1.08f);
            body.Agent.avoidancePriority = 30 + World.Random.Next(40);
            var person = go.AddComponent<Passenger>();
            Spawned++;
            person.Setup(this, Spawned, trip, World.Random);
            if (trip == Passenger.Purpose.Depart) Book(person);
            People.Add(person);
            return person;
        }

        /// <summary>Books a departing passenger on the next boarding (or the one after, if it is about to close) in a random car.</summary>
        private void Book(Passenger person)
        {
            var train = World.Train;
            var served = train != null ? train.ServedCars().ToList() : null;
            if (served == null || served.Count == 0) return;
            int service = train.NextBoardingService;
            if (train.Stage == TrainService.Phase.Closing || train.BoardingOpen && train.SecondsToDeparture() < 90) service++;
            person.Book(service, served[World.Random.Next(served.Count)].Number);
        }

        // ── 열차 ─────────────────────────────────────────────────────────────

        private void FillTrain(TrainService train)
        {
            int riders = Mathf.Min(RidersPerTrain, train.ServedCars().Sum(c => c.Seats.Count));
            var greeters = People.Where(p => p.Trip == Passenger.Purpose.Greet && p.Partner == null && !p.Met).ToList();
            for (int i = 0; i < riders; i++)
            {
                var seat = train.FreeSeat(null, World.Random);
                if (seat == null) break;
                // 에이전트는 걷는 면 위에서 만들어야 한다: 정차 위치의 문 앞 승강장에서 만든 뒤 곧바로 멀리 있는 열차 좌석에 앉힌다.
                var person = Spawn(seat.Car.DoorOutside, seat.Entry.yaw, Passenger.Purpose.Arrive);
                if (person == null) continue;
                person.RideIn(seat);
                // 마중 나온 사람 일부가 이 열차의 승객을 기다린다.
                if (greeters.Count > 0 && World.Chance(.7f))
                {
                    var greeter = greeters[World.Random.Next(greeters.Count)];
                    greeters.Remove(greeter);
                    greeter.Partner = person;
                    person.Partner = greeter;
                }
            }
        }

        private void OnOpened(TrainService train)
        {
            // 문 가까운 줄부터 차례로 일어난다.
            foreach (var person in People.Where(p => p.Current == Passenger.Activity.InTrain).ToList())
            {
                var seat = person.TrainSeat;
                float doorDistance = Vector3.Distance(seat.Entry.aisle, seat.Car.DoorInside);
                StartCoroutine(AlightLater(person, 1.2f + doorDistance * .9f + World.Range(0, 3.5f)));
            }
        }

        private IEnumerator AlightLater(Passenger person, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (person != null && person.Current == Passenger.Activity.InTrain) person.BeginAlight();
        }

        private void OnDeparted(TrainService train)
        {
            foreach (var person in train.Aboard().ToList())
            {
                if (person == null) continue;
                RodeAway++;
                People.Remove(person);
                Injured.Remove(person);
                Destroy(person.gameObject);
            }
        }

        // ── 사람 목록 ────────────────────────────────────────────────────────

        public void Leave(Passenger person, bool evacuated)
        {
            if (evacuated) Evacuated++; else LeftNormally++;
            People.Remove(person);
            Injured.Remove(person);
            Destroy(person.gameObject);
        }

        public void OnInjured(Passenger person, string cause)
        {
            if (!Injured.Contains(person)) Injured.Add(person);
            Session.Incidents.OnPassengerInjured(person, cause);
        }

        public int CountNear(Vector3 position, float radius, Passenger.Activity? activity)
        {
            int count = 0;
            float r2 = radius * radius;
            foreach (var person in People)
            {
                if (activity.HasValue && person.Current != activity.Value) continue;
                if ((person.transform.position - position).sqrMagnitude < r2) count++;
            }
            return count;
        }

        private static readonly float[] SpotTurns = { 0, 30, -30, 60, -60, 90, -90, 120, -120, 150, -150, 180 };
        private const float ClaimSeconds = 6f;
        // 나눠 준 자리: 받은 사람이 아직 걸어가는 중(열차 문턱을 넘는 중처럼 길 찾기 목적지가 없는 때 포함)이라도 다음 사람에게 다시 주지 않는다.
        private readonly List<(Vector3 At, Passenger Who, float Until)> claims = new List<(Vector3, Passenger, float)>();

        /// <summary>
        /// Where one person stands at a place several people share (meeting point, toilet door, shop front, beside a casualty,
        /// a city entrance): 0.7–1.8 m from <paramref name="point"/> on the same floor, reachable from it without crossing an
        /// edge, as far as possible from everyone else standing or heading there. Sending everyone to the one point stacked the
        /// agents and the crowd solver popped them half a metre apart. <paramref name="facing"/> (optional) is the side tried
        /// first, so a helper stops on the side they came from.
        /// </summary>
        public Vector3 SpotAt(Vector3 point, Passenger who = null, Vector3 facing = default)
        {
            point = StationWorld.OnNavMesh(point, 2f);
            facing.y = 0;
            float first = facing.sqrMagnitude > .01f ? Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg : World.Range(0, 360);
            var best = point;
            float bestClearance = -1;
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = .7f + ring * .55f;
                foreach (var turn in SpotTurns)
                {
                    float angle = (first + turn + ring * 15f) * Mathf.Deg2Rad;
                    var candidate = point + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(candidate, out var hit, .35f, UnityEngine.AI.NavMesh.AllAreas) || Mathf.Abs(hit.position.y - point.y) > .35f) continue;
                    if (UnityEngine.AI.NavMesh.Raycast(point, hit.position, out _, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    float clearance = Clearance(hit.position, who);
                    if (clearance >= 1.1f) return Claim(hit.position, who);
                    if (clearance > bestClearance) { bestClearance = clearance; best = hit.position; }
                }
            }
            return Claim(best, who);
        }

        /// <summary>Holds <paramref name="spot"/> for <paramref name="who"/> for a few seconds so nobody else is sent there meanwhile.</summary>
        public Vector3 Claim(Vector3 spot, Passenger who)
        {
            claims.RemoveAll(c => c.Until < Time.time || c.Who != null && c.Who == who);
            claims.Add((spot, who, Time.time + ClaimSeconds));
            return spot;
        }

        /// <summary>True when another person stands, or is walking to, within <paramref name="radius"/> of <paramref name="spot"/>.</summary>
        public bool Occupied(Vector3 spot, float radius, Passenger who = null) => Clearance(spot, who) < radius;

        /// <summary>
        /// Distance from <paramref name="spot"/> to the nearest other person on that floor, to where they are walking, or to a spot
        /// recently handed to someone else.
        /// </summary>
        private float Clearance(Vector3 spot, Passenger who)
        {
            float nearest = float.PositiveInfinity;
            foreach (var person in People)
            {
                if (person == null || person == who) continue;
                var at = person.transform.position;
                if (Mathf.Abs(at.y - spot.y) > 2f) continue;
                nearest = Mathf.Min(nearest, (at - spot).sqrMagnitude);
                var body = person.Body;
                if (body.EnRoute) nearest = Mathf.Min(nearest, (body.Goal - spot).sqrMagnitude);
            }
            foreach (var claim in claims)
                if (claim.Until >= Time.time && (claim.Who == null || claim.Who != who) && Mathf.Abs(claim.At.y - spot.y) < 2f)
                    nearest = Mathf.Min(nearest, (claim.At - spot).sqrMagnitude);
            return Mathf.Sqrt(nearest);
        }

        /// <summary>
        /// People around <paramref name="position"/> pick up on a cue without seeing the cause. A bell (radius of hundreds of
        /// metres) is heard on every floor of the building: NFTC 203 2.5.1.2 limits the alarm to the fire floor and the four
        /// above it only for buildings of 11 storeys or more (16 for apartments), so a three-storey station rings everywhere.
        /// Inside a KTX car it reaches only people near an open door. People running or shouting are noticed on the same floor only.
        /// </summary>
        public void Alert(Vector3 position, float radius, Hazard hazard, Passenger source, string cue)
        {
            if (hazard == null) return;
            float r2 = radius * radius;
            bool stationWide = radius >= 100f;
            var train = World.Train;
            foreach (var person in People)
            {
                if (person == source || person.Noticed.Contains(hazard)) continue;
                if (stationWide && person.Aboard && !HearsThroughDoor(person, train)) continue;
                var d = person.transform.position - position;
                if (!stationWide && Mathf.Abs(d.y) > 4) continue;
                if (d.sqrMagnitude < r2) person.Notice(hazard, true, cue);
            }
        }

        /// <summary>The train's windows and doors shut the bell out: it is heard only within 6 m of a car door that stands open.</summary>
        private static bool HearsThroughDoor(Passenger person, TrainService train) =>
            train != null && person.TrainSeat != null && train.DoorsOpen > .9f &&
            Vector3.Distance(person.transform.position, train.World(person.TrainSeat.Car.DoorInside)) < 6f;

        private void OnDestroy()
        {
            // 근무가 끝나면 판단 측정 요약을 기록에 남긴다(승객 판단 기록 crowd-*.jsonl 의 마지막 줄).
            if (Mind == null) return;
            if (World != null && World.Paths != null) World.Paths.Unreachable -= Mind.Metrics.Unreachable;
            Mind.Metrics.Record(new Newtonsoft.Json.Linq.JObject { ["summary"] = Mind.Metrics.Summary(Session != null ? Session.Jev : null) });
            Mind.Metrics.Flush(true);
        }

        /// <summary>Staff tells the people around a passenger which way to leave. Returns how many were told.</summary>
        public int InstructAround(Vector3 position, Vector3 staff)
        {
            var hazard = MainHazard;
            int told = 0;
            foreach (var person in People.ToArray())
            {
                if (person.Current == Passenger.Activity.Evacuate || person.Hurt) continue;
                var d = person.transform.position - position;
                if (Mathf.Abs(d.y) > 3 || d.sqrMagnitude > 25) continue;
                var danger = hazard != null && hazard.Localized ? hazard.Position : (Vector3?)null;
                person.Instruct(SafeExitFor(person.transform.position, danger, hazard != null ? hazard.Clearance : 0), true);
                told++;
            }
            Session.Log.AddGuided(told);
            return told;
        }

        /// <summary>
        /// <see cref="StationWorld.SafeExit"/> for one person: the exit with the cheapest walk that keeps clear of the danger,
        /// read from a route tree the people standing at the same waypoint share (a search per waypoint, microseconds), so
        /// telling a whole hall to leave at once runs no path query at all.
        /// </summary>
        public StationPoints.Point SafeExitFor(Vector3 from, Vector3? avoid, float clearance, StationPoints.Point except = null) =>
            World.SafeExit(from, avoid, clearance, except);

        /// <summary>Public announcement: everyone within earshot (the whole station, or near <paramref name="zone"/>) is asked to leave.</summary>
        public int Announce(Vector3? zone = null, float radius = 1e4f, bool stopArrivals = true)
        {
            var hazard = MainHazard;
            var danger = hazard != null && hazard.Localized ? hazard.Position : (Vector3?)null;
            int heard = 0;
            foreach (var person in People.ToArray())
            {
                if (person.Current == Passenger.Activity.Evacuate || person.Hurt) continue;
                if (zone.HasValue && Vector3.Distance(person.transform.position, zone.Value) > radius) continue;
                person.Instruct(SafeExitFor(person.transform.position, danger, hazard != null ? hazard.Clearance : 0), false);
                heard++;
            }
            if (stopArrivals) Arrivals = false;
            return heard;
        }

        private void Update()
        {
            if (World == null) return;
            if (Arrivals && InStation < Target && Time.time > nextArrival)
            {
                // 사건 중에는 들어오는 사람이 줄어든다(밖에서도 보이고 들린다).
                bool incident = MainHazard != null;
                nextArrival = Time.time + World.Range(1.2f, 3.2f) * (incident ? 3 : 1);
                var entrance = World.RandomExit();
                if (!PlayerView.Sees(entrance.Position, 60))
                {
                    float roll = (float)World.Random.NextDouble();
                    var trip = roll < .62f ? Passenger.Purpose.Depart : roll < .74f ? Passenger.Purpose.Greet : Passenger.Purpose.Visit;
                    var person = Spawn(SpotAt(entrance.Position), World.Range(0, 360), trip);
                    if (person != null) person.StartAt(Passenger.Activity.Walk, null, 0);
                }
            }

            if (HazardRegistry.Active.Count > 0 && People.Count > 0)
            {
                // 한 사람당 초당 두 번 정도 지각한다. 프레임마다 일부만 돌린다.
                perceiveBudget += People.Count * Time.deltaTime * 2f;
                int count = Mathf.Min(People.Count, Mathf.FloorToInt(perceiveBudget));
                perceiveBudget -= count;
                for (int i = 0; i < count; i++)
                {
                    perceiveCursor = (perceiveCursor + 1) % People.Count;
                    People[perceiveCursor].Perceive(.5f);
                }
            }
            Mind.Tick();
            // 길 안내: 걷는 사람의 길 요청을 프레임당 1 ms 안에서 처리한다(승객 판단의 Tick 과 합쳐 군중 비용으로 잰다).
            float pathMs = World.Paths.Tick(out int served);
            Mind.Metrics.Frame(pathMs, served, World.Paths.Queued, HazardRegistry.Active.Count > 0);
        }
    }
}
