using System;
using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// A gas shut-off valve on a shop's pipe (a <see cref="StationEquipment"/> of kind <see cref="Kind"/>): the intermediate valve
    /// (중간밸브) next to a range, which is what a staff member turns to stop a leak downstream of it, or the main valve on the
    /// supply just above the meter. The handle (child "Lever") lies along the pipe while the valve is open and turns a quarter
    /// turn to stand across it when closed (도시가스사업법 시행규칙 별표 7: 가스사용시설의 중간밸브). The staff member can close it; only the
    /// gas company opens it again, after the repair. Placement data: <c>shop</c>, <c>role</c> = <c>main</c>|<c>intermediate</c>,
    /// <c>feeds</c> = the id of the range an intermediate valve serves.
    /// By hand (E held) the handle follows the mouse drag or the wheel through its quarter turn: a valve turned part way is still open,
    /// nothing is announced and the leak goes on; it counts as closed only when the handle stands across the pipe. A single E press
    /// (simple controls) closes it at once.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class GasValvePoint : MonoBehaviour, IFpsInteraction, IFpsNamed, IFpsStated, IFpsHoldInteraction, IFpsObservable
    {
        public const string Kind = "gas_valve";
        private const float TurnSeconds = .45f;
        // 손맛 수치(튜닝 대상): 시선 1도의 끌기에 1/30, 휠 한 칸에 1/6 만큼 돈다. 0.02 이하면 끝까지 돌아 딸깍 걸린다.
        private const float DragPerDegree = 1f / 30f, WheelNotch = 1f / 6f, SnapClosed = .02f, LooseOpen = .98f;

        public StationEquipment Equipment { get; private set; }
        public string Shop { get; private set; } = "";
        public bool Main { get; private set; }
        /// <summary>Id of the range this intermediate valve serves (empty for the main valve).</summary>
        public string Feeds { get; private set; } = "";
        public bool Closed { get; private set; }
        /// <summary>How far the valve stands open: 1 the handle along the pipe, 0 across it (closed). A valve between is still open.</summary>
        public float Openness => openness;
        /// <summary>Someone closed it, and who ("역무원", "도시가스 안전점검원", "점포 직원").</summary>
        public event Action<GasValvePoint, string> Closing;

        private Transform lever;
        private float openness = 1f, goal = 1f;
        private bool announcedClosing;

        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Shop = Equipment.Text("shop");
            Main = Equipment.Text("role") == "main";
            Feeds = Equipment.Text("feeds");
            lever = transform.Find("Lever");
            enabled = false;
        }

        public string DisplayName => Equipment.Label;
        public string StateText => Closed ? "잠김" : openness < LooseOpen ? "반쯤 잠김" : "열림";
        public string InteractionPrompt => Closed ? "" : Main ? "가스 메인밸브 잠그기" : "가스 중간밸브 잠그기";

        public bool CanInteract(FirstPersonResponder responder, out string reason)
        {
            bool available = !Closed && responder != null && !responder.IsPaused;
            reason = available ? null : "";
            return available;
        }

        public bool TryInteract(FirstPersonResponder responder, out string feedback)
        {
            feedback = null;
            if (!CanInteract(responder, out _)) return false;
            Close("역무원");
            feedback = (Main ? "가스 메인밸브" : "가스 중간밸브") + "를 잠갔습니다 · 손잡이가 배관과 직각입니다";
            return true;
        }

        /// <summary>Turns the handle across the pipe and tells the listeners (the leaks it stops end, a fed fire loses its feed).</summary>
        public void Close(string by)
        {
            if (Closed) return;
            Closed = true;
            goal = 0f;
            Equipment.State = "잠김";
            enabled = true;
            Closing?.Invoke(this, by);
        }

        /// <summary>The gas company opens it again after the repair (the handle back along the pipe).</summary>
        public void Open()
        {
            if (!Closed) return;
            Closed = false;
            goal = 1f;
            Equipment.State = "열림";
            enabled = true;
        }

        // ── 손 조작 ──────────────────────────────────────────────────────────

        public HoldStyle HoldStyle => HoldStyle.Turn;

        /// <summary>Once shut it is not turned back by the staff member: only the gas company opens it.</summary>
        public bool Holdable(FirstPersonResponder responder) => CanInteract(responder, out _);

        public void BeginHold(FirstPersonResponder responder)
        {
            // 자동으로 돌던 중이어도(재개방 등) 손이 잡은 자리에서 멈춘다.
            goal = openness;
            announcedClosing = false;
        }

        public void Hold(FirstPersonResponder responder, Vector2 mouse, float wheel, float deltaSeconds)
        {
            if (Closed) return;
            // 오른쪽·아래로 끌면 잠기는 쪽, 반대로 끌면 다시 열리는 쪽. 휠은 위(+)가 여는 쪽이다.
            float toward = (mouse.x - mouse.y) * DragPerDegree - wheel * WheelNotch;
            if (toward == 0f) return;
            openness = Mathf.Clamp01(openness - toward);
            goal = openness;
            if (openness <= SnapClosed)
            {
                openness = 0f;
                goal = 0f;
                ApplyLever();
                announcedClosing = true;
                Close("역무원");
                responder?.ShowFeedback(Describe());
                return;
            }
            ApplyLever();
        }

        public string EndHold(FirstPersonResponder responder)
        {
            // 닫히는 순간 이미 알렸다.
            if (announcedClosing) return null;
            return Describe();
        }

        /// <summary>Progress toward shut (0 open … 1 across the pipe).</summary>
        public float HoldProgress => 1f - openness;

        private string Describe() =>
            (Main ? "가스 메인밸브" : "가스 중간밸브") + " · " + (Closed ? "손잡이가 배관과 직각(잠김)" : openness < LooseOpen ? "손잡이가 비스듬히 돌아가 있음" : "손잡이가 배관과 나란함(열림)");

        public string Observe(FirstPersonResponder responder) =>
            Equipment.Label + " · 손잡이가 배관과 " + (Closed || openness <= SnapClosed ? "직각" : openness < LooseOpen ? "비스듬함" : "나란함");

        private void ApplyLever()
        {
            if (lever != null) lever.localRotation = Quaternion.Euler(0, 90f * (1f - openness), 0);
        }

        private void Update()
        {
            openness = Mathf.MoveTowards(openness, goal, Time.deltaTime / TurnSeconds);
            ApplyLever();
            if (Mathf.Approximately(openness, goal)) enabled = false;
        }
    }
}
