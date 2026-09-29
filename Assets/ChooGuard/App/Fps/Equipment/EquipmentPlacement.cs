using System;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>One placed piece of equipment in a group's placement file (world position and euler rotation in metres/degrees).</summary>
    [Serializable]
    public sealed class EquipmentPlacement
    {
        public string id, kind, label, zone, prefab;
        public Vector3 position, rotation;
    }

    /// <summary>
    /// A group's placement file, <c>Assets/ChooGuard/Art/Emergency/Equipment/&lt;group&gt;.json</c>, written by that group's
    /// editor builder (menu ChooGuard/Emergency/Equipment/&lt;Group&gt;) and read by <see cref="EquipmentSpawner"/> at session start.
    /// </summary>
    [Serializable]
    public sealed class EquipmentPlacementFile
    {
        public int version = 1;
        public string group, note;
        public EquipmentPlacement[] items = Array.Empty<EquipmentPlacement>();
    }
}
