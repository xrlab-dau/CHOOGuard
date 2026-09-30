using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
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
