using static ChooGuard.App.Fps.Emergency.PracticeLabel;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// 2-1 방치 가방·의심 물체. 화재경보를 울리지 않고(S1, S2) 대피는 역무실(대테러책임자) 판단 뒤에 방송한다(S2, S3).
    /// 주인 찾기·무전 주의·대피 승인 요청은 (신규)라 뺐다.
    /// </summary>
    public sealed class LeftBagLesson : PracticeOrderLesson
    {
        public override string Section => "2-1";
        public override string Title => "방치 가방";
        public LeftBagLesson(EmergencySession session) : base(session)
        {
            Step("3", "주변 알림", PracticeSignal.Interact, GuideEvacuation);
            Step("4", "역무실 보고", PracticeSignal.Report);
            Step("5", "112·철도경찰 신고", PracticeSignal.Request, Police112, RailwayPolice);
            Step("6", "접근 통제", PracticeSignal.Interact, Cordon);
            Step("9", "대피 방송", PracticeSignal.Broadcast, EvacuateArea, EvacuateStation);
            Step("10", "경찰 인계", PracticeSignal.Handover);
            Forbid("fire-alarm", "의심 물체에 화재경보(발신기)를 울림", PracticeSignal.Interact, new[] { CallPoint });
            Before("evacuate-before-report", "역무실 보고 전에 대피 방송", "4", "9");
        }
    }

    /// <summary>2-2 폭발물 협박 전화. 통화 응대·확인표·점검 방송은 (신규)라 뺐다. 대피는 대테러책임자가 정한다(S2, S3).</summary>
    public sealed class BombThreatLesson : PracticeOrderLesson
    {
        public override string Section => "2-2";
        public override string Title => "폭발물 협박";
        public BombThreatLesson(EmergencySession session) : base(session)
        {
            Step("7a", "대피 판단 요청(보고)", PracticeSignal.Report);
            Step("7b", "역 전체 대피 방송", PracticeSignal.Broadcast, EvacuateStation, EvacuateArea);
            Step("9", "경찰 인계", PracticeSignal.Handover);
            Before("evacuate-before-report", "역무실 판단 요청 전에 대피 방송", "7a", "7b");
        }
    }

    /// <summary>2-3 의심 물질(흰 가루). 사람을 내보내고 접근을 막는다(S1). 덮기·접촉자 분리·세척은 (신규)라 뺐다.</summary>
    public sealed class SuspiciousSubstanceLesson : PracticeOrderLesson
    {
        public override string Section => "2-3";
        public override string Title => "의심 물질";
        public SuspiciousSubstanceLesson(EmergencySession session) : base(session)
        {
            Step("4", "현장 이탈·통제", PracticeSignal.Interact, Cordon);
            Step("6", "역무실 보고", PracticeSignal.Report);
            Step("7", "119·112 신고", PracticeSignal.Request, Fire119, Police112);
            Step("8", "대피 방송", PracticeSignal.Broadcast, EvacuateArea, EvacuateStation);
            Step("10", "화학구조대 인계", PracticeSignal.Handover);
            Require("no-cordon", "현장을 통제하지 않고 인계", "4");
            Before("evacuate-before-report", "역무실 보고 전에 대피 방송", "6", "8");
        }
    }

    /// <summary>
    /// 2-4 난동 승객. 근거(S5~S7)는 할 일을 나열하지만 앞뒤를 정하지 않아 감점 규칙은 없다. 진행한 단계만 결과에 보인다.
    /// 기록·물러나기는 (신규)라 뺐다.
    /// </summary>
    public sealed class DisturbanceLesson : PracticeOrderLesson
    {
        public override string Section => "2-4";
        public override string Title => "난동 승객";
        public DisturbanceLesson(EmergencySession session) : base(session)
        {
            Step("3", "말로 제지", PracticeSignal.Interact, CalmDown);
            Step("5", "112·철도경찰 신고", PracticeSignal.Request, Police112, RailwayPolice);
            Step("5", "112·철도경찰 신고", PracticeSignal.Report);
            Step("6", "주변 승객 보호", PracticeSignal.Broadcast, EvacuateArea, ClearAround);
            Step("8", "경찰 인계", PracticeSignal.Handover);
        }
    }
}
