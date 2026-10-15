using System;
using ChooGuard.App.Fps.Facilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Bind only the two existing matched wing landings; the source geometry and transforms remain authored.</summary>
    public static class ExistingElevatorCarriageSetup
    {
        public static string Apply()
        {
            const string bank = "맞이방 날개 승강기";
            string[] elements = { "B-ELEV2F-wing", "3F-ELEV-wing" };
            var doors = new StationDoor[2];
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<StationDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int index = Array.IndexOf(elements, candidate.Element);
                if (index < 0) continue;
                if (doors[index] != null) throw new InvalidOperationException("Duplicate existing elevator element: " + elements[index]);
                doors[index] = candidate;
            }
            var stops = new PhysicalElevatorCarriage.Stop[2];
            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i];
                if (door == null || door.Kind != StationDoor.DoorKind.Elevator || door.CarId != bank ||
                    door.Cabin == null || door.Leaves.Length != 4 || !door.Usable)
                    throw new InvalidOperationException("Existing elevator source is incomplete: " + elements[i]);
                if (door.Leaves[2].Pivot == null || door.Leaves[3].Pivot == null)
                    throw new InvalidOperationException("Existing car leaf is missing: " + elements[i]);
                // In this authored kit, leaves 0/1 are the hall pair and 2/3 are the cabin pair behind them.
                float hallDepth = (door.Local(door.Leaves[0].Pivot.position).z + door.Local(door.Leaves[1].Pivot.position).z) * .5f;
                float carDepth = (door.Local(door.Leaves[2].Pivot.position).z + door.Local(door.Leaves[3].Pivot.position).z) * .5f;
                if (carDepth >= hallDepth - .01f) throw new InvalidOperationException("Car leaves do not lie behind the hall leaves: " + elements[i]);
                stops[i] = new PhysicalElevatorCarriage.Stop { Door = door, SourceCabin = door.Cabin,
                    CarDoorLeaves = new[] { door.Leaves[2].Pivot, door.Leaves[3].Pivot } };
            }
            var a = doors[0].Cabin; var b = doors[1].Cabin;
            float expectedRise = 5.2f;
            if (UpperCoherentHeightReplay.TryGetWingElevatorRise(doors[0], doors[1], out float sourceRise)) expectedRise = sourceRise;
            if (doors[0].gameObject.scene != doors[1].gameObject.scene ||
                new Vector2(a.position.x - b.position.x, a.position.z - b.position.z).magnitude > .002f ||
                Quaternion.Angle(a.rotation, b.rotation) > .01f || Mathf.Abs(b.position.y - a.position.y - expectedRise) > .002f)
                throw new InvalidOperationException("The selected sources do not match the existing 2F–3F vertical bank");
            var binding = doors[0].GetComponent<PhysicalElevatorCarriage>() ?? Undo.AddComponent<PhysicalElevatorCarriage>(doors[0].gameObject);
            Undo.RecordObject(binding, "Bind existing elevator carriage");
            binding.CarId = bank; binding.Stops = stops;
            EditorUtility.SetDirty(binding);
            EditorSceneManager.MarkSceneDirty(doors[0].gameObject.scene);
            EditorSceneManager.SaveScene(doors[0].gameObject.scene);
            return "CG_EXISTING_ELEVATOR_CARRIAGE bank=맞이방_날개_승강기 stops=2 active_cabins=1 car_leaves=2 hall_leaves=4 continuous=true field_accuracy_verified=false";
        }
    }
}
