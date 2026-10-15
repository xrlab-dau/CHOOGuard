using UnityEngine;

namespace ChooGuard.App.Fps.Facilities
{
    /// <summary>A push button of an elevator: the landing call panel or a car panel button (floor, open, close, emergency call).</summary>
    public sealed class ElevatorButton : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsHoldInteraction, IFpsObservable
    {
        public enum Kinds { Hall, Floor, Open, Close, Alarm }

        public ElevatorCar Car;
        public Kinds Kind;
        /// <summary>Hall buttons: the landing they call to.</summary>
        public int Landing = -1;
        /// <summary>Floor buttons: the floor label printed on the button.</summary>
        public string Floor = "";

        /// <summary>Hand control tuning; the physical travel is supplied separately from the actual part.</summary>
        public const float PressSeconds = .2f, ReleaseSeconds = .1f;
        [SerializeField] private Transform pressPart;
        [SerializeField] private Vector3 localPressTravel;
        private Vector3 restPosition;
        private float held = -1f, pressed, tapUntil = -1f;
        private bool committed;

        public Transform PressPart => pressPart;
        public float PressPosition => pressed;

        public static ElevatorButton Add(Transform at, ElevatorCar car, int landing, Kinds kind, string floor, Vector3 size)
        {
            var button = at.GetComponent<ElevatorButton>() ?? at.gameObject.AddComponent<ElevatorButton>();
            button.Car = car;
            button.Kind = kind;
            button.Landing = landing;
            button.Floor = floor;
            if (at.GetComponent<Collider>() == null)
            {
                var box = at.gameObject.AddComponent<BoxCollider>();
                box.size = size;
            }
            return button;
        }

        /// <summary>Bind a separated real push part and its measured local travel; creates no geometry.</summary>
        public void BindPressPart(Transform part, Vector3 travel)
        {
            if (pressPart == part && localPressTravel == travel) return;
            if (pressPart != null) pressPart.localPosition = restPosition;
            pressPart = part;
            localPressTravel = travel;
            restPosition = part != null ? part.localPosition : Vector3.zero;
            ApplyPressPosition();
        }

        private void Awake() => restPosition = pressPart != null ? pressPart.localPosition : Vector3.zero;

        public string DisplayName => Kind == Kinds.Hall ? "엘리베이터 호출 버튼" : Kind == Kinds.Floor ? "엘리베이터 조작반 · " + Floor + "층" : Kind == Kinds.Open ? "엘리베이터 조작반 · 열림" : Kind == Kinds.Close ? "엘리베이터 조작반 · 닫힘" : "엘리베이터 조작반 · 비상통화";
        public string InteractionPrompt => Kind == Kinds.Hall ? "호출" : Kind == Kinds.Floor ? Floor + "층 누르기" : Kind == Kinds.Open ? "열림 누르기" : Kind == Kinds.Close ? "닫힘 누르기" : "비상통화 누르기";
        public string StateText => Requested ? "호출됨" : pressed >= .99f ? "눌림" : pressed > .01f ? "누르는 중" : "";

        /// <summary>The real call is pending, rather than the button being permanently depressed.</summary>
        public bool Requested
        {
            get
            {
                if (Car == null) return false;
                int target = Kind == Kinds.Hall ? Landing : Kind == Kinds.Floor ? Car.FloorIndex(Floor) : -1;
                if (target < 0 || target >= Car.Landings.Count) return false;
                return Kind == Kinds.Hall ? Car.Landings[target].HallCall : Car.Landings[target].CarCall;
            }
        }

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Car == null) { reason = "연결된 승강기가 없습니다"; return false; }
            if (!Car.Running && Kind != Kinds.Alarm) { reason = "운행 정지 (정전·지진·고장) · 비상통화만 됩니다"; return false; }
            if (Kind == Kinds.Hall && (Landing < 0 || Landing >= Car.Landings.Count)) { reason = "연결된 승강장이 없습니다"; return false; }
            if (Kind == Kinds.Floor && Car.FloorIndex(Floor) < 0) { reason = "운행 층이 연결되지 않았습니다"; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            tapUntil = Time.time + ReleaseSeconds;
            switch (Kind)
            {
                case Kinds.Hall:
                    Car.Call(Landing);
                    feedback = Car.Landings[Landing].Floor + "층 호출";
                    return true;
                case Kinds.Floor:
                    int to = Car.FloorIndex(Floor);
                    if (to < 0) return false;
                    Car.Press(to);
                    feedback = Floor + "층";
                    return true;
                case Kinds.Open: Car.OpenButton(); return true;
                case Kinds.Close: Car.CloseButton(); return true;
                default:
                    feedback = "비상통화 · 승강기 관리 담당에 연결됩니다";
                    return true;
            }
        }

        public HoldStyle HoldStyle => HoldStyle.Press;
        public bool Holdable(FirstPersonResponder responder) => responder != null && !responder.IsPaused && CanInteract(responder, out _);

        public void BeginHold(FirstPersonResponder responder)
        {
            held = CanInteract(responder, out _) ? 0f : -1f;
            committed = false;
        }

        public void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds)
        {
            if (held < 0f || committed || !CanInteract(responder, out _) || deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            held += deltaSeconds;
            if (held < PressSeconds) return;
            committed = TryInteract(responder, out var feedback);
            if (committed && !string.IsNullOrEmpty(feedback)) responder?.ShowFeedback(feedback);
        }

        public string EndHold(FirstPersonResponder responder)
        {
            bool partial = held >= .05f && !committed;
            held = -1f;
            committed = false;
            return partial ? "끝까지 누르지 않아 버튼 신호가 들어가지 않았습니다" : null;
        }

        public float HoldProgress => held < 0f ? -1f : committed ? 1f : Mathf.Clamp01(held / PressSeconds);
        public string Observe(FirstPersonResponder responder) => DisplayName + " · " + (Requested ? "호출 신호가 들어간 상태" : pressed > .01f ? "버튼을 누르는 중" : "버튼이 나와 있는 상태");

        private void Update() => AdvancePressVisual(Time.deltaTime);

        /// <summary>Move only the supplied separated part. A released button springs back even while its call remains queued.</summary>
        public void AdvancePressVisual(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            float target = held >= 0f ? committed ? 1f : Mathf.Clamp01(held / PressSeconds) : Time.time < tapUntil ? 1f : 0f;
            pressed = Mathf.MoveTowards(pressed, target, deltaSeconds / ReleaseSeconds);
            ApplyPressPosition();
        }

        private void ApplyPressPosition()
        {
            if (pressPart != null) pressPart.localPosition = restPosition + localPressTravel * pressed;
        }

        private void OnDisable()
        {
            held = tapUntil = -1f;
            pressed = 0f;
            committed = false;
            ApplyPressPosition();
        }
    }
}
