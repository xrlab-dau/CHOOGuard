using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Tutorial
{
    public sealed class CrewInspectionView : MonoBehaviour
    {
        private CrewInspectionSession session;
        private TMP_FontAsset font;
        private RectTransform modal, hud;
        private TMP_Text chapter, title, instruction, prompt, marker, phase, observation, voice;
        private TMP_Text targetPin;
        private Image progress, actionProgress;
        private RectTransform observationPanel;
        private RectTransform[] focusCorners;
        private string modalKind;
        public static CrewInspectionView Create(Transform parent, TMP_FontAsset font, CrewInspectionSession session)
        {
            var canvas = FpsUiFactory.InteractiveCanvas("승무원 점검 안내", parent, 30);
            var view = canvas.AddComponent<CrewInspectionView>(); view.session = session; view.font = font;
            var canvasRoot = (RectTransform)canvas.transform;
            var root = FpsUiFactory.Node(canvasRoot, "진행 안내"); FpsUiFactory.Stretch(root); view.hud = root;
            view.chapter = FpsUiFactory.Text(root, font, "진행", new Vector2(.5f, 1), new Vector2(0, -28), new Vector2(780, 36), 18, TextAlignmentOptions.Center);
            view.chapter.color = FpsUiFactory.Accent;
            FpsUiFactory.Block(root, "진행 바탕", new Vector2(.5f, 1), new Vector2(0, -68), new Vector2(460, 3), new Color(1, 1, 1, .2f));
            view.progress = FpsUiFactory.Block(root, "진행 표시", new Vector2(.5f, 1), new Vector2(-230, -68), new Vector2(0, 3), FpsUiFactory.Accent);
            view.progress.rectTransform.pivot = new Vector2(0, 1);
            var panel = FpsUiFactory.Block(root, "단계 안내", new Vector2(0, 0), new Vector2(24, 68), new Vector2(530, 260), FpsUiFactory.PanelColor);
            view.phase = FpsUiFactory.Text(panel.transform, font, "교육 상태", new Vector2(0, 1), new Vector2(20, -14), new Vector2(490, 24), 15, TextAlignmentOptions.TopLeft);
            view.phase.color = FpsUiFactory.Accent;
            view.title = FpsUiFactory.Text(panel.transform, font, "단계 제목", new Vector2(0, 1), new Vector2(20, -46), new Vector2(490, 42), 24, TextAlignmentOptions.TopLeft);
            view.title.fontStyle = FontStyles.Bold;
            view.instruction = FpsUiFactory.Text(panel.transform, font, "설명", new Vector2(0, 1), new Vector2(20, -96), new Vector2(490, 143), 18, TextAlignmentOptions.TopLeft);
            view.instruction.enableAutoSizing = true; view.instruction.fontSizeMin = 16; view.instruction.fontSizeMax = 18;
            FpsUiFactory.Block(root, "조작 안내 바탕", new Vector2(1, 0), new Vector2(-24, 100), new Vector2(620, 65), FpsUiFactory.PanelColor);
            view.prompt = FpsUiFactory.Text(root, font, "조작", new Vector2(1, 0), new Vector2(-24, 100), new Vector2(620, 65), 20, TextAlignmentOptions.Center);
            view.actionProgress = FpsUiFactory.Block(root, "장비 반응 진행", new Vector2(1, 0), new Vector2(-644, 100), new Vector2(0, 3), FpsUiFactory.Accent);
            view.actionProgress.rectTransform.pivot = new Vector2(0, 0);
            var observations = FpsUiFactory.Block(root, "관찰 사항", new Vector2(1, 1), new Vector2(-24, -100), new Vector2(360, 140), FpsUiFactory.PanelColor);
            view.observationPanel = observations.rectTransform;
            view.observation = FpsUiFactory.Text(observations.transform, font, "확인 포인트", new Vector2(0, 1), new Vector2(16, -14), new Vector2(328, 112), 18, TextAlignmentOptions.TopLeft);
            view.voice = FpsUiFactory.Text(root, font, "음성 상태", new Vector2(1, 1), new Vector2(-24, -38), new Vector2(340, 44), 15, TextAlignmentOptions.TopRight);
            FpsUiFactory.Text(root, font, "조준점", new Vector2(.5f, .5f), Vector2.zero, new Vector2(22, 22), 20, TextAlignmentOptions.Center).text = "·";
            FpsUiFactory.Block(root, "기본 조작 바탕", new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(1080, 34), FpsUiFactory.PanelColor);
            FpsUiFactory.Text(root, font, "기본 조작", new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(1080, 30), 16, TextAlignmentOptions.Center).text = "WASD 이동   E 점검·조작   Q 누른 채 무전 선택   Enter 결과 확인   R 안내 다시 듣기   V 음성 켜기·끄기   Esc 일시정지";
            view.focusCorners = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var corner = FpsUiFactory.Node(root, "관찰 모서리 " + i);
                FpsUiFactory.Place(corner, new Vector2(.5f, .5f), Vector2.zero, new Vector2(22, 22));
                FpsUiFactory.Block(corner, "가로", Vector2.zero, Vector2.zero, new Vector2(22, 2), FpsUiFactory.Accent);
                FpsUiFactory.Block(corner, "세로", Vector2.zero, Vector2.zero, new Vector2(2, 22), FpsUiFactory.Accent);
                corner.localRotation = Quaternion.Euler(0, 0, i * 90); view.focusCorners[i] = corner;
            }
            view.marker = FpsUiFactory.Text(root, font, "점검 대상", new Vector2(.5f, .5f), Vector2.zero, new Vector2(340, 64), 18, TextAlignmentOptions.Center);
            view.marker.color = FpsUiFactory.Accent;
            view.targetPin = FpsUiFactory.Text(root, font, "인터폰 실제 위치", new Vector2(.5f, .5f), Vector2.zero, new Vector2(16, 16), 14, TextAlignmentOptions.Center);
            view.targetPin.text = "●"; view.targetPin.color = FpsUiFactory.Accent; view.targetPin.gameObject.SetActive(false);
            view.modal = FpsUiFactory.Node(canvasRoot, "대화 화면"); FpsUiFactory.Stretch(view.modal);
            return view;
        }
        private void LateUpdate()
        {
            var step = session.Current;
            targetPin.gameObject.SetActive(false);
            if (step == null) { marker.gameObject.SetActive(false); return; }
            chapter.text = $"승무원 열차점검   ·   {step.Chapter}   ·   {session.StepIndex + 1}/{session.Plan.Steps.Length}";
            progress.rectTransform.sizeDelta = new Vector2(460f * session.StepIndex / session.Plan.Steps.Length, 3);
            title.text = step.Title;
            phase.text = session.Acting ? "실습 중 · 장비의 반응 관찰" : session.ReadingResult ? "✓ 확인 완료 · 이 행동이 필요한 이유" : "교육 안내 · 확인할 대상과 방법";
            instruction.text = session.ReadingResult ? session.LastResult : step.Instruction;
            prompt.text = session.Acting ? session.ActionFeedback : session.ReadingResult ? "Enter · 관찰 결과를 확인하고 다음으로" : step.HandheldRadio ? session.RadioInstructions : session.Responder.HasTarget ? session.Responder.CurrentPrompt : "노란 표식으로 이동해 대상을 바라보세요";
            actionProgress.rectTransform.sizeDelta = new Vector2(session.Acting ? 620 * session.ActionProgress : 0, 3);
            observationPanel.gameObject.SetActive((session.Acting || session.ReadingResult) && !string.IsNullOrWhiteSpace(step.Observation));
            observation.text = "직접 확인할 포인트\n\n" + step.Observation;
            voice.text = session.VoiceEnabled ? (session.CoachSpeaking ? "교육 안내 음성 재생 중" : "교육 음성 켜짐") + "\nR 다시 듣기 · V 음성 끄기" : "교육 음성 꺼짐\nV 음성 켜기";
            var point = session.Acting || session.ReadingResult ? session.ObservationPoint : session.CurrentPoint;
            ShowFocus(point);
            marker.gameObject.SetActive(point != null && session.Started && !session.ReadingResult && (!session.Acting || session.AwaitingObservation) && !modal.gameObject.activeSelf);
            if (!marker.gameObject.activeSelf) return;
            var camera = session.Responder.PlayerCamera;
            var attention = session.Acting ? session.AttentionTarget : point.Marker;
            Vector3 screen = camera.WorldToViewportPoint((attention != null ? attention.position : point.transform.position) + (point.Id == "intercom" ? Vector3.zero : Vector3.up * .04f));
            bool onScreen = screen.z > 0 && screen.x > .08f && screen.x < .92f && screen.y > .12f && screen.y < .88f;
            if (screen.z < 0) { screen.x = 1 - screen.x; screen.y = .75f; }
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = new Vector2(Mathf.Clamp(screen.x, .12f, .88f), Mathf.Clamp(screen.y, .18f, .85f));
            bool intercomVisible = onScreen && point.Id == "intercom";
            marker.rectTransform.pivot = intercomVisible ? new Vector2(0, .5f) : new Vector2(.5f, .5f);
            marker.alignment = intercomVisible ? TextAlignmentOptions.Left : TextAlignmentOptions.Center;
            marker.rectTransform.anchoredPosition = intercomVisible ? new Vector2(18, 0) : Vector2.zero;
            targetPin.gameObject.SetActive(intercomVisible);
            if (intercomVisible) { targetPin.rectTransform.anchorMin = targetPin.rectTransform.anchorMax = new Vector2(screen.x, screen.y); targetPin.rectTransform.anchoredPosition = Vector2.zero; }
            string label = session.Current.Action == CrewInspectionAction.CabinInspect && session.Acting && attention != null ? attention.name : point.Label;
            marker.text = onScreen ? point.Id == "intercom" ? "인터폰 · E 호출" : "● " + label : (screen.x < .5f ? "← " : "→ ") + label;
        }
        private void ShowFocus(CrewInspectionPoint point)
        {
            bool approachingIntercom = point != null && point.Id == "intercom" && session.Started &&
                Vector3.Distance(session.Responder.PlayerCamera.transform.position, point.transform.position) <= 3f;
            bool show = point != null && (session.Acting || session.ReadingResult || approachingIntercom) && !modal.gameObject.activeSelf;
            var bounds = point != null ? point.GetComponent<Collider>() : null;
            if (bounds == null) show = false;
            Vector2 min = Vector2.one, max = Vector2.zero;
            if (show)
            {
                var b = session.Current.Action == CrewInspectionAction.CabinInspect && session.Acting && session.AttentionTarget != null ? new Bounds(session.AttentionTarget.position, Vector3.one * .3f) : bounds.bounds;
                bool panel = point.FocusSize.x > 0 && point.FocusSize.y > 0 && point.Marker != null;
                for (int i = 0; i < (panel ? 4 : 8); i++)
                {
                    var p = panel ? point.Marker.position + point.transform.right * point.FocusSize.x * ((i & 1) == 0 ? -.5f : .5f) + point.transform.up * point.FocusSize.y * ((i & 2) == 0 ? -.5f : .5f)
                        : b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var screen = session.Responder.PlayerCamera.WorldToViewportPoint(p);
                    if (screen.z <= 0) { show = false; break; }
                    min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
                }
                if (min.x < .03f || max.x > .97f || min.y < .08f || max.y > .92f) show = false;
            }
            for (int i = 0; i < focusCorners.Length; i++)
            {
                var corner = focusCorners[i]; corner.gameObject.SetActive(show);
                if (!show) continue;
                var anchor = i == 0 ? min : i == 1 ? new Vector2(max.x, min.y) : i == 2 ? max : new Vector2(min.x, max.y);
                corner.anchorMin = corner.anchorMax = anchor; corner.anchoredPosition = Vector2.zero;
            }
        }
        public void ShowIntro() => Dialog("intro", "승무원 열차점검", "무전기 준비 → 방송·인터폰 → 객실 점검 → 출입문 조치\n\n비상통화 인터폰의 호출음과 응답을 확인하고,\n객실을 이동하며 설비 관찰과 냉방 조절을 배웁니다.\n약 2분 30초~5분 · 시간 제한과 시험은 없습니다.\n\nR 안내 다시 듣기 · V 음성 켜기/끄기\n열차팀장 업무 일부를 포함한 교육용 재구성입니다.", "안내 시작", session.Begin);
        public void ShowPause() => Dialog("pause", "튜토리얼 일시정지", "현재 단계에서 이어서 진행할 수 있습니다.\nWASD 이동 · 마우스 시점 · E 점검·조작\nQ 누른 채 무전 선택 · Enter 설명 확인", "계속하기", session.Resume);
        public void ShowComplete() => Dialog("complete", "열차점검 완료", "통신 장비와 안전 비품을 확인하고,\n출입문을 점검하면서 발견한 이상을 안내에 따라 조치했습니다.\n\n점검 → 이상 확인 → 출발 보류 → 조치·보고", "카테고리로", session.Exit);
        private void Dialog(string kind, string heading, string body, string primary, UnityEngine.Events.UnityAction action)
        {
            if (modalKind == kind && modal.gameObject.activeSelf) return;
            foreach (Transform child in modal) Destroy(child.gameObject);
            hud.gameObject.SetActive(false);
            modalKind = kind; modal.gameObject.SetActive(true);
            var shade = FpsUiFactory.Block(modal, "음영", Vector2.zero, Vector2.zero, Vector2.zero, new Color(.02f, .03f, .05f, .74f)); FpsUiFactory.Stretch(shade.rectTransform); shade.raycastTarget = true;
            var panel = FpsUiFactory.Node(modal, "설명"); FpsUiFactory.Place(panel, new Vector2(.5f, .5f), Vector2.zero, new Vector2(740, 510));
            var caption = FpsUiFactory.Text(panel, font, "제목", new Vector2(0, 1), Vector2.zero, new Vector2(740, 60), 36, TextAlignmentOptions.TopLeft); caption.text = heading; caption.fontStyle = FontStyles.Bold;
            FpsUiFactory.Text(panel, font, "내용", new Vector2(0, 1), new Vector2(0, -80), new Vector2(740, 255), 20, TextAlignmentOptions.TopLeft).text = body;
            var button = FpsUiFactory.Button(panel, font, primary, new Vector2(0, 0), new Vector2(0, 70), new Vector2(320, 56), action, 22);
            var restart = FpsUiFactory.Button(panel, font, "처음부터", new Vector2(0, 0), Vector2.zero, new Vector2(200, 50), session.Restart, 19);
            button.targetGraphic.CrossFadeColor(button.colors.normalColor, 0, true, true);
            restart.targetGraphic.CrossFadeColor(restart.colors.normalColor, 0, true, true);
            if (kind != "complete")
            {
                var back = FpsUiFactory.Button(panel, font, "카테고리로", new Vector2(0, 0), new Vector2(220, 0), new Vector2(230, 50), session.Exit, 19);
                back.targetGraphic.CrossFadeColor(back.colors.normalColor, 0, true, true);
            }
            button.Select();
        }
        public void HideModal() { modalKind = null; modal.gameObject.SetActive(false); hud.gameObject.SetActive(session.Started && !session.Finished); }
    }
}
