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
    /// Filing in and out is constant time (a shift places thousands of fittings): lists keep registration order until an object leaves, which moves the last one into its place.
    /// </summary>
    public static class EquipmentRegistry
    {
        /// <summary>A list with constant-time membership and removal (by Unity instance id).</summary>
        private sealed class Bucket
        {
            public readonly List<StationEquipment> Items = new List<StationEquipment>();
            private readonly Dictionary<int, int> index = new Dictionary<int, int>();

            public bool Add(StationEquipment equipment)
            {
                if (!index.TryAdd(equipment.GetInstanceID(), Items.Count)) return false;
                Items.Add(equipment);
                return true;
            }

            public bool Remove(StationEquipment equipment)
            {
                if (!index.Remove(equipment.GetInstanceID(), out int at)) return false;
                int last = Items.Count - 1;
                if (at != last)
                {
                    Items[at] = Items[last];
                    index[Items[at].GetInstanceID()] = at;
                }
                Items.RemoveAt(last);
                return true;
            }

            public void Clear()
            {
                Items.Clear();
                index.Clear();
            }
        }

        private static readonly Bucket all = new Bucket();
        private static readonly Dictionary<string, Bucket> byKind = new Dictionary<string, Bucket>(StringComparer.Ordinal);
        private static readonly Dictionary<string, StationEquipment> byId = new Dictionary<string, StationEquipment>(StringComparer.Ordinal);
        private static readonly IReadOnlyList<StationEquipment> none = Array.Empty<StationEquipment>();

        public static IReadOnlyList<StationEquipment> All => all.Items;

        /// <summary>The equipment of one kind (empty when there is none).</summary>
        public static IReadOnlyList<StationEquipment> OfKind(string kind) => byKind.TryGetValue(kind ?? "", out var bucket) ? bucket.Items : none;

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
            if (equipment == null || !all.Add(equipment)) return;
            if (!byKind.TryGetValue(equipment.Kind, out var bucket)) byKind[equipment.Kind] = bucket = new Bucket();
            bucket.Add(equipment);
            if (!string.IsNullOrEmpty(equipment.Id)) byId[equipment.Id] = equipment;
        }

        public static void Unregister(StationEquipment equipment)
        {
            if (equipment == null || !all.Remove(equipment)) return;
            if (byKind.TryGetValue(equipment.Kind, out var bucket)) bucket.Remove(equipment);
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
