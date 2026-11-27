using static ChooGuard.App.Fps.Emergency.PracticeLabel;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>1-3 전기 설비 화재. 물·소화전을 쓰지 않고(감전, F4) 전원 차단을 요청한 뒤 분말 소화기로 끈다.</summary>
    public sealed class ElectricFireLesson : PracticeOrderLesson
    {
        public override string Section => "1-3";
        public override string Title => "전기 설비 화재";
        public ElectricFireLesson(EmergencySession session) : base(session)
        {
            Step("4", "역무실 보고", PracticeSignal.Report);
            Step("7b", "전원 차단 요청", PracticeSignal.Request, ElectricTeam);
            Step("7c", "소화기 사용", PracticeSignal.Interact, ExtinguisherPick);
            Step("15", "소방대 인계", PracticeSignal.Handover);
            Forbid("water-on-electric", "전기 화재에 소화전(물) 사용", PracticeSignal.Interact, new[] { Hydrant });
        }
    }

    /// <summary>1-4 주방 기름·가스 화재. 물을 쓰지 않고(F5) 가스를 잠근 뒤 소화기로 끈다.</summary>
    public sealed class KitchenFireLesson : PracticeOrderLesson
    {
        public override string Section => "1-4";
        public override string Title => "주방 화재";
        public KitchenFireLesson(EmergencySession session) : base(session)
        {
            Step("4", "역무실 보고", PracticeSignal.Report);
            Step("7b", "가스 중간밸브 잠그기", PracticeSignal.Interact, GasMiddleValve);
            Step("7c", "소화기 사용", PracticeSignal.Interact, ExtinguisherPick);
            Step("15", "소방대 인계", PracticeSignal.Handover);
            Forbid("water-on-oil", "기름·주방 화재에 소화전(물) 사용", PracticeSignal.Interact, new[] { Hydrant });
        }
    }

    /// <summary>
    /// 5 지진. 점검 전에는 승강설비를 재가동하지 않고(Q3 제13조) 승강설비를 멈춘 뒤 계단으로 이동시킨다(Q3 제17조10호).
    /// 흔들리는 동안의 몸 보호·외치기·여진 대비는 (신규)라 뺐다.
    /// </summary>
    public sealed class EarthquakeLesson : PracticeOrderLesson
    {
        public override string Section => "5";
        public override string Title => "지진";
        public EarthquakeLesson(EmergencySession session) : base(session)
        {
            Step("6", "역무실 보고", PracticeSignal.Report);
            Step("7", "낙하물 통제", PracticeSignal.Interact, Cordon);
            Step("8", "부상자 119", PracticeSignal.Request, Ems119);
            Step("9", "승강설비 이용 금지", PracticeSignal.Measure, EscalatorHalt);
            Step("10", "승강기 담당 요청", PracticeSignal.Request, ElevatorTeam);
            Step("11", "이동 방송", PracticeSignal.Broadcast, EvacuateStation, EvacuateArea);
            Step("12", "대피 유도", PracticeSignal.Interact, GuideEvacuation);
            Step("14", "시설 점검 요청", PracticeSignal.Request, FacilityTeam);
            Step("16", "인계", PracticeSignal.Handover);
            Before("evacuate-before-halt", "승강설비를 멈추기 전에 이동 방송", "9", "11");
            Forbid("restart-before-inspection", "점검 전에 에스컬레이터 재가동", PracticeSignal.Interact, new[] { EscalatorRestart });
        }
    }

    /// <summary>
    /// 순서표가 아직 없는 연습(열차·선로, 열차 하부 연기)의 공통 순서: 역무실 보고 → 기관 요청·열차 보류 → 주변 통제·방송 →
    /// 현장 인계(docs/emergency-lesson-steps.md "모든 사건의 마지막 인계는 같다"). 근거는 게임 규칙이며, 순서표가 생기면
    /// 이 연습은 따로 만든다.
    /// </summary>
    public sealed class CommonOrderLesson : PracticeOrderLesson
    {
        private readonly string title;
        public override string Section => "공통";
        public override string Title => title;
        public CommonOrderLesson(EmergencySession session, string title) : base(session)
        {
            this.title = title;
            Step("1", "역무실 보고", PracticeSignal.Report);
            Step("2", "기관 요청", PracticeSignal.Request);
            Step("3", "열차 출발 보류", PracticeSignal.Measure, TrainHold);
            Step("4", "주변 통제·방송", PracticeSignal.Interact, Cordon);
            Step("4", "주변 통제·방송", PracticeSignal.Broadcast);
            Step("5", "현장 인계", PracticeSignal.Handover);
            Require("no-report", "역무실 보고 없이 인계", "1");
        }
    }
}
