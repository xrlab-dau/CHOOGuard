using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The KTX-Sancheon set at platform 5·6 of the terminus: it arrives from Seoul, opens its platform-side doors,
    /// everyone gets off, it closes for the turnaround, opens again for boarding, closes and leaves for Seoul, then the
    /// next set arrives. Timings are game-compressed (a real terminus turnaround takes 20 minutes or more). The set
    /// moves as one kinematic body; people inside ride in <see cref="Carrier"/>, whose local coordinates are the world
    /// positions at the stop pose (station data). Vestibule doors open by themselves when someone comes close.
    /// </summary>
    public sealed class TrainService : MonoBehaviour
    {
        public enum Phase { Away, Arriving, Opening, Alighting, Turnaround, Boarding, Closing, Departing }

        // 게임 압축 시간(초). 실제 운행 수치가 아니다.
        public const float ApproachDistance = 260f, ApproachSeconds = 44f, DoorSeconds = 2.6f, TurnaroundSeconds = 55f,
            BoardingSeconds = 150f, WarningSeconds = 12f, DepartSeconds = 38f, AwaySeconds = 80f, AlightingLimit = 150f;

        public sealed class Seat
        {
            public StationPoints.SeatEntry Entry;
            public Car Car;
            public Passenger Taken;
        }

        public sealed class Car
        {
            public StationPoints.CarEntry Entry;
            public Transform Root;
            public int Number;
            public string Label;
            public readonly List<Seat> Seats = new List<Seat>();
            /// <summary>People stepping through the door or walking the aisle (for spacing and door turns).</summary>
            public readonly List<Passenger> Movers = new List<Passenger>();
            /// <summary>Responders (paramedics, firefighters) walking inside the car: inner doors open for them and the set waits.</summary>
            public readonly List<Transform> Visitors = new List<Transform>();
            public Passenger InDoor;
            /// <summary>Boarders at this door, first come first in. Only the first walks up to the door; the rest wait beside it.</summary>
            public readonly List<Passenger> Waiting = new List<Passenger>();
            /// <summary>
            /// The door closed on something (a bag, a coat, an arm): how far (0..1 of the opening) its leaves stop short. 0 when
            /// nothing is caught. While above 0 the set cannot leave.
            /// </summary>
            public float Jam;
            /// <summary>This car's platform door opened by the train manager on a staff request (0..1), while the others stay shut.</summary>
            public float CrewOpen { get; internal set; }
            internal float CrewUntil;
            internal BoxCollider Step;
            internal Transform[] Leaves;
            internal Vector3[] LeafClosed;
            internal Vector3 LeafDelta;
            internal Transform Lift;
            internal Vector3 LiftClosed, LiftDelta;
            internal readonly List<InnerDoor> Inner = new List<InnerDoor>();
            public Vector3 DoorOutside => Entry.door.outside;
            public Vector3 DoorInside => Entry.door.inside;
        }

        internal sealed class InnerDoor
        {
            public Transform Leaf;
            public Vector3 Closed, Slide, Centre;
            public float Open, LastNear;
            /// <summary>The green '1분 열림' button above the door holds it open until this time (research K1).</summary>
            public float HoldUntil;
            public Car Car;
        }

        /// <summary>One full cycle (arrival to the next arrival), for booking later departures.</summary>
        public const float CycleSeconds = ApproachSeconds + DoorSeconds + 60 + TurnaroundSeconds + BoardingSeconds + WarningSeconds + DepartSeconds + AwaySeconds;

        public Phase Stage { get; private set; }
        /// <summary>The cycle whose return trip boards next (Service increments when a set stops).</summary>
        public int NextBoardingService => Stage == Phase.Away || Stage == Phase.Arriving || Stage == Phase.Departing ? Service + 1 : Service;
        public float StageStart { get; private set; }
        public int Service { get; private set; }
        public Transform Carrier { get; private set; }
        public Vector3 Axis { get; private set; }
        public IReadOnlyList<Car> Cars => cars;
        public float DoorsOpen { get; private set; }
        public float DepartureAt { get; private set; }
        public float ArrivalAt { get; private set; }
        public string Platform { get; private set; } = "5·6 타는 곳";
        /// <summary>Reasons the set may not leave (a door blocked, a fire on board, staff asked to hold, staff on board).</summary>
        public readonly HashSet<string> Holds = new HashSet<string>();
        /// <summary>The hold a jammed door raises. Unlike the others it lets the doors close (to the jam) instead of keeping them open.</summary>
        public const string DoorJamHold = "출입문 끼임";
        public bool AtPlatform => Stage >= Phase.Opening && Stage <= Phase.Closing;
        public bool BoardingOpen => Stage == Phase.Boarding;

        /// <summary>Raised while the set is still out of sight, before each arrival (fill it with arriving passengers).</summary>
        public event Action<TrainService> Loading;
        public event Action<TrainService> Opened, BoardingOpened, ClosingSoon, Departed;

        private readonly List<Car> cars = new List<Car>();
        private Transform root;
        private Vector3 stopPosition;
        private Rigidbody body;
        private EmergencySession session;
        private bool loaded, departedRaised;
        private float announceAt = -1;

        public void Setup(EmergencySession owner, StationPoints.TrainEntry data, EmergencyArt art)
        {
            session = owner;
            root = GameObject.Find(data.root)?.transform ?? throw new InvalidOperationException("KTX 모델 없음: " + data.root);
            stopPosition = root.position;
            Axis = data.axis.normalized;
            if (!root.TryGetComponent(out body)) body = root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            Carrier = new GameObject("열차 운행 기준").transform;
            Carrier.SetParent(transform, false);
            foreach (var entry in data.cars)
            {
                var carRoot = root.Find(entry.name);
                if (carRoot == null) continue;
                var car = new Car { Entry = entry, Root = carRoot, Number = entry.number, Label = entry.label };
                car.Leaves = new Transform[entry.door.leaves.Length];
                car.LeafClosed = new Vector3[entry.door.leaves.Length];
                for (int i = 0; i < entry.door.leaves.Length; i++)
                {
                    car.Leaves[i] = carRoot.Find(entry.door.leaves[i]);
                    // 트윈은 승강장 쪽 문이 열린 모양으로 저장돼 있다.
                    if (car.Leaves[i] != null) car.LeafClosed[i] = car.Leaves[i].localPosition - entry.door.openDelta;
                }
                car.LeafDelta = entry.door.openDelta;
                car.Lift = string.IsNullOrEmpty(entry.door.lift) ? null : carRoot.Find(entry.door.lift);
                if (car.Lift != null) { car.LiftClosed = car.Lift.localPosition - entry.door.liftDelta; car.LiftDelta = entry.door.liftDelta; }
                foreach (var seat in entry.seats) car.Seats.Add(new Seat { Entry = seat, Car = car });
                cars.Add(car);
                AddFloor(car);
                // 바깥 출입문은 열차팀장이 다룬다: 역무원은 문 앞에서 무전으로 개방을 요청한다(자료 K2, JEV 010).
                foreach (var leaf in car.Leaves)
                    if (leaf != null && leaf.GetComponentInChildren<Collider>() != null) leaf.gameObject.AddComponent<TrainDoorControl>().Bind(this, car);
            }
            session.RadioProviders.Add(CrewRequests);
            PatchInnerDoors(art);
            SetDoors(0);
            Platform = "5·6 타는 곳";
            // 첫 열차는 근무 시작 직후 서울에서 들어온다(JEV 007: 도착 먼저).
            MoveTo(ApproachDistance);
            ArrivalAt = Time.time + ApproachSeconds + 2;
            Enter(Phase.Away);
        }

        /// <summary>The aisle and vestibule have no floor colliders in the twin; the staff member needs one to walk in.</summary>
        private void AddFloor(Car car)
        {
            var e = car.Entry;
            if (e.aisle == null || e.aisle.Length == 0) return;
            var far = e.aisle[e.aisle.Length - 1];
            var near = e.door.inside - Axis * Mathf.Sign(Vector3.Dot(far - e.door.inside, Axis)) * 1.2f;
            var centre = (new Vector3(near.x, e.floor, near.z) + new Vector3(far.x, e.floor, far.z)) * .5f;
            float length = Mathf.Abs(Vector3.Dot(far - near, Axis)) + 1.4f;
            var lateral = new Vector3(Axis.z, 0, -Axis.x);
            var mid = (e.min + e.max) * .5f;
            centre += lateral * Vector3.Dot(new Vector3(mid.x, 0, mid.z) - new Vector3(centre.x, 0, centre.z), lateral);
            var floor = new GameObject(car.Label + " 바닥", typeof(BoxCollider));
            floor.transform.SetParent(Carrier, false);
            floor.transform.localPosition = centre - Vector3.up * .06f;
            floor.transform.localRotation = Quaternion.LookRotation(Axis);
            floor.GetComponent<BoxCollider>().size = new Vector3(2.7f, .12f, length);
            // 승강장 면에서 차내 바닥까지가 한 걸음(0.28 m)보다 높다: 문이 열려 있는 동안 문턱 앞에 그 중간 높이의 디딤을 둔다.
            float rise = e.floor - e.door.outside.y;
            if (rise <= .28f) return;
            var outward = e.door.outside - e.door.inside; outward.y = 0;
            if (outward.sqrMagnitude < 1e-4f) return;
            outward.Normalize();
            var sill = Vector3.Lerp(e.door.outside, e.door.inside, .5f);
            var step = new GameObject(car.Label + " 출입문 디딤", typeof(BoxCollider));
            step.transform.SetParent(Carrier, false);
            step.transform.localPosition = new Vector3(sill.x, e.door.outside.y + rise * .5f - .05f, sill.z) + outward * .12f;
            step.transform.localRotation = Quaternion.LookRotation(outward);
            car.Step = step.GetComponent<BoxCollider>();
            car.Step.size = new Vector3(1f, .1f, .45f);
            car.Step.enabled = false;
        }

        private void PatchInnerDoors(EmergencyArt art)
        {
            if (art == null || art.TrainDoors == null) return;
            bool any = false;
            foreach (var patch in art.TrainDoors)
            {
                var carRoot = root.Find(patch.Car);
                var source = carRoot != null ? carRoot.Find(patch.Source) : null;
                if (source == null || patch.Fixed == null || patch.Leaf == null) continue;
                var materials = source.TryGetComponent<Renderer>(out var original) ? original.sharedMaterials : null;
                Make(source, patch.Fixed, materials, "고정");
                var leaf = Make(source, patch.Leaf, materials, "안쪽 자동문");
                source.gameObject.SetActive(false);
                var car = cars.Find(c => c.Root == carRoot);
                var bounds = leaf.GetComponent<Renderer>().bounds;
                var inner = new InnerDoor { Leaf = leaf, Closed = leaf.localPosition, Slide = patch.Slide, Centre = Carrier.InverseTransformPoint(bounds.center), Car = car };
                car?.Inner.Add(inner);
                if (car != null) TrainInnerDoorControl.Build(this, inner, bounds, World(car.DoorInside), World(new Vector3(0, car.Entry.floor, 0)).y);
                any = true;
            }
            // 2호차 정적 열림 패치는 같은 규칙의 자동문으로 대체한다.
            if (any) foreach (Transform child in root) if (child.name == "Coach02InnerDoorStaticOpenPatch") child.gameObject.SetActive(false);
        }

        private static Transform Make(Transform source, Mesh mesh, Material[] materials, string suffix)
        {
            var go = new GameObject(source.name + " · " + suffix, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(source.parent, false);
            go.transform.localPosition = source.localPosition;
            go.transform.localRotation = source.localRotation;
            go.transform.localScale = source.localScale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            return go.transform;
        }

        // ── 좌표 ───────────────────────────────────────────────────────────

        /// <summary>World position now of a station-data train position (stop pose).</summary>
        public Vector3 World(Vector3 stopPose) => Carrier.TransformPoint(stopPose);

        /// <summary>The car whose body contains <paramref name="world"/> now, if any.</summary>
        public Car CarAt(Vector3 world)
        {
            var local = Carrier.InverseTransformPoint(world);
            var lateral = new Vector3(Axis.z, 0, -Axis.x);
            foreach (var car in cars)
            {
                var min = car.Entry.min; var max = car.Entry.max;
                if (local.y < min.y - .2f || local.y > max.y) continue;
                var centre = (min + max) * .5f;
                var d = local - centre; d.y = 0;
                // 차량은 선로 방향으로 기울어 있다: 축 방향 길이와 폭으로 판정한다.
                float halfLength = (Mathf.Abs(max.z - min.z) * Mathf.Abs(Axis.z) + Mathf.Abs(max.x - min.x) * Mathf.Abs(Axis.x)) * .5f - 1.2f;
                if (Mathf.Abs(Vector3.Dot(d, Axis)) <= halfLength && Mathf.Abs(Vector3.Dot(d, lateral)) <= 1.45f) return car;
            }
            return null;
        }

        /// <summary>Cars whose door opens onto platform the station's walkways reach (people board and alight only there).</summary>
        public IEnumerable<Car> ServedCars() { foreach (var c in cars) if (c.Entry.reachable) yield return c; }

        public Seat FreeSeat(Car preferred, System.Random random)
        {
            var list = new List<Seat>();
            if (preferred != null) foreach (var s in preferred.Seats) if (s.Taken == null) list.Add(s);
            if (list.Count == 0) foreach (var c in ServedCars()) foreach (var s in c.Seats) if (s.Taken == null) list.Add(s);
            return list.Count == 0 ? null : list[random.Next(list.Count)];
        }

        public Car CarNumber(int number) => cars.Find(c => c.Number == number);

        // ── 주기 ───────────────────────────────────────────────────────────

        private void Enter(Phase phase)
        {
            Stage = phase;
            StageStart = Time.time;
        }

        private void MoveTo(float distanceNorth)
        {
            // 편성 전체가 한 운동학 강체(자식 충돌체 묶음)로 움직인다. 같은 프레임에 탑승자 기준점도 옮겨 어긋나지 않게 한다.
            var position = stopPosition + Axis * distanceNorth;
            root.position = position;
            Carrier.position = position - stopPosition;
        }

        private void SetDoors(float open)
        {
            DoorsOpen = Mathf.Clamp01(open);
            // 플러그 도어: 먼저 바깥으로 살짝 나온 뒤 차체를 따라 옆으로 민다. 발판도 함께 나온다.
            foreach (var car in cars)
            {
                float leaf = Mathf.Max(Mathf.Max(DoorsOpen, car.Jam), car.CrewOpen);
                for (int i = 0; i < car.Leaves.Length; i++)
                    if (car.Leaves[i] != null) car.Leaves[i].localPosition = car.LeafClosed[i] + car.LeafDelta * leaf;
                if (car.Lift != null) car.Lift.localPosition = car.LiftClosed + car.LiftDelta * Mathf.Clamp01(leaf * 1.6f);
                if (car.Step != null && car.Step.enabled != leaf > .9f) car.Step.enabled = leaf > .9f;
            }
        }

        private void Update()
        {
            if (session == null) return;
            float now = Time.time, elapsed = now - StageStart;
            // 열차팀장이 정비 중에 한 호차 문만 열어 준 경우(정해진 1분 동안).
            foreach (var car in cars) car.CrewOpen = Mathf.MoveTowards(car.CrewOpen, Stage == Phase.Turnaround && now < car.CrewUntil ? 1 : 0, Time.deltaTime / DoorSeconds);
            switch (Stage)
            {
                case Phase.Away:
                    if (!loaded && elapsed > 1f) { loaded = true; Loading?.Invoke(this); }
                    if (now >= ArrivalAt - ApproachSeconds) { Enter(Phase.Arriving); announceAt = now + 6; }
                    break;
                case Phase.Arriving:
                {
                    float t = Mathf.Clamp01(elapsed / ApproachSeconds);
                    // 일정한 감속: 남은 거리 = D·(1-t)².
                    MoveTo(ApproachDistance * (1 - t) * (1 - t));
                    if (announceAt > 0 && now > announceAt)
                    {
                        announceAt = -1;
                        session.Announce(PaLine.TrainArriving, "지금 " + Platform + "로 서울에서 오는 KTX 열차가 들어오고 있습니다. 승강장 안전선 밖으로 물러서 주시기 바랍니다.");
                    }
                    if (t >= 1) { MoveTo(0); Service++; Enter(Phase.Opening); }
                    break;
                }
                case Phase.Opening:
                    SetDoors(elapsed / DoorSeconds);
                    if (elapsed >= DoorSeconds) { SetDoors(1); Enter(Phase.Alighting); Opened?.Invoke(this); }
                    break;
                case Phase.Alighting:
                {
                    // 차내 환자·화재 처리 중이거나 대응 인력이 안에 있으면 문을 닫고 회차 정비에 들어가지 않는다.
                    // 시간이 넘어도 통로를 걸어 나오는 사람(부축받는 환자 등)이 다 내릴 때까지는 닫지 않는다.
                    bool busy = Holds.Count > 0, walking = false;
                    foreach (var car in cars) { busy |= car.Visitors.Count > 0; walking |= car.Movers.Count > 0; }
                    if (!busy && ((elapsed > 20 && RidersAboard() == 0) || (elapsed > AlightingLimit && !walking))) Enter(Phase.Turnaround);
                    break;
                }
                case Phase.Turnaround:
                    // 문을 닫고 차내 정리(회차 정비)를 한 뒤 다시 연다.
                    SetDoors(1 - elapsed / DoorSeconds);
                    if (elapsed >= TurnaroundSeconds)
                    {
                        Enter(Phase.Boarding);
                        DepartureAt = now + BoardingSeconds + WarningSeconds;
                        session.Announce(PaLine.TrainBoarding, "서울행 KTX 열차 타는 곳은 " + Platform + "입니다. 지금부터 타실 수 있습니다.");
                        BoardingOpened?.Invoke(this);
                    }
                    break;
                case Phase.Boarding:
                    SetDoors(elapsed / DoorSeconds);
                    if (now >= DepartureAt - WarningSeconds)
                    {
                        Enter(Phase.Closing);
                        session.Announce(PaLine.TrainDeparting, Platform + " 서울행 KTX 열차 곧 출발합니다. 타시는 분은 서둘러 주시고 출입문에서 물러서 주십시오.");
                        ClosingSoon?.Invoke(this);
                    }
                    break;
                case Phase.Closing:
                {
                    bool staffAboard = session.Player != null && CarAt(session.Player.transform.position) != null;
                    if (staffAboard) Holds.Add("역무원 차내"); else Holds.Remove("역무원 차내");
                    bool respondersAboard = false;
                    foreach (var car in cars) respondersAboard |= car.Visitors.Count > 0;
                    if (respondersAboard) Holds.Add("대응 인력 차내"); else Holds.Remove("대응 인력 차내");
                    // 끼임은 문을 연 채 붙잡지 않는다: 문은 닫히다 끼인 것에 걸려 멈추고, 열차는 떠나지 못한다.
                    int keepOpen = Holds.Count - (Holds.Contains(DoorJamHold) ? 1 : 0);
                    if (elapsed < WarningSeconds || keepOpen > 0 || AnyInDoor()) { SetDoors(1); StageStart = Mathf.Max(StageStart, now - WarningSeconds); break; }
                    float closing = (elapsed - WarningSeconds) / DoorSeconds;
                    SetDoors(1 - closing);
                    if (closing >= 1 && Holds.Count == 0) { SetDoors(0); Enter(Phase.Departing); departedRaised = false; }
                    break;
                }
                case Phase.Departing:
                {
                    float t = Mathf.Clamp01(elapsed / DepartSeconds);
                    float distance = ApproachDistance * t * t;
                    MoveTo(distance);
                    if (!departedRaised && distance > 150) { departedRaised = true; Departed?.Invoke(this); }
                    if (t >= 1)
                    {
                        foreach (var car in cars) foreach (var seat in car.Seats) seat.Taken = null;
                        loaded = false;
                        ArrivalAt = now + AwaySeconds + ApproachSeconds;
                        Enter(Phase.Away);
                    }
                    break;
                }
            }
            UpdateInnerDoors();
        }

        private int RidersAboard()
        {
            int count = 0;
            foreach (var car in cars) { foreach (var seat in car.Seats) if (seat.Taken != null) count++; count += car.Movers.Count; }
            return count;
        }

        private bool AnyInDoor()
        {
            foreach (var car in cars) if (car.InDoor != null && car.Jam <= 0) return true;
            return false;
        }

        /// <summary>
        /// Middle of the gap a jammed door leaves open (world), with the car's outward direction and the direction the leaf
        /// slides to open. The gap is beside the leaf's edge on the side it slides away from; the leaf's rendered bounds are
        /// used because its pivot is not at its centre.
        /// </summary>
        public Vector3 DoorGap(Car car, out Vector3 outward, out Vector3 along)
        {
            var outside = World(car.DoorOutside);
            var inside = World(car.DoorInside);
            outward = outside - inside; outward.y = 0; outward.Normalize();
            Transform leaf = null;
            foreach (var candidate in car.Leaves) if (candidate != null) { leaf = candidate; break; }
            var slide = leaf != null ? leaf.parent.TransformVector(car.LeafDelta) : Vector3.zero;
            // 플러그 도어는 바깥으로 살짝 나온 뒤 옆으로 민다: 옆으로 미는 성분만 문 폭으로 본다.
            slide -= Vector3.Dot(slide, outward) * outward; slide.y = 0;
            float width = slide.magnitude;
            along = width > 1e-3f ? slide / width : Vector3.Cross(Vector3.up, outward);
            var centre = (outside + inside) * .5f;
            if (leaf == null) return centre;
            var renderers = leaf.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return centre;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            // 문짝 가운데에서 미는 반대쪽으로 문짝 반 폭 + 틈의 반만큼.
            float half = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(along.x), 0, Mathf.Abs(along.z))));
            return bounds.center - along * (half + width * car.Jam * .5f);
        }

        /// <summary>Opens the doors again for <paramref name="seconds"/> before closing (after something caught in one was freed).</summary>
        public void Reopen(float seconds)
        {
            if (Stage == Phase.Closing) StageStart = Time.time - WarningSeconds + seconds;
        }

        /// <summary>
        /// Staff asked the train manager (열차팀장, who works the exterior doors: research K2, JEV 010) to open this car's
        /// platform door. At the platform with the doors shut for the turnaround he opens that one door for a minute; while the
        /// set is closing to leave he opens them again and holds a moment; arriving or on the move he cannot.
        /// </summary>
        public bool RequestDoor(Car car, out string reply)
        {
            switch (Stage)
            {
                case Phase.Arriving: reply = "지금 진입 중입니다. 정차하면 출입문 열겠습니다."; return false;
                case Phase.Opening:
                case Phase.Alighting:
                case Phase.Boarding: reply = car.Label + " 출입문 열려 있습니다."; return false;
                case Phase.Turnaround:
                    if (!car.Entry.reachable) { reply = car.Label + " 문 앞은 승강장 통로가 아닙니다. 다른 호차로 오십시오."; return false; }
                    if (car.CrewOpen > .98f) { reply = car.Label + " 출입문 열려 있습니다."; return false; }
                    car.CrewUntil = Time.time + 60;
                    reply = car.Label + " 출입문 열겠습니다. 회차 정비 중이라 1분 뒤 닫습니다.";
                    return true;
                case Phase.Closing:
                    if (DoorsOpen > .98f) { reply = "출입문 열려 있습니다. 출발 전 확인 바랍니다."; return false; }
                    Reopen(8);
                    reply = "출입문 다시 열겠습니다. 출발 잠시 보류합니다.";
                    return true;
                default: reply = "운행 중이라 출입문을 열 수 없습니다."; return false;
            }
        }

        /// <summary>The staff member radios the request (door button or radio wheel) and the train manager answers.</summary>
        public bool Ask(Car car)
        {
            session.Hud.Radio.Push(RadioChannel.Self, "열차팀장, " + Platform + " " + car.Label + " 출입문 개방 바랍니다.");
            bool done = RequestDoor(car, out var reply);
            session.Hud.Radio.Push(RadioChannel.TrainCrew, reply);
            session.Log?.Add("열차팀장에게 " + car.Label + " 출입문 개방 요청 · " + (done ? "개방" : "불가") + " (" + Status() + ")");
            return done;
        }

        /// <summary>The car whose platform door is nearest <paramref name="world"/> (within range, same level), or null.</summary>
        public Car NearestDoor(Vector3 world, float range)
        {
            Car best = null;
            float bestDistance = range;
            foreach (var car in cars)
            {
                var door = World(car.DoorOutside);
                if (Mathf.Abs(door.y - world.y) > 2f) continue;
                float d = Horizontal(door, world);
                if (d < bestDistance) { bestDistance = d; best = car; }
            }
            return best;
        }

        private IEnumerable<EmergencySession.RadioOption> CrewRequests()
        {
            var player = session.Player;
            if (player == null || Stage == Phase.Away || Stage == Phase.Departing) yield break;
            var car = NearestDoor(player.transform.position, 6f);
            if (car == null || DoorsOpen > .98f || car.CrewOpen > .98f) yield break;
            yield return new EmergencySession.RadioOption { Label = "열차팀장 · " + car.Label + " 출입문 개방 요청", Send = () => Ask(car) };
        }

        /// <summary>People currently riding (seated or moving inside), for despawning at departure.</summary>
        public IEnumerable<Passenger> Aboard()
        {
            foreach (var car in cars)
            {
                foreach (var seat in car.Seats) if (seat.Taken != null) yield return seat.Taken;
                foreach (var mover in car.Movers) yield return mover;
            }
        }

        private void UpdateInnerDoors()
        {
            var player = session.Player != null ? session.Player.transform.position : Vector3.one * 1e6f;
            foreach (var car in cars)
            {
                foreach (var door in car.Inner)
                {
                    var centre = Carrier.TransformPoint(door.Centre);
                    bool near = Horizontal(player, centre) < 1.6f && Mathf.Abs(player.y - centre.y) < 2.2f;
                    foreach (var mover in car.Movers) if (mover != null && Horizontal(mover.transform.position, centre) < 1.6f) near = true;
                    foreach (var visitor in car.Visitors) if (visitor != null && Horizontal(visitor.position, centre) < 1.6f) near = true;
                    if (near) door.LastNear = Time.time;
                    float target = Time.time - door.LastNear < 1.8f || Time.time < door.HoldUntil ? 1 : 0;
                    door.Open = Mathf.MoveTowards(door.Open, target, Time.deltaTime / .7f);
                    door.Leaf.localPosition = door.Closed + door.Slide * door.Open;
                }
            }
        }

        private static float Horizontal(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

        /// <summary>
        /// Aisle points (car frame) between the vestibule and <paramref name="rowPoint"/>: inward from the door, or outward
        /// back to it. The aisle has no floor collider, so people inside follow these points instead of the navmesh.
        /// </summary>
        public static void AddAisle(List<Vector3> path, Car car, Vector3 rowPoint, bool inward)
        {
            var aisle = car.Entry.aisle;
            var start = aisle[0];
            float target = Horizontal(start, rowPoint);
            var points = new List<Vector3>();
            for (int i = 0; i < aisle.Length; i++) if (Horizontal(start, aisle[i]) < target - .3f) points.Add(aisle[i]);
            if (!inward) points.Reverse();
            path.AddRange(points);
        }

        /// <summary>The aisle point (car frame) nearest <paramref name="world"/>.</summary>
        public Vector3 AisleNear(Car car, Vector3 world)
        {
            var local = Carrier.InverseTransformPoint(world);
            var best = car.Entry.aisle[0];
            foreach (var point in car.Entry.aisle) if (Horizontal(point, local) < Horizontal(best, local)) best = point;
            return best;
        }

        /// <summary>What the Tab board and JEV context say about the set now.</summary>
        public string Status()
        {
            switch (Stage)
            {
                case Phase.Away: return "서울발 KTX 도착 " + Mathf.Max(0, Mathf.RoundToInt((ArrivalAt - Time.time) / 60f)) + "분 전";
                case Phase.Arriving: return "서울발 KTX " + Platform + " 도착 중";
                case Phase.Opening:
                case Phase.Alighting: return "서울발 KTX 도착 · 하차 중";
                case Phase.Turnaround: return "회차 정비 중 · 서울행 탑승 대기";
                case Phase.Boarding: return "서울행 KTX 탑승 중 · " + Mathf.Max(0, Mathf.CeilToInt((DepartureAt - Time.time) / 60f)) + "분 뒤 출발";
                case Phase.Closing: return Holds.Count > 0 ? "서울행 KTX 출발 보류 (" + string.Join(", ", Holds) + ")" : "서울행 KTX 출발 직전";
                default: return "서울행 KTX 출발";
            }
        }

        /// <summary>Seconds until this set's departure for Seoul (boarding at the platform), or the next cycle's.</summary>
        public float SecondsToDeparture()
        {
            float now = Time.time;
            switch (Stage)
            {
                case Phase.Boarding:
                case Phase.Closing: return Mathf.Max(0, DepartureAt - now);
                case Phase.Turnaround: return TurnaroundSeconds - (now - StageStart) + BoardingSeconds + WarningSeconds;
                case Phase.Alighting: return AlightingLimit * .5f + TurnaroundSeconds + BoardingSeconds + WarningSeconds;
                case Phase.Opening:
                case Phase.Arriving: return Mathf.Max(0, ArrivalAt - now) + 60 + TurnaroundSeconds + BoardingSeconds + WarningSeconds;
                default: return Mathf.Max(0, ArrivalAt - now) + 60 + TurnaroundSeconds + BoardingSeconds + WarningSeconds;
            }
        }
    }
}
