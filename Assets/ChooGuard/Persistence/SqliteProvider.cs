using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ChooGuard.Persistence
{
    /// <summary>Exact-file native loading. Currently macOS only; other ABIs fail closed.
    /// The caller supplies deployment-pinned source ID and SHA-256, never a discovered auto-approval.
    /// A successful probe is not Player qualification or a power-loss durability certification.</summary>
    public sealed class SqliteProvider : IDisposable
    {
        private IntPtr library, database;
        private readonly Open open;
        private readonly Close close;
        private readonly Prepare prepare;
        private readonly Step step;
        private readonly FinalizeStatement finalize;
        private readonly BindText bindText;
        private readonly ColumnText columnText;
        private readonly ColumnCount columnCount;
        private readonly ErrorMessage errorMessage;
        public string SourceId { get; }
        public string BinarySha256 { get; }
        public int VersionNumber { get; }
        public string DatabasePath { get; }
        internal object Gate { get; } = new object();

        public SqliteProvider(string nativePath, string expectedSha256, string expectedSourceId, string databasePath)
        {
            if (string.IsNullOrWhiteSpace(nativePath) || !Path.IsPathRooted(nativePath) || !File.Exists(nativePath))
                throw new ArgumentException("An existing absolute native binary path is required.", nameof(nativePath));
            if (string.IsNullOrWhiteSpace(expectedSourceId) || string.IsNullOrWhiteSpace(expectedSha256))
                throw new ArgumentException("Deployment-pinned source ID and binary SHA-256 are required.");
            if (string.IsNullOrWhiteSpace(databasePath) || !Path.IsPathRooted(databasePath) || databasePath.IndexOf('\0') >= 0)
                throw new ArgumentException("An absolute local file database path is required.", nameof(databasePath));
            DatabasePath = Path.GetFullPath(databasePath);
            using (var input = File.OpenRead(nativePath)) using (var hash = SHA256.Create())
                BinarySha256 = Hex(hash.ComputeHash(input));
            if (!StringComparer.Ordinal.Equals(BinarySha256, expectedSha256)) throw new InvalidOperationException("SQLITE_BINARY_HASH_MISMATCH");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) throw new PlatformNotSupportedException("SQLite ABI is not implemented for this host.");
            try
            {
                library = dlopen(nativePath, 2); // RTLD_NOW; no global search-name fallback.
                if (library == IntPtr.Zero) throw new InvalidOperationException("SQLITE_NATIVE_LOAD_FAILED");
                VersionNumber = Load<Version>("sqlite3_libversion_number")();
                SourceId = Text(Load<Source>("sqlite3_sourceid")());
                if (SourceId != expectedSourceId) throw new InvalidOperationException("SQLITE_SOURCE_ID_MISMATCH");
                // Older official backports require a separate reviewed allow-list, not a version-only bypass.
                if (VersionNumber < 3051003) throw new PlatformNotSupportedException("SQLITE_WAL_BASELINE_UNSUPPORTED");
                open = Load<Open>("sqlite3_open_v2"); close = Load<Close>("sqlite3_close_v2");
                prepare = Load<Prepare>("sqlite3_prepare_v2"); step = Load<Step>("sqlite3_step");
                finalize = Load<FinalizeStatement>("sqlite3_finalize"); bindText = Load<BindText>("sqlite3_bind_text");
                columnText = Load<ColumnText>("sqlite3_column_text"); columnCount = Load<ColumnCount>("sqlite3_column_count");
                errorMessage = Load<ErrorMessage>("sqlite3_errmsg");
                Check(open(Utf8(DatabasePath), out database, 2 | 4 | 0x10000, IntPtr.Zero)); // RW, CREATE, FULLMUTEX, no URI.
                Execute("PRAGMA busy_timeout=3000");
                if (!string.Equals(Scalar("PRAGMA journal_mode=WAL"), "wal", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("SQLITE_WAL_REQUIRED");
                Execute("PRAGMA synchronous=FULL"); Execute("PRAGMA foreign_keys=ON");
                VerifySettings();
            }
            catch { Dispose(); throw; }
        }

        public void VerifySettings()
        {
            lock (Gate)
            {
                if (Scalar("PRAGMA journal_mode") != "wal" || Scalar("PRAGMA synchronous") != "2" || Scalar("PRAGMA foreign_keys") != "1")
                    throw new InvalidOperationException("SQLITE_DURABILITY_SETTINGS_CHANGED");
            }
        }
        internal void Execute(string sql, params string[] arguments) { Query(sql, arguments); }
        internal string Scalar(string sql, params string[] arguments)
        {
            var rows = Query(sql, arguments);
            return rows.Count == 0 ? null : rows[0][0];
        }
        internal List<string[]> Query(string sql, params string[] arguments)
        {
            lock (Gate)
            {
                if (database == IntPtr.Zero) throw new ObjectDisposedException(nameof(SqliteProvider));
                IntPtr statement;
                Check(prepare(database, Utf8(sql), -1, out statement, IntPtr.Zero));
                try
                {
                    for (var i = 0; i < arguments.Length; i++)
                    {
                        if (arguments[i] == null) throw new ArgumentNullException(nameof(arguments));
                        var text = Utf8(arguments[i]);
                        Check(bindText(statement, i + 1, text, text.Length - 1, new IntPtr(-1)));
                    }
                    var rows = new List<string[]>();
                    int result;
                    while ((result = step(statement)) == 100)
                    {
                        var row = new string[columnCount(statement)];
                        for (var i = 0; i < row.Length; i++) row[i] = Text(columnText(statement, i));
                        rows.Add(row);
                    }
                    if (result != 101) Check(result);
                    return rows;
                }
                finally { finalize(statement); }
            }
        }
        private void Check(int result)
        {
            if (result != 0) throw new InvalidOperationException("SQLITE_" + result + ": " + (database == IntPtr.Zero ? "open failed" : Text(errorMessage(database))));
        }
        private T Load<T>(string name) where T : class
        {
            var address = dlsym(library, name);
            if (address == IntPtr.Zero) throw new EntryPointNotFoundException(name);
            return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
        }
        internal static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + "\0");
        private static string Text(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            var length = 0;
            while (Marshal.ReadByte(ptr, length) != 0) length++;
            var bytes = new byte[length]; Marshal.Copy(ptr, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        public void Dispose()
        {
            lock (Gate)
            {
                if (database != IntPtr.Zero) { close(database); database = IntPtr.Zero; }
                if (library != IntPtr.Zero) { dlclose(library); library = IntPtr.Zero; }
            }
        }
        [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlopen(string path, int flags);
        [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlsym(IntPtr handle, string name);
        [DllImport("/usr/lib/libSystem.B.dylib")] private static extern int dlclose(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Version();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Source();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Open(byte[] path, out IntPtr db, int flags, IntPtr vfs);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Close(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Prepare(IntPtr db, byte[] sql, int bytes, out IntPtr statement, IntPtr tail);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Step(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FinalizeStatement(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int BindText(IntPtr statement, int index, byte[] text, int length, IntPtr destructor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ColumnText(IntPtr statement, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ColumnCount(IntPtr statement);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ErrorMessage(IntPtr db);
    }
}
