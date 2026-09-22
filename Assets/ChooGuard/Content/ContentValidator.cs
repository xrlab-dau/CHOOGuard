using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ChooGuard.Contracts;

namespace ChooGuard.Content
{
    public enum ContentDocumentKind { Site, Scenario, RevisionCatalog }
    public enum ContentErrorCode
    {
        InvalidJson, EmptyInput, TrailingData, DuplicateProperty, UnknownProperty, MissingProperty,
        WrongType, InvalidString, InvalidId, InvalidRevision, NumberOutOfRange, LimitExceeded,
        DuplicateId, UnknownReference, MissingPortalEndpoint, MissingSourceReference, RevisionMismatch, UnknownRevision
    }

    public sealed class ContentRevisionRef
    {
        public string Id { get; }
        public long Revision { get; }
        public ContentRevisionRef(string id, long revision)
        {
            Id = new StableId(id).Value;
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            Revision = revision;
        }
    }

    public sealed class ContentValidationError
    {
        public ContentDocumentKind Document { get; }
        public ContentErrorCode Code { get; }
        public string JsonPointer { get; }
        public string Message { get; }
        internal ContentValidationError(ContentDocumentKind document, ContentErrorCode code, string pointer, string message)
        {
            Document = document;
            Code = code;
            JsonPointer = pointer;
            Message = message;
        }
    }

    public sealed class ContentValidationResult
    {
        public bool IsValid { get; }
        public SiteBundle Site { get; }
        public ScenarioSpec Scenario { get; }
        public IReadOnlyList<ContentValidationError> Errors { get; }
        internal ContentValidationResult(SiteBundle site, ScenarioSpec scenario, IEnumerable<ContentValidationError> errors)
        {
            Errors = ContentCopies.Freeze(errors);
            IsValid = Errors.Count == 0 && site != null && scenario != null;
            // 오류가 있는 그래프의 일부를 성공 DTO로 노출하지 않는다.
            Site = IsValid ? site : null;
            Scenario = IsValid ? scenario : null;
        }
    }

    public sealed class SiteBundle
    {
        public string Id { get; }
        public long Revision { get; }
        public IReadOnlyList<ContentFrame> Frames { get; }
        public IReadOnlyList<ContentRegion> Regions { get; }
        public IReadOnlyList<ContentPortal> Portals { get; }
        public IReadOnlyList<ContentEntity> Entities { get; }
        public IReadOnlyList<ContentRevisionRef> SourceRefs { get; }
        public IReadOnlyList<ContentQualification> Qualification { get; }
        internal SiteBundle(string id, long revision, IEnumerable<ContentFrame> frames,
            IEnumerable<ContentRegion> regions, IEnumerable<ContentPortal> portals, IEnumerable<ContentEntity> entities,
            IEnumerable<ContentRevisionRef> sourceRefs, IEnumerable<ContentQualification> qualification)
        {
            Id = id;
            Revision = revision;
            Frames = ContentCopies.Freeze(frames);
            Regions = ContentCopies.Freeze(regions);
            Portals = ContentCopies.Freeze(portals);
            Entities = ContentCopies.Freeze(entities);
            SourceRefs = ContentCopies.Freeze(sourceRefs);
            Qualification = ContentCopies.Freeze(qualification);
        }
    }

    public sealed class ContentFrame
    {
        public string Id { get; }
        internal ContentFrame(string id) { Id = id; }
    }
    public sealed class ContentRegion
    {
        public string Id { get; }
        public string FrameId { get; }
        internal ContentRegion(string id, string frameId) { Id = id; FrameId = frameId; }
    }
    public sealed class ContentPortal
    {
        public string Id { get; }
        public string FromRegionId { get; }
        public string ToRegionId { get; }
        internal ContentPortal(string id, string fromRegionId, string toRegionId)
        {
            Id = id; FromRegionId = fromRegionId; ToRegionId = toRegionId;
        }
    }
    public sealed class ContentEntity
    {
        public string Id { get; }
        public string FrameId { get; }
        public string RegionId { get; }
        internal ContentEntity(string id, string frameId, string regionId)
        {
            Id = id; FrameId = frameId; RegionId = regionId;
        }
    }
    public sealed class ContentQualification
    {
        public string Id { get; }
        public IReadOnlyList<string> SourceRefIds { get; }
        internal ContentQualification(string id, IEnumerable<string> sourceRefIds)
        {
            Id = id; SourceRefIds = ContentCopies.Freeze(sourceRefIds);
        }
    }
    public sealed class ScenarioSpec
    {
        public string Id { get; }
        public long Revision { get; }
        public ContentRevisionRef Site { get; }
        internal ScenarioSpec(string id, long revision, ContentRevisionRef site)
        {
            Id = id; Revision = revision; Site = site;
        }
    }

