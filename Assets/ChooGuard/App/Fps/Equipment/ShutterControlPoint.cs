using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The manual control box of a fire shutter (연동제어기의 수동조작함, a <see cref="StationEquipment"/> of kind <see cref="ControlKind"/>) on the wall beside it: three push
    /// buttons (상향, 정지, 하향) and, behind the key cover, the reset switch (복구). Only the key holder works it, the station staff member does (the makers'
    /// leaflets and the patent KR101245222B1 describe the keyed cover). The box knows its shutter by <c>shutter</c> in the placement data. Each button is a collider of
    /// its own (<see cref="ShutterButton"/>) so the player aims at one; the reset switch is the secondary action (R) of every button.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class ShutterControlPoint : MonoBehaviour
    {
        public const string ControlKind = "shutter_control";

        private StationEquipment equipment;
        private FireShutterPoint shutter;

        /// <summary>The shutter this box operates, found through the registry on first use.</summary>
        public FireShutterPoint Shutter
        {
            get
            {
                if (shutter != null) return shutter;
                equipment = equipment != null ? equipment : GetComponent<StationEquipment>();
                shutter = EquipmentRegistry.Find(equipment.Text("shutter"))?.GetComponent<FireShutterPoint>();
                return shutter;
            }
        }

        public string ShutterLabel => Shutter != null ? Shutter.Equipment.Label : "방화셔터";

        /// <summary>The staff member operated the box: the log line, the toast and the office's ear (the director hears <see cref="FireShutterPoint.Commanded"/>).</summary>
        public string Operate(FireShutterPoint.Command command)
        {
            var target = Shutter;
            if (target == null) return "연결된 방화셔터를 찾지 못했습니다";
            string refused = target.Operate(command, "역무원");
            if (refused != null) return refused;
            string what = command == FireShutterPoint.Command.Up ? "상향 버튼" : command == FireShutterPoint.Command.Down ? "하향 버튼" : command == FireShutterPoint.Command.Stop ? "정지 버튼" : "복구 스위치";
            EmergencySession.Current?.Log?.Add(target.Equipment.Label + " " + what + "을 역무원이 누름");
            return null;
        }
    }
}
