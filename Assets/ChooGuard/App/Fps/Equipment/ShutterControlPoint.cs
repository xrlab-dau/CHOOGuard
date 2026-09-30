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

    /// <summary>One push button of a <see cref="ShutterControlPoint"/> (its own collider); the secondary action (R) is the reset switch behind the key cover.</summary>
    public sealed class ShutterButton : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsSecondaryInteraction
    {
        [SerializeField] private FireShutterPoint.Command command = FireShutterPoint.Command.Stop;

        private ShutterControlPoint control;

        private ShutterControlPoint Control => control != null ? control : control = GetComponentInParent<ShutterControlPoint>();

        public string DisplayName => Control != null ? Control.ShutterLabel + " 조작함" : "방화셔터 조작함";

        public string InteractionPrompt => command == FireShutterPoint.Command.Up ? "상향 버튼 누르기" : command == FireShutterPoint.Command.Down ? "하향 버튼 누르기" : "정지 버튼 누르기";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            return responder != null && !responder.IsPaused && Control != null && Control.Shutter != null;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = Control.Operate(command);
            if (feedback != null) return false;
            var shutter = Control.Shutter;
            feedback = command == FireShutterPoint.Command.Up ? shutter.Equipment.Label + " 상승" : command == FireShutterPoint.Command.Down ? shutter.Equipment.Label + " 하강" : shutter.Equipment.Label + " 정지";
            return true;
        }

        public string SecondaryPrompt => "열쇠로 복구 스위치";

        public bool TrySecondary(FirstPersonResponder responder, out string feedback)
        {
            feedback = Control.Operate(FireShutterPoint.Command.Reset);
            if (feedback != null) return false;
            feedback = Control.Shutter.Equipment.Label + " 복구 · 상승";
            return true;
        }
    }
}
