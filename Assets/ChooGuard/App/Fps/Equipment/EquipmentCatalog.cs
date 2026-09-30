using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// What a shift needs to place the station's equipment: every group's placement file and every equipment prefab (by
    /// prefab name, as the placement entries name them). A player build cannot read <c>Art/Emergency/Equipment/*.json</c> from
    /// the asset folder, so the editor builders collect them here (EquipmentBuilder.RefreshCatalog) and EmergencyArt points at
    /// this asset.
    /// </summary>
    [CreateAssetMenu(menuName = "ChooGuard/Equipment Catalog", fileName = "EquipmentCatalog")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        [Tooltip("One placement file per equipment group (FireSafety, ElectricPlaza, KitchenGas).")]
        public TextAsset[] Placements = Array.Empty<TextAsset>();
        [Tooltip("Every prefab under Art/Emergency/Equipment/Prefabs; a placement entry names one by its file name.")]
        public GameObject[] Prefabs = Array.Empty<GameObject>();

        private Dictionary<string, GameObject> byName;

        /// <summary>The prefab called <paramref name="name"/>, or null.</summary>
        public GameObject Prefab(string name)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (var prefab in Prefabs) if (prefab != null) byName[prefab.name] = prefab;
            }
            return name != null && byName.TryGetValue(name, out var found) ? found : null;
        }

        private void OnValidate() => byName = null;
    }
}
