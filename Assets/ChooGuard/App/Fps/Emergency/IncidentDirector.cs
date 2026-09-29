using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Composes the shift's emergency instead of picking one. Every 10–16 s the game lists the atomic causal transitions
    /// the live world makes possible for specific people and things anywhere in the station — this passenger's power
    /// bank overheating in KTX car 2, that elderly passenger on platform 5·6 collapsing, a rider losing footing on the
    /// 3F escalator, a suitcase left on a bench, a door closing on a bag, the ground shaking — and JEV chooses which
    /// (if any) happens and how strongly (Choice, then Score). Common rules compute consequences; developments of what
    /// exists (fire growth, smoke, alarms, pile-ups, reports) are asked the same way. No incident type exists before it
    /// emerges from the chain. Local weights stand in when JEV is not connected. It also owns what the staff member
    /// knows, the radio to the station office, agency dispatch and arrival, and the handover that ends the shift.
    /// </summary>
    public sealed class IncidentDirector : MonoBehaviour
    {
        public enum Phase { Calm, Incident, Ended }

        public Phase Stage { get; private set; }
        public Hazard Main { get; private set; }
        public bool PlayerKnowsIncident { get; private set; }
        public Vector3 PlayerPosition => session.Player.transform.position;
        public StationWorld World => world;
        public int Rounds { get; private set; }
        public int JevRounds { get; private set; }
        /// <summary>The station fire bell rings once a detector trips and nobody silences it before the shift ends (JEV 009).</summary>
        public bool AlarmRinging => alarm;

        /// <summary>Only an active incident the player has learned about may become a navigation target.</summary>
        public bool TryGetKnownGuideTarget(out Hazard target)
        {
            target = null;
            if (!PlayerKnowsIncident || Stage != Phase.Incident) return false;
            if (Main != null && known.Contains(Main) && Main.Active && !(Main is EarthquakeHazard)) target = Main;
            if (target != null) return true;
            foreach (var hazard in known)
                if (hazard.Active && !(hazard is EarthquakeHazard)) { target = hazard; return true; }
            return false;
        }

        private EmergencySession session;
        private StationWorld world;
        private CrowdDirector crowd;
        private JevClient jev;
        private EmergencyArt art;
        private ShiftLog log;
        private Transform root;
        private TrainService Train => world.Train;

        // 합성
        private float calmUntil, nextRound, startedAt, knownAt;
        private int calmRounds, serial;
        private bool asking;
        private string knownHow = "";
        private const float DevelopmentCooldown = 40f;
        private readonly Dictionary<string, float> lastDevelopment = new Dictionary<string, float>();

        // 위험
        private readonly List<Hazard> all = new List<Hazard>();
        private readonly List<FireHazard> fires = new List<FireHazard>();
        private readonly List<SuspiciousItemHazard> bags = new List<SuspiciousItemHazard>();
        private readonly Dictionary<SuspiciousItemHazard, (float at, bool leaves)> ownerLeaves = new Dictionary<SuspiciousItemHazard, (float, bool)>();
        private readonly List<CollapseHazard> casualties = new List<CollapseHazard>();
        private readonly Dictionary<CollapseHazard, Escalator> fallOn = new Dictionary<CollapseHazard, Escalator>();
        private readonly List<DoorTrapHazard> traps = new List<DoorTrapHazard>();
        private readonly Dictionary<DoorTrapHazard, GameObject> trapMarkers = new Dictionary<DoorTrapHazard, GameObject>();
        private readonly HashSet<FireHazard> trainFires = new HashSet<FireHazard>();
        private EarthquakeHazard quake;
        private readonly List<FallingBoard> fallen = new List<FallingBoard>();
        private readonly List<EmergencyArt.HangingItem> hanging = new List<EmergencyArt.HangingItem>();
        private readonly HashSet<FallingBoard> fallenKnown = new HashSet<FallingBoard>();
        private float lastFall = -100, fireOutAt = -1, officeFollowUp = -1;
        private bool escalatorsStopped, escalatorsClosed;

        // 역무원
        private readonly HashSet<Hazard> known = new HashSet<Hazard>();
        private readonly HashSet<Hazard> reported = new HashSet<Hazard>();
        private readonly HashSet<Hazard> cordoned = new HashSet<Hazard>();
        private readonly HashSet<Hazard> reportedDone = new HashSet<Hazard>();
        private bool announced, alarm, damageReported, holdRequested, handedOver, citizenCalled;
        private readonly HashSet<Passenger> injuredKnown = new HashSet<Passenger>();
        private readonly HashSet<Passenger> treated = new HashSet<Passenger>();
        private readonly HashSet<Escalator> closedEscalators = new HashSet<Escalator>();

        // 기관
        private readonly Dictionary<Agency, float> arriveAt = new Dictionary<Agency, float>();
        private readonly Dictionary<Agency, string> calledBy = new Dictionary<Agency, string>();
        private readonly HashSet<Agency> arrived = new HashSet<Agency>();
        private readonly List<Responder> responders = new List<Responder>();

        private Transform cameraTransform;
        private Vector3 cameraRest;
        private bool wasShaking;
        private float nextSight;

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
            hanging.AddRange(art.Hanging);
            cameraTransform = session.Player.PlayerCamera.transform;
            cameraRest = cameraTransform.localPosition;
            // 첫 사건의 시각도 정해 두지 않는다. 서울발 KTX 가 들어와 사람들이 내리는 것을 볼 시간 뒤부터 묻는다.
            calmUntil = Time.time + world.Range(60, 100);
            session.RadioProviders.Add(Radio);
            session.BoardProviders.Add(SituationColumn);
            session.BoardProviders.Add(ActionColumn);
            session.BoardProviders.Add(AgencyColumn);
            session.BoardProviders.Add(CrowdColumn);
            Facilities.StationSignals.CallPointPressed += CallPoint;
        }

        private void OnDestroy()
        {
            Facilities.StationSignals.CallPointPressed -= CallPoint;
            HazardRegistry.Clear();
        }

        /// <summary>
        /// A manual call point (발신기) was pressed: the receiver rings the station bell (the same alarm the detectors raise) and
        /// the office asks for the place to be checked. People react to the bell when a fire is actually burning nearby.
        /// </summary>
        private void CallPoint(Vector3 at, string label)
        {
            var where = world.Describe(at);
            log.Add("발신기 동작 · " + where);
            if (alarm) { Office("역무실입니다. " + where + " 발신기도 동작했습니다."); return; }
            alarm = true;
            Office("역무실입니다. " + where + " 발신기 동작. 화재 여부 현장 확인 바랍니다.");
            FireHazard nearest = null;
            float best = float.PositiveInfinity;
            foreach (var fire in fires)
            {
                if (fire.Extinguished) continue;
                float d = Vector3.Distance(fire.Position, at);
                if (d < best) { best = d; nearest = fire; }
            }
            if (nearest != null) crowd.Alert(nearest.Position, 400, nearest, null, "the fire alarm bell is ringing across the station");
        }

        // ── 공개 상태 (JEV 에 보내는 관측 사실) ─────────────────────────────────

        public object PublicState()
        {
            var visible = new List<object>();
            foreach (var hazard in HazardRegistry.Active)
                visible.Add(new { what = hazard.Label, where = hazard.Where, now = hazard.Visible, seconds = Mathf.RoundToInt(Time.time - hazard.StartedAt) });
            var teams = new List<string>();
            foreach (var responder in responders) if (responder.Lead && responder.OnScene) teams.Add(Responder.AgencyName(responder.Agency));
            var areas = new Dictionary<string, int>();
            foreach (var person in crowd.People)
            {
                var area = world.Area(person.transform.position);
                areas[area] = areas.TryGetValue(area, out var n) ? n + 1 : 1;
            }
            return new
            {
                place = "KORAIL Busan Station (terminus of the Gyeongbu line): 2F concourse over the tracks, 3F shops and restaurants, 1F, station square, platforms 1–11; weekday afternoon",
                clock = session.Clock(session.ShiftSeconds),
                train = Train != null ? Train.Status() : "none",
                people_by_area = areas,
                visible_situation = visible,
                staff = new { reported = reported.Count, public_announcement = announced, cordons = cordoned.Count, fire_alarm_ringing = alarm, train_hold_requested = holdRequested },
                agencies_on_scene = teams,
            };
        }

        // ── 주기 ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (session == null || Stage == Phase.Ended) return;
            float dt = Time.deltaTime;
            foreach (var hazard in HazardRegistry.Active.ToArray()) hazard.Tick(dt);
            // 꺼진 불은 등록에서 빠지지만 남은 연기는 계속 옅어져야 한다.
            foreach (var fire in fires) if (fire.Extinguished) fire.Tick(dt);
            crowd.Shaking = quake != null && quake.Shaking;
            UpdateBags();
            UpdateConsequences();
            if (Time.time > nextRound && !asking && (Stage == Phase.Incident || Time.time > calmUntil)) Round();
            if (Time.time > nextSight) { nextSight = Time.time + .25f; LookAround(); }
            UpdateHud();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;
            bool shake = quake != null && quake.Shaking && !session.Player.IsPaused;
            if (shake)
            {
                float t = Time.time * 13f, s = quake.Strength;
                cameraTransform.localPosition = cameraRest + new Vector3(Mathf.PerlinNoise(t, 0) - .5f, Mathf.PerlinNoise(0, t) - .5f, Mathf.PerlinNoise(t, t) - .5f) * .07f * s;
                // 시점 기울기는 매 프레임 조종기의 상하 각도에서 새로 만든다(누적되지 않게).
                cameraTransform.localRotation = Quaternion.Euler(session.Player.PitchDegrees, 0, (Mathf.PerlinNoise(t * .7f, 3) - .5f) * 2.4f * s);
            }
            else if (wasShaking)
            {
                cameraTransform.localPosition = cameraRest;
                cameraTransform.localRotation = Quaternion.Euler(session.Player.PitchDegrees, 0, 0);
            }
            wasShaking = shake;
        }

        private void UpdateBags()
        {
            foreach (var bag in bags)
            {
                if (bag.Unattended) continue;
                // 가방 주인이 멀어지면 비로소 '주인 없는 가방'이 된다.
                if (ownerLeaves.TryGetValue(bag, out var plan) && plan.at > 0 && Time.time > plan.at && bag.Owner != null)
                {
                    ownerLeaves[bag] = (-1, plan.leaves);
                    var far = plan.leaves ? world.RandomExit().Position : world.AwayFrom(bag.Owner.transform.position, bag.Position, world.Range(28, 40));
                    bag.Owner.WalkOff(far, plan.leaves ? 0 : world.Range(60, 140));
                    log.Add("여행가방 곁의 승객이 가방을 두고 자리를 떴다 · " + bag.Where);
                }
                if (bag.Owner == null || Vector3.Distance(bag.Owner.transform.position, bag.Position) > 8)
                {
                    bag.MarkUnattended();
                    HazardRegistry.Add(bag);
                    log.Add("여행가방이 주인 없이 남았다 · " + bag.Where);
                }
            }
        }

        private void UpdateConsequences()
        {
            if (Stage != Phase.Incident) return;
            if (traps.Count > 0) ShowCaughtItems();
            foreach (var fire in fires)
            {
                if (fire.Extinguished) continue;
                if (!alarm && !trainFires.Contains(fire) && fire.Intensity > .55f) TriggerAlarm(fire);
                // 아무도 신고하지 않으면 결국 승객이 직접 신고한다.
                if (Time.time - fire.StartedAt > 75 && CountAware(fire) >= 4) CitizenCall(null, fire);
            }
            foreach (var bag in bags)
                if (bag.Unattended && Time.time - bag.UnattendedAt > 150 && CountAware(bag) >= 3) CitizenCall(null, bag);
            foreach (var casualty in casualties)
                if (casualty.Active && Time.time - casualty.StartedAt > 90 && CountAware(casualty) >= 2) CitizenCall(null, casualty);
            // 역무실은 감지기 동작이나 지진 뒤 일정 시간 보고가 없으면 스스로 판단해 부른다.
            if (officeFollowUp > 0 && Time.time > officeFollowUp && (quake == null || !quake.Shaking))
            {
                officeFollowUp = -1;
                if (fires.Count > 0 && !calledBy.ContainsKey(Agency.Fire))
                {
                    Call(Agency.Fire, "역무실(감지기 동작 확인)");
                    Office("역무실입니다. 화재감지기 동작 확인되어 119 신고했습니다.");
                }
                else if (quake != null && !calledBy.ContainsKey(Agency.Facility)) Call(Agency.Facility, "역무실 자체 판단");
            }
            foreach (var pair in arriveAt)
                if (!arrived.Contains(pair.Key) && Time.time >= pair.Value) { arrived.Add(pair.Key); SpawnTeam(pair.Key); break; }
            // 현장을 지휘할 기관(불은 소방, 의심 물체는 경찰, 환자는 구급, 문 끼임은 승무원, 지진은 시설)이 도착했는데
            // 역무원이 오지 않으면 결국 그 기관이 현장을 넘겨받는다. 거드는 기관은 넘겨받지 않는다.
            foreach (var responder in responders)
                if (responder.Lead && responder.OnScene && !handedOver && responder.Agency == CommandOf(Main) && Time.time - responder.OnSceneAt > 100)
                {
                    var name = Responder.AgencyName(responder.Agency);
                    Finish("인계 없이 " + name + Subject(name) + " 현장을 넘겨받음", "no_handover");
                    return;
                }
        }

        private static Agency CommandOf(Hazard hazard)
        {
            switch (hazard)
            {
                case FireHazard _: return Agency.Fire;
                case SuspiciousItemHazard _: return Agency.Police;
                case CollapseHazard _: return Agency.Medical;
                case DoorTrapHazard _: return Agency.Crew;
                default: return Agency.Facility;
            }
        }

        /// <summary>Subject particle for a Korean noun: 이 after a final consonant, 가 after a vowel.</summary>
        private static string Subject(string noun)
        {
            if (string.IsNullOrEmpty(noun)) return "가";
            char last = noun[noun.Length - 1];
            return last >= '가' && last <= '힣' && (last - '가') % 28 != 0 ? "이" : "가";
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
            public string Key, Description, Kind;
            public float Local;
            /// <summary>Magnitude scale for JEV Score (lowest first); null when the transition has no magnitude.</summary>
            public List<string> Levels;
            public Action<float> Apply;
        }

        private bool Ready(string key) => !lastDevelopment.TryGetValue(key, out var at) || Time.time - at > DevelopmentCooldown;

        private void Round()
        {
            nextRound = Time.time + (quake != null && quake.Shaking ? 4 : world.Range(10, 16));
            Rounds++;
            var candidates = Stage == Phase.Calm ? Origins(12) : Developments();
            if (Stage == Phase.Calm)
            {
                calmRounds++;
                // JEV 007: '아직 없음'은 세 번까지만 둔다. 이 근무는 훈련 근무다.
                if (calmRounds <= 3) candidates.Insert(0, new Transition { Key = "nothing_yet", Kind = "nothing", Local = .5f, Description = "Nothing happens yet; the station carries on normally for a while", Apply = _ => { } });
            }
            if (candidates.Count == 0 || candidates.Count == 1 && candidates[0].Kind == "nothing") return;
            if (jev == null || !jev.Available) { Execute(PickLocal(candidates), (float)world.Random.NextDouble(), "로컬 규칙"); return; }
            var question = new JevChoice
            {
                Id = Stage == Phase.Calm ? "what_happens" : "development",
                Instructions = Stage == Phase.Calm
                    ? "KORAIL station-staff emergency training shift in a realistic digital twin of Busan Station. Each option is something that could physically happen next to a specific person or thing present right now. Decide what happens next. Shift time " + Mathf.RoundToInt(session.ShiftSeconds) + " s."
                    : "An emergency is unfolding at Busan Station. Each option is a development the current world makes physically possible right now. Decide what happens next. Staff so far: reported=" + reported.Count + ", announcement=" + announced + ", cordons=" + cordoned.Count + ", train hold requested=" + holdRequested + ".",
            };
            foreach (var candidate in candidates) question.Criteria[candidate.Key] = candidate.Description;
            asking = true;
            StartCoroutine(jev.Ask("compose", PublicState(), new[] { question }, answers =>
            {
                if (Stage == Phase.Ended) { asking = false; return; }
                if (answers == null || !answers.TryGetValue(question.Id, out var answer)) { asking = false; Execute(PickLocal(candidates), (float)world.Random.NextDouble(), "로컬 규칙"); return; }
                var chosen = candidates.Find(c => c.Key == answer.Choice);
                if (chosen == null) { asking = false; Execute(PickLocal(candidates), (float)world.Random.NextDouble(), "로컬 규칙"); return; }
                JevRounds++;
                if (chosen.Levels == null) { asking = false; Execute(chosen, .5f, "JEV " + Mathf.RoundToInt(answer.Confidence * 100) + "%"); return; }
                AskMagnitude(chosen, answer.Confidence);
            }));
        }

        /// <summary>How strong the chosen transition is: a JEV Score on a scale the game wrote for it.</summary>
        private void AskMagnitude(Transition chosen, float choiceConfidence)
        {
            var question = new JevChoice
            {
                Id = "magnitude",
                Instructions = "This is now happening at Busan Station: " + chosen.Description + " How does it play out? Choose the level most plausible for this person and place.",
                Levels = chosen.Levels,
            };
            StartCoroutine(jev.Ask("compose-magnitude", PublicState(), new[] { question }, answers =>
            {
                asking = false;
                if (Stage == Phase.Ended) return;
                float magnitude = answers != null && answers.TryGetValue("magnitude", out var answer) ? answer.Score / Mathf.Max(1, chosen.Levels.Count - 1) : (float)world.Random.NextDouble();
                Execute(chosen, magnitude, answers != null ? "JEV " + Mathf.RoundToInt(choiceConfidence * 100) + "%·크기 " + magnitude.ToString("0.00") : "JEV·크기 로컬");
            }));
        }

        private Transition PickLocal(List<Transition> candidates)
        {
            float total = 0;
            foreach (var candidate in candidates) total += candidate.Local;
            float roll = (float)world.Random.NextDouble() * total;
            foreach (var candidate in candidates) { roll -= candidate.Local; if (roll <= 0) return candidate; }
            return candidates[0];
        }

        private void Execute(Transition transition, float magnitude, string decidedBy)
        {
            log.Composed(transition.Kind, transition.Key, magnitude, decidedBy.StartsWith("JEV", StringComparison.Ordinal), decidedBy);
            if (transition.Kind == "nothing" || transition.Kind == "no_change") return;
            lastDevelopment[transition.Key] = Time.time;
            lastDevelopment[transition.Kind] = Time.time;
            Debug.Log("CG_COMPOSE " + transition.Kind + " key=" + transition.Key + " m=" + magnitude.ToString("0.00") + " by " + decidedBy);
            transition.Apply(Mathf.Clamp01(magnitude));
        }

        private void Register(Hazard hazard)
        {
            all.Add(hazard);
            if (hazard.Active) HazardRegistry.Add(hazard);
            if (Main != null) return;
            Main = hazard;
            Stage = Phase.Incident;
            startedAt = Time.time;
            nextRound = Time.time + (hazard is EarthquakeHazard ? 3 : 12);
            Debug.Log("CG_INCIDENT_START " + hazard.Label + " at " + hazard.Where);
        }

        // 원자 전이 후보: 지금 역 안에 있는 사람·물건에서만 만든다(구역마다 골고루).
        private List<Transition> Origins(int max)
        {
            var list = new List<Transition>();
            var pools = new Dictionary<string, List<Passenger>>();
            foreach (var person in crowd.People)
            {
                if (person.Hostile || person.Hurt || !person.Body.Visible) continue;
                if (person.Current == Passenger.Activity.InTrain && (Train == null || Train.DoorsOpen < .9f || !Train.AtPlatform)) continue;
                if (!Passenger.Routine(person.Current)) continue;
                var zone = world.ZoneId(person.transform.position);
                if (!pools.TryGetValue(zone, out var pool)) pools[zone] = pool = new List<Passenger>();
                pool.Add(person);
            }
            var zones = pools.Keys.OrderBy(_ => world.Random.Next()).ToList();
            IEnumerable<Passenger> Spread(Func<Passenger, bool> filter, int count)
            {
                var picked = new List<Passenger>();
                foreach (var zone in zones)
                {
                    var match = pools[zone].Where(filter).OrderBy(_ => world.Random.Next()).FirstOrDefault();
                    if (match != null) picked.Add(match);
                    if (picked.Count >= count) break;
                }
                return picked;
            }
            bool Settled(Passenger p) => p.Current == Passenger.Activity.Sit || p.Current == Passenger.Activity.Stand || p.Current == Passenger.Activity.PlatformWait || p.Current == Passenger.Activity.InTrain || p.Current == Passenger.Activity.Queue || p.Current == Passenger.Activity.Browse || p.Current == Passenger.Activity.Meet;
            foreach (var p in Spread(p => p.CarriesPowerBank && Settled(p), 3)) list.Add(Overheat(p));
            foreach (var p in Spread(p => p.Current != Passenger.Activity.Walk || p.Elderly, 3)) list.Add(Collapse(p));
            foreach (var p in Spread(p => p.Luggage == 2 && Settled(p) && p.Current != Passenger.Activity.InTrain, 2)) list.Add(BagLeft(p));
            foreach (var escalator in world.Escalators.OrderBy(_ => world.Random.Next()))
            {
                if (!escalator.Running) continue;
                var rider = escalator.Bodies().Where(escalator.Carries).Select(b => b.GetComponent<Passenger>()).FirstOrDefault(p => p != null && !p.Hurt && !p.Hostile);
                if (rider != null) list.Add(EscalatorFall(rider, escalator));
                if (list.Count(t => t.Kind == "escalator_fall") >= 2) break;
            }
            // 닫히는 문 바로 앞(1.5 m 안)에서 아직 타지 못한 사람만 끼일 수 있다.
            if (Train != null && Train.Stage == TrainService.Phase.Closing)
                foreach (var car in Train.Cars)
                {
                    var boarding = crowd.People.FirstOrDefault(p => p.Current == Passenger.Activity.Board && !p.Body.Scripted && !p.HeldAtDoor && p.TrainSeat != null && p.TrainSeat.Car == car && Vector3.Distance(p.transform.position, Train.World(car.DoorOutside)) < 1.5f);
                    if (boarding != null) { list.Add(DoorTrap(boarding, car)); break; }
                }
            if (quake == null) list.Add(QuakeOrigin());
            // 후보가 많으면 섞어서 자른다(지진은 늘 둔다).
            var shuffled = list.Where(t => t.Kind != "quake").OrderBy(_ => world.Random.Next()).Take(max - 1).ToList();
            shuffled.AddRange(list.Where(t => t.Kind == "quake"));
            return shuffled;
        }

        private string Profile(Passenger p) =>
            "passenger #" + p.Number + " (" + (p.Body.Female ? "woman" : "man") + (p.Elderly ? ", elderly" : "") + (p.Luggage == 2 ? ", large suitcase" : p.Luggage == 1 ? ", bag" : "") + ")";

        private string Place(Vector3 position) => world.Area(position) + ", " + world.Describe(position);

        private Transition Overheat(Passenger owner) => new Transition
        {
            Key = "overheat_" + owner.Number, Kind = "overheat", Local = owner.Current == Passenger.Activity.InTrain ? .3f : .35f,
            Description = "The power bank in the bag of " + Profile(owner) + ", " + owner.Doing + " at " + Place(owner.transform.position) + ", starts to overheat.",
            Levels = new List<string> { "only a faint burning smell and a wisp of white smoke", "white smoke pouring out of the bag", "the bag bursts into small flames", "flames reach the seat or things around it", "a fierce fire with thick black smoke within seconds" },
            Apply = m => StartFire(owner, m),
        };

        private Transition Collapse(Passenger person) => new Transition
        {
            Key = "collapse_" + person.Number, Kind = "collapse", Local = person.Elderly ? .25f : .08f,
            Description = Profile(person) + ", " + person.Doing + " at " + Place(person.transform.position) + ", suddenly feels faint and collapses.",
            Levels = new List<string> { "dizzy and sits down on the floor, fully conscious", "falls down but answers when spoken to", "falls down and responds only weakly", "unconscious but breathing", "unconscious and not breathing normally" },
            Apply = m => StartCollapse(person, m),
        };

        private Transition BagLeft(Passenger owner) => new Transition
        {
            Key = "bag_" + owner.Number, Kind = "bag_left", Local = .22f,
            Description = Profile(owner) + ", " + owner.Doing + " at " + Place(owner.transform.position) + ", will get up and walk away leaving the suitcase behind.",
            Levels = new List<string> { "an absent-minded traveller who stays in the building", "walks off but lingers within the station", "walks off and heads for the exit", "walks off quickly after placing the suitcase out of the way", "hurries out of the station after looking around nervously" },
            Apply = m => StartBag(owner, m),
        };

        private Transition EscalatorFall(Passenger rider, Escalator escalator) => new Transition
        {
            Key = "fall_" + rider.Number, Kind = "escalator_fall", Local = rider.Elderly || rider.Luggage == 2 ? .22f : .08f,
            Description = Profile(rider) + " riding the " + escalator.Entry.label + " loses footing and falls on the moving steps.",
            Levels = new List<string> { "stumbles and catches the handrail", "falls and is bruised, tries to get up", "falls and cannot get up", "falls and knocks down the person behind", "a serious fall; several people pile up" },
            Apply = m => StartFall(rider, escalator, m),
        };

        private Transition DoorTrap(Passenger person, TrainService.Car car) => new Transition
        {
            Key = "door_" + car.Number, Kind = "door_trap", Local = .3f,
            Description = "The closing door of KTX " + car.Label + " at platform 5·6 catches " + Profile(person) + " who is still boarding.",
            Levels = new List<string> { "a coat hem is caught and pulled free at once", "a bag strap is caught", "a handbag is caught in the door", "a suitcase is caught in the door", "the passenger's arm is caught" },
            Apply = m => StartDoorTrap(person, car, m),
        };

        private Transition QuakeOrigin() => new Transition
        {
            Key = "quake", Kind = "quake", Local = .12f,
            Description = "An earthquake strikes; the whole station starts to shake.",
            Levels = new List<string> { "a light tremor", "moderate shaking", "strong shaking", "very strong shaking", "violent shaking" },
            Apply = StartQuake,
        };

        private List<Transition> Developments()
        {
            var list = new List<Transition> { new Transition { Key = "no_change", Kind = "no_change", Local = .45f, Description = "Nothing new happens for now", Apply = _ => { } } };
            foreach (var fire in fires)
            {
                var f = fire;
                if (!f.Extinguished)
                {
                    if (Ready("grow_" + f.Id) && f.Intensity < 1.2f)
                        list.Add(new Transition { Key = "grow_" + f.Id, Kind = "fire_grows", Local = .2f, Description = "At " + f.Where + " more of the bag's contents catch fire and the flames grow", Apply = _ => { f.Grow(.08f); log.Add("불이 커졌다 · " + f.Where); } });
                    if (Ready("smoke_" + f.Id) && f.SmokeRadius < 30)
                        list.Add(new Transition { Key = "smoke_" + f.Id, Kind = "smoke_spreads", Local = .15f, Description = "Air movement pushes the smoke from " + f.Where + " further out", Apply = _ => { f.SpreadSmoke(5); log.Add("연기가 퍼졌다 · " + f.Where); } });
                    var close = NearestPerson(f.Position, 7, p => p.Current != Passenger.Activity.Evacuate && !p.Hurt && !p.Hostile);
                    if (close != null && Ready("overcome"))
                        list.Add(new Transition { Key = "overcome_" + close.Number, Kind = "overcome", Local = .08f, Description = Profile(close) + " who stayed close to the fire at " + f.Where + " is overcome by the smoke", Apply = _ => close.Injure("화재 연기를 가까이서 들이마셨다", true) });
                    if (!alarm && !trainFires.Contains(f) && f.Intensity > .25f)
                        list.Add(new Transition { Key = "alarm_" + f.Id, Kind = "detector_alarm", Local = .15f, Description = "The automatic fire detector above " + f.Where + " triggers the alarm bell", Apply = _ => TriggerAlarm(f) });
                    if (trainFires.Contains(f) && Ready("car_empties"))
                    {
                        var car = Train.CarAt(f.Position);
                        // 아직 자리에 앉아 있는 사람이 있을 때만(이미 비었으면 일어날 일이 없다).
                        if (car != null && car.Seats.Exists(seat => seat.Taken != null && !seat.Taken.Hurt))
                            list.Add(new Transition { Key = "car_empties_" + car.Number, Kind = "car_empties", Local = .3f, Description = "Passengers in KTX " + car.Label + " see the smoke and hurry out onto platform 5·6", Apply = _ => EmptyCar(car, f) });
                    }
                }
                else if (fireOutAt > 0 && Time.time - fireOutAt < 40 && !arrived.Contains(Agency.Fire) && Ready("rekindle"))
                    list.Add(new Transition { Key = "rekindle_" + f.Id, Kind = "rekindle", Local = .08f, Description = "Embers inside the burnt bag at " + f.Where + " rekindle into small flames", Apply = _ => Rekindle(f) });
            }
            foreach (var bag in bags)
            {
                if (!bag.Unattended) continue;
                var b = bag;
                var curious = NearestPerson(b.Position, 15, p => !p.Hostile && !p.Hurt && p.Current != Passenger.Activity.Evacuate && p.Noticed.Contains(b));
                if (curious != null && !b.Touched && !cordoned.Contains(b) && Ready("curious_passenger"))
                    list.Add(new Transition { Key = "curious_" + curious.Number, Kind = "curious_passenger", Local = .12f, Description = "A curious passenger walks up to the unattended suitcase at " + b.Where + " to check whose it is", Apply = _ => CuriousPassenger(curious, b) });
                var reporter = NearestPerson(b.Position, 30, p => !p.Hostile && !p.Hurt && p.Noticed.Contains(b) && p.Current != Passenger.Activity.Report && p.Current != Passenger.Activity.Evacuate);
                if (reporter != null && !known.Contains(b) && Ready("passenger_reports"))
                    list.Add(new Transition { Key = "report_" + reporter.Number, Kind = "passenger_reports", Local = .2f, Description = "A passenger who noticed the suitcase at " + b.Where + " goes to tell the station staff member", Apply = _ => { reporter.ReportToStaff(); log.Add("가방을 본 승객이 역무원에게 알리러 감"); } });
            }
            foreach (var casualty in casualties)
            {
                if (!casualty.Active || casualty.Person == null) continue;
                var c = casualty;
                var helper = NearestPerson(c.Position, 12, p => !p.Hostile && !p.Hurt && !p.Helping && p.Current != Passenger.Activity.Evacuate && p.Current != Passenger.Activity.InTrain && p != c.Person);
                // 곁에서 돕는 사람은 둘까지(모두가 모여들지 않는다).
                int helping = crowd.People.Count(p => p.Helping && p.Focus == c);
                if (helper != null && helping < 2 && Ready("bystander_helps"))
                    list.Add(new Transition { Key = "help_" + helper.Number, Kind = "bystander_helps", Local = .18f, Description = "A passenger nearby goes over to help the person who " + c.Cause + " at " + c.Where, Apply = _ => { helper.HelpNearby(c); log.Add("주변 승객이 " + c.Cause + " 승객을 도우러 감 · " + c.Where); } });
                if (!known.Contains(c) && helper != null && Ready("passenger_reports"))
                    list.Add(new Transition { Key = "creport_" + helper.Number, Kind = "passenger_reports", Local = .15f, Description = "A passenger runs to fetch the station staff member for the person who " + c.Cause + " at " + c.Where, Apply = _ => { helper.ReportToStaff(); log.Add("승객이 역무원을 부르러 감 · " + c.Where); } });
                if (!citizenCalled && Ready("citizen_calls_119"))
                    list.Add(new Transition { Key = "call119_" + c.Id, Kind = "citizen_calls_119", Local = .12f, Description = "Someone near " + c.Where + " calls 119 for the person who " + c.Cause, Apply = _ => CitizenCall(null, c) });
                if (fallOn.TryGetValue(c, out var escalator) && escalator.Running)
                {
                    list.Add(new Transition { Key = "estop_" + escalator.Entry.id, Kind = "escalator_stopped", Local = .35f, Description = "A bystander presses the emergency stop button of the " + escalator.Entry.label, Apply = _ => { escalator.Stop("비상정지 버튼(승객)"); log.Add(escalator.Label + " 비상 정지(승객이 버튼을 누름)"); } });
                    var behind = escalator.Behind(c.Person.Body)?.GetComponent<Passenger>();
                    if (behind != null && !behind.Hurt && Ready("pileup"))
                        list.Add(new Transition { Key = "pileup_" + behind.Number, Kind = "pileup", Local = .1f, Description = "The moving steps carry " + Profile(behind) + " into the fallen person and they fall too", Apply = _ => { escalator.Fall(behind.Body); behind.Injure("에스컬레이터에서 앞사람에 걸려 넘어짐"); } });
                }
            }
            foreach (var trap in traps)
            {
                // 문이 실제로 닫혀 끼인 뒤에만 빼내기·승무원 확인이 일어난다(출발 경고 중에는 아직 열려 있다).
                if (!trap.Active || !trapShut.Contains(trap)) continue;
                var t = trap;
                if (!calledBy.ContainsKey(Agency.Crew) && Ready("crew_notices"))
                    list.Add(new Transition { Key = "crew_" + t.Id, Kind = "crew_notices", Local = .25f, Description = "The KTX conductor notices the door fault light on " + t.Car.Label + " and heads there", Apply = _ => { Call(Agency.Crew, "열차 승무원 자체 확인"); Colleague("열차팀장입니다. " + t.Car.Label + " 출입문 이상 확인하러 갑니다."); } });
                if (Ready("pulls_free"))
                    list.Add(new Transition { Key = "free_" + t.Id, Kind = "pulls_free", Local = .12f, Description = "Other passengers pull the " + t.Car.Label + " door leaves apart enough to free the caught " + (t.Injures ? "arm" : "belongings"), Apply = _ => FreeTrap(t, "승객들이 문을 벌려 끼인 것을 빼냄") });
            }
            if (quake != null && quake.Shaking && hanging.Count > 0 && fallen.Count < 3 && Time.time - lastFall > 5)
                list.Add(new Transition { Key = "board_falls", Kind = "board_falls", Local = .45f, Description = "A hanging board above a crowded part of the concourse comes loose and falls", Apply = _ => DropBoard() });
            if (quake != null && !quake.Shaking)
            {
                if (quake.Aftershocks == 0 && Time.time > quake.ShakeUntil + 40)
                    list.Add(new Transition
                    {
                        Key = "aftershock", Kind = "aftershock", Local = .12f, Description = "An aftershock shakes the station briefly",
                        Apply = _ =>
                        {
                            quake.Aftershock(world.Range(6, 10), world.Range(.4f, .6f));
                            foreach (var person in crowd.People) person.Noticed.Remove(quake);
                            log.Add("여진");
                        },
                    });
                if (!escalatorsStopped)
                    list.Add(new Transition { Key = "escalators_stop", Kind = "escalators_stop", Local = .25f, Description = "The escalators and elevators halt after the quake; people crowd at the landings and a few are left inside elevators", Apply = _ => StopEscalators() });
            }
            // 이미 벌어진 일과 별개로 새 일이 겹칠 수도 있다: 근무당 한 번, 이미 있는 종류는 빼고, 낮은 가중치로.
            if (!separateHappened)
            {
                var origin = Origins(4).FirstOrDefault(t => t.Kind != "quake" && !Present(t.Kind));
                if (origin != null)
                {
                    origin.Local = .03f;
                    origin.Description = "Separately, " + origin.Description;
                    var apply = origin.Apply;
                    origin.Apply = m => { separateHappened = true; apply(m); };
                    list.Add(origin);
                }
            }
            return list;
        }

        private bool separateHappened;

        private bool Present(string kind)
        {
            switch (kind)
            {
                case "overheat": return fires.Count > 0;
                case "collapse": return casualties.Exists(c => !fallOn.ContainsKey(c));
                case "escalator_fall": return fallOn.Count > 0;
                case "bag_left": return bags.Count > 0;
                case "door_trap": return traps.Count > 0;
                default: return false;
            }
        }

        // ── 전이 적용 ────────────────────────────────────────────────────────

        private void StartFire(Passenger owner, float magnitude)
        {
            bool aboard = owner.Current == Passenger.Activity.InTrain && Train != null;
            Vector3 spot;
            if (aboard) spot = owner.transform.position + owner.transform.right * .45f + owner.transform.forward * .35f;
            else
            {
                var side = owner.transform.right * (world.Chance(.5f) ? .5f : -.5f) + owner.transform.forward * .3f;
                spot = StationWorld.OnNavMesh(owner.transform.position + side, 1f);
                if (Physics.Raycast(spot + Vector3.up, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) spot = hit.point;
            }
            if (aboard)
            {
                var car = Train.CarAt(owner.transform.position);
                spot.y = (car != null ? car.Entry.floor : .45f) + Train.Carrier.position.y;
            }
            var fire = new FireHazard("fire-" + ++serial, spot, "승객 가방 속 보조배터리", .05f + .35f * magnitude * magnitude, art, aboard ? Train.Carrier : root) { Where = world.Describe(spot) };
            fires.Add(fire);
            if (aboard) { trainFires.Add(fire); Train.Holds.Add("차내 화재"); }
            Register(fire);
            log.Add("화재 · " + fire.Where + " — 승객 가방 속 보조배터리 과열 (" + fire.Visible + ")");
            owner.Notice(fire, false);
        }

        private void StartCollapse(Passenger person, float magnitude)
        {
            string[] states = { "어지러워하며 주저앉음(의식 있음)", "쓰러졌으나 부르면 대답함", "쓰러져 반응이 약함", "의식을 잃고 쓰러짐(숨은 쉼)", "의식을 잃고 숨이 고르지 않음" };
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var casualty = new CollapseHazard("collapse-" + ++serial, person, "응급 환자", "쓰러진", "승객이 " + states[level]) { Where = world.Describe(person.transform.position), Level = level };
            casualties.Add(casualty);
            // 0단계(어지러워 주저앉음)만 의식이 있어 웅크리고, 그 위로는 바닥에 쓰러진다.
            person.Injure("갑자기 쓰러짐 · " + states[level], level > 0);
            if (person.Current == Passenger.Activity.InTrain && Train != null) Train.Holds.Add("차내 응급환자");
            Register(casualty);
            log.Add("응급 환자 · " + casualty.Where + " — " + states[level]);
        }

        private void StartFall(Passenger rider, Escalator escalator, float magnitude)
        {
            escalator.Fall(rider.Body);
            var casualty = new CollapseHazard("fall-" + ++serial, rider, "에스컬레이터 넘어짐", "에스컬레이터에서 넘어진", magnitude < .2f ? "에스컬레이터에서 휘청였다가 손잡이를 잡음" : "에스컬레이터 계단에 넘어진 승객") { Where = escalator.Label };
            casualties.Add(casualty);
            fallOn[casualty] = escalator;
            rider.Injure("에스컬레이터에서 넘어짐");
            if (magnitude > .7f)
            {
                var behind = escalator.Behind(rider.Body)?.GetComponent<Passenger>();
                if (behind != null) { escalator.Fall(behind.Body); behind.Injure("앞사람과 함께 넘어짐"); }
            }
            if (magnitude > .9f) { escalator.Stop("비상정지 버튼(승객)"); log.Add(escalator.Label + " 비상 정지"); }
            Register(casualty);
            log.Add("에스컬레이터 넘어짐 · " + escalator.Label);
        }

        private void StartBag(Passenger owner, float magnitude)
        {
            var spot = owner.transform.position + owner.transform.right * .55f;
            if (Physics.Raycast(spot + Vector3.up, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) spot = hit.point;
            var bag = new SuspiciousItemHazard("bag-" + ++serial, spot, owner.transform.rotation * Quaternion.Euler(0, 90, 0), owner, art, root) { Where = world.Describe(spot) };
            bags.Add(bag);
            ownerLeaves[bag] = (Time.time + world.Range(12, 30), magnitude >= .5f);
            var marker = bag.View.GetComponent<HazardMarker>();
            marker.Prompt = () => cordoned.Contains(bag) ? "" : "주변 접근 통제";
            marker.Act = responder => PlaceCordon(bag, bag.Position, 6f, "방치 가방");
            // 가방은 주인이 떠나야 위험이 된다: 사건의 시작으로 기록은 하되 등록은 주인이 멀어질 때.
            all.Add(bag);
            if (Main == null) { Main = bag; Stage = Phase.Incident; startedAt = Time.time; nextRound = Time.time + 12; }
        }

        private void StartDoorTrap(Passenger person, TrainService.Car car, float magnitude)
        {
            string[] what = { "외투 자락", "가방 끈", "손가방", "여행가방", "팔" };
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var door = Train.World(car.DoorOutside);
            var trap = new DoorTrapHazard("door-" + ++serial, car, person, what[level], level == 4, door) { Where = world.Describe(door) };
            traps.Add(trap);
            // 문은 닫히다 끼인 것에 걸려 그 두께만큼 열린 채 멈춘다(열차는 떠나지 못한다). 폭은 문 열림 대비 비율.
            float[] gap = { .04f, .04f, .14f, .28f, .12f };
            car.Jam = Mathf.Max(car.Jam, gap[level]);
            trapItems[trap] = level;
            Train.Holds.Add(TrainService.DoorJamHold);
            person.HoldAtDoor(door);
            var marker = new GameObject("출입문 끼임 · " + car.Label, typeof(SphereCollider)).AddComponent<HazardMarker>();
            marker.transform.SetParent(root, false);
            marker.transform.position = door + Vector3.up * 1f;
            marker.GetComponent<SphereCollider>().radius = .6f;
            marker.Hazard = trap;
            marker.Name = "KTX " + car.Label + " 출입문 · " + what[level] + " 끼임";
            marker.Prompt = () => trap.Active && trapShut.Contains(trap) ? "끼인 " + what[level] + " 빼내기" : "";
            marker.Act = responder => FreeTrap(trap, "역무원이 끼인 " + what[level] + "을 빼냄");
            trapMarkers[trap] = marker.gameObject;
            Register(trap);
            log.Add("출입문 끼임 · KTX " + car.Label + " — " + what[level]);
        }

        private string FreeTrap(DoorTrapHazard trap, string how)
        {
            if (!trap.Active) return "";
            trap.Free();
            HazardRegistry.Remove(trap);
            if (!traps.Exists(t => t.Active && t.Car == trap.Car)) trap.Car.Jam = 0;
            if (!traps.Exists(t => t.Active)) Train.Holds.Remove(TrainService.DoorJamHold);
            // 끼인 것을 빼려면 문을 다시 연다. 그 뒤 문이 다시 닫히고 열차가 출발한다.
            Train.Reopen(5);
            trap.Person?.ReleaseFromDoor(trap.Injures);
            if (trapMarkers.TryGetValue(trap, out var marker)) { Destroy(marker); trapMarkers.Remove(trap); }
            if (trapProps.TryGetValue(trap, out var prop)) { Destroy(prop); trapProps.Remove(trap); }
            log.Add(how + " · " + trap.Car.Label);
            return trap.Injures ? "끼인 팔을 빼냈습니다 · 다친 승객을 살펴 주세요" : "끼인 것을 빼냈습니다 · 문을 다시 열고 닫습니다";
        }

        private readonly Dictionary<DoorTrapHazard, int> trapItems = new Dictionary<DoorTrapHazard, int>();
        private readonly Dictionary<DoorTrapHazard, GameObject> trapProps = new Dictionary<DoorTrapHazard, GameObject>();

        private readonly HashSet<DoorTrapHazard> trapShut = new HashSet<DoorTrapHazard>();

        /// <summary>
        /// The moment the leaves close onto what is caught: from then on it can be freed, and the item shows in the gap
        /// (nothing to show for an arm: the person is there).
        /// </summary>
        private void ShowCaughtItems()
        {
            foreach (var trap in traps)
            {
                if (!trap.Active || trapShut.Contains(trap) || Train.DoorsOpen > trap.Car.Jam + .02f) continue;
                trapShut.Add(trap);
                if (!trapItems.TryGetValue(trap, out int level) || level == 4) continue;
                var gap = Train.DoorGap(trap.Car, out var outward, out var along);
                var floor = Train.World(trap.Car.DoorInside).y;
                var item = new GameObject("끼인 물건 · " + trap.Car.Label);
                item.transform.SetPositionAndRotation(new Vector3(gap.x, floor, gap.z), Quaternion.LookRotation(outward, Vector3.up));
                // 로컬 x = 문틈 방향(얇게), z = 바깥쪽(승강장으로 삐져나옴).
                switch (level)
                {
                    case 0: Props.Model(item.transform, "외투 자락", art.CoatFlap, art.Coat); break;
                    case 1:
                    {
                        // 가방 끈이 문틈에 물려, 끈 끝의 손가방이 승강장 쪽으로 늘어져 있다.
                        var bag = Instantiate(art.HandbagModel, item.transform);
                        bag.name = "손가방";
                        bag.transform.localPosition = new Vector3(0, .3f, .24f);
                        Props.Belt(item.transform, item.transform.TransformPoint(new Vector3(0, 1.02f, -.03f)), item.transform.TransformPoint(new Vector3(0, .63f, .24f)), art.Leather, .025f, 1f, "가방 끈");
                        break;
                    }
                    // 손잡이가 문틈에 물려 손가방이 문 바깥에 매달려 있다.
                    case 2: Instantiate(art.HandbagModel, item.transform).transform.localPosition = new Vector3(0, .48f, .06f); break;
                    default: Props.Suitcase(item.transform, item.transform.position, item.transform.rotation * Quaternion.Euler(0, 90, 0), art); break;
                }
                item.transform.SetParent(Train.Carrier, true);
                trapProps[trap] = item;
            }
        }

        private void StartQuake(float magnitude)
        {
            quake = new EarthquakeHazard("quake-" + ++serial, new Vector3(64, 7, -2), 10 + 18 * magnitude, .45f + .55f * magnitude) { Where = "역 전체" };
            Register(quake);
            nextRound = Time.time + 3;
            log.Add("지진 · 역사 전체가 흔들리기 시작 (세기 " + quake.Strength.ToString("0.0") + ")");
            Know(quake, "흔들림을 직접 느낌");
            officeFollowUp = Time.time + 150;
            // 흔들리는 동안 열차는 출발하지 않는다.
            if (Train != null) Train.Holds.Add("지진");
        }

        private void EmptyCar(TrainService.Car car, FireHazard fire)
        {
            int count = 0;
            foreach (var seat in car.Seats)
                if (seat.Taken != null) { seat.Taken.Notice(fire, true, "smoke is filling the car"); seat.Taken.BeginAlight(true); count++; }
            log.Add("KTX " + car.Label + " 승객 " + count + "명이 연기를 보고 승강장으로 내림");
        }

        private void TriggerAlarm(FireHazard fire)
        {
            if (alarm) return;
            alarm = true;
            // 수신기의 화재 신호: 피난 경로의 자동문과 개집표기가 연동해 열린다(자료 D1·G1, JEV 010).
            Facilities.StationSignals.FireAlarm = true;
            log.Add("자동화재탐지설비 동작 · 비상벨 · " + fire.Where);
            Office("역무실입니다. " + fire.Where + " 화재감지기 동작. 현장 확인 바랍니다.");
            Know(fire, "화재감지기 동작 무전");
            crowd.Alert(fire.Position, 400, fire, null, "the fire alarm bell is ringing across the station");
            if (!calledBy.ContainsKey(Agency.Fire)) officeFollowUp = Time.time + 30;
        }

        private void Rekindle(FireHazard old)
        {
            var position = old.Position;
            var fire = new FireHazard("fire-" + ++serial, position, "꺼진 줄 알았던 가방", .1f, art, trainFires.Contains(old) ? Train.Carrier : root) { Where = old.Where };
            fires.Add(fire);
            if (trainFires.Contains(old)) trainFires.Add(fire);
            HazardRegistry.Add(fire);
            all.Add(fire);
            fireOutAt = -1;
            log.Add("꺼졌던 가방에서 다시 불꽃이 일었다 · " + fire.Where);
        }

        private void CuriousPassenger(Passenger person, SuspiciousItemHazard bag)
        {
            log.Add("승객 한 명이 가방을 확인하러 다가갔다 · " + bag.Where);
            person.MoveAway(0);
            person.Body.GoTo(StationWorld.OnNavMesh(bag.Position + (person.transform.position - bag.Position).normalized * .7f, 1), 1.2f);
            StartCoroutine(TouchWhenClose(person, bag));
        }

        private System.Collections.IEnumerator TouchWhenClose(Passenger person, SuspiciousItemHazard bag)
        {
            float until = Time.time + 25;
            while (person != null && Time.time < until && !cordoned.Contains(bag) && !person.Instructed)
            {
                if (Vector3.Distance(person.transform.position, bag.Position) < 1.1f)
                {
                    person.Body.Stop();
                    person.Body.SetCrouch(true);
                    bag.Touched = true;
                    log.Add("승객이 방치된 가방을 만졌다");
                    yield return new WaitForSeconds(4);
                    if (person != null) { person.Body.SetCrouch(false); person.Watch(10, false); }
                    yield break;
                }
                yield return new WaitForSeconds(.3f);
            }
        }

        private void DropBoard()
        {
            // 사람이 많은 곳 위의 안내판부터 흔들린다(무게·고정 상태는 알 수 없으므로 사람 밀도로만 고른다).
            EmergencyArt.HangingItem pick = null;
            int best = -1;
            foreach (var item in hanging)
            {
                int people = crowd.CountNear(new Vector3(item.Centre.x, 7, item.Centre.z), 8, null) + world.Random.Next(3);
                if (people > best) { best = people; pick = item; }
            }
            if (pick == null) return;
            hanging.Remove(pick);
            lastFall = Time.time;
            var board = FallingBoard.Drop(pick, root, 7.02f, world.Random);
            board.OnLanded += Landed;
            fallen.Add(board);
        }

        private void Landed(FallingBoard board)
        {
            float radius = Mathf.Max(board.Item.Size.x, board.Item.Size.z) * .5f + .4f;
            string where = world.Describe(board.Impact);
            log.Add(board.Item.Label + " 낙하 · " + where);
            foreach (var person in crowd.People.ToArray())
            {
                var d = person.transform.position - board.Impact;
                if (Mathf.Abs(d.y) > 3) continue;
                d.y = 0;
                if (d.magnitude < radius) person.Injure(board.Item.Label + "에 맞았다");
                else if (d.magnitude < radius + 6 && quake != null) person.Notice(quake, true);
            }
            world.Closed.Add((board.Impact, radius + .5f, board.Item.Label));
            var marker = board.gameObject.AddComponent<HazardMarker>();
            marker.Name = "떨어진 " + board.Item.Label;
            marker.Prompt = () => world.Closed.Exists(z => z.label == "통제선 · " + board.Item.Label) ? "" : "주변 접근 통제";
            marker.Act = responder => PlaceCordon(null, board.Impact, radius + 1.5f, board.Item.Label);
        }

        private void StopEscalators()
        {
            escalatorsStopped = true;
            foreach (var escalator in world.Escalators) escalator.Stop("지진 감지");
            int trapped = 0;
            foreach (var elevator in world.Elevators) { elevator.Stop(); trapped += elevator.Inside.Count; }
            log.Add("에스컬레이터·엘리베이터가 지진 감지로 정지" + (trapped > 0 ? " · 엘리베이터 안에 " + trapped + "명" : ""));
            Office("역무실입니다. 에스컬레이터와 엘리베이터 전 대 지진 감지로 정지했습니다." + (trapped > 0 ? " 엘리베이터 안에 승객이 갇혀 있습니다." : ""));
        }

        // ── 역무원이 아는 것 ────────────────────────────────────────────────────

        private void LookAround()
        {
            var eye = cameraTransform.position;
            var forward = cameraTransform.forward;
            foreach (var hazard in HazardRegistry.Active)
            {
                if (known.Contains(hazard) || !hazard.NeedsSight) continue;
                float distance = Vector3.Distance(eye, hazard.Position);
                float range = hazard is SuspiciousItemHazard ? 8 : hazard is CollapseHazard ? 14 : hazard.NoticeRadius * 1.2f;
                if (distance > range || Vector3.Angle(forward, hazard.Position - eye) > 50 || !HazardRegistry.CanSee(eye, hazard)) continue;
                Know(hazard, "직접 발견");
            }
            foreach (var board in fallen)
                if (board.Landed && !fallenKnown.Contains(board) && Vector3.Distance(eye, board.Impact) < 25 && Vector3.Angle(forward, board.Impact - eye) < 60)
                {
                    fallenKnown.Add(board);
                    log.Once("fallen-" + board.GetInstanceID(), "역무원이 떨어진 " + board.Item.Label + "을(를) 확인");
                    session.SetMarker("fallen-" + board.GetInstanceID(), board.Impact, MarkerKind.Incident, "낙하물");
                }
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
            if (hazard.NeedsSight) session.SetMarker("incident-" + hazard.Id, hazard.Position, MarkerKind.Incident, hazard.Label);
        }

        /// <summary>A passenger phones 119/112 themselves; the station office then hears it back from the agency.</summary>
        public void CitizenCall(Passenger who, Hazard hazard)
        {
            if (citizenCalled || hazard == null || hazard is EarthquakeHazard || hazard is DoorTrapHazard) return;
            var agency = hazard is FireHazard ? Agency.Fire : hazard is CollapseHazard ? Agency.Medical : Agency.Police;
            if (calledBy.ContainsKey(agency)) return;
            citizenCalled = true;
            string number = agency == Agency.Police ? "112" : "119";
            Call(agency, "승객 " + number + " 신고");
            Office("역무실입니다. " + number + "에서 " + Named(hazard) + " 신고 통보가 왔습니다. 현장 확인 바랍니다.");
            Know(hazard, "역무실 무전(" + number + " 통보)");
        }

        public void HearReport(Passenger who, Hazard hazard)
        {
            if (hazard == null) return;
            string line = hazard is FireHazard ? "저기 " + hazard.Where + " 쪽에서 연기가 나요!" :
                hazard is SuspiciousItemHazard ? hazard.Where + "에 주인 없는 가방이 한참 놓여 있어요." :
                hazard is CollapseHazard ? hazard.Where + "에 사람이 쓰러졌어요! 빨리 와 주세요." : "여기 좀 봐 주세요!";
            session.Hud.Toast("승객: " + line, 5f);
            log.Once("report-" + hazard.Id, "승객이 역무원에게 알림 · " + hazard.Label);
            Know(hazard, "승객이 알려 줌");
        }

        public string CheckInjured(Passenger person)
        {
            if (injuredKnown.Add(person))
            {
                log.Add("역무원이 부상 승객을 확인 · " + world.Describe(person.transform.position));
                session.SetMarker("injured-" + person.Number, person.transform.position, MarkerKind.Task, "부상자");
                foreach (var casualty in casualties) if (casualty.Person == person) Know(casualty, "부상자 직접 확인");
            }
            if (treated.Contains(person)) return "구급대원이 처치 중입니다";
            // 부르면 대답하지 못하는 사람은 말 대신 보이는 상태를 알려 준다(반응·호흡).
            var collapse = CollapseOf(person);
            if (collapse != null && collapse.Level >= 2) return collapse.Visible;
            return "부상 승객: 숨쉬기가 힘들어요… 움직이기가 어려워요.";
        }

        /// <summary>The active collapse or fall casualty of <paramref name="person"/>, or null.</summary>
        public CollapseHazard CollapseOf(Passenger person) => casualties.Find(c => c.Active && c.Person == person);

        /// <summary>
        /// The staff member set the AED down at <paramref name="at"/>. Beside a collapsed person (2.5 m, same floor) it waits for
        /// the paramedics: the first delivery goes to the shift log with the time since the collapse (JEV 011 carry_and_hand_over,
        /// logged_only). Returns the feedback line, or null when nobody is near.
        /// </summary>
        public string AedSetDown(Vector3 at)
        {
            CollapseHazard best = null;
            float bestDistance = 2.5f;
            foreach (var casualty in casualties)
            {
                if (!casualty.Active || casualty.Person == null) continue;
                var d = casualty.Person.transform.position - at;
                if (Mathf.Abs(d.y) > 1.5f) continue;
                d.y = 0;
                if (d.magnitude < bestDistance) { bestDistance = d.magnitude; best = casualty; }
            }
            if (best == null) return null;
            if (best.AedAt >= 0) return "AED 가 이미 환자 곁에 있습니다";
            best.AedAt = Time.time;
            log.Add("AED 를 환자 곁에 둠 · 쓰러진 지 " + Mathf.RoundToInt(best.AedAt - best.StartedAt) + "초 · " + best.Where);
            return treated.Contains(best.Person) ? "AED 를 구급대원 곁에 두었습니다" : "AED 를 환자 곁에 두었습니다 · 구급대에 인계합니다";
        }

        public void OnPassengerInjured(Passenger person, string cause) => log.Add("승객 1명 부상 (" + cause + ") · " + world.Describe(person.transform.position));

        public void OnFireOut(FireHazard hazard, string by)
        {
            if (!hazard.Extinguished) return;
            HazardRegistry.Remove(hazard);
            if (!fires.Exists(f => !f.Extinguished))
            {
                fireOutAt = Time.time;
                if (Train != null && !trainFires.Any(f => !f.Extinguished)) Train.Holds.Remove("차내 화재");
            }
            log.Add("불이 꺼졌다 (" + by + ") · " + hazard.Where);
        }

        /// <summary>Nearest untreated injured person; <paramref name="inTrain"/> false skips people still inside a KTX car.</summary>
        public Passenger NextPatient(Vector3 from, bool inTrain = true)
        {
            Passenger best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (var person in crowd.Injured)
            {
                if (person == null || treated.Contains(person)) continue;
                if (!inTrain && person.Current == Passenger.Activity.InTrain) continue;
                float d = Vector3.Distance(from, person.transform.position);
                if (d < bestDistance) { bestDistance = d; best = person; }
            }
            return best;
        }

        public void OnTreated(Passenger person)
        {
            if (!treated.Add(person)) return;
            log.Add("구급대가 부상 승객 1명을 처치");
            foreach (var casualty in casualties)
            {
                if (casualty.Person != person || !casualty.Active) continue;
                if (casualty.AedAt >= 0) log.Add("구급대 인계 · 역무원이 곁에 둔 AED 포함");
                casualty.End();
                HazardRegistry.Remove(casualty);
            }
            if (Train != null && !casualties.Exists(c => c.Active && c.Person != null && c.Person.Current == Passenger.Activity.InTrain)) Train.Holds.Remove("차내 응급환자");
        }

        // ── 무전 ────────────────────────────────────────────────────────────

        private IEnumerable<EmergencySession.RadioOption> Radio()
        {
            if (!PlayerKnowsIncident || handedOver) yield break;
            foreach (var hazard in known.Where(h => !reported.Contains(h)).Take(2))
            {
                var h = hazard;
                yield return Option(h is EarthquakeHazard ? "역무실 · 지진 흔들림 보고" : "역무실 · " + Named(h) + " 보고", () => Report(h));
            }
            if (reported.Count > 0 && !announced)
                yield return Option("역무실 · " + (Main is CollapseHazard || Main is DoorTrapHazard ? "안내방송(통로 비우기) 요청" : Main is SuspiciousItemHazard ? "안내방송(주변 대피) 요청" : "대피 안내방송 요청"), Announce);
            int injuries = 0;
            foreach (var person in injuredKnown) if (person != null && person.Hurt && !treated.Contains(person)) injuries++;
            if (injuries > 0 && !calledBy.ContainsKey(Agency.Medical))
                yield return Option("역무실 · 부상자 " + injuries + "명, 119 구급 요청", () =>
                {
                    Say("역무실, 부상 승객 " + injuries + "명 있습니다. 구급대 요청합니다.");
                    Office("역무실 수신. 119 구급 요청하겠습니다.");
                    Call(Agency.Medical, "역무원 요청");
                });
            if (quake != null && fallenKnown.Count > 0 && !damageReported)
                yield return Option("역무실 · 천장 낙하물 피해 보고", () =>
                {
                    damageReported = true;
                    Say("역무실, 천장 안내판 " + fallenKnown.Count + "곳 낙하했습니다.");
                    Office("역무실 수신. 시설 담당 보내겠습니다. 낙하 지점 접근 통제 바랍니다.");
                    log.Add("역무실에 낙하물 피해 보고");
                    Call(Agency.Facility, "역무원 보고");
                });
            var involved = InvolvedEscalators();
            if (involved.Count > 0 && !escalatorsClosed)
                yield return Option("역무실 · " + (involved.Count == 1 ? involved[0].Label : "에스컬레이터") + " 운행 정지·통제 요청", () =>
                {
                    escalatorsClosed = true;
                    foreach (var escalator in involved) { escalator.Stop("운행 정지·통제"); escalator.Close(); closedEscalators.Add(escalator); }
                    Say("역무실, " + (involved.Count == 1 ? involved[0].Label : "에스컬레이터") + " 운행 정지하고 이용 통제 요청합니다.");
                    session.Announce(PaLine.EscalatorStopped, "에스컬레이터 운행이 중지되었습니다. 옆 계단과 다른 통로를 이용해 주십시오.");
                    log.Add("에스컬레이터 운행 정지·이용 통제");
                    if (!calledBy.ContainsKey(Agency.Facility)) Call(Agency.Facility, "역무원 요청(에스컬레이터)");
                });
            if (Train != null && Train.AtPlatform && !holdRequested && known.Any(NearTrain))
                yield return Option("역무실 · 5·6 타는 곳 열차 출발 보류 요청", () =>
                {
                    holdRequested = true;
                    Train.Holds.Add("역무원 요청");
                    Say("역무실, 5·6 타는 곳 서울행 열차 출발 보류 요청합니다.");
                    Office("역무실 수신. 관제에 출발 보류 요청했습니다.");
                    log.Add("열차 출발 보류 요청");
                });
            foreach (var fire in fires.Where(f => f.Extinguished && reported.Contains(f) && !reportedDone.Contains(f)).Take(1))
            {
                var f = fire;
                yield return Option("역무실 · 초기 진화 완료 보고", () =>
                {
                    reportedDone.Add(f);
                    Say("역무실, " + f.Where + " 화재 초기 진화했습니다. 연기 남아 있습니다.");
                    Office("역무실 수신. 소방대 도착하면 현장 인계 바랍니다.");
                    log.Add("역무실에 초기 진화 보고");
                });
            }
            foreach (var bag in bags.Where(b => cordoned.Contains(b) && reported.Contains(b) && !reportedDone.Contains(b)).Take(1))
            {
                var b = bag;
                yield return Option("역무실 · 가방 주변 통제 완료 보고", () =>
                {
                    reportedDone.Add(b);
                    Say("역무실, 방치 가방 주변 통제선 설치했습니다.");
                    Office("역무실 수신. 경찰 도착까지 접근 통제 유지 바랍니다.");
                    log.Add("역무실에 통제 완료 보고");
                });
            }
        }

        private bool NearTrain(Hazard hazard)
        {
            if (Train == null) return false;
            if (hazard is DoorTrapHazard || trainFires.Contains(hazard as FireHazard)) return true;
            return world.Points.PlatformAt(hazard.Position)?.id == world.Points.Train?.platform || Train.CarAt(hazard.Position) != null;
        }

        private List<Escalator> InvolvedEscalators()
        {
            var list = new List<Escalator>();
            foreach (var pair in fallOn) if (known.Contains(pair.Key) && !closedEscalators.Contains(pair.Value)) list.Add(pair.Value);
            if (escalatorsStopped && known.Contains(quake)) foreach (var escalator in world.Escalators) if (!closedEscalators.Contains(escalator) && !list.Contains(escalator)) list.Add(escalator);
            return list;
        }

        private static EmergencySession.RadioOption Option(string label, Action send) => new EmergencySession.RadioOption { Label = label, Send = send };
        private void Say(string text) => session.Hud.Radio.Push(RadioChannel.Self, text);
        private void Office(string text) => session.Hud.Radio.Push(RadioChannel.Office, text);
        private void Colleague(string text) => session.Hud.Radio.Push(RadioChannel.Colleague, text);

        private void Report(Hazard hazard)
        {
            reported.Add(hazard);
            log.Add("역무실에 " + hazard.Label + " 보고 · " + hazard.Where);
            switch (hazard)
            {
                case FireHazard f:
                    bool aboard = trainFires.Contains(f);
                    Say("역무실, " + f.Where + " 화재 발생. " + (f.Intensity > .6f ? "불길이 큽니다." : "초기 단계입니다."));
                    Office("역무실 수신. 119 신고하겠습니다." + (aboard ? " 열차 출발 보류하고 승무원 보내겠습니다." : "") + " 초기 진화 가능하면 시도하고 무리하지 마십시오.");
                    Call(Agency.Fire, "역무실 신고");
                    if (aboard) { Train.Holds.Add("차내 화재"); Call(Agency.Crew, "역무실 요청"); }
                    break;
                case SuspiciousItemHazard b:
                    Say("역무실, " + b.Where + "에 주인 없는 여행가방 있습니다.");
                    Office("역무실 수신. 112와 철도경찰에 신고하겠습니다. 가방은 건드리지 말고 주변 접근을 통제하십시오.");
                    Call(Agency.Police, "역무실 신고");
                    break;
                case CollapseHazard c:
                    Say(fallOn.ContainsKey(c) ? "역무실, " + c.Where + "에서 승객이 넘어졌습니다. " + c.Visible + "." : "역무실, " + c.Where + "에 쓰러진 승객 있습니다. " + c.Visible + ".");
                    // 반응 없고 숨이 고르지 않으면 119 신고와 함께 가까운 AED 를 가져오게 한다(2025 한국 심폐소생술 가이드라인).
                    Office("역무실 수신. 119 구급 요청하겠습니다. 환자 곁을 지키고 주변을 비워 주십시오." + (fallOn.ContainsKey(c) ? " 에스컬레이터 정지 확인 바랍니다." : "") + (c.NotBreathingNormally ? " 가까운 자동심장충격기를 가져와 환자 곁에 두십시오." : ""));
                    Call(Agency.Medical, "역무실 신고");
                    break;
                case DoorTrapHazard t:
                    Say("역무실, 5·6 타는 곳 KTX " + t.Car.Label + " 출입문 끼임입니다. 출발 보류 바랍니다.");
                    Office("역무실 수신. 출발 보류하고 열차팀장에게 전달하겠습니다.");
                    Call(Agency.Crew, "역무실 요청");
                    break;
                default:
                    Say("역무실, 지진 흔들림 확인. 피해 상황 확인하겠습니다.");
                    Office("역무실 수신. 낙하물과 부상자, 승강설비 확인해서 알려 주십시오.");
                    break;
            }
        }

        private void Announce()
        {
            announced = true;
            var place = Main != null ? Main.Where : "역";
            string text;
            PaLine line;
            switch (Main)
            {
                case FireHazard _:
                    text = "안내 말씀 드립니다. " + place + "에 화재가 발생했습니다. 승객 여러분께서는 직원의 안내에 따라 가까운 출구로 대피해 주시기 바랍니다.";
                    line = PaLine.Fire;
                    break;
                case SuspiciousItemHazard _:
                    text = "안내 말씀 드립니다. " + place + " 주변에서 안전 확인을 하고 있습니다. 해당 구역에서 떨어져 직원 안내에 따라 이동해 주십시오.";
                    line = PaLine.SuspiciousItem;
                    break;
                case CollapseHazard _:
                    text = "안내 말씀 드립니다. " + place + "에 응급 환자가 있습니다. 통로를 비워 주시고 구급대 진입에 협조해 주십시오.";
                    line = PaLine.Medical;
                    break;
                case DoorTrapHazard _:
                    text = "안내 말씀 드립니다. " + place + " 출입문을 점검하고 있습니다. 열차 출발이 잠시 늦어지니 출입문에서 물러서 주십시오.";
                    line = PaLine.DoorCheck;
                    break;
                default:
                    text = "안내 말씀 드립니다. 지진이 발생했습니다. 낙하물에 주의하시고 머리를 보호하며 직원 안내에 따라 이동해 주십시오. 승강기는 이용하지 마십시오.";
                    line = PaLine.Earthquake;
                    break;
            }
            Say("역무실, 안내방송 요청합니다.");
            session.Announce(line, text);
            // 응급 환자·출입문은 역 전체를 비울 일이 아니다: 그 주변 사람만 물러서게 한다.
            if (Main is CollapseHazard || Main is DoorTrapHazard)
            {
                int cleared = 0;
                foreach (var person in crowd.People.ToArray())
                {
                    if (person.Hurt || person.Current == Passenger.Activity.InTrain || Vector3.Distance(person.transform.position, Main.Position) > 15) continue;
                    if (person.Focus == null) { person.Notice(Main, true, "an announcement asks people to keep the area clear"); }
                    cleared++;
                }
                log.Add("안내방송 · 주변 비우기 (" + cleared + "명)");
                return;
            }
            // 의심 물체는 역 전체가 아니라 주변(반경 50 m)을 비운다. 역 전체 대피는 경찰이 판단한다.
            if (Main is SuspiciousItemHazard)
            {
                int moved = crowd.Announce(Main.Position, 50f, false);
                log.Add("안내방송 · 의심 물체 주변 대피 (" + moved + "명)");
                return;
            }
            int heard = crowd.Announce();
            log.Add("대피 안내방송 (역 안 " + heard + "명)");
        }

        private void Call(Agency agency, string by)
        {
            if (calledBy.ContainsKey(agency)) return;
            calledBy[agency] = by;
            // 실제 출동 시간은 수 분이다. 게임에서는 압축한다(분 단위 → 1~2분). 승무원은 열차에 있어 빨리 온다.
            float delay = agency == Agency.Crew ? world.Range(15, 30) : agency == Agency.Facility ? world.Range(50, 80) : agency == Agency.Police ? world.Range(80, 120) : world.Range(100, 140);
            arriveAt[agency] = Time.time + delay;
            log.Add(Responder.AgencyName(agency) + " 출동 요청 (" + by + ")");
        }

        /// <summary>The hazard an agency is coming for (not necessarily the first incident of the shift).</summary>
        private Hazard TargetOf(Agency agency)
        {
            switch (agency)
            {
                case Agency.Fire: return fires.FirstOrDefault(f => !f.Extinguished) ?? fires.FirstOrDefault();
                case Agency.Police: return bags.FirstOrDefault(b => b.Unattended) ?? bags.FirstOrDefault();
                case Agency.Medical: return casualties.FirstOrDefault(c => c.Active);
                case Agency.Crew: return (Hazard)traps.Find(t => t.Active) ?? trainFires.FirstOrDefault(f => !f.Extinguished);
                default: return (Hazard)quake ?? casualties.FirstOrDefault(c => fallOn.ContainsKey(c));
            }
        }

        private Vector3 SceneOf(Agency agency)
        {
            var target = TargetOf(agency);
            switch (agency)
            {
                case Agency.Medical: return target != null ? target.Position : NextPatient(PlayerPosition)?.transform.position ?? Main?.Position ?? PlayerPosition;
                case Agency.Facility:
                    if (fallen.Count > 0) return fallen[0].Impact;
                    foreach (var pair in fallOn) return pair.Value.Middle;
                    return Main != null && Main.NeedsSight ? Main.Position : PlayerPosition;
                case Agency.Crew:
                    if (target is FireHazard fire) return world.Train.World(world.Train.CarAt(fire.Position)?.DoorOutside ?? fire.Position);
                    return target != null ? target.Position : PlayerPosition;
                default:
                    return target != null ? target.Position : Main != null && Main.NeedsSight ? Main.Position : PlayerPosition;
            }
        }

        private void SpawnTeam(Agency agency)
        {
            GameObject[] prefabs;
            switch (agency)
            {
                case Agency.Fire: prefabs = art.Crowd.Firefighters; break;
                case Agency.Police: prefabs = art.Crowd.Police; break;
                case Agency.Medical: prefabs = art.Crowd.Paramedics.Length > 0 ? new[] { art.Crowd.Paramedics[0], art.Crowd.Paramedics[0] } : art.Crowd.Paramedics; break;
                default: prefabs = new[] { art.Crowd.Colleague }; break;
            }
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
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                var start = StationWorld.OnNavMesh(entrance + new Vector3(i * .8f, 0, 0), 2);
                var go = Instantiate(prefabs[i], start, Quaternion.identity, root);
                go.name = Responder.AgencyName(agency) + " " + (i + 1);
                var body = go.GetComponent<PersonBody>();
                if (!body.Agent.isOnNavMesh) body.Agent.Warp(start);
                var responder = go.AddComponent<Responder>();
                var goal = world.Approach(scene, start, standOff + i * 1.2f);
                responder.Setup(this, agency, i == 0, goal, TargetOf(agency) ?? Main, art);
                responders.Add(responder);
                if (i == 0) session.SetMarker("agency-" + agency, start, MarkerKind.Responder, Responder.AgencyName(agency));
            }
            session.Hud.Radio.Push(agency == Agency.Fire ? RadioChannel.Fire : agency == Agency.Police ? RadioChannel.Police : agency == Agency.Medical ? RadioChannel.Medical : RadioChannel.Colleague,
                Responder.AgencyName(agency) + (agency == Agency.Crew ? " 승강장으로 갑니다." : " 부산역 도착, " + world.Describe(scene) + "(으)로 이동합니다."));
            log.Add(Responder.AgencyName(agency) + " 도착");
        }

        public void OnResponderArrived(Responder responder)
        {
            if (responder.Lead) session.SetMarker("agency-" + responder.Agency, responder.transform.position, MarkerKind.Responder, Responder.AgencyName(responder.Agency));
            // 승무원이 도착하면 끼인 문을 연다.
            if (responder.Agency == Agency.Crew) foreach (var trap in traps.Where(t => t.Active).ToList()) FreeTrap(trap, "열차 승무원이 출입문을 다시 열어 빼냄");
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
            if (hazard != null) cordoned.Add(hazard);
            log.Add(label + " 주변 통제선 설치 (반경 " + radius.ToString("0") + "m) · " + world.Describe(centre));
            return "통제선을 설치했습니다 · 주변 사람들을 바깥으로 안내했습니다";
        }

        // ── 인계와 종료 ─────────────────────────────────────────────────────

        public bool CanHandOver(Responder responder) => !handedOver && Stage == Phase.Incident && responder.Lead;

        public string HandOver(Responder responder)
        {
            handedOver = true;
            var summary = new StringBuilder();
            summary.Append(Main != null ? Named(Main) : "역 상황");
            if (PlayerKnowsIncident) summary.Append(", ").Append(session.Clock(knownAt - Time.time + session.ShiftSeconds)).Append(" 인지(").Append(knownHow).Append(")");
            foreach (var fire in fires) summary.Append(", ").Append(fire.Where).Append(fire.Extinguished ? " 불 꺼짐" : " 불 계속 탐");
            foreach (var bag in bags) summary.Append(bag.Touched ? ", 승객이 가방을 만졌음" : ", 가방은 아무도 만지지 않았음").Append(cordoned.Contains(bag) ? " · 통제선 설치" : " · 통제선 없음");
            foreach (var casualty in casualties)
                summary.Append(", ").Append(Named(casualty)).Append(casualty.Person != null && treated.Contains(casualty.Person) ? " 처치 중" : " 처치 전")
                    .Append(casualty.AedAt >= 0 ? " · AED 곁에 둠(쓰러진 지 " + Mathf.RoundToInt(casualty.AedAt - casualty.StartedAt) + "초)" : "");
            foreach (var trap in traps) summary.Append(", ").Append(trap.Car.Label).Append(trap.Active ? " 문 끼임 계속" : " 문 끼임 해소");
            if (quake != null) summary.Append(", 낙하물 ").Append(fallenKnown.Count).Append("곳 확인");
            summary.Append(", 대피 안내 ").Append(log.Guided).Append("명, 부상 ").Append(crowd.Injured.Count).Append("명");
            Say(Responder.AgencyName(responder.Agency) + "에 인계합니다: " + summary + ".");
            session.Hud.Radio.Push(responder.Agency == Agency.Fire ? RadioChannel.Fire : responder.Agency == Agency.Police ? RadioChannel.Police : responder.Agency == Agency.Medical ? RadioChannel.Medical : RadioChannel.Colleague,
                "인계받았습니다. 이후는 저희가 맡겠습니다.");
            log.Add(Responder.AgencyName(responder.Agency) + "에 현장 인계: " + summary);
            Invoke(nameof(FinishAfterHandover), 3f);
            return "현장을 " + Responder.AgencyName(responder.Agency) + "에 인계했습니다";
        }

        /// <summary>"Place + what" for radio calls and summaries, without repeating a word the place name already ends with.</summary>
        private static string Named(Hazard hazard)
        {
            var where = hazard.Where ?? "";
            var label = hazard.Label;
            int space = label.IndexOf(' ');
            if (space > 0 && where.EndsWith(label.Substring(0, space), StringComparison.Ordinal)) label = label.Substring(space + 1);
            return (where + " " + label).Trim();
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
            float danger = 0;
            foreach (var fire in fires)
            {
                if (fire.Extinguished) continue;
                if (fire.InSmoke(eye)) danger = Mathf.Max(danger, .7f);
                if (Vector3.Distance(eye, fire.Position) < fire.DangerRadius + .8f) danger = 1;
            }
            if (quake != null && quake.Shaking) danger = Mathf.Max(danger, .35f);
            session.Hud.SetDanger(danger);

            if (!PlayerKnowsIncident || Main == null)
            {
                session.SituationText = "평시 근무 · " + world.Describe(PlayerPosition);
                session.SituationColour = Color.white;
                return;
            }
            var focus = known.Contains(Main) ? Main : known.FirstOrDefault() ?? Main;
            string state = focus is FireHazard f ? (f.Extinguished ? "진화됨" : "타는 중") :
                focus is SuspiciousItemHazard ? (cordoned.Contains(focus) ? "통제 중" : "확인 필요") :
                focus is CollapseHazard c ? (c.Person != null && treated.Contains(c.Person) ? "처치 중" : "처치 필요") :
                focus is DoorTrapHazard t ? (t.Active ? "출발 보류" : "해소") :
                quake != null && quake.Shaking ? "흔들림" : "흔들림 멈춤";
            session.SituationText = focus.Label + " · " + focus.Where + " · " + state;
            session.SituationColour = state == "진화됨" || state == "통제 중" || state == "처치 중" || state == "해소" ? FpsUiFactory.Accent : FpsUiFactory.Danger;
        }

        private BoardOverlay.Column SituationColumn()
        {
            var column = new BoardOverlay.Column { Title = "상황" };
            if (Train != null) column.Lines.Add("열차: " + Train.Status());
            if (!PlayerKnowsIncident || Main == null) { column.Lines.Add("이상 없음"); column.Lines.Add("역 순회 근무 · " + world.Describe(PlayerPosition)); return column; }
            column.Lines.Add("인지 " + session.Clock(knownAt - Time.time + session.ShiftSeconds) + " (" + knownHow + ")");
            foreach (var hazard in known.Take(5))
            {
                string state = hazard is FireHazard f ? (f.Extinguished ? "꺼짐 · 연기 남음" : f.Intensity > 1 ? "크게 번짐 · 소화기로는 어려움" : "타는 중") :
                    hazard is SuspiciousItemHazard ? (cordoned.Contains(hazard) ? "통제선 안" : "그대로 놓여 있음") :
                    hazard is CollapseHazard c ? c.Visible :
                    hazard is DoorTrapHazard t ? (t.Active ? "끼인 채 · 출발 보류" : "해소") :
                    (quake != null && quake.Shaking ? "흔들림 계속" : "흔들림 멈춤") + " · 낙하물 " + fallenKnown.Count + "곳";
                column.Lines.Add(hazard.Label + " — " + hazard.Where + " · " + state);
            }
            if (escalatorsStopped) column.Lines.Add("에스컬레이터·엘리베이터 정지" + (escalatorsClosed ? " · 이용 통제" : ""));
            return column;
        }

        private BoardOverlay.Column ActionColumn()
        {
            var column = new BoardOverlay.Column { Title = "조치" };
            column.Lines.Add((reported.Count > 0 ? "● " : "○ ") + "역무실 보고" + (reported.Count > 1 ? " " + reported.Count + "건" : ""));
            column.Lines.Add((announced ? "● " : "○ ") + "안내방송");
            column.Lines.Add("직접 대피 안내 " + log.Guided + "명");
            if (bags.Count > 0 || fallen.Count > 0) column.Lines.Add((world.Closed.Exists(z => z.label.StartsWith("통제선", StringComparison.Ordinal)) ? "● " : "○ ") + "접근 통제");
            if (fires.Count > 0) column.Lines.Add((fires.TrueForAll(f => f.Extinguished) ? "● " : "○ ") + "초기 진화" + (log.SprayedSeconds > 0 ? " (분사 " + log.SprayedSeconds.ToString("0") + "초)" : ""));
            if (Train != null && known.Any(NearTrain)) column.Lines.Add((Train.Holds.Count > 0 ? "● " : "○ ") + "열차 출발 보류");
            if (fallOn.Count > 0) column.Lines.Add((escalatorsClosed ? "● " : "○ ") + "에스컬레이터 통제");
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
