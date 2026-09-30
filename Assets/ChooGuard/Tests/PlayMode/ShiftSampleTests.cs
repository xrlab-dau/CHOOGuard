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
        /// <summary>수집 중에는 소리를 끈다. 제품 코드는 고치지 않고 런타임에만 끈다.</summary>
        /// <remarks>
        /// 소리는 JEV 판단에 들어가지 않는다 — PublicState 에 음향이 없다. 데이터에 기여하지 않으면서
        /// 배치모드에서 CPU 와 오디오 장치를 쓰고, 근무를 여러 번 돌릴 때 그만큼 느려진다.
        ///
        /// 끄는 것은 이 시험이 돌 때뿐이다. 실제 플레이에는 영향이 없다.
        /// </remarks>
        private static void Silence(EmergencySession session)
        {
            int off = 0;
            if (session != null && session.Sound != null) { session.Sound.enabled = false; off++; }
            foreach (var soundscape in UnityEngine.Object.FindObjectsByType<StationSoundscape>(FindObjectsSortMode.None))
                if (soundscape != null) { soundscape.enabled = false; off++; }
            // 이미 울리고 있는 것까지 멈춘다. 컴포넌트만 끄면 재생 중인 소스는 계속 난다.
            foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source != null && source.isPlaying) { source.Stop(); off++; }
            AudioListener.volume = 0f;
            AudioListener.pause = true;
            Debug.Log("CG_SILENCE 끈 것 " + off + "개 · AudioListener 음소거·정지");
        }

        /// <summary>지금 살아 있는 위험 중 Label 에 <paramref name="what"/> 가 든 것이 있는가.</summary>
        /// <remarks>
        /// 종류를 타입으로 묻지 않고 Label 로 본다. 하드룰 1 이 '종류를 닫힌 목록으로 가정하지 말라' 고
        /// 하므로, 시험도 <c>is FireHazard</c> 대신 공통 정보(Label)로 판단한다.
        /// </remarks>
        private static bool Present(string what)
        {
            if (string.IsNullOrEmpty(what)) return true;
            foreach (var hazard in HazardRegistry.Active)
                if (hazard != null && hazard.Label != null && hazard.Label.Contains(what)) return true;
            return false;
        }

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
            // 특정 사건이 난 회차만 모으고 싶을 때. Hazard.Label 에 이 문자열이 들어가면 채택한다.
            // 예: CG_SHIFT_REQUIRE=화재
            //
            // 사건을 만들지 않고 **고른다.** 세계가 여전히 합성하므로 하드룰을 건드리지 않고,
            // 불 확대·연기 확산·경보 같은 전개 후보도 정상적으로 생긴다. 밖에서 FireHazard 를
            // 직접 만들면 IncidentDirector 의 fires 목록에 들어가지 않아 전개가 아예 없다.
            //
            // 대가: 표본이 **선택 편향**을 갖는다. 화재가 난 근무만 모으면 화재가 안 난 근무의
            // 분포를 잃는다. 분포를 보려면 이 값을 비우고 돌린다.
            var require = Environment.GetEnvironmentVariable("CG_SHIFT_REQUIRE");
            float seek = Mathf.Max(30, Number("CG_SHIFT_SEEK", 300));       // 그 사건을 기다리는 게임 시간
            int maxAttempts = Mathf.Max(1, Number("CG_SHIFT_ATTEMPTS", 8)); // 회차당 재시도 상한

            Debug.Log("CG_HARNESS count=" + count + " seconds=" + gameSeconds + " scale=" + scale
                      + " realCap=" + realCap + " seed=" + seed
                      + " out=" + (string.IsNullOrEmpty(outFolder) ? "(없음)" : outFolder));
            if (scale > 1)
                Debug.LogWarning("CG_HARNESS 시간을 " + scale + "배로 압축한다. 분당 요청 상한이 실시간 기준이라 "
                                 + "JEV 가 답하는 비율이 실제보다 낮게 나온다. 비율을 보려면 CG_SHIFT_SCALE=1 로 둘 것.");

            var summary = new StringBuilder("[\n");
            int attemptsSpent = 0;
            for (int shift = 0; shift < count; shift++)
            {
                int attempt = 0;
                bool accepted = string.IsNullOrEmpty(require);
            retry:
                // 시드를 회차·시도마다 다르게 준다. 같은 시드로 다시 돌리면 같은 근무가 나온다.
                if (seed != 0) EmergencySession.NextSeed = seed + shift * maxAttempts + attempt;
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

                Silence(session);
                var jev = session.Jev;
                var log = session.Log;
                float startedReal = Time.realtimeSinceStartup;
                float startedShift = session.ShiftSeconds;
                Time.timeScale = scale;
                // ① 요구한 사건이 날 때까지 기다린다. 요구가 없으면 이 구간을 건너뛴다.
                if (!accepted)
                {
                    attemptsSpent++;
                    try
                    {
                        while (session.ShiftSeconds - startedShift < seek
                               && Time.realtimeSinceStartup - startedReal < realCap
                               && !Present(require))
                            yield return null;
                    }
                    finally { Time.timeScale = 1f; }
                    accepted = Present(require);
                    Debug.Log("CG_SEEK shift=" + shift + " attempt=" + attempt
                              + " require=" + require + " found=" + accepted
                              + " game=" + (session.ShiftSeconds - startedShift).ToString("0") + "s");
                    if (!accepted)
                    {
                        attempt++;
                        // 상한에 걸리면 포기하고 그 회차는 요구 없이 그대로 쓴다 — 버리면 아무 표본도 안 남는다.
                        if (attempt < maxAttempts) goto retry;
                        Debug.LogWarning("CG_SEEK shift=" + shift + " 시도 " + maxAttempts
                                         + "회 안에 '" + require + "' 가 나지 않았다. 이 회차는 요구 없이 기록한다.");
                    }
                    Time.timeScale = scale;
                }
                // ② 남은 시간을 채운다.
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
                    + ",\"require\":\"" + (require ?? "") + "\""
                    + ",\"attempts\":" + (attempt + 1)
                    + ",\"accepted\":" + (accepted ? "true" : "false")
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
