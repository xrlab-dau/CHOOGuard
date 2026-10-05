using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The red emergency stop button at an escalator landing. Anyone may press it when someone falls (research E1, E3); the
    /// station staff member restarts the belt with the key switch once the steps are clear (research E2, JEV 010). It exists only
    /// where a survey record observed it (<see cref="EscalatorStopSurvey"/>), at the place the record gives.
    /// Holding E presses it in: it goes in at <see cref="CallPointButton.PressSeconds"/>, and letting go earlier presses nothing.
    /// A single E press (simple controls) presses it at once.
    /// </summary>
    public sealed class EscalatorStopButton : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsSecondaryInteraction, IFpsHoldInteraction, IFpsObservable
    {
        private const float KnobPressedZ = .55f, KnobRestZ = .9f, NoticedPress = .15f;

        private Escalator escalator;
        private string name_;
        private static Material red, steel;
        // 눌린 버튼은 정지가 이어지는 동안 들어가 있다.
        private Transform knob;
        private float held = -1f, knobIn;
        private bool pressedHere;

        /// <summary>The button plate at <paramref name="position"/>, its face toward <paramref name="facing"/> (out onto the landing).</summary>
        public static void Build(Escalator escalator, Vector3 position, Vector3 facing)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "비상정지 버튼 · " + escalator.Label;
            plate.transform.SetParent(escalator.transform, false);
            plate.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, Vector3.up));
            plate.transform.localScale = new Vector3(.12f, .12f, .02f);
            // 스테인리스 판에 빨간 버튼(이 역에서 버튼 본체가 보인 유일한 기록, 현대 에스컬레이터의 뉴얼 판. 다른 기종의 버튼 모양은 아직 관찰되지 않았다).
            plate.GetComponent<MeshRenderer>().sharedMaterial = Lamp(ref steel, "비상정지 버튼 판 (스테인리스)", new Color(.72f, .73f, .75f));
            var knob = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knob.name = "버튼";
            Destroy(knob.GetComponent<Collider>());
            knob.transform.SetParent(plate.transform, false);
            knob.transform.localPosition = new Vector3(0, 0, KnobRestZ);
            knob.transform.localRotation = Quaternion.Euler(90, 0, 0);
            knob.transform.localScale = new Vector3(.45f, 1.2f, .45f);
            knob.GetComponent<MeshRenderer>().sharedMaterial = Lamp(ref red, "비상정지 버튼 (빨강)", new Color(.85f, .06f, .05f));
            var button = plate.AddComponent<EscalatorStopButton>();
            button.escalator = escalator;
            button.name_ = escalator.Label + " 비상정지 버튼";
            button.knob = knob.transform;
        }

        private static Material Lamp(ref Material cache, string name, Color colour)
        {
            if (cache != null) return cache;
            cache = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            cache.SetColor("_BaseColor", colour);
            return cache;
        }

        public string DisplayName => name_;
        public string StateText => escalator == null || escalator.Running ? "" : pressedHere ? "눌림 · 정지 중" : "정지 중";
        public string InteractionPrompt => "비상정지 버튼 누르기";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (escalator == null) { reason = ""; return false; }
            if (!escalator.Running) { reason = "정지됨 · " + escalator.StoppedBy; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            feedback = Press();
            return true;
        }

        /// <summary>The button goes in: the belt stops, the log keeps it. Returns the feedback line.</summary>
        private string Press()
        {
            escalator.Stop("비상정지 버튼(역무원)");
            pressedHere = true;
            EmergencySession.Current?.Log?.Add(escalator.Label + " 비상정지 버튼을 역무원이 누름");
            return escalator.Label + " 비상정지 · 탄 사람은 걸어서 내립니다";
        }

        public string SecondaryPrompt => escalator != null && !escalator.Running && !escalator.Closed ? "열쇠로 재가동" : "";

        public bool TrySecondary(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (SecondaryPrompt.Length == 0) return false;
            if (!escalator.Restart(out feedback)) return false;
            pressedHere = false;
            EmergencySession.Current?.Log?.Add(escalator.Label + " 역무원이 열쇠 스위치로 재가동");
            var session = EmergencySession.Current;
            if (session != null) session.Hud.Radio.Push(RadioChannel.Self, "역무실, " + escalator.Label + " 이상 없음 확인하고 재가동했습니다.");
            feedback = escalator.Label + " 재가동";
            return true;
        }

        // ── 손 조작 ──────────────────────────────────────────────────────────

        public HoldStyle HoldStyle => HoldStyle.Press;

        public bool Holdable(FirstPersonResponder responder) => responder != null && !responder.IsPaused && CanInteract(responder, out _);

        public void BeginHold(FirstPersonResponder responder) => held = 0f;

        public void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds)
        {
            if (held < 0f || escalator == null || !escalator.Running) return;
            held += deltaSeconds;
            if (held < CallPointButton.PressSeconds) return;
            responder?.ShowFeedback(Press());
        }

        public string EndHold(FirstPersonResponder responder)
        {
            bool partly = escalator != null && escalator.Running && held >= NoticedPress;
            held = -1f;
            return partly ? "끝까지 누르지 않아 비상정지 버튼이 들어가지 않았습니다" : null;
        }

        /// <summary>Time held over <see cref="CallPointButton.PressSeconds"/> (0..1).</summary>
        public float HoldProgress => escalator != null && !escalator.Running ? 1f : Mathf.Clamp01(held / CallPointButton.PressSeconds);

        public string Observe(FirstPersonResponder responder) =>
            name_ + " · " + (escalator == null || escalator.Running ? "버튼이 나와 있고 에스컬레이터가 움직이는 중" : pressedHere ? "버튼이 눌려 있고 에스컬레이터가 멈춰 있음" : "에스컬레이터가 멈춰 있음");

        private void Update()
        {
            if (knob == null) return;
            float knobGoal = pressedHere && escalator != null && !escalator.Running ? 1f : 0f;
            if (Mathf.Approximately(knobIn, knobGoal)) return;
            knobIn = Mathf.MoveTowards(knobIn, knobGoal, Time.deltaTime / .12f);
            knob.localPosition = new Vector3(0, 0, Mathf.Lerp(KnobRestZ, KnobPressedZ, knobIn));
        }
    }
}
