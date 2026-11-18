using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>One kind of out-of-order action in the fire lesson: deducted once, counted every time (docs/fire-tutorial.md).</summary>
    public sealed class FireOrderMistake
    {
        public string Key, Label;
        public int Penalty, Count;
    }

    /// <summary>
    /// Order-score keeper for the fixed "맞이방 화재 대응" practice (Issue333 후속; docs/fire-tutorial.md "1-1"). Unlike
    /// CardiacTutorialLesson it stages nothing of its own: IncidentDirector.Practice.cs already draws a real bin/power-bank
    /// fire in the 2층 맞이방 for the "화재" practice family, and every verb this class scores — 발신기, 역무실 보고(119 포함),
    /// 소화기, 대피방송, 수신기 복구, 현장 인계 — is the same free-roam interaction a real shift already has
    /// (StaffTools.Extinguisher, IncidentDirector.Fire/Radio/Workflow.cs). This class only watches the order they happen in,
    /// the way CardiacTutorialScore watches the cardiac lesson, and never blocks an action itself.
    /// 1-2(소화전 사용)·1-3(전기설비 화재)·1-4(주방 기름·가스 화재) 분기와 2(외치기)·6(진화 판단 표시)·10(중단 판정)·
    /// 13(잔류자 확인) 단계는 게임에 아직 없는 조작이라 범위 밖이다. 추가하려면 이 파일에 OnX 메서드를 늘리고 해당 실제
    /// 조작이 생긴 자리에서 호출한다.
    /// </summary>
    public sealed class FireTutorialLesson
    {
        public const int FullScore = 100;
        public EmergencySession Session { get; }

        public bool CallPointPressed { get; private set; }
        public bool Reported { get; private set; }
        public bool ExtinguisherPicked { get; private set; }
        public bool PinPulled { get; private set; }
        public bool Sprayed { get; private set; }
        public bool Extinguished { get; private set; }
        public bool ExtinguishReported { get; private set; }
        public bool EvacuationAnnounced { get; private set; }
        public bool ReceiverReset { get; private set; }
        public bool HandedOver { get; private set; }

        /// <summary>The kinds this lesson scores, with their label and penalty (교육용 가중치, 공식 평가 기준 아님).</summary>
        public static class Mistake
        {
            public static readonly (string Key, string Label, int Penalty) ExtinguisherBeforeReport = ("extinguisher-before-report", "역무실 보고 전에 소화기 사용", 10);
            public static readonly (string Key, string Label, int Penalty) EvacuationBeforeAttempt = ("evacuation-before-attempt", "진화 시도 전에 대피방송", 10);
            public static readonly (string Key, string Label, int Penalty) ReceiverResetBeforeHandover = ("receiver-reset-before-handover", "소방대 인계 전에 수신기 복구", 10);
        }

        public readonly List<FireOrderMistake> Mistakes = new List<FireOrderMistake>();
        public int OrderScore => Mathf.Max(0, FullScore - Mistakes.Sum(mistake => mistake.Penalty));

        public FireTutorialLesson(EmergencySession session)
        {
            Session = session;
            Note("fire-lesson-start");
        }

        void Note(string fact, string visibleRecord = null)
        {
            Debug.Log("CG_FIRE_LESSON " + fact);
            if (!string.IsNullOrEmpty(visibleRecord)) Session?.Log?.Add("화재 연습 · " + visibleRecord);
        }

        public void RecordOrderMistake((string Key, string Label, int Penalty) kind)
        {
            if (HandedOver) return;
            var mistake = Mistakes.Find(entry => entry.Key == kind.Key);
            if (mistake != null) { mistake.Count++; Note("order-mistake-repeat:" + kind.Key + "; count=" + mistake.Count); return; }
            Mistakes.Add(new FireOrderMistake { Key = kind.Key, Label = kind.Label, Penalty = kind.Penalty, Count = 1 });
            Note("order-mistake:" + kind.Key + "; penalty=" + kind.Penalty + "; score=" + OrderScore, kind.Label + " · " + kind.Penalty + "점 감점");
            Session?.Hud?.Toast("순서 감점 −" + kind.Penalty + "점 · " + kind.Label, 4f);
        }

        /// <summary>단계 3 · 발신기(비상벨).</summary>
        public void OnCallPoint()
        {
            if (CallPointPressed) return;
            CallPointPressed = true;
            Note("call-point", "발신기 눌러 비상벨 울림");
        }

        /// <summary>단계 4·5 · 역무실 보고(화재 보고는 119 소방 출동을 함께 부른다).</summary>
        public void OnReported(FireHazard hazard)
        {
            if (Reported) return;
            Reported = true;
            Note("reported", "역무실 보고(119 신고 포함) · " + hazard.Where);
        }

        /// <summary>단계 7 · 소화기 들기.</summary>
        public void OnExtinguisherPicked()
        {
            if (ExtinguisherPicked) return;
            ExtinguisherPicked = true;
            if (!Reported) RecordOrderMistake(Mistake.ExtinguisherBeforeReport);
            Note("extinguisher-picked", "소화기 들기");
        }

        /// <summary>단계 8 · 안전핀. 분사는 안전핀이 빠지기 전엔 StaffTools 자체가 막아 두므로 여기서 추가로 거절하지 않는다.</summary>
        public void OnPinPulled()
        {
            if (PinPulled) return;
            PinPulled = true;
            Note("pin-pulled", "안전핀 뽑음");
        }

        /// <summary>단계 9 · 분사.</summary>
        public void OnSpraying()
        {
            if (Sprayed) return;
            Sprayed = true;
            Note("spraying", "분사 시작");
        }

        /// <summary>진화 성공(화재가 완전히 꺼짐).</summary>
        public void OnExtinguished()
        {
            if (Extinguished) return;
            Extinguished = true;
            Note("extinguished", "초기 진화 성공");
        }

        /// <summary>단계 14 · 진화 보고.</summary>
        public void OnExtinguishReported()
        {
            if (ExtinguishReported) return;
            ExtinguishReported = true;
            Note("extinguish-reported", "초기 진화 완료 보고");
        }

        /// <summary>단계 11 · 대피방송. 이 연습의 작은 불은 손 소화기로 진화되는 것을 기대하므로, 진화를 시도하기도 전의 대피방송은 과잉 대응으로 감점한다.</summary>
        public void OnEvacuationAnnounced()
        {
            if (EvacuationAnnounced) return;
            EvacuationAnnounced = true;
            if (!PinPulled && !Sprayed) RecordOrderMistake(Mistake.EvacuationBeforeAttempt);
            Note("evacuation-announced", "대피방송");
        }

        /// <summary>단계 16 · 수신기 복구. 공식 절차는 소방대 인계로 불이 꺼진 것이 확실해진 뒤에 복구하라고 명시한다.</summary>
        public void OnReceiverResetRequested()
        {
            if (ReceiverReset) return;
            ReceiverReset = true;
            if (!HandedOver) RecordOrderMistake(Mistake.ReceiverResetBeforeHandover);
            Note("receiver-reset", "수신기 복구 요청");
        }

        /// <summary>단계 15 · 소방대 현장 인계.</summary>
        public void OnHandedOver()
        {
            if (HandedOver) return;
            HandedOver = true;
            Note("handed-over", "소방대 현장 인계");
        }

        /// <summary>결과 화면에 덧붙이는 순서 점수와 감점 내역(ShiftLog.cs의 ShiftResult.Show 참고).</summary>
        public string ScoreReport()
        {
            var text = new StringBuilder();
            text.Append("<size=22>순서 점수 <b>").Append(OrderScore).Append("</b> / ").Append(FullScore).Append("</size>\n\n");
            if (Mistakes.Count == 0) { text.Append("순서를 어긴 조작이 없습니다."); return text.ToString(); }
            text.Append("감점 내역\n");
            foreach (var mistake in Mistakes)
            {
                text.Append("−").Append(mistake.Penalty).Append(" · ").Append(mistake.Label);
                if (mistake.Count > 1) text.Append(" (").Append(mistake.Count).Append("회, 감점은 1회)");
                text.Append('\n');
            }
            text.Append("\n<size=14>같은 실수는 한 번만 감점합니다. 교육용 가중치이며 공식 평가 기준이 아닙니다.</size>");
            return text.ToString();
        }
    }
}
