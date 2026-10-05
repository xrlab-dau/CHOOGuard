using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChooGuard.App.Fps.Equipment;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Composes the shift's emergency in real time instead of picking one. The game lists every atomic causal transition
    /// the live world makes possible for specific people, things and places anywhere in the station — the cause catalogue
    /// of the family files: fires from a power bank, a shop fryer, a litter bin, an electrical fault or under a KTX car;
    /// collapses, seizures, chest pain, breathing trouble, falls on stairs and escalators; left luggage, an aggressive
    /// passenger, a phoned bomb threat, suspicious powder; a gas smell, a burst pipe, a falling panel, a stuck elevator, a
    /// power cut, a detector tripping without a fire; a person on the track, a closing door catching a bag; an earthquake
    /// — and JEV judges how imminent each one is right now (IncidentDirector.Compose.cs: judged again the moment the
    /// situation changes and on a heartbeat). The game turns the levels into hazard rates and draws over the game time
    /// that has passed; how strongly what happens plays out comes from JEV's Score probabilities per level. Common rules
    /// compute consequences; developments of what exists are judged the same way. No incident type exists before it
    /// emerges from the chain, and nothing is composed without JEV. It also owns what the staff member knows, the radio
    /// to the station office, agency dispatch and arrival, and the handover that ends the shift. The family files
    /// (IncidentDirector.*.cs) hold each family's causes, developments, rules and its own radio and board lines.
    /// </summary>
    public sealed partial class IncidentDirector : MonoBehaviour
    {
        public enum Phase { Calm, Incident, Ended }

        public Phase Stage { get; private set; }
        public Hazard Main { get; private set; }
        public bool PlayerKnowsIncident { get; private set; }
        public Vector3 PlayerPosition => session.Player.transform.position;
        public StationWorld World => world;
        /// <summary>The station fire bell rings once a detector or call point trips; a real fire keeps it ringing to the end (JEV 009).</summary>
        public bool AlarmRinging => alarm;

        /// <summary>
        /// The hazard guidance leads to: an active one the player has learned about that has a place to go to (<see cref="Hazard.Localized"/>; a station-wide
        /// shake, a power cut or a phoned threat has none), the main one first, else the first registered. No kind is listed.
        /// </summary>
        public bool TryGetKnownGuideTarget(out Hazard target)
        {
            target = null;
            if (!PlayerKnowsIncident || Stage != Phase.Incident) return false;
            if (Main != null && GuideTarget(Main)) { target = Main; return true; }
            foreach (var hazard in all)
                if (GuideTarget(hazard)) { target = hazard; return true; }
            return false;
        }

        private bool GuideTarget(Hazard hazard) => hazard.Active && hazard.Localized && known.Contains(hazard);

        /// <summary>
        /// Hands the guidance planner what the player knows stands in the way, and nothing else: the hazards they have learned of, the cordons they put up,
        /// the lowered fire shutters they have seen and the escalators they had closed. What is in the world but not yet found (a registered hazard, a
        /// closed-off disc, a stopped escalator) never reaches the planner.
        /// </summary>
        public void GiveKnownObstructions(GuideRoute guide)
        {
            guide.Hazards.Clear();
            guide.Closures.Clear();
            guide.Escalators.Clear();
            foreach (var hazard in all) if (known.Contains(hazard)) guide.Hazards.Add(hazard);
            foreach (var cordon in staffCordons) guide.Closures.Add(new GuideRoute.Closure(cordon.centre, cordon.radius, cordon.hazard));
            AddSeenShutters(guide.Closures);
            foreach (var escalator in closedEscalators) guide.Escalators.Add(escalator);
        }

        private EmergencySession session;
        private StationWorld world;
        private CrowdDirector crowd;
        private JevClient jev;
        private EmergencyArt art;
        private ShiftLog log;
        private Transform root;
        private TrainService Train => world.Train;

        // 합성 (실시간 판단 루프는 IncidentDirector.Compose.cs)
        private float knownAt, jevNoticeAt;
        private int serial;
        private string knownHow = "";
        private const float DevelopmentCooldown = 40f;
        private readonly Dictionary<string, float> lastDevelopment = new Dictionary<string, float>();
        private readonly HashSet<string> composedKinds = new HashSet<string>();

        // 위험
        private readonly List<Hazard> all = new List<Hazard>();

        // 역무원
        private readonly HashSet<Hazard> known = new HashSet<Hazard>();
        private readonly List<(Vector3 centre, float radius, Hazard hazard)> staffCordons = new List<(Vector3, float, Hazard)>();
        private readonly HashSet<Hazard> reported = new HashSet<Hazard>();
        private readonly HashSet<Hazard> reportedDone = new HashSet<Hazard>();
        private readonly HashSet<Passenger> injuredKnown = new HashSet<Passenger>();
        private bool announced, alarm, handedOver, citizenCalled;
        private float officeFollowUp = -1;

        // 기관
        private readonly Dictionary<Agency, float> arriveAt = new Dictionary<Agency, float>();
        private readonly Dictionary<Agency, string> calledBy = new Dictionary<Agency, string>();
        private readonly HashSet<Agency> arrived = new HashSet<Agency>();
        private readonly List<Responder> responders = new List<Responder>();

        private Transform cameraTransform;
        private float nextSight, nextTeamMark;

        public void Begin(EmergencySession owner, StationWorld stationWorld, CrowdDirector people, JevClient client, EmergencyArt emergencyArt, ShiftLog shiftLog)
        {
            session = owner;
            world = stationWorld;
            crowd = people;
            jev = client;
            art = emergencyArt;
            log = shiftLog;
            root = new GameObject("사건").transform;
            root.SetParent(transform, false);
            cameraTransform = session.Player.PlayerCamera.transform;
            BeginFacility();
            BeginEquipment();
            BeginCompose();
            session.RadioProviders.Add(Radio);
            session.BoardProviders.Add(SituationColumn);
            session.BoardProviders.Add(ActionColumn);
            session.BoardProviders.Add(AgencyColumn);
            session.BoardProviders.Add(CrowdColumn);
            Facilities.StationSignals.CallPointPressed += CallPoint;
            if (jev == null || !jev.Available)
            {
                // 비상상황은 JEV 만 만든다. 키가 없으면 근무는 평온하게 흐르고, 그 사실을 처음부터 알린다.
                session.Hud.Toast(jev != null && jev.Source == JevKeySource.Off ? "JEV 가 꺼져 있어(TYPESAFE_API_KEY=off) 비상상황이 만들어지지 않습니다" : "JEV 키가 없어 비상상황이 만들어지지 않습니다 · 타이틀의 'JEV 연결'에서 키를 입력하세요", 9f);
                log.Add("JEV 없음 · 비상상황을 만들지 않음 (" + (jev != null ? jev.Status : "JEV 없음") + ")");
            }
        }

        private void OnDestroy()
        {
            Facilities.StationSignals.CallPointPressed -= CallPoint;
            EndFacility();
            EndEquipment();
            HazardRegistry.Clear();
        }

        // ── 주기 ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (session == null || Stage == Phase.Ended) return;
            float dt = Time.deltaTime;
            foreach (var hazard in HazardRegistry.Active.ToArray()) hazard.Tick(dt);
            FireTick(dt);
            SecurityTick();
            CasualtyTick();
            TrainTick();
            FacilityTick();
            EquipmentTick(dt);
            UpdateConsequences();
            Compose();
            if (Time.time > nextSight) { nextSight = Time.time + .25f; LookAround(); }
            TrackTeams();
            UpdateHud();
        }

        private void UpdateConsequences()
        {
            if (Stage != Phase.Incident) return;
            // 역무실은 감지기 동작이나 지진 뒤 일정 시간 보고가 없으면 스스로 판단해 부른다.
            if (officeFollowUp > 0 && Time.time > officeFollowUp && !Shaking)
            {
                officeFollowUp = -1;
                OfficeFollowUp();
            }
            foreach (var pair in arriveAt)
                if (!arrived.Contains(pair.Key) && Time.time >= pair.Value) { arrived.Add(pair.Key); SpawnTeam(pair.Key); break; }
            // 현장을 지휘할 기관이 도착했는데 역무원이 오지 않으면 결국 그 기관이 현장을 넘겨받는다. 거드는 기관은 넘겨받지 않는다
            // (따로 겹친 불로 소방이 지휘하게 되면 먼저 와 있던 구급대·경찰은 소방이 올 때까지 넘겨받지 않는다).
            foreach (var responder in responders)
                if (responder.Lead && responder.OnScene && !handedOver && responder.Agency == Commander && Time.time - responder.OnSceneAt > 100)
                {
                    var name = Responder.AgencyName(responder.Agency);
                    Finish("인계 없이 " + name + KoreanText.Subject(name) + " 현장을 넘겨받음", "no_handover");
                    return;
                }
        }

        private int CountAware(Hazard hazard)
        {
            int count = 0;
            foreach (var person in crowd.People) if (person.Noticed.Contains(hazard)) count++;
            return count;
        }

        // ── 합성: 원자 전이 ────────────────────────────────────────────────────

        private sealed class Transition
        {
            public string Key, Kind, Description;
            /// <summary>Starts a new emergency (a cause), as opposed to a development of one that exists.</summary>
            public bool Origin;
            /// <summary>Magnitude scale for JEV Score (lowest first); null when the transition has no magnitude.</summary>
            public List<string> Levels;
            public Action<float> Apply;
            /// <summary>An origin: the one concrete person, thing or place it names (the station itself for a cause that belongs to no single one). Set by <see cref="Bound{T}"/>.</summary>
            public object Subject;
            /// <summary>
            /// An origin: asks the world about that same subject again and returns the transition as the world makes it now, or null when
            /// the subject no longer qualifies. It tests the one subject and never looks through a listing, so what a check costs does not
            /// grow with the number of people and things in the station.
            /// </summary>
            public Func<Transition> Recheck;
        }

        /// <summary>
        /// The people in the station when a listing starts: a copy, because a listing spans several frames in which people come and go.
        /// Nobody is left out to keep a list short: every person is tested for every cause on their own (<see cref="Passengers"/>).
        /// One instance is refilled for every listing.
        /// </summary>
        private sealed class Roster
        {
            private readonly IncidentDirector director;
            public readonly List<Passenger> People = new List<Passenger>();

            public Roster(IncidentDirector owner) { director = owner; }

            public void Refill()
            {
                People.Clear();
                var everyone = director.crowd.People;
                for (int i = 0; i < everyone.Count; i++) People.Add(everyone[i]);
            }
        }

        /// <summary>Whether <paramref name="person"/> can be the subject of a new emergency right now: in the station (or in a KTX car standing open at the platform), visible, unhurt and going about their day.</summary>
        private bool Eligible(Passenger person)
        {
            // 목록은 여러 프레임에 걸쳐 만들어진다: 그 사이 역을 떠나 사라진 승객은 건너뛴다.
            if (person == null || person.Hostile || person.Hurt || !person.Body.Visible) return false;
            if (person.Current == Passenger.Activity.InTrain && (Train == null || Train.DoorsOpen < .9f || !Train.AtPlatform)) return false;
            return Passenger.Routine(person.Current);
        }

        /// <summary>
        /// An origin bound to the one subject it names. <paramref name="build"/> tests the subject against the world as it is now and
        /// returns its transition, or null when the subject does not qualify; the transition remembers the subject and how to ask again
        /// (<see cref="Transition.Recheck"/>), so a draw can confirm exactly this subject without listing the world again.
        /// </summary>
        private static Transition Bound<T>(T subject, Func<T, Transition> build) where T : class
        {
            var transition = build(subject);
            if (transition == null) return null;
            transition.Subject = subject;
            transition.Recheck = () => Bound(subject, build);
            return transition;
        }

        /// <summary>
        /// One transition for every subject in <paramref name="subjects"/> that qualifies (see <see cref="Bound{T}"/>), none dropped. After
        /// every subject there is a checkpoint (<c>null</c>, see <see cref="Fill"/>), so a listing of thousands ends its step when the
        /// frame's share is spent. The lists given here do not change while the shift runs.
        /// </summary>
        private static IEnumerable<Transition> Each<T>(IReadOnlyList<T> subjects, Func<T, Transition> build) where T : class
        {
            for (int i = 0; i < subjects.Count; i++)
            {
                var subject = subjects[i];
                if (subject is UnityEngine.Object unity ? unity == null : subject == null) continue;
                var transition = Bound(subject, build);
                if (transition != null) yield return transition;
                yield return null;
            }
        }

        /// <summary>A cause that belongs to the whole station (a phone call, a quake, a power cut): its subject is the station itself.</summary>
        private IEnumerable<Transition> Station(Func<Transition> build)
        {
            var transition = Bound(this, _ => build());
            if (transition != null) yield return transition;
        }

        private static IEnumerable<Transition> Chain(params IEnumerable<Transition>[] parts)
        {
            foreach (var part in parts)
                foreach (var transition in part)
                    yield return transition;
        }

        /// <summary>Everyone in the roster who qualifies for a cause: <paramref name="filter"/> is what the cause needs of a person, <paramref name="make"/> builds the transition.</summary>
        private IEnumerable<Transition> Passengers(Roster roster, Func<Passenger, bool> filter, Func<Passenger, Transition> make) =>
            Each(roster.People, person => Eligible(person) && filter(person) ? make(person) : null);

        private IEnumerable<Transition> Passengers(Roster roster, Func<Passenger, Transition> make) =>
            Each(roster.People, person => Eligible(person) ? make(person) : null);

        /// <summary>
        /// Every placed piece of <paramref name="kind"/> that can be the subject of a cause now: not burning, outside any cordon and
        /// accepted by <paramref name="usable"/>; <paramref name="make"/> builds its transition.
        /// </summary>
        private IEnumerable<Transition> Pieces(string kind, Func<StationEquipment, bool> usable, Func<StationEquipment, Transition> make) =>
            Each(EquipmentRegistry.OfKind(kind), piece => !BurningIn(piece) && usable(piece) && !world.IsClosed(piece.transform.position, 2) ? make(piece) : null);

        private bool Ready(string key) => !lastDevelopment.TryGetValue(key, out var at) || Time.time - at > DevelopmentCooldown;

        private void Execute(Transition transition, float magnitude, int candidates, string detail)
        {
            log.Composed(transition.Kind, transition.Key, transition.Origin, magnitude, candidates, detail);
            lastDevelopment[transition.Key] = Time.time;
            lastDevelopment[transition.Kind] = Time.time;
            if (transition.Origin)
            {
                composedKinds.Add(transition.Kind);
                lastEmergencyAt = session.ShiftSeconds;
                log.Director.NewEmergency();
            }
            Debug.Log("CG_COMPOSE " + transition.Kind + " key=" + transition.Key + " m=" + magnitude.ToString("0.00") + " by " + detail);
            transition.Apply(Mathf.Clamp01(magnitude));
        }

        private void Register(Hazard hazard)
        {
            if (!all.Contains(hazard)) all.Add(hazard);
            if (hazard.Active) HazardRegistry.Add(hazard);
            if (Main != null) return;
            Main = hazard;
            Stage = Phase.Incident;
            Debug.Log("CG_INCIDENT_START " + hazard.Label + " at " + hazard.Where);
        }

        /// <summary>
        /// Every cause whose preconditions hold anywhere right now, once for every person, piece of equipment or place it holds for
        /// (JEV 012 every_cause_every_round), appended to <paramref name="list"/>. The family files list them lazily, so each step
        /// does the work up to the end of the frame's share (<see cref="SliceSpent"/>), which lets the real-time loop spread
        /// a listing over frames however long it is. <paramref name="roster"/> is refilled by the first step.
        /// </summary>
        private IEnumerable<bool> OriginSteps(List<Transition> list, Roster roster)
        {
            roster.Refill();
            if (SliceSpent) yield return true;
            foreach (var step in Fill(list, FireOrigins(roster))) yield return step;
            foreach (var step in Fill(list, CasualtyOrigins(roster))) yield return step;
            foreach (var step in Fill(list, SecurityOrigins(roster))) yield return step;
            foreach (var step in Fill(list, TrainOrigins(roster))) yield return step;
            foreach (var step in Fill(list, FacilityOrigins())) yield return step;
            foreach (var step in Fill(list, EquipmentOrigins())) yield return step;
        }

        /// <summary>The developments of what exists, in steps of the frame's share like <see cref="OriginSteps"/>.</summary>
        private IEnumerable<bool> DevelopmentSteps(List<Transition> list)
        {
            foreach (var step in Fill(list, FireDevelopments())) yield return step;
            foreach (var step in Fill(list, CasualtyDevelopments())) yield return step;
            foreach (var step in Fill(list, SecurityDevelopments())) yield return step;
            foreach (var step in Fill(list, TrainDevelopments())) yield return step;
            foreach (var step in Fill(list, FacilityDevelopments())) yield return step;
            EquipmentDevelopments(list);
        }

        /// <summary>
        /// Appends what <paramref name="source"/> lists. A family is consumed whole in one step unless it marks safe places
        /// (no live collection is being enumerated there) with a <c>null</c> checkpoint: at a checkpoint, and after the
        /// family, the step ends once this frame's share is spent.
        /// </summary>
        private IEnumerable<bool> Fill(List<Transition> list, IEnumerable<Transition> source)
        {
            foreach (var transition in source)
            {
                if (transition == null)
                {
                    if (SliceSpent) yield return true;
                    continue;
                }
                list.Add(transition);
            }
            if (SliceSpent) yield return true;
        }

        /// <summary>The whole list of new-emergency candidates at once (editor harnesses read it by reflection to force every kind; the play loop lists in steps).</summary>
        private List<Transition> Origins()
        {
            wholeRoster = wholeRoster ?? new Roster(this);
            var list = new List<Transition>();
            foreach (var _ in OriginSteps(list, wholeRoster)) { }
            Distinct(list);
            return list;
        }

        /// <summary>The same person or place can be the candidate of several causes, but every key names one cause: the first wins (in place).</summary>
        private void Distinct(List<Transition> list)
        {
            seenKeys.Clear();
            int keep = 0;
            for (int i = 0; i < list.Count; i++)
                if (seenKeys.Add(list[i].Key)) list[keep++] = list[i];
            list.RemoveRange(keep, list.Count - keep);
        }

        private readonly HashSet<string> seenKeys = new HashSet<string>();
        private Roster listingRoster, wholeRoster;

        private string Profile(Passenger p) =>
            "passenger #" + p.Number + " (" + (p.Body.Female ? "woman" : "man") + (p.Elderly ? ", elderly" : "") + (p.Luggage == 2 ? ", large suitcase" : p.Luggage == 1 ? ", bag" : "") + ")";

        // 후보 설명은 1 s 마다 다시 만들어진다. 앉거나 서 있는 사람이 대부분이라 같은 자리의 이름(가장 가까운 표지를 찾는 일)을
        // 반 미터 칸마다 기억해 둔다. 이름이 열차에 따라 바뀌는 것은 열차가 서는 승강장과 객차 안뿐이라, 열차 단계가 바뀔 때
        // 그런 칸의 이름만 다시 짓는다.
        private struct PlaceName
        {
            public string Text;
            public bool TrainDependent;
            public int Epoch;
        }

        private readonly Dictionary<Vector3Int, PlaceName> places = new Dictionary<Vector3Int, PlaceName>();
        private readonly Dictionary<Vector3Int, PlaceName> fixedPlaces = new Dictionary<Vector3Int, PlaceName>();
        private TrainService.Phase placesTrainStage;
        private int placesEpoch;

        /// <summary>The name staff would give a spot in a candidate's description, remembered per half-metre cell (people keep moving, so the cache is trimmed when it grows).</summary>
        private string Place(Vector3 position) => PlaceIn(position, places, 4000);

        /// <summary>
        /// The same for a fixed object (a seat, a sprinkler head or pipe): the cells they stand on are few and never change, so their names are
        /// kept for the whole shift and a walking crowd's cells cannot push them out of the cache.
        /// </summary>
        private string FixedPlace(Vector3 position) => PlaceIn(position, fixedPlaces, int.MaxValue);

        private string PlaceIn(Vector3 position, Dictionary<Vector3Int, PlaceName> cache, int limit)
        {
            var stage = Train != null ? Train.Stage : TrainService.Phase.Away;
            if (stage != placesTrainStage) { placesTrainStage = stage; placesEpoch++; }
            if (cache.Count > limit) cache.Clear();
            var cell = new Vector3Int(Mathf.RoundToInt(position.x * 2), Mathf.RoundToInt(position.y), Mathf.RoundToInt(position.z * 2));
            if (cache.TryGetValue(cell, out var cached) && (!cached.TrainDependent || cached.Epoch == placesEpoch)) return cached.Text;
            var platform = Train != null ? world.Points.PlatformAt(position) : null;
            cached = new PlaceName
            {
                Text = world.Area(position) + ", " + world.Describe(position),
                TrainDependent = Train != null && (platform != null && platform.id == world.Points.Train?.platform || Train.CarAt(position) != null),
                Epoch = placesEpoch,
            };
            cache[cell] = cached;
            return cached.Text;
        }

        private static bool Settled(Passenger p) =>
            p.Current == Passenger.Activity.Sit || p.Current == Passenger.Activity.Stand || p.Current == Passenger.Activity.PlatformWait || p.Current == Passenger.Activity.InTrain ||
            p.Current == Passenger.Activity.Queue || p.Current == Passenger.Activity.Browse || p.Current == Passenger.Activity.Meet;

        /// <summary>A spot on the floor at <paramref name="position"/> (the walkable surface when there is one below).</summary>
        private static Vector3 Floor(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            return position;
        }

        // ── 역무원이 아는 것 ────────────────────────────────────────────────────

        private void LookAround()
        {
            var eye = cameraTransform.position;
            var forward = cameraTransform.forward;
            foreach (var hazard in HazardRegistry.Active)
            {
                if (known.Contains(hazard)) continue;
                float distance = Vector3.Distance(eye, hazard.Position);
                if (hazard.NeedsSight)
                {
                    if (distance > hazard.StaffSightRange || Vector3.Angle(forward, hazard.Position - eye) > 50 || !HazardRegistry.CanSee(eye, hazard)) continue;
                    Know(hazard, "직접 발견");
                }
                // 냄새·소리처럼 보지 않고도 알아채는 것은 가까이 가면 안다.
                else if (hazard.SensedAs != null && distance < hazard.NoticeRadius * .8f) Know(hazard, hazard.SensedAs);
            }
            LookAroundFacility(eye, forward);
            LookAroundEquipment(eye, forward);
            foreach (var person in crowd.Injured)
                if (!injuredKnown.Contains(person) && Vector3.Distance(eye, person.transform.position) < 6 && Vector3.Angle(forward, person.transform.position - eye) < 50)
                    CheckInjured(person);
        }

        private void Know(Hazard hazard, string how)
        {
            if (hazard == null || !known.Add(hazard)) return;
            if (!PlayerKnowsIncident)
            {
                PlayerKnowsIncident = true;
                knownAt = Time.time;
                knownHow = how;
            }
            log.Add("역무원 인지 · " + hazard.Label + " · " + hazard.Where + " (" + how + ")");
            if (hazard.Localized) session.SetMarker("incident-" + hazard.Id, hazard.Position, MarkerKind.Incident, hazard.Label);
        }

        /// <summary>
        /// A bystander can still phone about <paramref name="hazard"/>: nobody has phoned yet and the agency it calls has not
        /// been called (once the office has called it, a passenger's call changes nothing and is not offered to JEV).
        /// </summary>
        private bool CitizenMayCall(Hazard hazard) => !citizenCalled && hazard != null && hazard.CitizenCalls is Agency agency && !calledBy.ContainsKey(agency);

        /// <summary>A passenger phones 119/112 themselves; the station office then hears it back from the agency.</summary>
        public void CitizenCall(Passenger who, Hazard hazard)
        {
            if (!CitizenMayCall(hazard)) return;
            var agency = (Agency)hazard.CitizenCalls;
            citizenCalled = true;
            string number = agency == Agency.Police ? "112" : "119";
            Call(agency, "승객 " + number + " 신고");
            Office("역무실입니다. " + number + "에서 " + hazard.Named + " 신고 통보가 왔습니다. 현장 확인 바랍니다.");
            Know(hazard, "역무실 무전(" + number + " 통보)");
        }

        public void HearReport(Passenger who, Hazard hazard)
        {
            if (hazard == null) return;
            session.Hud.Toast("승객: " + hazard.ToldByPassenger, 5f);
            log.Once("report-" + hazard.Id, "승객이 역무원에게 알림 · " + hazard.Label);
            Know(hazard, "승객이 알려 줌");
        }

        public void OnPassengerInjured(Passenger person, string cause) => log.Add("승객 1명 부상 (" + cause + ") · " + world.Describe(person.transform.position));

        // ── 무전 ────────────────────────────────────────────────────────────

        private IEnumerable<EmergencySession.RadioOption> Radio()
        {
            if (!PlayerKnowsIncident || handedOver) yield break;
            foreach (var hazard in known.Where(h => !reported.Contains(h) && h.Reportable).Take(2))
            {
                var h = hazard;
                yield return Option("역무실 · " + h.ReportOption, () => Report(h));
            }
            if (reported.Count > 0 && !announced && Main != null) yield return Option("역무실 · " + Main.Announcement.Option, Announce);
            foreach (var option in CasualtyRadio()) yield return option;
            foreach (var option in FireRadio()) yield return option;
            foreach (var option in SecurityRadio()) yield return option;
            foreach (var option in TrainRadio()) yield return option;
            foreach (var option in FacilityRadio()) yield return option;
            foreach (var option in EquipmentRadio()) yield return option;
        }

        private static EmergencySession.RadioOption Option(string label, Action send) => new EmergencySession.RadioOption { Label = label, Send = send };
        private void Say(string text) => session.Hud.Radio.Push(RadioChannel.Self, text);
        private void Office(string text) => session.Hud.Radio.Push(RadioChannel.Office, text);
        private void Colleague(string text) => session.Hud.Radio.Push(RadioChannel.Colleague, text);

        private static RadioChannel ChannelOf(Agency agency) =>
            agency == Agency.Fire ? RadioChannel.Fire : agency == Agency.Police ? RadioChannel.Police : agency == Agency.Medical ? RadioChannel.Medical : RadioChannel.Colleague;

        /// <summary>The staff member reports <paramref name="hazard"/>: the office answers and sends whom it needs.</summary>
        private void Report(Hazard hazard)
        {
            reported.Add(hazard);
            log.Add("역무실에 " + hazard.Label + " 보고 · " + hazard.Where);
            Say(hazard.ReportLine);
            Office(hazard.OfficeReply);
            foreach (var agency in hazard.Dispatch) Call(agency, "역무실 신고");
            var hold = hazard.TrainHold;
            if (hold != null && Train != null) Train.Holds.Add(hold);
            Reported(hazard);
        }

        private void Announce()
        {
            announced = true;
            var pa = Main.Announcement;
            Say("역무실, 안내방송 요청합니다.");
            session.Announce(pa.Line, pa.Text);
            switch (pa.Scope)
            {
                case PaScope.ClearAround:
                {
                    // 응급 환자·출입문처럼 역 전체를 비울 일이 아니면 그 주변 사람만 물러서게 한다.
                    int cleared = 0;
                    foreach (var person in crowd.People.ToArray())
                    {
                        if (person.Hurt || person.Current == Passenger.Activity.InTrain || Vector3.Distance(person.transform.position, Main.Position) > pa.Radius) continue;
                        if (person.Focus == null) person.Notice(Main, true, "an announcement asks people to keep the area clear");
                        cleared++;
                    }
                    log.Add("안내방송 · 주변 비우기 (" + cleared + "명)");
                    break;
                }
                case PaScope.EvacuateArea:
                {
                    // 의심 물체처럼 주변만 비운다. 역 전체 대피는 경찰·소방이 판단한다.
                    int moved = crowd.Announce(Main.Position, pa.Radius, false);
                    log.Add("안내방송 · " + Main.Label + " 주변 대피 (" + moved + "명)");
                    break;
                }
                case PaScope.EvacuateStation:
                {
                    int heard = crowd.Announce();
                    log.Add("대피 안내방송 (역 안 " + heard + "명)");
                    break;
                }
                default:
                {
                    // 알리기만 하는 방송(정전·승강기 점검·오작동 안내): 들은 사람이 각자 판단한다.
                    int told = 0;
                    foreach (var person in crowd.People.ToArray())
                    {
                        if (person.Hurt || person.Hostile || person.Noticed.Contains(Main)) continue;
                        person.Notice(Main, true, "an announcement explains what is going on");
                        told++;
                    }
                    log.Add("안내방송 · " + Main.Label + " (" + told + "명에게 알림)");
                    break;
                }
            }
        }

        // ── 기관 ────────────────────────────────────────────────────────────

        private void Call(Agency agency, string by)
        {
            if (calledBy.ContainsKey(agency)) return;
            calledBy[agency] = by;
            // 실제 출동 시간은 수 분이다. 게임에서는 압축한다(분 단위 → 1~3분). 부르는 부대에 따라 다르다(Teams.Delay).
            var team = Teams.For(agency, TargetOf(agency));
            var window = Teams.Delay(team);
            arriveAt[agency] = Time.time + world.Range(window.x, window.y);
            log.Add(Teams.Name(team) + " 출동 요청 (" + by + ")");
        }

        /// <summary>The hazard an agency is coming for (not necessarily the first incident of the shift): its command first, then one it helps with.</summary>
        private Hazard TargetOf(Agency agency)
        {
            if (agency == Agency.Medical) return (Hazard)Casualties.FirstOrDefault(c => c.Active) ?? all.FirstOrDefault(h => h.Active && h.Command == agency);
            return all.FirstOrDefault(h => h.Active && h.Command == agency) ?? all.FirstOrDefault(h => h.Active && h.Involves(agency))
                ?? all.LastOrDefault(h => h.Command == agency) ?? all.LastOrDefault(h => h.Involves(agency));
        }

        private Vector3 SceneOf(Agency agency)
        {
            var target = TargetOf(agency);
            switch (agency)
            {
                case Agency.Medical: return target != null ? target.Scene : NextPatient(PlayerPosition)?.transform.position ?? (Main != null && Main.Localized ? Main.Scene : PlayerPosition);
                case Agency.Crew when target is FireHazard fire && fire.Aboard:
                    return Train.World(Train.CarAt(fire.Position)?.DoorOutside ?? fire.Position);
                case Agency.Facility when FacilityScene(out var scene):
                    return scene;
                default:
                    return target != null && target.HasScene ? target.Scene : Main != null && Main.HasScene ? Main.Scene : PlayerPosition;
            }
        }

        private void SpawnTeam(Agency agency)
        {
            var target = TargetOf(agency) ?? Main;
            var team = Teams.For(agency, target);
            var prefabs = Teams.Members(art.Crowd, team);
            var scene = SceneOf(agency);
            float standOff = agency == Agency.Police ? 9 : agency == Agency.Fire ? 4 : agency == Agency.Medical ? 1 : 3;
            // 현장이 탁자 위나 좌석 사이처럼 걸을 수 없는 곳이어도 같은 층의 걸을 수 있는 곳을 기준으로 삼는다.
            var floor = StationWorld.WalkableNear(scene);
            // 승무원은 열차에서(현장에 가장 가까운 다른 출입문으로 내려서), 다른 기관은 현장까지 실제로 걸어 닿는 가장 가까운 도시 쪽 출입구에서 들어온다.
            Vector3 entrance = scene;
            float best = float.PositiveInfinity;
            if (agency == Agency.Crew && world.Train != null && world.Train.Cars.Count > 0)
            {
                entrance = world.Train.World(world.Train.Cars[0].DoorOutside);
                foreach (var car in world.Train.Cars)
                {
                    var door = world.Train.World(car.DoorOutside);
                    if (Vector3.Distance(door, scene) < 3f) continue;
                    float walk = world.RouteLength(StationWorld.OnNavMesh(door, 1.5f), floor);
                    if (walk < best) { best = walk; entrance = door; }
                }
            }
            else
            {
                foreach (var exit in world.Points.Of(PointKind.Exit))
                {
                    float walk = world.RouteLength(StationWorld.OnNavMesh(exit.Position, 2f), floor);
                    if (walk < best) { best = walk; entrance = exit.Position; }
                }
                if (float.IsPositiveInfinity(best)) entrance = world.Points.Nearest(PointKind.Exit, scene)?.Position ?? scene;
            }
            StageVehicle(team);
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                var start = StationWorld.OnNavMesh(entrance + new Vector3(i * .8f, 0, 0), 2);
                var go = Instantiate(prefabs[i], start, Quaternion.identity, root);
                go.name = Teams.Member(team, i) + " " + (i + 1);
                var body = go.GetComponent<PersonBody>();
                if (!body.Agent.isOnNavMesh) body.Agent.Warp(start);
                var responder = go.AddComponent<Responder>();
                var goal = world.Approach(scene, start, standOff + i * 1.2f);
                responder.Setup(this, agency, team, i, goal, target, art);
                responders.Add(responder);
                if (i == 0) session.SetMarker("agency-" + agency, start, MarkerKind.Responder, Teams.Name(team));
            }
            session.Hud.Radio.Push(ChannelOf(agency), Teams.Name(team) + (agency == Agency.Crew ? " 승강장으로 갑니다." : " 부산역 도착, " + world.Describe(scene) + "(으)로 이동합니다."));
            log.Add(Teams.Name(team) + " 도착");
        }

        private readonly Dictionary<VehicleKind, GameObject> vehicles = new Dictionary<VehicleKind, GameObject>();

        /// <summary>
        /// Parks the team's vehicle on the station square in front of the main building, lights on: fire engines,
        /// ambulances, patrol cars and the special unit's van line up along the square facing 초량 (north), 10 m apart, on
        /// flat open ground (a downward ray finds the paving; a box the size of a van must be clear above it).
        /// </summary>
        private void StageVehicle(Team team)
        {
            var kind = Teams.Vehicle(team);
            if (kind == VehicleKind.None || vehicles.ContainsKey(kind)) return;
            var prefab = kind == VehicleKind.FireEngine ? art.FireEngine : kind == VehicleKind.Ambulance ? art.Ambulance : kind == VehicleKind.PoliceCar ? art.PoliceCar : art.SwatVan;
            if (prefab == null) return;
            var rotation = Quaternion.LookRotation(Vector3.forward);
            foreach (float z in VehicleRow)
            {
                var top = new Vector3(VehicleRowX, 40, z);
                if (!Physics.Raycast(top, Vector3.down, out var ground, 60, ~0, QueryTriggerInteraction.Ignore) || ground.normal.y < .95f) continue;
                var spot = ground.point;
                if (vehicles.Values.Any(v => Vector3.Distance(v.transform.position, spot) < 8.5f)) continue;
                if (Physics.CheckBox(spot + Vector3.up * 1.7f, new Vector3(1.3f, 1.4f, 3.9f), rotation, ~0, QueryTriggerInteraction.Ignore)) continue;
                var vehicle = Instantiate(prefab, spot, rotation, root);
                vehicle.name = Teams.Name(team) + " 차량";
                vehicles[kind] = vehicle;
                // 뒤에 라바콘 두 개(차 길이 절반 + 1.2 m 뒤, 좌우 0.8 m).
                if (art.TrafficConeModel != null)
                {
                    var bounds = new Bounds(spot, Vector3.zero);
                    foreach (var r in vehicle.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                    foreach (float side in new[] { -.8f, .8f })
                        Instantiate(art.TrafficConeModel, spot - Vector3.forward * (bounds.extents.z + 1.2f) + Vector3.right * side, rotation, vehicle.transform).name = "라바콘";
                }
                return;
            }
        }

        /// <summary>Where vehicles stage: a line across the square in front of the west exits (m, station frame).</summary>
        private const float VehicleRowX = -118f;
        private static readonly float[] VehicleRow = { 16, 26, 6, 36, -4, 46, -14, 56 };

        /// <summary>
        /// 출동한 팀의 표식을 선두 대원의 **지금 자리**로 옮긴다.
        /// </summary>
        /// <remarks>
        /// 표식은 팀을 만들 때(<see cref="SpawnTeam"/>)와 현장에 닿았을 때 두 번만 걸렸다. 그 사이 이동이
        /// 반영되지 않아, 지도만 보는 플레이어는 기관이 역 입구에 서 있다고 믿게 된다 — 실측에서 선두
        /// 대원이 119.5 m 떨어져 걷는 동안 표식은 0.00 m 움직였다.
        ///
        /// 팀이 없으면 **찍지 않는다.** 역 밖 출발지와 이동 경로는 구현돼 있지 않으므로 그때의 좌표는
        /// 존재하지 않는다. 없는 위치를 지어내지 않는다.
        ///
        /// 매 프레임은 과하다. 지도를 보는 사람이 끊김을 느끼지 않을 만큼만 옮긴다.
        /// </remarks>
        private void TrackTeams()
        {
            if (Time.time < nextTeamMark) return;
            nextTeamMark = Time.time + .25f;
            foreach (var responder in responders)
            {
                if (responder == null || !responder.Lead) continue;
                session.SetMarker("agency-" + responder.Agency, responder.transform.position,
                    MarkerKind.Responder, Teams.Name(responder.Team));
            }
        }

        public void OnResponderArrived(Responder responder)
        {
            // 표식은 TrackTeams 가 계속 옮긴다 — 도착할 때 따로 걸지 않는다. 두 곳에서 같은 표식을 걸면
            // 기준이 둘이 되어 한쪽만 고쳐질 때 조용히 어긋난다.
            // 승무원이 도착하면 끼인 문을 연다.
            if (responder.Agency == Agency.Crew) CrewArrived();
        }

        /// <summary>
        /// An arriving team finished its hands-on work on <paramref name="hazard"/> (a technician restarting a car, police
        /// sweeping the station, firefighters shutting a gas valve): the hazard is made safe and the team says so.
        /// </summary>
        public void OnWorked(Responder responder, Hazard hazard)
        {
            if (hazard == null || !hazard.Active) return;
            var line = hazard.Resolve(responder.Agency);
            if (!hazard.Active) HazardRegistry.Remove(hazard);
            log.Add(Teams.Name(responder.Team) + " 조치 완료 · " + hazard.Named);
            if (!string.IsNullOrEmpty(line)) session.Hud.Radio.Push(ChannelOf(responder.Agency), line);
            Resolved(hazard);
        }

        /// <summary>Family follow-ups once the office heard a report (a false alarm's receiver reset, a track stop order).</summary>
        private void Reported(Hazard hazard)
        {
            FacilityReported(hazard);
            EquipmentReported(hazard);
            TrainReported(hazard);
        }

        /// <summary>Family follow-ups once a team made a hazard safe (power back, the car running, the person brought up).</summary>
        private void Resolved(Hazard hazard)
        {
            FacilityResolved(hazard);
            EquipmentResolved(hazard);
            TrainResolved(hazard);
        }

        // ── 통제선 ──────────────────────────────────────────────────────────

        private string PlaceCordon(Hazard hazard, Vector3 centre, float radius, string label)
        {
            // 통제선 안의 사람을 먼저 밖으로 내보낸다.
            foreach (var person in crowd.People.ToArray())
            {
                var d = person.transform.position - centre;
                if (Mathf.Abs(d.y) > 3) continue;
                d.y = 0;
                if (d.magnitude < radius + 1 && !person.Hostile && !person.Hurt) person.Instruct(world.SafeExit(person.transform.position, centre, radius + 4), true);
            }
            Cordons.Place(root, centre, radius, "통제선 · " + label, art, world, this);
            staffCordons.Add((centre, radius, hazard));
            if (hazard != null) hazard.Cordoned = true;
            log.Add(label + " 주변 통제선 설치 (반경 " + radius.ToString("0") + "m) · " + world.Describe(centre));
            return "통제선을 설치했습니다 · 주변 사람들을 바깥으로 안내했습니다";
        }

        /// <summary>A marker the staff member can look at and use to cordon off <paramref name="hazard"/>.</summary>
        private HazardMarker CordonMarker(Hazard hazard, GameObject view, string name)
        {
            var marker = view.GetComponent<HazardMarker>() ?? view.AddComponent<HazardMarker>();
            marker.Hazard = hazard;
            marker.Name = name;
            marker.Prompt = () => hazard.Cordoned || handedOver ? "" : "주변 접근 통제";
            marker.Act = responder => PlaceCordon(hazard, hazard.Position, hazard.CordonRadius, hazard.Label);
            return marker;
        }

        // ── 인계와 종료 ─────────────────────────────────────────────────────

        /// <summary>
        /// The agency that takes command on scene: the fire service once it has been called for a shift that has a hazard
        /// it commands (emergency rescue control, 재난 및 안전관리 기본법 제52조), otherwise the agency commanding the first
        /// incident. A fire nobody has reported yet does not take the scene from the agency already working it.
        /// </summary>
        public Agency Commander => calledBy.ContainsKey(Agency.Fire) && all.Any(h => h.Command == Agency.Fire) ? Agency.Fire : Main != null ? Main.Command : Agency.Fire;

        public bool CanHandOver(Responder responder) => !handedOver && Stage == Phase.Incident && responder.Lead && responder.Agency == Commander;

        /// <summary>Why a team lead on scene cannot take the handover (another agency commands).</summary>
        public string HandOverRefusal(Responder responder) =>
            handedOver || Stage != Phase.Incident || responder.Agency == Commander ? "" : "현장 인계는 " + Responder.AgencyName(Commander) + "에 합니다";

        public string HandOver(Responder responder)
        {
            handedOver = true;
            string team = Teams.Name(responder.Team);
            var summary = new StringBuilder();
            summary.Append(Main != null ? Main.Named : "역 상황");
            if (PlayerKnowsIncident) summary.Append(", ").Append(session.Clock(knownAt - Time.time + session.ShiftSeconds)).Append(" 인지(").Append(knownHow).Append(")");
            foreach (var hazard in all) summary.Append(", ").Append(hazard.Handover);
            summary.Append(", 대피 안내 ").Append(log.Guided).Append("명, 부상 ").Append(crowd.Injured.Count).Append("명");
            Say(team + "에 인계합니다: " + summary + ".");
            session.Hud.Radio.Push(ChannelOf(responder.Agency), "인계받았습니다. 이후는 저희가 맡겠습니다.");
            log.Add(team + "에 현장 인계: " + summary);
            Invoke(nameof(FinishAfterHandover), 3f);
            return "현장을 " + team + "에 인계했습니다";
        }

        private void FinishAfterHandover() => Finish("현장 인계로 근무 종료", "handover");

        private void Finish(string heading, string ending)
        {
            if (Stage == Phase.Ended) return;
            Stage = Phase.Ended;
            if (!handedOver) log.Add(heading);
            if (session.Hands.Held != null) session.Hands.Drop();
            session.EndShift();
            ShiftResult.Show(session, (Main != null ? Main.Label + " · " + Main.Where + " — " : "") + heading, ending);
        }

        // ── 화면 ────────────────────────────────────────────────────────────

        private void UpdateHud()
        {
            var eye = cameraTransform.position;
            float danger = Mathf.Max(FireDanger(eye), FacilityDanger(eye), SecurityDanger(eye));
            session.Hud.SetDanger(danger);

            if (!PlayerKnowsIncident || Main == null)
            {
                session.SituationText = "평시 근무 · " + world.Describe(PlayerPosition);
                session.SituationColour = Color.white;
                return;
            }
            var focus = known.Contains(Main) ? Main : known.FirstOrDefault() ?? Main;
            session.SituationText = focus.Label + " · " + focus.Where + " · " + focus.State;
            session.SituationColour = focus.UnderControl ? FpsUiFactory.Accent : FpsUiFactory.Danger;
        }

        private BoardOverlay.Column SituationColumn()
        {
            var column = new BoardOverlay.Column { Title = "상황" };
            if (Train != null) column.Lines.Add("열차: " + Train.Status());
            if (jev == null || !jev.Available || jev.FailuresInARow >= 3) column.Lines.Add(jev != null ? jev.Status : "JEV 없음");
            if (!PlayerKnowsIncident || Main == null) { column.Lines.Add("이상 없음"); column.Lines.Add("역 순회 근무 · " + world.Describe(PlayerPosition)); return column; }
            column.Lines.Add("인지 " + session.Clock(knownAt - Time.time + session.ShiftSeconds) + " (" + knownHow + ")");
            foreach (var hazard in known.Take(5)) column.Lines.Add(hazard.Label + " — " + hazard.Where + " · " + hazard.BoardState);
            FacilityLines(column);
            return column;
        }

        private BoardOverlay.Column ActionColumn()
        {
            var column = new BoardOverlay.Column { Title = "조치" };
            column.Lines.Add((reported.Count > 0 ? "● " : "○ ") + "역무실 보고" + (reported.Count > 1 ? " " + reported.Count + "건" : ""));
            column.Lines.Add((announced ? "● " : "○ ") + "안내방송");
            column.Lines.Add("직접 대피 안내 " + log.Guided + "명");
            if (all.Any(h => h.Cordonable) || FallenCount > 0) column.Lines.Add((world.Closed.Exists(z => z.label.StartsWith("통제선", StringComparison.Ordinal)) ? "● " : "○ ") + "접근 통제");
            FireActions(column);
            TrainActions(column);
            CasualtyActions(column);
            FacilityActions(column);
            column.Lines.Add((handedOver ? "● " : "○ ") + "현장 인계");
            return column;
        }

        private BoardOverlay.Column AgencyColumn()
        {
            var column = new BoardOverlay.Column { Title = "기관" };
            if (calledBy.Count == 0) { column.Lines.Add("출동 요청 없음"); return column; }
            foreach (var pair in calledBy)
            {
                bool here = false;
                foreach (var responder in responders) if (responder.Agency == pair.Key && responder.OnScene) here = true;
                column.Lines.Add(Responder.AgencyName(pair.Key) + " · " + (here ? "현장 도착" : arrived.Contains(pair.Key) ? "도착, 이동 중" : "출동 중") + " (" + pair.Value + ")");
            }
            return column;
        }

        private BoardOverlay.Column CrowdColumn()
        {
            var column = new BoardOverlay.Column { Title = "승객" };
            column.Lines.Add("역 안 " + crowd.InStation + "명 · 열차 안 " + (crowd.People.Count - crowd.InStation) + "명");
            if (Stage != Phase.Calm)
            {
                column.Lines.Add("역 밖으로 대피 " + crowd.Evacuated + "명");
                column.Lines.Add("부상 " + crowd.Injured.Count + "명 (확인 " + injuredKnown.Count + ")");
            }
            return column;
        }

        private Passenger NearestPerson(Vector3 position, float radius, Func<Passenger, bool> filter)
        {
            Passenger best = null;
            float bestDistance = radius;
            foreach (var person in crowd.People)
            {
                if (!filter(person)) continue;
                var d = person.transform.position - position;
                if (Mathf.Abs(d.y) > 3) continue;
                float distance = d.magnitude;
                if (distance < bestDistance) { bestDistance = distance; best = person; }
            }
            return best;
        }

        /// <summary>The name staff would use on the radio for a spot anywhere in the station.</summary>
        public string Describe(Vector3 position) => world.Describe(position);
    }
}
