using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace ChooGuard.Persistence
{
    [DataContract] public sealed class RuntimePackageFile
    {
        [DataMember(IsRequired = true)] public string path;
        [DataMember(IsRequired = true)] public string sha256;
    }
    [DataContract] public sealed class RuntimePlatformPackage
    {
        [DataMember(IsRequired = true)] public string id;
        [DataMember(IsRequired = true)] public string sqlite;
        [DataMember(IsRequired = true)] public string sqliteSourceId;
        [DataMember(IsRequired = true)] public string python;
        [DataMember(IsRequired = true)] public string worker;
        [DataMember(IsRequired = true)] public string upstream;
        [DataMember(IsRequired = true)] public string referenceCase;
        [DataMember] public string node;
        [DataMember] public string broker;
        [DataMember] public string schemaDirectory;
        [DataMember(IsRequired = true)] public RuntimePackageFile[] files;
    }
    [DataContract] public sealed class RuntimePackageManifest
    {
        [DataMember(IsRequired = true)] public int schemaVersion;
        [DataMember(IsRequired = true)] public RuntimePlatformPackage[] platforms;
    }

    /// <summary>Explicit, relocatable deployment identity. No PATH, repo tools or virtualenv fallback.</summary>
    public sealed class RuntimePackage
    {
        private static string configuredRoot;
        private readonly Dictionary<string, RuntimePackageFile> files;
        public string Root { get; }
        public RuntimePlatformPackage Platform { get; }
        public static string HostId
        {
            get
            {
                var architecture = RuntimeInformation.ProcessArchitecture;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && architecture == Architecture.X64) return "win-x64";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && architecture == Architecture.Arm64) return "osx-arm64";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && architecture == Architecture.X64) return "osx-x64";
                throw new PlatformNotSupportedException("RUNTIME_PLATFORM_UNSUPPORTED: " + architecture);
            }
        }
        public static void ConfigureRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root)) throw new ArgumentException("Absolute runtime package root required.", nameof(root));
            configuredRoot = Path.GetFullPath(root);
        }
        public static RuntimePackage LoadConfigured()
        {
            var root = configuredRoot ?? Environment.GetEnvironmentVariable("CG_RUNTIME_PACKAGE");
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("RUNTIME_PACKAGE_NOT_CONFIGURED: configure the packaged runtime root before opening storage or workers.");
            return Load(root, HostId);
        }
        public static RuntimePackage Load(string root, string platformId)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root)) throw new ArgumentException("Absolute runtime package root required.", nameof(root));
            root = Path.GetFullPath(root);
            var path = Path.Combine(root, "runtime-manifest.json");
            if (!File.Exists(path)) throw new FileNotFoundException("RUNTIME_PACKAGE_MISSING: install the platform package; no development-environment fallback.", path);
            RuntimePackageManifest manifest;
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > 4 * 1024 * 1024) throw new InvalidDataException("RUNTIME_MANIFEST_TOO_LARGE");
                manifest = (RuntimePackageManifest)new DataContractJsonSerializer(typeof(RuntimePackageManifest)).ReadObject(stream);
            }
            if (manifest == null || manifest.schemaVersion != 1 || manifest.platforms == null) throw new InvalidDataException("RUNTIME_MANIFEST_VERSION");
            RuntimePlatformPackage selected = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in manifest.platforms)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || !ids.Add(entry.id)) throw new InvalidDataException("RUNTIME_PLATFORM_DUPLICATE");
                if (entry.id == platformId) selected = entry;
            }
            if (selected == null) throw new PlatformNotSupportedException("RUNTIME_PLATFORM_PACKAGE_MISSING: " + platformId);
            return new RuntimePackage(root, selected);
        }
        private RuntimePackage(string root, RuntimePlatformPackage platform)
        {
            Root = root; Platform = platform;
            files = new Dictionary<string, RuntimePackageFile>(StringComparer.Ordinal);
            if (platform.files == null || platform.files.Length == 0 || platform.files.Length > 20000) throw new InvalidDataException("RUNTIME_FILE_INVENTORY_INVALID");
            foreach (var file in platform.files)
            {
                if (file == null || !HashText(file.sha256)) throw new InvalidDataException("RUNTIME_FILE_HASH_INVALID");
                Resolve(file.path);
                if (files.ContainsKey(file.path)) throw new InvalidDataException("RUNTIME_FILE_DUPLICATE: " + file.path);
                files.Add(file.path, file);
            }
            if (string.IsNullOrWhiteSpace(platform.sqliteSourceId)) throw new InvalidDataException("RUNTIME_SQLITE_IDENTITY_MISSING");
            RequireListed(platform.sqlite); RequireListed(platform.python); RequireListed(platform.worker);
            Resolve(platform.upstream); Resolve(platform.referenceCase);
        }
        public string Resolve(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || relative.IndexOfAny(new[] { '\0', '\r', '\n', '\\', ':' }) >= 0 || Path.IsPathRooted(relative))
                throw new InvalidDataException("RUNTIME_PATH_INVALID: " + relative);
            foreach (var segment in relative.Split('/'))
                if (segment.Length == 0 || segment == "." || segment == "..") throw new InvalidDataException("RUNTIME_PATH_ESCAPE: " + relative);
            var result = Path.GetFullPath(Path.Combine(Root, relative));
            if (!result.StartsWith(Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("RUNTIME_PATH_ESCAPE");
            var current = Root;
            foreach (var segment in relative.Split('/'))
            {
                current = Path.Combine(current, segment);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("RUNTIME_SYMLINK_FORBIDDEN: " + relative);
            }
            return result;
        }
        private RuntimePackageFile RequireListed(string relative)
        {
            if (relative == null || !files.TryGetValue(relative, out var file)) throw new InvalidDataException("RUNTIME_FILE_NOT_PINNED: " + relative);
            return file;
        }
        public string VerifyFile(string relative)
        {
            var file = RequireListed(relative); var path = Resolve(relative);
            if (!File.Exists(path)) throw new FileNotFoundException("RUNTIME_PACKAGE_FILE_MISSING: " + relative, path);
            using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create())
                if (SqliteProvider.Hex(hash.ComputeHash(stream)) != file.sha256) throw new InvalidDataException("RUNTIME_PACKAGE_HASH_MISMATCH: " + relative);
            return path;
        }
        public void VerifyAll() { foreach (var file in Platform.files) VerifyFile(file.path); }
        public SqliteProvider OpenDatabase(string databasePath)
        {
            var path = VerifyFile(Platform.sqlite);
            return new SqliteProvider(path, RequireListed(Platform.sqlite).sha256, Platform.sqliteSourceId, databasePath);
        }
        public void RequireBroker()
        {
            if (string.IsNullOrEmpty(Platform.node) || string.IsNullOrEmpty(Platform.broker) || string.IsNullOrEmpty(Platform.schemaDirectory))
                throw new FileNotFoundException("GAMEPLAY_BROKER_PACKAGE_MISSING: run workers/package_broker.py for this platform.");
            RequireListed(Platform.node); RequireListed(Platform.broker);
            RequireListed(Platform.schemaDirectory + "/npc-decision.schema.json");
            RequireListed(Platform.schemaDirectory + "/future-step.schema.json");
        }
        public ProcessStartInfo BrokerStartInfo(string capability)
        {
            if (capability == null || capability.Length < 32 || capability.Length > 256)
                throw new ArgumentException("A per-launch 32–256 character capability is required.", nameof(capability));
            foreach (var c in capability)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-'))
                    throw new ArgumentException("Capability must be base64url or hexadecimal.", nameof(capability));
            RequireBroker(); VerifyAll();
            var start = new ProcessStartInfo
            {
                FileName = Resolve(Platform.node), Arguments = QuoteArgument(Resolve(Platform.broker)) + " --owner-stdin",
                WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            start.EnvironmentVariables.Remove("NODE_OPTIONS"); start.EnvironmentVariables.Remove("NODE_PATH");
            start.EnvironmentVariables["CHOOGUARD_GAMEPLAY_TOKEN"] = capability;
            start.EnvironmentVariables["CHOOGUARD_GAMEPLAY_SCHEMA_DIR"] = Resolve(Platform.schemaDirectory);
            return start;
        }
        public ProcessStartInfo PhysicsStartInfo()
        {
            VerifyAll();
            var start = new ProcessStartInfo
            {
                FileName = Resolve(Platform.python), Arguments = "-I -B -u -X utf8 " + QuoteArgument(Resolve(Platform.worker)),
                WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            // Explicit data paths only; -I ignores PYTHONPATH/user packages and -B keeps installation read-only.
            start.EnvironmentVariables["CG_PHYSICS_UPSTREAM"] = Resolve(Platform.upstream);
            start.EnvironmentVariables["CG_PHYSICS_CASE"] = Resolve(Platform.referenceCase);
            start.EnvironmentVariables["CG_RUNTIME_PACKAGE"] = Root;
            start.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            start.EnvironmentVariables["PYTHONUTF8"] = "1";
            return start;
        }
        // CRT/Mono process argument escaping, without a shell. Includes empty values and trailing backslashes.
        public static string QuoteArgument(string value)
        {
            if (value == null || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0) throw new ArgumentException("Invalid process argument.", nameof(value));
            var result = new StringBuilder(value.Length + 2).Append('"'); var slashes = 0;
            foreach (var character in value)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') result.Append('\\', slashes * 2 + 1); else result.Append('\\', slashes);
                result.Append(character); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
        private static bool HashText(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }
}
