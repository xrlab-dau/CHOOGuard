using System;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// Root of one placed piece of building equipment (a detector, a sprinkler head, a shutter, a fryer, a distribution
    /// board …). It sits in <see cref="EquipmentRegistry"/> while enabled, so emergencies and staff actions find the real
    /// object instead of inventing a place. Placed by <see cref="EquipmentSpawner"/> from a group's placement file; the
    /// prefab root carries it with the per-kind performance settings below.
    /// </summary>
    public class StationEquipment : MonoBehaviour
    {
        [Tooltip("Unique id from the placement file (the builder derives it from zone and running number).")]
        public string Id = "";
        [Tooltip("What it is, in snake_case English: smoke_detector, sprinkler_head, fire_shutter … Emergencies match on this.")]
        public string Kind = "";
        [Tooltip("Korean name the staff sees in the HUD and on the radio.")]
        public string Label = "";
        [Tooltip("Zone id of StationPoints (hall2f, hall3f, ground1f, plaza, tracks …) the object stands in.")]
        public string Zone = "";
        [Header("성능 (프리팹마다)")]
        [Tooltip("Farther than this from the camera its renderers are switched off. Small fittings vanish long before the frustum edge.")]
        public float DrawDistance = 40f;
        [Tooltip("The player can aim at it: the prefab carries a collider. Decorative equipment has none, so raycasts and physics ignore it.")]
        public bool Interactable;
        [Tooltip("The renderers may be merged into static batches (one draw for many identical fittings). False for anything that moves: shutter curtains, doors, fans.")]
        public bool Batchable = true;

        [SerializeField] private string state = "정상";

        /// <summary>How it stands now in words the staff sees ("정상", "동작", "고장", "점검 중"); groups own the vocabulary of their kinds.</summary>
        public string State
        {
            get => state;
            set
            {
                if (state == value) return;
                state = value;
                StateChanged?.Invoke(this);
            }
        }

        /// <summary>The state changed (a detector tripped, a shutter came down).</summary>
        public event Action<StationEquipment> StateChanged;

        /// <summary>
        /// Sets the data a placement entry carries. The spawner calls it right after instantiating, when the object is
        /// already registered under its prefab defaults, so it is filed again under the new kind.
        /// </summary>
        public void Assign(string id, string kind, string label, string zone)
        {
            bool registered = isActiveAndEnabled;
            if (registered) EquipmentRegistry.Unregister(this);
            Id = id;
            Kind = kind;
            Label = label;
            Zone = zone;
            if (registered) EquipmentRegistry.Register(this);
        }

        private void OnEnable() => EquipmentRegistry.Register(this);

        private void OnDisable() => EquipmentRegistry.Unregister(this);
    }
}
