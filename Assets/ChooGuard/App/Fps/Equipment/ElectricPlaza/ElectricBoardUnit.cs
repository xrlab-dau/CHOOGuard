using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The behaviour of a placed distribution board: the door is locked, staff unlock it with the board key (R) and swing it open by
    /// hand (E held, drag), and while it stands open enough the eight breakers behind it can be aimed at (<see cref="BreakerSwitch"/>).
    /// The door leaf carries the collider this component answers for; the breaker colliders exist only while the door is open
    /// (<see cref="BoardDoor.IsOpen"/>), so a closed board offers nothing but its door. A board that is smoking or burning cannot be
    /// opened: opening a live panel with an arcing fault is how arc-flash injuries happen. With simple controls one E press opens or
    /// closes it (the key is taken as used). The shift closes and locks the door when the staff member walks away.
    /// </summary>
    public sealed class ElectricBoardUnit : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsSecondaryInteraction, IFpsHoldInteraction, IFpsObservable
    {
        private const float WalkAwayMetres = 7f, WalkAwaySeconds = 4f;
        // 손맛 수치(튜닝 대상): 시선 40도의 끌기에 문이 끝까지 열린다.
        private const float DragDegreesForFullSwing = 40f;

        [Tooltip("The hinged door leaf on its pivot.")]
        public BoardDoor Door;
        [Tooltip("The eight breakers, slot 0 (main) first.")]
        public BreakerSwitch[] Switches = Array.Empty<BreakerSwitch>();

        /// <summary>Why the door cannot be opened now (a fire in it), or null. Set by the incident director.</summary>
        public Func<string> Blocked;

        /// <summary>The door is locked with the board key: it cannot be swung open until the staff member unlocks it (R).</summary>
        public bool Locked { get; private set; } = true;

        private StationEquipment equipment;
        private float awaySince = -1;
        private bool breakersLive;
        // 손으로 문을 잡은 동안: 문짝 중심(경첩 피벗 좌표)과 잡기 시작한 문의 상태(0 닫힘, 1 반쯤, 2 열림).
        private Vector3 leafCentre;
        private int stateAtBegin;

        private void Awake()
        {
            equipment = GetComponent<StationEquipment>();
            SetBreakers(false);
        }

        /// <summary>Hands every breaker its circuit once the network is built.</summary>
        public void Bind(ElectricNetwork.Board board)
        {
            var all = System.Linq.Enumerable.ToList(board.All);
            for (int i = 0; i < Switches.Length && i < all.Count; i++) Switches[i].Bind(all[i]);
        }

        /// <summary>The levers follow the circuits.</summary>
        public void Refresh()
        {
            foreach (var breaker in Switches) breaker.Refresh();
        }

        private void SetBreakers(bool active)
        {
            breakersLive = active;
            foreach (var breaker in Switches)
                foreach (var collider in breaker.GetComponents<Collider>()) collider.enabled = active;
        }

        private static bool SimpleControls()
        {
            var session = EmergencySession.Current;
            return session == null || session.Player == null || session.Player.SimpleControls;
        }

        public string DisplayName => equipment != null ? equipment.Label : "분전반";
        public string StateText => Door == null ? "" : Door.IsOpen ? "문 열림" : !Door.IsShut ? "문 반쯤 열림" : Locked ? "잠겨 있음" : "닫힘";
        public string InteractionPrompt => Door == null ? "" : Door.IsOpen ? "분전반 닫기" : "분전반 열기 (열쇠)";

        private bool FireBlocks() => Door != null && !Door.IsOpen && Blocked?.Invoke() is string why && why.Length > 0;

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Door == null || responder == null || responder.IsPaused) { reason = ""; return false; }
            if (!Door.IsOpen && Blocked?.Invoke() is string why && why.Length > 0) { reason = why; return false; }
            // 손 조작에서는 열쇠로 풀기 전까지 문이 열리지 않는다(단순 조작은 열쇠를 쓴 것으로 본다).
            if (!responder.SimpleControls && Locked && !Door.IsOpen) { reason = "분전반 문이 잠겨 있습니다"; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            bool open = !Door.IsOpen;
            Door.SetOpen(open);
            // 열쇠로 연 문은 풀려 있고, 닫으면 다시 잠근다. 차단기 충돌체는 문이 충분히 열렸을 때 Update 가 켠다.
            Locked = !open;
            feedback = open ? "열쇠로 분전반을 열었습니다 · 차단기 위에 이름이 표시됩니다" : "분전반을 닫았습니다";
            return true;
        }

        // ── 열쇠(R) ──────────────────────────────────────────────────────────

        /// <summary>The board key works only on a shut door, and only in hand-worked play (simple controls take it as used by the E press).</summary>
        public string SecondaryPrompt => Door == null || !Door.IsShut || SimpleControls() ? "" : Locked ? "열쇠로 잠금 해제" : "열쇠로 잠그기";

        public bool TrySecondary(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (SecondaryPrompt.Length == 0) return false;
            Locked = !Locked;
            feedback = Locked ? "열쇠로 분전반 문을 잠갔습니다" : "열쇠로 분전반 문의 잠금을 풀었습니다";
            return true;
        }

        // ── 손 조작 ──────────────────────────────────────────────────────────

        public HoldStyle HoldStyle => HoldStyle.Swing;

        /// <summary>The door can be swung when it is unlocked and no fire holds it shut (otherwise <see cref="CanInteract"/> gives the reason).</summary>
        public bool Holdable(FirstPersonResponder responder) => Door != null && responder != null && !responder.IsPaused && !Locked && !FireBlocks();

        public void BeginHold(FirstPersonResponder responder)
        {
            if (Door == null) return;
            // 스스로 여닫던 중이어도 손이 잡은 자리에서 멈춘다.
            Door.SetOpening(Door.Opening);
            stateAtBegin = DoorState();
            var leaf = Door.GetComponentInChildren<Collider>();
            if (leaf is BoxCollider box) leafCentre = Door.transform.InverseTransformPoint(box.transform.TransformPoint(box.center));
            else if (leaf != null) leafCentre = Door.transform.InverseTransformPoint(leaf.bounds.center);
            else leafCentre = Vector3.right * .2f;
        }

        public void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds)
        {
            if (Door == null || responder == null || responder.PlayerCamera == null) return;
            // 문짝이 지나는 방향을 시점 좌표로 바꾸어, 문짝을 끄는 쪽으로 마우스를 움직이면 그만큼 열린다(밀면 앞으로, 당기면 뒤로).
            var pivot = Door.transform;
            var direction = HandGesture.SwingDirection(pivot.parent, pivot.localRotation * leafCentre, Mathf.Sign(Door.OpenDegrees));
            float drag = HandGesture.Along(responder.PlayerCamera.transform, direction, mouse);
            if (drag != 0f) Door.SetOpening(Door.Opening + drag / DragDegreesForFullSwing);
        }

        public string EndHold(FirstPersonResponder responder)
        {
            if (Door == null) return null;
            int now = DoorState();
            if (now == stateAtBegin) return null;
            return now == 2 ? "분전반 문을 열었습니다 · 차단기 위에 이름이 표시됩니다" : now == 0 ? "분전반 문을 닫았습니다" : "분전반 문이 반쯤 열려 있습니다";
        }

        /// <summary>How far the door has swung (0..1).</summary>
        public float HoldProgress => Door != null ? Door.Opening : -1f;

        private int DoorState() => Door.IsOpen ? 2 : Door.IsShut ? 0 : 1;

        public string Observe(FirstPersonResponder responder)
        {
            if (Door == null) return DisplayName;
            string door = Door.IsOpen ? "문이 열려 있고 차단기가 보임" : !Door.IsShut ? "문이 조금 열려 있음" : Locked ? "문이 닫혀 잠겨 있음" : "문이 닫혀 있고 잠금이 풀려 있음";
            // 불꽃이 큰 분전반은 문틈으로 새어 나오는 것이 보인다.
            if (FireBlocks()) door += " · 문틈으로 연기와 불꽃이 새어 나옴";
            return DisplayName + " · " + door;
        }

        private void Update()
        {
            if (Door == null) { awaySince = -1; return; }
            // 문이 충분히 열린 동안만 차단기를 겨냥할 수 있다.
            bool open = Door.IsOpen;
            if (open != breakersLive) SetBreakers(open);
            if (Door.IsShut && Locked) { awaySince = -1; return; }
            var session = EmergencySession.Current;
            if (session == null || session.Player == null) return;
            if ((session.Player.transform.position - transform.position).sqrMagnitude < WalkAwayMetres * WalkAwayMetres) { awaySince = -1; return; }
            if (awaySince < 0) awaySince = Time.time;
            if (Time.time - awaySince < WalkAwaySeconds) return;
            // 자리를 떠나면 문을 닫고 다시 잠근다.
            Door.SetOpen(false);
            Locked = true;
            awaySince = -1;
        }
    }
}
