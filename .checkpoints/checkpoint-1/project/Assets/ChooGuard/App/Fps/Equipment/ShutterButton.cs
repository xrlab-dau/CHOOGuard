using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>One push button of a <see cref="ShutterControlPoint"/> (its own collider); the secondary action (R) is the reset switch behind the key cover.</summary>
    public sealed class ShutterButton : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsObservable, IFpsSecondaryInteraction
    {
        [SerializeField] private FireShutterPoint.Command command = FireShutterPoint.Command.Stop;

        private ShutterControlPoint control;

        private ShutterControlPoint Control => control != null ? control : control = GetComponentInParent<ShutterControlPoint>();

        private string namedFor, name_;

        public string DisplayName
        {
            get
            {
                if (Control == null) return "방화셔터 조작함";
                string label = Control.ShutterLabel;
                if (!ReferenceEquals(label, namedFor)) { namedFor = label; name_ = label + " 조작함"; }
                return name_;
            }
        }

        /// <summary>How the shutter beside the box stands now ("열림", "하강 중", "일부 폐쇄 (1단)" …): the word the shutter itself carries.</summary>
        public string StateText => Control != null && Control.Shutter != null ? Control.Shutter.Equipment.State : "";

        public string Observe(FirstPersonResponder responder)
        {
            var shutter = Control != null ? Control.Shutter : null;
            if (shutter == null) return DisplayName;
            string curtain = shutter.Moving ? (shutter.Target < shutter.Opening ? "셔터가 내려오는 중" : "셔터가 올라가는 중")
                : shutter.Opening >= .999f ? "셔터가 다 올라가 있음" : shutter.Opening <= .001f ? "셔터가 바닥까지 내려와 있음" : "셔터가 중간에 멈춰 있음";
            return DisplayName + " · " + curtain;
        }

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
