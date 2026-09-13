# 음성 수정 전달과 Foundation 팀 인계 — 2026-09-13

## 이번 전달 범위

최신 사용자 요청에 따라 개발은 음성 수정에서 멈춘다. 기존 미게시 공통 런타임·의존성·시험·도구를 선별해 함께 전달하되 오픈월드 신규 모델링은 시작하지 않는다. 작업 브랜치는 `feature/54-native-foundation`, PR 대상은 `develop`이며 병합은 별도 검토 사항이다.

자동 AAA/REWORK 반복과 추가 에이전트 검수 루프는 사용량 문제로 중단됐다. 과거 문서·이슈의 자동 재실행 문구는 현재 지시가 아니다. 외부 Higgsfield/VARCO/CV 제작도 재개하지 않는다. 기존 기록은 삭제하거나 새 수용으로 승격하지 않는다.

## 음성 변경

- `VoiceEpochCoordinator`: 최초 읽기 실패와 읽을 수 있지만 부적합한 기록을 구분한다. 원격 호출 전 최초 읽기만 실패한 경우 같은 서비스의 명시적 재시도로 다시 읽는다. 이미 발생한 원격 불확실성은 이 재시도로 해제하지 않는다.
- `ServerVoiceService` / `PersistentVoiceEpochStore`: schema 3의 `PendingOperations`를 실제 dispatch 전에 canonical에 저장한다. 새 서비스와 새 store adapter도 같은 canonical의 미완료 호출을 보고 발급·대체 작업을 차단한다. 완료 처리는 operation ID와 canonical 재읽기를 대조한다.
- 이전 방 삭제 확인 → 전체 새 방 intent 저장 → 생성 확인 → Ready 저장 순서를 유지한다. downtime은 허용하며 무중단을 주장하지 않는다.
- `Assert.Multiple`을 지원하지 않는 설치 NUnit에 맞춰 일반 assertion 블록을 사용한다. assertion 조건은 유지하지만 여러 실패를 한꺼번에 모으지는 않는다.

### 저장 형식 및 운영 제한

schema 3은 `PendingOperations` 필드를 요구한다. 구 schema 및 필드가 누락되거나 부적합한 canonical은 fail-closed이며 자동 migration을 제공하지 않는다. 프로세스 재시작 후 미완료 marker가 남으면 운영자가 실제 LiveKit room 상태를 확인하는 별도 복구 절차가 필요하다. marker를 무조건 지우거나 `.tmp`/`.bak`를 권위 있는 기록으로 승격하지 않는다.

파일 adapter는 single-process/single-writer 계약이다. 다중 프로세스 동시 writer, 임의 OS/power loss까지 안전하다는 증거는 없다. callback 모의시험은 실제 HTTP 완료·실제 프로세스 재시작 시험을 대신하지 않는다.

## 확인한 시험과 한계

Unity **6000.3.23f1**, 공식 Unity CLI, EditMode의
`ChooGuard.Foundation.Multiplayer.Tests.VoiceEpochCoordinatorTests`:

- 최종 NUnit XML: **89 total / 89 passed / 0 failed / 0 skipped / 0 inconclusive**, 고유 test case 89개.
- CLI 종료 코드 0, timeout 없음.
- 이전 실제 시험의 87개 중 최초 읽기 복구 6건과 새 서비스 pending-call 1건 실패를 수정했다. 중간 compile 실패도 있었다. 최종 성공이 이전 실패 이력을 지우지 않는다.
- 최종 실행 뒤의 핵심 3개 파일 SHA-256과 전달 소스를 대조했다. 이전 시도 전 manifest는 수정 전 시험 파일을 포함하므로 최종 실행의 전후 동일성 증거로 사용하지 않는다.
- 실제 마이크/PTT/스피커, LiveKit/Docker, WAN, 20명·NPC100·사건2건, Windows/Linux Player, 독립 개발자의 새 checkout 수용은 이 시험으로 검증하지 않았다.
- 기존 전체 fixture의 70 skipped 원인은 별도 조사 대상이며 보존 guard를 삭제해서 통과시키지 않는다.
- 현재 설치 캐시의 의존성 검증: LiveKit 고정 commit과 binary 7개, Pipeline patch 일치. 다운로드나 `--apply`는 실행하지 않았다.
- 커밋 `dfc6409b049ea187a49d2bf475aa8489f2c0937f`의 소스만 별도 디렉터리에 추출해 Python 시험을 다시 실행했다: `test_multiplayer_dependencies.py` **4/4**, `test_configure_local.py` **24/24**. 이 검사는 Git archive 기반의 오프라인 소스 검사이며 새 checkout의 Unity package resolve/compile을 실행한 결과는 아니다. 설정 시험 24/24는 #151의 미해결 소유권 경계를 증명하지 않는다.

재실행 예시(이미 승인·설치된 Editor/의존성을 사용하는 소유한 작업환경):

