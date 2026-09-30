using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The station's little electrical network of one shift: which distribution board feeds which vending machine or
    /// charging kiosk, and whether each breaker is on. The twin has no electrical drawings, so the feeding is a stated
    /// design assumption made where the plan gives no answer: a machine hangs on a branch breaker of the nearest board on
    /// its own floor that still has a free branch (KEC 232.84: every circuit of a board is labelled; a machine carries
    /// the label of its breaker). Machines take the first branches, the rest of the board is lighting, sockets and spares.
    /// A load is powered when its own switch (plug or power switch), its branch breaker and the board's main breaker are on.
    /// </summary>
    public static class ElectricNetwork
    {
        /// <summary>Branch breakers of one board (the deck has 8 slots, slot 0 is the main breaker).</summary>
        public const int Branches = BreakerDeckLayout.Slots - 1;

        /// <summary>Branches kept for lighting and sockets: machines never take the last two.</summary>
        private const int Reserved = 2;

        /// <summary>The name the staff member's own switching goes under ("by" of <see cref="Switched"/>, the hand that hangs a tag).</summary>
        public const string StaffName = "역무원";

        public sealed class Circuit
        {
            /// <summary>0 for the main breaker, 1..7 for the branches.</summary>
            public int Number;
            public Board Board;
            /// <summary>The machine on this branch, or null (main, lighting, sockets, spare).</summary>
            public StationEquipment Load;
            /// <summary>What the circuit feeds, as the label reads.</summary>
            public string Name = "";
            public bool On = true;
            /// <summary>Staff may switch it: the main breaker and the branches of machines. Lighting and sockets are the electrician's.</summary>
            public bool Operable;
            /// <summary>Lock-out tag hung on the breaker after it was switched off to isolate a fault ("작동금지 · 역무원 14:12:05"): only whoever hung it takes it off (KOSHA, 산안규칙 제319조), or null.</summary>
            public string Tag;
            /// <summary>Who hung the tag ("역무원", "전기 담당").</summary>
            public string TagBy;

            /// <summary>"LP-2F-03 3번" — the label that machine and breaker both carry.</summary>
            public string Label => Board.Code + (Number == 0 ? " 메인" : " " + Number + "번");
        }

        public sealed class Board
        {
            public StationEquipment Equipment;
            public ElectricBoardUnit Unit;
            public Circuit Main;
            public readonly List<Circuit> Branches = new List<Circuit>();
            /// <summary>The fire burnt it out: nothing can be switched on again until it is replaced.</summary>
            public bool Damaged;

            public string Name => Equipment.Label;
            /// <summary>"LP-2F-03" out of "분전반 LP-2F-03".</summary>
            public string Code => Equipment.Label.Replace("분전반 ", "");
            public IEnumerable<Circuit> All => new[] { Main }.Concat(Branches);
        }

        private static readonly List<Board> boards = new List<Board>();
        private static readonly Dictionary<StationEquipment, Circuit> feeds = new Dictionary<StationEquipment, Circuit>();

        public static IReadOnlyList<Board> Boards => boards;

        /// <summary>A breaker was switched: the circuit, its new state and who did it ("역무원", "전기 담당", "보호장치").</summary>
        public static event Action<Circuit, bool, string> Switched;

        /// <summary>The asset tag painted on a machine: VM-HALL2F-01 (vending machine), CK-HALL2F-01 (charging kiosk).</summary>
        public static string Tag(StationEquipment equipment)
        {
            string number = equipment.Id.Substring(equipment.Id.LastIndexOf('-') + 1);
            return (equipment.Kind == "charging_kiosk" ? "CK" : "VM") + "-" + equipment.Zone.ToUpperInvariant() + "-" + number;
        }

        /// <summary>Reads the placed boards and machines from the registry and hangs every machine on a board. Call once per shift after the equipment is spawned.</summary>
        public static void Build()
        {
            Clear();
            foreach (var equipment in EquipmentRegistry.OfKind("distribution_board").OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var board = new Board { Equipment = equipment, Unit = equipment.GetComponent<ElectricBoardUnit>() };
                board.Main = new Circuit { Number = 0, Board = board, Name = "분전반 전체(메인)", Operable = true };
                boards.Add(board);
            }
            var loads = EquipmentRegistry.OfKind("vending_machine").Concat(EquipmentRegistry.OfKind("charging_kiosk")).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
            foreach (var load in loads)
            {
                var board = boards.Where(b => Mathf.Abs(b.Equipment.transform.position.y - load.transform.position.y) < 3f && b.Branches.Count < Branches - Reserved)
                    .OrderBy(b => (b.Equipment.transform.position - load.transform.position).sqrMagnitude).ThenBy(b => b.Equipment.Id, StringComparer.Ordinal).FirstOrDefault();
                if (board == null) continue;
                var circuit = new Circuit { Number = board.Branches.Count + 1, Board = board, Load = load, Operable = true, Name = load.Label + " " + Tag(load) };
                board.Branches.Add(circuit);
                feeds[load] = circuit;
            }
            foreach (var board in boards)
            {
                // 남은 가지는 조명·콘센트·예비다(도면이 없어 이름만 붙인 가정).
                string[] rest = { "구역 조명", "청소·정비 콘센트", "예비", "예비", "예비" };
                for (int i = 0; board.Branches.Count < Branches; i++)
                    board.Branches.Add(new Circuit { Number = board.Branches.Count + 1, Board = board, Name = rest[Mathf.Min(i, rest.Length - 1)] });
                board.Unit?.Bind(board);
            }
            foreach (var load in loads) load.GetComponent<ElectricLoad>()?.Refresh();
        }

        public static void Clear()
        {
            boards.Clear();
            feeds.Clear();
        }

        /// <summary>The branch that feeds a machine, or null when the twin has no board on its floor.</summary>
        public static Circuit CircuitOf(StationEquipment load) => load != null && feeds.TryGetValue(load, out var circuit) ? circuit : null;

        public static Board BoardOf(StationEquipment board) => boards.Find(b => b.Equipment == board);

        /// <summary>Whether electricity reaches a machine now.</summary>
        public static bool Powered(StationEquipment load)
        {
            var own = load != null ? load.GetComponent<ElectricLoad>() : null;
            if (own != null && (own.Burnt || own.LocalOff)) return false;
            var circuit = CircuitOf(load);
            return circuit == null || circuit.On && circuit.Board.Main.On;
        }

        /// <summary>
        /// Switches a breaker. Returns false when nothing changed (already so, or a burnt-out board that cannot take power).
        /// The machines it feeds refresh at once and <see cref="Switched"/> tells the rest of the game.
        /// </summary>
        public static bool Switch(Circuit circuit, bool on, string by)
        {
            if (circuit.On == on || on && circuit.Board.Damaged) return false;
            circuit.On = on;
            circuit.Board.Unit?.Refresh();
            foreach (var affected in circuit.Number == 0 ? circuit.Board.Branches : new List<Circuit> { circuit })
                affected.Load?.GetComponent<ElectricLoad>()?.Refresh();
            Switched?.Invoke(circuit, on, by);
            return true;
        }
    }
}
