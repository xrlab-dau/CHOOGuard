using System.Collections.Generic;
using ChooGuard.App.Fps.Hud;
using ChooGuard.App.Fps.Shell;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Emergency
{
    public sealed partial class CardiacTutorialLesson
    {
        Canvas lessonCanvas, failureCanvas;
        TMP_Text objective, instruction, deviceText, targetLabel, failureText, padRing;
        GameObject timeTransition;
        WorldRouteGuide lessonRoute;
        float nextRoute;
        readonly List<Vector3> routePoints = new List<Vector3>();
        public string ObjectiveText => objective != null ? objective.text : "";
        public string ActionText => instruction != null ? instruction.text : "";

        void CreateLessonView()
        {
            var go = FpsUiFactory.Canvas("심정지 학습 안내", transform, 8);
            lessonCanvas = go.GetComponent<Canvas>();
            var panel = FpsUiFactory.Block(go.transform, "현재 목표 배경", new Vector2(0, 1), new Vector2(24, -125), new Vector2(450, 350), FpsUiFactory.PanelStrong);
            objective = FpsUiFactory.Text(panel.transform, Session.KoreanFont, "현재 목표", new Vector2(0, 1), new Vector2(18, -16), new Vector2(414, 52), 24, TextAlignmentOptions.TopLeft);
            objective.color = FpsUiFactory.Accent;
            instruction = FpsUiFactory.Text(panel.transform, Session.KoreanFont, "이유와 조작", new Vector2(0, 1), new Vector2(18, -78), new Vector2(414, 255), 17, TextAlignmentOptions.TopLeft);
            deviceText = FpsUiFactory.Text(go.transform, Session.KoreanFont, "지속 AED 안내", new Vector2(.5f, 0), new Vector2(0, 155), new Vector2(760, 90), 21, TextAlignmentOptions.Center);
            targetLabel = FpsUiFactory.Label(go.transform, Session.KoreanFont, "목표 위치", Vector2.zero, new Vector2(330, 65), 19);
            targetLabel.color = FpsUiFactory.Accent;
            padRing = FpsUiFactory.Label(go.transform, Session.KoreanFont, "패드 부착 부위", Vector2.zero, new Vector2(90, 90), 58);
            padRing.text = "○"; padRing.color = FpsUiFactory.Accent;
            lessonRoute = WorldRouteGuide.Create(transform);
            timeTransition = FpsUiFactory.Panel(go.transform, "교육용 시간 전환", Vector2.zero, new Vector2(880, 190), FpsUiFactory.PanelStrong).gameObject;
            var transition = FpsUiFactory.Label(timeTransition.transform, Session.KoreanFont, "시간 경과 설명", Vector2.zero, new Vector2(830, 160), 26);
            transition.text = "구급대 도착까지의 시간을 줄여 다음 조작을 익힙니다.\n실제 상황에서는 회복 징후나 구급대 교대까지 압박을 이어갑니다.\n지금도 압박을 계속하세요. 충격이나 시간만으로 회복한 것이 아닙니다.";
            timeTransition.SetActive(false);
            var failure = FpsUiFactory.InteractiveCanvas("심정지 학습 준비 실패", transform, 65);
            failureCanvas = failure.GetComponent<Canvas>();
            var shade = FpsUiFactory.Panel(failure.transform, "배경", Vector2.zero, Vector2.zero, FpsUiFactory.PanelStrong);
            FpsUiFactory.Stretch(shade.rectTransform); shade.raycastTarget = true;
            failureText = FpsUiFactory.Label(failure.transform, Session.KoreanFont, "준비 실패 안내", new Vector2(0, 80), new Vector2(900, 230), 26);
            FpsUiFactory.Button(failure.transform, Session.KoreanFont, "다시 준비", new Vector2(.5f, .5f), new Vector2(-170, -125), new Vector2(300, 56), Restart, 24);
            FpsUiFactory.Button(failure.transform, Session.KoreanFont, "타이틀로", new Vector2(.5f, .5f), new Vector2(170, -125), new Vector2(300, 56), SceneFlow.ToTitle, 24);
            failureCanvas.enabled = false;
        }

        void ShowTimeTransition(bool show) => timeTransition.SetActive(show);

        void RefreshLessonView()
        {
            if (lessonCanvas == null || Session == null) return;
            failureCanvas.enabled = Step == CardiacLessonStep.Failed;
            if (failureCanvas.enabled)
            {
                failureText.text = "학습 환경을 준비하지 못했습니다 · 미이수\n기존 맵에서 대상과 이동 동선을 준비하는 데 실패했습니다.\n다시 준비하거나 타이틀로 돌아가세요.";
                if (!Session.Player.IsPaused) Session.Player.Pause();
            }
            lessonCanvas.enabled = Ready && !Finished && !Session.Player.IsPaused;
            if (!lessonCanvas.enabled) { lessonRoute.Clear(); return; }
            string title, why, action, next;
            Vector3 target = PatientChest; string name = "학습 대상 승객";
            switch (Step)
            {
                case CardiacLessonStep.Response:
                    title = "1 · 반응 확인"; why = "주변 통로는 안전합니다. 쓰러진 사람의 반응을 먼저 확인합니다.";
                    action = "환자 가까이에서 바라보고 E · 불러 반응 확인"; next = "도움 요청"; break;
                case CardiacLessonStep.Help:
                    title = "2 · 119 신고와 도움 요청"; why = "반응이 없습니다. 혼자 장비를 가지러 떠나지 말고 신고와 지원을 요청합니다.";
                    action = "Q 누른 채 ‘요청 · 기관’에 좌클릭 → ‘119 구급’ 선택 후 Q 놓기"; next = "호흡 확인"; break;
                case CardiacLessonStep.Breathing:
                    title = "3 · 정상 호흡 확인"; why = "동료가119 신고를 맡았습니다. 정상 호흡이 없거나 비정상이면 압박이 필요합니다.";
                    action = "환자를 보고 E · 가슴 움직임과 호흡 확인"; next = "가슴압박 조작"; break;
                case CardiacLessonStep.FirstCompressions:
                    title = "4 · 직접 가슴압박"; why = "이 연습은 가슴압박소생술과 AED 조작을 다룹니다. 분당100~120회, 약5cm 깊이로 압박하고 완전히 이완합니다. 정해진 초마다 쉬지 않습니다.";
                    action = (Patient.Compressing ? "압박 중 · 자세 유지. 조작 확인 후에도 계속 압박\n" : "표시된 가슴 옆자리로 가까이 이동 → 환자에 R\n") + "조작 확인 " + firstShownSeconds.ToString("F0") + "/6초 · 중단 지시가 아닙니다."; next = "압박을 유지하며 동료 교대 요청";
                    if (!Patient.Compressing) { target = CompressionApproach; name = "가까운 압박 자리"; } break;
                case CardiacLessonStep.Relief:
                    title = "5 · 동료에게 압박 교대"; why = "동료가 기존 통로를 걸어오는 동안 압박을 계속합니다. 환자 곁에서 실제로 교대하면 AED를 가지러 갑니다.";
                    action = !ReliefRequested ? "압박을 이어가며 Q → 요청 · 기관 → 동료 · 압박 이어받기" :
                        (ColleagueAtPatient ? "동료가 곁에서 교대 자세를 잡고 있습니다.\n" : "동료 이동 중 · 약40초 예상. 압박을 유지하세요.\n") +
                        (Patient.Compressing ? "압박 유지 중 · 교대를 알리기 전까지 계속" : "압박이 중단됐습니다. 가슴 옆에서 환자를 보고 R로 재개");
                    if (ReliefRequested && !Patient.Compressing) { target = CompressionApproach; name = "압박을 이어갈 자리"; }
                    next = "동료의 교대 확인 → 기존 AED 가져오기"; break;
                case CardiacLessonStep.FetchAed:
                    title = "6 · 기존 AED 가져오기"; why = "동료가 압박을 이어가고 있습니다. 임시 장비가 아닌 철도경찰 앞 원래 AED를 사용합니다.";
                    action = "바닥 경로와 표식을 따라 보관함에 접근 → E로 AED 들기"; next = "환자 곁에 AED 내려놓기";
                    target = Cabinet.transform.position; name = "철도경찰 앞 AED"; break;
                case CardiacLessonStep.PlaceAed:
                    title = "7 · AED를 환자 곁에 놓기"; why = "본체와 환자 사이에 패드를 연결할 수 있도록 환자 곁에서 준비합니다.";
                    action = "AED를 든 채 환자를 보고 E · 환자 곁에 놓기\nG로 잘못 놓았다면 R로 다시 들어 옮길 수 있습니다."; next = "뚜껑 열고 패드 꺼내기"; break;
                case CardiacLessonStep.OpenLid:
                    title = "8 · AED 전원 켜기"; why = "뚜껑을 열면 기기가 안내를 시작합니다. 동료 압박은 계속됩니다.";
                    action = "환자 곁 AED 본체를 보고 E · 뚜껑 열기"; next = "패드2장 부착 조작";
                    target = Cabinet.Unit.transform.position; name = "AED 본체"; break;
                case CardiacLessonStep.Pads:
                    title = "9 · 패드2장 부착"; why = ModelLimitation;
                    action = Session.Hands.Pad != null ? "표시된 가슴 부위를 보고 E · 손에 든 패드 부착" : "뚜껑 안 패드에 E → 환자의 표시된 가슴 부위에 E. 한 장씩2회";
                    if (Session.Hands.Pad != null) { target = PadAimPoint; name = Cabinet.Unit.Procedure.Pads == 0 ? "오른쪽 빗장뼈 아래 ◯" : "왼쪽 아래 옆구리 ◯"; }
                    next = "환자에게서 모두 손 떼기";
                    if (Session.Hands.Pad == null) { var pad = Cabinet.Unit.PadInLid(); if (pad != null) target = pad.transform.position; name = "뚜껑 안 패드"; }
                    break;
                case CardiacLessonStep.Clear:
                    title = "10 · 모두 물러나기"; why = "분석과 충격 중에는 환자에게 닿으면 안 됩니다. 동료에게도 손을 떼도록 알립니다.";
                    action = "환자에 E · ‘물러나세요’ 외치기. 실제로 양쪽 압박이 멈춘 뒤 분석합니다."; next = "분석 결과와 충격 지시 확인"; break;
                case CardiacLessonStep.Analysing:
                    title = "10 · AED 분석 중"; why = "기기가 심장 리듬을 분석합니다. 환자 접촉은 분석을 방해합니다.";
                    action = "손을 떼고 기기 안내를 확인하세요. 환자에 R로 압박을 재개하면 분석이 중단됩니다."; next = "충격 지시 시 버튼 누르기"; break;
                case CardiacLessonStep.Shock:
                    title = "11 · 기기 지시대로 충격"; why = "충격 필요 여부는 AED가 판단합니다. 충전 후 버튼이 깜빡입니다.";
                    action = "모두 손을 뗀 상태 유지 → AED의 깜빡이는 충격 버튼에 E"; next = "환자로 시선을 돌려 즉시 압박 재개";
                    target = Cabinet.Unit.ShockControl.transform.position; name = "AED 충격 버튼"; break;
                case CardiacLessonStep.ResumeCompressions:
                    title = "12 · 즉시 압박 재개"; why = "충격 후에도, 충격 불필요 판정 후에도 즉시 압박을 이어갑니다. 회복을 가정하지 않습니다.";
                    action = (Patient.Compressing ? "압박 중 · 구급대가 이어받기 전까지 자세 유지\n" : "가까운 압박 자리로 이동 → 환자에 R · 압박 재개\n") + "조작 연습 " + resumedSeconds.ToString("F0") + "/8초. 이후 도착 대기 시간은 명시해 압축합니다."; next = "구급대에 직접 인계";
                    if (!Patient.Compressing) { target = CompressionApproach; name = "가까운 압박 자리"; } break;
                default:
                    title = "13 · 구급대에 상황 전달"; why = "관찰한 상태와 실제 실시한 처치를 전달합니다. 도착만으로 연습이 자동 완료되지 않습니다.";
                    action = "압박을 유지하며 접근하는 구급대를 기다린 뒤 구급대장에 E · 인계"; next = "학습 기록 확인 / 다시 연습";
                    var lead = Session.Incidents.CardiacLessonLead;
                    if (lead != null) { target = lead.transform.position + Vector3.up * 1.2f; name = "도착한 구급대장"; }
                    break;
            }
            objective.text = title;
            instruction.text = why + "\n\n<color=#FFD07A>" + action + "</color>\n<size=15>다음: " + next + " · Esc 일시정지/연습 중단</size>";
            deviceText.text = Cabinet.Unit.Patient == Patient && !string.IsNullOrEmpty(DeviceInstruction) ? "AED 안내\n" + DeviceInstruction : "";
            targetLabel.gameObject.SetActive(!Session.Wheel.Open && !Patient.Compressing);
            var camera = Session.Player.PlayerCamera;
            padRing.gameObject.SetActive(Step == CardiacLessonStep.Pads && Session.Hands.Pad != null && !Session.Wheel.Open);
            if (camera != null)
            {
                var screen = camera.WorldToViewportPoint(target);
                bool visible = screen.z > 0 && screen.x > .05f && screen.x < .95f && screen.y > .08f && screen.y < .85f;
                targetLabel.rectTransform.anchorMin = targetLabel.rectTransform.anchorMax = visible ? new Vector2(screen.x, screen.y) : new Vector2(.5f, .78f);
                targetLabel.rectTransform.anchoredPosition = visible ? new Vector2(0, 55) : Vector2.zero;
                string direction = screen.z < 0 ? "뒤쪽으로 돌아보세요 · " : screen.y < .08f ? "아래를 보세요 ↓ · " : screen.y > .85f ? "위를 보세요 ↑ · " : screen.x < .5f ? "← 왼쪽 · " : "오른쪽 → · ";
                targetLabel.text = (visible ? "▼ " : direction) + name + "\n" + Vector3.Distance(Session.Player.transform.position, target).ToString("F1") + "m";
                padRing.gameObject.SetActive(padRing.gameObject.activeSelf && visible);
                padRing.rectTransform.anchorMin = padRing.rectTransform.anchorMax = new Vector2(screen.x, screen.y);
                padRing.rectTransform.anchoredPosition = Vector2.zero;
            }
            if (Time.time < nextRoute) return;
            nextRoute = Time.time + .5f;
            routePoints.Clear();
            if (Step == CardiacLessonStep.FetchAed || Step == CardiacLessonStep.PlaceAed)
            {
                var path = new NavMeshPath();
                var destination = Step == CardiacLessonStep.FetchAed ? CabinetApproach : PlayerStart;
                if (NavMesh.CalculatePath(Session.Player.transform.position, destination, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    routePoints.AddRange(path.corners);
                Session.Map.SetRoute(routePoints, name + " · 학습 동선");
            }
            else Session.Map.SetRoute(null, name + " · 현재 학습 목표");
            lessonRoute.SetRoute(routePoints);
        }

        public void ShowResult(string ending)
        {
            var go = FpsUiFactory.InteractiveCanvas("심정지 학습 결과", Session.transform, 60);
            var shade = FpsUiFactory.Panel(go.transform, "배경", Vector2.zero, Vector2.zero, FpsUiFactory.PanelStrong);
            FpsUiFactory.Stretch(shade.rectTransform); shade.raycastTarget = true;
            var title = FpsUiFactory.Label(go.transform, Session.KoreanFont, "결과 제목", new Vector2(0, 280), new Vector2(1040, 70), 36);
            title.text = ending == "tutorial_completed" ? "심정지 조작 학습 완료" : "연습 중단 · 미이수";
            var text = FpsUiFactory.Label(go.transform, Session.KoreanFont, "실제 수행과 한계", new Vector2(0, 10), new Vector2(1000, 430), 22, TextAlignmentOptions.TopLeft);
            var p = Patient?.Aed;
            text.text = "직접 확인: 반응 " + (ResponseChecked ? "확인" : "미확인") + " · 호흡 " + (BreathingChecked ? "확인" : "미확인") +
                "\n119·동료 지원 요청 " + (HelpRequested ? "실시" : "미실시") + " · 동료 교대 " + (ReliefRequested ? "요청" : "미요청") +
                "\n직접 압박 조작 " + firstShownSeconds.ToString("F1") + "초 · AED 이후 압박 " + resumedSeconds.ToString("F1") + "초" +
                "\n패드 부착 조작 이력 " + appliedPads + "/2 · 물러나기 " + (p?.ClearCalls ?? 0) + "회 · 분석 " + (p?.Analyses ?? 0) + "회 · 충격 " + (p?.Shocks ?? 0) + "회\n\n" +
                ModelLimitation + "\n\n압박 시간과 구급대 도착 대기는 조작 학습을 위해 축약했습니다. 실제로는 회복 징후나 구조대 교대까지 압박과 AED 지시를 이어갑니다.\n이 연습은 가슴압박 조작과 AED 조작을 다루며, 인공호흡·임상 깊이·효과 판정이나 자격 인증을 제공하지 않습니다.";
            FpsUiFactory.Button(go.transform, Session.KoreanFont, "다시 연습", new Vector2(.5f, .5f), new Vector2(-170, -300), new Vector2(300, 56), Restart, 24);
            FpsUiFactory.Button(go.transform, Session.KoreanFont, "튜토리얼 목록", new Vector2(.5f, .5f), new Vector2(170, -300), new Vector2(300, 56), SceneFlow.ToTutorialMenu, 24);
        }
    }
}
