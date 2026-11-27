using static ChooGuard.App.Fps.Emergency.PracticeLabel;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// 3-0 모든 환자 공통 시작(docs/emergency-lesson-steps.md "3. 환자"). 119 신고는 Q 요청·기관 "119 구급"이나, 구급이 함께
    /// 출동하는 역무실 보고로 한다. 신고 때 의식·호흡·아픈 곳을 알려야 하므로(P6) 반응 확인 전에 신고하면 감점한다.
    /// </summary>
    public abstract class PatientLesson : PracticeOrderLesson
    {
        protected PatientLesson(EmergencySession session) : base(session) { }

        protected void CheckThenCall(string check, string checkName, string call)
        {
            Step(check, checkName, PracticeSignal.Interact, CheckPatient);
            Step(call, "119 신고", PracticeSignal.Request, Ems119);
            Step(call, "119 신고", PracticeSignal.Report);
            Before("call-before-check", "반응 확인 전에 119 신고(의식·호흡을 전하지 못함)", check, call);
        }

        protected void Handover(string no) => Step(no, "구급대 인계", PracticeSignal.Handover);
    }

    /// <summary>3-1 경련. 넘어짐 방지·주변 치우기·머리 보호·시간 재기·회복 자세는 (신규)라 뺐다.</summary>
    public sealed class SeizureLesson : PatientLesson
    {
        public override string Section => "3-1";
        public override string Title => "경련";
        public SeizureLesson(EmergencySession session) : base(session)
        {
            CheckThenCall("2", "반응 확인", "4");
            Handover("14");
        }
    }

    /// <summary>3-2 가슴통증. AED도 가져오게 한다(P4 심근경색).</summary>
    public sealed class ChestPainLesson : PatientLesson
    {
        public override string Section => "3-2";
        public override string Title => "가슴 통증";
        public ChestPainLesson(EmergencySession session) : base(session)
        {
            CheckThenCall("2", "반응 확인", "4");
            // 보관함에서 꺼내기만 하면 아직 가져온 것이 아니다. 환자 곁에 둘 때 잡는다.
            Step("6", "AED 가져오기", PracticeSignal.Interact, AedPlace);
            Handover("10");
            Require("no-aed", "AED를 가져오지 않음", "6");
        }
    }

    /// <summary>3-3 호흡곤란(천식·알레르기). 편한 자세·흡입기·자가주사기는 (신규)라 뺐다.</summary>
    public sealed class BreathingLesson : PatientLesson
    {
        public override string Section => "3-3";
        public override string Title => "호흡곤란";
        public BreathingLesson(EmergencySession session) : base(session)
        {
            CheckThenCall("2", "반응 확인", "4");
            Handover("9");
        }
    }

    /// <summary>3-4 저혈당. 먼저 대답하고 삼킬 수 있는지 판단한다(P5). 당 주기는 (신규)라 뺐다.</summary>
    public sealed class LowBloodSugarLesson : PatientLesson
    {
        public override string Section => "3-4";
        public override string Title => "저혈당";
        public LowBloodSugarLesson(EmergencySession session) : base(session)
        {
            CheckThenCall("3", "의식 판단", "4b");
            Handover("7");
        }
    }

    /// <summary>3-5 쓰러짐(실신, 정상 호흡 있음). 회복 자세·안정 안내는 (신규)라 뺐다.</summary>
    public sealed class CollapseLesson : PatientLesson
    {
        public override string Section => "3-5";
        public override string Title => "쓰러짐";
        public CollapseLesson(EmergencySession session) : base(session)
        {
            CheckThenCall("2", "반응 확인", "4");
            Handover("9");
        }
    }

    /// <summary>3-6 계단·여행가방 넘어짐(외상). 쓰러진 승객은 통제선을 칠 수 없어(Cordonable 아님) 1단계는 뺐다.</summary>
    public sealed class FallLesson : PatientLesson
    {
        public override string Section => "3-6";
        public override string Title => "계단·여행가방 넘어짐";
        public FallLesson(EmergencySession session) : base(session)
        {
            CheckThenCall("2", "반응·호흡 확인", "6");
            Handover("7");
        }
    }

    /// <summary>
    /// 3-7 에스컬레이터 넘어짐·끼임. 먼저 비상정지로 멈추고(P8, P9) 점검 전에는 재가동하지 않는다(E1). 끼임은 119 구조와
    /// 승강기 담당을 부른다.
    /// </summary>
    public sealed class EscalatorAccidentLesson : PatientLesson
    {
        public override string Section => "3-7";
        public override string Title => "에스컬레이터 사고";
        public EscalatorAccidentLesson(EmergencySession session) : base(session)
        {
            Step("2", "비상정지", PracticeSignal.Interact, EscalatorStop);
            Step("3", "진입 통제", PracticeSignal.Measure, EscalatorHalt);
            Step("5", "119·승강기 담당 호출", PracticeSignal.Request, Ems119, ElevatorTeam);
            Step("5", "119·승강기 담당 호출", PracticeSignal.Report);
            Step("6", "부상자 처치", PracticeSignal.Interact, CheckPatient);
            Handover("7");
            Before("care-before-stop", "비상정지 전에 부상자 처치(에스컬레이터가 계속 움직임)", "2", "6");
            Forbid("restart-before-inspection", "점검 전에 에스컬레이터 재가동", PracticeSignal.Interact, new[] { EscalatorRestart });
        }
    }
}
