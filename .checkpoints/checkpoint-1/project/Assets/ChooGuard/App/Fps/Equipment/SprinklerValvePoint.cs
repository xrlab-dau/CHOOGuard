using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The alarm valve station (유수검지장치, a <see cref="StationEquipment"/> of kind <see cref="ValveKind"/>) of one protection zone, on the wall of its valve room:
    /// the alarm check valve with the flow switch and the control valve with its tamper switch. Water running through it (any head of the zone discharging) is what
    /// the receiver reports as sprinkler flow (NFTC 103 2.6); closing the control valve stops the water and puts the zone out of service (2.5.16.1: the tamper switch
    /// shows it at the receiver). Staff read the station but do not shut it: closing the supply is the facility team's job. The zone name and head count come from
    /// the placement entry (<c>zone</c>, <c>heads</c>, <c>area</c>).
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class SprinklerValvePoint : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public const string ValveKind = "sprinkler_valve";

        public StationEquipment Equipment { get; private set; }
        /// <summary>Key the heads of the zone name in their <c>valve</c> entry.</summary>
        public string Key { get; private set; } = "";
        /// <summary>The zone as staff call it: "2층 3구역".</summary>
        public string ZoneName { get; private set; } = "";
        public int HeadCount { get; private set; }
        /// <summary>Floor area in square metres the zone protects.</summary>
        public float Area { get; private set; }
        /// <summary>Water is running through the alarm check valve (a head of the zone discharges).</summary>
        public bool Flowing { get; private set; }
        /// <summary>The control valve is shut: the zone has no water and no protection.</summary>
        public bool Closed { get; private set; }

        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Key = Equipment.Text("key");
            ZoneName = Equipment.Text("zone");
            HeadCount = Mathf.RoundToInt(Equipment.Number("heads"));
            Area = Equipment.Number("area");
            Flowing = false;
            Closed = false;
            Equipment.State = "정상";
        }

        public void SetFlowing(bool flowing)
        {
            if (Flowing == flowing) return;
            Flowing = flowing;
            Refresh();
        }

        /// <summary>The facility team shuts the control valve: no water, the tamper switch reports the zone out of service.</summary>
        public void Close()
        {
            Closed = true;
            Flowing = false;
            Refresh();
        }

        public void Open()
        {
            Closed = false;
            Refresh();
        }

        private void Refresh() => Equipment.State = Closed ? "밸브 폐쇄 · 구역 사용 중지" : Flowing ? "유수 검지 · 헤드 동작" : "정상";

        public string DisplayName => Equipment != null ? Equipment.Label : "유수검지장치";
        public string InteractionPrompt => "유수검지장치 상태 확인";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            return responder != null && !responder.IsPaused;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = ZoneName + " 유수검지장치 · " + HeadCount + "개 헤드 · " +
                       (Closed ? "제어밸브 잠겨 있음 · 이 구역은 스프링클러가 작동하지 않습니다" :
                        Flowing ? "유수 검지됨 · 헤드에서 물이 나오고 있습니다 · 급수 밸브는 시설 담당이 잠급니다" : "정상 · 제어밸브 열림 · 압력 정상");
            return true;
        }
    }
}
