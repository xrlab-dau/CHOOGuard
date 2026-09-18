using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ChooGuard.Foundation.Simulation;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace ChooGuard.Foundation.Tests
{
    [Serializable]
    public sealed class IncidentCatalog
    {
        public string schemaVersion;
        public string catalogId;
        public string catalogVersion;
        public string classification;
        public string disclaimer;
        public string exaStatus;
        public string emptyApplicabilityMeans;
        public SourceEntry[] sources;
        public string[] executionAllowlist;
        public IncidentVariant[] reviewedVariants;
        public IncidentVariant[] unreviewedVariants;

        public IncidentCatalog Clone()
        {
            var json = JsonUtility.ToJson(this);
            return JsonUtility.FromJson<IncidentCatalog>(json);
        }
    }

    [Serializable]
    public sealed class SourceEntry
    {
        public string sourceId;
        public string publisherOperator;
        public string title;
        public string documentRevisionDate;
        public string revisionStatus;
        public string audience;
        public string[] applicableOperators;
        public string[] applicableStations;
        public string[] applicableLines;
        public string[] applicableVehicles;
        public string[] applicableRoles;
        public string[] applicableIncidentKinds;
        public string locator;
        public string originalUrl;
        public string sha256;
        public string[] allowedUse;
        public string[] limits;
        public string retrievedAt;
        public string verification;
        public string procedureAcceptance;
        public string discoveryMethod;
    }

    [Serializable]
    public sealed class IncidentVariant
    {
        public string id;
        public string choiceId;
        public string kind;
        public int kindOrdinal;
        public string regionId;
        public string resourceKey;
        public long onsetTick;
        public long durationTicks;
        public string recoveryCondition;
        public string sourceId;
        public IncidentApplicability applicability;
        public string reviewStatus;
        public string procedureAcceptance;
        public bool isExecutionCandidate;
        public string rationale;
    }

    [Serializable]
    public sealed class IncidentApplicability
    {
        public string @operator;
        public string[] stations;
        public string[] lines;
        public string[] vehicles;
        public string[] roles;
    }

    /// <summary>
    /// Lightweight recursive JSON parser supporting objects, lists, strings, numbers, booleans, and null.
    /// Used for strict Draft 2020-12 JSON schema validation without relying on permissive reflection DTOs.
    /// </summary>
    public static class MiniJsonParser
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON input cannot be empty.");
            int index = 0;
            var result = ParseValue(json, ref index);
            SkipWhitespace(json, ref index);
            if (index != json.Length)
                throw new ArgumentException($"Trailing characters after JSON input at index {index}: '{json.Substring(index, Math.Min(20, json.Length - index))}'");
            return result;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length)
                throw new ArgumentException("Unexpected end of JSON string.");

            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' || c == 'f') return ParseBool(s, ref i);
            if (c == 'n') return ParseNull(s, ref i);
            if (c == '-' || char.IsDigit(c)) return ParseNumber(s, ref i);

            throw new ArgumentException($"Unexpected character '{c}' at index {i}.");
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; // skip '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}')
            {
                i++;
                return dict;
            }

            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"')
                    throw new ArgumentException($"Expected string key in object at index {i}.");
                string key = ParseString(s, ref i);

                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':')
                    throw new ArgumentException($"Expected ':' after key '{key}' at index {i}.");
                i++; // skip ':'

                object val = ParseValue(s, ref i);
                dict[key] = val;

                SkipWhitespace(s, ref i);
                if (i >= s.Length)
                    throw new ArgumentException("Unterminated object.");
                if (s[i] == '}')
                {
                    i++;
                    return dict;
                }
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                throw new ArgumentException($"Expected ',' or '}}' in object at index {i}.");
            }
            throw new ArgumentException("Unterminated object at EOF.");
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // skip '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']')
            {
                i++;
                return list;
            }

            while (i < s.Length)
            {
                object val = ParseValue(s, ref i);
                list.Add(val);

                SkipWhitespace(s, ref i);
                if (i >= s.Length)
                    throw new ArgumentException("Unterminated array.");
                if (s[i] == ']')
                {
                    i++;
                    return list;
                }
                if (s[i] == ',')
                {
                    i++;
                    continue;
                }
                throw new ArgumentException($"Expected ',' or ']' in array at index {i}.");
            }
            throw new ArgumentException("Unterminated array at EOF.");
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // skip opening quote
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (i >= s.Length) throw new ArgumentException("Unterminated escape sequence in string.");
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new ArgumentException("Unterminated unicode escape in string.");
                            string hex = s.Substring(i, 4);
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            i += 4;
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            throw new ArgumentException("Unterminated string at EOF.");
        }

        private static bool ParseBool(string s, ref int i)
        {
            if (s.Substring(i).StartsWith("true"))
            {
                i += 4;
                return true;
            }
            if (s.Substring(i).StartsWith("false"))
            {
                i += 5;
                return false;
            }
            throw new ArgumentException($"Invalid boolean at index {i}.");
        }

        private static object ParseNull(string s, ref int i)
        {
            if (s.Substring(i).StartsWith("null"))
            {
                i += 4;
                return null;
            }
            throw new ArgumentException($"Invalid null literal at index {i}.");
        }

        private static object ParseNumber(string s, ref int i)
        {
            int start = i;
            bool isFloat = false;
            if (s[i] == '-') i++;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i < s.Length && s[i] == '.')
            {
                isFloat = true;
                i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                isFloat = true;
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            string numStr = s.Substring(start, i - start);
            if (isFloat)
                return double.Parse(numStr, System.Globalization.CultureInfo.InvariantCulture);
            return long.Parse(numStr, System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Strict Draft 2020-12 JSON Schema validator tailored for the foundation incident catalog schema.
    /// Validates type, required properties, additionalProperties == false, enums, const, ranges, and $defs/$ref.
    /// </summary>
    public static class FoundationJsonSchemaValidator
    {
        public static void Validate(string schemaJson, string instanceJson)
        {
            var schemaObj = MiniJsonParser.Parse(schemaJson) as Dictionary<string, object>;
            if (schemaObj == null)
                throw new ArgumentException("Schema root must be a JSON object.");
            var instanceObj = MiniJsonParser.Parse(instanceJson);

            Dictionary<string, object> defs = null;
            if (schemaObj.TryGetValue("$defs", out var defsRaw) && defsRaw is Dictionary<string, object> defsDict)
                defs = defsDict;

            ValidateNode(schemaObj, instanceObj, "#", defs);
        }

        private static void ValidateNode(Dictionary<string, object> schema, object inst, string path, Dictionary<string, object> defs)
        {
            if (schema == null) return;

            // $ref resolution
            if (schema.TryGetValue("$ref", out var refRaw) && refRaw is string refStr)
            {
                if (refStr.StartsWith("#/$defs/") && defs != null)
                {
                    string defKey = refStr.Substring("#/$defs/".Length);
                    if (defs.TryGetValue(defKey, out var targetDef) && targetDef is Dictionary<string, object> targetSchema)
                    {
                        ValidateNode(targetSchema, inst, path, defs);
                        return;
                    }
                    throw new ArgumentException($"Unresolved $ref: {refStr} at {path}");
                }
            }

            // const
            if (schema.TryGetValue("const", out var constVal))
            {
                if (!Equals(constVal, inst))
                    throw new ArgumentException($"Schema violation at '{path}': expected const '{constVal}', got '{inst}'");
            }

            // enum
            if (schema.TryGetValue("enum", out var enumRaw) && enumRaw is List<object> enumList)
            {
                bool matched = false;
                foreach (var e in enumList)
                {
                    if (Equals(e, inst)) { matched = true; break; }
                }
                if (!matched)
                    throw new ArgumentException($"Schema violation at '{path}': value '{inst}' is not in enum [{string.Join(", ", enumList)}]");
            }

            // type check
            if (schema.TryGetValue("type", out var typeRaw))
            {
                List<string> allowedTypes = new List<string>();
                if (typeRaw is string singleType)
                    allowedTypes.Add(singleType);
                else if (typeRaw is List<object> typeList)
                    allowedTypes.AddRange(typeList.Select(t => t.ToString()));

                bool typeOk = false;
                foreach (var t in allowedTypes)
                {
                    if (t == "string" && inst is string) typeOk = true;
                    else if (t == "integer" && (inst is long || (inst is double d && Math.Floor(d) == d))) typeOk = true;
                    else if (t == "number" && (inst is long || inst is double)) typeOk = true;
                    else if (t == "boolean" && inst is bool) typeOk = true;
                    else if (t == "array" && inst is List<object>) typeOk = true;
                    else if (t == "object" && inst is Dictionary<string, object>) typeOk = true;
                    else if (t == "null" && inst == null) typeOk = true;
                }

                if (!typeOk)
                    throw new ArgumentException($"Schema violation at '{path}': expected type [{string.Join(", ", allowedTypes)}], got {inst?.GetType().Name ?? "null"} ({inst})");
            }

            // string checks
            if (inst is string s)
            {
                if (schema.TryGetValue("minLength", out var minLenRaw) && minLenRaw is long minLen)
                {
                    if (s.Length < minLen)
                        throw new ArgumentException($"Schema violation at '{path}': string length {s.Length} is less than minLength {minLen}");
                }
            }

            // integer/number checks
            if (inst is long num)
            {
                if (schema.TryGetValue("minimum", out var minRaw) && minRaw is long minVal && num < minVal)
                    throw new ArgumentException($"Schema violation at '{path}': value {num} is less than minimum {minVal}");
                if (schema.TryGetValue("maximum", out var maxRaw) && maxRaw is long maxVal && num > maxVal)
                    throw new ArgumentException($"Schema violation at '{path}': value {num} is greater than maximum {maxVal}");
            }

            // array checks
            if (inst is List<object> list)
            {
                if (schema.TryGetValue("minItems", out var minItemsRaw) && minItemsRaw is long minItems && list.Count < minItems)
                    throw new ArgumentException($"Schema violation at '{path}': array count {list.Count} is less than minItems {minItems}");

                if (schema.TryGetValue("uniqueItems", out var uniqRaw) && uniqRaw is bool uniq && uniq)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var it in list)
                    {
                        string repr = it?.ToString() ?? "null";
                        if (!seen.Add(repr))
                            throw new ArgumentException($"Schema violation at '{path}': array has duplicate item '{repr}'");
                    }
                }

                if (schema.TryGetValue("items", out var itemsRaw) && itemsRaw is Dictionary<string, object> itemSchema)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        ValidateNode(itemSchema, list[i], $"{path}[{i}]", defs);
                    }
                }
            }

            // object checks
            if (inst is Dictionary<string, object> dict)
            {
                if (schema.TryGetValue("required", out var reqRaw) && reqRaw is List<object> reqList)
                {
                    foreach (var r in reqList)
                    {
                        string reqProp = r.ToString();
                        if (!dict.ContainsKey(reqProp))
                            throw new ArgumentException($"Schema violation at '{path}': missing required property '{reqProp}'");
                    }
                }

                Dictionary<string, object> propsDict = null;
                if (schema.TryGetValue("properties", out var propsRaw) && propsRaw is Dictionary<string, object> pd)
                    propsDict = pd;

                if (schema.TryGetValue("additionalProperties", out var addPropRaw) && addPropRaw is bool addProp && !addProp)
                {
                    var allowedProps = propsDict != null ? new HashSet<string>(propsDict.Keys, StringComparer.Ordinal) : new HashSet<string>();
                    foreach (var k in dict.Keys)
                    {
                        if (!allowedProps.Contains(k))
                            throw new ArgumentException($"Schema violation at '{path}': additional property '{k}' not allowed (additionalProperties: false)");
                    }
                }

                if (propsDict != null)
                {
                    foreach (var kvp in propsDict)
                    {
                        if (dict.TryGetValue(kvp.Key, out var propVal) && kvp.Value is Dictionary<string, object> propSchema)
                        {
                            ValidateNode(propSchema, propVal, $"{path}.{kvp.Key}", defs);
                        }
                    }
                }
            }
        }
    }

    public static class IncidentCatalogValidator
    {
        public static readonly string[] ValidThirteenRegions = new[]
        {
            "rolling_stock_mainline",
            "rail_tracks_mainline",
            "rail_platforms_mainline",
            "rail_terminal_public",
            "station_concourse_2f",
            "station_hall_1f",
            "station_ticket_area",
            "forecourt_eurasia",
            "underground_connector",
            "underground_shopping_passage",
            "metro_concourse",
            "metro_platforms",
            "rolling_stock_metro"
        };

        public static readonly string[] ValidIncidentKinds = new[]
        {
            "Fire",
            "PowerLoss",
            "PublicAddressFailure",
            "RouteRestriction",
            "TrainFault",
            "EmergencyStop"
        };

        public static void Validate(IncidentCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentException("Catalog cannot be null.");

            if (catalog.schemaVersion != "1.0")
                throw new ArgumentException($"Invalid schemaVersion: {catalog.schemaVersion}");

            if (catalog.catalogId != "reviewed-incident-catalog")
                throw new ArgumentException($"Invalid catalogId: {catalog.catalogId}");

            if (catalog.catalogVersion != "1.0")
                throw new ArgumentException($"Invalid catalogVersion: {catalog.catalogVersion}");

            if (catalog.classification != "PUBLIC_REFERENCE_AND_SYNTHETIC_METADATA")
                throw new ArgumentException($"Invalid classification: {catalog.classification}");

            if (catalog.disclaimer != "KORAIL 검증 전 예시")
                throw new ArgumentException($"Invalid disclaimer: {catalog.disclaimer}");

            if (catalog.exaStatus != "blocked_403_1010")
                throw new ArgumentException($"Invalid exaStatus: {catalog.exaStatus}");

            if (catalog.emptyApplicabilityMeans != "unspecified_not_universal_permission")
                throw new ArgumentException($"Invalid emptyApplicabilityMeans: {catalog.emptyApplicabilityMeans}. Must be unspecified_not_universal_permission.");

            if (catalog.sources == null || catalog.sources.Length == 0)
                throw new ArgumentException("Catalog sources must not be empty.");

            var registeredSourceIds = new HashSet<string>(StringComparer.Ordinal);
            var sourceMap = new Dictionary<string, SourceEntry>(StringComparer.Ordinal);
            foreach (var s in catalog.sources)
            {
                if (string.IsNullOrWhiteSpace(s.sourceId))
                    throw new ArgumentException("Source entry missing sourceId.");
                if (!registeredSourceIds.Add(s.sourceId))
                    throw new ArgumentException($"Duplicate sourceId registered: {s.sourceId}");
                if (string.IsNullOrWhiteSpace(s.title))
                    throw new ArgumentException($"Source {s.sourceId} missing title.");
                if (string.IsNullOrWhiteSpace(s.publisherOperator))
                    throw new ArgumentException($"Source {s.sourceId} missing publisherOperator.");
                sourceMap[s.sourceId] = s;
            }

            if (catalog.executionAllowlist == null || catalog.executionAllowlist.Length == 0)
                throw new ArgumentException("executionAllowlist must not be empty.");

            var allowlistSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in catalog.executionAllowlist)
            {
                if (string.IsNullOrWhiteSpace(item))
                    throw new ArgumentException("executionAllowlist item cannot be empty.");
                if (!allowlistSet.Add(item))
                    throw new ArgumentException($"Duplicate choiceId in executionAllowlist: {item}");
            }

            if (catalog.reviewedVariants == null || catalog.reviewedVariants.Length == 0)
                throw new ArgumentException("reviewedVariants must not be empty.");

            if (catalog.unreviewedVariants == null)
                throw new ArgumentException("unreviewedVariants array must not be null.");

            var allIds = new HashSet<string>(StringComparer.Ordinal);
            var allChoiceIds = new HashSet<string>(StringComparer.Ordinal);
            var reviewedChoiceIds = new HashSet<string>(StringComparer.Ordinal);

            // Validate reviewed variants
            foreach (var v in catalog.reviewedVariants)
            {
                ValidateVariant(v, registeredSourceIds, sourceMap, isReviewed: true);
                if (!allIds.Add(v.id))
                    throw new ArgumentException($"Duplicate variant id: {v.id}");
                if (!allChoiceIds.Add(v.choiceId))
                    throw new ArgumentException($"Duplicate choiceId: {v.choiceId}");
                if (!reviewedChoiceIds.Add(v.choiceId))
                    throw new ArgumentException($"Duplicate reviewed choiceId: {v.choiceId}");

                if (!allowlistSet.Contains(v.choiceId))
                    throw new ArgumentException($"Reviewed variant choiceId {v.choiceId} missing from executionAllowlist.");
            }

            // 1:1 match between reviewedVariants and executionAllowlist
            if (allowlistSet.Count != reviewedChoiceIds.Count || !allowlistSet.SetEquals(reviewedChoiceIds))
                throw new ArgumentException("executionAllowlist must correspond 1:1 to reviewedVariants.");

            // Validate unreviewed variants
            foreach (var v in catalog.unreviewedVariants)
            {
                ValidateVariant(v, registeredSourceIds, sourceMap, isReviewed: false);
                if (!allIds.Add(v.id))
                    throw new ArgumentException($"Duplicate variant id: {v.id}");
                if (!allChoiceIds.Add(v.choiceId))
                    throw new ArgumentException($"Duplicate choiceId: {v.choiceId}");

                if (allowlistSet.Contains(v.choiceId))
                    throw new ArgumentException($"Unreviewed variant {v.choiceId} cannot be in executionAllowlist.");
            }
        }

        public static void ValidateVariant(IncidentVariant v, HashSet<string> registeredSources, Dictionary<string, SourceEntry> sourceMap, bool isReviewed)
        {
            if (v == null)
                throw new ArgumentException("Variant cannot be null.");

            if (string.IsNullOrWhiteSpace(v.id))
                throw new ArgumentException("Variant id cannot be empty.");
            if (string.IsNullOrWhiteSpace(v.choiceId))
                throw new ArgumentException("Variant choiceId cannot be empty.");

            // Review status checks
            if (isReviewed)
            {
                if (v.reviewStatus != "reviewed")
                    throw new ArgumentException($"Reviewed variant {v.id} has invalid reviewStatus: {v.reviewStatus}");
                if (v.procedureAcceptance != "provisional_synthetic_reviewed")
                    throw new ArgumentException($"Reviewed variant {v.id} must have procedureAcceptance provisional_synthetic_reviewed, got {v.procedureAcceptance}");
                if (!v.isExecutionCandidate)
                    throw new ArgumentException($"Reviewed variant {v.id} must have isExecutionCandidate == true.");
            }
            else
            {
                if (v.reviewStatus != "unreviewed")
                    throw new ArgumentException($"Unreviewed variant {v.id} has invalid reviewStatus: {v.reviewStatus}");
                if (v.procedureAcceptance != "pending_operator_review")
                    throw new ArgumentException($"Unreviewed variant {v.id} must have procedureAcceptance pending_operator_review, got {v.procedureAcceptance}");
                if (v.isExecutionCandidate)
                    throw new ArgumentException($"Unreviewed variant {v.id} cannot have isExecutionCandidate == true.");
            }

            // Kind and ordinal
            if (!ValidIncidentKinds.Contains(v.kind))
                throw new ArgumentException($"Undefined FoundationIncidentKind: {v.kind}");
            if (!Enum.TryParse(v.kind, out FoundationIncidentKind parsedKind))
                throw new ArgumentException($"Cannot parse FoundationIncidentKind: {v.kind}");
            if (v.kindOrdinal < 0 || v.kindOrdinal > 5 || v.kindOrdinal != (int)parsedKind)
                throw new ArgumentException($"Mismatched or invalid kindOrdinal {v.kindOrdinal} for kind {v.kind}");

            // Regions
            if (!ValidThirteenRegions.Contains(v.regionId))
                throw new ArgumentException($"Unknown or orphan regionId: {v.regionId}");
            if (!ValidThirteenRegions.Contains(v.resourceKey))
                throw new ArgumentException($"Unknown or orphan resourceKey: {v.resourceKey}");

            // Ticks
            if (v.onsetTick < 0)
                throw new ArgumentException($"Invalid onsetTick: {v.onsetTick}");
            if (v.durationTicks <= 0)
                throw new ArgumentException($"Invalid durationTicks: {v.durationTicks}");

            if (string.IsNullOrWhiteSpace(v.recoveryCondition))
                throw new ArgumentException($"Missing recoveryCondition for {v.id}");

            // Source check
            if (string.IsNullOrWhiteSpace(v.sourceId))
                throw new ArgumentException($"Missing sourceId for variant {v.id}");
            if (!registeredSources.Contains(v.sourceId))
                throw new ArgumentException($"Unregistered sourceId: {v.sourceId} for variant {v.id}");

            // Applicability
            if (v.applicability == null)
                throw new ArgumentException($"Missing applicability for variant {v.id}");

            var op = v.applicability.@operator;
            if (op != "KORAIL" && op != "HUMETRO" && op != "SR" && op != "SYNTHETIC")
                throw new ArgumentException($"Invalid applicability operator: {op}");

            var source = sourceMap[v.sourceId];

            // 1. SR data conflation rule
            if (source.publisherOperator == "SR" || v.sourceId == "SR-LARGE-ACCIDENT-20240226")
            {
                if (op == "KORAIL")
                    throw new ArgumentException($"Cannot conflate SR operational data ({v.sourceId}) as KORAIL SOP.");
                if (op != "SR")
                    throw new ArgumentException($"SR operational source {v.sourceId} must have operator SR, got {op}");
            }

            // 2. Synthetic variant authority hallucination check
            if (v.sourceId == "CG-SYNTHETIC-FUNCTIONAL-INCIDENTS-V1" || source.publisherOperator == "SYNTHETIC")
            {
                if (op != "SYNTHETIC")
                    throw new ArgumentException($"Synthetic variant {v.id} cannot claim official operator authority ({op}).");
                if (v.procedureAcceptance != "provisional_synthetic_reviewed")
                    throw new ArgumentException($"Synthetic variant {v.id} cannot claim official procedure acceptance ({v.procedureAcceptance}).");
            }

            // 3. Universal permission rule for empty applicability
            bool emptyStations = v.applicability.stations == null || v.applicability.stations.Length == 0;
            bool emptyLines = v.applicability.lines == null || v.applicability.lines.Length == 0;
            bool emptyRoles = v.applicability.roles == null || v.applicability.roles.Length == 0;
            if (emptyStations && emptyLines && emptyRoles)
            {
                if (v.isExecutionCandidate && op != "SYNTHETIC")
                    throw new ArgumentException("Empty applicability cannot confer universal execution permission for operator procedures.");
            }

            // 4. Strict publisher-operator cross-boundary check
            if (source.publisherOperator == "HUMETRO" && op != "HUMETRO")
                throw new ArgumentException($"Cannot conflate HUMETRO source {source.sourceId} as {op} applicability.");
            if (source.publisherOperator == "KORAIL" && op != "KORAIL")
                throw new ArgumentException($"Cannot conflate KORAIL source {source.sourceId} as {op} applicability.");
            if (op == "SYNTHETIC" && source.publisherOperator != "SYNTHETIC")
                throw new ArgumentException($"Cannot claim official operator source ({source.sourceId}) as SYNTHETIC applicability without synthetic provenance.");

            // 5. Cross-validate source.applicableOperators
            if (source.applicableOperators != null && source.applicableOperators.Length > 0)
            {
                if (!source.applicableOperators.Contains(op))
                    throw new ArgumentException($"Variant {v.id} operator '{op}' is not permitted by source {source.sourceId} applicableOperators [{string.Join(", ", source.applicableOperators)}].");
            }

            // 6. Source procedure acceptance vs Variant execution candidacy & review status
            if (source.procedureAcceptance == "pending_operator_review")
            {
                if (v.isExecutionCandidate)
                    throw new ArgumentException($"Variant {v.id} citing pending source {source.sourceId} cannot be marked as execution candidate.");
                if (v.reviewStatus == "reviewed")
                    throw new ArgumentException($"Variant {v.id} citing pending source {source.sourceId} cannot be marked as reviewed.");
                if (v.procedureAcceptance != "pending_operator_review")
                    throw new ArgumentException($"Variant {v.id} citing pending source {source.sourceId} must have procedureAcceptance pending_operator_review, got {v.procedureAcceptance}.");
            }
            if (v.isExecutionCandidate && source.procedureAcceptance != "provisional_synthetic_reviewed")
            {
                throw new ArgumentException($"Execution candidate {v.id} cannot reference source {source.sourceId} with procedureAcceptance {source.procedureAcceptance}.");
            }
        }

        public static IncidentSchedule BuildSchedule(IncidentCatalog catalog, string profileId = "reviewed-incident-schedule",
            int maxActive = 2, long firstOnsetTick = 1200, long minQuietTicks = 1200, long maxQuietTicks = 3600)
        {
            Validate(catalog);
            var choices = catalog.reviewedVariants.Select(v => new IncidentChoice
            {
                Id = v.choiceId,
                ResourceKey = v.resourceKey,
                Kind = (FoundationIncidentKind)v.kindOrdinal
            }).ToArray();

            return new IncidentSchedule
            {
                ProfileId = profileId,
                MaximumActive = maxActive,
                FirstOnsetTick = firstOnsetTick,
                MinimumQuietTicks = minQuietTicks,
                MaximumQuietTicks = maxQuietTicks,
                Choices = choices
            };
        }
    }

    [TestFixture]
    public sealed class Fmp09aIncidentCatalogTests
    {
        private static string ResolveCatalogPath()
        {
            const string relPath = "foundation/scenarios/reviewed-incident-catalog.json";
            if (File.Exists(relPath))
                return Path.GetFullPath(relPath);

            var package = PackageInfo.FindForAssembly(typeof(Fmp09aIncidentCatalogTests).Assembly);
            if (package != null)
            {
                var pkgRelPath = Path.Combine(package.resolvedPath, "..", "..", relPath);
                if (File.Exists(pkgRelPath))
                    return Path.GetFullPath(pkgRelPath);
            }

            var dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, relPath);
                if (File.Exists(candidate))
                    return candidate;
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }

            throw new FileNotFoundException($"Cannot locate {relPath}");
        }

        private static string ResolveSchemaPath()
        {
            const string relPath = "schemas/foundation-incident-catalog.schema.json";
            if (File.Exists(relPath))
                return Path.GetFullPath(relPath);

            var dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, relPath);
                if (File.Exists(candidate))
                    return candidate;
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }

            throw new FileNotFoundException($"Cannot locate {relPath}");
        }

        private static IncidentCatalog LoadCanonicalCatalog()
        {
            var path = ResolveCatalogPath();
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonUtility.FromJson<IncidentCatalog>(json);
        }

        [Test]
        public void CanonicalCatalogFileExistsAndLoads()
        {
            var path = ResolveCatalogPath();
            Assert.That(File.Exists(path), Is.True, "Catalog file must exist at " + path);
            var catalog = LoadCanonicalCatalog();
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.schemaVersion, Is.EqualTo("1.0"));
            Assert.That(catalog.catalogId, Is.EqualTo("reviewed-incident-catalog"));
            Assert.That(catalog.sources.Length, Is.EqualTo(6));
            Assert.That(catalog.executionAllowlist.Length, Is.EqualTo(7));
            Assert.That(catalog.reviewedVariants.Length, Is.EqualTo(7));
            Assert.That(catalog.unreviewedVariants.Length, Is.EqualTo(5));

            Assert.DoesNotThrow(() => IncidentCatalogValidator.Validate(catalog));
        }

        [Test]
        public void CanonicalCatalogMatchesJsonSchemaIntegrity()
        {
            var schemaPath = ResolveSchemaPath();
            var catalogPath = ResolveCatalogPath();
            Assert.That(File.Exists(schemaPath), Is.True, "JSON schema file must exist: " + schemaPath);
            Assert.That(File.Exists(catalogPath), Is.True, "Catalog file must exist: " + catalogPath);

            var schemaText = File.ReadAllText(schemaPath, Encoding.UTF8);
            var catalogText = File.ReadAllText(catalogPath, Encoding.UTF8);

            // Execute real Draft 2020-12 schema validation on the raw JSON document
            Assert.DoesNotThrow(() => FoundationJsonSchemaValidator.Validate(schemaText, catalogText));

            var catalog = LoadCanonicalCatalog();
            var allowlist = new HashSet<string>(catalog.executionAllowlist);
            Assert.That(catalog.reviewedVariants.All(v => allowlist.Contains(v.choiceId)), Is.True);
            Assert.That(catalog.unreviewedVariants.All(v => !allowlist.Contains(v.choiceId)), Is.True);
        }

        [Test]
        public void CanonicalCatalogDrivesIncidentDirectorDeterministically()
        {
            var catalog = LoadCanonicalCatalog();
            var schedule = IncidentCatalogValidator.BuildSchedule(catalog, firstOnsetTick: 2, minQuietTicks: 1, maxQuietTicks: 3);
            Assert.That(schedule.Choices.Length, Is.EqualTo(7));

            var director = new IncidentDirector(schedule, 424242UL);
            Assert.That(director.AdvanceOne(), Is.Null, "No onset before tick 2");
            var ep1 = director.AdvanceOne();
            Assert.That(ep1, Is.Not.Null, "First onset at tick 2");
            Assert.That(schedule.Choices.Any(c => c.Id == ep1.ChoiceId), Is.True);

            // Advance further to accumulate active episodes bounded by MaximumActive (2)
            for (var i = 0; i < 20; i++)
            {
                director.AdvanceOne();
            }
            Assert.That(director.Active.Count, Is.EqualTo(2));

            var checkpoint = director.ExportCheckpoint();
            var restored = new IncidentDirector(schedule, 9999UL);
            restored.Restore(checkpoint);
            Assert.That(restored.ExportCheckpoint(), Is.EqualTo(checkpoint), "Restored director matches checkpoint state exactly.");

            // Mitigate and complete an episode
            var activeId = director.Active[0].Id;
            director.Mitigate(activeId);
            restored.Mitigate(activeId);
            director.Complete(activeId);
            restored.Complete(activeId);

            for (var i = 0; i < 10; i++)
            {
                director.AdvanceOne();
                restored.AdvanceOne();
            }
            Assert.That(director.ExportCheckpoint(), Is.EqualTo(restored.ExportCheckpoint()), "Post-recovery execution is deterministic.");
        }



        // --------------------------------------------------------------------------------
        // Schema Validation Negative Probes
        // --------------------------------------------------------------------------------

        [Test]
        public void Negative_SchemaValidation_RefusesMissingRequiredKey()
        {
            var schemaText = File.ReadAllText(ResolveSchemaPath(), Encoding.UTF8);
            var catalogObj = MiniJsonParser.Parse(File.ReadAllText(ResolveCatalogPath(), Encoding.UTF8)) as Dictionary<string, object>;
            Assert.That(catalogObj, Is.Not.Null);

            // Remove required "revisionStatus" from the first source entry
            var sources = catalogObj["sources"] as List<object>;
            var firstSource = sources[0] as Dictionary<string, object>;
            firstSource.Remove("revisionStatus");

            var serialized = MiniJsonSerialize(catalogObj);
            var ex = Assert.Throws<ArgumentException>(() => FoundationJsonSchemaValidator.Validate(schemaText, serialized));
            Assert.That(ex.Message, Does.Contain("missing required property 'revisionStatus'"));
        }

        [Test]
        public void Negative_SchemaValidation_RefusesAdditionalProperty()
        {
            var schemaText = File.ReadAllText(ResolveSchemaPath(), Encoding.UTF8);
            var catalogObj = MiniJsonParser.Parse(File.ReadAllText(ResolveCatalogPath(), Encoding.UTF8)) as Dictionary<string, object>;
            Assert.That(catalogObj, Is.Not.Null);

            // Inject unexpected additional property into root
            catalogObj["unexpectedMaliciousKey"] = true;

            var serialized = MiniJsonSerialize(catalogObj);
            var ex = Assert.Throws<ArgumentException>(() => FoundationJsonSchemaValidator.Validate(schemaText, serialized));
            Assert.That(ex.Message, Does.Contain("additional property 'unexpectedMaliciousKey' not allowed"));
        }

        [Test]
        public void Negative_SchemaValidation_RefusesWrongType()
        {
            var schemaText = File.ReadAllText(ResolveSchemaPath(), Encoding.UTF8);
            var catalogObj = MiniJsonParser.Parse(File.ReadAllText(ResolveCatalogPath(), Encoding.UTF8)) as Dictionary<string, object>;
            Assert.That(catalogObj, Is.Not.Null);

            // Set kindOrdinal to a string instead of integer
            var reviewed = catalogObj["reviewedVariants"] as List<object>;
            var firstVariant = reviewed[0] as Dictionary<string, object>;
            firstVariant["kindOrdinal"] = "not_an_integer";

            var serialized = MiniJsonSerialize(catalogObj);
            var ex = Assert.Throws<ArgumentException>(() => FoundationJsonSchemaValidator.Validate(schemaText, serialized));
            Assert.That(ex.Message, Does.Contain("expected type [integer]"));
        }

        [Test]
        public void Negative_SchemaValidation_RefusesInvalidEnum()
        {
            var schemaText = File.ReadAllText(ResolveSchemaPath(), Encoding.UTF8);
            var catalogObj = MiniJsonParser.Parse(File.ReadAllText(ResolveCatalogPath(), Encoding.UTF8)) as Dictionary<string, object>;
            Assert.That(catalogObj, Is.Not.Null);

            // Set reviewStatus to an invalid enum value
            var reviewed = catalogObj["reviewedVariants"] as List<object>;
            var firstVariant = reviewed[0] as Dictionary<string, object>;
            firstVariant["reviewStatus"] = "super_reviewed_unofficial";

            var serialized = MiniJsonSerialize(catalogObj);
            var ex = Assert.Throws<ArgumentException>(() => FoundationJsonSchemaValidator.Validate(schemaText, serialized));
            Assert.That(ex.Message, Does.Contain("is not in enum"));
        }

        // --------------------------------------------------------------------------------
        // Operator & Source Boundary Negative Probes
        // --------------------------------------------------------------------------------

        [Test]
        public void Negative_RefuseOperatorCrossConflation_HumetroToKorail()
        {
            // Case 1: Changing reviewed variant to cite HUMETRO source with KORAIL operator
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[0].sourceId = "HUMETRO-PASSENGER-EMERGENCY";
            catalog.reviewedVariants[0].applicability.@operator = "KORAIL";

            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("HUMETRO").And.Contains("KORAIL"));

            // Case 2: Changing unreviewed HUMETRO variant operator to KORAIL
            var catalog2 = LoadCanonicalCatalog().Clone();
            var humetroVariant = catalog2.unreviewedVariants.First(v => v.sourceId == "HUMETRO-PASSENGER-EMERGENCY");
            humetroVariant.applicability.@operator = "KORAIL";

            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("HUMETRO").And.Contains("KORAIL"));
        }

        [Test]
        public void Negative_RefuseOperatorCrossConflation_KorailToHumetro()
        {
            // Case 3: Changing unreviewed KORAIL variant operator to HUMETRO
            var catalog = LoadCanonicalCatalog().Clone();
            var korailVariant = catalog.unreviewedVariants.First(v => v.sourceId == "KORAIL-PUBLIC-REPORT");
            korailVariant.applicability.@operator = "HUMETRO";

            var ex = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex.Message, Does.Contain("KORAIL").And.Contains("HUMETRO"));
        }

        [Test]
        public void Negative_RefusePendingOperatorSourceAsExecutionCandidate()
        {
            // Case 4: Promoting a real operator source variant (pending_operator_review) to isExecutionCandidate = true
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[0].sourceId = "HUMETRO-PASSENGER-EMERGENCY";
            catalog.reviewedVariants[0].applicability.@operator = "HUMETRO"; // matching operator, but source is pending_operator_review!

            var ex = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex.Message, Does.Contain("pending source").Or.Contain("execution candidate"));
        }

        [Test]
        public void Negative_RefuseUnreviewedVariantInExecutionAllowlist()
        {
            // Probe 1: Unreviewed variant choiceId added to execution allowlist
            var catalog = LoadCanonicalCatalog().Clone();
            var modifiedAllowlist = new List<string>(catalog.executionAllowlist) { "unreviewed-korail-public-report" };
            catalog.executionAllowlist = modifiedAllowlist.ToArray();

            var ex = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex.Message, Does.Contain("Unreviewed variant").Or.Contain("executionAllowlist"));

            // Probe 1b: Unreviewed variant marked as isExecutionCandidate == true
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.unreviewedVariants[0].isExecutionCandidate = true;
            Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
        }

        [Test]
        public void Negative_RefuseDuplicateChoiceOrIncidentId()
        {
            // Probe 2a: Duplicate choiceId
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[1].choiceId = catalog.reviewedVariants[0].choiceId;
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("Duplicate choiceId").IgnoreCase);

            // Probe 2b: Duplicate variant id
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.reviewedVariants[1].id = catalog2.reviewedVariants[0].id;
            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("Duplicate variant id").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseConflationOfSRDataAsKORARILSOP()
        {
            // Probe 3: Conflating SR data as KORAIL SOP
            var catalog = LoadCanonicalCatalog().Clone();
            var srVariant = catalog.unreviewedVariants.First(v => v.sourceId == "SR-LARGE-ACCIDENT-20240226");
            srVariant.applicability.@operator = "KORAIL";

            var ex = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex.Message, Does.Contain("SR").And.Contains("KORAIL"));
        }

        [Test]
        public void Negative_RefuseUniversalPermissionForEmptyApplicability()
        {
            // Probe 4a: emptyApplicabilityMeans changed from unspecified_not_universal_permission
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.emptyApplicabilityMeans = "universal_permission";
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("emptyApplicabilityMeans"));

            // Probe 4b: Empty applicability variant attempting universal candidate promotion
            var catalog2 = LoadCanonicalCatalog().Clone();
            var emptyVariant = catalog2.unreviewedVariants.First(v =>
                v.applicability.stations.Length == 0 &&
                v.applicability.lines.Length == 0 &&
                v.applicability.roles.Length == 0);
            emptyVariant.isExecutionCandidate = true;
            emptyVariant.reviewStatus = "reviewed";
            emptyVariant.procedureAcceptance = "provisional_synthetic_reviewed";
            catalog2.unreviewedVariants = catalog2.unreviewedVariants.Where(v => v.choiceId != emptyVariant.choiceId).ToArray();
            catalog2.reviewedVariants = catalog2.reviewedVariants.Concat(new[] { emptyVariant }).ToArray();
            var allowlist = new List<string>(catalog2.executionAllowlist) { emptyVariant.choiceId };
            catalog2.executionAllowlist = allowlist.ToArray();

            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("universal").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseUnknownOrOrphanRegionResourceKey()
        {
            // Probe 5a: Unknown regionId
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[0].regionId = "unknown_phantom_corridor";
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("regionId").IgnoreCase);

            // Probe 5b: Unknown resourceKey
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.reviewedVariants[0].resourceKey = "orphan_track_segment";
            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("resourceKey").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseMissingOrUnregisteredSourceId()
        {
            // Probe 6a: Unregistered sourceId
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[0].sourceId = "UNREGISTERED-UNKNOWN-SOURCE";
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("sourceId").IgnoreCase);

            // Probe 6b: Empty sourceId
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.reviewedVariants[0].sourceId = "";
            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("sourceId").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseUndefinedIncidentKindEnum()
        {
            // Probe 7a: Undefined kind enum name
            var catalog = LoadCanonicalCatalog().Clone();
            catalog.reviewedVariants[0].kind = "UndefinedIncidentKind";
            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("FoundationIncidentKind").IgnoreCase);

            // Probe 7b: Out of range kindOrdinal
            var catalog2 = LoadCanonicalCatalog().Clone();
            catalog2.reviewedVariants[0].kindOrdinal = 99;
            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("kindOrdinal").IgnoreCase);

            // Probe 7c: Mismatched kind and kindOrdinal
            var catalog3 = LoadCanonicalCatalog().Clone();
            catalog3.reviewedVariants[0].kindOrdinal = 3; // kind is Fire (0), ordinal mismatch
            var ex3 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog3));
            Assert.That(ex3.Message, Does.Contain("kindOrdinal").IgnoreCase);
        }

        [Test]
        public void Negative_RefuseHallucinatedOfficialAuthorityForSyntheticVariant()
        {
            // Probe 8a: Synthetic variant claiming official operator authority
            var catalog = LoadCanonicalCatalog().Clone();
            var syntheticVariant = catalog.reviewedVariants.First(v => v.sourceId == "CG-SYNTHETIC-FUNCTIONAL-INCIDENTS-V1");
            syntheticVariant.applicability.@operator = "KORAIL";

            var ex1 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog));
            Assert.That(ex1.Message, Does.Contain("Synthetic variant").And.Contains("operator authority"));

            // Probe 8b: Synthetic variant claiming official accepted procedure
            var catalog2 = LoadCanonicalCatalog().Clone();
            var syntheticVariant2 = catalog2.reviewedVariants.First(v => v.sourceId == "CG-SYNTHETIC-FUNCTIONAL-INCIDENTS-V1");
            syntheticVariant2.procedureAcceptance = "accepted_railway_sop";

            var ex2 = Assert.Throws<ArgumentException>(() => IncidentCatalogValidator.Validate(catalog2));
            Assert.That(ex2.Message, Does.Contain("procedureAcceptance"));
        }

        private static string MiniJsonSerialize(object obj)
        {
            if (obj == null) return "null";
            if (obj is string str) return "\"" + str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
            if (obj is bool b) return b ? "true" : "false";
            if (obj is long l) return l.ToString();
            if (obj is double d) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (obj is List<object> list)
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(MiniJsonSerialize(list[i]));
                }
                sb.Append("]");
                return sb.ToString();
            }
            if (obj is Dictionary<string, object> dict)
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var kvp in dict)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("\"").Append(kvp.Key).Append("\":").Append(MiniJsonSerialize(kvp.Value));
                }
                sb.Append("}");
                return sb.ToString();
            }
            return obj.ToString();
        }
    }
}
