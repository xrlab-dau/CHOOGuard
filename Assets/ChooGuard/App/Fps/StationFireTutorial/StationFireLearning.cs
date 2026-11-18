using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        sealed class DialogueLine { public int Id; public string Speaker,Content; }
        readonly Queue<DialogueLine> pendingDialogue=new Queue<DialogueLine>();
        readonly List<string> dialogueHistory=new List<string>();
        DialogueLine activeDialogue;
        int dialogueSequence,completedDialogue,resultDialogue;
        int reviewPage;
        readonly List<Button> reviewTabs=new List<Button>();

        int Speak(string speaker,string content,int unusedVoice)
        {
            // Repeated E/Q cannot starve the next meaningful line with duplicates.
            if(activeDialogue!=null&&activeDialogue.Speaker==speaker&&activeDialogue.Content==content)return activeDialogue.Id;
            foreach(var line in pendingDialogue)
                if(line.Speaker==speaker&&line.Content==content)return line.Id;
            var next=new DialogueLine{Id=++dialogueSequence,Speaker=speaker,Content=content};
            pendingDialogue.Enqueue(next);TickDialogue();return next.Id;
        }
        bool DialogueFinished(int id)=>id<=completedDialogue;
        void TickDialogue()
        {
            // The wheel owns the centre/lower screen. Preserve reading time while
            // its choices or the explicit arrival transition cover the subtitle.
            if(RadioOpen||arrivalTransition)
            {
                if(activeDialogue!=null)subtitleUntil+=Time.deltaTime;
                return;
            }
            if(activeDialogue!=null&&Time.time>=subtitleUntil)AdvanceDialogue();
            if(activeDialogue==null&&pendingDialogue.Count>0)
            {
                activeDialogue=pendingDialogue.Dequeue();
                subtitle.text="["+activeDialogue.Speaker+"] "+activeDialogue.Content;
                subtitleUntil=Time.time+Mathf.Clamp(activeDialogue.Content.Length*.09f+1,5,18);
                dialogueHistory.Add(subtitle.text);
                if(dialogueHistory.Count>24)dialogueHistory.RemoveAt(0);
                if(ConfirmSound!=null)replyAudio.PlayOneShot(ConfirmSound,.22f);
            }
        }
        void AdvanceDialogue()
        {
            if(activeDialogue==null)return;
            completedDialogue=activeDialogue.Id;activeDialogue=null;subtitle.text="";
        }
        string BriefInstructions(int stage)
        {
            if(RadioOpen)
            {
                if(radioGroup<0)return radioWheel.Selected==0?
                    "Q 유지 · "+Key("좌클릭")+"으로 ‘보고’ 열기":
                    "Q 유지 · 마우스를 위로 움직여 ‘보고’ 선택";
                if(radioGroup==0)
                {
                    bool result=Step==StationFireStep.ResultReport||Step==StationFireStep.Specialist;
                    int expected=result?1:0;
                    return radioWheel.Selected==expected?Key("Q 놓기")+"로 선택한 보고 전송":
                        "Q 유지 · 마우스를 "+(result?"아래":"위")+"로 움직여\n‘"+RadioReports[expected]+"’ 선택";
                }
                return "선택한 요청 전송: "+Key("Q 놓기")+"\n보고 메뉴로 돌아가기: "+Key("우클릭");
            }
            if(Step==StationFireStep.Report||Step==StationFireStep.ResultReport)
                return reportSending?"보고 내용을 확인하고 동료의 응답을 기다리세요.":Key("Q")+"를 누른 채 유지하여 무전 열기";
            if(stage==4||stage==5)
            {
                string part=stage==4?"pin":"nozzle",name=PartLabel(part);
                if(Controls.HeldPart!=null&&!Holding(part))return "먼저 "+Key("좌클릭 놓기")+"로 현재 부품을 놓으세요.";
                if(Mechanics.Position(ExtinguisherId,part)>=.95)return "3/3 · "+Key("좌클릭 놓기")+"로 잡기 끝내기";
                if(Holding(part))return "2/3 · 좌클릭을 누른 채 "+Key("휠↑")+"\n또는 표시된 방향으로 드래그";
                if(Controls.AimedPart?.OwnerId==ExtinguisherId&&Controls.AimedPart.PartId==part)
                    return "2/3 · "+Key("좌클릭")+"을 누른 채 유지하세요.";
                return "1/3 · "+name+"에 화면 가운데 조준점 맞추기";
            }
            if(stage==7)
            {
                if(SafetyCorrection)return SafetyMessage;
                if(Holding("lever"))return "좌클릭을 계속 누른 채\n마우스로 불꽃 아래쪽 조준";
                return Controls.AimedPart?.PartId=="lever"?Key("좌클릭")+"을 누른 채 유지하여 분사":
                    "소화기 위쪽 레버에 조준점 맞추기";
            }
            switch(stage)
            {
                case 1:return "표시된 승객에게 다가가 바라보고 "+Key("E");
                case 3:return "표시된 소화기에 다가가 본체를 보고 "+Key("F");
                case 6:return Key("WASD")+"로 바닥 경로를 따라 이동\n불을 지나지 않는 퇴로 유지";
                case 8:return Key("좌클릭 놓기")+"로 분사 멈추기";
                default:return Specialist.gameObject.activeSelf&&Specialist.Arrived?
                    "표시된 소방대에게 다가가 바라보고 "+Key("E"):
                    !DialogueFinished(resultDialogue)?"보고 자막 확인 후 소방대 도착 안내로 이어집니다.":
                    "소방대 도착을 기다리며 진입 통로를 비워 주세요.";
            }
        }
        string BriefFeedback(int stage)
        {
            if(RadioOpen)return radioWheel.Selected<0?"아직 선택되지 않았습니다.":
                "선택: "+(radioGroup<0?RadioGroups:radioGroup==0?RadioReports:RadioRequests)[radioWheel.Selected];
            if(stage==4||stage==5)
            {
                string part=stage==4?"pin":"nozzle";
                return PartLabel(part)+" 준비 "+Mathf.RoundToInt((float)Mechanics.Position(ExtinguisherId,part)*100)+"% · "+(Holding(part)?"잡고 있음":"잡지 않음");
            }
            return CurrentFeedback(stage);
        }
        void CreateReviewTabs()
        {
            var names=new[]{"현재 설명","장비 복습","무전 복습","대화 기록"};
            for(int i=0;i<names.Length;i++)
            {
                int page=i;
                reviewTabs.Add(FpsUiFactory.Button(pausePanel.transform,KoreanFont,names[i],new Vector2(.5f,.5f),
                    new Vector2(-352.5f+i*235,275),new Vector2(225,44),()=>{reviewPage=page;UpdatePauseView(true);},20));
            }
        }
        string ReviewBody(string why,string action,string next)
        {
            foreach(var tab in reviewTabs)tab.GetComponentInChildren<TMP_Text>().color=
                reviewTabs.IndexOf(tab)==reviewPage?FpsUiFactory.Accent:Color.white;
            if(reviewTabs.Count>0)reviewTabs[0].GetComponentInChildren<TMP_Text>().text=Step==StationFireStep.Complete?"학습 결과":"현재 설명";
            if(reviewPage==1)return "<color=#FFD07A>소화기 조작 복습</color>\n\n"+
                "1. 본체를 보고 F로 들기 · G로 내려놓기\n"+
                "2. 안전핀 조준 → 좌클릭 유지 + 휠↑ → 분리되면 놓기\n"+
                "3. 노즐 조준 → 좌클릭 유지 + 휠↑ → 준비되면 놓기\n"+
                "4. 퇴로를 둔 대응 위치에서 레버를 누른 채 화점 조준\n"+
                "5. 좌클릭을 놓아 중지 → Q 무전으로 결과 보고\n\n"+
                "휠 대신 표시된 방향으로 드래그할 수도 있습니다.\n이 화면을 읽어도 장비나 진행 상태는 바뀌지 않습니다.";
            if(reviewPage==2)return "<color=#FFD07A>무전 조작 복습</color>\n\n"+
                "1. Q를 누른 채 유지합니다.\n2. 마우스를 위로 움직여 ‘보고’를 선택합니다.\n"+
                "3. 좌클릭으로 하위 메뉴를 엽니다.\n4. 마우스를 다시 움직여 메시지를 고릅니다.\n"+
                "   위: 화재 위치 · 상황 / 아래: 초기소화 결과\n5. 주황색 선택을 확인하고 Q를 놓아 전송합니다.\n\n"+
                "우클릭: 이전 메뉴 · 선택 없이 Q 놓기: 취소\n‘요청 · 기관’에서는 지원 요청과 초기소화 중단을 보고합니다.";
            if(reviewPage==3)return dialogueHistory.Count==0?"아직 주고받은 대화가 없습니다.":
                "<color=#FFD07A>최근 대화 · 일시정지 중에는 자막 시간이 멈춥니다</color>\n\n"+
                string.Join("\n\n",dialogueHistory.Skip(Mathf.Max(0,dialogueHistory.Count-2)));
            if(Step==StationFireStep.Complete)return CompletionSummary();
            return why+"\n\n<color=#FFD07A>"+action+"</color>\n\n다음: "+next;
        }
        string CompletionSummary()
        {
            bool used=State.Entities[ExtinguisherId].Get("mech.charge")<.999;
            string response=WithdrawalRequested?"초기소화 중단 후 상황 인계":
                State.Entities[FireId].Get("fire")>.01?"불이 남은 상태를 보고·인계":"보이는 불꽃 감소 후 결과 보고·인계";
            return "<color=#FFD07A>이번에 실제로 수행한 대응</color>\n\n"+
                "승객 대피 방향 안내: "+(warningsIssued?"수행":"미수행")+"\n"+
                "화재 위치 · 지원 보고: "+(ReportReceived?"접수 확인":"미확인")+"\n"+
                "소화기 사용: "+(used?"사용 · 약제 잔량 "+Mathf.RoundToInt((float)State.Entities[ExtinguisherId].Get("mech.charge")*100)+"%":"미사용")+"\n"+
                "대응 결과: "+response+"\n"+
                "소방대 상황 전달: "+(SpecialistReceived?"수신 확인":"미확인")+"\n\n"+
                "<color=#FFD07A>다음 연습</color> · "+(!used?"상단 ‘장비 복습’에서 핀·노즐·레버 순서를 확인하세요.":"상단 ‘무전 복습’에서 위치 보고와 결과 보고를 구분해 보세요.")+"\n"+
                "잔불과 미확인 인원의 확인은 소방대가 이어갑니다.";
        }
    }
}
