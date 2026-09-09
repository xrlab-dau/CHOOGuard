using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using ChooGuard.Foundation.Demo;
using NUnit.Framework;

// Filesystem tests only: no screenshot, Unity serializer, or editor is substituted.
public sealed class CaptureWriterTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
    private static readonly byte[] SplitPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAAEHRFWHRTb3VyY2UAc3ludGhldGlj83pdwgAAAARJREFUeJxj+E0XbKoAAAAKSURBVM/A8B8EARD4A/3aUbJIAAAAAElFTkSuQmCC");
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private static string Serialize(ReconstructionCaptureWriter.CaptureReceipt value) => JsonSerializer.Serialize(value, Json);
    private static ReconstructionCaptureWriter NewWriter(string path) => new ReconstructionCaptureWriter(path, new[] { "view" }, new string('a', 64), "observed-unlit", "filesystem-test-only");
    private static void WriteAll(ReconstructionCaptureWriter writer)
    { for (var angle = 0; angle < 3; angle++) writer.WriteImage(0, angle, Png); }
    private static void WithOutput(Action<string> test)
    {
        var parent = Path.Combine(Path.GetTempPath(), "chooguard-capture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try { test(Path.Combine(parent, "run")); }
        finally { Directory.Delete(parent, true); }
    }

    [Test]
    public void CompleteBindsAllSixPersistedFilesToSnapshotViewsAnglesAndHashes()
    {
        WithOutput(path =>
        {
            var views = new[] { "second", "first" };
            var writer = new ReconstructionCaptureWriter(path, views, new string('a', 64), "observed-unlit", "filesystem-test-only");
            views[0] = "changed";
            for (var view = 0; view < 2; view++)
                for (var angle = 0; angle < 3; angle++) writer.WriteImage(view, angle, Png);
            var destination = Path.Combine(path, "capture-complete.json");
            Assert.That(File.Exists(destination), Is.False);
            writer.Complete(Serialize);
            var receipt = JsonSerializer.Deserialize<ReconstructionCaptureWriter.CaptureReceipt>(File.ReadAllText(destination), Json);
            Assert.That(receipt.schemaVersion, Is.EqualTo("unity-reconstruction-capture-2"));
            Assert.That(receipt.fbxSha256, Is.EqualTo(new string('a', 64)));
            Assert.That(receipt.views, Is.EqualTo(new[] { "second", "first" }));
            Assert.That(receipt.images.Length, Is.EqualTo(6));
            Assert.That(receipt.scope, Does.Contain("no metric, collision, facility or VR acceptance"));
            for (var index = 0; index < 6; index++)
            {
                var item = receipt.images[index];
                Assert.That(item.file, Is.EqualTo($"view-{index / 3}-{index % 3}.png"));
                Assert.That(item.view, Is.EqualTo(receipt.views[index / 3]));
                Assert.That(item.angle, Is.EqualTo(receipt.angles[index % 3]));
                var persisted = File.ReadAllBytes(Path.Combine(path, item.file));
                Assert.That(persisted, Is.EqualTo(Png));
                Assert.That(item.bytes, Is.EqualTo(persisted.LongLength));
                Assert.That(item.sha256, Is.EqualTo(Convert.ToHexString(SHA256.HashData(persisted)).ToLowerInvariant()));
            }
            Assert.That(File.Exists(destination + ".pending"), Is.False);
            Assert.Throws<InvalidOperationException>(() => writer.Complete(Serialize));
        });
    }

    [Test]
    public void PngWithAncillaryAndSplitImageChunksRetainsItsEncodedBytes()
    {
        // A standard-library zlib fixture: 2x1 RGBA, tEXt metadata and two consecutive IDAT chunks.
        WithOutput(path =>
        {
            var writer = NewWriter(path);
            for (var angle = 0; angle < 3; angle++) writer.WriteImage(0, angle, SplitPng);
            writer.Complete(Serialize);
            var receipt = JsonSerializer.Deserialize<ReconstructionCaptureWriter.CaptureReceipt>(File.ReadAllText(Path.Combine(path, "capture-complete.json")), Json);
            foreach (var item in receipt.images)
                Assert.That(File.ReadAllBytes(Path.Combine(path, item.file)), Is.EqualTo(SplitPng));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExistingDirectoryAndOldReceiptStayUnchanged(bool owned)
    {
        WithOutput(path =>
        {
            Directory.CreateDirectory(path);
            if (owned) File.WriteAllText(Path.Combine(path, "owner.txt"), "chooguard.reconstruction-captures.v1");
            File.WriteAllBytes(Path.Combine(path, "view-0-0.png"), Png);
            File.WriteAllText(Path.Combine(path, "capture-complete.json"), "old-receipt");
            Assert.Throws<InvalidOperationException>(() => NewWriter(path));
            Assert.That(File.ReadAllText(Path.Combine(path, "capture-complete.json")), Is.EqualTo("old-receipt"));
            Assert.That(File.ReadAllBytes(Path.Combine(path, "view-0-0.png")), Is.EqualTo(Png));
        });
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(8)]
    [TestCase(10)]
    public void FailedEncodingCannotBeRecoveredIntoASuccessReceipt(int size)
    {
        WithOutput(path =>
        {
            var writer = NewWriter(path);
            Assert.Throws<InvalidOperationException>(() => writer.WriteImage(0, 0, size < 0 ? null : new byte[size]));
            Assert.Throws<InvalidOperationException>(() => writer.WriteImage(0, 0, Png));
            Assert.Throws<InvalidOperationException>(() => writer.Complete(Serialize));
            Assert.That(File.Exists(Path.Combine(path, "capture-complete.json")), Is.False);
        });
    }

    [TestCase("signature-prefix")]
    [TestCase("missing-end")]
    [TestCase("bad-image-crc")]
    [TestCase("chunk-length-overflow")]
    [TestCase("missing-image")]
    [TestCase("trailing-bytes")]
    [TestCase("duplicate-header")]
    [TestCase("nonconsecutive-image")]
    public void MalformedPngCannotProduceACompleteReceipt(string fault)
    {
        WithOutput(path =>
        {
            byte[] malformed;
            switch (fault)
            {
                case "signature-prefix": malformed = Png.Take(9).ToArray(); break;
                case "missing-end": malformed = Png.Take(Png.Length - 12).ToArray(); break;
                case "missing-image": malformed = Png.Take(33).Concat(Png.Skip(Png.Length - 12)).ToArray(); break;
                case "trailing-bytes": malformed = Png.Concat(new byte[] { 0 }).ToArray(); break;
                case "duplicate-header": malformed = Png.Take(33).Concat(Png.Skip(8)).ToArray(); break;
                // Move the intact tEXt chunk between IDAT chunks; all chunk checksums still match.
                case "nonconsecutive-image": malformed = SplitPng.Take(33).Concat(SplitPng.Skip(61).Take(16)).Concat(SplitPng.Skip(33).Take(28)).Concat(SplitPng.Skip(77)).ToArray(); break;
                // Original success fixture: the IDAT CRC is ef9af564; its data requires efa2a75b.
                case "bad-image-crc": malformed = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9WQAAAAASUVORK5CYII="); break;
                default:
                    malformed = (byte[])Png.Clone();
                    for (var index = 8; index < 12; index++) malformed[index] = 255;
                    break;
            }
            var writer = NewWriter(path);
            Assert.Throws<InvalidOperationException>(() => writer.WriteImage(0, 0, malformed));
            Assert.Throws<InvalidOperationException>(() => writer.WriteImage(0, 0, Png));
            Assert.Throws<InvalidOperationException>(() => writer.Complete(Serialize));
            Assert.That(File.Exists(Path.Combine(path, "view-0-0.png")), Is.False);
            Assert.That(File.Exists(Path.Combine(path, "capture-complete.json")), Is.False);
        });
    }

    [Test]
    public void DigestWithTrailingNewlineIsRejectedBeforeCreatingOutput()
    {
        WithOutput(path =>
        {
            Assert.Throws<ArgumentException>(() => new ReconstructionCaptureWriter(path, new[] { "view" }, new string('a', 64) + "\n", "observed-unlit", "fixture"));
            Assert.That(Directory.Exists(path), Is.False);
        });
    }

    [Test]
    public void MissingImageDoesNotPublishReceipt()
    {
        WithOutput(path =>
        {
            var writer = NewWriter(path); writer.WriteImage(0, 0, Png);
            Assert.Throws<InvalidOperationException>(() => writer.Complete(Serialize));
            Assert.That(File.Exists(Path.Combine(path, "capture-complete.json")), Is.False);
        });
    }

    [Test]
    public void MidrunWriteFailureDoesNotPublishReceipt()
    {
        WithOutput(path =>
        {
            var writer = NewWriter(path); writer.WriteImage(0, 0, Png);
            Directory.CreateDirectory(Path.Combine(path, "view-0-1.png"));
            var error = Assert.Catch(() => writer.WriteImage(0, 1, Png));
            Assert.That(error is IOException || error is UnauthorizedAccessException, Is.True);
            Assert.Throws<InvalidOperationException>(() => writer.Complete(Serialize));
            Assert.That(File.Exists(Path.Combine(path, "capture-complete.json")), Is.False);
        });
    }

    [Test]
    public void PersistedImageTamperingPreventsCompletion()
    {
        WithOutput(path =>
        {
            var writer = NewWriter(path); WriteAll(writer);
            File.WriteAllBytes(Path.Combine(path, "view-0-0.png"), Png.Reverse().ToArray());
            Assert.Throws<InvalidOperationException>(() => writer.Complete(Serialize));
            Assert.That(File.Exists(Path.Combine(path, "capture-complete.json")), Is.False);
        });
    }

    [Test]
    public void SerializationFailureNeverPublishesPartialReceipt()
    {
        WithOutput(path =>
        {
            var writer = NewWriter(path); WriteAll(writer);
            Assert.Throws<IOException>(() => writer.Complete(_ => throw new IOException("serialization failure")));
            Assert.That(File.Exists(Path.Combine(path, "capture-complete.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(path, "capture-complete.json.pending")), Is.False);
        });
    }
}
