using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    public sealed partial class IncidentDirector
    {
        private StationBuilding building;

        /// <summary>Incident sites are inside the authored building and beneath its real static structure.</summary>
        public bool InsideBuilding(Vector3 position) => building != null && building.Contains(position);

        // This is checked both when listed and when a JEV answer is applied. A passenger who has left the building
        // cannot carry an old judgment outside. Unknown source types are withheld until they supply an indoor site.
        private bool IndoorSubject(object subject)
        {
            // Staged or manually attached new equipment cannot bypass the measured placement gate into JEV origins.
            if (subject is Component piece && piece != null)
            {
                var installation = piece.GetComponentInParent<StationEquipment>();
                if (installation != null && !installation.IndependentlyVerified) return false;
            }
            switch (subject)
            {
                case IncidentDirector director: return director == this && building != null;
                case TrainService.Car _: return false;
                case Elevator elevator:
                    if (elevator.Entry == null || elevator.Entry.stops == null) return false;
                    foreach (var stop in elevator.Entry.stops) if (InsideBuilding(stop.door)) return true;
                    return false;
                case SprinklerHeadPoint head: return InsideBuilding(head.FloorPoint);
                case SprinklerPipeLine pipe:
                    return InsideBuilding(new Vector3(pipe.A.x, pipe.FloorY, pipe.A.z)) && InsideBuilding(new Vector3(pipe.B.x, pipe.FloorY, pipe.B.z));
                case DetectorPoint detector: return InsideBuilding(detector.FloorPoint);
                case EmergencyArt.HangingItem item: return hanging.Contains(item) && InsideBuilding(HangingFloor(item));
                case Kitchen kitchen: return kitchen.Meter != null && kitchen.Meter.IndependentlyVerified && InsideBuilding(kitchen.Meter.transform.position);
                case StationPoints.Point point: return InsideBuilding(point.Position);
                case StationEquipment equipment:
                    if (equipment == null || !equipment.isActiveAndEnabled) return false;
                    var detectorPart = equipment.GetComponent<DetectorPoint>();
                    if (detectorPart != null) return IndoorSubject(detectorPart);
                    var sprinklerPart = equipment.GetComponent<SprinklerHeadPoint>();
                    if (sprinklerPart != null) return IndoorSubject(sprinklerPart);
                    var pipePart = equipment.GetComponent<SprinklerPipeLine>();
                    if (pipePart != null) return IndoorSubject(pipePart);
                    return InsideBuilding(equipment.transform.position);
                case Component component: return component != null && InsideBuilding(component.transform.position);
                default: return false;
            }
        }

        private static Vector3 HangingFloor(EmergencyArt.HangingItem item)
        {
            var below = item.Centre + Vector3.down * (item.Size.y * .5f + .3f);
            return Physics.Raycast(below, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * .02f : below;
        }
    }
}
