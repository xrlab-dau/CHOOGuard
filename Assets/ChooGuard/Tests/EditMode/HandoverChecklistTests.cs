#if UNITY_INCLUDE_TESTS
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;

namespace ChooGuard.Tests.EditMode
{
    public sealed class HandoverChecklistTests
    {
        [Test] public void EverySectionMustBeReviewedBeforeHandover()
        {
            var review = new HandoverChecklist();
            Assert.That(review.Ready, Is.False);
            review.Update(new[] { "observed incident", "unknown casualties", "staff actions", "agency work" });
            for (int i = 0; i < 3; i++) review.Review(i);
            Assert.That(review.Ready, Is.False);
            review.Review(3);
            Assert.That(review.Ready, Is.True);
        }
        [Test] public void NewObservedInformationInvalidatesPriorReview()
        {
            var review = new HandoverChecklist();
            review.Update(new[] { "smoke observed", "no count verified" });
            review.Review(0); review.Review(1);
            Assert.That(review.Update(new[] { "smoke observed", "one patient observed" }), Is.True);
            Assert.That(review.Ready, Is.False);
            Assert.That(review.Reviewed(0), Is.False);
        }
        [Test] public void UnchangedSnapshotAndCallerMutationDoNotInvalidateOrSilentlyChangeReview()
        {
            var data = new[] { "same observed facts" };
            var review = new HandoverChecklist(); review.Update(data); review.Review(0);
            data[0] = "mutated externally";
            Assert.That(review.Section(0), Is.EqualTo("same observed facts"));
            Assert.That(review.Update(new[] { "same observed facts" }), Is.False);
            Assert.That(review.Ready, Is.True);
        }
    }
}
#endif
