using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The fire-detection side of a spot-type detector on a ceiling (a <see cref="StationEquipment"/> of kind
    /// <see cref="SmokeKind"/> or <see cref="HeatKind"/>): where it stands relative to the floor, how far its detection area
    /// reaches, and its local action indicator (동작표시등), a lamp that lights when it trips and stays lit until the receiver
    /// is reset. The numbers come from the placement entry (<c>coverage</c>, <c>height</c>, <c>area</c>, <c>class</c>, <c>zone</c>
    /// in <see cref="StationEquipment.Data"/>); the builder derives them from NFTC 203.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class DetectorPoint : MonoBehaviour
    {
        public const string SmokeKind = "smoke_detector", HeatKind = "heat_detector";

        public StationEquipment Equipment { get; private set; }
        /// <summary>Horizontal reach of its detection area in metres: the half diagonal of the square of its <see cref="Area"/> (the farthest floor point it is responsible for).</summary>
        public float Coverage { get; private set; }
        /// <summary>Height of its ceiling above the floor in metres (NFTC 203 mounting height, 부착높이).</summary>
        public float MountHeight { get; private set; }
        /// <summary>Floor area in square metres it is responsible for (NFTC 203 2.4.3).</summary>
        public float Area { get; private set; }
        /// <summary>Detector type in words: "광전식 스포트형 1종".</summary>
        public string Class { get; private set; } = "";
        /// <summary>The receiver's detection zone (경계구역) it belongs to: "2층 맞이방 7구역".</summary>
        public string ZoneName { get; private set; } = "";

        public bool Smoke => Equipment.Kind == SmokeKind;
        public bool Tripped { get; private set; }

        /// <summary>The floor point below it (what the staff stands on).</summary>
        public Vector3 FloorPoint => transform.position + Vector3.down * MountHeight;

        private GameObject lamp;

        /// <summary>Reads its numbers from the placement data; the spawner has assigned them by the time the shift's fire safety starts.</summary>
        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Coverage = Equipment.Number("coverage", 6f);
            MountHeight = Equipment.Number("height", 3f);
            Area = Equipment.Number("area", 70f);
            Class = Equipment.Text("class");
            ZoneName = Equipment.Text("zone");
            var live = transform.Find(EquipmentSpawner.LiveChild);
            lamp = live != null ? live.gameObject : null;
            if (lamp != null) lamp.SetActive(false);
        }

        /// <summary>The detector trips: its lamp lights and its state says so.</summary>
        public void Trip()
        {
            Tripped = true;
            Equipment.State = "동작";
            if (lamp != null) lamp.SetActive(true);
        }

        /// <summary>The receiver is reset: the lamp goes out. A detector that still senses smoke trips again on the next check.</summary>
        public void Restore()
        {
            Tripped = false;
            Equipment.State = "정상";
            if (lamp != null) lamp.SetActive(false);
        }
    }
}
