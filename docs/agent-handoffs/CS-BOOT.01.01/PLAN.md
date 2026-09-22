# CS-BOOT.01.01 구현 계획

계획 readiness: **PLAN_READY** · 구현 상태: **IMPLEMENTED_NOT_VERIFIED** · revision `6-review-inventory-handoff` · candidate / fixture

승인 계획 `~/.claude/plans/distributed-churning-garden.md`를 구현한 현재 인계다. Windows Player 수용은 완료되지 않았으며 정본 acceptance/status를 변경하지 않는다. 이전 revision의 상세 계획·체크리스트·scratch 상태는 아래 보존 원본을 따른다. 과거 `NOT_PRODUCED`, root 부재, 복사 대기, claims 빈 목록은 현재 상태가 아니다.

## 현재 입력과 산출물

- 현재 sourceTreeDigest: `ebc9e92b0ee88779e10c6ea2c195cc1a3d22d7db8bca7198fc5c296f0cca23e0`
- 현재 evidence root: `docs/build/evidence/CS-BOOT.01.01/20260920T023553Z-09c7d4`
- 최종 검증: 해당 root의 `final-verification-repaired.json`
- 최종 원시 해시 목록: 해당 root의 `final-source-manifest.json`
- 정본 writes/outputs 80경로와 별도 승인 `.gitignore`가 root에 존재한다. `PLAN.json.outputs`의 현재 handoff 상태는 `PRODUCED_NOT_ACCEPTED`다. 정본 PlannedArtifact 상태는 수용 장부와 구별하여 그대로 둔다.
- `GENERATED_FILES.json`의 현재 disposition은 `RECONCILED_IN_ROOT`; 원래 scratch 경로·해시·미복사 disposition은 각 행의 `stageProvenance`에 보존한다.
- root package lock과 소유 scene/URP/TMP/metadata가 존재하며 실제 Unity 시험에서 검사했다.
- claim `CS-BOOT.01.01-candidate-fixture-20260920T010925Z-20b06c`: 수리 중 ACTIVE, 현재 **RELEASED**. `progress.records=[]`; ACCEPTED/SUBMITTED 없음.
- EXT-UNITY의 SATISFIED는 로컬 macOS Editor/기존 라이선스 관측만 의미한다.

## 실행 단계의 현재 상태

| 단계 | 현재 상태 | 증거와 한계 |
|---|---|---|
| Task1 경계·환경 | 완료, 환경 한계 기록 | 정본 소유권/asmdef 경계 동기화. 과거 이용 가능 계획 회귀 43/43, rdflib 부재 3건은 미실행 |
| Task1 TMP·하네스 | root 적용 및 시험 완료 | detached static TMP 7리소스와 metadata, 실제 컴파일된 초기 scene 부재 assertion RED 보존 |
| Task2 구현·TDD | 로컬 시험 완료 | 생성기/validator/baseline 구현, 최신 EditMode 51/51 및 PlayMode 1/1 |
| Task3 증거·인계 | 로컬 완료, Windows 미검증 | 새 Windows NOT_RUN CLI/receipt/baseline, strict12 PASS. 과거 소스 독립 검수 및 요청 수리 완료. 수정후 Terra/Sol 재검수 NOT_RUN |

## 유지한 구현 계약

