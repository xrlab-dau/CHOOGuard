using UnityEngine;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        public string CoachingObjective => objective != null ? objective.text : "";
        public string CoachingInstructions => instruction != null ? instruction.text : "";
        public string CoachingFeedback => deviceStatus != null ? deviceStatus.text : "";
        static string Key(string text) => "<b><color=#FFD07A>["+text+"]</color></b>";
        bool Holding(string part) => Controls.HeldPart?.OwnerId==ExtinguisherId&&Controls.HeldPart.PartId==part;

        void DescribeCurrentAction(out int stage,out string title,out string why,out string action,out string next)
        {
            stage=1;title="가까운 승객에게 위험 알리기";
            why="불이 난 의자에서 떨어져 열린 통로로 나가도록 가까운 승객에게 알립니다.";
            action="1. 표시된 승객에게 가까이 가세요\n2. 승객을 바라보고 "+Key("E")+" 한 번\n안내를 들은 승객이 이동하기 시작합니다.";
            next="Q 무전으로 화재 위치와 필요한 지원 보고";
            if(Step==StationFireStep.Warning)return;
            if(Step==StationFireStep.Report)
            {
                stage=2;title=reportSending?"보고 전송됨 · 동료 접수 중":"Q 무전으로 위치·지원 보고";
                why="동료가119 신고와 주변 승객 안내를 맡도록 위치와 필요한 지원을 전달합니다.";
                action=reportSending?"보고를 보냈습니다. 동료의 접수 응답을 확인하세요.\n응답 후 소화기 위치 안내가 나타납니다.":ReportInstructions(false);
                next="표시된 기존 소화기에 가서 F로 들기";return;
            }
            if(Step==StationFireStep.Extinguish)
            {
                if(State.Entities["player"].Get("mech.carry."+ExtinguisherId)<=0)
                {
                    stage=3;title="표시된 소화기를 F로 들기";
                    bool moved=Vector3.Distance(ExtinguisherBody.position,OriginalExtinguisherPosition)>.15f;
                    why=moved?"내려놓은 같은 소화기를 다시 듭니다. 이미 준비한 핀·노즐 상태는 유지됩니다.":"매장 입구에 원래 비치된 소화기입니다. 승객들이 이동하는 동안 장비를 준비합니다.";
                    action="1. 표식과 바닥 경로를 따라 가까이 이동\n2. 빨간 본체나 손잡이를 보고 "+Key("F")+" 한 번\n"+Key("G")+"는 내려놓기입니다.";
                    next="소화기 위쪽의 안전핀을 잡아 빼기";return;
                }
                double pin=Mechanics.Position(ExtinguisherId,"pin"),ready=Mechanics.Position(ExtinguisherId,"nozzle");
                if(pin<.95||Holding("pin"))
                {
                    stage=4;title="안전핀 잡고 빼기";why="안전핀이 꽂힌 동안에는 레버를 눌러도 분사되지 않습니다.";
                    action=pin>=.95?"안전핀이 분리됐습니다.\n"+Key("좌클릭")+"을 놓아 잡기를 끝내세요.":
                        "1. 위쪽의 안전핀을 조준\n2. "+Key("좌클릭 유지")+" + "+Key("휠↑")+" 여러 칸\n또는 잡은 채 표시된 방향으로 드래그\n3. 분리되면 좌클릭을 놓으세요.";
                    next="호스 끝 노즐을 클립에서 꺼내기";return;
                }
                if(ready<.95||Holding("nozzle"))
                {
                    stage=5;title="호스 끝 노즐 꺼내기";why="노즐을 클립에서 꺼내야 분말이 나갈 방향을 조절할 수 있습니다.";
                    action=ready>=.95?"노즐 준비가 끝났습니다.\n"+Key("좌클릭")+"을 놓아 잡기를 끝내세요.":
                        "1. 표식의 노즐을 조준\n2. "+Key("좌클릭 유지")+" + "+Key("휠↑")+" 여러 칸\n또는 잡은 채 표시된 방향으로 드래그\n3. 준비가 끝나면 좌클릭을 놓으세요.";
                    next="퇴로를 둔 대응 위치에서 레버 조작";return;
                }
                if(!Holding("lever")&&NeedsSuppressionApproach())
                {
                    stage=6;title="퇴로를 둔 대응 위치로 이동";why="불을 지나가지 않고 물러날 수 있는 통로를 둔 상태에서 초기소화를 시작합니다.";
                    action="1. "+Key("WASD")+"로 바닥 경로를 따라 이동\n2. 열린 통로와 화점 위치를 확인\n아직 레버를 누르지 않아도 됩니다.";
                    next="레버를 잡은 채 화점으로 시선 옮기기";return;
                }
                stage=7;title="레버를 잡고 화점에 분사";why="누른 채 조준하고, 실제 분말이 불꽃 아래쪽에 닿는지 확인하세요.";
                action=Holding("lever")?
                    "1. "+Key("좌클릭을 계속 유지")+"하세요\n2. 마우스를 움직여 화점 아래쪽 조준\n노즐 방향도 함께 바뀝니다.\n좌클릭을 놓으면 즉시 분사가 멈춥니다.":
                    "1. 위쪽 레버를 보고 "+Key("좌클릭 유지")+"\n2. 누른 채 마우스로 화점 아래쪽 조준\n3. 분말·불꽃 변화를 보고 조준 조절\n좌클릭을 놓으면 즉시 분사 중지";
                next="불꽃이 줄면 좌클릭을 놓아 분사 중지";return;
            }
            if(Step==StationFireStep.Release)
            {
                stage=8;title="좌클릭을 놓아 분사 멈추기";why="불꽃 감소 여부와 남은 위험을 확인하세요. 약제가 소진돼도 불이 남아 있을 수 있습니다.";
                action=Key("좌클릭 놓기")+"\n분말 방출과 분사음이 멈추는지 확인하세요.";
                next="Q 무전의 ‘초기소화 결과 보고’ 선택";return;
            }
            if(Step==StationFireStep.ResultReport)
            {
                stage=9;title="Q 무전으로 결과 전달";why="직접 사용한 장비와 관찰한 결과를 전합니다. 완전히 안전해졌다고 단정하지 않습니다.";
                action=ReportInstructions(true);next="도착한 소방대에게 E로 상황 전달";return;
            }
            if(Step==StationFireStep.Specialist)
            {
                stage=10;title="소방대에게 확인한 상황 전달";why="화재 위치·실시한 조치·미확인 위험을 전달하면 소방대가 현장 확인을 이어갑니다.";
                action=Specialist.gameObject.activeSelf&&Specialist.Arrived?
                    "1. 표시된 소방대에게 가까이 이동\n2. 소방대를 바라보고 "+Key("E")+" 한 번\n다른 동료와 대화하면 상황 전달이 되지 않습니다.":
                    "소방대가 열린 통로로 접근하고 있습니다.\n진입 통로를 비우고 도착 표식을 확인하세요.\n도착하면 가까이 가서 소방대를 보고 "+Key("E");
                next="소방대 수신 확인 후 학습 완료";return;
            }
            stage=10;title=ResponseWithdrawn?"초기 대응 중단·상황 전달 확인":"역무원 초기 대응 학습 완료";
            why=ResponseWithdrawn?"중단 시점까지 실제로 한 조치와 남은 위험을 전달했습니다.":"주변 안내·보고·장비 사용·결과 전달을 수행했습니다. 이후 확인은 소방대가 이어갑니다.";
            action="‘처음부터 다시’로 연습하거나\n‘튜토리얼 메뉴’로 돌아갈 수 있습니다.";next="직접 다시 연습하거나 다른 튜토리얼 선택";
        }

        string ReportInstructions(bool result)
        {
            string direction=result?"아래":"위",message=RadioReports[result?1:0];
            if(!RadioOpen)return "1. "+Key("Q")+"를 누른 채 유지\n2. 마우스를 위로 → ‘보고’에 "+Key("좌클릭")+"\n3. 다시 "+direction+"로 → ‘"+message+"’\n4. 주황색으로 선택되면 "+Key("Q 놓기");
            if(radioGroup<0)return Key("Q 유지")+" · 지금은 묶음 선택입니다\n1. 마우스를 위로 → ‘보고’ 선택\n2. "+Key("좌클릭 한 번")+"으로 하위 메뉴 열기\nQ를 놓기 전에 메시지까지 선택해야 합니다.";
            if(radioGroup==0)return Key("Q 유지")+" · ‘보고’ 메뉴가 열렸습니다\n1. 마우스를 다시 "+direction+"로 움직이세요\n2. ‘"+message+"’가 주황색이면\n3. "+Key("Q 놓기")+"로 전송 · "+Key("우클릭")+" 뒤로";
            return Key("Q 유지")+" · ‘요청 · 기관’ 메뉴입니다\n위:119 신고·지원 / 아래:초기소화 중단\n"+(result?"결과 보고":"위치 보고")+"는 "+Key("우클릭")+" → ‘보고’ 열기\n선택 후 Q 놓기 · 선택 없으면 취소";
        }
        string CurrentFeedback(int stage)
        {
            if(RadioOpen)
            {
                var options=radioGroup<0?RadioGroups:radioGroup==0?RadioReports:RadioRequests;
                int selected=radioWheel.Selected;
                return selected<0?"선택 없음 · 지금 Q를 놓으면 취소됩니다.\n메뉴가 바뀐 뒤에는 마우스를 다시 움직이세요.":
                    "현재 선택: "+options[selected]+"\n"+(radioGroup<0?"좌클릭으로 이 묶음을 열기":"Q를 놓으면 이 메시지를 전송합니다");
            }
            if(reportSending&&Step==StationFireStep.Report)return "전송 완료 · 동료가 위치를 확인하고 있습니다.";
            if(Step==StationFireStep.Extinguish)
            {
                if(SafetyCorrection)return "분사 안전 확인 · "+SafetyMessage;
                if(!string.IsNullOrEmpty(Controls.PartFeedback))return Controls.PartFeedback;
                if(!string.IsNullOrEmpty(Controls.InteractionFeedback))return Controls.InteractionFeedback;
                string required=stage==4?"pin":stage==5?"nozzle":stage==7?"lever":null;
                var selected=Controls.HeldPart!=null?Controls.HeldPart:Controls.AimedPart;
                if(required!=null&&selected!=null&&selected.OwnerId==ExtinguisherId&&selected.PartId!=required)
                    return "현재 "+PartLabel(selected.PartId)+"을 보고/잡고 있습니다.\n지금은 "+PartLabel(required)+"을 조준하세요.";
                if(stage==4||stage==5)
                {
                    double amount=Mechanics.Position(ExtinguisherId,required);
                    string value=amount>=.95?"준비 완료 · 좌클릭 놓기":Mathf.RoundToInt((float)amount*100)+"% · "+(Holding(required)?"잡힘":"아직 잡지 않음");
                    return PartLabel(required)+" · "+value+"\n"+Controls.PartInputFeedback;
                }
                if(stage==7)return (State.Entities[ExtinguisherId].Get("mech.flow")>0?"분사 중 · 불꽃과 실제 분말 방향을 확인":"레버를 잡고 좌클릭을 계속 유지")+"\n약제 잔량 "+Mathf.RoundToInt((float)State.Entities[ExtinguisherId].Get("mech.charge")*100)+"% · 놓으면 중지";
                return stage==3?"소화기를 들면 원래 받침대가 비고 본체가 따라 움직입니다.":"불을 지나지 않는 열린 퇴로를 유지하세요.";
            }
            if(Step==StationFireStep.Specialist)return SpecialistReceived?"소방대가 상황을 전달받았습니다.":"표시된 소방대에게 직접 전달해야 완료됩니다.";
            return Step==StationFireStep.Complete?"학습 종료 · 재시작하거나 메뉴로 돌아가세요":"현재 목표가 끝나면 다음 조작을 안내합니다.";
        }
        static string PartLabel(string part)=>part=="pin"?"안전핀":part=="nozzle"?"노즐":part=="lever"?"레버":"소화기 본체";
        string QuickActionText(int stage)
        {
            if(Step==StationFireStep.Complete)return "학습 완료 · 다시 연습하거나 튜토리얼 메뉴로 돌아갈 수 있습니다";
            if(!Captured)return "현재 단계의 설명을 확인하고 ‘계속’을 누르세요";
            if(RadioOpen)return radioGroup<0?"Q 계속 유지 → 위쪽 ‘보고’ 선택 → 좌클릭 한 번":"Q 계속 유지 → 메시지에 주황색 표시 → Q 놓아 전송";
            if(Step==StationFireStep.Extinguish&&!string.IsNullOrEmpty(Controls.PartInputFeedback))return Controls.PartInputFeedback;
            switch(stage){case 1:return "승객을 보고 E · 위험과 열린 통로 안내";case 2:return "Q 유지 → 보고 열기 → 화재 위치 선택 → Q 놓기";case 3:return "빨간 소화기에 가까이 → 본체나 손잡이를 보고 F";case 4:case 5:return "부품 조준 → 좌클릭 유지 + 휠↑ / 표시된 방향 드래그";case 6:return "WASD로 바닥 경로를 따라 대응 위치로 이동";case 7:return Holding("lever")?"좌클릭을 계속 누른 채 마우스로 화점 아래쪽 조준":"레버 조준 → 좌클릭을 누르고 유지";case 8:return "좌클릭을 놓으면 분말과 분사음이 멈춥니다";case 9:return "Q 유지 → 보고 열기 → 아래쪽 결과 보고 → Q 놓기";default:return "도착한 소방대를 보고 E · 확인한 상황 전달";}
        }
    }
}
