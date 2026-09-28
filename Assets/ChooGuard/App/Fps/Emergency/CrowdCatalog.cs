using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>Rigged people used by the shift: passengers, a station colleague and arriving responders.</summary>
    [CreateAssetMenu(menuName = "ChooGuard/Crowd Catalog", fileName = "CrowdCatalog")]
    public sealed class CrowdCatalog : ScriptableObject
    {
        public GameObject[] FemalePassengers = new GameObject[0];
        public GameObject[] MalePassengers = new GameObject[0];
        public GameObject Colleague;
        public GameObject[] Firefighters = new GameObject[0];
        public GameObject[] Police = new GameObject[0];
        public GameObject[] Paramedics = new GameObject[0];
    }
}