    internal static class ContentCopies
    {
        // 읽기 전용 인터페이스뿐 아니라 저장소도 복사하여 호출자의 변경을 차단한다.
        internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(new List<T>(values));
        }
    }

    /// <summary>자료 참조 골격만 검사한다. 실행 가능성, 출처 승인 또는 현장 자격을 판정하지 않는다.</summary>
    public static class ContentValidator
    {
        private const int MaximumDocumentLength = 1048576;
        private const int MaximumDepth = 32;
        private const int MaximumArrayLength = 4096;

        public static ContentValidationResult Validate(string siteJson, string scenarioJson,
            IReadOnlyList<ContentRevisionRef> knownSourceRevisions)
        {
            var errors = new List<ContentValidationError>();
            var site = ReadDocument(siteJson, ContentDocumentKind.Site, MapSite, errors);
            var scenario = ReadDocument(scenarioJson, ContentDocumentKind.Scenario, MapScenario, errors);
            var catalog = ReadCatalog(knownSourceRevisions, errors);
            if (errors.Count == 0) CheckRelations(site, scenario, catalog, errors);
            return new ContentValidationResult(site, scenario, errors);
        }

        private static T ReadDocument<T>(string text, ContentDocumentKind document, Func<Node, T> map,
            List<ContentValidationError> errors) where T : class
        {
            try
            {
                return map(new JsonReader(text).Read());
            }
            catch (WireFailure failure)
            {
                errors.Add(new ContentValidationError(document, failure.Code, failure.Pointer, failure.Message));
                return null;
            }
        }

        private static Dictionary<string, HashSet<long>> ReadCatalog(IReadOnlyList<ContentRevisionRef> input,
            List<ContentValidationError> errors)
        {
            var catalog = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
            if (input == null)
            {
                Add(errors, ContentDocumentKind.RevisionCatalog, ContentErrorCode.WrongType, "");
                return catalog;
            }
            if (input.Count > MaximumArrayLength)
            {
                Add(errors, ContentDocumentKind.RevisionCatalog, ContentErrorCode.LimitExceeded, "");
                return catalog;
            }
            // 외부 목록의 소유권을 복사한다. 원소 자체는 sealed immutable DTO다.
            var copy = new ContentRevisionRef[input.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = input[i];
            for (var i = 0; i < copy.Length; i++)
            {
                var item = copy[i];
                if (item == null)
                {
                    Add(errors, ContentDocumentKind.RevisionCatalog, ContentErrorCode.WrongType, Index("", i));
                    break;
                }
                HashSet<long> revisions;
                if (!catalog.TryGetValue(item.Id, out revisions))
                {
                    revisions = new HashSet<long>();
                    catalog.Add(item.Id, revisions);
                }
                if (!revisions.Add(item.Revision))
                {
                    Add(errors, ContentDocumentKind.RevisionCatalog, ContentErrorCode.DuplicateId, Index("", i) + "/id");
                    break;
                }
            }
            return catalog;
        }

        private static void CheckRelations(SiteBundle site, ScenarioSpec scenario,
            Dictionary<string, HashSet<long>> catalog, List<ContentValidationError> errors)
        {
            var frames = UniqueIds(site.Frames, value => value.Id, "/frames", errors);
            var regions = UniqueIds(site.Regions, value => value.Id, "/regions", errors);
            UniqueIds(site.Portals, value => value.Id, "/portals", errors);
            UniqueIds(site.Entities, value => value.Id, "/entities", errors);
            var sources = UniqueIds(site.SourceRefs, value => value.Id, "/sourceRefs", errors);
            UniqueIds(site.Qualification, value => value.Id, "/qualification", errors);
            for (var i = 0; i < site.Regions.Count; i++)
                RequireReference(frames, site.Regions[i].FrameId, Index("/regions", i) + "/frameId", ContentErrorCode.UnknownReference, errors);
            for (var i = 0; i < site.Portals.Count; i++)
            {
                var portal = site.Portals[i];
                var pointer = Index("/portals", i);
                RequireReference(regions, portal.FromRegionId, pointer + "/fromRegionId", ContentErrorCode.MissingPortalEndpoint, errors);
                RequireReference(regions, portal.ToRegionId, pointer + "/toRegionId", ContentErrorCode.MissingPortalEndpoint, errors);
            }
            for (var i = 0; i < site.Entities.Count; i++)
            {
                var entity = site.Entities[i];
                var pointer = Index("/entities", i);
                RequireReference(frames, entity.FrameId, pointer + "/frameId", ContentErrorCode.UnknownReference, errors);
                RequireReference(regions, entity.RegionId, pointer + "/regionId", ContentErrorCode.UnknownReference, errors);
            }
            for (var i = 0; i < site.SourceRefs.Count; i++)
            {
                var source = site.SourceRefs[i];
                HashSet<long> revisions;
                if (!catalog.TryGetValue(source.Id, out revisions))
                    Add(errors, ContentDocumentKind.Site, ContentErrorCode.UnknownReference, Index("/sourceRefs", i) + "/id");
                else if (!revisions.Contains(source.Revision))
                    Add(errors, ContentDocumentKind.Site, ContentErrorCode.UnknownRevision, Index("/sourceRefs", i) + "/revision");
            }
            for (var i = 0; i < site.Qualification.Count; i++)
            {
                var ids = site.Qualification[i].SourceRefIds;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var j = 0; j < ids.Count; j++)
                {
                    var pointer = Index(Index("/qualification", i) + "/sourceRefIds", j);
                    if (!seen.Add(ids[j])) Add(errors, ContentDocumentKind.Site, ContentErrorCode.DuplicateId, pointer);
                    RequireReference(sources, ids[j], pointer, ContentErrorCode.MissingSourceReference, errors);
                }
            }
            // 외부 catalog는 sourceRefs 전용이다. scenario는 반드시 전달된 site에 결속한다.
            if (!StringComparer.Ordinal.Equals(scenario.Site.Id, site.Id))
                Add(errors, ContentDocumentKind.Scenario, ContentErrorCode.UnknownReference, "/site/id");
            else if (scenario.Site.Revision != site.Revision)
                Add(errors, ContentDocumentKind.Scenario, ContentErrorCode.RevisionMismatch, "/site/revision");
        }

        private static HashSet<string> UniqueIds<T>(IReadOnlyList<T> values, Func<T, string> id,
            string pointer, List<ContentValidationError> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Count; i++)
                if (!seen.Add(id(values[i])))
                    Add(errors, ContentDocumentKind.Site, ContentErrorCode.DuplicateId, Index(pointer, i) + "/id");
            return seen;
        }

        private static void RequireReference(HashSet<string> ids, string id, string pointer,
            ContentErrorCode code, List<ContentValidationError> errors)
        {
            if (!ids.Contains(id)) Add(errors, ContentDocumentKind.Site, code, pointer);
        }

        private static void Add(List<ContentValidationError> errors, ContentDocumentKind document,
            ContentErrorCode code, string pointer)
        {
            // 입력 값이나 전체 payload를 메시지에 포함하지 않는다.
            errors.Add(new ContentValidationError(document, code, pointer, "자료 참조 계약 위반: " + code));
        }

        private static string Index(string pointer, int index) => pointer + "/" + index.ToString(CultureInfo.InvariantCulture);
        private static string Property(string pointer, string name) => pointer + "/" + name.Replace("~", "~0").Replace("/", "~1");
        private static WireFailure Fail(ContentErrorCode code, string pointer) => new WireFailure(code, pointer);

        private sealed class WireFailure : Exception
        {
            internal ContentErrorCode Code { get; }
            internal string Pointer { get; }
            internal WireFailure(ContentErrorCode code, string pointer) : base("자료 참조 계약 위반: " + code)
            {
                Code = code; Pointer = pointer;
            }
        }

        // 토큰 트리는 문서 길이·깊이·배열 한도 아래에서만 만들어진다. 범용 schema 엔진이 아니다.
        private enum NodeKind { Object, Array, String, Number, Boolean, Null }
        private sealed class Node
        {
            internal readonly NodeKind Kind;
            internal readonly string Pointer;
            internal string Text;
            internal Dictionary<string, Node> Members;
            internal List<string> Keys;
            internal List<Node> Items;
            internal Node(NodeKind kind, string pointer) { Kind = kind; Pointer = pointer; }
        }

        private static void Shape(Node node, params string[] names)
        {
            RequireKind(node, NodeKind.Object);
            foreach (var key in node.Keys)
            {
                var allowed = false;
                foreach (var name in names)
                    if (StringComparer.Ordinal.Equals(key, name)) { allowed = true; break; }
                if (!allowed) throw Fail(ContentErrorCode.UnknownProperty, Property(node.Pointer, key));
            }
            foreach (var name in names)
                if (!node.Members.ContainsKey(name)) throw Fail(ContentErrorCode.MissingProperty, Property(node.Pointer, name));
        }

        private static void RequireKind(Node node, NodeKind kind)
        {
            if (node.Kind != kind) throw Fail(ContentErrorCode.WrongType, node.Pointer);
        }

        private static string Id(Node node)
        {
            RequireKind(node, NodeKind.String);
            try { return new StableId(node.Text).Value; }
            catch (ArgumentException) { throw Fail(ContentErrorCode.InvalidId, node.Pointer); }
        }

        private static long Revision(Node node)
        {
            RequireKind(node, NodeKind.Number);
            foreach (var character in node.Text)
                if (character < '0' || character > '9') throw Fail(ContentErrorCode.InvalidRevision, node.Pointer);
            long revision;
            if (!long.TryParse(node.Text, NumberStyles.None, CultureInfo.InvariantCulture, out revision))
                throw Fail(ContentErrorCode.NumberOutOfRange, node.Pointer);
            return revision;
        }

        private static List<T> Array<T>(Node node, Func<Node, T> map)
        {
            RequireKind(node, NodeKind.Array);
            var values = new List<T>(node.Items.Count);
            foreach (var item in node.Items) values.Add(map(item));
            return values;
        }

        private static SiteBundle MapSite(Node node)
        {
            Shape(node, "id", "revision", "frames", "regions", "portals", "entities", "sourceRefs", "qualification");
            var fields = node.Members;
            return new SiteBundle(Id(fields["id"]), Revision(fields["revision"]),
                Array(fields["frames"], MapFrame), Array(fields["regions"], MapRegion),
                Array(fields["portals"], MapPortal), Array(fields["entities"], MapEntity),
                Array(fields["sourceRefs"], MapRevisionRef), Array(fields["qualification"], MapQualification));
        }

        private static ScenarioSpec MapScenario(Node node)
        {
            Shape(node, "id", "revision", "site");
            return new ScenarioSpec(Id(node.Members["id"]), Revision(node.Members["revision"]), MapRevisionRef(node.Members["site"]));
        }

        private static ContentRevisionRef MapRevisionRef(Node node)
        {
            Shape(node, "id", "revision");
            return new ContentRevisionRef(Id(node.Members["id"]), Revision(node.Members["revision"]));
        }

        private static ContentFrame MapFrame(Node node)
        {
            Shape(node, "id");
            return new ContentFrame(Id(node.Members["id"]));
        }

        private static ContentRegion MapRegion(Node node)
        {
            Shape(node, "id", "frameId");
            return new ContentRegion(Id(node.Members["id"]), Id(node.Members["frameId"]));
        }

        private static ContentPortal MapPortal(Node node)
        {
            Shape(node, "id", "fromRegionId", "toRegionId");
            return new ContentPortal(Id(node.Members["id"]), Id(node.Members["fromRegionId"]), Id(node.Members["toRegionId"]));
        }

        private static ContentEntity MapEntity(Node node)
        {
            Shape(node, "id", "frameId", "regionId");
            return new ContentEntity(Id(node.Members["id"]), Id(node.Members["frameId"]), Id(node.Members["regionId"]));
        }

        private static ContentQualification MapQualification(Node node)
        {
            Shape(node, "id", "sourceRefIds");
            var id = Id(node.Members["id"]);
            var refs = node.Members["sourceRefIds"];
            var ids = Array(refs, Id);
            if (ids.Count == 0) throw Fail(ContentErrorCode.WrongType, refs.Pointer);
            return new ContentQualification(id, ids);
        }

        private sealed class JsonReader
        {
            private readonly string text;
            private int position;
            internal JsonReader(string text) { this.text = text; }

            internal Node Read()
            {
                if (text == null) throw Fail(ContentErrorCode.EmptyInput, "");
                if (text.Length > MaximumDocumentLength) throw Fail(ContentErrorCode.LimitExceeded, "");
                Whitespace();
                if (position == text.Length) throw Fail(ContentErrorCode.EmptyInput, "");
                var root = Value("", 0);
                Whitespace();
                if (position != text.Length) throw Fail(ContentErrorCode.TrailingData, "");
                return root;
            }

            private Node Value(string pointer, int parentDepth)
            {
                Whitespace();
                if (position == text.Length) throw Fail(ContentErrorCode.InvalidJson, pointer);
                var first = text[position];
                if (first == '{' || first == '[')
                {
                    if (parentDepth == MaximumDepth) throw Fail(ContentErrorCode.LimitExceeded, pointer);
                    return first == '{' ? Object(pointer, parentDepth + 1) : ReadArray(pointer, parentDepth + 1);
                }
                if (first == '"') return new Node(NodeKind.String, pointer) { Text = String(pointer) };
                if (first == '-' || Digit(first)) return Number(pointer);
                if (first == 't') return Literal("true", NodeKind.Boolean, pointer);
                if (first == 'f') return Literal("false", NodeKind.Boolean, pointer);
                if (first == 'n') return Literal("null", NodeKind.Null, pointer);
                throw Fail(ContentErrorCode.InvalidJson, pointer);
            }

            private Node Object(string pointer, int depth)
            {
                position++;
                var node = new Node(NodeKind.Object, pointer)
                {
                    Members = new Dictionary<string, Node>(StringComparer.Ordinal), Keys = new List<string>()
                };
                Whitespace();
                if (Take('}')) return node;
                while (true)
                {
                    if (position == text.Length || text[position] != '"') throw Fail(ContentErrorCode.InvalidJson, pointer);
                    var key = String(pointer);
                    var childPointer = Property(pointer, key);
                    if (node.Members.ContainsKey(key)) throw Fail(ContentErrorCode.DuplicateProperty, childPointer);
                    Whitespace();
                    Expect(':', childPointer);
                    node.Members.Add(key, Value(childPointer, depth));
                    node.Keys.Add(key);
                    Whitespace();
                    if (Take('}')) return node;
                    Expect(',', pointer);
                    Whitespace();
                }
            }

            private Node ReadArray(string pointer, int depth)
            {
                position++;
                var node = new Node(NodeKind.Array, pointer) { Items = new List<Node>() };
                Whitespace();
                if (Take(']')) return node;
                while (true)
                {
                    if (node.Items.Count == MaximumArrayLength) throw Fail(ContentErrorCode.LimitExceeded, pointer);
                    node.Items.Add(Value(Index(pointer, node.Items.Count), depth));
                    Whitespace();
                    if (Take(']')) return node;
                    Expect(',', pointer);
                }
            }

            private Node Literal(string literal, NodeKind kind, string pointer)
            {
                foreach (var character in literal)
                    if (position == text.Length || text[position++] != character) throw Fail(ContentErrorCode.InvalidJson, pointer);
                TokenEnd(pointer);
                return new Node(kind, pointer);
            }

            private Node Number(string pointer)
            {
                var start = position;
                Take('-');
                if (position == text.Length) throw Fail(ContentErrorCode.InvalidJson, pointer);
                if (!Take('0'))
                {
                    if (!Digit(text[position]) || text[position] == '0') throw Fail(ContentErrorCode.InvalidJson, pointer);
                    while (position < text.Length && Digit(text[position])) position++;
                }
                if (Take('.')) Digits(pointer);
                if (Take('e') || Take('E'))
                {
                    if (!Take('+')) Take('-');
                    Digits(pointer);
                }
                TokenEnd(pointer);
                var lexeme = text.Substring(start, position - start);
                double value;
                if (!double.TryParse(lexeme, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsInfinity(value) || double.IsNaN(value))
                    throw Fail(ContentErrorCode.NumberOutOfRange, pointer);
                return new Node(NodeKind.Number, pointer) { Text = lexeme };
            }

            private void Digits(string pointer)
            {
                if (position == text.Length || !Digit(text[position])) throw Fail(ContentErrorCode.InvalidJson, pointer);
                while (position < text.Length && Digit(text[position])) position++;
            }

            private void TokenEnd(string pointer)
            {
                if (position < text.Length && !IsWhitespace(text[position]) && text[position] != ',' && text[position] != ']' && text[position] != '}')
                    throw Fail(ContentErrorCode.InvalidJson, pointer);
            }

            private string String(string pointer)
            {
                position++; // 여는 따옴표는 호출자가 확인했다.
                var builder = new StringBuilder();
                var pendingHighSurrogate = false;
                while (position < text.Length)
                {
                    var character = text[position++];
                    if (character == '"')
                    {
                        if (pendingHighSurrogate) throw Fail(ContentErrorCode.InvalidString, pointer);
                        return builder.ToString();
                    }
                    if (character < 0x20) throw Fail(ContentErrorCode.InvalidString, pointer);
                    if (character == '\\') character = Escape(pointer);
                    // 생 문자와 escape를 해독한 UTF16 스트림 모두에서 쌍을 검사한다.
                    if (pendingHighSurrogate)
                    {
                        if (!char.IsLowSurrogate(character)) throw Fail(ContentErrorCode.InvalidString, pointer);
                        pendingHighSurrogate = false;
                    }
                    else
                    {
                        if (char.IsLowSurrogate(character)) throw Fail(ContentErrorCode.InvalidString, pointer);
                        pendingHighSurrogate = char.IsHighSurrogate(character);
                    }
                    builder.Append(character);
                }
                throw Fail(ContentErrorCode.InvalidString, pointer);
            }

            private char Escape(string pointer)
            {
                if (position == text.Length) throw Fail(ContentErrorCode.InvalidString, pointer);
                switch (text[position++])
                {
                    case '"': return '"';
                    case '\\': return '\\';
                    case '/': return '/';
                    case 'b': return '\b';
                    case 'f': return '\f';
                    case 'n': return '\n';
                    case 'r': return '\r';
                    case 't': return '\t';
                    case 'u': return UnicodeEscape(pointer);
                    default: throw Fail(ContentErrorCode.InvalidString, pointer);
                }
            }

            private char UnicodeEscape(string pointer)
            {
                if (text.Length - position < 4) throw Fail(ContentErrorCode.InvalidString, pointer);
                var value = 0;
                for (var i = 0; i < 4; i++)
                {
                    var character = text[position++];
                    var digit = character >= '0' && character <= '9' ? character - '0' :
                        character >= 'a' && character <= 'f' ? character - 'a' + 10 :
                        character >= 'A' && character <= 'F' ? character - 'A' + 10 : -1;
                    if (digit < 0) throw Fail(ContentErrorCode.InvalidString, pointer);
                    value = value * 16 + digit;
                }
                return (char)value;
            }

            private static bool Digit(char value) => value >= '0' && value <= '9';
            private static bool IsWhitespace(char value) => value == ' ' || value == '\t' || value == '\r' || value == '\n';
            private void Whitespace()
            {
                while (position < text.Length && IsWhitespace(text[position])) position++;
            }
            private bool Take(char value)
            {
                if (position == text.Length || text[position] != value) return false;
                position++;
                return true;
            }
            private void Expect(char value, string pointer)
            {
                if (!Take(value)) throw Fail(ContentErrorCode.InvalidJson, pointer);
            }
        }
    }
}
