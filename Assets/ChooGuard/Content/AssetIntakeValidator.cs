using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ChooGuard.Content
{
    public enum AssetIntakeStatus { NOT_ACQUIRED, REJECTED, QUALIFICATION_INCOMPLETE, STATIC_CHECKS_PASSED }
    public enum AssetIntakeIssueCode
    {
        INVALID_SOURCE_URL, INVALID_PATH, PATH_COLLISION, UNSUPPORTED_SYMLINK,
        RAW_BYTES_LIMIT, ENTRY_COUNT_LIMIT, TOTAL_BYTES_LIMIT, ENTRY_BYTES_LIMIT, COMPRESSION_RATIO_LIMIT,
        EXTENSION_NOT_ALLOWED, DISALLOWED_CONTENT, UNSUPPORTED_NESTED_ARCHIVE, UNSUPPORTED_FORMAT,
        MAGIC_MISMATCH, INVALID_ARCHIVE, EMPTY_ARCHIVE, SEMANTIC_VALIDATION_UNSUPPORTED
    }

    public sealed class AssetIntakeProfile
    {
        public long MaxRawBytes { get; }
        public int MaxEntryCount { get; }
        public long MaxTotalUncompressedBytes { get; }
        public long MaxEntryBytes { get; }
        public double MaxCompressionRatio { get; }
        public IReadOnlyList<string> AllowedExtensions { get; }
        public AssetIntakeProfile(long maxRawBytes, int maxEntryCount, long maxTotalUncompressedBytes,
            long maxEntryBytes, double maxCompressionRatio, IEnumerable<string> allowedExtensions)
        {
            if (maxRawBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxRawBytes));
            if (maxEntryCount < 1) throw new ArgumentOutOfRangeException(nameof(maxEntryCount));
            if (maxTotalUncompressedBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxTotalUncompressedBytes));
            if (maxEntryBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxEntryBytes));
            if (double.IsNaN(maxCompressionRatio) || double.IsInfinity(maxCompressionRatio) || maxCompressionRatio <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCompressionRatio));
            if (allowedExtensions == null) throw new ArgumentNullException(nameof(allowedExtensions));
            var copy = new List<string>();
            foreach (var extension in allowedExtensions)
            {
                if (string.IsNullOrEmpty(extension) || extension.Length < 2 || extension[0] != '.')
                    throw new ArgumentException("확장자는 점으로 시작해야 합니다.", nameof(allowedExtensions));
                for (var i = 1; i < extension.Length; i++)
                    if (!((extension[i] >= 'a' && extension[i] <= 'z') || (extension[i] >= 'A' && extension[i] <= 'Z') ||
                        (extension[i] >= '0' && extension[i] <= '9')))
                        throw new ArgumentException("확장자는 ASCII 영숫자만 허용합니다.", nameof(allowedExtensions));
                copy.Add(extension.ToLowerInvariant());
            }
            AllowedExtensions = new ReadOnlyCollection<string>(copy);
            MaxRawBytes = maxRawBytes; MaxEntryCount = maxEntryCount;
            MaxTotalUncompressedBytes = maxTotalUncompressedBytes; MaxEntryBytes = maxEntryBytes;
            MaxCompressionRatio = maxCompressionRatio;
        }
    }

    public sealed class AssetIntakeRequest
    {
        private readonly byte[] raw;
        public string Id { get; }
        public string SourceUrl { get; }
        public string FileName { get; }
        public string Tier { get; }
        public DateTimeOffset? AcquiredAt { get; }
        public AssetIntakeProfile Profile { get; }
        public byte[] RawBytes => raw == null ? null : (byte[])raw.Clone();
        public AssetIntakeRequest(string id, string sourceUrl, string fileName, string tier,
            DateTimeOffset? acquiredAt, byte[] rawBytes, AssetIntakeProfile profile)
        {
            Id = new ChooGuard.Contracts.StableId(id).Value;
            SourceUrl = sourceUrl; FileName = fileName; Tier = tier; AcquiredAt = acquiredAt;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            raw = rawBytes == null ? null : (byte[])rawBytes.Clone();
        }
    }

    public sealed class AssetIntakeIssue
    {
        public AssetIntakeIssueCode Code { get; }
        public string EntryPath { get; }
        public string Message => "에셋 입고 검사: " + Code;
        internal AssetIntakeIssue(AssetIntakeIssueCode code, string path) { Code = code; EntryPath = path; }
    }

    public sealed class AssetIntakeEntry
    {
        public string Path { get; }
        public bool IsDirectory { get; }
        public long ByteCount { get; }
        public string Format { get; }
        internal AssetIntakeEntry(string path, bool directory, long count, string format)
        { Path = path; IsDirectory = directory; ByteCount = count; Format = format; }
    }

    public sealed class AssetIntakeReceipt
    {
        public string Id { get; }
        public string SourceUrl { get; }
        public string FileName { get; }
        public string Tier { get; }
        public DateTimeOffset? AcquiredAt { get; }
        public string RawHash { get; }
        public long? RawByteCount { get; }
        public string RawFormat { get; }
        public long TotalUncompressedBytes { get; }
        public IReadOnlyList<AssetIntakeEntry> Entries { get; }
        public AssetIntakeProfile Profile { get; }
        public string ImportStatus => "NOT_RUN";
        public string RigClipsStatus => "NOT_VERIFIED";
        public string PartsStatus => "NOT_VERIFIED";
        public string QualificationStatus => "INCOMPLETE";
        public string MalwareStatus => "NOT_ASSESSED";
        public string FieldApproval => "NOT_FIELD_APPROVED";
        public string LicenseApproval => "NOT_VERIFIED";
        internal AssetIntakeReceipt(AssetIntakeRequest r, string hash, long? count, string format,
            long total, IEnumerable<AssetIntakeEntry> entries)
        {
            Id = r.Id; SourceUrl = r.SourceUrl; FileName = r.FileName; Tier = r.Tier; AcquiredAt = r.AcquiredAt;
            RawHash = hash; RawByteCount = count; RawFormat = format; TotalUncompressedBytes = total;
            Entries = new ReadOnlyCollection<AssetIntakeEntry>(new List<AssetIntakeEntry>(entries)); Profile = r.Profile;
        }
    }

    public sealed class AssetIntakeResult
    {
        public AssetIntakeStatus Status { get; }
        public AssetIntakeReceipt Receipt { get; }
        public IReadOnlyList<AssetIntakeIssue> Issues { get; }
        // 헤더·경로·크기 검사는 안전한 import, 악성코드 부재 또는 현장 승인이 아니다.
        public bool SafeImportApproved => false;
        internal AssetIntakeResult(AssetIntakeStatus status, AssetIntakeReceipt receipt, IEnumerable<AssetIntakeIssue> issues)
        { Status = status; Receipt = receipt; Issues = new ReadOnlyCollection<AssetIntakeIssue>(new List<AssetIntakeIssue>(issues)); }
    }

    /// <summary>네트워크·추출·실행 없이 실제 바이트를 검사한다. 형식 식별은 전체 디코딩이나 의미 검증이 아니다.</summary>
    public static class AssetIntakeValidator
    {
        private static readonly HashSet<string> Denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".com", ".scr", ".msi", ".msp", ".bat", ".cmd", ".cs", ".csx", ".sh", ".bash",
            ".ps1", ".psm1", ".psd1", ".js", ".jse", ".mjs", ".cjs", ".ts", ".py", ".pyc", ".pyo", ".rb",
            ".pl", ".php", ".lua", ".vbs", ".vbe", ".wsf", ".wsh", ".hta", ".jar", ".class", ".so", ".dylib",
            ".bundle", ".app", ".lnk", ".url", ".desktop", ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".ppam"
        };
        private static readonly HashSet<string> Archives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".unitypackage" };
        private static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { { ".png", "PNG" }, { ".jpg", "JPEG" }, { ".jpeg", "JPEG" }, { ".ogg", "OGG" }, { ".otf", "OTF" }, { ".glb", "GLB" } };

        public static AssetIntakeResult Validate(AssetIntakeRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var issues = new List<AssetIntakeIssue>(); var entries = new List<AssetIntakeEntry>();
            Uri source;
            if (!Uri.TryCreate(request.SourceUrl, UriKind.Absolute, out source) || source.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrEmpty(source.Host) || !string.IsNullOrEmpty(source.UserInfo))
                issues.Add(new AssetIntakeIssue(AssetIntakeIssueCode.INVALID_SOURCE_URL, null));
            var bytes = request.RawBytes; string hash = null; string format = null; long total = 0;
            if (bytes != null)
            {
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                try
                {
                    Require(bytes.LongLength <= request.Profile.MaxRawBytes, AssetIntakeIssueCode.RAW_BYTES_LIMIT, null);
                    var name = NormalizePath(request.FileName, false);
                    var extension = Extension(name);
                    Require(!Denied.Contains(extension) && !Executable(bytes), AssetIntakeIssueCode.DISALLOWED_CONTENT, name);
                    if (extension == ".zip" || IsZip(bytes))
                    {
                        Require(extension == ".zip", AssetIntakeIssueCode.MAGIC_MISMATCH, name);
                        format = "ZIP";
                        InspectZip(bytes, request.Profile, entries, issues, ref total);
                    }
                    else
                    {
                        Require(bytes.LongLength <= request.Profile.MaxEntryBytes, AssetIntakeIssueCode.ENTRY_BYTES_LIMIT, name);
                        Require(bytes.LongLength <= request.Profile.MaxTotalUncompressedBytes, AssetIntakeIssueCode.TOTAL_BYTES_LIMIT, name);
                        format = InspectPayload(name, bytes, bytes.LongLength, request.Profile, issues);
                        total = bytes.LongLength;
                        entries.Add(new AssetIntakeEntry(name, false, total, format));
                    }
                }
                catch (IntakeFailure failure) { issues.Add(new AssetIntakeIssue(failure.Code, failure.Path)); }
                catch (InvalidDataException) { issues.Add(new AssetIntakeIssue(AssetIntakeIssueCode.INVALID_ARCHIVE, null)); }
                catch (IOException) { issues.Add(new AssetIntakeIssue(AssetIntakeIssueCode.INVALID_ARCHIVE, null)); }
                catch (NotSupportedException) { issues.Add(new AssetIntakeIssue(AssetIntakeIssueCode.INVALID_ARCHIVE, null)); }
                catch (ArgumentException) { issues.Add(new AssetIntakeIssue(AssetIntakeIssueCode.INVALID_ARCHIVE, null)); }
            }
            var status = bytes == null ? AssetIntakeStatus.NOT_ACQUIRED : AssetIntakeStatus.STATIC_CHECKS_PASSED;
            if (bytes != null && issues.Count > 0)
            {
                status = AssetIntakeStatus.QUALIFICATION_INCOMPLETE;
                foreach (var issue in issues)
                    if (issue.Code != AssetIntakeIssueCode.SEMANTIC_VALIDATION_UNSUPPORTED) { status = AssetIntakeStatus.REJECTED; break; }
            }
            return new AssetIntakeResult(status, new AssetIntakeReceipt(request, hash,
                bytes == null ? (long?)null : bytes.LongLength, format, total, entries), issues);
        }

        private static void InspectZip(byte[] raw, AssetIntakeProfile profile, List<AssetIntakeEntry> output,
            List<AssetIntakeIssue> issues, ref long total)
        {
            Require(IsZip(raw), AssetIntakeIssueCode.INVALID_ARCHIVE, null);
            using (var memory = new MemoryStream(raw, false))
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Read, false))
            {
                Require(archive.Entries.Count > 0, AssetIntakeIssueCode.EMPTY_ARCHIVE, null);
                Require(archive.Entries.Count <= profile.MaxEntryCount, AssetIntakeIssueCode.ENTRY_COUNT_LIMIT, null);
                var paths = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                var spellings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var names = new List<string>(); var directories = new List<bool>(); long declaredTotal = 0;
                foreach (var entry in archive.Entries)
                {
                    var directory = entry.FullName.EndsWith("/", StringComparison.Ordinal);
                    var name = NormalizePath(entry.FullName, directory);
                    Require(((entry.ExternalAttributes >> 16) & 0xF000) != 0xA000,
                        AssetIntakeIssueCode.UNSUPPORTED_SYMLINK, name);
                    // 디렉터리 reparse point도 일반 항목으로 신뢰하지 않는다.
                    Require((entry.ExternalAttributes & 0x400) == 0, AssetIntakeIssueCode.UNSUPPORTED_SYMLINK, name);
                    Require(!paths.ContainsKey(name), AssetIntakeIssueCode.PATH_COLLISION, name);
                    paths.Add(name, directory); names.Add(name); directories.Add(directory);
                    // 암묵적 상위 디렉터리도 원래 철자를 고정하여 A/x와 a/y의 충돌을 막는다.
                    var rawName = directory ? entry.FullName.Substring(0, entry.FullName.Length - 1) : entry.FullName;
                    var components = rawName.Split('/'); var originalPrefix = "";
                    foreach (var component in components)
                    {
                        originalPrefix = originalPrefix.Length == 0 ? component : originalPrefix + "/" + component;
                        var key = originalPrefix.Normalize(NormalizationForm.FormC); string previous;
                        Require(!spellings.TryGetValue(key, out previous) || StringComparer.Ordinal.Equals(previous, originalPrefix),
                            AssetIntakeIssueCode.PATH_COLLISION, name);
                        spellings[key] = originalPrefix;
                    }
                    Require(entry.Length >= 0 && entry.Length <= profile.MaxEntryBytes, AssetIntakeIssueCode.ENTRY_BYTES_LIMIT, name);
                    Require(entry.CompressedLength >= 0, AssetIntakeIssueCode.INVALID_ARCHIVE, name);
                    Require(entry.Length <= profile.MaxTotalUncompressedBytes - declaredTotal, AssetIntakeIssueCode.TOTAL_BYTES_LIMIT, name);
                    declaredTotal += entry.Length;
                    Require(!directory || entry.Length == 0, AssetIntakeIssueCode.INVALID_ARCHIVE, name);
                    CheckRatio(entry.Length, entry.CompressedLength, profile, name);
                }
                foreach (var name in names)
                {
                    var slash = name.IndexOf('/');
                    while (slash >= 0)
                    {
                        bool directory;
                        Require(!paths.TryGetValue(name.Substring(0, slash), out directory) || directory,
                            AssetIntakeIssueCode.PATH_COLLISION, name);
                        slash = name.IndexOf('/', slash + 1);
                    }
                }
                var buffer = new byte[8192];
                for (var i = 0; i < archive.Entries.Count; i++)
                {
                    var entry = archive.Entries[i]; var name = names[i];
                    // 전체 해제 내용을 보관하지 않고 헤더와 실제 해제 바이트 수만 수집한다.
                    var prefix = new byte[512]; var prefixLength = 0; long readTotal = 0;
                    using (var stream = entry.Open())
                    {
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            Require(read <= profile.MaxEntryBytes - readTotal, AssetIntakeIssueCode.ENTRY_BYTES_LIMIT, name);
                            Require(read <= profile.MaxTotalUncompressedBytes - total, AssetIntakeIssueCode.TOTAL_BYTES_LIMIT, name);
                            readTotal += read; total += read;
                            Require(readTotal <= entry.Length, AssetIntakeIssueCode.INVALID_ARCHIVE, name);
                            CheckRatio(readTotal, entry.CompressedLength, profile, name);
                            var keep = Math.Min(read, prefix.Length - prefixLength);
                            Array.Copy(buffer, 0, prefix, prefixLength, keep); prefixLength += keep;
                        }
                    }
                    Require(readTotal == entry.Length, AssetIntakeIssueCode.INVALID_ARCHIVE, name);
                    Array.Resize(ref prefix, prefixLength);
                    var format = directories[i] ? "DIRECTORY" : InspectPayload(name, prefix, readTotal, profile, issues);
                    output.Add(new AssetIntakeEntry(name, directories[i], readTotal, format));
                }
            }
        }

        private static void CheckRatio(long unpacked, long compressed, AssetIntakeProfile profile, string path)
        {
            Require(unpacked == 0 || (compressed > 0 && (double)unpacked / compressed <= profile.MaxCompressionRatio),
                AssetIntakeIssueCode.COMPRESSION_RATIO_LIMIT, path);
        }

        private static string InspectPayload(string path, byte[] header, long length, AssetIntakeProfile profile,
            List<AssetIntakeIssue> issues)
        {
            var extension = Extension(path);
            Require(!Denied.Contains(extension) && !Executable(header), AssetIntakeIssueCode.DISALLOWED_CONTENT, path);
            Require(!Archives.Contains(extension) && !ArchiveMagic(header), AssetIntakeIssueCode.UNSUPPORTED_NESTED_ARCHIVE, path);
            string expected;
            if (!Known.TryGetValue(extension, out expected) && extension != ".fbx" && extension != ".obj")
                throw new IntakeFailure(AssetIntakeIssueCode.UNSUPPORTED_FORMAT, path);
            var allowed = false;
            foreach (var item in profile.AllowedExtensions) if (item == extension) { allowed = true; break; }
            Require(allowed, AssetIntakeIssueCode.EXTENSION_NOT_ALLOWED, path);
            if (expected == null)
            {
                issues.Add(new AssetIntakeIssue(AssetIntakeIssueCode.SEMANTIC_VALIDATION_UNSUPPORTED, path));
                return "UNKNOWN";
            }
            var identified = Identify(header, length);
            Require(identified == expected, AssetIntakeIssueCode.MAGIC_MISMATCH, path);
            return identified;
        }

        private static string NormalizePath(string path, bool directory)
        {
            Require(!string.IsNullOrEmpty(path) && path.Length <= 4096 && path[0] != '/' && path.IndexOf('\\') < 0,
                AssetIntakeIssueCode.INVALID_PATH, path);
            string normalized;
            try { normalized = path.Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { throw new IntakeFailure(AssetIntakeIssueCode.INVALID_PATH, path); }
            if (directory) normalized = normalized.Substring(0, normalized.Length - 1);
            foreach (var part in normalized.Split('/'))
            {
                Require(part.Length > 0 && part.Length <= 255 && part != "." && part != ".." &&
                    !part.EndsWith(".", StringComparison.Ordinal) && !part.EndsWith(" ", StringComparison.Ordinal),
                    AssetIntakeIssueCode.INVALID_PATH, path);
                foreach (var c in part)
                    Require(!char.IsControl(c) && ":<>\"|?*".IndexOf(c) < 0 &&
                        System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format,
                        AssetIntakeIssueCode.INVALID_PATH, path);
                var stem = part.Split('.')[0].ToUpperInvariant();
                Require(stem != "CON" && stem != "PRN" && stem != "AUX" && stem != "NUL" &&
                    !(stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                    "123456789¹²³".IndexOf(stem[3]) >= 0), AssetIntakeIssueCode.INVALID_PATH, path);
            }
            return normalized;
        }

        private static string Extension(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot > name.LastIndexOf('/') ? name.Substring(dot).ToLowerInvariant() : "";
        }
        private static bool Starts(byte[] bytes, params byte[] magic)
        {
            if (bytes.Length < magic.Length) return false;
            for (var i = 0; i < magic.Length; i++) if (bytes[i] != magic[i]) return false;
            return true;
        }
        private static bool IsZip(byte[] b) => Starts(b, 80, 75, 3, 4) || Starts(b, 80, 75, 5, 6) || Starts(b, 80, 75, 7, 8);
        private static bool ArchiveMagic(byte[] b) => IsZip(b) || Starts(b, 55, 122, 188, 175, 39, 28) ||
            Starts(b, 82, 97, 114, 33) || Starts(b, 31, 139) || Starts(b, 66, 90, 104) || Starts(b, 253, 55, 122, 88, 90, 0) ||
            (b.Length >= 262 && b[257] == 117 && b[258] == 115 && b[259] == 116 && b[260] == 97 && b[261] == 114);
        private static bool Executable(byte[] b) => Starts(b, 77, 90) || Starts(b, 127, 69, 76, 70) || Starts(b, 35, 33) ||
            Starts(b, 202, 254, 186, 190) || Starts(b, 206, 250, 237, 254) || Starts(b, 207, 250, 237, 254) ||
            Starts(b, 254, 237, 250, 206) || Starts(b, 254, 237, 250, 207);
        private static uint Little32(byte[] b, int at) => (uint)b[at] | ((uint)b[at + 1] << 8) | ((uint)b[at + 2] << 16) | ((uint)b[at + 3] << 24);
        private static uint Big32(byte[] b, int at) => ((uint)b[at] << 24) | ((uint)b[at + 1] << 16) | ((uint)b[at + 2] << 8) | b[at + 3];
        private static string Identify(byte[] b, long length)
        {
            if (b.Length >= 33 && Starts(b, 137, 80, 78, 71, 13, 10, 26, 10) && Big32(b, 8) == 13 &&
                b[12] == 73 && b[13] == 72 && b[14] == 68 && b[15] == 82 && Big32(b, 16) > 0 && Big32(b, 20) > 0) return "PNG";
            if (b.Length >= 4 && Starts(b, 255, 216, 255) && b[3] != 0 && b[3] != 255 && length >= 10) return "JPEG";
            if (b.Length >= 27 && Starts(b, 79, 103, 103, 83, 0) && length >= 27L + b[26]) return "OGG";
            if (b.Length >= 12 && Starts(b, 79, 84, 84, 79) && ((b[4] << 8) | b[5]) > 0 &&
                length >= 12L + 16L * ((b[4] << 8) | b[5])) return "OTF";
            if (b.Length >= 20 && Starts(b, 103, 108, 84, 70) && Little32(b, 4) == 2 && Little32(b, 8) == length &&
                Little32(b, 12) <= length - 20 && Little32(b, 12) % 4 == 0 && Little32(b, 16) == 0x4E4F534A) return "GLB";
            return null;
        }
        private static void Require(bool condition, AssetIntakeIssueCode code, string path)
        { if (!condition) throw new IntakeFailure(code, path); }
        private sealed class IntakeFailure : Exception
        {
            internal readonly AssetIntakeIssueCode Code;
            internal readonly string Path;
            internal IntakeFailure(AssetIntakeIssueCode code, string path) { Code = code; Path = path; }
        }
    }
}
