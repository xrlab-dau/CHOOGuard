using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Security family — terror and crime shown only through what people can observe and how staff respond: a suitcase
    /// left behind, an aggressive passenger, a phoned bomb threat, an unknown white powder. Staff report, keep people
    /// away, cordon, announce and hand over to the police (and 119 hazmat for the powder); they never restrain, open or
    /// touch anything themselves (public guidance, research.md).
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private readonly List<SuspiciousItemHazard> bags = new List<SuspiciousItemHazard>();
        private readonly Dictionary<SuspiciousItemHazard, (float at, bool leaves)> ownerLeaves = new Dictionary<SuspiciousItemHazard, (float, bool)>();
        private readonly List<DisturbanceHazard> disturbances = new List<DisturbanceHazard>();
        private readonly Dictionary<DisturbanceHazard, float> nextMove = new Dictionary<DisturbanceHazard, float>();
        private readonly List<SuspiciousSubstanceHazard> substances = new List<SuspiciousSubstanceHazard>();
        private BombThreatHazard threat;

        // ── 원인 ──

        // 792 개 좌석을 한 번만 순위대로 세워 두고 매번 앞에서부터 닫히지 않은 첫 자리를 고른다(목록을 1 s 마다 만들기 때문).
        private List<StationPoints.Point> seatsByRank;

        private IEnumerable<Transition> SecurityOrigins(Pools pools)
        {
            foreach (var p in pools.Spread(p => p.Luggage == 2 && Settled(p) && p.Current != Passenger.Activity.InTrain, 2)) yield return BagLeft(p);
            yield return null;
            foreach (var p in pools.Spread(p => !p.Elderly && p.Current != Passenger.Activity.InTrain && p.Current != Passenger.Activity.Sit && p.Current != Passenger.Activity.Toilet, 2)) yield return Aggression(p);
            yield return null;
            if (threat == null) yield return ThreatCall();
            seatsByRank = seatsByRank ?? world.Points.Of(PointKind.Seat).OrderBy(s => Rank(s.Id)).ToList();
            var seat = seatsByRank.FirstOrDefault(s => !world.IsClosed(s.Position, 2));
            if (seat != null) yield return Powder(seat);
        }

        private Transition BagLeft(Passenger owner) => new Transition
        {
            Key = "bag_" + owner.Number, Kind = "bag_left", Origin = true,
            Description = Profile(owner) + ", " + owner.Doing + " at " + Place(owner.transform.position) + ", will get up and walk away leaving the suitcase behind.",
            Levels = new List<string> { "an absent-minded traveller who stays in the building", "walks off but lingers within the station", "walks off and heads for the exit", "walks off quickly after placing the suitcase out of the way", "hurries out of the station after looking around nervously" },
            Apply = m => StartBag(owner, m),
        };

        private Transition Aggression(Passenger person) => new Transition
        {
            Key = "aggression_" + person.Number, Kind = "disturbance", Origin = true,
            Description = Profile(person) + ", " + person.Doing + " at " + Place(person.transform.position) + ", who has been drinking, starts shouting at the people around them.",
            Levels = new List<string> { "shouts and swears at people nearby", "shouts and shoves people out of the way", "shouts and throws things around", "brandishes an object and threatens people", "lashes out and hits a bystander" },
            Apply = m => StartDisturbance(person, m),
        };

        private Transition ThreatCall() => new Transition
        {
            Key = "bomb_threat", Kind = "bomb_threat", Origin = true,
            Description = "The station office gets a phone call: a caller says an explosive has been placed somewhere in Busan Station.",
            Levels = new List<string> { "a short call that sounds like a prank", "a threat that names no place", "a threat that names a place in the station", "a threat that names a place and a time", "a detailed threat claiming several devices" },
            Apply = StartThreat,
        };

        private Transition Powder(StationPoints.Point seat) => new Transition
        {
            Key = "powder_" + seat.Id, Kind = "suspicious_powder", Origin = true,
            Description = "A torn envelope spilling an unknown white powder is lying on the floor beside the seats at " + Place(seat.Position) + ".",
            Levels = new List<string> { "a little powder scattered on the floor", "a torn envelope with powder spilt around it", "people close by start coughing", "people close by complain their eyes and throats sting", "someone close by struggles to breathe and sits down" },
            Apply = m => StartSubstance(seat, m),
        };

        // ── 적용 ──

        private void StartBag(Passenger owner, float magnitude)
        {
            var spot = Floor(owner.transform.position + owner.transform.right * .55f);
            var bag = new SuspiciousItemHazard("bag-" + ++serial, spot, owner.transform.rotation * Quaternion.Euler(0, 90, 0), owner, art, root) { Where = world.Describe(spot) };
            bags.Add(bag);
            ownerLeaves[bag] = (Time.time + world.Range(12, 30), magnitude >= .5f);
            CordonMarker(bag, bag.View, "주인 없는 여행가방");
            // 가방은 주인이 떠나야 위험이 된다: 사건의 시작으로 기록은 하되 위험 목록에는 주인이 멀어질 때 올린다.
            Register(bag);
        }

        /// <summary>An unattended bag found during a bomb threat: nobody is near it and it is already out of place.</summary>
        private void FoundBag(Vector3 near)
        {
            var spot = Floor(StationWorld.OnNavMesh(near + new Vector3(world.Range(-1.5f, 1.5f), 0, world.Range(-1.5f, 1.5f)), 2f));
            var bag = new SuspiciousItemHazard("bag-" + ++serial, spot, Quaternion.Euler(0, world.Range(0, 360), 0), null, art, root) { Where = world.Describe(spot) };
            bags.Add(bag);
            CordonMarker(bag, bag.View, "주인 없는 여행가방");
            Register(bag);
            log.Add("주인 없는 여행가방이 놓여 있음 · " + bag.Where);
        }

        private void StartDisturbance(Passenger person, float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var disturbance = new DisturbanceHazard("disturbance-" + ++serial, person, level) { Where = world.Describe(person.transform.position) };
            disturbances.Add(disturbance);
            person.TurnAggressive();
            Register(disturbance);
            log.Add("난동 · " + disturbance.Where + " — " + disturbance.Visible);
            if (level >= 4) Assault(disturbance);
        }

        /// <summary>The aggressive person hits or shoves someone next to them hard: that person is hurt.</summary>
        private void Assault(DisturbanceHazard disturbance)
        {
            var victim = NearestPerson(disturbance.Position, 4, p => !p.Hostile && !p.Hurt && p.Current != Passenger.Activity.InTrain);
            if (victim == null) return;
            var casualty = AddCasualty(victim, "폭행 부상", "난동 승객에게 맞아 쓰러진", "was hit by the aggressive passenger", "난동 승객에게 맞아 넘어진 승객", -1);
            casualty.Told = "에서 사람이 맞아서 쓰러졌어요!";
            victim.Injure("난동 승객에게 맞아 넘어짐", true);
            Register(casualty);
            log.Add("난동 승객이 휘두른 것에 승객 1명이 맞아 쓰러짐 · " + casualty.Where);
        }

        private void StartThreat(float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var (spot, claimed) = ClaimedPlace(level);
            threat = new BombThreatHazard("threat-" + ++serial, spot, claimed, level);
            Register(threat);
            // 협박 전화는 역무실이 받는다: 바로 112 에 신고하고 역무원에게 알린다.
            reported.Add(threat);
            Call(Agency.Police, "역무실 신고");
            Office("역무실입니다. 방금 '" + claimed + "에 폭발물을 설치했다'는 협박 전화가 왔습니다. 112에 신고했습니다. 순회하며 수상한 물건이 있는지 살피고, 발견하면 절대 만지지 말고 보고하십시오." +
                (level >= 3 ? " 경찰이 대피를 권고했습니다. 대피 안내방송을 요청하십시오." : ""));
            Know(threat, "역무실 무전(협박 전화)");
            log.Add("폭발물 협박 전화 · " + threat.Visible);
        }

        /// <summary>The part of the station a caller names: a busy zone at higher levels, nowhere in particular at the lowest.</summary>
        private (Vector3 spot, string claimed) ClaimedPlace(int level)
        {
            var zones = world.Points.Zones.Where(z => z.id != "plaza" && z.id != "skyplaza").ToList();
            var zone = zones.OrderByDescending(z => crowd.People.Count(p => world.ZoneId(p.transform.position) == z.id) + world.Random.Next(6)).FirstOrDefault();
            var centre = zone != null ? StationWorld.WalkableNear((zone.min + zone.max) * .5f, 30f) : PlayerPosition;
            return (centre, level < 2 || zone == null ? "역 안 어딘가" : zone.label);
        }

        private void StartSubstance(StationPoints.Point seat, float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var spot = Floor(StationWorld.OnNavMesh(seat.Position + Quaternion.Euler(0, seat.Yaw, 0) * Vector3.forward * .7f, 1f));
            var substance = new SuspiciousSubstanceHazard("powder-" + ++serial, spot, level, art, root) { Where = world.Describe(spot) };
            substances.Add(substance);
            CordonMarker(substance, substance.View, "흰 가루");
            Register(substance);
            log.Add("의심 물질 · " + substance.Where + " — " + substance.Visible);
            if (level >= 4)
            {
                var victim = NearestPerson(spot, 4, p => !p.Hostile && !p.Hurt && p.Current != Passenger.Activity.InTrain);
                if (victim != null) victim.Injure("흰 가루 가까이에서 숨쉬기 힘들어함", false);
            }
        }

        private void CuriousPassenger(Passenger person, SuspiciousItemHazard bag)
        {
            log.Add("승객 한 명이 가방을 확인하러 다가갔다 · " + bag.Where);
            person.MoveAway(0);
            person.Body.GoTo(StationWorld.OnNavMesh(bag.Position + (person.transform.position - bag.Position).normalized * .7f, 1), 1.2f);
            StartCoroutine(TouchWhenClose(person, bag));
        }

        private IEnumerator TouchWhenClose(Passenger person, SuspiciousItemHazard bag)
        {
            float until = Time.time + 25;
            while (person != null && Time.time < until && !bag.Cordoned && !person.Instructed)
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

        /// <summary>Police reached the aggressive person: they stop, and after a moment are walked out of the station.</summary>
        public void Restrain(DisturbanceHazard disturbance)
        {
            if (disturbance.Restrained || disturbance.Person == null) return;
            disturbance.Restrain();
            disturbance.Person.Body.Stop();
            disturbance.Person.Body.ClearPoses();
            log.Add("철도경찰이 난동 승객을 제지 · " + disturbance.Where);
            session.Hud.Radio.Push(RadioChannel.Police, "철도경찰입니다. 난동 승객 제지했습니다. 역 밖으로 데리고 나가겠습니다.");
            StartCoroutine(Escort(disturbance));
        }

        private IEnumerator Escort(DisturbanceHazard disturbance)
        {
            yield return new WaitForSeconds(4);
            var person = disturbance.Person;
            if (person == null) yield break;
            var exit = world.Points.Nearest(PointKind.Exit, person.transform.position) ?? world.RandomExit();
            person.LedAway(exit);
        }

        // ── 전개 ──

        private IEnumerable<Transition> SecurityDevelopments()
        {
            foreach (var bag in bags)
            {
                if (!bag.Unattended || !bag.Active) continue;
                var b = bag;
                var curious = NearestPerson(b.Position, 15, p => !p.Hostile && !p.Hurt && p.Current != Passenger.Activity.Evacuate && p.Noticed.Contains(b));
                if (curious != null && !b.Touched && !b.Cordoned && Ready("curious_passenger"))
                    yield return new Transition { Key = "curious_" + curious.Number, Kind = "curious_passenger", Description = "A curious passenger walks up to the unattended suitcase at " + b.Where + " to check whose it is", Apply = _ => CuriousPassenger(curious, b) };
                var reporter = NearestPerson(b.Position, 30, p => !p.Hostile && !p.Hurt && p.Noticed.Contains(b) && p.Current != Passenger.Activity.Report && p.Current != Passenger.Activity.Evacuate);
                if (reporter != null && !known.Contains(b) && Ready("passenger_reports"))
                    yield return new Transition { Key = "report_" + reporter.Number, Kind = "passenger_reports", Description = "A passenger who noticed the suitcase at " + b.Where + " goes to tell the station staff member", Apply = _ => { reporter.ReportToStaff(); log.Add("가방을 본 승객이 역무원에게 알리러 감"); } };
            }
            foreach (var disturbance in disturbances)
            {
                if (!disturbance.Active || disturbance.Restrained || disturbance.Person == null) continue;
                var d = disturbance;
                if (d.Level < 4 && Ready("escalates_" + d.Id))
                    yield return new Transition { Key = "escalates_" + d.Id, Kind = "disturbance_escalates", Description = "The aggressive passenger at " + d.Where + " gets more agitated (" + DisturbanceLevels[d.Level + 1] + ")", Apply = _ => { d.Escalate(1); log.Add("난동이 심해짐 · " + d.Visible); if (d.Level >= 4) Assault(d); } };
                if (d.Level > 0 && Ready("calms_" + d.Id))
                    yield return new Transition { Key = "calms_" + d.Id, Kind = "disturbance_calms", Description = "The aggressive passenger at " + d.Where + " calms down a little and just mutters", Apply = _ => { d.Escalate(-1); log.Add("난동 승객이 조금 누그러짐"); } };
                var reporter = NearestPerson(d.Position, 25, p => !p.Hostile && !p.Hurt && p.Noticed.Contains(d) && p.Current != Passenger.Activity.Report && p.Current != Passenger.Activity.Evacuate);
                if (reporter != null && !known.Contains(d) && Ready("passenger_reports"))
                    yield return new Transition { Key = "dreport_" + reporter.Number, Kind = "passenger_reports", Description = "A passenger runs to tell the station staff member about the aggressive passenger at " + d.Where, Apply = _ => { reporter.ReportToStaff(); log.Add("승객이 난동을 역무원에게 알리러 감"); } };
                if (CitizenMayCall(d) && Ready("citizen_calls_112"))
                    yield return new Transition { Key = "call112_" + d.Id, Kind = "citizen_calls_112", Description = "Someone near " + d.Where + " calls 112 about the aggressive passenger", Apply = _ => CitizenCall(null, d) };
            }
            if (threat != null && threat.Active)
            {
                if (threat.Level < 4 && Ready("threat_again"))
                    yield return new Transition { Key = "threat_again", Kind = "threat_calls_again", Description = "The caller rings the station office again with more detail about where the device is", Apply = _ => CallsAgain() };
                if (Ready("bag_found"))
                    yield return new Transition { Key = "bag_found", Kind = "bag_found", Description = "An unattended bag is lying in " + threat.Claimed + " with nobody near it", Apply = _ => FoundBag(threat.Position) };
            }
            foreach (var substance in substances)
            {
                if (!substance.Active) continue;
                var s = substance;
                var toucher = NearestPerson(s.Position, 8, p => !p.Hostile && !p.Hurt && p.Noticed.Contains(s) && p.Current != Passenger.Activity.Evacuate);
                if (toucher != null && !s.Cordoned && Ready("touches_powder"))
                    yield return new Transition { Key = "touch_" + toucher.Number, Kind = "touches_powder", Description = "A passenger bends down and touches the white powder at " + s.Where + " to see what it is", Apply = _ => { toucher.Body.GoTo(StationWorld.OnNavMesh(s.Position, 1f), 1.1f); toucher.Watch(15, false); log.Add("승객 한 명이 흰 가루를 만졌다 · " + s.Where); } };
                if (CitizenMayCall(s) && Ready("citizen_calls_112"))
                    yield return new Transition { Key = "callpowder_" + s.Id, Kind = "citizen_calls_112", Description = "Someone near " + s.Where + " calls 112 about the white powder", Apply = _ => CitizenCall(null, s) };
            }
        }

        private static readonly string[] DisturbanceLevels = { "shouts and swears at people nearby", "shouts and shoves people out of the way", "shouts and throws things around", "brandishes an object and threatens people", "lashes out and hits a bystander" };

        private void CallsAgain()
        {
            var (spot, claimed) = ClaimedPlace(Mathf.Max(2, threat.Level + 1));
            threat.CallsAgain(spot, claimed);
            Office("역무실입니다. 협박범이 다시 전화했습니다. '" + claimed + "'라고 합니다. 그쪽을 먼저 살피되 수상한 물건은 만지지 마십시오.");
            log.Add("협박 전화 재수신 · " + claimed);
        }

        // ── 규칙 ──

        private void SecurityTick()
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
            foreach (var disturbance in disturbances) if (disturbance.Active && !disturbance.Restrained) Rampage(disturbance);
            if (Stage != Phase.Incident) return;
            foreach (var bag in bags)
                if (bag.Unattended && Time.time - bag.UnattendedAt > 150 && CountAware(bag) >= 3) CitizenCall(null, bag);
            foreach (var disturbance in disturbances)
                if (disturbance.Active && Time.time - disturbance.StartedAt > 60 && CountAware(disturbance) >= 2) CitizenCall(null, disturbance);
        }

        /// <summary>
        /// The aggressive person keeps to the area where it started: walks at people nearby and gestures at them, or paces.
        /// Anyone they get right up to is pushed back and reacts (from the second level on).
        /// </summary>
        private void Rampage(DisturbanceHazard disturbance)
        {
            var person = disturbance.Person;
            if (person == null || person.Body.Seat != PersonBody.SeatPhase.None || person.Body.Scripted) return;
            var target = NearestPerson(person.transform.position, 7, p => !p.Hostile && !p.Hurt && p.Current != Passenger.Activity.InTrain && p.Current != Passenger.Activity.Evacuate);
            if (target != null && disturbance.Level >= 1 && Vector3.Distance(person.transform.position, target.transform.position) < 1.3f)
            {
                target.Notice(disturbance, false);
                if (!target.Noticed.Contains(disturbance) || target.Focus == disturbance) target.MoveAway(disturbance.DangerRadius + world.Range(4, 8));
            }
            if (nextMove.TryGetValue(disturbance, out var at) && Time.time < at) return;
            nextMove[disturbance] = Time.time + world.Range(3, 6);
            Vector3 goal;
            if (target != null && world.Chance(.6f)) goal = target.transform.position;
            else goal = StationWorld.OnNavMesh(disturbance.Home + new Vector3(world.Range(-6, 6), 0, world.Range(-6, 6)), 2f);
            person.Body.GoTo(goal, 1.1f + .2f * disturbance.Level);
            person.Body.Wave();
        }

        private float SecurityDanger(Vector3 eye)
        {
            float danger = 0;
            foreach (var disturbance in disturbances)
                if (disturbance.Active && !disturbance.Restrained && Vector3.Distance(eye, disturbance.Position) < disturbance.DangerRadius + 1) danger = Mathf.Max(danger, .6f);
            foreach (var substance in substances) if (substance.Irritates(eye)) danger = Mathf.Max(danger, .5f);
            return danger;
        }

        private IEnumerable<EmergencySession.RadioOption> SecurityRadio()
        {
            foreach (var hazard in all.Where(h => h.Cordoned && reported.Contains(h) && !reportedDone.Contains(h)).Take(1))
            {
                var h = hazard;
                yield return Option("역무실 · " + h.Label + " 주변 통제 완료 보고", () =>
                {
                    reportedDone.Add(h);
                    Say("역무실, " + h.Named + " 주변 통제선 설치했습니다.");
                    Office("역무실 수신. " + Responder.AgencyName(h.Command) + " 도착까지 접근 통제 유지 바랍니다.");
                    log.Add("역무실에 통제 완료 보고 · " + h.Label);
                });
            }
        }
    }
}
