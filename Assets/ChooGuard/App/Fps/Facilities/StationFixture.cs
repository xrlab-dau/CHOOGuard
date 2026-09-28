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
    /// reset, which the shift does not do (JEV 009: the bell rings to the end).
    /// </summary>
    public sealed class CallPointButton : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public bool Pressed { get; private set; }
        private string label;

        private void Awake() => label = TryGetComponent(out StationFixture fixture) ? fixture.Label : "발신기";

        public string DisplayName => label;
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
            Pressed = true;
            StationSignals.PressCallPoint(transform.position, label);
            feedback = "발신기를 눌렀습니다 · 역 전체에 비상벨이 울립니다";
            return true;
        }
    }
}
