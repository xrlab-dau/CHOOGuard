using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChooGuard.App.Fps.Hud
{
    public enum RadioChannel { Office, Control, Colleague, Fire, Police, Medical, Announcement, Self, TrainCrew }

    /// <summary>Top-right radio/announcement feed. Messages age out; the newest sits on top.</summary>
    public sealed class RadioFeed : MonoBehaviour
    {
        private const int MaxLines = 5;
        private const float Lifetime = 11f, Width = 430;

        private sealed class Line { public RectTransform Rect; public TMP_Text Text; public Image Tag; public TMP_Text TagText; public float Born; }
        private readonly List<Line> lines = new List<Line>();
        private TMP_FontAsset font;

        public static RadioFeed Create(RectTransform parent, TMP_FontAsset font)
        {
            var root = FpsUiFactory.Node(parent, "무전 피드");
            FpsUiFactory.Place(root, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(Width, 300));
            var feed = root.gameObject.AddComponent<RadioFeed>();
            feed.font = font;
            return feed;
        }

        public static string ChannelName(RadioChannel channel)
        {
            switch (channel)
            {
                case RadioChannel.Office: return "역무실";
                case RadioChannel.Control: return "관제";
                case RadioChannel.Colleague: return "동료";
                case RadioChannel.Fire: return "119";
                case RadioChannel.Police: return "철도경찰";
                case RadioChannel.Medical: return "구급";
                case RadioChannel.Announcement: return "안내방송";
                case RadioChannel.TrainCrew: return "열차팀장";
                default: return "나";
            }
        }

        private static Color ChannelColour(RadioChannel channel)
        {
            switch (channel)
            {
                case RadioChannel.Fire: return FpsUiFactory.Danger;
                case RadioChannel.Police: case RadioChannel.Medical: return FpsUiFactory.Staff;
                case RadioChannel.Announcement: return FpsUiFactory.Accent;
                case RadioChannel.Self: return new Color(.75f, .75f, .75f, 1);
                default: return new Color(.4f, .8f, .55f, 1);
            }
        }

        /// <summary>Raised for every new line (the session plays the announcement chime or the radio squelch).</summary>
        public event System.Action<RadioChannel> Pushed;

        public void Push(RadioChannel channel, string message)
        {
            Pushed?.Invoke(channel);
            if (lines.Count >= MaxLines) { Destroy(lines[lines.Count - 1].Rect.gameObject); lines.RemoveAt(lines.Count - 1); }
            var rect = FpsUiFactory.Node(transform, "무전");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
            var back = rect.gameObject.AddComponent<Image>();
            back.color = new Color(0, 0, 0, .45f);
            back.raycastTarget = false;
            var tag = FpsUiFactory.Block(rect, "채널", new Vector2(0, 1), new Vector2(8, -7), new Vector2(70, 22), ChannelColour(channel));
            var tagText = FpsUiFactory.Label(tag.transform, font, "채널명", Vector2.zero, new Vector2(70, 22), 13);
            tagText.text = ChannelName(channel);
            tagText.color = new Color(.05f, .05f, .05f, 1);
            tagText.fontStyle = FontStyles.Bold;
            var text = FpsUiFactory.Text(rect, font, "내용", new Vector2(0, 1), new Vector2(86, -6), new Vector2(Width - 96, 60), 16, TextAlignmentOptions.TopLeft);
            text.text = message;
            text.ForceMeshUpdate();
            float height = Mathf.Max(36, text.preferredHeight + 14);
            rect.sizeDelta = new Vector2(Width, height);
            text.rectTransform.sizeDelta = new Vector2(Width - 96, height - 8);
            lines.Insert(0, new Line { Rect = rect, Text = text, Tag = tag, TagText = tagText, Born = Time.unscaledTime });
            Layout();
        }

        private void Layout()
        {
            float y = 0;
            foreach (var line in lines) { line.Rect.anchoredPosition = new Vector2(0, -y); y += line.Rect.sizeDelta.y + 6; }
        }

        private void Update()
        {
            bool changed = false;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - lines[i].Born;
                if (age > Lifetime) { Destroy(lines[i].Rect.gameObject); lines.RemoveAt(i); changed = true; continue; }
                float alpha = Mathf.Clamp01((Lifetime - age) / 1.5f);
                lines[i].Text.alpha = alpha;
                lines[i].TagText.alpha = alpha;
                var tagColour = lines[i].Tag.color; tagColour.a = alpha; lines[i].Tag.color = tagColour;
            }
            if (changed) Layout();
        }
    }
}
