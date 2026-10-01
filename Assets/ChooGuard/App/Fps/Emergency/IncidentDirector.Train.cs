using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Train family: a closing KTX door catching a bag or an arm, and a person down on the track beside a platform (fell,
    /// or climbed down for a dropped item). The set is held at the platform or stopped short of it; the train crew frees a
    /// door, 119 rescue lifts a person off the track; staff never go down themselves (research.md).
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private readonly List<DoorTrapHazard> traps = new List<DoorTrapHazard>();
        private readonly Dictionary<DoorTrapHazard, GameObject> trapMarkers = new Dictionary<DoorTrapHazard, GameObject>();
        private readonly Dictionary<DoorTrapHazard, int> trapItems = new Dictionary<DoorTrapHazard, int>();
        private readonly Dictionary<DoorTrapHazard, GameObject> trapProps = new Dictionary<DoorTrapHazard, GameObject>();
        private readonly HashSet<DoorTrapHazard> trapShut = new HashSet<DoorTrapHazard>();
        private readonly List<TrackFallHazard> trackFalls = new List<TrackFallHazard>();
        private bool holdRequested;

        private const string TrackHold = "선로에 사람";

        // ── 원인 ──

        private IEnumerable<Transition> TrainOrigins(Roster roster) => Chain(
            DoorOrigins(),
            // 떨어짐과 (떨어뜨린 휴대전화를 주우려) 내려감은 다른 원인이라 따로 센다.
            Passengers(roster, p => TrackFallOf(p, false)),
            Passengers(roster, p => TrackFallOf(p, true)));

        /// <summary>The closing door of any car catches whoever is still boarding at it.</summary>
        private IEnumerable<Transition> DoorOrigins()
        {
            if (Train == null) yield break;
            foreach (var trap in Each(Train.Cars, car => Train.Stage == TrainService.Phase.Closing && LateBoarder(car) is Passenger boarding ? DoorTrap(boarding, car) : null)) yield return trap;
        }

        /// <summary>A person on a platform who can end up on the track: by losing balance, or (<paramref name="deliberate"/>) by climbing down for a dropped phone.</summary>
        private Transition TrackFallOf(Passenger person, bool deliberate)
        {
            if (person.Current == Passenger.Activity.InTrain || person.Current == Passenger.Activity.Sit || deliberate && !person.UsesPhone || world.Points.PlatformAt(person.transform.position) == null) return null;
            return TrackSpot(person, out var bed, out var edge, out var onTrain, out var platform) ? TrackFall(person, platform, bed, edge, onTrain, deliberate) : null;
        }

        /// <summary>Someone still boarding <paramref name="car"/> within 1.5 m of its door: only they can be caught by it.</summary>
        private Passenger LateBoarder(TrainService.Car car) =>
            crowd.People.FirstOrDefault(p => p.Current == Passenger.Activity.Board && !p.Body.Scripted && !p.HeldAtDoor && p.TrainSeat != null && p.TrainSeat.Car == car && Vector3.Distance(p.transform.position, Train.World(car.DoorOutside)) < 1.5f);

        private static readonly List<string> DoorTrapLevels = new List<string> { "a coat hem is caught and pulled free at once", "a bag strap is caught", "a handbag is caught in the door", "a suitcase is caught in the door", "the passenger's arm is caught" };

        private Transition DoorTrap(Passenger person, TrainService.Car car) => new Transition
        {
            Key = "door_" + car.Number, Kind = "door_trap", Origin = true,
            Description = "The closing door of KTX " + car.Label + " at platform 5·6 catches " + Profile(person) + " who is still boarding.",
            Levels = DoorTrapLevels,
            Apply = m => StartDoorTrap(person, car, m),
        };

        private static readonly List<string> TrackClimbLevels = new List<string> { "climbs down and reaches for the phone", "picks it up but struggles to climb back up", "cannot climb back up and wanders along the track", "slips while climbing back and hurts a leg", "falls back down while climbing and lies still" };
        private static readonly List<string> TrackFallLevels = new List<string> { "lands on their feet and stands up", "hurts an ankle and cannot climb back up", "cannot stand up", "hits their head and lies on the track", "lies motionless on the track" };

        private Transition TrackFall(Passenger person, StationPoints.PlatformEntry platform, Vector3 bed, Vector3 edge, bool onTrainTrack, bool deliberate) => new Transition
        {
            Key = (deliberate ? "trackclimb_" : "trackfall_") + person.Number, Kind = deliberate ? "track_climb" : "track_fall", Origin = true,
            Description = deliberate
                ? Profile(person) + ", " + person.Doing + " on " + platform.label + ", drops their phone onto the track and climbs down to get it."
                : Profile(person) + ", " + person.Doing + " near the edge of " + platform.label + ", loses balance and falls onto the track.",
            Levels = deliberate ? TrackClimbLevels : TrackFallLevels,
            Apply = m => StartTrackFall(person, platform, bed, edge, onTrainTrack, deliberate, m),
        };

        /// <summary>
        /// Where on the track beside <paramref name="person"/>'s platform they would end up: the track bed below the edge on
        /// their side (the far side when the KTX stands on theirs), the edge above it, and whether that is the KTX track.
        /// </summary>
        private bool TrackSpot(Passenger person, out Vector3 bed, out Vector3 edge, out bool onTrainTrack, out StationPoints.PlatformEntry platform)
        {
            bed = edge = default;
            onTrainTrack = false;
            platform = world.Points.PlatformAt(person.transform.position);
            if (platform == null) return false;
            var ab = platform.b - platform.a;
            ab.y = 0;
            if (ab.sqrMagnitude < 1) return false;
            var along = ab.normalized;
            var from = person.transform.position - platform.a;
            from.y = 0;
            float t = Mathf.Clamp(Vector3.Dot(from, along), 1, ab.magnitude - 1);
            var centre = platform.a + along * t;
            centre.y = person.transform.position.y;
            var side = Vector3.Cross(Vector3.up, along);
            float s = Vector3.Dot(person.transform.position - centre, side) >= 0 ? 1 : -1;
            bool trainPlatform = Train != null && world.Points.Train != null && platform.id == world.Points.Train.platform;
            float trainSide = trainPlatform ? Mathf.Sign(Vector3.Dot(Train.Carrier.position - centre, side)) : 0;
            // 열차가 서 있는 쪽이면 반대쪽 선로다(열차와 승강장 사이 틈은 다루지 않는다).
            if (trainPlatform && Train.AtPlatform && trainSide == s) s = -s;
            edge = StationWorld.OnNavMesh(centre + side * s * (platform.halfWidth - .6f), 1.5f);
            var beyond = centre + side * s * (platform.halfWidth + 1.4f);
            if (!Physics.Raycast(beyond + Vector3.up * .5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) || hit.point.y > centre.y - .6f) return false;
            bed = hit.point;
            onTrainTrack = trainPlatform && trainSide == s;
            return true;
        }

        // ── 적용 ──

        private void StartDoorTrap(Passenger person, TrainService.Car car, float magnitude)
        {
            // JEV 가 답하는 사이 그 사람이 이미 올라탔거나 열차가 떠났을 수 있다: 그때 그 문 앞에 남은 사람이 끼이고,
            // 아무도 없거나 문이 이미 닫혔으면 일어나지 않는다.
            if (Train.Stage != TrainService.Phase.Closing) { log.Add("출입문이 이미 닫혀 끼임 없음 · KTX " + car.Label); return; }
            if (person == null || person.Current != Passenger.Activity.Board || person.Body.Scripted || person.HeldAtDoor) person = LateBoarder(car);
            if (person == null) { log.Add("출입문이 닫힐 때 문 앞에 남은 사람이 없어 끼임 없음 · KTX " + car.Label); return; }
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
            marker.Act = responder => FreeTrap(trap, "역무원이 끼인 " + what[level] + KoreanText.Object(what[level]) + " 빼냄");
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

        private void StartTrackFall(Passenger person, StationPoints.PlatformEntry platform, Vector3 bed, Vector3 edge, bool onTrainTrack, bool deliberate, float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            var hazard = new TrackFallHazard("track-" + ++serial, person, platform.label, deliberate, level, onTrainTrack, bed, edge) { Where = platform.label + " 선로" };
            trackFalls.Add(hazard);
            // 승강장 끝까지 걸어가(또는 비틀거리다) 선로로 내려선다. 몸은 길찾기에서 벗어나 선로 위에 머문다.
            person.Strand();
            var home = person.Body.Home;
            var path = new List<Vector3> { home.InverseTransformPoint(edge), home.InverseTransformPoint(bed) };
            person.Body.FollowInFrame(home, path, deliberate ? 1.1f : 2.6f, () =>
            {
                if (hazard.Hurt) Down(hazard);
                else if (person != null) person.Body.SetCrouch(deliberate);
            });
            if (onTrainTrack && Train != null) Train.TrackHolds.Add(TrackHold);
            Register(hazard);
            log.Add(hazard.Label + " · " + platform.label + " — " + hazard.Visible);
        }

        /// <summary>The person on the track is hurt: they lie down there (the paramedics treat them once they are brought up).</summary>
        private void Down(TrackFallHazard hazard)
        {
            var person = hazard.Person;
            if (person == null || person.Hurt) return;
            person.Injure(hazard.Deliberate ? "선로에서 올라오다 떨어져 다침" : "승강장에서 선로로 떨어져 다침");
            person.Body.ClearPoses();
            if (hazard.Level >= 3) person.Body.SetDown(true); else person.Body.SetCrouch(true);
        }

        /// <summary>Whether <paramref name="person"/> is still down on a track (medics wait until the rescue team brings them up).</summary>
        private bool OnTrack(Passenger person)
        {
            foreach (var hazard in trackFalls) if (hazard.Active && hazard.Person == person) return true;
            return false;
        }

        /// <summary>Brought back up onto the platform (by the rescue team, or by bystanders reaching down): trains may run again.</summary>
        private void BringUp(TrackFallHazard hazard, string how)
        {
            var person = hazard.Person;
            if (person != null)
            {
                person.Body.ReturnToNavMesh(hazard.Edge);
                if (!person.Hurt) person.ReturnFromTrack();
            }
            if (Train != null && !trackFalls.Exists(t => t.Active && t.OnTrainTrack)) { Train.TrackHolds.Remove(TrackHold); Train.Holds.Remove(TrackHold); }
            log.Add(how + " · " + hazard.Platform);
        }

        // ── 전개 ──

        private IEnumerable<Transition> TrainDevelopments()
        {
            foreach (var trap in traps)
            {
                // 문이 실제로 닫혀 끼인 뒤에만 빼내기·승무원 확인이 일어난다(출발 경고 중에는 아직 열려 있다).
                if (!trap.Active || !trapShut.Contains(trap)) continue;
                var t = trap;
                if (!calledBy.ContainsKey(Agency.Crew) && Ready("crew_notices"))
                    yield return new Transition { Key = "crew_" + t.Id, Kind = "crew_notices", Description = "The KTX conductor notices the door fault light on " + t.Car.Label + " and heads there", Apply = _ => { Call(Agency.Crew, "열차 승무원 자체 확인"); Colleague("열차팀장입니다. " + t.Car.Label + " 출입문 이상 확인하러 갑니다."); } };
                if (Ready("pulls_free"))
                    yield return new Transition { Key = "free_" + t.Id, Kind = "pulls_free", Description = "Other passengers pull the " + t.Car.Label + " door leaves apart enough to free the caught " + (t.Injures ? "arm" : "belongings"), Apply = _ => FreeTrap(t, "승객들이 문을 벌려 끼인 것을 빼냄") };
            }
            foreach (var hazard in trackFalls)
            {
                if (!hazard.Active || hazard.Person == null) continue;
                var h = hazard;
                if (!h.Hurt && Ready("climbs_out_" + h.Id))
                    yield return new Transition { Key = "climbs_out_" + h.Id, Kind = "track_climbs_out", Description = "Passengers on " + h.Platform + " reach down and pull the person on the track back up onto the platform", Apply = _ => { h.ClimbedOut(); HazardRegistry.Remove(h); BringUp(h, "승객들이 선로 위 사람을 끌어올림"); } };
                if (h.Level < 4 && Ready("track_worse_" + h.Id))
                    yield return new Transition { Key = "track_worse_" + h.Id, Kind = "track_worsens", Description = "The person on the track at " + h.Platform + " tries to climb back up by themselves and slips", Apply = _ => { h.Worsen(); if (h.Hurt) Down(h); log.Add("선로 위 승객이 올라오려다 미끄러짐 · " + h.Platform); } };
                var reporter = NearestPerson(h.Edge, 25, p => !p.Hostile && !p.Hurt && p.Noticed.Contains(h) && p.Current != Passenger.Activity.Report && p.Current != Passenger.Activity.Evacuate);
                if (reporter != null && !known.Contains(h) && Ready("passenger_reports"))
                    yield return new Transition { Key = "treport_" + reporter.Number, Kind = "passenger_reports", Description = "A passenger on " + h.Platform + " runs to tell the station staff member that someone is on the track", Apply = _ => { reporter.ReportToStaff(); log.Add("승객이 선로 위 사람을 역무원에게 알리러 감"); } };
            }
        }

        // ── 규칙 ──

        private void TrainTick()
        {
            if (Stage != Phase.Incident) return;
            if (traps.Count > 0) ShowCaughtItems();
            foreach (var hazard in trackFalls)
                if (hazard.Active && Time.time - hazard.StartedAt > 45 && CountAware(hazard) >= 2) CitizenCall(null, hazard);
        }

        private void CrewArrived()
        {
            foreach (var trap in traps.Where(t => t.Active).ToList()) FreeTrap(trap, "열차 승무원이 출입문을 다시 열어 빼냄");
        }

        private void TrainReported(Hazard hazard)
        {
            // 선로 위 사람: 역무실이 관제에 그 승강장 열차 진입 중지를 요청한다(보고 전에도 열차는 이미 서 있을 수 있다).
            if (hazard is TrackFallHazard track && track.OnTrainTrack && Train != null) Train.TrackHolds.Add(TrackHold);
        }

        private void TrainResolved(Hazard hazard)
        {
            if (hazard is TrackFallHazard track) BringUp(track, "소방대가 선로 위 사람을 구조");
        }

        private bool NearTrain(Hazard hazard)
        {
            if (Train == null) return false;
            if (hazard is DoorTrapHazard || hazard is FireHazard fire && fire.Aboard || hazard is TrackFallHazard track && track.OnTrainTrack) return true;
            return world.Points.PlatformAt(hazard.Position)?.id == world.Points.Train?.platform || Train.CarAt(hazard.Position) != null;
        }

        private IEnumerable<EmergencySession.RadioOption> TrainRadio()
        {
            if (Train != null && Train.AtPlatform && !holdRequested && known.Any(NearTrain))
                yield return Option("역무실 · 5·6 타는 곳 열차 출발 보류 요청", () =>
                {
                    holdRequested = true;
                    Train.Holds.Add("역무원 요청");
                    Say("역무실, 5·6 타는 곳 서울행 열차 출발 보류 요청합니다.");
                    Office("역무실 수신. 관제에 출발 보류 요청했습니다.");
                    log.Add("열차 출발 보류 요청");
                });
        }

        private void TrainActions(BoardOverlay.Column column)
        {
            if (Train != null && known.Any(NearTrain)) column.Lines.Add((Train.Holds.Count > 0 || Train.TrackHolds.Count > 0 ? "● " : "○ ") + "열차 출발 보류·진입 정지");
        }
    }
}
