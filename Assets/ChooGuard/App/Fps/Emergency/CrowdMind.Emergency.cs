using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    // 급한 판단: 선택지, 지역 가중치, JEV 답을 지금 상태에 맞춰 실행하는 부분.
    public sealed partial class CrowdMind
    {
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
        private static readonly Option Detour = O("detour", "Gives up on going there and picks something else to do instead", (p, h, w) => p.Replan());
        private static readonly Option OtherExit = O("other_exit", "Heads for a different exit because the way to theirs is blocked", (p, h, w) => p.Reroute());
        private static readonly Option Phone = O("phone_emergency", "Stops and phones 119 or 112 from where they stand instead of walking on to the staff member", (p, h, w) => p.PhoneIn());

        private enum Table { Notice, Indirect, Instruction, Reevaluate, Quake, AfterQuake, Blocked }

        private static Table TableOf(Trigger trigger)
        {
            switch (trigger)
            {
                case Trigger.Quake: return Table.Quake;
                case Trigger.AfterQuake: return Table.AfterQuake;
                case Trigger.Instruction: return Table.Instruction;
                case Trigger.Cue: return Table.Indirect;
                case Trigger.Blocked: return Table.Blocked;
                case Trigger.Notice: return Table.Notice;
                default: return Table.Reevaluate;
            }
        }

        /// <summary>
        /// What this person could do now, each with the weight local rules give it. Weight 0 means it makes no sense for them
        /// right now (JEV is only offered the others); the weights themselves decide only in runs without JEV.
        /// </summary>
        private List<(Option, float)> Options(Judgement item)
        {
            var p = item.Who;
            var h = item.Hazard;
            var table = TableOf(item.Trigger);
            float distance = h != null && h.Localized ? Vector3.Distance(p.transform.position, h.Position) : 0;
            bool near = distance < 7;
            bool seated = p.Body.Seat != PersonBody.SeatPhase.None;
            bool aboard = p.Current == Passenger.Activity.InTrain;
            bool irritated = h != null && h.Irritates(p.transform.position);
            int leaving = crowd.CountNear(p.transform.position, 10, Passenger.Activity.Evacuate);
            var list = new List<(Option, float)>();
            if (aboard && table != Table.Quake)
            {
                bool doorsOpen = crowd.World.Train != null && crowd.World.Train.DoorsOpen > .9f;
                list.Add((StepOff, doorsOpen ? .55f + (near ? .3f : 0) : 0));
                list.Add((StayAboard, .25f));
                list.Add((Watch, .15f));
                list.Add((Alert, .08f));
                return list;
            }
            // 역무원에게 알리러 가는 길이 길어졌다: 계속 갈지, 이 자리에서 119·112 에 전화할지.
            if (p.Current == Passenger.Activity.Report && table == Table.Reevaluate)
            {
                list.Add((Continue, .30f));
                list.Add((Phone, .55f));
                list.Add((Watch, .10f));
                list.Add((Evacuate, p.Instructed ? .3f : .05f));
                return list;
            }
            switch (table)
            {
                case Table.Notice when h is FireHazard fire:
                {
                    float big = Mathf.Clamp01(fire.Intensity);
                    list.Add((Continue, .08f - .06f * big));
                    list.Add((Watch, near ? .06f : .14f));
                    list.Add((Film, near ? .03f : .08f));
                    list.Add((MoveAway, .26f + (near ? .15f : 0)));
                    list.Add((Evacuate, .18f + .2f * big + (irritated ? .3f : 0) + (near ? .1f : 0)));
                    list.Add((Run, .04f + .08f * big));
                    list.Add((Report, CanReport(h) ? .12f : 0));
                    list.Add((Alert, .10f));
                    break;
                }
                case Table.Notice when h is CollapseHazard:
                    list.Add((Help, near ? .35f : .15f));
                    list.Add((Report, CanReport(h) ? .3f : 0));
                    list.Add((Watch, .2f));
                    list.Add((Continue, .15f));
                    break;
                // 선로 위 사람: 뛰어내려 돕지 않는다. 소리쳐 알리고, 역무원을 부르고, 지켜본다.
                case Table.Notice when h is TrackFallHazard:
                    list.Add((Alert, .35f));
                    list.Add((Report, CanReport(h) ? .3f : 0));
                    list.Add((Watch, .2f));
                    list.Add((Film, .05f));
                    list.Add((Continue, .05f));
                    break;
                case Table.Notice when h is DisturbanceHazard:
                    list.Add((MoveAway, .35f + (near ? .15f : 0)));
                    list.Add((Run, .06f + (near ? .1f : 0)));
                    list.Add((Watch, near ? .06f : .15f));
                    list.Add((Film, near ? .03f : .08f));
                    list.Add((Report, CanReport(h) ? .15f : 0));
                    list.Add((Alert, .08f));
                    list.Add((Continue, near ? .02f : .08f));
                    break;
                case Table.Notice when h is GasLeakHazard || h is SuspiciousSubstanceHazard:
                    list.Add((MoveAway, .35f));
                    list.Add((Evacuate, .15f + (irritated ? .2f : 0)));
                    list.Add((Report, CanReport(h) ? .15f : 0));
                    list.Add((Alert, .15f));
                    list.Add((Watch, .05f));
                    list.Add((Continue, .10f));
                    break;
                // 정전: 대개 그 자리에서 기다리거나 휴대전화 불빛으로 둘러보고, 일부는 천천히 밖으로 나간다.
                case Table.Notice when h is PowerOutageHazard:
                    list.Add((Stay, .35f));
                    list.Add((LookAround, .25f));
                    list.Add((Continue, .2f));
                    list.Add((Evacuate, .15f + (p.Instructed ? .2f : 0)));
                    break;
                case Table.Notice when h is WaterLeakHazard || h is FallingObjectHazard:
                    list.Add((MoveAway, .3f));
                    list.Add((Watch, .2f));
                    list.Add((Film, .1f));
                    list.Add((Report, CanReport(h) ? .15f : 0));
                    list.Add((Continue, .25f));
                    break;
                // 처음 보는 종류의 사건도 같은 공개 성질(자리가 있는가, 숨 막히게 하는가)만으로 고른다.
                case Table.Notice:
                    bool placed = h != null && h.Localized;
                    list.Add((Continue, irritated ? .1f : .40f));
                    list.Add((Watch, placed ? .20f : 0));
                    list.Add((MoveAway, placed ? .15f : 0));
                    list.Add((Evacuate, irritated ? .35f : .05f));
                    list.Add((Report, CanReport(h) ? .15f : 0));
                    list.Add((Alert, .10f));
                    list.Add((Stay, placed ? 0 : .3f));
                    list.Add((LookAround, placed ? 0 : .2f));
                    break;
                case Table.Indirect:
                    list.Add((Follow, .45f + (leaving >= 3 ? .2f : 0)));
                    list.Add((LookAround, .35f));
                    list.Add((Continue, .20f));
                    break;
                case Table.Instruction:
                    bool sees = h != null && h.Localized && distance < h.NoticeRadius;
                    list.Add((Comply, (item.Direct ? .82f : .60f) + (sees ? .1f : 0)));
                    list.Add((Hesitate, item.Direct ? .14f : .25f));
                    list.Add((Ignore, item.Direct ? .04f : .15f));
                    break;
                case Table.Reevaluate when h is FireHazard fire:
                    list.Add((Evacuate, .45f + (irritated ? .3f : 0) + (p.Instructed ? .3f : 0)));
                    list.Add((MoveAway, .30f));
                    list.Add((Watch, .15f));
                    list.Add((Continue, .10f));
                    break;
                case Table.Reevaluate:
                    list.Add((Continue, p.Instructed ? .1f : .35f));
                    list.Add((Watch, .20f));
                    list.Add((MoveAway, h != null && h.Localized ? .25f : 0));
                    list.Add((Evacuate, p.Instructed ? .5f : .05f));
                    list.Add((Report, CanReport(h) ? .20f : 0));
                    break;
                case Table.Blocked:
                    if (p.Current == Passenger.Activity.Evacuate)
                    {
                        list.Add((OtherExit, .6f));
                        list.Add((Stay, .2f));
                        list.Add((Run, .1f));
                    }
                    else
                    {
                        list.Add((Detour, .7f));
                        list.Add((Stay, .2f));
                        list.Add((Evacuate, p.Instructed ? .3f : .05f));
                    }
                    break;
                case Table.Quake:
                    list.Add((Cover, seated ? .35f : .50f));
                    list.Add((seated ? HoldOn : Freeze, seated ? .45f : .30f));
                    list.Add((Run, .20f));
                    break;
                case Table.AfterQuake:
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
            // 역 전체가 함께 겪는 일(흔들림, 정전)은 알릴 거리가 아니다.
            if (player == null || hazard is EarthquakeHazard || hazard is PowerOutageHazard) return false;
            // 이미 두 사람이 알리러 갔으면 다른 사람들은 누군가 알렸으리라 여긴다.
            int reporting = 0;
            foreach (var person in crowd.People) if (person.Current == Passenger.Activity.Report && person.Focus == hazard) reporting++;
            if (reporting >= 2) return false;
            if (hazard == null || !hazard.Localized) return true;
            return !(crowd.Session.Incidents.PlayerKnowsIncident && Vector3.Distance(player.transform.position, hazard.Position) < 15);
        }

        // ── 답 적용 ─────────────────────────────────────────────────────────

        /// <summary>
        /// JEV's answer for an emergency question, applied only if it still fits: nothing they observe has changed since the
        /// question was built, the person is not hurt, the situation is not over. An option that stopped making sense
        /// (two people already went to tell staff, the doors closed) is skipped and the draw is repeated over the rest of
        /// JEV's own probabilities.
        /// </summary>
        private void ApplyUrgent(Judgement item, JevAnswer answer, float real)
        {
            var who = item.Who;
            var slot = who.Slot;
            Finish(item);
            // 답을 기다리는 사이 관측이 바뀌었다: 이 답은 낡았고 새 판단이 이미 줄에 있다.
            if (slot.Version != item.Version) { Metrics.Stale++; return; }
            if (who.Hurt || who.Hostile) { Metrics.Moot++; return; }
            var hazard = item.Hazard;
            // 이미 다 된 일(불이 꺼졌다)에 대한 판단은 더 의미가 없다: 하던 일로 돌아간다.
            if (hazard != null && !hazard.Active && item.Trigger != Trigger.Instruction) { Metrics.Moot++; who.KeepGoing(); return; }
            var options = Options(item);
            // JEV 가 가능성을 준 선택지 중 답이 오는 사이 맞지 않게 된 것(두 사람이 이미 알리러 갔다, 문이 닫혔다)을 센다.
            foreach (var key in item.Offered)
                if (Odds(answer, key) > 0 && !options.Exists(o => o.Item1.Key == key && o.Item2 > 0)) { Metrics.Revalidated++; break; }
            var chosen = DrawValid(answer, options, item.Offered);
            if (chosen == null) { Metrics.Moot++; return; }
            float game = Time.time - item.Raised, seconds = real - item.RaisedReal, trip = real - item.SentReal;
            Metrics.UrgentByJev++;
            Metrics.Reaction(game, seconds, trip);
            Perform(item, chosen, "JEV", game, seconds, trip);
        }

        /// <summary>Without JEV: local weights pick among what makes sense now.</summary>
        private void ResolveLocally(Judgement item)
        {
            var who = item.Who;
            Finish(item);
            if (who.Hurt || who.Hostile) return;
            var options = Options(item);
            float total = 0;
            foreach (var (_, weight) in options) total += Mathf.Max(0, weight);
            if (total <= 0) return;
            float roll = (float)crowd.World.Random.NextDouble() * total;
            Option chosen = null;
            foreach (var (option, weight) in options)
            {
                if (weight <= 0) continue;
                chosen = option;
                roll -= weight;
                if (roll <= 0) break;
            }
            Metrics.UrgentLocally++;
            Perform(item, chosen, "local", Time.time - item.Raised, 0, 0);
        }

        /// <summary>One option drawn from JEV's probabilities among those offered and still valid; null when none is.</summary>
        private Option DrawValid(JevAnswer answer, List<(Option option, float weight)> options, HashSet<string> offered)
        {
            float total = 0;
            foreach (var (option, weight) in options)
                if (weight > 0 && (offered == null || offered.Contains(option.Key))) total += Odds(answer, option.Key);
            if (total <= 0) return null;
            float roll = (float)crowd.World.Random.NextDouble() * total;
            Option last = null;
            foreach (var (option, weight) in options)
            {
                if (weight <= 0 || offered != null && !offered.Contains(option.Key)) continue;
                float odds = Odds(answer, option.Key);
                if (odds <= 0) continue;
                last = option;
                roll -= odds;
                if (roll <= 0) return option;
            }
            return last;
        }

        private static float Odds(JevAnswer answer, string key)
        {
            if (answer.Probabilities != null && answer.Probabilities.Count > 0)
                return answer.Probabilities.TryGetValue(key, out var probability) ? Mathf.Max(0, probability) : 0;
            return answer.Choice == key ? 1 : 0;
        }

        private void Perform(Judgement item, Option chosen, string source, float game, float seconds, float trip)
        {
            var who = item.Who;
            var slot = who.Slot;
            crowd.Session.Log.Decision(who, item.Trigger.ToString(), chosen.Key, source);
            slot.JudgedAt = Time.time;
            slot.Acts.Add(chosen.Description);
            if (slot.Acts.Count > 3) slot.Acts.RemoveAt(0);
            Metrics.Record(new JObject
            {
                ["t"] = Math.Round(crowd.Session.ShiftSeconds, 1),
                ["passenger"] = who.Number,
                ["trigger"] = item.Trigger.ToString(),
                ["hazard"] = item.Hazard != null ? item.Hazard.Label : null,
                ["choice"] = chosen.Key,
                ["source"] = source,
                ["offered"] = item.Offered != null ? item.Offered.Count : 0,
                ["batch"] = item.BatchSize,
                ["game_s"] = Math.Round(game, 3),
                ["real_s"] = Math.Round(seconds, 3),
                ["jev_s"] = Math.Round(trip, 3),
            });
            // 이미 대피를 시작한 사람에게 뒤늦은 '구경'·'계속'을 덮어쓰지 않는다.
            if (who.Current == Passenger.Activity.Evacuate && chosen != Evacuate && chosen != Run && chosen != Comply && chosen != Follow && chosen != OtherExit) return;
            chosen.Run(who, item.Hazard, crowd.World);
        }
    }
}
