using static ChooGuard.App.Fps.Emergency.PracticeLabel;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>4-1 승강기 갇힘. 갇힌 승객을 안심시킨다(E3). 번호 기록·강제 개문 거절은 (신규)라 뺐다.</summary>
    public sealed class ElevatorTrapLesson : PracticeOrderLesson
    {
        public override string Section => "4-1";
        public override string Title => "승강기 갇힘";
        public ElevatorTrapLesson(EmergencySession session) : base(session)
        {
            Step("3", "안심시키기", PracticeSignal.Interact, ReassureTrapped);
            Step("5", "승강기 담당 호출", PracticeSignal.Request, ElevatorTeam);
            Step("6", "119 요청", PracticeSignal.Request, Fire119);
            Step("7", "운행 정지·통제 안내", PracticeSignal.Broadcast, Inform);
            Step("8", "구출 후 확인", PracticeSignal.Interact, CheckPatient);
            Step("9", "인계", PracticeSignal.Handover);
            Require("no-reassure", "갇힌 승객을 안심시키지 않음", "3");
        }
    }

    /// <summary>4-2 정전. 비상조명이 켜질 때까지 제자리에서 기다리게 한 뒤 이동시킨다(E3). 점검 전 재가동 금지(E1 제13조).</summary>
    public sealed class PowerOutageLesson : PracticeOrderLesson
    {
        public override string Section => "4-2";
        public override string Title => "정전";
        public PowerOutageLesson(EmergencySession session) : base(session)
        {
            Step("2", "제자리 대기 방송", PracticeSignal.Broadcast, Inform);
            Step("3", "역무실 보고", PracticeSignal.Report);
            Step("5", "전기 담당 호출", PracticeSignal.Request, ElectricTeam);
            Step("7", "이동 안내", PracticeSignal.Interact, GuideEvacuation);
            Step("7", "이동 안내", PracticeSignal.Broadcast, EvacuateArea, EvacuateStation);
            Step("9", "인계", PracticeSignal.Handover);
            Before("move-before-calm", "제자리 대기 안내 전에 이동 안내", "2", "7");
            Forbid("restart-before-inspection", "복구·점검 전에 에스컬레이터 재가동", PracticeSignal.Interact, new[] { EscalatorRestart });
        }
    }

    /// <summary>4-3 화재감지기 오작동(비화재보). 현장에서 화재가 아님을 확인한 뒤에만 수신기를 복구한다(E5 제3조5호).</summary>
    public sealed class FalseAlarmLesson : PracticeOrderLesson
    {
        public override string Section => "4-3";
        public override string Title => "화재 경보 오작동";
        public FalseAlarmLesson(EmergencySession session) : base(session)
        {
            Step("4b", "비화재 보고", PracticeSignal.Report);
            Step("5", "승객 안내", PracticeSignal.Broadcast, Inform);
            Step("6", "수신기 복구", PracticeSignal.Measure, ReceiverReset);
            Step("8", "감지기 점검 요청", PracticeSignal.Request, FacilityTeam);
            Step("9", "인계", PracticeSignal.Handover);
            Before("reset-before-confirm", "비화재 확인 보고 전에 수신기 복구", "4b", "6");
        }
    }

    /// <summary>4-4 천장 안내판 낙하. 떨어진 곳과 그 아래를 막는다(E3, E9).</summary>
    public sealed class FallingObjectLesson : PracticeOrderLesson
    {
        public override string Section => "4-4";
        public override string Title => "낙하물";
        public FallingObjectLesson(EmergencySession session) : base(session)
        {
            Step("2", "부상자 확인", PracticeSignal.Interact, CheckPatient);
            Step("3", "접근 통제", PracticeSignal.Interact, Cordon);
            Step("4", "역무실 보고", PracticeSignal.Report);
            Step("5", "우회 방송", PracticeSignal.Broadcast, ClearAround);
            Step("6", "시설 담당 호출", PracticeSignal.Request, FacilityTeam);
            Step("7", "인계", PracticeSignal.Handover);
            Require("no-cordon", "떨어진 곳을 통제하지 않고 인계", "3");
        }
    }

    /// <summary>4-5 누수·스프링클러 살수. 물이 멈추고 화재가 아님을 확인한 뒤 수신기를 복구한다(E5 제3조5호).</summary>
    public sealed class WaterLeakLesson : PracticeOrderLesson
    {
        public override string Section => "4-5";
        public override string Title => "누수·스프링클러 살수";
        public WaterLeakLesson(EmergencySession session) : base(session)
        {
            Step("4", "미끄럼 통제", PracticeSignal.Interact, Cordon);
            Step("5", "역무실 보고", PracticeSignal.Report);
            Step("6", "시설·전기 담당 호출", PracticeSignal.Request, FacilityTeam, ElectricTeam);
            Step("7", "우회 방송", PracticeSignal.Broadcast, ClearAround);
            Step("8", "수신기 복구", PracticeSignal.Measure, ReceiverReset);
            Step("9", "인계", PracticeSignal.Handover);
            Before("reset-before-report", "역무실 보고 전에 수신기 복구", "5", "8");
        }
    }

    /// <summary>
    /// 4-6 방화셔터 오동작. 조작함의 정지·복구 스위치는 상호작용 문구가 셔터 상태에 따라 바뀌어 아직 신호로 받지 않는다.
    /// 근거가 앞뒤를 정한 단계가 신호로 잡히지 않아 감점 규칙은 없다.
    /// </summary>
    public sealed class ShutterFaultLesson : PracticeOrderLesson
    {
        public override string Section => "4-6";
        public override string Title => "방화셔터 오동작";
        public ShutterFaultLesson(EmergencySession session) : base(session)
        {
            Step("8", "시설 호출", PracticeSignal.Request, FacilityTeam);
            Step("8", "시설 호출", PracticeSignal.Measure, ShutterRestore);
            Step("9", "인계", PracticeSignal.Handover);
        }
    }

    /// <summary>4-7 주방 가스 누출. 밸브를 잠그고 가스가 빠진 뒤에 경보기를 복귀한다(E4, E6 NFTC 206).</summary>
    public sealed class GasLeakLesson : PracticeOrderLesson
    {
        public override string Section => "4-7";
        public override string Title => "가스 누출";
        public GasLeakLesson(EmergencySession session) : base(session)
        {
            Step("4", "중간밸브 잠그기", PracticeSignal.Interact, GasMiddleValve);
            Step("5", "메인밸브 잠그기", PracticeSignal.Interact, GasMainValve);
            Step("7", "주변 대피", PracticeSignal.Interact, Cordon);
            Step("7", "주변 대피", PracticeSignal.Broadcast, EvacuateArea);
            Step("9", "도시가스·119 신고", PracticeSignal.Request, GasCompany, Fire119);
            Step("10", "경보기 복귀", PracticeSignal.Interact, GasAlarmReset);
            Step("12", "인계", PracticeSignal.Handover);
            Before("alarm-before-valve", "밸브를 잠그기 전에 경보기 복귀", "4", "10");
        }
    }
}
