#if UNITY_INCLUDE_TESTS
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// The fire lesson's order score (docs/fire-tutorial.md "순서 기준", 2026-11-20): 신고 → 대피 유도 → 초기 진압, after the
    /// 코레일 둔내역·수색역 합동소방훈련. Evacuation before suppression is no longer a mistake; taking the extinguisher before
    /// guiding passengers (by PA or in person) is. The lesson is built without a session: it only records order.
    /// </summary>
    public sealed class FireTutorialScoreTests
    {
        static string[] Keys(FireTutorialLesson lesson) => lesson.Mistakes.Select(mistake => mistake.Key).ToArray();

        [Test]
        public void EvacuationBeforeSuppressionIsNotPenalised()
        {
            var lesson = new FireTutorialLesson(null);
            lesson.OnCallPoint();
            lesson.OnEvacuationAnnounced();
            Assert.That(lesson.EvacuationGuided, Is.True);
            Assert.That(lesson.Mistakes, Is.Empty, "진화 전 대피방송은 감점하지 않습니다.");
        }

        [Test]
        public void ExtinguisherBeforeAnyGuidanceIsPenalisedOnce()
        {
            var lesson = new FireTutorialLesson(null);
            lesson.OnExtinguisherPicked();
            Assert.That(Keys(lesson), Does.Contain(FireTutorialLesson.Mistake.ExtinguisherBeforeEvacuation.Key));
            Assert.That(Keys(lesson), Does.Contain(FireTutorialLesson.Mistake.ExtinguisherBeforeReport.Key));
            Assert.That(lesson.OrderScore, Is.EqualTo(FireTutorialLesson.FullScore - 20));
        }

        [Test]
        public void GuidingPassengersInPersonCountsAsEvacuationGuidance()
        {
            var lesson = new FireTutorialLesson(null);
            lesson.OnPassengersGuided(3);
            lesson.OnExtinguisherPicked();
            Assert.That(lesson.EvacuationGuided, Is.True);
            Assert.That(Keys(lesson), Does.Not.Contain(FireTutorialLesson.Mistake.ExtinguisherBeforeEvacuation.Key));
        }

        [Test]
        public void ReceiverResetBeforeHandoverIsStillPenalised()
        {
            var lesson = new FireTutorialLesson(null);
            lesson.OnPassengersGuided(1);
            lesson.OnReceiverResetRequested();
            Assert.That(Keys(lesson), Is.EqualTo(new[] { FireTutorialLesson.Mistake.ReceiverResetBeforeHandover.Key }));
        }
    }
}
#endif
