using NUnit.Framework;
using UnityEditor;
using ChooGuard.Foundation.Multiplayer.Editor;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class NativeSdkImportTests
    {
        [Test]
        public void LinuxVoiceLibraryCannotBeSelectedForMacOrWindows()
        {
            var importer = AssetImporter.GetAtPath(NativeSdkImportPolicy.OwnedRoot + "/ffi-linux-x86_64/liblivekit_ffi.so") as PluginImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX), Is.False);
            Assert.That(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64), Is.False);
            Assert.That(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64), Is.True);
        }
    }
}
