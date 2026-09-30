#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using ChooGuard.App.Fps.Emergency;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChooGuard.Tests.PlayMode
{
    /// <summary>
    /// 근무를 N 회 돌려 JEV 판단 기록을 모은다. 학습 데이터 수집용 하네스다 — 판정하지 않는다.
    /// </summary>
    /// <remarks>
    /// 설정은 전부 환경변수로 받는다. 값을 코드에 박으면 다른 PC 에서 돌릴 때마다 고쳐야 한다.
    ///
    ///   CG_SHIFT_COUNT     회차 수 (기본 1)
    ///   CG_SHIFT_SECONDS   회차당 게임 시간 초 (기본 600)
    ///   CG_SHIFT_SCALE     시간 압축 배수 (기본 1 — 아래 경고를 읽을 것)
    ///   CG_SHIFT_REALCAP   회차당 실시간 상한 초 (기본 1800)
    ///   CG_SHIFT_SEED      첫 회차 시드 (기본 0 = 매번 무작위). 지정하면 회차마다 +1
    ///   CG_SHIFT_OUT       요약 JSON 을 쓸 폴더 (기본: 쓰지 않음)
    ///
    /// **시간 압축 경고.** 2026-09-30 에 8배속으로 재 보니 요청 156건 중 155건이 군중 판단이었고
    /// 사건 합성은 25라운드 중 1회만 JEV 가 정했다(나머지는 로컬 규칙). JevClient 의 분당 90건
    /// 상한은 **실시간** 기준인데 게임을 8배로 돌리면 같은 실시간에 8배의 질의가 몰려 상한에 걸린다.
    /// **측정 방법이 측정 대상을 왜곡한다.** 비율을 보려면 CG_SHIFT_SCALE=1 로 둔다.
    /// 압축은 '총량이 얼마나 쌓이는가' 만 볼 때 쓴다.
    ///
    /// **예산은 군중과 나눠 쓴다.** CrowdDirector 도 같은 JevClient 를 쓴다. 사건 합성 표본만
    /// 필요하면 이 사실을 감안해 회차를 길게 잡거나, 군중 질의를 줄이는 쪽을 따로 다뤄야 한다.
    ///
    /// 실제 데이터의 정본은 이 시험의 요약이 아니라 JevClient 가 남기는 JSONL 이다:
    ///   &lt;persistentDataPath&gt;/jev-runs/jev-&lt;UTC&gt;.jsonl
    /// 한 줄에 at·purpose·http·seconds·model·request(state·questions)·answers 가 모두 들어 있다.
    ///
    /// [Explicit] 로 둔다. 일반 회귀에 섞이면 매 실행마다 JEV 를 호출해 과금된다.
    /// </remarks>
    public sealed class ShiftSampleTests
    {
        private static int Number(string name, int fallback)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        [UnityTest, Explicit("JEV 를 실제로 호출하고 과금된다. -testFilter 로 직접 지정해 돌릴 것.")]
        public IEnumerator 근무를_돌려_JEV_판단을_모은다()
        {
            int count = Mathf.Max(1, Number("CG_SHIFT_COUNT", 1));
            float gameSeconds = Mathf.Max(30, Number("CG_SHIFT_SECONDS", 600));
            float scale = Mathf.Clamp(Number("CG_SHIFT_SCALE", 1), 1, 20);
            float realCap = Mathf.Max(60, Number("CG_SHIFT_REALCAP", 1800));
            int seed = Number("CG_SHIFT_SEED", 0);
            var outFolder = Environment.GetEnvironmentVariable("CG_SHIFT_OUT");

            Debug.Log("CG_HARNESS count=" + count + " seconds=" + gameSeconds + " scale=" + scale
                      + " realCap=" + realCap + " seed=" + seed
                      + " out=" + (string.IsNullOrEmpty(outFolder) ? "(없음)" : outFolder));
            if (scale > 1)
                Debug.LogWarning("CG_HARNESS 시간을 " + scale + "배로 압축한다. 분당 요청 상한이 실시간 기준이라 "
                                 + "JEV 가 답하는 비율이 실제보다 낮게 나온다. 비율을 보려면 CG_SHIFT_SCALE=1 로 둘 것.");

            var summary = new StringBuilder("[\n");
            for (int shift = 0; shift < count; shift++)
            {
                if (seed != 0) EmergencySession.NextSeed = seed + shift;
                // 두 씬을 순서대로 올린다. StationEmergency 를 단독으로 열면 세션이 FpsStation 의
                // 역무원을 찾지 못한다. 평소에는 SceneFlow.EnsureSessionForDirectStationPlay 가
                // 이 Additive 로드를 대신하지만 [RuntimeInitializeOnLoadMethod] 라 PlayMode 시작 때
                // 한 번만 돌고, 시험 러너가 이미 시작한 뒤라 다시 불리지 않는다.
                yield return SceneManager.LoadSceneAsync("FpsStation", LoadSceneMode.Single);
                yield return null;
                yield return SceneManager.LoadSceneAsync("StationEmergency", LoadSceneMode.Additive);
                yield return null;
                // 측정이지 판정이 아니다. 근무 중 오류 로그로 시험을 실패시키면 무엇이 쌓였는지조차 못 본다.
                LogAssert.ignoreFailingMessages = true;

                var session = UnityEngine.Object.FindFirstObjectByType<EmergencySession>();
                Assert.IsNotNull(session, "EmergencySession 을 찾지 못했습니다 (회차 " + shift + ").");
                float bootDeadline = Time.realtimeSinceStartup + 60f;
                while (session.Incidents == null && Time.realtimeSinceStartup < bootDeadline) yield return null;
                Assert.IsNotNull(session.Incidents, "근무가 부팅되지 않았습니다 (회차 " + shift + ").");

                var jev = session.Jev;
                var log = session.Log;
                float startedReal = Time.realtimeSinceStartup;
                float startedShift = session.ShiftSeconds;
                Time.timeScale = scale;
                try
                {
                    while (session.ShiftSeconds - startedShift < gameSeconds
                           && Time.realtimeSinceStartup - startedReal < realCap)
                        yield return null;
                }
                finally { Time.timeScale = 1f; }

                float realSpent = Time.realtimeSinceStartup - startedReal;
                float gameSpent = session.ShiftSeconds - startedShift;
                string line = "{\"shift\":" + shift
                    + ",\"seed\":" + (seed != 0 ? seed + shift : 0)
                    + ",\"gameSeconds\":" + gameSpent.ToString("0", CultureInfo.InvariantCulture)
                    + ",\"realSeconds\":" + realSpent.ToString("0", CultureInfo.InvariantCulture)
                    + ",\"rounds\":" + session.Incidents.Rounds
                    + ",\"jevRounds\":" + session.Incidents.JevRounds
                    + ",\"byJev\":" + (log == null ? -1 : log.JevCompositions)
                    + ",\"byLocal\":" + (log == null ? -1 : log.LocalCompositions)
                    + ",\"requests\":" + (jev == null ? -1 : jev.Requests)
                    + ",\"failures\":" + (jev == null ? -1 : jev.Failures)
                    + ",\"inputTokens\":" + (jev == null ? -1 : jev.InputTokens)
                    + ",\"outputTokens\":" + (jev == null ? -1 : jev.OutputTokens)
                    + ",\"model\":\"" + (jev == null ? "" : jev.LastModel) + "\"}";
                Debug.Log("CG_SHIFT " + line);
                summary.Append("  ").Append(line).Append(shift < count - 1 ? ",\n" : "\n");
                Assert.Greater(session.Incidents.Rounds, 0, "라운드가 한 번도 돌지 않았습니다 (회차 " + shift + ").");
            }
            summary.Append("]\n");

            Debug.Log("CG_HARNESS_LOGS " + Path.Combine(UnityEngine.Application.persistentDataPath, "jev-runs"));
            if (string.IsNullOrEmpty(outFolder)) yield break;
            try
            {
                Directory.CreateDirectory(outFolder);
                var path = Path.Combine(outFolder, "shifts-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
                File.WriteAllText(path, summary.ToString(), new UTF8Encoding(false));
                Debug.Log("CG_HARNESS_OUT " + path);
            }
            catch (Exception error) { Debug.LogError("CG_HARNESS_OUT 실패 · " + error.Message); }
        }
    }
}
#endif
