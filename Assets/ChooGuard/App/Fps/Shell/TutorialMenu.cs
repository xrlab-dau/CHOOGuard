using System;
using ChooGuard.App.Fps.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Shell
{
    public sealed class TutorialMenu : MonoBehaviour
    {
        private Button first;
        public static TutorialMenu Create(Transform parent, TMP_FontAsset font, TutorialCatalog catalog,
            Action<TutorialCategory> start, Action back)
        {
            var root = FpsUiFactory.Node(parent, "튜토리얼 카테고리");
            FpsUiFactory.Stretch(root);
            var menu = root.gameObject.AddComponent<TutorialMenu>();
            var shade = FpsUiFactory.Block(root, "배경 음영", Vector2.zero, Vector2.zero, Vector2.zero, new Color(.02f, .03f, .05f, .68f));
            FpsUiFactory.Stretch(shade.rectTransform); shade.raycastTarget = true;
            var panel = FpsUiFactory.Node(root, "카테고리 목록");
            FpsUiFactory.Place(panel, new Vector2(.5f, .5f), Vector2.zero, new Vector2(900, 620));
            var heading = FpsUiFactory.Text(panel, font, "제목", new Vector2(0, 1), Vector2.zero, new Vector2(900, 62), 36, TextAlignmentOptions.TopLeft);
            heading.text = "튜토리얼"; heading.fontStyle = FontStyles.Bold;
            var description = FpsUiFactory.Text(panel, font, "안내", new Vector2(0, 1), new Vector2(0, -70), new Vector2(900, 55), 19, TextAlignmentOptions.TopLeft);
            description.text = "처음 배우는 업무를 순서대로 안내합니다. 원하는 카테고리를 선택하세요.";
            description.color = FpsUiFactory.TextDim;
            var scrollRoot = FpsUiFactory.Node(panel, "스크롤");
            FpsUiFactory.Place(scrollRoot, new Vector2(0, 1), new Vector2(0, -145), new Vector2(900, 370));
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = FpsUiFactory.Node(scrollRoot, "표시 영역"); FpsUiFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var surface = viewport.gameObject.AddComponent<Image>(); surface.color = new Color(1, 1, 1, .015f);
            var list = FpsUiFactory.Node(viewport, "항목");
            list.anchorMin = new Vector2(0, 1); list.anchorMax = Vector2.one; list.pivot = new Vector2(.5f, 1); list.sizeDelta = Vector2.zero;
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = list;
            string reason = "튜토리얼 자료를 불러오지 못했습니다.";
            if (catalog != null && catalog.Validate(out reason))
                foreach (var category in catalog.Categories)
                {
                    var selected = category;
                    var card = FpsUiFactory.Button(list, font, category.Title, new Vector2(0, 1), Vector2.zero, new Vector2(880, 150), () => start?.Invoke(selected), 25);
                    card.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
                    var caption = card.GetComponentInChildren<TMP_Text>();
                    caption.alignment = TextAlignmentOptions.TopLeft; caption.margin = new Vector4(24, 16, 24, 0);
                    caption.fontStyle = FontStyles.Bold;
                    var detail = FpsUiFactory.Text(card.transform, font, "내용", new Vector2(0, 1), new Vector2(24, -58), new Vector2(800, 72), 18, TextAlignmentOptions.TopLeft);
                    detail.text = category.Description + "\n" + category.Duration + " · 시간 제한 없음"; detail.color = FpsUiFactory.TextDim;
                    if (menu.first == null) menu.first = card;
                }
            else FpsUiFactory.Text(list, font, "오류", new Vector2(0, 1), Vector2.zero, new Vector2(880, 100), 20, TextAlignmentOptions.TopLeft).text = reason;
            FpsUiFactory.Button(panel, font, "시작 화면으로", new Vector2(0, 0), Vector2.zero, new Vector2(260, 56), () => back?.Invoke(), 20);
            menu.Hide(); return menu;
        }
        public void Show() { gameObject.SetActive(true); first?.Select(); }
        public void Hide() => gameObject.SetActive(false);
    }
}
