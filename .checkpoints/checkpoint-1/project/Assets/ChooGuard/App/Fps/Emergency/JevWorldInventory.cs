using System;
using System.Collections.Generic;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// A read-only inventory for JEV's director context. Counts live objects and their existing state, never creates
    /// objects, candidates or random draws. A registered source is not necessarily eligible for a transition: the
    /// director still checks the exact source's preconditions. The snapshot is rebuilt at most once per game second.
    /// </summary>
    public sealed class JevWorldInventory
    {
        public const float RefreshSeconds = 1f;

        private static readonly SortedDictionary<string, string[]> EquipmentCauses = new SortedDictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["distribution_board"] = new[] { "board_fire" },
            ["vending_machine"] = new[] { "vending_fire" },
            ["charging_kiosk"] = new[] { "kiosk_fire" },
            ["litter_bin"] = new[] { "bin_fire" },
            ["recycling_bin"] = new[] { "bin_battery_fire" },
            ["smoke_detector"] = new[] { "false_alarm" },
            ["sprinkler_pipe"] = new[] { "water_leak" },
            ["sprinkler_head"] = new[] { "sprinkler_discharge" },
            ["fire_shutter"] = new[] { "shutter_malfunction" },
            ["kitchen_fryer"] = new[] { "fryer_oil_fire" },
            ["gas_range"] = new[] { "kitchen_fire", "gas_cock_open" },
            ["kitchen_oven"] = new[] { "oven_fire" },
            ["gas_hose"] = new[] { "gas_hose_off" },
            ["gas_meter"] = new[] { "gas_meter_leak" },
        };

        private Dictionary<string, object> snapshot;
        private float nextAt, previousAt = float.NegativeInfinity;

        /// <summary>
        /// Pass the same building predicate used by composition, and the director's remaining hanging items (not the
        /// immutable art catalogue, which still contains items that have fallen). The supplied hanging list is already
        /// filtered by the director's floor-position check. Supply the equipment predicate too, so ceiling fittings use
        /// their floor metadata. This class does not query physics or routes directly, write registries or consume random
        /// numbers; its predicates may inspect the building. Work is one linear pass; output is grouped counts.
        /// </summary>
        public IReadOnlyDictionary<string, object> Snapshot(float now, StationWorld world, CrowdDirector crowd,
            IReadOnlyList<EmergencyArt.HangingItem> hanging, Func<Vector3, bool> insideBuilding,
            Func<StationEquipment, bool> indoorEquipment = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (insideBuilding == null) throw new ArgumentNullException(nameof(insideBuilding));
            if (snapshot != null && now >= previousAt && now < nextAt) return snapshot;
            previousAt = now;
            nextAt = now + RefreshSeconds;
            snapshot = Build(world, crowd, hanging, insideBuilding, indoorEquipment);
            return snapshot;
        }

        private static Dictionary<string, object> Build(StationWorld world, CrowdDirector crowd,
            IReadOnlyList<EmergencyArt.HangingItem> hanging, Func<Vector3, bool> inside,
            Func<StationEquipment, bool> indoorEquipment)
        {
            int people = 0, eligible = 0, elderly = 0, luggage = 0, phones = 0, powerBanks = 0, injured = 0, helping = 0;
            var activities = Counts();
            if (crowd != null)
                foreach (var person in crowd.People)
                {
                    if (person == null || person.Body == null || !person.Body.Visible || person.Current == Passenger.Activity.InTrain || !inside(person.transform.position)) continue;
                    people++;
                    if (!person.Hurt && !person.Hostile && Passenger.Routine(person.Current)) eligible++;
                    if (person.Elderly) elderly++;
                    if (person.Luggage > 0) luggage++;
                    if (person.UsesPhone) phones++;
                    if (person.CarriesPowerBank) powerBanks++;
                    if (person.Hurt) injured++;
                    if (person.Helping) helping++;
                    Count(activities, person.Current.ToString());
                }

            var places = Counts();
            foreach (var point in world.Points.All) if (inside(point.Position)) Count(places, point.Kind.ToString());

            int outsideEquipment = 0;
            var equipmentCounts = Counts();
            var equipmentStates = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);
            foreach (var piece in EquipmentRegistry.All)
            {
                if (piece == null || !piece.isActiveAndEnabled) continue;
                if (!(indoorEquipment != null ? indoorEquipment(piece) : inside(piece.transform.position))) { outsideEquipment++; continue; }
                string kind = string.IsNullOrEmpty(piece.Kind) ? "(unassigned)" : piece.Kind;
                Count(equipmentCounts, kind);
                if (!equipmentStates.TryGetValue(kind, out var states)) equipmentStates[kind] = states = Counts();
                Count(states, piece.State ?? "(unknown)");
            }
            var sources = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in EquipmentCauses)
            {
                equipmentCounts.TryGetValue(pair.Key, out int count);
                sources[pair.Key] = new
                {
                    registered_indoor_sources = count,
                    status = count == 0 ? "unavailable_no_registered_source" : "source_present_director_checks_preconditions",
                    implemented_atomic_causes = pair.Value,
                };
            }
            var noDeclaredOrigin = new List<string>();
            foreach (var kind in equipmentCounts.Keys) if (!EquipmentCauses.ContainsKey(kind)) noDeclaredOrigin.Add(kind);

            int signs = 0;
            if (hanging != null) foreach (var item in hanging) if (item != null) signs++;
            int escalators = 0, runningEscalators = 0, escalatorRiders = 0;
            foreach (var link in world.Escalators)
            {
                if (link == null || link.Entry == null || !inside(link.Middle)) continue;
                escalators++;
                if (link.Running) runningEscalators++;
                escalatorRiders += link.RiderCount;
            }
            int elevators = 0, runningElevators = 0, elevatorRiders = 0;
            foreach (var link in world.Elevators)
            {
                if (link == null || link.Entry == null || link.Entry.stops == null) continue;
                bool indoor = false;
                foreach (var stop in link.Entry.stops) if (stop != null && inside(stop.door)) { indoor = true; break; }
                if (!indoor) continue;
                elevators++;
                if (link.Running) runningElevators++;
                foreach (var body in link.Inside) if (body != null) elevatorRiders++;
            }

            return new Dictionary<string, object>
            {
                ["scope"] = "current station building interior only; outside sources are excluded",
                ["meaning"] = "Inventory is context, not new causes or a scenario list. Only the director's physically possible atomic transitions may happen. A place marker does not establish an unregistered appliance or suspicious object.",
                ["refresh_game_seconds"] = RefreshSeconds,
                ["passengers"] = new { visible_indoor = people, routine_unhurt_nonhostile = eligible, elderly, carrying_luggage = luggage, uses_phone = phones, carries_power_bank = powerBanks, injured, helping, activities },
                ["indoor_place_counts"] = places,
                ["registered_indoor_equipment_counts"] = equipmentCounts,
                ["registered_indoor_equipment_states"] = equipmentStates,
                ["outside_equipment_excluded"] = outsideEquipment,
                ["equipment_cause_source_availability"] = sources,
                ["registered_equipment_without_declared_origin_adapter"] = noDeclaredOrigin,
                ["building_links"] = new { remaining_hanging_items = signs, escalators, running_escalators = runningEscalators, escalator_riders = escalatorRiders, elevators, running_elevators = runningElevators, elevator_riders = elevatorRiders },
                ["station_wide_external_causes_already_supported"] = new[] { "earthquake", "mains power failure", "threat phone call" },
            };
        }

        private static SortedDictionary<string, int> Counts() => new SortedDictionary<string, int>(StringComparer.Ordinal);
        private static void Count(SortedDictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }
    }
}
