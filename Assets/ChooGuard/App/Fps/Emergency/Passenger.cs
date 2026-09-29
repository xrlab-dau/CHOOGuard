using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// One person in the station with a trip of their own: catching the KTX to Seoul, arriving on it, meeting someone,
    /// or just visiting the shops. What they do is judged by <see cref="CrowdMind"/> in real time (JEV when connected, local
    /// rules only in runs without it). A judgement never stops them: they keep doing what they were doing until the answer
    /// arrives. Only physical reflexes are decided here. Movement uses the whole-station navmesh with
    /// escalator and elevator rides; inside the train the body walks the aisle in the train's own frame.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PersonBody))]
    public sealed class Passenger : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public enum Activity { Walk, Queue, Browse, Sit, Stand, Leave, Toilet, PlatformWait, Board, InTrain, Alight, Meet, Deciding, Watch, MoveAway, Evacuate, Report, TakeCover, Injured, Aggressive, OnTrack }
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
        /// <summary>What this person's judgement keeps between requests (what they observed, what is being asked).</summary>
        public readonly CrowdMind.Slot Slot = new CrowdMind.Slot();
        // Noticed 중 직접 보고 듣고 맡은 것(나머지는 종소리·방송·남들의 반응으로만 안다).
        private readonly HashSet<Hazard> sensed = new HashSet<Hazard>();

        /// <summary>Where the passenger is right now in plain words, for JEV context and the Tab board.</summary>
        public string Doing => Describe(Current == Activity.Deciding ? before : Current) + " (" + Crowd.World.Area(transform.position) + ")";

        private Activity before = Activity.Walk, afterWalk;
        private StationPoints.Point place, exit;
        private CrowdMind.Choice choice;
        private float until, walkSpeed, nextRepath, reportStarted, stepSeconds, hiddenUntil, phoneCallEnds = -1;
        private Vector3 lookAt;
        private bool pendingStand, running, hidden, prefetched, alightQueued, helping, itinerary, blockedRaised;
        private float nextPathCheck, stuckSince = -1, reportAskedAt, stallSince;
        private Vector3 stallAt;
        /// <summary>Gone over to help someone who collapsed or fell (see <see cref="HelpNearby"/>).</summary>
        public bool Helping => helping;
        private int reevaluations, sitTries;

        /// <summary>True when they perceived <paramref name="hazard"/> themselves (rather than only hearing of it).</summary>
        public bool Senses(Hazard hazard) => sensed.Contains(hazard);

        /// <summary>Walking the trip's purpose itinerary right after spawning; JEV's first answer replaces it.</summary>
        public bool OnItinerary => itinerary && Current == Activity.Walk;
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
                    // 막 들어온 사람: 일정대로 걷는다. JEV 의 첫 답이 오면 그 답이 일정을 대신한다.
                    BeginTrip();
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
            // 다음 걸음이 아직 정해지지 않았으면 하던 일을 잇고, 답이 오면 그때 옮겨 간다.
            if (!Crowd.Mind.Ready(this)) { Await(); return; }
            ReleasePlace();
            prefetched = false;
            itinerary = false;
            if (!Crowd.Mind.TryTakeRoutine(this, out choice)) { Await(); return; }
            Execute(choice);
        }

        /// <summary>In an everyday activity that waits for the next step to be decided (standing, sitting, in a queue…).</summary>
        public bool Holding =>
            Current == Activity.Queue || Current == Activity.Browse || Current == Activity.Stand || Current == Activity.Sit || Current == Activity.Meet || Current == Activity.Toilet || Current == Activity.PlatformWait;

        /// <summary>Keeps doing what they were doing for a moment; a walk or ride that has ended leaves them standing where they are.</summary>
        private void Await()
        {
            until = Time.time + .75f;
            switch (Current)
            {
                case Activity.Queue:
                case Activity.Browse:
                case Activity.Stand:
                case Activity.Sit:
                case Activity.Meet:
                case Activity.Toilet:
                case Activity.PlatformWait:
                    return;
            }
            Current = Activity.Stand;
        }

        /// <summary>Right after spawning there is no current action: the first step of the trip's purpose, until JEV's first answer replaces it.</summary>
        private void BeginTrip()
        {
            ReleasePlace();
            choice = Crowd.Mind.Itinerary(this);
            Execute(choice);
            Crowd.Mind.AskFirst(this);
            itinerary = true;
        }

        /// <summary>Drops the itinerary walk for the step JEV chose (its first answer arrived while they were still on the way).</summary>
        public void Redirect(CrowdMind.Choice next)
        {
            ReleasePlace();
            choice = next;
            Execute(next);
        }

        /// <summary>Gives up on where they were heading (the way is blocked) and stands ready to be told what to do instead.</summary>
        public void Replan()
        {
            if (Body.Seat != PersonBody.SeatPhase.None || Body.Scripted || Current == Activity.InTrain) return;
            Body.Stop();
            ReleasePlace();
            itinerary = false;
            Current = Activity.Stand;
            until = Time.time;
        }

        /// <summary>The way to their exit is blocked: heads for a different exit.</summary>
        public void Reroute()
        {
            var danger = Focus != null && Focus.Localized ? Focus.Position : (Vector3?)null;
            exit = Crowd.SafeExitFor(transform.position, danger, Focus != null ? Focus.Clearance : 0, exit);
            blockedRaised = false;
            stallSince = Time.time;
            Current = Activity.Evacuate;
            Travel(exit.Position, running ? World.Range(2.6f, 3.4f) : walkSpeed * 1.35f);
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
                    if (place == null) { Await(); return; }
                    WalkTo(place.Position, Activity.PlatformWait, walkSpeed);
                    return;
                case Activity.Meet:
                case Activity.Toilet:
                case Activity.Queue:
                case Activity.Browse:
                case Activity.Stand:
                case Activity.Sit:
                    place = next.Place;
                    if (place == null) { Await(); return; }
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
            itinerary = false;
            blockedRaised = false;
            stallSince = Time.time;
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
            if (Current == Activity.Walk || Current == Activity.Evacuate || Current == Activity.MoveAway) CheckRoute();
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
                    if (Time.time > until && Body.Seat == PersonBody.SeatPhase.Seated)
                    {
                        // 다음 걸음이 정해지기 전에는 일어서지 않고 앉은 채 잇는다.
                        if (Crowd.Mind.Ready(this)) Body.BeginStand();
                        else until = Time.time + .75f;
                    }
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
                    // 물러선 곳에 닿았다: JEV 가 있으면 곧바로 다음을 판단한다(없으면 지켜본 뒤 규칙이 다시 정한다).
                    if (Crowd.Mind.Usable) Crowd.Mind.OnEnded(this);
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
            if (TrainSeat == null) { Remember("found the train full"); Current = Activity.Stand; Await(); return; }
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
            if (Hostile || Current == Activity.OnTrack) return;
            if (Current == Activity.Evacuate || Current == Activity.Injured) { ExposeToSmoke(deltaSeconds); return; }
            var eye = transform.position + Vector3.up * (Body.Seat == PersonBody.SeatPhase.None ? 1.6f : 1.2f);
            foreach (var hazard in HazardRegistry.Active)
            {
                if (!hazard.Active) continue;
                if (hazard is EarthquakeHazard quake && !quake.Shaking) continue;
                var offset = hazard.Position - transform.position;
                if (offset.magnitude > hazard.NoticeRadius) continue;
                // 보이는 것은 시야가 트여야 하고, 소리·냄새는 같은 층 가까이에서만 닿는다(역 전체가 겪는 흔들림·정전은 어디서나).
                if (hazard.NeedsSight ? !HazardRegistry.CanSee(eye, hazard) : !float.IsPositiveInfinity(hazard.NoticeRadius) && Mathf.Abs(offset.y) > 4f) continue;
                // 이미 직접 알고 있는 것은 눈에 띄게 달라졌을 때만 다시 판단한다(커졌다, 번졌다, 수습됐다).
                if (Noticed.Contains(hazard) && sensed.Contains(hazard)) { Crowd.Mind.Observe(this, hazard); continue; }
                if (hazard.NoticeChance < 1 && !World.Chance(hazard.NoticeChance)) continue;
                Notice(hazard, false);
            }
            var smoke = ExposeToSmoke(deltaSeconds);
            // 짙은 연기에 기침이 나면 판단을 기다리지 않고 연기 밖으로 물러선다(반사 행동). 역을 떠날지는 JEV 가 판단한다.
            if (smoke != null && !Hurt && Current != Activity.Report && Current != Activity.Alight && Current != Activity.Board && Current != Activity.MoveAway)
            {
                if (!Noticed.Contains(smoke) || !sensed.Contains(smoke)) Notice(smoke, false);
                Focus = smoke;
                MoveAway(smoke.SmokeRadius + World.Range(4, 8));
                return;
            }
            // 위험 반경 안이면 판단을 기다리지 않고 먼저 물러선다(반사 행동).
            if (Focus != null && Focus.Active && Focus.Localized && Current != Activity.MoveAway && Current != Activity.Evacuate && Current != Activity.Report && Current != Activity.InTrain && Current != Activity.Alight
                && Vector3.Distance(transform.position, Focus.Position) < Focus.DangerRadius)
                MoveAway(Focus.DangerRadius + World.Range(6, 12));
        }

        /// <summary>
        /// Breathing smoke, gas or powder: coughs; a long time in fire smoke makes them collapse. Returns the fire whose smoke
        /// this is, if any (the reflex to leave is for smoke).
        /// </summary>
        private FireHazard ExposeToSmoke(float deltaSeconds)
        {
            FireHazard smoke = null;
            bool irritated = false;
            foreach (var hazard in HazardRegistry.Active)
            {
                if (!hazard.Irritates(transform.position)) continue;
                irritated = true;
                if (hazard is FireHazard fire) smoke = fire;
            }
            if (smoke != null) SmokeSeconds += deltaSeconds;
            else SmokeSeconds = Mathf.Max(0, SmokeSeconds - deltaSeconds * .25f);
            Body.SetCough(irritated && Current != Activity.Injured && Body.Seat == PersonBody.SeatPhase.None);
            // 연기 속에 오래 머물면 쓰러진다. 90초는 게임 압축 시간이며 의학적 수치가 아니다.
            if (!Hurt && SmokeSeconds > 90) Injure("연기를 오래 들이마셨다", true);
            return smoke;
        }

        /// <summary>Becomes aware of <paramref name="hazard"/>, directly or because of a cue around them. Never stops them: the judgement decides what they do.</summary>
        public void Notice(Hazard hazard, bool indirect, string cue = null)
        {
            if (Hurt || Hostile || Current == Activity.OnTrack || Current == Activity.Evacuate) return;
            // 종소리로만 알던 것을 직접 본 순간은 새 관측이다. 같은 방식으로 이미 알고 있는 것은 아니다.
            if (Noticed.Contains(hazard) && (indirect || sensed.Contains(hazard))) return;
            if (!KnowsIncident()) Slot.Forget();
            Noticed.Add(hazard);
            if (indirect) { sensed.Remove(hazard); Cue = cue; }
            else { sensed.Add(hazard); Cue = null; }
            // 눈길은 가장 가까운 사건으로 간다(이미 다른 사건에 마음이 가 있어도 더 가까운 것이 생기면 옮겨 간다).
            if (Focus == null || !Focus.Active || hazard.Localized && (!Focus.Localized || Vector3.Distance(transform.position, hazard.Position) < Vector3.Distance(transform.position, Focus.Position)))
            {
                Focus = hazard;
                lookAt = hazard.Localized ? hazard.Position : transform.position + transform.forward;
            }
            if (hidden) { hidden = false; Body.SetVisible(true); }
            Crowd.Mind.OnNotice(this, hazard, indirect);
        }

        /// <summary>Knows of some incident that is still going on.</summary>
        private bool KnowsIncident()
        {
            foreach (var hazard in Noticed) if (hazard.Active) return true;
            return false;
        }

        /// <summary>True for everyday activities an alarming observation interrupts.</summary>
        public static bool Routine(Activity activity) =>
            activity == Activity.Walk || activity == Activity.Sit || activity == Activity.Stand || activity == Activity.Queue || activity == Activity.Browse ||
            activity == Activity.PlatformWait || activity == Activity.Meet || activity == Activity.InTrain || activity == Activity.Toilet;

        /// <summary>Remembers the everyday activity an emergency reaction breaks off, so carrying on can pick it up again.</summary>
        private void Interrupt()
        {
            if (Routine(Current)) before = Current;
        }

        /// <summary>Staff instruction or public announcement to leave through <paramref name="toward"/>.</summary>
        public void Instruct(StationPoints.Point toward, bool direct)
        {
            if (Hurt || Hostile || Current == Activity.OnTrack || Current == Activity.Evacuate) return;
            Instructed = true;
            exit = toward;
            if (Focus == null && Crowd.MainHazard != null)
            {
                if (!KnowsIncident()) Slot.Forget();
                Focus = Crowd.MainHazard;
                Noticed.Add(Focus);
            }
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
            Interrupt();
            Current = Activity.Watch;
            until = Time.time + seconds;
            if (Body.Seat == PersonBody.SeatPhase.None && !Body.Scripted) { Body.Stop(); Body.SetFilm(film); }
        }

        public void MoveAway(float distance)
        {
            if (Focus == null) { KeepGoing(); return; }
            if (Current == Activity.InTrain) { BeginAlight(true); return; }
            Interrupt();
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
            blockedRaised = false;
            stallSince = Time.time;
            var danger = Focus != null && Focus.Localized ? Focus.Position : (Vector3?)null;
            if (exit == null || !Instructed || exit.Kind != PointKind.Exit) exit = Crowd.SafeExitFor(transform.position, danger, Focus != null ? Focus.Clearance : 0);
            Travel(exit.Position, running ? World.Range(2.6f, 3.4f) : walkSpeed * 1.35f);
            // 뛰는 사람은 주변을 놀라게 한다. 조용히 걸어 나가는 사람은 눈에 잘 띄지 않는다.
            if (running) Crowd.Alert(transform.position, 7f, Focus, this, "people nearby are running toward the exits");
        }

        public void ReportToStaff()
        {
            if (Crowd.Player == null || Current == Activity.InTrain) { Evacuate(false); return; }
            Interrupt();
            ReleasePlace();
            Body.ClearPoses();
            Current = Activity.Report;
            reportStarted = Time.time;
            reportAskedAt = 0;
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
                if (Focus != null && Focus.Localized) Body.Face(Focus.Position, 120);
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
            // 역무원이 너무 멀거나 오래 걸린다. JEV 가 있으면 그 자리에서 119·112 에 전화할지 판단하게 하고(계속 걸으면 15 초마다 다시),
            // 없으면 규칙대로 전화한다(통화 20~40초, 게임 압축 시간).
            if (Time.time - reportStarted > 45 || distance > 90)
            {
                if (!Crowd.Mind.Usable) { PhoneIn(); return; }
                if (Time.time > reportAskedAt) { reportAskedAt = Time.time + 15f; Crowd.Mind.OnEnded(this); }
            }
            if (Time.time > nextRepath) { nextRepath = Time.time + 1f; Body.GoTo(StationWorld.OnNavMesh(player, 3), walkSpeed * 1.5f); }
        }

        /// <summary>Stops and phones 119 or 112 from where they stand (the staff member is too far away or taking too long).</summary>
        public void PhoneIn()
        {
            Body.Stop();
            Body.SetPhone(true);
            phoneCallEnds = Time.time + World.Range(20, 40);
        }

        /// <summary>Goes over to someone who collapsed or fell, crouches beside them for a while.</summary>
        public void HelpNearby(Hazard hazard)
        {
            if (hazard == null || Current == Activity.InTrain) { KeepGoing(); return; }
            Interrupt();
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
            Interrupt();
            Current = Activity.TakeCover;
            until = Time.time + seconds;
            if (Body.Seat == PersonBody.SeatPhase.None && !Body.Scripted) { Body.Stop(); Body.SetCrouch(true); }
        }

        public void Freeze(float seconds)
        {
            if (Current == Activity.InTrain) return;
            Interrupt();
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

        /// <summary>
        /// Turns aggressive (a disturbance): drops their own plans and ignores everything around them; the incident
        /// director moves them from now on (<see cref="Activity.Aggressive"/> has no routine of its own).
        /// </summary>
        public void TurnAggressive()
        {
            Hostile = true;
            ReleasePlace();
            if (hidden) { hidden = false; Body.SetVisible(true); }
            Body.ClearPoses();
            Current = Activity.Aggressive;
            if (Body.Seat != PersonBody.SeatPhase.None) Body.BeginStand();
        }

        /// <summary>Restrained and led out by the police: walks to <paramref name="toward"/> and leaves the station.</summary>
        public void LedAway(StationPoints.Point toward)
        {
            exit = toward;
            WalkTo(toward.Position, Activity.Leave, Mathf.Min(walkSpeed, 1.1f));
        }

        /// <summary>Down on the track below a platform (fell or climbed down): no routine until brought back up.</summary>
        public void Strand()
        {
            ReleasePlace();
            Body.ClearPoses();
            Current = Activity.OnTrack;
        }

        /// <summary>Back up on the platform unhurt: a moment to recover, then carries on with the trip.</summary>
        public void ReturnFromTrack()
        {
            if (Hurt) return;
            Body.ClearPoses();
            Begin(Activity.Stand, World.Range(4, 8));
        }

        private void AfterWatching()
        {
            Body.SetFilm(false);
            if (helping) { helping = false; Body.SetCrouch(false); }
            if (Focus != null && Focus.Active)
            {
                if (Crowd.Mind.Usable)
                {
                    // 지켜보는 동안에도 JEV 가 판단한다: 다음 답이 올 때까지 계속 지켜본다.
                    until = Time.time + 5f;
                    Crowd.Mind.OnEnded(this);
                    return;
                }
                if (reevaluations < 2)
                {
                    reevaluations++;
                    before = Current;
                    Current = Activity.Deciding;
                    Crowd.Mind.OnEnded(this);
                    return;
                }
            }
            KeepGoing();
        }

        /// <summary>
        /// About once a second, only while something is wrong in the station: is the way to where they are heading cut (a
        /// cordon, the fire or debris took the path)? Raises one judgement per walk.
        /// </summary>
        private void CheckRoute()
        {
            if (blockedRaised || Time.time < nextPathCheck || Hurt || Hostile) return;
            nextPathCheck = Time.time + 1f;
            if (HazardRegistry.Active.Count == 0 && World.Closed.Count == 0) { stuckSince = -1; return; }
            var agent = Body.Agent;
            if (!Body.OnNavMesh || Body.Scripted || Body.Seat != PersonBody.SeatPhase.None || agent.pathPending || agent.isOnOffMeshLink || Body.Riding != null) { stuckSince = -1; stallSince = Time.time; return; }
            var status = agent.pathStatus;
            bool far = (Body.Goal - transform.position).sqrMagnitude > 9f;
            bool cut = (status == UnityEngine.AI.NavMeshPathStatus.PathPartial || status == UnityEngine.AI.NavMeshPathStatus.PathInvalid && !agent.isStopped) && far;
            // 길이 있어 보여도 6초 넘게 한 걸음도 못 가면(앞이 막힘, 길을 잃고 멈춤) 막힌 것으로 본다.
            var here = transform.position;
            if ((here - stallAt).sqrMagnitude > .25f || !far) { stallAt = here; stallSince = Time.time; }
            bool stalled = far && Time.time - stallSince > 6f;
            if (!cut && !stalled) { stuckSince = -1; return; }
            if (!stalled)
            {
                if (stuckSince < 0) { stuckSince = Time.time; return; }
                if (Time.time - stuckSince < 1.5f) return;
            }
            blockedRaised = true;
            stuckSince = -1;
            stallSince = Time.time;
            Crowd.Mind.OnBlocked(this);
        }

        /// <summary>Where the walk stands (navmesh and agent state, current leg), to trace a person who stopped short of where they were going.</summary>
        public string WalkState() =>
            "onNavMesh=" + Body.OnNavMesh + " hasPath=" + Body.Agent.hasPath + " status=" + Body.Agent.pathStatus + " stopped=" + Body.Agent.isStopped + " pending=" + Body.Agent.pathPending +
            " remaining=" + Body.Agent.remainingDistance.ToString("0.0") + " speed=" + Body.Agent.velocity.magnitude.ToString("0.0") + " leg=" + leg + "/" + legs.Count + " seat=" + Body.Seat + " link=" + Body.Agent.isOnOffMeshLink;

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
                case Activity.Aggressive: return "shouting and acting aggressively";
                case Activity.OnTrack: return "down on the track below the platform";
                default: return "hurt, on the floor";
            }
        }

        // ── 역무원 상호작용 ────────────────────────────────────────────────────

        public string InteractionPrompt
        {
            get
            {
                if (Hurt) return CarriesAedFor() ? "AED 곁에 두기" : "상태 확인";
                if (Current == Activity.Aggressive) return "말로 진정시키기";
                if (Current == Activity.OnTrack) return "움직이지 말라고 안내";
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
            // 난동 승객은 직접 제지하지 않는다: 말은 걸 수 있지만 듣지 않는다(공개 행동요령: 거리를 두고 신고).
            if (Current == Activity.Aggressive) { feedback = "승객이 소리를 지르며 말을 듣지 않습니다 · 거리를 두고 경찰을 기다리세요"; Crowd.Session.Log.Once("calm-" + Number, "역무원이 난동 승객에게 말로 진정을 권함"); return true; }
            if (Current == Activity.OnTrack) { feedback = "선로 위 승객에게 움직이지 말고 구조를 기다리라고 안내했습니다"; Crowd.Session.Log.Once("track-" + Number, "역무원이 선로 위 승객에게 움직이지 말라고 안내"); return true; }
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
