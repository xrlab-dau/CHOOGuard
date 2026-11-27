using System;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The labels the practice lessons listen for, spelled exactly as the game shows them. Interaction prompts come from the
    /// targets (Passenger, HazardMarker, EscalatorStopButton, StationFixture, StaffTools, GasValvePoint…); requests, measures and
    /// broadcast scopes come from the radio (IncidentDirector.Radio.cs TeamRequests / MeasureRequests / ScopeName).
    /// </summary>
    public static class PracticeLabel
    {
        // E·R 상호작용
        public const string CheckPatient = "상태 확인";
        public const string GuideEvacuation = "대피 안내";
        public const string CalmDown = "말로 진정시키기";
        public const string Cordon = "주변 접근 통제";
        public const string EscalatorStop = "비상정지 버튼 누르기";
        public const string EscalatorRestart = "열쇠로 재가동";
        public const string ReassureTrapped = "갇힌 승객 안심시키기";
        public const string CallPoint = "발신기 누르기 (화재 신호)";
        public const string AedTake = "AED 꺼내기";
        /// <summary>AED가 환자 곁에 놓였다: E "AED 곁에 두기"와 G로 내려놓기 모두 IncidentDirector.AedSetDown에서 이 문구로 알린다.</summary>
        public const string AedPlace = "AED 곁에 두기";
        public const string ExtinguisherPick = "소화기 들기";
        public const string Hydrant = "호스·관창 꺼내기 (방수)";
        public const string GasMiddleValve = "가스 중간밸브 잠그기";
        public const string GasMainValve = "가스 메인밸브 잠그기";
        public const string GasAlarmReset = "가스누설경보기 확인·복귀";

        // Q 요청·기관 (TeamRequests)
        public const string Fire119 = "119 소방";
        public const string Ems119 = "119 구급";
        public const string Police112 = "112";
        public const string RailwayPolice = "철도경찰";
        public const string FacilityTeam = "시설 담당";
        public const string ElectricTeam = "전기 담당";
        public const string GasCompany = "도시가스";
        public const string ElevatorTeam = "승강기 담당";

        // Q 요청·설비 (MeasureRequests)
        public const string TrainHold = "열차 출발 보류";
        public const string EscalatorHalt = "에스컬레이터 정지";
        public const string ReceiverReset = "수신기 복구";
        public const string ShutterRestore = "방화셔터 복구";

        // Q 방송 범위 (ScopeName)
        public const string ClearAround = "주변 통로 비우기";
        public const string EvacuateArea = "구역 대피";
        public const string EvacuateStation = "역 전체 대피";
        public const string Inform = "침착 안내";
    }

    /// <summary>
    /// Builds the order lesson of a practice scenario (PracticeScenario.OrderLesson). Scenarios with a detailed table in
    /// docs/emergency-lesson-steps.md get their own lesson; the rest get <see cref="CommonOrderLesson"/>.
    /// </summary>
    public static class PracticeLessons
    {
        public static Func<EmergencySession, PracticeOrderLesson> Of<T>(Func<EmergencySession, T> create) where T : PracticeOrderLesson =>
            session => create(session).Start();
    }
}