```sh
unity test "$PROJECT" --mode EditMode \
  --filter ChooGuard.Foundation.Multiplayer.Tests.VoiceEpochCoordinatorTests \
  --output "$NEW_EVIDENCE_DIR/voice.xml" --report-format nunit \
  --editor-path "$UNITY_EDITOR_APP" --timeout 240 --retries 0 \
  --format json --non-interactive -- -nographics \
  -logFile "$NEW_EVIDENCE_DIR/unity.log"
```

macOS의 `UNITY_EDITOR_APP`은 `.app` bundle이다. 실행 전 기존 Editor/writer를 확인하고 중복 실행하지 않는다. 새 빈 evidence 디렉터리를 쓰고 원시 로그·사용자 경로·음성 녹음·비밀 설정은 Git에 넣지 않는다. 의존성 점검은 `python3 scripts/dev/prepare_multiplayer_dependencies.py`의 기본 검증 모드로 수행한다. `--apply`는 다운로드·캐시 수정을 하므로 별도 승인 없이 실행하지 않는다.

## 선별 게시와 제외

이 PR은 누적 Native 소스의 검토용 기준선이다. 생성된 `ConnectedWorld`, `FoundationSimulation`, `FoundationReview`, `MultiplayerSlice` 장면·재질·텍스처·prefab, 시험용 생성 장면, 로컬 raw evidence, 캐시, 실행본, 자격 증명, `.claude/`는 포함하지 않는다. 직렬화 YAML/meta를 손으로 수정하지 않는다. 기존 meta는 소스와 짝을 맞춰 전달한다.

`ProjectSettings/ProjectSettings.asset`의 로컬 변경도 제외한다. 게시 준비 중 `MultiplayerSceneBuilder`, `ConnectedWorldSceneBuilder`, `FoundationSimulationSceneBuilder`의 `insecureHttpOption = AlwaysAllowed` 자동 설정을 제거했다. 세 builder는 Unity의 기존 평문 HTTP 제한을 변경하지 않으며 runtime endpoint 검증도 loopback 밖에서 TLS를 요구한다. 이미 바뀐 로컬 ProjectSettings를 복구하거나 원격 서비스의 TLS를 구성한 것은 아니다. 기본 보안 설정에서는 loopback HTTP recipe가 차단될 수 있으므로 #100/#143에서 TLS 구성과 실제 플랫폼 endpoint 경계를 검증한다. 해결을 위해 전역 보안 제한을 자동 해제하지 않는다.

기존 Demo 변경, CV 제거 변경, 과거 보고서/컨텍스트 정리는 음성 수정과 분리해 원래 작업트리에 보존한다. source-only 게시를 완성된 장면/실행본 전달로 해석하지 않는다. #120의 독립 재현·나머지 게시 정합화는 여전히 열려 있다.

### 선별 소스 식별과 검토 상태

| 커밋 | 작업 단위 | 변경 경로 수 |
|---|---|---:|
| `39e349d` | 공통 멀티플레이·시뮬레이션 계약 | 50 |
| `6d533f6` | 음성 수정과 필요한 네트워크 런타임·시험·패키지 | 185 |
| `dfc6409` | 검증 도구·로컬 LiveKit recipe | 25 |

기준 `e7a6bb7`에서 `dfc6409b049ea187a49d2bf475aa8489f2c0937f`까지 정확히 260개 경로가 선별 목록과 일치하고 해당 커밋 bytes와 로컬 선택 파일도 일치했다. 경로 목록은 `git diff --name-only e7a6bb7 dfc6409b049ea187a49d2bf475aa8489f2c0937f`로 얻는다. 이 일치는 실행·안전·전체 코드감사 증거가 아니다. 이후 게시 보안 수정에서는 세 builder의 HTTP 제한 자동 해제 3줄과 관련 주석 1줄만 제거했다. 음성 runtime/회귀시험의 bytes는 변경하지 않았다.

기본 `git diff --check`에는 Unity 생성 meta의 trailing whitespace와 `SnapshotFragments.cs` / `SnapshotFragmentTests.cs`의 EOF 빈 줄 경고가 남아 있다. 원래 bytes를 보존했으며 meta 제외·`blank-at-eof` 제외 검사만 통과했다. 기본 검사가 완전히 통과했다고 표현하지 않는다.

새 checkout에서 package resolve와 Unity compile은 **미검증**이다. 현재 로컬 시험은 게시에서 제외한 Demo 변경과 캐시가 있는 환경에서 실행됐으므로 선별 tree의 독립 재현을 보장하지 않는다. 특히 `MultiplayerSceneBuilder`가 호출하는 기존 Demo builder와 통합해 #120/#143/#144에서 확인해야 한다. 조회 당시 `develop`의 추가 13개 커밋과 이번 260개 변경 경로의 교집합은 없었으나 의미적 호환성까지 검증한 것은 아니다. 기존 작업트리와 원격 변경은 강제 초기화·rebase·merge하지 않았다.

따라서 PR은 **검토·팀 인계용 초안**으로 전달하며, 컴파일·장면·실서비스 검증과 담당 리뷰 전 병합하지 않는다. 이 문서의 신규·중단 상태 설명은 보드 인계를 위한 현재 사용자 지시 기록이지 과거 그래프 전체의 수용·해시를 갱신한 결과가 아니다.

