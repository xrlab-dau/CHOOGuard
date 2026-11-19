using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>One kind of out-of-order (or unsafe) action in the cardiac lesson: deducted once, counted every time.</summary>
    public sealed class CardiacOrderMistake
    {
        public string Key, Label;
        public int Penalty, Count;
        public CardiacLessonStep Step;
    }

    /// <summary>
    /// The lesson's order score (2026-11-18): it starts at <see cref="FullScore"/> and every kind of action taken ahead of the
    /// lesson's order — or an unsafe one, such as touching the patient while the AED analyses — takes its penalty off once.
    /// Repeats of the same kind are counted for the result screen but not deducted again, so pressing a refused key twice
    /// does not empty the score. The gates that refuse these actions already existed; this only records them.
    /// </summary>
    public sealed partial class CardiacTutorialLesson
    {
        public const int FullScore = 100;

        /// <summary>The kinds the lesson scores, with their label and penalty.</summary>
        public static class Mistake
        {
            public static readonly (string Key, string Label, int Penalty) HelpBeforeResponse = ("help-before-response", "반응 확인 전에 119 요청", 10);
            public static readonly (string Key, string Label, int Penalty) BreathingBeforeHelp = ("breathing-before-help", "119 요청 전에 호흡 확인", 10);
            public static readonly (string Key, string Label, int Penalty) CompressBeforeBreathing = ("compress-before-breathing", "호흡 확인 전에 가슴압박", 10);
            public static readonly (string Key, string Label, int Penalty) ReliefBeforePractice = ("relief-before-practice", "직접 압박 전에 동료 교대 요청", 5);
            public static readonly (string Key, string Label, int Penalty) AedBeforeRelief = ("aed-before-relief", "압박 교대 전에 환자를 떠나 AED 꺼내기", 10);
            public static readonly (string Key, string Label, int Penalty) ContactDuringAnalysis = ("contact-during-analysis", "AED 분석·충전 중 환자 접촉(압박)", 15);
            public static readonly (string Key, string Label, int Penalty) AedLiftedInUse = ("aed-lifted-in-use", "AED 사용 중 본체를 들어 전원 끄기", 10);
        }

        public readonly List<CardiacOrderMistake> Mistakes = new List<CardiacOrderMistake>();
        public int OrderScore => Mathf.Max(0, FullScore - Mistakes.Sum(mistake => mistake.Penalty));

        public void RecordOrderMistake((string Key, string Label, int Penalty) kind)
        {
            if (!Ready || Finished || stopping) return;
            var mistake = Mistakes.Find(entry => entry.Key == kind.Key);
            if (mistake != null) { mistake.Count++; Note("order-mistake-repeat:" + kind.Key + "; count=" + mistake.Count); return; }
            Mistakes.Add(new CardiacOrderMistake { Key = kind.Key, Label = kind.Label, Penalty = kind.Penalty, Count = 1, Step = Step });
            Note("order-mistake:" + kind.Key + "; penalty=" + kind.Penalty + "; score=" + OrderScore, kind.Label + " · " + kind.Penalty + "점 감점");
            Session?.Hud?.Toast("순서 감점 −" + kind.Penalty + "점 · " + kind.Label, 4f);
        }

        /// <summary>E refused by the station's gates: only the cabinet's "relief first" refusal is an order mistake.</summary>
        void OnInteractionRefused(IFpsInteraction target, string reason)
        {
            if (target is AedCabinet cabinet && cabinet == Cabinet && cabinet.Stored && !MayFetchAed) RecordOrderMistake(Mistake.AedBeforeRelief);
        }

        /// <summary>Score and deductions for the result screen.</summary>
        public string ScoreReport()
        {
            var text = new StringBuilder();
            text.Append("<size=26>순서 점수 <b>").Append(OrderScore).Append("</b> / ").Append(FullScore).Append("</size>\n\n");
            if (Mistakes.Count == 0) { text.Append("순서를 어긴 조작이 없습니다."); return text.ToString(); }
            text.Append("감점 내역\n");
            foreach (var mistake in Mistakes)
            {
                text.Append("−").Append(mistake.Penalty).Append(" · ").Append(mistake.Label);
                if (mistake.Count > 1) text.Append(" (").Append(mistake.Count).Append("회, 감점은 1회)");
                text.Append('\n');
            }
            text.Append("\n<size=16>같은 실수는 한 번만 감점합니다. 막힌 조작은 화면의 현재 목표 순서대로 다시 하세요.</size>");
            return text.ToString();
        }
    }
}
