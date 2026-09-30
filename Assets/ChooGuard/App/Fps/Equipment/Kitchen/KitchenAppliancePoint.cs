using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A cooking appliance of a shop kitchen: a fryer or an oven on electric power, a range on city gas. It is running while the
    /// shop is open; the staff member can switch its heat off (the fryer's power switch, the oven's switch, the range's burner
    /// cock), which is the first thing to do about a fire in it. Fires and smoke start at its child "Fire" (the oil surface of a
    /// fryer tank, the burner under the pan, the oven's door gap). Placement data: <c>shop</c>, <c>fuel</c> = <c>gas</c>|<c>electric</c>,
    /// and for a range <c>valve</c>, <c>hose</c>, for a hooded appliance <c>hood</c> and <c>auto</c> (the ids of those pieces).
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class KitchenAppliancePoint : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public const string FryerKind = "kitchen_fryer", RangeKind = "gas_range", OvenKind = "kitchen_oven";

        public StationEquipment Equipment { get; private set; }
        public string Shop { get; private set; } = "";
        public bool Gas { get; private set; }
        public string ValveId { get; private set; } = "";
        public string HoseId { get; private set; } = "";
        public string HoodId { get; private set; } = "";
        public string AutoId { get; private set; } = "";
        /// <summary>Where a fire or smoke starts.</summary>
        public Transform Fire { get; private set; }
        /// <summary>The heat is on (the appliance is working).</summary>
        public bool On { get; private set; } = true;
        /// <summary>Someone switched the heat off, and who.</summary>
        public event Action<KitchenAppliancePoint, string> SwitchedOff;

        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Shop = Equipment.Text("shop");
            Gas = Equipment.Text("fuel") == "gas";
            ValveId = Equipment.Text("valve");
            HoseId = Equipment.Text("hose");
            HoodId = Equipment.Text("hood");
            AutoId = Equipment.Text("auto");
            Fire = transform.Find("Fire") ?? transform;
        }

        public string DisplayName => Equipment.Label + (On ? "" : " · 꺼짐");

        public string InteractionPrompt =>
            !On ? "" : Equipment.Kind == RangeKind ? "화구 코크 잠그기(불 끄기)" : Equipment.Kind == FryerKind ? "튀김기 전원 끄기" : "오븐 전원 끄기";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = On && responder != null && !responder.IsPaused;
            reason = available ? null : "";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out _)) return false;
            SwitchOff("역무원");
            feedback = Equipment.Kind == RangeKind ? "화구 코크를 잠갔습니다" : Equipment.Label + " 전원을 껐습니다";
            return true;
        }

        /// <summary>Switches the heat off and tells the listeners (a fire fed by it loses its feed).</summary>
        public void SwitchOff(string by)
        {
            if (!On) return;
            On = false;
            Equipment.State = "꺼짐";
            SwitchedOff?.Invoke(this, by);
        }
    }
}
