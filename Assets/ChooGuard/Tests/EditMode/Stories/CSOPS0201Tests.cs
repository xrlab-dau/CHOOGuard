#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Security.Cryptography;
using ChooGuard.Persistence;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public class CSOPS0201Tests
    {
        [Test] public void MissingDeploymentIdentityFailsClosed()
        {
            Assert.Throws<ArgumentException>(() => new SqliteProvider("relative", "", "", "relative"));
        }
        [Test] public void WrongBinaryHashFailsBeforeNativeLoad()
        {
            var file = Path.GetTempFileName();
            try { Assert.Throws<InvalidOperationException>(() => new SqliteProvider(file, new string('0', 64), "pinned", Path.GetFullPath(file + ".db"))); }
            finally { File.Delete(file); }
        }
        // Real-file tests intentionally require an explicitly supplied local deployment identity.
        // Missing native support is ignored, not counted as a passed integration test.
        internal static SqliteProvider Open(string path)
        {
            var binary = Environment.GetEnvironmentVariable("CG_TEST_SQLITE_BINARY");
            var hash = Environment.GetEnvironmentVariable("CG_TEST_SQLITE_SHA256");
            var source = Environment.GetEnvironmentVariable("CG_TEST_SQLITE_SOURCE_ID");
            if (string.IsNullOrEmpty(binary) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(source))
                Assert.Ignore("NOT_VERIFIED: explicit SQLite binary/source-id/hash required.");
            return new SqliteProvider(binary, hash, source, path);
        }
        [Test] public void FileDatabaseReopensWithRequiredPragmas()
        {
            var root = Path.Combine(Path.GetTempPath(), "cg-sqlite-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try { using (var p = Open(Path.Combine(root, "run.db"))) p.VerifySettings(); using (var p = Open(Path.Combine(root, "run.db"))) p.VerifySettings(); }
            finally { Directory.Delete(root, true); }
        }
    }
}
#endif
