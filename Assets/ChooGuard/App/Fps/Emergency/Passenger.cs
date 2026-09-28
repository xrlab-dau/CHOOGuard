using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// One person in the station with a trip of their own: catching the KTX to Seoul, arriving on it, meeting someone,
    /// or just visiting the shops. What to do next is decided through <see cref="CrowdMind"/> (JEV when connected,
    /// asked ahead of time so nobody stops to wait; local rules otherwise). Movement uses the whole-station navmesh with
    /// escalator and elevator rides; inside the train the body walks the aisle in the train's own frame.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PersonBody))]
    public sealed class Passenger : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public enum Activity { Walk, Queue, Browse, Sit, Stand, Leave, Toilet, PlatformWait, Board, InTrain, Alight, Meet, Deciding, Watch, MoveAway, Evacuate, Report, TakeCover, Injured }
        public enum Purpose { Depart, Arrive, Greet, Visit }

        public PersonBody Body { get; private set; }
        public CrowdDirector Crowd { get; private set; }
        public int Number { get; private set; }
        public Activity Current { get; private set; }
        public Hazard Focus { get; private set; }
        public bool Instructed { get; private set; }
        public bool Hurt { get; private set; }
        public float SmokeSeconds { get; private set; }
        /// <summary>The person who left a bag behind or means harm. Never reacts to hazards.</summary>
        public bool Hostile { get; private set; }

        // ── 여정(프로필) ──
        public Purpose Trip { get; private set; }
        /// <summary>Departing: the car on the ticket. Arriving: the car ridden.</summary>
        public int Car { get; private set; }
        /// <summary>Departing: which arrival cycle's return trip they are booked on (TrainService.Service).</summary>
        public int Service { get; private set; }
        public bool HasTicket { get; set; }
        /// <summary>0 none, 1 backpack or bag, 2 large suitcase.</summary>
        public int Luggage { get; private set; }
        public bool Elderly { get; private set; }
        public bool UsesPhone { get; private set; }
        /// <summary>Carries a power bank (lithium battery) in a bag: a candidate for the overheating transition.</summary>
        public bool CarriesPowerBank { get; private set; }
        public Passenger Partner { get; set; }
        public bool Met { get; set; }
        /// <summary>escalator, stairs, elevator or any: how this person changes floors (asked of JEV once).</summary>
        public string Route { get; private set; } = "any";
        public readonly List<string> Memory = new List<string>();
        public TrainService.Seat TrainSeat { get; private set; }
        public bool MissedTrain { get; private set; }
        public string Cue { get; private set; }
        public readonly HashSet<Hazard> Noticed = new HashSet<Hazard>();

        /// <summary>Where the passenger is right now in plain words, for JEV context and the Tab board.</summary>
        public string Doing => Describe(Current == Activity.Deciding ? before : Current) + " (" + Crowd.World.Area(transform.position) + ")";

        private Activity before = Activity.Walk, afterWalk;
        private StationPoints.Point place, exit;
        private CrowdMind.Choice choice;
        private float until, walkSpeed, nextRepath, reportStarted, stepSeconds, hiddenUntil, phoneCallEnds = -1;
        private Vector3 lookAt;
        private bool pendingStand, running, hidden, prefetched, alightQueued, helping;
        /// <summary>Gone over to help someone who collapsed or fell (see <see cref="HelpNearby"/>).</summary>
        public bool Helping => helping;
        private int reevaluations, sitTries;
        private StationWorld World => Crowd.World;
        private TrainService Train => Crowd.World.Train;

        public string DisplayName => Hurt ? "부상 승객" : "승객";

        public void Setup(CrowdDirector crowd, int number, Purpose trip, System.Random random)
        {
            Crowd = crowd;
            Number = number;
            Body = GetComponent<PersonBody>();
            Body.Home = crowd.PeopleRoot;
            Trip = trip;
            Elderly = random.NextDouble() < .14;
            Luggage = trip == Purpose.Depart || trip == Purpose.Arrive ? (random.NextDouble() < .38 ? 2 : random.NextDouble() < .6 ? 1 : 0) : random.NextDouble() < .3 ? 1 : 0;
            UsesPhone = random.NextDouble() < .6;
            CarriesPowerBank = UsesPhone && random.NextDouble() < .55;
            HasTicket = trip != Purpose.Depart || random.NextDouble() < .8;
            walkSpeed = (Elderly ? .85f : 1.15f) + (float)random.NextDouble() * .3f - (Luggage == 2 ? .1f : 0);
            Body.GoTo(transform.position, walkSpeed);
            SetRoute(Luggage == 2 || Elderly ? (random.NextDouble() < .5 ? "elevator" : "escalator") : random.NextDouble() < .6 ? "escalator" : "stairs");
        }

        /// <summary>Books a departing passenger on a return trip and a car.</summary>
        public void Book(int service, int car) { Service = service; Car = car; }

        public void SetRoute(string route)
        {
            Route = route;
            ApplyRoute();
        }

        private void ApplyRoute()
        {
            var agent = Body.Agent;
            if (!agent.isActiveAndEnabled) return;
            var route = Route;
            // 층을 바꿀 때 무엇을 탈지: 링크·계단 구역 비용으로 고른다(비용은 1 이상).
            agent.SetAreaCost(StationWorld.EscalatorArea, route == "stairs" ? 12 : route == "elevator" ? 6 : 1);
            agent.SetAreaCost(StationWorld.ElevatorArea, route == "elevator" ? 1 : route == "any" ? 8 : 25);
            agent.SetAreaCost(StationWorld.StairsArea, route == "stairs" ? 1 : route == "elevator" ? 20 : Luggage == 2 ? 6 : 3);
        }

        public void Remember(string what)
        {
            Memory.Add(what);
            if (Memory.Count > 6) Memory.RemoveAt(0);
        }

        // ── 시작 ────────────────────────────────────────────────────────────

        /// <summary>Starts the person mid-routine at their current spot (population present at shift start).</summary>
        public void StartAt(Activity activity, StationPoints.Point at, float seconds)
        {
            place = at;
            stepSeconds = seconds;
            switch (activity)
            {
                case Activity.Sit when at != null:
                    Body.PlaceSeated(at.Anchor, at.Yaw);
                    Begin(Activity.Sit, seconds);
                    return;
                case Activity.Walk:
                    // 막 들어온 사람: 다음 할 일을 먼저 묻고(보통 0.3초 안에 답) 잠깐 둘러본 뒤 움직인다.
                    Prefetch(1.2f);
                    Begin(Activity.Stand, 1.2f);
                    prefetched = true;
                    return;
                default:
                    Begin(activity, seconds);
                    if (activity == Activity.Stand || activity == Activity.Meet) Body.SetPhone(UsesPhone && World.Chance(.6f));
                    return;
            }
        }

        /// <summary>Seated inside an arriving train before it comes into view.</summary>
        public void RideIn(TrainService.Seat seat)
        {
            TrainSeat = seat;
            seat.Taken = this;
            Car = seat.Car.Number;
            Body.PlaceSeated(seat.Entry.anchor, seat.Entry.yaw, Train.Carrier, false);
            Current = Activity.InTrain;
        }

        private void Begin(Activity activity, float seconds)
        {
            Current = activity;
            until = Time.time + seconds;
            prefetched = false;
        }

        // ── 일상 판단 ─────────────────────────────────────────────────────────

        private void Decide()
        {
            ReleasePlace();
            prefetched = false;
            choice = Crowd.Mind.TakeRoutine(this);
            Execute(choice);
        }

        private void Execute(CrowdMind.Choice next)
        {
            if (next == null) { WalkTo(World.RandomExit().Position, Activity.Leave, walkSpeed); return; }
            if (next.Remember != null) Remember(next.Remember);
            stepSeconds = next.Seconds;
            switch (next.Activity)
            {
                case Activity.Leave:
                    exit = next.Place ?? World.RandomExit();
                    WalkTo(exit.Position, Activity.Leave, walkSpeed);
                    return;
                case Activity.PlatformWait:
                    place = next.Place;
                    if (place == null) { Execute(Crowd.Mind.Fallback(this)); return; }
                    WalkTo(place.Position, Activity.PlatformWait, walkSpeed);
                    return;
                case Activity.Meet:
                case Activity.Toilet:
                case Activity.Queue:
                case Activity.Browse:
                case Activity.Stand:
                case Activity.Sit:
                    place = next.Place;
                    if (place == null) { Execute(Crowd.Mind.Fallback(this)); return; }
                    // 여럿이 쓰는 곳(만남 장소·화장실 앞·가게 앞)은 저마다 빈자리에 선다(한 점에 겹쳐 서면 서로 밀려 튄다).
                    var target = next.Activity == Activity.Meet || next.Activity == Activity.Toilet || next.Activity == Activity.Browse ? Crowd.SpotAt(place.Position, this) : place.Position;
                    WalkTo(target, next.Activity, walkSpeed);
                    return;
                default:
                    Begin(Activity.Stand, next.Seconds);
                    return;
            }
        }

        private void WalkTo(Vector3 destination, Activity then, float speed)
        {
            Body.ClearPoses();
            afterWalk = then;
            Current = Activity.Walk;
            prefetched = false;
            if (!Travel(destination, speed)) { Current = then; until = Time.time + 2; }
        }

        // 승강장과 맞이방 사이 같은 긴 길은 계단·에스컬레이터 계단참을 거쳐 두 구간으로 간다(StationWorld.Via).
        private readonly List<Vector3> legs = new List<Vector3>();
        private int leg;
        private float legSpeed;

        private bool Travel(Vector3 destination, float speed)
        {
            ApplyRoute();
            legs.Clear();
            legs.AddRange(World.Via(transform.position, destination, Route));
            legs.Add(destination);
            leg = 0;
            legSpeed = speed;
            return Body.GoTo(legs[0], speed);
        }

        private bool OnLastLeg => leg >= legs.Count - 1;

        /// <summary>True once the final destination is reached; moves on to the next leg at each stop.</summary>
        private bool Travelled(float tolerance)
        {
            if (legs.Count == 0) return Body.Arrived(tolerance);
            if (!OnLastLeg)
            {
                if (Body.Arrived(1.2f)) { leg++; Body.GoTo(legs[leg], legSpeed); }
                return false;
            }
            return Body.Arrived(tolerance);
        }

        private void Arrive()
        {
            Current = afterWalk;
            until = Time.time + stepSeconds;
            prefetched = false;
            switch (Current)
            {
                case Activity.Sit:
                    // 자리 앞에 실제로 닿았을 때만 앉는다. 길이 없어 멀리서 '도착'했으면 한 번 더 걸어가 보고, 그래도 안 되면 다른 일을 한다.
                    bool seat = place != null && (place.Kind == PointKind.Seat || place.Kind == PointKind.Chair);
                    if (seat && Body.BeginSit(place.Anchor, place.Yaw)) { sitTries = 0; break; }
                    if (seat && ++sitTries < 3) { WalkTo(place.Position, Activity.Sit, walkSpeed); break; }
                    sitTries = 0;
                    Decide();
                    break;
                case Activity.Stand:
                case Activity.Meet:
                    Body.SetIdle(World.Chance(.3f) ? 1 : 0);
                    Body.SetPhone(UsesPhone && World.Chance(.5f));
                    break;
                case Activity.Browse:
                case Activity.Queue:
                    Body.SetIdle(World.Chance(.5f) ? 1 : 0);
                    if (Current == Activity.Queue) HasTicket = true;
                    break;
                case Activity.PlatformWait:
                    Body.SetPhone(UsesPhone && World.Chance(.5f));
                    break;
                case Activity.Toilet:
                    hidden = false;
                    hiddenUntil = 0;
                    break;
            }
        }

        private void ReleasePlace()
        {
            if (place == null) return;
            if (place.Kind == PointKind.Seat || place.Kind == PointKind.Chair) World.ReleaseSeat(place); else World.ReleasePlace(place);
            place = null;
        }

        private void OnDestroy()
        {
            if (Crowd == null || Crowd.World == null) return;
            ReleasePlace();
            if (TrainSeat != null && TrainSeat.Taken == this) TrainSeat.Taken = null;
            if (TrainSeat != null) { TrainSeat.Car.Movers.Remove(this); if (TrainSeat.Car.InDoor == this) TrainSeat.Car.InDoor = null; }
        }

        private void Update()
        {
            if (Crowd == null) return;
            switch (Current)
            {
                case Activity.Walk:
                    if (!prefetched && OnLastLeg && Body.OnNavMesh && !Body.Agent.pathPending && Body.SecondsLeft() < 12 && afterWalk != Activity.Leave && afterWalk != Activity.PlatformWait)
                        Prefetch(Body.SecondsLeft() + stepSeconds);
                    if (Travelled(afterWalk == Activity.Leave ? 1.5f : .5f)) Arrive();
                    break;
                case Activity.Queue:
                case Activity.Browse:
                case Activity.Stand:
                    if (place != null) Body.FaceYaw(Quaternion.Euler(0, place.Yaw, 0), 120);
                    if (!prefetched && until - Time.time < 12) Prefetch(until - Time.time);
                    if (Time.time > until) Decide();
                    break;
                case Activity.Meet:
                    UpdateMeet();
                    break;
                case Activity.Sit:
                    if (!prefetched && until - Time.time < 14) Prefetch(until - Time.time + 3);
                    if (Time.time > until && Body.Seat == PersonBody.SeatPhase.Seated) Body.BeginStand();
                    if (Body.Seat == PersonBody.SeatPhase.None && Time.time > until) Decide();
                    break;
                case Activity.Toilet:
                    UpdateToilet();
                    break;
                case Activity.Leave:
                    // 도시로 나가는 사람은 역무원 눈에 띄지 않을 때 사라진다.
                    if (!PlayerView.Sees(transform.position, 45)) Crowd.Leave(this, Focus != null);
                    break;
                case Activity.PlatformWait:
                    UpdatePlatformWait();
                    break;
                case Activity.Deciding:
                case Activity.Watch:
                    if (Body.Seat == PersonBody.SeatPhase.None && !Body.Scripted) Body.Face(lookAt, 240);
                    if (Current == Activity.Watch && Time.time > until) AfterWatching();
                    break;
                case Activity.MoveAway:
                    if (!Body.Arrived(.8f)) break;
                    Current = Activity.Watch;
                    until = Time.time + World.Range(15, 40);
                    if (helping && Focus != null) { lookAt = Focus.Position; Body.SetCrouch(true); }
                    else Body.SetFilm(World.Chance(.35f));
                    break;
                case Activity.Evacuate:
                    if (pendingStand) { if (Body.Seat == PersonBody.SeatPhase.None && !Body.Scripted) { pendingStand = false; GoToExit(); } break; }
                    if (Travelled(1.5f) && !PlayerView.Sees(transform.position, 45)) Crowd.Leave(this, true);
                    break;
                case Activity.Report:
                    UpdateReport();
                    break;
                case Activity.TakeCover:
                    if (Time.time > until && !Crowd.Shaking)
                    {
                        Body.SetCrouch(false);
                        Body.SetIdle(0);
                        before = Current;
                        Current = Activity.Deciding;
                        Crowd.Mind.AfterQuake(this);
                    }
                    break;
            }
        }

        private void Prefetch(float secondsLeft)
        {
            prefetched = true;
            Crowd.Mind.Prefetch(this, secondsLeft);
        }

        private void UpdateToilet()
        {
            if (!hidden)
            {
                if (hiddenUntil > 0)
                {
                    // 나온 뒤: 다음 일을 한다.
                    Decide();
                    return;
                }
                if (!PlayerView.Sees(transform.position, 30))
                {
                    hidden = true;
                    Body.SetVisible(false);
                    Body.Stop();
                    hiddenUntil = Time.time + World.Range(50, 140);
                }
                return;
            }
            if (!prefetched && hiddenUntil - Time.time < 12) Prefetch(hiddenUntil - Time.time);
            if (Time.time > hiddenUntil && !PlayerView.Sees(transform.position, 30))
            {
                hidden = false;
                Body.SetVisible(true);
                Remember("used the toilet");
            }
        }

        private void UpdateMeet()
        {
            if (!prefetched && until - Time.time < 12) Prefetch(until - Time.time);
            if (Partner != null && Partner.Current == Activity.Walk && Vector3.Distance(Partner.transform.position, transform.position) < 2.5f)
            {
                Met = true;
                Partner.Met = true;
                Body.Wave();
                Remember("met the person they were waiting for");
                var together = World.RandomExit();
                Partner.LeaveWith(together);
                LeaveWith(together);
                return;
            }
            if (Trip == Purpose.Greet && Partner == null && Time.time > until) { Decide(); return; }
            if (Trip != Purpose.Greet && Time.time > until) Decide();
        }

        /// <summary>Walks to <paramref name="toward"/> and leaves (a greeter and the passenger they met).</summary>
        public void LeaveWith(StationPoints.Point toward)
        {
            ReleasePlace();
            exit = toward;
            WalkTo(toward.Position, Activity.Leave, walkSpeed * .95f);
        }

        // ── 열차 ─────────────────────────────────────────────────────────────

        private void UpdatePlatformWait()
        {
            if (place != null) Body.FaceYaw(Quaternion.Euler(0, place.Yaw, 0), 120);
            if (Train == null) return;
            if (Train.BoardingOpen && Train.Service == Service) { BeginBoarding(); return; }
            if (Train.Service > Service || (Train.Service == Service && Train.Stage >= TrainService.Phase.Departing)) MissTrain();
        }

        private void MissTrain()
        {
            if (TrainSeat != null) { TrainSeat.Taken = null; TrainSeat = null; }
            MissedTrain = true;
            Remember("missed the train to Seoul");
            Service = Train.Service + 1;
            HasTicket = false;
            Current = Activity.Stand;
            Decide();
        }

        /// <summary>Walks to the car door and steps in; then the aisle to a free seat in the booked car.</summary>
        public void BeginBoarding()
        {
            var car = Train.CarNumber(Car) ?? Train.Cars[0];
            TrainSeat = Train.FreeSeat(car, World.Random);
            if (TrainSeat == null) { Remember("found the train full"); Decide(); return; }
            TrainSeat.Taken = this;
            ReleasePlace();
            Body.ClearPoses();
            Current = Activity.Board;
            Body.GoTo(Train.World(TrainSeat.Car.DoorOutside), walkSpeed);
            StartCoroutine(StepIn());
        }

        private System.Collections.IEnumerator StepIn()
        {
            var car = TrainSeat.Car;
            float nextGo = 0;
            Vector3? waitAt = null;
            while (Current == Activity.Board)
            {
                // 문 앞에 닿기 전에 열차가 떠났다.
                if (Train.Stage != TrainService.Phase.Boarding && Train.Stage != TrainService.Phase.Closing) { car.Waiting.Remove(this); MissTrain(); yield break; }
                // 문 앞 도착은 실제 거리로 본다. 에스컬레이터에서 막 내린 사람처럼 길 없이 서 있는 것을 도착으로 보지 않는다.
                var door = Train.World(car.DoorOutside);
                var gap = transform.position - door;
                float reach = new Vector2(gap.x, gap.z).magnitude;
                bool standing = !Body.Scripted && Body.Seat == PersonBody.SeatPhase.None;
                bool atDoor = standing && reach < .8f && Mathf.Abs(gap.y) < 1f;
                // 문 앞은 한 사람씩 오른다: 온 차례대로 줄을 서고, 맨 앞사람만 문 앞으로 가며 나머지는 문 옆 빈자리에서 기다린다
                // (모두 문 한 점으로 가면 겹쳐 서서 서로 밀려 튄다).
                if (reach < 4f && Mathf.Abs(gap.y) < 1f && !car.Waiting.Contains(this)) car.Waiting.Add(this);
                car.Waiting.RemoveAll(p => p == null || p.Current != Activity.Board || p.TrainSeat == null || p.TrainSeat.Car != car);
                bool first = car.Waiting.Count > 0 && car.Waiting[0] == this;
                bool free = !HeldAtDoor && (car.InDoor == null || car.InDoor == this);
                if (atDoor && free && first) break;
                // 닫히는 문에 걸린 사람은 문 앞에 붙들려 있다(HoldAtDoor).
                if (HeldAtDoor) { yield return null; continue; }
                // 줄에 서면 곧바로 기다릴 자리로 방향을 바꾸고(문 앞까지 갔다가 물러서지 않게), 차례가 오면 문 앞으로 간다.
                var want = !car.Waiting.Contains(this) || first && free ? door : waitAt ??= Crowd.SpotAt(door, this, door - Train.World(car.DoorInside));
                if (standing && Time.time > nextGo && ((Body.Goal - want).sqrMagnitude > .04f || Body.Arrived(.35f) && (transform.position - want).sqrMagnitude > .25f))
                {
                    nextGo = Time.time + .5f;
                    Body.GoTo(want, walkSpeed);
                }
                yield return null;
            }
            car.Waiting.Remove(this);
            if (Current != Activity.Board) yield break;
            car.InDoor = this;
            car.Movers.Add(this);
            var path = new List<Vector3> { car.DoorInside };
            TrainService.AddAisle(path, car, TrainSeat.Entry.aisle, true);
            path.Add(TrainSeat.Entry.aisle);
            path.Add(TrainSeat.Entry.front);
            Body.FollowInFrame(Train.Carrier, path, Mathf.Min(walkSpeed, 1f), () =>
            {
                car.Movers.Remove(this);
                if (!Body.BeginSit(TrainSeat.Entry.anchor, TrainSeat.Entry.yaw, Train.Carrier, false)) Body.PlaceSeated(TrainSeat.Entry.anchor, TrainSeat.Entry.yaw, Train.Carrier, false);
                Current = Activity.InTrain;
                Remember("boarded KTX car " + car.Number);
            }, i => MayAdvance(car, i, path));
        }

        /// <summary>Caught by the closing door (coat, bag or arm) while boarding: stays at the door until freed.</summary>
        public bool HeldAtDoor { get; private set; }

        public void HoldAtDoor(Vector3 door)
        {
            HeldAtDoor = true;
            Body.GoTo(door, walkSpeed * .8f);
        }

        /// <summary>Freed from the door. Boards once the doors open again, or stays on the platform hurt (an arm was caught).</summary>
        public void ReleaseFromDoor(bool hurt)
        {
            if (!HeldAtDoor) return;
            HeldAtDoor = false;
            if (!hurt) return;
            if (TrainSeat != null) { TrainSeat.Taken = null; TrainSeat = null; }
            Injure("출입문에 팔이 끼였다");
        }

        private bool MayAdvance(TrainService.Car car, int index, List<Vector3> path)
        {
            if (index > 0 && car.InDoor == this) car.InDoor = null;
            var here = transform.localPosition;
            var toward = path[index] - here; toward.y = 0;
            if (toward.sqrMagnitude < 1e-4f) { blockedSince = -1; return true; }
            toward.Normalize();
            bool blocked = false;
            foreach (var other in car.Movers)
            {
                if (other == null || other == this) continue;
                var d = other.transform.localPosition - here; d.y = 0;
                // 좁은 통로에서 바로 앞사람(0.7 m 안, 가는 방향 쪽) 뒤에 선다.
                if (d.magnitude < .7f && Vector3.Dot(d, toward) > .25f) { blocked = true; break; }
            }
            if (!blocked) { blockedSince = -1; return true; }
            // 서로 길을 막아 멈추지 않게, 2초 넘게 막히면 몸을 비켜 지나간다.
            if (blockedSince < 0) blockedSince = Time.time;
            return Time.time - blockedSince > 2f;
        }

        private float blockedSince = -1;

        /// <summary>Gets up, walks the aisle to the door and steps down onto the platform.</summary>
        public void BeginAlight(bool hurry = false)
        {
            if (TrainSeat == null || Current != Activity.InTrain || alightQueued) return;
            // 좌석에서 쓰러진 사람은 스스로 내리지 않는다(구급대가 처치한 뒤 부축해 내린다).
            if (Hurt && !assisted) return;
            alightQueued = true;
            Current = Activity.Alight;
            // 통로를 걸어 나오는 동안 내린 뒤 할 일을 미리 묻는다.
            if (!hurry) Prefetch(12);
            running = hurry;
            Body.BeginStand();
            StartCoroutine(StepOut());
        }

        private System.Collections.IEnumerator StepOut()
        {
            while (Body.Seat != PersonBody.SeatPhase.None) yield return null;
            var seat = TrainSeat;
            var car = seat.Car;
            seat.Taken = null;
            car.Movers.Add(this);
            var path = new List<Vector3> { seat.Entry.aisle };
            TrainService.AddAisle(path, car, seat.Entry.aisle, false);
            path.Add(car.Entry.aisle[0]);
            path.Add(car.DoorInside);
            path.Add(car.DoorOutside);
            int doorStep = path.Count - 1;
            int landings = 0;
            bool claimed = false;
            Body.FollowInFrame(Train.Carrier, path, assisted ? .6f : running ? 1.5f : Mathf.Min(walkSpeed, 1f), () =>
            {
                car.Movers.Remove(this);
                if (car.InDoor == this) car.InDoor = null;
                Body.ReturnToNavMesh(transform.position);
                Body.GoTo(transform.position, walkSpeed);
                TrainSeat = null;
                Trip = Purpose.Arrive;
                Remember("got off KTX car " + car.Number + " at platform 5·6");
                // 처치받은 환자는 구급대와 함께 승강장에 머문다.
                if (assisted) { Current = Activity.Injured; Body.Stop(); Body.SetCrouch(true); return; }
                if (Current == Activity.Evacuate || Instructed) { Current = Activity.Evacuate; GoToExit(); return; }
                Decide();
            }, i =>
            {
                if (i == doorStep)
                {
                    if (car.InDoor != null && car.InDoor != this) return false;
                    car.InDoor = this;
                    // 내려설 곳에 앞사람이 아직 서 있거나 누가 그리 가는 중이면 그 옆 빈자리로 바꾼다. 내려서는 걸음 내내 본다:
                    // 앞사람은 이쪽이 문턱을 넘는 동안에 내려서기 때문이다. 같은 점에 내려서면 두 몸이 겹쳐 군중 계산이 반 미터씩 튕겨 낸다.
                    var landing = Train.World(path[i]);
                    if (landings < 3 && Crowd.Occupied(landing, .6f, this))
                    {
                        landings++;
                        var outside = Train.World(car.DoorOutside);
                        path[i] = Train.Carrier.InverseTransformPoint(Crowd.SpotAt(outside, this, outside - Train.World(car.DoorInside)));
                    }
                    if (!claimed) { claimed = true; Crowd.Claim(Train.World(path[i]), this); }
                }
                return MayAdvance(car, i, path);
            });
        }

        private bool assisted;

        /// <summary>Treated in a car: walked slowly off onto the platform by the paramedics, where they stay.</summary>
        public void HelpedOff()
        {
            if (TrainSeat == null || Current != Activity.InTrain) return;
            assisted = true;
            // 앞으로 늘어진 자세를 풀고 일어선다(늘어짐 전이가 일어서기 동작을 덮지 않게).
            Body.SetSlump(false);
            BeginAlight(false);
        }

        /// <summary>The train leaves with this rider aboard (despawned by the crowd once out of sight).</summary>
        public bool Aboard => Current == Activity.InTrain || Current == Activity.Board && Body.Scripted;

        // ── 비상 ──────────────────────────────────────────────────────────────

        /// <summary>Perception tick (staggered by the crowd, about twice a second while something is wrong).</summary>
        public void Perceive(float deltaSeconds)
        {
            if (Hostile) return;
            if (Current == Activity.Evacuate || Current == Activity.Injured) { ExposeToSmoke(deltaSeconds); return; }
            var eye = transform.position + Vector3.up * (Body.Seat == PersonBody.SeatPhase.None ? 1.6f : 1.2f);
            foreach (var hazard in HazardRegistry.Active)
            {
                if (!hazard.Active || Noticed.Contains(hazard)) continue;
                float distance = Vector3.Distance(transform.position, hazard.Position);
                bool felt = !hazard.NeedsSight;
                if (hazard is EarthquakeHazard quake && !quake.Shaking) continue;
                if (!felt && (distance > hazard.NoticeRadius || !HazardRegistry.CanSee(eye, hazard))) continue;
                if (hazard.NoticeChance < 1 && !World.Chance(hazard.NoticeChance)) continue;
                Notice(hazard, false);
            }
            var smoke = ExposeToSmoke(deltaSeconds);
            // 짙은 연기 속에서는 판단을 기다리지 않고 연기를 피해 가까운 안전한 출구로 나간다(반사 행동).
            // 좁은 승강장처럼 옆으로 물러설 곳이 없는 데서도 출구 쪽 길은 있다.
            if (smoke != null && !Hurt && Current != Activity.Report && Current != Activity.Alight && Current != Activity.Board)
            {
                if (!Noticed.Contains(smoke)) Notice(smoke, false);
                Focus = smoke;
                Evacuate(false);
                return;
            }
            // 위험 반경 안이면 판단을 기다리지 않고 먼저 물러선다(반사 행동).
            if (Focus != null && Focus.Active && Focus.NeedsSight && Current != Activity.MoveAway && Current != Activity.Evacuate && Current != Activity.Report && Current != Activity.InTrain && Current != Activity.Alight
                && Vector3.Distance(transform.position, Focus.Position) < Focus.DangerRadius)
                MoveAway(Focus.DangerRadius + World.Range(6, 12));
        }

        /// <summary>Breathing smoke: coughs, and collapses after a long time in it. Returns the fire whose smoke this is, if any.</summary>
        private FireHazard ExposeToSmoke(float deltaSeconds)
        {
            FireHazard smoke = null;
            foreach (var hazard in HazardRegistry.Active)
                if (hazard is FireHazard fire && fire.InSmoke(transform.position)) smoke = fire;
            if (smoke != null) SmokeSeconds += deltaSeconds;
            else SmokeSeconds = Mathf.Max(0, SmokeSeconds - deltaSeconds * .25f);
            Body.SetCough(smoke != null && Current != Activity.Injured && Body.Seat == PersonBody.SeatPhase.None);
            // 연기 속에 오래 머물면 쓰러진다. 90초는 게임 압축 시간이며 의학적 수치가 아니다.
            if (!Hurt && SmokeSeconds > 90) Injure("연기를 오래 들이마셨다", true);
            return smoke;
        }

        /// <summary>Becomes aware of <paramref name="hazard"/>, directly or because of a cue around them.</summary>
        public void Notice(Hazard hazard, bool indirect, string cue = null)
        {
            if (Hurt || Hostile || Noticed.Contains(hazard) || Current == Activity.Evacuate) return;
            Noticed.Add(hazard);
            Cue = indirect ? cue : null;
            if (Focus != null && Focus.Active && !Routine(Current)) return;
            Focus = hazard;
            lookAt = hazard.NeedsSight ? hazard.Position : transform.position + transform.forward;
            StartDeciding();
            Crowd.Mind.OnNotice(this, hazard, indirect);
        }

        /// <summary>True for everyday activities an alarming observation interrupts.</summary>
        public static bool Routine(Activity activity) =>
            activity == Activity.Walk || activity == Activity.Sit || activity == Activity.Stand || activity == Activity.Queue || activity == Activity.Browse ||
            activity == Activity.PlatformWait || activity == Activity.Meet || activity == Activity.InTrain || activity == Activity.Toilet;

        private void StartDeciding()
        {
            if (Current == Activity.InTrain || Current == Activity.Alight || Current == Activity.Board) return;
            if (Current != Activity.Deciding) before = Current;
            if (hidden) { hidden = false; Body.SetVisible(true); }
            if ((Current == Activity.Sit) && Body.Seat != PersonBody.SeatPhase.None) { Current = Activity.Deciding; return; }
            Body.Stop();
            Body.ClearPoses();
            Current = Activity.Deciding;
        }

        /// <summary>Staff instruction or public announcement to leave through <paramref name="toward"/>.</summary>
        public void Instruct(StationPoints.Point toward, bool direct)
        {
            if (Hurt || Hostile || Current == Activity.Evacuate) return;
            Instructed = true;
            exit = toward;
            if (Focus == null && Crowd.MainHazard != null) { Focus = Crowd.MainHazard; Noticed.Add(Focus); }
            Crowd.Mind.OnInstruction(this, direct);
        }

        // 판단 결과 실행 ──

        public void KeepGoing()
        {
            if (Current == Activity.InTrain || Current == Activity.Alight || Current == Activity.Board) return;
            if (Current == Activity.Deciding || Current == Activity.Watch)
            {
                Body.SetFilm(false);
                if (Body.Seat != PersonBody.SeatPhase.None) { Current = Activity.Sit; until = Time.time + World.Range(30, 150); return; }
                if (before == Activity.PlatformWait && place != null) { Current = Activity.PlatformWait; return; }
                Decide();
            }
        }

        public void Watch(float seconds, bool film)
        {
            if (Current == Activity.InTrain) return;
            Current = Activity.Watch;
            until = Time.time + seconds;
            if (Body.Seat == PersonBody.SeatPhase.None && !Body.Scripted) { Body.Stop(); Body.SetFilm(film); }
        }

        public void MoveAway(float distance)
        {
            if (Focus == null) { KeepGoing(); return; }
            if (Current == Activity.InTrain) { BeginAlight(true); return; }
            ReleasePlace();
            Body.ClearPoses();
            var target = World.AwayFrom(transform.position, Focus.Position, distance);
            Current = Activity.MoveAway;
            if (Body.Seat != PersonBody.SeatPhase.None)
            {
                Body.BeginStand();
                StartCoroutine(AfterStanding(() => Body.GoTo(target, walkSpeed * 1.25f)));
                return;
            }
            Body.GoTo(target, walkSpeed * 1.25f);
        }

        public void Evacuate(bool run)
        {
            running = run;
            if (Current == Activity.InTrain)
            {
                // 열차 안에서는 문이 열려 있을 때 먼저 내린다.
                if (Train != null && Train.DoorsOpen > .9f) { Instructed = true; BeginAlight(true); }
                return;
            }
            if (Current == Activity.Alight || Current == Activity.Board && Body.Scripted) { Instructed = true; return; }
            ReleasePlace();
            Body.ClearPoses();
            Current = Activity.Evacuate;
            if (Body.Seat != PersonBody.SeatPhase.None) { Body.BeginStand(); pendingStand = true; return; }
            GoToExit();
        }

        private void GoToExit()
        {
            Current = Activity.Evacuate;
            var danger = Focus != null && Focus.NeedsSight ? Focus.Position : (Vector3?)null;
            if (exit == null || !Instructed || exit.Kind != PointKind.Exit) exit = World.SafeExit(transform.position, danger, Focus != null ? Focus.Clearance : 0);
            Travel(exit.Position, running ? World.Range(2.6f, 3.4f) : walkSpeed * 1.35f);
            // 뛰는 사람은 주변을 놀라게 한다. 조용히 걸어 나가는 사람은 눈에 잘 띄지 않는다.
            if (running) Crowd.Alert(transform.position, 7f, Focus, this, "people nearby are running toward the exits");
        }

        public void ReportToStaff()
        {
            if (Crowd.Player == null || Current == Activity.InTrain) { Evacuate(false); return; }
            ReleasePlace();
            Body.ClearPoses();
            Current = Activity.Report;
            reportStarted = Time.time;
            nextRepath = 0;
            phoneCallEnds = -1;
            if (Body.Seat != PersonBody.SeatPhase.None) Body.BeginStand();
        }

        private void UpdateReport()
        {
            if (Body.Seat != PersonBody.SeatPhase.None || Body.Scripted) return;
            // 직접 전화로 신고하는 중: 통화가 끝나면 물러선다.
            if (phoneCallEnds > 0)
            {
                if (Focus != null && Focus.NeedsSight) Body.Face(Focus.Position, 120);
                if (Time.time < phoneCallEnds) return;
                phoneCallEnds = -1;
                Body.SetPhone(false);
                Crowd.Session.Incidents.CitizenCall(this, Focus);
                MoveAway(Focus != null ? Focus.DangerRadius + 10 : 10);
                return;
            }
            var player = Crowd.Player.transform.position;
            float distance = Vector3.Distance(transform.position, player);
            if (distance < 2.4f && Mathf.Abs(player.y - transform.position.y) < 2f)
            {
                Body.Stop();
                Body.Face(player);
                Body.Wave();
                Crowd.Session.Incidents.HearReport(this, Focus);
                MoveAway(Focus != null ? Focus.DangerRadius + 10 : 10);
                return;
            }
            // 역무원이 너무 멀거나 오래 걸리면 그 자리에서 119·112 에 전화한다(통화 20~40초, 게임 압축 시간).
            if (Time.time - reportStarted > 45 || distance > 90)
            {
                Body.Stop();
                Body.SetPhone(true);
                phoneCallEnds = Time.time + World.Range(20, 40);
                return;
            }
            if (Time.time > nextRepath) { nextRepath = Time.time + 1f; Body.GoTo(StationWorld.OnNavMesh(player, 3), walkSpeed * 1.5f); }
        }

        /// <summary>Goes over to someone who collapsed or fell, crouches beside them for a while.</summary>
        public void HelpNearby(Hazard hazard)
        {
            if (hazard == null || Current == Activity.InTrain) { KeepGoing(); return; }
            ReleasePlace();
            Body.ClearPoses();
            helping = true;
            Focus = hazard;
            // 쓰러진 사람 곁의 빈자리(온 쪽부터 찾는다). 여럿이 도우러 와도 한 점에 겹치지 않는다.
            var target = Crowd.SpotAt(hazard.Position, this, transform.position - hazard.Position);
            Current = Activity.MoveAway;
            if (Body.Seat != PersonBody.SeatPhase.None) { Body.BeginStand(); StartCoroutine(AfterStanding(() => Body.GoTo(target, walkSpeed * 1.2f))); return; }
            Body.GoTo(target, walkSpeed * 1.2f);
        }

        public void AlertOthers()
        {
            if (Body.Seat == PersonBody.SeatPhase.None) Body.Wave();
            Crowd.Alert(transform.position, 10f, Focus, this, "someone nearby is warning people about it");
            Watch(World.Range(6, 12), false);
        }

        public void TakeCover(float seconds)
        {
            if (Current == Activity.InTrain) return;
            Current = Activity.TakeCover;
            until = Time.time + seconds;
            if (Body.Seat == PersonBody.SeatPhase.None && !Body.Scripted) { Body.Stop(); Body.SetCrouch(true); }
        }

        public void Freeze(float seconds)
        {
            if (Current == Activity.InTrain) return;
            Current = Activity.TakeCover;
            until = Time.time + seconds;
            if (Body.Seat == PersonBody.SeatPhase.None && !Body.Scripted) { Body.Stop(); Body.SetIdle(2); }
        }

        /// <summary>Hurt: stays where they are. <paramref name="down"/> — collapsed: lying on the floor, or slumped if seated.</summary>
        public void Injure(string cause, bool down = false)
        {
            if (Hurt) return;
            Hurt = true;
            // 앉은 채 쓰러진 사람의 자리는 비우지 않는다(다른 사람이 같은 자리에 앉지 않게).
            if (Body.Seat == PersonBody.SeatPhase.None) ReleasePlace();
            if (hidden) { hidden = false; Body.SetVisible(true); }
            // 쓰러진 사람은 바닥에 눕고(좌석에서는 앞으로 늘어지고), 다쳤어도 의식이 있는 사람은 그 자리에 웅크린다
            // (에스컬레이터 계단 위도 웅크린다).
            if (Body.Seat == PersonBody.SeatPhase.None)
            {
                if (!Body.Scripted) Body.Stop();
                Body.ClearPoses();
                if (down && !Body.Scripted) Body.SetDown(true);
                else { Body.SetCrouch(true); Body.SetCough(true); }
            }
            else if (down) Body.SetSlump(true);
            if (Current != Activity.InTrain) Current = Activity.Injured;
            Crowd.OnInjured(this, cause);
        }

        /// <summary>Gets up and walks off to <paramref name="destination"/>, lingers there, then leaves the station.</summary>
        public void WalkOff(Vector3 destination, float lingerSeconds)
        {
            Hostile = true;
            ReleasePlace();
            stepSeconds = lingerSeconds;
            UsesPhone = true;
            if (Body.Seat != PersonBody.SeatPhase.None)
            {
                Current = Activity.Deciding;
                Body.BeginStand();
                StartCoroutine(AfterStanding(() => WalkTo(destination, Activity.Stand, 1.65f)));
                return;
            }
            WalkTo(destination, Activity.Stand, 1.65f);
        }

        private void AfterWatching()
        {
            Body.SetFilm(false);
            if (helping) { helping = false; Body.SetCrouch(false); }
            if (Focus != null && Focus.Active && reevaluations < 2)
            {
                reevaluations++;
                before = Current;
                Current = Activity.Deciding;
                Crowd.Mind.OnReevaluate(this);
                return;
            }
            KeepGoing();
        }

        private System.Collections.IEnumerator AfterStanding(System.Action then)
        {
            while (Body.Seat != PersonBody.SeatPhase.None) yield return null;
            then();
        }

        private string Describe(Activity activity)
        {
            switch (activity)
            {
                case Activity.Walk: return afterWalk == Activity.Leave ? "walking out of the station" : afterWalk == Activity.PlatformWait ? "walking to the platform" : "walking";
                case Activity.Queue: return "queueing at a ticket window";
                case Activity.Browse: return "browsing at a shop";
                case Activity.Sit: return place != null && place.Kind == PointKind.Chair ? "sitting at a cafe table" : "sitting on a bench";
                case Activity.Stand: return "standing and waiting";
                case Activity.Leave: return "walking out of the station";
                case Activity.Toilet: return "at the toilets";
                case Activity.PlatformWait: return "waiting at the door of KTX car " + Car;
                case Activity.Board: return "boarding KTX car " + Car;
                case Activity.InTrain: return "seated in KTX car " + Car;
                case Activity.Alight: return "getting off KTX car " + Car;
                case Activity.Meet: return "waiting to meet someone off the train";
                case Activity.Deciding: return "stopped, looking at what happened";
                case Activity.Watch: return "watching from where they stand";
                case Activity.MoveAway: return "moving away";
                case Activity.Evacuate: return "leaving the station";
                case Activity.Report: return "going to tell station staff";
                case Activity.TakeCover: return "crouching to protect themselves";
                default: return "hurt, on the floor";
            }
        }

        // ── 역무원 상호작용 ────────────────────────────────────────────────────

        public string InteractionPrompt
        {
            get
            {
                if (Hurt) return CarriesAedFor() ? "AED 곁에 두기" : "상태 확인";
                return Crowd != null && Crowd.Session.Incidents.PlayerKnowsIncident ? "대피 안내" : "말 걸기";
            }
        }

        /// <summary>The staff member carries the AED and this is a collapsed or fallen person.</summary>
        private bool CarriesAedFor() => Crowd != null && Crowd.Session.Hands != null && Crowd.Session.Hands.Aed != null && Crowd.Session.Incidents.CollapseOf(this) != null;

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = Crowd != null && responder != null && !responder.IsPaused && Current != Activity.Evacuate && Body.Visible;
            reason = available ? null : Current == Activity.Evacuate ? "대피 중인 승객입니다" : "지금은 말을 걸 수 없습니다";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            if (Hurt && CarriesAedFor()) { feedback = Crowd.Session.Hands.PlaceAedBeside(this); return true; }
            if (Hurt) { feedback = Crowd.Session.Incidents.CheckInjured(this); return true; }
            if (Crowd.Session.Incidents.PlayerKnowsIncident)
            {
                int told = Crowd.InstructAround(transform.position, responder.transform.position);
                feedback = told > 1 ? "주변 승객 " + told + "명에게 대피 방향을 안내했습니다" : "승객에게 대피 방향을 안내했습니다";
                return true;
            }
            if (Body.Seat == PersonBody.SeatPhase.None && !Body.Scripted) { Body.Face(responder.transform.position); Body.Listen(); }
            feedback = "승객: " + SmallTalk();
            return true;
        }

        private string SmallTalk()
        {
            switch (Trip)
            {
                case Purpose.Depart:
                    return MissedTrain ? "열차를 놓쳤어요… 표를 다시 사야겠네요." : Train != null && Train.BoardingOpen ? "서울 가는 KTX 지금 타면 되죠? " + Car + "호차예요." : "서울 가는 KTX 기다리고 있어요. " + Car + "호차예요.";
                case Purpose.Arrive: return "방금 서울에서 내렸어요. 나가는 곳이 이쪽이죠?";
                case Purpose.Greet: return "서울에서 오는 가족 마중 나왔어요.";
                default: return Number % 2 == 0 ? "빵 사러 잠깐 들렀어요." : "화장실은 저쪽이죠? 감사합니다.";
            }
        }
    }
}