- Editor `6000.3.23f1`; URP `17.3.0`, uGUI `2.0.0`, TMP `5.0.0`, Input System `1.20.0`, Test Framework `1.6.0`. 실제 resolved lock을 사용하며 자동 설치·라이선스 활성화 없음.
- Bootstrap scene에는 Camera 1개, Screen Space Overlay Canvas 1개, EventSystem 1개가 있다. CanvasScaler 1280×720 ScaleWithScreenSize 및 중앙 정적 TMP `CHOOGuard bootstrap` 표식, raycastTarget=false.
- InputSystemUIInputModule만 사용하며 영속 package UI action 참조와 실행 중 action 활성화를 검사한다. 실제 장치·IME 수용과는 다르다.
- 소유 BootstrapURP/Renderer/GlobalSettings 및 embedded 기본 VolumeProfile 사용. GraphicsSettings/QualitySettings가 소유 pipeline을 참조하고 제외된 기본 global/profile 자산은 root에 없다.
- static TMP SDF의 source/fallback 참조를 제거하고 font/material/atlas 및 재귀 asset 의존성 closure를 검사한다. 원본 폰트/demo/불필요 자원은 복사하지 않으며 OFL 원고지는 보존한다.
- Editor와 EditMode asmdef는 Editor 전용, PlayMode asmdef는 Editor 참조 없음. 제품 public API/schema 추가 또는 변경 없음.
- Capture는 제출물을 관측하고 scene setup을 보존한다. validator가 generator를 호출해 제출물을 자동 수리하지 않는다. 생성기는 dirty scene을 거부하고 saved owned Bootstrap만 필요한 시점에 닫은 후 기존 setup을 복원한다.
- baseline 및 receipt는 실제 package/editor/pipeline/hash를 기록한다. strict JSON 처리는 기존 planlib.read_json/safe_path와 jsonschema를 재사용한다.

## 이번 검수 후 수리

### 반환된 BuildReport의 부분 산출물

지원 target의 validator → BuildPlayer 순서를 유지하고, 반환된 report의 **존재하는 reported file 경로와 SHA256을 결과 판정 전에** 모은다. Failed/Cancelled는 FAILED receipt 및 예외 전파를 유지한다. null report/빌드 예외는 `UNOBSERVED`, GetFiles/hash 실패는 `INCOMPLETE` 이유를 기록하며 이미 구한 hash는 보존한다. 빈 outputs를 파일 부재 증거로 사용하지 않는다. 디렉터리 crawler·부분 산출물 삭제·공개 API/schema 변경은 없다.

기존 EditMode 파일의 private seam 시험은 BuildResult/열거 결과를 **모의**하고 실제 scratch 파일을 사용한다. 실제 Unity failed/cancelled BuildReport 통합 실행으로 표현하지 않는다.

| 검증 | 실제 관측 |
|---|---|
| RED `editmode-inventory-red` | Unity exit2, timeout=false, 46 total / 44 pass / 2 fail. Failed·Cancelled 각각 partial-file expected1 / actual0 |
| GREEN `editmode-final` | Unity exit0, timeout=false, 51/51 PASS; failed/skipped/inconclusive 모두 0 |
| `playmode-final` | Unity exit0, timeout=false, 1/1 PASS; failed/skipped/inconclusive 모두 0 |
| `windows-cli-not-run` | Unity exit0, build/run NOT_RUN, module=False / Windows host=False, 출력 없음 |
| `baseline-final` | Unity exit0, 새 Windows NOT_RUN baseline/receipt |
| `baseline-final-validation.json` | 실제 strict12/12 PASS |

정확한 명령·시각·sourceHashes·XML은 각 invocation 디렉터리에 보존된다. `-runTests`에 `-quit`를 붙이지 않았으며 render 시험에 `-nographics`를 사용하지 않았다. raw 로그는 라이선스 식별자 가능성 때문에 로컬 scratch에만 둔다.

### 빌드 출력 경계

`BuildDevelopmentPlayer`는 `-cgBuildTarget`, `-cgBuildRoot`, `-cgBuildOutput` 각각 하나의 명시적 값을 요구한다. target은 StandaloneOSX 또는 StandaloneWindows64, root/output은 절대경로다. root와 부모가 존재하고 symlink/reparse가 아니어야 하며 output은 root의 strict descendant로 아직 어떤 entry도 없어야 한다. 프로젝트와 root의 겹침, 대소문자 alias, 기존/매달린 link leaf를 거부한다.

순서는 인자/파일시스템 preflight → module/Windows host → 지원 target validator → BuildPlayer다. 미지원 Windows는 validator/빌드/설치/fallback 없이 NOT_RUN receipt를 남긴다. preflight 실패는 receipt/출력을 만들지 않는다. 악의적 filesystem race 방어까지 보장하지 않는다. 정확한 최신 실행은 `windows-cli-not-run/command.json`에 있다.

## 소스 해시 수집 범위

