using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;

namespace ChooGuard.Tests.PlayMode
{
    // 실시간 디렉터의 추첨: 얼마나 자주·얼마나 늦게 물었는지가 아니라 지나간 게임 시간만이 사건 확률을 정한다.
    public sealed class DirectorRatesTests
    {
        private static float EventShare(float[] rates, float total, int steps, int trials, int seed, out float firstShare)
        {
            var random = new System.Random(seed);
            int events = 0, first = 0;
            for (int trial = 0; trial < trials; trial++)
                for (int step = 0; step < steps; step++)
                {
                    int pick = CompetingRisks.Draw(rates, total / steps, random);
                    if (pick < 0) continue;
                    events++;
                    if (pick == 0) first++;
                    break;
                }
            firstShare = events == 0 ? 0 : first / (float)events;
            return events / (float)trials;
        }

        [Test]
        public void ChanceOfAnEventDependsOnGameTimeCoveredNotOnHowOftenItIsDrawn()
        {
            var rates = new[] { .03f, .02f };
            float expected = 1 - (float)System.Math.Exp(-.05 * 10);

            float once = EventShare(rates, 10, 1, 20000, 7, out float onceFirst);
            float tenSteps = EventShare(rates, 10, 10, 20000, 8, out float tenFirst);
            float hundredSteps = EventShare(rates, 10, 100, 20000, 9, out _);

            Assert.That(once, Is.EqualTo(expected).Within(.02f));
            Assert.That(tenSteps, Is.EqualTo(expected).Within(.02f), "같은 10 s 를 열 번에 나누어 추첨해도 사건 확률은 같아야 한다");
            Assert.That(hundredSteps, Is.EqualTo(expected).Within(.02f));
            Assert.That(onceFirst, Is.EqualTo(.6f).Within(.03f), "어느 후보가 일어나는지는 빈도에 비례한다");
            Assert.That(tenFirst, Is.EqualTo(.6f).Within(.03f));
        }

        [Test]
        public void NothingHappensWithoutRateOrWithoutElapsedTime()
        {
            var random = new System.Random(1);

            Assert.That(CompetingRisks.Draw(new[] { 0f, 0f }, 100, random), Is.EqualTo(-1));
            Assert.That(CompetingRisks.Draw(new[] { 5f }, 0, random), Is.EqualTo(-1), "시간이 흐르지 않았으면 아무리 임박해도 일어나지 않는다");
            Assert.That(CompetingRisks.Draw(new float[0], 100, random), Is.EqualTo(-1));
        }

        [Test]
        public void JevLevelsBecomeAHazardRateWithoutInterpolationBetweenLevels()
        {
            var never = new[] { 1f, 0f, 0f, 0f, 0f };
            var unremarkable = new[] { 0f, 1f, 0f, 0f, 0f };
            var imminent = new[] { 0f, 0f, 0f, 0f, 1f };
            var split = new[] { 0f, .5f, 0f, 0f, .5f };

            Assert.That(Imminence.Rate(ImminenceScale.CalmOrigin, never), Is.EqualTo(0));
            float low = Imminence.Rate(ImminenceScale.CalmOrigin, unremarkable), high = Imminence.Rate(ImminenceScale.CalmOrigin, imminent);
            Assert.That(high, Is.GreaterThan(low * 10), "임박한 후보는 평범한 후보보다 훨씬 자주 일어난다");
            Assert.That(Imminence.Rate(ImminenceScale.CalmOrigin, split), Is.EqualTo((low + high) / 2).Within(low * .001f), "수준 확률의 기댓값이다");
            Assert.That(Imminence.Rate(ImminenceScale.IncidentOrigin, unremarkable), Is.LessThan(low), "진행 중인 사건 곁의 별개 사건은 평시보다 드물다");
        }

        [Test]
        public void OriginsShareOneStationBudgetOnceMoreThanTheCalibratedNumberAreListed()
        {
            Assert.That(Imminence.OriginShare(Imminence.ReferenceOrigins), Is.EqualTo(1f));
            Assert.That(Imminence.OriginShare(1), Is.EqualTo(1f));
            Assert.That(Imminence.OriginShare(Imminence.ReferenceOrigins * 2), Is.EqualTo(.5f), "설비를 더 모델링해도 새 비상상황의 빈도는 그대로다");
        }
    }
}
