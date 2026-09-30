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
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class GasValvePoint : MonoBehaviour, IFpsInteraction, IFpsNamed
    {
        public const string Kind = "gas_valve";
        private const float TurnSeconds = .45f;

        public StationEquipment Equipment { get; private set; }
        public string Shop { get; private set; } = "";
        public bool Main { get; private set; }
        /// <summary>Id of the range this intermediate valve serves (empty for the main valve).</summary>
        public string Feeds { get; private set; } = "";
        public bool Closed { get; private set; }
        /// <summary>Someone closed it, and who ("역무원", "도시가스 안전점검원", "점포 직원").</summary>
        public event Action<GasValvePoint, string> Closing;

        private Transform lever;
        private float turn;

        public void Bind()
        {
            Equipment = GetComponent<StationEquipment>();
            Shop = Equipment.Text("shop");
            Main = Equipment.Text("role") == "main";
            Feeds = Equipment.Text("feeds");
            lever = transform.Find("Lever");
            enabled = false;
        }

        public string DisplayName => Equipment.Label + (Closed ? " · 잠김" : "");
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
            Equipment.State = "잠김";
            enabled = true;
            Closing?.Invoke(this, by);
        }

        /// <summary>The gas company opens it again after the repair (the handle back along the pipe).</summary>
        public void Open()
        {
            if (!Closed) return;
            Closed = false;
            Equipment.State = "열림";
            enabled = true;
        }

        private void Update()
        {
            turn = Mathf.MoveTowards(turn, Closed ? 1f : 0f, Time.deltaTime / TurnSeconds);
            if (lever != null) lever.localRotation = Quaternion.Euler(0, 90f * turn, 0);
            if (Mathf.Approximately(turn, Closed ? 1f : 0f)) enabled = false;
        }
    }
}
