using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    // 일상 판단: 활동이 끝나기 전에 미리 물어 두고(낮은 우선순위), 활동이 끝날 때 가져간다. 답이 늦으면 하던 일을 잇는다.
    public sealed partial class CrowdMind
    {
        /// <summary>A routine answer older than this (game seconds) is asked again: the station has moved on.</summary>
        private const float RoutineFreshSeconds = 120f;

        // 자주 쓰는 장소 목록(판단 선택지 재료).
        private List<StationPoints.Point> shops, toilets, exits, meets;
        private List<string> cafes;

        private void InitRoutine()
        {
            var points = crowd.World.Points;
            shops = points.Of(PointKind.Shop).ToList();
            toilets = points.Of(PointKind.Toilet).ToList();
            exits = points.Of(PointKind.Exit).ToList();
            meets = points.Of(PointKind.Meet).ToList();
            cafes = points.Of(PointKind.Chair).Select(c => ShopOf(c)).Distinct().ToList();
        }

        private static string ShopOf(StationPoints.Point chair) => chair.Label.EndsWith(" 의자", StringComparison.Ordinal) ? chair.Label.Substring(0, chair.Label.Length - 3) : chair.Label;

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

        /// <summary>Asks what <paramref name="who"/> does after the current activity, about <paramref name="secondsLeft"/> before it ends.</summary>
        public void Prefetch(Passenger who, float secondsLeft)
        {
            // JEV 없는 근무는 활동이 끝날 때 지역 규칙이 정한다(미리 물을 곳이 없다).
            if (!Usable) return;
            var slot = who.Slot;
            if (slot.Routine == null || slot.Routine.Done) slot.Routine = NewEveryday(who, Trigger.Routine, Mathf.Max(0, secondsLeft - 2), "r");
            if (!slot.RouteAsked) { slot.RouteAsked = true; slot.Route = NewEveryday(who, Trigger.Route, 1e4f, "route"); }
        }

        /// <summary>Right after spawning (no current action yet): JEV's first answer replaces the itinerary walk as soon as it arrives.</summary>
        public void AskFirst(Passenger who)
        {
            if (!Usable) return;
            var slot = who.Slot;
            if (slot.Routine != null && !slot.Routine.Done) return;
            slot.Routine = NewEveryday(who, Trigger.Routine, 0, "r");
            slot.Routine.First = true;
        }

        /// <summary>
        /// True when the person can start their next step now. With JEV that means its answer is there and still makes
        /// sense; otherwise they keep doing what they were doing while the answer is asked for at once. Without JEV local
        /// weights decide, so they never wait.
        /// </summary>
        public bool Ready(Passenger who)
        {
            var slot = who.Slot;
            // 관측에 대한 판단이 진행 중이면 새 일을 시작하지 않는다: 답이 올 때까지 하던 일을 잇는다.
            if (slot.Urgent != null && !slot.Urgent.Done) return false;
            if (!Usable) return true;
            var item = slot.Routine;
            if (item != null && item.Answer != null)
            {
                if (Time.time - item.AnsweredAt <= RoutineFreshSeconds && Viable(item)) return true;
                Finish(item);
                item = null;
            }
            Escalate(who, item);
            return false;
        }

        /// <summary>Takes the next step: false while they must keep waiting; otherwise <paramref name="choice"/> (null: nothing fits, they leave).</summary>
        public bool TryTakeRoutine(Passenger who, out Choice choice)
        {
            choice = null;
            if (!Ready(who)) return false;
            var slot = who.Slot;
            var item = slot.Routine;
            slot.Routine = null;
            if (slot.WaitingSince >= 0)
            {
                float waited = Time.time - slot.WaitingSince;
                Metrics.RoutineWait.Add(waited);
                Metrics.LongestWait = Mathf.Max(Metrics.LongestWait, waited);
                slot.WaitingSince = -1;
            }
            string source;
            if (item != null && item.Answer != null)
            {
                item.Done = true;
                choice = PickRoutine(item, who);
                source = "JEV";
                Metrics.RoutineByJev++;
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

        /// <summary>The answer still leads somewhere: JEV gave some option that fits the world now.</summary>
        private static bool Viable(Judgement item)
        {
            foreach (var choice in item.Choices)
            {
                bool wanted = choice.Key == item.Answer || item.Odds != null && item.Odds.TryGetValue(choice.Key, out var p) && p > 0;
                if (wanted && (choice.Guard == null || choice.Guard())) return true;
            }
            return false;
        }

        /// <summary>
        /// The option JEV drew if it still fits and its place can be held; otherwise the next best by JEV's own probabilities.
        /// </summary>
        private Choice PickRoutine(Judgement item, Passenger who)
        {
            var order = new List<Choice>();
            var drawn = item.Choices.Find(c => c.Key == item.Answer);
            if (drawn != null) order.Add(drawn);
            order.AddRange(item.Choices.Where(c => c != drawn).OrderByDescending(c => item.Odds != null && item.Odds.TryGetValue(c.Key, out var p) ? p : 0));
            foreach (var choice in order)
                if ((choice.Guard == null || choice.Guard()) && Reserve(choice, who)) return choice;
            return null;
        }

        /// <summary>The person has to wait for the next step: the everyday question moves to the emergency lane so nobody stands idle.</summary>
        private void Escalate(Passenger who, Judgement item)
        {
            var slot = who.Slot;
            if (slot.WaitingSince < 0) slot.WaitingSince = Time.time;
            if (item == null || item.Done) { item = NewEveryday(who, Trigger.Routine, 0, "r"); slot.Routine = item; }
            if (item.Urgent) return;
            item.Urgent = true;
            item.Trigger = Trigger.Ended;
            item.Due = Time.time;
            Metrics.Escalations++;
            who.Slot.Log("waiting for the next step: everyday question escalated");
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
                case Passenger.Purpose.Arrive: order = new[] { "meet", "leave_0" }; break;
                case Passenger.Purpose.Greet: order = new[] { "meet_wait", "sit_hall" }; break;
                default: order = new[] { "shop_0", "cafe", "leave_0" }; break;
            }
            Metrics.Itineraries++;
            foreach (var key in order)
            {
                var choice = options.Find(o => o.Key == key);
                if (choice != null && Reserve(choice, who)) return choice;
            }
            foreach (var choice in options) if (Reserve(choice, who)) return choice;
            return null;
        }

        private void ReceiveRoutine(Judgement item, JevAnswer answer)
        {
            var who = item.Who;
            item.Answer = answer.Draw(crowd.World.Random);
            item.Odds = answer.Probabilities;
            if (!item.First || !who.OnItinerary) return;
            // 방금 들어와 일정대로 걷는 사람: 첫 답이 일정을 대신한다.
            var chosen = PickRoutine(item, who);
            if (chosen == null) return;
            item.Done = true;
            who.Slot.Routine = null;
            Metrics.FirstAnswers++;
            Metrics.RoutineByJev++;
            who.Slot.Log("first answer took over from the itinerary: " + chosen.Key);
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
            item.Choices = RoutineOptions(p);
            if (item.Choices.Count == 0) return null;
            var text = new StringBuilder(480);
            text.Append(Profile(p)).Append(". Trip: ").Append(Trip(p)).Append(". Train: ").Append(crowd.World.Train != null ? crowd.World.Train.Status() : "none").Append(". ");
            text.Append("Now: ").Append(p.Doing).Append(". ");
            if (p.Memory.Count > 0) text.Append("Earlier: ").Append(string.Join("; ", p.Memory)).Append(". ");
            if (HasKnowledge(p)) text.Append("They know about the situation described in the state and it may affect what they want to do. ");
            text.Append("Clock ").Append(crowd.Session.Clock(crowd.Session.ShiftSeconds)).Append(". Choose what this person does next, as an ordinary traveller at Busan Station would.");
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

        /// <summary>Somewhere people who know of an incident do not choose to go: close to it, or inside a cordon.</summary>
        private bool Avoided(Passenger p, Vector3 at)
        {
            if (crowd.World.IsClosed(at)) return true;
            foreach (var hazard in p.Noticed)
                if (hazard.Active && hazard.Localized && Mathf.Abs(hazard.Position.y - at.y) < 4f && Vector3.Distance(hazard.Position, at) < hazard.DangerRadius + 6f) return true;
            return false;
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
            void Add(string key, string description, float weight, Passenger.Activity activity, PointKind kind, float seconds, StationPoints.Point place = null, string filter = null, string remember = null, Func<bool> guard = null)
            {
                if (weight <= 0 || place != null && Avoided(p, place.Position)) return;
                list.Add(new Choice { Key = key, Description = description, Weight = weight, Activity = activity, Kind = kind, Seconds = seconds, Place = place, Filter = filter, Remember = remember, Guard = guard });
            }
            bool usedToilet = p.Memory.Any(m => m.Contains("toilet"));
            var toilet = Nearest(toilets, here);
            switch (p.Trip)
            {
                case Passenger.Purpose.Depart when !p.MissedTrain:
                {
                    bool boarding = train != null && (train.BoardingOpen || train.Stage == TrainService.Phase.Closing) && train.Service == p.Service;
                    if (boarding)
                        Add("board_now", "Go down to platform 5·6 now and board car " + p.Car + " (boarding is open; the train leaves in about " + minutes + ")", toTrain < 120 ? 16 : 6, Passenger.Activity.PlatformWait, PointKind.PlatformWait, 0, remember: "went down to platform 5·6 to board",
                            guard: () => train.Service == p.Service && (train.BoardingOpen || train.Stage == TrainService.Phase.Closing));
                    else if (toTrain < 420)
                        Add("platform_early", "Go down to platform 5·6 early and wait by car " + p.Car + "'s door (boarding opens soon; departure in about " + minutes + ")", toTrain < 240 ? 2f : .5f, Passenger.Activity.PlatformWait, PointKind.PlatformWait, 0, remember: "went down to the platform early",
                            guard: () => train.Service < p.Service || train.Service == p.Service && train.Stage < TrainService.Phase.Departing);
                    if (!p.HasTicket) Add("ticket", "Queue at a 2F ticket window for the ticket to Seoul", 3, Passenger.Activity.Queue, PointKind.Counter, world.Range(25, 55), remember: "bought a ticket", guard: () => !p.HasTicket);
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
                    AddExits(list, p, here, .8f, "Give up and leave toward");
                    Add("sit_hall", "Sit on a waiting-hall bench for a while", .8f, Passenger.Activity.Sit, PointKind.Seat, world.Range(60, 180));
                    break;
                case Passenger.Purpose.Arrive:
                    if (p.Partner != null && !p.Met && p.Partner.Current == Passenger.Activity.Meet)
                        Add("meet", "Go to the person waiting for them at " + p.Partner.Crowd.World.Describe(p.Partner.transform.position), 6, Passenger.Activity.Meet, PointKind.Meet, 30, NearestMeet(p.Partner.transform.position), remember: "went to meet their family",
                            guard: () => p.Partner != null && !p.Met && p.Partner.Current == Passenger.Activity.Meet);
                    AddExits(list, p, here, 1f, "Leave the station toward");
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
                    if (p.Memory.Count > 4) AddExits(list, p, here, .3f, "Stop waiting and leave toward");
                    break;
                }
                default:
                    AddShops(list, p, here, 1f, upperFloor);
                    AddCafe(list, p, here, .9f);
                    if (toilet != null) Add("toilet", "Use the toilets (" + toilet.Label + ")", usedToilet ? .02f : .3f, Passenger.Activity.Toilet, PointKind.Toilet, 0, toilet);
                    AddExits(list, p, here, .3f + .25f * p.Memory.Count, "Leave the station toward");
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

        private void AddShops(List<Choice> list, Passenger p, Vector3 here, float weight, bool upper)
        {
            if (weight <= 0 || shops.Count == 0) return;
            var world = crowd.World;
            // 가까운 가게 둘과 다른 층 가게 하나.
            var near = shops.Where(s => !Avoided(p, s.Position)).OrderBy(s => Vector3.Distance(s.Position, here) + world.Range(0, 40)).Take(2).ToList();
            var other = shops.Where(s => Mathf.Abs(s.Position.y - here.y) > 3 && !Avoided(p, s.Position)).OrderBy(_ => world.Random.Next()).FirstOrDefault();
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
            if (sample != null && Avoided(p, sample.Position)) return;
            list.Add(new Choice
            {
                Key = "cafe", Description = "Get something at " + cafe + " (" + (sample != null ? StationFloor(sample.Position) : "") + ") and sit at a table",
                Weight = weight, Activity = Passenger.Activity.Sit, Kind = PointKind.Chair, Filter = cafe, Seconds = world.Range(120, 300), Remember = "sat at " + cafe,
            });
        }

        private void AddExits(List<Choice> list, Passenger p, Vector3 here, float weight, string verb)
        {
            var world = crowd.World;
            int i = 0;
            // 사건이 아는 곳 가까이의 출구는 뒤로 미룬다(다른 출구가 있으면 그쪽을 먼저 후보로 든다).
            foreach (var exit in exits.OrderBy(e => Avoided(p, e.Position) ? 1 : 0).ThenBy(_ => world.Random.Next()).Take(3))
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
    }
}
