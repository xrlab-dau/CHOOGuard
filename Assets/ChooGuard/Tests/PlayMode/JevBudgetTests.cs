using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;

namespace ChooGuard.Tests.PlayMode
{
    // JEV 예산: 한 분이 가득 차도 디렉터의 몫이 남고, 토큰은 JEV 가 센 값으로 치른다.
    public sealed class JevBudgetTests
    {
        private static int Fill(JevBudget budget, JevLane lane, float now, long tokens)
        {
            int sent = 0;
            while (budget.CanSend(lane, now, tokens))
            {
                budget.End(budget.Begin(lane, now, tokens), tokens, 0, false);
                sent++;
            }
            return sent;
        }

        [Test]
        public void FullMinuteOfCrowdRequestsStillLeavesRoomForTheDirector()
        {
            var budget = new JevBudget();

            Fill(budget, JevLane.CrowdRoutine, 0, 100);
            Assert.That(budget.CanSend(JevLane.CrowdRoutine, 0), Is.False, "일상 판단은 위 차선의 몫을 넘볼 수 없다");
            Assert.That(budget.CanSend(JevLane.CrowdUrgent, 0), Is.True);
            Fill(budget, JevLane.CrowdUrgent, 0, 100);
            Assert.That(budget.CanSend(JevLane.CrowdUrgent, 0), Is.False);
            Assert.That(budget.CanSend(JevLane.Director, 0), Is.True, "디렉터는 군중이 한 분을 다 채워도 자기 몫을 쓴다");

            int director = Fill(budget, JevLane.Director, 0, 100);

            Assert.That(director, Is.GreaterThanOrEqualTo(100));
            Assert.That(budget.Usage(0).RequestsPerMinute, Is.EqualTo(JevBudget.MaxRequestsPerMinute), "한 분에 보내는 요청은 전체 한도를 넘지 않는다");
        }

        [Test]
        public void CrowdCannotSpendTheTokensReservedForTheDirector()
        {
            var budget = new JevBudget();

            Fill(budget, JevLane.CrowdRoutine, 0, 10000);
            Fill(budget, JevLane.CrowdUrgent, 0, 10000);

            Assert.That(budget.CanSend(JevLane.Director, 0, 300000), Is.True, "군중이 자기 몫을 다 써도 디렉터의 큰 요청 하나는 들어간다");
            Assert.That(budget.Usage(0).DollarsPerHour, Is.LessThanOrEqualTo(JevBudget.MaxDollarsPerHour));
        }

        [Test]
        public void JevCountReplacesTheEstimateAndALateAnswerDoesNotCorruptTheWindow()
        {
            var budget = new JevBudget();
            var quick = budget.Begin(JevLane.Director, 0, 1000);
            var slow = budget.Begin(JevLane.Director, 0, 1000);

            budget.End(quick, 400, 60, false);
            Assert.That(budget.Usage(JevLane.Director, 1).TokensPerMinute, Is.EqualTo(1400), "끝난 요청은 추정이 아니라 JEV 가 센 값으로 센다");

            Assert.That(budget.Usage(JevLane.Director, 61).TokensPerMinute, Is.EqualTo(0), "한 분이 지나면 창에서 빠진다");
            budget.End(slow, 300, 10, false);

            var usage = budget.Usage(JevLane.Director, 62);
            Assert.That(usage.TokensPerMinute, Is.EqualTo(0), "창을 벗어난 뒤 온 답이 창을 음수로 만들면 안 된다");
            Assert.That(usage.InputTokens, Is.EqualTo(700));
            Assert.That(usage.Dollars, Is.EqualTo(700 * JevBudget.DollarsPerMillionInputTokens / 1000000.0).Within(1e-12));
        }

        [Test]
        public void RequestWithoutAnAnswerKeepsItsEstimate()
        {
            var budget = new JevBudget();
            var ticket = budget.Begin(JevLane.CrowdRoutine, 0, 800);

            budget.End(ticket, null, 0, true);

            var usage = budget.Usage(JevLane.CrowdRoutine, 1);
            Assert.That(usage.Failures, Is.EqualTo(1));
            Assert.That(usage.InputTokens, Is.EqualTo(800), "응답이 없어도 요청은 청구됐을 수 있다");
        }

        [Test]
        public void LaneInFlightLimitHoldsUntilARequestEnds()
        {
            var budget = new JevBudget();
            var first = budget.Begin(JevLane.Director, 0, 100);
            budget.Begin(JevLane.Director, 0, 100);

            Assert.That(budget.CanSend(JevLane.Director, 0), Is.False);
            Assert.That(budget.CanSend(JevLane.CrowdUrgent, 0), Is.True, "차선마다 따로 센다");

            budget.End(first, 100, 0, false);
            Assert.That(budget.CanSend(JevLane.Director, 0), Is.True);
        }
    }
}
