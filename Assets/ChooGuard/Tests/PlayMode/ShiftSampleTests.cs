#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.Emergency;
using ChooGuard.App.Fps.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
    ///   CG_SHIFT_SECONDS   회차당 게임 시간 초 (기본 1200 = 20분)
    ///   CG_SHIFT_SCALE     시간 압축 배수 (기본 1 — 아래 경고를 읽을 것)
    ///   CG_SHIFT_REALCAP   회차당(재시도가 있으면 시도당) 실시간 상한 초 (기본 1800). 이 시험의 유일한 실시간 제한이다 -
    ///                      Unity 기본 180초 제한은 [Timeout(int.MaxValue)] 로 풀어 두었다
    ///   CG_SHIFT_SEED      기준 시드 (기본 0 = 매번 무작위). 세션(세계) 시드는 시드 + 회차 * CG_SHIFT_ATTEMPTS + 시도 번호,
    ///                      역무원 정책 시드는 시드 + 회차다. 요약의 seed 는 세션이 실제로 쓴 값(World.Seed)이다
    ///   CG_SHIFT_OUT       요약 JSON 과 JSONL 사본을 쓸 폴더 (기본: 쓰지 않음)
    ///   CG_SHIFT_ZONES     순찰을 허용할 구역 id, 쉼표 구분 (기본: hall2f,main2f,eastexit)
    ///   CG_SHIFT_REQUIRE · CG_SHIFT_SEEK · CG_SHIFT_ATTEMPTS   특정 사건이 난 회차 고르기 (본문 주석 참고)
    ///   CG_SHIFT_FILM · CG_SHIFT_FILM_EVERY_MS   근무를 그림으로 남기기 (기본: 끔)
    ///   CG_SHIFT_TIMELINE  요약에 인게임 시간순 사건 기록을 얹기 (1/true, 기본: 끔)
    ///
    /// **시간 압축 경고.** 2026-09-30 에 8배속으로 재 보니 요청 156건 중 155건이 군중 판단이었고
    /// 사건 합성은 25번 중 1번만 JEV 가 정했다(당시에는 나머지를 로컬 규칙이 대신했다 - 지금은
    /// 그 대체가 금지되어, 같은 상황이면 사건이 아예 나지 않는다). JevBudget 의 분당 요청 수·시간당 비용
    /// 상한은 **실시간** 기준인데 게임을 8배로 돌리면 같은 실시간에 8배의 질의가 몰려 상한에 걸린다.
    /// **측정 방법이 측정 대상을 왜곡한다.** 비율을 보려면 CG_SHIFT_SCALE=1 로 둔다.
    /// 압축은 '총량이 얼마나 쌓이는가' 만 볼 때 쓴다.
    ///
    /// **예산은 군중과 나눠 쓴다.** CrowdDirector 도 같은 JevClient 를 쓴다. 사건 합성 표본만
    /// 필요하면 이 사실을 감안해 회차를 길게 잡거나, 군중 질의를 줄이는 쪽을 따로 다뤄야 한다.
    ///
    /// 실제 데이터의 정본은 이 시험의 요약이 아니라 JevClient 가 남기는 JSONL 이다:
    ///   &lt;persistentDataPath&gt;/jev-runs/jev-&lt;UTC&gt;.jsonl   (세션마다 한 파일)
    /// 한 줄에 at·purpose·lane·http·seconds·model·input_tokens·request·answers 가 들어 있다.
    ///
    /// **jev-runs 폴더 전체를 세지 않는다.** 폴더에는 이전 실행의 기록이 남아 있고 JevClient 는 세션을 열 때마다
    /// 오래된 파일을 지운다. 그래서 세션의 JevClient.LogFile 하나만 읽고, 시도가 끝나는 즉시
    /// CG_SHIFT_OUT 으로 복사한다. **쓸 수 있는 줄은 http==200 이고 answers 가 비어 있지 않은 줄뿐이다** -
    /// 401·429·시간초과도 줄로 남으므로 줄 수(jevLogLines)가 아니라 answeredLines 로 본다.
    ///
    /// [Explicit] 로 둔다. 일반 회귀에 섞이면 매 실행마다 JEV 를 호출해 과금된다.
    /// </remarks>
    public sealed class ShiftSampleTests
    {
        /// <summary>
        /// 역무원 행동 정책. 인지 전에는 역사 안을 순찰하고, 인지한 뒤에는 행동할 때마다 먼저 무전한다.
        /// </summary>
        /// <remarks>
        /// 왜 필요한가: 하네스가 플레이어를 조작하지 않으면 JEV 가 보는 staff_response 가 근무 내내
        /// <c>staff_member_knows_of_an_emergency=false · station_office_informed=false ·
        /// station_announcement_made=false · area_cordoned_off=false</c> 로 고정된다. 대응 뒤의
        /// 전개를 한 건도 배우지 못한다.
        ///
        /// **무전이 곧 상태 변화다.** Hud.Radio.Push 로 글자만 띄우면 JEV 입력은 한 글자도 안 바뀐다
        /// (Focus 의 staff_response 는 인지·역무실 보고·안내방송·통제·화재경보·열차 정차·출동 기관이다).
        /// 그래서 RadioProviders 에 등록된 **실제 선택지**를 실행한다.
        /// IncidentDirector.Radio() 자체는 private 이지만 공개 리스트에 델리게이트로 담겨 있어
        /// 제품 코드를 고칠 필요가 없다.
        ///
        /// **순서는 코드가 이미 강제한다.** 인지 전에는 선택지가 0개이고, 보고 전에는 방송이 열리지
        /// 않는다. 그래서 확률표를 조건부로 짤 필요 없이 '지금 열린 것 중에서 고르기' 면 순서가
        /// 저절로 맞는다. 규칙 1(종류를 닫힌 목록으로 가정하지 않는다)도 지켜진다 — 선택지 이름을
        /// 하드코딩하지 않고 목록을 그대로 받는다.
        ///
        /// **이동은 StepInput 으로만 한다.** transform.position 에 대입하면 CharacterController 가
        /// 벽을 통과하고 중력도 받지 않는다. StepInput 은 제품과 같은 Simulate 를 타므로 벽·NPC 에
        /// 실제로 막히고, 그 안에서 RefreshInteraction 이 돌아 인지 판정도 정상적으로 일어난다.
        /// </remarks>
        private sealed class StaffPolicy
        {
            private const float NoopChance = .2f;      // 아무것도 하지 않을 확률
            private const float DecideSeconds = 6f;    // 행동 판정 주기(게임 시간)
            private const float ArriveRadius = 2.5f;   // 이 거리 안이면 도착으로 본다
            private const float StuckSeconds = 4f;     // 이만큼 제자리면 막힌 것으로 본다
            private const float BackoffSeconds = 1.5f; // 막혔을 때 물러나며 도는 시간
            private const float MinLegMetres = 6f;     // 이보다 가까운 지점은 목표로 삼지 않는다
            private const float MaxLegMetres = 60f;    // 이보다 먼 지점은 한 구간으로 삼지 않는다
            private const float CornerRadius = 1.2f;   // 경로 꺾임점을 지난 것으로 보는 거리
            private const int CandidatesPerPick = 25;  // 한 번 고를 때 경로를 계산해 볼 후보 수
            private const float TurnDegreesPerSecond = 180f;
            private const float FaceToWalkDegrees = 60f; // 이보다 많이 틀어져 있으면 제자리에서 돈다

            private readonly EmergencySession session;
            private readonly FirstPersonResponder player;
            private readonly System.Random random;
            private readonly List<StationPoints.Point> patrol = new List<StationPoints.Point>();
            private const float TraceSeconds = 20f;  // 순찰 상태를 남기는 주기(게임 시간)
            // 도착해서 지운 곳과, 지금 자리에서 길이 안 열린 곳은 다른 것이다.
            // 한 집합에 섞으면 '한 바퀴 돌았다' 와 '여기서는 못 간다' 를 구분할 수 없고,
            // 바퀴를 리셋할 때 못 가던 곳까지 되살아나 같은 실패를 다시 센다.
            private readonly HashSet<int> visited = new HashSet<int>();
            private readonly HashSet<int> unreachable = new HashSet<int>();
            private readonly List<KeyValuePair<float, int>> candidates = new List<KeyValuePair<float, int>>();
            private readonly NavMeshPath navPath = new NavMeshPath();
            private Vector3[] corners = Array.Empty<Vector3>();
            private int corner;                       // 지금 향하는 꺾임점
            private int current = -1;                 // 지금 향하는 patrol 색인
            private float sinceDecision, sinceProgress, sinceTrace;
            private float backoff, backoffTurn;       // 막혔을 때 후진하며 도는 상태
            private Vector3 lastSeen, startedAt;
            private float walked;                     // 실제로 이동한 누적 거리(m)

            public readonly Dictionary<string, int> Radioed = new Dictionary<string, int>();
            public int Noops, Arrivals, Unstucks, Laps, Recoveries;
            // PathPartial 과 PathInvalid 를 따로 센다. 전에 이 둘을 뭉쳐서 '섬 9개' 라고
            // 잘못 보고한 적이 있다 - 부분 경로는 길이 있는데 끝까지 못 가는 것이고,
            // 무효는 길 자체가 없는 것이다. 뭉치면 무엇이 문제인지 알 수 없다.
            public int Partial, Invalid;

            public StaffPolicy(EmergencySession owner, string zoneCsv, int seed)
            {
                session = owner;
                player = owner.Player;
                random = new System.Random(seed);

                // 역사 내부 구역에서만 순찰한다. 하늘광장·역광장·선로는 건물 밖이라 뺀다.
                var allowed = new HashSet<string>();
                foreach (var z in (zoneCsv ?? "").Split(','))
                {
                    var id = z.Trim();
                    if (id.Length > 0) allowed.Add(id);
                }

                var points = session.World != null && session.World.Points != null
                    ? session.World.Points.All : null;
                if (points != null)
                    foreach (var point in points)
                        if (point != null && allowed.Contains(point.Zone)) patrol.Add(point);

                // 예전에는 여기서 목록을 섞었다. Pick 이 매번 거리순으로 다시 정렬하므로
                // 셔플은 정렬 입력 순서만 바꿀 뿐 아무 효과가 없었다. 같은 자리를 맴돌지 않게
                // 막는 것은 셔플이 아니라 visited 다.
                //
                // 이 정책은 자기 난수(random)만 쓰고 StationWorld.Random 을 건드리지 않는다.
                // 하드룰 5(부가 기능이 합성 난수를 소비하지 않는다)를 지키기 위해서다.

                if (player != null)
                {
                    player.SetExternalInputMode(true); // 이 호출은 Pause() 를 부른다
                    player.Resume(false);              // 그래서 반드시 다시 풀어야 StepInput 이 먹는다
                    lastSeen = startedAt = player.transform.position;
                }
                Debug.Log("CG_POLICY 순찰 지점 " + patrol.Count + "개 · 구역 " + string.Join("·", allowed)
                          + (player == null ? " · 역무원 없음(정책 비활성)" : ""));
            }

            /// <summary>매 프레임 부른다. deltaSeconds 는 게임 시간(timeScale 반영)이다.</summary>
            public void Step(float deltaSeconds)
            {
                if (player == null || deltaSeconds <= 0) return;
                // Simulate 는 deltaSeconds > 0.5 면 조용히 false 를 돌려준다. timeScale 을 크게 주면
                // 한 프레임이 그 상한을 넘을 수 있어 여기서 자른다. 그만큼 게임 시간 대비 덜 걷는다.
                float step = Mathf.Min(deltaSeconds, .45f);

                var before = player.transform.position;
                Walk(step);
                walked += Vector3.Distance(before, player.transform.position);
                Trace(deltaSeconds);
                if (session.Incidents == null || !session.Incidents.PlayerKnowsIncident) { sinceDecision = 0; return; }
                sinceDecision += deltaSeconds;
                if (sinceDecision < DecideSeconds) return;
                sinceDecision = 0;
                Decide();
            }

            private void Decide()
            {
                // 지금 열린 무전 선택지를 그대로 모은다. 이름으로 거르지 않는다.
                var open = new List<EmergencySession.RadioOption>();
                foreach (var provider in session.RadioProviders)
                {
                    if (provider == null) continue;
                    var options = provider();
                    if (options == null) continue;
                    foreach (var option in options) open.Add(option);
                }
                if (open.Count == 0) return;                       // 할 수 있는 게 없으면 계속 순찰
                if (random.NextDouble() < NoopChance) { Noops++; return; }

                var chosen = open[random.Next(open.Count)];
                var label = string.IsNullOrEmpty(chosen.Label) ? "(이름 없음)" : chosen.Label;
                Radioed[label] = Radioed.TryGetValue(label, out var had) ? had + 1 : 1;
                Debug.Log("CG_STAFF t=" + session.ShiftSeconds.ToString("0") + "s 무전 · " + label
                          + " (열린 선택지 " + open.Count + "개)");
                if (chosen.Send != null) chosen.Send();
            }

            // 다음 지점으로 걸어간다. 직선 보행자는 소화기·NPC·기둥에 반드시 걸리므로
            // (FE-003 에서 이미 겪었다) 막히면 목표를 버리고 **물러나며 돈다.**
            //
            // 2026-09-30 실측 근거: 목표만 갈아치우던 판은 0~80 게임초 동안 (-10.3, -22.9) 에
            // 갇혀 2m 만 움직이고 목표를 15번 갈아치웠다. 목표가 전부 같은 방향(hall2f, 동쪽)이라
            // 매번 같은 벽을 밀었다. 방향이 다른 목표가 우연히 걸려서야 풀렸다 - 운이지 코드가 아니다.
            private void Walk(float step)
            {
                if (patrol.Count == 0) return;
                var here = player.transform.position;

                // 막힌 직후에는 목표를 보지 않고 먼저 뒤로 빠진다. 벽에서 떨어지지 않으면
                // 어느 목표를 골라도 같은 벽을 민다.
                if (backoff > 0)
                {
                    backoff -= step;
                    player.StepInput(new Vector2(0, -1f), new Vector2(backoffTurn * step, 0), false, false, false, step);
                    lastSeen = here; sinceProgress = 0;
                    return;
                }

                if (current < 0)
                {
                    current = Pick(here);
                    if (current < 0)
                    {
                        // 지금 서 있는 자리에서 NavMesh 로 닿는 지점이 없다. 물러나서 다시 본다.
                        // 그냥 return 하면 같은 자리에서 같은 판정을 근무 내내 반복한다.
                        backoff = BackoffSeconds;
                        backoffTurn = random.Next(2) == 0 ? -TurnDegreesPerSecond : TurnDegreesPerSecond;
                        return;
                    }
                    lastSeen = here; sinceProgress = 0;
                }

                // 경로 꺾임점을 차례로 따라간다. 직선으로 가면 성분이 끊긴 2층에서 벽만 민다.
                // 마지막 꺾임점은 목표 자신이므로 도착 반경을 더 넉넉히 준다.
                while (corner < corners.Length)
                {
                    float reach = corner == corners.Length - 1 ? ArriveRadius : CornerRadius;
                    var node = corners[corner];
                    // **높이를 포함해 잰다.** 평면 거리로만 재면 경사로를 오르는 꺾임점이 머리 위에
                    // 있을 때 '이미 지났다' 가 되어 그 꺾임점을 건너뛴다. 그러면 경사로를 타지 않고
                    // 질러가다 가장자리로 떨어진다 - 2026-10-01 실측에서 역무원이 y 7.0(2층)에서
                    // 0.0(1층)까지 내려가 140초를 헤맸고, 그때 '남은거리 4.3m' 로 찍혔다.
                    // 실제로는 목표가 7m 위였다. 꺾임점은 NavMesh 위에 있으므로 3차원이 맞다.
                    if (Vector3.Distance(node, here) >= reach) break;
                    corner++;
                }

                if (corner >= corners.Length)
                {
                    visited.Add(current); Drop(); Arrivals++;
                    // 실제로 걸어서 자리가 바뀌었다. 아까 길이 안 열리던 곳도 여기서는 열릴 수 있다.
                    unreachable.Clear();
                    sinceProgress = 0; lastSeen = here; return;
                }

                var waypoint = corners[corner];
                var flat = new Vector3(waypoint.x - here.x, 0, waypoint.z - here.z);

                // 제자리걸음 감지. 벽에 붙어 계속 미는 상태를 그대로 두면 근무 내내 안 움직인다.
                if ((here - lastSeen).sqrMagnitude > .25f) { lastSeen = here; sinceProgress = 0; }
                else
                {
                    sinceProgress += step;
                    if (sinceProgress > StuckSeconds)
                    {
                        // 이 지점은 지금 자리에서 닿지 않는다. 방문 표시해 다시 고르지 않게 하고 물러난다.
                        unreachable.Add(current); Drop(); Unstucks++;
                        sinceProgress = 0; lastSeen = here;
                        backoff = BackoffSeconds;
                        backoffTurn = random.Next(2) == 0 ? -TurnDegreesPerSecond : TurnDegreesPerSecond;
                        return;
                    }
                }

                float wanted = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(player.transform.eulerAngles.y, wanted);
                float turn = Mathf.Clamp(delta, -TurnDegreesPerSecond * step, TurnDegreesPerSecond * step);
                // 많이 틀어져 있으면 먼저 돈다. 옆걸음으로 가면 시야가 진행 방향을 벗어나 인지가 늦다.
                float forward = Mathf.Abs(delta) < FaceToWalkDegrees ? 1f : 0f;
                player.StepInput(new Vector2(0, forward), new Vector2(turn, 0), false, false, false, step);
            }

            // 가까운 미방문 지점 중 **NavMesh 로 완주 가능한** 것을 골라 경로까지 받아둔다.
            //
            // 거리만으로 고르면 안 되는 이유는 실측으로 두 번 확인됐다. 2층 보행 영역은 끊어진
            // 성분 여러 개라(#242) 벽 뒤 6m 지점이 30m 우회로보다 '가깝게' 보이고, 직선 보행자는
            // 그 성분으로 건너갈 수 없다. 가까운 순으로 고른 판은 국소 클러스터를 하나씩 소진하며
            // 140 게임초를 4m 상자 안에서 보냈다.
            private int Pick(Vector3 here)
            {
                int found = Scan(here, out int considered);
                if (found >= 0) return found;

                // 사거리 안에서 길이 안 열렸다. **여기서 unreachable 을 비우면 안 된다.**
                // 비울 이유도 없다 - 후보는 '지금 위치에서 6~60m' 로 걸리므로, 걸어가기만 하면
                // 새 지점이 저절로 사거리에 들어온다. 비웠더니 같은 실패를 되풀이해 계산했다
                // (2026-10-01 실측: 무효 1901건 = 25후보 x 37바퀴).
                //
                // '후보가 다 막혔다' 와 '996곳을 다 돌았다' 는 완전히 다른 일이다. 앞의 것을
                // 한 바퀴로 세면 바퀴 수가 폭주하고, 그 리셋이 다시 실패를 부른다.
                if (visited.Count < patrol.Count) return Recover(here);

                // 996곳을 전부 '도착' 했다. 이때만 한 바퀴다.
                visited.Clear();
                Laps++;
                return Scan(here, out _);
            }

            /// <summary>
            /// 사거리 안에 길이 없을 때 돌아갈 곳을 찾는다. 거리 상한을 풀고 가까운 순으로 본다.
            /// </summary>
            /// <remarks>
            /// 층을 벗어났을 때 순찰 층으로 복귀하는 길이 이것이다. 맹목 후진은 반대로 역무원을
            /// 더 밀어낸다 - 2026-10-01 실측에서 y 가 7.0(2층)에서 1.1(1층)까지 떨어졌고,
            /// 2층 밖에서는 2층 지점이 하나도 안 열려 후진과 실패가 서로를 먹였다.
            ///
            /// 여기서는 unreachable 을 보지 않는다. 그 표시는 '그때 그 자리에서' 안 열렸다는
            /// 뜻이고, 지금은 자리가 다르기 때문이다. 대신 실패를 Invalid/Partial 로 세지도
            /// 않는다 - 같은 지점을 두 번 세지 않기 위해서다.
            /// </remarks>
            private int Recover(Vector3 here)
            {
                candidates.Clear();
                for (int i = 0; i < patrol.Count; i++)
                {
                    if (visited.Contains(i)) continue;
                    var point = patrol[i].Position;
                    candidates.Add(new KeyValuePair<float, int>(
                        new Vector2(point.x - here.x, point.z - here.z).magnitude, i));
                }
                candidates.Sort(CompareDistance);

                int tested = 0;
                foreach (var candidate in candidates)
                {
                    if (tested++ >= CandidatesPerPick) break;
                    int index = candidate.Value;
                    if (!NavMesh.CalculatePath(here, patrol[index].Position, NavMesh.AllAreas, navPath)) continue;
                    if (navPath.status != NavMeshPathStatus.PathComplete) continue;
                    corners = navPath.corners;
                    corner = corners.Length > 1 ? 1 : 0;
                    unreachable.Clear();   // 복귀 경로를 잡았다. 새 자리에서 다시 본다.
                    Recoveries++;
                    return index;
                }
                return -1;   // 복귀할 길조차 없다. 호출한 쪽이 후진으로 자리를 흔든다.
            }

            /// <summary>목표를 버린다. 경로도 함께 버려야 Trace 가 옛 진행도를 찍지 않는다.</summary>
            private void Drop()
            {
                current = -1;
                corners = Array.Empty<Vector3>();
                corner = 0;
            }

            /// <summary>가까운 순으로 최대 CandidatesPerPick 곳의 경로를 계산해 본다.</summary>
            /// <param name="considered">실제로 경로를 계산해 본 후보 수. 0이면 후보 자체가 없었다.</param>
            private int Scan(Vector3 here, out int considered)
            {
                candidates.Clear();
                for (int i = 0; i < patrol.Count; i++)
                {
                    if (visited.Contains(i) || unreachable.Contains(i)) continue;
                    var point = patrol[i].Position;
                    float distance = new Vector2(point.x - here.x, point.z - here.z).magnitude;
                    // 발밑 지점을 골라 곧바로 도착 처리하는 것을 막고, 너무 먼 구간도 자른다.
                    if (distance < MinLegMetres || distance > MaxLegMetres) continue;
                    candidates.Add(new KeyValuePair<float, int>(distance, i));
                }
                candidates.Sort(CompareDistance);

                considered = 0;
                foreach (var candidate in candidates)
                {
                    if (considered >= CandidatesPerPick) break;   // 996곳 전부 계산하면 프레임이 죽는다
                    considered++;
                    int index = candidate.Value;
                    // 실패는 unreachable 로 간다. visited 에 넣으면 '도착했다' 와 섞인다.
                    if (!NavMesh.CalculatePath(here, patrol[index].Position, NavMesh.AllAreas, navPath))
                    { unreachable.Add(index); Invalid++; continue; }
                    if (navPath.status == NavMeshPathStatus.PathPartial) { unreachable.Add(index); Partial++; continue; }
                    if (navPath.status != NavMeshPathStatus.PathComplete) { unreachable.Add(index); Invalid++; continue; }
                    corners = navPath.corners;
                    // 0번 꺾임점은 지금 서 있는 자리다. 그것을 목표로 두면 즉시 '지났다' 가 된다.
                    corner = corners.Length > 1 ? 1 : 0;
                    return index;
                }
                return -1;
            }

            private static int CompareDistance(KeyValuePair<float, int> a, KeyValuePair<float, int> b)
            {
                return a.Key.CompareTo(b.Key);
            }

            // 순찰이 걷고 있는지, 어디서 막혔는지 눈으로 볼 수 있게 남긴다.
            // arrivals 만 보면 '안 걸었다' 와 '걸었지만 목표가 멀었다' 를 구분할 수 없다.
            private void Trace(float deltaSeconds)
            {
                sinceTrace += deltaSeconds;
                if (sinceTrace < TraceSeconds || patrol.Count == 0) return;
                sinceTrace = 0;
                var here = player.transform.position;
                var target = current >= 0 ? patrol[current] : null;
                float remaining = target == null ? -1f
                    : new Vector2(target.Position.x - here.x, target.Position.z - here.z).magnitude;
                Debug.Log("CG_PATROL t=" + session.ShiftSeconds.ToString("0") + "s"
                          + " 위치=(" + here.x.ToString("0.0") + "," + here.y.ToString("0.0") + "," + here.z.ToString("0.0") + ")"
                          + " 목표=" + (target == null ? (backoff > 0 ? "(후진 중)" : "(없음)")
                                : target.Zone + "(" + target.Position.x.ToString("0.0") + "," + target.Position.z.ToString("0.0") + ")")
                          + " 남은거리=" + remaining.ToString("0.0") + "m"
                          + " 걸은거리=" + walked.ToString("0") + "m"
                          + " 꺾임=" + corner + "/" + corners.Length
                          + " 도착=" + Arrivals + " 포기=" + Unstucks + " 복귀=" + Recoveries
                          + " 부분경로=" + Partial + " 무효=" + Invalid + " 바퀴=" + Laps);
            }

            /// <summary>요약 JSON 조각. 이 회차에 역무원이 실제로 무엇을 했는지 남긴다.</summary>
            public JObject Summary()
            {
                var radio = new JObject();
                foreach (var pair in Radioed) radio[pair.Key] = pair.Value;
                return new JObject
                {
                    ["patrolPoints"] = patrol.Count,
                    ["arrivals"] = Arrivals,
                    ["unstucks"] = Unstucks,
                    ["walkedMetres"] = Mathf.RoundToInt(walked),
                    ["laps"] = Laps,
                    ["recoveries"] = Recoveries,
                    ["partialPaths"] = Partial,
                    ["invalidPaths"] = Invalid,
                    ["noops"] = Noops,
                    ["radio"] = radio,
                };
            }
        }

        /// <summary>
        /// 근무를 그림으로 남긴다. 숫자만으로는 역무원이 '무엇을 하고 있었는지' 를 볼 수 없다.
        /// </summary>
        /// <remarks>
        /// batchmode 에서 WaitForEndOfFrame 은 돌아오지 않는다. 그래서 카메라를 직접 Render 해
        /// RenderTexture 로 받고 ReadPixels 로 읽는다. 프로젝트의 EmergencySceneBuilder 가 쓰는
        /// 방식과 같다.
        ///
        /// **-nographics 로 돌리면 빈 화면이 나온다.** 그래픽 장치가 없으면 Render 가 아무것도
        /// 그리지 않는다. 촬영할 때는 그 옵션을 뺀다.
        ///
        /// 기본은 꺼짐이다. 수집 실행에서 프레임을 쓰면 디스크와 시간을 먹고, 무엇보다
        /// 측정 대상(근무)에 없던 부하를 더한다.
        /// </remarks>
        private sealed class Recorder
        {
            private const int Width = 854, Height = 480;
            private readonly string folder;
            private readonly float interval;
            private readonly RenderTexture target;
            private readonly Texture2D shot;
            private float since;
            private bool closed;
            public int Frames { get; private set; }

            public Recorder(string folder, float interval)
            {
                this.folder = folder;
                this.interval = Mathf.Max(.05f, interval);
                Directory.CreateDirectory(folder);
                // 한 번만 만든다. 프레임마다 새로 만들면 근무가 길어질수록 느려진다.
                target = new RenderTexture(Width, Height, 24);
                shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            }

            public void Step(EmergencySession session, float deltaSeconds)
            {
                since += deltaSeconds;
                if (since < interval) return;
                since = 0;
                var camera = session.Player != null ? session.Player.PlayerCamera : null;
                if (camera == null) return;

                var wasTarget = camera.targetTexture;
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = wasTarget;

                var wasActive = RenderTexture.active;
                RenderTexture.active = target;
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply(false);
                RenderTexture.active = wasActive;

                File.WriteAllBytes(Path.Combine(folder, "f" + Frames.ToString("00000") + ".jpg"),
                                   shot.EncodeToJPG(75));
                Frames++;
            }

            public void Close()
            {
                if (closed) return;
                closed = true;
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            }
        }

        /// <summary>
        /// 쓸 수 있는 JEV 키가 없으면 그 이유를, 있으면 null 을 돌려준다. 키 값도, 키 파일 경로도 담지 않는다.
        /// </summary>
        /// <remarks>
        /// 게임이 키를 고르는 <see cref="JevKey.Load"/> 를 그대로 부른다. 환경변수가 공백이 아니면 그것만 본다
        /// (값이 off 면 꺼진 것이고, 키 형식이 아니면 키 파일로 넘어가지 않고 키가 없는 것이다).
        /// 환경변수가 비어 있으면 사용자 폴더의 키 파일을 본다. 따로 만든 검사는 게임과 어긋나서 무효 키로도
        /// 통과한 뒤 한 회차(최대 20분)를 돌리고서야 실패했다. 서버가 그 키를 받아 줄지는 요청을 보내 봐야
        /// 알며, 회차 요약의 answeredLines 가 그것을 말해 준다.
        /// </remarks>
        private static string KeyProblem()
        {
            if (JevKey.Load(out var source) != null) return null;
            if (source == JevKeySource.Off)
                return "TYPESAFE_API_KEY=off 입니다. JEV 를 끄면 비상상황이 만들어지지 않아 수집할 것이 없습니다.";
            if (JevKey.VariableUnusable)
                return "TYPESAFE_API_KEY 가 키 형식이 아닙니다(JevKey.Plausible). 환경변수가 있으면 키 파일은 보지 않습니다.";
            return "JEV 키가 없거나 키 파일이 키 형식이 아닙니다(JevKey.Plausible). 키가 없으면 비상상황이 만들어지지 않아 수집할 것이 없습니다. "
                 + "TYPESAFE_API_KEY 를 설정하거나 타이틀의 'JEV 연결' 에서 키를 저장하세요.";
        }

        private static long LogLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

        /// <summary>한 세션의 JSONL 에서 읽어 센 것.</summary>
        private sealed class RunLog
        {
            /// <summary>시작 위치 뒤의 완결된 줄 전부(원문 그대로).</summary>
            public string Text = "";
            public int Lines, Answered, Malformed;
            /// <summary>답한 줄을 purpose 별로.</summary>
            public readonly SortedDictionary<string, int> AnsweredBy = new SortedDictionary<string, int>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 한 세션의 기록 파일에서 <paramref name="from"/> 바이트 뒤의 완결된 줄을 읽는다. 답한 줄(http 200 이고
        /// answers 가 비어 있지 않음)만 <see cref="RunLog.Answered"/> 로 센다.
        /// </summary>
        private static RunLog ReadRunLog(string path, long from)
        {
            var result = new RunLog();
            if (!File.Exists(path)) return result;
            string text;
            // 클라이언트가 같은 파일에 붙여 쓰는 중일 수 있어 공유로 연다.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                stream.Seek(from, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream, new UTF8Encoding(false))) text = reader.ReadToEnd();
            }
            // 끝이 '\n' 으로 닫히지 않은 조각은 쓰는 중인 줄이다. 완결된 줄만 취한다.
            int end = text.LastIndexOf('\n');
            result.Text = end < 0 ? "" : text.Substring(0, end + 1);
            foreach (var line in result.Text.Split('\n'))
            {
                if (line.Length == 0) continue;
                result.Lines++;
                JObject entry;
                try { entry = JObject.Parse(line); }
                catch (JsonException) { result.Malformed++; continue; }
                if ((long?)entry["http"] != 200 || !(entry["answers"] is JObject answers) || answers.Count == 0) continue;
                result.Answered++;
                var purpose = (string)entry["purpose"] ?? "";
                result.AnsweredBy[purpose] = result.AnsweredBy.TryGetValue(purpose, out var had) ? had + 1 : 1;
            }
            if (result.Malformed > 0)
                Debug.LogWarning("CG_HARNESS JSONL 에서 읽지 못한 줄이 " + result.Malformed + "개 있습니다. 답한 줄로 세지 않았습니다.");
            return result;
        }

        /// <summary>회차 요약을 한 회차당 한 줄인 JSON 배열로 쓴다. 매 회차 끝에 전체를 다시 쓰므로 언제나 유효한 JSON 이다.</summary>
        private static void WriteSummary(string path, JArray shifts)
        {
            var text = new StringBuilder("[\n");
            for (int i = 0; i < shifts.Count; i++)
                text.Append("  ").Append(shifts[i].ToString(Formatting.None)).Append(i < shifts.Count - 1 ? ",\n" : "\n");
            File.WriteAllText(path, text.Append("]\n").ToString(), new UTF8Encoding(false));
        }

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

        // ── 시험이 바꾸는 전역 상태 ─────────────────────────────────────────────
        // 근무는 시간 배율·음소거·로그 판정·다음 시드·씬을 바꾼다. 시험이 중간에 실패해도 그대로 남으면 같은
        // 플레이 세션의 뒤 시험이 오염된다. SetUp 이 원래 값을 받아 두고 UnityTearDown 이 성공·실패·예외 어느
        // 쪽이든 되돌린다(iterator 의 finally 에서는 씬을 내릴 수 없다 - finally 안에서는 yield 를 못 쓴다).
        private float timeScaleBefore, volumeBefore;
        private bool pausedBefore, ignoreFailingBefore;
        private int nextSeedBefore;
        private Recorder recorder;
        // 이번 시도에서 본 것. 시도마다 비운다.
        private readonly HashSet<Hazard> hazardsSeen = new HashSet<Hazard>();
        private bool playerKnew;
        private int errorLogs;

        [SetUp]
        public void SaveGlobals()
        {
            timeScaleBefore = Time.timeScale;
            volumeBefore = AudioListener.volume;
            pausedBefore = AudioListener.pause;
            ignoreFailingBefore = LogAssert.ignoreFailingMessages;
            nextSeedBefore = EmergencySession.NextSeed;
            recorder = null;
            UnityEngine.Application.logMessageReceived += CountError;
        }

        // 먼저 요약·사본을 남기고 끝에서 오류 수로 실패시킨다. 측정 기록을 잃거나 런타임 오류를 성공으로 숨기지 않는다.
        private void CountError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorLogs++;
        }

        [UnityTearDown]
        public IEnumerator RestoreGlobals()
        {
            UnityEngine.Application.logMessageReceived -= CountError;
            if (recorder != null) { recorder.Close(); recorder = null; }
            // 로그 판정은 씬을 내리기 전에 되돌린다. 내리다 난 오류는 다른 시험과 똑같이 실패로 드러나야 한다.
            LogAssert.ignoreFailingMessages = ignoreFailingBefore;
            AudioListener.volume = volumeBefore;
            AudioListener.pause = pausedBefore;
            EmergencySession.NextSeed = nextSeedBefore;

            var emergency = SceneManager.GetSceneByName(SceneFlow.EmergencyScene);
            var station = SceneManager.GetSceneByName(SceneFlow.StationScene);
            if (emergency.isLoaded || station.isLoaded)
            {
                // 근무 씬(역무원·열차를 쓰는 쪽)을 먼저 내리고 한 프레임 뒤 역 씬을 내린다(다른 근무 시험과 같은 순서).
                SceneManager.SetActiveScene(SceneManager.CreateScene("ShiftSampleScratch"));
                if (emergency.isLoaded) yield return SceneManager.UnloadSceneAsync(emergency);
                yield return null;
                if (station.isLoaded) yield return SceneManager.UnloadSceneAsync(station);
            }
            // 근무 세션이 내려가며 Time.timeScale 을 1 로 돌려놓으므로 씬을 다 내린 뒤에 원래 값을 되돌린다.
            Time.timeScale = timeScaleBefore;
        }

        /// <summary>한 프레임의 일. 역무원이 움직이고, 찍고, 본 것을 센다. JEV 가 키를 거부했으면 false - 더 돌려도 모이는 것이 없다.</summary>
        private bool Tick(EmergencySession session, StaffPolicy staff)
        {
            float dt = Time.deltaTime;
            staff.Step(dt);
            if (recorder != null) recorder.Step(session, dt);
            foreach (var hazard in HazardRegistry.Active)
                if (hazard != null) hazardsSeen.Add(hazard);
            if (session.Incidents != null && session.Incidents.PlayerKnowsIncident) playerKnew = true;
            return !session.Jev.Rejected;
        }

        // Unity 테스트 프레임워크 1.6.0 은 [Timeout] 이 없으면 180초 뒤 UnityTestTimeoutException 으로 끝낸다
        // (UnityWorkItem.k_DefaultTimeout). 1배속 20분 근무가 3분에 끊기므로 틀을 풀고, 실시간 상한은
        // 회차마다 CG_SHIFT_REALCAP 이 맡는다(재시도가 있으면 시도마다).
        [UnityTest, Explicit("JEV 를 실제로 호출하고 과금된다. -testFilter 로 직접 지정해 돌릴 것."), Timeout(int.MaxValue)]
        public IEnumerator 근무를_돌려_JEV_판단을_모은다()
        {
            int count = Mathf.Max(1, Number("CG_SHIFT_COUNT", 1));
            // 사건 시각·종류·개수는 그 순간의 세계와 JEV 판단으로 정해진다. 긴 근무도 사건 발생을 보장하지 않는다.
            float gameSeconds = Mathf.Max(60, Number("CG_SHIFT_SECONDS", 1200));
            float scale = Mathf.Clamp(Number("CG_SHIFT_SCALE", 1), 1, 20);
            float realCap = Mathf.Max(60, Number("CG_SHIFT_REALCAP", 1800));
            int seed = Number("CG_SHIFT_SEED", 0);
            var outFolder = Environment.GetEnvironmentVariable("CG_SHIFT_OUT");
            // 순찰 구역. 기본값은 **대합실(맞이방)이 있는 2층 실내**로 고정한다(2026-09-30 사용자 지시).
            // station-points.json 에서 y 5.50~10.50 인 실내 구역만 골랐다:
            //   hall2f(2층 맞이방 885점) · main2f(2층 본관 82) · eastexit(2층 동측 출구 29) = 996점
            //
            // 한 층으로 묶는 이유는 취향이 아니다. 여러 층을 섞으면 다음 목표가 위층·아래층이 되어
            // 직선 보행자가 도달할 수 없다. 실제로 5구역(1·2·3층)으로 240초를 돌렸을 때
            // arrivals=0 · unstucks=2 였다 — 순찰이 한 번도 도착하지 못했다.
            //
            // 뺀 곳: ground1f(1층) · upper3f(3층) — 다른 층.
            //        skyplaza(x 112~235) · plaza(역 광장) · tracks(승강장·선로) — 건물 바깥.
            //        southgate · northdeck — 같은 2층이지만 맞이방 z 범위 밖으로 벗어나 실내 여부가 불확실.
            // 사건은 역 전체에서 합성된다(하드룰 2). 여기서 제한하는 것은 **역무원이 걸어다니는 범위**뿐이다.
            // 근무를 찍어 둘 폴더. 비우면 찍지 않는다(기본).
            var film = Environment.GetEnvironmentVariable("CG_SHIFT_FILM");
            float filmEvery = Mathf.Max(.05f, Number("CG_SHIFT_FILM_EVERY_MS", 500) / 1000f);
            var zones = Environment.GetEnvironmentVariable("CG_SHIFT_ZONES");
            // 요약에 타임라인·합성 기록을 얹을지. 끄는 것이 기본이다 - 회차마다 줄 수가 달라
            // 요약을 기계로 읽는 쪽의 가정을 깬다.
            var timelineFlag = (Environment.GetEnvironmentVariable("CG_SHIFT_TIMELINE") ?? "").Trim();
            bool withTimeline = timelineFlag == "1"
                || timelineFlag.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(zones)) zones = "hall2f,main2f,eastexit";
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

            // 키가 없으면 비상상황이 **아예 만들어지지 않는다**. 예전에는 로컬 가중치가 대신 골랐지만
            // 개정된 하드룰이 그 대체를 금지했다. 그래서 키 없이 돌리면 조용한 근무만 쌓이는데,
            // 그것을 '학습 데이터' 라고 부르면 거짓이 된다. 모으기 전에 끊는다.
            var keyProblem = KeyProblem();
            Debug.Log("CG_HARNESS JEV 키 " + (keyProblem == null ? "있음" : "없음"));
            Assert.IsTrue(keyProblem == null, keyProblem);

            Debug.Log("CG_HARNESS count=" + count + " seconds=" + gameSeconds + " scale=" + scale
                      + " realCap=" + realCap + " seed=" + seed + " attempts<=" + maxAttempts
                      + " out=" + (string.IsNullOrEmpty(outFolder) ? "(없음)" : outFolder)
                      + " zones=" + zones);
            if (scale > 1)
                Debug.LogWarning("CG_HARNESS 시간을 " + scale + "배로 압축한다. 디렉터는 1초마다,"
                                 + " 그리고 상황이 바뀌는 즉시 판단하는데 분당 요청 상한은 실시간 기준이라"
                                 + " 압축할수록 더 많은 판단이 상한에 걸려 버려진다. 수집용으로는 CG_SHIFT_SCALE=1 로 둘 것.");
            if (gameSeconds < 300)
                Debug.LogWarning("CG_HARNESS 근무가 " + gameSeconds + "초뿐이다."
                                 + " 사건 발생은 보장되지 않으며 짧은 실행은 긴 근무 분포의 증거가 아니다.");

            // 요약과 JSONL 사본. 쓸 수 없는 폴더면 20분을 돌리기 전에 여기서 실패한다.
            // JevClient 는 세션을 열 때마다 오래된 jev-*.jsonl 을 지우므로(JevClient.KeepRunLogs), 사본은
            // 시도가 끝나는 즉시 떠 둔다 - 백업을 끝에 몰아서 하면 앞 회차는 그 사이 지워질 수 있다.
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string summaryPath = null, copies = null;
            if (!string.IsNullOrEmpty(outFolder))
            {
                Directory.CreateDirectory(outFolder);
                summaryPath = Path.Combine(outFolder, "shifts-" + stamp + ".json");
                copies = Path.Combine(outFolder, "jev-" + stamp);
                Directory.CreateDirectory(copies);
            }
            else
                Debug.LogWarning("CG_HARNESS CG_SHIFT_OUT 이 없어 JSONL 사본을 뜨지 않는다. 기록은 jev-runs 에만 남고,"
                                 + " 세션이 " + JevClient.KeepRunLogs + "개를 넘으면 오래된 파일부터 지워진다.");

            var shifts = new JArray();
            var undrained = new List<int>();
            int totalErrorLogs = 0;
            for (int shift = 0; shift < count; shift++)
            {
                bool accepted = string.IsNullOrEmpty(require);
                int attempts = 0;
                bool discard;
                do
                {
                    int attempt = attempts++;
                    discard = false;
                    errorLogs = 0;
                    // 씬 부팅을 포함해 기록을 먼저 보존하고, 모든 시도의 오류를 끝에서 실패로 드러낸다.
                    LogAssert.ignoreFailingMessages = true;
                    // 시드를 회차·시도마다 다르게 준다. 같은 시드로 다시 돌리면 같은 근무가 나온다.
                    // 지정하지 않으면(0) 앞 시험이 남긴 NextSeed 를 쓰지 않도록 비운다 - 그래야 세션이 무작위로 정한다.
                    EmergencySession.NextSeed = seed != 0 ? seed + shift * maxAttempts + attempt : 0;
                    // 두 씬을 순서대로 올린다. StationEmergency 를 단독으로 열면 세션이 FpsStation 의
                    // 역무원을 찾지 못한다. 평소에는 SceneFlow.EnsureSessionForDirectStationPlay 가
                    // 이 Additive 로드를 대신하지만 [RuntimeInitializeOnLoadMethod] 라 PlayMode 시작 때
                    // 한 번만 돌고, 시험 러너가 이미 시작한 뒤라 다시 불리지 않는다.
                    yield return SceneManager.LoadSceneAsync(SceneFlow.StationScene, LoadSceneMode.Single);
                    yield return null;
                    yield return SceneManager.LoadSceneAsync(SceneFlow.EmergencyScene, LoadSceneMode.Additive);
                    yield return null;

                    var session = UnityEngine.Object.FindFirstObjectByType<EmergencySession>();
                    Assert.IsNotNull(session, "EmergencySession 을 찾지 못했습니다 (회차 " + shift + ").");
                    float bootDeadline = Time.realtimeSinceStartup + 60f;
                    while (session.Incidents == null && Time.realtimeSinceStartup < bootDeadline) yield return null;
                    Assert.IsNotNull(session.Incidents, "근무가 부팅되지 않았습니다 (회차 " + shift + ").");
                    var jev = session.Jev;
                    Assert.IsNotNull(jev, "JEV 클라이언트가 없습니다 (회차 " + shift + ").");

                    Silence(session);
                    // 역무원을 외부 입력으로 돌린다. 이 시점 이후 사람 입력은 무시된다.
                    // seed=0 은 '매번 무작위' 다. 전에는 shift+1 을 줘서 독립 실행끼리 순찰 순서와
                    // 아무것도 안 함 동전이 비트 단위로 같았다 - 문서가 약속한 다양성이 없었다.
                    int staffSeed = seed != 0 ? seed + shift : Environment.TickCount + shift * 7919;
                    Debug.Log("CG_POLICY 역무원 시드 " + staffSeed);
                    var staff = new StaffPolicy(session, zones, staffSeed);
                    if (!string.IsNullOrEmpty(film))
                        recorder = new Recorder(Path.Combine(film, "shift" + shift + "-attempt" + attempt), filmEvery);
                    hazardsSeen.Clear();
                    playerKnew = false;
                    // 이 세션의 기록 파일 하나만 본다. jev-runs 폴더 전체를 세면 이전 실행의 기록이 섞이고,
                    // JevClient 가 오래된 파일을 지우는 만큼 줄 수가 줄어든다. 파일은 세션마다 새로 생기므로
                    // 이미 있다면 그 길이 뒤부터가 이 세션의 것이다(첫 요청은 근무 26초 뒤라 지금은 비어 있다).
                    string runLog = jev.LogFile;
                    Assert.IsFalse(string.IsNullOrEmpty(runLog), "JevClient 가 기록 파일을 열지 않았습니다 (회차 " + shift + ").");
                    long runLogStart = LogLength(runLog);
                    float startedReal = Time.realtimeSinceStartup;
                    float startedShift = session.ShiftSeconds;
                    Time.timeScale = scale;
                    // ① 요구한 사건이 날 때까지 기다린다. 요구가 없으면 이 구간을 건너뛴다.
                    if (!accepted)
                    {
                        while (session.ShiftSeconds - startedShift < seek
                               && Time.realtimeSinceStartup - startedReal < realCap
                               && !Present(require)
                               && Tick(session, staff))
                            yield return null;
                        accepted = Present(require);
                        Debug.Log("CG_SEEK shift=" + shift + " attempt=" + attempt
                                  + " require=" + require + " found=" + accepted
                                  + " game=" + (session.ShiftSeconds - startedShift).ToString("0") + "s");
                        // 키를 거부당한 세션은 다시 열어도 같다 - 재시도하지 않는다.
                        if (!accepted && attempts < maxAttempts && !jev.Rejected) discard = true;
                        // 상한에 걸리면 포기하고 그 회차는 요구 없이 그대로 쓴다 — 버리면 아무 표본도 안 남는다.
                        else if (!accepted)
                            Debug.LogWarning("CG_SEEK shift=" + shift + " 시도 " + attempts
                                             + "회 안에 '" + require + "' 가 나지 않았다. 이 회차는 요구 없이 기록한다.");
                    }
                    // ② 남은 시간을 채운다. 버릴 시도는 채우지 않는다.
                    if (!discard)
                        while (session.ShiftSeconds - startedShift < gameSeconds
                               && Time.realtimeSinceStartup - startedReal < realCap
                               && Tick(session, staff))
                            yield return null;

                    float realSpent = Time.realtimeSinceStartup - startedReal;
                    float gameSpent = session.ShiftSeconds - startedShift;
                    if (recorder != null)
                    {
                        Debug.Log("CG_FILM shift=" + shift + " attempt=" + attempt + " 프레임 " + recorder.Frames + "장");
                        recorder.Close();
                        recorder = null;
                    }
                    // 끝났다. 디렉터는 게임 시간(ShiftSeconds)으로 묻는 만큼 시간을 멈추면 새로 묻지 않는다.
                    // 클라이언트는 줄을 풀 스레드에서 비동기로 쓰므로 기다리지 않고 읽으면 마지막 요청들이 빠진다.
                    // 기다림은 추측한 초가 아니라 JevClient.PendingRequests(받아들여졌지만 답과 기록이 아직 끝나지 않은
                    // 요청 수)가 0 이 될 때까지다. 요청 제한시간 + 3초를 넘으면 남은 수를 요약에 남기고 넘어간다.
                    Time.timeScale = 0f;
                    float drainUntil = Time.realtimeSinceStartup + jev.TimeoutSeconds + 3f;
                    while (jev.PendingRequests > 0 && Time.realtimeSinceStartup < drainUntil) yield return null;
                    int pending = jev.PendingRequests;
                    long requests = jev.Usage.Requests;
                    if (pending > 0)
                        Debug.LogWarning("CG_FLUSH shift=" + shift + " attempt=" + attempt + " 요청 " + pending
                                         + "건이 끝나지 않은 채 기다림을 끝냈다. 이 요청들의 기록은 JSONL 에 없을 수 있다.");
                    var log = ReadRunLog(runLog, runLogStart);
                    if (pending > 0) undrained.Add(shift);
                    totalErrorLogs += errorLogs;

                    string copyName = "jev-shift-" + shift.ToString("00", CultureInfo.InvariantCulture);
                    string copyRelative = null;
                    if (copies != null)
                    {
                        // 버린 시도(요구한 사건이 안 난 회차)는 표본이 아니다. 섞이지 않게 따로 둔다.
                        copyRelative = discard
                            ? Path.Combine("discarded", copyName + "-attempt-" + attempt + ".jsonl")
                            : copyName + ".jsonl";
                        var target = Path.Combine(copies, copyRelative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.WriteAllText(target, log.Text, new UTF8Encoding(false));
                    }
                    if (discard)
                    {
                        Debug.Log("CG_ATTEMPT_DISCARDED shift=" + shift + " attempt=" + attempt + " seed=" + session.World.Seed
                                  + " answered=" + log.Answered + "/" + log.Lines);
                        continue;   // do-while 의 조건(discard)으로 간다: 같은 회차를 새 세션으로 다시 연다
                    }

                    var answeredBy = new JObject();
                    foreach (var pair in log.AnsweredBy) answeredBy[pair.Key] = pair.Value;
                    var line = new JObject
                    {
                        ["shift"] = shift,
                        // 세션이 실제로 쓴 시드. 지정하지 않았으면(0) 세션이 무작위로 정한 값이다.
                        ["seed"] = session.World.Seed,
                        ["staffSeed"] = staffSeed,
                        ["gameSeconds"] = Mathf.RoundToInt(gameSpent),
                        ["realSeconds"] = Mathf.RoundToInt(realSpent),
                        // 받아들여진 요청 수. 기록된 줄(jevLogLines) + 끝나지 않은 요청(pendingRequests) 보다 크면
                        // 그 차이는 기록 파일이 상한(JevClient.MaxRunLogBytes)에 걸려 버려진 줄이다.
                        ["requests"] = requests,
                        ["jevLogLines"] = log.Lines,
                        // 수집물로 쓸 수 있는 줄: http==200 이고 answers 가 비어 있지 않다. 401·429·시간초과는 줄로 남아도 여기에 안 든다.
                        ["answeredLines"] = log.Answered,
                        ["answeredByPurpose"] = answeredBy,
                        ["malformedLines"] = log.Malformed,
                        // 기다림이 끝났을 때 요청이 더 남아 있었다면 drained=false 다 - 이 회차의 사본은 완전하지 않다.
                        ["drained"] = pending == 0,
                        ["pendingRequests"] = pending,
                        ["jevRejected"] = jev.Rejected,
                        ["keyPresent"] = true,
                        ["require"] = require ?? "",
                        ["attempts"] = attempts,
                        ["accepted"] = accepted,
                        ["model"] = jev.LastModel ?? "",
                        // 이 회차에 실제로 무슨 일이 있었는지의 서술. 특정 사건이 나야 한다는 기준이 아니다.
                        ["hazardsSeen"] = hazardsSeen.Count,
                        ["playerKnew"] = playerKnew,
                        ["errorLogs"] = errorLogs,
                        ["staff"] = staff.Summary(),
                    };
                    // 인게임 시간순 사건 기록. 만들지 않고 ShiftLog 가 이미 담은 것을 얹는다 -
                    // timeline 은 t(초)와 clock("00:12")을 함께, compositions 는 사건마다
                    // kind·origin·magnitude·candidates·detail 을 담는다.
                    //
                    // 기본으로 켜지 않는 이유는 용량이 아니라 **양식 안정성**이다. 타임라인은
                    // 회차마다 줄 수가 달라 요약을 기계로 읽는 쪽의 가정을 깬다.
                    // (용량은 문제가 아니다 - 타임라인 한 줄 약 70바이트, JSONL 한 줄은 실측 2.9KB.)
                    if (withTimeline && session.Log != null)
                    {
                        var report = session.Log.ToJson("수집 종료");
                        line["timeline"] = report["timeline"];
                        line["compositions"] = report["compositions"];
                        var entries = report["timeline"] as JArray;
                        Debug.Log("CG_TIMELINE shift=" + shift + " " + (entries == null ? 0 : entries.Count) + "줄");
                    }
                    if (copyRelative != null) line["jsonl"] = Path.Combine("jev-" + stamp, copyRelative).Replace('\\', '/');
                    Debug.Log("CG_SHIFT " + line.ToString(Formatting.None));
                    shifts.Add(line);
                    // 회차가 끝날 때마다 다시 쓴다: 뒤 회차에서 멈춰도 앞 회차 요약이 남는다.
                    if (summaryPath != null) WriteSummary(summaryPath, shifts);
                    // '라운드가 돌았나' 는 물을 수 없다 - 고정 주기가 없다. 대신 JEV 가 실제로 **답했는지** 를 묻는다.
                    // 줄이 있어도 401(키 거부)·429(한도)·시간초과면 답이 없다. 그런 근무는 수집물로 쓸 수 없다.
                    Assert.Greater(log.Answered, 0, "JEV 가 답한 요청(http 200 + answers)이 한 건도 없습니다 (회차 " + shift
                                                    + "). 기록된 줄 " + log.Lines + "개 · 받아들여진 요청 " + requests
                                                    + "건 · 상태 '" + jev.Status + "'. 키·네트워크·분당 예산을 확인하세요.");
                } while (discard);
            }

            Debug.Log("CG_HARNESS_LOGS " + Path.Combine(UnityEngine.Application.persistentDataPath, "jev-runs"));
            if (summaryPath != null) Debug.Log("CG_HARNESS_OUT " + summaryPath + " · " + copies);
            // 모은 것은 모두 썼다. 그래도 요청이 끝나지 않은 채 사본을 뜬 회차가 있으면 성공이라 부르지 않는다.
            Assert.That(undrained, Is.Empty, "요청이 끝나기 전에 기다림을 끝낸 회차가 있습니다(요약의 drained=false): "
                                              + string.Join(",", undrained) + ". 그 회차의 JSONL 사본은 완전하지 않을 수 있습니다.");
            Assert.That(totalErrorLogs, Is.Zero, "근무 중 런타임 오류가 기록됐습니다. 요약의 errorLogs와 Unity 로그를 확인하세요.");
        }
    }
}
#endif
