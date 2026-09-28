using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace ChooGuard.App.Fps.Hud
{
    /// <summary>Hold-Tab situation board (PUBG inventory position): what is known, what I did, who is coming.</summary>
    public sealed class BoardOverlay : MonoBehaviour
    {
        public sealed class Column
        {
            public string Title;
            public readonly List<string> Lines = new List<string>();
        }

        private Canvas canvas;
        private TMP_Text[] titles, bodies;
        private readonly StringBuilder builder = new StringBuilder(512);

        public bool Visible => canvas.enabled;

        public static BoardOverlay Create(Transform parent, TMP_FontAsset font)
        {
            var canvasObject = FpsUiFactory.Canvas("상황판", parent, 20);
            var board = canvasObject.AddComponent<BoardOverlay>();
            board.canvas = canvasObject.GetComponent<Canvas>();
            var root = (RectTransform)canvasObject.transform;
            var shade = FpsUiFactory.Panel(root, "음영", Vector2.zero, Vector2.zero, new Color(0, 0, 0, .55f));
            FpsUiFactory.Stretch(shade.rectTransform);
            var heading = FpsUiFactory.Text(root, font, "제목", new Vector2(.5f, 1), new Vector2(0, -70), new Vector2(1180, 40), 26, TextAlignmentOptions.Left);
            heading.text = "근무 상황판";
            heading.fontStyle = FontStyles.Bold;
            board.titles = new TMP_Text[3];
            board.bodies = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var x = -400 + i * 400;
                var panel = FpsUiFactory.Block(root, "칸 " + i, new Vector2(.5f, .5f), new Vector2(x, -20), new Vector2(380, 620), FpsUiFactory.PanelColor);
                board.titles[i] = FpsUiFactory.Text(panel.rectTransform, font, "칸 제목", new Vector2(0, 1), new Vector2(18, -14), new Vector2(344, 30), 19, TextAlignmentOptions.TopLeft);
                board.titles[i].color = FpsUiFactory.Accent;
                board.titles[i].fontStyle = FontStyles.Bold;
                board.bodies[i] = FpsUiFactory.Text(panel.rectTransform, font, "칸 내용", new Vector2(0, 1), new Vector2(18, -56), new Vector2(344, 548), 16, TextAlignmentOptions.TopLeft);
                board.bodies[i].overflowMode = TextOverflowModes.Truncate;
            }
            board.canvas.enabled = false;
            return board;
        }

        public void Show(IReadOnlyList<Column> columns)
        {
            for (int i = 0; i < titles.Length; i++)
            {
                if (i >= columns.Count) { titles[i].text = bodies[i].text = ""; continue; }
                titles[i].text = columns[i].Title;
                builder.Clear();
                if (columns[i].Lines.Count == 0) builder.Append("<color=#FFFFFF80>없음</color>");
                foreach (var line in columns[i].Lines) builder.Append(line).Append('\n');
                bodies[i].text = builder.ToString();
            }
            canvas.enabled = true;
        }

        public void Hide() => canvas.enabled = false;
    }
}
