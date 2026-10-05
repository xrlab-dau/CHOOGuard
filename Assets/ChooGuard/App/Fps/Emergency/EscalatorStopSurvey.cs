using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Where the emergency stop buttons of the station's escalators really are, as observed (Resources/EscalatorStops.json). A button is
    /// built only where a record ties that button to that landing (<see cref="Record.placementEvidence"/>: a photo or video of it, or a
    /// field measurement); a landing without a record gets no button (docs/CHOOGuard_Story_Plan_v5/WORLD_OBJECT_PROVENANCE.md). Positions
    /// are relative to the escalator's own landing in the twin, so a record says which numbers were observed and which are estimates.
    /// </summary>
    public static class EscalatorStopSurvey
    {
        /// <summary>The TextAsset under Resources holding the records.</summary>
        public const string Resource = "EscalatorStops";

        /// <summary>One observed button.</summary>
        [Serializable]
        public sealed class Record
        {
            /// <summary>Twin escalator id (station-points.json).</summary>
            public string escalator = "";
            /// <summary>"bottom" or "top": the landing by floor height.</summary>
            public string landing = "";
            /// <summary>"right" or "left" of a person riding in the direction the steps move.</summary>
            public string side = "";
            /// <summary>What it is mounted on as seen: newel, deck, skirt, other.</summary>
            public string mount = "";
            /// <summary>Centre height above the landing floor (m).</summary>
            public float height;
            /// <summary>From the belt end at this landing (where the steps meet the landing floor, about the comb plate) out onto the landing (m; negative toward the steps).</summary>
            public float along;
            /// <summary>Sideways from the escalator's centre line toward <see cref="side"/> (m).</summary>
            public float offset;
            /// <summary>What ties this exact button to this landing: source, time or frame, what is seen. Empty: not built.</summary>
            public string placementEvidence = "";
            /// <summary>Which numbers were observed and which are estimates ([INFERENCE]).</summary>
            public string positionBasis = "";

            public bool HasPlacementEvidence => !string.IsNullOrWhiteSpace(placementEvidence);
            public bool AtBottom => landing == "bottom";
            public bool OnLeft => side == "left";
        }

        [Serializable]
        private sealed class File
        {
            public int version;
            public string note;
            public Record[] stops;
        }

        /// <summary>The records under Resources (none when the file is missing).</summary>
        public static IReadOnlyList<Record> LoadResource() => Load(Resources.Load<TextAsset>(Resource));

        /// <summary>
        /// The records that may be built: those with evidence and a landing and side the builder understands. Others are left out
        /// (a malformed one with a warning, so a typo does not silently drop a surveyed button).
        /// </summary>
        public static IReadOnlyList<Record> Load(TextAsset asset)
        {
            var built = new List<Record>();
            if (asset == null) return built;
            var file = JsonUtility.FromJson<File>(asset.text);
            if (file == null || file.version != 1 || file.stops == null) throw new InvalidOperationException(Resource + ".json 구조 오류 (version 1, stops 필요)");
            foreach (var record in file.stops)
            {
                if (record == null || !record.HasPlacementEvidence) continue;
                bool landing = record.landing == "bottom" || record.landing == "top";
                bool side = record.side == "left" || record.side == "right";
                if (string.IsNullOrEmpty(record.escalator) || !landing || !side)
                {
                    Debug.LogWarning("[" + Resource + "] 기록을 읽지 못해 버튼을 두지 않음: " + record.escalator + " " + record.landing + " " + record.side);
                    continue;
                }
                built.Add(record);
            }
            return built;
        }
    }
}
