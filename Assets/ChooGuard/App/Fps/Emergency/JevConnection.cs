using System;

namespace ChooGuard.App.Fps.Emergency
{
    public enum JevFailure { None, Timeout, Network, Server, RateLimit, Authentication, InvalidResponse, PartialResponse, RequestRejected, Preparation }
    public enum JevSendBlock { None, InFlight, Budget, RetryDelay }

    /// <summary>Per-lane recovery; blocked polls do not count as failures. Time is real time, independent of game speed.</summary>
    public sealed class JevConnection
    {
        private sealed class Lane
        {
            public long CompletedSequence;
            public int Failures;
            public bool Recovering;
            public int RecoveryAnswers;
            public int FastAnswers;
            public JevFailure Failure;
            public JevSendBlock Block;
            public float LastAnswer = -1, RetryAt;
        }

        private readonly Lane[] lanes = { new Lane(), new Lane(), new Lane() };
        private float rateLimitUntil;
        public bool Rejected { get; private set; }

        public int Failures(JevLane lane) => lanes[(int)lane].Failures;
        public bool HasAnswer(JevLane lane) => lanes[(int)lane].LastAnswer >= 0;
        public JevFailure Failure(JevLane lane) => lanes[(int)lane].Failure;
        /// <summary>After a failure, probe with one request at a time until three complete answers confirm recovery.</summary>
        public bool Recovering(JevLane lane) => lanes[(int)lane].Recovering;
        /// <summary>Start with one request per lane. Slow or failing connections stay serial; three fast complete answers restore the budget's usual cap.</summary>
        public bool Serial(JevLane lane) => lanes[(int)lane].Recovering || lanes[(int)lane].FastAnswers < 3;
        public float RetryIn(JevLane lane, float now) => Math.Max(0, Math.Max(lanes[(int)lane].RetryAt, rateLimitUntil) - now);
        public bool CanSend(JevLane lane, float now) => !Rejected && RetryIn(lane, now) <= 0;
        public void Blocked(JevLane lane, JevSendBlock block) => lanes[(int)lane].Block = block;

        /// <summary>A late older request cannot replace a newer result in the same lane. A key refusal always stops the shift.</summary>
        public void Observe(JevLane lane, long sequence, JevFailure failure, float now, float retryAfter = 0, float seconds = 0, float fastSeconds = 4)
        {
            if (failure == JevFailure.Authentication) Rejected = true;
            // A quota response applies to every lane, even when an older request finishes last.
            if (failure == JevFailure.RateLimit)
            {
                if (float.IsNaN(retryAfter) || float.IsInfinity(retryAfter)) retryAfter = 0;
                rateLimitUntil = Math.Max(rateLimitUntil, now + Math.Max(15, Math.Min(120, retryAfter)));
            }
            var own = lanes[(int)lane];
            if (sequence < own.CompletedSequence) return;
            own.CompletedSequence = sequence;
            own.Block = JevSendBlock.None;
            own.Failure = failure;
            if (failure == JevFailure.None)
            {
                own.Failures = 0;
                own.LastAnswer = now;
                own.RetryAt = 0;
                own.FastAnswers = !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0 && seconds <= fastSeconds
                    ? Math.Min(3, own.FastAnswers + 1) : 0;
                if (own.Recovering && ++own.RecoveryAnswers >= 3) own.Recovering = false;
                return;
            }
            own.Failures++;
            own.FastAnswers = 0;
            own.Recovering = true;
            own.RecoveryAnswers = 0;
            float delay = Math.Min(30, (float)Math.Pow(2, Math.Min(own.Failures, 5)));
            if (failure == JevFailure.RateLimit)
            {
                delay = Math.Max(15, Math.Min(120, retryAfter));
            }
            else if (failure == JevFailure.RequestRejected) delay = 30;
            own.RetryAt = now + delay;
        }

        public static JevFailure Classify(long http, bool transportSuccess, bool timedOut, int validAnswers, int questions)
        {
            if (http == 401 || http == 403) return JevFailure.Authentication;
            if (http == 429) return JevFailure.RateLimit;
            if (timedOut || http == 408 || http == 504) return JevFailure.Timeout;
            if (http >= 500) return JevFailure.Server;
            if (http >= 400) return JevFailure.RequestRejected;
            if (!transportSuccess) return http == 0 ? JevFailure.Network : JevFailure.InvalidResponse;
            if (validAnswers == 0) return JevFailure.InvalidResponse;
            return validAnswers < questions ? JevFailure.PartialResponse : JevFailure.None;
        }

        public static string Reason(JevFailure failure)
        {
            switch (failure)
            {
                case JevFailure.Timeout: return "응답 시간 초과";
                case JevFailure.Network: return "네트워크 연결 실패";
                case JevFailure.Server: return "서버 오류";
                case JevFailure.RateLimit: return "서버 요청 한도";
                case JevFailure.Authentication: return "키 거부 · 타이틀에서 다시 입력";
                case JevFailure.InvalidResponse: return "사용할 수 없는 응답";
                case JevFailure.PartialResponse: return "일부 답변 누락";
                case JevFailure.RequestRejected: return "요청 거부 · 서비스 설정 확인";
                case JevFailure.Preparation: return "요청 준비 실패";
                default: return "정상";
            }
        }

        public string Describe(JevLane lane, float now)
        {
            var own = lanes[(int)lane];
            if (Rejected) return Reason(JevFailure.Authentication);
            if (rateLimitUntil > now) return "서버 요청 한도 · " + (int)Math.Ceiling(rateLimitUntil - now) + "초 뒤 재시도";
            if (RetryIn(lane, now) <= 0)
            {
                if (own.Block == JevSendBlock.Budget) return "게임 내부 예산 대기";
                if (own.Block == JevSendBlock.InFlight) return "답변 처리 중";
            }
            if (own.Failure != JevFailure.None)
            {
                float retry = RetryIn(lane, now);
                return Reason(own.Failure) + (retry > 0 ? " · " + (int)Math.Ceiling(retry) + "초 뒤 재시도" : " · 재연결 시도");
            }
            return own.LastAnswer >= 0 ? (own.Recovering ? "연결 회복 확인 중" : "정상") +
                " · 최근 응답 " + (int)Math.Max(0, now - own.LastAnswer) + "초 전" : "첫 답변 대기";
        }

        public string Summary(float now, string model)
        {
            string heading = !Rejected && rateLimitUntil <= now && HasAnswer(JevLane.Director) && Failure(JevLane.Director) == JevFailure.None
                && lanes[(int)JevLane.Director].Block != JevSendBlock.Budget
                ? "JEV 사건 판단 정상" : "JEV 판단 상태";
            if (!string.IsNullOrEmpty(model)) heading += " · " + model;
            return heading + "\n사건: " + Describe(JevLane.Director, now) +
                "\n시민 대응: " + Describe(JevLane.CrowdUrgent, now) +
                "\n시민 일상: " + Describe(JevLane.CrowdRoutine, now);
        }
    }
}
