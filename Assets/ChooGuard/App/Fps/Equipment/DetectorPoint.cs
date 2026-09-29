using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The fire-detection side of a detector (a <see cref="StationEquipment"/> of kind <see cref="SmokeKind"/> or
    /// <see cref="HeatKind"/>, a spot type on a ceiling, or <see cref="BeamKind"/>, one end unit of a beam pair on a wall): where it
    /// stands relative to the floor, how far its detection area reaches, and its local action indicator (동작표시등), a lamp that
    /// lights when it trips and stays lit until the receiver is reset. The numbers come from the placement entry (<c>coverage</c>,
    /// <c>height</c>, <c>area</c>, <c>class</c>, <c>zone</c>, and for beams <c>role</c> and <c>partner</c>, in
    /// <see cref="StationEquipment.Data"/>); the builder derives them from NFTC 203. A beam pair is one detector: the transmitter
    /// senses and trips, the receiver only lights its lamp.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class DetectorPoint : MonoBehaviour
    {
        public const string SmokeKind = "smoke_detector", HeatKind = "heat_detector", BeamKind = "beam_detector";

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

        /// <summary>It senses smoke (a spot smoke detector or a beam), not heat.</summary>
        public bool Smoke => Equipment.Kind != HeatKind;
        public bool Beam => Equipment.Kind == BeamKind;
        /// <summary>The receiver end of a beam pair: it lights its lamp with the transmitter and senses nothing itself.</summary>
        public bool Receiver { get; private set; }
        /// <summary>The kind of room it hangs in when that matters for what can set it off ("toilet"), or empty.</summary>
        public string Room { get; private set; } = "";
        public bool Tripped { get; private set; }
        /// <summary>The other end of a beam pair (resolved through the registry on first use), or null.</summary>
        public DetectorPoint Partner => partner != null ? partner : partnerId.Length == 0 ? null : partner = EquipmentRegistry.Find(partnerId)?.GetComponent<DetectorPoint>();

        /// <summary>The floor point below it (what the staff stands on).</summary>
        public Vector3 FloorPoint => transform.position + Vector3.down * MountHeight;

        private GameObject lamp;
        private DetectorPoint partner;
        private string partnerId = "";

        /// <summary>Reads its numbers from the placement data; the spawner has assigned them by the time the shift's fire safety starts.</summary>
        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Coverage = Equipment.Number("coverage", 6f);
            MountHeight = Equipment.Number("height", 3f);
            Area = Equipment.Number("area", 70f);
            Class = Equipment.Text("class");
            ZoneName = Equipment.Text("zone");
            Room = Equipment.Text("room");
            Receiver = Equipment.Text("role") == "rx";
            partnerId = Equipment.Text("partner");
            var live = transform.Find(EquipmentSpawner.LiveChild);
            lamp = live != null ? live.gameObject : null;
            if (lamp != null) lamp.SetActive(false);
        }

        /// <summary>Horizontal distance from <paramref name="point"/> to the detector: to the unit for a spot detector, to the optical axis (the segment between the two units) for a beam.</summary>
        public float HorizontalDistance(Vector3 point)
        {
            var a = transform.position;
            var other = Beam ? Partner : null;
            if (other == null) return new Vector2(point.x - a.x, point.z - a.z).magnitude;
            var ab = new Vector2(other.transform.position.x - a.x, other.transform.position.z - a.z);
            var ap = new Vector2(point.x - a.x, point.z - a.z);
            float t = ab.sqrMagnitude < 1e-6f ? 0 : Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude);
            return (ap - ab * t).magnitude;
        }

        /// <summary>The detector trips: its lamp lights and its state says so (a beam pair lights both ends).</summary>
        public void Trip()
        {
            Tripped = true;
            Light(true);
            Partner?.Light(true);
        }

        /// <summary>The receiver is reset: the lamp goes out. A detector that still senses smoke trips again on the next check.</summary>
        public void Restore()
        {
            Tripped = false;
            Light(false);
            Partner?.Light(false);
        }

        private void Light(bool on)
        {
            Equipment.State = on ? "동작" : "정상";
            if (lamp != null) lamp.SetActive(on);
        }
    }
}
