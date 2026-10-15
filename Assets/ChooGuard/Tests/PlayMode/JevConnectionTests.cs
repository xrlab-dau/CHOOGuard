using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    public sealed class JevConnectionTests
    {
        [TestCase(401, false, false, 0, 1, JevFailure.Authentication)]
        [TestCase(403, false, false, 0, 1, JevFailure.Authentication)]
        [TestCase(429, false, false, 0, 1, JevFailure.RateLimit)]
        [TestCase(408, false, false, 0, 1, JevFailure.Timeout)]
        [TestCase(504, false, false, 0, 1, JevFailure.Timeout)]
        [TestCase(0, false, true, 0, 1, JevFailure.Timeout)]
        [TestCase(0, false, false, 0, 1, JevFailure.Network)]
        [TestCase(502, false, false, 0, 1, JevFailure.Server)]
        [TestCase(400, false, false, 0, 1, JevFailure.RequestRejected)]
        [TestCase(200, false, false, 0, 1, JevFailure.InvalidResponse)]
        [TestCase(200, true, false, 0, 1, JevFailure.InvalidResponse)]
        [TestCase(200, true, false, 1, 2, JevFailure.PartialResponse)]
        [TestCase(200, true, false, 2, 2, JevFailure.None)]
        public void DistinguishesTransportFromUsableJudgements(long http, bool success, bool timeout, int valid, int expected, JevFailure reason)
        {
            Assert.That(JevConnection.Classify(http, success, timeout, valid, expected), Is.EqualTo(reason));
        }

        [Test]
        public void CrowdOutageDoesNotClaimThatIncidentJudgementsFailed()
        {
            var connection = new JevConnection();
            connection.Observe(JevLane.Director, 1, JevFailure.None, 10);
            connection.Observe(JevLane.CrowdUrgent, 2, JevFailure.Timeout, 11);
            Assert.That(connection.Failures(JevLane.Director), Is.Zero);
            Assert.That(connection.Describe(JevLane.Director, 11), Does.StartWith("정상"));
            Assert.That(connection.Summary(11, ""), Does.StartWith("JEV 사건 판단 정상"));
            Assert.That(connection.Summary(11, ""), Does.Contain("시민 대응: 응답 시간 초과"));
            connection.Observe(JevLane.CrowdRoutine, 3, JevFailure.None, 12);
            Assert.That(connection.Failures(JevLane.CrowdUrgent), Is.EqualTo(1));
        }

        [Test]
        public void TransientFailureBacksOffWithoutCountingPollsThenRecovers()
        {
            var connection = new JevConnection();
            float now = 10;
            var delays = new[] { 2f, 4f, 8f, 16f, 30f, 30f };
            for (int i = 0; i < delays.Length; i++)
            {
                connection.Observe(JevLane.Director, i + 1, JevFailure.Network, now);
                Assert.That(connection.RetryIn(JevLane.Director, now), Is.EqualTo(delays[i]));
                for (int poll = 0; poll < 100; poll++)
                {
                    Assert.That(connection.CanSend(JevLane.Director, now + 1), Is.False);
                    connection.Blocked(JevLane.Director, JevSendBlock.RetryDelay);
                }
                Assert.That(connection.Failures(JevLane.Director), Is.EqualTo(i + 1));
                Assert.That(connection.CanSend(JevLane.CrowdUrgent, now), Is.True);
                now += delays[i];
                Assert.That(connection.CanSend(JevLane.Director, now), Is.True);
            }
            connection.Observe(JevLane.Director, 7, JevFailure.None, now);
            Assert.That(connection.Failures(JevLane.Director), Is.Zero);
            Assert.That(connection.Describe(JevLane.Director, now), Does.StartWith("연결 회복 확인 중"));
        }

        [Test]
        public void LateOlderFailureDoesNotOverwriteNewerSuccess()
        {
            var connection = new JevConnection();
            connection.Observe(JevLane.Director, 2, JevFailure.None, 10);
            connection.Observe(JevLane.Director, 1, JevFailure.Timeout, 11);
            Assert.That(connection.Failures(JevLane.Director), Is.Zero);
            Assert.That(connection.CanSend(JevLane.Director, 11), Is.True);
        }

        [Test]
        public void OneAnswerDoesNotRestoreABurstAndThreeCompleteAnswersConfirmRecovery()
        {
            var connection = new JevConnection();
            connection.Observe(JevLane.CrowdUrgent, 1, JevFailure.Timeout, 0);
            connection.Observe(JevLane.CrowdUrgent, 2, JevFailure.None, 2);
            Assert.That(connection.Recovering(JevLane.CrowdUrgent), Is.True);
            Assert.That(connection.Recovering(JevLane.Director), Is.False);
            connection.Observe(JevLane.CrowdUrgent, 3, JevFailure.PartialResponse, 3);
            connection.Observe(JevLane.CrowdUrgent, 4, JevFailure.None, 5);
            connection.Observe(JevLane.CrowdUrgent, 5, JevFailure.None, 6);
            Assert.That(connection.Recovering(JevLane.CrowdUrgent), Is.True);
            connection.Observe(JevLane.CrowdUrgent, 6, JevFailure.None, 7);
            Assert.That(connection.Recovering(JevLane.CrowdUrgent), Is.False);
            Assert.That(connection.Describe(JevLane.CrowdUrgent, 7), Does.StartWith("정상"));
        }

        [Test]
        public void InitialAndSlowConnectionsStaySerialUntilThreeFastCompleteAnswers()
        {
            var connection = new JevConnection();
            Assert.That(connection.Serial(JevLane.CrowdUrgent), Is.True);
            for (int i = 1; i <= 3; i++)
                connection.Observe(JevLane.CrowdUrgent, i, JevFailure.None, i, seconds: 1);
            Assert.That(connection.Serial(JevLane.CrowdUrgent), Is.False);
            Assert.That(connection.Serial(JevLane.Director), Is.True);
            connection.Observe(JevLane.CrowdUrgent, 4, JevFailure.None, 10, seconds: 8);
            Assert.That(connection.Serial(JevLane.CrowdUrgent), Is.True, "A valid slow answer must reduce the next burst without claiming failure.");
            Assert.That(connection.Failures(JevLane.CrowdUrgent), Is.Zero);
            connection.Observe(JevLane.CrowdUrgent, 3, JevFailure.None, 11, seconds: 1);
            Assert.That(connection.Serial(JevLane.CrowdUrgent), Is.True, "An older fast result cannot undo current congestion.");
            connection.Observe(JevLane.CrowdUrgent, 5, JevFailure.None, 12, seconds: 1);
            connection.Observe(JevLane.CrowdUrgent, 6, JevFailure.None, 13, seconds: 1);
            connection.Observe(JevLane.CrowdUrgent, 7, JevFailure.Timeout, 14);
            Assert.That(connection.Serial(JevLane.CrowdUrgent), Is.True);
            for (int i = 8; i <= 10; i++)
                connection.Observe(JevLane.CrowdUrgent, i, JevFailure.None, i + 10, seconds: 1);
            Assert.That(connection.Serial(JevLane.CrowdUrgent), Is.False);
        }

        [TestCase(0, 3)]
        [TestCase(1, 3)]
        [TestCase(2, 2)]
        public void SelectedCrowdQuestionIsIncludedOnceEvenWhenAnotherLaneComesFirst(int selectedIndex, int capacity)
        {
            var itemType = typeof(CrowdMind).GetNestedType("Judgement", BindingFlags.NonPublic);
            var listType = typeof(List<>).MakeGenericType(itemType);
            var candidates = (IList)Activator.CreateInstance(listType);
            object selected = null;
            for (int i = 0; i < 5; i++)
            {
                var item = Activator.CreateInstance(itemType);
                itemType.GetField("Urgent").SetValue(item, i < selectedIndex);
                candidates.Add(item);
                if (i == selectedIndex) selected = item;
            }
            var batch = (IList)typeof(CrowdMind).GetMethod("BuildBatch", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { candidates, selected, capacity, new Func<Passenger, string>(_ => "same observation") });
            Assert.That(batch.Count, Is.EqualTo(capacity));
            Assert.That(batch[0], Is.SameAs(selected));
            var unique = new HashSet<object>();
            foreach (var item in batch)
            {
                Assert.That(unique.Add(item), Is.True, "The same judgement must never become two questions in one request.");
                Assert.That(itemType.GetField("Urgent").GetValue(item), Is.False);
            }
        }

        [Test]
        public void DuplicateQuestionIdsAreRejectedBeforeCreatingAnAmbiguousJsonRequest()
        {
            Assert.Throws<ArgumentException>(() => JevClient.Body(null, new[] { Questions[0], Questions[0] }));
            Assert.Throws<ArgumentException>(() => JevClient.Body(null, new[] { Questions[0], new JevChoice { Id = "choice" } }));
            var body = Newtonsoft.Json.Linq.JObject.Parse(JevClient.Body(null, Questions));
            Assert.That(((Newtonsoft.Json.Linq.JObject)body["questions"]).Count, Is.EqualTo(Questions.Length));
        }

        [Test]
        public void LateOlderSuccessDoesNotClearNewerFailure()
        {
            var connection = new JevConnection();
            connection.Observe(JevLane.Director, 2, JevFailure.Timeout, 10);
            connection.Observe(JevLane.Director, 1, JevFailure.None, 11);
            Assert.That(connection.Failures(JevLane.Director), Is.EqualTo(1));
            Assert.That(connection.CanSend(JevLane.Director, 11), Is.False);
        }

        [TestCase(0, 15)]
        [TestCase(90, 90)]
        [TestCase(999, 120)]
        [TestCase(float.NaN, 15)]
        public void QuotaDelayAppliesToAllLanesAndSurvivesAnInFlightSuccess(float retryAfter, float expected)
        {
            var connection = new JevConnection();
            connection.Observe(JevLane.CrowdRoutine, 1, JevFailure.RateLimit, 10, retryAfter);
            connection.Observe(JevLane.Director, 2, JevFailure.None, 11);
            foreach (JevLane lane in Enum.GetValues(typeof(JevLane)))
            {
                Assert.That(connection.CanSend(lane, 11), Is.False);
                Assert.That(connection.RetryIn(lane, 10), Is.EqualTo(expected));
                Assert.That(connection.CanSend(lane, 10 + expected), Is.True);
            }
            Assert.That(connection.Summary(11, ""), Does.Not.StartWith("JEV 사건 판단 정상"));
        }

        [Test]
        public void OlderQuotaAndAuthenticationResponsesStillApplyGlobally()
        {
            var connection = new JevConnection();
            connection.Observe(JevLane.Director, 3, JevFailure.None, 10);
            connection.Observe(JevLane.Director, 1, JevFailure.RateLimit, 11, 60);
            Assert.That(connection.CanSend(JevLane.CrowdUrgent, 12), Is.False);
            connection.Observe(JevLane.Director, 2, JevFailure.Authentication, 12);
            connection.Observe(JevLane.CrowdUrgent, 4, JevFailure.None, 100);
            Assert.That(connection.Rejected, Is.True);
            Assert.That(connection.CanSend(JevLane.Director, 100), Is.False);
            Assert.That(connection.Describe(JevLane.CrowdUrgent, 100), Does.Contain("키 거부"));
        }

        [Test]
        public void BudgetWaitIsNotReportedAsANetworkFailure()
        {
            var budget = new JevBudget();
            var connection = new JevConnection();
            var first = budget.Begin(JevLane.Director, 0, 100);
            var second = budget.Begin(JevLane.Director, 0, 100);
            Assert.That(budget.BlockReason(JevLane.Director, 0), Is.EqualTo(JevSendBlock.InFlight));
            budget.End(first, 100, 0, false);
            budget.End(second, 100, 0, false);
            Assert.That(budget.BlockReason(JevLane.Director, 0, JevBudget.MaxTokensPerMinute), Is.EqualTo(JevSendBlock.Budget));
            connection.Observe(JevLane.Director, 1, JevFailure.Timeout, 0);
            connection.Blocked(JevLane.Director, JevSendBlock.Budget);
            Assert.That(connection.Describe(JevLane.Director, 3), Is.EqualTo("게임 내부 예산 대기"));
        }

        private static readonly JevChoice[] Questions = MakeQuestions();
        private static JevChoice[] MakeQuestions()
        {
            var choice = new JevChoice { Id = "choice", Instructions = "Choose a valid candidate." };
            choice.Criteria["a"] = "A";
            choice.Criteria["b"] = "B";
            return new[] { choice, new JevChoice { Id = "score", Levels = new List<string> { "low", "high" } } };
        }
        private static object Read(string json) => typeof(JevClient).GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { Encoding.UTF8.GetBytes(json), Questions });
        private static object Field(object reply, string name) => reply?.GetType().GetField(name).GetValue(reply);
        private static Dictionary<string, JevAnswer> Answers(object reply) => (Dictionary<string, JevAnswer>)Field(reply, "Answers");

        [TestCase("{}")]
        [TestCase("{\"answers\":{}}")]
        [TestCase("{\"answers\":{\"choice\":{\"choice\":\"outside\"},\"score\":{\"score\":5}}}")]
        [TestCase("{\"answers\":{\"score\":{\"score\":\"NaN\"}}}")]
        [TestCase("{\"answers\":{\"score\":{\"score\":1e400}}}")]
        public void EmptyOrInvalidAnswersNeverCountAsSuccess(string json)
        {
            Assert.That(Answers(Read(json)), Is.Null);
        }

        [Test]
        public void MalformedAnswerDoesNotDiscardIndependentValidAnswerOrUsage()
        {
            var reply = Read("{\"model\":\"jev-test\",\"usage\":{\"input_tokens\":321,\"output_tokens\":12},\"answers\":{\"choice\":{\"choice\":\"a\"},\"score\":{\"score\":{}}}}");
            Assert.That(Answers(reply).Count, Is.EqualTo(1));
            Assert.That(Answers(reply)["choice"].Choice, Is.EqualTo("a"));
            Assert.That(Field(reply, "InputTokens"), Is.EqualTo(321L));
            Assert.That(JevConnection.Classify(200, true, false, Answers(reply).Count, 2), Is.EqualTo(JevFailure.PartialResponse));
        }

        [Test]
        public void InvalidOptionalMetadataCannotCorruptAValidChoice()
        {
            var reply = Read("{\"usage\":{\"input_tokens\":{}},\"answers\":{\"choice\":{\"choice\":\"a\",\"confidence\":{},\"probabilities\":{\"a\":\"NaN\",\"b\":0.4,\"outside\":1}}}}");
            var answer = Answers(reply)["choice"];
            Assert.That(answer.Choice, Is.EqualTo("a"));
            Assert.That(answer.Confidence, Is.Zero);
            Assert.That(answer.Probabilities.Count, Is.EqualTo(1));
            Assert.That(answer.Probabilities["b"], Is.EqualTo(.4f));
            Assert.That(Field(reply, "InputTokens"), Is.Null);
        }

        [Test]
        public void EmptyAnswersStillKeepReportedUsageForAccounting()
        {
            var reply = Read("{\"usage\":{\"input_tokens\":700},\"answers\":{}}");
            Assert.That(Answers(reply), Is.Null);
            Assert.That(Field(reply, "InputTokens"), Is.EqualTo(700L));
        }

        [Test]
        public void ValidScoreAndChoiceRemainUsable()
        {
            var answers = Answers(Read("{\"answers\":{\"choice\":{\"choice\":\"b\",\"confidence\":0.9},\"score\":{\"score\":0.6,\"probabilities\":{\"0\":0.4,\"1\":0.6}}}}"));
            Assert.That(answers.Count, Is.EqualTo(2));
            Assert.That(answers["choice"].Choice, Is.EqualTo("b"));
            Assert.That(answers["score"].Score, Is.EqualTo(.6f));
            Assert.That(answers["score"].LevelProbabilities(2), Is.EqualTo(new[] { .4f, .6f }));
        }

        [Test]
        public void BrokenJsonReturnsNoInventedAnswer() => Assert.That(Read("<html>server error</html>"), Is.Null);

        [TestCase("90", 90)]
        [TestCase("999", 120)]
        [TestCase("-2", 0)]
        [TestCase("NaN", 0)]
        [TestCase("Infinity", 0)]
        [TestCase("Thu, 01 Jan 2026 00:01:00 GMT", 60)]
        [TestCase("bad", 0)]
        public void RetryAfterUnderstandsSecondsAndDatesWithABound(string header, float expected)
        {
            var method = typeof(JevClient).GetMethod("RetryAfter", BindingFlags.NonPublic | BindingFlags.Static);
            var delay = method.Invoke(null, new object[] { header, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) });
            Assert.That(delay, Is.EqualTo(expected));
        }

        [Test]
        public void ClientBlockedPollsReturnOnceWithoutSendingChargingOrFailing()
        {
            string previous = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
            try
            {
                Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", "issue280-offline-test-key");
                var client = new JevClient(null); // No Warm(): this test must never use the network.
                var connection = (JevConnection)typeof(JevClient).GetField("connection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
                var budget = (JevBudget)typeof(JevClient).GetField("budget", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
                connection.Observe(JevLane.Director, 1, JevFailure.Timeout, Time.realtimeSinceStartup);
                for (int i = 0; i < 100; i++)
                {
                    int callbacks = 0;
                    var ask = client.Ask("offline", null, Questions, answer => { callbacks++; Assert.That(answer, Is.Null); }, JevLane.Director);
                    Assert.That(ask.MoveNext(), Is.False, "A blocked request must not reach the network.");
                    Assert.That(callbacks, Is.EqualTo(1));
                }
                Assert.That(client.Usage.Requests, Is.Zero);
                Assert.That(client.Usage.Failures, Is.Zero);
                Assert.That(client.PendingRequests, Is.Zero);
                Assert.That(client.FailuresInARowOf(JevLane.Director), Is.EqualTo(1));
                for (int i = 0; i < 3; i++)
                    connection.Observe(JevLane.CrowdUrgent, i + 1, JevFailure.None, Time.realtimeSinceStartup, seconds: 1);
                var first = budget.Begin(JevLane.CrowdUrgent, Time.realtimeSinceStartup, 100);
                for (int i = 0; i < 3; i++) budget.Begin(JevLane.CrowdUrgent, Time.realtimeSinceStartup, 100);
                Assert.That(client.CanSend(JevLane.CrowdUrgent), Is.False);
                Assert.That(client.StatusOf(JevLane.CrowdUrgent), Is.EqualTo("답변 처리 중"));
                budget.End(first, 100, 0, false);
                Assert.That(client.CanSend(JevLane.CrowdUrgent), Is.True);
                connection.Observe(JevLane.CrowdUrgent, 4, JevFailure.Timeout, Time.realtimeSinceStartup - 3);
                Assert.That(client.CanSend(JevLane.CrowdUrgent), Is.False, "Recovery waits for the already admitted requests.");
                for (int i = 0; i < 3; i++)
                    connection.Observe(JevLane.CrowdUrgent, 5 + i, JevFailure.None, Time.realtimeSinceStartup);
                Assert.That(client.CanSend(JevLane.CrowdUrgent), Is.True, "Three complete answers restore the existing lane cap.");
            }
            finally { Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", previous); }
        }

        [Test]
        public void PreparationOccupiesTheInitialProbeAndCancellationReleasesItWithoutSending()
        {
            string previous = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
            try
            {
                Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", "issue280-offline-test-key");
                var client = new JevClient(null);
                var state = new HeldPreparation();
                var ask = client.Ask("offline", state, Questions, _ => Assert.Fail("The cancelled preparation cannot reach a callback."), JevLane.CrowdRoutine);
                try
                {
                    Assert.That(ask.MoveNext(), Is.True);
                    Assert.That(client.CanSend(JevLane.CrowdRoutine), Is.False, "Do not prepare several simultaneous probes in one frame.");
                    Assert.That(client.CanSend(JevLane.Director), Is.True);
                }
                finally { ((IDisposable)ask).Dispose(); state.Release.Set(); }
                Assert.That(client.CanSend(JevLane.CrowdRoutine), Is.True);
                Assert.That(client.PendingRequests, Is.Zero);
                Assert.That(client.Usage.Requests, Is.Zero);
                Assert.That(client.Usage.Failures, Is.Zero);
            }
            finally { Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", previous); }
        }

        [Test]
        public void FastConnectionsReservePreparationsWithinTheExistingLaneCap()
        {
            string previous = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
            var pending = new List<IEnumerator>();
            var state = new HeldPreparation();
            try
            {
                Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", "issue280-offline-test-key");
                var client = new JevClient(null);
                var connection = (JevConnection)typeof(JevClient).GetField("connection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(client);
                for (int i = 1; i <= 3; i++) connection.Observe(JevLane.CrowdUrgent, i, JevFailure.None, Time.realtimeSinceStartup, seconds: 1);
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(client.CanSend(JevLane.CrowdUrgent), Is.True);
                    var ask = client.Ask("offline", state, Questions, _ => Assert.Fail("Cancelled preparation cannot reply."), JevLane.CrowdUrgent);
                    pending.Add(ask);
                    Assert.That(ask.MoveNext(), Is.True);
                }
                Assert.That(client.CanSend(JevLane.CrowdUrgent), Is.False, "Preparation is already an occupied slot even on a fast connection.");
                Assert.That(client.CanSend(JevLane.Director), Is.True, "Other lanes keep their reserved slots.");
                Assert.That(client.PendingRequests, Is.EqualTo(4));
                Assert.That(client.Usage.Requests, Is.Zero);
                foreach (var ask in pending) ((IDisposable)ask).Dispose();
                pending.Clear();
                Assert.That(client.PendingRequests, Is.Zero);
                Assert.That(client.CanSend(JevLane.CrowdUrgent), Is.True);
            }
            finally
            {
                foreach (var ask in pending) ((IDisposable)ask).Dispose();
                state.Release.Set();
                Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", previous);
            }
        }

        private sealed class HeldPreparation
        {
            [Newtonsoft.Json.JsonIgnore]
            public readonly System.Threading.ManualResetEventSlim Release = new System.Threading.ManualResetEventSlim();
            public string Value { get { Release.Wait(); return "offline"; } }
        }

        private sealed class UnserializableState
        {
            public string Broken => throw new InvalidOperationException("Offline preparation failure");
        }

        [UnityTest]
        public IEnumerator PreparationFailureReleasesPendingRequestAndDoesNotChargeTheBudget()
        {
            string previous = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
            try
            {
                Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", "issue280-offline-test-key");
                var client = new JevClient(null);
                int callbacks = 0;
                yield return client.Ask("offline", new UnserializableState(), Questions,
                    answer => { callbacks++; Assert.That(answer, Is.Null); }, JevLane.Director);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(client.PendingRequests, Is.Zero);
                Assert.That(client.Usage.Requests, Is.Zero);
                Assert.That(client.Usage.Failures, Is.Zero);
                Assert.That(client.StatusOf(JevLane.Director), Does.StartWith("요청 준비 실패"));
                Assert.That(client.CanSend(JevLane.Director), Is.False);
            }
            finally { Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", previous); }
        }
    }
}
