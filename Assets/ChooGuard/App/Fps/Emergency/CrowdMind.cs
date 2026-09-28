using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Decisions for people in the station, asked of JEV as typed Choice questions over options the game builds from
    /// the live world. Everyday decisions (what to do next, how to change floors) are asked ahead of time, while the
    /// current activity is still running, so nobody freezes waiting for an answer; key moments (noticing something,
    /// staff instructions, announcements, shaking) are asked when they happen. JEV probabilities are sampled so a crowd
    /// spreads over plausible choices. Without JEV, or when it is late, local weights decide; those weights are design
    /// heuristics, not measured behaviour data.
    /// </summary>
    public sealed class CrowdMind
    {
        public enum Kind { Notice, Indirect, Instruction, Reevaluate, Quake, AfterQuake, Routine, Route }

        public int AnsweredByJev { get; private set; }
        public int AnsweredLocally { get; private set; }
        public int RoutineByJev { get; private set; }
        public int RoutineLocally { get; private set; }

        /// <summary>One thing a person could do next, with where and for how long.</summary>
        public sealed class Choice
        {
            public string Key, Description, Remember, Filter;
            public float Weight, Seconds;
            public Passenger.Activity Activity;
            public PointKind Kind;
            public StationPoints.Point Place;
        }

        private sealed class Option
        {
            public string Key, Description;
            public Action<Passenger, Hazard, StationWorld> Run;
        }

        private sealed class Pending
        {
            public Passenger Who;
            public Kind Kind;
            public Hazard Hazard;
            public bool Flag;          // Indirect: via others' reaction · Instruction: direct from staff
            public float Asked, ReadyAt;
            public bool Sent, Done;
            public string Key, Answer;
            public List<(Option option, float weight)> Options;
            public List<Choice> Choices;
        }

        private readonly CrowdDirector crowd;
        private readonly JevClient jev;
        private readonly List<Pending> pending = new List<Pending>();
        private readonly Dictionary<Passenger, Pending> routine = new Dictionary<Passenger, Pending>();
        private readonly HashSet<Passenger> routeAsked = new HashSet<Passenger>();
        private float nextFlush;
        private int serial;
        private const int BatchSize = 8;
        private const float JevPatience = 2.4f;

        // 자주 쓰는 장소 목록(판단 선택지 재료).
        private readonly List<StationPoints.Point> shops, toilets, exits, meets;
        private readonly List<string> cafes;

        public CrowdMind(CrowdDirector crowd, JevClient jev)
        {
            this.crowd = crowd;
            this.jev = jev;
            var points = crowd.World.Points;
            shops = points.Of(PointKind.Shop).ToList();
            toilets = points.Of(PointKind.Toilet).ToList();
            exits = points.Of(PointKind.Exit).ToList();
            meets = points.Of(PointKind.Meet).ToList();
            cafes = points.Of(PointKind.Chair).Select(c => ShopOf(c)).Distinct().ToList();
        }

        private static string ShopOf(StationPoints.Point chair) => chair.Label.EndsWith(" 의자", StringComparison.Ordinal) ? chair.Label.Substring(0, chair.Label.Length - 3) : chair.Label;

        // ── 핵심 순간 ────────────────────────────────────────────────────────

        public void OnNotice(Passenger who, Hazard hazard, bool indirect) =>
            Ask(who, hazard is EarthquakeHazard quake ? (quake.Shaking ? Kind.Quake : Kind.AfterQuake) : indirect ? Kind.Indirect : Kind.Notice, hazard, indirect);

        public void OnInstruction(Passenger who, bool direct) => Ask(who, Kind.Instruction, who.Focus, direct);
        public void OnReevaluate(Passenger who) => Ask(who, Kind.Reevaluate, who.Focus, false);
        public void AfterQuake(Passenger who) => Ask(who, Kind.AfterQuake, who.Focus, false);

        private void Ask(Passenger who, Kind kind, Hazard hazard, bool flag)
        {
            // 같은 사람의 이전 질문은 새 상황으로 대체한다(일상 판단은 그대로 둔다).
            foreach (var old in pending) if (old.Who == who && !old.Done && old.Kind != Kind.Routine && old.Kind != Kind.Route) old.Done = true;
            var world = crowd.World;
            var item = new Pending
            {
                Who = who, Kind = kind, Hazard = hazard, Flag = flag,
                Asked = Time.time,
                // 사람이 알아차리고 움직이기까지의 반응 시간. JEV 가 없을 때도 같은 지연을 둔다.
                ReadyAt = Time.time + (kind == Kind.Quake ? world.Range(.2f, .7f) : world.Range(.5f, 1.4f)),
                Key = "p" + who.Number + "_" + (++serial),
            };
            item.Options = Options(item);
            pending.Add(item);
        }

        // ── 일상 판단(미리 묻기) ─────────────────────────────────────────────

        /// <summary>Asks what <paramref name="who"/> does after the current activity, about <paramref name="secondsLeft"/> before it ends.</summary>
        public void Prefetch(Passenger who, float secondsLeft)
        {
            if (routine.TryGetValue(who, out var old)) old.Done = true;
            var item = new Pending { Who = who, Kind = Kind.Routine, Asked = Time.time, ReadyAt = Time.time + Mathf.Max(0, secondsLeft - 2), Key = "r" + who.Number + "_" + (++serial), Choices = RoutineOptions(who) };
            routine[who] = item;
            pending.Add(item);
            if (routeAsked.Add(who))
                pending.Add(new Pending { Who = who, Kind = Kind.Route, Asked = Time.time, ReadyAt = Time.time + 30, Key = "route" + who.Number + "_" + (++serial) });
        }

        /// <summary>The next thing to do: the prefetched JEV answer when it came in time, local weights otherwise. Reserves the place.</summary>
        public Choice TakeRoutine(Passenger who)
        {
            routine.TryGetValue(who, out var item);
            routine.Remove(who);
            List<Choice> options;
            string key;
            string source;
            if (item != null && item.Answer != null)
            {
                options = item.Choices;
                key = item.Answer;
                source = "JEV";
                RoutineByJev++;
            }
            else
            {
                if (item != null) item.Done = true;
                options = item?.Choices ?? RoutineOptions(who);
                key = SampleLocal(options);
                source = "local";
                RoutineLocally++;
            }
            if (options.Count == 0) return null;
            var chosen = options.Find(o => o.Key == key) ?? options[0];
            if (!Reserve(chosen, who))
            {
                chosen = null;
                foreach (var option in options.OrderByDescending(o => o.Weight))
                    if (Reserve(option, who)) { chosen = option; break; }
            }
            if (chosen != null) crowd.Session.Log.Decision(who, "Routine", chosen.Key, source);
            return chosen;
        }

        /// <summary>A local decision now (the chosen place was taken or the option no longer applies).</summary>
        public Choice Fallback(Passenger who)
        {
            var options = RoutineOptions(who);
            foreach (var option in options.OrderByDescending(o => o.Weight + (float)crowd.World.Random.NextDouble()))
                if (Reserve(option, who)) return option;
            return null;
        }

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

        private bool Reserve(Choice choice, Passenger who)
        {
            var world = crowd.World;
            var here = who.transform.position;
            switch (choice.Kind)
            {
                case PointKind.Seat: choice.Place = world.ReserveSeat(here, 90); break;
                case PointKind.Chair: choice.Place = world.ReserveChair(here, 600, p => choice.Filter == null || ShopOf(p) == choice.Filter); break;
                case PointKind.Counter: choice.Place = world.ReservePlace(PointKind.Counter); break;
                case PointKind.Wait: choice.Place = world.ReservePlace(PointKind.Wait, p => choice.Filter == null || p.Zone == choice.Filter); break;
                case PointKind.PlatformWait:
                    choice.Place = world.ReservePlace(PointKind.PlatformWait, p => p.Id.StartsWith("pw-" + who.Car + "-", StringComparison.Ordinal)) ?? world.ReservePlace(PointKind.PlatformWait);
                    break;
                default: break; // 가게·화장실·출구·마중 지점은 여러 사람이 함께 쓴다(미리 정해 둠).
            }
            return choice.Place != null;
        }

        private List<Choice> RoutineOptions(Passenger p)
        {
            var list = new List<Choice>();
            var world = crowd.World;
            var train = world.Train;
            var here = p.transform.position;
            bool upperFloor = here.y > 10;
            float toTrain = SecondsToTrain(p);
            string minutes = Mathf.Max(1, Mathf.RoundToInt(toTrain / 60f)) + " min";
            void Add(string key, string description, float weight, Passenger.Activity activity, PointKind kind, float seconds, StationPoints.Point place = null, string filter = null, string remember = null)
            {
                if (weight <= 0) return;
                list.Add(new Choice { Key = key, Description = description, Weight = weight, Activity = activity, Kind = kind, Seconds = seconds, Place = place, Filter = filter, Remember = remember });
            }
            bool usedToilet = p.Memory.Any(m => m.Contains("toilet"));
            var toilet = Nearest(toilets, here);
            switch (p.Trip)
            {
                case Passenger.Purpose.Depart when !p.MissedTrain:
                {
                    bool boarding = train != null && (train.BoardingOpen || train.Stage == TrainService.Phase.Closing) && train.Service == p.Service;
                    if (boarding)
                        Add("board_now", "Go down to platform 5·6 now and board car " + p.Car + " (boarding is open; the train leaves in about " + minutes + ")", toTrain < 120 ? 16 : 6, Passenger.Activity.PlatformWait, PointKind.PlatformWait, 0, remember: "went down to platform 5·6 to board");
                    else if (toTrain < 420)
                        Add("platform_early", "Go down to platform 5·6 early and wait by car " + p.Car + "'s door (boarding opens soon; departure in about " + minutes + ")", toTrain < 240 ? 2f : .5f, Passenger.Activity.PlatformWait, PointKind.PlatformWait, 0, remember: "went down to the platform early");
                    if (!p.HasTicket) Add("ticket", "Queue at a 2F ticket window for the ticket to Seoul", 3, Passenger.Activity.Queue, PointKind.Counter, world.Range(25, 55), remember: "bought a ticket");
                    if (!boarding || toTrain > 240)
                    {
                        Add("sit_hall", "Sit on a waiting-hall bench on 2F until closer to departure", 1.4f, Passenger.Activity.Sit, PointKind.Seat, Mathf.Clamp(toTrain - 150, 40, 300), remember: "sat in the waiting hall");
                        Add("stand_board", "Stand in the waiting hall watching the departure boards", .6f, Passenger.Activity.Stand, PointKind.Wait, world.Range(30, 90), filter: "hall2f", remember: "watched the departure boards");
                        AddShops(list, p, here, toTrain > 180 ? .5f : .1f, upperFloor);
                        AddCafe(list, p, here, toTrain > 300 ? .5f : 0);
                        if (toilet != null) Add("toilet", "Use the toilets (" + toilet.Label + ")", usedToilet ? .05f : toTrain > 150 ? .35f : .1f, Passenger.Activity.Toilet, PointKind.Toilet, 0, toilet);
                        Add("phone", "Step aside and make a phone call", .25f, Passenger.Activity.Stand, PointKind.Wait, world.Range(40, 90), filter: world.Points.ZoneAt(here)?.id, remember: "made a phone call");
                    }
                    break;
                }
                case Passenger.Purpose.Depart:
                    Add("rebook", "Queue at a ticket window to change to the next train", 2, Passenger.Activity.Queue, PointKind.Counter, world.Range(30, 60), remember: "rebooked for the next train");
                    AddExits(list, here, .8f, "Give up and leave toward");
                    Add("sit_hall", "Sit on a waiting-hall bench for a while", .8f, Passenger.Activity.Sit, PointKind.Seat, world.Range(60, 180));
                    break;
                case Passenger.Purpose.Arrive:
                    if (p.Partner != null && !p.Met && p.Partner.Current == Passenger.Activity.Meet)
                        Add("meet", "Go to the person waiting for them at " + p.Partner.Crowd.World.Describe(p.Partner.transform.position), 6, Passenger.Activity.Meet, PointKind.Meet, 30, NearestMeet(p.Partner.transform.position), remember: "went to meet their family");
                    AddExits(list, here, 1f, "Leave the station toward");
                    if (toilet != null) Add("toilet", "Use the toilets (" + toilet.Label + ")", usedToilet ? .02f : .3f, Passenger.Activity.Toilet, PointKind.Toilet, 0, toilet);
                    AddCafe(list, p, here, .15f);
                    AddShops(list, p, here, .12f, upperFloor);
                    break;
                case Passenger.Purpose.Greet:
                {
                    bool soon = train != null && (train.Stage == TrainService.Phase.Away && train.ArrivalAt - Time.time < 240 || train.Stage >= TrainService.Phase.Arriving && train.Stage <= TrainService.Phase.Alighting);
                    var meet = world.Pick(meets);
                    if (meet != null) Add("meet_wait", "Wait at " + meet.Label + " for the person arriving from Seoul (" + (train != null ? train.Status() : "") + ")", soon ? 4 : 1.5f, Passenger.Activity.Meet, PointKind.Meet, world.Range(60, 150), meet, remember: "waited at the arrivals exit");
                    Add("sit_hall", "Sit on a waiting-hall bench until the train comes in", soon ? .3f : .9f, Passenger.Activity.Sit, PointKind.Seat, world.Range(60, 160));
                    AddCafe(list, p, here, soon ? .1f : .5f);
                    AddShops(list, p, here, .25f, upperFloor);
                    if (p.Memory.Count > 4) AddExits(list, here, .3f, "Stop waiting and leave toward");
                    break;
                }
                default:
                    AddShops(list, p, here, 1f, upperFloor);
                    AddCafe(list, p, here, .9f);
                    if (toilet != null) Add("toilet", "Use the toilets (" + toilet.Label + ")", usedToilet ? .02f : .3f, Passenger.Activity.Toilet, PointKind.Toilet, 0, toilet);
                    AddExits(list, here, .3f + .25f * p.Memory.Count, "Leave the station toward");
                    break;
            }
            if (list.Count == 0) AddExits(list, here, 1, "Leave the station toward");
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

        private void AddShops(List<Choice> list, Passenger p, Vector3 here, float weight, bool upper)
        {
            if (weight <= 0 || shops.Count == 0) return;
            var world = crowd.World;
            // 가까운 가게 둘과 다른 층 가게 하나.
            var near = shops.OrderBy(s => Vector3.Distance(s.Position, here) + world.Range(0, 40)).Take(2).ToList();
            var other = shops.Where(s => Mathf.Abs(s.Position.y - here.y) > 3).OrderBy(_ => world.Random.Next()).FirstOrDefault();
            if (other != null) near.Add(other);
            int i = 0;
            foreach (var shop in near)
            {
                string floor = StationFloor(shop.Position);
                list.Add(new Choice
                {
                    Key = "shop_" + i++, Description = "Browse " + shop.Label + " (" + floor + (Mathf.Abs(shop.Position.y - here.y) > 3 ? ", another floor" : "") + ")",
                    Weight = weight * (Mathf.Abs(shop.Position.y - here.y) > 3 ? .6f : 1), Activity = Passenger.Activity.Browse, Kind = PointKind.Shop, Place = shop,
                    Seconds = world.Range(25, 80), Remember = "browsed " + shop.Label,
                });
            }
        }

        private void AddCafe(List<Choice> list, Passenger p, Vector3 here, float weight)
        {
            if (weight <= 0 || cafes.Count == 0) return;
            var world = crowd.World;
            var cafe = cafes[world.Random.Next(cafes.Count)];
            var sample = world.Points.Of(PointKind.Chair).FirstOrDefault(c => ShopOf(c) == cafe);
            list.Add(new Choice
            {
                Key = "cafe", Description = "Get something at " + cafe + " (" + (sample != null ? StationFloor(sample.Position) : "") + ") and sit at a table",
                Weight = weight, Activity = Passenger.Activity.Sit, Kind = PointKind.Chair, Filter = cafe, Seconds = world.Range(120, 300), Remember = "sat at " + cafe,
            });
        }

        private void AddExits(List<Choice> list, Vector3 here, float weight, string verb)
        {
            var world = crowd.World;
            int i = 0;
            foreach (var exit in exits.OrderBy(_ => world.Random.Next()).Take(3))
                list.Add(new Choice { Key = "leave_" + i++, Description = verb + " " + exit.Label, Weight = weight * (exit.Id == "exit-port" ? .4f : 1), Activity = Passenger.Activity.Leave, Kind = PointKind.Exit, Place = exit });
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

        // ── 주기 ────────────────────────────────────────────────────────────

        public void Tick()
        {
            float now = Time.time;
            bool jevUsable = jev != null && jev.Available;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var item = pending[i];
                if (item.Done || item.Who == null) { pending.RemoveAt(i); continue; }
                if (item.Kind == Kind.Routine || item.Kind == Kind.Route)
                {
                    // 일상 판단은 다음 활동을 시작할 때 가져간다. 답이 없으면 그때 지역 규칙으로 정한다.
                    if (item.Answer != null || (!jevUsable && item.Kind == Kind.Route)) { if (item.Kind == Kind.Route || item.Answer != null) pending.RemoveAt(i); }
                    else if (item.Kind == Kind.Route && now - item.Asked > 60) pending.RemoveAt(i);
                    continue;
                }
                bool timedOut = item.Sent ? now - item.Asked > JevPatience + 1.5f : now - item.Asked > JevPatience;
                if (now >= item.ReadyAt && (!jevUsable || timedOut)) Resolve(item, null);
            }
            if (!jevUsable || now < nextFlush || !jev.CanSend()) return;
            // 핵심 순간을 먼저, 남는 자리에 일상 판단을 싣는다.
            var batch = new List<Pending>(BatchSize);
            foreach (var item in pending) if (!item.Sent && !item.Done && item.Kind != Kind.Routine && item.Kind != Kind.Route) { batch.Add(item); if (batch.Count == BatchSize) break; }
            foreach (var item in pending) if (batch.Count < BatchSize && !item.Sent && !item.Done && (item.Kind == Kind.Routine || item.Kind == Kind.Route)) batch.Add(item);
            if (batch.Count == 0) return;
            nextFlush = now + .35f;
            foreach (var item in batch) item.Sent = true;
            var questions = new List<JevChoice>(batch.Count);
            foreach (var item in batch) questions.Add(Question(item));
            crowd.StartCoroutine(jev.Ask("crowd", crowd.Session.Incidents.PublicState(), questions, answers =>
            {
                foreach (var item in batch)
                {
                    if (item.Done || item.Who == null) continue;
                    JevAnswer answer = null;
                    answers?.TryGetValue(item.Key, out answer);
                    if (item.Kind == Kind.Routine)
                    {
                        if (answer != null) item.Answer = Sample(answer, crowd.World.Random);
                        continue;
                    }
                    if (item.Kind == Kind.Route)
                    {
                        if (answer != null) item.Who.SetRoute(Sample(answer, crowd.World.Random));
                        item.Done = true;
                        continue;
                    }
                    if (answer == null) { item.Sent = false; item.Asked = Mathf.Min(item.Asked, Time.time - JevPatience); continue; }
                    Resolve(item, answer);
                }
            }));
        }

        private void Resolve(Pending item, JevAnswer answer)
        {
            item.Done = true;
            var world = crowd.World;
            Option chosen = null;
            string source;
            if (answer != null)
            {
                string key = Sample(answer, world.Random);
                foreach (var (option, _) in item.Options) if (option.Key == key) chosen = option;
                source = "JEV";
                AnsweredByJev++;
            }
            else
            {
                float total = 0;
                foreach (var (_, weight) in item.Options) total += Mathf.Max(0, weight);
                float roll = (float)world.Random.NextDouble() * total;
                foreach (var (option, weight) in item.Options)
                {
                    roll -= Mathf.Max(0, weight);
                    if (roll <= 0) { chosen = option; break; }
                }
                source = "local";
                AnsweredLocally++;
            }
            if (chosen == null) chosen = item.Options[0].option;
            // 같은 묶음에서 여럿이 동시에 '알리러 간다'를 고를 수 있다. 실행 순간에 다시 확인한다.
            if (chosen == Report && !CanReport(item.Hazard)) chosen = Watch;
            crowd.Session.Log.Decision(item.Who, item.Kind.ToString(), chosen.Key, source);
            // 답을 기다리는 사이 다쳤거나 연기를 피해 이미 대피하기 시작한 사람에게 뒤늦은 '구경'·'계속'을 덮어쓰지 않는다.
            var who = item.Who;
            if (who == null || who.Hurt) return;
            if (who.Current == Passenger.Activity.Evacuate && chosen != Evacuate && chosen != Run && chosen != Comply && chosen != Follow) return;
            chosen.Run(who, item.Hazard, world);
        }

        private static string Sample(JevAnswer answer, System.Random random)
        {
            if (answer.Probabilities == null || answer.Probabilities.Count == 0) return answer.Choice;
            float total = 0;
            foreach (var pair in answer.Probabilities) total += Mathf.Max(0, pair.Value);
            if (total <= 0) return answer.Choice;
            float roll = (float)random.NextDouble() * total;
            foreach (var pair in answer.Probabilities)
            {
                roll -= Mathf.Max(0, pair.Value);
                if (roll <= 0) return pair.Key;
            }
            return answer.Choice;
        }

        // ── 핵심 순간 선택지와 지역 가중치 ────────────────────────────────────

        private static Option O(string key, string description, Action<Passenger, Hazard, StationWorld> run) =>
            new Option { Key = key, Description = description, Run = run };

        private static readonly Option Continue = O("continue", "Carries on with what they were doing", (p, h, w) => p.KeepGoing());
        private static readonly Option Watch = O("watch", "Stops at a distance and keeps watching", (p, h, w) => p.Watch(w.Range(12, 25), false));
        private static readonly Option Film = O("film", "Takes out a phone and films it", (p, h, w) => p.Watch(w.Range(12, 25), true));
        private static readonly Option MoveAway = O("move_away", "Moves a safe distance away and waits there", (p, h, w) => p.MoveAway((h?.DangerRadius ?? 0) + w.Range(9, 16)));
        private static readonly Option Evacuate = O("evacuate", "Leaves the station calmly through an exit", (p, h, w) => p.Evacuate(false));
        private static readonly Option Run = O("run", "Runs for an exit", (p, h, w) => p.Evacuate(true));
        private static readonly Option Report = O("report_staff", "Goes to tell the station staff member on duty", (p, h, w) => p.ReportToStaff());
        private static readonly Option Alert = O("alert_others", "Warns the people nearby", (p, h, w) => p.AlertOthers());
        private static readonly Option Cover = O("take_cover", "Crouches and protects their head", (p, h, w) => p.TakeCover(w.Range(2, 5)));
        private static readonly Option Freeze = O("freeze", "Freezes in place, unsure what to do", (p, h, w) => p.Freeze(w.Range(2, 4)));
        private static readonly Option HoldOn = O("hold_on", "Stays seated, holding on", (p, h, w) => p.Freeze(w.Range(2, 5)));
        private static readonly Option Stay = O("stay", "Stays put and waits for information", (p, h, w) => p.Watch(w.Range(20, 45), false));
        private static readonly Option Follow = O("follow_crowd", "Follows the people who are leaving", (p, h, w) => p.Evacuate(false));
        private static readonly Option LookAround = O("look_around", "Stops and looks around to see what is going on", (p, h, w) => p.Watch(w.Range(5, 10), false));
        private static readonly Option Comply = O("comply", "Follows the instruction and leaves through the indicated exit", (p, h, w) => p.Evacuate(false));
        private static readonly Option Hesitate = O("hesitate", "Hesitates and looks around before deciding", (p, h, w) => p.Watch(w.Range(5, 12), false));
        private static readonly Option Ignore = O("ignore", "Ignores the instruction for now", (p, h, w) => p.KeepGoing());
        private static readonly Option StepOff = O("get_off", "Gets off the train onto the platform", (p, h, w) => p.MoveAway(8));
        private static readonly Option StayAboard = O("stay_aboard", "Stays in their seat for now", (p, h, w) => p.KeepGoing());
        private static readonly Option Help = O("help", "Goes over to help the person who is hurt", (p, h, w) => p.HelpNearby(h));

        private List<(Option, float)> Options(Pending item)
        {
            var p = item.Who;
            var h = item.Hazard;
            float distance = h != null && h.NeedsSight ? Vector3.Distance(p.transform.position, h.Position) : 0;
            bool near = distance < 7;
            bool seated = p.Body.Seat != PersonBody.SeatPhase.None;
            bool aboard = p.Current == Passenger.Activity.InTrain;
            int leaving = crowd.CountNear(p.transform.position, 10, Passenger.Activity.Evacuate);
            var list = new List<(Option, float)>();
            if (aboard && item.Kind != Kind.Quake)
            {
                bool doorsOpen = crowd.World.Train != null && crowd.World.Train.DoorsOpen > .9f;
                list.Add((StepOff, doorsOpen ? .55f + (near ? .3f : 0) : 0));
                list.Add((StayAboard, .25f));
                list.Add((Watch, .15f));
                list.Add((Alert, .08f));
                return list;
            }
            switch (item.Kind)
            {
                case Kind.Notice when h is FireHazard fire:
                {
                    float big = Mathf.Clamp01(fire.Intensity);
                    bool smoky = fire.InSmoke(p.transform.position);
                    list.Add((Continue, .08f - .06f * big));
                    list.Add((Watch, near ? .06f : .14f));
                    list.Add((Film, near ? .03f : .08f));
                    list.Add((MoveAway, .26f + (near ? .15f : 0)));
                    list.Add((Evacuate, .18f + .2f * big + (smoky ? .3f : 0) + (near ? .1f : 0)));
                    list.Add((Run, .04f + .08f * big));
                    list.Add((Report, CanReport(h) ? .12f : 0));
                    list.Add((Alert, .10f));
                    break;
                }
                case Kind.Notice when h is CollapseHazard:
                    list.Add((Help, near ? .35f : .15f));
                    list.Add((Report, CanReport(h) ? .3f : 0));
                    list.Add((Watch, .2f));
                    list.Add((Continue, .15f));
                    break;
                case Kind.Notice:
                    list.Add((Continue, .40f));
                    list.Add((Watch, .20f));
                    list.Add((MoveAway, .15f));
                    list.Add((Report, CanReport(h) ? .15f : 0));
                    list.Add((Alert, .10f));
                    break;
                case Kind.Indirect:
                    list.Add((Follow, .45f + (leaving >= 3 ? .2f : 0)));
                    list.Add((LookAround, .35f));
                    list.Add((Continue, .20f));
                    break;
                case Kind.Instruction:
                    bool sees = h != null && h.NeedsSight && distance < h.NoticeRadius;
                    list.Add((Comply, (item.Flag ? .82f : .60f) + (sees ? .1f : 0)));
                    list.Add((Hesitate, item.Flag ? .14f : .25f));
                    list.Add((Ignore, item.Flag ? .04f : .15f));
                    break;
                case Kind.Reevaluate when h is FireHazard fire:
                    bool inSmoke = fire.InSmoke(p.transform.position);
                    list.Add((Evacuate, .45f + (inSmoke ? .3f : 0) + (p.Instructed ? .3f : 0)));
                    list.Add((MoveAway, .30f));
                    list.Add((Watch, .15f));
                    list.Add((Continue, .10f));
                    break;
                case Kind.Reevaluate:
                    list.Add((Continue, p.Instructed ? .1f : .35f));
                    list.Add((Watch, .20f));
                    list.Add((MoveAway, .25f));
                    list.Add((Evacuate, p.Instructed ? .5f : .05f));
                    list.Add((Report, CanReport(h) ? .20f : 0));
                    break;
                case Kind.Quake:
                    list.Add((Cover, seated ? .35f : .50f));
                    list.Add((seated ? HoldOn : Freeze, seated ? .45f : .30f));
                    list.Add((Run, .20f));
                    break;
                case Kind.AfterQuake:
                    bool debris = crowd.World.IsClosed(p.transform.position, 8);
                    list.Add((Continue, .25f));
                    list.Add((Stay, .40f));
                    list.Add((Evacuate, .35f + (debris ? .2f : 0) + (p.Instructed ? .3f : 0)));
                    break;
            }
            return list;
        }

        /// <summary>Telling staff makes sense unless the staff member is visibly already dealing with it.</summary>
        private bool CanReport(Hazard hazard)
        {
            var player = crowd.Player;
            if (player == null || hazard is EarthquakeHazard) return false;
            // 이미 두 사람이 알리러 갔으면 다른 사람들은 누군가 알렸으리라 여긴다.
            int reporting = 0;
            foreach (var person in crowd.People) if (person.Current == Passenger.Activity.Report && person.Focus == hazard) reporting++;
            if (reporting >= 2) return false;
            if (hazard == null || !hazard.NeedsSight) return true;
            return !(crowd.Session.Incidents.PlayerKnowsIncident && Vector3.Distance(player.transform.position, hazard.Position) < 15);
        }

        private string Profile(Passenger p)
        {
            var text = new StringBuilder(160);
            text.Append("Passenger #").Append(p.Number).Append(" (").Append(p.Body.Female ? "woman" : "man");
            if (p.Elderly) text.Append(", elderly");
            text.Append(p.Luggage == 2 ? ", with a large suitcase" : p.Luggage == 1 ? ", with a bag" : "").Append(")");
            return text.ToString();
        }

        private JevChoice Question(Pending item)
        {
            var p = item.Who;
            var h = item.Hazard;
            var text = new StringBuilder(480);
            if (item.Kind == Kind.Route)
            {
                text.Append(Profile(p)).Append(" moves between floors and down to the platforms of Busan Station (escalators beside stairs, a few elevators). How do they usually change floors?");
                var route = new JevChoice { Id = item.Key, Instructions = text.ToString() };
                route.Criteria["escalator"] = "Rides the escalators";
                route.Criteria["stairs"] = "Walks the stairs";
                route.Criteria["elevator"] = "Waits for an elevator";
                return route;
            }
            if (item.Kind == Kind.Routine)
            {
                text.Append(Profile(p)).Append(". Trip: ").Append(Trip(p)).Append(". Train: ").Append(crowd.World.Train != null ? crowd.World.Train.Status() : "none").Append(". ");
                text.Append("Now: ").Append(p.Doing).Append(". ");
                if (p.Memory.Count > 0) text.Append("Earlier: ").Append(string.Join("; ", p.Memory)).Append(". ");
                text.Append("Clock ").Append(crowd.Session.Clock(crowd.Session.ShiftSeconds)).Append(". Choose what this person does next, as an ordinary traveller at Busan Station would.");
                var q = new JevChoice { Id = item.Key, Instructions = text.ToString() };
                foreach (var choice in item.Choices) q.Criteria[choice.Key] = choice.Description;
                return q;
            }
            text.Append(Profile(p)).Append(", currently ").Append(p.Doing).Append(". ");
            switch (item.Kind)
            {
                case Kind.Notice:
                    text.Append("They now see: '").Append(h.Visible).Append("' about ").Append(Mathf.RoundToInt(Vector3.Distance(p.transform.position, h.Position))).Append(" m away, at ").Append(h.Where).Append(". ");
                    break;
                case Kind.Indirect:
                    text.Append("They cannot see the cause but ").Append(p.Cue ?? "people nearby are reacting").Append(" (").Append(crowd.CountNear(p.transform.position, 10, Passenger.Activity.Evacuate)).Append(" leaving within 10 m). ");
                    break;
                case Kind.Instruction:
                    text.Append(item.Flag ? "A station staff member is telling them directly to leave through an exit. " : "A public announcement asks everyone to leave the area. ");
                    break;
                case Kind.Reevaluate:
                    text.Append("They have been watching '").Append(h?.Visible ?? "the situation").Append("' for a while; it is ").Append(h != null && h.Active ? "still ongoing" : "over").Append(". ");
                    break;
                case Kind.Quake:
                    text.Append("The whole station starts shaking strongly. ");
                    break;
                case Kind.AfterQuake:
                    text.Append("The shaking has just stopped. ").Append(crowd.World.IsClosed(p.transform.position, 8) ? "Something fell from the ceiling close to them. " : "");
                    break;
            }
            text.Append("People within 10 m: ").Append(crowd.CountNear(p.transform.position, 10, null)).Append(", of them leaving: ").Append(crowd.CountNear(p.transform.position, 10, Passenger.Activity.Evacuate)).Append(". ");
            text.Append(p.Instructed ? "They were already told to leave. " : "");
            text.Append("Choose what this person does next, as an ordinary member of the public would.");
            var question = new JevChoice { Id = item.Key, Instructions = text.ToString() };
            foreach (var (option, weight) in item.Options)
                if (weight > 0) question.Criteria[option.Key] = option.Description;
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
    }
}
