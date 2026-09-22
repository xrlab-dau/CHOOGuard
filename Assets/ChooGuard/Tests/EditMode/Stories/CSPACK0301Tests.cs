#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.Content;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class CSPACK0301Tests
    {
        private static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aX1sAAAAASUVORK5CYII=");
        private static AssetIntakeProfile Profile(long raw = 1000000, int count = 100,
            long total = 1000000, long entry = 1000000, double ratio = 10000,
            IEnumerable<string> extensions = null) => new AssetIntakeProfile(raw, count, total, entry, ratio,
                extensions ?? new[] { ".png", ".jpg", ".ogg", ".otf", ".glb", ".obj", ".fbx", ".zip" });
        private static AssetIntakeResult Check(byte[] bytes, string name = "asset.zip", AssetIntakeProfile profile = null,
            string tier = null, string url = "https://example.invalid/source") => AssetIntakeValidator.Validate(
                new AssetIntakeRequest("asset-1", url, name, tier, null, bytes, profile ?? Profile()));
        private static byte[] Zip(params string[] names) => ZipBytes(names.Select(n => new KeyValuePair<string, byte[]>(n,
            n.EndsWith("/", StringComparison.Ordinal) ? Array.Empty<byte>() : Png())).ToArray());
        private static byte[] ZipBytes(params KeyValuePair<string, byte[]>[] members)
        {
            using (var memory = new MemoryStream())
            {
                using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
                    foreach (var member in members)
                        using (var stream = zip.CreateEntry(member.Key).Open())
                            stream.Write(member.Value, 0, member.Value.Length);
                return memory.ToArray();
            }
        }
        private static void Has(AssetIntakeResult result, AssetIntakeIssueCode code) =>
            Assert.That(result.Issues.Select(i => i.Code), Does.Contain(code));

        [Test]
        public void MissingRawPreservesUnknownMetadataAndNeverInventsHash()
        {
            var r = Check(null);
            Assert.That(r.Status, Is.EqualTo(AssetIntakeStatus.NOT_ACQUIRED));
            Assert.That(r.Receipt.RawHash, Is.Null);
            Assert.That(r.Receipt.RawByteCount, Is.Null);
            Assert.That(r.Receipt.Tier, Is.Null);
            Assert.That(r.Receipt.AcquiredAt, Is.Null);
            Assert.That(r.Receipt.ImportStatus, Is.EqualTo("NOT_RUN"));
            Assert.That(r.SafeImportApproved, Is.False);
        }

        [Test]
        public void ActualBytesDetermineHashIndependentlyOfTier()
        {
            var a = Png(); var b = Png(); b[b.Length - 1] ^= 1;
            var first = Check(a, "asset.png", tier: "free");
            var otherTier = Check(a, "asset.png", tier: "unknown");
            var otherBytes = Check(b, "asset.png", tier: "free");
            using (var sha = SHA256.Create())
                Assert.That(first.Receipt.RawHash, Is.EqualTo(BitConverter.ToString(sha.ComputeHash(a)).Replace("-", "").ToLowerInvariant()));
            Assert.That(otherTier.Receipt.RawHash, Is.EqualTo(first.Receipt.RawHash));
            Assert.That(otherBytes.Receipt.RawHash, Is.Not.EqualTo(first.Receipt.RawHash));
            Assert.That(first.Receipt.RawByteCount, Is.EqualTo(a.LongLength));
            Assert.That(otherTier.Receipt.Tier, Is.EqualTo("unknown"));
        }

        [Test]
        public void RealZipStreamsProduceLimitedImmutableInspectionReceipt()
        {
            var r = Check(Zip("images/", "images/a.png"));
            Assert.That(r.Status, Is.EqualTo(AssetIntakeStatus.STATIC_CHECKS_PASSED));
            Assert.That(r.Receipt.Entries.Count, Is.EqualTo(2));
            Assert.That(r.Receipt.Entries[1].Format, Is.EqualTo("PNG"));
            Assert.That(r.Receipt.TotalUncompressedBytes, Is.EqualTo(Png().Length));
            Assert.That(r.Receipt.RigClipsStatus, Is.EqualTo("NOT_VERIFIED"));
            Assert.That(r.Receipt.PartsStatus, Is.EqualTo("NOT_VERIFIED"));
            Assert.That(r.Receipt.QualificationStatus, Is.EqualTo("INCOMPLETE"));
            Assert.That(r.SafeImportApproved, Is.False);
            Assert.That(r.Receipt.MalwareStatus, Is.EqualTo("NOT_ASSESSED"));
            Assert.Throws<NotSupportedException>(() => ((IList<AssetIntakeEntry>)r.Receipt.Entries).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<AssetIntakeIssue>)r.Issues).Clear());
        }

        [TestCase("../a.png")][TestCase("a/../b.png")][TestCase("/a.png")]
        [TestCase("C:/a.png")][TestCase("C:a.png")][TestCase("\\\\server\\a.png")]
        [TestCase("a\\..\\b.png")][TestCase("./a.png")][TestCase("a//b.png")]
        [TestCase("a/./b.png")][TestCase("a /b.png")][TestCase("a./b.png")]
        [TestCase("NUL.png")][TestCase("a:stream.png")][TestCase("a/CON/x.png")]
        [TestCase("a\u0001.png")][TestCase("a?.png")]
        public void PortablePathAbusesAreRejected(string name) => Has(Check(Zip(name)), AssetIntakeIssueCode.INVALID_PATH);

        [TestCase("a.png", "a.png")][TestCase("a.png", "A.PNG")]
        [TestCase("\u00e9.png", "e\u0301.png")][TestCase("a.png", "a.png/b.png")]
        [TestCase("a.png/b.png", "a.png")][TestCase("a.png/", "a.png")]
        [TestCase("A/x.png", "a/y.png")][TestCase("é/x.png", "é/y.png")]
        public void DuplicateNormalizedPathsAndFileAncestorsAreRejected(string first, string second) =>
            Has(Check(Zip(first, second)), AssetIntakeIssueCode.PATH_COLLISION);

        [Test]
        public void EverySymlinkIsUnsupportedEvenInsideArchive()
        {
            byte[] bytes;
            using (var memory = new MemoryStream())
            {
                using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
                {
                    var entry = zip.CreateEntry("inside.png");
                    entry.ExternalAttributes = unchecked((int)0xA1FF0000);
                    using (var stream = entry.Open()) { var raw = Png(); stream.Write(raw, 0, raw.Length); }
                }
                bytes = memory.ToArray();
            }
            Has(Check(bytes), AssetIntakeIssueCode.UNSUPPORTED_SYMLINK);
        }

        [TestCase("exe")][TestCase("dll")][TestCase("cs")][TestCase("sh")][TestCase("ps1")]
        [TestCase("js")][TestCase("py")][TestCase("docm")][TestCase("vbs")][TestCase("bat")]
        public void DeniedExtensionsCannotBeEnabledByProfile(string extension)
        {
            var name = "payload." + extension;
            Has(Check(Zip(name), profile: Profile(extensions: new[] { "." + extension })), AssetIntakeIssueCode.DISALLOWED_CONTENT);
        }

        [Test]
        public void ExecutableSignatureDisguisedAsImageIsDenied()
        {
            Has(Check(new byte[] { 77, 90, 0, 0 }, "x.png"), AssetIntakeIssueCode.DISALLOWED_CONTENT);
            Has(Check(ZipBytes(new KeyValuePair<string, byte[]>("x.png", new byte[] { 127, 69, 76, 70 }))), AssetIntakeIssueCode.DISALLOWED_CONTENT);
        }

        [Test]
        public void NestedArchiveIsRejectedByExtensionAndByActualMagic()
        {
            Has(Check(Zip("nested.zip")), AssetIntakeIssueCode.UNSUPPORTED_NESTED_ARCHIVE);
            Has(Check(ZipBytes(new KeyValuePair<string, byte[]>("nested.png", Zip("a.png")))), AssetIntakeIssueCode.UNSUPPORTED_NESTED_ARCHIVE);
        }

        [Test]
        public void BadMagicTruncatedZipAndEmptyArchiveNeverPass()
        {
            Has(Check(new byte[] { 1, 2, 3 }, "x.png"), AssetIntakeIssueCode.MAGIC_MISMATCH);
            Has(Check(ZipBytes(new KeyValuePair<string, byte[]>("x.png", new byte[] { 1, 2, 3 }))), AssetIntakeIssueCode.MAGIC_MISMATCH);
            var bytes = Zip("a.png"); Array.Resize(ref bytes, bytes.Length - 9);
            Has(Check(bytes), AssetIntakeIssueCode.INVALID_ARCHIVE);
            Has(Check(Zip()), AssetIntakeIssueCode.EMPTY_ARCHIVE);
            Has(Check(new byte[] { 1, 2, 3 }, "x.unknown"), AssetIntakeIssueCode.UNSUPPORTED_FORMAT);
        }

        [TestCase("jpg", "JPEG")][TestCase("ogg", "OGG")][TestCase("otf", "OTF")][TestCase("glb", "GLB")]
        public void SupportedHeadersAreIdentifiedWithoutClaimingSemanticValidation(string extension, string format)
        {
            // 각 형식의 최소 식별 헤더 fixture이며 실제 디코딩 성공 증거가 아니다.
            byte[] bytes;
            switch (extension)
            {
                case "jpg": bytes = new byte[10]; bytes[0] = 255; bytes[1] = 216; bytes[2] = 255; bytes[3] = 224; break;
                case "ogg": bytes = new byte[27]; Array.Copy(new byte[] { 79, 103, 103, 83 }, bytes, 4); break;
                case "otf": bytes = new byte[28]; Array.Copy(new byte[] { 79, 84, 84, 79 }, bytes, 4); bytes[5] = 1; break;
                default:
                    bytes = new byte[24]; Array.Copy(new byte[] { 103, 108, 84, 70, 2, 0, 0, 0, 24, 0, 0, 0,
                        4, 0, 0, 0, 74, 83, 79, 78, 123, 125, 32, 32 }, bytes, 24); break;
            }
            var r = Check(bytes, "x." + extension);
            Assert.That(r.Receipt.RawFormat, Is.EqualTo(format));
            Assert.That(r.Status, Is.EqualTo(AssetIntakeStatus.STATIC_CHECKS_PASSED));
            Assert.That(r.SafeImportApproved, Is.False);
            var zipped = Check(ZipBytes(new KeyValuePair<string, byte[]>("x." + extension, bytes)));
            Assert.That(zipped.Receipt.Entries.Single().Format, Is.EqualTo(format));
            bytes[0] = 0;
            Has(Check(bytes, "x." + extension), AssetIntakeIssueCode.MAGIC_MISMATCH);
        }

        [Test]
        public void TopLevelZipCannotLaunderDeniedOrMismatchedExtension()
        {
            Has(Check(Zip("x.png"), "x.cs"), AssetIntakeIssueCode.DISALLOWED_CONTENT);
            Has(Check(Zip("x.png"), "x.png"), AssetIntakeIssueCode.MAGIC_MISMATCH);
        }

        [Test]
        public void ForgedHugeCentralDirectoryLengthFailsBeforeAllocation()
        {
            var bytes = Zip("x.png");
            for (var i = 0; i <= bytes.Length - 46; i++)
            {
                if (bytes[i] != 80 || bytes[i + 1] != 75 || bytes[i + 2] != 1 || bytes[i + 3] != 2) continue;
                // uint 최댓값 바로 아래: ZIP64 sentinel을 피하면서 int 범위를 초과한다.
                bytes[i + 24] = 254; bytes[i + 25] = 255; bytes[i + 26] = 255; bytes[i + 27] = 255;
                break;
            }
            Has(Check(bytes), AssetIntakeIssueCode.ENTRY_BYTES_LIMIT);
        }

        [Test]
        public void UnsupportedModelSemanticsRemainIncompleteNotSafeImport()
        {
            var r = Check(Zip("model.fbx", "model.obj"));
            Assert.That(r.Status, Is.EqualTo(AssetIntakeStatus.QUALIFICATION_INCOMPLETE));
            Has(r, AssetIntakeIssueCode.SEMANTIC_VALIDATION_UNSUPPORTED);
            Assert.That(r.SafeImportApproved, Is.False);
        }

        [Test]
        public void EveryProfileLimitRejectsItsOwnBoundaryViolation()
        {
            var bytes = Zip("a.png", "b.png"); var length = Png().Length;
            Has(Check(bytes, profile: Profile(raw: bytes.Length - 1)), AssetIntakeIssueCode.RAW_BYTES_LIMIT);
            Has(Check(bytes, profile: Profile(count: 1)), AssetIntakeIssueCode.ENTRY_COUNT_LIMIT);
            Has(Check(bytes, profile: Profile(total: length * 2 - 1)), AssetIntakeIssueCode.TOTAL_BYTES_LIMIT);
            Has(Check(bytes, profile: Profile(entry: length - 1)), AssetIntakeIssueCode.ENTRY_BYTES_LIMIT);
            Has(Check(bytes, profile: Profile(ratio: 0.001)), AssetIntakeIssueCode.COMPRESSION_RATIO_LIMIT);
            Has(Check(bytes, profile: Profile(extensions: new[] { ".jpg" })), AssetIntakeIssueCode.EXTENSION_NOT_ALLOWED);
            Assert.That(Check(bytes, profile: Profile(raw: bytes.Length, count: 2, total: length * 2, entry: length)).Status,
                Is.EqualTo(AssetIntakeStatus.STATIC_CHECKS_PASSED));
        }

        [Test]
        public void RawImagesAlsoRespectUncompressedAndPerEntryBounds()
        {
            Has(Check(Png(), "x.png", Profile(total: 1)), AssetIntakeIssueCode.TOTAL_BYTES_LIMIT);
            Has(Check(Png(), "x.png", Profile(entry: 1)), AssetIntakeIssueCode.ENTRY_BYTES_LIMIT);
        }

        [TestCase("http://example.invalid/a")][TestCase("file:///a")][TestCase("not a url")]
        [TestCase("https://user:password@example.invalid/a")]
        public void OnlyCredentialFreeAbsoluteHttpsSourceIsAccepted(string url) =>
            Has(Check(Png(), "x.png", url: url), AssetIntakeIssueCode.INVALID_SOURCE_URL);

        [Test]
        public void InputOwnershipAndNullableTimestampArePreserved()
        {
            var bytes = Png(); var extensions = new List<string> { ".png" };
            var profile = Profile(extensions: extensions);
            var when = new DateTimeOffset(2026, 9, 20, 1, 0, 0, TimeSpan.Zero);
            var request = new AssetIntakeRequest("asset-1", "https://example.invalid/a", "x.png", null, when, bytes, profile);
            bytes[0] = 0; extensions.Clear(); request.RawBytes[0] = 0;
            Assert.That(AssetIntakeValidator.Validate(request).Status, Is.EqualTo(AssetIntakeStatus.STATIC_CHECKS_PASSED));
            Assert.That(AssetIntakeValidator.Validate(request).Receipt.AcquiredAt, Is.EqualTo(when));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)profile.AllowedExtensions).Clear());
        }

        [Test]
        public void InvalidProfilesAndNullRequestFailExplicitly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(raw: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(count: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(total: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(entry: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(ratio: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Profile(ratio: double.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => AssetIntakeValidator.Validate(null));
            Assert.That(Check(Png(), "x.png", Profile(raw: long.MaxValue, total: long.MaxValue, entry: long.MaxValue)).Status,
                Is.EqualTo(AssetIntakeStatus.STATIC_CHECKS_PASSED));
        }

        [Test]
        public void ValidationDoesNotExtractArchiveMembersToDisk()
        {
            var name = "intake-no-write-" + Guid.NewGuid().ToString("N") + ".png";
            var path = Path.Combine(Directory.GetCurrentDirectory(), name);
            Assert.That(File.Exists(path), Is.False);
            Check(Zip(name));
            Assert.That(File.Exists(path), Is.False);
        }
    }
}
#endif