## 남은 작업의 기존 진입점

| 작업 | 담당 역할 | 기존 이슈 | 다음 완료 조건 |
|---|---|---|---|
| 공개 층별 평면도 파일럿 | MAP | #119, #70 | 시설·층·발행 시점·이용 조건을 확인한 mapping과 한 구역 실제 모델, 축척/가정 검사 |
| 전체 오픈월드·장면 통합 | MAP/XR | #67, #76–#82 | 13구역과 층간/승하차 연결을 근거에 대조, collider·내비·늦은 참가·공유 상태 시험 |
| 서버 권위·복구 | XR | #27, #99 | 실제 프로토콜 참가자·checkpoint·순서·재접속 회귀와 새 실행본 |
| 실제 음성·운영 복구 | XR/QA | #100 | 실제 두 참가자 오디오/PTT·철회·old token·schema 전환·pending marker 복구 검증 |
| 열/연기 | XR | #101, #122–#123 | 현재 구획의 문/환기 반응과 서버 50ms 예산 안의 측정 |
| 군중100·열차 | XR | #59, #125–#130 | 실제 geometry 밀집/접촉/병목, 이동 frame·정지거리·점유·사건 연동 |
| 동시사건·협업·네비게이터 | XR/UX | #60, #35, #88, #131–#139 | 사건2건 상태 경합 없음, 보고/수신확인/인계·교관권한, 다층 안내와 연습/평가 배선 |
| 축소 리허설 | QA | #140 | source freeze 및 실행 가능한 coupled Player에서 실제 2~4 protocol client metrics; 3 client도 유효 |
| 전체 부하 | QA | #141 | 교관 포함20명/NPC100/사건2, 13구역 분산·2층 대합실 집결 각각60분 |
| 열화/음성 지연 | QA | #142 | 소유한 시험환경의 RTT100ms/손실1%, 행동 p95≤250ms, 실제 음성 p95≤300ms |
| 플랫폼과 독립 재현 | QA | #143, #144, #92 | Windows/Linux 빌드, 새로운 checkout의 의존성/실행 재현과 정제된 전달 manifest |

#143의 빌드 준비는 리허설 전에 필요하다. 최종 전달 판정은 #140 → #141 → #142 → 플랫폼별 최종 확인(#143) → #144 → #92 순서로 진행한다. 초기 목표는 서버20Hz, Windows1080p frame p95≤33.3ms이며 단위시험·짧은 시뮬레이션을 이 수용으로 바꾸지 않는다.

### 프로젝트에 등록한 별도 결함 카드

실행 보드: [CHOOguard 개발 실행](https://github.com/orgs/xrlab-dau/projects/1). 개인 담당자는 변경하지 않았으며 역할·우선순위·선행 조건·완료 조건을 등록했다.

1. [#149 · QA / P1](https://github.com/xrlab-dau/CHOOGuard/issues/149), 상위 #140 — Load causal contract R6B의 normative REWORK 5건과 실행 corpus: 독립 journal 관측 이름, exact variant dispatch, MetricsReader 역할, cleanup deadline 참조, 전체 deadline 경계의 양성 사례 가능성을 확정한다. CC00은 아직 수용되지 않았고 CC01–05는 채택되지 않았다.
2. [#150 · XR / P1](https://github.com/xrlab-dau/CHOOGuard/issues/150), 상위 #140 — `F(n)`/`U(n)`의 실제 Unity scheduler/metrics 결속: source-only accumulator 시험과 실제 참가자 수별 관측을 분리한다.
3. [#151 · XR / P0](https://github.com/xrlab-dau/CHOOGuard/issues/151), 상위 #100 — Voice config B-F1: 생성 staging을 pin하기 전 외부 디렉터리로 교체되는 경우 credential 쓰기·publication 자체를 막는 계약과 부정시험이 필요하다. 현재 trusted-parent 제한을 일반 안전성으로 확대하지 않는다.
4. [#152 · QA / P1](https://github.com/xrlab-dau/CHOOGuard/issues/152), 상위 #91 — 70 skipped fixture 원인 규명: discovery·filter·환경·guard를 실제 XML에 대조하고 필수 시험이 실행되게 한다. guard/시험 삭제나 skip 기대값 축소는 해결이 아니다.

## 오픈월드 제작의 필수 근거

실제 공개 평면도·층별 안내지도·도면을 바탕으로 모델링하며 임의 공간 배치로 완성 처리하지 않는다. 안내지도는 현재 실측 시공도면이 아니다. 확인된 외곽·구역·출입구·계단/에스컬레이터·층간 연결을 source ID에 연결하고, 미확인 치수·층고·구조·직원 SOP는 `공개 자료로 확정 불가`로 남긴다. 기존 합성 시제품도 근거와 대조해 수정한다. 코레일 자료 제공·현장 촬영을 기다리지 않으며 권리 미확인 원본 도면을 Git에 반입하지 않는다.

기존 개인 담당자는 유지한다. 보드의 역할·선행 조건은 인계 안내이며 새로운 권한 부여나 전체 Foundation 완료 선언이 아니다.
