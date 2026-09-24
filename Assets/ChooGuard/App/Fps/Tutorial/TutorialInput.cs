using ChooGuard.App.Fps.Work;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ChooGuard.App.Fps.Tutorial
{
    // 튜토리얼 전용 입력. FirstPersonResponder 에 키를 더하지 않는다 —
    // 그쪽은 이동·시선·상호작용 계층이고 판정이나 역할 경계를 알아서는 안 된다.
    // 설비가 절차를 모르고 세션이 규칙을 소유하는 것과 같은 분리다.
    //
    // 응답자와 같은 규율을 따른다: 일시정지 중에는 받지 않고, ReadKeyboard=false 면
    // 공개 메서드만으로 구동된다(시험이 OS 입력을 합성하지 않고 같은 경로를 탄다).
    public sealed class TutorialInput : MonoBehaviour
    {
        public TutorialSession Session;
        public FirstPersonResponder Responder;
        [Header("입력")]
        public bool ReadKeyboard=true;

        private void Awake()
        {
            if(Session==null)Session=GetComponent<TutorialSession>();
            if(Responder==null&&Session!=null)Responder=Session.Responder;
        }

        private void Update()
        {
            if(!ReadKeyboard)return;
            var keyboard=Keyboard.current;
            if(keyboard==null)return;
            HandleKeys(keyboard.digit1Key.wasPressedThisFrame,
                       keyboard.digit2Key.wasPressedThisFrame,
                       keyboard.fKey.wasPressedThisFrame);
        }

        // 키 읽기와 매핑을 나눈다. Keyboard.current 를 합성하지 않고도 매핑과 게이트를 시험할 수 있어야 한다 —
        // 공개 메서드만 시험하면 실제 입력 경로는 한 번도 실행되지 않는다(ECC 2026-09-24).
        public void HandleKeys(bool fitPressed,bool unfitPressed,bool repairPressed)
        {
            if(Session==null)return;
            if(Responder!=null&&Responder.IsPaused)return;
            if(fitPressed)SelectFit();
            if(unfitPressed)SelectUnfit();
            if(repairPressed)AttemptFieldRepair();
        }

        public bool SelectFit()=>Session!=null&&Session.SelectVerdict(InspectionVerdict.FIT);
        public bool SelectUnfit()=>Session!=null&&Session.SelectVerdict(InspectionVerdict.UNFIT);

        // 현장 수리 시도. 거부가 곧 채점 항목이다 — 시도할 수단이 없으면 역할 경계 위반도 0 으로 고정된다.
        // 성공(AllowFieldRepair=true)일 때 실제 수리를 수행하지는 않는다. 그 경우도 설비가 정한다.
        public bool AttemptFieldRepair()=>Session!=null&&Session.TryFieldRepair();
    }
}
