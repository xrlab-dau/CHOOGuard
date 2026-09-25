using System;
using ChooGuard.Contracts.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ChooGuard.App.Fps.Runtime
{
    public sealed class GameplayHud : MonoBehaviour
    {
        private GameplayBootstrap host;
        private TMP_Text status, prompt, detail, future, menuStatus, selection;
        private GameObject menu;
        private TMP_FontAsset font;
        private static readonly string[] VerbLabels = { "관찰", "대기", "이동", "집기", "내려놓기", "열기", "닫기", "격리", "설치", "분리", "체결", "해제", "재검사", "측정", "닦기", "보충", "폐기물 투입", "보고", "도움 요청", "동의", "동행 안내", "실물 인계", "접근 표지", "취소" };
        private float refreshAt;

        public void Initialize(GameplayBootstrap bootstrap, TMP_FontAsset koreanFont)
        {
            host = bootstrap; font = koreanFont;
            if (EventSystem.current == null)
            {
                var events = new GameObject("GameplayEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            var canvas = FpsUiFactory.Canvas("GameplayHudCanvas", transform, 200);
            canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            FpsUiFactory.Panel(canvas.transform, "상태 배경", new Vector2(0, 352), new Vector2(1420, 176), new Color(.025f, .045f, .065f, .94f));
            status = Text(canvas.transform, "상태", new Vector2(-310, 410), new Vector2(780, 44), 22);
            future = Text(canvas.transform, "미래 상태", new Vector2(370, 354), new Vector2(620, 135), 19);
            detail = Text(canvas.transform, "작업과 인물", new Vector2(-330, 333), new Vector2(740, 104), 19);
            prompt = Text(canvas.transform, "조작", new Vector2(0, -345), new Vector2(1400, 180), 20);
            Text(canvas.transform, "조준점", Vector2.zero, new Vector2(25, 25), 24, TextAlignmentOptions.Center).text = "+";
            menu = FpsUiFactory.Panel(canvas.transform, "진입과 일시정지 메뉴", new Vector2(0, -79), new Vector2(1410, 685), new Color(.035f, .065f, .09f, .99f)).gameObject;
            menu.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            Text(menu.transform, "제목", new Vector2(0, 299), new Vector2(1360, 52), 27).text = "CHOOGuard · FPS 조작 / 자율 인물 / 인과 미래";
            menuStatus = Text(menu.transform, "메뉴 상태", new Vector2(0, 242), new Vector2(1360, 60), 18);
            Button(menu.transform, "독립 튜토리얼 시작", new Vector2(-505, 175), new Vector2(300, 48), () => host.BeginTutorial());
            Button(menu.transform, "본세계 · 합성 실습실", new Vector2(-175, 175), new Vector2(300, 48), () => host.BeginMain(false));
            Button(menu.transform, "본세계 · 명시 모델 장면", new Vector2(155, 175), new Vector2(300, 48), () => host.BeginMain(true));
            Button(menu.transform, "계속 / 포인터 잠금", new Vector2(495, 175), new Vector2(300, 48), () => host.Resume());
            var roles = new[] { ActorRole.Maintenance, ActorRole.Cleaning, ActorRole.PassengerService, ActorRole.StationStaff };
            for (int i = 0; i < roles.Length; i++)
            {
                ActorRole role = roles[i];
                Button(menu.transform, RoleLabel(role), new Vector2(-510 + i * 225, 112), new Vector2(205, 42), () => host.SelectRole(role));
            }
            Button(menu.transform, "저장", new Vector2(410, 112), new Vector2(130, 42), () => host.Save());
            Button(menu.transform, "첫 진입으로", new Vector2(565, 112), new Vector2(165, 42), () => host.ReturnToEntry());
            selection = Text(menu.transform, "선택", new Vector2(0, 57), new Vector2(1360, 42), 20);
            var verbs = (ActionVerb[])Enum.GetValues(typeof(ActionVerb));
            for (int i = 0; i < verbs.Length; i++)
            {
                ActionVerb verb = verbs[i];
                Button(menu.transform, VerbLabel(verb), new Vector2(-565 + i % 8 * 162, 4 - i / 8 * 49), new Vector2(151, 40), () => host.SelectVerb(verb));
            }
            Button(menu.transform, "손에 든 도구 사용", new Vector2(-505, -164), new Vector2(300, 42), () => host.SelectHeldTool());
            Button(menu.transform, "작업점 순환", new Vector2(-175, -164), new Vector2(300, 42), () => host.CycleWorkPoint());
            Button(menu.transform, "수신/보관 대상 순환", new Vector2(155, -164), new Vector2(300, 42), () => host.CycleRecipient());
            Button(menu.transform, "자동 대상 동사", new Vector2(495, -164), new Vector2(300, 42), () => host.ClearSelection());
            Button(menu.transform, "튜토리얼 복원점 저장", new Vector2(-440, -224), new Vector2(390, 42), () => host.CaptureTutorial());
            Button(menu.transform, "튜토리얼 되감기", new Vector2(0, -224), new Vector2(390, 42), () => host.RewindTutorial());
            Button(menu.transform, "튜토리얼 종료 · 본세계 복귀", new Vector2(440, -224), new Vector2(390, 42), () => host.ExitTutorial());
            Text(menu.transform, "범위 경고", new Vector2(0, -288), new Vector2(1360, 65), 18).text = "합성 실습은 실제 역사 형상·차종 매뉴얼·격리·토크·소독/안전 승인을 대신하지 않습니다.\nWASD 이동 · 마우스 시점 · E 접수 · 좌클릭/Space 실제 작업 · Enter 확인 · R/F 회전 · 휠 깊이 · Esc 메뉴";
        }
        private void Update()
        {
            if (host == null || Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + .15f;
            menu.SetActive(host.MenuVisible);
            status.text = host.StatusText; detail.text = host.DetailText; future.text = host.FutureText;
            prompt.text = host.PromptText; menuStatus.text = host.Message; selection.text = host.SelectionText;
        }
        private TMP_Text Text(Transform parent, string name, Vector2 offset, Vector2 size, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var text = FpsUiFactory.Label(parent, font, name, offset, size, fontSize, alignment);
            text.richText = false; text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        private void Button(Transform parent, string label, Vector2 offset, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var image = FpsUiFactory.Panel(parent, label, offset, size, new Color(.12f, .23f, .3f)); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = new Color(.7f, .9f, 1); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            Text(image.transform, "제목", Vector2.zero, size - new Vector2(12, 4), 18, TextAlignmentOptions.Center).text = label;
        }
        public static string RoleLabel(ActorRole role)
        {
            switch (role) { case ActorRole.Maintenance: return "정비"; case ActorRole.Cleaning: return "청소"; case ActorRole.PassengerService: return "승무 서비스"; case ActorRole.StationStaff: return "역무"; default: return "이용객"; }
        }
        public static string VerbLabel(ActionVerb verb) => (int)verb >= 0 && (int)verb < VerbLabels.Length ? VerbLabels[(int)verb] : verb.ToString();
    }
}
