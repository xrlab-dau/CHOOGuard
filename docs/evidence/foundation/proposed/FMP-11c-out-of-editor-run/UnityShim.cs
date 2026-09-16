using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

// SHIM, NOT UNITY. Replaces UnityEngine.JsonUtility for the out-of-editor harness only.
// Unity's JsonUtility is native code and cannot run outside the editor; this is a
// reflection-based equivalent for the plain [Serializable] field graph used by
// foundation/world/connected-world-profile.json. Unity's real JsonUtility semantics
// (exact field-name match, unknown keys ignored, arrays of objects) are mirrored; nothing
// else is. If this shim and Unity's JsonUtility disagree, this harness is wrong, not Unity.
namespace UnityEngine
{
    public static class JsonUtility
    {
        public static T FromJson<T>(string json)
        {
            var value = JsonMini.Parse(json);
            return (T)Convert(value, typeof(T));
        }

        private static object Convert(object json, Type type)
        {
            if (json == null) return null;
            if (type == typeof(string)) return (string)json;
            if (type.IsArray)
            {
                var items = (List<object>)json;
                var element = type.GetElementType();
                var array = Array.CreateInstance(element, items.Count);
                for (var i = 0; i < items.Count; i++) array.SetValue(Convert(items[i], element), i);
                return array;
            }
            if (type.IsEnum) return Enum.Parse(type, (string)json);
            if (type.IsPrimitive)
            {
                if (type == typeof(bool)) return (bool)json;
                return System.Convert.ChangeType(json, type, CultureInfo.InvariantCulture);
            }
            var map = (Dictionary<string, object>)json;
            var isStruct = type.IsValueType;
            var boxed = isStruct ? Activator.CreateInstance(type) : Activator.CreateInstance(type, true);
            foreach (var pair in map)
            {
                var field = type.GetField(pair.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) continue;
                field.SetValue(boxed, Convert(pair.Value, field.FieldType));
            }
            return boxed;
        }
    }

    internal static class JsonMini
    {
        public static object Parse(string text)
        {
            var i = 0;
            var value = ParseValue(text, ref i);
            Skip(text, ref i);
            return value;
        }

        private static object ParseValue(string s, ref int i)
        {
            Skip(s, ref i);
            if (s[i] == '{') return ParseObject(s, ref i);
            if (s[i] == '[') return ParseArray(s, ref i);
            if (s[i] == '"') return ParseString(s, ref i);
            if (s[i] == 't') { i += 4; return true; }
            if (s[i] == 'f') { i += 5; return false; }
            if (s[i] == 'n') { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            i++;
            Skip(s, ref i);
            if (s[i] == '}') { i++; return map; }
            while (true)
            {
                Skip(s, ref i);
                var key = ParseString(s, ref i);
                Skip(s, ref i);
                i++; // ':'
                map[key] = ParseValue(s, ref i);
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // '}'
                return map;
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ParseValue(s, ref i));
                Skip(s, ref i);
                if (s[i] == ',') { i++; continue; }
                i++; // ']'
                return list;
            }
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4; break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }

        private static object ParseNumber(string s, ref int i)
        {
            var start = i;
            while (i < s.Length && "-+.eE0123456789".IndexOf(s[i]) >= 0) i++;
            var text = s.Substring(start, i - start);
            if (text.IndexOf('.') < 0 && text.IndexOf('e') < 0 && text.IndexOf('E') < 0
                && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
            return double.Parse(text, CultureInfo.InvariantCulture);
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
        }
    }
}
