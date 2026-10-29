using ChooGuard.App.Fps.Facilities;
using ChooGuard.App.Fps.Hud;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The red emergency stop button at an escalator landing. Anyone may press it when someone falls (research E1, E3); the
    /// station staff member restarts the belt with the key switch once the steps are clear (research E2, JEV 010). It exists only
    /// where a survey record observed it (<see cref="EscalatorStopSurvey"/>), on the twin's own escalator face nearest the place the
    /// record gives (<see cref="Escalator.MountOnTwin"/>).
    /// Holding E presses it in: it goes in at <see cref="CallPointButton.PressSeconds"/>, and letting go earlier presses nothing.
    /// A single E press (simple controls) presses it at once.
    /// </summary>
    public sealed class EscalatorStopButton : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsSecondaryInteraction, IFpsHoldInteraction, IFpsObservable
    {
        private const float NoticedPress = .15f;
        // 작은 빨간 버튼: 지름 5 cm, 받침에서 2 cm 나와 있고 1.2 cm 들어간다. 받침은 지름 7.5 cm, 1 cm 두께의 어두운 원판. 받침 뒷면 중심이 원점, +z 가 난간 밖.
        private const float BaseDiameter = .075f, BaseThickness = .01f, KnobDiameter = .05f, KnobLength = .02f, KnobTravel = .012f;

        private Escalator escalator;
        private string name_;
        private static Material red, dark;
        // 눌린 버튼은 정지가 이어지는 동안 들어가 있다.
        private Transform knob;
        private Vector3 knobRestPosition, knobTravel;
        private float held = -1f, knobIn;
        private bool pressedHere;

        /// <summary>Name of the pressed part inside a supplied button model (<see cref="EmergencyArt.EscalatorStopModel"/>).</summary>
        public const string KnobName = "버튼";
        /// <summary>How far the model's knob goes in when pressed (m, along the plate's -z, into the mount).</summary>
        public const float ModelKnobTravel = .008f;

        /// <summary>
        /// The button plate at <paramref name="position"/>, its face toward <paramref name="facing"/> (out onto the landing). With
        /// <paramref name="model"/> (the Meshy plate, origin at the back face centre, +z out of the mount, child <see cref="KnobName"/>
        /// = the red knob) that model is the plate and its knob is what goes in; without one it is a small red button on a dark round
        /// base (primitives), the base's back on the mount.
        /// </summary>
        public static void Build(Escalator escalator, Vector3 position, Vector3 facing, GameObject model = null)
        {
            if (model != null)
            {
                var plateModel = Object.Instantiate(model, escalator.transform);
                plateModel.name = "비상정지 버튼 · " + escalator.Label;
                plateModel.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, Vector3.up));
                var modelKnob = plateModel.transform.Find(KnobName);
                if (modelKnob != null) foreach (var c in modelKnob.GetComponentsInChildren<Collider>()) DestroyImmediate(c);   // the plate's collider is the aim target
                if (plateModel.GetComponent<Collider>() == null)
                {
                    // One collider on the plate root is the aim target; part colliders of the model would hit the wrong object.
                    foreach (var c in plateModel.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
                    var bounds = Props.LocalBounds(plateModel.transform);
                    var box = plateModel.AddComponent<BoxCollider>();
                    if (bounds.size.sqrMagnitude > 0) { box.center = bounds.center; box.size = bounds.size; }
                    else { box.size = new Vector3(.12f, .12f, .03f); box.center = new Vector3(0, 0, .015f); }
                }
                plateModel.AddComponent<EscalatorStopButton>().Bind(escalator, modelKnob, new Vector3(0, 0, -ModelKnobTravel));
                return;
            }
            // 작은 빨간 버튼이 어두운 받침에 앉아 난간 끝 아래쪽에 붙는다(버튼 본체는 이 역 영상에서 보이지 않았다: 표지 아래의 작은 비상정지 버튼으로 둔다).
            var plate = new GameObject("비상정지 버튼 · " + escalator.Label);
            plate.transform.SetParent(escalator.transform, false);
            plate.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, Vector3.up));
            var aim = plate.AddComponent<BoxCollider>();   // the one aim target: base and knob together
            aim.center = new Vector3(0, 0, (BaseThickness + KnobLength) / 2);
            aim.size = new Vector3(BaseDiameter, BaseDiameter, BaseThickness + KnobLength);
            Disc(plate.transform, "받침", BaseThickness / 2, BaseThickness, BaseDiameter, Lamp(ref dark, "비상정지 버튼 받침 (검정)", new Color(.12f, .12f, .13f)));
            var knob = Disc(plate.transform, KnobName, BaseThickness + KnobLength / 2, KnobLength, KnobDiameter, Lamp(ref red, "비상정지 버튼 (빨강)", new Color(.85f, .06f, .05f)));
            var button = plate.AddComponent<EscalatorStopButton>();
            button.Bind(escalator, knob.transform, new Vector3(0, 0, -KnobTravel));
        }

        /// <summary>A cylinder along the plate's z: its centre at <paramref name="centreZ"/>, <paramref name="length"/> long, no collider of its own.</summary>
        private static Transform Disc(Transform plate, string name, float centreZ, float length, float diameter, Material material)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            DestroyImmediate(disc.GetComponent<Collider>());   // at once: the plate's box must be the only aim target this frame
            disc.transform.SetParent(plate, false);
            disc.transform.localPosition = new Vector3(0, 0, centreZ);
            disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            disc.transform.localScale = new Vector3(diameter, length / 2, diameter);
            disc.GetComponent<MeshRenderer>().sharedMaterial = material;
            return disc.transform;
        }

        /// <summary>Connect a separated surveyed button and its local travel without creating a plate or changing its placement.</summary>
        public void Bind(Escalator source, Transform pressPart, Vector3 localTravel)
        {
            if (knob != null) knob.localPosition = knobRestPosition;
            escalator = source;
            name_ = source != null ? source.Label + " 비상정지 버튼" : "에스컬레이터 비상정지 버튼";
            knob = pressPart;
            knobRestPosition = pressPart != null ? pressPart.localPosition : Vector3.zero;
            knobTravel = localTravel;
            held = -1f;
            knobIn = 0f;
            pressedHere = false;
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
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            held += deltaSeconds;
            if (held < CallPointButton.PressSeconds) return;
            string feedback = Press();
            responder?.ShowFeedback(feedback);
        }

        public string EndHold(FirstPersonResponder responder)
        {
            bool partly = escalator != null && escalator.Running && held >= NoticedPress;
            held = -1f;
            return partly ? "끝까지 누르지 않아 비상정지 버튼이 들어가지 않았습니다" : null;
        }

        /// <summary>Time held over <see cref="CallPointButton.PressSeconds"/> (0..1).</summary>
        public float HoldProgress => pressedHere && escalator != null && !escalator.Running ? 1f : Mathf.Clamp01(held / CallPointButton.PressSeconds);

        public string Observe(FirstPersonResponder responder) =>
            name_ + " · " + (escalator == null || escalator.Running ? "버튼이 나와 있고 에스컬레이터가 움직이는 중" : pressedHere ? "버튼이 눌려 있고 에스컬레이터가 멈춰 있음" : "에스컬레이터가 멈춰 있음");

        private void Update()
        {
            if (knob == null) return;
            if (escalator != null && escalator.Running && held < 0f) pressedHere = false;
            float knobGoal = pressedHere && escalator != null && !escalator.Running ? 1f : Mathf.Clamp01(held / CallPointButton.PressSeconds);
            if (Mathf.Approximately(knobIn, knobGoal)) return;
            knobIn = Mathf.MoveTowards(knobIn, knobGoal, Time.deltaTime / .12f);
            knob.localPosition = knobRestPosition + knobTravel * knobIn;
        }
    }
}
