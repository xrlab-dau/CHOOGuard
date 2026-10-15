#if CHOOGUARD_DOOR_CLOSER_FAULT
using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Shell;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public sealed partial class IncidentDirector
    {
        private readonly List<StationDoor> operatingDoors = new List<StationDoor>();
        private DoorCloserFaultHazard doorCloserFault;
        private DoorOperatorFault ownedDoorFault;

        private void BeginDoors()
        {
            operatingDoors.Clear();
            operatingDoors.AddRange(FindObjectsByType<StationDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(d => d.gameObject.scene.name == SceneFlow.StationScene)
                .OrderBy(d => d.Element, StringComparer.Ordinal).ThenBy(d => d.Index));
        }

        private void EndDoors()
        {
            if (ownedDoorFault != null)
            {
                ownedDoorFault.Changed -= DoorFaultChanged;
                if (ownedDoorFault.Door != null && ownedDoorFault.Door.OperatorFault == ownedDoorFault)
                    ownedDoorFault.Door.OperatorFault = null;
                Destroy(ownedDoorFault);
            }
            operatingDoors.Clear();
        }

        private IEnumerable<Transition> DoorOperatorOrigins() => Each(operatingDoors, DoorCloserOrigin);

        private Transition DoorCloserOrigin(StationDoor door)
        {
            if (Stage != Phase.Calm || composedOrigins.Count != 0 || doorCloserFault != null || !operatingDoors.Contains(door) ||
                !DoorOperatorFault.CanFail(door) || !InsideBuilding(door.World(Vector3.zero)) ||
                world.IsClosed(door.World(Vector3.zero), 1)) return null;
            return new Transition
            {
                Key = "door_closer_return_" + door.Element + "_" + door.Index,
                Kind = "door_closer_return_failure", Origin = true,
                Description = "The modeled mechanical closer of the existing accessible indoor hinged door '" + door.Label +
                    "' at " + FixedPlace(door.World(Vector3.zero)) + " loses its return while the leaf is already open and nobody is holding it. " +
                    "The same real leaf stays at its present angle instead of closing. Staff must witness the lost return, manually close and secure it, request on-site maintenance and test its actual automatic closing after service.",
                Apply = _ => StartDoorCloserFailure(door),
            };
        }

        private void StartDoorCloserFailure(StationDoor door)
        {
            // This is the final commit guard on the exact door JEV judged, never a substitute door or a local fallback.
            if (DoorCloserOrigin(door) == null || !IndoorSubject(door)) return;
            var fault = door.GetComponent<DoorOperatorFault>();
            if (fault == null) fault = door.gameObject.AddComponent<DoorOperatorFault>();
            if (!fault.BeginFailure(door, session.Player)) { if (!fault.Active) Destroy(fault); return; }
            ownedDoorFault = fault; fault.Changed += DoorFaultChanged;
            doorCloserFault = new DoorCloserFaultHazard("door-closer-" + ++serial, fault) { Where = door.Label };
            Register(doorCloserFault);
            log.Add("기존 출입문 클로저 자동 복귀 불량 · " + door.Label);
        }

        private bool DoorServiceEligible()
        {
            if (doorCloserFault == null || !doorCloserFault.Active || ownedDoorFault == null || !ownedDoorFault.Active ||
                !ownedDoorFault.Diagnosed || !ownedDoorFault.Secured || ownedDoorFault.Serviced || ownedDoorFault.ServiceAuthorized ||
                !reported.Contains(doorCloserFault) || !InsideBuilding(ownedDoorFault.Door.World(Vector3.zero))) return false;
            // Arrival by radio alone is insufficient: a compatible existing worker must actually reach this opening.
            return responders.Any(r => r != null && r.OnScene && r.Worker && r.Team == Team.Facility &&
                Mathf.Abs(r.transform.position.y - doorCloserFault.Scene.y) < .7f &&
                Vector3.Distance(r.transform.position, doorCloserFault.Scene) < 4f);
        }

        private IEnumerable<Transition> DoorOperatorDevelopments()
        {
            if (!DoorServiceEligible()) yield break;
            yield return new Transition
            {
                Key = "door_closer_service_clearance_" + doorCloserFault.Id,
                Kind = "door_closer_service_clearance", Subject = ownedDoorFault,
                Description = "The already-dispatched station facility worker is physically at the same failed door '" + ownedDoorFault.Door.Label +
                    "'. Staff have witnessed its closer failure, manually closed the actual leaf, cleared its threshold, secured access and reported it. " +
                    "The worker confirms that isolation and begins the existing timed hands-on closer maintenance. This clearance does not repair the door or complete the incident; staff still have to open and release its real leaf and witness automatic closing afterward.",
                Apply = _ =>
                {
                    if (!DoorServiceEligible()) return;
                    var worker = responders.FirstOrDefault(r => r != null && r.OnScene && r.Worker && r.Team == Team.Facility &&
                        Mathf.Abs(r.transform.position.y - doorCloserFault.Scene.y) < .7f &&
                        Vector3.Distance(r.transform.position, doorCloserFault.Scene) < 4f);
                    ownedDoorFault.AuthorizeService(worker);
                },
            };
        }

        private void DoorFaultChanged(string description)
        {
            if (doorCloserFault == null) return;
            log.Add(description + " · " + doorCloserFault.Where);
            if (ownedDoorFault != null && ownedDoorFault.Diagnosed && !known.Contains(doorCloserFault))
                Know(doorCloserFault, "출입문 앞에서 자동 복귀 직접 확인");
        }

        private void DoorsTick()
        {
            if (doorCloserFault == null || !doorCloserFault.ConfirmVerified()) return;
            HazardRegistry.Remove(doorCloserFault);
            log.Add("출입문 현장 개방·자동 닫힘 시험 완료 · " + doorCloserFault.Where);
            Resolved(doorCloserFault);
        }
    }
}
#endif
