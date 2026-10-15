using UnityEngine;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>
    /// A wall-mounted device of the twin, placed by the interior kit (MajibangBuilder KitFixture / KitFixtureMarkers): the indoor
    /// hydrant cabinet (옥내소화전함), the manual call point (발신기) on its indicator set, and the AED cabinet. The call point
    /// works on its own (<see cref="CallPointButton"/>); the shift turns hydrant cabinets into a usable hose and AED cabinets
    /// into cabinets holding an AED to carry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StationFixture : MonoBehaviour
    {
        public enum FixtureKind { CallPoint, Hydrant, Aed }

        public string Element;
        public FixtureKind Kind;
        public string Label = "발신기";

        private void Awake()
        {
            if (Kind == FixtureKind.CallPoint && !TryGetComponent(out CallPointButton _)) gameObject.AddComponent<CallPointButton>();
        }
    }

    /// <summary>
    /// The manual call point (발신기): pressing it sends a fire signal to the receiver — the station bell rings and the
    /// interlocked public doors open (NFTC 203; JEV 010 include_fire_call_point). It stays latched until the receiver is
    /// reset, which the shift does not do (JEV 009: the bell rings to the end). By hand (E held) the button goes in at
    /// <see cref="PressSeconds"/>; letting go earlier presses nothing. A single E press (simple controls) presses it at once.
    /// </summary>
    public sealed class CallPointButton : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsHoldInteraction, IFpsObservable
    {
        /// <summary>How long a hand keeps pressing before the button goes in (game tuning, SIM_CONTROLS_BENCHMARK §6.2).</summary>
        public const float PressSeconds = .8f;
        // 이보다 짧게 눌렀다 뗀 것은 알리지 않는다(스친 것).
        private const float NoticedPress = .15f;

        public bool Pressed { get; private set; }
        private string label;
        private float held = -1f;

        private void Awake() => label = TryGetComponent(out StationFixture fixture) ? fixture.Label : "발신기";

        public string DisplayName => label;
        public string StateText => Pressed ? "눌림" : "";
        public string InteractionPrompt => "발신기 누르기 (화재 신호)";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Pressed) { reason = "눌림 · 수신기가 화재 신호를 받았습니다"; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            Press();
            feedback = "발신기를 눌렀습니다 · 역 전체에 비상벨이 울립니다";
            return true;
        }

        private void Press()
        {
            Pressed = true;
            StationSignals.PressCallPoint(transform.position, label);
        }

        // ── 손 조작 ──────────────────────────────────────────────────────────

        public HoldStyle HoldStyle => HoldStyle.Press;

        public bool Holdable(FirstPersonResponder responder) => !Pressed && responder != null && !responder.IsPaused;

        public void BeginHold(FirstPersonResponder responder) => held = 0f;

        public void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds)
        {
            if (held < 0f || Pressed) return;
            held += deltaSeconds;
            if (held < PressSeconds) return;
            Press();
            responder?.ShowFeedback("발신기를 눌렀습니다 · 역 전체에 비상벨이 울립니다");
        }

        public string EndHold(FirstPersonResponder responder)
        {
            bool partly = !Pressed && held >= NoticedPress;
            held = -1f;
            return partly ? "끝까지 누르지 않아 발신기 버튼이 들어가지 않았습니다" : null;
        }

        /// <summary>Time held over <see cref="PressSeconds"/> (0..1); the ring is hidden once it is pressed.</summary>
        public float HoldProgress => Pressed ? 1f : Mathf.Clamp01(held / PressSeconds);

        public string Observe(FirstPersonResponder responder) => label + " · " + (Pressed ? "버튼이 눌려 있고 수신기에 신호가 들어간 상태" : "버튼이 눌리지 않은 상태");
    }
}
