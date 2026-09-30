using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The fire alarm receiver (자동화재탐지설비 수신기, a <see cref="StationEquipment"/> of kind <see cref="PanelKind"/>) on the wall of the station office, where someone is always on duty
    /// (NFTC 203 2.2.3.1). Its screen shows what the receiver knows in Korean: the fire signal with the zone of every tripped detector (the same zone names the office reads out on
    /// the radio), a sprinkler flow signal with its zone, the valve-closed supervisory signal of a zone out of service, or "정상". The director writes the lines
    /// (<see cref="Show"/>); nothing here decides anything. The model's own screen carries a baked English boot picture, so the builder swaps its material for a plain dark display
    /// and this draws the text over it.
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class AlarmPanelPoint : MonoBehaviour
    {
        public const string PanelKind = "alarm_panel";

        public enum Tone { Normal, Fire, Supervisory }

        /// <summary>The model's screen in the prefab's own coordinates (metres): its middle and size, and how far in front of the wall the glass sits.</summary>
        public static readonly Vector2 ScreenCentre = new Vector2(.0025f, .1275f), ScreenSize = new Vector2(.128f, .108f);
        public const float ScreenDepth = .1525f;

        private static readonly Color NormalColour = new Color(.35f, 1f, .45f), FireColour = new Color(1f, .27f, .22f), SupervisoryColour = new Color(1f, .85f, .2f);

        private TMP_Text screen;
        private readonly StringBuilder text = new StringBuilder();

        public StationEquipment Equipment { get; private set; }

        /// <summary>What the screen shows now, one line per row (empty until the first <see cref="Show"/>).</summary>
        public string Shown { get; private set; } = "";

        public Tone Signal { get; private set; } = Tone.Normal;

        /// <summary>Puts the text object on the screen with the station's Korean font.</summary>
        public void Bind(TMP_FontAsset font)
        {
            Equipment = GetComponent<StationEquipment>();
            var go = new GameObject("Display");
            go.transform.SetParent(transform, false);
            // TextMeshPro draws its 3D text facing -Z; the screen faces +Z.
            go.transform.SetLocalPositionAndRotation(new Vector3(ScreenCentre.x, ScreenCentre.y, ScreenDepth), Quaternion.Euler(0f, 180f, 0f));
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.fontSize = .13f;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.margin = new Vector4(.004f, .003f, .004f, .003f);
            tmp.rectTransform.sizeDelta = ScreenSize;
            tmp.raycastTarget = false;
            screen = tmp;
            Shown = "";
            Show(Tone.Normal, new List<string>());
        }

        /// <summary>Shows the headline of <paramref name="tone"/> and, under it, the zone or device lines (at most five rows fit); writes only when the content changed.</summary>
        public void Show(Tone tone, IReadOnlyList<string> lines)
        {
            string headline = tone == Tone.Fire ? "화재" : tone == Tone.Supervisory ? "감시" : "정상";
            text.Clear();
            text.Append(headline);
            for (int i = 0; i < lines.Count && i < 5; i++) text.Append('\n').Append(lines[i]);
            string now = text.ToString();
            if (now == Shown && tone == Signal) return;
            Shown = now;
            Signal = tone;
            if (screen == null) return;
            // 제목 줄만 크게 쓴다.
            screen.text = "<size=150%>" + headline + "</size>" + now.Substring(headline.Length);
            screen.color = tone == Tone.Fire ? FireColour : tone == Tone.Supervisory ? SupervisoryColour : NormalColour;
        }
    }
}