- 모든 raw command의 `sourceHashes`: Assets/Packages/ProjectSettings의 **제품 78개**, schema 제외.
- 최종 manifest와 receipt sourceTreeDigest: 제품 78개 + `docs/build/baseline.schema.json`의 **79개**. 정렬 경로 + NUL + SHA256 + newline을 해시한다. baseline/receipt/evidence 자체는 제외한다.
- 새 PlayMode/Windows CLI/baseline 명령은 별도 `schemaSha256`와 `collectionScope`를 실행 전에 기록한다.
- 이번 EditMode와 과거 명령은 schema의 실행 직전 해시를 기록하지 않았다. 제품78 동일성과 schema의 before/final 동일성은 확인했지만 이것을 모든 invocation의 79개 동시 수집으로 바꾸지 않는다.
- 과거 `final-verification.json.sameInputInvocations`도 제품78 비교의 증거다. 원시 파일을 덮어쓰지 않고 새 보고서 `historicalEvidenceNotes`에 한계를 기록했다.

## 독립 검수와 역사 증거

- Terra MEDIUM 1건, Sol 후속 MEDIUM 3건(PLAN 현재값, GENERATED_FILES disposition, 78/79 수집범위 표현): 개별 4건, 승인 수리 범주는 2개다.
- 두 reviewer의 검수 대상은 이전 digest `763c09ae5ab28f61a978f58fa5fe548184ee460622e1523a14e8f88ed83abbe3`이다. 수정후본을 재검수했다고 주장하지 않는다. Root의 별도 과거 baseline12 PASS도 이전 입력 증거다.
- Terra는 GUID/localID/persistence/동일 경로/membership identity oracle을 동일 소유 계약의 타당한 직접 검사로 판단했다. 숨김 flags와 IsSubAsset=false는 관측했지만 flag-toggle 인과 실험은 하지 않았다. 상세 oracle 대체안의 편집 전 승인을 받았다고 주장하지 않는다.
- 과거 Mac 개발 빌드는 별도 pre-build 입력의 성공 증거이고 Player 실행은 NOT_RUN이다. 전후 3파일의 hash 변화는 기록되었으나 직전 raw 사본이 없어 전체 field-level diff는 UNKNOWN이다. 현재 소스와 Mac output의 동일성은 미입증이다. 이번 수리에는 새 Mac full build를 수행하지 않았다.
- 최초 두 generator rerun의 field-level 비교는 UNKNOWN. 이후 raw 사본이 있는 saved-scene pair의 제한된 비교로 앞선 누락을 대체하지 않는다.
- 과거 baseline bytes: 현재 evidence root의 `historical-baseline.json` 및 `before/docs/build/baseline.json`에 보존. 원시 SHA256 `38988fdd332fdcb0ea38ea6aad08068b2eb25f91fe1520373b9a67e70bd72e8e`.
- 수리 전 handoff 전체는 현재 evidence root의 `before/docs/agent-handoffs/`에 보존했다. 이 PLAN의 이전 원문도 그 아래 `CS-BOOT.01.01/PLAN.md`에 있다. 이전 final-verification/command/XML/receipt는 그대로 유지한다.

## 미수용 상태와 종료

Windows native Player build/run NOT_RUN, Mac Player 실행 NOT_RUN, 실제 화면/포인터/OS IME MANUAL_NOT_RUN, standalone OFL 동봉 NOT_CONFIRMED, coverage NOT_MEASURED. graph는 갱신 금지로 code STALE / global semantic UNKNOWN이다. 임의 nonempty VolumeComponent cross-reference 복제 일반성도 입증하지 않았다.

실제 provider model/effort의 독립 확인은 **UNKNOWN**이다. 발진 script의 `claude-ocx-native--gpt-6-astra` / `low` 및 started agentId는 harness 설정 증거로만 인계한다. 같은 agent 재개이며 라우팅 변경은 없다.

현재 다음 동작은 Root의 인계 판정을 기다리는 것이다. 새 agent/Unity 병렬/기능/다음 story/Graphify 갱신/commit/stage/push/PR/외부 업로드는 수행하지 않는다. records=[] 및 claim RELEASED를 유지한다.
