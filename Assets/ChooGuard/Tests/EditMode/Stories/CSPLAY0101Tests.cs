#if UNITY_INCLUDE_TESTS
using ChooGuard.Presentation.Input;
using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Tests.EditMode.Stories
{
    public sealed class CSPLAY0101Tests
    {
        [Test]
        public void Capture_KeepsFirstOwnerUntilRelease_EvenWhenAnotherOwnerAttemptsDown()
        {
            var captures = new PointerCaptureOwner();
            var first = new InputOwner(InputContext.Hud, 7, null, null);
            Assert.That(captures.TryBegin(first), Is.True);
            Assert.That(captures.TryBegin(new InputOwner(InputContext.World, 7, null, null)), Is.False);
            Assert.That(captures.TryEnd(7, out var released), Is.True);
            Assert.That(released.Context, Is.EqualTo(InputContext.Hud));
            Assert.That(captures.TryEnd(7, out _), Is.False);
        }

        [Test]
        public void Cancel_DiscardsCapture_AndOrphanReleaseHasNoOwner()
        {
            var captures = new PointerCaptureOwner();
            captures.TryBegin(new InputOwner(InputContext.Hud, 3, null, null));
            Assert.That(captures.Cancel(3), Is.True);
            Assert.That(captures.TryEnd(3, out _), Is.False);
            Assert.That(captures.Count, Is.Zero);
        }

        [Test]
        public void Capture_StoresFocusAndModalAtDown_NotAChangingGlobalOwner()
        {
            var focus = new GameObject("focus");
            var modal = new GameObject("modal");
            try
            {
                var captures = new PointerCaptureOwner();
                captures.TryBegin(new InputOwner(InputContext.Modal, 1, focus, modal));
                captures.TryBegin(new InputOwner(InputContext.World, 2, null, null));
                Assert.That(captures.TryGet(1, out var owner), Is.True);
                Assert.That(owner.Focus, Is.SameAs(focus));
                Assert.That(owner.Modal, Is.SameAs(modal));
                Assert.That(captures.TryEnd(2, out var other), Is.True);
                Assert.That(other.Context, Is.EqualTo(InputContext.World));
                Assert.That(captures.Count, Is.EqualTo(1));
                captures.CancelAll();
                Assert.That(captures.Count, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(focus);
                Object.DestroyImmediate(modal);
            }
        }

        [Test]
        public void UiCapture_RemainsUntilItsOwnRelease_IndependentOfOtherStreams()
        {
            var captures = new PointerCaptureOwner();
            captures.TryBegin(new InputOwner(InputContext.Hud, 1, null, null));
            captures.TryBegin(new InputOwner(InputContext.Camera, 2, null, null));
            Assert.That(captures.HasUiCapture, Is.True);
            captures.TryEnd(2, out _);
            Assert.That(captures.HasUiCapture, Is.True);
            captures.TryEnd(1, out _);
            Assert.That(captures.HasUiCapture, Is.False);
        }

        [Test]
        public void None_IsNotACapturableOwner()
        {
            var captures = new PointerCaptureOwner();
            Assert.That(captures.TryBegin(new InputOwner(InputContext.None, 0, null, null)), Is.False);
            Assert.That(captures.Count, Is.Zero);
        }
    }
}
#endif
