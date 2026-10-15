using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ChooGuard.Editor.Twin
{
    /// <summary>Pure JSON patch contract; keeps unknown fields, list order and every unrelated station datum.</summary>
    public static class SourcePdfToiletPointPatch
    {
        static readonly HashSet<string> Allowed = new HashSet<string>(new[] {
            "toilet-1f-source-left-upper", "toilet-1f-source-left-lower",
            "toilet-1f-source-right-upper", "toilet-1f-source-right-lower" }, StringComparer.Ordinal);
        static readonly HashSet<string> Fields = new HashSet<string>(new[] { "id", "kind", "label", "zone", "slot", "position", "anchor", "yaw" }, StringComparer.Ordinal);
        public static JObject BuildCandidate(JObject original, IEnumerable<JObject> replacements)
        {
            var result = (JObject)original.DeepClone(); var points = (JArray)result["points"]; var visited = new HashSet<string>();
            foreach (var replacement in replacements)
            {
                string id = (string)replacement["id"];
                if (!Allowed.Contains(id) || !visited.Add(id) || (string)replacement["kind"] != "Toilet" || replacement.Properties().Any(p => !Fields.Contains(p.Name)))
                    throw new InvalidOperationException("Replacement outside bounded source-toilet contract: " + id);
                var existing = points.OfType<JObject>().SingleOrDefault(p => (string)p["id"] == id);
                if (existing == null) points.Add(replacement.DeepClone());
                else foreach (var field in replacement.Properties()) existing[field.Name] = field.Value.DeepClone();
            }
            AssertPreserved(original, result); return result;
        }
        public static JObject Complement(JObject value)
        {
            var result = (JObject)value.DeepClone();
            result["points"] = new JArray(((JArray)result["points"]).Where(p => !Allowed.Contains((string)p["id"])).Select(p => p.DeepClone()));
            return result;
        }
        public static void AssertPreserved(JObject before, JObject after)
        {
            if (!JToken.DeepEquals(Complement(before), Complement(after))) throw new InvalidOperationException("Another station field/list/order changed.");
            var old = (JArray)before["points"]; var now = (JArray)after["points"];
            if (now.Count < old.Count) throw new InvalidOperationException("Existing station point removed.");
            for (int i = 0; i < old.Count; i++)
            {
                if ((string)old[i]["id"] != (string)now[i]["id"]) throw new InvalidOperationException("Existing station point order/id changed.");
                if (Allowed.Contains((string)old[i]["id"]))
                    foreach (var field in ((JObject)old[i]).Properties().Where(p => !Fields.Contains(p.Name)))
                        if (!JToken.DeepEquals(field.Value, now[i][field.Name])) throw new InvalidOperationException("Unknown existing source-point field changed.");
            }
            for (int i = old.Count; i < now.Count; i++) if (!Allowed.Contains((string)now[i]["id"])) throw new InvalidOperationException("Added unrelated station point.");
            if (now.Select(p => (string)p["id"]).Distinct().Count() != now.Count) throw new InvalidOperationException("Duplicate destination ID.");
            foreach (var p in now) if (Allowed.Contains((string)p["id"]) && (string)p["kind"] != "Toilet") throw new InvalidOperationException("Source point kind must be Toilet.");
        }
    }
}
