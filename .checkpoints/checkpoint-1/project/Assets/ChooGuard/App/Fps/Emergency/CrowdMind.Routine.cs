using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    // 일상 판단: 사람마다 JEV 의 계획(CrowdMind.Plan)을 한 번 받아 두고, 활동이 끝날 때 지역에서 다음 걸음을 꺼낸다. 계획이 아직 없거나 전제가 달라졌으면 하던 일을 잇는 동안 새 계획을 묻는다.
    public sealed partial class CrowdMind
    {
        // 자주 쓰는 장소 목록(판단 선택지 재료).
        private List<StationPoints.Point> shops, toilets, exits, meets;
        private List<string> cafes, livingZones;
        /// <summary>Standing places by zone: a person picks a zone first and a place in it second, so the concourse's hundreds of points do not outweigh the bus stop or the promenade.</summary>
        private readonly Dictionary<string, List<StationPoints.Point>> standing = new Dictionary<string, List<StationPoints.Point>>();

        /// <summary>Zones that have standing places, in a fixed order.</summary>
        public IReadOnlyList<string> LivingZones => livingZones;

        private void InitRoutine()
        {
            var points = crowd.World.Points;
            shops = points.Of(PointKind.Shop).ToList();
            toilets = points.Of(PointKind.Toilet).ToList();
            exits = points.Of(PointKind.Exit).ToList();
            meets = points.Of(PointKind.Meet).ToList();
            cafes = points.Of(PointKind.Chair).Select(c => ShopOf(c)).Distinct().ToList();
            standing.Clear();
            foreach (var wait in points.Of(PointKind.Wait))
            {
                string zone = wait.Zone ?? "";
                if (!standing.TryGetValue(zone, out var list)) standing[zone] = list = new List<StationPoints.Point>();
                list.Add(wait);
            }
            livingZones = standing.Keys.OrderBy(z => z, StringComparer.Ordinal).ToList();
        }

        private static string ShopOf(StationPoints.Point chair) => chair.Label.EndsWith(" 의자", StringComparison.Ordinal) ? chair.Label.Substring(0, chair.Label.Length - 3) : chair.Label;

        private static string FamilyOf(string key)
        {
            int colon = key.IndexOf(':');
            return colon < 0 ? key : key.Substring(0, colon);
        }

        // 바깥·승차 게이트·정류장 구역은 이름으로 안다(구역 id 는 월드 정의). 그 밖의 구역은 안쪽 일반 구역으로 본다.
        /// <summary>The zone of the bus stop shelter (the world's id).</summary>
        public const string BusStopZone = "busstop";
        private static bool IsBusStop(string zone) => zone == BusStopZone;
        private static bool IsGate(string zone) => zone == "southgate";

        private Judgement NewEveryday(Passenger who, Trigger trigger, float dueIn, string prefix)
        {
            var item = new Judgement
            {
                Who = who, Trigger = trigger, Everyday = true,
                Raised = Time.time, RaisedReal = Time.realtimeSinceStartup, Due = Time.time + dueIn,
                Key = prefix + who.Number + "_" + (++serial),
            };
            queue.Add(item);
            return item;
        }

        /// <summary>
        /// True when the person can start their next step now. With JEV that means they have a plan whose premises still hold;
        /// otherwise they keep doing what they were doing while a plan is asked for at once. Without JEV local weights decide, so
        /// they never wait.
        /// </summary>
        public bool Ready(Passenger who)
        {
            var slot = who.Slot;
            // JEV 가 답하지 않는다: 여정을 잇는다(Outage).
            if (Outage(who)) return true;
            // 관측에 대한 판단이 진행 중이면 새 일을 시작하지 않는다: 답이 올 때까지 하던 일을 잇는다.
            if (slot.Urgent != null && !slot.Urgent.Done) return false;
            if (!Usable) return true;
            if (Holds(who)) return true;
            Escalate(who, slot.Routine);
            return false;
        }

        /// <summary>Takes the next step: false while they must keep waiting; otherwise <paramref name="choice"/> (null: nothing fits, they leave).</summary>
        public bool TryTakeRoutine(Passenger who, out Choice choice)
        {
            choice = null;
            if (!Ready(who)) return false;
            var slot = who.Slot;
            if (Outage(who))
            {
                // 지역 규칙이 아니라 여정 목적 동선으로 걷는다. 묻던 질문은 그대로 두어 답이 오면 다음 걸음에 쓴다.
                slot.ContinuedAt = Time.time;
                slot.WaitingSince = -1;
                choice = Itinerary(who);
                Metrics.Continued++;
                slot.Log("JEV is not answering: the trip goes on (" + (choice != null ? choice.Key : "leaves") + ")");
                if (choice != null) crowd.Session.Log.Decision(who, "Routine", choice.Key, "itinerary");
                return true;
            }
            if (slot.WaitingSince >= 0)
            {
                float waited = Time.time - slot.WaitingSince;
                Metrics.RoutineWait.Add(waited);
                Metrics.LongestWait = Mathf.Max(Metrics.LongestWait, waited);
                slot.WaitingSince = -1;
            }
            string source;
            if (Usable)
            {
                // 계획의 다음 걸음: JEV 를 부르지 않는다.
                choice = slot.Plan != null ? StepOf(slot.Plan, who) : null;
                source = "JEV";
                if (choice != null) Metrics.RoutineByJev++;
                else
                {
                    // 계획에 더 고를 것이 없다(할 일을 마쳤거나 가려던 곳이 모두 막혔다): 새 계획을 묻고, 그동안 여정대로 간다.
                    slot.Plan = null;
                    if (slot.Routine == null || slot.Routine.Done) Ask(who, "exhausted");
                    choice = Itinerary(who);
                    source = "itinerary";
                }
            }
            else
            {
                var options = RoutineOptions(who);
                if (options.Count == 0) return true;
                string key = SampleLocal(options);
                choice = options.Find(o => o.Key == key) ?? options[0];
                if (!Reserve(choice, who))
                {
                    choice = null;
                    foreach (var option in options.OrderByDescending(o => o.Weight))
                        if (Reserve(option, who)) { choice = option; break; }
                }
                source = "local";
                Metrics.RoutineLocally++;
            }
            if (choice != null) crowd.Session.Log.Decision(who, "Routine", choice.Key, source);
            return true;
        }

        /// <summary>
        /// JEV is failing (its requests come back without an answer) and this person has waited <see cref="OutageSeconds"/> for a
        /// judgement: they carry on with the purpose of their trip until it answers again. That is the walk the spawn rule gives someone
        /// who has no answer yet; local weights stay for runs without JEV.
        /// </summary>
        private bool Outage(Passenger who)
        {
            if (failuresInARow == 0 || !Usable) return false;
            var slot = who.Slot;
            float since = float.PositiveInfinity;
            if (slot.WaitingSince >= 0) since = slot.WaitingSince;
            if (slot.Urgent != null && !slot.Urgent.Done) since = Mathf.Min(since, slot.Urgent.Raised);
            return !float.IsPositiveInfinity(since) && Time.time - Mathf.Max(since, slot.ContinuedAt) > OutageSeconds;
        }

        /// <summary>
        /// A person waiting for an answer that is not coming. Someone standing or watching (nothing calls <c>Decide</c> for them) sets
        /// off on their itinerary; someone already walking is already on their trip and simply is not waiting any more.
        /// </summary>
        private void ContinueStill(Passenger person, float now)
        {
            var slot = person.Slot;
            if (slot.Urgent == null || slot.Urgent.Done || !Outage(person)) return;
            slot.ContinuedAt = now;
            if (!person.Idle) return;
            Metrics.Continued++;
            slot.Log("JEV is not answering: the trip goes on");
            person.ContinueItinerary();
        }

        /// <summary>The person has to wait for the next step: the plan question moves to the emergency lane so nobody stands idle.</summary>
        private void Escalate(Passenger who, Judgement item)
        {
            var slot = who.Slot;
            if (slot.WaitingSince < 0) slot.WaitingSince = Time.time;
            if (item == null || item.Done) item = Ask(who, WhyAsk(who));
            if (item.Urgent) return;
            item.Urgent = true;
            item.Trigger = Trigger.Ended;
            item.Due = Time.time;
            Metrics.Escalations++;
            slot.Log("waiting for the next step: plan question escalated");
        }

        /// <summary>
        /// The first step of a trip with a purpose, decided in code so that a person who has just walked in has somewhere to
        /// go before JEV answers: depart → ticket window, waiting hall or platform; arrive → the exit; greet → the meeting
        /// point; visit → the shops.
        /// </summary>
        public Choice Itinerary(Passenger who)
        {
            var options = RoutineOptions(who);
            string[] order;
            switch (who.Trip)
            {
                case Passenger.Purpose.Depart: order = new[] { "board_now", "ticket", "platform_early", "sit_hall", "stand_board" }; break;
                case Passenger.Purpose.Arrive: order = new[] { "meet", "leave" }; break;
                case Passenger.Purpose.Greet: order = new[] { "meet_wait", "sit_hall" }; break;
                default: order = new[] { "shop", "cafe", "leave" }; break;
            }
            Metrics.Itineraries++;
            // 여정 걸음은 계획의 걸음이 아니다: 막힘이 계획의 선택지를 지우지 않게 한다.
            if (who.Slot.Plan != null) who.Slot.Plan.Active = null;
            foreach (var family in order)
            {
                var choice = options.Find(o => FamilyOf(o.Key) == family);
                if (choice != null && Reserve(choice, who)) return choice;
            }
            foreach (var choice in options) if (Reserve(choice, who)) return choice;
            return null;
        }

        /// <summary>
        /// JEV's plan arrives. It replaces whatever plan the person had; someone still walking the first step of their trip
        /// (just walked in) takes their first step from it at once.
        /// </summary>
        private void ReceiveRoutine(Judgement item, JevAnswer answer)
        {
            var who = item.Who;
            if (item.Basis.Trip != TripOf(who) || item.Basis.Known != KnownOf(who))
            {
                Finish(item);
                Metrics.Stale++;
                who.Slot.Log("old plan discarded: its trip or knowledge changed");
                if (who.Slot.Routine == null || who.Slot.Routine.Done)
                    Ask(who, item.Basis.Trip != TripOf(who) ? "trip" : "knowledge", item.First && who.OnItinerary);
                return;
            }
            var odds = answer.Probabilities != null && answer.Probabilities.Count > 0
                ? new Dictionary<string, float>(answer.Probabilities)
                : new Dictionary<string, float> { [answer.Choice ?? ""] = 1 };
            var plan = new Plan(item.Basis, item.Choices, odds);
            who.Slot.Plan = plan;
            Metrics.PlansAnswered++;
            who.Slot.Log("plan received: " + answer.Choice);
            Finish(item);
            if (!item.First || !who.OnItinerary) return;
            var chosen = StepOf(plan, who);
            if (chosen == null) return;
            Metrics.FirstAnswers++;
            Metrics.RoutineByJev++;
            who.Slot.Log("first plan took over from the itinerary: " + chosen.Key);
            crowd.Session.Log.Decision(who, "Routine", chosen.Key, "JEV");
            who.Redirect(chosen);
        }

        private void ReceiveRoute(Judgement item, JevAnswer answer)
        {
            item.Who.SetRoute(answer.Draw(crowd.World.Random));
            Finish(item);
        }

        private JevChoice RouteQuestion(Judgement item)
        {
            var text = new StringBuilder(240);
            text.Append(Profile(item.Who)).Append(" moves between floors and down to the platforms of Busan Station (escalators beside stairs, a few elevators). How do they usually change floors?");
            var route = new JevChoice { Id = item.Key, Instructions = text.ToString() };
            route.Criteria["escalator"] = "Rides the escalators";
            route.Criteria["stairs"] = "Walks the stairs";
            route.Criteria["elevator"] = "Waits for an elevator";
            return route;
        }

        private JevChoice RoutineQuestion(Judgement item)
        {
            var p = item.Who;
            item.Basis = BasisOf(p);
            item.Choices = RoutineOptions(p);
            if (item.Choices.Count == 0) return null;
            var text = new StringBuilder(640);
            text.Append(Profile(p)).Append(". Trip: ").Append(Trip(p)).Append(". Train: ").Append(crowd.World.Train != null ? crowd.World.Train.Status() : "none").Append(". ");
            text.Append("Now: ").Append(p.Doing).Append(". ");
            if (p.Memory.Count > 0) text.Append("Earlier: ").Append(string.Join("; ", p.Memory)).Append(". ");
            if (HasKnowledge(p)) text.Append("They know about the situation described in the state and it may affect what they want to do. ");
            text.Append("Clock ").Append(crowd.Session.Clock(crowd.Session.ShiftSeconds)).Append(". Choose how this person is inclined to spend their time in the station from now on, as an ordinary traveller at Busan Station would. ");
            text.Append("The game carries your probabilities out step after step over the next several minutes, each time among the options still possible then, and asks again only when their train, ticket, meeting or knowledge of an incident changes.");
            var question = new JevChoice { Id = item.Key, Instructions = text.ToString() };
            foreach (var choice in item.Choices) question.Criteria[choice.Key] = choice.Description;
            return question;
        }

        private string Trip(Passenger p)
        {
            switch (p.Trip)
            {
                case Passenger.Purpose.Depart:
                    return p.MissedTrain ? "missed the KTX to Seoul" : "taking the KTX to Seoul from platform 5·6, car " + p.Car + (p.HasTicket ? "" : ", no ticket yet") + ", leaves in about " + Mathf.Max(1, Mathf.RoundToInt(SecondsToTrain(p) / 60f)) + " min";
                case Passenger.Purpose.Arrive: return p.Partner != null && !p.Met ? "just arrived from Seoul; family is waiting in the station" : "arrived from Seoul, heading into the city";
                case Passenger.Purpose.Greet: return p.Met ? "met the person they came for" : "came to meet someone arriving on the KTX from Seoul";
                default: return "visiting the station's shops and cafes";
            }
        }

        // ── 지역 규칙(JEV 없는 근무) ────────────────────────────────────────────

        private string SampleLocal(List<Choice> options)
        {
            float total = 0;
            foreach (var o in options) total += Mathf.Max(0, o.Weight);
            float roll = (float)crowd.World.Random.NextDouble() * total;
            foreach (var o in options)
            {
                roll -= Mathf.Max(0, o.Weight);
                if (roll <= 0) return o.Key;
            }
            return options.Count > 0 ? options[0].Key : null;
        }

        /// <summary>Holds the place for the option (a seat, a counter, a standing spot) and fixes how long the step lasts; false when none can be had.</summary>
        private bool Reserve(Choice choice, Passenger who)
        {
            var world = crowd.World;
            var here = who.transform.position;
            switch (choice.Kind)
            {
                case PointKind.Seat: choice.Place = world.ReserveSeat(here, 90); break;
                case PointKind.Chair: choice.Place = world.ReserveChair(here, 600, p => choice.Filter == null || ShopOf(p) == choice.Filter); break;
                case PointKind.Counter: choice.Place = world.ReservePlace(PointKind.Counter); break;
                case PointKind.Wait:
                    // 구역의 정해 둔 지점(정류장 표지 같은)을 먼저, 비어 있지 않으면 같은 구역의 다른 자리.
                    choice.Place = world.ReservePlace(PointKind.Wait, p => (choice.Filter == null || p.Zone == choice.Filter) && (choice.Landmark == null || p.Label == choice.Landmark))
                        ?? world.ReservePlace(PointKind.Wait, p => choice.Filter == null || p.Zone == choice.Filter);
                    break;
                case PointKind.PlatformWait:
                    choice.Place = world.ReservePlace(PointKind.PlatformWait, p => p.Id.StartsWith("pw-" + who.Car + "-", StringComparison.Ordinal)) ?? world.ReservePlace(PointKind.PlatformWait);
                    break;
                default: break; // 가게·화장실·출구·마중 지점은 여러 사람이 함께 쓴다(미리 정해 둠).
            }
            if (choice.Place == null) return false;
            choice.Seconds = choice.Length != null ? choice.Length() : 0;
            return true;
        }

        /// <summary>Somewhere people who know of an incident do not choose to go: close to it, or inside a cordon.</summary>
        private bool Avoided(Passenger p, Vector3 at)
        {
            if (crowd.World.IsClosed(at)) return true;
            foreach (var hazard in p.Noticed)
                if (hazard.Active && hazard.Localized && Mathf.Abs(hazard.Position.y - at.y) < 4f && Vector3.Distance(hazard.Position, at) < hazard.DangerRadius + 6f) return true;
            return false;
        }

        private bool ArrivalSoon(TrainService train) =>
            train != null && (train.Stage == TrainService.Phase.Away && train.ArrivalAt - Time.time < 240 || train.Stage >= TrainService.Phase.Arriving && train.Stage <= TrainService.Phase.Alighting);

        /// <summary>
        /// What this person could do now. Keys are stable (a plan keeps its probabilities under them); fixed targets stay
        /// bound, while a moving meeting partner is explicitly re-resolved. The weights decide only in runs without JEV;
        /// JEV is offered every option with weight above zero.
        /// </summary>
        private List<Choice> RoutineOptions(Passenger p)
        {
            var list = new List<Choice>();
            var world = crowd.World;
            var train = world.Train;
            var here = p.transform.position;
            float toTrain = SecondsToTrain(p);
            string minutes = Mathf.Max(1, Mathf.RoundToInt(toTrain / 60f)) + " min";
            void Add(string key, string description, float weight, Passenger.Activity activity, PointKind kind, Func<float> length, StationPoints.Point place = null, string filter = null, string remember = null, Func<bool> guard = null, float fade = 1, Func<StationPoints.Point> resolve = null)
            {
                if (weight <= 0 || place != null && Avoided(p, place.Position)) return;
                list.Add(new Choice { Key = key, Description = description, Weight = weight, Activity = activity, Kind = kind, Length = length, Place = place, Filter = filter, Remember = remember, Guard = guard, Fade = fade, Resolve = resolve });
            }
            bool usedToilet = p.Memory.Any(m => m.Contains("toilet"));
            var toilet = Nearest(toilets, here);
            switch (p.Trip)
            {
                case Passenger.Purpose.Depart when !p.MissedTrain:
                {
                    bool boarding = Boarding(p);
                    if (boarding)
                        Add("board_now", "Go down to platform 5·6 now and board car " + p.Car + " (boarding is open; the train leaves in about " + minutes + ")", toTrain < 120 ? 16 : 6, Passenger.Activity.PlatformWait, PointKind.PlatformWait, null, remember: "went down to platform 5·6 to board",
                            guard: () => train.Service == p.Service && (train.BoardingOpen || train.Stage == TrainService.Phase.Closing));
                    else if (toTrain < EarlySeconds)
                        Add("platform_early", "Go down to platform 5·6 early and wait by car " + p.Car + "'s door (boarding opens soon; departure in about " + minutes + ")", toTrain < 240 ? 2f : .5f, Passenger.Activity.PlatformWait, PointKind.PlatformWait, null, remember: "went down to the platform early",
                            guard: () => train.Service < p.Service || train.Service == p.Service && train.Stage < TrainService.Phase.Departing);
                    if (!p.HasTicket) Add("ticket", "Queue at a 2F ticket window for the ticket to Seoul", 3, Passenger.Activity.Queue, PointKind.Counter, () => world.Range(25, 55), remember: "bought a ticket", guard: () => !p.HasTicket);
                    if (!boarding || toTrain > 240)
                    {
                        Add("sit_hall", "Sit on a waiting-hall bench on 2F until closer to departure", 1.4f, Passenger.Activity.Sit, PointKind.Seat, () => Mathf.Clamp(SecondsToTrain(p) - 150, 40, 300), remember: "sat in the waiting hall");
                        Add("stand_board", "Stand in the waiting hall watching the departure boards", .6f, Passenger.Activity.Stand, PointKind.Wait, () => world.Range(30, 90), filter: "hall2f", remember: "watched the departure boards");
                        AddShops(list, p, here, toTrain > 180 ? .5f : .1f);
                        AddCafe(list, p, here, toTrain > 300 ? .5f : 0);
                        if (toilet != null) Add("toilet", "Use the toilets (" + toilet.Label + ")", usedToilet ? .05f : toTrain > 150 ? .35f : .1f, Passenger.Activity.Toilet, PointKind.Toilet, null, toilet, fade: 0);
                        Add("phone", "Step aside and make a phone call", .25f, Passenger.Activity.Stand, PointKind.Wait, () => world.Range(40, 90), filter: world.Points.ZoneAt(here)?.id, remember: "made a phone call", fade: 0);
                        AddLiving(list, p, zone => IsGate(zone) ? (toTrain > 150 ? .5f : .15f) : StationWorld.IsOutdoorZone(zone) ? (toTrain > 300 ? (IsBusStop(zone) ? .1f : .2f) : 0) : .1f);
                    }
                    break;
                }
                case Passenger.Purpose.Depart:
                    Add("rebook", "Queue at a ticket window to change to the next train", 2, Passenger.Activity.Queue, PointKind.Counter, () => world.Range(30, 60), remember: "rebooked for the next train", guard: () => !p.HasTicket);
                    AddExits(list, p, here, .8f, "Give up and leave toward");
                    Add("sit_hall", "Sit on a waiting-hall bench for a while", .8f, Passenger.Activity.Sit, PointKind.Seat, () => world.Range(60, 180));
                    AddLiving(list, p, zone => StationWorld.IsOutdoorZone(zone) ? .2f : .08f);
                    break;
                case Passenger.Purpose.Arrive:
                    if (p.Partner != null && !p.Met && p.Partner.Current == Passenger.Activity.Meet)
                        Add("meet", "Go to the person waiting for them at " + p.Partner.Crowd.World.Describe(p.Partner.transform.position), 6, Passenger.Activity.Meet, PointKind.Meet, () => 30, NearestMeet(p.Partner.transform.position), remember: "went to meet their family",
                            guard: () => p.Partner != null && !p.Met && p.Partner.Current == Passenger.Activity.Meet,
                            resolve: () => p.Partner != null ? NearestMeet(p.Partner.transform.position) : null);
                    AddExits(list, p, here, 1f, "Leave the station toward");
                    if (toilet != null) Add("toilet", "Use the toilets (" + toilet.Label + ")", usedToilet ? .02f : .3f, Passenger.Activity.Toilet, PointKind.Toilet, null, toilet, fade: 0);
                    AddCafe(list, p, here, .15f);
                    AddShops(list, p, here, .12f);
                    AddLiving(list, p, zone => IsBusStop(zone) ? .5f : StationWorld.IsOutdoorZone(zone) ? .25f : .06f);
                    break;
                case Passenger.Purpose.Greet:
                {
                    bool soon = ArrivalSoon(train);
                    var meet = world.Pick(meets);
                    if (meet != null) Add("meet_wait", "Wait at " + meet.Label + " for the person arriving from Seoul (" + (train != null ? train.Status() : "") + ")", soon ? 4 : 1.5f, Passenger.Activity.Meet, PointKind.Meet, () => world.Range(60, 150), meet, remember: "waited at the arrivals exit");
                    Add("sit_hall", "Sit on a waiting-hall bench until the train comes in", soon ? .3f : .9f, Passenger.Activity.Sit, PointKind.Seat, () => world.Range(60, 160));
                    AddCafe(list, p, here, soon ? .1f : .5f);
                    AddShops(list, p, here, .25f);
                    AddLiving(list, p, zone => StationWorld.IsOutdoorZone(zone) ? .2f : .1f);
                    if (p.Memory.Count > 4) AddExits(list, p, here, .3f, "Stop waiting and leave toward");
                    break;
                }
                default:
                    AddShops(list, p, here, 1f);
                    AddCafe(list, p, here, .9f);
                    if (toilet != null) Add("toilet", "Use the toilets (" + toilet.Label + ")", usedToilet ? .02f : .3f, Passenger.Activity.Toilet, PointKind.Toilet, null, toilet, fade: 0);
                    AddExits(list, p, here, .3f + .25f * p.Memory.Count, "Leave the station toward");
                    AddLiving(list, p, zone => StationWorld.IsOutdoorZone(zone) ? (IsBusStop(zone) ? .15f : .4f) : .2f);
                    break;
            }
            if (list.Count == 0) AddExits(list, p, here, 1, "Leave the station toward");
            return list;
        }

        private float SecondsToTrain(Passenger p)
        {
            var train = crowd.World.Train;
            if (train == null) return 900;
            float seconds = train.SecondsToDeparture();
            int next = train.NextBoardingService;
            if (p.Service > next) seconds += TrainService.CycleSeconds * (p.Service - next);
            return seconds;
        }

        private void AddShops(List<Choice> list, Passenger p, Vector3 here, float weight)
        {
            if (weight <= 0 || shops.Count == 0) return;
            var world = crowd.World;
            // 가까운 가게 둘과 다른 층 가게 하나.
            var near = shops.Where(s => !Avoided(p, s.Position)).OrderBy(s => Vector3.Distance(s.Position, here) + world.Range(0, 40)).Take(2).ToList();
            var other = shops.Where(s => Mathf.Abs(s.Position.y - here.y) > 3 && !Avoided(p, s.Position)).OrderBy(_ => world.Random.Next()).FirstOrDefault();
            if (other != null && !near.Contains(other)) near.Add(other);
            foreach (var shop in near)
            {
                bool anotherFloor = Mathf.Abs(shop.Position.y - here.y) > 3;
                list.Add(new Choice
                {
                    Key = "shop:" + shop.Id, Description = "Browse " + shop.Label + " (" + StationFloor(shop.Position) + (anotherFloor ? ", another floor" : "") + ")",
                    Weight = weight * (anotherFloor ? .6f : 1), Activity = Passenger.Activity.Browse, Kind = PointKind.Shop, Place = shop,
                    Length = () => world.Range(25, 80), Remember = "browsed " + shop.Label, Fade = VisitFade,
                });
            }
        }

        private void AddCafe(List<Choice> list, Passenger p, Vector3 here, float weight)
        {
            if (weight <= 0 || cafes.Count == 0) return;
            var world = crowd.World;
            int index = world.Random.Next(cafes.Count);
            var cafe = cafes[index];
            var sample = world.Points.Of(PointKind.Chair).FirstOrDefault(c => ShopOf(c) == cafe);
            if (sample != null && Avoided(p, sample.Position)) return;
            list.Add(new Choice
            {
                Key = "cafe:" + index, Description = "Get something at " + cafe + " (" + (sample != null ? StationFloor(sample.Position) : "") + ") and sit at a table",
                Weight = weight, Activity = Passenger.Activity.Sit, Kind = PointKind.Chair, Filter = cafe, Length = () => world.Range(120, 300), Remember = "sat at " + cafe, Fade = VisitFade,
            });
        }

        private void AddExits(List<Choice> list, Passenger p, Vector3 here, float weight, string verb)
        {
            var world = crowd.World;
            // 사건이 아는 곳 가까이의 출구는 뒤로 미룬다(다른 출구가 있으면 그쪽을 먼저 후보로 든다).
            foreach (var exit in exits.OrderBy(e => Avoided(p, e.Position) ? 1 : 0).ThenBy(_ => world.Random.Next()).Take(3))
                list.Add(new Choice { Key = "leave:" + exit.Id, Description = verb + " " + exit.Label, Weight = weight * (exit.Id == "exit-port" ? .4f : 1), Activity = Passenger.Activity.Leave, Kind = PointKind.Exit, Place = exit });
        }

        /// <summary>
        /// Standing places offered zone first: a few zones picked at random (each as likely as any other, however many points
        /// it has), one landmark in each, and the zone's own weight for this trip (0: not for them). A busy concourse and a
        /// bus stop shelter are equally real destinations; the person and JEV decide which suits this trip.
        /// </summary>
        private void AddLiving(List<Choice> list, Passenger p, Func<string, float> weightOf)
        {
            if (livingZones.Count == 0) return;
            var world = crowd.World;
            int offered = 0;
            foreach (var zone in livingZones.OrderBy(_ => world.Random.Next()))
            {
                if (offered >= LivingZonesPerQuestion) break;
                float weight = weightOf(zone);
                if (weight <= 0) continue;
                var landmark = world.Pick(standing[zone]);
                if (landmark == null || Avoided(p, landmark.Position)) continue;
                string label = world.Points.Zone(zone)?.label ?? zone;
                string verb = IsGate(zone) ? "Wait at the boarding gate area " : IsBusStop(zone) ? "Wait at the bus stop " : StationWorld.IsOutdoorZone(zone) ? "Step outside to " : "Spend a while at ";
                list.Add(new Choice
                {
                    Key = "spot:" + zone, Description = verb + label + " near " + landmark.Label,
                    Weight = weight, Activity = Passenger.Activity.Stand, Kind = PointKind.Wait, Length = () => world.Range(40, 120),
                    Filter = zone, Landmark = landmark.Label, Remember = "spent a while at " + label, Fade = VisitFade,
                });
                offered++;
            }
        }

        private StationPoints.Point NearestMeet(Vector3 position) => Nearest(meets, position);

        private static StationPoints.Point Nearest(List<StationPoints.Point> list, Vector3 position)
        {
            StationPoints.Point best = null; float bestDistance = float.PositiveInfinity;
            foreach (var point in list)
            {
                float d = Vector3.Distance(point.Position, position) + Mathf.Abs(point.Position.y - position.y) * 4;
                if (d < bestDistance) { bestDistance = d; best = point; }
            }
            return best;
        }

        private static string StationFloor(Vector3 position) => position.y < 3.5f ? "1F" : position.y < 10 ? "2F" : "3F";
    }
}
