using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The flexible gas hose between a range's fuse cock and the range (kind <see cref="Kind"/>). It stands in two shapes, the
    /// children "Attached" (hanging in its S from the fuse cock to the range's inlet) and "Detached" (slipped off the range and
    /// swinging free); a leak starts at the loose end, the child "Leak". Placement data: <c>shop</c>, <c>range</c>, <c>valve</c>.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class GasHosePoint : MonoBehaviour
    {
        public const string Kind = "gas_hose";

        public StationEquipment Equipment { get; private set; }
        public string Shop { get; private set; } = "";
        public string RangeId { get; private set; } = "";
        public string ValveId { get; private set; } = "";
        public bool Detached { get; private set; }
        /// <summary>Where the gas comes out when the hose is off (the brass end of the loose hose).</summary>
        public Transform Leak { get; private set; }

        private GameObject attached, detached;

        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Shop = Equipment.Text("shop");
            RangeId = Equipment.Text("range");
            ValveId = Equipment.Text("valve");
            attached = transform.Find("Attached").gameObject;
            detached = transform.Find("Detached").gameObject;
            Leak = transform.Find("Leak");
        }

        /// <summary>The hose comes off the range (the clamp slips, the nut works loose).</summary>
        public void Detach()
        {
            Detached = true;
            Equipment.State = "이탈";
            attached.SetActive(false);
            detached.SetActive(true);
        }

        /// <summary>The gas company puts the hose back on with a new clamp.</summary>
        public void Reattach()
        {
            Detached = false;
            Equipment.State = "정상";
            attached.SetActive(true);
            detached.SetActive(false);
        }
    }
}
