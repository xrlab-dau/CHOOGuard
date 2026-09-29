using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// Every placed <see cref="StationEquipment"/> of the running shift, by kind and id. The incident director and the
    /// staff interactions ask it what exists ("which smoke detectors cover this fire", "the nearest fire shutter") so an
    /// emergency starts from a real object. Objects file themselves in while enabled; the session clears it at the end.
    /// Kind and id must not be changed on a registered object except through <see cref="StationEquipment.Assign"/>.
    /// </summary>
    public static class EquipmentRegistry
    {
        private static readonly List<StationEquipment> all = new List<StationEquipment>();
        private static readonly Dictionary<string, List<StationEquipment>> byKind = new Dictionary<string, List<StationEquipment>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, StationEquipment> byId = new Dictionary<string, StationEquipment>(StringComparer.Ordinal);
        private static readonly IReadOnlyList<StationEquipment> none = Array.Empty<StationEquipment>();

        public static IReadOnlyList<StationEquipment> All => all;

        /// <summary>The equipment of one kind (empty when there is none).</summary>
        public static IReadOnlyList<StationEquipment> OfKind(string kind) => byKind.TryGetValue(kind ?? "", out var list) ? list : none;

        /// <summary>The equipment with this placement id, or null.</summary>
        public static StationEquipment Find(string id) => byId.TryGetValue(id ?? "", out var equipment) ? equipment : null;

        /// <summary>
        /// The nearest equipment of <paramref name="kind"/> within <paramref name="maxDistance"/> metres (straight line) that
        /// passes <paramref name="filter"/>, or null.
        /// </summary>
        public static StationEquipment Nearest(string kind, Vector3 position, float maxDistance = float.PositiveInfinity, Func<StationEquipment, bool> filter = null)
        {
            StationEquipment best = null;
            float bestSquared = float.IsPositiveInfinity(maxDistance) ? float.PositiveInfinity : maxDistance * maxDistance;
            foreach (var equipment in OfKind(kind))
            {
                float squared = (equipment.transform.position - position).sqrMagnitude;
                if (squared > bestSquared || filter != null && !filter(equipment)) continue;
                best = equipment;
                bestSquared = squared;
            }
            return best;
        }

        /// <summary>Adds every equipment of <paramref name="kind"/> within <paramref name="radius"/> metres of <paramref name="position"/> to <paramref name="into"/> (which is not cleared).</summary>
        public static void Within(string kind, Vector3 position, float radius, List<StationEquipment> into)
        {
            float squared = radius * radius;
            foreach (var equipment in OfKind(kind))
                if ((equipment.transform.position - position).sqrMagnitude <= squared) into.Add(equipment);
        }

        public static void Register(StationEquipment equipment)
        {
            if (equipment == null || all.Contains(equipment)) return;
            all.Add(equipment);
            if (!byKind.TryGetValue(equipment.Kind, out var list)) byKind[equipment.Kind] = list = new List<StationEquipment>();
            list.Add(equipment);
            if (!string.IsNullOrEmpty(equipment.Id)) byId[equipment.Id] = equipment;
        }

        public static void Unregister(StationEquipment equipment)
        {
            if (equipment == null || !all.Remove(equipment)) return;
            if (byKind.TryGetValue(equipment.Kind, out var list)) list.Remove(equipment);
            if (!string.IsNullOrEmpty(equipment.Id) && byId.TryGetValue(equipment.Id, out var filed) && filed == equipment) byId.Remove(equipment.Id);
        }

        public static void Clear()
        {
            all.Clear();
            byKind.Clear();
            byId.Clear();
        }

        // 도메인 리로드를 끈 에디터 재생에서도 이전 근무의 (이미 파괴된) 설비가 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();
    }
}
