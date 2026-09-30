using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Physical person: navmesh movement, link rides (escalators, elevators) and scripted walks (train aisles), plus
    /// animator poses. Behaviour components (passenger, responder) decide where to go; this only moves and animates.
    /// While seated or scripted the agent is off and the transform is driven here, optionally in a moving frame
    /// (the train carrier) so riders move with the train.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class PersonBody : MonoBehaviour
    {
        public Animator Animator;
        public bool Female;

        public enum SeatPhase { None, Settling, Lowering, Seated, Rising, Stepping }

        public NavMeshAgent Agent { get; private set; }
        public SeatPhase Seat { get; private set; }
        /// <summary>True while an escalator, elevator or scripted path moves the body instead of the agent.</summary>
        public bool Scripted { get; private set; }
        /// <summary>The link (escalator, elevator) the body is riding now, if any.</summary>
        public IStationLink Riding { get; private set; }
        public bool Visible { get; private set; } = true;
        /// <summary>Walking speed the owner asked for (escalator walk-on/off and scripted walks reuse it).</summary>
        public float WalkSpeed { get; private set; } = 1.3f;
        /// <summary>Where the owner last asked the body to walk (<see cref="GoTo"/>).</summary>
        public Vector3 Goal => goal;

        public const string Locomotion = "이동", Walking = "걷기", SitDown = "의자에 앉기", SitIdle = "의자에 앉은 채", StandUp = "의자에서 일어서기";
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int PaceId = Animator.StringToHash("Pace");
        private static readonly int CrouchId = Animator.StringToHash("Crouch");
        private static readonly int PhoneId = Animator.StringToHash("Phone");
        private static readonly int CoughId = Animator.StringToHash("Cough");
        private static readonly int IdleId = Animator.StringToHash("Idle");
        private static readonly int FilmId = Animator.StringToHash("Film");
        private static readonly int ListenId = Animator.StringToHash("Listen");
        private static readonly int WaveId = Animator.StringToHash("Wave");
        private static readonly int DownId = Animator.StringToHash("Down");
        private static readonly int SlumpId = Animator.StringToHash("Slump");
        private static readonly int CollapseId = Animator.StringToHash("Collapse");
        private static readonly int TreatId = Animator.StringToHash("Treat");
        private static readonly int KneelDownState = Animator.StringToHash("무릎 꿇기"), TreatState = Animator.StringToHash("처치"), KneelUpState = Animator.StringToHash("무릎 펴기");

        // 표본 측정(Temp SitCalib, 2026-09-26): 앉기 끝에서 엉덩이가 발 기준 뒤로 0.46m(남)/0.45m(여),
        // 일어서기 끝에서 앞으로 0.42m(남)/0.40m(여). 앉은 채 동작은 엉덩이 바로 아래가 원점이다.
        private float SitBack => Female ? .45f : .46f;
        private float StandForward => Female ? .40f : .42f;

        private static readonly Dictionary<RuntimeAnimatorController, (float sit, float stand)> lengths = new Dictionary<RuntimeAnimatorController, (float, float)>();
        private static readonly Dictionary<RuntimeAnimatorController, (float walk, float run)> strides = new Dictionary<RuntimeAnimatorController, (float, float)>();
        // 좌석 위치는 frame(열차 운행 기준점 등) 좌표로 둔다. frame 이 없으면 월드 좌표.
        private Transform seatFrame;
        private Vector3 seatAnchor, phaseFrom, phaseTo;
        private Quaternion seatFacing, turnFrom, stepFacing;
        private float phaseTime, phaseLength, scriptedSpeed, stepSpeed;
        private bool standToAgent = true;
        private Coroutine traversal;
        private readonly List<Renderer> renderers = new List<Renderer>();

        /// <summary>Every enabled body (station door sensors look for people through this list).</summary>
        public static readonly List<PersonBody> All = new List<PersonBody>();
        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Agent.autoTraverseOffMeshLink = false;
            // 길은 PathService 가 SetPath 로 준다: 에이전트가 스스로(비동기 길찾기 줄에) 길을 다시 묻지 않는다.
            Agent.autoRepath = false;
            if (Animator == null) Animator = GetComponentInChildren<Animator>();
            if (Animator != null) { Animator.applyRootMotion = false; Animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
            GetComponentsInChildren(true, renderers);
        }

        public bool OnNavMesh => Agent.isActiveAndEnabled && Agent.isOnNavMesh;

        /// <summary>The service that finds this body's routes (set while a station world is open).</summary>
        internal static PathService Paths;

        /// <summary>How this person weighs the links of a route; set by whoever decides how they change floors.</summary>
        public RouteProfile Profile = RouteProfile.Default;
        /// <summary>Its requests to the path service are served before the everyday ones (evacuating, moving away from danger).</summary>
        public bool Urgent;

        // 길 안내가 이 몸에 준 여정: 목적지까지 거치는 노드 사슬과, 지금 걷는 구간이 향하는 곳(Step == Chain.Count 이면 목적지 자신).
        internal readonly List<RouteGraph.Node> Chain = new List<RouteGraph.Node>();
        internal int Step, Attempts;
        internal bool Queued, Fresh;
        internal RouteGraph.Node SkipStart;
        /// <summary>The path service found no way to the goal; the body stands where it is.</summary>
        public bool NoRoute { get; private set; }

        /// <summary>A route or a leg is being asked for (the body has no path yet, or is about to get a new one).</summary>
        public bool Planning => Queued || Fresh;
        /// <summary>Has somewhere to go and is on its way: a route asked for or a path in hand.</summary>
        public bool EnRoute => hasGoal && !goalPending && (Planning || OnNavMesh && Agent.hasPath);
        internal bool WantsRoute => hasGoal && !goalPending && !Scripted && Seat == SeatPhase.None && OnNavMesh;
        internal Vector3 Target => Step < Chain.Count ? Chain[Step].Position : goal;

        public bool GoTo(Vector3 destination, float speed)
        {
            WalkSpeed = speed;
            goal = destination;
            hasGoal = true;
            goalPending = false;
            sidestep = Vector3.zero;
            Forget();
            if (Seat != SeatPhase.None || Scripted) return false;
            // 막 일어선 순간이나 길 구멍에서 빠져나오는 중처럼 아직 길 위가 아니면, 길 위에 서는 대로 이어서 간다.
            if (!OnNavMesh) { goalPending = true; return false; }
            Agent.speed = speed;
            Agent.isStopped = false;
            if ((destination - transform.position).sqrMagnitude < .09f) { Agent.ResetPath(); return true; }
            Replan();
            return true;
        }

        /// <summary>Asks the path service for a fresh route to the goal from where the body stands; the old path is followed until the new one arrives.</summary>
        public void Replan() => Replan(false);

        internal void Replan(bool retry)
        {
            if (!hasGoal) return;
            Chain.Clear();
            Step = 0;
            Fresh = true;
            NoRoute = false;
            if (!retry) { Attempts = 0; SkipStart = null; }
            Paths?.Enqueue(this);
        }

        private void Forget()
        {
            Chain.Clear();
            Step = 0;
            Attempts = 0;
            Fresh = false;
            NoRoute = false;
            SkipStart = null;
        }

        /// <summary>The path service cannot walk the body to its goal: it stops, and <see cref="NoRoute"/> tells the owner.</summary>
        internal void GiveUp()
        {
            Forget();
            NoRoute = true;
            if (OnNavMesh) Agent.ResetPath();
        }

        public void Stop()
        {
            hasGoal = false;
            goalPending = false;
            sidestep = Vector3.zero;
            Forget();
            if (OnNavMesh && !Scripted) { Agent.isStopped = true; Agent.ResetPath(); }
        }

        // 마지막으로 가려던 곳: 발밑 길이 사라졌다가(불 둘레를 깎아 낸 구멍 등) 되돌아온 뒤, 또는 길 위에 서는 대로 이어 간다.
        private Vector3 goal;
        private bool hasGoal, goalPending;
        private float strandedSince = -1;

        /// <summary>
        /// The floor under the body left the navmesh (the carved hole around a fire grew over them): after a moment, steps to
        /// the nearest walkable spot on the same storey and carries on to where they were going.
        /// </summary>
        private void WatchStranded()
        {
            if (Scripted || Seat != SeatPhase.None || !Agent.isActiveAndEnabled || Agent.isOnNavMesh) { strandedSince = -1; return; }
            if (strandedSince < 0) { strandedSince = Time.time; return; }
            if (Time.time - strandedSince < .5f) return;
            strandedSince = -1;
            var floor = StationWorld.WalkableNear(transform.position, 4f);
            if (Agent.Warp(floor) && hasGoal && (goal - floor).sqrMagnitude >= 4f) goalPending = true;
        }

        private void ResumeGoal()
        {
            if (!goalPending || Seat != SeatPhase.None || Scripted || !OnNavMesh) return;
            goalPending = false;
            Agent.speed = WalkSpeed;
            Agent.isStopped = false;
            Replan();
        }

        private float lostSince = -1;
        private int lostRetries;
        private Vector3 lostFrom;

        /// <summary>
        /// Somewhere to go but no path and no route on its way. The path service hands paths out with <c>SetPath</c>, and an agent can
        /// drop one a frame later: standing in a pocket of navmesh its polygon does not lead out of (a sliver between a bench and a wall,
        /// found for people who stood up from a seat) it takes the path, marks it invalid and never moves (measured: hasPath false, path
        /// invalid or pending, for the rest of the shift). After a second the way is asked for again; when that fails too the body walks
        /// out of the pocket along the way the path service found (<see cref="Extricate"/>) and takes its path from there. Standing at a
        /// partial path's end lands here as well, so after a few tries it gives up instead of moving the body.
        /// </summary>
        private void WatchLostPath()
        {
            bool lost = hasGoal && !goalPending && !Scripted && Seat == SeatPhase.None && OnNavMesh && !Planning && !NoRoute && !Agent.hasPath && !Agent.isStopped
                && (goal - transform.position).sqrMagnitude > 4f;
            if (!lost)
            {
                lostSince = -1;
                if (lostRetries > 0 && (transform.position - lostFrom).sqrMagnitude > 4f) lostRetries = 0;
                return;
            }
            if (lostSince < 0) { lostSince = Time.time; return; }
            if (Time.time - lostSince < 1f) return;
            lostSince = -1;
            if (lostRetries == 0) lostFrom = transform.position;
            if (++lostRetries > 4) { hasGoal = false; lostRetries = 0; return; }
            if (lostRetries == 1 || !Extricate()) Replan();
        }

        // NavMeshPath 는 MonoBehaviour 의 정적 초기화에서 만들 수 없다(첫 쓸 때 만든다).
        private static NavMeshPath wayOut;
        private static readonly Vector3[] wayOutCorners = new Vector3[16];

        /// <summary>
        /// Walks the body, scripted with the agent off, to the first corner of the way ahead that is at least two metres off, then puts
        /// the agent back on the mesh there and plans again. For an agent the crowd solver cannot move (its polygon leads nowhere, or it
        /// is pinned against a corner): the path service's path starts on the floor the rest of the station is on, so its first corners are
        /// on that floor too.
        /// </summary>
        internal bool Extricate()
        {
            if (!hasGoal || !OnNavMesh || Scripted || Seat != SeatPhase.None || traversal != null) return false;
            wayOut ??= new NavMeshPath();
            if (!Agent.CalculatePath(Target, wayOut) || wayOut.status == NavMeshPathStatus.PathInvalid) return false;
            int count = wayOut.GetCornersNonAlloc(wayOutCorners);
            int pick = -1;
            for (int i = 1; i < count && pick < 0; i++) if ((wayOutCorners[i] - transform.position).sqrMagnitude >= 4f) pick = i;
            if (pick < 0 && count > 1) pick = count - 1;
            if (pick < 0) return false;
            Agent.ResetPath();
            Agent.enabled = false;
            Scripted = true;
            traversal = StartCoroutine(WalkOut(wayOutCorners[pick]));
            return true;
        }

        private IEnumerator WalkOut(Vector3 to)
        {
            yield return WalkLine(to, Mathf.Max(WalkSpeed, 1.2f));
            traversal = null;
            Scripted = false;
            scriptedSpeed = 0;
            Agent.enabled = true;
            Agent.Warp(StationWorld.OnNavMesh(transform.position, 1f));
            if (hasGoal) Replan();
        }

        private const float WaypointReach = 5f;

        /// <summary>Near the stop its leg leads to, the body asks for the leg after it (before it arrives, so it does not slow down there).</summary>
        private void FollowRoute()
        {
            if (Step >= Chain.Count || Planning || !WantsRoute) return;
            var d = Chain[Step].Position - transform.position;
            if (Mathf.Abs(d.y) > 1.6f || d.x * d.x + d.z * d.z > WaypointReach * WaypointReach) return;
            Step++;
            Paths?.Enqueue(this);
        }

        private float nextStaleCheck;

        /// <summary>A path the navmesh changed under (a fire's carved hole grew over it, a door locked) gets planned again, at most every two seconds.</summary>
        private void WatchStale()
        {
            if (Time.time < nextStaleCheck || !WantsRoute || Planning || !Agent.hasPath) return;
            nextStaleCheck = Time.time + .5f;
            if (Agent.pathStatus != NavMeshPathStatus.PathInvalid && !Agent.isPathStale) return;
            nextStaleCheck = Time.time + 2f;
            Replan();
        }

        public bool Arrived(float tolerance = .45f) =>
            Seat == SeatPhase.None && !Scripted && OnNavMesh && !Planning && !Agent.pathPending && Step >= Chain.Count && !Agent.isOnOffMeshLink && (!Agent.hasPath || Agent.remainingDistance <= tolerance);

        /// <summary>Straight-line estimate of the walking time left to the goal (seconds), or 0.</summary>
        public float SecondsLeft()
        {
            if (!OnNavMesh || Planning || !Agent.hasPath) return 0;
            float remaining = Agent.remainingDistance;
            if (float.IsInfinity(remaining)) remaining = Vector3.Distance(transform.position, Agent.destination) * 1.4f;
            // 이 구간 뒤로 남은 경유지와 목적지까지는 직선 거리로 어림한다.
            var at = Agent.destination;
            for (int i = Step + 1; i < Chain.Count; i++) { remaining += Vector3.Distance(at, Chain[i].Position) * 1.3f; at = Chain[i].Position; }
            if (Step < Chain.Count) remaining += Vector3.Distance(at, goal) * 1.3f;
            return remaining / Mathf.Max(.3f, Agent.speed);
        }

        public void Face(Vector3 point, float degreesPerSecond = 360)
        {
            var flat = point - transform.position;
            flat.y = 0;
            if (flat.sqrMagnitude > .01f) FaceYaw(Quaternion.LookRotation(flat), degreesPerSecond);
        }

        public void FaceYaw(Quaternion rotation, float degreesPerSecond = 360) =>
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, degreesPerSecond * Time.deltaTime);

        public void SetCrouch(bool on) { if (Animator != null) Animator.SetBool(CrouchId, on); }
        public void SetPhone(bool on) { if (Animator != null) Animator.SetBool(PhoneId, on); }
        public void SetCough(bool on) { if (Animator != null) Animator.SetBool(CoughId, on); }
        public void SetFilm(bool on) { if (Animator != null) Animator.SetBool(FilmId, on); }
        public void SetIdle(int variant) { if (Animator != null) Animator.SetInteger(IdleId, variant); }
        /// <summary>Collapses where they stand and lies on the floor (unconscious). Not for seated people.</summary>
        public void SetDown(bool on)
        {
            if (Animator == null) return;
            if (on && !Animator.GetBool(DownId)) Animator.SetTrigger(CollapseId);
            else if (!on) Animator.ResetTrigger(CollapseId);
            Animator.SetBool(DownId, on);
        }
        /// <summary>Slumps forward in the seat (collapsed while seated).</summary>
        public void SetSlump(bool on) { if (Animator != null) Animator.SetBool(SlumpId, on); }
        public void Listen() { if (Animator != null) Animator.SetTrigger(ListenId); }
        /// <summary>Kneels and treats with both hands (paramedic at a casualty); off, they stand back up.</summary>
        public void SetTreat(bool on) { if (Animator != null) Animator.SetBool(TreatId, on); }

        /// <summary>From kneeling down until standing back up after a treatment. The body should not walk meanwhile.</summary>
        public bool Kneeling
        {
            get
            {
                if (Animator == null) return false;
                int state = Animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                return Animator.GetBool(TreatId) || state == KneelDownState || state == TreatState || state == KneelUpState;
            }
        }

        public void Wave() { if (Animator != null) Animator.SetTrigger(WaveId); }

        public void ClearPoses()
        {
            SetCrouch(false); SetPhone(false); SetCough(false); SetFilm(false); SetIdle(0); SetDown(false); SetSlump(false); SetTreat(false);
        }

        public void SetVisible(bool visible)
        {
            if (Visible == visible) return;
            Visible = visible;
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = visible;
        }

        // ── 링크(에스컬레이터·엘리베이터) ──────────────────────────────────────

        private Vector3 linkStart;

        private void BeginLink(OffMeshLinkData data)
        {
            var link = data.owner as IStationLink;
            Riding = link;
            Scripted = true;
            linkStart = data.startPos;
            Agent.updatePosition = false;
            traversal = StartCoroutine(RunLink(link, data));
        }

        /// <summary>
        /// A rider waiting at the foot of a link keeps their agent there (others queue behind it); once they are on their way
        /// the agent is switched off. Left standing at the foot for the whole ride, it kept everyone behind it out of the link:
        /// one escalator held back the evacuees of a floor for the half minute each rider took.
        /// </summary>
        private void ReleaseLinkFoot()
        {
            if (!Agent.isActiveAndEnabled || (transform.position - linkStart).sqrMagnitude < 2.25f) return;
            Agent.enabled = false;
        }

        private IEnumerator RunLink(IStationLink link, OffMeshLinkData data)
        {
            if (link != null) yield return link.Traverse(this, data);
            else yield return WalkLine(data.endPos, WalkSpeed);
            traversal = null;
            Riding = null;
            Scripted = false;
            scriptedSpeed = 0;
            // 링크 끝에서 몸이 선 자리에 에이전트를 세우고, 남은 길은 길 안내가 그 자리에서 새로 찾는다(몸이 에이전트보다 앞서 있다).
            if (!Agent.enabled) Agent.enabled = true;
            else if (Agent.isOnOffMeshLink) Agent.CompleteOffMeshLink();
            Agent.updatePosition = true;
            Agent.Warp(StationWorld.OnNavMesh(transform.position, 1.5f));
            if (Agent.isOnNavMesh) Agent.isStopped = false;
            if (hasGoal) { if (Agent.isOnNavMesh) Replan(); else goalPending = true; }
        }

        /// <summary>Moves in a straight line at <paramref name="speed"/> with the walk animation (link rides call this).</summary>
        public IEnumerator WalkLine(Vector3 to, float speed)
        {
            while (true)
            {
                var delta = to - transform.position;
                float step = speed * Time.deltaTime;
                if (delta.magnitude <= step) { transform.position = to; break; }
                transform.position += delta.normalized * step;
                Move(delta, speed);
                yield return null;
            }
        }

        /// <summary>Scripted movement step: faces the horizontal direction and drives the locomotion blend.</summary>
        public void Move(Vector3 direction, float animationSpeed)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > 1e-4f) FaceYaw(Quaternion.LookRotation(direction), 420);
            scriptedSpeed = animationSpeed;
        }

        /// <summary>Stands still on a moving surface (escalator belt, elevator car) facing <paramref name="forward"/>.</summary>
        public void Ride(Vector3 position, Vector3 forward)
        {
            transform.position = position;
            forward.y = 0;
            if (forward.sqrMagnitude > 1e-4f) FaceYaw(Quaternion.LookRotation(forward), 240);
            scriptedSpeed = 0;
        }

        // ── 대본 이동(열차 통로·문) ──────────────────────────────────────────

        /// <summary>
        /// Leaves the navmesh and walks a path given in <paramref name="frame"/> coordinates (the train carrier), parented to
        /// it. <paramref name="done"/> runs at the last point. Used for stepping into a car and walking its aisle.
        /// </summary>
        public void FollowInFrame(Transform frame, IList<Vector3> localPath, float speed, System.Action done, System.Func<int, bool> mayAdvance = null)
        {
            if (traversal != null) { StopCoroutine(traversal); traversal = null; }
            if (Agent.isActiveAndEnabled) { if (Agent.isOnNavMesh) Agent.ResetPath(); Agent.enabled = false; }
            Scripted = true;
            transform.SetParent(frame, true);
            traversal = StartCoroutine(Follow(localPath, speed, done, mayAdvance));
        }

        private IEnumerator Follow(IList<Vector3> localPath, float speed, System.Action done, System.Func<int, bool> mayAdvance)
        {
            for (int i = 0; i < localPath.Count; i++)
            {
                while (true)
                {
                    if (mayAdvance != null && !mayAdvance(i)) { scriptedSpeed = 0; yield return null; continue; }
                    var target = localPath[i];
                    var delta = target - transform.localPosition;
                    float step = speed * Time.deltaTime;
                    if (delta.magnitude <= step) { transform.localPosition = target; break; }
                    transform.localPosition += delta.normalized * step;
                    Move(transform.parent != null ? transform.parent.TransformDirection(delta) : delta, speed);
                    yield return null;
                }
            }
            scriptedSpeed = 0;
            traversal = null;
            done?.Invoke();
        }

        /// <summary>Back onto the navmesh at <paramref name="world"/> (out of a car, off a scripted path).</summary>
        public bool ReturnToNavMesh(Vector3 world)
        {
            if (traversal != null) { StopCoroutine(traversal); traversal = null; }
            transform.SetParent(Home, true);
            Scripted = false;
            scriptedSpeed = 0;
            Seat = SeatPhase.None;
            Agent.enabled = true;
            bool ok = Agent.Warp(StationWorld.OnNavMesh(world, 1.5f));
            Agent.updatePosition = true;
            return ok && Agent.isOnNavMesh;
        }

        /// <summary>Parent the body returns to when it leaves a moving frame (the crowd root).</summary>
        public Transform Home { get; set; }

        // ── 부축 ────────────────────────────────────────────────────────────

        private PersonBody supported;
        private SupportHands hands;
        private readonly List<Vector3> trail = new List<Vector3>();
        private float trailGap;
        private const float SupportSpeed = 1.1f;

        /// <summary>True while walking behind someone with both hands on their shoulders.</summary>
        public bool Supporting => !ReferenceEquals(supported, null);

        /// <summary>
        /// Walks close behind <paramref name="patient"/> on the very way the patient walks (a KTX aisle, the door, the step down),
        /// <paramref name="gap"/> metres back, both hands on the patient's shoulders (<see cref="SupportHands"/>). Only the
        /// patient's own walking lays the way (not sitting or standing up), so the helper never cuts through a seat row. The
        /// agent is off meanwhile; <see cref="StopSupporting"/> puts the body back on the navmesh where it stands.
        /// </summary>
        public void SupportFromBehind(PersonBody patient, float gap = .55f)
        {
            if (traversal != null) { StopCoroutine(traversal); traversal = null; }
            if (Agent.isActiveAndEnabled) { if (Agent.isOnNavMesh) Agent.ResetPath(); Agent.enabled = false; }
            Scripted = true;
            transform.SetParent(Home, true);
            supported = patient;
            trailGap = gap;
            trail.Clear();
            if (Animator != null && patient.Animator != null)
            {
                hands = Animator.GetComponent<SupportHands>() ?? Animator.gameObject.AddComponent<SupportHands>();
                hands.Target = patient.Animator;
            }
        }

        public void StopSupporting()
        {
            if (ReferenceEquals(supported, null)) return;
            supported = null;
            trail.Clear();
            if (hands != null) { Destroy(hands); hands = null; }
            ReturnToNavMesh(transform.position);
        }

        private void FollowSupported()
        {
            var lead = supported.transform.position;
            if (supported.Scripted && supported.Seat == SeatPhase.None && (trail.Count == 0 || (lead - trail[trail.Count - 1]).sqrMagnitude > .0025f)) trail.Add(lead);
            // 환자가 걸어온 길을 환자에게서 gap 만큼 거슬러 올라간 점. 길이 아직 그만큼 없으면 제자리에서 기다린다.
            var target = transform.position;
            float need = trailGap;
            var from = lead;
            for (int i = trail.Count - 1; i >= 0 && need > 0; i--)
            {
                float span = Vector3.Distance(from, trail[i]);
                if (span >= need) { target = Vector3.Lerp(from, trail[i], need / span); need = 0; }
                else { need -= span; from = trail[i]; }
            }
            var delta = target - transform.position;
            float step = SupportSpeed * Time.deltaTime;
            var move = delta.magnitude <= step ? delta : delta.normalized * step;
            transform.position += move;
            var facing = lead - transform.position;
            facing.y = 0;
            if (facing.sqrMagnitude > .01f) FaceYaw(Quaternion.LookRotation(facing), 300);
            scriptedSpeed = Time.deltaTime > 0 ? move.magnitude / Time.deltaTime : 0;
        }

        // ── 앉기 ────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts sitting. Call when standing at the seat's approach point. <paramref name="frame"/> (optional) is the moving
        /// frame the seat belongs to (the train carrier); anchor and facing are then in that frame. Returns false (and does
        /// nothing) when the body is not near the seat: settling in is a short scripted step, and called from across the hall
        /// (a walk that "arrived" without a path) it slid the body tens of metres in half a second.
        /// </summary>
        public bool BeginSit(Vector3 anchor, float facingYaw, Transform frame = null, bool returnToAgent = true)
        {
            if (Seat != SeatPhase.None) return false;
            var facing = Quaternion.Euler(0, facingYaw, 0);
            var settle = anchor + facing * Vector3.forward * SitBack;
            settle.y = anchor.y;
            var here = frame != null ? frame.InverseTransformPoint(transform.position) : transform.position;
            var gap = here - settle;
            gap.y = 0;
            if (gap.magnitude > MaxSettle) return false;
            ClearPoses();
            if (traversal != null) { StopCoroutine(traversal); traversal = null; }
            if (Agent.isActiveAndEnabled) { if (Agent.isOnNavMesh) { Agent.isStopped = true; Agent.ResetPath(); } Agent.enabled = false; }
            Scripted = false;
            seatFrame = frame;
            standToAgent = returnToAgent;
            if (frame != null && transform.parent != frame) transform.SetParent(frame, true);
            seatAnchor = anchor;
            seatFacing = facing;
            turnFrom = LocalRotation;
            phaseFrom = LocalPosition;
            phaseTo = settle;
            // 돌아서며 물러서는 걸음은 걷는 빠르기(1 m/s)를 넘지 않는다.
            float length = Mathf.Max(.55f, gap.magnitude);
            stepSpeed = gap.magnitude / length;
            Enter(SeatPhase.Settling, length);
            return true;
        }

        /// <summary>Farthest (m) the body may stand from where it settles into a seat when it starts sitting.</summary>
        private const float MaxSettle = 1.5f;

        /// <summary>Gets up (or abandons sitting down). Returns false when not seated. Train seats end in scripted mode.</summary>
        public bool BeginStand()
        {
            switch (Seat)
            {
                case SeatPhase.Settling:
                    SetLocal(phaseFrom, turnFrom);
                    FinishStanding(phaseFrom);
                    return true;
                case SeatPhase.Lowering:
                case SeatPhase.Seated:
                    SetLocal(seatAnchor, seatFacing);
                    Animator.CrossFadeInFixedTime(StandUp, .1f);
                    Enter(SeatPhase.Rising, Lengths().stand);
                    return true;
                case SeatPhase.Rising:
                case SeatPhase.Stepping:
                    return true;
                default:
                    return false;
            }
        }

        private void FinishStanding(Vector3 localStand)
        {
            Seat = SeatPhase.None;
            if (standToAgent)
            {
                var world = seatFrame != null ? seatFrame.TransformPoint(localStand) : localStand;
                transform.SetParent(Home, true);
                Agent.enabled = true;
                Agent.Warp(world);
            }
            else Scripted = true;
            seatFrame = null;
        }

        private Vector3 LocalPosition => seatFrame != null ? seatFrame.InverseTransformPoint(transform.position) : transform.position;
        private Quaternion LocalRotation => seatFrame != null ? Quaternion.Inverse(seatFrame.rotation) * transform.rotation : transform.rotation;
        private void SetLocal(Vector3 position, Quaternion rotation)
        {
            if (seatFrame != null) transform.SetPositionAndRotation(seatFrame.TransformPoint(position), seatFrame.rotation * rotation);
            else transform.SetPositionAndRotation(position, rotation);
        }

        private (float sit, float stand) Lengths()
        {
            var controller = Animator.runtimeAnimatorController;
            if (controller == null) return (3, 3);
            if (lengths.TryGetValue(controller, out var cached)) return cached;
            float sit = 3, stand = 3;
            foreach (var clip in controller.animationClips)
            {
                if (clip.name.Contains("sit_down_chair")) sit = clip.length;
                else if (clip.name.Contains("sit_stand_up_chair")) stand = clip.length;
            }
            return lengths[controller] = (sit, stand);
        }

        private void Enter(SeatPhase phase, float length)
        {
            Seat = phase;
            phaseTime = 0;
            phaseLength = Mathf.Max(.05f, length);
        }

        private void Update()
        {
            if (!Scripted && Seat == SeatPhase.None && Agent.isActiveAndEnabled && Agent.isOnOffMeshLink && traversal == null) BeginLink(Agent.currentOffMeshLinkData);
            if (Riding != null) ReleaseLinkFoot();
            if (Seat != SeatPhase.None) { UpdateSeat(); return; }
            if (!ReferenceEquals(supported, null))
            {
                // 부축하던 사람이 사라졌으면(떠나 없어짐) 그 자리에서 걷는 면으로 돌아온다.
                if (supported == null) StopSupporting();
                else { FollowSupported(); if (Animator != null) Drive(scriptedSpeed, true); return; }
            }
            if (sidestep != Vector3.zero) { if (!Scripted && OnNavMesh) StepAside(); else sidestep = Vector3.zero; }
            WatchStranded();
            ResumeGoal();
            WatchLostPath();
            WatchStuck();
            FollowRoute();
            WatchStale();
            if (Animator == null) return;
            Drive(Scripted ? scriptedSpeed : sidestep != Vector3.zero ? SidestepSpeed : OnNavMesh && !Agent.isStopped ? Agent.velocity.magnitude : 0, true);
        }

        /// <summary>
        /// Sets the walk for moving at <paramref name="speed"/> m/s so the feet keep pace with the body: Speed places the step
        /// between the walk (1) and the run (2) at the speed each really covers on this person (clip stride × avatar scale ×
        /// playback rate), and Pace slows the walk below walking speed or speeds the run above running speed. The clips carry
        /// no travel of their own (RocketboxImportRules), so this is the only thing tying the steps to the ground.
        /// </summary>
        private void Drive(float speed, bool damped)
        {
            var (walkStride, runStride) = Strides();
            float scale = Mathf.Max(.1f, Animator.humanScale * Animator.speed);
            float walk = walkStride * scale, run = runStride * scale;
            float gait = speed <= 0 ? 0 : speed <= walk ? speed / walk : speed <= run ? 1 + (speed - walk) / (run - walk) : 2;
            if (damped) Animator.SetFloat(SpeedId, gait, .12f, Time.deltaTime);
            else Animator.SetFloat(SpeedId, gait);
            gait = Animator.GetFloat(SpeedId);
            Animator.SetFloat(PaceId, gait < 1 ? Mathf.Max(.3f, gait) : speed > run ? speed / run : 1);
        }

        private (float walk, float run) Strides()
        {
            var controller = Animator.runtimeAnimatorController;
            if (controller == null) return (1, 2.7f);
            if (strides.TryGetValue(controller, out var cached)) return cached;
            float walk = 0, run = 0;
            foreach (var clip in controller.animationClips)
            {
                if (clip.name.Contains("_walk_")) walk = clip.averageSpeed.magnitude;
                else if (clip.name.Contains("_run_")) run = clip.averageSpeed.magnitude;
            }
            if (walk < .1f) walk = 1;
            if (run <= walk) run = walk * 2.7f;
            return strides[controller] = (walk, run);
        }

        // 막힌 몸의 옆걸음: 순간이동하지 않고 비켜설 곳까지 걸은 뒤 원래 가려던 곳으로 다시 길을 찾는다.
        private const float SidestepSpeed = .9f;
        private Vector3 sidestep;

        private void StepAside()
        {
            float step = SidestepSpeed * Time.deltaTime;
            var move = sidestep.magnitude <= step ? sidestep : sidestep.normalized * step;
            FaceYaw(Quaternion.LookRotation(sidestep), 420);
            Agent.Move(move);
            sidestep -= move;
            if (sidestep.sqrMagnitude > 1e-4f) return;
            sidestep = Vector3.zero;
            Agent.isStopped = false;
            Replan();
        }

        private float stuckSince = -1;
        private int stuckStage;
        private Vector3 stuckFrom;

        /// <summary>
        /// A body with somewhere to go that has not got anywhere for seconds (wedged on a navmesh edge, jittering against it, or
        /// two people blocking each other in a gap) first plans again, then walks half a metre aside (<see cref="StepAside"/>)
        /// and plans again. It never jumps: a warp here showed as a person popping 0.6 m.
        /// </summary>
        private void WatchStuck()
        {
            bool trying = !Scripted && OnNavMesh && Agent.hasPath && !Agent.isStopped && !Planning && !Agent.isOnOffMeshLink
                && Agent.remainingDistance > Agent.stoppingDistance + .5f && Agent.desiredVelocity.sqrMagnitude > .09f;
            if (!trying) { stuckSince = -1; stuckStage = 0; return; }
            // 3초에 30cm 도 못 가면 막힌 것이다(가장자리에 부딪혀 떨리는 경우도 포함).
            if (stuckSince < 0 || (transform.position - stuckFrom).sqrMagnitude > .09f) { stuckSince = Time.time; stuckFrom = transform.position; stuckStage = 0; return; }
            if (Time.time - stuckSince < 3f) return;
            stuckSince = Time.time;
            if (stuckStage++ == 0) { Replan(); return; }
            if (stuckStage > 2 && Extricate()) { stuckStage = 0; return; }
            var ahead = Agent.steeringTarget - transform.position; ahead.y = 0;
            var direction = ahead.sqrMagnitude > .01f ? ahead.normalized : transform.forward;
            foreach (var turn in new[] { 0f, 60f, -60f, 120f, -120f, 180f })
            {
                var probe = transform.position + Quaternion.Euler(0, turn, 0) * direction * .6f;
                if (!NavMesh.SamplePosition(probe, out var hit, .4f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - transform.position.y) > .4f) continue;
                if (Vector3.Distance(hit.position, transform.position) < .2f) continue;
                sidestep = hit.position - transform.position;
                sidestep.y = 0;
                Agent.isStopped = true;
                return;
            }
        }

        private void UpdateSeat()
        {
            phaseTime += Time.deltaTime;
            float t = Mathf.Clamp01(phaseTime / phaseLength);
            switch (Seat)
            {
                case SeatPhase.Settling:
                    // 의자 앞에서 돌아서며 반 걸음 물러선다.
                    SetLocal(Vector3.Lerp(phaseFrom, phaseTo, t), Quaternion.Slerp(turnFrom, seatFacing, t));
                    Drive(stepSpeed * (1 - t), false);
                    if (t >= 1) { Animator.CrossFadeInFixedTime(SitDown, .15f); Enter(SeatPhase.Lowering, Lengths().sit); }
                    break;
                case SeatPhase.Lowering:
                    if (t >= 1)
                    {
                        // 앉기 동작은 발 기준으로 끝나고 앉은 채 동작은 엉덩이 기준이다. 같은 프레임에 원점을 옮긴다.
                        SetLocal(seatAnchor, seatFacing);
                        Animator.Play(SitIdle, 0, (float)(Random.value * .9));
                        Enter(SeatPhase.Seated, 1);
                    }
                    break;
                case SeatPhase.Rising:
                    if (t >= 1)
                    {
                        phaseFrom = seatAnchor + seatFacing * Vector3.forward * StandForward;
                        // 객실 좌석은 앞줄 등받이가 가까워 반 걸음 더 나가지 않는다.
                        phaseTo = phaseFrom + seatFacing * Vector3.forward * (seatFrame != null ? 0 : .25f);
                        // 걷는 면(에이전트를 켤 곳)까지 걸어서 내려선다. 의자 줄 사이처럼 걷는 면이 1 m 넘게 떨어져 있으면, 반 걸음만
                        // 옮긴 뒤 에이전트를 켜는 순간 몸이 그리로 튀었다(2026-09-27 측정 1.02 m). 먼 만큼 오래 걷는다.
                        if (seatFrame == null) phaseTo = StationWorld.OnNavMesh(phaseTo, 2.5f);
                        SetLocal(phaseFrom, seatFacing);
                        Animator.Play(Locomotion, 0, 0);
                        var step = phaseTo - phaseFrom;
                        float length = Mathf.Max(.35f, step.magnitude);
                        stepSpeed = step.magnitude / length;
                        step.y = 0;
                        stepFacing = step.sqrMagnitude > .25f ? Quaternion.LookRotation(step) : seatFacing;
                        Enter(SeatPhase.Stepping, length);
                    }
                    break;
                case SeatPhase.Stepping:
                    SetLocal(Vector3.Lerp(phaseFrom, phaseTo, t), Quaternion.Slerp(seatFacing, stepFacing, t * 3));
                    Drive(stepSpeed, false);
                    if (t >= 1) FinishStanding(phaseTo);
                    break;
            }
        }

        /// <summary>Places the person already seated (people present at shift start, riders inside an arriving train).</summary>
        public void PlaceSeated(Vector3 anchor, float facingYaw, Transform frame = null, bool returnToAgent = true)
        {
            ClearPoses();
            if (Agent.isActiveAndEnabled) Agent.enabled = false;
            seatFrame = frame;
            standToAgent = returnToAgent;
            if (frame != null) transform.SetParent(frame, false);
            seatAnchor = anchor;
            seatFacing = Quaternion.Euler(0, facingYaw, 0);
            SetLocal(anchor, seatFacing);
            Animator.Play(SitIdle, 0, Random.value);
            Enter(SeatPhase.Seated, 1);
        }

        /// <summary>World position of the stand-up spot in front of the current seat.</summary>
        public Vector3 SeatFront => seatFrame != null ? seatFrame.TransformPoint(seatAnchor + seatFacing * Vector3.forward * (StandForward + .25f)) : seatAnchor + seatFacing * Vector3.forward * (StandForward + .25f);
    }
}
