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
        [Tooltip("What the kind needs at run time, 'key=value' pairs joined by ';' (a detector's coverage radius and mounting height), from the placement entry.")]
        public string Data = "";
        [Header("성능 (프리팹마다)")]
        [Tooltip("Farther than this from the camera its renderers are switched off. Small fittings vanish long before the frustum edge.")]
        public float DrawDistance = 40f;
        [Tooltip("The player can aim at it: the prefab carries a collider. Decorative equipment has none, so raycasts and physics ignore it.")]
        public bool Interactable;
        [Tooltip("The renderers may be merged into static batches (one draw for many identical fittings). False for anything that moves: shutter curtains, doors, fans.")]
        public bool Batchable = true;

        /// <summary>
        /// Set by an owner that switches some renderers off itself (a burst sprinkler head hides its bulb): the culling keeps a renderer for which this returns true off
        /// when the object comes back into draw distance.
        /// </summary>
        public Func<Renderer, bool> KeepOff;

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
        public void Assign(string id, string kind, string label, string zone, string data = "")
        {
            bool registered = isActiveAndEnabled;
            if (registered) EquipmentRegistry.Unregister(this);
            Id = id;
            Kind = kind;
            Label = label;
            Zone = zone;
            Data = data ?? "";
            if (registered) EquipmentRegistry.Register(this);
        }

        /// <summary>A number from <see cref="Data"/> (invariant culture), or <paramref name="fallback"/> when the key is missing or not a number. Parses on every call: read it once, not per frame.</summary>
        public float Number(string key, float fallback = 0f) =>
            float.TryParse(Text(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;

        /// <summary>A word from <see cref="Data"/>, or <paramref name="fallback"/> when the key is missing.</summary>
        public string Text(string key, string fallback = "")
        {
            foreach (var pair in Data.Split(';'))
            {
                int equals = pair.IndexOf('=');
                if (equals == key.Length && pair.StartsWith(key, StringComparison.Ordinal)) return pair.Substring(equals + 1);
            }
            return fallback;
        }

        private void OnEnable() => EquipmentRegistry.Register(this);

        private void OnDisable() => EquipmentRegistry.Unregister(this);
    }
}
