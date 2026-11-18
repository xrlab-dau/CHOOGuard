using System.Collections;
using ChooGuard.App.Fps.Hud;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        static readonly string[] RadioGroups={"보고","요청 · 기관"};
        static readonly string[] RadioReports={"화재 위치 · 상황 보고","초기소화 결과 보고"};
        static readonly string[] RadioRequests={"119 신고 · 현장 지원 요청","초기소화 중단 · 상황 전달"};
        RadioWheel radioWheel;
        int radioGroup=-1,radioGuardFrame=-1;
        bool reportSending;
        public bool RadioOpen=>radioWheel!=null&&radioWheel.Open;
        public bool ResultReported {get;private set;}
        public bool WithdrawalRequested {get;private set;}
        public bool RadioInputBlocked=>RadioOpen||arrivalTransition||Time.frameCount<=radioGuardFrame||
            (!externalInteraction&&Keyboard.current!=null&&(Keyboard.current.qKey.isPressed||Keyboard.current.qKey.wasPressedThisFrame||Keyboard.current.qKey.wasReleasedThisFrame));

        void InitializeRadio(){radioWheel=RadioWheel.Create(transform,KoreanFont);radioWheel.SetTeachingPresentation(true);}
        void ReadRadioInput()
        {
            var k=Keyboard.current;var m=Mouse.current;if(k==null)return;
            StepRadioInput(k.qKey.wasPressedThisFrame,k.qKey.isPressed,m!=null?m.delta.ReadValue():Vector2.zero,
                m!=null&&m.leftButton.wasPressedThisFrame,m!=null&&m.rightButton.wasPressedThisFrame);
        }
        /// <summary>Same Q hold, mouse direction, left-open/right-back and release-to-send contract as the shift wheel.</summary>
        public void StepRadioInput(bool pressed,bool qHeld,Vector2 pointer,bool leftPressed=false,bool rightPressed=false)
        {
            if(!Captured||!Running||arrivalTransition)return;
            if(pressed&&!RadioOpen)
            {
                Controls.ResetInputState();ReleaseEquipmentPressure();radioGroup=-1;
                radioWheel.Show(RadioGroups,"무전\n<size=16>방향 고른 뒤\n좌클릭 열기</size>");
            }
            if(!RadioOpen)return;
            radioGuardFrame=Time.frameCount+1;radioWheel.Steer(pointer);
            if(leftPressed&&radioGroup<0&&radioWheel.Selected>=0)
            {
                radioGroup=radioWheel.Selected;
                var labels=radioGroup==0?RadioReports:RadioRequests;
                radioWheel.Show(labels,(radioGroup==0?"보고":"요청 · 기관")+"\n<size=16>떼면 보냄\n우클릭 뒤로</size>");
            }
            if(rightPressed&&radioGroup>=0)
            {radioGroup=-1;radioWheel.Show(RadioGroups,"무전\n<size=16>방향 고른 뒤\n좌클릭 열기</size>");}
            if(!qHeld)CloseRadio(true);
        }
        void CloseRadio(bool send)
        {
            if(!RadioOpen)return;
            int selected=radioWheel.Close();int group=radioGroup;radioGroup=-1;
            radioGuardFrame=Time.frameCount+1;Controls?.ResetInputState();ReleaseEquipmentPressure();
            if(send&&selected>=0&&group>=0)SendRadio(group,selected);
        }
        void SendRadio(int group,int selected)
        {
            if((group==0&&selected==0)||(group==1&&selected==0))
            {
                if(ReportReceived){Speak("동료","신고와 승객 안내 지원은 계속하고 있습니다. 현장 변화가 있으면 알려 주세요.",-1);return;}
                if(reportSending)return;
                reportSending=true;
                int sent=Speak("역무원","1층 매장 좌석에서 불과 연기를 확인했습니다. "+(warningsIssued?"주변 승객에게 대피 방향을 안내했습니다. ":"주변 승객 상황은 추가 확인이 필요합니다. ")+"119 신고와 현장 안내 지원 바랍니다.",-1);
                Record("q-wheel-fire-report-sent");StartCoroutine(AcknowledgeReport(sent));return;
            }
            if(group==1&&selected==1)
            {
                if(!ReportReceived){Speak("안내","먼저 화재 위치를 보고하고 신고·현장 지원을 요청하세요.",-1);return;}
                WithdrawalRequested=true;ReleaseEquipmentPressure();
                resultDialogue=Speak("역무원",ObservedResponseSummary(),-1);
                ResultReported=true;Record("q-wheel-withdrawal-reported");return;
            }
            if(group==0&&selected==1)
            {
                if(!SuppressionEnded){Speak("안내","아직 초기소화 결과를 확인하지 못했습니다. 상황에 따라 소화하거나 중단을 보고하세요.",-1);return;}
                if(State.Entities[ExtinguisherId].Get("mech.flow")>0||Controls.HeldPart?.PartId=="lever")
                {Speak("안내","레버를 놓고 분사를 멈춘 뒤 결과를 전달하세요.",-1);return;}
                ResultReported=true;
                resultDialogue=Speak("역무원",ObservedResponseSummary(),-1);
                Record("q-wheel-observed-suppression-result-reported");
            }
        }
        IEnumerator AcknowledgeReport(int sent)
        {
            yield return new WaitUntil(()=>DialogueFinished(sent));
            yield return new WaitForSeconds(.4f);
            ReportReceived=true;reportSending=false;
            Speak("동료","위치 확인했습니다. 119 신고를 맡고 주변 승객 안내를 지원하겠습니다.",-1);
            Record("local-report-acknowledged-reporting-role-assigned");
        }
    }
}
