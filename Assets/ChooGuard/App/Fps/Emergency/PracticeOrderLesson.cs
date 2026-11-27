using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>What the staff member did, as the order lessons hear it. Every kind is raised from the one place the game already funnels it through.</summary>
    public enum PracticeSignal
    {
        /// <summary>E or R succeeded; the label is the prompt the player read (FirstPersonResponder.InteractionPerformed).</summary>
        Interact,
        /// <summary>역무실 보고 (IncidentDirector.Report / ReportSlots); the label is the hazard label.</summary>
        Report,
        /// <summary>Q 요청·기관; the label is the request name ("119 구급", "112", "승강기 담당", …).</summary>
        Request,
        /// <summary>Q 요청·설비; the label is the measure name ("에스컬레이터 정지", "수신기 복구", …).</summary>
        Measure,
        /// <summary>Q 방송; the label is the scope name ("주변 통로 비우기", "구역 대피", "역 전체 대피", "침착 안내").</summary>
        Broadcast,
        /// <summary>The reviewed handover to the commanding team leader went through.</summary>
        Handover,
    }

    /// <summary>One kind of order mistake in a practice lesson: deducted once, counted every time.</summary>
    public sealed class PracticeOrderMistake
    {
        public string Key, Label;
        public int Penalty, Count;
    }

    /// <summary>
    /// Order-score keeper for a practice scenario (docs/practice-lessons-plan.md). Like FireTutorialLesson it stages nothing and
    /// never blocks an action: it hears <see cref="PracticeSignal"/>s, ticks the steps of the scenario's table in
    /// docs/emergency-lesson-steps.md that the game can already see, and deducts the rule mistakes once per kind (100점에서
    /// 시작, 인계 뒤에는 고정). A subclass is one scenario: it lists its steps with the table's numbers and its rules with their
    /// evidence. Steps marked (신규) in the table are left out because no signal exists for them yet.
    /// </summary>
    public abstract class PracticeOrderLesson
    {
        public const int FullScore = 100;
        public const int DefaultPenalty = 10;

        private sealed class StepEntry
        {
            public string No, Name;
            public PracticeSignal Kind;
            public string[] Labels;
            public int DoneAt;
            public bool Matches(PracticeSignal kind, string label) =>
                kind == Kind && (Labels.Length == 0 || Array.IndexOf(Labels, label) >= 0);
        }

        private enum RuleKind { Before, Forbid, Require }

        private sealed class Rule
        {
            public RuleKind Kind;
            public string Key, Label, First, Then;
            public int Penalty;
            public PracticeSignal Signal;
            public string[] Labels;
        }

        private readonly List<StepEntry> steps = new List<StepEntry>();
        private readonly List<Rule> rules = new List<Rule>();
        private int done;

        public EmergencySession Session { get; }
        /// <summary>The section of docs/emergency-lesson-steps.md this lesson follows, e.g. "3-7".</summary>
        public abstract string Section { get; }
        public abstract string Title { get; }
        public bool HandedOver { get; private set; }
        public readonly List<PracticeOrderMistake> Mistakes = new List<PracticeOrderMistake>();
        public int OrderScore => Mathf.Max(0, FullScore - Mistakes.Sum(mistake => mistake.Penalty));

        protected PracticeOrderLesson(EmergencySession session) { Session = session; }

        /// <summary>Called once after construction (the subclass has declared its steps and rules by then).</summary>
        public PracticeOrderLesson Start()
        {
            Note("start " + Section + " · steps=" + steps.Count + " rules=" + rules.Count);
            return this;
        }

        // ── 선언 ─────────────────────────────────────────────────────────────

        /// <summary>A step of the table, done when a matching signal arrives. No labels: any label of that kind.</summary>
        protected void Step(string no, string name, PracticeSignal kind, params string[] labels)
        {
            if (steps.Any(step => step.No == no && step.Kind == kind)) throw new ArgumentException("단계 번호와 신호가 겹칩니다: " + no);
            steps.Add(new StepEntry { No = no, Name = name, Kind = kind, Labels = labels ?? Array.Empty<string>() });
        }

        /// <summary>Doing step <paramref name="then"/> while step <paramref name="first"/> is not done yet is a mistake.</summary>
        protected void Before(string key, string label, string first, string then, int penalty = DefaultPenalty)
        {
            RequireStep(first); RequireStep(then);
            rules.Add(new Rule { Kind = RuleKind.Before, Key = key, Label = label, First = first, Then = then, Penalty = penalty });
        }

        /// <summary>Any matching signal is a mistake (no labels: any label of that kind).</summary>
        protected void Forbid(string key, string label, PracticeSignal kind, string[] labels, int penalty = DefaultPenalty) =>
            rules.Add(new Rule { Kind = RuleKind.Forbid, Key = key, Label = label, Signal = kind, Labels = labels ?? Array.Empty<string>(), Penalty = penalty });

        /// <summary>Handing over while step <paramref name="step"/> was never done is a mistake.</summary>
        protected void Require(string key, string label, string step, int penalty = DefaultPenalty)
        {
            RequireStep(step);
            rules.Add(new Rule { Kind = RuleKind.Require, Key = key, Label = label, First = step, Penalty = penalty });
        }

        private void RequireStep(string no)
        {
            if (!steps.Any(step => step.No == no)) throw new ArgumentException("규칙이 없는 단계를 가리킵니다: " + no);
        }

        // ── 진행 ─────────────────────────────────────────────────────────────

        /// <summary>The step <paramref name="no"/> is done (any of its signals arrived).</summary>
        public bool IsDone(string no) => steps.Any(step => step.No == no && step.DoneAt > 0);

        public IEnumerable<string> StepNumbers => steps.Select(step => step.No).Distinct();

        /// <summary>The declared steps in table order, one entry per signal (a step number can listen to more than one). The real-scene walkthrough tests do these.</summary>
        public IEnumerable<(string No, string Name, PracticeSignal Kind, string[] Labels)> DeclaredSteps =>
            steps.Select(step => (step.No, step.Name, step.Kind, (string[])step.Labels.Clone()));

        public void Signal(PracticeSignal kind, string label)
        {
            label = label ?? "";
            if (HandedOver) return;
            if (kind == PracticeSignal.Handover)
                foreach (var rule in rules)
                    if (rule.Kind == RuleKind.Require && !IsDone(rule.First)) Record(rule);
            foreach (var rule in rules)
                if (rule.Kind == RuleKind.Forbid && rule.Signal == kind && (rule.Labels.Length == 0 || Array.IndexOf(rule.Labels, label) >= 0))
                    Record(rule);
            foreach (var step in steps)
            {
                if (step.DoneAt > 0 || !step.Matches(kind, label)) continue;
                bool firstOfNumber = !IsDone(step.No);
                if (firstOfNumber)
                    foreach (var rule in rules)
                        if (rule.Kind == RuleKind.Before && rule.Then == step.No && !IsDone(rule.First)) Record(rule);
                step.DoneAt = ++done;
                if (firstOfNumber) Note("step " + step.No + " · " + step.Name, step.No + " · " + step.Name);
            }
            if (kind == PracticeSignal.Handover)
            {
                HandedOver = true;
                Note("handed-over; score=" + OrderScore);
            }
        }

        private void Record(Rule rule)
        {
            if (HandedOver) return;
            var mistake = Mistakes.Find(entry => entry.Key == rule.Key);
            if (mistake != null) { mistake.Count++; Note("order-mistake-repeat:" + rule.Key + "; count=" + mistake.Count); return; }
            Mistakes.Add(new PracticeOrderMistake { Key = rule.Key, Label = rule.Label, Penalty = rule.Penalty, Count = 1 });
            Note("order-mistake:" + rule.Key + "; penalty=" + rule.Penalty + "; score=" + OrderScore, rule.Label + " · " + rule.Penalty + "점 감점");
            Session?.Hud?.Toast("순서 감점 −" + rule.Penalty + "점 · " + rule.Label, 4f);
        }

        private void Note(string fact, string visibleRecord = null)
        {
            Debug.Log("CG_PRACTICE_LESSON " + Section + " " + fact);
            if (!string.IsNullOrEmpty(visibleRecord)) Session?.Log?.Add(Title + " 연습 · " + visibleRecord);
        }

        /// <summary>결과 화면(ShiftResult.Show)에 덧붙이는 순서 점수, 감점 내역, 한 단계 목록.</summary>
        public string ScoreReport()
        {
            var text = new StringBuilder();
            text.Append("<size=22>순서 점수 <b>").Append(OrderScore).Append("</b> / ").Append(FullScore).Append("</size>\n\n");
            if (Mistakes.Count == 0) text.Append("순서를 어긴 조작이 없습니다.\n");
            else
            {
                text.Append("감점 내역\n");
                foreach (var mistake in Mistakes)
                {
                    text.Append("−").Append(mistake.Penalty).Append(" · ").Append(mistake.Label);
                    if (mistake.Count > 1) text.Append(" (").Append(mistake.Count).Append("회, 감점은 1회)");
                    text.Append('\n');
                }
            }
            // 결과 화면의 수치 칸은 좁아서 한 단계 목록은 한 문단으로 이어 쓴다.
            text.Append("\n<size=15>한 단계 ");
            text.Append(string.Join("  ", StepNumbers.Select(no => (IsDone(no) ? "●" : "○") + no + " " + steps.First(entry => entry.No == no).Name)));
            text.Append("</size>\n\n<size=14>같은 실수는 한 번만 감점합니다. 교육용 가중치이며 공식 평가 기준이 아닙니다. 게임에 아직 없는 단계는 목록에서 뺐습니다.</size>");
            return text.ToString();
        }
    }
}
