using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The automatic fire shutters of the compartment lines as real objects: a linked detector lowers the curtain (smoke: to a gap people can still get out through, heat:
    /// all the way), it stays down until the receiver is reset and a person presses the reset switch at the control box, and the interlock can fail on its own
    /// (<see cref="ShutterFaultHazard"/>). A lowered curtain shuts its passage to the player, the crowd and the routes the director closes.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private readonly List<FireShutterPoint> shutters = new List<FireShutterPoint>();
        private readonly Dictionary<string, FireShutterPoint> shutterById = new Dictionary<string, FireShutterPoint>();
        private readonly HashSet<FireShutterPoint> shutterClosed = new HashSet<FireShutterPoint>();
        private readonly HashSet<FireShutterPoint> shutterSeen = new HashSet<FireShutterPoint>();
        private ShutterFaultHazard shutterFault;

        private const string ShutterClosure = "방화셔터";
        private const float ShutterClosureRadius = 3.5f;

        private void BindShutters()
        {
            foreach (var equipment in EquipmentRegistry.OfKind(FireShutterPoint.ShutterKind).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var shutter = equipment.GetComponent<FireShutterPoint>();
                shutters.Add(shutter);
                shutterById[equipment.Id] = shutter;
                shutter.Caught += ShutterCaught;
            }
            FireShutterPoint.Commanded += ShutterCommanded;
        }

        private void UnbindShutters() => FireShutterPoint.Commanded -= ShutterCommanded;

        /// <summary>A detector of a shutter tripped: smoke lowers the curtain to the gap, heat closes it (Building Act enforcement rules art. 14 (2) 4).</summary>
        private void ShutterOnDetector(DetectorPoint detector)
        {
            if (detector.ShutterId.Length == 0 || !shutterById.TryGetValue(detector.ShutterId, out var shutter)) return;
            bool heat = !detector.Smoke;
            bool wasDown = shutter.Opening < .999f;
            shutter.Trigger(heat, detector.Equipment.Label);
            if (!wasDown || heat) log.Add((heat ? "방화셔터 완전 폐쇄(열감지)" : "방화셔터 1단 하강(연기감지)") + " · " + shutter.Equipment.Label);
        }

        /// <summary>The receiver is reset: shutters lose their signal. They stay down until someone raises them.</summary>
        private void ClearShutterTriggers()
        {
            foreach (var shutter in shutters) if (shutter.Triggered) shutter.ClearTrigger();
        }

        private void ShutterTick()
        {
            foreach (var shutter in shutters)
            {
                bool blocking = shutter.Blocking;
                if (blocking && shutterClosed.Add(shutter)) world.Closed.Add((shutter.transform.position, ShutterClosureRadius, ShutterClosure));
                else if (!blocking && shutterClosed.Remove(shutter))
                {
                    var at = shutter.transform.position;
                    world.Closed.RemoveAll(c => c.Item3 == ShutterClosure && (c.Item1 - at).sqrMagnitude < 1f);
                }
                if (shutter.Opening >= .999f) shutterSeen.Remove(shutter);
            }
            if (shutterFault == null || !shutterFault.Active) return;
            var fault = shutterFault.Shutter;
            if (shutterFault.Caught != null && !shutterFault.Freed && fault.ClearHeight > 1.9f)
            {
                shutterFault.Freed = true;
                log.Add("끼인 승객을 구함 · " + fault.Equipment.Label);
            }
            // 제어기를 복구하고 셔터가 다 올라가면 사건은 끝난다.
            if (!fault.ControllerFault && fault.Opening >= .999f)
            {
                shutterFault.Cleared();
                HazardRegistry.Remove(shutterFault);
                log.Add("방화셔터 복구 · " + fault.Equipment.Label);
                Resolved(shutterFault);
            }
        }

        /// <summary>Someone works a control box or the interlock moves the curtain: for the log and the office's follow-up.</summary>
        private void ShutterCommanded(FireShutterPoint shutter, FireShutterPoint.Command command, string by)
        {
            string what = command == FireShutterPoint.Command.Up ? "상향" : command == FireShutterPoint.Command.Down ? "하향" : command == FireShutterPoint.Command.Stop ? "정지" : "복구 스위치";
            log.Add("방화셔터 " + what + " (" + by + ") · " + shutter.Equipment.Label);
        }

        /// <summary>The curtain came down on a person whose sensor is dead: they are caught under it.</summary>
        private void ShutterCaught(FireShutterPoint shutter, GameObject who)
        {
            var person = who.GetComponent<Passenger>();
            if (person == null || person.Hurt) return;
            shutter.Operate(FireShutterPoint.Command.Stop, "끼임");
            var casualty = AddCasualty(person, "방화셔터에 끼임", "방화셔터에 끼인", "caught under the fire shutter", "방화셔터 아래에 끼인 승객", 2);
            casualty.Told = " 방화셔터 아래에 사람이 끼었어요! 빨리 와 주세요.";
            person.Injure("방화셔터가 내려오며 끼임");
            Register(casualty);
            if (shutterFault != null && shutterFault.Shutter == shutter) shutterFault.Caught = person;
            log.Add("방화셔터에 승객이 끼임 · " + shutter.Equipment.Label);
        }

        // ── 원인 ──

        private IEnumerable<Transition> ShutterOrigins() => Each(shutters, ShutterFaultOf);

        private static readonly List<string> ShutterFaultLevels = new List<string>
        {
            "it stops half way and leaves a gap", "it comes down whole and cuts the passage", "it comes down whole and people crowd in front of it",
            "it comes down and its obstacle sensor does not work", "it comes down with the sensor dead onto a person who is walking under it",
        };

        /// <summary>The interlock of any shutter that is up, with no fire signal and a working controller, can fail on its own (one fault at a time).</summary>
        private Transition ShutterFaultOf(FireShutterPoint shutter)
        {
            if (shutterFault != null && shutterFault.Active || shutter.Opening < .999f || shutter.Triggered || shutter.ControllerFault) return null;
            return new Transition
            {
                Key = "shutter_fault_" + shutter.Equipment.Id, Kind = "shutter_malfunction", Origin = true,
                Description = "The interlock control of the fire shutter '" + shutter.Equipment.Label + "' at " + FixedPlace(shutter.transform.position) + " fails and the curtain starts to come down by itself although the receiver shows no fire signal.",
                Levels = ShutterFaultLevels,
                Apply = m => StartShutterFault(shutter, m),
            };
        }

        private void StartShutterFault(FireShutterPoint shutter, float magnitude)
        {
            int level = Mathf.Clamp(Mathf.RoundToInt(magnitude * 4), 0, 4);
            shutter.Malfunction(level == 0 ? FireShutterPoint.GapHeight / shutter.Height : 0f, level >= 3);
            shutterFault = new ShutterFaultHazard("shutter-" + ++serial, shutter, level) { Where = world.Describe(shutter.transform.position) };
            Register(shutterFault);
            log.Add("방화셔터 오동작 · " + shutterFault.Where + " — " + shutterFault.Visible);
            if (level >= 2)
                foreach (var person in crowd.People.ToArray())
                {
                    var d = person.transform.position - shutter.transform.position;
                    if (Mathf.Abs(d.y) < 3 && d.magnitude < 14 && !person.Hostile && !person.Hurt) person.Watch(12, false);
                }
            if (level == 4)
            {
                // 셔터 선을 가로질러 가던 사람이 있다: 셔터 쪽으로 서둘러 걸어가게 한다.
                var walker = NearestPerson(shutter.transform.position, 14, p => !p.Hurt && !p.Hostile && p.Current == Passenger.Activity.Walk);
                if (walker != null)
                {
                    float side = Mathf.Sign(Vector3.Dot(walker.transform.position - shutter.transform.position, shutter.transform.forward));
                    walker.Body.GoTo(StationWorld.OnNavMesh(shutter.transform.position - shutter.transform.forward * side * 2.5f, 2f), 1.5f);
                }
            }
        }

        // ── 전개 ──

        private void ShutterDevelopments(List<Transition> list)
        {
            foreach (var shutter in shutters)
            {
                var s = shutter;
                string id = shutter.Equipment.Id;
                // 오동작이 반쯤에서 멈췄다면 나머지도 내려온다.
                if (shutterFault != null && shutterFault.Active && shutterFault.Shutter == shutter && shutter.ClearHeight > .5f && !shutter.Moving && Ready("shutter_worse_" + id))
                    list.Add(new Transition { Key = "shutter_worse_" + id, Kind = "shutter_comes_down", Description = "The faulty control lets the fire shutter '" + shutter.Equipment.Label + "' come the rest of the way down", Apply = _ => { s.Malfunction(0f, shutterFault.Level >= 3); log.Add("방화셔터가 끝까지 내려옴 · " + s.Equipment.Label); } });
                // 경보 신호가 끝난 뒤 내려와 있는 셔터는 시설 담당이 복구 스위치로 올린다.
                if (shutter.Opening < .999f && !shutter.Triggered && !shutter.ControllerFault && !shutter.Moving && arrived.Contains(Agency.Facility) && Ready("shutter_up_" + id))
                    list.Add(new Transition { Key = "shutter_up_" + id, Kind = "shutter_reset", Description = "The facility team presses the reset switch of the fire shutter '" + shutter.Equipment.Label + "' at its control box and raises it", Apply = _ => s.Operate(FireShutterPoint.Command.Reset, "시설 담당") });
            }
        }

        // ── 역무원이 아는 것 ──

        /// <summary>The closed passages of lowered shutters the staff member has seen (guidance only knows those).</summary>
        private void AddSeenShutters(List<GuideRoute.Closure> into)
        {
            foreach (var shutter in shutters)
                if (shutterSeen.Contains(shutter) && shutterClosed.Contains(shutter)) into.Add(new GuideRoute.Closure(shutter.transform.position, ShutterClosureRadius, null));
        }

        private void LookAroundShutters(Vector3 eye, Vector3 forward)
        {
            foreach (var shutter in shutters)
            {
                if (shutter.Opening >= .999f || shutterSeen.Contains(shutter) || Mathf.Abs(eye.y - shutter.transform.position.y - 1.7f) > 2.5f) continue;
                var to = shutter.transform.position + Vector3.up * 1.5f - eye;
                var flat = to;
                flat.y = 0;
                if (flat.magnitude > 16f || Vector3.Angle(forward, to) > 55f) continue;
                shutterSeen.Add(shutter);
                session.Hud.Toast(shutter.Triggered
                    ? "방화셔터가 내려와 있습니다 · 감지기 신호가 남아 있어 수신기 복구 뒤 조작함의 복구 스위치(R)로 올립니다"
                    : "방화셔터가 내려와 있습니다 · 조작함에서 정지하거나 복구 스위치(R)로 올릴 수 있습니다", 6f);
            }
        }
    }
}
