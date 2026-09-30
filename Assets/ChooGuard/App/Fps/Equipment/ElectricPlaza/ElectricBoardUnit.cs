using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The behaviour of a placed distribution board: staff open the locked door with the board key and close it again, and
    /// while it stands open the eight breakers behind it can be aimed at (<see cref="BreakerSwitch"/>). The door leaf carries
    /// the collider this component answers for; the breaker colliders exist only while the door is open, so a closed
    /// board offers nothing but its door. A board that is smoking or burning cannot be opened: opening a live panel with an
    /// arcing fault is how arc-flash injuries happen. The shift closes the door when the staff member walks away.
    /// </summary>
    public sealed class ElectricBoardUnit : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        private const float WalkAwayMetres = 7f, WalkAwaySeconds = 4f;

        [Tooltip("The hinged door leaf on its pivot.")]
        public BoardDoor Door;
        [Tooltip("The eight breakers, slot 0 (main) first.")]
        public BreakerSwitch[] Switches = Array.Empty<BreakerSwitch>();

        /// <summary>Why the door cannot be opened now (a fire in it), or null. Set by the incident director.</summary>
        public Func<string> Blocked;

        private StationEquipment equipment;
        private float awaySince = -1;

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
            foreach (var breaker in Switches)
                foreach (var collider in breaker.GetComponents<Collider>()) collider.enabled = active;
        }

        public string DisplayName => equipment != null ? equipment.Label + (Door != null && Door.IsOpen ? " · 문 열림" : "") : "분전반";
        public string InteractionPrompt => Door == null ? "" : Door.IsOpen ? "분전반 닫기" : "분전반 열기 (열쇠)";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            reason = null;
            if (Door == null || responder == null || responder.IsPaused) { reason = ""; return false; }
            if (!Door.IsOpen && Blocked?.Invoke() is string why && why.Length > 0) { reason = why; return false; }
            return true;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out feedback)) return false;
            bool open = !Door.IsOpen;
            Door.SetOpen(open);
            SetBreakers(open);
            feedback = open ? "열쇠로 분전반을 열었습니다 · 차단기 위에 이름이 표시됩니다" : "분전반을 닫았습니다";
            return true;
        }

        private void Update()
        {
            if (Door == null || !Door.IsOpen) { awaySince = -1; return; }
            var session = EmergencySession.Current;
            if (session == null || session.Player == null) return;
            if ((session.Player.transform.position - transform.position).sqrMagnitude < WalkAwayMetres * WalkAwayMetres) { awaySince = -1; return; }
            if (awaySince < 0) awaySince = Time.time;
            if (Time.time - awaySince < WalkAwaySeconds) return;
            Door.SetOpen(false);
            SetBreakers(false);
            awaySince = -1;
        }
    }
}
