using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// One breaker of a distribution board's dead-front: a small collider over the breaker body (live only while the board
    /// door is open) with the lever model under it. Aiming at it names the circuit it switches, as the label of the board
    /// does; the primary action flips the lever. The lever follows the circuit, so a breaker the protection tripped or the
    /// electrician switched shows it too. By hand (E held) the mouse drags the lever from its end toward the other: past most of
    /// the travel it snaps over and the circuit switches, short of that it springs back.
    /// </summary>
    public sealed class BreakerSwitch : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsHoldInteraction, IFpsObservable
    {
        [Tooltip("Slot on the dead-front (0 = main breaker).")]
        public int Slot;
        [Tooltip("The lever model, origin on its hinge.")]
        public Transform Lever;

        private const float DegreesPerSecond = 260f;
        // 손맛 수치(튜닝 대상): 시선 1도의 끌기에 레버 1/25, 휠 한 칸에 1/6. 85% 를 넘기면 끝까지 넘어간다.
        private const float DragPerDegree = 1f / 25f, WheelNotch = 1f / 6f, SnapAt = .85f, MeaningfulPull = .15f;
        private ElectricNetwork.Circuit circuit;
        private float angle = float.NaN;
        // 손으로 잡은 동안: 레버가 시작한 끝(켜짐이면 위), 그 끝에서 반대 끝까지 간 비율.
        private bool held, snapped, startOn;
        private float pull;
        private ElectricNetwork.Circuit namedFor;
        private string name_;
        private string tagFor, stateWithTag;

        /// <summary>The circuit this breaker switches; null before the network is built.</summary>
        public ElectricNetwork.Circuit Circuit => circuit;

        public void Bind(ElectricNetwork.Circuit bound)
        {
            circuit = bound;
            SetAngle(Target, true);
        }

        private static float OnAngle => -BreakerDeckLayout.LeverDegrees;
        private static float OffAngle => BreakerDeckLayout.LeverDegrees;

        private float Target => circuit != null && circuit.On ? OnAngle : OffAngle;

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
            // 손이 잡고 있는 동안 레버는 손을 따른다(넘어간 뒤에는 회로의 상태로 간다).
            if (held && !snapped) return;
            float target = Target;
            angle = Mathf.MoveTowards(angle, target, DegreesPerSecond * Time.deltaTime);
            Lever.localRotation = Quaternion.Euler(angle, 0, 0);
            if (Mathf.Approximately(angle, target)) enabled = false;
        }

        public string DisplayName
        {
            get
            {
                if (circuit == null) return "차단기";
                if (namedFor != circuit)
                {
                    namedFor = circuit;
                    name_ = circuit.Board.Code + " · " + (circuit.Number == 0 ? "메인 차단기" : circuit.Number + "번 차단기") + " — " + circuit.Name;
                }
                return name_;
            }
        }

        public string StateText
        {
            get
            {
                if (circuit == null) return "";
                if (circuit.On) return "켜짐";
                if (circuit.Tag == null) return "꺼짐";
                if (!ReferenceEquals(tagFor, circuit.Tag)) { tagFor = circuit.Tag; stateWithTag = "꺼짐 · " + circuit.Tag; }
                return stateWithTag;
            }
        }

        public string InteractionPrompt
        {
            get
            {
                if (circuit == null) return "";
                if (circuit.Number == 0) return circuit.On ? "메인 차단기 내리기 (분전반 전체 차단)" : "메인 차단기 올리기";
                return circuit.On ? "차단기 내리기" : "차단기 올리기";
            }
        }

        public bool CanInteract(FirstPersonResponder responder, out string reason) => Check(responder, true, out reason);

        // describe: false 면 이유 문장을 만들지 않는다(매 프레임 부르는 Holdable 이 문자열을 만들지 않게).
        private bool Check(FirstPersonResponder responder, bool describe, out string reason)
        {
            reason = null;
            if (circuit == null || responder == null || responder.IsPaused) { reason = ""; return false; }
            if (!circuit.Operable) { if (describe) reason = DisplayName + " · 조명·콘센트 회로는 전기 담당이 다룹니다"; return false; }
            if (!circuit.On && circuit.Board.Damaged) { if (describe) reason = "화재로 소손된 분전반입니다 · 전기 담당이 교체할 때까지 올릴 수 없습니다"; return false; }
            // 작동금지 표지는 건 사람이 뗀다(산안규칙 제319조): 전기 담당이 건 표지를 역무원이 떼고 올리지 않는다.
            if (!circuit.On && circuit.Tag != null && circuit.TagBy != ElectricNetwork.StaffName) { if (describe) reason = circuit.Tag + " 표지가 붙어 있어 올릴 수 없습니다"; return false; }
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

        // ── 손 조작 ──────────────────────────────────────────────────────────

        public HoldStyle HoldStyle => HoldStyle.Lever;

        /// <summary>The lever can be grabbed when a press could switch it (otherwise <see cref="CanInteract"/> says why not).</summary>
        public bool Holdable(FirstPersonResponder responder) => Check(responder, false, out _);

        public void BeginHold(FirstPersonResponder responder)
        {
            held = true;
            snapped = false;
            startOn = circuit != null && circuit.On;
            float from = startOn ? OnAngle : OffAngle, to = startOn ? OffAngle : OnAngle;
            // 레버가 아직 움직이는 중이면 그 자리에서 이어서 잡는다.
            pull = float.IsNaN(angle) ? 0f : Mathf.Clamp01(Mathf.InverseLerp(from, to, angle));
        }

        public void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds)
        {
            if (!held || snapped || circuit == null) return;
            // 다른 곳에서 회로가 바뀌었다(보호장치 등): 손은 더 끌지 못하고 레버는 회로를 따른다.
            if (circuit.On != startOn) { snapped = true; Resume(); return; }
            // 마우스를 위로(휠은 위로 굴려) 밀면 레버가 올라가는 쪽이다. 켜진 레버는 아래로, 꺼진 레버는 위로 끈다.
            float up = mouse.y * DragPerDegree + wheel * WheelNotch;
            pull = Mathf.Clamp01(pull + (startOn ? -up : up));
            if (Lever != null && !float.IsNaN(angle))
            {
                angle = Mathf.Lerp(startOn ? OnAngle : OffAngle, startOn ? OffAngle : OnAngle, pull);
                Lever.localRotation = Quaternion.Euler(angle, 0, 0);
            }
            if (pull < SnapAt) return;
            // 끝까지 넘어갔다: 한 번 누른 것과 같은 길로 회로를 바꾼다. 거절돼도 레버는 회로의 자리로 돌아간다.
            snapped = true;
            Resume();
            TryInteract(responder, out string feedback);
            if (!string.IsNullOrEmpty(feedback)) responder?.ShowFeedback(feedback);
        }

        public string EndHold(FirstPersonResponder responder)
        {
            bool springBack = held && !snapped;
            held = false;
            if (!springBack) return null;
            Refresh();
            return pull >= MeaningfulPull ? "차단기 레버가 끝까지 넘어가지 않아 제자리로 돌아왔습니다" : null;
        }

        // 레버 모델이 있고 각도가 정해진 뒤에만 Update 를 켠다(없는 레버를 돌리지 않게).
        private void Resume()
        {
            if (Lever != null && !float.IsNaN(angle)) enabled = true;
        }

        /// <summary>How far the lever has been dragged from its end toward the other (0..1).</summary>
        public float HoldProgress => pull;

        public string Observe(FirstPersonResponder responder)
        {
            if (circuit == null) return "차단기";
            return DisplayName + " · 레버가 " + (circuit.On ? "올라가 있음(켜짐)" : "내려가 있음(꺼짐)") + (circuit.Tag != null ? " · 표지: " + circuit.Tag : "");
        }
    }
}
