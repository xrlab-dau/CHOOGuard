using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Fire detection, suppression, compartment and surveillance equipment as real objects (EquipmentRegistry): the
    /// spot-type smoke and heat detectors on the ceilings that a fire is sensed by (the automatic fire alarm follows the
    /// detector that covers the fire, after the time smoke needs to reach it) and that can trip without a fire. The
    /// alarm is the receiver's: the bell rings and the interlocked doors open until the receiver is reset. Times are game
    /// tuning like <see cref="FireHazard.Growth"/>, not fire-engineering figures; placement and areas follow NFTC 203.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private readonly List<DetectorPoint> detectors = new List<DetectorPoint>();
        /// <summary>Since when a detector has sensed a fire without tripping yet (the smoke's transport time to the ceiling).</summary>
        private readonly Dictionary<(FireHazard fire, DetectorPoint detector), float> sensing = new Dictionary<(FireHazard, DetectorPoint), float>();
        /// <summary>The smoke detector nearest a food shop's kitchen (its fumes drift there), by shop point id.</summary>
        private readonly Dictionary<string, DetectorPoint> kitchenDetectors = new Dictionary<string, DetectorPoint>();
        private readonly Dictionary<string, string> detectorPlaces = new Dictionary<string, string>();
        private float nextDetectorCheck;
        private int trippedDetectors;

        /// <summary>How far cooking fumes reach a smoke detector from a shop's kitchen, metres.</summary>
        private const float FumeReach = 15f;

        partial void BeginFireSafety()
        {
            foreach (var kind in new[] { DetectorPoint.SmokeKind, DetectorPoint.HeatKind, DetectorPoint.BeamKind })
                foreach (var equipment in EquipmentRegistry.OfKind(kind))
                {
                    var detector = equipment.GetComponent<DetectorPoint>();
                    if (detector == null) { Debug.LogError("[IncidentDirector] " + equipment.Id + " 에 DetectorPoint 가 없습니다."); continue; }
                    detector.Bind();
                    // 분리형은 송광부가 한 감지기다. 수광부는 표시등만 함께 켠다.
                    if (!detector.Receiver) detectors.Add(detector);
                }
            detectors.Sort((a, b) => string.CompareOrdinal(a.Equipment.Id, b.Equipment.Id));
            foreach (var shop in world.Points.Of(PointKind.Shop))
            {
                if (KitchenOf(shop) == null) continue;
                DetectorPoint nearest = null;
                float best = FumeReach * FumeReach;
                foreach (var detector in detectors)
                {
                    if (detector.Equipment.Kind != DetectorPoint.SmokeKind || Mathf.Abs(detector.FloorPoint.y - shop.Position.y) > 1.5f) continue;
                    var d = detector.transform.position - shop.Position;
                    d.y = 0;
                    if (d.sqrMagnitude < best) { best = d.sqrMagnitude; nearest = detector; }
                }
                if (nearest != null) kitchenDetectors[shop.Id] = nearest;
            }
            BindSprinklers();
            BindShutters();
            BindCameras();
            BindReceiver();
        }

        partial void EndFireSafety() => UnbindShutters();

        // ── 원인 ──

        partial void FireSafetyOrigins(Pools pools, List<Transition> list)
        {
            SprinklerOrigins(list);
            ShutterOrigins(list);
            if (falseAlarm != null || alarm || detectors.Count == 0) return;
            var picked = new List<(DetectorPoint detector, string en, string ko)>();
            // 조리 연기는 실제 주방 가까운 연기감지기만 울린다.
            foreach (var shop in world.Points.Of(PointKind.Shop).Where(s => kitchenDetectors.ContainsKey(s.Id)).OrderBy(s => Rank(s.Id)).Take(1))
                picked.Add((kitchenDetectors[shop.Id], "cooking fumes from the kitchen of the food shop '" + shop.Label + "' drifting under it", "'" + shop.Label + "' 조리 연기"));
            // 공중화장실의 담배 연기(화장실에는 감지기가 있다: NFTC 203 2.4.5.5 는 샤워 시설이 있는 화장실만 뺀다).
            foreach (var detector in detectors.Where(d => d.Room == "toilet").OrderBy(d => Rank(d.Equipment.Id)).Take(1))
                picked.Add((detector, "someone smoking a cigarette in the public toilet it hangs in", "화장실 흡연 연기"));
            // 감지기 자체의 노후·결로는 구역마다 하나씩 뽑아 역 곳곳에 후보를 둔다.
            foreach (var detector in detectors.Where(d => d.Equipment.Kind == DetectorPoint.SmokeKind && picked.TrueForAll(p => p.detector != d))
                         .GroupBy(d => d.Equipment.Zone).Select(g => g.OrderBy(d => Rank(d.Equipment.Id)).First())
                         .OrderBy(d => Rank(d.Equipment.Id)).Take(2))
                picked.Add((detector, "an ageing detector head fouled by condensation", "노후·결로로 감지기 오동작"));
            foreach (var p in picked) list.Add(DetectorTrips(p.detector, p.en, p.ko));
        }

        private Transition DetectorTrips(DetectorPoint detector, string cause, string causeKo) => new Transition
        {
            Key = "detector_" + detector.Equipment.Id, Kind = "false_alarm", Origin = true,
            Description = "The smoke detector '" + detector.Equipment.Label + "' at " + DetectorPlace(detector) + " trips although nothing is burning (" + cause + ").",
            Apply = _ => StartFalseAlarm(detector, causeKo),
        };

        /// <summary>Where a detector is, for JEV and the radio (cached: the place of a fixed object does not change).</summary>
        private string DetectorPlace(DetectorPoint detector)
        {
            if (!detectorPlaces.TryGetValue(detector.Equipment.Id, out var place)) detectorPlaces[detector.Equipment.Id] = place = Place(detector.FloorPoint);
            return place;
        }

        // ── 적용 ──

        private void StartFalseAlarm(DetectorPoint detector, string cause)
        {
            falseAlarm = new FalseAlarmHazard("detector-" + ++serial, detector, cause) { Where = world.Describe(detector.FloorPoint) };
            Register(falseAlarm);
            RaiseAlarm(detector, falseAlarm, 40);
        }

        /// <summary>
        /// The first detector of the receiver trips: its lamp lights, the bell rings station-wide and the interlocked doors
        /// open (a fire signal opens the escape routes whether or not anything burns), and the office tells the staff the
        /// receiver's zone.
        /// </summary>
        private void RaiseAlarm(DetectorPoint first, Hazard hazard, float followUp)
        {
            alarm = true;
            first.Trip();
            ShutterOnDetector(first);
            trippedDetectors = 1;
            Facilities.StationSignals.FireAlarm = true;
            log.Add("자동화재탐지설비 동작 · 비상벨 · " + first.ZoneName + " · " + first.Equipment.Label);
            Office("역무실입니다. 수신기 " + first.ZoneName + " 화재감지기 동작, " + hazard.Where + "입니다. 현장 확인 바랍니다.");
            Know(hazard, "화재감지기 동작 무전");
            crowd.Alert(hazard.Position, 400, hazard, null, "the fire alarm bell is ringing across the station");
            if (followUp > 0) officeFollowUp = Time.time + followUp;
        }

        private void TripDetector(FireHazard fire, DetectorPoint detector)
        {
            if (!alarm) RaiseAlarm(detector, fire, calledBy.ContainsKey(Agency.Fire) ? -1 : 30);
            else if (!detector.Tripped)
            {
                detector.Trip();
                ShutterOnDetector(detector);
                // 처음 몇 개만 적는다: 번지는 불이면 감지기가 줄줄이 동작한다.
                if (++trippedDetectors <= 4) log.Add("감지기 추가 동작 · " + detector.ZoneName + " · " + detector.Equipment.Label);
            }
        }

        /// <summary>The receiver is reset: every lamp goes out. A detector that still senses smoke trips again after its delay; one that tripped without a fire waits for the facility team.</summary>
        private void ResetDetectors()
        {
            foreach (var detector in detectors) if (detector.Tripped) detector.Restore();
            if (falseAlarm != null && falseAlarm.Active && falseAlarm.Detector != null) falseAlarm.Detector.Equipment.State = "점검 필요";
            sensing.Clear();
            trippedDetectors = 0;
            ClearShutterTriggers();
        }

        // ── 규칙 ──

        partial void FireSafetyTick(float dt)
        {
            ShowReceiver();
            if (Stage != Phase.Incident || Time.time < nextDetectorCheck) return;
            nextDetectorCheck = Time.time + .25f;
            foreach (var fire in fires)
            {
                // 열차 안의 불은 승강장 감지기가 느끼지 못한다. 차량 아래(승강장 쪽)의 불은 승강장 천장 감지기가 느낀다.
                if (fire.Extinguished || fire.Aboard && !fire.Beneath) continue;
                foreach (var detector in detectors)
                {
                    // 불이 난 바닥 위 감지기만: 같은 층이고 불보다 높이 달려 있다.
                    if (detector.transform.position.y - fire.Position.y < .8f || Mathf.Abs(fire.Position.y - detector.FloorPoint.y) > 1.5f) continue;
                    float reach = detector.HorizontalDistance(fire.Position);
                    var key = (fire, detector);
                    if (!Senses(fire, detector, reach)) { sensing.Remove(key); continue; }
                    if (!sensing.TryGetValue(key, out float since)) { sensing[key] = Time.time; continue; }
                    if (Time.time - since >= ResponseDelay(detector)) TripDetector(fire, detector);
                }
            }
            SprinklerFireTick();
            ShutterTick();
        }

        /// <summary>
        /// Whether the fire's smoke (a smoke detector) or heat (a heat detector) reaches the detector: it must lie in the
        /// detector's detection area, and the plume must climb to its ceiling — small fires never reach a high roof.
        /// </summary>
        private static bool Senses(FireHazard fire, DetectorPoint detector, float reach)
        {
            if (detector.Smoke) return detector.MountHeight <= 6f + 16f * fire.Intensity && reach <= detector.Coverage && reach <= fire.SmokeRadius * .67f;
            return fire.Intensity >= .4f + .02f * detector.MountHeight && reach <= Mathf.Min(detector.Coverage * .9f, 2f + 8f * fire.Intensity);
        }

        /// <summary>Seconds from sensing to tripping: smoke needs time to rise and spread along the ceiling, heat needs longer to warm the element.</summary>
        private static float ResponseDelay(DetectorPoint detector) => detector.Smoke ? 2f + .9f * detector.MountHeight : 6f + 1.2f * detector.MountHeight;

        // ── 역무원이 아는 것 ──

        partial void LookAroundFireSafety(Vector3 eye, Vector3 forward)
        {
            LookAroundShutters(eye, forward);
            // 동작한 감지기 아래(같은 층)에서 올려다보면 동작표시등이 켜져 있고 주변에 불이나 연기가 없다는 것을 안다(비화재보).
            if (falseAlarm == null || falseAlarm.Checked || falseAlarm.Detector == null) return;
            var detector = falseAlarm.Detector;
            var toDetector = detector.transform.position - eye;
            var flat = toDetector;
            flat.y = 0;
            if (flat.magnitude > 8f || Mathf.Abs(eye.y - detector.FloorPoint.y) > 2.5f || Vector3.Angle(forward, toDetector) > 60f) return;
            falseAlarm.Check();
            log.Add("역무원이 동작한 감지기 확인 · 동작표시등만 켜져 있고 불이나 연기 없음 · " + falseAlarm.Where);
            session.Hud.Toast("감지기 동작표시등만 켜져 있고 불이나 연기가 없습니다 · 비화재보로 보입니다 · 역무실에 보고하세요", 6f);
        }

        partial void FireSafetyResolved(Hazard hazard)
        {
            // 시설 담당이 감지기를 점검하고 나면 그 감지기는 정상이다.
            if (hazard is FalseAlarmHazard alarmHazard && !alarmHazard.Active && alarmHazard.Detector != null) alarmHazard.Detector.Equipment.State = "정상";
            SprinklerResolved(hazard);
        }

        partial void FireSafetyDevelopments(List<Transition> list)
        {
            SprinklerDevelopments(list);
            ShutterDevelopments(list);
        }

        // ── 무전 ──

        partial void FireSafetyRadio(List<EmergencySession.RadioOption> options)
        {
            // 불이 꺼지고 물이 멈췄는데 벨이 울리고 있으면 역무실에 수신기 복구를 요청한다(비화재보는 보고할 때 복구된다).
            if (alarm && falseAlarm == null && !SprinklerFlowing && !fires.Exists(f => !f.Extinguished))
            {
                bool afterFire = fires.Count > 0;
                options.Add(Option(afterFire ? "역무실 · 수신기 복구 요청 (불 꺼짐 확인)" : "역무실 · 수신기 복구 요청 (화재 아님·물 멈춤 확인)", () =>
                {
                    Say("역무실, " + (afterFire ? "불이 꺼진 것을 확인했습니다." : "화재가 아닌 것을 확인했습니다.") + " 수신기 복구 바랍니다.");
                    Office("역무실 수신. 수신기를 복구하겠습니다.");
                    ResetReceiver("역무원 요청");
                }));
            }
            if (CctvSubject(out var point, out var where))
                options.Add(Option("역무실 · CCTV 로 현장 확인 요청", () =>
                {
                    Say("역무실, " + where + " CCTV 확인 부탁합니다.");
                    CctvCheck(point, where);
                }));
            var left = shutters.FirstOrDefault(s => s.Opening < .999f && !s.Triggered && !s.ControllerFault && !s.Moving);
            if (left != null && !calledBy.ContainsKey(Agency.Facility))
                options.Add(Option("역무실 · 방화셔터 복구 요청 (시설 담당)", () =>
                {
                    Say("역무실, " + left.Equipment.Label + " 내려와 있습니다. 시설 담당 복구 부탁합니다.");
                    Office("역무실 수신. 시설 담당 보내 방화셔터를 복구하겠습니다.");
                    Call(Agency.Facility, "역무원 방화셔터 복구 요청");
                }));
        }
    }
}
