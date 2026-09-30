using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The fire alarm receiver of the station office as a real object: its screen shows the same zones the office reads out over the radio (the zone of every tripped detector, the zone
    /// of a sprinkler flow switch, the zone of a closed valve) and goes back to "정상" when the receiver is reset. It only mirrors the state the director already keeps.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private AlarmPanelPoint receiver;
        private readonly List<string> receiverLines = new List<string>();
        private float nextReceiverShow;

        private void BindReceiver()
        {
            var equipment = EquipmentRegistry.OfKind(AlarmPanelPoint.PanelKind).OrderBy(e => e.Id, System.StringComparer.Ordinal).FirstOrDefault();
            receiver = equipment != null ? equipment.GetComponent<AlarmPanelPoint>() : null;
            if (receiver != null) receiver.Bind(session.KoreanFont);
        }

        private void ShowReceiver()
        {
            if (receiver == null || Time.time < nextReceiverShow) return;
            nextReceiverShow = Time.time + .25f;
            receiverLines.Clear();
            foreach (var zone in detectors.Where(d => d.Tripped).Select(d => d.ZoneName).Distinct().OrderBy(z => z, System.StringComparer.Ordinal).Take(4))
                receiverLines.Add("감지 " + zone);
            foreach (var valve in valves.Values.Where(v => flowSignals.Contains(v.Key)).OrderBy(v => v.Key, System.StringComparer.Ordinal).Take(3))
                receiverLines.Add("유수 " + valve.ZoneName);
            var tone = receiverLines.Count > 0 ? AlarmPanelPoint.Tone.Fire : AlarmPanelPoint.Tone.Normal;
            foreach (var valve in valves.Values.Where(v => v.Closed).OrderBy(v => v.Key, System.StringComparer.Ordinal).Take(3))
            {
                receiverLines.Add("폐쇄 " + valve.ZoneName);
                if (tone == AlarmPanelPoint.Tone.Normal) tone = AlarmPanelPoint.Tone.Supervisory;
            }
            receiver.Show(tone, receiverLines);
        }
    }
}
