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
    /// By hand (E held) the range's burner cock is turned a quarter turn with the mouse or the wheel, and the flame goes out when it
    /// stands across; the fryer's and the oven's power switches are one press either way.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class KitchenAppliancePoint : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsHoldInteraction, IFpsObservable
    {
        public const string FryerKind = "kitchen_fryer", RangeKind = "gas_range", OvenKind = "kitchen_oven";
        // 손맛 수치(튜닝 대상): 시선 1도의 끌기에 1/30, 휠 한 칸에 1/6. 0.98 이상이면 끝까지 돌아 잠긴다.
        private const float DragPerDegree = 1f / 30f, WheelNotch = 1f / 6f, SnapOff = .98f;

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
        /// <summary>How far a range's burner cock is turned toward shut by hand: 0 as it was, 1 across the pipe (the heat is off).</summary>
        public float CockTurn => cock;
        /// <summary>Someone switched the heat off, and who.</summary>
        public event Action<KitchenAppliancePoint, string> SwitchedOff;

        private float cock;
        private bool announcedOff;

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

        public string DisplayName => Equipment.Label;
        public string StateText => !On ? "꺼짐" : cock > .02f ? "코크 반쯤 돌아감" : "켜짐";

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
            cock = 1f;
            Equipment.State = "꺼짐";
            SwitchedOff?.Invoke(this, by);
        }

        // ── 손 조작 ──────────────────────────────────────────────────────────

        public HoldStyle HoldStyle => HoldStyle.Turn;

        /// <summary>Only a range's burner cock is turned by hand; the fryer's and the oven's power switches stay a single press.</summary>
        public bool Holdable(FirstPersonResponder responder) => Equipment.Kind == RangeKind && CanInteract(responder, out _);

        public void BeginHold(FirstPersonResponder responder) => announcedOff = false;

        public void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds)
        {
            if (!On) return;
            // 오른쪽·아래로 끌면 잠기는 쪽, 휠은 위(+)가 풀리는 쪽이다.
            float toward = (mouse.x - mouse.y) * DragPerDegree - wheel * WheelNotch;
            if (toward == 0f) return;
            cock = Mathf.Clamp01(cock + toward);
            if (cock < SnapOff) return;
            announcedOff = true;
            SwitchOff("역무원");
            responder?.ShowFeedback(Describe());
        }

        public string EndHold(FirstPersonResponder responder) => announcedOff ? null : Describe();

        public float HoldProgress => cock;

        private string Describe() =>
            Equipment.Label + " · " + (!On ? "화구 코크가 직각으로 돌아가 불이 꺼져 있음" : cock > .02f ? "화구 코크가 중간쯤 돌아가 있음" : "화구 코크가 그대로임");

        public string Observe(FirstPersonResponder responder)
        {
            if (Equipment.Kind != RangeKind) return Equipment.Label + " · " + (On ? "켜져 있음" : "꺼져 있음");
            return Equipment.Label + " · " + (!On ? "불이 꺼져 있고 코크가 직각" : cock > .02f ? "불이 켜져 있고 코크가 중간쯤 돌아가 있음" : "불이 켜져 있고 코크가 열려 있음");
        }
    }
}
