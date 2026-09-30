using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// One breaker of a distribution board's dead-front: a small collider over the breaker body (live only while the board
    /// door is open) with the lever model under it. Aiming at it names the circuit it switches, as the label of the board
    /// does; the primary action flips the lever. The lever follows the circuit, so a breaker the protection tripped or the
    /// electrician switched shows it too.
    /// </summary>
    public sealed class BreakerSwitch : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        [Tooltip("Slot on the dead-front (0 = main breaker).")]
        public int Slot;
        [Tooltip("The lever model, origin on its hinge.")]
        public Transform Lever;

        private const float DegreesPerSecond = 260f;
        private ElectricNetwork.Circuit circuit;
        private float angle = float.NaN;

        /// <summary>The circuit this breaker switches; null before the network is built.</summary>
        public ElectricNetwork.Circuit Circuit => circuit;

        public void Bind(ElectricNetwork.Circuit bound)
        {
            circuit = bound;
            SetAngle(Target, true);
        }

        private float Target => circuit != null && circuit.On ? -BreakerDeckLayout.LeverDegrees : BreakerDeckLayout.LeverDegrees;

        /// <summary>Moves the lever to the circuit's state (animated unless <paramref name="instant"/>).</summary>
        public void Refresh() => SetAngle(Target, false);

        private void SetAngle(float target, bool instant)
        {
            if (Lever == null) return;
            if (float.IsNaN(angle) || instant) { angle = target; Lever.localRotation = Quaternion.Euler(angle, 0, 0); enabled = false; return; }
            enabled = true;
        }

        private void Awake() => enabled = false;

        private void Update()
        {
            float target = Target;
            angle = Mathf.MoveTowards(angle, target, DegreesPerSecond * Time.deltaTime);
            Lever.localRotation = Quaternion.Euler(angle, 0, 0);
            if (Mathf.Approximately(angle, target)) enabled = false;
        }

        public string DisplayName => circuit == null ? "차단기" : circuit.Board.Code + " · " + (circuit.Number == 0 ? "메인 차단기" : circuit.Number + "번 차단기") + " — " + circuit.Name + (circuit.On ? "" : " (꺼짐" + (circuit.Tag != null ? " · " + circuit.Tag : "") + ")");

        public string InteractionPrompt
        {
            get
            {
                if (circuit == null) return "";
                if (circuit.Number == 0) return circuit.On ? "메인 차단기 내리기 (분전반 전체 차단)" : "메인 차단기 올리기";
                return circuit.On ? "차단기 내리기" : "차단기 올리기";
            }
        }

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (circuit == null || responder == null || responder.IsPaused) { reason = ""; return false; }
            if (!circuit.Operable) { reason = DisplayName + " · 조명·콘센트 회로는 전기 담당이 다룹니다"; return false; }
            if (!circuit.On && circuit.Board.Damaged) { reason = "화재로 소손된 분전반입니다 · 전기 담당이 교체할 때까지 올릴 수 없습니다"; return false; }
            // 작동금지 표지는 건 사람이 뗀다(산안규칙 제319조): 전기 담당이 건 표지를 역무원이 떼고 올리지 않는다.
            if (!circuit.On && circuit.Tag != null && circuit.TagBy != ElectricNetwork.StaffName) { reason = circuit.Tag + " 표지가 붙어 있어 올릴 수 없습니다"; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            bool on = !circuit.On;
            if (!ElectricNetwork.Switch(circuit, on, "역무원")) return false;
            feedback = on ? circuit.Label + " 차단기를 올렸습니다" : circuit.Label + " 차단기를 내렸습니다" + (circuit.Load != null ? " · " + circuit.Load.Label + " 전원 꺼짐" : circuit.Number == 0 ? " · 분전반 전체가 꺼졌습니다" : "");
            return true;
        }
    }
}
