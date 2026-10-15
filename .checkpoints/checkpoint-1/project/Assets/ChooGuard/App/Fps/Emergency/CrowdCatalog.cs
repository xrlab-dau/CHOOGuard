using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Rigged people used by the shift: passengers, a station colleague and the arriving teams (<see cref="Teams"/>).
    /// Team members are listed lead first. Built by ChooGuard.Editor.CrowdAssetBuilder.
    /// </summary>
    [CreateAssetMenu(menuName = "ChooGuard/Crowd Catalog", fileName = "CrowdCatalog")]
    public sealed class CrowdCatalog : ScriptableObject
    {
        public GameObject[] FemalePassengers = new GameObject[0];
        public GameObject[] MalePassengers = new GameObject[0];
        public GameObject Colleague;
        [Header("119")]
        public GameObject[] Firefighters = new GameObject[0];
        public GameObject[] Rescuers = new GameObject[0];
        public GameObject[] HazmatTeam = new GameObject[0];
        public GameObject[] Paramedics = new GameObject[0];
        [Header("경찰: 철도특별사법경찰대, 지구대, 경찰특공대(폭발물처리요원, 대원)")]
        public GameObject[] Police = new GameObject[0];
        public GameObject[] PatrolPolice = new GameObject[0];
        public GameObject[] BombSquad = new GameObject[0];
        [Header("시설·협력업체·열차")]
        public GameObject[] FacilityStaff = new GameObject[0];
        public GameObject[] ElevatorTechnicians = new GameObject[0];
        public GameObject[] GasTechnicians = new GameObject[0];
        public GameObject[] Electricians = new GameObject[0];
        public GameObject[] TrainCrew = new GameObject[0];
    }
}
